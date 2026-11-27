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
		private static MessageBoxIcon _downloadNotice;
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
			var slot = Json.Object(Json.P("Name", Slot), Json.P("Source", "Api"), Json.P("Use", "Chat,Server"), Json.P("ApiBaseUrl", _upstream.Url), Json.P("ApiModel", "workflow-model"));
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
					using(var form = new Form1(_profile, _upstream.Url, (_, icon) => _downloadNotice = icon)){
						Program.MainForm = _form = form;
						form.Shown += (sender, args) => _shown.Set();
						Application.Run(form);
					}
				} catch(Exception ex){
					_uiError = ex;
					_shown.Set();
				}
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
				if(_form != null && !_form.IsDisposed)
					_form.BeginInvoke(new MethodInvoker(() => {
						CloseDialog();
						_form.BeginInvoke(new MethodInvoker(_form.Close));
					}));
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
						WriteSse(context.Response, Json.Object(Json.P("type", "response.completed"), Json.P("response", Message("**First** reply"))).ToJson());
						return;
					}
					if(question == edited){
						var item = Json.Object(Json.P("type", "reasoning"), Json.P("id", "rs_regenerated"), Json.P("encrypted_content", "test-state"), Json.P("summary", Json.Array()));
						Reply(context, Json.Object(Json.P("output", Json.Array(item, Message("Regenerated answer")["output"][0]))));
						return;
					}
					CollectionAssert.AreEqual(new[]{ edited, "Regenerated answer", "Continue the saved chat" },
						input.Where(item => item["role"].Exists).Select(item => item["content"].IsString ? item.GetString("content") : item["content"][0].GetString("text")).ToArray(),
						"The reopened conversation must reach the model.");
					Assert.AreEqual("test-state", input.Single(item => item.GetString("id") == "rs_regenerated").GetString("encrypted_content"));
					Reply(context, Message("Continued from saved history"));
				};
				Send(original);
				try{ WaitFor(() => Generation.Generating && VisibleReply() == "First", "the live streamed reply"); } finally{ continueStream.Set(); }
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
				if(results.Length == 0){ Reply(context, ToolCall("list", "list_directory", Json.Object(Json.P("path", ".")))); } else if(results.Length == 1){
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
						Reply(context, Json.Object(Json.P("sha", "fixture-revision"), Json.P("siblings", Json.Array(Json.Object(Json.P("rfilename", first)), Json.Object(Json.P("rfilename", second))))));
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
			WaitFor(() => _downloadNotice == MessageBoxIcon.Information && _form.listViewModels.Items.Count == 1
				&& Path.GetFileName(_form.listViewModels.Items[0].SubItems[1].Text) == Path.GetFileName(first), "one downloaded model in the local library");
			OnForm(() => Control<Button>("butDownload").PerformClick());
			WaitFor(() => Volatile.Read(ref secondPartRequests) == 2 && _downloadNotice == MessageBoxIcon.Error, "the failed replacement to finish");
			CollectionAssert.AreEqual(model, File.ReadAllBytes(firstPath));
			CollectionAssert.AreEqual(model, File.ReadAllBytes(secondPath));
		}
		[TestMethod]
		public void TheRunningFormServesChatThroughItsConfiguredSlot(){
			const string token = "workflow-token";
			var original = ModelSlotManager.GetActiveChatSlot();
			var slot = original.Clone();
			slot.ApiStore = true;
			slot.ApiBaseUrl = _upstream.Url + "/gateway/openai/";
			var address = "http://127.0.0.1:" + LoopbackServer.FreePort();
			OnForm(() => {
				ModelSlotManager.AddOrUpdate(slot);
				Common.APIServerPort = new Uri(address).Port;
				Assert.IsTrue(_form.ApiServer.Start("localhost", token));
			});
			try{
				Assert.AreEqual(HttpStatusCode.Unauthorized, ReadApiError(() => ApiRequest(address + "/v1/models", null)));
				using(var models = ApiRequest(address + "/v1/models", token))
				using(var reader = new StreamReader(models.GetResponseStream()))
					Assert.IsTrue(Json.Parse(reader.ReadToEnd())["data"].Any(item => item.GetString("id") == "lmstud/" + Slot));

				var prefix = address + "/v1/responses";
				var transcript = Json.Array(
					Json.Object(Json.P("role", "developer"), Json.P("content", "Use the supplied context.")),
					Json.Object(Json.P("role", "user"), Json.P("content", "Prior context")),
					Json.Object(Json.P("role", "assistant"), Json.P("content", "Acknowledged")),
					Json.Object(Json.P("role", "user"), Json.P("content", "Parent")));
				_upstream.Handle = context => {
					Assert.AreEqual("/gateway/openai/responses", context.Request.Url.AbsolutePath);
					var request = ReadRequest(context);
					var input = request["input"].Select(item => item["content"].IsString ? item.GetString("content") : item["content"][0].GetString("text")).ToArray();
					CollectionAssert.AreEqual(new[]{ "Use the supplied context.", "Prior context", "Acknowledged", "Parent" }, input.Take(4).ToArray());
					if(input.Last() == "Parent"){
						Assert.AreEqual(17, request.GetInt("max_output_tokens"));
						Assert.AreEqual(0.2d, request.GetDouble("temperature"));
					} else{
						Assert.AreEqual(6, input.Length, "A continuation should use the specified response snapshot.");
						Assert.AreEqual("From the slot", input[4]);
					}
					Assert.AreEqual(input.Last() != "Private branch", request.GetBool("store"));
					Reply(context, Message("From the slot"));
				};
				var parent = PostApi(prefix, token, Json.P("input", transcript), Json.P("max_output_tokens", 17), Json.P("temperature", 0.2));
				var id = parent.GetString("id");
				PostApi(prefix, token, Json.P("input", "First branch"), Json.P("previous_response_id", id));
				var privateBranch = PostApi(prefix, token, Json.P("input", "Private branch"), Json.P("previous_response_id", id), Json.P("store", false));
				Assert.AreEqual(HttpStatusCode.NotFound, ReadApiError(() => ApiRequest(prefix + "/" + privateBranch.GetString("id"), token)));
				PostApi(prefix, token, Json.P("input", "Third branch"), Json.P("previous_response_id", id));
				Assert.AreEqual(HttpStatusCode.NotFound, ReadApiError(() => PostApi(prefix, token, Json.P("input", "Missing parent"), Json.P("previous_response_id", "resp_missing"))));

				using(var finish = new ManualResetEventSlim()){
					_upstream.Handle = context => {
						var request = ReadRequest(context);
						Assert.AreEqual(true, request.GetBool("stream"));
						Assert.AreEqual(7, request.GetInt("max_output_tokens"));
						context.Response.ContentType = "text/event-stream";
						context.Response.SendChunked = true;
						WriteSse(context.Response, "{\"type\":\"response.output_item.added\",\"output_index\":0,\"item\":{\"id\":\"msg_stream\",\"type\":\"message\",\"role\":\"assistant\",\"status\":\"in_progress\",\"content\":[]}}");
						WriteSse(context.Response, "{\"type\":\"response.output_text.delta\",\"output_index\":0,\"content_index\":0,\"item_id\":\"msg_stream\",\"delta\":\"Streamed\"}");
						if(!finish.Wait(TimeoutMs)) throw new TimeoutException("The server buffered the upstream stream.");
						var item = Json.Object(Json.P("id", "msg_stream"), Json.P("type", "message"), Json.P("status", "incomplete"), Json.P("role", "assistant"),
							Json.P("content", Message("Streamed")["output"][0]["content"]));
						WriteSse(context.Response, Json.Object(Json.P("type", "response.output_item.done"), Json.P("output_index", 0), Json.P("item", item)).ToJson());
						WriteSse(context.Response, Json.Object(Json.P("type", "response.incomplete"), Json.P("response",
							Json.Object(Json.P("output", Json.Array(item)), Json.P("status", "incomplete"),
								Json.P("incomplete_details", Json.Object(Json.P("reason", "max_output_tokens"))),
								Json.P("usage", Json.Object(Json.P("input_tokens", 9), Json.P("output_tokens", 7), Json.P("total_tokens", 16)))))).ToJson());
					};
					try{
						using(var response = ApiRequest(prefix, token, Json.Object(Json.P("model", "lmstud/" + Slot), Json.P("input", "Stream"),
							Json.P("stream", true), Json.P("max_output_tokens", 7))))
						using(var reader = new StreamReader(response.GetResponseStream())){
							StringAssert.StartsWith(response.ContentType, "text/event-stream");
							var delta = ReadApiEvent(reader, "response.output_text.delta");
							Assert.AreEqual("Streamed", delta.GetString("delta"));
							Assert.AreEqual("msg_stream", delta.GetString("item_id"));
							finish.Set();
							var completed = ReadApiEvent(reader, "response.incomplete")["response"];
							Assert.AreEqual("max_output_tokens", completed["incomplete_details"].GetString("reason"));
							Assert.AreEqual(16, completed["usage"].GetInt("total_tokens"));
							Assert.AreEqual("msg_stream", completed["output"][0].GetString("id"));
						}
					} finally{ finish.Set(); }
				}
				CheckChatCompletionsFallback(address, token);

				slot.ApiModel = "gpt-6.2-sol";
				slot.ApiReasoningEffort = 1;
				OnForm(() => ModelSlotManager.AddOrUpdate(slot));
				_upstream.Handle = context => {
					Assert.AreEqual("/gateway/openai/responses", context.Request.Url.AbsolutePath);
					var request = ReadRequest(context);
					Assert.AreEqual("gpt-6.2-sol", request.GetString("model"));
					Assert.AreEqual("low", request["reasoning"].GetString("effort"));
					Assert.IsFalse(request["temperature"].Exists);
					context.Response.StatusCode = 400;
					Reply(context, Json.Object(Json.P("error", Json.Object(Json.P("code", "invalid_value"), Json.P("message", "Invalid input")))));
				};
				Assert.AreEqual(HttpStatusCode.BadRequest,
					ReadApiError(() => PostApi(prefix, token, Json.P("input", "No fallback on rejected input"), Json.P("temperature", 0.4))));
				Assert.IsNull(_upstream.Error, _upstream.Error?.ToString());
			} finally{
				OnForm(() => {
					_form.ApiServer.Stop();
					ModelSlotManager.AddOrUpdate(original);
				});
			}
			CheckClientStreamFailures();
		}
		private static void CheckChatCompletionsFallback(string address, string token){
			var tool = Json.Object(Json.P("name", "lookup"), Json.P("strict", true),
				Json.P("parameters", Json.Object(Json.P("type", "object"), Json.P("properties", Json.Object()), Json.P("additionalProperties", false), Json.P("required", Json.Array()))));
			var calls = NativeChat.BuildChatCompletionToolCalls(new[]{
				new APIClient.ToolCall("call_a", "lookup", "{}"), new APIClient.ToolCall("call_b", "lookup", "{}")
			});
			var messages = Json.Array(Json.Object(Json.P("role", "user"), Json.P("content", "Look up both items.")),
				Json.Object(Json.P("role", "assistant"), Json.P("tool_calls", calls)),
				Json.Object(Json.P("role", "tool"), Json.P("tool_call_id", "call_a"), Json.P("content", "First result")),
				Json.Object(Json.P("role", "tool"), Json.P("tool_call_id", "call_b"), Json.P("content", "Second result")));
			var probes = 0;
			_upstream.Handle = context => {
				var request = ReadRequest(context);
				if(context.Request.Url.AbsolutePath == "/gateway/openai/responses"){
					Interlocked.Increment(ref probes);
					Assert.AreEqual(true, request["tools"][0].GetBool("strict"));
					Assert.AreEqual("lookup", request["tool_choice"].GetString("name"));
					context.Response.StatusCode = 404;
					return;
				}
				Assert.AreEqual("/gateway/openai/chat/completions", context.Request.Url.AbsolutePath);
				Assert.AreEqual("workflow-model", request.GetString("model"));
				CollectionAssert.AreEqual(new[]{ "user", "assistant", "tool", "tool" }, request["messages"].Select(item => item.GetString("role")).ToArray());
				Assert.AreEqual(2, request["messages"][1]["tool_calls"].Count);
				Assert.AreEqual(true, request["tools"][0]["function"].GetBool("strict"));
				Assert.AreEqual("lookup", request["tool_choice"]["function"].GetString("name"));
				Assert.AreEqual(false, request.GetBool("parallel_tool_calls"));
				Assert.AreEqual(true, request["stream_options"].GetBool("include_usage"));
				context.Response.ContentType = "text/event-stream";
				context.Response.SendChunked = true;
				WriteSse(context.Response, "{\"choices\":[{\"index\":0,\"delta\":{\"content\":\"Both items found\"},\"finish_reason\":null}]}");
				WriteSse(context.Response, "{\"choices\":[{\"index\":0,\"delta\":{},\"finish_reason\":\"stop\"}]}");
				WriteSse(context.Response, "{\"choices\":[],\"usage\":{\"prompt_tokens\":11,\"completion_tokens\":3,\"total_tokens\":14}}");
				WriteSse(context.Response, "[DONE]");
			};
			var body = Json.Object(Json.P("model", "lmstud/" + Slot), Json.P("messages", messages), Json.P("stream", true),
				Json.P("stream_options", Json.Object(Json.P("include_usage", true))), Json.P("parallel_tool_calls", false),
				Json.P("tools", Json.Array(Json.Object(Json.P("type", "function"), Json.P("function", tool)))),
				Json.P("tool_choice", Json.Object(Json.P("type", "function"), Json.P("function", Json.Object(Json.P("name", "lookup"))))));
			using(var response = ApiRequest(address + "/v1/chat/completions", token, body))
			using(var reader = new StreamReader(response.GetResponseStream())){
				StringAssert.StartsWith(response.ContentType, "text/event-stream");
				var stream = reader.ReadToEnd();
				StringAssert.Contains(stream, "Both items found");
				StringAssert.Contains(stream, "\"finish_reason\":\"stop\"");
				StringAssert.Contains(stream, "\"total_tokens\":14");
				StringAssert.Contains(stream, "data: [DONE]");
			}
			Assert.AreEqual(1, Volatile.Read(ref probes));
		}
		private static void CheckClientStreamFailures(){
			using(var release = new ManualResetEventSlim())
			using(var finished = new ManualResetEventSlim()){
				_upstream.Handle = context => {
					context.Response.ContentType = "text/event-stream";
					context.Response.SendChunked = true;
					WriteEvent(context.Response, "Pending text");
					release.Wait(TimeoutMs);
					finished.Set();
				};
				Send("Stop this stream");
				try{
					WaitFor(() => Generation.Generating && VisibleReply() == "Pending text", "stream text before Stop");
					OnForm(() => _form.butGen.PerformClick());
					WaitFor(() => ChatIsIdle() && !_form.ChatMessages.Last().NativeBacked, "Stop to release the blocked response");
					Assert.IsFalse(release.IsSet);
				} finally{
					release.Set();
					Assert.IsTrue(finished.Wait(TimeoutMs));
				}
			}
			_upstream.Handle = context => {
				context.Response.ContentType = "text/event-stream";
				context.Response.SendChunked = true;
				WriteEvent(context.Response, "Failed text");
				WriteSse(context.Response, "{\"type\":\"response.failed\",\"response\":{\"status\":\"failed\",\"error\":{\"message\":\"Workflow failure\"}}}");
			};
			Send("Fail this stream");
			WaitFor(() => ChatIsIdle() && VisibleReply() == "Failed text" && !_form.ChatMessages.Last().NativeBacked
				&& _form.ChatMessages.Last().labelRole.Text == Resources.Incomplete, "failed text to be marked incomplete and kept out of conversation history");
		}
		private static HttpWebResponse ApiRequest(string url, string token, JsonNode body = default(JsonNode)){
			var request = (HttpWebRequest)WebRequest.Create(url);
			request.Proxy = null;
			request.Timeout = request.ReadWriteTimeout = TimeoutMs;
			if(token != null) request.Headers[HttpRequestHeader.Authorization] = "Bearer " + token;
			if(!body.IsNull){
				request.Method = "POST";
				request.ContentType = "application/json";
				var data = Encoding.UTF8.GetBytes(body.ToJson());
				request.ContentLength = data.Length;
				using(var stream = request.GetRequestStream()) stream.Write(data, 0, data.Length);
			}
			return (HttpWebResponse)request.GetResponse();
		}
		private static JsonNode PostApi(string url, string token, params JsonMember[] fields){
			var payload = Json.ObjectBuilder(fields);
			payload.Set("model", "lmstud/" + Slot);
			using(var response = ApiRequest(url, token, payload.ToNode()))
			using(var reader = new StreamReader(response.GetResponseStream())){ return Json.Parse(reader.ReadToEnd()); }
		}
		private static HttpStatusCode ReadApiError(Action send){
			try{ send(); }
			catch(WebException ex) when(ex.Response is HttpWebResponse){
				using(var response = (HttpWebResponse)ex.Response){ return response.StatusCode; }
			}
			Assert.Fail("Expected an HTTP error.");
			return 0;
		}
		private static JsonNode ReadApiEvent(StreamReader reader, string type){
			string line;
			while((line = reader.ReadLine()) != null){
				if(!line.StartsWith("data: ", StringComparison.Ordinal)) continue;
				var item = Json.Parse(line.Substring(6));
				if(item.GetString("type") == type) return item;
			}
			Assert.Fail("The stream ended before " + type);
			return JsonNode.Missing;
		}
		private static T Control<T>(string name) where T : Control{return (T)_form.Controls.Find(name, true).Single();}
		private static T OnForm<T>(Func<T> action){
			Assert.IsNull(_uiError, _uiError?.ToString());
			var pending = _form.BeginInvoke(action);
			Assert.IsTrue(pending.AsyncWaitHandle.WaitOne(TimeoutMs), "The application window did not respond.");
			return (T)_form.EndInvoke(pending);
		}
		private static void OnForm(Action action){
			OnForm(() => {
				action();
				return true;
			});
		}
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
			using(var lease = ModelSlotManager.TryEnterSlot(Slot, 0)){ return lease != null; }
		}
		private static string VisibleReply(){return _form.ChatMessages.LastOrDefault()?.MarkdownView.PlainText;}
		private static void Send(string prompt){
			OnForm(() => {
				_form.textInput.Text = prompt;
				_form.butGen.PerformClick();
			});
		}
		private static void WaitForReply(string text){WaitFor(() => ChatIsIdle() && VisibleReply() == text, text);}
		private static JsonNode ReadRequest(HttpListenerContext context){
			using(var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding)){ return Json.Parse(reader.ReadToEnd()); }
		}
		private static JsonNode Message(string text){return Json.Object(Json.P("output", Json.Array(Json.Object(Json.P("type", "message"), Json.P("role", "assistant"), Json.P("content", Json.Array(Json.Object(Json.P("type", "output_text"), Json.P("text", text))))))));}
		private static JsonNode ToolCall(string id, string name, JsonNode arguments){return Json.Object(Json.P("output", Json.Array(Json.Object(Json.P("type", "function_call"), Json.P("call_id", id), Json.P("name", name), Json.P("arguments", arguments.ToJson())))));}
		private static void Reply(HttpListenerContext context, JsonNode body){Write(context, Encoding.UTF8.GetBytes(body.ToJson()), "application/json");}
		private static void Write(HttpListenerContext context, byte[] bytes, string type = "application/octet-stream"){
			context.Response.ContentType = type;
			context.Response.ContentLength64 = bytes.Length;
			context.Response.OutputStream.Write(bytes, 0, bytes.Length);
		}
		private static void WriteEvent(HttpListenerResponse response, string text){
			WriteSse(response, Json.Object(Json.P("type", "response.output_text.delta"), Json.P("delta", text)).ToJson());
		}
		private static void WriteSse(HttpListenerResponse response, string data){
			var bytes = Encoding.UTF8.GetBytes("data: " + data + "\n\n");
			response.OutputStream.Write(bytes, 0, bytes.Length);
			response.OutputStream.Flush();
		}
		private static void CloseDialog(){
			var dialog = FindDialog();
			if(dialog != IntPtr.Zero) PostMessage(dialog, 0x0010, IntPtr.Zero, IntPtr.Zero);
		}
		private static IntPtr FindDialog(){
			var dialog = IntPtr.Zero;
			EnumThreadWindows(GetCurrentThreadId(),
				(window, state) => {
					if(window == _form.Handle || GetDlgItem(window, 1) == IntPtr.Zero) return true;
					dialog = window;
					return false;
				},
				IntPtr.Zero);
			return dialog;
		}
		[DllImport("kernel32.dll")]
		private static extern uint GetCurrentThreadId();
		[DllImport("user32.dll")]
		private static extern bool EnumThreadWindows(uint threadId, EnumWindowCallback callback, IntPtr state);
		[DllImport("user32.dll")]
		private static extern IntPtr GetDlgItem(IntPtr window, int id);
		[DllImport("user32.dll")]
		private static extern bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
		private delegate bool EnumWindowCallback(IntPtr window, IntPtr state);
		private sealed class LoopbackServer : IDisposable{
			private readonly HttpListener _listener = new HttpListener();
			private readonly Thread _thread;
			internal readonly string Url = "http://127.0.0.1:" + FreePort();
			internal volatile Exception Error;
			internal volatile Action<HttpListenerContext> Handle;
			internal LoopbackServer(){
				_listener.Prefixes.Add(Url + "/");
				_listener.Start();
				_thread = new Thread(Listen){ IsBackground = true };
				_thread.Start();
			}
			public void Dispose(){
				_listener.Close();
				Assert.IsTrue(_thread.Join(TimeoutMs), "The loopback server did not stop.");
			}
			private void Listen(){
				while(_listener.IsListening){
					HttpListenerContext context;
					try{ context = _listener.GetContext(); } catch(HttpListenerException){ return; } catch(ObjectDisposedException){ return; }
					try{ Handle(context); } catch(Exception ex){
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
		}
	}
}
