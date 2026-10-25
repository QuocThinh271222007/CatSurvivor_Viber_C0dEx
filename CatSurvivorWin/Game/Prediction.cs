using System.Numerics;

namespace CatSurvivor;

/// <summary>
/// Quái DỰ ĐOÁN ĐƯỜNG ĐI của mèo để chặn đầu và tung chiêu đúng chỗ (thay vì đuổi theo / ngắm vào chỗ mèo ĐANG đứng).
/// Mức độ phụ thuộc độ khó (Difficulty.Prediction*):
///   Dễ: tắt · Trung bình: yếu (tin 30%, nhìn trước tối đa 0.5s, vận tốc mèo bị làm mượt nên chậm nhận ra đổi hướng)
///   Khó: hiệu quả (80%, 1s, vận tốc trung bình) · Siêu khó: cực kì hiệu quả (100%, 2s, vận tốc gần tức thời, giải đúng bài toán chặn đầu).
/// Dùng ở: Slime (đuổi + vồ), Brute (đuổi + lao), Bat (chọn chỗ đứng + thời điểm gào), Bomber (từ Khó), Boss (đuổi + lao).
/// Mọi chỗ gọi đều đi qua đây nên ở Dễ kết quả y hệt cũ (InterceptPoint trả về đúng chỗ mèo đang đứng).
/// </summary>
public static class Prediction
{
	/// <summary>Vận tốc mèo mà quái dùng để dự đoán: Trung bình dùng bản làm mượt chậm, Siêu khó dùng bản gần tức thời.</summary>
	private static Vector2 PlayerVelocity(Player p) => Difficulty.Level switch
	{
		DifficultyLevel.Normal => p.VelSlow,
		DifficultyLevel.Hard => (p.VelSlow + p.VelFast) * 0.5f,
		DifficultyLevel.Nightmare => p.VelFast,
		_ => Vector2.Zero,
	};

	/// <summary>
	/// Điểm quái nên nhắm tới để chặn đầu mèo. `from` = chỗ quái, `speed` = tốc độ quái sẽ chạy/lao,
	/// `delay` = thời gian còn lại trước khi quái thực sự lao đi (gồng chiêu): mèo vẫn di chuyển trong lúc đó.
	/// </summary>
	public static Vector2 InterceptPoint(Vector2 from, Player p, float speed, float delay = 0f, BossArena arena = null)
	{
		Vector2 pos = p.Pos;
		float strength = Difficulty.PredictionStrength;
		if (strength <= 0f) return pos;

		Vector2 v = PlayerVelocity(p);
		float s = MathF.Max(speed, 1f);
		Vector2 d = pos + v * delay - from;       // mèo sẽ ở đâu khi quái bắt đầu lao
		float t = -1f;
		if (Difficulty.PredictionExact) t = SolveIntercept(d, v, s);
		if (t < 0f) t = d.Length() / s;           // không giải được (hoặc mức thường): thời gian đi thẳng tới chỗ đó

		float total = MathF.Min(delay + t, Difficulty.PredictionHorizon);
		Vector2 aim = pos + v * total * strength;
		return arena != null ? arena.Clamp(aim, 0f) : aim;   // đang đánh boss: mèo không ra khỏi vùng khoá, đừng nhắm ra ngoài
	}

	/// <summary>Hướng đơn vị từ `from` tới điểm chặn đầu; `fallback` (thường là hướng thẳng tới mèo) khi không tính được.</summary>
	public static Vector2 Dir(Vector2 from, Player p, float speed, Vector2 fallback, float delay = 0f, BossArena arena = null)
	{
		if (Difficulty.PredictionStrength <= 0f) return fallback;
		Vector2 to = InterceptPoint(from, p, speed, delay, arena) - from;
		return to.LengthSquared() > 1f ? to.Normalized() : fallback;
	}

	/// <summary>Chỗ mèo sẽ đứng sau `seconds` giây (theo mức dự đoán hiện tại); dùng để quyết định có nên tung chiêu không.</summary>
	public static Vector2 PositionAfter(Player p, float seconds, BossArena arena = null)
	{
		float strength = Difficulty.PredictionStrength;
		if (strength <= 0f) return p.Pos;
		Vector2 aim = p.Pos + PlayerVelocity(p) * MathF.Min(seconds, Difficulty.PredictionHorizon) * strength;
		return arena != null ? arena.Clamp(aim, 0f) : aim;
	}

	/// <summary>
	/// Thời gian t nhỏ nhất > 0 để quái chạy tốc độ `s` gặp mèo đang ở lệch `d` và chạy với vận tốc `v`:
	/// |d + v t| = s t  =>  (v.v - s²) t² + 2 (d.v) t + d.d = 0. Trả -1 nếu không có nghiệm (mèo chạy thoát được).
	/// </summary>
	private static float SolveIntercept(Vector2 d, Vector2 v, float s)
	{
		float a = Vector2.Dot(v, v) - s * s;
		float b = 2f * Vector2.Dot(d, v);
		float c = Vector2.Dot(d, d);
		if (MathF.Abs(a) < 1e-3f) return b < 0f ? -c / b : -1f;
		float disc = b * b - 4f * a * c;
		if (disc < 0f) return -1f;
		float sq = MathF.Sqrt(disc);
		float t1 = (-b - sq) / (2f * a), t2 = (-b + sq) / (2f * a);
		float best = float.MaxValue;
		if (t1 > 0f) best = MathF.Min(best, t1);
		if (t2 > 0f) best = MathF.Min(best, t2);
		return best == float.MaxValue ? -1f : best;
	}
}
