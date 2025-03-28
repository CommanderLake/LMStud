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
			this.textInput = new System.Windows.Forms.TextBox();
			this.splitContainer1 = new System.Windows.Forms.SplitContainer();
			this.panelChat = new LMStud.MyFlowLayoutPanel();
			this.butCodeBlock = new System.Windows.Forms.Button();
			this.checkMarkdown = new System.Windows.Forms.CheckBox();
			this.butReset = new System.Windows.Forms.Button();
			this.butGen = new System.Windows.Forms.Button();
			this.tabControl1 = new System.Windows.Forms.TabControl();
			this.tabPage1 = new System.Windows.Forms.TabPage();
			this.tabPage2 = new System.Windows.Forms.TabPage();
			this.groupBox2 = new System.Windows.Forms.GroupBox();
			this.comboNUMAStrat = new System.Windows.Forms.ComboBox();
			this.label6 = new System.Windows.Forms.Label();
			this.checkStrictCPU = new System.Windows.Forms.CheckBox();
			this.label12 = new System.Windows.Forms.Label();
			this.numRepPen = new System.Windows.Forms.NumericUpDown();
			this.label11 = new System.Windows.Forms.Label();
			this.numBatchSize = new System.Windows.Forms.NumericUpDown();
			this.label8 = new System.Windows.Forms.Label();
			this.numTopK = new System.Windows.Forms.NumericUpDown();
			this.label9 = new System.Windows.Forms.Label();
			this.numTopP = new System.Windows.Forms.NumericUpDown();
			this.groupBox1 = new System.Windows.Forms.GroupBox();
			this.numThreads = new System.Windows.Forms.NumericUpDown();
			this.label2 = new System.Windows.Forms.Label();
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
			this.textInstruction = new System.Windows.Forms.TextBox();
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
			this.listViewModelSearch = new System.Windows.Forms.ListView();
			this.columnHeader5 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
			this.columnHeader6 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
			this.columnHeader7 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
			this.columnHeader11 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
			this.columnHeader12 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
			this.columnHeader13 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
			this.columnHeader14 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
			this.progressBar1 = new System.Windows.Forms.ProgressBar();
			this.butDownload = new System.Windows.Forms.Button();
			this.listViewQuants = new System.Windows.Forms.ListView();
			this.columnHeader8 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
			this.columnHeader9 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
			this.columnHeader10 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
			this.folderBrowserDialog1 = new System.Windows.Forms.FolderBrowserDialog();
			this.statusStrip1 = new System.Windows.Forms.StatusStrip();
			this.toolStripStatusLabel1 = new System.Windows.Forms.ToolStripStatusLabel();
			this.labelTokens = new System.Windows.Forms.ToolStripStatusLabel();
			this.labelTPS = new System.Windows.Forms.ToolStripStatusLabel();
			((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
			this.splitContainer1.Panel1.SuspendLayout();
			this.splitContainer1.Panel2.SuspendLayout();
			this.splitContainer1.SuspendLayout();
			this.tabControl1.SuspendLayout();
			this.tabPage1.SuspendLayout();
			this.tabPage2.SuspendLayout();
			this.groupBox2.SuspendLayout();
			((System.ComponentModel.ISupportInitialize)(this.numRepPen)).BeginInit();
			((System.ComponentModel.ISupportInitialize)(this.numBatchSize)).BeginInit();
			((System.ComponentModel.ISupportInitialize)(this.numTopK)).BeginInit();
			((System.ComponentModel.ISupportInitialize)(this.numTopP)).BeginInit();
			this.groupBox1.SuspendLayout();
			((System.ComponentModel.ISupportInitialize)(this.numThreads)).BeginInit();
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
			this.checkMarkdown.Location = new System.Drawing.Point(106, 154);
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
			this.tabPage2.Controls.Add(this.groupBox2);
			this.tabPage2.Controls.Add(this.groupBox1);
			this.tabPage2.Controls.Add(this.label3);
			this.tabPage2.Controls.Add(this.butBrowse);
			this.tabPage2.Controls.Add(this.textModelsPath);
			this.tabPage2.Controls.Add(this.butApply);
			this.tabPage2.Controls.Add(this.label1);
			this.tabPage2.Controls.Add(this.textInstruction);
			this.tabPage2.Location = new System.Drawing.Point(4, 22);
			this.tabPage2.Margin = new System.Windows.Forms.Padding(0);
			this.tabPage2.Name = "tabPage2";
			this.tabPage2.Size = new System.Drawing.Size(1008, 949);
			this.tabPage2.TabIndex = 1;
			this.tabPage2.Text = "Settings";
			this.tabPage2.UseVisualStyleBackColor = true;
			// 
			// groupBox2
			// 
			this.groupBox2.Controls.Add(this.comboNUMAStrat);
			this.groupBox2.Controls.Add(this.label6);
			this.groupBox2.Controls.Add(this.checkStrictCPU);
			this.groupBox2.Controls.Add(this.label12);
			this.groupBox2.Controls.Add(this.numRepPen);
			this.groupBox2.Controls.Add(this.label11);
			this.groupBox2.Controls.Add(this.numBatchSize);
			this.groupBox2.Controls.Add(this.label8);
			this.groupBox2.Controls.Add(this.numTopK);
			this.groupBox2.Controls.Add(this.label9);
			this.groupBox2.Controls.Add(this.numTopP);
			this.groupBox2.Location = new System.Drawing.Point(6, 320);
			this.groupBox2.Name = "groupBox2";
			this.groupBox2.Size = new System.Drawing.Size(200, 174);
			this.groupBox2.TabIndex = 34;
			this.groupBox2.TabStop = false;
			this.groupBox2.Text = "Advanced";
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
			// checkStrictCPU
			// 
			this.checkStrictCPU.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.checkStrictCPU.AutoSize = true;
			this.checkStrictCPU.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
			this.checkStrictCPU.Location = new System.Drawing.Point(17, 150);
			this.checkStrictCPU.Name = "checkStrictCPU";
			this.checkStrictCPU.Size = new System.Drawing.Size(177, 17);
			this.checkStrictCPU.TabIndex = 28;
			this.checkStrictCPU.Text = "Strict CPU placement of threads";
			this.checkStrictCPU.UseVisualStyleBackColor = true;
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
			// groupBox1
			// 
			this.groupBox1.Controls.Add(this.numThreads);
			this.groupBox1.Controls.Add(this.label2);
			this.groupBox1.Controls.Add(this.numCtxSize);
			this.groupBox1.Controls.Add(this.label5);
			this.groupBox1.Controls.Add(this.numGPULayers);
			this.groupBox1.Controls.Add(this.label7);
			this.groupBox1.Controls.Add(this.label10);
			this.groupBox1.Controls.Add(this.numNGen);
			this.groupBox1.Controls.Add(this.numTemp);
			this.groupBox1.Controls.Add(this.label4);
			this.groupBox1.Location = new System.Drawing.Point(6, 156);
			this.groupBox1.Name = "groupBox1";
			this.groupBox1.Size = new System.Drawing.Size(200, 158);
			this.groupBox1.TabIndex = 33;
			this.groupBox1.TabStop = false;
			this.groupBox1.Text = "Common";
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
			// numCtxSize
			// 
			this.numCtxSize.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.numCtxSize.Location = new System.Drawing.Point(119, 45);
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
			this.label5.Location = new System.Drawing.Point(46, 47);
			this.label5.Name = "label5";
			this.label5.Size = new System.Drawing.Size(67, 13);
			this.label5.TabIndex = 25;
			this.label5.Text = "Context size:";
			// 
			// numGPULayers
			// 
			this.numGPULayers.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.numGPULayers.Location = new System.Drawing.Point(119, 71);
			this.numGPULayers.Maximum = new decimal(new int[] {
            1024,
            0,
            0,
            0});
			this.numGPULayers.Name = "numGPULayers";
			this.numGPULayers.Size = new System.Drawing.Size(75, 20);
			this.numGPULayers.TabIndex = 26;
			this.numGPULayers.Value = new decimal(new int[] {
            32,
            0,
            0,
            0});
			// 
			// label7
			// 
			this.label7.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.label7.AutoSize = true;
			this.label7.Location = new System.Drawing.Point(43, 99);
			this.label7.Name = "label7";
			this.label7.Size = new System.Drawing.Size(70, 13);
			this.label7.TabIndex = 15;
			this.label7.Text = "Temperature:";
			// 
			// label10
			// 
			this.label10.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.label10.AutoSize = true;
			this.label10.Location = new System.Drawing.Point(50, 73);
			this.label10.Name = "label10";
			this.label10.Size = new System.Drawing.Size(63, 13);
			this.label10.TabIndex = 27;
			this.label10.Text = "GPU layers:";
			// 
			// numNGen
			// 
			this.numNGen.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.numNGen.Location = new System.Drawing.Point(119, 123);
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
			this.numTemp.Location = new System.Drawing.Point(119, 97);
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
			this.label4.Location = new System.Drawing.Point(10, 125);
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
			this.butApply.Location = new System.Drawing.Point(6, 500);
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
			this.label1.Location = new System.Drawing.Point(-3, 0);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(59, 13);
			this.label1.TabIndex = 1;
			this.label1.Text = "Instruction:";
			// 
			// textInstruction
			// 
			this.textInstruction.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.textInstruction.Location = new System.Drawing.Point(0, 16);
			this.textInstruction.Margin = new System.Windows.Forms.Padding(0);
			this.textInstruction.Multiline = true;
			this.textInstruction.Name = "textInstruction";
			this.textInstruction.Size = new System.Drawing.Size(1008, 109);
			this.textInstruction.TabIndex = 0;
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
			this.splitContainer3.Panel1.Controls.Add(this.listViewModelSearch);
			// 
			// splitContainer3.Panel2
			// 
			this.splitContainer3.Panel2.Controls.Add(this.progressBar1);
			this.splitContainer3.Panel2.Controls.Add(this.butDownload);
			this.splitContainer3.Panel2.Controls.Add(this.listViewQuants);
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
			// listViewModelSearch
			// 
			this.listViewModelSearch.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.listViewModelSearch.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.columnHeader5,
            this.columnHeader6,
            this.columnHeader7,
            this.columnHeader11,
            this.columnHeader12,
            this.columnHeader13,
            this.columnHeader14});
			this.listViewModelSearch.HideSelection = false;
			this.listViewModelSearch.Location = new System.Drawing.Point(-2, 24);
			this.listViewModelSearch.Margin = new System.Windows.Forms.Padding(0);
			this.listViewModelSearch.Name = "listViewModelSearch";
			this.listViewModelSearch.Size = new System.Drawing.Size(1000, 441);
			this.listViewModelSearch.TabIndex = 0;
			this.listViewModelSearch.UseCompatibleStateImageBehavior = false;
			this.listViewModelSearch.View = System.Windows.Forms.View.Details;
			this.listViewModelSearch.SelectedIndexChanged += new System.EventHandler(this.ListViewModelSearch_SelectedIndexChanged);
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
			// listViewQuants
			// 
			this.listViewQuants.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.listViewQuants.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.columnHeader8,
            this.columnHeader9,
            this.columnHeader10});
			this.listViewQuants.HideSelection = false;
			this.listViewQuants.Location = new System.Drawing.Point(-2, 0);
			this.listViewQuants.Margin = new System.Windows.Forms.Padding(0);
			this.listViewQuants.Name = "listViewQuants";
			this.listViewQuants.Size = new System.Drawing.Size(1000, 443);
			this.listViewQuants.TabIndex = 1;
			this.listViewQuants.UseCompatibleStateImageBehavior = false;
			this.listViewQuants.View = System.Windows.Forms.View.Details;
			// 
			// columnHeader8
			// 
			this.columnHeader8.Text = "Filename";
			this.columnHeader8.Width = 434;
			// 
			// columnHeader9
			// 
			this.columnHeader9.Text = "Format";
			this.columnHeader9.Width = 443;
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
            this.labelTPS});
			this.statusStrip1.LayoutStyle = System.Windows.Forms.ToolStripLayoutStyle.HorizontalStackWithOverflow;
			this.statusStrip1.Location = new System.Drawing.Point(0, 973);
			this.statusStrip1.Name = "statusStrip1";
			this.statusStrip1.Size = new System.Drawing.Size(1016, 24);
			this.statusStrip1.TabIndex = 4;
			this.statusStrip1.Text = "statusStrip1";
			// 
			// toolStripStatusLabel1
			// 
			this.toolStripStatusLabel1.Name = "toolStripStatusLabel1";
			this.toolStripStatusLabel1.Size = new System.Drawing.Size(99, 19);
			this.toolStripStatusLabel1.Text = "No model loaded";
			// 
			// labelTokens
			// 
			this.labelTokens.BorderSides = System.Windows.Forms.ToolStripStatusLabelBorderSides.Left;
			this.labelTokens.BorderStyle = System.Windows.Forms.Border3DStyle.Etched;
			this.labelTokens.Name = "labelTokens";
			this.labelTokens.Size = new System.Drawing.Size(56, 19);
			this.labelTokens.Text = "0 Tokens";
			// 
			// labelTPS
			// 
			this.labelTPS.BorderSides = System.Windows.Forms.ToolStripStatusLabelBorderSides.Left;
			this.labelTPS.BorderStyle = System.Windows.Forms.Border3DStyle.Etched;
			this.labelTPS.Name = "labelTPS";
			this.labelTPS.Size = new System.Drawing.Size(48, 19);
			this.labelTPS.Text = "0 Tok/s";
			// 
			// Form1
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.AutoValidate = System.Windows.Forms.AutoValidate.Disable;
			this.ClientSize = new System.Drawing.Size(1016, 997);
			this.Controls.Add(this.statusStrip1);
			this.Controls.Add(this.tabControl1);
			this.DoubleBuffered = true;
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
			this.groupBox2.ResumeLayout(false);
			this.groupBox2.PerformLayout();
			((System.ComponentModel.ISupportInitialize)(this.numRepPen)).EndInit();
			((System.ComponentModel.ISupportInitialize)(this.numBatchSize)).EndInit();
			((System.ComponentModel.ISupportInitialize)(this.numTopK)).EndInit();
			((System.ComponentModel.ISupportInitialize)(this.numTopP)).EndInit();
			this.groupBox1.ResumeLayout(false);
			this.groupBox1.PerformLayout();
			((System.ComponentModel.ISupportInitialize)(this.numThreads)).EndInit();
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
		private System.Windows.Forms.TextBox textInstruction;
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
		private MyFlowLayoutPanel panelChat;
		private System.Windows.Forms.CheckBox checkLoadAuto;
		private System.Windows.Forms.SplitContainer splitContainer2;
		private System.Windows.Forms.ListView listViewMeta;
		private System.Windows.Forms.ColumnHeader columnHeader3;
		private System.Windows.Forms.ColumnHeader columnHeader4;
		private System.Windows.Forms.GroupBox groupBox2;
		private System.Windows.Forms.GroupBox groupBox1;
		private System.Windows.Forms.ToolStripStatusLabel labelTPS;
		private System.Windows.Forms.Button butCodeBlock;
		private System.Windows.Forms.TabPage tabPage4;
		private System.Windows.Forms.SplitContainer splitContainer3;
		private System.Windows.Forms.Button butSearch;
		private System.Windows.Forms.TextBox textSearchTerm;
		private System.Windows.Forms.Label label13;
		private System.Windows.Forms.ListView listViewModelSearch;
		private System.Windows.Forms.ListView listViewQuants;
		private System.Windows.Forms.ColumnHeader columnHeader5;
		private System.Windows.Forms.ColumnHeader columnHeader6;
		private System.Windows.Forms.ColumnHeader columnHeader7;
		private System.Windows.Forms.ColumnHeader columnHeader8;
		private System.Windows.Forms.ColumnHeader columnHeader9;
		private System.Windows.Forms.ColumnHeader columnHeader10;
		private System.Windows.Forms.Button butDownload;
		private System.Windows.Forms.ColumnHeader columnHeader11;
		private System.Windows.Forms.ColumnHeader columnHeader12;
		private System.Windows.Forms.ColumnHeader columnHeader13;
		private System.Windows.Forms.ColumnHeader columnHeader14;
		private System.Windows.Forms.ProgressBar progressBar1;
	}
}

