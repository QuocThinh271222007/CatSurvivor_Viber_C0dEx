using System.Numerics;

namespace CatSurvivor;

/// <summary>
/// Kĩ năng CHỦ ĐỘNG của mèo, tốn MANA. MỖI VÁN CHỈ CÓ MỘT kĩ năng, chọn bằng lá bài trước khi vào ván (xem SkillPick / Program); dùng phím Q hoặc E. (thanh xanh dương; xem Player.Mana / TrySpendMana):
///  - [Q] Sóng xung kích (Nova): một đợt sóng quanh mèo gây sát thương và hất văng mọi quái trong tầm.
///  - [E] Làm chậm thời gian: quái (kể cả boss, nhẹ hơn) chạy và ra đòn chậm lại trong vài giây; mèo vẫn chạy bình thường.
/// Chưa chọn (Chosen == null, kiểm thử) thì Q = Nova, E = Warp. Phím được GameInput xếp hàng khi bấm (không giữ). Hiển thị ở HUD (Hud.DrawSkills), hiệu ứng vẽ ở Draw(). Chưa có asset -> hình vẽ tạm bằng hình học;
/// khi có icon/hiệu ứng tự vẽ thì thay trong Hud.DrawSkills và SkillSet.Draw.
/// </summary>
public enum SkillKind { Nova, Warp, Stop, Guard }

public sealed class SkillSet
{
	/// <summary>Kĩ năng người chơi đã chọn cho ván sắp chơi (SkillPickForm đặt trước khi tạo World). null = chưa chọn: dùng được cả hai (kiểm thử, cheat).</summary>
	public static SkillKind? RunChoice;

	public static string NameOf(SkillKind k) => k switch
	{
		SkillKind.Nova => "Sóng xung kích",
		SkillKind.Warp => "Làm chậm thời gian",
		SkillKind.Stop => "Ngừng thời gian",
		_ => "Bất tử",
	};

	// ---------------------------------------------------------------- thông số
	public const float NovaCost = 30f;
	public const float NovaCooldown = 1.0f;       // giây giữa hai lần xả Nova
	public const float NovaRadius = 120f;
	private const float NovaDamage = 60f;          // x DamageMul của mèo
	private const float NovaKnock = 360f;          // px/s, chia cho Weight quái
	private const float NovaAnim = 0.4f;           // thời gian hiệu ứng sóng

	public const float WarpCost = 45f;
	public const float WarpCooldown = 8f;          // tính từ lúc bật
	public const float WarpDuration = 4f;
	private const float WarpScale = 0.45f;         // quái thường chỉ còn 45% tốc độ
	private const float WarpBossScale = 0.7f;      // boss chỉ chậm nhẹ hơn (70%)
	private const float WarpFade = 0.4f;           // thời gian vào/ra của lớp phủ màn hình

	public const float StopCost = 60f;
	public const float StopCooldown = 15f;         // tính từ lúc bật
	public const float StopDuration = 1.5f;        // mọi quái (kể cả boss) và đòn của chúng đứng yên hoàn toàn

	public const float GuardCost = 50f;
	public const float GuardCooldown = 12f;        // tính từ lúc bật
	public const float GuardDuration = 2f;         // mèo bất tử

	private readonly Player _p;
	private readonly List<Enemy> _hits = new();
	private float _novaAge = -1f;

	public float NovaCd { get; private set; }
	public float WarpCd { get; private set; }
	public float WarpLeft { get; private set; }
	public float StopCd { get; private set; }
	public float StopLeft { get; private set; }
	public float GuardCd { get; private set; }
	public float GuardLeft { get; private set; }

	/// <summary>Kĩ năng của ván này (mỗi ván chỉ một, dùng phím Q hoặc E). null = cả hai: Q = Nova, E = Warp.</summary>
	public SkillKind? Chosen { get; }

	public SkillSet(Player p) { _p = p; Chosen = RunChoice; }

