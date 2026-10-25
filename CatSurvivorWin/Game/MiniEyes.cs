using System.Numerics;

namespace CatSurvivor;

/// <summary>Trạng thái riêng của một mắt con (gắn vào Enemy.Mini).</summary>
public class MiniEyeData
{
	public int Index;                 // 0..3: vị trí trong hình chữ thập khi 4 con (của CÙNG một mắt chính) hợp thể
	public Enemy Owner;               // mắt chính mà con này phục vụ (khiên và chữ thập của mắt chính đó tính theo 4 con của nó)
	public float ShootCd = 2f;        // đếm ngược tới lần bắn chưởng kế tiếp
	public float Windup = -1f;        // >= 0: đang gồng bắn (đứng yên, báo hiệu), giá trị = thời gian đã gồng
	public Vector2 Aim;               // hướng đã khoá khi gồng
	public float DashLeft;            // > 0: đang lách sang bên khi bị dồn vào góc
	public Vector2 DashDir;
	public float Strafe = 1f;         // chiều lượn khi chạy trốn (+1 / -1)
	public float StrafeTimer;
	public float FlashLeft;           // > 0: vừa bắn - chớp sáng ở nòng (con mắt)
	public float Heading = Mx.Pi;     // hướng con mắt đang nhìn (radian, 0 = phải): thân xoay theo, xoay mượt về hướng mới
}

/// <summary>"Chưởng" của mắt con: viên plasma bay thẳng (một đoạn dài, hitbox hình đoạn thẳng).</summary>
public sealed class EyeBolt
{
	public Vector2 Pos, Dir;          // Pos = ĐẦU viên chưởng
	public float Life;
	public float Age;                 // thời gian bay: viên chưởng "dài ra" dần từ con mắt (xem Length)
	public bool Dead;

	/// <summary>Chiều dài hiện tại: mọc ra từ nòng trong ~0,12s rồi giữ nguyên.</summary>
	public float Length => MiniEyes.BoltLength * Mx.Clamp(Age / 0.12f, 0.15f, 1f);
}

/// <summary>
/// GIAI ĐOẠN 2 của boss (mắt chính còn <= 50% máu): 4 MẮT CON xuất hiện hỗ trợ.
///  - Mắt chính BẤT KHẢ XÂM PHẠM cho tới khi diệt hết 4 mắt con; diệt xong thì mắt chính choáng lâu hơn (thời gian phản công).
///  - Mắt con luôn CHẠY TRỐN mèo (lượn dọc tường, bị dồn góc thì lách sang bên), chỉ dừng lại gồng một chút để bắn CHƯỞNG (viên plasma) theo hướng mèo sắp tới.
///  - Khi đủ 4 con, mắt chính ra hiệu cho cả 4 hợp thể: xếp thành hình chữ thập quanh mắt chính rồi phóng 4 chùm plasma XOAY VÒNG (chưởng chữ thập).
///    Diệt một con giữa chừng là chữ thập tan.
/// Mọi tốc độ / hồi chiêu / độ chính xác phụ thuộc độ khó (xem Difficulty).
/// </summary>
public static class MiniEyes
{
	public const int Count = 4;
	// (giai đoạn giờ chuyển khi cạn THANH MÁU của giai đoạn, không còn theo % máu)

	// ---- mắt con
	public const float Hp = 260f;                      // x độ khó (xem EnemyTypes.CreateMiniEye); trước đây 110 - trâu hơn ở cả 4 chế độ
	public const float Radius = 14f;
	private const float FleeRange = 300f;              // mèo lại gần hơn mức này thì bỏ chạy
	private const float ComfortRange = 230f;           // xa hơn mức này thì tiến lại gần để còn bắn
	private const float WindupTime = 0.6f;
	private const float DashTime = 0.35f;

	/// <summary>Tốc độ chạy trốn (px/s): Dễ 125 · Trung bình 145 · Khó 170 · Siêu khó 195 (mèo chạy 220 - đuổi được, nhất là khi dash / vũ khí tầm xa).</summary>
	public static float FleeSpeed => 125f + 23f * (int)Difficulty.Level;

