using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using LMStud.Properties;
using Newtonsoft.Json;
namespace LMStud{
	internal class ApiServer{
		private readonly Form1 _form;
		private readonly SessionManager _sessions;
		private CancellationTokenSource _cts;
		private HttpListener _listener;
		public int Port = 11434;
		public ApiServer(Form1 form){
			_form = form;
			_sessions = new SessionManager(onRemoved: DestroyNativeSession);
		}
		public bool IsRunning => _listener != null && _listener.IsListening;
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
				if(!_form.LlModelLoaded || !_form.GenerationLock.Wait(0)){
					context.Response.StatusCode = 409;
					return;
				}
				acquired = true;
				if(method == "GET" && path == "/v1/models") HandleModels(context);
				else if(method == "GET" && path == "/v1/model") HandleModel(context);
				else if(method == "POST" && path == "/v1/chat/completions") HandleChat(context);
				else if(method == "POST" && path == "/v1/chat/reset") HandleReset(context);
				else context.Response.StatusCode = 404;
			} catch{ context.Response.StatusCode = 500; } finally{
				try{ context.Response.OutputStream.Close(); } catch{}
				if(acquired) _form.GenerationLock.Release();
			}
		}
		private void HandleModels(HttpListenerContext ctx){
			var models = _form.GetModelNames();
			var obj = new{ data = models.Select(m => new{ id = m }).ToArray() };
			var json = JsonConvert.SerializeObject(obj);
			var bytes = Encoding.UTF8.GetBytes(json);
			ctx.Response.ContentType = "application/json";
			ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
		}
		private void HandleModel(HttpListenerContext ctx){
			var modelPath = Settings.Default.LastModel;
			var model = Path.GetFileNameWithoutExtension(modelPath);
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
			_form.ActivateDefaultSession();
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
			var session = _sessions.Get(request.SessionId);
			ctx.Response.AddHeader("X-Session-Id", session.Id);
			var prompt = request.Messages?.LastOrDefault(m => m.Role == "user")?.Content ?? "";
			ctx.Response.ContentType = "application/json";
			var sb = new StringBuilder();
			void TokenCb(string token){sb.Append(token);}
			var error = NativeMethods.StudError.Success;
			string assistant;
			var tokens = 0;
			try{
				var generated = !_form.IsGenerating && _form.GenerateForApi(session.Id, prompt, TokenCb, out error);
				if(!generated){
					if(error != NativeMethods.StudError.Success){
						ctx.Response.StatusCode = 500;
						var detail = NativeMethods.GetLastError();
						NativeMethods.ClearLastErrorMessage();
						if(!string.IsNullOrEmpty(detail)){
							var err = JsonConvert.SerializeObject(new{ error = new{ message = detail, code = error.ToString() } });
							var buf = Encoding.UTF8.GetBytes(err);
							ctx.Response.OutputStream.Write(buf, 0, buf.Length);
						}
					} else{ ctx.Response.StatusCode = 409; }
					return;
				}
				assistant = sb.ToString();
				session.Messages.Add(new Message{ Role = "user", Content = prompt });
				session.Messages.Add(new Message{ Role = "assistant", Content = assistant });
				tokens = _form.GetTokenCount();
			} finally{ _form.ActivateDefaultSession(); }
			_sessions.Update(session, tokens);
			var resp = new{ session_id = session.Id, choices = new[]{ new{ message = new{ role = "assistant", content = assistant } } } };
			var json = JsonConvert.SerializeObject(resp);
			var bytes = Encoding.UTF8.GetBytes(json);
			ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
		}
		private void DestroyNativeSession(string sessionId){
			if(string.IsNullOrEmpty(sessionId) || sessionId == Form1.DefaultSessionId) return;
			_form.DestroySession(sessionId);
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