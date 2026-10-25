using System.Drawing.Drawing2D;
using System.Globalization;

namespace CatSurvivor;

public enum SkinState { Normal, Hover, Pressed, Disabled }

/// <summary>
/// "Da" giao diện dùng chung cho mọi menu/form: nút, khung (panel), nền, logo.
/// Nếu có asset trong Assets/ui/ thì dùng asset (xem docs/UI_ASSETS.md), không thì tự vẽ kiểu pixel-art theo palette của game:
///   button_normal.png  button_hover.png  button_pressed.png  button_disabled.png   (hover/pressed/disabled thiếu thì suy ra từ normal)
///   panel.png          khung nội dung của form (9-slice)
///   menu_bg.png        ảnh nền menu (phủ kín form)
///   logo.png           logo tên game (thay chữ "CAT SURVIVOR" ở menu chính)
///   skin.txt           cấu hình: button_margin, button_scale, panel_margin, panel_scale, bg_mode, accent, text
/// Ảnh nút/khung được vẽ kiểu 9-slice: `margin` = độ dày viền TRONG ẢNH (px), `scale` = phóng viền lên bao nhiêu lần khi vẽ.
/// </summary>
public static class UiSkin
{
	// ---- Palette (khớp màu của game)
	public static Rgba Accent = new(0.95f, 0.65f, 0.20f);
	public static Rgba AccentBright = new(1.00f, 0.86f, 0.38f);
	public static Rgba Text = new(0.94f, 0.94f, 0.97f);
	public static Rgba TextDim = new(0.66f, 0.69f, 0.78f);
	public static Rgba Danger = new(1.00f, 0.42f, 0.36f);
	public static Rgba Good = new(0.55f, 0.95f, 0.55f);
	public static readonly Color FormBack = Color.FromArgb(14, 18, 26);
	public static readonly Color FieldBack = Color.FromArgb(12, 15, 23);
	public static readonly Color FieldText = Color.FromArgb(232, 234, 242);

	// ---- Asset tuỳ chọn (null = tự vẽ)
	public static Bitmap ButtonNormal, ButtonHover, ButtonPressed, ButtonDisabled, PanelImage, Background, Logo;
	public static int ButtonMargin = 6, ButtonScale = 2, PanelMargin = 6, PanelScale = 2;
	public static string BackgroundMode = "cover"; // cover | stretch | tile

	private static bool _loaded;

	/// <summary>Nạp skin (một lần). Mọi lỗi đều được bỏ qua: thiếu asset thì dùng hình vẽ tay mặc định.</summary>
	public static void Load()
	{
		if (_loaded) return;
		_loaded = true;
		try
		{
			ReadConfig();
			ButtonNormal = AssetStore.OptionalImage("ui/button_normal.png");
			ButtonHover = AssetStore.OptionalImage("ui/button_hover.png");
			ButtonPressed = AssetStore.OptionalImage("ui/button_pressed.png");
			ButtonDisabled = AssetStore.OptionalImage("ui/button_disabled.png");
			PanelImage = AssetStore.OptionalImage("ui/panel.png");
			Background = AssetStore.OptionalImage("ui/menu_bg.png");
			Logo = AssetStore.OptionalImage("ui/logo.png");
		}
		catch (Exception ex)
		{
			AssetStore.Report("Assets/ui (skin)", ex.Message);
		}
	}

	private static void ReadConfig()
	{
		string path = AssetStore.PathOf("ui/skin.txt");
		if (!File.Exists(path)) return;
		foreach (var raw in File.ReadAllLines(path))
		{
			string line = raw.Trim();
			if (line.Length == 0 || line.StartsWith('#')) continue;
			int eq = line.IndexOf('=');
			if (eq <= 0) continue;
			string key = line.Substring(0, eq).Trim().ToLowerInvariant(), val = line.Substring(eq + 1).Trim();
			switch (key)
			{
				case "button_margin": if (int.TryParse(val, out int bm)) ButtonMargin = Math.Max(1, bm); break;
				case "button_scale": if (int.TryParse(val, out int bs)) ButtonScale = Math.Max(1, bs); break;
				case "panel_margin": if (int.TryParse(val, out int pm)) PanelMargin = Math.Max(1, pm); break;
				case "panel_scale": if (int.TryParse(val, out int ps)) PanelScale = Math.Max(1, ps); break;
				case "bg_mode": BackgroundMode = val.ToLowerInvariant(); break;
				case "accent": if (TryColor(val, out var a)) Accent = a; AccentBright = Brighten(Accent); break;
				case "text": if (TryColor(val, out var t)) Text = t; break;
			}
		}
	}

