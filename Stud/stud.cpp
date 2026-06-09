#include "stud.h"
#include "StudState.h"
#include "ChatMessageParser.h"
#include "hug.h"
#include "JSONCommon.h"
#include "MCP.h"
#include <mtmd-helper.h>
#include <filesystem>
#include <chrono>
#include <regex>
#include <unordered_map>
#include <algorithm>
#include <cctype>
#include <cstdlib>
#include <cstring>
#include <mutex>
#include <string_view>
#include <system_error>
#include <thread>
#include <jinja\parser.h>

#pragma comment(lib, "llama.lib")
#pragma comment(lib, "llama-common.lib")
#pragma comment(lib, "ggml.lib")
#pragma comment(lib, "ggml-base.lib")
#pragma comment(lib, "mtmd.lib")

using HrClock = std::chrono::high_resolution_clock;
extern "C" void CloseCommandPrompt();
extern "C" void StopCMDOutput();
extern "C" void MarkToolsJsonDirty();
static bool _gpuOomStud = false;
static std::string _lastErrorMessage;
static constexpr int MtpDraftTokensMax = 62;
static constexpr int MtpPrefillBatchMax = 32;
static constexpr int VisionImageMaxTokens = 1024;
extern "C" EXPORT const char* GetLastErrorMessage(){ return _lastErrorMessage.c_str(); }
extern "C" EXPORT void ClearLastErrorMessage(){ _lastErrorMessage.clear(); }
static void AppendLastErrorLogMessage(std::string_view msg){
	while(!msg.empty() && (msg.back() == '\r' || msg.back() == '\n')) msg.remove_suffix(1);
	if(msg.empty()) return;
	if(!_lastErrorMessage.empty()) _lastErrorMessage += "\r\n";
	_lastErrorMessage.append(msg.data(), msg.size());
}
static void GPUOomLogCallbackStud(const ggml_log_level level, const char* text, void* userData){
	if(level == GGML_LOG_LEVEL_ERROR || level == GGML_LOG_LEVEL_WARN){
		const std::string_view msg(text ? text : "");
		if(msg.find("out of memory") != std::string_view::npos ||
			msg.find("cudaMalloc failed") != std::string_view::npos)
			_gpuOomStud = true;
	}
	if(level == GGML_LOG_LEVEL_ERROR) AppendLastErrorLogMessage(text ? text : "");
}
class ScopedBackendErrorCapture{
public:
	ScopedBackendErrorCapture() : logLock(Stud::llamaLogMutex){
		mtmd_helper_log_set(GPUOomLogCallbackStud, nullptr);
		llama_log_set(GPUOomLogCallbackStud, nullptr);
	}
	~ScopedBackendErrorCapture(){
		llama_log_set(nullptr, nullptr);
		mtmd_helper_log_set(nullptr, nullptr);
	}
	ScopedBackendErrorCapture(const ScopedBackendErrorCapture&) = delete;
	ScopedBackendErrorCapture& operator=(const ScopedBackendErrorCapture&) = delete;
private:
	std::unique_lock<std::mutex> logLock;
};
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
	std::lock_guard<std::mutex> lock(Stud::modelsMutex);
	const auto it = Stud::models.find(slotName);
	return it == Stud::models.end() ? nullptr : it->second.get();
}
static Stud::StudModel& GetOrCreateModel(const std::string& slotName){
	std::lock_guard<std::mutex> lock(Stud::modelsMutex);
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
extern "C" EXPORT void SetManagedToolCallback(const Stud::ManagedToolCallbackFn cb){ Stud::managedToolCb = cb; }
extern "C" EXPORT void StreamManagedToolOutput(const char* slotName, const char* text){
	if(!text || !*text) return;
	const auto cb = Stud::tokenCb;
	if(!cb) return;
	cb(slotName, nullptr, text, 0, LlamaMemSize(slotName), 0.0, 2);
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
static bool TryExecuteMCPTool(const common_chat_tool_call& toolCall, std::string& toolResult){
	if(!MCPHasTool(toolCall.name.c_str())) return false;
	char* result = MCPExecuteTool(toolCall.name.c_str(), toolCall.arguments.c_str());
	if(!result){
		toolResult = "{\"error\":\"MCP tool execution failed\"}";
		return true;
	}
	try{ toolResult = result; }
	catch(...){ toolResult = "{\"error\":\"MCP tool execution failed\"}"; }
	std::free(result);
	return true;
}
static bool TryExecuteManagedTool(const char* callerSlotName, const common_chat_tool_call& toolCall, std::string& toolResult){
	const auto cb = Stud::managedToolCb;
	if(!cb) return false;
	char* result = cb(callerSlotName, toolCall.name.c_str(), toolCall.arguments.c_str());
	if(!result) return false;
	try{ toolResult = result; }
	catch(...){ toolResult = "{\"error\":\"managed tool execution failed\"}"; }
	LocalFree(result);
	return true;
}
extern "C" EXPORT char* ExecuteTool(const char* slotName, const char* name, const char* argsJson){
	if(!name || name[0] == '\0') return CopyCString("{\"error\":\"missing tool name\"}");
	const auto model = GetModel(slotName);
	const auto it = model->session.toolHandlers.find(name);
	if(it == model->session.toolHandlers.end() || !it->second) return CopyCString("{\"error\":\"unknown tool\"}");
	try{
		const std::string response = it->second(slotName, argsJson ? argsJson : "");
		return CopyCString(response);
	} catch(const std::exception& ex){ return CopyCString(std::string("{\"error\":\"") + JsonEscape(ex.what()) + "\"}"); } catch(...){ return CopyCString("{\"error\":\"tool execution failed\"}"); }
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
static std::string NormalizeModelPathKey(const char* filename){
	if(!filename || filename[0] == '\0') return std::string();
	std::filesystem::path path = std::filesystem::u8path(filename);
	std::error_code ec;
	auto normalized = std::filesystem::weakly_canonical(path, ec);
	if(ec){
		ec.clear();
		normalized = std::filesystem::absolute(path, ec);
		if(ec) normalized = path;
	}
	normalized = normalized.lexically_normal();
	auto key = normalized.u8string();
	std::transform(key.begin(), key.end(), key.begin(), [](unsigned char ch){ return static_cast<char>(std::tolower(ch)); });
	return key;
}
static std::string BuildSharedModelCacheKey(const char* filename, const int nGPULayers, const bool mMap, const bool mLock, const ggml_numa_strategy numaStrategy){
	std::string key = NormalizeModelPathKey(filename);
	key += "|gpu=" + std::to_string(nGPULayers);
	key += "|mmap=" + std::to_string(mMap ? 1 : 0);
	key += "|mlock=" + std::to_string(mLock ? 1 : 0);
	key += "|numa=" + std::to_string(numaStrategy);
	return key;
}
static std::shared_ptr<Stud::StudSharedModel> LoadOrReuseSharedModel(const char* filename, const int nGPULayers, const bool mMap, const bool mLock, const ggml_numa_strategy numaStrategy){
	if(!filename || filename[0] == '\0'){
		_lastErrorMessage = "Model filename is empty.";
		return nullptr;
	}
	const auto key = BuildSharedModelCacheKey(filename, nGPULayers, mMap, mLock, numaStrategy);
	std::lock_guard<std::mutex> cacheLock(Stud::sharedModelsMutex);
	const auto it = Stud::sharedModels.find(key);
	if(it != Stud::sharedModels.end()){
		if(auto shared = it->second.lock()) return shared;
		Stud::sharedModels.erase(it);
	}
	auto params = llama_model_default_params();
	params.n_gpu_layers = nGPULayers;
	params.use_mlock = mLock;
	params.use_mmap = mMap;
	llama_numa_init(numaStrategy);
	_gpuOomStud = false;
	llama_model* llModel = nullptr;
	{
		std::lock_guard<std::mutex> logLock(Stud::llamaLogMutex);
		llama_log_set(GPUOomLogCallbackStud, nullptr);
		try{
			llModel = llama_model_load_from_file(filename, params);
		} catch(const std::exception& e){ _lastErrorMessage = e.what(); } catch(...){ _lastErrorMessage = "llama_model_load_from_file failed."; }
		llama_log_set(nullptr, nullptr);
	}
	if(!llModel) return nullptr;
	auto shared = std::make_shared<Stud::StudSharedModel>();
	shared->llModel = llModel;
	shared->cacheKey = key;
	Stud::sharedModels[key] = shared;
	return shared;
}
bool IsModelSlotLoaded(const char* slotName){
	const auto* runtime = FindModel(NormName(slotName));
	return runtime && runtime->sharedModel && runtime->llModel && runtime->session.ctx;
}
static void FreeSpeculativeContext(Stud::StudSession& session){
	if(session.speculative){
		common_speculative_free(session.speculative);
		session.speculative = nullptr;
	}
	if(session.mtpCtx){
		llama_free(session.mtpCtx);
		session.mtpCtx = nullptr;
	}
	if(session.mtpTargetBatchCapacity > 0){
		llama_batch_free(session.mtpTargetBatch);
		session.mtpTargetBatch = {};
		session.mtpTargetBatchCapacity = 0;
	}
}
static void ClearSessionMemory(Stud::StudSession& session){
	if(session.memory) llama_memory_clear(session.memory, true);
	if(session.mtpCtx){
		if(const auto mtpMemory = llama_get_memory(session.mtpCtx)) llama_memory_clear(mtpMemory, true);
	}
}
static bool RemoveSessionMemoryAfter(Stud::StudSession& session, const llama_pos pos){
	bool ok = true;
	if(session.memory) ok = llama_memory_seq_rm(session.memory, 0, pos, -1) && ok;
	if(session.mtpCtx){
		if(const auto mtpMemory = llama_get_memory(session.mtpCtx)) ok = llama_memory_seq_rm(mtpMemory, 0, pos, -1) && ok;
	}
	return ok;
}
static bool RemoveMtpMemoryAfter(Stud::StudSession& session, const llama_pos pos){
	if(!session.mtpCtx) return true;
	const auto mtpMemory = llama_get_memory(session.mtpCtx);
	return !mtpMemory || llama_memory_seq_rm(mtpMemory, 0, pos, -1);
}
static int EffectiveMtpDraftTokens(const int mtpDraftTokens, const llama_context_params& ctxParams){
	const int ctxRoom = ctxParams.n_ctx == 0 ? mtpDraftTokens : std::max(0, static_cast<int>(ctxParams.n_ctx) - 1);
	const int batchRoom = std::max(0, static_cast<int>(ctxParams.n_batch) - 1);
	return std::min({mtpDraftTokens, ctxRoom, batchRoom});
}
static int EvalBatchSize(const Stud::StudSession& session){
	return session.speculative ? std::max(1, std::min(session.batchSize, MtpPrefillBatchMax)) : session.batchSize;
}
static llama_batch* EnsureMtpTargetBatch(Stud::StudSession& session, const int nTokens){
	if(session.mtpTargetBatchCapacity >= nTokens) return &session.mtpTargetBatch;
	if(session.mtpTargetBatchCapacity > 0){
		llama_batch_free(session.mtpTargetBatch);
		session.mtpTargetBatch = {};
		session.mtpTargetBatchCapacity = 0;
	}
	session.mtpTargetBatch = llama_batch_init(nTokens, 0, 1);
	if(!session.mtpTargetBatch.token || !session.mtpTargetBatch.pos || !session.mtpTargetBatch.n_seq_id || !session.mtpTargetBatch.seq_id || !session.mtpTargetBatch.logits){
		llama_batch_free(session.mtpTargetBatch);
		session.mtpTargetBatch = {};
		return nullptr;
	}
	session.mtpTargetBatchCapacity = nTokens;
	return &session.mtpTargetBatch;
}
static void AddSingleSeqToken(llama_batch& batch, const llama_token token, const llama_pos pos, const bool logits){
	const int i = batch.n_tokens++;
	batch.token[i] = token;
	batch.pos[i] = pos;
	batch.n_seq_id[i] = 1;
	batch.seq_id[i][0] = 0;
	batch.logits[i] = logits;
}
static StudError DecodeTokenBatch(Stud::StudSession& session, llama_token* tokens, const int nTokens, const llama_pos posStart, const bool outputAll){
	if(nTokens <= 0) return StudError::Success;
	if(!session.speculative){
		const auto batch = llama_batch_get_one(tokens, nTokens);
		const auto result = llama_decode(session.ctx, batch);
		return result == 0 ? StudError::Success : result == 1 ? StudError::ConvTooLong : StudError::LlamaDecodeError;
	}
	auto* batchPtr = EnsureMtpTargetBatch(session, nTokens);
	if(!batchPtr) return StudError::LlamaDecodeError;
	auto& batch = *batchPtr;
	common_batch_clear(batch);
	for(int i = 0; i < nTokens; ++i) AddSingleSeqToken(batch, tokens[i], posStart + i, outputAll || i == nTokens - 1);
	const auto result = llama_decode(session.ctx, batch);
	if(result == 0 && !common_speculative_process(session.speculative, batch)) return StudError::LlamaDecodeError;
	return result == 0 ? StudError::Success : result == 1 ? StudError::ConvTooLong : StudError::LlamaDecodeError;
}
static void TryCreateMtpContext(Stud::StudModel* model, llama_context_params ctxParams, const int mtpDraftTokens){
	if(mtpDraftTokens <= 0 || !model->session.ctx) return;
	if(model->session.visionCtx){
		AppendLastErrorLogMessage("MTP disabled: speculative decoding cannot mirror multimodal image embeddings.");
		return;
	}
	const auto seqRmType = common_context_can_seq_rm(model->session.ctx);
	if(seqRmType == COMMON_CONTEXT_SEQ_RM_TYPE_FULL || seqRmType == COMMON_CONTEXT_SEQ_RM_TYPE_NO){
		AppendLastErrorLogMessage("MTP disabled: this model context cannot roll back partially accepted draft tokens.");
		return;
	}
	ctxParams.ctx_type = LLAMA_CONTEXT_TYPE_MTP;
	ctxParams.n_rs_seq = static_cast<uint32_t>(mtpDraftTokens + 1);
	llama_context* mtpCtx = nullptr;
	try{ mtpCtx = llama_init_from_model(model->llModel, ctxParams); }
	catch(const std::exception& e){ AppendLastErrorLogMessage(e.what()); }
	catch(...){ AppendLastErrorLogMessage("llama_init_from_model failed for the MTP context."); }
	if(!mtpCtx){
		AppendLastErrorLogMessage("MTP disabled: the loaded model does not expose a compatible MTP head.");
		return;
	}
	const auto mtpSeqRmType = common_context_can_seq_rm(mtpCtx);
	if(mtpSeqRmType == COMMON_CONTEXT_SEQ_RM_TYPE_FULL || mtpSeqRmType == COMMON_CONTEXT_SEQ_RM_TYPE_NO){
		llama_free(mtpCtx);
		AppendLastErrorLogMessage("MTP disabled: the MTP draft context cannot roll back draft state.");
		return;
	}
	common_params_speculative specParams;
	specParams.types = {COMMON_SPECULATIVE_TYPE_DRAFT_MTP};
	specParams.draft.cache_type_k = ctxParams.type_k;
	specParams.draft.cache_type_v = ctxParams.type_v;
	specParams.draft.ctx_tgt = model->session.ctx;
	specParams.draft.ctx_dft = mtpCtx;
	specParams.draft.n_max = mtpDraftTokens;
	specParams.draft.n_min = 0;
	try{
		model->session.speculative = common_speculative_init(specParams, 1);
	} catch(const std::exception& e){
		AppendLastErrorLogMessage(e.what());
	} catch(...){
		AppendLastErrorLogMessage("common_speculative_init failed for MTP.");
	}
	if(!model->session.speculative){
		llama_free(mtpCtx);
		AppendLastErrorLogMessage("MTP disabled: llama.cpp did not create a speculative decoder.");
		return;
	}
	model->session.mtpCtx = mtpCtx;
	model->session.mtpDraftTokens = mtpDraftTokens;
}
static void RecreateSpecCtx(Stud::StudModel* model){
	auto& session = model->session;
	const int mtpDraftTokens = session.mtpDraftTokens;
	if(mtpDraftTokens <= 0 || !session.ctx || !model->llModel || !session.hasCtxParams) return;
	const auto ctxParams = session.ctxParams;
	FreeSpeculativeContext(session);
	TryCreateMtpContext(model, ctxParams, mtpDraftTokens);
}
static void ClearSessMemAndSpecState(Stud::StudModel* model){
	ClearSessionMemory(model->session);
	RecreateSpecCtx(model);
}
StudError CreateContext(const char* slotName, const int nCtx, const int batchSize, const unsigned int flashAttn, const int nThreads, const int nThreadsBatch, const int kType, const int vType, const int mtpDraftTokens){
	const auto model = GetModel(slotName);
	FreeSpeculativeContext(model->session);
	model->session.mtpDraftTokens = 0;
	if(model->session.ctx){
		llama_free(model->session.ctx);
		model->session.ctx = nullptr;
		model->session.memory = nullptr;
	}
	model->session.syntax.reasoning_format = COMMON_REASONING_FORMAT_AUTO;
	model->session.batchSize = batchSize;
	auto ctxParams = llama_context_default_params();
	ctxParams.n_ctx = nCtx;
	ctxParams.n_batch = batchSize;
	const int requestedMtpDraftTokens = model->session.visionCtx ? 0 : mtpDraftTokens;
	const int mtpDraftTokensEffective = EffectiveMtpDraftTokens(std::max(0, std::min(requestedMtpDraftTokens, MtpDraftTokensMax)), ctxParams);
	model->session.mtpDraftTokens = mtpDraftTokensEffective;
	ctxParams.n_rs_seq = mtpDraftTokensEffective > 0 ? static_cast<uint32_t>(mtpDraftTokensEffective) : 0;
	ctxParams.offload_kqv = true;
	ctxParams.op_offload = true;
	if(flashAttn == 0) ctxParams.flash_attn_type = LLAMA_FLASH_ATTN_TYPE_DISABLED;
	else if(flashAttn == 1) ctxParams.flash_attn_type = LLAMA_FLASH_ATTN_TYPE_ENABLED;
	else if(flashAttn == 2) ctxParams.flash_attn_type = LLAMA_FLASH_ATTN_TYPE_AUTO;
	ctxParams.n_threads = nThreads;
	ctxParams.n_threads_batch = nThreadsBatch;
	switch(kType){
		case 1: ctxParams.type_k = GGML_TYPE_Q4_0;
			break;
		case 2: ctxParams.type_k = GGML_TYPE_Q5_0;
			break;
		case 3: ctxParams.type_k = GGML_TYPE_Q8_0;
			break;
		case 4: ctxParams.type_k = GGML_TYPE_F16;
			break;
		default: ;
	}
	switch(vType){
		case 1: ctxParams.type_v = GGML_TYPE_Q4_0;
			break;
		case 2: ctxParams.type_v = GGML_TYPE_Q5_0;
			break;
		case 3: ctxParams.type_v = GGML_TYPE_Q8_0;
			break;
		case 4: ctxParams.type_v = GGML_TYPE_F16;
			break;
		default:;
	}
	constexpr auto initFailedMessage = "llama_init_from_model failed.";
	{
		std::lock_guard<std::mutex> logLock(Stud::llamaLogMutex);
		_gpuOomStud = false;
		_lastErrorMessage.clear();
		llama_log_set(GPUOomLogCallbackStud, nullptr);
		try{
			model->session.ctx = llama_init_from_model(model->llModel, ctxParams);
		} catch(const std::exception& e){ _lastErrorMessage = e.what(); } catch(...){ _lastErrorMessage = initFailedMessage; }
		llama_log_set(nullptr, nullptr);
	}
	if(!model->session.ctx){
		if(_lastErrorMessage.empty()) _lastErrorMessage = initFailedMessage;
		return _gpuOomStud ? StudError::GpuOutOfMemory : StudError::CantCreateContext;
	}
	model->session.memory = llama_get_memory(model->session.ctx);
	model->session.ctxParams = ctxParams;
	model->session.hasCtxParams = true;
	TryCreateMtpContext(model, ctxParams, mtpDraftTokensEffective);
	_lastErrorMessage.clear();
	auto result = StudError::Success;
	if(model->session.lane.sampler) result = RetokenizeChat(slotName, true);
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
	return CreateSamplerInternal(minP, topP, topK, temp, repeatPenalty, model->session.lane.sampler);
}
void DestroySession(const char* slotName){
	const auto model = GetModel(slotName);
	FreeSpeculativeContext(model->session);
	if(model->session.lane.sampler){
		llama_sampler_free(model->session.lane.sampler);
		model->session.lane.sampler = nullptr;
	}
	if(model->session.ctx){
		llama_free(model->session.ctx);
		model->session.ctx = nullptr;
		model->session.memory = nullptr;
	}
	model->session.mtpDraftTokens = 0;
	model->session.hasCtxParams = false;
}
void FreeModel(const char* slotName){
	const auto model = GetModel(slotName);
	if(model->session.visionCtx){
		mtmd_free(model->session.visionCtx);
		model->session.visionCtx = nullptr;
	}
	DestroySession(slotName);
	model->session.lane.cachedTokens.clear();
	model->session.vocab = nullptr;
	model->chatTemplates = nullptr;
	model->caps = jinja::caps();
	model->sharedModel.reset();
	model->llModel = nullptr;
}
void FreeModelSlot(const char* slotName){ FreeModel(slotName); }
void FreeAllModelSlots(){
	std::unordered_map<std::string, std::unique_ptr<Stud::StudModel>> modelsToFree;
	{
		std::lock_guard<std::mutex> lock(Stud::modelsMutex);
		modelsToFree.swap(Stud::models);
	}
	{
		std::lock_guard<std::mutex> lock(Stud::sharedModelsMutex);
		Stud::sharedModels.clear();
	}
	modelsToFree.clear();
}
StudError LoadModel(const char* slotName, const char* filename, const char* jinjaTemplate, const int nGPULayers, const bool mMap, const bool mLock, const ggml_numa_strategy numaStrategy){
	const auto model = GetModel(slotName);
	if(model->llModel || model->sharedModel) FreeModel(slotName);
	model->sharedModel = LoadOrReuseSharedModel(filename, nGPULayers, mMap, mLock, numaStrategy);
	model->llModel = model->sharedModel ? model->sharedModel->llModel : nullptr;
	if(!model->llModel){ return _gpuOomStud ? StudError::GpuOutOfMemory : StudError::CantLoadModel; }
	model->session.vocab = llama_model_get_vocab(model->llModel);
	try{
		std::string tmplSrc;
		if(jinjaTemplate && jinjaTemplate[0] != '\0'){
			model->chatTemplates = common_chat_templates_init(model->llModel, jinjaTemplate);
			tmplSrc = jinjaTemplate;
		} else{
			model->chatTemplates = common_chat_templates_init(model->llModel, "");
			const char* modelTemplate = llama_model_chat_template(model->llModel, nullptr);
			if(modelTemplate) tmplSrc = modelTemplate;
		}
		model->caps = jinja::caps();
		if(!tmplSrc.empty()){
			jinja::lexer lex;
			const jinja::lexer_result lexed = lex.tokenize(tmplSrc);
			jinja::program prog = jinja::parse_from_tokens(lexed);
			model->caps = jinja::caps_get(prog);
		}
	} catch(const std::exception& e){
		_lastErrorMessage = e.what();
		FreeModel(slotName);
		return StudError::CantApplyTemplate;
	} catch(...){
		_lastErrorMessage = "The chat template could not be parsed.";
		FreeModel(slotName);
		return StudError::CantApplyTemplate;
	}
	return StudError::Success;
}
StudError LoadVisionProjector(const char* slotName, const char* filename, const bool useGPU, const int nThreads, const unsigned int flashAttn){
	const auto model = GetModel(slotName);
	if(!model->llModel){
		_lastErrorMessage = "Load the text model before loading its multimodal projector.";
		return StudError::ModelNotLoaded;
	}
	if(model->session.visionCtx){
		mtmd_free(model->session.visionCtx);
		model->session.visionCtx = nullptr;
	}
	if(!filename || filename[0] == '\0') return StudError::Success;
	mtmd_context_params params = mtmd_context_params_default();
	params.use_gpu = useGPU;
	params.print_timings = false;
	params.n_threads = std::max(1, nThreads);
	params.image_max_tokens = VisionImageMaxTokens;
	if(flashAttn == 0) params.flash_attn_type = LLAMA_FLASH_ATTN_TYPE_DISABLED;
	else if(flashAttn == 1) params.flash_attn_type = LLAMA_FLASH_ATTN_TYPE_ENABLED;
	else params.flash_attn_type = LLAMA_FLASH_ATTN_TYPE_AUTO;
	if(useGPU && params.flash_attn_type == LLAMA_FLASH_ATTN_TYPE_ENABLED)
		params.flash_attn_type = LLAMA_FLASH_ATTN_TYPE_AUTO;
	_gpuOomStud = false;
	_lastErrorMessage.clear();
	{
		ScopedBackendErrorCapture logCapture;
		try{ model->session.visionCtx = mtmd_init_from_file(filename, model->llModel, params); }
		catch(const std::exception& e){ _lastErrorMessage = e.what(); }
		catch(...){ _lastErrorMessage = "mtmd_init_from_file failed."; }
	}
	if(!model->session.visionCtx){
		if(_lastErrorMessage.empty()) _lastErrorMessage = "The multimodal projector could not be loaded.";
		return _gpuOomStud ? StudError::GpuOutOfMemory : StudError::CantLoadVisionProjector;
	}
	if(!mtmd_support_vision(model->session.visionCtx)){
		mtmd_free(model->session.visionCtx);
		model->session.visionCtx = nullptr;
		_lastErrorMessage = "The selected multimodal projector does not support image input.";
		return StudError::CantLoadVisionProjector;
	}
	FreeSpeculativeContext(model->session);
	model->session.mtpDraftTokens = 0;
	return StudError::Success;
}
bool HasVisionProjector(const char* slotName){
	const auto* model = FindModel(NormName(slotName));
	return model && model->session.visionCtx && mtmd_support_vision(model->session.visionCtx);
}
bool HasTool(const char* slotName, const char* name){
	const auto model = GetModel(slotName);
	for(const auto& tool : model->session.tools){ if(tool.name._Equal(name)) return true; }
	return false;
}
void SetTokenCallback(const Stud::TokenCallbackFn cb){ Stud::tokenCb = cb; }
void SetThreadCount(const int n, const int batchSize){
	std::lock_guard<std::mutex> lock(Stud::modelsMutex);
	for(auto&[slotName, model] : Stud::models){
		model->session.ctxParams.n_threads = n;
		model->session.ctxParams.n_threads_batch = batchSize;
		if(model->session.ctx) llama_set_n_threads(model->session.ctx, n, batchSize);
		if(model->session.mtpCtx) llama_set_n_threads(model->session.mtpCtx, n, batchSize);
	}
}
int LlamaMemSize(const char* slotName){
	const auto model = GetModel(slotName);
	return static_cast<int>(model->session.lane.cachedTokens.size());
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
	FreeSpeculativeContext(model->session);
	return StudError::Success;
}
static Stud::StudLane& ActiveLane(const char* slotName){
	const auto model = GetModel(slotName);
	return model->session.lane;
}
static void RestoreSamplerFromCachedTokens(Stud::StudSession& session){
	if(!session.lane.sampler) return;
	llama_sampler_reset(session.lane.sampler);
	for(const llama_token token : session.lane.cachedTokens)
		if(token != LLAMA_TOKEN_NULL) llama_sampler_accept(session.lane.sampler, token);
}
static common_chat_msg MirrorDialecticMessage(common_chat_msg msg){
	if(msg.role._Equal("assistant")){
		msg.role = "user";
		msg.reasoning_content.clear();
	} else if(msg.role._Equal("user")){ msg.role = "assistant"; }
	return msg;
}
static bool AlignDialecticRelayChatStates(const Stud::StudModel& source, Stud::StudModel& target){
	const auto& sourceMessages = source.session.lane.messages;
	const auto& sourceMedia = source.session.lane.messageMedia;
	auto& targetMessages = target.session.lane.messages;
	auto& targetMedia = target.session.lane.messageMedia;
	targetMedia.resize(targetMessages.size());
	auto sourceLimit = sourceMessages.size();
	if(sourceLimit > 0 && sourceMessages.back().role._Equal("assistant")) --sourceLimit;
	if(targetMessages.size() > sourceLimit) return false;
	if(targetMessages.size() == sourceLimit) return false;
	for(size_t i = targetMessages.size(); i < sourceLimit; ++i){
		targetMessages.push_back(MirrorDialecticMessage(sourceMessages[i]));
		targetMedia.push_back(i < sourceMedia.size() ? sourceMedia[i] : Stud::MessageMedia());
	}
	return true;
}
std::string RoleString(const Stud::MessageRole role){
	switch(role){
		case Stud::MessageRole::User: return std::string("user");
		case Stud::MessageRole::Assistant: return std::string("assistant");
		case Stud::MessageRole::Tool: return std::string("tool");
		default: return std::string();
	}
}
static StudError LoadChatSyntax(common_chat_parser_params& syntax, const common_chat_params& chatData, bool parseToolCalls);
static StudError BuildChatTemplateParamsForMessages(const char* slotName, common_chat_params& chatData, const std::vector<common_chat_msg>& chatMsgs, bool addGenerationPrompt, bool includeTools);
using PromptMessage = Stud::PromptMessage;
static bool LaneHasMedia(const Stud::StudLane& lane){
	return std::any_of(lane.messageMedia.begin(), lane.messageMedia.end(), [](const Stud::MessageMedia& media){ return !media.empty(); });
}
static void EnsureMessageMediaAligned(Stud::StudLane& lane){ lane.messageMedia.resize(lane.messages.size()); }
static StudError ParseContentJson(const Stud::MessageRole role, const char* think, const char* contentJson, PromptMessage& result){
	return Stud::ParseChatContentJson(RoleString(role), think, contentJson, result, _lastErrorMessage);
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
static int32_t EvaluateVisionChunksGuarded(mtmd_context* visionCtx, llama_context* ctx, const mtmd_input_chunks* chunks, const int32_t batchSize, llama_pos* newPast){
#if defined(_WIN32)
	__try{
		return mtmd_helper_eval_chunks(visionCtx, ctx, chunks, 0, 0, batchSize, true, newPast);
	} __except(GetExceptionCode() == EXCEPTION_ACCESS_VIOLATION ? EXCEPTION_EXECUTE_HANDLER : EXCEPTION_CONTINUE_SEARCH){
		return INT32_MIN;
	}
#else
	return mtmd_helper_eval_chunks(visionCtx, ctx, chunks, 0, 0, batchSize, true, newPast);
#endif
}
static StudError EvaluateVisionPrompt(const char* slotName, const std::vector<common_chat_msg>& messages, const std::vector<Stud::MessageMedia>& messageMedia, const bool addGenerationPrompt, const bool includeTools){
	const auto model = GetModel(slotName);
	auto& session = model->session;
	auto& lane = session.lane;
	if(!session.visionCtx){
		_lastErrorMessage = "This local model slot has no multimodal projector loaded.";
		return StudError::VisionProjectorNotLoaded;
	}
	common_chat_params chatData;
	auto err = BuildChatTemplateParamsForMessages(slotName, chatData, messages, addGenerationPrompt, includeTools);
	if(err != StudError::Success) return err;
	err = LoadChatSyntax(session.syntax, chatData, includeTools && !session.tools.empty());
	if(err != StudError::Success) return err;
	mtmd::bitmaps bitmaps;
	for(const auto& media : messageMedia){
		for(const auto& image : media){
			mtmd::bitmap bitmap(mtmd_helper_bitmap_init_from_buf(session.visionCtx, image.data(), image.size()));
			if(!bitmap.ptr){
				_lastErrorMessage = "The multimodal projector could not decode an image.";
				return StudError::CantDecodeImage;
			}
			const auto hash = MediaHash(bitmap.ptr.get());
			bitmap.set_id(hash.c_str());
			bitmaps.entries.push_back(std::move(bitmap));
		}
	}
	mtmd_input_text input{chatData.prompt.c_str(), true, true};
	mtmd::input_chunks chunks(mtmd_input_chunks_init());
	auto bitmapPointers = bitmaps.c_ptr();
	const int32_t tokenizeResult = mtmd_tokenize(session.visionCtx, chunks.ptr.get(), &input, bitmapPointers.data(), bitmapPointers.size());
	if(tokenizeResult != 0){
		_lastErrorMessage = tokenizeResult == 1 ? "The number of image markers did not match the supplied images." : "The multimodal projector could not preprocess an image.";
		return StudError::CantDecodeImage;
	}
	const llama_pos totalPositions = mtmd_helper_get_n_pos(chunks.ptr.get());
	if(totalPositions > static_cast<llama_pos>(llama_n_ctx(session.ctx))) return StudError::ConvTooLong;
	ClearSessMemAndSpecState(model);
	llama_pos newPast = 0;
	_gpuOomStud = false;
	_lastErrorMessage.clear();
	int32_t evalResult;
	{
		ScopedBackendErrorCapture logCapture;
		evalResult = EvaluateVisionChunksGuarded(session.visionCtx, session.ctx, chunks.ptr.get(), session.batchSize, &newPast);
	}
	if(evalResult != 0){
		ClearSessMemAndSpecState(model);
		lane.cachedTokens.clear();
		if(lane.sampler) llama_sampler_reset(lane.sampler);
		if(evalResult == INT32_MIN && _lastErrorMessage.empty())
			_lastErrorMessage = "The vision encoder failed after a GPU allocation error.";
		else if(_lastErrorMessage.empty())
			_lastErrorMessage = "The multimodal prompt could not be evaluated.";
		if(_gpuOomStud) return StudError::GpuOutOfMemory;
		return evalResult == 1 ? StudError::ConvTooLong : StudError::LlamaDecodeError;
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
		_lastErrorMessage = "The multimodal prompt position count was inconsistent.";
		ClearSessMemAndSpecState(model);
		lane.cachedTokens.clear();
		if(lane.sampler) llama_sampler_reset(lane.sampler);
		return StudError::LlamaDecodeError;
	}
	return StudError::Success;
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
	for(auto& msg : msgs) if(msg.content.empty() && msg.content_parts.empty()) msg.content = " ";
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
	EnsureMessageMediaAligned(lane);
	if(!model->session.ctx || !lane.sampler || !model->session.vocab) return StudError::ModelNotLoaded;
	const bool toolsEnabled = !model->session.tools.empty();
	const auto hasUser = std::any_of(lane.messages.begin(), lane.messages.end(), [](const common_chat_msg& msg){
		return msg.role == "user";
	});
	if(!hasUser){
		ClearSessMemAndSpecState(model);
		lane.cachedTokens.clear();
		if(lane.sampler) llama_sampler_reset(lane.sampler);
		return LoadChatSyntax(model->session.syntax, common_chat_params(), false);
	}
	if(LaneHasMedia(lane)) return EvaluateVisionPrompt(slotName, lane.messages, lane.messageMedia, false, toolsEnabled);
	common_chat_params chatData;
	const auto applyErr = BuildChatTemplateParamsForMessages(slotName, chatData, lane.messages, false, toolsEnabled);
	if(applyErr != StudError::Success) return applyErr;
	const auto syntaxErr = LoadChatSyntax(model->session.syntax, chatData, toolsEnabled);
	if(syntaxErr != StudError::Success) return syntaxErr;
	std::vector<llama_token> promptTokens;
	if(!TokenizePrompt(slotName, chatData.prompt, promptTokens)) return StudError::CantTokenizePrompt;
	size_t prefix = 0;
	if(!rebuildMemory) while(prefix < lane.cachedTokens.size() && prefix < promptTokens.size() && lane.cachedTokens[prefix] == promptTokens[prefix]){ ++prefix; }
	const size_t matchedPrefix = prefix;
	if(model->session.speculative && !rebuildMemory && prefix > 0 && !(prefix == lane.cachedTokens.size() && prefix == promptTokens.size())) --prefix;
	const bool canShift = !model->session.speculative && llama_memory_can_shift(model->session.memory);
	const size_t oldSz = lane.cachedTokens.size();
	const size_t newSz = promptTokens.size();
	size_t suffix = 0;
	if(!rebuildMemory && canShift && oldSz > 0 && newSz > 0){ while(suffix + prefix < oldSz && suffix + prefix < newSz && suffix < oldSz && suffix < newSz && lane.cachedTokens[oldSz - 1 - suffix] == promptTokens[newSz - 1 - suffix]){ ++suffix; } }
	const size_t oldSize = lane.cachedTokens.size();
	const size_t newSize = promptTokens.size();
	if(!rebuildMemory && prefix == oldSize && oldSize == newSize) return StudError::Success;
	if(newSize > static_cast<size_t>(llama_n_ctx(model->session.ctx))){
		if(rebuildMemory){
			ClearSessMemAndSpecState(model);
			lane.cachedTokens.clear();
			if(lane.sampler) llama_sampler_reset(lane.sampler);
		}
		return StudError::ConvTooLong;
	}
	if(rebuildMemory){
		prefix = 0;
		suffix = 0;
		ClearSessMemAndSpecState(model);
	} else if(!canShift){
		if(prefix == 0){
			prefix = 0;
			ClearSessMemAndSpecState(model);
		}
	}
	if(!canShift && newSize < oldSize && matchedPrefix < newSize){
		prefix = 0;
		ClearSessMemAndSpecState(model);
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
		if(!rebuildMemory && prefix < oldSize && !RemoveSessionMemoryAfter(model->session, static_cast<llama_pos>(prefix))){
			ClearSessMemAndSpecState(model);
			lane.cachedTokens.clear();
			if(lane.sampler) llama_sampler_reset(lane.sampler);
			return StudError::LlamaDecodeError;
		}
		if(model->session.speculative && prefix > 0 && prefix < oldSize) FreeSpeculativeContext(model->session);
	}
	llama_sampler_reset(lane.sampler);
	for(size_t i = 0; i < prefix; ++i){ llama_sampler_accept(lane.sampler, promptTokens[i]); }
	const size_t decodeEnd = newSize - suffix;
	const int evalBatchSize = EvalBatchSize(model->session);
	const int batchSize = std::min(evalBatchSize, static_cast<int>(decodeEnd - prefix));
	if(batchSize > 0){
		for(size_t i = prefix; i < decodeEnd; i += evalBatchSize){
			const int nTokens = std::min(evalBatchSize, static_cast<int>(decodeEnd - i));
			const auto decodeErr = DecodeTokenBatch(model->session, &promptTokens[i], nTokens, static_cast<llama_pos>(i), true);
			if(decodeErr != StudError::Success){
				ClearSessMemAndSpecState(model);
				lane.cachedTokens.clear();
				if(lane.sampler) llama_sampler_reset(lane.sampler);
				return decodeErr;
			}
			for(int j = 0; j < nTokens; ++j){ llama_sampler_accept(lane.sampler, promptTokens[i + j]); }
		}
	}
	for(size_t i = decodeEnd; i < newSize; ++i){ llama_sampler_accept(lane.sampler, promptTokens[i]); }
	lane.cachedTokens = std::move(promptTokens);
	return StudError::Success;
}
static size_t CommonTokenPrefix(const std::vector<llama_token>& a, const std::vector<llama_token>& b){
	size_t prefix = 0;
	while(prefix < a.size() && prefix < b.size() && a[prefix] == b[prefix]) ++prefix;
	return prefix;
}
static bool RestorePromptPrefix(Stud::StudModel* model, const std::vector<llama_token>& promptTokens, const size_t prefix){
	auto& session = model->session;
	auto& lane = session.lane;
	if(prefix > promptTokens.size()) return false;
	if(!RemoveSessionMemoryAfter(session, static_cast<llama_pos>(prefix))){
		ClearSessMemAndSpecState(model);
		lane.cachedTokens.clear();
		if(lane.sampler) llama_sampler_reset(lane.sampler);
		return false;
	}
	if(prefix == 0) RecreateSpecCtx(model);
	else FreeSpeculativeContext(session);
	lane.cachedTokens.assign(promptTokens.begin(), promptTokens.begin() + prefix);
	if(lane.sampler){
		llama_sampler_reset(lane.sampler);
		for(size_t i = 0; i < prefix; ++i) llama_sampler_accept(lane.sampler, promptTokens[i]);
	}
	return true;
}
static bool RestoreCachedTokenPrefix(Stud::StudModel* model, const size_t prefix){
	auto& session = model->session;
	auto& lane = session.lane;
	if(prefix > lane.cachedTokens.size()) return false;
	if(!RemoveSessionMemoryAfter(session, static_cast<llama_pos>(prefix))){
		ClearSessMemAndSpecState(model);
		lane.cachedTokens.clear();
		if(lane.sampler) llama_sampler_reset(lane.sampler);
		return false;
	}
	if(prefix == 0) RecreateSpecCtx(model);
	else FreeSpeculativeContext(session);
	if(lane.sampler){
		llama_sampler_reset(lane.sampler);
		for(size_t i = 0; i < prefix; ++i)
			if(lane.cachedTokens[i] != LLAMA_TOKEN_NULL) llama_sampler_accept(lane.sampler, lane.cachedTokens[i]);
	}
	lane.cachedTokens.resize(prefix);
	return true;
}
static StudError DecodePromptTokenSuffix(const char* slotName, const std::vector<llama_token>& promptTokens, const size_t prefix, const common_chat_msg* appendMsg = nullptr){
	const auto model = GetModel(slotName);
	auto& session = model->session;
	auto& lane = session.lane;
	if(prefix > lane.cachedTokens.size() || prefix > promptTokens.size()) return StudError::Generic;
	if(appendMsg){
		lane.messages.push_back(*appendMsg);
		lane.messageMedia.emplace_back();
	}
	if(promptTokens.size() > lane.cachedTokens.size()) lane.cachedTokens.reserve(promptTokens.size());
	size_t p = prefix;
	while(p < promptTokens.size() && !session.stop.load()){
		const int nEval = std::min<int>(EvalBatchSize(session), static_cast<int>(promptTokens.size() - p));
		const int nCtx = llama_n_ctx(session.ctx);
		const int nCtxUsed = static_cast<int>(lane.cachedTokens.size());
		if(nCtxUsed + nEval > nCtx){
			if(appendMsg){
				lane.messages.pop_back();
				lane.messageMedia.pop_back();
			}
			RestorePromptPrefix(model, promptTokens, prefix);
			return StudError::ConvTooLong;
		}
		const auto result = DecodeTokenBatch(session, const_cast<llama_token*>(&promptTokens[p]), nEval, static_cast<llama_pos>(p), true);
		if(result != StudError::Success){
			if(appendMsg){
				lane.messages.pop_back();
				lane.messageMedia.pop_back();
			}
			RestorePromptPrefix(model, promptTokens, prefix);
			return result;
		}
		for(int j = 0; j < nEval; ++j) llama_sampler_accept(lane.sampler, promptTokens[p + j]);
		lane.cachedTokens.insert(lane.cachedTokens.end(), promptTokens.begin() + p, promptTokens.begin() + p + nEval);
		p += nEval;
	}
	return StudError::Success;
}
static StudError DecodeSinglePromptMessage(const char* slotName, const PromptMessage& input, const bool addAss, const bool appendToChat = true){
	const auto model = GetModel(slotName);
	auto& lane = model->session.lane;
	const auto& message = input.message;
	const bool toolsEnabled = !model->session.tools.empty();
	EnsureMessageMediaAligned(lane);
	if(LaneHasMedia(lane) || !input.media.empty()){
		std::vector<common_chat_msg> chatMsgs = lane.messages;
		std::vector<Stud::MessageMedia> chatMedia = lane.messageMedia;
		chatMsgs.push_back(message);
		chatMedia.push_back(input.media);
		const auto err = EvaluateVisionPrompt(slotName, chatMsgs, chatMedia, addAss, toolsEnabled);
		if(err != StudError::Success) return err;
		if(appendToChat){
			lane.messages.push_back(message);
			lane.messageMedia.push_back(input.media);
		}
		return StudError::Success;
	}
	std::vector<common_chat_msg> chatMsgs = lane.messages;
	chatMsgs.push_back(message);
	common_chat_params chatData;
	auto err = BuildChatTemplateParamsForMessages(slotName, chatData, chatMsgs, addAss, toolsEnabled);
	if(err != StudError::Success) return err;
	err = LoadChatSyntax(model->session.syntax, chatData, toolsEnabled);
	if(err != StudError::Success) return err;
	std::vector<llama_token> promptTokens;
	if(!TokenizePrompt(slotName, chatData.prompt, promptTokens)) return StudError::CantTokenizePrompt;
	size_t prefix = CommonTokenPrefix(lane.cachedTokens, promptTokens);
	if(prefix < lane.cachedTokens.size()){
		if(model->session.speculative && prefix > 0) --prefix;
		if(!RestorePromptPrefix(model, promptTokens, prefix)) prefix = 0;
	}
	return DecodePromptTokenSuffix(slotName, promptTokens, prefix, appendToChat ? &message : nullptr);
}
StudError DialecticRelaySwap(const char* slotName, const char* fromSlotName, const char* toSlotName){
	(void)slotName;
	const auto* fromModel = FindModel(NormName(fromSlotName));
	auto* toModel = FindModel(NormName(toSlotName));
	if(!fromModel || !toModel || !fromModel->llModel || !fromModel->session.ctx || !toModel->llModel || !toModel->session.ctx) return StudError::ModelNotLoaded;
	if(AlignDialecticRelayChatStates(*fromModel, *toModel)){
		ActiveLane(toSlotName).cachedTokens.clear();
		const auto err = RetokenizeChat(toSlotName, true);
		if(err != StudError::Success) return err;
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
	if(!session.ctx || !lane.sampler || !session.vocab){
		return StudError::Success;
	}
	ClearSessMemAndSpecState(model);
	llama_sampler_reset(lane.sampler);
	return LoadChatSyntax(session.syntax, common_chat_params(), false);
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
	return RetokenizeChat(slotName);
}
StudError SetMessageAt(const char* slotName, const int index, const char* think, const char* message){
	auto& session = GetModel(slotName)->session;
	auto& lane = session.lane;
	if(index < 0 || index >= static_cast<int>(lane.messages.size())) return StudError::IndexOutOfRange;
	auto& messageToUpdate = lane.messages[index];
	messageToUpdate.reasoning_content = messageToUpdate.role._Equal("assistant") ? think : std::string();
	messageToUpdate.content = std::string(message);
	messageToUpdate.content_parts.clear();
	EnsureMessageMediaAligned(lane);
	lane.messageMedia[index].clear();
	if(!session.ctx || !lane.sampler || !session.vocab){
		lane.cachedTokens.clear();
		return StudError::Success;
	}
	return RetokenizeChat(slotName);
}
StudError SetMessageAtJson(const char* slotName, const int index, const char* think, const char* contentJson){
	auto& lane = ActiveLane(slotName);
	if(index < 0 || index >= static_cast<int>(lane.messages.size())) return StudError::IndexOutOfRange;
	PromptMessage replacement;
	Stud::MessageRole role = Stud::MessageRole::User;
	if(lane.messages[index].role._Equal("assistant")) role = Stud::MessageRole::Assistant;
	else if(lane.messages[index].role._Equal("tool")) role = Stud::MessageRole::Tool;
	const auto parseErr = ParseContentJson(role, think, contentJson, replacement);
	if(parseErr != StudError::Success) return parseErr;
	replacement.message.tool_calls = lane.messages[index].tool_calls;
	replacement.message.tool_name = lane.messages[index].tool_name;
	replacement.message.tool_call_id = lane.messages[index].tool_call_id;
	EnsureMessageMediaAligned(lane);
	lane.messages[index] = std::move(replacement.message);
	lane.messageMedia[index] = std::move(replacement.media);
	const auto& session = GetModel(slotName)->session;
	if(!session.ctx || !lane.sampler || !session.vocab){
		lane.cachedTokens.clear();
		return StudError::Success;
	}
	return RetokenizeChat(slotName);
}
StudError RemoveMessageAt(const char* slotName, const int index){
	auto& session = GetModel(slotName)->session;
	auto& lane = session.lane;
	if(index < 0 || index >= static_cast<int>(lane.messages.size())) return StudError::IndexOutOfRange;
	EnsureMessageMediaAligned(lane);
	lane.messages.erase(lane.messages.begin() + index);
	lane.messageMedia.erase(lane.messageMedia.begin() + index);
	if(!session.ctx || !lane.sampler || !session.vocab){
		lane.cachedTokens.clear();
		return StudError::Success;
	}
	return RetokenizeChat(slotName);
}
StudError RemoveMessagesStartingAt(const char* slotName, int index){
	auto& session = GetModel(slotName)->session;
	auto& lane = session.lane;
	if(index < 0) index = 0;
	if(index > static_cast<int>(lane.messages.size())) index = static_cast<int>(lane.messages.size());
	EnsureMessageMediaAligned(lane);
	lane.messages.erase(lane.messages.begin() + index, lane.messages.end());
	lane.messageMedia.erase(lane.messageMedia.begin() + index, lane.messageMedia.end());
	if(!session.ctx || !lane.sampler || !session.vocab){
		lane.cachedTokens.clear();
		return StudError::Success;
	}
	return RetokenizeChat(slotName);
}
StudError AddMessage(const char* slotName, const Stud::MessageRole role, const char* think, const char* message){
	common_chat_msg msg;
	msg.role = RoleString(role);
	msg.reasoning_content = std::string(think);
	msg.content = std::string(message);
	auto& lane = ActiveLane(slotName);
	lane.messages.push_back(std::move(msg));
	lane.messageMedia.emplace_back();
	return RetokenizeChat(slotName);
}
StudError AddMessageJson(const char* slotName, const Stud::MessageRole role, const char* think, const char* contentJson){
	PromptMessage input;
	const auto parseErr = ParseContentJson(role, think, contentJson, input);
	if(parseErr != StudError::Success) return parseErr;
	auto& lane = ActiveLane(slotName);
	lane.messages.push_back(std::move(input.message));
	lane.messageMedia.push_back(std::move(input.media));
	return RetokenizeChat(slotName);
}
static StudError ReplaceChatMessages(const char* slotName, std::vector<common_chat_msg>&& msgs, std::vector<Stud::MessageMedia>&& media = {}){
	for(auto& msg : msgs) EnsureToolCallIds(msg);
	auto& session = GetModel(slotName)->session;
	auto& lane = ActiveLane(slotName);
	lane.messages = std::move(msgs);
	lane.messageMedia = std::move(media);
	EnsureMessageMediaAligned(lane);
	if(!session.ctx || !lane.sampler || !session.vocab){
		lane.cachedTokens.clear();
		return StudError::Success;
	}
	return RetokenizeChat(slotName);
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
	std::vector<Stud::MessageMedia> media;
	const char* p = SkipJsonWhitespace(messagesJson);
	const auto parseError = Stud::ParseChatMessagesJson(p, msgs, media, _lastErrorMessage);
	if(parseError != StudError::Success) return parseError;
	return ReplaceChatMessages(slotName, std::move(msgs), std::move(media));
}
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
	try{ return common_chat_parse(normalizedResponse, isPartial, syntax); } catch(std::exception& e){
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
	AsyncTokenPostProcessor(const char* slotName, const Stud::TokenCallbackFn callbackFn, const bool streamCallback, const common_chat_parser_params& chatSyntax, const HrClock::time_point& prepStart, std::string& responseText, common_chat_msg& parsedMsg, double& firstTokenTime) : _slotName(slotName), _model(GetModel(slotName)), _callbackFn(callbackFn), _streamCallback(streamCallback), _chatSyntax(chatSyntax), _prepStart(prepStart), _responseText(responseText), _parsedMsg(parsedMsg), _firstTokenTime(firstTokenTime), _queue(kQueueCapacity){
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
		_callbackFn(_slotName, _parsedMsg.reasoning_content.c_str(), _parsedMsg.content.c_str(), _pendingCallbackTokens, memSize, _firstTokenTime, 0);
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
	const char* _slotName;
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
static StudError RollbackGenerate(const char* slotName, const size_t chatStart, const size_t newMessageCount, const size_t cacheStart, common_chat_msg& outMsg, const StudError error){
	const auto model = GetModel(slotName);
	model->session.lane.messages.resize(chatStart + newMessageCount);
	model->session.lane.messageMedia.resize(chatStart + newMessageCount);
	if(RestoreCachedTokenPrefix(model, cacheStart)){
		outMsg = common_chat_msg();
		return error;
	}
	const auto rtErr = RetokenizeChat(slotName, true);
	outMsg = common_chat_msg();
	return rtErr != StudError::Success ? rtErr : error;
}
static StudError DecodePromptMessages(const char* slotName, const std::vector<PromptMessage>& messages, common_chat_msg& outMsg){
	for(const auto& message : messages){
		const bool addAss = !message.message.role._Equal("assistant");
		const auto err = DecodeSinglePromptMessage(slotName, message, addAss);
		if(err != StudError::Success){
			outMsg = common_chat_msg();
			return err;
		}
	}
	return StudError::Success;
}
StudError Generate(const char* slotName, const std::vector<PromptMessage>& messages, const int nPredict, const bool callback, common_chat_msg& outMsg, const bool emitFinalCallback = true, std::string* toolParseError = nullptr){
	if(toolParseError) toolParseError->clear();
	const auto prepStart = HrClock::now();
	auto model = GetModel(slotName);
	auto& session = model->session;
	auto& lane = session.lane;
	session.stop.store(false);
	const Stud::TokenCallbackFn cb = Stud::tokenCb;
	const size_t chatStart = lane.messages.size();
	EnsureMessageMediaAligned(lane);
	const auto promptErr = DecodePromptMessages(slotName, messages, outMsg);
	if(promptErr != StudError::Success) return promptErr;
	const size_t cacheStart = lane.cachedTokens.size();
	const bool toolsEnabled = !session.tools.empty();
	common_chat_parser_params streamSyntax = session.syntax;
	if(callback){
		common_chat_params streamChatData;
		auto streamSyntaxErr = BuildChatTemplateParamsForMessages(slotName, streamChatData, lane.messages, true, false);
		if(streamSyntaxErr == StudError::Success) streamSyntaxErr = LoadChatSyntax(streamSyntax, streamChatData, false);
		if(streamSyntaxErr != StudError::Success) return RollbackGenerate(slotName, chatStart, messages.size(), cacheStart, outMsg, streamSyntaxErr);
	} else streamSyntax.parse_tool_calls = false;
	std::string response;
	common_chat_msg msg;
	double ftTime = 0.0;
	AsyncTokenPostProcessor postProcessor(slotName, cb, callback, streamSyntax, prepStart, response, msg, ftTime);
	auto failWith = [&](const StudError error){
		postProcessor.Close();
		return RollbackGenerate(slotName, chatStart, messages.size(), cacheStart, outMsg, error);
	};
	if(session.speculative) common_speculative_begin(session.speculative, 0, lane.cachedTokens);
	std::vector<llama_token> draft;
	std::vector<llama_token> decodeTokens;
	if(session.speculative){
		draft.reserve(session.mtpDraftTokens);
		decodeTokens.reserve(static_cast<size_t>(session.mtpDraftTokens) + 1);
	}
	int i = 0;
	int sampleIdx = -1;
	while((nPredict < 0 || i < nPredict) && !session.stop.load()){
		const StudError pendingError = postProcessor.Error();
		if(pendingError != StudError::Success) return failWith(pendingError);
		const int posStart = static_cast<int>(lane.cachedTokens.size());
		if(posStart + 1 > static_cast<int>(llama_n_ctx(session.ctx))) return failWith(StudError::ConvTooLong);
		const llama_token sampledToken = llama_sampler_sample(lane.sampler, session.ctx, sampleIdx);
		if(sampledToken == LLAMA_TOKEN_NULL) return failWith(StudError::LlamaDecodeError);
		const bool sampledEog = llama_vocab_is_eog(session.vocab, sampledToken);
		draft.clear();
		decodeTokens.clear();
		decodeTokens.push_back(sampledToken);
		int acceptedDrafts = 0;
		bool hasRejectedToken = false;
		llama_token rejectedToken = LLAMA_TOKEN_NULL;
		if(session.speculative && !sampledEog){
			const int ctxRoom = static_cast<int>(llama_n_ctx(session.ctx)) - posStart - 1;
			const int batchRoom = std::max(0, static_cast<int>(llama_n_batch(session.ctx)) - 1);
			const int predictRoom = nPredict < 0 ? session.mtpDraftTokens : std::max(0, nPredict - i - 1);
			const int draftMax = std::min({session.mtpDraftTokens, ctxRoom, batchRoom, predictRoom});
			if(draftMax == session.mtpDraftTokens && draftMax > 0){
				auto& draftParams = common_speculative_get_draft_params(session.speculative, 0);
				draftParams.drafting = true;
				draftParams.n_max = draftMax;
				draftParams.n_past = static_cast<llama_pos>(posStart);
				draftParams.id_last = sampledToken;
				draftParams.prompt = &lane.cachedTokens;
				draftParams.result = &draft;
				common_speculative_draft(session.speculative);
				if(!RemoveMtpMemoryAfter(session, posStart)) return failWith(StudError::LlamaDecodeError);
				const auto eogDraft = std::find_if(draft.begin(), draft.end(), [&](const llama_token token){ return llama_vocab_is_eog(session.vocab, token); });
				if(eogDraft != draft.end()) draft.erase(eogDraft + 1, draft.end());
				if(!draft.empty()){
					decodeTokens.insert(decodeTokens.end(), draft.begin(), draft.end());
				}
			}
		}
		const auto decodeErr = DecodeTokenBatch(session, decodeTokens.data(), static_cast<int>(decodeTokens.size()), posStart, true);
		if(decodeErr != StudError::Success) return failWith(decodeErr);
		if(!draft.empty()){
			for(size_t draftIdx = 0; draftIdx < draft.size(); ++draftIdx){
				const auto targetToken = llama_sampler_sample(lane.sampler, session.ctx, static_cast<int32_t>(draftIdx));
				if(targetToken == LLAMA_TOKEN_NULL) return failWith(StudError::LlamaDecodeError);
				if(targetToken != draft[draftIdx]){
					rejectedToken = targetToken;
					hasRejectedToken = true;
					break;
				}
				++acceptedDrafts;
				if(llama_vocab_is_eog(session.vocab, targetToken)) break;
			}
			common_speculative_accept(session.speculative, 0, static_cast<uint16_t>(acceptedDrafts));
			if(acceptedDrafts < static_cast<int>(draft.size()) && !RemoveSessionMemoryAfter(session, posStart + 1 + acceptedDrafts)) return failWith(StudError::LlamaDecodeError);
			if(hasRejectedToken){
				const auto rejectedDecodeErr = DecodeTokenBatch(session, &rejectedToken, 1, posStart + 1 + acceptedDrafts, true);
				if(rejectedDecodeErr != StudError::Success) return failWith(rejectedDecodeErr);
				sampleIdx = -1;
			} else sampleIdx = acceptedDrafts;
		} else sampleIdx = -1;
		const int acceptedCount = 1 + acceptedDrafts + (hasRejectedToken ? 1 : 0);
		for(int acceptedIdx = 0; acceptedIdx < acceptedCount && (nPredict < 0 || i < nPredict); ++acceptedIdx){
			const llama_token token = acceptedIdx == 0 ? sampledToken : acceptedIdx <= acceptedDrafts ? draft[acceptedIdx - 1] : rejectedToken;
			lane.cachedTokens.push_back(token);
			if(llama_vocab_is_eog(session.vocab, token)){
				session.stop.store(true);
				break;
			}
			const auto enqueueErr = postProcessor.Enqueue(token, static_cast<int>(lane.cachedTokens.size()));
			if(enqueueErr != StudError::Success) return failWith(enqueueErr);
			++i;
		}
	}
	postProcessor.Close();
	if(postProcessor.Error() != StudError::Success) return RollbackGenerate(slotName, chatStart, messages.size(), cacheStart, outMsg, postProcessor.Error());
	common_chat_params finalChatData;
	auto finalSyntaxErr = BuildChatTemplateParamsForMessages(slotName, finalChatData, lane.messages, true, toolsEnabled);
	if(finalSyntaxErr == StudError::Success) finalSyntaxErr = LoadChatSyntax(session.syntax, finalChatData, toolsEnabled);
	if(finalSyntaxErr != StudError::Success) return RollbackGenerate(slotName, chatStart, messages.size(), cacheStart, outMsg, finalSyntaxErr);
	try{
		msg = ParseGeneratedMessage(response, session.syntax, false, toolParseError);
		EnsureToolCallIds(msg);
	} catch(std::exception& e){
		_lastErrorMessage = e.what();
		return RollbackGenerate(slotName, chatStart, messages.size(), cacheStart, outMsg, StudError::ChatParseError);
	}
	lane.messages.push_back(msg);
	lane.messageMedia.emplace_back();
	if(emitFinalCallback && cb && !callback) cb(slotName, msg.reasoning_content.c_str(), msg.content.c_str(), i, static_cast<int>(lane.cachedTokens.size()), ftTime, 0);
	outMsg = std::move(msg);
	//OutputDebugStringA(("\n!!! CONTEXT START !!!\n" + std::string(GetContextAsText(slotName)) + "\n!!! CONTEXT END !!!\n").c_str());
	return StudError::Success;
}
static StudError GenerateWithToolsPrompt(const char* slotName, PromptMessage input, const int nPredict, const bool callback){
	auto model = GetModel(slotName);
	common_chat_msg msg;
	std::vector<PromptMessage> msgs{std::move(input)};
	if(model->session.tools.empty()){ return Generate(slotName, msgs, nPredict, callback, msg); }
	const Stud::TokenCallbackFn cb = Stud::tokenCb;
	bool toolCalled;
	const auto addToolResult = [&](const std::string& toolMsg){
		if(cb) cb(slotName, nullptr, toolMsg.c_str(), 0, LlamaMemSize(slotName), 0, 1);
		msgs.push_back(PromptMessage());
		msgs.back().message.role = "tool";
		msgs.back().message.content = toolMsg;
		toolCalled = true;
	};
	do{
		std::string toolParseError;
		const auto err = Generate(slotName, msgs, nPredict, callback, msg, true, &toolParseError);
		if(err != StudError::Success) return err;
		if(!model->session.lane.messages.size()) return StudError::Success;
		msgs.clear();
		try{
			toolCalled = false;
			if(!toolParseError.empty()){
				auto toolMsg = "{\"error\":\"Unable to parse tool call: " + JsonEscape(toolParseError) + "\"}";
				addToolResult(toolMsg);
			}
			for(common_chat_tool_call& toolCall : msg.tool_calls){
				if(model->session.stop.load()) return StudError::Success;
				auto toolCallMsg = "Tool name: " + toolCall.name + "\r\nTool ID: " + toolCall.id + "\r\nTool arguments: " + toolCall.arguments;
				if(cb) cb(slotName, nullptr, toolCallMsg.c_str(), 0, LlamaMemSize(slotName), 0, 3);
				auto it = model->session.toolHandlers.find(toolCall.name);
				if(it != model->session.toolHandlers.end()){
					std::string toolMsg;
					try{ toolMsg = it->second(slotName, toolCall.arguments.c_str()); }
					catch(const std::exception& e){ toolMsg = "{\"error\":\"Tool execution failed: " + JsonEscape(e.what()) + "\"}"; }
					catch(...){ toolMsg = "{\"error\":\"Tool execution failed\"}"; }
					addToolResult(toolMsg);
				} else{
					std::string toolMsg;
					if(TryExecuteMCPTool(toolCall, toolMsg)){
						addToolResult(toolMsg);
					} else if(TryExecuteManagedTool(slotName, toolCall, toolMsg)){
						addToolResult(toolMsg);
					}
				}
			}
		} catch(std::exception& e){
			_lastErrorMessage = e.what();
			return StudError::ChatParseError;
		}
	} while(toolCalled);
	return StudError::Success;
}
StudError GenerateWithTools(const char* slotName, const Stud::MessageRole role, const char* prompt, const int nPredict, const bool callback){
	PromptMessage input;
	input.message.role = RoleString(role);
	input.message.content = prompt ? std::string(prompt) : std::string();
	return GenerateWithToolsPrompt(slotName, std::move(input), nPredict, callback);
}
StudError GenerateWithToolsJson(const char* slotName, const Stud::MessageRole role, const char* contentJson, const int nPredict, const bool callback){
	PromptMessage input;
	const auto parseErr = ParseContentJson(role, "", contentJson, input);
	if(parseErr != StudError::Success) return parseErr;
	return GenerateWithToolsPrompt(slotName, std::move(input), nPredict, callback);
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
static StudError GenerateForAPIPrompt(const char* slotName, PromptMessage inputMsg, const char* toolsJson, const int nPredict, const bool callback, char** responseJson){
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
	std::vector<PromptMessage> messages{std::move(inputMsg)};
	common_chat_msg outputMsg;
	const auto generateErr = Generate(slotName, messages, nPredict, callback, outputMsg, false);
	if(generateErr != StudError::Success) return generateErr;
	*responseJson = CopyCString(BuildAPIResponseJson(outputMsg));
	if(!*responseJson){
		_lastErrorMessage = "Unable to allocate API generation response.";
		return StudError::Generic;
	}
	return StudError::Success;
}
StudError GenerateForAPI(const char* slotName, const Stud::MessageRole role, const char* prompt, const char* toolsJson, const int nPredict, const bool callback, char** responseJson){
	PromptMessage input;
	input.message.role = RoleString(role);
	input.message.content = prompt ? std::string(prompt) : std::string();
	return GenerateForAPIPrompt(slotName, std::move(input), toolsJson, nPredict, callback, responseJson);
}
StudError GenerateForAPIJson(const char* slotName, const Stud::MessageRole role, const char* contentJson, const char* toolsJson, const int nPredict, const bool callback, char** responseJson){
	PromptMessage input;
	const auto parseErr = ParseContentJson(role, "", contentJson, input);
	if(parseErr != StudError::Success) return parseErr;
	return GenerateForAPIPrompt(slotName, std::move(input), toolsJson, nPredict, callback, responseJson);
}
void StopGeneration(const char* slotName){
	GetModel(slotName)->session.stop.store(true);
	StopCMDOutput();
}
char* GetContextAsText(const char* slotName){
	const auto& session = GetModel(slotName)->session;
	if(!session.ctx) return nullptr;
	std::string outStr;
	outStr.reserve(session.lane.cachedTokens.size() * 4);
	for(const llama_token tok : session.lane.cachedTokens)
		if(tok != LLAMA_TOKEN_NULL) outStr += common_token_to_piece(session.ctx, tok, true);
	auto* out = static_cast<char*>(std::malloc(outStr.size() + 1));
	if(out) std::memcpy(out, outStr.c_str(), outStr.size() + 1);
	return out;
}
extern "C" EXPORT void* CaptureChatState(const char* slotName){
	const auto& session = GetModel(slotName)->session;
	auto* snapshot = new(std::nothrow) Stud::StudSession();
	if(!snapshot) return nullptr;
	snapshot->lane.messages = session.lane.messages;
	snapshot->lane.messageMedia = session.lane.messageMedia;
	snapshot->lane.cachedTokens = session.lane.cachedTokens;
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
	RestoreSamplerFromCachedTokens(session);
	FreeSpeculativeContext(session);
	session.systemPrompt = snapshot->systemPrompt;
	session.toolsPrompt = snapshot->toolsPrompt;
	session.tools = snapshot->tools;
	session.toolHandlers = snapshot->toolHandlers;
	session.syntax = snapshot->syntax;
	session.batchSize = snapshot->batchSize;
}
extern "C" EXPORT void FreeChatState(void* state){ delete static_cast<Stud::StudSession*>(state); }
