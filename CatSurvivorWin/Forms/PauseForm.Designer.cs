namespace CatSurvivor;

partial class PauseForm
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
		btnResume = new PixelButton();
		btnRestart = new PixelButton();
		btnLeaderboard = new PixelButton();
		btnHelp = new PixelButton();
		btnSettings = new PixelButton();
		btnMenu = new PixelButton();
		btnQuit = new PixelButton();
		SuspendLayout();
		//
		// lblTitle
		//
		lblTitle.ForeColor = Color.FromArgb(242, 166, 51);
		lblTitle.Location = new Point(20, 30);
		lblTitle.Name = "lblTitle";
		lblTitle.PixelScale = 4;
		lblTitle.Size = new Size(340, 44);
		lblTitle.Text = "TẠM DỪNG";
		//
		// btnResume
		//
		btnResume.Highlight = true;
		btnResume.Location = new Point(50, 96);
		btnResume.Name = "btnResume";
		btnResume.PixelScale = 3;
		btnResume.Size = new Size(280, 52);
		btnResume.TabIndex = 0;
		btnResume.Text = "Tiếp tục";
		btnResume.Click += btnResume_Click;
		//
		// btnRestart
		//
		btnRestart.Location = new Point(50, 160);
		btnRestart.Name = "btnRestart";
		btnRestart.Size = new Size(280, 42);
		btnRestart.TabIndex = 1;
		btnRestart.Text = "Chơi lại";
		btnRestart.Click += btnRestart_Click;
		//
		// btnLeaderboard
		//
		btnLeaderboard.Location = new Point(50, 210);
		btnLeaderboard.Name = "btnLeaderboard";
		btnLeaderboard.Size = new Size(280, 42);
		btnLeaderboard.TabIndex = 2;
		btnLeaderboard.Text = "Bảng thành tích";
		btnLeaderboard.Click += btnLeaderboard_Click;
		//
		// btnHelp
		//
		btnHelp.Location = new Point(50, 260);
		btnHelp.Name = "btnHelp";
		btnHelp.Size = new Size(280, 42);
		btnHelp.TabIndex = 3;
		btnHelp.Text = "Hướng dẫn";
		btnHelp.Click += btnHelp_Click;
		//
		// btnSettings
		//
		btnSettings.Location = new Point(50, 310);
		btnSettings.Name = "btnSettings";
		btnSettings.Size = new Size(280, 42);
		btnSettings.TabIndex = 4;
		btnSettings.Text = "Cài đặt";
		btnSettings.Click += btnSettings_Click;
		//
		// btnMenu
		//
		btnMenu.Location = new Point(50, 360);
		btnMenu.Name = "btnMenu";
		btnMenu.Size = new Size(280, 42);
		btnMenu.TabIndex = 5;
		btnMenu.Text = "Về menu chính";
		btnMenu.Click += btnMenu_Click;
		//
		// btnQuit
		//
		btnQuit.Location = new Point(50, 410);
		btnQuit.Name = "btnQuit";
		btnQuit.Size = new Size(280, 42);
		btnQuit.TabIndex = 6;
		btnQuit.Text = "Thoát game";
		btnQuit.Click += btnQuit_Click;
		//
		// PauseForm
		//
		AutoScaleDimensions = new SizeF(96F, 96F);
		AutoScaleMode = AutoScaleMode.Dpi;
		ClientSize = new Size(380, 480);
		Controls.Add(lblTitle);
		Controls.Add(btnResume);
		Controls.Add(btnRestart);
		Controls.Add(btnLeaderboard);
		Controls.Add(btnHelp);
		Controls.Add(btnSettings);
		Controls.Add(btnMenu);
		Controls.Add(btnQuit);
		FormBorderStyle = FormBorderStyle.FixedDialog;
		KeyPreview = true;
		MaximizeBox = false;
		MinimizeBox = false;
		Name = "PauseForm";
		ShowInTaskbar = false;
		StartPosition = FormStartPosition.CenterParent;
		Text = "Tạm dừng";
		KeyDown += PauseForm_KeyDown;
		ResumeLayout(false);
	}

	#endregion

	private PixelLabel lblTitle;
	private PixelButton btnResume;
	private PixelButton btnRestart;
	private PixelButton btnLeaderboard;
	private PixelButton btnHelp;
	private PixelButton btnSettings;
	private PixelButton btnMenu;
	private PixelButton btnQuit;
}
