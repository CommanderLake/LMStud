using System;
using System.Collections.Generic;
using LMStud.Properties;
namespace LMStud{
	internal static class McpServerManager{
		internal const int DefaultTimeoutMs = 30000;
		internal static string Connect(ModelSlot slot){
			if(slot?.Source != ModelSlotSource.Mcp) return Error(Resources.Slot_is_not_an_MCP_server_);
			if(string.IsNullOrWhiteSpace(slot.Name)) return Error(Resources.MCP_slot_name_is_required_);
			if(string.IsNullOrWhiteSpace(slot.GetMcpEndpoint()))
				return Error(slot.McpTransport == McpSlotTransport.Http ? Resources.MCP_URL_is_required_ : Resources.MCP_command_line_is_required_);
			return Read(() => slot.McpTransport == McpSlotTransport.Http
				? NativeMethods.MCPConnectHttp(slot.Name, slot.McpUrl, slot.McpAuthHeader ?? "", slot.McpTimeoutMs)
				: NativeMethods.MCPConnectStdio(slot.Name, slot.McpCommandLine, slot.McpWorkingDirectory ?? "", slot.McpTimeoutMs));
		}
		internal static string Disconnect(string serverId){return Read(() => NativeMethods.MCPDisconnect(serverId));}
		internal static string Refresh(string serverId){return Read(() => NativeMethods.MCPRefreshTools(serverId));}
		internal static string BuildToolsJson(){return Read(NativeMethods.MCPBuildToolsJson);}
		internal static Dictionary<string, JsonNode> GetStatuses(){
			var statuses = new Dictionary<string, JsonNode>(StringComparer.OrdinalIgnoreCase);
			foreach(var status in new JsonNode(Read(NativeMethods.MCPListServers))){
				var id = status.GetString("id");
				if(!string.IsNullOrEmpty(id)) statuses[id] = status;
			}
			return statuses;
		}
		internal static bool TryExecuteToolCall(APIClient.ToolCall toolCall, out string result){
			result = null;
			if(toolCall == null || string.IsNullOrWhiteSpace(toolCall.Name) || !NativeMethods.MCPHasTool(toolCall.Name)) return false;
			result = Read(() => NativeMethods.MCPExecuteTool(toolCall.Name, toolCall.Arguments ?? "{}"));
			return true;
		}
		internal static bool IsConnected(string serverId){return !string.IsNullOrWhiteSpace(serverId) && NativeMethods.MCPIsConnected(serverId);}
		internal static string GetError(string response){
			try{
				var result = Json.Parse(response);
				return result.GetBool("ok") == true ? null : result.GetString("error") ?? response;
			} catch(InvalidOperationException){ return response ?? "MCP operation returned no result."; }
		}
		private static string Read(Func<IntPtr> call){
			try{ return NativeMethods.ReadUtf8AndFree(call()); } catch(Exception ex){ return Error(ex.Message); }
		}
		private static string Error(string message){return Json.Object(Json.P("error", message)).ToJson();}
	}
}