	private static bool TryColor(string hex, out Rgba c)
	{
		c = default;
		hex = hex.TrimStart('#');
		if (hex.Length != 6 || !int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int v)) return false;
		c = new Rgba(((v >> 16) & 255) / 255f, ((v >> 8) & 255) / 255f, (v & 255) / 255f);
		return true;
	}

	private static Rgba Brighten(Rgba c) => c.Lerp(Rgba.White, 0.35f);

	/// <summary>Hệ số phóng pixel-art theo DPI màn hình (100% = 1, 175%+ = 2...). Chữ/viền pixel luôn là bội số nguyên để sắc nét.</summary>
	public static int PixelFactor(int dpi) => Math.Max(1, (int)Math.Floor(dpi / 96.0 + 0.25));

	public static bool HasCustomButton => ButtonNormal != null;

	// ====================================================================== NÚT
	/// <summary>Vẽ nền nút trong hình chữ nhật r (toạ độ pixel của control). highlight = nút nhấn mạnh (vd. Bắt đầu).</summary>
	public static void DrawButton(Canvas c, Rectangle r, SkinState state, bool highlight, int dpi)
	{
		Load();
		if (ButtonNormal == null) { DrawFallbackButton(c, r, state, highlight, PixelFactor(dpi) * 2); return; }

		Bitmap tex = state switch
		{
			SkinState.Hover => ButtonHover ?? ButtonNormal,
			SkinState.Pressed => ButtonPressed ?? ButtonNormal,
			SkinState.Disabled => ButtonDisabled ?? ButtonNormal,
			_ => ButtonNormal,
		};
		float dm = ButtonMargin * ButtonScale * (dpi / 96f);
		Rgba? tint = null;
		if (state == SkinState.Pressed && ReferenceEquals(tex, ButtonNormal)) tint = new Rgba(0.78f, 0.78f, 0.78f);
		if (state == SkinState.Disabled && ReferenceEquals(tex, ButtonNormal)) tint = new Rgba(0.5f, 0.5f, 0.55f, 0.75f);
		var rect = new Rect2(r.X, r.Y, r.Width, r.Height);

		if (tint.HasValue) DrawNineSliceTinted(c, tex, rect, ButtonMargin, dm, tint.Value);
		else c.DrawNineSlice(tex, rect, ButtonMargin, true, dm);

		if (state == SkinState.Hover && ReferenceEquals(tex, ButtonNormal))
			c.FillRect(r.X, r.Y, r.Width, r.Height, new Rgba(1f, 1f, 1f, 0.10f)); // không có ảnh hover: sáng nhẹ
		if (highlight)
			c.DrawRect(new Rect2(r.X + 1, r.Y + 1, r.Width - 3, r.Height - 3), Accent.WithAlpha(0.9f), false, 2f);
	}

	private static void DrawNineSliceTinted(Canvas c, Bitmap tex, Rect2 dest, float sm, float m, Rgba tint)
	{
		float tw = tex.Width, th = tex.Height;
		float x0 = dest.Position.X, y0 = dest.Position.Y, x1 = x0 + m, y1 = y0 + m;
		float x3 = x0 + dest.Size.X, y3 = y0 + dest.Size.Y, x2 = x3 - m, y2 = y3 - m;
		void P(float dx, float dy, float dw, float dh, float sx, float sy, float sw, float sh) =>
			c.DrawTexture(tex, new Rect2(dx, dy, dw, dh), new Rect2(sx, sy, sw, sh), tint);
		P(x0, y0, m, m, 0, 0, sm, sm); P(x2, y0, m, m, tw - sm, 0, sm, sm);
		P(x0, y2, m, m, 0, th - sm, sm, sm); P(x2, y2, m, m, tw - sm, th - sm, sm, sm);
		P(x1, y0, x2 - x1, m, sm, 0, tw - 2 * sm, sm); P(x1, y2, x2 - x1, m, sm, th - sm, tw - 2 * sm, sm);
		P(x0, y1, m, y2 - y1, 0, sm, sm, th - 2 * sm); P(x2, y1, m, y2 - y1, tw - sm, sm, sm, th - 2 * sm);
		P(x1, y1, x2 - x1, y2 - y1, sm, sm, tw - 2 * sm, th - 2 * sm);
	}

	/// <summary>Hình chữ nhật cắt 4 góc (mỗi góc bớt một "pixel" u) - nét pixel-art đặc trưng.</summary>
	private static void Notched(Canvas c, float x, float y, float w, float h, float u, Rgba col)
	{
		if (w <= 2 * u || h <= 2 * u) { c.FillRect(x, y, w, h, col); return; }
		c.FillRect(x + u, y, w - 2 * u, h, col);
		c.FillRect(x, y + u, w, h - 2 * u, col);
	}

	private static void DrawFallbackButton(Canvas c, Rectangle r, SkinState st, bool highlight, int u)
	{
		Rgba outline = new(0.02f, 0.03f, 0.05f);
		Rgba border, top, bottom;
		switch (st)
		{
			case SkinState.Hover:
				border = AccentBright; top = new Rgba(0.27f, 0.29f, 0.41f); bottom = new Rgba(0.15f, 0.16f, 0.24f); break;
			case SkinState.Pressed:
				border = Accent.Lerp(Rgba.Black, 0.3f); top = new Rgba(0.08f, 0.09f, 0.14f); bottom = new Rgba(0.13f, 0.14f, 0.21f); break;
			case SkinState.Disabled:
				border = new Rgba(0.33f, 0.34f, 0.40f); top = new Rgba(0.14f, 0.14f, 0.17f); bottom = new Rgba(0.10f, 0.10f, 0.12f); break;
			default:
				border = Accent; top = new Rgba(0.19f, 0.21f, 0.31f); bottom = new Rgba(0.10f, 0.11f, 0.18f); break;
		}
		if (highlight && st != SkinState.Disabled)
		{
			border = st == SkinState.Hover ? Rgba.White.Lerp(AccentBright, 0.4f) : AccentBright;
			top = new Rgba(0.40f, 0.30f, 0.10f); bottom = new Rgba(0.22f, 0.15f, 0.05f);
			if (st == SkinState.Hover) { top = new Rgba(0.52f, 0.39f, 0.13f); bottom = new Rgba(0.30f, 0.21f, 0.07f); }
		}

		float x = r.X, y = r.Y, w = r.Width, h = r.Height;
		Notched(c, x, y, w, h, u, outline);                           // viền đen ngoài
		Notched(c, x + u, y + u, w - 2 * u, h - 2 * u, u, border);    // viền màu
		float ix = x + 2 * u, iy = y + 2 * u, iw = w - 4 * u, ih = h - 4 * u;
		if (iw <= 0 || ih <= 0) return;
		c.FillRect(ix, iy, iw, ih / 2f, top);                         // nền 2 dải (giả gradient dạng pixel)
		c.FillRect(ix, iy + ih / 2f, iw, ih - ih / 2f, bottom);
		if (st != SkinState.Pressed && st != SkinState.Disabled)
		{
			c.FillRect(ix, iy, iw, u / 2f, new Rgba(1f, 1f, 1f, 0.14f));                 // vạch sáng trên cùng
			c.FillRect(ix, iy + ih - u / 2f, iw, u / 2f, new Rgba(0f, 0f, 0f, 0.28f));   // vạch tối dưới cùng
		}
	}

	// ====================================================================== KHUNG
	/// <summary>Vẽ khung nội dung (panel) lớn của form.</summary>
	public static void DrawPanel(Canvas c, Rectangle r, int dpi)
	{
		Load();
		if (PanelImage != null)
		{
			c.DrawNineSlice(PanelImage, new Rect2(r.X, r.Y, r.Width, r.Height), PanelMargin, true, PanelMargin * PanelScale * (dpi / 96f));
			return;
		}
		int u = PixelFactor(dpi) * 2;
		Rgba outline = new(0.02f, 0.03f, 0.05f);
		Notched(c, r.X, r.Y, r.Width, r.Height, u, outline);
		Notched(c, r.X + u, r.Y + u, r.Width - 2 * u, r.Height - 2 * u, u, Accent.Lerp(Rgba.Black, 0.15f));
		Notched(c, r.X + 2 * u, r.Y + 2 * u, r.Width - 4 * u, r.Height - 4 * u, u, new Rgba(0.04f, 0.05f, 0.09f, 0.90f));
		// đường viền mảnh bên trong cho có chiều sâu
		c.DrawRect(new Rect2(r.X + 3 * u, r.Y + 3 * u, r.Width - 6 * u, r.Height - 6 * u), new Rgba(0.95f, 0.65f, 0.2f, 0.18f), false, 1f);
	}

	// ====================================================================== NỀN
	/// <summary>Vẽ nền menu phủ kín r: ảnh menu_bg.png nếu có, không thì gradient + hoa văn cỏ mờ + tối góc.</summary>
	public static void DrawBackground(Canvas c, Rectangle r, int dpi)
	{
		Load();
		var g = c.G;
		var oldClip = g.Clip;
		g.SetClip(r, CombineMode.Intersect);
		try
		{
			if (Background != null) { DrawImageBackground(c, r); return; }

			using (var br = new LinearGradientBrush(r, Color.FromArgb(22, 44, 34), Color.FromArgb(8, 12, 18), 90f))
				g.FillRectangle(br, r);

			// hoa văn ô cỏ của game, rất mờ
			var grass = AssetStore.Image("environment/grass.png");
			float tile = grass.Width * 2 * PixelFactor(dpi);
			var tint = new Rgba(0.55f, 0.85f, 0.65f, 0.16f);
			for (float y = r.Y; y < r.Bottom; y += tile)
				for (float x = r.X; x < r.Right; x += tile)
					c.DrawTexture(grass, new Rect2(x, y, tile + 0.6f, tile + 0.6f), tint);

			// tối dần về các góc
			using var path = new GraphicsPath();
			path.AddRectangle(r);
			using var vg = new PathGradientBrush(path)
			{
				CenterColor = Color.FromArgb(0, 0, 0, 0),
				SurroundColors = new[] { Color.FromArgb(200, 0, 0, 0) },
				FocusScales = new PointF(0.55f, 0.55f),
			};
			g.FillRectangle(vg, r);
		}
		finally
		{
			g.Clip = oldClip;
		}
	}

	private static void DrawImageBackground(Canvas c, Rectangle r)
	{
		var bg = Background;
		if (BackgroundMode == "stretch")
		{
			c.DrawTexture(bg, new Rect2(r.X, r.Y, r.Width, r.Height));
		}
		else if (BackgroundMode == "tile")
		{
			int s = Math.Max(1, r.Width / Math.Max(1, bg.Width * 2));
			for (float y = r.Y; y < r.Bottom; y += bg.Height * s)
				for (float x = r.X; x < r.Right; x += bg.Width * s)
					c.DrawTexture(bg, new Rect2(x, y, bg.Width * s + 0.6f, bg.Height * s + 0.6f));
		}
		else // cover: phủ kín, giữ tỉ lệ, cắt phần thừa
		{
			float k = MathF.Max((float)r.Width / bg.Width, (float)r.Height / bg.Height);
			float w = bg.Width * k, h = bg.Height * k;
			c.DrawTexture(bg, new Rect2(r.X + (r.Width - w) / 2f, r.Y + (r.Height - h) / 2f, w, h));
		}
	}
}
