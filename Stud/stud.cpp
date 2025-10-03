#include "stud.h"
#include "hug.h"
#include <filesystem>
#include <chrono>
#include <regex>
#include <unordered_map>
#include <minja\minja.hpp>
#include <minja\chat-template.hpp>
#include <algorithm>
#include <sstream>
struct RuntimeState{
	struct ModelRecord{
		llama_model* model = nullptr;
		const llama_vocab* vocab = nullptr;
		common_chat_templates_ptr chatTemplates;
		bool hasTools = false;
		bool defaultUseJinja = false;
		std::unordered_map<std::string, std::unique_ptr<ChatSession>> sessions;
		std::string activeSessionId;
	};
	std::unordered_map<std::string, ModelRecord> models;
	std::string activeModelId;
	std::string activeSessionId;
	llama_context* ctx = nullptr;
	llama_memory_t llMem = nullptr;
	std::string listScratch;
};
RuntimeState& GetRuntime(){
	static RuntimeState runtime;
	return runtime;
}
static RuntimeState::ModelRecord* GetActiveModelRecord(){
	auto& runtime = GetRuntime();
	if(runtime.activeModelId.empty()) return nullptr;
	const auto it = runtime.models.find(runtime.activeModelId);
	if(it == runtime.models.end()) return nullptr;
	return &it->second;
}
static ChatSession* GetActiveSession(){
	auto* model = GetActiveModelRecord();
	if(!model) return nullptr;
	auto& runtime = GetRuntime();
	const auto it = model->sessions.find(runtime.activeSessionId);
	if(it == model->sessions.end()) return nullptr;
	return it->second.get();
}
ChatSession& ActiveSession(){
	auto* session = GetActiveSession();
	if(!session){
		static ChatSession dummy;
		return dummy;
	}
	return *session;
}
const std::string& ActiveModelId(){ return GetRuntime().activeModelId; }
static llama_model*& ActiveModelHandle(){
	static llama_model* nullModel = nullptr;
	auto* model = GetActiveModelRecord();
	if(!model) return nullModel;
	return model->model;
}
static const llama_vocab*& ActiveVocabHandle(){
	static const llama_vocab* nullVocab = nullptr;
	auto* model = GetActiveModelRecord();
	if(!model) return nullVocab;
	return model->vocab;
}
static common_chat_templates_ptr& ActiveTemplatesHandle(){
	static common_chat_templates_ptr nullTemplates;
	auto* model = GetActiveModelRecord();
	if(!model) return nullTemplates;
	return model->chatTemplates;
}
static bool& ActiveToolsFlag(){
	static bool falseFlag = false;
	auto* model = GetActiveModelRecord();
	if(!model) return falseFlag;
	return model->hasTools;
}
#define _llModel ActiveModelHandle()
#define _vocab ActiveVocabHandle()
#define _chatTemplates ActiveTemplatesHandle()
#define _hasTools ActiveToolsFlag()
#define _session ActiveSession()
using HrClock = std::chrono::high_resolution_clock;
extern "C" void CloseCommandPrompt();
static bool _gpuOomStud = false;
static std::string _lastErrorMessage;
extern "C" EXPORT const char* GetLastErrorMessage(){ return _lastErrorMessage.c_str(); }
extern "C" EXPORT void ClearLastErrorMessage(){ _lastErrorMessage.clear(); }
static void ClearSessionState(ChatSession& session){
	for(auto& sampler : session.smpl){
		if(sampler){
			llama_sampler_free(sampler);
			sampler = nullptr;
		}
	}
	for(auto& msgs : session.chatMsgs){ msgs.clear(); }
	for(auto& tokens : session.cachedTokens){ tokens.clear(); }
	for(auto& state : session.dialState){ state.clear(); }
	session.prompt.clear();
	session.toolsPrompt.clear();
	session.ctx = nullptr;
	session.llMem = nullptr;
	session.dId = 0;
}
static ChatSession* EnsureSessionEntry(RuntimeState::ModelRecord& model, const std::string& sessionId){
	auto it = model.sessions.find(sessionId);
	if(it != model.sessions.end()) return it->second.get();
	auto session = std::make_unique<ChatSession>();
	session->syntax.reasoning_format = COMMON_REASONING_FORMAT_DEEPSEEK;
	session->useJinja = model.defaultUseJinja;
	auto* ptr = session.get();
	model.sessions.emplace(sessionId, std::move(session));
	return ptr;
}
static void SaveActiveSessionState(){
	auto& runtime = GetRuntime();
	auto* session = GetActiveSession();
	if(!session || !runtime.ctx) return;
	const int size = static_cast<int>(llama_state_get_size(runtime.ctx));
	if(size > 0){
		session->dialState[session->dId].assign(size, 0);
		llama_state_get_data(runtime.ctx, session->dialState[session->dId].data(), size);
	}
	session->ctx = nullptr;
	session->llMem = nullptr;
}
static void ReleaseContext(){
	auto& runtime = GetRuntime();
	if(runtime.ctx){
		llama_free(runtime.ctx);
		runtime.ctx = nullptr;
		runtime.llMem = nullptr;
	}
}
static void GPUOomLogCallbackStud(ggml_log_level level, const char* text, void* userData){
	if(level == GGML_LOG_LEVEL_ERROR || level == GGML_LOG_LEVEL_WARN){
		const std::string_view msg(text);
		if(msg.find("out of memory") != std::string_view::npos) _gpuOomStud = true;
	}
}
static StudError ActivateContext(ChatSession& session){
	auto& runtime = GetRuntime();
	if(!_llModel) return StudError::ModelNotLoaded;
	if(session.nCtx <= 0) return StudError::CantCreateContext;
	ReleaseContext();
	auto ctxParams = llama_context_default_params();
	ctxParams.n_ctx = session.nCtx;
	ctxParams.n_batch = session.nBatch;
	ctxParams.flash_attn = session.flashAttn;
	ctxParams.n_threads = session.nThreads;
	ctxParams.n_threads_batch = session.nThreadsBatch;
	_gpuOomStud = false;
	llama_log_set(GPUOomLogCallbackStud, nullptr);
	runtime.ctx = llama_init_from_model(_llModel, ctxParams);
	llama_log_set(nullptr, nullptr);
	if(!runtime.ctx){ return _gpuOomStud ? StudError::GpuOutOfMemory : StudError::CantCreateContext; }
	runtime.llMem = llama_get_memory(runtime.ctx);
	session.ctx = runtime.ctx;
	session.llMem = runtime.llMem;
	return StudError::Success;
}
static StudError RestoreSessionState(ChatSession& session, const bool rebuild){
	auto& runtime = GetRuntime();
	if(!runtime.ctx) return StudError::ModelNotLoaded;
	if(!session.dialState[session.dId].empty()){ llama_state_set_data(runtime.ctx, session.dialState[session.dId].data(), static_cast<int>(session.dialState[session.dId].size())); }
	if(session.smpl[session.dId]){
		auto err = RetokenizeChat(rebuild);
		if(err != StudError::Success) return err;
	}
	return StudError::Success;
}
void SetHWnd(HWND hWnd){ _hWnd = hWnd; }
void BackendInit(){
	_putenv("OMP_PROC_BIND=close");
	ggml_backend_load("ggml-cpu.dll");
	const HMODULE hModule = LoadLibraryA("nvcuda.dll");
	if(hModule != nullptr) ggml_backend_load("ggml-cuda.dll");
	llama_backend_init();
}
void AddTool(const char* name, const char* description, const char* parameters, std::string (*handler)(const char* args)){
	if(!name || !_hasTools) return;
	common_chat_tool tool;
	tool.name = name;
	if(description) tool.description = description;
	if(parameters) tool.parameters = parameters;
	_tools.push_back(tool);
	if(handler) _toolHandlers[name] = handler;
}
void ClearTools(){
	CloseCommandPrompt();
	_tools.clear();
	_toolHandlers.clear();
}
StudError CreateContext(const int nCtx, const int nBatch, const bool flashAttn, const int nThreads, const int nThreadsBatch){
	_session.nCtx = nCtx;
	_session.nBatch = nBatch;
	_session.flashAttn = flashAttn;
	_session.nThreads = nThreads;
	_session.nThreadsBatch = nThreadsBatch;
	auto err = ActivateContext(_session);
	if(err != StudError::Success) return err;
	if(_session.smpl[0]) return RetokenizeChat(true);
	return StudError::Success;
}
StudError CreateSamplerInternal(const float minP, const float topP, const int topK, const float temp, const float repeatPenalty, llama_sampler* & smpl){
	if(smpl){
		llama_sampler_free(smpl);
		smpl = nullptr;
	}
	smpl = llama_sampler_chain_init(llama_sampler_chain_default_params());
	if(!smpl){ return StudError::CantCreateSampler; }
	llama_sampler_chain_add(smpl, llama_sampler_init_penalties(128, repeatPenalty, 0.0f, 0.0f));
	// Optional: DRY (sequence) penalty immediately after penalties
	// llama_sampler_chain_add(_session.smpl, llama_sampler_init_dry(0.8f, 1.8f, -1));
	if(topK > 0) llama_sampler_chain_add(smpl, llama_sampler_init_top_k(topK));
	if(topP < 1.0f) llama_sampler_chain_add(smpl, llama_sampler_init_top_p(topP, 1));
	llama_sampler_chain_add(smpl, llama_sampler_init_temp(temp));
	llama_sampler_chain_add(smpl, llama_sampler_init_dist(LLAMA_DEFAULT_SEED));
	if(minP > 0.0f) llama_sampler_chain_add(smpl, llama_sampler_init_min_p(minP, 1));
	return StudError::Success;
}
StudError CreateSampler(const float minP, const float topP, const int topK, const float temp, const float repeatPenalty){
	_session.minP = minP;
	_session.topP = topP;
	_session.topK = topK;
	_session.temp = temp;
	_session.repeatPenalty = repeatPenalty;
	const auto result = CreateSamplerInternal(minP, topP, topK, temp, repeatPenalty, _session.smpl[0]);
	if(result != StudError::Success) return result;
	return CreateSamplerInternal(minP, topP, topK, temp, repeatPenalty, _session.smpl[1]);
}
StudError CreateSession(const int nCtx, const int nBatch, const bool flashAttn, const int nThreads, const int nThreadsBatch, const float minP, const float topP, const int topK, const float temp, const float repeatPenalty){
	if(!_llModel) return StudError::ModelNotLoaded;
	auto& runtime = GetRuntime();
	auto* model = GetActiveModelRecord();
	if(!model) return StudError::ModelNotLoaded;
	SaveActiveSessionState();
	if(runtime.activeSessionId.empty()) runtime.activeSessionId = STUD_DEFAULT_SESSION;
	model->activeSessionId = runtime.activeSessionId;
	auto* session = EnsureSessionEntry(*model, runtime.activeSessionId);
	(void)session;
	_session.useJinja = model->defaultUseJinja;
	_session.syntax.reasoning_format = COMMON_REASONING_FORMAT_DEEPSEEK;
	auto result = CreateContext(nCtx, nBatch, flashAttn, nThreads, nThreadsBatch);
	if(result != StudError::Success) return result;
	result = CreateSampler(minP, topP, topK, temp, repeatPenalty);
	if(result != StudError::Success) return result;
	return StudError::Success;
}
void DestroySession(){
	SaveActiveSessionState();
	ReleaseContext();
	auto* session = GetActiveSession();
	if(!session) return;
	ClearSessionState(*session);
	DialecticFree();
}
void FreeModel(){
	SaveActiveSessionState();
	ReleaseContext();
	auto& runtime = GetRuntime();
	auto* model = GetActiveModelRecord();
	if(!model){
		runtime.activeModelId.clear();
		runtime.activeSessionId.clear();
		return;
	}
	for(auto& entry : model->sessions){ ClearSessionState(*entry.second); }
	model->sessions.clear();
	if(model->model){
		llama_model_free(model->model);
		model->model = nullptr;
	}
	model->vocab = nullptr;
	model->chatTemplates.reset();
	model->hasTools = false;
	runtime.models.erase(runtime.activeModelId);
	runtime.activeModelId.clear();
	runtime.activeSessionId.clear();
}
StudError LoadModel(const char* filename, const char* jinjaTemplate, const int nGPULayers, const bool mMap, const bool mLock, const ggml_numa_strategy numaStrategy){
	const auto err = EnsureModel(STUD_DEFAULT_MODEL, filename, jinjaTemplate, nGPULayers, mMap, mLock, numaStrategy);
	if(err != StudError::Success) return err;
	return ActivateModel(STUD_DEFAULT_MODEL);
}
StudError EnsureModel(const char* modelId, const char* filename, const char* jinjaTemplate, const int nGPULayers, const bool mMap, const bool mLock, const ggml_numa_strategy numaStrategy){
	if(!filename || filename[0] == '\0') return StudError::CantLoadModel;
	const std::string id = (modelId && modelId[0] != '\0') ? modelId : STUD_DEFAULT_MODEL;
	auto& runtime = GetRuntime();
	auto& record = runtime.models[id];
	if(record.model) return StudError::Success;
	auto params = llama_model_default_params();
	params.n_gpu_layers = nGPULayers;
	params.use_mlock = mLock;
	params.use_mmap = mMap;
	llama_numa_init(numaStrategy);
	_gpuOomStud = false;
	llama_log_set(GPUOomLogCallbackStud, nullptr);
	record.model = llama_model_load_from_file(filename, params);
	llama_log_set(nullptr, nullptr);
	if(!record.model){
		runtime.models.erase(id);
		return _gpuOomStud ? StudError::GpuOutOfMemory : StudError::CantLoadModel;
	}
	record.vocab = llama_model_get_vocab(record.model);
	const auto bosStr = llama_vocab_get_text(record.vocab, llama_vocab_bos(record.vocab));
	const auto eosStr = llama_vocab_get_text(record.vocab, llama_vocab_eos(record.vocab));
	std::string tmplSrc;
	if(jinjaTemplate && jinjaTemplate[0] != '\0'){
		record.chatTemplates = common_chat_templates_init(record.model, jinjaTemplate, bosStr, eosStr);
		tmplSrc = jinjaTemplate;
	} else{
		record.chatTemplates = common_chat_templates_init(record.model, "");
		tmplSrc = llama_model_chat_template(record.model, nullptr);
	}
	record.hasTools = false;
	record.defaultUseJinja = false;
	if(!tmplSrc.empty()){
		try{
			const minja::chat_template tmpl(tmplSrc, bosStr, eosStr);
			record.hasTools = tmpl.original_caps().supports_tools;
			record.defaultUseJinja = true;
		} catch(...){ record.defaultUseJinja = false; }
	}
	auto* session = EnsureSessionEntry(record, STUD_DEFAULT_SESSION);
	session->useJinja = record.defaultUseJinja;
	session->syntax.reasoning_format = COMMON_REASONING_FORMAT_DEEPSEEK;
	record.activeSessionId = STUD_DEFAULT_SESSION;
	if(runtime.activeModelId.empty()) runtime.activeModelId = id;
	if(runtime.activeSessionId.empty()) runtime.activeSessionId = STUD_DEFAULT_SESSION;
	return StudError::Success;
}
StudError ActivateModel(const char* modelId){
	const std::string id = (modelId && modelId[0] != '\0') ? modelId : STUD_DEFAULT_MODEL;
	auto& runtime = GetRuntime();
	if(runtime.activeModelId == id) return StudError::Success;
	SaveActiveSessionState();
	auto* previous = GetActiveModelRecord();
	if(previous) previous->activeSessionId = runtime.activeSessionId;
	ReleaseContext();
	const auto it = runtime.models.find(id);
	if(it == runtime.models.end() || !it->second.model) return StudError::ModelNotLoaded;
	runtime.activeModelId = id;
	runtime.activeSessionId = it->second.activeSessionId;
	if(runtime.activeSessionId.empty()){
		if(it->second.sessions.empty()){
			it->second.activeSessionId = STUD_DEFAULT_SESSION;
			runtime.activeSessionId = STUD_DEFAULT_SESSION;
			EnsureSessionEntry(it->second, STUD_DEFAULT_SESSION);
		} else{
			runtime.activeSessionId = it->second.sessions.begin()->first;
			it->second.activeSessionId = runtime.activeSessionId;
		}
	}
	return StudError::Success;
}
void DestroyModel(const char* modelId){
	const std::string id = (modelId && modelId[0] != '\0') ? modelId : ActiveModelId();
	auto& runtime = GetRuntime();
	if(id.empty()) return;
	if(runtime.activeModelId == id){
		FreeModel();
		return;
	}
	const auto it = runtime.models.find(id);
	if(it == runtime.models.end()) return;
	for(auto& entry : it->second.sessions){ ClearSessionState(*entry.second); }
	if(it->second.model) llama_model_free(it->second.model);
	runtime.models.erase(it);
}
const char* ListModels(){
	auto& runtime = GetRuntime();
	std::ostringstream oss;
	bool first = true;
	for(const auto& kv : runtime.models){
		if(!kv.second.model) continue;
		if(!first) oss << '\n';
		first = false;
		oss << kv.first;
	}
	runtime.listScratch = oss.str();
	return runtime.listScratch.c_str();
}
StudError EnsureSessionId(const char* sessionId, const int nCtx, const int nBatch, const bool flashAttn, const int nThreads, const int nThreadsBatch, const float minP, const float topP, const int topK, const float temp, const float repeatPenalty, const char* modelId){
	std::string modelKey = (modelId && modelId[0] != '\0') ? modelId : ActiveModelId();
	if(modelKey.empty()) modelKey = STUD_DEFAULT_MODEL;
	auto& runtime = GetRuntime();
	const auto modelIt = runtime.models.find(modelKey);
	if(modelIt == runtime.models.end() || !modelIt->second.model) return StudError::ModelNotLoaded;
	std::string sessionKey = (sessionId && sessionId[0] != '\0') ? sessionId : STUD_DEFAULT_SESSION;
	auto* session = EnsureSessionEntry(modelIt->second, sessionKey);
	session->useJinja = modelIt->second.defaultUseJinja;
	session->nCtx = nCtx;
	session->nBatch = nBatch;
	session->flashAttn = flashAttn;
	session->nThreads = nThreads;
	session->nThreadsBatch = nThreadsBatch;
	session->minP = minP;
	session->topP = topP;
	session->topK = topK;
	session->temp = temp;
	session->repeatPenalty = repeatPenalty;
	session->syntax.reasoning_format = COMMON_REASONING_FORMAT_DEEPSEEK;
	auto err = CreateSamplerInternal(minP, topP, topK, temp, repeatPenalty, session->smpl[0]);
	if(err != StudError::Success) return err;
	err = CreateSamplerInternal(minP, topP, topK, temp, repeatPenalty, session->smpl[1]);
	if(err != StudError::Success) return err;
	if(modelIt->second.activeSessionId.empty()) modelIt->second.activeSessionId = sessionKey;
	if(runtime.activeModelId == modelKey && runtime.activeSessionId.empty()) runtime.activeSessionId = sessionKey;
	return StudError::Success;
}
StudError ActivateSessionId(const char* sessionId, const char* modelId){
	std::string modelKey = (modelId && modelId[0] != '\0') ? modelId : ActiveModelId();
	if(modelKey.empty()) modelKey = STUD_DEFAULT_MODEL;
	auto& runtime = GetRuntime();
	const auto modelIt = runtime.models.find(modelKey);
	if(modelIt == runtime.models.end() || !modelIt->second.model) return StudError::ModelNotLoaded;
	std::string sessionKey = (sessionId && sessionId[0] != '\0') ? sessionId : modelIt->second.activeSessionId;
	if(sessionKey.empty()) sessionKey = STUD_DEFAULT_SESSION;
	const auto sessionIt = modelIt->second.sessions.find(sessionKey);
	if(sessionIt == modelIt->second.sessions.end()) return StudError::IndexOutOfRange;
	if(runtime.activeModelId == modelKey && runtime.activeSessionId == sessionKey && runtime.ctx){
		auto* session = sessionIt->second.get();
		session->ctx = runtime.ctx;
		session->llMem = runtime.llMem;
		return StudError::Success;
	}
	if(runtime.activeModelId != modelKey){
		auto err = ActivateModel(modelKey.c_str());
		if(err != StudError::Success) return err;
	}
	if(runtime.activeSessionId != sessionKey) SaveActiveSessionState();
	runtime.activeSessionId = sessionKey;
	modelIt->second.activeSessionId = sessionKey;
	auto* session = sessionIt->second.get();
	auto err = ActivateContext(*session);
	if(err != StudError::Success) return err;
	return RestoreSessionState(*session, true);
}
void DestroySessionId(const char* sessionId, const char* modelId){
	std::string modelKey = (modelId && modelId[0] != '\0') ? modelId : ActiveModelId();
	if(modelKey.empty()) modelKey = STUD_DEFAULT_MODEL;
	auto& runtime = GetRuntime();
	const auto modelIt = runtime.models.find(modelKey);
	if(modelIt == runtime.models.end()) return;
	std::string sessionKey = (sessionId && sessionId[0] != '\0') ? sessionId : STUD_DEFAULT_SESSION;
	const auto sessionIt = modelIt->second.sessions.find(sessionKey);
	if(sessionIt == modelIt->second.sessions.end()) return;
	if(runtime.activeModelId == modelKey && runtime.activeSessionId == sessionKey){
		DestroySession();
		modelIt->second.sessions.erase(sessionIt);
		runtime.activeSessionId.clear();
		modelIt->second.activeSessionId.clear();
		return;
	}
	ClearSessionState(*sessionIt->second);
	modelIt->second.sessions.erase(sessionIt);
	if(modelIt->second.activeSessionId == sessionKey) modelIt->second.activeSessionId.clear();
}
const char* ListSessions(const char* modelId){
	std::string modelKey = (modelId && modelId[0] != '\0') ? modelId : ActiveModelId();
	if(modelKey.empty()) modelKey = STUD_DEFAULT_MODEL;
	auto& runtime = GetRuntime();
	const auto modelIt = runtime.models.find(modelKey);
	if(modelIt == runtime.models.end()){
		runtime.listScratch.clear();
		return runtime.listScratch.c_str();
	}
	std::ostringstream oss;
	bool first = true;
	for(const auto& kv : modelIt->second.sessions){
		if(!first) oss << '\n';
		first = false;
		oss << kv.first;
	}
	runtime.listScratch = oss.str();
	return runtime.listScratch.c_str();
}
bool HasTool(const char* name){
	for(const auto& tool : _tools){ if(tool.name._Equal(name)) return true; }
	return false;
}
void SetTokenCallback(const TokenCallbackFn cb){ _tokenCb = cb; }
void SetThreadCount(const int n, const int nBatch){ if(_session.ctx) llama_set_n_threads(_session.ctx, n, nBatch); }
int LlamaMemSize(){
	const int nCtxPosMin = llama_memory_seq_pos_min(_session.llMem, 0);
	const int nCtxPosMax = llama_memory_seq_pos_max(_session.llMem, 0);
	return nCtxPosMax - nCtxPosMin + 1;
}
int GetStateSize(){
	if(!_session.ctx) return 0;
	return static_cast<int>(llama_state_get_size(_session.ctx));
}
void GetStateData(unsigned char* dst, int size){ if(_session.ctx) llama_state_get_data(_session.ctx, dst, size); }
void SetStateData(const unsigned char* src, int size){ if(_session.ctx) llama_state_set_data(_session.ctx, src, size); }
void DialecticInit(){
	const int size = GetStateSize();
	if(size <= 0) return;
	_session.dialState[0].assign(size, 0);
	_session.dialState[1].assign(size, 0);
	GetStateData(_session.dialState[0].data(), size);
	GetStateData(_session.dialState[1].data(), size);
	_session.dId = 0;
}
void DialecticStart(){
	if(_session.dialState[0].empty()) return;
	const int size = static_cast<int>(_session.dialState[0].size());
	SetStateData(_session.dialState[_session.dId].data(), size);
}
StudError DialecticSwap(){
	if(_session.dialState[0].empty()) return StudError::Success;
	const int size = static_cast<int>(_session.dialState[0].size());
	GetStateData(_session.dialState[_session.dId].data(), size);
	_session.dId = 1 - _session.dId;
	SetStateData(_session.dialState[_session.dId].data(), size);
	return RetokenizeChat(true);
}
void DialecticFree(){
	_session.dialState[0].clear();
	_session.dialState[1].clear();
}
std::string RoleString(const MessageRole role){
	switch(role){
		case MessageRole::User: return std::string("user");
		case MessageRole::Assistant: return std::string("assistant");
		case MessageRole::Tool: return std::string("tool");
		default: return std::string();
	}
}
StudError RetokenizeChat(bool rebuildMemory = false){
	if(!_session.ctx || !_session.smpl[_session.dId] || !_vocab) return StudError::ModelNotLoaded;
	std::vector<common_chat_msg> msgs;
	std::string prompt(_session.prompt);
	if(_hasTools && !_session.toolsPrompt.empty()) prompt += _session.toolsPrompt;
	msgs.push_back({"system", prompt});
	msgs.insert(msgs.end(), _session.chatMsgs[_session.dId].begin(), _session.chatMsgs[_session.dId].end());
	for(auto& msg : msgs) if(msg.content.empty()) msg.content = " ";
	common_chat_templates_inputs in;
	in.use_jinja = _session.useJinja;
	in.messages = msgs;
	in.add_generation_prompt = false;
	in.tools = _tools;
	in.tool_choice = COMMON_CHAT_TOOL_CHOICE_AUTO;
	in.parallel_tool_calls = true;
	in.reasoning_format = COMMON_REASONING_FORMAT_DEEPSEEK;
	common_chat_params chatData;
	try{ chatData = common_chat_templates_apply(_chatTemplates.get(), in); } catch(std::exception& e){
		_lastErrorMessage = e.what();
		OutputDebugStringA((std::string("EXCEPTION:\r\n") + e.what()).c_str());
		return StudError::CantApplyTemplate;
	}
	_session.syntax.format = chatData.format;
	const int nPrompt = -llama_tokenize(_vocab, chatData.prompt.c_str(), chatData.prompt.size(), nullptr, 0, true, true);
	std::vector<llama_token> promptTokens(nPrompt);
	llama_tokenize(_vocab, chatData.prompt.c_str(), chatData.prompt.size(), promptTokens.data(), promptTokens.size(), true, true);
	size_t prefix = 0;
	while(prefix < _session.cachedTokens[_session.dId].size() && prefix < promptTokens.size() && _session.cachedTokens[_session.dId][prefix] == promptTokens[prefix]){ ++prefix; }
	const bool canShift = llama_memory_can_shift(_session.llMem);
	if(rebuildMemory){
		if(prefix == 0 || LlamaMemSize() < static_cast<llama_pos>(prefix - 1)){
			prefix = 0;
			llama_memory_clear(_session.llMem, true);
		}
	}
	const size_t oldSz = _session.cachedTokens[_session.dId].size();
	const size_t newSz = promptTokens.size();
	size_t suffix = 0;
	if(canShift && oldSz > 0 && newSz > 0){ while(suffix + prefix < oldSz && suffix + prefix < newSz && suffix < oldSz && suffix < newSz && _session.cachedTokens[_session.dId][oldSz - 1 - suffix] == promptTokens[newSz - 1 - suffix]){ ++suffix; } }
	const size_t oldSize = _session.cachedTokens[_session.dId].size();
	const size_t newSize = promptTokens.size();
	if(newSize > static_cast<size_t>(llama_n_ctx(_session.ctx))){ return StudError::ConvTooLong; }
	if(prefix == oldSize && oldSize == newSize) return StudError::Success;
	if(canShift && suffix > 0){
		if(prefix < oldSize - suffix){ llama_memory_seq_rm(_session.llMem, 0, prefix, oldSize - suffix); }
		if(oldSize != newSize){
			const int delta = static_cast<int>(newSize) - static_cast<int>(oldSize);
			llama_memory_seq_add(_session.llMem, 0, oldSize - suffix, -1, delta);
		}
	} else{
		suffix = 0;
		if(prefix < oldSize){ llama_memory_seq_rm(_session.llMem, 0, prefix, -1); }
	}
	llama_sampler_reset(_session.smpl[_session.dId]);
	for(size_t i = 0; i < prefix; ++i){ llama_sampler_accept(_session.smpl[_session.dId], promptTokens[i]); }
	const size_t decodeEnd = newSize - suffix;
	const int batchSize = std::min(_session.nBatch, static_cast<int>(decodeEnd - prefix));
	if(batchSize <= 0) return StudError::Success;
	for(size_t i = prefix; i < decodeEnd; i += _session.nBatch){
		const int nEval = std::min<int>(_session.nBatch, decodeEnd - i);
		auto batch = llama_batch_get_one(&promptTokens[i], nEval);
		if(llama_decode(_session.ctx, batch) != 0) return StudError::LlamaDecodeError;
		for(int j = 0; j < nEval; ++j){ llama_sampler_accept(_session.smpl[_session.dId], promptTokens[i + j]); }
	}
	for(size_t i = decodeEnd; i < newSize; ++i){ llama_sampler_accept(_session.smpl[_session.dId], promptTokens[i]); }
	_session.cachedTokens[_session.dId] = std::move(promptTokens);
	return StudError::Success;
}
static void AlignChatStates(){
	auto& a = _session.chatMsgs[0];
	auto& b = _session.chatMsgs[1];
	if(a.size() == b.size()) return;
	auto* longer = &a;
	auto* shorter = &b;
	if(a.size() < b.size()){
		longer = &b;
		shorter = &a;
	}
	for(size_t i = shorter->size(); i < longer->size(); ++i){
		auto msg = (*longer)[i];
		if(msg.role._Equal("assistant")){
			msg.role = "user";
			msg.reasoning_content.clear();
		} else if(msg.role._Equal("user")){ msg.role = "assistant"; }
		shorter->push_back(std::move(msg));
	}
}
StudError ResetChat(){
	_session.chatMsgs[0].clear();
	_session.chatMsgs[1].clear();
	_session.cachedTokens[0].clear();
	_session.cachedTokens[1].clear();
	if(!_session.ctx || !_session.smpl[_session.dId] || !_vocab){
		DialecticFree();
		return StudError::Success;
	}
	auto err = RetokenizeChat(true);
	if(err != StudError::Success || _session.dialState[_session.dId == 0 ? 1 : 0].empty()) return err;
	err = DialecticSwap();
	if(err != StudError::Success) return err;
	auto err2 = RetokenizeChat(true);
	auto err3 = DialecticSwap();
	return err2 != StudError::Success ? err2 : err3;
}
StudError SetSystemPrompt(const char* prompt, const char* toolsPrompt){
	_session.prompt = std::string(prompt);
	_session.toolsPrompt = std::string(toolsPrompt);
	_session.cachedTokens[0].clear();
	_session.cachedTokens[1].clear();
	if(!_session.ctx || !_session.smpl[_session.dId] || !_vocab) return StudError::Success;
	auto err = RetokenizeChat();
	if(err != StudError::Success || _session.dialState[_session.dId == 0 ? 1 : 0].empty()) return err;
	err = DialecticSwap();
	if(err != StudError::Success) return err;
	auto err2 = RetokenizeChat();
	auto err3 = DialecticSwap();
	return err2 != StudError::Success ? err2 : err3;
}
StudError SetMessageAt(const int index, const char* think, const char* message){
	AlignChatStates();
	if(index < 0 || index >= static_cast<int>(_session.chatMsgs[_session.dId].size())) return StudError::IndexOutOfRange;
	if(!_session.ctx || !_session.smpl[_session.dId] || !_vocab){
		_session.chatMsgs[0][index].reasoning_content = think;
		_session.chatMsgs[0][index].content = std::string(message);
		_session.chatMsgs[1][index].reasoning_content = think;
		_session.chatMsgs[1][index].content = std::string(message);
		_session.cachedTokens[0].clear();
		_session.cachedTokens[1].clear();
		return StudError::Success;
	}
	_session.chatMsgs[_session.dId][index].reasoning_content = think;
	_session.chatMsgs[_session.dId][index].content = std::string(message);
	auto err = RetokenizeChat();
	const auto dId = _session.dId == 0 ? 1 : 0;
	if(err != StudError::Success || _session.dialState[dId].empty() || _session.chatMsgs[dId].size() <= index) return err;
	err = DialecticSwap();
	if(err != StudError::Success) return err;
	_session.chatMsgs[_session.dId][index].reasoning_content = think;
	_session.chatMsgs[_session.dId][index].content = std::string(message);
	auto err2 = RetokenizeChat();
	auto err3 = DialecticSwap();
	return err2 != StudError::Success ? err2 : err3;
}
StudError RemoveMessageAt(const int index){
	AlignChatStates();
	if(index < 0 || index >= static_cast<int>(_session.chatMsgs[_session.dId].size())) return StudError::IndexOutOfRange;
	if(!_session.ctx || !_session.smpl[_session.dId] || !_vocab){
		_session.chatMsgs[0].erase(_session.chatMsgs[0].begin() + index);
		_session.chatMsgs[1].erase(_session.chatMsgs[1].begin() + index);
		_session.cachedTokens[0].clear();
		_session.cachedTokens[1].clear();
		return StudError::Success;
	}
	_session.chatMsgs[_session.dId].erase(_session.chatMsgs[_session.dId].begin() + index);
	auto err = RetokenizeChat();
	const auto dId = _session.dId == 0 ? 1 : 0;
	if(err != StudError::Success || _session.dialState[dId].empty() || _session.chatMsgs[dId].size() <= index) return err;
	err = DialecticSwap();
	if(err != StudError::Success) return err;
	_session.chatMsgs[_session.dId].erase(_session.chatMsgs[_session.dId].begin() + index);
	auto err2 = RetokenizeChat();
	auto err3 = DialecticSwap();
	return err2 != StudError::Success ? err2 : err3;
}
StudError RemoveMessagesStartingAt(int index){
	AlignChatStates();
	if(index < 0) index = 0;
	if(index > static_cast<int>(_session.chatMsgs[_session.dId].size())) index = static_cast<int>(_session.chatMsgs[_session.dId].size());
	if(!_session.ctx || !_session.smpl[_session.dId] || !_vocab){
		_session.chatMsgs[0].erase(_session.chatMsgs[0].begin() + index, _session.chatMsgs[0].end());
		_session.chatMsgs[1].erase(_session.chatMsgs[1].begin() + index, _session.chatMsgs[1].end());
		_session.cachedTokens[0].clear();
		_session.cachedTokens[1].clear();
		return StudError::Success;
	}
	_session.chatMsgs[_session.dId].erase(_session.chatMsgs[_session.dId].begin() + index, _session.chatMsgs[_session.dId].end());
	auto err = RetokenizeChat();
	const auto dId = _session.dId == 0 ? 1 : 0;
	if(err != StudError::Success || _session.dialState[dId].empty() || _session.chatMsgs[dId].size() <= index) return err;
	err = DialecticSwap();
	if(err != StudError::Success) return err;
	_session.chatMsgs[_session.dId].erase(_session.chatMsgs[_session.dId].begin() + index, _session.chatMsgs[_session.dId].end());
	auto err2 = RetokenizeChat();
	auto err3 = DialecticSwap();
	return err2 != StudError::Success ? err2 : err3;
}
StudError AddMessage(const MessageRole role, const char* message){
	common_chat_msg msg;
	msg.role = RoleString(role);
	msg.content = std::string(message);
	_session.chatMsgs[_session.dId].push_back(msg);
	return RetokenizeChat();
}
static std::string OpenToolResponseTag(){
	switch(_session.syntax.format){
		case COMMON_CHAT_FORMAT_DEEPSEEK_R1: return "<｜tool▁outputs▁begin｜>";
		case COMMON_CHAT_FORMAT_COMMAND_R7B: return "<|START_RESPONSE|>";
		case COMMON_CHAT_FORMAT_LLAMA_3_X:
		case COMMON_CHAT_FORMAT_LLAMA_3_X_WITH_BUILTIN_TOOLS: return "<|start_header_id|>ipython<|end_header_id|>";
		default: return "<tool_response>";
	}
}
static std::string CloseToolResponseTag(){
	switch(_session.syntax.format){
		case COMMON_CHAT_FORMAT_DEEPSEEK_R1: return "<｜tool▁outputs▁end｜>";
		case COMMON_CHAT_FORMAT_COMMAND_R7B: return "<|END_RESPONSE|>";
		case COMMON_CHAT_FORMAT_LLAMA_3_X:
		case COMMON_CHAT_FORMAT_LLAMA_3_X_WITH_BUILTIN_TOOLS: return "<|eot_id|>";
		default: return "</tool_response>";
	}
}
static std::string OpenToolCallTag(){
	switch(_session.syntax.format){
		case COMMON_CHAT_FORMAT_DEEPSEEK_R1: return "<｜tool▁call▁begin｜>";
		case COMMON_CHAT_FORMAT_COMMAND_R7B: return "<|START_ACTION|>";
		case COMMON_CHAT_FORMAT_MISTRAL_NEMO: return "[TOOL_CALLS]";
		case COMMON_CHAT_FORMAT_FIREFUNCTION_V2: return "functools[";
		case COMMON_CHAT_FORMAT_FUNCTIONARY_V3_1_LLAMA_3_1: return "<function=";
		default: return "<tool_call>";
	}
}
static std::string CloseToolCallTag(){
	switch(_session.syntax.format){
		case COMMON_CHAT_FORMAT_DEEPSEEK_R1: return "<｜tool▁call▁end｜>";
		case COMMON_CHAT_FORMAT_COMMAND_R7B: return "<|END_ACTION|>";
		case COMMON_CHAT_FORMAT_FIREFUNCTION_V2: return "]";
		case COMMON_CHAT_FORMAT_FUNCTIONARY_V3_1_LLAMA_3_1: return "</function>";
		case COMMON_CHAT_FORMAT_MISTRAL_NEMO: return "";
		default: return "</tool_call>";
	}
}
static std::string OpenThinkTag(){
	switch(_session.syntax.format){
		case COMMON_CHAT_FORMAT_COMMAND_R7B: return "<|START_THINKING|>";
		case COMMON_CHAT_FORMAT_LLAMA_3_X:
		case COMMON_CHAT_FORMAT_LLAMA_3_X_WITH_BUILTIN_TOOLS: return "<|start_header_id|>analysis<|end_header_id|>";
		default: return "<think>";
	}
}
static std::string CloseThinkTag(){
	switch(_session.syntax.format){
		case COMMON_CHAT_FORMAT_COMMAND_R7B: return "<|END_THINKING|>";
		case COMMON_CHAT_FORMAT_LLAMA_3_X:
		case COMMON_CHAT_FORMAT_LLAMA_3_X_WITH_BUILTIN_TOOLS: return "<|eot_id|>";
		default: return "</think>";
	}
}
static StudError doTool(std::string_view tok, ToolCtx& s, const bool cbOn, double& ftTime, const HrClock::time_point& t0, llama_memory_t llMem, std::string& response, std::vector<llama_token>& newTokens, const TokenCallbackFn cb){
	if(tok == OpenThinkTag()){
		s.inThink = true;
		return StudError::Success;
	}
	if(tok == CloseThinkTag()){
		s.inThink = false;
		return StudError::Success;
	}
	if(!_hasTools || !s.inThink) return StudError::Success;
	if(tok == OpenToolCallTag()){
		s.inCall = true;
		s.buf = tok;
		return StudError::Success;
	}
	if(s.inCall){
		if(tok != CloseToolCallTag() && tok != OpenToolResponseTag()){
			s.buf += tok;
			return StudError::Success;
		}
		if(tok == CloseToolCallTag()) s.buf += CloseToolCallTag();
		s.inCall = false;
		_session.syntax.parse_tool_calls = true;
		auto p = common_chat_parse(s.buf, true, _session.syntax);
		s.buf.clear();
		if(p.tool_calls.empty()) return StudError::Success;
		auto& c = p.tool_calls.back();
		auto tokenizeAndRun = [&](const std::string& text){
			if(text.empty()) return StudError::Success;
			const int n = -llama_tokenize(_vocab, text.c_str(), text.size(), nullptr, 0, true, true);
			std::vector<llama_token> v(n);
			llama_tokenize(_vocab, text.c_str(), text.size(), v.data(), n, true, true);
			for(size_t i = 0; i < v.size();){
				const int b = std::min<int>(_session.nBatch, v.size() - i);
				const llama_batch lb = llama_batch_get_one(&v[i], b);
				if(LlamaMemSize() + lb.n_tokens > llama_n_ctx(_session.ctx)) return StudError::ConvTooLong;
				if(llama_decode(_session.ctx, lb) != 0) return StudError::LlamaDecodeError;
				for(int k = 0; k < b; ++k) llama_sampler_accept(_session.smpl[_session.dId], v[i + k]);
				i += b;
			}
			newTokens.insert(newTokens.end(), v.begin(), v.end());
			response += text;
			return StudError::Success;
		};
		if(auto h = _toolHandlers.find(c.name); h != _toolHandlers.end()){
			if(tok == CloseToolCallTag()){
				std::string open = "\n" + OpenToolResponseTag();
				auto err = tokenizeAndRun(open);
				if(err != StudError::Success) return err;
			}
			std::string out = "\n" + h->second(c.arguments.c_str());
			auto err = tokenizeAndRun(out);
			if(err != StudError::Success) return err;
			std::string close = "\n" + CloseToolResponseTag();
			err = tokenizeAndRun(close);
			if(err != StudError::Success) return err;
		}
		if(_tokenCb && cbOn){
			_session.syntax.parse_tool_calls = false;
			auto msg = common_chat_parse(response, true, _session.syntax);
			cb(msg.reasoning_content.c_str(), static_cast<int>(msg.reasoning_content.length()), msg.content.c_str(), static_cast<int>(msg.content.length()), 1, LlamaMemSize(), ftTime, 0);
		}
	}
	return StudError::Success;
}
StudError Generate(const std::vector<common_chat_msg>& messages, const int nPredict, const bool callback, common_chat_msg& outMsg){
	const auto prepStart = HrClock::now();
	_stop.store(false);
	const TokenCallbackFn cb = _tokenCb;
	const size_t chatStart = _session.chatMsgs[_session.dId].size();
	for(const auto& message : messages){
		const auto formatted = common_chat_format_single(_chatTemplates.get(), _session.chatMsgs[_session.dId], message, !message.role._Equal("assistant"), _session.useJinja && message.role._Equal("assistant"));
		const int nPromptTokens = -llama_tokenize(_vocab, formatted.c_str(), formatted.size(), nullptr, 0, true, true);
		std::vector<llama_token> promptTokens(nPromptTokens);
		if(llama_tokenize(_vocab, formatted.c_str(), formatted.size(), promptTokens.data(), promptTokens.size(), true, true) < 0){
			outMsg = common_chat_msg();
			return StudError::CantTokenizePrompt;
		}
		_session.chatMsgs[_session.dId].push_back(message);
		size_t p = 0;
		while(p < promptTokens.size() && !_stop.load()){
			const int nEval = std::min<int>(_session.nBatch, promptTokens.size() - p);
			llama_batch batch = llama_batch_get_one(&promptTokens[p], nEval);
			const int nCtx = llama_n_ctx(_session.ctx);
			const int nCtxUsed = LlamaMemSize();
			if(nCtxUsed + batch.n_tokens > nCtx){
				_session.chatMsgs[_session.dId].pop_back();
				auto rtErr = RetokenizeChat(true);
				outMsg = common_chat_msg();
				return rtErr != StudError::Success ? rtErr : StudError::ConvTooLong;
			}
			auto result = llama_decode(_session.ctx, batch);
			if(result != 0){
				_session.chatMsgs[_session.dId].pop_back();
				auto rtErr = RetokenizeChat(true);
				outMsg = common_chat_msg();
				if(result == 1) return rtErr != StudError::Success ? rtErr : StudError::ConvTooLong;
				return rtErr != StudError::Success ? rtErr : StudError::LlamaDecodeError;
			}
			for(int j = 0; j < nEval; ++j){ llama_sampler_accept(_session.smpl[_session.dId], promptTokens[p + j]); }
			p += nEval;
		}
		_session.cachedTokens[_session.dId].insert(_session.cachedTokens[_session.dId].end(), promptTokens.begin(), promptTokens.end());
	}
	auto status = StudError::Success;
	llama_token newTokenId;
	std::vector<llama_token> newTokens;
	std::string response;
	common_chat_msg msg;
	ToolCtx tool;
	double ftTime = 0.0;
	int i = 0;
	while((nPredict < 0 || i < nPredict) && !_stop.load()){
		newTokenId = llama_sampler_sample(_session.smpl[_session.dId], _session.ctx, -1);
		if(llama_vocab_is_eog(_vocab, newTokenId)) _stop.store(true);
		char buf[256];
		const int n = llama_token_to_piece(_vocab, newTokenId, buf, sizeof buf, 0, false);
		if(n < 0){
			_session.chatMsgs[_session.dId].resize(chatStart + messages.size());
			auto rtErr = RetokenizeChat(true);
			outMsg = common_chat_msg();
			return rtErr != StudError::Success ? rtErr : StudError::CantConvertToken;
		}
		newTokens.push_back(newTokenId);
		if(ftTime == 0.0) ftTime = std::chrono::duration<double>(HrClock::now() - prepStart).count();
		std::string tokenStr(buf, n);
		response += tokenStr;
		++i;
		if(cb && callback && !tokenStr.empty()){
			_session.syntax.parse_tool_calls = false;
			msg = common_chat_parse(response, true, _session.syntax);
			cb(msg.reasoning_content.c_str(), static_cast<int>(msg.reasoning_content.length()), msg.content.c_str(), static_cast<int>(msg.content.length()), 1, LlamaMemSize(), ftTime, 0);
		}
		auto batch = llama_batch_get_one(&newTokenId, 1);
		const int nCtx = llama_n_ctx(_session.ctx);
		const int nCtxUsed = LlamaMemSize();
		if(nCtxUsed + batch.n_tokens > nCtx){
			_session.chatMsgs[_session.dId].resize(chatStart + messages.size());
			auto rtErr = RetokenizeChat(true);
			outMsg = common_chat_msg();
			return rtErr != StudError::Success ? rtErr : StudError::ConvTooLong;
		}
		auto decodeErr = llama_decode(_session.ctx, batch);
		if(decodeErr != 0){
			_session.chatMsgs[_session.dId].resize(chatStart + messages.size());
			auto rtErr = RetokenizeChat(true);
			outMsg = common_chat_msg();
			if(decodeErr == 1) return rtErr != StudError::Success ? rtErr : StudError::ConvTooLong;
			return rtErr != StudError::Success ? rtErr : StudError::LlamaDecodeError;
		}
		llama_sampler_accept(_session.smpl[_session.dId], newTokenId);
		if(_hasTools){
			auto toolErr = doTool(tokenStr, tool, callback, ftTime, prepStart, _session.llMem, response, newTokens, cb);
			if(toolErr != StudError::Success){
				status = toolErr;
				_stop.store(true);
			}
		}
	}
	_session.syntax.parse_tool_calls = false;
	msg = common_chat_parse(response, false, _session.syntax);
	_session.chatMsgs[_session.dId].push_back(msg);
	_session.cachedTokens[_session.dId].insert(_session.cachedTokens[_session.dId].end(), newTokens.begin(), newTokens.end());
	if(cb && !callback) cb(msg.reasoning_content.c_str(), static_cast<int>(msg.reasoning_content.length()), msg.content.c_str(), static_cast<int>(msg.content.length()), i, LlamaMemSize(), ftTime, 0);
	outMsg = std::move(msg);
	//OutputDebugStringA(("\n---\n" + std::string(GetContextAsText()) + "\n---\n").c_str());
	return status;
}
StudError GenerateWithTools(const MessageRole role, const char* prompt, const int nPredict, const bool callback){
	common_chat_msg msg;
	msg.role = RoleString(role);
	msg.content = std::string(prompt);
	std::vector<common_chat_msg> msgs{msg};
	if(!_hasTools){ return Generate(msgs, nPredict, callback, msg); }
	const TokenCallbackFn cb = _tokenCb;
	auto err = StudError::Success;
	bool toolCalled = false;
	do{
		err = Generate(msgs, nPredict, callback, msg);
		if(err != StudError::Success) return err;
		if(!_session.chatMsgs[_session.dId].size()) return StudError::Success;
		msgs.clear();
		_session.syntax.parse_tool_calls = true;
		try{
			auto parsed = common_chat_parse(msg.content, false, _session.syntax);
			msg.content.clear();
			msg.reasoning_content.clear();
			msg.tool_calls = parsed.tool_calls;
			toolCalled = false;
			for(common_chat_tool_call& toolCall : parsed.tool_calls){
				auto it = _toolHandlers.find(toolCall.name);
				if(it != _toolHandlers.end()){
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
void StopGeneration(){ _stop.store(true); }
char* GetContextAsText(){
	if(!_session.ctx) return nullptr;
	std::string outStr;
	outStr.reserve(_session.cachedTokens[_session.dId].size() * 4);
	for(const llama_token tok : _session.cachedTokens[_session.dId]){ outStr += common_token_to_piece(_session.ctx, tok, true); }
	auto* out = static_cast<char*>(std::malloc(outStr.size() + 1));
	if(out) std::memcpy(out, outStr.c_str(), outStr.size() + 1);
	return out;
}