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
			this.labelRole = new System.Windows.Forms.Label();
			this.panel1 = new System.Windows.Forms.Panel();
			this.butApplyEdit = new System.Windows.Forms.Button();
			this.butCancelEdit = new System.Windows.Forms.Button();
			this.checkThink = new System.Windows.Forms.CheckBox();
			this.panel1.SuspendLayout();
			this.SuspendLayout();
			// 
			// richTextMsg
			// 
			resources.ApplyResources(this.richTextMsg, "richTextMsg");
			this.richTextMsg.CausesValidation = false;
			this.richTextMsg.Name = "richTextMsg";
			this.richTextMsg.ReadOnly = true;
			// 
			// butDelete
			// 
			resources.ApplyResources(this.butDelete, "butDelete");
			this.butDelete.Name = "butDelete";
			this.butDelete.UseVisualStyleBackColor = true;
			// 
			// butEdit
			// 
			resources.ApplyResources(this.butEdit, "butEdit");
			this.butEdit.Name = "butEdit";
			this.butEdit.UseVisualStyleBackColor = true;
			// 
			// butRegen
			// 
			resources.ApplyResources(this.butRegen, "butRegen");
			this.butRegen.Name = "butRegen";
			this.butRegen.UseVisualStyleBackColor = true;
			// 
			// labelRole
			// 
			resources.ApplyResources(this.labelRole, "labelRole");
			this.labelRole.Name = "labelRole";
			// 
			// panel1
			// 
			this.panel1.CausesValidation = false;
			this.panel1.Controls.Add(this.butApplyEdit);
			this.panel1.Controls.Add(this.butCancelEdit);
			this.panel1.Controls.Add(this.checkThink);
			this.panel1.Controls.Add(this.richTextMsg);
			this.panel1.Controls.Add(this.butRegen);
			this.panel1.Controls.Add(this.labelRole);
			this.panel1.Controls.Add(this.butEdit);
			this.panel1.Controls.Add(this.butDelete);
			resources.ApplyResources(this.panel1, "panel1");
			this.panel1.Name = "panel1";
			// 
			// butApplyEdit
			// 
			resources.ApplyResources(this.butApplyEdit, "butApplyEdit");
			this.butApplyEdit.Name = "butApplyEdit";
			this.butApplyEdit.UseVisualStyleBackColor = true;
			// 
			// butCancelEdit
			// 
			resources.ApplyResources(this.butCancelEdit, "butCancelEdit");
			this.butCancelEdit.Name = "butCancelEdit";
			this.butCancelEdit.UseVisualStyleBackColor = true;
			// 
			// checkThink
			// 
			resources.ApplyResources(this.checkThink, "checkThink");
			this.checkThink.Name = "checkThink";
			this.checkThink.UseVisualStyleBackColor = true;
			this.checkThink.CheckedChanged += new System.EventHandler(this.CheckThink_CheckedChanged);
			// 
			// ChatMessage
			// 
			resources.ApplyResources(this, "$this");
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.AutoValidate = System.Windows.Forms.AutoValidate.Disable;
			this.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
			this.CausesValidation = false;
			this.Controls.Add(this.panel1);
			this.DoubleBuffered = true;
			this.Name = "ChatMessage";
			this.Load += new System.EventHandler(this.ChatMessage_Load);
			this.panel1.ResumeLayout(false);
			this.panel1.PerformLayout();
			this.ResumeLayout(false);

		}

		#endregion
		internal System.Windows.Forms.Label labelRole;
		internal System.Windows.Forms.RichTextBox richTextMsg;
		internal System.Windows.Forms.Button butDelete;
		internal System.Windows.Forms.Button butEdit;
		internal System.Windows.Forms.Button butRegen;
		internal System.Windows.Forms.Panel panel1;
		internal System.Windows.Forms.CheckBox checkThink;
		internal System.Windows.Forms.Button butApplyEdit;
		internal System.Windows.Forms.Button butCancelEdit;
	}
}
