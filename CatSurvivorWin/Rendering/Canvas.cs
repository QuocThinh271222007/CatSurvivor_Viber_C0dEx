using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Numerics;

namespace CatSurvivor;

/// <summary>
/// Lớp vẽ 2D bọc GDI+ (System.Drawing) với các hàm vẽ quen thuộc: DrawCircle, DrawArc, DrawLine, DrawPolygon, DrawTexture...
/// Hai chế độ toạ độ:
///   WorldMode(cam)  : toạ độ thế giới game, camera nhìn vào điểm `cam` (góc trên-trái màn hình logic)
///   ScreenMode()    : toạ độ màn hình logic 640x360 (dùng cho HUD)
/// SetTransform(vị trí, góc xoay, tỉ lệ) đặt hệ toạ độ cục bộ cho các lệnh vẽ kế tiếp (giống DrawSetTransform của Godot).
/// </summary>
public sealed class Canvas
{
	public Graphics G { get; private set; }

	/// <summary>Hệ số phóng hiện tại của khung vẽ (pixel thực / pixel logic).</summary>
	public float Scale => _scale;

	private float _scale = 1f;
	private PointF _offset;
	private Matrix _base = new();
	private readonly SolidBrush _brush = new(Color.White);
	private readonly Pen _pen = new(Color.White);
	private readonly Dictionary<int, ImageAttributes> _attrs = new();
	private readonly PointF[] _imgPts = new PointF[3];
	private readonly Matrix _scratch = new();

	public Canvas()
	{
		_pen.LineJoin = LineJoin.Round;
		_pen.StartCap = LineCap.Round;
		_pen.EndCap = LineCap.Round;
	}

	/// <summary>Bắt đầu một khung hình. scale/offset: phóng to đều + căn giữa (viền đen khi cửa sổ không đúng tỉ lệ 16:9).</summary>
	public void Begin(Graphics g, float scale, PointF offset, bool antialias = true)
	{
		G = g;
		_scale = scale;
		_offset = offset;
		g.SmoothingMode = antialias ? SmoothingMode.AntiAlias : SmoothingMode.None; // thế giới pixel-art ở độ phân giải gốc: không khử răng cưa (nhanh hơn, đúng kiểu pixel)
		g.InterpolationMode = InterpolationMode.NearestNeighbor; // pixel-art: phóng to không làm mờ
		g.PixelOffsetMode = PixelOffsetMode.Half;
		g.CompositingQuality = CompositingQuality.HighSpeed;
		ScreenMode();
	}

	public void ScreenMode() => SetBase(new Vector2(0f, 0f));

	/// <summary>cam = toạ độ thế giới của góc trên-trái màn hình logic.</summary>
	public void WorldMode(Vector2 cam) => SetBase(cam);

	private void SetBase(Vector2 cam)
	{
		_base.Dispose();
		_base = new Matrix(_scale, 0f, 0f, _scale, _offset.X - cam.X * _scale, _offset.Y - cam.Y * _scale);
		G.Transform = _base;
	}

	/// <summary>Đặt hệ toạ độ cục bộ: dời tới `offset`, xoay `rot` (radian), co giãn `scale`.</summary>
	public void SetTransform(Vector2 offset, float rot, Vector2 scale)
	{
		var m = _scratch; // ma trận tạm dùng lại (G.Transform sao chép giá trị nên an toàn)
		m.Reset();
		m.Multiply(_base);
		m.Translate(offset.X, offset.Y);
		if (rot != 0f) m.Rotate(Mx.RadToDeg(rot));
		if (scale.X != 1f || scale.Y != 1f) m.Scale(scale.X, scale.Y);
		G.Transform = m;
	}

	public void ResetTransform()
	{
		G.Transform = _base;
	}

	// ------------------------------------------------------------------ hình cơ bản
	public void FillRect(float x, float y, float w, float h, Rgba c)
	{
		_brush.Color = c.ToColor();
		G.FillRectangle(_brush, x, y, w, h);
	}

	public void DrawRect(Rect2 r, Rgba c, bool filled = true, float width = 1f)
	{
		if (filled) FillRect(r.Position.X, r.Position.Y, r.Size.X, r.Size.Y, c);
		else
		{
			_pen.Color = c.ToColor();
			_pen.Width = width;
			G.DrawRectangle(_pen, r.Position.X, r.Position.Y, r.Size.X, r.Size.Y);
		}
	}

