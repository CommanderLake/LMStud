using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using LMStud.Properties;
namespace LMStud{
	public partial class Form1{
		private const string Filter = "gguf";
		private readonly string _huggingFaceUrl;
		private volatile bool _cancelDownload;
		private bool _downloading;
		private int _hugFileRequest;
		private string _hugRevision;
		private string _modelName;
		private string _uploader;
		private void ListViewHugFiles_MouseDown(object sender, MouseEventArgs args){
			if(args.Button != MouseButtons.Right) return;
			var item = listViewHugFiles.GetItemAt(args.X, args.Y);
			if(item == null){
				listViewHugFiles.SelectedItems.Clear();
				return;
			}
			item.Selected = true;
			item.Focused = true;
		}
		private void ContextHugFiles_Opening(object sender, CancelEventArgs e){e.Cancel = !CanCopyUrl();}
		private void CopyURLToolStripMenuItem_Click(object sender, EventArgs e){
			if(!CanCopyUrl()) return;
			var fileName = listViewHugFiles.SelectedItems[0].SubItems[0].Text;
			try{ Clipboard.SetText(BuildHugDownloadUrl(_uploader, _modelName, fileName)); } catch(ExternalException){}
		}
		private bool CanCopyUrl(){return listViewHugFiles.SelectedItems.Count > 0 && !string.IsNullOrEmpty(_uploader) && !string.IsNullOrEmpty(_modelName);}
		private string BuildHugDownloadUrl(string uploader, string modelName, string fileName, string revision = null){
			var pathParts = fileName.Replace('\\', '/').Split('/');
			for(var i = 0; i < pathParts.Length; i++) pathParts[i] = Uri.EscapeDataString(pathParts[i]);
			var branch = Uri.EscapeDataString(string.IsNullOrEmpty(revision) ? "main" : revision);
			return $"{_huggingFaceUrl}/{Uri.EscapeDataString(uploader)}/{Uri.EscapeDataString(modelName)}/resolve/{branch}/{string.Join("/", pathParts)}";
		}
		private void TextSearchTerm_KeyDown(object sender, KeyEventArgs e){
			if(e.KeyCode != Keys.Enter) return;
			HugSearch(textSearchTerm.Text);
		}
		private void ButSearch_Click(object sender, EventArgs e){HugSearch(textSearchTerm.Text);}
		private void ButDownload_Click(object sender, EventArgs e){
			if(_downloading){
				_cancelDownload = true;
				butDownload.Enabled = false;
				return;
			}
			if(!CanCopyUrl()){
				MessageBox.Show(Resources.Please_select_an_item_from_both_lists_, Resources.LM_Stud, MessageBoxButtons.OK, MessageBoxIcon.Warning);
				return;
			}
			HugDownloadFiles(_uploader, _modelName, _hugRevision, (string[])listViewHugFiles.SelectedItems[0].Tag);
		}
		private void HugSearch(string term){
			if(string.IsNullOrWhiteSpace(term)) return;
			butSearch.Enabled = textSearchTerm.Enabled = false;
			ThreadPool.QueueUserWorkItem(o => {
				try{
					var escapedTerm = Uri.EscapeDataString(term);
					var url = $"{_huggingFaceUrl}/api/models?&filter={Filter}&search={escapedTerm}";
					var json = NativeMethods.ReadUtf8AndFreeOptional(NativeMethods.PerformHttpGet(url));
					if(json == null) throw new ArgumentNullException();
					if(json.StartsWith("Error:") || json.StartsWith("{\"error\":")) throw new Exception(json);
					var models = Json.Parse(json);
					Invoke(new MethodInvoker(() => {
						listViewHugSearch.BeginUpdate();
						listViewHugSearch.Items.Clear();
						listViewHugFiles.Items.Clear();
					}));
					var items = new List<ListViewItem>();
					foreach(var modelToken in models){
						if(!modelToken.IsObject) continue;
						var id = modelToken.GetString("id");
						if(id == null) continue;
						var parts = id.Split('/');
						var uploader = parts.Length > 1 ? parts[0] : "";
						var modelName = parts.Length > 1 ? parts[1] : id;
						items.Add(new ListViewItem(new[]{
							modelName, uploader, modelToken.GetString("likes"), modelToken.GetString("downloads"), modelToken.GetString("trendingScore"), modelToken.GetString("createdAt")
						}));
					}
					if(items.Count > 0) Invoke(new MethodInvoker(() => {listViewHugSearch.Items.AddRange(items.ToArray());}));
				} catch(Exception ex){
					Invoke(new MethodInvoker(
						() => {MessageBox.Show(this, string.Format(Resources.Model_search_error___0_, ex.Message), Resources.LM_Stud, MessageBoxButtons.OK, MessageBoxIcon.Error);}));
				} finally{
					Invoke(new MethodInvoker(() => {
						listViewHugSearch.EndUpdate();
						butSearch.Enabled = textSearchTerm.Enabled = true;
					}));
				}
			});
		}
		private void ListViewHugSearch_SelectedIndexChanged(object sender, EventArgs e){
			if(listViewHugSearch.SelectedItems.Count == 0){
				++_hugFileRequest;
				_uploader = _modelName = _hugRevision = null;
				listViewHugFiles.Items.Clear();
				return;
			}
			var selectedItem = listViewHugSearch.SelectedItems[0];
			HugLoadFiles(selectedItem.SubItems[1].Text, selectedItem.SubItems[0].Text);
		}
		private void HugLoadFiles(string uploader, string modelName, string fileExtension = ".gguf"){
			if(string.IsNullOrEmpty(uploader) || string.IsNullOrEmpty(modelName)) return;
			var request = ++_hugFileRequest;
			var repoId = $"{uploader}/{modelName}";
			_uploader = uploader;
			_modelName = modelName;
			_hugRevision = null;
			listViewHugFiles.Items.Clear();
			ThreadPool.QueueUserWorkItem(_ => {
				ListViewItem[] items = null;
				string revision = null;
				string error = null;
				try{
					var infoUrl = $"{_huggingFaceUrl}/api/models/{repoId}?blobs=true";
					var infoJson = NativeMethods.ReadUtf8AndFreeOptional(NativeMethods.PerformHttpGet(infoUrl));
					if(infoJson == null) throw new ArgumentNullException();
					if(infoJson.StartsWith("Error:")) throw new Exception(infoJson);
					var info = Json.Parse(infoJson);
					revision = info.GetString("sha");
					items = BuildHugModelFileItems(info["siblings"], fileExtension).ToArray();
				} catch(Exception ex){ error = string.Format(Resources.Error_loading_files_for_ + Resources._0____1_, repoId, ex.Message); }
				try{
					BeginInvoke(new MethodInvoker(() => {
						if(request != _hugFileRequest) return;
						if(error != null){
							MessageBox.Show(this, error, Resources.LM_Stud, MessageBoxButtons.OK, MessageBoxIcon.Error);
							return;
						}
						_hugRevision = revision;
						listViewHugFiles.BeginUpdate();
						try{
							listViewHugFiles.Items.Clear();
							listViewHugFiles.Items.AddRange(items);
						} finally{ listViewHugFiles.EndUpdate(); }
					}));
				} catch(ObjectDisposedException){} catch(InvalidOperationException){}
			});
		}
		internal static IEnumerable<ListViewItem> BuildHugModelFileItems(JsonNode siblings, string fileExtension = ".gguf"){
			var sizes = new Dictionary<string, long?>(StringComparer.Ordinal);
			foreach(var file in siblings){
				if(!file.IsObject) continue;
				var name = file.GetString("rfilename") ?? file.GetString("filename");
				if(name == null || !name.EndsWith(fileExtension, StringComparison.OrdinalIgnoreCase)) continue;
				sizes[name] = file.GetLong("size");
			}
			foreach(var file in sizes.Keys){
				var split = GGUFSplit.Parse(file);
				if(split != null && (split.Number != 1 || !split.Paths().All(sizes.ContainsKey))) continue;
				var parts = split?.Paths().ToArray() ?? new[]{ file };
				var partSizes = parts.Select(part => sizes[part]).ToArray();
				var size = partSizes.All(part => part.HasValue) ? $"{partSizes.Sum(part => part.Value/1048576.0):F2} MB" : "";
				yield return new ListViewItem(new[]{ file, size }){ Tag = parts };
			}
		}
		private void HugDownloadFiles(string uploader, string modelName, string revision, string[] files){
			var targetDir = Path.Combine(Common.ModelsDir, uploader, modelName);
			_cancelDownload = false;
			_downloading = true;
			butDownload.Text = Resources.Cancel;
			progressBar1.Value = 0;
			progressBar1.Maximum = 1000;
			var lastProgress = -1;
			bool ReportProgress(double fraction){
				if(_cancelDownload || IsDisposed || Disposing) return false;
				var progress = (int)(fraction*1000);
				if(progress != lastProgress){
					try{ Invoke(new MethodInvoker(() => {progressBar1.Value = progress;})); } catch(ObjectDisposedException){ return false; } catch(InvalidOperationException){ return false; }
					lastProgress = progress;
				}
				return !_cancelDownload;
			}
			ThreadPool.QueueUserWorkItem(_ => {
				string targetPath = null;
				string error = null;
				try{ targetPath = HuggingFaceDownload.Download(targetDir, files, file => BuildHugDownloadUrl(uploader, modelName, file, revision), ReportProgress); } catch(Exception ex){
					error = ex.Message;
				}
				try{
					BeginInvoke(new MethodInvoker(() => {
						progressBar1.Value = 0;
						butDownload.Text = Resources.Download;
						butDownload.Enabled = true;
						_downloading = false;
						if(targetPath != null){
							var message = files.Length == 1 ? string.Format(Resources.Downloaded__0__to___1_, files[0], targetPath)
								: string.Format(Resources.Downloaded_GGUF_Parts, files.Length, Path.GetDirectoryName(targetPath));
							MessageBox.Show(this, message, Resources.LM_Stud, MessageBoxButtons.OK, MessageBoxIcon.Information);
							PopulateModels();
						} else if(!_cancelDownload){ MessageBox.Show(this, string.Format(Resources.Download_failed___0_, error), Resources.LM_Stud, MessageBoxButtons.OK, MessageBoxIcon.Error); }
					}));
				} catch(ObjectDisposedException){} catch(InvalidOperationException){}
			});
		}
	}
}