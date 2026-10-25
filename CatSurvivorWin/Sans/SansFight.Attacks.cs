using System.Numerics;

namespace CatSurvivor;

// Đòn đánh: xương (BoneH/BoneV), xương đâm (BoneStab), bục (Platform), Gaster Blaster, xương ở menu, và vùng chiến đấu (CombatZone) co giãn.
// Màu xương: 0 trắng (luôn gây sát thương), 1 XANH (chỉ gây sát thương nếu trái tim đang di chuyển), 2 CAM (chỉ khi đứng yên).
internal sealed partial class SansFight
{
	// ================================================================== vùng chiến đấu
	// Dùng double như Construct 2 (JavaScript) để việc co giãn tới đúng đích không bị sai số.
	private double _zx = 32, _zy = 240, _zw = 576, _zh = 144;
	private double _tL = 33, _tT = 251, _tR = 608, _tB = 391;
	private float _resizeSpeed = 480f;
	private string _endResize = "";
	private bool _zoneVisible;

	private void InitZone() { }

	private void ZoneResize(float l, float t, float r, float b, string endFunc)
	{
		_tL = l; _tT = t; _tR = r; _tB = b;
		_endResize = endFunc ?? "";
		_zoneVisible = true;
	}

	private void ZoneResizeInstant(float l, float t, float r, float b)
	{
		_tL = l; _tT = t; _tR = r; _tB = b;
		_zx = l; _zy = t; _zw = r - l; _zh = b - t;
		ZoneTick(0f);
		_zoneVisible = true;
	}

	private void ZoneTick(float dt)
	{
		double step = _resizeSpeed * dt;
		double x1 = Math.Min(step, Math.Abs(_zx - _tL));
		double y1 = Math.Min(step, Math.Abs(_zy - _tT));
		double x2 = Math.Min(step, Math.Abs(_zx + _zw - _tR));
		double y2 = Math.Min(step, Math.Abs(_zy + _zh - _tB));

		if (_zx > _tL) { _zx -= x1; _zw += x1; }
		else if (_zx < _tL) { _zx += x1; _zw -= x1; }
		if (_zy > _tT) { _zy -= y1; _zh += y1; }
		else if (_zy < _tT) { _zy += y1; _zh -= y1; }
		if (_zx + _zw > _tR) _zw -= x2;
		else if (_zx + _zw < _tR) _zw += x2;
		if (_zy + _zh > _tB) _zh -= y2;
		else if (_zy + _zh < _tB) _zh += y2;

		const double eps = 1e-6;
		if (_endResize.Length > 0 && Math.Abs(_zx - _tL) < eps && Math.Abs(_zy - _tT) < eps && Math.Abs(_zx + _zw - _tR) < eps && Math.Abs(_zy + _zh - _tB) < eps)
		{
			string f = _endResize;
			_endResize = "";
			Call(f);
		}

		// Vùng thu nhỏ đè lên trái tim: đẩy trái tim vào trong (cách viền 5 px)
		float zl = (float)_zx, zt = (float)_zy, zr = (float)(_zx + _zw), zb = (float)(_zy + _zh);
		if (Overlaps(_hx - 8f, _hy - 8f, _hx + 8f, _hy + 8f, new RectangleF(zl, zt, zr - zl, zb - zt)))
		{
			if (zl + 5f > _hx - 8f) _hx = zl + 5f + 8f;
			if (zt + 5f > _hy - 8f) _hy = zt + 5f + 8f;
			if (zr - 5f < _hx + 8f) _hx = zr - 5f - 8f;
			if (zb - 5f < _hy + 8f) _hy = zb - 5f - 8f;
		}
	}

	private float ZL => (float)_zx;
	private float ZT => (float)_zy;
	private float ZR => (float)(_zx + _zw);
	private float ZB => (float)(_zy + _zh);

	// ================================================================== xương
	/// <summary>Vật gây sát thương dạng hình chữ nhật (xương, xương đâm).</summary>
	private class Hazard
	{
		public float X, Y, W, H;
		public int Color;
		public float Damage = 1f, Karma = 6f;
		public RectangleF Rect => new(X, Y, W, H);
	}