	/// <summary>Tô hình chữ nhật chuyển màu tuyến tính từ rom sang 	o (angle: 0 = trái -> phải, 90 = trên -> dưới).</summary>
	public void FillGradientRect(RectangleF r, Rgba from, Rgba to, float angle)
	{
		if (r.Width <= 0f || r.Height <= 0f) return;
		using var br = new LinearGradientBrush(r, from.ToColor(), to.ToColor(), angle);
		G.FillRectangle(br, r);
	}

	/// <summary>Tô hình elip (tâm, bán kính ngang rx, dọc ry) - không đổi ma trận nên rẻ hơn SetTransform + DrawCircle.</summary>
	public void FillEllipse(Vector2 center, float rx, float ry, Rgba c)
	{
		if (rx <= 0f || ry <= 0f) return;
		_brush.Color = c.ToColor();
		G.FillEllipse(_brush, center.X - rx, center.Y - ry, rx * 2f, ry * 2f);
	}

	public void DrawEllipse(Vector2 center, float rx, float ry, Rgba c, float width)
	{
		if (rx <= 0.5f || ry <= 0.5f) return;
		_pen.Color = c.ToColor();
		_pen.Width = width;
		G.DrawEllipse(_pen, center.X - rx, center.Y - ry, rx * 2f, ry * 2f);
	}

	public void DrawCircle(Vector2 center, float r, Rgba c)
	{
		if (r <= 0f) return;
		_brush.Color = c.ToColor();
		G.FillEllipse(_brush, center.X - r, center.Y - r, r * 2f, r * 2f);
	}

	/// <summary>Cung tròn từ góc `from` tới `to` (radian, chiều tăng = chiều kim đồng hồ trên màn hình).</summary>
	public void DrawArc(Vector2 center, float r, float from, float to, Rgba c, float width)
	{
		if (r <= 0.5f) return;
		float sweep = Mx.RadToDeg(to - from);
		if (MathF.Abs(sweep) < 0.1f) return;
		sweep = Mx.Clamp(sweep, -360f, 360f);
		_pen.Color = c.ToColor();
		_pen.Width = width;
		G.DrawArc(_pen, center.X - r, center.Y - r, r * 2f, r * 2f, Mx.RadToDeg(from), sweep);
	}

	public void DrawLine(Vector2 a, Vector2 b, Rgba c, float width)
	{
		_pen.Color = c.ToColor();
		_pen.Width = width;
		G.DrawLine(_pen, a.X, a.Y, b.X, b.Y);
	}

	public void DrawPolyline(Vector2[] pts, Rgba c, float width)
	{
		if (pts.Length < 2) return;
		_pen.Color = c.ToColor();
		_pen.Width = width;
		G.DrawLines(_pen, ToPoints(pts));
	}

	public void DrawPolygon(Vector2[] pts, Rgba c)
	{
		if (pts.Length < 3) return;
		_brush.Color = c.ToColor();
		G.FillPolygon(_brush, ToPoints(pts));
	}

	private static PointF[] ToPoints(Vector2[] pts)
	{
		var p = new PointF[pts.Length];
		for (int i = 0; i < pts.Length; i++) p[i] = new PointF(pts[i].X, pts[i].Y);
		return p;
	}

	// ------------------------------------------------------------------ ảnh
	/// <summary>Vẽ vùng `src` của ảnh vào hình chữ nhật `dest` (toạ độ cục bộ). dest.Size âm = lật ảnh. tint = nhân màu/độ trong suốt.</summary>
	public void DrawTexture(Bitmap tex, Rect2 dest, Rect2 src, Rgba? tint = null)
	{
		if (tex == null) return;
		float x = dest.Position.X, y = dest.Position.Y, w = dest.Size.X, h = dest.Size.Y;
		var pts = _imgPts; // dùng lại mảng (tránh cấp phát mỗi lần vẽ)
		pts[0] = new PointF(x, y); pts[1] = new PointF(x + w, y); pts[2] = new PointF(x, y + h);
		G.DrawImage(tex, pts, new RectangleF(src.Position.X, src.Position.Y, src.Size.X, src.Size.Y), GraphicsUnit.Pixel, Attributes(tint));
	}

