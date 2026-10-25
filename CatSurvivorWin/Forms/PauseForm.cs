namespace CatSurvivor;

public enum PauseAction { Resume, Restart, Menu, Quit }

/// <summary>
/// Menu tạm dừng (Esc hoặc P): Tiếp tục, Chơi lại, Bảng thành tích, Hướng dẫn, Cài đặt, Về menu chính, Thoát.
/// Giao diện dùng skin chung (xem Rendering/UiSkin.cs và docs/UI_ASSETS.md).
/// </summary>
public partial class PauseForm : SkinnedForm
{
	public PauseAction Action { get; private set; } = PauseAction.Resume;

	public PauseForm()
	{
		InitializeComponent();
	}

	private void Choose(PauseAction a)
	{
		Action = a;
		Close();
	}

	private void btnResume_Click(object sender, EventArgs e) => Choose(PauseAction.Resume);
	private void btnRestart_Click(object sender, EventArgs e) => Choose(PauseAction.Restart);
	private void btnMenu_Click(object sender, EventArgs e) => Choose(PauseAction.Menu);
	private void btnQuit_Click(object sender, EventArgs e) => Choose(PauseAction.Quit);

	private void btnLeaderboard_Click(object sender, EventArgs e)
	{
		using var f = new LeaderboardForm();
		f.ShowDialog(this);
	}

	private void btnHelp_Click(object sender, EventArgs e)
	{
		using var f = new HelpForm();
		f.ShowDialog(this);
	}

	private void btnSettings_Click(object sender, EventArgs e)
	{
		using var f = new SettingsForm(lockDifficulty: true);
		f.ShowDialog(this);
	}

	private void PauseForm_KeyDown(object sender, KeyEventArgs e)
	{
		// Esc / P lần nữa = tiếp tục
		if (e.KeyCode == Keys.Escape || e.KeyCode == Keys.P) { Action = PauseAction.Resume; Close(); }
		else if (e.KeyCode == Keys.M) SoundService.Toggle();
	}
}
