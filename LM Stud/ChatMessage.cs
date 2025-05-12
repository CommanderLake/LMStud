using System;
using System.Text;
using System.Windows.Forms;
namespace LMStud{
	internal partial class ChatMessage : UserControl{
		private readonly StringBuilder _msgBuilder = new StringBuilder();
		internal readonly bool User;
		private bool _markdown;
		private string _message;
		private string _think;
		private bool _generating;
		private bool _editing;
		internal ChatMessage(bool user, string message, bool markdown){
			User = user;
			_markdown = markdown;
			InitializeComponent();
			richTextMsg.ContentsResized += RichTextMsgOnContentsResized;
			label1.Text = user ? "User" : "Assistant";
			AppendText(message, true);
		}
		private void RichTextMsgOnContentsResized(object sender, ContentsResizedEventArgs e){
			Height = e.NewRectangle.Height + 32;
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
				RebuildMsgBuilder();
				RenderText();
			}
		}
		internal string Message{
			get => _message;
			set{
				_message = value;
				RebuildMsgBuilder();
				RenderText();
			}
		}
		internal string Content => _msgBuilder.ToString();
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
		internal void AppendText(string text, bool render){
			if(User){
				_msgBuilder.Append(text);
				_message = _msgBuilder.ToString();
				if(render) RenderText();
			} else{
				_msgBuilder.Append(text);
				_message = _msgBuilder.ToString();
				var thinking = ExtractThink(ref _message, out _think);
				if(!string.IsNullOrEmpty(_think) && checkThink.Visible == false) checkThink.Visible = true;
				if(thinking && !checkThink.Checked) checkThink.Checked = true;
				else if(!thinking && checkThink.Checked) checkThink.Checked = false;
				else if(render) RenderText();
			}
		}
		private void CheckThink_CheckedChanged(object sender, EventArgs e){RenderText();}
		private unsafe string MarkdownToRtf(string markdown){
			var rtfOut = (byte*)0;
			var rtfLen = 0;
			NativeMethods.ConvertMarkdownToRtf(markdown, ref rtfOut, ref rtfLen);
			return Encoding.ASCII.GetString(rtfOut, rtfLen);
		}
		internal void RenderText(){
			if(checkThink.Checked){
				if(_markdown) richTextMsg.Rtf = MarkdownToRtf(_think);
				else richTextMsg.Text = _think;
			} else{
				if(_markdown) richTextMsg.Rtf = MarkdownToRtf(_message);
				else richTextMsg.Text = _message;
			}
		}
		private static bool ExtractThink(ref string message, out string think){
			var inCodeBlock = false;
			var capturingThink = false;
			var mainBuilder = new StringBuilder();
			var thinkBuilder = new StringBuilder();
			var lines = message.Replace("\r\n", "\n").Split('\n');
			foreach(var line in lines){
				var trimmed = line.Trim();
				if(trimmed.StartsWith("```")){
					inCodeBlock = !inCodeBlock;
					if(!capturingThink){
						if(mainBuilder.Length > 0) mainBuilder.AppendLine();
						mainBuilder.Append(line);
					}
					continue;
				}
				if(!inCodeBlock && !capturingThink && trimmed.Equals("<think>", StringComparison.OrdinalIgnoreCase)){
					capturingThink = true;
					continue;
				}
				if(capturingThink && trimmed.Equals("</think>", StringComparison.OrdinalIgnoreCase)){
					capturingThink = false;
					continue;
				}
				if(capturingThink){
					if(thinkBuilder.Length > 0) thinkBuilder.AppendLine();
					thinkBuilder.Append(line);
				} else{
					if(mainBuilder.Length > 0) mainBuilder.AppendLine();
					mainBuilder.Append(line);
				}
			}
			think = thinkBuilder.ToString();
			message = mainBuilder.ToString();
			return capturingThink;
		}
		private void RebuildMsgBuilder(){
			_msgBuilder.Clear();
			if(!User && !string.IsNullOrEmpty(_think)){
				_msgBuilder.AppendLine("<think>");
				_msgBuilder.AppendLine(_think);
				_msgBuilder.AppendLine("</think>");
			}
			if(!string.IsNullOrEmpty(_message)) _msgBuilder.Append(_message);
		}
	}
}