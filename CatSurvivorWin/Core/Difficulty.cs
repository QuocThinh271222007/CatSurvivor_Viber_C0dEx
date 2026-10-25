namespace CatSurvivor;

public enum DifficultyLevel { Easy = 0, Normal = 1, Hard = 2, Nightmare = 3 }

/// <summary>
/// Độ khó (chọn ở Cài đặt của MENU CHÍNH - đang chơi không đổi được). Các mức cộng dồn:
///  - Dễ        : quái bình thường (x1); KHÔNG có dự đoán đường đi; vẫn vẽ vùng báo hiệu đòn đánh của quái
///                (phễu dơi, vòng nổ bomber, dải lao, vòng giậm của boss...).
///  - Trung bình: quái (cả boss) máu x2, sát thương x2; quái dự đoán đường đi YẾU; còn vùng báo hiệu.
///  - Khó       : quái máu x4, sát thương x4; dự đoán đường đi HIỆU QUẢ; TẮT vùng báo hiệu đòn đánh - chỉ còn nhìn hoạt ảnh quái để đoán đòn.
///  - Siêu khó  : quái máu x6, sát thương x6; dự đoán đường đi CỰC KÌ HIỆU QUẢ; KHÔNG thể hồi máu (hồi tự động, bình máu rơi,
///                hút máu...) và các thẻ nâng cấp liên quan máu (Máu tối đa, Hồi máu, Gà quay) không xuất hiện; giáp vẫn có.
/// Điểm thành tích cũng được nhân theo độ khó (ScoreMul). Xem Prediction.cs cho cơ chế dự đoán.
/// </summary>
public static class Difficulty
{
	public static DifficultyLevel Level => (DifficultyLevel)Mx.Clamp(Settings.Difficulty, 0, 3);

	/// <summary>Hệ số nhân máu của quái (cả boss): Dễ x1, Trung bình x2, Khó x4, Siêu khó x6.</summary>
	public static float EnemyHpMul => StatMul(Level);

	/// <summary>Hệ số nhân sát thương của quái (cả boss): cùng bảng với máu.</summary>
	public static float EnemyDamageMul => StatMul(Level);

	public static float StatMul(DifficultyLevel l) => l switch
	{
		DifficultyLevel.Easy => 1f,
		DifficultyLevel.Normal => 2f,
		DifficultyLevel.Hard => 4f,
		_ => 6f,
	};

	/// <summary>Hệ số nhân ĐIỂM thành tích theo độ khó (hiện cùng bảng với hệ số quái; sửa ở đây nếu muốn tách).</summary>
	public static int ScoreMul(DifficultyLevel l) => (int)StatMul(l);

	/// <summary>Có vẽ vùng báo hiệu đòn đánh của quái không (Dễ, Trung bình: có).</summary>
	public static bool ShowEnemyTelegraphs => Level <= DifficultyLevel.Normal;

	/// <summary>Có được hồi máu không (Siêu khó: không).</summary>
	public static bool AllowHealing => Level < DifficultyLevel.Nightmare;

	// ---------------------------------------------------------------- dự đoán đường đi của quái (xem Prediction.cs)
	/// <summary>Mức quái tin vào dự đoán (0 = đuổi thẳng vào chỗ mèo đang đứng, 1 = nhắm hẳn vào chỗ mèo sẽ tới): 0 / 0.3 / 0.8 / 1.</summary>
	public static float PredictionStrength => Level switch
	{
		DifficultyLevel.Easy => 0f,
		DifficultyLevel.Normal => 0.3f,
		DifficultyLevel.Hard => 0.8f,
		_ => 1f,
	};

	/// <summary>Quái nhìn trước tối đa bao nhiêu giây: 0 / 0.5 / 1.0 / 2.0.</summary>
	public static float PredictionHorizon => Level switch
	{
		DifficultyLevel.Easy => 0f,
		DifficultyLevel.Normal => 0.5f,
		DifficultyLevel.Hard => 1f,
		_ => 2f,
	};

	/// <summary>Siêu khó: giải đúng bài toán chặn đầu (tính cả tốc độ chạy của quái) thay vì chỉ cộng thêm vận tốc.</summary>
	public static bool PredictionExact => Level == DifficultyLevel.Nightmare;