	public bool WarpActive => WarpLeft > 0f;
	public bool StopActive => StopLeft > 0f;
	public bool GuardActive => GuardLeft > 0f;

	/// <summary>Hệ số tốc độ thời gian của quái thường / boss (1 = bình thường).</summary>
	public float EnemyTimeScale => StopActive ? 0f : WarpActive ? WarpScale : 1f;
	public float BossTimeScale => StopActive ? 0f : WarpActive ? WarpBossScale : 1f;

	/// <summary>Độ đậm của lớp phủ "chậm thời gian" trên màn hình (0..1, vào/ra mượt).</summary>
	public float WarpOverlay => WarpActive ? Mx.Clamp(MathF.Min(WarpLeft, WarpDuration - WarpLeft) / WarpFade, 0f, 1f) : 0f;

	/// <summary>Độ đậm lớp phủ "ngừng thời gian" (0..1, vào/ra mượt).</summary>
	public float StopOverlay => StopActive ? Mx.Clamp(MathF.Min(StopLeft, StopDuration - StopLeft) / 0.2f, 0f, 1f) : 0f;

	public bool NovaReady => NovaCd <= 0f && _p.Mana >= NovaCost;
	public bool WarpReady => WarpCd <= 0f && _p.Mana >= WarpCost;

	// ---------------------------------------------------------------- cập nhật
	public void Update(float dt)
	{
		NovaCd = MathF.Max(NovaCd - dt, 0f);
		WarpCd = MathF.Max(WarpCd - dt, 0f);
		WarpLeft = MathF.Max(WarpLeft - dt, 0f);
		StopCd = MathF.Max(StopCd - dt, 0f);
		StopLeft = MathF.Max(StopLeft - dt, 0f);
		GuardCd = MathF.Max(GuardCd - dt, 0f);
		GuardLeft = MathF.Max(GuardLeft - dt, 0f);
		if (_novaAge >= 0f && (_novaAge += dt) > NovaAnim) _novaAge = -1f;

		var input = _p.W.Input;
		bool q = input.ConsumeSkill(0), e = input.ConsumeSkill(1);
		if (Chosen is SkillKind k)
		{
			if (q || e) Try(k);
		}
		else
		{
			if (q) TryNova();
			if (e) TryWarp();
		}
	}

	public bool Try(SkillKind k) => k switch
	{
		SkillKind.Nova => TryNova(),
		SkillKind.Warp => TryWarp(),
		SkillKind.Stop => TryStop(),
		_ => TryGuard(),
	};

	/// <summary>Xả Nova nếu hết hồi chiêu và đủ mana (cũng dùng cho cheat / kiểm thử).</summary>
	public bool TryNova()
	{
		if (_p.Dead || NovaCd > 0f) return false;
		if (!_p.TrySpendMana(NovaCost)) { _p.W.ShowToast("Không đủ mana (cần " + (int)NovaCost + ")", 1.2f); return false; }

		NovaCd = NovaCooldown;
		_novaAge = 0f;
		_p.W.Enemies.QueryCircle(_p.Pos, NovaRadius, _hits);
		foreach (var e in _hits)
		{
			if (e.Dead) continue;
			Vector2 away = e.Pos - _p.Pos;
			away = away.LengthSquared() > 0.01f ? away.Normalized() : V.FromAngle(Rng.Randf() * Mx.Tau);
			_p.W.Enemies.Damage(e, NovaDamage * _p.DamageMul, away * NovaKnock);
		}
		_p.W.AddShake(4f);
		SoundService.Play("slam", -2f, 0.1f);
		return true;
	}

	/// <summary>Bật làm chậm thời gian nếu hết hồi chiêu và đủ mana.</summary>
	public bool TryWarp()
	{
		if (_p.Dead || WarpCd > 0f) return false;
		if (!_p.TrySpendMana(WarpCost)) { _p.W.ShowToast("Không đủ mana (cần " + (int)WarpCost + ")", 1.2f); return false; }

		WarpCd = WarpCooldown;
		WarpLeft = WarpDuration;
		SoundService.Play("levelup", -2f, 0.2f);
		return true;
	}

