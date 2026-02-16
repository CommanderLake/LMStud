using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using LMStud.Properties;
using Newtonsoft.Json;
namespace LMStud{
	public partial class Form1{
		private const string DefaultPrompt = "Assist the user to the best of your ability.";
		private const string FetchPrompt = "\nAfter calling the web_search tool you must subsequently call the get_webpage tool with a url followed by the get_webpage_text tool with the id of any relevant preview.";
		private volatile SemaphoreSlim _populateLock = new SemaphoreSlim(1, 1);
		private void CheckLoadAuto_CheckedChanged(object sender, EventArgs e){
			if(checkLoadAuto.Checked && File.Exists(Common.ModelsDir + Settings.Default.LastModel)) Settings.Default.LoadAuto = true;
			else Settings.Default.LoadAuto = false;
			Settings.Default.Save();
		}
		private void ListViewModels_ItemSelectionChanged(object sender, ListViewItemSelectionChangedEventArgs e){
			if(listViewModels.SelectedItems.Count == 1){
				tabControlModelStuff.Enabled = true;
				PopulateMeta((List<GGUFMetadataManager.GGUFMetadataEntry>)listViewModels.SelectedItems[0].Tag);
				PopulateModelSettings(listViewModels.SelectedItems[0].SubItems[1].Text);
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
			LoadModel(listViewModels.SelectedItems[0], false);
		}
		private void ButUnload_Click(object sender, EventArgs e){
			butUnload.Enabled = false;
			UnloadModel(true);
		}
		private bool ModelsFolderExists(bool showError){
			if(!Directory.Exists(Common.ModelsDir)){
				if(showError) MessageBox.Show(this, Resources.Models_folder_not_found_, Resources.LM_Stud, MessageBoxButtons.OK, MessageBoxIcon.Error);
				tabControl1.SelectTab(1);
				textModelsDir.Focus();
				return false;
			}
			return true;
		}
		private void PopulateModels(){
			if(!_populateLock.Wait(0) || !ModelsFolderExists(true)) return;
			ThreadPool.QueueUserWorkItem(_ => {
				try{
					if(!GenerationLock.Wait(-1)) return;
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
					_populateLock.Release();
					GenerationLock.Release();
				}
			});
		}
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
			if(addNativeMsg){
				var extra = NativeMethods.GetLastError();
				NativeMethods.ClearLastErrorMessage();
				if(!string.IsNullOrWhiteSpace(extra)) error += "\r\n\r\n" + extra;
			}
			void ShowMessage(){MessageBox.Show(this, string.Format(Resources._0____1_, action, error), Resources.LM_Stud, MessageBoxButtons.OK, MessageBoxIcon.Error);}
			if(InvokeRequired) Invoke(new MethodInvoker(ShowMessage));
			else ShowMessage();
		}
		private static Exception GetInnermostException(Exception exception){
			var current = exception;
			while(current?.InnerException != null) current = current.InnerException;
			return current;
		}
		private static string GetApiClientFriendlyError(Exception exception){
			if(exception == null) return Resources.Unknown_API_error_;
			var root = GetInnermostException(exception);
			switch(root){
				case SocketException _:
				case HttpRequestException _: return Resources.Unable_to_connect_to_the_API_server;
				case TaskCanceledException _:
				case TimeoutException _: return Resources.The_API_request_timed_out;
				case OperationCanceledException _: return Resources.The_API_request_was_canceled_before_completion;
				case JsonException _: return Resources.Received_an_invalid_response_from_the_API_server;
				case InvalidOperationException _:{
					var message = root.Message ?? "";
					if(message.IndexOf("401", StringComparison.OrdinalIgnoreCase) >= 0 || message.IndexOf("403", StringComparison.OrdinalIgnoreCase) >= 0) return Resources.Authentication_failed;
					if(message.IndexOf("404", StringComparison.OrdinalIgnoreCase) >= 0) return Resources.API_endpoint_not_found;
					if(message.IndexOf("429", StringComparison.OrdinalIgnoreCase) >= 0) return Resources.Rate_limit_exceeded;
					if(message.IndexOf("500", StringComparison.OrdinalIgnoreCase) >= 0 || message.IndexOf("502", StringComparison.OrdinalIgnoreCase) >= 0 || message.IndexOf("503", StringComparison.OrdinalIgnoreCase) >= 0) return Resources.The_API_server_encountered_an_error;
					break;
				}
			}
			return root.Message;
		}
		private void ShowApiClientError(string action, Exception exception){
			var detail = GetApiClientFriendlyError(exception);
			var technical = exception?.Message;
			if(!string.IsNullOrWhiteSpace(technical) && !string.Equals(technical, detail, StringComparison.Ordinal)) detail += "\r\n\r\n" + technical;
			ShowError(action, detail, false);
		}
		private void SetSystemPromptInternal(bool genLock){
			if(genLock) GenerationLock.Wait(-1);
			try{
				string prompt;
				if(Common.LoadedModel != null && _modelSettings.TryGetValue(Common.LoadedModel.SubItems[1].Text.Substring(Common.ModelsDir.Length), out var overrides) && overrides.OverrideSettings) prompt = overrides.SystemPrompt;
				else prompt = Common.SystemPrompt;
				var error = NativeMethods.SetSystemPrompt(prompt.Length > 0 ? prompt : DefaultPrompt, Common.GoogleSearchEnable && Common.WebpageFetchEnable ? FetchPrompt : "");
				if(error != NativeMethods.StudError.ModelNotLoaded && error != NativeMethods.StudError.Success) ShowError(Resources.Error_setting_system_prompt, error);
			} finally{
				if(genLock) GenerationLock.Release();
			}
		}
		private void SetSystemPrompt(bool genLock = true){
			if(!Common.LlModelLoaded){
				SetSystemPromptInternal(genLock);
				return;
			}
			BeginRetokenization();
			try{ SetSystemPromptInternal(genLock); } finally{ EndRetokenization(); }
		}
		void SetModelStatus(){
			butGen.Enabled = butReset.Enabled = (Common.APIClientEnable || Common.LlModelLoaded) && !Generating;
			if(Common.APIClientEnable) toolStripStatusLabel1.Text = Resources.Using_API_Model_ + Common.APIClientModel;
			else if(Common.LlModelLoaded) toolStripStatusLabel1.Text = Resources.Using_Model_ + Common.LoadedModel.Text;
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
		internal void LoadModel(ListViewItem modelLvi, bool autoLoad){
			var modelPath = modelLvi.SubItems[1].Text;
			if(!File.Exists(modelPath)){
				ShowError("LoadModel", "Model file not found\r\n\r\n" + modelPath, false);
				checkLoadAuto.Checked = false;
				return;
			}
			butLoad.Enabled = butUnload.Enabled = false;
			var modelsDir = Common.ModelsDir;
			var fileName = Path.GetFileName(modelPath);
			var meta = (List<GGUFMetadataManager.GGUFMetadataEntry>)modelLvi.Tag;
			ThreadPool.QueueUserWorkItem(o => {
				var overrideSettings = _modelSettings.TryGetValue(modelPath.Substring(modelsDir.Length), out var overrides) && overrides.OverrideSettings;
				var ctxSize = overrideSettings ? overrides.CtxSize : Common.CtxSize;
				var gpuLayers = overrideSettings ? overrides.GPULayers : Common.GPULayers;
				var temp = overrideSettings ? overrides.Temp : Common.Temp;
				var minP = overrideSettings ? overrides.MinP : Common.MinP;
				var topP = overrideSettings ? overrides.TopP : Common.TopP;
				var topK = overrideSettings ? overrides.TopK : Common.TopK;
				var flashAttn = overrideSettings ? overrides.FlashAttn : Common.FlashAttn;
				var jinjaTmpl = overrides != null && overrides.OverrideJinja && File.Exists(overrides.JinjaTemplate) ? File.ReadAllText(overrides.JinjaTemplate) : null;
				try{
					if(!GenerationLock.Wait(-1)) return;
					if(Common.LlModelLoaded) UnloadModelInternal(false);
					Invoke(new MethodInvoker(() => {toolStripStatusLabel1.Text = Resources.Loading__ + fileName;}));
					unsafe{
						var result = LoadModel(modelPath, jinjaTmpl, gpuLayers, Common.MMap, Common.MLock, Common.NumaStrat);
						if(result != NativeMethods.StudError.Success){
							Settings.Default.LoadAuto = false;
							Settings.Default.Save();
							return;
						}
						Common.LoadedModel = modelLvi;
						Common.ModelCtxMax = GGUFMetadataManager.GetGGUFCtxMax(meta);
						if(Common.ModelCtxMax <= 0) Common.CntCtxMax = ctxSize;
						else Common.CntCtxMax = ctxSize > Common.ModelCtxMax ? Common.ModelCtxMax : ctxSize;
						result = CreateSession(Common.CntCtxMax, Common.BatchSize, flashAttn, Common.NThreads, Common.NThreadsBatch, minP, topP, topK, temp, Common.RepPen);
						Common.LlModelLoaded = true;
						if(result != NativeMethods.StudError.Success){
							Settings.Default.LoadAuto = false;
							Settings.Default.Save();
							Invoke(new MethodInvoker(() => UnloadModel(false)));
							while(Common.LlModelLoaded) Thread.Sleep(10);
							return;
						}
						_tokenCallback = TokenCallback;
						NativeMethods.SetTokenCallback(_tokenCallback);
						RegisterTools();
						SetSystemPrompt(false);
						try{
							BeginInvoke(new MethodInvoker(() => {
								Settings.Default.LastModel = modelPath.Substring(modelsDir.Length);
								if(checkLoadAuto.Checked && !autoLoad){
									Settings.Default.LoadAuto = true;
									Settings.Default.Save();
								}
								toolTip1.SetToolTip(numCtxSize, Resources.ToolTip_numCtxSize + "\r\n" + Resources.Max_context_size_of_last_loaded_model + Common.ModelCtxMax);
							}));
						} catch(ObjectDisposedException){}
					}
				} finally{
					GenerationLock.Release();
					BeginInvoke(new MethodInvoker(() => {
						SetModelStatus();
						butLoad.Enabled = butUnload.Enabled = true;
					}));
				}
			});
		}
		private void UnloadModelInternal(bool genLock){
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
				Common.LoadedModel = null;
				Common.LlModelLoaded = false;
				if(genLock) GenerationLock.Release();
			}
		}
		private void UnloadModel(bool genLock){
			butUnload.Enabled = false;
			ThreadPool.QueueUserWorkItem(o => UnloadModelInternal(genLock));
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
	}
}