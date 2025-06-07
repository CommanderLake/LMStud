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
	std::vector<WebSection> sections;
};
static std::unordered_map<std::string, CachedPage> _webCache;
extern "C" {
	EXPORT void SetGoogle(const char* apiKey, const char* searchEngineId);
	EXPORT const char* GoogleSearch(const char* query);
	EXPORT const char* FetchWebpage(const char* argsJson);
	EXPORT const char* BrowseWebCache(const char* argsJson);
	EXPORT const char* GetWebSection(const char* argsJson);
	EXPORT void ClearWebCache();
}