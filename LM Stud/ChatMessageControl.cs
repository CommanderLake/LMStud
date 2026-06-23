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
		internal readonly MessageRole Role;
		internal List<APIClient.ToolCall> ApiToolCalls;
		internal string ApiToolCallId;
		internal string ApiContent;
		internal bool NativeBacked = true;
		internal string ApiMessageContent => ApiContent ?? Message ?? "";
		private bool _markdown;
		private bool _markdownLast;
		private string _message;
		private string _think;
		private bool _generating;
		private bool _editing;
		private bool _autoScroll = true;
		private bool _heightUpdatePending;
		private bool _pendingHeightAutoScroll;
		private int _pendingHeight;
		private readonly List<ChatMessageContinuationControl> _continuations = new List<ChatMessageContinuationControl>();
		private const int HeightOffset = 32;
		internal int TTSPosition = 0;
		internal ChatMessageControl(MessageRole role, string message, bool markdown):this(role, "", message, markdown){}
		internal ChatMessageControl(MessageRole role, string think, string message, bool markdown){
			InitializeComponent();
			Role = role;
			_think = think;
			_message = message;
			_markdown = markdown;
			labelRole.Text = role.ToString();
		}
		internal MarkdownRenderControl MarkdownView => markdownView;
		private void ChatMessageControl_Disposed(object sender, EventArgs e){ ClearContinuations(); }
		private void ChatMessage_Load(object sender, EventArgs e) {
			if(_message.Length > 0) RenderText();
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
		private int GetMarkdownControlHeight(){
			return markdownView.ContentHeight + panelMessage.Top + panelMessage.Height - panelMessage.ClientSize.Height;
		}
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
			try{
				BeginInvoke(new MethodInvoker(ApplyPendingHeight));
			} catch(ObjectDisposedException){ _heightUpdatePending = false; }
			catch(InvalidOperationException){ _heightUpdatePending = false; }
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
		internal bool Markdown{
			get => _markdown;
			set{
				if(Editing) _markdownLast = value;
				else{
					_markdown = value;
					_autoScroll = false;
					try{
						RenderText();
					} finally{ _autoScroll = true; }
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
		internal void UpdateText(string think, string message, bool render){
			_think = think;
			_message = message;
			if(Role == MessageRole.User){
				if(render) RequestRender();
			} else{
				if(!string.IsNullOrEmpty(_think) && checkThink.Visible == false) checkThink.Visible = true;
				if(!string.IsNullOrEmpty(_think) && string.IsNullOrEmpty(_message) && !checkThink.Checked){
					checkThink.Checked = true;
				}else if(!string.IsNullOrEmpty(_message) && checkThink.Checked){
					checkThink.Checked = false;
				}else{
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
				ClearContinuations();
				return;
			}
			var layoutWidth = Math.Max(10, markdownView.ClientSize.Width - 8);
			markdownView.SetContent(text, _markdown, false);
			List<string> chunks;
			if(MarkdownMessageChunker.RequiresSplit(text, markdownView.ContentHeight, _markdown)){
				chunks = MarkdownMessageChunker.SplitOversized(text, markdownView.Font, layoutWidth,
					markdownView.ForeColor, markdownView.BackColor, _markdown);
				markdownView.SetContent(chunks[0], _markdown, false);
			}else chunks = new List<string>{text};
			UpdateContinuations(chunks);
			ApplyHeight(GetMarkdownControlHeight());
		}
		private void UpdateContinuations(List<string> chunks){
			var required = Math.Max(0, chunks.Count - 1);
			while(_continuations.Count > required){
				var last = _continuations[_continuations.Count - 1];
				_continuations.RemoveAt(_continuations.Count - 1);
				last.Dispose();
			}
			while(_continuations.Count < required){
				var continuation = new ChatMessageContinuationControl(markdownView.Font, markdownView.BackColor, markdownView.ForeColor);
				continuation.ContentHeightApplied += ContinuationOnContentHeightApplied;
				_continuations.Add(continuation);
			}
			for(var i = 0; i < required; i++) _continuations[i].SetContent(chunks[i + 1], _markdown);
			AttachContinuations();
		}
		private void ContinuationOnContentHeightApplied(object sender, EventArgs e){
			if(_autoScroll) (Parent as MyFlowLayoutPanel)?.ScrollToEnd();
		}
		private void AttachContinuations(){
			if(Parent == null || _continuations.Count == 0) return;
			var ownerIndex = Parent.Controls.GetChildIndex(this);
			for(var i = 0; i < _continuations.Count; i++){
				var continuation = _continuations[i];
				if(continuation.Parent != Parent) continuation.Parent = Parent;
				continuation.Width = Width;
				Parent.Controls.SetChildIndex(continuation, Math.Min(ownerIndex + i + 1, Parent.Controls.Count - 1));
			}
			if(_autoScroll) (Parent as MyFlowLayoutPanel)?.ScrollToEnd();
		}
		private void ClearContinuations(){
			foreach(var continuation in _continuations) continuation.Dispose();
			_continuations.Clear();
		}
		protected override void OnParentChanged(EventArgs e){
			base.OnParentChanged(e);
			AttachContinuations();
		}
		protected override void OnSizeChanged(EventArgs e){
			base.OnSizeChanged(e);
			foreach(var continuation in _continuations)
				if(continuation.Width != Width)
					continuation.Width = Width;
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
