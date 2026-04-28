using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
namespace LMStud{
	internal sealed class ResponsesApiResponseParser : IAPIResponseFormatParser{
		public bool TryParse(JObject root, string responseId, out APIClient.ChatCompletionResult result){
			result = null;
			if(!(root["output"] is JArray output)) return false;
			var sanitizedOutput = APIResponseParserCommon.SanitizeOutputItems(output);
			string content = null;
			string reasoning = null;
			var toolCalls = new List<APIClient.ToolCall>();
			foreach(var item in output.OfType<JObject>()){
				var type = item.Value<string>("type");
				if(string.Equals(type, "message", StringComparison.OrdinalIgnoreCase)){
					var role = item.Value<string>("role");
					if(!string.Equals(role, "assistant", StringComparison.OrdinalIgnoreCase)) continue;
					var messageContent = APIResponseParserCommon.ExtractContentText(item["content"]);
					if(!string.IsNullOrWhiteSpace(messageContent)) content = messageContent;
					var messageReasoning = APIResponseParserCommon.ExtractReasoningTextFromContent(item["content"]);
					if(!string.IsNullOrWhiteSpace(messageReasoning)) reasoning = messageReasoning;
					var messageToolCalls = APIResponseParserCommon.ParseToolCalls(item["tool_calls"]);
					if(messageToolCalls != null) toolCalls.AddRange(messageToolCalls);
					var contentToolCalls = APIResponseParserCommon.ParseToolCallsFromContent(item["content"]);
					if(contentToolCalls != null) toolCalls.AddRange(contentToolCalls);
					continue;
				}
				if(string.Equals(type, "output_text", StringComparison.OrdinalIgnoreCase)){
					var messageContent = item.Value<string>("text") ?? item.Value<string>("content");
					if(!string.IsNullOrWhiteSpace(messageContent)) content = messageContent;
					continue;
				}
				if(string.Equals(type, "reasoning", StringComparison.OrdinalIgnoreCase)){
					var reasoningContent = APIResponseParserCommon.ExtractReasoningText(item);
					if(!string.IsNullOrWhiteSpace(reasoningContent)) reasoning = reasoningContent;
					continue;
				}
				if(string.Equals(type, "tool_call", StringComparison.OrdinalIgnoreCase) || string.Equals(type, "function_call", StringComparison.OrdinalIgnoreCase)){
					var toolCall = APIResponseParserCommon.ParseToolCallItem(item);
					if(toolCall != null) toolCalls.Add(toolCall);
				}
			}
			var finalToolCalls = toolCalls.Count > 0 ? toolCalls : null;
			if(string.IsNullOrWhiteSpace(content) && finalToolCalls == null){
				var outputText = APIResponseParserCommon.ExtractContentText(root["output_text"]);
				if(!string.IsNullOrWhiteSpace(outputText)) content = outputText;
			}
			if(string.IsNullOrWhiteSpace(reasoning)){
				var outputReasoning = APIResponseParserCommon.ExtractReasoningText(root["reasoning"] as JObject) ?? APIResponseParserCommon.ExtractContentText(root["reasoning"]);
				if(!string.IsNullOrWhiteSpace(outputReasoning)) reasoning = outputReasoning;
			}
			result = new APIClient.ChatCompletionResult(content ?? "", reasoning, finalToolCalls, responseId, sanitizedOutput);
			return true;
		}
	}
}