using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using LMStud;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace LM_Stud.Tests{
	[TestClass]
	[DoNotParallelize]
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
retry:			try { _form.Invoke(new MethodInvoker(() => { var h = _form.Handle; })); } catch { Thread.Sleep(10); goto retry; }
				_form.PopulateLock.Wait();
				_form.PopulateLock.Release();
				_form.Invoke(new MethodInvoker(() => {_form.LoadModel(_form.listViewModels.Items["Hermes-3-Llama-3.2-3B.Q8_0"], true);}));
				Common.APIServerPort = TestPort;
				while(!Common.LlModelLoaded || _form.ApiServer == null) Thread.Sleep(10);
				_form.ApiServer.Start();
			}
		}
		[ClassCleanup]
		public static void ClassCleanup(){
			lock(FormLock){
				try{ _form?.ApiServer?.Stop(); } catch(ObjectDisposedException){} catch(InvalidOperationException){}
				try{
					if(_form != null && !_form.IsDisposed && _form.IsHandleCreated)
						_form.Invoke(new MethodInvoker(() => {
							Program.MainForm?.Close();
							Program.MainForm?.Dispose();
							Program.MainForm = null;
						}));
				} catch(ObjectDisposedException){} catch(InvalidOperationException){} catch(NullReferenceException){} finally{
					if(ReferenceEquals(Program.MainForm, _form)) Program.MainForm = null;
					_form = null;
				}
			}
		}
		[TestMethod]
		public void HandleChat_WhenModelLoaded_AttemptsResponse(){
			using(var client = new HttpClient()){
				var response = client.PostAsync($"http://localhost:{TestPort}/v1/responses", JsonContent(Json.Object(Json.P("messages", Json.Array(Json.Object(Json.P("role", "user"), Json.P("content", "Hello"))))))).GetAwaiter().GetResult();
				Assert.IsTrue(response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.Conflict, "Status code: " + response.StatusCode + "; Chat should return 200 or 409 depending on native readiness.");
				if(response.StatusCode == HttpStatusCode.OK){
					var sessionId = response.Headers.Contains("X-Session-Id") ? string.Join("", response.Headers.GetValues("X-Session-Id")) : null;
					Assert.IsFalse(string.IsNullOrEmpty(sessionId), "Session id header should be present.");
					var json = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
					var result = Json.Parse(json);
					if(result.IsObject){
						var text = result["output"][0]["content"][0].GetString("text");
						Assert.IsFalse(string.IsNullOrEmpty(text), "Assistant response should contain output text.");
					}
					var session = _form.ApiServer.Sessions.Get(sessionId);
					Assert.AreEqual(2, session.Messages.Count, "Session should contain user and assistant messages.");
				}
			}
		}
		[TestMethod]
		public void HandleChat_ResponseShape_ContainsCoreResponsesFields(){
			using(var client = new HttpClient()){
				var response = PostRealResponsesUntilOk(client, "Hello");
				Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "Real chat endpoint should return 200 for response shape assertions.");
				var json = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
				var result = Json.Parse(json);
				Assert.AreEqual("response", result.GetString("object"), "Response object type should be 'response'.");
				Assert.AreEqual("completed", result.GetString("status"), "Response status should be completed.");
				Assert.IsTrue(result.GetLong("created_at") > 0, "Response should include created_at unix timestamp.");
				Assert.AreEqual("message", result["output"][0].GetString("type"), "Output item should be an assistant message.");
				Assert.AreEqual("completed", result["output"][0].GetString("status"), "Assistant message should be completed.");
				Assert.AreEqual("output_text", result["output"][0]["content"][0].GetString("type"), "Assistant content type should be output_text.");
			}
		}
		[TestMethod]
		public void HandleReset_WithSessionId_RemovesSession(){
			string sessionId;
			using(var client = new HttpClient()){
				var response = PostRealResponsesUntilOk(client, "Hi");
				Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "Real chat endpoint should return a session-bearing response.");
				sessionId = response.Headers.Contains("X-Session-Id") ? string.Join("", response.Headers.GetValues("X-Session-Id")) : null;
				Assert.IsFalse(string.IsNullOrEmpty(sessionId), "Chat call should return a session id.");
			}
			var existing = _form.ApiServer.Sessions.Get(sessionId);
			using(var client = new HttpClient()){
				var response = client.PostAsync($"http://localhost:{TestPort}/v1/reset", JsonContent(Json.Object(Json.P("session_id", sessionId)))).GetAwaiter().GetResult();
				Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "Reset endpoint should return success.");
				var json = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
				var result = Json.Parse(json);
				Assert.AreEqual("reset", result.GetString("status"), "Reset response should confirm reset state.");
				Assert.AreEqual("session", result.GetString("scope"), "Reset with a session id should default to session scope.");
			}
			var replacement = _form.ApiServer.Sessions.Get(sessionId);
			Assert.AreNotSame(existing, replacement, "Reset should remove the stored session.");
		}
		[TestMethod]
		public void HandleReset_GlobalScope_ClearsAllSessions(){
			var s1 = _form.ApiServer.Sessions.Get("global-reset-1");
			_form.ApiServer.Sessions.Update(s1, new List<APIServer.Message>{ new APIServer.Message{ Role = "user", Content = "a" } }, null, 0);
			var s2 = _form.ApiServer.Sessions.Get("global-reset-2");
			_form.ApiServer.Sessions.Update(s2, new List<APIServer.Message>{ new APIServer.Message{ Role = "user", Content = "b" } }, null, 0);
			using(var client = new HttpClient()){
				var response = client.PostAsync($"http://localhost:{TestPort}/v1/reset", JsonContent(Json.Object(Json.P("scope", "global")))).GetAwaiter().GetResult();
				Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "Global reset should return success.");
				var json = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
				var result = Json.Parse(json);
				Assert.AreEqual("global", result.GetString("scope"), "Response should confirm global reset scope.");
			}
			var replacement1 = _form.ApiServer.Sessions.Get("global-reset-1");
			var replacement2 = _form.ApiServer.Sessions.Get("global-reset-2");
			Assert.AreEqual(0, replacement1.Messages.Count, "Global reset should clear existing sessions.");
			Assert.AreEqual(0, replacement2.Messages.Count, "Global reset should clear existing sessions.");
		}
		private static StringContent JsonContent(JsonNode payload){ return new StringContent(payload.ToJson(), Encoding.UTF8, "application/json"); }
		private static HttpResponseMessage PostRealResponsesUntilOk(HttpClient client, string content){
			for(var attempt = 0; attempt < 10; ++attempt){
				var response = client.PostAsync($"http://localhost:{TestPort}/v1/responses",
					JsonContent(Json.Object(Json.P("messages", Json.Array(Json.Object(Json.P("role", "user"), Json.P("content", content))))))).GetAwaiter().GetResult();
				if(response.StatusCode == HttpStatusCode.OK || response.StatusCode != HttpStatusCode.Conflict || attempt == 9) return response;
				response.Dispose();
				Thread.Sleep(250);
			}
			throw new InvalidOperationException("Unreachable retry state.");
		}
	}
}
