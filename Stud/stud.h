#pragma once
#include <llama.h>
#include <chat.h>
#include <sampling.h>
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
inline common_params _params;
inline llama_model* _llModel = nullptr;
inline llama_context* _ctx = nullptr;
inline common_sampler* _smpl = nullptr;
inline const llama_vocab* _vocab = nullptr;
inline std::vector<llama_token> _tokens;
inline std::vector<common_chat_msg> _chatMsgs;
inline std::atomic_bool _stop{false};
inline common_chat_templates_ptr _chatTemplates;
using TokenCallbackFn = void(*)(const char* strPtr, int strLen, int tokenCount, int tokensTotal, double ftTime, int tool);
inline TokenCallbackFn _tokenCb = nullptr;
inline int _nConsumed = 0;
inline std::vector<common_chat_tool> _tools;
inline std::unordered_map<std::string, std::string(*)(const char*)> _toolHandlers;
inline common_chat_format _chatFormat = COMMON_CHAT_FORMAT_CONTENT_ONLY;
inline bool _hasTools;
extern "C" {
	EXPORT void BackendInit();
	EXPORT void ResetChat();
	EXPORT void FreeModel();
	EXPORT bool LoadModel(const char* filename, int nCtx, float temp, float repeatPenalty, int topK, int topP, int nThreads, bool strictCPU, int nThreadsBatch, bool strictCPUBatch, int nGPULayers, int nBatch, bool mMap, bool mLock, ggml_numa_strategy numaStrategy, bool flashAttn);
	EXPORT void AddTool(const char* name, const char* description, const char* parameters, std::string(*handler)(const char* args));
	EXPORT void ClearTools();
	EXPORT bool HasTool(const char* name);
	EXPORT void SetTokenCallback(TokenCallbackFn cb);
	EXPORT void SetThreadCount(int n, int nBatch);
	EXPORT int AddMessage(bool user, const char* message);
	EXPORT void RetokenizeChat();
	EXPORT void SetSystemPrompt(const char* prompt);
	EXPORT void SetMessageAt(int index, const char* message);
	EXPORT void RemoveMessageAt(int index);
	EXPORT void RemoveMessagesStartingAt(int index);
	EXPORT int Generate(unsigned int nPredict, bool callback);
	EXPORT int GenerateWithTools(unsigned int nPredict, bool callback);
	EXPORT void StopGeneration();
}