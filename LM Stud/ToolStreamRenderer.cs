using System;
using System.Text;
using System.Windows.Forms;
using LMStud.Properties;
namespace LMStud{
	internal sealed class ToolStreamRenderer{
		private readonly object _sync = new object();
		private readonly StringBuilder _content = new StringBuilder();
		private readonly string _toolCallId;
		private bool _completed;
		private ChatMessageControl _message;
		internal ToolStreamRenderer(string toolCallId){ _toolCallId = toolCallId; }
		internal bool HasContent{
			get{ lock(_sync){ return _content.Length > 0; } }
		}
		internal void Append(string delta){
			if(string.IsNullOrEmpty(delta)) return;
			string snapshot;
			lock(_sync){
				if(_completed) return;
				_content.Append(delta);
				snapshot = _content.ToString();
			}
			try{ Generation.MainForm.BeginInvoke(new MethodInvoker(() => Update(snapshot, false))); }
			catch(ObjectDisposedException){} catch(InvalidOperationException){}
		}
		internal void Complete(string text){
			string displayText;
			var rawText = text ?? "";
			lock(_sync){
				_completed = true;
				displayText = string.IsNullOrWhiteSpace(rawText) && _content.Length > 0 ? _content.ToString() : rawText;
			}
			try{ Generation.MainForm.Invoke(new MethodInvoker(() => Update(displayText, true, rawText))); }
			catch(ObjectDisposedException){} catch(InvalidOperationException){}
		}
		private void Update(string text, bool final, string rawText = null){
			lock(_sync){ if(!final && _completed) return; }
			var displayText = final ? NativeMethods.FormatToolOutputDisplay(text) : text ?? "";
			if(_message == null) _message = Generation.MainForm.AddMessage(MessageRole.Tool, "", displayText, null, _toolCallId);
			else _message.UpdateText("", displayText, true);
			if(final) _message.ApiContent = rawText ?? text ?? "";
			_message.SetRoleText(Resources.Tool_Output);
		}
	}
}
