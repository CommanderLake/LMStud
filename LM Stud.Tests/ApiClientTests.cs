using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using LMStud;
using LMStud.Parsers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
namespace LM_Stud.Tests{
	[TestClass]
	public class ApiClientTests{
		private static Form1 _form;
		private static int _originalReasoningEffort;
		private static int _originalReasoningSummary;
		private static bool _ownsForm;
		[ClassInitialize]
		public static void ClassInitialize(TestContext context){
			if(Program.MainForm == null || Program.MainForm.IsDisposed){
				_ownsForm = true;
				Exception startupError = null;
				var t = new Thread(() => {
					try{ Program.Main(); }
					catch(Exception ex){ startupError = ex; }
				});
				t.SetApartmentState(ApartmentState.STA);
				t.IsBackground = true;
				t.Start();
				var deadline = DateTime.UtcNow.AddSeconds(15);
				while(Program.MainForm == null || Program.MainForm.IsDisposed){
					if(startupError != null) throw startupError;
					if(DateTime.UtcNow > deadline) Assert.Fail("Program.Main did not create MainForm in time.");
					Thread.Sleep(10);
				}
			}
			_form = Program.MainForm;
			Thread.Sleep(1000);
			_originalReasoningEffort = Common.APIClientReasoningEffort;
			_originalReasoningSummary = Common.APIClientReasoningSummary;
		}
		[ClassCleanup]
		public static void ClassCleanup(){
			Common.APIClientReasoningEffort = _originalReasoningEffort;
			Common.APIClientReasoningSummary = _originalReasoningSummary;
			if(!_ownsForm || _form == null) return;
			try{
				if(!_form.IsDisposed && _form.IsHandleCreated)
					_form.Invoke(new MethodInvoker(() => {
						Program.MainForm?.Close();
						Program.MainForm?.Dispose();
						Program.MainForm = null;
					}));
			} catch(ObjectDisposedException){} catch(InvalidOperationException){} catch(NullReferenceException){} finally{
				if(ReferenceEquals(Program.MainForm, _form)) Program.MainForm = null;
			}
		}
		[TestCleanup]
		public void TestCleanup(){
			Common.APIClientReasoningEffort = _originalReasoningEffort;
			Common.APIClientReasoningSummary = _originalReasoningSummary;
		}
		[TestMethod]
		public void BuildInputItems_WithToolCall_AddsFunctionCallItem(){
			var messages = new List<APIClient.ChatMessage>{
				new APIClient.ChatMessage("assistant", ""){
					ToolCalls = new List<APIClient.ToolCall>{ new APIClient.ToolCall("call_1", "lookup", "{\"term\":\"weather\"}") }
				}
			};
			var items = APIClient.BuildInputItems(messages);
			Assert.AreEqual(1, items.Count, "Assistant tool calls without text should emit just the Responses function call item.");
			Assert.AreEqual("function_call", (string)items[0]["type"], "Tool call should use the Responses function call item shape.");
			Assert.AreEqual("call_1", (string)items[0]["call_id"], "Tool call id should be preserved.");
		}
		[TestMethod]
		public void AppendOutputItems_WithAssistantMessage_NormalizesForResponsesInput(){
			var history = new JArray();
			var output = new JArray(new JObject{
				["type"] = "message", ["status"] = "completed", ["role"] = "assistant",
				["content"] = new JArray(new JObject{ ["type"] = "output_text", ["text"] = "hello" })
			});
			var result = new APIClient.ChatCompletionResult("hello", null, null, "resp_1", output);
			APIClient.AppendOutputItems(history, result);
			Assert.AreEqual(1, history.Count, "Assistant output message should become one input message.");
			Assert.AreEqual("assistant", (string)history[0]["role"], "Assistant role should be preserved.");
			Assert.AreEqual("hello", (string)history[0]["content"], "Output text should be replayed as input content.");
		}
		[TestMethod]
		public void AppendOutputItems_WithToolCallsWithoutOutputItems_ReplaysToolCall(){
			var history = new JArray();
			var toolCalls = new List<APIClient.ToolCall>{ new APIClient.ToolCall("call_1", "lookup", "{\"term\":\"weather\"}") };
			var result = new APIClient.ChatCompletionResult("", null, toolCalls, "chatcmpl_1", null);
			APIClient.AppendOutputItems(history, result);
			Assert.AreEqual(1, history.Count, "Fallback parsers should still replay assistant tool calls.");
			Assert.AreEqual("function_call", (string)history[0]["type"], "Tool call should be replayed in Responses input shape.");
			Assert.AreEqual("call_1", (string)history[0]["call_id"], "Tool call id should be preserved for the following tool output.");
			Assert.AreEqual("lookup", (string)history[0]["name"], "Tool name should be preserved.");
		}
		[TestMethod]
		public void ParseResponseBody_WithResponsesUsage_ReadsTotalTokens(){
			var json = @"{
				""id"": ""resp_test"",
				""object"": ""response"",
				""output"": [
					{ ""type"": ""message"", ""role"": ""assistant"", ""content"": [
						{ ""type"": ""output_text"", ""text"": ""ok"" }
					] }
				],
				""usage"": { ""input_tokens"": 3, ""output_tokens"": 2, ""total_tokens"": 5 }
			}";
			var result = APIResponseParser.ParseResponseBody(json);
			Assert.AreEqual("ok", result.Content, "Responses output text should parse.");
			Assert.AreEqual(5, result.TotalTokens, "usage.total_tokens should be surfaced.");
		}
		[TestMethod]
		public void BuildInputMessagePayload_WithToolRole_RequiresCallId(){
			var toolMessage = new APIClient.ChatMessage("tool", "ok");
			Assert.ThrowsExactly<InvalidOperationException>(() => APIClient.BuildInputMessagePayload(toolMessage));
			toolMessage.ToolCallId = "call_123";
			var payload = (JObject)APIClient.BuildInputMessagePayload(toolMessage);
			Assert.AreEqual("function_call_output", (string)payload["type"], "Tool message should map to function call output.");
			Assert.AreEqual("call_123", (string)payload["call_id"], "Call id should be included for tool messages.");
		}
		[TestMethod]
		public void CreateChatCompletion_WithReasoning_AddsReasoningToResponsesPayload(){
			using(var listener = new HttpListener()){
				var baseUrl = "http://127.0.0.1:39593/";
				string requestBody = null;
				Common.APIClientReasoningEffort = 2;
				listener.Prefixes.Add(baseUrl);
				listener.Start();
				var server = Task.Run(() => {
					var ctx = listener.GetContext();
					using(var reader = new StreamReader(ctx.Request.InputStream, ctx.Request.ContentEncoding)){ requestBody = reader.ReadToEnd(); }
					ctx.Response.StatusCode = 200;
					ctx.Response.ContentType = "application/json";
					using(var writer = new StreamWriter(ctx.Response.OutputStream, Encoding.UTF8, 1024, true))
						writer.Write("{\"id\":\"resp_test\",\"output\":[{\"type\":\"message\",\"role\":\"assistant\",\"content\":[{\"type\":\"output_text\",\"text\":\"ok\"}]}]}");
					ctx.Response.Close();
				});
				var client = new APIClient(baseUrl, "", "test-model", false);
				var history = new JArray{ new JObject{ ["role"] = "user", ["content"] = "hello" } };
				var result = client.CreateChatCompletion(history, 0.5f, 128, null, null, CancellationToken.None);
				Assert.AreEqual("ok", result.Content, "Responses result should parse.");
				Assert.IsTrue(server.Wait(1000), "Test server should finish handling the request.");
				var payload = JObject.Parse(requestBody);
				Assert.AreEqual("low", (string)payload["reasoning"]?["effort"], "Reasoning effort should be forwarded to the Responses payload.");
			}
		}
		[TestMethod]
		[DataRow(39591, 404, "{\"error\":\"missing\"}", DisplayName = "404 endpoint missing")]
		[DataRow(39592, 400, "{\"error\":\"unsupported parameter: parallel_tool_calls\"}", DisplayName = "400 unsupported parameter")]
		public void CreateChatCompletion_FallsBackToChatCompletions(int port, int responsesStatusCode, string responsesBody){
			using(var listener = new HttpListener()){
				var baseUrl = $"http://127.0.0.1:{port}/";
				listener.Prefixes.Add(baseUrl);
				listener.Start();
				var server = Task.Run(() => {
					var responsesCtx = listener.GetContext();
					responsesCtx.Response.StatusCode = responsesStatusCode;
					using(var writer = new StreamWriter(responsesCtx.Response.OutputStream, Encoding.UTF8, 1024, true)) writer.Write(responsesBody);
					responsesCtx.Response.Close();
					var chatCtx = listener.GetContext();
					chatCtx.Response.StatusCode = 200;
					chatCtx.Response.ContentType = "application/json";
					using(var writer = new StreamWriter(chatCtx.Response.OutputStream, Encoding.UTF8, 1024, true))
						writer.Write("{\"id\":\"chatcmpl_test\",\"choices\":[{\"message\":{\"role\":\"assistant\",\"content\":\"fallback ok\"}}]}");
					chatCtx.Response.Close();
				});
				var client = new APIClient(baseUrl, "", "test-model", false);
				var history = new JArray{ new JObject{ ["role"] = "user", ["content"] = "hello" } };
				var result = client.CreateChatCompletion(history, 0.5f, 128, "[]", null, CancellationToken.None);
				Assert.AreEqual("fallback ok", result.Content, "Client should use chat completions as a fallback.");
				Assert.IsTrue(server.Wait(1000), "Test server should finish handling both requests.");
			}
		}

	}
}
