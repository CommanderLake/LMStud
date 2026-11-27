using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Forms;
namespace LMStud{
	internal enum MessageRole{
		User,
		Assistant,
		Tool
	}
	internal partial class ChatMessageControl : UserControl{
		private const int HeightOffset = 32;
		internal readonly MessageRole Role;
		private bool _autoScroll = true;
		private bool _editing;
		private bool _generating;
		private bool _heightUpdatePending;
		private bool _markdown;
		private bool _markdownLast;
		private string _message;
		private int _pendingHeight;
		private bool _pendingHeightAutoScroll;
		private string _think;
		internal string ApiContent;
		internal JsonNode ApiOutputItems{get; set;} = JsonNode.Missing;
		internal string ApiReplayKey;
		internal string ApiToolCallId;
		internal List<APIClient.ToolCall> ApiToolCalls;
		internal bool NativeBacked = true;
		internal int TTSPosition = 0;
		internal ChatMessageControl(MessageRole role, string message, bool markdown) : this(role, "", message, markdown){}
		internal ChatMessageControl(MessageRole role, string think, string message, bool markdown){
			InitializeComponent();
			Role = role;
			_think = think;
			_message = message;
			_markdown = markdown;
			labelRole.Text = role.ToString();
		}
		internal string ApiMessageContent => ApiContent ?? Message ?? "";
		internal MarkdownRenderControl MarkdownView => markdownView;
		internal bool Markdown{
			get => _markdown;
			set{
				if(Editing){ _markdownLast = value; } else{
					_markdown = value;
					_autoScroll = false;
					try{ RenderText(); } finally{ _autoScroll = true; }
				}
			}
		}
		internal string Think{
			get => _think;
			set{
				_think = value;
				RenderText();
			}
		}
		internal string Message{
			get => _message;
			set{
				_message = value;
				RenderText();
			}
		}
		internal bool Editing{
			get => _editing;
			set{
				if(_editing == value) return;
				if(value) _markdownLast = _markdown;
				_editing = value;
				if(!value) _markdown = _markdownLast;
				richTextMsg.ReadOnly = !value;
				butDelete.Enabled = !value;
				butEdit.Enabled = !value && NativeBacked;
				butEdit.Visible = !value && NativeBacked;
				butRegen.Enabled = !value && NativeBacked;
				butRegen.Visible = !value && NativeBacked;
				butCancelEdit.Visible = value;
				butCancelEdit.Enabled = value;
				butApplyEdit.Visible = value;
				butApplyEdit.Enabled = value;
				checkThink.Enabled = !value;
				_autoScroll = false;
				RenderText();
				_autoScroll = true;
			}
		}
		internal bool Generating{
			get => _generating;
			set{
				_generating = value;
				butApplyEdit.Enabled = !value;
				butCancelEdit.Enabled = !value;
				butDelete.Enabled = !value;
				butEdit.Enabled = !value && NativeBacked;
				butRegen.Enabled = !value && NativeBacked;
				checkThink.Enabled = !value;
				if(!value && renderTimer.Enabled) RenderText();
			}
		}
		private void ChatMessage_Load(object sender, EventArgs e){
			var hasReasoning = !string.IsNullOrEmpty(_think);
			checkThink.Visible = hasReasoning;
			if(_message.Length > 0 || hasReasoning) RenderText();
		}
		internal void SetRoleText(string role){labelRole.Text = role;}
		private void RichTextMsgOnContentsResized(object sender, ContentsResizedEventArgs e){
			if(markdownView.Visible) return;
			var contentHeight = e.NewRectangle.Height;
			var maximumContentHeight = Math.Max(18, (Parent?.ClientSize.Height ?? int.MaxValue) - 24);
			var capped = _editing && contentHeight > maximumContentHeight;
			richTextMsg.ScrollBars = capped ? RichTextBoxScrollBars.Vertical : RichTextBoxScrollBars.None;
			var newHeight = Math.Min(contentHeight, maximumContentHeight) + HeightOffset;
			if(newHeight != Height) ApplyHeight(newHeight);
		}
		private void MarkdownViewOnContentHeightChanged(object sender, EventArgs e){
			if(!markdownView.Visible) return;
			ApplyHeight(GetMarkdownControlHeight());
		}
		private int GetMarkdownControlHeight(){return markdownView.ContentHeight + panelMessage.Top + panelMessage.Height - panelMessage.ClientSize.Height;}
		private void ApplyHeight(int newHeight){
			if(IsDisposed || Disposing) return;
			_pendingHeight = newHeight;
			_pendingHeightAutoScroll = _autoScroll;
			if(_heightUpdatePending) return;
			if(!IsHandleCreated){
				ApplyPendingHeight();
				return;
			}
			_heightUpdatePending = true;
			try{ BeginInvoke(new MethodInvoker(ApplyPendingHeight)); } catch(ObjectDisposedException){ _heightUpdatePending = false; } catch(InvalidOperationException){ _heightUpdatePending = false; }
		}
		private void ApplyPendingHeight(){
			_heightUpdatePending = false;
			if(IsDisposed || Disposing) return;
			var newHeight = _pendingHeight;
			var autoScroll = _pendingHeightAutoScroll;
			if(Height == newHeight) return;
			Height = newHeight;
			if(autoScroll) (Parent as MyFlowLayoutPanel)?.ScrollToEnd();
		}
		internal void UpdateText(string think, string message, bool render){
			_think = think;
			_message = message;
			if(Role == MessageRole.User){
				if(render) RequestRender();
			} else{
				if(!string.IsNullOrEmpty(_think) && checkThink.Visible == false) checkThink.Visible = true;
				if(!string.IsNullOrEmpty(_think) && string.IsNullOrEmpty(_message) && !checkThink.Checked){ checkThink.Checked = true; } else if(!string.IsNullOrEmpty(_message) && checkThink.Checked){
					checkThink.Checked = false;
				} else{
					if(render) RequestRender();
				}
			}
		}
		private void RequestRender(){
			if(IsDisposed || Disposing) return;
			if(!IsHandleCreated){
				RenderText();
				return;
			}
			if(!renderTimer.Enabled) renderTimer.Start();
		}
		private void RenderTimer_Tick(object sender, EventArgs e){
			renderTimer.Stop();
			RenderText();
		}
		private void CheckThink_CheckedChanged(object sender, EventArgs e){RenderText();}
		private void RenderText(){
			renderTimer.Stop();
			var text = checkThink.Checked ? _think : _message;
			markdownView.Visible = !_editing;
			richTextMsg.Visible = _editing;
			if(_editing){
				if(richTextMsg.Text != text) richTextMsg.Text = text;
				return;
			}
			markdownView.SetContent(text, _markdown, false);
			ApplyHeight(GetMarkdownControlHeight());
		}
		private void RichTextMsgOnLinkClicked(object sender, LinkClickedEventArgs e){
			try{
				if(!Uri.TryCreate(e.LinkText, UriKind.Absolute, out var link)) return;
				var psi = new ProcessStartInfo(link.ToString());
				Process.Start(psi);
			} catch{}
		}
	}
}
