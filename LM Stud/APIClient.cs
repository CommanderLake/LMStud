using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LMStud.Parsers;
using LMStud.Properties;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
namespace LMStud{
	internal sealed class APIClient : IDisposable{
		private readonly string _apiBaseUrl;
		private readonly bool _apiClientStore;
		private readonly HttpClient _apiHttpClient = new HttpClient{ Timeout = Timeout.InfiniteTimeSpan };
		private readonly string _apiKey;
		private readonly string _instructions;
		private readonly string _model;
		internal APIClient(string apiBaseUrl, string apiKey, string model, bool apiClientStore, string instructions = null){
			_apiClientStore = apiClientStore;
			_apiBaseUrl = apiBaseUrl?.Trim() ?? "";
			_apiKey = apiKey?.Trim() ?? "";
			_model = model?.Trim() ?? "";
			_instructions = instructions?.Trim();
		}
		public void Dispose(){_apiHttpClient?.Dispose();}
		internal ChatCompletionResult CreateChatCompletion(JArray history, float temperature, int maxTokens, string toolsJson, JToken toolChoice, CancellationToken cancellationToken){
			if(string.IsNullOrWhiteSpace(_apiBaseUrl)) throw new InvalidOperationException("API base URL is not configured.");
			if(history == null) throw new InvalidOperationException("History is not configured.");
			var responsesPayload = BuildResponsesPayload(history, temperature, maxTokens, toolsJson, toolChoice);
			var responsesError = TrySendChatRequest(BuildResponsesEndpoint(_apiBaseUrl), responsesPayload, cancellationToken, out var responsesResult);
			if(responsesResult != null) return responsesResult;
			if(!ShouldFallbackToChatCompletions(responsesError)) throw responsesError ?? new InvalidOperationException("API response did not contain any message.");
			var chatCompletionsPayload = BuildChatCompletionsPayload(history, temperature, maxTokens, toolsJson, toolChoice);
			var chatCompletionsError = TrySendChatRequest(BuildChatCompletionsEndpoint(_apiBaseUrl), chatCompletionsPayload, cancellationToken, out var chatCompletionsResult);
			if(chatCompletionsResult != null) return chatCompletionsResult;
			throw chatCompletionsError ?? responsesError ?? new InvalidOperationException("API response did not contain any message.");
		}
		private static bool ShouldFallbackToChatCompletions(InvalidOperationException error){
			if(error == null) return true;
			var message = error.Message ?? "";
			if(message.IndexOf("404", StringComparison.OrdinalIgnoreCase) >= 0 || message.IndexOf("405", StringComparison.OrdinalIgnoreCase) >= 0 ||
				message.IndexOf("501", StringComparison.OrdinalIgnoreCase) >= 0 || message.IndexOf("not found", StringComparison.OrdinalIgnoreCase) >= 0 ||
				message.IndexOf("not implemented", StringComparison.OrdinalIgnoreCase) >= 0) return true;
			if(message.IndexOf("400", StringComparison.OrdinalIgnoreCase) < 0) return false;
			return message.IndexOf("unknown", StringComparison.OrdinalIgnoreCase) >= 0 || message.IndexOf("unsupported", StringComparison.OrdinalIgnoreCase) >= 0 ||
					message.IndexOf("invalid", StringComparison.OrdinalIgnoreCase) >= 0 || message.IndexOf("parallel_tool_calls", StringComparison.OrdinalIgnoreCase) >= 0 ||
					message.IndexOf("tool_choice", StringComparison.OrdinalIgnoreCase) >= 0 || message.IndexOf("max_output_tokens", StringComparison.OrdinalIgnoreCase) >= 0 ||
					message.IndexOf("input", StringComparison.OrdinalIgnoreCase) >= 0 || message.IndexOf("responses", StringComparison.OrdinalIgnoreCase) >= 0;
		}
		private InvalidOperationException TrySendChatRequest(string endpoint, JToken payload, CancellationToken cancellationToken, out ChatCompletionResult result){
			result = null;
			try{
				using(var request = new HttpRequestMessage(HttpMethod.Post, endpoint)){
					if(!string.IsNullOrWhiteSpace(_apiKey)) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
					var content = payload.ToString(Formatting.Indented);
					request.Content = new StringContent(content, Encoding.UTF8, "application/json");
					//File.AppendAllText("E:\\\\response1.txt", content + "\r\n");
					using(var response = _apiHttpClient.SendAsync(request, cancellationToken).GetAwaiter().GetResult()){
						var body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
						//File.AppendAllText("E:\\\\response1.txt", body + "\r\n\r\n");
						if(!response.IsSuccessStatusCode) throw new InvalidOperationException($"API error ({(int)response.StatusCode}): {body}");
						result = APIResponseParser.ParseResponseBody(body);
						return null;
					}
				}
			} catch(InvalidOperationException ex){ return ex; } catch(Exception ex){ return new InvalidOperationException(ex.Message, ex); }
		}
		private JObject BuildResponsesPayload(JArray history, float temperature, int maxTokens, string toolsJson, JToken toolChoice){
			var payload = new JObject{ ["model"] = _model, ["input"] = history, ["temperature"] = temperature };
			if(!string.IsNullOrWhiteSpace(_instructions)) payload["instructions"] = _instructions;
			payload["store"] = _apiClientStore;
			if(maxTokens > 0) payload["max_output_tokens"] = maxTokens;
			var effort = Program.MainForm.GetReasoningEffort();
			var summaryType = Program.MainForm.GetReasoningSummaryType();
			if(effort != null || summaryType != null){
				var reasoningPayload = new JObject();
				if(effort != null) reasoningPayload["effort"] = effort;
				if(summaryType != null) reasoningPayload["summary"] = summaryType;
				payload["reasoning"] = reasoningPayload;
			}
			if(!string.IsNullOrWhiteSpace(toolsJson)){
				var normalizedTools = NormalizeToolsJson(toolsJson);
				payload["tools"] = (JToken)normalizedTools ?? JToken.Parse(toolsJson);
				payload["tool_choice"] = toolChoice ?? new JValue("auto");
				payload["parallel_tool_calls"] = true;
			}
			return payload;
		}
		private JObject BuildChatCompletionsPayload(JArray history, float temperature, int maxTokens, string toolsJson, JToken toolChoice){
			var messages = ConvertHistoryToChatCompletionMessages(history);
			if(!string.IsNullOrWhiteSpace(_instructions)) messages.Insert(0, new JObject{ ["role"] = "system", ["content"] = _instructions });
			var payload = new JObject{ ["model"] = _model, ["messages"] = messages, ["temperature"] = temperature };
			if(maxTokens > 0) payload["max_tokens"] = maxTokens;
			if(!string.IsNullOrWhiteSpace(toolsJson)){
				var normalizedTools = NormalizeToolsJsonForChatCompletions(toolsJson);
				payload["tools"] = (JToken)normalizedTools ?? JToken.Parse(toolsJson);
				payload["tool_choice"] = toolChoice ?? new JValue("auto");
				payload["parallel_tool_calls"] = true;
			}
			return payload;
		}
		private static JArray ConvertHistoryToChatCompletionMessages(JArray history){
			var messages = new JArray();
			if(history == null) return messages;
			foreach(var item in history.OfType<JObject>()){
				var itemType = item.Value<string>("type");
				if(string.Equals(itemType, "function_call_output", StringComparison.OrdinalIgnoreCase)){
					messages.Add(new JObject{ ["role"] = "tool", ["tool_call_id"] = item.Value<string>("call_id"), ["content"] = item.Value<string>("output") ?? "" });
					continue;
				}
				if(string.Equals(itemType, "function_call", StringComparison.OrdinalIgnoreCase)){
					var toolCall = new JObject{
						["id"] = item.Value<string>("call_id"), ["type"] = "function",
						["function"] = new JObject{ ["name"] = item.Value<string>("name"), ["arguments"] = item.Value<string>("arguments") ?? "" }
					};
					messages.Add(new JObject{ ["role"] = "assistant", ["content"] = "", ["tool_calls"] = new JArray{ toolCall } });
					continue;
				}
				var role = item.Value<string>("role");
				if(string.IsNullOrWhiteSpace(role)) continue;
				messages.Add(new JObject{ ["role"] = role, ["content"] = item["content"] ?? "" });
			}
			return messages;
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
			foreach(var message in messages.Where(message => message != null)){
				var hasToolCalls = message.ToolCalls != null && message.ToolCalls.Count > 0;
				var hasContent = !string.IsNullOrWhiteSpace(message.Content);
				var isToolRole = string.Equals(message.Role, "tool", StringComparison.OrdinalIgnoreCase);
				if(isToolRole || hasContent || !hasToolCalls) items.Add(BuildInputMessagePayload(message));
				if(message.ToolCalls == null) continue;
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
			foreach(var item in result.OutputItems) history.Add(ConvertOutputItemToInputItem(item));
		}
		private static JToken ConvertOutputItemToInputItem(JToken item){
			if(!(item is JObject obj)) return item?.DeepClone();
			var type = obj.Value<string>("type");
			if(string.Equals(type, "message", StringComparison.OrdinalIgnoreCase)){
				var role = obj.Value<string>("role") ?? "assistant";
				var content = APIResponseParserCommon.ExtractContentText(obj["content"]) ?? obj.Value<string>("text") ?? "";
				return new JObject{ ["role"] = role, ["content"] = content };
			}
			if(string.Equals(type, "output_text", StringComparison.OrdinalIgnoreCase)){
				var content = obj.Value<string>("text") ?? obj.Value<string>("content") ?? "";
				return new JObject{ ["role"] = "assistant", ["content"] = content };
			}
			return obj.DeepClone();
		}
		private static JArray NormalizeToolsJson(string toolsJson){
			try{
				var parsed = JToken.Parse(toolsJson);
				if(!(parsed is JArray toolsArray)) return null;
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
		private static JArray NormalizeToolsJsonForChatCompletions(string toolsJson){
			try{
				var parsed = JToken.Parse(toolsJson);
				if(!(parsed is JArray toolsArray)) return null;
				var normalized = new JArray();
				foreach(var tool in toolsArray.OfType<JObject>()){
					if(tool["function"] is JObject functionObj){
						var name = functionObj.Value<string>("name");
						if(string.IsNullOrWhiteSpace(name)) continue;
						var function = new JObject{ ["name"] = name };
						var description = functionObj.Value<string>("description");
						if(!string.IsNullOrWhiteSpace(description)) function["description"] = description;
						if(functionObj["parameters"] != null) function["parameters"] = functionObj["parameters"];
						normalized.Add(new JObject{ ["type"] = "function", ["function"] = function });
						continue;
					}
					var toolName = tool.Value<string>("name");
					if(string.IsNullOrWhiteSpace(toolName)) continue;
					var fallbackFunction = new JObject{ ["name"] = toolName };
					var fallbackDescription = tool.Value<string>("description");
					if(!string.IsNullOrWhiteSpace(fallbackDescription)) fallbackFunction["description"] = fallbackDescription;
					if(tool["parameters"] != null) fallbackFunction["parameters"] = tool["parameters"];
					normalized.Add(new JObject{ ["type"] = "function", ["function"] = fallbackFunction });
				}
				return normalized.Count > 0 ? normalized : null;
			} catch(JsonException){ return null; }
		}
		internal List<string> GetModels(CancellationToken cancellationToken){
			if(string.IsNullOrWhiteSpace(_apiBaseUrl)) throw new InvalidOperationException("API base URL is not configured.");
			using(var request = new HttpRequestMessage(HttpMethod.Get, BuildModelsEndpoint(_apiBaseUrl))){
				if(!string.IsNullOrWhiteSpace(_apiKey)) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
				using(var response = _apiHttpClient.SendAsync(request, cancellationToken).GetAwaiter().GetResult()){
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
		private static string BuildResponsesEndpoint(string apiBaseUrl){return BuildV1Endpoint(apiBaseUrl, "responses");}
		private static string BuildChatCompletionsEndpoint(string apiBaseUrl){return BuildV1Endpoint(apiBaseUrl, "chat/completions");}
		private static string BuildModelsEndpoint(string apiBaseUrl){return BuildV1Endpoint(apiBaseUrl, "models");}
		private static string BuildV1Endpoint(string apiBaseUrl, string resource){
			var normalized = apiBaseUrl.TrimEnd('/');
			if(normalized.EndsWith("/v1", StringComparison.OrdinalIgnoreCase)) return normalized + "/" + resource;
			return normalized + "/v1/" + resource;
		}
		private static string GetApiClientFriendlyError(Exception exception){
			if(exception == null) return Resources.Unknown_API_error_;
			var root = exception;
			while(root?.InnerException != null) root = root.InnerException;
			switch(root){
				case SocketException _:
				case HttpRequestException _: return Resources.Unable_to_connect_to_the_API_server;
				case TaskCanceledException _:
				case TimeoutException _: return Resources.The_API_request_timed_out;
				case OperationCanceledException _: return Resources.The_API_request_was_canceled_before_completion;
				case JsonException _: return Resources.Received_an_invalid_response_from_the_API_server;
				case InvalidOperationException _:{
					var message = root.Message ?? "";
					if(message.IndexOf("401", StringComparison.OrdinalIgnoreCase) >= 0 || message.IndexOf("403", StringComparison.OrdinalIgnoreCase) >= 0) return Resources.Authentication_failed;
					if(message.IndexOf("404", StringComparison.OrdinalIgnoreCase) >= 0) return Resources.API_endpoint_not_found;
					if(message.IndexOf("429", StringComparison.OrdinalIgnoreCase) >= 0) return Resources.Rate_limit_exceeded;
					if(message.IndexOf("500", StringComparison.OrdinalIgnoreCase) >= 0 || message.IndexOf("502", StringComparison.OrdinalIgnoreCase) >= 0 ||
						message.IndexOf("503", StringComparison.OrdinalIgnoreCase) >= 0) return Resources.The_API_server_encountered_an_error;
					break;
				}
			}
			return root?.Message;
		}
		internal static void ShowApiClientError(string action, Exception exception){
			var detail = GetApiClientFriendlyError(exception);
			var technical = exception?.Message;
			if(!string.IsNullOrWhiteSpace(technical) && !string.Equals(technical, detail, StringComparison.Ordinal)) detail += "\r\n\r\n" + technical;
			Program.MainForm.ShowError(action, detail, false);
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
			public int? TotalTokens;
			public List<ToolCall> ToolCalls;
			public ChatCompletionResult(string content, string reasoning, List<ToolCall> toolCalls, string responseId, JArray outputItems, int? totalTokens = null){
				Content = content;
				Reasoning = reasoning;
				ToolCalls = toolCalls;
				ResponseId = responseId;
				OutputItems = outputItems;
				TotalTokens = totalTokens;
			}
		}
	}
}
