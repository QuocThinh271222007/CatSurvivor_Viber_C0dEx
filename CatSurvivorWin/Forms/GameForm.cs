using System.Diagnostics;

namespace CatSurvivor;

/// <summary>
/// Form chơi game: chứa GameCanvas, chạy vòng lặp game bằng Timer, nhận phím, và mở các hộp thoại
/// (Tạm dừng, Nâng cấp khi lên cấp, Kết thúc ván). Khi một hộp thoại đang mở thì game dừng hẳn.
///   Esc / P : tạm dừng   ·   M : bật/tắt âm thanh   ·   WASD/mũi tên: di chuyển   ·   Space/Shift: dash
/// </summary>
public partial class GameForm : Form
{
	private readonly World _world;
	private readonly Stopwatch _clock = Stopwatch.StartNew();
	private long _lastTicks;
	private bool _dialogOpen;

	/// <summary>Ván đang chơi (cho kiểm thử).</summary>
	internal World CurrentWorld => _world;

	/// <summary>Người chơi chọn gì khi rời ván: chơi lại, về menu, hay thoát hẳn.</summary>
	public GameOutcome Outcome { get; private set; } = GameOutcome.Quit;

	public GameForm()
	{
		InitializeComponent();
		_world = new World();
		canvas.World = _world;
		_world.Ended += OnEnded;
		Shown += (_, _) => Program.ShowAssetReport(this);
	}

	// ------------------------------------------------------------------ nhịp khung hình
	// Timer của WinForms mặc định chỉ chính xác ~15,6 ms (kẹt ở ~62 FPS). timeBeginPeriod(1) hạ độ phân giải đồng hồ hệ thống xuống 1 ms,
	// rồi timer 1 ms + tự canh nhịp theo tần số quét màn hình (60-144 Hz) cho khung hình đều và mượt trên màn hình tần số cao.
	[System.Runtime.InteropServices.DllImport("winmm.dll")] private static extern uint timeBeginPeriod(uint ms);
	[System.Runtime.InteropServices.DllImport("winmm.dll")] private static extern uint timeEndPeriod(uint ms);
	[System.Runtime.InteropServices.DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr hWnd);
	[System.Runtime.InteropServices.DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr hWnd, IntPtr hdc);
	[System.Runtime.InteropServices.DllImport("gdi32.dll")] private static extern int GetDeviceCaps(IntPtr hdc, int index);

	private double _frameSeconds = 1.0 / 60.0;
	private bool _highResTimer;

	private static int DisplayRefreshRate()
	{
		try
		{
			IntPtr dc = GetDC(IntPtr.Zero);
			int hz = GetDeviceCaps(dc, 116); // VREFRESH
			ReleaseDC(IntPtr.Zero, dc);
			return hz >= 30 && hz <= 360 ? hz : 60;
		}
		catch (Exception) { return 60; }
	}

	// ------------------------------------------------------------------ vòng lặp
	private void gameTimer_Tick(object sender, EventArgs e)	{
		long now = _clock.ElapsedTicks;
		double elapsed = (now - _lastTicks) / (double)Stopwatch.Frequency;
		if (elapsed < _frameSeconds - 0.0006) return; // chưa tới nhịp khung hình kế tiếp (timer 1 ms, tự điều tiết theo tần số quét màn hình)
		float dt = (float)elapsed;
		_lastTicks = now;
		dt = Math.Min(dt, 0.05f); // lag (kéo cửa sổ...) thì không để game nhảy vọt

		if (_dialogOpen || _world.IsEnded) return;

		// TimeScale (cheat debug): chia nhỏ thành nhiều bước ngắn để mô phỏng không bị "nhảy vọt" khi chạy nhanh
		float scaled = dt * _world.TimeScale;
		int steps = Math.Max(1, (int)MathF.Ceiling(scaled / 0.025f));
		for (int i = 0; i < steps && !_world.IsEnded; i++)
			_world.Update(scaled / steps);
		canvas.Invalidate();
		canvas.Update(); // vẽ ngay thay vì chờ

		if (_world.PendingLevelUps > 0 && !_world.IsEnded) ShowUpgrades();
	}

	// ------------------------------------------------------------------ toàn màn hình (Cài đặt)
	private bool _isFullscreen;
	private Rectangle _restoreBounds;

	/// <summary>Áp dụng Settings.Fullscreen: toàn màn hình = bỏ viền và phủ kín màn hình hiện tại; tắt = về cửa sổ thường như cũ.</summary>
	private void ApplyDisplayMode()
	{
		if (Settings.Fullscreen == _isFullscreen) return;
		if (Settings.Fullscreen)
		{
			_restoreBounds = Bounds;
			WindowState = FormWindowState.Normal;
			FormBorderStyle = FormBorderStyle.None;
			Bounds = Screen.FromControl(this).Bounds;
		}
		else
		{
			FormBorderStyle = FormBorderStyle.Sizable;
			Bounds = _restoreBounds;
		}
		_isFullscreen = Settings.Fullscreen;
	}

