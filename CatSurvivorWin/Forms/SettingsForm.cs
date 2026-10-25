namespace CatSurvivor;

/// <summary>
/// Menu Cài đặt (mở từ menu chính và menu Tạm dừng; KHÔNG hiện lúc đang chơi): bật/tắt âm thanh, âm lượng nhạc nền,
/// âm lượng hiệu ứng, toàn màn hình, độ khó (chỉ đổi được ở menu chính). Thay đổi áp dụng ngay và được lưu vào settings.txt khi đóng (Settings.Save).
/// </summary>
public partial class SettingsForm : SkinnedForm
{
	private readonly bool _lockDifficulty;

	/// <param name="lockDifficulty">true khi mở từ menu Tạm dừng (đang giữa một ván): không cho đổi độ khó, phải đổi ở menu chính.</param>
	public SettingsForm(bool lockDifficulty = false)
	{
		_lockDifficulty = lockDifficulty;
		InitializeComponent();
		sldMusic.Value = Settings.MusicVolume;
		sldSfx.Value = Settings.SfxVolume;
		UpdateTexts(Settings.Sound, Settings.Fullscreen);
		SoundService.EnabledChanged += OnSoundChanged;
	}

	private void UpdateTexts(bool sound, bool fullscreen)
	{
		btnSound.Text = sound ? "Âm thanh: BẬT" : "Âm thanh: TẮT";
		btnFullscreen.Text = fullscreen ? "Toàn màn hình: BẬT" : "Toàn màn hình: TẮT";
		btnHitbox.Text = Settings.ShowHitbox ? "Hiện hitbox: BẬT" : "Hiện hitbox: TẮT";
		btnAutoAim.Text = Settings.AutoAim ? "Tự động ngắm: BẬT" : "Tự động ngắm: TẮT (ngắm bằng chuột)";
		sldMusic.Enabled = sldSfx.Enabled = sound;

		var level = Difficulty.Level;
		btnDifficulty.Text = _lockDifficulty ? $"Độ khó: {Difficulty.Name(level)} (đổi ở menu chính)" : $"Độ khó: {Difficulty.Name(level)}";
		btnDifficulty.Enabled = !_lockDifficulty;
		lblDiffNote.Text = Difficulty.Describe(level);
	}

	private void OnSoundChanged(bool on) => UpdateTexts(on, Settings.Fullscreen); // phím M cũng cập nhật được nút này

	protected override void OnFormClosed(FormClosedEventArgs e)
	{
		SoundService.EnabledChanged -= OnSoundChanged;
		Settings.Save();
		base.OnFormClosed(e);
	}

	private void btnSound_Click(object sender, EventArgs e) => SoundService.Toggle();

	private void sldMusic_ValueChanged(object sender, EventArgs e) => SoundService.SetMusicVolume(sldMusic.Value);
	private void sldSfx_ValueChanged(object sender, EventArgs e) => SoundService.SetSfxVolume(sldSfx.Value);

	private void sldCommitted(object sender, EventArgs e) => Settings.Save();

	private void sldSfx_Committed(object sender, EventArgs e)
	{
		Settings.Save();
		SoundService.Play("pickup", -4f, 0f); // nghe thử mức âm lượng hiệu ứng
	}

	private void btnDifficulty_Click(object sender, EventArgs e)
	{
		Settings.Difficulty = (Mx.Clamp(Settings.Difficulty, 0, 3) + 1) % 4;   // Dễ -> Trung bình -> Khó -> Siêu khó -> Dễ
		Settings.Save();
		UpdateTexts(Settings.Sound, Settings.Fullscreen);
	}

	private void btnHitbox_Click(object sender, EventArgs e)
	{
		Settings.ShowHitbox = !Settings.ShowHitbox;
		Settings.Save();
		UpdateTexts(Settings.Sound, Settings.Fullscreen);
	}

	private void btnAutoAim_Click(object sender, EventArgs e)
	{
		Settings.AutoAim = !Settings.AutoAim;
		Settings.Save();
		UpdateTexts(Settings.Sound, Settings.Fullscreen);
	}

	private void btnFullscreen_Click(object sender, EventArgs e)
	{
		Settings.Fullscreen = !Settings.Fullscreen;
		Settings.Save();
		UpdateTexts(Settings.Sound, Settings.Fullscreen);
	}

	private void btnClose_Click(object sender, EventArgs e) => Close();
}
