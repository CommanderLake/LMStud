using System;
using System.Collections.Generic;
using System.Linq;
using LMStud.Properties;
namespace LMStud{
	internal static class McpServerManager{
		internal const int DefaultTimeoutMs = 30000;
		private static readonly object Sync = new object();
		private static readonly HashSet<string> ConnectedServerIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		internal static void SyncConfiguredServers(Action<IEnumerable<string>> afterRegisteredToolsChanged = null){
			var slotNames = GetNativeToolSlotNames();
			var leases = ModelSlotManager.EnterSlots(slotNames);
			var enabled = ModelSlotManager.Slots.Where(slot => slot.Source == ModelSlotSource.Mcp && slot.HasUse(ModelSlotUse.Tool) && !string.IsNullOrWhiteSpace(slot.GetMcpEndpoint())).ToList();
			var enabledIds = new HashSet<string>(enabled.Select(slot => slot.Name), StringComparer.OrdinalIgnoreCase);
			try{
				List<string> staleIds;
				lock(Sync) staleIds = ConnectedServerIds.Where(id => !enabledIds.Contains(id)).ToList();
				foreach(var id in staleIds) Disconnect(id, false);
				foreach(var slot in enabled) Connect(slot, false);
				RefreshRegisteredTools(slotNames, true, afterRegisteredToolsChanged);
			} finally{ ModelSlotManager.ReleaseSlots(leases); }
		}
		internal static string Connect(ModelSlot slot, Action<IEnumerable<string>> afterRegisteredToolsChanged = null){return Connect(slot, true, afterRegisteredToolsChanged);}
		private static string Connect(ModelSlot slot, bool refreshRegisteredTools, Action<IEnumerable<string>> afterRegisteredToolsChanged = null){
			if(slot == null || slot.Source != ModelSlotSource.Mcp) return ErrorJson(Resources.Slot_is_not_an_MCP_server_);
			if(string.IsNullOrWhiteSpace(slot.Name)) return ErrorJson(Resources.MCP_slot_name_is_required_);
			var slotNames = GetNativeToolSlotNames();
			var leases = refreshRegisteredTools ? ModelSlotManager.EnterSlots(slotNames) : null;
			var endpoint = slot.GetMcpEndpoint();
			try{
				if(string.IsNullOrWhiteSpace(endpoint)){
					lock(Sync) DisconnectNativeLocked(slot.Name);
					RefreshRegisteredTools(slotNames, refreshRegisteredTools, afterRegisteredToolsChanged);
					return ErrorJson(slot.McpTransport == McpSlotTransport.Http ? Resources.MCP_URL_is_required_ : Resources.MCP_command_line_is_required_);
				}
				lock(Sync){
					try{
						DisconnectNativeLocked(slot.Name);
						var timeoutMs = slot.McpTimeoutMs > 0 ? slot.McpTimeoutMs : DefaultTimeoutMs;
						var response = slot.McpTransport == McpSlotTransport.Http ? NativeMethods.ReadUtf8AndFree(NativeMethods.MCPConnectHttp(slot.Name, slot.McpUrl, slot.McpAuthHeader ?? "", timeoutMs)) : NativeMethods.ReadUtf8AndFree(NativeMethods.MCPConnectStdio(slot.Name, slot.McpCommandLine, slot.McpWorkingDirectory ?? "", timeoutMs));
						if(IsOk(response)) ConnectedServerIds.Add(slot.Name);
						else ConnectedServerIds.Remove(slot.Name);
						RefreshRegisteredTools(slotNames, refreshRegisteredTools, afterRegisteredToolsChanged);
						return response;
					} catch(Exception ex){
						ConnectedServerIds.Remove(slot.Name);
						RefreshRegisteredTools(slotNames, refreshRegisteredTools, afterRegisteredToolsChanged);
						return ErrorJson(ex.Message);
					}
				}
			} finally{ ModelSlotManager.ReleaseSlots(leases); }
		}
		internal static string Disconnect(string serverId, Action<IEnumerable<string>> afterRegisteredToolsChanged = null){return Disconnect(serverId, true, afterRegisteredToolsChanged);}
		private static string Disconnect(string serverId, bool refreshRegisteredTools, Action<IEnumerable<string>> afterRegisteredToolsChanged = null){
			if(string.IsNullOrWhiteSpace(serverId)) return ErrorJson("MCP server id is required.");
			var slotNames = GetNativeToolSlotNames();
			var leases = refreshRegisteredTools ? ModelSlotManager.EnterSlots(slotNames) : null;
			try{
				lock(Sync){
					var response = "";
					try{ response = DisconnectNativeLocked(serverId); } catch(Exception ex){ response = ErrorJson(ex.Message); }
					RefreshRegisteredTools(slotNames, refreshRegisteredTools, afterRegisteredToolsChanged);
					return response;
				}
			} finally{ ModelSlotManager.ReleaseSlots(leases); }
		}
		private static string DisconnectNativeLocked(string serverId){
			var response = "";
			try{ response = NativeMethods.ReadUtf8AndFree(NativeMethods.MCPDisconnect(serverId)); } catch(Exception ex){ response = ErrorJson(ex.Message); }
			ConnectedServerIds.Remove(serverId);
			return response;
		}
		internal static void DisconnectAll(){
			lock(Sync){
				try{ NativeMethods.MCPDisconnectAll(); } catch{}
				ConnectedServerIds.Clear();
			}
			Tools.InvalidateToolsJsonCache();
		}
		internal static string BuildToolsJson(){
			lock(Sync){
				try{ return NativeMethods.ReadUtf8AndFree(NativeMethods.MCPBuildToolsJson()); } catch{ return null; }
			}
		}
		internal static bool TryExecuteToolCall(APIClient.ToolCall toolCall, out string result){
			result = null;
			if(toolCall == null || string.IsNullOrWhiteSpace(toolCall.Name)) return false;
			lock(Sync){
				bool hasTool;
				try{ hasTool = NativeMethods.MCPHasTool(toolCall.Name); } catch{ return false; }
				if(!hasTool) return false;
				try{ result = NativeMethods.ReadUtf8AndFree(NativeMethods.MCPExecuteTool(toolCall.Name, toolCall.Arguments ?? "{}")); } catch(Exception ex){ result = ErrorJson(ex.Message); }
				return true;
			}
		}
		internal static bool IsConnected(string serverId){
			if(string.IsNullOrWhiteSpace(serverId)) return false;
			lock(Sync){
				if(ConnectedServerIds.Contains(serverId)) return true;
				RefreshConnectedServerIds();
				return ConnectedServerIds.Contains(serverId);
			}
		}
		internal static bool IsOk(string response){
			if(string.IsNullOrWhiteSpace(response)) return false;
			try{
				var obj = Json.Parse(response);
				return obj.GetBool("ok") == true && !obj["error"].Exists;
			} catch{ return false; }
		}
		private static List<string> GetNativeToolSlotNames(){
			return ModelSlotManager.Slots.Where(slot => slot.Source != ModelSlotSource.Mcp).Select(slot => slot.Name).ToList();
		}
		private static void RefreshRegisteredTools(IEnumerable<string> slotNames, bool refreshRegisteredTools, Action<IEnumerable<string>> afterRegisteredToolsChanged){
			if(!refreshRegisteredTools) return;
			Tools.RegisterToolsForSlotsWithoutLock(slotNames);
			try{ afterRegisteredToolsChanged?.Invoke(slotNames); } catch{}
		}
		private static void RefreshConnectedServerIds(){
			try{
				var json = NativeMethods.ReadUtf8AndFree(NativeMethods.MCPListServers());
				var array = Json.Parse(json);
				ConnectedServerIds.Clear();
				if(!array.IsArray) return;
				foreach(var id in from item in array.Where(item => item.IsObject) let id = item.GetString("id") where !string.IsNullOrWhiteSpace(id) && item.GetBool("initialized") == true select id){ ConnectedServerIds.Add(id); }
			} catch(Exception){}
		}
		private static string ErrorJson(string message){ return Json.Object(Json.P("error", message)).ToJson(); }
	}
}
