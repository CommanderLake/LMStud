using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using LMStud;
using LMStud.Properties;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LM_Stud.Tests{
	[TestClass]
	[DoNotParallelize]
	public class ApplicationWorkflowTests{
		private static Form1 _form;
		private static string _modelsDirectory;
		private static string _originalModelsDirectory;

		[ClassInitialize]
		public static void ClassInitialize(TestContext context){
			_originalModelsDirectory = Settings.Default.ModelsDir;
			_modelsDirectory = Path.Combine(Path.GetTempPath(), "lmstud-ui-tests-" + Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(_modelsDirectory);
			Settings.Default.ModelsDir = _modelsDirectory;
			var thread = new Thread(Program.Main);
			thread.SetApartmentState(ApartmentState.STA);
			thread.IsBackground = true;
			thread.Start();
			while(Program.MainForm == null) Thread.Sleep(10);
			_form = Program.MainForm;
retry:		try {_form.Invoke(new MethodInvoker(() => { var handle = _form.Handle; }));}
			catch{Thread.Sleep(10); goto retry;}
			_form.PopulateLock.Wait();
			_form.PopulateLock.Release();
		}

		[ClassCleanup]
		public static void ClassCleanup(){
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
				Settings.Default.ModelsDir = _originalModelsDirectory;
				if(Directory.Exists(_modelsDirectory)) Directory.Delete(_modelsDirectory, true);
			}
		}

		[TestInitialize]
		public void TestInitialize(){ResetConversation();}

		[TestCleanup]
		public void TestCleanup(){
			Generation.Generating = false;
			Generation.APIServerGenerating = false;
			Generation.DialPaused = false;
		}

		[TestMethod]
		public void UserCanEditCancelAndDeleteAChatMessage(){
			ChatMessageControl message = null;
			_form.Invoke(new MethodInvoker(() => {
				NativeMethods.AddMessage("main", MessageRole.User, "", "Original");
				message = _form.AddMessage(MessageRole.User, "", "Original");
				message.Editing = true;
				message.richTextMsg.Text = "Discarded";
				_form.MsgButEditCancelOnClick(message);
			}));
			Assert.AreEqual("Original", message.Message, "Cancelling an edit should leave the message unchanged.");

			_form.Invoke(new MethodInvoker(() => {
				message.Editing = true;
				message.richTextMsg.Text = "Updated";
				_form.MsgButEditApplyOnClick(message);
			}));
			WaitForActiveChatSlotIdle();
			Assert.AreEqual("Updated", message.Message, "Applying an edit should update the conversation.");

			_form.Invoke(new MethodInvoker(() => {_form.MsgButDeleteOnClick(message);}));
			WaitForActiveChatSlotIdle();
			Assert.AreEqual(0, _form.ChatMessages.Count, "Deleting the message should remove it from the conversation.");
		}

		[TestMethod]
		public void DeletingAfterDisplayOnlyToolCallUsesNativeMessageIndex(){
			ChatMessageControl finalAnswer = null;
			_form.Invoke(new MethodInvoker(() => {
				NativeMethods.AddMessage("main", MessageRole.User, "", "Question");
				NativeMethods.AddMessage("main", MessageRole.Assistant, "", "");
				NativeMethods.AddMessage("main", MessageRole.Tool, "", "{\"ok\":true}");
				NativeMethods.AddMessage("main", MessageRole.Assistant, "", "Final answer");

				_form.AddMessage(MessageRole.User, "", "Question");
				_form.AddMessage(MessageRole.Assistant, "", "");
				var toolCallDisplay = _form.AddMessage(MessageRole.Tool, "", "Tool name: test", null, null, false);
				toolCallDisplay.SetRoleText(Resources.Tool_Call);
				_form.AddMessage(MessageRole.Tool, "", "{\"ok\":true}");
				finalAnswer = _form.AddMessage(MessageRole.Assistant, "", "Final answer");
			}));

			_form.Invoke(new MethodInvoker(() => {_form.MsgButDeleteOnClick(finalAnswer);}));
			WaitForActiveChatSlotIdle();

			Assert.AreEqual(4, _form.ChatMessages.Count, "Deleting a message after a display-only tool-call row should not hit the native index limit.");
			Assert.IsFalse(_form.ChatMessages.Contains(finalAnswer), "The selected answer should be removed from the UI.");
		}

		[TestMethod]
		public void MarkdownPreferenceUpdatesExistingMessages(){
			ChatMessageControl message = null;
			_form.Invoke(new MethodInvoker(() => {
				NativeMethods.AddMessage("main", MessageRole.User, "", "**formatted**");
				message = _form.AddMessage(MessageRole.User, "", "**formatted**");
				_form.checkMarkdown.Checked = true;
				_form.CheckMarkdown_CheckedChanged(_form.checkMarkdown, EventArgs.Empty);
			}));
			Assert.IsTrue(message.Markdown);
			Assert.AreEqual("formatted", message.MarkdownView.PlainText);
		}

		private static void ResetConversation(){
			_form.Invoke(new MethodInvoker(() => {
				_form.textInput.Text = "";
				_form.checkMarkdown.Checked = false;
				_form.checkDialectic.Checked = false;
				_form.ButReset_Click(null, null);
			}));
			var deadline = DateTime.UtcNow.AddSeconds(30);
			while(DateTime.UtcNow < deadline){
				var slotName = ModelSlotManager.GetActiveChatSlot()?.Name ?? Common.ActiveModelSlotName ?? "main";
				var slotLock = ModelSlotManager.TryEnterSlot(slotName, 50);
				if(slotLock != null)
					try{
						var count = -1;
						_form.Invoke(new MethodInvoker(() => {count = _form.ChatMessages.Count;}));
						if(count == 0) return;
					} finally{slotLock.Dispose();}
				Thread.Sleep(10);
			}
			Assert.Fail("Timed out waiting for the conversation to reset.");
		}

		private static void WaitForActiveChatSlotIdle(){
			var deadline = DateTime.UtcNow.AddSeconds(30);
			while(DateTime.UtcNow < deadline){
				var slotName = ModelSlotManager.GetActiveChatSlot()?.Name ?? Common.ActiveModelSlotName ?? "main";
				var slotLock = ModelSlotManager.TryEnterSlot(slotName, 50);
				if(slotLock != null){
					slotLock.Dispose();
					return;
				}
				Thread.Sleep(10);
			}
			Assert.Fail("Timed out waiting for the chat operation to finish.");
		}

	}
}
