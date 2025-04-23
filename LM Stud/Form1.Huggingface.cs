using System;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
namespace LMStud{
	internal partial class Form1{
		private const string ApiUrl = "https://huggingface.co/api/models";
		private const string PipelineTag = "text-generation";
		private const string Filter = "gguf";
		private const int Limit = 50;
		private volatile bool _downloading;
		private string _modelName;
		private string _uploader;
		private void TextSearchTerm_KeyDown(object sender, KeyEventArgs e){
			if(e.KeyCode != Keys.Enter) return;
			HugSearch(textSearchTerm.Text);
		}
		private void ButSearch_Click(object sender, EventArgs e){HugSearch(textSearchTerm.Text);}
		private void ButDownload_Click(object sender, EventArgs e){
			if(!_downloading){
				if(listViewHugFiles.SelectedItems.Count == 0 || string.IsNullOrEmpty(_uploader) || string.IsNullOrEmpty(_modelName)){
					MessageBox.Show("Please select an item from both lists.", "Selection Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
					return;
				}
				var variantLabel = listViewHugFiles.SelectedItems[0].SubItems[0].Text;
				butDownload.Text = "Cancel";
				HugDownloadFile(_uploader, _modelName, variantLabel);
			} else _downloading = false;
		}
		private void HugSearch(string term){
			if(string.IsNullOrWhiteSpace(term)) return;
			butSearch.Enabled = textSearchTerm.Enabled = false;
			ThreadPool.QueueUserWorkItem(o => {
				try{
					var escapedTerm = Uri.EscapeDataString(term);
					var url = $"{ApiUrl}?&filter={Filter}&search={escapedTerm}";
					var resPtr = NativeMethods.PerformHttpGet(url);
					var json = Marshal.PtrToStringAnsi(resPtr);
					NativeMethods.FreeMemory(resPtr);
					if(json == null) throw new ArgumentNullException();
					if(json.StartsWith("Error:") || json.StartsWith("{\"error\":")) throw new Exception(json);
					var models = JArray.Parse(json);
					Invoke(new MethodInvoker(() => {
						listViewHugSearch.BeginUpdate();
						listViewHugSearch.Items.Clear();
						listViewHugFiles.Items.Clear();
					}));
					foreach(var modelToken in models){
						var model = (JObject)modelToken;
						var hfModel = model.ToObject<HugModel>();
						if(hfModel?.ID == null) continue;
						var parts = hfModel.ID.Split('/');
						var uploader = parts.Length > 1 ? parts[0] : "";
						var modelName = parts.Length > 1 ? parts[1] : hfModel.ID;
						Invoke(new MethodInvoker(() => {
							listViewHugSearch.Items.Add(
								new ListViewItem(new[]{ modelName, uploader, hfModel.Likes, hfModel.Downloads, hfModel.TrendingScore, hfModel.CreatedAt, hfModel.LastModified }));
						}));
					}
				} catch(Exception ex){
					Invoke(new MethodInvoker(() => {MessageBox.Show(this, $"Model search error: {ex.Message}", "LM Stud Error", MessageBoxButtons.OK, MessageBoxIcon.Error);}));
				} finally{
					Invoke(new MethodInvoker(() => {
						listViewHugSearch.EndUpdate();
						butSearch.Enabled = textSearchTerm.Enabled = true;
					}));
				}
			});
		}
		private void ListViewHugSearch_SelectedIndexChanged(object sender, EventArgs e){
			if(listViewHugSearch.SelectedItems.Count == 0) return;
			var selectedItem = listViewHugSearch.SelectedItems[0];
			HugLoadFiles(selectedItem.SubItems[1].Text, selectedItem.SubItems[0].Text, ".gguf");
		}
		private void HugLoadFiles(string uploader, string modelName, string fileExt){
			if(string.IsNullOrEmpty(uploader) || string.IsNullOrEmpty(modelName) || string.IsNullOrEmpty(fileExt)) return;
			var repoId = $"{uploader}/{modelName}";
			_uploader = uploader;
			_modelName = modelName;
			listViewHugFiles.BeginUpdate();
			listViewHugFiles.Items.Clear();
			ThreadPool.QueueUserWorkItem(o => {
				try{
					var infoUrl = $"https://huggingface.co/api/models/{repoId}?blobs=true";
					var resPtr = NativeMethods.PerformHttpGet(infoUrl);
					var infoJson = Marshal.PtrToStringAnsi(resPtr);
					NativeMethods.FreeMemory(resPtr);
					if(infoJson == null) throw new ArgumentNullException();
					if(infoJson.StartsWith("Error:")) throw new Exception(infoJson);
					var info = JObject.Parse(infoJson);
					if(!info.TryGetValue("siblings", out var siblings) || !(siblings is JArray siblingsArray)) return;
					foreach(var file in siblingsArray){
						if(!(file is JObject fileObject)) continue;
						var fileName = fileObject.Value<string>("rfilename") ?? fileObject.Value<string>("filename");
						var fileSize = fileObject.Value<long?>("size");
						if(string.IsNullOrEmpty(fileName)) continue;
						if(!fileName.EndsWith(fileExt, StringComparison.OrdinalIgnoreCase)) continue;
						var sizeDisplay = fileSize.HasValue ? $"{fileSize.Value/1048576:F2} MB" : "";
						var item = new ListViewItem(new[]{ fileName, sizeDisplay });
						Invoke(new MethodInvoker(() => {listViewHugFiles.Items.Add(item);}));
					}
				} catch(HttpRequestException ex){
					Invoke(new MethodInvoker(() => {MessageBox.Show(this, $"HTTP Error loading files for {repoId}: {ex.Message}", "LM Stud Error", MessageBoxButtons.OK, MessageBoxIcon.Error);}));
				} catch(JsonException ex){
					Invoke(new MethodInvoker(() => {MessageBox.Show(this, $"JSON Parse Error for {repoId}: {ex.Message}", "LM Stud Error", MessageBoxButtons.OK, MessageBoxIcon.Error);}));
				} catch(Exception ex){
					Invoke(new MethodInvoker(() => {MessageBox.Show(this, $"Error loading files for {repoId}: {ex.Message}", "LM Stud Error", MessageBoxButtons.OK, MessageBoxIcon.Error);}));
				} finally{ Invoke(new MethodInvoker(() => {listViewHugFiles.EndUpdate();})); }
			});
		}
		private void HugDownloadFile(string uploader, string modelName, string variantLabel){
			var downloadUrl = $"https://huggingface.co/{uploader}/{modelName}/resolve/main/{variantLabel}";
			var targetDir = Path.Combine(_modelsPath, uploader, modelName);
			try{ Directory.CreateDirectory(targetDir); } catch(Exception ex){
				MessageBox.Show($"Failed to create directory: {ex.Message}", "LM Stud Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
				return;
			}
			var targetPath = Path.Combine(targetDir, variantLabel);
			_downloading = true;
			progressBar1.Value = 0;
			progressBar1.Maximum = 1000;
			int ProgressCb(long totalBytes, long downloadedBytes){
				if(totalBytes <= 0) return 0;
				var percent = (int)(downloadedBytes*1000/totalBytes);
				progressBar1.Invoke((MethodInvoker)(() => {
					progressBar1.Value = percent;
				}));
				return _downloading ? 0 : 1;
			}
			ThreadPool.QueueUserWorkItem(_ => {
				try{
					var result = NativeMethods.DownloadFileWithProgress(downloadUrl, targetPath, ProgressCb);
					if(result == 0){
						if(_downloading){
							Invoke(new MethodInvoker(() => {
								MessageBox.Show(this, $"Downloaded {variantLabel} to:\n{targetPath}", "Download Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
								PopulateModels();
							}));
						} else{ File.Delete(targetPath); }
					} else{
						Invoke(new MethodInvoker(() => {MessageBox.Show(this, $"Download failed with error code: {result}", "LM Stud Error", MessageBoxButtons.OK, MessageBoxIcon.Error);}));
					}
				} catch(Exception ex){
					if(_downloading) Invoke(new MethodInvoker(() => {MessageBox.Show(this, $"Download failed: {ex.Message}", "LM Stud Error", MessageBoxButtons.OK, MessageBoxIcon.Error);}));
				} finally{
					Invoke(new MethodInvoker(() => {
						progressBar1.Value = 0;
						butDownload.Text = "Download";
						_downloading = false;
					}));
				}
			});
		}
		public class HugModel{
			public string CreatedAt = null;
			public string Downloads = null;
			public string ID = null;
			public string LastModified = null;
			public string Likes = null;
			public string TrendingScore = null;
		}
	}
}