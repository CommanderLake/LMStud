#include "tools.h"
#include "JSONCommon.h"
#include <charconv>
#include <filesystem>
#include <sstream>
#include <fstream>
#include <regex>
void SetFileBaseDir(const char* dir){
	if(dir && *dir){
		std::error_code ec;
		_baseFolder = std::filesystem::weakly_canonical(dir, ec);
		if(ec) _baseFolder.clear();
	} else{ _baseFolder.clear(); }
}
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
std::string ListDirectoryTool(const char* argsJson){
	std::string path = GetArgValue(argsJson, "path");
	const std::string recStr = GetArgValue(argsJson, "recursive");
	const bool recursive = recStr == "true" || recStr == "1";
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
	if(ec) return "{\"error\":\"folder not found, maybe its a file\"}";
	std::string json = "{\"entries\":[";
	for(size_t i = 0; i < files.size(); ++i){
		json += "\"" + JsonEscape(files[i]) + "\"";
		if(i + 1 < files.size()) json += ",";
	}
	json += "]}";
	return json;
}
std::string ReadFileTool(const char* argsJson){
	std::string path = GetArgValue(argsJson, "path");
	const std::string startStr = GetArgValue(argsJson, "start");
	const std::string endStr = GetArgValue(argsJson, "end");
	int start = -1, end = -1;
	if(!startStr.empty()) std::from_chars(startStr.data(), startStr.data() + startStr.size(), start);
	if(!endStr.empty()) std::from_chars(endStr.data(), endStr.data() + endStr.size(), end);
	std::filesystem::path p = _baseFolder / path;
	if(!IsPathAllowed(p)) return "{\"error\":\"invalid path\"}";
	if(is_directory(p)) return "{\"error\":\"path is a folder\"}";
	std::ifstream f(p);
	if(!f.is_open()) return "{\"error\":\"open failed, try the list_directory tool\"}";
	std::string line, body;
	int lineNo = 1;
	while(std::getline(f, line)){
		if((start == -1 || lineNo >= start) && (end == -1 || lineNo <= end)){
			body += std::to_string(lineNo);
			body += ":";
			body += line;
			body += '\n';
		}
		if(end != -1 && lineNo >= end) break;
		++lineNo;
	}
	const std::string fileName = p.filename().string();
	const std::string ext = p.extension().string();
	std::string contentType;
	if(ext == ".h" || ext == ".cpp" || ext == ".cc" || ext == ".cxx" || ext == ".c") contentType = "cpp";
	else if(ext == ".cs") contentType = "csharp";
	else if(ext == ".py") contentType = "python";
	else if(ext == ".java") contentType = "java";
	else if(ext == ".js" || ext == ".jsx") contentType = "javascript";
	else if(ext == ".html" || ext == ".htm") contentType = "html";
	else if(ext == ".css") contentType = "css";
	else if(ext == ".xml") contentType = "xml";
	else if(ext == ".json") contentType = "json";
	else if(ext == ".md") contentType = "markdown";
	else if(ext == ".rb") contentType = "ruby";
	else if(ext == ".php") contentType = "php";
	else if(ext == ".swift") contentType = "swift";
	else if(ext == ".go") contentType = "go";
	else if(ext == ".rs") contentType = "rust";
	else if(ext == ".ts" || ext == ".tsx") contentType = "typescript";
	else if(ext == ".sql") contentType = "sql";
	else if(ext == ".sh") contentType = "bash";
	else contentType = "";
	std::string out = "[FILE] " + fileName + "\n```" + contentType + "\n" + body + "```\n";
	return out;
}
std::string SearchFileTool(const char* argsJson){
	const std::string path = GetArgValue(argsJson, "path");
	const std::string keyword = GetArgValue(argsJson, "keyword");
	const std::string maxResultsStr = GetArgValue(argsJson, "max_results");
	const std::string caseSensitiveStr = GetArgValue(argsJson, "case_sensitive");
	const std::string recursiveStr = GetArgValue(argsJson, "recursive");
	const std::string useRegexStr = GetArgValue(argsJson, "use_regex");
	const bool caseSensitive = !(caseSensitiveStr == "false" || caseSensitiveStr == "0");
	const bool recursive = recursiveStr == "true" || recursiveStr == "1";
	const bool useRegex = useRegexStr == "true" || useRegexStr == "1";
	int maxResults = 100;
	if(!maxResultsStr.empty()){
		auto[ptr, ec] = std::from_chars(maxResultsStr.data(), maxResultsStr.data() + maxResultsStr.size(), maxResults);
		if(ec != std::errc() || ptr != maxResultsStr.data() + maxResultsStr.size() || maxResults <= 0){ return "{\"error\":\"max_results must be a valid positive integer\"}"; }
	}
	if(keyword.empty()){ return "{\"error\":\"keyword is required\"}"; }
	const std::filesystem::path p = _baseFolder / path;
	if(!IsPathAllowed(p)) return "{\"error\":\"invalid path\"}";
	const bool isDir = is_directory(p);
	if(!isDir && !exists(p)){ return "{\"error\":\"file or directory not found\"}"; }
	std::string json = "{\"matches\":[";
	auto trim = [](const std::string& s){
		const auto start = s.find_first_not_of(" \t\r\n");
		if(start == std::string::npos) return std::string();
		const auto end = s.find_last_not_of(" \t\r\n");
		return s.substr(start, end - start + 1);
	};
	auto toLower = [](std::string s){
		std::transform(s.begin(), s.end(), s.begin(), [](unsigned char c){ return static_cast<char>(std::tolower(c, std::locale())); });
		return s;
	};
	std::vector<std::string> keywords;
	if(!useRegex){
		size_t start = 0;
		while(start <= keyword.size()){
			const size_t sep = keyword.find('|', start);
			const size_t len = sep == std::string::npos ? keyword.size() - start : sep - start;
			const std::string token = trim(keyword.substr(start, len));
			if(!token.empty()){ keywords.push_back(caseSensitive ? token : toLower(token)); }
			if(sep == std::string::npos) break;
			start = sep + 1;
		}
		if(keywords.empty()) return "{\"error\":\"keyword is required\"}";
	}
	std::regex kwRegex;
	if(useRegex){
		try{
			auto flags = std::regex_constants::ECMAScript;
			if(!caseSensitive) flags |= std::regex_constants::icase;
			kwRegex = std::regex(keyword, flags);
		} catch(const std::regex_error&){
			return "{\"error\":\"invalid regex keyword\"}";
		}
	}
	int matches = 0;
	auto processFile = [&](const std::filesystem::path& filePath, const std::string& fileLabel)-> bool{
		std::ifstream f(filePath);
		if(!f.is_open()){
			return false;
		}
		std::string line;
		int lineNo = 1;
		while(std::getline(f, line)){
			bool lineMatched = false;
			if(useRegex){
				lineMatched = std::regex_search(line, kwRegex);
			} else{
				const std::string lineSearch = caseSensitive ? line : toLower(line);
				for(const auto& kwSearch : keywords){
					if(lineSearch.find(kwSearch) != std::string::npos){
						lineMatched = true;
						break;
					}
				}
			}
			if(lineMatched){
				json += "{\"line\":" + std::to_string(lineNo) + ",\"text\":\"" + JsonEscape(line) + "\"";
				if(!fileLabel.empty()) json += ",\"file\":\"" + JsonEscape(fileLabel) + "\"";
				json += "},";
				++matches;
				if(matches >= maxResults) return true;
			}
			++lineNo;
		}
		return false;
	};
	if(isDir){
		std::error_code ec;
		if(recursive){
			for(const auto& entry : std::filesystem::recursive_directory_iterator(p, ec)){
				if(ec) break;
				if(!entry.is_regular_file(ec)) continue;
				const std::string fileLabel = relative(entry.path(), _baseFolder, ec).generic_string();
				if(processFile(entry.path(), fileLabel)) break;
			}
		} else{
			for(const auto& entry : std::filesystem::directory_iterator(p, ec)){
				if(ec) break;
				if(!entry.is_regular_file(ec)) continue;
				const std::string fileLabel = relative(entry.path(), _baseFolder, ec).generic_string();
				if(processFile(entry.path(), fileLabel)) break;
			}
		}
		if(ec) return "{\"error\":\"failed to iterate directory\"}";
	} else{
		std::ifstream f(p);
		if(!f.is_open()) return "{\"error\":\"failed to read file\"}";
		f.close();
		processFile(p, "");
	}
	if(json.back() == ',') json.pop_back();
	json += "]}";
	return json;
}
std::string CreateFileTool(const char* argsJson){
	const std::string path = GetArgValue(argsJson, "path");
	const std::string text = GetArgValue(argsJson, "text");
	const std::string overwriteStr = GetArgValue(argsJson, "overwrite");
	const bool overwrite = overwriteStr == "true" || overwriteStr == "1";
	const std::filesystem::path p = _baseFolder / path;
	if(!IsPathAllowed(p)) return "{\"error\":\"invalid path\"}";
	if(exists(p) && !overwrite) return "{\"error\":\"exists\"}";
	create_directories(p.parent_path());
	std::ofstream f(p);
	if(!f.is_open()) return "{\"error\":\"open failed\"}";
	f << text;
	return "{\"result\":\"success\"}";
}
std::string ReplaceLinesTool(const char* argsJson){
	std::string path = GetArgValue(argsJson, "path");
	const std::string startStr = GetArgValue(argsJson, "start");
	const std::string endStr = GetArgValue(argsJson, "end");
	std::string text = GetArgValue(argsJson, "text");
	int start = -1, end = -1;
	if(!startStr.empty()) std::from_chars(startStr.data(), startStr.data() + startStr.size(), start);
	if(!endStr.empty()) std::from_chars(endStr.data(), endStr.data() + endStr.size(), end);
	else end = start;
	if(start < 1 || end < start) return "{\"error\":\"range, check line numbers using file_read_lines\"}";
	std::filesystem::path p = _baseFolder / path;
	if(!IsPathAllowed(p)) return "{\"error\":\"invalid path\"}";
	std::ifstream in(p);
	if(!in.is_open()) return "{\"error\":\"open failed\"}";
	std::vector<std::string> lines;
	std::string line;
	while(std::getline(in, line)){
		if(!line.empty() && line.back() == '\r') line.pop_back();
		lines.push_back(line);
	}
	in.close();
	if(start > static_cast<int>(lines.size()) || end > static_cast<int>(lines.size())) return "{\"error\":\"range, check line numbers using file_read_lines\"}";
	std::vector<std::string> newLines;
	std::stringstream ss(text);
	while(std::getline(ss, line)){
		if(!line.empty() && line.back() == '\r') line.pop_back();
		newLines.push_back(line);
	}
	lines.erase(lines.begin() + (start - 1), lines.begin() + end);
	lines.insert(lines.begin() + (start - 1), newLines.begin(), newLines.end());
	std::ofstream out(p, std::ios::trunc);
	if(!out.is_open()) return "{\"error\":\"open failed\"}";
	for(size_t i = 0; i < lines.size(); ++i){
		out << lines[i];
		if(i + 1 < lines.size()) out << '\n';
	}
	return "{\"result\":\"success\"}";
}
std::string ApplyPatchTool(const char* argsJson){
	const std::string path = GetArgValue(argsJson, "path");
	const std::string patchStr = GetArgValue(argsJson, "patch");
	if(path.empty() || patchStr.empty()) return "{\"error\":\"args\"}";
	std::filesystem::path p = _baseFolder / path;
	if(!IsPathAllowed(p)) return "{\"error\":\"invalid path\"}";
	std::ifstream in(p);
	if(!in.is_open()) return "{\"error\":\"open failed\"}";
	std::vector<std::string> lines;
	std::string line;
	while(std::getline(in, line)){
		if(!line.empty() && line.back() == '\r') line.pop_back();
		lines.push_back(line);
	}
	in.close();
	struct Hunk{
		int startOld = -1;
		std::vector<std::string> lines;
	};
	std::vector<Hunk> hunks;
	std::istringstream patchStream(patchStr);
	Hunk cur;
	bool inHunk = false;
	std::regex hunkRe(R"(^@@ -(\d+)(?:,(\d+))? \+(\d+)(?:,(\d+))? @@.*$)");
	auto isIgnoredPatchLine = [](const std::string& patchLine)-> bool{
		return patchLine.rfind("*** Begin Patch", 0) == 0 || patchLine.rfind("*** End Patch", 0) == 0 || patchLine.rfind("*** Update File", 0) == 0
			|| patchLine.rfind("diff --git", 0) == 0 || patchLine.rfind("--- ", 0) == 0 || patchLine.rfind("+++ ", 0) == 0
			|| patchLine.rfind("```", 0) == 0;
	};
	while(std::getline(patchStream, line)){
		if(!line.empty() && line.back() == '\r') line.pop_back();
		if(isIgnoredPatchLine(line)) continue;
		std::smatch m;
		if(line == "@@" || std::regex_match(line, m, hunkRe)){
			if(inHunk){
				hunks.push_back(cur);
				cur.lines.clear();
				cur.startOld = -1;
			}
			inHunk = true;
			if(m.size() > 1 && m[1].matched) cur.startOld = std::stoi(m[1]) - 1;
		} else if(inHunk){ cur.lines.push_back(line); }
	}
	if(inHunk) hunks.push_back(cur);
	if(hunks.empty()) return "{\"error\":\"invalid patch: no hunks\"}";
	auto shorten = [](std::string s){
		if(s.size() > 40) s = s.substr(0, 37) + "...";
		return s;
	};
	auto makeError = [](const std::string& msg)-> std::string{ return "{\"error\":\"" + JsonEscape(msg) + "\"}"; };
	auto findSequence = [](const std::vector<std::string>& haystack, const std::vector<std::string>& needle)-> int{
		if(needle.empty()) return 0;
		if(needle.size() > haystack.size()) return -1;
		for(size_t i = 0; i + needle.size() <= haystack.size(); ++i){
			bool match = true;
			for(size_t j = 0; j < needle.size(); ++j){
				if(haystack[i + j] != needle[j]){
					match = false;
					break;
				}
			}
			if(match) return static_cast<int>(i);
		}
		return -1;
	};
	auto out = lines;
	int offset = 0;
	for(const auto& h : hunks){
		int idx = -1;
		if(h.startOld >= 0){
			idx = h.startOld + offset;
			if(idx < 0 || idx > static_cast<int>(out.size())) return makeError("range");
		} else{
			std::vector<std::string> anchor;
			for(const auto& pl : h.lines){
				if(pl.empty()){
					anchor.push_back("");
					continue;
				}
				char tag = pl[0];
				if(tag == ' ' || tag == '-') anchor.push_back(pl.substr(1));
				else if(tag != '+' && tag != '\\') return makeError("invalid patch line");
			}
			if(anchor.empty()) return makeError("cannot locate hunk without context");
			idx = findSequence(out, anchor);
			if(idx < 0) return makeError("context mismatch: could not locate hunk");
		}
		for(const auto& pl : h.lines){
			char tag = " "[0];
			std::string text;
			if(!pl.empty()){
				tag = pl[0];
				text = pl.substr(1);
			}
			if(tag == ' ' || tag == '-'){
				if(idx >= static_cast<int>(out.size())) return makeError("context end-of-file at line " + std::to_string(idx + 1));
				if(out[idx] != text) return makeError("context mismatch at line " + std::to_string(idx + 1) + ", file='" + shorten(out[idx]) + "', patch='" + shorten(text) + "'");
				if(tag == '-'){
					out.erase(out.begin() + idx);
					--offset;
				} else{ ++idx; }
			} else if(tag == '+'){
				out.insert(out.begin() + idx, text);
				++idx;
				++offset;
			} else if(tag == '\\'){
				continue;
			} else{
				if(idx >= static_cast<int>(out.size())) return makeError("context end-of-file at line " + std::to_string(idx + 1));
				if(out[idx] != pl) return makeError("context mismatch at line " + std::to_string(idx + 1) + ", file='" + shorten(out[idx]) + "', patch='" + shorten(pl) + "'");
				++idx;
			}
		}
	}
	if(out == lines) return "{\"error\":\"no changes\"}";
	std::ofstream outFile(p, std::ios::trunc);
	if(!outFile.is_open()) return "{\"error\":\"open failed\"}";
	for(size_t i = 0; i < out.size(); ++i){
		outFile << out[i];
		if(i + 1 < out.size()) outFile << "\n";
	}
	return "{\"result\":\"success\"}";
}