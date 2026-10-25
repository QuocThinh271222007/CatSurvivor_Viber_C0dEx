using System.Numerics;

namespace CatSurvivor;

/// <summary>
/// Hợp nhất vũ khí: khi HAI vũ khí gốc cùng đạt cấp Balance.FusionMinLevel, menu nâng cấp sẽ có thêm lựa chọn ★ HỢP NHẤT.
/// Chọn nó thì cả hai vũ khí gốc bị thay bằng MỘT vũ khí hợp nhất (cấp bắt đầu = trung bình hai cấp, làm tròn lên),
/// vẫn nâng cấp tiếp được tới cấp tối đa. Mỗi vũ khí hợp nhất chỉ có thể sở hữu một cái.
///
///   Magic Bolt   + Orbit Blades -> Star Ring   : quả cầu xoay quanh người, mỗi quả tự bắn đạn vào quái gần nhất
///   Magic Bolt   + Claw Slash   -> Wave Slash  : kiếm khí xuyên thấu bay về hướng chuột, đẩy mạnh
///   Magic Bolt   + Holy Aura    -> Star Nova   : aura, định kỳ nổ ra vòng đạn toả đều mọi hướng
///   Orbit Blades + Claw Slash   -> Whirlwind   : các cánh quạt khổng lồ xoay tròn, quét văng quái, giật lùi người chơi
///   Orbit Blades + Holy Aura    -> Holy Blades : lưỡi kiếm xoay trên rìa aura, hút máu theo từng nhát
///   Claw Slash   + Holy Aura    -> Shockwave   : sóng xung kích lan ra theo vòng tròn, hất văng quái, giật lùi người chơi
/// </summary>
public static class Fusions
{
	public record Recipe(Type A, Type B, string Name, string Desc, Func<Player, int, Weapon> Make);

	public static readonly Recipe[] All =
	{
		new(typeof(MagicBolt), typeof(OrbitBlades), "Star Ring", "quả cầu xoay quanh người, tự bắn đạn vào quái gần nhất", (p, l) => new StarRing(p, l)),
		new(typeof(MagicBolt), typeof(ClawSlash), "Wave Slash", "kiếm khí xuyên thấu bay theo hướng chuột, đẩy rất mạnh", (p, l) => new WaveSlash(p, l)),
		new(typeof(MagicBolt), typeof(HolyShield), "Star Nova", "aura + định kỳ nổ vòng đạn toả mọi hướng", (p, l) => new StarNova(p, l)),
		new(typeof(OrbitBlades), typeof(ClawSlash), "Whirlwind", "cánh quạt khổng lồ xoay tròn, văng quái, giật lùi bạn", (p, l) => new Whirlwind(p, l)),
		new(typeof(OrbitBlades), typeof(HolyShield), "Holy Blades", "lưỡi kiếm xoay trên rìa aura, hút máu mỗi nhát", (p, l) => new HolyBlades(p, l)),
		new(typeof(ClawSlash), typeof(HolyShield), "Shockwave", "sóng xung kích lan tròn, hất văng quái, giật lùi bạn", (p, l) => new Shockwave(p, l)),
	};

	public static Weapon Find(Player p, Type t) => p.Weapons.Find(w => w.GetType() == t);

	/// <summary>Các công thức đang đủ điều kiện: có cả hai vũ khí gốc, cả hai đủ cấp, và chưa có vũ khí hợp nhất đó.</summary>
	public static List<Recipe> Available(Player p)
	{
		var list = new List<Recipe>();
		foreach (var r in All)
		{
			var a = Find(p, r.A);
			var b = Find(p, r.B);
			if (a == null || b == null) continue;
			if (a.Level < Balance.FusionMinLevel || b.Level < Balance.FusionMinLevel) continue;
			if (p.Weapons.Exists(w => w.Name == r.Name)) continue;
			list.Add(r);
		}
		return list;
	}

