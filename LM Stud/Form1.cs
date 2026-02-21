using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using LMStud.Properties;
namespace LMStud{
	public partial class Form1 : Form{
		internal static Form1 This;
		internal APIServer ApiServer;
		private static NativeMethods.TokenCallback _tokenCallback;
		private static NativeMethods.WhisperCallback _whisperCallback;
		private static NativeMethods.SpeechEndCallback _speechEndCallback;
		internal readonly List<ChatMessageControl> ChatMessages = new List<ChatMessageControl>();
		internal CheckState CheckVoiceInputLast = CheckState.Unchecked;
		private LVColumnClickHandler _columnClickHandler;
		internal string InputEditOldText = "";
		internal volatile bool Rendering;
		private int _retokenizeCount;
		private bool _whisperLoaded;
		internal bool IsEditing;
		private struct FormControlStates{
			internal static bool ButApplyEnabled;
			internal static bool ButApplyModelSettingsEnabled;
			internal static bool ButGenEnabled;
			internal static bool ButLoadEnabled;
			internal static bool ButResetEnabled;
			internal static bool ButUnloadEnabled;
			internal static bool ListViewModelsEnabled;
		}
		internal Form1(){
			This = this;
			//var culture = new CultureInfo("zh-CN");
			//Thread.CurrentThread.CurrentUICulture = culture;
			//Thread.CurrentThread.CurrentCulture = culture;
			InitializeComponent();
			Icon = Resources.LM_Stud_256;
			Generation.MainForm = this;
			STT.MainForm = this;
			TTS.MainForm = this;
			TTS.SetHandlers();
			InitializeListViews();
			SetToolTips();
			LoadConfig();
			LoadModelSettings();
		}
		private void Form1_Load(object sender, EventArgs e){
			NativeMethods.SetHWnd(Handle);
			NativeMethods.CurlGlobalInit();
			PopulateModels();
			PopulateWhisperModels(true, true);
			NativeMethods.BackendInit();
			if(Common.APIClientEnable) Tools.RegisterTools();
			ApiServer = new APIServer();
			if(Common.APIServerEnable) ApiServer.Start();
			if(!Settings.Default.LoadAuto) return;
			checkLoadAuto.Checked = true;
			ThreadPool.QueueUserWorkItem(o => {
				try{
					_populateLock.Wait(-1);
					var modelPath = Common.ModelsDir + Settings.Default.LastModel;
					var modelLvi = listViewModels.Items.Cast<ListViewItem>().FirstOrDefault(item => item.SubItems[1].Text == modelPath);
					if(modelLvi != null) LoadModel(modelLvi, true);
				} finally{ _populateLock.Release(); }
			});
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
			if(Generation.Generating) Generation.StopActiveGeneration();
			if(_whisperLoaded){
				NativeMethods.StopSpeechTranscription();
				NativeMethods.UnloadWhisperModel();
			}
			ApiServer.Stop();
			NativeMethods.CloseCommandPrompt();
			NativeMethods.CurlGlobalCleanup();
		}
		private void Form1_KeyDown(object sender, KeyEventArgs e){
			if(e.KeyCode != Keys.Escape) return;
			TTS.CancelPendingSpeech();
			if(!Generation.Generating) return;
			Generation.StopActiveGeneration();
		}
		private void ButCodeBlock_Click(object sender, EventArgs e){textInput.Paste("```\r\n\r\n```");}
		internal void CheckMarkdown_CheckedChanged(object sender, EventArgs e){
			foreach(var message in ChatMessages) message.Markdown = checkMarkdown.Checked;
		}
		private void CheckVoiceInput_CheckedChanged(object sender, EventArgs e){
			try{
				if(checkVoiceInput.CheckState != CheckState.Unchecked && CheckVoiceInputLast == CheckState.Unchecked){
					var vadModelPath = Common.ModelsDir + Common.VADModel;
					var whisperModelPath = Common.ModelsDir + Common.WhisperModel;
					if(!File.Exists(whisperModelPath)){
						checkVoiceInput.Checked = false;
						MessageBox.Show(this, Resources.Error_Whisper_model_not_found, Resources.LM_Stud, MessageBoxButtons.OK, MessageBoxIcon.Error);
						tabControl1.SelectTab(1);
						comboWhisperModel.Focus();
						return;
					}
					if(Common.UseWhisperVAD)
						if(!File.Exists(vadModelPath)){
							if(MessageBox.Show(this, Resources.VAD_model_not_found__use_Basic_VAD_, Resources.LM_Stud, MessageBoxButtons.OKCancel, MessageBoxIcon.Exclamation) != DialogResult.OK){
								checkVoiceInput.Checked = false;
								return;
							}
							radioBasicVAD.Checked = true;
							Common.UseWhisperVAD = Settings.Default.UseWhisperVAD = false;
							Settings.Default.Save();
						}
					if(!_whisperLoaded){
						var result = LoadWhisperModel(whisperModelPath, Common.NThreads, Common.WhisperUseGPU, Common.UseWhisperVAD, Common.UseWhisperVAD ? vadModelPath : "");
						if(result == NativeMethods.StudError.Success){
							_whisperLoaded = true;
							_whisperCallback = STT.WhisperCallback;
							NativeMethods.SetWhisperCallback(_whisperCallback);
							_speechEndCallback = STT.SpeechEndCallback;
							NativeMethods.SetSpeechEndCallback(_speechEndCallback);
						} else{
							_whisperLoaded = false;
							MessageBox.Show(this, Resources.Error_initialising_voice_input, Resources.LM_Stud, MessageBoxButtons.OK, MessageBoxIcon.Error);
							checkVoiceInput.Checked = false;
							return;
						}
					}
					STT.RequestStart(true);
				} else{
					NativeMethods.StopSpeechTranscription();
				}
			} finally{ CheckVoiceInputLast = checkVoiceInput.CheckState; }
		}
		private void CheckSpeak_CheckedChanged(object sender, EventArgs e){
			UpdateSetting(ref Common.Speak, checkSpeak.Checked, value => {Settings.Default.Speak = value;});
			Settings.Default.Save();
		}
		internal void CheckDialectic_CheckedChanged(object sender, EventArgs e){
			if(checkDialectic.Checked){
				if(!Common.LlModelLoaded){
					MessageBox.Show(this, Resources.Load_a_model_first_, Resources.LM_Stud, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
					checkDialectic.Checked = false;
					return;
				}
				var err = NativeMethods.DialecticInit();
				if(err != NativeMethods.StudError.Success){
					ShowError(Resources.Dialectic_enable, err);
					return;
				}
			}
			else{ NativeMethods.DialecticFree(); }
			Generation.DialecticStarted = false;
			Generation.DialecticPaused = false;
		}
		internal void ButGen_Click(object sender, EventArgs e){
			if(Generation.Generating){
				Generation.DialecticPaused = true;
				Generation.StopActiveGeneration();
			} else Generation.Generate();
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
				} finally{ Generation.GenerationLock.Release(); }
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
			} else if(e.KeyCode != Keys.Enter && e.KeyCode != Keys.Escape && checkVoiceInput.CheckState != CheckState.Unchecked){ StartEditing(); } else if(e.KeyCode == Keys.Enter && !e.Control && !e.Shift && butGen.Enabled){
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
			if(Generation.Generating) return;
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
						if(result != NativeMethods.StudError.Success) ShowError(Resources.Error_deleting_message, result);
						EndRetokenization();
					}));
				} finally{ Generation.GenerationLock.Release(); }
			});
		}
		internal void MsgButRegenOnClick(ChatMessageControl cm){
			if(!Common.LlModelLoaded || Generation.Generating || !TryBeginRetokenization()) return;
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
						if(result != NativeMethods.StudError.Success) ShowError(Resources.Error_regenerating_message, result);
						else regenerate = true;
						EndRetokenization();
					}));
				} finally{ Generation.GenerationLock.Release(); }
				if(regenerate)
					BeginInvoke(new MethodInvoker(() => {
						if(IsDisposed) return;
						if(resetDialecticSeed){
							NativeMethods.DialecticInit();
							Generation.DialecticStarted = false;
							Generation.DialecticPaused = false;
						}
						Generation.Generate(role, msg, true);
					}));
			});
		}
		private void MsgButEditOnClick(ChatMessageControl cm){
			if(Generation.Generating || cm.Editing) return;
			foreach(var msg in ChatMessages.Where(msg => msg != cm && msg.Editing)) MsgButEditCancelOnClick(msg);
			cm.Editing = true;
			cm.richTextMsg.Focus();
		}
		internal void MsgButEditCancelOnClick(ChatMessageControl cm){
			if(Generation.Generating || !cm.Editing) return;
			cm.Editing = false;
			cm.Markdown = checkMarkdown.Checked;
		}
		internal void MsgButEditApplyOnClick(ChatMessageControl cm){
			if(Generation.Generating || !cm.Editing) return;
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
				} finally{ Generation.GenerationLock.Release(); }
			});
		}
		private void RichTextMsgOnMouseWheel(object sender, MouseEventArgs e){NativeMethods.SendMessage(panelChat.Handle, 0x020A, (IntPtr)(((e.Delta/8) << 16) & 0xffff0000), IntPtr.Zero);}
		internal ChatMessageControl AddMessage(MessageRole role, string think, string message, List<APIClient.ToolCall> toolCalls = null, string toolCallId = null){
			var cm = new ChatMessageControl(role, think, message ?? "", checkMarkdown.Checked){ ApiToolCalls = toolCalls, ApiToolCallId = toolCallId };
			cm.Parent = panelChat;
			cm.Width = panelChat.ClientSize.Width;
			cm.butDelete.Click += (o, args) => MsgButDeleteOnClick(cm);
			cm.butRegen.Click += (o, args) => MsgButRegenOnClick(cm);
			cm.butEdit.Click += (o, args) => MsgButEditOnClick(cm);
			cm.butCancelEdit.Click += (o, args) => MsgButEditCancelOnClick(cm);
			cm.butApplyEdit.Click += (o, args) => MsgButEditApplyOnClick(cm);
			cm.richTextMsg.MouseWheel += RichTextMsgOnMouseWheel;
			panelChat.Controls.Add(cm);
			panelChat.ScrollToEnd();
			ChatMessages.Add(cm);
			return cm;
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
		internal void RunOnUiThread(Action action){
			if(IsDisposed) return;
			if(InvokeRequired) Invoke(action);
			else action();
		}
		private void BeginRetokenization(){
			if(Interlocked.Increment(ref _retokenizeCount) != 1) return;
			RunOnUiThread(() => {
				FormControlStates.ButApplyEnabled = butApply.Enabled;
				FormControlStates.ButApplyModelSettingsEnabled = butApplyModelSettings.Enabled;
				FormControlStates.ButGenEnabled = butGen.Enabled;
				FormControlStates.ButLoadEnabled = butLoad.Enabled;
				FormControlStates.ButResetEnabled = butReset.Enabled;
				FormControlStates.ButUnloadEnabled = butUnload.Enabled;
				FormControlStates.ListViewModelsEnabled = listViewModels.Enabled;
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
				butApply.Enabled = FormControlStates.ButApplyEnabled;
				butApplyModelSettings.Enabled = FormControlStates.ButApplyModelSettingsEnabled;
				butGen.Enabled = FormControlStates.ButGenEnabled;
				butLoad.Enabled = FormControlStates.ButLoadEnabled;
				butReset.Enabled = FormControlStates.ButResetEnabled;
				butUnload.Enabled = FormControlStates.ButUnloadEnabled;
				listViewModels.Enabled = FormControlStates.ListViewModelsEnabled;
				UpdateStatusMessage();
			});
		}
		private bool TryBeginRetokenization(){
			if(Volatile.Read(ref _retokenizeCount) > 0) return false;
			if(!Generation.GenerationLock.Wait(0)) return false;
			if(Volatile.Read(ref _retokenizeCount) > 0){
				Generation.GenerationLock.Release();
				return false;
			}
			BeginRetokenization();
			return true;
		}
		internal void StartEditing(){
			if(IsEditing) return;
			InputEditOldText = textInput.Text;
			IsEditing = true;
			UpdateStatusMessage();
			if(checkVoiceInput.CheckState == CheckState.Checked) NativeMethods.StopSpeechTranscription();
		}
		internal void FinishEditing(){
			IsEditing = false;
			UpdateStatusMessage();
			if(checkVoiceInput.CheckState != CheckState.Checked) return;
			STT.RequestStart(true);
		}
		internal void CancelEditing(){
			textInput.Text = InputEditOldText;
			textInput.SelectionStart = textInput.Text.Length;
			FinishEditing();
		}
		internal void FinishedGenerating(){
			butGen.Text = Resources.Generate;
			butReset.Enabled = butApply.Enabled = true;
			Generation.Generating = false;
			foreach(var message in ChatMessages) message.Generating = false;
			if(checkVoiceInput.CheckState == CheckState.Checked) STT.RequestStart(false);
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