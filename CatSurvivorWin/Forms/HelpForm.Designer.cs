namespace CatSurvivor;

partial class HelpForm
{
	private System.ComponentModel.IContainer components = null;

	protected override void Dispose(bool disposing)
	{
		if (disposing && components != null) components.Dispose();
		base.Dispose(disposing);
	}

	#region Windows Form Designer generated code

	private void InitializeComponent()
	{
		lblTitle = new PixelLabel();
		rtbHelp = new RichTextBox();
		btnClose = new PixelButton();
		SuspendLayout();
		//
		// lblTitle
		//
		lblTitle.ForeColor = Color.FromArgb(242, 166, 51);
		lblTitle.Location = new Point(20, 28);
		lblTitle.Name = "lblTitle";
		lblTitle.PixelScale = 4;
		lblTitle.Size = new Size(720, 46);
		lblTitle.Text = "HƯỚNG DẪN CHƠI";
		//
		// rtbHelp
		//
		rtbHelp.Location = new Point(40, 90);
		rtbHelp.Name = "rtbHelp";
		rtbHelp.ReadOnly = true;
		rtbHelp.Size = new Size(680, 440);
		rtbHelp.TabStop = false;
		//
		// btnClose
		//
		btnClose.Highlight = true;
		btnClose.Location = new Point(290, 548);
		btnClose.Name = "btnClose";
		btnClose.Size = new Size(180, 46);
		btnClose.TabIndex = 0;
		btnClose.Text = "Đóng";
		btnClose.Click += btnClose_Click;
		//
		// HelpForm
		//
		AcceptButton = btnClose;
		AutoScaleDimensions = new SizeF(96F, 96F);
		AutoScaleMode = AutoScaleMode.Dpi;
		CancelButton = btnClose;
		ClientSize = new Size(760, 620);
		Controls.Add(lblTitle);
		Controls.Add(rtbHelp);
		Controls.Add(btnClose);
		FormBorderStyle = FormBorderStyle.FixedDialog;
		MaximizeBox = false;
		MinimizeBox = false;
		Name = "HelpForm";
		ShowInTaskbar = false;
		StartPosition = FormStartPosition.CenterParent;
		Text = "Hướng dẫn";
		ResumeLayout(false);
	}

	#endregion

	private PixelLabel lblTitle;
	private RichTextBox rtbHelp;
	private PixelButton btnClose;
}
