using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using LMStud.Properties;
namespace LMStud{
	public partial class Form1{
		private bool _apiClientEnable;
		private string _apiClientKey;
		private string _apiClientModel;
		private string _apiClientURL;
		private bool _apiServerEnable;
		private int _apiServerPort;
		private int _batchSize;
		private bool _cmdEnable;
		private int _cmdTimeoutMs;
		private int _ctxSize;
		private bool _dateTimeEnable;
		private string _fileBaseDir;
		private bool _fileCreateEnable;
		private bool _fileListEnable;
		private bool _fileReadEnable;
		private bool _fileWriteEnable;
		private CheckState _flashAttn;
		private float _freqThreshold;
		private int _genDelay;
		private string _googleAPIKey;
		private bool _googleSearchEnable;
		private string _googleSearchID;
		private int _googleSearchResultCount;
		private int _gpuLayers;
		private float _minP;
		private bool _mLock;
		private bool _mMap;
		private string _modelsPath;
		private int _nGen;
		private int _nThreads;
		private int _nThreadsBatch;
		private NativeMethods.GgmlNumaStrategy _numaStrat;
		private float _repPen;
		private bool _speak;
		private string _systemPrompt;
		private float _temp;
		private int _topK;
		private float _topP;
		private bool _useWhisperVAD;
		private int _vadModelIndex;
		private float _vadThreshold;
		private string _wakeWord;
		private float _wakeWordSimilarity;
		private bool _webpageFetchEnable;
		private int _whisperModelIndex;
		private float _whisperTemp;
		private bool _whisperUseGPU;
		private void LoadConfig(){
			_systemPrompt = textSystemPrompt.Text = Settings.Default.SystemPrompt;
			_modelsPath = textModelsPath.Text = Settings.Default.ModelsDir;
			_ctxSize = (int)(numCtxSize.Value = Settings.Default.CtxSize);
			_gpuLayers = (int)(numGPULayers.Value = Settings.Default.GPULayers);
			_temp = (float)(numTemp.Value = Settings.Default.Temp);
			_nGen = (int)(numNGen.Value = Settings.Default.NGen);
			_numaStrat = (NativeMethods.GgmlNumaStrategy)(comboNUMAStrat.SelectedIndex = Settings.Default.NUMAStrat);
			_repPen = (float)(numRepPen.Value = Settings.Default.RepPen);
			_topK = (int)(numTopK.Value = Settings.Default.TopK);
			_topP = (float)(numTopP.Value = Settings.Default.TopP);
			_minP = (float)(numMinP.Value = Settings.Default.MinP);
			_batchSize = (int)(numBatchSize.Value = Settings.Default.BatchSize);
			_mMap = checkMMap.Checked = Settings.Default.MMap;
			_mLock = checkMLock.Checked = Settings.Default.MLock;
			_nThreads = (int)(numThreads.Value = Settings.Default.Threads);
			_nThreadsBatch = (int)(numThreadsBatch.Value = Settings.Default.ThreadsBatch);
			_wakeWord = textWakeWord.Text = Settings.Default.WakeWord;
			_wakeWordSimilarity = (float)(numWakeWordSimilarity.Value = Settings.Default.WakeWordSimilarity);
			try{ _vadThreshold = (float)(numVadThreshold.Value = Settings.Default.VadThreshold); } catch(ArgumentOutOfRangeException){ _vadThreshold = (float)numVadThreshold.Value; }
			_freqThreshold = (float)(numFreqThreshold.Value = Settings.Default.FreqThreshold);
			_whisperUseGPU = checkWhisperUseGPU.Checked = Settings.Default.whisperUseGPU;
			_whisperTemp = (float)(numWhisperTemp.Value = Settings.Default.WhisperTemp);
			_useWhisperVAD = radioWhisperVAD.Checked = Settings.Default.UseWhisperVAD;
			_speak = checkSpeak.Checked = Settings.Default.Speak;
			_flashAttn = checkFlashAttn.CheckState = (CheckState)Settings.Default.FlashAttn;
			_googleAPIKey = textGoogleApiKey.Text = Settings.Default.GoogleAPIKey;
			_googleSearchID = textGoogleSearchID.Text = Settings.Default.GoogleSearchID;
			_googleSearchResultCount = (int)(numGoogleResults.Value = Settings.Default.GoogleSearchResultCount);
			_googleSearchEnable = checkGoogleEnable.Checked = Settings.Default.GoogleSearchEnable;
			_webpageFetchEnable = checkWebpageFetchEnable.Checked = Settings.Default.WebpageFetchEnable;
			_fileBaseDir = textFileBasePath.Text = Settings.Default.FileBaseDir;
			_fileListEnable = checkFileListEnable.Checked = Settings.Default.FileListEnable;
			_fileCreateEnable = checkFileCreateEnable.Checked = Settings.Default.FileCreateEnable;
			_fileReadEnable = checkFileReadEnable.Checked = Settings.Default.FileReadEnable;
			_fileWriteEnable = checkFileWriteEnable.Checked = Settings.Default.FileWriteEnable;
			_dateTimeEnable = checkDateTimeEnable.Checked = Settings.Default.DateTimeEnable;
			_cmdEnable = checkCMDEnable.Checked = Settings.Default.CMDToolEnable;
			_cmdTimeoutMs = (int)(numCmdTimeout.Value = Settings.Default.CMDToolTimeoutMs);
			_apiServerEnable = checkApiServerEnable.Checked = Settings.Default.ApiServerEnable;
			_apiServerPort = (int)(numApiServerPort.Value = Settings.Default.ApiServerPort);
			_genDelay = (int)(numGenDelay.Value = Settings.Default.GenDelay);
			_apiClientEnable = checkApiClientEnable.Checked = Settings.Default.ApiClientEnable;
			_apiClientURL = textApiClientUrl.Text = Settings.Default.ApiClientBaseUrl;
			_apiClientKey = textApiClientKey.Text = Settings.Default.ApiClientKey;
			_apiClientModel = comboApiClientModel.Text = Settings.Default.ApiClientModel;
			NativeMethods.SetSilenceTimeout(_genDelay);
			NativeMethods.SetFileBaseDir(_fileBaseDir);
			NativeMethods.SetWakeCommand(_wakeWord);
			NativeMethods.SetVADThresholds(_vadThreshold, _freqThreshold);
			NativeMethods.SetWakeWordSimilarity(_wakeWordSimilarity);
			NativeMethods.SetWhisperTemp(_whisperTemp);
			NativeMethods.SetGoogle(_googleAPIKey, _googleSearchID, _googleSearchResultCount);
			NativeMethods.SetCommandPromptTimeout(_cmdTimeoutMs);
			SetModelStatus();
			if(_apiServerEnable){
				_apiServer.Port = _apiServerPort;
				_apiServer.Start();
			}
			if(_apiClientEnable) RegisterTools();
		}
		private void UpdateSetting<T>(ref T currentValue, T newValue, Action<T> updateAction){
			if(EqualityComparer<T>.Default.Equals(currentValue, newValue)) return;
			currentValue = newValue;
			updateAction(newValue);
		}
		private void ButApply_Click(object sender, EventArgs e){
			var reloadModel = false;
			var reloadCtx = false;
			var reloadSmpl = false;
			var reloadWhisper = false;
			var setVAD = false;
			var setWWS = false;
			var setGoogle = false;
			var registerTools = false;
			var setSystemPrompt = false;
			var modelOverrideChanged = false;
			var setStatusLabel = false;
			ModelSettings ms = default;
			var overrideSettings = LlModelLoaded && _modelSettings.TryGetValue(_models[_modelIndex].FilePath, out ms) && ms.OverrideSettings;
			UpdateSetting(ref _systemPrompt, textSystemPrompt.Text, value => {
				Settings.Default.SystemPrompt = value;
				if(overrideSettings){
					modelOverrideChanged = true;
					return;
				}
				setSystemPrompt = true;
			});
			UpdateSetting(ref _modelsPath, textModelsPath.Text, value => {
				Settings.Default.ModelsDir = value;
				PopulateModels();
				PopulateWhisperModels(true, true);
			});
			UpdateSetting(ref _ctxSize, (int)numCtxSize.Value, value => {
				Settings.Default.CtxSize = value;
				if(overrideSettings){
					modelOverrideChanged = true;
					return;
				}
				if(!LlModelLoaded) return;
				if(_modelCtxMax <= 0) _cntCtxMax = _ctxSize;
				else _cntCtxMax = _ctxSize > _modelCtxMax ? _modelCtxMax : _ctxSize;
				reloadCtx = true;
			});
			UpdateSetting(ref _gpuLayers, (int)numGPULayers.Value, value => {
				Settings.Default.GPULayers = value;
				if(overrideSettings){
					modelOverrideChanged = true;
					return;
				}
				reloadModel = true;
			});
			UpdateSetting(ref _temp, (float)numTemp.Value, value => {
				Settings.Default.Temp = numTemp.Value;
				if(overrideSettings){
					modelOverrideChanged = true;
					return;
				}
				reloadSmpl = true;
			});
			UpdateSetting(ref _nGen, (int)numNGen.Value, value => {Settings.Default.NGen = value;});
			var newNumaIndex = comboNUMAStrat.SelectedIndex;
			UpdateSetting(ref _numaStrat, (NativeMethods.GgmlNumaStrategy)newNumaIndex, value => {
				Settings.Default.NUMAStrat = newNumaIndex;
				reloadModel = true;
			});
			UpdateSetting(ref _repPen, (float)numRepPen.Value, value => {
				Settings.Default.RepPen = numRepPen.Value;
				reloadSmpl = true;
			});
			UpdateSetting(ref _topK, (int)numTopK.Value, value => {
				Settings.Default.TopK = value;
				if(overrideSettings){
					modelOverrideChanged = true;
					return;
				}
				reloadSmpl = true;
			});
			UpdateSetting(ref _topP, (float)numTopP.Value, value => {
				Settings.Default.TopP = numTopP.Value;
				if(overrideSettings){
					modelOverrideChanged = true;
					return;
				}
				reloadSmpl = true;
			});
			UpdateSetting(ref _minP, (float)numMinP.Value, value => {
				Settings.Default.MinP = numMinP.Value;
				if(overrideSettings){
					modelOverrideChanged = true;
					return;
				}
				reloadSmpl = true;
			});
			UpdateSetting(ref _batchSize, (int)numBatchSize.Value, value => {
				Settings.Default.BatchSize = value;
				reloadCtx = true;
			});
			UpdateSetting(ref _mMap, checkMMap.Checked, value => {
				Settings.Default.MMap = value;
				reloadModel = true;
			});
			UpdateSetting(ref _mLock, checkMLock.Checked, value => {
				Settings.Default.MLock = value;
				reloadModel = true;
			});
			if(_nThreads != (int)numThreads.Value || _nThreadsBatch != (int)numThreadsBatch.Value){
				UpdateSetting(ref _nThreads, (int)numThreads.Value, value => {Settings.Default.Threads = value;});
				UpdateSetting(ref _nThreadsBatch, (int)numThreadsBatch.Value, value => {Settings.Default.ThreadsBatch = value;});
				NativeMethods.SetThreadCount((int)numThreads.Value, (int)numThreadsBatch.Value);
			}
			UpdateSetting(ref _whisperModelIndex, comboWhisperModel.SelectedIndex, value => {
				Settings.Default.WhisperModel = comboWhisperModel.Text;
				reloadWhisper = true;
			});
			UpdateSetting(ref _wakeWord, textWakeWord.Text, value => {
				Settings.Default.WakeWord = value;
				NativeMethods.SetWakeCommand(value);
			});
			UpdateSetting(ref _wakeWordSimilarity, (float)numWakeWordSimilarity.Value, value => {
				Settings.Default.WakeWordSimilarity = numWakeWordSimilarity.Value;
				setWWS = true;
			});
			UpdateSetting(ref _freqThreshold, (float)numFreqThreshold.Value, value => {
				Settings.Default.FreqThreshold = numFreqThreshold.Value;
				setVAD = true;
			});
			UpdateSetting(ref _whisperTemp, (float)numWhisperTemp.Value, value => {
				Settings.Default.WhisperTemp = numWhisperTemp.Value;
				reloadWhisper = true;
			});
			UpdateSetting(ref _whisperUseGPU, checkWhisperUseGPU.Checked, value => {
				Settings.Default.whisperUseGPU = value;
				reloadWhisper = true;
			});
			UpdateSetting(ref _useWhisperVAD, radioWhisperVAD.Checked, value => {
				Settings.Default.UseWhisperVAD = value;
				reloadWhisper = true;
			});
			UpdateSetting(ref _vadModelIndex, comboVADModel.SelectedIndex, value => {
				Settings.Default.VADModel = comboVADModel.Text;
				reloadWhisper = true;
			});
			UpdateSetting(ref _vadThreshold, (float)numVadThreshold.Value, value => {
				Settings.Default.VadThreshold = numVadThreshold.Value;
				setVAD = true;
			});
			UpdateSetting(ref _flashAttn, checkFlashAttn.CheckState, value => {
				Settings.Default.FlashAttn = (uint)value;
				if(overrideSettings){
					modelOverrideChanged = true;
					return;
				}
				reloadCtx = true;
			});
			UpdateSetting(ref _googleAPIKey, textGoogleApiKey.Text, value => {
				Settings.Default.GoogleAPIKey = value;
				setGoogle = true;
			});
			UpdateSetting(ref _googleSearchID, textGoogleSearchID.Text, value => {
				Settings.Default.GoogleSearchID = value;
				setGoogle = true;
			});
			UpdateSetting(ref _googleSearchResultCount, (int)numGoogleResults.Value, value => {
				Settings.Default.GoogleSearchResultCount = value;
				setGoogle = true;
			});
			UpdateSetting(ref _googleSearchEnable, checkGoogleEnable.Checked, value => {
				Settings.Default.GoogleSearchEnable = value;
				registerTools = true;
			});
			UpdateSetting(ref _webpageFetchEnable, checkWebpageFetchEnable.Checked, value => {
				Settings.Default.WebpageFetchEnable = value;
				registerTools = true;
			});
			UpdateSetting(ref _fileBaseDir, textFileBasePath.Text, value => {
				Settings.Default.FileBaseDir = value;
				NativeMethods.SetFileBaseDir(value);
			});
			UpdateSetting(ref _fileListEnable, checkFileListEnable.Checked, value => {
				Settings.Default.FileListEnable = value;
				registerTools = true;
			});
			UpdateSetting(ref _fileCreateEnable, checkFileCreateEnable.Checked, value => {
				Settings.Default.FileCreateEnable = value;
				registerTools = true;
			});
			UpdateSetting(ref _fileReadEnable, checkFileReadEnable.Checked, value => {
				Settings.Default.FileReadEnable = value;
				registerTools = true;
			});
			UpdateSetting(ref _fileWriteEnable, checkFileWriteEnable.Checked, value => {
				Settings.Default.FileWriteEnable = value;
				registerTools = true;
			});
			UpdateSetting(ref _dateTimeEnable, checkDateTimeEnable.Checked, value => {
				Settings.Default.DateTimeEnable = value;
				registerTools = true;
			});
			UpdateSetting(ref _cmdEnable, checkCMDEnable.Checked, value => {
				Settings.Default.CMDToolEnable = value;
				registerTools = true;
			});
			UpdateSetting(ref _cmdTimeoutMs, (int)numCmdTimeout.Value, value => {
				Settings.Default.CMDToolTimeoutMs = value;
				NativeMethods.SetCommandPromptTimeout(value);
			});
			UpdateSetting(ref _apiServerPort, (int)numApiServerPort.Value, value => {
				Settings.Default.ApiServerPort = value;
				if(_apiServerEnable){
					_apiServer.Stop();
					_apiServer.Port = value;
					_apiServer.Start();
				}
			});
			UpdateSetting(ref _apiServerEnable, checkApiServerEnable.Checked, value => {
				Settings.Default.ApiServerEnable = value;
				if(value){
					_apiServer.Port = _apiServerPort;
					_apiServer.Start();
				}
				else{ _apiServer.Stop(); }
			});
			UpdateSetting(ref _genDelay, (int)numGenDelay.Value, value => {
				Settings.Default.GenDelay = value;
				NativeMethods.SetSilenceTimeout(value);
			});
			UpdateSetting(ref _apiClientEnable, checkApiClientEnable.Checked, value => {
				Settings.Default.ApiClientEnable = value;
				setStatusLabel = true;
			});
			UpdateSetting(ref _apiClientURL, textApiClientUrl.Text, value => {Settings.Default.ApiClientBaseUrl = value;});
			UpdateSetting(ref _apiClientKey, textApiClientKey.Text, value => {Settings.Default.ApiClientKey = value;});
			UpdateSetting(ref _apiClientModel, comboApiClientModel.Text, value => {
				Settings.Default.ApiClientModel = value;
				setStatusLabel = true;
			});
			var flash = overrideSettings ? ms.FlashAttn : _flashAttn;
			var minP = overrideSettings ? ms.MinP : _minP;
			var topP = overrideSettings ? ms.TopP : _topP;
			var topK = overrideSettings ? ms.TopK : _topK;
			var temp = overrideSettings ? ms.Temp : _temp;
			if(LlModelLoaded)
				if(reloadModel &&
					MessageBox.Show(this, Resources.A_changed_setting_requires_the_model_to_be_reloaded__reload_now_, Resources.LM_Stud, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes){ LoadModel(_modelIndex, false); }
				else{
					if(reloadCtx) CreateContext(_cntCtxMax, _batchSize, flash, _nThreads, _nThreadsBatch);
					if(reloadSmpl) CreateSampler(minP, topP, topK, temp, _repPen);
				}
			if(setVAD) NativeMethods.SetVADThresholds(_vadThreshold, _freqThreshold);
			if(setWWS) NativeMethods.SetWakeWordSimilarity(_wakeWordSimilarity);
			if(reloadWhisper && _whisperLoaded){
				if(_whisperModelIndex < 0 || !File.Exists(_whisperModels[_whisperModelIndex])){
					checkVoiceInput.Checked = false;
					MessageBox.Show(this, Resources.Error_Whisper_model_not_found, Resources.LM_Stud, MessageBoxButtons.OK, MessageBoxIcon.Error);
				}
				else{
					if(checkVoiceInput.CheckState != CheckState.Unchecked) NativeMethods.StopSpeechTranscription();
					NativeMethods.LoadWhisperModel(_whisperModels[_whisperModelIndex], _nThreads, _whisperUseGPU, _useWhisperVAD, _whisperModels[_vadModelIndex]);
					if(checkVoiceInput.CheckState != CheckState.Unchecked) NativeMethods.StartSpeechTranscription();
				}
			}
			if(setGoogle) NativeMethods.SetGoogle(_googleAPIKey, _googleSearchID, _googleSearchResultCount);
			if(registerTools) RegisterTools();
			if(setStatusLabel) SetModelStatus();
			if(registerTools || setSystemPrompt) ThreadPool.QueueUserWorkItem(o => {SetSystemPrompt();});
			if(overrideSettings && modelOverrideChanged) MessageBox.Show(this, Resources.The_modified_settings_are_overridden_by_the_Model_Settings_for_this_model_, Resources.LM_Stud, MessageBoxButtons.OK, MessageBoxIcon.Information);
			Settings.Default.Save();
		}
		private void ButBrowse_Click(object sender, EventArgs e){
			if(folderBrowserDialog1.ShowDialog(this) == DialogResult.OK) textModelsPath.Text = folderBrowserDialog1.SelectedPath;
		}
		private void TextModelsPath_KeyDown(object sender, KeyEventArgs e){
			if(e.KeyCode != Keys.Enter) return;
			var path = textModelsPath.Text.Trim();
			if(Directory.Exists(path)) textModelsPath.Text = path;
			else MessageBox.Show(this, Resources.Folder_not_found, Resources.LM_Stud, MessageBoxButtons.OK, MessageBoxIcon.Error);
		}
		private void ComboWhisperModel_DropDown(object sender, EventArgs e){
			if(!Directory.Exists(_modelsPath)) return;
			PopulateWhisperModels(true, false);
		}
		private void ButWhispDown_Click(object sender, EventArgs e){
			HugLoadFiles("ggerganov", "whisper.cpp", ".bin");
			tabControl1.SelectTab(3);
		}
		private void LinkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e){
			textSystemPrompt.SelectAll();
			textSystemPrompt.Paste(@"The list_directory and file tools operate relative to a base directory.
The file tools use 1-based line numbers.
First use list_directory with an empty path before using the file tools to help with coding tasks or other file changes.
Always read a file and verify its contents before making changes.");
		}
		private void ButDownloadVADModel_Click(object sender, EventArgs e){
			HugLoadFiles("ggml-org", "whisper-vad", ".bin");
			tabControl1.SelectTab(3);
		}
		private void ComboVADModel_DropDown(object sender, EventArgs e){
			if(!Directory.Exists(_modelsPath)) return;
			PopulateWhisperModels(false, true);
		}
		private void PopulateWhisperModels(bool whisper, bool vad){
			if(!ModelsFolderExists(false)) return;
			UseWaitCursor = true;
			try{
				_whisperModels.Clear();
				if(whisper) comboWhisperModel.Items.Clear();
				if(vad) comboVADModel.Items.Clear();
				var files = Directory.GetFiles(_modelsPath, "*.bin", SearchOption.AllDirectories);
				foreach(var file in files){
					using(var fs = new FileStream(file, FileMode.Open, FileAccess.Read))
					using(var br = new BinaryReader(fs)){
						var magic = br.ReadUInt32();
						if(magic != 0x67676D6C)// "lmgg" in little-endian
							continue;
					}
					_whisperModels.Add(file);
					if(whisper) comboWhisperModel.Items.Add(Path.GetFileName(file));
					if(vad) comboVADModel.Items.Add(Path.GetFileName(file));
				}
				if(whisper){
					comboWhisperModel.SelectedIndex = comboWhisperModel.Items.IndexOf(Settings.Default.WhisperModel);
					_whisperModelIndex = comboWhisperModel.SelectedIndex;
				}
				if(vad){
					comboVADModel.SelectedIndex = comboVADModel.Items.IndexOf(Settings.Default.VADModel);
					_vadModelIndex = comboVADModel.SelectedIndex;
				}
			} finally{ UseWaitCursor = false; }
		}
		private void ComboApiClientModel_DropDown(object sender, EventArgs e){
			try{
				var client = new ApiClient(textApiClientUrl.Text, textApiClientKey.Text, "");
				var clientModels = client.ListModels(CancellationToken.None);
				foreach(var model in clientModels) comboApiClientModel.Items.Add(model);
			} catch(Exception ex){ MessageBox.Show(this, ex.ToString(), Resources.LM_Stud, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);}
		}
	}
}