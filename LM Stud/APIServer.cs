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
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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
				MessageBox.Show($"API server could not start on port {Common.APIServerPort} because the port is already in use.\r\n\r\n{ex.Message}", Resources.LM_Stud, MessageBoxButtons.OK, MessageBoxIcon.Error);
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
				HandleContext(ctx);
			}
		}
		private void HandleContext(HttpListenerContext context){
			try{
				var req = context.Request;
				var method = req.HttpMethod;
				var path = req.Url.AbsolutePath;
				var useRemoteApi = Settings.Default.APIClientEnable;
				if(!useRemoteApi && !Common.LlModelLoaded){
					context.Response.StatusCode = 409;
					return;
				}
				if(method == "POST" && path == "/v1/responses") HandleChat(context, false);
				else if(method == "POST" && path == "/v1/chat/completions") HandleChat(context, true);
				else if(method == "POST" && path == "/v1/reset") HandleReset(context);
				else context.Response.StatusCode = 404;
			} catch{ context.Response.StatusCode = 500; } finally{
				try{ context.Response.OutputStream.Close(); } catch{}
			}
		}
		private void HandleReset(HttpListenerContext ctx){
			if(!Generation.GenerationLock.Wait(0)){
				ctx.Response.StatusCode = 409;
				return;
			}
			try{
				string body;
				using(var reader = new StreamReader(ctx.Request.InputStream, ctx.Request.ContentEncoding)){ body = reader.ReadToEnd(); }
				ChatRequest request = null;
				if(!string.IsNullOrEmpty(body))
					try{ request = JsonConvert.DeserializeObject<ChatRequest>(body); } catch{
						ctx.Response.StatusCode = 400;
						return;
					}
				var resetScope = request?.ResetScope;
				var sessionId = ResolveSessionId(ctx, request);
				var hasSession = !string.IsNullOrWhiteSpace(sessionId);
				if(string.IsNullOrWhiteSpace(resetScope)) resetScope = hasSession ? "session" : "global";
				if(string.Equals(resetScope, "session", StringComparison.OrdinalIgnoreCase)){
					if(hasSession) Sessions.Remove(sessionId);
				}
				else{
					Sessions.Clear();
					NativeMethods.ResetChat();
					NativeMethods.CloseCommandPrompt();
				}
				var resp = new{ status = "reset", scope = resetScope.ToLowerInvariant() };
				var json = JsonConvert.SerializeObject(resp, Formatting.Indented);
				var bytes = Encoding.UTF8.GetBytes(json);
				ctx.Response.ContentType = "application/json";
				ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
			} finally{ Generation.GenerationLock.Release(); }
		}
		private void HandleChat(HttpListenerContext ctx, bool outputChatCompletions){
			string body;
			using(var reader = new StreamReader(ctx.Request.InputStream, ctx.Request.ContentEncoding)){ body = reader.ReadToEnd(); }
			ChatRequest request;
			try{ request = JsonConvert.DeserializeObject<ChatRequest>(body); } catch(JsonException ex){
				ctx.Response.StatusCode = 400;
				var err = JsonConvert.SerializeObject(new{ error = new{ message = ex.Message } }, Formatting.Indented);
				var buf = Encoding.UTF8.GetBytes(err);
				ctx.Response.OutputStream.Write(buf, 0, buf.Length);
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
			if(Settings.Default.APIClientEnable){
				HandleRemoteChat(ctx, request, messages, inputItems, store, outputChatCompletions);
				return;
			}
			var session = Sessions.Get(ResolveSessionId(ctx, request));
			ResetSessionForModeSwitch(session, false);
			var newChatState = IntPtr.Zero;
			try{
				ctx.Response.AddHeader("X-Session-Id", session.Id);
				var historyMessages = ExtractHistoryAndLatestInput(messages, inputItems, out var latestInput);
				if(latestInput?.Content == null){
					ctx.Response.StatusCode = 400;
					return;
				}
				ctx.Response.ContentType = "application/json";
				var toolsJson = ResolveClientToolsJson(session, request);
				var historyJson = historyMessages != null && historyMessages.Count > 0 ? BuildChatHistoryJson(historyMessages) : null;
				if(!Generation.GenerateForApiServer(session.State, session.NativeChatState, historyJson, ToNativeRole(latestInput.Role), latestInput.Content, toolsJson, out var result, out var newState, out newChatState, out var tokens)){
					ctx.Response.StatusCode = 409;
					return;
				}
				Sessions.Apply(session, s => {
					s.LastBackend = "local";
					if(request.Tools != null) s.ToolsJson = toolsJson;
					if(historyMessages != null && historyMessages.Count > 0) s.Messages = CloneMessages(historyMessages);
					s.Messages.Add(CloneMessage(latestInput));
					s.Messages.Add(new Message{ Role = "assistant", Content = result.Content, ToolCalls = result.ToolCalls });
					s.State = newState;
					s.SetNativeChatState(newChatState);
					newChatState = IntPtr.Zero;
					s.TokenCount = tokens;
				});
				var model = Path.GetFileNameWithoutExtension(Common.LoadedModel.SubItems[1].Text);
				var resp = outputChatCompletions ? BuildChatCompletionsPayload(result, model, session.Id) : BuildResponsePayload(result, model, session.Id);
				Sessions.RememberResponse(resp.Value<string>("id"), session);
				var json = JsonConvert.SerializeObject(resp, Formatting.Indented);
				var bytes = Encoding.UTF8.GetBytes(json);
				ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
			} finally{
				if(newChatState != IntPtr.Zero) NativeMethods.FreeChatState(newChatState);
				if(!store) Sessions.Remove(session.Id);
			}
		}
		private void HandleRemoteChat(HttpListenerContext ctx, ChatRequest request, List<Message> incomingMessages, JArray inputItems, bool store, bool outputChatCompletions){
			var session = Sessions.Get(ResolveSessionId(ctx, request));
			ctx.Response.AddHeader("X-Session-Id", session.Id);
			ctx.Response.ContentType = "application/json";
			APIClient client = null;
			try{
				var incomingDelta = BuildIncomingDelta(incomingMessages, inputItems);
				if(incomingDelta == null || incomingDelta.Count == 0){
					ctx.Response.StatusCode = 400;
					return;
				}
				ResetSessionForModeSwitch(session, true);
				var persisted = BuildPersistedHistory(session.Messages, session.ToolRecords);
				foreach(var item in incomingDelta) persisted.Add(item);
				var toolsJson = ResolveClientToolsJson(session, request);
				var instructions = request.Instructions ?? Common.SystemPrompt;
				client = new APIClient(Common.APIClientUrl, Common.APIClientKey, Common.APIClientModel, false, instructions);
				var result = client.CreateChatCompletion(persisted, (float)Settings.Default.Temp, (int)Settings.Default.NGen, toolsJson, request.ToolChoice,
					request.Reasoning ?? APIClient.BuildReasoningPayload(Common.APIClientReasoningEffort), CancellationToken.None);
				APIClient.AppendOutputItems(persisted, result);
				Sessions.Apply(session, s => {
					if(request.Tools != null) s.ToolsJson = toolsJson;
					foreach(var deltaMessage in incomingDelta){
						var parsed = ParseInputItemToMessage(deltaMessage);
						if(parsed != null) s.Messages.Add(parsed);
					}
					s.Messages.Add(new Message{ Role = "assistant", Content = result.Content, ToolCalls = result.ToolCalls });
					s.State = null;
					s.TokenCount = result.TotalTokens ?? 0;
					s.LastBackend = "remote";
				});
				var resp = outputChatCompletions ? BuildChatCompletionsPayload(result, Settings.Default.APIClientModel, session.Id)
					: BuildResponsePayload(result, Settings.Default.APIClientModel, session.Id);
				Sessions.RememberResponse(resp.Value<string>("id"), session);
				var json = JsonConvert.SerializeObject(resp, Formatting.Indented);
				var bytes = Encoding.UTF8.GetBytes(json);
				ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
			} catch(Exception ex){
				ctx.Response.StatusCode = 502;
				var err = JsonConvert.SerializeObject(new{ error = new{ message = ex.Message } }, Formatting.Indented);
				var buf = Encoding.UTF8.GetBytes(err);
				ctx.Response.OutputStream.Write(buf, 0, buf.Length);
			} finally{
				if(!store) Sessions.Remove(session.Id);
				client?.Dispose();
			}
		}
		private void ResetSessionForModeSwitch(SessionManager.Session session, bool useRemoteApi){
			if(session == null) return;
			var expected = useRemoteApi ? "remote" : "local";
			Sessions.Apply(session, s => {
				if(string.IsNullOrEmpty(s.LastBackend) || s.LastBackend == expected) return;
					s.Messages.Clear();
					s.State = null;
					s.TokenCount = 0;
					s.ToolRecords.Clear();
					s.ToolsJson = null;
					s.ClearNativeChatState();
					s.LastBackend = expected;
				});
		}
		private string ResolveSessionId(HttpListenerContext ctx, ChatRequest request){
			if(!string.IsNullOrWhiteSpace(request?.SessionId)) return request.SessionId;
			var headerSessionId = ctx?.Request?.Headers["X-Session-Id"];
			if(!string.IsNullOrWhiteSpace(headerSessionId)) return headerSessionId;
			return Sessions.GetSessionIdForResponse(request?.PreviousResponseId);
		}
		private static JObject BuildResponsePayload(APIClient.ChatCompletionResult result, string model, string sessionId){
			var createdAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
			var output = result?.OutputItems != null && result.OutputItems.Count > 0 ? (JArray)result.OutputItems.DeepClone() : BuildResponseOutputItems(result?.Content, result?.ToolCalls);
			return new JObject{
				["id"] = "resp_" + Guid.NewGuid().ToString("N"), ["object"] = "response", ["created_at"] = createdAt, ["status"] = "completed",
				["model"] = model, ["session_id"] = sessionId, ["output"] = output
			};
		}
		private static JObject BuildChatCompletionsPayload(APIClient.ChatCompletionResult result, string model, string sessionId){
			var createdAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
			var message = new JObject{ ["role"] = "assistant", ["content"] = result?.Content ?? "" };
			var toolCalls = BuildChatCompletionToolCalls(result?.ToolCalls);
			var finishReason = "stop";
			if(toolCalls != null && toolCalls.Count > 0){
				message["tool_calls"] = toolCalls;
				finishReason = "tool_calls";
			}
			return new JObject{
				["id"] = "chatcmpl_" + Guid.NewGuid().ToString("N"), ["object"] = "chat.completion", ["created"] = createdAt, ["model"] = model,
				["session_id"] = sessionId,
				["choices"] = new JArray(new JObject{ ["index"] = 0, ["message"] = message, ["finish_reason"] = finishReason })
			};
		}
		private static JArray BuildResponseOutputItems(string content, List<APIClient.ToolCall> toolCalls){
			var output = new JArray();
			if(!string.IsNullOrWhiteSpace(content) || toolCalls == null || toolCalls.Count == 0){
				output.Add(new JObject{
					["type"] = "message", ["status"] = "completed", ["role"] = "assistant",
					["content"] = new JArray(new JObject{ ["type"] = "output_text", ["text"] = content ?? "" })
				});
			}
			if(toolCalls != null)
				foreach(var toolCall in toolCalls.Where(call => call != null && !string.IsNullOrWhiteSpace(call.Id) && !string.IsNullOrWhiteSpace(call.Name))){
					output.Add(new JObject{
						["type"] = "function_call", ["status"] = "completed", ["call_id"] = toolCall.Id, ["name"] = toolCall.Name, ["arguments"] = toolCall.Arguments ?? ""
					});
				}
			return output;
		}
		private static JArray BuildChatCompletionToolCalls(List<APIClient.ToolCall> toolCalls){
			if(toolCalls == null || toolCalls.Count == 0) return null;
			var array = new JArray();
			foreach(var toolCall in toolCalls.Where(call => call != null && !string.IsNullOrWhiteSpace(call.Id) && !string.IsNullOrWhiteSpace(call.Name))){
				array.Add(new JObject{
					["id"] = toolCall.Id, ["type"] = "function",
					["function"] = new JObject{ ["name"] = toolCall.Name, ["arguments"] = toolCall.Arguments ?? "" }
				});
			}
			return array.Count > 0 ? array : null;
		}
		private static JArray BuildInputItems(ChatRequest request){
			if(request == null) return null;
			var normalized = NormalizeInputItems(request.Input);
			if(normalized != null && normalized.Count > 0) return normalized;
			if(request.Messages == null || request.Messages.Count == 0) return null;
			var items = new JArray();
			foreach(var message in request.Messages){
				if(message == null) continue;
				if(string.IsNullOrWhiteSpace(message.Role)) continue;
				var apiMessage = new APIClient.ChatMessage(message.Role, message.Content ?? ""){ ToolCallId = message.ToolCallId, ToolCalls = GetToolCalls(message) };
				var hasToolCalls = apiMessage.ToolCalls != null && apiMessage.ToolCalls.Count > 0;
				var hasContent = !string.IsNullOrWhiteSpace(apiMessage.Content);
				if(string.Equals(apiMessage.Role, "tool", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(apiMessage.ToolCallId))
					items.Add(new JObject{ ["role"] = apiMessage.Role, ["content"] = apiMessage.Content ?? "" });
				else if(hasContent || !hasToolCalls) items.Add(APIClient.BuildInputMessagePayload(apiMessage));
				if(apiMessage.ToolCalls == null) continue;
				foreach(var toolCall in apiMessage.ToolCalls.Where(call => call != null && !string.IsNullOrWhiteSpace(call.Id) && !string.IsNullOrWhiteSpace(call.Name)))
					items.Add(new JObject{ ["type"] = "function_call", ["call_id"] = toolCall.Id, ["name"] = toolCall.Name, ["arguments"] = toolCall.Arguments ?? "" });
			}
			return items.Count > 0 ? items : null;
		}
		private static string ResolveClientToolsJson(SessionManager.Session session, ChatRequest request){
			if(request?.Tools != null) return request.Tools.Count > 0 ? request.Tools.ToString(Formatting.None) : null;
			return session?.ToolsJson;
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
			return message?.ToolCalls ?? APIResponseParserCommon.ParseToolCalls(message?.ToolCallsJson);
		}
		private static MessageRole ToNativeRole(string role){
			if(string.Equals(role, "assistant", StringComparison.OrdinalIgnoreCase)) return MessageRole.Assistant;
			if(string.Equals(role, "tool", StringComparison.OrdinalIgnoreCase)) return MessageRole.Tool;
			return MessageRole.User;
		}
		private static JArray BuildPersistedHistory(List<Message> messages, List<ToolRecord> toolRecords){
			var history = new JArray();
			if(messages == null) return history;
			foreach(var message in messages){
				if(string.IsNullOrWhiteSpace(message?.Role)) continue;
				var toolCalls = GetToolCalls(message);
				var hasToolCalls = toolCalls != null && toolCalls.Count > 0;
				if(string.IsNullOrWhiteSpace(message.Content) && !hasToolCalls) continue;
				if(string.Equals(message.Role, "tool", StringComparison.OrdinalIgnoreCase)){
					if(!string.IsNullOrWhiteSpace(message.ToolCallId)){
						history.Add(new JObject{ ["type"] = "function_call_output", ["call_id"] = message.ToolCallId, ["output"] = message.Content ?? "" });
						continue;
					}
					var record = toolRecords?.FirstOrDefault(t => !t.Consumed && string.Equals(t.Output, message.Content, StringComparison.Ordinal));
					if(record != null && !string.IsNullOrWhiteSpace(record.CallId)){
						history.Add(new JObject{ ["type"] = "function_call_output", ["call_id"] = record.CallId, ["output"] = message.Content });
						record.Consumed = true;
						continue;
					}
				}
				if(!hasToolCalls || !string.IsNullOrWhiteSpace(message.Content))
					history.Add(new JObject{ ["role"] = message.Role, ["content"] = message.Content ?? "" });
				if(hasToolCalls)
					foreach(var toolCall in toolCalls.Where(call => call != null && !string.IsNullOrWhiteSpace(call.Id) && !string.IsNullOrWhiteSpace(call.Name)))
						history.Add(new JObject{ ["type"] = "function_call", ["call_id"] = toolCall.Id, ["name"] = toolCall.Name, ["arguments"] = toolCall.Arguments ?? "" });
			}
			return history;
		}
		private static JArray BuildIncomingDelta(List<Message> incomingMessages, JArray inputItems){
			if(incomingMessages != null && incomingMessages.Count > 0){
				var last = incomingMessages.LastOrDefault(m => !string.IsNullOrWhiteSpace(m?.Role) && (m.Content != null || (GetToolCalls(m) != null && GetToolCalls(m).Count > 0)));
				if(last == null) return null;
				var apiMessage = new APIClient.ChatMessage(last.Role, last.Content ?? ""){ ToolCallId = last.ToolCallId, ToolCalls = GetToolCalls(last) };
				if(string.Equals(apiMessage.Role, "tool", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(apiMessage.ToolCallId))
					return new JArray(new JObject{ ["role"] = apiMessage.Role, ["content"] = apiMessage.Content ?? "" });
				return APIClient.BuildInputItems(new[]{ apiMessage });
			}
			if(inputItems == null || inputItems.Count == 0) return null;
			var delta = new JArray();
			foreach(var item in inputItems) delta.Add(item.DeepClone());
			return delta;
		}
		private static List<Message> ExtractHistoryAndLatestInput(List<Message> messages, JArray inputItems, out Message latest){
			latest = null;
			if(messages != null && messages.Count > 0){
				for(var i = messages.Count - 1; i >= 0; i--){
					var candidate = messages[i];
					if(string.IsNullOrWhiteSpace(candidate?.Role) || candidate.Content == null) continue;
					latest = CloneMessage(candidate);
					return CloneMessages(messages.Take(i).Where(HasMessagePayload));
				}
			}
			latest = ExtractLatestInputMessage(null, inputItems);
			return new List<Message>();
		}
		private static bool HasMessagePayload(Message message){
			if(string.IsNullOrWhiteSpace(message?.Role)) return false;
			var toolCalls = GetToolCalls(message);
			return message.Content != null || !string.IsNullOrWhiteSpace(message.ToolCallId) || (toolCalls != null && toolCalls.Count > 0);
		}
		private static string BuildChatHistoryJson(IEnumerable<Message> messages){
			var array = new JArray();
			foreach(var message in messages ?? Enumerable.Empty<Message>()){
				if(!HasMessagePayload(message)) continue;
				var toolCalls = GetToolCalls(message);
				var obj = new JObject{ ["role"] = message.Role };
				if(message.Content != null || toolCalls == null || toolCalls.Count == 0) obj["content"] = message.Content ?? "";
				if(!string.IsNullOrWhiteSpace(message.ToolCallId)) obj["tool_call_id"] = message.ToolCallId;
				var toolCallsJson = BuildChatCompletionToolCalls(toolCalls);
				if(toolCallsJson != null) obj["tool_calls"] = toolCallsJson;
				array.Add(obj);
			}
			return array.Count > 0 ? array.ToString(Formatting.None) : null;
		}
		private static Message ExtractLatestInputMessage(List<Message> messages, JArray inputItems){
			if(messages != null && messages.Count > 0){
				var latest = messages.LastOrDefault(m => !string.IsNullOrWhiteSpace(m?.Role) && m.Content != null);
				if(latest != null) return latest;
			}
			if(inputItems == null || inputItems.Count == 0) return null;
			for(var i = inputItems.Count - 1; i >= 0; i--){
				var parsed = ParseInputItemToMessage(inputItems[i]);
				if(parsed == null || parsed.Content == null) continue;
				return parsed;
			}
			return null;
		}
		private static Message ParseInputItemToMessage(JToken item){
			if(!(item is JObject obj)) return null;
			var type = obj.Value<string>("type");
			if(string.Equals(type, "function_call_output", StringComparison.OrdinalIgnoreCase) || string.Equals(type, "tool_result", StringComparison.OrdinalIgnoreCase)){
				var output = obj.Value<string>("output") ?? obj.Value<string>("content") ?? obj.Value<string>("text") ?? "";
				var callId = obj.Value<string>("call_id") ?? obj.Value<string>("tool_call_id") ?? obj.Value<string>("id");
				return new Message{ Role = "tool", Content = output, ToolCallId = callId };
			}
			if(string.Equals(type, "function_call", StringComparison.OrdinalIgnoreCase) || string.Equals(type, "tool_call", StringComparison.OrdinalIgnoreCase)){
				var toolCall = APIResponseParserCommon.ParseToolCallItem(obj);
				if(toolCall == null) return null;
				return new Message{ Role = "assistant", Content = "", ToolCalls = new List<APIClient.ToolCall>{ toolCall } };
			}
			var role = obj.Value<string>("role") ?? "user";
			var content = ExtractContentText(obj["content"]) ?? obj.Value<string>("text");
			var toolCalls = APIResponseParserCommon.ParseToolCalls(obj["tool_calls"]);
			content = StripToolCallDisplayText(role, content);
			if(string.IsNullOrWhiteSpace(content) && (toolCalls == null || toolCalls.Count == 0)) return null;
			return new Message{ Role = role, Content = content ?? "", ToolCalls = toolCalls };
		}
		private static string StripToolCallDisplayText(string role, string content){
			if(!string.Equals(role, "assistant", StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(content)) return content;
			var index = content.IndexOf(Resources.__Tool_name_, StringComparison.Ordinal);
			if(index < 0) index = content.IndexOf("\nTool name:", StringComparison.OrdinalIgnoreCase);
			if(index < 0) index = content.IndexOf("\n工具名称", StringComparison.Ordinal);
			if(index < 0) return content;
			return content.Substring(0, index).TrimEnd();
		}
		private static JArray NormalizeInputItems(JToken input){
			if(input == null || input.Type == JTokenType.Null) return null;
			if(input.Type == JTokenType.String) return new JArray(new JObject{ ["role"] = "user", ["content"] = input.ToString() });
			if(input is JObject inputObj){
				var normalizedObj = NormalizeInputObject(inputObj);
				return normalizedObj == null ? null : new JArray(normalizedObj);
			}
			if(input is JArray array){
				var items = new JArray();
				foreach(var item in array){
					if(item == null || item.Type == JTokenType.Null) continue;
					if(item.Type == JTokenType.String){
						items.Add(new JObject{ ["role"] = "user", ["content"] = item.ToString() });
						continue;
					}
					if(item is JObject obj){
						var normalizedObj = NormalizeInputObject(obj);
						if(normalizedObj != null) items.Add(normalizedObj);
					}
				}
				return items.Count > 0 ? items : null;
			}
			return null;
		}
		private static JObject NormalizeInputObject(JObject obj){
			var clone = (JObject)obj.DeepClone();
			var role = clone.Value<string>("role");
			if(!string.Equals(role, "assistant", StringComparison.OrdinalIgnoreCase)) return clone;
			var content = ExtractContentText(clone["content"]) ?? clone.Value<string>("text");
			var stripped = StripToolCallDisplayText(role, content);
			if(string.Equals(stripped, content, StringComparison.Ordinal)) return clone;
			var toolCalls = APIResponseParserCommon.ParseToolCalls(clone["tool_calls"]);
			if(string.IsNullOrWhiteSpace(stripped) && (toolCalls == null || toolCalls.Count == 0)) return null;
			if(clone["content"] != null) clone["content"] = stripped ?? "";
			else clone["text"] = stripped ?? "";
			return clone;
		}
		private static List<Message> ParseInputMessages(JToken input){
			if(input == null || input.Type == JTokenType.Null) return null;
			if(input.Type == JTokenType.String) return new List<Message>{ new Message{ Role = "user", Content = input.ToString() } };
			var inputItems = input as JArray;
			if(inputItems == null && input is JObject inputObj) inputItems = new JArray(inputObj);
			if(inputItems == null) return null;
			var messages = new List<Message>();
			foreach(var item in inputItems){
				if(item == null || item.Type == JTokenType.Null) continue;
				if(item.Type == JTokenType.String){
					messages.Add(new Message{ Role = "user", Content = item.ToString() });
					continue;
				}
				if(!(item is JObject obj)) continue;
				var type = obj.Value<string>("type");
				if(string.Equals(type, "message", StringComparison.OrdinalIgnoreCase)){
					var role = obj.Value<string>("role") ?? "user";
					var content = ExtractContentText(obj["content"]);
					var toolCalls = APIResponseParserCommon.ParseToolCalls(obj["tool_calls"]);
					content = StripToolCallDisplayText(role, content);
					if(string.IsNullOrWhiteSpace(content) && (toolCalls == null || toolCalls.Count == 0)) continue;
					messages.Add(new Message{ Role = role, Content = content ?? "", ToolCalls = toolCalls });
					continue;
				}
				if(string.Equals(type, "input_text", StringComparison.OrdinalIgnoreCase) || string.Equals(type, "output_text", StringComparison.OrdinalIgnoreCase)){
					var content = obj.Value<string>("text") ?? obj.Value<string>("content");
					if(string.IsNullOrWhiteSpace(content)) continue;
					messages.Add(new Message{ Role = "user", Content = content });
					continue;
				}
				if(string.Equals(type, "function_call_output", StringComparison.OrdinalIgnoreCase) || string.Equals(type, "tool_result", StringComparison.OrdinalIgnoreCase)){
					var content = obj.Value<string>("output") ?? obj.Value<string>("content") ?? obj.Value<string>("text") ?? "";
					var callId = obj.Value<string>("call_id") ?? obj.Value<string>("tool_call_id") ?? obj.Value<string>("id");
					messages.Add(new Message{ Role = "tool", Content = content, ToolCallId = callId });
					continue;
				}
				if(string.Equals(type, "function_call", StringComparison.OrdinalIgnoreCase) || string.Equals(type, "tool_call", StringComparison.OrdinalIgnoreCase)){
					var toolCall = APIResponseParserCommon.ParseToolCallItem(obj);
					if(toolCall != null) messages.Add(new Message{ Role = "assistant", Content = "", ToolCalls = new List<APIClient.ToolCall>{ toolCall } });
					continue;
				}
				var roleFallback = obj.Value<string>("role") ?? "user";
				var contentFallback = ExtractContentText(obj["content"]) ?? obj.Value<string>("text") ?? obj.Value<string>("content");
				var fallbackToolCalls = APIResponseParserCommon.ParseToolCalls(obj["tool_calls"]);
				contentFallback = StripToolCallDisplayText(roleFallback, contentFallback);
				if(string.IsNullOrWhiteSpace(contentFallback) && (fallbackToolCalls == null || fallbackToolCalls.Count == 0)) continue;
				messages.Add(new Message{ Role = roleFallback, Content = contentFallback ?? "", ToolCalls = fallbackToolCalls });
			}
			return messages.Count > 0 ? messages : null;
		}
		private static string ExtractContentText(JToken contentToken){
			if(contentToken == null || contentToken.Type == JTokenType.Null) return null;
			if(contentToken.Type == JTokenType.String) return contentToken.ToString();
			if(contentToken.Type == JTokenType.Array){
				var sb = new StringBuilder();
				foreach(var item in contentToken){
					if(item == null || item.Type == JTokenType.Null) continue;
					if(item.Type == JTokenType.String){
						sb.Append(item);
						continue;
					}
					if(!(item is JObject obj)) continue;
					var text = obj.Value<string>("text") ?? obj.Value<string>("content");
					if(!string.IsNullOrWhiteSpace(text)) sb.Append(text);
				}
				return sb.Length > 0 ? sb.ToString() : null;
			}
			if(contentToken is JObject contentObj) return contentObj.Value<string>("text") ?? contentObj.Value<string>("content");
			return null;
		}
		private class ChatRequest{
			[JsonProperty("input")] public JToken Input;
			[JsonProperty("instructions")] public string Instructions;
			[JsonProperty("messages")] public List<Message> Messages;
			[JsonProperty("scope")] public string ResetScope;
			[JsonProperty("session_id")] public string SessionId;
			[JsonProperty("previous_response_id")] public string PreviousResponseId;
			[JsonProperty("reasoning")] public JToken Reasoning;
			[JsonProperty("store")] public bool? Store;
			[JsonProperty("tool_choice")] public JToken ToolChoice;
			[JsonProperty("tools")] public JArray Tools;
		}
		public class ToolRecord{
			public string CallId;
			public string Name;
			public string Output;
			internal bool Consumed;
		}
		public class Message{
			[JsonProperty("content")] public string Content;
			[JsonProperty("role")] public string Role;
			[JsonProperty("tool_call_id")] public string ToolCallId;
			[JsonIgnore] internal List<APIClient.ToolCall> ToolCalls;
			[JsonProperty("tool_calls")] public JArray ToolCallsJson;
		}
	}
}
