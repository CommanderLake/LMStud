using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
namespace LMStud{
	internal partial class Form1{
		private struct ModelSettings{
			public bool UseModelSettings;
			public int CtxSize;
			public int GPULayers;
			public float Temp;
			public float MinP;
			public float TopP;
			public int TopK;
			public bool FlashAttn;
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
			if(listViewModels.SelectedIndices.Count == 0) return;
			var path = _models[listViewModels.SelectedIndices[0]].FilePath;
			var settings = new ModelSettings{
				UseModelSettings = checkUseModelSettings.Checked, CtxSize = (int)numCtxSizeModel.Value, GPULayers = (int)numGPULayersModel.Value, Temp = (float)numTempModel.Value,
				MinP = (float)numMinPModel.Value, TopP = (float)numTopPModel.Value, TopK = (int)numTopKModel.Value, FlashAttn = checkFlashAttnModel.Checked
			};
			_modelSettings[path] = settings;
			SaveModelSettings();
		}
	}
}