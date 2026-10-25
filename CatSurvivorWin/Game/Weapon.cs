using System.Numerics;

namespace CatSurvivor;

/// <summary>
/// Vũ khí tự động. Chỉ số tính từ Level qua các property, nên nâng cấp = Level++.
/// Player gọi Update/Draw mỗi frame. Toạ độ trong Draw là toạ độ local của Player.
/// </summary>
public abstract class Weapon
{
	public const int MaxLevel = Balance.WeaponMaxLevel;

	public int Level { get; private set; } = 1;
	protected readonly Player P;

	protected Weapon(Player p) { P = p; }

	/// <summary>Đặt cấp ban đầu (dùng cho vũ khí hợp nhất, bắt đầu từ cấp trung bình của hai vũ khí gốc).</summary>
	protected void SetLevel(int level) => Level = Mx.Clamp(level, 1, MaxLevel);

	/// <summary>
	/// Trọng lượng của vũ khí: quyết định lực đẩy quái (Force = Weight x WeaponForceScale) và độ giật lùi của người chơi.
	/// Vũ khí nhẹ (đạn, lưỡi dao, aura) chỉ đẩy nhẹ; vũ khí nặng (vuốt, lốc xoáy, sóng xung kích) văng quái lên không
	/// và giật ngược người chơi.
	/// </summary>
	public virtual float Weight => 1f;

	/// <summary>Lực đẩy mà vũ khí truyền cho quái (px/s), trước khi chia cho Weight của quái.</summary>
	public float Force => Weight * Balance.WeaponForceScale;

	public virtual bool IsFusion => false;

	protected EnemyManager Em => P.W.Enemies;

	/// <summary>Hệ số tốc độ bắn RIÊNG của vũ khí này (gốc 1, tăng bằng thẻ "<vũ khí>: bắn nhanh").</summary>
	public float RateMul = 1f;

	/// <summary>Tốc độ bắn thực = chung x riêng, có trần. Các bộ đếm hồi chiêu/nhịp đánh chạy nhanh lên theo hệ số này (hoạt ảnh xoay không đổi).</summary>
	public float Rate => MathF.Min((UsesGlobalFireRate ? P.FireRate : 1f) * RateMul, Balance.FireRateTotalMax);

	/// <summary>Thẻ "Tốc độ bắn" CHUNG có áp dụng cho vũ khí này không (Holy Shield không tấn công nên không).</summary>
	protected virtual bool UsesGlobalFireRate => true;

	/// <summary>Tên và mô tả thẻ nâng tốc độ RIÊNG của vũ khí ("<vũ khí>: <tên> +25%").</summary>
	public virtual string RateCardName => "bắn nhanh";
	public virtual string RateCardDesc => "Tốc độ đánh riêng của vũ khí này";

	/// <summary>
	/// Gây sát thương + đẩy quái theo hướng `dir` (vector đơn vị, từ người chơi tới quái).
	///  - Quái nhận lực Force / (Weight quái)  -> quái nặng đứng vững hơn (xem EnemyManager.Damage).
	///  - Vũ khí nặng (Weight >= RecoilMinWeight) còn giật ngược người chơi như phản lực:
	///      recoil = Force x RecoilScale x [wQuái / (wQuái + wMèo)] / wMèo
	/// </summary>
	protected void HitEnemy(Enemy e, float damage, Vector2 dir)
	{
		if (e.Dead) return;
		if (Weight >= Balance.RecoilMinWeight)
		{
			float recoil = Force * Balance.WeaponRecoilScale * (e.Weight / (e.Weight + P.Weight)) / P.Weight;
			P.Recoil(-dir * recoil);
		}
		Em.Damage(e, damage, dir * Force);
	}

	/// <summary>Hướng ngắm hiện tại (xem Player.AimAngle): tự động ngắm vào quái gần nhất, hoặc theo chuột khi tắt tự động ngắm.</summary>
	protected float AimAngle() => P.AimAngle;

	/// <summary>Hướng con trỏ chuột (không bị ảnh hưởng bởi tự động ngắm): dành cho vũ khí vốn ngắm bằng chuột như Claw và Wave Slash.</summary>
	protected float MouseAimAngle() => P.MouseAngle;

