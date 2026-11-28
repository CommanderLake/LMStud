#define CURL_STATICLIB
#include "MCP.h"
#include "stud.h"
#include <Windows.h>
#include <algorithm>
#include <atomic>
#include <chrono>
#include <cmath>
#include <cstdint>
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <curl\curl.h>
#include <functional>
#include <map>
#include <memory>
#include <mutex>
#include <nlohmann/json.hpp>
#include <set>
#include <sstream>
#include <string>
#include <thread>
#include <vector>

extern std::string FormatWin32Error(const char* context);
void ClearMcpToolSchemas(const char* slotName);

namespace{
	using Json = nlohmann::json;
	using Clock = std::chrono::steady_clock;
	using Deadline = Clock::time_point;
	constexpr char kModernVersion[] = "2026-07-28";
	constexpr char kLegacyVersion[] = "2025-11-25";
	constexpr int kDefaultTimeout = 30000;
	constexpr size_t kMaxMessageSize = 32 * 1024 * 1024;
	const Json kClientInfo = {{"name", "LM Stud"}, {"version", "1.0"}};

	enum class Transport{ Stdio, Http };
	struct MirroredParameter{
		std::vector<std::string> path;
		std::string header;
		std::string type;
	};
	struct Tool{
		std::string name;
		std::string alias;
		std::string description;
		Json schema;
		std::vector<MirroredParameter> headers;
	};
	struct HttpBinding{
		std::string version = kModernVersion;
		std::string sessionId;
	};
	struct Catalog{
		bool ready = false;
		std::string version;
		std::string name;
		std::string error;
		std::vector<Tool> tools;
	};
	struct Server{
		Server(std::string id, const Transport transport, const int timeout) :
			id(std::move(id)), transport(transport), timeout(timeout > 0 ? timeout : kDefaultTimeout){}
		const std::string id;
		const Transport transport;
		const int timeout;
		std::string url;
		std::vector<std::string> headers;
		std::mutex requests;
		std::thread watcher;
		std::atomic_bool cancelled{false};
		std::atomic_bool refresh{false};
		std::atomic_uint expiredBinding{0};
		std::atomic_uint bindingRevision{1};
		// Transport state is used under requests. The HTTP watcher uses a copy of its binding.
		HttpBinding binding;
		uint64_t nextId = 1;
		bool hasTools = false;
		bool listChanged = false;
		uint64_t subscription = 0;
		bool subscriptionAcknowledged = false;
		std::string name;
		HANDLE process = nullptr;
		HANDLE input = nullptr;
		HANDLE output = nullptr;
		HANDLE diagnostics = nullptr;
		std::string pending;
		std::string stderrTail;
		// Published state is used under serversMutex. It never waits for transport I/O.
		Catalog catalog;
	};
	std::mutex serversMutex;
	std::map<std::string, std::shared_ptr<Server>> servers;
	std::atomic_uint catalogRevision{0};
	bool acceptingConnections = true;

