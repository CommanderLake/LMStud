using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
namespace LMStud{
	internal class ApiServer{
		private readonly Form1 _form;
		private CancellationTokenSource _cts;
		private HttpListener _listener;
		public ApiServer(Form1 form){_form = form;}
		public int Port = 11434;
		public bool IsRunning => _listener != null && _listener.IsListening;
		public void Start(){
			if(IsRunning) return;
			_cts = new CancellationTokenSource();
			_listener = new HttpListener();
			_listener.Prefixes.Add($"http://*:{Port}/");
			_listener.Start();
			Task.Run(() => ListenLoop(_cts.Token));
		}
		public void Stop(){
			if(!IsRunning) return;
			_cts.Cancel();
			try{ _listener.Stop(); } catch{}
			_listener.Close();
			_listener = null;
		}
		private async Task ListenLoop(CancellationToken token){
			while(!token.IsCancellationRequested){
				HttpListenerContext ctx;
				try{ ctx = await _listener.GetContextAsync(); } catch{ break; }
				_ = Task.Run(() => HandleContext(ctx), token);
			}
		}
		private void HandleContext(HttpListenerContext context){
			try{
				var req = context.Request;
				if(req.HttpMethod == "GET" && req.Url.AbsolutePath == "/v1/models") HandleModels(context);
				else if(req.HttpMethod == "POST" && req.Url.AbsolutePath == "/v1/chat/completions") HandleChat(context);
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
		private void HandleChat(HttpListenerContext ctx){
			string body;
			using(var reader = new StreamReader(ctx.Request.InputStream, ctx.Request.ContentEncoding)){ body = reader.ReadToEnd(); }
			var request = JsonConvert.DeserializeObject<ChatRequest>(body);
			if(request == null) return;
			var prompt = request.Messages?.LastOrDefault(m => m.Role == "user")?.Content ?? "";
			var stream = request.Stream;
			ctx.Response.ContentType = stream ? "text/event-stream" : "application/json";
			var sb = new StringBuilder();
			void TokenCb(string token){
				if(stream){
					var chunk = "data: " + JsonConvert.SerializeObject(new{ choices = new[]{ new{ delta = new{ content = token } } } }) + "\n\n";
					var buf = Encoding.UTF8.GetBytes(chunk);
					ctx.Response.OutputStream.Write(buf, 0, buf.Length);
					ctx.Response.OutputStream.Flush();
				} else{ sb.Append(token); }
			}
			_form.GenerateForApi(prompt, TokenCb);
			if(stream){
				var end = Encoding.UTF8.GetBytes("data: [DONE]\n\n");
				ctx.Response.OutputStream.Write(end, 0, end.Length);
			} else{
				var resp = new{ choices = new[]{ new{ message = new{ role = "assistant", content = sb.ToString() } } } };
				var json = JsonConvert.SerializeObject(resp);
				var bytes = Encoding.UTF8.GetBytes(json);
				ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
			}
		}
		private class ChatRequest{
			public List<Message> Messages;
			public bool Stream;
		}
		private class Message{
			public string Role;
			public string Content;
		}
	}
}