	/// <summary>
	/// Vẽ một sprite xoay quanh pivot tại vị trí localPos (toạ độ cục bộ của mèo), góc rot (radian), tỉ lệ scale.
	/// Dùng trong Draw: xong tự trả hệ toạ độ về lại cục bộ của mèo để các lệnh vẽ sau không bị lệch.
	/// </summary>
	protected void DrawSprite(Canvas c, Bitmap tex, Rect2 src, Vector2 pivot, Vector2 localPos, float rot, float scale, Rgba? tint = null, bool flipY = false)
	{
		c.SetTransform(P.Pos + localPos, rot, new Vector2(scale, flipY ? -scale : scale));
		c.DrawTexture(tex, new Rect2(-pivot.X, -pivot.Y, src.Size.X, src.Size.Y), src, tint);
		c.SetTransform(P.Pos, 0f, Vector2.One);
	}

	protected static Rect2 FrameRect(int frame) => new(frame * WeaponArt.Frame, 0f, WeaponArt.Frame, WeaponArt.Frame);

	public abstract string Name { get; }
	public abstract string NextLevelText { get; }
	public abstract void Update(float dt);
	public virtual void Draw(Canvas c) { }

	/// <summary>Player.TakeDamage hỏi vũ khí có đỡ đòn vào mèo không (Holy Shield). true = đòn bị chặn hoàn toàn.</summary>
	public virtual bool AbsorbHit(Vector2 fromPos) => false;

	/// <summary>Vẽ hitbox (vùng gây sát thương) của vũ khí khi bật Hiện hitbox trong Cài đặt. Toạ độ cục bộ của mèo.</summary>
	public virtual void DrawHitbox(Canvas c) { }

	public void LevelUp() { if (Level < MaxLevel) Level++; }
}

/// <summary>
/// CUNG: bắn mũi tên vào quái gần nhất (class vẫn tên MagicBolt để các công thức hợp nhất không đổi).
/// Lên cấp: thêm mũi tên, bắn nhanh hơn, xuyên qua quái. Hình: cung (bow.png) lơ lửng cạnh mèo hướng về mục tiêu và kéo dây trước mỗi phát bắn.
/// </summary>
public class MagicBolt : Weapon
{
	private readonly List<Enemy> _targets = new();
	private readonly List<Enemy> _aimTargets = new();
	private float _cd;
	private float _aim;
	private bool _hasTarget;

	public MagicBolt(Player p) : base(p) { }

	public override string Name => "Bow";
	public override float Weight => 1.1f;
	public override string NextLevelText => "Bắn nhanh hơn, mạnh hơn, thêm đạn / xuyên quái";

	private float Cooldown => MathF.Max(1.1f * MathF.Pow(0.9f, Level - 1), Balance.WeaponMinCooldown);
	private int Count => 1 + (Level - 1) / 2;
	private float Damage => 8f + 3f * Level;
	private int Pierce => (Level - 1) / 3;

