using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
namespace LMStud{
	public partial class Form1{
		private bool _populatingSlotEditor;
		private string _slotEditorAutoToolName;
		private void ListViewSlots_SelectedIndexChanged(object sender, EventArgs e) {
			PopulateSlotEditorFromSelection();
			UpdateSlotButtons();
		}
		private void ListViewSlots_DoubleClick(object sender, EventArgs e) {
			if(listViewSlots.SelectedItems.Count != 1) return;
			var slot = (ModelSlot)listViewSlots.SelectedItems[0].Tag;
			if(!ModelSlotManager.SetActiveChatSlot(slot.Name)) return;
			ApplyActiveSlotToRuntime(true);
			PopulateSlotsList();
			SelectSlot(slot.Name);
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
			var generated = ModelSlotManager.BuildToolName(textSlotsEditName.Text);
			if(!string.IsNullOrWhiteSpace(textSlotsEditToolName.Text) && !string.Equals(textSlotsEditToolName.Text, _slotEditorAutoToolName, StringComparison.OrdinalIgnoreCase)) return;
			textSlotsEditToolName.Text = generated;
			_slotEditorAutoToolName = generated;
		}
		private void ComboSlotsEditSource_SelectedIndexChanged(object sender, EventArgs e) {
			if(comboSlotsEditSource.SelectedIndex < 0) comboSlotsEditSource.SelectedIndex = 0;
			var api = comboSlotsEditSource.SelectedIndex == 1;
			textSlotsEditModel.Enabled = label44.Enabled = !api;
			textSlotsEditApiUrl.Enabled = textSlotsEditApiKey.Enabled = comboSlotsEditApiModel.Enabled = checkSlotsEditStore.Enabled = api;
			comboSlotEditReasonEffort.Enabled = comboSlotEditReasonSummary.Enabled = label45.Enabled = label46.Enabled = label47.Enabled = label51.Enabled = label52.Enabled = api;
			checkSlotsEditTool.Enabled = api;
			textSlotsEditToolName.Enabled = label49.Enabled = api;
			if(!api) checkSlotsEditTool.Checked = false;
		}
		private void ButSlotsAdd_Click(object sender, EventArgs e){
			if(!TryReadSlotEditor(null, true, out var slot)) return;
			ModelSlotManager.AddOrUpdate(slot);
			ApplyActiveSlotToRuntime(true);
			PopulateSlotsList();
			SelectSlot(slot.Name);
		}
		private void ButSlotsSave_Click(object sender, EventArgs e){SaveSelectedSlot();}
		private void ButSlotsRemove_Click(object sender, EventArgs e){
			if(listViewSlots.SelectedItems.Count != 1) return;
			var slot = (ModelSlot)listViewSlots.SelectedItems[0].Tag;
			if(MessageBox.Show(this, "Remove the " + slot.Name + " slot?", "LM Stud", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK) return;
			var unloadLocalSlot = slot.Source == ModelSlotSource.Local && (Common.LoadedLocalSlots.ContainsKey(slot.Name) || NativeMethods.IsModelSlotLoaded(slot.Name));
			if(!ModelSlotManager.Remove(slot.Name)) MessageBox.Show(this, "The main slot cannot be removed. Edit it instead.", "LM Stud", MessageBoxButtons.OK, MessageBoxIcon.Information);
			else if(unloadLocalSlot) UnloadModel(true, slot.Name);
			ApplyActiveSlotToRuntime(true);
			PopulateSlotsList();
		}
		private void ButLoadSlot_Click(object sender, EventArgs e){
			if(!TryLoadSelectedSlot()) ShowError("Load Slot", "Select a local slot first.", false);
		}
		private void ButUnloadSlot_Click(object sender, EventArgs e){
			if(listViewSlots.SelectedItems.Count != 1){
				ShowError("Unload Slot", "Select a slot first.", false);
				return;
			}
			var slot = (ModelSlot)listViewSlots.SelectedItems[0].Tag;
			if(slot.Source != ModelSlotSource.Local){
				ShowError("Unload Slot", "Only local slots can be unloaded.", false);
				return;
			}
			UnloadModel(true, slot.Name);
		}
		private void ComboSlotsEditApiModel_DropDown(object sender, EventArgs e){
			try{
				comboSlotsEditApiModel.Items.Clear();
				using(var client = new APIClient(textSlotsEditApiUrl.Text, textSlotsEditApiKey.Text, "", checkSlotsEditStore.Checked, null)){
					foreach(var model in client.GetModels(CancellationToken.None)) comboSlotsEditApiModel.Items.Add(model);
				}
			} catch(Exception ex){ APIClient.ShowApiClientError("API Client", ex); }
		}
		private void ButSlotsEditUseSelectedModel_Click(object sender, EventArgs e) {
			if(listViewModels.SelectedItems.Count == 0) return;
			textSlotsEditModel.Text = listViewModels.SelectedItems[0].SubItems[1].Text;
			comboSlotsEditSource.SelectedIndex = 0;
		}
		private void InitializeSlotUi(){
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
					item.SubItems.Add(slot.Source == ModelSlotSource.Api ? "API" : "Local");
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
				comboSlotsEditSource.SelectedIndex = slot.Source == ModelSlotSource.Api ? 1 : 0;
				textSlotsEditModel.Text = slot.LocalPath ?? "";
				textSlotsEditApiUrl.Text = slot.ApiBaseUrl ?? "";
				textSlotsEditApiKey.Text = slot.ApiKey ?? "";
				comboSlotsEditApiModel.Text = slot.ApiModel ?? "";
				checkSlotsEditStore.Checked = slot.ApiStore;
				SetComboSelectedIndex(comboSlotEditReasonEffort, slot.ApiReasoningEffort);
				SetComboSelectedIndex(comboSlotEditReasonSummary, slot.ApiReasoningSummary);
				textSlotsEditInstructions.Text = slot.Instructions ?? "";
				_slotEditorAutoToolName = string.IsNullOrWhiteSpace(slot.ToolName) ? ModelSlotManager.BuildToolName(slot.Name) : slot.ToolName;
				textSlotsEditToolName.Text = _slotEditorAutoToolName;
				checkSlotsEditChat.Checked = slot.HasUse(ModelSlotUse.Chat);
				checkSlotsEditDialectic.Checked = slot.HasUse(ModelSlotUse.Dialectic);
				checkSlotsEditTool.Checked = slot.HasUse(ModelSlotUse.Tool);
				checkSlotsEditServer.Checked = slot.HasUse(ModelSlotUse.Server);
			} finally{
				_populatingSlotEditor = false;
			}
			ComboSlotsEditSource_SelectedIndexChanged(null, null);
		}
		private void UpdateSlotButtons(){
			var hasSelection = listViewSlots != null && listViewSlots.SelectedItems.Count == 1;
			var slot = hasSelection ? (ModelSlot)listViewSlots.SelectedItems[0].Tag : null;
			butSlotsSave.Enabled = hasSelection;
			butSlotsRemove.Enabled = hasSelection && !string.Equals(slot.Name, "main", StringComparison.OrdinalIgnoreCase);
			butLoadSlot.Enabled = hasSelection && slot.Source == ModelSlotSource.Local;
			butUnloadSlot.Enabled = hasSelection && ModelSlotManager.CanServeLocalSlot(slot);
		}
		private void SaveSelectedSlot(){
			if(listViewSlots.SelectedItems.Count != 1) return;
			var original = (ModelSlot)listViewSlots.SelectedItems[0].Tag;
			if(!TryReadSlotEditor(original.Name, false, out var slot)) return;
			ModelSlotManager.AddOrUpdate(slot, original.Name);
			ApplyActiveSlotToRuntime(true);
			PopulateSlotsList();
			SelectSlot(slot.Name);
		}
		private bool TryReadSlotEditor(string originalName, bool adding, out ModelSlot slot){
			slot = null;
			var name = textSlotsEditName.Text.Trim();
			if(string.IsNullOrWhiteSpace(name)){
				MessageBox.Show(this, "Slot name is required.", "LM Stud", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
				textSlotsEditName.Focus();
				return false;
			}
			var duplicate = ModelSlotManager.Slots.Any(existing => string.Equals(existing.Name, name, StringComparison.OrdinalIgnoreCase) &&
				(adding || !string.Equals(existing.Name, originalName, StringComparison.OrdinalIgnoreCase)));
			if(duplicate){
				MessageBox.Show(this, "Another slot already uses that name.", "LM Stud", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
				textSlotsEditName.Focus();
				return false;
			}
			var use = ModelSlotUse.None;
			if(checkSlotsEditChat.Checked) use |= ModelSlotUse.Chat;
			if(checkSlotsEditDialectic.Checked) use |= ModelSlotUse.Dialectic;
			if(checkSlotsEditTool.Checked) use |= ModelSlotUse.Tool;
			if(checkSlotsEditServer.Checked) use |= ModelSlotUse.Server;
			slot = new ModelSlot{
				Name = name,
				Source = comboSlotsEditSource.SelectedIndex == 1 ? ModelSlotSource.Api : ModelSlotSource.Local,
				LocalPath = textSlotsEditModel.Text.Trim(),
				ApiBaseUrl = textSlotsEditApiUrl.Text.Trim(),
				ApiKey = textSlotsEditApiKey.Text.Trim(),
				ApiModel = comboSlotsEditApiModel.Text.Trim(),
				ApiReasoningEffort = GetComboSelectedIndex(comboSlotEditReasonEffort),
				ApiReasoningSummary = GetComboSelectedIndex(comboSlotEditReasonSummary),
				ApiStore = checkSlotsEditStore.Checked,
				Instructions = textSlotsEditInstructions.Text.Trim(),
				ToolName = textSlotsEditToolName.Text.Trim(),
				Use = use
			};
			return true;
		}
		private void SelectSlot(string slotName){
			foreach(ListViewItem item in listViewSlots.Items){
				var slot = item.Tag as ModelSlot;
				item.Selected = slot != null && string.Equals(slot.Name, slotName, StringComparison.OrdinalIgnoreCase);
				if(item.Selected) item.EnsureVisible();
			}
		}
		private void ApplyActiveSlotToRuntime(bool updateControls, bool registerTools = true){
			var slot = ModelSlotManager.GetActiveChatSlot();
			if(slot == null) return;
			Common.ActiveModelSlotName = slot.Name;
			if(slot.Source == ModelSlotSource.Local){
				if(!Generation.Generating && !Generation.APIServerGenerating) NativeMethods.ActivateModelSlot(slot.Name);
				Common.LoadedModel = GetLoadedModelForSlot(slot);
				Common.LlModelLoaded = Common.LoadedLocalSlots.Count > 0;
			}
			if(registerTools) Tools.RegisterTools();
			else Tools.InvalidateToolsJsonCache();
			RunOnUiThread(SetModelStatus);
		}
		private bool TryLoadSelectedSlot(){
			if(tabControlModelStuff.SelectedTab != tabPageSlots || listViewSlots.SelectedItems.Count != 1) return false;
			var slot = (ModelSlot)listViewSlots.SelectedItems[0].Tag;
			if(slot.Source == ModelSlotSource.Api){
				slot.Use |= ModelSlotUse.Chat | ModelSlotUse.Server;
				ModelSlotManager.AddOrUpdate(slot);
				ApplyActiveSlotToRuntime(true);
				PopulateSlotsList();
				return true;
			}
			var path = slot.ResolveLocalPath();
			if(string.IsNullOrWhiteSpace(path) || !File.Exists(path)){
				ShowError("Load Slot", "Model file not found\r\n\r\n" + path, false);
				return true;
			}
			var item = listViewModels.Items.Cast<ListViewItem>().FirstOrDefault(lvi => string.Equals(lvi.SubItems[1].Text, path, StringComparison.OrdinalIgnoreCase));
			if(item == null){
				ShowError("Load Slot", "The model is not in the current Models list. Refresh the list or check the Models folder.\r\n\r\n" + path, false);
				return true;
			}
			LoadModel(item, false, slot.Name);
			return true;
		}
		private static int GetComboSelectedIndex(ComboBox combo){
			return combo.SelectedIndex > 0 && combo.SelectedIndex < combo.Items.Count ? combo.SelectedIndex : 0;
		}
		private static void SetComboSelectedIndex(ComboBox combo, int selectedIndex){
			combo.SelectedIndex = selectedIndex > 0 && selectedIndex < combo.Items.Count ? selectedIndex : 0;
		}
	}
}