	protected override void OnShown(EventArgs e)
	{
		base.OnShown(e);
		ApplyDisplayMode();
		_lastTicks = _clock.ElapsedTicks;
		try { _highResTimer = timeBeginPeriod(1) == 0; } catch (Exception) { }
		_frameSeconds = _highResTimer ? 1.0 / Math.Clamp(DisplayRefreshRate(), 60, 144) : 1.0 / 60.0;
		gameTimer.Interval = _highResTimer ? 1 : 8;
		gameTimer.Start();
	}

	// ------------------------------------------------------------------ phím
	private void GameForm_KeyDown(object sender, KeyEventArgs e)
	{
		if (e.KeyCode == Keys.Escape || e.KeyCode == Keys.P)
		{
			if (e.KeyCode == Keys.Escape || !e.Alt) ShowPause();
			e.Handled = true;
			return;
		}
		if (Cheats.Enabled && (e.KeyCode == Keys.F1 || e.KeyCode == Keys.Oemtilde))
		{
			ShowCheat(); // console cheat (chỉ bản Debug)
			e.Handled = true;
			return;
		}
		if (e.KeyCode == Keys.F)
		{
			// Bật/tắt tự động ngắm (tắt: vũ khí vẫn tự bắn nhưng ngắm theo con trỏ chuột)
			Settings.AutoAim = !Settings.AutoAim;
			Settings.Save();
			e.Handled = true;
			return;
		}
		if (e.KeyCode == Keys.M)
		{
			SoundService.Toggle();
			e.Handled = true;
			return;
		}
		_world.Input.KeyDown(e.KeyCode);
		if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Up || e.KeyCode == Keys.Down || e.KeyCode == Keys.Left || e.KeyCode == Keys.Right)
			e.Handled = true; // tránh phím mũi tên/Space bị control khác xử lý
	}

	private void GameForm_KeyUp(object sender, KeyEventArgs e) => _world.Input.KeyUp(e.KeyCode);

	private void GameForm_Deactivate(object sender, EventArgs e) => _world.Input.Clear(); // mất tiêu điểm: nhả hết phím để mèo không tự chạy

	protected override bool ProcessDialogKey(Keys keyData)
	{
		// Để phím mũi tên đến KeyDown của form thay vì di chuyển tiêu điểm giữa các control
		Keys k = keyData & Keys.KeyCode;
		if (k == Keys.Up || k == Keys.Down || k == Keys.Left || k == Keys.Right) return false;
		return base.ProcessDialogKey(keyData);
	}

	// ------------------------------------------------------------------ hộp thoại
	/// <summary>Mở một hộp thoại modal và dừng game trong lúc đó.</summary>
	private DialogResult ShowModal(Form dialog)
	{
		_dialogOpen = true;
		_world.Input.Clear();
		try { return dialog.ShowDialog(this); }
		finally
		{
			_dialogOpen = false;
			_lastTicks = _clock.ElapsedTicks; // không tính thời gian dừng vào dt
			dialog.Dispose();
		}
	}

	private void ShowPause()
	{
		if (_dialogOpen || _world.IsEnded || _world.Player.Dead) return;
		var pause = new PauseForm();
		ShowModal(pause);
		ApplyDisplayMode(); // có thể vừa đổi toàn màn hình trong Cài đặt
		switch (pause.Action)
		{
			case PauseAction.Restart: Outcome = GameOutcome.Restart; Close(); break;
			case PauseAction.Menu: Outcome = GameOutcome.Menu; Close(); break;
			case PauseAction.Quit: Outcome = GameOutcome.Quit; Close(); break;
		}
	}

	private void ShowCheat()
	{
		if (_dialogOpen || _world.IsEnded) return;
		ShowModal(new CheatForm(_world));
	}

	private void ShowUpgrades()
	{
		// Nhặt nhiều XP một lúc có thể lên nhiều cấp: lần lượt chọn từng phần thưởng
		while (_world.PendingLevelUps > 0)
		{
			var choose = new UpgradeForm(Upgrades.BuildChoices(_world.Player), _world.State.Level);
			ShowModal(choose);
			choose.Chosen?.Apply();
			_world.PendingLevelUps--;
		}
	}

	private void OnEnded(bool victory)
	{
		gameTimer.Stop();
		canvas.Invalidate();
		canvas.Update(); // khung hình cuối (mèo ngã / boss đổ)

		var over = new GameOverForm(_world.State, victory);
		ShowModal(over);
		Outcome = over.Outcome;
		Close();
	}

	protected override void OnFormClosed(FormClosedEventArgs e)
	{
		gameTimer.Stop();
		if (_highResTimer) { try { timeEndPeriod(1); } catch (Exception) { } _highResTimer = false; }
		base.OnFormClosed(e);
	}
}
