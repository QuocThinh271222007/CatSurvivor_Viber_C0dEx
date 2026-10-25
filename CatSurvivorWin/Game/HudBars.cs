using System.Drawing.Imaging;
using System.Numerics;
using System.Runtime.InteropServices;

namespace CatSurvivor;

/// <summary>
/// Thanh máu / mana / XP của HUD, vẽ từ asset của bạn (xuất bằng tools\export_assets.ps1 từ "heal bar and mana bar.aseprite"):
///  - Assets/ui/hud_bars.png : thanh đỏ (máu) ở trên, thanh xanh (mana) ở dưới - thanh đỏ (máu) ở trên, thanh xanh dương (mana) ở giữa, thanh xanh lá (XP) ở dưới; vị trí được DÒ TỰ ĐỘNG từ các dải pixel có hình
///  - Assets/ui/hud_head.png : đầu + chân mèo thò lên trên thanh máu (vẽ ĐÈ lên phần máu)
/// Mỗi thanh vẽ kiểu 3 mảnh (đầu trái + phần giữa kéo giãn + đầu phải) nên đặt chiều dài tuỳ ý mà không méo pixel.
/// Phần máu còn lại = thanh sáng cắt theo tỉ lệ, đặt đè lên chính thanh đó tối đi (viền đen vẫn liền mạch).
/// Chưa vẽ thanh XP (chỉ có 2 thanh) thì dùng lại hình thanh mana đổi sang xanh lá. Thiếu asset thì Available = false và HUD tự vẽ thanh phẳng.
/// </summary>
public static class HudBars
{
	private const string BarsPath = "ui/hud_bars.png";
	private const string HeadPath = "ui/hud_head.png";
	private const string BossBarPath = "ui/boss_bar.png", BossHeadPath = "ui/boss_bar_head.png";
	private const string HeadSansPath = "ui/hud_head_sans.png";
	private const float Cap = 8f;                       // chiều rộng mỗi đầu bo tròn giữ nguyên, phần giữa mới kéo giãn
	private static readonly Rgba Empty = new(0.28f, 0.28f, 0.32f);   // màu phần thanh còn trống

	private static bool _loaded;
	private static Bitmap _bars, _expBars, _head;
	private static Rect2 _health, _mana, _exp, _headSrc;

	public static bool Available { get { Load(); return _bars != null; } }

	/// <summary>Nạp lại (khi đổi nhân vật: đầu trên thanh máu đổi theo).</summary>
	public static void Reload() { _loaded = false; _head = null; }

	/// <summary>Kích thước gốc của thanh máu (đúng như vẽ trong Aseprite) và phần đầu mèo nhô lên phía trên thanh.</summary>
	public static Vector2 HealthSize { get { Load(); return _health.Size; } }
	public static float HeadOverhang { get { Load(); return _head == null ? 0f : MathF.Max(0f, _health.Position.Y - _headSrc.Position.Y); } }
	public static float BarHeight { get { Load(); return _mana.Size.Y; } }

	/// <summary>Đoạn ngang (tính từ mép trái thanh máu) bị đầu + chân mèo che: X = đầu trái, Y = đầu phải. (0,0) nếu không có đầu mèo.</summary>
	public static Vector2 HeadSpan
	{
		get
		{
			Load();
			if (_head == null) return Vector2.Zero;
			float x0 = _headSrc.Position.X - _health.Position.X;
			return new Vector2(x0, x0 + _headSrc.Size.X);
		}
	}

	private static void Load()
	{
		if (_loaded) return;
		_loaded = true;
		try
		{
			var bars = AssetStore.OptionalImage(BarsPath);
			if (bars == null) return;
			var bands = Bands(bars);
			if (bands.Count < 2) { AssetStore.Report("Assets/" + BarsPath, "cần 2 thanh (máu ở trên, mana ở dưới)"); return; }
			_health = bands[0];
			_mana = bands[1];
			_bars = bars;
			if (bands.Count >= 3) { _exp = bands[2]; _expBars = bars; }   // có thanh XP vẽ tay (thanh thứ 3)
			else { _exp = _mana; _expBars = Recolored(bars); }            // chưa vẽ: dùng thanh mana đổi sang xanh lá

			// Chơi Sans: dùng ui/hud_head_sans.png nếu bạn đã vẽ, không thì thanh máu không có đầu (không dùng đầu mèo)
			_head = AssetStore.OptionalImage(CharacterSkin.Selected == CharacterSkin.Sans ? HeadSansPath : HeadPath);
			if (_head != null)
			{
				var all = Bands(_head);
				if (all.Count == 0) _head = null;
				else
				{
					float x0 = float.MaxValue, y0 = float.MaxValue, x1 = 0f, y1 = 0f;
					foreach (var b in all)
					{
						x0 = MathF.Min(x0, b.Position.X); y0 = MathF.Min(y0, b.Position.Y);
						x1 = MathF.Max(x1, b.End.X); y1 = MathF.Max(y1, b.End.Y);
					}
					_headSrc = new Rect2(x0, y0, x1 - x0, y1 - y0);
				}
			}
		}
		catch (Exception ex)
		{
			AssetStore.Report("Assets/ui/hud_bars.png", ex.Message);
			_bars = null;
		}
	}

