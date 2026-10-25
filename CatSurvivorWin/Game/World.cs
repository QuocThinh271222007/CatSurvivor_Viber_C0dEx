using System.Numerics;

namespace CatSurvivor;

/// <summary>
/// Một ván chơi: giữ mọi hệ thống (người chơi, quái, đạn, XP, boss), cập nhật theo thời gian và vẽ ra Canvas.
/// Form chỉ cần gọi Update(dt) và Draw(canvas); World báo lên form qua sự kiện (lên cấp, kết thúc ván).
/// </summary>
public sealed class World
{
	public readonly GameState State = new();
	public readonly GameInput Input = new();

	public readonly Player Player;
	public readonly EnemyManager Enemies;
	public readonly ProjectileManager Projectiles;
	public readonly PickupManager Pickups;
	public readonly BossArena Arena;

	/// <summary>Tâm camera (toạ độ thế giới). Camera đuổi theo mèo có độ trễ; khi đánh boss thì khoá giữa vùng.</summary>
	public Vector2 Cam;

	/// <summary>Tốc độ game (cheat debug): 1 = bình thường. GameForm nhân vào dt.</summary>
	public float TimeScale = 1f;

	/// <summary>Dòng thông báo nổi ở góc dưới (kết quả lệnh cheat), tự tắt sau vài giây.</summary>
	public string Toast { get; private set; }
	private float _toastTime;

	public void ShowToast(string text, float seconds = 3f) { Toast = text; _toastTime = seconds; }

	private float _deathTimer;

	/// <summary>Rung màn hình (px, giảm dần): vũ khí nặng giáng xuống thì rung nhẹ.</summary>
	public float Shake { get; private set; }
	public void AddShake(float px) => Shake = MathF.Max(Shake, px);

	/// <summary>Số khung hình đã chạy (dùng cho hiệu ứng rung).</summary>
	public long Frame { get; private set; }

	/// <summary>Số lần lên cấp chưa được chọn phần thưởng (form hiện menu nâng cấp cho tới khi về 0).</summary>
	public int PendingLevelUps { get; set; }

	/// <summary>Bắn khi ván kết thúc: true = đã hạ boss, false = mèo chết.</summary>
	public event Action<bool> Ended;
	public bool IsEnded { get; private set; }

	// Tile nền (xuất từ Aseprite: enviroment.aseprite). Phải lát liền mạch.
	private const string FloorTexturePath = "environment/grass.png";
	private readonly Bitmap _floor;

	public static Vector2 ViewSize => new(Balance.ViewWidth, Balance.ViewHeight);

	public World()
	{
		_floor = AssetStore.Image(FloorTexturePath);

		// Thứ tự tạo quan trọng: các hệ thống tham chiếu lẫn nhau qua World
		Arena = new BossArena(this);
		Pickups = new PickupManager(this);
		Enemies = new EnemyManager(this);
		Projectiles = new ProjectileManager(this);
		Player = new Player(this);

		Cam = Player.Pos;
		State.LeveledUp += () =>
		{
			PendingLevelUps++;
			SoundService.Play("levelup", -2f, 0.1f);
		};
		// Mèo chết: dừng đồng hồ ngay; màn kết thúc chỉ hiện sau khi mèo tưng lên và rơi khỏi màn hình (xem Update)
		Player.Died += () => State.Running = false;
	}

	/// <summary>Vị trí chuột trong thế giới game (để ngắm vũ khí).</summary>
	public Vector2 MouseWorld => Cam - ViewSize / 2f + Input.MouseView;

	internal void RaiseEnded(bool victory)
	{
		if (IsEnded) return;
		IsEnded = true;
		State.Running = false;
		Ended?.Invoke(victory);
	}

