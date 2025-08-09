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
			this.textInput = new System.Windows.Forms.TextBox();
			this.splitContainer2 = new System.Windows.Forms.SplitContainer();
			this.listViewModels = new System.Windows.Forms.ListView();
			this.columnHeader1 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
			this.columnHeader2 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
			this.tabControlModelStuff = new System.Windows.Forms.TabControl();
			this.tabPageModelSettings = new System.Windows.Forms.TabPage();
			this.labelSystemPromptModel = new System.Windows.Forms.Label();
			this.textSystemPromptModel = new System.Windows.Forms.TextBox();
			this.butApplyModelSettings = new System.Windows.Forms.Button();
			this.checkUseModelSettings = new System.Windows.Forms.CheckBox();
			this.groupAdvancedModel = new System.Windows.Forms.GroupBox();
			this.numMinPModel = new System.Windows.Forms.NumericUpDown();
			this.numTopKModel = new System.Windows.Forms.NumericUpDown();
			this.numTopPModel = new System.Windows.Forms.NumericUpDown();
			this.label27 = new System.Windows.Forms.Label();
			this.checkFlashAttnModel = new System.Windows.Forms.CheckBox();
			this.label31 = new System.Windows.Forms.Label();
			this.label32 = new System.Windows.Forms.Label();
			this.groupCommonModel = new System.Windows.Forms.GroupBox();
			this.numCtxSizeModel = new System.Windows.Forms.NumericUpDown();
			this.numGPULayersModel = new System.Windows.Forms.NumericUpDown();
			this.numTempModel = new System.Windows.Forms.NumericUpDown();
			this.label33 = new System.Windows.Forms.Label();
			this.label34 = new System.Windows.Forms.Label();
			this.label35 = new System.Windows.Forms.Label();
			this.tabPageMetadata = new System.Windows.Forms.TabPage();
			this.listViewMeta = new System.Windows.Forms.ListView();
			this.columnHeader3 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
			this.columnHeader4 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
			this.splitContainer3 = new System.Windows.Forms.SplitContainer();
			this.listViewHugSearch = new System.Windows.Forms.ListView();
			this.columnHeader5 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
			this.columnHeader6 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
			this.columnHeader7 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
			this.columnHeader11 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
			this.columnHeader12 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
			this.columnHeader13 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
			this.columnHeader14 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
			this.listViewHugFiles = new System.Windows.Forms.ListView();
			this.columnHeader8 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
			this.columnHeader10 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
			this.checkSpeak = new System.Windows.Forms.CheckBox();
			this.checkVoiceInput = new System.Windows.Forms.CheckBox();
			this.checkMarkdown = new System.Windows.Forms.CheckBox();
			this.checkStream = new System.Windows.Forms.CheckBox();
			this.butCodeBlock = new System.Windows.Forms.Button();
			this.butReset = new System.Windows.Forms.Button();
			this.butGen = new System.Windows.Forms.Button();
			this.butSearch = new System.Windows.Forms.Button();
			this.textSearchTerm = new System.Windows.Forms.TextBox();
			this.label13 = new System.Windows.Forms.Label();
			this.progressBar1 = new System.Windows.Forms.ProgressBar();
			this.butDownload = new System.Windows.Forms.Button();
			this.tabControl1 = new System.Windows.Forms.TabControl();
			this.tabPageChat = new System.Windows.Forms.TabPage();
			this.tabPageSettings = new System.Windows.Forms.TabPage();
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
			this.tabPageModels = new System.Windows.Forms.TabPage();
			this.checkLoadAuto = new System.Windows.Forms.CheckBox();
			this.butUnload = new System.Windows.Forms.Button();
			this.butLoad = new System.Windows.Forms.Button();
			this.tabPageHuggingFace = new System.Windows.Forms.TabPage();
			this.folderBrowserDialog1 = new System.Windows.Forms.FolderBrowserDialog();
			this.statusStrip1 = new System.Windows.Forms.StatusStrip();
			this.toolStripStatusLabel1 = new System.Windows.Forms.ToolStripStatusLabel();
			this.labelTokens = new System.Windows.Forms.ToolStripStatusLabel();
			this.labelTPS = new System.Windows.Forms.ToolStripStatusLabel();
			this.labelPreGen = new System.Windows.Forms.ToolStripStatusLabel();
			this.toolTip1 = new System.Windows.Forms.ToolTip(this.components);
			this.groupBox7 = new System.Windows.Forms.GroupBox();
			this.checkApiServerEnable = new System.Windows.Forms.CheckBox();
			this.numApiServerPort = new System.Windows.Forms.NumericUpDown();
			this.label28 = new System.Windows.Forms.Label();
			((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
			this.splitContainer1.Panel1.SuspendLayout();
			this.splitContainer1.Panel2.SuspendLayout();
			this.splitContainer1.SuspendLayout();
			((System.ComponentModel.ISupportInitialize)(this.splitContainer2)).BeginInit();
			this.splitContainer2.Panel1.SuspendLayout();
			this.splitContainer2.Panel2.SuspendLayout();
			this.splitContainer2.SuspendLayout();
			this.tabControlModelStuff.SuspendLayout();
			this.tabPageModelSettings.SuspendLayout();
			this.groupAdvancedModel.SuspendLayout();
			((System.ComponentModel.ISupportInitialize)(this.numMinPModel)).BeginInit();
			((System.ComponentModel.ISupportInitialize)(this.numTopKModel)).BeginInit();
			((System.ComponentModel.ISupportInitialize)(this.numTopPModel)).BeginInit();
			this.groupCommonModel.SuspendLayout();
			((System.ComponentModel.ISupportInitialize)(this.numCtxSizeModel)).BeginInit();
			((System.ComponentModel.ISupportInitialize)(this.numGPULayersModel)).BeginInit();
			((System.ComponentModel.ISupportInitialize)(this.numTempModel)).BeginInit();
			this.tabPageMetadata.SuspendLayout();
			((System.ComponentModel.ISupportInitialize)(this.splitContainer3)).BeginInit();
			this.splitContainer3.Panel1.SuspendLayout();
			this.splitContainer3.Panel2.SuspendLayout();
			this.splitContainer3.SuspendLayout();
			this.tabControl1.SuspendLayout();
			this.tabPageChat.SuspendLayout();
			this.tabPageSettings.SuspendLayout();
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
			this.tabPageModels.SuspendLayout();
			this.tabPageHuggingFace.SuspendLayout();
			this.statusStrip1.SuspendLayout();
			this.groupBox7.SuspendLayout();
			((System.ComponentModel.ISupportInitialize)(this.numApiServerPort)).BeginInit();
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
			this.splitContainer1.Panel1.Controls.Add(this.panelChat);
			// 
			// splitContainer1.Panel2
			// 
			this.splitContainer1.Panel2.Controls.Add(this.textInput);
			// 
			// panelChat
			// 
			resources.ApplyResources(this.panelChat, "panelChat");
			this.panelChat.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
			this.panelChat.CausesValidation = false;
			this.panelChat.Name = "panelChat";
			this.panelChat.Layout += new System.Windows.Forms.LayoutEventHandler(this.PanelChat_Layout);
			// 
			// textInput
			// 
			this.textInput.AllowDrop = true;
			resources.ApplyResources(this.textInput, "textInput");
			this.textInput.Name = "textInput";
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
			this.splitContainer2.Panel1.Controls.Add(this.listViewModels);
			// 
			// splitContainer2.Panel2
			// 
			this.splitContainer2.Panel2.Controls.Add(this.tabControlModelStuff);
			// 
			// listViewModels
			// 
			this.listViewModels.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.columnHeader1,
            this.columnHeader2});
			resources.ApplyResources(this.listViewModels, "listViewModels");
			this.listViewModels.GridLines = true;
			this.listViewModels.HideSelection = false;
			this.listViewModels.MultiSelect = false;
			this.listViewModels.Name = "listViewModels";
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
			// tabControlModelStuff
			// 
			this.tabControlModelStuff.Controls.Add(this.tabPageModelSettings);
			this.tabControlModelStuff.Controls.Add(this.tabPageMetadata);
			resources.ApplyResources(this.tabControlModelStuff, "tabControlModelStuff");
			this.tabControlModelStuff.Name = "tabControlModelStuff";
			this.tabControlModelStuff.SelectedIndex = 0;
			// 
			// tabPageModelSettings
			// 
			this.tabPageModelSettings.Controls.Add(this.labelSystemPromptModel);
			this.tabPageModelSettings.Controls.Add(this.textSystemPromptModel);
			this.tabPageModelSettings.Controls.Add(this.butApplyModelSettings);
			this.tabPageModelSettings.Controls.Add(this.checkUseModelSettings);
			this.tabPageModelSettings.Controls.Add(this.groupAdvancedModel);
			this.tabPageModelSettings.Controls.Add(this.groupCommonModel);
			resources.ApplyResources(this.tabPageModelSettings, "tabPageModelSettings");
			this.tabPageModelSettings.Name = "tabPageModelSettings";
			this.tabPageModelSettings.UseVisualStyleBackColor = true;
			// 
			// labelSystemPromptModel
			// 
			resources.ApplyResources(this.labelSystemPromptModel, "labelSystemPromptModel");
			this.labelSystemPromptModel.Name = "labelSystemPromptModel";
			// 
			// textSystemPromptModel
			// 
			resources.ApplyResources(this.textSystemPromptModel, "textSystemPromptModel");
			this.textSystemPromptModel.Name = "textSystemPromptModel";
			// 
			// butApplyModelSettings
			// 
			resources.ApplyResources(this.butApplyModelSettings, "butApplyModelSettings");
			this.butApplyModelSettings.Name = "butApplyModelSettings";
			this.butApplyModelSettings.UseVisualStyleBackColor = true;
			this.butApplyModelSettings.Click += new System.EventHandler(this.ButApplyModelSettings_Click);
			// 
			// checkUseModelSettings
			// 
			resources.ApplyResources(this.checkUseModelSettings, "checkUseModelSettings");
			this.checkUseModelSettings.Name = "checkUseModelSettings";
			this.checkUseModelSettings.UseVisualStyleBackColor = true;
			this.checkUseModelSettings.CheckedChanged += new System.EventHandler(this.CheckUseModelSettings_CheckedChanged);
			// 
			// groupAdvancedModel
			// 
			this.groupAdvancedModel.Controls.Add(this.numMinPModel);
			this.groupAdvancedModel.Controls.Add(this.numTopKModel);
			this.groupAdvancedModel.Controls.Add(this.numTopPModel);
			this.groupAdvancedModel.Controls.Add(this.label27);
			this.groupAdvancedModel.Controls.Add(this.checkFlashAttnModel);
			this.groupAdvancedModel.Controls.Add(this.label31);
			this.groupAdvancedModel.Controls.Add(this.label32);
			resources.ApplyResources(this.groupAdvancedModel, "groupAdvancedModel");
			this.groupAdvancedModel.Name = "groupAdvancedModel";
			this.groupAdvancedModel.TabStop = false;
			// 
			// numMinPModel
			// 
			resources.ApplyResources(this.numMinPModel, "numMinPModel");
			this.numMinPModel.DecimalPlaces = 2;
			this.numMinPModel.Increment = new decimal(new int[] {
            1,
            0,
            0,
            131072});
			this.numMinPModel.Maximum = new decimal(new int[] {
            1,
            0,
            0,
            0});
			this.numMinPModel.Name = "numMinPModel";
			// 
			// numTopKModel
			// 
			resources.ApplyResources(this.numTopKModel, "numTopKModel");
			this.numTopKModel.Maximum = new decimal(new int[] {
            100000,
            0,
            0,
            0});
			this.numTopKModel.Name = "numTopKModel";
			this.numTopKModel.Value = new decimal(new int[] {
            40,
            0,
            0,
            0});
			// 
			// numTopPModel
			// 
			resources.ApplyResources(this.numTopPModel, "numTopPModel");
			this.numTopPModel.DecimalPlaces = 2;
			this.numTopPModel.Increment = new decimal(new int[] {
            1,
            0,
            0,
            131072});
			this.numTopPModel.Maximum = new decimal(new int[] {
            1,
            0,
            0,
            0});
			this.numTopPModel.Name = "numTopPModel";
			this.numTopPModel.Value = new decimal(new int[] {
            95,
            0,
            0,
            131072});
			// 
			// label27
			// 
			resources.ApplyResources(this.label27, "label27");
			this.label27.Name = "label27";
			// 
			// checkFlashAttnModel
			// 
			resources.ApplyResources(this.checkFlashAttnModel, "checkFlashAttnModel");
			this.checkFlashAttnModel.Checked = true;
			this.checkFlashAttnModel.CheckState = System.Windows.Forms.CheckState.Checked;
			this.checkFlashAttnModel.Name = "checkFlashAttnModel";
			this.checkFlashAttnModel.UseVisualStyleBackColor = true;
			// 
			// label31
			// 
			resources.ApplyResources(this.label31, "label31");
			this.label31.Name = "label31";
			// 
			// label32
			// 
			resources.ApplyResources(this.label32, "label32");
			this.label32.Name = "label32";
			// 
			// groupCommonModel
			// 
			this.groupCommonModel.Controls.Add(this.numCtxSizeModel);
			this.groupCommonModel.Controls.Add(this.numGPULayersModel);
			this.groupCommonModel.Controls.Add(this.numTempModel);
			this.groupCommonModel.Controls.Add(this.label33);
			this.groupCommonModel.Controls.Add(this.label34);
			this.groupCommonModel.Controls.Add(this.label35);
			resources.ApplyResources(this.groupCommonModel, "groupCommonModel");
			this.groupCommonModel.Name = "groupCommonModel";
			this.groupCommonModel.TabStop = false;
			// 
			// numCtxSizeModel
			// 
			resources.ApplyResources(this.numCtxSizeModel, "numCtxSizeModel");
			this.numCtxSizeModel.Maximum = new decimal(new int[] {
            1048576,
            0,
            0,
            0});
			this.numCtxSizeModel.Name = "numCtxSizeModel";
			this.numCtxSizeModel.Value = new decimal(new int[] {
            8192,
            0,
            0,
            0});
			// 
			// numGPULayersModel
			// 
			resources.ApplyResources(this.numGPULayersModel, "numGPULayersModel");
			this.numGPULayersModel.Maximum = new decimal(new int[] {
            1024,
            0,
            0,
            0});
			this.numGPULayersModel.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            -2147483648});
			this.numGPULayersModel.Name = "numGPULayersModel";
			this.numGPULayersModel.Value = new decimal(new int[] {
            1,
            0,
            0,
            -2147483648});
			// 
			// numTempModel
			// 
			resources.ApplyResources(this.numTempModel, "numTempModel");
			this.numTempModel.DecimalPlaces = 2;
			this.numTempModel.Increment = new decimal(new int[] {
            1,
            0,
            0,
            65536});
			this.numTempModel.Maximum = new decimal(new int[] {
            1,
            0,
            0,
            0});
			this.numTempModel.Name = "numTempModel";
			this.numTempModel.Value = new decimal(new int[] {
            6,
            0,
            0,
            65536});
			// 
			// label33
			// 
			resources.ApplyResources(this.label33, "label33");
			this.label33.Name = "label33";
			// 
			// label34
			// 
			resources.ApplyResources(this.label34, "label34");
			this.label34.Name = "label34";
			// 
			// label35
			// 
			resources.ApplyResources(this.label35, "label35");
			this.label35.Name = "label35";
			// 
			// tabPageMetadata
			// 
			this.tabPageMetadata.Controls.Add(this.listViewMeta);
			resources.ApplyResources(this.tabPageMetadata, "tabPageMetadata");
			this.tabPageMetadata.Name = "tabPageMetadata";
			this.tabPageMetadata.UseVisualStyleBackColor = true;
			// 
			// listViewMeta
			// 
			this.listViewMeta.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.columnHeader3,
            this.columnHeader4});
			resources.ApplyResources(this.listViewMeta, "listViewMeta");
			this.listViewMeta.GridLines = true;
			this.listViewMeta.HideSelection = false;
			this.listViewMeta.MultiSelect = false;
			this.listViewMeta.Name = "listViewMeta";
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
			this.splitContainer3.Panel1.Controls.Add(this.listViewHugSearch);
			// 
			// splitContainer3.Panel2
			// 
			this.splitContainer3.Panel2.Controls.Add(this.listViewHugFiles);
			// 
			// listViewHugSearch
			// 
			this.listViewHugSearch.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.columnHeader5,
            this.columnHeader6,
            this.columnHeader7,
            this.columnHeader11,
            this.columnHeader12,
            this.columnHeader13,
            this.columnHeader14});
			resources.ApplyResources(this.listViewHugSearch, "listViewHugSearch");
			this.listViewHugSearch.GridLines = true;
			this.listViewHugSearch.HideSelection = false;
			this.listViewHugSearch.Name = "listViewHugSearch";
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
			// listViewHugFiles
			// 
			this.listViewHugFiles.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.columnHeader8,
            this.columnHeader10});
			resources.ApplyResources(this.listViewHugFiles, "listViewHugFiles");
			this.listViewHugFiles.GridLines = true;
			this.listViewHugFiles.HideSelection = false;
			this.listViewHugFiles.Name = "listViewHugFiles";
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
			// checkSpeak
			// 
			resources.ApplyResources(this.checkSpeak, "checkSpeak");
			this.checkSpeak.Name = "checkSpeak";
			this.checkSpeak.UseVisualStyleBackColor = true;
			this.checkSpeak.CheckedChanged += new System.EventHandler(this.CheckSpeak_CheckedChanged);
			// 
			// checkVoiceInput
			// 
			resources.ApplyResources(this.checkVoiceInput, "checkVoiceInput");
			this.checkVoiceInput.Name = "checkVoiceInput";
			this.checkVoiceInput.ThreeState = true;
			this.checkVoiceInput.UseVisualStyleBackColor = true;
			this.checkVoiceInput.CheckedChanged += new System.EventHandler(this.CheckVoiceInput_CheckedChanged);
			// 
			// checkMarkdown
			// 
			resources.ApplyResources(this.checkMarkdown, "checkMarkdown");
			this.checkMarkdown.Checked = true;
			this.checkMarkdown.CheckState = System.Windows.Forms.CheckState.Checked;
			this.checkMarkdown.Name = "checkMarkdown";
			this.checkMarkdown.UseVisualStyleBackColor = true;
			this.checkMarkdown.CheckedChanged += new System.EventHandler(this.CheckMarkdown_CheckedChanged);
			// 
			// checkStream
			// 
			resources.ApplyResources(this.checkStream, "checkStream");
			this.checkStream.Checked = true;
			this.checkStream.CheckState = System.Windows.Forms.CheckState.Checked;
			this.checkStream.Name = "checkStream";
			this.checkStream.UseVisualStyleBackColor = true;
			// 
			// butCodeBlock
			// 
			resources.ApplyResources(this.butCodeBlock, "butCodeBlock");
			this.butCodeBlock.Name = "butCodeBlock";
			this.butCodeBlock.UseVisualStyleBackColor = true;
			this.butCodeBlock.Click += new System.EventHandler(this.ButCodeBlock_Click);
			// 
			// butReset
			// 
			resources.ApplyResources(this.butReset, "butReset");
			this.butReset.Name = "butReset";
			this.butReset.UseVisualStyleBackColor = true;
			this.butReset.Click += new System.EventHandler(this.ButReset_Click);
			// 
			// butGen
			// 
			resources.ApplyResources(this.butGen, "butGen");
			this.butGen.Name = "butGen";
			this.butGen.Text = global::LMStud.Properties.Resources.Generate;
			this.butGen.UseVisualStyleBackColor = true;
			this.butGen.Click += new System.EventHandler(this.ButGen_Click);
			// 
			// butSearch
			// 
			resources.ApplyResources(this.butSearch, "butSearch");
			this.butSearch.Name = "butSearch";
			this.butSearch.UseVisualStyleBackColor = true;
			this.butSearch.Click += new System.EventHandler(this.ButSearch_Click);
			// 
			// textSearchTerm
			// 
			resources.ApplyResources(this.textSearchTerm, "textSearchTerm");
			this.textSearchTerm.Name = "textSearchTerm";
			this.textSearchTerm.KeyDown += new System.Windows.Forms.KeyEventHandler(this.TextSearchTerm_KeyDown);
			// 
			// label13
			// 
			resources.ApplyResources(this.label13, "label13");
			this.label13.Name = "label13";
			// 
			// progressBar1
			// 
			resources.ApplyResources(this.progressBar1, "progressBar1");
			this.progressBar1.Name = "progressBar1";
			// 
			// butDownload
			// 
			resources.ApplyResources(this.butDownload, "butDownload");
			this.butDownload.Name = "butDownload";
			this.butDownload.UseVisualStyleBackColor = true;
			this.butDownload.Click += new System.EventHandler(this.ButDownload_Click);
			// 
			// tabControl1
			// 
			resources.ApplyResources(this.tabControl1, "tabControl1");
			this.tabControl1.Controls.Add(this.tabPageChat);
			this.tabControl1.Controls.Add(this.tabPageSettings);
			this.tabControl1.Controls.Add(this.tabPageModels);
			this.tabControl1.Controls.Add(this.tabPageHuggingFace);
			this.tabControl1.Name = "tabControl1";
			this.tabControl1.SelectedIndex = 0;
			// 
			// tabPageChat
			// 
			this.tabPageChat.Controls.Add(this.checkSpeak);
			this.tabPageChat.Controls.Add(this.splitContainer1);
			this.tabPageChat.Controls.Add(this.checkVoiceInput);
			this.tabPageChat.Controls.Add(this.butReset);
			this.tabPageChat.Controls.Add(this.checkMarkdown);
			this.tabPageChat.Controls.Add(this.butGen);
			this.tabPageChat.Controls.Add(this.checkStream);
			this.tabPageChat.Controls.Add(this.butCodeBlock);
			resources.ApplyResources(this.tabPageChat, "tabPageChat");
			this.tabPageChat.Name = "tabPageChat";
			this.tabPageChat.UseVisualStyleBackColor = true;
			// 
			// tabPageSettings
			// 
			this.tabPageSettings.Controls.Add(this.groupBox7);
			this.tabPageSettings.Controls.Add(this.textModelsPath);
			this.tabPageSettings.Controls.Add(this.groupBox6);
			this.tabPageSettings.Controls.Add(this.groupBox5);
			this.tabPageSettings.Controls.Add(this.groupBox4);
			this.tabPageSettings.Controls.Add(this.groupBox3);
			this.tabPageSettings.Controls.Add(this.groupBox2);
			this.tabPageSettings.Controls.Add(this.groupBox1);
			this.tabPageSettings.Controls.Add(this.groupCPUParamsBatch);
			this.tabPageSettings.Controls.Add(this.groupCPUParams);
			this.tabPageSettings.Controls.Add(this.groupAdvanced);
			this.tabPageSettings.Controls.Add(this.groupCommon);
			this.tabPageSettings.Controls.Add(this.label3);
			this.tabPageSettings.Controls.Add(this.butBrowse);
			this.tabPageSettings.Controls.Add(this.butApply);
			this.tabPageSettings.Controls.Add(this.label1);
			this.tabPageSettings.Controls.Add(this.textSystemPrompt);
			resources.ApplyResources(this.tabPageSettings, "tabPageSettings");
			this.tabPageSettings.Name = "tabPageSettings";
			this.tabPageSettings.UseVisualStyleBackColor = true;
			// 
			// textModelsPath
			// 
			resources.ApplyResources(this.textModelsPath, "textModelsPath");
			this.textModelsPath.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.Suggest;
			this.textModelsPath.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.FileSystem;
			this.textModelsPath.Name = "textModelsPath";
			this.textModelsPath.KeyDown += new System.Windows.Forms.KeyEventHandler(this.TextModelsPath_KeyDown);
			// 
			// groupBox6
			// 
			this.groupBox6.Controls.Add(this.numVadThreshold);
			this.groupBox6.Controls.Add(this.butVADDown);
			this.groupBox6.Controls.Add(this.radioBasicVAD);
			this.groupBox6.Controls.Add(this.radioWhisperVAD);
			this.groupBox6.Controls.Add(this.label17);
			this.groupBox6.Controls.Add(this.comboVADModel);
			this.groupBox6.Controls.Add(this.label26);
			resources.ApplyResources(this.groupBox6, "groupBox6");
			this.groupBox6.Name = "groupBox6";
			this.groupBox6.TabStop = false;
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
			this.butVADDown.UseVisualStyleBackColor = true;
			this.butVADDown.Click += new System.EventHandler(this.butDownloadVADModel_Click);
			// 
			// radioBasicVAD
			// 
			resources.ApplyResources(this.radioBasicVAD, "radioBasicVAD");
			this.radioBasicVAD.Checked = true;
			this.radioBasicVAD.Name = "radioBasicVAD";
			this.radioBasicVAD.TabStop = true;
			this.radioBasicVAD.UseVisualStyleBackColor = true;
			// 
			// radioWhisperVAD
			// 
			resources.ApplyResources(this.radioWhisperVAD, "radioWhisperVAD");
			this.radioWhisperVAD.Name = "radioWhisperVAD";
			this.radioWhisperVAD.UseVisualStyleBackColor = true;
			// 
			// label17
			// 
			resources.ApplyResources(this.label17, "label17");
			this.label17.Name = "label17";
			// 
			// comboVADModel
			// 
			resources.ApplyResources(this.comboVADModel, "comboVADModel");
			this.comboVADModel.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
			this.comboVADModel.FormattingEnabled = true;
			this.comboVADModel.Name = "comboVADModel";
			this.comboVADModel.DropDown += new System.EventHandler(this.ComboVADModel_DropDown);
			// 
			// label26
			// 
			resources.ApplyResources(this.label26, "label26");
			this.label26.Name = "label26";
			// 
			// groupBox5
			// 
			this.groupBox5.Controls.Add(this.checkDateTimeEnable);
			resources.ApplyResources(this.groupBox5, "groupBox5");
			this.groupBox5.Name = "groupBox5";
			this.groupBox5.TabStop = false;
			// 
			// checkDateTimeEnable
			// 
			resources.ApplyResources(this.checkDateTimeEnable, "checkDateTimeEnable");
			this.checkDateTimeEnable.Name = "checkDateTimeEnable";
			this.checkDateTimeEnable.UseVisualStyleBackColor = true;
			// 
			// groupBox4
			// 
			this.groupBox4.Controls.Add(this.textFileBasePath);
			this.groupBox4.Controls.Add(this.linkFileInstruction);
			this.groupBox4.Controls.Add(this.label22);
			this.groupBox4.Controls.Add(this.checkFileWriteEnable);
			this.groupBox4.Controls.Add(this.checkFileCreateEnable);
			this.groupBox4.Controls.Add(this.checkFileReadEnable);
			this.groupBox4.Controls.Add(this.checkFileListEnable);
			resources.ApplyResources(this.groupBox4, "groupBox4");
			this.groupBox4.Name = "groupBox4";
			this.groupBox4.TabStop = false;
			// 
			// textFileBasePath
			// 
			resources.ApplyResources(this.textFileBasePath, "textFileBasePath");
			this.textFileBasePath.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.Suggest;
			this.textFileBasePath.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.FileSystemDirectories;
			this.textFileBasePath.Name = "textFileBasePath";
			// 
			// linkFileInstruction
			// 
			resources.ApplyResources(this.linkFileInstruction, "linkFileInstruction");
			this.linkFileInstruction.LinkColor = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(255)))));
			this.linkFileInstruction.Name = "linkFileInstruction";
			this.linkFileInstruction.TabStop = true;
			this.linkFileInstruction.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.LinkLabel1_LinkClicked);
			// 
			// label22
			// 
			resources.ApplyResources(this.label22, "label22");
			this.label22.Name = "label22";
			// 
			// checkFileWriteEnable
			// 
			resources.ApplyResources(this.checkFileWriteEnable, "checkFileWriteEnable");
			this.checkFileWriteEnable.Name = "checkFileWriteEnable";
			this.checkFileWriteEnable.UseVisualStyleBackColor = true;
			// 
			// checkFileCreateEnable
			// 
			resources.ApplyResources(this.checkFileCreateEnable, "checkFileCreateEnable");
			this.checkFileCreateEnable.Name = "checkFileCreateEnable";
			this.checkFileCreateEnable.UseVisualStyleBackColor = true;
			// 
			// checkFileReadEnable
			// 
			resources.ApplyResources(this.checkFileReadEnable, "checkFileReadEnable");
			this.checkFileReadEnable.Name = "checkFileReadEnable";
			this.checkFileReadEnable.UseVisualStyleBackColor = true;
			// 
			// checkFileListEnable
			// 
			resources.ApplyResources(this.checkFileListEnable, "checkFileListEnable");
			this.checkFileListEnable.Name = "checkFileListEnable";
			this.checkFileListEnable.UseVisualStyleBackColor = true;
			// 
			// groupBox3
			// 
			this.groupBox3.Controls.Add(this.checkWebpageFetchEnable);
			resources.ApplyResources(this.groupBox3, "groupBox3");
			this.groupBox3.Name = "groupBox3";
			this.groupBox3.TabStop = false;
			// 
			// checkWebpageFetchEnable
			// 
			resources.ApplyResources(this.checkWebpageFetchEnable, "checkWebpageFetchEnable");
			this.checkWebpageFetchEnable.Name = "checkWebpageFetchEnable";
			this.checkWebpageFetchEnable.UseVisualStyleBackColor = true;
			// 
			// groupBox2
			// 
			this.groupBox2.Controls.Add(this.numGoogleResults);
			this.groupBox2.Controls.Add(this.textGoogleSearchID);
			this.groupBox2.Controls.Add(this.textGoogleApiKey);
			this.groupBox2.Controls.Add(this.label21);
			this.groupBox2.Controls.Add(this.checkGoogleEnable);
			this.groupBox2.Controls.Add(this.label20);
			this.groupBox2.Controls.Add(this.label19);
			resources.ApplyResources(this.groupBox2, "groupBox2");
			this.groupBox2.Name = "groupBox2";
			this.groupBox2.TabStop = false;
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
			// 
			// textGoogleApiKey
			// 
			resources.ApplyResources(this.textGoogleApiKey, "textGoogleApiKey");
			this.textGoogleApiKey.Name = "textGoogleApiKey";
			// 
			// label21
			// 
			resources.ApplyResources(this.label21, "label21");
			this.label21.Name = "label21";
			// 
			// checkGoogleEnable
			// 
			resources.ApplyResources(this.checkGoogleEnable, "checkGoogleEnable");
			this.checkGoogleEnable.Name = "checkGoogleEnable";
			this.checkGoogleEnable.UseVisualStyleBackColor = true;
			// 
			// label20
			// 
			resources.ApplyResources(this.label20, "label20");
			this.label20.Name = "label20";
			// 
			// label19
			// 
			resources.ApplyResources(this.label19, "label19");
			this.label19.Name = "label19";
			// 
			// groupBox1
			// 
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
			resources.ApplyResources(this.groupBox1, "groupBox1");
			this.groupBox1.Name = "groupBox1";
			this.groupBox1.TabStop = false;
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
			// 
			// label25
			// 
			resources.ApplyResources(this.label25, "label25");
			this.label25.Name = "label25";
			// 
			// label24
			// 
			resources.ApplyResources(this.label24, "label24");
			this.label24.Name = "label24";
			// 
			// label18
			// 
			resources.ApplyResources(this.label18, "label18");
			this.label18.Name = "label18";
			// 
			// checkWhisperUseGPU
			// 
			resources.ApplyResources(this.checkWhisperUseGPU, "checkWhisperUseGPU");
			this.checkWhisperUseGPU.Name = "checkWhisperUseGPU";
			this.checkWhisperUseGPU.UseVisualStyleBackColor = true;
			// 
			// label16
			// 
			resources.ApplyResources(this.label16, "label16");
			this.label16.Name = "label16";
			// 
			// butWhispDown
			// 
			resources.ApplyResources(this.butWhispDown, "butWhispDown");
			this.butWhispDown.Name = "butWhispDown";
			this.butWhispDown.UseVisualStyleBackColor = true;
			this.butWhispDown.Click += new System.EventHandler(this.ButWhispDown_Click);
			// 
			// comboWhisperModel
			// 
			resources.ApplyResources(this.comboWhisperModel, "comboWhisperModel");
			this.comboWhisperModel.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
			this.comboWhisperModel.FormattingEnabled = true;
			this.comboWhisperModel.Name = "comboWhisperModel";
			this.comboWhisperModel.DropDown += new System.EventHandler(this.ComboWhisperModel_DropDown);
			// 
			// label15
			// 
			resources.ApplyResources(this.label15, "label15");
			this.label15.Name = "label15";
			// 
			// groupCPUParamsBatch
			// 
			this.groupCPUParamsBatch.Controls.Add(this.numThreadsBatch);
			this.groupCPUParamsBatch.Controls.Add(this.label14);
			resources.ApplyResources(this.groupCPUParamsBatch, "groupCPUParamsBatch");
			this.groupCPUParamsBatch.Name = "groupCPUParamsBatch";
			this.groupCPUParamsBatch.TabStop = false;
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
			// 
			// groupCPUParams
			// 
			this.groupCPUParams.Controls.Add(this.numThreads);
			this.groupCPUParams.Controls.Add(this.label2);
			resources.ApplyResources(this.groupCPUParams, "groupCPUParams");
			this.groupCPUParams.Name = "groupCPUParams";
			this.groupCPUParams.TabStop = false;
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
			// 
			// groupAdvanced
			// 
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
			resources.ApplyResources(this.groupAdvanced, "groupAdvanced");
			this.groupAdvanced.Name = "groupAdvanced";
			this.groupAdvanced.TabStop = false;
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
			// 
			// checkFlashAttn
			// 
			resources.ApplyResources(this.checkFlashAttn, "checkFlashAttn");
			this.checkFlashAttn.Checked = true;
			this.checkFlashAttn.CheckState = System.Windows.Forms.CheckState.Checked;
			this.checkFlashAttn.Name = "checkFlashAttn";
			this.checkFlashAttn.UseVisualStyleBackColor = true;
			// 
			// checkMLock
			// 
			resources.ApplyResources(this.checkMLock, "checkMLock");
			this.checkMLock.Name = "checkMLock";
			this.checkMLock.UseVisualStyleBackColor = true;
			// 
			// checkMMap
			// 
			resources.ApplyResources(this.checkMMap, "checkMMap");
			this.checkMMap.Name = "checkMMap";
			this.checkMMap.UseVisualStyleBackColor = true;
			// 
			// label6
			// 
			resources.ApplyResources(this.label6, "label6");
			this.label6.Name = "label6";
			// 
			// label12
			// 
			resources.ApplyResources(this.label12, "label12");
			this.label12.Name = "label12";
			// 
			// label11
			// 
			resources.ApplyResources(this.label11, "label11");
			this.label11.Name = "label11";
			// 
			// label8
			// 
			resources.ApplyResources(this.label8, "label8");
			this.label8.Name = "label8";
			// 
			// label9
			// 
			resources.ApplyResources(this.label9, "label9");
			this.label9.Name = "label9";
			// 
			// groupCommon
			// 
			this.groupCommon.Controls.Add(this.numCtxSize);
			this.groupCommon.Controls.Add(this.numGPULayers);
			this.groupCommon.Controls.Add(this.numNGen);
			this.groupCommon.Controls.Add(this.numTemp);
			this.groupCommon.Controls.Add(this.label5);
			this.groupCommon.Controls.Add(this.label7);
			this.groupCommon.Controls.Add(this.label10);
			this.groupCommon.Controls.Add(this.label4);
			resources.ApplyResources(this.groupCommon, "groupCommon");
			this.groupCommon.Name = "groupCommon";
			this.groupCommon.TabStop = false;
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
			// 
			// label7
			// 
			resources.ApplyResources(this.label7, "label7");
			this.label7.Name = "label7";
			// 
			// label10
			// 
			resources.ApplyResources(this.label10, "label10");
			this.label10.Name = "label10";
			// 
			// label4
			// 
			resources.ApplyResources(this.label4, "label4");
			this.label4.Name = "label4";
			// 
			// label3
			// 
			resources.ApplyResources(this.label3, "label3");
			this.label3.Name = "label3";
			// 
			// butBrowse
			// 
			resources.ApplyResources(this.butBrowse, "butBrowse");
			this.butBrowse.Name = "butBrowse";
			this.butBrowse.UseVisualStyleBackColor = true;
			this.butBrowse.Click += new System.EventHandler(this.ButBrowse_Click);
			// 
			// butApply
			// 
			resources.ApplyResources(this.butApply, "butApply");
			this.butApply.Name = "butApply";
			this.butApply.UseVisualStyleBackColor = true;
			this.butApply.Click += new System.EventHandler(this.ButApply_Click);
			// 
			// label1
			// 
			resources.ApplyResources(this.label1, "label1");
			this.label1.Name = "label1";
			// 
			// textSystemPrompt
			// 
			resources.ApplyResources(this.textSystemPrompt, "textSystemPrompt");
			this.textSystemPrompt.Name = "textSystemPrompt";
			// 
			// tabPageModels
			// 
			this.tabPageModels.Controls.Add(this.splitContainer2);
			this.tabPageModels.Controls.Add(this.checkLoadAuto);
			this.tabPageModels.Controls.Add(this.butUnload);
			this.tabPageModels.Controls.Add(this.butLoad);
			resources.ApplyResources(this.tabPageModels, "tabPageModels");
			this.tabPageModels.Name = "tabPageModels";
			this.tabPageModels.UseVisualStyleBackColor = true;
			// 
			// checkLoadAuto
			// 
			resources.ApplyResources(this.checkLoadAuto, "checkLoadAuto");
			this.checkLoadAuto.Name = "checkLoadAuto";
			this.checkLoadAuto.UseVisualStyleBackColor = true;
			this.checkLoadAuto.CheckedChanged += new System.EventHandler(this.CheckLoadAuto_CheckedChanged);
			// 
			// butUnload
			// 
			resources.ApplyResources(this.butUnload, "butUnload");
			this.butUnload.Name = "butUnload";
			this.butUnload.UseVisualStyleBackColor = true;
			this.butUnload.Click += new System.EventHandler(this.ButUnload_Click);
			// 
			// butLoad
			// 
			resources.ApplyResources(this.butLoad, "butLoad");
			this.butLoad.Name = "butLoad";
			this.butLoad.UseVisualStyleBackColor = true;
			this.butLoad.Click += new System.EventHandler(this.ButLoad_Click);
			// 
			// tabPageHuggingFace
			// 
			this.tabPageHuggingFace.Controls.Add(this.butDownload);
			this.tabPageHuggingFace.Controls.Add(this.label13);
			this.tabPageHuggingFace.Controls.Add(this.butSearch);
			this.tabPageHuggingFace.Controls.Add(this.textSearchTerm);
			this.tabPageHuggingFace.Controls.Add(this.progressBar1);
			this.tabPageHuggingFace.Controls.Add(this.splitContainer3);
			resources.ApplyResources(this.tabPageHuggingFace, "tabPageHuggingFace");
			this.tabPageHuggingFace.Name = "tabPageHuggingFace";
			this.tabPageHuggingFace.UseVisualStyleBackColor = true;
			// 
			// statusStrip1
			// 
			this.statusStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripStatusLabel1,
            this.labelTokens,
            this.labelTPS,
            this.labelPreGen});
			this.statusStrip1.LayoutStyle = System.Windows.Forms.ToolStripLayoutStyle.HorizontalStackWithOverflow;
			resources.ApplyResources(this.statusStrip1, "statusStrip1");
			this.statusStrip1.Name = "statusStrip1";
			// 
			// toolStripStatusLabel1
			// 
			this.toolStripStatusLabel1.Name = "toolStripStatusLabel1";
			resources.ApplyResources(this.toolStripStatusLabel1, "toolStripStatusLabel1");
			// 
			// labelTokens
			// 
			this.labelTokens.BorderSides = System.Windows.Forms.ToolStripStatusLabelBorderSides.Left;
			this.labelTokens.BorderStyle = System.Windows.Forms.Border3DStyle.Etched;
			this.labelTokens.Name = "labelTokens";
			resources.ApplyResources(this.labelTokens, "labelTokens");
			// 
			// labelTPS
			// 
			this.labelTPS.BorderSides = System.Windows.Forms.ToolStripStatusLabelBorderSides.Left;
			this.labelTPS.BorderStyle = System.Windows.Forms.Border3DStyle.Etched;
			this.labelTPS.Name = "labelTPS";
			resources.ApplyResources(this.labelTPS, "labelTPS");
			// 
			// labelPreGen
			// 
			this.labelPreGen.BorderSides = System.Windows.Forms.ToolStripStatusLabelBorderSides.Left;
			this.labelPreGen.BorderStyle = System.Windows.Forms.Border3DStyle.Etched;
			this.labelPreGen.Name = "labelPreGen";
			resources.ApplyResources(this.labelPreGen, "labelPreGen");
			// 
			// toolTip1
			// 
			this.toolTip1.AutoPopDelay = 30000;
			this.toolTip1.InitialDelay = 500;
			this.toolTip1.ReshowDelay = 0;
			this.toolTip1.UseAnimation = false;
			this.toolTip1.UseFading = false;
			// 
			// groupBox7
			// 
			this.groupBox7.Controls.Add(this.numApiServerPort);
			this.groupBox7.Controls.Add(this.label28);
			this.groupBox7.Controls.Add(this.checkApiServerEnable);
			resources.ApplyResources(this.groupBox7, "groupBox7");
			this.groupBox7.Name = "groupBox7";
			this.groupBox7.TabStop = false;
			// 
			// checkApiServerEnable
			// 
			resources.ApplyResources(this.checkApiServerEnable, "checkApiServerEnable");
			this.checkApiServerEnable.Name = "checkApiServerEnable";
			this.checkApiServerEnable.UseVisualStyleBackColor = true;
			// 
			// numApiServerPort
			// 
			resources.ApplyResources(this.numApiServerPort, "numApiServerPort");
			this.numApiServerPort.Maximum = new decimal(new int[] {
            65535,
            0,
            0,
            0});
			this.numApiServerPort.Name = "numApiServerPort";
			this.numApiServerPort.Value = new decimal(new int[] {
            8,
            0,
            0,
            0});
			// 
			// label28
			// 
			resources.ApplyResources(this.label28, "label28");
			this.label28.Name = "label28";
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
			this.tabControlModelStuff.ResumeLayout(false);
			this.tabPageModelSettings.ResumeLayout(false);
			this.tabPageModelSettings.PerformLayout();
			this.groupAdvancedModel.ResumeLayout(false);
			((System.ComponentModel.ISupportInitialize)(this.numMinPModel)).EndInit();
			((System.ComponentModel.ISupportInitialize)(this.numTopKModel)).EndInit();
			((System.ComponentModel.ISupportInitialize)(this.numTopPModel)).EndInit();
			this.groupCommonModel.ResumeLayout(false);
			((System.ComponentModel.ISupportInitialize)(this.numCtxSizeModel)).EndInit();
			((System.ComponentModel.ISupportInitialize)(this.numGPULayersModel)).EndInit();
			((System.ComponentModel.ISupportInitialize)(this.numTempModel)).EndInit();
			this.tabPageMetadata.ResumeLayout(false);
			this.splitContainer3.Panel1.ResumeLayout(false);
			this.splitContainer3.Panel2.ResumeLayout(false);
			((System.ComponentModel.ISupportInitialize)(this.splitContainer3)).EndInit();
			this.splitContainer3.ResumeLayout(false);
			this.tabControl1.ResumeLayout(false);
			this.tabPageChat.ResumeLayout(false);
			this.tabPageChat.PerformLayout();
			this.tabPageSettings.ResumeLayout(false);
			this.tabPageSettings.PerformLayout();
			this.groupBox6.ResumeLayout(false);
			((System.ComponentModel.ISupportInitialize)(this.numVadThreshold)).EndInit();
			this.groupBox5.ResumeLayout(false);
			this.groupBox4.ResumeLayout(false);
			this.groupBox4.PerformLayout();
			this.groupBox3.ResumeLayout(false);
			this.groupBox2.ResumeLayout(false);
			this.groupBox2.PerformLayout();
			((System.ComponentModel.ISupportInitialize)(this.numGoogleResults)).EndInit();
			this.groupBox1.ResumeLayout(false);
			this.groupBox1.PerformLayout();
			((System.ComponentModel.ISupportInitialize)(this.numWakeWordSimilarity)).EndInit();
			((System.ComponentModel.ISupportInitialize)(this.numWhisperTemp)).EndInit();
			((System.ComponentModel.ISupportInitialize)(this.numFreqThreshold)).EndInit();
			this.groupCPUParamsBatch.ResumeLayout(false);
			((System.ComponentModel.ISupportInitialize)(this.numThreadsBatch)).EndInit();
			this.groupCPUParams.ResumeLayout(false);
			((System.ComponentModel.ISupportInitialize)(this.numThreads)).EndInit();
			this.groupAdvanced.ResumeLayout(false);
			((System.ComponentModel.ISupportInitialize)(this.numMinP)).EndInit();
			((System.ComponentModel.ISupportInitialize)(this.numRepPen)).EndInit();
			((System.ComponentModel.ISupportInitialize)(this.numBatchSize)).EndInit();
			((System.ComponentModel.ISupportInitialize)(this.numTopK)).EndInit();
			((System.ComponentModel.ISupportInitialize)(this.numTopP)).EndInit();
			this.groupCommon.ResumeLayout(false);
			((System.ComponentModel.ISupportInitialize)(this.numCtxSize)).EndInit();
			((System.ComponentModel.ISupportInitialize)(this.numGPULayers)).EndInit();
			((System.ComponentModel.ISupportInitialize)(this.numNGen)).EndInit();
			((System.ComponentModel.ISupportInitialize)(this.numTemp)).EndInit();
			this.tabPageModels.ResumeLayout(false);
			this.tabPageModels.PerformLayout();
			this.tabPageHuggingFace.ResumeLayout(false);
			this.tabPageHuggingFace.PerformLayout();
			this.statusStrip1.ResumeLayout(false);
			this.statusStrip1.PerformLayout();
			this.groupBox7.ResumeLayout(false);
			((System.ComponentModel.ISupportInitialize)(this.numApiServerPort)).EndInit();
			this.ResumeLayout(false);
			this.PerformLayout();

		}

		#endregion

		private System.Windows.Forms.TextBox textInput;
		private System.Windows.Forms.SplitContainer splitContainer1;
		private System.Windows.Forms.Button butGen;
		private System.Windows.Forms.TabControl tabControl1;
		private System.Windows.Forms.TabPage tabPageChat;
		private System.Windows.Forms.TabPage tabPageSettings;
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
		private System.Windows.Forms.TabPage tabPageModels;
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
		private System.Windows.Forms.TabPage tabPageHuggingFace;
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
		private System.Windows.Forms.TabControl tabControlModelStuff;
		private System.Windows.Forms.TabPage tabPageMetadata;
		private System.Windows.Forms.TabPage tabPageModelSettings;
		private System.Windows.Forms.GroupBox groupAdvancedModel;
		private System.Windows.Forms.NumericUpDown numMinPModel;
		private System.Windows.Forms.NumericUpDown numTopKModel;
		private System.Windows.Forms.NumericUpDown numTopPModel;
		private System.Windows.Forms.Label label27;
		private System.Windows.Forms.CheckBox checkFlashAttnModel;
		private System.Windows.Forms.Label label31;
		private System.Windows.Forms.Label label32;
		private System.Windows.Forms.GroupBox groupCommonModel;
		private System.Windows.Forms.NumericUpDown numCtxSizeModel;
		private System.Windows.Forms.NumericUpDown numGPULayersModel;
		private System.Windows.Forms.NumericUpDown numTempModel;
		private System.Windows.Forms.Label label33;
		private System.Windows.Forms.Label label34;
		private System.Windows.Forms.Label label35;
		private System.Windows.Forms.Button butApplyModelSettings;
		private System.Windows.Forms.CheckBox checkUseModelSettings;
		private System.Windows.Forms.Label labelSystemPromptModel;
		private System.Windows.Forms.TextBox textSystemPromptModel;
		private System.Windows.Forms.GroupBox groupBox7;
		private System.Windows.Forms.NumericUpDown numApiServerPort;
		private System.Windows.Forms.Label label28;
		private System.Windows.Forms.CheckBox checkApiServerEnable;
	}
}

