#include "tools.h"
#include "stud.h"
#include <regex>
void SetGoogle(const char* apiKey, const char* searchEngineId, const int resultCount){
	if(apiKey){ googleAPIKey = apiKey; } else{ googleAPIKey.clear(); }
	if(searchEngineId){ googleSearchID = searchEngineId; } else{ googleSearchID.clear(); }
	if(resultCount >= 1 && resultCount <= 100) googleResultCount = resultCount;
}
std::string JsonEscape(const std::string& in){
	std::string out;
	out.reserve(in.size());
	for(const char c : in){
		switch(c){
			case '"': out += "\\\"";
				break;
			case '\\': out += "\\\\";
				break;
			case '\n': out += "\\n";
				break;
			case '\r': out += "\\r";
				break;
			case '\t': out += "\\t";
				break;
			default: out += c;
				break;
		}
	}
	return out;
}
static std::string GetJsonValue(const char* json, const char* key){
	if(!json || !key) return "";
	const char* p = strstr(json, key);
	if(!p) return "";
	p = strchr(p, ':');
	if(!p) return "";
	++p;
	while(*p == ' ' || *p == '\t') ++p;
	if(*p == '\"'){
		++p;
		std::string val;
		bool esc = false;
		while(*p){
			const char c = *p++;
			if(esc){
				switch(c){
					case 'n': val += '\n';
						break;
					case 'r': val += '\r';
						break;
					case 't': val += '\t';
						break;
					case '"': val += '"';
						break;
					case '\\': val += '\\';
						break;
					default: val += c;
						break;
				}
				esc = false;
			} else if(c == '\\'){ esc = true; } else if(c == '\"') break;
			else val += c;
		}
		return val;
	}
	const char* end = p;
	while(*end && *end != ',' && *end != '}' && *end != ' ') ++end;
	if(end > p) return std::string(p, end);
	return "";
}
std::string GetArgValue(const char* args, const char* key){
	if(!args || !key) return "";
	std::string paramTag = "<parameter=";
	paramTag += key;
	paramTag += ">";
	const char* p = strstr(args, paramTag.c_str());
	if(p){
		p += paramTag.size();
		const char* end = strstr(p, "</parameter>");
		if(end){
			while(*p=='\n' || *p=='\r') ++p;
			const char* e = end;
			while(e>p && (*(e-1)=='\n' || *(e-1)=='\r')) --e;
			return std::string(p, e);
		}
	}
	std::string openTag = "<";
	openTag += key;
	openTag += ">";
	p = strstr(args, openTag.c_str());
	if(p){
		p += openTag.size();
		std::string closeTag = "</";
		closeTag += key;
		closeTag += ">";
		const char* end = strstr(p, closeTag.c_str());
		if(end) return std::string(p, end);
	}
	return GetJsonValue(args, key);
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
		AddTool("file_search_keyword", "Search a file for lines containing a keyword", "{\"type\":\"object\",\"properties\":{\"path\":{\"type\":\"string\"},\"keyword\":{\"type\":\"string\"},\"max_results\":{\"type\":\"integer\",\"minimum\":1},\"case_sensitive\":{\"type\":\"boolean\"}},\"required\":[\"path\",\"keyword\",\"max_results\"]}", SearchFileTool);
	}
	if(fileWrite){
		AddTool("file_replace_lines", "Replace range of lines in a text file (1 based)", "{\"type\":\"object\",\"properties\":{\"path\":{\"type\":\"string\"},\"start\":{\"type\":\"integer\"},\"end\":{\"type\":\"integer\"},\"text\":{\"type\":\"string\"}},\"required\":[\"path\",\"start\",\"end\",\"text\"]}", ReplaceLinesTool);
		AddTool("file_apply_diff_patch", "Apply unified diff patch to a file", "{\"type\":\"object\",\"properties\":{\"path\":{\"type\":\"string\"},\"patch\":{\"type\":\"string\"}},\"required\":[\"path\",\"patch\"]}", ApplyPatchTool);
	}
	if(commandPrompt){
		AddTool("command_prompt_start", "Start or restart a Windows command prompt session and return the initial output.", "{\"type\":\"object\",\"properties\":{},\"required\":[]}", StartCommandPromptTool);
		AddTool("command_prompt_execute", "Execute a command in the active Windows command prompt session.", "{\"type\":\"object\",\"properties\":{\"command\":{\"type\":\"string\"},\"close\":{\"type\":\"boolean\"}},\"required\":[\"command\"]}", CommandPromptExecuteTool);
	} else CloseCommandPrompt();
}