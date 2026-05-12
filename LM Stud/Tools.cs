using System;
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
			NativeMethods.RegisterTools(slotName, Common.DateTimeEnable, Common.GoogleSearchEnable, Common.WebpageFetchEnable, Common.FileListEnable, Common.FileCreateEnable, Common.FileReadEnable, Common.FileWriteEnable, Common.CMDEnable);
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
			if(ToolsEnabled()){
				var ptr = NativeMethods.GetToolsJson(slotName, out var length);
				if(ptr != IntPtr.Zero && length > 0){
					var buffer = new byte[length];
					Marshal.Copy(ptr, buffer, 0, length);
					AddTools(tools, Encoding.UTF8.GetString(buffer));
				}
			}
			foreach(var tool in ModelSlotManager.BuildModelCallTools().OfType<JObject>()) tools.Add(tool);
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
			var slotName = ModelSlotManager.GetActiveChatSlot()?.Name ?? Common.ActiveModelSlotName ?? "main";
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
		private static void AddTools(JArray destination, string toolsJson){
			if(string.IsNullOrWhiteSpace(toolsJson)) return;
			try{
				var parsed = JToken.Parse(toolsJson);
				if(!(parsed is JArray array)) return;
				foreach(var tool in array.Where(tool => tool != null)) destination.Add(tool.DeepClone());
			} catch(JsonException){}
		}
	}
}
