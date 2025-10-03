#include "tools.h"
#include <Windows.h>
#include <algorithm>
#include <cctype>
#include <mutex>
#include <string>
#include <memory>
#include <chrono>
#include <vector>
#include <sstream>
struct CommandPromptSession{
	HANDLE process = nullptr;
	HANDLE stdinWrite = nullptr;
	HANDLE stdoutRead = nullptr;
	std::string id;
	bool active = false;
};
std::mutex g_cmdMutex;
CommandPromptSession g_cmdSession;
constexpr const char* kDefaultSession = "default";
constexpr const char* kEndMarker = "__LMSTUD_CMD_END_7B3F9A2C__";
constexpr const char* kReadyMarker = "__LMSTUD_CMD_READY_7B3F9A2C__";
constexpr DWORD kDefaultTimeoutMs = 60000;
constexpr DWORD kInitTimeoutMs = 10000;
class HandleGuard{
	HANDLE& handle;
public:
	explicit HandleGuard(HANDLE& h) : handle(h){}
	~HandleGuard(){
		if(handle && handle != INVALID_HANDLE_VALUE){
			CloseHandle(handle);
			handle = nullptr;
		}
	}
	HandleGuard(const HandleGuard&) = delete;
	HandleGuard& operator=(const HandleGuard&) = delete;
	void release(){ handle = nullptr; }
};
std::string FormatWin32Error(const char* context){
	const DWORD err = GetLastError();
	LPSTR buffer = nullptr;
	const DWORD len = FormatMessageA(FORMAT_MESSAGE_ALLOCATE_BUFFER | FORMAT_MESSAGE_FROM_SYSTEM | FORMAT_MESSAGE_IGNORE_INSERTS, nullptr, err, 0, reinterpret_cast<LPSTR>(&buffer), 0, nullptr);
	std::unique_ptr<char, decltype(&LocalFree)> bufferGuard(buffer, LocalFree);
	std::string message;
	if(context && *context){
		message += context;
		message += ": ";
	}
	if(len > 0 && buffer){
		DWORD trimLen = len;
		while(trimLen > 0 && (buffer[trimLen - 1] == '\r' || buffer[trimLen - 1] == '\n' || buffer[trimLen - 1] == ' ')){ --trimLen; }
		message.append(buffer, trimLen);
	} else{
		message += "error ";
		message += std::to_string(err);
	}
	return message;
}
void CloseHandles(CommandPromptSession& session){
	if(session.stdinWrite && session.stdinWrite != INVALID_HANDLE_VALUE){
		CloseHandle(session.stdinWrite);
		session.stdinWrite = nullptr;
	}
	if(session.stdoutRead && session.stdoutRead != INVALID_HANDLE_VALUE){
		CloseHandle(session.stdoutRead);
		session.stdoutRead = nullptr;
	}
	if(session.process && session.process != INVALID_HANDLE_VALUE){
		CloseHandle(session.process);
		session.process = nullptr;
	}
	session.id.clear();
	session.active = false;
}
void InternalCloseCommandPrompt(const bool force){
	if(!g_cmdSession.active) return;
	if(g_cmdSession.stdinWrite && g_cmdSession.stdinWrite != INVALID_HANDLE_VALUE){
		DWORD written = 0;
		const char exitCmd[] = "exit\r\n";
		WriteFile(g_cmdSession.stdinWrite, exitCmd, static_cast<DWORD>(sizeof(exitCmd) - 1), &written, nullptr);
	}
	if(g_cmdSession.process && g_cmdSession.process != INVALID_HANDLE_VALUE){
		if(WaitForSingleObject(g_cmdSession.process, 1000) == WAIT_TIMEOUT && force){
			TerminateProcess(g_cmdSession.process, 1);
			WaitForSingleObject(g_cmdSession.process, 2000);
		}
	}
	CloseHandles(g_cmdSession);
}
bool WriteLine(const CommandPromptSession& session, std::string text, std::string& error){
	if(!session.stdinWrite || session.stdinWrite == INVALID_HANDLE_VALUE){
		error = "stdin handle is invalid";
		return false;
	}
	while(!text.empty() && (text.back() == '\r' || text.back() == '\n')){ text.pop_back(); }
	text += "\r\n";
	DWORD written = 0;
	if(!WriteFile(session.stdinWrite, text.c_str(), static_cast<DWORD>(text.size()), &written, nullptr)){
		error = FormatWin32Error("WriteFile");
		return false;
	}
	if(written != text.size()){
		error = "Partial write to stdin";
		return false;
	}
	return true;
}
bool ReadUntilMarker(const CommandPromptSession& session, const std::string& marker, std::string& output, std::string& error, const DWORD timeoutMs){
	output.clear();
	if(!session.stdoutRead || session.stdoutRead == INVALID_HANDLE_VALUE){
		error = "stdout handle is invalid";
		return false;
	}
	if(!session.process || session.process == INVALID_HANDLE_VALUE){
		error = "process handle is invalid";
		return false;
	}
	const auto start = std::chrono::steady_clock::now();
	bool markerFound = false;
	size_t markerPos = std::string::npos;
	while(true){
		if(WaitForSingleObject(session.process, 0) == WAIT_OBJECT_0){
			DWORD exitCode = 0;
			GetExitCodeProcess(session.process, &exitCode);
			error = "Command prompt exited (code: " + std::to_string(exitCode) + ")";
			return false;
		}
		if(timeoutMs != INFINITE){
			auto elapsed = std::chrono::duration_cast<std::chrono::milliseconds>(std::chrono::steady_clock::now() - start).count();
			if(elapsed > timeoutMs){
				error = "Command timed out after " + std::to_string(timeoutMs) + "ms";
				return false;
			}
		}
		DWORD available = 0;
		if(!PeekNamedPipe(session.stdoutRead, nullptr, 0, nullptr, &available, nullptr)){
			error = FormatWin32Error("PeekNamedPipe");
			return false;
		}
		if(available > 0){
			char buffer[4096];
			const DWORD toRead = std::min<DWORD>(available, static_cast<DWORD>(sizeof(buffer)));
			DWORD read = 0;
			if(!ReadFile(session.stdoutRead, buffer, toRead, &read, nullptr)){
				error = FormatWin32Error("ReadFile");
				return false;
			}
			if(read > 0){
				output.append(buffer, read);
				if(!marker.empty() && !markerFound){
					markerPos = output.find(marker);
					if(markerPos != std::string::npos){
						markerFound = true;
						Sleep(50);
					}
				}
			}
		} else if(markerFound){
			break;
		} else if(marker.empty()){
			return true;
		} else{
			Sleep(10);
		}
	}
	return markerFound || marker.empty();
}
void StripMarker(std::string& text, const std::string& marker){
	const auto pos = text.find(marker);
	if(pos == std::string::npos) return;
	size_t start = pos;
	while(start > 0 && text[start - 1] != '\n') --start;
	size_t end = pos + marker.size();
	if(end < text.size() && text[end] == '\r') ++end;
	if(end < text.size() && text[end] == '\n') ++end;
	text.erase(start, end - start);
}
void RemoveConsecutiveDuplicateLines(std::string& text){
	std::vector<std::string> lines;
	std::istringstream stream(text);
	std::string line;
	while(std::getline(stream, line)){
		if(!line.empty() && line.back() == '\r'){ line.pop_back(); }
		lines.push_back(line);
	}
	if(lines.empty()) return;
	std::vector<std::string> result;
	for(size_t i = 0; i < lines.size(); ++i){
		if(i == 0 || lines[i] != lines[i - 1]){ result.push_back(lines[i]); }
	}
	text.clear();
	for(size_t i = 0; i < result.size(); ++i){
		if(i > 0) text += '\n';
		text += result[i];
	}
}
void TrimNewlines(std::string& text){
	size_t start = 0;
	while(start < text.size() && (text[start] == '\r' || text[start] == '\n')){ ++start; }
	if(start > 0){ text.erase(0, start); }
	while(!text.empty() && (text.back() == '\r' || text.back() == '\n')){ text.pop_back(); }
}
void NormalizeCommandOutput(std::string& text){
	text.erase(std::remove(text.begin(), text.end(), '\r'), text.end());
	TrimNewlines(text);
}
std::string TrimCopy(std::string text){
	const auto begin = text.find_first_not_of(" \t\r\n");
	if(begin == std::string::npos) return {};
	const auto end = text.find_last_not_of(" \t\r\n");
	return text.substr(begin, end - begin + 1);
}
bool StartCommandPromptSession(const std::string& sessionId, std::string& startupOutput, std::string& error){
	startupOutput.clear();
	SECURITY_ATTRIBUTES sa{};
	sa.nLength = sizeof(sa);
	sa.bInheritHandle = TRUE;
	HANDLE stdoutRead = nullptr;
	HANDLE stdoutWrite = nullptr;
	if(!CreatePipe(&stdoutRead, &stdoutWrite, &sa, 0)){
		error = FormatWin32Error("CreatePipe(stdout)");
		return false;
	}
	HandleGuard stdoutReadGuard(stdoutRead);
	HandleGuard stdoutWriteGuard(stdoutWrite);
	if(!SetHandleInformation(stdoutRead, HANDLE_FLAG_INHERIT, 0)){
		error = FormatWin32Error("SetHandleInformation(stdout)");
		return false;
	}
	HANDLE stdinRead = nullptr;
	HANDLE stdinWrite = nullptr;
	if(!CreatePipe(&stdinRead, &stdinWrite, &sa, 0)){
		error = FormatWin32Error("CreatePipe(stdin)");
		return false;
	}
	HandleGuard stdinReadGuard(stdinRead);
	HandleGuard stdinWriteGuard(stdinWrite);
	if(!SetHandleInformation(stdinWrite, HANDLE_FLAG_INHERIT, 0)){
		error = FormatWin32Error("SetHandleInformation(stdin)");
		return false;
	}
	STARTUPINFOW si{};
	si.cb = sizeof(si);
	si.dwFlags = STARTF_USESTDHANDLES | STARTF_USESHOWWINDOW;
	si.hStdOutput = stdoutWrite;
	si.hStdError = stdoutWrite;
	si.hStdInput = stdinRead;
	si.wShowWindow = SW_HIDE;
	PROCESS_INFORMATION pi{};
	wchar_t commandLine[] = L"cmd.exe /Q";
	wchar_t userProfile[MAX_PATH];
	DWORD size = MAX_PATH;
	if(GetEnvironmentVariableW(L"USERPROFILE", userProfile, size) == 0){
		wcscpy_s(userProfile, L"C:\\");
	}
	if(!CreateProcessW(nullptr, commandLine, nullptr, nullptr, TRUE, CREATE_NO_WINDOW | CREATE_UNICODE_ENVIRONMENT, nullptr, userProfile, &si, &pi)){
		error = FormatWin32Error("CreateProcessW");
		return false;
	}

	CloseHandle(pi.hThread);
	if(stdoutWrite && stdoutWrite != INVALID_HANDLE_VALUE){
		CloseHandle(stdoutWrite);          
	}
	if(stdinRead && stdinRead != INVALID_HANDLE_VALUE){
		CloseHandle(stdinRead);          
	}
	stdoutWrite = nullptr;
	stdinRead = nullptr;
	g_cmdSession.process = pi.hProcess;
	g_cmdSession.stdinWrite = stdinWrite;
	g_cmdSession.stdoutRead = stdoutRead;
	g_cmdSession.id = sessionId.empty() ? kDefaultSession : sessionId;
	g_cmdSession.active = true;
	stdinWriteGuard.release();
	stdoutReadGuard.release();
	std::string initError;
	Sleep(50);
	std::string initCmd = "chcp 65001>nul 2>&1 & echo " + std::string(kReadyMarker);
	if(!WriteLine(g_cmdSession, initCmd, initError)){
		error = "Failed to initialize session: " + initError;
		InternalCloseCommandPrompt(true);
		return false;
	}
	if(!ReadUntilMarker(g_cmdSession, kReadyMarker, startupOutput, error, kInitTimeoutMs)){
		InternalCloseCommandPrompt(true);
		return false;
	}
	StripMarker(startupOutput, kReadyMarker);
	RemoveConsecutiveDuplicateLines(startupOutput);
	NormalizeCommandOutput(startupOutput);
	return true;
}
bool ExecuteCommand(const std::string& command, std::string& output, std::string& error){
	if(!g_cmdSession.active){
		error = "Session not active";
		return false;
	}
	DWORD available = 0;
	if(PeekNamedPipe(g_cmdSession.stdoutRead, nullptr, 0, nullptr, &available, nullptr) && available > 0){
		char buffer[4096];
		DWORD read = 0;
		while(available > 0){
			ReadFile(g_cmdSession.stdoutRead, buffer, std::min<DWORD>(available, sizeof(buffer)), &read, nullptr);
			if(!PeekNamedPipe(g_cmdSession.stdoutRead, nullptr, 0, nullptr, &available, nullptr)) break;
		}
	}
	std::string combinedCommand = command + " & echo " + kEndMarker;
	if(!WriteLine(g_cmdSession, combinedCommand, error)){ return false; }
	if(!ReadUntilMarker(g_cmdSession, kEndMarker, output, error, kDefaultTimeoutMs)){ return false; }
	StripMarker(output, kEndMarker);
	if(!output.empty()){
		std::vector<std::string> lines;
		std::istringstream stream(output);
		std::string line;
		bool foundCommandEcho = false;
		while(std::getline(stream, line)){
			if(!foundCommandEcho && line.find(" & echo ") != std::string::npos){
				foundCommandEcho = true;
				continue;
			}
			lines.push_back(line);
		}
		output.clear();
		for(size_t i = 0; i < lines.size(); ++i){
			if(i > 0) output += '\n';
			output += lines[i];
		}
	}
	RemoveConsecutiveDuplicateLines(output);
	NormalizeCommandOutput(output);
	return true;
}
bool IsTruthy(const std::string& value){
	auto lower = value;
	std::transform(lower.begin(), lower.end(), lower.begin(), [](unsigned char c){ return static_cast<char>(std::tolower(c)); });
	return lower == "true" || lower == "1" || lower == "yes" || lower == "close";
}
bool IsExitCommand(const std::string& value){
	auto lower = value;
	std::transform(lower.begin(), lower.end(), lower.begin(), [](unsigned char c){ return static_cast<char>(std::tolower(c)); });
	return lower == "exit" || lower == "quit";
}
std::string StartCommandPromptTool(const char* argsJson){
	const auto sessionArg = TrimCopy(GetArgValue(argsJson, "session"));
	const auto sessionId = sessionArg.empty() ? std::string(kDefaultSession) : sessionArg;
	std::lock_guard<std::mutex> lock(g_cmdMutex);
	if(g_cmdSession.active){ InternalCloseCommandPrompt(true); }
	std::string output;
	std::string error;
	if(!StartCommandPromptSession(sessionId, output, error)){ return "{\"error\":\"" + JsonEscape(error) + "\"}"; }
	if(output.empty()){ output = "(no output)"; }
	return "```cmd\n" + output + "\n```";
}
std::string CommandPromptExecuteTool(const char* argsJson){
	const auto sessionArg = TrimCopy(GetArgValue(argsJson, "session"));
	const auto commandArg = TrimCopy(GetArgValue(argsJson, "command"));
	const auto closeArg = TrimCopy(GetArgValue(argsJson, "close"));
	if(commandArg.empty() && (closeArg.empty() || !IsTruthy(closeArg))){ return "{\"error\":\"command is required\"}"; }
	const auto sessionId = sessionArg.empty() ? std::string(kDefaultSession) : sessionArg;
	std::lock_guard<std::mutex> lock(g_cmdMutex);
	if(!g_cmdSession.active || g_cmdSession.id != sessionId){
		if(!closeArg.empty() && IsTruthy(closeArg)){ return "{\"result\":\"session already closed\"}"; }
		return "{\"error\":\"Session not started. Call command_prompt_start first.\"}";
	}
	if(!closeArg.empty() && IsTruthy(closeArg)){
		InternalCloseCommandPrompt(true);
		return "{\"result\":\"session closed\"}";
	}
	if(IsExitCommand(commandArg)){
		InternalCloseCommandPrompt(true);
		return "{\"result\":\"session closed\"}";
	}
	std::string output;
	std::string error;
	if(!ExecuteCommand(commandArg, output, error)){
		InternalCloseCommandPrompt(true);
		return "{\"error\":\"" + JsonEscape(error) + "\"}";
	}
	if(output.empty()){ output = "(no output)"; }
	return "```cmd\n" + output + "\n```";
}
extern "C" EXPORT void CloseCommandPrompt(){
	std::lock_guard<std::mutex> lock(g_cmdMutex);
	InternalCloseCommandPrompt(true);
}