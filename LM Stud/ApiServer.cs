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
		private readonly SessionManager _sessions = new SessionManager();
		private CancellationTokenSource _cts;
		private HttpListener _listener;
		public int Port = 11434;
		public ApiServer(Form1 form){_form = form;}
		public bool IsRunning => _listener != null && _listener.IsListening;
		private static ApiClient CreateApiClient(){
			return new ApiClient(Settings.Default.ApiClientBaseUrl, Settings.Default.ApiClientKey, Settings.Default.ApiClientModel, Settings.Default.SystemPrompt);
		}
		public void Start(){
			if(IsRunning) return;
			_cts = new CancellationTokenSource();
			_listener = new HttpListener();
			_listener.Prefixes.Add($"http://*:{Port}/");
			_listener.Start();
			ThreadPool.QueueUserWorkItem(o => {ListenLoop(_cts.Token);});
		}
		public void Stop(){
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
			var acquired = false;
			try{
				var req = context.Request;
				var method = req.HttpMethod;
				var path = req.Url.AbsolutePath;
				var useRemoteApi = Settings.Default.ApiClientEnable;
				if(!useRemoteApi && !_form.LlModelLoaded){
					context.Response.StatusCode = 409;
					return;
				}
				if(!_form.GenerationLock.Wait(0)){
					context.Response.StatusCode = 409;
					return;
				}
				acquired = true;
				if(method == "GET" && path == "/v1/models") HandleModels(context, useRemoteApi);
				else if(method == "GET" && path == "/v1/model") HandleModel(context, useRemoteApi);
				else if(method == "POST" && path == "/v1/responses") HandleChat(context);
				else if(method == "POST" && path == "/v1/reset") HandleReset(context);
				else context.Response.StatusCode = 404;
			} catch{ context.Response.StatusCode = 500; } finally{
				try{ context.Response.OutputStream.Close(); } catch{}
				if(acquired) _form.GenerationLock.Release();
			}
		}
		private void HandleModels(HttpListenerContext ctx, bool useRemoteApi){
			List<string> models;
			if(useRemoteApi)
				try{
					var client = CreateApiClient();
					models = client.ListModels(CancellationToken.None);
				} catch(Exception ex){
					ctx.Response.StatusCode = 502;
					var err = JsonConvert.SerializeObject(new{ error = new{ message = ex.Message } });
					var buf = Encoding.UTF8.GetBytes(err);
					ctx.Response.OutputStream.Write(buf, 0, buf.Length);
					return;
				}
			else models = _form.GetModelNames().ToList();
			var obj = new{ data = models.Select(m => new{ id = m }).ToArray() };
			var json = JsonConvert.SerializeObject(obj);
			var bytes = Encoding.UTF8.GetBytes(json);
			ctx.Response.ContentType = "application/json";
			ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
		}
		private void HandleModel(HttpListenerContext ctx, bool useRemoteApi){
			var model = useRemoteApi ? Settings.Default.ApiClientModel : Path.GetFileNameWithoutExtension(Settings.Default.LastModel);
			var obj = new{ model };
			var json = JsonConvert.SerializeObject(obj);
			var bytes = Encoding.UTF8.GetBytes(json);
			ctx.Response.ContentType = "application/json";
			ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
		}
		private void HandleReset(HttpListenerContext ctx){
			string body;
			using(var reader = new StreamReader(ctx.Request.InputStream, ctx.Request.ContentEncoding)){ body = reader.ReadToEnd(); }
			ChatRequest request = null;
			if(!string.IsNullOrEmpty(body))
				try{ request = JsonConvert.DeserializeObject<ChatRequest>(body); } catch{
					ctx.Response.StatusCode = 400;
					return;
				}
			NativeMethods.ResetChat();
			NativeMethods.CloseCommandPrompt();
			if(request?.SessionId != null) _sessions.Remove(request.SessionId);
			var resp = new{ status = "reset" };
			var json = JsonConvert.SerializeObject(resp);
			var bytes = Encoding.UTF8.GetBytes(json);
			ctx.Response.ContentType = "application/json";
			ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
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
			if(messages == null || messages.Count == 0){
				ctx.Response.StatusCode = 400;
				return;
			}
			if(Settings.Default.ApiClientEnable){
				HandleRemoteChat(ctx, request, messages);
				return;
			}
			var session = _sessions.Get(request.SessionId);
			ctx.Response.AddHeader("X-Session-Id", session.Id);
			var prompt = messages.LastOrDefault(m => m.Role == "user")?.Content ?? "";
			ctx.Response.ContentType = "application/json";
			var sb = new StringBuilder();
			void TokenCb(string token){sb.Append(token);}
			if(!_form.GenerateForApi(session.State, prompt, TokenCb, out var newState, out var tokens)){
				ctx.Response.StatusCode = 409;
				return;
			}
			var assistant = sb.ToString();
			session.Messages.Add(new Message{ Role = "user", Content = prompt });
			session.Messages.Add(new Message{ Role = "assistant", Content = assistant });
			_sessions.Update(session, session.Messages, newState, tokens);
			var resp = BuildResponsePayload(assistant, Path.GetFileNameWithoutExtension(Settings.Default.LastModel), session.Id);
			var json = JsonConvert.SerializeObject(resp);
			var bytes = Encoding.UTF8.GetBytes(json);
			ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
		}
		private void HandleRemoteChat(HttpListenerContext ctx, ChatRequest request, List<Message> incomingMessages){
			var session = _sessions.Get(request.SessionId);
			ctx.Response.AddHeader("X-Session-Id", session.Id);
			ctx.Response.ContentType = "application/json";
			try{
				var messages = new List<ApiClient.ChatMessage>();
				foreach(var incoming in incomingMessages ?? Enumerable.Empty<Message>()){
					if(string.IsNullOrWhiteSpace(incoming?.Role)) continue;
					if(string.IsNullOrWhiteSpace(incoming.Content)) continue;
					messages.Add(new ApiClient.ChatMessage(incoming.Role, incoming.Content));
				}
				if(messages.Count == 0){
					ctx.Response.StatusCode = 400;
					return;
				}
				var history = ApiClient.BuildInputItems(messages);
				var toolsJson = _form.BuildApiToolsJson();
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
				if(messages.Count > 0){
					var last = messages.Last();
					session.Messages.Add(new Message{ Role = last.Role, Content = last.Content });
				}
				if(toolOutputs != null){
					foreach(var toolResult in toolOutputs) session.Messages.Add(new Message{ Role = "tool", Content = toolResult });
				}
				session.Messages.Add(new Message{ Role = "assistant", Content = assistant });
				_sessions.Update(session, session.Messages, null, 0);
				var resp = BuildResponsePayload(assistant, Settings.Default.ApiClientModel, session.Id);
				var json = JsonConvert.SerializeObject(resp);
				var bytes = Encoding.UTF8.GetBytes(json);
				ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
			} catch(Exception ex){
				ctx.Response.StatusCode = 502;
				var err = JsonConvert.SerializeObject(new{ error = new{ message = ex.Message } });
				var buf = Encoding.UTF8.GetBytes(err);
				ctx.Response.OutputStream.Write(buf, 0, buf.Length);
			}
		}
		private static object BuildResponsePayload(string assistant, string model, string sessionId){
			var message = new{ id = "msg_" + Guid.NewGuid().ToString("N"), type = "message", role = "assistant", content = new[]{ new{ type = "output_text", text = assistant ?? "" } } };
			return new{
				id = "resp_" + Guid.NewGuid().ToString("N"), @object = "response", created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(), model,
				session_id = sessionId, output = new[]{ message }
			};
		}
		private static List<Message> ParseInputMessages(JToken input){
			if(input == null) return null;
			if(input.Type == JTokenType.String) return new List<Message>{ new Message{ Role = "user", Content = input.ToString() } };
			if(input.Type == JTokenType.Array){
				var messages = new List<Message>();
				foreach(var item in input){
					if(item.Type == JTokenType.String){
						messages.Add(new Message{ Role = "user", Content = item.ToString() });
						continue;
					}
					if(!(item is JObject obj)) continue;
					var role = obj.Value<string>("role") ?? "user";
					var content = ExtractContentText(obj["content"]);
					if(string.IsNullOrWhiteSpace(content)) continue;
					messages.Add(new Message{ Role = role, Content = content });
				}
				return messages;
			}
			return null;
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
			[JsonProperty("tool_choice")] public JToken ToolChoice;
			[JsonProperty("tools")] public JArray Tools;
		}
		public class Message{
			public string Content;
			public string Role;
		}
	}
}