	// ---------------------------------------------------------------- boss thông minh hơn theo độ khó (xem BossAI.cs)
	/// <summary>Mức "biết nhìn tình huống" khi chọn đòn (0 = ngẫu nhiên như cũ, 1 = luôn chọn đòn hợp tình huống nhất): 0 / 0.35 / 0.75 / 1.</summary>
	public static float BossSmartness => Level switch
	{
		DifficultyLevel.Easy => 0f,
		DifficultyLevel.Normal => 0.35f,
		DifficultyLevel.Hard => 0.75f,
		_ => 1f,
	};

	/// <summary>Boss chạy / gồng / lao nhanh hơn: x1 / 1.08 / 1.2 / 1.35.</summary>
	public static float BossSpeedMul => Level switch { DifficultyLevel.Easy => 1f, DifficultyLevel.Normal => 1.08f, DifficultyLevel.Hard => 1.2f, _ => 1.35f };

	/// <summary>Hồi chiêu giữa hai đòn của boss nhân với hệ số này: x1 / 0.9 / 0.7 / 0.5.</summary>
	public static float BossCooldownMul => Level switch { DifficultyLevel.Easy => 1f, DifficultyLevel.Normal => 0.9f, DifficultyLevel.Hard => 0.7f, _ => 0.5f };

	/// <summary>Thời gian kiệt sức sau khi lao đâm tường nhân với hệ số này (cửa sổ phản công ngắn lại): x1 / 0.85 / 0.6 / 0.35.</summary>
	public static float BossStunMul => Level switch { DifficultyLevel.Easy => 1f, DifficultyLevel.Normal => 0.85f, DifficultyLevel.Hard => 0.6f, _ => 0.35f };

	/// <summary>Tốc độ bẻ lái (rad/giây) của boss ĐANG lao để bám theo chỗ mèo sẽ tới: 0 / 0 / 0.9 / 2.2.</summary>
	public static float BossHoming => Level switch { DifficultyLevel.Easy => 0f, DifficultyLevel.Normal => 0f, DifficultyLevel.Hard => 0.9f, _ => 2.2f };

	/// <summary>Xác suất boss tung NGAY đòn kế tiếp (đòn liên hoàn) thay vì nghỉ hết hồi chiêu: 0 / 0.1 / 0.25 / 0.5.</summary>
	public static float BossComboChance => Level switch { DifficultyLevel.Easy => 0f, DifficultyLevel.Normal => 0.1f, DifficultyLevel.Hard => 0.25f, _ => 0.5f };

	/// <summary>Siêu khó: boss biết dừng lao trước khi đâm tường nên không tự choáng.</summary>
	public static bool BossAvoidsWalls => Level == DifficultyLevel.Nightmare;
	/// <summary>Khó / Siêu khó: cứ mỗi chừng này giây (0 = không có) mắt chính gọi BÙ mắt con (đủ lại 4 con) nếu đã mất con nào, khiên bất khả xâm phạm bật lại.</summary>
	public static float BossResummonInterval => Level >= DifficultyLevel.Hard ? 40f : 0f;

	public static string Name(DifficultyLevel l) => l switch
	{
		DifficultyLevel.Easy => "Dễ",
		DifficultyLevel.Normal => "Trung bình",
		DifficultyLevel.Hard => "Khó",
		_ => "Siêu khó",
	};

	public static string Describe(DifficultyLevel l) => l switch
	{
		DifficultyLevel.Easy => "Quái bình thường, không dự đoán đường đi. Có vùng báo hiệu đòn đánh. Điểm x1.",
		DifficultyLevel.Normal => "Quái máu x2, sát thương x2, dự đoán đường đi yếu. Có vùng báo hiệu đòn đánh. Điểm x2.",
		DifficultyLevel.Hard => "Quái máu x4, sát thương x4, dự đoán đường đi hiệu quả, KHÔNG hiện vùng báo hiệu đòn đánh. Điểm x4.",
		_ => "Quái máu x6, sát thương x6, dự đoán cực kì chuẩn, không hồi máu, không thẻ nâng cấp máu (trừ giáp). Điểm x6.",
	};
}
