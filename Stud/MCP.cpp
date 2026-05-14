#define CURL_STATICLIB
#include "MCP.h"
#include "JSONCommon.h"
#include "stud.h"
#include <Windows.h>
#include <algorithm>
#include <atomic>
#include <chrono>
#include <cctype>
#include <curl\curl.h>
#include <cstdlib>
#include <cstring>
#include <memory>
#include <mutex>
#include <sstream>
#include <string>
#include <thread>
#include <unordered_map>
#include <vector>
constexpr const char* kProtocolVersion = "2025-11-25";
constexpr DWORD kDefaultTimeoutMs = 10000;
enum class McpTransport{ Stdio, Http };
struct McpTool{
	std::string exposedName;
	std::string name;
	std::string description;
	std::string inputSchema;
};
struct McpServerSession{
	std::string id;
	HANDLE process = nullptr;
	HANDLE stdinWrite = nullptr;
	HANDLE stdoutRead = nullptr;
	HANDLE stderrRead = nullptr;
	std::thread stderrThread;
	std::atomic_bool stopStderr{false};
	std::mutex stderrMutex;
	std::string pendingStdout;
	DWORD timeoutMs = kDefaultTimeoutMs;
	int nextRequestId = 1;
	bool initialized = false;
	std::string initializeResult;
	std::string stderrLog;
	McpTransport transport = McpTransport::Stdio;
	std::string httpUrl;
	std::string httpCustomHeader;
	std::string httpSessionId;
	std::vector<McpTool> tools;
};
std::mutex gMCPMutex;
std::unordered_map<std::string, std::unique_ptr<McpServerSession>> gMCPServers;
char* CopyCString(const std::string& text){
	auto* buffer = static_cast<char*>(std::malloc(text.size() + 1));
	if(!buffer) return nullptr;
	std::memcpy(buffer, text.data(), text.size());
	buffer[text.size()] = '\0';
	return buffer;
}
std::string ErrorJson(const std::string& message){ return "{\"error\":\"" + JsonEscape(message) + "\"}"; }
std::string OkJson(){ return "{\"ok\":true}"; }
std::string TrimAscii(const std::string& value){
	size_t first = 0;
	while(first < value.size() && std::isspace(static_cast<unsigned char>(value[first]))) ++first;
	size_t last = value.size();
	while(last > first && std::isspace(static_cast<unsigned char>(value[last - 1]))) --last;
	return value.substr(first, last - first);
}
std::string ToLowerAscii(std::string value){
	std::transform(value.begin(), value.end(), value.begin(), [](unsigned char c){ return static_cast<char>(std::tolower(c)); });
	return value;
}
bool StartsWithNoCase(const std::string& value, const char* prefix){
	if(!prefix) return false;
	const size_t len = std::strlen(prefix);
	if(value.size() < len) return false;
	for(size_t i = 0; i < len; ++i)
		if(std::tolower(static_cast<unsigned char>(value[i])) != std::tolower(static_cast<unsigned char>(prefix[i]))) return false;
	return true;
}
extern std::string FormatWin32Error(const char* context);
void CloseHandleIfValid(HANDLE& handle){
	if(handle && handle != INVALID_HANDLE_VALUE){
		CloseHandle(handle);
		handle = nullptr;
	}
}
void CloseSessionHandles(McpServerSession& session, const bool terminate){
	if(session.stdinWrite && session.stdinWrite != INVALID_HANDLE_VALUE) FlushFileBuffers(session.stdinWrite);
	CloseHandleIfValid(session.stdinWrite);
	if(session.process && session.process != INVALID_HANDLE_VALUE){
		if(WaitForSingleObject(session.process, 1000) == WAIT_TIMEOUT && terminate){
			TerminateProcess(session.process, 1);
			WaitForSingleObject(session.process, 2000);
		}
	}
	CloseHandleIfValid(session.stdoutRead);
	session.stopStderr.store(true);
	CloseHandleIfValid(session.stderrRead);
	if(session.stderrThread.joinable()) session.stderrThread.join();
	CloseHandleIfValid(session.process);
}
bool IsSafeIdChar(const char c){ return std::isalnum(static_cast<unsigned char>(c)) || c == '_' || c == '-'; }
std::string NormalizeToolNamePart(const std::string& value){
	std::string normalized;
	normalized.reserve(value.size());
	for(const char c : value) normalized += IsSafeIdChar(c) ? c : '_';
	while(!normalized.empty() && normalized.front() == '_') normalized.erase(normalized.begin());
	if(normalized.empty()) normalized = "server";
	return normalized;
}
std::string CompactJson(const char* json){
	if(!json) return "{}";
	std::string compact;
	bool inString = false;
	bool escaped = false;
	for(const char* p = json; *p; ++p){
		const char c = *p;
		if(inString){
			compact += c;
			if(escaped) escaped = false;
			else if(c == '\\') escaped = true;
			else if(c == '"') inString = false;
			continue;
		}
		if(c == '"'){
			inString = true;
			compact += c;
			continue;
		}
		if(!std::isspace(static_cast<unsigned char>(c))) compact += c;
	}
	return compact.empty() ? "{}" : compact;
}
bool WriteJsonLine(McpServerSession& session, const std::string& message, std::string& error){
	if(!session.stdinWrite || session.stdinWrite == INVALID_HANDLE_VALUE){
		error = "MCP stdin handle is invalid.";
		return false;
	}
	std::string line = message;
	line += "\n";
	DWORD written = 0;
	if(!WriteFile(session.stdinWrite, line.data(), static_cast<DWORD>(line.size()), &written, nullptr)){
		error = FormatWin32Error("WriteFile");
		return false;
	}
	if(written != line.size()){
		error = "Partial write to MCP stdin.";
		return false;
	}
	return true;
}
bool ReadNextJsonLine(McpServerSession& session, std::string& line, std::string& error, const DWORD timeoutMs){
	line.clear();
	if(!session.stdoutRead || session.stdoutRead == INVALID_HANDLE_VALUE){
		error = "MCP stdout handle is invalid.";
		return false;
	}
	const auto start = std::chrono::steady_clock::now();
	while(true){
		const auto newline = session.pendingStdout.find('\n');
		if(newline != std::string::npos){
			line = session.pendingStdout.substr(0, newline);
			session.pendingStdout.erase(0, newline + 1);
			if(!line.empty() && line.back() == '\r') line.pop_back();
			if(!line.empty()) return true;
			continue;
		}
		if(session.process && session.process != INVALID_HANDLE_VALUE && WaitForSingleObject(session.process, 0) == WAIT_OBJECT_0){
			DWORD exitCode = 0;
			GetExitCodeProcess(session.process, &exitCode);
			error = "MCP server exited with code " + std::to_string(exitCode) + ".";
			return false;
		}
		const auto elapsed = std::chrono::duration_cast<std::chrono::milliseconds>(std::chrono::steady_clock::now() - start).count();
		if(timeoutMs != INFINITE && elapsed > timeoutMs){
			error = "MCP request timed out after " + std::to_string(timeoutMs) + "ms.";
			return false;
		}
		DWORD available = 0;
		if(!PeekNamedPipe(session.stdoutRead, nullptr, 0, nullptr, &available, nullptr)){
			error = FormatWin32Error("PeekNamedPipe");
			return false;
		}
		if(available == 0){
			Sleep(10);
			continue;
		}
		char buffer[4096];
		const DWORD toRead = std::min<DWORD>(available, static_cast<DWORD>(sizeof buffer));
		DWORD read = 0;
		if(!ReadFile(session.stdoutRead, buffer, toRead, &read, nullptr)){
			error = FormatWin32Error("ReadFile");
			return false;
		}
		if(read > 0) session.pendingStdout.append(buffer, read);
	}
}
std::string RequestIdJson(const int id){ return std::to_string(id); }
bool SameJsonId(const std::string& response, const int requestId){
	std::string idRaw;
	if(!GetJsonPropertyRaw(response, "id", idRaw)) return false;
	const char* p = SkipJsonWhitespace(idRaw.c_str());
	if(!p) return false;
	return std::string(p) == RequestIdJson(requestId) || std::string(p) == JsonString(RequestIdJson(requestId));
}
void RespondToServerRequest(McpServerSession& session, const std::string& request){
	std::string idRaw;
	std::string method;
	if(!GetJsonPropertyRaw(request, "id", idRaw)) return;
	if(!GetJsonStringProperty(request, "method", method)) return;
	std::string response;
	if(method == "ping") response = "{\"jsonrpc\":\"2.0\",\"id\":" + idRaw + ",\"result\":{}}";
	else response = "{\"jsonrpc\":\"2.0\",\"id\":" + idRaw + ",\"error\":{\"code\":-32601,\"message\":\"LM Stud native MCP client does not implement client method " + JsonEscape(method) + "\"}}";
	std::string ignored;
	WriteJsonLine(session, response, ignored);
}
bool SendStdioRequest(McpServerSession& session, const std::string& method, const std::string& paramsJson, std::string& response, std::string& error){
	const int id = session.nextRequestId++;
	std::string request = "{\"jsonrpc\":\"2.0\",\"id\":" + RequestIdJson(id) + ",\"method\":\"" + JsonEscape(method) + "\"";
	if(!paramsJson.empty()) request += ",\"params\":" + paramsJson;
	request += "}";
	if(!WriteJsonLine(session, request, error)) return false;
	while(true){
		std::string line;
		if(!ReadNextJsonLine(session, line, error, session.timeoutMs)) return false;
		if(SameJsonId(line, id)){
			response = line;
			std::string errorObj;
			if(GetJsonPropertyRaw(response, "error", errorObj)){
				std::string message;
				if(GetJsonStringProperty(errorObj, "message", message)) error = message;
				else error = errorObj;
				return false;
			}
			return true;
		}
		RespondToServerRequest(session, line);
	}
}
bool SendStdioNotification(McpServerSession& session, const std::string& method, const std::string& paramsJson, std::string& error){
	std::string notification = "{\"jsonrpc\":\"2.0\",\"method\":\"" + JsonEscape(method) + "\"";
	if(!paramsJson.empty()) notification += ",\"params\":" + paramsJson;
	notification += "}";
	return WriteJsonLine(session, notification, error);
}
struct McpHttpExchange{
	std::string body;
	std::string contentType;
	std::string sessionId;
	long statusCode = 0;
};
size_t HttpBodyCallback(void* ptr, const size_t size, const size_t nmemb, void* userdata){
	const auto response = static_cast<std::string*>(userdata);
	response->append(static_cast<char*>(ptr), size * nmemb);
	return size * nmemb;
}
size_t HttpHeaderCallback(char* buffer, const size_t size, const size_t nitems, void* userdata){
	const auto exchange = static_cast<McpHttpExchange*>(userdata);
	const std::string header(buffer, size * nitems);
	const auto colon = header.find(':');
	if(colon != std::string::npos){
		const auto name = ToLowerAscii(TrimAscii(header.substr(0, colon)));
		const auto value = TrimAscii(header.substr(colon + 1));
		if(name == "mcp-session-id") exchange->sessionId = value;
		else if(name == "content-type") exchange->contentType = ToLowerAscii(value);
	}
	return size * nitems;
}
void AppendCustomHttpHeaders(curl_slist*& headers, const std::string& customHeader){
	std::istringstream stream(customHeader);
	std::string line;
	while(std::getline(stream, line)){
		line = TrimAscii(line);
		if(line.empty()) continue;
		if(line.find(':') == std::string::npos) line = "Authorization: Bearer " + line;
		headers = curl_slist_append(headers, line.c_str());
	}
}
std::string ExtractJsonRpcErrorMessage(const std::string& response){
	std::string errorObject;
	if(!GetJsonPropertyRaw(response, "error", errorObject)) return "";
	std::string message;
	if(GetJsonStringProperty(errorObject, "message", message)) return message;
	return errorObject;
}
bool TryConsumeSseEvent(const std::string& eventData, const int requestId, std::string& response, std::string& firstJson){
	const auto data = TrimAscii(eventData);
	if(data.empty() || data == "[DONE]") return false;
	if(data[0] != '{') return false;
	if(firstJson.empty()) firstJson = data;
	if(SameJsonId(data, requestId)){
		response = data;
		return true;
	}
	return false;
}
bool ExtractSseJsonResponse(const std::string& body, const int requestId, std::string& response, std::string& error){
	std::istringstream stream(body);
	std::string line;
	std::string eventData;
	std::string firstJson;
	while(std::getline(stream, line)){
		if(!line.empty() && line.back() == '\r') line.pop_back();
		if(line.empty()){
			if(TryConsumeSseEvent(eventData, requestId, response, firstJson)) return true;
			eventData.clear();
			continue;
		}
		if(StartsWithNoCase(line, "data:")){
			auto data = line.substr(5);
			if(!data.empty() && data[0] == ' ') data.erase(data.begin());
			if(!eventData.empty()) eventData += "\n";
			eventData += data;
		}
	}
	if(TryConsumeSseEvent(eventData, requestId, response, firstJson)) return true;
	if(!firstJson.empty()){
		response = firstJson;
		return true;
	}
	error = "MCP HTTP SSE response did not contain a JSON-RPC response.";
	return false;
}
bool DecodeHttpResponseBody(const McpHttpExchange& exchange, const int requestId, std::string& response, std::string& error){
	const auto body = TrimAscii(exchange.body);
	if(body.empty()){
		error = "MCP HTTP response body was empty.";
		return false;
	}
	if(exchange.contentType.find("text/event-stream") != std::string::npos || StartsWithNoCase(body, "data:") || StartsWithNoCase(body, "event:") || StartsWithNoCase(body, "id:"))
		return ExtractSseJsonResponse(exchange.body, requestId, response, error);
	response = body;
	return true;
}
bool HttpPostJson(McpServerSession& session, const std::string& message, const bool expectResponse, const int requestId, std::string& response, std::string& error){
	if(session.httpUrl.empty()){
		error = "MCP HTTP URL is empty.";
		return false;
	}
	CURL* curl = curl_easy_init();
	if(!curl){
		error = "Failed to initialize libcurl.";
		return false;
	}
	curl_slist* headers = nullptr;
	headers = curl_slist_append(headers, "Content-Type: application/json");
	headers = curl_slist_append(headers, "Accept: application/json, text/event-stream");
	std::string protocolHeader = std::string("MCP-Protocol-Version: ") + kProtocolVersion;
	headers = curl_slist_append(headers, protocolHeader.c_str());
	if(!session.httpSessionId.empty()){
		std::string sessionHeader = "MCP-Session-Id: " + session.httpSessionId;
		headers = curl_slist_append(headers, sessionHeader.c_str());
	}
	AppendCustomHttpHeaders(headers, session.httpCustomHeader);
	McpHttpExchange exchange;
	curl_easy_setopt(curl, CURLOPT_URL, session.httpUrl.c_str());
	curl_easy_setopt(curl, CURLOPT_HTTPHEADER, headers);
	curl_easy_setopt(curl, CURLOPT_POST, 1L);
	curl_easy_setopt(curl, CURLOPT_POSTFIELDS, message.c_str());
	curl_easy_setopt(curl, CURLOPT_POSTFIELDSIZE, static_cast<long>(message.size()));
	curl_easy_setopt(curl, CURLOPT_WRITEFUNCTION, HttpBodyCallback);
	curl_easy_setopt(curl, CURLOPT_WRITEDATA, &exchange.body);
	curl_easy_setopt(curl, CURLOPT_HEADERFUNCTION, HttpHeaderCallback);
	curl_easy_setopt(curl, CURLOPT_HEADERDATA, &exchange);
	curl_easy_setopt(curl, CURLOPT_TIMEOUT_MS, static_cast<long>(session.timeoutMs));
	curl_easy_setopt(curl, CURLOPT_SSL_OPTIONS, CURLSSLOPT_NATIVE_CA);
	curl_easy_setopt(curl, CURLOPT_FOLLOWLOCATION, 1L);
	curl_easy_setopt(curl, CURLOPT_NOSIGNAL, 1L);
	curl_easy_setopt(curl, CURLOPT_USERAGENT, "LM Stud/1.0 MCP");
	const CURLcode res = curl_easy_perform(curl);
	curl_easy_getinfo(curl, CURLINFO_RESPONSE_CODE, &exchange.statusCode);
	curl_slist_free_all(headers);
	curl_easy_cleanup(curl);
	if(!exchange.sessionId.empty()) session.httpSessionId = exchange.sessionId;
	if(res != CURLE_OK){
		error = std::string("MCP HTTP request failed: ") + curl_easy_strerror(res);
		return false;
	}
	if(exchange.statusCode == 202){
		if(expectResponse){
			error = "MCP HTTP server returned 202 Accepted without a JSON-RPC response.";
			return false;
		}
		response.clear();
		return true;
	}
	if(exchange.statusCode < 200 || exchange.statusCode >= 300){
		error = "MCP HTTP request failed with status " + std::to_string(exchange.statusCode);
		const auto body = TrimAscii(exchange.body);
		const auto rpcError = ExtractJsonRpcErrorMessage(body);
		if(!rpcError.empty()) error += ": " + rpcError;
		else if(!body.empty()) error += ": " + body;
		return false;
	}
	if(!expectResponse){
		response = TrimAscii(exchange.body);
		return true;
	}
	return DecodeHttpResponseBody(exchange, requestId, response, error);
}
bool SendHttpRequest(McpServerSession& session, const std::string& method, const std::string& paramsJson, std::string& response, std::string& error){
	const int id = session.nextRequestId++;
	std::string request = "{\"jsonrpc\":\"2.0\",\"id\":" + RequestIdJson(id) + ",\"method\":\"" + JsonEscape(method) + "\"";
	if(!paramsJson.empty()) request += ",\"params\":" + paramsJson;
	request += "}";
	if(!HttpPostJson(session, request, true, id, response, error)) return false;
	const auto rpcError = ExtractJsonRpcErrorMessage(response);
	if(!rpcError.empty()){
		error = rpcError;
		return false;
	}
	if(!SameJsonId(response, id)){
		error = "MCP HTTP response did not match the request id.";
		return false;
	}
	return true;
}
bool SendHttpNotification(McpServerSession& session, const std::string& method, const std::string& paramsJson, std::string& error){
	std::string notification = "{\"jsonrpc\":\"2.0\",\"method\":\"" + JsonEscape(method) + "\"";
	if(!paramsJson.empty()) notification += ",\"params\":" + paramsJson;
	notification += "}";
	std::string response;
	return HttpPostJson(session, notification, false, 0, response, error);
}
bool SendRequest(McpServerSession& session, const std::string& method, const std::string& paramsJson, std::string& response, std::string& error){
	return session.transport == McpTransport::Http ? SendHttpRequest(session, method, paramsJson, response, error) : SendStdioRequest(session, method, paramsJson, response, error);
}
bool SendNotification(McpServerSession& session, const std::string& method, const std::string& paramsJson, std::string& error){
	return session.transport == McpTransport::Http ? SendHttpNotification(session, method, paramsJson, error) : SendStdioNotification(session, method, paramsJson, error);
}
std::string BuildInitializeParams(){ return std::string("{\"protocolVersion\":\"") + kProtocolVersion + "\",\"capabilities\":{},\"clientInfo\":{\"name\":\"LM Stud\",\"title\":\"LM Stud\",\"version\":\"1.0\"}}"; }
std::string ExtractResultObject(const std::string& response){
	std::string result;
	if(GetJsonPropertyRaw(response, "result", result)) return result;
	return "{}";
}
bool RefreshToolsLocked(McpServerSession& session, std::string& responseJson, std::string& error){
	std::string response;
	if(!SendRequest(session, "tools/list", "{}", response, error)) return false;
	const auto result = ExtractResultObject(response);
	std::string toolsRaw;
	if(!GetJsonPropertyRaw(result, "tools", toolsRaw)){
		error = "MCP tools/list response did not contain a tools array.";
		return false;
	}
	std::vector<McpTool> tools;
	std::unordered_map<std::string, int> exposedCounts;
	for(const auto& toolObject : ExtractJsonObjects(toolsRaw.c_str())){
		std::string name;
		if(!GetJsonStringProperty(toolObject, "name", name) || name.empty()) continue;
		std::string description;
		GetJsonStringProperty(toolObject, "description", description);
		std::string inputSchema;
		if(!GetJsonPropertyRaw(toolObject, "inputSchema", inputSchema) || IsJsonNull(inputSchema)) inputSchema = "{\"type\":\"object\"}";
		std::string exposed = "mcp_" + NormalizeToolNamePart(session.id) + "_" + NormalizeToolNamePart(name);
		auto count = ++exposedCounts[exposed];
		if(count > 1) exposed += "_" + std::to_string(count);
		tools.push_back({exposed, name, description, inputSchema});
	}
	session.tools = std::move(tools);
	responseJson = BuildInitializeParams();
	return true;
}
std::string BuildToolsJsonLocked(){
	std::string json = "[";
	bool first = true;
	for(const auto& pair : gMCPServers){
		const auto& session = *pair.second;
		if(!session.initialized) continue;
		for(const auto& tool : session.tools){
			if(!first) json += ",";
			first = false;
			json += "{\"type\":\"function\",\"function\":{";
			json += "\"name\":\"" + JsonEscape(tool.exposedName) + "\",";
			json += "\"description\":\"";
			json += JsonEscape("[MCP " + session.id + "] " + tool.description);
			json += "\",\"parameters\":";
			json += tool.inputSchema.empty() ? "{\"type\":\"object\"}" : tool.inputSchema;
			json += "}}";
		}
	}
	json += "]";
	return json;
}
void RegisterToolsForSlotLocked(const char* slotName){
	for(const auto& pair : gMCPServers){
		const auto& session = *pair.second;
		if(!session.initialized) continue;
		for(const auto& tool : session.tools){
			const auto description = "[MCP " + session.id + "] " + tool.description;
			AddTool(slotName, tool.exposedName.c_str(), description.c_str(), tool.inputSchema.empty() ? "{\"type\":\"object\"}" : tool.inputSchema.c_str(), nullptr);
		}
	}
}
McpServerSession* FindSessionForToolLocked(const std::string& exposedName, McpTool& tool){
	for(auto& pair : gMCPServers){
		auto& session = *pair.second;
		const auto it = std::find_if(session.tools.begin(), session.tools.end(), [&](const McpTool& candidate){ return candidate.exposedName == exposedName; });
		if(it == session.tools.end()) continue;
		tool = *it;
		return &session;
	}
	return nullptr;
}
std::string NormalizeToolCallResult(const std::string& response){
	std::string result;
	if(!GetJsonPropertyRaw(response, "result", result)) return response;
	std::string structured;
	if(GetJsonPropertyRaw(result, "structuredContent", structured) && !IsJsonNull(structured)) return structured;
	std::string contentRaw;
	if(!GetJsonPropertyRaw(result, "content", contentRaw)) return result;
	std::string text;
	for(const auto& item : ExtractJsonObjects(contentRaw.c_str())){
		std::string type;
		GetJsonStringProperty(item, "type", type);
		if(type == "text"){
			std::string itemText;
			if(GetJsonStringProperty(item, "text", itemText)){
				if(!text.empty()) text += "\n";
				text += itemText;
			}
			continue;
		}
		if(!text.empty()) text += "\n";
		text += item;
	}
	std::string isErrorRaw;
	if(GetJsonPropertyRaw(result, "isError", isErrorRaw)){
		const char* p = SkipJsonWhitespace(isErrorRaw.c_str());
		if(p && std::strncmp(p, "true", 4) == 0) return ErrorJson(text.empty() ? "MCP tool returned an error." : text);
	}
	return text.empty() ? result : text;
}
void DrainStderr(McpServerSession* session){
	if(!session) return;
	char buffer[4096];
	while(!session->stopStderr.load()){
		DWORD available = 0;
		if(!session->stderrRead || session->stderrRead == INVALID_HANDLE_VALUE) break;
		if(!PeekNamedPipe(session->stderrRead, nullptr, 0, nullptr, &available, nullptr)) break;
		if(available == 0){
			Sleep(100);
			continue;
		}
		DWORD read = 0;
		const DWORD toRead = std::min<DWORD>(available, static_cast<DWORD>(sizeof buffer));
		if(!ReadFile(session->stderrRead, buffer, toRead, &read, nullptr)) break;
		if(read > 0){
			std::lock_guard<std::mutex> lock(session->stderrMutex);
			session->stderrLog.append(buffer, read);
			if(session->stderrLog.size() > 32768) session->stderrLog.erase(0, session->stderrLog.size() - 32768);
		}
	}
}
bool StartProcess(McpServerSession& session, const char* commandLine, const char* workingDirectory, std::string& error){
	SECURITY_ATTRIBUTES sa{};
	sa.nLength = sizeof(sa);
	sa.bInheritHandle = TRUE;
	HANDLE stdinRead = nullptr;
	HANDLE stdoutWrite = nullptr;
	HANDLE stderrWrite = nullptr;
	if(!CreatePipe(&stdinRead, &session.stdinWrite, &sa, 0)){
		error = FormatWin32Error("CreatePipe stdin");
		return false;
	}
	if(!SetHandleInformation(session.stdinWrite, HANDLE_FLAG_INHERIT, 0)){
		error = FormatWin32Error("SetHandleInformation stdin");
		CloseHandleIfValid(stdinRead);
		return false;
	}
	if(!CreatePipe(&session.stdoutRead, &stdoutWrite, &sa, 0)){
		error = FormatWin32Error("CreatePipe stdout");
		CloseHandleIfValid(stdinRead);
		return false;
	}
	if(!SetHandleInformation(session.stdoutRead, HANDLE_FLAG_INHERIT, 0)){
		error = FormatWin32Error("SetHandleInformation stdout");
		CloseHandleIfValid(stdinRead);
		CloseHandleIfValid(stdoutWrite);
		return false;
	}
	if(!CreatePipe(&session.stderrRead, &stderrWrite, &sa, 0)){
		error = FormatWin32Error("CreatePipe stderr");
		CloseHandleIfValid(stdinRead);
		CloseHandleIfValid(stdoutWrite);
		return false;
	}
	if(!SetHandleInformation(session.stderrRead, HANDLE_FLAG_INHERIT, 0)){
		error = FormatWin32Error("SetHandleInformation stderr");
		CloseHandleIfValid(stdinRead);
		CloseHandleIfValid(stdoutWrite);
		CloseHandleIfValid(stderrWrite);
		return false;
	}
	STARTUPINFOA si{};
	si.cb = sizeof(si);
	si.dwFlags = STARTF_USESTDHANDLES;
	si.hStdInput = stdinRead;
	si.hStdOutput = stdoutWrite;
	si.hStdError = stderrWrite;
	PROCESS_INFORMATION pi{};
	std::string command = commandLine ? commandLine : "";
	if(command.empty()){
		error = "MCP command line is empty.";
		CloseHandleIfValid(stdinRead);
		CloseHandleIfValid(stdoutWrite);
		CloseHandleIfValid(stderrWrite);
		return false;
	}
	const BOOL created = CreateProcessA(nullptr, &command[0], nullptr, nullptr, TRUE, CREATE_NO_WINDOW, nullptr, workingDirectory && *workingDirectory ? workingDirectory : nullptr, &si, &pi);
	CloseHandleIfValid(stdinRead);
	CloseHandleIfValid(stdoutWrite);
	CloseHandleIfValid(stderrWrite);
	if(!created){
		error = FormatWin32Error("CreateProcess");
		return false;
	}
	session.process = pi.hProcess;
	CloseHandleIfValid(pi.hThread);
	session.stderrThread = std::thread(DrainStderr, &session);
	return true;
}
std::string ConnectStdio(const char* serverId, const char* commandLine, const char* workingDirectory, const int timeoutMs){
	const std::string id = serverId && *serverId ? serverId : "";
	if(id.empty()) return ErrorJson("MCP server id is empty.");
	auto session = std::make_unique<McpServerSession>();
	session->id = id;
	session->transport = McpTransport::Stdio;
	session->timeoutMs = timeoutMs > 0 ? static_cast<DWORD>(timeoutMs) : kDefaultTimeoutMs;
	std::string error;
	if(!StartProcess(*session, commandLine, workingDirectory, error)){
		CloseSessionHandles(*session, true);
		return ErrorJson(error);
	}
	std::string initResponse;
	if(!SendRequest(*session, "initialize", BuildInitializeParams(), initResponse, error)){
		CloseSessionHandles(*session, true);
		return ErrorJson("MCP initialize failed: " + error);
	}
	session->initializeResult = ExtractResultObject(initResponse);
	if(!SendNotification(*session, "notifications/initialized", "", error)){
		CloseSessionHandles(*session, true);
		return ErrorJson("MCP initialized notification failed: " + error);
	}
	std::string ignored;
	if(!RefreshToolsLocked(*session, ignored, error)){
		CloseSessionHandles(*session, true);
		return ErrorJson("MCP tools/list failed: " + error);
	}
	session->initialized = true;
	std::lock_guard<std::mutex> lock(gMCPMutex);
	const auto existing = gMCPServers.find(id);
	if(existing != gMCPServers.end()){
		CloseSessionHandles(*existing->second, true);
		gMCPServers.erase(existing);
	}
	gMCPServers[id] = std::move(session);
	return OkJson();
}
std::string ConnectHttp(const char* serverId, const char* url, const char* customHeader, const int timeoutMs){
	const std::string id = serverId && *serverId ? serverId : "";
	if(id.empty()) return ErrorJson("MCP server id is empty.");
	const std::string endpoint = url && *url ? url : "";
	if(endpoint.empty()) return ErrorJson("MCP HTTP URL is empty.");
	auto session = std::make_unique<McpServerSession>();
	session->id = id;
	session->transport = McpTransport::Http;
	session->httpUrl = endpoint;
	session->httpCustomHeader = customHeader ? customHeader : "";
	session->timeoutMs = timeoutMs > 0 ? static_cast<DWORD>(timeoutMs) : kDefaultTimeoutMs;
	std::string error;
	std::string initResponse;
	if(!SendRequest(*session, "initialize", BuildInitializeParams(), initResponse, error)) return ErrorJson("MCP initialize failed: " + error);
	session->initializeResult = ExtractResultObject(initResponse);
	if(!SendNotification(*session, "notifications/initialized", "", error)) return ErrorJson("MCP initialized notification failed: " + error);
	std::string ignored;
	if(!RefreshToolsLocked(*session, ignored, error)) return ErrorJson("MCP tools/list failed: " + error);
	session->initialized = true;
	std::lock_guard<std::mutex> lock(gMCPMutex);
	const auto existing = gMCPServers.find(id);
	if(existing != gMCPServers.end()){
		CloseSessionHandles(*existing->second, true);
		gMCPServers.erase(existing);
	}
	gMCPServers[id] = std::move(session);
	return OkJson();
}
char* MCPConnectStdio(const char* serverId, const char* commandLine, const char* workingDirectory, int timeoutMs){
	try{ return CopyCString(ConnectStdio(serverId, commandLine, workingDirectory, timeoutMs)); } catch(const std::exception& ex){ return CopyCString(ErrorJson(ex.what())); } catch(...){ return CopyCString(ErrorJson("MCP connect failed.")); }
}
char* MCPConnectHttp(const char* serverId, const char* url, const char* customHeader, int timeoutMs){
	try{ return CopyCString(ConnectHttp(serverId, url, customHeader, timeoutMs)); } catch(const std::exception& ex){ return CopyCString(ErrorJson(ex.what())); } catch(...){ return CopyCString(ErrorJson("MCP HTTP connect failed.")); }
}
char* MCPDisconnect(const char* serverId){
	try{
		std::lock_guard<std::mutex> lock(gMCPMutex);
		const std::string id = serverId && *serverId ? serverId : "";
		const auto it = gMCPServers.find(id);
		if(it == gMCPServers.end()) return CopyCString(ErrorJson("MCP server is not connected."));
		CloseSessionHandles(*it->second, false);
		gMCPServers.erase(it);
		return CopyCString(OkJson());
	} catch(const std::exception& ex){ return CopyCString(ErrorJson(ex.what())); } catch(...){ return CopyCString(ErrorJson("MCP disconnect failed.")); }
}
void MCPDisconnectAll(){
	std::lock_guard<std::mutex> lock(gMCPMutex);
	for(auto& pair : gMCPServers) CloseSessionHandles(*pair.second, false);
	gMCPServers.clear();
}
char* MCPRefreshTools(const char* serverId){
	try{
		std::lock_guard<std::mutex> lock(gMCPMutex);
		const std::string id = serverId && *serverId ? serverId : "";
		const auto it = gMCPServers.find(id);
		if(it == gMCPServers.end()) return CopyCString(ErrorJson("MCP server is not connected."));
		std::string responseJson;
		std::string error;
		if(!RefreshToolsLocked(*it->second, responseJson, error)) return CopyCString(ErrorJson(error));
		return CopyCString(OkJson());
	} catch(const std::exception& ex){ return CopyCString(ErrorJson(ex.what())); } catch(...){ return CopyCString(ErrorJson("MCP tools refresh failed.")); }
}
char* MCPBuildToolsJson(){
	try{
		std::lock_guard<std::mutex> lock(gMCPMutex);
		return CopyCString(BuildToolsJsonLocked());
	} catch(const std::exception& ex){ return CopyCString(ErrorJson(ex.what())); } catch(...){ return CopyCString(ErrorJson("MCP tools serialization failed.")); }
}
void MCPRegisterToolsForSlot(const char* slotName){
	try{
		std::lock_guard<std::mutex> lock(gMCPMutex);
		RegisterToolsForSlotLocked(slotName);
	} catch(...){}
}
bool MCPHasTool(const char* exposedName){
	std::lock_guard<std::mutex> lock(gMCPMutex);
	McpTool tool;
	return exposedName && FindSessionForToolLocked(exposedName, tool) != nullptr;
}
char* MCPExecuteTool(const char* exposedName, const char* argumentsJson){
	try{
		std::lock_guard<std::mutex> lock(gMCPMutex);
		McpTool tool;
		auto* session = exposedName ? FindSessionForToolLocked(exposedName, tool) : nullptr;
		if(!session) return CopyCString(ErrorJson("unknown MCP tool"));
		const auto args = CompactJson(argumentsJson);
		const auto params = "{\"name\":\"" + JsonEscape(tool.name) + "\",\"arguments\":" + args + "}";
		std::string response;
		std::string error;
		if(!SendRequest(*session, "tools/call", params, response, error)) return CopyCString(ErrorJson(error));
		return CopyCString(NormalizeToolCallResult(response));
	} catch(const std::exception& ex){ return CopyCString(ErrorJson(ex.what())); } catch(...){ return CopyCString(ErrorJson("MCP tool execution failed.")); }
}
char* MCPListServers(){
	try{
		std::lock_guard<std::mutex> lock(gMCPMutex);
		std::string json = "[";
		bool first = true;
		for(const auto& pair : gMCPServers){
			if(!first) json += ",";
			first = false;
			json += "{\"id\":\"" + JsonEscape(pair.first) + "\",\"initialized\":";
			json += pair.second->initialized ? "true" : "false";
			json += ",\"transport\":\"";
			json += pair.second->transport == McpTransport::Http ? "http" : "stdio";
			json += "\"";
			json += ",\"tool_count\":" + std::to_string(pair.second->tools.size()) + "}";
		}
		json += "]";
		return CopyCString(json);
	} catch(const std::exception& ex){ return CopyCString(ErrorJson(ex.what())); } catch(...){ return CopyCString(ErrorJson("MCP server listing failed.")); }
}
