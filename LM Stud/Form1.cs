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
	public partial class Form1 : Form{
		internal static Form1 This;
		private static NativeMethods.TokenCallback _tokenCallback;
		private static NativeMethods.WhisperCallback _whisperCallback;
		private static NativeMethods.SpeechEndCallback _speechEndCallback;
		private readonly ApiServer _apiServer;
		internal readonly List<ChatMessageControl> ChatMessages = new List<ChatMessageControl>();
		internal readonly StringBuilder SpeechBuffer = new StringBuilder();
		private readonly Stopwatch _swRate = new Stopwatch();
		private readonly Stopwatch _swTot = new Stopwatch();
		private readonly SpeechSynthesizer _tts = new SpeechSynthesizer();
		private volatile bool _ttsSpeaking;
		private int _ttsPendingCount;
		private int _retokenizeCount;
		private bool _retokenizeButApplyEnabled;
		private bool _retokenizeButApplyModelSettingsEnabled;
		private bool _retokenizeButGenEnabled;
		private bool _retokenizeButLoadEnabled;
		private bool _retokenizeButResetEnabled;
		private bool _retokenizeButUnloadEnabled;
		private bool _retokenizeListViewModelsEnabled;
		internal readonly SemaphoreSlim GenerationLock = new SemaphoreSlim(1, 1);
		internal volatile bool APIServerGenerating;
		private Action<string> _apiTokenCallback;
		internal CheckState CheckVoiceInputLast = CheckState.Unchecked;
		private ChatMessageControl _cntAssMsg;
		private ChatMessageControl _cntToolMsg;
		private LVColumnClickHandler _columnClickHandler;
		internal string EditOriginalText = "";
		internal bool FirstToken = true;
		internal volatile bool Generating;
		private int _genTokenTotal;
		internal bool IsEditing;
		private int _msgTokenCount;
		private volatile bool _rendering;
		private bool _whisperLoaded;
		internal bool DialecticStarted;
		internal bool DialecticPaused;
		internal Form1(){
			This = this;
			//var culture = new CultureInfo("zh-CN");
			//Thread.CurrentThread.CurrentUICulture = culture;
			//Thread.CurrentThread.CurrentCulture = culture;
			InitializeComponent();
			Icon = Resources.LM_Stud_256;
			_apiServer = new ApiServer(this);
			_tts.SpeakStarted += TtsOnSpeakStarted;
			_tts.SpeakCompleted += TtsOnSpeakCompleted;
			InitializeListViews();
			SetToolTips();
			LoadConfig();
			LoadModelSettings();
		}
		private void SetToolTip(Control control){toolTip1.SetToolTip(control, Resources.ResourceManager.GetString("ToolTip_" + control.Name));}
		private void SetToolTips(){
			SetToolTip(textSystemPrompt);
			SetToolTip(textModelsDir);
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
			LoadModel(Settings.Default.LastModel, true);
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
			if(Generating) NativeMethods.StopGeneration();
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
			if(!Generating) return;
			NativeMethods.StopGeneration();
		}
		private void ButCodeBlock_Click(object sender, EventArgs e){textInput.Paste("```\r\n\r\n```");}
		internal void CheckMarkdown_CheckedChanged(object sender, EventArgs e){
			foreach(var message in ChatMessages) message.Markdown = checkMarkdown.Checked;
		}
		private void CheckVoiceInput_CheckedChanged(object sender, EventArgs e){
			try{
				if(checkVoiceInput.CheckState != CheckState.Unchecked && CheckVoiceInputLast == CheckState.Unchecked){
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
				}
			} finally{ CheckVoiceInputLast = checkVoiceInput.CheckState; }
		}
		private void CheckSpeak_CheckedChanged(object sender, EventArgs e){
			UpdateSetting(ref _speak, checkSpeak.Checked, value => {Settings.Default.Speak = value;});
			Settings.Default.Save();
		}
		internal void CheckDialectic_CheckedChanged(object sender, EventArgs e){
			if(checkDialectic.Checked){
				if(!LlModelLoaded){
					MessageBox.Show(this, Resources.Load_a_model_first_, Resources.LM_Stud, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
					checkDialectic.Checked = false;
					return;
				}
				NativeMethods.DialecticInit();
				DialecticStarted = false;
				DialecticPaused = false;
			} else{
				NativeMethods.DialecticFree();
				DialecticStarted = false;
				DialecticPaused = false;
			}
		}
		internal void ButGen_Click(object sender, EventArgs e){
			if(Generating){
				DialecticPaused = true;
				NativeMethods.StopGeneration();
			} else Generate();
		}
		internal void ButReset_Click(object sender, EventArgs e){
			if(!TryBeginRetokenization()) return;
			ThreadPool.QueueUserWorkItem(_ => {
				try{
					NativeMethods.ResetChat();
					NativeMethods.CloseCommandPrompt();
					NativeMethods.ClearWebCache();
					Invoke(new MethodInvoker(() => {
						panelChat.SuspendLayout();
						foreach(var message in ChatMessages) message.Dispose();
						panelChat.ResumeLayout();
						ChatMessages.Clear();
						labelTokens.Text = NativeMethods.LlamaMemSize() + Resources._Tokens;
						EndRetokenization();
					}));
				} finally{ GenerationLock.Release(); }
			});
		}
		private void TextInput_KeyDown(object sender, KeyEventArgs e){
			if(IsEditing){
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
			if(checkVoiceInput.CheckState != CheckState.Unchecked && !IsEditing) StartEditing();
		}
		private void PanelChat_Layout(object sender, LayoutEventArgs e){
			panelChat.SuspendLayout();
			try{
				for(var i = 0; i < panelChat.Controls.Count; ++i) panelChat.Controls[i].Width = panelChat.ClientSize.Width;
			} finally{ panelChat.ResumeLayout(true); }
		}
		internal void MsgButDeleteOnClick(ChatMessageControl cm){
			if(Generating) return;
			var id = ChatMessages.IndexOf(cm);
			if(id < 0) return;
			if(!TryBeginRetokenization()) return;
			ThreadPool.QueueUserWorkItem(_ => {
				NativeMethods.StudError result;
				try{
					result = NativeMethods.RemoveMessageAt(id);
					Invoke(new MethodInvoker(() => {
						if(result != NativeMethods.StudError.IndexOutOfRange && id < ChatMessages.Count){
							ChatMessages[id].Dispose();
							ChatMessages.RemoveAt(id);
						}
						if(result != NativeMethods.StudError.Success) ShowErrorMessage(Resources.Error_creating_session, result);
						EndRetokenization();
					}));
				} finally{ GenerationLock.Release(); }
			});
		}
		internal void MsgButRegenOnClick(ChatMessageControl cm){
			if(!LlModelLoaded || Generating || !TryBeginRetokenization()) return;
			var idx = ChatMessages.IndexOf(cm);
			if(idx < 0) return;
			while(ChatMessages[idx].Role == MessageRole.Assistant) if(--idx < 0) return;
			var role = ChatMessages[idx].Role;
			var msg = ChatMessages[idx].Message;
			var resetDialecticSeed = checkDialectic.Checked && idx == 0 && role == MessageRole.User;
			ThreadPool.QueueUserWorkItem(_ => {
				NativeMethods.StudError result;
				var regenerate = false;
				try{
					result = NativeMethods.RemoveMessagesStartingAt(idx);
					Invoke(new MethodInvoker(() => {
						if(result != NativeMethods.StudError.IndexOutOfRange)
							for(var i = ChatMessages.Count - 1; i >= idx; i--){
								ChatMessages[i].Dispose();
								ChatMessages.RemoveAt(i);
							}
						if(result != NativeMethods.StudError.Success) ShowErrorMessage(Resources.Error_creating_session, result);
						else regenerate = true;
						EndRetokenization();
					}));
				} finally{ GenerationLock.Release(); }
				if(regenerate)
					BeginInvoke(new MethodInvoker(() => {
						if(IsDisposed) return;
						if(resetDialecticSeed){
							NativeMethods.DialecticInit();
							DialecticStarted = false;
							DialecticPaused = false;
						}
						Generate(role, msg, true);
					}));
			});
		}
		private void MsgButEditOnClick(ChatMessageControl cm){
			if(Generating || cm.Editing) return;
			foreach(var msg in ChatMessages.Where(msg => msg != cm && msg.Editing)) MsgButEditCancelOnClick(msg);
			cm.Editing = true;
			cm.richTextMsg.Focus();
		}
		internal void MsgButEditCancelOnClick(ChatMessageControl cm){
			if(Generating || !cm.Editing) return;
			cm.Editing = false;
			cm.Markdown = checkMarkdown.Checked;
		}
		internal void MsgButEditApplyOnClick(ChatMessageControl cm){
			if(Generating || !cm.Editing) return;
			var idx = ChatMessages.IndexOf(cm);
			if(idx < 0 || !TryBeginRetokenization()) return;
			var oldThink = cm.Think;
			var oldMessage = cm.Message;
			var newText = cm.richTextMsg.Text;
			var newThink = cm.checkThink.Checked ? newText : oldThink;
			var newMessage = cm.checkThink.Checked ? oldMessage : newText;
			ThreadPool.QueueUserWorkItem(_ => {
				NativeMethods.StudError result;
				try{
					result = NativeMethods.SetMessageAt(idx, newThink, newMessage);
					Invoke(new MethodInvoker(() => {
						if(result == NativeMethods.StudError.Success){
							cm.Think = newThink;
							cm.Message = newMessage;
							cm.Editing = false;
							cm.Markdown = checkMarkdown.Checked;
						}
						else MessageBox.Show(this, Resources.Context_full, Resources.LM_Stud, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
						EndRetokenization();
					}));
				} finally{ GenerationLock.Release(); }
			});
		}
		private void RichTextMsgOnMouseWheel(object sender, MouseEventArgs e){NativeMethods.SendMessage(panelChat.Handle, 0x020A, (IntPtr)(((e.Delta/8) << 16) & 0xffff0000), IntPtr.Zero);}
		internal ChatMessageControl AddMessage(MessageRole role, string think, string message, List<ApiClient.ToolCall> toolCalls = null, string toolCallId = null){
			var cm = new ChatMessageControl(role, think, message ?? "", checkMarkdown.Checked){ ApiToolCalls = toolCalls, ApiToolCallId = toolCallId };
			cm.Parent = panelChat;
			cm.Width = panelChat.ClientSize.Width;
			cm.butDelete.Click += (o, args) => MsgButDeleteOnClick(cm);
			cm.butRegen.Click += (o, args) => MsgButRegenOnClick(cm);
			cm.butEdit.Click += (o, args) => MsgButEditOnClick(cm);
			cm.butCancelEdit.Click += (o, args) => MsgButEditCancelOnClick(cm);
			cm.butApplyEdit.Click += (o, args) => MsgButEditApplyOnClick(cm);
			cm.richTextMsg.MouseWheel += RichTextMsgOnMouseWheel;
			cm.richTextMsg.LinkClicked += RichTextMsgOnLinkClicked;
			panelChat.Controls.Add(cm);
			panelChat.ScrollToEnd();
			ChatMessages.Add(cm);
			return cm;
		}
		private static void RichTextMsgOnLinkClicked(object sender, LinkClickedEventArgs e){
			try{
				if (!Uri.TryCreate(e.LinkText, UriKind.Absolute, out var link)) return;
				var psi = new ProcessStartInfo(link.ToString());
				Process.Start(psi);
			} catch{}
		}
		private void SetStatusMessageVisible(bool visible, string message){
			if(message != null) labelStatusMsg.Text = message;
			toolStripStatusLabel1.Visible = labelTokens.Visible = labelTPS.Visible = labelPreGen.Visible = !visible;
			labelStatusMsg.Visible = visible;
		}
		private void UpdateStatusMessage(){
			if(_retokenizeCount > 0) SetStatusMessageVisible(true, Resources.Retokenizing_chat___);
			else if(IsEditing) SetStatusMessageVisible(true, Resources.Editing_transcription_);
			else SetStatusMessageVisible(false, null);
		}
		private void RunOnUiThread(Action action){
			if(IsDisposed) return;
			if(InvokeRequired) Invoke(action);
			else action();
		}
		private NativeMethods.StudError SyncNativeChatMessages(){
			if(IsDisposed) return NativeMethods.StudError.Success;
			var roles = Array.Empty<int>();
			var thinks = Array.Empty<string>();
			var messages = Array.Empty<string>();
			RunOnUiThread(() => {
				var count = ChatMessages.Count;
				roles = new int[count];
				thinks = new string[count];
				messages = new string[count];
				for(var i = 0; i < count; i++){
					var msg = ChatMessages[i];
					roles[i] = (int)msg.Role;
					thinks[i] = msg.Think ?? "";
					messages[i] = msg.Message ?? "";
				}
			});
			return NativeMethods.SyncChatMessages(roles, thinks, messages, roles.Length);
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
				butApply.Enabled = false;
				butApplyModelSettings.Enabled = false;
				butGen.Enabled = false;
				butLoad.Enabled = false;
				butReset.Enabled = false;
				butUnload.Enabled = false;
				listViewModels.Enabled = false;
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
				UpdateStatusMessage();
			});
		}
		private bool TryBeginRetokenization(){
			if(Volatile.Read(ref _retokenizeCount) > 0) return false;
			if(!GenerationLock.Wait(0)) return false;
			if(Volatile.Read(ref _retokenizeCount) > 0){
				GenerationLock.Release();
				return false;
			}
			BeginRetokenization();
			return true;
		}
		private void StartEditing(){
			if(IsEditing) return;
			EditOriginalText = textInput.Text;
			IsEditing = true;
			UpdateStatusMessage();
			if(checkVoiceInput.CheckState == CheckState.Checked) NativeMethods.StopSpeechTranscription();
		}
		private void FinishEditing(){
			IsEditing = false;
			UpdateStatusMessage();
			if(checkVoiceInput.CheckState != CheckState.Checked) return;
			TryStartSpeechTranscription(true);
		}
		private void CancelEditing(){
			textInput.Text = EditOriginalText;
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
			if(checkVoiceInput.CheckState != CheckState.Checked) return;
			if(IsEditing || Generating || APIServerGenerating || _ttsSpeaking || Volatile.Read(ref _ttsPendingCount) > 0) return;
			if(NativeMethods.StartSpeechTranscription()) return;
			if(!showErrorOnFailure) return;
			MessageBox.Show(this, Resources.Error_starting_voice_input, Resources.LM_Stud, MessageBoxButtons.OK, MessageBoxIcon.Error);
			checkVoiceInput.Checked = false;
			CheckVoiceInputLast = checkVoiceInput.CheckState;
		}
		private void TtsOnSpeakStarted(object sender, SpeakStartedEventArgs e){
			if(IsDisposed) return;
			BeginInvoke((MethodInvoker)(() => {
				if(IsDisposed) return;
				_ttsSpeaking = true;
				if(checkVoiceInput.CheckState == CheckState.Checked) NativeMethods.StopSpeechTranscription();
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
				if(pending > 0) return;
				_ttsSpeaking = false;
				TryStartSpeechTranscription(false);
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
			if(Generating || APIServerGenerating){
				GenerationLock.Release();
				return;
			}
			if(checkVoiceInput.CheckState == CheckState.Checked) NativeMethods.StopSpeechTranscription();
			DialecticPaused = false;
			Generating = true;
			foreach(var msg in ChatMessages.Where(msg => msg.Editing)) MsgButEditCancelOnClick(msg);
			butGen.Text = Resources.Stop;
			butReset.Enabled = butApply.Enabled = false;
			var newMsg = prompt.Trim();
			if(addToChat) AddMessage(role, "", newMsg);
			var seedMsg = checkDialectic.Checked && ChatMessages.Count == 1;
			_cntAssMsg = null;
			_cntToolMsg = null;
			foreach(var message in ChatMessages) message.Generating = true;
			CancelPendingSpeech();
			FirstToken = true;
			if(role == MessageRole.User){
				NativeMethods.SetCommittedText("");
				if(addToChat) textInput.Text = "";
			}
			if(!useRemote && checkDialectic.Checked && !DialecticStarted && role == MessageRole.User){
				NativeMethods.DialecticStart();
				DialecticStarted = true;
			}
			if(useRemote){
				ThreadPool.QueueUserWorkItem(o => {GenerateWithApiClient(role, newMsg, addToChat);});
				return;
			}
			ThreadPool.QueueUserWorkItem(o => {
				var lockHeld = true;
				_msgTokenCount = 0;
				_genTokenTotal = 0;
				_swTot.Restart();
				_swRate.Restart();
				var generationError = NativeMethods.GenerateWithTools(role, newMsg, _nGen, checkStream.Checked);
				_swTot.Stop();
				_swRate.Stop();
				if(SpeechBuffer.Length > 0){
					var remainingText = SpeechBuffer.ToString().Trim();
					if(!string.IsNullOrWhiteSpace(remainingText)) QueueSpeech(remainingText);
					SpeechBuffer.Clear();
				}
				try{
					Invoke(new MethodInvoker(() => {
						string followupPrompt = null;
						if(generationError != NativeMethods.StudError.Success){
							if(generationError == NativeMethods.StudError.ContextFull)
								MessageBox.Show(this, Resources.Context_full, Resources.LM_Stud, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
							else MessageBox.Show(this, generationError.ToString(), Resources.LM_Stud, MessageBoxButtons.OK, MessageBoxIcon.Error);
						}
						var elapsed = _swTot.Elapsed.TotalSeconds;
						if(_genTokenTotal > 0 && elapsed > 0.0){
							var callsPerSecond = _genTokenTotal/elapsed;
							labelTPS.Text = string.Format(Resources._0_F2__Tok_s, callsPerSecond);
							_swTot.Reset();
							_swRate.Reset();
						}
						FinishedGenerating();
						if(generationError != NativeMethods.StudError.Success) return;
						if(checkDialectic.Checked && !DialecticPaused){
							var last = ChatMessages.LastOrDefault();
							if(last != null){
								NativeMethods.DialecticSwap();
								if(seedMsg) NativeMethods.AddMessage(MessageRole.Assistant, prompt);
								followupPrompt = last.Message;
							}
						}
						if(string.IsNullOrWhiteSpace(followupPrompt)) return;
						GenerationLock.Release();
						lockHeld = false;
						Generate(MessageRole.User, followupPrompt, false);
					}));
				} catch(ObjectDisposedException){} finally{ if(lockHeld) GenerationLock.Release(); }
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
			foreach(var msg in ChatMessages){
				var hasToolCalls = msg.ApiToolCalls != null && msg.ApiToolCalls.Count > 0;
				if(msg.Role == MessageRole.Tool && string.IsNullOrWhiteSpace(msg.ApiToolCallId)) continue;
				if(string.IsNullOrWhiteSpace(msg.Message) && !hasToolCalls) continue;
				var apiMessage = new ApiClient.ChatMessage(RoleToApiRole(msg.Role), msg.Message);
				if(hasToolCalls) apiMessage.ToolCalls = msg.ApiToolCalls;
				if(!string.IsNullOrWhiteSpace(msg.ApiToolCallId)) apiMessage.ToolCallId = msg.ApiToolCallId;
				messages.Add(apiMessage);
			}
			if(!addToChat && !string.IsNullOrWhiteSpace(prompt)) messages.Add(new ApiClient.ChatMessage(RoleToApiRole(role), prompt));
			return messages;
		}
		private void GenerateWithApiClient(MessageRole role, string prompt, bool addToChat){
			Exception error = null;
			var syncError = NativeMethods.StudError.Success;
			try{
				var messages = BuildApiMessages(role, prompt, addToChat);
				var history = ApiClient.BuildInputItems(messages);
				var toolsJson = BuildApiToolsJson();
				var client = new ApiClient(_apiClientUrl, _apiClientKey, _apiClientModel, APIClientStore, _systemPrompt);
				string lastToolSignature = null;
				while(true){
					var result = client.CreateChatCompletion(history, _temp, _nGen, toolsJson, null, CancellationToken.None);
					ApiClient.AppendOutputItems(history, result);
					var content = result.Content;
					var reasoning = result.Reasoning;
					var toolCalls = result.ToolCalls;
					if(!string.IsNullOrWhiteSpace(content) || (toolCalls != null && toolCalls.Count > 0) || !string.IsNullOrWhiteSpace(reasoning)){
						if(toolCalls != null && toolCalls.Count > 0)
							content = toolCalls.Aggregate(content, (current, toolCall) => current + Resources.__Tool_name_ + toolCall.Name + Resources.__Tool_ID_ + toolCall.Id + Resources.__Tool_arguments_ + toolCall.Arguments);
						try{
							Invoke(new MethodInvoker(() => {
								var message = AddMessage(MessageRole.Assistant, reasoning ?? "", content ?? "", toolCalls);
								if(toolCalls != null && toolCalls.Count > 0) message.SetRoleText(Resources.Tool_Call);
								if(_speak && !string.IsNullOrWhiteSpace(content)) QueueSpeech(content);
							}));
						} catch(ObjectDisposedException){}
					}
					if(toolCalls == null || toolCalls.Count == 0) break;
					var toolSignature = string.Join("|", toolCalls.Select(call => $"{call.Id}:{call.Name}:{call.Arguments}"));
					if(toolSignature == lastToolSignature) throw new InvalidOperationException("Repeated tool calls detected.");
					lastToolSignature = toolSignature;
					foreach(var toolCall in toolCalls){
						var toolResult = ExecuteToolCall(toolCall);
						var toolMessage = new ApiClient.ChatMessage("tool", toolResult){ ToolCallId = toolCall.Id, ToolName = toolCall.Name };
						history.Add(ApiClient.BuildInputMessagePayload(toolMessage));
						if(string.IsNullOrWhiteSpace(toolResult)) continue;
						try{
							Invoke(new MethodInvoker(() => {
								var toolMessageControl = AddMessage(MessageRole.Tool, "", toolResult, null, toolCall.Id);
								toolMessageControl.SetRoleText(Resources.Tool_Output);
							}));
						} catch(ObjectDisposedException){}
					}
				}
				syncError = SyncNativeChatMessages();
			} catch(Exception ex){ error = ex; }
			try{
				Invoke(new MethodInvoker(() => {
					if(error != null) MessageBox.Show(this, error.ToString(), Resources.LM_Stud, MessageBoxButtons.OK, MessageBoxIcon.Error);
					if(error == null && syncError != NativeMethods.StudError.Success){
						if(syncError == NativeMethods.StudError.ContextFull)
							MessageBox.Show(this, Resources.Context_full, Resources.LM_Stud, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
						else MessageBox.Show(this, syncError.ToString(), Resources.LM_Stud, MessageBoxButtons.OK, MessageBoxIcon.Error);
					}
					FinishedGenerating();
				}));
			} catch(ObjectDisposedException){} finally{ GenerationLock.Release(); }
		}
		private void FinishedGenerating(){
			butGen.Text = Resources.Generate;
			butReset.Enabled = butApply.Enabled = true;
			Generating = false;
			foreach(var message in ChatMessages) message.Generating = false;
			if(checkVoiceInput.CheckState == CheckState.Checked) TryStartSpeechTranscription(false);
		}
		internal bool GenerateForApiServer(byte[] state, string prompt, Action<string> onToken, out byte[] newState, out int tokenCount){
			newState = null;
			tokenCount = 0;
			if(string.IsNullOrWhiteSpace(prompt)) return false;
			if(!GenerationLock.Wait(300000)) return false;
			try{
				if(!LlModelLoaded || Generating || APIServerGenerating) return false;
				APIServerGenerating = true;
				_apiTokenCallback = onToken;
				var originalState = GetState();
				var chatSnapshot = NativeMethods.CaptureChatState();
				try{
					SetState(state);
					NativeMethods.GenerateWithTools(MessageRole.User, prompt, _nGen, false);
					newState = GetState();
					tokenCount = NativeMethods.LlamaMemSize();
					return true;
				} finally{
					try{ SetState(originalState); } catch{}
					if(chatSnapshot != IntPtr.Zero) try{ NativeMethods.RestoreChatState(chatSnapshot); } finally{ NativeMethods.FreeChatState(chatSnapshot); }
					_apiTokenCallback = null;
					APIServerGenerating = false;
				}
			} finally{ GenerationLock.Release(); }
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
		private static unsafe void TokenCallback(byte* thinkPtr, int thinkLen, byte* messagePtr, int messageLen, int tokenCount, int tokensTotal, double ftTime, int tool){
			if(This.APIServerGenerating){
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
			if(tool > 0){// 1 == tool output, 2 = CMD output stream, 3 = tool call details
				try{
					This.BeginInvoke(new MethodInvoker(() => {
						if(This._cntToolMsg == null){
							This._cntToolMsg = This.AddMessage(MessageRole.Tool, "", message);
							switch(tool){
								case 1:
								case 2: This._cntToolMsg.SetRoleText(Resources.Tool_Output);
									break;
								case 3: This._cntToolMsg.SetRoleText(Resources.Tool_Call);
									break;
							}
						}
						else{ This._cntToolMsg.UpdateText("", message, true); }
						This._cntAssMsg = null;
						This.labelTokens.Text = string.Format(Resources._0___1__2_, tokensTotal, This._cntCtxMax, Resources._Tokens);
						if(tool == 1 || tool == 3) This._cntToolMsg = null;
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
						if(This.FirstToken){
							This.labelPreGen.Text = Resources.First_token_time__ + ftTime + Resources._s;
							This.FirstToken = false;
						}
						if(elapsed >= 1.0){
							var callsPerSecond = This._msgTokenCount/elapsed;
							This.labelTPS.Text = string.Format(Resources._0_F2__Tok_s, callsPerSecond);
							This._msgTokenCount = 0;
						}
						if(This._cntAssMsg == null) This._cntAssMsg = This.AddMessage(MessageRole.Assistant, "", "");
						var lastThink = This._cntAssMsg.checkThink.Checked;
						This._cntAssMsg.UpdateText(think, message, renderToken);
						if(This._speak && !This._cntAssMsg.checkThink.Checked && !lastThink && !string.IsNullOrWhiteSpace(message)){
							var i = This._cntAssMsg.TTSPosition;
							for(; i < This._cntAssMsg.Message.Length; i++){
								var ch = This._cntAssMsg.Message[i];
								if(ch != '`' && ch != '*' && ch != '_' && ch != '#') This.SpeechBuffer.Append(ch);
							}
							This._cntAssMsg.TTSPosition = i;
							while((i = FindSentenceEnd(This.SpeechBuffer)) >= 0){
								var sentence = This.SpeechBuffer.ToString(0, i + 1).Trim();
								if(!string.IsNullOrWhiteSpace(sentence)) This.QueueSpeech(sentence);
								This.SpeechBuffer.Remove(0, i + 1);
								while(This.SpeechBuffer.Length > 0 && char.IsWhiteSpace(This.SpeechBuffer[0])) This.SpeechBuffer.Remove(0, 1);
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
				if(thisform.IsDisposed || This.IsEditing) return;
				This.textInput.Text = transcription;
				This.textInput.SelectionStart = This.textInput.Text.Length;
			}));
		}
		private static void SpeechEndCallback(){
			var thisform = This;
			if(This.IsDisposed) return;
			This.BeginInvoke((MethodInvoker)(() => {
				if(thisform.IsDisposed || This.IsEditing) return;
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