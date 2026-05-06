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
		private void InitializeSlotUi(){
			butSlotsAdd.Click += ButSlotsAdd_Click;
			butSlotsSave.Click += ButSlotsSave_Click;
			butSlotsRemove.Click += ButSlotsRemove_Click;
			listViewSlots.SelectedIndexChanged += (sender, args) => {
				PopulateSlotEditorFromSelection();
				UpdateSlotButtons();
			};
			listViewSlots.DoubleClick += (sender, args) => SaveSelectedSlot();
			listViewSlots.KeyDown += ListViewSlots_KeyDown;
			comboSlotsEditSource.SelectedIndexChanged += (sender, args) => UpdateSlotEditorSourceFields();
			textSlotsEditName.TextChanged += (sender, args) => UpdateSlotEditorToolNameFromSlotName();
			comboSlotsEditApiModel.DropDown += ComboSlotsEditApiModel_DropDown;
			textSlotsEditApiKey.UseSystemPasswordChar = true;
			SetSlotToolTips();
			UpdateSlotEditorSourceFields();
			UpdateSlotButtons();
		}
		private void SetSlotToolTips(){
			toolTip1.SetToolTip(listViewSlots, "Configured model slots. The bold slot is used for normal chat.");
			toolTip1.SetToolTip(groupBoxSlotConfig, "Edit the selected slot or fill these fields and add a new slot.");
			toolTip1.SetToolTip(textSlotsEditName, "Short unique slot name, for example main, critic, judge, or coder.");
			toolTip1.SetToolTip(comboSlotsEditSource, "Choose whether this slot points to a local GGUF model or an API model.");
			toolTip1.SetToolTip(textSlotsEditModel, "Local GGUF model path for this slot. Relative paths are resolved from the Models folder.");
			toolTip1.SetToolTip(butSlotsEditUseSelectedModel, "Use the selected model from the model list for this slot.");
			toolTip1.SetToolTip(textSlotsEditApiUrl, "API base URL for API slots, for example http://localhost:1234 or an OpenAI-compatible endpoint.");
			toolTip1.SetToolTip(textSlotsEditApiKey, "Bearer API key for this slot. Leave blank for local servers that do not require a key.");
			toolTip1.SetToolTip(comboSlotsEditApiModel, "API model name for this slot. Open the drop-down to fetch /v1/models from the configured API URL.");
			toolTip1.SetToolTip(checkSlotsEditStore, "Send store=true when this slot is used as the main API chat slot.");
			toolTip1.SetToolTip(textSlotsEditInstructions, "Optional system instructions used when this slot is called directly or as a tool.");
			toolTip1.SetToolTip(textSlotsEditToolName, "Function name exposed to models when this API slot is enabled as a tool.");
			toolTip1.SetToolTip(checkSlotsEditChat, "Use this slot for normal chat. Only one slot can be the chat slot.");
			toolTip1.SetToolTip(checkSlotsEditDialectic, "Reserve this slot for dialectic mode routing.");
			toolTip1.SetToolTip(checkSlotsEditTool, "Expose this API slot as a callable model tool, such as ask_critic.");
			toolTip1.SetToolTip(checkSlotsEditServer, "Expose this slot through the API server as lmstud/<name>.");
			toolTip1.SetToolTip(butSlotsAdd, "Create a new slot from the fields above.");
			toolTip1.SetToolTip(butSlotsSave, "Save the fields above to the selected slot.");
			toolTip1.SetToolTip(butSlotsRemove, "Remove the selected slot. The main slot cannot be removed.");
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
			else PopulateSlotEditor(CreateNewSlotSuggestion());
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
			UpdateSlotEditorSourceFields();
		}
		private void UpdateSlotButtons(){
			var hasSelection = listViewSlots != null && listViewSlots.SelectedItems.Count == 1;
			butSlotsSave.Enabled = hasSelection;
			butSlotsRemove.Enabled = hasSelection && !string.Equals(((ModelSlot)listViewSlots.SelectedItems[0].Tag).Name, "main", StringComparison.OrdinalIgnoreCase);
		}
		private void UpdateSlotEditorSourceFields(){
			if(comboSlotsEditSource.SelectedIndex < 0) comboSlotsEditSource.SelectedIndex = 0;
			var api = comboSlotsEditSource.SelectedIndex == 1;
			textSlotsEditModel.Enabled = !api;
			textSlotsEditApiUrl.Enabled = textSlotsEditApiKey.Enabled = comboSlotsEditApiModel.Enabled = checkSlotsEditStore.Enabled = api;
			checkSlotsEditTool.Enabled = api;
			textSlotsEditToolName.Enabled = api;
			if(!api) checkSlotsEditTool.Checked = false;
		}
		private void UpdateSlotEditorToolNameFromSlotName(){
			if(_populatingSlotEditor) return;
			var generated = ModelSlotManager.BuildToolName(textSlotsEditName.Text);
			if(string.IsNullOrWhiteSpace(textSlotsEditToolName.Text) || string.Equals(textSlotsEditToolName.Text, _slotEditorAutoToolName, StringComparison.OrdinalIgnoreCase)){
				textSlotsEditToolName.Text = generated;
				_slotEditorAutoToolName = generated;
			}
		}
		private void ButSlotsAdd_Click(object sender, EventArgs e){
			if(!TryReadSlotEditor(null, true, out var slot)) return;
			ModelSlotManager.AddOrUpdate(slot);
			ApplyActiveSlotToRuntime(true);
			PopulateSlotsList();
			SelectSlot(slot.Name);
		}
		private void ButSlotsSave_Click(object sender, EventArgs e){SaveSelectedSlot();}
		private void SaveSelectedSlot(){
			if(listViewSlots.SelectedItems.Count != 1) return;
			var original = (ModelSlot)listViewSlots.SelectedItems[0].Tag;
			if(!TryReadSlotEditor(original.Name, false, out var slot)) return;
			ModelSlotManager.AddOrUpdate(slot, original.Name);
			ApplyActiveSlotToRuntime(true);
			PopulateSlotsList();
			SelectSlot(slot.Name);
		}
		private void ButSlotsRemove_Click(object sender, EventArgs e){
			if(listViewSlots.SelectedItems.Count != 1) return;
			var slot = (ModelSlot)listViewSlots.SelectedItems[0].Tag;
			if(MessageBox.Show(this, "Remove the " + slot.Name + " slot?", "LM Stud", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK) return;
			if(!ModelSlotManager.Remove(slot.Name)) MessageBox.Show(this, "The main slot cannot be removed. Edit it instead.", "LM Stud", MessageBoxButtons.OK, MessageBoxIcon.Information);
			ApplyActiveSlotToRuntime(true);
			PopulateSlotsList();
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
		private void ListViewSlots_KeyDown(object sender, KeyEventArgs e){
			if(e.KeyCode == Keys.Insert){
				e.SuppressKeyPress = true;
				PopulateSlotEditor(CreateNewSlotSuggestion());
				textSlotsEditName.Focus();
			} else if(e.KeyCode == Keys.Enter){
				e.SuppressKeyPress = true;
				SaveSelectedSlot();
			} else if(e.KeyCode == Keys.Delete){
				e.SuppressKeyPress = true;
				ButSlotsRemove_Click(sender, EventArgs.Empty);
			} else if(e.KeyCode == Keys.F5) PopulateSlotsList();
		}
		private ModelSlot CreateNewSlotSuggestion(){
			var names = ModelSlotManager.Slots.Select(slot => slot.Name).ToList();
			string name;
			if(!names.Any(existing => string.Equals(existing, "critic", StringComparison.OrdinalIgnoreCase))) name = "critic";
			else if(!names.Any(existing => string.Equals(existing, "judge", StringComparison.OrdinalIgnoreCase))) name = "judge";
			else{
				var i = 2;
				do{ name = "slot" + i++; } while(names.Any(existing => string.Equals(existing, name, StringComparison.OrdinalIgnoreCase)));
			}
			var selectedLocalPath = listViewModels.SelectedItems.Count == 1 ? listViewModels.SelectedItems[0].SubItems[1].Text : "";
			if(!string.IsNullOrWhiteSpace(Common.APIClientModel))
				return new ModelSlot{
					Name = name, Source = ModelSlotSource.Api, ApiBaseUrl = Common.APIClientUrl, ApiKey = Common.APIClientKey, ApiModel = Common.APIClientModel,
					ApiStore = Common.APIClientStore, ToolName = ModelSlotManager.BuildToolName(name), Use = ModelSlotUse.Tool | ModelSlotUse.Server
				};
			return new ModelSlot{
				Name = name, Source = ModelSlotSource.Local, LocalPath = selectedLocalPath, ToolName = ModelSlotManager.BuildToolName(name), Use = ModelSlotUse.Server
			};
		}
		private void ApplyActiveSlotToRuntime(bool updateControls, bool registerTools = true){
			var slot = ModelSlotManager.GetActiveChatSlot();
			if(slot == null) return;
			Common.ActiveModelSlotName = slot.Name;
			Action updateUi = null;
			if(slot.Source == ModelSlotSource.Api){
				Common.APIClientEnable = true;
				Common.APIClientUrl = slot.ApiBaseUrl ?? "";
				Common.APIClientKey = slot.ApiKey ?? "";
				Common.APIClientModel = slot.ApiModel ?? "";
				Common.APIClientStore = slot.ApiStore;
				if(updateControls) updateUi = () => {
					checkApiClientEnable.Checked = true;
					textApiClientUrl.Text = Common.APIClientUrl;
					textApiClientKey.Text = Common.APIClientKey;
					comboApiClientModel.Text = Common.APIClientModel;
					checkApiClientStore.Checked = Common.APIClientStore;
				};
			} else{
				Common.APIClientEnable = false;
				if(updateControls) updateUi = () => { checkApiClientEnable.Checked = false; };
			}
			if(registerTools) Tools.RegisterTools();
			else Tools.InvalidateToolsJsonCache();
			RunOnUiThread(() => {
				updateUi?.Invoke();
				SetModelStatus();
			});
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
	}
}
