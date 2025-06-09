#include "tools.h"
#include "hug.h"
#include <charconv>
#include <regex>
#include <curl\curl.h>
static std::string UrlEncode(const char* text){
	CURL* curl = curl_easy_init();
	char* enc = curl_easy_escape(curl, text, 0);
	std::string out = enc ? enc : "";
	curl_free(enc);
	curl_easy_cleanup(curl);
	return out;
}
void SetGoogle(const char* apiKey, const char* searchEngineId){
	googleAPIKey = apiKey;
	googleSearchID = searchEngineId;
}
const char* GoogleSearch(const char* argsJson){
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
	return PerformHttpGet(("https://customsearch.googleapis.com/customsearch/v1?key="+googleAPIKey+"&cx="+googleSearchID+"&num=5&fields=items(title,link,snippet)&prettyPrint=true&q="+UrlEncode(query.c_str())).c_str());
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
static void ParseSections(const std::string& html, CachedPage& page){
	const std::string cleaned = std::regex_replace(html, std::regex(R"(<(script|style)[^>]*?>[\s\S]*?<\/\s*\1\s*>)", std::regex::icase), " ");
	const std::regex sectionRe(R"(<(p|article|section)[^>]*>([\s\S]*?)<\/\s*\1\s*>)", std::regex::icase);
	const std::regex tagRe("<[^>]+>");
	const std::regex wsRe("\\s+");
	auto it = cleaned.cbegin();
	std::smatch m;
	while(std::regex_search(it, cleaned.cend(), m, sectionRe)){
		const std::string tag = m[1].str();
		std::string txt = std::regex_replace(m[2].str(), tagRe, " ");
		txt = std::regex_replace(txt, wsRe, " ");
		const size_t s = txt.find_first_not_of(' ');
		const size_t e = txt.find_last_not_of(' ');
		if(s!=std::string::npos&&e!=std::string::npos){
			txt = txt.substr(s, e-s+1);
			if(!txt.empty()) page.sections.push_back({tag, txt});
		}
		it = m.suffix().first;
	}
}
static char* MakeJson(const std::string& s){
	const auto out = static_cast<char*>(std::malloc(s.size()+1));
	if(out) std::memcpy(out, s.c_str(), s.size()+1);
	return out;
}
const char* GetWebpage(const char* argsJson){
	std::string url = GetJsonValue(argsJson, "url");
	if(url.empty()) url = argsJson ? argsJson : "";
	const auto res = PerformHttpGet(url.c_str());
	if(!res) return nullptr;
	const std::string html(res);
	FreeMemory(res);
	CachedPage page;
	ParseSections(html, page);
	_webCache[url] = page;
	std::string json = "{\n  \"url\": \""+JsonEscape(url)+"\",\n  \"sections\": [\n";
	for(size_t i = 0; i<page.sections.size(); ++i){
		auto& sec = page.sections[i];
		std::string snippet = sec.text.substr(0, 80);
		if(sec.text.size()>80) snippet += "...";
		json += "    {\"id\": "+std::to_string(i)+", \"tag\": \""+sec.tag+"\", \"snippet\": \""+JsonEscape(snippet)+"\"}";
		if(i+1<page.sections.size()) json += ",";
		json += "\n";
	}
	json += "  ]\n}";
	return MakeJson(json);
}
const char* GetWebSection(const char* argsJson){
	std::string url = GetJsonValue(argsJson, "url");
	const std::string idStr = GetJsonValue(argsJson, "id");
	if(url.empty()) url = argsJson ? argsJson : "";
	int id = -1;
	if(!idStr.empty()){
		const auto res = std::from_chars(idStr.data(), idStr.data() + idStr.size(), id);
		if(res.ec != std::errc()) id = -1;
	}
	const auto it = _webCache.find(url);
	if(it==_webCache.end()||id<0||id>=static_cast<int>(it->second.sections.size())) return MakeJson("{\"error\":\"not found\"}");
	return MakeJson(JsonEscape(it->second.sections[id].text));
}
const char* ListSections(const char* argsJson){
	std::string url = GetJsonValue(argsJson, "url");
	if(url.empty()) url = argsJson ? argsJson : "";
	const auto it = _webCache.find(url);
	if(it==_webCache.end()) return MakeJson("{\"error\":\"not cached\"}");
	const auto& page = it->second;
	std::string json = "{\n  \"url\": \""+JsonEscape(url)+"\",\n  \"sections\": [\n";
	for(size_t i = 0; i<page.sections.size(); ++i){
		auto& sec = page.sections[i];
		std::string snippet = sec.text.substr(0, 80);
		if(sec.text.size()>80) snippet += "...";
		json += "    {\"id\": "+std::to_string(i)+", \"tag\": \""+sec.tag+"\", \"snippet\": \""+JsonEscape(snippet)+"\"}";
		if(i+1<page.sections.size()) json += ",";
		json += "\n";
	}
	json += "  ]\n}";
	return MakeJson(json);
}
void ClearWebCache(){ _webCache.clear(); }