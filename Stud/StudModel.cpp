#include "StudInternal.h"
#include <algorithm>
#include <charconv>
#include <cctype>
#include <filesystem>
#include <ggml-cpp.h>
#include <jinja\parser.h>
#include <system_error>
static constexpr int MtpDraftTokensMax = 62;
static constexpr int VisionImageMaxTokens = 1024;
static std::string NormalizeModelPathKey(const char* filename){
	if(!filename || filename[0] == '\0') return std::string();
	const std::filesystem::path path = std::filesystem::u8path(filename);
	std::error_code error;
	auto normalized = weakly_canonical(path, error);
	if(error){
		error.clear();
		normalized = absolute(path, error);
		if(error) normalized = path;
	}
	normalized = normalized.lexically_normal();
	auto key = normalized.u8string();
	std::transform(key.begin(), key.end(), key.begin(), [](unsigned char ch){ return static_cast<char>(std::tolower(ch)); });
	return key;
}
static std::string BuildSharedModelCacheKey(const char* filename, const int nGpuLayers, const bool memoryMap, const bool memoryLock, const ggml_numa_strategy numaStrategy){
	std::string key = NormalizeModelPathKey(filename);
	key += "|gpu=" + std::to_string(nGpuLayers);
	key += "|mmap=" + std::to_string(memoryMap ? 1 : 0);
	key += "|mlock=" + std::to_string(memoryLock ? 1 : 0);
	key += "|numa=" + std::to_string(numaStrategy);
	return key;
}
static std::shared_ptr<Stud::StudSharedModel> LoadOrReuseSharedModel(const char* filename, const int nGpuLayers, const bool memoryMap, const bool memoryLock, const ggml_numa_strategy numaStrategy){
	auto& errorMessage = Stud::Internal::LastErrorMessage();
	if(!filename || filename[0] == '\0'){
		errorMessage = "Model filename is empty.";
		return nullptr;
	}
	const auto key = BuildSharedModelCacheKey(filename, nGpuLayers, memoryMap, memoryLock, numaStrategy);
	std::lock_guard<std::mutex> cacheLock(Stud::sharedModelsMutex);
	const auto it = Stud::sharedModels.find(key);
	if(it != Stud::sharedModels.end()){
		if(auto shared = it->second.lock()) return shared;
		Stud::sharedModels.erase(it);
	}
	auto params = llama_model_default_params();
	params.n_gpu_layers = nGpuLayers;
	params.load_mode = memoryMap ? (memoryLock ? LLAMA_LOAD_MODE_MMAP_MLOCK : LLAMA_LOAD_MODE_MMAP) : (memoryLock ? LLAMA_LOAD_MODE_MLOCK : LLAMA_LOAD_MODE_NONE);
	params.load_mtp = true;
	llama_numa_init(numaStrategy);
	Stud::Internal::GpuOomDetected() = false;
	llama_model* llamaModel = nullptr;
	{
		std::lock_guard<std::mutex> logLock(Stud::llamaLogMutex);
		llama_log_set(Stud::Internal::BackendLogCallback, nullptr);
		try{ llamaModel = llama_model_load_from_file(filename, params); } catch(const std::exception& e){ errorMessage = e.what(); } catch(...){ errorMessage = "llama_model_load_from_file failed."; }
		llama_log_set(nullptr, nullptr);
	}
	if(!llamaModel) return nullptr;
	auto shared = std::make_shared<Stud::StudSharedModel>();
	shared->llModel = llamaModel;
	shared->cacheKey = key;
	Stud::sharedModels[key] = shared;
	return shared;
}
static void ClearSessionMemory(const Stud::StudSession& session){
	if(session.memory) llama_memory_clear(session.memory, true);
	if(session.mtpCtx){ if(const auto mtpMemory = llama_get_memory(session.mtpCtx)) llama_memory_clear(mtpMemory, true); }
}
static int EffectiveMtpDraftTokens(const int requestedDraftTokens, const llama_context_params& contextParams){
	const int contextRoom = contextParams.n_ctx == 0 ? requestedDraftTokens : std::max(0, static_cast<int>(contextParams.n_ctx) - 1);
	const int batchRoom = std::max(0, static_cast<int>(contextParams.n_batch) - 1);
	return std::min({requestedDraftTokens, contextRoom, batchRoom});
}
static void TryCreateMtpContext(Stud::StudModel* model, llama_context_params contextParams, const int draftTokens){
	if(draftTokens <= 0 || !model->session.ctx) return;
	if(model->session.visionCtx){
		Stud::Internal::AppendLastErrorLogMessage("MTP disabled: speculative decoding cannot mirror multimodal image embeddings.");
		return;
	}
	const auto sequenceRemovalType = common_context_can_seq_rm(model->session.ctx);
	if(sequenceRemovalType == COMMON_CONTEXT_SEQ_RM_TYPE_FULL || sequenceRemovalType == COMMON_CONTEXT_SEQ_RM_TYPE_NO){
		Stud::Internal::AppendLastErrorLogMessage("MTP disabled: this model context cannot roll back partially accepted draft tokens.");
		return;
	}
	contextParams.ctx_type = LLAMA_CONTEXT_TYPE_MTP;
	contextParams.ctx_other = model->session.ctx;
	contextParams.n_ctx = llama_n_ctx(model->session.ctx);
	contextParams.n_rs_seq = 0;
	contextParams.n_outputs_max = 1;
	contextParams.n_outputs_max_per_seq = 1;
	llama_context* mtpContext = nullptr;
	try{ mtpContext = llama_init_from_model(model->llModel, contextParams); } catch(const std::exception& e){ Stud::Internal::AppendLastErrorLogMessage(e.what()); } catch(...){ Stud::Internal::AppendLastErrorLogMessage("llama_init_from_model failed for the MTP context."); }
	if(!mtpContext){
		Stud::Internal::AppendLastErrorLogMessage("MTP disabled: the loaded model does not expose a compatible MTP head.");
		return;
	}
	const auto mtpSequenceRemovalType = common_context_can_seq_rm(mtpContext);
	if(mtpSequenceRemovalType == COMMON_CONTEXT_SEQ_RM_TYPE_FULL || mtpSequenceRemovalType == COMMON_CONTEXT_SEQ_RM_TYPE_NO){
		llama_free(mtpContext);
		Stud::Internal::AppendLastErrorLogMessage("MTP disabled: the MTP draft context cannot roll back draft state.");
		return;
	}
	common_params_speculative speculativeParams;
	speculativeParams.types = {COMMON_SPECULATIVE_TYPE_DRAFT_MTP};
	speculativeParams.draft.cache_type_k = contextParams.type_k;
	speculativeParams.draft.cache_type_v = contextParams.type_v;
	speculativeParams.draft.ctx_tgt = model->session.ctx;
	speculativeParams.draft.ctx_dft = mtpContext;
	speculativeParams.draft.n_max = draftTokens;
	speculativeParams.draft.n_min = 0;
	try{ model->session.speculative = common_speculative_init(speculativeParams, 1); } catch(const std::exception& e){ Stud::Internal::AppendLastErrorLogMessage(e.what()); } catch(...){ Stud::Internal::AppendLastErrorLogMessage("common_speculative_init failed for MTP."); }
	if(!model->session.speculative){
		llama_free(mtpContext);
		Stud::Internal::AppendLastErrorLogMessage("MTP disabled: llama.cpp did not create a speculative decoder.");
		return;
	}
	model->session.mtpCtx = mtpContext;
	model->session.mtpDraftTokens = draftTokens;
}
StudError Stud::Internal::CreateSampler(const llama_vocab* vocab, const StudSession::Sampling& options, llama_sampler*& sampler){
	if(!vocab) return StudError::ModelNotLoaded;
	if(sampler){
		llama_sampler_free(sampler);
		sampler = nullptr;
	}
	sampler = llama_sampler_chain_init(llama_sampler_chain_default_params());
	if(!sampler) return StudError::CantCreateSampler;
	llama_sampler_chain_add(sampler, llama_sampler_init_penalties(llama_vocab_n_tokens(vocab), 128, options.repeatPenalty, 0.0f, 0.0f));
	llama_sampler_chain_add(sampler, llama_sampler_init_temp(options.temperature));
	if(options.topK > 0) llama_sampler_chain_add(sampler, llama_sampler_init_top_k(options.topK));
	if(options.topP < 1.0f) llama_sampler_chain_add(sampler, llama_sampler_init_top_p(options.topP, 1));
	if(options.minP > 0.0f) llama_sampler_chain_add(sampler, llama_sampler_init_min_p(options.minP, 1));
	llama_sampler_chain_add(sampler, llama_sampler_init_dist(LLAMA_DEFAULT_SEED));
	return StudError::Success;
}
namespace Stud::Internal{
	void FreeSpeculativeContext(StudSession& session){
		if(session.speculative){
			common_speculative_free(session.speculative);
			session.speculative = nullptr;
			llama_set_embeddings_nextn(session.ctx, false, false);
		}
		if(session.mtpCtx){
			llama_free(session.mtpCtx);
			session.mtpCtx = nullptr;
		}
	}
	bool RemoveSessionMemoryAfter(const StudSession& session, const llama_pos position){
		bool success = true;
		if(session.memory) success = llama_memory_seq_rm(session.memory, 0, position, -1) && success;
		if(session.mtpCtx){ if(const auto mtpMemory = llama_get_memory(session.mtpCtx)) success = llama_memory_seq_rm(mtpMemory, 0, position, -1) && success; }
		return success;
	}
	bool RemoveMtpMemoryAfter(const StudSession& session, const llama_pos position){
		if(!session.mtpCtx) return true;
		const auto mtpMemory = llama_get_memory(session.mtpCtx);
		return !mtpMemory || llama_memory_seq_rm(mtpMemory, 0, position, -1);
	}
	StudError DecodeTokenBatch(StudSession& session, const llama_token* tokens, const int tokenCount, const llama_pos startPosition, const bool verifyDraft){
		if(tokenCount <= 0) return StudError::Success;
		auto& batch = session.targetBatch;
		if(!batch.batch) batch = common_batch(session.ctx);
		if(!batch.batch) return StudError::LlamaDecodeError;
		batch.clear();
		for(int i = 0; i < tokenCount; ++i) batch.add(tokens[i], startPosition + i, 0, verifyDraft || i == tokenCount - 1);
		const auto result = llama_process(session.ctx, LLAMA_PROCESS_TYPE_DECODE, batch.get());
		if(result == 0 && session.speculative && !verifyDraft && !common_speculative_process(session.speculative, batch)) return StudError::LlamaDecodeError;
		return result == 0 ? StudError::Success : result == 1 ? StudError::ConvTooLong : StudError::LlamaDecodeError;
	}
	void RecreateSpeculativeContext(StudModel* model){
		auto& session = model->session;
		const int draftTokens = session.mtpDraftTokens;
		if(draftTokens <= 0 || !session.ctx || !model->llModel || !session.hasCtxParams) return;
		const auto contextParams = session.ctxParams;
		FreeSpeculativeContext(session);
		TryCreateMtpContext(model, contextParams, draftTokens);
	}
	void ClearSessionMemoryAndSpeculativeState(StudModel* model){
		ClearSessionMemory(model->session);
		RecreateSpeculativeContext(model);
	}
}
bool IsModelSlotLoaded(const char* slotName){
	const auto* model = Stud::Internal::FindModel(Stud::Internal::NormalizeSlotName(slotName));
	return model && model->sharedModel && model->llModel && model->session.ctx;
}
StudError CreateContext(const char* slotName, const int nCtx, const int nBatch, const unsigned int flashAttn, const bool offloadKQV, const bool offloadOP, const int nThreads, const int nThreadsBatch, const int kType, const int vType, const int mtpDraftTokens){
	const auto model = GetModel(slotName);
	if(!model->llModel) return StudError::ModelNotLoaded;
	auto& errorMessage = Stud::Internal::LastErrorMessage();
	Stud::Internal::FreeSpeculativeContext(model->session);
	model->session.mtpDraftTokens = 0;
	model->session.targetBatch = common_batch();
	if(model->session.ctx){
		llama_free(model->session.ctx);
		model->session.ctx = nullptr;
		model->session.memory = nullptr;
	}
	model->session.syntax = {};
	auto contextParams = llama_context_default_params();
	contextParams.n_ctx = nCtx;
	contextParams.n_batch = nBatch;
	const int requestedDraftTokens = model->session.visionCtx || llama_model_n_layer_nextn(model->llModel) == 0 ? 0 : mtpDraftTokens;
	const int effectiveDraftTokens = EffectiveMtpDraftTokens(std::max(0, std::min(requestedDraftTokens, MtpDraftTokensMax)), contextParams);
	model->session.mtpDraftTokens = effectiveDraftTokens;
	contextParams.n_rs_seq = effectiveDraftTokens > 0 ? static_cast<uint32_t>(effectiveDraftTokens) : 0;
	contextParams.n_outputs_max = effectiveDraftTokens + 1;
	contextParams.n_outputs_max_per_seq = effectiveDraftTokens + 1;
	contextParams.offload_kqv = offloadKQV;
	contextParams.op_offload = offloadOP;
	if(flashAttn == 0) contextParams.flash_attn_type = LLAMA_FLASH_ATTN_TYPE_DISABLED;
	else if(flashAttn == 1) contextParams.flash_attn_type = LLAMA_FLASH_ATTN_TYPE_ENABLED;
	else if(flashAttn == 2) contextParams.flash_attn_type = LLAMA_FLASH_ATTN_TYPE_AUTO;
	contextParams.n_threads = nThreads;
	contextParams.n_threads_batch = nThreadsBatch;
	switch(kType){
		case 1: contextParams.type_k = GGML_TYPE_Q4_0;
			break;
		case 2: contextParams.type_k = GGML_TYPE_Q5_0;
			break;
		case 3: contextParams.type_k = GGML_TYPE_Q8_0;
			break;
		case 4: contextParams.type_k = GGML_TYPE_F16;
			break;
		default: ;
	}
	switch(vType){
		case 1: contextParams.type_v = GGML_TYPE_Q4_0;
			break;
		case 2: contextParams.type_v = GGML_TYPE_Q5_0;
			break;
		case 3: contextParams.type_v = GGML_TYPE_Q8_0;
			break;
		case 4: contextParams.type_v = GGML_TYPE_F16;
			break;
		default: ;
	}
	constexpr auto initFailedMessage = "llama_init_from_model failed.";
	{
		std::lock_guard<std::mutex> logLock(Stud::llamaLogMutex);
		Stud::Internal::GpuOomDetected() = false;
		errorMessage.clear();
		llama_log_set(Stud::Internal::BackendLogCallback, nullptr);
		try{ model->session.ctx = llama_init_from_model(model->llModel, contextParams); } catch(const std::exception& e){ errorMessage = e.what(); } catch(...){ errorMessage = initFailedMessage; }
		llama_log_set(nullptr, nullptr);
	}
	if(!model->session.ctx){
		if(errorMessage.empty()) errorMessage = initFailedMessage;
		return Stud::Internal::GpuOomDetected() ? StudError::GpuOutOfMemory : StudError::CantCreateContext;
	}
	model->session.memory = llama_get_memory(model->session.ctx);
	model->session.batchSize = std::min(std::max(1, nBatch), static_cast<int>(llama_n_batch(model->session.ctx)));
	model->session.ctxParams = contextParams;
	model->session.hasCtxParams = true;
	TryCreateMtpContext(model, contextParams, effectiveDraftTokens);
	errorMessage.clear();
	auto result = StudError::Success;
	if(model->session.lane.sampler) result = RetokenizeChat(slotName, true);
	return result;
}
StudError CreateSampler(const char* slotName, const float minP, const float topP, const int topK, const float temperature, const float repeatPenalty){
	const auto model = GetModel(slotName);
	model->session.sampling = {minP, topP, topK, temperature, repeatPenalty};
	return Stud::Internal::CreateSampler(model->session.vocab, model->session.sampling, model->session.lane.sampler);
}
void DestroySession(const char* slotName){
	const auto model = GetModel(slotName);
	Stud::Internal::FreeSpeculativeContext(model->session);
	model->session.targetBatch = common_batch();
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
static uint32_t GetGgufCount(const gguf_context* gguf, const char* key){
	const auto index = gguf_find_key(gguf, key);
	if(index < 0) return 0;
	switch(gguf_get_kv_type(gguf, index)){
		case GGUF_TYPE_UINT16: return gguf_get_val_u16(gguf, index);
		case GGUF_TYPE_UINT32: return gguf_get_val_u32(gguf, index);
		default: return 0;
	}
}
static bool HasTrunkBlocks(const gguf_context* gguf, uint32_t firstHeadBlock, uint32_t blockCount, bool& hasHead){
	for(int64_t i = 0; i < gguf_get_n_tensors(gguf); ++i){
		const std::string_view name = gguf_get_tensor_name(gguf, i);
		if(name.compare(0, 4, "blk.") != 0) continue;
		const auto end = name.find('.', 4);
		if(end == std::string_view::npos) continue;
		uint32_t block;
		const auto parsed = std::from_chars(name.data() + 4, name.data() + end, block);
		if(parsed.ec != std::errc{} || parsed.ptr != name.data() + end) continue;
		if(block < firstHeadBlock) return true;
		if(block < blockCount && name.compare(end, 7, ".nextn.") == 0) hasHead = true;
	}
	return false;
}
bool IsStandaloneMtpHead(const char* filename){
	if(!filename || !filename[0]) return false;
	try{
		const gguf_init_params params{true, nullptr};
		const gguf_context_ptr gguf(gguf_init_from_file(filename, params));
		if(!gguf) return false;
		const auto index = gguf_find_key(gguf.get(), "general.architecture");
		if(index < 0 || gguf_get_kv_type(gguf.get(), index) != GGUF_TYPE_STRING) return false;
		const std::string architecture = gguf_get_val_str(gguf.get(), index);
		const auto blocks = GetGgufCount(gguf.get(), (architecture + ".block_count").c_str());
		const auto headBlocks = GetGgufCount(gguf.get(), (architecture + ".nextn_predict_layers").c_str());
		if(headBlocks == 0 || headBlocks > blocks) return false;
		const auto firstHeadBlock = blocks - headBlocks;
		bool hasHead = false;
		if(HasTrunkBlocks(gguf.get(), firstHeadBlock, blocks, hasHead)) return false;
		const auto splitCount = GetGgufCount(gguf.get(), "split.count");
		if(splitCount > 1){
			const auto splitNo = GetGgufCount(gguf.get(), "split.no");
			std::vector<char> prefix(std::string_view(filename).size() + 1);
			if(splitNo >= splitCount || !llama_split_prefix(prefix.data(), prefix.size(), filename, splitNo, splitCount)) return false;
			std::vector<char> path(prefix.size());
			for(uint32_t i = 0; i < splitCount; ++i){
				if(i == splitNo) continue;
				if(!llama_split_path(path.data(), path.size(), prefix.data(), i, splitCount)) return false;
				const gguf_context_ptr part(gguf_init_from_file(path.data(), params));
				if(!part || HasTrunkBlocks(part.get(), firstHeadBlock, blocks, hasHead)) return false;
			}
		}
		return hasHead;
	} catch(...){ return false; }
}
StudError LoadModel(const char* slotName, const char* filename, const char* jinjaTemplate, const int nGPULayers, const bool mMap, const bool mLock, const ggml_numa_strategy numaStrategy){
	auto& errorMessage = Stud::Internal::LastErrorMessage();
	if(IsStandaloneMtpHead(filename)){
		errorMessage = "This GGUF contains only an MTP draft head. Select the full model GGUF.";
		return StudError::CantLoadModel;
	}
	const auto model = GetModel(slotName);
	if(model->llModel || model->sharedModel) FreeModel(slotName);
	model->sharedModel = LoadOrReuseSharedModel(filename, nGPULayers, mMap, mLock, numaStrategy);
	model->llModel = model->sharedModel ? model->sharedModel->llModel : nullptr;
	if(!model->llModel) return Stud::Internal::GpuOomDetected() ? StudError::GpuOutOfMemory : StudError::CantLoadModel;
	model->session.vocab = llama_model_get_vocab(model->llModel);
	try{
		std::string templateSource;
		if(jinjaTemplate && jinjaTemplate[0] != '\0'){
			model->chatTemplates = common_chat_templates_init(model->llModel, jinjaTemplate);
			templateSource = jinjaTemplate;
		} else{
			model->chatTemplates = common_chat_templates_init(model->llModel, "");
			const char* modelTemplate = llama_model_chat_template(model->llModel, nullptr);
			if(modelTemplate) templateSource = modelTemplate;
		}
		model->caps = jinja::caps();
		if(!templateSource.empty()){
			jinja::lexer lexer;
			const jinja::lexer_result lexed = lexer.tokenize(templateSource);
			jinja::program program = parse_from_tokens(lexed);
			model->caps = caps_get(program);
		}
	} catch(const std::exception& e){
		errorMessage = e.what();
		FreeModel(slotName);
		return StudError::CantApplyTemplate;
	} catch(...){
		errorMessage = "The chat template could not be parsed.";
		FreeModel(slotName);
		return StudError::CantApplyTemplate;
	}
	return StudError::Success;
}
StudError LoadVisionProjector(const char* slotName, const char* filename, const bool useGPU, const int nThreads, const unsigned int flashAttn){
	const auto model = GetModel(slotName);
	auto& errorMessage = Stud::Internal::LastErrorMessage();
	if(!model->llModel){
		errorMessage = "Load the text model before loading its multimodal projector.";
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
	if(useGPU && params.flash_attn_type == LLAMA_FLASH_ATTN_TYPE_ENABLED) params.flash_attn_type = LLAMA_FLASH_ATTN_TYPE_AUTO;
	Stud::Internal::GpuOomDetected() = false;
	errorMessage.clear();
	{
		Stud::Internal::ScopedBackendErrorCapture logCapture;
		try{ model->session.visionCtx = mtmd_init_from_file(filename, model->llModel, params); } catch(const std::exception& e){ errorMessage = e.what(); } catch(...){ errorMessage = "mtmd_init_from_file failed."; }
	}
	if(!model->session.visionCtx){
		if(errorMessage.empty()) errorMessage = "The multimodal projector could not be loaded.";
		return Stud::Internal::GpuOomDetected() ? StudError::GpuOutOfMemory : StudError::CantLoadVisionProjector;
	}
	if(!mtmd_support_vision(model->session.visionCtx)){
		mtmd_free(model->session.visionCtx);
		model->session.visionCtx = nullptr;
		errorMessage = "The selected multimodal projector does not support image input.";
		return StudError::CantLoadVisionProjector;
	}
	Stud::Internal::FreeSpeculativeContext(model->session);
	model->session.mtpDraftTokens = 0;
	return StudError::Success;
}
bool HasVisionProjector(const char* slotName){
	const auto* model = Stud::Internal::FindModel(Stud::Internal::NormalizeSlotName(slotName));
	return model && model->session.visionCtx && mtmd_support_vision(model->session.visionCtx);
}
void SetThreadCount(const int threadCount, const int batchThreadCount){
	std::lock_guard<std::mutex> lock(Stud::modelsMutex);
	for(auto& [slotName, model] : Stud::models){
		(void)slotName;
		model->session.ctxParams.n_threads = threadCount;
		model->session.ctxParams.n_threads_batch = batchThreadCount;
		if(model->session.ctx) llama_set_n_threads(model->session.ctx, threadCount, batchThreadCount);
		if(model->session.mtpCtx) llama_set_n_threads(model->session.mtpCtx, threadCount, batchThreadCount);
	}
}
int LlamaMemSize(const char* slotName){ return static_cast<int>(GetModel(slotName)->session.lane.cachedTokens.size()); }
int GetStateSize(const char* slotName){
	const auto model = GetModel(slotName);
	if(!model->session.ctx) return 0;
	return static_cast<int>(llama_state_get_size(model->session.ctx));
}
StudError GetStateData(const char* slotName, unsigned char* destination, const int size){
	const auto model = GetModel(slotName);
	auto& errorMessage = Stud::Internal::LastErrorMessage();
	if(!model->session.ctx || !destination || size <= 0){
		errorMessage = "Invalid Parameter";
		return StudError::Generic;
	}
	const auto expected = static_cast<size_t>(size);
	const auto copied = llama_state_get_data(model->session.ctx, destination, expected);
	if(copied != expected){
		errorMessage = "llama_state_get_data copied " + std::to_string(copied) + " bytes, expected " + std::to_string(expected);
		return StudError::Generic;
	}
	return StudError::Success;
}
StudError SetStateData(const char* slotName, const unsigned char* source, const int size){
	const auto model = GetModel(slotName);
	auto& errorMessage = Stud::Internal::LastErrorMessage();
	if(!model->session.ctx || !source || size <= 0){
		errorMessage = "Invalid Parameter";
		return StudError::Generic;
	}
	const auto expected = static_cast<size_t>(size);
	const auto read = llama_state_set_data(model->session.ctx, source, expected);
	if(read != expected){
		errorMessage = "llama_state_set_data read " + std::to_string(read) + " bytes, expected " + std::to_string(expected);
		return StudError::Generic;
	}
	Stud::Internal::FreeSpeculativeContext(model->session);
	return StudError::Success;
}
