namespace CatSurvivor;

partial class MainMenuForm
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
		catBanner = new CatBanner();
		lblTitle = new PixelLabel();
		lblSubtitle = new PixelLabel();
		btnStart = new PixelButton();
		btnLeaderboard = new PixelButton();
		btnHelp = new PixelButton();
		btnAbout = new PixelButton();
		btnSettings = new PixelButton();
		btnExit = new PixelButton();
		lblHint = new PixelLabel();
		SuspendLayout();
		//
		// catBanner
		//
		catBanner.Location = new Point(30, 30);
		catBanner.Name = "catBanner";
		catBanner.Size = new Size(380, 100);
		//
		// lblTitle
		//
		lblTitle.ForeColor = Color.FromArgb(242, 166, 51);
		lblTitle.Location = new Point(20, 136);
		lblTitle.Name = "lblTitle";
		lblTitle.PixelScale = 5;
		lblTitle.Size = new Size(400, 54);
		lblTitle.Text = "CAT SURVIVOR";
		//
		// lblSubtitle
		//
		lblSubtitle.ForeColor = Color.FromArgb(168, 176, 199);
		lblSubtitle.Location = new Point(20, 194);
		lblSubtitle.Name = "lblSubtitle";
		lblSubtitle.PixelScale = 2;
		lblSubtitle.Size = new Size(400, 26);
		lblSubtitle.Text = "Sống sót - hạ boss ở cấp 27";
		//
		// btnStart
		//
		btnStart.Highlight = true;
		btnStart.Location = new Point(60, 236);
		btnStart.Name = "btnStart";
		btnStart.PixelScale = 3;
		btnStart.Size = new Size(320, 54);
		btnStart.TabIndex = 0;
		btnStart.Text = "▶  Bắt đầu";
		btnStart.Click += btnStart_Click;
		//
		// btnLeaderboard
		//
		btnLeaderboard.Location = new Point(60, 300);
		btnLeaderboard.Name = "btnLeaderboard";
		btnLeaderboard.Size = new Size(320, 44);
		btnLeaderboard.TabIndex = 1;
		btnLeaderboard.Text = "Bảng thành tích";
		btnLeaderboard.Click += btnLeaderboard_Click;
		//
		// btnHelp
		//
		btnHelp.Location = new Point(60, 352);
		btnHelp.Name = "btnHelp";
		btnHelp.Size = new Size(320, 44);
		btnHelp.TabIndex = 2;
		btnHelp.Text = "Hướng dẫn";
		btnHelp.Click += btnHelp_Click;
		//
		// btnAbout
		//
		btnAbout.Location = new Point(60, 404);
		btnAbout.Name = "btnAbout";
		btnAbout.Size = new Size(320, 44);
		btnAbout.TabIndex = 3;
		btnAbout.Text = "Thông tin nhóm";
		btnAbout.Click += btnAbout_Click;
		//
		// btnSettings
		//
		btnSettings.Location = new Point(60, 456);
		btnSettings.Name = "btnSettings";
		btnSettings.Size = new Size(320, 44);
		btnSettings.TabIndex = 4;
		btnSettings.Text = "Cài đặt";
		btnSettings.Click += btnSettings_Click;
		//
		// btnExit
		//
		btnExit.Location = new Point(60, 508);
		btnExit.Name = "btnExit";
		btnExit.Size = new Size(320, 44);
		btnExit.TabIndex = 5;
		btnExit.Text = "Thoát";
		btnExit.Click += btnExit_Click;
		//
		// lblHint
		//
		lblHint.ForeColor = Color.FromArgb(120, 128, 150);
		lblHint.Location = new Point(20, 566);
		lblHint.Name = "lblHint";
		lblHint.PixelScale = 1;
		lblHint.Size = new Size(400, 20);
		lblHint.Text = "W A S D di chuyển  •  Space dash  •  Esc tạm dừng  •  M âm thanh";
		//
		// MainMenuForm
		//
		AutoScaleDimensions = new SizeF(96F, 96F);
		AutoScaleMode = AutoScaleMode.Dpi;
		ClientSize = new Size(440, 600);
		Controls.Add(catBanner);
		Controls.Add(lblTitle);
		Controls.Add(lblSubtitle);
		Controls.Add(btnStart);
		Controls.Add(btnLeaderboard);
		Controls.Add(btnHelp);
		Controls.Add(btnAbout);
		Controls.Add(btnSettings);
		Controls.Add(btnExit);
		Controls.Add(lblHint);
		FormBorderStyle = FormBorderStyle.FixedDialog;
		MaximizeBox = false;
		Name = "MainMenuForm";
		StartPosition = FormStartPosition.CenterScreen;
		Text = "Cat Survivor";
		ResumeLayout(false);
	}

	#endregion

	private CatBanner catBanner;
	private PixelLabel lblTitle;
	private PixelLabel lblSubtitle;
	private PixelButton btnStart;
	private PixelButton btnLeaderboard;
	private PixelButton btnHelp;
	private PixelButton btnAbout;
	private PixelButton btnSettings;
	private PixelButton btnExit;
	private PixelLabel lblHint;
}
