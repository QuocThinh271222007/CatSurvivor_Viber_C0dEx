using System.Diagnostics;
using System.Numerics;

namespace CatSurvivor;

// Kiểm tra trận đánh Sans (easter egg): CatSurvivor.exe --selftest --sans --out <thư mục>
internal static partial class SelfTest
{
	private const float SansDt = 1f / 60f;

	private static void SansShot(SansFight f, string name)
	{
		using var fb = new FrameBuffer((int)SansFight.W, (int)SansFight.H);
		fb.G.Clear(Color.Black);
		var cv = new Canvas();
		cv.Begin(fb.G, 1f, PointF.Empty, false);
		f.Draw(cv);
		using var bmp = new Bitmap((int)SansFight.W, (int)SansFight.H);
		using var g = Graphics.FromImage(bmp);
		fb.BlitTo(g, new Rectangle(0, 0, bmp.Width, bmp.Height));
		bmp.Save(Path.Combine(_out, name + ".png"));
	}

	/// <summary>Chạy một bước, với các nút giả lập (bấm trong đúng khung hình này).</summary>
	private static void SansStep(SansFight f, params SansBtn[] press)
	{
		foreach (var b in press) f.Pad.Tap(b, true);
		f.Update(SansDt);
		foreach (var b in press) f.Pad.Tap(b, false);
	}

	/// <summary>Bot người chơi: lướt qua chữ (X rồi Z), chọn FIGHT, bấm Target; né đòn thì không (dùng God).</summary>
	private static SansBtn[] SansBotInput(SansFight f, int tick)
	{
		if (f.TestTextWaiting) return tick % 20 == 0 ? new[] { SansBtn.Cancel } : tick % 20 == 10 ? new[] { SansBtn.Confirm } : Array.Empty<SansBtn>();
		if (f.TestTargetActive) return tick % 25 == 0 ? new[] { SansBtn.Confirm } : Array.Empty<SansBtn>();
		if (f.TestMenuState >= 2) return tick % 25 == 0 ? new[] { SansBtn.Confirm } : Array.Empty<SansBtn>();
		return Array.Empty<SansBtn>();
	}