	/// <summary>Ngừng thời gian: mọi quái và boss đứng yên 1.5 giây (mèo vẫn đi và đánh bình thường).</summary>
	public bool TryStop()
	{
		if (_p.Dead || StopCd > 0f) return false;
		if (!_p.TrySpendMana(StopCost)) { _p.W.ShowToast("Không đủ mana (cần " + (int)StopCost + ")", 1.2f); return false; }

		StopCd = StopCooldown;
		StopLeft = StopDuration;
		SoundService.Play("slam", -4f, 0.0f);
		return true;
	}

	/// <summary>Bất tử 2 giây: mọi sát thương lên mèo bị bỏ qua.</summary>
	public bool TryGuard()
	{
		if (_p.Dead || GuardCd > 0f) return false;
		if (!_p.TrySpendMana(GuardCost)) { _p.W.ShowToast("Không đủ mana (cần " + (int)GuardCost + ")", 1.2f); return false; }

		GuardCd = GuardCooldown;
		GuardLeft = GuardDuration;
		SoundService.Play("levelup", -2f, 0.1f);
		return true;
	}

	// ---------------------------------------------------------------- hiệu ứng (toạ độ cục bộ của mèo)
	public void Draw(Canvas c)
	{
		if (_novaAge >= 0f)
		{
			float k = _novaAge / NovaAnim;
			float ease = 1f - (1f - k) * (1f - k);
			float r = NovaRadius * (0.12f + 0.88f * ease);
			c.DrawCircle(Vector2.Zero, r, new Rgba(1f, 0.95f, 0.6f, 0.16f * (1f - k)));
			c.DrawArc(Vector2.Zero, r, 0f, Mx.Tau, new Rgba(1f, 0.95f, 0.65f, 0.95f * (1f - k)), 3f);
			c.DrawArc(Vector2.Zero, r * 0.82f, 0f, Mx.Tau, new Rgba(1f, 1f, 1f, 0.55f * (1f - k)), 1.5f);
		}

		if (GuardActive)
		{
			// Hào quang vàng quanh mèo; chớp nhanh khi sắp hết
			float pulse = 0.5f + 0.5f * MathF.Sin(Environment.TickCount64 / 1000f * (GuardLeft < 0.5f ? 28f : 9f));
			c.DrawCircle(Vector2.Zero, 20f, new Rgba(1f, 0.9f, 0.4f, 0.12f + 0.12f * pulse));
			c.DrawArc(Vector2.Zero, 20f, 0f, Mx.Tau, new Rgba(1f, 0.92f, 0.5f, 0.55f + 0.4f * pulse), 2f);
			c.DrawArc(Vector2.Zero, 24f, -Mx.Pi / 2f, -Mx.Pi / 2f + Mx.Tau * (GuardLeft / GuardDuration), new Rgba(1f, 1f, 0.8f, 0.85f), 2f);
		}

		if (StopActive)
		{
			// Vòng đồng hồ trắng: cung còn lại = thời gian còn lại
			c.DrawArc(Vector2.Zero, 29f, -Mx.Pi / 2f, -Mx.Pi / 2f + Mx.Tau * (StopLeft / StopDuration), new Rgba(0.95f, 0.95f, 1f, 0.9f), 2f);
		}

		if (WarpActive)
		{
			// Vòng đồng hồ quanh mèo: cung còn lại = thời gian còn lại
			float t = Environment.TickCount64 / 1000f;
			float left = WarpLeft / WarpDuration;
			c.DrawArc(Vector2.Zero, 26f + 1.5f * MathF.Sin(t * 6f), 0f, Mx.Tau, new Rgba(0.55f, 0.7f, 1f, 0.25f), 1f);
			c.DrawArc(Vector2.Zero, 29f, -Mx.Pi / 2f, -Mx.Pi / 2f + Mx.Tau * left, new Rgba(0.7f, 0.85f, 1f, 0.8f), 2f);
		}
	}
}