	/// <summary>Thời gian hồi giữa hai lần bắn chưởng.</summary>
	public static float ShootCooldown => Mx.Clamp(2.8f - 0.3f * (int)Difficulty.Level, 1.6f, 3f) * (0.8f + Rng.Randf() * 0.4f);

	// ---- chưởng (viên plasma)
	public const float BoltSpeed = 300f;
	public const float BoltLength = 64f;
	public const float BoltThickness = 11f;
	private const float BoltLife = 3f;
	private static float BoltDamage => Balance.BossRingDamage * 0.8f * Difficulty.EnemyDamageMul;

	// ---- chưởng chữ thập xoay vòng
	public const float CrossRadius = 62f;              // khoảng cách mắt con tới mắt chính khi hợp thể
	public const float BeamLength = 300f;
	public const float BeamWidth = 14f;
	public const float CrossWindup = 1.4f;             // xếp hình + báo hiệu
	public const float CrossDuration = 5f;             // thời gian quét
	public static float CrossSpin => 0.8f + 0.12f * (int)Difficulty.Level;   // rad/giây
	public static float CrossCooldown => Mx.Clamp(9f - 1.2f * (int)Difficulty.Level, 5f, 9f);
	private static float BeamDamage => Balance.BossRingDamage * 0.9f * Difficulty.EnemyDamageMul;

	// ================================================================ nòng (con mắt) và hiệu ứng bắn
	// Sprite mắt con (small eye ball) quay mặt sang TRÁI, con mắt cách tâm thân ~11,5px; game lật ngang theo hướng mèo. Chưởng phải đi ra từ ĐÚNG con mắt đó.
	private const float EyeOffsetX = 11.5f;

	/// <summary>Độ lơ lửng nhấp nhô (px): hình vẽ nhấc lên bấy nhiêu so với vị trí thật; nòng đi theo.</summary>
	public static float Bob(Enemy e) => 2.5f * MathF.Sin(e.AnimTime * 5f + (e.Mini?.Index ?? 0));

	/// <summary>Vị trí con mắt (nòng bắn): trước thân EyeOffsetX px theo hướng đang nhìn (thân xoay theo Heading).</summary>
	public static Vector2 Muzzle(Enemy e) => e.Pos + new Vector2(0f, -Bob(e)) + V.FromAngle(e.Mini?.Heading ?? Mx.Pi) * EyeOffsetX;

	private const float TurnRate = 14f;   // rad/giây: tốc độ xoay thân

	private static float TurnAngle(float from, float to, float maxStep)
	{
		float diff = Mx.Wrap(to - from, -Mx.Pi, Mx.Pi);
		return from + Mx.Clamp(diff, -maxStep, maxStep);
	}

