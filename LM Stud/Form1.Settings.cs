﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using LMStud.Properties;
namespace LMStud{
	internal partial class Form1{
		private string _instruction = "";
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
		private bool _strictCPU;
		private bool _strictCPUBatch;
		private int _whisperModelIndex;
		private string _wakeWord;
		private float _vadThreshold = 0.6f;
		private float _freqThreshold = 100.0f;
		private bool _whisperUseGPU;
		private bool _speak;
		private void LoadConfig(){
			_instruction = textInstruction.Text = Settings.Default.Instruction;
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
			_strictCPU = checkStrictCPU.Checked = Settings.Default.StrictCPU;
			_nThreadsBatch = (int)(numThreadsBatch.Value = Settings.Default.ThreadsBatch);
			_strictCPUBatch = checkStrictCPUBatch.Checked = Settings.Default.StrictCPUBatch;
			_wakeWord = textWakeWord.Text = Settings.Default.WakeWord;
			_vadThreshold = (float)(numVadThreshold.Value = Settings.Default.VadThreshold);
			_freqThreshold = (float)(numFreqThreshold.Value = Settings.Default.FreqThreshold);
			_whisperUseGPU = checkWhisperUseGPU.Checked = Settings.Default.whisperUseGPU;
			_speak = checkSpeak.Checked = Settings.Default.Speak;
			NativeMethods.SetWakeCommand(_wakeWord);
			NativeMethods.SetVADThresholds(_vadThreshold, _freqThreshold);
		}
		//private void ButApply_Click(object sender, EventArgs e){
		//	if(_instruction != textInstruction.Text){
		//		Settings.Default.Instruction = _instruction = textInstruction.Text;
		//		NativeMethods.SetSystemPrompt(textInstruction.Text);
		//	}
		//	if(_modelsPath != textModelsPath.Text){
		//		Settings.Default.ModelsDir = _modelsPath = textModelsPath.Text;
		//		PopulateModels();
		//	}
		//	if(_ctxSize != (int)numCtxSize.Value) Settings.Default.CtxSize = _ctxSize = (int)numCtxSize.Value;
		//	if(_gpuLayers != (int)numGPULayers.Value) Settings.Default.GPULayers = _gpuLayers = (int)numGPULayers.Value;
		//	if(_temp != (int)numTemp.Value){
		//		_temp = (float)numTemp.Value;
		//		Settings.Default.Temp = numTemp.Value;
		//	}
		//	if(_nGen != (int)numNGen.Value) Settings.Default.NGen = _nGen = (int)numNGen.Value;
		//	if(_numaStrat != (NativeMethods.GgmlNumaStrategy)comboNUMAStrat.SelectedIndex){
		//		_numaStrat = (NativeMethods.GgmlNumaStrategy)comboNUMAStrat.SelectedIndex;
		//		Settings.Default.NUMAStrat = comboNUMAStrat.SelectedIndex;
		//	}
		//	if(_repPen != (float)numRepPen.Value){
		//		_repPen = (float)numRepPen.Value;
		//		Settings.Default.RepPen = numRepPen.Value;
		//	}
		//	if(_topK != (int)numTopK.Value) Settings.Default.TopK = _topK = (int)numTopK.Value;
		//	if(_topP != (double)numTopP.Value){
		//		_topP = (float)numTopP.Value;
		//		Settings.Default.TopP = numTopP.Value;
		//	}
		//	if(_batchSize != (int)numBatchSize.Value) Settings.Default.BatchSize = _batchSize = (int)numBatchSize.Value;
		//	if(_mMap != checkMMap.Checked) Settings.Default.MMap = _mMap = checkMMap.Checked;
		//	if(_mLock != checkMLock.Checked) Settings.Default.MLock = _mLock = checkMLock.Checked;
		//	if(_nThreads != (int)numThreads.Value || _nThreadsBatch != (int)numThreadsBatch.Value){
		//		Settings.Default.Threads = _nThreads = (int)numThreads.Value;
		//		Settings.Default.ThreadsBatch = _nThreadsBatch = (int)numThreadsBatch.Value;
		//		NativeMethods.SetThreadCount((int)numThreads.Value, (int)numThreadsBatch.Value);
		//	}
		//	if(_strictCPU != checkStrictCPU.Checked) Settings.Default.StrictCPU = _strictCPU = checkStrictCPU.Checked;
		//	if(_strictCPUBatch != checkStrictCPUBatch.Checked) Settings.Default.StrictCPUBatch = _strictCPUBatch = checkStrictCPUBatch.Checked;
		//	if(_whisperModelIndex != comboWhisperModel.SelectedIndex){
		//		_whisperModelIndex = comboWhisperModel.SelectedIndex;
		//		Settings.Default.WhisperModel = comboWhisperModel.Text;
		//	}
		//	if(_wakeWord != textWakeWord.Text){
		//		Settings.Default.WakeWord = _wakeWord = textWakeWord.Text;
		//		NativeMethods.SetWakeCommand(_wakeWord);
		//	}
		//	if(_whisperUseGPU != checkWhisperUseGPU.Checked) Settings.Default.whisperUseGPU = _whisperUseGPU = checkWhisperUseGPU.Checked;
		//	if(_vadThreshold != (double)numVadThreshold.Value || _freqThreshold != (double)numFreqThreshold.Value){
		//		_vadThreshold = (float)numVadThreshold.Value;
		//		Settings.Default.VadThreshold = numVadThreshold.Value;
		//		_freqThreshold = (float)numFreqThreshold.Value;
		//		Settings.Default.FreqThreshold = numFreqThreshold.Value;
		//		NativeMethods.SetVADThresholds(_vadThreshold, _freqThreshold);
		//	}
		//	Settings.Default.Save();
		//}
		// Helper method to reduce repetition when updating a setting.
		private void UpdateSetting<T>(ref T currentValue, T newValue, Action<T> updateAction){
			if(!EqualityComparer<T>.Default.Equals(currentValue, newValue)){
				currentValue = newValue;
				updateAction(newValue);
			}
		}
		private void ButApply_Click(object sender, EventArgs e){
			UpdateSetting(ref _instruction, textInstruction.Text, value => {
				Settings.Default.Instruction = value;
				NativeMethods.SetSystemPrompt(value);
			});
			UpdateSetting(ref _modelsPath, textModelsPath.Text, value => {
				Settings.Default.ModelsDir = value;
				PopulateModels();
			});
			UpdateSetting(ref _ctxSize, (int)numCtxSize.Value, value => {Settings.Default.CtxSize = value;});
			UpdateSetting(ref _gpuLayers, (int)numGPULayers.Value, value => {Settings.Default.GPULayers = value;});
			UpdateSetting(ref _temp, (float)numTemp.Value, value => {Settings.Default.Temp = numTemp.Value;});
			UpdateSetting(ref _nGen, (int)numNGen.Value, value => {Settings.Default.NGen = value;});
			var newNumaIndex = comboNUMAStrat.SelectedIndex;
			UpdateSetting(ref _numaStrat, (NativeMethods.GgmlNumaStrategy)newNumaIndex, value => {Settings.Default.NUMAStrat = newNumaIndex;});
			UpdateSetting(ref _repPen, (float)numRepPen.Value, value => {Settings.Default.RepPen = numRepPen.Value;});
			UpdateSetting(ref _topK, (int)numTopK.Value, value => {Settings.Default.TopK = value;});
			UpdateSetting(ref _topP, (float)numTopP.Value, value => {Settings.Default.TopP = numTopP.Value;});
			UpdateSetting(ref _batchSize, (int)numBatchSize.Value, value => {Settings.Default.BatchSize = value;});
			UpdateSetting(ref _mMap, checkMMap.Checked, value => {Settings.Default.MMap = value;});
			UpdateSetting(ref _mLock, checkMLock.Checked, value => {Settings.Default.MLock = value;});
			if(_nThreads != (int)numThreads.Value || _nThreadsBatch != (int)numThreadsBatch.Value){
				UpdateSetting(ref _nThreads, (int)numThreads.Value, value => {Settings.Default.Threads = value;});
				UpdateSetting(ref _nThreadsBatch, (int)numThreadsBatch.Value, value => {Settings.Default.ThreadsBatch = value;});
				NativeMethods.SetThreadCount((int)numThreads.Value, (int)numThreadsBatch.Value);
			}
			UpdateSetting(ref _strictCPU, checkStrictCPU.Checked, value => {Settings.Default.StrictCPU = value;});
			UpdateSetting(ref _strictCPUBatch, checkStrictCPUBatch.Checked, value => {Settings.Default.StrictCPUBatch = value;});
			UpdateSetting(ref _whisperModelIndex, comboWhisperModel.SelectedIndex, value => {Settings.Default.WhisperModel = comboWhisperModel.Text;});
			UpdateSetting(ref _wakeWord, textWakeWord.Text, value => {
				Settings.Default.WakeWord = value;
				NativeMethods.SetWakeCommand(value);
			});
			UpdateSetting(ref _whisperUseGPU, checkWhisperUseGPU.Checked, value => {Settings.Default.whisperUseGPU = value;});
			UpdateSetting(ref _vadThreshold, (float)numVadThreshold.Value, value => {
				Settings.Default.VadThreshold = numVadThreshold.Value;
				NativeMethods.SetVADThresholds(_vadThreshold, _freqThreshold);
			});
			UpdateSetting(ref _freqThreshold, (float)numFreqThreshold.Value, value => {
				Settings.Default.FreqThreshold = numFreqThreshold.Value;
				NativeMethods.SetVADThresholds(_vadThreshold, _freqThreshold);
			});
			UpdateSetting(ref _speak, checkSpeak.Checked, value => {Settings.Default.Speak = value;});
			Settings.Default.Save();
		}
		private void ButBrowse_Click(object sender, EventArgs e){
			if(folderBrowserDialog1.ShowDialog(this) == DialogResult.OK) textModelsPath.Text = folderBrowserDialog1.SelectedPath;
		}
		private void TextModelsPath_KeyDown(object sender, KeyEventArgs e){
			if(e.KeyCode != Keys.Enter) return;
			MessageBox.Show(this, "Folder not found", "LM Stud Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
		}
		private void ComboWhisperModel_DropDown(object sender, EventArgs e){
			if(!Directory.Exists(_modelsPath)) return;
			PopulateWhisperModels();
		}
		private void ButWhispDown_Click(object sender, EventArgs e){
			HugLoadFiles("ggerganov", "whisper.cpp", ".bin");
			tabControl1.SelectTab(3);
		}
		private void PopulateWhisperModels(){
			if (!ModelsFolderExists()) return;
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