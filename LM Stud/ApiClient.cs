using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
namespace LMStud{
	internal sealed class ApiClient{
		private static readonly HttpClient HttpClient = new HttpClient();
		private readonly string _apiBaseUrl;
		private readonly string _apiKey;
		private readonly string _instructions;
		private readonly string _model;
		internal ApiClient(string apiBaseUrl, string apiKey, string model, string instructions = null){
			_apiBaseUrl = apiBaseUrl?.Trim() ?? "";
			_apiKey = apiKey?.Trim() ?? "";
			_model = model?.Trim() ?? "";
			_instructions = instructions?.Trim();
		}
		internal ChatCompletionResult CreateChatCompletion(JArray history, float temperature, int maxTokens, string toolsJson, JToken toolChoice, CancellationToken cancellationToken){
			if(string.IsNullOrWhiteSpace(_apiBaseUrl)) throw new InvalidOperationException("API base URL is not configured.");
			if(string.IsNullOrWhiteSpace(_apiKey)) throw new InvalidOperationException("API key is not configured.");
			if(string.IsNullOrWhiteSpace(_model)) throw new InvalidOperationException("API model is not configured.");
			if(history == null) throw new InvalidOperationException("History is not configured.");
			var payload = new JObject{ ["model"] = _model, ["input"] = history, ["temperature"] = temperature };
			if(!string.IsNullOrWhiteSpace(_instructions)) payload["instructions"] = _instructions;
			payload["store"] = true;
			if(maxTokens > 0) payload["max_output_tokens"] = maxTokens;
			if(!string.IsNullOrWhiteSpace(toolsJson)){
				var normalizedTools = NormalizeToolsJson(toolsJson);
				payload["tools"] = (JToken)normalizedTools ?? JToken.Parse(toolsJson);
				payload["tool_choice"] = toolChoice ?? new JValue("auto");
				payload["parallel_tool_calls"] = true;
			}
			using(var request = new HttpRequestMessage(HttpMethod.Post, BuildChatEndpoint(_apiBaseUrl))){
				request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
				request.Content = new StringContent(payload.ToString(Formatting.None), Encoding.UTF8, "application/json");
				using(var response = HttpClient.SendAsync(request, cancellationToken).GetAwaiter().GetResult()){
					var body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
					if(!response.IsSuccessStatusCode) throw new InvalidOperationException($"API error ({(int)response.StatusCode}): {body}");
					return ParseResponseBody(body);
				}
			}
		}
		internal static JToken BuildInputMessagePayload(ChatMessage message){
			var isToolRole = string.Equals(message.Role, "tool", StringComparison.OrdinalIgnoreCase);
			if(isToolRole){
				if(string.IsNullOrWhiteSpace(message.ToolCallId)) throw new InvalidOperationException("Tool call output requires a call_id.");
				return new JObject{ ["type"] = "function_call_output", ["call_id"] = message.ToolCallId, ["output"] = message.Content ?? "" };
			}
			return new JObject{ ["role"] = message.Role, ["content"] = message.Content ?? "" };
		}
		internal static JArray BuildInputItems(IEnumerable<ChatMessage> messages){
			if(messages == null) return new JArray();
			var items = new JArray();
			foreach(var message in messages){
				if(message == null) continue;
				items.Add(BuildInputMessagePayload(message));
				if(message.ToolCalls != null)
					foreach(var toolCall in message.ToolCalls){
						if(toolCall == null) continue;
						if(string.IsNullOrWhiteSpace(toolCall.Id) || string.IsNullOrWhiteSpace(toolCall.Name)) continue;
						var toolItem = new JObject{ ["type"] = "function_call", ["call_id"] = toolCall.Id, ["name"] = toolCall.Name, ["arguments"] = toolCall.Arguments ?? "" };
						items.Add(toolItem);
					}
			}
			return items;
		}
		internal static void AppendOutputItems(JArray history, ChatCompletionResult result){
			if(history == null || result?.OutputItems == null) return;
			foreach(var item in result.OutputItems) history.Add(item);
		}
		private static ChatCompletionResult ParseResponseBody(string jsonText){
			if(string.IsNullOrWhiteSpace(jsonText)) throw new InvalidOperationException("API response did not contain any message.");
			var root = JObject.Parse(jsonText);
			var responseId = root.Value<string>("id");
			if(root["output"] is JArray output){
				var sanitizedOutput = SanitizeOutputItems(output);
				string content = null;
				string reasoning = null;
				var toolCalls = new List<ToolCall>();
				foreach(var item in output.OfType<JObject>()){
					var type = item.Value<string>("type");
					if(string.Equals(type, "message", StringComparison.OrdinalIgnoreCase)){
						var role = item.Value<string>("role");
						if(!string.Equals(role, "assistant", StringComparison.OrdinalIgnoreCase)) continue;
						var messageContent = ExtractContentText(item["content"]);
						if(!string.IsNullOrWhiteSpace(messageContent)) content = messageContent;
						var messageReasoning = ExtractReasoningTextFromContent(item["content"]);
						if(!string.IsNullOrWhiteSpace(messageReasoning)) reasoning = messageReasoning;
						var messageToolCalls = ParseToolCalls(item["tool_calls"]);
						if(messageToolCalls != null) toolCalls.AddRange(messageToolCalls);
						var contentToolCalls = ParseToolCallsFromContent(item["content"]);
						if(contentToolCalls != null) toolCalls.AddRange(contentToolCalls);
						continue;
					}
					if(string.Equals(type, "output_text", StringComparison.OrdinalIgnoreCase)){
						var messageContent = item.Value<string>("text") ?? item.Value<string>("content");
						if(!string.IsNullOrWhiteSpace(messageContent)) content = messageContent;
						continue;
					}
					if(string.Equals(type, "reasoning", StringComparison.OrdinalIgnoreCase)){
						var reasoningContent = ExtractReasoningText(item);
						if(!string.IsNullOrWhiteSpace(reasoningContent)) reasoning = reasoningContent;
						continue;
					}
					if(string.Equals(type, "tool_call", StringComparison.OrdinalIgnoreCase) || string.Equals(type, "function_call", StringComparison.OrdinalIgnoreCase)){
						var toolCall = ParseToolCallItem(item);
						if(toolCall != null) toolCalls.Add(toolCall);
					}
				}
				var finalToolCalls = toolCalls.Count > 0 ? toolCalls : null;
				if(string.IsNullOrWhiteSpace(content) && finalToolCalls == null){
					var outputText = ExtractContentText(root["output_text"]);
					if(!string.IsNullOrWhiteSpace(outputText)) content = outputText;
				}
				if(string.IsNullOrWhiteSpace(reasoning)){
					var outputReasoning = ExtractReasoningText(root["reasoning"] as JObject) ?? ExtractContentText(root["reasoning"]);
					if(!string.IsNullOrWhiteSpace(outputReasoning)) reasoning = outputReasoning;
				}
				var finalContent = content ?? "";
				return new ChatCompletionResult(finalContent, reasoning, finalToolCalls, responseId, sanitizedOutput);
			}
			var message = ExtractFirstMessage(root);
			if(message == null) throw new InvalidOperationException("API response did not contain any message.");
			var messageContentFallback = message["content"]?.Type == JTokenType.Null ? null : message["content"]?.ToString();
			var toolCallsFallback = ParseToolCalls(message["tool_calls"]);
			if(string.IsNullOrWhiteSpace(messageContentFallback) && (toolCallsFallback == null || toolCallsFallback.Count == 0))
				throw new InvalidOperationException("API response did not contain any content.");
			var fallbackReasoning = ExtractReasoningTextFromContent(message["content"]);
			return new ChatCompletionResult(messageContentFallback, fallbackReasoning, toolCallsFallback, responseId, null);
		}
		private static JArray SanitizeOutputItems(JArray output){
			if(output == null) return null;
			var sanitized = new JArray();
			foreach(var item in output){
				var clone = item?.DeepClone();
				RemoveIdFields(clone);
				sanitized.Add(clone);
			}
			return sanitized;
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
		private static string ExtractContentText(JToken contentToken){
			if(contentToken == null || contentToken.Type == JTokenType.Null) return null;
			if(contentToken.Type == JTokenType.String) return contentToken.ToString();
			if(contentToken.Type == JTokenType.Array){
				var sb = new StringBuilder();
				foreach(var item in contentToken){
					if(item == null || item.Type == JTokenType.Null) continue;
					string text = null;
					if(item.Type == JTokenType.String) text = item.ToString();
					else if(item is JObject obj) text = obj.Value<string>("text") ?? obj.Value<string>("content");
					if(string.IsNullOrWhiteSpace(text)) continue;
					sb.Append(text);
				}
				return sb.Length > 0 ? sb.ToString() : null;
			}
			if(contentToken is JObject contentObj){
				var text = contentObj.Value<string>("text") ?? contentObj.Value<string>("content");
				return !string.IsNullOrWhiteSpace(text) ? text : null;
			}
			return null;
		}
		private static string ExtractReasoningText(JToken reasoningItem){
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
		private static string ExtractReasoningTextFromContent(JToken contentToken){
			var contentArray = contentToken as JArray;
			if(contentArray == null) return null;
			var sb = new StringBuilder();
			foreach(var item in contentArray.OfType<JObject>()){
				var type = item.Value<string>("type");
				if(!string.Equals(type, "reasoning", StringComparison.OrdinalIgnoreCase) && !string.Equals(type, "thinking", StringComparison.OrdinalIgnoreCase)) continue;
				var reasoningText = item.Value<string>("text") ?? item.Value<string>("content") ?? item.Value<string>("summary");
				if(!string.IsNullOrWhiteSpace(reasoningText)) sb.Append(reasoningText);
			}
			return sb.Length > 0 ? sb.ToString() : null;
		}
		private static ToolCall ParseToolCallItem(JObject toolCallObj){
			var name = toolCallObj.Value<string>("name") ?? toolCallObj["function"]?.Value<string>("name");
			if(string.IsNullOrWhiteSpace(name)) return null;
			var callId = toolCallObj.Value<string>("call_id");
			if(string.IsNullOrWhiteSpace(callId)) return null;
			var argumentsToken = toolCallObj["arguments"] ?? toolCallObj["function"]?["arguments"];
			var arguments = argumentsToken == null ? "" : argumentsToken.Type == JTokenType.String ? argumentsToken.ToString() : argumentsToken.ToString(Formatting.None);
			return new ToolCall(callId, name, arguments);
		}
		private static List<ToolCall> ParseToolCallsFromContent(JToken contentToken){
			var contentArray = contentToken as JArray;
			if(contentArray == null) return null;
			var toolCalls = new List<ToolCall>();
			foreach(var item in contentArray.OfType<JObject>()){
				var type = item.Value<string>("type");
				if(!string.Equals(type, "tool_call", StringComparison.OrdinalIgnoreCase) && !string.Equals(type, "function_call", StringComparison.OrdinalIgnoreCase)) continue;
				var toolCall = ParseToolCallItem(item);
				if(toolCall != null) toolCalls.Add(toolCall);
			}
			return toolCalls.Count > 0 ? toolCalls : null;
		}
		private static JArray NormalizeToolsJson(string toolsJson){
			try{
				var parsed = JToken.Parse(toolsJson);
				var toolsArray = parsed as JArray;
				if(toolsArray == null) return null;
				var normalized = new JArray();
				foreach(var tool in toolsArray.OfType<JObject>()){
					var type = tool.Value<string>("type") ?? "function";
					if(tool["function"] is JObject functionObj){
						var name = functionObj.Value<string>("name");
						if(string.IsNullOrWhiteSpace(name)) continue;
						var normalizedTool = new JObject{ ["type"] = type, ["name"] = name };
						var description = functionObj.Value<string>("description");
						if(!string.IsNullOrWhiteSpace(description)) normalizedTool["description"] = description;
						if(functionObj["parameters"] != null) normalizedTool["parameters"] = functionObj["parameters"];
						normalized.Add(normalizedTool);
						continue;
					}
					var toolName = tool.Value<string>("name");
					if(string.IsNullOrWhiteSpace(toolName)) continue;
					var fallbackTool = new JObject{ ["type"] = type, ["name"] = toolName };
					var fallbackDescription = tool.Value<string>("description");
					if(!string.IsNullOrWhiteSpace(fallbackDescription)) fallbackTool["description"] = fallbackDescription;
					if(tool["parameters"] != null) fallbackTool["parameters"] = tool["parameters"];
					normalized.Add(fallbackTool);
				}
				return normalized.Count > 0 ? normalized : null;
			} catch(JsonException){ return null; }
		}
		private static JObject ExtractFirstMessage(JObject root){
			var choices = root["choices"] as JArray;
			if(choices == null || choices.Count == 0) return null;
			var first = choices[0] as JObject;
			return first?["message"] as JObject;
		}
		private static List<ToolCall> ParseToolCalls(JToken toolCallsToken){
			var toolCallsArray = toolCallsToken as JArray;
			if(toolCallsArray == null) return null;
			var toolCalls = new List<ToolCall>();
			foreach(var toolCallToken in toolCallsArray){
				var toolCallObj = toolCallToken as JObject;
				if(toolCallObj == null) continue;
				var toolCall = ParseToolCallItem(toolCallObj);
				if(toolCall != null) toolCalls.Add(toolCall);
			}
			return toolCalls.Count > 0 ? toolCalls : null;
		}
		internal List<string> ListModels(CancellationToken cancellationToken){
			if(string.IsNullOrWhiteSpace(_apiBaseUrl)) throw new InvalidOperationException("API base URL is not configured.");
			using(var request = new HttpRequestMessage(HttpMethod.Get, BuildModelsEndpoint(_apiBaseUrl))){
				if(!string.IsNullOrWhiteSpace(_apiKey)) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
				using(var response = HttpClient.SendAsync(request, cancellationToken).GetAwaiter().GetResult()){
					var body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
					if(!response.IsSuccessStatusCode) throw new InvalidOperationException($"API error ({(int)response.StatusCode}): {body}");
					var json = JObject.Parse(body);
					var models = new List<string>();
					if(!(json["data"] is JArray data)) return models;
					models.AddRange(data.Select(item => item?["id"]?.ToString()).Where(id => !string.IsNullOrWhiteSpace(id)));
					return models;
				}
			}
		}
		private static string BuildChatEndpoint(string apiBaseUrl){
			var normalized = apiBaseUrl.EndsWith("/") ? apiBaseUrl : apiBaseUrl + "/";
			if(normalized.EndsWith("/v1/")) return normalized + "responses";
			if(normalized.EndsWith("/v1")) return normalized + "/responses";
			return normalized + "v1/responses";
		}
		private static string BuildModelsEndpoint(string apiBaseUrl){
			var normalized = apiBaseUrl.EndsWith("/") ? apiBaseUrl : apiBaseUrl + "/";
			if(normalized.EndsWith("/v1/")) return normalized + "models";
			if(normalized.EndsWith("/v1")) return normalized + "/models";
			return normalized + "v1/models";
		}
		internal sealed class ChatMessage{
			public string Content;
			public string Role;
			public string ToolCallId;
			public List<ToolCall> ToolCalls;
			public string ToolName;
			public ChatMessage(string role, string content){
				Role = role;
				Content = content;
			}
		}
		internal sealed class ToolCall{
			public string Arguments;
			public string Id;
			public string Name;
			public ToolCall(string id, string name, string arguments){
				Id = id;
				Name = name;
				Arguments = arguments;
			}
		}
		internal sealed class ChatCompletionResult{
			public string Content;
			public JArray OutputItems;
			public string Reasoning;
			public string ResponseId;
			public List<ToolCall> ToolCalls;
			public ChatCompletionResult(string content, string reasoning, List<ToolCall> toolCalls, string responseId, JArray outputItems){
				Content = content;
				Reasoning = reasoning;
				ToolCalls = toolCalls;
				ResponseId = responseId;
				OutputItems = outputItems;
			}
		}
	}
}