	public override void Update(float dt)
	{
		_cd -= dt * Rate;

		// Hướng cung: về phía quái gần nhất (không có thì theo hướng mèo đang nhìn)
		Em.FindNearest(P.Pos, 1, 320f, _aimTargets);
		_hasTarget = _aimTargets.Count > 0;
		// Tự động ngắm: cung chĩa vào quái gần nhất (không có quái thì theo hướng mèo nhìn); tắt: cung luôn chĩa theo con trỏ chuột
		float want = !Settings.AutoAim ? AimAngle() : _hasTarget ? (_aimTargets[0].Pos - P.Pos).Angle() : P.Facing.Angle();
		_aim = _aim + Mx.Wrap(want - _aim, -Mx.Pi, Mx.Pi) * (1f - MathF.Exp(-18f * dt)); // xoay mượt

		if (_cd > 0f) return;

		Vector2 pos = P.Pos;
		Em.FindNearest(pos, Count, 320f, _targets);
		if (_targets.Count == 0) return; // chờ có quái trong tầm thì bắn ngay (cả hai chế độ ngắm)

		_cd = Cooldown;
		for (int i = 0; i < Count; i++)
		{
			SoundService.Play("shot", -16f, 0.15f);
			Vector2 dir;
			if (Settings.AutoAim)
			{
				// Tự động ngắm: mỗi mũi tên nhắm một quái gần nhất
				Enemy t = _targets[i % _targets.Count];
				dir = (t.Pos - pos).Normalized();
				if (i >= _targets.Count) // thiếu mục tiêu -> bắn lệch nhẹ quanh mục tiêu gần nhất
				{
					float spread = 0.25f * (i - _targets.Count + 1) * (i % 2 == 0 ? 1f : -1f);
					dir = dir.Rotated(spread);
				}
			}
			else
			{
				// Ngắm bằng chuột: các mũi tên toả nhẹ quanh hướng con trỏ
				dir = V.FromAngle(AimAngle() + (i - (Count - 1) / 2f) * 0.16f);
			}
			P.W.Projectiles.Spawn(pos + dir * 14f, dir * 280f, Damage * P.DamageMul, Pierce, 4f, 1.6f, Force, null, WeaponArt.Arrow());
		}
	}

	public override void Draw(Canvas c)
	{
		var tex = WeaponArt.Bow;
		if (tex == null) return; // chưa có asset: không vẽ gì thêm (đạn vẫn bay)

		// Kéo dây trong lúc chờ phát bắn kế tiếp: khung 0 = thả, 1..3 = kéo dần, kèm mũi tên đã lắp
		float drawTime = MathF.Min(0.30f, Cooldown * 0.5f);
		int frame = 0;
		if (_hasTarget && _cd < drawTime)
			frame = 1 + Math.Min(2, (int)((1f - Mx.Clamp(_cd / drawTime, 0f, 1f)) * 3f));

		Vector2 local = V.FromAngle(_aim) * 15f;
		c.SetTransform(P.Pos + local, _aim, Vector2.One);
		if (frame > 0)
		{
			var a = WeaponArt.ArrowSrc;
			float tail = WeaponArt.BowStringX[frame] - WeaponArt.BowPivot.X;
			c.DrawTexture(tex, new Rect2(tail, -WeaponArt.ArrowPivot.Y, a.Size.X, a.Size.Y), a);
		}
		var crop = WeaponArt.BowCrop;
		c.DrawTexture(tex,
			new Rect2(crop.Position.X - WeaponArt.BowPivot.X, crop.Position.Y - WeaponArt.BowPivot.Y, crop.Size.X, crop.Size.Y),
			new Rect2(frame * WeaponArt.Frame + crop.Position.X, crop.Position.Y, crop.Size.X, crop.Size.Y));
		c.SetTransform(P.Pos, 0f, Vector2.One);
	}
}

/// <summary>DAO: những con dao xoay quanh người, gây sát thương theo nhịp (class vẫn tên OrbitBlades để các công thức hợp nhất không đổi).</summary>
public class OrbitBlades : Weapon
{
	private const float Tick = 0.35f;
	private const float BladeRadius = 7f;

	private readonly List<Enemy> _hits = new();
	private float _angle;
	private float _tick;

	public OrbitBlades(Player p) : base(p) { }

	public override string Name => "Knife";
	public override float Weight => 0.8f;
	public override string NextLevelText => "Thêm lưỡi dao, xoay nhanh hơn, mạnh hơn";

	private int Count => 2 + (Level - 1) / 2;
	private float OrbitRadius => 46f + 2f * Level;
	private float AngularSpeed => 2.6f + 0.15f * Level;
	private float Damage => 6f + 2f * Level;

	private Vector2 BladeOffset(int i) => V.FromAngle(_angle + i * Mx.Tau / Count) * OrbitRadius;

	public override void Update(float dt)
	{
		_angle += AngularSpeed * dt;
		_tick -= dt * Rate;
		if (_tick > 0f) return;
		_tick = Tick;

		Vector2 center = P.Pos;
		for (int i = 0; i < Count; i++)
		{
			Vector2 bp = center + BladeOffset(i);
			Em.QueryCircle(bp, BladeRadius, _hits);
			foreach (var e in _hits)
				HitEnemy(e, Damage * P.DamageMul, (e.Pos - center).Normalized());
		}
	}

