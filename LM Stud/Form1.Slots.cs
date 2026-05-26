using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using LMStud.Properties;
namespace LMStud{
	public partial class Form1{
		private bool _populatingSlotEditor;
		private string _slotEditorAutoToolName;
		private string _slotLoadButtonText;
		private string _slotUnloadButtonText;
		private bool _slotSelectionChanged;
		private void ListViewSlots_DoubleClick(object sender, EventArgs e) {
			if(listViewSlots.SelectedItems.Count != 1) return;
			var slot = (ModelSlot)listViewSlots.SelectedItems[0].Tag;
			if(slot.Source == ModelSlotSource.Mcp){
				TryLoadSelectedSlot();
				return;
			}
			if(!ModelSlotManager.SetActiveChatSlot(slot.Name)) return;
			ApplyActiveSlotToModel();
			PopulateSlotsList();
			SelectSlot(slot.Name);
		}
		private void ListViewSlots_Click(object sender, EventArgs e) {
			if(_slotSelectionChanged){
				_slotSelectionChanged = false;
				return;
			}
			PopulateSlotEditorFromSelection();
			UpdateSlotButtons();
		}
		private void ListViewSlots_SelectedIndexChanged(object sender, EventArgs e) {
			PopulateSlotEditorFromSelection();
			UpdateSlotButtons();
			_slotSelectionChanged = true;
		}
		private void ListViewSlots_KeyDown(object sender, KeyEventArgs e){
			if(e.KeyCode == Keys.Enter){
				e.SuppressKeyPress = true;
				SaveSelectedSlot();
			} else if(e.KeyCode == Keys.Delete){
				e.SuppressKeyPress = true;
				ButSlotsRemove_Click(sender, EventArgs.Empty);
			} else if(e.KeyCode == Keys.F5) PopulateSlotsList();
		}
		private void TextSlotsEditName_TextChanged(object sender, EventArgs e) {
			if(_populatingSlotEditor) return;
			UpdateAutoToolNamesForNameChange();
		}
		private void ComboSlotsEditSource_SelectedIndexChanged(object sender, EventArgs e) {
			if(comboSlotsEditSource.SelectedIndex < 0) comboSlotsEditSource.SelectedIndex = 0;
			var source = GetSlotEditorSource();
			var local = source == ModelSlotSource.Local;
			var api = source == ModelSlotSource.Api;
			var mcp = source == ModelSlotSource.Mcp;
			panelSlotsEditLocal.Visible = local;
			panelSlotsEditApi.Visible = api;
			panelSlotsEditMcp.Visible = mcp;
			if(local) panelSlotsEditLocal.BringToFront();
			else if(api) panelSlotsEditApi.BringToFront();
			else if(mcp) panelSlotsEditMcp.BringToFront();
			if(mcp && comboSlotsEditMcpTransport.SelectedIndex < 0) SetMcpTransportCombo(McpSlotTransport.Stdio);
			UpdateMcpTransportUi();
			textSlotsEditSystemPrompt.Enabled = label48.Enabled = !mcp;
			butSlotsEditUseSelectedModel.Enabled = local;
			checkSlotsEditChat.Enabled = !mcp;
			checkSlotsEditDialectic.Enabled = local;
			checkSlotsEditServer.Enabled = !mcp;
			checkSlotsEditTool.Enabled = true;
			UpdateAutoToolNameForCurrentSource();
			if(!_populatingSlotEditor && mcp){
				checkSlotsEditChat.Checked = false;
				checkSlotsEditDialectic.Checked = false;
				checkSlotsEditServer.Checked = false;
				checkSlotsEditTool.Checked = true;
			}
			if(!_populatingSlotEditor && !local) checkSlotsEditDialectic.Checked = false;
			if(!_populatingSlotEditor && !local) checkSlotsEditLocalOverride.Checked = false;
			UpdateSlotSystemPromptUi();
		}
		private void ComboSlotsEditLocalModel_SelectedIndexChanged(object sender, EventArgs e){
			if(!_populatingSlotEditor && GetSlotEditorSource() == ModelSlotSource.Local && !checkSlotsEditLocalOverride.Checked)
				textSlotsEditSystemPrompt.Text = GetSystemPromptForModel(GetSlotsEditLocalModelPath());
		}
		private void CheckSlotsEditLocalOverride_CheckedChanged(object sender, EventArgs e){
			UpdateSlotSystemPromptUi();
		}
		private void ButSlotsAdd_Click(object sender, EventArgs e){
			if(!TryReadSlotEditor(null, true, out var slot)) return;
			ModelSlotManager.AddOrUpdate(slot);
			ApplyMcpSlotConnectionState(slot);
			ApplyActiveSlotToModel();
			PopulateSlotsList();
			SelectSlot(slot.Name);
		}
		private void ButSlotsSave_Click(object sender, EventArgs e){SaveSelectedSlot();}
		private void ButSlotsRemove_Click(object sender, EventArgs e){
			if(listViewSlots.SelectedItems.Count != 1) return;
			var slot = (ModelSlot)listViewSlots.SelectedItems[0].Tag;
			if(MessageBox.Show(this, string.Format(Resources.Remove_the__0__slot_, slot.Name), Resources.LM_Stud, MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK) return;
			var unloadLocalSlot = SlotHasLoadedLocalModel(slot.Name);
			if(slot.Source == ModelSlotSource.Mcp) McpServerManager.Disconnect(slot.Name, RetokenizeLoadedLocalSlotsForToolChange);
			if(!ModelSlotManager.Remove(slot.Name)) MessageBox.Show(this, Resources.The_main_slot_cannot_be_removed__Edit_it_instead_, Resources.LM_Stud, MessageBoxButtons.OK, MessageBoxIcon.Information);
			else if(unloadLocalSlot) UnloadModel(true, slot.Name);
			ApplyActiveSlotToModel();
			PopulateSlotsList();
		}
		private void ButLoadSlot_Click(object sender, EventArgs e){
			if(!TryLoadSelectedSlot()) ShowError(Resources.Load_Slot, Resources.Select_a_local_or_MCP_slot_first_, false);
		}
		private void ButUnloadSlot_Click(object sender, EventArgs e){
			if(listViewSlots.SelectedItems.Count != 1){
				ShowError(Resources.Unload_Slot, Resources.Select_a_slot_first_, false);
				return;
			}
			var slot = (ModelSlot)listViewSlots.SelectedItems[0].Tag;
			if(slot.Source == ModelSlotSource.Mcp){
				var response = McpServerManager.Disconnect(slot.Name, RetokenizeLoadedLocalSlotsForToolChange);
				if(!McpServerManager.IsOk(response)) ShowError(Resources.MCP_Disconnect, response, false);
				PopulateSlotsList();
				UpdateSlotButtons();
				return;
			}
			if(slot.Source != ModelSlotSource.Local){
				ShowError(Resources.Unload_Slot, Resources.Only_local_slots_can_be_unloaded_, false);
				return;
			}
			UnloadModel(true, slot.Name);
		}
		private void ComboSlotsEditApiModel_DropDown(object sender, EventArgs e){
			if(GetSlotEditorSource() != ModelSlotSource.Api) return;
			try{
				comboSlotsEditApiModel.Items.Clear();
				using(var client = new APIClient(textSlotsEditApiUrl.Text.Trim(), textSlotsEditApiKey.Text, "", checkSlotsEditStore.Checked)){
					foreach(var model in client.GetModels(CancellationToken.None)) comboSlotsEditApiModel.Items.Add(model);
				}
			} catch(Exception ex){ APIClient.ShowApiClientError(Resources.API_Client, ex); }
		}
		private void ComboSlotsEditMcpTransport_SelectedIndexChanged(object sender, EventArgs e){
			if(_populatingSlotEditor || GetSlotEditorSource() != ModelSlotSource.Mcp) return;
			UpdateMcpTransportUi();
		}
		private void ButSlotsEditUseSelectedModel_Click(object sender, EventArgs e) {
			if(listViewModels.SelectedItems.Count == 0) return;
			SetSlotsEditLocalModel(listViewModels.SelectedItems[0].SubItems[1].Text);
			comboSlotsEditSource.SelectedIndex = 0;
		}
		private void InitSlotUi(){
			_slotLoadButtonText = butLoadSlot.Text;
			_slotUnloadButtonText = butUnloadSlot.Text;
			if(comboSlotsEditMcpTransport.SelectedIndex < 0) SetMcpTransportCombo(McpSlotTransport.Stdio);
			ComboSlotsEditSource_SelectedIndexChanged(null, null);
			UpdateSlotButtons();
		}
		private void PopulateSlotsList(){
			if(listViewSlots == null) return;
			var selectedName = listViewSlots.SelectedItems.Count == 1 ? ((ModelSlot)listViewSlots.SelectedItems[0].Tag).Name : null;
			listViewSlots.BeginUpdate();
			try{
				listViewSlots.Items.Clear();
				foreach(var slot in ModelSlotManager.Slots){
					var item = new ListViewItem(slot.Name);
					item.SubItems.Add(FormatSlotSource(slot.Source));
					item.SubItems.Add(slot.DisplayModel());
					item.SubItems.Add(ModelSlotManager.GetSlotState(slot));
					item.SubItems.Add(ModelSlotManager.FormatUse(slot));
					item.Tag = slot;
					if(slot.HasUse(ModelSlotUse.Chat)) item.Font = new Font(listViewSlots.Font, FontStyle.Bold);
					listViewSlots.Items.Add(item);
					if(string.Equals(slot.Name, selectedName, StringComparison.OrdinalIgnoreCase)) item.Selected = true;
				}
			} finally{ listViewSlots.EndUpdate(); }
			if(listViewSlots.SelectedItems.Count == 1) PopulateSlotEditorFromSelection();
			UpdateSlotButtons();
		}
		private void PopulateSlotEditorFromSelection(){
			if(listViewSlots.SelectedItems.Count != 1) return;
			PopulateSlotEditor((ModelSlot)listViewSlots.SelectedItems[0].Tag);
		}
		private void PopulateSlotEditor(ModelSlot slot){
			if(slot == null) return;
			_populatingSlotEditor = true;
			try{
				textSlotsEditName.Text = slot.Name ?? "";
				comboSlotsEditSource.SelectedIndex = GetSlotSourceIndex(slot.Source);
				if(slot.Source == ModelSlotSource.Mcp){
					SetMcpTransportCombo(slot.McpTransport);
					textSlotsEditMcpUrl.Text = slot.GetMcpEndpoint();
					textSlotsEditMcpHeader.Text = slot.McpTransport == McpSlotTransport.Http ? slot.McpAuthHeader ?? "" : "";
				} else{
					SetSlotsEditLocalModel(slot.Source == ModelSlotSource.Local ? slot.LocalPath ?? "" : "");
					textSlotsEditApiUrl.Text = slot.Source == ModelSlotSource.Api ? slot.ApiBaseUrl ?? "" : "";
					textSlotsEditApiKey.Text = slot.ApiKey ?? "";
					comboSlotsEditApiModel.Text = slot.Source == ModelSlotSource.Api ? slot.ApiModel ?? "" : "";
				}
				checkSlotsEditStore.Checked = slot.ApiStore;
				SetComboSelectedIndex(comboSlotEditReasonEffort, slot.ApiReasoningEffort);
				SetComboSelectedIndex(comboSlotEditReasonSummary, slot.ApiReasoningSummary);
				checkSlotsEditLocalOverride.Checked = slot.Source == ModelSlotSource.Local && slot.OverrideSystemPrompt;
				if(slot.Source == ModelSlotSource.Mcp) textSlotsEditSystemPrompt.Text = "";
				else if(slot.Source == ModelSlotSource.Local && !slot.OverrideSystemPrompt) textSlotsEditSystemPrompt.Text = GetSystemPromptForModel(slot.ResolveLocalPath());
				else textSlotsEditSystemPrompt.Text = slot.Instructions ?? "";
				_slotEditorAutoToolName = string.IsNullOrWhiteSpace(slot.ToolName) ? ModelSlotManager.BuildToolName(slot.Name) : slot.ToolName;
				textSlotsEditLocalToolName.Text = slot.Source == ModelSlotSource.Local ? _slotEditorAutoToolName : "";
				textSlotsEditApiToolName.Text = slot.Source == ModelSlotSource.Api ? _slotEditorAutoToolName : "";
				checkSlotsEditChat.Checked = slot.Source != ModelSlotSource.Mcp && slot.HasUse(ModelSlotUse.Chat);
				checkSlotsEditDialectic.Checked = slot.Source == ModelSlotSource.Local && slot.HasUse(ModelSlotUse.Dialectic);
				checkSlotsEditTool.Checked = slot.HasUse(ModelSlotUse.Tool);
				checkSlotsEditServer.Checked = slot.Source != ModelSlotSource.Mcp && slot.HasUse(ModelSlotUse.Server);
				ComboSlotsEditSource_SelectedIndexChanged(null, null);
			} finally{
				_populatingSlotEditor = false;
			}
		}
		private void UpdateSlotButtons(){
			var hasSelection = listViewSlots != null && listViewSlots.SelectedItems.Count == 1;
			var slot = hasSelection ? (ModelSlot)listViewSlots.SelectedItems[0].Tag : null;
			butSlotsSave.Enabled = hasSelection;
			butSlotsRemove.Enabled = hasSelection && !string.Equals(slot.Name, ModelSlotManager.MainSlotName, StringComparison.OrdinalIgnoreCase);
			butLoadSlot.Text = hasSelection && slot.Source == ModelSlotSource.Mcp ? Resources.Connect : _slotLoadButtonText;
			butUnloadSlot.Text = hasSelection && slot.Source == ModelSlotSource.Mcp ? Resources.Disconnect : _slotUnloadButtonText;
			butLoadSlot.Enabled = hasSelection && (slot.Source == ModelSlotSource.Local || slot.Source == ModelSlotSource.Mcp);
			var localSlotLoaded = hasSelection && slot.Source == ModelSlotSource.Local && (ModelSlotManager.CanServeLocalSlot(slot) || SlotHasLoadedLocalModel(slot.Name));
			butUnloadSlot.Enabled = hasSelection && (localSlotLoaded || ModelSlotManager.CanServeMcpSlot(slot));
		}
		private void SaveSelectedSlot(){
			if(listViewSlots.SelectedItems.Count != 1) return;
			var original = (ModelSlot)listViewSlots.SelectedItems[0].Tag;
			if(!TryReadSlotEditor(original.Name, false, out var slot)) return;
			Common.LoadedLocalSlots.TryGetValue(original.Name, out var loadedModel);
			var unloadStaleLocalSlot = SlotEditInvalidatesLoadedLocalSlot(original, slot, loadedModel) && SlotHasLoadedLocalModel(original.Name);
			ModelSlotManager.AddOrUpdate(slot, original.Name);
			if(original.Source == ModelSlotSource.Mcp && (slot.Source != ModelSlotSource.Mcp || !string.Equals(original.Name, slot.Name, StringComparison.OrdinalIgnoreCase))) McpServerManager.Disconnect(original.Name, RetokenizeLoadedLocalSlotsForToolChange);
			ApplyMcpSlotConnectionState(slot);
			if(unloadStaleLocalSlot){
				UnloadModel(true, original.Name, () => {
					ApplyActiveSlotToModel();
					PopulateSlotsList();
					SelectSlot(slot.Name);
				});
				return;
			}
			if(slot.Source == ModelSlotSource.Local && SlotHasLoadedLocalModel(slot.Name) && SlotSystemPromptChanged(original, slot))
				ThreadPool.QueueUserWorkItem(o => SetSystemPromptForSlot(slot.Name));
			ApplyActiveSlotToModel();
			PopulateSlotsList();
			SelectSlot(slot.Name);
		}
		internal static bool SlotEditInvalidatesLoadedLocalSlot(ModelSlot original, ModelSlot updated, ListViewItem loadedModel){
			if(original == null || updated == null) return false;
			if(loadedModel != null && loadedModel.SubItems.Count >= 2)
				return updated.Source != ModelSlotSource.Local ||
					!string.Equals(original.Name, updated.Name, StringComparison.OrdinalIgnoreCase) ||
					!SameModelPath(loadedModel.SubItems[1].Text, updated.ResolveLocalPath());
			if(original.Source != ModelSlotSource.Local) return false;
			return updated.Source != ModelSlotSource.Local ||
				!string.Equals(original.Name, updated.Name, StringComparison.OrdinalIgnoreCase) ||
				!SameModelPath(original.ResolveLocalPath(), updated.ResolveLocalPath());
		}
		private static bool SlotHasLoadedLocalModel(string slotName){
			return !string.IsNullOrWhiteSpace(slotName) && (Common.LoadedLocalSlots.ContainsKey(slotName) || NativeMethods.IsModelSlotLoaded(slotName));
		}
		private static bool SlotSystemPromptChanged(ModelSlot original, ModelSlot updated){
			if(original == null || updated == null) return false;
			return original.OverrideSystemPrompt != updated.OverrideSystemPrompt ||
				!string.Equals(original.Instructions ?? "", updated.Instructions ?? "", StringComparison.Ordinal);
		}
		private bool TryReadSlotEditor(string originalName, bool adding, out ModelSlot slot){
			slot = null;
			var name = textSlotsEditName.Text.Trim();
			if(string.IsNullOrWhiteSpace(name)){
				MessageBox.Show(this, Resources.Slot_name_is_required_, Resources.LM_Stud, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
				textSlotsEditName.Focus();
				return false;
			}
			var duplicate = ModelSlotManager.Slots.Any(existing => string.Equals(existing.Name, name, StringComparison.OrdinalIgnoreCase) &&
				(adding || !string.Equals(existing.Name, originalName, StringComparison.OrdinalIgnoreCase)));
			if(duplicate){
				MessageBox.Show(this, Resources.Another_slot_already_uses_that_name_, Resources.LM_Stud, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
				textSlotsEditName.Focus();
				return false;
			}
			var use = ModelSlotUse.None;
			var source = GetSlotEditorSource();
			if(string.Equals(name, ModelSlotManager.MainSlotName, StringComparison.OrdinalIgnoreCase) && source == ModelSlotSource.Mcp){
				MessageBox.Show(this, Resources.The_main_slot_must_be_a_local_or_API_chat_slot__, Resources.LM_Stud, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
				comboSlotsEditSource.Focus();
				return false;
			}
			if(source != ModelSlotSource.Mcp && checkSlotsEditChat.Checked) use |= ModelSlotUse.Chat;
			if(source == ModelSlotSource.Local && checkSlotsEditDialectic.Checked) use |= ModelSlotUse.Dialectic;
			if(checkSlotsEditTool.Checked) use |= ModelSlotUse.Tool;
			if(source != ModelSlotSource.Mcp && checkSlotsEditServer.Checked) use |= ModelSlotUse.Server;
			var mcpTransport = GetMcpEditorTransport();
			var localModel = GetSlotsEditLocalModelPath();
			var apiBaseUrl = textSlotsEditApiUrl.Text.Trim();
			var mcpEndpoint = textSlotsEditMcpUrl.Text.Trim();
			if(source == ModelSlotSource.Mcp && checkSlotsEditTool.Checked && string.IsNullOrWhiteSpace(mcpEndpoint)){
				MessageBox.Show(this, mcpTransport == McpSlotTransport.Http ? Resources.MCP_URL_is_required_when_the_MCP_slot_is_enabled_as_a_tool_ : Resources.MCP_command_is_required_when_the_MCP_slot_is_enabled_as_a_tool_, Resources.LM_Stud, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
				textSlotsEditMcpUrl.Focus();
				return false;
			}
			var existingSlot = string.IsNullOrWhiteSpace(originalName) ? null : ModelSlotManager.GetSlot(originalName);
			var existingMcpTimeoutMs = existingSlot != null && existingSlot.McpTimeoutMs > 0 ? existingSlot.McpTimeoutMs : McpServerManager.DefaultTimeoutMs;
			var existingMcpWorkingDirectory = existingSlot?.McpWorkingDirectory ?? "";
			var localSystemPromptOverride = source == ModelSlotSource.Local && checkSlotsEditLocalOverride.Checked;
			var apiSystemPromptOverride = source == ModelSlotSource.Api && !string.IsNullOrWhiteSpace(textSlotsEditSystemPrompt.Text);
			slot = new ModelSlot{
				Name = name,
				Source = source,
				LocalPath = source == ModelSlotSource.Local ? localModel : "",
				ApiBaseUrl = source == ModelSlotSource.Api ? apiBaseUrl : "",
				ApiKey = source == ModelSlotSource.Api ? textSlotsEditApiKey.Text.Trim() : "",
				ApiModel = source == ModelSlotSource.Api ? comboSlotsEditApiModel.Text.Trim() : "",
				ApiReasoningEffort = GetComboSelectedIndex(comboSlotEditReasonEffort),
				ApiReasoningSummary = GetComboSelectedIndex(comboSlotEditReasonSummary),
				ApiStore = checkSlotsEditStore.Checked,
				Instructions = source == ModelSlotSource.Mcp ? "" : (localSystemPromptOverride || apiSystemPromptOverride ? textSlotsEditSystemPrompt.Text.Trim() : ""),
				McpAuthHeader = source == ModelSlotSource.Mcp && mcpTransport == McpSlotTransport.Http ? textSlotsEditMcpHeader.Text.Trim() : "",
				McpCommandLine = source == ModelSlotSource.Mcp && mcpTransport == McpSlotTransport.Stdio ? mcpEndpoint : "",
				McpTimeoutMs = source == ModelSlotSource.Mcp ? existingMcpTimeoutMs : McpServerManager.DefaultTimeoutMs,
				McpTransport = mcpTransport,
				McpUrl = source == ModelSlotSource.Mcp && mcpTransport == McpSlotTransport.Http ? mcpEndpoint : "",
				McpWorkingDirectory = source == ModelSlotSource.Mcp && mcpTransport == McpSlotTransport.Stdio ? existingMcpWorkingDirectory : "",
				OverrideSystemPrompt = localSystemPromptOverride || apiSystemPromptOverride,
				ToolName = source == ModelSlotSource.Local ? textSlotsEditLocalToolName.Text.Trim() : source == ModelSlotSource.Api ? textSlotsEditApiToolName.Text.Trim() : "",
				Use = use
			};
			return true;
		}
		private void UpdateSlotSystemPromptUi(){
			var source = GetSlotEditorSource();
			var local = source == ModelSlotSource.Local;
			var mcp = source == ModelSlotSource.Mcp;
			checkSlotsEditLocalOverride.Enabled = local;
			label48.Enabled = !mcp;
			textSlotsEditSystemPrompt.Enabled = !mcp && (!local || checkSlotsEditLocalOverride.Checked);
			if(_populatingSlotEditor) return;
			if(mcp) textSlotsEditSystemPrompt.Text = "";
			else if(local && !checkSlotsEditLocalOverride.Checked) textSlotsEditSystemPrompt.Text = GetSystemPromptForModel(GetSlotsEditLocalModelPath());
		}
		private TextBox GetToolNameTextBox(ModelSlotSource source){
			switch(source){
				case ModelSlotSource.Local: return textSlotsEditLocalToolName;
				case ModelSlotSource.Api: return textSlotsEditApiToolName;
				default: return null;
			}
		}
		private void UpdateAutoToolNameForCurrentSource(){
			if(_populatingSlotEditor) return;
			var box = GetToolNameTextBox(GetSlotEditorSource());
			if(box == null) return;
			var generated = ModelSlotManager.BuildToolName(textSlotsEditName.Text);
			UpdateAutoToolNameBox(box, _slotEditorAutoToolName, generated);
			_slotEditorAutoToolName = generated;
		}
		private void UpdateAutoToolNamesForNameChange(){
			var previous = _slotEditorAutoToolName;
			var generated = ModelSlotManager.BuildToolName(textSlotsEditName.Text);
			UpdateAutoToolNameBox(textSlotsEditLocalToolName, previous, generated);
			UpdateAutoToolNameBox(textSlotsEditApiToolName, previous, generated);
			_slotEditorAutoToolName = generated;
		}
		private static void UpdateAutoToolNameBox(TextBox box, string previousAutoToolName, string generatedToolName){
			if(box == null) return;
			if(!string.IsNullOrWhiteSpace(box.Text) && !string.Equals(box.Text, previousAutoToolName, StringComparison.OrdinalIgnoreCase)) return;
			box.Text = generatedToolName;
		}
		private void SelectSlot(string slotName){
			foreach(ListViewItem item in listViewSlots.Items){
				var slot = item.Tag as ModelSlot;
				item.Selected = slot != null && string.Equals(slot.Name, slotName, StringComparison.OrdinalIgnoreCase);
				if(item.Selected) item.EnsureVisible();
			}
		}
		private void ApplyActiveSlotToModel(bool registerTools = true){
			var slot = ModelSlotManager.GetActiveChatSlot();
			if(slot == null) return;
			Common.ActiveModelSlotName = slot.Name;
			if(slot.Source == ModelSlotSource.Local){
				Common.LoadedModel = GetLoadedModelForSlot(slot);
				Common.LlModelLoaded = Common.LoadedLocalSlots.Count > 0;
			}
			if(registerTools) Tools.RegisterTools(slot.Name);
			else Tools.InvalidateToolsJsonCache();
			RunOnUiThread(SetModelStatus);
		}
		private bool TryLoadSelectedSlot(){
			if(tabControlModelStuff.SelectedTab != tabPageSlots || listViewSlots.SelectedItems.Count != 1) return false;
			var slot = (ModelSlot)listViewSlots.SelectedItems[0].Tag;
			if(slot.Source == ModelSlotSource.Mcp){
				var response = McpServerManager.Connect(slot, RetokenizeLoadedLocalSlotsForToolChange);
				if(!McpServerManager.IsOk(response)) ShowError(Resources.MCP_Connect, response, false);
				PopulateSlotsList();
				UpdateSlotButtons();
				return true;
			}
			if(slot.Source == ModelSlotSource.Api){
				slot.Use |= ModelSlotUse.Chat;
				ModelSlotManager.AddOrUpdate(slot);
				ApplyActiveSlotToModel();
				PopulateSlotsList();
				return true;
			}
			var path = slot.ResolveLocalPath();
			if(string.IsNullOrWhiteSpace(path) || !File.Exists(path)){
				ShowError(Resources.Load_Slot, Resources.Model_File_Not_Found + path, false);
				return true;
			}
			var item = listViewModels.Items.Cast<ListViewItem>().FirstOrDefault(lvi => string.Equals(lvi.SubItems[1].Text, path, StringComparison.OrdinalIgnoreCase));
			if(item == null){
				ShowError(Resources.Load_Slot, Resources.The_model_is_not_in_the_current_models_list__ + path, false);
				return true;
			}
			LoadModel(slot.Name, item, false);
			return true;
		}
		private static int GetComboSelectedIndex(ComboBox combo){
			return combo.SelectedIndex > 0 && combo.SelectedIndex < combo.Items.Count ? combo.SelectedIndex : 0;
		}
		private static void SetComboSelectedIndex(ComboBox combo, int selectedIndex){
			combo.SelectedIndex = selectedIndex > 0 && selectedIndex < combo.Items.Count ? selectedIndex : 0;
		}
		private string GetSlotsEditLocalModelPath(){
			if(comboSlotsEditLocalModel.SelectedItem is ListViewItem item && item.SubItems.Count > 1) return item.SubItems[1].Text;
			return ResolveModelPath(comboSlotsEditLocalModel.Text);
		}
		private void SetSlotsEditLocalModel(string modelPathOrName){
			if(string.IsNullOrWhiteSpace(modelPathOrName)){
				comboSlotsEditLocalModel.SelectedIndex = -1;
				return;
			}
			var item = FindModelItem(modelPathOrName);
			if(item != null){
				comboSlotsEditLocalModel.SelectedItem = item;
				return;
			}
			var modelPath = ResolveModelPath(modelPathOrName);
			var missingItem = new ListViewItem(Path.GetFileNameWithoutExtension(modelPath));
			missingItem.SubItems.Add(modelPath);
			comboSlotsEditLocalModel.Items.Add(missingItem);
			comboSlotsEditLocalModel.SelectedItem = missingItem;
		}
		private void ApplyMcpSlotConnectionState(ModelSlot slot){
			if(slot?.Source != ModelSlotSource.Mcp) return;
			var response = slot.HasUse(ModelSlotUse.Tool) ? McpServerManager.Connect(slot, RetokenizeLoadedLocalSlotsForToolChange) : McpServerManager.Disconnect(slot.Name, RetokenizeLoadedLocalSlotsForToolChange);
			if(slot.HasUse(ModelSlotUse.Tool) && !McpServerManager.IsOk(response)) ShowError(Resources.MCP_Connect, response, false);
		}
		private void RetokenizeLoadedLocalSlotsForToolChange(IEnumerable<string> slotNames){
			var loadedSlots = ModelSlotManager.GetLoadedLocalSlotNames();
			foreach(var slotName in (slotNames ?? Enumerable.Empty<string>()).Where(name => loadedSlots.Contains(name, StringComparer.OrdinalIgnoreCase)))
				SetSystemPromptForSlot(slotName, false);
		}
		private McpSlotTransport GetMcpEditorTransport(){
			return comboSlotsEditMcpTransport.SelectedIndex == 0 ? McpSlotTransport.Http : McpSlotTransport.Stdio;
		}
		private void SetMcpTransportCombo(McpSlotTransport transport){
			comboSlotsEditMcpTransport.SelectedIndex = transport == McpSlotTransport.Http ? 0 : 1;
		}
		private void UpdateMcpTransportUi(){
			var http = GetMcpEditorTransport() == McpSlotTransport.Http;
			label41.Text = http ? Resources.URL_ : Resources.Command_;
			label45.Visible = textSlotsEditMcpHeader.Visible = http;
			label45.Enabled = textSlotsEditMcpHeader.Enabled = http;
			if(!http && !_populatingSlotEditor) textSlotsEditMcpHeader.Text = "";
		}
		private ModelSlotSource GetSlotEditorSource(){
			switch(comboSlotsEditSource.SelectedIndex){
				case 1: return ModelSlotSource.Api;
				case 2: return ModelSlotSource.Mcp;
				default: return ModelSlotSource.Local;
			}
		}
		private static int GetSlotSourceIndex(ModelSlotSource source){
			switch(source){
				case ModelSlotSource.Api: return 1;
				case ModelSlotSource.Mcp: return 2;
				default: return 0;
			}
		}
		private static string FormatSlotSource(ModelSlotSource source){
			switch(source){
				case ModelSlotSource.Api: return Resources.API;
				case ModelSlotSource.Mcp: return Resources.MCP;
				default: return Resources.Local;
			}
		}
	}
}
