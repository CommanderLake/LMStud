using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
namespace LMStud{
	internal static class APIResponseParserCommon{
		internal static JArray SanitizeOutputItems(JArray output){
			if(output == null) return null;
			var sanitized = new JArray();
			foreach(var item in output){
				var clone = item?.DeepClone();
				RemoveIdFields(clone);
				sanitized.Add(clone);
			}
			return sanitized;
		}
		internal static string ExtractContentText(JToken contentToken){
			if(contentToken == null || contentToken.Type == JTokenType.Null) return null;
			if(contentToken.Type == JTokenType.String) return contentToken.ToString();
			if(contentToken.Type == JTokenType.Array){
				var sb = new StringBuilder();
				foreach(var item in contentToken){
					if(item == null || item.Type == JTokenType.Null) continue;
					string text = null;
					if(item.Type == JTokenType.String){ text = item.ToString(); }
					else if(item is JObject obj){
						text = obj.Value<string>("text") ?? obj.Value<string>("content");
						if(string.IsNullOrWhiteSpace(text) && obj["parts"] is JArray parts) text = ExtractContentText(parts);
					}
					if(string.IsNullOrWhiteSpace(text)) continue;
					sb.Append(text);
				}
				return sb.Length > 0 ? sb.ToString() : null;
			}
			if(contentToken is JObject contentObj){
				var text = contentObj.Value<string>("text") ?? contentObj.Value<string>("content");
				if(string.IsNullOrWhiteSpace(text) && contentObj["parts"] is JArray parts) text = ExtractContentText(parts);
				return !string.IsNullOrWhiteSpace(text) ? text : null;
			}
			return null;
		}
		internal static string ExtractReasoningText(JToken reasoningItem){
			if(reasoningItem == null || reasoningItem.Type == JTokenType.Null) return null;
			if(reasoningItem is JObject reasoningObj){
				var summaryText = ExtractContentText(reasoningObj["summary"]);
				if(!string.IsNullOrWhiteSpace(summaryText)) return summaryText;
				var textTok = reasoningObj["text"];
				if(textTok != null && textTok.Type == JTokenType.String) return (string)textTok;
				return ExtractContentText(reasoningObj["content"]);
			}
			return ExtractContentText(reasoningItem);
		}
		internal static string ExtractReasoningTextFromContent(JToken contentToken){
			if(!(contentToken is JArray contentArray)) return null;
			var sb = new StringBuilder();
			foreach(var item in contentArray.OfType<JObject>()){
				var type = item.Value<string>("type");
				if(!string.Equals(type, "reasoning", StringComparison.OrdinalIgnoreCase) && !string.Equals(type, "thinking", StringComparison.OrdinalIgnoreCase) &&
					!string.Equals(type, "redacted_thinking", StringComparison.OrdinalIgnoreCase)) continue;
				var reasoningText = item.Value<string>("text") ?? item.Value<string>("content") ?? item.Value<string>("summary");
				if(!string.IsNullOrWhiteSpace(reasoningText)) sb.Append(reasoningText);
			}
			return sb.Length > 0 ? sb.ToString() : null;
		}
		internal static APIClient.ToolCall ParseToolCallItem(JObject toolCallObj){
			var name = toolCallObj.Value<string>("name") ?? toolCallObj["function"]?.Value<string>("name");
			if(string.IsNullOrWhiteSpace(name)) return null;
			var callId = toolCallObj.Value<string>("call_id") ?? toolCallObj.Value<string>("id");
			if(string.IsNullOrWhiteSpace(callId)) return null;
			var argumentsToken = toolCallObj["arguments"] ?? toolCallObj["function"]?["arguments"] ?? toolCallObj["input"];
			var arguments = argumentsToken == null ? "" : argumentsToken.Type == JTokenType.String ? argumentsToken.ToString() : argumentsToken.ToString(Formatting.None);
			return new APIClient.ToolCall(callId, name, arguments);
		}
		internal static List<APIClient.ToolCall> ParseToolCallsFromContent(JToken contentToken){
			if(!(contentToken is JArray contentArray)) return null;
			var toolCalls = new List<APIClient.ToolCall>();
			foreach(var item in contentArray.OfType<JObject>()){
				var type = item.Value<string>("type");
				if(!string.Equals(type, "tool_call", StringComparison.OrdinalIgnoreCase) && !string.Equals(type, "function_call", StringComparison.OrdinalIgnoreCase) &&
					!string.Equals(type, "tool_use", StringComparison.OrdinalIgnoreCase)) continue;
				var toolCall = ParseToolCallItem(item);
				if(toolCall != null) toolCalls.Add(toolCall);
			}
			return toolCalls.Count > 0 ? toolCalls : null;
		}
		internal static List<APIClient.ToolCall> ParseToolCalls(JToken toolCallsToken){
			if(!(toolCallsToken is JArray toolCallsArray)) return null;
			var toolCalls = new List<APIClient.ToolCall>();
			foreach(var toolCallToken in toolCallsArray){
				if(!(toolCallToken is JObject toolCallObj)) continue;
				var toolCall = ParseToolCallItem(toolCallObj);
				if(toolCall != null) toolCalls.Add(toolCall);
			}
			return toolCalls.Count > 0 ? toolCalls : null;
		}
		private static void RemoveIdFields(JToken token){
			if(token == null || token.Type == JTokenType.Null) return;
			if(token is JObject obj){
				obj.Remove("id");
				foreach(var property in obj.Properties().ToList()) RemoveIdFields(property.Value);
				return;
			}
			if(token is JArray array)
				foreach(var item in array)
					RemoveIdFields(item);
		}
	}
}