	public override void DrawHitbox(Canvas c)
	{
		for (int i = 0; i < Count; i++) c.DrawArc(BladeOffset(i), BladeRadius, 0f, Mx.Tau, new Rgba(0.3f, 0.9f, 1f, 0.9f), 1f);
	}

	public override void Draw(Canvas c)
	{
		var tex = WeaponArt.Knife;
		for (int i = 0; i < Count; i++)
		{
			Vector2 o = BladeOffset(i);
			if (tex == null)
			{
				c.DrawCircle(o, BladeRadius, new Rgba(0.8f, 0.9f, 1f));
				c.DrawCircle(o, BladeRadius - 3f, new Rgba(0.4f, 0.6f, 1f));
				continue;
			}
			// Mũi dao hướng theo chiều quay (tiếp tuyến): sprite có mũi hướng lên (-y) nên xoay thêm 90°
			float theta = _angle + i * Mx.Tau / Count;
			DrawSprite(c, tex, FrameRect(0), WeaponArt.KnifePivot, o, theta + Mx.Pi, 1.1f);
		}
	}
}

/// <summary>
/// Vuốt mèo: QUẬT MẠNH TỪ TRÊN XUỐNG một vùng hình quạt về HƯỚNG CHUỘT. Claw KHÔNG có tự động ngắm - luôn ngắm bằng con trỏ.
/// Chỉ quật khi có quái trong tầm. Lên cấp: tầm xa, góc rộng, mạnh hơn;
/// Lv4+ vung thêm nhát phía sau, Lv7+ vung 4 hướng (chữ thập).
/// </summary>
public class ClawSlash : Weapon
{
	// Một nhát quật: găng NHẤC CAO lên và ngửa ra sau (WindUp) -> QUẬT MẠNH TỪ TRÊN XUỐNG chỗ quái (tới ImpactAt, tăng tốc dần)
	// -> giáng xuống: tính sát thương, rung màn hình, để lại 3 vệt cào + sóng bụi (phần còn lại).
	private const float SlashLife = 0.30f;
	private const float WindUp = 0.36f;
	private const float ImpactAt = 0.64f;
	private const float RaiseHeight = 54f;     // độ cao găng nhấc lên trước khi quật (px)

	private struct Slash { public float Angle; public float Age; public bool Struck; }

	private readonly List<Enemy> _hits = new();
	private readonly List<Slash> _slashes = new();
	private float _cd;

	public ClawSlash(Player p) : base(p) { }

	public override string Name => "Claw";
	public override float Weight => 2.2f + 0.1f * Level; // NẶNG: văng quái nhẹ lên không + giật lùi người chơi
	public override string NextLevelText => "Quật xa hơn, rộng hơn, mạnh hơn (Lv4: thêm nhát sau lưng, Lv7: 4 hướng)";

	private float Cooldown => MathF.Max(0.9f * MathF.Pow(0.92f, Level - 1), Balance.WeaponMinCooldown);
	private float Range => 56f + 4f * Level;
	private float HalfAngle => Mx.DegToRad(32f + 5f * Level);
	private float Damage => 14f + 4f * Level;
	private int Directions => Level >= 7 ? 4 : Level >= 4 ? 2 : 1;

	public override void Update(float dt)
	{
		for (int i = _slashes.Count - 1; i >= 0; i--)
		{
			var s = _slashes[i];
			s.Age += dt;
			if (!s.Struck && s.Age >= SlashLife * ImpactAt)
			{
				// Găng giáng xuống: lúc này mới gây sát thương (khớp với hình ảnh quật xuống)
				s.Struck = true;
				Strike(P.Pos, s.Angle);
				P.W.AddShake(2.8f);
				SoundService.Play("slam", -9f, 0.1f);
			}
			if (s.Age >= SlashLife) _slashes.RemoveAt(i);
			else _slashes[i] = s;
		}

		_cd -= dt * Rate;
		if (_cd > 0f) return;

		Vector2 pos = P.Pos;
		Em.QueryCircle(pos, Range, _hits);
		if (_hits.Count == 0) return; // không có quái trong tầm thì chưa quật

		_cd = Cooldown;
		float aim = MouseAimAngle(); // Claw KHÔNG có tự động ngắm: luôn theo con trỏ chuột
		for (int d = 0; d < Directions; d++)
		{
			// 1 hướng: [0] · 2 hướng: [0, 180°] · 4 hướng: [0, 90°, 180°, 270°]
			float angle = aim + d * Mx.Tau / Directions;
			_slashes.Add(new Slash { Angle = angle, Age = 0f });
		}
	}

