namespace CatSurvivor;

partial class GameOverForm
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
		lblStats = new PixelLabel();
		lblName = new PixelLabel();
		txtName = new TextBox();
		btnSave = new PixelButton();
		lblMessage = new PixelLabel();
		btnLeaderboard = new PixelButton();
		btnRestart = new PixelButton();
		btnMenu = new PixelButton();
		btnQuit = new PixelButton();
		SuspendLayout();
		//
		// lblTitle
		//
		lblTitle.ForeColor = Color.FromArgb(255, 107, 92);
		lblTitle.Location = new Point(20, 28);
		lblTitle.Name = "lblTitle";
		lblTitle.PixelScale = 4;
		lblTitle.Size = new Size(460, 48);
		lblTitle.Text = "BẠN ĐÃ CHẾT";
		//
		// lblStats
		//
		lblStats.Location = new Point(20, 84);
		lblStats.Name = "lblStats";
		lblStats.PixelScale = 2;
		lblStats.Size = new Size(460, 60);
		//
		// lblName
		//
		lblName.ForeColor = Color.FromArgb(168, 176, 199);
		lblName.HAlign = HorizontalAlignment.Left;
		lblName.Location = new Point(60, 160);
		lblName.Name = "lblName";
		lblName.PixelScale = 2;
		lblName.Size = new Size(60, 30);
		lblName.Text = "Tên:";
		//
		// txtName
		//
		txtName.Location = new Point(124, 160);
		txtName.MaxLength = 16;
		txtName.Name = "txtName";
		txtName.PlaceholderText = "Nhập tên của bạn";
		txtName.Size = new Size(316, 29);
		txtName.TabIndex = 0;
		txtName.KeyDown += txtName_KeyDown;
		//
		// btnSave
		//
		btnSave.Highlight = true;
		btnSave.Location = new Point(60, 204);
		btnSave.Name = "btnSave";
		btnSave.Size = new Size(380, 48);
		btnSave.TabIndex = 1;
		btnSave.Text = "Lưu thành tích";
		btnSave.Click += btnSave_Click;
		//
		// lblMessage
		//
		lblMessage.ForeColor = Color.FromArgb(140, 242, 140);
		lblMessage.Location = new Point(60, 258);
		lblMessage.Name = "lblMessage";
		lblMessage.PixelScale = 1;
		lblMessage.Size = new Size(380, 22);
		//
		// btnLeaderboard
		//
		btnLeaderboard.Location = new Point(60, 288);
		btnLeaderboard.Name = "btnLeaderboard";
		btnLeaderboard.Size = new Size(380, 42);
		btnLeaderboard.TabIndex = 2;
		btnLeaderboard.Text = "Bảng thành tích";
		btnLeaderboard.Click += btnLeaderboard_Click;
		//
		// btnRestart
		//
		btnRestart.Location = new Point(60, 338);
		btnRestart.Name = "btnRestart";
		btnRestart.Size = new Size(380, 42);
		btnRestart.TabIndex = 3;
		btnRestart.Text = "Chơi lại";
		btnRestart.Click += btnRestart_Click;
		//
		// btnMenu
		//
		btnMenu.Location = new Point(60, 388);
		btnMenu.Name = "btnMenu";
		btnMenu.Size = new Size(380, 42);
		btnMenu.TabIndex = 4;
		btnMenu.Text = "Về menu chính";
		btnMenu.Click += btnMenu_Click;
		//
		// btnQuit
		//
		btnQuit.Location = new Point(60, 438);
		btnQuit.Name = "btnQuit";
		btnQuit.Size = new Size(380, 42);
		btnQuit.TabIndex = 5;
		btnQuit.Text = "Thoát game";
		btnQuit.Click += btnQuit_Click;
		//
		// GameOverForm
		//
		AutoScaleDimensions = new SizeF(96F, 96F);
		AutoScaleMode = AutoScaleMode.Dpi;
		ClientSize = new Size(500, 510);
		Controls.Add(lblTitle);
		Controls.Add(lblStats);
		Controls.Add(lblName);
		Controls.Add(txtName);
		Controls.Add(btnSave);
		Controls.Add(lblMessage);
		Controls.Add(btnLeaderboard);
		Controls.Add(btnRestart);
		Controls.Add(btnMenu);
		Controls.Add(btnQuit);
		FormBorderStyle = FormBorderStyle.FixedDialog;
		MaximizeBox = false;
		MinimizeBox = false;
		Name = "GameOverForm";
		ShowInTaskbar = false;
		StartPosition = FormStartPosition.CenterParent;
		Text = "Kết thúc";
		ResumeLayout(false);
		PerformLayout();
	}

	#endregion

	private PixelLabel lblTitle;
	private PixelLabel lblStats;
	private PixelLabel lblName;
	private TextBox txtName;
	private PixelButton btnSave;
	private PixelLabel lblMessage;
	private PixelButton btnLeaderboard;
	private PixelButton btnRestart;
	private PixelButton btnMenu;
	private PixelButton btnQuit;
}
