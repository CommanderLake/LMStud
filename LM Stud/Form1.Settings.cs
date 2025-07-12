using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using LMStud.Properties;
namespace LMStud{
	internal partial class Form1{
		private string _systemPrompt = "";
		private string _modelsPath = "";
		private int _ctxSize = 4096;
		private int _gpuLayers = 32;
		private float _temp = 0.7f;
		private int _nGen = -1;
		private NativeMethods.GgmlNumaStrategy _numaStrat = 0;
		private float _repPen = 1.1f;
		private int _topK = 40;
		private float _topP = 0.95f;
		private int _batchSize = 512;
		private bool _mMap = true;
		private bool _mLock;
		private int _nThreads = 8;
		private int _nThreadsBatch = 8;
		private int _whisperModelIndex;
		private string _wakeWord;
		private float _vadThreshold = 0.6f;
		private float _freqThreshold = 100.0f;
		private bool _whisperUseGPU;
		private bool _speak;
		private bool _flashAttn = true;
		private string _googleAPIKey;
		private string _googleSearchID;
		private int _googleSearchResultCount = 5;
		private bool _googleSearchEnable;
		private bool _webpageFetchEnable;
		private string _fileBaseDir;
		private bool _fileListEnable;
		private bool _fileCreateEnable;
		private bool _fileReadEnable;
		private bool _fileWriteEnable;
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
			_batchSize = (int)(numBatchSize.Value = Settings.Default.BatchSize);
			_mMap = checkMMap.Checked = Settings.Default.MMap;
			_mLock = checkMLock.Checked = Settings.Default.MLock;
			_nThreads = (int)(numThreads.Value = Settings.Default.Threads);
			_nThreadsBatch = (int)(numThreadsBatch.Value = Settings.Default.ThreadsBatch);
			_wakeWord = textWakeWord.Text = Settings.Default.WakeWord;
			_vadThreshold = (float)(numVadThreshold.Value = Settings.Default.VadThreshold);
			_freqThreshold = (float)(numFreqThreshold.Value = Settings.Default.FreqThreshold);
			_whisperUseGPU = checkWhisperUseGPU.Checked = Settings.Default.whisperUseGPU;
			_speak = checkSpeak.Checked = Settings.Default.Speak;
			_flashAttn = checkFlashAttn.Checked = Settings.Default.FlashAttn;
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
			NativeMethods.SetFileBaseDir(_fileBaseDir);
			NativeMethods.SetWakeCommand(_wakeWord);
			NativeMethods.SetVADThresholds(_vadThreshold, _freqThreshold);
			NativeMethods.SetGoogle(_googleAPIKey, _googleSearchID, _googleSearchResultCount);
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
			var setGoogle = false;
			var registerTools = false;
			var setSystemPrompt = false;
			UpdateSetting(ref _systemPrompt, textSystemPrompt.Text, value => {
				Settings.Default.SystemPrompt = value;
				setSystemPrompt = true;
			});
			UpdateSetting(ref _modelsPath, textModelsPath.Text, value => {
				Settings.Default.ModelsDir = value;
				PopulateModels();
				PopulateWhisperModels();
			});
			UpdateSetting(ref _ctxSize, (int)numCtxSize.Value, value => {
				Settings.Default.CtxSize = value;
				if(!_llModelLoaded) return;
				if(_modelCtxMax <= 0) _cntCtxMax = _ctxSize;
				else _cntCtxMax = _ctxSize > _modelCtxMax ? _modelCtxMax : _ctxSize;
				reloadCtx = true;
			});
			UpdateSetting(ref _gpuLayers, (int)numGPULayers.Value, value => {
				Settings.Default.GPULayers = value;
				reloadModel = true;
			});
			UpdateSetting(ref _temp, (float)numTemp.Value, value => {
				Settings.Default.Temp = numTemp.Value;
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
				reloadSmpl = true;
			});
			UpdateSetting(ref _topP, (float)numTopP.Value, value => {
				Settings.Default.TopP = numTopP.Value;
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
			UpdateSetting(ref _whisperUseGPU, checkWhisperUseGPU.Checked, value => {
				Settings.Default.whisperUseGPU = value;
				reloadWhisper = true;
			});
			UpdateSetting(ref _vadThreshold, (float)numVadThreshold.Value, value => {
				Settings.Default.VadThreshold = numVadThreshold.Value;
				setVAD = true;
			});
			UpdateSetting(ref _freqThreshold, (float)numFreqThreshold.Value, value => {
				Settings.Default.FreqThreshold = numFreqThreshold.Value;
				setVAD = true;
			});
			UpdateSetting(ref _speak, checkSpeak.Checked, value => {Settings.Default.Speak = value;});
			UpdateSetting(ref _flashAttn, checkFlashAttn.Checked, value => {
				Settings.Default.FlashAttn = value;
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
			Settings.Default.Save();
			if(reloadModel && _llModelLoaded && MessageBox.Show(this, "A changed setting requires the model to be reloaded, reload now?", "LM Stud", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
				LoadModel(_modelIndex, false);
			else{
				if(reloadCtx){
					NativeMethods.CreateContext(_cntCtxMax, _batchSize, _flashAttn, _nThreads, _nThreadsBatch);
					NativeMethods.RetokenizeChat(true);
				}
				if(reloadSmpl) NativeMethods.CreateSampler(_topP, _topK, _temp, _repPen);
			}
			if(setVAD) NativeMethods.SetVADThresholds(_vadThreshold, _freqThreshold);
			if(reloadWhisper && _whisperLoaded){
				if(_whisperModelIndex < 0 || !File.Exists(_whisperModels[_whisperModelIndex])){
					checkVoiceInput.Checked = false;
					MessageBox.Show(this, "Whisper model not found", "LM Stud Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
				} else{
					if(checkVoiceInput.CheckState != CheckState.Unchecked) NativeMethods.StopSpeechTranscription();
					NativeMethods.LoadWhisperModel(_whisperModels[_whisperModelIndex], _nThreads, _whisperUseGPU);
					if(checkVoiceInput.CheckState != CheckState.Unchecked) NativeMethods.StartSpeechTranscription();
				}
			}
			if(setGoogle) NativeMethods.SetGoogle(_googleAPIKey, _googleSearchID, _googleSearchResultCount);
			if(registerTools) RegisterTools();
			if(registerTools || setSystemPrompt) SetSystemPrompt();
		}
		private void ButBrowse_Click(object sender, EventArgs e){
			if(folderBrowserDialog1.ShowDialog(this) == DialogResult.OK) textModelsPath.Text = folderBrowserDialog1.SelectedPath;
		}
		private void TextModelsPath_KeyDown(object sender, KeyEventArgs e){
			if(e.KeyCode != Keys.Enter) return;
			var path = textModelsPath.Text.Trim();
			if(Directory.Exists(path)) textModelsPath.Text = path;
			else MessageBox.Show(this, "Folder not found", "LM Stud Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
		}
		private void ComboWhisperModel_DropDown(object sender, EventArgs e){
			if(!Directory.Exists(_modelsPath)) return;
			PopulateWhisperModels();
		}
		private void ButWhispDown_Click(object sender, EventArgs e){
			HugLoadFiles("ggerganov", "whisper.cpp", ".bin");
			tabControl1.SelectTab(3);
		}
		private void LinkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e){
			textSystemPrompt.Text = "The list directory and file tools operate relative to a base directory.\r\nUse list directory with an empty path before using the file tools to help with coding tasks throughout my project.\r\nAlways read files after modifying them to verify changes.";
		}
		private void PopulateWhisperModels(){
			if (!ModelsFolderExists(false)) return;
			UseWaitCursor = true;
			try{
				_whisperModels.Clear();
				comboWhisperModel.Items.Clear();
				var files = Directory.GetFiles(_modelsPath, "*.bin", SearchOption.AllDirectories);
				foreach(var file in files){
					using(var fs = new FileStream(file, FileMode.Open, FileAccess.Read))
					using(var br = new BinaryReader(fs)){
						var magic = br.ReadUInt32();
						if(magic != 0x67676D6C)// "lmgg" in little-endian
							continue;
					}
					_whisperModels.Add(file);
					comboWhisperModel.Items.Add(Path.GetFileName(file));
				}
				comboWhisperModel.SelectedIndex = comboWhisperModel.Items.IndexOf(Settings.Default.WhisperModel);
				_whisperModelIndex = comboWhisperModel.SelectedIndex;
			} finally{ UseWaitCursor = false; }
		}
	}
}