	// ================================================================ AI mắt con
	public static void Step(Enemy e, float dt, Player player, EnemyManager em, BossArena arena, Enemy boss)
	{
		var m = e.Mini;
		var bd = boss.Boss;
		e.AnimTime += dt;
		m.FlashLeft = MathF.Max(m.FlashLeft - dt, 0f);
		Vector2 pp = player.Pos;
		if (MathF.Abs(pp.X - e.Pos.X) > 3f) e.FaceX = pp.X - e.Pos.X;   // luôn quay mặt về phía mèo
		// Thân xoay để con mắt nhìn về hướng đang quan tâm: hướng chữ thập (hợp thể) · hướng đã khoá (gồng bắn) · mèo
		float want = bd.CrossState > 0 ? bd.CrossAngle + m.Index * Mx.Pi / 2f
			: m.Windup >= 0f ? MathF.Atan2(m.Aim.Y, m.Aim.X)
			: MathF.Atan2(pp.Y - e.Pos.Y, pp.X - e.Pos.X);
		m.Heading = TurnAngle(m.Heading, want, TurnRate * dt);
		Vector2 muzzle = Muzzle(e);
		Vector2 toPlayer = pp - e.Pos;
		float dist = toPlayer.Length();
		Vector2 dirToPlayer = (pp - muzzle).LengthSquared() > 1f ? (pp - muzzle).Normalized() : V.Left;

		// ---- Hợp thể chữ thập: bay tới vị trí của mình quanh mắt chính (xoay cùng chùm tia)
		if (bd.CrossState > 0)
		{
			m.Windup = -1f;
			m.ShootCd = MathF.Max(m.ShootCd, 1.2f);
			Vector2 target = CrossSlot(boss.Pos, bd.CrossAngle, m.Index);
			Vector2 to = target - e.Pos;
			float d = to.Length();
			float step = (bd.CrossState == 1 ? 420f : 600f) * dt;
			e.Pos = d <= step ? target : e.Pos + to / d * step;
			return;
		}

		// ---- Gồng bắn chưởng: đứng yên, nhìn mèo, rồi bắn
		if (m.Windup >= 0f)
		{
			m.Windup += dt;
			if (m.Windup < WindupTime * 0.5f)
				m.Aim = Prediction.Dir(muzzle, player, BoltSpeed, dirToPlayer, WindupTime - m.Windup + dist / BoltSpeed * 0.5f, arena); // nửa đầu còn chỉnh hướng
			if (m.Windup >= WindupTime)
			{
				em.Bolts.Add(new EyeBolt { Pos = muzzle + m.Aim * 6f, Dir = m.Aim, Life = BoltLife });
				m.FlashLeft = 0.14f;
				SoundService.Play("shot", -6f, 0.12f);
				m.Windup = -1f;
				m.ShootCd = ShootCooldown;
			}
			return;
		}

		// ---- Chạy trốn mèo
		m.ShootCd -= dt;
		m.StrafeTimer -= dt;
		if (m.StrafeTimer <= 0f) { m.StrafeTimer = 1.2f + Rng.Randf() * 1.5f; m.Strafe = Rng.Randf() < 0.5f ? -1f : 1f; }

		Rect2 r = arena.Active ? arena.Rect : new Rect2(e.Pos - new Vector2(500f), new Vector2(1000f));
		float margin = 70f;
		Vector2 wall = Vector2.Zero;                                  // đẩy ra khỏi tường
		if (e.Pos.X - r.Position.X < margin) wall.X += 1f - (e.Pos.X - r.Position.X) / margin;
		if (r.End.X - e.Pos.X < margin) wall.X -= 1f - (r.End.X - e.Pos.X) / margin;
		if (e.Pos.Y - r.Position.Y < margin) wall.Y += 1f - (e.Pos.Y - r.Position.Y) / margin;
		if (r.End.Y - e.Pos.Y < margin) wall.Y -= 1f - (r.End.Y - e.Pos.Y) / margin;
		bool cornered = wall.LengthSquared() > 0.5f && dist < 140f;

		Vector2 move;
		float speed = FleeSpeed;
		if (m.DashLeft > 0f)
		{
			m.DashLeft -= dt;
			move = m.DashDir;
			speed *= 2.3f;
		}
		else if (cornered)
		{
			// Bị dồn góc: lách vuông góc với hướng mèo, về phía còn nhiều chỗ hơn
			Vector2 perp = new(-dirToPlayer.Y, dirToPlayer.X);
			Vector2 centre = r.Center - e.Pos;
			m.DashDir = (Vector2.Dot(perp, centre) >= 0f ? perp : -perp).Normalized();
			m.DashLeft = DashTime;
			move = m.DashDir;
			speed *= 2.3f;
		}
		else if (dist < FleeRange)
		{
			Vector2 away = -dirToPlayer;
			Vector2 tangent = new Vector2(-away.Y, away.X) * m.Strafe;
			move = (away + tangent * 0.45f + wall * 1.4f).Normalized();
		}
		else if (dist > ComfortRange + 160f)
			move = (dirToPlayer + wall).Normalized() * 0.6f;       // ở quá xa thì lững thững tiến lại để còn tầm bắn
		else
			move = new Vector2(-dirToPlayer.Y, dirToPlayer.X) * m.Strafe * 0.35f + wall;   // vừa tầm: lượn quanh

		e.Pos += move * speed * dt;

		// Hết hồi chiêu và đủ gần thì gồng bắn
		if (m.ShootCd <= 0f && dist < 460f && dist > 60f)
		{
			m.Windup = 0f;
			m.Aim = dirToPlayer;
		}
	}

