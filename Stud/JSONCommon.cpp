#include "JSONCommon.h"
#include <algorithm>
#include <cctype>
#include <cstring>
#include <cstdlib>
#include <stdexcept>
#include <utility>
#ifndef EXPORT
#define EXPORT __declspec(dllexport)
#endif
struct JsonProperty{
	std::string name;
	std::string rawValue;
};
static std::string TrimCopy(std::string text){
	const auto begin = std::find_if_not(text.begin(), text.end(), [](unsigned char ch){ return std::isspace(ch); });
	if(begin == text.end()) return {};
	const auto end = std::find_if_not(text.rbegin(), text.rend(), [](unsigned char ch){ return std::isspace(ch); }).base();
	return std::string(begin, end);
}
const char* SkipJsonWhitespace(const char* p){
	while(p && *p && std::isspace(static_cast<unsigned char>(*p))) ++p;
	return p;
}
static int JsonHexValue(const char ch){
	if(ch >= '0' && ch <= '9') return ch - '0';
	if(ch >= 'a' && ch <= 'f') return ch - 'a' + 10;
	if(ch >= 'A' && ch <= 'F') return ch - 'A' + 10;
	return -1;
}
static bool ReadJsonUnicodeEscape(const char*& p, unsigned int& codepoint){
	codepoint = 0;
	for(int i = 0; i < 4; ++i){
		const int value = JsonHexValue(p[i]);
		if(value < 0) return false;
		codepoint = (codepoint << 4) | static_cast<unsigned int>(value);
	}
	p += 4;
	return true;
}
static void AppendUtf8Codepoint(std::string& value, const unsigned int codepoint){
	if(codepoint <= 0x7F) value += static_cast<char>(codepoint);
	else if(codepoint <= 0x7FF){
		value += static_cast<char>(0xC0 | (codepoint >> 6));
		value += static_cast<char>(0x80 | (codepoint & 0x3F));
	} else if(codepoint <= 0xFFFF){
		value += static_cast<char>(0xE0 | (codepoint >> 12));
		value += static_cast<char>(0x80 | ((codepoint >> 6) & 0x3F));
		value += static_cast<char>(0x80 | (codepoint & 0x3F));
	} else{
		value += static_cast<char>(0xF0 | (codepoint >> 18));
		value += static_cast<char>(0x80 | ((codepoint >> 12) & 0x3F));
		value += static_cast<char>(0x80 | ((codepoint >> 6) & 0x3F));
		value += static_cast<char>(0x80 | (codepoint & 0x3F));
	}
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
			case 'u': {
				unsigned int codepoint;
				if(!ReadJsonUnicodeEscape(p, codepoint)) return false;
				if(codepoint >= 0xD800 && codepoint <= 0xDBFF){
					if(p[0] != '\\' || p[1] != 'u') return false;
					p += 2;
					unsigned int lowSurrogate;
					if(!ReadJsonUnicodeEscape(p, lowSurrogate) || lowSurrogate < 0xDC00 || lowSurrogate > 0xDFFF) return false;
					codepoint = 0x10000 + ((codepoint - 0xD800) << 10) + (lowSurrogate - 0xDC00);
				} else if(codepoint >= 0xDC00 && codepoint <= 0xDFFF) return false;
				AppendUtf8Codepoint(value, codepoint);
				break;
			}
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
	if(!p || std::strncmp(p, "null", 4) != 0) return false;
	p = SkipJsonWhitespace(p + 4);
	return p && !*p;
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
static bool ReadJsonObjectProperties(const std::string& objectJson, std::vector<JsonProperty>& properties){
	properties.clear();
	const char* p = SkipJsonWhitespace(objectJson.c_str());
	if(!p || *p != '{') return false;
	++p;
	for(;;){
		p = SkipJsonWhitespace(p);
		if(!p || !*p) return false;
		if(*p == '}') return !*SkipJsonWhitespace(p + 1);
		JsonProperty property;
		if(!ReadJsonString(p, property.name)) return false;
		p = SkipJsonWhitespace(p);
		if(!p || *p != ':') return false;
		const char* valueStart = SkipJsonWhitespace(p + 1);
		const char* valueEnd = SkipJsonValue(valueStart);
		if(!valueStart || !valueEnd) return false;
		property.rawValue.assign(valueStart, static_cast<size_t>(valueEnd - valueStart));
		properties.push_back(std::move(property));
		p = SkipJsonWhitespace(valueEnd);
		if(!p || !*p) return false;
		if(*p == ','){
			++p;
			continue;
		}
		if(*p == '}') return !*SkipJsonWhitespace(p + 1);
		return false;
	}
}
static bool ReadJsonArrayValues(const std::string& arrayJson, std::vector<std::string>& values){
	values.clear();
	const char* p = SkipJsonWhitespace(arrayJson.c_str());
	if(!p || *p != '[') return false;
	++p;
	for(;;){
		p = SkipJsonWhitespace(p);
		if(!p || !*p) return false;
		if(*p == ']') return !*SkipJsonWhitespace(p + 1);
		const char* valueEnd = SkipJsonValue(p);
		if(!valueEnd) return false;
		values.emplace_back(p, static_cast<size_t>(valueEnd - p));
		p = SkipJsonWhitespace(valueEnd);
		if(!p || !*p) return false;
		if(*p == ','){
			++p;
			continue;
		}
		if(*p == ']') return !*SkipJsonWhitespace(p + 1);
		return false;
	}
}
static bool ReadJsonStringRaw(const std::string& rawValue, std::string& value){
	const char* p = rawValue.c_str();
	if(!ReadJsonString(p, value)) return false;
	p = SkipJsonWhitespace(p);
	return p && !*p;
}
static bool IsJsonObjectRaw(const std::string& rawValue){
	const char* p = SkipJsonWhitespace(rawValue.c_str());
	return p && *p == '{';
}
static bool IsJsonArrayRaw(const std::string& rawValue){
	const char* p = SkipJsonWhitespace(rawValue.c_str());
	return p && *p == '[';
}
static std::string RawJsonValueForDisplay(const std::string& rawValue);
static void AppendDisplayStringChar(std::string& output, char ch, char next){
	switch(ch){
		case '"': output += "\\\"";
			break;
		case '\\': output += "\\\\";
			break;
		case '\n': output += "\r\n";
			break;
		case '\r': if(next != '\n') output += "\r\n";
			break;
		case '\t': output += "\\t";
			break;
		case '\b': output += "\\b";
			break;
		case '\f': output += "\\f";
			break;
		default: output += ch;
			break;
	}
}
static std::string DisplayJsonString(const std::string& value){
	std::string output = "\"";
	for(size_t i = 0; i < value.size(); ++i) AppendDisplayStringChar(output, value[i], i + 1 < value.size() ? value[i + 1] : '\0');
	output += "\"";
	return output;
}
static std::string IndentMultilineValue(const std::string& value, const std::string& indent){
	std::string output;
	output.reserve(value.size() + indent.size() * 2);
	for(size_t i = 0; i < value.size(); ++i){
		output += value[i];
		if(value[i] == '\n' && i + 1 < value.size()) output += indent;
	}
	return output;
}
std::string FormatJsonForDisplayText(const char* json){
	const std::string raw = TrimCopy(json ? std::string(json) : std::string());
	if(raw.empty()) return {};
	if(IsJsonObjectRaw(raw)){
		std::vector<JsonProperty> properties;
		if(!ReadJsonObjectProperties(raw, properties)) return raw;
		if(properties.empty()) return "{ }";
		std::string output = "{";
		const std::string indent = "  ";
		for(size_t i = 0; i < properties.size(); ++i){
			const auto& property = properties[i];
			const auto value = RawJsonValueForDisplay(property.rawValue);
			output += "\r\n";
			output += indent;
			output += DisplayJsonString(property.name);
			output += ": ";
			output += IndentMultilineValue(value, indent);
			if(i + 1 < properties.size()) output += ",";
		}
		output += "\r\n}";
		return output;
	}
	if(IsJsonArrayRaw(raw)){
		std::vector<std::string> values;
		if(!ReadJsonArrayValues(raw, values)) return raw;
		if(values.empty()) return "[ ]";
		std::string output = "[";
		const std::string indent = "  ";
		for(size_t i = 0; i < values.size(); ++i){
			const auto& valueRaw = values[i];
			const auto value = RawJsonValueForDisplay(valueRaw);
			output += "\r\n";
			output += indent;
			output += IndentMultilineValue(value, indent);
			if(i + 1 < values.size()) output += ",";
		}
		output += "\r\n]";
		return output;
	}
	return RawJsonValueForDisplay(raw);
}
static std::string RawJsonValueForDisplay(const std::string& rawValue){
	const std::string raw = TrimCopy(rawValue);
	std::string stringValue;
	if(ReadJsonStringRaw(raw, stringValue)) return DisplayJsonString(stringValue);
	if(IsJsonNull(raw)) return "null";
	if(IsJsonObjectRaw(raw) || IsJsonArrayRaw(raw)) return FormatJsonForDisplayText(raw.c_str());
	return raw;
}
std::string FormatToolOutputForDisplayText(const char* result){
	const std::string raw = TrimCopy(result ? std::string(result) : std::string());
	if(raw.empty()) return {};
	if(IsJsonObjectRaw(raw) || IsJsonArrayRaw(raw)) return FormatJsonForDisplayText(raw.c_str());
	std::string decodedString;
	if(ReadJsonStringRaw(raw, decodedString)) return decodedString;
	return RawJsonValueForDisplay(raw);
}
std::string FormatToolCallForDisplayText(const char* message){
	const std::string text = message ? std::string(message) : std::string();
	const std::string marker = "Tool arguments:";
	const auto index = text.find(marker);
	if(index == std::string::npos) return text;
	const auto argsStart = index + marker.size();
	const auto args = TrimCopy(text.substr(argsStart));
	if(args.empty()) return text;
	return TrimCopy(text.substr(0, argsStart)) + "\r\n" + FormatJsonForDisplayText(args.c_str());
}
static void AppendJsonIndent(std::string& output, const int depth){
	output.append(static_cast<size_t>(depth * 2), ' ');
}
static bool AppendNormalizedJsonValue(const char*& p, std::string& output, bool indented, int depth);
static bool AppendNormalizedJsonObject(const char*& p, std::string& output, const bool indented, const int depth){
	p = SkipJsonWhitespace(p);
	if(!p || *p != '{') return false;
	output += '{';
	++p;
	p = SkipJsonWhitespace(p);
	if(!p || !*p) return false;
	if(*p == '}'){
		output += '}';
		++p;
		return true;
	}
	for(;;){
		if(indented){
			output += '\n';
			AppendJsonIndent(output, depth + 1);
		}
		p = SkipJsonWhitespace(p);
		const char* nameStart = p;
		const char* nameEnd = SkipJsonStringRaw(p);
		if(!nameEnd) return false;
		output.append(nameStart, static_cast<size_t>(nameEnd - nameStart));
		p = SkipJsonWhitespace(nameEnd);
		if(!p || *p != ':') return false;
		output += indented ? ": " : ":";
		++p;
		if(!AppendNormalizedJsonValue(p, output, indented, depth + 1)) return false;
		p = SkipJsonWhitespace(p);
		if(!p || !*p) return false;
		if(*p == '}'){
			if(indented){
				output += '\n';
				AppendJsonIndent(output, depth);
			}
			output += '}';
			++p;
			return true;
		}
		if(*p != ',') return false;
		output += ',';
		++p;
	}
}
static bool AppendNormalizedJsonArray(const char*& p, std::string& output, const bool indented, const int depth){
	p = SkipJsonWhitespace(p);
	if(!p || *p != '[') return false;
	output += '[';
	++p;
	p = SkipJsonWhitespace(p);
	if(!p || !*p) return false;
	if(*p == ']'){
		output += ']';
		++p;
		return true;
	}
	for(;;){
		if(indented){
			output += '\n';
			AppendJsonIndent(output, depth + 1);
		}
		if(!AppendNormalizedJsonValue(p, output, indented, depth + 1)) return false;
		p = SkipJsonWhitespace(p);
		if(!p || !*p) return false;
		if(*p == ']'){
			if(indented){
				output += '\n';
				AppendJsonIndent(output, depth);
			}
			output += ']';
			++p;
			return true;
		}
		if(*p != ',') return false;
		output += ',';
		++p;
	}
}
static bool IsJsonPrimitiveRawValid(const std::string& raw){
	if(raw == "true" || raw == "false" || raw == "null") return true;
	if(raw.empty()) return false;
	size_t i = 0;
	if(raw[i] == '-'){
		++i;
		if(i == raw.size()) return false;
	}
	if(raw[i] == '0'){
		++i;
	} else if(raw[i] >= '1' && raw[i] <= '9'){
		do{ ++i; } while(i < raw.size() && raw[i] >= '0' && raw[i] <= '9');
	} else return false;
	if(i < raw.size() && raw[i] == '.'){
		++i;
		const size_t fractionStart = i;
		while(i < raw.size() && raw[i] >= '0' && raw[i] <= '9') ++i;
		if(i == fractionStart) return false;
	}
	if(i < raw.size() && (raw[i] == 'e' || raw[i] == 'E')){
		++i;
		if(i < raw.size() && (raw[i] == '+' || raw[i] == '-')) ++i;
		const size_t exponentStart = i;
		while(i < raw.size() && raw[i] >= '0' && raw[i] <= '9') ++i;
		if(i == exponentStart) return false;
	}
	return i == raw.size();
}
static bool AppendNormalizedJsonValue(const char*& p, std::string& output, const bool indented, const int depth){
	p = SkipJsonWhitespace(p);
	if(!p || !*p) return false;
	if(*p == '{') return AppendNormalizedJsonObject(p, output, indented, depth);
	if(*p == '[') return AppendNormalizedJsonArray(p, output, indented, depth);
	if(*p == '"'){
		const char* end = SkipJsonStringRaw(p);
		if(!end) return false;
		output.append(p, static_cast<size_t>(end - p));
		p = end;
		return true;
	}
	const char* start = p;
	const char* end = SkipJsonValue(p);
	if(!end || end == start) return false;
	std::string raw(start, static_cast<size_t>(end - start));
	raw = TrimCopy(raw);
	if(!IsJsonPrimitiveRawValid(raw)) return false;
	output += raw;
	p = end;
	return true;
}
std::string NormalizeJsonText(const char* json, const bool indented){
	const char* p = SkipJsonWhitespace(json);
	if(!p || !*p) return {};
	std::string output;
	if(!AppendNormalizedJsonValue(p, output, indented, 0)) throw std::invalid_argument("Invalid JSON");
	p = SkipJsonWhitespace(p);
	if(p && *p) throw std::invalid_argument("Trailing JSON content");
	return output;
}
static char* CopyDisplayCString(const std::string& text){
	auto* buffer = static_cast<char*>(std::malloc(text.size() + 1));
	if(!buffer) return nullptr;
	std::memcpy(buffer, text.data(), text.size());
	buffer[text.size()] = '\0';
	return buffer;
}
extern "C" EXPORT char* FormatJsonForDisplay(const char* json){ try{ return CopyDisplayCString(FormatJsonForDisplayText(json)); } catch(...){ return CopyDisplayCString(json ? json : ""); } }
extern "C" EXPORT char* FormatToolOutputForDisplay(const char* result){ try{ return CopyDisplayCString(FormatToolOutputForDisplayText(result)); } catch(...){ return CopyDisplayCString(result ? result : ""); } }
extern "C" EXPORT char* FormatToolCallForDisplay(const char* message){ try{ return CopyDisplayCString(FormatToolCallForDisplayText(message)); } catch(...){ return CopyDisplayCString(message ? message : ""); } }
extern "C" EXPORT char* JsonPretty(const char* json){ try{ return CopyDisplayCString(NormalizeJsonText(json, true)); } catch(...){ return CopyDisplayCString(""); } }
extern "C" EXPORT int JsonGetKind(const char* json){
	const char* p = SkipJsonWhitespace(json);
	if(!p || !*p) return 0;
	int kind = 0;
	if(*p == '{') kind = 5;
	else if(*p == '[') kind = 6;
	else if(*p == '"') kind = 4;
	else if(std::strncmp(p, "null", 4) == 0) kind = 1;
	else if(std::strncmp(p, "true", 4) == 0 || std::strncmp(p, "false", 5) == 0) kind = 2;
	else if(*p == '-' || (*p >= '0' && *p <= '9')) kind = 3;
	if(kind == 0) return 0;
	const char* end = SkipJsonValue(p);
	if(!end || end == p) return 0;
	if(kind == 3){
		const std::string raw(p, static_cast<size_t>(end - p));
		if(!IsJsonPrimitiveRawValid(raw)) return 0;
	}
	end = SkipJsonWhitespace(end);
	if(end && *end) return 0;
	return kind;
}
extern "C" EXPORT char* JsonGetProperty(const char* objectJson, const char* key){
	try{
		std::string rawValue;
		if(!objectJson || !key || !GetJsonPropertyRaw(std::string(objectJson), key, rawValue)) return nullptr;
		return CopyDisplayCString(rawValue);
	} catch(...){ return nullptr; }
}
extern "C" EXPORT char* JsonObjectKeys(const char* objectJson){
	try{
		std::vector<JsonProperty> properties;
		if(!objectJson || !ReadJsonObjectProperties(std::string(objectJson), properties)) return nullptr;
		std::string json = "[";
		for(size_t i = 0; i < properties.size(); ++i){
			if(i > 0) json += ",";
			json += JsonString(properties[i].name);
		}
		json += "]";
		return CopyDisplayCString(json);
	} catch(...){ return nullptr; }
}
extern "C" EXPORT int JsonArrayCount(const char* arrayJson){
	try{
		std::vector<std::string> values;
		if(!arrayJson || !ReadJsonArrayValues(std::string(arrayJson), values)) return -1;
		return static_cast<int>(values.size());
	} catch(...){ return -1; }
}
extern "C" EXPORT char* JsonArrayItem(const char* arrayJson, const int index){
	try{
		std::vector<std::string> values;
		if(index < 0 || !arrayJson || !ReadJsonArrayValues(std::string(arrayJson), values) || static_cast<size_t>(index) >= values.size()) return nullptr;
		return CopyDisplayCString(values[static_cast<size_t>(index)]);
	} catch(...){ return nullptr; }
}
extern "C" EXPORT char* JsonReadStringValue(const char* rawValue){
	try{
		if(!rawValue) return nullptr;
		std::string value;
		if(!ReadJsonStringRaw(std::string(rawValue), value)) return nullptr;
		return CopyDisplayCString(value);
	} catch(...){ return nullptr; }
}
extern "C" EXPORT char* JsonMakeString(const char* value){
	try{ return CopyDisplayCString(JsonString(value ? std::string(value) : std::string())); }
	catch(...){ return CopyDisplayCString("\"\""); }
}
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
