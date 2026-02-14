using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using LMStud.Properties;
namespace LMStud{
	public partial class Form1{
		private const string DefaultPrompt = "Assist the user to the best of your ability.";
		private const string FetchPrompt = "\nAfter calling the web_search tool you must subsequently call the get_webpage tool with a url followed by the get_webpage_text tool with the id of any relevant preview.";
		internal volatile bool LlModelLoaded;
		private string _lastModelPath = "";
		internal volatile bool Populating;
		private int _cntCtxMax;
		private int _modelCtxMax;
		private string _modelPath;
		private readonly List<string> _whisperModels = new List<string>();
		private void CheckLoadAuto_CheckedChanged(object sender, EventArgs e){
			if(checkLoadAuto.Checked && File.Exists(Settings.Default.LastModel)) Settings.Default.LoadAuto = true;
			else Settings.Default.LoadAuto = false;
			Settings.Default.Save();
		}
		private void ListViewModels_ItemSelectionChanged(object sender, ListViewItemSelectionChangedEventArgs e){
			if(listViewModels.SelectedItems.Count == 1){
				tabControlModelStuff.Enabled = true;
				var modelPath = listViewModels.SelectedItems[0].SubItems[1].Text;
				PopulateMeta((List<GGUFMetadataManager.GGUFMetadataEntry>)listViewModels.SelectedItems[0].Tag);
				PopulateModelSettings(modelPath);
			} else{
				listViewMeta.Items.Clear();
				tabControlModelStuff.Enabled = false;
			}
		}
		private void ListViewModels_KeyDown(object sender, KeyEventArgs e){
			if(e.KeyCode != Keys.F5) return;
			PopulateModels();
		}
		private void ListViewModels_DoubleClick(object sender, EventArgs e){
			ButLoad_Click(null, null);
		}
		private void ButLoad_Click(object sender, EventArgs e){
			if(listViewModels.SelectedItems.Count == 0) return;
			LoadModel(listViewModels.SelectedItems[0].SubItems[1].Text, false);
		}
		private void ButUnload_Click(object sender, EventArgs e){
			butUnload.Enabled = false;
			UnloadModel(true);
		}
		private bool ModelsFolderExists(bool showError){
			if(!Directory.Exists(_modelsDir)){
				if(showError) MessageBox.Show(this, Resources.Models_folder_not_found__please_specify_a_valid_folder_in_the_Settings_tab, Resources.LM_Stud, MessageBoxButtons.OK, MessageBoxIcon.Error);
				tabControl1.SelectTab(1);
				textModelsDir.Focus();
				return false;
			}
			return true;
		}
		private void PopulateModels(){
			if(Populating || !ModelsFolderExists(true) || !GenerationLock.Wait(0)) return;
			Populating = true;
			ThreadPool.QueueUserWorkItem(_ => {
				try{
					var files = Directory.GetFiles(_modelsDir, "*.gguf", SearchOption.AllDirectories);
					var items = new List<ListViewItem>();
					foreach(var file in files){
						var meta = GGUFMetadataManager.LoadGGUFMetadata(file);
						var lvi = new ListViewItem(Path.GetFileNameWithoutExtension(file));
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
					Populating = false;
					GenerationLock.Release();
				}
			});
		}
		[Localizable(true)]
		private void ShowError(string action, NativeMethods.StudError error){
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
		private void ShowError(string action, string error, bool addNativeMsg){
			void ShowMessage(){MessageBox.Show(this, string.Format(Resources._0____1_, action, error), Resources.LM_Stud, MessageBoxButtons.OK, MessageBoxIcon.Error);}
			if(InvokeRequired) Invoke(new MethodInvoker(ShowMessage));
			else ShowMessage();
		}
		private void SetSystemPromptInternal(bool genLock){
			if(genLock) GenerationLock.Wait(-1);
			try{
				string prompt;
				if(_modelPath != null && _modelSettings.TryGetValue(_modelPath.Substring(_modelsDir.Length), out var overrides) && overrides.OverrideSettings) prompt = overrides.SystemPrompt;
				else prompt = _systemPrompt;
				var error = NativeMethods.SetSystemPrompt(prompt.Length > 0 ? prompt : DefaultPrompt, _googleSearchEnable && _webpageFetchEnable ? FetchPrompt : "");
				if(error != NativeMethods.StudError.ModelNotLoaded && error != NativeMethods.StudError.Success) ShowError(Resources.Error_setting_system_prompt, error);
			} finally{
				if(genLock) GenerationLock.Release();
			}
		}
		private void SetSystemPrompt(bool genLock = true){
			if(!LlModelLoaded){
				SetSystemPromptInternal(genLock);
				return;
			}
			BeginRetokenization();
			try{ SetSystemPromptInternal(genLock); } finally{ EndRetokenization(); }
		}
		void SetModelStatus(){
			butGen.Enabled = butReset.Enabled = (_apiClientEnable || LlModelLoaded) && !Generating;
			if(_apiClientEnable) toolStripStatusLabel1.Text = Resources.Using_API_Model_ + _apiClientModel;
			else if(LlModelLoaded) toolStripStatusLabel1.Text = Resources.Using_Model_ + Path.GetFileName(_lastModelPath);
			else toolStripStatusLabel1.Text = Resources.No_model_loaded;
		}
		NativeMethods.StudError LoadModel(string filename, string jinjaTemplate, int nGPULayers, bool mMap, bool mLock, NativeMethods.GgmlNumaStrategy numaStrategy){
			var result = NativeMethods.LoadModel(filename, jinjaTemplate, nGPULayers, mMap, mLock, numaStrategy);
			if(result != NativeMethods.StudError.Success) ShowError(Resources.Error_loading_model, result);
			return result;
		}
		NativeMethods.StudError CreateSession(int nCtx, int nBatch, CheckState flashAttn, int nThreads, int nThreadsBatch, float minP, float topP, int topK, float temp, float repeatPenalty){
			var result = NativeMethods.CreateSession(nCtx, nBatch, (uint)flashAttn, nThreads, nThreadsBatch, minP, topP, topK, temp, repeatPenalty);
			if(result != NativeMethods.StudError.Success) ShowError(Resources.Error_creating_session, result);
			return result;
		}
		NativeMethods.StudError CreateContext(int nCtx, int nBatch, CheckState flashAttn, int nThreads, int nThreadsBatch){
			GenerationLock.Wait(-1);
			NativeMethods.StudError result;
			try{ result = NativeMethods.CreateContext(nCtx, nBatch, (uint)flashAttn, nThreads, nThreadsBatch); } finally{ GenerationLock.Release(); }
			if(result != NativeMethods.StudError.Success) ShowError(Resources.Error_creating_context, result);
			return result;
		}
		NativeMethods.StudError CreateSampler(float minP, float topP, int topK, float temp, float repeatPenalty){
			GenerationLock.Wait(-1);
			NativeMethods.StudError result;
			try{ result = NativeMethods.CreateSampler(minP, topP, topK, temp, repeatPenalty); } finally{ GenerationLock.Release(); }
			if(result != NativeMethods.StudError.Success) ShowError(Resources.Error_creating_sampler, result);
			return result;
		}
		NativeMethods.StudError LoadWhisperModel(string modelPath, int nThreads, bool useGPU, bool useVAD, string vadModel){
			var result = NativeMethods.LoadWhisperModel(modelPath, nThreads, useGPU, useVAD, vadModel);
			if(result != NativeMethods.StudError.Success) ShowError(Resources.Error_initialising_voice_input, result);
			return result;
		}
		private void LoadModel(string modelPath, bool autoLoad){
			if(!GenerationLock.Wait(1000)) return;
			var fileName = Path.GetFileName(modelPath);
			var meta = (List<GGUFMetadataManager.GGUFMetadataEntry>)listViewModels.SelectedItems[0].Tag;
			var overrideSettings = _modelSettings.TryGetValue(modelPath.Substring(_modelsDir.Length), out var overrides) && overrides.OverrideSettings;
			var ctxSize = overrideSettings ? overrides.CtxSize : _ctxSize;
			var gpuLayers = overrideSettings ? overrides.GPULayers : _gpuLayers;
			var temp = overrideSettings ? overrides.Temp : _temp;
			var minP = overrideSettings ? overrides.MinP : _minP;
			var topP = overrideSettings ? overrides.TopP : _topP;
			var topK = overrideSettings ? overrides.TopK : _topK;
			var flashAttn = overrideSettings ? overrides.FlashAttn : _flashAttn;
			var jinjaTmpl = overrides != null && overrides.OverrideJinja && File.Exists(overrides.JinjaTemplate) ? File.ReadAllText(overrides.JinjaTemplate) : null;
			butLoad.Enabled = butUnload.Enabled = false;
			ThreadPool.QueueUserWorkItem(o => {
				try{
					if(LlModelLoaded){
						Invoke(new MethodInvoker(() => UnloadModel(false)));
						while(LlModelLoaded) Thread.Sleep(10);
					}
					Invoke(new MethodInvoker(() => {
						toolStripStatusLabel1.Text = Resources.Loading__ + fileName;
					}));
					unsafe{
						var result = LoadModel(modelPath, jinjaTmpl, gpuLayers, _mMap, _mLock, _numaStrat);
						if(result != NativeMethods.StudError.Success){
							Settings.Default.LoadAuto = false;
							Settings.Default.Save();
							return;
						}
						_modelPath = modelPath;
						_modelCtxMax = GGUFMetadataManager.GetGGUFCtxMax(meta);
						if(_modelCtxMax <= 0) _cntCtxMax = ctxSize;
						else _cntCtxMax = ctxSize > _modelCtxMax ? _modelCtxMax : ctxSize;
						result = CreateSession(_cntCtxMax, _batchSize, flashAttn, _nThreads, _nThreadsBatch, minP, topP, topK, temp, _repPen);
						_lastModelPath = modelPath;
						LlModelLoaded = true;
						if(result != NativeMethods.StudError.Success){
							Settings.Default.LoadAuto = false;
							Settings.Default.Save();
							Invoke(new MethodInvoker(() => UnloadModel(false)));
							while(LlModelLoaded) Thread.Sleep(10);
							return;
						}
						_tokenCallback = TokenCallback;
						NativeMethods.SetTokenCallback(_tokenCallback);
						RegisterTools();
						SetSystemPrompt(false);
						try{
							Invoke(new MethodInvoker(() => {
								Settings.Default.LastModel = modelPath;
								if(checkLoadAuto.Checked && !autoLoad){
									Settings.Default.LoadAuto = true;
									Settings.Default.Save();
								}
								toolTip1.SetToolTip(numCtxSize, Resources.ToolTip_numCtxSize + "\r\n" + Resources.Max_context_size_of_last_loaded_model + _modelCtxMax);
							}));
						} catch(ObjectDisposedException){}
					}
				} finally{
					GenerationLock.Release();
					Invoke(new MethodInvoker(() => {
						SetModelStatus();
						butLoad.Enabled = butUnload.Enabled = true;
					}));
				}
			});
		}
		private void UnloadModel(bool genLock){
			butUnload.Enabled = false;
			ThreadPool.QueueUserWorkItem(o => {
				try{
					NativeMethods.StopGeneration();
					if(genLock) GenerationLock.Wait(-1);
					ClearRegisteredTools();
					NativeMethods.FreeModel();
					try{
						BeginInvoke(new MethodInvoker(() => {
							toolTip1.SetToolTip(numCtxSize, Resources.ToolTip_numCtxSize);
							SetModelStatus();
						}));
					} catch(ObjectDisposedException){}
				} finally{
					LlModelLoaded = false;
					if(genLock) GenerationLock.Release();
				}
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
		internal IEnumerable<string> GetModelNames(){return from ListViewItem m in listViewModels.Items select m.Text;}
	}
}