	/// <summary>Cập nhật một bước (dt giây). Form chỉ gọi khi game đang chạy (không tạm dừng, không mở menu).</summary>
	public void Update(float dt)
	{
		Frame++;
		if (Shake > 0f) Shake = MathF.Max(0f, Shake - 14f * dt);
		if (_toastTime > 0f && (_toastTime -= dt) <= 0f) Toast = null;
		State.Update(dt);
		Pickups.Update(dt);
		Enemies.Update(dt);
		Projectiles.Update(dt);
		Player.Update(dt);
		Arena.Update(dt);

		if (Player.Dead)
		{
			// Camera đứng yên để mèo rơi ra khỏi màn hình; xong (hoặc quá 2,6 giây) thì báo kết thúc ván
			_deathTimer += dt;
			bool offScreen = Player.Pos.Y - Cam.Y > ViewSize.Y / 2f + 30f;
			if ((offScreen && _deathTimer > 0.8f) || _deathTimer > 2.6f) RaiseEnded(false);
			return;
		}

		// Camera trễ nhẹ phía sau mèo; đánh boss thì theo mèo trong phạm vi khung
		Vector2 target = Arena.Active ? Arena.CameraCenter(Player.Pos, ViewSize) : Player.Pos;
		Cam += (target - Cam) * (1f - MathF.Exp(-8f * dt));
	}

	/// <summary>Vẽ cả thế giới + HUD. Thứ tự vẽ: nền < XP < quái sau lưng mèo < mèo < quái trước mặt < đạn < vùng boss < hiệu ứng vũ khí < HUD.</summary>
	public void Draw(Canvas c)
	{
		DrawWorld(c);
		DrawOverlay(c);
	}

	/// <summary>Chỉ phần thế giới (nền, quái, mèo, đạn...): vẽ ở độ phân giải gốc 640x360 rồi phóng to.</summary>
	public void DrawWorld(Canvas c)
	{
		Vector2 shake = Shake > 0f ? new Vector2(MathF.Sin(Frame * 12.9898f), MathF.Cos(Frame * 78.233f)) * Shake : Vector2.Zero;
		c.WorldMode(Cam - ViewSize / 2f + shake);
		DrawFloor(c);
		Pickups.Draw(c);
		Enemies.PrepareDraw(Cam - ViewSize / 2f, ViewSize);
		Enemies.DrawBack(c);
		Player.Draw(c);
		Enemies.DrawFront(c);
		Enemies.DrawEyeEffects(c);
		Projectiles.Draw(c);
		Player.DrawWeapons(c);
		if (Settings.ShowHitbox) DrawHitboxes(c);
		c.ResetTransform();
	}

	/// <summary>Hitbox (Cài đặt > Hiện hitbox): mèo xanh lá, quái đỏ, đạn vàng, vùng sát thương vũ khí xanh dương.</summary>
	private void DrawHitboxes(Canvas c)
	{
		c.ResetTransform();
		c.DrawArc(Player.Pos, Player.Radius, 0f, Mx.Tau, new Rgba(0.3f, 1f, 0.4f, 0.95f), 1f);
		Enemies.DrawHitboxes(c);
		Projectiles.DrawHitboxes(c);
		Player.DrawWeaponHitboxes(c);
	}

	/// <summary>HUD + biểu ngữ boss: vẽ lên trên ở độ phân giải cửa sổ để chữ luôn sắc nét.</summary>
	public void DrawOverlay(Canvas c)
	{
		c.ScreenMode();
		Hud.Draw(c, this);
		Arena.DrawBanner(c);
	}

	/// <summary>Lát nền vô hạn: chỉ vẽ các tile đang nằm trong màn hình.</summary>
	private void DrawFloor(Canvas c)
	{
		float tw = _floor.Width, th = _floor.Height;
		Vector2 tl = Cam - ViewSize / 2f;
		float x0 = MathF.Floor(tl.X / tw) * tw, y0 = MathF.Floor(tl.Y / th) * th;
		for (float y = y0; y < tl.Y + ViewSize.Y; y += th)
			for (float x = x0; x < tl.X + ViewSize.X; x += tw)
				c.DrawTexture(_floor, new Rect2(x, y, tw + 0.6f, th + 0.6f)); // chồng nhẹ 0.6px để không hở khe giữa các tile khi phóng to số lẻ
	}
}
