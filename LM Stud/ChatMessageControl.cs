//#define newMarkdown
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Text;
using System.Threading;
using System.Windows.Forms;
namespace LMStud{
	internal enum MessageRole{
		User,
		Assistant,
		Tool
	}
	internal partial class ChatMessageControl : UserControl{
		private readonly Font _font;
		internal readonly MessageRole Role;
		internal List<APIClient.ToolCall> ApiToolCalls;
		internal string ApiToolCallId;
		private bool _markdown;
		private bool _markdownLast;
		private string _message;
		private string _think;
		private bool _generating;
		private bool _editing;
		private bool _autoScroll = true;
		private const int HeightOffset = 32;
#if newMarkdown
		private readonly MarkdownRenderControl _markdownView;
#endif
		internal int TTSPosition = 0;
		internal ChatMessageControl(MessageRole role, string message, bool markdown):this(role, "", message, markdown){}
		internal ChatMessageControl(MessageRole role, string think, string message, bool markdown){
			InitializeComponent();
			_font = richTextMsg.Font;
#if newMarkdown
			_markdownView = new MarkdownRenderControl {
				Visible = false,
				Location = richTextMsg.Location,
				Size = richTextMsg.Size,
				Anchor = richTextMsg.Anchor,
				BackColor = richTextMsg.BackColor,
				ForeColor = richTextMsg.ForeColor,
				Font = new System.Drawing.Font("Segoe UI Symbol", richTextMsg.Font.Size, richTextMsg.Font.Style)
			};
			_markdownView.ContentHeightChanged += MarkdownViewOnContentHeightChanged;
			panel1.Controls.Add(_markdownView);
			_markdownView.BringToFront();
#endif
			Role = role;
			_think = think;
			_message = message;
			_markdown = markdown;
			labelRole.Text = role.ToString();
		}
		private void ChatMessage_Load(object sender, EventArgs e) {
			if(_message.Length > 0) UpdateText(_think, _message, true);
		}
		internal void SetRoleText(string role){labelRole.Text = role;}
		private void RichTextMsgOnContentsResized(object sender, ContentsResizedEventArgs e){
#if newMarkdown
			if(_markdownView.Visible) return;
#endif
			var newHeight = e.NewRectangle.Height + HeightOffset;
			if(newHeight != Height) ApplyHeight(newHeight);
		}
#if newMarkdown
		private void MarkdownViewOnContentHeightChanged(object sender, EventArgs e){
			if(!_markdownView.Visible) return;
			ApplyHeight(_markdownView.AutoScrollMinSize.Height + 24);
		}
#endif
		private void ApplyHeight(int newHeight){
			ThreadPool.QueueUserWorkItem(o => {//Layout issue workaround
				try{
					Invoke(new MethodInvoker(() => {
						if(Height == newHeight) return;
						Height = newHeight;
						if((bool)o) ((MyFlowLayoutPanel)Parent).ScrollToEnd();
					}));
				} catch(ObjectDisposedException){} catch(InvalidOperationException){}
			}, _autoScroll);
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
						if(_markdown) return;
						richTextMsg.SelectAll();
						richTextMsg.SelectionFont = _font;
						richTextMsg.Select(0, 0);
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
				richTextMsg.ReadOnly = !value;
				butDelete.Enabled = !value;
				butEdit.Enabled = !value;
				butEdit.Visible = !value;
				butRegen.Enabled = !value;
				butRegen.Visible = !value;
				butCancelEdit.Visible = value;
				butCancelEdit.Enabled = value;
				butApplyEdit.Visible = value;
				butApplyEdit.Enabled = value;
				checkThink.Enabled = !value;
				if(value){
					_markdownLast = _markdown;
					Markdown = false;
				} else _markdown = _markdownLast;
				_editing = value;
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
				butEdit.Enabled = !value;
				butRegen.Enabled = !value;
				checkThink.Enabled = !value;
			}
		}
		internal void UpdateText(string think, string message, bool render){
			_think = think;
			_message = message;
			if(Role == MessageRole.User){
				if(render) RenderText();
			} else{
				if(!string.IsNullOrEmpty(_think) && checkThink.Visible == false) checkThink.Visible = true;
				if(!string.IsNullOrEmpty(_think) && string.IsNullOrEmpty(_message) && !checkThink.Checked){
					checkThink.Checked = true;
				}else if(!string.IsNullOrEmpty(_message) && checkThink.Checked){
					checkThink.Checked = false;
				}else{
					if(render) RenderText();
				}
			}
		}
		private void CheckThink_CheckedChanged(object sender, EventArgs e){RenderText();}
		private unsafe string MarkdownToRtf(string markdown){
			var rtfOut = (byte*)0;
			var rtfLen = 0;
			NativeMethods.ConvertMarkdownToRtf(markdown, ref rtfOut, ref rtfLen);
			return Encoding.ASCII.GetString(rtfOut, rtfLen);
		}
		private void RenderText(){
#if newMarkdown
			var text = checkThink.Checked ? _think : _message;
			var useMarkdownView = _markdown && !_editing;
			_markdownView.Visible = useMarkdownView;
			richTextMsg.Visible = !useMarkdownView;
			if(useMarkdownView){
				_markdownView.MarkdownText = text;
				ApplyHeight(_markdownView.AutoScrollMinSize.Height + 32);
				return;
			}
#endif
			if(checkThink.Checked){
				if(_markdown) richTextMsg.Rtf = MarkdownToRtf(_think);
				else richTextMsg.Text = _think;
			} else{
				if(_markdown) richTextMsg.Rtf = MarkdownToRtf(_message);
				else richTextMsg.Text = _message;
			}
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