#include <string>
#include <sstream>
#include <vector>
#include <algorithm>
#include <cctype>
#include <iomanip>
#include <iostream>
#include <array>
#include <charconv>
struct MarkdownState{
	bool inCodeBlock = false;
	bool firstLine = true;
};
static constexpr std::array<char, 16> kHex = {
    '0','1','2','3','4','5','6','7',
    '8','9','A','B','C','D','E','F'
};
std::string EscapeRtf(const std::string& utf8){
    if (utf8.empty()) return {};
    std::string out;
    out.reserve(utf8.size() * 4);
    for (std::size_t i = 0; i < utf8.size(); ){
        uint32_t cp = 0;
        unsigned char b = utf8[i];
        if (b < 0x80) { cp = b; ++i; }
        else if ((b & 0xE0) == 0xC0 && i + 1 < utf8.size()) {
            cp = ((b & 0x1F) << 6) | (utf8[i + 1] & 0x3F); i += 2;
        } else if ((b & 0xF0) == 0xE0 && i + 2 < utf8.size()) {
            cp = ((b & 0x0F) << 12) | ((utf8[i + 1] & 0x3F) << 6) | (utf8[i + 2] & 0x3F); i += 3;
        } else if ((b & 0xF8) == 0xF0 && i + 3 < utf8.size()) {
            cp = ((b & 0x07) << 18) | ((utf8[i + 1] & 0x3F) << 12) | ((utf8[i + 2] & 0x3F) << 6) | (utf8[i + 3] & 0x3F); i += 4;
        } else { ++i; continue; }
        if (cp < 0x20 || (cp >= 0x80 && cp < 0x100)){
            out += "\\'";
            out += kHex[(cp >> 4) & 0xF];
            out += kHex[cp & 0xF];
        }
        else if (cp < 0x80){
            char ch = static_cast<char>(cp);
            if (ch == '\\' || ch == '{' || ch == '}') out += '\\';
            out += ch;
        }
        else{
            out += "\\u";
            char buf[12];
            auto [ptr, ec] = std::to_chars(buf, buf + sizeof(buf), cp);
            out.append(buf, ptr);
            out += '?';
        }
    }
    return out;
}
static std::string trimStart(const std::string& s){
	size_t start = 0;
	while(start<s.size()&&std::isspace(static_cast<unsigned char>(s[start]))) start++;
	return s.substr(start);
}
static bool startsWith(const std::string& s, const std::string& prefix){ return s.substr(0, prefix.size())==prefix; }
static bool isWhitespace(const std::string& s){ return std::all_of(s.begin(), s.end(), [](char c){ return std::isspace(static_cast<unsigned char>(c)); }); }
static std::vector<std::string> splitString(const std::string& s, char delimiter){
	std::vector<std::string> tokens;
	std::stringstream ss(s);
	std::string token;
	while(std::getline(ss, token, delimiter)){ tokens.push_back(token); }
	return tokens;
}
static std::string ProcessInlineMarkdown(const std::string& text){
    std::stringstream result;
    std::string buffer;
    size_t pos = 0;
    const auto flushPlain = [&] {
        if (!buffer.empty()) {
            result << EscapeRtf(buffer);
            buffer.clear();
        }
    };
    const auto utf8CharLen = [&](size_t i)->size_t {
        unsigned char b = static_cast<unsigned char>(text[i]);
        if (b < 0x80) return 1;
        if ((b & 0xE0) == 0xC0) return 2;
        if ((b & 0xF0) == 0xE0) return 3;
        if ((b & 0xF8) == 0xF0) return 4;
        return 1;
    };
    const auto isValidEmph = [&](size_t p, char m)->bool {
        bool left = (p == 0 || std::isspace((unsigned char)text[p - 1]) || std::ispunct((unsigned char)text[p - 1]));
        bool right = (p + 1 < text.size() && !std::isspace((unsigned char)text[p + 1]));
        return left && right;
    };
    while (pos < text.size()){
        if (pos + 2 < text.size() &&
            ((text[pos] == '*' && text[pos + 1] == '*' && text[pos + 2] == '*') || (text[pos] == '_' && text[pos + 1] == '_' && text[pos + 2] == '_')) &&
            isValidEmph(pos, text[pos])){
            flushPlain();
            char marker = text[pos];
            pos += 3;
            std::string triple(3, marker);
            size_t end = text.find(triple, pos);
            if (end != std::string::npos &&
                (end + 3 >= text.size() || std::isspace((unsigned char)text[end + 3]) || std::ispunct((unsigned char)text[end + 3]))){
                result << R"(\b\i )" << EscapeRtf(text.substr(pos, end - pos)) << R"(\b0\i0 )";
                pos = end + 3;
                continue;
            }
            buffer += triple;
            continue;
        }
        if (pos + 1 < text.size() &&
            ((text[pos] == '*' && text[pos + 1] == '*') ||
                (text[pos] == '_' && text[pos + 1] == '_')) &&
            isValidEmph(pos, text[pos])){
            flushPlain();
            char marker = text[pos];
            pos += 2;
            std::string dbl(2, marker);
            size_t end = text.find(dbl, pos);
            if (end != std::string::npos &&
                (end + 2 >= text.size() || std::isspace((unsigned char)text[end + 2]) || std::ispunct((unsigned char)text[end + 2]))){
                result << R"(\b )" << EscapeRtf(text.substr(pos, end - pos)) << R"(\b0 )";
                pos = end + 2;
                continue;
            }
            buffer += dbl;
            continue;
        }
        if ((text[pos] == '*' || text[pos] == '_') && isValidEmph(pos, text[pos])){
            flushPlain();
            char marker = text[pos++];
            size_t end = text.find(marker, pos);
            if (end != std::string::npos && (end + 1 >= text.size() || std::isspace((unsigned char)text[end + 1]) || std::ispunct((unsigned char)text[end + 1]))){
                result << R"(\i )" << EscapeRtf(text.substr(pos, end - pos)) << R"(\i0 )";
                pos = end + 1;
                continue;
            }
            buffer += marker;
            continue;
        }
        if (pos + 1 < text.size() && text[pos] == '~' && text[pos + 1] == '~' && isValidEmph(pos, '~')){
            flushPlain();
            pos += 2;
            size_t end = text.find("~~", pos);
            if (end != std::string::npos &&
                (end + 2 >= text.size() || std::isspace((unsigned char)text[end + 2]) || std::ispunct((unsigned char)text[end + 2]))){
                result << R"(\strike )" << EscapeRtf(text.substr(pos, end - pos)) << R"(\strike0 )";
                pos = end + 2;
                continue;
            }
            buffer += "~~";
            continue;
        }
        if (text[pos] == '`'){
            flushPlain();
            ++pos;
            size_t end = text.find('`', pos);
            if (end != std::string::npos) {
                result << EscapeRtf(text.substr(pos, end - pos));
                pos = end + 1;
            }
            else { buffer += '`'; }
            continue;
        }
        if (text[pos] == '[' && (pos == 0 || std::isspace((unsigned char)text[pos - 1]))){
            size_t rb = text.find(']', pos);
            if (rb != std::string::npos && rb + 1 < text.size() && text[rb + 1] == '('){
                size_t rp = text.find(')', rb + 2);
                if (rp != std::string::npos){
                    flushPlain();
                    std::string linkText = text.substr(pos + 1, rb - pos - 1);
                    result << R"(\ul )" << EscapeRtf(linkText) << R"(\ulnone )";
                    pos = rp + 1;
                    continue;
                }
            }
        }
        size_t len = utf8CharLen(pos);
        buffer.append(text, pos, len);
        pos += len;
    }
    flushPlain();
    return result.str();
}
static bool TryProcessListItem(const std::string& trimmedLine, const std::string& leadingWhitespace, std::stringstream& rtf){
	if(startsWith(trimmedLine, "- ")||startsWith(trimmedLine, "* ")){
		const std::string content = ProcessInlineMarkdown(trimmedLine.substr(2));
		rtf<<leadingWhitespace<<R"(\bullet  )"<<content;
		return true;
	}
	return false;
}
struct HeadingStyle{
	std::string prefix;
	std::string style;
	std::string endStyle;
};
static bool TryProcessHeading(const std::string& trimmedLine, std::stringstream& rtf){
	const std::vector<HeadingStyle> headingStyles = {
		{"# ", R"(\b\ul\fs32 )", R"(\b0\ulnone\fs20)"},
		{"## ", R"(\b\ul\fs28 )", R"(\b0\ulnone\fs20)"},
		{"### ", R"(\b\fs24 )", R"(\b0\fs20)"},
		{"#### ", R"(\b\fs20 )", R"(\b0\fs20)"},
		{"##### ", R"(\b\fs18 )", R"(\b0\fs20)"},
		{"###### ", R"(\b\fs16 )", R"(\b0\fs20)"}
	};
	for(const auto& style : headingStyles){
		if(startsWith(trimmedLine, style.prefix)){
			const std::string content = ProcessInlineMarkdown(trimmedLine.substr(style.prefix.size()));
			rtf<<"{"<<style.style<<content<<style.endStyle<<"} ";
			return true;
		}
	}
	return false;
}
static bool TryProcessNumberedList(const std::string& trimmedLine, std::stringstream& rtf){
	if(!trimmedLine.empty() && trimmedLine[0] > 0 && std::isdigit(trimmedLine[0])){
		const size_t dotIndex = trimmedLine.find(". ");
		if(dotIndex!=std::string::npos&&dotIndex>0){
			const std::string numberPart = trimmedLine.substr(0, dotIndex+1);
			const std::string textPart = trimmedLine.substr(dotIndex+2);
			rtf<<EscapeRtf(numberPart)<<" "<<ProcessInlineMarkdown(textPart);
			return true;
		}
	}
	return false;
}
static void ProcessMarkdownLine(const std::string& line, std::stringstream& rtf){
	const std::string trimmedLine = trimStart(line);
	const std::string leadingWhitespace = line.substr(0, line.size()-trimmedLine.size());
	if(TryProcessHeading(trimmedLine, rtf)||TryProcessListItem(trimmedLine, leadingWhitespace, rtf)||TryProcessNumberedList(trimmedLine, rtf)){ return; }
	rtf<<ProcessInlineMarkdown(line);
}
static void ProcessLine(const std::string& line, std::stringstream& rtf, MarkdownState& state){
	if(isWhitespace(line)){
		rtf<<R"(\line )";
		return;
	}
	const std::string trimmed = trimStart(line);
	if(startsWith(trimmed, "```")){
		state.inCodeBlock = !state.inCodeBlock;
		return;
	}
	if(!state.firstLine){ rtf<<R"(\line )"; }
	state.firstLine = false;
	if(state.inCodeBlock){
		rtf<<EscapeRtf(line);
		return;
	}
	ProcessMarkdownLine(line, rtf);
}
std::stringstream rtf;
std::string rtfStr;
extern "C" __declspec(dllexport) void ConvertMarkdownToRtf(const char* markdown, const char* & rtfOut, int& rtfLen){
	const std::string rtfHeader = R"({\rtf1\ansi\ansicpg1252\deff0\nouicompat{\fonttbl{\f0\fnil Segoe UI;}}\viewkind4\uc1\pard\sa0\sl0\slmult1\f0\fs20 )";
	rtf.str(std::string());
	rtf.clear();
	rtfStr.clear();
	rtf<<rtfHeader;
	std::string normalized(markdown);
	normalized.erase(std::remove(normalized.begin(), normalized.end(), '\r'), normalized.end());
	const std::vector<std::string> lines = splitString(normalized, '\n');
	size_t startIndex = 0;
	while(startIndex<lines.size()&&isWhitespace(lines[startIndex])){ ++startIndex; }
	if(startIndex==lines.size()){
		rtf<<"}";
		rtfStr = rtf.str();
		rtfOut = rtfStr.c_str();
		rtfLen = rtfStr.size();
	}
	MarkdownState state;
	for(size_t i = startIndex; i<lines.size(); ++i){ ProcessLine(lines[i], rtf, state); }
	rtf<<"}";
	rtfStr = rtf.str();
	rtfOut = rtfStr.c_str();
	rtfLen = rtfStr.size();
}