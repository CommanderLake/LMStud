using System;
using System.Collections.Generic;
using System.Linq;
namespace LMStud{
	internal static class NativeChat{
		internal sealed class MessageSnapshot{
			internal readonly string ApiContent;
			internal readonly string ApiToolCallId;
			internal readonly List<APIClient.ToolCall> ApiToolCalls;
			internal readonly string Message;
			internal readonly bool NativeBacked;
			internal readonly MessageRole Role;
			internal readonly string Think;
			internal MessageSnapshot(MessageRole role, string think, string message, string apiContent = null, List<APIClient.ToolCall> apiToolCalls = null, string apiToolCallId = null, bool nativeBacked = true){
				Role = role;
				Think = think ?? "";
				Message = message ?? "";
				ApiContent = apiContent;
				ApiToolCalls = apiToolCalls == null ? null : new List<APIClient.ToolCall>(apiToolCalls);
				ApiToolCallId = apiToolCallId;
				NativeBacked = nativeBacked;
			}
			internal string ApiMessageContent => ApiContent ?? Message ?? "";
			internal MessageSnapshot WithText(string think, string message){ return new MessageSnapshot(Role, think, message, null, ApiToolCalls, ApiToolCallId, NativeBacked); }
		}
		internal static string GetActiveSlotName(){
			if(!string.IsNullOrWhiteSpace(Generation.CntDialSlotName)) return Generation.CntDialSlotName;
			return ModelSlotManager.GetActiveChatSlot()?.Name ?? Common.ActiveModelSlotName ?? ModelSlotManager.MainSlotName;
		}
		internal static IEnumerable<string> GetActiveSlotNames(){
			var slotNames = Generation.GetDialecticSlotNames();
			return slotNames.Length > 0 ? slotNames : new[]{ GetActiveSlotName() };
		}
		internal static NativeMethods.StudError ResetState(){return RunForActiveSlots(NativeMethods.ResetChat);}
		internal static NativeMethods.StudError RemoveMessageAt(int index){return RunForActiveSlots(slotName => NativeMethods.RemoveMessageAt(slotName, index));}
		internal static NativeMethods.StudError RemoveMessagesStartingAt(int index){return RunForActiveSlots(slotName => NativeMethods.RemoveMessagesStartingAt(slotName, index));}
		internal static NativeMethods.StudError SetMessageAt(int index, MessageRole role, string think, string message){
			var hasImages = false;
			var contentJson = role == MessageRole.User ? MarkdownImages.BuildNativeContentJson(message, out hasImages) : null;
			return RunForActiveSlots(slotName => hasImages ? NativeMethods.SetMessageAtJson(slotName, index, think, contentJson) : NativeMethods.SetMessageAt(slotName, index, think, message));
		}
		internal static NativeMethods.StudError SetSystemPrompt(string slotName, string prompt, string toolsPrompt){
			return NativeMethods.SetSystemPrompt(string.IsNullOrWhiteSpace(slotName) ? GetActiveSlotName() : slotName, prompt, toolsPrompt);
		}
		internal static List<MessageSnapshot> CaptureMessages(IEnumerable<ChatMessageControl> messages){
			var snapshots = new List<MessageSnapshot>();
			if(messages == null) return snapshots;
			foreach(var msg in messages.Where(msg => msg != null && msg.NativeBacked))
				snapshots.Add(new MessageSnapshot(msg.Role, msg.Think, msg.Message, msg.ApiContent, msg.ApiToolCalls, msg.ApiToolCallId, msg.NativeBacked));
			return snapshots;
		}
		internal static NativeMethods.StudError SyncMessages(string slotName, IEnumerable<MessageSnapshot> messages){
			if(string.IsNullOrWhiteSpace(slotName)) slotName = GetActiveSlotName();
			return NativeMethods.SyncChatMessagesJson(slotName, BuildMessagesJson(messages));
		}
		internal static NativeMethods.StudError SyncActiveMessages(IEnumerable<MessageSnapshot> messages){
			return RunForActiveSlots(slotName => SyncMessages(slotName, messages));
		}
		internal static string BuildMessagesJson(IEnumerable<MessageSnapshot> messages){
			var array = Json.ArrayBuilder();
			if(messages == null) return array.ToJson();
			foreach(var message in messages.Where(message => message?.NativeBacked == true)){
				var obj = Json.ObjectBuilder(Json.P("role", RoleToJson(message.Role)));
				if(message.Role == MessageRole.User){
					var contentJson = MarkdownImages.BuildNativeContentJson(message.ApiMessageContent, out var _);
					obj["content"] = Json.Parse(contentJson);
				} else obj["content"] = Json.String(message.ApiMessageContent);
				if(message.Role == MessageRole.Assistant && !string.IsNullOrEmpty(message.Think)) obj["reasoning_content"] = Json.String(message.Think);
				var toolCalls = BuildChatCompletionToolCalls(message.ApiToolCalls);
				if(toolCalls.Exists) obj["tool_calls"] = toolCalls;
				if(message.Role == MessageRole.Tool && !string.IsNullOrWhiteSpace(message.ApiToolCallId)) obj["tool_call_id"] = Json.String(message.ApiToolCallId);
				array.Add(obj.ToNode());
			}
			return array.ToJson();
		}
		internal static JsonNode BuildChatCompletionToolCalls(IEnumerable<APIClient.ToolCall> toolCalls){
			if(toolCalls == null) return JsonNode.Missing;
			var array = Json.ArrayBuilder();
			foreach(var call in toolCalls.Where(call => call != null && !string.IsNullOrWhiteSpace(call.Id) && !string.IsNullOrWhiteSpace(call.Name))){
				array.Add(Json.Object(Json.P("id", call.Id), Json.P("type", "function"),
					Json.P("function", Json.Object(Json.P("name", call.Name), Json.P("arguments", call.Arguments ?? "")))));
			}
			return array.Count > 0 ? array.ToNode() : JsonNode.Missing;
		}
		internal static string RoleToJson(MessageRole role){
			switch(role){
				case MessageRole.Assistant: return "assistant";
				case MessageRole.Tool: return "tool";
				default: return "user";
			}
		}
		private static NativeMethods.StudError RunForActiveSlots(Func<string, NativeMethods.StudError> action){
			if(!Generation.DialecticRelayEnabled) return action(GetActiveSlotName());
			var result = NativeMethods.StudError.Success;
			var applied = false;
			foreach(var slotName in Generation.GetDialecticSlotNames().Where(name => !string.IsNullOrWhiteSpace(name)).Distinct(StringComparer.OrdinalIgnoreCase)){
				result = action(slotName);
				if(result == NativeMethods.StudError.Success){
					applied = true;
					continue;
				}
				if(result == NativeMethods.StudError.IndexOutOfRange) continue;
				break;
			}
			return applied && result == NativeMethods.StudError.IndexOutOfRange ? NativeMethods.StudError.Success : result;
		}
	}
}
