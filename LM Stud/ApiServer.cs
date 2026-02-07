using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using LMStud.Properties;
using Newtonsoft.Json;
namespace LMStud{
	public class ApiServer{
		private readonly Form1 _form;
		private readonly SessionManager _sessions = new SessionManager();
		private CancellationTokenSource _cts;
		private HttpListener _listener;
		public int Port = 11434;
		public ApiServer(Form1 form){_form = form;}
		public bool IsRunning => _listener != null && _listener.IsListening;
		private static ApiClient CreateApiClient(){return new ApiClient(Settings.Default.ApiClientBaseUrl, Settings.Default.ApiClientKey, Settings.Default.ApiClientModel);}
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
				else if(method == "POST" && path == "/v1/chat/completions") HandleChat(context);
				else if(method == "POST" && path == "/v1/chat/reset") HandleReset(context);
				else context.Response.StatusCode = 404;
			} catch{ context.Response.StatusCode = 500; } finally{
				try{ context.Response.OutputStream.Close(); } catch{}
				if(acquired) _form.GenerationLock.Release();
			}
		}
		private void HandleModels(HttpListenerContext ctx, bool useRemoteApi){
			List<string> models;
			if(useRemoteApi){
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
			}
			else{ models = _form.GetModelNames().ToList(); }
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
			if(Settings.Default.ApiClientEnable){
				HandleRemoteChat(ctx, request);
				return;
			}
			var session = _sessions.Get(request.SessionId);
			ctx.Response.AddHeader("X-Session-Id", session.Id);
			var prompt = request.Messages?.LastOrDefault(m => m.Role == "user")?.Content ?? "";
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
			var resp = new{ session_id = session.Id, choices = new[]{ new{ message = new{ role = "assistant", content = assistant } } } };
			var json = JsonConvert.SerializeObject(resp);
			var bytes = Encoding.UTF8.GetBytes(json);
			ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
		}
		private void HandleRemoteChat(HttpListenerContext ctx, ChatRequest request){
			var session = _sessions.Get(request.SessionId);
			ctx.Response.AddHeader("X-Session-Id", session.Id);
			ctx.Response.ContentType = "application/json";
			try{
				var messages = new List<ApiClient.ChatMessage>();
				if(!string.IsNullOrWhiteSpace(Settings.Default.SystemPrompt)) messages.Add(new ApiClient.ChatMessage("system", Settings.Default.SystemPrompt));
				if(request.Messages != null){
					foreach(var message in request.Messages){
						if(string.IsNullOrWhiteSpace(message?.Content) || string.IsNullOrWhiteSpace(message.Role)) continue;
						messages.Add(new ApiClient.ChatMessage(message.Role, message.Content));
					}
				}
				var client = CreateApiClient();
				var assistant = client.CreateChatCompletion(messages, (float)Settings.Default.Temp, (int)Settings.Default.NGen, CancellationToken.None);
				session.Messages.AddRange(request.Messages ?? new List<Message>());
				session.Messages.Add(new Message{ Role = "assistant", Content = assistant });
				_sessions.Update(session, session.Messages, null, 0);
				var resp = new{ session_id = session.Id, choices = new[]{ new{ message = new{ role = "assistant", content = assistant } } } };
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
		private class ChatRequest{
			public List<Message> Messages;
			[JsonProperty("session_id")] public string SessionId;
		}
		public class Message{
			public string Content;
			public string Role;
		}
	}
}