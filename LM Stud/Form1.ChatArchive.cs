using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using LMStud.Properties;
namespace LMStud{
	public partial class Form1{
		internal bool ChatFileOperationActive{get; private set;}
		private bool ChatBusy => Generation.Generating || Volatile.Read(ref _retokenizeCount) > 0 || _shutdownPending || _shutdownReady;
		private bool HasChatToExport => ChatMessages.Count != 0 || textInput.TextLength != 0;
		private void ChatToolStripMenuItem_DropDownOpening(object sender, EventArgs e){
			restoreChatToolStripMenuItem.Enabled = !ChatFileOperationActive && !ChatBusy;
			exportChatToolStripMenuItem.Enabled = restoreChatToolStripMenuItem.Enabled && HasChatToExport;
		}
		private void ExportChat_Click(object sender, EventArgs e){
			if(ChatFileOperationActive || ChatBusy || !HasChatToExport) return;
			if(saveChatDialog.ShowDialog(this) == DialogResult.OK) ExportChatArchive(saveChatDialog.FileName);
		}
		private void RestoreChat_Click(object sender, EventArgs e){
			if(ChatFileOperationActive || ChatBusy) return;
			if(openChatDialog.ShowDialog(this) == DialogResult.OK) RestoreChatArchive(openChatDialog.FileName);
		}
		internal void ExportChatArchive(string path){
			var title = saveChatDialog.Title;
			if(ChatFileOperationActive || ChatBusy){
				ShowError(title, Resources.Chat_File_Busy, false);
				return;
			}
			var relaySlots = Generation.GetDialecticSlotNames();
			var modelSlot = ModelSlotManager.GetActiveChatSlot();
			var slotName = relaySlots.Length > 0 ? relaySlots[0] : NativeChat.GetActiveSlotName();
			var slots = relaySlots.Length > 0 ? relaySlots : new[]{ slotName };
			var archive = new ChatArchive{
				Draft = textInput.Text,
				Messages = ChatMessages.Select(message => new ChatArchive.Message{
					Role = message.Role,
					Speaker = message.labelRole.Text,
					Text = message.Message,
					Reasoning = message.Think,
					ShowReasoning = message.checkThink.Checked,
					DisplayOnly = !message.NativeBacked,
					ApiContent = message.ApiContent,
					ToolCalls = message.ApiToolCalls == null ? null : new List<APIClient.ToolCall>(message.ApiToolCalls),
					ToolCallId = message.ApiToolCallId
				}).ToList(),
				PartnerTurn = relaySlots.Length > 1 && string.Equals(Generation.CntDialSlotName, relaySlots[1], StringComparison.OrdinalIgnoreCase)
			};
			var leases = ModelSlotManager.TryEnterSlots(slots, 0);
			if(leases == null){
				ShowError(title, Resources.Chat_File_Busy, false);
				return;
			}
			ChatFileOperationActive = true;
			ThreadPool.QueueUserWorkItem(_ => {
				string error = null;
				try{
					if((relaySlots.Length > 0 || modelSlot?.Source != ModelSlotSource.Api) && archive.Messages.Count != 0) archive.LocalHistory = NativeChat.ReadHistory(slotName);
					if(relaySlots.Length > 1) archive.PartnerHistory = archive.Messages.Count == 0 ? "[]" : NativeChat.ReadHistory(relaySlots[1]);
					archive.EmbedLocalImages();
					archive.Write(path);
				} catch(Exception ex){ error = ex.Message; } finally{
					ModelSlotManager.ReleaseSlots(leases);
					FinishChatFileOperation(title, error);
				}
			});
		}
		internal void RestoreChatArchive(string path){
			var title = openChatDialog.Title;
			if(ChatFileOperationActive || ChatBusy){
				ShowError(title, Resources.Chat_File_Busy, false);
				return;
			}
			ChatFileOperationActive = true;
			ThreadPool.QueueUserWorkItem(_ => {
				string error = null;
				try{ RestoreChatArchive(ChatArchive.Read(path), title); } catch(Exception ex){ error = ex.GetBaseException().Message; } finally{ FinishChatFileOperation(title, error); }
			});
		}
		private void RestoreChatArchive(ChatArchive archive, string title){
			List<ModelSlot> relaySlots = null;
			List<ModelSlotLockLease> leases = null;
			string slotName = null;
			string partnerSlotName = null;
			var tabsEnabled = true;
			var restoredFiles = new List<string>();
			var restored = false;
			try{
				RunOnUiThread(() => {
					if(_shutdownPending || _shutdownReady) return;
					if(HasChatToExport
						&& MessageBox.Show(this, Resources.Replace_Current_Chat, title, MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
					if(ChatBusy) throw new InvalidOperationException(Resources.Chat_File_Busy);
					relaySlots = archive.PartnerHistory == null ? null : ModelSlotManager.ResolveDialecticLocalSlots();
					var restoreRelay = relaySlots?.Count == 2;
					slotName = restoreRelay ? relaySlots[0].Name : ModelSlotManager.GetActiveChatSlot()?.Name ?? ModelSlotManager.MainSlotName;
					partnerSlotName = restoreRelay ? relaySlots[1].Name : null;
					leases = ModelSlotManager.TryEnterSlots(restoreRelay ? new[]{ slotName, partnerSlotName } : new[]{ slotName }, 0);
					if(leases == null) throw new InvalidOperationException(Resources.Chat_File_Busy);
					tabsEnabled = tabControlMain.Enabled;
					BeginRetokenization();
					tabControlMain.Enabled = false;
				});
				if(leases == null) return;
				archive.RestoreEmbeddedImages(restoredFiles);
				var history = partnerSlotName == null && archive.PartnerTurn ? archive.PartnerHistory : archive.LocalHistory;
				if(history == null || (history == "[]" && archive.PartnerHistory == null)) history = NativeChat.BuildMessagesJson(archive.Messages.Select(message => message.ToSnapshot()));
				NativeChat.RestoreHistory(slotName, history, partnerSlotName, archive.PartnerHistory);
				var tokenCount = NativeMethods.LlamaMemSize(archive.PartnerTurn ? partnerSlotName ?? slotName : slotName);
				RunOnUiThread(() => {
					ApplyArchivedMessages(archive);
					restored = true;
					if(partnerSlotName != null){
						Generation.ConfigureDialecticSlots(relaySlots);
						if(archive.PartnerTurn) Generation.CntDialSlotName = partnerSlotName;
					}
					checkDialectic.Checked = partnerSlotName != null;
					Generation.DialPaused = false;
					labelTokens.Text = tokenCount + Resources._Tokens;
					labelTPS.Text = labelPreGen.Text = "";
					tabControlMain.SelectedTab = tabPageChat;
				});
			} finally{
				ModelSlotManager.ReleaseSlots(leases);
				if(!restored)
					foreach(var file in restoredFiles)
						try{ File.Delete(file); } catch(IOException){} catch(UnauthorizedAccessException){}
				if(leases != null)
					RunOnUiThread(() => {
						tabControlMain.Enabled = tabsEnabled;
						EndRetokenization();
						SetModelStatus();
					});
			}
		}
		private void FinishChatFileOperation(string title, string error){
			try{
				BeginInvoke(new MethodInvoker(() => {
					ChatFileOperationActive = false;
					if(error != null && !_shutdownPending && !_shutdownReady) ShowError(title, error, false);
				}));
			} catch(ObjectDisposedException){} catch(InvalidOperationException){}
		}
		private void ApplyArchivedMessages(ChatArchive archive){
			TTS.CancelPendingSpeech();
			panelChat.AutoScrollEnable = false;
			panelChat.SuspendLayout();
			try{
				foreach(var message in ChatMessages) message.Dispose();
				ChatMessages.Clear();
				foreach(var saved in archive.Messages){
					var message = AddMessage(saved.Role, saved.Reasoning ?? "", saved.Text, saved.ToolCalls, saved.ToolCallId, !saved.DisplayOnly);
					message.ApiContent = saved.ApiContent;
					if(saved.Speaker != null) message.SetRoleText(saved.Speaker);
					message.checkThink.Checked = saved.ShowReasoning && !string.IsNullOrEmpty(saved.Reasoning);
				}
				textInput.Text = archive.Draft ?? "";
				textInput.SelectionStart = textInput.TextLength;
				InputEditOldText = textInput.Text;
				NativeMethods.SetCommittedText(textInput.Text);
			} finally{
				panelChat.ResumeLayout();
				panelChat.AutoScrollEnable = checkAutoScroll.Checked;
				panelChat.ScrollToEnd();
			}
		}
	}
}