	private sealed class Bone : Hazard
	{
		public bool Vertical;
		public int Dir;
		public float Speed;
	}

	private readonly List<Bone> _bones = new();

	private void AddBone(bool vertical, int x, int y, int len, int dir, int speed, int color)
	{
		_bones.Add(new Bone
		{
			Vertical = vertical, X = x, Y = y, Dir = dir, Speed = speed, Color = color,
			W = vertical ? 10f : len, H = vertical ? len : 10f,
		});
	}

	/// <summary>BoneHRepeat/BoneVRepeat: nhiều xương cách đều nhau (lùi dần theo hướng ngược với hướng bay).</summary>
	private void BoneRepeat(bool vertical, string[] a)
	{
		float sx = F(a[0]), sy = F(a[1]), len = F(a[2]), speed = F(a[4]);
		int dir = I(a[3]), count = I(a[5]), spacing = I(a[6]);
		var d = DirVec(dir);
		for (int i = 0; i < count; i++)
			AddBone(vertical, (int)(sx - d.X * spacing * i), (int)(sy - d.Y * spacing * i), (int)len, dir, (int)speed, 0);
	}

	/// <summary>Cột xương có khe hở dạng sóng sin để chui qua (spacing dương: từ phải sang trái, âm: từ trái sang phải).</summary>
	private void SineBones(int count, int spacing, int speed, int height)
	{
		for (int i = 0; i < count; i++)
		{
			float x = 0f; int dir = 0;
			if (spacing > 0) { x = ZR + spacing * i; dir = 2; }
			if (spacing < 0) { x = ZL + spacing * i; dir = 0; }
			int sine = (int)MathF.Floor(MathF.Sin(i / 3f) * 28f);
			float y = ZT + 6f;
			AddBone(true, (int)x, (int)y, height + sine, dir, speed, 0);
			y = ZT + 6f + height + sine + 39f;
			AddBone(true, (int)x, (int)y, (int)(ZB - 5f - y), dir, speed, 0);
		}
	}

	private void UpdateBones(float dt)
	{
		foreach (var b in _bones)
		{
			var d = DirVec(b.Dir);
			b.X += d.X * dt * b.Speed;
			b.Y += d.Y * dt * b.Speed;
		}
		_bones.RemoveAll(b =>
			(b.Dir == 0 && b.X > W) || (b.Dir == 1 && b.Y > H) || (b.Dir == 2 && b.X < -b.W) || (b.Dir == 3 && b.Y < -b.H));
	}

	// ================================================================== xương đâm từ mép vùng chiến đấu
	private sealed class StabWarn
	{
		public int Dir;
		public float Dist, Warn, Stay;
		public RectangleF Rect;
	}

	private sealed class Stab : Hazard
	{
		public int Dir;
		public float Dist, Stay, DestX, DestY;
		public bool Reverse, Vertical;
	}

	private readonly List<StabWarn> _warns = new();
	private readonly List<Stab> _stabs = new();

	/// <summary>Cảnh báo (khung đỏ) warnTime giây rồi một bức tường xương nhô ra từ mép dir (0 phải, 1 dưới, 2 trái, 3 trên) sâu `dist` px, nằm lại stayTime giây rồi rút về.</summary>
	private void BoneStab(int dir, int dist, float warnTime, float stayTime)
	{
		if (dir < 0 || dir > 3) return;
		SoundService.PlayExtra("Warning");
		float w, h, x, y;
		switch (dir)
		{
			case 0: w = dist - 3; h = (float)_zh - 16f; x = ZR - w - 8f; y = ZT + 8f; break;
			case 1: w = (float)_zw - 16f; h = dist - 3; x = ZL + 8f; y = ZB - h - 8f; break;
			case 2: w = dist - 3; h = (float)_zh - 16f; x = ZL + 8f; y = ZT + 8f; break;
			default: w = (float)_zw - 16f; h = dist - 3; x = ZL + 8f; y = ZT + 8f; break;
		}
		_warns.Add(new StabWarn { Dir = dir, Dist = dist, Warn = warnTime, Stay = stayTime, Rect = new RectangleF(x, y, w, h) });
	}

