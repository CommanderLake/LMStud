#include "tools.h"
#include "hug.h"
#include <charconv>
#include <regex>
#include <curl\curl.h>
#include "stud.h"
static std::unordered_map<std::string, CachedPage> _webCache;
static std::string UrlEncode(const char* text){
	CURL* curl = curl_easy_init();
	char* enc = curl_easy_escape(curl, text, 0);
	std::string out = enc ? enc : "";
	curl_free(enc);
	curl_easy_cleanup(curl);
	return out;
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
		const char* end = strchr(p, '\"');
		if(end&&end>p) return std::string(p, end);
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
void RegisterTools(const bool googleSearch, const bool webpageFetch){
	ClearTools();
	AddTool("get_datetime", "Return local date and time.", "{\"type\":\"object\"}", GetLongDateTime);
	if(googleSearch){ AddTool("web_search", "Google search, JSON results, call get_webpage next.", "{\"type\":\"object\",\"properties\":{\"query\":{\"type\":\"string\"}},\"required\":[\"query\"]}", GoogleSearch); }
	if(webpageFetch){
		AddTool("get_webpage", "Fetch page and preview <p>, <article> and <section> text, call get_webpage_text next.", "{\"type\":\"object\",\"properties\":{\"url\":{\"type\":\"string\"}},\"required\":[\"url\"]}", GetWebpage);
		AddTool("get_webpage_text", "Get full text for a get_webpage preview.", "{\"type\":\"object\",\"properties\":{\"url\":{\"type\":\"string\"},\"id\":{\"type\":\"string\"}},\"required\":[\"url\",\"id\"]}", GetWebTag);
		//AddTool("list_tags", "List the tags of a previously fetched webpage.", "{\"type\":\"object\",\"properties\":{\"url\":{\"type\":\"string\"}},\"required\":[\"url\"]}", ListWebTags);
	}
}