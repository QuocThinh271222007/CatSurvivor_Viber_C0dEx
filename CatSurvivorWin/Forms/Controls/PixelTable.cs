using System.ComponentModel;
using System.Numerics;

namespace CatSurvivor;

/// <summary>Một dòng của bảng. Color: màu chữ riêng (null = mặc định). Highlight: tô nền vàng (vd. thành tích vừa lưu).</summary>
public class PixelTableRow
{
	public string[] Cells;
	public Rgba? Color;
	public bool Highlight;
}

/// <summary>
/// Bảng dữ liệu vẽ bằng font pixel (dùng cho Bảng thành tích và Thông tin nhóm). Chỉ đọc, không chọn dòng.
/// Cột khai báo bằng AddColumn(tiêu đề, bề rộng thiết kế, căn lề); chữ dài quá bề rộng cột sẽ bị cắt bằng "…".
/// </summary>
public class PixelTable : Control
{
	private readonly Canvas _canvas = new();
	private readonly List<(string title, int width, HorizontalAlignment align)> _cols = new();

	[Browsable(false)] public List<PixelTableRow> Rows { get; } = new();
	[DefaultValue(2)] public int PixelScale { get; set; } = 2;
	[DefaultValue("")] public string EmptyText { get; set; } = "";

	public PixelTable()
	{
		SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
		BackColor = UiSkin.FieldBack;
		TabStop = false;
	}

	public void AddColumn(string title, int width, HorizontalAlignment align = HorizontalAlignment.Left)
	{
		_cols.Add((title, width, align));
		Invalidate();
	}

	public void ClearRows() { Rows.Clear(); Invalidate(); }

	protected override void OnPaint(PaintEventArgs e)
	{
		try { PaintSkin(e.Graphics); }
		catch (Exception) { base.OnPaint(e); }
	}

	private void PaintSkin(Graphics g)
	{
		_canvas.Begin(g, 1f, PointF.Empty);
		int f = UiSkin.PixelFactor(DeviceDpi);
		int scale = Math.Max(1, PixelScale * f);
		float rowH = PixelFont.LineHeight * scale + 10 * f;
		float dpi = DeviceDpi / 96f;

		_canvas.FillRect(0, 0, Width, Height, new Rgba(0.03f, 0.04f, 0.07f, 0.92f));
		_canvas.DrawRect(new Rect2(0, 0, Width - 1, Height - 1), UiSkin.Accent.WithAlpha(0.45f), false, 1f);

		// bề rộng cột: bề rộng thiết kế co/giãn cho vừa chiều ngang control
		float sum = _cols.Sum(c => c.width);
		float k = sum > 0 ? Width / sum : 1f;
		float[] xs = new float[_cols.Count + 1];
		for (int i = 0; i < _cols.Count; i++) xs[i + 1] = xs[i] + _cols[i].width * k;

		float pad = 8 * dpi;
		// tiêu đề
		_canvas.FillRect(1, 1, Width - 2, rowH, UiSkin.Accent.WithAlpha(0.16f));
		for (int i = 0; i < _cols.Count; i++)
			DrawCell(_cols[i].title, xs[i], xs[i + 1], 1 + (rowH - PixelFont.LineHeight * scale) / 2f, scale, _cols[i].align, UiSkin.AccentBright, pad);
		_canvas.FillRect(1, 1 + rowH, Width - 2, Math.Max(1, f), UiSkin.Accent.WithAlpha(0.8f));

		if (Rows.Count == 0 && !string.IsNullOrEmpty(EmptyText))
		{
			string text = EmptyText;
			float w = PixelFont.Measure(_canvas, text, scale);
			PixelFont.Draw(_canvas, text, new Vector2((Width - w) / 2f, rowH * 2.5f), scale, UiSkin.TextDim);
		}

		float y = 1 + rowH + f;
		for (int r = 0; r < Rows.Count; r++)
		{
			var row = Rows[r];
			if (y + rowH > Height) break;
			if (row.Highlight) _canvas.FillRect(1, y, Width - 2, rowH, new Rgba(1f, 0.85f, 0.2f, 0.28f));
			else if (r % 2 == 1) _canvas.FillRect(1, y, Width - 2, rowH, new Rgba(1f, 1f, 1f, 0.04f));

			Rgba col = row.Color ?? (row.Highlight ? new Rgba(1f, 0.95f, 0.5f) : UiSkin.Text);
			float ty = y + (rowH - PixelFont.LineHeight * scale) / 2f;
			for (int i = 0; i < _cols.Count && i < row.Cells.Length; i++)
				DrawCell(row.Cells[i], xs[i], xs[i + 1], ty, scale, _cols[i].align, col, pad);
			y += rowH;
		}
	}

	private void DrawCell(string text, float x0, float x1, float y, int scale, HorizontalAlignment align, Rgba color, float pad)
	{
		float avail = x1 - x0 - 2 * pad;
		text = Fit(text ?? "", avail, scale);
		float w = PixelFont.Measure(_canvas, text, scale);
		float x = align == HorizontalAlignment.Right ? x1 - pad - w : align == HorizontalAlignment.Center ? x0 + (x1 - x0 - w) / 2f : x0 + pad;
		PixelFont.Draw(_canvas, text, new Vector2(x, y), scale, color);
	}

	/// <summary>Cắt bớt chữ và thêm "…" nếu rộng hơn avail.</summary>
	private string Fit(string text, float avail, int scale)
	{
		if (PixelFont.Measure(_canvas, text, scale) <= avail) return text;
		while (text.Length > 1 && PixelFont.Measure(_canvas, text + "…", scale) > avail) text = text.Substring(0, text.Length - 1);
		return text + "…";
	}
}
