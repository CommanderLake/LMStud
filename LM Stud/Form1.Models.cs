using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using LMStud.Properties;
namespace LMStud{
	internal partial class Form1{
		private volatile bool _loaded;
		private volatile bool _populating;
		private int _cntCtxMax;
		private struct ModelInfo{
			public readonly string FilePath;
			public readonly List<GGUFMetadataManager.GGUFMetadataEntry> Meta;
			public ModelInfo(string filePath, List<GGUFMetadataManager.GGUFMetadataEntry> meta){
				FilePath = filePath;
				Meta = meta;
			}
		}
		private readonly List<ModelInfo> _models = new List<ModelInfo>();
		private void CheckLoadAuto_CheckedChanged(object sender, EventArgs e){
			if(checkLoadAuto.Checked && File.Exists(Settings.Default.LastModel)){
				Settings.Default.LoadAuto = true;
				Settings.Default.Save();
			} else{
				Settings.Default.LoadAuto = false;
				Settings.Default.Save();
			}
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
		private void PopulateModels(){
			if(_populating) return;
			if(!Directory.Exists(_modelsPath)) { 
				MessageBox.Show(this, "Models folder not found", "LM Stud Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
				return;
			}
			_populating = true;
			listViewModels.BeginUpdate();
			listViewModels.Items.Clear();
			for(var i = _models.Count - 1; i >= 0; i--) _models[i].Meta.Clear();
			_models.Clear();
			ThreadPool.QueueUserWorkItem(_ => {
				try {
					var files = Directory.GetFiles(_modelsPath, "*.gguf", SearchOption.AllDirectories);
					foreach(var file in files) {
						var meta = GGUFMetadataManager.LoadGGUFMetadata(file);
						_models.Add(new ModelInfo(file, meta));
						Invoke(new MethodInvoker(() => {
							var lvi = new ListViewItem(Path.GetFileName(file));
							lvi.SubItems.Add(file);
							listViewModels.Items.Add(lvi);
						}));
					}
				} catch(Exception ex) { Invoke(new MethodInvoker(() => {MessageBox.Show(this, ex.ToString(), "LM Stud Error", MessageBoxButtons.OK, MessageBoxIcon.Error);})); } finally {
					Invoke(new MethodInvoker(() => {
						listViewModels.EndUpdate();
					}));
					_populating = false;
				}
			});
		}
		private void LoadModel(int modelIndex, bool autoLoad){
			butGen.Enabled = butReset.Enabled = listViewModels.Enabled = butLoad.Enabled = butUnload.Enabled = false;
			var modelPath = _models[modelIndex].FilePath;
			var fileName = Path.GetFileName(modelPath);
			toolStripStatusLabel1.Text = "Loading: " + fileName;
			ThreadPool.QueueUserWorkItem(o => {
				unsafe{
					if(_generating){
						NativeMethods.StopGeneration();
						while(_generating) Thread.Sleep(10);
					}
					var modelCtxMax = GGUFMetadataManager.GetGGUFCtxMax(_models[modelIndex].Meta);
					_cntCtxMax = _ctxSize > modelCtxMax ? modelCtxMax : _ctxSize;
					var success = NativeMethods.LoadModel(modelPath, _instruction, _cntCtxMax, _temp, _repPen, _topK, _topP, _nThreads, _strictCPU, _nThreadsBatch, _strictCPUBatch, _gpuLayers, _batchSize, _mMap, _mLock, _numaStrat);
					if(!success){
						Settings.Default.LoadAuto = false;
						Settings.Default.Save();
						Invoke(new MethodInvoker(() => {MessageBox.Show(this, "Error loading model", "LM Stud Error", MessageBoxButtons.OK, MessageBoxIcon.Error);}));
						return;
					}
					_loaded = true;
					_tokenCallback = TokenCallback;
					NativeMethods.SetTokenCallback(_tokenCallback);
					Invoke(new MethodInvoker(() => {
						Settings.Default.LastModel = modelPath;
						if(checkLoadAuto.Checked && !autoLoad){
							Settings.Default.LoadAuto = true;
							Settings.Default.Save();
						}
						toolStripStatusLabel1.Text = "Loaded model: " + fileName;
						butGen.Enabled = butReset.Enabled = listViewModels.Enabled = butLoad.Enabled = butUnload.Enabled = true;
					}));
				}
			});
		}
		private void UnloadModel(){
			butGen.Enabled = butReset.Enabled = butUnload.Enabled = false;
			ThreadPool.QueueUserWorkItem(o => {
				if(_generating) {
					NativeMethods.StopGeneration();
					while(_generating) Thread.Sleep(10);
				}
				NativeMethods.FreeModel();
				_loaded = false;
				Invoke(new MethodInvoker(() => {
					toolStripStatusLabel1.Text = "Model unloaded";
				}));
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