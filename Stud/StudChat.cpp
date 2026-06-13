#include "StudInternal.h"
#include "JSONCommon.h"
#include <algorithm>
#include <chrono>
#include <cstdint>
#include <cstdlib>
#include <cstring>
#include <mtmd-helper.h>
#include <new>
using PromptMessage = Stud::PromptMessage;
static bool LaneHasMedia(const Stud::StudLane& lane){ return std::any_of(lane.messageMedia.begin(), lane.messageMedia.end(), [](const Stud::MessageMedia& media){ return !media.empty(); }); }
static bool TokenizePrompt(const char* slotName, const std::string& prompt, std::vector<llama_token>& tokens, const bool addSpecial = true){
	try{
		tokens = common_tokenize(GetModel(slotName)->session.vocab, prompt, addSpecial, true);
		return true;
	} catch(const std::exception& e){
		Stud::Internal::LastErrorMessage() = e.what();
		return false;
	}
}
static std::string GenerateToolCallId(){
	using Clock = std::chrono::high_resolution_clock;
	static std::atomic<unsigned long long> counter{0};
	const auto id = counter.fetch_add(1, std::memory_order_relaxed);
	const auto ticks = std::chrono::duration_cast<std::chrono::microseconds>(Clock::now().time_since_epoch()).count();
	return "call_" + std::to_string(ticks) + "_" + std::to_string(id);
}
static std::string MediaHash(const mtmd_bitmap* bitmap){
	const unsigned char* data = mtmd_bitmap_get_data(bitmap);
	const size_t size = mtmd_bitmap_get_n_bytes(bitmap);
	uint64_t hash = 14695981039346656037ULL;
	for(size_t i = 0; i < size; ++i){
		hash ^= data[i];
		hash *= 1099511628211ULL;
	}
	return std::to_string(hash);
}
static int32_t EvaluateVisionChunksGuarded(mtmd_context* visionContext, llama_context* context, const mtmd_input_chunks* chunks, const int32_t batchSize, llama_pos* newPast){
#if defined(_WIN32)
	__try{ return mtmd_helper_eval_chunks(visionContext, context, chunks, 0, 0, batchSize, true, newPast); } __except(GetExceptionCode() == EXCEPTION_ACCESS_VIOLATION ? EXCEPTION_EXECUTE_HANDLER : EXCEPTION_CONTINUE_SEARCH){ return INT32_MIN; }
#else
	return mtmd_helper_eval_chunks(visionContext, context, chunks, 0, 0, batchSize, true, newPast);
#endif
}
static StudError EvaluateVisionPrompt(const char* slotName, const std::vector<common_chat_msg>& messages, const std::vector<Stud::MessageMedia>& messageMedia, const bool addGenerationPrompt, const bool includeTools){
	const auto model = GetModel(slotName);
	auto& session = model->session;
	auto& lane = session.lane;
	auto& errorMessage = Stud::Internal::LastErrorMessage();
	if(!session.visionCtx){
		errorMessage = "This local model slot has no multimodal projector loaded.";
		return StudError::VisionProjectorNotLoaded;
	}
	common_chat_params chatData;
	auto error = Stud::Internal::BuildChatTemplateParams(slotName, chatData, messages, addGenerationPrompt, includeTools);
	if(error != StudError::Success) return error;
	error = Stud::Internal::LoadChatSyntax(session.syntax, chatData, includeTools && !session.tools.empty());
	if(error != StudError::Success) return error;
	mtmd::bitmaps bitmaps;
	for(const auto& media : messageMedia){
		for(const auto& image : media){
			mtmd::bitmap bitmap(mtmd_helper_bitmap_init_from_buf(session.visionCtx, image.data(), image.size()));
			if(!bitmap.ptr){
				errorMessage = "The multimodal projector could not decode an image.";
				return StudError::CantDecodeImage;
			}
			const auto hash = MediaHash(bitmap.ptr.get());
			bitmap.set_id(hash.c_str());
			bitmaps.entries.push_back(std::move(bitmap));
		}
	}
	const mtmd_input_text input{chatData.prompt.c_str(), true, true};
	const mtmd::input_chunks chunks(mtmd_input_chunks_init());
	auto bitmapPointers = bitmaps.c_ptr();
	const int32_t tokenizeResult = mtmd_tokenize(session.visionCtx, chunks.ptr.get(), &input, bitmapPointers.data(), bitmapPointers.size());
	if(tokenizeResult != 0){
		errorMessage = tokenizeResult == 1 ? "The number of image markers did not match the supplied images." : "The multimodal projector could not preprocess an image.";
		return StudError::CantDecodeImage;
	}
	const llama_pos totalPositions = mtmd_helper_get_n_pos(chunks.ptr.get());
	if(totalPositions > static_cast<llama_pos>(llama_n_ctx(session.ctx))) return StudError::ConvTooLong;
	Stud::Internal::ClearSessionMemoryAndSpeculativeState(model);
	llama_pos newPast = 0;
	Stud::Internal::GpuOomDetected() = false;
	errorMessage.clear();
	int32_t evaluationResult;
	{
		Stud::Internal::ScopedBackendErrorCapture logCapture;
		evaluationResult = EvaluateVisionChunksGuarded(session.visionCtx, session.ctx, chunks.ptr.get(), session.batchSize, &newPast);
	}
	if(evaluationResult != 0){
		Stud::Internal::ClearSessionMemoryAndSpeculativeState(model);
		lane.cachedTokens.clear();
		if(lane.sampler) llama_sampler_reset(lane.sampler);
		if(evaluationResult == INT32_MIN && errorMessage.empty()) errorMessage = "The vision encoder failed after a GPU allocation error.";
		else if(errorMessage.empty()) errorMessage = "The multimodal prompt could not be evaluated.";
		if(Stud::Internal::GpuOomDetected()) return StudError::GpuOutOfMemory;
		return evaluationResult == 1 ? StudError::ConvTooLong : StudError::LlamaDecodeError;
	}
	lane.cachedTokens.clear();
	lane.cachedTokens.reserve(static_cast<size_t>(newPast));
	if(lane.sampler) llama_sampler_reset(lane.sampler);
	for(size_t i = 0; i < chunks.size(); ++i){
		const auto chunk = chunks[i];
		if(mtmd_input_chunk_get_type(chunk) == MTMD_INPUT_CHUNK_TYPE_TEXT){
			size_t tokenCount = 0;
			const llama_token* tokens = mtmd_input_chunk_get_tokens_text(chunk, &tokenCount);
			for(size_t tokenIndex = 0; tokenIndex < tokenCount; ++tokenIndex){
				lane.cachedTokens.push_back(tokens[tokenIndex]);
				if(lane.sampler) llama_sampler_accept(lane.sampler, tokens[tokenIndex]);
			}
		} else{
			const llama_pos positions = mtmd_input_chunk_get_n_pos(chunk);
			lane.cachedTokens.insert(lane.cachedTokens.end(), static_cast<size_t>(positions), LLAMA_TOKEN_NULL);
		}
	}
	if(lane.cachedTokens.size() != static_cast<size_t>(newPast)){
		errorMessage = "The multimodal prompt position count was inconsistent.";
		Stud::Internal::ClearSessionMemoryAndSpeculativeState(model);
		lane.cachedTokens.clear();
		if(lane.sampler) llama_sampler_reset(lane.sampler);
		return StudError::LlamaDecodeError;
	}
	return StudError::Success;
}
static size_t CommonTokenPrefix(const std::vector<llama_token>& left, const std::vector<llama_token>& right){
	size_t prefix = 0;
	while(prefix < left.size() && prefix < right.size() && left[prefix] == right[prefix]) ++prefix;
	return prefix;
}
static bool TryBuildAppendOnlyPrompt(const char* slotName, const std::string& renderedPrompt, const bool addAssistantPrompt, std::vector<llama_token>& promptTokens, size_t& prefix){
	const auto model = GetModel(slotName);
	const auto& lane = model->session.lane;
	if(!lane.endsWithEOG || lane.messages.empty() || lane.cachedTokens.empty()) return false;
	common_chat_params previousChatData;
	const bool toolsEnabled = !model->session.tools.empty();
	if(Stud::Internal::BuildChatTemplateParams(slotName, previousChatData, lane.messages, false, toolsEnabled) != StudError::Success) return false;
	if(renderedPrompt.compare(0, previousChatData.prompt.size(), previousChatData.prompt) != 0) return false;
	std::string suffix;
	if(addAssistantPrompt && !previousChatData.prompt.empty() && previousChatData.prompt.back() == '\n') suffix = "\n";
	suffix.append(renderedPrompt, previousChatData.prompt.size(), std::string::npos);
	std::vector<llama_token> suffixTokens;
	if(!TokenizePrompt(slotName, suffix, suffixTokens, false)) return false;
	const size_t cachedSize = lane.cachedTokens.size();
	promptTokens = lane.cachedTokens;
	promptTokens.reserve(cachedSize + suffixTokens.size());
	promptTokens.insert(promptTokens.end(), suffixTokens.begin(), suffixTokens.end());
	prefix = cachedSize;
	return true;
}
static bool RestorePromptPrefix(Stud::StudModel* model, const std::vector<llama_token>& promptTokens, const size_t prefix){
	auto& session = model->session;
	auto& lane = session.lane;
	if(prefix > promptTokens.size()) return false;
	if(!Stud::Internal::RemoveSessionMemoryAfter(session, static_cast<llama_pos>(prefix))){
		Stud::Internal::ClearSessionMemoryAndSpeculativeState(model);
		lane.cachedTokens.clear();
		if(lane.sampler) llama_sampler_reset(lane.sampler);
		return false;
	}
	if(prefix == 0) Stud::Internal::RecreateSpeculativeContext(model);
	else Stud::Internal::FreeSpeculativeContext(session);
	lane.cachedTokens.assign(promptTokens.begin(), promptTokens.begin() + prefix);
	if(lane.sampler){
		llama_sampler_reset(lane.sampler);
		for(size_t i = 0; i < prefix; ++i) llama_sampler_accept(lane.sampler, promptTokens[i]);
	}
	return true;
}
static StudError DecodePromptTokenSuffix(const char* slotName, const std::vector<llama_token>& promptTokens, const size_t prefix, const common_chat_msg* appendedMessage = nullptr){
	const auto model = GetModel(slotName);
	auto& session = model->session;
	auto& lane = session.lane;
	if(prefix > lane.cachedTokens.size() || prefix > promptTokens.size()) return StudError::Generic;
	if(appendedMessage){
		lane.messages.push_back(*appendedMessage);
		lane.messageMedia.emplace_back();
	}
	if(promptTokens.size() > lane.cachedTokens.size()) lane.cachedTokens.reserve(promptTokens.size());
	size_t position = prefix;
	while(position < promptTokens.size() && !session.stop.load()){
		const int evaluationCount = std::min<int>(Stud::Internal::EvalBatchSize(session), static_cast<int>(promptTokens.size() - position));
		const int contextSize = llama_n_ctx(session.ctx);
		const int usedContext = static_cast<int>(lane.cachedTokens.size());
		if(usedContext + evaluationCount > contextSize){
			if(appendedMessage){
				lane.messages.pop_back();
				lane.messageMedia.pop_back();
			}
			RestorePromptPrefix(model, promptTokens, prefix);
			return StudError::ConvTooLong;
		}
		const auto result = Stud::Internal::DecodeTokenBatch(session, const_cast<llama_token*>(&promptTokens[position]), evaluationCount, static_cast<llama_pos>(position), true);
		if(result != StudError::Success){
			if(appendedMessage){
				lane.messages.pop_back();
				lane.messageMedia.pop_back();
			}
			RestorePromptPrefix(model, promptTokens, prefix);
			return result;
		}
		for(int i = 0; i < evaluationCount; ++i) llama_sampler_accept(lane.sampler, promptTokens[position + i]);
		lane.cachedTokens.insert(lane.cachedTokens.end(), promptTokens.begin() + position, promptTokens.begin() + position + evaluationCount);
		position += evaluationCount;
	}
	return StudError::Success;
}
static common_chat_msg MirrorDialecticMessage(common_chat_msg message){
	if(message.role._Equal("assistant")){
		message.role = "user";
		message.reasoning_content.clear();
	} else if(message.role._Equal("user")){ message.role = "assistant"; }
	return message;
}
static bool AlignDialecticRelayChatStates(const Stud::StudModel& source, Stud::StudModel& target){
	const auto& sourceMessages = source.session.lane.messages;
	const auto& sourceMedia = source.session.lane.messageMedia;
	auto& targetMessages = target.session.lane.messages;
	auto& targetMedia = target.session.lane.messageMedia;
	targetMedia.resize(targetMessages.size());
	auto sourceLimit = sourceMessages.size();
	if(sourceLimit > 0 && sourceMessages.back().role._Equal("assistant")) --sourceLimit;
	if(targetMessages.size() >= sourceLimit) return false;
	for(size_t i = targetMessages.size(); i < sourceLimit; ++i){
		targetMessages.push_back(MirrorDialecticMessage(sourceMessages[i]));
		targetMedia.push_back(i < sourceMedia.size() ? sourceMedia[i] : Stud::MessageMedia());
	}
	return true;
}
static void RestoreSamplerFromCachedTokens(const Stud::StudSession& session){
	if(!session.lane.sampler) return;
	llama_sampler_reset(session.lane.sampler);
	for(const llama_token token : session.lane.cachedTokens) if(token != LLAMA_TOKEN_NULL) llama_sampler_accept(session.lane.sampler, token);
}
static StudError ReplaceChatMessages(const char* slotName, std::vector<common_chat_msg>&& messages, std::vector<Stud::MessageMedia>&& media = {}){
	for(auto& message : messages) Stud::Internal::EnsureToolCallIds(message);
	const auto& session = GetModel(slotName)->session;
	auto& lane = Stud::Internal::ActiveLane(slotName);
	lane.messages = std::move(messages);
	lane.messageMedia = std::move(media);
	Stud::Internal::EnsureMessageMediaAligned(lane);
	if(!session.ctx || !lane.sampler || !session.vocab){
		lane.cachedTokens.clear();
		return StudError::Success;
	}
	return RetokenizeChat(slotName, false);
}
namespace Stud::Internal{
	StudLane& ActiveLane(const char* slotName){ return GetModel(slotName)->session.lane; }
	std::string RoleString(const MessageRole role){
		switch(role){
			case MessageRole::User: return std::string("user");
			case MessageRole::Assistant: return std::string("assistant");
			case MessageRole::Tool: return std::string("tool");
			default: return std::string();
		}
	}
	void EnsureMessageMediaAligned(StudLane& lane){ lane.messageMedia.resize(lane.messages.size()); }
	void EnsureToolCallIds(common_chat_msg& message){ for(auto& toolCall : message.tool_calls) if(toolCall.id.empty()) toolCall.id = GenerateToolCallId(); }
	StudError ParseContentJson(const MessageRole role, const char* reasoning, const char* contentJson, PromptMessage& result){ return ParseChatContentJson(RoleString(role), reasoning, contentJson, result, LastErrorMessage()); }
	StudError BuildChatTemplateParams(const char* slotName, common_chat_params& chatData, const std::vector<common_chat_msg>& messages, const bool addGenerationPrompt, const bool includeTools){
		const auto model = GetModel(slotName);
		const auto hasUser = std::any_of(messages.begin(), messages.end(), [](const common_chat_msg& message){ return message.role == "user"; });
		if(!hasUser){
			chatData = common_chat_params();
			return StudError::Success;
		}
		const bool useTools = includeTools && !model->session.tools.empty();
		std::vector<common_chat_msg> templatedMessages;
		std::string prompt(model->session.systemPrompt);
		if(useTools && !model->session.toolsPrompt.empty()) prompt += model->session.toolsPrompt;
		templatedMessages.push_back({"system", prompt});
		templatedMessages.insert(templatedMessages.end(), messages.begin(), messages.end());
		for(auto& message : templatedMessages) if(message.content.empty() && message.content_parts.empty()) message.content = " ";
		common_chat_templates_inputs inputs;
		inputs.use_jinja = true;
		inputs.messages = templatedMessages;
		inputs.add_generation_prompt = addGenerationPrompt;
		if(useTools){
			inputs.tools = model->session.tools;
			inputs.tool_choice = COMMON_CHAT_TOOL_CHOICE_AUTO;
			inputs.parallel_tool_calls = model->caps.supports_parallel_tool_calls;
		}
		inputs.reasoning_format = COMMON_REASONING_FORMAT_AUTO;
		try{ chatData = common_chat_templates_apply(model->chatTemplates.get(), inputs); } catch(const std::exception& e){
			LastErrorMessage() = e.what();
			OutputDebugStringA((std::string("EXCEPTION:\r\n") + e.what()).c_str());
			return StudError::CantApplyTemplate;
		}
		return StudError::Success;
	}
	StudError LoadChatSyntax(common_chat_parser_params& syntax, const common_chat_params& chatData, const bool parseToolCalls){
		syntax.format = chatData.format;
		syntax.reasoning_format = COMMON_REASONING_FORMAT_AUTO;
		syntax.reasoning_in_content = false;
		syntax.generation_prompt = chatData.generation_prompt;
		syntax.parse_tool_calls = parseToolCalls;
		try{
			syntax.parser = common_peg_arena();
			if(!chatData.parser.empty()) syntax.parser.load(chatData.parser);
		} catch(const std::exception& e){
			LastErrorMessage() = e.what();
			return StudError::ChatParseError;
		}
		return StudError::Success;
	}
	bool RestoreCachedTokenPrefix(StudModel* model, const size_t prefix){
		auto& session = model->session;
		auto& lane = session.lane;
		if(prefix > lane.cachedTokens.size()) return false;
		if(!RemoveSessionMemoryAfter(session, static_cast<llama_pos>(prefix))){
			ClearSessionMemoryAndSpeculativeState(model);
			lane.cachedTokens.clear();
			if(lane.sampler) llama_sampler_reset(lane.sampler);
			return false;
		}
		if(prefix == 0) RecreateSpeculativeContext(model);
		else FreeSpeculativeContext(session);
		if(lane.sampler){
			llama_sampler_reset(lane.sampler);
			for(size_t i = 0; i < prefix; ++i) if(lane.cachedTokens[i] != LLAMA_TOKEN_NULL) llama_sampler_accept(lane.sampler, lane.cachedTokens[i]);
		}
		lane.cachedTokens.resize(prefix);
		return true;
	}
	StudError DecodeSinglePromptMessage(const char* slotName, const PromptMessage& input, const bool addAssistantPrompt, const bool appendToChat){
		const auto model = GetModel(slotName);
		auto& lane = model->session.lane;
		const auto& message = input.message;
		const bool toolsEnabled = !model->session.tools.empty();
		EnsureMessageMediaAligned(lane);
		if(LaneHasMedia(lane) || !input.media.empty()){
			std::vector<common_chat_msg> chatMessages = lane.messages;
			std::vector<MessageMedia> chatMedia = lane.messageMedia;
			chatMessages.push_back(message);
			chatMedia.push_back(input.media);
			const auto error = EvaluateVisionPrompt(slotName, chatMessages, chatMedia, addAssistantPrompt, toolsEnabled);
			if(error != StudError::Success) return error;
			if(appendToChat){
				lane.messages.push_back(message);
				lane.messageMedia.push_back(input.media);
			}
			lane.endsWithEOG = !addAssistantPrompt;
			return StudError::Success;
		}
		std::vector<common_chat_msg> chatMessages = lane.messages;
		chatMessages.push_back(message);
		common_chat_params chatData;
		auto error = BuildChatTemplateParams(slotName, chatData, chatMessages, addAssistantPrompt, toolsEnabled);
		if(error != StudError::Success) return error;
		error = LoadChatSyntax(model->session.syntax, chatData, toolsEnabled);
		if(error != StudError::Success) return error;
		std::vector<llama_token> promptTokens;
		size_t prefix = 0;
		if(!TryBuildAppendOnlyPrompt(slotName, chatData.prompt, addAssistantPrompt, promptTokens, prefix)){
			if(!TokenizePrompt(slotName, chatData.prompt, promptTokens)) return StudError::CantTokenizePrompt;
			prefix = CommonTokenPrefix(lane.cachedTokens, promptTokens);
			if(prefix < lane.cachedTokens.size()){
				if(model->session.speculative && prefix > 0) --prefix;
				if(!RestorePromptPrefix(model, promptTokens, prefix)) prefix = 0;
			}
		}
		const auto decodeError = DecodePromptTokenSuffix(slotName, promptTokens, prefix, appendToChat ? &message : nullptr);
		if(decodeError == StudError::Success) lane.endsWithEOG = !addAssistantPrompt;
		return decodeError;
	}
}
StudError RetokenizeChat(const char* slotName, const bool rebuildMemory){
	const auto model = GetModel(slotName);
	auto& lane = Stud::Internal::ActiveLane(slotName);
	lane.endsWithEOG = false;
	Stud::Internal::EnsureMessageMediaAligned(lane);
	if(!model->session.ctx || !lane.sampler || !model->session.vocab) return StudError::ModelNotLoaded;
	const bool toolsEnabled = !model->session.tools.empty();
	const auto hasUser = std::any_of(lane.messages.begin(), lane.messages.end(), [](const common_chat_msg& message){ return message.role == "user"; });
	if(!hasUser){
		Stud::Internal::ClearSessionMemoryAndSpeculativeState(model);
		lane.cachedTokens.clear();
		if(lane.sampler) llama_sampler_reset(lane.sampler);
		const auto syntaxError = Stud::Internal::LoadChatSyntax(model->session.syntax, common_chat_params(), false);
		if(syntaxError == StudError::Success) lane.endsWithEOG = lane.messages.empty();
		return syntaxError;
	}
	if(LaneHasMedia(lane)){
		const auto visionError = EvaluateVisionPrompt(slotName, lane.messages, lane.messageMedia, false, toolsEnabled);
		if(visionError == StudError::Success) lane.endsWithEOG = true;
		return visionError;
	}
	common_chat_params chatData;
	const auto applyError = Stud::Internal::BuildChatTemplateParams(slotName, chatData, lane.messages, false, toolsEnabled);
	if(applyError != StudError::Success) return applyError;
	const auto syntaxError = Stud::Internal::LoadChatSyntax(model->session.syntax, chatData, toolsEnabled);
	if(syntaxError != StudError::Success) return syntaxError;
	std::vector<llama_token> promptTokens;
	if(!TokenizePrompt(slotName, chatData.prompt, promptTokens)) return StudError::CantTokenizePrompt;
	size_t prefix = 0;
	if(!rebuildMemory) while(prefix < lane.cachedTokens.size() && prefix < promptTokens.size() && lane.cachedTokens[prefix] == promptTokens[prefix]) ++prefix;
	const size_t matchedPrefix = prefix;
	if(model->session.speculative && !rebuildMemory && prefix > 0 && !(prefix == lane.cachedTokens.size() && prefix == promptTokens.size())) --prefix;
	const bool canShift = !model->session.speculative && llama_memory_can_shift(model->session.memory);
	const size_t oldSize = lane.cachedTokens.size();
	const size_t newSize = promptTokens.size();
	size_t suffix = 0;
	if(!rebuildMemory && canShift && oldSize > 0 && newSize > 0) while(suffix + prefix < oldSize && suffix + prefix < newSize && suffix < oldSize && suffix < newSize && lane.cachedTokens[oldSize - 1 - suffix] == promptTokens[newSize - 1 - suffix]) ++suffix;
	if(!rebuildMemory && prefix == oldSize && oldSize == newSize){
		lane.endsWithEOG = true;
		return StudError::Success;
	}
	if(newSize > static_cast<size_t>(llama_n_ctx(model->session.ctx))){
		if(rebuildMemory){
			Stud::Internal::ClearSessionMemoryAndSpeculativeState(model);
			lane.cachedTokens.clear();
			if(lane.sampler) llama_sampler_reset(lane.sampler);
		}
		return StudError::ConvTooLong;
	}
	if(rebuildMemory){
		prefix = 0;
		suffix = 0;
		Stud::Internal::ClearSessionMemoryAndSpeculativeState(model);
	} else if(!canShift && prefix == 0){ Stud::Internal::ClearSessionMemoryAndSpeculativeState(model); }
	if(!canShift && newSize < oldSize && matchedPrefix < newSize){
		prefix = 0;
		Stud::Internal::ClearSessionMemoryAndSpeculativeState(model);
	}
	if(canShift && suffix > 0){
		const auto suffixStart = static_cast<llama_pos>(oldSize - suffix);
		if(prefix < oldSize - suffix) llama_memory_seq_rm(model->session.memory, 0, static_cast<llama_pos>(prefix), suffixStart);
		if(oldSize != newSize){
			const int delta = static_cast<int>(newSize) - static_cast<int>(oldSize);
			llama_memory_seq_add(model->session.memory, 0, suffixStart, -1, delta);
		}
	} else{
		suffix = 0;
		if(!rebuildMemory && prefix < oldSize && !Stud::Internal::RemoveSessionMemoryAfter(model->session, static_cast<llama_pos>(prefix))){
			Stud::Internal::ClearSessionMemoryAndSpeculativeState(model);
			lane.cachedTokens.clear();
			if(lane.sampler) llama_sampler_reset(lane.sampler);
			return StudError::LlamaDecodeError;
		}
		if(model->session.speculative && prefix > 0 && prefix < oldSize) Stud::Internal::FreeSpeculativeContext(model->session);
	}
	llama_sampler_reset(lane.sampler);
	for(size_t i = 0; i < prefix; ++i) llama_sampler_accept(lane.sampler, promptTokens[i]);
	const size_t decodeEnd = newSize - suffix;
	const int evaluationBatchSize = Stud::Internal::EvalBatchSize(model->session);
	const int batchSize = std::min(evaluationBatchSize, static_cast<int>(decodeEnd - prefix));
	if(batchSize > 0){
		for(size_t i = prefix; i < decodeEnd; i += evaluationBatchSize){
			const int tokenCount = std::min(evaluationBatchSize, static_cast<int>(decodeEnd - i));
			const auto decodeError = Stud::Internal::DecodeTokenBatch(model->session, &promptTokens[i], tokenCount, static_cast<llama_pos>(i), true);
			if(decodeError != StudError::Success){
				Stud::Internal::ClearSessionMemoryAndSpeculativeState(model);
				lane.cachedTokens.clear();
				if(lane.sampler) llama_sampler_reset(lane.sampler);
				return decodeError;
			}
			for(int j = 0; j < tokenCount; ++j) llama_sampler_accept(lane.sampler, promptTokens[i + j]);
		}
	}
	for(size_t i = decodeEnd; i < newSize; ++i) llama_sampler_accept(lane.sampler, promptTokens[i]);
	lane.cachedTokens = std::move(promptTokens);
	lane.endsWithEOG = true;
	return StudError::Success;
}
StudError DialecticRelaySwap(const char* slotName, const char* fromSlotName, const char* toSlotName){
	(void)slotName;
	const auto* fromModel = Stud::Internal::FindModel(Stud::Internal::NormalizeSlotName(fromSlotName));
	auto* toModel = Stud::Internal::FindModel(Stud::Internal::NormalizeSlotName(toSlotName));
	if(!fromModel || !toModel || !fromModel->llModel || !fromModel->session.ctx || !toModel->llModel || !toModel->session.ctx) return StudError::ModelNotLoaded;
	if(AlignDialecticRelayChatStates(*fromModel, *toModel)){
		Stud::Internal::ActiveLane(toSlotName).cachedTokens.clear();
		const auto error = RetokenizeChat(toSlotName, true);
		if(error != StudError::Success) return error;
	}
	return StudError::Success;
}
StudError ResetChat(const char* slotName){
	const auto model = GetModel(slotName);
	auto& session = model->session;
	auto& lane = session.lane;
	lane.messages.clear();
	lane.messageMedia.clear();
	lane.cachedTokens.clear();
	lane.endsWithEOG = true;
	if(!session.ctx || !lane.sampler || !session.vocab) return StudError::Success;
	Stud::Internal::ClearSessionMemoryAndSpeculativeState(model);
	llama_sampler_reset(lane.sampler);
	return Stud::Internal::LoadChatSyntax(session.syntax, common_chat_params(), false);
}
StudError SetSystemPrompt(const char* slotName, const char* prompt, const char* toolsPrompt){
	auto& session = GetModel(slotName)->session;
	auto& lane = session.lane;
	session.systemPrompt = std::string(prompt);
	session.toolsPrompt = std::string(toolsPrompt);
	if(!session.ctx || !lane.sampler || !session.vocab){
		lane.cachedTokens.clear();
		return StudError::Success;
	}
	return RetokenizeChat(slotName, false);
}
StudError SetMessageAt(const char* slotName, const int index, const char* think, const char* message){
	auto& session = GetModel(slotName)->session;
	auto& lane = session.lane;
	if(index < 0 || index >= static_cast<int>(lane.messages.size())) return StudError::IndexOutOfRange;
	auto& messageToUpdate = lane.messages[index];
	messageToUpdate.reasoning_content = messageToUpdate.role._Equal("assistant") ? think : std::string();
	messageToUpdate.content = std::string(message);
	messageToUpdate.content_parts.clear();
	Stud::Internal::EnsureMessageMediaAligned(lane);
	lane.messageMedia[index].clear();
	if(!session.ctx || !lane.sampler || !session.vocab){
		lane.cachedTokens.clear();
		return StudError::Success;
	}
	return RetokenizeChat(slotName, false);
}
StudError SetMessageAtJson(const char* slotName, const int index, const char* think, const char* contentJson){
	auto& lane = Stud::Internal::ActiveLane(slotName);
	if(index < 0 || index >= static_cast<int>(lane.messages.size())) return StudError::IndexOutOfRange;
	PromptMessage replacement;
	auto role = Stud::MessageRole::User;
	if(lane.messages[index].role._Equal("assistant")) role = Stud::MessageRole::Assistant;
	else if(lane.messages[index].role._Equal("tool")) role = Stud::MessageRole::Tool;
	const auto parseError = Stud::Internal::ParseContentJson(role, think, contentJson, replacement);
	if(parseError != StudError::Success) return parseError;
	replacement.message.tool_calls = lane.messages[index].tool_calls;
	replacement.message.tool_name = lane.messages[index].tool_name;
	replacement.message.tool_call_id = lane.messages[index].tool_call_id;
	Stud::Internal::EnsureMessageMediaAligned(lane);
	lane.messages[index] = std::move(replacement.message);
	lane.messageMedia[index] = std::move(replacement.media);
	const auto& session = GetModel(slotName)->session;
	if(!session.ctx || !lane.sampler || !session.vocab){
		lane.cachedTokens.clear();
		return StudError::Success;
	}
	return RetokenizeChat(slotName, false);
}
StudError RemoveMessageAt(const char* slotName, const int index){
	auto& session = GetModel(slotName)->session;
	auto& lane = session.lane;
	if(index < 0 || index >= static_cast<int>(lane.messages.size())) return StudError::IndexOutOfRange;
	Stud::Internal::EnsureMessageMediaAligned(lane);
	lane.messages.erase(lane.messages.begin() + index);
	lane.messageMedia.erase(lane.messageMedia.begin() + index);
	if(!session.ctx || !lane.sampler || !session.vocab){
		lane.cachedTokens.clear();
		return StudError::Success;
	}
	return RetokenizeChat(slotName, false);
}
StudError RemoveMessagesStartingAt(const char* slotName, int index){
	auto& session = GetModel(slotName)->session;
	auto& lane = session.lane;
	if(index < 0) index = 0;
	if(index > static_cast<int>(lane.messages.size())) index = static_cast<int>(lane.messages.size());
	Stud::Internal::EnsureMessageMediaAligned(lane);
	lane.messages.erase(lane.messages.begin() + index, lane.messages.end());
	lane.messageMedia.erase(lane.messageMedia.begin() + index, lane.messageMedia.end());
	if(!session.ctx || !lane.sampler || !session.vocab){
		lane.cachedTokens.clear();
		return StudError::Success;
	}
	return RetokenizeChat(slotName, false);
}
StudError AddMessage(const char* slotName, const Stud::MessageRole role, const char* think, const char* message){
	common_chat_msg chatMessage;
	chatMessage.role = Stud::Internal::RoleString(role);
	chatMessage.reasoning_content = std::string(think);
	chatMessage.content = std::string(message);
	auto& lane = Stud::Internal::ActiveLane(slotName);
	lane.messages.push_back(std::move(chatMessage));
	lane.messageMedia.emplace_back();
	return RetokenizeChat(slotName, false);
}
StudError AddMessageJson(const char* slotName, const Stud::MessageRole role, const char* think, const char* contentJson){
	PromptMessage input;
	const auto parseError = Stud::Internal::ParseContentJson(role, think, contentJson, input);
	if(parseError != StudError::Success) return parseError;
	auto& lane = Stud::Internal::ActiveLane(slotName);
	lane.messages.push_back(std::move(input.message));
	lane.messageMedia.push_back(std::move(input.media));
	return RetokenizeChat(slotName, false);
}
StudError SyncChatMessages(const char* slotName, const int* roles, const char** thinks, const char** messages, const int count){
	std::vector<common_chat_msg> chatMessages;
	if(count > 0){
		chatMessages.reserve(static_cast<size_t>(count));
		for(int i = 0; i < count; ++i){
			const auto role = static_cast<Stud::MessageRole>(roles ? roles[i] : 0);
			common_chat_msg message;
			message.role = Stud::Internal::RoleString(role);
			message.content = messages && messages[i] ? std::string(messages[i]) : std::string();
			if(role == Stud::MessageRole::Assistant) message.reasoning_content = thinks && thinks[i] ? std::string(thinks[i]) : std::string();
			chatMessages.push_back(std::move(message));
		}
	}
	return ReplaceChatMessages(slotName, std::move(chatMessages));
}
StudError SyncChatMessagesJson(const char* slotName, const char* messagesJson){
	std::vector<common_chat_msg> messages;
	std::vector<Stud::MessageMedia> media;
	const char* json = SkipJsonWhitespace(messagesJson);
	const auto parseError = Stud::ParseChatMessagesJson(json, messages, media, Stud::Internal::LastErrorMessage());
	if(parseError != StudError::Success) return parseError;
	return ReplaceChatMessages(slotName, std::move(messages), std::move(media));
}
char* GetContextAsText(const char* slotName){
	const auto& session = GetModel(slotName)->session;
	if(!session.ctx) return nullptr;
	std::string text;
	text.reserve(session.lane.cachedTokens.size() * 4);
	for(const llama_token token : session.lane.cachedTokens) if(token != LLAMA_TOKEN_NULL) text += common_token_to_piece(session.ctx, token, true);
	auto* output = static_cast<char*>(std::malloc(text.size() + 1));
	if(output) std::memcpy(output, text.c_str(), text.size() + 1);
	return output;
}
extern "C" EXPORT void* CaptureChatState(const char* slotName){
	const auto& session = GetModel(slotName)->session;
	auto* snapshot = new(std::nothrow) Stud::StudSession();
	if(!snapshot) return nullptr;
	snapshot->lane.messages = session.lane.messages;
	snapshot->lane.messageMedia = session.lane.messageMedia;
	snapshot->lane.cachedTokens = session.lane.cachedTokens;
	snapshot->lane.endsWithEOG = session.lane.endsWithEOG;
	snapshot->systemPrompt = session.systemPrompt;
	snapshot->toolsPrompt = session.toolsPrompt;
	snapshot->tools = session.tools;
	snapshot->toolHandlers = session.toolHandlers;
	snapshot->syntax = session.syntax;
	snapshot->batchSize = session.batchSize;
	return snapshot;
}
extern "C" EXPORT void RestoreChatState(const char* slotName, void* state){
	if(!state) return;
	auto& session = GetModel(slotName)->session;
	const auto* snapshot = static_cast<Stud::StudSession*>(state);
	session.lane.messages = snapshot->lane.messages;
	session.lane.messageMedia = snapshot->lane.messageMedia;
	session.lane.cachedTokens = snapshot->lane.cachedTokens;
	session.lane.endsWithEOG = snapshot->lane.endsWithEOG;
	RestoreSamplerFromCachedTokens(session);
	Stud::Internal::FreeSpeculativeContext(session);
	session.systemPrompt = snapshot->systemPrompt;
	session.toolsPrompt = snapshot->toolsPrompt;
	session.tools = snapshot->tools;
	session.toolHandlers = snapshot->toolHandlers;
	session.syntax = snapshot->syntax;
	session.batchSize = snapshot->batchSize;
}
extern "C" EXPORT void FreeChatState(void* state){ delete static_cast<Stud::StudSession*>(state); }
