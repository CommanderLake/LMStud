using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using LMStud;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
namespace LM_Stud.Tests{
	[TestClass]
	[DoNotParallelize]
	public class ApiServerSlotRoutingTests{
		[TestInitialize]
		public void TestInitialize(){ModelSlotManager.Load();}
		[TestMethod]
		public async Task HandleChat_WithDifferentSlotModels_RoutesToDifferentApiBackends(){
			var serverPort = GetFreePort();
			var upstreamPort1 = GetFreePort();
			var upstreamPort2 = GetFreePort();
			var slotName1 = UniqueSlotName("test_api_a");
			var slotName2 = UniqueSlotName("test_api_b");
			var upstream1 = StartFakeResponsesServer(upstreamPort1, "from-a", out var upstreamBody1);
			var upstream2 = StartFakeResponsesServer(upstreamPort2, "from-b", out var upstreamBody2);
			var apiServer = new APIServer();
			try{
				ModelSlotManager.AddOrUpdate(new ModelSlot{
					Name = slotName1, Source = ModelSlotSource.Api, ApiBaseUrl = $"http://127.0.0.1:{upstreamPort1}", ApiModel = "upstream-model-a",
					ApiReasoningEffort = 2, ApiReasoningSummary = 3, ApiStore = true, Use = ModelSlotUse.Server
				});
				ModelSlotManager.AddOrUpdate(new ModelSlot{
					Name = slotName2, Source = ModelSlotSource.Api, ApiBaseUrl = $"http://127.0.0.1:{upstreamPort2}", ApiModel = "upstream-model-b",
					ApiReasoningEffort = 4, Use = ModelSlotUse.Server
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
				var upstreamJson1 = JObject.Parse(upstreamBody1.Result);
				var upstreamJson2 = JObject.Parse(upstreamBody2.Result);
				Assert.AreEqual("upstream-model-a", (string)upstreamJson1["model"]);
				Assert.AreEqual("upstream-model-b", (string)upstreamJson2["model"]);
				Assert.AreEqual(true, (bool)upstreamJson1["store"]);
				Assert.AreEqual(false, (bool)upstreamJson2["store"]);
				Assert.AreEqual("low", (string)upstreamJson1["reasoning"]?["effort"]);
				Assert.AreEqual("detailed", (string)upstreamJson1["reasoning"]?["summary"]);
				Assert.AreEqual("high", (string)upstreamJson2["reasoning"]?["effort"]);
				Assert.IsNull(upstreamJson2["reasoning"]?["summary"]);
			} finally{
				apiServer.Stop();
				ModelSlotManager.Remove(slotName1);
				ModelSlotManager.Remove(slotName2);
			}
		}
		[TestMethod]
		public async Task HandleModels_ReturnsConfiguredServerSlots(){
			var serverPort = GetFreePort();
			var slotName = UniqueSlotName("test_list");
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
		[TestMethod]
		public async Task HandleChat_DoesNotRouteToSlotsWithoutServerUse(){
			var serverPort = GetFreePort();
			var slotName = UniqueSlotName("test_hidden_server");
			var apiServer = new APIServer();
			try{
				ModelSlotManager.AddOrUpdate(new ModelSlot{
					Name = slotName, Source = ModelSlotSource.Api, ApiBaseUrl = "http://127.0.0.1:1", ApiModel = "hidden-model", Use = ModelSlotUse.Tool
				});
				Common.APIServerPort = serverPort;
				apiServer.Start();
				using(var client = new HttpClient()){
					var response = await PostResponses(client, serverPort, "lmstud/" + slotName, "hello hidden");
					Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode, await response.Content.ReadAsStringAsync());
				}
			} finally{
				apiServer.Stop();
				ModelSlotManager.Remove(slotName);
			}
		}
		[TestMethod]
		public void ResolveServerSlot_WithNullModel_PrefersServerSlotOverChatSlot(){
			var activeChatSlot = ModelSlotManager.GetActiveChatSlot();
			var serverSlotName = UniqueSlotName("test_default_server");
			try{
				ModelSlotManager.AddOrUpdate(new ModelSlot{
					Name = serverSlotName, Source = ModelSlotSource.Api, ApiBaseUrl = "http://127.0.0.1:1", ApiModel = "server-model", Use = ModelSlotUse.Server
				});
				var slot = ModelSlotManager.ResolveServerSlot(null);
				Assert.IsNotNull(slot);
				if(activeChatSlot != null && !activeChatSlot.HasUse(ModelSlotUse.Server)) Assert.AreNotEqual(activeChatSlot.Name, slot.Name);
				Assert.IsTrue(slot.HasUse(ModelSlotUse.Server), "Default model resolution should prefer a server-enabled slot over the active chat slot.");
			} finally{
				ModelSlotManager.Remove(serverSlotName);
			}
		}
		[TestMethod]
		public void ResolveServerSlot_WithNullModel_PrefersAvailableServerSlot(){
			var busySlotName = UniqueSlotName("test_default_busy");
			var availableSlotName = UniqueSlotName("test_default_available");
			try{
				ModelSlotManager.AddOrUpdate(new ModelSlot{
					Name = busySlotName, Source = ModelSlotSource.Api, ApiBaseUrl = "http://127.0.0.1:1", ApiModel = "busy-model", Use = ModelSlotUse.Server
				});
				ModelSlotManager.AddOrUpdate(new ModelSlot{
					Name = availableSlotName, Source = ModelSlotSource.Api, ApiBaseUrl = "http://127.0.0.1:1", ApiModel = "available-model", Use = ModelSlotUse.Server
				});
				using(ModelSlotManager.EnterSlot(busySlotName)){
					var slot = ModelSlotManager.ResolveServerSlot(null);
					Assert.IsNotNull(slot);
					Assert.AreNotEqual(busySlotName, slot.Name);
				}
			} finally{
				ModelSlotManager.Remove(availableSlotName);
				ModelSlotManager.Remove(busySlotName);
			}
		}
		[TestMethod]
		public void LoadLocalIntoSlot_DoesNotAddServerUseToExistingNonServerSlot(){
			var slotName = UniqueSlotName("test_local_nonserver");
			try{
				ModelSlotManager.AddOrUpdate(new ModelSlot{
					Name = slotName, Source = ModelSlotSource.Local, LocalPath = "old.gguf", Use = ModelSlotUse.Dialectic
				});
				ModelSlotManager.LoadLocalIntoSlot(slotName, "new.gguf", false);
				var slot = ModelSlotManager.GetSlot(slotName);
				Assert.IsNotNull(slot);
				Assert.IsFalse(slot.HasUse(ModelSlotUse.Server), "Loading a non-server slot should preserve the user's Server checkbox state.");
			} finally{
				ModelSlotManager.Remove(slotName);
			}
		}
		[TestMethod]
		public void TryExecuteToolCall_IgnoresApiSlotsThatAreNotEnabledAsTools(){
			var slotName = UniqueSlotName("test_hidden_tool");
			try{
				ModelSlotManager.AddOrUpdate(new ModelSlot{
					Name = slotName, Source = ModelSlotSource.Api, ApiBaseUrl = "http://127.0.0.1:1", ApiModel = "hidden-model", ToolName = "ask_hidden_model", Use = ModelSlotUse.Server
				});
				var handled = ModelSlotManager.TryExecuteToolCall(new APIClient.ToolCall("call_1", "ask_hidden_model", "{\"prompt\":\"hello\"}"), out var result);
				Assert.IsFalse(handled);
				Assert.IsNull(result);
			} finally{
				ModelSlotManager.Remove(slotName);
			}
		}
		[TestMethod]
		public void SlotEditInvalidatesLoadedLocalSlot_WhenModelPathChanges(){
			var oldPath = Path.Combine(Path.GetTempPath(), "old-model.gguf");
			var newPath = Path.Combine(Path.GetTempPath(), "new-model.gguf");
			var original = new ModelSlot{ Name = "main", Source = ModelSlotSource.Local, LocalPath = oldPath, Use = ModelSlotUse.Chat };
			var updated = new ModelSlot{ Name = "main", Source = ModelSlotSource.Local, LocalPath = newPath, Use = ModelSlotUse.Chat };
			Assert.IsTrue(Form1.SlotEditInvalidatesLoadedLocalSlot(original, updated, LoadedModelItem(oldPath)));
		}
		[TestMethod]
		public void SlotEditInvalidatesLoadedLocalSlot_WhenSlotNameChanges(){
			var modelPath = Path.Combine(Path.GetTempPath(), "loaded-model.gguf");
			var original = new ModelSlot{ Name = "main", Source = ModelSlotSource.Local, LocalPath = modelPath, Use = ModelSlotUse.Chat };
			var updated = new ModelSlot{ Name = "renamed", Source = ModelSlotSource.Local, LocalPath = modelPath, Use = ModelSlotUse.Chat };
			Assert.IsTrue(Form1.SlotEditInvalidatesLoadedLocalSlot(original, updated, LoadedModelItem(modelPath)));
		}
		[TestMethod]
		public void SlotEditInvalidatesLoadedLocalSlot_WhenSourceChanges(){
			var modelPath = Path.Combine(Path.GetTempPath(), "loaded-model.gguf");
			var original = new ModelSlot{ Name = "main", Source = ModelSlotSource.Local, LocalPath = modelPath, Use = ModelSlotUse.Chat };
			var updated = new ModelSlot{ Name = "main", Source = ModelSlotSource.Api, ApiBaseUrl = "http://127.0.0.1:1", ApiModel = "remote-model", Use = ModelSlotUse.Chat };
			Assert.IsTrue(Form1.SlotEditInvalidatesLoadedLocalSlot(original, updated, LoadedModelItem(modelPath)));
		}
		[TestMethod]
		public void SlotEditKeepsLoadedLocalSlot_WhenNameAndModelPathAreUnchanged(){
			var modelPath = Path.Combine(Path.GetTempPath(), "loaded-model.gguf");
			var original = new ModelSlot{ Name = "main", Source = ModelSlotSource.Local, LocalPath = modelPath, Use = ModelSlotUse.Chat };
			var updated = new ModelSlot{ Name = "main", Source = ModelSlotSource.Local, LocalPath = modelPath, Use = ModelSlotUse.Chat | ModelSlotUse.Server };
			Assert.IsFalse(Form1.SlotEditInvalidatesLoadedLocalSlot(original, updated, LoadedModelItem(modelPath)));
		}
		private static ListViewItem LoadedModelItem(string path){
			var item = new ListViewItem(Path.GetFileNameWithoutExtension(path));
			item.SubItems.Add(path);
			return item;
		}
		private static string UniqueSlotName(string prefix){return prefix + "_" + Guid.NewGuid().ToString("N");}
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
