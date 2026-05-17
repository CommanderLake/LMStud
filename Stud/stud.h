#pragma once
#include "StudError.h"
#include <Windows.h>
#include <llama.h>
#include <string>
#define EXPORT __declspec(dllexport)
namespace Stud{
	struct StudModel;
	enum class MessageRole{
		User,
		Assistant,
		Tool
	};
	using ToolHandlerFn = std::string(*)(const char*, const char*);
	using TokenCallbackFn = void(*)(const char* slotName, const char* think, const char* message, int tokenCount, int tokensTotal, double ftTime, int tool);
	using ManagedToolCallbackFn = char*(*)(const char* slotName, const char* name, const char* argsJson);
}
extern "C" {
EXPORT void SetHWnd(HWND hWnd);
EXPORT void BackendInit();
EXPORT void AddTool(const char* slotName, const char* name, const char* description, const char* parameters, Stud::ToolHandlerFn handler);
EXPORT void ClearTools(const char* slotName);
EXPORT char* ExecuteTool(const char* slotName, const char* name, const char* argsJson);
EXPORT const char* GetToolsJson(const char* slotName, int* length);
EXPORT StudError CreateContext(const char* slotName, int nCtx, int nBatch, unsigned int flashAttn, int nThreads, int nThreadsBatch, int kType, int vType);
EXPORT StudError CreateSampler(const char* slotName, float minP, float topP, int topK, float temp, float repeatPenalty);
EXPORT void DestroySession(const char* slotName);
EXPORT StudError ResetChat(const char* slotName);
EXPORT bool IsModelSlotLoaded(const char* slotName);
EXPORT void FreeModel(const char* slotName);
EXPORT void FreeModelSlot(const char* slotName);
EXPORT void FreeAllModelSlots();
EXPORT StudError LoadModel(const char* slotName, const char* filename, const char* jinjaTemplate, int nGPULayers, bool mMap, bool mLock, ggml_numa_strategy numaStrategy);
EXPORT bool HasTool(const char* slotName, const char* name);
EXPORT void SetTokenCallback(Stud::TokenCallbackFn cb);
EXPORT void SetManagedToolCallback(Stud::ManagedToolCallbackFn cb);
EXPORT void StreamManagedToolOutput(const char* slotName, const char* text);
EXPORT char* FormatJsonForDisplay(const char* json);
EXPORT char* FormatToolOutputForDisplay(const char* result);
EXPORT char* FormatToolCallForDisplay(const char* message);
EXPORT void SetThreadCount(int n, int batchSize);
EXPORT int LlamaMemSize(const char* slotName);
EXPORT int GetStateSize(const char* slotName);
EXPORT StudError GetStateData(const char* slotName, unsigned char* dst, int size);
EXPORT StudError SetStateData(const char* slotName, const unsigned char* src, int size);
EXPORT StudError DialecticRelaySwap(const char* slotName, const char* fromSlotName, const char* toSlotName);
EXPORT StudError RetokenizeChat(const char* slotName, bool rebuildMemory);
EXPORT StudError SetSystemPrompt(const char* slotName, const char* prompt, const char* toolsPrompt);
EXPORT StudError SetMessageAt(const char* slotName, int index, const char* think, const char* message);
EXPORT StudError RemoveMessageAt(const char* slotName, int index);
EXPORT StudError RemoveMessagesStartingAt(const char* slotName, int index);
EXPORT StudError AddMessage(const char* slotName, Stud::MessageRole role, const char* think, const char* message);
EXPORT StudError SyncChatMessages(const char* slotName, const int* roles, const char** thinks, const char** messages, int count);
EXPORT StudError SyncChatMessagesJson(const char* slotName, const char* messagesJson);
EXPORT StudError GenerateWithTools(const char* slotName, Stud::MessageRole role, const char* prompt, int nPredict, bool callback);
EXPORT StudError GenerateForAPI(const char* slotName, Stud::MessageRole role, const char* prompt, const char* toolsJson, int nPredict, bool callback, char** responseJson);
EXPORT void StopGeneration(const char* slotName);
EXPORT const char* GetLastErrorMessage();
EXPORT void ClearLastErrorMessage();
EXPORT void* CaptureChatState(const char* slotName);
EXPORT void RestoreChatState(const char* slotName, void* state);
EXPORT void FreeChatState(void* state);
}
char* GetContextAsText(const char* slotName);
Stud::StudModel* GetModel(const char* slotName);
