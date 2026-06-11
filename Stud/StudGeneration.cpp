#include "StudInternal.h"
#include "JSONCommon.h"
#include "MCP.h"
#include <algorithm>
#include <cctype>
#include <chrono>
#include <condition_variable>
#include <cstdlib>
#include <cstring>
#include <thread>
extern "C" void CloseCommandPrompt();
extern "C" void StopCMDOutput();
extern "C" void MarkToolsJsonDirty();
using Clock = std::chrono::high_resolution_clock;
using PromptMessage = Stud::PromptMessage;
static char* CopyCString(const std::string& text){
	const auto size = text.size();
	auto* buffer = static_cast<char*>(std::malloc(size + 1));
	if(!buffer) return nullptr;
	std::memcpy(buffer, text.data(), size);
	buffer[size] = '\0';
	return buffer;
}
static bool TryExecuteMcpTool(const common_chat_tool_call& toolCall, std::string& toolResult){
	if(!MCPHasTool(toolCall.name.c_str())) return false;
	char* result = MCPExecuteTool(toolCall.name.c_str(), toolCall.arguments.c_str());
	if(!result){
		toolResult = "{\"error\":\"MCP tool execution failed\"}";
		return true;
	}
	try{ toolResult = result; } catch(...){ toolResult = "{\"error\":\"MCP tool execution failed\"}"; }
	std::free(result);
	return true;
}
static bool TryExecuteManagedTool(const char* callerSlotName, const common_chat_tool_call& toolCall, std::string& toolResult){
	const auto callback = Stud::managedToolCb;
	if(!callback) return false;
	char* result = callback(callerSlotName, toolCall.name.c_str(), toolCall.arguments.c_str());
	if(!result) return false;
	try{ toolResult = result; } catch(...){ toolResult = "{\"error\":\"managed tool execution failed\"}"; }
	LocalFree(result);
	return true;
}
static StudError RegisterApiToolSchemas(const char* slotName, const char* toolsJson){
	ClearTools(slotName);
	const char* json = SkipJsonWhitespace(toolsJson);
	if(!json || !*json) return StudError::Success;
	std::string toolsRoot(json);
	std::string toolsProperty;
	if(*json == '{' && GetJsonPropertyRaw(toolsRoot, "tools", toolsProperty)) toolsRoot = toolsProperty;
	json = SkipJsonWhitespace(toolsRoot.c_str());
	if(!json || !*json || IsJsonNull(toolsRoot)) return StudError::Success;
	const auto toolObjects = ExtractJsonObjects(json);
	if(toolObjects.empty()){
		if(*json == '[') return StudError::Success;
		Stud::Internal::LastErrorMessage() = "API tools must be a JSON object or array.";
		return StudError::ChatParseError;
	}
	for(const auto& toolObject : toolObjects){
		std::string type;
		GetJsonStringProperty(toolObject, "type", type);
		if(!type.empty() && type != "function") continue;
		std::string functionObject;
		std::string toolDefinition = toolObject;
		if(GetJsonPropertyRaw(toolObject, "function", functionObject)){
			const char* functionStart = SkipJsonWhitespace(functionObject.c_str());
			if(functionStart && *functionStart == '{') toolDefinition = functionObject;
		}
		std::string name;
		if(!GetJsonStringProperty(toolDefinition, "name", name) || name.empty()) continue;
		std::string description;
		GetJsonStringProperty(toolDefinition, "description", description);
		std::string parameters;
		if(!GetJsonPropertyRaw(toolDefinition, "parameters", parameters)) GetJsonPropertyRaw(toolDefinition, "input_schema", parameters);
		if(parameters.empty() || IsJsonNull(parameters)) parameters = "{}";
		AddTool(slotName, name.c_str(), description.c_str(), parameters.c_str(), nullptr);
	}
	return StudError::Success;
}
class ScopedApiTools{
public:
	explicit ScopedApiTools(const char* slotName) : _model(GetModel(slotName)), _toolsSnapshot(_model->session.tools), _handlersSnapshot(_model->session.toolHandlers), _syntaxSnapshot(_model->session.syntax){}
	~ScopedApiTools(){ Restore(); }
	void Restore(){
		if(_restored) return;
		_model->session.tools = std::move(_toolsSnapshot);
		_model->session.toolHandlers = std::move(_handlersSnapshot);
		_model->session.syntax = _syntaxSnapshot;
		MarkToolsJsonDirty();
		_restored = true;
	}
private:
	Stud::StudModel* _model;
	std::vector<common_chat_tool> _toolsSnapshot;
	std::unordered_map<std::string, Stud::ToolHandlerFn> _handlersSnapshot;
	common_chat_parser_params _syntaxSnapshot{};
	bool _restored = false;
};
static std::string NormalizeXmlToolBoolParameters(const std::string& response){
	std::string normalized = response;
	const std::string parameterTag = "<parameter=";
	const std::string closeTag = "</parameter>";
	size_t searchFrom = 0;
	while((searchFrom = normalized.find(parameterTag, searchFrom)) != std::string::npos){
		const size_t valueStart = normalized.find('>', searchFrom + parameterTag.size());
		if(valueStart == std::string::npos) break;
		const size_t closeStart = normalized.find(closeTag, valueStart + 1);
		if(closeStart == std::string::npos) break;
		size_t trimStart = valueStart + 1;
		size_t trimEnd = closeStart;
		while(trimStart < trimEnd && std::isspace(static_cast<unsigned char>(normalized[trimStart]))) ++trimStart;
		while(trimEnd > trimStart && std::isspace(static_cast<unsigned char>(normalized[trimEnd - 1]))) --trimEnd;
		const std::string value = normalized.substr(trimStart, trimEnd - trimStart);
		if(value == "True") normalized.replace(trimStart, value.size(), "true");
		else if(value == "False") normalized.replace(trimStart, value.size(), "false");
		searchFrom = closeStart + closeTag.size();
	}
	return normalized;
}
static common_chat_msg ParseGeneratedMessage(const std::string& response, const common_chat_parser_params& syntax, const bool isPartial, std::string* toolParseError = nullptr){
	const std::string normalizedResponse = syntax.parse_tool_calls ? NormalizeXmlToolBoolParameters(response) : response;
	try{ return common_chat_parse(normalizedResponse, isPartial, syntax); } catch(const std::exception& e){
		if(!isPartial && syntax.parse_tool_calls && toolParseError) *toolParseError = e.what();
		OutputDebugStringA((std::string("CHAT PARSE FALLBACK:\r\n") + e.what()).c_str());
		common_chat_parser_params fallback;
		fallback.reasoning_format = syntax.reasoning_format;
		fallback.reasoning_in_content = syntax.reasoning_in_content;
		fallback.parse_tool_calls = false;
		return common_chat_parse(response, isPartial, fallback);
	}
}
struct PendingToken{
	llama_token token;
	int memorySize;
};
class AsyncTokenPostProcessor{
public:
	AsyncTokenPostProcessor(const char* slotName, const Stud::TokenCallbackFn callback, const bool streamCallback, const common_chat_parser_params& chatSyntax, const Clock::time_point& preparationStart, std::string& responseText, common_chat_msg& parsedMessage, double& firstTokenTime) :
		_slotName(slotName), _model(GetModel(slotName)), _callback(callback), _streamCallback(streamCallback), _chatSyntax(chatSyntax), _preparationStart(preparationStart), _responseText(responseText), _parsedMessage(parsedMessage), _firstTokenTime(firstTokenTime), _queue(QueueCapacity){
		_chatSyntax.parse_tool_calls = false;
		_worker = std::thread([this]{ WorkerLoop(); });
	}
	~AsyncTokenPostProcessor(){ Close(); }
	StudError Error() const{ return _asyncError.load(std::memory_order_acquire); }
	void Close(){
		const bool wasClosed = _queueClosed.exchange(true, std::memory_order_acq_rel);
		if(!wasClosed) _queueCondition.notify_all();
		if(_worker.joinable()) _worker.join();
	}
	StudError Enqueue(const llama_token token, const int memorySize){
		for(;;){
			const StudError error = Error();
			if(error != StudError::Success) return error;
			const size_t head = _queueHead.load(std::memory_order_acquire);
			const size_t tail = _queueTail.load(std::memory_order_relaxed);
			if((tail - head) < QueueCapacity){
				_queue[tail % QueueCapacity] = PendingToken{token, memorySize};
				_queueTail.store(tail + 1, std::memory_order_release);
				if(tail == head) _queueCondition.notify_one();
				return StudError::Success;
			}
			std::this_thread::yield();
		}
	}
private:
	void EmitStreamingCallback(const int memorySize){
		if(!_callback || !_streamCallback || _pendingCallbackTokens <= 0) return;
		_parsedMessage = ParseGeneratedMessage(_responseText, _chatSyntax, true);
		_callback(_slotName, _parsedMessage.reasoning_content.c_str(), _parsedMessage.content.c_str(), _pendingCallbackTokens, memorySize, _firstTokenTime, 0);
		_pendingCallbackTokens = 0;
		_lastCallbackTime = std::chrono::steady_clock::now();
	}
	void WorkerLoop(){
		constexpr auto MinimumCallbackInterval = std::chrono::milliseconds(16);
		constexpr int MaximumTokensPerCallback = 8;
		int lastMemorySize = 0;
		for(;;){
			const size_t head = _queueHead.load(std::memory_order_relaxed);
			const size_t tail = _queueTail.load(std::memory_order_acquire);
			if(head == tail){
				if(_queueClosed.load(std::memory_order_acquire)){
					EmitStreamingCallback(lastMemorySize);
					return;
				}
				if(_pendingCallbackTokens > 0 && std::chrono::steady_clock::now() - _lastCallbackTime >= MinimumCallbackInterval) EmitStreamingCallback(lastMemorySize);
				std::unique_lock<std::mutex> lock(_queueWaitMutex);
				_queueCondition.wait_for(lock, std::chrono::milliseconds(1), [&](){ return _queueClosed.load(std::memory_order_acquire) || _queueHead.load(std::memory_order_relaxed) != _queueTail.load(std::memory_order_acquire); });
				continue;
			}
			const PendingToken pending = _queue[head % QueueCapacity];
			_queueHead.store(head + 1, std::memory_order_release);
			char buffer[256];
			const int length = llama_token_to_piece(_model->session.vocab, pending.token, buffer, sizeof buffer, 0, false);
			if(length < 0){
				_asyncError.store(StudError::CantConvertToken, std::memory_order_release);
				_queueClosed.store(true, std::memory_order_release);
				_queueCondition.notify_all();
				return;
			}
			if(_firstTokenTime == 0.0) _firstTokenTime = std::chrono::duration<double>(Clock::now() - _preparationStart).count();
			_responseText.append(buffer, static_cast<size_t>(length));
			if(length > 0){
				lastMemorySize = pending.memorySize;
				++_pendingCallbackTokens;
				const bool reachedBatchSize = _pendingCallbackTokens >= MaximumTokensPerCallback;
				const bool reachedRateLimit = std::chrono::steady_clock::now() - _lastCallbackTime >= MinimumCallbackInterval;
				if(reachedBatchSize || reachedRateLimit) EmitStreamingCallback(lastMemorySize);
			}
		}
	}
	const char* _slotName;
	static constexpr size_t QueueCapacity = 128;
	Stud::StudModel* _model;
	Stud::TokenCallbackFn _callback;
	bool _streamCallback;
	common_chat_parser_params _chatSyntax;
	Clock::time_point _preparationStart;
	std::string& _responseText;
	common_chat_msg& _parsedMessage;
	double& _firstTokenTime;
	std::vector<PendingToken> _queue;
	std::atomic<size_t> _queueHead{0};
	std::atomic<size_t> _queueTail{0};
	std::atomic<bool> _queueClosed{false};
	std::atomic<StudError> _asyncError{StudError::Success};
	std::mutex _queueWaitMutex;
	std::condition_variable _queueCondition;
	std::thread _worker;
	std::chrono::steady_clock::time_point _lastCallbackTime = std::chrono::steady_clock::now();
	int _pendingCallbackTokens = 0;
};
static StudError RollbackGenerate(const char* slotName, const size_t chatStart, const size_t newMessageCount, const size_t cacheStart, common_chat_msg& outputMessage, const StudError error){
	const auto model = GetModel(slotName);
	model->session.lane.messages.resize(chatStart + newMessageCount);
	model->session.lane.messageMedia.resize(chatStart + newMessageCount);
	if(Stud::Internal::RestoreCachedTokenPrefix(model, cacheStart)){
		outputMessage = common_chat_msg();
		return error;
	}
	const auto retokenizeError = RetokenizeChat(slotName, true);
	outputMessage = common_chat_msg();
	return retokenizeError != StudError::Success ? retokenizeError : error;
}
static StudError DecodePromptMessages(const char* slotName, const std::vector<PromptMessage>& messages, common_chat_msg& outputMessage){
	for(const auto& message : messages){
		const bool addAssistantPrompt = !message.message.role._Equal("assistant");
		const auto error = Stud::Internal::DecodeSinglePromptMessage(slotName, message, addAssistantPrompt);
		if(error != StudError::Success){
			outputMessage = common_chat_msg();
			return error;
		}
	}
	return StudError::Success;
}
static StudError Generate(const char* slotName, const std::vector<PromptMessage>& messages, const int predictionCount, const bool callback, common_chat_msg& outputMessage, const bool emitFinalCallback = true, std::string* toolParseError = nullptr){
	if(toolParseError) toolParseError->clear();
	const auto preparationStart = Clock::now();
	auto model = GetModel(slotName);
	auto& session = model->session;
	auto& lane = session.lane;
	session.stop.store(false);
	const Stud::TokenCallbackFn tokenCallback = Stud::tokenCb;
	const size_t chatStart = lane.messages.size();
	Stud::Internal::EnsureMessageMediaAligned(lane);
	const auto promptError = DecodePromptMessages(slotName, messages, outputMessage);
	if(promptError != StudError::Success) return promptError;
	const size_t cacheStart = lane.cachedTokens.size();
	const bool toolsEnabled = !session.tools.empty();
	common_chat_parser_params streamSyntax = session.syntax;
	if(callback){
		common_chat_params streamChatData;
		auto streamSyntaxError = Stud::Internal::BuildChatTemplateParams(slotName, streamChatData, lane.messages, true, false);
		if(streamSyntaxError == StudError::Success) streamSyntaxError = Stud::Internal::LoadChatSyntax(streamSyntax, streamChatData, false);
		if(streamSyntaxError != StudError::Success) return RollbackGenerate(slotName, chatStart, messages.size(), cacheStart, outputMessage, streamSyntaxError);
	} else streamSyntax.parse_tool_calls = false;
	std::string response;
	common_chat_msg message;
	double firstTokenTime = 0.0;
	AsyncTokenPostProcessor postProcessor(slotName, tokenCallback, callback, streamSyntax, preparationStart, response, message, firstTokenTime);
	auto failWith = [&](const StudError error){
		postProcessor.Close();
		return RollbackGenerate(slotName, chatStart, messages.size(), cacheStart, outputMessage, error);
	};
	if(session.speculative) common_speculative_begin(session.speculative, 0, lane.cachedTokens);
	std::vector<llama_token> draft;
	std::vector<llama_token> decodeTokens;
	if(session.speculative){
		draft.reserve(session.mtpDraftTokens);
		decodeTokens.reserve(static_cast<size_t>(session.mtpDraftTokens) + 1);
	}
	int generatedCount = 0;
	int sampleIndex = -1;
	bool endOfGeneration = false;
	while((predictionCount < 0 || generatedCount < predictionCount) && !endOfGeneration && !session.stop.load()){
		const StudError pendingError = postProcessor.Error();
		if(pendingError != StudError::Success) return failWith(pendingError);
		const int positionStart = static_cast<int>(lane.cachedTokens.size());
		if(positionStart + 1 > static_cast<int>(llama_n_ctx(session.ctx))) return failWith(StudError::ConvTooLong);
		const llama_token sampledToken = llama_sampler_sample(lane.sampler, session.ctx, sampleIndex);
		if(sampledToken == LLAMA_TOKEN_NULL) return failWith(StudError::LlamaDecodeError);
		const bool sampledEndOfGeneration = llama_vocab_is_eog(session.vocab, sampledToken);
		draft.clear();
		decodeTokens.clear();
		decodeTokens.push_back(sampledToken);
		int acceptedDrafts = 0;
		bool hasRejectedToken = false;
		llama_token rejectedToken = LLAMA_TOKEN_NULL;
		if(session.speculative && !sampledEndOfGeneration){
			const int contextRoom = static_cast<int>(llama_n_ctx(session.ctx)) - positionStart - 1;
			const int batchRoom = std::max(0, static_cast<int>(llama_n_batch(session.ctx)) - 1);
			const int predictionRoom = predictionCount < 0 ? session.mtpDraftTokens : std::max(0, predictionCount - generatedCount - 1);
			const int draftMaximum = std::min({session.mtpDraftTokens, contextRoom, batchRoom, predictionRoom});
			if(draftMaximum == session.mtpDraftTokens && draftMaximum > 0){
				auto& draftParams = common_speculative_get_draft_params(session.speculative, 0);
				draftParams.drafting = true;
				draftParams.n_max = draftMaximum;
				draftParams.n_past = static_cast<llama_pos>(positionStart);
				draftParams.id_last = sampledToken;
				draftParams.prompt = &lane.cachedTokens;
				draftParams.result = &draft;
				common_speculative_draft(session.speculative);
				if(!Stud::Internal::RemoveMtpMemoryAfter(session, positionStart)) return failWith(StudError::LlamaDecodeError);
				const auto endOfGenerationDraft = std::find_if(draft.begin(), draft.end(), [&](const llama_token token){ return llama_vocab_is_eog(session.vocab, token); });
				if(endOfGenerationDraft != draft.end()) draft.erase(endOfGenerationDraft + 1, draft.end());
				if(!draft.empty()) decodeTokens.insert(decodeTokens.end(), draft.begin(), draft.end());
			}
		}
		const auto decodeError = Stud::Internal::DecodeTokenBatch(session, decodeTokens.data(), static_cast<int>(decodeTokens.size()), positionStart, true);
		if(decodeError != StudError::Success) return failWith(decodeError);
		if(!draft.empty()){
			for(size_t draftIndex = 0; draftIndex < draft.size(); ++draftIndex){
				const auto targetToken = llama_sampler_sample(lane.sampler, session.ctx, static_cast<int32_t>(draftIndex));
				if(targetToken == LLAMA_TOKEN_NULL) return failWith(StudError::LlamaDecodeError);
				if(targetToken != draft[draftIndex]){
					rejectedToken = targetToken;
					hasRejectedToken = true;
					break;
				}
				++acceptedDrafts;
				if(llama_vocab_is_eog(session.vocab, targetToken)) break;
			}
			common_speculative_accept(session.speculative, 0, static_cast<uint16_t>(acceptedDrafts));
			if(acceptedDrafts < static_cast<int>(draft.size()) && !Stud::Internal::RemoveSessionMemoryAfter(session, positionStart + 1 + acceptedDrafts)) return failWith(StudError::LlamaDecodeError);
			if(hasRejectedToken){
				const auto rejectedDecodeError = Stud::Internal::DecodeTokenBatch(session, &rejectedToken, 1, positionStart + 1 + acceptedDrafts, true);
				if(rejectedDecodeError != StudError::Success) return failWith(rejectedDecodeError);
				sampleIndex = -1;
			} else sampleIndex = acceptedDrafts;
		} else sampleIndex = -1;
		const int acceptedCount = 1 + acceptedDrafts + (hasRejectedToken ? 1 : 0);
		for(int acceptedIndex = 0; acceptedIndex < acceptedCount && (predictionCount < 0 || generatedCount < predictionCount); ++acceptedIndex){
			const llama_token token = acceptedIndex == 0 ? sampledToken : acceptedIndex <= acceptedDrafts ? draft[acceptedIndex - 1] : rejectedToken;
			lane.cachedTokens.push_back(token);
			if(llama_vocab_is_eog(session.vocab, token)){
				endOfGeneration = true;
				break;
			}
			const auto enqueueError = postProcessor.Enqueue(token, static_cast<int>(lane.cachedTokens.size()));
			if(enqueueError != StudError::Success) return failWith(enqueueError);
			++generatedCount;
		}
	}
	postProcessor.Close();
	if(postProcessor.Error() != StudError::Success) return RollbackGenerate(slotName, chatStart, messages.size(), cacheStart, outputMessage, postProcessor.Error());
	common_chat_params finalChatData;
	auto finalSyntaxError = Stud::Internal::BuildChatTemplateParams(slotName, finalChatData, lane.messages, true, toolsEnabled);
	if(finalSyntaxError == StudError::Success) finalSyntaxError = Stud::Internal::LoadChatSyntax(session.syntax, finalChatData, toolsEnabled);
	if(finalSyntaxError != StudError::Success) return RollbackGenerate(slotName, chatStart, messages.size(), cacheStart, outputMessage, finalSyntaxError);
	try{
		message = ParseGeneratedMessage(response, session.syntax, false, toolParseError);
		Stud::Internal::EnsureToolCallIds(message);
	} catch(const std::exception& e){
		Stud::Internal::LastErrorMessage() = e.what();
		return RollbackGenerate(slotName, chatStart, messages.size(), cacheStart, outputMessage, StudError::ChatParseError);
	}
	lane.messages.push_back(message);
	lane.messageMedia.emplace_back();
	if(emitFinalCallback && tokenCallback && !callback) tokenCallback(slotName, message.reasoning_content.c_str(), message.content.c_str(), generatedCount, static_cast<int>(lane.cachedTokens.size()), firstTokenTime, 0);
	outputMessage = std::move(message);
	return StudError::Success;
}
static StudError GenerateWithToolsPrompt(const char* slotName, PromptMessage input, const int predictionCount, const bool callback){
	auto model = GetModel(slotName);
	common_chat_msg message;
	std::vector<PromptMessage> messages{std::move(input)};
	if(model->session.tools.empty()) return Generate(slotName, messages, predictionCount, callback, message);
	const Stud::TokenCallbackFn tokenCallback = Stud::tokenCb;
	bool toolCalled;
	const auto addToolResult = [&](const std::string& toolMessage){
		if(tokenCallback) tokenCallback(slotName, nullptr, toolMessage.c_str(), 0, LlamaMemSize(slotName), 0, 1);
		messages.push_back(PromptMessage());
		messages.back().message.role = "tool";
		messages.back().message.content = toolMessage;
		toolCalled = true;
	};
	do{
		std::string toolParseError;
		const auto error = Generate(slotName, messages, predictionCount, callback, message, true, &toolParseError);
		if(error != StudError::Success) return error;
		if(!model->session.lane.messages.size()) return StudError::Success;
		messages.clear();
		try{
			toolCalled = false;
			if(!toolParseError.empty()) addToolResult("{\"error\":\"Unable to parse tool call: " + JsonEscape(toolParseError) + "\"}");
			for(common_chat_tool_call& toolCall : message.tool_calls){
				if(model->session.stop.load()) return StudError::Success;
				const auto toolCallMessage = "Tool name: " + toolCall.name + "\r\nTool ID: " + toolCall.id + "\r\nTool arguments: " + toolCall.arguments;
				if(tokenCallback) tokenCallback(slotName, nullptr, toolCallMessage.c_str(), 0, LlamaMemSize(slotName), 0, 3);
				const auto handler = model->session.toolHandlers.find(toolCall.name);
				if(handler != model->session.toolHandlers.end()){
					std::string toolMessage;
					try{ toolMessage = handler->second(slotName, toolCall.arguments.c_str()); } catch(const std::exception& e){ toolMessage = "{\"error\":\"Tool execution failed: " + JsonEscape(e.what()) + "\"}"; } catch(...){ toolMessage = "{\"error\":\"Tool execution failed\"}"; }
					addToolResult(toolMessage);
				} else{
					std::string toolMessage;
					if(TryExecuteMcpTool(toolCall, toolMessage)) addToolResult(toolMessage);
					else if(TryExecuteManagedTool(slotName, toolCall, toolMessage)) addToolResult(toolMessage);
				}
			}
		} catch(const std::exception& e){
			Stud::Internal::LastErrorMessage() = e.what();
			return StudError::ChatParseError;
		}
	} while(toolCalled);
	return StudError::Success;
}
static std::string BuildApiResponseJson(const common_chat_msg& message){
	const bool hasContent = !message.content.empty();
	const bool hasToolCalls = !message.tool_calls.empty();
	std::string json = "{";
	json += "\"role\":\"assistant\",";
	json += "\"content\":" + JsonString(message.content) + ",";
	json += "\"reasoning\":" + JsonString(message.reasoning_content) + ",";
	json += "\"finish_reason\":\"";
	json += hasToolCalls ? "tool_calls" : "stop";
	json += "\",";
	json += "\"tool_calls\":[";
	for(size_t i = 0; i < message.tool_calls.size(); ++i){
		const auto& toolCall = message.tool_calls[i];
		if(i > 0) json += ",";
		json += "{\"id\":" + JsonString(toolCall.id) + ",\"type\":\"function\",\"function\":{";
		json += "\"name\":" + JsonString(toolCall.name) + ",";
		json += "\"arguments\":" + JsonString(toolCall.arguments);
		json += "}}";
	}
	json += "],\"output\":[";
	bool hasOutputItem = false;
	if(hasContent){
		json += "{\"type\":\"message\",\"status\":\"completed\",\"role\":\"assistant\",\"content\":[{\"type\":\"output_text\",\"text\":" + JsonString(message.content) + "}]}";
		hasOutputItem = true;
	}
	for(const auto& toolCall : message.tool_calls){
		if(hasOutputItem) json += ",";
		json += "{\"type\":\"function_call\",\"status\":\"completed\",";
		json += "\"call_id\":" + JsonString(toolCall.id) + ",";
		json += "\"name\":" + JsonString(toolCall.name) + ",";
		json += "\"arguments\":" + JsonString(toolCall.arguments);
		json += "}";
		hasOutputItem = true;
	}
	json += "]}";
	return json;
}
static StudError GenerateForApiPrompt(const char* slotName, PromptMessage inputMessage, const char* toolsJson, const int predictionCount, const bool callback, char** responseJson){
	if(responseJson) *responseJson = nullptr;
	if(!responseJson){
		Stud::Internal::LastErrorMessage() = "responseJson is null.";
		return StudError::Generic;
	}
	ScopedApiTools apiTools(slotName);
	const auto toolsError = RegisterApiToolSchemas(slotName, toolsJson);
	if(toolsError != StudError::Success) return toolsError;
	const auto retokenizeError = RetokenizeChat(slotName, false);
	if(retokenizeError != StudError::Success) return retokenizeError;
	const std::vector<PromptMessage> messages{std::move(inputMessage)};
	common_chat_msg outputMessage;
	const auto generateError = Generate(slotName, messages, predictionCount, callback, outputMessage, false);
	if(generateError != StudError::Success) return generateError;
	*responseJson = CopyCString(BuildApiResponseJson(outputMessage));
	if(!*responseJson){
		Stud::Internal::LastErrorMessage() = "Unable to allocate API generation response.";
		return StudError::Generic;
	}
	return StudError::Success;
}
void AddTool(const char* slotName, const char* name, const char* description, const char* parameters, const Stud::ToolHandlerFn handler){
	if(!name) return;
	const auto model = GetModel(slotName);
	common_chat_tool tool;
	tool.name = name;
	if(description) tool.description = description;
	if(parameters) tool.parameters = parameters;
	model->session.tools.push_back(tool);
	if(handler) model->session.toolHandlers[name] = handler;
	MarkToolsJsonDirty();
}
void ClearTools(const char* slotName){
	const auto model = GetModel(slotName);
	CloseCommandPrompt();
	model->session.tools.clear();
	model->session.toolHandlers.clear();
	MarkToolsJsonDirty();
}
bool HasTool(const char* slotName, const char* name){
	const auto model = GetModel(slotName);
	for(const auto& tool : model->session.tools) if(tool.name._Equal(name)) return true;
	return false;
}
extern "C" EXPORT char* ExecuteTool(const char* slotName, const char* name, const char* argumentsJson){
	if(!name || name[0] == '\0') return CopyCString("{\"error\":\"missing tool name\"}");
	const auto model = GetModel(slotName);
	const auto handler = model->session.toolHandlers.find(name);
	if(handler == model->session.toolHandlers.end() || !handler->second) return CopyCString("{\"error\":\"unknown tool\"}");
	try{
		const std::string response = handler->second(slotName, argumentsJson ? argumentsJson : "");
		return CopyCString(response);
	} catch(const std::exception& e){ return CopyCString(std::string("{\"error\":\"") + JsonEscape(e.what()) + "\"}"); } catch(...){ return CopyCString("{\"error\":\"tool execution failed\"}"); }
}
extern "C" EXPORT void SetManagedToolCallback(const Stud::ManagedToolCallbackFn callback){ Stud::managedToolCb = callback; }
extern "C" EXPORT void StreamManagedToolOutput(const char* slotName, const char* text){
	if(!text || !*text) return;
	const auto callback = Stud::tokenCb;
	if(!callback) return;
	callback(slotName, nullptr, text, 0, LlamaMemSize(slotName), 0.0, 2);
}
StudError GenerateWithTools(const char* slotName, const Stud::MessageRole role, const char* prompt, const int predictionCount, const bool callback){
	PromptMessage input;
	input.message.role = Stud::Internal::RoleString(role);
	input.message.content = prompt ? std::string(prompt) : std::string();
	return GenerateWithToolsPrompt(slotName, std::move(input), predictionCount, callback);
}
StudError GenerateWithToolsJson(const char* slotName, const Stud::MessageRole role, const char* contentJson, const int predictionCount, const bool callback){
	PromptMessage input;
	const auto parseError = Stud::Internal::ParseContentJson(role, "", contentJson, input);
	if(parseError != StudError::Success) return parseError;
	return GenerateWithToolsPrompt(slotName, std::move(input), predictionCount, callback);
}
StudError GenerateForAPI(const char* slotName, const Stud::MessageRole role, const char* prompt, const char* toolsJson, const int predictionCount, const bool callback, char** responseJson){
	PromptMessage input;
	input.message.role = Stud::Internal::RoleString(role);
	input.message.content = prompt ? std::string(prompt) : std::string();
	return GenerateForApiPrompt(slotName, std::move(input), toolsJson, predictionCount, callback, responseJson);
}
StudError GenerateForAPIJson(const char* slotName, const Stud::MessageRole role, const char* contentJson, const char* toolsJson, const int predictionCount, const bool callback, char** responseJson){
	PromptMessage input;
	const auto parseError = Stud::Internal::ParseContentJson(role, "", contentJson, input);
	if(parseError != StudError::Success) return parseError;
	return GenerateForApiPrompt(slotName, std::move(input), toolsJson, predictionCount, callback, responseJson);
}
void StopGeneration(const char* slotName){
	GetModel(slotName)->session.stop.store(true);
	StopCMDOutput();
}
