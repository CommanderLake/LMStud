#include "tools.h"
#include "stud.h"
#include "StudState.h"
#include "JSONCommon.h"
#include <ctime>
#include <regex>
static std::string _toolsJsonCache;
static bool _toolsJsonDirty = true;
void SetGoogle(const char* apiKey, const char* searchEngineId, const int resultCount){
	if(apiKey){ googleAPIKey = apiKey; } else{ googleAPIKey.clear(); }
	if(searchEngineId){ googleSearchID = searchEngineId; } else{ googleSearchID.clear(); }
	if(resultCount >= 1 && resultCount <= 100) googleResultCount = resultCount;
}
std::string GetLongDateTime(const char* slotName, const char* argsJson){
	const auto now = std::time(nullptr);
	std::tm localTime{};
	localtime_s(&localTime, &now);
	char buffer[80];
	std::strftime(buffer, sizeof buffer, "%A, %d %B %Y, %H:%M:%S", &localTime);
	return std::string(buffer);
}
void RegisterTools(const char* slotName, const bool dateTime, const bool googleSearch, const bool webpageFetch, const bool fileList, const bool fileCreate, const bool fileRead, const bool fileWrite, const bool commandPrompt){
	ClearTools(slotName);
	if(dateTime) AddTool(slotName, "get_datetime", "Return local date and time", "{\"type\":\"object\",\"properties\":{},\"required\":[]}", GetLongDateTime);
	if(googleSearch){ AddTool(slotName, "web_search", "Google search, JSON results, call get_webpage next", "{\"type\":\"object\",\"properties\":{\"query\":{\"type\":\"string\"}},\"required\":[\"query\"]}", GoogleSearch); }
	if(webpageFetch){
		AddTool(slotName, "get_webpage", "Fetch page and preview <p>, <article> and <section> text, call get_webpage_text next", "{\"type\":\"object\",\"properties\":{\"url\":{\"type\":\"string\"}},\"required\":[\"url\"]}", GetWebpage);
		AddTool(slotName, "get_webpage_text", "Get full text for a get_webpage preview", "{\"type\":\"object\",\"properties\":{\"url\":{\"type\":\"string\"},\"id\":{\"type\":\"string\"}},\"required\":[\"url\",\"id\"]}", GetWebTag);
		//AddTool(slotName, "list_tags", "List the tags of a previously fetched webpage.", "{\"type\":\"object\",\"properties\":{\"url\":{\"type\":\"string\"}},\"required\":[\"url\"]}", ListWebTags);
	}
	if(fileList){
		AddTool(slotName, "list_directory", "List a bounded directory view (default 200 entries, depth 4). Recursive listings show dependency/build folders without entering them by default. Partial views include a warning and counts; use a narrower path or search_files, or page with next_offset. Entry JSON is capped at 16 KiB per response.", R"({"type":"object","properties":{"path":{"type":"string"},"recursive":{"type":"boolean","default":false},"max_entries":{"type":"integer","minimum":1,"maximum":1000,"default":200},"offset":{"type":"integer","minimum":0,"maximum":2147483647,"default":0,"description":"Resume using next_offset and the same path/filters; directory changes can shift pages"},"max_depth":{"type":"integer","minimum":1,"maximum":32,"default":4,"description":"Recursive depth; 1 lists immediate children only"},"include_generated":{"type":"boolean","default":false,"description":"Enter packages, node_modules, vendor, bin, obj, build, Debug, Release, dist, out, target, .vs, .idea, .venv, venv, __pycache__, .next, .nuget, .cache, coverage, TestResults and bower_components. .git remains excluded."}}})", ListDirectoryTool);
		AddTool(slotName, "search_files", "Search a folder for files by name or relative path", "{\"type\":\"object\",\"properties\":{\"path\":{\"type\":\"string\"},\"query\":{\"type\":\"string\"},\"max_results\":{\"type\":\"integer\",\"minimum\":1},\"case_sensitive\":{\"type\":\"boolean\"},\"recursive\":{\"type\":\"boolean\"},\"use_regex\":{\"type\":\"boolean\"}},\"required\":[\"path\",\"query\"]}", SearchFilesTool);
	}
	if(fileCreate){ AddTool(slotName, "create_file", "Create a new file and fill it with text", "{\"type\":\"object\",\"properties\":{\"path\":{\"type\":\"string\"},\"text\":{\"type\":\"string\"},\"overwrite\":{\"type\":\"boolean\"}},\"required\":[\"path\",\"text\"]}", CreateFileTool); }
	if(fileRead){
		AddTool(slotName, "get_file_info", "Get file or folder metadata: type, size, attributes and UTC dates. Known text files up to 16 MiB also include encoding, line/word/character counts and newline details. Text analysis skips reparse points. Missing paths return exists=false.", "{\"type\":\"object\",\"properties\":{\"path\":{\"type\":\"string\",\"description\":\"Relative to the configured base folder; absolute path required in full filesystem mode\"},\"include_text_stats\":{\"type\":\"boolean\",\"default\":true,\"description\":\"Analyze recognized text files; false returns metadata without reading content\"}},\"required\":[\"path\"]}", GetFileInfoTool);
		AddTool(slotName, "read_file_lines", "Display range of lines from a text file with line numbers", "{\"type\":\"object\",\"properties\":{\"path\":{\"type\":\"string\"},\"start\":{\"type\":\"integer\"},\"end\":{\"type\":\"integer\"}},\"required\":[\"path\"]}", ReadFileTool);
		AddTool(slotName, "search_file_contents", "Search files for lines containing a keyword or regex", "{\"type\":\"object\",\"properties\":{\"path\":{\"type\":\"string\"},\"keyword\":{\"type\":\"string\"},\"max_results\":{\"type\":\"integer\",\"minimum\":1},\"case_sensitive\":{\"type\":\"boolean\"},\"recursive\":{\"type\":\"boolean\"},\"use_regex\":{\"type\":\"boolean\"}},\"required\":[\"path\",\"keyword\",\"max_results\"]}", SearchFileTool);
	}
	if(fileWrite){
		AddTool(slotName, "replace_file_lines", "Replace range of lines in a text file", "{\"type\":\"object\",\"properties\":{\"path\":{\"type\":\"string\"},\"start\":{\"type\":\"integer\"},\"end\":{\"type\":\"integer\"},\"text\":{\"type\":\"string\"}},\"required\":[\"path\",\"start\",\"end\",\"text\"]}", ReplaceLinesTool);
		AddTool(slotName, "apply_file_diff_patch", "Apply unified diff patch to a file", "{\"type\":\"object\",\"properties\":{\"path\":{\"type\":\"string\"},\"patch\":{\"type\":\"string\"}},\"required\":[\"path\",\"patch\"]}", ApplyPatchTool);
	}
	if(commandPrompt){
		AddTool(slotName, "command_prompt_start", "Start or restart a Windows command prompt session", "{\"type\":\"object\",\"properties\":{},\"required\":[]}", StartCommandPromptTool);
		AddTool(slotName, "command_prompt_execute", "Execute a command in the active Windows command prompt session", "{\"type\":\"object\",\"properties\":{\"command\":{\"type\":\"string\"},\"close\":{\"type\":\"boolean\"}},\"required\":[\"command\"]}", CommandPromptExecuteTool);
	} else CloseCommandPrompt();
}
extern "C" void MarkToolsJsonDirty(){ _toolsJsonDirty = true; }
static void RefreshToolsJsonCache(const char* slotName){
	if(!_toolsJsonDirty) return;
	const auto& tools = GetModel(slotName)->session.tools;
	std::string json = "[";
	for(size_t i = 0; i < tools.size(); i++){
		const auto& tool = tools[i];
		if(i > 0) json += ",";
		json += "{\"type\":\"function\",\"function\":{";
		json += "\"name\":\"" + JsonEscape(tool.name) + "\",";
		json += "\"description\":\"" + JsonEscape(tool.description) + "\",";
		json += "\"parameters\":";
		if(tool.parameters.empty()) json += "{}";
		else json += tool.parameters;
		json += "}}";
	}
	json += "]";
	_toolsJsonCache = json;
	_toolsJsonDirty = false;
}
extern "C" EXPORT const char* GetToolsJson(const char* slotName, int* length){
	RefreshToolsJsonCache(slotName);
	if(length) *length = static_cast<int>(_toolsJsonCache.size());
	return _toolsJsonCache.c_str();
}
