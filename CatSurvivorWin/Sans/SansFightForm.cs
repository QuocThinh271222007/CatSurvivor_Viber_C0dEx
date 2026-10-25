using System.Diagnostics;

namespace CatSurvivor;

/// <summary>
/// Control hiển thị trận Sans: vẽ ở độ phân giải logic 640x480 rồi phóng to đều (giữ tỉ lệ 4:3, viền đen nếu cửa sổ lệch) - cùng cách làm của GameCanvas.
/// </summary>
internal sealed class SansCanvas : Control
{
	private readonly Canvas _canvas = new();
	private FrameBuffer _frame, _scratch;
	private bool _sharp, _barsDirty = true;
	private float _scale = 1f;
	private PointF _offset;

	public SansFight Fight { get; set; }

	public SansCanvas()
	{
		SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.Opaque, true);
		SetStyle(ControlStyles.OptimizedDoubleBuffer, false);
		DoubleBuffered = false;
		BackColor = Color.Black;
		TabStop = false;
		Cursor = Cursors.Default;
	}

	protected override void OnPaintBackground(PaintEventArgs e) { }

	private void UpdateLayout()
	{
		float fit = MathF.Max(0.1f, MathF.Min(ClientSize.Width / SansFight.W, ClientSize.Height / SansFight.H));
		float whole = MathF.Floor(fit);
		_sharp = fit >= 1f && whole / fit < 0.80f;       // lệch xa bội nguyên: lấp đầy cửa sổ bằng cách phóng "sharp" (xem FrameBuffer.BlitSharp)
		_scale = fit >= 1f && !_sharp ? whole : fit;
		_offset = new PointF((ClientSize.Width - SansFight.W * _scale) / 2f, (ClientSize.Height - SansFight.H * _scale) / 2f);
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		var g = e.Graphics;
		if (Fight == null) { g.Clear(Color.Black); return; }
		UpdateLayout();
		var dest = Rectangle.Round(new RectangleF(_offset.X, _offset.Y, SansFight.W * _scale, SansFight.H * _scale));
		if (_barsDirty || e.ClipRectangle.Width < ClientSize.Width || e.ClipRectangle.Height < ClientSize.Height)
		{
			g.Clear(Color.Black);
			_barsDirty = false;
		}
		_frame ??= new FrameBuffer((int)SansFight.W, (int)SansFight.H);
		if (!_frame.Ok) return;
		_frame.G.Clear(Color.Black);
		_canvas.Begin(_frame.G, 1f, PointF.Empty, antialias: false);
		Fight.Draw(_canvas);
		if (_sharp) _frame.BlitSharp(g, dest, ref _scratch);
		else _frame.BlitTo(g, dest);
	}

	protected override void OnResize(EventArgs e)
	{
		base.OnResize(e);
		_barsDirty = true;
		Invalidate();
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing) { _frame?.Dispose(); _frame = null; _scratch?.Dispose(); _scratch = null; }
		base.Dispose(disposing);
	}
}

/// <summary>
/// Cửa sổ trận đánh Sans (easter egg). Mũi tên/WASD: di chuyển trái tim · Z/Enter: xác nhận · X/Shift: huỷ + đi chậm · Esc: bỏ cuộc · M: bật/tắt âm.
/// Đóng cửa sổ khi trận kết thúc; Result cho biết thắng (Sans kiệt sức) hay thua (hết máu) hay bỏ cuộc (Running).
/// </summary>
internal sealed class SansFightForm : Form
{
	private const float Step = 1f / 120f;      // bước mô phỏng cố định (va chạm xương chính xác hơn)

	private readonly SansFight _fight = new();
	private readonly SansCanvas _canvas = new() { Dock = DockStyle.Fill };
	private readonly System.Windows.Forms.Timer _timer = new() { Interval = 1 };
	private readonly Stopwatch _clock = Stopwatch.StartNew();
	private long _last;
	private float _acc;
	private bool _highRes, _fullscreen, _asking;
	private Rectangle _restore;

	[System.Runtime.InteropServices.DllImport("winmm.dll")] private static extern uint timeBeginPeriod(uint ms);
	[System.Runtime.InteropServices.DllImport("winmm.dll")] private static extern uint timeEndPeriod(uint ms);

	public SansFight.Outcome Result => _fight.Result;

