#pragma once
#include "StudError.h"
#include <Windows.h>
#include <llama.h>
#include <chat.h>
#include <mutex>
#include <vector>
#include <unordered_map>
#include <string>
#include <atomic>
#pragma comment(lib, "llama.lib")
#pragma comment(lib, "common.lib")
#pragma comment(lib, "ggml.lib")
#pragma comment(lib, "ggml-base.lib")
#define EXPORT __declspec(dllexport)
namespace Stud{
	enum class MessageRole{
		User,
		Assistant,
		Tool
	};
	struct ChatSession{
		llama_context* ctx = nullptr;
		const llama_vocab* _vocab = nullptr;
		llama_sampler* smpl[2] = {nullptr, nullptr};
		llama_memory_t llMem = nullptr;
		std::vector<common_chat_msg> chatMsgs[2];
		std::vector<llama_token> cachedTokens[2];
		std::vector<unsigned char> dialState[2];
		int dId = 0;
		std::string prompt;
		std::string toolsPrompt;
		common_chat_syntax syntax;
		bool useJinja = true;
		int nBatch = 1;
	};
	struct ToolCtx{
		bool inThink = false;
		bool inCall = false;
		std::string buf;
	};
	using ToolHandlerFn = std::string(*)(const char*);
	using TokenCallbackFn = void(*)(const char* thinkPtr, int thinkLen, const char* messagePtr, int messageLen, int tokenCount, int tokensTotal, double ftTime, int tool);
	namespace Backend{
		struct BackendState{
			HWND hWnd = nullptr;
			llama_model* model = nullptr;
			ChatSession session;
			std::atomic_bool stop{false};
			common_chat_templates_ptr chatTemplates = nullptr;
			TokenCallbackFn tokenCallback = nullptr;
			std::vector<common_chat_tool> tools;
			std::unordered_map<std::string, ToolHandlerFn> toolHandlers;
			bool hasTools = false;
		};
		BackendState& state();
	}
}
extern "C" {
EXPORT void SetHWnd(HWND hWnd);
EXPORT void BackendInit();
EXPORT void AddTool(const char* name, const char* description, const char* parameters, Stud::ToolHandlerFn handler);
EXPORT void ClearTools();
EXPORT StudError CreateContext(int nCtx, int nBatch, bool flashAttn, int nThreads, int nThreadsBatch);
EXPORT StudError CreateSampler(float minP, float topP, int topK, float temp, float repeatPenalty);
EXPORT StudError CreateSession(int nCtx, int nBatch, bool flashAttn, int nThreads, int nThreadsBatch, float minP, float topP, int topK, float temp, float repeatPenalty);
EXPORT void DestroySession();
EXPORT StudError ResetChat();
EXPORT void FreeModel();
EXPORT StudError LoadModel(const char* filename, const char* jinjaTemplate, int nGPULayers, bool mMap, bool mLock, ggml_numa_strategy numaStrategy);
EXPORT bool HasTool(const char* name);
EXPORT void SetTokenCallback(Stud::TokenCallbackFn cb);
EXPORT void SetThreadCount(int n, int nBatch);
EXPORT int LlamaMemSize();
EXPORT int GetStateSize();
EXPORT void GetStateData(unsigned char* dst, int size);
EXPORT void SetStateData(const unsigned char* src, int size);
EXPORT void DialecticInit();
EXPORT void DialecticStart();
EXPORT StudError DialecticSwap();
EXPORT void DialecticFree();
EXPORT StudError RetokenizeChat(bool rebuildMemory);
EXPORT StudError SetSystemPrompt(const char* prompt, const char* toolsPrompt);
EXPORT StudError SetMessageAt(int index, const char* think, const char* message);
EXPORT StudError RemoveMessageAt(int index);
EXPORT StudError RemoveMessagesStartingAt(int index);
EXPORT StudError AddMessage(Stud::MessageRole role, const char* message);
EXPORT StudError GenerateWithTools(Stud::MessageRole role, const char* prompt, int nPredict, bool callback);
EXPORT void StopGeneration();
EXPORT const char* GetLastErrorMessage();
EXPORT void ClearLastErrorMessage();
EXPORT void* CaptureChatState();
EXPORT void RestoreChatState(void* state);
EXPORT void FreeChatState(void* state);
}
char* GetContextAsText();