	std::string Dump(const Json& value){ return value.dump(-1, ' ', false, Json::error_handler_t::replace); }
	char* Copy(const std::string& value){
		auto* buffer = static_cast<char*>(std::malloc(value.size() + 1));
		if(buffer) std::memcpy(buffer, value.c_str(), value.size() + 1);
		return buffer;
	}
	std::string Error(const std::string& message){ return Dump({{"error", message}}); }
	std::string Ok(){ return "{\"ok\":true}"; }
	std::string Trim(const std::string& text){
		const auto first = text.find_first_not_of(" \t\r\n");
		return first == std::string::npos ? std::string() : text.substr(first, text.find_last_not_of(" \t\r\n") - first + 1);
	}
	std::string Lower(std::string text){
		for(auto& ch : text) if(ch >= 'A' && ch <= 'Z') ch += 'a' - 'A';
		return text;
	}
	const Json& Field(const Json& value, const std::string& key){
		static const Json missing;
		if(!value.is_object()) return missing;
		const auto it = value.find(key);
		return it == value.end() ? missing : *it;
	}
	std::string Text(const Json& value, const char* key){
		const auto& field = Field(value, key);
		return field.is_string() ? field.get<std::string>() : std::string();
	}
	Deadline Until(const int timeout){ return Clock::now() + std::chrono::milliseconds(timeout); }
	long Remaining(const Deadline deadline){
		return static_cast<long>(std::max<int64_t>(0, std::chrono::duration_cast<std::chrono::milliseconds>(deadline - Clock::now()).count()));
	}
	std::string WaitError(const Server& server){ return server.cancelled ? "MCP connection was closed." : "MCP request timed out."; }
	bool IsModern(const HttpBinding& binding){ return binding.version == kModernVersion; }
	bool IsResponse(const Json& message, const uint64_t id){
		const auto& responseId = Field(message, "id");
		return id != 0 && responseId.is_number_integer() && responseId == id && Text(message, "jsonrpc") == "2.0"
			&& !message.contains("method") && (message.contains("result") != message.contains("error"));
	}
	Json Request(const HttpBinding& binding, const char* method, Json params, const uint64_t id = 0){
		if(IsModern(binding)){
			params["_meta"]["io.modelcontextprotocol/protocolVersion"] = binding.version;
			params["_meta"]["io.modelcontextprotocol/clientCapabilities"] = Json::object();
			params["_meta"]["io.modelcontextprotocol/clientInfo"] = kClientInfo;
		}
		Json message = {{"jsonrpc", "2.0"}, {"method", method}};
		if(id) message["id"] = id;
		if(!params.empty()) message["params"] = std::move(params);
		return message;
	}
	Json ServerReply(const Json& request){
		const auto& id = Field(request, "id");
		if(Text(request, "jsonrpc") != "2.0" || (!id.is_string() && !id.is_number_integer()) || !Field(request, "method").is_string()) return {};
		Json reply = {{"jsonrpc", "2.0"}, {"id", id}};
		if(Text(request, "method") == "ping") reply["result"] = Json::object();
		else reply["error"] = {{"code", -32601}, {"message", "LM Stud does not support this client method."}};
		return reply;
	}
	bool ToolsChanged(const Json& message){
		return Text(message, "jsonrpc") == "2.0" && Text(message, "method") == "notifications/tools/list_changed" && !message.contains("id");
	}
	void CloseHandleIfSet(HANDLE& handle){
		if(handle) CloseHandle(handle);
		handle = nullptr;
	}
	void CloseStdio(Server& server){
		CloseHandleIfSet(server.input);
		if(server.process && WaitForSingleObject(server.process, 500) == WAIT_TIMEOUT){
			TerminateProcess(server.process, 1);
			WaitForSingleObject(server.process, 500);
		}
		CloseHandleIfSet(server.process);
		CloseHandleIfSet(server.output);
		CloseHandleIfSet(server.diagnostics);
	}
	std::wstring Wide(const std::string& value){
		if(value.empty()) return {};
		const auto size = static_cast<int>(value.size());
		const int count = MultiByteToWideChar(CP_UTF8, MB_ERR_INVALID_CHARS, value.data(), size, nullptr, 0);
		if(!count) throw std::invalid_argument("MCP command or working directory is not valid UTF-8.");
		std::wstring result(count, L'\0');
		MultiByteToWideChar(CP_UTF8, MB_ERR_INVALID_CHARS, value.data(), size, &result[0], count);
		return result;
	}
	bool StartStdio(Server& server, const std::string& command, const std::string& directory, std::string& error){
		auto wideCommand = Wide(command);
		const auto cwd = Wide(directory);
		if(wideCommand.empty()){
			error = "MCP command line is empty.";
			return false;
		}
		SIZE_T size = 0;
		InitializeProcThreadAttributeList(nullptr, 1, 0, &size);
		const auto storage = std::make_unique<unsigned char[]>(size);
		SECURITY_ATTRIBUTES sa{sizeof(sa), nullptr, TRUE};
		STARTUPINFOEXW si{};
		si.StartupInfo.cb = sizeof(si);
		si.StartupInfo.dwFlags = STARTF_USESTDHANDLES;
		auto& input = si.StartupInfo.hStdInput;
		auto& output = si.StartupInfo.hStdOutput;
		auto& diagnostics = si.StartupInfo.hStdError;
		DWORD mode = PIPE_NOWAIT;
		bool ok = CreatePipe(&input, &server.input, &sa, 0) && CreatePipe(&server.output, &output, &sa, 0)
			&& CreatePipe(&server.diagnostics, &diagnostics, &sa, 0) && SetNamedPipeHandleState(server.input, &mode, nullptr, nullptr)
			&& SetHandleInformation(server.input, HANDLE_FLAG_INHERIT, 0) && SetHandleInformation(server.output, HANDLE_FLAG_INHERIT, 0)
			&& SetHandleInformation(server.diagnostics, HANDLE_FLAG_INHERIT, 0);
		if(!ok) error = FormatWin32Error("MCP stdio pipes");
		// Restrict this child to its own pipe ends even when other servers are starting.
		si.lpAttributeList = reinterpret_cast<LPPROC_THREAD_ATTRIBUTE_LIST>(storage.get());
		if(ok && !InitializeProcThreadAttributeList(si.lpAttributeList, 1, 0, &size)){
			error = FormatWin32Error("MCP process attributes");
			ok = false;
		} else if(ok){
			PROCESS_INFORMATION pi{};
			HANDLE handles[]{input, output, diagnostics};
			ok = UpdateProcThreadAttribute(si.lpAttributeList, 0, PROC_THREAD_ATTRIBUTE_HANDLE_LIST, handles, sizeof(handles), nullptr, nullptr)
				&& CreateProcessW(nullptr, &wideCommand[0], nullptr, nullptr, TRUE, EXTENDED_STARTUPINFO_PRESENT | CREATE_NO_WINDOW,
					nullptr, cwd.empty() ? nullptr : cwd.c_str(), &si.StartupInfo, &pi);
			if(!ok) error = FormatWin32Error("MCP process launch");
			else server.process = pi.hProcess;
			CloseHandleIfSet(pi.hThread);
			DeleteProcThreadAttributeList(si.lpAttributeList);
		}
		CloseHandleIfSet(input);
		CloseHandleIfSet(output);
		CloseHandleIfSet(diagnostics);
		return ok;
	}
	bool ReadPipe(const HANDLE pipe, std::string& buffer, const bool tail){
		DWORD available = 0;
		if(!PeekNamedPipe(pipe, nullptr, 0, nullptr, &available, nullptr)) return false;
		char data[4096];
		DWORD count = 0;
		if(available && !ReadFile(pipe, data, std::min<DWORD>(available, sizeof(data)), &count, nullptr)) return false;
		buffer.append(data, count);
		if(tail && buffer.size() > 4096) buffer.erase(0, buffer.size() - 4096);
		return true;
	}
	bool PumpStdio(Server& server, std::string& error){
		ReadPipe(server.diagnostics, server.stderrTail, true);
		const bool read = ReadPipe(server.output, server.pending, false);
		if(read && server.pending.size() <= kMaxMessageSize) return true;
		error = read ? "MCP stdio message is too large." : "MCP server closed its output.";
		DWORD exitCode = STILL_ACTIVE;
		if(server.process && GetExitCodeProcess(server.process, &exitCode) && exitCode != STILL_ACTIVE)
			error = "MCP process exited with code " + std::to_string(exitCode) + ".";
		if(!server.stderrTail.empty()) error += "\n" + Trim(server.stderrTail);
		return false;
	}
	bool PopStdio(Server& server, Json& message){
		const auto lf = server.pending.find('\n');
		if(lf == std::string::npos) return false;
		message = Json::parse(server.pending.begin(), server.pending.begin() + lf, nullptr, false);
		server.pending.erase(0, lf + 1);
		return true;
	}
	bool WriteStdio(Server& server, const Json& message, const Deadline deadline, std::string& error){
		const auto line = Dump(message) + "\n";
		size_t offset = 0;
		while(!server.cancelled && Remaining(deadline)){
			DWORD written = 0;
			if(!WriteFile(server.input, line.data() + offset, static_cast<DWORD>(std::min<size_t>(line.size() - offset, 65536)), &written, nullptr)){
				error = FormatWin32Error("MCP stdin");
				if(offset) CloseStdio(server);
				return false;
			}
			offset += written;
			if(offset == line.size()) return true;
			if(!PumpStdio(server, error)){
				CloseStdio(server);
				return false;
			}
			if(written == 0) Sleep(5);
		}
		error = WaitError(server);
		// A partial JSON line cannot be resumed with another request.
		if(offset) CloseStdio(server);
		return false;
	}
	bool HandleSubscription(const Json& message, const uint64_t id, bool& acknowledged, Server& server){
		const auto& params = Field(message, "params");
		const auto& subscription = Field(Field(params, "_meta"), "io.modelcontextprotocol/subscriptionId");
		if(Text(message, "jsonrpc") != "2.0" || !subscription.is_number_integer() || subscription != id || message.contains("id")) return false;
		if(Text(message, "method") == "notifications/subscriptions/acknowledged"){
			acknowledged = Field(Field(params, "notifications"), "toolsListChanged") == true;
			return acknowledged;
		}
		if(acknowledged && ToolsChanged(message)) server.refresh = true;
		return false;
	}
	void ReceiveStdio(Server& server, const Json& message, const Deadline deadline){
		if(IsModern(server.binding)){
			if(server.subscription && IsResponse(message, server.subscription)){
				server.subscription = 0;
				server.subscriptionAcknowledged = false;
			} else if(server.subscription && HandleSubscription(message, server.subscription, server.subscriptionAcknowledged, server)){
				server.refresh = true;
			}
			return;
		}
		if(server.listChanged && ToolsChanged(message)) server.refresh = true;
		const auto response = ServerReply(message);
		std::string ignored;
		if(!response.is_null()) WriteStdio(server, response, deadline, ignored);
	}
	Json ExchangeStdio(Server& server, const Json& request, const uint64_t id, const Deadline deadline, std::string& error){
		if(!WriteStdio(server, request, deadline, error)) return {};
		while(!server.cancelled && Remaining(deadline)){
			Json message;
			while(Remaining(deadline) && !server.cancelled && PopStdio(server, message)){
				if(IsResponse(message, id)) return message;
				ReceiveStdio(server, message, deadline);
			}
			const auto size = server.pending.size();
			if(!PumpStdio(server, error)) return {};
			if(server.pending.size() == size) Sleep(5);
		}
		error = WaitError(server);
		if(!server.cancelled && Text(request, "method") != "initialize"){
			std::string ignored;
			WriteStdio(server, Request(server.binding, "notifications/cancelled", {{"requestId", id}}), Until(50), ignored);
		}
		return {};
	}

