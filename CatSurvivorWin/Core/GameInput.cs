using System.Numerics;

namespace CatSurvivor;

/// <summary>
/// Trạng thái bàn phím/chuột của game. Form chuyển sự kiện KeyDown/KeyUp/MouseMove vào đây;
/// game đọc lại mỗi khung hình (di chuyển bằng WASD/phím mũi tên, dash bằng Space/Shift, ngắm bằng chuột).
/// </summary>
public sealed class GameInput
{
	private readonly HashSet<Keys> _down = new();
	private bool _dashQueued;
	private int _skillQueued;      // bit 0: kĩ năng Q, bit 1: kĩ năng E (mỗi lần nhấn xếp hàng một lần dùng)

	/// <summary>Vị trí chuột theo toạ độ màn hình logic (640x360).</summary>
	public Vector2 MouseView;

	public void KeyDown(Keys k)
	{
		// Lần nhấn đầu tiên (không tính tự lặp phím) của Space/Shift mới xếp hàng một cú dash
		if (!_down.Add(k)) return; // giữ phím (tự lặp) không tính là nhấn lại
		if (k == Keys.Space || k == Keys.ShiftKey || k == Keys.LShiftKey || k == Keys.RShiftKey) _dashQueued = true;
		else if (k == Keys.Q) _skillQueued |= 1;
		else if (k == Keys.E) _skillQueued |= 2;
	}

	public void KeyUp(Keys k) => _down.Remove(k);

	public void Clear()
	{
		_down.Clear();
		_dashQueued = false;
		_skillQueued = 0;
	}

	/// <summary>Có lần bấm kĩ năng `slot` (0 = Q, 1 = E) đang chờ không (đọc xong thì xoá).</summary>
	public bool ConsumeSkill(int slot)
	{
		int bit = 1 << slot;
		bool queued = (_skillQueued & bit) != 0;
		_skillQueued &= ~bit;
		return queued;
	}

	/// <summary>Có cú dash đang chờ không (đọc xong thì xoá).</summary>
	public bool ConsumeDash()
	{
		bool d = _dashQueued;
		_dashQueued = false;
		return d;
	}

	/// <summary>Hướng di chuyển (độ dài tối đa 1, đi chéo không nhanh hơn).</summary>
	public Vector2 Move()
	{
		float x = (Held(Keys.D, Keys.Right) ? 1f : 0f) - (Held(Keys.A, Keys.Left) ? 1f : 0f);
		float y = (Held(Keys.S, Keys.Down) ? 1f : 0f) - (Held(Keys.W, Keys.Up) ? 1f : 0f);
		return new Vector2(x, y).LimitLength(1f);
	}

	private bool Held(Keys a, Keys b) => _down.Contains(a) || _down.Contains(b);
}
