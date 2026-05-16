using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
namespace LMStud {
	internal static class Tools {
		private static string _toolsJsonCache;
		private static volatile bool _toolsJsonCacheDirty = true;
		internal static void RegisterTools(string slotName){
			RegisterToolsCore(slotName);
			InvalidateToolsJsonCache();
		}
		private static void RegisterToolsCore(string slotName){
			NativeMethods.RegisterTools(slotName, Common.DateTimeEnable, Common.GoogleSearchEnable, Common.WebpageFetchEnable, Common.FileListEnable, Common.FileCreateEnable, Common.FileReadEnable, Common.FileWriteEnable, Common.CMDEnable);
			NativeMethods.MCPRegisterToolsForSlot(slotName);
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
			if(!_toolsJsonCacheDirty && !string.IsNullOrWhiteSpace(_toolsJsonCache)) return _toolsJsonCache;
			var tools = new JArray();
			var toolNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			if(ToolsEnabled()){
				var ptr = NativeMethods.GetToolsJson(slotName, out var length);
				if(ptr != IntPtr.Zero && length > 0){
					var buffer = new byte[length];
					Marshal.Copy(ptr, buffer, 0, length);
					AddTools(tools, Encoding.UTF8.GetString(buffer), toolNames);
				}
			}
			foreach(var tool in ModelSlotManager.BuildModelCallTools().OfType<JObject>()) AddTool(tools, tool, toolNames);
			AddTools(tools, McpServerManager.BuildToolsJson(), toolNames);
			if(tools.Count == 0) return null;
			_toolsJsonCache = tools.ToString(Formatting.None);
			_toolsJsonCacheDirty = false;
			return _toolsJsonCache;
		}
		internal static void InvalidateToolsJsonCache(){
			_toolsJsonCacheDirty = true;
			_toolsJsonCache = null;
		}
		internal static string ExecuteToolCall(APIClient.ToolCall toolCall){
			if(toolCall == null || string.IsNullOrWhiteSpace(toolCall.Name)) return "{\"error\":\"missing tool name\"}";
			if(ModelSlotManager.TryExecuteToolCall(toolCall, out var modelResult)) return modelResult;
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
		private static void AddTools(JArray destination, string toolsJson, HashSet<string> toolNames){
			if(string.IsNullOrWhiteSpace(toolsJson)) return;
			try{
				var parsed = JToken.Parse(toolsJson);
				if(!(parsed is JArray array)) return;
				foreach(var tool in array.Where(tool => tool != null)) AddTool(destination, tool, toolNames);
			} catch(JsonException){}
		}
		private static void AddTool(JArray destination, JToken tool, HashSet<string> toolNames){
			var name = GetToolName(tool);
			if(!string.IsNullOrWhiteSpace(name) && toolNames != null && !toolNames.Add(name)) return;
			destination.Add(tool.DeepClone());
		}
		private static string GetToolName(JToken tool){
			var obj = tool as JObject;
			if(obj == null) return null;
			var function = obj["function"] as JObject ?? obj;
			return function.Value<string>("name");
		}
	}
}
