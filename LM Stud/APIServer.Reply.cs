using System;
using System.Linq;
using System.Net;
using System.Text;
namespace LMStud{
	public partial class APIServer{
		private sealed class ServerReply{
			private readonly HttpListenerContext _context;
			private readonly ChatRequest _request;
			private readonly string _model;
			private readonly string _sessionId;
			private readonly bool _store;
			private readonly long _created = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
			private readonly string _messageId = "msg_" + Guid.NewGuid().ToString("N");
			private readonly StringBuilder _text = new StringBuilder();
			private int _sequence;
			private bool _forwardingItems;
			internal readonly string Id;
			internal bool Started{get; private set;}

			internal ServerReply(HttpListenerContext context, ChatRequest request, string model, string sessionId, bool store){
				_context = context;
				_request = request;
				_model = model;
				_sessionId = sessionId;
				_store = store;
				Id = (request.Chat ? "chatcmpl_" : "resp_") + Guid.NewGuid().ToString("N");
				context.Response.AddHeader("X-Session-Id", sessionId);
			}

			internal void TextDelta(string text){
				if(string.IsNullOrEmpty(text)) return;
				StartStream();
				if(_request.Chat) Chunk(Json.Object(Json.P("content", text)));
				else{
					if(_text.Length == 0){
						ItemEvent("response.output_item.added", 0, Message(_messageId, Json.Array(), "in_progress"));
						PartEvent("response.content_part.added", _messageId, 0, 0, TextPart(""));
					}
					TextEvent("response.output_text.delta", _messageId, 0, 0, "delta", text);
				}
				_text.Append(text);
			}
			internal bool ForwardResponseEvent(JsonNode source){
				var type = source.GetString("type");
				if(type == null || !type.StartsWith("response.", StringComparison.Ordinal)) return false;
				if(type == "response.created" || type == "response.in_progress" || type == "response.queued"
					|| type == "response.completed" || type == "response.done" || type == "response.incomplete" || type == "response.failed") return false;
				if(!_forwardingItems){
					if(type != "response.output_item.added" || _text.Length > 0 || string.IsNullOrEmpty(source["item"].GetString("id"))) return false;
					_forwardingItems = true;
				}
				StartStream();
				Event(type, source.Properties().Where(property => property.Name != "type" && property.Name != "sequence_number")
					.Select(property => Json.P(property.Name, property.Value)).ToArray());
				return true;
			}
			internal JsonNode BuildResult(APIClient.ChatCompletionResult result){
				if(_request.Chat){
					var message = Json.ObjectBuilder(Json.P("role", "assistant"), Json.P("content", result.Content), Json.P("refusal", result.Refusal));
					var calls = NativeChat.BuildChatCompletionToolCalls(result.ToolCalls);
					if(calls.Exists){
						message.Set("tool_calls", calls);
						if(string.IsNullOrEmpty(result.Content)) message.Set("content", null);
					}
					if(!string.IsNullOrEmpty(result.Reasoning)) message.Set("reasoning_content", result.Reasoning);
					return Json.Object(Json.P("id", Id), Json.P("object", "chat.completion"), Json.P("created", _created),
						Json.P("model", _model), Json.P("session_id", _sessionId), Json.P("usage", Usage(result, true)),
						Json.P("choices", Json.Array(Json.Object(Json.P("index", 0), Json.P("message", message), Json.P("logprobs", null),
							Json.P("finish_reason", FinishReason(result))))));
				}
				var status = result.Incomplete ? "incomplete" : "completed";
				var output = Json.ArrayBuilder();
				if(_text.Length > 0){
					var parts = Json.ArrayBuilder();
					parts.Add(TextPart(result.Content));
					if(result.Refusal != null) parts.Add(RefusalPart(result.Refusal));
					output.Add(Message(_messageId, parts.ToNode(), status));
				}
				if(result.OutputItems.IsArray && result.OutputItems.Count > 0){
					foreach(var item in result.OutputItems){
						var type = item.GetString("type");
						if(_text.Length > 0 && type == "message") continue;
						output.Add(WithItemId(item, status));
					}
				} else{
					if(_text.Length == 0 && (!string.IsNullOrEmpty(result.Content) || result.Refusal != null)){
						var parts = Json.ArrayBuilder();
						if(!string.IsNullOrEmpty(result.Content)) parts.Add(TextPart(result.Content));
						if(result.Refusal != null) parts.Add(RefusalPart(result.Refusal));
						output.Add(Message(_messageId, parts.ToNode(), status));
					}
					foreach(var call in result.ToolCalls ?? Enumerable.Empty<APIClient.ToolCall>())
						output.Add(Json.Object(Json.P("id", "fc_" + Guid.NewGuid().ToString("N")), Json.P("type", "function_call"), Json.P("status", status),
							Json.P("call_id", call.Id), Json.P("name", call.Name), Json.P("arguments", call.Arguments)));
				}
				var incomplete = result.Incomplete ? Json.Object(Json.P("reason", result.IncompleteReason ?? (result.FinishReason == "content_filter" ? "content_filter" : "max_output_tokens"))) : JsonNode.Null;
				return Response(status, output.ToNode(), Usage(result, false), JsonNode.Null, incomplete);
			}

