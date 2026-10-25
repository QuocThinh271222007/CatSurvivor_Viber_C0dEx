namespace CatSurvivor;

partial class SettingsForm
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
		btnDifficulty = new PixelButton();
		lblDiffNote = new PixelLabel();
		btnSound = new PixelButton();
		lblMusic = new PixelLabel();
		sldMusic = new PixelSlider();
		lblSfx = new PixelLabel();
		sldSfx = new PixelSlider();
		btnAutoAim = new PixelButton();
		btnFullscreen = new PixelButton();
		btnHitbox = new PixelButton();
		lblNote = new PixelLabel();
		btnClose = new PixelButton();
		SuspendLayout();
		//
		// lblTitle
		//
		lblTitle.ForeColor = Color.FromArgb(242, 166, 51);
		lblTitle.Location = new Point(20, 28);
		lblTitle.Name = "lblTitle";
		lblTitle.PixelScale = 4;
		lblTitle.Size = new Size(520, 46);
		lblTitle.Text = "CÀI ĐẶT";
		//
		// btnDifficulty
		//
		btnDifficulty.Location = new Point(60, 92);
		btnDifficulty.Name = "btnDifficulty";
		btnDifficulty.Size = new Size(440, 46);
		btnDifficulty.TabIndex = 7;
		btnDifficulty.Text = "Độ khó: Dễ";
		btnDifficulty.Click += btnDifficulty_Click;
		//
		// lblDiffNote
		//
		lblDiffNote.ForeColor = Color.FromArgb(168, 176, 199);
		lblDiffNote.Location = new Point(40, 142);
		lblDiffNote.Name = "lblDiffNote";
		lblDiffNote.PixelScale = 1;
		lblDiffNote.Size = new Size(480, 30);
		lblDiffNote.Text = "";
		//
		// btnSound
		//
		btnSound.Location = new Point(60, 176);
		btnSound.Name = "btnSound";
		btnSound.Size = new Size(440, 46);
		btnSound.TabIndex = 0;
		btnSound.Text = "Âm thanh: BẬT";
		btnSound.Click += btnSound_Click;
		//
		// lblMusic
		//
		lblMusic.ForeColor = Color.FromArgb(168, 176, 199);
		lblMusic.HAlign = HorizontalAlignment.Left;
		lblMusic.Location = new Point(64, 240);
		lblMusic.Name = "lblMusic";
		lblMusic.PixelScale = 2;
		lblMusic.Size = new Size(432, 26);
		lblMusic.Text = "Âm lượng nhạc nền";
		//
		// sldMusic
		//
		sldMusic.Location = new Point(64, 270);
		sldMusic.Name = "sldMusic";
		sldMusic.Size = new Size(432, 34);
		sldMusic.TabIndex = 1;
		sldMusic.Value = 70;
		sldMusic.ValueChanged += sldMusic_ValueChanged;
		sldMusic.Committed += sldCommitted;
		//
		// lblSfx
		//
		lblSfx.ForeColor = Color.FromArgb(168, 176, 199);
		lblSfx.HAlign = HorizontalAlignment.Left;
		lblSfx.Location = new Point(64, 316);
		lblSfx.Name = "lblSfx";
		lblSfx.PixelScale = 2;
		lblSfx.Size = new Size(432, 26);
		lblSfx.Text = "Âm lượng hiệu ứng";
		//
		// sldSfx
		//
		sldSfx.Location = new Point(64, 346);
		sldSfx.Name = "sldSfx";
		sldSfx.Size = new Size(432, 34);
		sldSfx.TabIndex = 2;
		sldSfx.Value = 100;
		sldSfx.ValueChanged += sldSfx_ValueChanged;
		sldSfx.Committed += sldSfx_Committed;
		//
		// btnAutoAim
		//
		btnAutoAim.Location = new Point(60, 402);
		btnAutoAim.Name = "btnAutoAim";
		btnAutoAim.Size = new Size(440, 46);
		btnAutoAim.TabIndex = 3;
		btnAutoAim.Text = "Tự động ngắm: BẬT";
		btnAutoAim.Click += btnAutoAim_Click;
		//
		// btnFullscreen
		//
		btnFullscreen.Location = new Point(60, 458);
		btnFullscreen.Name = "btnFullscreen";
		btnFullscreen.Size = new Size(440, 46);
		btnFullscreen.TabIndex = 4;
		btnFullscreen.Text = "Toàn màn hình: TẮT";
		btnFullscreen.Click += btnFullscreen_Click;
		//
		// btnHitbox
		//
		btnHitbox.Location = new Point(60, 514);
		btnHitbox.Name = "btnHitbox";
		btnHitbox.Size = new Size(440, 46);
		btnHitbox.TabIndex = 5;
		btnHitbox.Text = "Hiện hitbox: TẮT";
		btnHitbox.Click += btnHitbox_Click;
		//
		// lblNote
		//
		lblNote.ForeColor = Color.FromArgb(120, 128, 150);
		lblNote.Location = new Point(40, 572);
		lblNote.Name = "lblNote";
		lblNote.PixelScale = 1;
		lblNote.Size = new Size(480, 34);
		lblNote.Text = "Cài đặt được lưu tự động vào settings.txt. Trong lúc chơi: F bật/tắt tự động ngắm (Bow, Holy Cross; Claw luôn ngắm bằng chuột), M bật/tắt âm thanh, Esc tạm dừng.";
		//
		// btnClose
		//
		btnClose.Highlight = true;
		btnClose.Location = new Point(190, 616);
		btnClose.Name = "btnClose";
		btnClose.Size = new Size(180, 46);
		btnClose.TabIndex = 6;
		btnClose.Text = "Đóng";
		btnClose.Click += btnClose_Click;
		//
		// SettingsForm
		//
		AcceptButton = btnClose;
		AutoScaleDimensions = new SizeF(96F, 96F);
		AutoScaleMode = AutoScaleMode.Dpi;
		CancelButton = btnClose;
		ClientSize = new Size(560, 688);
		Controls.Add(lblTitle);
		Controls.Add(btnDifficulty);
		Controls.Add(lblDiffNote);
		Controls.Add(btnSound);
		Controls.Add(lblMusic);
		Controls.Add(sldMusic);
		Controls.Add(lblSfx);
		Controls.Add(sldSfx);
		Controls.Add(btnAutoAim);
		Controls.Add(btnFullscreen);
		Controls.Add(btnHitbox);
		Controls.Add(lblNote);
		Controls.Add(btnClose);
		FormBorderStyle = FormBorderStyle.FixedDialog;
		MaximizeBox = false;
		MinimizeBox = false;
		Name = "SettingsForm";
		ShowInTaskbar = false;
		StartPosition = FormStartPosition.CenterParent;
		Text = "Cài đặt";
		ResumeLayout(false);
	}

	#endregion

	private PixelLabel lblTitle;
	private PixelButton btnDifficulty;
	private PixelLabel lblDiffNote;
	private PixelButton btnSound;
	private PixelLabel lblMusic;
	private PixelSlider sldMusic;
	private PixelLabel lblSfx;
	private PixelSlider sldSfx;
	private PixelButton btnAutoAim;
	private PixelButton btnFullscreen;
	private PixelButton btnHitbox;
	private PixelLabel lblNote;
	private PixelButton btnClose;
}
