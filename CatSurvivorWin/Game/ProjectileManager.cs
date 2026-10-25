using System.Numerics;

namespace CatSurvivor;

/// <summary>
/// Quản lý toàn bộ đạn trong một đối tượng duy nhất (không tạo đối tượng riêng cho từng viên).
/// Va chạm với quái qua spatial hash của EnemyManager.
/// </summary>
public sealed class ProjectileManager
{
	private readonly World _w;

	private class Projectile
	{
		public Vector2 Pos, Vel;
		public float Damage, Radius, Life, Knock;
		public Rgba Color = new(1f, 0.95f, 0.5f);
		public int Pierce;                          // số quái có thể xuyên thêm sau quái đầu tiên
		public WeaponArt.ProjectileSprite Sprite;   // != null: vẽ bằng sprite (vd. mũi tên) thay cho hình tròn
		public readonly HashSet<Enemy> Hit = new(); // mỗi viên chỉ đánh mỗi quái 1 lần
	}

	private readonly List<Projectile> _list = new();
	private readonly List<Enemy> _tmp = new();

	public ProjectileManager(World w) { _w = w; }

	/// <summary>knock: lực đẩy quái (px/s, thường truyền Weapon.Force); color: màu đạn (mặc định vàng).</summary>
	public void Spawn(Vector2 pos, Vector2 vel, float damage, int pierce, float radius, float life, float knock = 120f, Rgba? color = null, WeaponArt.ProjectileSprite sprite = null)
	{
		_list.Add(new Projectile { Pos = pos, Vel = vel, Damage = damage, Pierce = pierce, Radius = radius, Life = life, Knock = knock, Color = color ?? new Rgba(1f, 0.95f, 0.5f), Sprite = sprite });
	}

	public void Update(float dt)
	{
		for (int i = _list.Count - 1; i >= 0; i--)
		{
			var p = _list[i];
			p.Pos += p.Vel * dt;
			p.Life -= dt;

			bool remove = p.Life <= 0f;
			if (!remove)
			{
				_w.Enemies.QueryCircle(p.Pos, p.Radius, _tmp);
				foreach (var e in _tmp)
				{
					if (!p.Hit.Add(e)) continue;
					_w.Enemies.Damage(e, p.Damage, p.Vel.Normalized() * p.Knock);
					if (p.Pierce-- <= 0) { remove = true; break; }
				}
			}

			if (remove)
			{
				_list[i] = _list[^1];
				_list.RemoveAt(_list.Count - 1);
			}
		}
	}

	/// <summary>Hitbox đạn (vòng vàng).</summary>
	public void DrawHitboxes(Canvas c)
	{
		foreach (var p in _list) c.DrawArc(p.Pos, p.Radius, 0f, Mx.Tau, new Rgba(1f, 0.9f, 0.2f, 0.95f), 1f);
	}

	public void Draw(Canvas c)
	{
		foreach (var p in _list)
		{
			if (p.Sprite != null)
			{
				var s = p.Sprite;
				c.SetTransform(p.Pos, p.Vel.Angle(), new Vector2(s.Scale, s.Scale));
				c.DrawTexture(s.Tex, new Rect2(-s.Pivot.X, -s.Pivot.Y, s.Src.Size.X, s.Src.Size.Y), s.Src);
				c.ResetTransform();
				continue;
			}
			c.DrawLine(p.Pos, p.Pos - p.Vel.Normalized() * (4f + p.Radius * 2f), new Rgba(p.Color, 0.6f), MathF.Max(2f, p.Radius * 0.8f));
			c.DrawCircle(p.Pos, p.Radius, p.Color);
		}
	}
}
