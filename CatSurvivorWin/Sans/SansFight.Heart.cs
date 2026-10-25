using System.Numerics;

namespace CatSurvivor;

// Trái tim của người chơi (PlayerHeart): chế độ ĐỎ (đi tự do bằng mũi tên) và XANH (trọng lực theo hướng góc, nhảy, đập xuống "slam").
// Vận tốc (_hdx, _hdy) do HeartControl đặt mỗi khung hình, rồi StepBehaviors di chuyển từng bước <= 1 px và dừng khi chạm vật rắn (viền vùng chiến đấu / bục một chiều).
internal sealed partial class SansFight
{
	private const float HeartJumpStrength = 180f;
	private const float HeartJumpHoldCutoff = 30f;

	private float _hx = 320f, _hy = 320f, _hdx, _hdy;
	private float _hAngle = 90f;        // độ: 90 = trọng lực hướng xuống; trái tim hướng về phía trọng lực
	private int _hMode;                 // 0 đỏ, 1 xanh, -1 đã chết
	private bool _hVisible, _hEnabled = true;
	private bool _slammed, _slamDamage;
	private float _maxFall = 750f;
	private Rgba _hTint = new(1f, 0f, 0f, 1f);

	private void InitHeart() => HeartMode(0);

	private void HeartMode(int mode)
	{
		if (mode == 0) { _hMode = 0; _hAngle = 90f; _hTint = new Rgba(1f, 0f, 0f, 1f); }
		else if (mode == 1) { _hMode = 1; _hAngle = 90f; _hTint = new Rgba(0f, 0.2353f, 1f, 1f); }
	}

	/// <summary>Hất trái tim về một phía (0 phải, 1 xuống, 2 trái, 3 lên) với tốc độ tối đa: chuyển sang chế độ xanh, đập vào khung thì rung màn hình + trừ 1 máu (nếu bật SansSlamDamage).</summary>
	private void SansSlam(float dir)
	{
		if (dir < 0f || dir > 3f) return;
		HeartMode(1);
		_slammed = true;
		_hAngle = MathF.Floor(dir) * 90f;
		var v = DirVec((int)MathF.Floor(dir));
		_hdx = v.X * _maxFall;
		_hdy = v.Y * _maxFall;
	}

	private bool AngleIs(float a)
	{
		float d = ((_hAngle - a) % 360f + 540f) % 360f - 180f;
		return MathF.Abs(d) <= 0.5f;
	}

	// ---------------------------------------------------------------- va chạm với vật rắn
	/// <summary>Trái tim (16x16) dịch (ox, oy) có chạm vật rắn không: viền vùng chiến đấu, hoặc bục (chỉ chặn khi đang rơi/đứng lên bục theo hướng trọng lực dọc).</summary>
	private bool HeartSolid(float ox, float oy)
	{
		float l = _hx + ox - 8f, t = _hy + oy - 8f, r = l + 16f, b = t + 16f;
		foreach (var rc in BorderRects())
			if (Overlaps(l, t, r, b, rc)) return true;
		foreach (var p in _platforms)
		{
			if (!Overlaps(l, t, r, b, p.Rect)) continue;
			if (AngleIs(90f) && p.Y > _hy && _hdy >= p.Dy && b <= p.Y + 2f) return true;
			if (AngleIs(270f) && p.Y < _hy && _hdy <= p.Dy && t >= p.Y + p.H - 2f) return true;
		}
		return false;
	}

	private static bool Overlaps(float l, float t, float r, float b, RectangleF rc) =>
		l < rc.Right && r > rc.Left && t < rc.Bottom && b > rc.Top;

	/// <summary>4 viền rắn của vùng chiến đấu, dày 5 px.</summary>
	private IEnumerable<RectangleF> BorderRects()
	{
		float l = ZL, t = ZT, w = ZR - ZL, h = ZB - ZT;
		yield return new RectangleF(l, t, w, 5f);
		yield return new RectangleF(l, t, 5f, h);
		yield return new RectangleF(l, t + h - 5f, w, 5f);
		yield return new RectangleF(l + w - 5f, t, 5f, h);
	}

	// ---------------------------------------------------------------- di chuyển theo vận tốc
	private void StepBehaviors(float dt)
	{
		foreach (var p in _platforms)
		{
			p.X += p.Dx * dt;
			p.Y += p.Dy * dt;
		}
		if (_hEnabled && _hMode >= 0)
		{
			StepAxis(true, _hdx * dt);
			StepAxis(false, _hdy * dt);
		}
	}

	private void StepAxis(bool horizontal, float dist)
	{
		int n = (int)MathF.Ceiling(MathF.Abs(dist));
		if (n == 0) return;
		float step = dist / n;
		for (int i = 0; i < n; i++)
		{
			if (horizontal) _hx += step; else _hy += step;
			if (!HeartSolid(0f, 0f)) continue;

			// Va vào vật rắn: nếu đang bị hất (slam) đủ mạnh thì nghe/rung/trừ máu
			float speed = horizontal ? _hdx : _hdy;
			if (_slammed)
			{
				_slammed = false;
				if (MathF.Abs(speed) >= 330f)
				{
					SoundService.PlayExtra("PlayerDamaged");
					SoundService.PlayExtra("Slam");
					Shake((int)MathF.Floor(MathF.Abs(speed) / 30f / 3f));
					if (_slamDamage && Hp > 1f) Hp -= 1f;
				}
			}
			if (speed != 0f)
			{
				if (horizontal) { _hdx = 0f; _hx -= step; } else { _hdy = 0f; _hy -= step; } // dừng và lùi về vị trí trước bước này
			}
			break;
		}
	}