	using Url = std::unique_ptr<CURLU, decltype(&curl_url_cleanup)>;
	std::string UrlPart(CURLU* url, const CURLUPart part, const unsigned int flags = 0){
		char* raw = nullptr;
		curl_url_get(url, part, &raw, flags);
		const std::unique_ptr<char, decltype(&curl_free)> data(raw, curl_free);
		return raw ? raw : "";
	}
	bool ParseHttpUrl(CURLU* url, const std::string& value){
		if(!url || curl_url_set(url, CURLUPART_URL, value.c_str(), CURLU_DISALLOW_USER) != CURLUE_OK) return false;
		const auto scheme = UrlPart(url, CURLUPART_SCHEME);
		return scheme == "https" || scheme == "http";
	}
	bool SameOrigin(const std::string& first, const std::string& second){
		Url a(curl_url(), curl_url_cleanup);
		Url b(curl_url(), curl_url_cleanup);
		return ParseHttpUrl(a.get(), first) && ParseHttpUrl(b.get(), second)
			&& UrlPart(a.get(), CURLUPART_SCHEME) == UrlPart(b.get(), CURLUPART_SCHEME)
			&& Lower(UrlPart(a.get(), CURLUPART_HOST)) == Lower(UrlPart(b.get(), CURLUPART_HOST))
			&& UrlPart(a.get(), CURLUPART_PORT, CURLU_DEFAULT_PORT) == UrlPart(b.get(), CURLUPART_PORT, CURLU_DEFAULT_PORT);
	}
	bool HeaderToken(const std::string& name){
		return !name.empty() && name.find_first_not_of("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!#$%&'*+-.^_\x60|~") == std::string::npos;
	}
	bool VisibleAscii(const std::string& value){
		return std::all_of(value.begin(), value.end(), [](const unsigned char c){ return (c >= ' ' && c <= '~') || c == '\t'; });
	}
	std::vector<std::string> ParseHeaders(const std::string& value){
		std::vector<std::string> headers;
		std::istringstream stream(value);
		std::string line;
		while(std::getline(stream, line)){
			line = Trim(line);
			if(line.empty()) continue;
			if(line.find(':') == std::string::npos) line = "Authorization: Bearer " + line;
			const auto name = Lower(Trim(line.substr(0, line.find(':'))));
			if(!VisibleAscii(line) || !HeaderToken(name) || name == "accept" || name == "content-type" || name == "content-length"
				|| name == "host" || name == "transfer-encoding" || name.compare(0, 4, "mcp-") == 0)
				throw std::invalid_argument("The MCP authentication headers contain an invalid or reserved header.");
			headers.push_back(std::move(line));
		}
		return headers;
	}
	std::string EncodeHeader(const std::string& text){
		constexpr char alphabet[] = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";
		const bool sentinel = text.size() >= 11 && text.compare(0, 9, "=?base64?") == 0 && text.compare(text.size() - 2, 2, "?=") == 0;
		if(VisibleAscii(text) && Trim(text) == text && !sentinel) return text;
		std::string encoded = "=?base64?";
		for(size_t i = 0; i < text.size(); i += 3){
			const auto a = static_cast<unsigned char>(text[i]);
			const auto b = i + 1 < text.size() ? static_cast<unsigned char>(text[i + 1]) : 0;
			const auto c = i + 2 < text.size() ? static_cast<unsigned char>(text[i + 2]) : 0;
			encoded += alphabet[a >> 2];
			encoded += alphabet[((a & 3) << 4) | (b >> 4)];
			encoded += i + 1 < text.size() ? alphabet[((b & 15) << 2) | (c >> 6)] : '=';
			encoded += i + 2 < text.size() ? alphabet[c & 63] : '=';
		}
		return encoded + "?=";
	}
	std::string Header(const std::string& name, const std::string& value){ return value.empty() ? name + ";" : name + ": " + value; }

	using Receive = std::function<bool(const Json&, const std::string&)>;
	struct HttpExchange{
		const Receive& receive;
		long status = 0;
		bool sse = false;
		bool stopped = false;
		bool skipLf = false;
		std::string sessionId;
		std::string body;
		std::string line;
		std::string data;

