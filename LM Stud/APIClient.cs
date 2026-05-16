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
		private readonly int _reasoningEffort;
		private readonly int _reasoningSummary;
		internal APIClient(string apiBaseUrl, string apiKey, string model, bool apiClientStore, string instructions = null, int reasoningEffort = 0, int reasoningSummary = 0){
			_apiClientStore = apiClientStore;
			_apiBaseUrl = apiBaseUrl?.Trim() ?? "";
			_apiKey = apiKey?.Trim() ?? "";
			_model = model?.Trim() ?? "";
			_instructions = instructions?.Trim();
			_reasoningEffort = reasoningEffort;
			_reasoningSummary = reasoningSummary;
		}
		public void Dispose(){_apiHttpClient?.Dispose();}
		internal ChatCompletionResult CreateChatCompletion(JArray history, float temperature, int maxTokens, string toolsJson, JToken toolChoice, CancellationToken cancellationToken, Action<string> streamCallback = null){
			if(string.IsNullOrWhiteSpace(_apiBaseUrl)) throw new InvalidOperationException(Resources.API_base_URL_is_not_configured_);
			if(history == null) throw new InvalidOperationException(Resources.History_is_not_configured_);
			var stream = streamCallback != null;
			var responsesPayload = BuildResponsesPayload(history, temperature, maxTokens, toolsJson, toolChoice, stream);
			var responsesError = TrySendChatRequest(BuildResponsesEndpoint(_apiBaseUrl), responsesPayload, cancellationToken, streamCallback, out var responsesResult);
			if(responsesResult != null) return responsesResult;
			if(!ShouldFallbackToChatCompletions(responsesError)) throw responsesError ?? new InvalidOperationException(Resources.API_response_did_not_contain_any_message_);
			var chatCompletionsPayload = BuildChatCompletionsPayload(history, temperature, maxTokens, toolsJson, toolChoice, stream);
			var chatCompletionsError = TrySendChatRequest(BuildChatCompletionsEndpoint(_apiBaseUrl), chatCompletionsPayload, cancellationToken, streamCallback, out var chatCompletionsResult);
			if(chatCompletionsResult != null) return chatCompletionsResult;
			throw chatCompletionsError ?? responsesError ?? new InvalidOperationException(Resources.API_response_did_not_contain_any_message_);
		}
		private static bool ShouldFallbackToChatCompletions(InvalidOperationException error){
			if(error == null) return true;
			var message = error.Message ?? "";
			if(ContainsAny(message, "404", "405", "501", "not found", "not implemented")) return true;
			return ContainsAny(message, "400") && ContainsAny(message, "unknown", "unsupported", "invalid", "parallel_tool_calls", "tool_choice", "max_output_tokens", "input", "responses", "stream");
		}
		private static bool ContainsAny(string text, params string[] values){
			return values.Any(value => text.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0);
		}
		private InvalidOperationException TrySendChatRequest(string endpoint, JToken payload, CancellationToken cancellationToken, Action<string> streamCallback, out ChatCompletionResult result){
			result = null;
			try{
				using(var request = CreateRequest(HttpMethod.Post, endpoint)){
					var content = payload.ToString(Formatting.Indented);
					request.Content = new StringContent(content, Encoding.UTF8, "application/json");
					var completionOption = streamCallback == null ? HttpCompletionOption.ResponseContentRead : HttpCompletionOption.ResponseHeadersRead;
					using(var response = _apiHttpClient.SendAsync(request, completionOption, cancellationToken).GetAwaiter().GetResult()){
						if(streamCallback != null && response.IsSuccessStatusCode){
							result = ParseStreamingResponse(response.Content, streamCallback, cancellationToken);
							return null;
						}
						var body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
						if(!response.IsSuccessStatusCode) throw new InvalidOperationException(string.Format(Resources.API_error___0_____1_, (int)response.StatusCode, body));
						result = APIResponseParser.ParseResponseBody(body);
						return null;
					}
				}
			} catch(OperationCanceledException){ throw; } catch(InvalidOperationException ex){ return ex; } catch(Exception ex){ return new InvalidOperationException(ex.Message, ex); }
		}
		private HttpRequestMessage CreateRequest(HttpMethod method, string endpoint){
			var request = new HttpRequestMessage(method, endpoint);
			if(!string.IsNullOrWhiteSpace(_apiKey)) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
			return request;
		}
		private ChatCompletionResult ParseStreamingResponse(HttpContent content, Action<string> streamCallback, CancellationToken cancellationToken){
			var accumulator = new StreamingResponseAccumulator();
			using(var stream = content.ReadAsStreamAsync().GetAwaiter().GetResult())
			using(var reader = new StreamReader(stream)){
				var eventName = "";
				var data = new StringBuilder();
				var rawBody = new StringBuilder();
				var sawSseData = false;
				while(!reader.EndOfStream){
					cancellationToken.ThrowIfCancellationRequested();
					var line = reader.ReadLine();
					if(line == null) break;
					if(line.Length == 0){
						if(ProcessStreamingEvent(eventName, data.ToString(), accumulator, streamCallback)) break;
						eventName = "";
						data.Clear();
						continue;
					}
					if(line.StartsWith("event:", StringComparison.OrdinalIgnoreCase)){
						eventName = line.Substring(6).Trim();
						continue;
					}
					if(line.StartsWith("data:", StringComparison.OrdinalIgnoreCase)){
						sawSseData = true;
						if(data.Length > 0) data.Append('\n');
						data.Append(line.Substring(5).TrimStart());
						continue;
					}
					if(!line.StartsWith(":", StringComparison.Ordinal)) rawBody.AppendLine(line);
				}
				if(!sawSseData && rawBody.Length > 0) return APIResponseParser.ParseResponseBody(rawBody.ToString());
				if(data.Length > 0) ProcessStreamingEvent(eventName, data.ToString(), accumulator, streamCallback);
			}
			return accumulator.BuildResult();
		}
		private static bool ProcessStreamingEvent(string eventName, string data, StreamingResponseAccumulator accumulator, Action<string> streamCallback){
			if(string.IsNullOrWhiteSpace(data)) return false;
			if(string.Equals(data.Trim(), "[DONE]", StringComparison.OrdinalIgnoreCase)) return true;
			JObject root;
			try{ root = JObject.Parse(data); } catch(JsonException){ return false; }
			accumulator.RememberResponseId(root.Value<string>("id"));
			var type = root.Value<string>("type") ?? eventName;
			if(string.Equals(type, "response.completed", StringComparison.OrdinalIgnoreCase) || string.Equals(type, "response.done", StringComparison.OrdinalIgnoreCase)){
				if(root["response"] is JObject responseObj) accumulator.SetFinalResponse(responseObj);
				return false;
			}
			if(string.Equals(type, "response.output_text.delta", StringComparison.OrdinalIgnoreCase)){
				accumulator.AppendContent(root.Value<string>("delta"), streamCallback);
				return false;
			}
			if(string.Equals(type, "response.reasoning_text.delta", StringComparison.OrdinalIgnoreCase) ||
				string.Equals(type, "response.reasoning_summary_text.delta", StringComparison.OrdinalIgnoreCase)){
				accumulator.AppendReasoning(root.Value<string>("delta"));
				return false;
			}
			if(string.Equals(type, "response.function_call_arguments.delta", StringComparison.OrdinalIgnoreCase)){
				accumulator.AppendToolArguments(GetStreamingToolKey(root), root.Value<string>("delta"));
				return false;
			}
			if(string.Equals(type, "response.output_item.done", StringComparison.OrdinalIgnoreCase) || string.Equals(type, "response.output_item.added", StringComparison.OrdinalIgnoreCase)){
				if(root["item"] is JObject item) accumulator.MergeToolCall(GetStreamingToolKey(root), item);
				return false;
			}
			if(root["choices"] is JArray choices){
				foreach(var choice in choices.OfType<JObject>()){
					if(!(choice["delta"] is JObject delta)) continue;
					var contentDelta = APIResponseParserCommon.ExtractContentText(delta["content"]);
					accumulator.AppendContent(contentDelta, streamCallback);
					accumulator.AppendReasoning(delta.Value<string>("reasoning_content") ?? delta.Value<string>("reasoning"));
					if(delta["tool_calls"] is JArray toolCalls)
						foreach(var toolCall in toolCalls.OfType<JObject>())
							accumulator.MergeChatToolCall(toolCall);
				}
			}
			return false;
		}
		private static string GetStreamingToolKey(JObject root){
			return root?.Value<string>("item_id") ?? root?.Value<string>("output_index") ?? root?.Value<string>("id") ?? "0";
		}
		private JObject BuildResponsesPayload(JArray history, float temperature, int maxTokens, string toolsJson, JToken toolChoice, bool stream){
			var payload = new JObject{ ["model"] = _model, ["input"] = history, ["temperature"] = temperature };
			if(!string.IsNullOrWhiteSpace(_instructions)) payload["instructions"] = _instructions;
			payload["store"] = _apiClientStore;
			if(stream) payload["stream"] = true;
			if(maxTokens > 0) payload["max_output_tokens"] = maxTokens;
			var effort = Common.GetReasoningEffort(_reasoningEffort);
			var summaryType = Common.GetReasoningSummaryType(_reasoningSummary);
			if(effort != null || summaryType != null){
				var reasoningPayload = new JObject();
				if(effort != null) reasoningPayload["effort"] = effort;
				if(summaryType != null) reasoningPayload["summary"] = summaryType;
				payload["reasoning"] = reasoningPayload;
			}
			AddToolOptions(payload, toolsJson, toolChoice, false);
			return payload;
		}
		private JObject BuildChatCompletionsPayload(JArray history, float temperature, int maxTokens, string toolsJson, JToken toolChoice, bool stream){
			var messages = ConvertHistoryToChatCompletionMessages(history);
			if(!string.IsNullOrWhiteSpace(_instructions)) messages.Insert(0, new JObject{ ["role"] = "system", ["content"] = _instructions });
			var payload = new JObject{ ["model"] = _model, ["messages"] = messages, ["temperature"] = temperature };
			if(stream) payload["stream"] = true;
			if(maxTokens > 0) payload["max_tokens"] = maxTokens;
			AddToolOptions(payload, toolsJson, toolChoice, true);
			return payload;
		}
		private static void AddToolOptions(JObject payload, string toolsJson, JToken toolChoice, bool chatCompletions){
			if(string.IsNullOrWhiteSpace(toolsJson)) return;
			payload["tools"] = (JToken)NormalizeToolsJson(toolsJson, chatCompletions) ?? JToken.Parse(toolsJson);
			payload["tool_choice"] = toolChoice ?? new JValue("auto");
			payload["parallel_tool_calls"] = true;
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
				if(string.IsNullOrWhiteSpace(message.ToolCallId)) throw new InvalidOperationException(Resources.Tool_call_output_requires_a_call_id_);
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
			if(history == null || result == null) return;
			if(result.OutputItems != null){
				foreach(var item in result.OutputItems) history.Add(ConvertOutputItemToInputItem(item));
				return;
			}
			if(string.IsNullOrWhiteSpace(result.Content) && (result.ToolCalls == null || result.ToolCalls.Count == 0)) return;
			var message = new ChatMessage("assistant", result.Content ?? ""){ ToolCalls = result.ToolCalls };
			foreach(var item in BuildInputItems(new[]{ message })) history.Add(item);
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
		private static JArray NormalizeToolsJson(string toolsJson, bool chatCompletions){
			try{
				var parsed = JToken.Parse(toolsJson);
				if(!(parsed is JArray toolsArray)) return null;
				var normalized = new JArray();
				foreach(var tool in toolsArray.OfType<JObject>()){
					var type = tool.Value<string>("type") ?? "function";
					var function = BuildToolFunction(tool["function"] as JObject ?? tool);
					if(function == null) continue;
					normalized.Add(chatCompletions ? new JObject{ ["type"] = "function", ["function"] = function } : BuildResponsesTool(type, function));
				}
				return normalized.Count > 0 ? normalized : null;
			} catch(JsonException){ return null; }
		}
		private static JObject BuildResponsesTool(string type, JObject function){
			var tool = new JObject{ ["type"] = type, ["name"] = function["name"]?.DeepClone() };
			if(function["description"] != null) tool["description"] = function["description"].DeepClone();
			if(function["parameters"] != null) tool["parameters"] = function["parameters"].DeepClone();
			return tool;
		}
		private static JObject BuildToolFunction(JObject source){
			var name = source?.Value<string>("name");
			if(string.IsNullOrWhiteSpace(name)) return null;
			var function = new JObject{ ["name"] = name };
			var description = source.Value<string>("description");
			if(!string.IsNullOrWhiteSpace(description)) function["description"] = description;
			if(source["parameters"] != null) function["parameters"] = source["parameters"].DeepClone();
			return function;
		}
		internal List<string> GetModels(CancellationToken cancellationToken){
			if(string.IsNullOrWhiteSpace(_apiBaseUrl)) throw new InvalidOperationException(Resources.API_base_URL_is_not_configured_);
			using(var request = CreateRequest(HttpMethod.Get, BuildModelsEndpoint(_apiBaseUrl))){
				using(var response = _apiHttpClient.SendAsync(request, cancellationToken).GetAwaiter().GetResult()){
					var body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
					if(!response.IsSuccessStatusCode) throw new InvalidOperationException(string.Format(Resources.API_error___0_____1_, (int)response.StatusCode, body));
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
		private sealed class StreamingResponseAccumulator{
			private readonly StringBuilder _content = new StringBuilder();
			private readonly StringBuilder _reasoning = new StringBuilder();
			private readonly Dictionary<string, StreamingToolCall> _toolCalls = new Dictionary<string, StreamingToolCall>();
			private ChatCompletionResult _finalResult;
			private string _responseId;
			internal void RememberResponseId(string responseId){
				if(!string.IsNullOrWhiteSpace(responseId)) _responseId = responseId;
			}
			internal void SetFinalResponse(JObject responseObj){
				if(responseObj == null) return;
				RememberResponseId(responseObj.Value<string>("id"));
				try{ _finalResult = APIResponseParser.ParseResponseBody(responseObj.ToString(Formatting.None)); } catch(InvalidOperationException){}
			}
			internal void AppendContent(string delta, Action<string> streamCallback){
				if(string.IsNullOrEmpty(delta)) return;
				_content.Append(delta);
				streamCallback?.Invoke(delta);
			}
			internal void AppendReasoning(string delta){
				if(!string.IsNullOrEmpty(delta)) _reasoning.Append(delta);
			}
			internal void AppendToolArguments(string key, string delta){
				if(string.IsNullOrEmpty(delta)) return;
				GetToolCall(key).Arguments.Append(delta);
			}
			internal void MergeToolCall(string key, JObject item){
				if(item == null) return;
				var type = item.Value<string>("type");
				if(!string.Equals(type, "function_call", StringComparison.OrdinalIgnoreCase) && !string.Equals(type, "tool_call", StringComparison.OrdinalIgnoreCase)) return;
				var toolCall = GetToolCall(key);
				toolCall.Id = item.Value<string>("call_id") ?? item.Value<string>("id") ?? toolCall.Id;
				toolCall.Name = item.Value<string>("name") ?? item["function"]?.Value<string>("name") ?? toolCall.Name;
				var argumentsToken = item["arguments"] ?? item["function"]?["arguments"];
				if(argumentsToken == null) return;
				var arguments = argumentsToken.Type == JTokenType.String ? argumentsToken.ToString() : argumentsToken.ToString(Formatting.None);
				if(toolCall.Arguments.Length == 0 && !string.IsNullOrEmpty(arguments)) toolCall.Arguments.Append(arguments);
			}
			internal void MergeChatToolCall(JObject toolCallDelta){
				if(toolCallDelta == null) return;
				var key = toolCallDelta.Value<string>("index") ?? toolCallDelta.Value<string>("id") ?? "0";
				var toolCall = GetToolCall(key);
				toolCall.Id = toolCallDelta.Value<string>("id") ?? toolCall.Id;
				toolCall.Name = toolCallDelta["function"]?.Value<string>("name") ?? toolCallDelta.Value<string>("name") ?? toolCall.Name;
				var arguments = toolCallDelta["function"]?.Value<string>("arguments") ?? toolCallDelta.Value<string>("arguments");
				if(!string.IsNullOrEmpty(arguments)) toolCall.Arguments.Append(arguments);
			}
			internal ChatCompletionResult BuildResult(){
				if(_finalResult != null) return _finalResult;
				var toolCalls = _toolCalls.Values
					.Where(call => !string.IsNullOrWhiteSpace(call.Id) && !string.IsNullOrWhiteSpace(call.Name))
					.Select(call => new ToolCall(call.Id, call.Name, call.Arguments.ToString()))
					.ToList();
				var finalToolCalls = toolCalls.Count > 0 ? toolCalls : null;
				if(_content.Length == 0 && finalToolCalls == null) throw new InvalidOperationException(Resources.API_response_did_not_contain_any_message_);
				return new ChatCompletionResult(_content.ToString(), _reasoning.Length > 0 ? _reasoning.ToString() : null, finalToolCalls, _responseId, null);
			}
			private StreamingToolCall GetToolCall(string key){
				key = string.IsNullOrWhiteSpace(key) ? "0" : key;
				if(!_toolCalls.TryGetValue(key, out var toolCall)){
					toolCall = new StreamingToolCall();
					_toolCalls[key] = toolCall;
				}
				return toolCall;
			}
		}
		private sealed class StreamingToolCall{
			internal readonly StringBuilder Arguments = new StringBuilder();
			internal string Id;
			internal string Name;
		}
	}
}
