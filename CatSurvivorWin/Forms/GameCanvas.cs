using System.Numerics;

namespace CatSurvivor;

/// <summary>
/// Control hiển thị game: vẽ World ở độ phân giải logic 640x360, phóng to đều (giữ tỉ lệ 16:9, phóng nguyên số lần, viền đen nếu cửa sổ không vừa khít).
/// Có chống nháy (double buffer). Chuột trên control được đổi sang toạ độ logic để vũ khí ngắm theo chuột.
/// </summary>
public sealed class GameCanvas : Control
{
	private readonly Canvas _canvas = new();
	private bool _barsDirty = true;
	private FrameBuffer _frame;       // khung hình 640x360 (vẽ thế giới ở đây rồi phóng to)
	private FrameBuffer _scratch;     // bộ đệm trung gian cho kiểu phóng "sharp"
	private bool _sharp;
	private float _scale = 1f;
	private PointF _offset;

	/// <summary>Ván chơi đang hiển thị (null = chỉ tô đen).</summary>
	[System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
	public World World { get; set; }

	public GameCanvas()
	{
		// KHÔNG dùng bộ đệm kép của WinForms: khung hình đã dựng sẵn trong FrameBuffer rồi phóng to bằng MỘT lệnh StretchBlt thẳng lên cửa sổ,
		// nên đệm kép chỉ thêm một lần chép cả màn hình (vài ms ở 1080p).
		SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.Opaque, true);
		SetStyle(ControlStyles.OptimizedDoubleBuffer, false);
		DoubleBuffered = false;
		BackColor = Color.Black;
		TabStop = false;
		Cursor = Cursors.Cross;
	}

	protected override void OnPaintBackground(PaintEventArgs e) { /* tự tô trong OnPaint */ }

	private void UpdateLayout()
	{
		float fit = MathF.Max(0.1f, MathF.Min(ClientSize.Width / Balance.ViewWidth, ClientSize.Height / Balance.ViewHeight));
		// Gần bội nguyên (>= 80% cỡ vừa khít) thì phóng NGUYÊN số lần (điểm ảnh đều tăm tắp, viền đen mỏng). Lệch nhiều thì lấp đầy cửa sổ theo tỉ lệ lẻ,
		// vẽ kiểu "sharp" (xem FrameBuffer.BlitSharp) để vẫn nét mà không bỏ phí màn hình (toàn màn hình / phóng to cửa sổ).
		float whole = MathF.Floor(fit);
		_sharp = fit >= 1f && whole / fit < 0.80f;
		_scale = fit >= 1f && !_sharp ? whole : fit;
		_offset = new PointF((ClientSize.Width - Balance.ViewWidth * _scale) / 2f, (ClientSize.Height - Balance.ViewHeight * _scale) / 2f);
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		var g = e.Graphics;
		if (World == null) { g.Clear(Color.Black); return; }

		UpdateLayout();
		var dest = Rectangle.Round(new RectangleF(_offset.X, _offset.Y, Balance.ViewWidth * _scale, Balance.ViewHeight * _scale));

		// Viền đen (khi cửa sổ lệch tỉ lệ 16:9): chỉ tô lại khi cần (đổi kích thước/lần vẽ đầu), không tô mỗi khung kẻo nhấp nháy
		if (_barsDirty || e.ClipRectangle.Width < ClientSize.Width || e.ClipRectangle.Height < ClientSize.Height)
		{
			g.Clear(Color.Black);
			_barsDirty = false;
		}

		_frame ??= new FrameBuffer((int)Balance.ViewWidth, (int)Balance.ViewHeight);
		if (_frame.Ok)
		{
			// Thế giới + HUD: vẽ ở độ phân giải gốc rồi phóng to MỘT lần bằng StretchBlt (nhanh, pixel-art sắc nét).
			// (Trước đây HUD vẽ lại ở độ phân giải cửa sổ - chữ ở độ phân giải cao tốn ~2-5 ms mỗi khung.)
			_frame.G.Clear(Color.Black);
			_canvas.Begin(_frame.G, 1f, PointF.Empty, antialias: false);
			World.DrawWorld(_canvas);
			World.DrawOverlay(_canvas);
			if (_sharp) _frame.BlitSharp(g, dest, ref _scratch);
			else _frame.BlitTo(g, dest);
		}
		else
		{
			// Dự phòng: không tạo được khung đệm thì vẽ trực tiếp (chậm hơn khi đông quái)
			g.SetClip(dest);
			_canvas.Begin(g, _scale, _offset);
			World.Draw(_canvas);
		}
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing) { _frame?.Dispose(); _frame = null; _scratch?.Dispose(); _scratch = null; }
		base.Dispose(disposing);
	}

	protected override void OnMouseMove(MouseEventArgs e)
	{
		base.OnMouseMove(e);
		UpdateLayout();
		if (World != null)
			World.Input.MouseView = new Vector2((e.X - _offset.X) / _scale, (e.Y - _offset.Y) / _scale);
	}

	protected override void OnResize(EventArgs e)
	{
		base.OnResize(e);
		_barsDirty = true;
		Invalidate();
	}
}
