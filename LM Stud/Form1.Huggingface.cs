using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Windows.Forms;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
namespace LMStud{
	internal partial class Form1{
		private readonly HttpClient _client = new HttpClient();
		private const string ApiUrl = "https://huggingface.co/api/models";
		private const string Filter = "text-generation";
		private const int Limit = 50;
		private const string SearchAppend = " gguf"; // Extracted constant
		private bool _downloading;
		private void TextSearchTerm_KeyDown(object sender, KeyEventArgs e){
			if(e.KeyCode != Keys.Enter) return;
			Search(textSearchTerm.Text);
		}
		private void ButSearch_Click(object sender, EventArgs e){Search(textSearchTerm.Text);}
		private void ButDownload_Click(object sender, EventArgs e){
			if(!_downloading) DownloadQuant();
			else _downloading = false;
		}
		public class HuggingFaceModel{
			public string ID = null;
			public string Likes = null;
			public string Downloads = null;
			public string TrendingScore = null;
			public string CreatedAt = null;
			public string LastModified = null;
		}
		private void Search(string term) {
			if(string.IsNullOrWhiteSpace(term)) return;
			butSearch.Enabled = textSearchTerm.Enabled = false;
			ThreadPool.QueueUserWorkItem(o => {
				try {
					var escapedTerm = Uri.EscapeDataString(term + SearchAppend);
					var url = $"{ApiUrl}?filter={Filter}&search={escapedTerm}&limit={Limit}&full=true";
					var response = _client.GetAsync(url).ConfigureAwait(false).GetAwaiter().GetResult();
					response.EnsureSuccessStatusCode();
					var json = response.Content.ReadAsStringAsync().ConfigureAwait(false).GetAwaiter().GetResult();
					var models = JArray.Parse(json);
					Invoke(new MethodInvoker(() => {
						listViewModelSearch.BeginUpdate();
						listViewModelSearch.Items.Clear();
						listViewQuants.Items.Clear();
					}));
					foreach(var modelToken in models) {
						var model = (JObject)modelToken;
						var hfModel = model.ToObject<HuggingFaceModel>();
						if(hfModel?.ID == null) continue;
						var parts = hfModel.ID.Split('/');
						var uploader = parts.Length > 1 ? parts[0] : "";
						var modelName = parts.Length > 1 ? parts[1] : hfModel.ID;
						Invoke(new MethodInvoker(() => { listViewModelSearch.Items.Add(new ListViewItem(new[] { modelName, uploader, hfModel.Likes, hfModel.Downloads, hfModel.TrendingScore, hfModel.CreatedAt, hfModel.LastModified })); }));
					}
				} catch(Exception ex) {
					Invoke(new MethodInvoker(() => {
						MessageBox.Show(this, $"Model search error: {ex.Message}", "LM Stud Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
					}));
				} finally {
					Invoke(new MethodInvoker(() => {
						listViewModelSearch.EndUpdate();
						butSearch.Enabled = textSearchTerm.Enabled = true;
					}));
				}
			});
		}
		private void ListViewModelSearch_SelectedIndexChanged(object sender, EventArgs e){
			if(listViewModelSearch.SelectedItems.Count == 0) return;
			var selectedItem = listViewModelSearch.SelectedItems[0];
			var modelName = selectedItem.SubItems[0].Text;
			var uploader = selectedItem.SubItems[1].Text;
			var repoId = $"{uploader}/{modelName}";
			LoadQuants(repoId);
		}
		private void LoadQuants(string repoId) {
			listViewQuants.BeginUpdate();
			listViewQuants.Items.Clear();
			ThreadPool.QueueUserWorkItem(o => {
				try {
					var infoUrl = $"https://huggingface.co/api/models/{repoId}?blobs=true";
					var response = _client.GetAsync(infoUrl).ConfigureAwait(false).GetAwaiter().GetResult();
					response.EnsureSuccessStatusCode();
					var infoJson = response.Content.ReadAsStringAsync().ConfigureAwait(false).GetAwaiter().GetResult();
					var info = JObject.Parse(infoJson);
					if(!info.TryGetValue("siblings", out var siblings) || !(siblings is JArray siblingsArray)) return;
					foreach(var file in siblingsArray){
						if(!(file is JObject fileObject)) continue;
						var fileName = fileObject.Value<string>("rfilename") ?? fileObject.Value<string>("filename");
						var fileSize = fileObject.Value<long?>("size");
						if(string.IsNullOrEmpty(fileName)) continue;
						var format = fileName.EndsWith(".gguf", StringComparison.OrdinalIgnoreCase) ? "GGUF" : null;
						if(format == null) continue;
						var sizeDisplay = fileSize.HasValue ? $"{fileSize.Value / 1048576:F2} MB" : "";
						var item = new ListViewItem(new[] { fileName, sizeDisplay });
						Invoke(new MethodInvoker(() => { listViewQuants.Items.Add(item); }));
					}
				} catch(HttpRequestException ex) {
					Invoke(new MethodInvoker(() => {
						MessageBox.Show(this, $"HTTP Error loading files for {repoId}: {ex.Message}", "LM Stud Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
					}));
				} catch(JsonException ex) {
					Invoke(new MethodInvoker(() => {
						MessageBox.Show(this, $"JSON Parse Error for {repoId}: {ex.Message}", "LM Stud Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
					}));
				} catch(Exception ex) {
					Invoke(new MethodInvoker(() => {
						MessageBox.Show(this, $"Error loading files for {repoId}: {ex.Message}", "LM Stud Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
					}));
				} finally {
					Invoke(new MethodInvoker(() => {
						listViewQuants.EndUpdate();
					}));
				}
			});
		}
		private void DownloadQuant() {
			if(listViewQuants.SelectedItems.Count == 0 || listViewModelSearch.SelectedItems.Count == 0) {
				MessageBox.Show("Please select an item from both lists.", "Selection Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
				return;
			}
			var variantLabel = listViewQuants.SelectedItems[0].SubItems[0].Text;
			var uploader = listViewModelSearch.SelectedItems[0].SubItems[1].Text;
			var modelName = listViewModelSearch.SelectedItems[0].SubItems[0].Text;
			var branch = "main";
			var baseFolder = _modelsPath;
			var targetDir = Path.Combine(baseFolder, uploader, modelName);
			try {
				Directory.CreateDirectory(targetDir);
			} catch(Exception ex) {
				MessageBox.Show($"Failed to create directory: {ex.Message}", "LM Stud Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
				return;
			}
			var downloadUrl = $"https://huggingface.co/{uploader}/{modelName}/resolve/{branch}/{variantLabel}";
			var targetPath = Path.Combine(targetDir, variantLabel);
			_downloading = true;
			butDownload.Text = "Cancel";
			progressBar1.Value = 0;
			ThreadPool.QueueUserWorkItem(_ => {
				try {
					using(var response = _client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false).GetAwaiter().GetResult()) {
						response.EnsureSuccessStatusCode();
						using(var httpStream = response.Content.ReadAsStreamAsync().ConfigureAwait(false).GetAwaiter().GetResult()) {
							using(var fileStream = File.Create(targetPath)) {
								var contentLength = response.Content.Headers.ContentLength ?? 0;
								var buffer = new byte[10485760];
								Invoke(new MethodInvoker(() => progressBar1.Maximum = (int)(contentLength/buffer.Length)));
								long totalBytesRead = 0;
								while(_downloading) {
									var bytesRead = httpStream.Read(buffer, 0, buffer.Length);
									if(bytesRead == 0) break;
									fileStream.Write(buffer, 0, bytesRead);
									totalBytesRead += bytesRead;
									if(contentLength <= 0) continue;
									var read = totalBytesRead;
									BeginInvoke(new MethodInvoker(() => progressBar1.Value = (int)(read/buffer.Length)));
								}
								if(_downloading && contentLength > 0 && totalBytesRead != contentLength) throw new IOException($"Download incomplete - Expected {contentLength} bytes, received {totalBytesRead}");
							}
						}
					}
					if(_downloading)
						Invoke(new MethodInvoker(() => {
							MessageBox.Show(this, $"Downloaded {variantLabel} to:\n{targetPath}", "Download Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
							PopulateModels();
						}));
					else File.Delete(targetPath);
				} catch(HttpRequestException httpEx) {
					Invoke(new MethodInvoker(() => {
						MessageBox.Show(this, httpEx.ToString(), "Download Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
					}));
				} catch(Exception ex) {
					Invoke(new MethodInvoker(() => {
						MessageBox.Show(this, $"Download failed: {ex.Message}", "LM Stud Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
					}));
				} finally {
					Invoke(new MethodInvoker(() => {
						progressBar1.Value = 0;
						butDownload.Text = "Download";
						_downloading = false;
					}));
				}
			});
		}
	}
}