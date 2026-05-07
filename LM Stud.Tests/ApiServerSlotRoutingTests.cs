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
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
namespace LM_Stud.Tests{
	[TestClass]
	public class ApiServerSlotRoutingTests{
		[TestMethod]
		public async Task HandleChat_WithDifferentSlotModels_RoutesToDifferentApiBackends(){
			var serverPort = GetFreePort();
			var upstreamPort1 = GetFreePort();
			var upstreamPort2 = GetFreePort();
			var slotName1 = "test_api_a_" + Guid.NewGuid().ToString("N").Substring(0, 8);
			var slotName2 = "test_api_b_" + Guid.NewGuid().ToString("N").Substring(0, 8);
			var upstream1 = StartFakeResponsesServer(upstreamPort1, "from-a", out var upstreamBody1);
			var upstream2 = StartFakeResponsesServer(upstreamPort2, "from-b", out var upstreamBody2);
			var apiServer = new APIServer();
			try{
				ModelSlotManager.AddOrUpdate(new ModelSlot{
					Name = slotName1, Source = ModelSlotSource.Api, ApiBaseUrl = $"http://127.0.0.1:{upstreamPort1}", ApiModel = "upstream-model-a", Use = ModelSlotUse.Server
				});
				ModelSlotManager.AddOrUpdate(new ModelSlot{
					Name = slotName2, Source = ModelSlotSource.Api, ApiBaseUrl = $"http://127.0.0.1:{upstreamPort2}", ApiModel = "upstream-model-b", Use = ModelSlotUse.Server
				});
				Common.APIServerPort = serverPort;
				apiServer.Start();
				using(var client = new HttpClient()){
					var response1 = await PostResponses(client, serverPort, "lmstud/" + slotName1, "hello a");
					var response2 = await PostResponses(client, serverPort, "lmstud/" + slotName2, "hello b");
					Assert.AreEqual(HttpStatusCode.OK, response1.StatusCode, await response1.Content.ReadAsStringAsync());
					Assert.AreEqual(HttpStatusCode.OK, response2.StatusCode, await response2.Content.ReadAsStringAsync());
					var json1 = JObject.Parse(await response1.Content.ReadAsStringAsync());
					var json2 = JObject.Parse(await response2.Content.ReadAsStringAsync());
					Assert.AreEqual("lmstud/" + slotName1, (string)json1["model"]);
					Assert.AreEqual("lmstud/" + slotName2, (string)json2["model"]);
				}
				Assert.IsTrue(upstream1.Wait(5000), "First fake upstream should receive a request.");
				Assert.IsTrue(upstream2.Wait(5000), "Second fake upstream should receive a request.");
				Assert.AreEqual("upstream-model-a", (string)JObject.Parse(upstreamBody1.Result)["model"]);
				Assert.AreEqual("upstream-model-b", (string)JObject.Parse(upstreamBody2.Result)["model"]);
			} finally{
				apiServer.Stop();
				ModelSlotManager.Remove(slotName1);
				ModelSlotManager.Remove(slotName2);
			}
		}
		[TestMethod]
		public async Task HandleModels_ReturnsConfiguredServerSlots(){
			var serverPort = GetFreePort();
			var slotName = "test_list_" + Guid.NewGuid().ToString("N").Substring(0, 8);
			var apiServer = new APIServer();
			try{
				ModelSlotManager.AddOrUpdate(new ModelSlot{
					Name = slotName, Source = ModelSlotSource.Api, ApiBaseUrl = "http://127.0.0.1:1", ApiModel = "listed-model", Use = ModelSlotUse.Server
				});
				Common.APIServerPort = serverPort;
				apiServer.Start();
				using(var client = new HttpClient()){
					var response = await client.GetAsync($"http://127.0.0.1:{serverPort}/v1/models");
					Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
					var json = JObject.Parse(await response.Content.ReadAsStringAsync());
					var ids = json["data"] as JArray;
					Assert.IsTrue(ids != null && ids.Any(item => (string)item["id"] == "lmstud/" + slotName), "Configured server slot should be listed by /v1/models.");
				}
			} finally{
				apiServer.Stop();
				ModelSlotManager.Remove(slotName);
			}
		}
		private static async Task<HttpResponseMessage> PostResponses(HttpClient client, int port, string model, string input){
			var payload = new{ model = model, input = input };
			return await client.PostAsync($"http://127.0.0.1:{port}/v1/responses", new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json"));
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
						var ctx = listener.GetContext();
						using(var reader = new StreamReader(ctx.Request.InputStream, ctx.Request.ContentEncoding)) bodySource.SetResult(reader.ReadToEnd());
						ctx.Response.StatusCode = 200;
						ctx.Response.ContentType = "application/json";
						ctx.Response.KeepAlive = false;
						var response = "{\"id\":\"resp_test\",\"output\":[{\"type\":\"message\",\"role\":\"assistant\",\"content\":[{\"type\":\"output_text\",\"text\":\"" + text + "\"}]}]}";
						var buffer = Encoding.UTF8.GetBytes(response);
						ctx.Response.ContentLength64 = buffer.Length;
						ctx.Response.OutputStream.Write(buffer, 0, buffer.Length);
						ctx.Response.OutputStream.Close();
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
