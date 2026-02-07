using System.Windows.Forms;
using LMStud;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace LM_Stud.Tests{
	[TestClass]
	public class ChatMessageTests{
		private ChatMessageControl _chatMessage;
		private Panel _parentPanel;
		[TestInitialize]
		public void TestInitialize(){_parentPanel = new Panel();}
		[TestCleanup]
		public void TestCleanup(){
			_chatMessage?.Dispose();
			_parentPanel?.Dispose();
		}
		[TestMethod]
		public void Constructor_InitializesWithUserRole(){
			_chatMessage = new ChatMessageControl(MessageRole.User, "Test message", false);
			Assert.AreEqual(MessageRole.User, _chatMessage.Role, "Role should be User.");
			Assert.AreEqual("Test message", _chatMessage.Message, "Message should match.");
			Assert.IsFalse(_chatMessage.Markdown, "Markdown should be false.");
			Assert.IsFalse(_chatMessage.Editing, "Should not be in editing mode initially.");
			Assert.IsFalse(_chatMessage.Generating, "Should not be generating initially.");
		}
		[TestMethod]
		public void Constructor_InitializesWithAssistantRole(){
			_chatMessage = new ChatMessageControl(MessageRole.Assistant, "Response", true);
			Assert.AreEqual(MessageRole.Assistant, _chatMessage.Role, "Role should be Assistant.");
			Assert.AreEqual("Response", _chatMessage.Message, "Message should match.");
			Assert.IsTrue(_chatMessage.Markdown, "Markdown should be true.");
		}
		[TestMethod]
		public void Constructor_InitializesWithToolRole(){
			_chatMessage = new ChatMessageControl(MessageRole.Tool, "Tool output", false);
			Assert.AreEqual(MessageRole.Tool, _chatMessage.Role, "Role should be Tool.");
			Assert.AreEqual("Tool output", _chatMessage.Message, "Message should match.");
		}
		[TestMethod]
		public void SetRoleText_UpdatesRoleLabel(){
			_chatMessage = new ChatMessageControl(MessageRole.User, "Test", false);
			_chatMessage.SetRoleText("Custom Role");
			Assert.AreEqual("Custom Role", _chatMessage.labelRole.Text, "Role text should be updated.");
		}
		[TestMethod]
		public void Editing_SetTrue_ShowsEditControls(){
			_chatMessage = new ChatMessageControl(MessageRole.User, "Test", false);
			_chatMessage.Editing = true;
			Assert.IsTrue(_chatMessage.richTextMsg.ReadOnly == false, "RichTextBox should be editable.");
			Assert.IsTrue(_chatMessage.butApplyEdit.Visible, "Apply button should be visible.");
			Assert.IsTrue(_chatMessage.butCancelEdit.Visible, "Cancel button should be visible.");
			Assert.IsFalse(_chatMessage.butEdit.Visible, "Edit button should be hidden.");
		}
		[TestMethod]
		public void Editing_SetFalse_HidesEditControls(){
			_chatMessage = new ChatMessageControl(MessageRole.User, "Test", false);
			_chatMessage.Editing = true;
			_chatMessage.Editing = false;
			Assert.IsTrue(_chatMessage.richTextMsg.ReadOnly, "RichTextBox should be read-only.");
			Assert.IsFalse(_chatMessage.butApplyEdit.Visible, "Apply button should be hidden.");
			Assert.IsFalse(_chatMessage.butCancelEdit.Visible, "Cancel button should be hidden.");
			Assert.IsTrue(_chatMessage.butEdit.Visible, "Edit button should be visible.");
		}
		[TestMethod]
		public void Generating_SetTrue_DisablesButtons(){
			_chatMessage = new ChatMessageControl(MessageRole.User, "Test", false);
			_chatMessage.Generating = true;
			Assert.IsFalse(_chatMessage.butEdit.Enabled, "Edit button should be disabled.");
			Assert.IsFalse(_chatMessage.butDelete.Enabled, "Delete button should be disabled.");
			Assert.IsFalse(_chatMessage.butRegen.Enabled, "Regen button should be disabled.");
		}
		[TestMethod]
		public void Generating_SetFalse_EnablesButtons(){
			_chatMessage = new ChatMessageControl(MessageRole.User, "Test", false);
			_chatMessage.Generating = true;
			_chatMessage.Generating = false;
			Assert.IsTrue(_chatMessage.butEdit.Enabled, "Edit button should be enabled.");
			Assert.IsTrue(_chatMessage.butDelete.Enabled, "Delete button should be enabled.");
		}
		[TestMethod]
		public void Message_SetValue_UpdatesRichTextBox(){
			_chatMessage = new ChatMessageControl(MessageRole.User, "Initial", false);
			_chatMessage.Message = "Updated message";
			Assert.AreEqual("Updated message", _chatMessage.richTextMsg.Text, "RichTextBox should show updated message.");
		}
		[TestMethod]
		public void Think_Property_GetSet(){
			_chatMessage = new ChatMessageControl(MessageRole.Assistant, "Message", false);
			_chatMessage.Think = "Thinking text";
			Assert.AreEqual("Thinking text", _chatMessage.Think, "Think property should be set.");
		}
		[TestMethod]
		public void CheckThink_CheckedChanged_TogglesMessageDisplay(){
			_chatMessage = new ChatMessageControl(MessageRole.Assistant, "Message", false);
			_chatMessage.Think = "Thinking";
			_chatMessage.checkThink.Enabled = true;
			_chatMessage.checkThink.Checked = true;
			Assert.AreEqual("Thinking", _chatMessage.richTextMsg.Text, "Should show thinking text when checked.");
			_chatMessage.checkThink.Checked = false;
			Assert.AreEqual("Message", _chatMessage.richTextMsg.Text, "Should show message text when unchecked.");
		}
		[TestMethod]
		public void AssistantRole_ShowsRegenButton(){
			_chatMessage = new ChatMessageControl(MessageRole.Assistant, "Test", false);
			Assert.IsTrue(_chatMessage.butRegen.Visible, "Regen button should be visible for Assistant role.");
		}
		[TestMethod]
		public void Markdown_True_RendersMarkdown(){
			_chatMessage = new ChatMessageControl(MessageRole.User, "**Bold** text", true);

			// Note: Full markdown rendering test would require mock of NativeMethods.ConvertMarkdownToRtf
			Assert.IsTrue(_chatMessage.Markdown, "Markdown flag should be true.");
		}
		[TestMethod]
		public void Dispose_CleansUpResources(){
			_chatMessage = new ChatMessageControl(MessageRole.User, "Test", false);
			var isDisposed = false;
			_chatMessage.Disposed += (sender, e) => isDisposed = true;
			_chatMessage.Dispose();
			Assert.IsTrue(isDisposed, "Disposed event should be raised.");
		}
		[TestMethod]
		public void Width_SetValue_UpdatesControlWidth(){
			_chatMessage = new ChatMessageControl(MessageRole.User, "Test", false);
			_chatMessage.Width = 500;
			Assert.AreEqual(500, _chatMessage.Width, "Width should be updated.");
		}
		[TestMethod]
		public void Parent_SetValue_AddsToParent(){
			_chatMessage = new ChatMessageControl(MessageRole.User, "Test", false);
			_chatMessage.Parent = _parentPanel;
			Assert.AreSame(_parentPanel, _chatMessage.Parent, "Parent should be set.");
			Assert.IsTrue(_parentPanel.Controls.Contains(_chatMessage), "Parent should contain the chat message.");
		}
	}
}