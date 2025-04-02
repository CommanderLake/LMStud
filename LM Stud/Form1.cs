using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using LMStud.Properties;
namespace LMStud{
	internal partial class Form1 : Form{
		private static Form1 _this;
		private static NativeMethods.TokenCallback _tokenCallback;
		private static int _callbackCount;
		private static int _callbackTot;
		private static readonly Stopwatch Sw = new Stopwatch();
		private static readonly Stopwatch SwTot = new Stopwatch();
		private volatile bool _generating;
		private readonly List<ChatMessage> _chatMessages = new List<ChatMessage>();
		private ChatMessage _cntAssMsg;
		private int _tokenCount;
		internal Form1(){
			_this = this;
			NativeMethods.SetOMPEnv();
			InitializeComponent();
			LoadConfig();
			SetConfig();
			PopulateModels();
		}
		private void Form1_Load(object sender, EventArgs e) {
			if(!Settings.Default.LoadAuto) return;
			checkLoadAuto.Checked = true;
			ThreadPool.QueueUserWorkItem(o => {
				while(_populating) Thread.Sleep(10);
				for(var i = 0; i < _models.Count; i++) {
					var model = _models[i];
					if(model.FilePath == Settings.Default.LastModel)
						LoadModel(i, true);
				}
			});
		}
		private void Form1_FormClosing(object sender, FormClosingEventArgs e){NativeMethods.StopGeneration();}
		private void Form1_KeyDown(object sender, KeyEventArgs e){
			if(e.KeyCode != Keys.Escape) return;
			NativeMethods.StopGeneration();
		}
		private void CheckMarkdown_CheckedChanged(object sender, EventArgs e){
			foreach(var message in _chatMessages) message.Markdown = checkMarkdown.Checked;
		}
		private void ButCodeBlock_Click(object sender, EventArgs e) {
			textInput.Paste("```\r\n\r\n```");
		}
		private void ButGen_Click(object sender, EventArgs e){Generate(false);}
		private void ButReset_Click(object sender, EventArgs e){
			NativeMethods.ResetChat();
			foreach(var message in _chatMessages) message.Dispose();
			_chatMessages.Clear();
			_tokenCount = 0;
			labelTokens.Text = "0 Tokens";
		}
		private void TextInput_KeyDown(object sender, KeyEventArgs e){
			if(e.KeyCode != Keys.Enter || e.Control || e.Shift || !butGen.Enabled) return;
			e.SuppressKeyPress = true;
			Generate(false);
		}
		private void PanelChat_Layout(object sender, LayoutEventArgs e){
			panelChat.SuspendLayout();
			try{
				for(var i = 0; i < panelChat.Controls.Count; ++i) panelChat.Controls[i].Width = panelChat.ClientSize.Width;
			} finally{ panelChat.ResumeLayout(false); }
		}
		private void MsgButDeleteOnClick(ChatMessage cm){
			if(_generating) return;
			var id = _chatMessages.IndexOf(cm);
			NativeMethods.RemoveMessageAt(id);
			_chatMessages[id].Dispose();
			_chatMessages.RemoveAt(id);
		}
		private void MsgButRegenOnClick(ChatMessage cm){
			if(!_loaded || _generating) return;
			var id = _chatMessages.IndexOf(cm);
			if(_chatMessages[id].User) ++id;
			if(id < _chatMessages.Count){
				var count = _chatMessages.Count - id;
				for(var i = _chatMessages.Count - 1; i >= id; i--) _chatMessages[i].Dispose();
				_chatMessages.RemoveRange(id, count);
				NativeMethods.RemoveMessagesStartingAt(id);
			}
			Generate(true);
		}
		private void MsgButEditOnClick(ChatMessage cm){
			if(_generating || cm.Editing) return;
			foreach(var msg in _chatMessages.Where(msg => msg != cm && msg.Editing)) MsgButEditCancelOnClick(msg);
			cm.Editing = true;
			cm.richTextMsg.Focus();
		}
		private void MsgButEditCancelOnClick(ChatMessage cm){
			if(_generating || !cm.Editing) return;
			cm.Editing = false;
			cm.Markdown = checkMarkdown.Checked;
		}
		private void MsgButEditApplyOnClick(ChatMessage cm){
			if(_generating || !cm.Editing) return;
			if(cm.checkThink.Checked) cm.Think = cm.richTextMsg.Text;
			else cm.Message = cm.richTextMsg.Text;
			NativeMethods.SetMessageAt(_chatMessages.IndexOf(cm), cm.Content);
			cm.Editing = false;
			cm.Markdown = checkMarkdown.Checked;
		}
		private ChatMessage AddMessage(bool user, string message){
			var cm = new ChatMessage(user, message, checkMarkdown.Checked);
			cm.Width = panelChat.ClientSize.Width;
			cm.butDelete.Click += (o, args) => MsgButDeleteOnClick(cm);
			cm.butRegen.Click += (o, args) => MsgButRegenOnClick(cm);
			cm.butEdit.Click += (o, args) => MsgButEditOnClick(cm);
			cm.butCancelEdit.Click += (o, args) => MsgButEditCancelOnClick(cm);
			cm.butApplyEdit.Click += (o, args) => MsgButEditApplyOnClick(cm);
			panelChat.Controls.Add(cm);
			_chatMessages.Add(cm);
			return cm;
		}
		private void Generate(bool regenerating){
			if(!_loaded) return;
			if(!_generating){
				_generating = true;
				foreach(var msg in _chatMessages.Where(msg => msg.Editing)) MsgButEditCancelOnClick(msg);
				butGen.Text = "Stop";
				butReset.Enabled = false;
				if(!regenerating){
					var msg = textInput.Text.Trim();
					AddMessage(true, msg);
					NativeMethods.AddMessage(true, msg);
					NativeMethods.SendMessage(panelChat.Handle, NativeMethods.WM_VSCROLL, (IntPtr)NativeMethods.SB_BOTTOM, IntPtr.Zero);
				}
				_cntAssMsg = AddMessage(false, "");
				ThreadPool.QueueUserWorkItem(o => {
					SwTot.Restart();
					Sw.Restart();
					NativeMethods.Generate(_nGen);
					SwTot.Stop();
					Sw.Stop();
					Invoke(new MethodInvoker(() => {
						var elapsed = SwTot.Elapsed.TotalSeconds;
						if(_callbackTot > 0 && elapsed > 0.0){
							var callsPerSecond = _callbackTot/elapsed;
							labelTPS.Text = $"{callsPerSecond:F2} Tok/s";
							_callbackTot = 0;
							SwTot.Reset();
							Sw.Reset();
						}
						butGen.Text = "Generate";
						butReset.Enabled = true;
						_generating = false;
					}));
				});
			} else{ NativeMethods.StopGeneration(); }
		}
		private static unsafe void TokenCallback(byte* tokenPtr, int strLen, int tokenCount){
			if(_this.IsDisposed) return;
			var token = Encoding.UTF8.GetString(tokenPtr, strLen);
			var thisform = _this;
			_this.BeginInvoke((MethodInvoker)(() => {
				if(thisform.IsDisposed) return;
				++_callbackCount;
				++_callbackTot;
				var elapsed = Sw.Elapsed.TotalSeconds;
				if(elapsed >= 1.0){
					var callsPerSecond = _callbackCount/elapsed;
					_this.labelTPS.Text = $"{callsPerSecond:F2} Tok/s";
					_callbackCount = 0;
					Sw.Restart();
				}
				_this._cntAssMsg.AppendText(token);
				_this.labelTokens.Text = tokenCount + "/" + _this._cntCtxMax + " Tokens";
				_this._tokenCount = tokenCount;
				NativeMethods.SendMessage(_this.panelChat.Handle, NativeMethods.WM_VSCROLL, (IntPtr)NativeMethods.SB_BOTTOM, IntPtr.Zero);
			}));
		}
	}
}