	/// <summary>Gây sát thương mọi quái trong hình quạt (tâm = người chơi, hướng = angle).</summary>
	private void Strike(Vector2 pos, float angle)
	{
		Em.QueryCircle(pos, Range, _hits);
		foreach (var e in _hits)
		{
			Vector2 v = e.Pos - pos;
			float dist = v.Length();
			// Quái sát người luôn bị trúng; còn lại kiểm tra lệch góc <= nửa góc quạt
			bool inArc = dist < e.Radius + 14f
				|| MathF.Abs(Mx.Wrap(v.Angle() - angle, -Mx.Pi, Mx.Pi)) <= HalfAngle;
			if (inArc)
				HitEnemy(e, Damage * P.DamageMul, v / MathF.Max(dist, 0.01f));
		}
	}

	private static float EaseOut(float k) => 1f - (1f - k) * (1f - k);

	/// <summary>Tư thế găng ở thời điểm u (0..1) của nhát quật: vị trí (toạ độ cục bộ của mèo), góc xoay, tỉ lệ, độ nhấc (0..1).</summary>
	private (Vector2 pos, float rot, float scale, float cock) Pose(Slash s, float u)
	{
		float side = MathF.Cos(s.Angle) < 0f ? -1f : 1f;
		float cock;                                              // 1 = nhấc cao + ngửa hết cỡ, 0 = đã giáng xuống
		if (u < WindUp) cock = EaseOut(u / WindUp);               // nhấc lên
		else if (u < ImpactAt) { float k = (u - WindUp) / (ImpactAt - WindUp); cock = 1f - k * k * k; } // quật xuống: tăng tốc rất nhanh
		else cock = 0f;

		Vector2 dir = V.FromAngle(s.Angle);
		Vector2 target = dir * Range * 0.7f;
		Vector2 pos = target - dir * (8f * cock) + new Vector2(0f, -RaiseHeight * cock);
		float rot = s.Angle - side * 1.15f * cock;                // ngửa mũi vuốt lên trên rồi bổ xuống
		float scale = 2.2f + 0.9f * cock;                         // ở trên cao thì to hơn (gần camera)
		return (pos, rot, scale, cock);
	}

