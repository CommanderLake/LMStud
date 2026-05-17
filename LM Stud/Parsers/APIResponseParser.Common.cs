using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
namespace LMStud.Parsers{
	internal static class APIResponseParserCommon{
		internal static JsonNode SanitizeOutputItems(JsonNode output){
			if(!output.IsArray) return JsonNode.Missing;
			var sanitized = Json.ArrayBuilder();
			foreach(var item in output){
				sanitized.Add(RemoveIdFields(item));
			}
			return sanitized.ToNode();
		}
		internal static int? ExtractTotalTokens(JsonNode root){
			return root["usage"]["total_tokens"].AsInt();
		}
		internal static void AppendIfNotBlank(StringBuilder sb, string text){
			if(!string.IsNullOrWhiteSpace(text)) sb.Append(text);
		}
		internal static string ToStringOrNull(StringBuilder sb){
			return sb.Length > 0 ? sb.ToString() : null;
		}
		internal static bool HasResultContent(string content, string reasoning, List<APIClient.ToolCall> toolCalls){
			return !string.IsNullOrWhiteSpace(content) || !string.IsNullOrWhiteSpace(reasoning) || (toolCalls != null && toolCalls.Count > 0);
		}
		internal static string ExtractContentText(JsonNode contentToken){
			if(contentToken.IsNull) return null;
			if(contentToken.IsString) return contentToken.AsString();
			if(contentToken.IsArray){
				var sb = new StringBuilder();
				foreach(var item in contentToken){
					if(item.IsNull) continue;
					string text = null;
					if(item.IsString){ text = item.AsString(); }
					else if(item.IsObject){
						var obj = item;
						if(IsNonTextContentItem(obj)) continue;
						text = obj.GetString("text") ?? obj.GetString("content");
						if(string.IsNullOrWhiteSpace(text) && obj["parts"].IsArray) text = ExtractContentText(obj["parts"]);
					}
					if(string.IsNullOrWhiteSpace(text)) continue;
					sb.Append(text);
				}
				return sb.Length > 0 ? sb.ToString() : null;
			}
			if(contentToken.IsObject){
				var contentObj = contentToken;
				if(IsNonTextContentItem(contentObj)) return null;
				var text = contentObj.GetString("text") ?? contentObj.GetString("content");
				if(string.IsNullOrWhiteSpace(text) && contentObj["parts"].IsArray) text = ExtractContentText(contentObj["parts"]);
				return !string.IsNullOrWhiteSpace(text) ? text : null;
			}
			return null;
		}
		internal static string ExtractReasoningText(JsonNode reasoningItem){
			if(reasoningItem.IsNull) return null;
			if(reasoningItem.IsObject){
				var reasoningObj = reasoningItem;
				var summaryText = ExtractContentText(reasoningObj["summary"]);
				if(!string.IsNullOrWhiteSpace(summaryText)) return summaryText;
				var textTok = reasoningObj["text"];
				if(textTok.IsString) return textTok.AsString();
				return ExtractContentText(reasoningObj["content"]);
			}
			return ExtractContentText(reasoningItem);
		}
		internal static string ExtractReasoningTextFromContent(JsonNode contentToken){
			if(!contentToken.IsArray) return null;
			var sb = new StringBuilder();
			foreach(var item in contentToken.Where(item => item.IsObject)){
				var type = item.GetString("type");
				if(!string.Equals(type, "reasoning", StringComparison.OrdinalIgnoreCase) && !string.Equals(type, "thinking", StringComparison.OrdinalIgnoreCase) &&
					!string.Equals(type, "redacted_thinking", StringComparison.OrdinalIgnoreCase)) continue;
				var reasoningText = item.GetString("text") ?? item.GetString("content") ?? item.GetString("summary");
				if(!string.IsNullOrWhiteSpace(reasoningText)) sb.Append(reasoningText);
			}
			return sb.Length > 0 ? sb.ToString() : null;
		}
		internal static APIClient.ToolCall ParseToolCallItem(JsonNode toolCallObj){
			if(!toolCallObj.IsObject) return null;
			var name = toolCallObj.GetString("name") ?? toolCallObj["function"].GetString("name");
			if(string.IsNullOrWhiteSpace(name)) return null;
			var callId = toolCallObj.GetString("call_id") ?? toolCallObj.GetString("id");
			if(string.IsNullOrWhiteSpace(callId)) return null;
			var argumentsToken = toolCallObj["arguments"];
			if(!argumentsToken.Exists) argumentsToken = toolCallObj["function"]["arguments"];
			if(!argumentsToken.Exists) argumentsToken = toolCallObj["input"];
			var arguments = argumentsToken.IsNull ? "" : argumentsToken.IsString ? argumentsToken.AsString() : argumentsToken.ToJson();
			return new APIClient.ToolCall(callId, name, arguments);
		}
		internal static List<APIClient.ToolCall> ParseToolCallsFromContent(JsonNode contentToken){
			if(!contentToken.IsArray) return null;
			var toolCalls = new List<APIClient.ToolCall>();
			foreach(var item in contentToken.Where(item => item.IsObject)){
				var type = item.GetString("type");
				if(!string.Equals(type, "tool_call", StringComparison.OrdinalIgnoreCase) && !string.Equals(type, "function_call", StringComparison.OrdinalIgnoreCase) &&
					!string.Equals(type, "tool_use", StringComparison.OrdinalIgnoreCase)) continue;
				var toolCall = ParseToolCallItem(item);
				if(toolCall != null) toolCalls.Add(toolCall);
			}
			return toolCalls.Count > 0 ? toolCalls : null;
		}
		internal static List<APIClient.ToolCall> ParseToolCalls(JsonNode toolCallsToken){
			if(!toolCallsToken.IsArray) return null;
			var toolCalls = new List<APIClient.ToolCall>();
			foreach(var toolCallToken in toolCallsToken){
				if(!toolCallToken.IsObject) continue;
				var toolCall = ParseToolCallItem(toolCallToken);
				if(toolCall != null) toolCalls.Add(toolCall);
			}
			return toolCalls.Count > 0 ? toolCalls : null;
		}
		private static JsonNode RemoveIdFields(JsonNode token){
			if(token.IsNull) return JsonNode.Null;
			if(token.IsObject){
				var obj = Json.ObjectBuilder();
				foreach(var property in token.Properties()){
					if(string.Equals(property.Name, "id", StringComparison.OrdinalIgnoreCase)) continue;
					obj.Set(property.Name, RemoveIdFields(property.Value));
				}
				return obj.ToNode();
			}
			if(token.IsArray){
				var array = Json.ArrayBuilder();
				foreach(var item in token) array.Add(RemoveIdFields(item));
				return array.ToNode();
			}
			if(token.IsString) return Json.String(token.AsString());
			return token;
		}
		private static bool IsNonTextContentItem(JsonNode obj){
			var type = obj.GetString("type");
			return string.Equals(type, "reasoning", StringComparison.OrdinalIgnoreCase) ||
				string.Equals(type, "thinking", StringComparison.OrdinalIgnoreCase) ||
				string.Equals(type, "redacted_thinking", StringComparison.OrdinalIgnoreCase) ||
				string.Equals(type, "tool_call", StringComparison.OrdinalIgnoreCase) ||
				string.Equals(type, "function_call", StringComparison.OrdinalIgnoreCase) ||
				string.Equals(type, "tool_use", StringComparison.OrdinalIgnoreCase);
		}
	}
}
