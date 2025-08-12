using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using LMStud.Properties;
using Newtonsoft.Json;
namespace LMStud{
	internal class ApiServer{
		private readonly Form1 _form;
		private readonly SessionManager _sessions = new SessionManager();
		private CancellationTokenSource _cts;
		private HttpListener _listener;
		public int Port = 11434;
		public ApiServer(Form1 form){_form = form;}
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
				ThreadPool.QueueUserWorkItem(_ => HandleContext(ctx));
			}
		}
		private void HandleContext(HttpListenerContext context){
			try{
				var req = context.Request;
				if(req.HttpMethod == "GET" && req.Url.AbsolutePath == "/v1/models") HandleModels(context);
				else if(req.HttpMethod == "POST" && req.Url.AbsolutePath == "/v1/chat/completions") HandleChat(context);
				else if(req.HttpMethod == "POST" && req.Url.AbsolutePath == "/v1/chat/reset") HandleReset(context);
				else context.Response.StatusCode = 404;
			} catch{} finally{
				try{ context.Response.OutputStream.Close(); } catch{}
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
		private void HandleReset(HttpListenerContext ctx){
			string body;
			using(var reader = new StreamReader(ctx.Request.InputStream, ctx.Request.ContentEncoding)){ body = reader.ReadToEnd(); }
			ChatRequest request = null;
			if(!string.IsNullOrEmpty(body)){
				try{ request = JsonConvert.DeserializeObject<ChatRequest>(body); } catch{}
			}
			NativeMethods.ResetChat();
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
			var session = _sessions.Get(request.SessionId);
			_form.SetState(session.State);
			var prompt = request.Messages?.LastOrDefault(m => m.Role == "user")?.Content ?? "";
			var stream = request.Stream;
			ctx.Response.ContentType = stream ? "text/event-stream" : "application/json";
			ctx.Response.AddHeader("X-Session-Id", session.Id);
			var sb = new StringBuilder();
			void TokenCb(string token){
				sb.Append(token);
				if(stream){
					var chunk = "data: " + JsonConvert.SerializeObject(new{ choices = new[]{ new{ delta = new{ content = token } } } }) + "\n\n";
					var buf = Encoding.UTF8.GetBytes(chunk);
					ctx.Response.OutputStream.Write(buf, 0, buf.Length);
					ctx.Response.OutputStream.Flush();
				}
			}
			_form.GenerateForApi(prompt, TokenCb);
			var assistant = sb.ToString();
			session.Messages.Add(new Message{ Role = "user", Content = prompt });
			session.Messages.Add(new Message{ Role = "assistant", Content = assistant });
			var state = _form.GetState();
			var tokens = _form.GetTokenCount();
			_sessions.Update(session, session.Messages, state, tokens);
			if(stream){
				var end = Encoding.UTF8.GetBytes("data: [DONE]\n\n");
				ctx.Response.OutputStream.Write(end, 0, end.Length);
			} else{
				var resp = new{ session_id = session.Id, choices = new[]{ new{ message = new{ role = "assistant", content = assistant } } } };
				var json = JsonConvert.SerializeObject(resp);
				var bytes = Encoding.UTF8.GetBytes(json);
				ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
			}
		}
		private class ChatRequest{
			public List<Message> Messages;
			[JsonProperty("session_id")] public string SessionId;
			public bool Stream;
		}
		public class Message{
			public string Role;
			public string Content;
		}
	}
}