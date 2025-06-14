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
            if (disposing && (components != null)) {
                components.Dispose();
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
			this.textInput = new System.Windows.Forms.TextBox();
			this.splitContainer1 = new System.Windows.Forms.SplitContainer();
			this.panelChat = new LMStud.MyFlowLayoutPanel();
			this.checkStream = new System.Windows.Forms.CheckBox();
			this.checkSpeak = new System.Windows.Forms.CheckBox();
			this.checkVoiceInput = new System.Windows.Forms.CheckBox();
			this.butCodeBlock = new System.Windows.Forms.Button();
			this.checkMarkdown = new System.Windows.Forms.CheckBox();
			this.butReset = new System.Windows.Forms.Button();
			this.butGen = new System.Windows.Forms.Button();
			this.tabControl1 = new System.Windows.Forms.TabControl();
			this.tabPage1 = new System.Windows.Forms.TabPage();
			this.tabPage2 = new System.Windows.Forms.TabPage();
			this.groupBox3 = new System.Windows.Forms.GroupBox();
			this.checkWebpageFetchEnable = new System.Windows.Forms.CheckBox();
			this.groupBox2 = new System.Windows.Forms.GroupBox();
			this.checkGoogleEnable = new System.Windows.Forms.CheckBox();
			this.label20 = new System.Windows.Forms.Label();
			this.label19 = new System.Windows.Forms.Label();
			this.textGoogleSearchID = new System.Windows.Forms.TextBox();
			this.textGoogleApiKey = new System.Windows.Forms.TextBox();
			this.groupBox1 = new System.Windows.Forms.GroupBox();
			this.label18 = new System.Windows.Forms.Label();
			this.numFreqThreshold = new System.Windows.Forms.NumericUpDown();
			this.label17 = new System.Windows.Forms.Label();
			this.numVadThreshold = new System.Windows.Forms.NumericUpDown();
			this.checkWhisperUseGPU = new System.Windows.Forms.CheckBox();
			this.label16 = new System.Windows.Forms.Label();
			this.textWakeWord = new System.Windows.Forms.TextBox();
			this.butWhispDown = new System.Windows.Forms.Button();
			this.comboWhisperModel = new System.Windows.Forms.ComboBox();
			this.label15 = new System.Windows.Forms.Label();
			this.groupCPUParamsBatch = new System.Windows.Forms.GroupBox();
			this.numThreadsBatch = new System.Windows.Forms.NumericUpDown();
			this.label14 = new System.Windows.Forms.Label();
			this.checkStrictCPUBatch = new System.Windows.Forms.CheckBox();
			this.groupCPUParams = new System.Windows.Forms.GroupBox();
			this.numThreads = new System.Windows.Forms.NumericUpDown();
			this.label2 = new System.Windows.Forms.Label();
			this.checkStrictCPU = new System.Windows.Forms.CheckBox();
			this.groupAdvanced = new System.Windows.Forms.GroupBox();
			this.checkFlashAttn = new System.Windows.Forms.CheckBox();
			this.checkMLock = new System.Windows.Forms.CheckBox();
			this.checkMMap = new System.Windows.Forms.CheckBox();
			this.comboNUMAStrat = new System.Windows.Forms.ComboBox();
			this.label6 = new System.Windows.Forms.Label();
			this.label12 = new System.Windows.Forms.Label();
			this.numRepPen = new System.Windows.Forms.NumericUpDown();
			this.label11 = new System.Windows.Forms.Label();
			this.numBatchSize = new System.Windows.Forms.NumericUpDown();
			this.label8 = new System.Windows.Forms.Label();
			this.numTopK = new System.Windows.Forms.NumericUpDown();
			this.label9 = new System.Windows.Forms.Label();
			this.numTopP = new System.Windows.Forms.NumericUpDown();
			this.groupCommon = new System.Windows.Forms.GroupBox();
			this.numCtxSize = new System.Windows.Forms.NumericUpDown();
			this.label5 = new System.Windows.Forms.Label();
			this.numGPULayers = new System.Windows.Forms.NumericUpDown();
			this.label7 = new System.Windows.Forms.Label();
			this.label10 = new System.Windows.Forms.Label();
			this.numNGen = new System.Windows.Forms.NumericUpDown();
			this.numTemp = new System.Windows.Forms.NumericUpDown();
			this.label4 = new System.Windows.Forms.Label();
			this.label3 = new System.Windows.Forms.Label();
			this.butBrowse = new System.Windows.Forms.Button();
			this.textModelsPath = new System.Windows.Forms.TextBox();
			this.butApply = new System.Windows.Forms.Button();
			this.label1 = new System.Windows.Forms.Label();
			this.textSystemPrompt = new System.Windows.Forms.TextBox();
			this.tabPage3 = new System.Windows.Forms.TabPage();
			this.splitContainer2 = new System.Windows.Forms.SplitContainer();
			this.listViewModels = new System.Windows.Forms.ListView();
			this.columnHeader1 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
			this.columnHeader2 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
			this.listViewMeta = new System.Windows.Forms.ListView();
			this.columnHeader3 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
			this.columnHeader4 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
			this.checkLoadAuto = new System.Windows.Forms.CheckBox();
			this.butUnload = new System.Windows.Forms.Button();
			this.butLoad = new System.Windows.Forms.Button();
			this.tabPage4 = new System.Windows.Forms.TabPage();
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
			this.tabControl1.SuspendLayout();
			this.tabPage1.SuspendLayout();
			this.tabPage2.SuspendLayout();
			this.groupBox3.SuspendLayout();
			this.groupBox2.SuspendLayout();
			this.groupBox1.SuspendLayout();
			((System.ComponentModel.ISupportInitialize)(this.numFreqThreshold)).BeginInit();
			((System.ComponentModel.ISupportInitialize)(this.numVadThreshold)).BeginInit();
			this.groupCPUParamsBatch.SuspendLayout();
			((System.ComponentModel.ISupportInitialize)(this.numThreadsBatch)).BeginInit();
			this.groupCPUParams.SuspendLayout();
			((System.ComponentModel.ISupportInitialize)(this.numThreads)).BeginInit();
			this.groupAdvanced.SuspendLayout();
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
			((System.ComponentModel.ISupportInitialize)(this.splitContainer2)).BeginInit();
			this.splitContainer2.Panel1.SuspendLayout();
			this.splitContainer2.Panel2.SuspendLayout();
			this.splitContainer2.SuspendLayout();
			this.tabPage4.SuspendLayout();
			((System.ComponentModel.ISupportInitialize)(this.splitContainer3)).BeginInit();
			this.splitContainer3.Panel1.SuspendLayout();
			this.splitContainer3.Panel2.SuspendLayout();
			this.splitContainer3.SuspendLayout();
			this.statusStrip1.SuspendLayout();
			this.SuspendLayout();
			// 
			// textInput
			// 
			this.textInput.AllowDrop = true;
			this.textInput.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.textInput.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.textInput.Location = new System.Drawing.Point(0, 0);
			this.textInput.Margin = new System.Windows.Forms.Padding(0);
			this.textInput.Multiline = true;
			this.textInput.Name = "textInput";
			this.textInput.ScrollBars = System.Windows.Forms.ScrollBars.Both;
			this.textInput.Size = new System.Drawing.Size(1004, 150);
			this.textInput.TabIndex = 0;
			this.textInput.DragDrop += new System.Windows.Forms.DragEventHandler(this.TextInput_DragDrop);
			this.textInput.DragEnter += new System.Windows.Forms.DragEventHandler(this.TextInput_DragEnter);
			this.textInput.KeyDown += new System.Windows.Forms.KeyEventHandler(this.TextInput_KeyDown);
			// 
			// splitContainer1
			// 
			this.splitContainer1.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
			this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
			this.splitContainer1.FixedPanel = System.Windows.Forms.FixedPanel.Panel2;
			this.splitContainer1.Location = new System.Drawing.Point(0, 0);
			this.splitContainer1.Margin = new System.Windows.Forms.Padding(0);
			this.splitContainer1.Name = "splitContainer1";
			this.splitContainer1.Orientation = System.Windows.Forms.Orientation.Horizontal;
			// 
			// splitContainer1.Panel1
			// 
			this.splitContainer1.Panel1.Controls.Add(this.panelChat);
			// 
			// splitContainer1.Panel2
			// 
			this.splitContainer1.Panel2.Controls.Add(this.checkStream);
			this.splitContainer1.Panel2.Controls.Add(this.checkSpeak);
			this.splitContainer1.Panel2.Controls.Add(this.checkVoiceInput);
			this.splitContainer1.Panel2.Controls.Add(this.butCodeBlock);
			this.splitContainer1.Panel2.Controls.Add(this.checkMarkdown);
			this.splitContainer1.Panel2.Controls.Add(this.butReset);
			this.splitContainer1.Panel2.Controls.Add(this.butGen);
			this.splitContainer1.Panel2.Controls.Add(this.textInput);
			this.splitContainer1.Size = new System.Drawing.Size(1008, 949);
			this.splitContainer1.SplitterDistance = 768;
			this.splitContainer1.TabIndex = 1;
			// 
			// panelChat
			// 
			this.panelChat.AutoScroll = true;
			this.panelChat.CausesValidation = false;
			this.panelChat.Dock = System.Windows.Forms.DockStyle.Fill;
			this.panelChat.FlowDirection = System.Windows.Forms.FlowDirection.TopDown;
			this.panelChat.Location = new System.Drawing.Point(0, 0);
			this.panelChat.Name = "panelChat";
			this.panelChat.Size = new System.Drawing.Size(1004, 764);
			this.panelChat.TabIndex = 0;
			this.panelChat.WrapContents = false;
			this.panelChat.Layout += new System.Windows.Forms.LayoutEventHandler(this.PanelChat_Layout);
			// 
			// checkStream
			// 
			this.checkStream.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.checkStream.AutoSize = true;
			this.checkStream.Checked = true;
			this.checkStream.CheckState = System.Windows.Forms.CheckState.Checked;
			this.checkStream.Location = new System.Drawing.Point(106, 154);
			this.checkStream.Name = "checkStream";
			this.checkStream.Size = new System.Drawing.Size(92, 17);
			this.checkStream.TabIndex = 12;
			this.checkStream.Text = "Stream output";
			this.checkStream.UseVisualStyleBackColor = true;
			// 
			// checkSpeak
			// 
			this.checkSpeak.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.checkSpeak.AutoSize = true;
			this.checkSpeak.Location = new System.Drawing.Point(371, 154);
			this.checkSpeak.Name = "checkSpeak";
			this.checkSpeak.Size = new System.Drawing.Size(108, 17);
			this.checkSpeak.TabIndex = 11;
			this.checkSpeak.Text = "Speak responses";
			this.checkSpeak.UseVisualStyleBackColor = true;
			this.checkSpeak.CheckedChanged += new System.EventHandler(this.CheckSpeak_CheckedChanged);
			// 
			// checkVoiceInput
			// 
			this.checkVoiceInput.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.checkVoiceInput.AutoSize = true;
			this.checkVoiceInput.Location = new System.Drawing.Point(286, 154);
			this.checkVoiceInput.Name = "checkVoiceInput";
			this.checkVoiceInput.Size = new System.Drawing.Size(79, 17);
			this.checkVoiceInput.TabIndex = 5;
			this.checkVoiceInput.Text = "Voice input";
			this.checkVoiceInput.ThreeState = true;
			this.checkVoiceInput.UseVisualStyleBackColor = true;
			this.checkVoiceInput.CheckedChanged += new System.EventHandler(this.CheckVoiceInput_CheckedChanged);
			// 
			// butCodeBlock
			// 
			this.butCodeBlock.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.butCodeBlock.Location = new System.Drawing.Point(0, 150);
			this.butCodeBlock.Name = "butCodeBlock";
			this.butCodeBlock.Size = new System.Drawing.Size(100, 23);
			this.butCodeBlock.TabIndex = 4;
			this.butCodeBlock.Text = "Insert code block";
			this.butCodeBlock.UseVisualStyleBackColor = true;
			this.butCodeBlock.Click += new System.EventHandler(this.ButCodeBlock_Click);
			// 
			// checkMarkdown
			// 
			this.checkMarkdown.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.checkMarkdown.AutoSize = true;
			this.checkMarkdown.Checked = true;
			this.checkMarkdown.CheckState = System.Windows.Forms.CheckState.Checked;
			this.checkMarkdown.Location = new System.Drawing.Point(204, 154);
			this.checkMarkdown.Name = "checkMarkdown";
			this.checkMarkdown.Size = new System.Drawing.Size(76, 17);
			this.checkMarkdown.TabIndex = 3;
			this.checkMarkdown.Text = "Markdown";
			this.checkMarkdown.UseVisualStyleBackColor = true;
			this.checkMarkdown.CheckedChanged += new System.EventHandler(this.CheckMarkdown_CheckedChanged);
			// 
			// butReset
			// 
			this.butReset.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.butReset.Enabled = false;
			this.butReset.Location = new System.Drawing.Point(929, 150);
			this.butReset.Margin = new System.Windows.Forms.Padding(0);
			this.butReset.Name = "butReset";
			this.butReset.Size = new System.Drawing.Size(75, 23);
			this.butReset.TabIndex = 2;
			this.butReset.Text = "Reset";
			this.butReset.UseVisualStyleBackColor = true;
			this.butReset.Click += new System.EventHandler(this.ButReset_Click);
			// 
			// butGen
			// 
			this.butGen.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.butGen.Enabled = false;
			this.butGen.Location = new System.Drawing.Point(854, 150);
			this.butGen.Margin = new System.Windows.Forms.Padding(0);
			this.butGen.Name = "butGen";
			this.butGen.Size = new System.Drawing.Size(75, 23);
			this.butGen.TabIndex = 1;
			this.butGen.Text = "Generate";
			this.butGen.UseVisualStyleBackColor = true;
			this.butGen.Click += new System.EventHandler(this.ButGen_Click);
			// 
			// tabControl1
			// 
			this.tabControl1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.tabControl1.Controls.Add(this.tabPage1);
			this.tabControl1.Controls.Add(this.tabPage2);
			this.tabControl1.Controls.Add(this.tabPage3);
			this.tabControl1.Controls.Add(this.tabPage4);
			this.tabControl1.Location = new System.Drawing.Point(0, 0);
			this.tabControl1.Margin = new System.Windows.Forms.Padding(0);
			this.tabControl1.Name = "tabControl1";
			this.tabControl1.SelectedIndex = 0;
			this.tabControl1.Size = new System.Drawing.Size(1016, 975);
			this.tabControl1.TabIndex = 3;
			// 
			// tabPage1
			// 
			this.tabPage1.Controls.Add(this.splitContainer1);
			this.tabPage1.Location = new System.Drawing.Point(4, 22);
			this.tabPage1.Margin = new System.Windows.Forms.Padding(0);
			this.tabPage1.Name = "tabPage1";
			this.tabPage1.Size = new System.Drawing.Size(1008, 949);
			this.tabPage1.TabIndex = 0;
			this.tabPage1.Text = "Chat";
			this.tabPage1.UseVisualStyleBackColor = true;
			// 
			// tabPage2
			// 
			this.tabPage2.Controls.Add(this.groupBox3);
			this.tabPage2.Controls.Add(this.groupBox2);
			this.tabPage2.Controls.Add(this.groupBox1);
			this.tabPage2.Controls.Add(this.groupCPUParamsBatch);
			this.tabPage2.Controls.Add(this.groupCPUParams);
			this.tabPage2.Controls.Add(this.groupAdvanced);
			this.tabPage2.Controls.Add(this.groupCommon);
			this.tabPage2.Controls.Add(this.label3);
			this.tabPage2.Controls.Add(this.butBrowse);
			this.tabPage2.Controls.Add(this.textModelsPath);
			this.tabPage2.Controls.Add(this.butApply);
			this.tabPage2.Controls.Add(this.label1);
			this.tabPage2.Controls.Add(this.textSystemPrompt);
			this.tabPage2.Location = new System.Drawing.Point(4, 22);
			this.tabPage2.Margin = new System.Windows.Forms.Padding(0);
			this.tabPage2.Name = "tabPage2";
			this.tabPage2.Size = new System.Drawing.Size(1008, 949);
			this.tabPage2.TabIndex = 1;
			this.tabPage2.Text = "Settings";
			this.tabPage2.UseVisualStyleBackColor = true;
			// 
			// groupBox3
			// 
			this.groupBox3.Controls.Add(this.checkWebpageFetchEnable);
			this.groupBox3.Location = new System.Drawing.Point(209, 434);
			this.groupBox3.Name = "groupBox3";
			this.groupBox3.Size = new System.Drawing.Size(200, 52);
			this.groupBox3.TabIndex = 39;
			this.groupBox3.TabStop = false;
			this.groupBox3.Text = "Webpage Fetch Tool";
			// 
			// checkWebpageFetchEnable
			// 
			this.checkWebpageFetchEnable.AutoSize = true;
			this.checkWebpageFetchEnable.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
			this.checkWebpageFetchEnable.Location = new System.Drawing.Point(135, 21);
			this.checkWebpageFetchEnable.Name = "checkWebpageFetchEnable";
			this.checkWebpageFetchEnable.Size = new System.Drawing.Size(59, 17);
			this.checkWebpageFetchEnable.TabIndex = 0;
			this.checkWebpageFetchEnable.Text = "Enable";
			this.checkWebpageFetchEnable.UseVisualStyleBackColor = true;
			// 
			// groupBox2
			// 
			this.groupBox2.Controls.Add(this.checkGoogleEnable);
			this.groupBox2.Controls.Add(this.label20);
			this.groupBox2.Controls.Add(this.label19);
			this.groupBox2.Controls.Add(this.textGoogleSearchID);
			this.groupBox2.Controls.Add(this.textGoogleApiKey);
			this.groupBox2.Location = new System.Drawing.Point(209, 336);
			this.groupBox2.Name = "groupBox2";
			this.groupBox2.Size = new System.Drawing.Size(200, 92);
			this.groupBox2.TabIndex = 38;
			this.groupBox2.TabStop = false;
			this.groupBox2.Text = "Google Search Tool";
			// 
			// checkGoogleEnable
			// 
			this.checkGoogleEnable.AutoSize = true;
			this.checkGoogleEnable.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
			this.checkGoogleEnable.Location = new System.Drawing.Point(135, 14);
			this.checkGoogleEnable.Name = "checkGoogleEnable";
			this.checkGoogleEnable.Size = new System.Drawing.Size(59, 17);
			this.checkGoogleEnable.TabIndex = 4;
			this.checkGoogleEnable.Text = "Enable";
			this.checkGoogleEnable.UseVisualStyleBackColor = true;
			// 
			// label20
			// 
			this.label20.AutoSize = true;
			this.label20.Location = new System.Drawing.Point(8, 67);
			this.label20.Name = "label20";
			this.label20.Size = new System.Drawing.Size(93, 13);
			this.label20.TabIndex = 3;
			this.label20.Text = "Search engine ID:";
			// 
			// label19
			// 
			this.label19.AutoSize = true;
			this.label19.Location = new System.Drawing.Point(8, 41);
			this.label19.Name = "label19";
			this.label19.Size = new System.Drawing.Size(48, 13);
			this.label19.TabIndex = 2;
			this.label19.Text = "API Key:";
			// 
			// textGoogleSearchID
			// 
			this.textGoogleSearchID.Location = new System.Drawing.Point(107, 64);
			this.textGoogleSearchID.Name = "textGoogleSearchID";
			this.textGoogleSearchID.Size = new System.Drawing.Size(87, 20);
			this.textGoogleSearchID.TabIndex = 1;
			// 
			// textGoogleApiKey
			// 
			this.textGoogleApiKey.Location = new System.Drawing.Point(62, 38);
			this.textGoogleApiKey.Name = "textGoogleApiKey";
			this.textGoogleApiKey.Size = new System.Drawing.Size(132, 20);
			this.textGoogleApiKey.TabIndex = 0;
			// 
			// groupBox1
			// 
			this.groupBox1.Controls.Add(this.label18);
			this.groupBox1.Controls.Add(this.numFreqThreshold);
			this.groupBox1.Controls.Add(this.label17);
			this.groupBox1.Controls.Add(this.numVadThreshold);
			this.groupBox1.Controls.Add(this.checkWhisperUseGPU);
			this.groupBox1.Controls.Add(this.label16);
			this.groupBox1.Controls.Add(this.textWakeWord);
			this.groupBox1.Controls.Add(this.butWhispDown);
			this.groupBox1.Controls.Add(this.comboWhisperModel);
			this.groupBox1.Controls.Add(this.label15);
			this.groupBox1.Location = new System.Drawing.Point(209, 156);
			this.groupBox1.Name = "groupBox1";
			this.groupBox1.Size = new System.Drawing.Size(200, 174);
			this.groupBox1.TabIndex = 37;
			this.groupBox1.TabStop = false;
			this.groupBox1.Text = "Voice";
			// 
			// label18
			// 
			this.label18.AutoSize = true;
			this.label18.Location = new System.Drawing.Point(8, 129);
			this.label18.Name = "label18";
			this.label18.Size = new System.Drawing.Size(110, 13);
			this.label18.TabIndex = 9;
			this.label18.Text = "Frequency Threshold:";
			// 
			// numFreqThreshold
			// 
			this.numFreqThreshold.Increment = new decimal(new int[] {
            10,
            0,
            0,
            0});
			this.numFreqThreshold.Location = new System.Drawing.Point(124, 127);
			this.numFreqThreshold.Maximum = new decimal(new int[] {
            300,
            0,
            0,
            0});
			this.numFreqThreshold.Name = "numFreqThreshold";
			this.numFreqThreshold.Size = new System.Drawing.Size(70, 20);
			this.numFreqThreshold.TabIndex = 8;
			this.numFreqThreshold.Value = new decimal(new int[] {
            100,
            0,
            0,
            0});
			// 
			// label17
			// 
			this.label17.AutoSize = true;
			this.label17.Location = new System.Drawing.Point(36, 103);
			this.label17.Name = "label17";
			this.label17.Size = new System.Drawing.Size(82, 13);
			this.label17.TabIndex = 7;
			this.label17.Text = "VAD Threshold:";
			// 
			// numVadThreshold
			// 
			this.numVadThreshold.DecimalPlaces = 2;
			this.numVadThreshold.Increment = new decimal(new int[] {
            5,
            0,
            0,
            131072});
			this.numVadThreshold.Location = new System.Drawing.Point(124, 101);
			this.numVadThreshold.Maximum = new decimal(new int[] {
            1,
            0,
            0,
            0});
			this.numVadThreshold.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            65536});
			this.numVadThreshold.Name = "numVadThreshold";
			this.numVadThreshold.Size = new System.Drawing.Size(70, 20);
			this.numVadThreshold.TabIndex = 6;
			this.numVadThreshold.Value = new decimal(new int[] {
            6,
            0,
            0,
            65536});
			// 
			// checkWhisperUseGPU
			// 
			this.checkWhisperUseGPU.AutoSize = true;
			this.checkWhisperUseGPU.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
			this.checkWhisperUseGPU.Location = new System.Drawing.Point(123, 153);
			this.checkWhisperUseGPU.Name = "checkWhisperUseGPU";
			this.checkWhisperUseGPU.Size = new System.Drawing.Size(71, 17);
			this.checkWhisperUseGPU.TabIndex = 5;
			this.checkWhisperUseGPU.Text = "Use GPU";
			this.checkWhisperUseGPU.UseVisualStyleBackColor = true;
			// 
			// label16
			// 
			this.label16.AutoSize = true;
			this.label16.Location = new System.Drawing.Point(6, 78);
			this.label16.Name = "label16";
			this.label16.Size = new System.Drawing.Size(65, 13);
			this.label16.TabIndex = 4;
			this.label16.Text = "Wake word:";
			// 
			// textWakeWord
			// 
			this.textWakeWord.Location = new System.Drawing.Point(77, 75);
			this.textWakeWord.Name = "textWakeWord";
			this.textWakeWord.Size = new System.Drawing.Size(117, 20);
			this.textWakeWord.TabIndex = 3;
			// 
			// butWhispDown
			// 
			this.butWhispDown.Location = new System.Drawing.Point(53, 46);
			this.butWhispDown.Name = "butWhispDown";
			this.butWhispDown.Size = new System.Drawing.Size(141, 23);
			this.butWhispDown.TabIndex = 2;
			this.butWhispDown.Text = "Download Model...";
			this.butWhispDown.UseVisualStyleBackColor = true;
			this.butWhispDown.Click += new System.EventHandler(this.ButWhispDown_Click);
			// 
			// comboWhisperModel
			// 
			this.comboWhisperModel.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
			this.comboWhisperModel.FormattingEnabled = true;
			this.comboWhisperModel.Location = new System.Drawing.Point(53, 19);
			this.comboWhisperModel.Name = "comboWhisperModel";
			this.comboWhisperModel.Size = new System.Drawing.Size(141, 21);
			this.comboWhisperModel.TabIndex = 1;
			this.comboWhisperModel.DropDown += new System.EventHandler(this.ComboWhisperModel_DropDown);
			// 
			// label15
			// 
			this.label15.AutoSize = true;
			this.label15.Location = new System.Drawing.Point(6, 22);
			this.label15.Name = "label15";
			this.label15.Size = new System.Drawing.Size(39, 13);
			this.label15.TabIndex = 0;
			this.label15.Text = "Model:";
			// 
			// groupCPUParamsBatch
			// 
			this.groupCPUParamsBatch.Controls.Add(this.numThreadsBatch);
			this.groupCPUParamsBatch.Controls.Add(this.label14);
			this.groupCPUParamsBatch.Controls.Add(this.checkStrictCPUBatch);
			this.groupCPUParamsBatch.Location = new System.Drawing.Point(3, 230);
			this.groupCPUParamsBatch.Name = "groupCPUParamsBatch";
			this.groupCPUParamsBatch.Size = new System.Drawing.Size(200, 68);
			this.groupCPUParamsBatch.TabIndex = 36;
			this.groupCPUParamsBatch.TabStop = false;
			this.groupCPUParamsBatch.Text = "CPU Params Batch";
			// 
			// numThreadsBatch
			// 
			this.numThreadsBatch.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.numThreadsBatch.Location = new System.Drawing.Point(119, 19);
			this.numThreadsBatch.Maximum = new decimal(new int[] {
            512,
            0,
            0,
            0});
			this.numThreadsBatch.Name = "numThreadsBatch";
			this.numThreadsBatch.Size = new System.Drawing.Size(75, 20);
			this.numThreadsBatch.TabIndex = 30;
			this.numThreadsBatch.Value = new decimal(new int[] {
            8,
            0,
            0,
            0});
			// 
			// label14
			// 
			this.label14.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.label14.AutoSize = true;
			this.label14.Location = new System.Drawing.Point(64, 21);
			this.label14.Name = "label14";
			this.label14.Size = new System.Drawing.Size(49, 13);
			this.label14.TabIndex = 29;
			this.label14.Text = "Threads:";
			// 
			// checkStrictCPUBatch
			// 
			this.checkStrictCPUBatch.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.checkStrictCPUBatch.AutoSize = true;
			this.checkStrictCPUBatch.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
			this.checkStrictCPUBatch.Location = new System.Drawing.Point(17, 45);
			this.checkStrictCPUBatch.Name = "checkStrictCPUBatch";
			this.checkStrictCPUBatch.Size = new System.Drawing.Size(177, 17);
			this.checkStrictCPUBatch.TabIndex = 31;
			this.checkStrictCPUBatch.Text = "Strict CPU placement of threads";
			this.checkStrictCPUBatch.UseVisualStyleBackColor = true;
			// 
			// groupCPUParams
			// 
			this.groupCPUParams.Controls.Add(this.numThreads);
			this.groupCPUParams.Controls.Add(this.label2);
			this.groupCPUParams.Controls.Add(this.checkStrictCPU);
			this.groupCPUParams.Location = new System.Drawing.Point(3, 156);
			this.groupCPUParams.Name = "groupCPUParams";
			this.groupCPUParams.Size = new System.Drawing.Size(200, 68);
			this.groupCPUParams.TabIndex = 35;
			this.groupCPUParams.TabStop = false;
			this.groupCPUParams.Text = "CPU Params";
			// 
			// numThreads
			// 
			this.numThreads.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.numThreads.Location = new System.Drawing.Point(119, 19);
			this.numThreads.Maximum = new decimal(new int[] {
            512,
            0,
            0,
            0});
			this.numThreads.Name = "numThreads";
			this.numThreads.Size = new System.Drawing.Size(75, 20);
			this.numThreads.TabIndex = 3;
			this.numThreads.Value = new decimal(new int[] {
            8,
            0,
            0,
            0});
			// 
			// label2
			// 
			this.label2.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.label2.AutoSize = true;
			this.label2.Location = new System.Drawing.Point(64, 21);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(49, 13);
			this.label2.TabIndex = 2;
			this.label2.Text = "Threads:";
			// 
			// checkStrictCPU
			// 
			this.checkStrictCPU.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.checkStrictCPU.AutoSize = true;
			this.checkStrictCPU.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
			this.checkStrictCPU.Location = new System.Drawing.Point(17, 46);
			this.checkStrictCPU.Name = "checkStrictCPU";
			this.checkStrictCPU.Size = new System.Drawing.Size(177, 17);
			this.checkStrictCPU.TabIndex = 28;
			this.checkStrictCPU.Text = "Strict CPU placement of threads";
			this.checkStrictCPU.UseVisualStyleBackColor = true;
			// 
			// groupAdvanced
			// 
			this.groupAdvanced.Controls.Add(this.checkFlashAttn);
			this.groupAdvanced.Controls.Add(this.checkMLock);
			this.groupAdvanced.Controls.Add(this.checkMMap);
			this.groupAdvanced.Controls.Add(this.comboNUMAStrat);
			this.groupAdvanced.Controls.Add(this.label6);
			this.groupAdvanced.Controls.Add(this.label12);
			this.groupAdvanced.Controls.Add(this.numRepPen);
			this.groupAdvanced.Controls.Add(this.label11);
			this.groupAdvanced.Controls.Add(this.numBatchSize);
			this.groupAdvanced.Controls.Add(this.label8);
			this.groupAdvanced.Controls.Add(this.numTopK);
			this.groupAdvanced.Controls.Add(this.label9);
			this.groupAdvanced.Controls.Add(this.numTopP);
			this.groupAdvanced.Location = new System.Drawing.Point(3, 434);
			this.groupAdvanced.Name = "groupAdvanced";
			this.groupAdvanced.Size = new System.Drawing.Size(200, 218);
			this.groupAdvanced.TabIndex = 34;
			this.groupAdvanced.TabStop = false;
			this.groupAdvanced.Text = "Advanced";
			// 
			// checkFlashAttn
			// 
			this.checkFlashAttn.AutoSize = true;
			this.checkFlashAttn.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
			this.checkFlashAttn.Checked = true;
			this.checkFlashAttn.CheckState = System.Windows.Forms.CheckState.Checked;
			this.checkFlashAttn.Location = new System.Drawing.Point(98, 196);
			this.checkFlashAttn.Name = "checkFlashAttn";
			this.checkFlashAttn.Size = new System.Drawing.Size(96, 17);
			this.checkFlashAttn.TabIndex = 35;
			this.checkFlashAttn.Text = "Flash Attention";
			this.checkFlashAttn.UseVisualStyleBackColor = true;
			// 
			// checkMLock
			// 
			this.checkMLock.AutoSize = true;
			this.checkMLock.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
			this.checkMLock.Location = new System.Drawing.Point(135, 173);
			this.checkMLock.Name = "checkMLock";
			this.checkMLock.Size = new System.Drawing.Size(59, 17);
			this.checkMLock.TabIndex = 34;
			this.checkMLock.Text = "MLock";
			this.checkMLock.UseVisualStyleBackColor = true;
			// 
			// checkMMap
			// 
			this.checkMMap.AutoSize = true;
			this.checkMMap.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
			this.checkMMap.Checked = true;
			this.checkMMap.CheckState = System.Windows.Forms.CheckState.Checked;
			this.checkMMap.Location = new System.Drawing.Point(138, 150);
			this.checkMMap.Name = "checkMMap";
			this.checkMMap.Size = new System.Drawing.Size(56, 17);
			this.checkMMap.TabIndex = 33;
			this.checkMMap.Text = "MMap";
			this.checkMMap.UseVisualStyleBackColor = true;
			// 
			// comboNUMAStrat
			// 
			this.comboNUMAStrat.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.comboNUMAStrat.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
			this.comboNUMAStrat.FormattingEnabled = true;
			this.comboNUMAStrat.Items.AddRange(new object[] {
            "Disabled",
            "Distribute",
            "Isolate",
            "Numactl",
            "Mirror",
            "Count"});
			this.comboNUMAStrat.Location = new System.Drawing.Point(119, 19);
			this.comboNUMAStrat.Name = "comboNUMAStrat";
			this.comboNUMAStrat.Size = new System.Drawing.Size(75, 21);
			this.comboNUMAStrat.TabIndex = 31;
			// 
			// label6
			// 
			this.label6.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.label6.AutoSize = true;
			this.label6.Location = new System.Drawing.Point(31, 48);
			this.label6.Name = "label6";
			this.label6.Size = new System.Drawing.Size(82, 13);
			this.label6.TabIndex = 13;
			this.label6.Text = "Repeat penalty:";
			// 
			// label12
			// 
			this.label12.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.label12.AutoSize = true;
			this.label12.Location = new System.Drawing.Point(29, 22);
			this.label12.Name = "label12";
			this.label12.Size = new System.Drawing.Size(84, 13);
			this.label12.TabIndex = 32;
			this.label12.Text = "NUMA Strategy:";
			// 
			// numRepPen
			// 
			this.numRepPen.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.numRepPen.DecimalPlaces = 2;
			this.numRepPen.Increment = new decimal(new int[] {
            1,
            0,
            0,
            131072});
			this.numRepPen.Location = new System.Drawing.Point(119, 46);
			this.numRepPen.Maximum = new decimal(new int[] {
            10,
            0,
            0,
            0});
			this.numRepPen.Name = "numRepPen";
			this.numRepPen.Size = new System.Drawing.Size(75, 20);
			this.numRepPen.TabIndex = 14;
			this.numRepPen.Value = new decimal(new int[] {
            11,
            0,
            0,
            65536});
			// 
			// label11
			// 
			this.label11.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.label11.AutoSize = true;
			this.label11.Location = new System.Drawing.Point(54, 126);
			this.label11.Name = "label11";
			this.label11.Size = new System.Drawing.Size(59, 13);
			this.label11.TabIndex = 30;
			this.label11.Text = "Batch size:";
			// 
			// numBatchSize
			// 
			this.numBatchSize.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.numBatchSize.Location = new System.Drawing.Point(119, 124);
			this.numBatchSize.Maximum = new decimal(new int[] {
            1048576,
            0,
            0,
            0});
			this.numBatchSize.Name = "numBatchSize";
			this.numBatchSize.Size = new System.Drawing.Size(75, 20);
			this.numBatchSize.TabIndex = 29;
			this.numBatchSize.Value = new decimal(new int[] {
            512,
            0,
            0,
            0});
			// 
			// label8
			// 
			this.label8.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.label8.AutoSize = true;
			this.label8.Location = new System.Drawing.Point(74, 74);
			this.label8.Name = "label8";
			this.label8.Size = new System.Drawing.Size(39, 13);
			this.label8.TabIndex = 17;
			this.label8.Text = "Top K:";
			// 
			// numTopK
			// 
			this.numTopK.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.numTopK.Location = new System.Drawing.Point(119, 72);
			this.numTopK.Maximum = new decimal(new int[] {
            100000,
            0,
            0,
            0});
			this.numTopK.Name = "numTopK";
			this.numTopK.Size = new System.Drawing.Size(75, 20);
			this.numTopK.TabIndex = 18;
			this.numTopK.Value = new decimal(new int[] {
            40,
            0,
            0,
            0});
			// 
			// label9
			// 
			this.label9.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.label9.AutoSize = true;
			this.label9.Location = new System.Drawing.Point(74, 100);
			this.label9.Name = "label9";
			this.label9.Size = new System.Drawing.Size(39, 13);
			this.label9.TabIndex = 19;
			this.label9.Text = "Top P:";
			// 
			// numTopP
			// 
			this.numTopP.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.numTopP.DecimalPlaces = 2;
			this.numTopP.Increment = new decimal(new int[] {
            1,
            0,
            0,
            131072});
			this.numTopP.Location = new System.Drawing.Point(119, 98);
			this.numTopP.Maximum = new decimal(new int[] {
            1,
            0,
            0,
            0});
			this.numTopP.Name = "numTopP";
			this.numTopP.Size = new System.Drawing.Size(75, 20);
			this.numTopP.TabIndex = 20;
			this.numTopP.Value = new decimal(new int[] {
            95,
            0,
            0,
            131072});
			// 
			// groupCommon
			// 
			this.groupCommon.Controls.Add(this.numCtxSize);
			this.groupCommon.Controls.Add(this.label5);
			this.groupCommon.Controls.Add(this.numGPULayers);
			this.groupCommon.Controls.Add(this.label7);
			this.groupCommon.Controls.Add(this.label10);
			this.groupCommon.Controls.Add(this.numNGen);
			this.groupCommon.Controls.Add(this.numTemp);
			this.groupCommon.Controls.Add(this.label4);
			this.groupCommon.Location = new System.Drawing.Point(3, 304);
			this.groupCommon.Name = "groupCommon";
			this.groupCommon.Size = new System.Drawing.Size(200, 124);
			this.groupCommon.TabIndex = 33;
			this.groupCommon.TabStop = false;
			this.groupCommon.Text = "Common";
			// 
			// numCtxSize
			// 
			this.numCtxSize.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.numCtxSize.Location = new System.Drawing.Point(119, 19);
			this.numCtxSize.Maximum = new decimal(new int[] {
            1048576,
            0,
            0,
            0});
			this.numCtxSize.Name = "numCtxSize";
			this.numCtxSize.Size = new System.Drawing.Size(75, 20);
			this.numCtxSize.TabIndex = 24;
			this.numCtxSize.Value = new decimal(new int[] {
            4096,
            0,
            0,
            0});
			// 
			// label5
			// 
			this.label5.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.label5.AutoSize = true;
			this.label5.Location = new System.Drawing.Point(46, 21);
			this.label5.Name = "label5";
			this.label5.Size = new System.Drawing.Size(67, 13);
			this.label5.TabIndex = 25;
			this.label5.Text = "Context size:";
			// 
			// numGPULayers
			// 
			this.numGPULayers.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.numGPULayers.Location = new System.Drawing.Point(119, 45);
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
			this.numGPULayers.Size = new System.Drawing.Size(75, 20);
			this.numGPULayers.TabIndex = 26;
			this.numGPULayers.Value = new decimal(new int[] {
            1,
            0,
            0,
            -2147483648});
			// 
			// label7
			// 
			this.label7.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.label7.AutoSize = true;
			this.label7.Location = new System.Drawing.Point(43, 73);
			this.label7.Name = "label7";
			this.label7.Size = new System.Drawing.Size(70, 13);
			this.label7.TabIndex = 15;
			this.label7.Text = "Temperature:";
			// 
			// label10
			// 
			this.label10.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.label10.AutoSize = true;
			this.label10.Location = new System.Drawing.Point(50, 47);
			this.label10.Name = "label10";
			this.label10.Size = new System.Drawing.Size(63, 13);
			this.label10.TabIndex = 27;
			this.label10.Text = "GPU layers:";
			// 
			// numNGen
			// 
			this.numNGen.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.numNGen.Location = new System.Drawing.Point(119, 97);
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
			this.numNGen.Size = new System.Drawing.Size(75, 20);
			this.numNGen.TabIndex = 23;
			this.numNGen.Value = new decimal(new int[] {
            1,
            0,
            0,
            -2147483648});
			// 
			// numTemp
			// 
			this.numTemp.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.numTemp.DecimalPlaces = 2;
			this.numTemp.Increment = new decimal(new int[] {
            1,
            0,
            0,
            65536});
			this.numTemp.Location = new System.Drawing.Point(119, 71);
			this.numTemp.Maximum = new decimal(new int[] {
            1,
            0,
            0,
            0});
			this.numTemp.Name = "numTemp";
			this.numTemp.Size = new System.Drawing.Size(75, 20);
			this.numTemp.TabIndex = 16;
			this.numTemp.Value = new decimal(new int[] {
            7,
            0,
            0,
            65536});
			// 
			// label4
			// 
			this.label4.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.label4.AutoSize = true;
			this.label4.Location = new System.Drawing.Point(10, 99);
			this.label4.Name = "label4";
			this.label4.Size = new System.Drawing.Size(103, 13);
			this.label4.TabIndex = 22;
			this.label4.Text = "Tokens to generate:";
			// 
			// label3
			// 
			this.label3.AutoSize = true;
			this.label3.Location = new System.Drawing.Point(3, 133);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(73, 13);
			this.label3.TabIndex = 21;
			this.label3.Text = "Models folder:";
			// 
			// butBrowse
			// 
			this.butBrowse.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.butBrowse.Location = new System.Drawing.Point(930, 128);
			this.butBrowse.Name = "butBrowse";
			this.butBrowse.Size = new System.Drawing.Size(75, 23);
			this.butBrowse.TabIndex = 12;
			this.butBrowse.Text = "Browse";
			this.butBrowse.UseVisualStyleBackColor = true;
			this.butBrowse.Click += new System.EventHandler(this.ButBrowse_Click);
			// 
			// textModelsPath
			// 
			this.textModelsPath.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.textModelsPath.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.Suggest;
			this.textModelsPath.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.FileSystem;
			this.textModelsPath.Location = new System.Drawing.Point(82, 130);
			this.textModelsPath.Name = "textModelsPath";
			this.textModelsPath.Size = new System.Drawing.Size(842, 20);
			this.textModelsPath.TabIndex = 11;
			this.textModelsPath.KeyDown += new System.Windows.Forms.KeyEventHandler(this.TextModelsPath_KeyDown);
			// 
			// butApply
			// 
			this.butApply.Location = new System.Drawing.Point(3, 658);
			this.butApply.Name = "butApply";
			this.butApply.Size = new System.Drawing.Size(75, 23);
			this.butApply.TabIndex = 4;
			this.butApply.Text = "Apply";
			this.butApply.UseVisualStyleBackColor = true;
			this.butApply.Click += new System.EventHandler(this.ButApply_Click);
			// 
			// label1
			// 
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(3, 3);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(59, 13);
			this.label1.TabIndex = 1;
			this.label1.Text = "Instruction:";
			// 
			// textSystemPrompt
			// 
			this.textSystemPrompt.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.textSystemPrompt.Location = new System.Drawing.Point(0, 16);
			this.textSystemPrompt.Margin = new System.Windows.Forms.Padding(0);
			this.textSystemPrompt.Multiline = true;
			this.textSystemPrompt.Name = "textSystemPrompt";
			this.textSystemPrompt.Size = new System.Drawing.Size(1008, 109);
			this.textSystemPrompt.TabIndex = 0;
			// 
			// tabPage3
			// 
			this.tabPage3.Controls.Add(this.splitContainer2);
			this.tabPage3.Controls.Add(this.checkLoadAuto);
			this.tabPage3.Controls.Add(this.butUnload);
			this.tabPage3.Controls.Add(this.butLoad);
			this.tabPage3.Location = new System.Drawing.Point(4, 22);
			this.tabPage3.Margin = new System.Windows.Forms.Padding(0);
			this.tabPage3.Name = "tabPage3";
			this.tabPage3.Size = new System.Drawing.Size(1008, 949);
			this.tabPage3.TabIndex = 2;
			this.tabPage3.Text = "Models";
			this.tabPage3.UseVisualStyleBackColor = true;
			// 
			// splitContainer2
			// 
			this.splitContainer2.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.splitContainer2.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
			this.splitContainer2.Location = new System.Drawing.Point(3, 0);
			this.splitContainer2.Name = "splitContainer2";
			this.splitContainer2.Orientation = System.Windows.Forms.Orientation.Horizontal;
			// 
			// splitContainer2.Panel1
			// 
			this.splitContainer2.Panel1.Controls.Add(this.listViewModels);
			// 
			// splitContainer2.Panel2
			// 
			this.splitContainer2.Panel2.Controls.Add(this.listViewMeta);
			this.splitContainer2.Size = new System.Drawing.Size(1005, 923);
			this.splitContainer2.SplitterDistance = 461;
			this.splitContainer2.TabIndex = 4;
			// 
			// listViewModels
			// 
			this.listViewModels.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.columnHeader1,
            this.columnHeader2});
			this.listViewModels.Dock = System.Windows.Forms.DockStyle.Fill;
			this.listViewModels.GridLines = true;
			this.listViewModels.HideSelection = false;
			this.listViewModels.Location = new System.Drawing.Point(0, 0);
			this.listViewModels.Margin = new System.Windows.Forms.Padding(0);
			this.listViewModels.MultiSelect = false;
			this.listViewModels.Name = "listViewModels";
			this.listViewModels.Size = new System.Drawing.Size(1001, 457);
			this.listViewModels.TabIndex = 0;
			this.listViewModels.UseCompatibleStateImageBehavior = false;
			this.listViewModels.View = System.Windows.Forms.View.Details;
			this.listViewModels.ItemSelectionChanged += new System.Windows.Forms.ListViewItemSelectionChangedEventHandler(this.ListViewModels_ItemSelectionChanged);
			this.listViewModels.DoubleClick += new System.EventHandler(this.ListViewModels_DoubleClick);
			this.listViewModels.KeyDown += new System.Windows.Forms.KeyEventHandler(this.ListViewModels_KeyDown);
			// 
			// columnHeader1
			// 
			this.columnHeader1.Text = "Name";
			this.columnHeader1.Width = 250;
			// 
			// columnHeader2
			// 
			this.columnHeader2.Text = "Full Path";
			this.columnHeader2.Width = 746;
			// 
			// listViewMeta
			// 
			this.listViewMeta.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.columnHeader3,
            this.columnHeader4});
			this.listViewMeta.Dock = System.Windows.Forms.DockStyle.Fill;
			this.listViewMeta.GridLines = true;
			this.listViewMeta.HideSelection = false;
			this.listViewMeta.Location = new System.Drawing.Point(0, 0);
			this.listViewMeta.MultiSelect = false;
			this.listViewMeta.Name = "listViewMeta";
			this.listViewMeta.Size = new System.Drawing.Size(1001, 454);
			this.listViewMeta.TabIndex = 0;
			this.listViewMeta.UseCompatibleStateImageBehavior = false;
			this.listViewMeta.View = System.Windows.Forms.View.Details;
			// 
			// columnHeader3
			// 
			this.columnHeader3.Text = "Metadata Entry";
			this.columnHeader3.Width = 250;
			// 
			// columnHeader4
			// 
			this.columnHeader4.Text = "Value";
			this.columnHeader4.Width = 746;
			// 
			// checkLoadAuto
			// 
			this.checkLoadAuto.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.checkLoadAuto.AutoSize = true;
			this.checkLoadAuto.Location = new System.Drawing.Point(3, 930);
			this.checkLoadAuto.Name = "checkLoadAuto";
			this.checkLoadAuto.Size = new System.Drawing.Size(147, 17);
			this.checkLoadAuto.TabIndex = 3;
			this.checkLoadAuto.Text = "Load last model at startup";
			this.checkLoadAuto.UseVisualStyleBackColor = true;
			this.checkLoadAuto.CheckedChanged += new System.EventHandler(this.CheckLoadAuto_CheckedChanged);
			// 
			// butUnload
			// 
			this.butUnload.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.butUnload.Enabled = false;
			this.butUnload.Location = new System.Drawing.Point(933, 926);
			this.butUnload.Margin = new System.Windows.Forms.Padding(0);
			this.butUnload.Name = "butUnload";
			this.butUnload.Size = new System.Drawing.Size(75, 23);
			this.butUnload.TabIndex = 2;
			this.butUnload.Text = "Unload";
			this.butUnload.UseVisualStyleBackColor = true;
			this.butUnload.Click += new System.EventHandler(this.ButUnload_Click);
			// 
			// butLoad
			// 
			this.butLoad.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.butLoad.Location = new System.Drawing.Point(858, 926);
			this.butLoad.Margin = new System.Windows.Forms.Padding(0);
			this.butLoad.Name = "butLoad";
			this.butLoad.Size = new System.Drawing.Size(75, 23);
			this.butLoad.TabIndex = 1;
			this.butLoad.Text = "Load";
			this.butLoad.UseVisualStyleBackColor = true;
			this.butLoad.Click += new System.EventHandler(this.ButLoad_Click);
			// 
			// tabPage4
			// 
			this.tabPage4.Controls.Add(this.splitContainer3);
			this.tabPage4.Location = new System.Drawing.Point(4, 22);
			this.tabPage4.Name = "tabPage4";
			this.tabPage4.Padding = new System.Windows.Forms.Padding(3);
			this.tabPage4.Size = new System.Drawing.Size(1008, 949);
			this.tabPage4.TabIndex = 3;
			this.tabPage4.Text = "Huggingface";
			this.tabPage4.UseVisualStyleBackColor = true;
			// 
			// splitContainer3
			// 
			this.splitContainer3.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.splitContainer3.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
			this.splitContainer3.Location = new System.Drawing.Point(3, 3);
			this.splitContainer3.Name = "splitContainer3";
			this.splitContainer3.Orientation = System.Windows.Forms.Orientation.Horizontal;
			// 
			// splitContainer3.Panel1
			// 
			this.splitContainer3.Panel1.Controls.Add(this.butSearch);
			this.splitContainer3.Panel1.Controls.Add(this.textSearchTerm);
			this.splitContainer3.Panel1.Controls.Add(this.label13);
			this.splitContainer3.Panel1.Controls.Add(this.listViewHugSearch);
			// 
			// splitContainer3.Panel2
			// 
			this.splitContainer3.Panel2.Controls.Add(this.progressBar1);
			this.splitContainer3.Panel2.Controls.Add(this.butDownload);
			this.splitContainer3.Panel2.Controls.Add(this.listViewHugFiles);
			this.splitContainer3.Size = new System.Drawing.Size(1002, 943);
			this.splitContainer3.SplitterDistance = 469;
			this.splitContainer3.TabIndex = 2;
			// 
			// butSearch
			// 
			this.butSearch.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.butSearch.Location = new System.Drawing.Point(923, 0);
			this.butSearch.Margin = new System.Windows.Forms.Padding(0);
			this.butSearch.Name = "butSearch";
			this.butSearch.Size = new System.Drawing.Size(75, 23);
			this.butSearch.TabIndex = 3;
			this.butSearch.Text = "Go";
			this.butSearch.UseVisualStyleBackColor = true;
			this.butSearch.Click += new System.EventHandler(this.ButSearch_Click);
			// 
			// textSearchTerm
			// 
			this.textSearchTerm.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.textSearchTerm.Location = new System.Drawing.Point(84, 2);
			this.textSearchTerm.Margin = new System.Windows.Forms.Padding(0);
			this.textSearchTerm.Name = "textSearchTerm";
			this.textSearchTerm.Size = new System.Drawing.Size(839, 20);
			this.textSearchTerm.TabIndex = 2;
			this.textSearchTerm.KeyDown += new System.Windows.Forms.KeyEventHandler(this.TextSearchTerm_KeyDown);
			// 
			// label13
			// 
			this.label13.AutoSize = true;
			this.label13.Location = new System.Drawing.Point(1, 5);
			this.label13.Name = "label13";
			this.label13.Size = new System.Drawing.Size(80, 13);
			this.label13.TabIndex = 1;
			this.label13.Text = "Search models:";
			// 
			// listViewHugSearch
			// 
			this.listViewHugSearch.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
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
			this.listViewHugSearch.Location = new System.Drawing.Point(0, 24);
			this.listViewHugSearch.Margin = new System.Windows.Forms.Padding(0);
			this.listViewHugSearch.Name = "listViewHugSearch";
			this.listViewHugSearch.Size = new System.Drawing.Size(998, 441);
			this.listViewHugSearch.TabIndex = 0;
			this.listViewHugSearch.UseCompatibleStateImageBehavior = false;
			this.listViewHugSearch.View = System.Windows.Forms.View.Details;
			this.listViewHugSearch.SelectedIndexChanged += new System.EventHandler(this.ListViewHugSearch_SelectedIndexChanged);
			// 
			// columnHeader5
			// 
			this.columnHeader5.Text = "Name";
			this.columnHeader5.Width = 250;
			// 
			// columnHeader6
			// 
			this.columnHeader6.Text = "Uploader";
			this.columnHeader6.Width = 230;
			// 
			// columnHeader7
			// 
			this.columnHeader7.Text = "Likes";
			this.columnHeader7.Width = 85;
			// 
			// columnHeader11
			// 
			this.columnHeader11.Text = "Downloads";
			this.columnHeader11.Width = 86;
			// 
			// columnHeader12
			// 
			this.columnHeader12.Text = "Trending Score";
			this.columnHeader12.Width = 87;
			// 
			// columnHeader13
			// 
			this.columnHeader13.Text = "Created";
			this.columnHeader13.Width = 120;
			// 
			// columnHeader14
			// 
			this.columnHeader14.Text = "Modified";
			this.columnHeader14.Width = 120;
			// 
			// progressBar1
			// 
			this.progressBar1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.progressBar1.Location = new System.Drawing.Point(0, 443);
			this.progressBar1.Name = "progressBar1";
			this.progressBar1.Size = new System.Drawing.Size(923, 23);
			this.progressBar1.TabIndex = 3;
			// 
			// butDownload
			// 
			this.butDownload.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.butDownload.Location = new System.Drawing.Point(923, 443);
			this.butDownload.Margin = new System.Windows.Forms.Padding(0);
			this.butDownload.Name = "butDownload";
			this.butDownload.Size = new System.Drawing.Size(75, 23);
			this.butDownload.TabIndex = 2;
			this.butDownload.Text = "Download";
			this.butDownload.UseVisualStyleBackColor = true;
			this.butDownload.Click += new System.EventHandler(this.ButDownload_Click);
			// 
			// listViewHugFiles
			// 
			this.listViewHugFiles.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.listViewHugFiles.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.columnHeader8,
            this.columnHeader10});
			this.listViewHugFiles.GridLines = true;
			this.listViewHugFiles.HideSelection = false;
			this.listViewHugFiles.Location = new System.Drawing.Point(0, 0);
			this.listViewHugFiles.Margin = new System.Windows.Forms.Padding(0);
			this.listViewHugFiles.Name = "listViewHugFiles";
			this.listViewHugFiles.Size = new System.Drawing.Size(998, 443);
			this.listViewHugFiles.TabIndex = 1;
			this.listViewHugFiles.UseCompatibleStateImageBehavior = false;
			this.listViewHugFiles.View = System.Windows.Forms.View.Details;
			// 
			// columnHeader8
			// 
			this.columnHeader8.Text = "Filename";
			this.columnHeader8.Width = 878;
			// 
			// columnHeader10
			// 
			this.columnHeader10.Text = "Size";
			this.columnHeader10.Width = 100;
			// 
			// statusStrip1
			// 
			this.statusStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripStatusLabel1,
            this.labelTokens,
            this.labelTPS,
            this.labelPreGen});
			this.statusStrip1.LayoutStyle = System.Windows.Forms.ToolStripLayoutStyle.HorizontalStackWithOverflow;
			this.statusStrip1.Location = new System.Drawing.Point(0, 975);
			this.statusStrip1.Name = "statusStrip1";
			this.statusStrip1.Size = new System.Drawing.Size(1016, 22);
			this.statusStrip1.TabIndex = 4;
			this.statusStrip1.Text = "statusStrip1";
			// 
			// toolStripStatusLabel1
			// 
			this.toolStripStatusLabel1.Name = "toolStripStatusLabel1";
			this.toolStripStatusLabel1.Size = new System.Drawing.Size(86, 17);
			this.toolStripStatusLabel1.Text = "No model loaded";
			// 
			// labelTokens
			// 
			this.labelTokens.BorderSides = System.Windows.Forms.ToolStripStatusLabelBorderSides.Left;
			this.labelTokens.BorderStyle = System.Windows.Forms.Border3DStyle.Etched;
			this.labelTokens.Name = "labelTokens";
			this.labelTokens.Size = new System.Drawing.Size(54, 17);
			this.labelTokens.Text = "0 Tokens";
			// 
			// labelTPS
			// 
			this.labelTPS.BorderSides = System.Windows.Forms.ToolStripStatusLabelBorderSides.Left;
			this.labelTPS.BorderStyle = System.Windows.Forms.Border3DStyle.Etched;
			this.labelTPS.Name = "labelTPS";
			this.labelTPS.Size = new System.Drawing.Size(46, 17);
			this.labelTPS.Text = "0 Tok/s";
			// 
			// labelPreGen
			// 
			this.labelPreGen.BorderSides = System.Windows.Forms.ToolStripStatusLabelBorderSides.Left;
			this.labelPreGen.BorderStyle = System.Windows.Forms.Border3DStyle.Etched;
			this.labelPreGen.Name = "labelPreGen";
			this.labelPreGen.Size = new System.Drawing.Size(110, 17);
			this.labelPreGen.Text = "Pre-generation time:";
			// 
			// toolTip1
			// 
			this.toolTip1.AutoPopDelay = 10000;
			this.toolTip1.InitialDelay = 500;
			this.toolTip1.ReshowDelay = 0;
			this.toolTip1.UseAnimation = false;
			this.toolTip1.UseFading = false;
			// 
			// Form1
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
			this.AutoValidate = System.Windows.Forms.AutoValidate.Disable;
			this.ClientSize = new System.Drawing.Size(1016, 997);
			this.Controls.Add(this.statusStrip1);
			this.Controls.Add(this.tabControl1);
			this.DoubleBuffered = true;
			this.KeyPreview = true;
			this.Name = "Form1";
			this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
			this.Text = "LM Stud";
			this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.Form1_FormClosing);
			this.Load += new System.EventHandler(this.Form1_Load);
			this.KeyDown += new System.Windows.Forms.KeyEventHandler(this.Form1_KeyDown);
			this.splitContainer1.Panel1.ResumeLayout(false);
			this.splitContainer1.Panel2.ResumeLayout(false);
			this.splitContainer1.Panel2.PerformLayout();
			((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
			this.splitContainer1.ResumeLayout(false);
			this.tabControl1.ResumeLayout(false);
			this.tabPage1.ResumeLayout(false);
			this.tabPage2.ResumeLayout(false);
			this.tabPage2.PerformLayout();
			this.groupBox3.ResumeLayout(false);
			this.groupBox3.PerformLayout();
			this.groupBox2.ResumeLayout(false);
			this.groupBox2.PerformLayout();
			this.groupBox1.ResumeLayout(false);
			this.groupBox1.PerformLayout();
			((System.ComponentModel.ISupportInitialize)(this.numFreqThreshold)).EndInit();
			((System.ComponentModel.ISupportInitialize)(this.numVadThreshold)).EndInit();
			this.groupCPUParamsBatch.ResumeLayout(false);
			this.groupCPUParamsBatch.PerformLayout();
			((System.ComponentModel.ISupportInitialize)(this.numThreadsBatch)).EndInit();
			this.groupCPUParams.ResumeLayout(false);
			this.groupCPUParams.PerformLayout();
			((System.ComponentModel.ISupportInitialize)(this.numThreads)).EndInit();
			this.groupAdvanced.ResumeLayout(false);
			this.groupAdvanced.PerformLayout();
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
			this.splitContainer2.Panel1.ResumeLayout(false);
			this.splitContainer2.Panel2.ResumeLayout(false);
			((System.ComponentModel.ISupportInitialize)(this.splitContainer2)).EndInit();
			this.splitContainer2.ResumeLayout(false);
			this.tabPage4.ResumeLayout(false);
			this.splitContainer3.Panel1.ResumeLayout(false);
			this.splitContainer3.Panel1.PerformLayout();
			this.splitContainer3.Panel2.ResumeLayout(false);
			((System.ComponentModel.ISupportInitialize)(this.splitContainer3)).EndInit();
			this.splitContainer3.ResumeLayout(false);
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
		private System.Windows.Forms.CheckBox checkStrictCPU;
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
		private System.Windows.Forms.CheckBox checkStrictCPUBatch;
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
	}
}