	public override void Draw(Canvas c)
	{
		var tex = WeaponArt.Claw;
		float aim = MouseAimAngle();
		if (tex == null)
		{
			// Chưa có asset: mũi tên nhỏ chỉ hướng chuột
			Vector2 tip = V.FromAngle(aim) * 26f;
			c.DrawLine(V.FromAngle(aim) * 18f, tip, new Rgba(1f, 1f, 1f, 0.5f), 2f);
			c.DrawCircle(tip, 2.5f, new Rgba(1f, 0.85f, 0.3f, 0.8f));
		}
		else if (_slashes.Count == 0)
		{
			// Chờ ra đòn: găng vuốt lơ lửng cạnh mèo, mũi chỉ về hướng chuột (nhún nhẹ); lật dọc khi quay trái để găng luôn đứng thẳng
			float bob = MathF.Sin(P.W.Frame * 0.12f) * 1.5f;
			DrawSprite(c, tex, FrameRect(0), WeaponArt.ClawPivot, V.FromAngle(aim) * (20f + bob), aim, 1.4f, null, flipY: MathF.Cos(aim) < 0f);
		}

		foreach (var s in _slashes)
		{
			float u = s.Age / SlashLife;
			Vector2 dir = V.FromAngle(s.Angle);
			Vector2 target = dir * Range * 0.7f;

			// Vùng sẽ bị quật: viền quạt mờ báo trước, đậm lên lúc giáng xuống
			float zone = u < ImpactAt ? 0.18f + 0.25f * u : MathF.Max(0f, 1f - (u - ImpactAt) / (1f - ImpactAt));
			c.DrawArc(Vector2.Zero, Range, s.Angle - HalfAngle, s.Angle + HalfAngle, new Rgba(1f, 1f, 1f, 0.55f * zone), 2f);
			if (u >= ImpactAt)
			{
				const int steps = 14;
				var fan = new Vector2[steps + 2];
				fan[0] = Vector2.Zero;
				for (int i = 0; i <= steps; i++)
					fan[i + 1] = V.FromAngle(s.Angle - HalfAngle + 2f * HalfAngle * i / steps) * Range;
				c.DrawPolygon(fan, new Rgba(1f, 1f, 1f, 0.16f * zone));
			}

			if (tex != null)
			{
				bool flip = MathF.Cos(s.Angle) < 0f;
				// Vệt mờ theo sau khi đang quật xuống
				if (u > WindUp && u < ImpactAt + 0.06f)
					for (int g = 3; g >= 1; g--)
					{
						float uu = MathF.Max(WindUp * 0.5f, u - g * 0.035f);
						var gp = Pose(s, uu);
						DrawSprite(c, tex, FrameRect(0), WeaponArt.ClawPivot, gp.pos, gp.rot, gp.scale, new Rgba(1f, 1f, 1f, 0.30f / g), flip);
					}
				var pose = Pose(s, u);
				float alpha = u < ImpactAt ? 1f : 1f - 0.85f * (u - ImpactAt) / (1f - ImpactAt);
				DrawSprite(c, tex, FrameRect(0), WeaponArt.ClawPivot, pose.pos, pose.rot, pose.scale, new Rgba(1f, 1f, 1f, alpha), flip);
			}

			// Giáng xuống: 3 vệt cào chéo từ trên xuống + sóng bụi toả ra
			if (u >= ImpactAt)
			{
				float k = (u - ImpactAt) / (1f - ImpactAt);
				for (int j = -1; j <= 1; j++)
				{
					Vector2 b = target + new Vector2(j * 7f, 0f);
					c.DrawLine(b + new Vector2(-4f, -22f), b + new Vector2(4f, 22f), new Rgba(1f, 1f, 1f, 0.95f * (1f - k)), 3.2f - 2f * k);
				}
				c.DrawArc(target, 6f + 34f * k, 0f, Mx.Tau, new Rgba(1f, 0.95f, 0.8f, 0.7f * (1f - k)), 2f);
			}
		}
	}

	public override void DrawHitbox(Canvas c)
	{
		float aim = MouseAimAngle();
		for (int d = 0; d < Directions; d++)
		{
			float a = aim + d * Mx.Tau / Directions;
			var col = new Rgba(0.3f, 0.9f, 1f, 0.9f);
			c.DrawArc(Vector2.Zero, Range, a - HalfAngle, a + HalfAngle, col, 1f);
			c.DrawLine(Vector2.Zero, V.FromAngle(a - HalfAngle) * Range, col, 1f);
			c.DrawLine(Vector2.Zero, V.FromAngle(a + HalfAngle) * Range, col, 1f);
		}
	}
}

/// <summary>
/// Khiên thánh: KHÔNG gây sát thương. Mỗi lớp khiên đỡ trọn MỘT đòn đánh vào mèo (mất máu và bị đẩy lùi đều bị chặn),
/// vỡ rồi tự hồi lại sau một lúc. Cấp cao thì nhiều lớp hơn và hồi nhanh hơn.
/// </summary>
public class HolyShield : Weapon
{
	private const float PopTime = 0.35f;
	private const float ShoveRadius = 56f;
	private const float ShoveForce = 240f;   // lực hất quái xung quanh khi khiên vỡ (chia cho Weight quái)

	private readonly List<Enemy> _hits = new();
	private int _charges;
	private float _recharge;                 // thời gian đã hồi cho lớp khiên kế tiếp
	private float _pop;                      // hiệu ứng vỡ (đếm lùi)

