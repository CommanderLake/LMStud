using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using LMStud.Parsers;
using LMStud.Properties;
namespace LMStud{
	public partial class APIServer{
		private const int MaxRequestBytes = 32 * 1024 * 1024;
		internal readonly SessionManager Sessions = new SessionManager();
		private readonly SemaphoreSlim _requests = new SemaphoreSlim(32, 32);
		private CancellationTokenSource _stop;
		private HttpListener _listener;

		internal bool Start(string host = null, string token = null){
			if(_listener != null) return true;
			host = (host ?? Settings.Default.APIServerHost ?? "").Trim();
			token = (token ?? Settings.Default.APIServerToken ?? "").Trim();
			if(host.Length == 0) host = "localhost";
			var listener = new HttpListener();
			try{
				var ip = IPAddress.TryParse(host, out var address) ? address : null;
				var loopback = host.Equals("localhost", StringComparison.OrdinalIgnoreCase) || (ip != null && IPAddress.IsLoopback(ip));
				if(Uri.CheckHostName(host) == UriHostNameType.Unknown || IPAddress.Any.Equals(ip) || IPAddress.IPv6Any.Equals(ip))
					throw new InvalidOperationException("Enter a specific API server host name or IP address.");
				if(!loopback && token.Length == 0) throw new InvalidOperationException("A bearer token is required for LAN access.");
				if(host.Equals("localhost", StringComparison.OrdinalIgnoreCase)){
					listener.Prefixes.Add($"http://localhost:{Common.APIServerPort}/");
					listener.Prefixes.Add($"http://127.0.0.1:{Common.APIServerPort}/");
				} else{
					var prefixHost = ip?.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6 ? "[" + ip + "]" : host;
					listener.Prefixes.Add($"http://{prefixHost}:{Common.APIServerPort}/");
				}
				listener.Start();
				_stop = new CancellationTokenSource();
				var stopping = _stop.Token;
				_listener = listener;
				ThreadPool.QueueUserWorkItem(_ => Listen(listener, stopping, token, loopback));
				return true;
			} catch(Exception ex) when(ex is HttpListenerException || ex is ArgumentException || ex is InvalidOperationException){
				listener.Close();
				MessageBox.Show(string.Format(Resources.API_server_could_not_start_on_port__, Common.APIServerPort, ex.Message),
					Resources.LM_Stud, MessageBoxButtons.OK, MessageBoxIcon.Error);
				return false;
			}
		}

		internal void Stop(){
			var listener = _listener;
			if(listener == null) return;
			_listener = null;
			_stop.Cancel();
			listener.Close();
			_stop.Dispose();
			_stop = null;
		}
		private void Listen(HttpListener listener, CancellationToken stopping, string token, bool loopback){
			while(!stopping.IsCancellationRequested){
				HttpListenerContext context;
				try{ context = listener.GetContext(); }
				catch(HttpListenerException){ break; }
				catch(ObjectDisposedException){ break; }
				ThreadPool.QueueUserWorkItem(_ => Handle(context, stopping, token, loopback));
			}
		}
		private void Handle(HttpListenerContext context, CancellationToken stopping, string token, bool loopback){
			var entered = false;
			try{
				stopping.ThrowIfCancellationRequested();
				if(loopback && !context.Request.IsLocal){
					Error(context, 403, "local_access_only", "This API server is configured for loopback access.");
					return;
				}
				if(token.Length != 0 && !Authorized(context.Request.Headers["Authorization"], token)){
					context.Response.AddHeader("WWW-Authenticate", "Bearer");
					Error(context, 401, "invalid_api_key", "A valid API bearer token is required.");
					return;
				}
				if(!(entered = _requests.Wait(0))){
					Error(context, 503, "server_busy", "Too many requests are in progress.");
					return;
				}
				var method = context.Request.HttpMethod;
				var path = context.Request.Url.AbsolutePath.TrimEnd('/');
				if(method == "GET" && path == "/v1/models")
					WriteJson(context, Json.Object(Json.P("object", "list"), Json.P("data", ModelSlotManager.BuildServerModels())));
				else if(method == "POST" && (path == "/v1/responses" || path == "/v1/chat/completions"))
					HandleChat(context, path == "/v1/chat/completions", stopping);
				else if(method == "POST" && path == "/v1/reset") HandleReset(context);
				else if((method == "GET" || method == "DELETE") && path.StartsWith("/v1/responses/", StringComparison.Ordinal))
					HandleSavedResponse(context, path.Substring("/v1/responses/".Length), method == "DELETE");
				else Error(context, 404, "endpoint_not_found", "Unknown API endpoint.");
			} catch(ApiError ex){ TryError(context, ex.Status, ex.Code, ex.Message, ex.Parameter); }
			catch(HttpListenerException){}
			catch(ObjectDisposedException){}
			catch(OperationCanceledException){}
			catch(Exception ex){ TryError(context, 500, "server_error", ex.Message); }
			finally{
				if(entered) _requests.Release();
				try{ context.Response.Close(); } catch{}
			}
		}

