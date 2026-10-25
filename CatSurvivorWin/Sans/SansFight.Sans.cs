using System.Numerics;

namespace CatSurvivor;

// Thân Sans ghép từ 5 mảnh (chân, thân, thân-động-tác, đầu, mồ hôi) như bản gốc: mỗi mảnh có "điểm gắn" (image point) cho mảnh kế tiếp.
// Mọi sprite của Sans vẽ phóng 2x. Hoạt ảnh: Idle (nhấp nhô thở), HeadBob, Tired (thở dốc), hoặc một động tác tay (HandUp/Down/Left/Right) thay cả thân.
internal sealed partial class SansFight
{
	private const float SansScale = 2f;

	// ---- chân (SansLegs): gốc toạ độ của cả thân ----
	private float _sansX = 320f, _sansY = 224f, _sansXSpeed;
	private int _dodgeState;        // 0 đứng, 1 né sang trái, 2 chờ, 3 về chỗ cũ
	private float _dodgeTimer;

	// ---- thân / đầu / tay ----
	private bool _legsVisible = true, _torsoVisible = true, _bodyVisible;
	private string _sansAnim = "";                  // "", "Idle", "HeadBob", "Tired"
	private string _torsoAnim = "Default", _headAnim = "Default", _bodyAnim = "HandDown";
	private float _torsoT, _headT, _bodyT;
	private Vector2 _torsoOff, _headOff;
	private int _headFrame;
	private int _sweat;                              // 0 = ẩn, 1..3 = lượng mồ hôi

	private void InitSans()
	{
		SansHeadAnim("ClosedEyes");
		SansAnimation("");
	}

	private void SansAnimation(string name)
	{
		_bodyVisible = false;
		_legsVisible = true;
		_torsoVisible = true;
		_sansAnim = name;
	}

	private void SansBodyAnim(string name)
	{
		_sansAnim = "";
		_bodyVisible = true;
		_legsVisible = false;
		_torsoVisible = false;
		_bodyAnim = name;
		_bodyT = 0f;
	}

	private void SansTorsoAnim(string name)
	{
		_bodyVisible = false;
		_legsVisible = true;
		_torsoVisible = true;
		_torsoAnim = name;
	}

	private void SansHeadAnim(string name)
	{
		_headAnim = name;
		_headFrame = name == "BlueEye" ? _rng.Next(2) : 0;   // mắt xanh/vàng chớp ngẫu nhiên
	}

	private void SansSweat(int level) => _sweat = Math.Clamp(level, 0, 3);

