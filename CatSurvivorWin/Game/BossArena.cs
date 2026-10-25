using System.Numerics;

namespace CatSurvivor;

/// <summary>
/// Màn đánh boss. Khi người chơi đạt cấp tối đa (xong menu nâng cấp cuối):
///  - khoá một VÙNG chữ nhật quanh mèo (Balance.BossArenaSize): mèo, quái, boss không ra khỏi được; vùng RỘNG HƠN màn hình, camera đi theo mèo nhưng không nhìn ra ngoài khung
///  - dọn quái thường, ngừng sinh quái / XP
///  - xuất hiện boss (xem BossAI.cs). Boss chết -> màn chiến thắng.
/// Không vẽ viền: camera bị chặn ngay mép vùng khoá nên mép màn hình chính là "tường".
/// </summary>
public sealed class BossArena
{
	public const string BossName = "Mắt Quỷ Đột Biến";

	private readonly World _w;

	public bool Active { get; private set; }
	public Rect2 Rect { get; private set; }

	/// <summary>Boss hiện tại (null nếu chưa bắt đầu).</summary>
	public Enemy Boss { get; private set; }

	private bool _pendingStart;
	private float _bannerTime;
	private float _victoryTimer = -1f;

	public BossArena(World w)
	{
		_w = w;
		w.State.ReachedMaxLevel += () => _pendingStart = true;
	}

	/// <summary>Ghim một điểm (có tính bán kính vật thể r) vào trong vùng khoá; không khoá thì giữ nguyên.</summary>
	public Vector2 Clamp(Vector2 p, float r)
	{
		if (!Active) return p;
		return new Vector2(
			Mx.Clamp(p.X, Rect.Position.X + r, Rect.End.X - r),
			Mx.Clamp(p.Y, Rect.Position.Y + r, Rect.End.Y - r));
	}

	public void Update(float dt)
	{
		var player = _w.Player;

		// Chờ menu nâng cấp cuối đóng (game hết tạm dừng) rồi mới mở màn boss - Update chỉ chạy khi game đang chạy
		if (_pendingStart && !player.Dead)
		{
			_pendingStart = false;
			StartBoss(player);
		}
		if (!Active) return;

		if (_bannerTime > 0f) _bannerTime -= dt;

		// Mọi boss chết -> dọn quái nhỏ, đếm lùi rồi hiện màn chiến thắng
		if (Boss != null && Boss.Dead && !_w.Enemies.AnyBossAlive() && _victoryTimer < 0f)   // cả hai mắt chính (giai đoạn 3) phải chết
		{
			_victoryTimer = 3f;
			_w.Enemies.KillMinions(Boss);
			_w.State.BossDefeated = true;
		}
		if (_victoryTimer >= 0f)
		{
			_victoryTimer -= dt;
			if (_victoryTimer <= 0f)
			{
				_victoryTimer = -1000f; // chỉ báo một lần
				_w.RaiseEnded(true);
			}
		}
	}

	private void StartBoss(Player player)
	{
		Vector2 center = player.Pos;
		Rect = new Rect2(center - Balance.BossArenaSize / 2f, Balance.BossArenaSize);
		Active = true;
		_w.State.BossActive = true;

		_w.Enemies.ClearForBoss();
		Boss = _w.Enemies.SpawnBoss(new Vector2(center.X, Rect.Position.Y + 70f));

		_bannerTime = 3f;
		SoundService.Play("boss", 0f, 0.5f);
	}

	/// <summary>
	/// Tâm camera khi đang khoá vùng: theo mèo nhưng không bao giờ nhìn ra ngoài mép vùng khoá (mép màn hình dừng đúng ở mép vùng);
	/// trục nào vùng nhỏ hơn màn hình thì đặt giữa vùng.
	/// </summary>
	public Vector2 CameraCenter(Vector2 playerPos, Vector2 view)
	{
		var lo = Rect.Position + view / 2f;
		var hi = Rect.End - view / 2f;
		return new Vector2(
			lo.X < hi.X ? Mx.Clamp(playerPos.X, lo.X, hi.X) : Rect.Center.X,
			lo.Y < hi.Y ? Mx.Clamp(playerPos.Y, lo.Y, hi.Y) : Rect.Center.Y);
	}

	/// <summary>Biểu ngữ BOSS (toạ độ màn hình logic), mờ dần sau vài giây.</summary>
	public void DrawBanner(Canvas c)
	{
		if (_bannerTime <= 0f) return;
		float a = Mx.Clamp(_bannerTime / 0.8f, 0f, 1f);
		var col = new Rgba(1f, 0.35f, 0.25f, a);
		PixelFont.DrawCentered(c, "!  BOSS  !", Balance.ViewWidth / 2f, 90f, 3, col);
		PixelFont.DrawCentered(c, BossName, Balance.ViewWidth / 2f, 124f, 3, col);
	}
}