		private static bool Authorized(string header, string token){
			const string prefix = "Bearer ";
			if(header == null || !header.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || header.Length != prefix.Length + token.Length) return false;
			var difference = 0;
			for(var i = 0; i < token.Length; ++i) difference |= header[prefix.Length + i] ^ token[i];
			return difference == 0;
		}
		private static JsonNode ReadBody(HttpListenerRequest request, bool allowEmpty = false){
			if(request.ContentLength64 > MaxRequestBytes) throw new ApiError(413, "request_too_large", "The JSON body exceeds 32 MiB.");
			using(var body = new MemoryStream()){
				var buffer = new byte[16384];
				int read;
				while((read = request.InputStream.Read(buffer, 0, buffer.Length)) > 0){
					if(body.Length + read > MaxRequestBytes) throw new ApiError(413, "request_too_large", "The JSON body exceeds 32 MiB.");
					body.Write(buffer, 0, read);
				}
				if(allowEmpty && body.Length == 0) return Json.Object();
				JsonNode json;
				try{ json = Json.Parse(Encoding.UTF8.GetString(body.GetBuffer(), 0, (int)body.Length).TrimStart('\uFEFF')); }
				catch(InvalidOperationException){ throw new ApiError(400, "invalid_json", "Expected a JSON object."); }
				if(!json.IsObject) throw new ApiError(400, "invalid_json", "Expected a JSON object.");
				return json;
			}
		}

		private static string SessionId(HttpListenerContext context, JsonNode body){
			var sessionId = body.GetString("session_id") ?? context.Request.Headers["X-Session-Id"];
			if(string.IsNullOrWhiteSpace(sessionId)) return null;
			if(sessionId.Length > 128 || sessionId.Any(ch => ch < '!' || ch > '~'))
				throw new ApiError(400, "invalid_value", "The session ID must be a short printable identifier.", "session_id");
			return sessionId;
		}
		private void HandleReset(HttpListenerContext context){
			var body = ReadBody(context.Request, true);
			var sessionId = SessionId(context, body);
			var previous = body.GetString("previous_response_id");
			if(sessionId == null && previous != null) sessionId = (Sessions.FindResponse(previous) ?? throw MissingResponse(previous)).Session.Id;
			var scope = body.GetString("scope")?.ToLowerInvariant() ?? (sessionId == null ? "global" : "session");
			if(scope == "session"){
				if(sessionId == null) throw new ApiError(400, "invalid_value", "A session ID is required for session reset.", "session_id");
				Sessions.Remove(sessionId);
			} else if(scope == "global"){
				Sessions.Clear();
				NativeMethods.CloseCommandPrompt();
			} else throw new ApiError(400, "invalid_value", "Expected session or global.", "scope");
			WriteJson(context, Json.Object(Json.P("status", "reset"), Json.P("scope", scope)));
		}
		private void HandleSavedResponse(HttpListenerContext context, string id, bool delete){
			if(delete){
				if(!Sessions.DeleteResponse(id)) throw MissingResponse(id);
				WriteJson(context, Json.Object(Json.P("id", id), Json.P("object", "response.deleted"), Json.P("deleted", true)));
			} else WriteJson(context, (Sessions.FindResponse(id) ?? throw MissingResponse(id)).Payload);
		}
		private static ApiError MissingResponse(string id){
			return new ApiError(404, "response_not_found", "Unknown or expired response: " + id, "previous_response_id");
		}

