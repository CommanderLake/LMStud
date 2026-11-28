using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Drawing.Design;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using System.Windows.Forms.Design;
namespace LMStud{
	[DefaultProperty(nameof(TabPages))]
	[DefaultEvent(nameof(SelectedIndexChanged))]
	[Designer(typeof(TabDesigner))]
	public partial class ComboTabControl : UserControl, ISupportInitialize{
		private readonly Panel pagePanel = new Panel{ Dock = DockStyle.Fill };
		private readonly TabPageCollection tabPages;
		private bool committedDropDownSelection;
		private bool initializing;
		private int pendingSelectedIndex = -1;
		private ComboTabPage selectedTab;
		private ComboBox selector;
		private object[] selectorItems;
		private SelectorWindow selectorWindow;
		private bool synchronizationQueued;
		private bool synchronizingPages;
		private bool updatingSelector;
		public ComboTabControl(){
			tabPages = new TabPageCollection(this);
			InitializeComponent();
			Controls.Add(pagePanel);
			pagePanel.ControlRemoved += (_, e) => {
				if(!Disposing && e.Control is ComboTabPage page) tabPages.Remove(page);
			};
		}
		[Category("Behavior")]
		[DefaultValue(null)]
		[Description("The ComboBox whose items define the pages.")]
		public ComboBox Selector{
			get => selector;
			set{
				if(value == selector) return;
				if(value?.IsDisposed == true) throw new ObjectDisposedException(nameof(value));
				DetachSelector();
				selector = value;
				if(selector == null) return;
				selector.SelectedIndexChanged += SelectorIndexChanged;
				selector.DropDown += SelectorDropDown;
				selector.SelectionChangeCommitted += SelectorSelectionCommitted;
				selector.DropDownClosed += SelectorDropDownClosed;
				selector.DataSourceChanged += SelectorItemsChanged;
				selector.DisplayMemberChanged += SelectorItemsChanged;
				selector.HandleCreated += SelectorHandleCreated;
				selector.Disposed += SelectorDisposed;
				if(selector.IsHandleCreated) WatchSelectorItems();
				SynchronizePages();
			}
		}
		[Category("Behavior")]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
		[Editor(typeof(CollectionEditor), typeof(UITypeEditor))]
		public TabPageCollection TabPages{
			get{
				SynchronizePages();
				return tabPages;
			}
		}
		[Browsable(false)] public int TabCount => TabPages.Count;
		[Browsable(false)]
		[DefaultValue(-1)]
		public int SelectedIndex{
			get{
				SynchronizePages();
				return tabPages.IndexOf(selectedTab);
			}
			set{
				SynchronizePages();
				if(value < -1 || (value >= tabPages.Count && IsHandleCreated && !initializing)) throw new ArgumentOutOfRangeException(nameof(value));
				pendingSelectedIndex = value >= tabPages.Count ? value : -1;
				SelectPage(value >= 0 && value < tabPages.Count ? tabPages[value] : null);
			}
		}
		[Browsable(false)]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public ComboTabPage SelectedTab{
			get{
				SynchronizePages();
				return selectedTab;
			}
			set{
				SynchronizePages();
				pendingSelectedIndex = -1;
				SelectPage(tabPages.Contains(value) ? value : null);
			}
		}
		void ISupportInitialize.BeginInit(){initializing = true;}
		void ISupportInitialize.EndInit(){
			initializing = false;
			SynchronizePages();
		}
		public event EventHandler<ComboTabCancelEventArgs> Deselecting;
		public event EventHandler<ComboTabEventArgs> Deselected;
		public event EventHandler<ComboTabCancelEventArgs> Selecting;
		public event EventHandler<ComboTabEventArgs> Selected;
		public event EventHandler SelectedIndexChanged;
		public void SelectTab(int index){SelectedTab = TabPages[index];}
		public void SelectTab(string key){SelectTab(TabPages[key]);}
		public void SelectTab(ComboTabPage page){
			if(page == null) throw new ArgumentNullException(nameof(page));
			SelectTab(TabPages.IndexOf(page));
		}
		public void DeselectTab(int index){DeselectTab(TabPages[index]);}
		public void DeselectTab(string key){DeselectTab(TabPages[key]);}
		public void DeselectTab(ComboTabPage page){
			if(page == null) throw new ArgumentNullException(nameof(page));
			if(page == selectedTab) SelectedIndex = TabCount > 1 ? (SelectedIndex + 1)%TabCount : -1;
		}
		protected override ControlCollection CreateControlsInstance(){return new ComboTabControls(this);}
		protected override bool ProcessCmdKey(ref Message msg, Keys keyData){
			if(TabCount > 0 && selector?.DroppedDown != true)
				switch(keyData){
					case Keys.Control | Keys.Tab:
					case Keys.Control | Keys.PageDown:
						MoveSelection(true);
						return true;
					case Keys.Control | Keys.Shift | Keys.Tab:
					case Keys.Control | Keys.PageUp:
						MoveSelection(false);
						return true;
				}
			return base.ProcessCmdKey(ref msg, keyData);
		}
		private void MoveSelection(bool forward){
			if(TabCount == 0) return;
			var index = SelectedIndex;
			SelectedIndex = forward ? (index + 1)%TabCount : index <= 0 ? TabCount - 1 : index - 1;
		}
		private void DetachSelector(){
			if(selector == null) return;
			selector.SelectedIndexChanged -= SelectorIndexChanged;
			selector.DropDown -= SelectorDropDown;
			selector.SelectionChangeCommitted -= SelectorSelectionCommitted;
			selector.DropDownClosed -= SelectorDropDownClosed;
			selector.DataSourceChanged -= SelectorItemsChanged;
			selector.DisplayMemberChanged -= SelectorItemsChanged;
			selector.HandleCreated -= SelectorHandleCreated;
			selector.Disposed -= SelectorDisposed;
			selectorWindow?.ReleaseHandle();
			selectorWindow = null;
			selector = null;
			selectorItems = null;
			synchronizationQueued = false;
			committedDropDownSelection = false;
		}
		private void SelectorIndexChanged(object sender, EventArgs e){
			if(sender == selector && !selector.DroppedDown) SelectFromComboBox();
		}
		private void SelectorDropDown(object sender, EventArgs e){committedDropDownSelection = false;}
		private void SelectorSelectionCommitted(object sender, EventArgs e){committedDropDownSelection = true;}
		private void SelectorDropDownClosed(object sender, EventArgs e){
			if(committedDropDownSelection) SelectFromComboBox();
			else RefreshSelector();
		}
		private void SelectorHandleCreated(object sender, EventArgs e){
			if(sender != selector) return;
			synchronizationQueued = false;
			WatchSelectorItems();
			SynchronizePages();
		}
		private void SelectorItemsChanged(object sender, EventArgs e){SynchronizePages();}
		private void SelectorDisposed(object sender, EventArgs e){
			if(sender == selector) DetachSelector();
		}
		private void SelectFromComboBox(){
			var combo = selector;
			if(updatingSelector || initializing || synchronizingPages || combo == null) return;
			SynchronizePages();
			if(combo != selector) return;
			var index = combo.SelectedIndex;
			SelectPage(index >= 0 && index < tabPages.Count ? tabPages[index] : null);
			RefreshSelector();
		}
		private void SelectPage(ComboTabPage page){
			if(page == selectedTab) return;
			var previous = selectedTab;
			var previousIndex = tabPages.IndexOf(previous);
			var moveFocus = previous?.ContainsFocus == true;
			var leaving = new ComboTabCancelEventArgs(previous, previousIndex);
			Deselecting?.Invoke(this, leaving);
			if(leaving.Cancel || selectedTab != previous) return;
			if(moveFocus && AutoValidate != AutoValidate.Disable && !Validate() && AutoValidate != AutoValidate.EnableAllowFocusChange) return;
			Deselected?.Invoke(this, new ComboTabEventArgs(previous, previousIndex));
			var entering = new ComboTabCancelEventArgs(page, tabPages.IndexOf(page));
			Selecting?.Invoke(this, entering);
			if(entering.Cancel || selectedTab != previous || (page != null && !tabPages.Contains(page))) return;
			ShowPage(page);
			if(moveFocus && selectedTab == page && page?.SelectNextControl(null, true, true, true, false) != true) selector?.Focus();
		}
		private void ShowPage(ComboTabPage page){
			var previous = selectedTab;
			selectedTab = page;
			if(page != null){
				page.Visible = true;
				page.BringToFront();
			}
			if(previous != null && previous != page) previous.Visible = false;
			RefreshSelector();
			if(previous != page){
				Selected?.Invoke(this, new ComboTabEventArgs(page, tabPages.IndexOf(page)));
				SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
			}
		}
		private void AttachPage(ComboTabPage page){
			page.Visible = false;
			page.Dock = DockStyle.Fill;
			pagePanel.Controls.Add(page);
		}
		private void DetachPage(ComboTabPage page){
			if(page.Parent == pagePanel) pagePanel.Controls.Remove(page);
		}
		private void PagesChanged(ComboTabPage fallback){
			if(pendingSelectedIndex >= 0 && pendingSelectedIndex < tabPages.Count){
				fallback = tabPages[pendingSelectedIndex];
				pendingSelectedIndex = -1;
			}
			ShowPage(fallback);
		}
		private void RefreshSelector(){
			var combo = selector;
			if(combo == null || combo.IsDisposed || updatingSelector || initializing || Disposing || IsDisposed) return;
			var index = tabPages.IndexOf(selectedTab);
			if(index >= combo.Items.Count || index == combo.SelectedIndex) return;
			updatingSelector = true;
			try{ combo.SelectedIndex = index; } finally{ updatingSelector = false; }
		}
		private void SynchronizePages(){
			var combo = selector;
			if(combo == null || combo.IsDisposed || initializing || synchronizingPages || Disposing || IsDisposed) return;
			if(combo.DataSource != null && combo.BindingContext == null) return;
			var items = combo.Items.Cast<object>().ToArray();
			var titles = items.Select(combo.GetItemText).ToArray();
			if(selectorItems != null && items.SequenceEqual(selectorItems) && tabPages.Select(page => page.Text).SequenceEqual(titles)) return;
			synchronizingPages = true;
			try{
				var oldPages = tabPages.ToArray();
				var firstSync = selectorItems == null;
				var nextPages = new ComboTabPage[items.Length];
				var matched = new HashSet<ComboTabPage>();
				if(!firstSync && selectorItems.Length == oldPages.Length)
					for(var i = 0; i < items.Length; i++){
						for(var j = 0; j < oldPages.Length; j++)
							if(Equals(items[i], selectorItems[j]) && matched.Add(oldPages[j])){
								nextPages[i] = oldPages[j];
								break;
							}
					}
				var host = (IDesignerHost)Site?.GetService(typeof(IDesignerHost));
				for(var i = 0; i < nextPages.Length; i++){
					if(nextPages[i] == null){
						if((firstSync || oldPages.Length == nextPages.Length) && i < oldPages.Length && matched.Add(oldPages[i])) nextPages[i] = oldPages[i];
						else nextPages[i] = host == null ? new ComboTabPage() : (ComboTabPage)host.CreateComponent(typeof(ComboTabPage));
					}
					nextPages[i].Text = titles[i];
				}
				var index = combo.SelectedIndex;
				var previousCount = selectorItems?.Length ?? 0;
				selectorItems = items;
				tabPages.SetPages(nextPages, oldPages);
				var page = index >= 0 && index < nextPages.Length ? nextPages[index] : null;
				if(page == null && nextPages.Contains(selectedTab)) page = selectedTab;
				if(page == null && nextPages.Length > 0 && (firstSync || previousCount == 0 || selectedTab != null)) page = nextPages[0];
				PagesChanged(page);
				foreach(var removed in oldPages.Where(old => !nextPages.Contains(old)))
					if(host != null && removed.Site != null) host.DestroyComponent(removed);
					else removed.Dispose();
			} finally{ synchronizingPages = false; }
		}
		private void WatchSelectorItems(){
			selectorWindow = new SelectorWindow(this);
			selectorWindow.AssignHandle(selector.Handle);
		}
		private void QueuePageSynchronization(){
			var combo = selector;
			if(synchronizationQueued || combo == null || !combo.IsHandleCreated || combo.Disposing) return;
			synchronizationQueued = true;
			combo.BeginInvoke((MethodInvoker)(() => {
				if(combo != selector) return;
				synchronizationQueued = false;
				SynchronizePages();
			}));
		}

