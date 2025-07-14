using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Speech.Synthesis;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using LMStud.Properties;
namespace LMStud{
	internal partial class Form1 : Form{
		private static Form1 _this;
		private static NativeMethods.TokenCallback _tokenCallback;
		private static NativeMethods.WhisperCallback _whisperCallback;
		private readonly List<ChatMessage> _chatMessages = new List<ChatMessage>();
		private readonly StringBuilder _speechBuffer = new StringBuilder();
		private readonly Stopwatch _swRate = new Stopwatch();
		private readonly Stopwatch _swTot = new Stopwatch();
		private readonly SpeechSynthesizer _tts = new SpeechSynthesizer();
		private ChatMessage _cntAssMsg;
		private bool _first = true;
		private volatile bool _generating;
		private int _genTokenTotal;
		private int _msgTokenCount;
		private volatile bool _rendering;
		private bool _whisperLoaded;
		internal Form1(){
			_this = this;
			InitializeComponent();
			Icon = Resources.LM_Stud_256;
			SetToolTips();
			LoadConfig();
		}
		private void SetToolTips(){
			toolTip1.SetToolTip(textSystemPrompt, "Tell the AI who or what to be, how to respond, or provide initial context.");
			toolTip1.SetToolTip(textModelsPath, "Path to the folder containing your .gguf model files.");
			toolTip1.SetToolTip(numCtxSize, "Context size (max tokens). Higher values improve memory but use more RAM.");
			toolTip1.SetToolTip(numGPULayers, "Number of layers to offload to GPU. More layers improve performance but increase GPU memory usage.");
			toolTip1.SetToolTip(numTemp, "Temperature controls randomness. Lower values make responses more deterministic; higher values produce more creative outputs.");
			toolTip1.SetToolTip(numNGen, "Number of tokens to generate per response (max length of the AI's reply).");
			toolTip1.SetToolTip(comboNUMAStrat, "NUMA (Non-Uniform Memory Access) strategy. Adjust if using multi-socket CPUs or specific memory configurations.");
			toolTip1.SetToolTip(numRepPen, "Repetition penalty reduces repetitive outputs. Higher values strongly discourage repeated phrases.");
			toolTip1.SetToolTip(numTopK, "Top-K sampling: limits token choice to the K most probable tokens, improving coherency.");
			toolTip1.SetToolTip(numTopP, "Top-P sampling: controls diversity by choosing from the smallest possible set of tokens whose cumulative probability exceeds this threshold.");
			toolTip1.SetToolTip(numMinP, "Min-P sampling: keeps only tokens whose individual probability is at least this fraction of the top token’s probability, trimming unlikely options and reducing noise.\r\nIf Min-P is non zero it is recommended to set Top-P to 1.0 and Top-K to 0.");
			toolTip1.SetToolTip(numBatchSize, "Batch size for processing tokens during generation. Higher values can improve performance at the cost of higher RAM usage.");
			toolTip1.SetToolTip(groupCPUParams, "Parameters controlling text generation on CPU.");
			toolTip1.SetToolTip(numThreads, "CPU threads for token generation. Typically, around 75% of your physical cores is optimal to prevent oversaturating the memory controller.");
			toolTip1.SetToolTip(checkMMap, "Map the model file to memory for on-demand loading, may improve model load times.");
			toolTip1.SetToolTip(checkMLock, "Lock the model in RAM.");
			toolTip1.SetToolTip(groupCPUParamsBatch, "Parameters for the pre-generation step (batch preparation).");
			toolTip1.SetToolTip(numThreadsBatch, "CPU threads used for batch preparation.");
			toolTip1.SetToolTip(numVadThreshold, "Voice Activity Detection, higher values increase sensitivity to speech (0.1-1.0).");
			toolTip1.SetToolTip(numFreqThreshold, "High-pass filter cutoff frequency. Higher values reduce background noise.");
			toolTip1.SetToolTip(checkSpeak, "Speak the generated responses using the computers default voice.");
			toolTip1.SetToolTip(checkVoiceInput, "An intermediate check state (filled in) means it will transcribe spoken words without generating, when checked it will automatically generate.");
			toolTip1.SetToolTip(textGoogleApiKey, "A Google API key is required to use the search tool, you can get a free one for 100 searches per day.");
			toolTip1.SetToolTip(textGoogleSearchID, "Create your own Google Programmable Search Engine and copy its \"Search engine ID\" here.");
			toolTip1.SetToolTip(checkFileListEnable, "Enable the tool for listing the contents of a folder under the base path.");
			toolTip1.SetToolTip(checkFileCreateEnable, "Enable the tool for creating new files under the base path.");
			toolTip1.SetToolTip(checkFileReadEnable, "Enable the tool for reading the contents of files under the base path.");
			toolTip1.SetToolTip(checkFileWriteEnable, "Enable the tool for writing to files under the base path.");
			toolTip1.SetToolTip(textFileBasePath, "File access is relative to this path and canonically restricted to files and folders under this path only.");
			toolTip1.SetToolTip(linkFileInstruction, "Set an instruction to optimize the way the assistant uses the file tools.");
		}
		private void Form1_Load(object sender, EventArgs e){
			NativeMethods.SetHWnd(Handle);
			NativeMethods.CurlGlobalInit();
			PopulateModels();
			PopulateWhisperModels();
			NativeMethods.BackendInit();
			if(!Settings.Default.LoadAuto) return;
			checkLoadAuto.Checked = true;
			ThreadPool.QueueUserWorkItem(o => {
				while(_populating) Thread.Sleep(10);
				for(var i = 0; i < _models.Count; i++){
					var model = _models[i];
					if(model.FilePath != Settings.Default.LastModel) continue;
					Invoke(new MethodInvoker(() => {LoadModel(i, true);}));
					break;
				}
			});
		}
		private void Form1_FormClosing(object sender, FormClosingEventArgs e){
			if(_generating) NativeMethods.StopGeneration();
			if(_whisperLoaded){
				NativeMethods.StopSpeechTranscription();
				NativeMethods.UnloadWhisperModel();
			}
			_tts.Dispose();
			NativeMethods.CurlGlobalCleanup();
		}
		private void Form1_KeyDown(object sender, KeyEventArgs e){
			if(e.KeyCode != Keys.Escape) return;
			_tts.SpeakAsyncCancelAll();
			if(!_generating) return;
			NativeMethods.StopGeneration();
		}
		private void ButCodeBlock_Click(object sender, EventArgs e){textInput.Paste("```\r\n\r\n```");}
		private void CheckMarkdown_CheckedChanged(object sender, EventArgs e){
			foreach(var message in _chatMessages) message.Markdown = checkMarkdown.Checked;
		}
		private void CheckVoiceInput_CheckedChanged(object sender, EventArgs e){
			if(checkVoiceInput.CheckState != CheckState.Unchecked){
				if(_whisperModelIndex < 0 || !File.Exists(_whisperModels[_whisperModelIndex])){
					checkVoiceInput.Checked = false;
					MessageBox.Show(this, "Whisper model not found", "LM Stud", MessageBoxButtons.OK, MessageBoxIcon.Error);
					tabControl1.SelectTab(1);
					comboWhisperModel.Focus();
					return;
				}
				if(_llModelLoaded){
					if(!_whisperLoaded){
						var result = NativeMethods.LoadWhisperModel(_whisperModels[_whisperModelIndex], _nThreads, _whisperUseGPU);
						if(result){
							_whisperLoaded = true;
							_whisperCallback = WhisperCallback;
							NativeMethods.SetWhisperCallback(_whisperCallback);
						} else{
							_whisperLoaded = false;
							MessageBox.Show(this, "Error initialising whisper", "LM Stud", MessageBoxButtons.OK, MessageBoxIcon.Error);
							checkVoiceInput.Checked = false;
							return;
						}
					}
					if(NativeMethods.StartSpeechTranscription()) return;
					MessageBox.Show(this, "Error starting whisper transcription", "LM Stud", MessageBoxButtons.OK, MessageBoxIcon.Error);
					checkVoiceInput.Checked = false;
				} else{
					MessageBox.Show(this, "Load a model on the Models tab first", "LM Stud", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
					checkVoiceInput.Checked = false;
				}
			} else{
				NativeMethods.StopSpeechTranscription();
			}
		}
		private void CheckSpeak_CheckedChanged(object sender, EventArgs e){
			UpdateSetting(ref _speak, checkSpeak.Checked, value => {Settings.Default.Speak = value;});
			Settings.Default.Save();
		}
		private void ButGen_Click(object sender, EventArgs e){
			if(_generating) NativeMethods.StopGeneration();
			else Generate();
		}
		private void ButReset_Click(object sender, EventArgs e){
			NativeMethods.ResetChat();
			NativeMethods.ClearWebCache();
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
			var ok = NativeMethods.RemoveMessageAt(id);
			_chatMessages[id].Dispose();
			_chatMessages.RemoveAt(id);
			if(!ok) MessageBox.Show(this, "Conversation too long for context", "LM Stud", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
		}
		private void MsgButRegenOnClick(ChatMessage cm){
			if(!_llModelLoaded || _generating) return;
			var idx = _chatMessages.IndexOf(cm);
			if(idx < 0) return;
			while(_chatMessages[idx].Role == MessageRole.Assistant)
				if(--idx < 0)
					return;
			var role = _chatMessages[idx].Role;
			var msg = _chatMessages[idx].Message;
			var ok = NativeMethods.RemoveMessagesStartingAt(idx);
			for(var i = _chatMessages.Count - 1; i >= idx; i--){
				_chatMessages[i].Dispose();
				_chatMessages.RemoveAt(i);
			}
			if(!ok){
				MessageBox.Show(this, "Conversation too long for context", "LM Stud", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
				return;
			}
			Generate(role, msg);
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
			var idx = _chatMessages.IndexOf(cm);
			if(cm.checkThink.Checked) cm.Think = cm.richTextMsg.Text;
			else cm.Message = cm.richTextMsg.Text;
			if(!NativeMethods.SetMessageAt(idx, cm.Think, cm.Message)) MessageBox.Show(this, "Conversation too long for context", "LM Stud", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
			cm.Editing = false;
			cm.Markdown = checkMarkdown.Checked;
		}
		private void RichTextMsgOnMouseWheel(object sender, MouseEventArgs e){NativeMethods.SendMessage(panelChat.Handle, 0x020A, (IntPtr)(((e.Delta/8) << 16) & 0xffff0000), IntPtr.Zero);}
		private ChatMessage AddMessage(MessageRole role, string message){
			var cm = new ChatMessage(role, message, checkMarkdown.Checked){ Parent = panelChat, Width = panelChat.ClientSize.Width };
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
		private static void RichTextMsgOnLinkClicked(object sender, LinkClickedEventArgs e){Process.Start(e.LinkText);}
		private void Generate(){
			var prompt = textInput.Text;
			textInput.Text = "";
			Generate(MessageRole.User, prompt);
		}
		private void Generate(MessageRole role, string prompt){
			if(!_llModelLoaded || _generating || string.IsNullOrWhiteSpace(prompt)) return;
			_generating = true;
			foreach(var msg in _chatMessages.Where(msg => msg.Editing)) MsgButEditCancelOnClick(msg);
			butGen.Text = "Stop";
			butReset.Enabled = butApply.Enabled = false;
			var newMsg = prompt.Trim();
			var cm = AddMessage(role, newMsg);
			_cntAssMsg = null;
			foreach(var message in _chatMessages) message.Generating = true;
			_tts.SpeakAsyncCancelAll();
			_first = true;
			ThreadPool.QueueUserWorkItem(o => {
				_msgTokenCount = 0;
				_genTokenTotal = 0;
				_swTot.Restart();
				_swRate.Restart();
				NativeMethods.GenerateWithTools(role, newMsg, _nGen, checkStream.Checked);
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
						if(_genTokenTotal > 0 && elapsed > 0.0){
							var callsPerSecond = _genTokenTotal/elapsed;
							labelTPS.Text = $"{callsPerSecond:F2} Tok/s";
							_swTot.Reset();
							_swRate.Reset();
						}
						butGen.Text = "Generate";
						butReset.Enabled = butApply.Enabled = true;
						_generating = false;
						foreach(var message in _chatMessages) message.Generating = false;
					}));
				} catch(ObjectDisposedException){}
			});
		}
		private static int FindSentenceEnd(StringBuilder sb){
			for(var i = 0; i < sb.Length - 1; i++)
				if((sb[i] == '.' || sb[i] == '!' || sb[i] == '?') && char.IsWhiteSpace(sb[i + 1]))
					return i;
			return -1;
		}
		private static unsafe void TokenCallback(byte* thinkPtr, int thinkLen, byte* messagePtr, int messageLen, int tokenCount, int tokensTotal, double ftTime, int tool){
			var elapsed = _this._swRate.Elapsed.TotalSeconds;
			if(elapsed >= 1.0) _this._swRate.Restart();
			var think = "";
			if(thinkLen > 0) think = Encoding.UTF8.GetString(thinkPtr, thinkLen);
			var message = "";
			if(messageLen > 0) message = Encoding.UTF8.GetString(messagePtr, messageLen);
			if(tool == 1){
				try{
					_this.Invoke(new MethodInvoker(() => {
						var cm = _this.AddMessage(MessageRole.Tool, message);
						cm.SetRoleText("Tool");
						_this._cntAssMsg = null;
						_this.labelTokens.Text = tokensTotal + "/" + _this._cntCtxMax + " Tokens";
					}));
				} catch(ObjectDisposedException){}
				return;
			}
			_this._msgTokenCount += tokenCount;
			_this._genTokenTotal += tokenCount;
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
						if(_this._cntAssMsg == null) _this._cntAssMsg = _this.AddMessage(MessageRole.Assistant, "");
						var lastThink = _this._cntAssMsg.checkThink.Checked;
						_this._cntAssMsg.UpdateText(think, message, renderToken);
						if(_this._speak && !_this._cntAssMsg.checkThink.Checked && !lastThink && !string.IsNullOrWhiteSpace(message)){
							var i = _this._cntAssMsg.TTSPosition;
							for(; i < _this._cntAssMsg.Message.Length; i++){
								var ch = _this._cntAssMsg.Message[i];
								if(ch != '`' && ch != '*' && ch != '_' && ch != '#') _this._speechBuffer.Append(ch);
							}
							_this._cntAssMsg.TTSPosition = i;
							while((i = FindSentenceEnd(_this._speechBuffer)) >= 0){
								var sentence = _this._speechBuffer.ToString(0, i + 1).Trim();
								if(!string.IsNullOrWhiteSpace(sentence)) _this._tts.SpeakAsync(sentence);
								_this._speechBuffer.Remove(0, i + 1);
								while(_this._speechBuffer.Length > 0 && char.IsWhiteSpace(_this._speechBuffer[0])) _this._speechBuffer.Remove(0, 1);
							}
						}
						_this.labelTokens.Text = tokensTotal + "/" + _this._cntCtxMax + " Tokens";
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
				if(_this.checkVoiceInput.CheckState == CheckState.Checked) _this.Generate();
			}));
		}
		private void TextInput_DragEnter(object sender, DragEventArgs e){
			if(e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy;
		}
		private void TextInput_DragDrop(object sender, DragEventArgs e){
			var files = (string[])e.Data.GetData(DataFormats.FileDrop);
			foreach(var filePath in files)
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