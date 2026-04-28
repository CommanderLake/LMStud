using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LMStud;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
namespace LM_Stud.Tests{
	[TestClass]
	public class ApiClientTests{
		[TestMethod]
		public void BuildInputItems_WithToolCall_AddsFunctionCallItem(){
			var messages = new List<APIClient.ChatMessage>{
				new APIClient.ChatMessage("assistant", ""){
					ToolCalls = new List<APIClient.ToolCall>{ new APIClient.ToolCall("call_1", "lookup", "{\"term\":\"weather\"}") }
				}
			};
			var items = APIClient.BuildInputItems(messages);
			Assert.AreEqual(2, items.Count, "Message and tool call items should be emitted.");
			Assert.AreEqual("assistant", (string)items[0]["role"], "First item should be the assistant message payload.");
			Assert.AreEqual("function_call", (string)items[1]["type"], "Second item should be a function call.");
			Assert.AreEqual("call_1", (string)items[1]["call_id"], "Tool call id should be preserved.");
		}
		[TestMethod]
		public void BuildInputMessagePayload_WithToolRole_RequiresCallId(){
			var toolMessage = new APIClient.ChatMessage("tool", "ok");
			Assert.ThrowsException<InvalidOperationException>(() => APIClient.BuildInputMessagePayload(toolMessage));
			toolMessage.ToolCallId = "call_123";
			var payload = (JObject)APIClient.BuildInputMessagePayload(toolMessage);
			Assert.AreEqual("function_call_output", (string)payload["type"], "Tool message should map to function call output.");
			Assert.AreEqual("call_123", (string)payload["call_id"], "Call id should be included for tool messages.");
		}
		[TestMethod]
		public void CreateChatCompletion_WhenResponsesEndpointFails_FallsBackToChatCompletions(){
			using(var listener = new HttpListener()){
				var baseUrl = "http://127.0.0.1:39591/";
				listener.Prefixes.Add(baseUrl);
				listener.Start();
				var server = Task.Run(() => {
					var responsesCtx = listener.GetContext();
					responsesCtx.Response.StatusCode = 404;
					using(var writer = new StreamWriter(responsesCtx.Response.OutputStream, Encoding.UTF8, 1024, true)) writer.Write("{\"error\":\"missing\"}");
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
				var result = client.CreateChatCompletion(history, 0.2f, 128, null, null, CancellationToken.None);
				Assert.AreEqual("fallback ok", result.Content, "Client should use chat completions as a fallback.");
				Assert.IsTrue(server.Wait(1000), "Test server should finish handling both requests.");
			}
		}
	}
}