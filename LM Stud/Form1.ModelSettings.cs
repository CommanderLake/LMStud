using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using LMStud.Properties;
using Newtonsoft.Json;
namespace LMStud{
	public partial class Form1{
		private static readonly string ModelSettingsFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LM Stud");
		private static readonly string ModelSettingsFile = Path.Combine(ModelSettingsFolder, "ModelSettings.json");
		private readonly Dictionary<string, ModelSettings> _modelSettings = new Dictionary<string, ModelSettings>();
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
			var modelIndex = (int)listViewModels.SelectedItems[0].Tag;
			var path = _models[modelIndex].FilePath;
			_modelSettings.TryGetValue(path, out var oldSettings);
			var overrideNew = checkOverrideSettings.Checked;
			var systemPromptNew = textSystemPromptModel.Text;
			var ctxSizeNew = (int)numCtxSizeModel.Value;
			var gpuLayersNew = (int)numGPULayersModel.Value;
			var tempNew = (float)numTempModel.Value;
			var minPNew = (float)numMinPModel.Value;
			var topPNew = (float)numTopPModel.Value;
			var topKNew = (int)numTopKModel.Value;
			var flashNew = checkFlashAttnModel.CheckState;
			var jinjaOverrideNew = checkOverrideJinjaModel.Checked;
			var jinjaTmplNew = textJinjaTmplModel.Text;
			_modelSettings[path] = new ModelSettings(overrideNew, systemPromptNew, ctxSizeNew, gpuLayersNew, tempNew, minPNew, topPNew, topKNew, flashNew, jinjaOverrideNew, jinjaTmplNew);
			SaveModelSettings();
			if(_modelIndex != modelIndex || !LlModelLoaded) return;
			var overrideOld = oldSettings?.OverrideSettings ?? false;
			var systemPromptOld = overrideOld ? oldSettings.SystemPrompt : _systemPrompt;
			var ctxSizeOld = overrideOld ? oldSettings.CtxSize : _ctxSize;
			var gpuLayersOld = overrideOld ? oldSettings.GPULayers : _gpuLayers;
			var tempOld = overrideOld ? oldSettings.Temp : _temp;
			var minPOld = overrideOld ? oldSettings.MinP : _minP;
			var topPOld = overrideOld ? oldSettings.TopP : _topP;
			var topKOld = overrideOld ? oldSettings.TopK : _topK;
			var flashOld = overrideOld ? oldSettings.FlashAttn : _flashAttn;
			var jinjaOverrideOld = oldSettings?.OverrideJinja ?? false;
			var jinjaTmplOld = oldSettings?.JinjaTemplate ?? string.Empty;
			var systemPromptEff = overrideNew ? systemPromptNew : _systemPrompt;
			var ctxSizeEff = overrideNew ? ctxSizeNew : _ctxSize;
			var gpuLayersEff = overrideNew ? gpuLayersNew : _gpuLayers;
			var tempEff = overrideNew ? tempNew : _temp;
			var minPEff = overrideNew ? minPNew : _minP;
			var topPEff = overrideNew ? topPNew : _topP;
			var topKEff = overrideNew ? topKNew : _topK;
			var flashEff = overrideNew ? flashNew : _flashAttn;
			var jinjaOverrideEff = jinjaOverrideNew;
			var jinjaTmplEff = jinjaOverrideNew ? jinjaTmplNew : string.Empty;
			var reloadModel = gpuLayersOld != gpuLayersEff || jinjaOverrideOld != jinjaOverrideEff || jinjaTmplOld != jinjaTmplEff;
			var reloadCtx = ctxSizeOld != ctxSizeEff || flashOld != flashEff;
			var reloadSmpl = tempOld != tempEff || minPOld != minPEff || topPOld != topPEff || topKOld != topKEff;
			var setSystemPrompt = systemPromptOld != systemPromptEff;
			if(reloadModel && MessageBox.Show(this, Resources.A_changed_setting_requires_the_model_to_be_reloaded__reload_now_, Resources.LM_Stud, MessageBoxButtons.YesNo, MessageBoxIcon.Question) ==
				DialogResult.Yes){ LoadModel(_modelIndex, false); } else{
				if(reloadCtx){
					if(_modelCtxMax <= 0) _cntCtxMax = ctxSizeEff;
					else _cntCtxMax = ctxSizeEff > _modelCtxMax ? _modelCtxMax : ctxSizeEff;
					CreateContext(_cntCtxMax, _batchSize, flashEff, _nThreads, _nThreadsBatch);
				}
				if(reloadSmpl) CreateSampler(minPEff, topPEff, topKEff, tempEff, _repPen);
			}
			if(setSystemPrompt) QueueSetSystemPrompt(systemPromptEff);
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
				checkFlashAttnModel.CheckState = ms.FlashAttn;
				checkOverrideJinjaModel.Checked = ms.OverrideJinja;
				textJinjaTmplModel.Text = ms.JinjaTemplate;
			} else{
				checkOverrideSettings.Checked = false;
				textSystemPromptModel.Text = _systemPrompt;
				numCtxSizeModel.Value = _ctxSize;
				numGPULayersModel.Value = _gpuLayers;
				numTempModel.Value = (decimal)_temp;
				numMinPModel.Value = (decimal)_minP;
				numTopPModel.Value = (decimal)_topP;
				numTopKModel.Value = _topK;
				checkFlashAttnModel.CheckState = _flashAttn;
				checkOverrideJinjaModel.Checked = false;
				textJinjaTmplModel.Text = string.Empty;
			}
		}
		private void ButBrowseJinjaTmplModel_Click(object sender, EventArgs e){
			if(openFileDialog1.ShowDialog(this) == DialogResult.OK) textJinjaTmplModel.Text = openFileDialog1.FileName;
		}
		private class ModelSettings{
			public readonly int CtxSize;
			public readonly CheckState FlashAttn;
			public readonly int GPULayers;
			public readonly string JinjaTemplate;
			public readonly float MinP;
			public readonly bool OverrideJinja;
			public readonly bool OverrideSettings;
			public readonly string SystemPrompt;
			public readonly float Temp;
			public readonly int TopK;
			public readonly float TopP;
			public ModelSettings(bool overrideSettings, string systemPrompt, int ctxSize, int gpuLayers, float temp, float minP, float topP, int topK, CheckState flashAttn, bool overrideJinja,
				string jinjaTemplate){
				OverrideSettings = overrideSettings;
				SystemPrompt = systemPrompt;
				CtxSize = ctxSize;
				GPULayers = gpuLayers;
				Temp = temp;
				MinP = minP;
				TopP = topP;
				TopK = topK;
				FlashAttn = flashAttn;
				OverrideJinja = overrideJinja;
				JinjaTemplate = jinjaTemplate;
			}
		}
		private void CheckUseModelSettings_CheckedChanged(object sender, EventArgs e){
			groupCommonModel.Enabled = groupAdvancedModel.Enabled = labelSystemPromptModel.Enabled = textSystemPromptModel.Enabled = checkOverrideSettings.Checked;
		}
	}
}