#include "stud.h"
#include "StudState.h"
#include "hug.h"
#include "JSONCommon.h"
#include <nlohmann\json.hpp>
#include <filesystem>
#include <chrono>
#include <regex>
#include <unordered_map>
#include <algorithm>
#include <cctype>
#include <cstring>
#include <mutex>
#include <thread>
#include <jinja\parser.h>

#pragma comment(lib, "llama.lib")
#pragma comment(lib, "llama-common.lib")
#pragma comment(lib, "ggml.lib")
#pragma comment(lib, "ggml-base.lib")

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
void SetHWnd(const HWND hWnd){ Stud::hWnd = hWnd; }
void BackendInit(){
	_putenv("OMP_PROC_BIND=close");
	ggml_backend_load("ggml-cpu.dll");
	const HMODULE hModule = LoadLibraryA("nvcuda.dll");
	if(hModule != nullptr) ggml_backend_load("ggml-cuda.dll");
	llama_backend_init();
}
static std::string NormName(const char* slotName){
	std::string name = slotName ? slotName : "";
	name.erase(name.begin(), std::find_if(name.begin(), name.end(), [](unsigned char ch){ return !std::isspace(ch); }));
	name.erase(std::find_if(name.rbegin(), name.rend(), [](unsigned char ch){ return !std::isspace(ch); }).base(), name.end());
	if(name.empty()) name = "main";
	std::transform(name.begin(), name.end(), name.begin(), [](unsigned char ch){ return static_cast<char>(std::tolower(ch)); });
	return name;
}
static Stud::StudModel* FindModel(const std::string& slotName){
	const auto it = Stud::models.find(slotName);
	return it == Stud::models.end() ? nullptr : it->second.get();
}
static Stud::StudModel& GetOrCreateModel(const std::string& slotName){
	auto& model = Stud::models[slotName];
	if(!model){
		model = std::make_unique<Stud::StudModel>();
		model->slotName = slotName;
	}
	return *model;
}
Stud::StudModel* GetModel(const char* slotName){ return &GetOrCreateModel(NormName(slotName)); }
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
extern "C" EXPORT char* ExecuteTool(const char* slotName, const char* name, const char* argsJson){
	if(!name || name[0] == '\0') return CopyCString("{\"error\":\"missing tool name\"}");
	const auto model = GetModel(slotName);
	const auto it = model->session.toolHandlers.find(name);
	if(it == model->session.toolHandlers.end() || !it->second) return CopyCString("{\"error\":\"unknown tool\"}");
	try{
		const std::string response = it->second(slotName, argsJson ? argsJson : "");
		return CopyCString(response);
	} catch(const std::exception& ex){ return CopyCString(std::string("{\"error\":\"") + ex.what() + "\"}"); } catch(...){ return CopyCString("{\"error\":\"tool execution failed\"}"); }
}
static StudError RegisterAPIToolSchemas(const char* slotName, const char* toolsJson){
	ClearTools(slotName);
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
		if(!GetJsonPropertyRaw(toolDefinition, "parameters", parameters)) GetJsonPropertyRaw(toolDefinition, "input_schema", parameters);
		if(parameters.empty() || IsJsonNull(parameters)) parameters = "{}";
		AddTool(slotName, name.c_str(), description.c_str(), parameters.c_str(), nullptr);
	}
	return StudError::Success;
}
class ScopedAPITools{
public:
	ScopedAPITools(const char* slotName) : _model(GetModel(slotName)), _toolsSnapshot(_model->session.tools), _handlersSnapshot(_model->session.toolHandlers), _syntaxSnapshot(_model->session.syntax){}
	~ScopedAPITools(){ Restore(); }
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
bool IsModelSlotLoaded(const char* slotName){
	const auto* runtime = FindModel(NormName(slotName));
	return runtime && runtime->llModel && runtime->session.ctx;
}
StudError CreateContext(const char* slotName, const int nCtx, const int batchSize, const unsigned int flashAttn, const int nThreads, const int nThreadsBatch, const int kType, const int vType){
	const auto model = GetModel(slotName);
	if(model->session.ctx){
		llama_free(model->session.ctx);
		model->session.ctx = nullptr;
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
	switch(kType){
		case 1: ctxParams.type_k = GGML_TYPE_TQ1_0;
			break;
		case 2: ctxParams.type_k = GGML_TYPE_TQ2_0;
			break;
		default: ;
	}
	switch(vType){
		case 1: ctxParams.type_v = GGML_TYPE_TQ1_0;
			break;
		case 2: ctxParams.type_v = GGML_TYPE_TQ2_0;
			break;
		default: ;
	}
	_gpuOomStud = false;
	llama_log_set(GPUOomLogCallbackStud, nullptr);
	try{
		model->session.ctx = llama_init_from_model(model->llModel, ctxParams);
	} catch(std::exception& e){
		_lastErrorMessage = e.what();
		return StudError::CantCreateContext;
	}
	llama_log_set(nullptr, nullptr);
	if(!model->session.ctx){ return _gpuOomStud ? StudError::GpuOutOfMemory : StudError::CantCreateContext; }
	model->session.memory = llama_get_memory(model->session.ctx);
	auto result = StudError::Success;
	if(model->session.lanes[0].sampler) result = RetokenizeChat(slotName, true);
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
StudError CreateSampler(const char* slotName, const float minP, const float topP, const int topK, const float temp, const float repeatPenalty){
	const auto model = GetModel(slotName);
	const auto result = CreateSamplerInternal(minP, topP, topK, temp, repeatPenalty, model->session.lanes[0].sampler);
	if(result != StudError::Success) return result;
	return CreateSamplerInternal(minP, topP, topK, temp, repeatPenalty, model->session.lanes[1].sampler);
}
StudError CreateSession(const char* slotName, const int nCtx, const int batchSize, const unsigned int flashAttn, const int nThreads, const int nThreadsBatch, const float minP, const float topP, const int topK, const float temp, const float repeatPenalty, const int kType, const int vType){
	const auto model = GetModel(slotName);
	if(!model->llModel) return StudError::ModelNotLoaded;
	model->session.syntax.reasoning_format = COMMON_REASONING_FORMAT_AUTO;
	auto result = CreateContext(slotName, nCtx, batchSize, flashAttn, nThreads, nThreadsBatch, kType, vType);
	if(result != StudError::Success) return result;
	model->session.batchSize = batchSize;
	result = CreateSampler(slotName, minP, topP, topK, temp, repeatPenalty);
	if(result != StudError::Success) return result;
	return StudError::Success;
}
void DestroySession(const char* slotName){
	const auto model = GetModel(slotName);
	if(model->session.lanes[0].sampler){
		llama_sampler_free(model->session.lanes[0].sampler);
		model->session.lanes[0].sampler = nullptr;
	}
	if(model->session.lanes[1].sampler){
		llama_sampler_free(model->session.lanes[1].sampler);
		model->session.lanes[1].sampler = nullptr;
	}
	if(model->session.ctx){
		llama_free(model->session.ctx);
		model->session.ctx = nullptr;
	}
	DialecticFree(slotName);
}
void FreeModel(const char* slotName){
	const auto model = GetModel(slotName);
	DestroySession(slotName);
	model->session.lanes[0].cachedTokens.clear();
	model->session.lanes[1].cachedTokens.clear();
	model->session.vocab = nullptr;
	model->chatTemplates = nullptr;
	model->caps = jinja::caps();
	if(model->llModel){
		llama_model_free(model->llModel);
		model->llModel = nullptr;
	}
}
StudError LoadModel(const char* slotName, const char* filename, const char* jinjaTemplate, const int nGPULayers, const bool mMap, const bool mLock, const ggml_numa_strategy numaStrategy){
	const auto model = GetModel(slotName);
	if(model->llModel) FreeModel(slotName);
	auto params = llama_model_default_params();
	params.n_gpu_layers = nGPULayers;
	params.use_mlock = mLock;
	params.use_mmap = mMap;
	llama_numa_init(numaStrategy);
	_gpuOomStud = false;
	llama_log_set(GPUOomLogCallbackStud, nullptr);
	model->llModel = llama_model_load_from_file(filename, params);
	llama_log_set(nullptr, nullptr);
	if(!model->llModel){ return _gpuOomStud ? StudError::GpuOutOfMemory : StudError::CantLoadModel; }
	model->session.vocab = llama_model_get_vocab(model->llModel);
	std::string tmplSrc;
	if(jinjaTemplate && jinjaTemplate[0] != '\0'){
		model->chatTemplates = common_chat_templates_init(model->llModel, jinjaTemplate);
		tmplSrc = jinjaTemplate;
	} else{
		model->chatTemplates = common_chat_templates_init(model->llModel, "");
		tmplSrc = llama_model_chat_template(model->llModel, nullptr);
	}
	jinja::lexer lex;
	const jinja::lexer_result lexed = lex.tokenize(tmplSrc);
	jinja::program prog = jinja::parse_from_tokens(lexed);
	model->caps = jinja::caps_get(prog);
	return StudError::Success;
}
bool HasTool(const char* slotName, const char* name){
	const auto model = GetModel(slotName);
	for(const auto& tool : model->session.tools){ if(tool.name._Equal(name)) return true; }
	return false;
}
void SetTokenCallback(const Stud::TokenCallbackFn cb){ Stud::tokenCb = cb; }
void SetThreadCount(const int n, const int batchSize){
	for(auto&[slotName, model] : Stud::models){
		if(model->session.ctx) llama_set_n_threads(model->session.ctx, n, batchSize);
	}
}
int LlamaMemSize(const char* slotName){
	const auto model = GetModel(slotName);
	return static_cast<int>(model->session.lanes[model->session.activeLane].cachedTokens.size());
}
int GetStateSize(const char* slotName){
	const auto model = GetModel(slotName);
	if(!model->session.ctx) return 0;
	return static_cast<int>(llama_state_get_size(model->session.ctx));
}
StudError GetStateData(const char* slotName, unsigned char* dst, int size){
	const auto model = GetModel(slotName);
	if(!model->session.ctx || !dst || size <= 0){
		_lastErrorMessage = "Invalid Parameter";
		return StudError::Generic;
	}
	const auto expected = static_cast<size_t>(size);
	const auto copied = llama_state_get_data(model->session.ctx, dst, expected);
	if(copied != expected){
		_lastErrorMessage = "llama_state_get_data copied " + std::to_string(copied) + " bytes, expected " + std::to_string(expected);
		return StudError::Generic;
	}
	return StudError::Success;
}
StudError SetStateData(const char* slotName, const unsigned char* src, int size){
	const auto model = GetModel(slotName);
	if(!model->session.ctx || !src || size <= 0){
		_lastErrorMessage = "Invalid Parameter";
		return StudError::Generic;
	}
	const auto expected = static_cast<size_t>(size);
	const auto read = llama_state_set_data(model->session.ctx, src, expected);
	if(read != expected){
		_lastErrorMessage = "llama_state_set_data read " + std::to_string(read) + " bytes, expected " + std::to_string(expected);
		return StudError::Generic;
	}
	return StudError::Success;
}
static Stud::StudLane& ActiveLane(const char* slotName){
	const auto model = GetModel(slotName);
	return model->session.lanes[model->session.activeLane];
}
static Stud::StudLane& OtherLane(const char* slotName){
	const auto model = GetModel(slotName);
	return model->session.lanes[model->session.activeLane == 0 ? 1 : 0];
}
static StudError SaveDialecticState(const char* slotName){
	const auto model = GetModel(slotName);
	if(!model->session.ctx) return StudError::ModelNotLoaded;
	const int size = GetStateSize(slotName);
	if(size <= 0){
		_lastErrorMessage = "llama_state_get_size returned invalid size:" + std::to_string(size);
		return StudError::Generic;
	}
	auto& lane = ActiveLane(slotName);
	if(static_cast<int>(lane.state.size()) != size) lane.state.assign(size, 0);
	return GetStateData(slotName, lane.state.data(), size);
}
static StudError RestoreDialecticState(const char* slotName){
	const auto model = GetModel(slotName);
	if(!model->session.ctx) return StudError::ModelNotLoaded;
	const int size = GetStateSize(slotName);
	if(size <= 0){
		_lastErrorMessage = "llama_state_get_size returned invalid size:" + std::to_string(size);
		return StudError::Generic;
	}
	const auto& lane = ActiveLane(slotName);
	if(lane.state.empty()){
		_lastErrorMessage = "Dialectic relay state not initialised";
		return StudError::Generic;
	}
	if(static_cast<int>(lane.state.size()) != size){
		_lastErrorMessage = "Dialectic relay state size does not match active context";
		return StudError::Generic;
	}
	return SetStateData(slotName, lane.state.data(), size);
}
static common_chat_msg MirrorDialecticMessage(common_chat_msg msg){
	if(msg.role._Equal("assistant")){
		msg.role = "user";
		msg.reasoning_content.clear();
	} else if(msg.role._Equal("user")){ msg.role = "assistant"; }
	return msg;
}
static bool AlignDialecticRelayChatStates(const Stud::StudModel& source, Stud::StudModel& target){
	const auto& sourceMessages = source.session.lanes[source.session.activeLane].messages;
	auto& targetMessages = target.session.lanes[target.session.activeLane].messages;
	auto sourceLimit = sourceMessages.size();
	if(sourceLimit > 0 && sourceMessages.back().role._Equal("assistant")) --sourceLimit;
	if(targetMessages.size() > sourceLimit) return false;
	if(targetMessages.size() == sourceLimit) return false;
	for(size_t i = targetMessages.size(); i < sourceLimit; ++i) targetMessages.push_back(MirrorDialecticMessage(sourceMessages[i]));
	return true;
}
StudError DialecticInit(const char* slotName){
	const auto model = GetModel(slotName);
	const int size = GetStateSize(slotName);
	if(size <= 0){
		_lastErrorMessage = "llama_state_get_size returned invalid size:" + std::to_string(size);
		return StudError::Generic;
	}
	model->session.lanes[0].state.assign(size, 0);
	model->session.lanes[1].state.assign(size, 0);
	const auto err = GetStateData(slotName, model->session.lanes[0].state.data(), size);
	if(err != StudError::Success){
		return err;
	}
	memcpy(model->session.lanes[1].state.data(), model->session.lanes[0].state.data(), size);
	model->session.activeLane = 0;
	model->session.dialecticRelay = false;
	return StudError::Success;
}
StudError DialecticRelayInit(const char* slotName){
	const auto model = GetModel(slotName);
	const int size = GetStateSize(slotName);
	if(size <= 0){
		_lastErrorMessage = "llama_state_get_size returned invalid size:" + std::to_string(size);
		return StudError::Generic;
	}
	if(model->session.activeLane != 0){
		model->session.lanes[0].messages = ActiveLane(slotName).messages;
		model->session.lanes[0].cachedTokens = ActiveLane(slotName).cachedTokens;
	}
	model->session.activeLane = 0;
	model->session.dialecticRelay = true;
	model->session.assNextGen = false;
	model->session.lanes[0].state.assign(size, 0);
	model->session.lanes[1].messages.clear();
	model->session.lanes[1].cachedTokens.clear();
	model->session.lanes[1].state.clear();
	const auto err = GetStateData(slotName, model->session.lanes[0].state.data(), size);
	if(err != StudError::Success) return err;
	return StudError::Success;
}
StudError DialecticStart(const char* slotName){
	const auto& lane = ActiveLane(slotName);
	if(lane.state.empty()){
		_lastErrorMessage = "Dialectic states not initialised";
		return StudError::Generic;
	}
	const int size = static_cast<int>(lane.state.size());
	return SetStateData(slotName, lane.state.data(), size);
}
void DialecticFree(const char* slotName){
	const auto model = GetModel(slotName);
	model->session.lanes[0].state.clear();
	model->session.lanes[1].state.clear();
	model->session.dialecticRelay = false;
}
static bool SameModelDialecticActive(const char* slotName){
	const auto model = GetModel(slotName);
	if(model->session.dialecticRelay) return false;
	return !OtherLane(slotName).state.empty();
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
static bool TokenizePrompt(const char* slotName, const std::string& prompt, std::vector<llama_token>& tokens){
	try{
		tokens = common_tokenize(GetModel(slotName)->session.vocab, prompt, true, true);
		return true;
	} catch(std::exception& e){
		_lastErrorMessage = e.what();
		return false;
	}
}
static StudError BuildChatTemplateParamsForMessages(const char* slotName, common_chat_params& chatData, const std::vector<common_chat_msg>& chatMsgs, const bool addGenerationPrompt, const bool includeTools){
	const auto model = GetModel(slotName);
	const auto hasUser = std::any_of(chatMsgs.begin(), chatMsgs.end(), [](const common_chat_msg& msg){
		return msg.role == "user";
	});
	if(!hasUser){
		chatData = common_chat_params();
		return StudError::Success;
	}
	const bool useTools = includeTools && !model->session.tools.empty();
	std::vector<common_chat_msg> msgs;
	std::string prompt(model->session.systemPrompt);
	if(useTools && !model->session.toolsPrompt.empty()) prompt += model->session.toolsPrompt;
	msgs.push_back({"system", prompt});
	msgs.insert(msgs.end(), chatMsgs.begin(), chatMsgs.end());
	for(auto& msg : msgs) if(msg.content.empty()) msg.content = " ";
	common_chat_templates_inputs in;
	in.use_jinja = true;
	in.messages = msgs;
	in.add_generation_prompt = addGenerationPrompt;
	if(useTools){
		in.tools = model->session.tools;
		in.tool_choice = COMMON_CHAT_TOOL_CHOICE_AUTO;
		in.parallel_tool_calls = model->caps.supports_parallel_tool_calls;
	}
	in.reasoning_format = COMMON_REASONING_FORMAT_AUTO;
	try{ chatData = common_chat_templates_apply(model->chatTemplates.get(), in); } catch(std::exception& e){
		_lastErrorMessage = e.what();
		OutputDebugStringA((std::string("EXCEPTION:\r\n") + e.what()).c_str());
		return StudError::CantApplyTemplate;
	}
	return StudError::Success;
}
StudError RetokenizeChat(const char* slotName, bool rebuildMemory = false){
	const auto model = GetModel(slotName);
	auto& lane = ActiveLane(slotName);
	if(!model->session.ctx || !lane.sampler || !model->session.vocab) return StudError::ModelNotLoaded;
	const bool toolsEnabled = !model->session.tools.empty();
	const auto hasUser = std::any_of(lane.messages.begin(), lane.messages.end(), [](const common_chat_msg& msg){
		return msg.role == "user";
	});
	if(!hasUser){
		if(model->session.memory) llama_memory_clear(model->session.memory, true);
		lane.cachedTokens.clear();
		if(lane.sampler) llama_sampler_reset(lane.sampler);
		return LoadChatSyntax(model->session.syntax, common_chat_params(), false);
	}
	common_chat_params chatData;
	const auto applyErr = BuildChatTemplateParamsForMessages(slotName, chatData, lane.messages, false, toolsEnabled);
	if(applyErr != StudError::Success) return applyErr;
	const auto syntaxErr = LoadChatSyntax(model->session.syntax, chatData, toolsEnabled);
	if(syntaxErr != StudError::Success) return syntaxErr;
	std::vector<llama_token> promptTokens;
	if(!TokenizePrompt(slotName, chatData.prompt, promptTokens)) return StudError::CantTokenizePrompt;
	size_t prefix = 0;
	while(prefix < lane.cachedTokens.size() && prefix < promptTokens.size() && lane.cachedTokens[prefix] == promptTokens[prefix]){ ++prefix; }
	const bool canShift = llama_memory_can_shift(model->session.memory);
	const size_t oldSz = lane.cachedTokens.size();
	const size_t newSz = promptTokens.size();
	size_t suffix = 0;
	if(canShift && oldSz > 0 && newSz > 0){ while(suffix + prefix < oldSz && suffix + prefix < newSz && suffix < oldSz && suffix < newSz && lane.cachedTokens[oldSz - 1 - suffix] == promptTokens[newSz - 1 - suffix]){ ++suffix; } }
	const size_t oldSize = lane.cachedTokens.size();
	const size_t newSize = promptTokens.size();
	if(prefix == oldSize && oldSize == newSize) return StudError::Success;
	if(newSize > static_cast<size_t>(llama_n_ctx(model->session.ctx))){ return StudError::ConvTooLong; }
	if(rebuildMemory || !canShift){
		if(prefix == 0 || LlamaMemSize(slotName) < static_cast<llama_pos>(prefix - 1)){
			prefix = 0;
			llama_memory_clear(model->session.memory, true);
		}
	}
	if(!canShift && newSize < oldSize){
		prefix = 0;
		llama_memory_clear(model->session.memory, true);
	}
	if(canShift && suffix > 0){
		if(prefix < oldSize - suffix){ llama_memory_seq_rm(model->session.memory, 0, prefix, oldSize - suffix); }
		if(oldSize != newSize){
			const int delta = static_cast<int>(newSize) - static_cast<int>(oldSize);
			llama_memory_seq_add(model->session.memory, 0, oldSize - suffix, -1, delta);
		}
	} else{
		suffix = 0;
		if(prefix < oldSize){ llama_memory_seq_rm(model->session.memory, 0, prefix, -1); }
	}
	llama_sampler_reset(lane.sampler);
	for(size_t i = 0; i < prefix; ++i){ llama_sampler_accept(lane.sampler, promptTokens[i]); }
	const size_t decodeEnd = newSize - suffix;
	const int batchSize = std::min(model->session.batchSize, static_cast<int>(decodeEnd - prefix));
	if(batchSize > 0){
		for(size_t i = prefix; i < decodeEnd; i += model->session.batchSize){
			const int nTokens = std::min<int>(model->session.batchSize, decodeEnd - i);
			const auto batch = llama_batch_get_one(&promptTokens[i], nTokens);
			if(llama_decode(model->session.ctx, batch) != 0){
				if(model->session.memory) llama_memory_clear(model->session.memory, true);
				lane.cachedTokens.clear();
				if(lane.sampler) llama_sampler_reset(lane.sampler);
				return StudError::LlamaDecodeError;
			}
			for(int j = 0; j < nTokens; ++j){ llama_sampler_accept(lane.sampler, promptTokens[i + j]); }
		}
	}
	for(size_t i = decodeEnd; i < newSize; ++i){ llama_sampler_accept(lane.sampler, promptTokens[i]); }
	lane.cachedTokens = std::move(promptTokens);
	return StudError::Success;
}
static StudError DecodePromptText(const char* slotName, const std::string& prompt, const int dId, const common_chat_msg* appendMsg = nullptr){
	auto& session = GetModel(slotName)->session;
	auto& lane = session.lanes[dId];
	std::vector<llama_token> promptTokens;
	if(!TokenizePrompt(slotName, prompt, promptTokens)) return StudError::CantTokenizePrompt;
	if(appendMsg) lane.messages.push_back(*appendMsg);
	size_t p = 0;
	while(p < promptTokens.size() && !session.stop.load()){
		const int nEval = std::min<int>(session.batchSize, promptTokens.size() - p);
		const llama_batch batch = llama_batch_get_one(&promptTokens[p], nEval);
		const int nCtx = llama_n_ctx(session.ctx);
		const int nCtxUsed = static_cast<int>(lane.cachedTokens.size());
		if(nCtxUsed + batch.n_tokens > nCtx){
			if(appendMsg) lane.messages.pop_back();
			return StudError::ConvTooLong;
		}
		const auto result = llama_decode(session.ctx, batch);
		if(result != 0){
			if(appendMsg) lane.messages.pop_back();
			return result == 1 ? StudError::ConvTooLong : StudError::LlamaDecodeError;
		}
		for(int j = 0; j < nEval; ++j) llama_sampler_accept(lane.sampler, promptTokens[p + j]);
		lane.cachedTokens.insert(lane.cachedTokens.end(), promptTokens.begin() + p, promptTokens.begin() + p + nEval);
		p += nEval;
	}
	return StudError::Success;
}
static StudError DecodeSinglePromptMessage(const char* slotName, const common_chat_msg& message, const int dId, const bool addAss, const bool appendToChat = true){
	const auto model = GetModel(slotName);
	const auto& lane = model->session.lanes[dId];
	if(appendToChat && message.role == "user" && lane.messages.empty() && lane.cachedTokens.empty()){
		const std::vector<common_chat_msg> chatMsgs{message};
		common_chat_params chatData;
		const bool toolsEnabled = !model->session.tools.empty();
		auto err = BuildChatTemplateParamsForMessages(slotName, chatData, chatMsgs, addAss, toolsEnabled);
		if(err != StudError::Success) return err;
		err = LoadChatSyntax(model->session.syntax, chatData, toolsEnabled);
		return err == StudError::Success ? DecodePromptText(slotName, chatData.prompt, dId, &message) : err;
	}
	std::string formatted;
	try{ formatted = common_chat_format_single(model->chatTemplates.get(), lane.messages, message, addAss, message.role._Equal("assistant")); } catch(std::exception& e){
		_lastErrorMessage = e.what();
		OutputDebugStringA((std::string("EXCEPTION:\r\n") + e.what()).c_str());
		return StudError::CantApplyTemplate;
	}
	return DecodePromptText(slotName, formatted, dId, appendToChat ? &message : nullptr);
}
static void AlignChatStates(const char* slotName){
	auto& session = GetModel(slotName)->session;
	const auto& a = session.lanes[0].messages;
	const auto& b = session.lanes[1].messages;
	if(a.size() == b.size()) return;
	const int longerId = a.size() >= b.size() ? 0 : 1;
	const int shorterId = 1 - longerId;
	const auto& longer = session.lanes[longerId].messages;
	auto& shorter = session.lanes[shorterId].messages;
	for(size_t i = shorter.size(); i < longer.size(); ++i){
		auto msg = longer[i];
		if(msg.role._Equal("assistant")){
			msg.role = "user";
			msg.reasoning_content.clear();
		} else if(msg.role._Equal("user")){ msg.role = "assistant"; }
		const bool mirroredRoleIsAssistant = msg.role._Equal("assistant");
		shorter.push_back(msg);
		if(session.ctx && session.lanes[shorterId].sampler && session.vocab && !session.lanes[shorterId].state.empty() && session.activeLane == shorterId){
			const auto err = DecodeSinglePromptMessage(slotName, shorter.back(), shorterId, mirroredRoleIsAssistant, false);
			if(err == StudError::Success) session.assNextGen = mirroredRoleIsAssistant;
		}
	}
}
StudError DialecticSwap(const char* slotName){
	auto& session = GetModel(slotName)->session;
	if(session.dialecticRelay) return SaveDialecticState(slotName);
	if(session.lanes[0].state.empty()) return StudError::Success;
	AlignChatStates(slotName);
	const int size = static_cast<int>(ActiveLane(slotName).state.size());
	GetStateData(slotName, ActiveLane(slotName).state.data(), size);
	session.activeLane = session.activeLane == 0 ? 1 : 0;
	SetStateData(slotName, ActiveLane(slotName).state.data(), size);
	return StudError::Success;
}
StudError DialecticRelaySwap(const char* slotName, const char* fromSlotName, const char* toSlotName){
	const auto* fromModel = FindModel(NormName(fromSlotName));
	auto* toModel = FindModel(NormName(toSlotName));
	if(!fromModel || !toModel || !fromModel->llModel || !fromModel->session.ctx || !toModel->llModel || !toModel->session.ctx) return StudError::ModelNotLoaded;
	auto err = SaveDialecticState(slotName);
	if(err != StudError::Success) return err;
	err = RestoreDialecticState(slotName);
	if(err != StudError::Success) return err;
	if(AlignDialecticRelayChatStates(*fromModel, *toModel)){
		ActiveLane(slotName).cachedTokens.clear();
		err = RetokenizeChat(slotName, true);
		if(err != StudError::Success) return err;
		err = SaveDialecticState(slotName);
		if(err != StudError::Success) return err;
	}
	return StudError::Success;
}
StudError ResetChat(const char* slotName){
	auto& session = GetModel(slotName)->session;
	session.lanes[0].messages.clear();
	session.lanes[1].messages.clear();
	session.lanes[0].cachedTokens.clear();
	session.lanes[1].cachedTokens.clear();
	session.assNextGen = false;
	if(!session.ctx || !ActiveLane(slotName).sampler || !session.vocab){
		DialecticFree(slotName);
		return StudError::Success;
	}
	auto err = RetokenizeChat(slotName, true);
	if(err != StudError::Success) return err;
	if(session.dialecticRelay) return SaveDialecticState(slotName);
	if(!SameModelDialecticActive(slotName)) return err;
	err = DialecticSwap(slotName);
	if(err != StudError::Success) return err;
	const auto err2 = RetokenizeChat(slotName, true);
	const auto err3 = DialecticSwap(slotName);
	return err2 != StudError::Success ? err2 : err3;
}
StudError SetSystemPrompt(const char* slotName, const char* prompt, const char* toolsPrompt){
	auto& session = GetModel(slotName)->session;
	session.systemPrompt = std::string(prompt);
	session.toolsPrompt = std::string(toolsPrompt);
	if(!session.ctx || !ActiveLane(slotName).sampler || !session.vocab){
		ActiveLane(slotName).cachedTokens.clear();
		if(SameModelDialecticActive(slotName)) OtherLane(slotName).cachedTokens.clear();
		return StudError::Success;
	}
	auto err = RetokenizeChat(slotName);
	if(err != StudError::Success) return err;
	if(session.dialecticRelay) return SaveDialecticState(slotName);
	if(!SameModelDialecticActive(slotName)) return err;
	err = DialecticSwap(slotName);
	if(err != StudError::Success) return err;
	const auto err2 = RetokenizeChat(slotName);
	const auto err3 = DialecticSwap(slotName);
	return err2 != StudError::Success ? err2 : err3;
}
StudError SetMessageAt(const char* slotName, const int index, const char* think, const char* message){
	const auto& session = GetModel(slotName)->session;
	const bool mirror = SameModelDialecticActive(slotName);
	if(mirror) AlignChatStates(slotName);
	if(index < 0 || index >= static_cast<int>(ActiveLane(slotName).messages.size())) return StudError::IndexOutOfRange;
	const auto applyMessageUpdate = [&](std::vector<common_chat_msg>& msgs){
		msgs[index].reasoning_content = msgs[index].role._Equal("assistant") ? think : std::string();
		msgs[index].content = std::string(message);
	};
	if(!session.ctx || !ActiveLane(slotName).sampler || !session.vocab){
		applyMessageUpdate(ActiveLane(slotName).messages);
		if(mirror) applyMessageUpdate(OtherLane(slotName).messages);
		ActiveLane(slotName).cachedTokens.clear();
		if(mirror) OtherLane(slotName).cachedTokens.clear();
		return StudError::Success;
	}
	applyMessageUpdate(ActiveLane(slotName).messages);
	auto err = RetokenizeChat(slotName);
	const auto otherId = session.activeLane == 0 ? 1 : 0;
	if(err != StudError::Success) return err;
	if(session.dialecticRelay) return SaveDialecticState(slotName);
	if(!mirror || session.lanes[otherId].messages.size() <= index) return err;
	err = DialecticSwap(slotName);
	if(err != StudError::Success) return err;
	applyMessageUpdate(ActiveLane(slotName).messages);
	const auto err2 = RetokenizeChat(slotName);
	const auto err3 = DialecticSwap(slotName);
	return err2 != StudError::Success ? err2 : err3;
}
StudError RemoveMessageAt(const char* slotName, const int index){
	const auto& session = GetModel(slotName)->session;
	const bool mirror = SameModelDialecticActive(slotName);
	if(mirror) AlignChatStates(slotName);
	if(index < 0 || index >= static_cast<int>(ActiveLane(slotName).messages.size())) return StudError::IndexOutOfRange;
	if(!session.ctx || !ActiveLane(slotName).sampler || !session.vocab){
		ActiveLane(slotName).messages.erase(ActiveLane(slotName).messages.begin() + index);
		if(mirror) OtherLane(slotName).messages.erase(OtherLane(slotName).messages.begin() + index);
		ActiveLane(slotName).cachedTokens.clear();
		if(mirror) OtherLane(slotName).cachedTokens.clear();
		return StudError::Success;
	}
	ActiveLane(slotName).messages.erase(ActiveLane(slotName).messages.begin() + index);
	auto err = RetokenizeChat(slotName);
	const auto otherId = session.activeLane == 0 ? 1 : 0;
	if(err != StudError::Success) return err;
	if(session.dialecticRelay) return SaveDialecticState(slotName);
	if(!mirror || session.lanes[otherId].messages.size() <= index) return err;
	err = DialecticSwap(slotName);
	if(err != StudError::Success) return err;
	ActiveLane(slotName).messages.erase(ActiveLane(slotName).messages.begin() + index);
	const auto err2 = RetokenizeChat(slotName);
	const auto err3 = DialecticSwap(slotName);
	return err2 != StudError::Success ? err2 : err3;
}
StudError RemoveMessagesStartingAt(const char* slotName, int index){
	const auto& session = GetModel(slotName)->session;
	const bool mirror = SameModelDialecticActive(slotName);
	if(mirror) AlignChatStates(slotName);
	if(index < 0) index = 0;
	if(index > static_cast<int>(ActiveLane(slotName).messages.size())) index = static_cast<int>(ActiveLane(slotName).messages.size());
	if(!session.ctx || !ActiveLane(slotName).sampler || !session.vocab){
		ActiveLane(slotName).messages.erase(ActiveLane(slotName).messages.begin() + index, ActiveLane(slotName).messages.end());
		if(mirror) OtherLane(slotName).messages.erase(OtherLane(slotName).messages.begin() + index, OtherLane(slotName).messages.end());
		ActiveLane(slotName).cachedTokens.clear();
		if(mirror) OtherLane(slotName).cachedTokens.clear();
		return StudError::Success;
	}
	ActiveLane(slotName).messages.erase(ActiveLane(slotName).messages.begin() + index, ActiveLane(slotName).messages.end());
	auto err = RetokenizeChat(slotName);
	const auto otherId = session.activeLane == 0 ? 1 : 0;
	if(err != StudError::Success) return err;
	if(session.dialecticRelay) return SaveDialecticState(slotName);
	if(!mirror || session.lanes[otherId].messages.size() <= index) return err;
	err = DialecticSwap(slotName);
	if(err != StudError::Success) return err;
	ActiveLane(slotName).messages.erase(ActiveLane(slotName).messages.begin() + index, ActiveLane(slotName).messages.end());
	const auto err2 = RetokenizeChat(slotName);
	const auto err3 = DialecticSwap(slotName);
	return err2 != StudError::Success ? err2 : err3;
}
StudError AddMessage(const char* slotName, const Stud::MessageRole role, const char* think, const char* message){
	common_chat_msg msg;
	msg.role = RoleString(role);
	msg.reasoning_content = std::string(think);
	msg.content = std::string(message);
	ActiveLane(slotName).messages.push_back(msg);
	return RetokenizeChat(slotName);
}
static StudError ReplaceChatMessages(const char* slotName, std::vector<common_chat_msg>&& msgs){
	for(auto& msg : msgs) EnsureToolCallIds(msg);
	auto& session = GetModel(slotName)->session;
	const bool mirror = SameModelDialecticActive(slotName);
	ActiveLane(slotName).messages = std::move(msgs);
	if(mirror) OtherLane(slotName).messages.clear();
	session.assNextGen = false;
	if(mirror) AlignChatStates(slotName);
	ActiveLane(slotName).cachedTokens.clear();
	if(mirror) OtherLane(slotName).cachedTokens.clear();
	if(!session.ctx || !ActiveLane(slotName).sampler || !session.vocab) return StudError::Success;
	auto err = RetokenizeChat(slotName, true);
	if(err != StudError::Success) return err;
	if(session.dialecticRelay) return SaveDialecticState(slotName);
	if(!mirror) return err;
	err = DialecticSwap(slotName);
	if(err != StudError::Success) return err;
	const auto err2 = RetokenizeChat(slotName, true);
	const auto err3 = DialecticSwap(slotName);
	return err2 != StudError::Success ? err2 : err3;
}
StudError SyncChatMessages(const char* slotName, const int* roles, const char** thinks, const char** messages, int count){
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
	return ReplaceChatMessages(slotName, std::move(msgs));
}
StudError SyncChatMessagesJson(const char* slotName, const char* messagesJson){
	std::vector<common_chat_msg> msgs;
	const char* p = SkipJsonWhitespace(messagesJson);
	if(p && *p){
		try{ msgs = common_chat_msgs_parse_oaicompat(nlohmann::ordered_json::parse(p)); } catch(const std::exception& e){
			_lastErrorMessage = e.what();
			return StudError::ChatParseError;
		}
	}
	return ReplaceChatMessages(slotName, std::move(msgs));
}
static common_chat_msg ParseGeneratedMessage(const std::string& response, const common_chat_parser_params& syntax, const bool isPartial, std::string* toolParseError = nullptr){
	try{ return common_chat_parse(response, isPartial, syntax); } catch(std::exception& e){
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
	int memSize;
};
class AsyncTokenPostProcessor{
public:
	AsyncTokenPostProcessor(const char* slotName, const Stud::TokenCallbackFn callbackFn, const bool streamCallback, const common_chat_parser_params& chatSyntax, const HrClock::time_point& prepStart, std::string& responseText, common_chat_msg& parsedMsg, double& firstTokenTime) : _model(GetModel(slotName)), _callbackFn(callbackFn), _streamCallback(streamCallback), _chatSyntax(chatSyntax), _prepStart(prepStart), _responseText(responseText), _parsedMsg(parsedMsg), _firstTokenTime(firstTokenTime), _queue(kQueueCapacity){
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
			const int n = llama_token_to_piece(_model->session.vocab, pending.token, buf, sizeof buf, 0, false);
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
	Stud::StudModel* _model;
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
static StudError RollbackGenerate(const char* slotName, const size_t chatStart, const size_t newMessageCount, common_chat_msg& outMsg, const StudError error){
	const auto model = GetModel(slotName);
	model->session.lanes[model->session.activeLane].messages.resize(chatStart + newMessageCount);
	const auto rtErr = RetokenizeChat(slotName, true);
	outMsg = common_chat_msg();
	return rtErr != StudError::Success ? rtErr : error;
}
static StudError DecodePromptMessages(const char* slotName, const std::vector<common_chat_msg>& messages, common_chat_msg& outMsg){
	const auto model = GetModel(slotName);
	for(const auto& message : messages){
		const bool addAss = model->session.assNextGen || !message.role._Equal("assistant");
		model->session.assNextGen = false;
		const auto err = DecodeSinglePromptMessage(slotName, message, model->session.activeLane, addAss);
		if(err != StudError::Success){
			const auto rtErr = RetokenizeChat(slotName, true);
			outMsg = common_chat_msg();
			return rtErr != StudError::Success ? rtErr : err;
		}
	}
	return StudError::Success;
}
StudError Generate(const char* slotName, const std::vector<common_chat_msg>& messages, const int nPredict, const bool callback, common_chat_msg& outMsg, const bool emitFinalCallback = true, std::string* toolParseError = nullptr){
	if(toolParseError) toolParseError->clear();
	const auto prepStart = HrClock::now();
	auto model = GetModel(slotName);
	model->session.stop.store(false);
	const Stud::TokenCallbackFn cb = Stud::tokenCb;
	const size_t chatStart = model->session.lanes[model->session.activeLane].messages.size();
	const auto promptErr = DecodePromptMessages(slotName, messages, outMsg);
	if(promptErr != StudError::Success) return promptErr;
	const bool toolsEnabled = !model->session.tools.empty();
	common_chat_parser_params streamSyntax = model->session.syntax;
	if(callback){
		common_chat_params streamChatData;
		auto streamSyntaxErr = BuildChatTemplateParamsForMessages(slotName, streamChatData, model->session.lanes[model->session.activeLane].messages, true, false);
		if(streamSyntaxErr == StudError::Success) streamSyntaxErr = LoadChatSyntax(streamSyntax, streamChatData, false);
		if(streamSyntaxErr != StudError::Success) return RollbackGenerate(slotName, chatStart, messages.size(), outMsg, streamSyntaxErr);
	} else streamSyntax.parse_tool_calls = false;
	std::string response;
	common_chat_msg msg;
	double ftTime = 0.0;
	AsyncTokenPostProcessor postProcessor(slotName, cb, callback, streamSyntax, prepStart, response, msg, ftTime);
	auto failWith = [&](StudError error){
		postProcessor.Close();
		return RollbackGenerate(slotName, chatStart, messages.size(), outMsg, error);
	};
	int i = 0;
	while((nPredict < 0 || i < nPredict) && !model->session.stop.load()){
		const StudError pendingError = postProcessor.Error();
		if(pendingError != StudError::Success) return failWith(pendingError);
		if(LlamaMemSize(slotName) + 1 > llama_n_ctx(model->session.ctx)) return failWith(StudError::ConvTooLong);
		auto newTokenId = llama_sampler_sample(model->session.lanes[model->session.activeLane].sampler, model->session.ctx, -1);
		const auto isEog = llama_vocab_is_eog(model->session.vocab, newTokenId);
		const auto decodeErr = llama_decode(model->session.ctx, llama_batch_get_one(&newTokenId, 1));
		if(decodeErr != 0) return failWith(decodeErr == 1 ? StudError::ConvTooLong : StudError::LlamaDecodeError);
		llama_sampler_accept(model->session.lanes[model->session.activeLane].sampler, newTokenId);
		model->session.lanes[model->session.activeLane].cachedTokens.push_back(newTokenId);
		if(isEog) break;
		const auto enqueueErr = postProcessor.Enqueue(newTokenId, LlamaMemSize(slotName));
		if(enqueueErr != StudError::Success) return failWith(enqueueErr);
		++i;
	}
	postProcessor.Close();
	if(postProcessor.Error() != StudError::Success) return RollbackGenerate(slotName, chatStart, messages.size(), outMsg, postProcessor.Error());
	common_chat_params finalChatData;
	auto finalSyntaxErr = BuildChatTemplateParamsForMessages(slotName, finalChatData, model->session.lanes[model->session.activeLane].messages, true, toolsEnabled);
	if(finalSyntaxErr == StudError::Success) finalSyntaxErr = LoadChatSyntax(model->session.syntax, finalChatData, toolsEnabled);
	if(finalSyntaxErr != StudError::Success) return RollbackGenerate(slotName, chatStart, messages.size(), outMsg, finalSyntaxErr);
	try{
		msg = ParseGeneratedMessage(response, model->session.syntax, false, toolParseError);
		EnsureToolCallIds(msg);
	} catch(std::exception& e){
		_lastErrorMessage = e.what();
		return RollbackGenerate(slotName, chatStart, messages.size(), outMsg, StudError::ChatParseError);
	}
	model->session.lanes[model->session.activeLane].messages.push_back(msg);
	if(emitFinalCallback && cb && !callback) cb(msg.reasoning_content.c_str(), static_cast<int>(msg.reasoning_content.length()), msg.content.c_str(), static_cast<int>(msg.content.length()), i, LlamaMemSize(slotName), ftTime, 0);
	outMsg = std::move(msg);
	//OutputDebugStringA(("\n!!! CONTEXT START !!!\n" + std::string(GetContextAsText()) + "\n!!! CONTEXT END !!!\n").c_str());
	return StudError::Success;
}
StudError GenerateWithTools(const char* slotName, const Stud::MessageRole role, const char* prompt, const int nPredict, const bool callback){
	auto model = GetModel(slotName);
	common_chat_msg msg;
	msg.role = RoleString(role);
	msg.content = std::string(prompt);
	std::vector<common_chat_msg> msgs{msg};
	if(model->session.tools.empty()){ return Generate(slotName, msgs, nPredict, callback, msg); }
	const Stud::TokenCallbackFn cb = Stud::tokenCb;
	bool toolCalled;
	do{
		std::string toolParseError;
		const auto err = Generate(slotName, msgs, nPredict, callback, msg, true, &toolParseError);
		if(err != StudError::Success) return err;
		if(!model->session.lanes[model->session.activeLane].messages.size()) return StudError::Success;
		msgs.clear();
		try{
			toolCalled = false;
			if(!toolParseError.empty()){
				auto toolMsg = "{\"error\":\"Unable to parse tool call: " + JsonEscape(toolParseError) + "\"}";
				if(cb) cb(nullptr, 0, toolMsg.c_str(), static_cast<int>(toolMsg.length()), 0, LlamaMemSize(slotName), 0, 1);
				msgs.push_back(common_chat_msg());
				msgs.back().role = "tool";
				msgs.back().content = toolMsg;
				toolCalled = true;
			}
			for(common_chat_tool_call& toolCall : msg.tool_calls){
				if(model->session.stop.load()) return StudError::Success;
				auto toolCallMsg = "Tool name: " + toolCall.name + "\r\nTool ID: " + toolCall.id + "\r\nTool arguments: " + toolCall.arguments;
				if(cb) cb(nullptr, 0, toolCallMsg.c_str(), static_cast<int>(toolCallMsg.length()), 0, LlamaMemSize(slotName), 0, 3);
				auto it = model->session.toolHandlers.find(toolCall.name);
				if(it != model->session.toolHandlers.end()){
					auto toolMsg = it->second(slotName, toolCall.arguments.c_str());
					if(cb) cb(nullptr, 0, toolMsg.c_str(), static_cast<int>(toolMsg.length()), 0, LlamaMemSize(slotName), 0, 1);
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
StudError GenerateForAPI(const char* slotName, const Stud::MessageRole role, const char* prompt, const char* toolsJson, const int nPredict, char** responseJson){
	if(responseJson) *responseJson = nullptr;
	if(!responseJson){
		_lastErrorMessage = "responseJson is null.";
		return StudError::Generic;
	}
	ScopedAPITools apiTools(slotName);
	const auto toolsErr = RegisterAPIToolSchemas(slotName, toolsJson);
	if(toolsErr != StudError::Success) return toolsErr;
	const auto retokenizeErr = RetokenizeChat(slotName);
	if(retokenizeErr != StudError::Success) return retokenizeErr;
	common_chat_msg inputMsg;
	inputMsg.role = RoleString(role);
	inputMsg.content = prompt ? std::string(prompt) : std::string();
	std::vector<common_chat_msg> messages{inputMsg};
	common_chat_msg outputMsg;
	const auto generateErr = Generate(slotName, messages, nPredict, false, outputMsg, false);
	if(generateErr != StudError::Success) return generateErr;
	*responseJson = CopyCString(BuildAPIResponseJson(outputMsg));
	if(!*responseJson){
		_lastErrorMessage = "Unable to allocate API generation response.";
		return StudError::Generic;
	}
	return StudError::Success;
}
void StopGeneration(const char* slotName){
	GetModel(slotName)->session.stop.store(true);
	StopCMDOutput();
}
char* GetContextAsText(const char* slotName){
	const auto& session = GetModel(slotName)->session;
	if(!session.ctx) return nullptr;
	std::string outStr;
	outStr.reserve(session.lanes[session.activeLane].cachedTokens.size() * 4);
	for(const llama_token tok : session.lanes[session.activeLane].cachedTokens){ outStr += common_token_to_piece(session.ctx, tok, true); }
	auto* out = static_cast<char*>(std::malloc(outStr.size() + 1));
	if(out) std::memcpy(out, outStr.c_str(), outStr.size() + 1);
	return out;
}
extern "C" EXPORT void* CaptureChatState(const char* slotName){
	const auto& session = GetModel(slotName)->session;
	auto* snapshot = new(std::nothrow) Stud::StudSession();
	if(!snapshot) return nullptr;
	snapshot->lanes[0].messages = session.lanes[0].messages;
	snapshot->lanes[0].cachedTokens = session.lanes[0].cachedTokens;
	snapshot->lanes[0].state = session.lanes[0].state;
	snapshot->lanes[1].messages = session.lanes[1].messages;
	snapshot->lanes[1].cachedTokens = session.lanes[1].cachedTokens;
	snapshot->lanes[1].state = session.lanes[1].state;
	snapshot->activeLane = session.activeLane;
	snapshot->systemPrompt = session.systemPrompt;
	snapshot->toolsPrompt = session.toolsPrompt;
	snapshot->tools = session.tools;
	snapshot->toolHandlers = session.toolHandlers;
	snapshot->syntax = session.syntax;
	snapshot->assNextGen = session.assNextGen;
	snapshot->dialecticRelay = session.dialecticRelay;
	snapshot->batchSize = session.batchSize;
	return snapshot;
}
extern "C" EXPORT void RestoreChatState(const char* slotName, void* state){
	if(!state) return;
	auto& session = GetModel(slotName)->session;
	const auto* snapshot = static_cast<Stud::StudSession*>(state);
	session.lanes[0].messages = snapshot->lanes[0].messages;
	session.lanes[0].cachedTokens = snapshot->lanes[0].cachedTokens;
	session.lanes[0].state = snapshot->lanes[0].state;
	session.lanes[1].messages = snapshot->lanes[1].messages;
	session.lanes[1].cachedTokens = snapshot->lanes[1].cachedTokens;
	session.lanes[1].state = snapshot->lanes[1].state;
	session.activeLane = snapshot->activeLane;
	session.systemPrompt = snapshot->systemPrompt;
	session.toolsPrompt = snapshot->toolsPrompt;
	session.tools = snapshot->tools;
	session.toolHandlers = snapshot->toolHandlers;
	session.syntax = snapshot->syntax;
	session.assNextGen = snapshot->assNextGen;
	session.dialecticRelay = snapshot->dialecticRelay;
	session.batchSize = snapshot->batchSize;
}
extern "C" EXPORT void FreeChatState(void* state){ delete static_cast<Stud::StudSession*>(state); }
