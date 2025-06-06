using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Speech.Synthesis;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using LMStud.Properties;
namespace LMStud{
	internal partial class Form1 : Form{
		private static Form1 _this;
		private static NativeMethods.TokenCallback _tokenCallback;
		private static NativeMethods.WhisperCallback _whisperCallback;
		private int _msgTokenCount;
		private readonly Stopwatch _swRate = new Stopwatch();
		private readonly Stopwatch _swTot = new Stopwatch();
		private volatile bool _generating;
		private volatile bool _rendering;
		private readonly List<ChatMessage> _chatMessages = new List<ChatMessage>();
		private ChatMessage _cntAssMsg;
		private readonly StringBuilder _speechBuffer = new StringBuilder();
		private bool _whisperInited;
		private bool _first = true;
		private readonly SpeechSynthesizer _tts = new SpeechSynthesizer();
		internal Form1(){
			_this = this;
			InitializeComponent();
			Icon = Resources.LM_Stud_256;
			SetToolTips();
			LoadConfig();
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
			toolTip1.SetToolTip(checkMMap, "Map the model file to memory for on-demand loading, may improve model load times.");
			toolTip1.SetToolTip(checkMLock, "Lock the model in RAM.");
			toolTip1.SetToolTip(checkStrictCPU, "Sets thread affinities on supported backends to isolate threads to specific logical cores.");
			toolTip1.SetToolTip(groupCPUParamsBatch, "Parameters for the pre-generation step (batch preparation).");
			toolTip1.SetToolTip(numThreadsBatch, "CPU threads used for batch preparation.");
			toolTip1.SetToolTip(checkStrictCPUBatch, "Sets thread affinities on supported backends to isolate threads to specific logical cores for batch preparation.");
			toolTip1.SetToolTip(numVadThreshold, "Voice Activity Detection, higher values increase sensitivity to speech (0.1-1.0).");
			toolTip1.SetToolTip(numFreqThreshold, "High-pass filter cutoff frequency. Higher values reduce background noise.");
			toolTip1.SetToolTip(checkSpeak, "Speak the generated responses using the computers default voice.");
			toolTip1.SetToolTip(checkVoiceInput, "An intermediate check state (filled in) means it will transcribe spoken words without generating, when checked it will automatically generate.");
		}
		private void Form1_Load(object sender, EventArgs e) {
			NativeMethods.CurlGlobalInit();
			PopulateModels();
			PopulateWhisperModels();
			NativeMethods.BackendInit();
			if(!Settings.Default.LoadAuto) return;
			checkLoadAuto.Checked = true;
			ThreadPool.QueueUserWorkItem(o => {
				while(_populating) Thread.Sleep(10);
				for(var i = 0; i < _models.Count; i++) {
					var model = _models[i];
					if(model.FilePath != Settings.Default.LastModel) continue;
					Invoke(new MethodInvoker(() => {LoadModel(i, true);}));
					break;
				}
			});
		}
		private void Form1_FormClosing(object sender, FormClosingEventArgs e){
			if(_generating) NativeMethods.StopGeneration();
			if(_whisperInited){
				NativeMethods.StopSpeechTranscription();
				NativeMethods.UnloadWhisperModel();
			}
			NativeMethods.CurlGlobalCleanup();
		}
		private void Form1_KeyDown(object sender, KeyEventArgs e){
			if(e.KeyCode != Keys.Escape) return;
			_tts.SpeakAsyncCancelAll();
			if(!_generating) return;
			NativeMethods.StopGeneration();
		}
		private void ButCodeBlock_Click(object sender, EventArgs e) {
			textInput.Paste("```\r\n\r\n```");
		}
		private void CheckMarkdown_CheckedChanged(object sender, EventArgs e){
			foreach(var message in _chatMessages) message.Markdown = checkMarkdown.Checked;
		}
		private void CheckVoiceInput_CheckedChanged(object sender, EventArgs e){
			if(checkVoiceInput.CheckState != CheckState.Unchecked){
				if(_whisperModelIndex < 0 || !File.Exists(_whisperModels[_whisperModelIndex])){
					MessageBox.Show(this, "Invalid whisper model selection", "LM Stud Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
					tabControl1.SelectTab(1);
					checkVoiceInput.Checked = false;
					return;
				}
				if(_modelLoaded){
					if(!_whisperInited){
						_whisperInited = NativeMethods.LoadWhisperModel(_whisperModels[_whisperModelIndex], _nThreads, _whisperUseGPU);
						if(_whisperInited){
							_whisperCallback = WhisperCallback;
							NativeMethods.SetWhisperCallback(_whisperCallback);
						} else{
							MessageBox.Show(this, "Error initialising whisper", "LM Stud Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
							checkVoiceInput.Checked = false;
							return;
						}
					}
					if(NativeMethods.StartSpeechTranscription()) return;
					MessageBox.Show(this, "Error starting whisper transcription", "LM Stud Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
					checkVoiceInput.Checked = false;
				} else{
					MessageBox.Show(this, "Load a model on the Models tab first", "LM Stud", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
					checkVoiceInput.Checked = false;
				}
			} else{
				NativeMethods.StopSpeechTranscription();
				_whisperInited = false;
			}
		}
		private void CheckSpeak_CheckedChanged(object sender, EventArgs e) {
			UpdateSetting(ref _speak, checkSpeak.Checked, value => {Settings.Default.Speak = value;});
			Settings.Default.Save();
		}
		private void ButGen_Click(object sender, EventArgs e){
			if(_generating) NativeMethods.StopGeneration();
			else Generate(false);
		}
		private void ButReset_Click(object sender, EventArgs e){
			NativeMethods.ResetChat();
			panelChat.SuspendLayout();
			foreach(var message in _chatMessages) message.Dispose();
			panelChat.ResumeLayout();
			_chatMessages.Clear();
			labelTokens.Text = "0 Tokens";
		}
		private void TextInput_KeyDown(object sender, KeyEventArgs e){
			if(e.KeyCode != Keys.Enter || e.Control || e.Shift || !butGen.Enabled) return;
			e.SuppressKeyPress = true;
			ButGen_Click(null, null);
		}
		private void PanelChat_Layout(object sender, LayoutEventArgs e){
			panelChat.SuspendLayout();
			try{
				for(var i = 0; i < panelChat.Controls.Count; ++i) panelChat.Controls[i].Width = panelChat.ClientSize.Width;
			} finally{ panelChat.ResumeLayout(true); }
		}
		private void MsgButDeleteOnClick(ChatMessage cm){
			if(_generating) return;
			var id = _chatMessages.IndexOf(cm);
			NativeMethods.RemoveMessageAt(id);
			_chatMessages[id].Dispose();
			_chatMessages.RemoveAt(id);
		}
		private void MsgButRegenOnClick(ChatMessage cm){
			if(!_modelLoaded || _generating) return;
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
		private void RichTextMsgOnMouseWheel(object sender, MouseEventArgs e){
			NativeMethods.SendMessage(panelChat.Handle, 0x020A, (IntPtr)((e.Delta/8 << 16) & 0xffff0000), IntPtr.Zero);
		}
		private ChatMessage AddMessage(bool user, string message){
			var cm = new ChatMessage(user, message, checkMarkdown.Checked);
			cm.Width = panelChat.ClientSize.Width;
			cm.butDelete.Click += (o, args) => MsgButDeleteOnClick(cm);
			cm.butRegen.Click += (o, args) => MsgButRegenOnClick(cm);
			cm.butEdit.Click += (o, args) => MsgButEditOnClick(cm);
			cm.butCancelEdit.Click += (o, args) => MsgButEditCancelOnClick(cm);
			cm.butApplyEdit.Click += (o, args) => MsgButEditApplyOnClick(cm);
			cm.richTextMsg.MouseWheel += RichTextMsgOnMouseWheel;
			cm.richTextMsg.LinkClicked += RichTextMsgOnLinkClicked;
			panelChat.Controls.Add(cm);
			_chatMessages.Add(cm);
			return cm;
		}
		private void RichTextMsgOnLinkClicked(object sender, LinkClickedEventArgs e){
			Process.Start(e.LinkText);
		}
		private void Generate(bool regenerating){
			if(!_modelLoaded || _generating || !regenerating && string.IsNullOrWhiteSpace(textInput.Text)) return;
			_generating = true;
			foreach(var msg in _chatMessages.Where(msg => msg.Editing)) MsgButEditCancelOnClick(msg);
			butGen.Text = "Stop";
			butReset.Enabled = false;
			if(!regenerating){
				var msg = textInput.Text.Trim();
				var cm = AddMessage(true, msg);
				cm.Width -= 1;//Workaround for resize issue
				cm.Width = panelChat.ClientSize.Width;
				NativeMethods.AddMessage(true, msg);
			}
                        _cntAssMsg = null;
                        NativeMethods.SendMessage(_this.panelChat.Handle, NativeMethods.WM_VSCROLL, (IntPtr)NativeMethods.SB_BOTTOM, IntPtr.Zero);
			foreach(var message in _chatMessages) message.Generating = true;
			textInput.Text = "";
			_tts.SpeakAsyncCancelAll();
			_first = true;
			ThreadPool.QueueUserWorkItem(o => {
				_swTot.Restart();
				_swRate.Restart();
				var toks = _googleHandler != null ? NativeMethods.GenerateWithTools(_nGen, checkStream.Checked) : NativeMethods.Generate(_nGen, checkStream.Checked);
				_swTot.Stop();
				_swRate.Stop();
				if(_speechBuffer.Length > 0){
					var remainingText = _speechBuffer.ToString().Trim();
					if(!string.IsNullOrWhiteSpace(remainingText)) _tts.SpeakAsync(remainingText);
					_speechBuffer.Clear();
				}
				try{
					Invoke(new MethodInvoker(() => {
						var elapsed = _swTot.Elapsed.TotalSeconds;
						if(toks > 0 && elapsed > 0.0){
							var callsPerSecond = toks/elapsed;
							labelTPS.Text = $"{callsPerSecond:F2} Tok/s";
							_swTot.Reset();
							_swRate.Reset();
						}
						butGen.Text = "Generate";
						butReset.Enabled = true;
						_generating = false;
						foreach(var message in _chatMessages) message.Generating = false;
					}));
				} catch(ObjectDisposedException){}
			});
		}
		private static int FindSentenceEnd(StringBuilder sb){
			for(var i = 0; i < sb.Length - 1; i++) if((sb[i] == '.' || sb[i] == '!' || sb[i] == '?') && char.IsWhiteSpace(sb[i + 1])) return i;
			return -1;
		}
                private static unsafe void TokenCallback(byte* strPtr, int strLen, int tokens, int tokensTotal, double ftTime, byte* rolePtr, int roleLen){
                        var tokenStr = Encoding.UTF8.GetString(strPtr, strLen);
                        string role;
                        if(rolePtr == null || roleLen <= 0){
                                role = string.Empty;
                        } else{
                                var bytes = new byte[roleLen];
                                Marshal.Copy(new IntPtr(rolePtr), bytes, 0, roleLen);
                                role = Encoding.UTF8.GetString(bytes);
                        }
                        if(role == "tool"){
                                try{
                                        _this.Invoke(new MethodInvoker(() => {
                                                var cm = _this.AddMessage(false, tokenStr);
                                                cm.SetRoleText("Tool");
                                                cm.Width -= 1;//Workaround for resize issue
                                                cm.Width = _this.panelChat.ClientSize.Width;
                                                _this._cntAssMsg = null;
                                                _this.labelTokens.Text = tokensTotal + "/" + _this._cntCtxMax + " Tokens";
                                                NativeMethods.SendMessage(_this.panelChat.Handle, NativeMethods.WM_VSCROLL, (IntPtr)NativeMethods.SB_BOTTOM, IntPtr.Zero);
                                        }));
                                } catch(ObjectDisposedException){}
                                return;
                        }
                        _this._msgTokenCount += tokens;
                        var elapsed = _this._swRate.Elapsed.TotalSeconds;
                        if(elapsed >= 1.0) _this._swRate.Restart();
                        var renderToken = !_this._rendering;
                        try{
                                _this.BeginInvoke((MethodInvoker)(() => {
                                        try{
                                                _this._rendering = true;
                                                if(_this._first){
                                                        _this.labelPreGen.Text = "First token time: " + ftTime + " s";
                                                        _this._first = false;
                                                }
                                                if(elapsed >= 1.0){
                                                        var callsPerSecond = _this._msgTokenCount/elapsed;
                                                        _this.labelTPS.Text = $"{callsPerSecond:F2} Tok/s";
                                                        _this._msgTokenCount = 0;
                                                }
                                                if(_this._cntAssMsg == null){
                                                        _this._cntAssMsg = _this.AddMessage(false, "");
                                                        _this._cntAssMsg.Width -= 1;//Workaround for resize issue
                                                        _this._cntAssMsg.Width = _this.panelChat.ClientSize.Width;
                                                }
                                                var lastThink = _this._cntAssMsg.checkThink.Checked;
                                                _this._cntAssMsg.AppendText(tokenStr, renderToken);
                                                if(_this._speak && !_this._cntAssMsg.checkThink.Checked && !lastThink && !string.IsNullOrWhiteSpace(tokenStr)){
                                                        foreach(var ch in tokenStr.Where(ch => ch != '`' && ch != '*' && ch != '_' && ch != '#')) _this._speechBuffer.Append(ch);
                                                        int idx;
							while((idx = FindSentenceEnd(_this._speechBuffer)) >= 0){
								var sentence = _this._speechBuffer.ToString(0, idx + 1).Trim();
								if(!string.IsNullOrWhiteSpace(sentence)) _this._tts.SpeakAsync(sentence);
								_this._speechBuffer.Remove(0, idx + 1);
								while(_this._speechBuffer.Length > 0 && char.IsWhiteSpace(_this._speechBuffer[0])) _this._speechBuffer.Remove(0, 1);
							}
						}
						_this.labelTokens.Text = tokensTotal + "/" + _this._cntCtxMax + " Tokens";
						NativeMethods.SendMessage(_this.panelChat.Handle, NativeMethods.WM_VSCROLL, (IntPtr)NativeMethods.SB_BOTTOM, IntPtr.Zero);
					} catch(ObjectDisposedException){} finally{ _this._rendering = false; }
				}));
			} catch(ObjectDisposedException){}
		}
		private static void WhisperCallback(string transcription){
			var thisform = _this;
			if(_this.IsDisposed) return;
			_this.BeginInvoke((MethodInvoker)(() => {
				if(thisform.IsDisposed) return;
				_this.textInput.AppendText(transcription);
				if(_this.checkVoiceInput.CheckState == CheckState.Checked) _this.Generate(false);
			}));
		}
		private void TextInput_DragEnter(object sender, DragEventArgs e) {
			if(e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy;
		}
		private void TextInput_DragDrop(object sender, DragEventArgs e) {
			var files = (string[])e.Data.GetData(DataFormats.FileDrop);
			foreach(var filePath in files){
				try{
					var fileName = Path.GetFileName(filePath);
					var fileContent = File.ReadAllText(filePath);
					string contentType;
					switch(Path.GetExtension(filePath)){
						case ".h":// C/C++ header
						case ".cpp":// C++ source
						case ".cc":// C++ source
						case ".cxx":// C++ source
						case ".c":// C source
							contentType = "cpp";
							break;
						case ".cs":// C#
							contentType = "csharp";
							break;
						case ".py":// Python
							contentType = "python";
							break;
						case ".java":// Java
							contentType = "java";
							break;
						case ".js":// JavaScript
						case ".jsx":// React/JSX
							contentType = "javascript";
							break;
						case ".html":// HTML
						case ".htm":// HTML
							contentType = "html";
							break;
						case ".css":// CSS
							contentType = "css";
							break;
						case ".xml":// XML
							contentType = "xml";
							break;
						case ".json":// JSON
							contentType = "json";
							break;
						case ".md":// Markdown
							contentType = "markdown";
							break;
						case ".rb":// Ruby
							contentType = "ruby";
							break;
						case ".php":// PHP
							contentType = "php";
							break;
						case ".swift":// Swift
							contentType = "swift";
							break;
						case ".go":// Go
							contentType = "go";
							break;
						case ".rs":// Rust
							contentType = "rust";
							break;
						case ".ts":// TypeScript
						case ".tsx":// TypeScript React
							contentType = "typescript";
							break;
						case ".sql":// SQL
							contentType = "sql";
							break;
						case ".sh":// Shell script
							contentType = "bash";
							break;
						default:// Fallback for unknown files
							contentType = "";
							break;
					}
					textInput.AppendText($"[FILE] {fileName}\r\n```{contentType}\r\n{fileContent}\r\n```\r\n\r\n");
				} catch(Exception ex){ MessageBox.Show($"Error reading file: {ex.Message}"); }
			}
		}
	}
}