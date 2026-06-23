#pragma once
#include "ChatMessageParser.h"
#include "stud.h"
#include <atomic>
#include <chat.h>
#include <jinja\caps.h>
#include <mtmd.h>
#include <speculative.h>
#include <memory>
#include <mutex>
#include <unordered_map>
#include <vector>
namespace Stud{
	struct StudLane{
		llama_sampler* sampler = nullptr;
		std::vector<common_chat_msg> messages;
		std::vector<MessageMedia> messageMedia;
		std::vector<llama_token> cachedTokens;
		bool endsWithEOG = true;
	};
	struct StudSession{
		llama_context* ctx = nullptr;
		mtmd_context* visionCtx = nullptr;
		llama_context* mtpCtx = nullptr;
		common_speculative* speculative = nullptr;
		llama_batch mtpTargetBatch{};
		int mtpTargetBatchCapacity = 0;
		const llama_vocab* vocab = nullptr;
		llama_memory_t memory = nullptr;
		StudLane lane;
		std::string systemPrompt;
		std::string toolsPrompt;
		std::vector<common_chat_tool> tools;
		std::unordered_map<std::string, ToolHandlerFn> toolHandlers;
		common_chat_parser_params syntax;
		std::atomic_bool stop{false};
		int batchSize = 1;
		int mtpDraftTokens = 0;
		llama_context_params ctxParams{};
		bool hasCtxParams = false;
		~StudSession(){
			if(speculative){
				common_speculative_free(speculative);
				speculative = nullptr;
			}
			if(lane.sampler){
				llama_sampler_free(lane.sampler);
				lane.sampler = nullptr;
			}
			if(mtpCtx){
				llama_free(mtpCtx);
				mtpCtx = nullptr;
			}
			if(mtpTargetBatchCapacity > 0){
				llama_batch_free(mtpTargetBatch);
				mtpTargetBatch = {};
				mtpTargetBatchCapacity = 0;
			}
			if(visionCtx){
				mtmd_free(visionCtx);
				visionCtx = nullptr;
			}
			if(ctx){
				llama_free(ctx);
				ctx = nullptr;
				memory = nullptr;
			}
		}
	};
	struct StudSharedModel{
		llama_model* llModel = nullptr;
		std::string cacheKey;
		~StudSharedModel(){
			if(llModel) llama_model_free(llModel);
		}
	};
	struct StudModel{
		llama_model* llModel = nullptr;
		std::shared_ptr<StudSharedModel> sharedModel = nullptr;
		StudSession session;
		common_chat_templates_ptr chatTemplates = nullptr;
		jinja::caps caps;
		std::string slotName = "main";
	};
	inline HWND hWnd = nullptr;
	inline std::unordered_map<std::string, std::unique_ptr<StudModel>> models;
	inline std::unordered_map<std::string, std::weak_ptr<StudSharedModel>> sharedModels;
	inline std::mutex modelsMutex;
	inline std::mutex sharedModelsMutex;
	inline std::mutex llamaLogMutex;
	inline TokenCallbackFn tokenCb = nullptr;
	inline RetokenizationCallbackFn retokenizationCb = nullptr;
	inline ManagedToolCallbackFn managedToolCb = nullptr;
}