	public SansFightForm()
	{
		AutoScaleMode = AutoScaleMode.None;
		Text = "* Bad Time";
		BackColor = Color.Black;
		KeyPreview = true;
		StartPosition = FormStartPosition.CenterScreen;
		DoubleBuffered = false;
		MinimumSize = new Size(320, 240);

		var work = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1280, 720);
		int k = Math.Max(1, (int)Math.Floor(Math.Min((work.Width - 40) / SansFight.W, (work.Height - 90) / SansFight.H)));
		ClientSize = new Size((int)SansFight.W * k, (int)SansFight.H * k);

		_canvas.Fight = _fight;
		Controls.Add(_canvas);
		_timer.Tick += (_, _) => Tick();
		KeyDown += (_, e) => OnKey(e, true);
		KeyUp += (_, e) => OnKey(e, false);
		Deactivate += (_, _) => _fight.Pad.Clear();
		Shown += (_, _) => Program.ShowAssetReport(this);
	}

	/// <summary>Để kiểm thử: truy cập trận đang chạy.</summary>
	internal SansFight Fight => _fight;

	protected override void OnShown(EventArgs e)
	{
		base.OnShown(e);
		if (Settings.Fullscreen)
		{
			_restore = Bounds;
			FormBorderStyle = FormBorderStyle.None;
			Bounds = Screen.FromControl(this).Bounds;
			_fullscreen = true;
		}
		try { _highRes = timeBeginPeriod(1) == 0; } catch (Exception) { }
		_timer.Interval = _highRes ? 1 : 8;
		_last = _clock.ElapsedTicks;
		SoundService.BgmMuted = true;     // nhạc nền của game im đi, nhạc Megalovania sẽ vào đúng lúc của nó
		_timer.Start();
	}

	private void OnKey(KeyEventArgs e, bool down)
	{
		if (down)
		{
			if (e.KeyCode == Keys.Escape) { e.Handled = true; Quit(); return; }
			if (e.KeyCode == Keys.M) { SoundService.Toggle(); e.Handled = true; return; }
			if (Cheats.Enabled) HandleCheat(e);
			_fight.Pad.KeyDown(e.KeyCode);
		}
		else _fight.Pad.KeyUp(e.KeyCode);
		if (e.KeyCode is Keys.Up or Keys.Down or Keys.Left or Keys.Right or Keys.Space or Keys.Enter) e.Handled = true;
	}

	/// <summary>Cheat (chỉ bản Debug): F2 nhảy tới đòn cuối, F3 bật/tắt bất tử.</summary>
	private void HandleCheat(KeyEventArgs e)
	{
		if (e.KeyCode == Keys.F2) _fight.CheatSkipToFinal();
		else if (e.KeyCode == Keys.F3) _fight.God = !_fight.God;
		else return;
		e.Handled = true;
	}

	protected override bool ProcessDialogKey(Keys keyData)
	{
		Keys k = keyData & Keys.KeyCode;
		if (k is Keys.Up or Keys.Down or Keys.Left or Keys.Right or Keys.Enter) return false;   // để KeyDown của form nhận
		return base.ProcessDialogKey(keyData);
	}

	private void Quit()
	{
		if (_asking) return;
		_asking = true;
		_timer.Stop();
		_fight.Pad.Clear();
		var r = MessageBox.Show(this, "Bỏ cuộc trận đấu với Sans?", "* Bad Time", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
		_asking = false;
		if (r == DialogResult.Yes) { DialogResult = DialogResult.Cancel; Close(); return; }
		_last = _clock.ElapsedTicks;
		_timer.Start();
	}

	private void Tick()
	{
		long now = _clock.ElapsedTicks;
		float elapsed = (float)((now - _last) / (double)Stopwatch.Frequency);
		_last = now;
		_acc = MathF.Min(_acc + elapsed, 0.1f);       // lag (kéo cửa sổ...) thì không nhảy vọt
		bool ran = false;
		while (_acc >= Step && _fight.Result == SansFight.Outcome.Running)
		{
			_fight.Update(Step);
			_acc -= Step;
			ran = true;
		}
		if (ran) { _canvas.Invalidate(); _canvas.Update(); }

		if (_fight.Result != SansFight.Outcome.Running)
		{
			_timer.Stop();
			DialogResult = DialogResult.OK;
			Close();
		}
	}

	protected override void OnFormClosed(FormClosedEventArgs e)
	{
		_timer.Stop();
		if (_highRes) { try { timeEndPeriod(1); } catch (Exception) { } _highRes = false; }
		SoundService.StopExtra("*");
		SoundService.StopExtraMusic();
		SoundService.BgmMuted = false;
		base.OnFormClosed(e);
	}
}
