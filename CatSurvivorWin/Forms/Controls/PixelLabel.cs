using System.ComponentModel;
using System.Numerics;

namespace CatSurvivor;

/// <summary>
/// Nhãn chữ pixel (font Aseprite) trong suốt, tự xuống dòng theo bề rộng. Màu = ForeColor, cỡ = PixelScale (bội số của 8px).
/// Căn lề bằng HAlign; có đổ bóng cho dễ đọc trên nền ảnh.
/// </summary>
public class PixelLabel : Control
{
	private readonly Canvas _canvas = new();

	[DefaultValue(2)] public int PixelScale { get; set; } = 2;
	[DefaultValue(true)] public bool Shadow { get; set; } = true;
	[DefaultValue(HorizontalAlignment.Center)] public HorizontalAlignment HAlign { get; set; } = HorizontalAlignment.Center;
	[DefaultValue(true)] public bool VCenter { get; set; } = true;

	public PixelLabel()
	{
		SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
			| ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
		BackColor = Color.Transparent;
		ForeColor = Color.FromArgb(240, 240, 247);
		TabStop = false;
	}

	[Browsable(true), EditorBrowsable(EditorBrowsableState.Always)]
	public override string Text { get => base.Text; set { base.Text = value; Invalidate(); } }

	protected override void OnForeColorChanged(EventArgs e) { Invalidate(); base.OnForeColorChanged(e); }

	protected override void OnPaint(PaintEventArgs e)
	{
		try
		{
			_canvas.Begin(e.Graphics, 1f, PointF.Empty);
			int f = UiSkin.PixelFactor(DeviceDpi);
			int scale = Math.Max(1, PixelScale * f);
			var lines = PixelFont.Wrap(_canvas, Text ?? "", Width, scale);
			float lineH = (PixelFont.LineHeight + 2) * scale;
			float y = VCenter ? (Height - lines.Count * lineH) / 2f : 0f;
			Rgba col = Rgba.From(ForeColor);
			foreach (var line in lines)
			{
				float w = PixelFont.Measure(_canvas, line, scale);
				float x = HAlign == HorizontalAlignment.Left ? 0f : HAlign == HorizontalAlignment.Right ? Width - w : (Width - w) / 2f;
				if (Shadow) PixelFont.Draw(_canvas, line, new Vector2(x + f, y + f), scale, new Rgba(0f, 0f, 0f, 0.7f));
				PixelFont.Draw(_canvas, line, new Vector2(x, y), scale, col);
				y += lineH;
			}
		}
		catch (Exception) { base.OnPaint(e); }
	}
}