		// ComboBox.ObjectCollection has no change event. Read it after WinForms finishes each native edit.
		private sealed class SelectorWindow : NativeWindow{
			private readonly ComboTabControl owner;
			internal SelectorWindow(ComboTabControl owner){this.owner = owner;}
			protected override void WndProc(ref Message m){
				const int CB_ADDSTRING = 0x0143;
				const int CB_DELETESTRING = 0x0144;
				const int CB_INSERTSTRING = 0x014A;
				const int CB_RESETCONTENT = 0x014B;
				base.WndProc(ref m);
				if(m.Msg == CB_ADDSTRING || m.Msg == CB_DELETESTRING || m.Msg == CB_INSERTSTRING || m.Msg == CB_RESETCONTENT) owner.QueuePageSynchronization();
			}
		}
		public sealed class TabPageCollection : Collection<ComboTabPage>{
			private readonly ComboTabControl owner;
			internal TabPageCollection(ComboTabControl owner){this.owner = owner;}
			public ComboTabPage this[string key]{
				get{
					var index = IndexOfKey(key);
					return index >= 0 ? this[index] : null;
				}
			}
			internal void SetPages(ComboTabPage[] pages, ComboTabPage[] oldPages){
				Items.Clear();
				foreach(var page in pages) Items.Add(page);
				foreach(var page in oldPages)
					if(!pages.Contains(page))
						owner.DetachPage(page);
				foreach(var page in pages)
					if(page.Parent != owner.pagePanel)
						owner.AttachPage(page);
			}
			public void Add(string text){Add(new ComboTabPage(text));}
			public void Add(string key, string text){Add(new ComboTabPage(text){ Name = key });}
			public void AddRange(ComboTabPage[] pages){
				if(pages == null) throw new ArgumentNullException(nameof(pages));
				foreach(var page in pages) Add(page);
			}
			public int IndexOfKey(string key){
				if(!string.IsNullOrEmpty(key))
					for(var i = 0; i < Count; i++)
						if(string.Equals(this[i].Name, key, StringComparison.OrdinalIgnoreCase))
							return i;
				return -1;
			}
			public bool ContainsKey(string key){return IndexOfKey(key) >= 0;}
			public void RemoveByKey(string key){
				var index = IndexOfKey(key);
				if(index >= 0) RemoveAt(index);
			}
			public void Insert(int index, string text){Insert(index, new ComboTabPage(text));}
			public void Insert(int index, string key, string text){Insert(index, new ComboTabPage(text){ Name = key });}
			protected override void InsertItem(int index, ComboTabPage page){
				if(page == null) throw new ArgumentNullException(nameof(page));
				if(Contains(page)) return;
				base.InsertItem(index, page);
				owner.AttachPage(page);
				owner.PagesChanged(Count == 1 ? page : owner.selectedTab);
			}
			protected override void SetItem(int index, ComboTabPage page){
				if(page == null) throw new ArgumentNullException(nameof(page));
				var oldPage = this[index];
				if(page == oldPage) return;
				if(Contains(page)) throw new ArgumentException("The page is already in this control.", nameof(page));
				base.SetItem(index, page);
				owner.DetachPage(oldPage);
				owner.AttachPage(page);
				owner.PagesChanged(owner.selectedTab == oldPage ? page : owner.selectedTab);
			}
			protected override void RemoveItem(int index){
				var page = this[index];
				base.RemoveItem(index);
				owner.DetachPage(page);
				owner.PagesChanged(owner.selectedTab == page ? Count > 0 ? this[0] : null : owner.selectedTab);
			}
			protected override void ClearItems(){
				var pages = new List<ComboTabPage>(Items);
				base.ClearItems();
				foreach(var page in pages) owner.DetachPage(page);
				owner.pendingSelectedIndex = -1;
				owner.PagesChanged(null);
			}
		}
		private sealed class ComboTabControls : ControlCollection{
			private readonly ComboTabControl owner;
			internal ComboTabControls(ComboTabControl owner) : base(owner){this.owner = owner;}
			public override void Add(Control value){
				if(value is ComboTabPage page) owner.TabPages.Add(page);
				else base.Add(value);
			}
			public override void Remove(Control value){
				if(value is ComboTabPage page) owner.TabPages.Remove(page);
				else base.Remove(value);
			}
			public override void Clear(){
				owner.TabPages.Clear();
				for(var i = Count - 1; i >= 0; i--){
					var child = this[i];
					if(child != owner.pagePanel) base.Remove(child);
				}
			}
		}
		internal sealed class TabDesigner : ParentControlDesigner{
			private DesignerActionListCollection actionLists;
			private IComponentChangeService changes;
			private ComboTabControl Tabs => (ComboTabControl)Control;
			public override ICollection AssociatedComponents => Tabs.TabPages;
			public override DesignerActionListCollection ActionLists =>
				actionLists ?? (actionLists = new DesignerActionListCollection{ new TabActions(Tabs) });
			public override DesignerVerbCollection Verbs =>
				new DesignerVerbCollection{
					new DesignerVerb("Add Tab", (_, __) => EditTabs(true)){ Enabled = Tabs.Selector == null },
					new DesignerVerb("Remove Tab", (_, __) => EditTabs(false)){ Enabled = Tabs.Selector == null && Tabs.SelectedTab != null }
				};
			public override bool CanParent(Control control){return false;}
			public override void Initialize(IComponent component){
				base.Initialize(component);
				changes = (IComponentChangeService)GetService(typeof(IComponentChangeService));
				if(changes != null) changes.ComponentChanged += ComponentChanged;
			}
			protected override void Dispose(bool disposing){
				if(disposing && changes != null) changes.ComponentChanged -= ComponentChanged;
				base.Dispose(disposing);
			}
			private void ComponentChanged(object sender, ComponentChangedEventArgs e){
				if(e.Component == Tabs.Selector) Tabs.SynchronizePages();
				else if(e.Component != Tabs || (e.Member?.Name != nameof(Selector) && e.Member?.Name != nameof(TabPages))) return;
				((DesignerActionUIService)GetService(typeof(DesignerActionUIService)))?.Refresh(Tabs);
			}
			private void EditTabs(bool add){
				if(Tabs.Selector != null) return;
				var host = (IDesignerHost)GetService(typeof(IDesignerHost));
				var changes = (IComponentChangeService)GetService(typeof(IComponentChangeService));
				var property = TypeDescriptor.GetProperties(Tabs)[nameof(TabPages)];
				using(var transaction = host.CreateTransaction(add ? "Add Tab" : "Remove Tab")){
					try{ changes.OnComponentChanging(Tabs, property); } catch(CheckoutException e) when(e == CheckoutException.Canceled){ return; }
					if(add){
						var page = (ComboTabPage)host.CreateComponent(typeof(ComboTabPage));
						page.Text = page.Name;
						Tabs.TabPages.Add(page);
						Tabs.SelectedTab = page;
					} else if(Tabs.SelectedTab != null){ host.DestroyComponent(Tabs.SelectedTab); }
					changes.OnComponentChanged(Tabs, property, null, null);
					transaction.Commit();
				}
			}
		}
		private sealed class TabActions : DesignerActionList{
			internal TabActions(ComboTabControl tabs) : base(tabs){ Tabs = tabs; }
			internal ComboTabControl Tabs{ get; }

