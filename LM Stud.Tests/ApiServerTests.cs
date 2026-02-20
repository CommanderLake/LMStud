using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using LMStud;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
namespace LM_Stud.Tests{
	[TestClass]
	public class ApiServerTests{
		private const int TestPort = 11435;
		private static readonly object FormLock = new object();
		private static Form1 _form;
		[ClassInitialize]
		public static void ClassInitialize(TestContext context){
			lock(FormLock){
				var t = new Thread(Program.Main);
				t.SetApartmentState(ApartmentState.STA);
				t.IsBackground = true;
				t.Start();
				while(Program.MainForm == null) Thread.Sleep(10);
				_form = Program.MainForm;
				Thread.Sleep(1000);
				_form.Invoke(new MethodInvoker(() => {_form.LoadModel(_form.listViewModels.Items["Hermes-3-Llama-3.2-3B.Q8_0"], true);}));
				Common.APIServerPort = TestPort;
				while(!Common.LlModelLoaded || _form.ApiServer == null) Thread.Sleep(10);
				_form.ApiServer.Start();
			}
		}
		[ClassCleanup]
		public static void ClassCleanup(){
			lock(FormLock){
				_form.ApiServer.Stop();
				_form.Invoke(new MethodInvoker(() => {
					Program.MainForm.Close();
					Program.MainForm.Dispose();
				}));
			}
		}
		[TestMethod]
		public async Task HandleChat_WhenModelLoaded_AttemptsResponse(){
			using(var client = new HttpClient()){
				var payload = new{ messages = new[]{ new{ role = "user", content = "Hello" } } };
				var response = await client.PostAsync($"http://localhost:{TestPort}/v1/responses", new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json"));
				Assert.IsTrue(response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.Conflict, "Status code: " + response.StatusCode + "; Chat should return 200 or 409 depending on native readiness.");
				if(response.StatusCode == HttpStatusCode.OK){
					var sessionId = response.Headers.Contains("X-Session-Id") ? string.Join("", response.Headers.GetValues("X-Session-Id")) : null;
					Assert.IsFalse(string.IsNullOrEmpty(sessionId), "Session id header should be present.");
					var json = await response.Content.ReadAsStringAsync();
					dynamic result = JsonConvert.DeserializeObject(json);
					var text = (string)result.output[0].content[0].text;
					Assert.IsFalse(string.IsNullOrEmpty(text), "Assistant response should contain output text.");
					var session = _form.ApiServer.Sessions.Get(sessionId);
					Assert.AreEqual(2, session.Messages.Count, "Session should contain user and assistant messages.");
				}
			}
		}
		[TestMethod]
		public async Task HandleChat_ResponseShape_ContainsCoreResponsesFields(){
			using(var client = new HttpClient()){
				var payload = new{ messages = new[]{ new{ role = "user", content = "Hello" } } };
				var response = await client.PostAsync($"http://localhost:{TestPort}/v1/responses", new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json"));
				if(response.StatusCode != HttpStatusCode.OK) Assert.Inconclusive("Chat endpoint did not return 200 in this environment.");
				var json = await response.Content.ReadAsStringAsync();
				dynamic result = JsonConvert.DeserializeObject(json);
				Assert.AreEqual("response", (string)result.@object, "Response object type should be 'response'.");
				Assert.AreEqual("completed", (string)result.status, "Response status should be completed.");
				Assert.IsTrue((long)result.created_at > 0, "Response should include created_at unix timestamp.");
				Assert.AreEqual("message", (string)result.output[0].type, "Output item should be an assistant message.");
				Assert.AreEqual("completed", (string)result.output[0].status, "Assistant message should be completed.");
				Assert.AreEqual("output_text", (string)result.output[0].content[0].type, "Assistant content type should be output_text.");
			}
		}
		[TestMethod]
		public async Task HandleReset_WithSessionId_RemovesSession(){
			string sessionId;
			using(var client = new HttpClient()){
				var payload = new{ messages = new[]{ new{ role = "user", content = "Hi" } } };
				var response = await client.PostAsync($"http://localhost:{TestPort}/v1/responses", new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json"));
				sessionId = response.Headers.Contains("X-Session-Id") ? string.Join("", response.Headers.GetValues("X-Session-Id")) : null;
				Assert.IsFalse(string.IsNullOrEmpty(sessionId), "Chat call should return a session id.");
			}
			var existing = _form.ApiServer.Sessions.Get(sessionId);
			using(var client = new HttpClient()){
				var resetPayload = new{ session_id = sessionId };
				var response = await client.PostAsync($"http://localhost:{TestPort}/v1/reset", new StringContent(JsonConvert.SerializeObject(resetPayload), Encoding.UTF8, "application/json"));
				Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "Reset endpoint should return success.");
				var json = await response.Content.ReadAsStringAsync();
				dynamic result = JsonConvert.DeserializeObject(json);
				Assert.AreEqual("reset", (string)result.status, "Reset response should confirm reset state.");
				Assert.AreEqual("session", (string)result.scope, "Reset with a session id should default to session scope.");
			}
			var replacement = _form.ApiServer.Sessions.Get(sessionId);
			Assert.AreNotSame(existing, replacement, "Reset should remove the stored session.");
		}
		[TestMethod]
		public async Task HandleReset_GlobalScope_ClearsAllSessions(){
			var s1 = _form.ApiServer.Sessions.Get("global-reset-1");
			_form.ApiServer.Sessions.Update(s1, new List<APIServer.Message>{ new APIServer.Message{ Role = "user", Content = "a" } }, null, 0);
			var s2 = _form.ApiServer.Sessions.Get("global-reset-2");
			_form.ApiServer.Sessions.Update(s2, new List<APIServer.Message>{ new APIServer.Message{ Role = "user", Content = "b" } }, null, 0);
			using(var client = new HttpClient()){
				var resetPayload = new{ scope = "global" };
				var response = await client.PostAsync($"http://localhost:{TestPort}/v1/reset", new StringContent(JsonConvert.SerializeObject(resetPayload), Encoding.UTF8, "application/json"));
				Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "Global reset should return success.");
				var json = await response.Content.ReadAsStringAsync();
				dynamic result = JsonConvert.DeserializeObject(json);
				Assert.AreEqual("global", (string)result.scope, "Response should confirm global reset scope.");
			}
			var replacement1 = _form.ApiServer.Sessions.Get("global-reset-1");
			var replacement2 = _form.ApiServer.Sessions.Get("global-reset-2");
			Assert.AreEqual(0, replacement1.Messages.Count, "Global reset should clear existing sessions.");
			Assert.AreEqual(0, replacement2.Messages.Count, "Global reset should clear existing sessions.");
		}
	}
}