#pragma once
#include "StudState.h"
#include <string_view>
namespace Stud::Internal{
	std::string& LastErrorMessage();
	bool& GpuOomDetected();
	void AppendLastErrorLogMessage(std::string_view message);
	void BackendLogCallback(ggml_log_level level, const char* text, void* userData);
	class ScopedBackendErrorCapture{
	public:
		ScopedBackendErrorCapture();
		~ScopedBackendErrorCapture();
		ScopedBackendErrorCapture(const ScopedBackendErrorCapture&) = delete;
		ScopedBackendErrorCapture& operator=(const ScopedBackendErrorCapture&) = delete;
	private:
		std::unique_lock<std::mutex> _logLock;
	};
	std::string NormalizeSlotName(const char* slotName);
	StudModel* FindModel(const std::string& slotName);
	void FreeSpeculativeContext(StudSession& session);
	void ClearSessionMemoryAndSpeculativeState(StudModel* model);
	bool RemoveSessionMemoryAfter(StudSession& session, llama_pos position);
	bool RemoveMtpMemoryAfter(StudSession& session, llama_pos position);
	int EvalBatchSize(const StudSession& session);
	StudError DecodeTokenBatch(StudSession& session, llama_token* tokens, int tokenCount, llama_pos startPosition, bool outputAll);
	void RecreateSpeculativeContext(StudModel* model);
	StudLane& ActiveLane(const char* slotName);
	std::string RoleString(MessageRole role);
	void EnsureMessageMediaAligned(StudLane& lane);
	void EnsureToolCallIds(common_chat_msg& message);
	StudError ParseContentJson(MessageRole role, const char* reasoning, const char* contentJson, PromptMessage& result);
	StudError BuildChatTemplateParams(const char* slotName, common_chat_params& chatData, const std::vector<common_chat_msg>& messages, bool addGenerationPrompt, bool includeTools);
	StudError LoadChatSyntax(common_chat_parser_params& syntax, const common_chat_params& chatData, bool parseToolCalls);
	bool RestoreCachedTokenPrefix(StudModel* model, size_t prefix);
	StudError DecodeSinglePromptMessage(const char* slotName, const PromptMessage& input, bool addAssistantPrompt, bool appendToChat = true);
}