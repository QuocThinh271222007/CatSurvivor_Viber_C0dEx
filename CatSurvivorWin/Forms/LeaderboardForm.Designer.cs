namespace CatSurvivor;

partial class LeaderboardForm
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
		tblScores = new PixelTable();
		lblStatus = new PixelLabel();
		btnClose = new PixelButton();
		SuspendLayout();
		//
		// lblTitle
		//
		lblTitle.ForeColor = Color.FromArgb(242, 166, 51);
		lblTitle.Location = new Point(20, 28);
		lblTitle.Name = "lblTitle";
		lblTitle.PixelScale = 4;
		lblTitle.Size = new Size(920, 46);
		lblTitle.Text = "BẢNG THÀNH TÍCH";
		//
		// tblScores
		//
		tblScores.EmptyText = "Chưa có thành tích nào. Hãy chơi và lưu điểm!";
		tblScores.Location = new Point(40, 90);
		tblScores.Name = "tblScores";
		tblScores.PixelScale = 2;
		tblScores.Size = new Size(880, 320);
		//
		// lblStatus
		//
		lblStatus.ForeColor = Color.FromArgb(168, 176, 199);
		lblStatus.Location = new Point(40, 420);
		lblStatus.Name = "lblStatus";
		lblStatus.PixelScale = 2;
		lblStatus.Size = new Size(880, 28);
		//
		// btnClose
		//
		btnClose.Highlight = true;
		btnClose.Location = new Point(390, 462);
		btnClose.Name = "btnClose";
		btnClose.Size = new Size(180, 46);
		btnClose.TabIndex = 0;
		btnClose.Text = "Đóng";
		btnClose.Click += btnClose_Click;
		//
		// LeaderboardForm
		//
		AcceptButton = btnClose;
		AutoScaleDimensions = new SizeF(96F, 96F);
		AutoScaleMode = AutoScaleMode.Dpi;
		CancelButton = btnClose;
		ClientSize = new Size(960, 530);
		Controls.Add(lblTitle);
		Controls.Add(tblScores);
		Controls.Add(lblStatus);
		Controls.Add(btnClose);
		FormBorderStyle = FormBorderStyle.FixedDialog;
		MaximizeBox = false;
		MinimizeBox = false;
		Name = "LeaderboardForm";
		ShowInTaskbar = false;
		StartPosition = FormStartPosition.CenterParent;
		Text = "Bảng thành tích";
		ResumeLayout(false);
	}

	#endregion

	private PixelLabel lblTitle;
	private PixelTable tblScores;
	private PixelLabel lblStatus;
	private PixelButton btnClose;
}
