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
		private bool _adjusting;
		internal ChatMessage(bool user, string message, bool markdown){
			User = user;
			_markdown = markdown;
			InitializeComponent();
			label1.Text = user ? "User" : "Assistant";
			AppendText(message);
		}
		internal bool Markdown{
			get => _markdown;
			set{
				_markdown = value;
				SetText();
			}
		}
		internal void AppendText(string text){
			_msgBuilder.Append(text);
			_message = _msgBuilder.ToString();
			var thinking = ExtractThink(ref _message, out _think);
			if(!string.IsNullOrEmpty(_think) && checkThink.Visible == false) checkThink.Visible = true;
			if(thinking && !checkThink.Checked) checkThink.Checked = true;
			else if(!thinking && checkThink.Checked) checkThink.Checked = false;
			else SetText();
		}
		protected override void OnSizeChanged(EventArgs e){
			base.OnSizeChanged(null);
			if(!_adjusting) AdjustHeight();
		}
		private void RichTextMsg_TextChanged(object sender, EventArgs e){AdjustHeight();}
		private void CheckThink_CheckedChanged(object sender, EventArgs e){SetText();}
		private void AdjustHeight(){
			var p = richTextMsg.GetPositionFromCharIndex(richTextMsg.TextLength);
			_adjusting = true;
			Height = p.Y + richTextMsg.Font.Height + 32;
			_adjusting = false;
		}
		private unsafe string MarkdownToRtf(string markdown){
			var rtfOut = (byte*)0;
			var rtfLen = 0;
			NativeMethods.ConvertMarkdownToRtf(markdown, ref rtfOut, ref rtfLen);
			return Encoding.UTF8.GetString(rtfOut, rtfLen);
		}
		private void SetText(){
			if(checkThink.Checked){
				if(Markdown){
					richTextMsg.Rtf = MarkdownToRtf(_think);
				} else richTextMsg.Text = _think;
			} else if(Markdown){
				var newRtf = MarkdownToRtf(_message);
				richTextMsg.Rtf = newRtf;
			} else{ richTextMsg.Text = _message; }
		}
		private static bool ExtractThink(ref string message, out string think){
			var inCodeBlock = false;
			var capturingThink = false;
			var mainBuilder = new StringBuilder();
			var thinkBuilder = new StringBuilder();
			var lines = message.Replace("\r\n", "\n").Split('\n');
			foreach(var line in lines){
				var trimmed = line.Trim();
				if(trimmed == "```"){
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
	}
}