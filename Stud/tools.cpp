#include "tools.h"
#include "stud.h"
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
std::string GetLongDateTime(const char* argsJson){
	const auto now = std::time(nullptr);
	std::tm localTime{};
	localtime_s(&localTime, &now);
	char buffer[80];
	std::strftime(buffer, sizeof buffer, "%A, %d %B %Y, %H:%M:%S", &localTime);
	return std::string(buffer);
}
void RegisterTools(const bool dateTime, const bool googleSearch, const bool webpageFetch, const bool fileList, const bool fileCreate, const bool fileRead, const bool fileWrite, const bool commandPrompt){
	ClearTools();
	if(dateTime) AddTool("get_datetime", "Return local date and time", "{\"type\":\"object\",\"properties\":{},\"required\":[]}", GetLongDateTime);
	if(googleSearch){ AddTool("web_search", "Google search, JSON results, call get_webpage next.", "{\"type\":\"object\",\"properties\":{\"query\":{\"type\":\"string\"}},\"required\":[\"query\"]}", GoogleSearch); }
	if(webpageFetch){
		AddTool("get_webpage", "Fetch page and preview <p>, <article> and <section> text, call get_webpage_text next", "{\"type\":\"object\",\"properties\":{\"url\":{\"type\":\"string\"}},\"required\":[\"url\"]}", GetWebpage);
		AddTool("get_webpage_text", "Get full text for a get_webpage preview", "{\"type\":\"object\",\"properties\":{\"url\":{\"type\":\"string\"},\"id\":{\"type\":\"string\"}},\"required\":[\"url\",\"id\"]}", GetWebTag);
		//AddTool("list_tags", "List the tags of a previously fetched webpage.", "{\"type\":\"object\",\"properties\":{\"url\":{\"type\":\"string\"}},\"required\":[\"url\"]}", ListWebTags);
	}
	if(fileList){ AddTool("list_directory", "List files in path, path is relative to a base folder", "{\"type\":\"object\",\"properties\":{\"path\":{\"type\":\"string\"},\"recursive\":{\"type\":\"boolean\"}}}", ListDirectoryTool); }
	if(fileCreate){ AddTool("file_create", "Create a new file and fill it with text", "{\"type\":\"object\",\"properties\":{\"path\":{\"type\":\"string\"},\"text\":{\"type\":\"string\"},\"overwrite\":{\"type\":\"boolean\"}},\"required\":[\"path\",\"text\"]}", CreateFileTool); }
	if(fileRead){
		AddTool("file_read_lines", "Display range of lines from a text file with line numbers", "{\"type\":\"object\",\"properties\":{\"path\":{\"type\":\"string\"},\"start\":{\"type\":\"integer\"},\"end\":{\"type\":\"integer\"}},\"required\":[\"path\"]}", ReadFileTool);
		AddTool("file_search_contents", "Search files for lines containing a keyword or regex", "{\"type\":\"object\",\"properties\":{\"path\":{\"type\":\"string\"},\"keyword\":{\"type\":\"string\"},\"max_results\":{\"type\":\"integer\",\"minimum\":1},\"case_sensitive\":{\"type\":\"boolean\"},\"recursive\":{\"type\":\"boolean\"},\"use_regex\":{\"type\":\"boolean\"}},\"required\":[\"path\",\"keyword\",\"max_results\"]}", SearchFileTool);
	}
	if(fileWrite){
		AddTool("file_replace_lines", "Replace range of lines in a text file", "{\"type\":\"object\",\"properties\":{\"path\":{\"type\":\"string\"},\"start\":{\"type\":\"integer\"},\"end\":{\"type\":\"integer\"},\"text\":{\"type\":\"string\"}},\"required\":[\"path\",\"start\",\"end\",\"text\"]}", ReplaceLinesTool);
		AddTool("file_apply_diff_patch", "Apply unified diff patch to a file", "{\"type\":\"object\",\"properties\":{\"path\":{\"type\":\"string\"},\"patch\":{\"type\":\"string\"}},\"required\":[\"path\",\"patch\"]}", ApplyPatchTool);
	}
	if(commandPrompt){
		AddTool("command_prompt_start", "Start or restart a Windows command prompt session and return the initial output.", "{\"type\":\"object\",\"properties\":{},\"required\":[]}", StartCommandPromptTool);
		AddTool("command_prompt_execute", "Execute a command in the active Windows command prompt session.", "{\"type\":\"object\",\"properties\":{\"command\":{\"type\":\"string\"},\"close\":{\"type\":\"boolean\"}},\"required\":[\"command\"]}", CommandPromptExecuteTool);
	} else CloseCommandPrompt();
}
extern "C" void MarkToolsJsonDirty(){ _toolsJsonDirty = true; }
static void RefreshToolsJsonCache(){
	if(!_toolsJsonDirty) return;
	const auto& tools = Stud::Backend::state().tools;
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
extern "C" EXPORT const char* GetToolsJson(int* length){
	RefreshToolsJsonCache();
	if(length) *length = static_cast<int>(_toolsJsonCache.size());
	return _toolsJsonCache.c_str();
}