	private static void TestSansFight()
	{
		Say("== SANS FIGHT (easter egg)");
		var sw = Stopwatch.StartNew();
		SansFight.PreloadMusic();
		Say($"  nạp nhạc Megalovania: {sw.ElapsedMilliseconds} ms, nạp xong={SoundService.LoadedCount >= 0}");

		TestSansTimeline();

		// ---- 1) từng kịch bản chạy tới EndAttack, không lỗi, không lệnh lạ ----
		Say("  -- chạy từng kịch bản đòn đánh (bất tử, đứng yên) --");
		int bad = 0;
		var unknown = new HashSet<string>();
		foreach (var name in SansAttackData.Scripts.Keys.OrderBy(k => k))
		{
			var f = new SansFight(7) { God = true };
			f.TestSkipBegin();
			f.HitAttempts = 5;
			f.TestRunAttack(name);
			int maxBones = 0, maxBlasters = 0, maxPlat = 0, ticks = 0;
			bool menu = false;
			for (; ticks < 180 * 60; ticks++)
			{
				SansStep(f, SansBotInput(f, ticks));
				maxBones = Math.Max(maxBones, f.TestBoneCount);
				maxBlasters = Math.Max(maxBlasters, f.TestBlasterCount);
				maxPlat = Math.Max(maxPlat, f.TestPlatformCount);
				if (f.TestPanicked) break;
				if (f.TestBackInMenu) { menu = true; break; }
			}
			unknown.UnionWith(f.TestUnknown);
			bool ok = menu && !f.TestPanicked;
			if (!ok) bad++;
			Say($"    {name,-26} {(ok ? "OK " : "!! ")} {ticks / 60f,6:0.0}s  xương tối đa {maxBones,3}  blaster {maxBlasters,2}  bục {maxPlat,2}{(f.TestPanicked ? "  LỖI KỊCH BẢN" : "")}");
		}
		Say($"  kết quả: {SansAttackData.Scripts.Count - bad}/{SansAttackData.Scripts.Count} kịch bản chạy hết; lệnh lạ: {(unknown.Count == 0 ? "không" : string.Join(",", unknown))}");

		// ---- 2) bot đánh cả trận (bất tử) + chụp ảnh các cảnh chính ----
		Say("  -- bot đánh hết cả trận (bất tử) --");
		{
			var f = new SansFight(3) { God = true };
			var shots = new HashSet<string>();
			bool Once(string k) => shots.Add(k);
			int lastHit = -1;
			float t = 0f;
			int tick = 0;
			var turnLog = new List<string>();
			bool musicAtFirstMenu = false;
			for (; tick < 40 * 60 * 60 && f.Result == SansFight.Outcome.Running; tick++)
			{
				SansStep(f, SansBotInput(f, tick));
				t += SansDt;
				if (f.HitAttempts != lastHit) { lastHit = f.HitAttempts; turnLog.Add($"HitAttempts={f.HitAttempts}@{t:0}s"); }

				var h = f.TestHeart;
				if (f.TestTextWaiting && t > 0.8f && Once("intro_text")) SansShot(f, "sans_01_intro_text");
				if (f.TestMenuState == 2 && Once("menu")) { SansShot(f, "sans_02_menu"); musicAtFirstMenu = SoundService.ExtraMusicPlaying; }
				if (f.TestScriptRunning && f.TestBoneCount >= 6 && h.mode == 1 && f.HitAttempts == 1 && Once("bones_blue")) SansShot(f, "sans_03_bones_blue");
				if (f.TestPlatformCount >= 2 && f.TestBoneCount >= 4 && Once("platforms")) SansShot(f, "sans_04_platforms");
				if (f.TestBlasterCount >= 2 && Once("blasters")) SansShot(f, "sans_05_blasters");
				if (f.HitAttempts >= 14 && f.TestMenuBoneCount > 0 && f.TestMenuState >= 2 && Once("menubone")) SansShot(f, "sans_06_menubones");
				if (f.HitAttempts == 23 && f.TestBoneCount >= 3 && Once("final")) SansShot(f, "sans_07_final");
			}
			SansShot(f, "sans_09_end");
			Say($"    kết quả: {f.Result} sau {t:0}s mô phỏng ({t / 60f:0.0} phút), HitAttempts cuối = {f.HitAttempts}, HP {f.Hp:0}/{f.MaxHp:0}");
			Say("    các lượt: " + string.Join(" ", turnLog.Take(40)));
			Say($"    nhạc Megalovania: bắt đầu ở menu đầu tiên={musicAtFirstMenu}, sau khi thắng còn phát={SoundService.ExtraMusicPlaying} (mong đợi true / false)");
			Say($"    -> {(f.Result == SansFight.Outcome.Won && musicAtFirstMenu && !SoundService.ExtraMusicPlaying ? "OK (thắng được)" : "!! SAI")}");
		}

		// ---- 3) không né: phải chết ----
		Say("  -- đứng yên không né: phải thua --");
		{
			var f = new SansFight(11);
			float t = 0f;
			bool shotDeath = false;
			for (int tick = 0; tick < 20 * 60 * 60 && f.Result == SansFight.Outcome.Running; tick++)
			{
				SansStep(f, SansBotInput(f, tick));
				t += SansDt;
				if (f.TestHeart.mode < 0 && !shotDeath && t > 0) { shotDeath = true; SansShot(f, "sans_08_dead"); }
			}
			Say($"    kết quả: {f.Result} sau {t:0}s, HP {f.Hp:0}, trái tim mode {f.TestHeart.mode}  -> {(f.Result == SansFight.Outcome.Lost ? "OK" : "!! SAI")}");
		}

		TestSansGallery();
		TestSansMenus();
		TestSansMenus2();
		TestSansHeartPhysics();
		TestSansHazards();
		TestSansUnlock();
		TestSansIntegration();
		TestSansEndToEnd();
	}

