using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using LMStud.Properties;
namespace LMStud{
	public partial class Form1{
		private readonly Dictionary<string, ModelSettings> _modelSettings = new Dictionary<string, ModelSettings>();
		private readonly string _modelSettingsFile;
		private void LoadModelSettings(){
			if(!File.Exists(_modelSettingsFile)) return;
			try{
				var json = File.ReadAllText(_modelSettingsFile);
				var dict = ParseModelSettings(json);
				if(dict == null) return;
				foreach(var kv in dict) _modelSettings[kv.Key] = kv.Value;
			} catch{}
		}
		private void SaveModelSettings(){
			try{
				var json = SerializeModelSettings(_modelSettings);
				Directory.CreateDirectory(Path.GetDirectoryName(_modelSettingsFile));
				File.WriteAllText(_modelSettingsFile, json);
			} catch{}
		}
		private bool TryGetModelSettings(string modelPath, out ModelSettings settings){
			settings = null;
			var key = Common.GetPathRelativeToModelsDir(modelPath);
			return key != null && _modelSettings.TryGetValue(key, out settings);
		}
		private bool TryGetEnabledModelSettings(string modelPath, out ModelSettings settings){return TryGetModelSettings(modelPath, out settings) && settings != null && settings.OverrideSettings;}
		private static Dictionary<string, ModelSettings> ParseModelSettings(string json){
			var root = Json.Parse(json);
			if(!root.IsObject) return null;
			var dict = new Dictionary<string, ModelSettings>();
			foreach(var property in root.Properties()){
				var settings = ParseModelSettingsValue(property.Value);
				if(settings != null) dict[property.Name] = settings;
			}
			return dict;
		}
		private static ModelSettings ParseModelSettingsValue(JsonNode node){
			if(!node.IsObject) return null;
			return new ModelSettings(node.GetBool("OverrideSettings") ?? false,
				node.GetString("SystemPrompt"),
				node.GetInt("CtxSize") ?? 0,
				node.GetInt("GPULayers") ?? 0,
				(float)(node.GetDouble("Temp") ?? 0),
				(float)(node.GetDouble("MinP") ?? 0),
				(float)(node.GetDouble("TopP") ?? 0),
				node.GetInt("TopK") ?? 0,
				(CheckState)(node.GetInt("FlashAttn") ?? 0),
				node.GetBool("OffloadKQV") ?? true,
				node.GetBool("OffloadOP") ?? true,
				node.GetBool("OverrideJinja") ?? false,
				node.GetString("JinjaTemplate"));
		}
		private static string SerializeModelSettings(Dictionary<string, ModelSettings> settings){
			var root = Json.ObjectBuilder();
			foreach(var kv in settings) root.Set(kv.Key, SerializeModelSettingsValue(kv.Value));
			return root.ToJson(JsonFormat.Indented);
		}
		private static JsonNode SerializeModelSettingsValue(ModelSettings settings){
			return Json.Object(Json.P("OverrideSettings", settings.OverrideSettings),
				Json.P("SystemPrompt", settings.SystemPrompt),
				Json.P("CtxSize", settings.CtxSize),
				Json.P("GPULayers", settings.GPULayers),
				Json.P("Temp", settings.Temp),
				Json.P("MinP", settings.MinP),
				Json.P("TopP", settings.TopP),
				Json.P("TopK", settings.TopK),
				Json.P("FlashAttn", (int)settings.FlashAttn),
				Json.P("OffloadKQV", settings.OffloadKQV),
				Json.P("OffloadOP", settings.OffloadOP),
				Json.P("OverrideJinja", settings.OverrideJinja),
				Json.P("JinjaTemplate", settings.JinjaTemplate));
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
		private static int GetModelLayerCount(ListViewItem modelLvi){
			var meta = modelLvi?.Tag as List<GGUFMetadataManager.GGUFMetadataEntry>;
			return meta == null ? 0 : GGUFMetadataManager.GetGGUFLayerCount(meta);
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
		private void QueueReloads(List<ReloadRequest> reloads, Action completed = null){
			if(reloads == null || reloads.Count == 0){
				completed?.Invoke();
				return;
			}
			BeginRetokenization();
			ThreadPool.QueueUserWorkItem(_ => {
				try{
					foreach(var reload in reloads){
						if(reload.ReloadContext)
							CreateContext(reload.SlotName,
								reload.ContextSize,
								Common.BatchSize,
								reload.FlashAttn,
								reload.OffloadKQV,
								reload.OffloadOP,
								Common.NThreads,
								Common.NThreadsBatch,
								Common.KType,
								Common.VType,
								Common.MtpDraftTokens);
						if(reload.ReloadSampler) CreateSampler(reload.SlotName, reload.MinP, reload.TopP, reload.TopK, reload.Temp, Common.RepPen);
					}
				} finally{
					try{
						BeginInvoke(new MethodInvoker(() => {
							try{ completed?.Invoke(); } finally{ EndRetokenization(); }
						}));
					} catch(ObjectDisposedException){ EndRetokenization(); }
				}
			});
		}
		private void ButApplyModelSettings_Click(object sender, EventArgs e){
			if(listViewModels.SelectedItems.Count == 0) return;
			var selectedModel = listViewModels.SelectedItems[0];
			var selectedModelPath = selectedModel.SubItems[1].Text;
			var modelSettingsKey = Common.GetPathRelativeToModelsDir(selectedModelPath);
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
			var offloadKQVNew = checkOffloadKQVModel.Checked;
			var offloadOPNew = checkOffloadOPModel.Checked;
			var jinjaOverrideNew = checkOverrideJinjaModel.Checked;
			var jinjaTmplNew = textJinjaTmplModel.Text;
			_modelSettings[modelSettingsKey] = new ModelSettings(overrideNew,
				systemPromptNew,
				ctxSizeNew,
				gpuLayersNew,
				tempNew,
				minPNew,
				topPNew,
				topKNew,
				flashNew,
				offloadKQVNew,
				offloadOPNew,
				jinjaOverrideNew,
				jinjaTmplNew);
			SaveModelSettings();
			var loadedSlotNames = ModelSlotManager.GetLoadedLocalSlotNamesForPath(selectedModelPath);
			if(loadedSlotNames.Count == 0 || !Common.LlModelLoaded) return;
			var inheritedSystemPromptSlotNames = loadedSlotNames.Where(SlotUsesInheritedSystemPrompt).ToList();
			var overrideOld = oldSettings?.OverrideSettings ?? false;
			var systemPromptOld = overrideOld ? oldSettings.SystemPrompt : Common.SystemPrompt;
			var ctxSizeOld = overrideOld ? oldSettings.CtxSize : Common.CtxSize;
			var gpuLayersOld = overrideOld ? oldSettings.GPULayers : Common.GPULayers;
			var tempOld = overrideOld ? oldSettings.Temp : Common.Temp;
			var minPOld = overrideOld ? oldSettings.MinP : Common.MinP;
			var topPOld = overrideOld ? oldSettings.TopP : Common.TopP;
			var topKOld = overrideOld ? oldSettings.TopK : Common.TopK;
			var flashOld = overrideOld ? oldSettings.FlashAttn : Common.FlashAttn;
			var offloadKQVOld = overrideOld ? oldSettings.OffloadKQV : Common.OffloadKQV;
			var offloadOPOld = overrideOld ? oldSettings.OffloadOP : Common.OffloadOP;
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
			var offloadKQVEff = overrideNew ? offloadKQVNew : Common.OffloadKQV;
			var offloadOPEff = overrideNew ? offloadOPNew : Common.OffloadOP;
			var jinjaOverrideEff = overrideNew && jinjaOverrideNew;
			var jinjaTmplEff = jinjaOverrideEff ? jinjaTmplNew : string.Empty;
			var reloadModel = gpuLayersOld != gpuLayersEff || jinjaOverrideOld != jinjaOverrideEff || jinjaTmplOld != jinjaTmplEff;
			var reloadCtx = ctxSizeOld != ctxSizeEff || flashOld != flashEff || offloadKQVOld != offloadKQVEff || offloadOPOld != offloadOPEff;
			var reloadSmpl = tempOld != tempEff || minPOld != minPEff || topPOld != topPEff || topKOld != topKEff;
			var setSystemPrompt = systemPromptOld != systemPromptEff;
			if(setSystemPrompt && inheritedSystemPromptSlotNames.Count < loadedSlotNames.Count)
				MessageBox.Show(this, Resources.The_modified_settings_are_overridden_, Resources.LM_Stud, MessageBoxButtons.OK, MessageBoxIcon.Information);
			if(reloadModel
				&& MessageBox.Show(this, Resources.A_changed_setting_requires_the_model_to_be_reloaded__reload_now_, Resources.LM_Stud, MessageBoxButtons.YesNo, MessageBoxIcon.Question)
				== DialogResult.Yes){
				foreach(var slotName in loadedSlotNames) LoadModel(slotName, selectedModel, false);
			} else{
				var reloads = new List<ReloadRequest>();
				if(reloadCtx){
					var contextSize = ClampContextSize(selectedModel, ctxSizeEff);
					foreach(var slotName in loadedSlotNames){
						UpdateCommonContextLimitForSlot(slotName, selectedModel, contextSize);
						reloads.Add(new ReloadRequest{
							SlotName = slotName, ReloadContext = true, ContextSize = contextSize, FlashAttn = flashEff, OffloadKQV = offloadKQVEff, OffloadOP = offloadOPEff
						});
					}
				}
				if(reloadSmpl)
					foreach(var slotName in loadedSlotNames){
						var reload = reloads.FirstOrDefault(item => string.Equals(item.SlotName, slotName, StringComparison.OrdinalIgnoreCase));
						if(reload == null){
							reload = new ReloadRequest{ SlotName = slotName };
							reloads.Add(reload);
						}
						reload.ReloadSampler = true;
						reload.MinP = minPEff;
						reload.TopP = topPEff;
						reload.TopK = topKEff;
						reload.Temp = tempEff;
					}
				QueueReloads(reloads,
					() => {
						if(setSystemPrompt) ThreadPool.QueueUserWorkItem(o => {SetSystemPromptsForSlots(inheritedSystemPromptSlotNames);});
					});
				return;
			}
			if(setSystemPrompt) ThreadPool.QueueUserWorkItem(o => {SetSystemPromptsForSlots(inheritedSystemPromptSlotNames);});
		}
		private void PopulateModelSettings(string modelPath){
			var modelLvi = listViewModels.SelectedItems[0];
			var modelCtxMax = GetModelContextMax(modelLvi);
			var ctxSizeToolTip = _numCtxSizeToolTip;
			if(modelCtxMax > 0) ctxSizeToolTip += "\r\n" + Resources.Max_context_size_of_selected_model__ + modelCtxMax;
			toolTip1.SetToolTip(numCtxSizeModel, ctxSizeToolTip);
			var layerCount = GetModelLayerCount(modelLvi);
			var gpuLayersToolTip = toolTip1.GetToolTip(numGPULayers);
			if(layerCount > 0) gpuLayersToolTip += "\r\n" + string.Format(Resources.Max_gpu_layers_of_selected_model, layerCount + 1);
			toolTip1.SetToolTip(numGPULayersModel, gpuLayersToolTip);
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
				checkOffloadKQVModel.Checked = ms.OffloadKQV;
				checkOffloadOPModel.Checked = ms.OffloadOP;
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
				checkOffloadKQVModel.Checked = Common.OffloadKQV;
				checkOffloadOPModel.Checked = Common.OffloadOP;
				checkOverrideJinjaModel.Checked = false;
				textJinjaTmplModel.Text = string.Empty;
			}
		}
		private void ButBrowseJinjaTmplModel_Click(object sender, EventArgs e){
			if(openFileDialog1.ShowDialog(this) == DialogResult.OK) textJinjaTmplModel.Text = openFileDialog1.FileName;
		}
		private void CheckUseModelSettings_CheckedChanged(object sender, EventArgs e){
			groupCommonModel.Enabled = groupAdvancedModel.Enabled = labelSystemPromptModel.Enabled = textSystemPromptModel.Enabled = checkOverrideSettings.Checked;
		}
		private sealed class ReloadRequest{
			internal int ContextSize;
			internal CheckState FlashAttn;
			internal float MinP;
			internal bool OffloadKQV;
			internal bool OffloadOP;
			internal bool ReloadContext;
			internal bool ReloadSampler;
			internal string SlotName;
			internal float Temp;
			internal int TopK;
			internal float TopP;
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
			public readonly bool OffloadKQV;
			public readonly bool OffloadOP;
			public readonly bool OverrideJinja;
			public readonly bool OverrideSettings;
			public readonly string SystemPrompt;
			public readonly float Temp;
			public readonly int TopK;
			public readonly float TopP;
			public ModelSettings(bool overrideSettings,
				string systemPrompt,
				int ctxSize,
				int gpuLayers,
				float temp,
				float minP,
				float topP,
				int topK,
				CheckState flashAttn,
				bool offloadKqv,
				bool offloadOp,
				bool overrideJinja,
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
				OffloadKQV = offloadKqv;
				OffloadOP = offloadOp;
				OverrideJinja = overrideJinja;
				JinjaTemplate = jinjaTemplate;
			}
		}
	}
}