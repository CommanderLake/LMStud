#include "JSONCommon.h"
#include <cctype>
#include <cstring>
const char* SkipJsonWhitespace(const char* p){
	while(p && *p && std::isspace(static_cast<unsigned char>(*p))) ++p;
	return p;
}
bool ReadJsonString(const char*& p, std::string& value){
	p = SkipJsonWhitespace(p);
	if(!p || *p != '"') return false;
	++p;
	value.clear();
	while(*p){
		const char c = *p++;
		if(c == '"') return true;
		if(c != '\\'){
			value += c;
			continue;
		}
		const char esc = *p++;
		switch(esc){
			case '"': value += '"';
				break;
			case '\\': value += '\\';
				break;
			case '/': value += '/';
				break;
			case 'b': value += '\b';
				break;
			case 'f': value += '\f';
				break;
			case 'n': value += '\n';
				break;
			case 'r': value += '\r';
				break;
			case 't': value += '\t';
				break;
			case 'u': value += "\\u";
				for(int i = 0; i < 4 && *p; ++i) value += *p++;
				break;
			default: value += esc;
				break;
		}
	}
	return false;
}
static const char* SkipJsonStringRaw(const char* p){
	if(!p || *p != '"') return nullptr;
	++p;
	while(*p){
		const char c = *p++;
		if(c == '\\'){
			if(*p) ++p;
			continue;
		}
		if(c == '"') return p;
	}
	return nullptr;
}
static const char* SkipJsonObjectRaw(const char* p){
	p = SkipJsonWhitespace(p);
	if(!p || *p != '{') return nullptr;
	++p;
	for(;;){
		p = SkipJsonWhitespace(p);
		if(!p || !*p) return nullptr;
		if(*p == '}') return p + 1;
		p = SkipJsonStringRaw(p);
		if(!p) return nullptr;
		p = SkipJsonWhitespace(p);
		if(!p || *p != ':') return nullptr;
		p = SkipJsonValue(p + 1);
		if(!p) return nullptr;
		p = SkipJsonWhitespace(p);
		if(!p || !*p) return nullptr;
		if(*p == ','){
			++p;
			continue;
		}
		if(*p == '}') return p + 1;
		return nullptr;
	}
}
static const char* SkipJsonArrayRaw(const char* p){
	p = SkipJsonWhitespace(p);
	if(!p || *p != '[') return nullptr;
	++p;
	for(;;){
		p = SkipJsonWhitespace(p);
		if(!p || !*p) return nullptr;
		if(*p == ']') return p + 1;
		p = SkipJsonValue(p);
		if(!p) return nullptr;
		p = SkipJsonWhitespace(p);
		if(!p || !*p) return nullptr;
		if(*p == ','){
			++p;
			continue;
		}
		if(*p == ']') return p + 1;
		return nullptr;
	}
}
const char* SkipJsonValue(const char* p){
	p = SkipJsonWhitespace(p);
	if(!p || !*p) return nullptr;
	if(*p == '"') return SkipJsonStringRaw(p);
	if(*p == '{') return SkipJsonObjectRaw(p);
	if(*p == '[') return SkipJsonArrayRaw(p);
	while(*p && *p != ',' && *p != '}' && *p != ']' && !std::isspace(static_cast<unsigned char>(*p))) ++p;
	return p;
}
bool GetJsonPropertyRaw(const std::string& objectJson, const char* key, std::string& rawValue){
	const char* p = SkipJsonWhitespace(objectJson.c_str());
	if(!p || *p != '{') return false;
	++p;
	for(;;){
		p = SkipJsonWhitespace(p);
		if(!p || !*p || *p == '}') return false;
		std::string propertyName;
		if(!ReadJsonString(p, propertyName)) return false;
		p = SkipJsonWhitespace(p);
		if(!p || *p != ':') return false;
		const char* valueStart = SkipJsonWhitespace(p + 1);
		const char* valueEnd = SkipJsonValue(valueStart);
		if(!valueStart || !valueEnd) return false;
		if(propertyName == key){
			rawValue.assign(valueStart, static_cast<size_t>(valueEnd - valueStart));
			return true;
		}
		p = SkipJsonWhitespace(valueEnd);
		if(!p || !*p || *p == '}') return false;
		if(*p != ',') return false;
		++p;
	}
}
bool GetJsonStringProperty(const std::string& objectJson, const char* key, std::string& value){
	std::string rawValue;
	if(!GetJsonPropertyRaw(objectJson, key, rawValue)) return false;
	const char* p = rawValue.c_str();
	return ReadJsonString(p, value);
}
std::vector<std::string> ExtractJsonObjects(const char* json){
	std::vector<std::string> objects;
	const char* p = SkipJsonWhitespace(json);
	if(!p || !*p) return objects;
	if(*p == '{'){
		if(const char* end = SkipJsonObjectRaw(p)) objects.emplace_back(p, static_cast<size_t>(end - p));
		return objects;
	}
	if(*p != '[') return objects;
	++p;
	for(;;){
		p = SkipJsonWhitespace(p);
		if(!p || !*p || *p == ']') return objects;
		const char* valueEnd = SkipJsonValue(p);
		if(!valueEnd) return objects;
		if(*p == '{') objects.emplace_back(p, static_cast<size_t>(valueEnd - p));
		p = SkipJsonWhitespace(valueEnd);
		if(!p || !*p || *p == ']') return objects;
		if(*p != ',') return objects;
		++p;
	}
}
bool IsJsonNull(const std::string& rawValue){
	const char* p = SkipJsonWhitespace(rawValue.c_str());
	return p && std::strncmp(p, "null", 4) == 0;
}
std::string JsonEscape(const std::string& in){
	std::string escaped;
	escaped.reserve(in.size());
	constexpr char hex[] = "0123456789abcdef";
	for(const unsigned char c : in){
		switch(c){
			case '"': escaped += "\\\"";
				break;
			case '\\': escaped += "\\\\";
				break;
			case '\b': escaped += "\\b";
				break;
			case '\f': escaped += "\\f";
				break;
			case '\n': escaped += "\\n";
				break;
			case '\r': escaped += "\\r";
				break;
			case '\t': escaped += "\\t";
				break;
			default: if(c < 0x20){
					escaped += "\\u00";
					escaped += hex[c >> 4];
					escaped += hex[c & 0x0F];
				} else escaped += static_cast<char>(c);
				break;
		}
	}
	return escaped;
}
std::string JsonString(const std::string& in){ return "\"" + JsonEscape(in) + "\""; }
static std::string GetJsonValue(const char* json, const char* key){
	if(!json || !key) return "";
	std::string rawValue;
	if(!GetJsonPropertyRaw(std::string(json), key, rawValue)) return "";
	const char* p = rawValue.c_str();
	std::string stringValue;
	if(ReadJsonString(p, stringValue)) return stringValue;
	if(IsJsonNull(rawValue)) return "";
	const char* start = SkipJsonWhitespace(rawValue.c_str());
	if(!start) return "";
	const char* end = rawValue.c_str() + rawValue.size();
	while(end > start && std::isspace(static_cast<unsigned char>(*(end - 1)))) --end;
	return std::string(start, static_cast<size_t>(end - start));
}
std::string GetArgValue(const char* args, const char* key){
	if(!args || !key) return "";
	std::string paramTag = "<parameter=";
	paramTag += key;
	paramTag += ">";
	const char* p = std::strstr(args, paramTag.c_str());
	if(p){
		p += paramTag.size();
		const char* end = std::strstr(p, "</parameter>");
		if(end){
			while(*p == '\n' || *p == '\r') ++p;
			const char* e = end;
			while(e > p && (*(e - 1) == '\n' || *(e - 1) == '\r')) --e;
			return std::string(p, e);
		}
	}
	std::string openTag = "<";
	openTag += key;
	openTag += ">";
	p = std::strstr(args, openTag.c_str());
	if(p){
		p += openTag.size();
		std::string closeTag = "</";
		closeTag += key;
		closeTag += ">";
		const char* end = std::strstr(p, closeTag.c_str());
		if(end) return std::string(p, end);
	}
	return GetJsonValue(args, key);
}