	private void UpdateStabs(float dt)
	{
		// Hết thời gian cảnh báo: phát tiếng, thay khung đỏ bằng bức tường xương
		for (int i = _warns.Count - 1; i >= 0; i--)
		{
			var wn = _warns[i];
			if (wn.Warn != 0f) continue;
			SoundService.PlayExtra("BoneStab");
			_warns.RemoveAt(i);
			var s = new Stab { Dir = wn.Dir, Dist = wn.Dist, Stay = wn.Stay };
			if (wn.Dir == 1 || wn.Dir == 3)
			{
				s.Vertical = true;
				s.X = ZL; s.W = (float)_zw; s.H = wn.Dist + 8f; s.DestX = s.X;
				if (wn.Dir == 1) { s.Y = ZB - 5f; s.DestY = ZB - 5f - wn.Dist; }
				else { s.Y = ZT + 5f - s.H; s.DestY = ZT + 5f - s.H + wn.Dist; }
			}
			else
			{
				s.Y = ZT; s.H = (float)_zh; s.W = wn.Dist + 8f; s.DestY = s.Y;
				if (wn.Dir == 0) { s.X = ZR - 5f; s.DestX = ZR - 5f - wn.Dist; }
				else { s.X = ZL + 5f - s.W; s.DestX = ZL + 5f - s.W + wn.Dist; }
			}
			_stabs.Add(s);
		}
		foreach (var wn in _warns)
			if (wn.Warn > 0f) wn.Warn -= MathF.Min(dt, wn.Warn);

		foreach (var s in _stabs)
		{
			float speed = s.Dist * 10f;
			var d = DirVec(s.Dir);
			if (s.Reverse)
			{
				s.X += d.X * dt * speed;
				s.Y += d.Y * dt * speed;
			}
			else
			{
				s.X -= d.X * dt * speed;
				s.Y -= d.Y * dt * speed;
				// Vượt quá đích (đích nằm về phía hướng dir so với vị trí hiện tại) thì dừng đúng đích
				var to = new Vector2(s.DestX - s.X, s.DestY - s.Y);
				if (to == Vector2.Zero ? s.Dir == 0 : Vector2.Dot(Vector2.Normalize(to), d) > 0.99996f)
				{
					s.X = s.DestX;
					s.Y = s.DestY;
				}
				if (s.X == s.DestX && s.Y == s.DestY)
				{
					s.Stay -= MathF.Min(dt, s.Stay);
					if (s.Stay == 0f) s.Reverse = true;
				}
			}
		}
		_stabs.RemoveAll(s => s.X + s.W < 0f || s.X > W || s.Y + s.H < 0f || s.Y > H);
	}

	// ================================================================== bục (xanh: đứng lên được)
	private sealed class SansPlatform
	{
		public float X, Y, W;
		public int Dir;
		public float Speed;
		public bool Reverse;
		public float H => 7f;
		public float Dx => DirVec(Dir).X * Speed;
		public float Dy => DirVec(Dir).Y * Speed;
		public RectangleF Rect => new(X, Y, W, H);
	}

	private readonly List<SansPlatform> _platforms = new();

	private void AddPlatform(int x, int y, int w, int dir, int speed, bool reverse) =>
		_platforms.Add(new SansPlatform { X = x, Y = y, W = w, Dir = dir, Speed = speed, Reverse = reverse });

	private void PlatformRepeat(string[] a)
	{
		float sx = F(a[0]), sy = F(a[1]);
		int w = I(a[2]), dir = I(a[3]), speed = I(a[4]), count = I(a[5]), spacing = I(a[6]);
		var d = DirVec(dir);
		for (int i = 0; i < count; i++)
			AddPlatform((int)(sx - d.X * spacing * i), (int)(sy - d.Y * spacing * i), w, dir, speed, false);
	}