	/// <summary>Thay hai vũ khí gốc bằng vũ khí hợp nhất.</summary>
	public static void Apply(Player p, Recipe r)
	{
		var a = Find(p, r.A);
		var b = Find(p, r.B);
		if (a == null || b == null) return;
		int level = (int)Math.Ceiling((a.Level + b.Level) / 2f);
		p.Weapons.Remove(a);
		p.Weapons.Remove(b);
		var fused = r.Make(p, level);
		fused.RateMul = MathF.Max(a.RateMul, b.RateMul); // giữ lại bonus tốc độ bắn riêng của hai vũ khí gốc
		p.AddWeapon(fused);
	}
}

// ===================================================================================================================
/// <summary>Star Ring (Bolt + Orbit): quả cầu xoay quanh người, vừa gây sát thương khi chạm vừa tự bắn đạn.</summary>
public class StarRing : Weapon
{
	private const float OrbRadius = 8f;
	private const float Tick = 0.35f;

	private readonly List<Enemy> _hits = new();
	private readonly List<Enemy> _targets = new();
	private float _angle, _tick, _fire;

	public StarRing(Player p, int level) : base(p) { SetLevel(level); }

	public override string Name => "Star Ring";
	public override string NextLevelText => "Thêm quả cầu, bắn nhanh hơn, mạnh hơn";
	public override bool IsFusion => true;
	public override float Weight => 1.0f;

	private int Count => 3 + Level / 2;
	private float OrbitRadius => 54f + 2f * Level;
	private float AngularSpeed => 2.2f + 0.12f * Level;
	private float ContactDamage => 8f + 2.5f * Level;
	private float BoltDamage => 10f + 3.5f * Level;
	private float FireCooldown => MathF.Max(1.4f - 0.1f * Level, Balance.WeaponMinCooldown + 0.2f);

	private Vector2 Orb(int i) => V.FromAngle(_angle + i * Mx.Tau / Count) * OrbitRadius;

	public override void Update(float dt)
	{
		_angle += AngularSpeed * dt;
		Vector2 center = P.Pos;

		_tick -= dt * Rate;
		if (_tick <= 0f)
		{
			_tick = Tick;
			for (int i = 0; i < Count; i++)
			{
				Em.QueryCircle(center + Orb(i), OrbRadius, _hits);
				foreach (var e in _hits)
					HitEnemy(e, ContactDamage * P.DamageMul, (e.Pos - center).Normalized());
			}
		}

		_fire -= dt * Rate;
		if (_fire <= 0f)
		{
			_fire = FireCooldown;
			for (int i = 0; i < Count; i++)
			{
				Vector2 pos = center + Orb(i);
				Em.FindNearest(pos, 1, 260f, _targets);
				if (_targets.Count == 0) continue;
				Vector2 dir = (_targets[0].Pos - pos).Normalized();
				P.W.Projectiles.Spawn(pos, dir * 300f, BoltDamage * P.DamageMul, 1, 4f, 1.4f, Force, new Rgba(0.7f, 0.9f, 1f));
			}
		}
	}

	public override void DrawHitbox(Canvas c)
	{
		for (int i = 0; i < Count; i++) c.DrawArc(Orb(i), OrbRadius, 0f, Mx.Tau, new Rgba(0.3f, 0.9f, 1f, 0.9f), 1f);
	}

	public override void Draw(Canvas c)
	{
		for (int i = 0; i < Count; i++)
		{
			Vector2 o = Orb(i);
			c.DrawCircle(o, OrbRadius + 3f, new Rgba(0.6f, 0.85f, 1f, 0.25f));
			c.DrawCircle(o, OrbRadius, new Rgba(0.75f, 0.92f, 1f));
			c.DrawCircle(o, OrbRadius - 3f, Rgba.White);
		}
	}
}

// ===================================================================================================================
/// <summary>Wave Slash (Bolt + Claw): kiếm khí to, xuyên thấu mọi quái, bay về hướng chuột và đẩy rất mạnh.</summary>
public class WaveSlash : Weapon
{
	private readonly List<Enemy> _targets = new();
	private float _cd;

