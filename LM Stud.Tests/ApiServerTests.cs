using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using LMStud;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
namespace LM_Stud.Tests{
	[TestClass]
	public class ApiServerTests{
		private const int TestPort = 11435;// Different port to avoid conflicts
		private ApiServer _apiServer;
		private Form1 _mockForm;
		[TestInitialize]
		public void TestInitialize(){
			_mockForm = new MockForm1();
			_apiServer = new ApiServer(_mockForm){ Port = TestPort };
		}
		[TestCleanup]
		public void TestCleanup(){_apiServer?.Stop();}
		[TestMethod]
		public void Start_WhenNotRunning_StartsServer(){
			Assert.IsFalse(_apiServer.IsRunning, "Server should not be running initially.");
			_apiServer.Start();
			Thread.Sleep(100);// Allow time for server to start
			Assert.IsTrue(_apiServer.IsRunning, "Server should be running after Start().");
		}
		[TestMethod]
		public void Start_WhenAlreadyRunning_DoesNothing(){
			_apiServer.Start();
			Thread.Sleep(100);
			Assert.IsTrue(_apiServer.IsRunning);
			_apiServer.Start();// Should not throw
			Assert.IsTrue(_apiServer.IsRunning, "Server should still be running.");
		}
		[TestMethod]
		public void Stop_WhenRunning_StopsServer(){
			_apiServer.Start();
			Thread.Sleep(100);
			Assert.IsTrue(_apiServer.IsRunning);
			_apiServer.Stop();
			Assert.IsFalse(_apiServer.IsRunning, "Server should not be running after Stop().");
		}
		[TestMethod]
		public void Stop_WhenNotRunning_DoesNothing(){
			Assert.IsFalse(_apiServer.IsRunning);
			_apiServer.Stop();// Should not throw
			Assert.IsFalse(_apiServer.IsRunning, "Server should still not be running.");
		}
		[TestMethod]
		public async Task HandleModels_ReturnsModelList(){
			_mockForm.LlModelLoaded = true;
			_apiServer.Start();
			await Task.Delay(100);
			using(var client = new WebClient()){
				var response = client.DownloadString($"http://localhost:{TestPort}/v1/models");
				dynamic result = JsonConvert.DeserializeObject(response);
				Assert.IsNotNull(result.data, "Response should contain data array.");
				Assert.IsTrue(result.data.Count > 0, "Should return at least one model.");
			}
		}
		[TestMethod]
		public async Task HandleModel_ReturnsCurrentModel(){
			_mockForm.LlModelLoaded = true;
			_apiServer.Start();
			await Task.Delay(100);
			using(var client = new WebClient()){
				var response = client.DownloadString($"http://localhost:{TestPort}/v1/model");
				dynamic result = JsonConvert.DeserializeObject(response);
				Assert.IsNotNull(result.model, "Response should contain model name.");
			}
		}
		[TestMethod]
		public async Task HandleChat_WhenModelNotLoaded_Returns409(){
			_mockForm.LlModelLoaded = false;
			_apiServer.Start();
			await Task.Delay(100);
			using(var client = new WebClient()){
				try{
					client.Headers[HttpRequestHeader.ContentType] = "application/json";
					var data = JsonConvert.SerializeObject(new{ messages = new[]{ new{ role = "user", content = "test" } } });
					client.UploadString($"http://localhost:{TestPort}/v1/chat/completions", data);
					Assert.Fail("Should have thrown WebException with 409 status.");
				} catch(WebException ex){
					var response = ex.Response as HttpWebResponse;
					Assert.AreEqual(HttpStatusCode.Conflict, response?.StatusCode, "Should return 409 when model not loaded.");
				}
			}
		}
		[TestMethod]
		public async Task HandleReset_WithSessionId_ResetsSession(){
			_mockForm.LlModelLoaded = true;
			_apiServer.Start();
			await Task.Delay(100);
			using(var client = new WebClient()){
				client.Headers[HttpRequestHeader.ContentType] = "application/json";
				var data = JsonConvert.SerializeObject(new{ sessionId = "test-session" });
				var response = client.UploadString($"http://localhost:{TestPort}/v1/chat/reset", data);
				Assert.IsNotNull(response, "Reset should return a response.");
			}
		}
		[TestMethod]
		public async Task InvalidEndpoint_Returns404(){
			_mockForm.LlModelLoaded = true;
			_apiServer.Start();
			await Task.Delay(100);
			using(var client = new WebClient()){
				try{
					client.DownloadString($"http://localhost:{TestPort}/invalid/endpoint");
					Assert.Fail("Should have thrown WebException with 404 status.");
				} catch(WebException ex){
					var response = ex.Response as HttpWebResponse;
					Assert.AreEqual(HttpStatusCode.NotFound, response?.StatusCode, "Should return 404 for invalid endpoint.");
				}
			}
		}
		private class MockForm1 : Form1{
			public MockForm1(){GenerationLock = new SemaphoreSlim(1, 1);}
			public new bool LlModelLoaded{get; set;} = true;
			public List<string> GetModelNames(){return new List<string>{ "test-model-1", "test-model-2" };}
		}
	}
}