	private void UpdatePlatforms()
	{
		foreach (var p in _platforms)
		{
			if (!p.Reverse) continue;
			// Bục "quay đầu" khi chạm mép vùng chiến đấu
			if (p.Dir == 0 && p.X + p.W >= ZR) p.Dir = 2;
			if (p.Dir == 1 && p.Y + p.H >= ZB) p.Dir = 3;
			if (p.Dir == 2 && p.X <= ZL) p.Dir = 0;
			if (p.Dir == 3 && p.Y <= ZT) p.Dir = 1;
		}
	}

	// ================================================================== Gaster Blaster
	private sealed class Blaster
	{
		public int State;                 // 0 bay vào, 1 chờ, 2 nạp (0.1s), 3 bắn + rút lui
		public float X, Y, EndX, EndY, Ang, EndAng, Timer, LeaveSpeed, BlastTime;
		public float W, H;                // kích thước sprite (57x44 nhân theo cỡ)
		public bool Firing;               // dùng hoạt ảnh "Fire"
		public float AnimT;
		public bool BeamVisible;
		public float BlastTimer, BaseSize, SineSize, Opacity = 100f;
		public float HitDamage, HitKarma;
		public const float ImgW = 57f, ImgH = 44f;
		public float Scale => H / ImgH / 2f;
	}

	private readonly List<Blaster> _blasters = new();

	/// <summary>size 0/1/2 = cỡ x2 (chỉ ngang)/x2/x3. (sx,sy) vị trí bắt đầu (ngoài màn hình), (ex,ey) + endAng (độ) vị trí và hướng bắn, spin: chờ trước khi bắn, blast: thời gian chùm tia.</summary>
	private void GasterBlaster(int size, int sx, int sy, int ex, int ey, int endAng, float spin, float blast)
	{
		var b = new Blaster { X = sx, Y = sy, EndX = ex, EndY = ey, EndAng = endAng, Ang = 90f, Timer = spin, BlastTime = blast, W = Blaster.ImgW, H = Blaster.ImgH };
		SoundService.StopExtra("GasterBlaster");
		SoundService.PlayExtra("GasterBlaster", 1.2f, "GasterBlaster");
		if (sx == ex && sy == ey) b.Ang = endAng;
		if (size == 0) b.W = Blaster.ImgW * 2f;
		else if (size == 1) { b.W = Blaster.ImgW * 2f; b.H = Blaster.ImgH * 2f; }
		else if (size == 2) { b.W = Blaster.ImgW * 3f; b.H = Blaster.ImgH * 3f; }
		_blasters.Add(b);
	}