	public WaveSlash(Player p, int level) : base(p) { SetLevel(level); }

	public override string Name => "Wave Slash";
	public override string NextLevelText => "Bắn nhanh hơn, mạnh hơn (Lv4: 3 luồng, Lv7: 5 luồng)";
	public override bool IsFusion => true;
	public override float Weight => 2.0f + 0.08f * Level;

	private float Cooldown => MathF.Max(0.85f * MathF.Pow(0.92f, Level - 1), Balance.WeaponMinCooldown + 0.05f);
	private float Damage => 20f + 6f * Level;
	private int Waves => Level >= 7 ? 5 : Level >= 4 ? 3 : 1;

	public override void Update(float dt)
	{
		_cd -= dt * Rate;
		if (_cd > 0f) return;

		Vector2 pos = P.Pos;
		Em.FindNearest(pos, 1, 300f, _targets);
		if (_targets.Count == 0) return; // chỉ bắn khi có quái trong tầm

		_cd = Cooldown;
		float aim = MouseAimAngle();
		for (int i = 0; i < Waves; i++)
		{
			Vector2 dir = V.FromAngle(aim + (i - (Waves - 1) / 2f) * 0.3f);
			P.W.Projectiles.Spawn(pos + dir * 14f, dir * 340f, Damage * P.DamageMul, 99, 11f, 0.75f, Force, new Rgba(0.5f, 1f, 1f));
		}
	}

	public override void Draw(Canvas c)
	{
		float aim = MouseAimAngle();
		Vector2 tip = V.FromAngle(aim) * 26f;
		c.DrawLine(V.FromAngle(aim) * 18f, tip, new Rgba(0.7f, 1f, 1f, 0.6f), 2f);
		c.DrawCircle(tip, 2.5f, new Rgba(0.5f, 1f, 1f, 0.9f));
	}
}

// ===================================================================================================================
/// <summary>Star Nova (Bolt + Aura): aura quanh người, định kỳ nổ một vòng đạn toả đều mọi hướng.</summary>
public class StarNova : Weapon
{
	private const float Tick = 0.5f;

	private readonly List<Enemy> _hits = new();
	private float _tick;
	private float _nova = 1.2f;

	public StarNova(Player p, int level) : base(p) { SetLevel(level); }

	public override string Name => "Star Nova";
	public override string NextLevelText => "Aura rộng hơn, nhiều đạn hơn, nổ nhanh hơn";
	public override bool IsFusion => true;
	public override float Weight => 0.6f;

	private float AuraRadius => 48f + 4f * Level;
	private float AuraDamage => 5f + 1.8f * Level;
	private float NovaCooldown => MathF.Max(2.6f - 0.15f * Level, 1.2f);
	private int NovaBolts => 8 + Level;
	private float NovaDamage => 12f + 4f * Level;

	public override void Update(float dt)
	{
		Vector2 center = P.Pos;

		_tick -= dt * Rate;
		if (_tick <= 0f)
		{
			_tick = Tick;
			Em.QueryCircle(center, AuraRadius, _hits);
			foreach (var e in _hits)
				HitEnemy(e, AuraDamage * P.DamageMul, (e.Pos - center).Normalized());
		}

		_nova -= dt * Rate;
		if (_nova <= 0f)
		{
			_nova = NovaCooldown;
			float a0 = Rng.Randf() * Mx.Tau;
			for (int i = 0; i < NovaBolts; i++)
			{
				Vector2 dir = V.FromAngle(a0 + i * Mx.Tau / NovaBolts);
				P.W.Projectiles.Spawn(center + dir * 10f, dir * 250f, NovaDamage * P.DamageMul, 1, 4f, 1.2f, Force * 1.5f, new Rgba(1f, 0.9f, 0.4f));
			}
		}
	}

	public override void DrawHitbox(Canvas c) => c.DrawArc(Vector2.Zero, AuraRadius, 0f, Mx.Tau, new Rgba(0.3f, 0.9f, 1f, 0.9f), 1f);

