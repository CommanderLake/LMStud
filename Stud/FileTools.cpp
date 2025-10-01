#include "tools.h"
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
	while(std::getline(in, line)){ lines.push_back(line); }
	in.close();
	if(start > static_cast<int>(lines.size()) || end > static_cast<int>(lines.size())) return "{\"error\":\"range, check line numbers using file_read_lines\"}";
	std::vector<std::string> newLines;
	std::stringstream ss(text);
	while(std::getline(ss, line)){ newLines.push_back(line); }
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
		int startOld;
		std::vector<std::string> lines;
	};
	std::vector<Hunk> hunks;
	std::istringstream patchStream(patchStr);
	Hunk cur;
	bool inHunk = false;
	std::regex hunkRe(R"(@@ -(\d+)(?:,(\d+))? \+(\d+)(?:,(\d+))? @@.*)");
	while(std::getline(patchStream, line)){
		if(!line.empty() && line.back() == '\r') line.pop_back();
		std::smatch m;
		if(std::regex_match(line, m, hunkRe)){
			if(inHunk){
				hunks.push_back(cur);
				cur.lines.clear();
			}
			inHunk = true;
			cur.startOld = std::stoi(m[1]) - 1;
		} else if(inHunk){ cur.lines.push_back(line); }
	}
	if(inHunk) hunks.push_back(cur);
	auto trim = [](const std::string& s)-> std::string{
		const auto b = s.find_first_not_of(" \t");
		const auto e = s.find_last_not_of(" \t");
		if(b == std::string::npos) return {};
		return s.substr(b, e - b + 1);
	};
	auto shorten = [](std::string s){
		if(s.size() > 40) s = s.substr(0, 37) + "...";
		return s;
	};
	auto makeError = [](const std::string& msg)-> std::string{ return "{\"error\":\"" + JsonEscape(msg) + "\"}"; };
	auto out = lines;
	int offset = 0;
	for(const auto& h : hunks){
		int idx = h.startOld + offset;
		if(idx < 0 || idx > static_cast<int>(out.size())) return makeError("range");
		for(const auto& pl : h.lines){
			if(pl.empty()){
				if(idx >= static_cast<int>(out.size())) return makeError("context end-of-file at line " + std::to_string(idx + 1));
				++idx;
				continue;
			}
			char tag = pl[0];
			std::string text = pl.substr(1);
			if(tag == ' ' || tag == '-'){
				if(idx >= static_cast<int>(out.size())) return makeError("context end-of-file at line " + std::to_string(idx + 1));
				if(trim(out[idx]) != trim(text)) return makeError("context mismatch at line " + std::to_string(idx + 1) + ", file='" + shorten(trim(out[idx])) + "', patch='" + shorten(trim(text)) + "'");
				if(tag == '-'){
					out.erase(out.begin() + idx);
					--offset;
				} else{ ++idx; }
			} else if(tag == '+'){
				out.insert(out.begin() + idx, text);
				++idx;
				++offset;
			} else{
				if(idx >= static_cast<int>(out.size())) return makeError("context end-of-file at line " + std::to_string(idx + 1));
				if(trim(out[idx]) != trim(pl)) return makeError("context mismatch at line " + std::to_string(idx + 1) + ", file='" + shorten(trim(out[idx])) + "', patch='" + shorten(trim(pl)) + "'");
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