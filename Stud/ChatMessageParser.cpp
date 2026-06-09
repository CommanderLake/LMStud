#include "ChatMessageParser.h"
#include <base64.hpp>
#include <mtmd.h>
#include <nlohmann\json.hpp>
#include <stdexcept>
#include <utility>
namespace{
	constexpr size_t MaximumImageBytes = 20 * 1024 * 1024;
	constexpr size_t MaximumEncodedImageBytes = ((MaximumImageBytes + 2) / 3) * 4;
	StudError DecodeImageDataUrl(const std::string& url, Stud::MediaBytes& bytes, std::string& error){
		const auto comma = url.find(',');
		if(comma == std::string::npos || url.compare(0, 11, "data:image/") != 0){
			error = "Local vision input must be an image data URL.";
			return StudError::CantDecodeImage;
		}
		const auto header = url.substr(0, comma);
		if(header.size() < 7 || header.rfind(";base64") != header.size() - 7){
			error = "Image data URLs must use base64 encoding.";
			return StudError::CantDecodeImage;
		}
		const auto encodedSize = url.size() - comma - 1;
		if(encodedSize == 0 || encodedSize > MaximumEncodedImageBytes){
			error = "The image is empty or exceeds the 20 MB input limit.";
			return StudError::CantDecodeImage;
		}
		try{
			const std::string decoded = base64::decode(url.data() + comma + 1, encodedSize);
			if(decoded.empty() || decoded.size() > MaximumImageBytes){
				error = "The image is empty or exceeds the 20 MB input limit.";
				return StudError::CantDecodeImage;
			}
			bytes.assign(decoded.begin(), decoded.end());
			return StudError::Success;
		} catch(const std::exception& e){
			error = e.what();
			return StudError::CantDecodeImage;
		}
	}
	StudError NormalizeMessageJson(nlohmann::ordered_json messageJson, common_chat_msg& message, Stud::MessageMedia& media, std::string& error){
		try{
			if(!messageJson.is_object()) throw std::invalid_argument("Expected a message object.");
			if(!messageJson.contains("content")) messageJson["content"] = "";
			auto& content = messageJson["content"];
			if(content.is_array()){
				nlohmann::ordered_json normalized = nlohmann::ordered_json::array();
				for(const auto& part : content){
					if(!part.is_object()) throw std::invalid_argument("Expected an object in message content.");
					const std::string type = part.value("type", "");
					if(type == "text" || type == "input_text"){
						normalized.push_back({{"type", "text"}, {"text", part.value("text", "")}});
						continue;
					}
					if(type == "image_url" || type == "input_image"){
						std::string url;
						if(part.contains("image_url")){
							const auto& imageUrl = part["image_url"];
							if(imageUrl.is_string()) url = imageUrl.get<std::string>();
							else if(imageUrl.is_object()) url = imageUrl.value("url", "");
						}
						Stud::MediaBytes image;
						const auto decodeError = DecodeImageDataUrl(url, image, error);
						if(decodeError != StudError::Success) return decodeError;
						media.push_back(std::move(image));
						normalized.push_back({{"type", "media_marker"}, {"text", mtmd_default_marker()}});
						continue;
					}
					if(type == "media_marker") throw std::invalid_argument("A media marker was provided without image data.");
					throw std::invalid_argument("Unsupported message content type: " + type);
				}
				content = std::move(normalized);
			}
			auto parsed = common_chat_msgs_parse_oaicompat(nlohmann::ordered_json::array({messageJson}));
			if(parsed.size() != 1) throw std::runtime_error("Unable to parse the chat message.");
			message = std::move(parsed.front());
			return StudError::Success;
		} catch(const std::exception& e){
			error = e.what();
			return StudError::ChatParseError;
		}
	}
}
StudError Stud::ParseChatContentJson(const std::string& role, const char* reasoning, const char* contentJson, PromptMessage& result, std::string& error){
	result = PromptMessage();
	error.clear();
	try{
		nlohmann::ordered_json messageJson;
		messageJson["role"] = role;
		messageJson["content"] = contentJson && contentJson[0] != '\0' ? nlohmann::ordered_json::parse(contentJson) : nlohmann::ordered_json("");
		if(role == "assistant" && reasoning && reasoning[0] != '\0') messageJson["reasoning_content"] = reasoning;
		return NormalizeMessageJson(std::move(messageJson), result.message, result.media, error);
	} catch(const std::exception& e){
		error = e.what();
		return StudError::ChatParseError;
	}
}
StudError Stud::ParseChatMessagesJson(const char* messagesJson, std::vector<common_chat_msg>& messages, std::vector<MessageMedia>& media, std::string& error){
	messages.clear();
	media.clear();
	error.clear();
	if(!messagesJson || messagesJson[0] == '\0') return StudError::Success;
	try{
		const auto root = nlohmann::ordered_json::parse(messagesJson);
		if(!root.is_array()) throw std::invalid_argument("Expected a messages array.");
		messages.reserve(root.size());
		media.reserve(root.size());
		for(const auto& messageJson : root){
			common_chat_msg message;
			MessageMedia messageMedia;
			const auto parseError = NormalizeMessageJson(messageJson, message, messageMedia, error);
			if(parseError != StudError::Success) return parseError;
			messages.push_back(std::move(message));
			media.push_back(std::move(messageMedia));
		}
		return StudError::Success;
	} catch(const std::exception& e){
		error = e.what();
		return StudError::ChatParseError;
	}
}
