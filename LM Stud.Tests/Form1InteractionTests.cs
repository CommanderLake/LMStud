using System;
using System.Threading;
using System.Windows.Forms;
using LMStud;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace LM_Stud.Tests{
	[TestClass]
	public class Form1InteractionTests{
		private static Form1 _form;
		[ClassInitialize]
		public static void ClassInitialize(TestContext context){
			var t = new Thread(Program.Main);
			t.SetApartmentState(ApartmentState.STA);
			t.IsBackground = true;
			t.Start();
			while(Program.MainForm == null) Thread.Sleep(10);
			_form = Program.MainForm;
		}
		[ClassCleanup]
		public static void ClassCleanup(){
			_form.Invoke(new MethodInvoker(() => {
				Program.MainForm.Close();
				Program.MainForm.Dispose();
			}));
		}
		[TestMethod]
		public void Generate_WithWhitespaceInput_DoesNotAddMessage(){
			Common.LlModelLoaded = true;
			_form.Invoke(new MethodInvoker(() => {
				_form.textInput.Text = "   ";
				_form.ButGen_Click(null, null);
			}));
			Thread.Sleep(100);
			Assert.AreEqual(0, _form.ChatMessages.Count, "Whitespace input should not enqueue a chat message.");
		}
		[TestMethod]
		public void Generate_WhenSemaphoreUnavailable_DoesNotProceed(){
			Common.LlModelLoaded = true;
			Generation.GenerationLock.Wait();
			_form.Invoke(new MethodInvoker(() => {
				_form.textInput.Text = "Hello";
				_form.ButGen_Click(null, null);
			}));
			Generation.GenerationLock.Release();
			Assert.AreEqual(0, _form.ChatMessages.Count, "Generation should not start when the semaphore cannot be acquired.");
			Assert.IsFalse(Generation.Generating, "Generating flag must remain false when generation is skipped.");
		}
		[TestMethod]
		public void DialecticToggle_EnablesWhenModelLoaded(){
			Common.LlModelLoaded = true;
			_form.Invoke(new MethodInvoker(() => {
				_form.checkDialectic.Checked = true;
				_form.CheckDialectic_CheckedChanged(_form.checkDialectic, EventArgs.Empty);
			}));
			Assert.IsTrue(_form.checkDialectic.Checked, "Checkbox should stay checked when enabling dialectic mode succeeds.");
			Assert.IsFalse(Generation.DialecticStarted, "Dialectic should reset start flag when enabling.");
			Assert.IsFalse(Generation.DialecticPaused, "Dialectic should reset pause flag when enabling.");
		}
		[TestMethod]
		public void DialecticToggle_DisablesClearingState(){
			Common.LlModelLoaded = true;
			Generation.DialecticStarted = true;
			Generation.DialecticPaused = true;
			_form.Invoke(new MethodInvoker(() => {
				_form.checkDialectic.Checked = false;
				_form.CheckDialectic_CheckedChanged(_form.checkDialectic, EventArgs.Empty);
			}));
			Assert.IsFalse(_form.checkDialectic.Checked, "Checkbox should remain unchecked after disabling.");
			Assert.IsFalse(Generation.DialecticStarted, "Disabling should clear start flag.");
			Assert.IsFalse(Generation.DialecticPaused, "Disabling should clear pause flag.");
		}
		[TestMethod]
		public void MarkdownToggle_AppliesToExistingMessages(){
			_form.Invoke(new MethodInvoker(() => {_form.ButReset_Click(null, null);}));
			Thread.Sleep(100);
			ChatMessageControl message = null;
			_form.Invoke(new MethodInvoker(() => {
				NativeMethods.AddMessage(MessageRole.User, "Original");
				message = _form.AddMessage(MessageRole.User, "", "Original");
			}));
			Thread.Sleep(100);
			_form.Invoke(new MethodInvoker(() => {
				_form.checkMarkdown.Checked = true;
				_form.CheckMarkdown_CheckedChanged(_form.checkMarkdown, EventArgs.Empty);
			}));
			Thread.Sleep(100);
			Assert.IsTrue(message.Markdown, "Existing messages should adopt the checkbox markdown setting.");
		}
		[TestMethod]
		public void EditCancel_LeavesMessageUnchanged(){
			_form.Invoke(new MethodInvoker(() => {_form.ButReset_Click(null, null);}));
			Thread.Sleep(100);
			ChatMessageControl message = null;
			_form.Invoke(new MethodInvoker(() => {
				NativeMethods.AddMessage(MessageRole.User, "Original");
				message = _form.AddMessage(MessageRole.User, "", "Original");
			}));
			Thread.Sleep(100);
			_form.Invoke(new MethodInvoker(() => {
				message.Editing = true;
				_form.checkMarkdown.Checked = true;
				_form.MsgButEditCancelOnClick(message);
			}));
			Thread.Sleep(100);
			Assert.IsFalse(message.Editing, "Cancel should exit edit mode.");
			Assert.IsTrue(message.Markdown, "Message markdown should match checkbox state after cancel.");
			Assert.AreEqual("Original", message.Message, "Cancel should not modify the message text.");
		}
		[TestMethod]
		public void EditApply_WritesBackEditedText(){
			_form.Invoke(new MethodInvoker(() => {_form.ButReset_Click(null, null);}));
			Thread.Sleep(100);
			ChatMessageControl message = null;
			_form.Invoke(new MethodInvoker(() => {
				NativeMethods.AddMessage(MessageRole.User, "Original");
				message = _form.AddMessage(MessageRole.User, "", "Original");
			}));
			Thread.Sleep(100);
			_form.Invoke(new MethodInvoker(() => {
				message.Editing = true;
				message.richTextMsg.Text = "Updated";
				_form.MsgButEditApplyOnClick(message);
			}));
			Thread.Sleep(100);
			Assert.IsFalse(message.Editing, "Apply should leave edit mode.");
			Assert.AreEqual("Updated", message.Message, "Apply should persist edits into the message body.");
		}
		[TestMethod]
		public void DeleteMessage_RemovesFromChat(){
			_form.Invoke(new MethodInvoker(() => {_form.ButReset_Click(null, null);}));
			Thread.Sleep(100);
			ChatMessageControl message = null;
			_form.Invoke(new MethodInvoker(() => {
				NativeMethods.AddMessage(MessageRole.User, "To Delete");
				message = _form.AddMessage(MessageRole.User, "", "To Delete");
			}));
			Thread.Sleep(100);
			_form.Invoke(new MethodInvoker(() => {
				Common.LlModelLoaded = true;
				_form.MsgButDeleteOnClick(message);
			}));
			Thread.Sleep(100);
			Assert.AreEqual(0, _form.ChatMessages.Count, "Message should be removed from collection after delete.");
		}
		[TestMethod]
		public void Regenerate_WhenAlreadyGenerating_DoesNothing(){
			_form.Invoke(new MethodInvoker(() => {_form.ButReset_Click(null, null);}));
			Thread.Sleep(100);
			ChatMessageControl message = null;
			_form.Invoke(new MethodInvoker(() => {
				NativeMethods.AddMessage(MessageRole.Assistant, "Assistant");
				message = _form.AddMessage(MessageRole.Assistant, "", "Assistant");
			}));
			Thread.Sleep(100);
			_form.Invoke(new MethodInvoker(() => {
				Generation.Generating = true;
				_form.MsgButRegenOnClick(message);
			}));
			Thread.Sleep(100);
			Assert.AreEqual(1, _form.ChatMessages.Count, "Regenerate should not mutate messages while generation is active.");
		}
	}
}