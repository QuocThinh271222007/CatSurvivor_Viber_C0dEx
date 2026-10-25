using System.Globalization;
using System.Numerics;

namespace CatSurvivor;

/// <summary>
/// Trận đánh Sans ("Bad Time Simulator") - port sang C# từ dự án Construct 2 của Jcw87 (github.com/Jcw87/c2-sans-fight), chạy ở màn hình logic 640x480.
/// Cấu trúc bám theo các event sheet gốc, mỗi file partial là một mảng:
///   SansFight.cs         : trạng thái chung, vòng cập nhật, lệnh của kịch bản, bắt đầu/kết thúc lượt, sát thương, KR, chết
///   SansFight.Heart.cs   : trái tim (đỏ đi tự do / xanh có trọng lực, nhảy, đập xuống - "slam"), va chạm với khung và bục
///   SansFight.Attacks.cs : xương, xương đâm, bục, Gaster Blaster, xương ở menu, vùng chiến đấu co giãn
///   SansFight.Menu.cs    : menu FIGHT / ACT / ITEM / MERCY, thanh Target, chữ gõ từng ký tự, vật phẩm
///   SansFight.Sans.cs    : thân Sans ghép từ chân/thân/đầu/mồ hôi, hoạt ảnh, né đòn, lời thoại
///   SansFight.Draw.cs    : vẽ
/// Các đòn đánh của Sans là kịch bản CSV (SansAttackData) do SansTimeline chạy. Thắng khi qua hết các lượt (Sans kiệt sức), thua khi hết máu.
/// </summary>
internal sealed partial class SansFight
{
	public const float W = 640f, H = 480f;

	public enum Outcome { Running, Won, Lost }

	public Outcome Result { get; private set; } = Outcome.Running;
	public readonly SansPad Pad = new();

	// ---- toàn cục (Globals.xml): HP 92, KR (karma) trừ dần vào HP ----
	public float Hp = 92f, MaxHp = 92f, Kr, KrT;
	/// <summary>Số lần người chơi chọn FIGHT (Sans né) - quyết định giai đoạn của trận đấu.</summary>
	public int HitAttempts;
	/// <summary>Chỉ số đòn kế tiếp trong giai đoạn hiện tại.</summary>
	public int NextAttack;
	/// <summary>Cheat (chỉ Debug): bất tử.</summary>
	public bool God;
	public float Time => _time;

	private float _time;
	private bool _begun, _black;
	private float _lastDamageTime = -10f;
	private string _infoText = "";
	private readonly Random _rng;
	private readonly SansTimeline _tl;

	public SansFight(int? seed = null)
	{
		_rng = seed.HasValue ? new Random(seed.Value) : new Random();
		_tl = new SansTimeline(_rng) { Call = RunFunction, Panic = BattlePanic, HeartPos = () => (_hx, _hy) };
		_items.AddRange(new[] { 0, 1, 2, 3, 3, 3, 3, 3 }); // Pie, Noodles, Steak, 5 x Legendary Hero
		InitSans();
		InitZone();
		InitHeart();
		SoundService.PreloadExtras("BattleText", "BoneStab", "Ding", "Flash", "GasterBlast", "GasterBlast2", "GasterBlaster", "HeartShatter", "HeartSplit",
			"MenuCursor", "MenuSelect", "PlayerDamaged", "PlayerFight", "PlayerHeal", "SansSpeak", "Slam", "Warning");
	}

	/// <summary>Cheat (chỉ Debug): nhảy thẳng tới giai đoạn cuối - lần FIGHT kế tiếp là đòn cuối cùng của Sans.</summary>
	public void CheatSkipToFinal() { HitAttempts = 22; NextAttack = 0; }

	/// <summary>Nạp trước nhạc nền (mất khoảng nửa giây): gọi trước khi mở cửa sổ để lúc vào trận không bị khựng.</summary>
	public static void PreloadMusic() => SoundService.PreloadExtras("mus_zz_megalovania");