			internal void Finish(APIClient.ChatCompletionResult result, JsonNode payload){
				if(!_request.Stream){ WriteJson(_context, payload); return; }
				StartStream();
				if(_request.Chat){
					FinishChat(result);
					return;
				}
				if(!_forwardingItems){
					var index = 0;
					foreach(var item in payload["output"]){
						FinishItem(item, index, _text.Length > 0 && index == 0);
						index++;
					}
				}
				Event(result.Incomplete ? "response.incomplete" : "response.completed", Json.P("response", payload));
			}
			private void FinishChat(APIClient.ChatCompletionResult result){
				var streamed = _text.ToString();
				if(!string.IsNullOrEmpty(result.Content) && result.Content.StartsWith(streamed, StringComparison.Ordinal)) TextDelta(result.Content.Substring(streamed.Length));
				if(!string.IsNullOrEmpty(result.Reasoning)) Chunk(Json.Object(Json.P("reasoning_content", result.Reasoning)));
				if(result.Refusal != null) Chunk(Json.Object(Json.P("refusal", result.Refusal)));
				if(result.ToolCalls != null){
					var calls = Json.ArrayBuilder();
					foreach(var call in result.ToolCalls)
						calls.Add(Json.Object(Json.P("index", calls.Count), Json.P("id", call.Id), Json.P("type", "function"),
							Json.P("function", Json.Object(Json.P("name", call.Name), Json.P("arguments", call.Arguments ?? "")))));
					if(calls.Count > 0) Chunk(Json.Object(Json.P("tool_calls", calls)));
				}
				Chunk(Json.Object(), FinishReason(result));
				var usage = Usage(result, true);
				if(_request.IncludeUsage && !usage.IsNull)
					Data(null, ChatChunk(Json.Array(), usage).ToJson());
				Data(null, "[DONE]");
			}
			private void FinishItem(JsonNode item, int index, bool wasStreaming){
				var type = item.GetString("type");
				var id = item.GetString("id");
				if(!wasStreaming){
					var initial = Copy(item);
					if(type == "message") initial.Set("content", Json.Array());
					if(type == "function_call") initial.Set("arguments", "");
					if(type == "message" || type == "function_call") initial.Set("status", "in_progress");
					ItemEvent("response.output_item.added", index, initial.ToNode());
				}
				if(type == "message"){
					var contentIndex = 0;
					foreach(var part in item["content"]){
						var refusal = part.GetString("type") == "refusal";
						if(!refusal && part.GetString("type") != "output_text"){ contentIndex++; continue; }
						var partStarted = wasStreaming && contentIndex == 0;
						var kind = refusal ? "refusal" : "output_text";
						var textField = refusal ? "refusal" : "text";
						var text = part.GetString(textField) ?? "";
						if(!partStarted) PartEvent("response.content_part.added", id, index, contentIndex, refusal ? RefusalPart("") : TextPart(""));
						var delta = partStarted && text.StartsWith(_text.ToString(), StringComparison.Ordinal) ? text.Substring(_text.Length) : partStarted ? "" : text;
						if(delta.Length > 0) TextEvent("response." + kind + ".delta", id, index, contentIndex, "delta", delta);
						TextEvent("response." + kind + ".done", id, index, contentIndex, textField, text);
						PartEvent("response.content_part.done", id, index, contentIndex++, part);
					}
				} else if(type == "function_call"){
					var arguments = item.GetString("arguments") ?? "";
					if(arguments.Length > 0) Event("response.function_call_arguments.delta", Json.P("item_id", id), Json.P("output_index", index), Json.P("delta", arguments));
					Event("response.function_call_arguments.done", Json.P("item_id", id), Json.P("output_index", index),
						Json.P("name", item.GetString("name")), Json.P("arguments", arguments));
				}
				ItemEvent("response.output_item.done", index, item);
			}
			internal void Fail(string code, string message){ Fail(500, code, message); }
			internal void Fail(int status, string code, string message, string parameter = null){
				if(!Started){ Error(_context, status, code, message, parameter); return; }
				var error = ErrorObject(status, code, message, parameter);
				if(_request.Chat) Data(null, Json.Object(Json.P("error", error)).ToJson());
				else Event("response.failed", Json.P("response", Response("failed", Json.Array(), JsonNode.Null, error, JsonNode.Null)));
			}

