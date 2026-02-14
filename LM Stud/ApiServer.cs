using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using LMStud.Properties;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
namespace LMStud{
	public class ApiServer{
		private readonly Form1 _form;
		internal readonly SessionManager Sessions = new SessionManager();
		private CancellationTokenSource _cts;
		private HttpListener _listener;
		internal int Port = 11434;
		internal ApiServer(Form1 form){_form = form;}
		internal bool IsRunning => _listener != null && _listener.IsListening;
		private ApiClient CreateApiClient(){
			return new ApiClient(Settings.Default.ApiClientBaseUrl, Settings.Default.ApiClientKey, Settings.Default.ApiClientModel, _form.APIClientStore, Settings.Default.SystemPrompt);
		}
		internal void Start(){
			if(IsRunning) return;
			_cts = new CancellationTokenSource();
			_listener = new HttpListener();
			_listener.Prefixes.Add($"http://*:{Port}/");
			_listener.Start();
			ThreadPool.QueueUserWorkItem(o => {ListenLoop(_cts.Token);});
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
				var useRemoteApi = Settings.Default.ApiClientEnable;
				if(!useRemoteApi && !_form.LlModelLoaded){
					context.Response.StatusCode = 409;
					return;
				}
				if(method == "POST" && path == "/v1/responses") HandleChat(context);
				else if(method == "POST" && path == "/v1/reset") HandleReset(context);
				else context.Response.StatusCode = 404;
			} catch{ context.Response.StatusCode = 500; } finally{
				try{ context.Response.OutputStream.Close(); } catch{}
			}
		}
		private void HandleReset(HttpListenerContext ctx){
			if(!_form.GenerationLock.Wait(0)){
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
				var hasSession = !string.IsNullOrWhiteSpace(request?.SessionId);
				if(string.IsNullOrWhiteSpace(resetScope)) resetScope = hasSession ? "session" : "global";
				if(string.Equals(resetScope, "session", StringComparison.OrdinalIgnoreCase)){
					if(hasSession) Sessions.Remove(request.SessionId);
				}
				else{
					Sessions.Clear();
					NativeMethods.ResetChat();
					NativeMethods.CloseCommandPrompt();
				}
				var resp = new{ status = "reset", scope = resetScope.ToLowerInvariant() };
				var json = JsonConvert.SerializeObject(resp);
				var bytes = Encoding.UTF8.GetBytes(json);
				ctx.Response.ContentType = "application/json";
				ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
			} finally{ _form.GenerationLock.Release(); }
		}
		private void HandleChat(HttpListenerContext ctx){
			string body;
			using(var reader = new StreamReader(ctx.Request.InputStream, ctx.Request.ContentEncoding)){ body = reader.ReadToEnd(); }
			ChatRequest request;
			try{ request = JsonConvert.DeserializeObject<ChatRequest>(body); } catch(JsonException ex){
				ctx.Response.StatusCode = 400;
				var err = JsonConvert.SerializeObject(new{ error = new{ message = ex.Message } });
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
			if(Settings.Default.ApiClientEnable){
				HandleRemoteChat(ctx, request, messages, inputItems, store);
				return;
			}
			if(messages == null || messages.Count == 0){
				ctx.Response.StatusCode = 400;
				return;
			}
			var session = Sessions.Get(request.SessionId);
			try{
				ctx.Response.AddHeader("X-Session-Id", session.Id);
				var prompt = messages.LastOrDefault(m => m.Role == "user")?.Content ?? "";
				ctx.Response.ContentType = "application/json";
				var sb = new StringBuilder();
				void TokenCb(string token){sb.Append(token);}
				if(!_form.GenerateForApiServer(session.State, prompt, TokenCb, out var newState, out var tokens)){
					ctx.Response.StatusCode = 409;
					return;
				}
				var assistant = sb.ToString();
				Sessions.Apply(session, s => {
					s.Messages.Add(new Message{ Role = "user", Content = prompt });
					s.Messages.Add(new Message{ Role = "assistant", Content = assistant });
					s.State = newState;
					s.TokenCount = tokens;
				});
				var resp = BuildResponsePayload(assistant, Path.GetFileNameWithoutExtension(Settings.Default.LastModel), session.Id);
				var json = JsonConvert.SerializeObject(resp);
				var bytes = Encoding.UTF8.GetBytes(json);
				ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
			} finally{
				if(!store) Sessions.Remove(session.Id);
			}
		}
		private void HandleRemoteChat(HttpListenerContext ctx, ChatRequest request, List<Message> incomingMessages, JArray inputItems, bool store){
			var session = Sessions.Get(request.SessionId);
			ctx.Response.AddHeader("X-Session-Id", session.Id);
			ctx.Response.ContentType = "application/json";
			try{
				var incomingDelta = BuildIncomingDelta(incomingMessages, inputItems);
				if(incomingDelta == null || incomingDelta.Count == 0){
					ctx.Response.StatusCode = 400;
					return;
				}
				var persisted = BuildPersistedHistory(session.Messages);
				foreach(var item in incomingDelta) persisted.Add(item);
				var history = persisted;
				var toolsJson = request.Tools != null ? request.Tools.ToString(Formatting.None) : _form.BuildApiToolsJson();
				var client = CreateApiClient();
				var result = client.CreateChatCompletion(history, (float)Settings.Default.Temp, (int)Settings.Default.NGen, toolsJson, request.ToolChoice, CancellationToken.None);
				ApiClient.AppendOutputItems(history, result);
				List<string> toolOutputs = null;
				var rounds = 0;
				string lastToolSignature = null;
				while(result.ToolCalls != null && result.ToolCalls.Count > 0 && rounds < 5){
					var toolSignature = string.Join("|", result.ToolCalls.Select(call => $"{call.Id}:{call.Name}:{call.Arguments}"));
					if(toolSignature == lastToolSignature) throw new InvalidOperationException("Repeated tool calls detected.");
					lastToolSignature = toolSignature;
					foreach(var toolCall in result.ToolCalls){
						var toolResult = _form.ExecuteToolCall(toolCall);
						if(toolOutputs == null) toolOutputs = new List<string>();
						toolOutputs.Add(toolResult);
						var toolMessage = new ApiClient.ChatMessage("tool", toolResult){ ToolCallId = toolCall.Id, ToolName = toolCall.Name };
						history.Add(ApiClient.BuildInputMessagePayload(toolMessage));
					}
					result = client.CreateChatCompletion(history, (float)Settings.Default.Temp, (int)Settings.Default.NGen, toolsJson, request.ToolChoice, CancellationToken.None);
					ApiClient.AppendOutputItems(history, result);
					rounds++;
				}
				var assistant = result.Content;
				Sessions.Apply(session, s => {
					foreach(var deltaMessage in incomingDelta){
						var parsed = ParseInputItemToMessage(deltaMessage);
						if(parsed != null) s.Messages.Add(parsed);
					}
					if(toolOutputs != null)
						foreach(var toolResult in toolOutputs)
							s.Messages.Add(new Message{ Role = "tool", Content = toolResult });
					s.Messages.Add(new Message{ Role = "assistant", Content = assistant });
					s.State = null;
					s.TokenCount = 0;
				});
				var resp = BuildResponsePayload(assistant, Settings.Default.ApiClientModel, session.Id);
				var json = JsonConvert.SerializeObject(resp);
				var bytes = Encoding.UTF8.GetBytes(json);
				ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
			} catch(Exception ex){
				ctx.Response.StatusCode = 502;
				var err = JsonConvert.SerializeObject(new{ error = new{ message = ex.Message } });
				var buf = Encoding.UTF8.GetBytes(err);
				ctx.Response.OutputStream.Write(buf, 0, buf.Length);
			} finally{
				if(!store) Sessions.Remove(session.Id);
			}
		}
		private static object BuildResponsePayload(string assistant, string model, string sessionId){
			var text = assistant ?? "";
			var createdAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
			var message = new{
				id = "msg_" + Guid.NewGuid().ToString("N"), type = "message", status = "completed", role = "assistant",
				content = new[]{ new{ type = "output_text", text } }
			};
			return new{
				id = "resp_" + Guid.NewGuid().ToString("N"), @object = "response", created_at = createdAt, status = "completed",
				model, session_id = sessionId, output = new[]{ message }
			};
		}
		private static JArray BuildInputItems(ChatRequest request){
			if(request == null) return null;
			var normalized = NormalizeInputItems(request.Input);
			if(normalized != null && normalized.Count > 0) return normalized;
			if(request.Messages == null || request.Messages.Count == 0) return null;
			var messages = new List<ApiClient.ChatMessage>();
			foreach(var message in request.Messages){
				if(message == null) continue;
				if(string.IsNullOrWhiteSpace(message.Role)) continue;
				messages.Add(new ApiClient.ChatMessage(message.Role, message.Content ?? ""));
			}
			return messages.Count > 0 ? ApiClient.BuildInputItems(messages) : null;
		}
		private static JArray BuildPersistedHistory(List<Message> messages){
			var history = new JArray();
			if(messages == null) return history;
			foreach(var message in messages){
				if(string.IsNullOrWhiteSpace(message?.Role)) continue;
				if(string.IsNullOrWhiteSpace(message.Content)) continue;
				history.Add(new JObject{ ["role"] = message.Role, ["content"] = message.Content });
			}
			return history;
		}
		private static JArray BuildIncomingDelta(List<Message> incomingMessages, JArray inputItems){
			if(incomingMessages != null && incomingMessages.Count > 0){
				var last = incomingMessages.LastOrDefault(m => !string.IsNullOrWhiteSpace(m?.Role) && !string.IsNullOrWhiteSpace(m.Content));
				if(last == null) return null;
				return new JArray(new JObject{ ["role"] = last.Role, ["content"] = last.Content });
			}
			if(inputItems == null || inputItems.Count == 0) return null;
			var lastItem = inputItems.LastOrDefault();
			if(lastItem == null) return null;
			return new JArray(lastItem.DeepClone());
		}
		private static Message ParseInputItemToMessage(JToken item){
			if(!(item is JObject obj)) return null;
			var role = obj.Value<string>("role") ?? "user";
			var content = ExtractContentText(obj["content"]) ?? obj.Value<string>("text");
			if(string.IsNullOrWhiteSpace(content)) return null;
			return new Message{ Role = role, Content = content };
		}
		private static JArray NormalizeInputItems(JToken input){
			if(input == null || input.Type == JTokenType.Null) return null;
			if(input.Type == JTokenType.String) return new JArray(new JObject{ ["role"] = "user", ["content"] = input.ToString() });
			if(input is JObject inputObj) return new JArray(inputObj.DeepClone());
			if(input is JArray array){
				var items = new JArray();
				foreach(var item in array){
					if(item == null || item.Type == JTokenType.Null) continue;
					if(item.Type == JTokenType.String){
						items.Add(new JObject{ ["role"] = "user", ["content"] = item.ToString() });
						continue;
					}
					if(item is JObject obj) items.Add(obj.DeepClone());
				}
				return items.Count > 0 ? items : null;
			}
			return null;
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
					if(string.IsNullOrWhiteSpace(content)) continue;
					messages.Add(new Message{ Role = role, Content = content });
					continue;
				}
				if(string.Equals(type, "input_text", StringComparison.OrdinalIgnoreCase) || string.Equals(type, "output_text", StringComparison.OrdinalIgnoreCase)){
					var content = obj.Value<string>("text") ?? obj.Value<string>("content");
					if(string.IsNullOrWhiteSpace(content)) continue;
					messages.Add(new Message{ Role = "user", Content = content });
					continue;
				}
				if(string.Equals(type, "function_call_output", StringComparison.OrdinalIgnoreCase) || string.Equals(type, "tool_result", StringComparison.OrdinalIgnoreCase)){
					var content = obj.Value<string>("output") ?? obj.Value<string>("content") ?? obj.Value<string>("text");
					if(string.IsNullOrWhiteSpace(content)) continue;
					messages.Add(new Message{ Role = "tool", Content = content });
					continue;
				}
				if(string.Equals(type, "function_call", StringComparison.OrdinalIgnoreCase) || string.Equals(type, "tool_call", StringComparison.OrdinalIgnoreCase)) continue;
				var roleFallback = obj.Value<string>("role") ?? "user";
				var contentFallback = ExtractContentText(obj["content"]) ?? obj.Value<string>("text") ?? obj.Value<string>("content");
				if(string.IsNullOrWhiteSpace(contentFallback)) continue;
				messages.Add(new Message{ Role = roleFallback, Content = contentFallback });
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
					if(item is JObject obj){
						var text = obj.Value<string>("text") ?? obj.Value<string>("content");
						if(!string.IsNullOrWhiteSpace(text)) sb.Append(text);
					}
				}
				return sb.Length > 0 ? sb.ToString() : null;
			}
			if(contentToken is JObject contentObj) return contentObj.Value<string>("text") ?? contentObj.Value<string>("content");
			return null;
		}
		private class ChatRequest{
			[JsonProperty("input")] public JToken Input;
			public List<Message> Messages;
			[JsonProperty("session_id")] public string SessionId;
			[JsonProperty("store")] public bool? Store;
			[JsonProperty("tool_choice")] public JToken ToolChoice;
			[JsonProperty("tools")] public JArray Tools;
			[JsonProperty("scope")] public string ResetScope;
		}
		public class Message{
			public string Content;
			public string Role;
		}
	}
}