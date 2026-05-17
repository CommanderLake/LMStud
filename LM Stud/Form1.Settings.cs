using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using LMStud.Properties;
namespace LMStud{
	public partial class Form1{
		private readonly List<string> _whisperModels = new List<string>();
		private void LoadConfig(){
			var mp = Settings.Default.ModelsDir;
			mp = mp[mp.Length - 1] == '\\' || mp[mp.Length - 1] == '/' ? mp : mp + '\\';
			Common.ModelsDir = textModelsDir.Text = mp;
			Common.SystemPrompt = textSystemPrompt.Text = Settings.Default.SystemPrompt;
			Common.CtxSize = (int)(numCtxSize.Value = Settings.Default.CtxSize);
			Common.GPULayers = (int)(numGPULayers.Value = Settings.Default.GPULayers);
			Common.Temp = (float)(numTemp.Value = Settings.Default.Temp);
			Common.NGen = (int)(numNGen.Value = Settings.Default.NGen);
			Common.NumaStrat = (NativeMethods.GgmlNumaStrategy)(comboNUMAStrat.SelectedIndex = Settings.Default.NUMAStrat);
			Common.RepPen = (float)(numRepPen.Value = Settings.Default.RepPen);
			Common.TopK = (int)(numTopK.Value = Settings.Default.TopK);
			Common.TopP = (float)(numTopP.Value = Settings.Default.TopP);
			Common.MinP = (float)(numMinP.Value = Settings.Default.MinP);
			Common.BatchSize = (int)(numBatchSize.Value = Settings.Default.BatchSize);
			Common.KType = (NativeMethods.QuantType)(comboKType.SelectedIndex = Settings.Default.KType);
			Common.VType = (NativeMethods.QuantType)(comboVType.SelectedIndex = Settings.Default.VType);
			Common.MMap = checkMMap.Checked = Settings.Default.MMap;
			Common.MLock = checkMLock.Checked = Settings.Default.MLock;
			Common.NThreads = (int)(numThreads.Value = Settings.Default.Threads);
			Common.NThreadsBatch = (int)(numThreadsBatch.Value = Settings.Default.ThreadsBatch);
			Common.WhisperModel = Settings.Default.WhisperModel;
			Common.VADModel = Settings.Default.VADModel;
			Common.WakeWord = textWakeWord.Text = Settings.Default.WakeWord;
			Common.WakeWordSimilarity = (float)(numWakeWordSimilarity.Value = Settings.Default.WakeWordSimilarity);
			try{ Common.VADThreshold = (float)(numVadThreshold.Value = Settings.Default.VadThreshold); } catch(ArgumentOutOfRangeException){ Common.VADThreshold = (float)numVadThreshold.Value; }
			Common.FreqThreshold = (float)(numFreqThreshold.Value = Settings.Default.FreqThreshold);
			Common.WhisperUseGPU = checkWhisperUseGPU.Checked = Settings.Default.whisperUseGPU;
			Common.WhisperTemp = (float)(numWhisperTemp.Value = Settings.Default.WhisperTemp);
			Common.UseWhisperVAD = radioWhisperVAD.Checked = Settings.Default.UseWhisperVAD;
			Common.Speak = checkSpeak.Checked = Settings.Default.Speak;
			Common.FlashAttn = checkFlashAttn.CheckState = (CheckState)Settings.Default.FlashAttn;
			Common.GoogleAPIKey = textGoogleApiKey.Text = Settings.Default.GoogleAPIKey;
			Common.GoogleSearchID = textGoogleSearchID.Text = Settings.Default.GoogleSearchID;
			Common.GoogleSearchResultCount = (int)(numGoogleResults.Value = Settings.Default.GoogleSearchResultCount);
			Common.GoogleSearchEnable = checkGoogleEnable.Checked = Settings.Default.GoogleSearchEnable;
			Common.WebpageFetchEnable = checkWebpageFetchEnable.Checked = Settings.Default.WebpageFetchEnable;
			Common.FileBaseDir = textFileBasePath.Text = Settings.Default.FileBaseDir;
			Common.FileListEnable = checkFileListEnable.Checked = Settings.Default.FileListEnable;
			Common.FileCreateEnable = checkFileCreateEnable.Checked = Settings.Default.FileCreateEnable;
			Common.FileReadEnable = checkFileReadEnable.Checked = Settings.Default.FileReadEnable;
			Common.FileWriteEnable = checkFileWriteEnable.Checked = Settings.Default.FileWriteEnable;
			Common.DateTimeEnable = checkDateTimeEnable.Checked = Settings.Default.DateTimeEnable;
			Common.CMDEnable = checkCMDEnable.Checked = Settings.Default.CMDToolEnable;
			Common.CMDTimeoutMs = (int)(numCmdTimeout.Value = Settings.Default.CMDToolTimeoutMs);
			Common.APIServerEnable = checkApiServerEnable.Checked = Settings.Default.APIServerEnable;
			Common.APIServerPort = (int)(numApiServerPort.Value = Settings.Default.APIServerPort);
			Common.GenDelay = (int)(numGenDelay.Value = Settings.Default.GenDelay);
			NativeMethods.SetSilenceTimeout(Common.GenDelay);
			NativeMethods.SetFileBaseDir(Common.FileBaseDir);
			NativeMethods.SetWakeCommand(Common.WakeWord);
			NativeMethods.SetVADThresholds(Common.VADThreshold, Common.FreqThreshold);
			NativeMethods.SetWakeWordSimilarity(Common.WakeWordSimilarity);
			NativeMethods.SetWhisperTemp(Common.WhisperTemp);
			NativeMethods.SetGoogle(Common.GoogleAPIKey, Common.GoogleSearchID, Common.GoogleSearchResultCount);
			NativeMethods.SetCommandPromptTimeout(Common.CMDTimeoutMs);
		}
		private static void UpdateSetting<T>(ref T currentValue, T newValue, Action<T> updateAction){
			if(EqualityComparer<T>.Default.Equals(currentValue, newValue)) return;
			currentValue = newValue;
			updateAction(newValue);
		}
		private void ButApply_Click(object sender, EventArgs e){
			var popModels = false;
			var reloadModelForAllSlots = false;
			var reloadModelForDefaultSlots = false;
			var reloadCtxForAllSlots = false;
			var reloadCtxForDefaultSlots = false;
			var reloadSmplForAllSlots = false;
			var reloadSmplForDefaultSlots = false;
			var reloadWhisper = false;
			var setVAD = false;
			var setWWS = false;
			var setGoogle = false;
			var registerTools = false;
			var setSystemPrompt = false;
			var modelOverrideChanged = false;
			UpdateSetting(ref Common.SystemPrompt, textSystemPrompt.Text, value => {
				Settings.Default.SystemPrompt = value;
				setSystemPrompt = true;
			});
			if(Directory.Exists(textModelsDir.Text)){
				UpdateSetting(ref Common.ModelsDir, textModelsDir.Text, mp => {
					mp = mp[mp.Length - 1] == '\\' || mp[mp.Length - 1] == '/' ? mp : mp + '\\';
					textModelsDir.Text = Common.ModelsDir = Settings.Default.ModelsDir = mp;
					popModels = true;
				});
			}
			else{
				MessageBox.Show(this, Resources.Models_folder_not_found, Resources.LM_Stud, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
				textModelsDir.Text = Common.ModelsDir = Settings.Default.ModelsDir;
			}
			UpdateSetting(ref Common.CtxSize, (int)numCtxSize.Value, value => {
				Settings.Default.CtxSize = value;
				reloadCtxForDefaultSlots = true;
			});
			UpdateSetting(ref Common.GPULayers, (int)numGPULayers.Value, value => {
				Settings.Default.GPULayers = value;
				reloadModelForDefaultSlots = true;
			});
			UpdateSetting(ref Common.Temp, (float)numTemp.Value, value => {
				Settings.Default.Temp = numTemp.Value;
				reloadSmplForDefaultSlots = true;
			});
			UpdateSetting(ref Common.NGen, (int)numNGen.Value, value => {Settings.Default.NGen = value;});
			var newNumaIndex = comboNUMAStrat.SelectedIndex;
			UpdateSetting(ref Common.NumaStrat, (NativeMethods.GgmlNumaStrategy)newNumaIndex, value => {
				Settings.Default.NUMAStrat = newNumaIndex;
				reloadModelForAllSlots = true;
			});
			UpdateSetting(ref Common.RepPen, (float)numRepPen.Value, value => {
				Settings.Default.RepPen = numRepPen.Value;
				reloadSmplForAllSlots = true;
			});
			UpdateSetting(ref Common.TopK, (int)numTopK.Value, value => {
				Settings.Default.TopK = value;
				reloadSmplForDefaultSlots = true;
			});
			UpdateSetting(ref Common.TopP, (float)numTopP.Value, value => {
				Settings.Default.TopP = numTopP.Value;
				reloadSmplForDefaultSlots = true;
			});
			UpdateSetting(ref Common.MinP, (float)numMinP.Value, value => {
				Settings.Default.MinP = numMinP.Value;
				reloadSmplForDefaultSlots = true;
			});
			UpdateSetting(ref Common.BatchSize, (int)numBatchSize.Value, value => {
				Settings.Default.BatchSize = value;
				reloadCtxForAllSlots = true;
			});
			var newKType = comboKType.SelectedIndex;
			UpdateSetting(ref Common.KType, (NativeMethods.QuantType)newKType, value => {
				Settings.Default.KType = newKType;
				reloadCtxForAllSlots = true;
			});
			var newVType = comboVType.SelectedIndex;
			UpdateSetting(ref Common.VType, (NativeMethods.QuantType)newVType, value => {
				Settings.Default.VType = newVType;
				reloadCtxForAllSlots = true;
			});
			UpdateSetting(ref Common.MMap, checkMMap.Checked, value => {
				Settings.Default.MMap = value;
				reloadModelForAllSlots = true;
			});
			UpdateSetting(ref Common.MLock, checkMLock.Checked, value => {
				Settings.Default.MLock = value;
				reloadModelForAllSlots = true;
			});
			if(Common.NThreads != (int)numThreads.Value || Common.NThreadsBatch != (int)numThreadsBatch.Value){
				UpdateSetting(ref Common.NThreads, (int)numThreads.Value, value => {Settings.Default.Threads = value;});
				UpdateSetting(ref Common.NThreadsBatch, (int)numThreadsBatch.Value, value => {Settings.Default.ThreadsBatch = value;});
				NativeMethods.SetThreadCount((int)numThreads.Value, (int)numThreadsBatch.Value);
			}
			string GetModelString(int index){return index < 0 ? "" : _whisperModels[index].Substring(Common.ModelsDir.Length);}
			UpdateSetting(ref Common.WhisperModel, GetModelString(comboWhisperModel.SelectedIndex), value => {
				Settings.Default.WhisperModel = value;
				reloadWhisper = true;
			});
			UpdateSetting(ref Common.VADModel, GetModelString(comboVADModel.SelectedIndex), value => {
				Settings.Default.VADModel = value;
				reloadWhisper = true;
			});
			UpdateSetting(ref Common.WakeWord, textWakeWord.Text, value => {
				Settings.Default.WakeWord = value;
				NativeMethods.SetWakeCommand(value);
			});
			UpdateSetting(ref Common.WakeWordSimilarity, (float)numWakeWordSimilarity.Value, value => {
				Settings.Default.WakeWordSimilarity = numWakeWordSimilarity.Value;
				setWWS = true;
			});
			UpdateSetting(ref Common.FreqThreshold, (float)numFreqThreshold.Value, value => {
				Settings.Default.FreqThreshold = numFreqThreshold.Value;
				setVAD = true;
			});
			UpdateSetting(ref Common.WhisperTemp, (float)numWhisperTemp.Value, value => {
				Settings.Default.WhisperTemp = numWhisperTemp.Value;
				reloadWhisper = true;
			});
			UpdateSetting(ref Common.WhisperUseGPU, checkWhisperUseGPU.Checked, value => {
				Settings.Default.whisperUseGPU = value;
				reloadWhisper = true;
			});
			UpdateSetting(ref Common.UseWhisperVAD, radioWhisperVAD.Checked, value => {
				Settings.Default.UseWhisperVAD = value;
				reloadWhisper = true;
			});
			UpdateSetting(ref Common.VADThreshold, (float)numVadThreshold.Value, value => {
				Settings.Default.VadThreshold = numVadThreshold.Value;
				setVAD = true;
			});
			UpdateSetting(ref Common.FlashAttn, checkFlashAttn.CheckState, value => {
				Settings.Default.FlashAttn = (uint)value;
				reloadCtxForDefaultSlots = true;
			});
			UpdateSetting(ref Common.GoogleAPIKey, textGoogleApiKey.Text, value => {
				Settings.Default.GoogleAPIKey = value;
				setGoogle = true;
			});
			UpdateSetting(ref Common.GoogleSearchID, textGoogleSearchID.Text, value => {
				Settings.Default.GoogleSearchID = value;
				setGoogle = true;
			});
			UpdateSetting(ref Common.GoogleSearchResultCount, (int)numGoogleResults.Value, value => {
				Settings.Default.GoogleSearchResultCount = value;
				setGoogle = true;
			});
			UpdateSetting(ref Common.GoogleSearchEnable, checkGoogleEnable.Checked, value => {
				Settings.Default.GoogleSearchEnable = value;
				registerTools = true;
			});
			UpdateSetting(ref Common.WebpageFetchEnable, checkWebpageFetchEnable.Checked, value => {
				Settings.Default.WebpageFetchEnable = value;
				registerTools = true;
			});
			UpdateSetting(ref Common.FileBaseDir, textFileBasePath.Text, value => {
				Settings.Default.FileBaseDir = value;
				NativeMethods.SetFileBaseDir(value);
			});
			UpdateSetting(ref Common.FileListEnable, checkFileListEnable.Checked, value => {
				Settings.Default.FileListEnable = value;
				registerTools = true;
			});
			UpdateSetting(ref Common.FileCreateEnable, checkFileCreateEnable.Checked, value => {
				Settings.Default.FileCreateEnable = value;
				registerTools = true;
			});
			UpdateSetting(ref Common.FileReadEnable, checkFileReadEnable.Checked, value => {
				Settings.Default.FileReadEnable = value;
				registerTools = true;
			});
			UpdateSetting(ref Common.FileWriteEnable, checkFileWriteEnable.Checked, value => {
				Settings.Default.FileWriteEnable = value;
				registerTools = true;
			});
			UpdateSetting(ref Common.DateTimeEnable, checkDateTimeEnable.Checked, value => {
				Settings.Default.DateTimeEnable = value;
				registerTools = true;
			});
			UpdateSetting(ref Common.CMDEnable, checkCMDEnable.Checked, value => {
				Settings.Default.CMDToolEnable = value;
				registerTools = true;
			});
			UpdateSetting(ref Common.CMDTimeoutMs, (int)numCmdTimeout.Value, value => {
				Settings.Default.CMDToolTimeoutMs = value;
				NativeMethods.SetCommandPromptTimeout(value);
			});
			UpdateSetting(ref Common.APIServerPort, (int)numApiServerPort.Value, value => {
				Settings.Default.APIServerPort = value;
				if(Common.APIServerEnable){
					ApiServer.Stop();
					ApiServer.Start();
				}
			});
			UpdateSetting(ref Common.APIServerEnable, checkApiServerEnable.Checked, value => {
				Settings.Default.APIServerEnable = value;
				if(value) ApiServer.Start();
				else ApiServer.Stop();
			});
			UpdateSetting(ref Common.GenDelay, (int)numGenDelay.Value, value => {
				Settings.Default.GenDelay = value;
				NativeMethods.SetSilenceTimeout(value);
			});
			if(popModels){
				PopulateModels();
				PopulateWhisperModels(true, true);
			}
			var defaultModelSettingChanged = setSystemPrompt || reloadModelForDefaultSlots || reloadCtxForDefaultSlots || reloadSmplForDefaultSlots;
			var reloads = new List<ReloadRequest>();
			if(Common.LlModelLoaded){
				var slots = GetLoadedLocalSlotItems();
				if(defaultModelSettingChanged && slots.Any(slot => slot.HasOverrides)) modelOverrideChanged = true;
				if(setSystemPrompt && slots.Any(slot => !SlotUsesInheritedSystemPrompt(slot.SlotName))) modelOverrideChanged = true;
				var anyReloadModel = slots.Any(slot => reloadModelForAllSlots || (reloadModelForDefaultSlots && !slot.HasOverrides));
				var reloadLoadedModels = anyReloadModel &&
					MessageBox.Show(this, Resources.A_changed_setting_requires_the_model_to_be_reloaded__reload_now_, Resources.LM_Stud, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
				foreach(var slot in slots){
					var reloadModel = reloadModelForAllSlots || (reloadModelForDefaultSlots && !slot.HasOverrides);
					var reloadCtx = reloadCtxForAllSlots || (reloadCtxForDefaultSlots && !slot.HasOverrides);
					var reloadSmpl = reloadSmplForAllSlots || (reloadSmplForDefaultSlots && !slot.HasOverrides);
					if(reloadModel && reloadLoadedModels){
						LoadModel(slot.SlotName, slot.LoadedModel, false);
						continue;
					}
					var settings = slot.Overrides;
					var ctxSize = slot.HasOverrides ? settings.CtxSize : Common.CtxSize;
					var flash = slot.HasOverrides ? settings.FlashAttn : Common.FlashAttn;
					var minP = slot.HasOverrides ? settings.MinP : Common.MinP;
					var topP = slot.HasOverrides ? settings.TopP : Common.TopP;
					var topK = slot.HasOverrides ? settings.TopK : Common.TopK;
					var temp = slot.HasOverrides ? settings.Temp : Common.Temp;
					var contextSize = ClampContextSize(slot.LoadedModel, ctxSize);
					if(reloadCtx){
						UpdateCommonContextLimitForSlot(slot.SlotName, slot.LoadedModel, contextSize);
						reloads.Add(new ReloadRequest{
							SlotName = slot.SlotName,
							ReloadContext = true,
							ContextSize = contextSize,
							FlashAttn = flash
						});
					}
					if(reloadSmpl){
						var reload = reloads.FirstOrDefault(item => string.Equals(item.SlotName, slot.SlotName, StringComparison.OrdinalIgnoreCase));
						if(reload == null){
							reload = new ReloadRequest{ SlotName = slot.SlotName };
							reloads.Add(reload);
						}
						reload.ReloadSampler = true;
						reload.MinP = minP;
						reload.TopP = topP;
						reload.TopK = topK;
						reload.Temp = temp;
					}
				}
			}
			if(setVAD) NativeMethods.SetVADThresholds(Common.VADThreshold, Common.FreqThreshold);
			if(setWWS) NativeMethods.SetWakeWordSimilarity(Common.WakeWordSimilarity);
			if(reloadWhisper && _whisperLoaded){
				var vadModelPath = Common.ModelsDir + Common.VADModel;
				var whisperModelPath = Common.ModelsDir + Common.WhisperModel;
				if(Common.WhisperModel == null || !File.Exists(whisperModelPath)){
					checkVoiceInput.Checked = false;
					MessageBox.Show(this, Resources.Error_Whisper_model_not_found, Resources.LM_Stud, MessageBoxButtons.OK, MessageBoxIcon.Error);
				}
				else{
					if(checkVoiceInput.CheckState != CheckState.Unchecked) NativeMethods.StopSpeechTranscription();
					var vadPath = Common.UseWhisperVAD && File.Exists(vadModelPath) ? vadModelPath : "";
					NativeMethods.LoadWhisperModel(whisperModelPath, Common.NThreads, Common.WhisperUseGPU, Common.UseWhisperVAD, vadPath);
					if(checkVoiceInput.CheckState != CheckState.Unchecked) NativeMethods.StartSpeechTranscription();
				}
			}
			if(setGoogle) NativeMethods.SetGoogle(Common.GoogleAPIKey, Common.GoogleSearchID, Common.GoogleSearchResultCount);
			if(registerTools) Tools.RegisterToolsForAllSlots();
			if(modelOverrideChanged) MessageBox.Show(this, Resources.The_modified_settings_are_overridden_, Resources.LM_Stud, MessageBoxButtons.OK, MessageBoxIcon.Information);
			Settings.Default.Save();
			void AfterReload(){
				var activeSlot = ModelSlotManager.GetActiveChatSlot();
				if(activeSlot?.Source != ModelSlotSource.Api && Common.LlModelLoaded){
					ModelSlotManager.SyncMainFromLoadedLocal();
					ApplyActiveSlotToModel();
					PopulateSlotsList();
				}
				if(registerTools || setSystemPrompt) ThreadPool.QueueUserWorkItem(o => {
					var promptSlots = ModelSlotManager.GetLoadedLocalSlotNames();
					if(setSystemPrompt && !registerTools) promptSlots = promptSlots.Where(SlotUsesInheritedSystemPrompt).ToList();
					SetSystemPromptsForSlots(promptSlots);
				});
			}
			QueueReloads(reloads, AfterReload);
		}
		private void ButBrowse_Click(object sender, EventArgs e){
			if(folderBrowserDialog1.ShowDialog(this) == DialogResult.OK) textModelsDir.Text = folderBrowserDialog1.SelectedPath;
		}
		private void TextModelsPath_KeyDown(object sender, KeyEventArgs e){
			if(e.KeyCode != Keys.Enter) return;
			var path = textModelsDir.Text.Trim();
			if(Directory.Exists(path)) textModelsDir.Text = path;
			else MessageBox.Show(this, Resources.Folder_not_found, Resources.LM_Stud, MessageBoxButtons.OK, MessageBoxIcon.Error);
		}
		private void ComboWhisperModel_DropDown(object sender, EventArgs e){
			if(!Directory.Exists(Common.ModelsDir)) return;
			PopulateWhisperModels(true, false);
		}
		private void ButWhispDown_Click(object sender, EventArgs e){
			HugLoadFiles("ggerganov", "whisper.cpp", ".bin");
			tabControlMain.SelectTab(3);
		}
		private void ButDownloadVADModel_Click(object sender, EventArgs e){
			HugLoadFiles("ggml-org", "whisper-vad", ".bin");
			tabControlMain.SelectTab(3);
		}
		private void ComboVADModel_DropDown(object sender, EventArgs e){
			if(!Directory.Exists(Common.ModelsDir)) return;
			PopulateWhisperModels(false, true);
		}
		private void PopulateWhisperModels(bool whisper, bool vad){
			if(!ModelsFolderExists(false)) return;
			UseWaitCursor = true;
			try{
				_whisperModels.Clear();
				if(whisper) comboWhisperModel.Items.Clear();
				if(vad) comboVADModel.Items.Clear();
				var files = Directory.GetFiles(Common.ModelsDir, "*.bin", SearchOption.AllDirectories);
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
				if(whisper) comboWhisperModel.SelectedIndex = comboWhisperModel.Items.IndexOf(Path.GetFileName(Common.WhisperModel));
				if(vad) comboVADModel.SelectedIndex = comboVADModel.Items.IndexOf(Path.GetFileName(Common.VADModel));
			} finally{ UseWaitCursor = false; }
		}
	}
}