	public HolyShield(Player p) : base(p) { _charges = MaxCharges; }

	public override string Name => "Holy Shield";
	public override float Weight => 0.4f;
	public override string NextLevelText => "Hồi khiên nhanh hơn (Lv3: 2 lớp khiên, Lv6: 3 lớp)";

	// Khiên không tấn công: thẻ tốc độ bắn chung không áp dụng, thẻ riêng của nó là "hồi khiên nhanh"
	protected override bool UsesGlobalFireRate => false;
	public override string RateCardName => "hồi khiên nhanh";
	public override string RateCardDesc => "Khiên hồi lại nhanh hơn";

	public int MaxCharges => 1 + Level / 3;
	public int Charges => _charges;
	private float RechargeTime => MathF.Max(12f - 1f * Level, 4f);
	private float Radius => 24f;

	public override void Update(float dt)
	{
		if (_pop > 0f) _pop -= dt;
		_charges = Math.Min(_charges, MaxCharges);
		if (_charges >= MaxCharges) { _recharge = 0f; return; }

		_recharge += dt * Rate;
		if (_recharge >= RechargeTime) { _charges++; _recharge = 0f; }
	}

	/// <summary>Player.TakeDamage hỏi từng vũ khí: còn lớp khiên thì đỡ đòn này (true) và vỡ một lớp, hất nhẹ quái đứng sát.</summary>
	public override bool AbsorbHit(Vector2 fromPos)
	{
		if (_charges <= 0) return false;
		_charges--;
		_pop = PopTime;

		Vector2 center = P.Pos;
		Em.QueryCircle(center, ShoveRadius, _hits);
		foreach (var e in _hits)
		{
			if (e.Dead) continue;
			Vector2 away = e.Pos - center;
			away = away.LengthSquared() > 0.01f ? away.Normalized() : V.FromAngle(Rng.Randf() * Mx.Tau);
			e.PendingKnock += away * ShoveForce / e.Weight;
		}
		return true;
	}

	public override void DrawHitbox(Canvas c) => c.DrawArc(Vector2.Zero, Radius + 4f * MathF.Max(_charges - 1, 0), 0f, Mx.Tau, new Rgba(0.3f, 0.9f, 1f, 0.9f), 1f);

	public override void Draw(Canvas c)
	{
		var tex = WeaponArt.Holy;
		float pulse = 1f + 0.03f * MathF.Sin(P.W.Frame * 0.08f);

		// Mỗi lớp khiên là một bong bóng, lớp sau to hơn lớp trước một chút
		for (int i = 0; i < _charges; i++)
		{
			float r = (Radius + 5f * i) * pulse;
			if (tex != null)
				DrawSprite(c, tex, WeaponArt.ShieldSrc, WeaponArt.ShieldPivot, Vector2.Zero, 0f, r * 2f / WeaponArt.ShieldSrc.Size.X);
			else
			{
				c.DrawCircle(Vector2.Zero, r, new Rgba(1f, 0.9f, 0.3f, 0.10f));
				c.DrawArc(Vector2.Zero, r, 0f, Mx.Tau, new Rgba(1f, 0.9f, 0.3f, 0.5f), 1.5f);
			}
		}

		// Hết khiên: vòng mờ chạy dần thể hiện tiến độ hồi lớp tiếp theo
		if (_charges < MaxCharges)
		{
			float k = Mx.Clamp(_recharge / RechargeTime, 0f, 1f);
			c.DrawArc(Vector2.Zero, Radius + 2f, -Mx.Pi / 2f, -Mx.Pi / 2f + Mx.Tau * k, new Rgba(1f, 0.9f, 0.4f, _charges == 0 ? 0.55f : 0.3f), 1f);
		}

		// Vừa đỡ đòn: vòng sáng toả ra rồi mờ đi
		if (_pop > 0f)
		{
			float k = 1f - _pop / PopTime;
			c.DrawArc(Vector2.Zero, Radius + 4f + 22f * k, 0f, Mx.Tau, new Rgba(1f, 0.95f, 0.6f, 0.85f * (1f - k)), 2f);
		}
	}
}
