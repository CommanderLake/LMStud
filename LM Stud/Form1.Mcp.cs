using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using LMStud.Properties;
namespace LMStud{
	public partial class Form1{
		private readonly HashSet<string> _mcpOperations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		private readonly CountdownEvent _mcpWorkers = new CountdownEvent(1);
		private System.Threading.Timer _mcpTimer;
		private int _mcpPollPosted;
		private uint _registeredMcpRevision;
		private bool _mcpRegistrationPending;

		private void StartMcpServers(){
			NativeMethods.MCPStart();
			_registeredMcpRevision = NativeMethods.MCPGetCatalogRevision();
			foreach(var slot in ModelSlotManager.Slots.Where(slot => slot.Source == ModelSlotSource.Mcp && slot.HasUse(ModelSlotUse.Tool)))
				QueueMcpConnect(slot, false);
			_mcpTimer = new System.Threading.Timer(_ => {
				if(_shutdownPending || Interlocked.Exchange(ref _mcpPollPosted, 1) != 0) return;
				PostMcpUpdate(() => {
					Interlocked.Exchange(ref _mcpPollPosted, 0);
					UpdateMcpServers();
				});
			}, null, 500, 500);
		}
		private void QueueMcpConnect(ModelSlot slot, bool showError = true){
			QueueMcpOperation(slot.Name, () => McpServerManager.Connect(slot), showError ? Resources.MCP_Connect : null);
		}
		private void QueueMcpDisconnect(string name, bool showError = false){
			QueueMcpOperation(name, () => McpServerManager.Disconnect(name), showError ? Resources.MCP_Disconnect : null);
		}
		private void ApplyMcpSlotConnectionState(ModelSlot slot){
			if(slot.Source != ModelSlotSource.Mcp) return;
			if(slot.HasUse(ModelSlotUse.Tool)) QueueMcpConnect(slot);
			else QueueMcpDisconnect(slot.Name);
		}
		private void QueueMcpOperation(string name, Func<string> operation, string errorTitle = null){
			if(_shutdownPending || !_mcpOperations.Add(name)) return;
			UpdateSelectedMcpButtons();
			QueueMcpWorker(() => {
				var error = McpServerManager.GetError(operation());
				PostMcpUpdate(() => {
					_mcpOperations.Remove(name);
					UpdateMcpServers();
					if(errorTitle != null && !string.IsNullOrEmpty(error)) ShowError(errorTitle, error, false);
				});
			});
		}
		private void QueueMcpWorker(Action action){
			_mcpWorkers.AddCount();
			ThreadPool.QueueUserWorkItem(_ => {
				try{ action(); } finally{ _mcpWorkers.Signal(); }
			});
		}
		private void PostMcpUpdate(Action action){
			try{
				BeginInvoke(new MethodInvoker(() => { if(!_shutdownPending) action(); }));
			} catch(InvalidOperationException){}
		}
		private void UpdateMcpServers(){
			foreach(ListViewItem item in listViewSlots.Items){
				if(!(item.Tag is ModelSlot slot) || slot.Source != ModelSlotSource.Mcp) continue;
				var state = ModelSlotManager.GetSlotState(slot);
				if(item.SubItems[3].Text != state) item.SubItems[3].Text = state;
			}
			UpdateSelectedMcpButtons();
			foreach(var status in McpServerManager.GetStatuses())
				if(status.Value.GetBool("refresh") == true)
					QueueMcpOperation(status.Key, () => McpServerManager.Refresh(status.Key));
			var revision = NativeMethods.MCPGetCatalogRevision();
			if(_registeredMcpRevision == revision || _mcpRegistrationPending) return;
			_mcpRegistrationPending = true;
			QueueMcpWorker(() => {
				try{
					foreach(var name in ModelSlotManager.GetLoadedLocalSlotNames()){
						using(var lease = ModelSlotManager.EnterSlot(name)){
							if(_shutdownPending) break;
							if(!SlotHasLoadedLocalModel(name)) continue;
							Tools.RegisterMcpTools(name);
							SetSystemPromptForSlot(name, false);
						}
					}
				} finally{
					PostMcpUpdate(() => {
						_registeredMcpRevision = revision;
						_mcpRegistrationPending = false;
					});
				}
			});
		}
		private void UpdateSelectedMcpButtons(){
			if(listViewSlots.SelectedItems.Count == 1 && (listViewSlots.SelectedItems[0].Tag as ModelSlot)?.Source == ModelSlotSource.Mcp) UpdateSlotButtons();
		}
	}
}