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
		private volatile bool _llModelLoaded;
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
		private void CheckLoadAuto_CheckedChanged(object sender, EventArgs e){
			if(checkLoadAuto.Checked && File.Exists(Settings.Default.LastModel)) Settings.Default.LoadAuto = true;
			else Settings.Default.LoadAuto = false;
			Settings.Default.Save();
		}
		private void ListViewModels_ItemSelectionChanged(object sender, ListViewItemSelectionChangedEventArgs e){
			if(listViewModels.SelectedItems.Count < 1) return;
			PopulateMeta(listViewModels.SelectedIndices[0]);
		}
		private void ListViewModels_KeyDown(object sender, KeyEventArgs e){
			if(e.KeyCode != Keys.F5) return;
			PopulateModels();
		}
		private void ButLoad_Click(object sender, EventArgs e){
			if(listViewModels.SelectedIndices.Count == 0) return;
			LoadModel(listViewModels.SelectedIndices[0], false);
		}
		private void ButUnload_Click(object sender, EventArgs e){
			butUnload.Enabled = false;
			UnloadModel();
		}
		private void ListViewModels_DoubleClick(object sender, EventArgs e){
			if(listViewModels.SelectedIndices.Count == 0) return;
			LoadModel(listViewModels.SelectedIndices[0], false);
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
						_models.Add(new ModelInfo(file, meta));
						var lvi = new ListViewItem(Path.GetFileName(file));
						lvi.SubItems.Add(file);
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
				default:
					detail = error.ToString();
					break;
			}
			if(InvokeRequired) Invoke(new MethodInvoker(() => {MessageBox.Show(this, string.Format(Resources._0____1_, action, detail), Resources.LM_Stud, MessageBoxButtons.OK, MessageBoxIcon.Error);}));
			else MessageBox.Show(this, string.Format(Resources._0____1_, action, detail), Resources.LM_Stud, MessageBoxButtons.OK, MessageBoxIcon.Error);
		}
		private void SetSystemPrompt(){
			var error = NativeMethods.SetSystemPrompt(_systemPrompt.Length > 0 ? _systemPrompt : DefaultPrompt, _googleSearchEnable && _webpageFetchEnable ? FetchPrompt : "");
			if(error != NativeMethods.StudError.ModelNotLoaded && error != NativeMethods.StudError.Success) ShowErrorMessage(Resources.Error_setting_system_prompt, error);
		}
		NativeMethods.StudError LoadModel(string filename, int nGPULayers, bool mMap, bool mLock, NativeMethods.GgmlNumaStrategy numaStrategy){
			var result = NativeMethods.LoadModel(filename, nGPULayers, mMap, mLock, numaStrategy);
			if(result != NativeMethods.StudError.Success) ShowErrorMessage(Resources.Error_loading_model, result);
			return result;
		}
		NativeMethods.StudError CreateSession(int nCtx, int nBatch, bool flashAttn, int nThreads, int nThreadsBatch, float minP, float topP, int topK, float temp, float repeatPenalty){
			var result = NativeMethods.CreateSession(nCtx, nBatch, flashAttn, nThreads, nThreadsBatch, minP, topP, topK, temp, repeatPenalty);
			if(result != NativeMethods.StudError.Success) ShowErrorMessage(Resources.Error_creating_session, result);
			return result;
		}
		NativeMethods.StudError CreateContext(int nCtx, int nBatch, bool flashAttn, int nThreads, int nThreadsBatch){
			var result = NativeMethods.CreateContext(nCtx, nBatch, flashAttn, nThreads, nThreadsBatch);
			if(result != NativeMethods.StudError.Success) ShowErrorMessage(Resources.Error_creating_context, result);
			return result;
		}
		NativeMethods.StudError CreateSampler(float minP, float topP, int topK, float temp, float repeatPenalty){
			var result = NativeMethods.CreateSampler(minP, topP, topK, temp, repeatPenalty);
			if(result != NativeMethods.StudError.Success) ShowErrorMessage(Resources.Error_creating_sampler, result);
			return result;
		}
		private void LoadModel(int modelIndex, bool autoLoad){
			var whisperOn = checkVoiceInput.CheckState != CheckState.Unchecked;
			var modelPath = _models[modelIndex].FilePath;
			var fileName = Path.GetFileName(modelPath);
			butGen.Enabled = butReset.Enabled = listViewModels.Enabled = butLoad.Enabled = butUnload.Enabled = false;
			ThreadPool.QueueUserWorkItem(o => {
				try{
					if(_llModelLoaded){
						Invoke(new MethodInvoker(UnloadModel));
						while(_llModelLoaded) Thread.Sleep(10);
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
						var result = LoadModel(modelPath, _gpuLayers, _mMap, _mLock, _numaStrat);
						if(result != NativeMethods.StudError.Success){
							Settings.Default.LoadAuto = false;
							Settings.Default.Save();
							return;
						}
						_modelIndex = modelIndex;
						_modelCtxMax = GGUFMetadataManager.GetGGUFCtxMax(_models[modelIndex].Meta);
						if(_modelCtxMax <= 0) _cntCtxMax = _ctxSize;
						else _cntCtxMax = _ctxSize > _modelCtxMax ? _modelCtxMax : _ctxSize;
						result = CreateSession(_cntCtxMax, _batchSize, _flashAttn, _nThreads, _nThreadsBatch, _minP, _topP, _topK, _temp, _repPen);
						if(result != NativeMethods.StudError.Success){
							Settings.Default.LoadAuto = false;
							Settings.Default.Save();
							_llModelLoaded = true;
							Invoke(new MethodInvoker(UnloadModel));
							while(_llModelLoaded) Thread.Sleep(10);
							return;
						}
						_tokenCallback = TokenCallback;
						NativeMethods.SetTokenCallback(_tokenCallback);
						RegisterTools();
						SetSystemPrompt();
						if(whisperOn) NativeMethods.StartSpeechTranscription();
						try{
							Invoke(new MethodInvoker(() => {
								Settings.Default.LastModel = modelPath;
								if(checkLoadAuto.Checked && !autoLoad){
									Settings.Default.LoadAuto = true;
									Settings.Default.Save();
								}
								toolTip1.SetToolTip(numCtxSize,
									"Context size (max tokens). Higher values improve memory but use more RAM.\r\nThe model last loaded has a maximum context size of " + _modelCtxMax);
								toolStripStatusLabel1.Text = Resources.Done_loading__ + fileName;
							}));
						} catch(ObjectDisposedException){}
						_llModelLoaded = true;
					}
				} finally{ Invoke(new MethodInvoker(() => {butGen.Enabled = butReset.Enabled = listViewModels.Enabled = butLoad.Enabled = butUnload.Enabled = true;})); }
			});
		}
		private void UnloadModel(){
			butGen.Enabled = butReset.Enabled = butUnload.Enabled = false;
			ThreadPool.QueueUserWorkItem(o => {
				if(_generating) {
					NativeMethods.StopGeneration();
					while(_generating) Thread.Sleep(10);
				}
				ClearRegisteredTools();
				NativeMethods.FreeModel();
				try{
					BeginInvoke(new MethodInvoker(() => {
						toolTip1.SetToolTip(numCtxSize, "Context size (max tokens). Higher values improve memory but use more RAM.");
						toolStripStatusLabel1.Text = Resources.Model_unloaded;
					}));
				} catch(ObjectDisposedException){}
				_llModelLoaded = false;
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
	}
}