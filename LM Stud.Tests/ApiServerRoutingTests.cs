using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LMStud;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LM_Stud.Tests{
	[TestClass]
	[DoNotParallelize]
	public class ApiServerRoutingTests{
		[TestInitialize]
		public void TestInitialize(){ModelSlotManager.Load();}

		[TestMethod]
		public void RequestsToDifferentConfiguredModels_ReachTheirSelectedBackends(){
			var serverPort = GetFreePort();
			var upstreamPort1 = GetFreePort();
			var upstreamPort2 = GetFreePort();
			var slotName1 = UniqueSlotName("api_a");
			var slotName2 = UniqueSlotName("api_b");
			var upstream1 = StartFakeResponsesServer(upstreamPort1, "from-a", out var upstreamBody1);
			var upstream2 = StartFakeResponsesServer(upstreamPort2, "from-b", out var upstreamBody2);
			var apiServer = new APIServer();
			try{
				ModelSlotManager.AddOrUpdate(new ModelSlot{
					Name = slotName1,
					Source = ModelSlotSource.Api,
					ApiBaseUrl = $"http://127.0.0.1:{upstreamPort1}",
					ApiModel = "upstream-model-a",
					ApiReasoningEffort = 2,
					ApiReasoningSummary = 3,
					ApiStore = true,
					Use = ModelSlotUse.Server
				});
				ModelSlotManager.AddOrUpdate(new ModelSlot{
					Name = slotName2,
					Source = ModelSlotSource.Api,
					ApiBaseUrl = $"http://127.0.0.1:{upstreamPort2}",
					ApiModel = "upstream-model-b",
					ApiReasoningEffort = 4,
					Use = ModelSlotUse.Server
				});
				Common.APIServerPort = serverPort;
				apiServer.Start();

				using(var client = new HttpClient()){
					var response1 = PostResponses(client, serverPort, "lmstud/" + slotName1, "hello a");
					var response2 = PostResponses(client, serverPort, "lmstud/" + slotName2, "hello b");
					Assert.AreEqual(HttpStatusCode.OK, response1.StatusCode, response1.Content.ReadAsStringAsync().GetAwaiter().GetResult());
					Assert.AreEqual(HttpStatusCode.OK, response2.StatusCode, response2.Content.ReadAsStringAsync().GetAwaiter().GetResult());
				}

				Assert.IsTrue(upstream1.Wait(5000), "The first configured backend should receive its request.");
				Assert.IsTrue(upstream2.Wait(5000), "The second configured backend should receive its request.");
				var upstreamJson1 = Json.Parse(upstreamBody1.Result);
				var upstreamJson2 = Json.Parse(upstreamBody2.Result);
				Assert.AreEqual("upstream-model-a", upstreamJson1.GetString("model"));
				Assert.AreEqual("upstream-model-b", upstreamJson2.GetString("model"));
				Assert.AreEqual(true, upstreamJson1.GetBool("store"));
				Assert.AreEqual("low", upstreamJson1["reasoning"].GetString("effort"));
				Assert.AreEqual("detailed", upstreamJson1["reasoning"].GetString("summary"));
				Assert.AreEqual("high", upstreamJson2["reasoning"].GetString("effort"));
			} finally{
				apiServer.Stop();
				ModelSlotManager.Remove(slotName1);
				ModelSlotManager.Remove(slotName2);
			}
		}

		[TestMethod]
		public void ModelsEndpoint_ListsOnlySlotsEnabledForServerUse(){
			var serverPort = GetFreePort();
			var publicSlot = UniqueSlotName("public");
			var privateSlot = UniqueSlotName("private");
			var apiServer = new APIServer();
			try{
				ModelSlotManager.AddOrUpdate(new ModelSlot{
					Name = publicSlot,
					Source = ModelSlotSource.Api,
					ApiBaseUrl = "http://127.0.0.1:1",
					ApiModel = "listed-model",
					Use = ModelSlotUse.Server
				});
				ModelSlotManager.AddOrUpdate(new ModelSlot{
					Name = privateSlot,
					Source = ModelSlotSource.Api,
					ApiBaseUrl = "http://127.0.0.1:1",
					ApiModel = "hidden-model",
					Use = ModelSlotUse.Tool
				});
				Common.APIServerPort = serverPort;
				apiServer.Start();

				using(var client = new HttpClient()){
					var response = client.GetAsync($"http://127.0.0.1:{serverPort}/v1/models").GetAwaiter().GetResult();
					Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
					var data = Json.Parse(response.Content.ReadAsStringAsync().GetAwaiter().GetResult())["data"];
					Assert.IsTrue(data.Any(item => item.GetString("id") == "lmstud/" + publicSlot));
					Assert.IsFalse(data.Any(item => item.GetString("id") == "lmstud/" + privateSlot));
				}
			} finally{
				apiServer.Stop();
				ModelSlotManager.Remove(publicSlot);
				ModelSlotManager.Remove(privateSlot);
			}
		}

		[TestMethod]
		public void RequestingAModelNotEnabledForServerUse_ReturnsNotFound(){
			var serverPort = GetFreePort();
			var slotName = UniqueSlotName("tool_only");
			var apiServer = new APIServer();
			try{
				ModelSlotManager.AddOrUpdate(new ModelSlot{
					Name = slotName,
					Source = ModelSlotSource.Api,
					ApiBaseUrl = "http://127.0.0.1:1",
					ApiModel = "hidden-model",
					Use = ModelSlotUse.Tool
				});
				Common.APIServerPort = serverPort;
				apiServer.Start();

				using(var client = new HttpClient()){
					var response = PostResponses(client, serverPort, "lmstud/" + slotName, "hello");
					Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
				}
			} finally{
				apiServer.Stop();
				ModelSlotManager.Remove(slotName);
			}
		}

		private static string UniqueSlotName(string prefix){return prefix + "_" + Guid.NewGuid().ToString("N");}

		private static HttpResponseMessage PostResponses(HttpClient client, int port, string model, string input){
			var payload = Json.Object(Json.P("model", model), Json.P("input", input));
			return client.PostAsync($"http://127.0.0.1:{port}/v1/responses",
				new StringContent(payload.ToJson(), Encoding.UTF8, "application/json")).GetAwaiter().GetResult();
		}

		private static Task StartFakeResponsesServer(int port, string text, out Task<string> bodyTask){
			var bodySource = new TaskCompletionSource<string>();
			var ready = new ManualResetEventSlim(false);
			bodyTask = bodySource.Task;
			var task = Task.Run(() => {
				try{
					using(var listener = new HttpListener()){
						listener.Prefixes.Add($"http://127.0.0.1:{port}/");
						listener.Start();
						ready.Set();
						var context = listener.GetContext();
						using(var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding))
							bodySource.SetResult(reader.ReadToEnd());
						var response = Encoding.UTF8.GetBytes(
							"{\"id\":\"resp_test\",\"output\":[{\"type\":\"message\",\"role\":\"assistant\",\"content\":[{\"type\":\"output_text\",\"text\":\"" +
							text + "\"}]}]}");
						context.Response.StatusCode = 200;
						context.Response.ContentType = "application/json";
						context.Response.ContentLength64 = response.Length;
						context.Response.OutputStream.Write(response, 0, response.Length);
						context.Response.Close();
					}
				} catch(Exception ex){
					bodySource.TrySetException(ex);
					throw;
				}
			});
			if(!ready.Wait(5000)) throw new InvalidOperationException("Fake upstream did not start.");
			return task;
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