	/// <summary>
	/// Vẽ một vùng sprite KHÔNG qua SetTransform: đặt điểm gốc pivot của sprite tại pos, co giãn (sx, sy) - sx/sy âm = lật.
	/// Nhanh hơn rõ rệt khi vẽ hàng trăm sprite mỗi khung (không phải tạo/đặt ma trận cho từng con). Không hỗ trợ xoay.
	/// </summary>
	public void DrawSpriteDirect(Bitmap tex, Rect2 src, Vector2 pos, Vector2 pivot, float sx, float sy, Rgba? tint = null)
	{
		if (tex == null) return;
		float w = src.Size.X * sx, h = src.Size.Y * sy;
		float x = pos.X - pivot.X * sx, y = pos.Y - pivot.Y * sy;
		var pts = _imgPts;
		pts[0] = new PointF(x, y); pts[1] = new PointF(x + w, y); pts[2] = new PointF(x, y + h);
		G.DrawImage(tex, pts, new RectangleF(src.Position.X, src.Position.Y, src.Size.X, src.Size.Y), GraphicsUnit.Pixel, Attributes(tint));
	}

	public void DrawTexture(Bitmap tex, Rect2 dest, Rgba? tint = null) =>
		DrawTexture(tex, dest, new Rect2(0f, 0f, tex.Width, tex.Height), tint);

	/// <summary>Vẽ kiểu 9-slice: 4 góc giữ nguyên, 4 cạnh giãn một chiều, phần giữa giãn hai chiều (không vẽ phần giữa nếu fillCenter = false).</summary>
	public void DrawNineSlice(Bitmap tex, Rect2 dest, float margin, bool fillCenter = false, float destMargin = -1f)
	{
		if (tex == null) return;
		float tw = tex.Width, th = tex.Height;
		float sm = margin;                              // độ dày viền trong ẢNH
		float m = destMargin >= 0f ? destMargin : margin; // độ dày viền khi VẼ (phóng to viền pixel-art bằng cách đặt lớn hơn)
		float x0 = dest.Position.X, y0 = dest.Position.Y, x1 = x0 + m, y1 = y0 + m;
		float x3 = x0 + dest.Size.X, y3 = y0 + dest.Size.Y, x2 = x3 - m, y2 = y3 - m;
		void Part(float dx, float dy, float dw, float dh, float sx, float sy, float sw, float sh) =>
			DrawTexture(tex, new Rect2(dx, dy, dw, dh), new Rect2(sx, sy, sw, sh));

		Part(x0, y0, m, m, 0, 0, sm, sm);                                       // 4 góc
		Part(x2, y0, m, m, tw - sm, 0, sm, sm);
		Part(x0, y2, m, m, 0, th - sm, sm, sm);
		Part(x2, y2, m, m, tw - sm, th - sm, sm, sm);
		Part(x1, y0, x2 - x1, m, sm, 0, tw - 2 * sm, sm);                       // 4 cạnh
		Part(x1, y2, x2 - x1, m, sm, th - sm, tw - 2 * sm, sm);
		Part(x0, y1, m, y2 - y1, 0, sm, sm, th - 2 * sm);
		Part(x2, y1, m, y2 - y1, tw - sm, sm, sm, th - 2 * sm);
		if (fillCenter) Part(x1, y1, x2 - x1, y2 - y1, sm, sm, tw - 2 * sm, th - 2 * sm);
	}

	/// <summary>ImageAttributes: nhân màu (tint) + chống lem mép khi phóng to. Lưu đệm theo màu đã lượng tử hoá 8-bit.</summary>
	private ImageAttributes _plain;

	public ImageAttributes Attributes(Rgba? tint)
	{
		if (!tint.HasValue && _plain != null) return _plain; // đường nhanh: không tô màu
		int key = 0;
		if (tint.HasValue)
		{
			var t = tint.Value;
			static int B(float v) => (int)MathF.Round(Mx.Clamp(v, 0f, 1f) * 255f);
			key = (B(t.A) << 24) | (B(t.R) << 16) | (B(t.G) << 8) | B(t.B);
			if (key == unchecked((int)0xFFFFFFFF)) key = 0;
		}
		if (_attrs.TryGetValue(key, out var ia)) { if (key == 0) _plain = ia; return ia; }

		ia = new ImageAttributes();
		ia.SetWrapMode(WrapMode.TileFlipXY);
		if (key != 0)
		{
			float a = ((key >> 24) & 255) / 255f, r = ((key >> 16) & 255) / 255f, g = ((key >> 8) & 255) / 255f, b = (key & 255) / 255f;
			ia.SetColorMatrix(new ColorMatrix(new[]
			{
				new[] { r, 0f, 0f, 0f, 0f },
				new[] { 0f, g, 0f, 0f, 0f },
				new[] { 0f, 0f, b, 0f, 0f },
				new[] { 0f, 0f, 0f, a, 0f },
				new[] { 0f, 0f, 0f, 0f, 1f },
			}));
		}
		_attrs[key] = ia;
		if (key == 0) _plain = ia;
		return ia;
	}
}