	// ------------------------------------------------------------------ ảnh chụp các cảnh để xem bằng mắt
	private static void TestSansGallery()
	{
		Say("  -- chụp ảnh các cảnh (sans_g*.png) --");
		// (a) lời thoại + (b) thanh Target + đòn chém + Sans né
		var f = new SansFight(21) { God = true };
		for (int i = 0; i < 40; i++) SansStep(f);
		SansShot(f, "sans_g1_ready");
		int tick = 0;
		bool shotTarget = false, shotStrike = false;
		for (; tick < 6000; tick++)
		{
			SansStep(f, SansBotInput(f, tick));
			if (f.TestTargetActive && !shotTarget && tick % 7 == 0) { shotTarget = true; SansShot(f, "sans_g2_target"); }
			if (f.TestStrike && !shotStrike) { shotStrike = true; for (int k = 0; k < 14; k++) SansStep(f); SansShot(f, "sans_g3_strike"); break; }
		}
		// (c) menu thông tin đã gõ xong
		for (; tick < 9000 && !f.TestBackInMenu; tick++) SansStep(f, SansBotInput(f, tick));
		for (int i = 0; i < 150; i++) SansStep(f);
		SansShot(f, "sans_g4_info");
		// (d) danh sách đồ
		SansStep(f, SansBtn.Right); SansStep(f, SansBtn.Right); SansStep(f, SansBtn.Confirm);
		for (int i = 0; i < 20; i++) SansStep(f);
		SansShot(f, "sans_g5_items");
		// (e) chùm tia Gaster Blaster + (f) xương đâm
		var b = new SansFight(4) { God = true };
		b.TestSkipBegin();
		b.TestRunAttack("sans_randomblaster1");
		bool shotBeam = false;
		for (int i = 0; i < 60 * 9 && !shotBeam; i++) { SansStep(b); if (b.TestBeamCount >= 2) { for (int k = 0; k < 8; k++) SansStep(b); SansShot(b, "sans_g6_beam"); shotBeam = true; } }
		var s = new SansFight(4) { God = true };
		s.TestSkipBegin();
		s.TestRunAttack("sans_bonestab1");
		bool shotWarn = false, shotStab = false;
		for (int i = 0; i < 60 * 10 && !shotStab; i++)
		{
			SansStep(s);
			if (s.TestWarnCount > 0 && !shotWarn) { shotWarn = true; SansShot(s, "sans_g7_warn"); }
			if (s.TestStabCount > 0 && !shotStab) { for (int k = 0; k < 12; k++) SansStep(s); SansShot(s, "sans_g8_stab"); shotStab = true; }
		}
		// (g) Sans kiệt sức khi thắng + lời thoại cuối
		var w = new SansFight(6) { God = true };
		w.TestSkipBegin();
		w.HitAttempts = 23;
		w.TestRunAttack("sans_spare");
		for (int i = 0; i < 60 * 12 && w.Result == SansFight.Outcome.Running; i++)
		{
			SansStep(w, w.TestTextWaiting && i % 40 == 5 ? new[] { SansBtn.Cancel } : Array.Empty<SansBtn>());
			if (i == 60 * 4) SansShot(w, "sans_g9_tired");
		}
		Say($"    ảnh: ready={shotTarget || true}, target={shotTarget}, strike={shotStrike}, beam={shotBeam}, warn={shotWarn}, stab={shotStab}");
	}

	// ------------------------------------------------------------------ bộ chạy kịch bản
	private static void TestSansTimeline()
	{
		Say("  -- SansTimeline (biến, nhãn, nhảy, toán) --");
		var calls = new List<string>();
		var tl = new SansTimeline(new Random(1)) { Call = (n, a) => calls.Add(n + ":" + string.Join("/", a.Take(3))), HeartPos = () => (12f, 34f) };
		tl.Play("0,SET,I,0\n0,:loop\n0,ADD,I,$I,1\n0,JMPL,loop,$I,5\n0,Out,$I\n0.5,Late,x\n0,SIN,S,90\n0,Out,$S\n0,GetHeartPos,HX,HY\n0,Out,$HX,$HY\n0,JMPREL,2\n0,Skipped\n0,Out,end\n0,ANGLE,A,0,0,0,10\n0,Out,$A");
		for (int i = 0; i < 90; i++) tl.Update(SansDt);
		string joined = string.Join(" | ", calls);
		Say("    " + joined);
		bool ok = calls.SequenceEqual(new[] { "Out:5//", "Late:x//", "Out:1//", "Out:12/34/", "Out:end//", "Out:90//" });
		Say($"    -> {(ok ? "OK" : "!! SAI")}");
	}

	// ------------------------------------------------------------------ menu
	private static void TestSansMenus()
	{
		Say("  -- menu: ACT/Check, ITEM, MERCY, huỷ (X), vòng con trỏ --");
		var f = new SansFight(5) { God = true };
		f.TestSkipBegin();
		f.HitAttempts = 3; f.NextAttack = 2;
		f.TestRunAttack("sans_spare");
		for (int i = 0; i < 600 && !f.TestBackInMenu; i++) SansStep(f);
		for (int i = 0; i < 90; i++) SansStep(f);          // để co khung + hiện chữ
		Say($"    vào menu: state={f.TestMenuState}, chọn={f.TestSelected}, khung={f.TestZone}, chữ='{f.TestInfoText.Replace("\n", " ")}'");
		bool ok = f.TestMenuState == 2 && f.TestSelected == 0;

		SansStep(f, SansBtn.Right); SansStep(f, SansBtn.Right);
		ok &= f.TestSelected == 2;
		SansStep(f, SansBtn.Right); SansStep(f, SansBtn.Right);          // 3 rồi vòng về 0
		ok &= f.TestSelected == 0;
		SansStep(f, SansBtn.Left);                                      // 0 sang trái -> vòng về 3
		ok &= f.TestSelected == 3;
		SansStep(f, SansBtn.Left); SansStep(f, SansBtn.Left); SansStep(f, SansBtn.Left); // 2,1,0
		ok &= f.TestSelected == 0;

		// ITEM: dùng Pie
		f.Hp = 20f;
		SansStep(f, SansBtn.Right); SansStep(f, SansBtn.Right);          // ITEM
		SansStep(f, SansBtn.Confirm);                                    // mở danh sách đồ
		ok &= f.TestMenuState == 3 && f.TestMenuDepth == 2;
		SansStep(f, SansBtn.Cancel);                                     // X: quay lại menu chính
		ok &= f.TestMenuState == 2 && f.TestMenuDepth == 1 && f.TestSelected == 2;
		SansStep(f, SansBtn.Confirm);
		int itemsBefore = f.TestItemCount;
		SansStep(f, SansBtn.Confirm);                                    // dùng món đầu (Pie +99, tối đa MaxHP)
		Say($"    ITEM: HP 20 -> {f.Hp:0}, đồ {itemsBefore} -> {f.TestItemCount}");
		ok &= f.TestItemCount == itemsBefore - 1 && f.Hp > 20f;

		// chữ gõ xong rồi Z -> Sans tấn công
		for (int i = 0; i < 400 && f.TestScriptRunning == false; i++) SansStep(f, i % 20 == 0 ? new[] { SansBtn.Cancel } : i % 20 == 10 ? new[] { SansBtn.Confirm } : Array.Empty<SansBtn>());
		Say($"    sau khi dùng đồ: kịch bản đang chạy={f.TestScriptRunning}, menu state={f.TestMenuState}");
		ok &= f.TestScriptRunning || f.TestBackInMenu;
		Say($"    -> {(ok ? "OK" : "!! SAI")}");
	}

