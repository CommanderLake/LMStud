using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
namespace LMStud{
	internal partial class Form1{
		private struct ModelSettings{
			public readonly bool UseModelSettings;
			public readonly string SystemPrompt;
			public readonly int CtxSize;
			public readonly int GPULayers;
			public readonly float Temp;
			public readonly float MinP;
			public readonly float TopP;
			public readonly int TopK;
			public readonly bool FlashAttn;
			public ModelSettings(bool useModelSettings, string systemPrompt, int ctxSize, int gpuLayers, float temp, float minP, float topP, int topK, bool flashAttn){
				UseModelSettings = useModelSettings;
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
		private readonly string _modelSettingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LM Stud\\ModelSettings.json");
		private void LoadModelSettings(){
			if(!File.Exists(_modelSettingsPath)) return;
			try{
				var json = File.ReadAllText(_modelSettingsPath);
				var dict = JsonConvert.DeserializeObject<Dictionary<string, ModelSettings>>(json);
				if(dict == null) return;
				foreach(var kv in dict) _modelSettings[kv.Key] = kv.Value;
			} catch{}
		}
		private void SaveModelSettings(){
			try{
				var json = JsonConvert.SerializeObject(_modelSettings, Formatting.Indented);
				File.WriteAllText(_modelSettingsPath, json);
			} catch{}
		}
		private void ButApplyModelSettings_Click(object sender, EventArgs e){
			if(listViewModels.SelectedItems.Count == 0) return;
			var path = _models[(int)listViewModels.SelectedItems[0].Tag].FilePath;
			var settings = new ModelSettings(checkUseModelSettings.Checked, textSystemPromptModel.Text, (int)numCtxSizeModel.Value, (int)numGPULayersModel.Value, (float)numTempModel.Value, (float)numMinPModel.Value,
				(float)numTopPModel.Value, (int)numTopKModel.Value, checkFlashAttnModel.Checked);
			_modelSettings[path] = settings;
			SaveModelSettings();
		}
	}
}