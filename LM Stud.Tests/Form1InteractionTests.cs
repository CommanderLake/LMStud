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
	public class Form1InteractionTests{
		private static NativeMethods.INativeMethods _originalNative;
		private static NativeStub _stub;
		[ClassInitialize]
		public static void ClassInitialize(TestContext context){
			_originalNative = NativeMethods.Implementation;
			_stub = new NativeStub();
			NativeMethods.Implementation = _stub;
		}
		[ClassCleanup]
		public static void ClassCleanup(){NativeMethods.Implementation = _originalNative;}
		private static Form1 CreateForm(){
			var form = (Form1)FormatterServices.GetUninitializedObject(typeof(Form1));
			var staticField = typeof(Form1).GetField("This", BindingFlags.Static | BindingFlags.Public);
			staticField?.SetValue(null, form);
			SetField(form, "GenerationLock", new SemaphoreSlim(1, 1));
			SetField(form, "_chatMessages", new List<ChatMessageControl>());
			SetField(form, "_speechBuffer", new StringBuilder());
			SetField(form, "_swRate", new Stopwatch());
			SetField(form, "_swTot", new Stopwatch());
			var tts = (SpeechSynthesizer)FormatterServices.GetUninitializedObject(typeof(SpeechSynthesizer));
			SetField(form, "_tts", tts);
			SetField(form, "checkDialectic", new CheckBox());
			SetField(form, "checkMarkdown", new CheckBox());
			SetField(form, "checkAutoScroll", new CheckBox());
			SetField(form, "checkVoiceInput", new CheckBox());
			SetField(form, "textInput", new TextBox());
			SetField(form, "panelChat", new MyFlowLayoutPanel());
			SetField(form, "butGen", new Button());
			SetField(form, "butReset", new Button());
			SetField(form, "butApply", new Button());
			SetField(form, "toolStripStatusLabel1", new ToolStripStatusLabel());
			SetField(form, "labelTPS", new ToolStripStatusLabel());
			SetField(form, "labelPreGen", new ToolStripStatusLabel());
			SetField(form, "labelTokens", new ToolStripStatusLabel());
			return form;
		}
		private static void SetField(object instance, string name, object value){
			var field = instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
			if(field == null) throw new InvalidOperationException($"Field '{name}' not found on type '{instance.GetType().FullName}'.");
			field.SetValue(instance, value);
		}
		private static T GetField<T>(object instance, string name){
			var field = instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
			if(field == null) throw new InvalidOperationException($"Field '{name}' not found on type '{instance.GetType().FullName}'.");
			return (T)field.GetValue(instance);
		}
		private static void Invoke(object instance, string name, params object[] parameters){
			var type = instance.GetType();
			var args = parameters ?? Array.Empty<object>();
			MethodInfo match = null;
			foreach(var candidate in type.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)){
				if(!string.Equals(candidate.Name, name, StringComparison.Ordinal)) continue;
				var expected = candidate.GetParameters();
				if(expected.Length != args.Length) continue;
				var compatible = true;
				for(var i = 0; i < expected.Length; i++){
					var arg = args[i];
					var paramType = expected[i].ParameterType;
					if(arg == null){
						if(paramType.IsValueType && Nullable.GetUnderlyingType(paramType) == null){
							compatible = false;
							break;
						}
						continue;
					}
					if(!paramType.IsAssignableFrom(arg.GetType())){
						compatible = false;
						break;
					}
				}
				if(!compatible) continue;
				match = candidate;
				break;
			}
			if(match == null) throw new InvalidOperationException($"Method '{name}' not found on type '{instance.GetType().FullName}'.");
			match.Invoke(instance, parameters);
		}
		[TestMethod]
		public void Generate_WithWhitespaceInput_DoesNotAddMessage(){
			var form = CreateForm();
			SetField(form, "LlModelLoaded", true);
			var textInput = GetField<TextBox>(form, "textInput");
			textInput.Text = "   ";
			Invoke(form, "Generate");
			var messages = GetField<List<ChatMessageControl>>(form, "_chatMessages");
			Assert.AreEqual(0, messages.Count, "Whitespace input should not enqueue a chat message.");
		}
		[TestMethod]
		public void Generate_WhenSemaphoreUnavailable_DoesNotProceed(){
			var form = CreateForm();
			SetField(form, "LlModelLoaded", true);
			SetField(form, "GenerationLock", new SemaphoreSlim(0, 1));
			var textInput = GetField<TextBox>(form, "textInput");
			textInput.Text = "Hello";
			Invoke(form, "Generate");
			var messages = GetField<List<ChatMessageControl>>(form, "_chatMessages");
			Assert.AreEqual(0, messages.Count, "Generation should not start when the semaphore cannot be acquired.");
			Assert.IsFalse(GetField<bool>(form, "_generating"), "Generating flag must remain false when generation is skipped.");
		}
		[TestMethod]
		public void DialecticToggle_EnablesWhenModelLoaded(){
			var form = CreateForm();
			SetField(form, "LlModelLoaded", true);
			var checkDialectic = GetField<CheckBox>(form, "checkDialectic");
			checkDialectic.Checked = true;
			Invoke(form, "CheckDialectic_CheckedChanged", checkDialectic, EventArgs.Empty);
			Assert.IsTrue(checkDialectic.Checked, "Checkbox should stay checked when enabling dialectic mode succeeds.");
			Assert.IsFalse(GetField<bool>(form, "_dialecticStarted"), "Dialectic should reset start flag when enabling.");
			Assert.IsFalse(GetField<bool>(form, "_dialecticPaused"), "Dialectic should reset pause flag when enabling.");
		}
		[TestMethod]
		public void DialecticToggle_DisablesClearingState(){
			var form = CreateForm();
			SetField(form, "LlModelLoaded", true);
			SetField(form, "_dialecticStarted", true);
			SetField(form, "_dialecticPaused", true);
			var checkDialectic = GetField<CheckBox>(form, "checkDialectic");
			checkDialectic.Checked = false;
			Invoke(form, "CheckDialectic_CheckedChanged", checkDialectic, EventArgs.Empty);
			Assert.IsFalse(checkDialectic.Checked, "Checkbox should remain unchecked after disabling.");
			Assert.IsFalse(GetField<bool>(form, "_dialecticStarted"), "Disabling should clear start flag.");
			Assert.IsFalse(GetField<bool>(form, "_dialecticPaused"), "Disabling should clear pause flag.");
		}
		[TestMethod]
		public void MarkdownToggle_AppliesToExistingMessages(){
			var form = CreateForm();
			var messages = GetField<List<ChatMessageControl>>(form, "_chatMessages");
			var userMessage = new ChatMessageControl(MessageRole.User, "Hello", false);
			messages.Add(userMessage);
			var checkMarkdown = GetField<CheckBox>(form, "checkMarkdown");
			checkMarkdown.Checked = true;
			Invoke(form, "CheckMarkdown_CheckedChanged", checkMarkdown, EventArgs.Empty);
			Assert.IsTrue(userMessage.Markdown, "Existing messages should adopt the checkbox markdown setting.");
		}
		[TestMethod]
		public void EditCancel_LeavesMessageUnchanged(){
			var form = CreateForm();
			var messages = GetField<List<ChatMessageControl>>(form, "_chatMessages");
			var message = new ChatMessageControl(MessageRole.User, "Original", false);
			messages.Add(message);
			message.Editing = true;
			var checkMarkdown = GetField<CheckBox>(form, "checkMarkdown");
			checkMarkdown.Checked = true;
			Invoke(form, "MsgButEditCancelOnClick", message);
			Assert.IsFalse(message.Editing, "Cancel should exit edit mode.");
			Assert.IsTrue(message.Markdown, "Message markdown should match checkbox state after cancel.");
			Assert.AreEqual("Original", message.Message, "Cancel should not modify the message text.");
		}
		[TestMethod]
		public void EditApply_WritesBackEditedText(){
			var form = CreateForm();
			var messages = GetField<List<ChatMessageControl>>(form, "_chatMessages");
			var message = new ChatMessageControl(MessageRole.User, "Original", false);
			messages.Add(message);
			message.Editing = true;
			message.richTextMsg.Text = "Updated";
			var checkMarkdown = GetField<CheckBox>(form, "checkMarkdown");
			checkMarkdown.Checked = false;
			Invoke(form, "MsgButEditApplyOnClick", message);
			Assert.IsFalse(message.Editing, "Apply should leave edit mode.");
			Assert.AreEqual("Updated", message.Message, "Apply should persist edits into the message body.");
		}
		[TestMethod]
		public void DeleteMessage_RemovesFromChat(){
			var form = CreateForm();
			var messages = GetField<List<ChatMessageControl>>(form, "_chatMessages");
			var message = new ChatMessageControl(MessageRole.User, "To delete", false);
			messages.Add(message);
			SetField(form, "LlModelLoaded", true);
			Invoke(form, "MsgButDeleteOnClick", message);
			Assert.AreEqual(0, messages.Count, "Message should be removed from collection after delete.");
		}
		[TestMethod]
		public void Regenerate_WhenAlreadyGenerating_DoesNothing(){
			var form = CreateForm();
			var messages = GetField<List<ChatMessageControl>>(form, "_chatMessages");
			var message = new ChatMessageControl(MessageRole.Assistant, "Assistant", false);
			messages.Add(message);
			SetField(form, "_generating", true);
			Invoke(form, "MsgButRegenOnClick", message);
			Assert.AreEqual(1, messages.Count, "Regenerate should not mutate messages while generation is active.");
		}
		private sealed class NativeStub : NativeMethods.INativeMethods{
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
			public NativeMethods.StudError ResetChat(){return NativeMethods.StudError.Success;}
			public void SetTokenCallback(NativeMethods.TokenCallback cb){}
			public void SetThreadCount(int n, int nBatch){}
			public int LlamaMemSize(){return 0;}
			public int GetStateSize(){return 0;}
			public void GetStateData(IntPtr dst, int size){}
			public void SetStateData(IntPtr src, int size){}
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
			public IntPtr ExecuteTool(string name, string argsJson){return IntPtr.Zero;}
			public IntPtr GetToolsJson(out int length){
				length = 0;
				return IntPtr.Zero;
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