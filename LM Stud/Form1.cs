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
using Timer = System.Windows.Forms.Timer;
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
		private volatile bool _apiGenerating;
		private Action<string> _apiTokenCallback;
		private int _genTokenTotal;
		private int _msgTokenCount;
		private volatile bool _rendering;
		private bool _whisperLoaded;
		private LVColumnClickHandler _columnClickHandler;
		private readonly ApiServer _apiServer;
		internal bool IsGenerating => _generating || _apiGenerating;
		internal readonly SemaphoreSlim GenerationLock = new SemaphoreSlim(1, 1);
		private readonly Timer _genTimer = new Timer();
		internal Form1(){
			_this = this;
			//var culture = new CultureInfo("zh-CN");
			//Thread.CurrentThread.CurrentUICulture = culture;
			//Thread.CurrentThread.CurrentCulture = culture;
			InitializeComponent();
			_apiServer = new ApiServer(this);
			InitializeListViews();
			Icon = Resources.LM_Stud_256;
			SetToolTips();
			LoadConfig();
			LoadModelSettings();
			if(_genDelay > 0) _genTimer.Interval = _genDelay;
			_genTimer.Tick += (sender, args) => {
				_genTimer.Stop();
				Generate();
			};
		}
		private void SetToolTip(Control control){toolTip1.SetToolTip(control, Resources.ResourceManager.GetString("ToolTip_" + control.Name));}
		private void SetToolTips(){
			SetToolTip(textSystemPrompt);
			SetToolTip(textModelsPath);
			SetToolTip(numCtxSize);
			SetToolTip(numGPULayers);
			SetToolTip(numTemp);
			SetToolTip(numNGen);
			SetToolTip(comboNUMAStrat);
			SetToolTip(numRepPen);
			SetToolTip(numTopK);
			SetToolTip(numTopP);
			SetToolTip(numMinP);
			SetToolTip(numBatchSize);
			SetToolTip(groupCPUParams);
			SetToolTip(numThreads);
			SetToolTip(checkMMap);
			SetToolTip(checkMLock);
			SetToolTip(groupCPUParamsBatch);
			SetToolTip(numThreadsBatch);
			SetToolTip(numVadThreshold);
			SetToolTip(numFreqThreshold);
			SetToolTip(checkSpeak);
			SetToolTip(checkVoiceInput);
			SetToolTip(textGoogleApiKey);
			SetToolTip(textGoogleSearchID);
			SetToolTip(checkFileListEnable);
			SetToolTip(checkFileCreateEnable);
			SetToolTip(checkFileReadEnable);
			SetToolTip(checkFileWriteEnable);
			SetToolTip(textFileBasePath);
			SetToolTip(linkFileInstruction);
			SetToolTip(numWakeWordSimilarity);
			SetToolTip(textWakeWord);
			SetToolTip(radioBasicVAD);
			SetToolTip(radioWhisperVAD);
			SetToolTip(comboVADModel);
			SetToolTip(butVADDown);
			SetToolTip(checkApiServerEnable);
			SetToolTip(numApiServerPort);
		}
		private void Form1_Load(object sender, EventArgs e){
			NativeMethods.SetHWnd(Handle);
			NativeMethods.CurlGlobalInit();
			PopulateModels();
			PopulateWhisperModels(true, true);
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
		private void InitializeListViews(){
			_columnClickHandler = new LVColumnClickHandler();
			var columnDataTypesHugSearch = new[]{
				SortDataType.String,// Name
				SortDataType.String,// Uploader
				SortDataType.Integer,// Likes
				SortDataType.Integer,// Downloads
				SortDataType.Integer,// Trending
				SortDataType.DateTime,// Created
				SortDataType.DateTime// Modified
			};
			var columnDataTypesHugFiles = new[]{
				SortDataType.String,// FileName
				SortDataType.Double// Size
			};
			_columnClickHandler.RegisterListView(listViewModels);
			_columnClickHandler.RegisterListView(listViewMeta);
			_columnClickHandler.RegisterListView(listViewHugSearch, columnDataTypesHugSearch, 4, SortOrder.Descending);
			_columnClickHandler.RegisterListView(listViewHugFiles, columnDataTypesHugFiles);
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
				if(_whisperModelIndex < 0 || _whisperModelIndex >= _whisperModels.Count || !File.Exists(_whisperModels[_whisperModelIndex])){
					checkVoiceInput.Checked = false;
					MessageBox.Show(this, Resources.Error_Whisper_model_not_found, Resources.LM_Stud, MessageBoxButtons.OK, MessageBoxIcon.Error);
					tabControl1.SelectTab(1);
					comboWhisperModel.Focus();
					return;
				}
				if(_useWhisperVAD)
					if(_vadModelIndex < 0 || _vadModelIndex >= _whisperModels.Count || !File.Exists(_whisperModels[_vadModelIndex])){
						if(MessageBox.Show(this, Resources.VAD_model_not_found__use_Basic_VAD_, Resources.LM_Stud, MessageBoxButtons.OKCancel, MessageBoxIcon.Exclamation) != DialogResult.OK){
							checkVoiceInput.Checked = false;
							return;
						}
						radioBasicVAD.Checked = true;
						_useWhisperVAD = Settings.Default.UseWhisperVAD = false;
						Settings.Default.Save();
					}
				if(!_whisperLoaded){
					var result = LoadWhisperModel(_whisperModels[_whisperModelIndex], _nThreads, _whisperUseGPU, _useWhisperVAD, _useWhisperVAD ? _whisperModels[_vadModelIndex] : "");
					if(result == NativeMethods.StudError.Success){
						_whisperLoaded = true;
						_whisperCallback = WhisperCallback;
						NativeMethods.SetWhisperCallback(_whisperCallback);
					} else{
						_whisperLoaded = false;
						MessageBox.Show(this, Resources.Error_initialising_voice_input, Resources.LM_Stud, MessageBoxButtons.OK, MessageBoxIcon.Error);
						checkVoiceInput.Checked = false;
						return;
					}
				}
				if(NativeMethods.StartSpeechTranscription()) return;
				MessageBox.Show(this, Resources.Error_starting_voice_input, Resources.LM_Stud, MessageBoxButtons.OK, MessageBoxIcon.Error);
				checkVoiceInput.Checked = false;
			} else{
				_genTimer.Stop();
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
			labelTokens.Text = NativeMethods.LlamaMemSize() + Resources._Tokens;
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
			GenerationLock.Wait(-1);
			NativeMethods.StudError result;
			try{ result = NativeMethods.RemoveMessageAt(id); } finally{GenerationLock.Release();}
			if(result != NativeMethods.StudError.IndexOutOfRange){
				_chatMessages[id].Dispose();
				_chatMessages.RemoveAt(id);
			}
			if(result != NativeMethods.StudError.Success) ShowErrorMessage(Resources.Error_creating_session, result);
		}
		private void MsgButRegenOnClick(ChatMessage cm){
			if(!LlModelLoaded || _generating) return;
			var idx = _chatMessages.IndexOf(cm);
			if(idx < 0) return;
			while(_chatMessages[idx].Role == MessageRole.Assistant)
				if(--idx < 0)
					return;
			var role = _chatMessages[idx].Role;
			var msg = _chatMessages[idx].Message;
			GenerationLock.Wait(-1);
			NativeMethods.StudError result;
			try{ result = NativeMethods.RemoveMessagesStartingAt(idx); } finally{ GenerationLock.Release(); }
			if(result != NativeMethods.StudError.IndexOutOfRange)
				for(var i = _chatMessages.Count - 1; i >= idx; i--){
					_chatMessages[i].Dispose();
					_chatMessages.RemoveAt(i);
				}
			if(result != NativeMethods.StudError.Success){
				ShowErrorMessage(Resources.Error_creating_session, result);
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
			if(NativeMethods.SetMessageAt(idx, cm.Think, cm.Message) != NativeMethods.StudError.Success) MessageBox.Show(this, Resources.Conversation_too_long_for_context, Resources.LM_Stud, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
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
			panelChat.ScrollToEnd();
			return cm;
		}
		private static void RichTextMsgOnLinkClicked(object sender, LinkClickedEventArgs e){Process.Start(e.LinkText);}
		private void Generate(){
			var prompt = textInput.Text;
			if(Generate(MessageRole.User, prompt)) textInput.Text = "";
		}
		private bool Generate(MessageRole role, string prompt){
			if(!LlModelLoaded || string.IsNullOrWhiteSpace(prompt)) return false;
			if(!GenerationLock.Wait(0)) return false;
			if(_generating || _apiGenerating){
				GenerationLock.Release();
				return false;
			}
			_generating = true;
			foreach(var msg in _chatMessages.Where(msg => msg.Editing)) MsgButEditCancelOnClick(msg);
			butGen.Text = Resources.Stop;
			butReset.Enabled = butApply.Enabled = false;
			var newMsg = prompt.Trim();
			AddMessage(role, newMsg);
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
					BeginInvoke(new MethodInvoker(() => {
						var elapsed = _swTot.Elapsed.TotalSeconds;
						if(_genTokenTotal > 0 && elapsed > 0.0){
							var callsPerSecond = _genTokenTotal/elapsed;
							labelTPS.Text = string.Format(Resources._0_F2__Tok_s, callsPerSecond);
							_swTot.Reset();
							_swRate.Reset();
						}
						butGen.Text = Resources.Generate;
						butReset.Enabled = butApply.Enabled = true;
						_generating = false;
						foreach(var message in _chatMessages) message.Generating = false;
					}));
				} catch(ObjectDisposedException){} finally{ GenerationLock.Release(); }
			});
			return true;
		}
		internal bool GenerateForApi(byte[] state, string prompt, Action<string> onToken){
			if(!LlModelLoaded || _generating || _apiGenerating || string.IsNullOrWhiteSpace(prompt)) return false;
			_apiGenerating = true;
			_apiTokenCallback = onToken;
			try{
				SetState(state);
				NativeMethods.GenerateWithTools(MessageRole.User, prompt, _nGen, false);
				return true;
			} finally{
				_apiTokenCallback = null;
				_apiGenerating = false;
			}
		}
		private static int FindSentenceEnd(StringBuilder sb){
			for(var i = 0; i < sb.Length - 1; i++)
				if((sb[i] == '.' || sb[i] == '!' || sb[i] == '?') && char.IsWhiteSpace(sb[i + 1]))
					return i;
			return -1;
		}
		internal byte[] GetState(){
			var size = NativeMethods.GetStateSize();
			var data = new byte[size];
			unsafe{
				fixed(byte* p = data){ NativeMethods.CopyStateData((IntPtr)p, size); }
			}
			return data;
		}
		internal void SetState(byte[] state){
			NativeMethods.ResetChat();
			if(state == null || state.Length == 0) return;
			unsafe{
				fixed(byte* p = state){ NativeMethods.SetStateData((IntPtr)p, state.Length); }
			}
		}
		internal int GetTokenCount(){return NativeMethods.LlamaMemSize();}
		private static unsafe void TokenCallback(byte* thinkPtr, int thinkLen, byte* messagePtr, int messageLen, int tokenCount, int tokensTotal, double ftTime, int tool){
			if(_this._apiGenerating){
				if(messageLen <= 0) return;
				var msg = Encoding.UTF8.GetString(messagePtr, messageLen);
				_this._apiTokenCallback?.Invoke(msg);
				return;
			}
			var elapsed = _this._swRate.Elapsed.TotalSeconds;
			if(elapsed >= 1.0) _this._swRate.Restart();
			var think = "";
			if(thinkLen > 0) think = Encoding.UTF8.GetString(thinkPtr, thinkLen);
			var message = "";
			if(messageLen > 0) message = Encoding.UTF8.GetString(messagePtr, messageLen);
			if(tool == 1){
				try{
					_this.BeginInvoke(new MethodInvoker(() => {
						var cm = _this.AddMessage(MessageRole.Tool, message);
						cm.SetRoleText("Tool");
						_this._cntAssMsg = null;
						_this.labelTokens.Text = string.Format(Resources._0___1__2_, tokensTotal, _this._cntCtxMax, Resources._Tokens);
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
							_this.labelPreGen.Text = Resources.First_token_time__ + ftTime + Resources._s;
							_this._first = false;
						}
						if(elapsed >= 1.0){
							var callsPerSecond = _this._msgTokenCount/elapsed;
							_this.labelTPS.Text = string.Format(Resources._0_F2__Tok_s, callsPerSecond);
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
						_this.labelTokens.Text = string.Format(Resources._0___1__2_, tokensTotal, _this._cntCtxMax, Resources._Tokens);
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
				if(_this.checkVoiceInput.CheckState != CheckState.Checked) return;
				_this._genTimer.Stop();
				if(_this._genDelay == 0) _this.Generate();
				else _this._genTimer.Start();
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
				} catch(Exception ex){ MessageBox.Show(string.Format(Resources.Error_reading_file___0_, ex.Message)); }
		}
	}
}