	public override void Draw(Canvas c)
	{
		c.DrawCircle(Vector2.Zero, AuraRadius, new Rgba(1f, 0.9f, 0.4f, 0.10f));
		c.DrawArc(Vector2.Zero, AuraRadius, 0f, Mx.Tau, new Rgba(1f, 0.9f, 0.4f, 0.5f), 1.5f);
		// Vòng tích năng lượng lớn dần tới lúc nổ
		float k = Mx.Clamp(1f - _nova / NovaCooldown, 0f, 1f);
		c.DrawArc(Vector2.Zero, AuraRadius * k, 0f, Mx.Tau, new Rgba(1f, 1f, 0.7f, 0.6f * k), 2f);
	}
}

// ===================================================================================================================
/// <summary>Whirlwind (Orbit + Claw): các cánh quạt khổng lồ xoay quanh người, quét văng quái và giật lùi người chơi.</summary>
public class Whirlwind : Weapon
{
	private const float Tick = 0.15f;
	private const float HalfWidth = 0.45f;

	private readonly List<Enemy> _hits = new();
	private float _angle, _tick;

	public Whirlwind(Player p, int level) : base(p) { SetLevel(level); }

	public override string Name => "Whirlwind";
	public override string NextLevelText => "Thêm cánh, tầm xa hơn, xoay nhanh hơn, mạnh hơn";
	public override bool IsFusion => true;
	public override float Weight => 3.0f;   // rất nặng: văng quái + giật lùi người chơi

	private int Arms => 2 + Level / 3;
	private float Reach => 66f + 4f * Level;
	private float AngularSpeed => 3.6f + 0.15f * Level;
	private float Damage => 14f + 4.5f * Level;

	public override void Update(float dt)
	{
		_angle += AngularSpeed * dt;
		_tick -= dt * Rate;
		if (_tick > 0f) return;
		_tick = Tick;

		Vector2 center = P.Pos;
		Em.QueryCircle(center, Reach, _hits);
		foreach (var e in _hits)
		{
			Vector2 v = e.Pos - center;
			float dist = v.Length();
			float ang = v.Angle();
			bool swept = dist < e.Radius + 12f;
			for (int a = 0; a < Arms && !swept; a++)
				swept = MathF.Abs(Mx.Wrap(ang - (_angle + a * Mx.Tau / Arms), -Mx.Pi, Mx.Pi)) <= HalfWidth;
			if (swept)
				HitEnemy(e, Damage * P.DamageMul, v / MathF.Max(dist, 0.01f));
		}
	}

	public override void DrawHitbox(Canvas c)
	{
		c.DrawArc(Vector2.Zero, 12f, 0f, Mx.Tau, new Rgba(0.3f, 0.9f, 1f, 0.9f), 1f);
		for (int a = 0; a < Arms; a++)
		{
			float ang = _angle + a * Mx.Tau / Arms;
			c.DrawArc(Vector2.Zero, Reach, ang - HalfWidth, ang + HalfWidth, new Rgba(0.3f, 0.9f, 1f, 0.9f), 1f);
			c.DrawLine(Vector2.Zero, V.FromAngle(ang - HalfWidth) * Reach, new Rgba(0.3f, 0.9f, 1f, 0.9f), 1f);
			c.DrawLine(Vector2.Zero, V.FromAngle(ang + HalfWidth) * Reach, new Rgba(0.3f, 0.9f, 1f, 0.9f), 1f);
		}
	}

	public override void Draw(Canvas c)
	{
		for (int a = 0; a < Arms; a++)
		{
			float ang = _angle + a * Mx.Tau / Arms;
			const int steps = 10;
			var fan = new Vector2[steps + 2];
			fan[0] = Vector2.Zero;
			for (int i = 0; i <= steps; i++)
				fan[i + 1] = V.FromAngle(ang - HalfWidth + 2f * HalfWidth * i / steps) * Reach;
			c.DrawPolygon(fan, new Rgba(0.85f, 0.95f, 1f, 0.20f));
			c.DrawArc(Vector2.Zero, Reach, ang - HalfWidth, ang + HalfWidth, new Rgba(1f, 1f, 1f, 0.85f), 3f);
			c.DrawLine(Vector2.Zero, V.FromAngle(ang) * Reach, new Rgba(1f, 1f, 1f, 0.6f), 2f);
		}
	}
}