	// ================================================================== cập nhật
	private void UpdateSans(float dt)
	{
		if (_sansXSpeed != 0f)
		{
			// Sans chạy vụt qua màn hình (đòn cuối): ra khỏi mép trái thì hiện lại bên phải với mặt/dáng ngẫu nhiên
			_sansY = ZT - 16f;
			_sansX += _sansXSpeed * dt;
			_sansXSpeed -= 45f * dt;
			if (_sansX < -100f)
			{
				_sansX = 740f;
				string[] heads = { "Default", "LookLeft", "Wink", "ClosedEyes", "NoEyes" };
				string[] torsos = { "Default", "Default", "Default", "Shrug" };
				SansHeadAnim(heads[_rng.Next(heads.Length)]);
				_torsoAnim = torsos[_rng.Next(torsos.Length)];
			}
		}
		else _sansY = (float)_tT - 16f;

		// ---- né đòn FIGHT: lướt sang trái, đứng chờ, rồi lướt về ----
		if (_dodgeState != 0) _dodgeTimer += dt;
		if (_dodgeState == 1)
		{
			_sansX = 320f - SinDeg(_dodgeTimer * 225f) * 100f;
			if (_dodgeTimer >= 0.4f)
			{
				_sansX = 220f;
				_dodgeState = 2;
				_dodgeTimer = 0f;
				_delayed.Add((_time + 0.6f, () => AddMissText(272f, 50f, 1.5f)));
			}
		}
		if (_dodgeState == 2 && _dodgeTimer >= 1.1f)
		{
			_dodgeState = 3;
			_dodgeTimer = 0f;
		}
		if (_dodgeState == 3)
		{
			_sansX = 320f - CosDeg(_dodgeTimer * 225f) * 100f;
			if (_dodgeTimer >= 0.4f)
			{
				_sansX = 320f;
				_dodgeState = 0;
				_dodgeTimer = 0f;
				HitAttempts++;
				_choice = null;
				if (_target != null) _target.State = 2;
				StartAttack();
			}
		}

		// ---- nhấp nhô ----
		if (_sansAnim.Length > 0) { _torsoT += dt; _headT += dt; }
		if (_sansAnim == "Idle")
		{
			if (_torsoT > 1.2f) _torsoT -= 1.2f;
			if (_headT > 1.2f) _headT -= 1.2f;
			_torsoOff = new Vector2(SinDeg(360f * _torsoT / 1.2f), SinDeg(720f * _torsoT / 1.2f));
			_headOff.Y = -SinDeg(720f * _headT / 1.2f) * 0.4f;
		}
		if (_sansAnim == "HeadBob")
		{
			if (_headT > 1.1f) _headT -= 1.1f;
			_headOff = new Vector2(SinDeg(360f * _headT / 1.1f), SinDeg(720f * _headT / 1.1f));
		}
		if (_sansAnim == "Tired")
		{
			if (_torsoT > 3.8f) _torsoT -= 3.8f;
			if (_headT > 3.8f) _headT -= 3.8f;
			_torsoOff.Y = SinDeg(360f * _torsoT / 3.8f);
			_headOff.Y = SinDeg(360f * _headT / 3.8f);
		}
		if (_sansAnim.Length == 0)
		{
			_torsoT = _headT = 0f;
			_torsoOff = _headOff = Vector2.Zero;
		}
		_bodyT += dt;
	}

	// ================================================================== hình học các mảnh (dùng cho vẽ và vị trí lời thoại)
	private struct Part { public Bitmap Tex; public RectangleF Dest; }

	/// <summary>Khung hình hiện tại của hoạt ảnh tay (15 khung/giây, chạy một lần rồi giữ khung cuối).</summary>
	private Bitmap BodyFrame()
	{
		var frames = SansAssets.Anim("SansBody", _bodyAnim);
		return frames[Math.Min(frames.Length - 1, (int)(_bodyT * 15f))];
	}

	// Điểm gắn đầu (chuẩn hoá theo ảnh) của từng khung hoạt ảnh tay: (x, y)
	private static readonly Dictionary<string, Vector2[]> BodyHeadPoints = new()
	{
		["HandDown"] = new[] { new Vector2(0.46875f, 0.4f), new Vector2(0.46875f, 0.385714f), new Vector2(0.46875f, 0.428571f), new Vector2(0.46875f, 0.442857f) },
		["HandUp"] = new[] { new Vector2(0.46875f, 0.428571f), new Vector2(0.46875f, 0.442857f), new Vector2(0.46875f, 0.4f), new Vector2(0.46875f, 0.385714f), new Vector2(0.46875f, 0.4f) },
		["HandRight"] = new[] { new Vector2(0.34375f, 0.125f), new Vector2(0.322917f, 0.125f), new Vector2(0.3125f, 0.125f), new Vector2(0.375f, 0.125f), new Vector2(0.354167f, 0.125f) },
		["HandLeft"] = new[] { new Vector2(0.354167f, 0.125f), new Vector2(0.375f, 0.125f), new Vector2(0.3125f, 0.125f), new Vector2(0.322917f, 0.125f), new Vector2(0.34375f, 0.125f) },
	};

	private static float BodyHotX(string anim) => anim is "HandLeft" or "HandRight" ? 0.34375f : 0.46875f;

	private Part LegsPart()
	{
		var tex = SansAssets.Frame("SansLegs", "Standing", 0);
		float w = tex.Width * SansScale, h = tex.Height * SansScale;
		return new Part { Tex = tex, Dest = new RectangleF(_sansX - 0.477273f * w, _sansY - h, w, h) };
	}