	// ================================================================== vòng cập nhật
	/// <summary>Chạy một bước mô phỏng dt giây. Thứ tự các nhóm sự kiện giống event sheet gốc.</summary>
	public void Update(float dt)
	{
		_time += dt;
		if (!_begun) { _begun = true; StartAttack(); }

		StepBehaviors(dt);          // trái tim, bục di chuyển theo vận tốc ("Custom movement" của Construct 2)
		_tl.Update(dt);             // chạy kịch bản đòn đánh
		UpdateTexts(dt);            // chữ gõ từng ký tự + lời thoại
		UpdateMenu(dt);             // menu, thanh Target
		UpdatePlatforms();
		HeartControl(dt);           // điều khiển trái tim (đặt vận tốc cho bước sau)
		UpdateBones(dt);
		UpdateStabs(dt);
		UpdateBlasters(dt);
		UpdateMenuBones(dt);
		ZoneTick(dt);
		UpdateSans(dt);
		UpdateDamage(dt);
		UpdateShake(dt);
		UpdateDeath(dt);

		Pad.EndTick();
	}

	// ================================================================== lệnh (kịch bản CSV + nội bộ)
	private static float F(string s) =>
		float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float v) && !float.IsNaN(v) ? v : 0f;

	private static int I(string s) => (int)F(s);

	/// <summary>Thực hiện một lệnh theo tên (từ kịch bản, hoặc "hàm" nội bộ như StartAttack / MenuBattle được gọi khi hết chữ, hết co vùng...).</summary>
	private void RunFunction(string name, string[] a)
	{
		switch (name)
		{
			// ---- chung ----
			case "BlackScreen": BlackScreen(I(a[0]) == 1); break;
			case "Sound": SoundService.PlayExtra(a[0], a[1].Length > 0 ? F(a[1]) : 1f); break;
			case "Music": SoundService.PlayExtraMusic(a[0]); break;
			case "TLPause": _tl.Pause(); break;
			case "TLResume": _tl.Resume(); break;
			case "EndAttack": EndAttack(); break;
			// ---- tấn công ----
			case "BoneH": AddBone(false, I(a[0]), I(a[1]), I(a[2]), I(a[3]), I(a[4]), I(a[5])); break;
			case "BoneV": AddBone(true, I(a[0]), I(a[1]), I(a[2]), I(a[3]), I(a[4]), I(a[5])); break;
			case "BoneHRepeat": BoneRepeat(false, a); break;
			case "BoneVRepeat": BoneRepeat(true, a); break;
			case "SineBones": SineBones(I(a[0]), I(a[1]), I(a[2]), I(a[3])); break;
			case "BoneStab": BoneStab(I(a[0]), I(a[1]), F(a[2]), F(a[3])); break;
			case "GasterBlaster": GasterBlaster(I(a[0]), I(a[1]), I(a[2]), I(a[3]), I(a[4]), I(a[5]), F(a[6]), F(a[7])); break;
			case "Platform": AddPlatform(I(a[0]), I(a[1]), I(a[2]), I(a[3]), I(a[4]), I(a[5]) != 0); break;
			case "PlatformRepeat": PlatformRepeat(a); break;
			// ---- trái tim ----
			case "HeartMode": HeartMode(I(a[0])); break;
			case "HeartTeleport": _hx = I(a[0]); _hy = I(a[1]); _hVisible = true; break;
			case "HeartMaxFallSpeed": _maxFall = I(a[0]); break;
			case "SansSlam": SansSlam(F(a[0])); break;
			case "SansSlamDamage": _slamDamage = I(a[0]) != 0; break;
			// ---- vùng chiến đấu ----
			case "CombatZoneSpeed": _resizeSpeed = I(a[0]); break;
			case "CombatZoneResize": ZoneResize(F(a[0]), F(a[1]), F(a[2]), F(a[3]), a[4]); break;
			case "CombatZoneResizeInstant":
			case "CombatZoneResizeAuto": ZoneResizeInstant(F(a[0]), F(a[1]), F(a[2]), F(a[3])); break;
			// ---- Sans ----
			case "SansAnimation": SansAnimation(a[0]); break;
			case "SansBody": SansBodyAnim(a[0]); break;
			case "SansTorso": SansTorsoAnim(a[0]); break;
			case "SansHead": SansHeadAnim(a[0]); break;
			case "SansSweat": SansSweat(I(a[0])); break;
			case "SansX": _sansX = I(a[0]); break;
			case "SansRepeat": _sansXSpeed = -900f; break;
			case "SansEndRepeat": _sansXSpeed = 0f; break;
			case "SansText": SansText(a[0].Replace("\\n", "\n"), a[1]); break;
			// ---- nội bộ (EndFunc của chữ, kết thúc co vùng...) ----
			case "StartAttack": StartAttack(); break;
			case "MenuBattle": MenuBattle(); break;
			case "MenuFight": MenuFight(); break;
			case "MenuFightEnemy": MenuFightEnemy(); break;
			case "MenuAct": MenuAct(); break;
			case "MenuActEnemy": MenuActEnemy(); break;
			case "MenuCheckSans": MenuCheckSans(); break;
			case "MenuCheckSans2": MenuCheckSans2(); break;
			case "MenuItem": MenuItemList(); break;
			case "MenuUseItem": MenuUseItem(); break;
			case "MenuMercy": MenuMercy(); break;
			case "MenuSpare": MenuSpare(); break;
			case "EndSansText": EndSansText(); break;
			case "Win1": SansText("alright, i guess\nyou win.", "Win2"); break;
			case "Win2": Result = Outcome.Won; break;
			default: TestUnknown.Add(name); break; // lệnh lạ: bỏ qua (như Construct 2 khi gọi hàm không có)
		}
	}

	private void Call(string name, params string[] args)
	{
		var a = new string[9];
		for (int i = 0; i < 9; i++) a[i] = i < args.Length ? args[i] : "";
		RunFunction(name, a);
	}

	/// <summary>Lỗi kịch bản: hiện chữ báo lỗi 3 giây rồi kết thúc lượt (như BattlePanic của bản gốc).</summary>
	private void BattlePanic(string message)
	{
		_texts.RemoveAll(t => t.Name == "Error");
		_texts.Add(new RpgText { Font = SansFont.Battle, Pos = new Vector2(0, 0), Scale = 3, Shown = message.ToUpperInvariant(), Timeout = 3f, Name = "Error", Layer = LayerOverlay, Wrap = 640f });
		EndAttack();
	}

	private void BlackScreen(bool on)
	{
		_black = on;
		if (on)
		{
			_zoneVisible = false;
			SoundService.SetExtraMusicPaused(true);
			ClearAttackObjects();
		}
		else
		{
			SoundService.SetExtraMusicPaused(false);
			_zoneVisible = true;
		}
	}

	// ================================================================== lượt đánh
	private static readonly string[] Phase1 =
	{
		"sans_intro", "sans_bonegap1", "sans_bluebone", "sans_bonegap2", "sans_platforms1", "sans_platforms2", "sans_platforms3",
		"sans_platforms4", "sans_platformblaster", "sans_platforms4hard", "sans_bonegap1fast", "sans_boneslideh", "sans_bonegap2", "sans_platformblasterfast",
	};
	private static readonly string[] Phase1Random = { "sans_bonegap1fast", "sans_bonegap2", "sans_boneslideh", "sans_platformblasterfast" };
	private static readonly string[] Phase2 =
	{
		"sans_multi1", "sans_randomblaster1", "sans_multi2", "sans_bonestab1", "sans_bonestab2", "sans_randomblaster2", "sans_boneslidev", "sans_multi3", "sans_bonestab3",
	};
	private static readonly string[] Phase2Random = { "sans_bonestab3", "sans_multi3", "sans_randomblaster2" };

	private void ResetVars()
	{
		_resizeSpeed = 480f;
		if (_hMode == 0 || _hMode == 1) HeartMode(_hMode);
		_maxFall = 750f;
		_slamDamage = false;
		_sansXSpeed = 0f;
		_sansX = 320f;
	}

	private void RunAttack(string name)
	{
		if (SansAttackData.Scripts.TryGetValue(name, out var script)) _tl.Play(script);
		else BattlePanic("Missing attack " + name);
	}

	/// <summary>Bắt đầu lượt Sans tấn công (sau khi người chơi chọn xong ở menu). Đòn nào phụ thuộc HitAttempts (số lần FIGHT) và NextAttack.</summary>
	private void StartAttack()
	{
		_hEnabled = true;
		_hdx = _hdy = 0f;
		ResetVars();
		MenuBonesOff();

		if (HitAttempts < 13)
		{
			if (NextAttack < Phase1.Length) RunAttack(Phase1[NextAttack++]);
			else RunAttack(Phase1Random[_rng.Next(Phase1Random.Length)]);
		}
		else if (HitAttempts == 13)
		{
			// Lượt "Sans nghỉ": chỉ một đòn trống, nhạc tạm dừng
			RunAttack("sans_spare");
			SansSweat(2);
			NextAttack = 0;
			SoundService.SetExtraMusicPaused(true);
		}
		else if (HitAttempts <= 22)
		{
			SoundService.SetExtraMusicPaused(false); // (bản gốc chỉ tiếp tục nhạc ở cuối lượt; ở đây nhạc quay lại ngay khi Sans tấn công)
			if (NextAttack < Phase2.Length) RunAttack(Phase2[NextAttack++]);
			else RunAttack(Phase2Random[_rng.Next(Phase2Random.Length)]);
		}
		else RunAttack("sans_final");
	}

	/// <summary>Kết thúc lượt tấn công: dọn đòn, vùng chiến đấu co về khung menu, chọn dòng thông báo, hoặc (qua lượt cuối) Sans kiệt sức và thua cuộc.</summary>
	private void EndAttack()
	{
		ClearAttackObjects();
		BattleMenuEnable(true);
		_hVisible = true;
		_hEnabled = false;
		ResetVars();
		HeartMode(0);          // ở menu trái tim luôn đỏ (bản gốc để nguyên màu của đòn vừa rồi)
		_black = false;

		SansAnimation("Idle");
		SansHeadAnim("Default");
		SansTorsoAnim("Default");

		_infoText = "* You felt your sins crawling\n  on your back.";
		if (HitAttempts < 13 && NextAttack == 1)
		{
			_infoText = "* You feel like you're going to\n  have a bad time.";
			if (!SoundService.ExtraMusicPlaying) SoundService.PlayExtraMusic("mus_zz_megalovania");
		}
		if (HitAttempts != 13) SoundService.SetExtraMusicPaused(false);
		else _infoText = "* Sans is taking a break.";
		if (HitAttempts == 15) _infoText = "* The REAL battle finally begins.";
		if (HitAttempts == 19) _infoText = "* Reading this doesn't seem\n  like the best use of time.";
		if (HitAttempts == 20) _infoText = "* Sans is starting to look\n  really tired.";
		if (HitAttempts == 21) _infoText = "* Sans is preparing something.";
		if (HitAttempts == 22) _infoText = "* Sans is getting ready to\n  use his special attack.";

		if (HitAttempts > 22)
		{
			// Hết lượt: Sans thở hổn hển, bạn thắng
			BattleMenuEnable(false);
			ZoneResize(ZL, ZT, ZR, ZB, "");
			SansAnimation("Tired");
			SansHeadAnim("Tired2");
			SoundService.StopExtra("*");
			SoundService.StopExtraMusic();
			SansText("huff... puff...", "Win1");
		}
		if (HitAttempts > 13 && HitAttempts != 16 && HitAttempts != 17 && HitAttempts <= 22) MenuBoneLeft();
		if (HitAttempts > 15 && HitAttempts <= 22) StartBottomBones();
	}

	// ================================================================== sát thương, KR, chết
	private void DamagePlayer(float damage, float karma)
	{
		_lastDamageTime = _time;
		Hp -= damage;
		Kr += karma;
		SoundService.PlayExtra("PlayerDamaged");
	}

	private void UpdateDamage(float dt)
	{
		if (_lastDamageTime < _time - 0.033f)
		{
			var hit = new RectangleF(_hx - 2f, _hy - 2f, 4f, 4f); // PlayerHitbox 4x4 ở tâm trái tim
			CheckMenuBoneHits(hit);
			CheckBlastHits(hit);
			CheckBoneHits(hit);
		}

		if (God) { Hp = MaxHp; Kr = 0f; }

		// KR (karma): tối đa 40, không vượt HP-1, rồi trừ dần cả KR lẫn HP - càng nhiều KR trừ càng nhanh
		if (Kr > 40f) Kr = 40f;
		if (Kr >= Hp) Kr = Hp - 1f;
		if (Kr > 0f && Hp > 1f)
		{
			KrT += dt;
			if (Kr >= 40f && KrT >= 0.033f) DrainKr();
			if (Kr >= 30f && KrT >= 0.066f) DrainKr();
			if (Kr >= 20f && KrT >= 0.166f) DrainKr();
			if (Kr >= 10f && KrT >= 0.5f) DrainKr();
			if (KrT >= 1f) DrainKr();
		}
		if (Hp > MaxHp) Hp = MaxHp;

		if (Hp <= 0f && _hMode >= 0) BeginDeath();
	}

	private void DrainKr() { Kr -= 1f; Hp -= 1f; KrT = 0f; }

	// ---- chết: trái tim vỡ đôi rồi vỡ vụn, rơi xuống ----
	private struct Shard { public Vector2 Pos, Vel; }
	private readonly List<Shard> _shards = new();
	private float _deathT;
	private int _deathStage = -1;
	private bool _heartSplit;

	private void BeginDeath()
	{
		_hMode = -1;
		_hEnabled = false;
		_hAngle = 90f;
		_hdx = _hdy = 0f;
		_tl.Stop();
		BlackScreen(true);
		SoundService.StopExtra("*");
		SoundService.StopExtraMusic();
		ClearAttackObjects();
		_deathT = 0f;
		_deathStage = 0;
	}

	private void UpdateDeath(float dt)
	{
		for (int i = 0; i < _shards.Count; i++)
		{
			var s = _shards[i];
			s.Vel += new Vector2(0f, 300f * dt);     // gia tốc 300 px/s^2 hướng xuống
			s.Pos += s.Vel * dt;
			_shards[i] = s;
		}
		if (_deathStage < 0) return;
		_deathT += dt;
		if (_deathStage == 0 && _deathT >= 20f / 30f)
		{
			_heartSplit = true;
			SoundService.PlayExtra("HeartSplit");
			_deathStage = 1;
		}
		if (_deathStage == 1 && _deathT >= 20f / 30f + 40f / 30f)
		{
			_hVisible = false;
			SoundService.PlayExtra("HeartShatter");
			for (int i = 0; i < 6; i++)
			{
				float ang = (float)(_rng.NextDouble() * Math.PI * 2.0);
				_shards.Add(new Shard { Pos = new Vector2(_hx, _hy), Vel = new Vector2(MathF.Cos(ang), MathF.Sin(ang)) * 180f });
			}
			_deathStage = 2;
		}
		if (_deathStage == 2 && _deathT >= 20f / 30f + 40f / 30f + 2f)
		{
			_deathStage = 3;
			Result = Outcome.Lost;
		}
	}

	// ================================================================== lắc màn hình
	private int _shakeIntensity;
	private float _shakeTimer;
	/// <summary>Độ lệch cả màn hình (px) khi đang rung.</summary>
	public Vector2 ShakeOffset { get; private set; }

	private void Shake(int intensity)
	{
		_shakeIntensity = intensity;
		_shakeTimer = 0f;
	}

	private void UpdateShake(float dt)
	{
		if (_shakeIntensity > 0) _shakeTimer += dt;
		else ShakeOffset = Vector2.Zero;
		if (_shakeTimer >= 1f / 30f)
		{
			_shakeTimer -= 1f / 30f;
			_shakeIntensity--;
			ShakeOffset = new Vector2(_shakeIntensity * (_rng.Next(2) == 0 ? 1 : -1), _shakeIntensity * (_rng.Next(2) == 0 ? 1 : -1));
		}
	}

	// ================================================================== tiện ích
	private static float Rad(float deg) => deg * (MathF.PI / 180f);
	private static float CosDeg(float deg) => MathF.Cos(Rad(deg));
	private static float SinDeg(float deg) => MathF.Sin(Rad(deg));

	/// <summary>Cos/sin chính xác cho hướng 0..3 (0 phải, 1 xuống, 2 trái, 3 lên): tránh sai số 6e-17 của cos(90°).</summary>
	private static Vector2 DirVec(int dir) => ((dir % 4 + 4) % 4) switch
	{
		0 => new Vector2(1f, 0f),
		1 => new Vector2(0f, 1f),
		2 => new Vector2(-1f, 0f),
		_ => new Vector2(0f, -1f),
	};
}
