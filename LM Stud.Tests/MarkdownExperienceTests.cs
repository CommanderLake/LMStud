using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using LMStud;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LM_Stud.Tests{
	[TestClass]
	public class MarkdownExperienceTests{
		[TestMethod]
		public void CommonMarkdownContentRendersAsReadableText(){
			const string markdown = "# Heading\n\n- [x] task\n- item with **bold**, *italic*, ~~strike~~, and `code`\n\n" +
				"| Name | Value |\n| --- | ---: |\n| A | 1 |\n\n> quote\n\n```csharp\nvar value = 1;\n```";
			using(var renderer = new GdiMarkdownRenderer()){
				renderer.Layout(markdown, Control.DefaultFont, 500, Color.Black, Color.White);
				Assert.IsTrue(renderer.TotalHeight > 0);
				StringAssert.Contains(renderer.PlainText, "Heading");
				StringAssert.Contains(renderer.PlainText, "[x] task");
				StringAssert.Contains(renderer.PlainText, "bold");
				StringAssert.Contains(renderer.PlainText, "Name");
				StringAssert.Contains(renderer.PlainText, "var value = 1;");
			}
		}

		[TestMethod]
		public void RenderedResponseCanBeSelectedWithSelectAll(){
			using(var control = new MarkdownRenderControl{Size = new Size(500, 100)}){
				control.MarkdownText = "**Bold** and selectable";
				var onKeyDown = typeof(MarkdownRenderControl).GetMethod("OnKeyDown",
					BindingFlags.Instance | BindingFlags.NonPublic);
				onKeyDown.Invoke(control, new object[]{new KeyEventArgs(Keys.Control | Keys.A)});
				Assert.AreEqual("Bold and selectable", control.PlainText);
				Assert.AreEqual(control.PlainText.Length, control.SelectionLength);
			}
		}

		[TestMethod]
		public void VeryLongMarkdownMessagesAreSplitIntoRenderableChunks(){
			var markdown = new StringBuilder("```text\n");
			for(var i = 0; i < 2500; i++) markdown.Append("line ").Append(i).Append('\n');
			markdown.Append("```");

			var chunks = MarkdownMessageChunker.Split(markdown.ToString());
			Assert.IsTrue(chunks.Count > 1);
			foreach(var chunk in chunks)
				using(var renderer = new GdiMarkdownRenderer()){
					renderer.Layout(chunk, Control.DefaultFont, 500, Color.Black, Color.White);
					Assert.IsTrue(renderer.TotalHeight > 0);
					Assert.IsTrue(renderer.TotalHeight + 8 <= MarkdownMessageChunker.MaximumRenderedContentHeight);
				}
		}

		[TestMethod]
		public void AttachedLocalImageIsDeliveredToLocalAndRemoteBackends(){
			var path = Path.Combine(Path.GetTempPath(), "lmstud-vision-" + Guid.NewGuid().ToString("N") + ".png");
			try{
				using(var bitmap = new Bitmap(2, 2)) bitmap.Save(path);
				var markdown = "Describe this image.\n\n![sample](<" + new Uri(path).AbsoluteUri + ">)";

				var responsesPayload = APIClient.BuildInputMessagePayload(new APIClient.ChatMessage("user", markdown));
				var responsesContent = responsesPayload["content"];
				Assert.AreEqual("input_text", responsesContent[0].GetString("type"));
				Assert.AreEqual("input_image", responsesContent[1].GetString("type"));
				StringAssert.StartsWith(responsesContent[1].GetString("image_url"), "data:image/png;base64,");

				var history = Json.ArrayBuilder();
				history.Add(responsesPayload);
				var chatContent = APIClient.ConvertHistoryToChatCompletionMessages(history).Single()["content"];
				Assert.AreEqual("text", chatContent[0].GetString("type"));
				Assert.AreEqual("image_url", chatContent[1].GetString("type"));

				var nativeContent = Json.Parse(MarkdownImages.BuildNativeContentJson(markdown, out var hasImages));
				Assert.IsTrue(hasImages);
				Assert.AreEqual("text", nativeContent[0].GetString("type"));
				Assert.AreEqual("image_url", nativeContent[1].GetString("type"));
			} finally{
				if(File.Exists(path)) File.Delete(path);
			}
		}

		[TestMethod]
		public void NativeChatSnapshotPreservesToolCallAndReasoningShape(){
			var messagesJson = NativeChat.BuildMessagesJson(new[]{
				new NativeChat.MessageSnapshot(MessageRole.User, "", "Question"),
				new NativeChat.MessageSnapshot(MessageRole.Assistant, "thinking", "", apiToolCalls: new List<APIClient.ToolCall>{new APIClient.ToolCall("call_1", "lookup", "{\"q\":\"x\"}")}),
				new NativeChat.MessageSnapshot(MessageRole.Tool, "", "{\"ok\":true}", apiToolCallId: "call_1")
			});

			var messages = Json.Parse(messagesJson);
			Assert.AreEqual(3, messages.Count);
			Assert.AreEqual("user", messages[0].GetString("role"));
			Assert.AreEqual("Question", messages[0].GetString("content"));
			Assert.AreEqual("assistant", messages[1].GetString("role"));
			Assert.AreEqual("thinking", messages[1].GetString("reasoning_content"));
			Assert.AreEqual("call_1", messages[1]["tool_calls"][0].GetString("id"));
			Assert.AreEqual("lookup", messages[1]["tool_calls"][0]["function"].GetString("name"));
			Assert.AreEqual("{\"q\":\"x\"}", messages[1]["tool_calls"][0]["function"].GetString("arguments"));
			Assert.AreEqual("tool", messages[2].GetString("role"));
			Assert.AreEqual("call_1", messages[2].GetString("tool_call_id"));
		}
	}
}
