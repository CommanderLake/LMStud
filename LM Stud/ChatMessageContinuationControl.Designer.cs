namespace LMStud{
	internal sealed partial class ChatMessageContinuationControl{
		private System.ComponentModel.IContainer components = null;

		protected override void Dispose(bool disposing){
			if(disposing && components != null) components.Dispose();
			base.Dispose(disposing);
		}

		private void InitializeComponent(){
			this.MarkdownView = new LMStud.MarkdownRenderControl();
			this.SuspendLayout();
			//
			// MarkdownView
			//
			this.MarkdownView.BackColor = System.Drawing.SystemColors.Window;
			this.MarkdownView.Dock = System.Windows.Forms.DockStyle.Fill;
			this.MarkdownView.ForeColor = System.Drawing.SystemColors.WindowText;
			this.MarkdownView.Location = new System.Drawing.Point(0, 0);
			this.MarkdownView.Margin = new System.Windows.Forms.Padding(0);
			this.MarkdownView.Name = "MarkdownView";
			this.MarkdownView.Size = new System.Drawing.Size(192, 42);
			this.MarkdownView.TabIndex = 0;
			this.MarkdownView.ContentHeightChanged += new System.EventHandler(this.MarkdownView_ContentHeightChanged);
			//
			// ChatMessageContinuationControl
			//
			this.Controls.Add(this.MarkdownView);
			this.Margin = new System.Windows.Forms.Padding(0);
			this.Name = "ChatMessageContinuationControl";
			this.Size = new System.Drawing.Size(192, 42);
			this.ResumeLayout(false);
		}

		internal MarkdownRenderControl MarkdownView;
	}
}