		bool FinishLine(){
			if(line.compare(0, 3, "\xEF\xBB\xBF") == 0) line.erase(0, 3);
			if(line.empty()){
				if(!data.empty()){
					data.pop_back();
					const auto event = Json::parse(data, nullptr, false);
					data.clear();
					if(event.is_object() && !receive(event, sessionId)) return false;
				}
			} else if(line == "data" || line.compare(0, 5, "data:") == 0){
				auto start = std::min<size_t>(line.size(), 5);
				if(start < line.size() && line[start] == ' ') ++start;
				data.append(line, start, std::string::npos);
				data += '\n';
			}
			line.clear();
			return true;
		}
	};
	size_t ReadHttpHeader(char* bytes, size_t size, size_t count, void* context){
		auto& exchange = *static_cast<HttpExchange*>(context);
		try{
			const std::string line(bytes, size * count);
			if(line.compare(0, 5, "HTTP/") == 0){
				const auto space = line.find(' ');
				if(space != std::string::npos) exchange.status = std::strtol(line.c_str() + space, nullptr, 10);
				exchange.sse = false;
				exchange.sessionId.clear();
			} else{
				const auto colon = line.find(':');
				if(colon != std::string::npos){
					const auto name = Lower(line.substr(0, colon));
					const auto value = Trim(line.substr(colon + 1));
					if(name == "content-type") exchange.sse = Lower(value).compare(0, 17, "text/event-stream") == 0;
					else if(name == "mcp-session-id" && value.find_first_of(" \t") == std::string::npos && VisibleAscii(value)) exchange.sessionId = value;
				}
			}
			return size * count;
		} catch(...){ return 0; }
	}
	size_t ReadHttpBody(char* bytes, size_t size, size_t count, void* context){
		auto& exchange = *static_cast<HttpExchange*>(context);
		const size_t length = size * count;
		try{
			if(!exchange.sse || !exchange.receive || exchange.status < 200 || exchange.status >= 300){
				if(exchange.body.size() + length > kMaxMessageSize) return 0;
				exchange.body.append(bytes, length);
				return length;
			}
			for(size_t i = 0; i < length; ++i){
				const char c = bytes[i];
				if(exchange.skipLf){
					exchange.skipLf = false;
					if(c == '\n') continue;
				}
				if(c == '\r' || c == '\n'){
					exchange.skipLf = c == '\r';
					if(!exchange.FinishLine()){
						exchange.stopped = true;
						return 0;
					}
				} else exchange.line += c;
				if(exchange.line.size() + exchange.data.size() > kMaxMessageSize) return 0;
			}
			return length;
		} catch(...){ return 0; }
	}
	struct HttpProgress{
		const Server& server;
		bool cleanup;
		unsigned int revision;
		Deadline deadline;
		const HttpExchange& exchange;
		bool listen;
	};
	int CheckHttpProgress(void* context, curl_off_t, curl_off_t, curl_off_t, curl_off_t){
		const auto& progress = *static_cast<HttpProgress*>(context);
		return (!progress.cleanup && progress.server.cancelled) || progress.server.bindingRevision != progress.revision
			|| ((!progress.listen || !progress.exchange.sse || progress.exchange.status != 200) && Clock::now() >= progress.deadline);
	}
	struct HttpResult{
		long status = 0;
		std::string sessionId;
		std::string body;
		std::string error;
	};
	HttpResult ExchangeHttp(const Server& server, const HttpBinding& binding, const char* verb, const Json& message,
							const Deadline deadline, const Receive& receive = {}, const std::vector<std::string>& extra = {},
							const bool listen = false, const bool cleanup = false, const unsigned int revision = 0){
		using Easy = std::unique_ptr<CURL, decltype(&curl_easy_cleanup)>;
		struct Headers{
			curl_slist* list = nullptr;
			~Headers(){ curl_slist_free_all(list); }
			void Add(const std::string& value){
				const auto appended = curl_slist_append(list, value.c_str());
				if(!appended) throw std::bad_alloc();
				list = appended;
			}
		} headers;
		const auto method = Text(message, "method");
		const bool post = std::strcmp(verb, "POST") == 0;
		headers.Add(std::strcmp(verb, "GET") == 0 ? "Accept: text/event-stream" : "Accept: application/json, text/event-stream");
		if(post) headers.Add("Content-Type: application/json");
		if(method != "initialize") headers.Add("MCP-Protocol-Version: " + binding.version);
		if(IsModern(binding) && !method.empty()) headers.Add("Mcp-Method: " + method);
		if(IsModern(binding) && method == "tools/call") headers.Add(Header("Mcp-Name", EncodeHeader(Text(Field(message, "params"), "name"))));
		if(!IsModern(binding) && !binding.sessionId.empty()) headers.Add("Mcp-Session-Id: " + binding.sessionId);
		for(const auto& header : server.headers) headers.Add(header);
		for(const auto& header : extra) headers.Add(header);
		const auto body = post ? Dump(message) : "";
		std::string url = server.url;
		HttpResult result;
		for(int redirects = 0; redirects <= 4; ++redirects){
			if((!cleanup && server.cancelled) || !Remaining(deadline)){
				result.error = WaitError(server);
				return result;
			}
			Easy curl(curl_easy_init(), curl_easy_cleanup);
			if(!curl){
				result.error = "Failed to initialize libcurl.";
				return result;
			}
			HttpExchange exchange{receive};
			HttpProgress progress{server, cleanup, listen ? revision : server.bindingRevision.load(), deadline, exchange, listen};
			curl_easy_setopt(curl.get(), CURLOPT_URL, url.c_str());
			curl_easy_setopt(curl.get(), CURLOPT_PROTOCOLS_STR, "http,https");
			curl_easy_setopt(curl.get(), CURLOPT_HTTPHEADER, headers.list);
			curl_easy_setopt(curl.get(), CURLOPT_CUSTOMREQUEST, verb);
			if(post){
				curl_easy_setopt(curl.get(), CURLOPT_POSTFIELDS, body.c_str());
				curl_easy_setopt(curl.get(), CURLOPT_POSTFIELDSIZE_LARGE, static_cast<curl_off_t>(body.size()));
			}
			curl_easy_setopt(curl.get(), CURLOPT_WRITEFUNCTION, ReadHttpBody);
			curl_easy_setopt(curl.get(), CURLOPT_WRITEDATA, &exchange);
			curl_easy_setopt(curl.get(), CURLOPT_HEADERFUNCTION, ReadHttpHeader);
			curl_easy_setopt(curl.get(), CURLOPT_HEADERDATA, &exchange);
			curl_easy_setopt(curl.get(), CURLOPT_NOPROGRESS, 0L);
			curl_easy_setopt(curl.get(), CURLOPT_XFERINFOFUNCTION, CheckHttpProgress);
			curl_easy_setopt(curl.get(), CURLOPT_XFERINFODATA, &progress);
			curl_easy_setopt(curl.get(), CURLOPT_CONNECTTIMEOUT_MS, std::max(1L, Remaining(deadline)));
			if(!listen) curl_easy_setopt(curl.get(), CURLOPT_TIMEOUT_MS, std::max(1L, Remaining(deadline)));
			curl_easy_setopt(curl.get(), CURLOPT_SSL_OPTIONS, CURLSSLOPT_NATIVE_CA);
			curl_easy_setopt(curl.get(), CURLOPT_NOSIGNAL, 1L);
			curl_easy_setopt(curl.get(), CURLOPT_USERAGENT, "LM Stud/1.0 MCP");
			const CURLcode status = curl_easy_perform(curl.get());
			result.status = exchange.status;
			if(status != CURLE_OK && !exchange.stopped){
				result.error = server.cancelled && !cleanup ? WaitError(server) : std::string("MCP HTTP request failed: ") + curl_easy_strerror(status);
				return result;
			}
			char* redirect = nullptr;
			curl_easy_getinfo(curl.get(), CURLINFO_REDIRECT_URL, &redirect);
			if(exchange.status >= 300 && exchange.status < 400 && redirect){
				if(!SameOrigin(url, redirect)){
					result.error = "The MCP server redirected to a different origin. Configure its final endpoint URL.";
					return result;
				}
				url = redirect;
				continue;
			}
			result.body = std::move(exchange.body);
			result.sessionId = std::move(exchange.sessionId);
			return result;
		}
		result.error = "Too many redirects from the MCP server.";
		return result;
	}
	struct Reply{
		Json data;
		Json details;
		std::string error;
		long status = 0;
		int code = 0;
		bool rpcError = false;
		bool timeout = false;