	private static SansFight MenuFight(int hit)
	{
		var f = new SansFight(8) { God = true };
		f.TestSkipBegin();
		f.HitAttempts = hit; f.NextAttack = 3;
		f.TestRunAttack("sans_spare");
		for (int i = 0; i < 600 && !f.TestBackInMenu; i++) SansStep(f);
		for (int i = 0; i < 90; i++) SansStep(f);
		return f;
	}

	private static void TestSansMenus2()
	{
		Say("  -- menu: ACT > Check, FIGHT trượt, MERCY > Spare --");
		bool ok = true;
		{
			var f = MenuFight(0);
			SansStep(f, SansBtn.Right);                       // ACT
			SansStep(f, SansBtn.Confirm);                     // danh sách đối thủ
			ok &= f.TestMenuState == 3 && f.TestMenuDepth == 2;
			SansStep(f, SansBtn.Confirm);                     // * Sans
			ok &= f.TestMenuState == 3 && f.TestMenuDepth == 3;
			SansStep(f, SansBtn.Confirm);                     // * Check
			ok &= f.TestMenuState == 0 && f.TestTextWaiting;  // chữ thông tin đang gõ
			for (int i = 0; i < 300 && !f.TestAttacking; i++) SansStep(f, i % 20 == 0 ? new[] { SansBtn.Cancel } : i % 20 == 10 ? new[] { SansBtn.Confirm } : Array.Empty<SansBtn>());
			Say($"    ACT > Check: sau khi đọc xong Sans tấn công={f.TestAttacking}");
			ok &= f.TestAttacking;
		}
		{
			var f = MenuFight(2);
			int before = f.HitAttempts;
			SansStep(f, SansBtn.Confirm);                     // FIGHT
			SansStep(f, SansBtn.Confirm);                     // * Sans -> hiện thanh Target
			ok &= f.TestTargetActive;
			for (int i = 0; i < 60 * 4 && f.TestTargetActive; i++) SansStep(f);   // để thanh chạy qua, không bấm
			for (int i = 0; i < 5; i++) SansStep(f);
			Say($"    FIGHT trượt: HitAttempts {before} -> {f.HitAttempts} (mong đợi không đổi), Sans tấn công={f.TestAttacking}");
			ok &= f.HitAttempts == before && f.TestAttacking;
		}
		{
			var f = MenuFight(4);
			SansStep(f, SansBtn.Left);                        // vòng sang MERCY
			ok &= f.TestSelected == 3;
			SansStep(f, SansBtn.Confirm); SansStep(f, SansBtn.Confirm);   // * Spare
			Say($"    MERCY > Spare: Sans tấn công={f.TestAttacking}, menu state={f.TestMenuState}");
			ok &= f.TestAttacking && f.TestMenuState == 0;
		}
		{
			// FIGHT trúng: HitAttempts +1, Sans né rồi tấn công; thanh Target bấm giữa chừng
			var f = MenuFight(2);
			SansStep(f, SansBtn.Confirm); SansStep(f, SansBtn.Confirm);
			for (int i = 0; i < 20; i++) SansStep(f);
			SansStep(f, SansBtn.Confirm);
			ok &= f.TestDodging;
			for (int i = 0; i < 60 * 3 && f.TestDodging; i++) SansStep(f);
			for (int i = 0; i < 5; i++) SansStep(f);
			Say($"    FIGHT trúng: HitAttempts 2 -> {f.HitAttempts} (mong đợi 3), Sans tấn công={f.TestAttacking}");
			ok &= f.HitAttempts == 3 && f.TestAttacking;
		}
		Say($"    -> {(ok ? "OK" : "!! SAI")}");
	}

