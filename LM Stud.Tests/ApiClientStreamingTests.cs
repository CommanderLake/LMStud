using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LMStud;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace LM_Stud.Tests{
	[TestClass]
	public class ApiClientStreamingTests{
		[TestMethod]
		public void CreateChatCompletion_WithStreamCallback_ParsesResponsesSse(){
			using(var listener = new HttpListener()){
				var baseUrl = "http://127.0.0.1:39594/";
				string requestBody = null;
				listener.Prefixes.Add(baseUrl);
				listener.Start();
				var server = Task.Run(() => {
					var ctx = listener.GetContext();
					using(var reader = new StreamReader(ctx.Request.InputStream, ctx.Request.ContentEncoding)){ requestBody = reader.ReadToEnd(); }
					ctx.Response.StatusCode = 200;
					ctx.Response.ContentType = "text/event-stream";
					using(var writer = new StreamWriter(ctx.Response.OutputStream, new UTF8Encoding(false), 1024, true)){
						writer.WriteLine("event: response.output_text.delta");
						writer.WriteLine("data: {\"type\":\"response.output_text.delta\",\"delta\":\"hel\"}");
						writer.WriteLine();
						writer.Flush();
						writer.WriteLine("event: response.output_text.delta");
						writer.WriteLine("data: {\"type\":\"response.output_text.delta\",\"delta\":\"lo\"}");
						writer.WriteLine();
						writer.Flush();
						writer.WriteLine("data: [DONE]");
						writer.WriteLine();
					}
					ctx.Response.Close();
				});
				var streamed = new StringBuilder();
				var client = new APIClient(baseUrl, "", "test-model", false);
				var history = Json.ArrayBuilder();
				history.Add(Json.Object(Json.P("role", "user"), Json.P("content", "hello")));
				var result = client.CreateChatCompletion(history, 0.5f, 128, null, null, CancellationToken.None, delta => streamed.Append(delta));
				Assert.AreEqual("hello", result.Content, "Streaming deltas should be assembled into the final result.");
				Assert.AreEqual("hello", streamed.ToString(), "Streaming callback should receive each text delta.");
				Assert.IsTrue(server.Wait(1000), "Test server should finish handling the streaming request.");
				var payload = Json.Parse(requestBody);
				Assert.AreEqual(true, payload.GetBool("stream"), "Streaming requests should include stream=true.");
			}
		}
		[TestMethod]
		public void CreateChatCompletion_WithReasoningOnlyStream_ReturnsReasoning(){
			using(var listener = new HttpListener()){
				var baseUrl = "http://127.0.0.1:39595/";
				listener.Prefixes.Add(baseUrl);
				listener.Start();
				var server = Task.Run(() => {
					var ctx = listener.GetContext();
					ctx.Response.StatusCode = 200;
					ctx.Response.ContentType = "text/event-stream";
					using(var writer = new StreamWriter(ctx.Response.OutputStream, new UTF8Encoding(false), 1024, true)){
						writer.WriteLine("event: response.reasoning_text.delta");
						writer.WriteLine("data: {\"type\":\"response.reasoning_text.delta\",\"delta\":\"thinking\"}");
						writer.WriteLine();
						writer.Flush();
						writer.WriteLine("data: [DONE]");
						writer.WriteLine();
					}
					ctx.Response.Close();
				});
				var client = new APIClient(baseUrl, "", "test-model", false);
				var history = Json.ArrayBuilder();
				history.Add(Json.Object(Json.P("role", "user"), Json.P("content", "hello")));
				var result = client.CreateChatCompletion(history, 0.5f, 128, null, null, CancellationToken.None, delta => {});
				Assert.AreEqual("", result.Content, "Reasoning-only streams should not manufacture answer text.");
				Assert.AreEqual("thinking", result.Reasoning, "Reasoning deltas should be preserved even without output text.");
				Assert.IsTrue(server.Wait(1000), "Test server should finish handling the reasoning stream.");
			}
		}
	}
}
