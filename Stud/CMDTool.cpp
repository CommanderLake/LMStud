#include "tools.h"
#include <Windows.h>
#include <algorithm>
#include <cctype>
#include <cstring>
#include <mutex>
#include <string>
namespace{
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
	constexpr const char* kEndMarker = "__LMSTUD_PROMPT_END__";
	constexpr const char* kReadyMarker = "__LMSTUD_PROMPT_READY__";
	constexpr const char* kPromptText = "LMSTUD_PROMPT>";
}
static std::string FormatWin32Error(const char* context){
	const DWORD err = GetLastError();
	LPSTR buffer = nullptr;
	const DWORD len = FormatMessageA(FORMAT_MESSAGE_ALLOCATE_BUFFER | FORMAT_MESSAGE_FROM_SYSTEM | FORMAT_MESSAGE_IGNORE_INSERTS, nullptr, err, 0, reinterpret_cast<LPSTR>(&buffer), 0, nullptr);
	std::string message;
	if(context && *context){
		message += context;
		message += ": ";
	}
	if(len && buffer){
		DWORD trimLen = len;
		while(trimLen > 0 && (buffer[trimLen - 1] == '\r' || buffer[trimLen - 1] == '\n' || buffer[trimLen - 1] == ' ')) --trimLen;
		message.append(buffer, trimLen);
		LocalFree(buffer);
	} else{
		message += "error ";
		message += std::to_string(err);
	}
	return message;
}
static void CloseHandles(CommandPromptSession& session){
	if(session.stdinWrite){
		CloseHandle(session.stdinWrite);
		session.stdinWrite = nullptr;
	}
	if(session.stdoutRead){
		CloseHandle(session.stdoutRead);
		session.stdoutRead = nullptr;
	}
	if(session.process){
		CloseHandle(session.process);
		session.process = nullptr;
	}
	session.id.clear();
	session.active = false;
}
static void InternalCloseCommandPrompt(const bool force){
	if(!g_cmdSession.active) return;
	if(g_cmdSession.stdinWrite){
		DWORD written = 0;
		const char exitCmd[] = "exit\r\n";
		WriteFile(g_cmdSession.stdinWrite, exitCmd, static_cast<DWORD>(sizeof exitCmd - 1), &written, nullptr);
	}
	if(g_cmdSession.process){
		if(WaitForSingleObject(g_cmdSession.process, 1000) == WAIT_TIMEOUT && force){
			TerminateProcess(g_cmdSession.process, 0);
			WaitForSingleObject(g_cmdSession.process, 2000);
		}
	}
	CloseHandles(g_cmdSession);
}
static bool WriteLine(const CommandPromptSession& session, std::string text, std::string& error){
	if(!session.stdinWrite){
		error = "stdin unavailable";
		return false;
	}
	while(!text.empty() && (text.back() == '\r' || text.back() == '\n')) text.pop_back();
	text += "\r\n";
	DWORD written = 0;
	if(!WriteFile(session.stdinWrite, text.c_str(), static_cast<DWORD>(text.size()), &written, nullptr)){
		error = FormatWin32Error("WriteFile");
		return false;
	}
	return true;
}
static bool ReadUntilMarker(const CommandPromptSession& session, const std::string& marker, std::string& output, std::string& error, const DWORD timeoutMs){
	output.clear();
	const auto start = GetTickCount64();
	bool markerSeen = false;
	while(true){
		DWORD available = 0;
		if(!PeekNamedPipe(session.stdoutRead, nullptr, 0, nullptr, &available, nullptr)){
			error = FormatWin32Error("PeekNamedPipe");
			return false;
		}
		if(available > 0){
			char buffer[4096];
			const DWORD toRead = std::min<DWORD>(available, static_cast<DWORD>(sizeof buffer));
			DWORD read = 0;
			if(!ReadFile(session.stdoutRead, buffer, toRead, &read, nullptr)){
				error = FormatWin32Error("ReadFile");
				return false;
			}
			if(read > 0){
				output.append(buffer, read);
				if(marker.empty()) return true;
				if(output.find(marker) != std::string::npos) markerSeen = true;
			}
		} else{
			if(markerSeen) break;
			if(WaitForSingleObject(session.process, 0) == WAIT_OBJECT_0){
				DWORD exitCode = 0;
				GetExitCodeProcess(session.process, &exitCode);
				error = "Command prompt exited (" + std::to_string(exitCode) + ")";
				return false;
			}
			if(timeoutMs != INFINITE){
				const auto elapsed = GetTickCount64() - start;
				if(elapsed > timeoutMs){
					error = "Command timed out.";
					return false;
				}
			}
			Sleep(10);
		}
	}
	if(!marker.empty() && markerSeen){
		while(true){
			DWORD available = 0;
			if(!PeekNamedPipe(session.stdoutRead, nullptr, 0, nullptr, &available, nullptr)){
				error = FormatWin32Error("PeekNamedPipe");
				return false;
			}
			if(available == 0) break;
			char buffer[4096];
			const DWORD toRead = std::min<DWORD>(available, static_cast<DWORD>(sizeof buffer));
			DWORD read = 0;
			if(!ReadFile(session.stdoutRead, buffer, toRead, &read, nullptr)){
				error = FormatWin32Error("ReadFile");
				return false;
			}
			if(read == 0) break;
			output.append(buffer, read);
		}
		return true;
	}
	return marker.empty();
}
static void StripMarker(std::string& text, const std::string& marker){
	const auto pos = text.find(marker);
	if(pos == std::string::npos) return;
	size_t start = pos;
	if(start > 0 && text[start - 1] == '\r') --start;
	if(start > 0 && text[start - 1] == '\n') --start;
	size_t end = pos + marker.size();
	if(end < text.size() && text[end] == '\r') ++end;
	if(end < text.size() && text[end] == '\n') ++end;
	text.erase(start, end - start);
}
static void StripTrailingPrompt(std::string& text){
	while(true){
		const auto pos = text.rfind(kPromptText);
		if(pos == std::string::npos) break;
		bool onlyWhitespace = true;
		const size_t promptLen = std::strlen(kPromptText);
		for(size_t i = pos + promptLen; i < text.size(); ++i){
			const auto ch = text[i];
			if(ch != ' ' && ch != '\r' && ch != '\n'){
				onlyWhitespace = false;
				break;
			}
		}
		if(!onlyWhitespace) break;
		size_t start = pos;
		while(start > 0 && (text[start - 1] == '\r' || text[start - 1] == '\n' || text[start - 1] == ' ')) --start;
		text.erase(start);
	}
}
static void TrimNewlines(std::string& text){
	while(!text.empty() && (text.front() == '\r' || text.front() == '\n')) text.erase(text.begin());
	while(!text.empty() && (text.back() == '\r' || text.back() == '\n')) text.pop_back();
}
static std::string TrimCopy(std::string text){
	const auto begin = text.find_first_not_of(" \t\r\n");
	if(begin == std::string::npos) return {};
	const auto end = text.find_last_not_of(" \t\r\n");
	return text.substr(begin, end - begin + 1);
}
static bool StartCommandPromptSession(const std::string& sessionId, std::string& error){
	InternalCloseCommandPrompt(true);
	SECURITY_ATTRIBUTES sa{};
	sa.nLength = sizeof sa;
	sa.bInheritHandle = TRUE;
	HANDLE stdoutRead = nullptr;
	HANDLE stdoutWrite = nullptr;
	if(!CreatePipe(&stdoutRead, &stdoutWrite, &sa, 0)){
		error = FormatWin32Error("CreatePipe(stdout)");
		return false;
	}
	if(!SetHandleInformation(stdoutRead, HANDLE_FLAG_INHERIT, 0)){
		error = FormatWin32Error("SetHandleInformation(stdout)");
		CloseHandle(stdoutRead);
		CloseHandle(stdoutWrite);
		return false;
	}
	HANDLE stdinRead = nullptr;
	HANDLE stdinWrite = nullptr;
	if(!CreatePipe(&stdinRead, &stdinWrite, &sa, 0)){
		error = FormatWin32Error("CreatePipe(stdin)");
		CloseHandle(stdoutRead);
		CloseHandle(stdoutWrite);
		return false;
	}
	if(!SetHandleInformation(stdinWrite, HANDLE_FLAG_INHERIT, 0)){
		error = FormatWin32Error("SetHandleInformation(stdin)");
		CloseHandle(stdoutRead);
		CloseHandle(stdoutWrite);
		CloseHandle(stdinRead);
		CloseHandle(stdinWrite);
		return false;
	}
	STARTUPINFOW si{};
	si.cb = sizeof si;
	si.dwFlags = STARTF_USESTDHANDLES | STARTF_USESHOWWINDOW;
	si.hStdOutput = stdoutWrite;
	si.hStdError = stdoutWrite;
	si.hStdInput = stdinRead;
	si.wShowWindow = SW_SHOW;
	PROCESS_INFORMATION pi{};
	std::wstring commandLine = L"cmd.exe";
	commandLine.push_back(L'\0');
	if(!CreateProcessW(nullptr, commandLine.data(), nullptr, nullptr, TRUE, CREATE_NO_WINDOW, nullptr, nullptr, &si, &pi)){
		error = FormatWin32Error("CreateProcessW");
		CloseHandle(stdoutRead);
		CloseHandle(stdoutWrite);
		CloseHandle(stdinRead);
		CloseHandle(stdinWrite);
		return false;
	}
	CloseHandle(stdoutWrite);
	CloseHandle(stdinRead);
	CloseHandle(pi.hThread);
	g_cmdSession.process = pi.hProcess;
	g_cmdSession.stdinWrite = stdinWrite;
	g_cmdSession.stdoutRead = stdoutRead;
	g_cmdSession.id = sessionId.empty() ? kDefaultSession : sessionId;
	g_cmdSession.active = true;
	std::string initError;
	if(!WriteLine(g_cmdSession, "@echo off", initError) || !WriteLine(g_cmdSession, "chcp 65001>nul", initError) || !WriteLine(g_cmdSession, "prompt LMSTUD_PROMPT$G", initError) || !WriteLine(g_cmdSession, std::string("echo ") + kReadyMarker, initError)){
		error = initError;
		InternalCloseCommandPrompt(true);
		return false;
	}
	std::string discard;
	if(!ReadUntilMarker(g_cmdSession, kReadyMarker, discard, error, 10000)){
		InternalCloseCommandPrompt(true);
		return false;
	}
	StripMarker(discard, kReadyMarker);
	return true;
}
static bool EnsureSession(const std::string& sessionId, std::string& error){
	const auto target = sessionId.empty() ? std::string(kDefaultSession) : sessionId;
	if(g_cmdSession.active){ if(WaitForSingleObject(g_cmdSession.process, 0) != WAIT_TIMEOUT || g_cmdSession.id != target){ InternalCloseCommandPrompt(true); } }
	if(!g_cmdSession.active) return StartCommandPromptSession(target, error);
	return true;
}
static bool ExecuteCommand(const std::string& command, std::string& output, std::string& error){
	if(!g_cmdSession.active){
		error = "session unavailable";
		return false;
	}
	if(!WriteLine(g_cmdSession, command, error)) return false;
	if(!WriteLine(g_cmdSession, std::string("echo ") + kEndMarker, error)) return false;
	if(!ReadUntilMarker(g_cmdSession, kEndMarker, output, error, 60000)) return false;
	StripMarker(output, kEndMarker);
	StripTrailingPrompt(output);
	output.erase(std::remove(output.begin(), output.end(), '\r'), output.end());
	TrimNewlines(output);
	return true;
}
static bool IsTruthy(const std::string& value){
	auto lower = value;
	std::transform(lower.begin(), lower.end(), lower.begin(), [](unsigned char c){ return static_cast<char>(std::tolower(c)); });
	return lower == "true" || lower == "1" || lower == "yes" || lower == "close";
}
static bool IsExitCommand(const std::string& value){
	auto lower = value;
	std::transform(lower.begin(), lower.end(), lower.begin(), [](unsigned char c){ return static_cast<char>(std::tolower(c)); });
	return lower == "exit" || lower == "quit";
}
std::string CommandPromptTool(const char* argsJson){
	const auto sessionArg = TrimCopy(GetArgValue(argsJson, "session"));
	const auto commandArg = TrimCopy(GetArgValue(argsJson, "command"));
	const auto closeArg = TrimCopy(GetArgValue(argsJson, "close"));
	const auto sessionId = sessionArg.empty() ? std::string(kDefaultSession) : sessionArg;
	std::lock_guard<std::mutex> lock(g_cmdMutex);
	if(!closeArg.empty() && IsTruthy(closeArg)){
		InternalCloseCommandPrompt(true);
		return "{\"result\":\"closed\"}";
	}
	std::string error;
	if(!EnsureSession(sessionId, error)) return "{\"error\":\"" + JsonEscape(error) + "\"}";
	if(commandArg.empty()) return "{\"result\":\"session ready\"}";
	if(IsExitCommand(commandArg)){
		InternalCloseCommandPrompt(true);
		return "{\"result\":\"closed\"}";
	}
	std::string output;
	if(!ExecuteCommand(commandArg, output, error)){
		InternalCloseCommandPrompt(true);
		return "{\"error\":\"" + JsonEscape(error) + "\"}";
	}
	if(output.empty()) output = "(no output)";
	return "```cmd\n" + output + "\n```";
}
extern "C" EXPORT void CloseCommandPrompt(){
	std::lock_guard<std::mutex> lock(g_cmdMutex);
	InternalCloseCommandPrompt(true);
}