	private void UpdateBlasters(float dt)
	{
		for (int i = _blasters.Count - 1; i >= 0; i--)
		{
			var b = _blasters[i];
			b.AnimT += dt;
			if (b.Timer > 0f && (b.State == 1 || b.State == 2)) b.Timer -= MathF.Min(dt, b.Timer);

			if (b.State == 0)
			{
				if (MathF.Abs(b.X - b.EndX) >= 3f) b.X += (b.EndX - b.X) * dt * 10f; else b.X = b.EndX;
				if (MathF.Abs(b.Y - b.EndY) >= 3f) b.Y += (b.EndY - b.Y) * dt * 10f; else b.Y = b.EndY;
				if (MathF.Abs(b.Ang - b.EndAng) >= 3f) b.Ang += (b.EndAng - b.Ang) * dt * 10f; else b.Ang = b.EndAng;
				if (b.X == b.EndX && b.Y == b.EndY && b.Ang == b.EndAng) b.State = 1;
			}
			if (b.State == 1 && b.Timer == 0f)
			{
				b.Firing = true;
				b.AnimT = 0f;
				b.State = 2;
				b.Timer = 0.1f;
			}
			if (b.State == 2 && b.Timer == 0f)
			{
				b.State = 3;
				b.BeamVisible = true;
				b.HitDamage = 1f;
				b.HitKarma = 10f;
				SoundService.StopExtra("GasterBlast");
				SoundService.StopExtra("GasterBlast2");
				SoundService.PlayExtra("GasterBlast", 1.2f, "GasterBlast");
				SoundService.PlayExtra("GasterBlast2", 1.2f, "GasterBlast2");
				if (b.H > Blaster.ImgH) Shake(5);
			}
			if (b.State == 3)
			{
				b.LeaveSpeed += 30f * dt * 60f;
				float m = MathF.Max(b.W, b.H);
				if (b.X < -m || b.X > W + m || b.Y < -m || b.Y > H + m) b.LeaveSpeed = 0f;
				b.X -= CosDeg(b.Ang) * dt * b.LeaveSpeed;
				b.Y -= SinDeg(b.Ang) * dt * b.LeaveSpeed;
			}

			// Chùm tia: dày dần trong 4/30 s tới 35 px x cỡ, giữ BlastTime giây, rồi mỏng đi và mờ dần
			if (b.BeamVisible)
			{
				b.BlastTimer += dt;
				float full = 35f * b.H / Blaster.ImgH;
				if (b.BlastTimer < 4f / 30f) b.BaseSize += MathF.Floor(full / 4f) * dt * 30f;
				if (b.BlastTimer >= 4f / 30f && b.BlastTimer < 4f / 30f + dt) b.BaseSize = full;
				if (b.BlastTimer > 5f / 30f + b.BlastTime)
				{
					b.BaseSize *= MathF.Pow(0.8f, dt * 30f);
					b.Opacity = 100f - ((b.BlastTimer - b.BlastTime) * 30f - 5f) * 10f;
					if (b.BaseSize < 2f) { _blasters.RemoveAt(i); continue; }
				}
				if (b.Opacity <= 80f) b.HitDamage = 0f;
				b.SineSize = SinDeg(b.BlastTimer * 30f / 1.5f * 180f / MathF.PI) * b.BaseSize / 4f;
			}
		}
	}

	/// <summary>Hình chữ nhật xoay của vùng sát thương chùm tia (gốc, hướng, dài, rộng).</summary>
	private static (Vector2 origin, float ang, float len, float height) BeamHit(Blaster b)
	{
		float sc = b.Scale;
		var origin = new Vector2(b.X + CosDeg(b.Ang) * 70f * sc, b.Y + SinDeg(b.Ang) * 70f * sc);
		return (origin, b.Ang, 1000f, b.BaseSize * 3f / 4f);
	}

	// ================================================================== xương ở menu (chỉ từ lượt 14 trở đi)
	private sealed class MenuBone
	{
		public bool Bottom, Destroying;
		public float X, Y, Timer, Damage = 1f, Karma = 0f;
		public int State, Button;
		public RectangleF Rect => new(X + 2f, Y + 2f, 10f, 40f);   // vùng va chạm 10x40 trong ảnh 14x44
	}

	private readonly List<MenuBone> _menuBones = new();
	private bool _bottomBones;
	private float _bottomTimer;
	private int _bottomAlt;

	private void MenuBoneLeft() => _menuBones.Add(new MenuBone { X = -10f, Y = 270f });
	private void MenuBonesOff() { foreach (var b in _menuBones) if (!b.Bottom) b.Destroying = true; _bottomBones = false; }
	private void StartBottomBones() { _bottomBones = true; _bottomTimer = 0f; _bottomAlt = 0; }