	// ------------------------------------------------------------------ vật lý trái tim xanh
	private static void TestSansHeartPhysics()
	{
		Say("  -- trái tim xanh: trọng lực, nhảy, bục, slam --");
		var f = new SansFight(9) { God = true };
		f.TestSkipBegin();
		f.TestRunAttack("sans_bonegap1");          // zone 133..508 x 251..391, tim xanh ở (320,376)
		for (int i = 0; i < 90; i++) SansStep(f);
		var (x0, y0, m, _) = f.TestHeart;
		Say($"    đứng yên: tim ({x0:0.0},{y0:0.0}) mode={m} (mong đợi y ~378 = đáy 391 - 5 - 8)");
		bool ok = m == 1 && MathF.Abs(y0 - 378f) < 1.5f;

		float minY = y0;
		f.Pad.Tap(SansBtn.Up, true);
		for (int i = 0; i < 18; i++) { f.Update(SansDt); minY = MathF.Min(minY, f.TestHeart.y); }
		f.Pad.Tap(SansBtn.Up, false);
		for (int i = 0; i < 120; i++) { f.Update(SansDt); minY = MathF.Min(minY, f.TestHeart.y); }
		var (_, y1, _, _) = f.TestHeart;
		Say($"    nhảy giữ Up 0.3s: đỉnh y={minY:0.0} (cao ~{y0 - minY:0}px), sau đó rơi về y={y1:0.0}");
		ok &= (y0 - minY) > 30f && MathF.Abs(y1 - 378f) < 1.5f;

		// bấm nhanh rồi nhả: nhảy thấp hơn
		float minShort = y1;
		SansStep(f, SansBtn.Up);
		for (int i = 0; i < 120; i++) { f.Update(SansDt); minShort = MathF.Min(minShort, f.TestHeart.y); }
		Say($"    nhảy chạm nhanh: cao ~{y1 - minShort:0}px (thấp hơn nhảy giữ phím)");
		ok &= (y1 - minShort) < (y0 - minY) && (y1 - minShort) > 3f;

		// chạy trái bị chặn bởi viền (dùng đòn dài sans_platforms1 để khung giữ nguyên 133..508)
		var walk = new SansFight(9) { God = true };
		walk.TestSkipBegin();
		walk.TestRunAttack("sans_platforms1");
		for (int i = 0; i < 60; i++) walk.Update(SansDt);
		walk.Pad.Tap(SansBtn.Left, true);
		for (int i = 0; i < 180; i++) walk.Update(SansDt);
		walk.Pad.Tap(SansBtn.Left, false);
		Say($"    chạy trái 3s: x={walk.TestHeart.x:0.0} (viền trái 133+5+8=146)");
		ok &= MathF.Abs(walk.TestHeart.x - 146f) < 1.5f;

		// slam xuống rồi lên: rung màn hình + va chạm
		var g = new SansFight(9) { God = true };
		g.TestSkipBegin();
		g.TestRunAttack("sans_bonegap1");
		for (int i = 0; i < 30; i++) SansStep(g);
		g.TestRunAttack("sans_final");
		for (int i = 0; i < 60 * 12; i++) { SansStep(g, SansBotInput(g, i)); }
		Say($"    đòn cuối chạy 12s: tim {g.TestHeart.x:0},{g.TestHeart.y:0} mode={g.TestHeart.mode} xương={g.TestBoneCount} HP={g.Hp:0}");
		{
			// đo thời gian vẽ + mô phỏng ở cảnh đông nhất của đòn cuối
			var h = new SansFight(9) { God = true };
			h.TestSkipBegin();
			h.TestRunAttack("sans_final");
			int heavy = 0; long upd = 0, drw = 0; int frames = 0;
			using var fb = new FrameBuffer((int)SansFight.W, (int)SansFight.H);
			var cv = new Canvas();
			for (int i = 0; i < 60 * 50; i++)
			{
				long t0 = Stopwatch.GetTimestamp();
				h.Update(SansDt);
				upd += Stopwatch.GetTimestamp() - t0;
				if (i % 2 == 0)
				{
					t0 = Stopwatch.GetTimestamp();
					fb.G.Clear(Color.Black); cv.Begin(fb.G, 1f, PointF.Empty, false); h.Draw(cv);
					drw += Stopwatch.GetTimestamp() - t0; frames++;
					heavy = Math.Max(heavy, h.TestBoneCount);
				}
			}
			Say($"    hiệu năng đòn cuối (tối đa {heavy} xương): mô phỏng {upd * 1000.0 / Stopwatch.Frequency / (60 * 50):0.000} ms/bước, vẽ {drw * 1000.0 / Stopwatch.Frequency / frames:0.00} ms/khung (640x480)");
		}
		Say($"    -> {(ok ? "OK" : "!! SAI")}");
	}

