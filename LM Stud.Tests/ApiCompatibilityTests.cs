using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LMStud;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LM_Stud.Tests{
	[TestClass]
	public class ApiCompatibilityTests{
		[TestMethod]
		public void ChatRequest_FallsBackWhenResponsesEndpointIsUnavailable(){
			var port = GetFreePort();
			using(var listener = new HttpListener()){
				var baseUrl = $"http://127.0.0.1:{port}/";
				listener.Prefixes.Add(baseUrl);
				listener.Start();
				var server = Task.Run(() => {
					var responsesRequest = listener.GetContext();
					Assert.AreEqual("/v1/responses", responsesRequest.Request.Url.AbsolutePath);
					responsesRequest.Response.StatusCode = 404;
					responsesRequest.Response.Close();

					var chatRequest = listener.GetContext();
					Assert.AreEqual("/v1/chat/completions", chatRequest.Request.Url.AbsolutePath);
					chatRequest.Response.StatusCode = 200;
					chatRequest.Response.ContentType = "application/json";
					using(var writer = new StreamWriter(chatRequest.Response.OutputStream, new UTF8Encoding(false), 1024, true))
						writer.Write("{\"id\":\"chatcmpl_test\",\"choices\":[{\"message\":{\"role\":\"assistant\",\"content\":\"fallback ok\"}}]}");
					chatRequest.Response.Close();
				});

				var history = Json.ArrayBuilder();
				history.Add(Json.Object(Json.P("role", "user"), Json.P("content", "hello")));
				var client = new APIClient(baseUrl, "", "test-model", false);
				var result = client.CreateChatCompletion(history, 0.5f, 128, "[]", null, CancellationToken.None);

				Assert.AreEqual("fallback ok", result.Content);
				Assert.IsTrue(server.Wait(5000), "The compatibility fallback should complete both requests.");
			}
		}

		private static int GetFreePort(){
			var listener = new TcpListener(IPAddress.Loopback, 0);
			listener.Start();
			var port = ((IPEndPoint)listener.LocalEndpoint).Port;
			listener.Stop();
			return port;
		}
	}
}
