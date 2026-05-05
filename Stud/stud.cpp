#include "stud.h"
#include "hug.h"
#include "JSONCommon.h"
#include <nlohmann\json.hpp>
#include <filesystem>
#include <chrono>
#include <regex>
#include <unordered_map>
#include <algorithm>
#include <cstring>
#include <mutex>
#include <thread>
#include <jinja\parser.h>
using HrClock = std::chrono::high_resolution_clock;
extern "C" void CloseCommandPrompt();
extern "C" void StopCMDOutput();
extern "C" void MarkToolsJsonDirty();
static bool _gpuOomStud = false;
static std::string _lastErrorMessage;
extern "C" EXPORT const char* GetLastErrorMessage(){ return _lastErrorMessage.c_str(); }
extern "C" EXPORT void ClearLastErrorMessage(){ _lastErrorMessage.clear(); }
static void GPUOomLogCallbackStud(const ggml_log_level level, const char* text, void* userData){
	if(level == GGML_LOG_LEVEL_ERROR || level == GGML_LOG_LEVEL_WARN){
		const std::string_view msg(text);
		if(msg.find("out of memory") != std::string_view::npos) _gpuOomStud = true;
	}
}
void SetHWnd(const HWND hWnd){ Stud::inst.hWnd = hWnd; }
void BackendInit(){
	_putenv("OMP_PROC_BIND=close");
	ggml_backend_load("ggml-cpu.dll");
	const HMODULE hModule = LoadLibraryA("nvcuda.dll");
	if(hModule != nullptr) ggml_backend_load("ggml-cuda.dll");
	llama_backend_init();
}
void AddTool(const char* name, const char* description, const char* parameters, const Stud::ToolHandlerFn handler){
	if(!name) return;
	common_chat_tool tool;
	tool.name = name;
	if(description) tool.description = description;
	if(parameters) tool.parameters = parameters;
	Stud::inst.tools.push_back(tool);
	if(handler) Stud::inst.toolHandlers[name] = handler;
	MarkToolsJsonDirty();
}
void ClearTools(){
	CloseCommandPrompt();
	Stud::inst.tools.clear();
	Stud::inst.toolHandlers.clear();
	MarkToolsJsonDirty();
}
static char* CopyCString(const std::string& text){
	const auto size = text.size();
	auto* buffer = static_cast<char*>(std::malloc(size + 1));
	if(!buffer) return nullptr;
	std::memcpy(buffer, text.data(), size);
	buffer[size] = '\0';
	return buffer;
}
static std::string GenerateToolCallId(){
	static std::atomic<unsigned long long> counter{0};
	const auto id = counter.fetch_add(1, std::memory_order_relaxed);
	const auto ticks = std::chrono::duration_cast<std::chrono::microseconds>(HrClock::now().time_since_epoch()).count();
	return "call_" + std::to_string(ticks) + "_" + std::to_string(id);
}
static void EnsureToolCallIds(common_chat_msg& msg){
	for(auto& toolCall : msg.tool_calls)
		if(toolCall.id.empty()) toolCall.id = GenerateToolCallId();
}
extern "C" EXPORT char* ExecuteTool(const char* name, const char* argsJson){
	if(!name || name[0] == '\0') return CopyCString("{\"error\":\"missing tool name\"}");
	const auto it = Stud::inst.toolHandlers.find(name);
	if(it == Stud::inst.toolHandlers.end() || !it->second) return CopyCString("{\"error\":\"unknown tool\"}");
	try{
		const std::string response = it->second(argsJson ? argsJson : "");
		return CopyCString(response);
	} catch(const std::exception& ex){ return CopyCString(std::string("{\"error\":\"") + ex.what() + "\"}"); } catch(...){ return CopyCString("{\"error\":\"tool execution failed\"}"); }
}
static StudError RegisterAPIToolSchemas(const char* toolsJson){
	Stud::inst.tools.clear();
	Stud::inst.toolHandlers.clear();
	MarkToolsJsonDirty();
	const char* p = SkipJsonWhitespace(toolsJson);
	if(!p || !*p) return StudError::Success;
	std::string toolsRoot(p);
	std::string toolsProperty;
	if(*p == '{' && GetJsonPropertyRaw(toolsRoot, "tools", toolsProperty)) toolsRoot = toolsProperty;
	p = SkipJsonWhitespace(toolsRoot.c_str());
	if(!p || !*p || IsJsonNull(toolsRoot)) return StudError::Success;
	const auto toolObjects = ExtractJsonObjects(p);
	if(toolObjects.empty()){
		if(*p == '[') return StudError::Success;
		_lastErrorMessage = "API tools must be a JSON object or array.";
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
		if(!GetJsonPropertyRaw(toolDefinition, "parameters", parameters))
			GetJsonPropertyRaw(toolDefinition, "input_schema", parameters);
		if(parameters.empty() || IsJsonNull(parameters)) parameters = "{}";
		AddTool(name.c_str(), description.c_str(), parameters.c_str(), nullptr);
	}
	return StudError::Success;
}
class ScopedAPITools{
public:
	ScopedAPITools() : _toolsSnapshot(Stud::inst.tools), _handlersSnapshot(Stud::inst.toolHandlers), _syntaxSnapshot(Stud::inst.session.syntax){}
	~ScopedAPITools(){ Restore(); }
	void Restore(){
		if(_restored) return;
		Stud::inst.tools = std::move(_toolsSnapshot);
		Stud::inst.toolHandlers = std::move(_handlersSnapshot);
		Stud::inst.session.syntax = _syntaxSnapshot;
		MarkToolsJsonDirty();
		_restored = true;
	}
private:
	std::vector<common_chat_tool> _toolsSnapshot;
	std::unordered_map<std::string, Stud::ToolHandlerFn> _handlersSnapshot;
	common_chat_parser_params _syntaxSnapshot{};
	bool _restored = false;
};
StudError CreateContext(const int nCtx, const int batchSize, const unsigned int flashAttn, const int nThreads, const int nThreadsBatch){
	if(Stud::inst.session.ctx){
		llama_free(Stud::inst.session.ctx);
		Stud::inst.session.ctx = nullptr;
	}
	auto ctxParams = llama_context_default_params();
	ctxParams.n_ctx = nCtx;
	ctxParams.n_batch = batchSize;
	//ctxParams.flash_attn = flashAttn > 0;
	if(flashAttn == 0) ctxParams.flash_attn_type = LLAMA_FLASH_ATTN_TYPE_DISABLED;
	else if(flashAttn == 1) ctxParams.flash_attn_type = LLAMA_FLASH_ATTN_TYPE_ENABLED;
	else if(flashAttn == 2) ctxParams.flash_attn_type = LLAMA_FLASH_ATTN_TYPE_AUTO;
	ctxParams.n_threads = nThreads;
	ctxParams.n_threads_batch = nThreadsBatch;
	_gpuOomStud = false;
	llama_log_set(GPUOomLogCallbackStud, nullptr);
	Stud::inst.session.ctx = llama_init_from_model(Stud::inst.llModel, ctxParams);
	llama_log_set(nullptr, nullptr);
	if(!Stud::inst.session.ctx){ return _gpuOomStud ? StudError::GpuOutOfMemory : StudError::CantCreateContext; }
	Stud::inst.session.llMem = llama_get_memory(Stud::inst.session.ctx);
	auto result = StudError::Success;
	if(Stud::inst.session.smpl[0]) result = RetokenizeChat(true);
	return result;
}
StudError CreateSamplerInternal(const float minP, const float topP, const int topK, const float temp, const float repeatPenalty, llama_sampler* & smpl){
	if(smpl){
		llama_sampler_free(smpl);
		smpl = nullptr;
	}
	smpl = llama_sampler_chain_init(llama_sampler_chain_default_params());
	if(!smpl){ return StudError::CantCreateSampler; }
	llama_sampler_chain_add(smpl, llama_sampler_init_penalties(128, repeatPenalty, 0.0f, 0.0f));
	llama_sampler_chain_add(smpl, llama_sampler_init_temp(temp));
	if(topK > 0) llama_sampler_chain_add(smpl, llama_sampler_init_top_k(topK));
	if(topP < 1.0f) llama_sampler_chain_add(smpl, llama_sampler_init_top_p(topP, 1));
	if(minP > 0.0f) llama_sampler_chain_add(smpl, llama_sampler_init_min_p(minP, 1));
	llama_sampler_chain_add(smpl, llama_sampler_init_dist(LLAMA_DEFAULT_SEED));
	return StudError::Success;
}
StudError CreateSampler(const float minP, const float topP, const int topK, const float temp, const float repeatPenalty){
	const auto result = CreateSamplerInternal(minP, topP, topK, temp, repeatPenalty, Stud::inst.session.smpl[0]);
	if(result != StudError::Success) return result;
	return CreateSamplerInternal(minP, topP, topK, temp, repeatPenalty, Stud::inst.session.smpl[1]);
}
StudError CreateSession(const int nCtx, const int batchSize, const unsigned int flashAttn, const int nThreads, const int nThreadsBatch, const float minP, const float topP, const int topK, const float temp, const float repeatPenalty){
	if(!Stud::inst.llModel) return StudError::ModelNotLoaded;
	Stud::inst.session.syntax.reasoning_format = COMMON_REASONING_FORMAT_AUTO;
	auto result = CreateContext(nCtx, batchSize, flashAttn, nThreads, nThreadsBatch);
	if(result != StudError::Success) return result;
	Stud::inst.session.batchSize = batchSize;
	result = CreateSampler(minP, topP, topK, temp, repeatPenalty);
	if(result != StudError::Success) return result;
	return StudError::Success;
}
void DestroySession(){
	if(Stud::inst.session.smpl[0]){
		llama_sampler_free(Stud::inst.session.smpl[0]);
		Stud::inst.session.smpl[0] = nullptr;
	}
	if(Stud::inst.session.smpl[1]){
		llama_sampler_free(Stud::inst.session.smpl[1]);
		Stud::inst.session.smpl[1] = nullptr;
	}
	if(Stud::inst.session.ctx){
		llama_free(Stud::inst.session.ctx);
		Stud::inst.session.ctx = nullptr;
	}
	DialecticFree();
}
void FreeModel(){
	DestroySession();
	Stud::inst.session.cachedTokens[0].clear();
	Stud::inst.session.cachedTokens[1].clear();
	if(Stud::inst.llModel){
		llama_model_free(Stud::inst.llModel);
		Stud::inst.llModel = nullptr;
	}
}
StudError LoadModel(const char* filename, const char* jinjaTemplate, const int nGPULayers, const bool mMap, const bool mLock, const ggml_numa_strategy numaStrategy){
	auto params = llama_model_default_params();
	params.n_gpu_layers = nGPULayers;
	params.use_mlock = mLock;
	params.use_mmap = mMap;
	llama_numa_init(numaStrategy);
	_gpuOomStud = false;
	llama_log_set(GPUOomLogCallbackStud, nullptr);
	Stud::inst.llModel = llama_model_load_from_file(filename, params);
	llama_log_set(nullptr, nullptr);
	if(!Stud::inst.llModel){ return _gpuOomStud ? StudError::GpuOutOfMemory : StudError::CantLoadModel; }
	Stud::inst.session._vocab = llama_model_get_vocab(Stud::inst.llModel);
	std::string tmplSrc;
	if(jinjaTemplate && jinjaTemplate[0] != '\0'){
		Stud::inst.chatTemplates = common_chat_templates_init(Stud::inst.llModel, jinjaTemplate);
		tmplSrc = jinjaTemplate;
	} else{
		Stud::inst.chatTemplates = common_chat_templates_init(Stud::inst.llModel, "");
		tmplSrc = llama_model_chat_template(Stud::inst.llModel, nullptr);
	}
	jinja::lexer lex;
	const jinja::lexer_result lexed = lex.tokenize(tmplSrc);
	jinja::program prog = jinja::parse_from_tokens(lexed);
	Stud::inst.caps = jinja::caps_get(prog);
	return StudError::Success;
}
bool HasTool(const char* name){
	for(const auto& tool : Stud::inst.tools){ if(tool.name._Equal(name)) return true; }
	return false;
}
void SetTokenCallback(const Stud::TokenCallbackFn cb){ Stud::inst.tokenCb = cb; }
void SetThreadCount(const int n, const int batchSize){ if(Stud::inst.session.ctx) llama_set_n_threads(Stud::inst.session.ctx, n, batchSize); }
int LlamaMemSize(){ return static_cast<int>(Stud::inst.session.cachedTokens[Stud::inst.session.dId].size()); }
int GetStateSize(){
	if(!Stud::inst.session.ctx) return 0;
	return static_cast<int>(llama_state_get_size(Stud::inst.session.ctx));
}
StudError GetStateData(unsigned char* dst, int size){
	if(!Stud::inst.session.ctx || !dst || size <= 0){
		_lastErrorMessage = "Invalid Parameter";
		return StudError::Generic;
	}
	const auto expected = static_cast<size_t>(size);
	const auto copied = llama_state_get_data(Stud::inst.session.ctx, dst, expected);
	if(copied != expected){
		_lastErrorMessage = "llama_state_get_data copied " + std::to_string(copied) + " bytes, expected " + std::to_string(expected);
		return StudError::Generic;
	}
	return StudError::Success;
}
StudError SetStateData(const unsigned char* src, int size){
	if(!Stud::inst.session.ctx || !src || size <= 0){
		_lastErrorMessage = "Invalid Parameter";
		return StudError::Generic;
	}
	const auto expected = static_cast<size_t>(size);
	const auto read = llama_state_set_data(Stud::inst.session.ctx, src, expected);
	if(read != expected){
		_lastErrorMessage = "llama_state_set_data read " + std::to_string(read) + " bytes, expected " + std::to_string(expected);
		return StudError::Generic;
	}
	return StudError::Success;
}
StudError DialecticInit(){
	const int size = GetStateSize();
	if(size <= 0){
		_lastErrorMessage = "llama_state_get_size returned invalid size:" + std::to_string(size);
		return StudError::Generic;
	}
	Stud::inst.session.dialState[0].assign(size, 0);
	Stud::inst.session.dialState[1].assign(size, 0);
	const auto err = GetStateData(Stud::inst.session.dialState[0].data(), size);
	if(err != StudError::Success){
		return err;
	}
	memcpy(Stud::inst.session.dialState[1].data(), Stud::inst.session.dialState[0].data(), size);
	Stud::inst.session.dId = 0;
	return StudError::Success;
}
StudError DialecticStart(){
	if(Stud::inst.session.dialState[0].empty()){
		_lastErrorMessage = "Dialectic states not initialised";
		return StudError::Generic;
	}
	const int size = static_cast<int>(Stud::inst.session.dialState[0].size());
	SetStateData(Stud::inst.session.dialState[Stud::inst.session.dId].data(), size);
	return StudError::Success;
}
void DialecticFree(){
	Stud::inst.session.dialState[0].clear();
	Stud::inst.session.dialState[1].clear();
}
std::string RoleString(const Stud::MessageRole role){
	switch(role){
		case Stud::MessageRole::User: return std::string("user");
		case Stud::MessageRole::Assistant: return std::string("assistant");
		case Stud::MessageRole::Tool: return std::string("tool");
		default: return std::string();
	}
}
static StudError LoadChatSyntax(common_chat_parser_params& syntax, const common_chat_params& chatData, const bool parseToolCalls){
	syntax.format = chatData.format;
	syntax.reasoning_format = COMMON_REASONING_FORMAT_AUTO;
	syntax.reasoning_in_content = false;
	syntax.generation_prompt = chatData.generation_prompt;
	syntax.parse_tool_calls = parseToolCalls;
	try{
		syntax.parser = common_peg_arena();
		if(!chatData.parser.empty()) syntax.parser.load(chatData.parser);
	} catch(std::exception& e){
		_lastErrorMessage = e.what();
		return StudError::ChatParseError;
	}
	return StudError::Success;
}
static bool TokenizePrompt(const std::string& prompt, std::vector<llama_token>& tokens){
	try{
		tokens = common_tokenize(Stud::inst.session._vocab, prompt, true, true);
		return true;
	} catch(std::exception& e){
		_lastErrorMessage = e.what();
		return false;
	}
}
static StudError BuildChatTemplateParamsForMessages(common_chat_params& chatData, const std::vector<common_chat_msg>& chatMsgs, const bool addGenerationPrompt, const bool includeTools){
	const auto hasUser = std::any_of(chatMsgs.begin(), chatMsgs.end(), [](const common_chat_msg& msg){
		return msg.role == "user";
	});
	if(!hasUser){
		chatData = common_chat_params();
		return StudError::Success;
	}
	const bool useTools = includeTools && !Stud::inst.tools.empty();
	std::vector<common_chat_msg> msgs;
	std::string prompt(Stud::inst.session.prompt);
	if(useTools && !Stud::inst.session.toolsPrompt.empty()) prompt += Stud::inst.session.toolsPrompt;
	msgs.push_back({"system", prompt});
	msgs.insert(msgs.end(), chatMsgs.begin(), chatMsgs.end());
	for(auto& msg : msgs) if(msg.content.empty()) msg.content = " ";
	common_chat_templates_inputs in;
	in.use_jinja = true;
	in.messages = msgs;
	in.add_generation_prompt = addGenerationPrompt;
	if(useTools){
		in.tools = Stud::inst.tools;
		in.tool_choice = COMMON_CHAT_TOOL_CHOICE_AUTO;
		in.parallel_tool_calls = Stud::inst.caps.supports_parallel_tool_calls;
	}
	in.reasoning_format = COMMON_REASONING_FORMAT_AUTO;
	try{ chatData = common_chat_templates_apply(Stud::inst.chatTemplates.get(), in); } catch(std::exception& e){
		_lastErrorMessage = e.what();
		OutputDebugStringA((std::string("EXCEPTION:\r\n") + e.what()).c_str());
		return StudError::CantApplyTemplate;
	}
	return StudError::Success;
}
StudError RetokenizeChat(bool rebuildMemory = false){
	if(!Stud::inst.session.ctx || !Stud::inst.session.smpl[Stud::inst.session.dId] || !Stud::inst.session._vocab) return StudError::ModelNotLoaded;
	const bool toolsEnabled = !Stud::inst.tools.empty();
	const auto hasUser = std::any_of(Stud::inst.session.chatMsgs[Stud::inst.session.dId].begin(), Stud::inst.session.chatMsgs[Stud::inst.session.dId].end(), [](const common_chat_msg& msg){
		return msg.role == "user";
	});
	if(!hasUser){
		if(Stud::inst.session.llMem) llama_memory_clear(Stud::inst.session.llMem, true);
		Stud::inst.session.cachedTokens[Stud::inst.session.dId].clear();
		if(Stud::inst.session.smpl[Stud::inst.session.dId]) llama_sampler_reset(Stud::inst.session.smpl[Stud::inst.session.dId]);
		return LoadChatSyntax(Stud::inst.session.syntax, common_chat_params(), false);
	}
	common_chat_params chatData;
	const auto applyErr = BuildChatTemplateParamsForMessages(chatData, Stud::inst.session.chatMsgs[Stud::inst.session.dId], false, toolsEnabled);
	if(applyErr != StudError::Success) return applyErr;
	const auto syntaxErr = LoadChatSyntax(Stud::inst.session.syntax, chatData, toolsEnabled);
	if(syntaxErr != StudError::Success) return syntaxErr;
	std::vector<llama_token> promptTokens;
	if(!TokenizePrompt(chatData.prompt, promptTokens)) return StudError::CantTokenizePrompt;
	size_t prefix = 0;
	while(prefix < Stud::inst.session.cachedTokens[Stud::inst.session.dId].size() && prefix < promptTokens.size() && Stud::inst.session.cachedTokens[Stud::inst.session.dId][prefix] == promptTokens[prefix]){ ++prefix; }
	const bool canShift = llama_memory_can_shift(Stud::inst.session.llMem);
	const size_t oldSz = Stud::inst.session.cachedTokens[Stud::inst.session.dId].size();
	const size_t newSz = promptTokens.size();
	size_t suffix = 0;
	if(canShift && oldSz > 0 && newSz > 0){ while(suffix + prefix < oldSz && suffix + prefix < newSz && suffix < oldSz && suffix < newSz && Stud::inst.session.cachedTokens[Stud::inst.session.dId][oldSz - 1 - suffix] == promptTokens[newSz - 1 - suffix]){ ++suffix; } }
	const size_t oldSize = Stud::inst.session.cachedTokens[Stud::inst.session.dId].size();
	const size_t newSize = promptTokens.size();
	if(prefix == oldSize && oldSize == newSize) return StudError::Success;
	if(newSize > static_cast<size_t>(llama_n_ctx(Stud::inst.session.ctx))){ return StudError::ConvTooLong; }
	if(rebuildMemory || !canShift){
		if(prefix == 0 || LlamaMemSize() < static_cast<llama_pos>(prefix - 1)){
			prefix = 0;
			llama_memory_clear(Stud::inst.session.llMem, true);
		}
	}
	if(!canShift && newSize < oldSize){
		prefix = 0;
		llama_memory_clear(Stud::inst.session.llMem, true);
	}
	if(canShift && suffix > 0){
		if(prefix < oldSize - suffix){ llama_memory_seq_rm(Stud::inst.session.llMem, 0, prefix, oldSize - suffix); }
		if(oldSize != newSize){
			const int delta = static_cast<int>(newSize) - static_cast<int>(oldSize);
			llama_memory_seq_add(Stud::inst.session.llMem, 0, oldSize - suffix, -1, delta);
		}
	} else{
		suffix = 0;
		if(prefix < oldSize){ llama_memory_seq_rm(Stud::inst.session.llMem, 0, prefix, -1); }
	}
	llama_sampler_reset(Stud::inst.session.smpl[Stud::inst.session.dId]);
	for(size_t i = 0; i < prefix; ++i){ llama_sampler_accept(Stud::inst.session.smpl[Stud::inst.session.dId], promptTokens[i]); }
	const size_t decodeEnd = newSize - suffix;
	const int batchSize = std::min(Stud::inst.session.batchSize, static_cast<int>(decodeEnd - prefix));
	if(batchSize > 0){
		for(size_t i = prefix; i < decodeEnd; i += Stud::inst.session.batchSize){
			const int nTokens = std::min<int>(Stud::inst.session.batchSize, decodeEnd - i);
			const auto batch = llama_batch_get_one(&promptTokens[i], nTokens);
			if(llama_decode(Stud::inst.session.ctx, batch) != 0){
				if(Stud::inst.session.llMem) llama_memory_clear(Stud::inst.session.llMem, true);
				Stud::inst.session.cachedTokens[Stud::inst.session.dId].clear();
				if(Stud::inst.session.smpl[Stud::inst.session.dId]) llama_sampler_reset(Stud::inst.session.smpl[Stud::inst.session.dId]);
				return StudError::LlamaDecodeError;
			}
			for(int j = 0; j < nTokens; ++j){ llama_sampler_accept(Stud::inst.session.smpl[Stud::inst.session.dId], promptTokens[i + j]); }
		}
	}
	for(size_t i = decodeEnd; i < newSize; ++i){ llama_sampler_accept(Stud::inst.session.smpl[Stud::inst.session.dId], promptTokens[i]); }
	Stud::inst.session.cachedTokens[Stud::inst.session.dId] = std::move(promptTokens);
	return StudError::Success;
}
static StudError DecodePromptText(const std::string& prompt, const int dId, const common_chat_msg* appendMsg = nullptr){
	std::vector<llama_token> promptTokens;
	if(!TokenizePrompt(prompt, promptTokens)) return StudError::CantTokenizePrompt;
	if(appendMsg) Stud::inst.session.chatMsgs[dId].push_back(*appendMsg);
	size_t p = 0;
	while(p < promptTokens.size() && !Stud::inst.stop.load()){
		const int nEval = std::min<int>(Stud::inst.session.batchSize, promptTokens.size() - p);
		const llama_batch batch = llama_batch_get_one(&promptTokens[p], nEval);
		const int nCtx = llama_n_ctx(Stud::inst.session.ctx);
		const int nCtxUsed = static_cast<int>(Stud::inst.session.cachedTokens[dId].size());
		if(nCtxUsed + batch.n_tokens > nCtx){
			if(appendMsg) Stud::inst.session.chatMsgs[dId].pop_back();
			return StudError::ConvTooLong;
		}
		const auto result = llama_decode(Stud::inst.session.ctx, batch);
		if(result != 0){
			if(appendMsg) Stud::inst.session.chatMsgs[dId].pop_back();
			return result == 1 ? StudError::ConvTooLong : StudError::LlamaDecodeError;
		}
		for(int j = 0; j < nEval; ++j) llama_sampler_accept(Stud::inst.session.smpl[dId], promptTokens[p + j]);
		Stud::inst.session.cachedTokens[dId].insert(Stud::inst.session.cachedTokens[dId].end(), promptTokens.begin() + p, promptTokens.begin() + p + nEval);
		p += nEval;
	}
	return StudError::Success;
}
static StudError DecodeSinglePromptMessage(const common_chat_msg& message, const int dId, const bool addAss, const bool appendToChat = true){
	if(appendToChat && message.role == "user" && Stud::inst.session.chatMsgs[dId].empty() && Stud::inst.session.cachedTokens[dId].empty()){
		const std::vector<common_chat_msg> chatMsgs{message};
		common_chat_params chatData;
		const bool toolsEnabled = !Stud::inst.tools.empty();
		auto err = BuildChatTemplateParamsForMessages(chatData, chatMsgs, addAss, toolsEnabled);
		if(err != StudError::Success) return err;
		err = LoadChatSyntax(Stud::inst.session.syntax, chatData, toolsEnabled);
		return err == StudError::Success ? DecodePromptText(chatData.prompt, dId, &message) : err;
	}
	std::string formatted;
	try{ formatted = common_chat_format_single(Stud::inst.chatTemplates.get(), Stud::inst.session.chatMsgs[dId], message, addAss, message.role._Equal("assistant")); } catch(std::exception& e){
		_lastErrorMessage = e.what();
		OutputDebugStringA((std::string("EXCEPTION:\r\n") + e.what()).c_str());
		return StudError::CantApplyTemplate;
	}
	return DecodePromptText(formatted, dId, appendToChat ? &message : nullptr);
}
static void AlignChatStates(){
	const auto& a = Stud::inst.session.chatMsgs[0];
	const auto& b = Stud::inst.session.chatMsgs[1];
	if(a.size() == b.size()) return;
	const int longerId = a.size() >= b.size() ? 0 : 1;
	const int shorterId = 1 - longerId;
	const auto& longer = Stud::inst.session.chatMsgs[longerId];
	auto& shorter = Stud::inst.session.chatMsgs[shorterId];
	for(size_t i = shorter.size(); i < longer.size(); ++i){
		auto msg = longer[i];
		if(msg.role._Equal("assistant")){
			msg.role = "user";
			msg.reasoning_content.clear();
		} else if(msg.role._Equal("user")){ msg.role = "assistant"; }
		const bool mirroredRoleIsAssistant = msg.role._Equal("assistant");
		shorter.push_back(msg);
		if(Stud::inst.session.ctx && Stud::inst.session.smpl[shorterId] && Stud::inst.session._vocab && !Stud::inst.session.dialState[shorterId].empty() && Stud::inst.session.dId == shorterId){
			const auto err = DecodeSinglePromptMessage(shorter.back(), shorterId, mirroredRoleIsAssistant, false);
			if(err == StudError::Success) Stud::inst.session.assNextGen = mirroredRoleIsAssistant;
		}
	}
}
StudError DialecticSwap(){
	if(Stud::inst.session.dialState[0].empty()) return StudError::Success;
	AlignChatStates();
	const int size = static_cast<int>(Stud::inst.session.dialState[Stud::inst.session.dId].size());
	GetStateData(Stud::inst.session.dialState[Stud::inst.session.dId].data(), size);
	Stud::inst.session.dId = 1 - Stud::inst.session.dId;
	SetStateData(Stud::inst.session.dialState[Stud::inst.session.dId].data(), size);
	return StudError::Success;
}
StudError ResetChat(){
	Stud::inst.session.chatMsgs[0].clear();
	Stud::inst.session.chatMsgs[1].clear();
	Stud::inst.session.cachedTokens[0].clear();
	Stud::inst.session.cachedTokens[1].clear();
	Stud::inst.session.assNextGen = false;
	if(!Stud::inst.session.ctx || !Stud::inst.session.smpl[Stud::inst.session.dId] || !Stud::inst.session._vocab){
		DialecticFree();
		return StudError::Success;
	}
	auto err = RetokenizeChat(true);
	if(err != StudError::Success || Stud::inst.session.dialState[Stud::inst.session.dId == 0 ? 1 : 0].empty()) return err;
	err = DialecticSwap();
	if(err != StudError::Success) return err;
	const auto err2 = RetokenizeChat(true);
	const auto err3 = DialecticSwap();
	return err2 != StudError::Success ? err2 : err3;
}
StudError SetSystemPrompt(const char* prompt, const char* toolsPrompt){
	Stud::inst.session.prompt = std::string(prompt);
	Stud::inst.session.toolsPrompt = std::string(toolsPrompt);
	if(!Stud::inst.session.ctx || !Stud::inst.session.smpl[Stud::inst.session.dId] || !Stud::inst.session._vocab){
		Stud::inst.session.cachedTokens[0].clear();
		Stud::inst.session.cachedTokens[1].clear();
		return StudError::Success;
	}
	auto err = RetokenizeChat();
	if(err != StudError::Success || Stud::inst.session.dialState[Stud::inst.session.dId == 0 ? 1 : 0].empty()) return err;
	err = DialecticSwap();
	if(err != StudError::Success) return err;
	const auto err2 = RetokenizeChat();
	const auto err3 = DialecticSwap();
	return err2 != StudError::Success ? err2 : err3;
}
StudError SetMessageAt(const int index, const char* think, const char* message){
	AlignChatStates();
	if(index < 0 || index >= static_cast<int>(Stud::inst.session.chatMsgs[Stud::inst.session.dId].size())) return StudError::IndexOutOfRange;
	const auto applyMessageUpdate = [&](std::vector<common_chat_msg>& msgs){
		msgs[index].reasoning_content = msgs[index].role._Equal("assistant") ? think : std::string();
		msgs[index].content = std::string(message);
	};
	if(!Stud::inst.session.ctx || !Stud::inst.session.smpl[Stud::inst.session.dId] || !Stud::inst.session._vocab){
		applyMessageUpdate(Stud::inst.session.chatMsgs[0]);
		applyMessageUpdate(Stud::inst.session.chatMsgs[1]);
		Stud::inst.session.cachedTokens[0].clear();
		Stud::inst.session.cachedTokens[1].clear();
		return StudError::Success;
	}
	applyMessageUpdate(Stud::inst.session.chatMsgs[Stud::inst.session.dId]);
	auto err = RetokenizeChat();
	const auto dId = Stud::inst.session.dId == 0 ? 1 : 0;
	if(err != StudError::Success || Stud::inst.session.dialState[dId].empty() || Stud::inst.session.chatMsgs[dId].size() <= index) return err;
	err = DialecticSwap();
	if(err != StudError::Success) return err;
	applyMessageUpdate(Stud::inst.session.chatMsgs[Stud::inst.session.dId]);
	const auto err2 = RetokenizeChat();
	const auto err3 = DialecticSwap();
	return err2 != StudError::Success ? err2 : err3;
}
StudError RemoveMessageAt(const int index){
	AlignChatStates();
	if(index < 0 || index >= static_cast<int>(Stud::inst.session.chatMsgs[Stud::inst.session.dId].size())) return StudError::IndexOutOfRange;
	if(!Stud::inst.session.ctx || !Stud::inst.session.smpl[Stud::inst.session.dId] || !Stud::inst.session._vocab){
		Stud::inst.session.chatMsgs[0].erase(Stud::inst.session.chatMsgs[0].begin() + index);
		Stud::inst.session.chatMsgs[1].erase(Stud::inst.session.chatMsgs[1].begin() + index);
		Stud::inst.session.cachedTokens[0].clear();
		Stud::inst.session.cachedTokens[1].clear();
		return StudError::Success;
	}
	Stud::inst.session.chatMsgs[Stud::inst.session.dId].erase(Stud::inst.session.chatMsgs[Stud::inst.session.dId].begin() + index);
	auto err = RetokenizeChat();
	const auto dId = Stud::inst.session.dId == 0 ? 1 : 0;
	if(err != StudError::Success || Stud::inst.session.dialState[dId].empty() || Stud::inst.session.chatMsgs[dId].size() <= index) return err;
	err = DialecticSwap();
	if(err != StudError::Success) return err;
	Stud::inst.session.chatMsgs[Stud::inst.session.dId].erase(Stud::inst.session.chatMsgs[Stud::inst.session.dId].begin() + index);
	const auto err2 = RetokenizeChat();
	const auto err3 = DialecticSwap();
	return err2 != StudError::Success ? err2 : err3;
}
StudError RemoveMessagesStartingAt(int index){
	AlignChatStates();
	if(index < 0) index = 0;
	if(index > static_cast<int>(Stud::inst.session.chatMsgs[Stud::inst.session.dId].size())) index = static_cast<int>(Stud::inst.session.chatMsgs[Stud::inst.session.dId].size());
	if(!Stud::inst.session.ctx || !Stud::inst.session.smpl[Stud::inst.session.dId] || !Stud::inst.session._vocab){
		Stud::inst.session.chatMsgs[0].erase(Stud::inst.session.chatMsgs[0].begin() + index, Stud::inst.session.chatMsgs[0].end());
		Stud::inst.session.chatMsgs[1].erase(Stud::inst.session.chatMsgs[1].begin() + index, Stud::inst.session.chatMsgs[1].end());
		Stud::inst.session.cachedTokens[0].clear();
		Stud::inst.session.cachedTokens[1].clear();
		return StudError::Success;
	}
	Stud::inst.session.chatMsgs[Stud::inst.session.dId].erase(Stud::inst.session.chatMsgs[Stud::inst.session.dId].begin() + index, Stud::inst.session.chatMsgs[Stud::inst.session.dId].end());
	auto err = RetokenizeChat();
	const auto dId = Stud::inst.session.dId == 0 ? 1 : 0;
	if(err != StudError::Success || Stud::inst.session.dialState[dId].empty() || Stud::inst.session.chatMsgs[dId].size() <= index) return err;
	err = DialecticSwap();
	if(err != StudError::Success) return err;
	Stud::inst.session.chatMsgs[Stud::inst.session.dId].erase(Stud::inst.session.chatMsgs[Stud::inst.session.dId].begin() + index, Stud::inst.session.chatMsgs[Stud::inst.session.dId].end());
	const auto err2 = RetokenizeChat();
	const auto err3 = DialecticSwap();
	return err2 != StudError::Success ? err2 : err3;
}
StudError AddMessage(const Stud::MessageRole role, const char* message){
	common_chat_msg msg;
	msg.role = RoleString(role);
	msg.content = std::string(message);
	Stud::inst.session.chatMsgs[Stud::inst.session.dId].push_back(msg);
	return RetokenizeChat();
}
static StudError ReplaceChatMessages(std::vector<common_chat_msg>&& msgs){
	for(auto& msg : msgs) EnsureToolCallIds(msg);
	Stud::inst.session.chatMsgs[Stud::inst.session.dId] = std::move(msgs);
	Stud::inst.session.chatMsgs[Stud::inst.session.dId == 0 ? 1 : 0].clear();
	Stud::inst.session.assNextGen = false;
	AlignChatStates();
	Stud::inst.session.cachedTokens[0].clear();
	Stud::inst.session.cachedTokens[1].clear();
	if(!Stud::inst.session.ctx || !Stud::inst.session.smpl[Stud::inst.session.dId] || !Stud::inst.session._vocab) return StudError::Success;
	auto err = RetokenizeChat(true);
	const auto dId = Stud::inst.session.dId == 0 ? 1 : 0;
	if(err != StudError::Success || Stud::inst.session.dialState[dId].empty()) return err;
	err = DialecticSwap();
	if(err != StudError::Success) return err;
	const auto err2 = RetokenizeChat(true);
	const auto err3 = DialecticSwap();
	return err2 != StudError::Success ? err2 : err3;
}
StudError SyncChatMessages(const int* roles, const char** thinks, const char** messages, int count){
	std::vector<common_chat_msg> msgs;
	if(count > 0){
		msgs.reserve(static_cast<size_t>(count));
		for(int i = 0; i < count; ++i){
			const auto role = static_cast<Stud::MessageRole>(roles ? roles[i] : 0);
			common_chat_msg msg;
			msg.role = RoleString(role);
			msg.content = messages && messages[i] ? std::string(messages[i]) : std::string();
			if(role == Stud::MessageRole::Assistant) msg.reasoning_content = thinks && thinks[i] ? std::string(thinks[i]) : std::string();
			msgs.push_back(std::move(msg));
		}
	}
	return ReplaceChatMessages(std::move(msgs));
}
StudError SyncChatMessagesJson(const char* messagesJson){
	std::vector<common_chat_msg> msgs;
	const char* p = SkipJsonWhitespace(messagesJson);
	if(p && *p){
		try{ msgs = common_chat_msgs_parse_oaicompat(nlohmann::ordered_json::parse(p)); } catch(const std::exception& e){
			_lastErrorMessage = e.what();
			return StudError::ChatParseError;
		}
	}
	return ReplaceChatMessages(std::move(msgs));
}
static common_chat_msg ParseGeneratedMessage(const std::string& response, const common_chat_parser_params& syntax, const bool isPartial){
	try{ return common_chat_parse(response, isPartial, syntax); } catch(std::exception& e){
		if(!isPartial && syntax.parse_tool_calls) throw;
		OutputDebugStringA((std::string("CHAT PARSE FALLBACK:\r\n") + e.what()).c_str());
		common_chat_parser_params fallback;
		fallback.reasoning_format = syntax.reasoning_format;
		fallback.reasoning_in_content = syntax.reasoning_in_content;
		return common_chat_parse(response, isPartial, fallback);
	}
}
struct PendingToken{
	llama_token token;
	int memSize;
};
class AsyncTokenPostProcessor{
public:
	AsyncTokenPostProcessor(const Stud::TokenCallbackFn callbackFn, const bool streamCallback, const common_chat_parser_params& chatSyntax, const HrClock::time_point& prepStart, std::string& responseText, common_chat_msg& parsedMsg, double& firstTokenTime) : _callbackFn(callbackFn), _streamCallback(streamCallback), _chatSyntax(chatSyntax), _prepStart(prepStart), _responseText(responseText), _parsedMsg(parsedMsg), _firstTokenTime(firstTokenTime), _queue(kQueueCapacity){ 
		_chatSyntax.parse_tool_calls = false;
		_worker = std::thread([this]{ WorkerLoop(); });
	}
	~AsyncTokenPostProcessor(){ Close(); }
	StudError Error() const{ return _asyncError.load(std::memory_order_acquire); }
	void Close(){
		const bool wasClosed = _queueClosed.exchange(true, std::memory_order_acq_rel);
		if(!wasClosed) _queueCv.notify_all();
		if(_worker.joinable()) _worker.join();
	}
	StudError Enqueue(const llama_token token, const int memSize){
		for(;;){
			const StudError error = Error();
			if(error != StudError::Success) return error;
			const size_t head = _queueHead.load(std::memory_order_acquire);
			const size_t tail = _queueTail.load(std::memory_order_relaxed);
			if((tail - head) < kQueueCapacity){
				_queue[tail % kQueueCapacity] = PendingToken{token, memSize};
				_queueTail.store(tail + 1, std::memory_order_release);
				if(tail == head) _queueCv.notify_one();
				return StudError::Success;
			}
			std::this_thread::yield();
		}
	}
private:
	void EmitStreamingCallback(const int memSize){
		if(!_callbackFn || !_streamCallback || _pendingCallbackTokens <= 0) return;
		_parsedMsg = ParseGeneratedMessage(_responseText, _chatSyntax, true);
		_callbackFn(_parsedMsg.reasoning_content.c_str(), static_cast<int>(_parsedMsg.reasoning_content.length()), _parsedMsg.content.c_str(), static_cast<int>(_parsedMsg.content.length()), _pendingCallbackTokens, memSize, _firstTokenTime, 0);
		_pendingCallbackTokens = 0;
		_lastCallbackTime = std::chrono::steady_clock::now();
	}
	void WorkerLoop(){
		constexpr auto kMinCallbackInterval = std::chrono::milliseconds(16);
		constexpr int kMaxTokensPerCallback = 8;
		int lastMemSize = 0;
		for(;;){
			const size_t head = _queueHead.load(std::memory_order_relaxed);
			const size_t tail = _queueTail.load(std::memory_order_acquire);
			if(head == tail){
				if(_queueClosed.load(std::memory_order_acquire)){
					EmitStreamingCallback(lastMemSize);
					return;
				}
				if(_pendingCallbackTokens > 0 && std::chrono::steady_clock::now() - _lastCallbackTime >= kMinCallbackInterval){ EmitStreamingCallback(lastMemSize); }
				std::unique_lock<std::mutex> lock(_queueWaitMutex);
				_queueCv.wait_for(lock, std::chrono::milliseconds(1), [&](){ return _queueClosed.load(std::memory_order_acquire) || _queueHead.load(std::memory_order_relaxed) != _queueTail.load(std::memory_order_acquire); });
				continue;
			}
			const PendingToken pending = _queue[head % kQueueCapacity];
			_queueHead.store(head + 1, std::memory_order_release);
			char buf[256];
			const int n = llama_token_to_piece(Stud::inst.session._vocab, pending.token, buf, sizeof buf, 0, false);
			if(n < 0){
				_asyncError.store(StudError::CantConvertToken, std::memory_order_release);
				_queueClosed.store(true, std::memory_order_release);
				_queueCv.notify_all();
				return;
			}
			if(_firstTokenTime == 0.0) _firstTokenTime = std::chrono::duration<double>(HrClock::now() - _prepStart).count();
			_responseText.append(buf, static_cast<size_t>(n));
			if(n > 0){
				lastMemSize = pending.memSize;
				++_pendingCallbackTokens;
				const bool reachedBatchSize = _pendingCallbackTokens >= kMaxTokensPerCallback;
				const bool reachedRateLimit = std::chrono::steady_clock::now() - _lastCallbackTime >= kMinCallbackInterval;
				if(reachedBatchSize || reachedRateLimit) EmitStreamingCallback(lastMemSize);
			}
		}
	}
	static constexpr size_t kQueueCapacity = 128;
	Stud::TokenCallbackFn _callbackFn;
	bool _streamCallback;
	common_chat_parser_params _chatSyntax;
	HrClock::time_point _prepStart;
	std::string& _responseText;
	common_chat_msg& _parsedMsg;
	double& _firstTokenTime;
	std::vector<PendingToken> _queue;
	std::atomic<size_t> _queueHead{0};
	std::atomic<size_t> _queueTail{0};
	std::atomic<bool> _queueClosed{false};
	std::atomic<StudError> _asyncError{StudError::Success};
	std::mutex _queueWaitMutex;
	std::condition_variable _queueCv;
	std::thread _worker;
	std::chrono::steady_clock::time_point _lastCallbackTime = std::chrono::steady_clock::now();
	int _pendingCallbackTokens = 0;
};
static StudError RollbackGenerate(const size_t chatStart, const size_t newMessageCount, common_chat_msg& outMsg, const StudError error){
	Stud::inst.session.chatMsgs[Stud::inst.session.dId].resize(chatStart + newMessageCount);
	const auto rtErr = RetokenizeChat(true);
	outMsg = common_chat_msg();
	return rtErr != StudError::Success ? rtErr : error;
}
static StudError DecodePromptMessages(const std::vector<common_chat_msg>& messages, common_chat_msg& outMsg){
	for(const auto& message : messages){
		const bool addAss = Stud::inst.session.assNextGen || !message.role._Equal("assistant");
		Stud::inst.session.assNextGen = false;
		const auto err = DecodeSinglePromptMessage(message, Stud::inst.session.dId, addAss);
		if(err != StudError::Success){
			const auto rtErr = RetokenizeChat(true);
			outMsg = common_chat_msg();
			return rtErr != StudError::Success ? rtErr : err;
		}
	}
	return StudError::Success;
}
StudError Generate(const std::vector<common_chat_msg>& messages, const int nPredict, const bool callback, common_chat_msg& outMsg, const bool emitFinalCallback = true){
	const auto prepStart = HrClock::now();
	Stud::inst.stop.store(false);
	const Stud::TokenCallbackFn cb = Stud::inst.tokenCb;
	const size_t chatStart = Stud::inst.session.chatMsgs[Stud::inst.session.dId].size();
	const auto promptErr = DecodePromptMessages(messages, outMsg);
	if(promptErr != StudError::Success) return promptErr;
	const bool toolsEnabled = !Stud::inst.tools.empty();
	common_chat_parser_params streamSyntax = Stud::inst.session.syntax;
	if(callback){
		common_chat_params streamChatData;
		auto streamSyntaxErr = BuildChatTemplateParamsForMessages(streamChatData, Stud::inst.session.chatMsgs[Stud::inst.session.dId], true, false);
		if(streamSyntaxErr == StudError::Success) streamSyntaxErr = LoadChatSyntax(streamSyntax, streamChatData, false);
		if(streamSyntaxErr != StudError::Success) return RollbackGenerate(chatStart, messages.size(), outMsg, streamSyntaxErr);
	} else streamSyntax.parse_tool_calls = false;
	std::string response;
	common_chat_msg msg;
	double ftTime = 0.0;
	AsyncTokenPostProcessor postProcessor(cb, callback, streamSyntax, prepStart, response, msg, ftTime);
	auto failWith = [&](StudError error){
		postProcessor.Close();
		return RollbackGenerate(chatStart, messages.size(), outMsg, error);
	};
	int i = 0;
	while((nPredict < 0 || i < nPredict) && !Stud::inst.stop.load()){
		const StudError pendingError = postProcessor.Error();
		if(pendingError != StudError::Success) return failWith(pendingError);
		if(LlamaMemSize() + 1 > llama_n_ctx(Stud::inst.session.ctx)) return failWith(StudError::ConvTooLong);
		auto newTokenId = llama_sampler_sample(Stud::inst.session.smpl[Stud::inst.session.dId], Stud::inst.session.ctx, -1);
		const auto isEog = llama_vocab_is_eog(Stud::inst.session._vocab, newTokenId);
		const auto decodeErr = llama_decode(Stud::inst.session.ctx, llama_batch_get_one(&newTokenId, 1));
		if(decodeErr != 0) return failWith(decodeErr == 1 ? StudError::ConvTooLong : StudError::LlamaDecodeError);
		llama_sampler_accept(Stud::inst.session.smpl[Stud::inst.session.dId], newTokenId);
		Stud::inst.session.cachedTokens[Stud::inst.session.dId].push_back(newTokenId);
		if(isEog) break;
		const auto enqueueErr = postProcessor.Enqueue(newTokenId, LlamaMemSize());
		if(enqueueErr != StudError::Success) return failWith(enqueueErr);
		++i;
	}
	postProcessor.Close();
	if(postProcessor.Error() != StudError::Success) return RollbackGenerate(chatStart, messages.size(), outMsg, postProcessor.Error());
	common_chat_params finalChatData;
	auto finalSyntaxErr = BuildChatTemplateParamsForMessages(finalChatData, Stud::inst.session.chatMsgs[Stud::inst.session.dId], true, toolsEnabled);
	if(finalSyntaxErr == StudError::Success) finalSyntaxErr = LoadChatSyntax(Stud::inst.session.syntax, finalChatData, toolsEnabled);
	if(finalSyntaxErr != StudError::Success) return RollbackGenerate(chatStart, messages.size(), outMsg, finalSyntaxErr);
	try{
		msg = ParseGeneratedMessage(response, Stud::inst.session.syntax, false);
		EnsureToolCallIds(msg);
	} catch(std::exception& e){
		_lastErrorMessage = e.what();
		return RollbackGenerate(chatStart, messages.size(), outMsg, StudError::ChatParseError);
	}
	Stud::inst.session.chatMsgs[Stud::inst.session.dId].push_back(msg);
	if(emitFinalCallback && cb && !callback) cb(msg.reasoning_content.c_str(), static_cast<int>(msg.reasoning_content.length()), msg.content.c_str(), static_cast<int>(msg.content.length()), i, LlamaMemSize(), ftTime, 0);
	outMsg = std::move(msg);
	//OutputDebugStringA(("\n!!! CONTEXT START !!!\n" + std::string(GetContextAsText()) + "\n!!! CONTEXT END !!!\n").c_str());
	return StudError::Success;
}
StudError GenerateWithTools(const Stud::MessageRole role, const char* prompt, const int nPredict, const bool callback){
	common_chat_msg msg;
	msg.role = RoleString(role);
	msg.content = std::string(prompt);
	std::vector<common_chat_msg> msgs{msg};
	if(Stud::inst.tools.empty()){ return Generate(msgs, nPredict, callback, msg); }
	const Stud::TokenCallbackFn cb = Stud::inst.tokenCb;
	bool toolCalled;
	do{
		const auto err = Generate(msgs, nPredict, callback, msg);
		if(err != StudError::Success) return err;
		if(!Stud::inst.session.chatMsgs[Stud::inst.session.dId].size()) return StudError::Success;
		msgs.clear();
		try{
			toolCalled = false;
			for(common_chat_tool_call& toolCall : msg.tool_calls){
				if(Stud::inst.stop.load()) return StudError::Success;
				auto toolCallMsg = "Tool name: " + toolCall.name + "\r\nTool ID: " + toolCall.id + "\r\nTool arguments: " + toolCall.arguments;
				if(cb) cb(nullptr, 0, toolCallMsg.c_str(), static_cast<int>(toolCallMsg.length()), 0, LlamaMemSize(), 0, 3);
				auto it = Stud::inst.toolHandlers.find(toolCall.name);
				if(it != Stud::inst.toolHandlers.end()){
					auto toolMsg = it->second(toolCall.arguments.c_str());
					if(cb) cb(nullptr, 0, toolMsg.c_str(), static_cast<int>(toolMsg.length()), 0, LlamaMemSize(), 0, 1);
					msgs.push_back(common_chat_msg());
					msgs.back().role = "tool";
					msgs.back().content = toolMsg;
					toolCalled = true;
				}
			}
		} catch(std::exception& e){
			_lastErrorMessage = e.what();
			return StudError::ChatParseError;
		}
	} while(toolCalled);
	return StudError::Success;
}
static std::string BuildAPIResponseJson(const common_chat_msg& msg){
	const bool hasContent = !msg.content.empty();
	const bool hasToolCalls = !msg.tool_calls.empty();
	std::string json = "{";
	json += "\"role\":\"assistant\",";
	json += "\"content\":" + JsonString(msg.content) + ",";
	json += "\"reasoning\":" + JsonString(msg.reasoning_content) + ",";
	json += "\"finish_reason\":\"";
	json += hasToolCalls ? "tool_calls" : "stop";
	json += "\",";
	json += "\"tool_calls\":[";
	for(size_t i = 0; i < msg.tool_calls.size(); ++i){
		const auto& toolCall = msg.tool_calls[i];
		if(i > 0) json += ",";
		json += "{\"id\":" + JsonString(toolCall.id) + ",\"type\":\"function\",\"function\":{";
		json += "\"name\":" + JsonString(toolCall.name) + ",";
		json += "\"arguments\":" + JsonString(toolCall.arguments);
		json += "}}";
	}
	json += "],\"output\":[";
	bool hasOutputItem = false;
	if(hasContent){
		json += "{\"type\":\"message\",\"status\":\"completed\",\"role\":\"assistant\",\"content\":[{\"type\":\"output_text\",\"text\":" + JsonString(msg.content) + "}]}";
		hasOutputItem = true;
	}
	for(const auto& toolCall : msg.tool_calls){
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
StudError GenerateForAPI(const Stud::MessageRole role, const char* prompt, const char* toolsJson, const int nPredict, char** responseJson){
	if(responseJson) *responseJson = nullptr;
	if(!responseJson){
		_lastErrorMessage = "responseJson is null.";
		return StudError::Generic;
	}
	ScopedAPITools apiTools;
	const auto toolsErr = RegisterAPIToolSchemas(toolsJson);
	if(toolsErr != StudError::Success) return toolsErr;
	const auto retokenizeErr = RetokenizeChat();
	if(retokenizeErr != StudError::Success) return retokenizeErr;
	common_chat_msg inputMsg;
	inputMsg.role = RoleString(role);
	inputMsg.content = prompt ? std::string(prompt) : std::string();
	std::vector<common_chat_msg> messages{inputMsg};
	common_chat_msg outputMsg;
	const auto generateErr = Generate(messages, nPredict, false, outputMsg, false);
	if(generateErr != StudError::Success) return generateErr;
	*responseJson = CopyCString(BuildAPIResponseJson(outputMsg));
	if(!*responseJson){
		_lastErrorMessage = "Unable to allocate API generation response.";
		return StudError::Generic;
	}
	return StudError::Success;
}
void StopGeneration(){
	Stud::inst.stop.store(true);
	StopCMDOutput();
}
char* GetContextAsText(){
	if(!Stud::inst.session.ctx) return nullptr;
	std::string outStr;
	outStr.reserve(Stud::inst.session.cachedTokens[Stud::inst.session.dId].size() * 4);
	for(const llama_token tok : Stud::inst.session.cachedTokens[Stud::inst.session.dId]){ outStr += common_token_to_piece(Stud::inst.session.ctx, tok, true); }
	auto* out = static_cast<char*>(std::malloc(outStr.size() + 1));
	if(out) std::memcpy(out, outStr.c_str(), outStr.size() + 1);
	return out;
}
extern "C" EXPORT void* CaptureChatState(){
	auto* snapshot = new(std::nothrow) Stud::ChatStateSnapshot();
	if(!snapshot) return nullptr;
	snapshot->chatMsgs[0] = Stud::inst.session.chatMsgs[0];
	snapshot->chatMsgs[1] = Stud::inst.session.chatMsgs[1];
	snapshot->cachedTokens[0] = Stud::inst.session.cachedTokens[0];
	snapshot->cachedTokens[1] = Stud::inst.session.cachedTokens[1];
	snapshot->dialState[0] = Stud::inst.session.dialState[0];
	snapshot->dialState[1] = Stud::inst.session.dialState[1];
	snapshot->dId = Stud::inst.session.dId;
	snapshot->prompt = Stud::inst.session.prompt;
	snapshot->toolsPrompt = Stud::inst.session.toolsPrompt;
	snapshot->syntax = Stud::inst.session.syntax;
	snapshot->addNextGen = Stud::inst.session.assNextGen;
	snapshot->batchSize = Stud::inst.session.batchSize;
	return snapshot;
}
extern "C" EXPORT void RestoreChatState(void* state){
	if(!state) return;
	const auto* snapshot = static_cast<Stud::ChatStateSnapshot*>(state);
	Stud::inst.session.chatMsgs[0] = snapshot->chatMsgs[0];
	Stud::inst.session.chatMsgs[1] = snapshot->chatMsgs[1];
	Stud::inst.session.cachedTokens[0] = snapshot->cachedTokens[0];
	Stud::inst.session.cachedTokens[1] = snapshot->cachedTokens[1];
	Stud::inst.session.dialState[0] = snapshot->dialState[0];
	Stud::inst.session.dialState[1] = snapshot->dialState[1];
	Stud::inst.session.dId = snapshot->dId;
	Stud::inst.session.prompt = snapshot->prompt;
	Stud::inst.session.toolsPrompt = snapshot->toolsPrompt;
	Stud::inst.session.syntax = snapshot->syntax;
	Stud::inst.session.assNextGen = snapshot->addNextGen;
	Stud::inst.session.batchSize = snapshot->batchSize;
}
extern "C" EXPORT void FreeChatState(void* state){ delete static_cast<Stud::ChatStateSnapshot*>(state); }