		private void HandleChat(HttpListenerContext context, bool chat, CancellationToken stopping){
			var request = new ChatRequest(ReadBody(context.Request), chat);
			var explicitSession = SessionId(context, request.Body);
			var parent = request.PreviousId == null ? null : Sessions.FindResponse(request.PreviousId) ?? throw MissingResponse(request.PreviousId);
			if(explicitSession != null && parent != null && explicitSession != parent.Session.Id)
				throw new ApiError(400, "invalid_value", "The response belongs to a different session.", "previous_response_id");
			var slot = ModelSlotManager.ResolveServerSlot(request.Model ?? parent?.Model);
			if(slot == null) throw new ApiError(404, "model_not_found", "No matching server model slot is configured.", "model");
			var remote = slot.Source == ModelSlotSource.Api;
			if(remote ? !ModelSlotManager.CanServeApiSlot(slot) : !ModelSlotManager.CanServeLocalSlot(slot))
				throw new ApiError(409, "slot_not_ready", "The requested model slot is not ready: " + slot.Name, "model");
			var model = ModelSlotManager.GetServerModelId(slot);
			var backend = remote ? model + "|" + slot.ApiBaseUrl + "|" + slot.ApiModel : model + "|" + slot.ResolveLocalPath();
			if(parent != null && parent.Backend != backend)
				throw new ApiError(400, "model_mismatch", "Use the model slot that created the previous response.", "model");

			var store = request.Store ?? (!chat || explicitSession != null);
			var session = parent?.Session ?? Sessions.Get(explicitSession, store);
			var reply = new ServerReply(context, request, model, session.Id, store);
			lock(session.SyncRoot){
				if(parent != null && !ReferenceEquals(parent, Sessions.FindResponse(request.PreviousId))) throw MissingResponse(request.PreviousId);
				if(session.Retired) throw new ApiError(409, "session_reset", "The API session has expired or been reset.");
				using(var lease = EnterSlot(slot.Name, stopping)){
					if(lease == null) throw new ApiError(409, "generation_busy", "The model slot is busy.");
					var prior = parent ?? (explicitSession == null ? null : session.Latest);
					if(prior?.Backend != backend) prior = null;
					// Legacy requests may send either one new message or a complete messages array.
					var append = prior != null && (parent != null || !request.UsesMessages || request.Body["messages"].Count == 1
						|| request.Body["messages"].All(message => message.GetString("role") == "tool"));
					var history = Json.ArrayBuilder();
					if(append) foreach(var item in prior.Items) history.Add(item);
					foreach(var item in request.Input) history.Add(item);
					var tools = request.Tools.Exists ? request.Tools.ToJson() : explicitSession != null && parent == null ? prior?.ToolsJson : null;
					if(!remote) ValidateLocalOptions(request, tools);
					var instructions = request.Instructions ?? (remote ? slot.GetInstructionsOrDefault() : Program.MainForm?.GetDefaultSystemPromptForSlot(slot.Name) ?? Common.SystemPrompt);

					IntPtr newChatState = IntPtr.Zero;
					try{
						APIClient.ChatCompletionResult result;
						byte[] newState = null;
						Action<string> stream = request.Stream ? (Action<string>)reply.TextDelta : null;
						if(remote){
							var client = new APIClient(slot.ApiBaseUrl, slot.ApiKey, slot.ApiModel, store && slot.ApiStore, instructions,
								slot.ApiReasoningEffort, slot.ApiReasoningSummary, slot.ApiMode, explicitStore: request.Store.HasValue);
							result = client.CreateChatCompletion(history, request.Temperature ?? Common.Temp, request.MaxTokens ?? Common.NGen, tools,
								request.ToolChoice, stopping, stream, request.ParallelToolCalls, request.TopP, request.TextFormat, request.IncludeUsage,
								request.Stream && !request.Chat ? (Func<JsonNode, bool>)reply.ForwardResponseEvent : null);
						} else{
							var instructionParts = new List<string>{ instructions };
							var messages = Json.ArrayBuilder();
							foreach(var message in APIClient.ConvertHistoryToChatCompletionMessages(history)){
								var role = message.GetString("role");
								if(role == "system" || role == "developer") instructionParts.Add(APIResponseParserCommon.ExtractContentText(message["content"]));
								else messages.Add(message);
							}
							instructions = string.Join("\n\n", instructionParts.Where(part => !string.IsNullOrEmpty(part)));
							if(messages.Count == 0) throw new ApiError(400, "invalid_input", "The local model needs a message or a tool result.", "input");
							var replay = Json.ArrayBuilder();
							foreach(var message in messages.Take(messages.Count - 1)) replay.Add(message);
							var reuse = append && ReferenceEquals(prior, session.Latest) && request.Input.Count == 1 && prior.Instructions == instructions;
							var prompt = messages[messages.Count - 1];
							if(!Generation.GenerateForApiServer(slot.Name, prompt, replay.Count == 0 ? null : replay.ToJson(), request.ToolChoice?.AsString() == "none" ? null : tools,
								out result, out newState, out newChatState, out _, request.MaxTokens, instructions, stream,
								state: reuse ? session.State : null, chatState: reuse ? session.NativeChatState : IntPtr.Zero, captureState: store,
								temperature: request.Temperature, topP: request.TopP, cancellationToken: stopping))
								throw new ApiError(500, "generation_failed", NativeMethods.GetLastError() ?? "The local model could not generate a response.");
						}
						stopping.ThrowIfCancellationRequested();
						var payload = reply.BuildResult(result);
						if(store){
							APIClient.AppendOutputItems(history, result);
							var turn = new SessionManager.Turn(session, reply.Id, model, backend, instructions, tools, history.ToArray(), payload);
							if(!Sessions.Save(session, turn, newState, newChatState, !chat))
								throw new ApiError(409, "session_reset", "The session was reset before the reply could be saved.");
							newChatState = IntPtr.Zero;
						}
						reply.Finish(result, payload);
					} catch(ApiError ex) when(reply.Started){ reply.Fail(ex.Code, ex.Message); }
					catch(APIClient.RequestException ex){
						reply.Fail(ex.StatusCode < 400 ? 502 : ex.StatusCode, ex.Code ?? "upstream_error", ex.UpstreamMessage ?? ex.Message, ex.Parameter);
					}
					catch(WebException ex) when(remote && ex.Status == WebExceptionStatus.Timeout){ reply.Fail(504, "upstream_timeout", ex.Message); }
					catch(Exception ex) when(remote && !(ex is OperationCanceledException) && !(ex is HttpListenerException) && !(ex is ApiError)){
						reply.Fail(502, "upstream_error", ex.Message);
					} catch(Exception ex) when(reply.Started && !(ex is OperationCanceledException) && !(ex is HttpListenerException)){
						reply.Fail("generation_failed", ex.Message);
					} finally{
						if(newChatState != IntPtr.Zero) NativeMethods.FreeChatState(newChatState);
					}
				}
			}
		}
		private static ModelSlotLockLease EnterSlot(string name, CancellationToken stopping){
			var timer = System.Diagnostics.Stopwatch.StartNew();
			do{
				stopping.ThrowIfCancellationRequested();
				var lease = ModelSlotManager.TryEnterSlot(name, 100);
				if(lease != null) return lease;
			} while(timer.ElapsedMilliseconds < 300000);
			return null;
		}
		private static void ValidateLocalOptions(ChatRequest request, string toolsJson){
			if(!request.TextFormat.IsNull && request.TextFormat.GetString("type") != "text")
				throw new ApiError(400, "unsupported_parameter", "Structured output is available through supported upstream models.", request.Chat ? "response_format" : "text");
			if(request.ToolChoice.HasValue && !request.ToolChoice.Value.IsNull && request.ToolChoice.Value.AsString() != "auto" && request.ToolChoice.Value.AsString() != "none")
				throw new ApiError(400, "unsupported_parameter", "The local model supports auto and none tool choice.", "tool_choice");
			if(string.IsNullOrEmpty(toolsJson) || request.ToolChoice?.AsString() == "none") return;
			var tools = Json.Parse(toolsJson);
			if(tools.Count == 0) return;
			if(tools.Any(tool => tool.GetString("type") != "function"))
				throw new ApiError(400, "unsupported_parameter", "The local model supports function tools.", "tools");
			if(tools.Any(tool => (tool["function"].GetBool("strict") ?? tool.GetBool("strict")) == true))
				throw new ApiError(400, "unsupported_parameter", "The local model cannot enforce strict tool schemas.", "tools");
			if(request.ParallelToolCalls == false)
				throw new ApiError(400, "unsupported_parameter", "The local model cannot enforce a single tool call.", "parallel_tool_calls");
		}
		private static void WriteJson(HttpListenerContext context, JsonNode payload){
			var bytes = Encoding.UTF8.GetBytes(payload.ToJson());
			context.Response.ContentType = "application/json; charset=utf-8";
			context.Response.ContentLength64 = bytes.Length;
			context.Response.OutputStream.Write(bytes, 0, bytes.Length);
		}
		private static JsonNode ErrorObject(int status, string code, string message, string param = null){
			var type = status == 401 ? "authentication_error" : status == 429 ? "rate_limit_error" : status >= 500 ? "server_error" : "invalid_request_error";
			return Json.Object(Json.P("message", message), Json.P("type", type), Json.P("code", code), Json.P("param", param));
		}
		private static void Error(HttpListenerContext context, int statusCode, string code, string message, string param = null){
			context.Response.StatusCode = statusCode;
			WriteJson(context, Json.Object(Json.P("error", ErrorObject(statusCode, code, message, param))));
		}
		private static void TryError(HttpListenerContext context, int status, string code, string message, string parameter = null){
			try{ Error(context, status, code, message, parameter); } catch(HttpListenerException){} catch(InvalidOperationException){}
		}
		private sealed class ApiError : InvalidOperationException{
			internal readonly int Status;
			internal readonly string Code;
			internal readonly string Parameter;
			internal ApiError(int status, string code, string message, string parameter = null) : base(message){
				Status = status;
				Code = code;
				Parameter = parameter;
			}
		}

