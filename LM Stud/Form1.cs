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
		private int _callbackCount;
		private int _callbackTot;
		private readonly Stopwatch SwRate = new Stopwatch();
		private readonly Stopwatch SwPreGen = new Stopwatch();
		private readonly Stopwatch SwTot = new Stopwatch();
		private volatile bool _generating;
		private readonly List<ChatMessage> _chatMessages = new List<ChatMessage>();
		private ChatMessage _cntAssMsg;
		private int _tokenCount;
		internal Form1(){
			_this = this;
			NativeMethods.SetOMPEnv();
			InitializeComponent();
			SetToolTips();
			LoadConfig();
			SetConfig();
			PopulateModels();
		}
		private void SetToolTips(){
			toolTip1.SetToolTip(textInstruction, "Tell the AI who or what to be, how to respond, or provide initial context.");
			toolTip1.SetToolTip(textModelsPath, "Path to the folder containing your .gguf model files.");
			toolTip1.SetToolTip(numCtxSize, "Context size (max tokens). Higher values improve memory but use more RAM.");
			toolTip1.SetToolTip(numGPULayers, "Number of layers to offload to GPU. More layers improve performance but increase GPU memory usage.");
			toolTip1.SetToolTip(numTemp, "Temperature controls randomness. Lower values make responses more deterministic; higher values produce more creative outputs.");
			toolTip1.SetToolTip(numNGen, "Number of tokens to generate per response (max length of the AI's reply).");
			toolTip1.SetToolTip(comboNUMAStrat, "NUMA (Non-Uniform Memory Access) strategy. Adjust if using multi-socket CPUs or specific memory configurations.");
			toolTip1.SetToolTip(numRepPen, "Repetition penalty reduces repetitive outputs. Higher values strongly discourage repeated phrases.");
			toolTip1.SetToolTip(numTopK, "Top-K sampling: limits token choice to the K most probable tokens, improving coherency.");
			toolTip1.SetToolTip(numTopP, "Top-P sampling: controls diversity by choosing from the smallest possible set of tokens whose cumulative probability exceeds this threshold.");
			toolTip1.SetToolTip(numBatchSize, "Batch size for processing tokens during generation. Higher values can improve performance at the cost of higher RAM usage.");
			toolTip1.SetToolTip(groupCPUParams, "Parameters controlling text generation on CPU.");
			toolTip1.SetToolTip(numThreads, "CPU threads for token generation. Typically, around 75% of your physical cores is optimal to prevent oversaturating the memory controller.");
			toolTip1.SetToolTip(checkStrictCPU, "Sets thread affinities on supported backends to isolate threads to specific logical cores.");
			toolTip1.SetToolTip(groupCPUParamsBatch, "Parameters for the pre-generation step (initial preparation before generation starts).");
			toolTip1.SetToolTip(numThreadsBatch, "CPU threads used in pre-generation.");
			toolTip1.SetToolTip(checkStrictCPUBatch, "Sets thread affinities on supported backends to isolate threads to specific logical cores pre-generation.");
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
					SwPreGen.Restart();
					SwRate.Restart();
					NativeMethods.Generate(_nGen);
					SwTot.Stop();
					SwRate.Stop();
					Invoke(new MethodInvoker(() => {
						var elapsed = SwTot.Elapsed.TotalSeconds;
						if(_callbackTot > 0 && elapsed > 0.0){
							var callsPerSecond = _callbackTot/elapsed;
							labelTPS.Text = $"{callsPerSecond:F2} Tok/s";
							_callbackTot = 0;
							SwTot.Reset();
							SwRate.Reset();
						}
						butGen.Text = "Generate";
						butReset.Enabled = true;
						_generating = false;
					}));
				});
			} else{ NativeMethods.StopGeneration(); }
		}
		private static unsafe void TokenCallback(byte* tokenPtr, int strLen, int tokenCount){
			_this.SwPreGen.Stop();
			++_this._callbackCount;
			++_this._callbackTot;
			var elapsed = _this.SwRate.Elapsed.TotalSeconds;
			if(elapsed >= 1.0) _this.SwRate.Restart();
			var initElapsed = _this.SwPreGen.Elapsed.TotalSeconds;
			var token = Encoding.UTF8.GetString(tokenPtr, strLen);
			var thisform = _this;
			if(_this.IsDisposed) return;
			_this.BeginInvoke((MethodInvoker)(() => {
				if(thisform.IsDisposed) return;
				_this.labelPreGen.Text = "Pre-generation time: " + initElapsed + " s";
				if(elapsed >= 1.0){
					var callsPerSecond = _this._callbackCount/elapsed;
					_this.labelTPS.Text = $"{callsPerSecond:F2} Tok/s";
					_this._callbackCount = 0;
				}
				_this._cntAssMsg.AppendText(token);
				_this.labelTokens.Text = tokenCount + "/" + _this._cntCtxMax + " Tokens";
				_this._tokenCount = tokenCount;
				NativeMethods.SendMessage(_this.panelChat.Handle, NativeMethods.WM_VSCROLL, (IntPtr)NativeMethods.SB_BOTTOM, IntPtr.Zero);
			}));
		}
	}
}