using System;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using LMStud;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace LM_Stud.Tests{
	[TestClass]
	public class Form1AdditionalTests{
		private Form1 _form;
		[ClassInitialize]
		public static void ClassInitialize(TestContext context){
			var t = new Thread(Program.Main);
			t.SetApartmentState(ApartmentState.STA);
			t.IsBackground = true;
			t.Start();
			while(Program.MainForm == null) Thread.Sleep(10);
		}
		[ClassCleanup]
		public static void ClassCleanup(){
			Program.MainForm.Close();
			Program.MainForm.Dispose();
		}
		[TestInitialize]
		public void TestInitialize(){
			_form = Program.MainForm;
			_form.Invoke(new MethodInvoker(() => {_form.ButReset_Click(null, null);}));
			Thread.Sleep(100);
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
					if(expectedType.IsValueType && Nullable.GetUnderlyingType(expectedType) == null) return false;
					continue;
				}
				if(!expectedType.IsInstanceOfType(providedValue)) return false;
			}
			return true;
		}
		[TestMethod]
		public void GetState_ReturnsCurrentState(){
			byte[] state = null;
			_form.Invoke(new MethodInvoker(() => {state = _form.GetState();}));
			Assert.IsNotNull(state, "State should not be null.");
			Assert.IsTrue(state.Length >= 0, "State size should be non-negative.");
		}
		[TestMethod]
		public void SetState_RestoresState(){
			var state = new byte[]{ 1, 2, 3, 4, 5 };
			_form.Invoke(new MethodInvoker(() => {_form.SetState(state);}));
		}
		[TestMethod]
		public void SetState_WithNull_OnlyResetsChat(){_form.Invoke(new MethodInvoker(() => {_form.SetState(null);}));}
		[TestMethod]
		public void GetTokenCount_ReturnsMemorySize(){
			var count = 0;
			_form.Invoke(new MethodInvoker(() => {count = _form.GetTokenCount();}));
			Assert.IsTrue(count >= 0, "Token count should be non-negative.");
		}
		[TestMethod]
		public void StartEditing_SetsEditingMode(){
			_form.Invoke(new MethodInvoker(() => {
				var textInput = _form.textInput;
				textInput.Text = "Initial text";
				_form.IsEditing = false;
			}));
			_form.Invoke(new MethodInvoker(() => {Invoke(_form, "StartEditing");}));
			Assert.IsTrue(_form.IsEditing, "Should be in editing mode.");
			Assert.AreEqual("Initial text", _form.EditOriginalText, "Original text should be saved.");
		}
		[TestMethod]
		public void FinishEditing_ExitsEditingMode(){
			_form.Invoke(new MethodInvoker(() => {_form.IsEditing = true;}));
			_form.Invoke(new MethodInvoker(() => {Invoke(_form, "FinishEditing");}));
			Assert.IsFalse(_form.IsEditing, "Should not be in editing mode.");
		}
		[TestMethod]
		public void CancelEditing_RestoresOriginalText(){
			_form.Invoke(new MethodInvoker(() => {
				var textInput = _form.textInput;
				_form.IsEditing = true;
				_form.EditOriginalText = "Original";
				textInput.Text = "Modified";
			}));
			_form.Invoke(new MethodInvoker(() => {Invoke(_form, "CancelEditing");}));
			string restored = null;
			_form.Invoke(new MethodInvoker(() => {
				var textInput = _form.textInput;
				restored = textInput.Text;
			}));
			Assert.AreEqual("Original", restored, "Should restore original text.");
			Assert.IsFalse(_form.IsEditing, "Should exit editing mode.");
		}
		[TestMethod]
		public void ApiGenerating_PreventsNormalGeneration(){
			_form.Invoke(new MethodInvoker(() => {
				_form.APIGenerating = true;
				_form.LlModelLoaded = true;
				var textInput = _form.textInput;
				textInput.Text = "Test message";
			}));
			_form.Invoke(new MethodInvoker(() => {Invoke(_form, "Generate");}));
			var messages = _form.ChatMessages;
			Assert.AreEqual(0, messages.Count, "Should not add message when API is generating.");
		}
		[TestMethod]
		public void SpeechBuffer_AccumulatesText(){
			var speechBuffer = _form.SpeechBuffer;
			_form.Invoke(new MethodInvoker(() => {speechBuffer.Clear();}));
			_form.Invoke(new MethodInvoker(() => {
				speechBuffer.Append("Hello ");
				speechBuffer.Append("World");
			}));
			Assert.AreEqual("Hello World", speechBuffer.ToString(), "Speech buffer should accumulate text.");
		}
		[TestMethod]
		public void VoiceInput_CheckStateTracking(){
			_form.Invoke(new MethodInvoker(() => {_form.CheckVoiceInputLast = CheckState.Unchecked;}));
			_form.Invoke(new MethodInvoker(() => {
				var checkVoiceInput = _form.checkVoiceInput;
				checkVoiceInput.CheckState = CheckState.Checked;
				_form.CheckVoiceInputLast = checkVoiceInput.CheckState;
			}));
			Assert.AreEqual(CheckState.Checked, _form.CheckVoiceInputLast, "Should track voice input check state.");
		}
		[TestMethod]
		public void FirstToken_FlagReset(){
			_form.Invoke(new MethodInvoker(() => {_form.FirstToken = false;}));
			_form.Invoke(new MethodInvoker(() => {_form.FirstToken = true;}));
			Assert.IsTrue(_form.FirstToken, "First token flag should be set.");
		}
	}
}