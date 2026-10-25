using System.ComponentModel;
using System.Numerics;

namespace CatSurvivor;

/// <summary>
/// Thanh trượt giá trị 0..100 vẽ theo skin pixel-art: kéo chuột, bấm vào thanh, hoặc phím ← → (±5), Home/End.
/// ValueChanged bắn liên tục khi kéo; Committed bắn khi nhả chuột / nhả phím (dùng để lưu hoặc phát âm thử).
/// </summary>
public class PixelSlider : Control
{
	private readonly Canvas _canvas = new();
	private int _value = 50;
	private bool _drag;

	[DefaultValue(50)]
	public int Value
	{
		get => _value;
		set
		{
			value = Mx.Clamp(value, 0, 100);
			if (value == _value) return;
			_value = value;
			Invalidate();
			ValueChanged?.Invoke(this, EventArgs.Empty);
		}
	}

	public event EventHandler ValueChanged;
	public event EventHandler Committed;

	public PixelSlider()
	{
		SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
			| ControlStyles.ResizeRedraw | ControlStyles.Selectable | ControlStyles.SupportsTransparentBackColor, true);
		BackColor = Color.Transparent;
		Cursor = Cursors.Hand;
		TabStop = true;
	}

	private int Factor => UiSkin.PixelFactor(DeviceDpi);

	/// <summary>Vùng thanh trượt (chừa bên phải chỗ hiện số %).</summary>
	private Rectangle Track
	{
		get
		{
			int textW = 5 * 6 * 2 * Factor; // "100%" ở cỡ chữ 2
			int h = Math.Max(12 * Factor, Height / 2);
			return new Rectangle(0, (Height - h) / 2, Width - textW - 8 * Factor, h);
		}
	}

	private void SetFromMouse(int x)
	{
		var t = Track;
		float k = t.Width <= 0 ? 0f : (x - t.X) / (float)t.Width;
		Value = (int)MathF.Round(Mx.Clamp(k, 0f, 1f) * 100f / 5f) * 5; // bước 5%
	}

	protected override void OnMouseDown(MouseEventArgs e) { Focus(); if (e.Button == MouseButtons.Left) { _drag = true; Capture = true; SetFromMouse(e.X); } base.OnMouseDown(e); }
	protected override void OnMouseMove(MouseEventArgs e) { if (_drag) SetFromMouse(e.X); base.OnMouseMove(e); }
	protected override void OnMouseUp(MouseEventArgs e)
	{
		if (_drag) { _drag = false; Capture = false; Committed?.Invoke(this, EventArgs.Empty); }
		base.OnMouseUp(e);
	}

	protected override bool IsInputKey(Keys keyData) => (keyData & Keys.KeyCode) is Keys.Left or Keys.Right or Keys.Home or Keys.End || base.IsInputKey(keyData);

	protected override void OnKeyDown(KeyEventArgs e)
	{
		switch (e.KeyCode)
		{
			case Keys.Left: Value -= 5; break;
			case Keys.Right: Value += 5; break;
			case Keys.Home: Value = 0; break;
			case Keys.End: Value = 100; break;
			default: base.OnKeyDown(e); return;
		}
		e.Handled = true;
	}

	protected override void OnKeyUp(KeyEventArgs e)
	{
		if (e.KeyCode is Keys.Left or Keys.Right or Keys.Home or Keys.End) Committed?.Invoke(this, EventArgs.Empty);
		base.OnKeyUp(e);
	}

	protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
	protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
	protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }

	protected override void OnPaint(PaintEventArgs e)
	{
		try
		{
			_canvas.Begin(e.Graphics, 1f, PointF.Empty);
			int f = Factor, u = 2 * f;
			var t = Track;
			bool focus = Focused && ShowFocusCues;
			Rgba accent = !Enabled ? UiSkin.TextDim.WithAlpha(0.5f) : focus ? UiSkin.AccentBright : UiSkin.Accent;

			// rãnh: viền đen + viền màu + nền tối
			_canvas.FillRect(t.X, t.Y, t.Width, t.Height, new Rgba(0.02f, 0.03f, 0.05f));
			_canvas.FillRect(t.X + u / 2f, t.Y + u / 2f, t.Width - u, t.Height - u, accent.Lerp(Rgba.Black, 0.35f));
			var inner = new RectangleF(t.X + u, t.Y + u, t.Width - 2 * u, t.Height - 2 * u);
			_canvas.FillRect(inner.X, inner.Y, inner.Width, inner.Height, new Rgba(0.05f, 0.06f, 0.10f));

			// phần đã đầy
			float fillW = inner.Width * _value / 100f;
			if (fillW > 0f)
			{
				_canvas.FillRect(inner.X, inner.Y, fillW, inner.Height, accent);
				_canvas.FillRect(inner.X, inner.Y, fillW, Math.Max(1, inner.Height / 3f), new Rgba(1f, 1f, 1f, 0.22f));
			}

			// núm kéo
			float kw = 6 * f, kx = inner.X + fillW - kw / 2f;
			kx = Mx.Clamp(kx, t.X, t.Right - kw);
			_canvas.FillRect(kx - f, t.Y - 3 * f, kw + 2 * f, t.Height + 6 * f, new Rgba(0.02f, 0.03f, 0.05f));
			_canvas.FillRect(kx, t.Y - 2 * f, kw, t.Height + 4 * f, Enabled ? UiSkin.Text : UiSkin.TextDim);

			// số %
			string label = $"{_value}%";
			int scale = 2 * f;
			float tw = PixelFont.Measure(_canvas, label, scale);
			float tx = Width - tw, ty = (Height - PixelFont.LineHeight * scale) / 2f;
			PixelFont.Draw(_canvas, label, new Vector2(tx + f, ty + f), scale, new Rgba(0f, 0f, 0f, 0.65f));
			PixelFont.Draw(_canvas, label, new Vector2(tx, ty), scale, Enabled ? UiSkin.Text : UiSkin.TextDim);
		}
		catch (Exception) { base.OnPaint(e); }
	}
}
