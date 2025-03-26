using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using LMStud.Properties;
namespace LMStud{
	internal partial class Form1{
		private struct ModelInfo{
			public readonly string FilePath;
			public readonly List<GGUFMetadataManager.GGUFMetadataEntry> Meta;
			public ModelInfo(string filePath, List<GGUFMetadataManager.GGUFMetadataEntry> meta){
				FilePath = filePath;
				Meta = meta;
			}
		}
		private readonly List<ModelInfo> _models = new List<ModelInfo>();
		private int _cntCtxMax;
		private void CheckLoadAuto_CheckedChanged(object sender, EventArgs e){
			if(checkLoadAuto.Checked && File.Exists(Settings.Default.LastModel)){
				Settings.Default.LoadAuto = true;
				Settings.Default.Save();
			} else{
				Settings.Default.LoadAuto = false;
				Settings.Default.Save();
			}
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
			ThreadPool.QueueUserWorkItem(o => {
				if(_generating){
					NativeMethods.StopGeneration();
					while(_generating) Thread.Sleep(10);
				}
				NativeMethods.FreeModel();
				Invoke(new MethodInvoker(() => {
					butUnload.Enabled = false;
					toolStripStatusLabel1.Text = "Unloaded model";
				}));
			});
		}
		private void ListViewModels_DoubleClick(object sender, EventArgs e){
			if(listViewModels.SelectedIndices.Count == 0) return;
			LoadModel(listViewModels.SelectedIndices[0], false);
		}
		private void PopulateModels(){
			var modelsPath = textModelsPath.Text;
			if(Directory.Exists(modelsPath)){
				listViewModels.Items.Clear();
				for(var i = _models.Count - 1; i >= 0; i--) _models[i].Meta.Clear();
				_models.Clear();
				try{
					var files = Directory.GetFiles(modelsPath, "*.gguf", SearchOption.AllDirectories);
					foreach(var file in files){
						var meta = GGUFMetadataManager.LoadGGUFMetadata(file);
						_models.Add(new ModelInfo(file, meta));
						var lvi = new ListViewItem(Path.GetFileName(file));
						lvi.SubItems.Add(file);
						listViewModels.Items.Add(lvi);
					}
				} catch(Exception ex){ MessageBox.Show(this, ex.ToString(), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
			} else{ MessageBox.Show(this, "Models folder does not exist.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
		}
		private void LoadModel(int modelIndex, bool autoLoad){
			butGen.Enabled = butReset.Enabled = listViewModels.Enabled = butLoad.Enabled = butUnload.Enabled = false;
			toolStripStatusLabel1.Text = "Loading model...";
			ThreadPool.QueueUserWorkItem(o => {
				if(_generating){
					NativeMethods.StopGeneration();
					while(_generating) Thread.Sleep(10);
				}
				var modelPath = _models[modelIndex].FilePath;
				var modelCtxMax = GGUFMetadataManager.GetGGUFCtxMax(_models[modelIndex].Meta);
				_cntCtxMax = _ctxSize > modelCtxMax ? modelCtxMax : _ctxSize;
				var success = NativeMethods.LoadModel(modelPath, _instruction, _cntCtxMax, _temp, _repPen, _topK, _topP, _nThreads, _strictCPU, _gpuLayers, _batchSize, _numaStrat);
				if(!success){
					Settings.Default.LoadAuto = false;
					Settings.Default.Save();
					Invoke(new MethodInvoker(() => {MessageBox.Show(this, "Error loading model", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);}));
					return;
				}
				_tokenCallback = TokenCallback;
				NativeMethods.SetTokenCallback(_tokenCallback);
				Invoke(new MethodInvoker(() => {
					Settings.Default.LastModel = modelPath;
					if(checkLoadAuto.Checked && !autoLoad){
						Settings.Default.LoadAuto = true;
						Settings.Default.Save();
					}
					toolStripStatusLabel1.Text = "Loaded model: " + Path.GetFileName(modelPath);
					butGen.Enabled = butReset.Enabled = listViewModels.Enabled = butLoad.Enabled = butUnload.Enabled = true;
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
		private void ListViewModels_ItemSelectionChanged(object sender, ListViewItemSelectionChangedEventArgs e){
			if(listViewModels.SelectedItems.Count < 1) return;
			PopulateMeta(listViewModels.SelectedIndices[0]);
		}
	}
}