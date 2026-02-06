using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Speech.Synthesis;
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
		private ApiServer _apiServer;
		private Form1 _form;
		private NativeMethodsStub _nativeStub;
		private string _originalLastModel;
		[TestInitialize]
		public void TestInitialize(){
			_form = CreateForm();
			SetField(_form, "LlModelLoaded", true);
			SetModelList(_form, "test-model-1", "test-model-2");
			_nativeStub = new NativeMethodsStub();
			_nativeStub.Install();
			_originalLastModel = Settings.Default.LastModel;
			Settings.Default.LastModel = "test-model-1.gguf";
			_apiServer = new ApiServer(_form){ Port = TestPort };
		}
		[TestCleanup]
		public void TestCleanup(){
			Settings.Default.LastModel = _originalLastModel;
			_apiServer?.Stop();
			_nativeStub?.Dispose();
			if(_form != null){
				try{ _form.Dispose(); } catch(NullReferenceException){/* form was never fully initialised */
				}
				var staticField = typeof(Form1).GetField("This", BindingFlags.Static | BindingFlags.Public);
				staticField?.SetValue(null, null);
				_form = null;
			}
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
				Assert.AreEqual(2, (int)result.data.Count, "Should return two models from the stub list.");
				Assert.AreEqual("test-model-1", (string)result.data[0].id, "First model id should match stub.");
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
			SetField(_form, "LlModelLoaded", false);
			_apiServer.Start();
			await WaitForServerAsync();
			using(var client = new HttpClient()){
				var payload = new{ messages = new[]{ new{ role = "user", content = "hello" } } };
				var response = await client.PostAsync($"http://localhost:{TestPort}/v1/chat/completions", new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json"));
				Assert.AreEqual(HttpStatusCode.Conflict, response.StatusCode, "Chat endpoint should return 409 when model is unavailable.");
			}
		}
		[TestMethod]
		public async Task HandleChat_WhenModelLoaded_ReturnsAssistantResponse(){
			_apiServer.Start();
			await WaitForServerAsync();
			using(var client = new HttpClient()){
				var payload = new{ messages = new[]{ new{ role = "user", content = "Hello" } } };
				var response = await client.PostAsync($"http://localhost:{TestPort}/v1/chat/completions", new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json"));
				Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "Successful chat should return 200.");
				var sessionId = response.Headers.Contains("X-Session-Id") ? string.Join("", response.Headers.GetValues("X-Session-Id")) : null;
				Assert.IsFalse(string.IsNullOrEmpty(sessionId), "Session id header should be present.");
				var json = await response.Content.ReadAsStringAsync();
				dynamic result = JsonConvert.DeserializeObject(json);
				Assert.AreEqual("assistant response", (string)result.choices[0].message.content, "Stubbed assistant response should be returned.");
				Assert.AreEqual("Hello", _nativeStub.LastPrompt, "Native stub should receive prompt from request.");
				var sessionManager = GetSessionManager();
				var session = sessionManager.Get(sessionId);
				Assert.AreEqual(2, session.Messages.Count, "Session should contain user and assistant messages.");
				Assert.AreEqual(_nativeStub.LastGeneratedTokenCount, session.TokenCount, "Session token count should reflect native stub result.");
			}
		}
		[TestMethod]
		public async Task HandleReset_WithSessionId_RemovesSession(){
			_apiServer.Start();
			await WaitForServerAsync();
			string sessionId;
			using(var client = new HttpClient()){
				var payload = new{ messages = new[]{ new{ role = "user", content = "Hi" } } };
				var response = await client.PostAsync($"http://localhost:{TestPort}/v1/chat/completions", new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json"));
				sessionId = response.Headers.Contains("X-Session-Id") ? string.Join("", response.Headers.GetValues("X-Session-Id")) : null;
				Assert.IsFalse(string.IsNullOrEmpty(sessionId), "Chat call should return a session id.");
			}
			var sessionManager = GetSessionManager();
			var existing = sessionManager.Get(sessionId);
			using(var client = new HttpClient()){
				var resetPayload = new{ session_id = sessionId };
				var response = await client.PostAsync($"http://localhost:{TestPort}/v1/chat/reset", new StringContent(JsonConvert.SerializeObject(resetPayload), Encoding.UTF8, "application/json"));
				Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "Reset endpoint should return success.");
				var json = await response.Content.ReadAsStringAsync();
				dynamic result = JsonConvert.DeserializeObject(json);
				Assert.AreEqual("reset", (string)result.status, "Reset response should confirm reset state.");
			}
			Assert.IsTrue(_nativeStub.ResetChatCalled, "Reset should invoke native reset.");
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
		private static Form1 CreateForm(){
			var form = (Form1)FormatterServices.GetUninitializedObject(typeof(Form1));
			var staticField = typeof(Form1).GetField("This", BindingFlags.Static | BindingFlags.Public);
			staticField?.SetValue(null, form);
			SetField(form, "GenerationLock", new SemaphoreSlim(1, 1));
			SetField(form, "_chatMessages", new List<ChatMessage>());
			SetField(form, "_speechBuffer", new StringBuilder());
			SetField(form, "_swRate", new Stopwatch());
			SetField(form, "_swTot", new Stopwatch());
			SetField(form, "_tts", new SpeechSynthesizer());
			SetField(form, "checkDialectic", new CheckBox());
			SetField(form, "checkMarkdown", new CheckBox());
			SetField(form, "checkAutoScroll", new CheckBox());
			SetField(form, "checkVoiceInput", new CheckBox());
			SetField(form, "checkStream", new CheckBox());
			SetField(form, "textInput", new TextBox());
			SetField(form, "panelChat", new MyFlowLayoutPanel());
			SetField(form, "butGen", new Button());
			SetField(form, "butReset", new Button());
			SetField(form, "butApply", new Button());
			SetField(form, "toolStripStatusLabel1", new ToolStripStatusLabel());
			SetField(form, "labelTPS", new ToolStripStatusLabel());
			SetField(form, "labelPreGen", new ToolStripStatusLabel());
			SetField(form, "labelTokens", new ToolStripStatusLabel());
			SetField(form, "labelEditing", new ToolStripStatusLabel());
			var modelInfoType = typeof(Form1).GetNestedType("ModelInfo", BindingFlags.NonPublic);
			var modelsList = Activator.CreateInstance(typeof(List<>).MakeGenericType(modelInfoType));
			SetField(form, "_models", modelsList);
			SetField(form, "_whisperModels", new List<string>());
			return form;
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
		private sealed class NativeMethodsStub : NativeMethods.INativeMethods, IDisposable{
			private readonly NativeMethods.INativeMethods _original;
			private byte[] _state = Array.Empty<byte>();
			public NativeMethodsStub(){_original = NativeMethods.Implementation;}
			public string LastPrompt{get; private set;}
			public int TokenCount{get; private set;}
			public int LastGeneratedTokenCount{get; private set;}
			public bool ResetChatCalled{get; private set;}
			public void Dispose(){NativeMethods.Implementation = _original;}
			public void SetHWnd(IntPtr hWnd){}
			public void BackendInit(){}
			public NativeMethods.StudError CreateContext(int nCtx, int nBatch, uint flashAttn, int nThreads, int nThreadsBatch){return NativeMethods.StudError.Success;}
			public NativeMethods.StudError CreateSampler(float minP, float topP, int topK, float temp, float repeatPenalty){return NativeMethods.StudError.Success;}
			public NativeMethods.StudError CreateSession(int nCtx, int nBatch, uint flashAttn, int nThreads, int nThreadsBatch, float minP, float topP, int topK, float temp, float repeatPenalty){
				return NativeMethods.StudError.Success;
			}
			public NativeMethods.StudError LoadModel(string filename, string jinjaTemplate, int nGPULayers, bool mMap, bool mLock, NativeMethods.GgmlNumaStrategy numaStrategy){
				return NativeMethods.StudError.Success;
			}
			public void FreeModel(){}
			public NativeMethods.StudError ResetChat(){
				ResetChatCalled = true;
				TokenCount = 0;
				return NativeMethods.StudError.Success;
			}
			public void SetTokenCallback(NativeMethods.TokenCallback cb){}
			public void SetThreadCount(int n, int nBatch){}
			public int LlamaMemSize(){return TokenCount;}
			public int GetStateSize(){return _state.Length;}
			public void GetStateData(IntPtr dst, int size){
				if(_state.Length == 0 || size == 0) return;
				Marshal.Copy(_state, 0, dst, Math.Min(size, _state.Length));
			}
			public void SetStateData(IntPtr src, int size){
				_state = new byte[size];
				if(size > 0) Marshal.Copy(src, _state, 0, size);
			}
			public NativeMethods.StudError SetSystemPrompt(string prompt, string toolsPrompt){return NativeMethods.StudError.Success;}
			public NativeMethods.StudError SetMessageAt(int index, string think, string message){return NativeMethods.StudError.Success;}
			public void DialecticInit(){}
			public void DialecticStart(){}
			public NativeMethods.StudError DialecticSwap(){return NativeMethods.StudError.Success;}
			public void DialecticFree(){}
			public NativeMethods.StudError RetokenizeChat(bool rebuildMemory){return NativeMethods.StudError.Success;}
			public NativeMethods.StudError RemoveMessageAt(int index){return NativeMethods.StudError.Success;}
			public NativeMethods.StudError RemoveMessagesStartingAt(int index){return NativeMethods.StudError.Success;}
			public NativeMethods.StudError AddMessage(MessageRole role, string message){return NativeMethods.StudError.Success;}
			public NativeMethods.StudError GenerateWithTools(MessageRole role, string prompt, int nPredict, bool callback){
				LastPrompt = prompt;
				TokenCount = Math.Max(1, prompt?.Length ?? 0);
				LastGeneratedTokenCount = TokenCount;
				_state = Encoding.UTF8.GetBytes("state:" + prompt);
				var callbackField = typeof(Form1).GetField("_apiTokenCallback", BindingFlags.Instance | BindingFlags.NonPublic);
				var cb = (Action<string>)callbackField?.GetValue(Form1.This);
				cb?.Invoke("assistant response");
				return NativeMethods.StudError.Success;
			}
			public void SetGoogle(string apiKey, string searchEngineID, int resultCount){}
			public void SetFileBaseDir(string dir){}
			public void ClearTools(){}
			public void ClearLastErrorMessage(){}
			public string GetLastError(){return string.Empty;}
			public void RegisterTools(bool dateTime, bool googleSearch, bool webpageFetch, bool fileList, bool fileCreate, bool fileRead, bool fileWrite, bool commandPrompt){}
			public void CloseCommandPrompt(){}
			public void StopGeneration(){}
			public void ClearWebCache(){}
			public unsafe void ConvertMarkdownToRtf(string markdown, ref byte* rtfOut, ref int rtfLen){
				if(markdown == null){
					rtfOut = null;
					rtfLen = 0;
					return;
				}
				var rtfText = $"{{\\rtf1\\ansi {markdown.Replace("\\", "\\\\").Replace("{", "\\{").Replace("}", "\\}")}}}";
				var bytes = Encoding.ASCII.GetBytes(rtfText);
				var buffer = Marshal.AllocHGlobal(bytes.Length);
				Marshal.Copy(bytes, 0, buffer, bytes.Length);
				rtfOut = (byte*)buffer;
				rtfLen = bytes.Length;
			}
			public void SetWhisperCallback(NativeMethods.WhisperCallback cb){}
			public void SetSpeechEndCallback(NativeMethods.SpeechEndCallback cb){}
			public NativeMethods.StudError LoadWhisperModel(string modelPath, int nThreads, bool useGPU, bool useVAD, string vadModel){return NativeMethods.StudError.Success;}
			public void UnloadWhisperModel(){}
			public bool StartSpeechTranscription(){return false;}
			public void StopSpeechTranscription(){}
			public void SetWakeCommand(string wakeCmd){}
			public void SetVADThresholds(float vad, float freq){}
			public void SetWakeWordSimilarity(float similarity){}
			public void SetWhisperTemp(float temp){}
			public void SetSilenceTimeout(int milliseconds){}
			public void SetCommandPromptTimeout(int milliseconds){}
			public void SetCommittedText(string text){}
			public IntPtr PerformHttpGet(string url){return IntPtr.Zero;}
			public int DownloadFile(string url, string targetPath){return 0;}
			public int DownloadFileWithProgress(string url, string targetPath, NativeMethods.ProgressCallback progressCallback){return 0;}
			public void FreeMemory(IntPtr ptr){
				if(ptr != IntPtr.Zero) Marshal.FreeHGlobal(ptr);
			}
			public void CurlGlobalInit(){}
			public void CurlGlobalCleanup(){}
			public IntPtr CaptureChatState(){return IntPtr.Zero;}
			public void RestoreChatState(IntPtr state){}
			public void FreeChatState(IntPtr state){}
			public IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wp, IntPtr lp){return IntPtr.Zero;}
			public bool EnableScrollBar(HandleRef hWnd, int wSBflags, int wArrows){return true;}
			public void Install(){NativeMethods.Implementation = this;}
		}
	}
}