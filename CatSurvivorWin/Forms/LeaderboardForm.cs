namespace CatSurvivor;

/// <summary>
/// Bảng thành tích (xếp hạng): đọc thanhtichs.txt, hiện top 10 theo điểm. `highlight`: dòng vừa lưu sẽ được tô vàng.
/// Lỗi đọc file được báo bằng nhãn + hộp thông báo, không làm văng game. Giao diện dùng skin chung (xem docs/UI_ASSETS.md).
/// </summary>
public partial class LeaderboardForm : SkinnedForm
{
	public LeaderboardForm(ScoreEntry highlight = null)
	{
		InitializeComponent();

		tblScores.AddColumn("Hạng", 56, HorizontalAlignment.Center);
		tblScores.AddColumn("Tên", 150);
		tblScores.AddColumn("Điểm", 90, HorizontalAlignment.Right);
		tblScores.AddColumn("Độ khó", 100, HorizontalAlignment.Center);
		tblScores.AddColumn("Thời gian", 100, HorizontalAlignment.Center);
		tblScores.AddColumn("Cấp", 50, HorizontalAlignment.Center);
		tblScores.AddColumn("Kills", 66, HorizontalAlignment.Right);
		tblScores.AddColumn("Kết quả", 128, HorizontalAlignment.Center);
		tblScores.AddColumn("Ngày giờ", 160, HorizontalAlignment.Center);
		Fill(highlight);
	}

	private void Fill(ScoreEntry highlight)
	{
		var scores = Storage.ReadScores(out string error);
		tblScores.ClearRows();

		int rank = 0;
		foreach (var s in scores)
		{
			if (++rank > 10) break;
			bool mine = highlight != null && s.Name == highlight.Name && s.Score == highlight.Score && s.Date == highlight.Date;
			tblScores.Rows.Add(new PixelTableRow
			{
				Cells = new[]
				{
					rank.ToString(), s.Name, s.Score.ToString(), Difficulty.Name((DifficultyLevel)s.Mode),
					$"{s.Seconds / 60:00}:{s.Seconds % 60:00}", s.Level.ToString(), s.Kills.ToString(), s.Victory ? "Thắng boss" : "Thua", s.Date,
				},
				Highlight = mine,
				Color = !mine && rank == 1 ? UiSkin.Accent.Lerp(Rgba.White, 0.25f) : null,
			});
		}
		tblScores.Invalidate();

		if (error != null)
		{
			lblStatus.ForeColor = Color.FromArgb(255, 107, 92);
			lblStatus.Text = error;
			MessageBox.Show(this, error, "Lỗi bảng thành tích", MessageBoxButtons.OK, MessageBoxIcon.Warning);
		}
		else if (scores.Count == 0)
		{
			lblStatus.Text = "";
		}
		else
		{
			lblStatus.Text = $"Hiển thị top {Math.Min(10, scores.Count)} / {scores.Count} lượt chơi  •  file: {Storage.ScoreFile}";
		}
	}

	private void btnClose_Click(object sender, EventArgs e) => Close();
}