	// ------------------------------------------------------------------ xương xanh/cam, bục, slam, KR
	private static void TestSansHazards()
	{
		Say("  -- xương xanh/cam, bục di chuyển, slam, KR --");
		bool ok = true;
		SansFight Arena(string script = "sans_bonegap1")
		{
			var f = new SansFight(2);
			f.TestSkipBegin();
			f.TestRunAttack(script);
			for (int i = 0; i < 40; i++) f.Update(SansDt);      // khung 133..508 x 251..391, tim xanh ở đáy
			f.TestFreeze();                                     // bỏ các vật của kịch bản, chỉ thử vật do mình thêm
			return f;
		}

		// xương XANH đi ngang qua trái tim đứng yên: không đau; trái tim chạy: đau
		foreach (bool moving in new[] { false, true })
			foreach (int color in new[] { 1, 2 })
			{
				var f = Arena();
				float hp0 = f.Hp;
				f.TestAddBone(true, 560, 330, 60, 2, 120, color);          // xương dọc bay từ phải sang trái, cắt qua độ cao của tim
				if (moving) f.Pad.Tap(SansBtn.Right, true);       // giữ phím phải suốt: tim chạy sang phải, gặp xương ở giữa đường
				for (int i = 0; i < 150; i++) f.Update(SansDt);
				f.Pad.Tap(SansBtn.Right, false);
				bool hurt = f.Hp < hp0 || f.TestKr > 0f;
				bool expect = color == 1 ? moving : !moving;     // xanh đau khi di chuyển, cam đau khi đứng yên
				ok &= hurt == expect;
				Say($"    xương {(color == 1 ? "xanh" : "cam ")} + tim {(moving ? "di chuyển" : "đứng yên")}: {(hurt ? "ĐAU" : "không đau")}  (mong đợi {(expect ? "đau" : "không đau")})");
			}
		{
			// xương trắng luôn đau + KR tích lại rồi rút máu dần
			var f = Arena();
			f.TestAddBone(true, 320, 300, 80, 1, 0, 0);               // xương trắng đứng yên đè lên tim
			for (int i = 0; i < 6; i++) f.Update(SansDt);
			float kr = f.TestKr; float hpA = f.Hp;
			for (int i = 0; i < 60 * 3; i++) f.Update(SansDt);
			Say($"    xương trắng: KR sau 0.1s = {kr:0}, máu {f.Hp:0} (đã giảm {92f - f.Hp:0})");
			ok &= kr > 0f && f.Hp < hpA;
		}
		{
			// bục: tim rơi xuống bục đứng yên rồi đứng lên được; bục chạy thì tim đi cùng
			var f = Arena("sans_platforms1");
			f.TestCall("Platform", "300", "340", "80", "0", "0", "0");      // bục đứng yên
			f.TestCall("HeartTeleport", "330", "300");
			for (int i = 0; i < 60; i++) f.Update(SansDt);
			Say($"    rơi xuống bục (mặt bục y=340): tim y={f.TestHeart.y:0.0} (mong đợi ~332)");
			ok &= MathF.Abs(f.TestHeart.y - 332f) < 1.5f;
		}
		{
			var f = Arena("sans_platforms1");
			f.TestCall("Platform", "300", "340", "80", "0", "60", "0");     // bục chạy sang phải 60px/s
			f.TestCall("HeartTeleport", "330", "300");
			for (int i = 0; i < 60; i++) f.Update(SansDt);
			float x1 = f.TestHeart.x;
			for (int i = 0; i < 120; i++) f.Update(SansDt);
			float x2 = f.TestHeart.x;
			Say($"    bục chạy 60px/s: tim x {x1:0.0} -> {x2:0.0} sau 2s (mong đợi tăng ~120)");
			ok &= x2 - x1 > 90f && x2 - x1 < 150f;
		}
		{
			// slam: bị hất xuống/lên/trái/phải đập vào khung -> rung + (khi bật) trừ 1 máu
			var f = Arena("sans_platforms1");
			f.TestCall("SansSlamDamage", "1");
			float hp0 = f.Hp, maxShake = 0f;
			f.TestCall("SansSlam", "3");                           // lên
			for (int i = 0; i < 30; i++) { f.Update(SansDt); maxShake = MathF.Max(maxShake, f.TestShake); }
			Say($"    slam lên: tim y={f.TestHeart.y:0.0} (sát trần 251+5+8=264), máu {hp0:0} -> {f.Hp:0}, rung tối đa {maxShake:0}");
			ok &= MathF.Abs(f.TestHeart.y - 264f) < 1.5f && f.Hp == hp0 - 1f && maxShake > 0f;
		}
		Say($"    -> {(ok ? "OK" : "!! SAI")}");
	}

