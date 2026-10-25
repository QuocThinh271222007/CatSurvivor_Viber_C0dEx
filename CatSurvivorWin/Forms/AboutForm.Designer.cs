namespace CatSurvivor;

partial class AboutForm
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
		lblGame = new PixelLabel();
		tblTeam = new PixelTable();
		lblError = new PixelLabel();
		lblNote = new PixelLabel();
		lblCredit = new PixelLabel();
		btnClose = new PixelButton();
		SuspendLayout();
		//
		// lblTitle
		//
		lblTitle.ForeColor = Color.FromArgb(242, 166, 51);
		lblTitle.Location = new Point(20, 28);
		lblTitle.Name = "lblTitle";
		lblTitle.PixelScale = 4;
		lblTitle.Size = new Size(660, 46);
		lblTitle.Text = "THÔNG TIN NHÓM";
		//
		// lblGame
		//
		lblGame.ForeColor = Color.FromArgb(235, 236, 244);
		lblGame.Location = new Point(20, 82);
		lblGame.Name = "lblGame";
		lblGame.PixelScale = 2;
		lblGame.Size = new Size(660, 52);
		lblGame.Text = "CAT SURVIVOR  •  Bài tập Lập trình Windows (2511COMP1019)\nCông nghệ: Windows Forms (.NET 11, C#)  •  Phiên bản 1.0";
		//
		// tblTeam
		//
		tblTeam.Location = new Point(40, 146);
		tblTeam.Name = "tblTeam";
		tblTeam.PixelScale = 2;
		tblTeam.Size = new Size(620, 190);
		//
		// lblError
		//
		lblError.ForeColor = Color.FromArgb(255, 107, 92);
		lblError.Location = new Point(40, 344);
		lblError.Name = "lblError";
		lblError.PixelScale = 1;
		lblError.Size = new Size(620, 20);
		//
		// lblNote
		//
		lblNote.ForeColor = Color.FromArgb(150, 158, 180);
		lblNote.Location = new Point(40, 366);
		lblNote.Name = "lblNote";
		lblNote.PixelScale = 1;
		lblNote.Size = new Size(620, 20);
		//
		// lblCredit
		//
		lblCredit.ForeColor = Color.FromArgb(150, 158, 180);
		lblCredit.Location = new Point(40, 388);
		lblCredit.Name = "lblCredit";
		lblCredit.PixelScale = 1;
		lblCredit.Size = new Size(620, 20);
		lblCredit.Text = "Font: Aseprite Font © David Capello (CC BY 4.0) - aseprite.org";
		//
		// btnClose
		//
		btnClose.Highlight = true;
		btnClose.Location = new Point(260, 424);
		btnClose.Name = "btnClose";
		btnClose.Size = new Size(180, 46);
		btnClose.TabIndex = 0;
		btnClose.Text = "Đóng";
		btnClose.Click += btnClose_Click;
		//
		// AboutForm
		//
		AcceptButton = btnClose;
		AutoScaleDimensions = new SizeF(96F, 96F);
		AutoScaleMode = AutoScaleMode.Dpi;
		CancelButton = btnClose;
		ClientSize = new Size(700, 494);
		Controls.Add(lblTitle);
		Controls.Add(lblGame);
		Controls.Add(tblTeam);
		Controls.Add(lblError);
		Controls.Add(lblNote);
		Controls.Add(lblCredit);
		Controls.Add(btnClose);
		FormBorderStyle = FormBorderStyle.FixedDialog;
		MaximizeBox = false;
		MinimizeBox = false;
		Name = "AboutForm";
		ShowInTaskbar = false;
		StartPosition = FormStartPosition.CenterParent;
		Text = "Thông tin nhóm";
		ResumeLayout(false);
	}

	#endregion

	private PixelLabel lblTitle;
	private PixelLabel lblGame;
	private PixelTable tblTeam;
	private PixelLabel lblError;
	private PixelLabel lblNote;
	private PixelLabel lblCredit;
	private PixelButton btnClose;
}