	// ------------------------------------------------------------------ vẽ
	/// <summary>Thanh máu ở kích thước gốc; `pos` = góc trên-trái của THANH (đầu mèo nhô lên phía trên pos.Y một đoạn HeadOverhang).</summary>
	public static void DrawHealth(Canvas c, Vector2 pos, float ratio)
	{
		Load();
		if (_bars == null) return;
		DrawBar(c, _bars, _health, pos, _health.Size.X, ratio);
		if (_head != null)
			c.DrawTexture(_head, new Rect2(pos + (_headSrc.Position - _health.Position), _headSrc.Size), _headSrc);
	}

	/// <summary>Thanh mana (xanh dương) với chiều rộng tuỳ ý.</summary>
	public static void DrawMana(Canvas c, Vector2 pos, float width, float ratio)
	{
		Load();
		if (_bars != null) DrawBar(c, _bars, _mana, pos, width, ratio);
	}

	/// <summary>Thanh XP (xanh lá) với chiều rộng tuỳ ý.</summary>
	public static void DrawExp(Canvas c, Vector2 pos, float width, float ratio)
	{
		Load();
		if (_expBars != null) DrawBar(c, _expBars, _exp, pos, width, ratio);
	}

	private static void DrawBar(Canvas c, Bitmap tex, Rect2 band, Vector2 pos, float width, float ratio)
	{
		pos = new Vector2(MathF.Round(pos.X), MathF.Round(pos.Y));
		width = MathF.Round(width);
		Slice(c, tex, band, pos, width, width, Empty);                       // nền: cả thanh, tối đi
		ratio = Mx.Clamp(ratio, 0f, 1f);
		if (ratio > 0f)
			Slice(c, tex, band, pos, width, MathF.Round(1f + (width - 2f) * ratio), null);   // phần đầy: sáng, cắt theo tỉ lệ
	}

	/// <summary>Thanh 3 mảnh, chỉ vẽ phần có x &lt; limit (tính từ mép trái thanh).</summary>
	private static void Slice(Canvas c, Bitmap tex, Rect2 band, Vector2 pos, float width, float limit, Rgba? tint)
	{
		float cap = MathF.Min(Cap, MathF.Floor(width / 2f));
		float sx = band.Position.X, sy = band.Position.Y, sw = band.Size.X, h = band.Size.Y;

		void Piece(float dx, float dw, float srcX, float srcW, bool stretch)
		{
			float vis = MathF.Min(dw, limit - dx);
			if (vis <= 0f) return;
			float sv = stretch ? srcW : srcW * vis / dw;
			c.DrawTexture(tex, new Rect2(pos.X + dx, pos.Y, vis, h), new Rect2(srcX, sy, sv, h), tint);
		}

		Piece(0f, cap, sx, cap, false);                                                  // đầu trái
		Piece(cap, width - 2f * cap, sx + MathF.Floor(sw / 2f), 1f, true);               // giữa: một cột pixel kéo giãn
		Piece(width - cap, cap, sx + sw - cap, cap, false);                              // đầu phải
	}

	// ------------------------------------------------------------------ thanh máu BOSS
	// boss_health_bar.aseprite -> Assets/ui/boss_bar.png ("Layer 1": viền đen + ruột đỏ sọc chéo) + boss_bar_head.png (các layer còn lại: đám mắt thò lên trên thanh).
	// Vẽ NGUYÊN cỡ (không kéo giãn - sọc chéo không bị méo): phần máu = bản sáng cắt theo tỉ lệ, đè lên bản tối cả thanh; khiên bật thì dùng bản đổi sang xanh dương.
	private static bool _bossLoaded;
	private static Bitmap _bossBar, _bossBarBlue, _bossHead;
	private static Rect2 _bossBand, _bossHeadSrc;

	private static void LoadBoss()
	{
		if (_bossLoaded) return;
		_bossLoaded = true;
		try
		{
			var bar = AssetStore.OptionalImage(BossBarPath);
			if (bar == null) return;
			var bands = Bands(bar);
			if (bands.Count == 0) return;
			_bossBand = bands[0];
			_bossBar = bar;
			_bossBarBlue = Recolored(bar);
			_bossHead = AssetStore.OptionalImage(BossHeadPath);
			if (_bossHead != null)
			{
				var all = Bands(_bossHead);
				if (all.Count == 0) _bossHead = null;
				else
				{
					float x0 = float.MaxValue, y0 = float.MaxValue, x1 = 0f, y1 = 0f;
					foreach (var b in all) { x0 = MathF.Min(x0, b.Position.X); y0 = MathF.Min(y0, b.Position.Y); x1 = MathF.Max(x1, b.End.X); y1 = MathF.Max(y1, b.End.Y); }
					_bossHeadSrc = new Rect2(x0, y0, x1 - x0, y1 - y0);
				}
			}
		}
		catch (Exception ex) { AssetStore.Report("Assets/ui/boss_bar.png", ex.Message); _bossBar = null; }
	}

