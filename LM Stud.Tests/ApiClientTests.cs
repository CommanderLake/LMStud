using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using LMStud;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
namespace LM_Stud.Tests{
	[TestClass]
	public class ApiClientTests{
		[TestMethod]
		public void GetHttpClient_DefaultsToInfiniteTimeSpan(){
			var field = typeof(ApiClient).GetField("_httpClient", BindingFlags.NonPublic | BindingFlags.Static);
			Assert.IsNotNull(field, "Expected _httpClient field to exist.");
			var previousClient = (System.Net.Http.HttpClient)field.GetValue(null);
			try{
				field.SetValue(null, null);
				var getHttpClientMethod = typeof(ApiClient).GetMethod("GetHttpClient", BindingFlags.NonPublic | BindingFlags.Static);
				Assert.IsNotNull(getHttpClientMethod, "Expected GetHttpClient method to exist.");
				var client = (System.Net.Http.HttpClient)getHttpClientMethod.Invoke(null, null);
				Assert.AreEqual(Timeout.InfiniteTimeSpan, client.Timeout, "Lazy HttpClient should default to infinite timeout.");
			} finally{
				field.SetValue(null, previousClient);
			}
		}
		[TestMethod]
		public void BuildInputItems_WithToolCall_AddsFunctionCallItem(){
			var messages = new List<ApiClient.ChatMessage>{
				new ApiClient.ChatMessage("assistant", ""){
					ToolCalls = new List<ApiClient.ToolCall>{ new ApiClient.ToolCall("call_1", "lookup", "{\"term\":\"weather\"}") }
				}
			};
			var items = ApiClient.BuildInputItems(messages);
			Assert.AreEqual(2, items.Count, "Message and tool call items should be emitted.");
			Assert.AreEqual("assistant", (string)items[0]["role"], "First item should be the assistant message payload.");
			Assert.AreEqual("function_call", (string)items[1]["type"], "Second item should be a function call.");
			Assert.AreEqual("call_1", (string)items[1]["call_id"], "Tool call id should be preserved.");
		}
		[TestMethod]
		public void BuildInputMessagePayload_WithToolRole_RequiresCallId(){
			var toolMessage = new ApiClient.ChatMessage("tool", "ok");
			Assert.ThrowsException<InvalidOperationException>(() => ApiClient.BuildInputMessagePayload(toolMessage));
			toolMessage.ToolCallId = "call_123";
			var payload = (JObject)ApiClient.BuildInputMessagePayload(toolMessage);
			Assert.AreEqual("function_call_output", (string)payload["type"], "Tool message should map to function call output.");
			Assert.AreEqual("call_123", (string)payload["call_id"], "Call id should be included for tool messages.");
		}
	}
}