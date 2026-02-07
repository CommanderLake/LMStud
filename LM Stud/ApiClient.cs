using System;
using System.Collections.Generic;
using System.IO;
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
		private readonly string _model;
		private readonly string _instructions;
		internal ApiClient(string apiBaseUrl, string apiKey, string model, string instructions = null){
			_apiBaseUrl = apiBaseUrl?.Trim() ?? "";
			_apiKey = apiKey?.Trim() ?? "";
			_model = model?.Trim() ?? "";
			_instructions = instructions?.Trim();
		}
		internal ChatCompletionResult CreateChatCompletion(IReadOnlyList<ChatMessage> messages, float temperature, int maxTokens, string toolsJson, JToken toolChoice, string previousResponseId, CancellationToken cancellationToken){
			if(string.IsNullOrWhiteSpace(_apiBaseUrl)) throw new InvalidOperationException("API base URL is not configured.");
			if(string.IsNullOrWhiteSpace(_apiKey)) throw new InvalidOperationException("API key is not configured.");
			if(string.IsNullOrWhiteSpace(_model)) throw new InvalidOperationException("API model is not configured.");
			var payload = new JObject{
				["model"] = _model,
				["input"] = new JArray(messages.Select(BuildInputMessagePayload)),
				["temperature"] = temperature
			};
			if(!string.IsNullOrWhiteSpace(_instructions)) payload["instructions"] = _instructions;
			payload["store"] = true;
			if(!string.IsNullOrWhiteSpace(previousResponseId)) payload["previous_response_id"] = previousResponseId;
			if(maxTokens > 0) payload["max_tokens"] = maxTokens;
			if(!string.IsNullOrWhiteSpace(toolsJson)){
				var normalizedTools = NormalizeToolsJson(toolsJson);
				payload["tools"] = (JToken)normalizedTools ?? new JRaw(toolsJson);
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
		private static JObject BuildInputMessagePayload(ChatMessage message){
			var isToolRole = string.Equals(message.Role, "tool", StringComparison.OrdinalIgnoreCase);
			JObject payload;
			if(isToolRole){
				payload = new JObject{
					["type"] = "function_call_output",
					["call_id"] = message.ToolCallId ?? "",
					["output"] = message.Content ?? ""
				};
				if(!string.IsNullOrWhiteSpace(message.ToolName)) payload["name"] = message.ToolName;
				return payload;
			}
			payload = new JObject{ ["role"] = message.Role };
			var contentItems = new JArray();
			if(!string.IsNullOrWhiteSpace(message.Content)){
				contentItems.Add(new JObject{
					["type"] = "input_text",
					["text"] = message.Content
				});
			}
			payload["content"] = contentItems.Count > 0 ? (JToken)contentItems : JValue.CreateNull();
			return payload;
		}
			private static ChatCompletionResult ParseResponseBody(string jsonText){
				if(string.IsNullOrWhiteSpace(jsonText)) throw new InvalidOperationException("API response did not contain any message.");
				var root = JObject.Parse(jsonText);
				var responseId = root.Value<string>("id");
				if(root["output"] is JArray output){
						string content = null;
						var toolCalls = new List<ToolCall>();
					foreach(var item in output.OfType<JObject>()){
						var type = item.Value<string>("type");
						if(string.Equals(type, "message", StringComparison.OrdinalIgnoreCase)){
							var role = item.Value<string>("role");
							if(!string.Equals(role, "assistant", StringComparison.OrdinalIgnoreCase)) continue;
							var messageContent = ExtractContentText(item["content"]);
							if(!string.IsNullOrWhiteSpace(messageContent)) content = messageContent;
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
						if(string.Equals(type, "tool_call", StringComparison.OrdinalIgnoreCase)
							|| string.Equals(type, "function_call", StringComparison.OrdinalIgnoreCase)){
							var toolCall = ParseToolCallItem(item);
							if(toolCall != null) toolCalls.Add(toolCall);
						}
					}
					var finalToolCalls = toolCalls.Count > 0 ? toolCalls : null;
					if(string.IsNullOrWhiteSpace(content) && finalToolCalls == null){
						var outputText = ExtractContentText(root["output_text"]);
						if(!string.IsNullOrWhiteSpace(outputText)) content = outputText;
					}
						var finalContent = content ?? "";
						return new ChatCompletionResult(finalContent, finalToolCalls, responseId);
					}
				var message = ExtractFirstMessage(root);
				if(message == null) throw new InvalidOperationException("API response did not contain any message.");
				var messageContentFallback = message["content"]?.Type == JTokenType.Null ? null : message["content"]?.ToString();
				var toolCallsFallback = ParseToolCalls(message["tool_calls"]);
				if(string.IsNullOrWhiteSpace(messageContentFallback) && (toolCallsFallback == null || toolCallsFallback.Count == 0)) throw new InvalidOperationException("API response did not contain any content.");
				return new ChatCompletionResult(messageContentFallback, toolCallsFallback, responseId);
			}
		private static string ExtractContentText(JToken contentToken){
			if(contentToken == null || contentToken.Type == JTokenType.Null) return null;
			if(contentToken.Type == JTokenType.String) return contentToken.ToString();
			if(contentToken.Type == JTokenType.Array){
				var sb = new StringBuilder();
				foreach(var item in contentToken){
					if(item == null || item.Type == JTokenType.Null) continue;
					if(item.Type == JTokenType.String){ sb.Append(item.ToString()); continue; }
					if(item is JObject obj){
						var text = obj.Value<string>("text") ?? obj.Value<string>("content");
						if(!string.IsNullOrWhiteSpace(text)) sb.Append(text);
					}
				}
				return sb.Length > 0 ? sb.ToString() : null;
			}
			if(contentToken is JObject contentObj){
				return contentObj.Value<string>("text") ?? contentObj.Value<string>("content");
			}
			return null;
		}
		private static ToolCall ParseToolCallItem(JObject toolCallObj){
			var name = toolCallObj.Value<string>("name") ?? toolCallObj["function"]?.Value<string>("name");
			if(string.IsNullOrWhiteSpace(name)) return null;
			var argumentsToken = toolCallObj["arguments"] ?? toolCallObj["function"]?["arguments"];
			var arguments = argumentsToken == null ? "" : argumentsToken.Type == JTokenType.String ? argumentsToken.ToString() : argumentsToken.ToString(Formatting.None);
			var id = toolCallObj.Value<string>("id") ?? toolCallObj.Value<string>("call_id");
			return new ToolCall(id, name, arguments);
		}
		private static List<ToolCall> ParseToolCallsFromContent(JToken contentToken){
			var contentArray = contentToken as JArray;
			if(contentArray == null) return null;
			var toolCalls = new List<ToolCall>();
			foreach(var item in contentArray.OfType<JObject>()){
				var type = item.Value<string>("type");
				if(!string.Equals(type, "tool_call", StringComparison.OrdinalIgnoreCase)
					&& !string.Equals(type, "function_call", StringComparison.OrdinalIgnoreCase)) continue;
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
					if(tool["function"] is JObject functionObj){
						var name = functionObj.Value<string>("name");
						if(string.IsNullOrWhiteSpace(name)) continue;
						var normalizedTool = new JObject{
							["type"] = tool.Value<string>("type") ?? "function",
							["name"] = name
						};
						var description = functionObj.Value<string>("description");
						if(!string.IsNullOrWhiteSpace(description)) normalizedTool["description"] = description;
						if(functionObj["parameters"] != null) normalizedTool["parameters"] = functionObj["parameters"];
						normalized.Add(normalizedTool);
						continue;
					}
					if(!string.IsNullOrWhiteSpace(tool.Value<string>("name"))){
						normalized.Add(tool);
					}
				}
				return normalized.Count > 0 ? normalized : null;
			} catch(JsonException){
				return null;
			}
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
				if(toolCall != null){
					toolCalls.Add(toolCall);
					continue;
				}
				var functionObj = toolCallObj["function"] as JObject;
				if(functionObj == null) continue;
				var name = functionObj.Value<string>("name");
				if(string.IsNullOrWhiteSpace(name)) continue;
				var argumentsToken = functionObj["arguments"];
				var arguments = argumentsToken == null ? "" : argumentsToken.Type == JTokenType.String ? argumentsToken.ToString() : argumentsToken.ToString(Formatting.None);
				toolCalls.Add(new ToolCall(toolCallObj.Value<string>("id"), name, arguments));
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
			public string ToolName;
			public List<ToolCall> ToolCalls;
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
			public string ResponseId;
			public List<ToolCall> ToolCalls;
			public ChatCompletionResult(string content, List<ToolCall> toolCalls, string responseId){
				Content = content;
				ToolCalls = toolCalls;
				ResponseId = responseId;
			}
		}
	}
}