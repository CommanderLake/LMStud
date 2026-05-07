#pragma once
#include "stud.h"
#include <atomic>
#include <chat.h>
#include <jinja\caps.h>
#include <unordered_map>
#include <vector>
namespace Stud{
	struct ChatLane{
		llama_sampler* sampler = nullptr;
		std::vector<common_chat_msg> messages;
		std::vector<llama_token> cachedTokens;
		std::vector<unsigned char> state;
	};
	struct ChatLaneSnapshot{
		std::vector<common_chat_msg> messages;
		std::vector<llama_token> cachedTokens;
		std::vector<unsigned char> state;
	};
	struct ChatSession{
		llama_context* ctx = nullptr;
		const llama_vocab* vocab = nullptr;
		llama_memory_t memory = nullptr;
		ChatLane lanes[2];
		int activeLane = 0;
		std::string systemPrompt;
		std::string toolsPrompt;
		common_chat_parser_params syntax;
		bool assistantNextGeneration = false;
		int batchSize = 1;
	};
	struct StudState{
		HWND hWnd = nullptr;
		llama_model* llModel = nullptr;
		ChatSession session;
		std::atomic_bool stop{false};
		common_chat_templates_ptr chatTemplates = nullptr;
		TokenCallbackFn tokenCb = nullptr;
		std::vector<common_chat_tool> tools;
		std::unordered_map<std::string, ToolHandlerFn> toolHandlers;
		jinja::caps caps;
	};
	struct ChatStateSnapshot{
		ChatLaneSnapshot lanes[2];
		int activeLane = 0;
		std::string systemPrompt;
		std::string toolsPrompt;
		common_chat_parser_params syntax{};
		bool assistantNextGeneration = false;
		int batchSize = 1;
	};
	inline StudState state;
}
