#pragma once
#include "StudError.h"
#include <Windows.h>
#include <llama.h>
#include <string>
#define EXPORT __declspec(dllexport)
namespace Stud{
	enum class MessageRole{
		User,
		Assistant,
		Tool
	};
	using ToolHandlerFn = std::string(*)(const char*);
	using TokenCallbackFn = void(*)(const char* thinkPtr, int thinkLen, const char* messagePtr, int messageLen, int tokenCount, int tokensTotal, double ftTime, int tool);
}
extern "C" {
EXPORT void SetHWnd(HWND hWnd);
EXPORT void BackendInit();
EXPORT void AddTool(const char* name, const char* description, const char* parameters, Stud::ToolHandlerFn handler);
EXPORT void ClearTools();
EXPORT char* ExecuteTool(const char* name, const char* argsJson);
EXPORT const char* GetToolsJson(int* length);
EXPORT StudError CreateContext(int nCtx, int nBatch, unsigned int flashAttn, int nThreads, int nThreadsBatch, int kType, int vType);
EXPORT StudError CreateSampler(float minP, float topP, int topK, float temp, float repeatPenalty);
EXPORT StudError CreateSession(int nCtx, int nBatch, unsigned int flashAttn, int nThreads, int nThreadsBatch, float minP, float topP, int topK, float temp, float repeatPenalty, int kType, int vType);
EXPORT void DestroySession();
EXPORT StudError ResetChat();
EXPORT StudError ActivateModelSlot(const char* slotName);
EXPORT const char* GetActiveModelSlotName();
EXPORT bool IsModelSlotLoaded(const char* slotName);
EXPORT void FreeModelSlot(const char* slotName);
EXPORT void FreeModel();
EXPORT StudError LoadModel(const char* filename, const char* jinjaTemplate, int nGPULayers, bool mMap, bool mLock, ggml_numa_strategy numaStrategy);
EXPORT bool HasTool(const char* name);
EXPORT void SetTokenCallback(Stud::TokenCallbackFn cb);
EXPORT void SetThreadCount(int n, int batchSize);
EXPORT int LlamaMemSize();
EXPORT int GetStateSize();
EXPORT StudError GetStateData(unsigned char* dst, int size);
EXPORT StudError SetStateData(const unsigned char* src, int size);
EXPORT StudError DialecticInit();
EXPORT StudError DialecticRelayInit();
EXPORT StudError DialecticStart();
EXPORT StudError DialecticSwap();
EXPORT StudError DialecticRelaySwap(const char* fromSlotName, const char* toSlotName);
EXPORT void DialecticFree();
EXPORT StudError RetokenizeChat(bool rebuildMemory);
EXPORT StudError SetSystemPrompt(const char* prompt, const char* toolsPrompt);
EXPORT StudError SetMessageAt(int index, const char* think, const char* message);
EXPORT StudError RemoveMessageAt(int index);
EXPORT StudError RemoveMessagesStartingAt(int index);
EXPORT StudError AddMessage(Stud::MessageRole role, const char* message);
EXPORT StudError SyncChatMessages(const int* roles, const char** thinks, const char** messages, int count);
EXPORT StudError SyncChatMessagesJson(const char* messagesJson);
EXPORT StudError GenerateWithTools(Stud::MessageRole role, const char* prompt, int nPredict, bool callback);
EXPORT StudError GenerateForAPI(Stud::MessageRole role, const char* prompt, const char* toolsJson, int nPredict, char** responseJson);
EXPORT void StopGeneration();
EXPORT const char* GetLastErrorMessage();
EXPORT void ClearLastErrorMessage();
EXPORT void* CaptureChatState();
EXPORT void RestoreChatState(void* state);
EXPORT void FreeChatState(void* state);
}
char* GetContextAsText();
