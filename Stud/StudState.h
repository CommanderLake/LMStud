#pragma once
#include "stud.h"
#include <atomic>
#include <chat.h>
#include <jinja\caps.h>
#include <memory>
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
		bool dialecticRelay = false;
		int batchSize = 1;
	};
	struct ModelRuntime{
		llama_model* llModel = nullptr;
		ChatSession session;
		common_chat_templates_ptr chatTemplates = nullptr;
		jinja::caps caps;
		std::string slotName = "main";
	};
	struct StudState{
		HWND hWnd = nullptr;
		ModelRuntime defaultRuntime;
		ModelRuntime* activeRuntime = nullptr;
		std::unordered_map<std::string, std::unique_ptr<ModelRuntime>> runtimes;
		std::atomic_bool stop{false};
		TokenCallbackFn tokenCb = nullptr;
		std::vector<common_chat_tool> tools;
		std::unordered_map<std::string, ToolHandlerFn> toolHandlers;
	};
	struct ChatStateSnapshot{
		ChatLaneSnapshot lanes[2];
		int activeLane = 0;
		std::string systemPrompt;
		std::string toolsPrompt;
		common_chat_parser_params syntax{};
		bool assistantNextGeneration = false;
		bool dialecticRelay = false;
		int batchSize = 1;
	};
	inline StudState state;
	inline ModelRuntime& runtime(){ return state.activeRuntime ? *state.activeRuntime : state.defaultRuntime; }
}
