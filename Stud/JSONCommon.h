#pragma once
#include <string>
#include <vector>
const char* SkipJsonWhitespace(const char* p);
bool ReadJsonString(const char*& p, std::string& value);
const char* SkipJsonValue(const char* p);
bool GetJsonPropertyRaw(const std::string& objectJson, const char* key, std::string& rawValue);
bool GetJsonStringProperty(const std::string& objectJson, const char* key, std::string& value);
std::vector<std::string> ExtractJsonObjects(const char* json);
bool IsJsonNull(const std::string& rawValue);
std::string JsonEscape(const std::string& in);
std::string JsonString(const std::string& in);
std::string GetArgValue(const char* args, const char* key);