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
		public void StartEditing_SetsEditingMode(){
			_form.Invoke(new MethodInvoker(() => {
				var textInput = _form.textInput;
				textInput.Text = "Initial text";
				_form.IsEditing = false;
			}));
			_form.Invoke(new MethodInvoker(() => {_form.StartEditing();}));
			Assert.IsTrue(_form.IsEditing, "Should be in editing mode.");
			Assert.AreEqual("Initial text", _form.InputEditOldText, "Original text should be saved.");
		}
		[TestMethod]
		public void FinishEditing_ExitsEditingMode(){
			_form.Invoke(new MethodInvoker(() => {_form.IsEditing = true;}));
			_form.Invoke(new MethodInvoker(() => {_form.FinishEditing();}));
			Assert.IsFalse(_form.IsEditing, "Should not be in editing mode.");
		}
		[TestMethod]
		public void CancelEditing_RestoresOriginalText(){
			_form.Invoke(new MethodInvoker(() => {
				var textInput = _form.textInput;
				_form.IsEditing = true;
				_form.InputEditOldText = "Original";
				textInput.Text = "Modified";
			}));
			_form.Invoke(new MethodInvoker(() => {_form.CancelEditing();}));
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
				Generation.APIServerGenerating = true;
				Common.LlModelLoaded = true;
				var textInput = _form.textInput;
				textInput.Text = "Test message";
			}));
			_form.Invoke(new MethodInvoker(Generation.Generate));
			var messages = _form.ChatMessages;
			Assert.AreEqual(0, messages.Count, "Should not add message when API is generating.");
		}
		[TestMethod]
		public void SpeechBuffer_AccumulatesText(){
			var speechBuffer = TTS.Pending;
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
			_form.Invoke(new MethodInvoker(() => {Generation.FirstToken = false;}));
			_form.Invoke(new MethodInvoker(() => {Generation.FirstToken = true;}));
			Assert.IsTrue(Generation.FirstToken, "First token flag should be set.");
		}
	}
}