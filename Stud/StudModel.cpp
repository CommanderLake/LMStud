#include "StudInternal.h"
#include <algorithm>
#include <cctype>
#include <filesystem>
#include <jinja\parser.h>
#include <system_error>
static constexpr int MtpDraftTokensMax = 62;
static constexpr int MtpPrefillBatchMax = 32;
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
	params.use_mlock = memoryLock;
	params.use_mmap = memoryMap;
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
static void ClearSessionMemory(Stud::StudSession& session){
	if(session.memory) llama_memory_clear(session.memory, true);
	if(session.mtpCtx){ if(const auto mtpMemory = llama_get_memory(session.mtpCtx)) llama_memory_clear(mtpMemory, true); }
}
static int EffectiveMtpDraftTokens(const int requestedDraftTokens, const llama_context_params& contextParams){
	const int contextRoom = contextParams.n_ctx == 0 ? requestedDraftTokens : std::max(0, static_cast<int>(contextParams.n_ctx) - 1);
	const int batchRoom = std::max(0, static_cast<int>(contextParams.n_batch) - 1);
	return std::min({requestedDraftTokens, contextRoom, batchRoom});
}
static llama_batch* EnsureMtpTargetBatch(Stud::StudSession& session, const int tokenCount){
	if(session.mtpTargetBatchCapacity >= tokenCount) return &session.mtpTargetBatch;
	if(session.mtpTargetBatchCapacity > 0){
		llama_batch_free(session.mtpTargetBatch);
		session.mtpTargetBatch = {};
		session.mtpTargetBatchCapacity = 0;
	}
	session.mtpTargetBatch = llama_batch_init(tokenCount, 0, 1);
	if(!session.mtpTargetBatch.token || !session.mtpTargetBatch.pos || !session.mtpTargetBatch.n_seq_id || !session.mtpTargetBatch.seq_id || !session.mtpTargetBatch.logits){
		llama_batch_free(session.mtpTargetBatch);
		session.mtpTargetBatch = {};
		return nullptr;
	}
	session.mtpTargetBatchCapacity = tokenCount;
	return &session.mtpTargetBatch;
}
static void AddSingleSequenceToken(llama_batch& batch, const llama_token token, const llama_pos position, const bool outputLogits){
	const int index = batch.n_tokens++;
	batch.token[index] = token;
	batch.pos[index] = position;
	batch.n_seq_id[index] = 1;
	batch.seq_id[index][0] = 0;
	batch.logits[index] = outputLogits;
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
	contextParams.n_rs_seq = static_cast<uint32_t>(draftTokens + 1);
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
static StudError CreateSamplerInternal(const float minP, const float topP, const int topK, const float temperature, const float repeatPenalty, llama_sampler*& sampler){
	if(sampler){
		llama_sampler_free(sampler);
		sampler = nullptr;
	}
	sampler = llama_sampler_chain_init(llama_sampler_chain_default_params());
	if(!sampler) return StudError::CantCreateSampler;
	llama_sampler_chain_add(sampler, llama_sampler_init_penalties(128, repeatPenalty, 0.0f, 0.0f));
	llama_sampler_chain_add(sampler, llama_sampler_init_temp(temperature));
	if(topK > 0) llama_sampler_chain_add(sampler, llama_sampler_init_top_k(topK));
	if(topP < 1.0f) llama_sampler_chain_add(sampler, llama_sampler_init_top_p(topP, 1));
	if(minP > 0.0f) llama_sampler_chain_add(sampler, llama_sampler_init_min_p(minP, 1));
	llama_sampler_chain_add(sampler, llama_sampler_init_dist(LLAMA_DEFAULT_SEED));
	return StudError::Success;
}
namespace Stud::Internal{
	void FreeSpeculativeContext(StudSession& session){
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
	bool RemoveSessionMemoryAfter(StudSession& session, const llama_pos position){
		bool success = true;
		if(session.memory) success = llama_memory_seq_rm(session.memory, 0, position, -1) && success;
		if(session.mtpCtx){ if(const auto mtpMemory = llama_get_memory(session.mtpCtx)) success = llama_memory_seq_rm(mtpMemory, 0, position, -1) && success; }
		return success;
	}
	bool RemoveMtpMemoryAfter(StudSession& session, const llama_pos position){
		if(!session.mtpCtx) return true;
		const auto mtpMemory = llama_get_memory(session.mtpCtx);
		return !mtpMemory || llama_memory_seq_rm(mtpMemory, 0, position, -1);
	}
	int EvalBatchSize(const StudSession& session){ return session.speculative ? std::max(1, std::min(session.batchSize, MtpPrefillBatchMax)) : session.batchSize; }
	StudError DecodeTokenBatch(StudSession& session, llama_token* tokens, const int tokenCount, const llama_pos startPosition, const bool outputAll){
		if(tokenCount <= 0) return StudError::Success;
		if(!session.speculative){
			const auto batch = llama_batch_get_one(tokens, tokenCount);
			const auto result = llama_decode(session.ctx, batch);
			return result == 0 ? StudError::Success : result == 1 ? StudError::ConvTooLong : StudError::LlamaDecodeError;
		}
		auto* batchPointer = EnsureMtpTargetBatch(session, tokenCount);
		if(!batchPointer) return StudError::LlamaDecodeError;
		auto& batch = *batchPointer;
		common_batch_clear(batch);
		for(int i = 0; i < tokenCount; ++i) AddSingleSequenceToken(batch, tokens[i], startPosition + i, outputAll || i == tokenCount - 1);
		const auto result = llama_decode(session.ctx, batch);
		if(result == 0 && !common_speculative_process(session.speculative, batch)) return StudError::LlamaDecodeError;
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
StudError CreateContext(const char* slotName, const int contextSize, const int batchSize, const unsigned int flashAttention, const int threadCount, const int batchThreadCount, const int keyType, const int valueType, const int mtpDraftTokens){
	const auto model = GetModel(slotName);
	auto& errorMessage = Stud::Internal::LastErrorMessage();
	Stud::Internal::FreeSpeculativeContext(model->session);
	model->session.mtpDraftTokens = 0;
	if(model->session.ctx){
		llama_free(model->session.ctx);
		model->session.ctx = nullptr;
		model->session.memory = nullptr;
	}
	model->session.syntax.reasoning_format = COMMON_REASONING_FORMAT_AUTO;
	model->session.batchSize = batchSize;
	auto contextParams = llama_context_default_params();
	contextParams.n_ctx = contextSize;
	contextParams.n_batch = batchSize;
	const int requestedDraftTokens = model->session.visionCtx ? 0 : mtpDraftTokens;
	const int effectiveDraftTokens = EffectiveMtpDraftTokens(std::max(0, std::min(requestedDraftTokens, MtpDraftTokensMax)), contextParams);
	model->session.mtpDraftTokens = effectiveDraftTokens;
	contextParams.n_rs_seq = effectiveDraftTokens > 0 ? static_cast<uint32_t>(effectiveDraftTokens) : 0;
	contextParams.offload_kqv = true;
	contextParams.op_offload = true;
	if(flashAttention == 0) contextParams.flash_attn_type = LLAMA_FLASH_ATTN_TYPE_DISABLED;
	else if(flashAttention == 1) contextParams.flash_attn_type = LLAMA_FLASH_ATTN_TYPE_ENABLED;
	else if(flashAttention == 2) contextParams.flash_attn_type = LLAMA_FLASH_ATTN_TYPE_AUTO;
	contextParams.n_threads = threadCount;
	contextParams.n_threads_batch = batchThreadCount;
	switch(keyType){
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
	switch(valueType){
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
	return CreateSamplerInternal(minP, topP, topK, temperature, repeatPenalty, model->session.lane.sampler);
}
void DestroySession(const char* slotName){
	const auto model = GetModel(slotName);
	Stud::Internal::FreeSpeculativeContext(model->session);
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
StudError LoadModel(const char* slotName, const char* filename, const char* jinjaTemplate, const int nGpuLayers, const bool memoryMap, const bool memoryLock, const ggml_numa_strategy numaStrategy){
	const auto model = GetModel(slotName);
	auto& errorMessage = Stud::Internal::LastErrorMessage();
	if(model->llModel || model->sharedModel) FreeModel(slotName);
	model->sharedModel = LoadOrReuseSharedModel(filename, nGpuLayers, memoryMap, memoryLock, numaStrategy);
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
StudError LoadVisionProjector(const char* slotName, const char* filename, const bool useGpu, const int threadCount, const unsigned int flashAttention){
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
	params.use_gpu = useGpu;
	params.print_timings = false;
	params.n_threads = std::max(1, threadCount);
	params.image_max_tokens = VisionImageMaxTokens;
	if(flashAttention == 0) params.flash_attn_type = LLAMA_FLASH_ATTN_TYPE_DISABLED;
	else if(flashAttention == 1) params.flash_attn_type = LLAMA_FLASH_ATTN_TYPE_ENABLED;
	else params.flash_attn_type = LLAMA_FLASH_ATTN_TYPE_AUTO;
	if(useGpu && params.flash_attn_type == LLAMA_FLASH_ATTN_TYPE_ENABLED) params.flash_attn_type = LLAMA_FLASH_ATTN_TYPE_AUTO;
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