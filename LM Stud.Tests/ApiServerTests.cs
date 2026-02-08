using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using LMStud;
using LMStud.Properties;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
namespace LM_Stud.Tests{
	[TestClass]
	public class ApiServerTests{
		private const int TestPort = 11435;
		private static readonly object FormLock = new object();
		private ApiServer _apiServer;
		private Form1 _form;
		private string _originalLastModel;
		[ClassInitialize]
		public static void ClassInitialize(TestContext context){
			lock(FormLock){
				var t = new Thread(Program.Main);
				t.SetApartmentState(ApartmentState.STA);
				t.IsBackground = true;
				t.Start();
				while(Program.MainForm == null) Thread.Sleep(10);
				Program.MainForm.Populating = true;
			}
		}
		[ClassCleanup]
		public static void ClassCleanup(){
			lock(FormLock){
				Program.MainForm.Close();
				Program.MainForm.Dispose();
			}
		}
		[TestInitialize]
		public void TestInitialize(){
			_form = Program.MainForm;
			_form.Invoke(new MethodInvoker(() => {
				_form.LlModelLoaded = true;
				SetModelList(_form, "test-model-1", "test-model-2");
			}));
			_originalLastModel = Settings.Default.LastModel;
			Settings.Default.LastModel = "test-model-1.gguf";
			_apiServer = new ApiServer(_form){ Port = TestPort };
		}
		[TestCleanup]
		public void TestCleanup(){
			Settings.Default.LastModel = _originalLastModel;
			_apiServer?.Stop();
		}
		[TestMethod]
		public void Start_WhenNotRunning_StartsServer(){
			Assert.IsFalse(_apiServer.IsRunning, "Server should not be running initially.");
			_apiServer.Start();
			Assert.IsTrue(SpinWait.SpinUntil(() => _apiServer.IsRunning, TimeSpan.FromSeconds(1)), "Server should start listening.");
		}
		[TestMethod]
		public void Start_WhenAlreadyRunning_DoesNothing(){
			_apiServer.Start();
			Assert.IsTrue(SpinWait.SpinUntil(() => _apiServer.IsRunning, TimeSpan.FromSeconds(1)));
			_apiServer.Start();
			Assert.IsTrue(_apiServer.IsRunning, "Server should remain running after redundant start.");
		}
		[TestMethod]
		public void Stop_WhenRunning_StopsServer(){
			_apiServer.Start();
			Assert.IsTrue(SpinWait.SpinUntil(() => _apiServer.IsRunning, TimeSpan.FromSeconds(1)));
			_apiServer.Stop();
			Assert.IsFalse(_apiServer.IsRunning, "Server should be stopped after Stop().");
		}
		[TestMethod]
		public void Stop_WhenNotRunning_DoesNothing(){
			Assert.IsFalse(_apiServer.IsRunning);
			_apiServer.Stop();
			Assert.IsFalse(_apiServer.IsRunning, "Stop should be safe when server was not running.");
		}
		[TestMethod]
		public async Task HandleModels_ReturnsModelList(){
			_apiServer.Start();
			await WaitForServerAsync();
			using(var client = new HttpClient()){
				var response = await client.GetAsync($"http://localhost:{TestPort}/v1/models");
				Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "Models endpoint should return success.");
				var json = await response.Content.ReadAsStringAsync();
				dynamic result = JsonConvert.DeserializeObject(json);
				Assert.AreEqual(2, (int)result.data.Count, "Should return two models from the configured list.");
				Assert.AreEqual("test-model-1", (string)result.data[0].id, "First model id should match configured list.");
			}
		}
		[TestMethod]
		public async Task HandleModel_ReturnsCurrentModel(){
			_apiServer.Start();
			await WaitForServerAsync();
			using(var client = new HttpClient()){
				var response = await client.GetAsync($"http://localhost:{TestPort}/v1/model");
				Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "Model endpoint should return success.");
				var json = await response.Content.ReadAsStringAsync();
				dynamic result = JsonConvert.DeserializeObject(json);
				Assert.AreEqual("test-model-1", (string)result.model, "Model name should come from settings.");
			}
		}
		[TestMethod]
		public async Task HandleChat_WhenModelNotLoaded_Returns409(){
			_form.Invoke(new MethodInvoker(() => { _form.LlModelLoaded = false; }));
			_apiServer.Start();
			await WaitForServerAsync();
			using(var client = new HttpClient()){
				var payload = new{ messages = new[]{ new{ role = "user", content = "hello" } } };
				var response = await client.PostAsync($"http://localhost:{TestPort}/v1/responses", new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json"));
				Assert.AreEqual(HttpStatusCode.Conflict, response.StatusCode, "Chat endpoint should return 409 when model is unavailable.");
			}
		}
		[TestMethod]
		public async Task HandleChat_WhenModelLoaded_AttemptsResponse(){
			_apiServer.Start();
			await WaitForServerAsync();
			using(var client = new HttpClient()){
				var payload = new{ messages = new[]{ new{ role = "user", content = "Hello" } } };
				var response = await client.PostAsync($"http://localhost:{TestPort}/v1/responses", new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json"));
				Assert.IsTrue(response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.Conflict, "Chat should return 200 or 409 depending on native readiness.");
				if(response.StatusCode == HttpStatusCode.OK){
					var sessionId = response.Headers.Contains("X-Session-Id") ? string.Join("", response.Headers.GetValues("X-Session-Id")) : null;
					Assert.IsFalse(string.IsNullOrEmpty(sessionId), "Session id header should be present.");
					var json = await response.Content.ReadAsStringAsync();
					dynamic result = JsonConvert.DeserializeObject(json);
					var text = (string)result.output[0].content[0].text;
					Assert.IsFalse(string.IsNullOrEmpty(text), "Assistant response should contain output text.");
					var sessionManager = GetSessionManager();
					var session = sessionManager.Get(sessionId);
					Assert.AreEqual(2, session.Messages.Count, "Session should contain user and assistant messages.");
				}
			}
		}
		[TestMethod]
		public async Task HandleReset_WithSessionId_RemovesSession(){
			_apiServer.Start();
			await WaitForServerAsync();
			string sessionId;
			using(var client = new HttpClient()){
				var payload = new{ messages = new[]{ new{ role = "user", content = "Hi" } } };
				var response = await client.PostAsync($"http://localhost:{TestPort}/v1/responses", new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json"));
				sessionId = response.Headers.Contains("X-Session-Id") ? string.Join("", response.Headers.GetValues("X-Session-Id")) : null;
				Assert.IsFalse(string.IsNullOrEmpty(sessionId), "Chat call should return a session id.");
			}
			var sessionManager = GetSessionManager();
			var existing = sessionManager.Get(sessionId);
			using(var client = new HttpClient()){
				var resetPayload = new{ session_id = sessionId };
				var response = await client.PostAsync($"http://localhost:{TestPort}/v1/reset", new StringContent(JsonConvert.SerializeObject(resetPayload), Encoding.UTF8, "application/json"));
				Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "Reset endpoint should return success.");
				var json = await response.Content.ReadAsStringAsync();
				dynamic result = JsonConvert.DeserializeObject(json);
				Assert.AreEqual("reset", (string)result.status, "Reset response should confirm reset state.");
			}
			var replacement = sessionManager.Get(sessionId);
			Assert.AreNotSame(existing, replacement, "Reset should remove the stored session.");
		}
		[TestMethod]
		public async Task InvalidEndpoint_Returns404(){
			_apiServer.Start();
			await WaitForServerAsync();
			using(var client = new HttpClient()){
				var response = await client.GetAsync($"http://localhost:{TestPort}/invalid/endpoint");
				Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode, "Unknown endpoints should return 404.");
			}
		}
		private static async Task WaitForServerAsync(){
			using(var client = new HttpClient()){
				var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(2);
				while(DateTime.UtcNow < deadline)
					try{
						await client.GetAsync($"http://localhost:{TestPort}/health-check");
						return;
					} catch(Exception){ await Task.Delay(20); }
			}
		}
		private static void SetField(object instance, string name, object value){
			var field = instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
			if(field == null) throw new InvalidOperationException($"Field '{name}' not found on type '{instance.GetType().FullName}'.");
			field.SetValue(instance, value);
		}
		private static void SetModelList(Form1 form, params string[] models){
			var field = typeof(Form1).GetField("_models", BindingFlags.Instance | BindingFlags.NonPublic);
			var list = (IList)field.GetValue(form);
			list.Clear();
			var modelInfoType = typeof(Form1).GetNestedType("ModelInfo", BindingFlags.NonPublic);
			foreach(var model in models){
				var meta = new List<GGUFMetadataManager.GGUFMetadataEntry>();
				var instance = Activator.CreateInstance(modelInfoType, model + ".gguf", meta);
				list.Add(instance);
			}
		}
		private SessionManager GetSessionManager(){
			var field = typeof(ApiServer).GetField("_sessions", BindingFlags.Instance | BindingFlags.NonPublic);
			return (SessionManager)field.GetValue(_apiServer);
		}
	}
}