			[TypeConverter(typeof(PageIndexConverter))]
			[Editor(typeof(PageIndexEditor), typeof(UITypeEditor))]
			public int PageIndex{
				get => Tabs.SelectedIndex;
				set => TypeDescriptor.GetProperties(Tabs)[nameof(SelectedIndex)].SetValue(Tabs, value);
			}

			public override DesignerActionItemCollection GetSortedActionItems(){
				var items = new DesignerActionItemCollection();
				if(Tabs.TabCount > 0) items.Add(new DesignerActionPropertyItem(nameof(PageIndex), "Selected page"));
				return items;
			}
		}
		private sealed class PageIndexConverter : TypeConverter{
			public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType){
				if(destinationType == typeof(string) && context?.Instance is TabActions actions && value is int index){
					var pages = actions.Tabs.TabPages;
					return index >= 0 && index < pages.Count ? pages[index].Text : "(None)";
				}
				return base.ConvertTo(context, culture, value, destinationType);
			}
		}
		private sealed class PageIndexEditor : UITypeEditor{
			public override UITypeEditorEditStyle GetEditStyle(ITypeDescriptorContext context) => UITypeEditorEditStyle.DropDown;

			public override object EditValue(ITypeDescriptorContext context, IServiceProvider provider, object value){
				if(!(context?.Instance is TabActions actions) ||
					!(provider?.GetService(typeof(IWindowsFormsEditorService)) is IWindowsFormsEditorService editor)) return value;
				var titles = actions.Tabs.TabPages.Select(page => page.Text).ToArray();
				if(titles.Length == 0) return value;

				using(var list = new ListBox{ BorderStyle = BorderStyle.None }){
					list.Items.AddRange(titles);
					list.SelectedIndex = actions.PageIndex;
					list.Width = titles.Max(title => TextRenderer.MeasureText(title, list.Font).Width) + SystemInformation.VerticalScrollBarWidth;
					list.Height = Math.Min(titles.Length, 8)*list.ItemHeight;
					var selection = -1;
					list.MouseClick += (_, e) => {
						if(e.Button != MouseButtons.Left) return;
						selection = list.IndexFromPoint(e.Location);
						if(selection >= 0) editor.CloseDropDown();
					};
					list.PreviewKeyDown += (_, e) => {
						if(e.KeyCode == Keys.Enter || e.KeyCode == Keys.Escape) e.IsInputKey = true;
					};
					list.KeyDown += (_, e) => {
						if(e.KeyCode != Keys.Enter && e.KeyCode != Keys.Escape) return;
						if(e.KeyCode == Keys.Enter) selection = list.SelectedIndex;
						e.SuppressKeyPress = true;
						editor.CloseDropDown();
					};
					editor.DropDownControl(list);
					return selection >= 0 ? selection : value;
				}
			}
		}
	}
	[DefaultProperty(nameof(Text))]
	[ToolboxItem(false)]
	public class ComboTabPage : Panel{
		public ComboTabPage(){}
		public ComboTabPage(string text){Text = text;}
		[Browsable(true)]
		[Localizable(true)]
		[DefaultValue("")]
		[EditorBrowsable(EditorBrowsableState.Always)]
		public override string Text{get => base.Text; set => base.Text = value;}
	}
	public sealed class ComboTabEventArgs : EventArgs{
		internal ComboTabEventArgs(ComboTabPage page, int index){
			TabPage = page;
			TabPageIndex = index;
		}
		public ComboTabPage TabPage{get;}
		public int TabPageIndex{get;}
	}
	public sealed class ComboTabCancelEventArgs : CancelEventArgs{
		internal ComboTabCancelEventArgs(ComboTabPage page, int index){
			TabPage = page;
			TabPageIndex = index;
		}
		public ComboTabPage TabPage{get;}
		public int TabPageIndex{get;}
	}
}