	public static bool BossBarAvailable { get { LoadBoss(); return _bossBar != null; } }

	/// <summary>Kích thước thanh máu boss (đúng như vẽ trong Aseprite).</summary>
	public static Vector2 BossBarSize { get { LoadBoss(); return _bossBand.Size; } }

	/// <summary>Đám mắt nhô lên phía TRÊN thanh bao nhiêu px.</summary>
	public static float BossHeadOverhang { get { LoadBoss(); return _bossHead == null ? 0f : MathF.Max(0f, _bossBand.Position.Y - _bossHeadSrc.Position.Y); } }

	/// <summary>Vẽ thanh máu boss: pos = góc trên-trái của THANH (đám mắt nằm phía trên pos.Y), ratio 0..1, shielded = đang bất khả xâm phạm (thanh xanh dương).</summary>
	public static void DrawBossBar(Canvas c, Vector2 pos, float ratio, bool shielded)
	{
		LoadBoss();
		if (_bossBar == null) return;
		pos = new Vector2(MathF.Round(pos.X), MathF.Round(pos.Y));
		var tex = shielded ? _bossBarBlue : _bossBar;
		float w = _bossBand.Size.X, h = _bossBand.Size.Y;
		c.DrawTexture(tex, new Rect2(pos, new Vector2(w, h)), _bossBand, Empty);                     // nền: cả thanh, tối đi
		ratio = Mx.Clamp(ratio, 0f, 1f);
		if (ratio > 0f)
		{
			float vis = MathF.Round(1f + (w - 2f) * ratio);
			c.DrawTexture(tex, new Rect2(pos, new Vector2(vis, h)), new Rect2(_bossBand.Position, new Vector2(vis, h)));   // phần đầy: sáng, cắt theo tỉ lệ
		}
		if (_bossHead != null)
			c.DrawTexture(_bossHead, new Rect2(pos + (_bossHeadSrc.Position - _bossBand.Position), _bossHeadSrc.Size), _bossHeadSrc);
	}

	// ------------------------------------------------------------------ dò dải thanh + đổi màu
	/// <summary>Các dải thanh (nhóm hàng liên tiếp có pixel hiện hình), từ trên xuống: hộp bao của từng dải.</summary>
	private static List<Rect2> Bands(Bitmap bmp)
	{
		int w = bmp.Width, h = bmp.Height;
		byte[] data = Pixels(bmp, out int stride);
		var list = new List<Rect2>();
		int start = -1, minX = w, maxX = -1;
		for (int y = 0; y <= h; y++)
		{
			bool any = false;
			if (y < h)
				for (int x = 0; x < w; x++)
					if (data[y * stride + x * 4 + 3] > 0) { any = true; if (x < minX) minX = x; if (x > maxX) maxX = x; }
			if (any && start < 0) start = y;
			if (!any && start >= 0)
			{
				list.Add(new Rect2(minX, start, maxX - minX + 1, y - start));
				start = -1; minX = w; maxX = -1;
			}
		}
		return list;
	}

	private static byte[] Pixels(Bitmap bmp, out int stride)
	{
		var bd = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
		try
		{
			stride = bd.Stride;
			byte[] data = new byte[Math.Abs(bd.Stride) * bmp.Height];
			Marshal.Copy(bd.Scan0, data, 0, data.Length);
			return data;
		}
		finally { bmp.UnlockBits(bd); }
	}

	/// <summary>Bản đổi màu của thanh: xoay kênh (R,G,B) -> (G,B,R); xanh dương thành xanh lá, viền đen giữ nguyên.</summary>
	private static Bitmap Recolored(Bitmap src)
	{
		var dst = new Bitmap(src.Width, src.Height, PixelFormat.Format32bppPArgb);
		var rect = new Rectangle(0, 0, src.Width, src.Height);
		byte[] data = Pixels(src, out int stride);
		for (int i = 0; i + 3 < data.Length; i += 4)
		{
			byte b = data[i], g = data[i + 1], r = data[i + 2];
			data[i] = r; data[i + 1] = b; data[i + 2] = g;   // B' = R, G' = B, R' = G
		}
		var bd = dst.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppPArgb);
		try { Marshal.Copy(data, 0, bd.Scan0, data.Length); }
		finally { dst.UnlockBits(bd); }
		return dst;
	}
}
