using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
namespace LMStud{
	internal partial class Form1{
		private class ModelSettings{
			public readonly bool OverrideSettings;
			public readonly string SystemPrompt;
			public readonly int CtxSize;
			public readonly int GPULayers;
			public readonly float Temp;
			public readonly float MinP;
			public readonly float TopP;
			public readonly int TopK;
			public readonly bool FlashAttn;
			public ModelSettings(bool overrideSettings, string systemPrompt, int ctxSize, int gpuLayers, float temp, float minP, float topP, int topK, bool flashAttn){
				OverrideSettings = overrideSettings;
				SystemPrompt = systemPrompt;
				CtxSize = ctxSize;
				GPULayers = gpuLayers;
				Temp = temp;
				MinP = minP;
				TopP = topP;
				TopK = topK;
				FlashAttn = flashAttn;
			}
		}
		private readonly Dictionary<string, ModelSettings> _modelSettings = new Dictionary<string, ModelSettings>();
		private static readonly string ModelSettingsFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LM Stud");
		private static readonly string ModelSettingsFile = Path.Combine(ModelSettingsFolder, "ModelSettings.json");
		private void LoadModelSettings(){
			if(!File.Exists(ModelSettingsFile)) return;
			try{
				var json = File.ReadAllText(ModelSettingsFile);
				var dict = JsonConvert.DeserializeObject<Dictionary<string, ModelSettings>>(json);
				if(dict == null) return;
				foreach(var kv in dict) _modelSettings[kv.Key] = kv.Value;
			} catch{}
		}
		private void SaveModelSettings(){
			try{
				var json = JsonConvert.SerializeObject(_modelSettings, Formatting.Indented);
				if(!Directory.Exists(ModelSettingsFolder)) Directory.CreateDirectory(ModelSettingsFolder);
				File.WriteAllText(ModelSettingsFile, json);
			} catch{}
		}
		private void ButApplyModelSettings_Click(object sender, EventArgs e){
			if(listViewModels.SelectedItems.Count == 0) return;
			var path = _models[(int)listViewModels.SelectedItems[0].Tag].FilePath;
			var settings = new ModelSettings(checkOverrideSettings.Checked, textSystemPromptModel.Text, (int)numCtxSizeModel.Value, (int)numGPULayersModel.Value, (float)numTempModel.Value, (float)numMinPModel.Value,
				(float)numTopPModel.Value, (int)numTopKModel.Value, checkFlashAttnModel.Checked);
			_modelSettings[path] = settings;
			SaveModelSettings();
		}
		private void PopulateModelSettings(int modelIndex){
			var path = _models[modelIndex].FilePath;
			if(_modelSettings.TryGetValue(path, out var ms)){
				checkOverrideSettings.Checked = ms.OverrideSettings;
				textSystemPromptModel.Text = ms.SystemPrompt;
				numCtxSizeModel.Value = ms.CtxSize;
				numGPULayersModel.Value = ms.GPULayers;
				numTempModel.Value = (decimal)ms.Temp;
				numMinPModel.Value = (decimal)ms.MinP;
				numTopPModel.Value = (decimal)ms.TopP;
				numTopKModel.Value = ms.TopK;
				checkFlashAttnModel.Checked = ms.FlashAttn;
			} else{
				checkOverrideSettings.Checked = false;
				textSystemPromptModel.Text = _systemPrompt;
				numCtxSizeModel.Value = _ctxSize;
				numGPULayersModel.Value = _gpuLayers;
				numTempModel.Value = (decimal)_temp;
				numMinPModel.Value = (decimal)_minP;
				numTopPModel.Value = (decimal)_topP;
				numTopKModel.Value = _topK;
				checkFlashAttnModel.Checked = _flashAttn;
			}
		}
	}
}