		explicit operator bool() const{ return error.empty(); }
	};
	Reply Call(Server& server, const char* method, Json params, const Deadline deadline, const std::vector<std::string>& headers = {}){
		const auto id = server.nextId++;
		const auto request = Request(server.binding, method, std::move(params), id);
		Json message;
		Reply reply;
		std::string sessionId;
		if(server.transport == Transport::Stdio){
			message = ExchangeStdio(server, request, id, deadline, reply.error);
			reply.timeout = !Remaining(deadline);
		} else{
			const auto http = ExchangeHttp(server, server.binding, "POST", request, deadline, [&](const Json& event, const std::string& session){
				if(IsResponse(event, id)){
					message = event;
					return false;
				}
				if(!IsModern(server.binding)){
					if(server.listChanged && ToolsChanged(event)) server.refresh = true;
					const auto response = ServerReply(event);
					if(!response.is_null()){
						auto binding = server.binding;
						if(binding.sessionId.empty()) binding.sessionId = session;
						ExchangeHttp(server, binding, "POST", response, deadline);
					}
				}
				return true;
			}, headers);
			reply.status = http.status;
			reply.error = http.error;
			sessionId = http.sessionId;
			if(message.is_null()) message = Json::parse(http.body, nullptr, false);
		}
		if(!reply.error.empty()) return reply;
		const bool matched = IsResponse(message, id);
		const auto& error = Field(message, "error");
		const auto& code = Field(error, "code");
		// Modern HTTP validation errors can be returned before the server reads the request ID.
		if(code.is_number_integer() && (matched || (reply.status == 400 && Field(message, "id").is_null() && Text(message, "jsonrpc") == "2.0"))){
			reply.rpcError = true;
			reply.code = code.get<int>();
			reply.details = Field(error, "data");
			reply.error = Text(error, "message");
			if(reply.error.empty()) reply.error = "MCP returned JSON-RPC error " + std::to_string(reply.code) + ".";
		} else if(reply.status && (reply.status < 200 || reply.status >= 300)){
			reply.error = "MCP HTTP request failed with status " + std::to_string(reply.status) + ".";
		} else if(!matched || !Field(message, "result").is_object()){
			reply.error = "MCP did not return a matching JSON-RPC result.";
		} else{
			reply.data = std::move(message["result"]);
			if(std::strcmp(method, "initialize") == 0) server.binding.sessionId = std::move(sessionId);
		}
		return reply;
	}
	bool Notify(Server& server, const Json& message, const Deadline deadline, std::string& error){
		if(server.transport == Transport::Stdio) return WriteStdio(server, message, deadline, error);
		const auto reply = ExchangeHttp(server, server.binding, "POST", message, deadline);
		if(!reply.error.empty()) error = reply.error;
		else if(reply.status < 200 || reply.status >= 300) error = "MCP notification failed with HTTP status " + std::to_string(reply.status) + ".";
		return error.empty();
	}

