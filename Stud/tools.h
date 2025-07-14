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
	EXPORT void AddTool(const char* name, const char* description, const char* parameters, std::string(*handler)(const char* args));
	EXPORT void ClearTools();
	EXPORT void SetGoogle(const char* apiKey, const char* searchEngineId, int resultCount);
	EXPORT void ClearWebCache();
	EXPORT void SetFileBaseDir(const char* dir);
	EXPORT void RegisterTools(bool googleSearch, bool webpageFetch, bool fileList, bool fileCreate, bool fileRead, bool fileWrite);
}
std::string GoogleSearch(const char* query);
std::string GetWebpage(const char* argsJson);
std::string GetWebTag(const char* argsJson);
std::string ListWebTags(const char* argsJson);
std::string GetLongDateTime(const char* argsJson);
std::string ListDirectoryTool(const char* argsJson);
std::string ReadFileTool(const char* argsJson);
std::string CreateFileTool(const char* argsJson);
std::string ReplaceLinesTool(const char* argsJson);
std::string ApplyPatchTool(const char* argsJson);