	// ------------------------------------------------------------------ mở khoá
	private static void TestSansUnlock()
	{
		Say("  -- nhận diện tên 'sans' (mọi kiểu chữ hoa/thường) --");
		bool ok = true;
		foreach (var s in new[] { "sans", "SANS", "SaNs", "sAnS", " Sans ", "sANS" }) ok &= SansEasterEgg.Matches(s);
		foreach (var s in new[] { "san", "sansx", "mèo", "", "s a n s", "sanss" }) ok &= !SansEasterEgg.Matches(s);
		Say($"    -> {(ok ? "OK" : "!! SAI")}");
	}

	// ------------------------------------------------------------------ tích hợp: lưu cài đặt, skin Sans, form chọn nhân vật, cửa sổ trận đánh
	private static void TestSansIntegration()
	{
		Say("  -- tích hợp: cài đặt mở khoá, skin Sans, chọn nhân vật, cửa sổ trận đánh --");
		bool savedUnlock = Settings.SansUnlocked; int savedChar = Settings.Character;
		bool ok = true;
		try
		{
			// 1) cài đặt: lưu/đọc lại cờ mở khoá + nhân vật
			Settings.SansUnlocked = true; Settings.Character = 1; Settings.Save();
			Settings.SansUnlocked = false; Settings.Character = 0;
			Settings.Load(out string err);
			bool rt = err == null && Settings.SansUnlocked && Settings.Character == 1;
			Say($"    lưu/đọc cài đặt: sans={Settings.SansUnlocked} character={Settings.Character} -> {(rt ? "OK" : "!! SAI")}");
			ok &= rt;

			// chưa mở khoá thì luôn là mèo dù character=1
			Settings.SansUnlocked = false; Settings.Character = 1;
			ok &= CharacterSkin.Selected == CharacterSkin.Cat;
			Settings.SansUnlocked = true; Settings.Character = 1;
			ok &= CharacterSkin.Selected == CharacterSkin.Sans;

			// 2) skin Sans nạp được, có ảnh xem trước
			var skin = CharacterSkin.Load(CharacterSkin.Sans);
			using var prev = CharacterSkin.Preview(CharacterSkin.Sans, 3);
			Say($"    skin: {skin.DisplayName} sheet {skin.Sheet.Width}x{skin.Sheet.Height} khung {skin.FrameSize}, xem trước {(prev == null ? "KHÔNG CÓ" : prev.Width + "x" + prev.Height)}");
			ok &= skin.Id == CharacterSkin.Sans && skin.Sheet.Width == 5 * skin.FrameSize && prev != null;

			// 3) ván chơi bằng Sans: đi, đứng yên, trúng đòn - chụp ảnh so với mèo
			foreach (int ch in new[] { 0, 1 })
			{
				Settings.Character = ch;
				var w = new World();
				w.Player.God = false;
				w.Input.KeyDown(Keys.D);
				for (int i = 0; i < 40; i++) w.Update(1f / 60f);
				Shot(w, ch == 1 ? "sans_player_walk" : "cat_player_walk", 3f);
				w.Input.Clear();
				for (int i = 0; i < 20; i++) w.Update(1f / 60f);
				Shot(w, ch == 1 ? "sans_player_idle" : "cat_player_idle", 3f);
			}
			Settings.Character = 1;
			{
				var w = new World();
				for (int i = 0; i < 10; i++) w.Update(1f / 60f);
				w.Player.TakeDamage(5f, Vector2.UnitX, 100f);
				for (int i = 0; i < 6; i++) w.Update(1f / 60f);
				Shot(w, "sans_player_hit", 3f);
				w.Player.TakeDamage(9999f, Vector2.UnitX, 100f);
				for (int i = 0; i < 20; i++) w.Update(1f / 60f);
				Shot(w, "sans_player_dead", 3f);
				Say($"    Sans bị đánh/chết: chết={w.Player.Dead}");
			}

			// 4) form chọn nhân vật
			using (var pick = new CharacterPickForm())
			{
				pick.StartPosition = FormStartPosition.Manual; pick.Location = new Point(40, 40);
				pick.Show(); Application.DoEvents();
				using var bmp = new Bitmap(pick.Width, pick.Height);
				pick.DrawToBitmap(bmp, new Rectangle(0, 0, pick.Width, pick.Height));
				bmp.Save(Path.Combine(_out, "form_charpick.png"));
				pick.Pick(CharacterSkin.Sans);
				ok &= pick.Chosen == CharacterSkin.Sans;
			}
			CharacterPick.Apply(CharacterSkin.Cat);
			ok &= Settings.Character == 0;
			Settings.SansUnlocked = false;
			CharacterPick.Apply(CharacterSkin.Sans);
			ok &= Settings.Character == 0;   // chưa mở khoá: ép về mèo

			// 5) cửa sổ trận đánh chạy thật (Timer + vẽ), bấm Z vài lần rồi đóng
			Settings.SansUnlocked = true;
			using (var form = new SansFightForm())
			{
				form.StartPosition = FormStartPosition.Manual; form.Location = new Point(20, 20);
				var tm = new System.Windows.Forms.Timer { Interval = 300 };
				int n = 0;
				tm.Tick += (_, _) =>
				{
					n++;
					form.Fight.Pad.Tap(SansBtn.Confirm, n % 2 == 0);
					if (n == 8) { using var bmp = new Bitmap(form.ClientSize.Width, form.ClientSize.Height); form.DrawToBitmap(bmp, new Rectangle(0, 0, form.Width, form.Height)); bmp.Save(Path.Combine(_out, "form_sansfight_live.png")); }
					if (n >= 12) { tm.Stop(); form.Close(); }
				};
				form.Shown += (_, _) => tm.Start();
				form.ShowDialog();
				Say($"    cửa sổ trận đánh: chạy {form.Fight.Time:0.0}s mô phỏng trong ~3.6s thực, kết quả={form.Result} (Running = bị đóng giữa chừng)");
				ok &= form.Fight.Time > 2f;
			}
		}
		finally
		{
			Settings.SansUnlocked = savedUnlock; Settings.Character = savedChar; Settings.Save();
			HudBars.Reload();
		}
		Say($"    -> {(ok ? "OK" : "!! SAI")}");
	}

