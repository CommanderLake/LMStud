using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using LMStud.Properties;
namespace LMStud{
	internal partial class Form1{
		private const string DefaultPrompt = "Assist the user to the best of your ability.";
		private const string FetchPrompt = "\nAfter calling the web_search tool you must subsequently call the get_webpage tool with a url followed by the get_webpage_text tool with the id of any relevant preview.";
		internal volatile bool LlModelLoaded;
		private volatile bool _populating;
		private int _cntCtxMax;
		private int _modelCtxMax;
		private int _modelIndex;
		private readonly List<ModelInfo> _models = new List<ModelInfo>();
		private readonly List<string> _whisperModels = new List<string>();
		private struct ModelInfo{
			public readonly string FilePath;
			public readonly List<GGUFMetadataManager.GGUFMetadataEntry> Meta;
			public ModelInfo(string filePath, List<GGUFMetadataManager.GGUFMetadataEntry> meta){
				FilePath = filePath;
				Meta = meta;
			}
		}
		private string GetModelPath(string relativePath){return Path.IsPathRooted(relativePath) ? relativePath : Path.Combine(_modelsPath, relativePath);}
		private string GetModelPath(int index){return GetModelPath(_models[index].FilePath);}
		private void CheckLoadAuto_CheckedChanged(object sender, EventArgs e){
			if(checkLoadAuto.Checked && File.Exists(GetModelPath(Settings.Default.LastModel))) Settings.Default.LoadAuto = true;
			else Settings.Default.LoadAuto = false;
			Settings.Default.Save();
		}
		private void ListViewModels_ItemSelectionChanged(object sender, ListViewItemSelectionChangedEventArgs e){
			if(listViewModels.SelectedItems.Count == 1){
				tabControlModelStuff.Enabled = true;
				var index = (int)listViewModels.SelectedItems[0].Tag;
				PopulateMeta(index);
				PopulateModelSettings(index);
			} else{
				listViewMeta.Items.Clear();
				tabControlModelStuff.Enabled = false;
			}
		}
		private void ListViewModels_KeyDown(object sender, KeyEventArgs e){
			if(e.KeyCode != Keys.F5) return;
			PopulateModels();
		}
		private void ButLoad_Click(object sender, EventArgs e){
			if(listViewModels.SelectedItems.Count == 0) return;
			LoadModel((int)listViewModels.SelectedItems[0].Tag, false);
		}
		private void ButUnload_Click(object sender, EventArgs e){
			butUnload.Enabled = false;
			UnloadModel();
		}
		private void ListViewModels_DoubleClick(object sender, EventArgs e){
			if(listViewModels.SelectedItems.Count == 0) return;
			LoadModel((int)listViewModels.SelectedItems[0].Tag, false);
		}
		private void CheckUseModelSettings_CheckedChanged(object sender, EventArgs e){
			groupCommonModel.Enabled = groupAdvancedModel.Enabled = butApplyModelSettings.Enabled = labelSystemPromptModel.Enabled = textSystemPromptModel.Enabled = checkOverrideSettings.Checked;
		}
		private bool ModelsFolderExists(bool showError){
			if(!Directory.Exists(_modelsPath)){
				if(showError) MessageBox.Show(this, Resources.Models_folder_not_found__please_specify_a_valid_folder_in_the_Settings_tab, Resources.LM_Stud, MessageBoxButtons.OK, MessageBoxIcon.Error);
				tabControl1.SelectTab(1);
				textModelsPath.Focus();
				return false;
			}
			return true;
		}
		private void PopulateModels(){
			if(_populating || !ModelsFolderExists(true)) return;
			_populating = true;
			listViewModels.BeginUpdate();
			listViewModels.Items.Clear();
			for(var i = _models.Count - 1; i >= 0; i--) _models[i].Meta.Clear();
			_models.Clear();
			ThreadPool.QueueUserWorkItem(_ => {
				try{
					var files = Directory.GetFiles(_modelsPath, "*.gguf", SearchOption.AllDirectories);
					var items = new List<ListViewItem>();
					foreach(var file in files){
						var meta = GGUFMetadataManager.LoadGGUFMetadata(file);
						var idx = _models.Count;
						var relPath = Uri.UnescapeDataString(new Uri(_modelsPath + Path.DirectorySeparatorChar).MakeRelativeUri(new Uri(file)).ToString().Replace('/', Path.DirectorySeparatorChar));
						_models.Add(new ModelInfo(relPath, meta));
						var lvi = new ListViewItem(Path.GetFileName(file));
						lvi.SubItems.Add(file);
						lvi.Tag = idx;
						items.Add(lvi);
					}
					Invoke(new MethodInvoker(() => {listViewModels.Items.AddRange(items.ToArray());}));
				} catch(Exception ex){ Invoke(new MethodInvoker(() => {MessageBox.Show(this, ex.ToString(), Resources.LM_Stud, MessageBoxButtons.OK, MessageBoxIcon.Error);})); } finally{
					try{ Invoke(new MethodInvoker(() => {listViewModels.EndUpdate();})); } catch(Exception e){
						MessageBox.Show(this, e.ToString(), Resources.LM_Stud, MessageBoxButtons.OK, MessageBoxIcon.Error);
					}
					_populating = false;
				}
			});
		}
		[Localizable(true)]
		private void ShowErrorMessage(string action, NativeMethods.StudError error){
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
				case NativeMethods.StudError.ConvTooLong:
					detail = Resources.The_conversation_is_too_long_for_the_context_window_;
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
				default:
					detail = error.ToString();
					break;
			}
			void ShowMessage(){MessageBox.Show(this, string.Format(Resources._0____1_, action, detail), Resources.LM_Stud, MessageBoxButtons.OK, MessageBoxIcon.Error);}
			if(InvokeRequired){
				Invoke(new MethodInvoker(ShowMessage));
			} else ShowMessage();
		}
		private void SetSystemPrompt(string prompt){
			GenerationLock.Wait(-1);
			NativeMethods.StudError error;
			try{ error = NativeMethods.SetSystemPrompt(prompt.Length > 0 ? prompt : DefaultPrompt, _googleSearchEnable && _webpageFetchEnable ? FetchPrompt : ""); } finally{ GenerationLock.Release(); }
			if(error != NativeMethods.StudError.ModelNotLoaded && error != NativeMethods.StudError.Success) ShowErrorMessage(Resources.Error_setting_system_prompt, error);
		}
		private void SetSystemPrompt(){SetSystemPrompt(_systemPrompt);}
		NativeMethods.StudError LoadModel(string filename, int nGPULayers, bool mMap, bool mLock, NativeMethods.GgmlNumaStrategy numaStrategy){
			var result = NativeMethods.LoadModel(filename, nGPULayers, mMap, mLock, numaStrategy);
			if(result != NativeMethods.StudError.Success) ShowErrorMessage(Resources.Error_loading_model, result);
			return result;
		}
		NativeMethods.StudError CreateSession(int nCtx, int nBatch, bool flashAttn, int nThreads, int nThreadsBatch, float minP, float topP, int topK, float temp, float repeatPenalty){
			GenerationLock.Wait(-1);
			NativeMethods.StudError result;
			try{ result = NativeMethods.CreateSession(nCtx, nBatch, flashAttn, nThreads, nThreadsBatch, minP, topP, topK, temp, repeatPenalty); } finally{ GenerationLock.Release(); }
			if(result != NativeMethods.StudError.Success) ShowErrorMessage(Resources.Error_creating_session, result);
			return result;
		}
		NativeMethods.StudError CreateContext(int nCtx, int nBatch, bool flashAttn, int nThreads, int nThreadsBatch){
			GenerationLock.Wait(-1);
			NativeMethods.StudError result;
			try{ result = NativeMethods.CreateContext(nCtx, nBatch, flashAttn, nThreads, nThreadsBatch); } finally{ GenerationLock.Release(); }
			if(result != NativeMethods.StudError.Success) ShowErrorMessage(Resources.Error_creating_context, result);
			return result;
		}
		NativeMethods.StudError CreateSampler(float minP, float topP, int topK, float temp, float repeatPenalty){
			GenerationLock.Wait(-1);
			NativeMethods.StudError result;
			try{ result = NativeMethods.CreateSampler(minP, topP, topK, temp, repeatPenalty); } finally{ GenerationLock.Release(); }
			if(result != NativeMethods.StudError.Success) ShowErrorMessage(Resources.Error_creating_sampler, result);
			return result;
		}
		NativeMethods.StudError LoadWhisperModel(string modelPath, int nThreads, bool useGPU, bool useVAD, string vadModel){
			var result = NativeMethods.LoadWhisperModel(modelPath, nThreads, useGPU, useVAD, vadModel);
			if(result != NativeMethods.StudError.Success) ShowErrorMessage(Resources.Error_initialising_voice_input, result);
			return result;
		}
		private void LoadModel(int modelIndex, bool autoLoad){
			var whisperOn = checkVoiceInput.CheckState != CheckState.Unchecked;
			var modelKey = _models[modelIndex].FilePath;
			var modelPath = GetModelPath(modelKey);
			var fileName = Path.GetFileName(modelPath);
			var overrideSettings = _modelSettings.TryGetValue(modelKey, out var settings) && settings.OverrideSettings;
			var systemPrompt = overrideSettings ? settings.SystemPrompt : _systemPrompt;
			var ctxSize = overrideSettings ? settings.CtxSize : _ctxSize;
			var gpuLayers = overrideSettings ? settings.GPULayers : _gpuLayers;
			var temp = overrideSettings ? settings.Temp : _temp;
			var minP = overrideSettings ? settings.MinP : _minP;
			var topP = overrideSettings ? settings.TopP : _topP;
			var topK = overrideSettings ? settings.TopK : _topK;
			var flashAttn = overrideSettings ? settings.FlashAttn : _flashAttn;
			butGen.Enabled = butReset.Enabled = listViewModels.Enabled = butLoad.Enabled = butUnload.Enabled = false;
			ThreadPool.QueueUserWorkItem(o => {
				try{
					if(LlModelLoaded){
						Invoke(new MethodInvoker(UnloadModel));
						while(LlModelLoaded) Thread.Sleep(10);
					}
					Invoke(new MethodInvoker(() => {
						toolStripStatusLabel1.Text = Resources.Loading__ + fileName;
					}));
					unsafe{
						if(_generating){
							NativeMethods.StopGeneration();
							while(_generating) Thread.Sleep(10);
						}
						if(whisperOn) NativeMethods.StopSpeechTranscription();
						var result = LoadModel(modelPath, gpuLayers, _mMap, _mLock, _numaStrat);
						if(result != NativeMethods.StudError.Success){
							Settings.Default.LoadAuto = false;
							Settings.Default.Save();
							return;
						}
						_modelIndex = modelIndex;
						_modelCtxMax = GGUFMetadataManager.GetGGUFCtxMax(_models[modelIndex].Meta);
						if(_modelCtxMax <= 0) _cntCtxMax = ctxSize;
						else _cntCtxMax = ctxSize > _modelCtxMax ? _modelCtxMax : ctxSize;
						result = CreateSession(_cntCtxMax, _batchSize, flashAttn, _nThreads, _nThreadsBatch, minP, topP, topK, temp, _repPen);
						if(result != NativeMethods.StudError.Success){
							Settings.Default.LoadAuto = false;
							Settings.Default.Save();
							LlModelLoaded = true;
							Invoke(new MethodInvoker(UnloadModel));
							while(LlModelLoaded) Thread.Sleep(10);
							return;
						}
						_tokenCallback = TokenCallback;
						NativeMethods.SetTokenCallback(_tokenCallback);
						RegisterTools();
						SetSystemPrompt(systemPrompt);
						if(whisperOn) NativeMethods.StartSpeechTranscription();
						try{
							Invoke(new MethodInvoker(() => {
								Settings.Default.LastModel = modelKey;
								if(checkLoadAuto.Checked && !autoLoad){
									Settings.Default.LoadAuto = true;
									Settings.Default.Save();
								}
								toolTip1.SetToolTip(numCtxSize, Resources.ToolTip_numCtxSize + Resources.__The_last_model_loaded_has_a_maximum_context_size_of_ + _modelCtxMax);
								toolStripStatusLabel1.Text = Resources.Done_loading__ + fileName;
							}));
						} catch(ObjectDisposedException){}
						LlModelLoaded = true;
					}
				} finally{
					Invoke(new MethodInvoker(() => {
						butGen.Enabled = butReset.Enabled = listViewModels.Enabled = butLoad.Enabled = butUnload.Enabled = true;
						toolStripStatusLabel1.Text = Resources.No_model_loaded;
					}));
				}
			});
		}
		private void UnloadModel(){
			butGen.Enabled = butReset.Enabled = butUnload.Enabled = false;
			ThreadPool.QueueUserWorkItem(o => {
				GenerationLock.Wait(-1);
				try{
					if(_generating){
						NativeMethods.StopGeneration();
						while(_generating) Thread.Sleep(10);
					}
					ClearRegisteredTools();
					NativeMethods.FreeModel();
					try{
						BeginInvoke(new MethodInvoker(() => {
							toolTip1.SetToolTip(numCtxSize, Resources.ToolTip_numCtxSize);
							toolStripStatusLabel1.Text = Resources.Model_unloaded;
						}));
					} catch(ObjectDisposedException){}
					LlModelLoaded = false;
				} finally{ GenerationLock.Release(); }
			});
		}
		private string ConvertValueToString(GGUFMetadataManager.GGUFMetaValue metaVal){
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
		private void PopulateMeta(int index){
			listViewMeta.Items.Clear();
			foreach(var entry in _models[index].Meta){
				var key = entry.Key ?? "";
				var valueStr = ConvertValueToString(entry.Val);
				var lvi = new ListViewItem(key);
				lvi.SubItems.Add(valueStr);
				listViewMeta.Items.Add(lvi);
			}
		}
		internal IEnumerable<string> GetModelNames(){
			foreach(var m in _models) yield return Path.GetFileNameWithoutExtension(m.FilePath);
		}
	}
}