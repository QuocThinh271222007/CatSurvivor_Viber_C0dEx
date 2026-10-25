namespace CatSurvivor;

public enum MenuChoice { Start, Exit }

/// <summary>
/// Màn hình Start: tên game + các nút (Bắt đầu, Bảng thành tích, Hướng dẫn, Thông tin nhóm, Cài đặt, Thoát).
/// Giao diện dùng skin chung (xem Rendering/UiSkin.cs và docs/UI_ASSETS.md để thay asset nút / nền / logo).
/// </summary>
public partial class MainMenuForm : SkinnedForm
{
	public MenuChoice Choice { get; private set; } = MenuChoice.Exit;

	public MainMenuForm()
	{
		InitializeComponent();
		Shown += (_, _) => Program.ShowAssetReport(this);
	}

	private void btnStart_Click(object sender, EventArgs e)
	{
		Choice = MenuChoice.Start;
		Close();
	}

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

	private void btnAbout_Click(object sender, EventArgs e)
	{
		using var f = new AboutForm();
		f.ShowDialog(this);
	}

	private void btnSettings_Click(object sender, EventArgs e)
	{
		using var f = new SettingsForm();
		f.ShowDialog(this);
	}

	private void btnExit_Click(object sender, EventArgs e)
	{
		Choice = MenuChoice.Exit;
		Close();
	}
}