// ===================================================================================================================
/// <summary>Holy Blades (Orbit + Aura): lưỡi kiếm xoay trên rìa aura; mỗi nhát chém hút một ít máu (có giới hạn mỗi giây).</summary>
public class HolyBlades : Weapon
{
	private const float BladeRadius = 9f;
	private const float BladeTick = 0.25f;
	private const float AuraTick = 0.5f;
	private const float HealPerHit = 0.4f;

	private readonly List<Enemy> _hits = new();
	private float _angle, _tickBlade, _tickAura, _healBudget;

	public HolyBlades(Player p, int level) : base(p) { SetLevel(level); }

	public override string Name => "Holy Blades";
	public override string NextLevelText => "Aura rộng hơn, thêm lưỡi kiếm, hút máu nhiều hơn";
	public override bool IsFusion => true;
	public override float Weight => 1.0f;

	private float AuraRadius => 56f + 4f * Level;
	private int Blades => 3 + Level / 2;
	private float BladeDamage => 10f + 3f * Level;
	private float AuraDamage => 4f + 1.5f * Level;
	private float AngularSpeed => 2f + 0.1f * Level;
	private float HealPerSecond => 0.5f + 0.25f * Level;
	private float OrbitRadius => AuraRadius * 0.9f;

	private Vector2 BladeOffset(int i) => V.FromAngle(_angle + i * Mx.Tau / Blades) * OrbitRadius;

	public override void Update(float dt)
	{
		_angle += AngularSpeed * dt;
		_healBudget = MathF.Min(_healBudget + HealPerSecond * dt, HealPerSecond); // tối đa HealPerSecond HP tích lại
		Vector2 center = P.Pos;

		_tickBlade -= dt * Rate;
		if (_tickBlade <= 0f)
		{
			_tickBlade = BladeTick;
			for (int i = 0; i < Blades; i++)
			{
				Em.QueryCircle(center + BladeOffset(i), BladeRadius, _hits);
				foreach (var e in _hits)
				{
					HitEnemy(e, BladeDamage * P.DamageMul, (e.Pos - center).Normalized());
					if (_healBudget >= HealPerHit)
					{
						P.Heal(HealPerHit);
						_healBudget -= HealPerHit;
					}
				}
			}
		}

		_tickAura -= dt * Rate;
		if (_tickAura <= 0f)
		{
			_tickAura = AuraTick;
			Em.QueryCircle(center, AuraRadius, _hits);
			foreach (var e in _hits)
				HitEnemy(e, AuraDamage * P.DamageMul, (e.Pos - center).Normalized());
		}
	}

	public override void DrawHitbox(Canvas c)
	{
		c.DrawArc(Vector2.Zero, AuraRadius, 0f, Mx.Tau, new Rgba(0.3f, 0.9f, 1f, 0.9f), 1f);
		for (int i = 0; i < Blades; i++) c.DrawArc(BladeOffset(i), BladeRadius, 0f, Mx.Tau, new Rgba(0.3f, 0.9f, 1f, 0.9f), 1f);
	}