	/// <summary>Điểm "Torso" trên chân (nơi gắn thân): đỉnh chân, ngay trên gốc.</summary>
	private Vector2 TorsoAnchor() => new(_sansX, _sansY - SansAssets.Frame("SansLegs", "Standing", 0).Height * SansScale);

	private Part TorsoPart()
	{
		var tex = SansAssets.Frame("SansTorso", _torsoAnim, 0);
		float w = tex.Width * SansScale, h = tex.Height * SansScale;
		var a = TorsoAnchor() + _torsoOff;
		return new Part { Tex = tex, Dest = new RectangleF(a.X - 0.5f * w, a.Y - h, w, h) };
	}

	private Part BodyPart()
	{
		var tex = BodyFrame();
		float w = tex.Width * SansScale, h = tex.Height * SansScale;
		return new Part { Tex = tex, Dest = new RectangleF(_sansX - BodyHotX(_bodyAnim) * w, _sansY - h, w, h) };
	}

	/// <summary>Vị trí chân đầu (giữa-đáy cổ): lấy từ điểm gắn "Head" của thân hoặc của hoạt ảnh tay.</summary>
	private Vector2 HeadAnchor()
	{
		if (_bodyVisible)
		{
			var body = BodyPart();
			var pts = BodyHeadPoints[_bodyAnim];
			int frames = SansAssets.Anim("SansBody", _bodyAnim).Length;
			var p = pts[Math.Min(Math.Min(frames, pts.Length) - 1, (int)(_bodyT * 15f))];
			return new Vector2(body.Dest.X + p.X * body.Dest.Width, body.Dest.Y + p.Y * body.Dest.Height);
		}
		var torso = TorsoPart();
		float hy = _torsoAnim == "Shrug" ? 0.208333f : 0.24f;
		return new Vector2(torso.Dest.X + 0.5f * torso.Dest.Width + _headOff.X, torso.Dest.Y + hy * torso.Dest.Height + _headOff.Y);
	}

	private Part HeadPart()
	{
		var tex = SansAssets.Frame("SansHead", _headAnim, _headFrame);
		float w = tex.Width * SansScale, h = tex.Height * SansScale;
		var a = HeadAnchor();
		return new Part { Tex = tex, Dest = new RectangleF(a.X - 0.5f * w, a.Y - h, w, h) };
	}

	private Part SweatPart()
	{
		var tex = SansAssets.Frame("SansSweat", "Sweat" + Math.Max(1, _sweat), 0);
		float w = tex.Width * SansScale, h = tex.Height * SansScale;
		var head = HeadPart().Dest;
		return new Part { Tex = tex, Dest = new RectangleF(head.X + head.Width / 2f - 0.5f * w, head.Y, w, h) };
	}

	// ================================================================== lời thoại
	private readonly List<Vector2> _bubbles = new();

	/// <summary>Sans nói (hộp thoại bên phải-trên đầu): chữ hiện từng ký tự, bấm Z để sang câu tiếp; trong lúc nói kịch bản tạm dừng. endFunc mặc định là EndSansText (đóng hộp thoại + chạy tiếp).</summary>
	private void SansText(string text, string endFunc)
	{
		var bubble = new Vector2(_sansX + 64f, _sansY - 128f);
		_bubbles.Add(bubble);
		_texts.Add(new RpgText
		{
			Font = SansFont.Sans, Pos = bubble + new Vector2(32f, 16f), Full = text, Wrap = 256f, Tint = new Rgba(0f, 0f, 0f, 1f),
			Voice = "SansSpeak", Interactive = true, EndFunc = endFunc.Length == 0 ? "EndSansText" : endFunc, Layer = LayerEnemies, Name = "SansText",
		});
		_tl.Pause();
	}

	private void EndSansText()
	{
		_bubbles.Clear();
		_tl.Resume();
	}
}
