using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using LMStud.Properties;
namespace LMStud{
	public partial class Form1{
		private const string DefaultPrompt = "Assist the user to the best of your ability.";
		private const string FetchPrompt = "\nAfter calling the web_search tool you must subsequently call the get_webpage tool with a url followed by the get_webpage_text tool with the id of any relevant preview.";
		internal volatile SemaphoreSlim PopulateLock = new SemaphoreSlim(1, 1);
		private void CheckLoadAuto_CheckedChanged(object sender, EventArgs e){
			if(checkLoadAuto.Checked && File.Exists(Common.ModelsDir + Settings.Default.LastModel)) Settings.Default.LoadAuto = true;
			else Settings.Default.LoadAuto = false;
			Settings.Default.Save();
		}
		private void ListViewModels_ItemSelectionChanged(object sender, ListViewItemSelectionChangedEventArgs e){
			if(listViewModels.SelectedItems.Count == 1){
				SetModelSpecificTabsEnabled(true);
				PopulateMeta((List<GGUFMetadataManager.GGUFMetadataEntry>)listViewModels.SelectedItems[0].Tag);
				PopulateModelSettings(listViewModels.SelectedItems[0].SubItems[1].Text);
			} else{
				if(!IsHandleCreated || IsDisposed) return;
				BeginInvoke(new MethodInvoker(() => {
					if(IsDisposed || listViewModels.SelectedItems.Count != 0) return;
					listViewMeta.Items.Clear();
					SetModelSpecificTabsEnabled(false);
				}));
			}
		}
		private void SetModelSpecificTabsEnabled(bool enabled){
			tabControlModelStuff.Enabled = true;
			foreach(Control control in tabPageModelSettings.Controls) control.Enabled = enabled;
			foreach(Control control in tabPageMetadata.Controls) control.Enabled = enabled;
			if(!enabled && tabControlModelStuff.SelectedTab != tabPageSlots) tabControlModelStuff.SelectedTab = tabPageSlots;
		}
		private void ListViewModels_KeyDown(object sender, KeyEventArgs e){
			if(e.KeyCode != Keys.F5) return;
			PopulateModels();
		}
		private void ListViewModels_DoubleClick(object sender, EventArgs e){
			ButLoad_Click(null, null);
		}
		private void ButLoad_Click(object sender, EventArgs e) {
			if(listViewModels.SelectedItems.Count == 1){
				LoadModel("main", listViewModels.SelectedItems[0], false);
				return;
			}
			ShowError("Load Main", "Select a model first.", false);
		}
		private void ButUnload_Click(object sender, EventArgs e){
			butUnloadMain.Enabled = false;
			UnloadModel(true, "main");
		}
		private void ButExtract_Click(object sender, EventArgs e){
			const string action = "Extract Template";
			if(listViewModels.SelectedItems.Count == 0){
				ShowError(action, "Select a model first.", false);
				return;
			}
			var selected = listViewModels.SelectedItems[0];
			var modelPath = selected.SubItems[1].Text;
			var meta = selected.Tag as List<GGUFMetadataManager.GGUFMetadataEntry>;
			if(meta == null){
				if(!File.Exists(modelPath)){
					ShowError(action, "Model file not found\r\n\r\n" + modelPath, false);
					return;
				}
				meta = GGUFMetadataManager.LoadGGUFMetadata(modelPath);
			}
			string template = null;
			foreach(var entry in meta){
				if(!string.Equals(entry.Key, "tokenizer.chat_template", StringComparison.Ordinal)) continue;
				template = entry.Val.Value as string;
				break;
			}
			if(string.IsNullOrWhiteSpace(template)){
				foreach(var entry in meta){
					if(entry.Key == null || !entry.Key.EndsWith(".chat_template", StringComparison.Ordinal)) continue;
					template = entry.Val.Value as string;
					if(!string.IsNullOrWhiteSpace(template)) break;
				}
			}
			if(string.IsNullOrWhiteSpace(template)){
				MessageBox.Show(this, "No Jinja chat template was found in the selected model.", Resources.LM_Stud, MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}
			saveFileDialog1.Title = action;
			if(saveFileDialog1.ShowDialog(this) != DialogResult.OK) return;
			try{
				File.WriteAllText(saveFileDialog1.FileName, template, new UTF8Encoding(false));
			} catch(Exception ex){
				ShowError(action, ex.Message, false);
			}
		}
		private bool ModelsFolderExists(bool showError){
			if(!Directory.Exists(Common.ModelsDir)){
				if(showError) MessageBox.Show(this, Resources.Models_folder_not_found_, Resources.LM_Stud, MessageBoxButtons.OK, MessageBoxIcon.Error);
				tabControlMain.SelectTab(1);
				textModelsDir.Focus();
				return false;
			}
			return true;
		}
		private void PopulateModels(){
			if(!PopulateLock.Wait(0) || !ModelsFolderExists(true)) return;
			ThreadPool.QueueUserWorkItem(_ => {
				try{
					var files = Directory.GetFiles(Common.ModelsDir, "*.gguf", SearchOption.AllDirectories);
					var items = new List<ListViewItem>();
					foreach(var file in files){
						var meta = GGUFMetadataManager.LoadGGUFMetadata(file);
						var name = Path.GetFileNameWithoutExtension(file);
						var lvi = new ListViewItem(name){ Name = name };
						lvi.SubItems.Add(file);
						lvi.Tag = meta;
						items.Add(lvi);
					}
					Invoke(new MethodInvoker(() => {
						try{
							listViewModels.BeginUpdate();
							listViewModels.Items.Clear();
							listViewModels.Items.AddRange(items.ToArray());
						} finally{ listViewModels.EndUpdate(); }
					}));
				} catch(Exception ex){
					Invoke(new MethodInvoker(() => {
						MessageBox.Show(this, ex.ToString(), Resources.LM_Stud, MessageBoxButtons.OK, MessageBoxIcon.Error);
					}));
				} finally{
					PopulateLock.Release();
				}
			});
		}
		internal void ShowError(string action, NativeMethods.StudError error){
			string detail;
			switch(error){
				case NativeMethods.StudError.CantLoadModel:
					detail = Resources.The_model_could_not_be_loaded_;
					break;
				case NativeMethods.StudError.ModelNotLoaded:
					detail = Resources.No_model_has_been_loaded_;
					break;
				case NativeMethods.StudError.CantCreateContext:
					detail = Resources.Failed_to_create_the_model_context_;
					break;
				case NativeMethods.StudError.CantCreateSampler:
					detail = Resources.Failed_to_create_the_sampler_;
					break;
				case NativeMethods.StudError.CantApplyTemplate:
					detail = Resources.The_chat_template_could_not_be_applied_;
					break;
				case NativeMethods.StudError.ContextFull:
					detail = Resources.Context_full;
					break;
				case NativeMethods.StudError.LlamaDecodeError:
					detail = Resources.The_model_backend_returned_a_decode_error_;
					break;
				case NativeMethods.StudError.IndexOutOfRange:
					detail = Resources.An_index_was_out_of_range_;
					break;
				case NativeMethods.StudError.CantTokenizePrompt:
					detail = Resources.Failed_to_tokenize_the_prompt_;
					break;
				case NativeMethods.StudError.CantConvertToken:
					detail = Resources.Failed_to_convert_a_token_;
					break;
				case NativeMethods.StudError.ChatParseError:
					detail = Resources.Unable_to_parse_the_chat_message_;
					break;
				case NativeMethods.StudError.GpuOutOfMemory:
					detail = Resources.The_GPU_is_out_of_memory_;
					break;
				case NativeMethods.StudError.CantLoadWhisperModel:
					detail = Resources.Error_loading_Whisper_model_;
					break;
				case NativeMethods.StudError.CantLoadVADModel:
					detail = Resources.Error_loading_VAD_model_;
					break;
				case NativeMethods.StudError.CantInitAudioCapture:
					detail = Resources.Error_initializing_audio_capture_;
					break;
				case NativeMethods.StudError.Generic:
					detail = "";
					break;
				default:
					detail = error.ToString();
					break;
			}
			var extra = NativeMethods.GetLastError();
			NativeMethods.ClearLastErrorMessage();
			if(!string.IsNullOrEmpty(extra)) detail += "\r\n\r\n" + extra;
			ShowError(action, detail, false);
		}
		internal void ShowError(string action, string error, bool addNativeMsg){
			if(addNativeMsg){
				var extra = NativeMethods.GetLastError();
				NativeMethods.ClearLastErrorMessage();
				if(!string.IsNullOrWhiteSpace(extra)) error += "\r\n\r\n" + extra;
			}
			void ShowMessage(){MessageBox.Show(this, string.Format(Resources._0____1_, action, error), Resources.LM_Stud, MessageBoxButtons.OK, MessageBoxIcon.Error);}
			if(InvokeRequired) Invoke(new MethodInvoker(ShowMessage));
			else ShowMessage();
		}
		private void SetSystemPromptForSlot(string slotName, bool acquireSlotLock = true){
			if(string.IsNullOrWhiteSpace(slotName)) slotName = NativeChat.GetActiveSlotName();
			ModelSlotLockLease slotLock = null;
			if(acquireSlotLock){
				slotLock = ModelSlotManager.TryEnterSlot(slotName, 0);
				if(slotLock == null) return;
			}
			try{
				var prompt = GetSystemPromptForSlot(slotName);
				var error = NativeChat.SetSystemPrompt(slotName, prompt.Length > 0 ? prompt : DefaultPrompt, Common.GoogleSearchEnable && Common.WebpageFetchEnable ? FetchPrompt : "");
				if(error != NativeMethods.StudError.ModelNotLoaded && error != NativeMethods.StudError.Success) ShowError(Resources.Error_setting_system_prompt, error);
			} finally{ slotLock?.Dispose(); }
		}
		private void SetSystemPromptsForSlots(IEnumerable<string> slotNames){
			foreach(var slotName in (slotNames ?? Enumerable.Empty<string>()).Where(name => !string.IsNullOrWhiteSpace(name)).Distinct(StringComparer.OrdinalIgnoreCase)) SetSystemPromptForSlot(slotName);
		}
		private void SetSystemPromptsForLoadedSlots(){SetSystemPromptsForSlots(ModelSlotManager.GetLoadedLocalSlotNames());}
		private string GetSystemPromptForSlot(string slotName){
			if(!string.IsNullOrWhiteSpace(slotName) && Common.LoadedLocalSlots.TryGetValue(slotName, out var loadedModel) && loadedModel?.SubItems.Count > 1){
				if(TryGetEnabledModelSettings(loadedModel.SubItems[1].Text, out var overrides)) return overrides.SystemPrompt ?? "";
			}
			return Common.SystemPrompt ?? "";
		}
		private static string GetModelSettingsKey(string modelPath){
			if(string.IsNullOrWhiteSpace(modelPath)) return null;
			var modelsDir = Common.ModelsDir ?? "";
			return !string.IsNullOrWhiteSpace(modelsDir) && modelPath.StartsWith(modelsDir, StringComparison.OrdinalIgnoreCase) ? modelPath.Substring(modelsDir.Length) : modelPath;
		}
		private void SetModelStatus(){
			var activeSlot = ModelSlotManager.GetActiveChatSlot();
			var loadedSlot = ModelSlotManager.GetLoadedLocalSlot();
			var activeLoadedModel = GetLoadedModelForSlot(activeSlot);
			var activeLocalLoaded = activeLoadedModel != null;
			var activeApiReady = ModelSlotManager.CanServeApiSlot(activeSlot);
			if(activeLocalLoaded) Common.LoadedModel = activeLoadedModel;
			butGen.Enabled = (activeApiReady || activeLocalLoaded) && !Generation.Generating;
			butReset.Enabled = !Generation.Generating;
			butUnloadMain.Enabled = Common.LoadedLocalSlots.ContainsKey("main") && NativeMethods.IsModelSlotLoaded("main");
			if(checkDialectic.Checked && Generation.DialecticRelayEnabled) toolStripStatusLabel1.Text = "Dialectic relay: " + Generation.DialPriSlotName + " <-> " + Generation.DialSecSlotName;
			else if(activeApiReady) toolStripStatusLabel1.Text = "Using slot " + activeSlot.Name + ": " + activeSlot.DisplayModel();
			else if(activeLocalLoaded && activeSlot != null) toolStripStatusLabel1.Text = "Using slot " + activeSlot.Name + ": " + activeLoadedModel.Text;
			else if(Common.LlModelLoaded && Common.LoadedModel != null) toolStripStatusLabel1.Text = Resources.Using_Model_ + Common.LoadedModel.Text;
			else toolStripStatusLabel1.Text = Resources.No_model_loaded;
			if(activeApiReady && loadedSlot != null) toolStripStatusLabel1.Text += " | Loaded: " + loadedSlot.Name;
		}
		private static ListViewItem GetLoadedModelForSlot(ModelSlot slot){
			if(slot == null || slot.Source != ModelSlotSource.Local) return null;
			if(!Common.LoadedLocalSlots.TryGetValue(slot.Name, out var loadedModel)) return null;
			return ModelSlotManager.CanServeLocalSlot(slot) ? loadedModel : null;
		}
		private NativeMethods.StudError LoadModel(string slotName, string filename, string jinjaTemplate, int nGPULayers, bool mMap, bool mLock, NativeMethods.GgmlNumaStrategy numaStrategy){
			var result = NativeMethods.LoadModel(slotName, filename, jinjaTemplate, nGPULayers, mMap, mLock, numaStrategy);
			if(result != NativeMethods.StudError.Success) ShowError(Resources.Error_loading_model, result);
			return result;
		}
		private NativeMethods.StudError CreateSession(string slotName, int nCtx, int nBatch, CheckState flashAttn, int nThreads, int nThreadsBatch, float minP, float topP, int topK, float temp, float repeatPenalty, NativeMethods.QuantType kType, NativeMethods.QuantType vType){
			var result = NativeMethods.CreateSession(slotName, nCtx, nBatch, (uint)flashAttn, nThreads, nThreadsBatch, minP, topP, topK, temp, repeatPenalty, (int)kType, (int)vType);
			if(result != NativeMethods.StudError.Success) ShowError(Resources.Error_creating_session, result);
			return result;
		}
		private NativeMethods.StudError CreateContext(string slotName, int nCtx, int nBatch, CheckState flashAttn, int nThreads, int nThreadsBatch, NativeMethods.QuantType kType, NativeMethods.QuantType vType){
			var slotLock = ModelSlotManager.EnterSlot(slotName);
			NativeMethods.StudError result;
			try{ result = NativeMethods.CreateContext(slotName, nCtx, nBatch, (uint)flashAttn, nThreads, nThreadsBatch, (int)kType, (int)vType); } finally{ slotLock.Dispose(); }
			if(result != NativeMethods.StudError.Success) ShowError(Resources.Error_creating_context, result);
			return result;
		}
		private NativeMethods.StudError CreateSampler(string slotName, float minP, float topP, int topK, float temp, float repeatPenalty){
			var slotLock = ModelSlotManager.EnterSlot(slotName);
			NativeMethods.StudError result;
			try{ result = NativeMethods.CreateSampler(slotName, minP, topP, topK, temp, repeatPenalty); } finally{ slotLock.Dispose(); }
			if(result != NativeMethods.StudError.Success) ShowError(Resources.Error_creating_sampler, result);
			return result;
		}
		private NativeMethods.StudError LoadWhisperModel(string modelPath, int nThreads, bool useGPU, bool useVAD, string vadModel){
			var result = NativeMethods.LoadWhisperModel(modelPath, nThreads, useGPU, useVAD, vadModel);
			if(result != NativeMethods.StudError.Success) ShowError(Resources.Error_initialising_voice_input, result);
			return result;
		}
		internal void LoadModel(ListViewItem modelLvi, bool autoLoad){LoadModel("main", modelLvi, autoLoad);}
		internal void LoadModel(string slotName, ListViewItem modelLvi, bool autoLoad){
			var modelPath = modelLvi.SubItems[1].Text;
			if(!File.Exists(modelPath)){
				ShowError("LoadModel", "Model file not found\r\n\r\n" + modelPath, false);
				checkLoadAuto.Checked = false;
				return;
			}
			SetModelLoadButtonsEnabled(false);
			var modelsDir = Common.ModelsDir;
			var fileName = Path.GetFileName(modelPath);
			var meta = (List<GGUFMetadataManager.GGUFMetadataEntry>)modelLvi.Tag;
			var activeChatSlot = ModelSlotManager.GetActiveChatSlot();
			var loadingActiveChatSlot = activeChatSlot != null && string.Equals(activeChatSlot.Name, slotName, StringComparison.OrdinalIgnoreCase);
			var activeChatSlotLoaded = activeChatSlot?.Source == ModelSlotSource.Local && ModelSlotManager.CanServeLocalSlot(activeChatSlot);
			var makeSlotChat = string.Equals(slotName, "main", StringComparison.OrdinalIgnoreCase) ||
				(activeChatSlot?.Source == ModelSlotSource.Local && !activeChatSlotLoaded);
			if(activeChatSlotLoaded && !loadingActiveChatSlot && !string.Equals(slotName, "main", StringComparison.OrdinalIgnoreCase)) makeSlotChat = false;
			ThreadPool.QueueUserWorkItem(o => {
				var slotLock = ModelSlotManager.EnterSlot(slotName);
				ModelSettings overrides;
				var overrideSettings = TryGetEnabledModelSettings(modelPath, out overrides);
				var ctxSize = overrideSettings ? overrides.CtxSize : Common.CtxSize;
				var gpuLayers = overrideSettings ? overrides.GPULayers : Common.GPULayers;
				var temp = overrideSettings ? overrides.Temp : Common.Temp;
				var minP = overrideSettings ? overrides.MinP : Common.MinP;
				var topP = overrideSettings ? overrides.TopP : Common.TopP;
				var topK = overrideSettings ? overrides.TopK : Common.TopK;
				var flashAttn = overrideSettings ? overrides.FlashAttn : Common.FlashAttn;
				var jinjaTmpl = overrideSettings && overrides != null && overrides.OverrideJinja && File.Exists(overrides.JinjaTemplate) ? File.ReadAllText(overrides.JinjaTemplate) : null;
				try{
					Invoke(new MethodInvoker(() => {toolStripStatusLabel1.Text = Resources.Loading__ + fileName;}));
					unsafe{
						var result = LoadModel(slotName, modelPath, jinjaTmpl, gpuLayers, Common.MMap, Common.MLock, Common.NumaStrat);
						if(result != NativeMethods.StudError.Success){
							Settings.Default.LoadAuto = false;
							Settings.Default.Save();
							Common.LoadedLocalSlots.Remove(slotName);
							Common.LlModelLoaded = Common.LoadedLocalSlots.Count > 0;
							Common.LoadedModel = Common.LoadedLocalSlots.TryGetValue(Common.ActiveModelSlotName ?? "", out var activeLoadedAfterLoadFailure) ? activeLoadedAfterLoadFailure : Common.LoadedLocalSlots.Values.FirstOrDefault();
							return;
						}
						Common.LoadedModel = modelLvi;
						var modelCtxMax = GetModelContextMax(modelLvi);
						var contextSize = ClampContextSize(modelLvi, ctxSize);
						Common.ModelCtxMax = modelCtxMax;
						Common.CntCtxMax = contextSize;
						result = CreateSession(slotName, contextSize, Common.BatchSize, flashAttn, Common.NThreads, Common.NThreadsBatch, minP, topP, topK, temp, Common.RepPen, Common.KType, Common.VType);
						Common.LlModelLoaded = true;
						if(result != NativeMethods.StudError.Success){
							Settings.Default.LoadAuto = false;
							Settings.Default.Save();
							NativeMethods.FreeModelSlot(slotName);
							Common.LoadedLocalSlots.Remove(slotName);
							Common.LlModelLoaded = Common.LoadedLocalSlots.Count > 0;
							Common.LoadedModel = Common.LoadedLocalSlots.TryGetValue(Common.ActiveModelSlotName ?? "", out var activeLoadedAfterSessionFailure) ? activeLoadedAfterSessionFailure : Common.LoadedLocalSlots.Values.FirstOrDefault();
							return;
						}
						NativeMethods.SetTokenCallback(TokenCallback);
						Tools.RegisterTools(slotName);
						Common.LoadedLocalSlots[slotName] = modelLvi;
						SetSystemPromptForSlot(slotName, false);
						ModelSlotManager.LoadLocalIntoSlot(slotName, modelPath, makeSlotChat);
						ApplyActiveSlotToModel(true);
						try{
							BeginInvoke(new MethodInvoker(() => {
								Settings.Default.LastModel = modelPath.Substring(modelsDir.Length);
								if(checkLoadAuto.Checked && !autoLoad){
									Settings.Default.LoadAuto = true;
									Settings.Default.Save();
								}
								toolTip1.SetToolTip(numCtxSize, _numCtxSizeToolTip + "\r\n" + Resources.Max_context_size_of_last_loaded_model + Common.ModelCtxMax);
							}));
						} catch(ObjectDisposedException){}
					}
				} finally{
					slotLock.Dispose();
					BeginInvoke(new MethodInvoker(() => {
						SetModelStatus();
						PopulateSlotsList();
						butLoadMain.Enabled = true;
						UpdateSlotButtons();
					}));
				}
			});
		}
		private void UnloadModelInternal(bool genLock, string slotName){
			ModelSlotLockLease slotLock = null;
			try{
				Generation.StopActiveGeneration();
				if(string.IsNullOrWhiteSpace(slotName)) slotName = Common.ActiveModelSlotName ?? "main";
				if(genLock) slotLock = ModelSlotManager.EnterSlot(slotName);
				NativeMethods.FreeModelSlot(slotName);
			} finally{
				if(!string.IsNullOrWhiteSpace(slotName)) Common.LoadedLocalSlots.Remove(slotName);
				Common.LlModelLoaded = Common.LoadedLocalSlots.Count > 0;
				Common.LoadedModel = Common.LoadedLocalSlots.TryGetValue(Common.ActiveModelSlotName ?? "", out var activeLoaded) ? activeLoaded : Common.LoadedLocalSlots.Values.FirstOrDefault();
				try{
					BeginInvoke(new MethodInvoker(() => {
						toolTip1.SetToolTip(numCtxSize, _numCtxSizeToolTip);
						SetModelStatus();
						PopulateSlotsList();
						UpdateSlotButtons();
					}));
				} catch(ObjectDisposedException){}
				slotLock?.Dispose();
			}
		}
		private void UnloadModel(bool genLock, string slotName){
			SetModelLoadButtonsEnabled(false);
			ThreadPool.QueueUserWorkItem(o => UnloadModelInternal(genLock, slotName));
		}
		private void SetModelLoadButtonsEnabled(bool enabled){
			butLoadMain.Enabled = enabled;
			butUnloadMain.Enabled = enabled && Common.LoadedLocalSlots.ContainsKey("main") && NativeMethods.IsModelSlotLoaded("main");
			butLoadSlot.Enabled = enabled;
			butUnloadSlot.Enabled = enabled;
		}
		private static string ConvertValueToString(GGUFMetadataManager.GGUFMetaValue metaVal){
			switch(metaVal.Type){
				case GGUFMetadataManager.GGUFType.ARRAY:
					if(!(metaVal.Value is List<object> list)) return "[]";
					const int limit = 32;
					var truncated = list.Count > limit ? list.GetRange(0, limit) : list;
					var joined = string.Join(", ", truncated);
					return list.Count > limit ? $"[ {joined}, ... (total {list.Count} items) ]" : $"[ {joined} ]";
				default: return metaVal.Value?.ToString() ?? "";
			}
		}
		private void PopulateMeta(List<GGUFMetadataManager.GGUFMetadataEntry> meta){
			listViewMeta.Items.Clear();
			foreach(var entry in meta){
				var key = entry.Key ?? "";
				var valueStr = ConvertValueToString(entry.Val);
				var lvi = new ListViewItem(key);
				lvi.SubItems.Add(valueStr);
				listViewMeta.Items.Add(lvi);
			}
		}
	}
}
