using System.ComponentModel;
using System.Numerics;

namespace CatSurvivor;

/// <summary>
/// Nút bấm tự vẽ bằng skin của game (xem UiSkin) + chữ font pixel Aseprite. Là con của Button nên vẫn có Click, phím Enter/Space,
/// Tab, AcceptButton/CancelButton như bình thường. Phát âm "click" khi bấm.
/// Dòng đầu của Text là tiêu đề; các dòng sau (nếu có) là mô tả nhỏ hơn, tự xuống dòng theo bề rộng (dùng cho thẻ nâng cấp).
/// </summary>
public class PixelButton : Button
{
	private readonly Canvas _canvas = new();
	private bool _hover, _down;

	/// <summary>Cỡ chữ pixel (bội số của 8px: 1, 2, 3...). Tự nhân thêm theo DPI màn hình.</summary>
	[DefaultValue(2)] public int PixelScale { get; set; } = 2;

	/// <summary>Nút nhấn mạnh (viền/nền vàng), vd. nút "Bắt đầu" hoặc phần thưởng hợp nhất.</summary>
	[DefaultValue(false)] public bool Highlight { get; set; }

	/// <summary>Hình minh hoạ bên trái thẻ (vd. món giáp); vẽ phóng nguyên số lần cho pixel vuông vức. null = không có.</summary>
	[Browsable(false)] [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
	public Bitmap IconImage { get; set; }

	public PixelButton()
	{
		SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
			| ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
		FlatStyle = FlatStyle.Flat;
		FlatAppearance.BorderSize = 0;
		BackColor = Color.Transparent;
		Cursor = Cursors.Hand;
		UseVisualStyleBackColor = false;
		TextAlign = ContentAlignment.MiddleCenter;
	}

	private int Factor => UiSkin.PixelFactor(DeviceDpi);
	private int TitleScale => Math.Max(1, PixelScale * Factor);
	private int DescScale => Math.Max(1, TitleScale / 2);
	private int Pad => (int)(10 * DeviceDpi / 96f) + 2 * Factor;
	private int IconBox => IconImage == null ? 0 : (int)(Math.Clamp(Math.Max(IconImage.Width, IconImage.Height), 64, 192) * DeviceDpi / 96f); // ô vuông chứa icon (to hơn nếu là hình lá bài)          // ô vuông chứa icon
	private float TextLeft => Pad + 2 * Factor + (IconImage == null ? 0 : IconBox + Pad);  // lề trái của chữ (chừa chỗ cho icon)

	// ------------------------------------------------------------------ trạng thái chuột/phím
	protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
	protected override void OnMouseLeave(EventArgs e) { _hover = false; _down = false; Invalidate(); base.OnMouseLeave(e); }
	protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) _down = true; Invalidate(); base.OnMouseDown(e); }
	protected override void OnMouseUp(MouseEventArgs e) { _down = false; Invalidate(); base.OnMouseUp(e); }
	protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
	protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
	protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
	protected override void OnTextChanged(EventArgs e) { Invalidate(); base.OnTextChanged(e); }

	protected override void OnClick(EventArgs e)
	{
		SoundService.Play("click", -6f, 0.03f);
		base.OnClick(e);
	}

	// ------------------------------------------------------------------ vẽ
	protected override void OnPaint(PaintEventArgs e)
	{
		try { PaintSkin(e.Graphics); }
		catch (Exception) { base.OnPaint(e); } // lỗi vẽ skin thì rơi về nút mặc định, không văng
	}

	private void PaintSkin(Graphics g)
	{
		_canvas.Begin(g, 1f, PointF.Empty);
		var state = !Enabled ? SkinState.Disabled : _down ? SkinState.Pressed : (_hover || (Focused && ShowFocusCues)) ? SkinState.Hover : SkinState.Normal;
		UiSkin.DrawButton(_canvas, ClientRectangle, state, Highlight, DeviceDpi);

		Rgba title = state switch
		{
			SkinState.Disabled => UiSkin.TextDim.WithAlpha(0.55f),
			SkinState.Hover => UiSkin.AccentBright,
			_ => Highlight ? UiSkin.AccentBright : UiSkin.Text,
		};
		Rgba desc = state == SkinState.Disabled ? UiSkin.TextDim.WithAlpha(0.5f) : state == SkinState.Hover ? UiSkin.Text : UiSkin.TextDim;

		var lines = BuildLines(TextWidth);
		DrawIcon(g);
		float total = TotalHeight(lines);
		float y = (Height - total) / 2f + (state == SkinState.Pressed ? Factor : 0);
		bool left = TextAlign is ContentAlignment.MiddleLeft or ContentAlignment.TopLeft or ContentAlignment.BottomLeft;
		Rgba shadow = new(0f, 0f, 0f, 0.65f);

		for (int i = 0; i < lines.Count; i++)
		{
			var (text, scale) = lines[i];
			Rgba col = i == 0 ? title : desc;
			float w = PixelFont.Measure(_canvas, text, scale);
			float x = left ? TextLeft : (Width - w) / 2f;
			PixelFont.Draw(_canvas, text, new Vector2(x + Factor, y + Factor), scale, shadow);
			PixelFont.Draw(_canvas, text, new Vector2(x, y), scale, col);
			y += LineHeight(scale, i == 0 && lines.Count > 1);
		}
	}

		private float TextWidth => Width - TextLeft - Pad - 2 * Factor;

	private void DrawIcon(Graphics g)
	{
		if (IconImage == null) return;
		// Phóng nguyên số lần (nearest) vừa khít ô icon, canh giữa theo chiều cao thẻ
		int k = Math.Max(1, Math.Min(IconBox / IconImage.Width, IconBox / IconImage.Height));
		int w = IconImage.Width * k, h = IconImage.Height * k;
		int x = Pad + 2 * Factor + (IconBox - w) / 2, y = (Height - h) / 2;
		var old = (g.InterpolationMode, g.PixelOffsetMode);
		g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
		g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
		g.DrawImage(IconImage, new Rectangle(x, y, w, h), 0, 0, IconImage.Width, IconImage.Height, GraphicsUnit.Pixel);
		(g.InterpolationMode, g.PixelOffsetMode) = old;
	}

	private static float LineHeight(int scale, bool afterTitle) => (PixelFont.LineHeight + 2) * scale + (afterTitle ? scale : 0);

	/// <summary>Dòng đầu = tiêu đề (scale lớn), các dòng sau = mô tả (scale nhỏ, tự xuống dòng).</summary>
	private List<(string text, int scale)> BuildLines(float maxWidth)
	{
		var parts = Text.Replace("\r", "").Split('\n', 2);
		var lines = new List<(string, int)>();
		if (parts.Length == 1)
		{
			lines.Add((parts[0], TitleScale));
			return lines;
		}
		foreach (var l in PixelFont.Wrap(_canvas, parts[0], maxWidth, TitleScale)) lines.Add((l, TitleScale));
		foreach (var l in PixelFont.Wrap(_canvas, parts[1], maxWidth, DescScale)) lines.Add((l, DescScale));
		return lines;
	}

	private float TotalHeight(List<(string text, int scale)> lines)
	{
		float h = 0f;
		for (int i = 0; i < lines.Count; i++) h += LineHeight(lines[i].scale, i == 0 && lines.Count > 1);
		return h;
	}

	/// <summary>Chiều cao cần thiết để hiện đủ chữ (kể cả mô tả nhiều dòng) ở bề rộng hiện tại. Dùng cho thẻ nâng cấp.</summary>
	public int MeasureHeight()
	{
		using var bmp = new Bitmap(1, 1);
		using var g = Graphics.FromImage(bmp);
		_canvas.Begin(g, 1f, PointF.Empty);
		return (int)MathF.Max(TotalHeight(BuildLines(TextWidth)) + 2 * Pad, IconImage == null ? 0 : IconBox + 2 * Pad);
	}
}