	public override void Draw(Canvas c)
	{
		c.DrawCircle(Vector2.Zero, AuraRadius, new Rgba(1f, 0.95f, 0.5f, 0.08f));
		c.DrawArc(Vector2.Zero, AuraRadius, 0f, Mx.Tau, new Rgba(1f, 0.95f, 0.5f, 0.45f), 1.5f);
		var tex = WeaponArt.Knife;
		for (int i = 0; i < Blades; i++)
		{
			Vector2 o = BladeOffset(i);
			c.DrawCircle(o, BladeRadius + 3f, new Rgba(1f, 0.95f, 0.6f, 0.25f));
			if (tex == null)
			{
				c.DrawCircle(o, BladeRadius, new Rgba(1f, 0.97f, 0.8f));
				c.DrawCircle(o, BladeRadius - 4f, new Rgba(1f, 0.8f, 0.3f));
				continue;
			}
			// dao vàng xoay quanh aura, mũi theo chiều quay
			float theta = _angle + i * Mx.Tau / Blades;
			DrawSprite(c, tex, FrameRect(0), WeaponArt.KnifePivot, o, theta + Mx.Pi, 1.3f, new Rgba(1f, 0.92f, 0.55f));
		}
	}
}

// ===================================================================================================================
/// <summary>Shockwave (Claw + Aura): sóng xung kích lan ra thành vòng tròn, mỗi quái bị trúng một lần và bị hất văng.</summary>
public class Shockwave : Weapon
{
	private const float Speed = 280f;
	private const float Width = 12f;

	private class Ring
	{
		public Vector2 Center;
		public float R = 8f;
		public readonly HashSet<Enemy> Hit = new();
	}

	private readonly List<Ring> _rings = new();
	private readonly List<Enemy> _hits = new();
	private float _cd;

	public Shockwave(Player p, int level) : base(p) { SetLevel(level); }

	public override string Name => "Shockwave";
	public override string NextLevelText => "Sóng lan xa hơn, mạnh hơn, phát nhanh hơn";
	public override bool IsFusion => true;
	public override float Weight => 2.6f + 0.1f * Level; // nặng: hất văng quái + giật lùi người chơi

	private float Cooldown => MathF.Max(1.8f - 0.1f * Level, 0.8f);
	private float MaxRadius => 100f + 8f * Level;
	private float Damage => 18f + 5f * Level;

	public override void Update(float dt)
	{
		for (int i = _rings.Count - 1; i >= 0; i--)
		{
			var ring = _rings[i];
			ring.R += Speed * dt;
			if (ring.R > MaxRadius) { _rings.RemoveAt(i); continue; }

			Em.QueryCircle(ring.Center, ring.R + Width, _hits);
			foreach (var e in _hits)
			{
				Vector2 v = e.Pos - ring.Center;
				float dist = v.Length();
				if (dist < ring.R - Width - e.Radius) continue; // chưa tới / đã nằm trong vòng (nhưng chưa bị quét qua)
				if (!ring.Hit.Add(e)) continue;
				HitEnemy(e, Damage * P.DamageMul, v / MathF.Max(dist, 0.01f));
			}
		}

		_cd -= dt * Rate;
		if (_cd > 0f) return;
		Em.QueryCircle(P.Pos, MaxRadius * 0.8f, _hits);
		if (_hits.Count == 0) return; // chỉ phát sóng khi có quái trong tầm
		_cd = Cooldown;
		_rings.Add(new Ring { Center = P.Pos });
	}

	public override void DrawHitbox(Canvas c)
	{
		foreach (var ring in _rings)
		{
			Vector2 local = ring.Center - P.Pos;
			c.DrawArc(local, MathF.Max(ring.R - Width, 1f), 0f, Mx.Tau, new Rgba(0.3f, 0.9f, 1f, 0.9f), 1f);
			c.DrawArc(local, ring.R + Width, 0f, Mx.Tau, new Rgba(0.3f, 0.9f, 1f, 0.9f), 1f);
		}
	}

	public override void Draw(Canvas c)
	{
		foreach (var ring in _rings)
		{
			float alpha = 1f - ring.R / MaxRadius;
			Vector2 local = ring.Center - P.Pos;
			c.DrawArc(local, ring.R, 0f, Mx.Tau, new Rgba(1f, 0.9f, 0.5f, 0.85f * alpha), 4f);
			c.DrawArc(local, MathF.Max(ring.R - 5f, 1f), 0f, Mx.Tau, new Rgba(1f, 1f, 1f, 0.5f * alpha), 2f);
		}
	}
}
