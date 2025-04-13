namespace LMStud {
	internal partial class ChatMessage {
		/// <summary> 
		/// Required designer variable.
		/// </summary>
		private System.ComponentModel.IContainer components = null;

		/// <summary> 
		/// Clean up any resources being used.
		/// </summary>
		/// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
		protected override void Dispose(bool disposing) {
			if(disposing && (components != null)) {
				components.Dispose();
			}
			base.Dispose(disposing);
		}

		#region Component Designer generated code

		/// <summary> 
		/// Required method for Designer support - do not modify 
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent() {
			System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ChatMessage));
			this.richTextMsg = new System.Windows.Forms.RichTextBox();
			this.butDelete = new System.Windows.Forms.Button();
			this.butEdit = new System.Windows.Forms.Button();
			this.butRegen = new System.Windows.Forms.Button();
			this.label1 = new System.Windows.Forms.Label();
			this.panel1 = new System.Windows.Forms.Panel();
			this.butApplyEdit = new System.Windows.Forms.Button();
			this.butCancelEdit = new System.Windows.Forms.Button();
			this.checkThink = new System.Windows.Forms.CheckBox();
			this.panel1.SuspendLayout();
			this.SuspendLayout();
			// 
			// richTextMsg
			// 
			this.richTextMsg.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.richTextMsg.CausesValidation = false;
			this.richTextMsg.DetectUrls = false;
			this.richTextMsg.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.richTextMsg.Location = new System.Drawing.Point(0, 24);
			this.richTextMsg.Margin = new System.Windows.Forms.Padding(0);
			this.richTextMsg.Name = "richTextMsg";
			this.richTextMsg.ReadOnly = true;
			this.richTextMsg.ScrollBars = System.Windows.Forms.RichTextBoxScrollBars.None;
			this.richTextMsg.Size = new System.Drawing.Size(200, 40);
			this.richTextMsg.TabIndex = 0;
			this.richTextMsg.Text = "";
			// 
			// butDelete
			// 
			this.butDelete.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.butDelete.Enabled = false;
			this.butDelete.Image = ((System.Drawing.Image)(resources.GetObject("butDelete.Image")));
			this.butDelete.Location = new System.Drawing.Point(176, 0);
			this.butDelete.Margin = new System.Windows.Forms.Padding(0);
			this.butDelete.Name = "butDelete";
			this.butDelete.Size = new System.Drawing.Size(24, 24);
			this.butDelete.TabIndex = 1;
			this.butDelete.UseVisualStyleBackColor = true;
			// 
			// butEdit
			// 
			this.butEdit.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.butEdit.Enabled = false;
			this.butEdit.Image = ((System.Drawing.Image)(resources.GetObject("butEdit.Image")));
			this.butEdit.Location = new System.Drawing.Point(152, 0);
			this.butEdit.Margin = new System.Windows.Forms.Padding(0);
			this.butEdit.Name = "butEdit";
			this.butEdit.Size = new System.Drawing.Size(24, 24);
			this.butEdit.TabIndex = 2;
			this.butEdit.UseVisualStyleBackColor = true;
			// 
			// butRegen
			// 
			this.butRegen.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.butRegen.Enabled = false;
			this.butRegen.Image = ((System.Drawing.Image)(resources.GetObject("butRegen.Image")));
			this.butRegen.Location = new System.Drawing.Point(128, 0);
			this.butRegen.Margin = new System.Windows.Forms.Padding(0);
			this.butRegen.Name = "butRegen";
			this.butRegen.Size = new System.Drawing.Size(24, 24);
			this.butRegen.TabIndex = 3;
			this.butRegen.UseVisualStyleBackColor = true;
			// 
			// label1
			// 
			this.label1.AutoSize = true;
			this.label1.Font = new System.Drawing.Font("Segoe UI Semibold", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.label1.Location = new System.Drawing.Point(3, 0);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(54, 21);
			this.label1.TabIndex = 4;
			this.label1.Text = "label1";
			// 
			// panel1
			// 
			this.panel1.CausesValidation = false;
			this.panel1.Controls.Add(this.butApplyEdit);
			this.panel1.Controls.Add(this.butCancelEdit);
			this.panel1.Controls.Add(this.checkThink);
			this.panel1.Controls.Add(this.richTextMsg);
			this.panel1.Controls.Add(this.butRegen);
			this.panel1.Controls.Add(this.label1);
			this.panel1.Controls.Add(this.butEdit);
			this.panel1.Controls.Add(this.butDelete);
			this.panel1.Dock = System.Windows.Forms.DockStyle.Fill;
			this.panel1.Location = new System.Drawing.Point(0, 0);
			this.panel1.Name = "panel1";
			this.panel1.Size = new System.Drawing.Size(200, 64);
			this.panel1.TabIndex = 5;
			// 
			// butApplyEdit
			// 
			this.butApplyEdit.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.butApplyEdit.Enabled = false;
			this.butApplyEdit.Image = ((System.Drawing.Image)(resources.GetObject("butApplyEdit.Image")));
			this.butApplyEdit.Location = new System.Drawing.Point(128, 0);
			this.butApplyEdit.Margin = new System.Windows.Forms.Padding(0);
			this.butApplyEdit.Name = "butApplyEdit";
			this.butApplyEdit.Size = new System.Drawing.Size(24, 24);
			this.butApplyEdit.TabIndex = 7;
			this.butApplyEdit.UseVisualStyleBackColor = true;
			this.butApplyEdit.Visible = false;
			// 
			// butCancelEdit
			// 
			this.butCancelEdit.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.butCancelEdit.Enabled = false;
			this.butCancelEdit.Image = ((System.Drawing.Image)(resources.GetObject("butCancelEdit.Image")));
			this.butCancelEdit.Location = new System.Drawing.Point(152, 0);
			this.butCancelEdit.Margin = new System.Windows.Forms.Padding(0);
			this.butCancelEdit.Name = "butCancelEdit";
			this.butCancelEdit.Size = new System.Drawing.Size(24, 24);
			this.butCancelEdit.TabIndex = 6;
			this.butCancelEdit.UseVisualStyleBackColor = true;
			this.butCancelEdit.Visible = false;
			// 
			// checkThink
			// 
			this.checkThink.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.checkThink.AutoSize = true;
			this.checkThink.Enabled = false;
			this.checkThink.Location = new System.Drawing.Point(72, 5);
			this.checkThink.Name = "checkThink";
			this.checkThink.Size = new System.Drawing.Size(53, 17);
			this.checkThink.TabIndex = 5;
			this.checkThink.Text = "Think";
			this.checkThink.UseVisualStyleBackColor = true;
			this.checkThink.Visible = false;
			this.checkThink.CheckedChanged += new System.EventHandler(this.CheckThink_CheckedChanged);
			// 
			// ChatMessage
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.AutoValidate = System.Windows.Forms.AutoValidate.Disable;
			this.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
			this.CausesValidation = false;
			this.Controls.Add(this.panel1);
			this.DoubleBuffered = true;
			this.Margin = new System.Windows.Forms.Padding(0);
			this.Name = "ChatMessage";
			this.Size = new System.Drawing.Size(200, 64);
			this.panel1.ResumeLayout(false);
			this.panel1.PerformLayout();
			this.ResumeLayout(false);

		}

		#endregion
		internal System.Windows.Forms.Label label1;
		internal System.Windows.Forms.RichTextBox richTextMsg;
		internal System.Windows.Forms.Button butDelete;
		internal System.Windows.Forms.Button butEdit;
		internal System.Windows.Forms.Button butRegen;
		private System.Windows.Forms.Panel panel1;
		internal System.Windows.Forms.CheckBox checkThink;
		internal System.Windows.Forms.Button butApplyEdit;
		internal System.Windows.Forms.Button butCancelEdit;
	}
}
