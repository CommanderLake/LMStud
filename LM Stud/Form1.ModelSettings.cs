using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
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
		private bool TryGetModelSettings(string modelPath, out ModelSettings settings){
			settings = null;
			var key = GetModelSettingsKey(modelPath);
			return key != null && _modelSettings.TryGetValue(key, out settings);
		}
		private bool TryGetEnabledModelSettings(string modelPath, out ModelSettings settings){
			return TryGetModelSettings(modelPath, out settings) && settings != null && settings.OverrideSettings;
		}
		private List<LoadedLocalSlotItem> GetLoadedLocalSlotItems(){
			var slots = new List<LoadedLocalSlotItem>();
			foreach(var loaded in new List<KeyValuePair<string, ListViewItem>>(Common.LoadedLocalSlots)){
				var loadedModel = loaded.Value;
				if(string.IsNullOrWhiteSpace(loaded.Key) || loadedModel == null || loadedModel.SubItems.Count < 2 || !NativeMethods.IsModelSlotLoaded(loaded.Key)) continue;
				ModelSettings overrides;
				var hasOverrides = TryGetEnabledModelSettings(loadedModel.SubItems[1].Text, out overrides);
				slots.Add(new LoadedLocalSlotItem(loaded.Key, loadedModel, overrides, hasOverrides));
			}
			return slots;
		}
		private static int GetModelContextMax(ListViewItem modelLvi){
			var meta = modelLvi?.Tag as List<GGUFMetadataManager.GGUFMetadataEntry>;
			return meta == null ? 0 : GGUFMetadataManager.GetGGUFCtxMax(meta);
		}
		private static int ClampContextSize(ListViewItem modelLvi, int requestedCtxSize){
			var modelCtxMax = GetModelContextMax(modelLvi);
			return modelCtxMax <= 0 ? requestedCtxSize : Math.Min(requestedCtxSize, modelCtxMax);
		}
		private static void UpdateCommonContextLimitForSlot(string slotName, ListViewItem modelLvi, int contextSize){
			if(!string.Equals(slotName, Common.ActiveModelSlotName, StringComparison.OrdinalIgnoreCase)) return;
			Common.ModelCtxMax = GetModelContextMax(modelLvi);
			Common.CntCtxMax = contextSize;
		}
		private void ButApplyModelSettings_Click(object sender, EventArgs e){
			if(listViewModels.SelectedItems.Count == 0) return;
			var selectedModel = listViewModels.SelectedItems[0];
			var selectedModelPath = selectedModel.SubItems[1].Text;
			var modelSettingsKey = GetModelSettingsKey(selectedModelPath);
			if(modelSettingsKey == null) return;
			_modelSettings.TryGetValue(modelSettingsKey, out var oldSettings);
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
			_modelSettings[modelSettingsKey] = new ModelSettings(overrideNew, systemPromptNew, ctxSizeNew, gpuLayersNew, tempNew, minPNew, topPNew, topKNew, flashNew, jinjaOverrideNew, jinjaTmplNew);
			SaveModelSettings();
			var loadedSlotNames = ModelSlotManager.GetLoadedLocalSlotNamesForPath(selectedModelPath);
			if(loadedSlotNames.Count == 0 || !Common.LlModelLoaded) return;
			var overrideOld = oldSettings?.OverrideSettings ?? false;
			var systemPromptOld = overrideOld ? oldSettings.SystemPrompt : Common.SystemPrompt;
			var ctxSizeOld = overrideOld ? oldSettings.CtxSize : Common.CtxSize;
			var gpuLayersOld = overrideOld ? oldSettings.GPULayers : Common.GPULayers;
			var tempOld = overrideOld ? oldSettings.Temp : Common.Temp;
			var minPOld = overrideOld ? oldSettings.MinP : Common.MinP;
			var topPOld = overrideOld ? oldSettings.TopP : Common.TopP;
			var topKOld = overrideOld ? oldSettings.TopK : Common.TopK;
			var flashOld = overrideOld ? oldSettings.FlashAttn : Common.FlashAttn;
			var jinjaOverrideOld = overrideOld && (oldSettings?.OverrideJinja ?? false);
			var jinjaTmplOld = jinjaOverrideOld ? oldSettings.JinjaTemplate ?? string.Empty : string.Empty;
			var systemPromptEff = overrideNew ? systemPromptNew : Common.SystemPrompt;
			var ctxSizeEff = overrideNew ? ctxSizeNew : Common.CtxSize;
			var gpuLayersEff = overrideNew ? gpuLayersNew : Common.GPULayers;
			var tempEff = overrideNew ? tempNew : Common.Temp;
			var minPEff = overrideNew ? minPNew : Common.MinP;
			var topPEff = overrideNew ? topPNew : Common.TopP;
			var topKEff = overrideNew ? topKNew : Common.TopK;
			var flashEff = overrideNew ? flashNew : Common.FlashAttn;
			var jinjaOverrideEff = overrideNew && jinjaOverrideNew;
			var jinjaTmplEff = jinjaOverrideEff ? jinjaTmplNew : string.Empty;
			var reloadModel = gpuLayersOld != gpuLayersEff || jinjaOverrideOld != jinjaOverrideEff || jinjaTmplOld != jinjaTmplEff;
			var reloadCtx = ctxSizeOld != ctxSizeEff || flashOld != flashEff;
			var reloadSmpl = tempOld != tempEff || minPOld != minPEff || topPOld != topPEff || topKOld != topKEff;
			var setSystemPrompt = systemPromptOld != systemPromptEff;
			if(reloadModel && MessageBox.Show(this, Resources.A_changed_setting_requires_the_model_to_be_reloaded__reload_now_, Resources.LM_Stud, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes){
				foreach(var slotName in loadedSlotNames) LoadModel(slotName, selectedModel, false);
			} else{
				if(reloadCtx){
					var contextSize = ClampContextSize(selectedModel, ctxSizeEff);
					foreach(var slotName in loadedSlotNames){
						UpdateCommonContextLimitForSlot(slotName, selectedModel, contextSize);
						CreateContext(slotName, contextSize, Common.BatchSize, flashEff, Common.NThreads, Common.NThreadsBatch, Common.KType, Common.VType);
					}
				}
				if(reloadSmpl)
					foreach(var slotName in loadedSlotNames) CreateSampler(slotName, minPEff, topPEff, topKEff, tempEff, Common.RepPen);
			}
			if(setSystemPrompt) ThreadPool.QueueUserWorkItem(o => {SetSystemPromptsForSlots(loadedSlotNames);});
		}
		private void PopulateModelSettings(string modelPath){
			if(TryGetModelSettings(modelPath, out var ms)){
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
				textSystemPromptModel.Text = Common.SystemPrompt;
				numCtxSizeModel.Value = Common.CtxSize;
				numGPULayersModel.Value = Common.GPULayers;
				numTempModel.Value = (decimal)Common.Temp;
				numMinPModel.Value = (decimal)Common.MinP;
				numTopPModel.Value = (decimal)Common.TopP;
				numTopKModel.Value = Common.TopK;
				checkFlashAttnModel.CheckState = Common.FlashAttn;
				checkOverrideJinjaModel.Checked = false;
				textJinjaTmplModel.Text = string.Empty;
			}
		}
		private void ButBrowseJinjaTmplModel_Click(object sender, EventArgs e){
			if(openFileDialog1.ShowDialog(this) == DialogResult.OK) textJinjaTmplModel.Text = openFileDialog1.FileName;
		}
		private class LoadedLocalSlotItem{
			public readonly bool HasOverrides;
			public readonly ListViewItem LoadedModel;
			public readonly ModelSettings Overrides;
			public readonly string SlotName;
			public LoadedLocalSlotItem(string slotName, ListViewItem loadedModel, ModelSettings overrides, bool hasOverrides){
				SlotName = slotName;
				LoadedModel = loadedModel;
				Overrides = overrides;
				HasOverrides = hasOverrides;
			}
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
