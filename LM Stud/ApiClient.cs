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
		internal ApiClient(string apiBaseUrl, string apiKey, string model){
			_apiBaseUrl = apiBaseUrl?.Trim() ?? "";
			_apiKey = apiKey?.Trim() ?? "";
			_model = model?.Trim() ?? "";
		}
		internal ChatCompletionResult CreateChatCompletion(IReadOnlyList<ChatMessage> messages, float temperature, int maxTokens, string toolsJson, JToken toolChoice, CancellationToken cancellationToken){
			if(string.IsNullOrWhiteSpace(_apiBaseUrl)) throw new InvalidOperationException("API base URL is not configured.");
			if(string.IsNullOrWhiteSpace(_apiKey)) throw new InvalidOperationException("API key is not configured.");
			if(string.IsNullOrWhiteSpace(_model)) throw new InvalidOperationException("API model is not configured.");
			var payload = new JObject{
				["model"] = _model,
				["messages"] = new JArray(messages.Select(BuildMessagePayload)),
				["temperature"] = temperature
			};
			if(maxTokens > 0) payload["max_tokens"] = maxTokens;
			if(!string.IsNullOrWhiteSpace(toolsJson)){
				payload["tools"] = new JRaw(toolsJson);
				payload["tool_choice"] = toolChoice ?? new JValue("auto");
				payload["parallel_tool_calls"] = true;
			}
			using(var request = new HttpRequestMessage(HttpMethod.Post, BuildChatEndpoint(_apiBaseUrl))){
				request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
				request.Content = new StringContent(payload.ToString(Formatting.None), Encoding.UTF8, "application/json");
				using(var response = HttpClient.SendAsync(request, cancellationToken).GetAwaiter().GetResult()){
					var body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
					if(!response.IsSuccessStatusCode) throw new InvalidOperationException($"API error ({(int)response.StatusCode}): {body}");
					var message = ExtractFirstMessage(body);
					if(message == null) throw new InvalidOperationException("API response did not contain any message.");
					var content = message["content"]?.Type == JTokenType.Null ? null : message["content"]?.ToString();
					var toolCalls = ParseToolCalls(message["tool_calls"]);
					if(string.IsNullOrWhiteSpace(content) && (toolCalls == null || toolCalls.Count == 0)) throw new InvalidOperationException("API response did not contain any content.");
					return new ChatCompletionResult(content, toolCalls);
				}
			}
		}
		private static JObject BuildMessagePayload(ChatMessage message){
			var payload = new JObject{ ["role"] = message.Role, ["content"] = message.Content ?? (JToken)JValue.CreateNull() };
			if(message.ToolCalls != null && message.ToolCalls.Count > 0){
				payload["tool_calls"] = new JArray(message.ToolCalls.Select(call => new JObject{
					["id"] = call.Id,
					["type"] = "function",
					["function"] = new JObject{
						["name"] = call.Name,
						["arguments"] = call.Arguments ?? ""
					}
				}));
			}
			if(!string.IsNullOrWhiteSpace(message.ToolCallId)) payload["tool_call_id"] = message.ToolCallId;
			return payload;
		}
		private static JObject ExtractFirstMessage(string jsonText){
			if(string.IsNullOrWhiteSpace(jsonText)) return null;
			using(var stringReader = new StringReader(jsonText))
			using(var reader = new JsonTextReader(stringReader)){
				while(reader.Read()){
					if(reader.TokenType != JsonToken.PropertyName || !"choices".Equals(reader.Value)) continue;
					if(!reader.Read() || reader.TokenType != JsonToken.StartArray) return null;
					if(!reader.Read() || reader.TokenType != JsonToken.StartObject) return null;
					while(reader.Read()){
						if(reader.TokenType == JsonToken.PropertyName && "message".Equals(reader.Value)){
							if(!reader.Read()) return null;
							return JObject.Load(reader);
						}
						if(reader.TokenType == JsonToken.PropertyName){
							reader.Read();
							JToken.ReadFrom(reader);
						} else if(reader.TokenType == JsonToken.EndObject){
							break;
						}
					}
				}
			}
			return null;
		}
		private static List<ToolCall> ParseToolCalls(JToken toolCallsToken){
			var toolCallsArray = toolCallsToken as JArray;
			if(toolCallsArray == null) return null;
			var toolCalls = new List<ToolCall>();
			foreach(var toolCallToken in toolCallsArray){
				var toolCallObj = toolCallToken as JObject;
				if(toolCallObj == null) continue;
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
			public List<ToolCall> ToolCalls;
			public ChatCompletionResult(string content, List<ToolCall> toolCalls){
				Content = content;
				ToolCalls = toolCalls;
			}
		}
	}
}