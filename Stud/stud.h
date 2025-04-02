#pragma once
#include <llama.h>
#include <chat.h>
#include <sampling.h>
#include <mutex>
#include <vector>
#ifdef _WIN32
#define EXPORT __declspec(dllexport)
#else
#define EXPORT
#endif
static common_params _params;
static llama_model* _llModel = nullptr;
static llama_context* _ctx = nullptr;
static common_sampler* _smpl = nullptr;
static const llama_vocab* _vocab = nullptr;
static std::vector<llama_token> _tokens;
static std::vector<common_chat_msg> _chatMsgs;
static bool _stop = false;
static common_chat_templates_ptr _chatTemplates;
using TokenCallbackFn = void(*)(const char* token, int strLen, int tokenCount);
static TokenCallbackFn _tokenCb = nullptr;
int _gaI;
int _gaN;
int _gaW;
static int _nPast = 0;
static int _nConsumed = 0;
extern "C"{
	EXPORT void SetOMPEnv();
	EXPORT void ResetChat(bool msgs);
	EXPORT void FreeModel();
	EXPORT bool LoadModel(const char* filename, const char* system, int contextSize, float temp, float repeatPenalty, int topK, int topP, int nThreads, bool strictCPU, int nGPULayers, int nBatch, ggml_numa_strategy numaStrategy);
	EXPORT void SetTokenCallback(TokenCallbackFn cb);
	EXPORT void SetThreadCount(int n);
	EXPORT int AddMessage(bool user, const char* message);
	EXPORT void RetokenizeChat();
	EXPORT void SetSystemPrompt(const char* prompt);
	EXPORT void SetMessageAt(int index, const char* message);
	EXPORT void RemoveMessageAt(int index);
	EXPORT void RemoveMessagesStartingAt(int index);
	EXPORT void Generate(unsigned int nPredict);
	EXPORT void StopGeneration();
}