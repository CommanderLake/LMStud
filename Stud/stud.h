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
inline llama_model* _llModel = nullptr;
inline const llama_vocab* _vocab = nullptr;
struct ChatSession{
	llama_context* ctx = nullptr;
	llama_sampler* smpl = nullptr;
	std::vector<common_chat_msg> chatMsgs;
	std::vector<llama_token> cachedTokens;
	std::string prompt;
	common_chat_format chatFormat = COMMON_CHAT_FORMAT_CONTENT_ONLY;
	bool useJinja = true;
	int nBatch = 1;
};
inline std::unordered_map<int, ChatSession> _sessions;
inline int _activeSession = -1;
inline int _nextSessionId = 1;
inline std::atomic_bool _stop{false};
inline common_chat_templates_ptr _chatTemplates;
using TokenCallbackFn = void(*)(const char* strPtr, int strLen, int tokenCount, int tokensTotal, double ftTime, int tool);
inline TokenCallbackFn _tokenCb = nullptr;
inline std::vector<common_chat_tool> _tools;
inline std::unordered_map<std::string, std::string(*)(const char*)> _toolHandlers;
inline bool _hasTools;
extern "C" {
EXPORT void BackendInit();
EXPORT void ResetChat();
EXPORT void FreeModel();
EXPORT int LoadModel(HWND hWnd, const char* filename, int nCtx, float temp, float repeatPenalty, int topK, int topP, int nThreads, int nThreadsBatch, int nGPULayers, int nBatch, bool mMap, bool mLock, ggml_numa_strategy numaStrategy, bool flashAttn);
EXPORT void AddTool(const char* name, const char* description, const char* parameters, std::string (*handler)(const char* args));
EXPORT void ClearTools();
EXPORT bool HasTool(const char* name);
EXPORT void SetTokenCallback(TokenCallbackFn cb);
EXPORT void SetThreadCount(int n, int nBatch);
EXPORT void AddMessage(std::string role, std::string message);
EXPORT void RetokenizeChat();
EXPORT void SetSystemPrompt(const char* prompt);
EXPORT void SetMessageAt(int index, const char* message);
EXPORT void RemoveMessageAt(int index);
EXPORT void RemoveMessagesStartingAt(int index);
EXPORT std::string Generate(HWND hWnd, std::string role, const std::string& prompt, unsigned int nPredict, bool callback);
EXPORT int GenerateWithTools(HWND hWnd, MessageRole role, char* prompt, unsigned int nGen, bool callback);
EXPORT void StopGeneration();
EXPORT int CreateSession(int nCtx, float temp, float repeatPenalty, int topK, int topP, int nThreads, int nThreadsBatch, int nBatch, bool flashAttn);
EXPORT void DestroySession(int id);
EXPORT bool SetActiveSession(int id);
EXPORT int GetActiveSession();
//EXPORT char* GetContextAsText();
}