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
namespace LM_Stud.Tests{
	[TestClass]
	[DoNotParallelize]
	public class ApiServerSlotRoutingTests{
		[TestInitialize]
		public void TestInitialize(){ModelSlotManager.Load();}
		[TestMethod]
		public void HandleChat_WithDifferentSlotModels_RoutesToDifferentApiBackends(){
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
					var response1 = PostResponses(client, serverPort, "lmstud/" + slotName1, "hello a");
					var response2 = PostResponses(client, serverPort, "lmstud/" + slotName2, "hello b");
					var body1 = response1.Content.ReadAsStringAsync().GetAwaiter().GetResult();
					var body2 = response2.Content.ReadAsStringAsync().GetAwaiter().GetResult();
					Assert.AreEqual(HttpStatusCode.OK, response1.StatusCode, body1);
					Assert.AreEqual(HttpStatusCode.OK, response2.StatusCode, body2);
					var json1 = Json.Parse(body1);
					var json2 = Json.Parse(body2);
					Assert.AreEqual("lmstud/" + slotName1, json1.GetString("model"));
					Assert.AreEqual("lmstud/" + slotName2, json2.GetString("model"));
				}
				Assert.IsTrue(upstream1.Wait(5000), "First fake upstream should receive a request.");
				Assert.IsTrue(upstream2.Wait(5000), "Second fake upstream should receive a request.");
				var upstreamJson1 = Json.Parse(upstreamBody1.Result);
				var upstreamJson2 = Json.Parse(upstreamBody2.Result);
				Assert.AreEqual("upstream-model-a", upstreamJson1.GetString("model"));
				Assert.AreEqual("upstream-model-b", upstreamJson2.GetString("model"));
				Assert.AreEqual(true, upstreamJson1.GetBool("store"));
				Assert.AreEqual(false, upstreamJson2.GetBool("store"));
				Assert.AreEqual("low", upstreamJson1["reasoning"].GetString("effort"));
				Assert.AreEqual("detailed", upstreamJson1["reasoning"].GetString("summary"));
				Assert.AreEqual("high", upstreamJson2["reasoning"].GetString("effort"));
				Assert.IsFalse(upstreamJson2["reasoning"]["summary"].Exists);
			} finally{
				apiServer.Stop();
				ModelSlotManager.Remove(slotName1);
				ModelSlotManager.Remove(slotName2);
			}
		}
		[TestMethod]
		public void HandleModels_ReturnsConfiguredServerSlots(){
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
					var response = client.GetAsync($"http://127.0.0.1:{serverPort}/v1/models").GetAwaiter().GetResult();
					Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
					var json = Json.Parse(response.Content.ReadAsStringAsync().GetAwaiter().GetResult());
					var ids = json["data"];
					Assert.IsTrue(ids.IsArray && ids.Any(item => item.GetString("id") == "lmstud/" + slotName), "Configured server slot should be listed by /v1/models.");
				}
			} finally{
				apiServer.Stop();
				ModelSlotManager.Remove(slotName);
			}
		}
		[TestMethod]
		public void HandleChat_DoesNotRouteToSlotsWithoutServerUse(){
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
					var response = PostResponses(client, serverPort, "lmstud/" + slotName, "hello hidden");
					Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode, response.Content.ReadAsStringAsync().GetAwaiter().GetResult());
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
		public void ModelSlotInstructions_InheritGlobalUnlessSystemPromptOverrideIsEnabled(){
			var oldPrompt = Common.SystemPrompt;
			try{
				Common.SystemPrompt = "global prompt";
				var slot = new ModelSlot{ Instructions = "slot prompt" };
				Assert.AreEqual("global prompt", slot.GetInstructionsOrDefault());
				slot.OverrideSystemPrompt = true;
				Assert.AreEqual("slot prompt", slot.GetInstructionsOrDefault());
				slot.Instructions = "";
				Assert.AreEqual("", slot.GetInstructionsOrDefault(), "An enabled override may intentionally provide an empty prompt.");
			} finally{
				Common.SystemPrompt = oldPrompt;
			}
		}
		[TestMethod]
		public void AddOrUpdate_MigratesLegacyNonEmptySlotPromptToOverride(){
			var oldPrompt = Common.SystemPrompt;
			var slotName = UniqueSlotName("test_legacy_prompt");
			try{
				Common.SystemPrompt = "global prompt";
				ModelSlotManager.AddOrUpdate(new ModelSlot{
					Name = slotName, Source = ModelSlotSource.Api, ApiBaseUrl = "http://127.0.0.1:1", ApiModel = "legacy-model",
					Instructions = "legacy prompt", Use = ModelSlotUse.Server
				});
				var slot = ModelSlotManager.GetSlot(slotName);
				Assert.IsTrue(slot.OverrideSystemPrompt);
				Assert.AreEqual("legacy prompt", slot.GetInstructionsOrDefault());
			} finally{
				ModelSlotManager.Remove(slotName);
				Common.SystemPrompt = oldPrompt;
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
		public void BuildModelCallTools_ExcludesCallerSlot(){
			var slotName = UniqueSlotName("test_caller_tool");
			try{
				ModelSlotManager.AddOrUpdate(new ModelSlot{
					Name = slotName, Source = ModelSlotSource.Api, ApiBaseUrl = "http://127.0.0.1:1", ApiModel = "caller-model", ToolName = "ask_caller_model", Use = ModelSlotUse.Tool
				});
				var tools = ModelSlotManager.BuildModelCallTools(slotName);
				Assert.IsFalse(tools.Where(tool => tool.IsObject).Any(tool => tool["function"].GetString("name") == "ask_caller_model"), "A slot should not expose itself as a model-call tool.");
			} finally{
				ModelSlotManager.Remove(slotName);
			}
		}
		[TestMethod]
		public void TryExecuteToolCall_ReturnsErrorForSelfCall(){
			var slotName = UniqueSlotName("test_self_tool");
			try{
				ModelSlotManager.AddOrUpdate(new ModelSlot{
					Name = slotName, Source = ModelSlotSource.Api, ApiBaseUrl = "http://127.0.0.1:1", ApiModel = "self-model", ToolName = "ask_self_model", Use = ModelSlotUse.Tool
				});
				var handled = ModelSlotManager.TryExecuteToolCall(new APIClient.ToolCall("call_1", "ask_self_model", "{\"prompt\":\"hello\"}"), slotName, out var result);
				Assert.IsTrue(handled);
				StringAssert.Contains(result, "cannot call itself");
			} finally{
				ModelSlotManager.Remove(slotName);
			}
		}
		[TestMethod]
		public void TryExecuteToolCall_ReturnsErrorForColdLocalToolSlot(){
			var slotName = UniqueSlotName("test_cold_local_tool");
			try{
				ModelSlotManager.AddOrUpdate(new ModelSlot{
					Name = slotName, Source = ModelSlotSource.Local, LocalPath = "cold.gguf", ToolName = "ask_cold_local", Use = ModelSlotUse.Tool
				});
				var handled = ModelSlotManager.TryExecuteToolCall(new APIClient.ToolCall("call_1", "ask_cold_local", "{\"prompt\":\"hello\"}"), "caller", out var result);
				Assert.IsTrue(handled);
				StringAssert.Contains(result, "not loaded");
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
		private static HttpResponseMessage PostResponses(HttpClient client, int port, string model, string input){
			var payload = Json.Object(Json.P("model", model), Json.P("input", input));
			return client.PostAsync($"http://127.0.0.1:{port}/v1/responses", new StringContent(payload.ToJson(), Encoding.UTF8, "application/json")).GetAwaiter().GetResult();
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