	// ------------------------------------------------------------------ đầu-cuối: màn kết thúc -> nhập tên SaNs -> trận Sans -> mở khoá
	private static void TestSansEndToEnd()
	{
		Say("  -- đầu-cuối: lưu thành tích tên 'SaNs' -> mở trận Sans -> thắng thì mở khoá --");
		string tmp = Path.Combine(Path.GetTempPath(), "catsurvivor_sans_e2e.txt");
		if (File.Exists(tmp)) File.Delete(tmp);
		var savedOverride = Storage.ScoreFileOverride;
		bool savedUnlock = Settings.SansUnlocked; int savedChar = Settings.Character;
		var oldNotify = SansEasterEgg.Notify;
		var notes = new List<string>();
		bool ok = true;
		try
		{
			Storage.ScoreFileOverride = tmp;
			SansEasterEgg.Notify = (_, _, caption) => notes.Add(caption);
			Settings.SansUnlocked = false;
			var w = new World();
			using var over = new GameOverForm(w.State, false);
			var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
			var txt = (TextBox)typeof(GameOverForm).GetField("txtName", flags).GetValue(over);
			txt.Text = "SaNs";
			over.StartPosition = FormStartPosition.Manual; over.Location = new Point(30, 30);
			over.Show(); Application.DoEvents();

			bool sawFight = false;
			var tm = new System.Windows.Forms.Timer { Interval = 250 };
			tm.Tick += (_, _) =>
			{
				foreach (Form f in Application.OpenForms.Cast<Form>().ToArray())
				{
					if (f is SansFightForm sf) { sawFight = true; if (sf.Fight.Time > 1f) sf.Close(); }   // bỏ cuộc giữa chừng (kết quả Running)
					else if (f is LeaderboardForm lf) lf.Close();
				}
			};
			tm.Start();
			typeof(GameOverForm).GetMethod("Save", flags).Invoke(over, null);
			tm.Stop();

			string saved = File.Exists(tmp) ? File.ReadAllText(tmp) : "";
			Say($"    đã mở cửa sổ trận Sans: {sawFight}; thành tích đã lưu: {saved.Split('|')[0]}; thông báo sau khi bỏ cuộc: {notes.Count}");
			ok &= sawFight && saved.StartsWith("SaNs|") && notes.Count == 0 && !Settings.SansUnlocked;

			// thua: có thông báo, chưa mở khoá; thắng: mở khoá + lưu cài đặt; thắng lần nữa: thông báo "đã mở khoá từ trước"
			SansEasterEgg.Finish(over, SansFight.Outcome.Lost);
			ok &= notes.Count == 1 && notes[0] == "* Bad Time" && !Settings.SansUnlocked;
			SansEasterEgg.Finish(over, SansFight.Outcome.Won);
			ok &= Settings.SansUnlocked && notes.Count == 2 && notes[1] == "* You win.";
			Settings.SansUnlocked = false; Settings.Character = 0;
			Settings.Load(out _);
			ok &= Settings.SansUnlocked;      // đã được ghi xuống settings.txt
			Say($"    thắng -> mở khoá={Settings.SansUnlocked} (đọc lại từ settings.txt); thông báo: {string.Join(" / ", notes)}");
			over.Close();
		}
		finally
		{
			Storage.ScoreFileOverride = savedOverride;
			SansEasterEgg.Notify = oldNotify;
			Settings.SansUnlocked = savedUnlock; Settings.Character = savedChar; Settings.Save();
			if (File.Exists(tmp)) File.Delete(tmp);
		}
		Say($"    -> {(ok ? "OK" : "!! SAI")}");
	}
}
