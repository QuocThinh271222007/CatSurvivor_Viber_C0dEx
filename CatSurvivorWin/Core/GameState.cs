namespace CatSurvivor;

/// <summary>Thời gian chơi, kills, level/XP của một ván.</summary>
public sealed class GameState
{
	/// <summary>Bắn mỗi lần lên level (có thể bắn nhiều lần nếu nhặt nhiều XP một lúc).</summary>
	public event Action LeveledUp;

	/// <summary>Cấp tối đa (khoá ở đây). Đạt cấp này -> bắn ReachedMaxLevel, BossArena mở màn đánh boss sau khi menu nâng cấp cuối đóng.</summary>
	public const int MaxLevel = Balance.MaxPlayerLevel;
	public event Action ReachedMaxLevel;

	/// <summary>Đang trong màn đánh boss (vùng đã khoá, ngừng sinh quái thường).</summary>
	public bool BossActive { get; set; }
	public bool BossDefeated { get; set; }

	/// <summary>Độ khó của ván này (chốt lúc bắt đầu ván): điểm thành tích được nhân theo độ khó.</summary>
	public DifficultyLevel DifficultyMode { get; } = Difficulty.Level;

	public bool Running { get; set; } = true;
	public float Time { get; private set; }
	public int Kills { get; set; }
	public int Level { get; private set; } = 1;
	public float Xp { get; private set; }
	public float XpToNext { get; private set; } = XpForLevel(1);

	/// <summary>Đã dùng cheat trong ván này (HUD hiện nhãn DEBUG).</summary>
	public bool CheatsUsed { get; set; }

	/// <summary>Cheat (debug): đặt lại thời gian chơi (giây).</summary>
	public void SetTime(float seconds) => Time = MathF.Max(0f, seconds);

	public void Update(float dt)
	{
		if (Running) Time += dt;
	}

	/// <summary>
	/// Điểm thành tích = số quái hạ x 10 + (cấp - 1) x 100; hạ boss thì +5000 và thưởng thêm 2 điểm cho mỗi giây
	/// còn lại so với mốc 1800 giây (30 phút). Tổng được NHÂN theo độ khó (Dễ x1, Trung bình x2, Khó x4, Siêu khó x6).
	/// </summary>
	public int ComputeScore(bool victory) => ComputeBaseScore(victory) * Difficulty.ScoreMul(DifficultyMode);

	/// <summary>Điểm trước khi nhân độ khó.</summary>
	public int ComputeBaseScore(bool victory)
	{
		int score = Kills * 10 + (Level - 1) * 100;
		if (victory) score += 5000 + Math.Max(0, 1800 - (int)Time) * 2;
		return score;
	}

	public void AddXp(float amount)
	{
		if (Level >= MaxLevel) return; // đã khoá ở cấp tối đa: XP không còn tác dụng

		Xp += amount;
		while (Xp >= XpToNext)
		{
			Xp -= XpToNext;
			Level++;
			XpToNext = XpForLevel(Level);
			LeveledUp?.Invoke();

			if (Level >= MaxLevel)
			{
				Xp = 0f;
				ReachedMaxLevel?.Invoke();
				break;
			}
		}
	}

	/// <summary>XP cần để lên level kế tiếp: tăng dần theo level.</summary>
	private static float XpForLevel(int level) => 4f + level * 3f;
}
