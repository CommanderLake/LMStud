#pragma once
#include <unordered_map>
#define EXPORT __declspec(dllexport)
inline std::string googleAPIKey;
inline std::string googleSearchID;
struct WebSection{
	std::string tag;
	std::string text;
};
struct CachedPage{
	std::vector<WebSection> tags;
};
extern "C" {
	EXPORT void SetGoogle(const char* apiKey, const char* searchEngineId);
	EXPORT const char* GoogleSearch(const char* query);
	EXPORT const char* GetWebpage(const char* argsJson);
	EXPORT const char* GetWebTag(const char* argsJson);
	EXPORT const char* ListWebTags(const char* argsJson);
	EXPORT void ClearWebCache();
}