#include <string>
#include <sstream>
#include <vector>
#include <algorithm>
#include <cctype>
#include <iomanip>
struct MarkdownState{
	bool inCodeBlock = false;
	bool firstLine = true;
};
static std::string EscapeRtf(const std::string& source){
	if(source.empty()) return "";
	std::stringstream sb;
	for(const char c : source){
		const unsigned char uc = static_cast<unsigned char>(c);
		if(uc>=0x20&&uc<0x80){
			if(c=='\\'||c=='{'||c=='}') sb<<'\\';
			sb<<c;
		} else if(uc<0x20||(uc>=0x80&&uc<=0xFF)){ sb<<"\\'"<<std::uppercase<<std::hex<<std::setw(2)<<std::setfill('0')<<static_cast<int>(uc)<<std::dec; } else{ sb<<"\\u"<<static_cast<int>(c)<<"?"; }
	}
	return sb.str();
}
// Helper: Remove leading whitespace.
static std::string trimStart(const std::string& s){
	size_t start = 0;
	while(start<s.size()&&std::isspace(static_cast<unsigned char>(s[start]))) start++;
	return s.substr(start);
}
// Helper: Check if a string starts with a given prefix.
static bool startsWith(const std::string& s, const std::string& prefix){ return s.substr(0, prefix.size())==prefix; }
// Helper: Check if the entire string is whitespace.
static bool isWhitespace(const std::string& s){ return std::all_of(s.begin(), s.end(), [](char c){ return std::isspace(static_cast<unsigned char>(c)); }); }
// Helper: Split a string by a given delimiter.
static std::vector<std::string> splitString(const std::string& s, char delimiter){
	std::vector<std::string> tokens;
	std::stringstream ss(s);
	std::string token;
	while(std::getline(ss, token, delimiter)){ tokens.push_back(token); }
	return tokens;
}
static std::string ProcessInlineMarkdown(const std::string& text){
	std::stringstream result;
	size_t pos = 0;
	while(pos<text.size()){
		// Lambda to validate if the marker is at a valid delimiter position.
		auto IsValidEmphasisDelimiter = [&](size_t position, char marker) -> bool{
			const bool validLeft = (position==0||std::isspace(static_cast<unsigned char>(text[position-1]))||std::ispunct(static_cast<unsigned char>(text[position-1])));
			const bool validRight = (position+1<text.size()&&!std::isspace(static_cast<unsigned char>(text[position+1])));
			return validLeft&&validRight;
		};
		if(pos+2<text.size()&&(((text[pos]=='*'&&text[pos+1]=='*'&&text[pos+2]=='*')||(text[pos]=='_'&&text[pos+1]=='_'&&text[pos+2]=='_'))&&IsValidEmphasisDelimiter(pos, text[pos]))){
			const char marker = text[pos];
			pos += 3;
			std::string markerTriple(3, marker);
			const size_t endPos = text.find(markerTriple, pos);
			if(endPos!=std::string::npos&&(endPos+3>=text.size()||std::isspace(static_cast<unsigned char>(text[endPos+3]))||std::ispunct(static_cast<unsigned char>(text[endPos+3])))){
				result<<R"(\b\i )"<<EscapeRtf(text.substr(pos, endPos-pos))<<R"(\b0\i0 )";
				pos = endPos+3;
				continue;
			}
			result<<EscapeRtf(markerTriple);
			continue;
		}
		if(pos+1<text.size()&&(((text[pos]=='*'&&text[pos+1]=='*')||(text[pos]=='_'&&text[pos+1]=='_'))&&IsValidEmphasisDelimiter(pos, text[pos]))){
			const char marker = text[pos];
			pos += 2;
			std::string markerDouble(2, marker);
			const size_t endPos = text.find(markerDouble, pos);
			if(endPos!=std::string::npos&&(endPos+2>=text.size()||std::isspace(static_cast<unsigned char>(text[endPos+2]))||std::ispunct(static_cast<unsigned char>(text[endPos+2])))){
				result<<R"(\b )"<<EscapeRtf(text.substr(pos, endPos-pos))<<R"(\b0 )";
				pos = endPos+2;
				continue;
			}
			result<<EscapeRtf(markerDouble);
			continue;
		}
		if(pos<text.size()&&(text[pos]=='*'||text[pos]=='_')&&IsValidEmphasisDelimiter(pos, text[pos])){
			const char marker = text[pos];
			pos++;
			const size_t endPos = text.find(marker, pos);
			if(endPos!=std::string::npos&&(endPos+1>=text.size()||std::isspace(static_cast<unsigned char>(text[endPos+1]))||std::ispunct(static_cast<unsigned char>(text[endPos+1])))){
				result<<R"(\i )"<<EscapeRtf(text.substr(pos, endPos-pos))<<R"(\i0 )";
				pos = endPos+1;
				continue;
			}
			result<<EscapeRtf(std::string(1, marker));
			continue;
		}
		if(pos+1<text.size()&&text[pos]=='~'&&text[pos+1]=='~'&&IsValidEmphasisDelimiter(pos, '~')){
			pos += 2;
			const size_t endPos = text.find("~~", pos);
			if(endPos!=std::string::npos&&(endPos+2>=text.size()||std::isspace(static_cast<unsigned char>(text[endPos+2]))||std::ispunct(static_cast<unsigned char>(text[endPos+2])))){
				result<<R"(\strike )"<<EscapeRtf(text.substr(pos, endPos-pos))<<R"(\strike0 )";
				pos = endPos+2;
				continue;
			}
			result<<EscapeRtf("~~");
			continue;
		}
		if(pos<text.size()&&text[pos]=='`'){
			pos++;
			const size_t endPos = text.find('`', pos);
			if(endPos!=std::string::npos){
				result<<EscapeRtf(text.substr(pos, endPos-pos));
				pos = endPos+1;
				continue;
			}
			result<<EscapeRtf("`");
			continue;
		}
		if(pos<text.size()&&text[pos]=='['&&(pos==0||std::isspace(static_cast<unsigned char>(text[pos-1])))){
			const size_t closeBracket = text.find(']', pos);
			if(closeBracket!=std::string::npos&&closeBracket>pos&&closeBracket+1<text.size()&&text[closeBracket+1]=='('){
				const size_t closeParenthesis = text.find(')', closeBracket+2);
				if(closeParenthesis!=std::string::npos){
					std::string linkText = text.substr(pos+1, closeBracket-pos-1);
					result<<R"(\ul )"<<EscapeRtf(linkText)<<R"(\ulnone )";
					pos = closeParenthesis+1;
					continue;
				}
				result<<EscapeRtf("[");
				pos++;
				continue;
			}
			result<<EscapeRtf("[");
			pos++;
			continue;
		}
		result<<EscapeRtf(std::string(1, text[pos]));
		pos++;
	}
	return result.str();
}
static bool TryProcessListItem(const std::string& trimmedLine, const std::string& leadingWhitespace, std::stringstream& rtf){
	if(startsWith(trimmedLine, "- ")||startsWith(trimmedLine, "* ")){
		//char marker = trimmedLine[0];
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
	if(!trimmedLine.empty()&&std::isdigit(trimmedLine[0])){
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
extern "C" __declspec(dllexport) void ConvertMarkdownToRtf(const char* markdown, const char* &rtfOut, int &rtfLen){
	const std::string rtfHeader = R"({\rtf1\ansi\ansicpg1252\deff0\nouicompat{\fonttbl{\f0\fnil Segoe UI;}}\viewkind4\uc1\pard\sa0\sl0\slmult1\f0\fs20 )";
	rtf.str(std::string());
	rtf.clear();
	rtfStr.clear();
	rtf<<rtfHeader;
	// Normalize newlines by removing '\r'
	std::string normalized(markdown);
	normalized.erase(std::remove(normalized.begin(), normalized.end(), '\r'), normalized.end());
	const std::vector<std::string> lines = splitString(normalized, '\n');
	// Find the first non-whitespace line.
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