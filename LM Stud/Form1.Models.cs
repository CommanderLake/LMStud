using System;
using System.Collections.Generic;
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
				if(showError) MessageBox.Show(this, "Models folder not found, please specify a valid folder in the Settings tab", "LM Stud Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
				} catch(Exception ex){ Invoke(new MethodInvoker(() => {MessageBox.Show(this, ex.ToString(), "LM Stud Error", MessageBoxButtons.OK, MessageBoxIcon.Error);})); } finally{
					try{ Invoke(new MethodInvoker(() => {listViewModels.EndUpdate();})); } catch(Exception e){
						MessageBox.Show(this, e.ToString(), "LM Stud Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
					}
					_populating = false;
				}
			});
		}
		private void SetSystemPrompt(){
			if(!NativeMethods.SetSystemPrompt(_systemPrompt.Length > 0 ? _systemPrompt : DefaultPrompt, _googleSearchEnable && _webpageFetchEnable ? FetchPrompt : ""))
				MessageBox.Show(this, "Conversation too long for context while setting system prompt", "LM Stud", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
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
						toolStripStatusLabel1.Text = "Loading: " + fileName;
					}));
					unsafe{
						if(_generating){
							NativeMethods.StopGeneration();
							while(_generating) Thread.Sleep(10);
						}
						if(whisperOn) NativeMethods.StopSpeechTranscription();
						var result = NativeMethods.LoadModel(modelPath, _gpuLayers, _mMap, _mLock, _numaStrat);
						if(result < 0){
							Settings.Default.LoadAuto = false;
							Settings.Default.Save();
							return;
						}
						_modelIndex = modelIndex;
						_modelCtxMax = GGUFMetadataManager.GetGGUFCtxMax(_models[modelIndex].Meta);
						if(_modelCtxMax <= 0) _cntCtxMax = _ctxSize;
						else _cntCtxMax = _ctxSize > _modelCtxMax ? _modelCtxMax : _ctxSize;
						result = NativeMethods.CreateSession(_cntCtxMax, _batchSize, _flashAttn, _nThreads, _nThreadsBatch, _minP, _topP, _topK, _temp, _repPen);
						if(result < 0){
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
								toolStripStatusLabel1.Text = "Done loading: " + fileName;
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
						toolStripStatusLabel1.Text = "Model unloaded";
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