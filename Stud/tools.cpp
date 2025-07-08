#include "tools.h"
#include "hug.h"
#include "stud.h"
#include <charconv>
#include <filesystem>
#include <fstream>
#include <regex>
#include <curl\curl.h>
static std::unordered_map<std::string, CachedPage> _webCache;
static std::filesystem::path _baseFolder;
static bool IsPathAllowed(const std::filesystem::path& p){
	if(_baseFolder.empty()) return false;
	std::error_code ec;
	const auto abs = weakly_canonical(p, ec);
	const auto base = weakly_canonical(_baseFolder, ec);
	if(ec) return false;
	const auto absStr = abs.native();
	const auto baseStr = base.native();
	if(absStr.rfind(baseStr, 0) != 0) return false;
	if(absStr.size() == baseStr.size()) return true;
	return absStr[baseStr.size()] == '\\';
}
void SetFileBaseDir(const char* dir){
	if(dir && *dir){
		std::error_code ec;
		_baseFolder = std::filesystem::weakly_canonical(dir, ec);
		if(ec) _baseFolder.clear();
	} else{
		_baseFolder.clear();
	}
}
static std::string UrlEncode(const char* text){
	CURL* curl = curl_easy_init();
	char* enc = curl_easy_escape(curl, text, 0);
	std::string out = enc ? enc : "";
	curl_free(enc);
	curl_easy_cleanup(curl);
	return out;
}
void AddTool(const char* name, const char* description, const char* parameters, std::string(*handler)(const char* args)){
	if(!name || !_hasTools) return;
	common_chat_tool t;
	t.name = name;
	if(description) t.description = description;
	if(parameters) t.parameters = parameters;
	_tools.push_back(t);
	if(handler) _toolHandlers[name] = handler;
}
void ClearTools(){
	_tools.clear();
	_toolHandlers.clear();
}
void SetGoogle(const char* apiKey, const char* searchEngineId, const int resultCount){
	if(apiKey){ googleAPIKey = apiKey; } else{ googleAPIKey.clear(); }
	if(searchEngineId){ googleSearchID = searchEngineId; } else{ googleSearchID.clear(); }
	if(resultCount >= 1 && resultCount <= 100) googleResultCount = resultCount;
}
std::string GoogleSearch(const char* argsJson){
	const char* queryStart = nullptr;
	const char* queryEnd = nullptr;
	if(argsJson){
		const char* p = strstr(argsJson, "\"query\"");
		if(p){
			p = strchr(p, ':');
			if(p){
				++p;
				while(*p==' '||*p=='\t') ++p;
				if(*p=='\"'){
					queryStart = ++p;
					queryEnd = strchr(queryStart, '\"');
				}
			}
		}
	}
	std::string query;
	if(queryStart&&queryEnd&&queryEnd>queryStart){ query.assign(queryStart, queryEnd); } else{ query = argsJson ? argsJson : ""; }
	const auto result = PerformHttpGet(("https://customsearch.googleapis.com/customsearch/v1?key="+googleAPIKey+"&cx="+googleSearchID+"&num="+std::to_string(googleResultCount)+"&fields=items(title,link,snippet)&prettyPrint=true&q="+UrlEncode(query.c_str())).c_str());
	auto resultStr = std::string(result);
	std::free(result);
	return resultStr;
}
static std::string JsonEscape(const std::string& in){
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
	if(!json||!key) return "";
	const char* p = strstr(json, key);
	if(!p) return "";
	p = strchr(p, ':');
	if(!p) return "";
	++p;
	while(*p==' '||*p=='\t') ++p;
	if(*p=='\"'){
		++p;
		std::string val;
		bool esc = false;
		while(*p){
			const char c = *p++;
			if(esc){
				switch(c){
					case 'n': val += '\n'; break;
					case 'r': val += '\r'; break;
					case 't': val += '\t'; break;
					case '"': val += '"'; break;
					case '\\': val += '\\'; break;
					default: val += c; break;
				}
				esc = false;
			} else if(c=='\\'){ esc = true; } else if(c=='\"') break;
			else val += c;
		}
		return val;
	} else{
		const char* end = p;
		while(*end&&*end!=','&&*end!='}'&&*end!=' ') ++end;
		if(end>p) return std::string(p, end);
	}
	return "";
}
static void ParseTags(const std::string& html, CachedPage& page){
	const std::string cleaned = std::regex_replace(html, std::regex(R"(<(script|style)[^>]*?>[\s\S]*?<\/\s*\1\s*>)", std::regex::icase), " ");
	const std::regex tagsRe(R"(<(p|article|section)[^>]*>([\s\S]*?)<\/\s*\1\s*>)", std::regex::icase);
	const std::regex tagRe("<[^>]+>");
	const std::regex wsRe("\\s+");
	auto it = cleaned.cbegin();
	std::smatch m;
	while(std::regex_search(it, cleaned.cend(), m, tagsRe)){
		const std::string tag = m[1].str();
		std::string txt = std::regex_replace(m[2].str(), tagRe, " ");
		txt = std::regex_replace(txt, wsRe, " ");
		const size_t s = txt.find_first_not_of(' ');
		const size_t e = txt.find_last_not_of(' ');
		if(s!=std::string::npos&&e!=std::string::npos){
			txt = txt.substr(s, e-s+1);
			if(!txt.empty()) page.tags.push_back({tag, txt});
		}
		it = m.suffix().first;
	}
}
static std::string TagsToJson(const std::string& url, const CachedPage& page){
	std::string json = "{\n  \"url\": \""+JsonEscape(url)+"\",\n  \"tags\": [\n";
	for(size_t i = 0; i<page.tags.size(); ++i){
		const auto& sec = page.tags[i];
		std::string preview = sec.text.substr(0, 80);
		if(sec.text.size()>80) preview += "...";
		json += "    {\"id\": "+std::to_string(i)+", \"tag\": \""+sec.tag+"\", \"preview\": \""+JsonEscape(preview)+"\", \"length\": "+std::to_string(sec.text.size())+"}";
		if(i+1<page.tags.size()) json += ",";
		json += "\n";
	}
	json += "  ]\n}";
	return json;
}
std::string GetWebpage(const char* argsJson){
	std::string url = GetJsonValue(argsJson, "url");
	if(url.empty()) url = argsJson ? argsJson : "";
	const auto res = PerformHttpGet(url.c_str());
	if(!res) return "{\"error\":\"download failed\"}";
	const std::string html(res);
	FreeMemory(res);
	CachedPage page;
	ParseTags(html, page);
	_webCache[url] = page;
	return TagsToJson(url, page);
}
std::string GetWebTag(const char* argsJson){
	std::string url = GetJsonValue(argsJson, "url");
	const std::string idStr = GetJsonValue(argsJson, "id");
	if(url.empty()) url = argsJson ? argsJson : "";
	int id = -1;
	if(!idStr.empty()){
		const auto res = std::from_chars(idStr.data(), idStr.data()+idStr.size(), id);
		if(res.ec!=std::errc()) id = -1;
	}
	const auto it = _webCache.find(url);
	if(it==_webCache.end()||id<0||id>=static_cast<int>(it->second.tags.size())) return "{\"error\":\"not found\"}";
	return JsonEscape(it->second.tags[id].text);
}
std::string ListWebTags(const char* argsJson){
	std::string url = GetJsonValue(argsJson, "url");
	if(url.empty()) url = argsJson ? argsJson : "";
	const auto it = _webCache.find(url);
	if(it==_webCache.end()) return "{\"error\":\"not cached\"}";
	const auto& page = it->second;
	return TagsToJson(url, page);
}
void ClearWebCache(){ _webCache.clear(); }
std::string GetLongDateTime(const char* argsJson){
	const auto now = std::time(nullptr);
	const std::tm* localTime = std::localtime(&now);
	char buffer[80];
	std::strftime(buffer, sizeof buffer, "%A, %d %B %Y, %H:%M:%S", localTime);
	return std::string(buffer);
}
std::string ListFilesTool(const char* argsJson){
	std::string path = GetJsonValue(argsJson, "path");
	const std::string recStr = GetJsonValue(argsJson, "recursive");
	const bool recursive = recStr=="true"||recStr=="1";
	if(path.empty()) path = ".";
	const std::filesystem::path p = _baseFolder / path;
	if(!IsPathAllowed(p)) return "{\"error\":\"invalid path\"}";
	std::vector<std::string> files;
	std::error_code ec;
	if(recursive){
		for(const auto& entry : std::filesystem::recursive_directory_iterator(p, ec)){
			if(ec) break;
			files.push_back(relative(entry.path(), _baseFolder, ec).generic_string());
		}
	} else{
		for(const auto& entry : std::filesystem::directory_iterator(p, ec)){
			if(ec) break;
			files.push_back(relative(entry.path(), _baseFolder, ec).generic_string());
		}
	}
	if(ec) return "{\"error\":\"io error\"}";
	std::string json = "{\"files\":[";
	for(size_t i = 0; i<files.size(); ++i){
		json += "\"" + JsonEscape(files[i]) + "\"";
		if(i+1<files.size()) json += ",";
	}
	json += "]}";
	return json;
}
std::string ReadFileTool(const char* argsJson){
	std::string path = GetJsonValue(argsJson, "path");
	const std::string startStr = GetJsonValue(argsJson, "start");
	const std::string endStr = GetJsonValue(argsJson, "end");
	int start = -1, end = -1;
	if(!startStr.empty()) std::from_chars(startStr.data(), startStr.data()+startStr.size(), start);
	if(!endStr.empty()) std::from_chars(endStr.data(), endStr.data()+endStr.size(), end);
	std::filesystem::path p = _baseFolder / path;
	if(!IsPathAllowed(p)) return "{\"error\":\"invalid path\"}";
	if(is_directory(p)) return "{\"error\":\"path is a folder\"}";
	std::ifstream f(p, std::ios::binary);
	if(!f.is_open()) return "{\"error\":\"open failed\"}";
	std::string line, out;
	int lineNo = 1;
	while(std::getline(f, line)){
		if((start==-1||lineNo>=start) && (end==-1||lineNo<=end)){
			out += line;
			out += '\n';
		}
		if(end!=-1 && lineNo>=end) break;
		++lineNo;
	}
	return out;
}
std::string CreateFileTool(const char* argsJson){
	const std::string path = GetJsonValue(argsJson, "path");
	const std::string text = GetJsonValue(argsJson, "text");
	const std::string overwriteStr = GetJsonValue(argsJson, "overwrite");
	const bool overwrite = overwriteStr=="true"||overwriteStr=="1";
	const std::filesystem::path p = _baseFolder / path;
	if(!IsPathAllowed(p)) return "{\"error\":\"invalid path\"}";
	if(exists(p) && !overwrite) return "{\"error\":\"exists\"}";
	create_directories(p.parent_path());
	std::ofstream f(p, std::ios::binary);
	if(!f.is_open()) return "{\"error\":\"open failed\"}";
	f << text;
	return "{\"result\":\"success\"}";
}
std::string ReplaceLinesTool(const char* argsJson){
	std::string path = GetJsonValue(argsJson, "path");
	const std::string startStr = GetJsonValue(argsJson, "start");
	const std::string endStr = GetJsonValue(argsJson, "end");
	std::string text = GetJsonValue(argsJson, "text");
	int start = -1, end = -1;
	if(!startStr.empty()) std::from_chars(startStr.data(), startStr.data()+startStr.size(), start);
	if(!endStr.empty()) std::from_chars(endStr.data(), endStr.data()+endStr.size(), end);
	if(start<1||end<start) return "{\"error\":\"range\"}";
	std::filesystem::path p = _baseFolder / path;
	if(!IsPathAllowed(p)) return "{\"error\":\"invalid path\"}";
	std::ifstream in(p, std::ios::binary);
	if(!in.is_open()) return "{\"error\":\"open failed\"}";
	std::vector<std::string> lines;
	std::string line;
	while(std::getline(in, line)){ lines.push_back(line); }
	in.close();
	if(start-1>static_cast<int>(lines.size())||end-1>static_cast<int>(lines.size())) return "{\"error\":\"range\"}";
	std::vector<std::string> newLines;
	std::stringstream ss(text);
	while(std::getline(ss, line)){ newLines.push_back(line); }
	lines.erase(lines.begin()+start-1, lines.begin()+end);
	lines.insert(lines.begin()+start-1, newLines.begin(), newLines.end());
	std::ofstream out(p, std::ios::binary|std::ios::trunc);
	if(!out.is_open()) return "{\"error\":\"open failed\"}";
	for(size_t i = 0; i<lines.size(); ++i){ out<<lines[i]; if(i+1<lines.size()) out<<'\n'; }
	return "{\"result\":\"success\"}";
}
void RegisterTools(const bool googleSearch, const bool webpageFetch, const bool fileList, const bool fileCreate, const bool fileRead, const bool fileWrite){
	ClearTools();
	AddTool("get_datetime", "Return local date and time", "{\"type\":\"object\"}", GetLongDateTime);
	if(googleSearch){ AddTool("web_search", "Google search, JSON results, call get_webpage next.", "{\"type\":\"object\",\"properties\":{\"query\":{\"type\":\"string\"}},\"required\":[\"query\"]}", GoogleSearch); }
	if(webpageFetch){
		AddTool("get_webpage", "Fetch page and preview <p>, <article> and <section> text, call get_webpage_text next", "{\"type\":\"object\",\"properties\":{\"url\":{\"type\":\"string\"}},\"required\":[\"url\"]}", GetWebpage);
		AddTool("get_webpage_text", "Get full text for a get_webpage preview", "{\"type\":\"object\",\"properties\":{\"url\":{\"type\":\"string\"},\"id\":{\"type\":\"string\"}},\"required\":[\"url\",\"id\"]}", GetWebTag);
		//AddTool("list_tags", "List the tags of a previously fetched webpage.", "{\"type\":\"object\",\"properties\":{\"url\":{\"type\":\"string\"}},\"required\":[\"url\"]}", ListWebTags);
	}
	if(fileList){
		AddTool("list_directory", "List files in path, path is relative to a base folder", "{\"type\":\"object\",\"properties\":{\"path\":{\"type\":\"string\"},\"recursive\":{\"type\":\"boolean\"}}}", ListFilesTool);
	}
	if(fileCreate){
		AddTool("create_file", "Create a new file and fill it with text", "{\"type\":\"object\",\"properties\":{\"path\":{\"type\":\"string\"},\"text\":{\"type\":\"string\"},\"overwrite\":{\"type\":\"boolean\"}},\"required\":[\"path\",\"text\"]}", CreateFileTool);
	}
	if(fileRead){
		AddTool("read_file_lines", "Read range of lines from a text file", "{\"type\":\"object\",\"properties\":{\"path\":{\"type\":\"string\"},\"start\":{\"type\":\"integer\"},\"end\":{\"type\":\"integer\"}},\"required\":[\"path\"]}", ReadFileTool);
	}
	if(fileWrite){
		AddTool("replace_file_lines", "Replace range of lines in a text file", "{\"type\":\"object\",\"properties\":{\"path\":{\"type\":\"string\"},\"start\":{\"type\":\"integer\"},\"end\":{\"type\":\"integer\"},\"text\":{\"type\":\"string\"}},\"required\":[\"path\",\"start\",\"end\",\"text\"]}", ReplaceLinesTool);
	}
}