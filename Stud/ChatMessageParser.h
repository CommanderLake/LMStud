#pragma once
#include "StudError.h"
#include <chat.h>
#include <string>
#include <vector>
namespace Stud{
	using MediaBytes = std::vector<unsigned char>;
	using MessageMedia = std::vector<MediaBytes>;
	struct PromptMessage{
		common_chat_msg message;
		MessageMedia media;
	};
	StudError ParseChatContentJson(const std::string& role, const char* reasoning, const char* contentJson, PromptMessage& result, std::string& error);
	StudError ParseChatMessagesJson(const char* messagesJson, std::vector<common_chat_msg>& messages, std::vector<MessageMedia>& media, std::string& error);
}
