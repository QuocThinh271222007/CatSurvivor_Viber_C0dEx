namespace CatSurvivor;

/// <summary>
/// Màn hình kết thúc (thua hoặc thắng boss): hiện kết quả + điểm, nhập tên để LƯU thành tích vào file,
/// xem bảng xếp hạng, chơi lại, về menu chính hoặc thoát. Form tạm: tự thiết kế lại trong Designer.
/// </summary>
public partial class GameOverForm : SkinnedForm
{
	private static string _lastName = "";   // nhớ tên vừa nhập cho lần sau

	private readonly GameState _state;
	private readonly bool _victory;
	private bool _saved;

	public GameOutcome Outcome { get; private set; } = GameOutcome.Menu;

	public GameOverForm(GameState state, bool victory)
	{
		InitializeComponent();
		StyleField(txtName);
		_state = state;
		_victory = victory;

		int t = (int)state.Time;
		lblTitle.Text = victory ? "CHIẾN THẮNG!" : "BẠN ĐÃ CHẾT";
		lblTitle.ForeColor = victory ? Color.FromArgb(140, 242, 140) : Color.FromArgb(255, 107, 92);
		lblStats.Text = (victory ? $"Bạn đã hạ {BossArena.BossName}\n" : "") + $"Thời gian {t / 60:00}:{t % 60:00}   Cấp {state.Level}   Kills {state.Kills}\nĐộ khó {Difficulty.Name(state.DifficultyMode)}   ĐIỂM: {state.ComputeScore(victory)}" + (Difficulty.ScoreMul(state.DifficultyMode) > 1 ? $" (x{Difficulty.ScoreMul(state.DifficultyMode)})" : "");
		txtName.Text = _lastName;
		SoundService.Play(victory ? "win" : "lose", 0f, 0.5f);
	}

	private void btnSave_Click(object sender, EventArgs e) => Save();

	private void txtName_KeyDown(object sender, KeyEventArgs e)
	{
		if (e.KeyCode == Keys.Enter) { Save(); e.SuppressKeyPress = true; }
	}

	/// <summary>Ghi thành tích vào thanhtichs.txt (System.IO, có try...catch) rồi mở bảng xếp hạng với dòng vừa lưu được tô đậm.</summary>
	private void Save()
	{
		if (_saved) return;
		string name = txtName.Text.Trim();
		if (name.Length == 0) name = "Ẩn danh";
		_lastName = name;

		var entry = new ScoreEntry
		{
			Name = name,
			Score = _state.ComputeScore(_victory),
			Seconds = (int)_state.Time,
			Level = _state.Level,
			Kills = _state.Kills,
			Victory = _victory,
			Date = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
			Mode = (int)_state.DifficultyMode,
		};

		if (Storage.AppendScore(entry, out string error))
		{
			_saved = true;
			btnSave.Enabled = false;
			lblMessage.Text = "Đã lưu thành tích!";
			if (SansEasterEgg.Matches(name)) SansEasterEgg.Run(this);   // easter egg: lưu thành tích với tên SANS (hoa/thường tuỳ ý) -> đánh nhau với Sans
			using var f = new LeaderboardForm(entry);
			f.ShowDialog(this);
		}
		else
		{
			lblMessage.Text = "";
			MessageBox.Show(this, error, "Lỗi ghi file", MessageBoxButtons.OK, MessageBoxIcon.Error); // báo lỗi thay vì để game văng
		}
	}

	private void btnLeaderboard_Click(object sender, EventArgs e)
	{
		using var f = new LeaderboardForm();
		f.ShowDialog(this);
	}

	private void LeaveWith(GameOutcome outcome)
	{
		Outcome = outcome;
		Close();
	}

	private void btnRestart_Click(object sender, EventArgs e) => LeaveWith(GameOutcome.Restart);
	private void btnMenu_Click(object sender, EventArgs e) => LeaveWith(GameOutcome.Menu);
	private void btnQuit_Click(object sender, EventArgs e) => LeaveWith(GameOutcome.Quit);
}
