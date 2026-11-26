using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using LMStud;
using LMStud.Properties;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LM_Stud.Tests{
	[TestClass]
	[DoNotParallelize]
	public class ApplicationWorkflowTests{
		private const int TimeoutMs = 15000;
		private const string Slot = "workflow-chat";
		private static Form1 _form;
		private static Thread _ui;
		private static LoopbackServer _upstream;
		private static string _profile;
		private static string _models;
		private static string _files;
		private static ManualResetEventSlim _shown;
		private static volatile Exception _uiError;

		[ClassInitialize]
		public static void Start(TestContext context){
			_profile = Path.Combine(Path.GetTempPath(), "lmstud-workflows-" + Guid.NewGuid().ToString("N"));
			_models = Path.Combine(_profile, "models");
			_files = Path.Combine(_profile, "files");
			Directory.CreateDirectory(_models);
			Directory.CreateDirectory(_files);
			_upstream = new LoopbackServer();
			var slot = Json.Object(Json.P("Name", Slot), Json.P("Source", "Api"), Json.P("Use", "Chat,Server"),
				Json.P("ApiBaseUrl", _upstream.Url), Json.P("ApiModel", "workflow-model"));
			File.WriteAllText(Path.Combine(_profile, "ModelSlots.json"), Json.Object(Json.P("ActiveChatSlot", Slot), Json.P("Slots", Json.Array(slot))).ToJson());

			var settings = Settings.Default;
			settings.ModelsDir = _models;
			settings.LoadAuto = false;
			settings.LastModel = "";
			settings.Speak = false;
			settings.APIServerEnable = false;
			settings.FileBaseDir = _files;
			settings.FileListEnable = settings.FileReadEnable = true;
			settings.FileCreateEnable = settings.FileWriteEnable = settings.CMDToolEnable = false;
			settings.GoogleSearchEnable = settings.WebpageFetchEnable = settings.DateTimeEnable = false;
			_shown = new ManualResetEventSlim();
			_ui = new Thread(() => {
				try{
					Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException, true);
					Application.EnableVisualStyles();
					Application.SetCompatibleTextRenderingDefault(false);
					using(var form = new Form1(_profile, _upstream.Url)){
						Program.MainForm = _form = form;
						form.Shown += (sender, args) => _shown.Set();
						Application.Run(form);
					}
				} catch(Exception ex){ _uiError = ex; _shown.Set(); }
			}){ IsBackground = true };
			_ui.SetApartmentState(ApartmentState.STA);
			_ui.Start();
			Assert.IsTrue(_shown.Wait(TimeoutMs), "The application window did not open.");
			Assert.IsNull(_uiError, _uiError?.ToString());
			Assert.IsTrue(_form.PopulateLock.Wait(TimeoutMs), "The initial model scan did not finish.");
			_form.PopulateLock.Release();
		}

		[ClassCleanup]
		public static void Stop(){
			try{
				if(_form != null && !_form.IsDisposed){
					_form.BeginInvoke(new MethodInvoker(() => {
						CloseDialog();
						_form.BeginInvoke(new MethodInvoker(_form.Close));
					}));
				}
				Assert.IsTrue(_ui == null || _ui.Join(TimeoutMs), "The form did not close.");
				_shown?.Dispose();
			} finally{
				Program.MainForm = null;
				_upstream?.Dispose();
				Settings.Default.Reload();
				if(Directory.Exists(_profile)) Directory.Delete(_profile, true);
			}
		}

		[TestInitialize]
		public void ResetConversation(){
			_upstream.Error = null;
			WaitFor(ChatIsIdle, "the previous chat action to finish");
			OnForm(() => {
				Control<TabControl>("tabControlMain").SelectTab("tabPageChat");
				_form.textInput.Clear();
				_form.checkStream.Checked = true;
				_form.checkMarkdown.Checked = true;
				_form.butReset.PerformClick();
			});
			WaitFor(() => ChatIsIdle() && _form.ChatMessages.Count == 0, "chat reset");
		}
		[TestCleanup]
		public void CloseTestDialog(){
			if(_uiError == null && !_form.IsDisposed) OnForm(CloseDialog);
		}

		[TestMethod]
		public void StreamEditRegenerateAndReopenAConversation(){
			const string original = "Original question";
			const string edited = "Corrected question";
			using(var continueStream = new ManualResetEventSlim()){
				_upstream.Handle = context => {
					var input = ReadRequest(context)["input"];
					var question = input.Last(item => item.GetString("role") == "user").GetString("content");
					if(question == original){
						context.Response.ContentType = "text/event-stream";
						context.Response.SendChunked = true;
						WriteEvent(context.Response, "**First**");
						if(!continueStream.Wait(TimeoutMs)) throw new TimeoutException("The first delta never appeared in chat.");
						WriteEvent(context.Response, " reply");
						return;
					}
					if(question == edited){ Reply(context, Message("Regenerated answer")); return; }
					CollectionAssert.AreEqual(new[]{ edited, "Regenerated answer", "Continue the saved chat" },
						input.Select(item => item.GetString("content")).ToArray(), "The reopened conversation must reach the model.");
					Reply(context, Message("Continued from saved history"));
				};

				Send(original);
				try{ WaitFor(() => Generation.Generating && VisibleReply() == "First", "the live streamed reply"); }
				finally{ continueStream.Set(); }
				WaitForReply("First reply");
			}

			OnForm(() => {
				var message = _form.ChatMessages[0];
				message.butEdit.PerformClick();
				message.richTextMsg.Text = "Discard this edit";
				message.butCancelEdit.PerformClick();
				message.butEdit.PerformClick();
				Assert.AreEqual(original, message.richTextMsg.Text);
				message.richTextMsg.Text = edited;
				message.butApplyEdit.PerformClick();
			});
			WaitFor(() => ChatIsIdle() && _form.ChatMessages[0].Message == edited, "the applied edit");
			OnForm(() => _form.ChatMessages[0].butRegen.PerformClick());
			WaitForReply("Regenerated answer");

			var path = Path.Combine(_profile, "conversation.lmchat");
			OnForm(() => {
				_form.textInput.Text = "Unsent draft";
				_form.ExportChatArchive(path);
			});
			WaitFor(() => ChatIsIdle() && File.Exists(path), "the exported chat");
			ResetConversation();
			OnForm(() => _form.RestoreChatArchive(path));
			WaitFor(() => ChatIsIdle() && _form.textInput.Text == "Unsent draft" && VisibleReply() == "Regenerated answer", "the restored chat and draft");
			Send("Continue the saved chat");
			WaitForReply("Continued from saved history");
		}

		[TestMethod]
		public void ChatFindsAndInspectsAFileThroughItsTools(){
			const string name = "\u6587\u4ef6 sample.txt";
			File.WriteAllText(Path.Combine(_files, name), "one\r\n\r\nlast");
			_upstream.Handle = context => {
				var results = ReadRequest(context)["input"].Where(item => item.GetString("type") == "function_call_output").ToArray();
				if(results.Length == 0){
					Reply(context, ToolCall("list", "list_directory", Json.Object(Json.P("path", "."))));
				} else if(results.Length == 1){
					var listing = results[0].GetString("output");
					Assert.IsTrue(Json.Parse(listing)["entries"].Any(item => item.AsString() == name), listing);
					Reply(context, ToolCall("inspect", "get_file_info", Json.Object(Json.P("path", name))));
				} else{
					var info = Json.Parse(results.Last().GetString("output"));
					Assert.AreEqual(3, info["text"].GetInt("line_count"));
					Reply(context, Message("The file has 3 lines."));
				}
			};
			Send("Find the sample file and tell me how many lines it has.");
			WaitForReply("The file has 3 lines.");
			Assert.AreEqual(2, OnForm(() => _form.ChatMessages.Count(message => message.Role == MessageRole.Tool)));
		}

		[TestMethod]
		public void DownloadAndFindASplitModelInTheLibrary(){
			const string repo = "fixtures/split-model";
			const string folder = "nested variant/";
			const string first = folder + "model-00001-of-00002.gguf";
			const string second = folder + "model-00002-of-00002.gguf";
			var model = new byte[24];
			Encoding.ASCII.GetBytes("GGUF").CopyTo(model, 0);
			model[4] = 3;
			var replacement = model.Concat(new byte[]{ 1 }).ToArray();
			var attempt = 0;
			var secondPartRequests = 0;
			_upstream.Handle = context => {
				switch(Uri.UnescapeDataString(context.Request.Url.AbsolutePath)){
					case "/api/models":
						Reply(context, Json.Array(Json.Object(Json.P("id", repo))));
						break;
					case "/api/models/" + repo:
						Reply(context, Json.Object(Json.P("sha", "fixture-revision"), Json.P("siblings", Json.Array(
							Json.Object(Json.P("rfilename", first)), Json.Object(Json.P("rfilename", second))))));
						break;
					case "/" + repo + "/resolve/fixture-revision/" + first:
						Write(context, ++attempt == 1 ? model : replacement);
						break;
					case "/" + repo + "/resolve/fixture-revision/" + second:
						Interlocked.Increment(ref secondPartRequests);
						if(attempt == 1) Write(context, model);
						else context.Response.StatusCode = 404;
						break;
					default: throw new InvalidOperationException(context.Request.RawUrl);
				}
			};

			OnForm(() => {
				Control<TabControl>("tabControlMain").SelectTab("tabPageHuggingFace");
				Control<TextBox>("textSearchTerm").Text = "split-model";
				Control<Button>("butSearch").PerformClick();
			});
			WaitFor(() => Control<ListView>("listViewHugSearch").Items.Count == 1, "Hugging Face search results");
			OnForm(() => Control<ListView>("listViewHugSearch").Items[0].Selected = true);
			WaitFor(() => Control<ListView>("listViewHugFiles").Items.Count == 1, "one downloadable split model");
			OnForm(() => {
				Control<ListView>("listViewHugFiles").Items[0].Selected = true;
				Control<Button>("butDownload").PerformClick();
			});
			var destination = Path.Combine(_models, "fixtures", "split-model");
			var firstPath = Path.GetFullPath(Path.Combine(destination, first));
			var secondPath = Path.GetFullPath(Path.Combine(destination, second));
			WaitFor(() => {
				CloseDialog();
				return _form.listViewModels.Items.Count == 1 && Path.GetFileName(_form.listViewModels.Items[0].SubItems[1].Text) == Path.GetFileName(first);
			}, "one downloaded model in the local library");

			OnForm(() => Control<Button>("butDownload").PerformClick());
			WaitFor(() => {
				CloseDialog();
				return Volatile.Read(ref secondPartRequests) == 2 && Control<Button>("butDownload").Text == Resources.Download;
			}, "the failed replacement to finish");
			CollectionAssert.AreEqual(model, File.ReadAllBytes(firstPath));
			CollectionAssert.AreEqual(model, File.ReadAllBytes(secondPath));
		}

		[TestMethod]
		public void TheRunningFormServesChatThroughItsConfiguredSlot(){
			_upstream.Handle = context => {
				if(context.Request.Url.AbsolutePath == "/v1/responses"){ context.Response.StatusCode = 404; return; }
				Assert.AreEqual("/v1/chat/completions", context.Request.Url.AbsolutePath);
				var request = ReadRequest(context);
				Assert.AreEqual("workflow-model", request.GetString("model"));
				Assert.AreEqual("Reply through my slot", request["messages"].Last().GetString("content"));
				Reply(context, Json.Object(Json.P("choices", Json.Array(Json.Object(Json.P("message",
					Json.Object(Json.P("role", "assistant"), Json.P("content", "Reply from the configured slot"))))))));
			};
			var port = LoopbackServer.FreePort();
			OnForm(() => { Common.APIServerPort = port; _form.ApiServer.Start("127.0.0.1"); });
			try{
				var address = "http://127.0.0.1:" + port;
				using(var client = new WebClient{ Encoding = Encoding.UTF8, Proxy = null }){
					var models = Json.Parse(client.DownloadString(address + "/v1/models"))["data"];
					Assert.IsTrue(models.Any(model => model.GetString("id") == "lmstud/" + Slot));
					client.Headers[HttpRequestHeader.ContentType] = "application/json";
					var request = Json.Object(Json.P("model", "lmstud/" + Slot), Json.P("input", "Reply through my slot"), Json.P("store", false));
					var reply = client.UploadString(address + "/v1/responses", request.ToJson());
					StringAssert.Contains(reply, "Reply from the configured slot");
				}
			} finally{ OnForm(() => _form.ApiServer.Stop()); }
		}

		private static T Control<T>(string name) where T : Control{ return (T)_form.Controls.Find(name, true).Single(); }
		private static T OnForm<T>(Func<T> action){
			Assert.IsNull(_uiError, _uiError?.ToString());
			var pending = _form.BeginInvoke(action);
			Assert.IsTrue(pending.AsyncWaitHandle.WaitOne(TimeoutMs), "The application window did not respond.");
			return (T)_form.EndInvoke(pending);
		}
		private static void OnForm(Action action){ OnForm(() => { action(); return true; }); }
		private static void WaitFor(Func<bool> condition, string action){
			var watch = Stopwatch.StartNew();
			while(watch.ElapsedMilliseconds < TimeoutMs){
				Assert.IsNull(_upstream.Error, _upstream.Error?.ToString());
				if(OnForm(condition)) return;
				Thread.Sleep(15);
			}
			Assert.Fail("Timed out waiting for " + action + ".");
		}
		private static bool ChatIsIdle(){
			if(Generation.Generating || _form.ChatFileOperationActive) return false;
			using(var lease = ModelSlotManager.TryEnterSlot(Slot, 0)) return lease != null;
		}
		private static string VisibleReply(){ return _form.ChatMessages.LastOrDefault()?.MarkdownView.PlainText; }
		private static void Send(string prompt){ OnForm(() => { _form.textInput.Text = prompt; _form.butGen.PerformClick(); }); }
		private static void WaitForReply(string text){ WaitFor(() => ChatIsIdle() && VisibleReply() == text, text); }

		private static JsonNode ReadRequest(HttpListenerContext context){
			using(var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding)) return Json.Parse(reader.ReadToEnd());
		}
		private static JsonNode Message(string text){ return Json.Object(Json.P("output", Json.Array(Json.Object(
			Json.P("type", "message"), Json.P("role", "assistant"), Json.P("content", Json.Array(Json.Object(Json.P("type", "output_text"), Json.P("text", text)))))))); }
		private static JsonNode ToolCall(string id, string name, JsonNode arguments){ return Json.Object(Json.P("output", Json.Array(
			Json.Object(Json.P("type", "function_call"), Json.P("call_id", id), Json.P("name", name), Json.P("arguments", arguments.ToJson()))))); }
		private static void Reply(HttpListenerContext context, JsonNode body){ Write(context, Encoding.UTF8.GetBytes(body.ToJson()), "application/json"); }
		private static void Write(HttpListenerContext context, byte[] bytes, string type = "application/octet-stream"){
			context.Response.ContentType = type;
			context.Response.ContentLength64 = bytes.Length;
			context.Response.OutputStream.Write(bytes, 0, bytes.Length);
		}
		private static void WriteEvent(HttpListenerResponse response, string text){
			var payload = Json.Object(Json.P("type", "response.output_text.delta"), Json.P("delta", text)).ToJson();
			var bytes = Encoding.UTF8.GetBytes("data: " + payload + "\n\n");
			response.OutputStream.Write(bytes, 0, bytes.Length);
			response.OutputStream.Flush();
		}

		private static void CloseDialog(){
			var dialog = FindDialog();
			if(dialog != IntPtr.Zero) PostMessage(dialog, 0x0010, IntPtr.Zero, IntPtr.Zero);
		}
		private static IntPtr FindDialog(){
			var dialog = IntPtr.Zero;
			EnumThreadWindows(GetCurrentThreadId(), (window, state) => {
				if(window == _form.Handle || GetDlgItem(window, 1) == IntPtr.Zero) return true;
				dialog = window;
				return false;
			}, IntPtr.Zero);
			return dialog;
		}
		private delegate bool EnumWindowCallback(IntPtr window, IntPtr state);
		[DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
		[DllImport("user32.dll")] private static extern bool EnumThreadWindows(uint threadId, EnumWindowCallback callback, IntPtr state);
		[DllImport("user32.dll")] private static extern IntPtr GetDlgItem(IntPtr window, int id);
		[DllImport("user32.dll")] private static extern bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

		private sealed class LoopbackServer : IDisposable{
			private readonly HttpListener _listener = new HttpListener();
			private readonly Thread _thread;
			internal readonly string Url = "http://127.0.0.1:" + FreePort();
			internal volatile Action<HttpListenerContext> Handle;
			internal volatile Exception Error;
			internal LoopbackServer(){
				_listener.Prefixes.Add(Url + "/");
				_listener.Start();
				_thread = new Thread(Listen){ IsBackground = true };
				_thread.Start();
			}
			private void Listen(){
				while(_listener.IsListening){
					HttpListenerContext context;
					try{ context = _listener.GetContext(); }
					catch(HttpListenerException){ return; } catch(ObjectDisposedException){ return; }
					try{ Handle(context); }
					catch(Exception ex){
						Error = ex;
						try{ context.Response.StatusCode = 500; } catch(InvalidOperationException){}
					} finally{
						try{ context.Response.Close(); } catch(HttpListenerException){} catch(ObjectDisposedException){}
					}
				}
			}
			internal static int FreePort(){
				var socket = new TcpListener(IPAddress.Loopback, 0);
				socket.Start();
				var port = ((IPEndPoint)socket.LocalEndpoint).Port;
				socket.Stop();
				return port;
			}
			public void Dispose(){
				_listener.Close();
				Assert.IsTrue(_thread.Join(TimeoutMs), "The loopback server did not stop.");
			}
		}
	}
}
