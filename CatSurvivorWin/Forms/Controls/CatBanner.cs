using System.ComponentModel;
using System.Numerics;

namespace CatSurvivor;

/// <summary>
/// Dải minh hoạ động ở đầu menu chính: chú mèo chạy giữa, slime/bomber/dơi từ hai bên tiến vào (dùng sprite thật của game).
/// Nếu có Assets/ui/logo.png thì vẽ logo thay thế dải này.
/// </summary>
public class CatBanner : Control
{
	private readonly Canvas _canvas = new();
	private readonly System.Windows.Forms.Timer _timer = new() { Interval = 90 };
	private int _tick;

	public CatBanner()
	{
		SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
			| ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
		BackColor = Color.Transparent;
		TabStop = false;
		_timer.Tick += (_, _) => { _tick++; if (Visible && !DesignMode) Invalidate(); };
	}

	protected override void OnHandleCreated(EventArgs e)
	{
		base.OnHandleCreated(e);
		if (!DesignMode) _timer.Start();
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing) _timer.Dispose();
		base.Dispose(disposing);
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		try
		{
			_canvas.Begin(e.Graphics, 1f, PointF.Empty);
			UiSkin.Load();
			if (UiSkin.Logo != null)
			{
				float k = MathF.Min((float)Width / UiSkin.Logo.Width, (float)Height / UiSkin.Logo.Height);
				float w = UiSkin.Logo.Width * k, h = UiSkin.Logo.Height * k;
				_canvas.DrawTexture(UiSkin.Logo, new Rect2((Width - w) / 2f, (Height - h) / 2f, w, h));
				return;
			}

			int f = UiSkin.PixelFactor(DeviceDpi);
			float ground = Height - 6 * f;
			float cx = Width / 2f;
			float unit = Math.Min(Width / 380f, Height / 100f); // co theo kích thước control

			var cat = AssetStore.Image("characters/main_character.png");
			var slime = AssetStore.Image("enemies/slime.png");
			var bomber = AssetStore.Image("enemies/bomber.png");
			var bat = AssetStore.Image("enemies/bat.png");

			// Bóng + sprite, nhịp nhún theo _tick
			float s = 1.5f * unit;
			Sprite(slime, _tick % 7, 64, new Vector2(32, 40), 7f, cx - 150 * unit, ground, s, flip: true, shadow: 14 * unit);
			Sprite(bomber, (_tick / 2) % 4, 64, new Vector2(30, 40), 6f, cx - 85 * unit, ground, s, flip: true, shadow: 13 * unit);
			Sprite(cat, (_tick / 2) % 4, 32, new Vector2(16, 28), 2f, cx, ground, 3.4f * unit, flip: false, shadow: 18 * unit);
			float bob = MathF.Sin(_tick * 0.5f) * 3f * unit;
			Sprite(bat, _tick % 8, 64, new Vector2(32, 31), 16f, cx + 90 * unit, ground - bob, s, flip: true, shadow: 11 * unit, groundY: ground);
			Sprite(slime, (_tick + 3) % 7, 64, new Vector2(32, 40), 7f, cx + 150 * unit, ground, s, flip: false, shadow: 14 * unit);
		}
		catch (Exception) { /* trang trí: lỗi vẽ thì bỏ qua */ }
	}

	/// <summary>Vẽ một frame: center = điểm gốc của sprite trong khung, footDown = chân thấp hơn điểm đó bao nhiêu px; chân đặt tại baseY, bóng đặt ở groundY.</summary>
	private void Sprite(Bitmap tex, int frame, int fsize, Vector2 center, float footDown, float x, float baseY, float scale, bool flip, float shadow, float? groundY = null)
	{
		_canvas.SetTransform(new Vector2(x, groundY ?? baseY), 0f, new Vector2(1f, 0.35f));
		_canvas.DrawCircle(Vector2.Zero, shadow, new Rgba(0f, 0f, 0f, 0.35f));
		_canvas.SetTransform(new Vector2(x, baseY - footDown * scale), 0f, new Vector2(flip ? -scale : scale, scale));
		_canvas.DrawTexture(tex, new Rect2(-center.X, -center.Y, fsize, fsize), new Rect2(frame * fsize, 0, fsize, fsize));
		_canvas.ResetTransform();
	}
}