	// ---------------------------------------------------------------- điều khiển
	private void HeartJump()
	{
		if (_hMode != 1) return;
		var g = new Vector2(CosDeg(_hAngle), SinDeg(_hAngle));
		g = new Vector2(MathF.Round(g.X), MathF.Round(g.Y));
		if (HeartSolid(g.X, g.Y))
		{
			_hdx -= g.X * HeartJumpStrength;
			_hdy -= g.Y * HeartJumpStrength;
		}
	}

	private void HeartControl(float dt)
	{
		float speed = Pad.Down(SansBtn.Cancel) ? 75f : 150f;   // giữ X/Shift: đi chậm
		bool up = Pad.Down(SansBtn.Up), down = Pad.Down(SansBtn.Down), left = Pad.Down(SansBtn.Left), right = Pad.Down(SansBtn.Right);

		if (_hMode == 0)
		{
			if (up == down) _hdy = 0f;
			else { if (up) _hdy = -speed; if (down) _hdy = speed; }
			if (left == right) _hdx = 0f;
			else { if (left) _hdx = -speed; if (right) _hdx = speed; }
			return;
		}
		if (_hMode != 1) return;

		// ---- chế độ XANH ----
		float downSpeed = 0f, gravity = 0f;
		if (AngleIs(0f))
		{
			if (Pad.Pressed(SansBtn.Left)) HeartJump();
			if (Pad.Released(SansBtn.Left) && _hdx < -HeartJumpHoldCutoff) _hdx = -HeartJumpHoldCutoff;   // nhả sớm: nhảy thấp
			downSpeed = _hdx;
		}
		else if (AngleIs(90f))
		{
			if (Pad.Pressed(SansBtn.Up)) HeartJump();
			if (Pad.Released(SansBtn.Up) && _hdy < -HeartJumpHoldCutoff) _hdy = -HeartJumpHoldCutoff;
			downSpeed = _hdy;
		}
		else if (AngleIs(180f))
		{
			if (Pad.Pressed(SansBtn.Right)) HeartJump();
			if (Pad.Released(SansBtn.Right) && _hdx > HeartJumpHoldCutoff) _hdx = HeartJumpHoldCutoff;
			downSpeed = -_hdx;
		}
		else if (AngleIs(270f))
		{
			if (Pad.Pressed(SansBtn.Down)) HeartJump();
			if (Pad.Released(SansBtn.Down) && _hdy > HeartJumpHoldCutoff) _hdy = HeartJumpHoldCutoff;
			downSpeed = -_hdy;
		}

		// Trọng lực theo tốc độ rơi: nhảy lên chậm dần rồi rơi nhanh dần tới trần
		if (downSpeed < 240f && downSpeed > 15f) gravity = 540f;
		if (downSpeed <= 15f && downSpeed > -30f) gravity = 180f;
		if (downSpeed <= -30f && downSpeed > -120f) gravity = 450f;
		if (downSpeed <= -120f) gravity = 180f;

		var gv = new Vector2(MathF.Round(CosDeg(_hAngle)), MathF.Round(SinDeg(_hAngle)));
		if (!HeartSolid(gv.X * 0.2f, gv.Y * 0.2f))
		{
			_hdx += gv.X * gravity * dt;
			_hdy += gv.Y * gravity * dt;
			if (AngleIs(0f) && _hdx > _maxFall) _hdx = _maxFall;
			if (AngleIs(90f) && _hdy > _maxFall) _hdy = _maxFall;
			if (AngleIs(180f) && _hdx < -_maxFall) _hdx = -_maxFall;
			if (AngleIs(270f) && _hdy < -_maxFall) _hdy = -_maxFall;
		}

		if (AngleIs(0f) || AngleIs(180f))
		{
			_hdy = 0f;                                  // trọng lực ngang: tự do lên/xuống
			if (up != down) { if (up) _hdy -= speed; if (down) _hdy += speed; }
		}
		if (AngleIs(90f) || AngleIs(270f))
		{
			_hdx = 0f;                                  // trọng lực dọc: tự do trái/phải
			RideMovingPlatform(gv);
			if (left != right) { if (left) _hdx -= speed; if (right) _hdx += speed; }
		}
	}

	/// <summary>Đứng trên bục đang di chuyển thì đi cùng bục (và dính sát mặt bục).</summary>
	private void RideMovingPlatform(Vector2 g)
	{
		float l = _hx + g.X * 0.5f - 8f, t = _hy + g.Y * 0.5f - 8f, r = l + 16f, b = t + 16f;
		foreach (var p in _platforms)
		{
			if (!Overlaps(l, t, r, b, p.Rect)) continue;
			if (AngleIs(90f) && p.Y > _hy && _hdy >= p.Dy && b <= p.Y + 2f)
			{
				_hdx = p.Dx; _hdy = p.Dy; _hy = p.Y - 8.05f;
			}
			if (AngleIs(270f) && p.Y < _hy && _hdy <= p.Dy && t >= p.Y + p.H - 2f)
			{
				_hdx = p.Dx; _hdy = p.Dy; _hy = p.Y + p.H + 8.05f;
			}
		}
	}
}
