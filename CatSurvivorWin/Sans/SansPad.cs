namespace CatSurvivor;

internal enum SansBtn { Up, Down, Left, Right, Confirm, Cancel, Menu }

/// <summary>
/// Tay cầm ảo của trận Sans (port từ "VPad" + InputManagement của repo gốc):
///   Lên/xuống/trái/phải = mũi tên hoặc WASD · Xác nhận = Z hoặc Enter · Huỷ (giữ để trái tim đi chậm) = X hoặc Shift · Menu = C hoặc Ctrl
/// Down: đang giữ. Pressed/Released: vừa bấm/vừa nhả kể từ lần EndTick trước (không bỏ sót cú bấm nhanh giữa hai khung hình).
/// </summary>
internal sealed class SansPad
{
	private readonly HashSet<Keys> _keys = new();
	private readonly bool[] _down = new bool[7], _pressed = new bool[7], _released = new bool[7];

	public bool Down(SansBtn b) => _down[(int)b];
	public bool Pressed(SansBtn b) => _pressed[(int)b];
	public bool Released(SansBtn b) => _released[(int)b];

	private static int Map(Keys k) => k switch
	{
		Keys.Up or Keys.W => (int)SansBtn.Up,
		Keys.Down or Keys.S => (int)SansBtn.Down,
		Keys.Left or Keys.A => (int)SansBtn.Left,
		Keys.Right or Keys.D => (int)SansBtn.Right,
		Keys.Z or Keys.Enter => (int)SansBtn.Confirm,
		Keys.X or Keys.ShiftKey or Keys.LShiftKey or Keys.RShiftKey => (int)SansBtn.Cancel,
		Keys.C or Keys.ControlKey or Keys.LControlKey or Keys.RControlKey => (int)SansBtn.Menu,
		_ => -1,
	};

	public void KeyDown(Keys code)
	{
		code &= Keys.KeyCode;
		int b = Map(code);
		if (b < 0 || !_keys.Add(code)) return;   // phím lặp (auto-repeat) bỏ qua
		if (!_down[b]) { _down[b] = true; _pressed[b] = true; }
	}

	public void KeyUp(Keys code)
	{
		code &= Keys.KeyCode;
		int b = Map(code);
		if (b < 0) return;
		_keys.Remove(code);
		if (_down[b] && !_keys.Any(k => Map(k) == b)) { _down[b] = false; _released[b] = true; }
	}

	/// <summary>Gọi cuối mỗi bước mô phỏng: xoá các cờ "vừa bấm/vừa nhả".</summary>
	public void EndTick()
	{
		Array.Clear(_pressed);
		Array.Clear(_released);
	}

	/// <summary>Nhả hết phím (mất tiêu điểm cửa sổ).</summary>
	public void Clear()
	{
		_keys.Clear();
		Array.Clear(_down);
		Array.Clear(_pressed);
		Array.Clear(_released);
	}

	/// <summary>Chỉ cho kiểm thử: giả lập bấm/nhả một nút.</summary>
	internal void Tap(SansBtn b, bool down)
	{
		int i = (int)b;
		if (down && !_down[i]) _pressed[i] = true;
		if (!down && _down[i]) _released[i] = true;
		_down[i] = down;
	}
}
