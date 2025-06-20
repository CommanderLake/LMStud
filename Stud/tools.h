#pragma once
#include <unordered_map>
#define EXPORT __declspec(dllexport)
inline std::string googleAPIKey;
inline std::string googleSearchID;
inline int googleResultCount = 5;
struct WebSection{
	std::string tag;
	std::string text;
};
struct CachedPage{
	std::vector<WebSection> tags;
};
extern "C" {
	EXPORT void SetGoogle(const char* apiKey, const char* searchEngineId, int resultCount);
	EXPORT std::string GoogleSearch(const char* query);
	EXPORT std::string GetWebpage(const char* argsJson);
	EXPORT std::string GetWebTag(const char* argsJson);
	EXPORT std::string ListWebTags(const char* argsJson);
	EXPORT void ClearWebCache();
	EXPORT std::string GetLongDateTime(const char* argsJson);
	EXPORT void RegisterTools(bool googleSearch, bool webpageFetch);
}