	bool Supports(const Json& versions, const std::string& version){
		return versions.is_array() && std::find(versions.begin(), versions.end(), Json(version)) != versions.end();
	}
	bool IsLegacyVersion(const std::string& version, const Transport transport){
		return version == kLegacyVersion || version == "2025-06-18" || version == "2025-03-26"
			|| (version == "2024-11-05" && transport == Transport::Stdio);
	}
	void SetCapabilities(Server& server, const Json& result, const Json& info){
		const auto& tools = Field(Field(result, "capabilities"), "tools");
		server.hasTools = tools.is_object();
		server.listChanged = Field(tools, "listChanged") == true;
		server.name = Text(info, "title");
		if(server.name.empty()) server.name = Text(info, "name");
	}
	bool InitializeLegacy(Server& server, const std::string& offered, const Deadline deadline, std::string& error){
		server.binding = {offered, ""};
		server.bindingRevision++;
		const auto reply = Call(server, "initialize", {{"protocolVersion", offered}, {"capabilities", Json::object()}, {"clientInfo", kClientInfo}}, deadline);
		if(!reply){
			error = "MCP initialize failed: " + reply.error;
			return false;
		}
		const auto selected = Text(reply.data, "protocolVersion");
		if(!IsLegacyVersion(selected, server.transport)){
			error = "Unsupported MCP protocol version: " + selected;
			return false;
		}
		server.binding.version = selected;
		SetCapabilities(server, reply.data, Field(reply.data, "serverInfo"));
		return Notify(server, Request(server.binding, "notifications/initialized", Json::object()), deadline, error);
	}
	bool Discover(Server& server, std::string& error){
		const auto probe = Call(server, "server/discover", Json::object(), Until(server.timeout));
		if(probe){
			if(!Supports(Field(probe.data, "supportedVersions"), kModernVersion)){
				error = "The MCP server has no compatible stateless protocol version.";
				return false;
			}
			SetCapabilities(server, probe.data, Field(Field(probe.data, "_meta"), "io.modelcontextprotocol/serverInfo"));
			return true;
		}
		if(probe.code == -32022){
			const auto& versions = Field(probe.details, "supported");
			for(const auto version : {kLegacyVersion, "2025-06-18", "2025-03-26", "2024-11-05"})
				if(IsLegacyVersion(version, server.transport) && Supports(versions, version)) return InitializeLegacy(server, version, Until(server.timeout), error);
		}
		const bool legacy = probe.code != -32020 && probe.code != -32021 && probe.code != -32022
			&& (server.transport == Transport::Stdio ? (probe.timeout || probe.rpcError)
				: (probe.status == 400 || probe.status == 404 || probe.status == 405 || probe.status == 422
					|| (probe.status >= 200 && probe.status < 300 && (probe.code == -32601 || probe.code == -32600 || probe.code == -32602))));
		if(legacy && !server.cancelled) return InitializeLegacy(server, kLegacyVersion, Until(server.timeout), error);
		error = "MCP discovery failed: " + probe.error;
		return false;
	}
	bool HasAnnotation(const Json& node){
		if(node.is_object() && node.contains("x-mcp-header")) return true;
		return (node.is_object() || node.is_array()) && std::any_of(node.begin(), node.end(), HasAnnotation);
	}
	bool ReadAnnotations(const Json& node, std::vector<std::string>& path, std::vector<MirroredParameter>& headers, std::set<std::string>& names){
		if(!node.is_object()) return !HasAnnotation(node);
		if(node.contains("x-mcp-header")){
			const auto name = Text(node, "x-mcp-header");
			const auto type = Text(node, "type");
			if(path.empty() || !HeaderToken(name) || (type != "string" && type != "integer" && type != "boolean") || !names.insert(Lower(name)).second) return false;
			headers.push_back({path, "Mcp-Param-" + name, type});
		}
		for(auto property = node.begin(); property != node.end(); ++property){
			const auto& key = property.key();
			if(key == "properties" && property->is_object()){
				for(auto it = property->begin(); it != property->end(); ++it){
					path.push_back(it.key());
					if(!ReadAnnotations(*it, path, headers, names)) return false;
					path.pop_back();
				}
			} else if(key != "default" && key != "examples" && key != "enum" && key != "const" && HasAnnotation(*property)) return false;
		}
		return true;
	}
	bool ToolHeaders(const Tool& tool, const Json& arguments, std::vector<std::string>& headers, std::string& error){
		for(const auto& field : tool.headers){
			const Json* value = &arguments;
			for(const auto& part : field.path) value = &Field(*value, part);
			if(value->is_null()) continue;
			std::string text;
			if(field.type == "string" && value->is_string()) text = value->get<std::string>();
			else if(field.type == "boolean" && value->is_boolean()) text = value->dump();
			else if(field.type == "integer" && value->is_number()){
				const double number = value->get<double>();
				constexpr double maxSafe = 9007199254740991.0;
				if(std::abs(number) <= maxSafe && std::floor(number) == number) text = std::to_string(static_cast<int64_t>(number));
				else{
					error = "MCP tool parameter must be a safe integer: " + field.path.back();
					return false;
				}
			} else{
				error = "Invalid type for MCP tool parameter: " + field.path.back();
				return false;
			}
			headers.push_back(Header(field.header, EncodeHeader(text)));
		}
		return true;
	}
	bool AliasCharacter(const char c){ return (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '-' || c == '_'; }
	std::string Alias(const std::string& id, const std::string& name){
		const auto server = Lower(id);
		std::string readable = server + "_" + Lower(name);
		// Keep simple existing aliases. An underscore in a server id makes the name boundary ambiguous.
		if(!server.empty() && server.find('_') == std::string::npos && name.front() != '_' && name == Lower(name)
			&& readable.size() <= 60 && std::all_of(readable.begin(), readable.end(), AliasCharacter)) return "mcp_" + readable;
		const auto key = std::to_string(server.size()) + ":" + server + name;
		uint64_t hash = 14695981039346656037ULL;
		for(const unsigned char c : key) hash = (hash ^ c) * 1099511628211ULL;
		char suffix[17];
		std::snprintf(suffix, sizeof(suffix), "%016llx", static_cast<unsigned long long>(hash));
		for(auto& c : readable) if(!AliasCharacter(c)) c = '_';
		return "mcp__" + readable.substr(0, 42) + "_" + suffix;
	}
	bool IsExpired(const Server& server, const Reply& reply){
		return reply.status == 404 && !server.binding.sessionId.empty();
	}
	bool LoadTools(Server& server, const Deadline deadline, std::vector<Tool>& catalog, bool& expired, std::string& error){
		if(!server.hasTools) return true;
		Json params = Json::object();
		std::set<std::string> cursors;
		std::set<std::string> names;
		for(;;){
			const auto reply = Call(server, "tools/list", params, deadline);
			if(!reply){
				expired = IsExpired(server, reply);
				error = "MCP tools/list failed: " + reply.error;
				return false;
			}
			const auto& tools = Field(reply.data, "tools");
			if(!tools.is_array()){
				error = "MCP tools/list did not return a tools array.";
				return false;
			}
			for(const auto& item : tools){
				const auto name = Text(item, "name");
				if(name.empty() || !names.insert(name).second) continue;
				Tool tool{name, Alias(server.id, name), Text(item, "description"), Field(item, "inputSchema"), {}};
				if(!tool.schema.is_object()) tool.schema = {{"type", "object"}};
				if(server.transport == Transport::Http && IsModern(server.binding)){
					std::vector<std::string> path;
					std::set<std::string> headers;
					if(!ReadAnnotations(tool.schema, path, tool.headers, headers)){
						OutputDebugStringA(("Ignoring MCP tool with invalid x-mcp-header annotations: " + name + "\n").c_str());
						continue;
					}
				}
				catalog.push_back(std::move(tool));
			}
			const auto& cursor = Field(reply.data, "nextCursor");
			if(cursor.is_null()) return true;
			if(!cursor.is_string() || !cursors.insert(cursor.get<std::string>()).second){
				error = "Invalid or repeated cursor in MCP tools/list.";
				return false;
			}
			params["cursor"] = cursor;
		}
	}
	bool SameTool(const Tool& a, const Tool& b){ return a.alias == b.alias && a.description == b.description && a.schema == b.schema; }
	void Publish(Server& server, std::vector<Tool> tools){
		std::lock_guard<std::mutex> lock(serversMutex);
		if(server.cancelled) return;
		const auto oldSize = server.catalog.tools.size();
		if(tools.size() != oldSize || !std::equal(tools.begin(), tools.end(), server.catalog.tools.begin(), SameTool)) ++catalogRevision;
		server.catalog = {true, server.binding.version, server.name, "", std::move(tools)};
	}
	void SetFailure(Server& server, const std::string& error, const bool disconnected){
		std::lock_guard<std::mutex> lock(serversMutex);
		if(server.cancelled) return;
		server.catalog.error = error;
		if(disconnected){
			server.catalog.ready = false;
			if(!server.catalog.tools.empty()) ++catalogRevision;
			server.catalog.tools.clear();
		}
	}
	bool Refresh(Server& server, bool expired, std::string& error){
		server.refresh.exchange(false);
		std::vector<Tool> tools;
		if(!expired && LoadTools(server, Until(server.timeout), tools, expired, error)){
			Publish(server, std::move(tools));
			return !server.cancelled;
		}
		const bool lostSession = expired;
		if(lostSession && !server.cancelled){
			tools.clear();
			error.clear();
			const auto deadline = Until(server.timeout);
			if(InitializeLegacy(server, kLegacyVersion, deadline, error) && LoadTools(server, deadline, tools, expired, error)){
				Publish(server, std::move(tools));
				return !server.cancelled;
			}
		}
		SetFailure(server, error, lostSession || (server.transport == Transport::Stdio && !server.process));
		return false;
	}

	Json ListenRequest(const HttpBinding& binding, const uint64_t id){
		return Request(binding, "subscriptions/listen", {{"notifications", {{"toolsListChanged", true}}}}, id);
	}
	void WatchStdio(Server& server){
		while(!server.cancelled){
			{
				std::unique_lock<std::mutex> lock(server.requests, std::try_to_lock);
				if(lock && !server.cancelled){
					std::string error;
					if(!PumpStdio(server, error)){
						SetFailure(server, error, true);
						CloseStdio(server);
						return;
					}
					const auto deadline = Until(100);
					Json message;
					while(Remaining(deadline) && !server.cancelled && PopStdio(server, message)) ReceiveStdio(server, message, deadline);
				}
			}
			Sleep(75);
		}
	}
	void WatchHttp(Server& server){
		unsigned int disabledRevision = 0;
		bool haveDisabledRevision = false;
		bool legacyReconnect = false;
		while(!server.cancelled){
			HttpBinding binding;
			unsigned int revision = 0;
			uint64_t id = 0;
			bool enabled = false;
			{
				std::unique_lock<std::mutex> lock(server.requests, std::try_to_lock);
				if(lock && !server.cancelled && server.listChanged){
					revision = server.bindingRevision.load();
					enabled = !haveDisabledRevision || disabledRevision != revision;
					binding = server.binding;
					if(enabled && IsModern(binding)) id = server.nextId++;
				}
			}
			if(enabled){
				const bool modern = id != 0;
				bool acknowledged = false;
				bool completed = false;
				if(!modern && legacyReconnect) server.refresh = true;
				const auto message = modern ? ListenRequest(binding, id) : Json();
				const auto reply = ExchangeHttp(server, binding, modern ? "POST" : "GET", message, Until(server.timeout),
					[&](const Json& event, const std::string&){
						if(modern){
							completed = IsResponse(event, id);
							if(completed) return false;
							if(HandleSubscription(event, id, acknowledged, server)) server.refresh = true;
						} else{
							if(ToolsChanged(event)) server.refresh = true;
							const auto response = ServerReply(event);
							if(!response.is_null()) ExchangeHttp(server, binding, "POST", response, Until(std::min(server.timeout, 1000)));
						}
						return true;
					}, {}, true, false, revision);
				const bool expired = reply.status == 404 && !binding.sessionId.empty();
				if(expired && server.bindingRevision == revision){
					server.expiredBinding = revision;
					server.refresh = true;
				}
				if(completed || expired || reply.status == 400 || reply.status == 404 || reply.status == 405){
					haveDisabledRevision = true;
					disabledRevision = revision;
				}
				legacyReconnect = !modern && reply.status == 200;
				for(int i = 0; i < 10 && !server.cancelled && server.bindingRevision == revision; ++i) Sleep(100);
			} else Sleep(100);
		}
	}
	void Watch(Server& server){
		try{
			if(server.transport == Transport::Stdio) WatchStdio(server);
			else WatchHttp(server);
		} catch(const std::exception& ex){ SetFailure(server, ex.what(), false); }
	}
	void CloseServer(Server& server){
		server.cancelled = true;
		std::thread watcher;
		{
			std::lock_guard<std::mutex> lock(server.requests);
			watcher = std::move(server.watcher);
			if(!server.binding.sessionId.empty() && server.expiredBinding != server.bindingRevision){
				try{ ExchangeHttp(server, server.binding, "DELETE", {}, Until(std::min(server.timeout, 2000)), {}, {}, false, true); } catch(...){}
			}
			server.binding.sessionId.clear();
			CloseStdio(server);
		}
		if(watcher.joinable()) watcher.join();
	}
	std::shared_ptr<Server> FindServer(const char* id){
		std::lock_guard<std::mutex> lock(serversMutex);
		const auto it = servers.find(Lower(id ? id : ""));
		return it != servers.end() ? it->second : nullptr;
	}
	void RemoveServer(const std::shared_ptr<Server>& server){
		std::lock_guard<std::mutex> lock(serversMutex);
		const auto entry = servers.find(Lower(server->id));
		if(entry != servers.end() && entry->second == server){
			server->cancelled = true;
			if(!server->catalog.tools.empty()) ++catalogRevision;
			servers.erase(entry);
		}
	}
	std::string Connect(const std::shared_ptr<Server>& server, const std::string& command = "", const std::string& directory = ""){
		if(server->id.empty()) return Error("MCP server id is empty.");
		std::shared_ptr<Server> previous;
		{
			std::lock_guard<std::mutex> lock(serversMutex);
			if(!acceptingConnections) return Error("MCP is shutting down.");
			auto& entry = servers[Lower(server->id)];
			previous = std::move(entry);
			entry = server;
			if(previous){
				previous->cancelled = true;
				if(!previous->catalog.tools.empty()) ++catalogRevision;
			}
		}
		if(previous) CloseServer(*previous);
		std::string error;
		try{
			std::lock_guard<std::mutex> lock(server->requests);
			if(!server->cancelled && (server->transport == Transport::Http || StartStdio(*server, command, directory, error))
				&& Discover(*server, error) && Refresh(*server, false, error)){
				if(server->transport == Transport::Stdio && IsModern(server->binding) && server->listChanged){
					server->subscription = server->nextId++;
					if(!WriteStdio(*server, ListenRequest(server->binding, server->subscription), Until(server->timeout), error)){
						server->subscription = 0;
						SetFailure(*server, error, false);
					}
				}
				server->watcher = std::thread(Watch, std::ref(*server));
				return Ok();
			}
		} catch(const std::exception& ex){ error = ex.what(); }
		RemoveServer(server);
		CloseServer(*server);
		return Error(error.empty() ? WaitError(*server) : error);
	}
	std::shared_ptr<Server> FindTool(const std::string& alias, Tool* tool = nullptr){
		std::lock_guard<std::mutex> lock(serversMutex);
		std::shared_ptr<Server> match;
		for(const auto& pair : servers){
			const auto& server = pair.second;
			if(!server->catalog.ready || server->cancelled) continue;
			for(const auto& candidate : server->catalog.tools){
				if(candidate.alias != alias) continue;
				if(match) return nullptr;
				match = server;
				if(tool) *tool = candidate;
			}
		}
		return match;
	}
	std::string ToolResult(const Json& data){
		if(Text(data, "resultType") == "input_required") return Error("This MCP tool requires additional client input.");
		const auto& content = Field(data, "content");
		const auto& structured = Field(data, "structuredContent");
		std::string text;
		bool nonText = false;
		if(content.is_array()){
			for(const auto& item : content){
				if(Text(item, "type") != "text"){
					nonText = true;
					continue;
				}
				if(!text.empty()) text += "\n";
				text += Text(item, "text");
			}
		}
		const bool failed = Field(data, "isError") == true;
		const bool differentText = !text.empty() && !structured.is_null() && Json::parse(text, nullptr, false) != structured;
		if(!failed && !nonText && !differentText){
			if(!structured.is_null()) return Dump(structured);
			if(!text.empty()) return text;
		}
		Json result = Json::object();
		if(!structured.is_null()) result["structuredContent"] = structured;
		if(content.is_array()) result["content"] = content;
		if(failed){
			if(text.empty()) text = Text(structured, "message");
			result["isError"] = true;
			result["error"] = text.empty() ? "MCP tool returned an error." : text;
		}
		return Dump(result);
	}
	std::string ExecuteTool(Server& server, Tool tool, const Json& args){
		std::lock_guard<std::mutex> lock(server.requests);
		if(server.cancelled) return Error(WaitError(server));
		if(server.transport == Transport::Stdio && !server.process) return Error("MCP server is disconnected.");
		if(!server.binding.sessionId.empty() && server.expiredBinding == server.bindingRevision){
			std::string error;
			if(!Refresh(server, true, error)) return Error(error);
		}
		for(int attempt = 0; attempt < 2; ++attempt){
			std::vector<std::string> headers;
			std::string error;
			{
				std::lock_guard<std::mutex> registry(serversMutex);
				const auto& tools = server.catalog.tools;
				const auto match = std::find_if(tools.begin(), tools.end(), [&](const Tool& current){ return current.alias == tool.alias && current.name == tool.name; });
				if(match == tools.end()) return Error("MCP tool is no longer available. Refresh the tools and try again.");
				tool = *match;
			}
			if(!ToolHeaders(tool, args, headers, error)) return Error(error);
			const auto reply = Call(server, "tools/call", {{"name", tool.name}, {"arguments", args}}, Until(server.timeout), headers);
			if(reply) return ToolResult(reply.data);
			if(IsExpired(server, reply)){
				if(Refresh(server, true, error)) return Error("The MCP session expired and was reconnected. Run the tool again to retry.");
				return Error(error);
			}
			// A header mismatch is rejected before the tool runs. Re-read its schema before retrying.
			if(attempt == 0 && reply.status == 400 && reply.code == -32020){
				if(!Refresh(server, false, error)) return Error(error);
			} else return Error(reply.error);
		}
		return Error("MCP tool execution failed.");
	}
	std::string Description(const Server& server, const Tool& tool){ return "[MCP " + server.id + "] " + tool.description; }
	template<typename F> void ForAvailableTools(F visit){
		std::map<std::string, size_t> aliases;
		for(const auto& pair : servers) if(pair.second->catalog.ready && !pair.second->cancelled)
			for(const auto& tool : pair.second->catalog.tools) ++aliases[tool.alias];
		for(const auto& pair : servers) if(pair.second->catalog.ready && !pair.second->cancelled)
			for(const auto& tool : pair.second->catalog.tools) if(aliases[tool.alias] == 1) visit(*pair.second, tool);
	}
}

char* MCPConnectStdio(const char* serverId, const char* commandLine, const char* directory, const int timeout){
	try{
		return Copy(Connect(std::make_shared<Server>(serverId ? serverId : "", Transport::Stdio, timeout), commandLine ? commandLine : "", directory ? directory : ""));
	} catch(const std::exception& ex){ return Copy(Error(ex.what())); }
}
char* MCPConnectHttp(const char* serverId, const char* url, const char* customHeader, const int timeout){
	try{
		const auto endpoint = std::make_shared<Server>(serverId ? serverId : "", Transport::Http, timeout);
		endpoint->url = url ? url : "";
		Url parsed(curl_url(), curl_url_cleanup);
		if(!ParseHttpUrl(parsed.get(), endpoint->url)) return Copy(Error("Enter an HTTP or HTTPS MCP endpoint."));
		endpoint->headers = ParseHeaders(customHeader ? customHeader : "");
		return Copy(Connect(endpoint));
	} catch(const std::exception& ex){ return Copy(Error(ex.what())); }
}
void MCPStart(){
	std::lock_guard<std::mutex> lock(serversMutex);
	acceptingConnections = true;
}
void MCPAbortAll(){
	std::lock_guard<std::mutex> lock(serversMutex);
	acceptingConnections = false;
	for(const auto& pair : servers) pair.second->cancelled = true;
}
char* MCPDisconnect(const char* serverId){
	try{
		const auto server = FindServer(serverId);
		if(server){
			RemoveServer(server);
			CloseServer(*server);
		}
		return Copy(Ok());
	} catch(const std::exception& ex){ return Copy(Error(ex.what())); }
}
void MCPDisconnectAll(){
	MCPAbortAll();
	decltype(servers) closing;
	{
		std::lock_guard<std::mutex> lock(serversMutex);
		closing.swap(servers);
		if(!closing.empty()) ++catalogRevision;
	}
	for(const auto& pair : closing) CloseServer(*pair.second);
}
char* MCPRefreshTools(const char* serverId){
	try{
		const auto server = FindServer(serverId);
		if(!server) return Copy(Error("MCP server is not connected."));
		std::lock_guard<std::mutex> lock(server->requests);
		if(server->cancelled || (server->transport == Transport::Stdio && !server->process)) return Copy(Error("MCP server is not connected."));
		std::string error;
		const bool expired = !server->binding.sessionId.empty() && server->expiredBinding == server->bindingRevision;
		return Copy(Refresh(*server, expired, error) ? Ok() : Error(error));
	} catch(const std::exception& ex){ return Copy(Error(ex.what())); }
}
unsigned int MCPGetCatalogRevision(){ return catalogRevision; }
bool MCPIsConnected(const char* serverId){
	if(!serverId || !*serverId) return false;
	std::lock_guard<std::mutex> lock(serversMutex);
	const auto entry = servers.find(Lower(serverId));
	return entry != servers.end() && entry->second->catalog.ready && !entry->second->cancelled;
}
char* MCPBuildToolsJson(){
	try{
		Json tools = Json::array();
		{
			std::lock_guard<std::mutex> lock(serversMutex);
			ForAvailableTools([&](const Server& server, const Tool& tool){
				tools.push_back({{"type", "function"}, {"function", {{"name", tool.alias}, {"description", Description(server, tool)}, {"parameters", tool.schema}}}});
			});
		}
		return Copy(Dump(tools));
	} catch(const std::exception& ex){ return Copy(Error(ex.what())); }
}
void MCPRegisterToolsForSlot(const char* slotName){
	try{
		struct Schema{
			std::string name, description, parameters;
		};
		std::vector<Schema> schemas;
		{
			std::lock_guard<std::mutex> lock(serversMutex);
			ForAvailableTools([&](const Server& server, const Tool& tool){
				schemas.push_back({tool.alias, Description(server, tool), Dump(tool.schema)});
			});
		}
		ClearMcpToolSchemas(slotName);
		for(const auto& tool : schemas) AddTool(slotName, tool.name.c_str(), tool.description.c_str(), tool.parameters.c_str(), nullptr);
	} catch(...){}
}
bool MCPHasTool(const char* exposedName){
	try{
		return exposedName && _strnicmp(exposedName, "mcp_", 4) == 0 && FindTool(Lower(exposedName)) != nullptr;
	} catch(...){ return false; }
}
char* MCPExecuteTool(const char* exposedName, const char* argumentsJson){
	try{
		Tool tool;
		const auto server = exposedName ? FindTool(Lower(exposedName), &tool) : nullptr;
		if(!server) return Copy(Error("Unknown MCP tool."));
		const auto raw = Trim(argumentsJson ? argumentsJson : "");
		const auto args = raw.empty() ? Json::object() : Json::parse(raw, nullptr, false);
		if(!args.is_object()) return Copy(Error("MCP tool arguments must be a JSON object."));
		return Copy(ExecuteTool(*server, std::move(tool), args));
	} catch(const std::exception& ex){ return Copy(Error(ex.what())); }
}
char* MCPListServers(){
	try{
		Json list = Json::array();
		{
			std::lock_guard<std::mutex> lock(serversMutex);
			for(const auto& pair : servers){
				const auto& server = *pair.second;
				const auto& catalog = server.catalog;
				Json tools = Json::array();
				for(const auto& tool : catalog.tools) tools.push_back(tool.alias);
				list.push_back({{"id", server.id}, {"initialized", catalog.ready && !server.cancelled},
					{"protocol_version", catalog.version}, {"name", catalog.name}, {"error", catalog.error},
					{"transport", server.transport == Transport::Http ? "http" : "stdio"},
					{"tool_count", catalog.tools.size()}, {"tools", std::move(tools)}, {"refresh", server.refresh.load() && !server.cancelled}});
			}
		}
		return Copy(Dump(list));
	} catch(const std::exception& ex){ return Copy(Error(ex.what())); }
}