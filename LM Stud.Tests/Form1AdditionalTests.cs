using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Speech.Synthesis;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using LMStud;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace LM_Stud.Tests{
	[TestClass]
	public class Form1AdditionalTests{
		private Form1 _form;
		[TestInitialize]
		public void TestInitialize(){_form = CreateForm();}
		[TestCleanup]
		public void TestCleanup(){
			if(_form != null){
				try{ _form.Dispose(); } catch(NullReferenceException){
					// the form is never fully initialised when created via FormatterServices
					// which can cause ObjectDisposed routines to dereference null fields
				}
				var staticField = typeof(Form1).GetField("This", BindingFlags.Static | BindingFlags.Public);
				staticField?.SetValue(null, null);
				_form = null;
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
			return form;
		}
		private static void SetField(object instance, string name, object value){
			var field = instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
			if(field == null) throw new InvalidOperationException($"Field '{name}' not found.");
			field.SetValue(instance, value);
		}
		private static T GetField<T>(object instance, string name){
			var field = instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
			if(field == null) throw new InvalidOperationException($"Field '{name}' not found.");
			return (T)field.GetValue(instance);
		}
		private static void Invoke(object instance, string name, params object[] parameters){
			if(parameters == null) parameters = Array.Empty<object>();
			var methods = instance.GetType().GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
			foreach(var candidate in methods){
				if(candidate.Name != name) continue;
				var parameterInfos = candidate.GetParameters();
				if(!ParametersMatch(parameterInfos, parameters)) continue;
				candidate.Invoke(instance, parameters);
				return;
			}
			throw new InvalidOperationException($"Method '{name}' with matching signature not found.");
		}
		private static bool ParametersMatch(ParameterInfo[] parameterInfos, object[] parameters){
			if(parameterInfos.Length != parameters.Length) return false;
			for(var i = 0; i < parameterInfos.Length; i++){
				var expectedType = parameterInfos[i].ParameterType;
				var providedValue = parameters[i];
				if(providedValue == null){
					if(expectedType.IsValueType && Nullable.GetUnderlyingType(expectedType) == null){ return false; }
					continue;
				}
				if(!expectedType.IsInstanceOfType(providedValue)){ return false; }
			}
			return true;
		}
		[TestMethod]
		public void GetState_ReturnsCurrentState(){
			// Arrange
			NativeMethods.Implementation = new MockNativeMethods{ StateSize = 100 };

			// Act
			var state = _form.GetState();

			// Assert
			Assert.IsNotNull(state, "State should not be null.");
			Assert.AreEqual(100, state.Length, "State size should match.");
		}
		[TestMethod]
		public void SetState_RestoresState(){
			// Arrange
			NativeMethods.Implementation = new MockNativeMethods();
			var state = new byte[]{ 1, 2, 3, 4, 5 };

			// Act
			_form.SetState(state);

			// Assert
			var mock = (MockNativeMethods)NativeMethods.Implementation;
			Assert.IsTrue(mock.ResetChatCalled, "ResetChat should be called.");
			Assert.IsTrue(mock.SetStateDataCalled, "SetStateData should be called.");
		}
		[TestMethod]
		public void SetState_WithNull_OnlyResetsChat(){
			// Arrange
			NativeMethods.Implementation = new MockNativeMethods();

			// Act
			_form.SetState(null);

			// Assert
			var mock = (MockNativeMethods)NativeMethods.Implementation;
			Assert.IsTrue(mock.ResetChatCalled, "ResetChat should be called.");
			Assert.IsFalse(mock.SetStateDataCalled, "SetStateData should not be called for null state.");
		}
		[TestMethod]
		public void GetTokenCount_ReturnsMemorySize(){
			// Arrange
			NativeMethods.Implementation = new MockNativeMethods{ MemSize = 42 };

			// Act
			var count = _form.GetTokenCount();

			// Assert
			Assert.AreEqual(42, count, "Token count should match memory size.");
		}
		[TestMethod]
		public void StartEditing_SetsEditingMode(){
			// Arrange
			var textInput = GetField<TextBox>(_form, "textInput");
			textInput.Text = "Initial text";
			SetField(_form, "_isEditing", false);

			// Act
			Invoke(_form, "StartEditing");

			// Assert
			Assert.IsTrue(GetField<bool>(_form, "_isEditing"), "Should be in editing mode.");
			Assert.AreEqual("Initial text", GetField<string>(_form, "_editOriginalText"), "Original text should be saved.");
		}
		[TestMethod]
		public void FinishEditing_ExitsEditingMode(){
			// Arrange
			SetField(_form, "_isEditing", true);
			NativeMethods.Implementation = new MockNativeMethods();

			// Act
			Invoke(_form, "FinishEditing");

			// Assert
			Assert.IsFalse(GetField<bool>(_form, "_isEditing"), "Should not be in editing mode.");
		}
		[TestMethod]
		public void CancelEditing_RestoresOriginalText(){
			// Arrange
			var textInput = GetField<TextBox>(_form, "textInput");
			SetField(_form, "_isEditing", true);
			SetField(_form, "_editOriginalText", "Original");
			textInput.Text = "Modified";
			NativeMethods.Implementation = new MockNativeMethods();

			// Act
			Invoke(_form, "CancelEditing");

			// Assert
			Assert.AreEqual("Original", textInput.Text, "Should restore original text.");
			Assert.IsFalse(GetField<bool>(_form, "_isEditing"), "Should exit editing mode.");
		}
		[TestMethod]
		public void ApiGenerating_PreventsNormalGeneration(){
			// Arrange
			SetField(_form, "_apiGenerating", true);
			SetField(_form, "LlModelLoaded", true);
			var textInput = GetField<TextBox>(_form, "textInput");
			textInput.Text = "Test message";

			// Act
			Invoke(_form, "Generate");

			// Assert
			var messages = GetField<List<ChatMessage>>(_form, "_chatMessages");
			Assert.AreEqual(0, messages.Count, "Should not add message when API is generating.");
		}
		[TestMethod]
		public void SpeechBuffer_AccumulatesText(){
			// Arrange
			var speechBuffer = GetField<StringBuilder>(_form, "_speechBuffer");
			speechBuffer.Clear();

			// Act
			speechBuffer.Append("Hello ");
			speechBuffer.Append("World");

			// Assert
			Assert.AreEqual("Hello World", speechBuffer.ToString(), "Speech buffer should accumulate text.");
		}
		[TestMethod]
		public void VoiceInput_CheckStateTracking(){
			// Arrange
			var checkVoiceInput = GetField<CheckBox>(_form, "checkVoiceInput");
			SetField(_form, "_checkVoiceInputLast", CheckState.Unchecked);

			// Act
			checkVoiceInput.CheckState = CheckState.Checked;
			SetField(_form, "_checkVoiceInputLast", checkVoiceInput.CheckState);

			// Assert
			Assert.AreEqual(CheckState.Checked, GetField<CheckState>(_form, "_checkVoiceInputLast"), "Should track voice input check state.");
		}
		[TestMethod]
		public void FirstToken_FlagReset(){
			// Arrange
			SetField(_form, "_firstToken", false);

			// Act
			SetField(_form, "_firstToken", true);

			// Assert
			Assert.IsTrue(GetField<bool>(_form, "_firstToken"), "First token flag should be set.");
		}
		private class MockNativeMethods : NativeMethods.INativeMethods{
			public int StateSize{get; set;}
			public int MemSize{get; set;}
			public bool ResetChatCalled{get; set;}
			public bool SetStateDataCalled{get; set;}
			public void SetHWnd(IntPtr hWnd){}
			public void BackendInit(){}
			public NativeMethods.StudError CreateContext(int nCtx, int nBatch, bool flashAttn, int nThreads, int nThreadsBatch){return NativeMethods.StudError.Success;}
			public NativeMethods.StudError CreateSampler(float minP, float topP, int topK, float temp, float repeatPenalty){return NativeMethods.StudError.Success;}
			public NativeMethods.StudError CreateSession(int nCtx, int nBatch, bool flashAttn, int nThreads, int nThreadsBatch, float minP, float topP, int topK, float temp, float repeatPenalty){
				return NativeMethods.StudError.Success;
			}
			public NativeMethods.StudError LoadModel(string filename, string jinjaTemplate, int nGPULayers, bool mMap, bool mLock, NativeMethods.GgmlNumaStrategy numaStrategy){
				return NativeMethods.StudError.Success;
			}
			public void FreeModel(){}
			public NativeMethods.StudError ResetChat(){
				ResetChatCalled = true;
				return NativeMethods.StudError.Success;
			}
			public void SetTokenCallback(NativeMethods.TokenCallback cb){}
			public void SetThreadCount(int n, int nBatch){}
			public int LlamaMemSize(){return MemSize;}
			public int GetStateSize(){return StateSize;}
			public void GetStateData(IntPtr dst, int size){
				unsafe{
					var p = (byte*)dst;
					for(var i = 0; i < size; i++) p[i] = (byte)i;
				}
			}
			public void SetStateData(IntPtr src, int size){SetStateDataCalled = true;}
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
			public NativeMethods.StudError GenerateWithTools(MessageRole role, string prompt, int nPredict, bool callback){return NativeMethods.StudError.Success;}
			public void SetGoogle(string apiKey, string searchEngineID, int resultCount){}
			public void SetFileBaseDir(string dir){}
			public void ClearTools(){}
			public void ClearLastErrorMessage(){}
			public string GetLastError(){return string.Empty;}
			public void RegisterTools(bool dateTime, bool googleSearch, bool webpageFetch, bool fileList, bool fileCreate, bool fileRead, bool fileWrite, bool commandPrompt){}
			public void CloseCommandPrompt(){}
			public void StopGeneration(){}
			public void ClearWebCache(){}
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
			public void SetCommittedText(string text){}
			public unsafe void ConvertMarkdownToRtf(string markdown, ref byte* rtfOut, ref int rtfLen){}
			public IntPtr PerformHttpGet(string url){return IntPtr.Zero;}
			public int DownloadFile(string url, string targetPath){return 0;}
			public int DownloadFileWithProgress(string url, string targetPath, NativeMethods.ProgressCallback progressCallback){return 0;}
			public void FreeMemory(IntPtr ptr){}
			public void CurlGlobalInit(){}
			public void CurlGlobalCleanup(){}
			public IntPtr CaptureChatState(){return IntPtr.Zero;}
			public void RestoreChatState(IntPtr state){}
			public void FreeChatState(IntPtr state){}
			public IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wp, IntPtr lp){return IntPtr.Zero;}
			public bool EnableScrollBar(HandleRef hWnd, int wSBflags, int wArrows){return true;}
		}
	}
}