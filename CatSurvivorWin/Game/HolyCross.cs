using System.Numerics;

namespace CatSurvivor;

/// <summary>
/// THÁNH GIÁ: những cây thánh giá bay lơ lửng quanh mèo; cây nào hết hồi chiêu sẽ tự lao vào quái gần nhất (xuyên qua mọi quái trên đường,
/// mỗi quái trúng một lần), rồi bay về vị trí quanh mèo. Lên cấp: thêm thánh giá, lao nhanh/xa hơn, mạnh hơn.
/// Khi tắt tự động ngắm (phím F), thánh giá nhắm quái gần CON TRỎ chuột nhất.
/// </summary>
public class HolyCross : Weapon
{
	private enum Mode { Orbit, Strike, Return }

	private sealed class Cross
	{
		public Mode Mode;
		public Vector2 Pos;
		public Vector2 Heading = V.Right;
		public Enemy Target;
		public float Cd;
		public float Age;
		public readonly HashSet<Enemy> Hit = new();
	}

	private const float HitRadius = 7f;
	private const float ReturnSpeed = 420f;
	private const float OrbitRadius = 30f;
	private const float MaxStrikeTime = 1.2f;

	private readonly List<Cross> _crosses = new();
	private readonly List<Enemy> _near = new();
	private readonly List<Enemy> _hits = new();
	private float _angle;

	public HolyCross(Player p) : base(p) { }

	public override string Name => "Holy Cross";
	public override float Weight => 0.9f;
	public override string NextLevelText => "Thêm thánh giá, lao nhanh và xa hơn, mạnh hơn";

	private int Count => 1 + Level / 2;                          // cấp 1: 1 · cấp 2: 2 · cấp 4: 3 · cấp 6: 4 · cấp 8: 5
	private float Cooldown => MathF.Max(1.5f - 0.1f * Level, 0.5f);
	private float Damage => 9f + 3.5f * Level;
	private float StrikeSpeed => 300f + 12f * Level;
	private float SeekRange => 170f + 6f * Level;

	private Vector2 Slot(int i, int n) =>
		P.Pos + V.FromAngle(_angle + i * Mx.Tau / n) * OrbitRadius + new Vector2(0f, MathF.Sin(P.W.Frame * 0.1f + i * 1.7f) * 2f);

	public override void Update(float dt)
	{
		while (_crosses.Count < Count)
			_crosses.Add(new Cross { Pos = P.Pos, Cd = 0.25f * _crosses.Count });
		_angle += 1.8f * dt;

		int n = _crosses.Count;
		for (int i = 0; i < n; i++)
		{
			var c = _crosses[i];
			Vector2 slot = Slot(i, n);
			switch (c.Mode)
			{
				case Mode.Orbit:
					c.Pos += (slot - c.Pos) * (1f - MathF.Exp(-12f * dt));
					c.Cd -= dt * Rate;
					if (c.Cd <= 0f) TryLaunch(c);
					break;

				case Mode.Strike:
				{
					c.Age += dt;
					if (c.Target == null || c.Target.Dead || c.Age > MaxStrikeTime) { c.Mode = Mode.Return; break; }

					Vector2 to = c.Target.Pos - c.Pos;
					float dist = to.Length();
					if (dist > 0.01f) c.Heading = to / dist;
					c.Pos += c.Heading * StrikeSpeed * dt;

					Em.QueryCircle(c.Pos, HitRadius, _hits);
					foreach (var e in _hits)
					{
						if (!c.Hit.Add(e)) continue;
						HitEnemy(e, Damage * P.DamageMul, c.Heading);
						if (e == c.Target) c.Mode = Mode.Return; // đánh trúng mục tiêu thì quay về (các quái dính trên đường vẫn bị trúng)
					}
					if (dist < 4f) c.Mode = Mode.Return;
					break;
				}

				default: // Return
				{
					Vector2 to = slot - c.Pos;
					float dist = to.Length();
					if (dist < 8f)
					{
						c.Mode = Mode.Orbit;
						c.Cd = Cooldown;
						c.Target = null;
						break;
					}
					c.Heading = to / dist;
					c.Pos += c.Heading * ReturnSpeed * dt;
					break;
				}
			}
		}
	}

	/// <summary>Chọn mục tiêu gần nhất (so với mèo, hoặc so với con trỏ khi tắt tự động ngắm) chưa bị cây khác nhắm tới rồi lao đi.</summary>
	private void TryLaunch(Cross c)
	{
		Vector2 origin = Settings.AutoAim ? P.Pos : P.W.MouseWorld;
		Em.FindNearest(origin, _crosses.Count + 4, SeekRange, _near);
		foreach (var e in _near)
		{
			bool taken = false;
			foreach (var o in _crosses)
				if (o != c && o.Mode == Mode.Strike && o.Target == e) { taken = true; break; }
			if (taken) continue;

			c.Target = e;
			c.Mode = Mode.Strike;
			c.Age = 0f;
			c.Hit.Clear();
			c.Heading = (e.Pos - c.Pos).Normalized();
			SoundService.Play("shot", -14f, 0.12f);
			return;
		}
		c.Cd = 0.15f; // chưa có mục tiêu: thử lại sau một chút
	}

	public override void DrawHitbox(Canvas c)
	{
		foreach (var cr in _crosses) c.DrawArc(cr.Pos - P.Pos, HitRadius, 0f, Mx.Tau, new Rgba(0.3f, 0.9f, 1f, 0.9f), 1f);
	}

	public override void Draw(Canvas c)
	{
		var tex = WeaponArt.Holy;
		foreach (var cr in _crosses)
		{
			Vector2 local = cr.Pos - P.Pos;
			bool flying = cr.Mode != Mode.Orbit;
			c.DrawCircle(local, flying ? 8f : 6f, new Rgba(1f, 0.95f, 0.4f, flying ? 0.32f : 0.16f)); // vầng sáng
			if (tex == null)
			{
				c.FillRect(local.X - 1f, local.Y - 4f, 2f, 8f, new Rgba(1f, 0.9f, 0.3f));
				c.FillRect(local.X - 3f, local.Y - 2f, 6f, 2f, new Rgba(1f, 0.9f, 0.3f));
				continue;
			}
			// Cây thánh giá lao theo kiểu "mũi kiếm": phần thân dài (phía dưới sprite) hướng về phía trước
			float rot = flying ? cr.Heading.Angle() - Mx.Pi / 2f : 0f;
			DrawSprite(c, tex, WeaponArt.CrossSrc, WeaponArt.CrossPivot, local, rot, 2f);
		}
	}
}
