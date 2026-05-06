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
		internal static void RegisterTools(){
			NativeMethods.RegisterTools(Common.DateTimeEnable, Common.GoogleSearchEnable, Common.WebpageFetchEnable, Common.FileListEnable, Common.FileCreateEnable, Common.FileReadEnable, Common.FileWriteEnable, Common.CMDEnable);
			InvalidateToolsJsonCache();
		}
		internal static void ClearRegisteredTools(){
			NativeMethods.ClearTools();
			NativeMethods.ClearWebCache();
			InvalidateToolsJsonCache();
		}
		private static bool ToolsEnabled(){
			return Common.DateTimeEnable || Common.GoogleSearchEnable || Common.WebpageFetchEnable || Common.FileListEnable || Common.FileCreateEnable || Common.FileReadEnable || Common.FileWriteEnable || Common.CMDEnable;
		}
		internal static string BuildApiToolsJson(){
			if(!_toolsJsonCacheDirty && !string.IsNullOrWhiteSpace(_toolsJsonCache)) return _toolsJsonCache;
			var tools = new JArray();
			if(ToolsEnabled()){
				var ptr = NativeMethods.GetToolsJson(out var length);
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
			var ptr = NativeMethods.ExecuteTool(toolCall.Name, toolCall.Arguments ?? "");
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
				foreach(var tool in array) if(tool != null) destination.Add(tool.DeepClone());
			} catch(JsonException){}
		}
	}
}
