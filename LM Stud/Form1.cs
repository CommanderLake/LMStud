using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Speech.Synthesis;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using LMStud.Properties;
namespace LMStud{
	public partial class Form1 : Form{
		public static Form1 This;
		private static NativeMethods.TokenCallback _tokenCallback;
		private static NativeMethods.WhisperCallback _whisperCallback;
		private static NativeMethods.SpeechEndCallback _speechEndCallback;
		private readonly ApiServer _apiServer;
		private readonly List<ChatMessage> _chatMessages = new List<ChatMessage>();
		private readonly StringBuilder _speechBuffer = new StringBuilder();
		private readonly Stopwatch _swRate = new Stopwatch();
		private readonly Stopwatch _swTot = new Stopwatch();
		private readonly SpeechSynthesizer _tts = new SpeechSynthesizer();
		private volatile bool _ttsSpeaking;
		private int _ttsPendingCount;
		private volatile bool _voiceInputResumePending;
		private int _retokenizeCount;
		private bool _retokenizeButApplyEnabled;
		private bool _retokenizeButApplyModelSettingsEnabled;
		private bool _retokenizeButGenEnabled;
		private bool _retokenizeButLoadEnabled;
		private bool _retokenizeButResetEnabled;
		private bool _retokenizeButUnloadEnabled;
		private bool _retokenizeListViewModelsEnabled;
		private bool _retokenizePanelChatEnabled;
		private bool _retokenizeTextInputEnabled;
		internal readonly SemaphoreSlim GenerationLock = new SemaphoreSlim(1, 1);
		private volatile bool _apiGenerating;
		private Action<string> _apiTokenCallback;
		private CheckState _checkVoiceInputLast = CheckState.Unchecked;
		private ChatMessage _cntAssMsg;
		private ChatMessage _cntToolMsg;
		private LVColumnClickHandler _columnClickHandler;
		private string _editOriginalText = "";
		private bool _firstToken = true;
		private volatile bool _generating;
		private int _genTokenTotal;
		private bool _isEditing;
		private int _msgTokenCount;
		private volatile bool _rendering;
		private bool _whisperLoaded;
		private bool _dialecticStarted;
		private bool _dialecticPaused;
		public Form1(){
			This = this;
			//var culture = new CultureInfo("zh-CN");
			//Thread.CurrentThread.CurrentUICulture = culture;
			//Thread.CurrentThread.CurrentCulture = culture;
			InitializeComponent();
			_apiServer = new ApiServer(this);
			_tts.SpeakStarted += TtsOnSpeakStarted;
			_tts.SpeakCompleted += TtsOnSpeakCompleted;
			InitializeListViews();
			Icon = Resources.LM_Stud_256;
			SetToolTips();
			LoadConfig();
			LoadModelSettings();
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
			SetToolTip(checkFlashAttn);
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
			SetToolTip(numGenDelay);
			SetToolTip(numCmdTimeout);
			SetToolTip(textJinjaTmplModel);
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
			_tts?.Dispose();
			NativeMethods.CloseCommandPrompt();
			NativeMethods.CurlGlobalCleanup();
		}
		private void Form1_KeyDown(object sender, KeyEventArgs e){
			if(e.KeyCode != Keys.Escape) return;
			CancelPendingSpeech();
			if(!_generating) return;
			NativeMethods.StopGeneration();
		}
		private void ButCodeBlock_Click(object sender, EventArgs e){textInput.Paste("```\r\n\r\n```");}
		private void CheckMarkdown_CheckedChanged(object sender, EventArgs e){
			foreach(var message in _chatMessages) message.Markdown = checkMarkdown.Checked;
		}
		private void CheckVoiceInput_CheckedChanged(object sender, EventArgs e){
			try{
				if(checkVoiceInput.CheckState != CheckState.Unchecked && _checkVoiceInputLast == CheckState.Unchecked){
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
							_speechEndCallback = SpeechEndCallback;
							NativeMethods.SetSpeechEndCallback(_speechEndCallback);
						} else{
							_whisperLoaded = false;
							MessageBox.Show(this, Resources.Error_initialising_voice_input, Resources.LM_Stud, MessageBoxButtons.OK, MessageBoxIcon.Error);
							checkVoiceInput.Checked = false;
							return;
						}
					}
					TryStartSpeechTranscription(true);
				} else{
					NativeMethods.StopSpeechTranscription();
					_voiceInputResumePending = false;
				}
			} finally{ _checkVoiceInputLast = checkVoiceInput.CheckState; }
		}
		private void CheckSpeak_CheckedChanged(object sender, EventArgs e){
			UpdateSetting(ref _speak, checkSpeak.Checked, value => {Settings.Default.Speak = value;});
			Settings.Default.Save();
		}
		private void CheckDialectic_CheckedChanged(object sender, EventArgs e){
			if(checkDialectic.Checked){
				if(!LlModelLoaded){
					MessageBox.Show(this, Resources.Load_a_model_first_, Resources.LM_Stud, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
					checkDialectic.Checked = false;
					return;
				}
				NativeMethods.DialecticInit();
				_dialecticStarted = false;
				_dialecticPaused = false;
			} else{
				NativeMethods.DialecticFree();
				_dialecticStarted = false;
				_dialecticPaused = false;
			}
		}
		private void ButGen_Click(object sender, EventArgs e){
			if(_generating){
				_dialecticPaused = true;
				NativeMethods.StopGeneration();
			} else Generate();
		}
		private void ButReset_Click(object sender, EventArgs e){
			if(_retokenizeCount > 0) return;
			BeginRetokenization();
			ThreadPool.QueueUserWorkItem(_ => {
				GenerationLock.Wait(-1);
				try{ NativeMethods.ResetChat(); } finally{ GenerationLock.Release(); }
				NativeMethods.CloseCommandPrompt();
				NativeMethods.ClearWebCache();
				Invoke(new MethodInvoker(() => {
					panelChat.SuspendLayout();
					foreach(var message in _chatMessages) message.Dispose();
					panelChat.ResumeLayout();
					_chatMessages.Clear();
					labelTokens.Text = NativeMethods.LlamaMemSize() + Resources._Tokens;
					EndRetokenization();
				}));
			});
		}
		private void TextInput_KeyDown(object sender, KeyEventArgs e){
			if(_isEditing){
				if(e.KeyCode == Keys.Enter && !e.Control && !e.Shift){
					e.SuppressKeyPress = true;
					NativeMethods.SetCommittedText(textInput.Text);
					FinishEditing();
				} else if(e.KeyCode == Keys.Escape){
					e.SuppressKeyPress = true;
					CancelEditing();
				}
			} else if(e.KeyCode != Keys.Enter && checkVoiceInput.CheckState != CheckState.Unchecked){ StartEditing(); } else if(e.KeyCode == Keys.Enter && !e.Control && !e.Shift && butGen.Enabled){
				e.SuppressKeyPress = true;
				ButGen_Click(null, null);
			}
		}
		private void TextInput_MouseDown(object sender, MouseEventArgs e){
			if(checkVoiceInput.CheckState != CheckState.Unchecked && !_isEditing) StartEditing();
		}
		private void PanelChat_Layout(object sender, LayoutEventArgs e){
			panelChat.SuspendLayout();
			try{
				for(var i = 0; i < panelChat.Controls.Count; ++i) panelChat.Controls[i].Width = panelChat.ClientSize.Width;
			} finally{ panelChat.ResumeLayout(true); }
		}
		private void MsgButDeleteOnClick(ChatMessage cm){
			if(_generating || _retokenizeCount > 0) return;
			var id = _chatMessages.IndexOf(cm);
			if(id < 0) return;
			BeginRetokenization();
			ThreadPool.QueueUserWorkItem(_ => {
				GenerationLock.Wait(-1);
				NativeMethods.StudError result;
				try{ result = NativeMethods.RemoveMessageAt(id); } finally{ GenerationLock.Release(); }
				Invoke(new MethodInvoker(() => {
					if(result != NativeMethods.StudError.IndexOutOfRange && id < _chatMessages.Count){
						_chatMessages[id].Dispose();
						_chatMessages.RemoveAt(id);
					}
					if(result != NativeMethods.StudError.Success) ShowErrorMessage(Resources.Error_creating_session, result);
					EndRetokenization();
				}));
			});
		}
		private void MsgButRegenOnClick(ChatMessage cm){
			if(!LlModelLoaded || _generating || _retokenizeCount > 0) return;
			var idx = _chatMessages.IndexOf(cm);
			if(idx < 0) return;
			while(_chatMessages[idx].Role == MessageRole.Assistant)
				if(--idx < 0)
					return;
			var role = _chatMessages[idx].Role;
			var msg = _chatMessages[idx].Message;
			BeginRetokenization();
			ThreadPool.QueueUserWorkItem(_ => {
				GenerationLock.Wait(-1);
				NativeMethods.StudError result;
				try{ result = NativeMethods.RemoveMessagesStartingAt(idx); } finally{ GenerationLock.Release(); }
				Invoke(new MethodInvoker(() => {
					if(result != NativeMethods.StudError.IndexOutOfRange)
						for(var i = _chatMessages.Count - 1; i >= idx; i--){
							_chatMessages[i].Dispose();
							_chatMessages.RemoveAt(i);
						}
					if(result != NativeMethods.StudError.Success){
						ShowErrorMessage(Resources.Error_creating_session, result);
						EndRetokenization();
						return;
					}
					EndRetokenization();
					Generate(role, msg, true);
				}));
			});
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
			if(_generating || !cm.Editing || _retokenizeCount > 0) return;
			var idx = _chatMessages.IndexOf(cm);
			if(idx < 0) return;
			if(cm.checkThink.Checked) cm.Think = cm.richTextMsg.Text;
			else cm.Message = cm.richTextMsg.Text;
			cm.Editing = false;
			cm.Markdown = checkMarkdown.Checked;
			BeginRetokenization();
			ThreadPool.QueueUserWorkItem(_ => {
				GenerationLock.Wait(-1);
				NativeMethods.StudError result;
				try{ result = NativeMethods.SetMessageAt(idx, cm.Think, cm.Message); } finally{ GenerationLock.Release(); }
				Invoke(new MethodInvoker(() => {
					if(result != NativeMethods.StudError.Success) MessageBox.Show(this, Resources.Conversation_too_long_for_context, Resources.LM_Stud, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
					EndRetokenization();
				}));
			});
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
		private void SetStatusMessageVisible(bool visible, string message){
			if(message != null) labelStatusMsg.Text = message;
			toolStripStatusLabel1.Visible = labelTokens.Visible = labelTPS.Visible = labelPreGen.Visible = !visible;
			labelStatusMsg.Visible = visible;
		}
		[Localizable(true)]
		private void UpdateStatusMessage(){
			if(_retokenizeCount > 0) SetStatusMessageVisible(true, Resources.Retokenizing_chat___);
			else if(_isEditing) SetStatusMessageVisible(true, Resources.Editing_transcription_);
			else SetStatusMessageVisible(false, null);
		}
		private void RunOnUiThread(Action action){
			if(IsDisposed) return;
			if(InvokeRequired) Invoke(action);
			else action();
		}
		private void BeginRetokenization(){
			if(Interlocked.Increment(ref _retokenizeCount) != 1) return;
			RunOnUiThread(() => {
				_retokenizeButApplyEnabled = butApply.Enabled;
				_retokenizeButApplyModelSettingsEnabled = butApplyModelSettings.Enabled;
				_retokenizeButGenEnabled = butGen.Enabled;
				_retokenizeButLoadEnabled = butLoad.Enabled;
				_retokenizeButResetEnabled = butReset.Enabled;
				_retokenizeButUnloadEnabled = butUnload.Enabled;
				_retokenizeListViewModelsEnabled = listViewModels.Enabled;
				_retokenizePanelChatEnabled = panelChat.Enabled;
				_retokenizeTextInputEnabled = textInput.Enabled;
				butApply.Enabled = false;
				butApplyModelSettings.Enabled = false;
				butGen.Enabled = false;
				butLoad.Enabled = false;
				butReset.Enabled = false;
				butUnload.Enabled = false;
				listViewModels.Enabled = false;
				panelChat.Enabled = false;
				textInput.Enabled = false;
				UpdateStatusMessage();
			});
		}
		private void EndRetokenization(){
			if(Interlocked.Decrement(ref _retokenizeCount) != 0) return;
			RunOnUiThread(() => {
				butApply.Enabled = _retokenizeButApplyEnabled;
				butApplyModelSettings.Enabled = _retokenizeButApplyModelSettingsEnabled;
				butGen.Enabled = _retokenizeButGenEnabled;
				butLoad.Enabled = _retokenizeButLoadEnabled;
				butReset.Enabled = _retokenizeButResetEnabled;
				butUnload.Enabled = _retokenizeButUnloadEnabled;
				listViewModels.Enabled = _retokenizeListViewModelsEnabled;
				panelChat.Enabled = _retokenizePanelChatEnabled;
				textInput.Enabled = _retokenizeTextInputEnabled;
				UpdateStatusMessage();
			});
		}
		private void StartEditing(){
			if(_isEditing) return;
			_editOriginalText = textInput.Text;
			_isEditing = true;
			UpdateStatusMessage();
			if(checkVoiceInput.CheckState == CheckState.Checked) NativeMethods.StopSpeechTranscription();
		}
		private void FinishEditing(){
			_isEditing = false;
			UpdateStatusMessage();
			if(checkVoiceInput.CheckState != CheckState.Checked) return;
			TryStartSpeechTranscription(true);
		}
		private void CancelEditing(){
			textInput.Text = _editOriginalText;
			textInput.SelectionStart = textInput.Text.Length;
			FinishEditing();
		}
		private void CancelPendingSpeech(){
			_tts.SpeakAsyncCancelAll();
			Interlocked.Exchange(ref _ttsPendingCount, 0);
			_ttsSpeaking = false;
			TryStartSpeechTranscription(false);
		}
		private void QueueSpeech(string text){
			if(string.IsNullOrWhiteSpace(text)) return;
			Interlocked.Increment(ref _ttsPendingCount);
			_tts.SpeakAsync(text);
		}
		private void TryStartSpeechTranscription(bool showErrorOnFailure){
			if(checkVoiceInput.CheckState != CheckState.Checked){
				_voiceInputResumePending = false;
				return;
			}
			_voiceInputResumePending = true;
			if(_isEditing) return;
			if(_generating || _apiGenerating || _ttsSpeaking || Volatile.Read(ref _ttsPendingCount) > 0) return;
			if(NativeMethods.StartSpeechTranscription()){
				_voiceInputResumePending = false;
				return;
			}
			_voiceInputResumePending = false;
			if(showErrorOnFailure){
				MessageBox.Show(this, Resources.Error_starting_voice_input, Resources.LM_Stud, MessageBoxButtons.OK, MessageBoxIcon.Error);
				checkVoiceInput.Checked = false;
				_checkVoiceInputLast = checkVoiceInput.CheckState;
			}
		}
		private void TtsOnSpeakStarted(object sender, SpeakStartedEventArgs e){
			if(IsDisposed) return;
			BeginInvoke((MethodInvoker)(() => {
				if(IsDisposed) return;
				_ttsSpeaking = true;
				if(checkVoiceInput.CheckState == CheckState.Checked){
					NativeMethods.StopSpeechTranscription();
					_voiceInputResumePending = true;
				}
			}));
		}
		private void TtsOnSpeakCompleted(object sender, SpeakCompletedEventArgs e){
			var pending = Interlocked.Decrement(ref _ttsPendingCount);
			if(pending < 0){
				Interlocked.Exchange(ref _ttsPendingCount, 0);
				pending = 0;
			}
			if(IsDisposed) return;
			BeginInvoke((MethodInvoker)(() => {
				if(IsDisposed) return;
				if(pending <= 0){
					_ttsSpeaking = false;
					TryStartSpeechTranscription(false);
				}
			}));
		}
		private void Generate(){
			var prompt = textInput.Text;
			Generate(MessageRole.User, prompt, true);
		}
		private void Generate(MessageRole role, string prompt, bool addToChat){
			var useRemote = _apiClientEnable;
			if((!useRemote && !LlModelLoaded) || string.IsNullOrWhiteSpace(prompt)) return;
			if(!GenerationLock.Wait(0)) return;
			if(_generating || _apiGenerating){
				GenerationLock.Release();
				return;
			}
			if(checkVoiceInput.CheckState == CheckState.Checked) NativeMethods.StopSpeechTranscription();
			_dialecticPaused = false;
			_generating = true;
			foreach(var msg in _chatMessages.Where(msg => msg.Editing)) MsgButEditCancelOnClick(msg);
			butGen.Text = Resources.Stop;
			butReset.Enabled = butApply.Enabled = false;
			var newMsg = prompt.Trim();
			if(addToChat) AddMessage(role, newMsg);
			var seedMsg = checkDialectic.Checked && _chatMessages.Count == 1;
			_cntAssMsg = null;
			_cntToolMsg = null;
			foreach(var message in _chatMessages) message.Generating = true;
			CancelPendingSpeech();
			_firstToken = true;
			if(role == MessageRole.User){
				NativeMethods.SetCommittedText("");
				if(addToChat) textInput.Text = "";
			}
			if(!useRemote && checkDialectic.Checked && !_dialecticStarted && role == MessageRole.User){
				NativeMethods.DialecticStart();
				_dialecticStarted = true;
			}
			if(useRemote){
				ThreadPool.QueueUserWorkItem(o => {GenerateWithApi(role, newMsg, addToChat);});
				return;
			}
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
					if(!string.IsNullOrWhiteSpace(remainingText)) QueueSpeech(remainingText);
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
						if(checkVoiceInput.CheckState == CheckState.Checked) TryStartSpeechTranscription(false);
						if(checkDialectic.Checked && !_dialecticPaused){
							var last = _chatMessages.LastOrDefault();
							if(last != null){
								NativeMethods.DialecticSwap();
								if(seedMsg) NativeMethods.AddMessage(MessageRole.Assistant, prompt);
								Generate(MessageRole.User, last.Message, false);
							}
						}
					}));
				} catch(ObjectDisposedException){} finally{ GenerationLock.Release(); }
			});
		}
		private static string RoleToApiRole(MessageRole role){
			switch(role){
				case MessageRole.Assistant: return "assistant";
				case MessageRole.Tool: return "tool";
				default: return "user";
			}
		}
		private List<ApiClient.ChatMessage> BuildApiMessages(MessageRole role, string prompt, bool addToChat){
			var messages = new List<ApiClient.ChatMessage>();
			var systemPrompt = _systemPrompt;
			if(!string.IsNullOrWhiteSpace(systemPrompt)) messages.Add(new ApiClient.ChatMessage("system", systemPrompt));
			foreach(var msg in _chatMessages){
				if(string.IsNullOrWhiteSpace(msg.Message)) continue;
				messages.Add(new ApiClient.ChatMessage(RoleToApiRole(msg.Role), msg.Message));
			}
			if(!addToChat && !string.IsNullOrWhiteSpace(prompt)) messages.Add(new ApiClient.ChatMessage(RoleToApiRole(role), prompt));
			return messages;
		}
		private void GenerateWithApi(MessageRole role, string prompt, bool addToChat){
			string assistantMsg = null;
			List<string> toolOutputs = null;
			Exception error = null;
			try{
				var messages = BuildApiMessages(role, prompt, addToChat);
				var toolsJson = BuildApiToolsJson();
				var client = new ApiClient(_apiClientURL, _apiClientKey, _apiClientModel);
				var result = client.CreateChatCompletion(messages, _temp, _nGen, toolsJson, null, CancellationToken.None);
				var rounds = 0;
				string lastToolSignature = null;
				while(result.ToolCalls != null && result.ToolCalls.Count > 0 && rounds < 5){
					var toolSignature = string.Join("|", result.ToolCalls.Select(call => $"{call.Id}:{call.Name}:{call.Arguments}"));
					if(toolSignature == lastToolSignature) throw new InvalidOperationException("Repeated tool calls detected.");
					lastToolSignature = toolSignature;
					messages.Add(new ApiClient.ChatMessage("assistant", result.Content){ ToolCalls = result.ToolCalls });
					foreach(var toolCall in result.ToolCalls){
						var toolResult = ExecuteToolCall(toolCall);
						if(toolOutputs == null) toolOutputs = new List<string>();
						toolOutputs.Add(toolResult);
						messages.Add(new ApiClient.ChatMessage("tool", toolResult){ ToolCallId = toolCall.Id });
					}
					result = client.CreateChatCompletion(messages, _temp, _nGen, toolsJson, null, CancellationToken.None);
					rounds++;
				}
				assistantMsg = result.Content;
			} catch(Exception ex){ error = ex; }
			try{
				BeginInvoke(new MethodInvoker(() => {
					if(error != null){ MessageBox.Show(this, error.ToString(), Resources.LM_Stud, MessageBoxButtons.OK, MessageBoxIcon.Error); }
					else{
						if(toolOutputs != null){
							foreach(var output in toolOutputs){
								if(string.IsNullOrWhiteSpace(output)) continue;
								var toolMessage = AddMessage(MessageRole.Tool, output);
								toolMessage.SetRoleText("Tool");
							}
						}
						if(!string.IsNullOrWhiteSpace(assistantMsg)){
							var message = AddMessage(MessageRole.Assistant, assistantMsg);
							message.Generating = false;
							if(_speak) QueueSpeech(assistantMsg);
						}
					}
					butGen.Text = Resources.Generate;
					butReset.Enabled = butApply.Enabled = true;
					_generating = false;
					foreach(var message in _chatMessages) message.Generating = false;
					if(checkVoiceInput.CheckState == CheckState.Checked) TryStartSpeechTranscription(false);
				}));
			} catch(ObjectDisposedException){} finally{ GenerationLock.Release(); }
		}
		internal bool GenerateForApi(byte[] state, string prompt, Action<string> onToken, out byte[] newState, out int tokenCount){
			newState = null;
			tokenCount = 0;
			if(!LlModelLoaded || _generating || _apiGenerating || string.IsNullOrWhiteSpace(prompt)) return false;
			_apiGenerating = true;
			_apiTokenCallback = onToken;
			var originalState = GetState();
			var chatSnapshot = NativeMethods.CaptureChatState();
			try{
				SetState(state);
				NativeMethods.GenerateWithTools(MessageRole.User, prompt, _nGen, false);
				newState = GetState();
				tokenCount = GetTokenCount();
				return true;
			} finally{
				try{ SetState(originalState); } catch{}
				if(chatSnapshot != IntPtr.Zero) try{ NativeMethods.RestoreChatState(chatSnapshot); } finally{ NativeMethods.FreeChatState(chatSnapshot); }
				_apiTokenCallback = null;
				_apiGenerating = false;
			}
		}
		private static int FindSentenceEnd(StringBuilder sb){
			for(var i = 0; i < sb.Length - 1; i++) if((sb[i] == '.' || sb[i] == '!' || sb[i] == '?') && char.IsWhiteSpace(sb[i + 1])) return i;
			return -1;
		}
		internal byte[] GetState(){
			var size = NativeMethods.GetStateSize();
			var data = new byte[size];
			unsafe{
				fixed(byte* p = data){ NativeMethods.GetStateData((IntPtr)p, size); }
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
			if(This._apiGenerating){
				if(messageLen <= 0) return;
				var msg = Encoding.UTF8.GetString(messagePtr, messageLen);
				This._apiTokenCallback?.Invoke(msg);
				return;
			}
			var elapsed = This._swRate.Elapsed.TotalSeconds;
			if(elapsed >= 1.0) This._swRate.Restart();
			var think = "";
			if(thinkLen > 0) think = Encoding.UTF8.GetString(thinkPtr, thinkLen);
			var message = "";
			if(messageLen > 0) message = Encoding.UTF8.GetString(messagePtr, messageLen);
			if(tool == 1 || tool == 2){
				try{
					This.BeginInvoke(new MethodInvoker(() => {
						if(This._cntToolMsg == null){
							This._cntToolMsg = This.AddMessage(MessageRole.Tool, message);
							This._cntToolMsg.SetRoleText("Tool");
						}
						else{ This._cntToolMsg.UpdateText("", message, true); }
						This._cntAssMsg = null;
						This.labelTokens.Text = string.Format(Resources._0___1__2_, tokensTotal, This._cntCtxMax, Resources._Tokens);
						if(tool == 1) This._cntToolMsg = null;
					}));
				} catch(ObjectDisposedException){}
				return;
			}
			This._msgTokenCount += tokenCount;
			This._genTokenTotal += tokenCount;
			var renderToken = !This._rendering;
			try{
				This.BeginInvoke((MethodInvoker)(() => {
					try{
						This._rendering = true;
						if(This._firstToken){
							This.labelPreGen.Text = Resources.First_token_time__ + ftTime + Resources._s;
							This._firstToken = false;
						}
						if(elapsed >= 1.0){
							var callsPerSecond = This._msgTokenCount/elapsed;
							This.labelTPS.Text = string.Format(Resources._0_F2__Tok_s, callsPerSecond);
							This._msgTokenCount = 0;
						}
						if(This._cntAssMsg == null) This._cntAssMsg = This.AddMessage(MessageRole.Assistant, "");
						var lastThink = This._cntAssMsg.checkThink.Checked;
						This._cntAssMsg.UpdateText(think, message, renderToken);
						if(This._speak && !This._cntAssMsg.checkThink.Checked && !lastThink && !string.IsNullOrWhiteSpace(message)){
							var i = This._cntAssMsg.TTSPosition;
							for(; i < This._cntAssMsg.Message.Length; i++){
								var ch = This._cntAssMsg.Message[i];
								if(ch != '`' && ch != '*' && ch != '_' && ch != '#') This._speechBuffer.Append(ch);
							}
							This._cntAssMsg.TTSPosition = i;
							while((i = FindSentenceEnd(This._speechBuffer)) >= 0){
								var sentence = This._speechBuffer.ToString(0, i + 1).Trim();
								if(!string.IsNullOrWhiteSpace(sentence)) This.QueueSpeech(sentence);
								This._speechBuffer.Remove(0, i + 1);
								while(This._speechBuffer.Length > 0 && char.IsWhiteSpace(This._speechBuffer[0])) This._speechBuffer.Remove(0, 1);
							}
						}
						This.labelTokens.Text = string.Format(Resources._0___1__2_, tokensTotal, This._cntCtxMax, Resources._Tokens);
					} catch(ObjectDisposedException){} finally{ This._rendering = false; }
				}));
			} catch(ObjectDisposedException){}
		}
		private static void WhisperCallback(string transcription){
			var thisform = This;
			if(This.IsDisposed) return;
			This.BeginInvoke((MethodInvoker)(() => {
				if(thisform.IsDisposed || This._isEditing) return;
				This.textInput.Text = transcription;
				This.textInput.SelectionStart = This.textInput.Text.Length;
			}));
		}
		private static void SpeechEndCallback(){
			var thisform = This;
			if(This.IsDisposed) return;
			This.BeginInvoke((MethodInvoker)(() => {
				if(thisform.IsDisposed || This._isEditing) return;
				if(This.checkVoiceInput.CheckState != CheckState.Checked) return;
				NativeMethods.StopSpeechTranscription();
				This.Generate();
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