			private void StartStream(){
				if(Started) return;
				var response = _context.Response;
				response.ContentType = "text/event-stream; charset=utf-8";
				response.SendChunked = true;
				response.Headers[HttpResponseHeader.CacheControl] = "no-cache";
				Started = true;
				if(_request.Chat) Chunk(Json.Object(Json.P("role", "assistant"), Json.P("content", "")));
				else{
					var state = Response("in_progress", Json.Array(), JsonNode.Null, JsonNode.Null, JsonNode.Null);
					Event("response.created", Json.P("response", state));
					Event("response.in_progress", Json.P("response", state));
				}
			}
			private void ItemEvent(string type, int index, JsonNode item){
				Event(type, Json.P("output_index", index), Json.P("item", item));
			}
			private void PartEvent(string type, string itemId, int index, int part, JsonNode content){
				Event(type, Json.P("item_id", itemId), Json.P("output_index", index), Json.P("content_index", part), Json.P("part", content));
			}
			private void TextEvent(string type, string itemId, int index, int part, string field, string text){
				Event(type, Json.P("item_id", itemId), Json.P("output_index", index), Json.P("content_index", part), Json.P(field, text), Json.P("logprobs", Json.Array()));
			}
			private void Event(string type, params JsonMember[] fields){
				var node = Json.ObjectBuilder(Json.P("type", type), Json.P("sequence_number", _sequence++));
				foreach(var field in fields) node.Set(field.Name, field.Value);
				Data(type, node.ToJson());
			}
			private void Chunk(JsonNode delta, string finish = null){
				var choice = Json.Object(Json.P("index", 0), Json.P("delta", delta), Json.P("finish_reason", finish), Json.P("logprobs", null));
				Data(null, ChatChunk(Json.Array(choice), JsonNode.Null).ToJson());
			}
			private JsonNode ChatChunk(JsonNode choices, JsonNode usage){
				var node = Json.ObjectBuilder(Json.P("id", Id), Json.P("object", "chat.completion.chunk"),
					Json.P("created", _created), Json.P("model", _model), Json.P("choices", choices));
				if(_request.IncludeUsage) node.Set("usage", usage);
				return node.ToNode();
			}
			private void Data(string type, string data){
				var bytes = Encoding.UTF8.GetBytes((type == null ? "" : "event: " + type + "\n") + "data: " + data + "\n\n");
				var stream = _context.Response.OutputStream;
				stream.Write(bytes, 0, bytes.Length);
				stream.Flush();
			}
			private JsonNode Response(string status, JsonNode output, JsonNode usage, JsonNode error, JsonNode incomplete){
				return Json.Object(Json.P("id", Id), Json.P("object", "response"), Json.P("created_at", _created), Json.P("model", _model),
					Json.P("status", status), Json.P("error", error), Json.P("incomplete_details", incomplete), Json.P("output", output), Json.P("usage", usage),
					Json.P("previous_response_id", _request.PreviousId), Json.P("store", _store), Json.P("session_id", _sessionId), Json.P("max_output_tokens", _request.MaxTokens),
					Json.P("instructions", _request.Instructions), Json.P("parallel_tool_calls", _request.ParallelToolCalls ?? true),
					Json.P("temperature", _request.Temperature), Json.P("top_p", _request.TopP), Json.P("metadata", _request.Body["metadata"].IsObject ? _request.Body["metadata"] : Json.Object()),
					Json.P("tools", _request.Tools.IsArray ? _request.Tools : Json.Array()), Json.P("tool_choice", _request.ToolChoice ?? Json.String("auto")));
			}
			private static JsonNode Usage(APIClient.ChatCompletionResult result, bool chat){
				if(!result.InputTokens.HasValue || !result.OutputTokens.HasValue) return JsonNode.Null;
				return Json.Object(Json.P(chat ? "prompt_tokens" : "input_tokens", result.InputTokens),
					Json.P(chat ? "completion_tokens" : "output_tokens", result.OutputTokens),
					Json.P("total_tokens", result.TotalTokens ?? (long)result.InputTokens.Value + result.OutputTokens.Value));
			}
			private static string FinishReason(APIClient.ChatCompletionResult result){
				if(result.FinishReason != null) return result.FinishReason;
				if(result.Incomplete) return result.IncompleteReason == "content_filter" ? "content_filter" : "length";
				return result.ToolCalls?.Count > 0 ? "tool_calls" : "stop";
			}
			private static JsonNode Message(string id, JsonNode content, string status){
				return Json.Object(Json.P("id", id), Json.P("type", "message"), Json.P("role", "assistant"), Json.P("status", status), Json.P("content", content));
			}
			private static JsonNode TextPart(string text){
				return Json.Object(Json.P("type", "output_text"), Json.P("text", text ?? ""), Json.P("annotations", Json.Array()), Json.P("logprobs", Json.Array()));
			}
			private static JsonNode RefusalPart(string text){return Json.Object(Json.P("type", "refusal"), Json.P("refusal", text));}
			private static JsonNode WithItemId(JsonNode item, string status){
				if(!item.IsObject || !item["id"].IsNull) return item;
				var type = item.GetString("type");
				var builder = Copy(item);
				var prefix = type == "message" ? "msg_" : type == "function_call" ? "fc_" : "item_";
				builder.Set("id", prefix + Guid.NewGuid().ToString("N"));
				if(item["status"].IsNull && (type == "message" || type == "function_call")) builder.Set("status", status);
				if(type == "message"){
					var parts = Json.ArrayBuilder();
					foreach(var part in item["content"]){
						if(part.GetString("type") == "output_text" && part["annotations"].IsNull){
							var text = Copy(part);
							text.Set("annotations", Json.Array());
							parts.Add(text);
						} else parts.Add(part);
					}
					if(item["content"].IsArray) builder.Set("content", parts);
				}
				return builder.ToNode();
			}
			private static JsonObjectBuilder Copy(JsonNode node){
				var builder = Json.ObjectBuilder();
				foreach(var property in node.Properties()) builder.Set(property.Name, property.Value);
				return builder;
			}
		}
	}
}
