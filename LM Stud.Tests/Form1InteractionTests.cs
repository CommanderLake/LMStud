using System;
using System.Threading;
using System.Windows.Forms;
using LMStud;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace LM_Stud.Tests{
	[TestClass]
	public class Form1InteractionTests{
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
		[TestMethod]
		public void Generate_WithWhitespaceInput_DoesNotAddMessage(){
			var form = Program.MainForm;
			form.LlModelLoaded = true;
			form.Invoke(new MethodInvoker(() => {
				form.textInput.Text = "   ";
				form.ButGen_Click(null, null);
			}));
			Thread.Sleep(100);
			Assert.AreEqual(0, form.ChatMessages.Count, "Whitespace input should not enqueue a chat message.");
		}
		[TestMethod]
		public void Generate_WhenSemaphoreUnavailable_DoesNotProceed(){
			var form = Program.MainForm;
			form.LlModelLoaded = true;
			form.GenerationLock.Wait();
			form.Invoke(new MethodInvoker(() => {
				form.textInput.Text = "Hello";
				form.ButGen_Click(null, null);
			}));
			form.GenerationLock.Release();
			Assert.AreEqual(0, form.ChatMessages.Count, "Generation should not start when the semaphore cannot be acquired.");
			Assert.IsFalse(form.Generating, "Generating flag must remain false when generation is skipped.");
		}
		[TestMethod]
		public void DialecticToggle_EnablesWhenModelLoaded(){
			var form = Program.MainForm;
			form.LlModelLoaded = true;
			form.Invoke(new MethodInvoker(() => {
				form.checkDialectic.Checked = true;
				form.CheckDialectic_CheckedChanged(form.checkDialectic, EventArgs.Empty);
			}));
			Assert.IsTrue(form.checkDialectic.Checked, "Checkbox should stay checked when enabling dialectic mode succeeds.");
			Assert.IsFalse(form.DialecticStarted, "Dialectic should reset start flag when enabling.");
			Assert.IsFalse(form.DialecticPaused, "Dialectic should reset pause flag when enabling.");
		}
		[TestMethod]
		public void DialecticToggle_DisablesClearingState(){
			var form = Program.MainForm;
			form.LlModelLoaded = true;
			form.DialecticStarted = true;
			form.DialecticPaused = true;
			form.Invoke(new MethodInvoker(() => {
				form.checkDialectic.Checked = false;
				form.CheckDialectic_CheckedChanged(form.checkDialectic, EventArgs.Empty);
			}));
			Assert.IsFalse(form.checkDialectic.Checked, "Checkbox should remain unchecked after disabling.");
			Assert.IsFalse(form.DialecticStarted, "Disabling should clear start flag.");
			Assert.IsFalse(form.DialecticPaused, "Disabling should clear pause flag.");
		}
		[TestMethod]
		public void MarkdownToggle_AppliesToExistingMessages(){
			var form = Program.MainForm;
			form.Invoke(new MethodInvoker(() => {form.ButReset_Click(null, null);}));
			Thread.Sleep(100);
			ChatMessageControl message = null;
			form.Invoke(new MethodInvoker(() => {
				NativeMethods.AddMessage(MessageRole.User, "Original");
				message = form.AddMessage(MessageRole.User, "", "Original");
			}));
			Thread.Sleep(100);
			form.Invoke(new MethodInvoker(() => {
				form.checkMarkdown.Checked = true;
				form.CheckMarkdown_CheckedChanged(form.checkMarkdown, EventArgs.Empty);
			}));
			Thread.Sleep(100);
			Assert.IsTrue(message.Markdown, "Existing messages should adopt the checkbox markdown setting.");
		}
		[TestMethod]
		public void EditCancel_LeavesMessageUnchanged(){
			var form = Program.MainForm;
			form.Invoke(new MethodInvoker(() => {form.ButReset_Click(null, null);}));
			Thread.Sleep(100);
			ChatMessageControl message = null;
			form.Invoke(new MethodInvoker(() => {
				NativeMethods.AddMessage(MessageRole.User, "Original");
				message = form.AddMessage(MessageRole.User, "", "Original");
			}));
			Thread.Sleep(100);
			form.Invoke(new MethodInvoker(() => {
				message.Editing = true;
				form.checkMarkdown.Checked = true;
				form.MsgButEditCancelOnClick(message);
			}));
			Thread.Sleep(100);
			Assert.IsFalse(message.Editing, "Cancel should exit edit mode.");
			Assert.IsTrue(message.Markdown, "Message markdown should match checkbox state after cancel.");
			Assert.AreEqual("Original", message.Message, "Cancel should not modify the message text.");
		}
		[TestMethod]
		public void EditApply_WritesBackEditedText(){
			var form = Program.MainForm;
			form.Invoke(new MethodInvoker(() => {form.ButReset_Click(null, null);}));
			Thread.Sleep(100);
			ChatMessageControl message = null;
			form.Invoke(new MethodInvoker(() => {
				NativeMethods.AddMessage(MessageRole.User, "Original");
				message = form.AddMessage(MessageRole.User, "", "Original");
			}));
			Thread.Sleep(100);
			form.Invoke(new MethodInvoker(() => {
				message.Editing = true;
				message.richTextMsg.Text = "Updated";
				form.MsgButEditApplyOnClick(message);
			}));
			Thread.Sleep(100);
			Assert.IsFalse(message.Editing, "Apply should leave edit mode.");
			Assert.AreEqual("Updated", message.Message, "Apply should persist edits into the message body.");
		}
		[TestMethod]
		public void DeleteMessage_RemovesFromChat(){
			var form = Program.MainForm;
			form.Invoke(new MethodInvoker(() => {form.ButReset_Click(null, null);}));
			Thread.Sleep(100);
			ChatMessageControl message = null;
			form.Invoke(new MethodInvoker(() => {
				NativeMethods.AddMessage(MessageRole.User, "To Delete");
				message = form.AddMessage(MessageRole.User, "", "To Delete");
			}));
			Thread.Sleep(100);
			form.Invoke(new MethodInvoker(() => {
				form.LlModelLoaded = true;
				form.MsgButDeleteOnClick(message);
			}));
			Thread.Sleep(100);
			Assert.AreEqual(0, form.ChatMessages.Count, "Message should be removed from collection after delete.");
		}
		[TestMethod]
		public void Regenerate_WhenAlreadyGenerating_DoesNothing(){
			var form = Program.MainForm;
			form.Invoke(new MethodInvoker(() => {form.ButReset_Click(null, null);}));
			Thread.Sleep(100);
			ChatMessageControl message = null;
			form.Invoke(new MethodInvoker(() => {
				NativeMethods.AddMessage(MessageRole.Assistant, "Assistant");
				message = form.AddMessage(MessageRole.Assistant, "", "Assistant");
			}));
			Thread.Sleep(100);
			form.Invoke(new MethodInvoker(() => {
				form.Generating = true;
				form.MsgButRegenOnClick(message);
			}));
			Thread.Sleep(100);
			Assert.AreEqual(1, form.ChatMessages.Count, "Regenerate should not mutate messages while generation is active.");
		}
	}
}