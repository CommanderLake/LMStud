#pragma once
//#include "GGUFMeta.h"
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
static common_params params;
static llama_model* llModel = nullptr;
static llama_context* ctx = nullptr;
static common_sampler* smpl = nullptr;
static const llama_vocab* vocab = nullptr;
static std::mutex tokensMutex;
static std::vector<llama_token> embdInp;
static std::vector<common_chat_msg> chatMsgs;
static bool stop = false;
static common_chat_templates_ptr chatTemplates;
using TokenCallbackFn = void(*)(const char* token, int tokenCount);
static TokenCallbackFn tokenCb = nullptr;
//static std::vector<GGUFMetadataEntry> metadata;
static const char* DEFAULT_SYSTEM_MESSAGE = "You are a helpful AI assistant";
int gaI;
int gaN;
int gaW;
static int nPast = 0;
static int nConsumed = 0;
extern "C"{
	EXPORT void SetOMPEnv();
	EXPORT void ResetChat(bool msgs);
	EXPORT void FreeModel();
	EXPORT void LoadGGUFMetadata(const char* filename);
	EXPORT bool LoadModel(const char* filename, const char* system, int contextSize, float temp, float repeatPenalty, int topK, int topP, int nThreads, bool strictCPU, int nGPULayers, int nBatch, ggml_numa_strategy numaStrategy);
	EXPORT void SetTokenCallback(TokenCallbackFn cb);
	EXPORT void SetThreadCount(int n);
	EXPORT int AddMessage(bool user, const char* message);
	EXPORT void RetokenizeChat();
	EXPORT void RemoveMessageAt(int index);
	EXPORT void RemoveMessagesStartingAt(int index);
	EXPORT void Generate(unsigned int nPredict);
	EXPORT void StopGeneration();
}