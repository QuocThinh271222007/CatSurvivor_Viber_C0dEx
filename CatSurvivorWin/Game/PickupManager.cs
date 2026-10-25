using System.Numerics;

namespace CatSurvivor;

/// <summary>
/// Quản lý XP gem và bình máu. Vào bán kính hút của người chơi thì bay về phía người chơi và tăng tốc dần.
/// Ngoài XP rơi từ quái còn có XP TỰ SINH trên map (ambient) - có giới hạn: tối đa số viên cùng lúc và tối đa số viên mỗi phút.
/// Sprite XP xuất từ Aseprite (experiences.aseprite): gem xanh lá (nhỏ), gem đỏ (vừa), viên cầu vàng (lớn).
/// </summary>
public sealed class PickupManager
{
	private readonly World _w;

	private const int MaxPickups = 800;

	// --- Sprite (Assets/items/experiences.png, ảnh 64x64 chứa 3 biểu tượng) ---
	private const string SpritePath = "items/experiences.png";
	private static readonly Rect2 GreenGem = new(3, 0, 7, 7);    // XP nhỏ   (< 1.5)
	private static readonly Rect2 RedGem = new(19, 2, 6, 6);     // XP vừa   (1.5 - 2.99)
	private static readonly Rect2 GoldOrb = new(34, 3, 8, 8);    // XP lớn   (>= 3)

	// --- XP tự sinh trên map ---
	private const float AmbientInterval = 0.5f;                  // thử sinh mỗi chừng này giây (trước đây 1.0, ban đầu 2.5)
	private const float AmbientMinDist = 140f;                   // sinh quanh người chơi trong vòng [Min, Max]
	private const float AmbientMaxDist = 420f;
	private const float AmbientDespawnDist = 850f;               // viên nào bị bỏ lại quá xa thì biến mất

	private class Pickup
	{
		public Vector2 Pos;
		public float Value;
		public bool Heal;
		public bool Ambient;
		public bool Attracted;
		public float Speed;
		public float Phase;       // pha nhấp nhô
	}

	private readonly List<Pickup> _list = new();
	private readonly Bitmap _sprite;
	private float _ambientTimer = 1f;
	private int _ambientMinute = -1;
	private int _ambientSpawnedThisMinute;

	/// <summary>Số XP tự sinh đang nằm trên map / đã sinh trong phút hiện tại.</summary>
	public int AmbientAlive { get; private set; }
	public int AmbientSpawnedThisMinute => _ambientSpawnedThisMinute;

	public PickupManager(World w)
	{
		_w = w;
		_sprite = AssetStore.Image(SpritePath);
	}

	public void DropGem(Vector2 pos, float xp)
	{
		if (_w.State.Level >= GameState.MaxLevel) return;                          // đã khoá ở cấp tối đa: không còn XP
		if (_list.Count >= MaxPickups) { _w.State.AddXp(xp); return; }             // quá nhiều -> nhận XP luôn
		_list.Add(new Pickup { Pos = pos, Value = xp, Phase = Rng.Randf() * Mx.Tau });
	}

	public void DropHeal(Vector2 pos)
	{
		_list.Add(new Pickup { Pos = pos, Value = 20f, Heal = true, Phase = Rng.Randf() * Mx.Tau });
	}

	/// <summary>
	/// XP tự sinh: thỉnh thoảng một viên xuất hiện quanh người chơi. Có hai giới hạn (xem Balance.Ambient*):
	/// tối đa AmbientMaxAlive viên nằm trên map cùng lúc và tối đa AmbientPerMinute viên mỗi phút -> nguồn XP này không vô tận.
	/// </summary>
	private void AmbientTick(float dt, Player player)
	{
		if (_w.State.BossActive || _w.State.Level >= GameState.MaxLevel) return; // hết XP tự sinh khi đã max cấp / đang đánh boss
		int minute = (int)(_w.State.Time / 60f);
		if (minute != _ambientMinute) { _ambientMinute = minute; _ambientSpawnedThisMinute = 0; }

		_ambientTimer -= dt;
		if (_ambientTimer > 0f) return;
		_ambientTimer = AmbientInterval;

		if (AmbientAlive >= Balance.AmbientMaxAlive || _ambientSpawnedThisMinute >= Balance.AmbientPerMinute) return;

		Vector2 pos = player.Pos + V.FromAngle(Rng.Randf() * Mx.Tau) * (AmbientMinDist + Rng.Randf() * (AmbientMaxDist - AmbientMinDist));
		float roll = Rng.Randf();
		float value = roll < 0.50f ? 1f : roll < 0.80f ? 2f : 4f; // xanh lá / đỏ / vàng (vàng 20%)
		_list.Add(new Pickup { Pos = pos, Value = value, Ambient = true, Phase = Rng.Randf() * Mx.Tau });
		_ambientSpawnedThisMinute++;
	}

	public void Update(float dt)
	{
		var player = _w.Player;
		if (player.Dead) return;

		Vector2 pp = player.Pos;
		AmbientTick(dt, player);

		int ambient = 0;
		for (int i = _list.Count - 1; i >= 0; i--)
		{
			var pk = _list[i];
			Vector2 d = pp - pk.Pos;
			float len = d.Length();

			if (pk.Ambient && len > AmbientDespawnDist) // bị bỏ lại quá xa
			{
				_list[i] = _list[^1];
				_list.RemoveAt(_list.Count - 1);
				continue;
			}

			if (!pk.Attracted && len < player.MagnetRadius) pk.Attracted = true;
			if (pk.Attracted)
			{
				pk.Speed += 900f * dt;
				if (len > 0.01f) pk.Pos += d / len * MathF.Min(pk.Speed * dt, len);
			}

			if (len < player.Radius + 4f)
			{
				if (pk.Heal) player.Heal(pk.Value);
				else { _w.State.AddXp(pk.Value); SoundService.Play("pickup", -14f, 0.04f); }

				_list[i] = _list[^1];
				_list.RemoveAt(_list.Count - 1);
				continue;
			}
			if (pk.Ambient) ambient++;
		}
		AmbientAlive = ambient;
	}

	public void Draw(Canvas c)
	{
		double now = Environment.TickCount64 / 1000.0;
		foreach (var pk in _list)
		{
			float bob = MathF.Sin((float)now * 3f + pk.Phase) * 1.5f; // nhấp nhô nhẹ
			Vector2 cpos = pk.Pos + new Vector2(0f, bob);

			if (pk.Heal)
			{
				// Hộp cứu thương nhỏ: nền trắng, chữ thập đỏ (không dùng sprite XP nào)
				c.FillRect(cpos.X - 4f, cpos.Y - 4f, 8f, 8f, Rgba.White);
				c.FillRect(cpos.X - 1f, cpos.Y - 3f, 2f, 6f, new Rgba(0.9f, 0.15f, 0.15f));
				c.FillRect(cpos.X - 3f, cpos.Y - 1f, 6f, 2f, new Rgba(0.9f, 0.15f, 0.15f));
				continue;
			}

			Rect2 src = pk.Value < 1.5f ? GreenGem : pk.Value < 3f ? RedGem : GoldOrb;
			c.DrawTexture(_sprite, new Rect2(cpos - src.Size / 2f, src.Size), src);
		}
	}
}
