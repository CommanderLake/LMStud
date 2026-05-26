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
	public class APIServer{
		internal readonly SessionManager Sessions = new SessionManager();
		private CancellationTokenSource _cts;
		private HttpListener _listener;
		private bool IsRunning => _listener != null && _listener.IsListening;
		internal void Start(){
			if(IsRunning) return;
			try{
				_cts = new CancellationTokenSource();
				_listener = new HttpListener();
				_listener.Prefixes.Add($"http://*:{Common.APIServerPort}/");
				_listener.Start();
				ThreadPool.QueueUserWorkItem(o => {ListenLoop(_cts.Token);});
			} catch(HttpListenerException ex){
				try{ _listener?.Close(); } catch{}
				_listener = null;
				_cts?.Dispose();
				_cts = null;
				MessageBox.Show(string.Format(Resources.API_server_could_not_start_on_port__, Common.APIServerPort, ex.Message), Resources.LM_Stud, MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
		}
		internal void Stop(){
			if(!IsRunning) return;
			_cts.Cancel();
			try{ _listener.Stop(); } catch{}
			_listener.Close();
			_listener = null;
		}
		private void ListenLoop(CancellationToken token){
			while(!token.IsCancellationRequested){
				HttpListenerContext ctx;
				try{ ctx = _listener.GetContext(); } catch{ break; }
				ThreadPool.QueueUserWorkItem(_ => HandleContext(ctx));
			}
		}
		private void HandleContext(HttpListenerContext context){
			try{
				var req = context.Request;
				var method = req.HttpMethod;
				var path = req.Url.AbsolutePath;
				if(method == "GET" && path == "/v1/models") HandleModels(context);
				else if(method == "POST" && path == "/v1/responses") HandleChat(context, false);
				else if(method == "POST" && path == "/v1/chat/completions") HandleChat(context, true);
				else if(method == "POST" && path == "/v1/reset") HandleReset(context);
				else context.Response.StatusCode = 404;
			} catch{ context.Response.StatusCode = 500; } finally{
				try{ context.Response.OutputStream.Close(); } catch{}
			}
		}
		private void HandleModels(HttpListenerContext ctx){
			var resp = Json.Object(Json.P("object", "list"), Json.P("data", ModelSlotManager.BuildServerModels()));
			WriteJson(ctx, resp);
		}
		private void HandleReset(HttpListenerContext ctx){
			string body;
			using(var reader = new StreamReader(ctx.Request.InputStream, ctx.Request.ContentEncoding)){ body = reader.ReadToEnd(); }
			ChatRequest request = null;
			if(!string.IsNullOrEmpty(body))
				try{ request = ParseChatRequest(body); } catch{
					ctx.Response.StatusCode = 400;
					return;
				}
			var resetScope = request?.ResetScope;
			var sessionId = ResolveSessionId(ctx, request);
			var hasSession = !string.IsNullOrWhiteSpace(sessionId);
			if(string.IsNullOrWhiteSpace(resetScope)) resetScope = hasSession ? "session" : "global";
			if(string.Equals(resetScope, "session", StringComparison.OrdinalIgnoreCase)){
				if(hasSession) Sessions.Remove(sessionId);
			}else{
				Sessions.Clear();
				NativeMethods.CloseCommandPrompt();
			}
			WriteJson(ctx, Json.Object(Json.P("status", "reset"), Json.P("scope", resetScope.ToLowerInvariant())));
		}
		private void HandleChat(HttpListenerContext ctx, bool outputChatCompletions){
			string body;
			using(var reader = new StreamReader(ctx.Request.InputStream, ctx.Request.ContentEncoding)){ body = reader.ReadToEnd(); }
			ChatRequest request;
			try{ request = ParseChatRequest(body); } catch(Exception ex){
				ctx.Response.StatusCode = 400;
				WriteJson(ctx, Json.Object(Json.P("error", Json.Object(Json.P("message", ex.Message)))));
				return;
			}
			if(request == null){
				ctx.Response.StatusCode = 400;
				return;
			}
			var messages = request.Messages ?? ParseInputMessages(request.Input);
			var inputItems = BuildInputItems(request);
			if((messages == null || messages.Count == 0) && (inputItems == null || inputItems.Count == 0)){
				ctx.Response.StatusCode = 400;
				return;
			}
			var store = request.Store ?? true;
			var slot = ModelSlotManager.ResolveServerSlot(request.Model);
			if(slot == null){
				WriteError(ctx, 404, "model_not_found", string.IsNullOrWhiteSpace(request.Model) ? "No default model slot is configured." : "Model slot not found: " + request.Model);
				return;
			}
			if(slot.Source == ModelSlotSource.Api){
				if(!ModelSlotManager.CanServeApiSlot(slot)){
					WriteError(ctx, 409, "slot_incomplete", "API slot is missing an API URL or model: " + slot.Name);
					return;
				}
				HandleRemoteChat(ctx, request, messages, inputItems, store, outputChatCompletions, slot);
				return;
			}
			if(!ModelSlotManager.CanServeLocalSlot(slot)){
				WriteError(ctx, 409, "slot_not_loaded", "Local slot is not loaded: " + slot.Name);
				return;
			}
			var session = Sessions.Get(ResolveSessionId(ctx, request));
			var newChatState = IntPtr.Zero;
			ModelSlotLockLease slotLock = null;
			try{
				ctx.Response.AddHeader("X-Session-Id", session.Id);
				var historyMessages = ExtractHistoryAndLatestInput(messages, inputItems, out var latestInput);
				if(latestInput?.Content == null){
					WriteError(ctx, 400, "invalid_request", "Request did not contain a user input message.");
					return;
				}
				var historyJson = historyMessages != null && historyMessages.Count > 0 ? BuildChatHistoryJson(historyMessages) : null;
				slotLock = ModelSlotManager.TryEnterSlot(slot.Name, 300000);
				if(slotLock == null){
					WriteError(ctx, 409, "generation_busy", "The local backend is busy or could not generate a response.");
					return;
				}
				APIClient.ChatCompletionResult result = null;
				byte[] newState = null;
				var tokens = 0;
				string toolsJson = null;
				JsonNode resp = JsonNode.Missing;
				var backendKey = "local:" + slot.Name;
				lock(session.SyncRoot){
					if(!Sessions.IsActive(session)){
						WriteError(ctx, 409, "session_reset", "The API session was reset before the request could be prepared.");
						return;
					}
					var modeSwitch = !string.IsNullOrEmpty(session.LastBackend) && session.LastBackend != backendKey;
					toolsJson = ResolveClientToolsJsonForBackend(session, request, modeSwitch);
					if(!Generation.GenerateForApiServer(slot.Name, modeSwitch ? null : session.State, modeSwitch ? IntPtr.Zero : session.NativeChatState, historyJson, ToNativeRole(latestInput.Role), latestInput.Content, toolsJson, out result, out newState, out newChatState, out tokens)){
						WriteError(ctx, 409, "generation_busy", "The local backend is busy or could not generate a response.");
						return;
					}
					var model = ModelSlotManager.GetServerModelId(slot);
					resp = outputChatCompletions ? BuildChatCompletionsPayload(result, model, session.Id) : BuildResponsePayload(result, model, session.Id);
					var sessionActive = Sessions.Apply(session, s => {
						ResetSessionForModeSwitchCore(s, backendKey);
						s.LastBackend = backendKey;
						if(request.Tools.Exists) s.ToolsJson = toolsJson;
						if(historyMessages != null && historyMessages.Count > 0) s.Messages = CloneMessages(historyMessages);
						s.Messages.Add(CloneMessage(latestInput));
						s.Messages.Add(new Message{ Role = "assistant", Content = result.Content, ToolCalls = result.ToolCalls });
						s.State = newState;
						s.SetNativeChatState(newChatState);
						newChatState = IntPtr.Zero;
						s.TokenCount = tokens;
					}, () => resp.GetString("id"));
					if(!sessionActive){
						WriteError(ctx, 409, "session_reset", "The API session was reset before the request could be stored.");
						return;
					}
				}
				WriteJson(ctx, resp);
			} finally{
				if(newChatState != IntPtr.Zero) NativeMethods.FreeChatState(newChatState);
				if(!store) Sessions.Remove(session.Id);
				slotLock?.Dispose();
			}
		}
		private void HandleRemoteChat(HttpListenerContext ctx, ChatRequest request, List<Message> incomingMessages, JsonArrayBuilder inputItems, bool store, bool outputChatCompletions, ModelSlot slot){
			var session = Sessions.Get(ResolveSessionId(ctx, request));
			ctx.Response.AddHeader("X-Session-Id", session.Id);
			APIClient client = null;
			ModelSlotLockLease slotLock = null;
			try{
				slotLock = ModelSlotManager.TryEnterSlot(slot.Name, 300000);
				if(slotLock == null){
					WriteError(ctx, 409, "generation_busy", "The API slot is busy or could not generate a response.");
					return;
				}
				lock(session.SyncRoot){
					if(!Sessions.IsActive(session)){
						WriteError(ctx, 409, "session_reset", "The API session was reset before the request could be prepared.");
						return;
					}
					var incomingDelta = BuildIncomingDelta(incomingMessages, inputItems);
					if(incomingDelta == null || incomingDelta.Count == 0){
						WriteError(ctx, 400, "invalid_request", "Request did not contain a new input message.");
						return;
					}
					var backendKey = "remote:" + slot.Name;
					var modeSwitch = !string.IsNullOrEmpty(session.LastBackend) && session.LastBackend != backendKey;
					var persisted = modeSwitch ? Json.ArrayBuilder() : BuildPersistedHistory(session.Messages);
					foreach(var item in incomingDelta) persisted.Add(item);
					var toolsJson = ResolveClientToolsJsonForBackend(session, request, modeSwitch);
					var instructions = request.Instructions ?? slot.GetInstructionsOrDefault();
					client = new APIClient(slot.ApiBaseUrl, slot.ApiKey, slot.ApiModel, slot.ApiStore, instructions, slot.ApiReasoningEffort, slot.ApiReasoningSummary);
					var result = client.CreateChatCompletion(persisted, Common.Temp, Common.NGen, toolsJson, request.ToolChoice, CancellationToken.None);
					APIClient.AppendOutputItems(persisted, result);
					var model = ModelSlotManager.GetServerModelId(slot);
					var resp = outputChatCompletions ? BuildChatCompletionsPayload(result, model, session.Id) : BuildResponsePayload(result, model, session.Id);
					if(!Sessions.Apply(session, s => {
						ResetSessionForModeSwitchCore(s, backendKey);
						if(request.Tools.Exists) s.ToolsJson = toolsJson;
						foreach(var deltaMessage in incomingDelta){
							var parsed = ParseInputItemToMessage(deltaMessage);
							if(parsed != null) s.Messages.Add(parsed);
						}
						s.Messages.Add(new Message{ Role = "assistant", Content = result.Content, ToolCalls = result.ToolCalls });
						s.State = null;
						s.TokenCount = result.TotalTokens ?? 0;
						s.LastBackend = backendKey;
					}, () => resp.GetString("id"))){
						WriteError(ctx, 409, "session_reset", "The API session was reset before the response could be stored.");
						return;
					}
					WriteJson(ctx, resp);
				}
			} catch(Exception ex){
				WriteError(ctx, 502, "upstream_error", ex.Message);
			} finally{
				if(!store) Sessions.Remove(session.Id);
				slotLock?.Dispose();
				client?.Dispose();
			}
		}
		private static void WriteError(HttpListenerContext ctx, int statusCode, string code, string message){
			ctx.Response.StatusCode = statusCode;
			WriteJson(ctx, Json.Object(Json.P("error", Json.Object(Json.P("code", code), Json.P("message", message)))));
		}
		private static void WriteJson(HttpListenerContext ctx, JsonNode payload){
			var json = payload.ToJson(JsonFormat.Indented);
			var bytes = Encoding.UTF8.GetBytes(json);
			ctx.Response.ContentType = "application/json";
			ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
		}
		private static void ResetSessionForModeSwitchCore(SessionManager.Session session, string backendKey){
			if(session == null || string.IsNullOrEmpty(session.LastBackend) || session.LastBackend == backendKey) return;
			session.Messages.Clear();
			session.State = null;
			session.TokenCount = 0;
			session.ToolsJson = null;
			session.ClearNativeChatState();
			session.LastBackend = backendKey;
		}
		private string ResolveSessionId(HttpListenerContext ctx, ChatRequest request){
			if(!string.IsNullOrWhiteSpace(request?.SessionId)) return request.SessionId;
			var headerSessionId = ctx?.Request?.Headers["X-Session-Id"];
			if(!string.IsNullOrWhiteSpace(headerSessionId)) return headerSessionId;
			return Sessions.GetSessionIdForResponse(request?.PreviousResponseId);
		}
		private static JsonNode BuildResponsePayload(APIClient.ChatCompletionResult result, string model, string sessionId){
			var createdAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
			var output = result != null && result.OutputItems.IsArray && result.OutputItems.Count > 0 ? result.OutputItems : BuildResponseOutputItems(result?.Content, result?.ToolCalls);
			return Json.Object(Json.P("id", "resp_" + Guid.NewGuid().ToString("N")), Json.P("object", "response"), Json.P("created_at", createdAt), Json.P("status", "completed"),
				Json.P("model", model), Json.P("session_id", sessionId), Json.P("output", output));
		}
		private static JsonNode BuildChatCompletionsPayload(APIClient.ChatCompletionResult result, string model, string sessionId){
			var createdAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
			var message = Json.ObjectBuilder(Json.P("role", "assistant"), Json.P("content", result?.Content ?? ""));
			var toolCalls = BuildChatCompletionToolCalls(result?.ToolCalls);
			var finishReason = "stop";
			if(toolCalls.IsArray && toolCalls.Count > 0){
				message["tool_calls"] = toolCalls;
				finishReason = "tool_calls";
			}
			return Json.Object(Json.P("id", "chatcmpl_" + Guid.NewGuid().ToString("N")), Json.P("object", "chat.completion"), Json.P("created", createdAt), Json.P("model", model),
				Json.P("session_id", sessionId),
				Json.P("choices", Json.Array(Json.Object(Json.P("index", 0), Json.P("message", message), Json.P("finish_reason", finishReason)))));
		}
		private static JsonNode BuildResponseOutputItems(string content, List<APIClient.ToolCall> toolCalls){
			var output = Json.ArrayBuilder();
			if(!string.IsNullOrWhiteSpace(content) || toolCalls == null || toolCalls.Count == 0){
				output.Add(Json.Object(Json.P("type", "message"), Json.P("status", "completed"), Json.P("role", "assistant"),
					Json.P("content", Json.Array(Json.Object(Json.P("type", "output_text"), Json.P("text", content ?? ""))))));
			}
			if(toolCalls != null)
				foreach(var toolCall in toolCalls.Where(call => call != null && !string.IsNullOrWhiteSpace(call.Id) && !string.IsNullOrWhiteSpace(call.Name))){
					output.Add(Json.Object(Json.P("type", "function_call"), Json.P("status", "completed"), Json.P("call_id", toolCall.Id), Json.P("name", toolCall.Name), Json.P("arguments", toolCall.Arguments ?? "")));
				}
			return output.ToNode();
		}
		private static JsonNode BuildChatCompletionToolCalls(List<APIClient.ToolCall> toolCalls){
			if(toolCalls == null || toolCalls.Count == 0) return JsonNode.Missing;
			var array = Json.ArrayBuilder();
			foreach(var toolCall in toolCalls.Where(call => call != null && !string.IsNullOrWhiteSpace(call.Id) && !string.IsNullOrWhiteSpace(call.Name))){
				array.Add(Json.Object(Json.P("id", toolCall.Id), Json.P("type", "function"),
					Json.P("function", Json.Object(Json.P("name", toolCall.Name), Json.P("arguments", toolCall.Arguments ?? "")))));
			}
			return array.Count > 0 ? array.ToNode() : JsonNode.Missing;
		}
		private static JsonArrayBuilder BuildInputItems(ChatRequest request){
			if(request == null) return null;
			var normalized = NormalizeInputItems(request.Input);
			if(normalized != null && normalized.Count > 0) return normalized;
			if(request.Messages == null || request.Messages.Count == 0) return null;
			var items = Json.ArrayBuilder();
			foreach(var message in request.Messages){
				if(message == null) continue;
				if(string.IsNullOrWhiteSpace(message.Role)) continue;
				var apiMessage = new APIClient.ChatMessage(message.Role, message.Content ?? ""){ ToolCallId = message.ToolCallId, ToolCalls = GetToolCalls(message) };
				var hasToolCalls = apiMessage.ToolCalls != null && apiMessage.ToolCalls.Count > 0;
				var hasContent = !string.IsNullOrWhiteSpace(apiMessage.Content);
				if(string.Equals(apiMessage.Role, "tool", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(apiMessage.ToolCallId))
					items.Add(Json.Object(Json.P("role", apiMessage.Role), Json.P("content", apiMessage.Content ?? "")));
				else if(hasContent || !hasToolCalls) items.Add(APIClient.BuildInputMessagePayload(apiMessage));
				if(apiMessage.ToolCalls == null) continue;
				foreach(var toolCall in apiMessage.ToolCalls.Where(call => call != null && !string.IsNullOrWhiteSpace(call.Id) && !string.IsNullOrWhiteSpace(call.Name)))
					items.Add(Json.Object(Json.P("type", "function_call"), Json.P("call_id", toolCall.Id), Json.P("name", toolCall.Name), Json.P("arguments", toolCall.Arguments ?? "")));
			}
			return items.Count > 0 ? items : null;
		}
		private static string ResolveClientToolsJsonForBackend(SessionManager.Session session, ChatRequest request, bool modeSwitch){
			if(request != null && request.Tools.Exists) return request.Tools.IsArray && request.Tools.Count > 0 ? request.Tools.ToJson() : null;
			return modeSwitch ? null : session?.ToolsJson;
		}
		private static Message CloneMessage(Message message){
			if(message == null) return null;
			var toolCalls = GetToolCalls(message);
			return new Message{
				Role = message.Role, Content = message.Content, ToolCallId = message.ToolCallId,
				ToolCalls = toolCalls == null ? null : new List<APIClient.ToolCall>(toolCalls)
			};
		}
		private static List<Message> CloneMessages(IEnumerable<Message> messages){
			if(messages == null) return new List<Message>();
			return messages.Select(CloneMessage).Where(message => message != null).ToList();
		}
		private static List<APIClient.ToolCall> GetToolCalls(Message message){
			if(message == null) return null;
			return message.ToolCalls ?? APIResponseParserCommon.ParseToolCalls(message.ToolCallsJson);
		}
		private static MessageRole ToNativeRole(string role){
			if(string.Equals(role, "assistant", StringComparison.OrdinalIgnoreCase)) return MessageRole.Assistant;
			if(string.Equals(role, "tool", StringComparison.OrdinalIgnoreCase)) return MessageRole.Tool;
			return MessageRole.User;
		}
		private static JsonArrayBuilder BuildPersistedHistory(List<Message> messages){
			var history = Json.ArrayBuilder();
			if(messages == null) return history;
			foreach(var message in messages){
				if(string.IsNullOrWhiteSpace(message?.Role)) continue;
				var toolCalls = GetToolCalls(message);
				var hasToolCalls = toolCalls != null && toolCalls.Count > 0;
				if(string.IsNullOrWhiteSpace(message.Content) && !hasToolCalls) continue;
				if(string.Equals(message.Role, "tool", StringComparison.OrdinalIgnoreCase)){
					if(!string.IsNullOrWhiteSpace(message.ToolCallId)){
						history.Add(Json.Object(Json.P("type", "function_call_output"), Json.P("call_id", message.ToolCallId), Json.P("output", message.Content ?? "")));
						continue;
					}
				}
				if(!hasToolCalls || !string.IsNullOrWhiteSpace(message.Content))
					history.Add(Json.Object(Json.P("role", message.Role), Json.P("content", message.Content ?? "")));
				if(hasToolCalls)
					foreach(var toolCall in toolCalls.Where(call => call != null && !string.IsNullOrWhiteSpace(call.Id) && !string.IsNullOrWhiteSpace(call.Name)))
						history.Add(Json.Object(Json.P("type", "function_call"), Json.P("call_id", toolCall.Id), Json.P("name", toolCall.Name), Json.P("arguments", toolCall.Arguments ?? "")));
			}
			return history;
		}
		private static JsonArrayBuilder BuildIncomingDelta(List<Message> incomingMessages, JsonArrayBuilder inputItems){
			if(incomingMessages != null && incomingMessages.Count > 0){
				var last = incomingMessages.LastOrDefault(m => !string.IsNullOrWhiteSpace(m?.Role) && (m.Content != null || (GetToolCalls(m) != null && GetToolCalls(m).Count > 0)));
				if(last == null) return null;
				var apiMessage = new APIClient.ChatMessage(last.Role, last.Content ?? ""){ ToolCallId = last.ToolCallId, ToolCalls = GetToolCalls(last) };
				if(string.Equals(apiMessage.Role, "tool", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(apiMessage.ToolCallId))
				{
					var single = Json.ArrayBuilder();
					single.Add(Json.Object(Json.P("role", apiMessage.Role), Json.P("content", apiMessage.Content ?? "")));
					return single;
				}
				return APIClient.BuildInputItems(new[]{ apiMessage });
			}
			if(inputItems == null || inputItems.Count == 0) return null;
			var delta = Json.ArrayBuilder();
			foreach(var item in inputItems) delta.Add(item);
			return delta;
		}
		private static List<Message> ExtractHistoryAndLatestInput(List<Message> messages, JsonArrayBuilder inputItems, out Message latest){
			latest = null;
			if(messages != null && messages.Count > 0){
				for(var i = messages.Count - 1; i >= 0; i--){
					var candidate = messages[i];
					if(string.IsNullOrWhiteSpace(candidate?.Role) || candidate.Content == null) continue;
					latest = CloneMessage(candidate);
					return CloneMessages(messages.Take(i).Where(HasMessagePayload));
				}
			}
			latest = ExtractLatestInputMessage(inputItems);
			return new List<Message>();
		}
		private static bool HasMessagePayload(Message message){
			if(string.IsNullOrWhiteSpace(message?.Role)) return false;
			var toolCalls = GetToolCalls(message);
			return message.Content != null || !string.IsNullOrWhiteSpace(message.ToolCallId) || (toolCalls != null && toolCalls.Count > 0);
		}
		private static string BuildChatHistoryJson(IEnumerable<Message> messages){
			var array = Json.ArrayBuilder();
			foreach(var message in messages ?? Enumerable.Empty<Message>()){
				if(!HasMessagePayload(message)) continue;
				var toolCalls = GetToolCalls(message);
				var obj = Json.ObjectBuilder(Json.P("role", message.Role));
				if(message.Content != null || toolCalls == null || toolCalls.Count == 0) obj["content"] = Json.String(message.Content ?? "");
				if(!string.IsNullOrWhiteSpace(message.ToolCallId)) obj["tool_call_id"] = Json.String(message.ToolCallId);
				var toolCallsJson = BuildChatCompletionToolCalls(toolCalls);
				if(toolCallsJson.Exists) obj["tool_calls"] = toolCallsJson;
				array.Add(obj.ToNode());
			}
			return array.Count > 0 ? array.ToJson() : null;
		}
		private static Message ExtractLatestInputMessage(JsonArrayBuilder inputItems){
			if(inputItems == null || inputItems.Count == 0) return null;
			for(var i = inputItems.Count - 1; i >= 0; i--){
				var parsed = ParseInputItemToMessage(inputItems[i]);
				if(parsed == null || parsed.Content == null) continue;
				return parsed;
			}
			return null;
		}
		private static Message ParseInputItemToMessage(JsonNode item){
			if(!item.IsObject) return null;
			var obj = item;
			var type = obj.GetString("type");
			if(string.Equals(type, "function_call_output", StringComparison.OrdinalIgnoreCase) || string.Equals(type, "tool_result", StringComparison.OrdinalIgnoreCase)){
				var output = obj.GetString("output") ?? obj.GetString("content") ?? obj.GetString("text") ?? "";
				var callId = obj.GetString("call_id") ?? obj.GetString("tool_call_id") ?? obj.GetString("id");
				return new Message{ Role = "tool", Content = output, ToolCallId = callId };
			}
			if(string.Equals(type, "function_call", StringComparison.OrdinalIgnoreCase) || string.Equals(type, "tool_call", StringComparison.OrdinalIgnoreCase)){
				var toolCall = APIResponseParserCommon.ParseToolCallItem(obj);
				if(toolCall == null) return null;
				return new Message{ Role = "assistant", Content = "", ToolCalls = new List<APIClient.ToolCall>{ toolCall } };
			}
			var role = obj.GetString("role") ?? "user";
			var content = ExtractContentText(obj["content"]) ?? obj.GetString("text") ?? obj.GetString("content");
			var toolCalls = APIResponseParserCommon.ParseToolCalls(obj["tool_calls"]);
			if(string.IsNullOrWhiteSpace(content) && (toolCalls == null || toolCalls.Count == 0)) return null;
			return new Message{ Role = role, Content = content ?? "", ToolCalls = toolCalls };
		}
		private static JsonArrayBuilder NormalizeInputItems(JsonNode input){
			if(input.IsNull) return null;
			if(input.IsString){
				var single = Json.ArrayBuilder();
				single.Add(Json.Object(Json.P("role", "user"), Json.P("content", input.AsString())));
				return single;
			}
			if(input.IsObject){
				var single = Json.ArrayBuilder();
				single.Add(input);
				return single;
			}
			if(input.IsArray){
				var items = Json.ArrayBuilder();
				foreach(var item in input.Where(item => !item.IsNull)){
					if(item.IsString){
						items.Add(Json.Object(Json.P("role", "user"), Json.P("content", item.AsString())));
						continue;
					}
					if(item.IsObject) items.Add(item);
				}
				return items.Count > 0 ? items : null;
			}
			return null;
		}
		private static List<Message> ParseInputMessages(JsonNode input){
			if(input.IsNull) return null;
			if(input.IsString) return new List<Message>{ new Message{ Role = "user", Content = input.AsString() } };
			JsonArrayBuilder inputItems = null;
			if(input.IsArray) inputItems = NormalizeInputItems(input);
			else if(input.IsObject){
				inputItems = Json.ArrayBuilder();
				inputItems.Add(input);
			}
			if(inputItems == null) return null;
			var messages = new List<Message>();
			foreach(var item in inputItems){
				if(item.IsNull) continue;
				if(item.IsString){
					messages.Add(new Message{ Role = "user", Content = item.AsString() });
					continue;
				}
				var parsed = ParseInputItemToMessage(item);
				if(parsed != null) messages.Add(parsed);
			}
			return messages.Count > 0 ? messages : null;
		}
		private static string ExtractContentText(JsonNode contentToken){
			if(contentToken.IsNull) return null;
			if(contentToken.IsString) return contentToken.AsString();
			if(contentToken.IsArray){
				var sb = new StringBuilder();
				foreach(var item in contentToken){
					if(item.IsNull) continue;
					if(item.IsString){
						sb.Append(item.AsString());
						continue;
					}
					if(!item.IsObject) continue;
					var obj = item;
					var text = obj.GetString("text") ?? obj.GetString("content");
					if(!string.IsNullOrWhiteSpace(text)) sb.Append(text);
				}
				return sb.Length > 0 ? sb.ToString() : null;
			}
			if(contentToken.IsObject) return contentToken.GetString("text") ?? contentToken.GetString("content");
			return null;
		}
		private static ChatRequest ParseChatRequest(string body){
			var root = Json.Parse(body);
			if(!root.IsObject) throw new InvalidOperationException("JSON request body must be an object.");
			return new ChatRequest{
				Input = root["input"],
				Instructions = root.GetString("instructions"),
				Messages = ParseRequestMessages(root["messages"]),
				Model = root.GetString("model"),
				ResetScope = root.GetString("scope"),
				SessionId = root.GetString("session_id"),
				PreviousResponseId = root.GetString("previous_response_id"),
				Store = root.GetBool("store"),
				ToolChoice = root["tool_choice"].Exists ? (JsonNode?)root["tool_choice"] : null,
				Tools = root["tools"]
			};
		}
		private static List<Message> ParseRequestMessages(JsonNode messagesToken){
			if(!messagesToken.IsArray) return null;
			var messages = new List<Message>();
			foreach(var messageToken in messagesToken){
				if(!messageToken.IsObject) continue;
				var role = messageToken.GetString("role");
				if(string.IsNullOrWhiteSpace(role)) continue;
				messages.Add(new Message{
					Role = role,
					Content = ExtractContentText(messageToken["content"]),
					ToolCallId = messageToken.GetString("tool_call_id"),
					ToolCallsJson = messageToken["tool_calls"]
				});
			}
			return messages.Count > 0 ? messages : null;
		}
		private class ChatRequest{
			public JsonNode Input;
			public string Instructions;
			public List<Message> Messages;
			public string Model;
			public string ResetScope;
			public string SessionId;
			public string PreviousResponseId;
			public bool? Store;
			public JsonNode? ToolChoice;
			public JsonNode Tools;
		}
		public class Message{
			public string Content;
			public string Role;
			public string ToolCallId;
			internal List<APIClient.ToolCall> ToolCalls;
			internal JsonNode ToolCallsJson;
		}
	}
}
