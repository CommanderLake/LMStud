using System;
using System.Collections.Generic;
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
	}
}