#include "tools.h"
#include "JSONCommon.h"
#include <algorithm>
#include <charconv>
#include <climits>
#include <cstdio>
#include <cstdint>
#include <filesystem>
#include <iomanip>
#include <locale>
#include <sstream>
#include <fstream>
#include <regex>
#include <system_error>
#include <unordered_set>
#include <windows.h>
static constexpr std::uintmax_t MaxSearchFileBytes = 16777216;
static constexpr std::uintmax_t MaxFileInfoTextBytes = 16777216;
static constexpr size_t MaxDirectoryListingBytes = 16384;
static bool _fullFileSystemAccess = true;
static std::string WideToUtf8(const std::wstring& text){
	if(text.empty()) return std::string();
	const int size = WideCharToMultiByte(CP_UTF8, 0, text.data(), static_cast<int>(text.size()), nullptr, 0, nullptr, nullptr);
	if(size <= 0) return std::string();
	std::string utf8(size, '\0');
	WideCharToMultiByte(CP_UTF8, 0, text.data(), static_cast<int>(text.size()), &utf8[0], size, nullptr, nullptr);
	return utf8;
}
static std::string PathToUtf8Generic(const std::filesystem::path& path){
#ifdef _WIN32
	return WideToUtf8(path.generic_wstring());
#else
	return path.generic_string();
#endif
}
void SetFileBaseDir(const char* dir){
	if(dir && *dir){
		std::error_code ec;
		_baseFolder = std::filesystem::weakly_canonical(dir, ec);
		if(ec) _baseFolder.clear();
		_fullFileSystemAccess = false;
	} else{
		_baseFolder.clear();
		_fullFileSystemAccess = true;
	}
}
static std::filesystem::path ResolveFileToolPath(const std::string& path){
	if(_baseFolder.empty()) return std::filesystem::u8path(path);
	return _baseFolder / std::filesystem::u8path(path);
}
static bool IsGitPathComponent(const std::filesystem::path& part){
	auto name = PathToUtf8Generic(part);
	std::transform(name.begin(), name.end(), name.begin(), [](unsigned char c){ return c >= 'A' && c <= 'Z' ? static_cast<char>(c + ('a' - 'A')) : static_cast<char>(c); });
	return name == ".git";
}
static bool HasGitPathComponent(const std::filesystem::path& path){
	for(const auto& part : path){
		if(IsGitPathComponent(part)) return true;
	}
	return false;
}
static bool IsGitDirectoryEntry(const std::filesystem::directory_entry& entry){
	std::error_code ec;
	if(!entry.is_directory(ec) || ec) return false;
	return IsGitPathComponent(entry.path().filename());
}
static std::string GetFileToolPathLabel(const std::filesystem::path& path, const std::filesystem::path* displayRoot = nullptr){
	std::error_code ec;
	if(_baseFolder.empty()){
		if(displayRoot && !displayRoot->empty()){
			const auto relPath = relative(path, *displayRoot, ec);
			const auto rel = PathToUtf8Generic(relPath);
			if(!ec && !rel.empty() && rel != ".." && rel.rfind("../", 0) != 0) return rel;
			ec.clear();
		}
		const auto filename = path.filename();
		if(!filename.empty()) return PathToUtf8Generic(filename);
		auto fullPath = weakly_canonical(path, ec);
		if(ec){
			ec.clear();
			fullPath = absolute(path, ec);
		}
		return PathToUtf8Generic(ec ? path : fullPath);
	}
	const auto relPath = relative(path, _baseFolder, ec);
	return PathToUtf8Generic(ec ? path : relPath);
}
static bool IsPathAllowed(const std::filesystem::path& p){
	if(HasGitPathComponent(p)) return false;
	if(_baseFolder.empty()) return _fullFileSystemAccess && p.is_absolute();
	std::error_code absEc;
	std::error_code baseEc;
	const auto abs = weakly_canonical(p, absEc);
	const auto base = weakly_canonical(_baseFolder, baseEc);
	if(absEc || baseEc) return false;
	const auto absStr = abs.native();
	const auto baseStr = base.native();
	if(absStr.rfind(baseStr, 0) != 0) return false;
	if(absStr.size() == baseStr.size()) return true;
	return absStr[baseStr.size()] == '\\';
}
static bool IsRegularFileForReading(const std::filesystem::path& p, std::uintmax_t* size = nullptr){
	std::error_code ec;
	const auto status = symlink_status(p, ec);
	if(ec) return false;
	if(!is_regular_file(status)) return false;
	if(is_symlink(status)) return false;
	if(size){
		*size = file_size(p, ec);
		if(ec) return false;
	}
	return true;
}
static std::string EntriesJson(const std::vector<std::string>& entries){
	std::string json = "{\"entries\":[";
	for(size_t i = 0; i < entries.size(); ++i){
		json += "\"" + JsonEscape(entries[i]) + "\"";
		if(i + 1 < entries.size()) json += ",";
	}
	json += "]}";
	return json;
}
static bool ListAvailableDrives(std::vector<std::string>& drives){
	const DWORD required = GetLogicalDriveStringsA(0, nullptr);
	if(required == 0) return false;
	std::vector<char> buffer(required + 1, '\0');
	const DWORD written = GetLogicalDriveStringsA(static_cast<DWORD>(buffer.size()), buffer.data());
	if(written == 0 || written >= buffer.size()) return false;
	for(const char* drive = buffer.data(); *drive != '\0';){
		std::string root = drive;
		std::replace(root.begin(), root.end(), '\\', '/');
		drives.push_back(root);
		drive += root.size() + 1;
	}
	return true;
}
static bool IsCurrentDirectoryRequest(const std::string& path){
	return path.empty() || path == "." || path == "./" || path == ".\\";
}
static bool IsGeneratedDirectory(const std::filesystem::path& path){
	static const std::unordered_set<std::string> names = {
		"packages", "node_modules", "bower_components", "vendor", "bin", "obj", "build", "debug", "release", "dist", "out", "target",
		".vs", ".idea", ".venv", "venv", "__pycache__", ".next", ".nuget", ".cache", "coverage", "testresults"
	};
	auto name = PathToUtf8Generic(path.filename());
	std::transform(name.begin(), name.end(), name.begin(), [](unsigned char c){ return c >= 'A' && c <= 'Z' ? static_cast<char>(c + ('a' - 'A')) : static_cast<char>(c); });
	return names.count(name) != 0;
}
static bool ReadDirectoryListingInteger(const char* argsJson, const char* key, int minimum, int maximum, int& value){
	const auto text = GetArgValue(argsJson, key);
	if(text.empty()) return true;
	const auto result = std::from_chars(text.data(), text.data() + text.size(), value);
	return result.ec == std::errc() && result.ptr == text.data() + text.size() && value >= minimum && value <= maximum;
}
std::string ListDirectoryTool(const char* slotName, const char* argsJson){
	std::string path = GetArgValue(argsJson, "path");
	const std::string recStr = GetArgValue(argsJson, "recursive");
	const bool recursive = recStr == "true" || recStr == "1";
	const auto includeGeneratedStr = GetArgValue(argsJson, "include_generated");
	if(!includeGeneratedStr.empty() && includeGeneratedStr != "true" && includeGeneratedStr != "false" && includeGeneratedStr != "1" && includeGeneratedStr != "0")
		return "{\"error\":\"include_generated must be a boolean\"}";
	const bool includeGenerated = includeGeneratedStr == "true" || includeGeneratedStr == "1";
	int maxEntries = 200, maxDepth = 4, offset = 0;
	if(!ReadDirectoryListingInteger(argsJson, "max_entries", 1, 1000, maxEntries)) return "{\"error\":\"max_entries must be an integer from 1 to 1000\"}";
	if(!ReadDirectoryListingInteger(argsJson, "max_depth", 1, 32, maxDepth)) return "{\"error\":\"max_depth must be an integer from 1 to 32\"}";
	if(!ReadDirectoryListingInteger(argsJson, "offset", 0, INT_MAX, offset)) return "{\"error\":\"offset must be a non-negative integer up to 2147483647\"}";
	const bool drivesRequest = _baseFolder.empty() && _fullFileSystemAccess && IsCurrentDirectoryRequest(path);
	if(path.empty()) path = ".";
	const std::filesystem::path p = ResolveFileToolPath(path);
	if(!drivesRequest && !IsPathAllowed(p)) return "{\"error\":\"invalid path\"}";
	std::vector<std::string> files;
	size_t entryBytes = 2; // Include the entries array brackets in the byte budget.
	std::uintmax_t seen = 0, nextOffset = 0, excludedDirectories = 0, depthLimitedDirectories = 0, reparseDirectories = 0, oversizedEntries = 0;
	bool truncated = false;
	auto addEntry = [&](const std::string& label){
		const auto index = seen++;
		if(index < static_cast<std::uintmax_t>(offset)) return true;
		const auto encodedBytes = JsonString(label).size();
		if(encodedBytes + 2 > MaxDirectoryListingBytes){ ++oversizedEntries; return true; }
		const auto bytes = encodedBytes + (files.empty() ? 0 : 1);
		if(files.size() >= static_cast<size_t>(maxEntries) || entryBytes + bytes > MaxDirectoryListingBytes){
			truncated = true;
			nextOffset = index;
			return false;
		}
		files.push_back(label);
		entryBytes += bytes;
		return true;
	};
	std::error_code ec;
	if(drivesRequest){
		// Drive roots use the same budgets and paging as an ordinary directory listing.
		std::vector<std::string> drives;
		if(!ListAvailableDrives(drives)) return "{\"error\":\"failed to list drives\"}";
		for(const auto& drive : drives) if(!addEntry(drive)) break;
	} else if(recursive){
		for(auto it = std::filesystem::recursive_directory_iterator(p, ec), end = std::filesystem::recursive_directory_iterator(); it != end; it.increment(ec)){
			if(ec) break;
			const auto& entry = *it;
			if(IsGitDirectoryEntry(entry)){
				it.disable_recursion_pending();
				continue;
			}
			const auto attributes = GetFileAttributesW(entry.path().c_str());
			if(attributes != INVALID_FILE_ATTRIBUTES && (attributes & FILE_ATTRIBUTE_DIRECTORY)){
				if(attributes & FILE_ATTRIBUTE_REPARSE_POINT){
					it.disable_recursion_pending();
					++reparseDirectories;
				} else if(!includeGenerated && IsGeneratedDirectory(entry.path())){
					it.disable_recursion_pending();
					++excludedDirectories;
				} else if(it.depth() + 1 >= maxDepth){
					it.disable_recursion_pending();
					++depthLimitedDirectories;
				}
			}
			if(!addEntry(GetFileToolPathLabel(entry.path(), &p))) break;
		}
	} else{
		for(auto it = std::filesystem::directory_iterator(p, ec), end = std::filesystem::directory_iterator(); it != end; it.increment(ec)){
			if(ec) break;
			const auto& entry = *it;
			if(IsGitDirectoryEntry(entry)) continue;
			if(!addEntry(GetFileToolPathLabel(entry.path(), &p))) break;
		}
	}
	if(ec) return "{\"error\":\"folder not found, maybe its a file\"}";
	auto json = EntriesJson(files);
	json.pop_back();
	json += ",\"returned_count\":" + std::to_string(files.size()) + ",\"truncated\":" + (truncated ? "true" : "false")
		+ ",\"next_offset\":" + (truncated ? std::to_string(nextOffset) : "null") + ",\"total_entries\":" + (truncated ? "null" : std::to_string(seen))
		+ ",\"entries_seen\":" + std::to_string(seen) + ",\"max_entries\":" + std::to_string(maxEntries) + ",\"max_depth\":" + std::to_string(maxDepth)
		+ ",\"include_generated\":" + (includeGenerated ? "true" : "false") + ",\"excluded_directory_count\":" + std::to_string(excludedDirectories)
		+ ",\"depth_limited_directory_count\":" + std::to_string(depthLimitedDirectories) + ",\"reparse_directory_count\":" + std::to_string(reparseDirectories)
		+ ",\"oversized_entry_count\":" + std::to_string(oversizedEntries);
	if(truncated || excludedDirectories || depthLimitedDirectories || reparseDirectories || oversizedEntries){
		json += ",\"warning\":\"This is a partial directory view. ";
		if(truncated) json += "Output budget reached; more entries exist. ";
		if(excludedDirectories) json += "Dependency/build directories were listed without descending into them. ";
		if(depthLimitedDirectories) json += "Maximum recursion depth reached. ";
		if(reparseDirectories) json += "Directory reparse points were not traversed. ";
		if(oversizedEntries) json += "Some paths were too long to return. ";
		json += "Use a narrower path or search_files. ";
		if(truncated) json += "For more entries, repeat the same arguments with offset=" + std::to_string(nextOffset) + "; max_entries can be raised to 1000. ";
		if(excludedDirectories) json += "Set include_generated=true to enter dependency/build directories, or list one directly. ";
		if(depthLimitedDirectories) json += "Increase max_depth (up to 32) to recurse deeper. ";
		json += "\"";
	}
	return json + "}";
}
std::string SearchFilesTool(const char* slotName, const char* argsJson){
	std::string path = GetArgValue(argsJson, "path");
	const std::string query = GetArgValue(argsJson, "query");
	const std::string maxResultsStr = GetArgValue(argsJson, "max_results");
	const std::string caseSensitiveStr = GetArgValue(argsJson, "case_sensitive");
	const std::string recursiveStr = GetArgValue(argsJson, "recursive");
	const std::string useRegexStr = GetArgValue(argsJson, "use_regex");
	const bool caseSensitive = caseSensitiveStr == "true" || caseSensitiveStr == "1";
	const bool recursive = recursiveStr == "true" || recursiveStr == "1";
	const bool useRegex = useRegexStr == "true" || useRegexStr == "1";
	int maxResults = 100;
	if(!maxResultsStr.empty()){
		auto[ptr, ec] = std::from_chars(maxResultsStr.data(), maxResultsStr.data() + maxResultsStr.size(), maxResults);
		if(ec != std::errc() || ptr != maxResultsStr.data() + maxResultsStr.size() || maxResults <= 0) return "{\"error\":\"max_results must be a valid positive integer\"}";
	}
	auto trim = [](const std::string& s){
		const auto start = s.find_first_not_of(" \t\r\n");
		if(start == std::string::npos) return std::string();
		const auto end = s.find_last_not_of(" \t\r\n");
		return s.substr(start, end - start + 1);
	};
	auto toLower = [](std::string s){
		std::transform(s.begin(), s.end(), s.begin(), [](unsigned char c){ return c >= 'A' && c <= 'Z' ? static_cast<char>(c + ('a' - 'A')) : static_cast<char>(c); });
		return s;
	};
	std::vector<std::string> terms;
	if(!useRegex){
		size_t start = 0;
		while(start <= query.size()){
			const size_t sep = query.find('|', start);
			const size_t len = sep == std::string::npos ? query.size() - start : sep - start;
			const std::string token = trim(query.substr(start, len));
			if(!token.empty()) terms.push_back(caseSensitive ? token : toLower(token));
			if(sep == std::string::npos) break;
			start = sep + 1;
		}
		if(terms.empty()) return "{\"error\":\"query is required\"}";
	} else if(query.empty()) return "{\"error\":\"query is required\"}";
	std::regex queryRegex;
	if(useRegex){
		try{
			auto flags = std::regex_constants::ECMAScript;
			if(!caseSensitive) flags |= std::regex_constants::icase;
			queryRegex = std::regex(query, flags);
		} catch(const std::regex_error&){
			return "{\"error\":\"invalid regex query\"}";
		}
	}
	if(path.empty()) path = ".";
	const std::filesystem::path p = ResolveFileToolPath(path);
	if(!IsPathAllowed(p)) return "{\"error\":\"invalid path\"}";
	if(!is_directory(p)) return "{\"error\":\"path is not a folder\"}";
	std::vector<std::string> files;
	auto isMatch = [&](const std::string& label){
		if(useRegex) return std::regex_search(label, queryRegex);
		const std::string searchLabel = caseSensitive ? label : toLower(label);
		for(const auto& term : terms){
			if(searchLabel.find(term) != std::string::npos) return true;
		}
		return false;
	};
	auto addIfMatch = [&](const std::filesystem::directory_entry& entry)-> bool{
		if(!IsRegularFileForReading(entry.path())) return false;
		const std::string label = GetFileToolPathLabel(entry.path(), &p);
		if(!isMatch(label)) return false;
		files.push_back(label);
		return static_cast<int>(files.size()) >= maxResults;
	};
	std::error_code ec;
	if(recursive){
		for(auto it = std::filesystem::recursive_directory_iterator(p, ec), end = std::filesystem::recursive_directory_iterator(); it != end; it.increment(ec)){
			if(ec) break;
			const auto& entry = *it;
			if(IsGitDirectoryEntry(entry)){
				it.disable_recursion_pending();
				continue;
			}
			if(addIfMatch(entry)) break;
		}
	} else{
		for(const auto& entry : std::filesystem::directory_iterator(p, ec)){
			if(ec) break;
			if(IsGitDirectoryEntry(entry)) continue;
			if(addIfMatch(entry)) break;
		}
	}
	if(ec) return "{\"error\":\"failed to iterate directory\"}";
	return EntriesJson(files);
}
static std::string FileInfoError(const std::string& message, DWORD code = 0){
	return "{\"error\":" + JsonString(message) + (code ? ",\"win32_error\":" + std::to_string(code) : "") + "}";
}
static std::string FileInfoDate(const FILETIME& time){
	if(time.dwLowDateTime == 0 && time.dwHighDateTime == 0) return "null";
	SYSTEMTIME utc{};
	if(!FileTimeToSystemTime(&time, &utc)) return "null";
	char buffer[32];
	sprintf_s(buffer, "%04u-%02u-%02uT%02u:%02u:%02u.%03uZ", utc.wYear, utc.wMonth, utc.wDay, utc.wHour, utc.wMinute, utc.wSecond, utc.wMilliseconds);
	return JsonString(buffer);
}
static std::string FileInfoSize(const std::uintmax_t bytes){
	static const char* units[] = {"B", "KiB", "MiB", "GiB", "TiB", "PiB", "EiB"};
	double size = static_cast<double>(bytes);
	size_t unit = 0;
	while(size >= 1024 && unit + 1 < sizeof(units)/sizeof(units[0])){
		size /= 1024;
		++unit;
	}
	std::ostringstream out;
	out.imbue(std::locale::classic());
	out << std::fixed << std::setprecision(unit == 0 ? 0 : 2) << size << ' ' << units[unit];
	return out.str();
}
static bool IsKnownTextFile(const std::filesystem::path& path){
	static const std::unordered_set<std::string> extensions = {
		".txt", ".text", ".log", ".md", ".markdown", ".rst", ".adoc", ".csv", ".tsv", ".json", ".jsonl", ".ndjson", ".xml", ".xaml", ".resx", ".svg",
		".html", ".htm", ".css", ".scss", ".sass", ".less", ".js", ".jsx", ".mjs", ".cjs", ".ts", ".tsx", ".py", ".pyw", ".ipynb", ".rb", ".php",
		".java", ".kt", ".kts", ".c", ".cpp", ".cc", ".cxx", ".h", ".hpp", ".hh", ".hxx", ".inl", ".ipp", ".cs", ".fs", ".fsx", ".vb", ".go", ".rs", ".swift", ".sql",
		".sh", ".bash", ".zsh", ".fish", ".bat", ".cmd", ".ps1", ".psm1", ".psd1", ".yml", ".yaml", ".toml", ".ini", ".cfg", ".conf", ".config", ".properties",
		".sln", ".vcxproj", ".filters", ".props", ".targets", ".csproj", ".fsproj", ".vbproj", ".cmake", ".mak", ".gradle", ".tex", ".rtf", ".lua", ".r", ".pl"
	};
	static const std::unordered_set<std::string> names = {
		"readme", "license", "licence", "copying", "authors", "changelog", "makefile", "gnumakefile", "dockerfile", "containerfile", "cmakelists.txt",
		".gitignore", ".gitattributes", ".editorconfig", ".dockerignore", ".npmrc", ".env"
	};
	auto lower = [](std::string s){
		std::transform(s.begin(), s.end(), s.begin(), [](unsigned char c){ return c >= 'A' && c <= 'Z' ? static_cast<char>(c + ('a' - 'A')) : static_cast<char>(c); });
		return s;
	};
	const auto name = lower(PathToUtf8Generic(path.filename()));
	return extensions.count(lower(PathToUtf8Generic(path.extension()))) || names.count(name) || name.rfind(".env.", 0) == 0;
}
enum class FileInfoEncoding{ Utf8, Utf16LE, Utf16BE, Utf32LE, Utf32BE, Unknown8Bit };
static bool ReadFileInfoCodepoint(const std::string& data, size_t& pos, FileInfoEncoding encoding, std::uint32_t& cp){
	const auto byte = [&](size_t offset){ return static_cast<unsigned char>(data[offset]); };
	if(encoding == FileInfoEncoding::Unknown8Bit){ cp = byte(pos++); return true; }
	if(encoding == FileInfoEncoding::Utf8){
		cp = byte(pos++);
		if(cp < 0x80) return true;
		const int extra = cp >= 0xC2 && cp <= 0xDF ? 1 : cp >= 0xE0 && cp <= 0xEF ? 2 : cp >= 0xF0 && cp <= 0xF4 ? 3 : -1;
		if(extra < 0 || data.size() - pos < static_cast<size_t>(extra)) return false;
		cp &= (1u << (6 - extra)) - 1;
		for(int i = 0; i < extra; ++i){
			const auto next = byte(pos++);
			if((next & 0xC0) != 0x80) return false;
			cp = (cp << 6) | (next & 0x3F);
		}
		return cp >= (extra == 1 ? 0x80u : extra == 2 ? 0x800u : 0x10000u) && cp <= 0x10FFFF && !(cp >= 0xD800 && cp <= 0xDFFF);
	}
	const bool utf16 = encoding == FileInfoEncoding::Utf16LE || encoding == FileInfoEncoding::Utf16BE;
	const bool littleEndian = encoding == FileInfoEncoding::Utf16LE || encoding == FileInfoEncoding::Utf32LE;
	const size_t width = utf16 ? 2 : 4;
	auto readUnit = [&](std::uint32_t& unit){
		if(data.size() - pos < width) return false;
		unit = 0;
		for(size_t i = 0; i < width; ++i) unit |= static_cast<std::uint32_t>(byte(pos + i)) << (8*(littleEndian ? i : width - i - 1));
		pos += width;
		return true;
	};
	if(!readUnit(cp)) return false;
	if(utf16 && cp >= 0xD800 && cp <= 0xDBFF){
		std::uint32_t low;
		if(!readUnit(low) || low < 0xDC00 || low > 0xDFFF) return false;
		cp = 0x10000 + ((cp - 0xD800) << 10) + low - 0xDC00;
	}
	return cp <= 0x10FFFF && !(cp >= 0xD800 && cp <= 0xDFFF);
}
static bool IsFileInfoWhitespace(std::uint32_t cp){
	return cp == ' ' || (cp >= '\t' && cp <= '\r') || cp == 0x85 || cp == 0xA0 || cp == 0x1680 || (cp >= 0x2000 && cp <= 0x200A) || cp == 0x2028 || cp == 0x2029 || cp == 0x202F || cp == 0x205F || cp == 0x3000;
}
struct FileInfoTextStats{
	std::uintmax_t lines = 0, blankLines = 0, words = 0, characters = 0, longestLine = 0, crlf = 0, lf = 0, cr = 0;
	bool endsWithNewline = false, nonAscii = false;
};
static std::string ScanFileInfoText(const std::string& data, size_t pos, FileInfoEncoding encoding, FileInfoTextStats& stats){
	std::uintmax_t lineLength = 0;
	bool blank = true, inWord = false, previousCR = false;
	auto finishLine = [&](){
		++stats.lines;
		if(blank) ++stats.blankLines;
		stats.longestLine = std::max(stats.longestLine, lineLength);
		lineLength = 0;
		blank = true;
	};
	while(pos < data.size()){
		std::uint32_t cp;
		if(!ReadFileInfoCodepoint(data, pos, encoding, cp)) return "invalid_encoding";
		if((cp < 0x20 && !(cp >= '\t' && cp <= '\r')) || cp == 0x7F) return "binary_content";
		const bool whitespace = IsFileInfoWhitespace(cp);
		++stats.characters;
		stats.nonAscii = stats.nonAscii || cp > 0x7F;
		if(!whitespace && !inWord) ++stats.words;
		inWord = !whitespace;
		if(cp == '\r'){
			++stats.cr;
			finishLine();
		} else if(cp == '\n'){
			if(previousCR){ --stats.cr; ++stats.crlf; }
			else{ ++stats.lf; finishLine(); }
		} else{
			++lineLength;
			if(!whitespace) blank = false;
		}
		previousCR = cp == '\r';
		stats.endsWithNewline = cp == '\r' || cp == '\n';
	}
	// A trailing line terminator does not create an extra empty line. BOM-only files have zero lines.
	if(stats.characters && !stats.endsWithNewline) finishLine();
	return "";
}
static std::string GetFileInfoText(const std::filesystem::path& path, std::uintmax_t size){
	std::ifstream file(path, std::ios::binary);
	if(!file.is_open()) return "{\"status\":\"unavailable\",\"reason\":\"open_failed\"}";
	std::string data(static_cast<size_t>(size), '\0');
	file.read(data.data(), static_cast<std::streamsize>(data.size()));
	if(file.bad() || static_cast<size_t>(file.gcount()) != data.size() || file.peek() != std::ifstream::traits_type::eof())
		return "{\"status\":\"unavailable\",\"reason\":\"read_failed_or_file_changed\"}";
	FileInfoEncoding encoding = FileInfoEncoding::Utf8;
	std::string label = "UTF-8";
	size_t bom = 0;
	if(data.compare(0, 4, "\xFF\xFE\x00\x00", 4) == 0){ encoding = FileInfoEncoding::Utf32LE; label = "UTF-32LE"; bom = 4; }
	else if(data.compare(0, 4, "\x00\x00\xFE\xFF", 4) == 0){ encoding = FileInfoEncoding::Utf32BE; label = "UTF-32BE"; bom = 4; }
	else if(data.compare(0, 3, "\xEF\xBB\xBF", 3) == 0) bom = 3;
	else if(data.compare(0, 2, "\xFF\xFE", 2) == 0){ encoding = FileInfoEncoding::Utf16LE; label = "UTF-16LE"; bom = 2; }
	else if(data.compare(0, 2, "\xFE\xFF", 2) == 0){ encoding = FileInfoEncoding::Utf16BE; label = "UTF-16BE"; bom = 2; }
	FileInfoTextStats stats;
	auto reason = ScanFileInfoText(data, bom, encoding, stats);
	if(reason == "invalid_encoding" && bom == 0){
		// Legacy text still has useful line counts; its character/word counts cannot be inferred reliably.
		encoding = FileInfoEncoding::Unknown8Bit;
		label = "unknown-8-bit";
		stats = {};
		reason = ScanFileInfoText(data, 0, encoding, stats);
	}
	if(!reason.empty()) return "{\"status\":\"unavailable\",\"reason\":" + JsonString(reason) + "}";
	if(encoding == FileInfoEncoding::Utf8 && !bom && !stats.nonAscii) label = "ASCII";
	const auto newlineTypes = (stats.crlf > 0) + (stats.lf > 0) + (stats.cr > 0);
	const char* newlines = newlineTypes > 1 ? "mixed" : stats.crlf ? "CRLF" : stats.lf ? "LF" : stats.cr ? "CR" : "none";
	const bool knownEncoding = encoding != FileInfoEncoding::Unknown8Bit;
	return "{\"status\":\"complete\",\"encoding\":" + JsonString(label) + ",\"has_bom\":" + (bom ? "true" : "false")
		+ ",\"line_count\":" + std::to_string(stats.lines) + ",\"blank_line_count\":" + std::to_string(stats.blankLines)
		+ ",\"word_count\":" + (knownEncoding ? std::to_string(stats.words) : "null")
		+ ",\"character_count\":" + (knownEncoding ? std::to_string(stats.characters) : "null")
		+ ",\"longest_line_characters\":" + (knownEncoding ? std::to_string(stats.longestLine) : "null")
		+ ",\"newline_style\":" + JsonString(newlines) + ",\"newline_counts\":{\"crlf\":" + std::to_string(stats.crlf) + ",\"lf\":" + std::to_string(stats.lf) + ",\"cr\":" + std::to_string(stats.cr)
		+ "},\"ends_with_newline\":" + (stats.endsWithNewline ? "true" : "false") + "}";
}
std::string GetFileInfoTool(const char* slotName, const char* argsJson){
	const auto path = GetArgValue(argsJson, "path");
	if(path.empty()) return FileInfoError("path is required");
	if(path.find('\0') != std::string::npos) return FileInfoError("invalid path");
	const auto includeText = GetArgValue(argsJson, "include_text_stats");
	if(!includeText.empty() && includeText != "true" && includeText != "false" && includeText != "0" && includeText != "1") return FileInfoError("include_text_stats must be a boolean");
	try{
		const auto p = ResolveFileToolPath(path);
		if(!IsPathAllowed(p)) return FileInfoError("invalid path");
		std::error_code ec;
		const auto fullPath = absolute(p, ec).lexically_normal();
		if(ec) return FileInfoError("failed to resolve path");
		std::string json = "{\"path\":" + JsonString(PathToUtf8Generic(fullPath));
		WIN32_FILE_ATTRIBUTE_DATA info{};
		if(!GetFileAttributesExW(fullPath.c_str(), GetFileExInfoStandard, &info)){
			const auto error = GetLastError();
			if(error == ERROR_FILE_NOT_FOUND || error == ERROR_PATH_NOT_FOUND) return json + ",\"exists\":false}";
			return FileInfoError("failed to read file metadata", error);
		}
		const bool directory = (info.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY) != 0;
		const bool reparse = (info.dwFileAttributes & FILE_ATTRIBUTE_REPARSE_POINT) != 0;
		const bool regular = !directory && !reparse && !(info.dwFileAttributes & FILE_ATTRIBUTE_DEVICE);
		const std::uintmax_t size = (static_cast<std::uintmax_t>(info.nFileSizeHigh) << 32) | info.nFileSizeLow;
		json += ",\"exists\":true,\"name\":" + JsonString(PathToUtf8Generic(fullPath.filename())) + ",\"extension\":" + JsonString(PathToUtf8Generic(fullPath.extension()));
		json += ",\"parent\":" + JsonString(PathToUtf8Generic(fullPath.parent_path())) + ",\"type\":" + JsonString(reparse ? "reparse_point" : directory ? "directory" : regular ? "file" : "other");
		json += ",\"size_bytes\":" + (regular ? std::to_string(size) : "null") + ",\"size_display\":" + (regular ? JsonString(FileInfoSize(size)) : "null");
		json += ",\"created_utc\":" + FileInfoDate(info.ftCreationTime) + ",\"modified_utc\":" + FileInfoDate(info.ftLastWriteTime) + ",\"accessed_utc\":" + FileInfoDate(info.ftLastAccessTime);
		json += ",\"read_only\":" + std::string(info.dwFileAttributes & FILE_ATTRIBUTE_READONLY ? "true" : "false") + ",\"hidden\":" + (info.dwFileAttributes & FILE_ATTRIBUTE_HIDDEN ? "true" : "false");
		json += ",\"system\":" + std::string(info.dwFileAttributes & FILE_ATTRIBUTE_SYSTEM ? "true" : "false") + ",\"reparse_point\":" + (reparse ? "true" : "false");
		std::string skipReason;
		if(includeText == "false" || includeText == "0") skipReason = "disabled";
		else if(!regular) skipReason = "not_a_regular_file";
		else if(!IsKnownTextFile(fullPath)) skipReason = "unrecognized_text_file";
		else if(size > MaxFileInfoTextBytes) skipReason = "size_limit";
		json += ",\"text\":";
		if(skipReason.empty()) json += GetFileInfoText(fullPath, size);
		else json += "{\"status\":\"skipped\",\"reason\":" + JsonString(skipReason) + ",\"max_bytes\":" + std::to_string(MaxFileInfoTextBytes) + "}";
		return json + "}";
	} catch(const std::filesystem::filesystem_error&){
		return FileInfoError("invalid path or file metadata unavailable");
	}
}
std::string ReadFileTool(const char* slotName, const char* argsJson){
	std::string path = GetArgValue(argsJson, "path");
	const std::string startStr = GetArgValue(argsJson, "start");
	const std::string endStr = GetArgValue(argsJson, "end");
	int start = -1, end = -1;
	if(!startStr.empty()) std::from_chars(startStr.data(), startStr.data() + startStr.size(), start);
	if(!endStr.empty()) std::from_chars(endStr.data(), endStr.data() + endStr.size(), end);
	std::filesystem::path p = ResolveFileToolPath(path);
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
	const std::string fileName = PathToUtf8Generic(p.filename());
	const std::string ext = PathToUtf8Generic(p.extension());
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
	std::string out = "File: " + fileName + "\n```" + contentType + "\n" + body + "```\n";
	return out;
}
std::string SearchFileTool(const char* slotName, const char* argsJson){
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
	const std::filesystem::path p = ResolveFileToolPath(path);
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
		std::uintmax_t fileSize = 0;
		if(!IsRegularFileForReading(filePath, &fileSize) || fileSize > MaxSearchFileBytes){
			return false;
		}
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
			for(auto it = std::filesystem::recursive_directory_iterator(p, ec), end = std::filesystem::recursive_directory_iterator(); it != end; it.increment(ec)){
				if(ec) break;
				const auto& entry = *it;
				if(IsGitDirectoryEntry(entry)){
					it.disable_recursion_pending();
					continue;
				}
				if(!IsRegularFileForReading(entry.path())) continue;
				const std::string fileLabel = GetFileToolPathLabel(entry.path(), &p);
				if(processFile(entry.path(), fileLabel)) break;
			}
		} else{
			for(const auto& entry : std::filesystem::directory_iterator(p, ec)){
				if(ec) break;
				if(IsGitDirectoryEntry(entry)) continue;
				if(!IsRegularFileForReading(entry.path())) continue;
				const std::string fileLabel = GetFileToolPathLabel(entry.path(), &p);
				if(processFile(entry.path(), fileLabel)) break;
			}
		}
		if(ec) return "{\"error\":\"failed to iterate directory\"}";
	} else{
		std::uintmax_t fileSize = 0;
		if(!IsRegularFileForReading(p, &fileSize)) return "{\"error\":\"path is not a regular file\"}";
		if(fileSize > MaxSearchFileBytes) return "{\"error\":\"file is too large to search\"}";
		processFile(p, "");
	}
	if(json.back() == ',') json.pop_back();
	json += "]}";
	return json;
}
std::string CreateFileTool(const char* slotName, const char* argsJson){
	const std::string path = GetArgValue(argsJson, "path");
	const std::string text = GetArgValue(argsJson, "text");
	const std::string overwriteStr = GetArgValue(argsJson, "overwrite");
	const bool overwrite = overwriteStr == "true" || overwriteStr == "1";
	const std::filesystem::path p = ResolveFileToolPath(path);
	if(!IsPathAllowed(p)) return "{\"error\":\"invalid path\"}";
	if(exists(p) && !overwrite) return "{\"error\":\"exists\"}";
	create_directories(p.parent_path());
	std::ofstream f(p);
	if(!f.is_open()) return "{\"error\":\"open failed\"}";
	f << text;
	return "{\"result\":\"success\"}";
}
std::string ReplaceLinesTool(const char* slotName, const char* argsJson){
	std::string path = GetArgValue(argsJson, "path");
	const std::string startStr = GetArgValue(argsJson, "start");
	const std::string endStr = GetArgValue(argsJson, "end");
	std::string text = GetArgValue(argsJson, "text");
	int start = -1, end = -1;
	if(!startStr.empty()) std::from_chars(startStr.data(), startStr.data() + startStr.size(), start);
	if(!endStr.empty()) std::from_chars(endStr.data(), endStr.data() + endStr.size(), end);
	else end = start;
	if(start < 1 || end < start) return "{\"error\":\"range, check line numbers using file_read_lines\"}";
	std::filesystem::path p = ResolveFileToolPath(path);
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
std::string ApplyPatchTool(const char* slotName, const char* argsJson){
	const std::string path = GetArgValue(argsJson, "path");
	const std::string patchStr = GetArgValue(argsJson, "patch");
	if(path.empty() || patchStr.empty()) return "{\"error\":\"args\"}";
	std::filesystem::path p = ResolveFileToolPath(path);
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
