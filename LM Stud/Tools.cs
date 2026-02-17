using System;
using System.Runtime.InteropServices;
using System.Text;
namespace LMStud {
	internal class Tools {
		private static string _toolsJsonCache;
		private static bool _toolsJsonCacheDirty = true;
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
			if(!ToolsEnabled()) return null;
			if(!_toolsJsonCacheDirty && !string.IsNullOrWhiteSpace(_toolsJsonCache)) return _toolsJsonCache;
			var ptr = NativeMethods.GetToolsJson(out var length);
			if(ptr == IntPtr.Zero || length <= 0) return null;
			var buffer = new byte[length];
			Marshal.Copy(ptr, buffer, 0, length);
			_toolsJsonCache = Encoding.UTF8.GetString(buffer);
			_toolsJsonCacheDirty = false;
			return _toolsJsonCache;
		}
		private static void InvalidateToolsJsonCache(){
			_toolsJsonCacheDirty = true;
			_toolsJsonCache = null;
		}
		internal static string ExecuteToolCall(ApiClient.ToolCall toolCall){
			if(toolCall == null || string.IsNullOrWhiteSpace(toolCall.Name)) return "{\"error\":\"missing tool name\"}";
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
	}
}
