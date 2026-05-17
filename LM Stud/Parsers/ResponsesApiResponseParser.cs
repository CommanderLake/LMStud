using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
namespace LMStud.Parsers{
	internal sealed class ResponsesApiResponseParser : IAPIResponseFormatParser{
		public bool TryParse(JsonNode root, string responseId, out APIClient.ChatCompletionResult result){
			result = null;
			var output = root["output"];
			if(!output.IsArray) return false;
			var sanitizedOutput = APIResponseParserCommon.SanitizeOutputItems(output);
			var content = new StringBuilder();
			var reasoning = new StringBuilder();
			var toolCalls = new List<APIClient.ToolCall>();
			foreach(var item in output.Where(item => item.IsObject)){
				var type = item.GetString("type");
				if(string.Equals(type, "message", StringComparison.OrdinalIgnoreCase)){
					var role = item.GetString("role");
					if(!string.Equals(role, "assistant", StringComparison.OrdinalIgnoreCase)) continue;
					var messageContent = APIResponseParserCommon.ExtractContentText(item["content"]);
					APIResponseParserCommon.AppendIfNotBlank(content, messageContent);
					var messageReasoning = APIResponseParserCommon.ExtractReasoningTextFromContent(item["content"]);
					APIResponseParserCommon.AppendIfNotBlank(reasoning, messageReasoning);
					var messageToolCalls = APIResponseParserCommon.ParseToolCalls(item["tool_calls"]);
					if(messageToolCalls != null) toolCalls.AddRange(messageToolCalls);
					var contentToolCalls = APIResponseParserCommon.ParseToolCallsFromContent(item["content"]);
					if(contentToolCalls != null) toolCalls.AddRange(contentToolCalls);
					continue;
				}
				if(string.Equals(type, "output_text", StringComparison.OrdinalIgnoreCase)){
					var messageContent = item.GetString("text") ?? item.GetString("content");
					APIResponseParserCommon.AppendIfNotBlank(content, messageContent);
					continue;
				}
				if(string.Equals(type, "reasoning", StringComparison.OrdinalIgnoreCase)){
					var reasoningContent = APIResponseParserCommon.ExtractReasoningText(item);
					APIResponseParserCommon.AppendIfNotBlank(reasoning, reasoningContent);
					continue;
				}
				if(string.Equals(type, "tool_call", StringComparison.OrdinalIgnoreCase) || string.Equals(type, "function_call", StringComparison.OrdinalIgnoreCase)){
					var toolCall = APIResponseParserCommon.ParseToolCallItem(item);
					if(toolCall != null) toolCalls.Add(toolCall);
				}
			}
			var finalToolCalls = toolCalls.Count > 0 ? toolCalls : null;
			var finalContent = APIResponseParserCommon.ToStringOrNull(content);
			var finalReasoning = APIResponseParserCommon.ToStringOrNull(reasoning);
			if(string.IsNullOrWhiteSpace(finalContent) && finalToolCalls == null){
				var outputText = APIResponseParserCommon.ExtractContentText(root["output_text"]);
				if(!string.IsNullOrWhiteSpace(outputText)) finalContent = outputText;
			}
			if(string.IsNullOrWhiteSpace(finalReasoning)){
				var outputReasoning = APIResponseParserCommon.ExtractReasoningText(root["reasoning"]) ?? APIResponseParserCommon.ExtractContentText(root["reasoning"]);
				if(!string.IsNullOrWhiteSpace(outputReasoning)) finalReasoning = outputReasoning;
			}
			if(!APIResponseParserCommon.HasResultContent(finalContent, finalReasoning, finalToolCalls)) return false;
			result = new APIClient.ChatCompletionResult(finalContent ?? "", finalReasoning, finalToolCalls, responseId, sanitizedOutput, APIResponseParserCommon.ExtractTotalTokens(root));
			return true;
		}
	}
}
