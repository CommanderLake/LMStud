﻿using System;
using System.Text;
using System.Threading;
using System.Windows.Forms;
namespace LMStud{
	internal enum MessageRole{
		User,
		Assistant,
		Tool
	}
	internal partial class ChatMessage : UserControl{
		internal readonly MessageRole Role;
		private bool _markdown;
		private string _message;
		private string _think = "";
		private bool _generating;
		private bool _editing;
		internal int TTSPosition = 0;
		internal ChatMessage(MessageRole role, string message, bool markdown){
			Role = role;
			_markdown = markdown;
			InitializeComponent();
			richTextMsg.ContentsResized += RichTextMsgOnContentsResized;
			label1.Text = role.ToString();
			_message = message;
		}
		private void ChatMessage_Load(object sender, EventArgs e) {
			if(_message.Length > 0){
				UpdateText("", _message, true);
			}
		}
		internal void SetRoleText(string role){label1.Text = role;}
		private void RichTextMsgOnContentsResized(object sender, ContentsResizedEventArgs e){
			ThreadPool.QueueUserWorkItem(o => {//Layout issue workaround
				try{
					Invoke(new MethodInvoker(() => {
						var newHeight = e.NewRectangle.Height + 32;
						if(Height == newHeight) return;
						Height = newHeight;
					}));
				} catch(ObjectDisposedException){}
			});
		}
		internal bool Markdown{
			get => _markdown;
			set{
				_markdown = value;
				RenderText();
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
				_editing = value;
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
				}
				else if(!string.IsNullOrEmpty(_message) && checkThink.Checked){
					checkThink.Checked = false;
				}
				else{
					if(render) RenderText();
				}
			}
			((MyFlowLayoutPanel)Parent).ScrollToEnd();
		}
		private void CheckThink_CheckedChanged(object sender, EventArgs e){RenderText();}
		private unsafe string MarkdownToRtf(string markdown){
			var rtfOut = (byte*)0;
			var rtfLen = 0;
			NativeMethods.ConvertMarkdownToRtf(markdown, ref rtfOut, ref rtfLen);
			return Encoding.ASCII.GetString(rtfOut, rtfLen);
		}
		private void RenderText(){
			if(checkThink.Checked){
				if(_markdown) richTextMsg.Rtf = MarkdownToRtf(_think);
				else richTextMsg.Text = _think;
			} else{
				if(_markdown) richTextMsg.Rtf = MarkdownToRtf(_message);
				else richTextMsg.Text = _message;
			}
		}
	}
}