using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using LMStud.Parsers;
using LMStud.Properties;
namespace LMStud{
	internal enum ApiMode{
		Auto,
		Responses,
		ChatCompletions
	}
	internal sealed class APIClient{
		private readonly string _apiKey;
		private readonly Uri _baseUri;
		private readonly bool _explicitStore;
		private readonly string _instructions;
		private readonly ApiMode _mode;
		private readonly string _model;
		private readonly int _reasoningEffort;
		private readonly int _reasoningSummary;
		private readonly bool _store;
		private bool _chatFallback;
		internal APIClient(string apiBaseUrl,
							string apiKey,
							string model,
							bool store,
							string instructions = null,
							int reasoningEffort = 0,
							int reasoningSummary = 0,
							ApiMode mode = ApiMode.Auto,
							bool explicitStore = false){
			if(!Uri.TryCreate(apiBaseUrl?.Trim(), UriKind.Absolute, out var uri) || (uri.Scheme != "http" && uri.Scheme != "https"))
				throw new InvalidOperationException(Resources.API_base_URL_is_not_configured_);
			var path = uri.AbsolutePath.TrimEnd('/');
			if(path.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase)){
				path = path.Substring(0, path.Length - "/chat/completions".Length);
				if(mode == ApiMode.Auto) mode = ApiMode.ChatCompletions;
			} else if(path.EndsWith("/responses", StringComparison.OrdinalIgnoreCase)){
				path = path.Substring(0, path.Length - "/responses".Length);
				if(mode == ApiMode.Auto) mode = ApiMode.Responses;
			}
			_baseUri = new UriBuilder(uri){ Path = path.Length == 0 ? "/v1" : path, Fragment = "" }.Uri;
			_store = store;
			_apiKey = apiKey?.Trim();
			_model = model?.Trim() ?? "";
			_instructions = instructions;
			_reasoningEffort = reasoningEffort;
			_reasoningSummary = reasoningSummary;
			_mode = mode;
			_explicitStore = explicitStore;
		}
		private string ModelFamily => _model.Substring(_model.LastIndexOf('/') + 1).ToLowerInvariant();
		private bool IsGpt62 => ModelFamily.StartsWith("gpt-6.2", StringComparison.Ordinal);
		private bool IsOSeries => ModelFamily.Length > 1 && ModelFamily[0] == 'o' && char.IsDigit(ModelFamily[1]);
		private bool IsOpenAi => _baseUri.Host.Equals("api.openai.com", StringComparison.OrdinalIgnoreCase);
		internal ChatCompletionResult CreateChatCompletion(JsonArrayBuilder history,
															float? temperature,
															int? maxTokens,
															string toolsJson,
															JsonNode? toolChoice,
															CancellationToken cancellationToken,
															Action<string> streamCallback = null,
															bool? parallelToolCalls = null,
															float? topP = null,
															JsonNode textFormat = default,
															bool includeUsage = false,
															Func<JsonNode, bool> responseEvent = null){
			if(history == null) throw new InvalidOperationException(Resources.History_is_not_configured_);
			var tools = string.IsNullOrWhiteSpace(toolsJson) ? JsonNode.Missing : Json.Parse(toolsJson);
			var requiresResponses = tools.IsArray
									&& tools.Any(tool => {
										var type = tool.GetString("type") ?? "function";
										return type != "function";
									});
			requiresResponses |= IsGpt62 && tools.Count > 0;
			if(_mode != ApiMode.ChatCompletions && (!_chatFallback || requiresResponses)){
				var responses = BuildPayload(history, temperature, maxTokens, tools, toolChoice, streamCallback != null, parallelToolCalls, topP, textFormat, false, includeUsage);
				try{ return Send("POST", "responses", responses, cancellationToken, streamCallback, (reader, token, callback) => ReadCompletion(reader, token, callback, responseEvent)); } catch
					(RequestException ex) when(_mode == ApiMode.Auto && !requiresResponses && ex.EndpointUnavailable){ _chatFallback = true; }
			}
			if(requiresResponses) throw new InvalidOperationException("The selected tools require the Responses API.");
			var chat = BuildPayload(history, temperature, maxTokens, tools, toolChoice, streamCallback != null, parallelToolCalls, topP, textFormat, true, includeUsage);
			return Send("POST", "chat/completions", chat, cancellationToken, streamCallback, (reader, token, callback) => ReadCompletion(reader, token, callback, null));
		}
		private T Send<T>(string method, string resource, JsonNode payload, CancellationToken token, Action<string> streamCallback, Func<StreamReader, CancellationToken, Action<string>, T> read){
			token.ThrowIfCancellationRequested();
			var endpoint = new UriBuilder(_baseUri){ Path = _baseUri.AbsolutePath.TrimEnd('/') + "/" + resource }.Uri;
			var request = (HttpWebRequest)WebRequest.Create(endpoint);
			request.Method = method;
			request.Timeout = 300000;
			request.ReadWriteTimeout = 120000;
			request.AllowAutoRedirect = false;
			if(endpoint.IsLoopback) request.Proxy = null;
			request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
			request.Accept = streamCallback == null ? "application/json" : "text/event-stream, application/json";
			if(!string.IsNullOrWhiteSpace(_apiKey)) request.Headers[HttpRequestHeader.Authorization] = "Bearer " + _apiKey;
			using(token.Register(request.Abort)){
				try{
					if(method == "POST"){
						var bytes = Encoding.UTF8.GetBytes(payload.ToJson());
						request.ContentType = "application/json; charset=utf-8";
						request.ContentLength = bytes.Length;
						using(var output = request.GetRequestStream()){ output.Write(bytes, 0, bytes.Length); }
					}
					using(var response = (HttpWebResponse)request.GetResponse()){
						using(var reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8)){
							if((int)response.StatusCode >= 300) throw new RequestException((int)response.StatusCode, "");
							var result = read(reader, token, streamCallback);
							token.ThrowIfCancellationRequested();
							return result;
						}
					}
				} catch(WebException ex) when(ex.Response is HttpWebResponse){
					using(var response = (HttpWebResponse)ex.Response){
						token.ThrowIfCancellationRequested();
						using(var reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8)){
							var buffer = new char[8192];
							throw new RequestException((int)response.StatusCode, new string(buffer, 0, reader.ReadBlock(buffer, 0, buffer.Length)));
						}
					}
				} catch(Exception) when(token.IsCancellationRequested){ throw new OperationCanceledException(token); }
			}
		}
		private static ChatCompletionResult ReadCompletion(StreamReader reader, CancellationToken token, Action<string> callback, Func<JsonNode, bool> responseEvent){
			if(callback == null) return APIResponseParser.ParseResponseBody(reader.ReadToEnd());
			var response = new StreamingResponse();
			var data = new StringBuilder();
			var body = new StringBuilder();
			var eventName = "";
			var sawData = false;
			string line;
			while((line = reader.ReadLine()) != null){
				token.ThrowIfCancellationRequested();
				if(line.Length == 0){
					if(response.Process(eventName, data.ToString(), callback, responseEvent)) return response.Result();
					data.Clear();
					eventName = "";
				} else if(line.StartsWith("event:", StringComparison.Ordinal)){ eventName = line.Substring(6).Trim(); } else if(line.StartsWith("data:", StringComparison.Ordinal)){
					if(data.Length > 0) data.Append('\n');
					data.Append(line.Length > 5 && line[5] == ' ' ? line.Substring(6) : line.Substring(5));
					sawData = true;
				} else if(!sawData && line[0] != ':'){ body.AppendLine(line); }
			}
			token.ThrowIfCancellationRequested();
			if(!sawData && body.Length > 0) return APIResponseParser.ParseResponseBody(body.ToString());
			if(data.Length > 0) response.Process(eventName, data.ToString(), callback, responseEvent);
			return response.Result();
		}
		private JsonNode BuildPayload(JsonArrayBuilder history,
									float? temperature,
									int? maxTokens,
									JsonNode tools,
									JsonNode? choice,
									bool stream,
									bool? parallelToolCalls,
									float? topP,
									JsonNode textFormat,
									bool chat,
									bool includeUsage){
			var payload = Json.ObjectBuilder(Json.P("model", _model));
			var effort = Common.GetReasoningEffort(_reasoningEffort);
			if(IsGpt62 && effort == "none") effort = "low";
			if(!IsGpt62 && !IsOSeries && (!ModelFamily.StartsWith("gpt-5", StringComparison.Ordinal) || effort == "none")){
				if(temperature.HasValue) payload.Set("temperature", temperature.Value);
				if(topP.HasValue) payload.Set("top_p", topP.Value);
			}
			if(stream) payload.Set("stream", true);
			if(chat){
				var messages = ConvertHistoryToChatCompletionMessages(history);
				if(!string.IsNullOrWhiteSpace(_instructions))
					messages.Insert(0,
									Json.Object(Json.P("role", IsGpt62 || IsOSeries || ModelFamily.StartsWith("gpt-5", StringComparison.Ordinal) ? "developer" : "system"),
												Json.P("content", _instructions)));
				payload.Set("messages", messages);
				var modernLimit = IsOpenAi
								|| IsGpt62
								|| IsOSeries
								|| ModelFamily.StartsWith("gpt-5", StringComparison.Ordinal)
								|| _baseUri.Host.Equals("generativelanguage.googleapis.com", StringComparison.OrdinalIgnoreCase);
				if(maxTokens > 0) payload.Set(modernLimit ? "max_completion_tokens" : "max_tokens", maxTokens.Value);
				if(effort != null) payload.Set("reasoning_effort", effort);
				if(IsOpenAi || _store || _explicitStore) payload.Set("store", _store);
				if(stream && (IsOpenAi || includeUsage)) payload.Set("stream_options", Json.Object(Json.P("include_usage", true)));
			} else{
				payload.Set("input", history);
				if(!string.IsNullOrWhiteSpace(_instructions)) payload.Set("instructions", _instructions);
				payload.Set("store", _store);
				if(maxTokens > 0) payload.Set("max_output_tokens", maxTokens.Value);
				var summary = Common.GetReasoningSummaryType(_reasoningSummary);
				if(effort != null || summary != null){
					var reasoning = Json.ObjectBuilder();
					if(effort != null) reasoning.Set("effort", effort);
					if(summary != null) reasoning.Set("summary", summary);
					payload.Set("reasoning", reasoning);
				}
				if(IsOpenAi && !_store && (IsGpt62 || IsOSeries || ModelFamily.StartsWith("gpt-5", StringComparison.Ordinal))) payload.Set("include", Json.Array("reasoning.encrypted_content"));
			}
			if(!textFormat.IsNull){
				var format = ConvertTextFormat(textFormat, chat);
				payload.Set(chat ? "response_format" : "text", chat ? format : Json.Object(Json.P("format", format)));
			}
			if(tools.Exists && !tools.IsNull){
				if(!tools.IsArray) throw new InvalidOperationException("tools must be an array.");
				if(tools.Count > 0) payload.Set("tools", Json.Array(tools.Select(tool => ConvertNamedTool(tool, chat)).Cast<object>().ToArray()));
			}
			if(!chat || !IsGpt62){
				if(choice.HasValue && !choice.Value.IsNull) payload.Set("tool_choice", ConvertToolChoice(choice.Value, chat));
				if(parallelToolCalls.HasValue) payload.Set("parallel_tool_calls", parallelToolCalls.Value);
			}
			return payload.ToNode();
		}
		private static JsonNode ConvertNamedTool(JsonNode tool, bool chat){
			var type = tool.GetString("type") ?? "function";
			if(type != "function" && type != "custom") return tool;
			var definition = tool[type].IsObject ? tool[type] : tool;
			var fields = Json.ObjectBuilder();
			if(!chat) fields.Set("type", type);
			foreach(var property in definition.Properties())
				if(property.Name != "type")
					fields.Set(property.Name, property.Value);
			return chat ? Json.Object(Json.P("type", type), Json.P(type, fields)) : fields.ToNode();
		}
		private static JsonNode ConvertToolChoice(JsonNode choice, bool chat){
			if(choice.GetString("type") != "allowed_tools") return choice.IsObject ? ConvertNamedTool(choice, chat) : choice;
			var options = choice["allowed_tools"].IsObject ? choice["allowed_tools"] : choice;
			var tools = Json.Array(options["tools"].Select(tool => ConvertNamedTool(tool, chat)).Cast<object>().ToArray());
			var builder = Json.ObjectBuilder(Json.P("mode", options["mode"]), Json.P("tools", tools));
			if(chat) return Json.Object(Json.P("type", "allowed_tools"), Json.P("allowed_tools", builder));
			builder.Set("type", "allowed_tools");
			return builder.ToNode();
		}
		private static JsonNode ConvertTextFormat(JsonNode format, bool chat){
			if(format.GetString("type") != "json_schema") return format;
			var schema = format["json_schema"].IsObject ? format["json_schema"] : format;
			var builder = Json.ObjectBuilder();
			if(!chat) builder.Set("type", "json_schema");
			foreach(var property in schema.Properties())
				if(property.Name != "type")
					builder.Set(property.Name, property.Value);
			return chat ? Json.Object(Json.P("type", "json_schema"), Json.P("json_schema", builder)) : builder.ToNode();
		}
		internal static JsonArrayBuilder ConvertHistoryToChatCompletionMessages(IEnumerable<JsonNode> history){
			var messages = Json.ArrayBuilder();
			JsonObjectBuilder assistant = null;
			JsonArrayBuilder calls = null;
			void FlushAssistant(){
				if(assistant == null) return;
				if(calls != null) assistant.Set("tool_calls", calls);
				messages.Add(assistant);
				assistant = null;
				calls = null;
			}
			foreach(var item in history ?? Enumerable.Empty<JsonNode>()){
				var type = item.GetString("type");
				if(type == "reasoning") continue;
				if(type == "function_call"){
					if(assistant == null) assistant = Json.ObjectBuilder(Json.P("role", "assistant"), Json.P("content", null));
					if(calls == null) calls = Json.ArrayBuilder();
					calls.Add(Json.Object(Json.P("id", item.GetString("call_id")),
										Json.P("type", "function"),
										Json.P("function", Json.Object(Json.P("name", item.GetString("name")), Json.P("arguments", item.GetString("arguments") ?? "")))));
					continue;
				}
				var role = item.GetString("role");
				if(type != "function_call_output" && string.IsNullOrWhiteSpace(role)) continue;
				FlushAssistant();
				if(type == "function_call_output"){
					messages.Add(Json.Object(Json.P("role", "tool"),
											Json.P("tool_call_id", item.GetString("call_id")),
											Json.P("content", MarkdownImages.ConvertResponsesContentToChat(item["output"]))));
					continue;
				}
				var message = Json.ObjectBuilder(Json.P("role", role), Json.P("content", MarkdownImages.ConvertResponsesContentToChat(item["content"])));
				if(role == "assistant") assistant = message;
				else messages.Add(message);
			}
			FlushAssistant();
			return messages;
		}
		internal static JsonArrayBuilder ConvertChatMessagesToInput(JsonNode messages){
			if(!messages.IsArray) throw new InvalidOperationException("messages must be an array.");
			var items = Json.ArrayBuilder();
			foreach(var message in messages){
				var role = message.GetString("role");
				if(string.IsNullOrWhiteSpace(role)) throw new InvalidOperationException("Every message needs a role.");
				var content = ConvertChatContentToInput(message["content"], role == "assistant");
				if(role == "tool"){
					var id = message.GetString("tool_call_id");
					if(string.IsNullOrWhiteSpace(id)) throw new InvalidOperationException(Resources.Tool_call_output_requires_a_call_id_);
					items.Add(Json.Object(Json.P("type", "function_call_output"), Json.P("call_id", id), Json.P("output", content)));
					continue;
				}
				var calls = message["tool_calls"];
				if(!content.IsNull || calls.Count == 0) items.Add(Json.Object(Json.P("role", role), Json.P("content", content.IsNull ? Json.String("") : content)));
				foreach(var call in calls){
					var name = call["function"].GetString("name");
					var id = call.GetString("id");
					if(string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(id)) throw new InvalidOperationException("Each function call needs a name and ID.");
					items.Add(Json.Object(Json.P("type", "function_call"), Json.P("name", name), Json.P("call_id", id), Json.P("arguments", call["function"].GetString("arguments") ?? "")));
				}
			}
			return items;
		}
		private static JsonNode ConvertChatContentToInput(JsonNode content, bool assistant){
			if(!content.IsArray) return content;
			var parts = Json.ArrayBuilder();
			foreach(var part in content){
				var type = part.GetString("type");
				if(type == "text"){ parts.Add(Json.Object(Json.P("type", assistant ? "output_text" : "input_text"), Json.P("text", part["text"]))); } else if(type == "image_url"){
					var image = part["image_url"];
					var builder = Json.ObjectBuilder(Json.P("type", "input_image"), Json.P("image_url", image.GetString("url")));
					if(image["detail"].Exists) builder.Set("detail", image["detail"]);
					parts.Add(builder);
				} else{ parts.Add(part); }
			}
			return parts.ToNode();
		}
		internal static JsonNode BuildInputMessagePayload(ChatMessage message){
			var isToolRole = string.Equals(message.Role, "tool", StringComparison.OrdinalIgnoreCase);
			if(isToolRole){
				if(string.IsNullOrWhiteSpace(message.ToolCallId)) throw new InvalidOperationException(Resources.Tool_call_output_requires_a_call_id_);
				return Json.Object(Json.P("type", "function_call_output"), Json.P("call_id", message.ToolCallId), Json.P("output", message.Content ?? ""));
			}
			if(string.Equals(message.Role, "user", StringComparison.OrdinalIgnoreCase)){
				var visionContent = MarkdownImages.BuildResponsesContent(message.Content ?? "", out var hasImages);
				if(hasImages) return Json.Object(Json.P("role", message.Role), Json.P("content", visionContent));
			}
			return Json.Object(Json.P("role", message.Role), Json.P("content", message.Content ?? ""));
		}
		internal static JsonArrayBuilder BuildInputItems(IEnumerable<ChatMessage> messages){
			if(messages == null) return Json.ArrayBuilder();
			var items = Json.ArrayBuilder();
			foreach(var message in messages.Where(message => message != null)){
				if(message.OutputItems.IsArray && message.OutputItems.Count > 0){
					foreach(var item in message.OutputItems) items.Add(item);
					continue;
				}
				var hasToolCalls = message.ToolCalls != null && message.ToolCalls.Count > 0;
				var hasContent = !string.IsNullOrWhiteSpace(message.Content);
				var isToolRole = string.Equals(message.Role, "tool", StringComparison.OrdinalIgnoreCase);
				if(isToolRole || hasContent || !hasToolCalls) items.Add(BuildInputMessagePayload(message));
				if(message.ToolCalls == null) continue;
				foreach(var toolCall in message.ToolCalls){
					if(toolCall == null) continue;
					if(string.IsNullOrWhiteSpace(toolCall.Id) || string.IsNullOrWhiteSpace(toolCall.Name)) continue;
					var toolItem = Json.Object(Json.P("type", "function_call"), Json.P("call_id", toolCall.Id), Json.P("name", toolCall.Name), Json.P("arguments", toolCall.Arguments ?? ""));
					items.Add(toolItem);
				}
			}
			return items;
		}
		internal static void AppendOutputItems(JsonArrayBuilder history, ChatCompletionResult result){
			if(result.OutputItems.IsArray && result.OutputItems.Count > 0){
				foreach(var item in result.OutputItems)
					if(!result.Incomplete || (item.GetString("type") != "function_call" && item.GetString("type") != "custom_tool_call"))
						history.Add(item);
				return;
			}
			if(!string.IsNullOrWhiteSpace(result.Content) || result.ToolCalls?.Count > 0)
				foreach(var item in BuildInputItems(new[]{ new ChatMessage("assistant", result.Content ?? ""){ ToolCalls = result.ToolCalls } }))
					history.Add(item);
		}
		internal List<string> GetModels(CancellationToken token){
			return Send("GET",
						"models",
						JsonNode.Missing,
						token,
						null,
						(reader, _, callback) => Json.Parse(reader.ReadToEnd())["data"].Select(item => item.GetString("id")).Where(id => !string.IsNullOrWhiteSpace(id)).ToList());
		}
		internal static void ShowApiClientError(string action, Exception exception){
			var detail = exception.Message;
			if(exception is RequestException request)
				switch(request.StatusCode){
					case 401:
					case 403:
						detail = Resources.Authentication_failed;
						break;
					case 404 when request.EndpointUnavailable:
						detail = Resources.API_endpoint_not_found;
						break;
					case 429:
						detail = Resources.Rate_limit_exceeded;
						break;
					case 500:
					case 502:
					case 503:
						detail = Resources.The_API_server_encountered_an_error;
						break;
				}
			else if(exception is WebException web) detail = web.Status == WebExceptionStatus.Timeout ? Resources.The_API_request_timed_out : Resources.Unable_to_connect_to_the_API_server;
			if(!string.Equals(detail, exception.Message, StringComparison.Ordinal)) detail += "\r\n\r\n" + exception.Message;
			Program.MainForm.ShowError(action, detail, false);
		}
		internal sealed class RequestException : InvalidOperationException{
			internal RequestException(int status, string body) : base(string.Format(Resources.API_error___0_____1_, status, body)){
				StatusCode = status;
				var error = JsonNode.Missing;
				try{
					error = Json.Parse(body)["error"];
					Code = error.GetString("code");
					Parameter = error.GetString("param");
					UpstreamMessage = error.GetString("message") ?? (error.IsString ? error.AsString() : null);
				} catch(InvalidOperationException){}
				if(status == 405 || status == 501){
					EndpointUnavailable = true;
					return;
				}
				if(status != 404) return;
				if(error.Exists){
					EndpointUnavailable = Code == "endpoint_not_found" || Code == "unsupported_endpoint" || Code == "route_not_found";
					return;
				}
				body = body.Trim();
				EndpointUnavailable = body.Length == 0 || body.Equals("404 page not found", StringComparison.OrdinalIgnoreCase) || body.Equals("Not Found", StringComparison.OrdinalIgnoreCase);
			}
			internal int StatusCode{get;}
			internal bool EndpointUnavailable{get;}
			internal string Code{get;}
			internal string Parameter{get;}
			internal string UpstreamMessage{get;}
		}
		internal sealed class ChatMessage{
			public string Content;
			public JsonNode OutputItems;
			public string Role;
			public string ToolCallId;
			public List<ToolCall> ToolCalls;
			public ChatMessage(string role, string content){
				Role = role;
				Content = content;
			}
		}
		[DataContract]
		internal sealed class ToolCall{
			[DataMember(Name = "arguments")] public string Arguments;
			[DataMember(Name = "id")] public string Id;
			[DataMember(Name = "name")] public string Name;
			public ToolCall(string id, string name, string arguments){
				Id = id;
				Name = name;
				Arguments = arguments;
			}
		}
		internal sealed class ChatCompletionResult{
			public string Content;
			public string FinishReason;
			public string IncompleteReason;
			public int? InputTokens;
			public JsonNode OutputItems;
			public int? OutputTokens;
			public string Reasoning;
			public string Refusal;
			public string ResponseId;
			public List<ToolCall> ToolCalls;
			public int? TotalTokens;
			public ChatCompletionResult(string content, string reasoning, List<ToolCall> toolCalls, string responseId, JsonNode outputItems, int? totalTokens = null){
				Content = content;
				Reasoning = reasoning;
				ToolCalls = toolCalls;
				ResponseId = responseId;
				OutputItems = outputItems;
				TotalTokens = totalTokens;
			}
			internal bool Incomplete => IncompleteReason != null || FinishReason == "length" || FinishReason == "content_filter";
			internal void ReadUsage(JsonNode usage){
				InputTokens = usage.GetInt("input_tokens") ?? usage.GetInt("prompt_tokens");
				OutputTokens = usage.GetInt("output_tokens") ?? usage.GetInt("completion_tokens");
				TotalTokens = usage.GetInt("total_tokens") ?? TotalTokens;
			}
		}
		private sealed class StreamingResponse{
			private readonly SortedDictionary<int, ToolCallDelta> _calls = new SortedDictionary<int, ToolCallDelta>();
			private readonly SortedDictionary<int, JsonNode> _items = new SortedDictionary<int, JsonNode>();
			private readonly StringBuilder _reasoning = new StringBuilder();
			private readonly StringBuilder _refusal = new StringBuilder();
			private readonly StringBuilder _text = new StringBuilder();
			private ChatCompletionResult _final;
			private string _finishReason;
			private string _id;
			private bool _responses;
			private bool _terminal;
			private JsonNode _usage = JsonNode.Missing;
			internal bool Process(string eventName, string data, Action<string> callback, Func<JsonNode, bool> responseEvent){
				if(string.IsNullOrWhiteSpace(data)) return false;
				if(data.Trim() == "[DONE]"){
					_terminal = true;
					return true;
				}
				var root = Json.Parse(data);
				var type = root.GetString("type") ?? eventName;
				_responses |= type.StartsWith("response.", StringComparison.Ordinal);
				var forwarded = responseEvent != null && responseEvent(root);
				_id = root.GetString("id") ?? root["response"].GetString("id") ?? _id;
				var itemIndex = root.GetInt("output_index") ?? 0;
				switch(type){
					case "error":
					case "response.failed":
						throw new InvalidOperationException(root["response"]["error"].GetString("message")
															?? root["error"].GetString("message") ?? root.GetString("message") ?? "The upstream stream failed.");
					case "response.completed":
					case "response.done":
					case "response.incomplete":
						var response = root["response"];
						if(response["usage"].IsObject) _usage = response["usage"];
						_final = response["output"].Count > 0 || response.GetString("status") == "incomplete" ? APIResponseParser.ParseResponseBody(response.ToJson()) : null;
						if(type == "response.incomplete"){
							if(_final == null) _final = new ChatCompletionResult(_text.ToString(), _reasoning.ToString(), null, _id, JsonNode.Missing);
							_final.IncompleteReason = response["incomplete_details"].GetString("reason") ?? "max_output_tokens";
							_final.ReadUsage(_usage);
							_final.ToolCalls = null;
						}
						_terminal = true;
						return true;
					case "response.output_item.done":
						if(root["item"].IsObject) _items[itemIndex] = root["item"];
						break;
					case "response.output_text.delta":
						AppendText(root.GetString("delta"), forwarded ? null : callback);
						break;
					case "response.reasoning_text.delta":
					case "response.reasoning_summary_text.delta":
						_reasoning.Append(root.GetString("delta"));
						break;
					case "response.refusal.delta":
						_refusal.Append(root.GetString("delta"));
						break;
				}
				if(root["usage"].IsObject) _usage = root["usage"];
				foreach(var choice in root["choices"]){
					if((choice.GetInt("index") ?? 0) != 0) continue;
					_finishReason = choice.GetString("finish_reason") ?? _finishReason;
					var delta = choice["delta"];
					AppendText(APIResponseParserCommon.ExtractContentText(delta["content"]), callback);
					_reasoning.Append(delta.GetString("reasoning_content") ?? delta.GetString("reasoning"));
					_refusal.Append(delta.GetString("refusal"));
					foreach(var tool in delta["tool_calls"]){
						var index = tool.GetInt("index") ?? 0;
						if(!_calls.TryGetValue(index, out var call)) _calls[index] = call = new ToolCallDelta();
						var function = tool["function"];
						call.Id = tool.GetString("id") ?? call.Id;
						call.Name = function.GetString("name") ?? call.Name;
						call.Arguments.Append(function.GetString("arguments"));
					}
				}
				return false;
			}
			private void AppendText(string text, Action<string> callback){
				if(string.IsNullOrEmpty(text)) return;
				_text.Append(text);
				callback?.Invoke(text);
			}
			internal ChatCompletionResult Result(){
				if(!_terminal && (_responses || _finishReason == null)) throw new InvalidOperationException("The API stream ended before a completion event.");
				if(_final != null) return _final;
				if(_items.Count > 0) return APIResponseParser.ParseResponseBody(Json.Object(Json.P("id", _id), Json.P("output", Json.Array(_items.Values.Cast<object>().ToArray()))).ToJson());
				var tools = _calls.Values.Where(call => !string.IsNullOrWhiteSpace(call.Id) && !string.IsNullOrWhiteSpace(call.Name))
					.Select(call => new ToolCall(call.Id, call.Name, call.Arguments.ToString())).ToList();
				var result = new ChatCompletionResult(_text.ToString(), _reasoning.ToString(), tools.Count == 0 ? null : tools, _id, JsonNode.Missing){
					FinishReason = _finishReason, Refusal = _refusal.Length == 0 ? null : _refusal.ToString()
				};
				if(result.Incomplete) result.ToolCalls = null;
				result.ReadUsage(_usage);
				return result;
			}
		}
		private sealed class ToolCallDelta{
			internal readonly StringBuilder Arguments = new StringBuilder();
			internal string Id;
			internal string Name;
		}
	}
}