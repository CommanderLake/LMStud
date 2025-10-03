#pragma once
#include <filesystem>
#include <unordered_map>
#define EXPORT __declspec(dllexport)
inline std::string googleAPIKey;
inline std::string googleSearchID;
inline int googleResultCount = 5;
inline std::filesystem::path _baseFolder;
struct WebSection{
	std::string tag;
	std::string text;
};
struct CachedPage{
	std::vector<WebSection> tags;
};
inline std::unordered_map<std::string, CachedPage> _webCache;
extern "C" {
	EXPORT void SetGoogle(const char* apiKey, const char* searchEngineId, int resultCount);
	EXPORT void ClearWebCache();
	EXPORT void SetFileBaseDir(const char* dir);
	EXPORT void RegisterTools(bool dateTime, bool googleSearch, bool webpageFetch, bool fileList, bool fileCreate, bool fileRead, bool fileWrite, bool commandPrompt);
	EXPORT void CloseCommandPrompt();
}
std::string JsonEscape(const std::string& in);
std::string GetArgValue(const char* args, const char* key);
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
std::string StartCommandPromptTool(const char* argsJson);
std::string CommandPromptExecuteTool(const char* argsJson);