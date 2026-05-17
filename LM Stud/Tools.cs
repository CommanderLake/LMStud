using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using LMStud.Properties;
namespace LMStud {
	internal static class Tools {
		private static readonly Dictionary<string, string> ToolsJsonCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		private static readonly NativeMethods.ManagedToolCallback ManagedToolCallbackFn = ExecuteManagedToolCall;
		private static bool _managedToolCallbackRegistered;
		private static volatile bool _toolsJsonCacheDirty = true;
		internal static void RegisterTools(string slotName){
			RegisterToolsCore(slotName);
			InvalidateToolsJsonCache();
		}
		private static void RegisterToolsCore(string slotName){
			EnsureManagedToolCallbackRegistered();
			NativeMethods.RegisterTools(slotName, Common.DateTimeEnable, Common.GoogleSearchEnable, Common.WebpageFetchEnable, Common.FileListEnable, Common.FileCreateEnable, Common.FileReadEnable, Common.FileWriteEnable, Common.CMDEnable);
			NativeMethods.MCPRegisterToolsForSlot(slotName);
			RegisterModelCallTools(slotName);
		}
		internal static void RegisterToolsForAllSlots(){
			var slotNames = ModelSlotManager.Slots.Where(slot => slot.Source != ModelSlotSource.Mcp).Select(slot => slot.Name).ToList();
			var leases = ModelSlotManager.EnterSlots(slotNames);
			try{ RegisterToolsForSlotsWithoutLock(slotNames); }
			finally{ ModelSlotManager.ReleaseSlots(leases); }
		}
		internal static void RegisterToolsForSlotsWithoutLock(IEnumerable<string> slotNames){
			foreach(var slotName in (slotNames ?? Enumerable.Empty<string>()).Where(name => !string.IsNullOrWhiteSpace(name)).Distinct(StringComparer.OrdinalIgnoreCase)) RegisterToolsCore(slotName);
			InvalidateToolsJsonCache();
		}
		internal static void ClearRegisteredTools(string slotName){
			NativeMethods.ClearTools(slotName);
			NativeMethods.ClearWebCache();
			InvalidateToolsJsonCache();
		}
		private static bool ToolsEnabled(){
			return Common.DateTimeEnable || Common.GoogleSearchEnable || Common.WebpageFetchEnable || Common.FileListEnable || Common.FileCreateEnable || Common.FileReadEnable || Common.FileWriteEnable || Common.CMDEnable;
		}
		internal static string BuildApiToolsJson(string slotName){
			var cacheKey = string.IsNullOrWhiteSpace(slotName) ? "" : slotName.Trim();
			if(!_toolsJsonCacheDirty && ToolsJsonCache.TryGetValue(cacheKey, out var cached) && !string.IsNullOrWhiteSpace(cached)) return cached;
			var tools = Json.ArrayBuilder();
			var toolNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			if(ToolsEnabled()){
				var ptr = NativeMethods.GetToolsJson(slotName, out var length);
				if(ptr != IntPtr.Zero && length > 0){
					var buffer = new byte[length];
					Marshal.Copy(ptr, buffer, 0, length);
					AddTools(tools, Encoding.UTF8.GetString(buffer), toolNames);
				}
			}
			foreach(var tool in ModelSlotManager.BuildModelCallTools(slotName).Where(tool => tool.IsObject)) AddTool(tools, tool, toolNames);
			AddTools(tools, McpServerManager.BuildToolsJson(), toolNames);
			if(tools.Count == 0) return null;
			var toolsJson = tools.ToJson();
			ToolsJsonCache[cacheKey] = toolsJson;
			_toolsJsonCacheDirty = false;
			return toolsJson;
		}
		internal static void InvalidateToolsJsonCache(){
			_toolsJsonCacheDirty = true;
			ToolsJsonCache.Clear();
		}
		internal static string ExecuteToolCall(APIClient.ToolCall toolCall, string callerSlotName, Action<string> streamCallback = null){
			if(toolCall == null || string.IsNullOrWhiteSpace(toolCall.Name)) return "{\"error\":\"missing tool name\"}";
			if(ModelSlotManager.TryExecuteToolCall(toolCall, callerSlotName, streamCallback, out var modelResult)) return modelResult;
			if(McpServerManager.TryExecuteToolCall(toolCall, out var mcpResult)) return mcpResult;
			var slotName = ModelSlotManager.GetActiveChatSlot()?.Name ?? Common.ActiveModelSlotName ?? ModelSlotManager.MainSlotName;
			var ptr = NativeMethods.ExecuteTool(slotName, toolCall.Name, toolCall.Arguments ?? "");
			if(ptr == IntPtr.Zero) return "{\"error\":\"tool execution failed\"}";
			try{
				var length = 0;
				while(Marshal.ReadByte(ptr, length) != 0) length++;
				var buffer = new byte[length];
				Marshal.Copy(ptr, buffer, 0, length);
				return Encoding.UTF8.GetString(buffer);
			} finally{ NativeMethods.FreeMemory(ptr); }
		}
		private static void RegisterModelCallTools(string slotName){
			var toolNames = GetRegisteredToolNames(slotName);
			foreach(var tool in ModelSlotManager.BuildModelCallTools(slotName).Where(tool => tool.IsObject)){
				var function = tool["function"].IsObject ? tool["function"] : tool;
				var name = function.GetString("name");
				if(string.IsNullOrWhiteSpace(name) || !toolNames.Add(name)) continue;
				NativeMethods.AddTool(slotName, name, function.GetString("description") ?? "", function["parameters"].Exists ? function["parameters"].ToJson() : "{}", IntPtr.Zero);
			}
		}
		private static HashSet<string> GetRegisteredToolNames(string slotName){
			var toolNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			var ptr = NativeMethods.GetToolsJson(slotName, out var length);
			if(ptr == IntPtr.Zero || length <= 0) return toolNames;
			var buffer = new byte[length];
			Marshal.Copy(ptr, buffer, 0, length);
			AddToolNames(Encoding.UTF8.GetString(buffer), toolNames);
			return toolNames;
		}
		private static void EnsureManagedToolCallbackRegistered(){
			if(_managedToolCallbackRegistered) return;
			NativeMethods.SetManagedToolCallback(ManagedToolCallbackFn);
			_managedToolCallbackRegistered = true;
		}
		private static IntPtr ExecuteManagedToolCall(string callerSlotName, string name, string argsJson){
			try{
				var toolCall = new APIClient.ToolCall(null, name, argsJson ?? "");
				var streamedText = new StringBuilder();
				Action<string> streamCallback = null;
				if(!string.IsNullOrWhiteSpace(callerSlotName))
					streamCallback = delta => {
						if(string.IsNullOrEmpty(delta)) return;
						streamedText.Append(delta);
						NativeMethods.StreamManagedToolOutput(callerSlotName, streamedText.ToString());
					};
				if(ModelSlotManager.TryExecuteToolCall(toolCall, callerSlotName, streamCallback, out var result)) return AllocNativeString(result ?? "");
			} catch(Exception ex){ return AllocNativeString(Json.Object(Json.P("error", ex.Message)).ToJson()); }
			return IntPtr.Zero;
		}
		private static IntPtr AllocNativeString(string value){
			var bytes = Encoding.UTF8.GetBytes(value ?? "");
			var ptr = Marshal.AllocHGlobal(bytes.Length + 1);
			Marshal.Copy(bytes, 0, ptr, bytes.Length);
			Marshal.WriteByte(ptr, bytes.Length, 0);
			return ptr;
		}
		private static void AddTools(JsonArrayBuilder destination, string toolsJson, HashSet<string> toolNames){
			if(string.IsNullOrWhiteSpace(toolsJson)) return;
			try{
				var array = Json.Parse(toolsJson);
				if(!array.IsArray) return;
				foreach(var tool in array) AddTool(destination, tool, toolNames);
			} catch{}
		}
		private static void AddToolNames(string toolsJson, HashSet<string> toolNames){
			if(string.IsNullOrWhiteSpace(toolsJson) || toolNames == null) return;
			try{
				var array = Json.Parse(toolsJson);
				if(!array.IsArray) return;
				foreach(var name in array.Select(GetToolName).Where(name => !string.IsNullOrWhiteSpace(name))) toolNames.Add(name);
			} catch{}
		}
		private static void AddTool(JsonArrayBuilder destination, JsonNode tool, HashSet<string> toolNames){
			var name = GetToolName(tool);
			if(!string.IsNullOrWhiteSpace(name) && toolNames != null && !toolNames.Add(name)) return;
			destination.Add(tool);
		}
		private static string GetToolName(JsonNode tool){
			if(!tool.IsObject) return null;
			var function = tool["function"].IsObject ? tool["function"] : tool;
			return function.GetString("name");
		}
		public static string FormatToolCallDisplayText(string content, IList<APIClient.ToolCall> toolCalls){
			if(toolCalls == null || toolCalls.Count == 0) return content;
			var sb = new StringBuilder();
			if(!string.IsNullOrWhiteSpace(content)){
				sb.AppendLine(content.TrimEnd());
				sb.AppendLine();
			}
			for(var i = 0; i < toolCalls.Count; i++){
				var toolCall = toolCalls[i];
				if(toolCalls.Count > 1) sb.AppendLine(Resources.Tool_call_ + (i + 1));
				sb.AppendLine(Resources.Tool_name__ + (toolCall.Name ?? ""));
				if(!string.IsNullOrWhiteSpace(toolCall.Id)) sb.AppendLine(Resources.Tool_ID__ + toolCall.Id);
				if(!string.IsNullOrWhiteSpace(toolCall.Arguments)){
					sb.AppendLine(Resources.Tool_arguments_);
					sb.AppendLine(NativeMethods.FormatJsonDisplay(toolCall.Arguments));
				}
				if(i < toolCalls.Count - 1) sb.AppendLine();
			}
			return sb.ToString().TrimEnd();
		}
		public static string FormatToolMessageForDisplay(int tool, string message){
			switch(tool){
				case 1: return NativeMethods.FormatToolOutputDisplay(message);
				case 3: return NativeMethods.FormatToolCallDisplay(message);
				default: return message ?? "";
			}
		}
		public static void AddToolOutputMessage(string toolCallId, string toolResult){
			try{
				Generation.MainForm.Invoke(new MethodInvoker(() => {
					var toolMessageControl = Generation.MainForm.AddMessage(MessageRole.Tool, "", NativeMethods.FormatToolOutputDisplay(toolResult), null, toolCallId);
					toolMessageControl.ApiContent = toolResult ?? "";
					toolMessageControl.SetRoleText(Resources.Tool_Output);
				}));
			} catch(ObjectDisposedException){} catch(InvalidOperationException){}
		}
	}
}