	private void UpdateMenuBones(float dt)
	{
		foreach (var b in _menuBones)
		{
			if (b.Bottom) continue;
			b.Timer += dt;
			b.X = -30f + MathF.Abs(MathF.Sin(b.Timer * 10f / 3f)) * 105f;   // đưa qua đưa lại sát mép trái khung chữ
			if (b.X > 64f) b.Timer -= dt * 0.72f;                              // dừng lâu hơn ở xa nhất
		}
		_menuBones.RemoveAll(b => !b.Bottom && b.Destroying && b.X <= -8f);

		if (_bottomBones)
		{
			_bottomTimer += dt;
			if (_bottomTimer >= 0.6f)
			{
				_bottomTimer -= 0.6f;
				// Hai xương trồi lên từ đáy màn hình, mỗi xương chạy dọc một nút để quét trái tim nếu nó đang ở nút đó
				_menuBones.Add(new MenuBone { Bottom = true, Button = 0 + _bottomAlt, X = ButtonX(0 + _bottomAlt) + 110f, Y = H });
				_menuBones.Add(new MenuBone { Bottom = true, Button = 2 + _bottomAlt, X = ButtonX(2 + _bottomAlt) + 110f, Y = H });
				_bottomAlt = _bottomAlt == 0 ? 1 : 0;
			}
		}
		foreach (var b in _menuBones)
		{
			if (!b.Bottom) continue;
			if (b.State == 0)
			{
				b.Y -= 300f * dt;
				if (b.Y <= 440f) { b.Y = 440f; b.State = 1; }
			}
			if (b.State == 1)
			{
				b.X -= 150f * dt;
				float stop = ButtonX(b.Button) - 14f;
				if (b.X <= stop) { b.X = stop; b.State = 2; }
			}
			if (b.State == 2) b.Y += 300f * dt;
		}
		_menuBones.RemoveAll(b => b.Bottom && b.Y > H);
	}

	// ================================================================== trúng đòn
	private void CheckMenuBoneHits(RectangleF hit)
	{
		if (!_hVisible) return;
		var picked = _menuBones.Where(b => b.Damage > 0f && b.Rect.IntersectsWith(hit)).ToList();
		if (picked.Count == 0) return;
		DamagePlayer(picked[0].Damage, picked[0].Karma);
		foreach (var b in picked) if (b.Karma >= 3f) b.Karma = 2f;
		if (Hp <= 0f) Hp = 1f;      // xương ở menu không giết được
	}

	private void CheckBlastHits(RectangleF hit)
	{
		foreach (var b in _blasters)
		{
			if (!b.BeamVisible || b.HitDamage <= 0f) continue;
			var (o, ang, len, h) = BeamHit(b);
			// Đưa tâm hitbox vào toạ độ cục bộ của chùm tia rồi kiểm tra hình chữ nhật
			var rel = new Vector2(hit.X + hit.Width / 2f - o.X, hit.Y + hit.Height / 2f - o.Y);
			float lx = rel.X * CosDeg(ang) + rel.Y * SinDeg(ang);
			float ly = -rel.X * SinDeg(ang) + rel.Y * CosDeg(ang);
			if (lx + 2f > 0f && lx - 2f < len && MathF.Abs(ly) < h / 2f + 2f)
			{
				DamagePlayer(b.HitDamage, b.HitKarma);
				if (b.HitKarma >= 3f) b.HitKarma = 2f;
			}
		}
	}

	private void CheckBoneHits(RectangleF hit)
	{
		bool moving = _hdx != 0f || _hdy != 0f;
		var all = new List<Hazard>(_bones.Count + _stabs.Count);
		all.AddRange(_bones);
		all.AddRange(_stabs);
		for (int color = 0; color < 3; color++)
		{
			if (color == 1 && !moving) continue;    // xương xanh: chỉ đau khi đang di chuyển
			if (color == 2 && moving) continue;     // xương cam: chỉ đau khi đứng yên
			var picked = all.Where(h => h.Color == color && h.Damage > 0f && h.Rect.IntersectsWith(hit)).ToList();
			if (picked.Count == 0) continue;
			DamagePlayer(picked[0].Damage, picked[0].Karma);
			foreach (var h in picked) if (h.Karma >= 3f) h.Karma = 2f;
		}
	}

	/// <summary>Xoá mọi vật tấn công (xương, xương đâm, bục, Gaster Blaster, xương menu).</summary>
	private void ClearAttackObjects()
	{
		_bones.Clear();
		_stabs.Clear();
		_warns.Clear();
		_platforms.Clear();
		_blasters.Clear();
		_menuBones.Clear();
	}
}
