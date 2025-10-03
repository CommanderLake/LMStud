#pragma once
#include "StudError.h"
#include <Windows.h>
#include <llama.h>
#include <chat.h>
#include <mutex>
#include <vector>
#include <unordered_map>
#include <string>
#include <memory>
#include <array>
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
	llama_sampler* smpl[2] = {nullptr, nullptr};
	llama_memory_t llMem = nullptr;
	std::array<std::vector<common_chat_msg>, 2> chatMsgs;
	std::array<std::vector<llama_token>, 2> cachedTokens;
	std::array<std::vector<unsigned char>, 2> dialState;
	int dId = 0;
	std::string prompt;
	std::string toolsPrompt;
	common_chat_syntax syntax;
	bool useJinja = true;
	int nBatch = 1;
	int nCtx = 0;
	bool flashAttn = false;
	int nThreads = 0;
	int nThreadsBatch = 0;
	float minP = 0.0f;
	float topP = 1.0f;
	int topK = 0;
	float temp = 1.0f;
	float repeatPenalty = 1.0f;
};
struct ToolCtx{
	bool inThink = false;
	bool inCall = false;
	std::string buf;
};
inline HWND _hWnd;
inline std::atomic_bool _stop{false};
#define STUD_DEFAULT_MODEL "default"
#define STUD_DEFAULT_SESSION "default"

struct RuntimeState;

RuntimeState& GetRuntime();
ChatSession& ActiveSession();
const std::string& ActiveModelId();
using TokenCallbackFn = void(*)(const char* thinkPtr, int thinkLen, const char* messagePtr, int messageLen, int tokenCount, int tokensTotal, double ftTime, int tool);
inline TokenCallbackFn _tokenCb = nullptr;
inline std::vector<common_chat_tool> _tools;
inline std::unordered_map<std::string, std::string(*)(const char*)> _toolHandlers;
inline bool _hasTools;
extern "C" {
	EXPORT void SetHWnd(HWND hWnd);
	EXPORT void BackendInit();
	EXPORT void AddTool(const char* name, const char* description, const char* parameters, std::string(*handler)(const char* args));
	EXPORT void ClearTools();
	EXPORT StudError CreateContext(int nCtx, int nBatch, bool flashAttn, int nThreads, int nThreadsBatch);
	EXPORT StudError CreateSampler(float minP, float topP, int topK, float temp, float repeatPenalty);
	EXPORT StudError CreateSession(int nCtx, int nBatch, bool flashAttn, int nThreads, int nThreadsBatch, float minP, float topP, int topK, float temp, float repeatPenalty);
	EXPORT void DestroySession();
	EXPORT StudError ResetChat();
	EXPORT void FreeModel();
	EXPORT StudError LoadModel(const char* filename, const char* jinjaTemplate, int nGPULayers, bool mMap, bool mLock, ggml_numa_strategy numaStrategy);
	EXPORT StudError EnsureModel(const char* modelId, const char* filename, const char* jinjaTemplate, int nGPULayers, bool mMap, bool mLock, ggml_numa_strategy numaStrategy);
	EXPORT StudError ActivateModel(const char* modelId);
	EXPORT void DestroyModel(const char* modelId);
	EXPORT const char* ListModels();
	EXPORT StudError EnsureSessionId(const char* sessionId, int nCtx, int nBatch, bool flashAttn, int nThreads, int nThreadsBatch, float minP, float topP, int topK, float temp, float repeatPenalty, const char* modelId);
	EXPORT StudError ActivateSessionId(const char* sessionId, const char* modelId);
	EXPORT void DestroySessionId(const char* sessionId, const char* modelId);
	EXPORT const char* ListSessions(const char* modelId);
	EXPORT bool HasTool(const char* name);
	EXPORT void SetTokenCallback(TokenCallbackFn cb);
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
	EXPORT StudError AddMessage(MessageRole role, const char* message);
	EXPORT StudError GenerateWithTools(MessageRole role, const char* prompt, int nPredict, bool callback);
	EXPORT void StopGeneration();
	EXPORT const char* GetLastErrorMessage();
	EXPORT void ClearLastErrorMessage();
	char* GetContextAsText();
}