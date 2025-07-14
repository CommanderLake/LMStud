#pragma once
#define WIN32_LEAN_AND_MEAN
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
enum class MessageRole{
	User,
	Assistant,
	Tool
};
struct ChatSession{
	llama_context* ctx = nullptr;
	llama_sampler* smpl = nullptr;
	std::vector<common_chat_msg> chatMsgs;
	std::vector<llama_token> cachedTokens;
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
inline HWND _hWnd;
inline llama_model* _llModel = nullptr;
inline const llama_vocab* _vocab = nullptr;
inline ChatSession _session;
inline std::atomic_bool _stop{false};
inline common_chat_templates_ptr _chatTemplates;
using TokenCallbackFn = void(*)(const char* thinkPtr, int thinkLen, const char* messagePtr, int messageLen, int tokenCount, int tokensTotal, double ftTime, int tool);
inline TokenCallbackFn _tokenCb = nullptr;
inline std::vector<common_chat_tool> _tools;
inline std::unordered_map<std::string, std::string(*)(const char*)> _toolHandlers;
inline bool _hasTools;
extern "C" {
	EXPORT void SetHWnd(HWND hWnd);
	EXPORT void BackendInit();
	EXPORT int CreateContext(int nCtx, int nBatch, bool flashAttn, int nThreads, int nThreadsBatch);
	EXPORT int CreateSampler(float minP, float topP, int topK, float temp, float repeatPenalty);
	EXPORT int CreateSession(int nCtx, int nBatch, bool flashAttn, int nThreads, int nThreadsBatch, float minP, float topP, int topK, float temp, float repeatPenalty);
	EXPORT void DestroySession();
	EXPORT void ResetChat();
	EXPORT void FreeModel();
	EXPORT int LoadModel(const char* filename, int nGPULayers, bool mMap, bool mLock, ggml_numa_strategy numaStrategy);
	EXPORT bool HasTool(const char* name);
	EXPORT void SetTokenCallback(TokenCallbackFn cb);
	EXPORT void SetThreadCount(int n, int nBatch);
	EXPORT bool RetokenizeChat(bool rebuildMemory);
	EXPORT bool SetSystemPrompt(const char* prompt, const char* toolsPrompt);
	EXPORT bool SetMessageAt(int index, const char* think, const char* message);
	EXPORT bool RemoveMessageAt(int index);
	EXPORT bool RemoveMessagesStartingAt(int index);
	EXPORT void GenerateWithTools(MessageRole role, const char* prompt, int nPredict, bool callback);
	EXPORT void StopGeneration();
	char* GetContextAsText();
}
common_chat_msg Generate(std::vector<common_chat_msg> messages, int nPredict, bool callback);