		private sealed class ChatRequest{
			internal readonly JsonNode Body;
			internal readonly bool Chat;
			internal readonly bool UsesMessages;
			internal readonly JsonArrayBuilder Input;
			internal readonly string Model;
			internal readonly string PreviousId;
			internal readonly string Instructions;
			internal readonly bool? Store;
			internal readonly bool Stream;
			internal readonly bool IncludeUsage;
			internal readonly int? MaxTokens;
			internal readonly float? Temperature;
			internal readonly float? TopP;
			internal readonly bool? ParallelToolCalls;
			internal readonly JsonNode Tools;
			internal readonly JsonNode? ToolChoice;
			internal readonly JsonNode TextFormat;
			internal ChatRequest(JsonNode body, bool chat){
				Body = body;
				Chat = chat;
				Model = ReadString(body, "model");
				PreviousId = chat ? null : ReadString(body, "previous_response_id");
				Instructions = ReadString(body, "instructions");
				Store = ReadBool(body, "store");
				Stream = ReadBool(body, "stream") ?? false;
				IncludeUsage = chat && ReadBool(body["stream_options"], "include_usage") == true;
				Temperature = ReadFloat(body, "temperature", 0, 2);
				TopP = ReadFloat(body, "top_p", 0, 1);
				MaxTokens = chat ? ReadInt(body, "max_completion_tokens") ?? ReadInt(body, "max_tokens") : ReadInt(body, "max_output_tokens");
				ParallelToolCalls = ReadBool(body, "parallel_tool_calls");
				Tools = body["tools"];
				if(!Tools.IsNull && !Tools.IsArray) throw Invalid("tools", "Expected an array of tools.");
				ToolChoice = body["tool_choice"].IsNull ? (JsonNode?)null : body["tool_choice"];
				TextFormat = chat ? body["response_format"] : body["text"]["format"];
				if(chat && (ReadInt(body, "n") ?? 1) != 1) throw Invalid("n", "Only one choice is supported.");
				UsesMessages = body["input"].IsNull && body["messages"].Exists;
				try{ Input = UsesMessages ? APIClient.ConvertChatMessagesToInput(body["messages"]) : ReadInput(body["input"]); }
				catch(InvalidOperationException ex) when(!(ex is ApiError)){ throw Invalid(chat ? "messages" : "input", ex.Message); }
				if(Input.Count == 0) throw Invalid(chat ? "messages" : "input", "Expected at least one input item.");
			}
			private static JsonArrayBuilder ReadInput(JsonNode input){
				var items = Json.ArrayBuilder();
				void Add(JsonNode item){
					if(item.IsString) items.Add(Json.Object(Json.P("role", "user"), Json.P("content", item)));
					else if(item.IsObject) items.Add(item);
					else throw Invalid("input", "Input items must be objects or strings.");
				}
				if(input.IsArray) foreach(var item in input) Add(item);
				else if(!input.IsNull) Add(input);
				return items;
			}
			private static string ReadString(JsonNode body, string field){
				if(body[field].IsNull) return null;
				if(!body[field].IsString) throw Invalid(field, "Expected a string.");
				return body.GetString(field);
			}
			private static bool? ReadBool(JsonNode body, string field){
				if(body[field].IsNull) return null;
				if(body[field].Kind != JsonKind.Boolean) throw Invalid(field, "Expected a boolean.");
				return body.GetBool(field);
			}
			private static int? ReadInt(JsonNode body, string field){
				if(body[field].IsNull) return null;
				var value = body.GetInt(field);
				if(body[field].Kind != JsonKind.Number || !value.HasValue || value.Value < 1) throw Invalid(field, "Expected a positive integer.");
				return value;
			}
			private static float? ReadFloat(JsonNode body, string field, float min, float max){
				if(body[field].IsNull) return null;
				var value = body.GetDouble(field);
				if(body[field].Kind != JsonKind.Number || !value.HasValue || double.IsNaN(value.Value) || value.Value < min || value.Value > max)
					throw Invalid(field, $"Expected a number between {min} and {max}.");
				return (float)value.Value;
			}
			private static ApiError Invalid(string field, string message){return new ApiError(400, "invalid_value", message, field);}
		}
	}
}
