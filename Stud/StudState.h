#pragma once
#include "stud.h"
#include <atomic>
#include <chat.h>
#include <jinja\caps.h>
#include <memory>
#include <mutex>
#include <unordered_map>
#include <vector>
namespace Stud{
	struct StudLane{
		llama_sampler* sampler = nullptr;
		std::vector<common_chat_msg> messages;
		std::vector<llama_token> cachedTokens;
		std::vector<unsigned char> state;
	};
	struct StudSession{
		llama_context* ctx = nullptr;
		const llama_vocab* vocab = nullptr;
		llama_memory_t memory = nullptr;
		StudLane lanes[2];
		int activeLane = 0;
		std::string systemPrompt;
		std::string toolsPrompt;
		std::vector<common_chat_tool> tools;
		std::unordered_map<std::string, ToolHandlerFn> toolHandlers;
		common_chat_parser_params syntax;
		std::atomic_bool stop{false};
		bool dialecticRelay = false;
		bool assNextGen = false;
		int batchSize = 1;
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
}