	/// <summary>Vị trí mắt con thứ index trong hình chữ thập (tâm = mắt chính).</summary>
	public static Vector2 CrossSlot(Vector2 centre, float angle, int index) => centre + V.FromAngle(angle + index * Mx.Pi / 2f) * CrossRadius;

	// ================================================================ chưởng (viên plasma)
	public static void UpdateBolts(List<EyeBolt> bolts, float dt, Player player, BossArena arena)
	{
		for (int i = bolts.Count - 1; i >= 0; i--)
		{
			var b = bolts[i];
			b.Pos += b.Dir * BoltSpeed * dt;
			b.Life -= dt;
			b.Age += dt;

			// Trúng mèo? (đoạn thẳng dài BoltLength, đầu bolt ở b.Pos). Dash xuyên qua thì chưởng bay tiếp.
			if (!b.Dead && !player.Dead && !player.IsDashing)
			{
				float d = DistToSegment(player.Pos, b.Pos - b.Dir * b.Length, b.Pos);
				if (d <= player.Radius + BoltThickness * 0.5f)
				{
					player.TakeDamage(BoltDamage, b.Pos - b.Dir * b.Length, 220f);
					b.Dead = true;
				}
			}

			var gr = arena.Rect.Grow(80f);
			bool outside = arena.Active && (b.Pos.X < gr.Position.X || b.Pos.Y < gr.Position.Y || b.Pos.X > gr.End.X || b.Pos.Y > gr.End.Y);
			if (b.Dead || b.Life <= 0f || outside) bolts.RemoveAt(i);
		}
	}

	// ================================================================ chùm tia chữ thập
	/// <summary>Chùm tia thứ index: điểm đầu (ở mắt con) và điểm cuối. Ở giai đoạn gồng chỉ là đường báo hiệu.</summary>
	public static (Vector2 a, Vector2 b) Beam(Enemy boss, int index)
	{
		var bd = boss.Boss;
		Vector2 dir = V.FromAngle(bd.CrossAngle + index * Mx.Pi / 2f);
		Vector2 a = boss.Pos + dir * CrossRadius + dir * EyeOffsetX;   // chùm tia mọc ra từ con mắt của mắt con (đang xoay nhìn ra ngoài)
		return (a, a + dir * BeamLength);
	}

	/// <summary>Quét chữ thập: xoay và gây sát thương cho mèo chạm chùm tia (dash xuyên qua được).</summary>
	public static void StepCross(Enemy boss, float dt, Player player)
	{
		var bd = boss.Boss;
		if (bd.CrossState == 1)
		{
			bd.CrossTimer += dt;
			if (bd.CrossTimer >= CrossWindup) { bd.CrossState = 2; bd.CrossTimer = 0f; SoundService.Play("boss", -4f, 0.3f); }
			return;
		}
		bd.CrossTimer += dt;
		bd.CrossAngle += CrossSpin * dt;
		for (int i = 0; i < Count; i++)
		{
			var (a, b) = Beam(boss, i);
			if (DistToSegment(player.Pos, a, b) <= player.Radius + BeamWidth * 0.5f)
				player.TakeDamage(BeamDamage, a, 260f);
		}
	}

	public static float DistToSegment(Vector2 p, Vector2 a, Vector2 b)
	{
		Vector2 ab = b - a;
		float len2 = ab.LengthSquared();
		float t = len2 > 0.0001f ? Mx.Clamp(Vector2.Dot(p - a, ab) / len2, 0f, 1f) : 0f;
		return (p - (a + ab * t)).Length();
	}
}