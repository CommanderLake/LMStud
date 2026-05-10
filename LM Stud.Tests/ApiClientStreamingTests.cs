using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LMStud;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
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
				var history = new JArray{ new JObject{ ["role"] = "user", ["content"] = "hello" } };
				var result = client.CreateChatCompletion(history, 0.5f, 128, null, null, CancellationToken.None, delta => streamed.Append(delta));
				Assert.AreEqual("hello", result.Content, "Streaming deltas should be assembled into the final result.");
				Assert.AreEqual("hello", streamed.ToString(), "Streaming callback should receive each text delta.");
				Assert.IsTrue(server.Wait(1000), "Test server should finish handling the streaming request.");
				var payload = JObject.Parse(requestBody);
				Assert.AreEqual(true, (bool)payload["stream"], "Streaming requests should include stream=true.");
			}
		}
	}
}
