using LMStud.Properties;
namespace LMStud
{
	internal partial class Form1
	{
		/// <summary>
		/// Required designer variable.
		/// </summary>
		private System.ComponentModel.IContainer components = null;

		/// <summary>
		/// Clean up any resources being used.
		/// </summary>
		/// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
		protected override void Dispose(bool disposing)
		{
			if (disposing){
				_columnClickHandler?.UnregisterListView(listViewModels);
				_columnClickHandler?.UnregisterListView(listViewMeta);
				_columnClickHandler?.UnregisterListView(listViewHugSearch);
				_columnClickHandler?.UnregisterListView(listViewHugFiles);
				if(components != null){ components.Dispose(); }
			}
			base.Dispose(disposing);
		}

		#region Windows Form Designer generated code

		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
			this.components = new System.ComponentModel.Container();
			System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Form1));
			this.splitContainer1 = new System.Windows.Forms.SplitContainer();
			this.panelChat = new LMStud.MyFlowLayoutPanel();
			this.checkSpeak = new System.Windows.Forms.CheckBox();
			this.checkVoiceInput = new System.Windows.Forms.CheckBox();
			this.checkMarkdown = new System.Windows.Forms.CheckBox();
			this.checkStream = new System.Windows.Forms.CheckBox();
			this.butCodeBlock = new System.Windows.Forms.Button();
			this.butReset = new System.Windows.Forms.Button();
			this.butGen = new System.Windows.Forms.Button();
			this.textInput = new System.Windows.Forms.TextBox();
			this.splitContainer2 = new System.Windows.Forms.SplitContainer();
			this.listViewModels = new System.Windows.Forms.ListView();
			this.columnHeader1 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
			this.columnHeader2 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
			this.listViewMeta = new System.Windows.Forms.ListView();
			this.columnHeader3 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
			this.columnHeader4 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
			this.splitContainer3 = new System.Windows.Forms.SplitContainer();
			this.butSearch = new System.Windows.Forms.Button();
			this.textSearchTerm = new System.Windows.Forms.TextBox();
			this.label13 = new System.Windows.Forms.Label();
			this.listViewHugSearch = new System.Windows.Forms.ListView();
			this.columnHeader5 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
			this.columnHeader6 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
			this.columnHeader7 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
			this.columnHeader11 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
			this.columnHeader12 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
			this.columnHeader13 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
			this.columnHeader14 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
			this.progressBar1 = new System.Windows.Forms.ProgressBar();
			this.butDownload = new System.Windows.Forms.Button();
			this.listViewHugFiles = new System.Windows.Forms.ListView();
			this.columnHeader8 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
			this.columnHeader10 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
			this.tabControl1 = new System.Windows.Forms.TabControl();
			this.tabPage1 = new System.Windows.Forms.TabPage();
			this.tabPage2 = new System.Windows.Forms.TabPage();
			this.textModelsPath = new System.Windows.Forms.TextBox();
			this.groupBox6 = new System.Windows.Forms.GroupBox();
			this.numVadThreshold = new System.Windows.Forms.NumericUpDown();
			this.butVADDown = new System.Windows.Forms.Button();
			this.radioBasicVAD = new System.Windows.Forms.RadioButton();
			this.radioWhisperVAD = new System.Windows.Forms.RadioButton();
			this.label17 = new System.Windows.Forms.Label();
			this.comboVADModel = new System.Windows.Forms.ComboBox();
			this.label26 = new System.Windows.Forms.Label();
			this.groupBox5 = new System.Windows.Forms.GroupBox();
			this.checkDateTimeEnable = new System.Windows.Forms.CheckBox();
			this.groupBox4 = new System.Windows.Forms.GroupBox();
			this.textFileBasePath = new System.Windows.Forms.TextBox();
			this.linkFileInstruction = new System.Windows.Forms.LinkLabel();
			this.label22 = new System.Windows.Forms.Label();
			this.checkFileWriteEnable = new System.Windows.Forms.CheckBox();
			this.checkFileCreateEnable = new System.Windows.Forms.CheckBox();
			this.checkFileReadEnable = new System.Windows.Forms.CheckBox();
			this.checkFileListEnable = new System.Windows.Forms.CheckBox();
			this.groupBox3 = new System.Windows.Forms.GroupBox();
			this.checkWebpageFetchEnable = new System.Windows.Forms.CheckBox();
			this.groupBox2 = new System.Windows.Forms.GroupBox();
			this.numGoogleResults = new System.Windows.Forms.NumericUpDown();
			this.textGoogleSearchID = new System.Windows.Forms.TextBox();
			this.textGoogleApiKey = new System.Windows.Forms.TextBox();
			this.label21 = new System.Windows.Forms.Label();
			this.checkGoogleEnable = new System.Windows.Forms.CheckBox();
			this.label20 = new System.Windows.Forms.Label();
			this.label19 = new System.Windows.Forms.Label();
			this.groupBox1 = new System.Windows.Forms.GroupBox();
			this.numWakeWordSimilarity = new System.Windows.Forms.NumericUpDown();
			this.numWhisperTemp = new System.Windows.Forms.NumericUpDown();
			this.numFreqThreshold = new System.Windows.Forms.NumericUpDown();
			this.textWakeWord = new System.Windows.Forms.TextBox();
			this.label25 = new System.Windows.Forms.Label();
			this.label24 = new System.Windows.Forms.Label();
			this.label18 = new System.Windows.Forms.Label();
			this.checkWhisperUseGPU = new System.Windows.Forms.CheckBox();
			this.label16 = new System.Windows.Forms.Label();
			this.butWhispDown = new System.Windows.Forms.Button();
			this.comboWhisperModel = new System.Windows.Forms.ComboBox();
			this.label15 = new System.Windows.Forms.Label();
			this.groupCPUParamsBatch = new System.Windows.Forms.GroupBox();
			this.numThreadsBatch = new System.Windows.Forms.NumericUpDown();
			this.label14 = new System.Windows.Forms.Label();
			this.groupCPUParams = new System.Windows.Forms.GroupBox();
			this.numThreads = new System.Windows.Forms.NumericUpDown();
			this.label2 = new System.Windows.Forms.Label();
			this.groupAdvanced = new System.Windows.Forms.GroupBox();
			this.numMinP = new System.Windows.Forms.NumericUpDown();
			this.comboNUMAStrat = new System.Windows.Forms.ComboBox();
			this.numRepPen = new System.Windows.Forms.NumericUpDown();
			this.numBatchSize = new System.Windows.Forms.NumericUpDown();
			this.numTopK = new System.Windows.Forms.NumericUpDown();
			this.numTopP = new System.Windows.Forms.NumericUpDown();
			this.label23 = new System.Windows.Forms.Label();
			this.checkFlashAttn = new System.Windows.Forms.CheckBox();
			this.checkMLock = new System.Windows.Forms.CheckBox();
			this.checkMMap = new System.Windows.Forms.CheckBox();
			this.label6 = new System.Windows.Forms.Label();
			this.label12 = new System.Windows.Forms.Label();
			this.label11 = new System.Windows.Forms.Label();
			this.label8 = new System.Windows.Forms.Label();
			this.label9 = new System.Windows.Forms.Label();
			this.groupCommon = new System.Windows.Forms.GroupBox();
			this.numCtxSize = new System.Windows.Forms.NumericUpDown();
			this.numGPULayers = new System.Windows.Forms.NumericUpDown();
			this.numNGen = new System.Windows.Forms.NumericUpDown();
			this.numTemp = new System.Windows.Forms.NumericUpDown();
			this.label5 = new System.Windows.Forms.Label();
			this.label7 = new System.Windows.Forms.Label();
			this.label10 = new System.Windows.Forms.Label();
			this.label4 = new System.Windows.Forms.Label();
			this.label3 = new System.Windows.Forms.Label();
			this.butBrowse = new System.Windows.Forms.Button();
			this.butApply = new System.Windows.Forms.Button();
			this.label1 = new System.Windows.Forms.Label();
			this.textSystemPrompt = new System.Windows.Forms.TextBox();
			this.tabPage3 = new System.Windows.Forms.TabPage();
			this.checkLoadAuto = new System.Windows.Forms.CheckBox();
			this.butUnload = new System.Windows.Forms.Button();
			this.butLoad = new System.Windows.Forms.Button();
			this.tabPage4 = new System.Windows.Forms.TabPage();
			this.folderBrowserDialog1 = new System.Windows.Forms.FolderBrowserDialog();
			this.statusStrip1 = new System.Windows.Forms.StatusStrip();
			this.toolStripStatusLabel1 = new System.Windows.Forms.ToolStripStatusLabel();
			this.labelTokens = new System.Windows.Forms.ToolStripStatusLabel();
			this.labelTPS = new System.Windows.Forms.ToolStripStatusLabel();
			this.labelPreGen = new System.Windows.Forms.ToolStripStatusLabel();
			this.toolTip1 = new System.Windows.Forms.ToolTip(this.components);
			((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
			this.splitContainer1.Panel1.SuspendLayout();
			this.splitContainer1.Panel2.SuspendLayout();
			this.splitContainer1.SuspendLayout();
			((System.ComponentModel.ISupportInitialize)(this.splitContainer2)).BeginInit();
			this.splitContainer2.Panel1.SuspendLayout();
			this.splitContainer2.Panel2.SuspendLayout();
			this.splitContainer2.SuspendLayout();
			((System.ComponentModel.ISupportInitialize)(this.splitContainer3)).BeginInit();
			this.splitContainer3.Panel1.SuspendLayout();
			this.splitContainer3.Panel2.SuspendLayout();
			this.splitContainer3.SuspendLayout();
			this.tabControl1.SuspendLayout();
			this.tabPage1.SuspendLayout();
			this.tabPage2.SuspendLayout();
			this.groupBox6.SuspendLayout();
			((System.ComponentModel.ISupportInitialize)(this.numVadThreshold)).BeginInit();
			this.groupBox5.SuspendLayout();
			this.groupBox4.SuspendLayout();
			this.groupBox3.SuspendLayout();
			this.groupBox2.SuspendLayout();
			((System.ComponentModel.ISupportInitialize)(this.numGoogleResults)).BeginInit();
			this.groupBox1.SuspendLayout();
			((System.ComponentModel.ISupportInitialize)(this.numWakeWordSimilarity)).BeginInit();
			((System.ComponentModel.ISupportInitialize)(this.numWhisperTemp)).BeginInit();
			((System.ComponentModel.ISupportInitialize)(this.numFreqThreshold)).BeginInit();
			this.groupCPUParamsBatch.SuspendLayout();
			((System.ComponentModel.ISupportInitialize)(this.numThreadsBatch)).BeginInit();
			this.groupCPUParams.SuspendLayout();
			((System.ComponentModel.ISupportInitialize)(this.numThreads)).BeginInit();
			this.groupAdvanced.SuspendLayout();
			((System.ComponentModel.ISupportInitialize)(this.numMinP)).BeginInit();
			((System.ComponentModel.ISupportInitialize)(this.numRepPen)).BeginInit();
			((System.ComponentModel.ISupportInitialize)(this.numBatchSize)).BeginInit();
			((System.ComponentModel.ISupportInitialize)(this.numTopK)).BeginInit();
			((System.ComponentModel.ISupportInitialize)(this.numTopP)).BeginInit();
			this.groupCommon.SuspendLayout();
			((System.ComponentModel.ISupportInitialize)(this.numCtxSize)).BeginInit();
			((System.ComponentModel.ISupportInitialize)(this.numGPULayers)).BeginInit();
			((System.ComponentModel.ISupportInitialize)(this.numNGen)).BeginInit();
			((System.ComponentModel.ISupportInitialize)(this.numTemp)).BeginInit();
			this.tabPage3.SuspendLayout();
			this.tabPage4.SuspendLayout();
			this.statusStrip1.SuspendLayout();
			this.SuspendLayout();
			// 
			// splitContainer1
			// 
			resources.ApplyResources(this.splitContainer1, "splitContainer1");
			this.splitContainer1.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
			this.splitContainer1.FixedPanel = System.Windows.Forms.FixedPanel.Panel2;
			this.splitContainer1.Name = "splitContainer1";
			// 
			// splitContainer1.Panel1
			// 
			resources.ApplyResources(this.splitContainer1.Panel1, "splitContainer1.Panel1");
			this.splitContainer1.Panel1.Controls.Add(this.panelChat);
			this.toolTip1.SetToolTip(this.splitContainer1.Panel1, resources.GetString("splitContainer1.Panel1.ToolTip"));
			// 
			// splitContainer1.Panel2
			// 
			resources.ApplyResources(this.splitContainer1.Panel2, "splitContainer1.Panel2");
			this.splitContainer1.Panel2.Controls.Add(this.checkSpeak);
			this.splitContainer1.Panel2.Controls.Add(this.checkVoiceInput);
			this.splitContainer1.Panel2.Controls.Add(this.checkMarkdown);
			this.splitContainer1.Panel2.Controls.Add(this.checkStream);
			this.splitContainer1.Panel2.Controls.Add(this.butCodeBlock);
			this.splitContainer1.Panel2.Controls.Add(this.butReset);
			this.splitContainer1.Panel2.Controls.Add(this.butGen);
			this.splitContainer1.Panel2.Controls.Add(this.textInput);
			this.toolTip1.SetToolTip(this.splitContainer1.Panel2, resources.GetString("splitContainer1.Panel2.ToolTip"));
			this.toolTip1.SetToolTip(this.splitContainer1, resources.GetString("splitContainer1.ToolTip"));
			// 
			// panelChat
			// 
			resources.ApplyResources(this.panelChat, "panelChat");
			this.panelChat.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
			this.panelChat.CausesValidation = false;
			this.panelChat.Name = "panelChat";
			this.toolTip1.SetToolTip(this.panelChat, resources.GetString("panelChat.ToolTip"));
			this.panelChat.Layout += new System.Windows.Forms.LayoutEventHandler(this.PanelChat_Layout);
			// 
			// checkSpeak
			// 
			resources.ApplyResources(this.checkSpeak, "checkSpeak");
			this.checkSpeak.Name = "checkSpeak";
			this.toolTip1.SetToolTip(this.checkSpeak, resources.GetString("checkSpeak.ToolTip"));
			this.checkSpeak.UseVisualStyleBackColor = true;
			this.checkSpeak.CheckedChanged += new System.EventHandler(this.CheckSpeak_CheckedChanged);
			// 
			// checkVoiceInput
			// 
			resources.ApplyResources(this.checkVoiceInput, "checkVoiceInput");
			this.checkVoiceInput.Name = "checkVoiceInput";
			this.checkVoiceInput.ThreeState = true;
			this.toolTip1.SetToolTip(this.checkVoiceInput, resources.GetString("checkVoiceInput.ToolTip"));
			this.checkVoiceInput.UseVisualStyleBackColor = true;
			this.checkVoiceInput.CheckedChanged += new System.EventHandler(this.CheckVoiceInput_CheckedChanged);
			// 
			// checkMarkdown
			// 
			resources.ApplyResources(this.checkMarkdown, "checkMarkdown");
			this.checkMarkdown.Checked = true;
			this.checkMarkdown.CheckState = System.Windows.Forms.CheckState.Checked;
			this.checkMarkdown.Name = "checkMarkdown";
			this.toolTip1.SetToolTip(this.checkMarkdown, resources.GetString("checkMarkdown.ToolTip"));
			this.checkMarkdown.UseVisualStyleBackColor = true;
			this.checkMarkdown.CheckedChanged += new System.EventHandler(this.CheckMarkdown_CheckedChanged);
			// 
			// checkStream
			// 
			resources.ApplyResources(this.checkStream, "checkStream");
			this.checkStream.Checked = true;
			this.checkStream.CheckState = System.Windows.Forms.CheckState.Checked;
			this.checkStream.Name = "checkStream";
			this.toolTip1.SetToolTip(this.checkStream, resources.GetString("checkStream.ToolTip"));
			this.checkStream.UseVisualStyleBackColor = true;
			// 
			// butCodeBlock
			// 
			resources.ApplyResources(this.butCodeBlock, "butCodeBlock");
			this.butCodeBlock.Name = "butCodeBlock";
			this.toolTip1.SetToolTip(this.butCodeBlock, resources.GetString("butCodeBlock.ToolTip"));
			this.butCodeBlock.UseVisualStyleBackColor = true;
			this.butCodeBlock.Click += new System.EventHandler(this.ButCodeBlock_Click);
			// 
			// butReset
			// 
			resources.ApplyResources(this.butReset, "butReset");
			this.butReset.Name = "butReset";
			this.toolTip1.SetToolTip(this.butReset, resources.GetString("butReset.ToolTip"));
			this.butReset.UseVisualStyleBackColor = true;
			this.butReset.Click += new System.EventHandler(this.ButReset_Click);
			// 
			// butGen
			// 
			resources.ApplyResources(this.butGen, "butGen");
			this.butGen.Name = "butGen";
			this.butGen.Text = global::LMStud.Properties.Resources.Generate;
			this.toolTip1.SetToolTip(this.butGen, resources.GetString("butGen.ToolTip"));
			this.butGen.UseVisualStyleBackColor = true;
			this.butGen.Click += new System.EventHandler(this.ButGen_Click);
			// 
			// textInput
			// 
			resources.ApplyResources(this.textInput, "textInput");
			this.textInput.AllowDrop = true;
			this.textInput.Name = "textInput";
			this.toolTip1.SetToolTip(this.textInput, resources.GetString("textInput.ToolTip"));
			this.textInput.DragDrop += new System.Windows.Forms.DragEventHandler(this.TextInput_DragDrop);
			this.textInput.DragEnter += new System.Windows.Forms.DragEventHandler(this.TextInput_DragEnter);
			this.textInput.KeyDown += new System.Windows.Forms.KeyEventHandler(this.TextInput_KeyDown);
			// 
			// splitContainer2
			// 
			resources.ApplyResources(this.splitContainer2, "splitContainer2");
			this.splitContainer2.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
			this.splitContainer2.Name = "splitContainer2";
			// 
			// splitContainer2.Panel1
			// 
			resources.ApplyResources(this.splitContainer2.Panel1, "splitContainer2.Panel1");
			this.splitContainer2.Panel1.Controls.Add(this.listViewModels);
			this.toolTip1.SetToolTip(this.splitContainer2.Panel1, resources.GetString("splitContainer2.Panel1.ToolTip"));
			// 
			// splitContainer2.Panel2
			// 
			resources.ApplyResources(this.splitContainer2.Panel2, "splitContainer2.Panel2");
			this.splitContainer2.Panel2.Controls.Add(this.listViewMeta);
			this.toolTip1.SetToolTip(this.splitContainer2.Panel2, resources.GetString("splitContainer2.Panel2.ToolTip"));
			this.toolTip1.SetToolTip(this.splitContainer2, resources.GetString("splitContainer2.ToolTip"));
			// 
			// listViewModels
			// 
			resources.ApplyResources(this.listViewModels, "listViewModels");
			this.listViewModels.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.columnHeader1,
            this.columnHeader2});
			this.listViewModels.GridLines = true;
			this.listViewModels.HideSelection = false;
			this.listViewModels.MultiSelect = false;
			this.listViewModels.Name = "listViewModels";
			this.toolTip1.SetToolTip(this.listViewModels, resources.GetString("listViewModels.ToolTip"));
			this.listViewModels.UseCompatibleStateImageBehavior = false;
			this.listViewModels.View = System.Windows.Forms.View.Details;
			this.listViewModels.ItemSelectionChanged += new System.Windows.Forms.ListViewItemSelectionChangedEventHandler(this.ListViewModels_ItemSelectionChanged);
			this.listViewModels.DoubleClick += new System.EventHandler(this.ListViewModels_DoubleClick);
			this.listViewModels.KeyDown += new System.Windows.Forms.KeyEventHandler(this.ListViewModels_KeyDown);
			// 
			// columnHeader1
			// 
			resources.ApplyResources(this.columnHeader1, "columnHeader1");
			// 
			// columnHeader2
			// 
			resources.ApplyResources(this.columnHeader2, "columnHeader2");
			// 
			// listViewMeta
			// 
			resources.ApplyResources(this.listViewMeta, "listViewMeta");
			this.listViewMeta.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.columnHeader3,
            this.columnHeader4});
			this.listViewMeta.GridLines = true;
			this.listViewMeta.HideSelection = false;
			this.listViewMeta.MultiSelect = false;
			this.listViewMeta.Name = "listViewMeta";
			this.toolTip1.SetToolTip(this.listViewMeta, resources.GetString("listViewMeta.ToolTip"));
			this.listViewMeta.UseCompatibleStateImageBehavior = false;
			this.listViewMeta.View = System.Windows.Forms.View.Details;
			// 
			// columnHeader3
			// 
			resources.ApplyResources(this.columnHeader3, "columnHeader3");
			// 
			// columnHeader4
			// 
			resources.ApplyResources(this.columnHeader4, "columnHeader4");
			// 
			// splitContainer3
			// 
			resources.ApplyResources(this.splitContainer3, "splitContainer3");
			this.splitContainer3.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
			this.splitContainer3.Name = "splitContainer3";
			// 
			// splitContainer3.Panel1
			// 
			resources.ApplyResources(this.splitContainer3.Panel1, "splitContainer3.Panel1");
			this.splitContainer3.Panel1.Controls.Add(this.butSearch);
			this.splitContainer3.Panel1.Controls.Add(this.textSearchTerm);
			this.splitContainer3.Panel1.Controls.Add(this.label13);
			this.splitContainer3.Panel1.Controls.Add(this.listViewHugSearch);
			this.toolTip1.SetToolTip(this.splitContainer3.Panel1, resources.GetString("splitContainer3.Panel1.ToolTip"));
			// 
			// splitContainer3.Panel2
			// 
			resources.ApplyResources(this.splitContainer3.Panel2, "splitContainer3.Panel2");
			this.splitContainer3.Panel2.Controls.Add(this.progressBar1);
			this.splitContainer3.Panel2.Controls.Add(this.butDownload);
			this.splitContainer3.Panel2.Controls.Add(this.listViewHugFiles);
			this.toolTip1.SetToolTip(this.splitContainer3.Panel2, resources.GetString("splitContainer3.Panel2.ToolTip"));
			this.toolTip1.SetToolTip(this.splitContainer3, resources.GetString("splitContainer3.ToolTip"));
			// 
			// butSearch
			// 
			resources.ApplyResources(this.butSearch, "butSearch");
			this.butSearch.Name = "butSearch";
			this.toolTip1.SetToolTip(this.butSearch, resources.GetString("butSearch.ToolTip"));
			this.butSearch.UseVisualStyleBackColor = true;
			this.butSearch.Click += new System.EventHandler(this.ButSearch_Click);
			// 
			// textSearchTerm
			// 
			resources.ApplyResources(this.textSearchTerm, "textSearchTerm");
			this.textSearchTerm.Name = "textSearchTerm";
			this.toolTip1.SetToolTip(this.textSearchTerm, resources.GetString("textSearchTerm.ToolTip"));
			this.textSearchTerm.KeyDown += new System.Windows.Forms.KeyEventHandler(this.TextSearchTerm_KeyDown);
			// 
			// label13
			// 
			resources.ApplyResources(this.label13, "label13");
			this.label13.Name = "label13";
			this.toolTip1.SetToolTip(this.label13, resources.GetString("label13.ToolTip"));
			// 
			// listViewHugSearch
			// 
			resources.ApplyResources(this.listViewHugSearch, "listViewHugSearch");
			this.listViewHugSearch.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.columnHeader5,
            this.columnHeader6,
            this.columnHeader7,
            this.columnHeader11,
            this.columnHeader12,
            this.columnHeader13,
            this.columnHeader14});
			this.listViewHugSearch.GridLines = true;
			this.listViewHugSearch.HideSelection = false;
			this.listViewHugSearch.Name = "listViewHugSearch";
			this.toolTip1.SetToolTip(this.listViewHugSearch, resources.GetString("listViewHugSearch.ToolTip"));
			this.listViewHugSearch.UseCompatibleStateImageBehavior = false;
			this.listViewHugSearch.View = System.Windows.Forms.View.Details;
			this.listViewHugSearch.SelectedIndexChanged += new System.EventHandler(this.ListViewHugSearch_SelectedIndexChanged);
			// 
			// columnHeader5
			// 
			resources.ApplyResources(this.columnHeader5, "columnHeader5");
			// 
			// columnHeader6
			// 
			resources.ApplyResources(this.columnHeader6, "columnHeader6");
			// 
			// columnHeader7
			// 
			resources.ApplyResources(this.columnHeader7, "columnHeader7");
			// 
			// columnHeader11
			// 
			resources.ApplyResources(this.columnHeader11, "columnHeader11");
			// 
			// columnHeader12
			// 
			resources.ApplyResources(this.columnHeader12, "columnHeader12");
			// 
			// columnHeader13
			// 
			resources.ApplyResources(this.columnHeader13, "columnHeader13");
			// 
			// columnHeader14
			// 
			resources.ApplyResources(this.columnHeader14, "columnHeader14");
			// 
			// progressBar1
			// 
			resources.ApplyResources(this.progressBar1, "progressBar1");
			this.progressBar1.Name = "progressBar1";
			this.toolTip1.SetToolTip(this.progressBar1, resources.GetString("progressBar1.ToolTip"));
			// 
			// butDownload
			// 
			resources.ApplyResources(this.butDownload, "butDownload");
			this.butDownload.Name = "butDownload";
			this.butDownload.Text = global::LMStud.Properties.Resources.Download;
			this.toolTip1.SetToolTip(this.butDownload, resources.GetString("butDownload.ToolTip"));
			this.butDownload.UseVisualStyleBackColor = true;
			this.butDownload.Click += new System.EventHandler(this.ButDownload_Click);
			// 
			// listViewHugFiles
			// 
			resources.ApplyResources(this.listViewHugFiles, "listViewHugFiles");
			this.listViewHugFiles.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.columnHeader8,
            this.columnHeader10});
			this.listViewHugFiles.GridLines = true;
			this.listViewHugFiles.HideSelection = false;
			this.listViewHugFiles.Name = "listViewHugFiles";
			this.toolTip1.SetToolTip(this.listViewHugFiles, resources.GetString("listViewHugFiles.ToolTip"));
			this.listViewHugFiles.UseCompatibleStateImageBehavior = false;
			this.listViewHugFiles.View = System.Windows.Forms.View.Details;
			// 
			// columnHeader8
			// 
			resources.ApplyResources(this.columnHeader8, "columnHeader8");
			// 
			// columnHeader10
			// 
			resources.ApplyResources(this.columnHeader10, "columnHeader10");
			// 
			// tabControl1
			// 
			resources.ApplyResources(this.tabControl1, "tabControl1");
			this.tabControl1.Controls.Add(this.tabPage1);
			this.tabControl1.Controls.Add(this.tabPage2);
			this.tabControl1.Controls.Add(this.tabPage3);
			this.tabControl1.Controls.Add(this.tabPage4);
			this.tabControl1.Name = "tabControl1";
			this.tabControl1.SelectedIndex = 0;
			this.toolTip1.SetToolTip(this.tabControl1, resources.GetString("tabControl1.ToolTip"));
			// 
			// tabPage1
			// 
			resources.ApplyResources(this.tabPage1, "tabPage1");
			this.tabPage1.Controls.Add(this.splitContainer1);
			this.tabPage1.Name = "tabPage1";
			this.toolTip1.SetToolTip(this.tabPage1, resources.GetString("tabPage1.ToolTip"));
			this.tabPage1.UseVisualStyleBackColor = true;
			// 
			// tabPage2
			// 
			resources.ApplyResources(this.tabPage2, "tabPage2");
			this.tabPage2.Controls.Add(this.textModelsPath);
			this.tabPage2.Controls.Add(this.groupBox6);
			this.tabPage2.Controls.Add(this.groupBox5);
			this.tabPage2.Controls.Add(this.groupBox4);
			this.tabPage2.Controls.Add(this.groupBox3);
			this.tabPage2.Controls.Add(this.groupBox2);
			this.tabPage2.Controls.Add(this.groupBox1);
			this.tabPage2.Controls.Add(this.groupCPUParamsBatch);
			this.tabPage2.Controls.Add(this.groupCPUParams);
			this.tabPage2.Controls.Add(this.groupAdvanced);
			this.tabPage2.Controls.Add(this.groupCommon);
			this.tabPage2.Controls.Add(this.label3);
			this.tabPage2.Controls.Add(this.butBrowse);
			this.tabPage2.Controls.Add(this.butApply);
			this.tabPage2.Controls.Add(this.label1);
			this.tabPage2.Controls.Add(this.textSystemPrompt);
			this.tabPage2.Name = "tabPage2";
			this.toolTip1.SetToolTip(this.tabPage2, resources.GetString("tabPage2.ToolTip"));
			this.tabPage2.UseVisualStyleBackColor = true;
			// 
			// textModelsPath
			// 
			resources.ApplyResources(this.textModelsPath, "textModelsPath");
			this.textModelsPath.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.Suggest;
			this.textModelsPath.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.FileSystem;
			this.textModelsPath.Name = "textModelsPath";
			this.toolTip1.SetToolTip(this.textModelsPath, resources.GetString("textModelsPath.ToolTip"));
			this.textModelsPath.KeyDown += new System.Windows.Forms.KeyEventHandler(this.TextModelsPath_KeyDown);
			// 
			// groupBox6
			// 
			resources.ApplyResources(this.groupBox6, "groupBox6");
			this.groupBox6.Controls.Add(this.numVadThreshold);
			this.groupBox6.Controls.Add(this.butVADDown);
			this.groupBox6.Controls.Add(this.radioBasicVAD);
			this.groupBox6.Controls.Add(this.radioWhisperVAD);
			this.groupBox6.Controls.Add(this.label17);
			this.groupBox6.Controls.Add(this.comboVADModel);
			this.groupBox6.Controls.Add(this.label26);
			this.groupBox6.Name = "groupBox6";
			this.groupBox6.TabStop = false;
			this.toolTip1.SetToolTip(this.groupBox6, resources.GetString("groupBox6.ToolTip"));
			// 
			// numVadThreshold
			// 
			resources.ApplyResources(this.numVadThreshold, "numVadThreshold");
			this.numVadThreshold.DecimalPlaces = 2;
			this.numVadThreshold.Increment = new decimal(new int[] {
            5,
            0,
            0,
            131072});
			this.numVadThreshold.Maximum = new decimal(new int[] {
            1,
            0,
            0,
            0});
			this.numVadThreshold.Name = "numVadThreshold";
			this.toolTip1.SetToolTip(this.numVadThreshold, resources.GetString("numVadThreshold.ToolTip"));
			this.numVadThreshold.Value = new decimal(new int[] {
            5,
            0,
            0,
            65536});
			// 
			// butVADDown
			// 
			resources.ApplyResources(this.butVADDown, "butVADDown");
			this.butVADDown.Name = "butVADDown";
			this.toolTip1.SetToolTip(this.butVADDown, resources.GetString("butVADDown.ToolTip"));
			this.butVADDown.UseVisualStyleBackColor = true;
			this.butVADDown.Click += new System.EventHandler(this.butDownloadVADModel_Click);
			// 
			// radioBasicVAD
			// 
			resources.ApplyResources(this.radioBasicVAD, "radioBasicVAD");
			this.radioBasicVAD.Checked = true;
			this.radioBasicVAD.Name = "radioBasicVAD";
			this.radioBasicVAD.TabStop = true;
			this.toolTip1.SetToolTip(this.radioBasicVAD, resources.GetString("radioBasicVAD.ToolTip"));
			this.radioBasicVAD.UseVisualStyleBackColor = true;
			// 
			// radioWhisperVAD
			// 
			resources.ApplyResources(this.radioWhisperVAD, "radioWhisperVAD");
			this.radioWhisperVAD.Name = "radioWhisperVAD";
			this.toolTip1.SetToolTip(this.radioWhisperVAD, resources.GetString("radioWhisperVAD.ToolTip"));
			this.radioWhisperVAD.UseVisualStyleBackColor = true;
			// 
			// label17
			// 
			resources.ApplyResources(this.label17, "label17");
			this.label17.Name = "label17";
			this.toolTip1.SetToolTip(this.label17, resources.GetString("label17.ToolTip"));
			// 
			// comboVADModel
			// 
			resources.ApplyResources(this.comboVADModel, "comboVADModel");
			this.comboVADModel.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
			this.comboVADModel.FormattingEnabled = true;
			this.comboVADModel.Name = "comboVADModel";
			this.toolTip1.SetToolTip(this.comboVADModel, resources.GetString("comboVADModel.ToolTip"));
			this.comboVADModel.DropDown += new System.EventHandler(this.ComboVADModel_DropDown);
			// 
			// label26
			// 
			resources.ApplyResources(this.label26, "label26");
			this.label26.Name = "label26";
			this.toolTip1.SetToolTip(this.label26, resources.GetString("label26.ToolTip"));
			// 
			// groupBox5
			// 
			resources.ApplyResources(this.groupBox5, "groupBox5");
			this.groupBox5.Controls.Add(this.checkDateTimeEnable);
			this.groupBox5.Name = "groupBox5";
			this.groupBox5.TabStop = false;
			this.toolTip1.SetToolTip(this.groupBox5, resources.GetString("groupBox5.ToolTip"));
			// 
			// checkDateTimeEnable
			// 
			resources.ApplyResources(this.checkDateTimeEnable, "checkDateTimeEnable");
			this.checkDateTimeEnable.Name = "checkDateTimeEnable";
			this.toolTip1.SetToolTip(this.checkDateTimeEnable, resources.GetString("checkDateTimeEnable.ToolTip"));
			this.checkDateTimeEnable.UseVisualStyleBackColor = true;
			// 
			// groupBox4
			// 
			resources.ApplyResources(this.groupBox4, "groupBox4");
			this.groupBox4.Controls.Add(this.textFileBasePath);
			this.groupBox4.Controls.Add(this.linkFileInstruction);
			this.groupBox4.Controls.Add(this.label22);
			this.groupBox4.Controls.Add(this.checkFileWriteEnable);
			this.groupBox4.Controls.Add(this.checkFileCreateEnable);
			this.groupBox4.Controls.Add(this.checkFileReadEnable);
			this.groupBox4.Controls.Add(this.checkFileListEnable);
			this.groupBox4.Name = "groupBox4";
			this.groupBox4.TabStop = false;
			this.toolTip1.SetToolTip(this.groupBox4, resources.GetString("groupBox4.ToolTip"));
			// 
			// textFileBasePath
			// 
			resources.ApplyResources(this.textFileBasePath, "textFileBasePath");
			this.textFileBasePath.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.Suggest;
			this.textFileBasePath.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.FileSystemDirectories;
			this.textFileBasePath.Name = "textFileBasePath";
			this.toolTip1.SetToolTip(this.textFileBasePath, resources.GetString("textFileBasePath.ToolTip"));
			// 
			// linkFileInstruction
			// 
			resources.ApplyResources(this.linkFileInstruction, "linkFileInstruction");
			this.linkFileInstruction.LinkColor = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(255)))));
			this.linkFileInstruction.Name = "linkFileInstruction";
			this.linkFileInstruction.TabStop = true;
			this.toolTip1.SetToolTip(this.linkFileInstruction, resources.GetString("linkFileInstruction.ToolTip"));
			this.linkFileInstruction.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.LinkLabel1_LinkClicked);
			// 
			// label22
			// 
			resources.ApplyResources(this.label22, "label22");
			this.label22.Name = "label22";
			this.toolTip1.SetToolTip(this.label22, resources.GetString("label22.ToolTip"));
			// 
			// checkFileWriteEnable
			// 
			resources.ApplyResources(this.checkFileWriteEnable, "checkFileWriteEnable");
			this.checkFileWriteEnable.Name = "checkFileWriteEnable";
			this.toolTip1.SetToolTip(this.checkFileWriteEnable, resources.GetString("checkFileWriteEnable.ToolTip"));
			this.checkFileWriteEnable.UseVisualStyleBackColor = true;
			// 
			// checkFileCreateEnable
			// 
			resources.ApplyResources(this.checkFileCreateEnable, "checkFileCreateEnable");
			this.checkFileCreateEnable.Name = "checkFileCreateEnable";
			this.toolTip1.SetToolTip(this.checkFileCreateEnable, resources.GetString("checkFileCreateEnable.ToolTip"));
			this.checkFileCreateEnable.UseVisualStyleBackColor = true;
			// 
			// checkFileReadEnable
			// 
			resources.ApplyResources(this.checkFileReadEnable, "checkFileReadEnable");
			this.checkFileReadEnable.Name = "checkFileReadEnable";
			this.toolTip1.SetToolTip(this.checkFileReadEnable, resources.GetString("checkFileReadEnable.ToolTip"));
			this.checkFileReadEnable.UseVisualStyleBackColor = true;
			// 
			// checkFileListEnable
			// 
			resources.ApplyResources(this.checkFileListEnable, "checkFileListEnable");
			this.checkFileListEnable.Name = "checkFileListEnable";
			this.toolTip1.SetToolTip(this.checkFileListEnable, resources.GetString("checkFileListEnable.ToolTip"));
			this.checkFileListEnable.UseVisualStyleBackColor = true;
			// 
			// groupBox3
			// 
			resources.ApplyResources(this.groupBox3, "groupBox3");
			this.groupBox3.Controls.Add(this.checkWebpageFetchEnable);
			this.groupBox3.Name = "groupBox3";
			this.groupBox3.TabStop = false;
			this.toolTip1.SetToolTip(this.groupBox3, resources.GetString("groupBox3.ToolTip"));
			// 
			// checkWebpageFetchEnable
			// 
			resources.ApplyResources(this.checkWebpageFetchEnable, "checkWebpageFetchEnable");
			this.checkWebpageFetchEnable.Name = "checkWebpageFetchEnable";
			this.toolTip1.SetToolTip(this.checkWebpageFetchEnable, resources.GetString("checkWebpageFetchEnable.ToolTip"));
			this.checkWebpageFetchEnable.UseVisualStyleBackColor = true;
			// 
			// groupBox2
			// 
			resources.ApplyResources(this.groupBox2, "groupBox2");
			this.groupBox2.Controls.Add(this.numGoogleResults);
			this.groupBox2.Controls.Add(this.textGoogleSearchID);
			this.groupBox2.Controls.Add(this.textGoogleApiKey);
			this.groupBox2.Controls.Add(this.label21);
			this.groupBox2.Controls.Add(this.checkGoogleEnable);
			this.groupBox2.Controls.Add(this.label20);
			this.groupBox2.Controls.Add(this.label19);
			this.groupBox2.Name = "groupBox2";
			this.groupBox2.TabStop = false;
			this.toolTip1.SetToolTip(this.groupBox2, resources.GetString("groupBox2.ToolTip"));
			// 
			// numGoogleResults
			// 
			resources.ApplyResources(this.numGoogleResults, "numGoogleResults");
			this.numGoogleResults.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            0});
			this.numGoogleResults.Name = "numGoogleResults";
			this.toolTip1.SetToolTip(this.numGoogleResults, resources.GetString("numGoogleResults.ToolTip"));
			this.numGoogleResults.Value = new decimal(new int[] {
            5,
            0,
            0,
            0});
			// 
			// textGoogleSearchID
			// 
			resources.ApplyResources(this.textGoogleSearchID, "textGoogleSearchID");
			this.textGoogleSearchID.Name = "textGoogleSearchID";
			this.toolTip1.SetToolTip(this.textGoogleSearchID, resources.GetString("textGoogleSearchID.ToolTip"));
			// 
			// textGoogleApiKey
			// 
			resources.ApplyResources(this.textGoogleApiKey, "textGoogleApiKey");
			this.textGoogleApiKey.Name = "textGoogleApiKey";
			this.toolTip1.SetToolTip(this.textGoogleApiKey, resources.GetString("textGoogleApiKey.ToolTip"));
			// 
			// label21
			// 
			resources.ApplyResources(this.label21, "label21");
			this.label21.Name = "label21";
			this.toolTip1.SetToolTip(this.label21, resources.GetString("label21.ToolTip"));
			// 
			// checkGoogleEnable
			// 
			resources.ApplyResources(this.checkGoogleEnable, "checkGoogleEnable");
			this.checkGoogleEnable.Name = "checkGoogleEnable";
			this.toolTip1.SetToolTip(this.checkGoogleEnable, resources.GetString("checkGoogleEnable.ToolTip"));
			this.checkGoogleEnable.UseVisualStyleBackColor = true;
			// 
			// label20
			// 
			resources.ApplyResources(this.label20, "label20");
			this.label20.Name = "label20";
			this.toolTip1.SetToolTip(this.label20, resources.GetString("label20.ToolTip"));
			// 
			// label19
			// 
			resources.ApplyResources(this.label19, "label19");
			this.label19.Name = "label19";
			this.toolTip1.SetToolTip(this.label19, resources.GetString("label19.ToolTip"));
			// 
			// groupBox1
			// 
			resources.ApplyResources(this.groupBox1, "groupBox1");
			this.groupBox1.Controls.Add(this.numWakeWordSimilarity);
			this.groupBox1.Controls.Add(this.numWhisperTemp);
			this.groupBox1.Controls.Add(this.numFreqThreshold);
			this.groupBox1.Controls.Add(this.textWakeWord);
			this.groupBox1.Controls.Add(this.label25);
			this.groupBox1.Controls.Add(this.label24);
			this.groupBox1.Controls.Add(this.label18);
			this.groupBox1.Controls.Add(this.checkWhisperUseGPU);
			this.groupBox1.Controls.Add(this.label16);
			this.groupBox1.Controls.Add(this.butWhispDown);
			this.groupBox1.Controls.Add(this.comboWhisperModel);
			this.groupBox1.Controls.Add(this.label15);
			this.groupBox1.Name = "groupBox1";
			this.groupBox1.TabStop = false;
			this.toolTip1.SetToolTip(this.groupBox1, resources.GetString("groupBox1.ToolTip"));
			// 
			// numWakeWordSimilarity
			// 
			resources.ApplyResources(this.numWakeWordSimilarity, "numWakeWordSimilarity");
			this.numWakeWordSimilarity.DecimalPlaces = 1;
			this.numWakeWordSimilarity.Increment = new decimal(new int[] {
            1,
            0,
            0,
            65536});
			this.numWakeWordSimilarity.Maximum = new decimal(new int[] {
            1,
            0,
            0,
            0});
			this.numWakeWordSimilarity.Name = "numWakeWordSimilarity";
			this.toolTip1.SetToolTip(this.numWakeWordSimilarity, resources.GetString("numWakeWordSimilarity.ToolTip"));
			this.numWakeWordSimilarity.Value = new decimal(new int[] {
            8,
            0,
            0,
            65536});
			// 
			// numWhisperTemp
			// 
			resources.ApplyResources(this.numWhisperTemp, "numWhisperTemp");
			this.numWhisperTemp.DecimalPlaces = 1;
			this.numWhisperTemp.Increment = new decimal(new int[] {
            1,
            0,
            0,
            65536});
			this.numWhisperTemp.Maximum = new decimal(new int[] {
            10,
            0,
            0,
            0});
			this.numWhisperTemp.Name = "numWhisperTemp";
			this.toolTip1.SetToolTip(this.numWhisperTemp, resources.GetString("numWhisperTemp.ToolTip"));
			this.numWhisperTemp.Value = new decimal(new int[] {
            2,
            0,
            0,
            65536});
			// 
			// numFreqThreshold
			// 
			resources.ApplyResources(this.numFreqThreshold, "numFreqThreshold");
			this.numFreqThreshold.Increment = new decimal(new int[] {
            10,
            0,
            0,
            0});
			this.numFreqThreshold.Maximum = new decimal(new int[] {
            300,
            0,
            0,
            0});
			this.numFreqThreshold.Name = "numFreqThreshold";
			this.toolTip1.SetToolTip(this.numFreqThreshold, resources.GetString("numFreqThreshold.ToolTip"));
			this.numFreqThreshold.Value = new decimal(new int[] {
            100,
            0,
            0,
            0});
			// 
			// textWakeWord
			// 
			resources.ApplyResources(this.textWakeWord, "textWakeWord");
			this.textWakeWord.Name = "textWakeWord";
			this.toolTip1.SetToolTip(this.textWakeWord, resources.GetString("textWakeWord.ToolTip"));
			// 
			// label25
			// 
			resources.ApplyResources(this.label25, "label25");
			this.label25.Name = "label25";
			this.toolTip1.SetToolTip(this.label25, resources.GetString("label25.ToolTip"));
			// 
			// label24
			// 
			resources.ApplyResources(this.label24, "label24");
			this.label24.Name = "label24";
			this.toolTip1.SetToolTip(this.label24, resources.GetString("label24.ToolTip"));
			// 
			// label18
			// 
			resources.ApplyResources(this.label18, "label18");
			this.label18.Name = "label18";
			this.toolTip1.SetToolTip(this.label18, resources.GetString("label18.ToolTip"));
			// 
			// checkWhisperUseGPU
			// 
			resources.ApplyResources(this.checkWhisperUseGPU, "checkWhisperUseGPU");
			this.checkWhisperUseGPU.Name = "checkWhisperUseGPU";
			this.toolTip1.SetToolTip(this.checkWhisperUseGPU, resources.GetString("checkWhisperUseGPU.ToolTip"));
			this.checkWhisperUseGPU.UseVisualStyleBackColor = true;
			// 
			// label16
			// 
			resources.ApplyResources(this.label16, "label16");
			this.label16.Name = "label16";
			this.toolTip1.SetToolTip(this.label16, resources.GetString("label16.ToolTip"));
			// 
			// butWhispDown
			// 
			resources.ApplyResources(this.butWhispDown, "butWhispDown");
			this.butWhispDown.Name = "butWhispDown";
			this.toolTip1.SetToolTip(this.butWhispDown, resources.GetString("butWhispDown.ToolTip"));
			this.butWhispDown.UseVisualStyleBackColor = true;
			this.butWhispDown.Click += new System.EventHandler(this.ButWhispDown_Click);
			// 
			// comboWhisperModel
			// 
			resources.ApplyResources(this.comboWhisperModel, "comboWhisperModel");
			this.comboWhisperModel.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
			this.comboWhisperModel.FormattingEnabled = true;
			this.comboWhisperModel.Name = "comboWhisperModel";
			this.toolTip1.SetToolTip(this.comboWhisperModel, resources.GetString("comboWhisperModel.ToolTip"));
			this.comboWhisperModel.DropDown += new System.EventHandler(this.ComboWhisperModel_DropDown);
			// 
			// label15
			// 
			resources.ApplyResources(this.label15, "label15");
			this.label15.Name = "label15";
			this.toolTip1.SetToolTip(this.label15, resources.GetString("label15.ToolTip"));
			// 
			// groupCPUParamsBatch
			// 
			resources.ApplyResources(this.groupCPUParamsBatch, "groupCPUParamsBatch");
			this.groupCPUParamsBatch.Controls.Add(this.numThreadsBatch);
			this.groupCPUParamsBatch.Controls.Add(this.label14);
			this.groupCPUParamsBatch.Name = "groupCPUParamsBatch";
			this.groupCPUParamsBatch.TabStop = false;
			this.toolTip1.SetToolTip(this.groupCPUParamsBatch, resources.GetString("groupCPUParamsBatch.ToolTip"));
			// 
			// numThreadsBatch
			// 
			resources.ApplyResources(this.numThreadsBatch, "numThreadsBatch");
			this.numThreadsBatch.Maximum = new decimal(new int[] {
            512,
            0,
            0,
            0});
			this.numThreadsBatch.Name = "numThreadsBatch";
			this.toolTip1.SetToolTip(this.numThreadsBatch, resources.GetString("numThreadsBatch.ToolTip"));
			this.numThreadsBatch.Value = new decimal(new int[] {
            8,
            0,
            0,
            0});
			// 
			// label14
			// 
			resources.ApplyResources(this.label14, "label14");
			this.label14.Name = "label14";
			this.toolTip1.SetToolTip(this.label14, resources.GetString("label14.ToolTip"));
			// 
			// groupCPUParams
			// 
			resources.ApplyResources(this.groupCPUParams, "groupCPUParams");
			this.groupCPUParams.Controls.Add(this.numThreads);
			this.groupCPUParams.Controls.Add(this.label2);
			this.groupCPUParams.Name = "groupCPUParams";
			this.groupCPUParams.TabStop = false;
			this.toolTip1.SetToolTip(this.groupCPUParams, resources.GetString("groupCPUParams.ToolTip"));
			// 
			// numThreads
			// 
			resources.ApplyResources(this.numThreads, "numThreads");
			this.numThreads.Maximum = new decimal(new int[] {
            512,
            0,
            0,
            0});
			this.numThreads.Name = "numThreads";
			this.toolTip1.SetToolTip(this.numThreads, resources.GetString("numThreads.ToolTip"));
			this.numThreads.Value = new decimal(new int[] {
            8,
            0,
            0,
            0});
			// 
			// label2
			// 
			resources.ApplyResources(this.label2, "label2");
			this.label2.Name = "label2";
			this.toolTip1.SetToolTip(this.label2, resources.GetString("label2.ToolTip"));
			// 
			// groupAdvanced
			// 
			resources.ApplyResources(this.groupAdvanced, "groupAdvanced");
			this.groupAdvanced.Controls.Add(this.numMinP);
			this.groupAdvanced.Controls.Add(this.comboNUMAStrat);
			this.groupAdvanced.Controls.Add(this.numRepPen);
			this.groupAdvanced.Controls.Add(this.numBatchSize);
			this.groupAdvanced.Controls.Add(this.numTopK);
			this.groupAdvanced.Controls.Add(this.numTopP);
			this.groupAdvanced.Controls.Add(this.label23);
			this.groupAdvanced.Controls.Add(this.checkFlashAttn);
			this.groupAdvanced.Controls.Add(this.checkMLock);
			this.groupAdvanced.Controls.Add(this.checkMMap);
			this.groupAdvanced.Controls.Add(this.label6);
			this.groupAdvanced.Controls.Add(this.label12);
			this.groupAdvanced.Controls.Add(this.label11);
			this.groupAdvanced.Controls.Add(this.label8);
			this.groupAdvanced.Controls.Add(this.label9);
			this.groupAdvanced.Name = "groupAdvanced";
			this.groupAdvanced.TabStop = false;
			this.toolTip1.SetToolTip(this.groupAdvanced, resources.GetString("groupAdvanced.ToolTip"));
			// 
			// numMinP
			// 
			resources.ApplyResources(this.numMinP, "numMinP");
			this.numMinP.DecimalPlaces = 2;
			this.numMinP.Increment = new decimal(new int[] {
            1,
            0,
            0,
            131072});
			this.numMinP.Maximum = new decimal(new int[] {
            1,
            0,
            0,
            0});
			this.numMinP.Name = "numMinP";
			this.toolTip1.SetToolTip(this.numMinP, resources.GetString("numMinP.ToolTip"));
			// 
			// comboNUMAStrat
			// 
			resources.ApplyResources(this.comboNUMAStrat, "comboNUMAStrat");
			this.comboNUMAStrat.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
			this.comboNUMAStrat.FormattingEnabled = true;
			this.comboNUMAStrat.Items.AddRange(new object[] {
            resources.GetString("comboNUMAStrat.Items"),
            resources.GetString("comboNUMAStrat.Items1"),
            resources.GetString("comboNUMAStrat.Items2"),
            resources.GetString("comboNUMAStrat.Items3"),
            resources.GetString("comboNUMAStrat.Items4"),
            resources.GetString("comboNUMAStrat.Items5")});
			this.comboNUMAStrat.Name = "comboNUMAStrat";
			this.toolTip1.SetToolTip(this.comboNUMAStrat, resources.GetString("comboNUMAStrat.ToolTip"));
			// 
			// numRepPen
			// 
			resources.ApplyResources(this.numRepPen, "numRepPen");
			this.numRepPen.DecimalPlaces = 2;
			this.numRepPen.Increment = new decimal(new int[] {
            1,
            0,
            0,
            131072});
			this.numRepPen.Maximum = new decimal(new int[] {
            10,
            0,
            0,
            0});
			this.numRepPen.Name = "numRepPen";
			this.toolTip1.SetToolTip(this.numRepPen, resources.GetString("numRepPen.ToolTip"));
			this.numRepPen.Value = new decimal(new int[] {
            11,
            0,
            0,
            65536});
			// 
			// numBatchSize
			// 
			resources.ApplyResources(this.numBatchSize, "numBatchSize");
			this.numBatchSize.Maximum = new decimal(new int[] {
            1048576,
            0,
            0,
            0});
			this.numBatchSize.Name = "numBatchSize";
			this.toolTip1.SetToolTip(this.numBatchSize, resources.GetString("numBatchSize.ToolTip"));
			this.numBatchSize.Value = new decimal(new int[] {
            512,
            0,
            0,
            0});
			// 
			// numTopK
			// 
			resources.ApplyResources(this.numTopK, "numTopK");
			this.numTopK.Maximum = new decimal(new int[] {
            100000,
            0,
            0,
            0});
			this.numTopK.Name = "numTopK";
			this.toolTip1.SetToolTip(this.numTopK, resources.GetString("numTopK.ToolTip"));
			this.numTopK.Value = new decimal(new int[] {
            40,
            0,
            0,
            0});
			// 
			// numTopP
			// 
			resources.ApplyResources(this.numTopP, "numTopP");
			this.numTopP.DecimalPlaces = 2;
			this.numTopP.Increment = new decimal(new int[] {
            1,
            0,
            0,
            131072});
			this.numTopP.Maximum = new decimal(new int[] {
            1,
            0,
            0,
            0});
			this.numTopP.Name = "numTopP";
			this.toolTip1.SetToolTip(this.numTopP, resources.GetString("numTopP.ToolTip"));
			this.numTopP.Value = new decimal(new int[] {
            95,
            0,
            0,
            131072});
			// 
			// label23
			// 
			resources.ApplyResources(this.label23, "label23");
			this.label23.Name = "label23";
			this.toolTip1.SetToolTip(this.label23, resources.GetString("label23.ToolTip"));
			// 
			// checkFlashAttn
			// 
			resources.ApplyResources(this.checkFlashAttn, "checkFlashAttn");
			this.checkFlashAttn.Checked = true;
			this.checkFlashAttn.CheckState = System.Windows.Forms.CheckState.Checked;
			this.checkFlashAttn.Name = "checkFlashAttn";
			this.toolTip1.SetToolTip(this.checkFlashAttn, resources.GetString("checkFlashAttn.ToolTip"));
			this.checkFlashAttn.UseVisualStyleBackColor = true;
			// 
			// checkMLock
			// 
			resources.ApplyResources(this.checkMLock, "checkMLock");
			this.checkMLock.Name = "checkMLock";
			this.toolTip1.SetToolTip(this.checkMLock, resources.GetString("checkMLock.ToolTip"));
			this.checkMLock.UseVisualStyleBackColor = true;
			// 
			// checkMMap
			// 
			resources.ApplyResources(this.checkMMap, "checkMMap");
			this.checkMMap.Name = "checkMMap";
			this.toolTip1.SetToolTip(this.checkMMap, resources.GetString("checkMMap.ToolTip"));
			this.checkMMap.UseVisualStyleBackColor = true;
			// 
			// label6
			// 
			resources.ApplyResources(this.label6, "label6");
			this.label6.Name = "label6";
			this.toolTip1.SetToolTip(this.label6, resources.GetString("label6.ToolTip"));
			// 
			// label12
			// 
			resources.ApplyResources(this.label12, "label12");
			this.label12.Name = "label12";
			this.toolTip1.SetToolTip(this.label12, resources.GetString("label12.ToolTip"));
			// 
			// label11
			// 
			resources.ApplyResources(this.label11, "label11");
			this.label11.Name = "label11";
			this.toolTip1.SetToolTip(this.label11, resources.GetString("label11.ToolTip"));
			// 
			// label8
			// 
			resources.ApplyResources(this.label8, "label8");
			this.label8.Name = "label8";
			this.toolTip1.SetToolTip(this.label8, resources.GetString("label8.ToolTip"));
			// 
			// label9
			// 
			resources.ApplyResources(this.label9, "label9");
			this.label9.Name = "label9";
			this.toolTip1.SetToolTip(this.label9, resources.GetString("label9.ToolTip"));
			// 
			// groupCommon
			// 
			resources.ApplyResources(this.groupCommon, "groupCommon");
			this.groupCommon.Controls.Add(this.numCtxSize);
			this.groupCommon.Controls.Add(this.numGPULayers);
			this.groupCommon.Controls.Add(this.numNGen);
			this.groupCommon.Controls.Add(this.numTemp);
			this.groupCommon.Controls.Add(this.label5);
			this.groupCommon.Controls.Add(this.label7);
			this.groupCommon.Controls.Add(this.label10);
			this.groupCommon.Controls.Add(this.label4);
			this.groupCommon.Name = "groupCommon";
			this.groupCommon.TabStop = false;
			this.toolTip1.SetToolTip(this.groupCommon, resources.GetString("groupCommon.ToolTip"));
			// 
			// numCtxSize
			// 
			resources.ApplyResources(this.numCtxSize, "numCtxSize");
			this.numCtxSize.Maximum = new decimal(new int[] {
            1048576,
            0,
            0,
            0});
			this.numCtxSize.Name = "numCtxSize";
			this.toolTip1.SetToolTip(this.numCtxSize, resources.GetString("numCtxSize.ToolTip"));
			this.numCtxSize.Value = new decimal(new int[] {
            8192,
            0,
            0,
            0});
			// 
			// numGPULayers
			// 
			resources.ApplyResources(this.numGPULayers, "numGPULayers");
			this.numGPULayers.Maximum = new decimal(new int[] {
            1024,
            0,
            0,
            0});
			this.numGPULayers.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            -2147483648});
			this.numGPULayers.Name = "numGPULayers";
			this.toolTip1.SetToolTip(this.numGPULayers, resources.GetString("numGPULayers.ToolTip"));
			this.numGPULayers.Value = new decimal(new int[] {
            1,
            0,
            0,
            -2147483648});
			// 
			// numNGen
			// 
			resources.ApplyResources(this.numNGen, "numNGen");
			this.numNGen.Maximum = new decimal(new int[] {
            131072,
            0,
            0,
            0});
			this.numNGen.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            -2147483648});
			this.numNGen.Name = "numNGen";
			this.toolTip1.SetToolTip(this.numNGen, resources.GetString("numNGen.ToolTip"));
			this.numNGen.Value = new decimal(new int[] {
            1,
            0,
            0,
            -2147483648});
			// 
			// numTemp
			// 
			resources.ApplyResources(this.numTemp, "numTemp");
			this.numTemp.DecimalPlaces = 2;
			this.numTemp.Increment = new decimal(new int[] {
            1,
            0,
            0,
            65536});
			this.numTemp.Maximum = new decimal(new int[] {
            1,
            0,
            0,
            0});
			this.numTemp.Name = "numTemp";
			this.toolTip1.SetToolTip(this.numTemp, resources.GetString("numTemp.ToolTip"));
			this.numTemp.Value = new decimal(new int[] {
            6,
            0,
            0,
            65536});
			// 
			// label5
			// 
			resources.ApplyResources(this.label5, "label5");
			this.label5.Name = "label5";
			this.toolTip1.SetToolTip(this.label5, resources.GetString("label5.ToolTip"));
			// 
			// label7
			// 
			resources.ApplyResources(this.label7, "label7");
			this.label7.Name = "label7";
			this.toolTip1.SetToolTip(this.label7, resources.GetString("label7.ToolTip"));
			// 
			// label10
			// 
			resources.ApplyResources(this.label10, "label10");
			this.label10.Name = "label10";
			this.toolTip1.SetToolTip(this.label10, resources.GetString("label10.ToolTip"));
			// 
			// label4
			// 
			resources.ApplyResources(this.label4, "label4");
			this.label4.Name = "label4";
			this.toolTip1.SetToolTip(this.label4, resources.GetString("label4.ToolTip"));
			// 
			// label3
			// 
			resources.ApplyResources(this.label3, "label3");
			this.label3.Name = "label3";
			this.toolTip1.SetToolTip(this.label3, resources.GetString("label3.ToolTip"));
			// 
			// butBrowse
			// 
			resources.ApplyResources(this.butBrowse, "butBrowse");
			this.butBrowse.Name = "butBrowse";
			this.toolTip1.SetToolTip(this.butBrowse, resources.GetString("butBrowse.ToolTip"));
			this.butBrowse.UseVisualStyleBackColor = true;
			this.butBrowse.Click += new System.EventHandler(this.ButBrowse_Click);
			// 
			// butApply
			// 
			resources.ApplyResources(this.butApply, "butApply");
			this.butApply.Name = "butApply";
			this.toolTip1.SetToolTip(this.butApply, resources.GetString("butApply.ToolTip"));
			this.butApply.UseVisualStyleBackColor = true;
			this.butApply.Click += new System.EventHandler(this.ButApply_Click);
			// 
			// label1
			// 
			resources.ApplyResources(this.label1, "label1");
			this.label1.Name = "label1";
			this.toolTip1.SetToolTip(this.label1, resources.GetString("label1.ToolTip"));
			// 
			// textSystemPrompt
			// 
			resources.ApplyResources(this.textSystemPrompt, "textSystemPrompt");
			this.textSystemPrompt.Name = "textSystemPrompt";
			this.toolTip1.SetToolTip(this.textSystemPrompt, resources.GetString("textSystemPrompt.ToolTip"));
			// 
			// tabPage3
			// 
			resources.ApplyResources(this.tabPage3, "tabPage3");
			this.tabPage3.Controls.Add(this.splitContainer2);
			this.tabPage3.Controls.Add(this.checkLoadAuto);
			this.tabPage3.Controls.Add(this.butUnload);
			this.tabPage3.Controls.Add(this.butLoad);
			this.tabPage3.Name = "tabPage3";
			this.toolTip1.SetToolTip(this.tabPage3, resources.GetString("tabPage3.ToolTip"));
			this.tabPage3.UseVisualStyleBackColor = true;
			// 
			// checkLoadAuto
			// 
			resources.ApplyResources(this.checkLoadAuto, "checkLoadAuto");
			this.checkLoadAuto.Name = "checkLoadAuto";
			this.toolTip1.SetToolTip(this.checkLoadAuto, resources.GetString("checkLoadAuto.ToolTip"));
			this.checkLoadAuto.UseVisualStyleBackColor = true;
			this.checkLoadAuto.CheckedChanged += new System.EventHandler(this.CheckLoadAuto_CheckedChanged);
			// 
			// butUnload
			// 
			resources.ApplyResources(this.butUnload, "butUnload");
			this.butUnload.Name = "butUnload";
			this.toolTip1.SetToolTip(this.butUnload, resources.GetString("butUnload.ToolTip"));
			this.butUnload.UseVisualStyleBackColor = true;
			this.butUnload.Click += new System.EventHandler(this.ButUnload_Click);
			// 
			// butLoad
			// 
			resources.ApplyResources(this.butLoad, "butLoad");
			this.butLoad.Name = "butLoad";
			this.toolTip1.SetToolTip(this.butLoad, resources.GetString("butLoad.ToolTip"));
			this.butLoad.UseVisualStyleBackColor = true;
			this.butLoad.Click += new System.EventHandler(this.ButLoad_Click);
			// 
			// tabPage4
			// 
			resources.ApplyResources(this.tabPage4, "tabPage4");
			this.tabPage4.Controls.Add(this.splitContainer3);
			this.tabPage4.Name = "tabPage4";
			this.toolTip1.SetToolTip(this.tabPage4, resources.GetString("tabPage4.ToolTip"));
			this.tabPage4.UseVisualStyleBackColor = true;
			// 
			// folderBrowserDialog1
			// 
			resources.ApplyResources(this.folderBrowserDialog1, "folderBrowserDialog1");
			// 
			// statusStrip1
			// 
			resources.ApplyResources(this.statusStrip1, "statusStrip1");
			this.statusStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripStatusLabel1,
            this.labelTokens,
            this.labelTPS,
            this.labelPreGen});
			this.statusStrip1.LayoutStyle = System.Windows.Forms.ToolStripLayoutStyle.HorizontalStackWithOverflow;
			this.statusStrip1.Name = "statusStrip1";
			this.toolTip1.SetToolTip(this.statusStrip1, resources.GetString("statusStrip1.ToolTip"));
			// 
			// toolStripStatusLabel1
			// 
			resources.ApplyResources(this.toolStripStatusLabel1, "toolStripStatusLabel1");
			this.toolStripStatusLabel1.Name = "toolStripStatusLabel1";
			// 
			// labelTokens
			// 
			resources.ApplyResources(this.labelTokens, "labelTokens");
			this.labelTokens.BorderSides = System.Windows.Forms.ToolStripStatusLabelBorderSides.Left;
			this.labelTokens.BorderStyle = System.Windows.Forms.Border3DStyle.Etched;
			this.labelTokens.Name = "labelTokens";
			// 
			// labelTPS
			// 
			resources.ApplyResources(this.labelTPS, "labelTPS");
			this.labelTPS.BorderSides = System.Windows.Forms.ToolStripStatusLabelBorderSides.Left;
			this.labelTPS.BorderStyle = System.Windows.Forms.Border3DStyle.Etched;
			this.labelTPS.Name = "labelTPS";
			// 
			// labelPreGen
			// 
			resources.ApplyResources(this.labelPreGen, "labelPreGen");
			this.labelPreGen.BorderSides = System.Windows.Forms.ToolStripStatusLabelBorderSides.Left;
			this.labelPreGen.BorderStyle = System.Windows.Forms.Border3DStyle.Etched;
			this.labelPreGen.Name = "labelPreGen";
			// 
			// toolTip1
			// 
			this.toolTip1.AutoPopDelay = 30000;
			this.toolTip1.InitialDelay = 500;
			this.toolTip1.ReshowDelay = 0;
			this.toolTip1.UseAnimation = false;
			this.toolTip1.UseFading = false;
			// 
			// Form1
			// 
			resources.ApplyResources(this, "$this");
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
			this.AutoValidate = System.Windows.Forms.AutoValidate.Disable;
			this.Controls.Add(this.statusStrip1);
			this.Controls.Add(this.tabControl1);
			this.DoubleBuffered = true;
			this.KeyPreview = true;
			this.Name = "Form1";
			this.toolTip1.SetToolTip(this, resources.GetString("$this.ToolTip"));
			this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.Form1_FormClosing);
			this.Load += new System.EventHandler(this.Form1_Load);
			this.KeyDown += new System.Windows.Forms.KeyEventHandler(this.Form1_KeyDown);
			this.splitContainer1.Panel1.ResumeLayout(false);
			this.splitContainer1.Panel2.ResumeLayout(false);
			this.splitContainer1.Panel2.PerformLayout();
			((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
			this.splitContainer1.ResumeLayout(false);
			this.splitContainer2.Panel1.ResumeLayout(false);
			this.splitContainer2.Panel2.ResumeLayout(false);
			((System.ComponentModel.ISupportInitialize)(this.splitContainer2)).EndInit();
			this.splitContainer2.ResumeLayout(false);
			this.splitContainer3.Panel1.ResumeLayout(false);
			this.splitContainer3.Panel1.PerformLayout();
			this.splitContainer3.Panel2.ResumeLayout(false);
			((System.ComponentModel.ISupportInitialize)(this.splitContainer3)).EndInit();
			this.splitContainer3.ResumeLayout(false);
			this.tabControl1.ResumeLayout(false);
			this.tabPage1.ResumeLayout(false);
			this.tabPage2.ResumeLayout(false);
			this.tabPage2.PerformLayout();
			this.groupBox6.ResumeLayout(false);
			this.groupBox6.PerformLayout();
			((System.ComponentModel.ISupportInitialize)(this.numVadThreshold)).EndInit();
			this.groupBox5.ResumeLayout(false);
			this.groupBox5.PerformLayout();
			this.groupBox4.ResumeLayout(false);
			this.groupBox4.PerformLayout();
			this.groupBox3.ResumeLayout(false);
			this.groupBox3.PerformLayout();
			this.groupBox2.ResumeLayout(false);
			this.groupBox2.PerformLayout();
			((System.ComponentModel.ISupportInitialize)(this.numGoogleResults)).EndInit();
			this.groupBox1.ResumeLayout(false);
			this.groupBox1.PerformLayout();
			((System.ComponentModel.ISupportInitialize)(this.numWakeWordSimilarity)).EndInit();
			((System.ComponentModel.ISupportInitialize)(this.numWhisperTemp)).EndInit();
			((System.ComponentModel.ISupportInitialize)(this.numFreqThreshold)).EndInit();
			this.groupCPUParamsBatch.ResumeLayout(false);
			this.groupCPUParamsBatch.PerformLayout();
			((System.ComponentModel.ISupportInitialize)(this.numThreadsBatch)).EndInit();
			this.groupCPUParams.ResumeLayout(false);
			this.groupCPUParams.PerformLayout();
			((System.ComponentModel.ISupportInitialize)(this.numThreads)).EndInit();
			this.groupAdvanced.ResumeLayout(false);
			this.groupAdvanced.PerformLayout();
			((System.ComponentModel.ISupportInitialize)(this.numMinP)).EndInit();
			((System.ComponentModel.ISupportInitialize)(this.numRepPen)).EndInit();
			((System.ComponentModel.ISupportInitialize)(this.numBatchSize)).EndInit();
			((System.ComponentModel.ISupportInitialize)(this.numTopK)).EndInit();
			((System.ComponentModel.ISupportInitialize)(this.numTopP)).EndInit();
			this.groupCommon.ResumeLayout(false);
			this.groupCommon.PerformLayout();
			((System.ComponentModel.ISupportInitialize)(this.numCtxSize)).EndInit();
			((System.ComponentModel.ISupportInitialize)(this.numGPULayers)).EndInit();
			((System.ComponentModel.ISupportInitialize)(this.numNGen)).EndInit();
			((System.ComponentModel.ISupportInitialize)(this.numTemp)).EndInit();
			this.tabPage3.ResumeLayout(false);
			this.tabPage3.PerformLayout();
			this.tabPage4.ResumeLayout(false);
			this.statusStrip1.ResumeLayout(false);
			this.statusStrip1.PerformLayout();
			this.ResumeLayout(false);
			this.PerformLayout();

		}

		#endregion

		private System.Windows.Forms.TextBox textInput;
		private System.Windows.Forms.SplitContainer splitContainer1;
		private System.Windows.Forms.Button butGen;
		private System.Windows.Forms.TabControl tabControl1;
		private System.Windows.Forms.TabPage tabPage1;
		private System.Windows.Forms.TabPage tabPage2;
		private System.Windows.Forms.NumericUpDown numThreads;
		private System.Windows.Forms.Label label2;
		private System.Windows.Forms.Label label1;
		private System.Windows.Forms.TextBox textSystemPrompt;
		private System.Windows.Forms.Button butApply;
		private System.Windows.Forms.Button butBrowse;
		private System.Windows.Forms.TextBox textModelsPath;
		private System.Windows.Forms.Button butReset;
		private System.Windows.Forms.NumericUpDown numRepPen;
		private System.Windows.Forms.Label label6;
		private System.Windows.Forms.NumericUpDown numTemp;
		private System.Windows.Forms.Label label7;
		private System.Windows.Forms.NumericUpDown numTopP;
		private System.Windows.Forms.Label label9;
		private System.Windows.Forms.NumericUpDown numTopK;
		private System.Windows.Forms.Label label8;
		private System.Windows.Forms.Label label3;
		private System.Windows.Forms.TabPage tabPage3;
		private System.Windows.Forms.ListView listViewModels;
		private System.Windows.Forms.ColumnHeader columnHeader1;
		private System.Windows.Forms.FolderBrowserDialog folderBrowserDialog1;
		private System.Windows.Forms.Button butLoad;
		private System.Windows.Forms.ColumnHeader columnHeader2;
		private System.Windows.Forms.NumericUpDown numNGen;
		private System.Windows.Forms.Label label4;
		private System.Windows.Forms.Label label5;
		private System.Windows.Forms.NumericUpDown numCtxSize;
		private System.Windows.Forms.StatusStrip statusStrip1;
		private System.Windows.Forms.ToolStripStatusLabel toolStripStatusLabel1;
		private System.Windows.Forms.Label label10;
		private System.Windows.Forms.NumericUpDown numGPULayers;
		private System.Windows.Forms.Label label11;
		private System.Windows.Forms.NumericUpDown numBatchSize;
		private System.Windows.Forms.Label label12;
		private System.Windows.Forms.ComboBox comboNUMAStrat;
		private System.Windows.Forms.Button butUnload;
		private System.Windows.Forms.ToolStripStatusLabel labelTokens;
		private System.Windows.Forms.CheckBox checkMarkdown;
		internal MyFlowLayoutPanel panelChat;
		private System.Windows.Forms.CheckBox checkLoadAuto;
		private System.Windows.Forms.SplitContainer splitContainer2;
		private System.Windows.Forms.ListView listViewMeta;
		private System.Windows.Forms.ColumnHeader columnHeader3;
		private System.Windows.Forms.ColumnHeader columnHeader4;
		private System.Windows.Forms.GroupBox groupAdvanced;
		private System.Windows.Forms.GroupBox groupCommon;
		private System.Windows.Forms.ToolStripStatusLabel labelTPS;
		private System.Windows.Forms.Button butCodeBlock;
		private System.Windows.Forms.TabPage tabPage4;
		private System.Windows.Forms.SplitContainer splitContainer3;
		private System.Windows.Forms.Button butSearch;
		private System.Windows.Forms.TextBox textSearchTerm;
		private System.Windows.Forms.Label label13;
		private System.Windows.Forms.ListView listViewHugSearch;
		private System.Windows.Forms.ListView listViewHugFiles;
		private System.Windows.Forms.ColumnHeader columnHeader5;
		private System.Windows.Forms.ColumnHeader columnHeader6;
		private System.Windows.Forms.ColumnHeader columnHeader7;
		private System.Windows.Forms.ColumnHeader columnHeader8;
		private System.Windows.Forms.ColumnHeader columnHeader10;
		private System.Windows.Forms.Button butDownload;
		private System.Windows.Forms.ColumnHeader columnHeader11;
		private System.Windows.Forms.ColumnHeader columnHeader12;
		private System.Windows.Forms.ColumnHeader columnHeader13;
		private System.Windows.Forms.ColumnHeader columnHeader14;
		private System.Windows.Forms.ProgressBar progressBar1;
		private System.Windows.Forms.GroupBox groupCPUParamsBatch;
		private System.Windows.Forms.NumericUpDown numThreadsBatch;
		private System.Windows.Forms.Label label14;
		private System.Windows.Forms.GroupBox groupCPUParams;
		private System.Windows.Forms.ToolTip toolTip1;
		private System.Windows.Forms.ToolStripStatusLabel labelPreGen;
		private System.Windows.Forms.CheckBox checkMLock;
		private System.Windows.Forms.CheckBox checkMMap;
		private System.Windows.Forms.GroupBox groupBox1;
		private System.Windows.Forms.ComboBox comboWhisperModel;
		private System.Windows.Forms.Label label15;
		private System.Windows.Forms.Button butWhispDown;
		private System.Windows.Forms.Label label16;
		private System.Windows.Forms.TextBox textWakeWord;
		private System.Windows.Forms.CheckBox checkVoiceInput;
		private System.Windows.Forms.CheckBox checkWhisperUseGPU;
		private System.Windows.Forms.Label label18;
		private System.Windows.Forms.NumericUpDown numFreqThreshold;
		private System.Windows.Forms.Label label17;
		private System.Windows.Forms.NumericUpDown numVadThreshold;
		private System.Windows.Forms.CheckBox checkSpeak;
		private System.Windows.Forms.CheckBox checkFlashAttn;
		private System.Windows.Forms.CheckBox checkStream;
		private System.Windows.Forms.GroupBox groupBox2;
		private System.Windows.Forms.Label label20;
		private System.Windows.Forms.Label label19;
		private System.Windows.Forms.TextBox textGoogleSearchID;
		private System.Windows.Forms.TextBox textGoogleApiKey;
		private System.Windows.Forms.CheckBox checkGoogleEnable;
		private System.Windows.Forms.GroupBox groupBox3;
		private System.Windows.Forms.CheckBox checkWebpageFetchEnable;
		private System.Windows.Forms.Label label21;
		private System.Windows.Forms.NumericUpDown numGoogleResults;
		private System.Windows.Forms.GroupBox groupBox4;
		private System.Windows.Forms.CheckBox checkFileListEnable;
		private System.Windows.Forms.CheckBox checkFileWriteEnable;
		private System.Windows.Forms.CheckBox checkFileCreateEnable;
		private System.Windows.Forms.CheckBox checkFileReadEnable;
		private System.Windows.Forms.Label label22;
		private System.Windows.Forms.TextBox textFileBasePath;
		private System.Windows.Forms.LinkLabel linkFileInstruction;
		private System.Windows.Forms.Label label23;
		private System.Windows.Forms.NumericUpDown numMinP;
		private System.Windows.Forms.Label label24;
		private System.Windows.Forms.NumericUpDown numWhisperTemp;
		private System.Windows.Forms.Label label25;
		private System.Windows.Forms.NumericUpDown numWakeWordSimilarity;
		private System.Windows.Forms.GroupBox groupBox5;
		private System.Windows.Forms.CheckBox checkDateTimeEnable;
		private System.Windows.Forms.GroupBox groupBox6;
		private System.Windows.Forms.Button butVADDown;
		private System.Windows.Forms.RadioButton radioBasicVAD;
		private System.Windows.Forms.RadioButton radioWhisperVAD;
		private System.Windows.Forms.ComboBox comboVADModel;
		private System.Windows.Forms.Label label26;
	}
}

