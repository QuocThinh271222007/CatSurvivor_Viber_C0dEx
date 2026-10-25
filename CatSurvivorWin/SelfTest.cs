using System.Diagnostics;
using System.Numerics;
using System.Text;

namespace CatSurvivor;

/// <summary>
/// Chế độ tự kiểm tra (chạy: CatSurvivor.exe --selftest [--out thư_mục]): mô phỏng game không cần người chơi,
/// kiểm tra các hệ thống chính, chụp ảnh màn hình ra PNG và ghi kết quả vào selftest.log.
/// Dùng khi phát triển để chắc chắn bản port chạy đúng; người chơi bình thường không cần dùng.
/// </summary>
internal static partial class SelfTest
{
	private static readonly StringBuilder Log = new();
	private static string _out;

	private static void Say(string s) => Log.AppendLine(s);

	public static void Run(string[] args)
	{
		int i = Array.IndexOf(args, "--out");
		_out = i >= 0 && i + 1 < args.Length ? args[i + 1] : AppContext.BaseDirectory;
		Directory.CreateDirectory(_out);

		try
		{
			if (args.Contains("--autoaim")) { TestAutoAim(); File.WriteAllText(Path.Combine(_out, "selftest.log"), Log.ToString()); return; }
			if (args.Contains("--death")) { TestDeathAndScore(); File.WriteAllText(Path.Combine(_out, "selftest.log"), Log.ToString()); return; }
			if (args.Contains("--prof")) { TestProfile(); File.WriteAllText(Path.Combine(_out, "selftest.log"), Log.ToString()); return; }
			if (args.Contains("--lowhp")) { TestLowHp(); File.WriteAllText(Path.Combine(_out, "selftest.log"), Log.ToString()); return; }
			if (args.Contains("--level")) { TestLevelScaling(); File.WriteAllText(Path.Combine(_out, "selftest.log"), Log.ToString()); return; }
			if (args.Contains("--difficulty")) { TestDifficulty(); File.WriteAllText(Path.Combine(_out, "selftest.log"), Log.ToString()); return; }
			if (args.Contains("--bossbrain")) { TestBossBrain(); File.WriteAllText(Path.Combine(_out, "selftest.log"), Log.ToString()); return; }
			if (args.Contains("--skills")) { TestSkills(); File.WriteAllText(Path.Combine(_out, "selftest.log"), Log.ToString()); return; }
			if (args.Contains("--sound")) { TestSound(); File.WriteAllText(Path.Combine(_out, "selftest.log"), Log.ToString()); return; }
			if (args.Contains("--bomber")) { TestBomberChase(); TestBomber(); File.WriteAllText(Path.Combine(_out, "selftest.log"), Log.ToString()); return; }
			if (args.Contains("--font")) { TestFont(); File.WriteAllText(Path.Combine(_out, "selftest.log"), Log.ToString()); return; }
			if (args.Contains("--present")) { TestPresent(); File.WriteAllText(Path.Combine(_out, "selftest.log"), Log.ToString()); return; }
			if (args.Contains("--hitbox")) { TestHitbox(); File.WriteAllText(Path.Combine(_out, "selftest.log"), Log.ToString()); return; }
			if (args.Contains("--phases")) { TestPhases(); File.WriteAllText(Path.Combine(_out, "selftest.log"), Log.ToString()); return; }
			if (args.Contains("--brute")) { TestBrute(); File.WriteAllText(Path.Combine(_out, "selftest.log"), Log.ToString()); return; }
			if (args.Contains("--sans")) { TestSansFight(); File.WriteAllText(Path.Combine(_out, "selftest.log"), Log.ToString()); return; }
			if (args.Contains("--eyefx")) { TestEyeFx(); File.WriteAllText(Path.Combine(_out, "selftest.log"), Log.ToString()); return; }
			if (args.Contains("--eyes")) { TestMiniEyes(); File.WriteAllText(Path.Combine(_out, "selftest.log"), Log.ToString()); return; }
			if (args.Contains("--pick")) { TestSkillPick(); File.WriteAllText(Path.Combine(_out, "selftest.log"), Log.ToString()); return; }
			if (args.Contains("--armor")) { TestArmorArt(); File.WriteAllText(Path.Combine(_out, "selftest.log"), Log.ToString()); return; }
			if (args.Contains("--shield")) { TestHolyShield(); File.WriteAllText(Path.Combine(_out, "selftest.log"), Log.ToString()); return; }
			if (args.Contains("--weapons")) { TestWeaponArt(); File.WriteAllText(Path.Combine(_out, "selftest.log"), Log.ToString()); return; }
			if (args.Contains("--bench")) { Bench(); File.WriteAllText(Path.Combine(_out, "selftest.log"), Log.ToString()); return; }
			if (args.Contains("--cheats")) { TestSpawnDensity(); TestFireRate(); TestUpgradeOdds(); TestAmbientXp(); TestCheats(); TestSettings(); File.WriteAllText(Path.Combine(_out, "selftest.log"), Log.ToString()); return; }
			if (args.Contains("--forms")) { TestForms(); File.WriteAllText(Path.Combine(_out, "selftest.log"), Log.ToString()); return; }
			TestSpawnDensity();
			TestSimulation();
			TestBossFlow();
			TestDeathAndScore();
			TestStorage();
			TestSound();
			TestSettings();
			TestForms();
			TestGameFormLoop();
		}
		catch (Exception ex)
		{
			Say("!! EXCEPTION: " + ex);
		}
		File.WriteAllText(Path.Combine(_out, "selftest.log"), Log.ToString());
	}

	// ------------------------------------------------------------------ helpers
	private static void Shot(World w, string name, float scale = 2f)
	{
		int W = (int)(Balance.ViewWidth * scale), H = (int)(Balance.ViewHeight * scale);
		using var bmp = new Bitmap(W, H);
		using var g = Graphics.FromImage(bmp);
		g.Clear(Color.Black);
		var canvas = new Canvas();
		using var fb = new FrameBuffer((int)Balance.ViewWidth, (int)Balance.ViewHeight);
		fb.G.Clear(Color.Black);
		canvas.Begin(fb.G, 1f, PointF.Empty, false);
		w.DrawWorld(canvas);
		w.DrawOverlay(canvas);
		fb.BlitTo(g, new Rectangle(0, 0, W, H));
		bmp.Save(Path.Combine(_out, name + ".png"));
	}

	private static void ApplyUpgrades(World w)
	{
		while (w.PendingLevelUps > 0)
		{
			var choices = Upgrades.BuildChoices(w.Player);
			if (choices.Count == 0) throw new Exception("không có lựa chọn nâng cấp");
			choices[(int)(Rng.Randi() % (uint)choices.Count)].Apply();
			w.PendingLevelUps--;
		}
	}

	private static void Bot(World w, float t)
	{
		// đổi hướng chạy mỗi 1.5 giây theo chu kỳ 8 hướng; thỉnh thoảng dash
		var dirs = new[] { Keys.D, Keys.S, Keys.A, Keys.W };
		int phase = (int)(t / 1.5f);
		w.Input.Clear();
		w.Input.KeyDown(dirs[phase % 4]);
		if (phase % 3 == 0) w.Input.KeyDown(dirs[(phase + 1) % 4]);
		if ((int)(t * 2) % 11 == 0) { w.Input.KeyDown(Keys.Space); w.Input.KeyUp(Keys.Space); }
		w.Input.MouseView = new Vector2(320f + 150f * MathF.Cos(t), 180f + 100f * MathF.Sin(t));
	}

	// ------------------------------------------------------------------ tự động ngắm / ngắm bằng chuột
	private static void TestAutoAim()
	{
		Say("== AUTO-AIM (sát thương lên bia đứng yên cách 110px bên phải trong 8 giây; chuột chỉ vào bia hoặc chỉ ngược lại)");
		bool saved = Settings.AutoAim;
		double Run(string weapon, bool autoAim, bool mouseOnDummy)
		{
			Settings.AutoAim = autoAim;
			var w = new World();
			w.Player.God = true; w.Player.Speed = 0f;
			w.Player.Weapons.Clear();
			Cheats.Execute(w, "weapon " + weapon + (weapon == "claw" ? " 3" : " 5")); // claw cấp 3: chưa có nhát phía sau lưng
			float dist = weapon == "claw" ? 62f : 110f; // vuốt tầm ngắn: đặt bia gần hơn
			var dummy = EnemyTypes.Create(EnemyTypes.Brute, 0f, w.Player.Pos + new Vector2(dist, 0), 1.2f, 0);
			dummy.MaxHp = dummy.Hp = 1e5f; dummy.Armor = 0f; dummy.Speed = 0f;
			w.Enemies.Enemies.Clear(); w.Enemies.Enemies.Add(dummy);
			w.Input.MouseView = new Vector2(320f + (mouseOnDummy ? dist : -dist), 180f);
			for (int i = 0; i < 8 * 60; i++)
			{
				dummy.Pos = w.Player.Pos + new Vector2(dist, 0);
				w.Enemies.Enemies.RemoveAll(e => e != dummy); // chỉ giữ bia (không để quái sinh thêm làm nhiễu)
				w.Update(1f / 60f);
				w.Player.Hp = w.Player.MaxHp;
			}
			return 1e5f - dummy.Hp;
		}
		foreach (var weapon in new[] { "bow", "claw", "cross" })
		{
			double a1 = Run(weapon, true, true), a2 = Run(weapon, true, false), m1 = Run(weapon, false, true), m2 = Run(weapon, false, false);
			Say($"  {weapon,-6}: TỰ ĐỘNG NGẮM bật: chuột vào bia={a1,5:F0} / chuột ngược={a2,5:F0} (mong đợi cả hai > 0)  ||  TẮT (ngắm chuột): chuột vào bia={m1,5:F0} (> 0) / chuột ngược={m2,5:F0} (mong đợi 0)");
		}
		Settings.AutoAim = saved;
	}
	// ------------------------------------------------------------------ đo từng phần của một khung hình (cấp 27, đông quái, đủ vũ khí)
	private static void TestProfile()
	{
		Say("== PROFILE (cấp 27, 90s, mèo đứng yên bị vây, đủ vũ khí; trung bình ms/khung từ giây 45)");
		var w = new World();
		Cheats.Execute(w, "level 27"); w.State.BossActive = false;
		w.Player.Weapons.Clear(); // không vũ khí: quái dồn quanh mèo để đo trường hợp đông nhất
		w.Player.God = true; w.Player.Speed = 0f;
		const float dt = 1f / 60f;
		using var screen = new Bitmap(1920, 1080); using var sg = Graphics.FromImage(screen);
		using var fb = new FrameBuffer(640, 360);
		var cv = new Canvas();
		var acc = new Dictionary<string, double>(); int frames = 0, enemies = 0;
		double T(long a) => (Stopwatch.GetTimestamp() - a) * 1000.0 / Stopwatch.Frequency;
		void Add(string k, double ms) { acc[k] = acc.GetValueOrDefault(k) + ms; }

		for (int step = 1; step <= 90 * 60; step++)
		{
			long t0 = Stopwatch.GetTimestamp();
			w.Update(dt);
			if (w.State.BossActive) w.State.BossActive = false;
			w.Player.Hp = w.Player.MaxHp;
			double upd = T(t0);
			if (step < 45 * 60 || step % 3 != 0) continue;

			Add("1 cập nhật logic", upd);
			var view = w.Cam - World.ViewSize / 2f;
			fb.G.Clear(Color.Black);
			cv.Begin(fb.G, 1f, PointF.Empty, false);
			cv.WorldMode(view);
			long a = Stopwatch.GetTimestamp(); w.Pickups.Draw(cv); Add("2 nền+XP", T(a));   // (nền tính chung trong DrawWorld bên dưới)
			a = Stopwatch.GetTimestamp(); w.Enemies.PrepareDraw(view, World.ViewSize); Add("3 chuẩn bị/sắp xếp quái", T(a));
			a = Stopwatch.GetTimestamp(); w.Enemies.DrawBack(cv); w.Enemies.DrawFront(cv); Add("4 vẽ quái", T(a));
			a = Stopwatch.GetTimestamp(); w.Player.Draw(cv); w.Projectiles.Draw(cv); Add("5 mèo+đạn", T(a));
			a = Stopwatch.GetTimestamp(); w.Player.DrawWeapons(cv); Add("6 hiệu ứng vũ khí", T(a));
			a = Stopwatch.GetTimestamp(); cv.Begin(fb.G, 1f, PointF.Empty, false); w.DrawOverlay(cv); Add("8 HUD (chữ, vẽ ở độ phân giải gốc)", T(a));
			a = Stopwatch.GetTimestamp(); fb.BlitTo(sg, new Rectangle(0, 0, 1920, 1080)); Add("7 phóng to 1080p (StretchBlt)", T(a));
			a = Stopwatch.GetTimestamp(); fb.G.Clear(Color.Black); cv.Begin(fb.G, 1f, PointF.Empty, false); w.DrawWorld(cv); Add("9 DrawWorld nguyên khối (tham chiếu)", T(a));
			frames++; enemies += w.Enemies.Enemies.Count;
		}
		Say($"  số quái TB {enemies / Math.Max(1, frames)}");
		foreach (var kv in acc.OrderBy(k => k.Key)) Say($"  {kv.Key,-40} {kv.Value / Math.Max(1, frames),6:F2} ms");
	}

	// ------------------------------------------------------------------ viền đỏ khi gần hết máu
	private static void TestLowHp()
	{
		Say("== LOW HP VIGNETTE (ảnh ở máu 50% / 30% / 10%, hít vào & thở ra)");
		var w = new World();
		w.Player.God = true;
		foreach (var (ratio, name) in new[] { (0.5f, "lowhp_50"), (0.30f, "lowhp_30"), (0.10f, "lowhp_10") })
		{
			w.Player.Hp = w.Player.MaxHp * ratio;
			Shot(w, name, 1.5f);
		}
		// hai pha của nhịp thở ở máu 10%
		w.Player.Hp = w.Player.MaxHp * 0.10f;
		for (int i = 0; i < 6; i++) { Shot(w, $"lowhp_breath{i}", 1f); System.Threading.Thread.Sleep(170); }
		Say("  đã lưu ảnh");
	}

	// ------------------------------------------------------------------ quái mạnh/đông theo cấp nhân vật
	private static void TestLevelScaling()
	{
		Say("== LEVEL SCALING (mèo đứng yên, không vũ khí, bất tử; cùng thời gian chơi, khác cấp)");
		foreach (int level in new[] { 1, 10, 20, 27 })
		{
			var w = new World();
			if (level > 1)
			{
				Cheats.Execute(w, $"level {level}");
				w.Arena.Update(0f);
				w.State.BossActive = false;
			}
			w.Player.Weapons.Clear();
			float diff0 = w.Enemies.DiffTime;
			const float dt = 1f / 60f;
			var rows = new List<string>();
			double drawMs = 0; int draws = 0;
			using var bmp = new Bitmap(1280, 720); using var gfx = Graphics.FromImage(bmp); var cv = new Canvas(); using var fb = new FrameBuffer(640, 360);
			for (int step = 1; step <= 90 * 60; step++)
			{
				w.Update(dt);
				w.Player.Hp = w.Player.MaxHp;
				if (w.State.BossActive) { w.State.BossActive = false; }
				if (step > 45 * 60 && step % 6 == 0)
				{
					long d0 = Stopwatch.GetTimestamp();
					gfx.Clear(Color.Black); fb.G.Clear(Color.Black); cv.Begin(fb.G, 1f, PointF.Empty, false); w.DrawWorld(cv);
					w.DrawOverlay(cv); fb.BlitTo(gfx, new Rectangle(0, 0, 1280, 720));
					drawMs += (Stopwatch.GetTimestamp() - d0) * 1000.0 / Stopwatch.Frequency; draws++;
				}
				if (step == 30 * 60 || step == 90 * 60)
				{
					var list = w.Enemies.Enemies.Where(e => e.Kind != EnemyTypes.Boss && !e.Dead).ToList();
					rows.Add($"t={step / 60}s: {list.Count} quái, máu TB {(list.Count > 0 ? list.Average(e => e.MaxHp) : 0):F0}, sát thương TB {(list.Count > 0 ? list.Average(e => e.Damage) : 0):F1}, giáp TB {(list.Count > 0 ? list.Average(e => e.Armor) : 0):F1}, tốc độ TB {(list.Count > 0 ? list.Average(e => e.Speed) : 0):F0}");
				}
			}
			Say($"  cấp {level} (độ khó hiệu dụng bắt đầu {diff0:F0}s): " + string.Join(" | ", rows) + $" | vẽ TB {drawMs / Math.Max(1, draws):F1} ms/khung");
		}
	}

	// ------------------------------------------------------------------ ảnh chụp vũ khí có sprite
	private static void TestWeaponArt()
	{
		Say("== WEAPON ART (ảnh crop quanh mèo, phóng 4x)");
		var w = new World();
		const float dt = 1f / 60f;
		w.Player.God = true;
		Say("  " + Cheats.Execute(w, "weapon all 5").Message);
		Cheats.Execute(w, "spawn slime 6");
		Say($"  sprite: bow={(WeaponArt.Bow != null)} knife={(WeaponArt.Knife != null)} claw={(WeaponArt.Claw != null)}");

		void Crop(string name)
		{
			const float scale = 4f;
			using var bmp = new Bitmap((int)(160 * scale), (int)(140 * scale));
			using var g = Graphics.FromImage(bmp);
			using var fb = new FrameBuffer((int)Balance.ViewWidth, (int)Balance.ViewHeight);
			fb.G.Clear(Color.Black);
			var canvas = new Canvas();
			canvas.Begin(fb.G, 1f, PointF.Empty, false);
			w.DrawWorld(canvas);
			g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
			g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
			g.DrawImage(fb.Bitmap, new Rectangle(0, 0, bmp.Width, bmp.Height), new Rectangle(240, 110, 160, 140), GraphicsUnit.Pixel);
			bmp.Save(Path.Combine(_out, name + ".png"));
		}

		int saved = 0;
		for (int step = 0; step < 60 * 6 && saved < 6; step++)
		{
			var a = new Vector2(120f * MathF.Cos(step * 0.02f), 0f);
			w.Input.MouseView = new Vector2(320f + 100f * MathF.Cos(step * 0.03f), 180f + 100f * MathF.Sin(step * 0.03f));
			w.Update(dt);
			w.Player.Hp = w.Player.MaxHp;
			if (step == 80 || step == 83 || step == 90 || step == 100 || step == 120 || step == 150) Crop($"weapon_{saved++}");
		}
		Say($"  đã lưu {saved} ảnh");

		// Vuốt: bia đứng bên TRÁI và bên PHẢI để kiểm tra vuốt không bị lật ngược khi quay trái
		foreach (var (dx, tag) in new[] { (-55f, "L"), (55f, "R") })
		{
			var w3 = new World();
			w3.Player.God = true; w3.Player.Speed = 0f;
			w3.Player.Weapons.Clear();
			Cheats.Execute(w3, "weapon claw 1");
			var t3 = EnemyTypes.Create(EnemyTypes.Brute, 0f, w3.Player.Pos + new Vector2(dx, 0), 1f, 0);
			t3.MaxHp = t3.Hp = 1e5f; t3.Armor = 0f; t3.Speed = 0f;
			w3.Enemies.Enemies.Clear(); w3.Enemies.Enemies.Add(t3);
			w3.Input.MouseView = new Vector2(320f + dx * 2f, 180f); // Claw luôn ngắm bằng chuột: chỉ chuột về phía bia
			int shots = 0;
			for (int step = 0; step < 200 && shots < 6; step++)
			{
				t3.Pos = w3.Player.Pos + new Vector2(dx, 0);
				w3.Enemies.Enemies.RemoveAll(e => e != t3);
				w3.Update(dt);
				w3.Player.Hp = w3.Player.MaxHp;
				if (step == 2 || step == 6 || step == 9 || step == 11 || step == 13 || step == 16) { CropOf(w3, $"claw_{tag}_{shots++}"); }
			}
		}

		// Hitbox: bật Hiện hitbox, đủ loại vũ khí + vài quái quanh mèo
		bool hb = Settings.ShowHitbox;
		Settings.ShowHitbox = true;
		var w4 = new World();
		w4.Player.God = true; w4.Player.Speed = 0f;
		Cheats.Execute(w4, "weapon all 5");
		Cheats.Execute(w4, "spawn slime 4");
		Cheats.Execute(w4, "spawn bat 2");
		for (int step = 0; step < 90; step++) { w4.Update(dt); w4.Player.Hp = w4.Player.MaxHp; if (step == 20 || step == 40) CropOf(w4, $"hitbox_{step / 20 - 1}"); }
		Settings.ShowHitbox = hb;

		// Cung riêng lẻ: mục tiêu trâu bò đứng yên bên phải-trên để thấy cung kéo dây + mũi tên bay
		var w2 = new World();
		w2.Player.God = true;
		var target = EnemyTypes.Create(EnemyTypes.Brute, 0f, w2.Player.Pos + new Vector2(110, -35), 2f, 0);
		target.MaxHp = target.Hp = 1e9f; target.Armor = 0f; target.Speed = 0f;
		w2.Enemies.Enemies.Add(target);
		w2.Player.Speed = 0f;
		for (int step = 0; step < 150; step++)
		{
			target.Pos = w2.Player.Pos + new Vector2(110, -35);
			w2.Update(dt);
			w2.Player.Hp = w2.Player.MaxHp;
			if (step >= 60 && step % 9 == 0 && step <= 138) CropOf(w2, $"bow_{(step - 60) / 9}");
		}
	}

	private static void CropOf(World w, string name)
	{
		const float scale = 4f;
		using var bmp = new Bitmap((int)(160 * scale), (int)(140 * scale));
		using var g = Graphics.FromImage(bmp);
		using var fb = new FrameBuffer((int)Balance.ViewWidth, (int)Balance.ViewHeight);
		fb.G.Clear(Color.Black);
		var canvas = new Canvas();
		canvas.Begin(fb.G, 1f, PointF.Empty, false);
		w.DrawWorld(canvas);
		g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
		g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
		g.DrawImage(fb.Bitmap, new Rectangle(0, 0, bmp.Width, bmp.Height), new Rectangle(240, 110, 160, 140), GraphicsUnit.Pixel);
		bmp.Save(Path.Combine(_out, name + ".png"));
	}

	// ------------------------------------------------------------------ đo tốc độ các cách vẽ ảnh GDI+
	private static void Bench()
	{
		Say("== BENCH vẽ 140 sprite/khung (1280x720, tỉ lệ x2)");
		var tex = AssetStore.Image("enemies/slime.png");
		using var bmp = new Bitmap(1280, 720);
		using var g = Graphics.FromImage(bmp);
		var canvas = new Canvas();
		var rnd = new Random(1);
		var pos = Enumerable.Range(0, 140).Select(_ => new PointF(rnd.Next(0, 560), rnd.Next(0, 300))).ToArray();
		var attrsWrap = new System.Drawing.Imaging.ImageAttributes(); attrsWrap.SetWrapMode(System.Drawing.Drawing2D.WrapMode.TileFlipXY);

		double Time(string name, Action<Graphics, PointF> draw, Action setup = null)
		{
			g.ResetTransform(); g.ScaleTransform(2f, 2f);
			g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor; g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
			g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighSpeed;
			setup?.Invoke();
			for (int w = 0; w < 5; w++) foreach (var pt in pos) draw(g, pt);
			long t0 = Stopwatch.GetTimestamp();
			for (int f = 0; f < 30; f++) foreach (var pt in pos) draw(g, pt);
			double ms = (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency / 30;
			Say($"  {name,-58} {ms,6:F2} ms/khung");
			return ms;
		}

		var pts = new PointF[3];
		Time("A: PointF[3] + ImageAttributes(wrap)", (gg, q) => { pts[0] = q; pts[1] = new PointF(q.X + 64, q.Y); pts[2] = new PointF(q.X, q.Y + 64); gg.DrawImage(tex, pts, new RectangleF(0, 0, 64, 64), GraphicsUnit.Pixel, attrsWrap); });
		Time("B: PointF[3], không attributes", (gg, q) => { pts[0] = q; pts[1] = new PointF(q.X + 64, q.Y); pts[2] = new PointF(q.X, q.Y + 64); gg.DrawImage(tex, pts, new RectangleF(0, 0, 64, 64), GraphicsUnit.Pixel); });
		Time("C: Rectangle int + attributes", (gg, q) => gg.DrawImage(tex, new Rectangle((int)q.X, (int)q.Y, 64, 64), 0, 0, 64, 64, GraphicsUnit.Pixel, attrsWrap));
		Time("D: Rectangle int, không attributes", (gg, q) => gg.DrawImage(tex, new Rectangle((int)q.X, (int)q.Y, 64, 64), 0, 0, 64, 64, GraphicsUnit.Pixel));
		Time("E: RectangleF dest + src, không attributes", (gg, q) => gg.DrawImage(tex, new RectangleF(q.X, q.Y, 64, 64), new RectangleF(0, 0, 64, 64), GraphicsUnit.Pixel));
		Time("F: như D nhưng Interpolation=Default (bilinear)", (gg, q) => gg.DrawImage(tex, new Rectangle((int)q.X, (int)q.Y, 64, 64), 0, 0, 64, 64, GraphicsUnit.Pixel), () => g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.Bilinear);
		Time("G: D + PixelOffset None", (gg, q) => gg.DrawImage(tex, new Rectangle((int)q.X, (int)q.Y, 64, 64), 0, 0, 64, 64, GraphicsUnit.Pixel), () => g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.None);
		Time("H: bóng FillEllipse AA", (gg, q) => { gg.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias; using var br = new SolidBrush(Color.FromArgb(70, 0, 0, 0)); gg.FillEllipse(br, q.X, q.Y, 20, 8); });
		Time("I: bóng FillEllipse không AA", (gg, q) => { gg.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None; using var br = new SolidBrush(Color.FromArgb(70, 0, 0, 0)); gg.FillEllipse(br, q.X, q.Y, 20, 8); });
		// bitmap Format32bppArgb thường (không premultiplied) để so sánh
		using var plain = new Bitmap(tex.Width, tex.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
		using (var gg2 = Graphics.FromImage(plain)) { gg2.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy; gg2.DrawImage(tex, 0, 0, tex.Width, tex.Height); }
		Time("J: D nhưng ảnh Format32bppArgb (không PArgb)", (gg, q) => gg.DrawImage(plain, new Rectangle((int)q.X, (int)q.Y, 64, 64), 0, 0, 64, 64, GraphicsUnit.Pixel));
	}

	// ------------------------------------------------------------------ tốc độ bắn
	private static void TestFireRate()
	{
		Say("== FIRE RATE (đếm số lần Magic Bolt bắn trong 10 giây, mục tiêu đứng gần)");
		int Shots(float globalRate, float weaponRate)
		{
			var w = new World();
			w.Player.God = true;
			w.Player.FireRate = globalRate;
			w.Player.Weapons[0].RateMul = weaponRate;
			var cdField = typeof(MagicBolt).GetField("_cd", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
			int shots = 0; float prev = 0f;
			var target = EnemyTypes.Create(EnemyTypes.Brute, 0f, w.Player.Pos + new Vector2(120, 0), 2f, 0);
			target.MaxHp = target.Hp = 1e9f; target.Armor = 0f;
			w.Enemies.Enemies.Add(target);
			for (int i = 0; i < 600; i++)
			{
				target.Pos = w.Player.Pos + new Vector2(120, 0); // giữ mục tiêu đứng yên trong tầm
				w.Update(1f / 60f);
				float cd = (float)cdField.GetValue(w.Player.Weapons[0]);
				if (cd > prev + 0.05f) shots++;
				prev = cd;
			}
			return shots;
		}
		int a = Shots(1f, 1f), b = Shots(2f, 1f), c = Shots(1f, 2f), d = Shots(2f, 2f);
		Say($"  x1: {a} lần · chung x2: {b} lần · riêng x2: {c} lần · chung x2 + riêng x2 (trần tổng x{Balance.FireRateTotalMax}): {d} lần");
	}

	// ------------------------------------------------------------------ tỉ lệ nâng cấp vũ khí
	private static void TestUpgradeOdds()
	{
		Say("== UPGRADE ODDS (2000 lần rút ở cấp 1)");
		var w = new World();
		int weaponChoices = 0, total = 0, setsWithWeapon = 0, sets = 2000;
		for (int i = 0; i < sets; i++)
		{
			var c = Upgrades.BuildChoices(w.Player);
			if (c.Select(x => x.Title).Distinct().Count() != c.Count) Say("  !! lựa chọn bị lặp");
			bool any = false;
			foreach (var u in c) { total++; if (u.IsWeapon) { weaponChoices++; any = true; } }
			if (any) setsWithWeapon++;
		}
		Say($"  {weaponChoices * 100.0 / total:F0}% lựa chọn là vũ khí; {setsWithWeapon * 100.0 / sets:F1}% lần rút có ít nhất 1 vũ khí (mong đợi 100%)");
	}

	// ------------------------------------------------------------------ XP tự sinh
	private static void TestAmbientXp()
	{
		Say("== AMBIENT XP (mèo đứng yên 60s, không vũ khí)");
		var w = new World();
		w.Player.Weapons.Clear();
		w.Player.MagnetRadius = 0f;
		w.Player.Hp = w.Player.MaxHp;
		var alive = new List<int>();
		for (int step = 1; step <= 60 * 60; step++)
		{
			w.Update(1f / 60f);
			w.Player.Hp = w.Player.MaxHp;
			if (step % (10 * 60) == 0) alive.Add(w.Pickups.AmbientAlive);
		}
		Say($"  số viên XP tự sinh đang nằm trên map mỗi 10s: {string.Join(", ", alive)}; đã sinh trong phút này: {w.Pickups.AmbientSpawnedThisMinute} (trần {Balance.AmbientPerMinute}/phút, {Balance.AmbientMaxAlive} cùng lúc)");
	}

	// ------------------------------------------------------------------ cheat
	private static void TestCheats()
	{
		Say($"== CHEATS (Enabled={Cheats.Enabled})");
		var w = new World();
		const float dt = 1f / 60f;
		void Do(string cmd, int frames = 30)
		{
			var r = Cheats.Execute(w, cmd);
			for (int i = 0; i < frames && !w.IsEnded; i++) { w.Update(dt); w.Player.Hp = Math.Max(w.Player.Hp, 1f); }
			while (w.PendingLevelUps > 0) { Upgrades.BuildChoices(w.Player)[0].Apply(); w.PendingLevelUps--; }
			string msg = r.Message.Replace("\r\n", " | ");
			Say($"  > {cmd,-28} ok={r.Ok,-5} keepOpen={r.KeepOpen,-5} {msg.Substring(0, Math.Min(150, msg.Length))}");
		}
		Do("god on"); Do("god off"); Do("god");
		Do("xp 20"); Do("level 10"); Do("level 5");
		Do("weapon all 5"); Do("weapon claw"); Do("fuse"); Do("equip all"); Do("stats max");
		Do("spawn slime 5"); Do("spawn slime 3 special"); Do("spawn bat 4"); Do("spawn bomber 2 1.5"); Do("spawn brute 2");
		foreach (var f in new[] { "ring", "wall", "vflock", "pincer", "squad", "escort" }) Do("formation " + f);
		Do("time 600"); Do("gems 30"); Do("killall"); Do("dash"); Do("heal"); Do("timescale 3"); Do("timescale 1");
		Do("info"); Do("help");
		Do("spawn dragon"); Do("blah"); Do("xp abc"); Do("level"); Do("bosshp 10");
		Do("weapon all 1"); Do("boss", 200); Say($"    boss: {(w.Arena.Boss == null ? "null" : $"hp={w.Arena.Boss.Hp:0} dead={w.Arena.Boss.Dead} state={w.Arena.Boss.Boss.State}")}, active={w.Arena.Active}, ended={w.IsEnded}, playerDead={w.Player.Dead}, arena enemies={w.Enemies.Enemies.Count}");
		Do("bosshp 500"); Do("bossdie", 600);
		Say($"  sau cheat: ended={w.IsEnded}, cheatsUsed={w.State.CheatsUsed}");
	}

	// ------------------------------------------------------------------ 0. mật độ quái khi mèo đứng yên, không vũ khí
	private static void TestSpawnDensity()
	{
		Say("== SPAWN DENSITY (mèo đứng yên, không vũ khí, bất tử)");
		var w = new World();
		w.Player.Weapons.Clear();
		const float dt = 1f / 60f;
		var kinds = new int[5];
		int peak = 0; double drawMs = 0; int draws = 0, upd = 0; double updMs = 0;
		using var bmp = new Bitmap(1280, 720); using var gfx = Graphics.FromImage(bmp); var cv = new Canvas();
		using var fb = new FrameBuffer(640, 360);
		for (int step = 1; step <= 120 * 60; step++)
		{
			long u0 = Stopwatch.GetTimestamp();
			w.Update(dt);
			updMs += (Stopwatch.GetTimestamp() - u0) * 1000.0 / Stopwatch.Frequency; upd++;
			peak = Math.Max(peak, w.Enemies.Enemies.Count);
			if (step > 60 * 60 && step % 6 == 0)
			{
				long d0 = Stopwatch.GetTimestamp();
				// đường vẽ thật của GameCanvas: thế giới ở 640x360 -> StretchBlt -> HUD ở độ phân giải cửa sổ
				gfx.Clear(Color.Black); fb.G.Clear(Color.Black); cv.Begin(fb.G, 1f, PointF.Empty, false); w.DrawWorld(cv);
				w.DrawOverlay(cv); fb.BlitTo(gfx, new Rectangle(0, 0, 1280, 720));
				drawMs += (Stopwatch.GetTimestamp() - d0) * 1000.0 / Stopwatch.Frequency; draws++;
			}
			w.Player.Hp = w.Player.MaxHp;
			if (step == 30 * 60 || step == 60 * 60 || step == 120 * 60)
			{
				Array.Clear(kinds);
				foreach (var e in w.Enemies.Enemies) kinds[e.Kind]++;
				Say($"  t={step / 60}s: {w.Enemies.Enemies.Count} quái (slime {kinds[0]}, dơi {kinds[1]}, bomber {kinds[2]}, brute {kinds[3]})");
			}
		}
		Say($"  đỉnh {peak} quái; cập nhật TB {updMs / upd:F2} ms/khung, vẽ TB {drawMs / Math.Max(draws, 1):F2} ms/khung (1280x720, từ phút 2) => ngân sách 60 FPS = 16.7 ms");
	}

	// ------------------------------------------------------------------ 1. mô phỏng 150s
	private static void TestSimulation()
	{
		Say("== SIMULATION (150 giây, mèo bất tử, bot chạy + dash)");
		var w = new World();
		w.State.Running = true;
		const float dt = 1f / 60f;
		var sw = Stopwatch.StartNew();
		int maxEnemies = 0;
		float t = 0f;
		var canvas = new Canvas();
		using var bmp = new Bitmap(1280, 720);
		using var g = Graphics.FromImage(bmp);
		double drawMs = 0; int draws = 0;

		for (int step = 0; step < 150 * 60; step++)
		{
			t += dt;
			Bot(w, t);
			w.Update(dt);
			w.Player.Hp = w.Player.MaxHp;                 // bất tử để chạy hết 150s
			if (step % 600 == 599) w.State.AddXp(30f);    // ép lên cấp để thử menu nâng cấp
			ApplyUpgrades(w);
			maxEnemies = Math.Max(maxEnemies, w.Enemies.Enemies.Count);

			if (step % 6 == 0)                            // đo thời gian vẽ
			{
				var d0 = Stopwatch.GetTimestamp();
				g.Clear(Color.Black);
				canvas.Begin(g, 2f, PointF.Empty);
				w.Draw(canvas);
				drawMs += (Stopwatch.GetTimestamp() - d0) * 1000.0 / Stopwatch.Frequency;
				draws++;
			}
			if (step == 30 * 60) { Say($"  t=30s : quái {w.Enemies.Enemies.Count}, cấp {w.State.Level}, kills {w.State.Kills}"); Shot(w, "shot_30s"); }
			if (step == 60 * 60) Say($"  t=60s : quái {w.Enemies.Enemies.Count}, cấp {w.State.Level}, kills {w.State.Kills}");
			if (step == 100 * 60) { Say($"  t=100s: quái {w.Enemies.Enemies.Count}, cấp {w.State.Level}, kills {w.State.Kills}"); Shot(w, "shot_100s"); }
		}
		Say($"  t=150s: quái {w.Enemies.Enemies.Count} (tối đa {maxEnemies}), cấp {w.State.Level}, kills {w.State.Kills}, vũ khí {string.Join(",", w.Player.Weapons.Select(x => x.Name + x.Level))}");
		Say($"  thời gian mô phỏng+vẽ: {sw.ElapsedMilliseconds} ms cho 9000 bước; vẽ trung bình {drawMs / Math.Max(draws, 1):F2} ms/khung (độ phân giải 1280x720)");
	}

	// ------------------------------------------------------------------ 2. boss
	private static void TestBossFlow()
	{
		Say("== BOSS FLOW");
		var w = new World();
		bool? ended = null;
		w.Ended += v => ended = v;
		const float dt = 1f / 60f;

		int picks = 0;
		while (w.State.Level < GameState.MaxLevel)
		{
			w.State.AddXp(10000f);
			picks += w.PendingLevelUps;
			ApplyUpgrades(w);
		}
		Say($"  đạt cấp {w.State.Level} sau {picks} lần chọn nâng cấp; hợp nhất khả dụng: {Fusions.Available(w.Player).Count}; vũ khí: {string.Join(",", w.Player.Weapons.Select(x => x.Name + x.Level))}");

		float t = 0f;
		w.Update(dt); // Arena.Update mở màn boss ở khung kế tiếp
		Say($"  Arena.Active = {w.Arena.Active}, boss = {(w.Arena.Boss != null)}");

		int[] statesSeen = new int[12];
		for (int step = 0; step < 60 * 40 && ended == null; step++)
		{
			t += dt;
			Bot(w, t);
			w.Update(dt);
			w.Player.Hp = w.Player.MaxHp;
			var b = w.Arena.Boss;
			if (b != null && !b.Dead) statesSeen[b.Boss.State]++;
			// sau 6s đánh boss: giảm dần máu để xem hết các pha
			if (b != null && !b.Dead && step > 6 * 60 && step % 30 == 0) w.Enemies.Damage(b, 120f, Vector2.Zero);
			if (step == 5 * 60) { Shot(w, "shot_boss_intro"); Say($"  khung boss {w.Arena.Rect.Size.X:0}x{w.Arena.Rect.Size.Y:0}, mèo={w.Player.Pos}, camera={w.Cam}"); }
			if (step >= 12 * 60 && step <= 13 * 60) w.Player.Pos = w.Arena.Rect.Position + new Vector2(30f, 30f); // ghim ở góc trên-trái: xem khung + camera bị chặn
			if (step == 13 * 60) Shot(w, "shot_boss_corner");
			if (step >= 7 * 60 && step <= 8 * 60 && b != null && !b.Dead) w.Player.Pos = w.Arena.Clamp(b.Pos + new Vector2(0f, 110f), 10f); // đứng gần boss để chụp sprite
			if (step == 8 * 60) Shot(w, "shot_boss_close");
			if (step == 9 * 60 && b != null && !b.Dead)
			{
				// Mắt nhìn 8 hướng: đặt mèo quanh boss ở 8 góc (0 = phải, theo chiều kim đồng hồ), kiểm tra hướng nhìn Look = 0..7
				b.Pos = w.Arena.Rect.Center;
				b.Boss.State = 1; b.Boss.Timer = 0f; // đang đi đuổi (không lao) thì mắt nhìn theo mèo
				var looks = new List<int>();
				for (int k = 0; k < 8; k++)
				{
					w.Player.Pos = b.Pos + V.FromAngle(k * Mx.Tau / 8f) * 130f;
					w.Cam = b.Pos; // camera vào boss để chụp rõ con mắt
					Shot(w, $"shot_eye_{k}", 1f);
					looks.Add(b.Boss.Look);
				}
				Say($"  hướng nhìn của mắt khi mèo ở 8 góc: {string.Join(",", looks)} (mong đợi 0,1,2,3,4,5,6,7)");
			}
			if (step == 20 * 60) Shot(w, "shot_boss_fight");
			if (b != null && b.Boss.Phase == 3 && step % 100 == 0 && step > 600) { /* nổi điên */ }
		}
		Say($"  trạng thái boss đã gặp (số khung): idle/intro={statesSeen[0]} chase={statesSeen[1]} chargeWind={statesSeen[2]} charge={statesSeen[3]} stun={statesSeen[4]} slamWind={statesSeen[5]} slam={statesSeen[6]} roar={statesSeen[7]}");
		for (int k = 0; k < 60 * 6 && ended == null; k++) w.Update(dt);
		Say($"  kết thúc ván: ended={ended} (mong đợi True), điểm = {w.State.ComputeScore(true)}");

		// clamp trong vùng khoá
		var r = w.Arena.Rect;
		var p = w.Player.Pos;
		Say($"  mèo trong vùng khoá: {(p.X >= r.Position.X && p.X <= r.End.X && p.Y >= r.Position.Y && p.Y <= r.End.Y)}");
	}

	// ------------------------------------------------------------------ Độ khó
	private static void TestDifficulty()
	{
		Say("== DIFFICULTY");
		int saved = Settings.Difficulty;
		try
		{
			float baseHp = 0f, baseDmg = 0f;
			for (int d = 0; d < 4; d++)
			{
				Settings.Difficulty = d;
				var name = Difficulty.Name(Difficulty.Level);

				// quái: máu / sát thương (slime cỡ 1, bậc 0, t=0: chỉ khác nhau do độ khó)
				var slime = EnemyTypes.Create(EnemyTypes.Slime, 0f, Vector2.Zero, 1f, 0);
				var boss = EnemyTypes.Create(EnemyTypes.Boss, 0f, Vector2.Zero);
				if (d == 0) { baseHp = slime.MaxHp; baseDmg = slime.Damage; }

				// hồi máu: Heal() và hồi tự động
				var w = new World();
				w.Player.Hp = 10f;
				w.Player.Heal(50f);
				float afterHeal = w.Player.Hp;
				w.Player.Regen = 5f;
				w.Player.Hp = 10f;
				for (int i = 0; i < 60; i++) w.Player.Update(1f / 60f);
				float afterRegen = w.Player.Hp;

				// thẻ nâng cấp: gom tên của 300 lần rút
				var titles = new HashSet<string>();
				for (int i = 0; i < 300; i++)
					foreach (var u in Upgrades.BuildChoices(new World().Player)) titles.Add(u.Title);
				bool hasMaxHp = titles.Any(t => t.StartsWith("Máu tối đa")), hasRegen = titles.Any(t => t.StartsWith("Hồi máu")), hasChicken = titles.Contains("Gà quay");
				bool hasArmor = titles.Any(t => t.StartsWith("Giáp +2") || t.StartsWith("Trang bị"));

				Say($"  [{d}] {name}: slime máu x{slime.MaxHp / baseHp:0.00} sát thương x{slime.Damage / baseDmg:0.00} · boss máu {boss.MaxHp:0} · vùng báo hiệu={Difficulty.ShowEnemyTelegraphs} · " +
					$"hồi máu: Heal 10->{afterHeal:0}, hồi tự động 10->{afterRegen:0.0} · thẻ máu tối đa={hasMaxHp}, hồi máu={hasRegen}, gà quay={hasChicken}, giáp={hasArmor}");
			}
			for (int d = 0; d < 4; d++)
				{
					// điểm thành tích: cùng một ván (30 kills, cấp 1) nhân theo độ khó
					Settings.Difficulty = d;
					var gs = new World().State;
					gs.Kills = 30;
					Say($"  điểm [{Difficulty.Name(Difficulty.Level)}]: {gs.ComputeBaseScore(false)} -> {gs.ComputeScore(false)}");
				}
				Say("  mong đợi: Dễ x1/x1 telegraph=True · Trung bình x2/x2 telegraph=True · Khó x4/x4 telegraph=False · Siêu khó x6/x6 telegraph=False + không hồi máu (10->10) + không có thẻ máu tối đa/hồi máu/gà quay, vẫn có giáp · điểm 300 -> 300/600/1200/1800");
				TestPrediction();
		}
		finally { Settings.Difficulty = saved; }
	}

	/// <summary>
	/// Dự đoán đường đi: mèo chạy sang PHẢI đều đều, một quái tốc độ 100 xuất phát phía trên mèo 200px đuổi theo.
	/// Đo thời gian quái bắt kịp (còn cách < 12px) và đường chạy: dự đoán càng tốt thì bắt càng nhanh. Cũng đo độ chuẩn của
	/// điểm chặn đầu của quái đứng yên (sai lệch với chỗ mèo thật sự đứng sau 1 giây).
	/// </summary>
	private static void TestPrediction()
	{
		Say("== PREDICTION (quái đuổi mèo đang chạy ngang)");
		int saved = Settings.Difficulty;
		try
		{
			for (int d = 0; d < 4; d++)
			{
				Settings.Difficulty = d;
				var w = new World();
				const float dt = 1f / 60f;
				w.Input.KeyDown(Keys.D);
				for (int i = 0; i < 90; i++) w.Player.Update(dt); // chạy 1.5s cho vận tốc làm mượt ổn định
				Vector2 epos = w.Player.Pos + new Vector2(0f, -200f);
				const float espeed = 140f;   // nhanh hơn mèo (110): chỉ khi đón đầu tốt mới bắt kịp sớm
				float caught = -1f, minDist = 1e9f;
				for (int i = 0; i < 60 * 8 && caught < 0f; i++)
				{
					w.Player.Update(dt);
					Vector2 direct = (w.Player.Pos - epos).Normalized();
					Vector2 dir = Prediction.Dir(epos, w.Player, espeed, direct);
					epos += dir * espeed * dt;
					float dist = (w.Player.Pos - epos).Length();
					minDist = Math.Min(minDist, dist);
					if (dist < 12f) caught = i * dt;
				}

				// độ chuẩn: quái đứng yên cách 200px phía trước-trên; đối chiếu điểm nhắm với chỗ mèo thật sự ở sau (thời gian bay)
				Vector2 from = w.Player.Pos + new Vector2(150f, -150f);
				Vector2 aim = Prediction.InterceptPoint(from, w.Player, espeed);
				float tFly = (aim - from).Length() / espeed;
				Vector2 truth = w.Player.Pos + w.Player.Velocity * tFly;
				float lead = (aim - w.Player.Pos).Length();
				Say($"  [{d}] {Difficulty.Name(Difficulty.Level)}: bắt kịp sau {(caught < 0f ? "KHÔNG (gần nhất " + minDist.ToString("0") + "px)" : caught.ToString("0.00") + "s")} · điểm chặn đầu lệch khỏi chỗ mèo đang đứng {lead:0}px, sai lệch với chỗ mèo thật sự tới {(aim - truth).Length():0}px");
			}
			Say("  mong đợi: thời gian bắt kịp giảm dần theo độ khó (Dễ chậm nhất, Siêu khó nhanh nhất) · sai lệch với chỗ mèo thật sự tới giảm dần, Siêu khó ~0");
		}
		finally { Settings.Difficulty = saved; }
	}

	/// <summary>Bomber đuổi mèo đang chạy thẳng: đo thời gian từ lúc xuất phát (cách 220px phía sau) tới lúc bắt kịp và châm ngòi, với mèo chạy thường và mèo đã nâng tốc độ tối đa.</summary>
	private static void TestBomberChase()
	{
		Say("== BOMBER CHASE (mèo chạy thẳng sang phải, bomber xuất phát cách 220px phía sau)");
		foreach (float speed in new[] { 110f, 165f, Balance.PlayerSpeed })
		{
			var w = new World();
			w.Player.God = true;
			w.Player.Weapons.Clear(); // không có vũ khí: đạn của mèo không đẩy lùi / bắn chết bomber trong lúc đo
			w.Player.Speed = speed;
			float run = w.Player.RunSpeed;
			var b = EnemyTypes.Create(EnemyTypes.Bomber, 0f, w.Player.Pos + new Vector2(-220f, 0f), 1f, 0);
			b.MaxHp = b.Hp = 1e6f; b.Armor = 1e3f; // trâu để vũ khí của mèo không bắn chết trước khi bắt kịp
			w.Enemies.Enemies.Add(b);
			w.Input.KeyDown(Keys.D);
			float caught = -1f;
			for (int i = 0; i < 60 * 15 && caught < 0f; i++)
			{
				w.Update(1f / 60f);
				if (b.FuseTime >= 0f || b.Exploded) caught = i / 60f;
			}
			Say($"  mèo chạy {run:0}px/s: bomber (tốc độ gốc {b.Speed:0}) {(caught < 0f ? "KHÔNG bắt kịp trong 15s" : $"bắt kịp và châm ngòi sau {caught:0.0}s")}");
		}
		Say("  mong đợi: bắt kịp trong vài giây ở mọi mức tốc độ của mèo (trừ khi mèo dash)");
	}

	/// <summary>Bomber: khung TRẮNG khi bị đánh (khung 16) và hiệu ứng nổ (khung 13-15) phóng theo bán kính nổ. Ảnh cắt quanh bomber, phóng 3x.</summary>
	private static void TestBomber()
	{
		Say("== BOMBER");
		var w = new World();
		w.Player.God = true;
		var b = EnemyTypes.Create(EnemyTypes.Bomber, 0f, w.Player.Pos + new Vector2(0f, 40f), 1f, 0);
		b.Speed = 0f;
		w.Enemies.Enemies.Add(b);
		for (int i = 0; i < 3; i++) w.Update(1f / 60f);

		void Crop(string name)
		{
			using var full = new Bitmap(1280, 720);
			using (var g = Graphics.FromImage(full))
			{
				var canvas = new Canvas();
				using var fb = new FrameBuffer(640, 360);
				fb.G.Clear(Color.FromArgb(70, 120, 70));
				canvas.Begin(fb.G, 1f, PointF.Empty, false);
				w.DrawWorld(canvas);
				fb.BlitTo(g, new Rectangle(0, 0, 1280, 720));
			}
			// bomber ở (320, 180 + 40 - camera lệch) -> cắt vùng 200x140 quanh bomber (toạ độ khung hình 2x)
			int cx = 640, cy = 360 + (int)((b.Pos.Y - w.Player.Pos.Y) * 2f);
			var r = new Rectangle(cx - 140, cy - 100, 280, 200);
			using var crop = new Bitmap(r.Width * 2, r.Height * 2);
			using (var g2 = Graphics.FromImage(crop)) { g2.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor; g2.DrawImage(full, new Rectangle(0, 0, crop.Width, crop.Height), r, GraphicsUnit.Pixel); }
			crop.Save(Path.Combine(_out, name + ".png"));
		}

		b.Flash = 0.2f;
		Crop("bomber_hit");                         // khung trắng hoàn toàn
		b.Flash = 0f;
		b.FuseTime = 0.02f;                         // châm ngòi gần nổ
		w.Update(1f / 60f); w.Update(1f / 60f); w.Update(1f / 60f);
		Say($"  nổ: Exploded={b.Exploded}, bán kính nổ={44f * b.Size:0}px");
		for (int k = 0; k < 3; k++)
		{
			Crop($"bomber_blast_{k}");
			for (int i = 0; i < 8; i++) w.Update(1f / 60f);   // BombAnim 0.4s chia 3 khung
		}
	}

	/// <summary>Kĩ năng chủ động: Q (Nova) và E (làm chậm thời gian) - tốn mana, hồi chiêu, tác dụng lên quái.</summary>
	private static void TestSkills()
	{
		Say("== SKILLS (Q Nova, E làm chậm thời gian)");
		const float dt = 1f / 60f;
		var w = new World();
		w.Player.God = true;
		var ring = new List<Enemy>();
		for (int i = 0; i < 6; i++)
		{
			var e = EnemyTypes.Create(EnemyTypes.Slime, 0f, w.Player.Pos + V.FromAngle(i * Mx.Tau / 6f) * 70f, 1f, 0);
			e.Speed = 0f; e.MaxHp = e.Hp = 1e4f; e.Armor = 0f;
			w.Enemies.Enemies.Add(e); ring.Add(e);
		}
		for (int i = 0; i < 2; i++) w.Update(dt);
		var hp0 = ring.Select(e => e.Hp).ToArray();
		var pos0 = ring.Select(e => e.Pos).ToArray();
		float mana0 = w.Player.Mana;

		w.Input.KeyDown(Keys.Q); w.Update(dt); w.Input.KeyUp(Keys.Q);
		for (int i = 0; i < 20; i++) w.Update(dt);
		float dmg = hp0[0] - ring[0].Hp, moved = (ring[0].Pos - pos0[0]).Length();
		Say($"  Q: mana {mana0:0} -> {w.Player.Mana:0} (mong đợi -{SkillSet.NovaCost:0}), quái bị trừ {dmg:0} máu và bị hất {moved:0}px (mong đợi > 0), hồi chiêu còn {w.Player.Skills.NovaCd:0.00}s");
		Shot(w, "shot_skill_nova_after", 2f);

		// ngay lập tức bấm Q lần nữa: còn hồi chiêu -> không tốn thêm mana
		float manaBefore = w.Player.Mana;
		w.Input.KeyDown(Keys.Q); w.Update(dt); w.Input.KeyUp(Keys.Q);
		Say($"  Q lần 2 khi còn hồi chiêu: mana {manaBefore:0} -> {w.Player.Mana:0} (mong đợi không đổi ngoài hồi tự nhiên)");

		// thiếu mana: không dùng được
		w.Player.Mana = 5f;
		for (int i = 0; i < 90; i++) w.Update(dt);          // qua hết hồi chiêu
		w.Player.Mana = 5f;
		float hpBefore = ring[0].Hp;
		w.Input.KeyDown(Keys.Q); w.Update(dt); w.Input.KeyUp(Keys.Q);
		Say($"  Q khi thiếu mana (5): quái trúng đòn={ring[0].Hp < hpBefore} (mong đợi False), toast='{w.Toast}'");

		// E: so sánh quãng đường quái đi được trong 1 giây có / không làm chậm
		float Travel(bool warp)
		{
			var w2 = new World();
			w2.Player.God = true;
			var e = EnemyTypes.Create(EnemyTypes.Slime, 0f, w2.Player.Pos + new Vector2(0f, -400f), 1f, 0);
			e.Speed = 60f; e.MaxHp = e.Hp = 1e4f;
			w2.Enemies.Enemies.Add(e);
			w2.Update(dt);
			if (warp) { w2.Player.Mana = w2.Player.MaxMana; w2.Input.KeyDown(Keys.E); w2.Update(dt); w2.Input.KeyUp(Keys.E); }
			Vector2 start = e.Pos; float m0 = w2.Player.Mana;
			for (int i = 0; i < 60; i++) w2.Update(dt);
			if (warp) Say($"  E: mana đã trừ {SkillSet.WarpCost:0}? mana sau 1s = {w2.Player.Mana:0}, đang chậm={w2.Player.Skills.WarpActive}, hồi chiêu {w2.Player.Skills.WarpCd:0.0}s");
			return (e.Pos - start).Length();
		}
		float normal = Travel(false), slowed = Travel(true);
		Say($"  quái đi trong 1s: bình thường {normal:0}px, đang làm chậm {slowed:0}px (mong đợi ~45%: {slowed / Math.Max(normal, 1f):0.00})");

		var w3 = new World();
		w3.Player.Mana = w3.Player.MaxMana;
		w3.Input.KeyDown(Keys.E); w3.Update(dt); w3.Input.KeyUp(Keys.E);
		for (int i = 0; i < 30; i++) w3.Update(dt);
		Shot(w3, "shot_skill_warp", 2f);
	}

	/// <summary>
	/// Boss thông minh theo độ khó: cùng một kiểu chạy vòng của mèo trong 40 giây (máu boss giữ nguyên, mèo không chết),
	/// đếm số đòn boss tung, số lần tự choáng vì đâm tường, tổng thời gian kiệt sức và số lần trúng đòn. Chạy 3 ván mỗi mức rồi lấy trung bình.
	/// </summary>
	private static void TestBossBrain()
	{
		Say("== BOSS BRAIN (mèo chạy vòng 40s, trung bình 3 ván)");
		int saved = Settings.Difficulty;
		try
		{
			for (int d = 0; d < 4; d++)
			{
				Settings.Difficulty = d;
				float charges = 0, slams = 0, roars = 0, stuns = 0, stunFrames = 0, hits = 0; var stFrames = new float[8];
				const int trials = 3, frames = 60 * 40;
				for (int tr = 0; tr < trials; tr++)
				{
					var w = new World();
					Cheats.Execute(w, "boss");
					w.Player.MaxHp = Balance.PlayerMaxHp; // 400 máu: một cú húc ở Khó / Siêu khó gây hơn 100 sát thương
					w.Player.Hp = w.Player.MaxHp;
					w.Update(1f / 60f);
					var boss = w.Arena.Boss;
					Vector2 center = w.Arena.Rect.Center;
					int prev = -1;
					for (int i = 0; i < frames; i++)
					{
						// mèo chạy vòng quanh tâm vùng khoá (bán kính 170) bằng phím WASD thật (để có vận tốc cho boss dự đoán): hướng tiếp tuyến + bù lệch bán kính
						Vector2 rel = w.Player.Pos - center;
						float rl = Math.Max(rel.Length(), 1f);
						Vector2 steer = new Vector2(-rel.Y, rel.X) / rl + (-rel / rl) * ((rl - 170f) / 40f);
						w.Input.Clear();
						if (steer.X > 0.38f) w.Input.KeyDown(Keys.D); else if (steer.X < -0.38f) w.Input.KeyDown(Keys.A);
						if (steer.Y > 0.38f) w.Input.KeyDown(Keys.S); else if (steer.Y < -0.38f) w.Input.KeyDown(Keys.W);
						float hp = w.Player.Hp = w.Player.MaxHp;
						boss.Hp = boss.MaxHp;
						w.Player.God = false;
						w.Update(1f / 60f);
						if (w.Player.Hp < hp) hits++;
						if (boss.Dead) break;
						int st = boss.Boss.State;
						if (st != prev)
						{
							if (st == 2) charges++; else if (st == 5) slams++; else if (st == 7) roars++; else if (st == 4) stuns++;
							prev = st;
						}
						if (st == 4) stunFrames++;
						stFrames[st]++;
					}
				}
				Say($"  [{d}] {Difficulty.Name(Difficulty.Level)}: lao {charges / trials:0.0} · giậm {slams / trials:0.0} · gầm {roars / trials:0.0} (tổng đòn {(charges + slams + roars) / trials:0.0}) · tự choáng {stuns / trials:0.0} lần ({stunFrames / trials / 60f:0.0}s) · trúng đòn {hits / trials:0.0} lần · giây ở mỗi trạng thái (intro,đuổi,gồng lao,lao,choáng,gồng giậm,giậm,gầm): {string.Join("/", stFrames.Select(f => (f / trials / 60f).ToString("0")))}");
			}
			Say("  mong đợi: tổng đòn tăng dần theo độ khó, số lần tự choáng / thời gian kiệt sức giảm dần (Siêu khó ~0), số lần mèo bị trúng tăng dần");
		}
		finally { Settings.Difficulty = saved; }
	}

	// ------------------------------------------------------------------ Font: chữ có móc (ơ, ư) và các dấu
	private static void TestFont()
	{
		Say("== FONT");
		using var bmp = new Bitmap(640, 180);
		using var g = Graphics.FromImage(bmp);
		g.Clear(Color.FromArgb(40, 80, 40));
		var canvas = new Canvas();
		using var fb = new FrameBuffer(320, 90);
		fb.G.Clear(Color.FromArgb(40, 80, 40));
		canvas.Begin(fb.G, 1f, PointF.Empty, false);
		canvas.ScreenMode();
		PixelFont.Draw(canvas, "o ơ u ư O Ơ U Ư ờ ở ỡ ớ ợ ừ ử ữ ứ ự", new Vector2(4, 4), 1, Rgba.White);
		PixelFont.Draw(canvas, "Hơn Hướng dẫn Nhớ Thưởng Mượn Quỷ", new Vector2(4, 20), 1, Rgba.White);
		PixelFont.Draw(canvas, "ơ ư Ơ Ư", new Vector2(4, 40), 3, Rgba.White);
		fb.BlitTo(g, new Rectangle(0, 0, 640, 180));
		bmp.Save(Path.Combine(_out, "font_viet.png"));
		Say("  đã ghi font_viet.png");
	}

	// ------------------------------------------------------------------ Holy Shield: đỡ đòn
	// ------------------------------------------------------------------ ảnh cận cảnh hiệu ứng bắn của mắt con
	private static void TestEyeFx()
	{
		Say("== HIỆU ỨNG BẮN CỦA MẮT CON (ảnh cận cảnh 4x theo chuỗi thời gian)");
		const float dt = 1f / 60f;
		var w = new World();
		w.Player.God = true;
		w.Player.Weapons.Clear();
		Cheats.Execute(w, "boss");
		for (int i = 0; i < 60 * 3; i++) w.Update(dt);
		Cheats.Execute(w, "stage2");
		for (int i = 0; i < 60 * 4; i++) w.Update(dt);
		var boss = w.Arena.Boss;
		var eyes = w.Enemies.Enemies.Where(e => e.Kind == EnemyTypes.MiniEye).ToList();
		var mini = eyes[0];
		foreach (var o in eyes.Skip(1)) o.Pos = boss.Pos + new Vector2(400f, 300f);  // cất mấy con kia đi cho ảnh gọn
		boss.Pos = mini.Pos + new Vector2(0f, -400f);
		w.Player.Pos = mini.Pos + new Vector2(210f, 60f);                           // mèo nằm bên phải, hơi thấp
		w.Cam = w.Player.Pos - new Vector2(110f, 30f);

		var frames = new List<Bitmap>();
		void Grab()
		{
			using var fb = new FrameBuffer((int)Balance.ViewWidth, (int)Balance.ViewHeight);
			fb.G.Clear(Color.FromArgb(60, 100, 60));
			var cv = new Canvas();
			cv.Begin(fb.G, 1f, PointF.Empty, false);
			w.DrawWorld(cv);
			// cắt quanh mắt con (toạ độ màn hình = thế giới - (Cam - View/2))
			var tl = w.Cam - World.ViewSize / 2f;
			var c = mini.Pos - tl;
			var src = new Rectangle((int)c.X - 40, (int)c.Y - 40, 130, 80);
			var bmp = new Bitmap(src.Width * 4, src.Height * 4);
			using var g = Graphics.FromImage(bmp);
			g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
			g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
			g.DrawImage(fb.Bitmap, new Rectangle(0, 0, bmp.Width, bmp.Height), src, GraphicsUnit.Pixel);
			frames.Add(bmp);
		}
		var m = mini.Mini;
		m.ShootCd = 0f; m.DashLeft = 0f;
		int step = 0, shotAt = -1;
		var grabAt = new HashSet<int> { 5, 20, 36, 44 };
		int bolts0 = 0;
		for (step = 0; step < 120 && frames.Count < 8; step++)
		{
			boss.Pos = mini.Pos + new Vector2(-330f, -200f);                          // giữ mắt chính ở xa cho ảnh gọn
			foreach (var o in eyes.Skip(1)) o.Pos = mini.Pos + new Vector2(-300f, 220f);
			w.Update(dt);
			if (m.Windup < 0f && shotAt < 0 && w.Enemies.Bolts.Count > bolts0) shotAt = step;
			if (shotAt < 0 && grabAt.Contains(step)) Grab();
			if (shotAt >= 0 && (step - shotAt == 1 || step - shotAt == 5 || step - shotAt == 12 || step - shotAt == 22)) Grab();
		}
		Say($"  chụp {frames.Count} khung, bắn ở bước {shotAt}, heading={m.Heading:0.00} rad");
		if (frames.Count > 0)
		{
			int cols = 2, rows = (frames.Count + 1) / 2;
			using var sheet = new Bitmap(frames[0].Width * cols, frames[0].Height * rows);
			using var g = Graphics.FromImage(sheet);
			for (int i = 0; i < frames.Count; i++) g.DrawImage(frames[i], (i % cols) * frames[0].Width, (i / cols) * frames[0].Height);
			sheet.Save(Path.Combine(_out, "eyefx_sheet.png"));
		}
		foreach (var b in frames) b.Dispose();
	}
	// ------------------------------------------------------------------ giai đoạn 2 của boss: 4 mắt con
	/// <summary>Phép thử chữ thập: dựng ván ở giai đoạn 2 (không đuổi mắt con), ép chữ thập, đứng trên tia, rồi diệt một con.</summary>
	private static void CrossCheck(int diff)
	{
		const float dt = 1f / 60f;
		var w = new World();
		w.Player.MaxHp = 5000f; w.Player.Hp = 5000f;
		w.Player.Weapons.Clear(); // không để vũ khí của mèo giết mắt con trước khi thử
		Cheats.Execute(w, "boss");
		for (int i = 0; i < 60 * 3; i++) w.Update(dt);
		Cheats.Execute(w, "stage2");
		for (int i = 0; i < 60 * 4; i++) w.Update(dt);
		var boss = w.Arena.Boss; var bd = boss.Boss;
		bd.CrossCd = 0f; bd.AttackCd = 0f; bd.State = 1;
		for (int i = 0; i < 60 * 2 && bd.State != 9; i++) w.Update(dt);
		Say($"  chữ thập: state={bd.State} crossState={bd.CrossState} (mong đợi 9 và 1), mắt con={w.Enemies.MiniCount()}");
		for (int i = 0; i < 90; i++) w.Update(dt);                          // qua pha xếp hình
		var (a, b) = MiniEyes.Beam(boss, 0);
		w.Player.Pos = a + (b - a) * 0.5f;
		float hpx = w.Player.Hp;
		Shot(w, $"eyes_cross_{diff}");
		for (int i = 0; i < 30; i++) w.Update(dt);
		Say($"  đứng trên tia: máu {hpx:0} -> {w.Player.Hp:0} (mong đợi giảm), crossState={bd.CrossState}, góc quay {bd.CrossAngle:0.00}");
		var one = w.Enemies.Enemies.First(e => e.Kind == EnemyTypes.MiniEye && !e.Dead);
		w.Enemies.Damage(one, 1e6f, Vector2.Zero);
		for (int i = 0; i < 10; i++) w.Update(dt);
		Say($"  diệt 1 con giữa chừng: crossState={bd.CrossState} state={bd.State} (mong đợi 0 và 1)");
	}
	private static void TestMiniEyes()
	{
		Say("== GIAI ĐOẠN 2: 4 MẮT CON");
		// Đo phóng "sharp" (cửa sổ tỉ lệ lẻ, vd. 1920x1000) so với nearest nguyên
		{
			using var fb = new FrameBuffer((int)Balance.ViewWidth, (int)Balance.ViewHeight);
			using var target = new Bitmap(1920, 1000);
			using var tg = Graphics.FromImage(target);
			FrameBuffer scratch = null;
			var rect = new Rectangle(0, 0, 1920, 1000);
			var sw = Stopwatch.StartNew();
			for (int i = 0; i < 60; i++) fb.BlitSharp(tg, rect, ref scratch);
			double sharp = sw.Elapsed.TotalMilliseconds / 60;
			sw.Restart();
			for (int i = 0; i < 60; i++) fb.BlitTo(tg, rect);
			Say($"  phóng 1920x1000: sharp {sharp:0.0} ms/khung, nearest {sw.Elapsed.TotalMilliseconds / 60:0.0} ms/khung");
			scratch?.Dispose();
		}
		const float dt = 1f / 60f;
		int savedDiff = Settings.Difficulty;
		try
		{
			foreach (int diff in new[] { 0, 3 })
			{
				Settings.Difficulty = diff;
				Say($"-- độ khó {Difficulty.Name(Difficulty.Level)}: mắt con chạy {MiniEyes.FleeSpeed:0}px/s, hồi bắn ~{MiniEyes.ShootCooldown:0.0}s, chữ thập quay {MiniEyes.CrossSpin:0.00} rad/s");
				var w = new World();
				w.Player.God = false;
				w.Player.MaxHp = 5000f; w.Player.Hp = 5000f;
				Cheats.Execute(w, "boss");
				for (int i = 0; i < 60 * 3; i++) w.Update(dt);                       // qua màn xuất hiện của boss
				var boss = w.Arena.Boss;
				var bd = boss.Boss;
				Say($"  boss: state={bd.State} stage={bd.Stage} hp={boss.Hp:0}/{boss.MaxHp:0}");

				// Một đòn lớn không được vượt mốc 50% (phải qua giai đoạn 2)
				w.Enemies.Damage(boss, boss.MaxHp, Vector2.Zero);
				Say($"  sau đòn cực lớn: hp={boss.Hp:0} ({boss.Hp / boss.MaxHp:P0}), chết={boss.Dead} (mong đợi còn ~48%, chưa chết)");
				for (int i = 0; i < 60 * 3; i++) w.Update(dt);
				int minis = w.Enemies.MiniCount();
				Say($"  sau 3s: stage={bd.Stage} khiên={bd.ShieldUp} mắt con={minis} (mong đợi stage 2, khiên bật, 4 con)");
				float hpBefore = boss.Hp;
				w.Enemies.Damage(boss, 500f, Vector2.Zero);
				Say($"  đánh mắt chính khi có khiên: máu {hpBefore:0} -> {boss.Hp:0} (mong đợi không đổi)");

				// Mắt con chạy trốn: mèo chạy thẳng vào con gần nhất; đo khoảng cách + vị trí nằm trong vùng
				int shots = 0; float hp0 = w.Player.Hp; float minGap = 1e9f; bool inside = true;
				for (int i = 0; i < 60 * 12; i++)
				{
					var near = w.Enemies.Enemies.Where(e => e.Kind == EnemyTypes.MiniEye && !e.Dead).OrderBy(e => (e.Pos - w.Player.Pos).Length()).FirstOrDefault();
					w.Input.Clear();
					if (near != null)
					{
						var d = near.Pos - w.Player.Pos;
						if (d.X > 6f) w.Input.KeyDown(Keys.D); else if (d.X < -6f) w.Input.KeyDown(Keys.A);
						if (d.Y > 6f) w.Input.KeyDown(Keys.S); else if (d.Y < -6f) w.Input.KeyDown(Keys.W);
						minGap = MathF.Min(minGap, d.Length());
					}
					int before = w.Enemies.Bolts.Count;
					w.Update(dt);
					if (w.Enemies.Bolts.Count > before) shots++;
					foreach (var e in w.Enemies.Enemies) if (e.Kind == EnemyTypes.MiniEye && !e.Dead && !w.Arena.Rect.Grow(1f).Equals(default(Rect2)) && (e.Pos.X < w.Arena.Rect.Position.X - 1 || e.Pos.X > w.Arena.Rect.End.X + 1 || e.Pos.Y < w.Arena.Rect.Position.Y - 1 || e.Pos.Y > w.Arena.Rect.End.Y + 1)) inside = false;
					if (i == 60 * 6) Shot(w, $"eyes_chase_{diff}");
				}
				w.Input.Clear();
				Say($"  12s đuổi: mắt con còn {w.Enemies.MiniCount()}, khoảng cách gần nhất {minGap:0}px, chưởng đã bắn {shots}, mèo mất {hp0 - w.Player.Hp:0} máu, luôn trong vùng={inside}");

				CrossCheck(diff);

				// Diệt hết -> khiên tắt, mắt chính choáng, đánh được
				foreach (var e in w.Enemies.Enemies.Where(e => e.Kind == EnemyTypes.MiniEye && !e.Dead).ToList()) w.Enemies.Damage(e, 1e6f, Vector2.Zero);
				for (int i = 0; i < 10; i++) w.Update(dt);
				float h1 = boss.Hp;
				w.Enemies.Damage(boss, 300f, Vector2.Zero);
				Say($"  diệt hết: khiên={bd.ShieldUp} state={bd.State} (mong đợi false, 4), đánh mắt chính: máu {h1:0} -> {boss.Hp:0} (mong đợi giảm)");
			}
		}
		finally { Settings.Difficulty = savedDiff; }
	}
	// ------------------------------------------------------------------ mỗi ván chỉ một kĩ năng
	private static void TestSkillPick()
	{
		Say("== SKILL PICK (mỗi ván chỉ một kĩ năng)");
		var saved = SkillSet.RunChoice;
		try
		{
			foreach (var kind in new[] { SkillKind.Nova, SkillKind.Warp, SkillKind.Stop, SkillKind.Guard })
				foreach (var key in new[] { Keys.Q, Keys.E })
				{
					SkillSet.RunChoice = kind;
					var w = new World();
					w.Player.God = true;
					w.Player.Mana = w.Player.MaxMana;
					w.Input.KeyDown(key); w.Input.KeyUp(key);
					w.Update(1f / 60f);
					var sk = w.Player.Skills;
					bool nova = sk.NovaCd > 0f, warp = sk.WarpActive, stop = sk.StopActive, guard = sk.GuardActive;
					int on = (nova ? 1 : 0) + (warp ? 1 : 0) + (stop ? 1 : 0) + (guard ? 1 : 0);
					bool want = kind switch { SkillKind.Nova => nova, SkillKind.Warp => warp, SkillKind.Stop => stop, _ => guard };
					Say($"  chọn {kind}, bấm {key}: nova={nova} warp={warp} stop={stop} guard={guard} -> {(want && on == 1 ? "OK" : "!! SAI")}");
				}
			// Ngừng thời gian: quái đứng yên + không gây sát thương; hết 1.5s thì chạy lại
			{
				SkillSet.RunChoice = SkillKind.Stop;
				var w = new World();
				w.Player.Mana = w.Player.MaxMana;
				Cheats.Execute(w, "spawn slime 6");
				foreach (var en in w.Enemies.Enemies) en.Pos = w.Player.Pos + new Vector2(60f, 0f);
				var start = w.Enemies.Enemies.Select(en => en.Pos).ToList();
				float hp0 = w.Player.Hp;
				w.Player.Skills.TryStop();
				for (int i = 0; i < 80; i++) w.Update(1f / 60f); // ~1.33s
				float moved = 0f; int idx = 0;
				foreach (var en in w.Enemies.Enemies) { if (idx < start.Count) moved = MathF.Max(moved, (en.Pos - start[idx]).Length()); idx++; }
				Say($"  Ngừng thời gian: sau 1.33s quái dịch tối đa {moved:0.0}px (mong đợi ~0), máu mèo {hp0:0} -> {w.Player.Hp:0} (mong đợi không đổi), đang ngừng={w.Player.Skills.StopActive}");
				for (int i = 0; i < 60; i++) w.Update(1f / 60f);
				Say($"  sau khi hết: đang ngừng={w.Player.Skills.StopActive}, máu mèo {w.Player.Hp:0} (quái đã chạy lại và đánh được)");
			}
			// Bất tử: quái đè lên mèo 2s không mất máu; hết rồi mới mất
			{
				SkillSet.RunChoice = SkillKind.Guard;
				var w = new World();
				w.Player.Mana = w.Player.MaxMana;
				Cheats.Execute(w, "spawn slime 6");
				float hp0 = w.Player.Hp;
				w.Player.Skills.TryGuard();
				for (int i = 0; i < 100; i++) { foreach (var en in w.Enemies.Enemies) en.Pos = w.Player.Pos; w.Update(1f / 60f); } // 1.67s
				Say($"  Bất tử: sau 1.67s máu {hp0:0} -> {w.Player.Hp:0} (mong đợi không đổi), còn {w.Player.Skills.GuardLeft:0.00}s");
				for (int i = 0; i < 90; i++) { foreach (var en in w.Enemies.Enemies) en.Pos = w.Player.Pos; w.Update(1f / 60f); }
				Say($"  sau khi hết bất tử: máu {w.Player.Hp:0} (mong đợi < {hp0:0}), đang bất tử={w.Player.Skills.GuardActive}");
				Shot(w, "pick_hud_guard");
			}
			// Độ Dễ / Trung bình: HAI kĩ năng (Q = đầu, E = thứ hai); Khó / Siêu khó: MỘT
			{
				int savedDiff = Settings.Difficulty;
				var savedSecond = SkillSet.RunChoice2;
				try
				{
					foreach (int d in new[] { 0, 1, 2, 3 })
					{
						Settings.Difficulty = d;
						Say($"  độ khó {d}: được mang {SkillPick.PicksAllowed} kĩ năng (mong đợi {(d <= 1 ? 2 : 1)})");
					}
					SkillSet.RunChoice = SkillKind.Nova; SkillSet.RunChoice2 = SkillKind.Stop;
					foreach (var key in new[] { Keys.Q, Keys.E })
					{
						var w = new World();
						w.Player.God = true;
						w.Player.Mana = w.Player.MaxMana;
						w.Input.KeyDown(key); w.Input.KeyUp(key);
						w.Update(1f / 60f);
						var sk = w.Player.Skills;
						bool ok = key == Keys.Q ? sk.NovaCd > 0f && !sk.StopActive : sk.StopActive && sk.NovaCd <= 0f;
						Say($"  hai kĩ năng Nova(Q) + Stop(E), bấm {key}: nova={sk.NovaCd > 0f} stop={sk.StopActive} -> {(ok ? "OK" : "!! SAI")}");
					}
					Shot(new World(), "pick_hud_two");
					// Màn chọn: lá đầu đánh dấu Q và chưa đóng, lá trùng bị bỏ qua, lá thứ hai đóng form
					using var f2 = new SkillPickForm(SkillPick.Cards(), 2);
					f2.StartPosition = FormStartPosition.Manual; f2.Location = new Point(40, 40);
					f2.Show(); Application.DoEvents();
					var pick = typeof(SkillPickForm).GetMethod("Pick", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
					var cards = SkillPick.Cards();
					pick.Invoke(f2, new object[] { cards[0] }); Application.DoEvents();
					bool openAfterFirst = f2.Visible && f2.Chosen == null;
					using (var bmp2 = new Bitmap(f2.Width, f2.Height)) { f2.DrawToBitmap(bmp2, new Rectangle(0, 0, f2.Width, f2.Height)); bmp2.Save(Path.Combine(_out, "pick_form_two.png")); }
					pick.Invoke(f2, new object[] { cards[0] });                // trùng: bỏ qua
					bool stillOpen = f2.Visible && f2.Chosen == null;
					pick.Invoke(f2, new object[] { cards[2] });
					Say($"  màn chọn 2 lá: sau lá đầu còn mở={openAfterFirst}, chọn trùng bị bỏ qua={stillOpen}, kết quả={f2.Chosen}+{f2.Chosen2} (mong đợi Nova+Stop) -> {(openAfterFirst && stillOpen && f2.Chosen == SkillKind.Nova && f2.Chosen2 == SkillKind.Stop ? "OK" : "!! SAI")}");
				}
				finally { Settings.Difficulty = savedDiff; SkillSet.RunChoice2 = savedSecond; }
			}
			SkillSet.RunChoice = SkillKind.Warp;
			Shot(new World(), "pick_hud_warp");
			SkillSet.RunChoice = null;

			using var form = new SkillPickForm(SkillPick.Cards());
			form.StartPosition = FormStartPosition.Manual; form.Location = new Point(40, 40);
			form.Show(); Application.DoEvents();
			form.Highlight(2); Application.DoEvents();
			Say($"  form chọn kĩ năng: Size={form.Size} dpi={form.DeviceDpi}, art={SkillPick.Cards()[0].Art.Width}x{SkillPick.Cards()[0].Art.Height}");
			using var bmp = new Bitmap(form.Width, form.Height);
			form.DrawToBitmap(bmp, new Rectangle(0, 0, form.Width, form.Height));
			bmp.Save(Path.Combine(_out, "pick_form.png"));
			form.Hide();
		}
		finally { SkillSet.RunChoice = saved; }
	}
	// ------------------------------------------------------------------ ảnh giáp mèo mang + thẻ nâng cấp có icon
	private static void TestArmorArt()
	{
		Say("== ARMOR ART (mèo mang giáp cấp 1 và 2, HUD, thẻ nâng cấp có icon)");
		foreach (var slot in Enum.GetValues<EquipSlot>())
			for (int lv = 1; lv <= 2; lv++)
				Say($"  item {slot} {lv}: {(EquipmentAssets.Item(slot, lv) is Bitmap b ? $"{b.Width}x{b.Height}" : "THIẾU")}");
		for (int lv = 1; lv <= 2; lv++)
		{
			var w = new World();
			w.Player.God = true;
			Cheats.Execute(w, "equip all " + lv);
			Shot(w, $"armor_full_{lv}");
			Say($"  Lv{lv}: giáp tổng {w.Player.Armor:0.#}, mũ={w.Player.Equipped[EquipSlot.Helm].Name}");

			using var bmp = new Bitmap(320, 320);
			using var g = Graphics.FromImage(bmp);
			using var fb = new FrameBuffer((int)Balance.ViewWidth, (int)Balance.ViewHeight);
			fb.G.Clear(Color.FromArgb(60, 100, 60));
			var canvas = new Canvas();
			canvas.Begin(fb.G, 1f, PointF.Empty, false);
			w.DrawWorld(canvas);
			g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
			g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
			g.DrawImage(fb.Bitmap, new Rectangle(0, 0, 320, 320), new Rectangle(304, 164, 32, 32), GraphicsUnit.Pixel);
			bmp.Save(Path.Combine(_out, $"armor_cat_{lv}.png"));
		}

		// thẻ nâng cấp: ép rút các thẻ trang bị
		var w2 = new World();
		var cards = new List<Upgrade>();
		foreach (var s in new[] { EquipSlot.Helm, EquipSlot.Shield, EquipSlot.Boots })
			cards.Add(new Upgrade { Title = $"Trang bị: {new Equipment(s, 2).Name} (mới)", Desc = Equipment.Describe(s, 2), Icon = EquipmentAssets.Item(s, 2) });
		using var form = new UpgradeForm(cards, 5);
		form.StartPosition = FormStartPosition.Manual;
		form.Location = new Point(40, 40);
		form.Show();
		Application.DoEvents();
		using var fbmp = new Bitmap(form.Width, form.Height);
		form.DrawToBitmap(fbmp, new Rectangle(0, 0, form.Width, form.Height));
		fbmp.Save(Path.Combine(_out, "armor_upgrade_form.png"));
		form.Hide();
	}
	private static void TestHolyShield()
	{
		Say("== HOLY SHIELD");
		var w = new World();
		var sh = new HolyShield(w.Player);
		w.Player.AddWeapon(sh);
		float hp0 = w.Player.Hp;
		Vector2 src = w.Player.Pos + new Vector2(20f, 0f);

		w.Player.TakeDamage(30f, src, 100f);
		Say($"  đòn 1: máu {hp0:0} -> {w.Player.Hp:0} (mong đợi giữ nguyên), lớp khiên còn {sh.Charges}/{sh.MaxCharges} (mong đợi 0/1)");

		for (int i = 0; i < 40; i++) w.Update(1f / 60f); // qua hết thời gian bất tử sau đỡ đòn
		w.Player.TakeDamage(30f, src, 100f);
		Say($"  đòn 2 (khiên đã vỡ): máu {w.Player.Hp:0} (mong đợi giảm)");

		for (int i = 0; i < 60 * 12 && sh.Charges == 0; i++) w.Update(1f / 60f);
		Say($"  sau khi chờ: lớp khiên {sh.Charges}/{sh.MaxCharges} (mong đợi 1/1 - đã hồi)");
		Shot(w, "shot_shield", 3f);

		// Khiên không tấn công: thẻ "Tốc độ bắn" chung không làm khiên hồi nhanh hơn, và thẻ riêng của nó chỉ nói về việc hồi khiên
		w.Player.FireRate = 2f;
		sh.RateMul = 1.5f;
		Say($"  Rate của khiên khi FireRate chung x2 và thẻ riêng x1.5: {sh.Rate:0.00} (mong đợi 1.50, không nhân thêm FireRate chung)");
		sh.RateMul = 1f; w.Player.FireRate = 1f;
		var titles = new HashSet<string>();
		for (int i = 0; i < 300; i++)
			foreach (var u in Upgrades.BuildChoices(w.Player)) if (u.Title.StartsWith("Holy Shield")) titles.Add(u.Title + " | " + u.Desc);
		Say("  thẻ nâng cấp của Holy Shield: " + string.Join(" || ", titles));
	}

	// ------------------------------------------------------------------ 3. chết + điểm
	private static void TestDeathAndScore()
	{
		Say("== DEATH");
		var w = new World();
		bool? ended = null;
		w.Ended += v => ended = v;
		w.Player.TakeDamage(1e6f, w.Player.Pos + new Vector2(20, 0), 0f);
		float startY = w.Player.Pos.Y, minY = startY; int frames = 0;
		Say($"  vừa chết: Dead={w.Player.Dead}, Ended={ended?.ToString() ?? "chưa"} (mong đợi chưa - đang chờ hiệu ứng rơi)");
		for (; frames < 60 * 5 && ended == null; frames++)
		{
			w.Update(1f / 60f);
			minY = Math.Min(minY, w.Player.Pos.Y);
			if (frames == 8 || frames == 20 || frames == 34 || frames == 48) Shot(w, $"shot_dead_{frames}", 1f);
		}
		Say($"  hiệu ứng chết: nảy lên cao {startY - minY:F0}px rồi rơi; báo kết thúc (victory={ended}) sau {frames / 60f:F2}s (mong đợi False, ~1,0-1,5s), điểm={w.State.ComputeScore(false)}");
	}

	// ------------------------------------------------------------------ 4. file
	private static void TestStorage()
	{
		Say("== STORAGE (System.IO + try/catch)");
		string tmp = Path.Combine(Path.GetTempPath(), "catsurvivor_selftest_scores.txt");
		File.Delete(tmp);
		Storage.ScoreFileOverride = tmp;
		var ok1 = Storage.AppendScore(new ScoreEntry { Name = "A|B", Score = 100, Seconds = 60, Level = 3, Kills = 7, Date = "2026-10-20 10:00" }, out string e1);
		var ok2 = Storage.AppendScore(new ScoreEntry { Name = "Tester Việt", Score = 900, Seconds = 90, Level = 9, Kills = 70, Victory = true, Date = "2026-10-20 10:05" }, out string e2);
		File.AppendAllText(tmp, "dòng hỏng không đúng định dạng\n");
		var list = Storage.ReadScores(out string e3);
		Say($"  ghi: {ok1}/{ok2} lỗi=({e1}),({e2}); đọc: {list.Count} dòng hợp lệ (bỏ dòng hỏng), thứ tự: {string.Join(" > ", list.Select(x => x.Name + ":" + x.Score))}, lỗi={e3 ?? "không"}");

		Storage.ScoreFileOverride = Path.GetTempPath().TrimEnd('\\'); // trỏ vào THƯ MỤC để ép lỗi ghi
		var okBad = Storage.AppendScore(new ScoreEntry(), out string eBad);
		Say($"  ĐƯỜNG LỖI (file là thư mục): ghi ok={okBad}, thông báo='{(eBad ?? "").Substring(0, Math.Min(80, (eBad ?? "").Length))}...' (không ném ngoại lệ ra ngoài)");
		Storage.ScoreFileOverride = null;
		Say($"  thư mục dữ liệu: {Storage.DataDir}");
		var team = Storage.ReadTeam(out string eTeam);
		Say($"  thông tin nhóm: {team.Count} dòng, lỗi={eTeam ?? "không"}");

		// thiếu asset: không văng
		var missing = AssetStore.Image("khong_ton_tai/abc.png");
		Say($"  thiếu asset -> hình thay thế {missing.Width}x{missing.Height}; báo cáo: {(AssetStore.TakeReport() ?? "(không)").Replace("\n", " | ")}");
	}

	// ------------------------------------------------------------------ cài đặt
	private static void TestSettings()
	{
		Say("== SETTINGS (settings.txt)");
		var (s0, m0, x0, f0) = (Settings.Sound, Settings.MusicVolume, Settings.SfxVolume, Settings.Fullscreen);
		Settings.Sound = false; Settings.MusicVolume = 35; Settings.SfxVolume = 80; Settings.Fullscreen = true;
		Settings.Save();
		string text = File.ReadAllText(Storage.PathOf(Storage.SettingsFile)).Replace("\r\n", " | ");
		Settings.Sound = true; Settings.MusicVolume = 0; Settings.SfxVolume = 0; Settings.Fullscreen = false;
		Settings.Load(out string err);
		Say($"  file: {text}");
		Say($"  đọc lại: sound={Settings.Sound} music={Settings.MusicVolume} sfx={Settings.SfxVolume} fullscreen={Settings.Fullscreen} lỗi={err ?? "không"} (mong đợi False/35/80/True)");
		SoundService.SetMusicVolume(50);
		Say($"  SetMusicVolume(50) -> {Settings.MusicVolume}; SetSfxVolume(120) -> {SetSfx()}");
		(Settings.Sound, Settings.MusicVolume, Settings.SfxVolume, Settings.Fullscreen) = (s0, m0, x0, f0); // trả lại như cũ
		Settings.Save();
		static int SetSfx() { SoundService.SetSfxVolume(120); return Settings.SfxVolume; }
	}

	// ------------------------------------------------------------------ 5. âm thanh
	private static void TestSound()
	{
		Say("== SOUND");
		Say($"  Enabled={SoundService.Enabled}, đã nạp {SoundService.LoadedCount} hiệu ứng + nhạc nền={SoundService.BgmLoaded}");
		SoundService.Play("kill", -4f, 0f);
		SoundService.Toggle();
		Say($"  Toggle -> Enabled={SoundService.Enabled}");
		SoundService.Toggle();
		Say($"  Toggle -> Enabled={SoundService.Enabled}");

		// Âm lượng thật sự có tác dụng không? Đo mức đỉnh của buffer đang gửi ra loa (SoundService.LastPeak)
		float Peak(int ms) { float m = 0f; var sw = Stopwatch.StartNew(); while (sw.ElapsedMilliseconds < ms) { m = Math.Max(m, SoundService.LastPeak); Thread.Sleep(5); } return m; }
		int savedMusic = Settings.MusicVolume, savedSfx = Settings.SfxVolume;
		bool savedOn = SoundService.Enabled;
		if (!savedOn) SoundService.SetEnabled(true);
		if (SoundService.BgmLoaded)
		{
			SoundService.SetMusicVolume(100); SoundService.SetSfxVolume(0);
			float p100 = Peak(300);
			SoundService.SetMusicVolume(50);
			float p50 = Peak(300);
			SoundService.SetMusicVolume(0);
			Peak(100);                                   // chờ buffer cũ phát hết
			float p0 = Peak(300);
			Say($"  nhạc nền: đỉnh ở 100% = {p100:0.000}, 50% = {p50:0.000}, 0% = {p0:0.000} (mong đợi giảm dần và 0% = 0.000)");

			// Hiệu ứng: âm lượng hiệu ứng 0 -> không phát; 100 -> có tiếng (nhạc nền đã tắt để chỉ đo hiệu ứng)
			SoundService.SetSfxVolume(0);
			SoundService.Play("slam", 0f, 0f);
			float s0 = Peak(300);
			SoundService.SetSfxVolume(100);
			SoundService.Play("slam", 0f, 0f);
			float s100 = Peak(300);
			Say($"  hiệu ứng (slam): đỉnh ở 0% = {s0:0.000}, 100% = {s100:0.000} (mong đợi 0.000 và > 0)");
		}
		else Say("  (không có nhạc nền/thiết bị âm thanh: bỏ qua phép đo âm lượng)");
		SoundService.SetMusicVolume(savedMusic);
		SoundService.SetSfxVolume(savedSfx);
		if (!savedOn) SoundService.SetEnabled(false);
	}

	// ------------------------------------------------------------------ 7. vòng lặp thật của GameForm
	private static void TestGameFormLoop()
	{
		Say("== GAMEFORM LOOP THẬT (Timer + vẽ lên màn hình, 6 giây)");
		var game = new GameForm();
		var w = game.CurrentWorld;
		var timer = new System.Windows.Forms.Timer { Interval = 500 };
		int ticks = 0;
		long f0 = 0;
		timer.Tick += (_, _) =>
		{
			ticks++;
			w.Input.KeyDown(Keys.D);
			if (ticks == 2) f0 = w.Frame;
			if (ticks == 4) { w.Player.Hp = w.Player.MaxHp; }
			if (ticks == 10)
			{
				using var bmp = new Bitmap(game.ClientSize.Width, game.ClientSize.Height);
				game.DrawToBitmap(bmp, new Rectangle(0, 0, game.Width, game.Height));
				bmp.Save(Path.Combine(_out, "form_game_live.png"));
				long frames = w.Frame - f0;
				Say($"  {frames} khung trong 4 giây thực => {frames / 4.0:F0} FPS (game time {w.State.Time:F1}s, cấp {w.State.Level}, quái {w.Enemies.Enemies.Count})");
				timer.Stop();
				game.Close();
			}
		};
		game.Shown += (_, _) => timer.Start();
		game.ShowDialog();
	}

	// ------------------------------------------------------------------ 6. form
	private static void TestForms()
	{
		Say("== FORMS (chụp ảnh)");
		void Snap(Form f, string name)
		{
			f.StartPosition = FormStartPosition.Manual;
			f.Location = new Point(40, 40);
			f.Show();
			Application.DoEvents();
			Say($"  {f.Name}: Size={f.Size} Client={f.ClientSize} dpi={f.DeviceDpi}");
			using var bmp = new Bitmap(f.Width, f.Height);
			f.DrawToBitmap(bmp, new Rectangle(0, 0, f.Width, f.Height));
			bmp.Save(Path.Combine(_out, name + ".png"));
			f.Close();
			f.Dispose();
		}

		var w = new World();
		Storage.ScoreFileOverride = null;
		Snap(new MainMenuForm(), "form_menu");
		Snap(new PauseForm(), "form_pause");
		Snap(new SettingsForm(), "form_settings");
		int savedDiff = Settings.Difficulty;
		Settings.Difficulty = 3;
		Snap(new SettingsForm(lockDifficulty: true), "form_settings_locked"); // mở từ menu Tạm dừng: không đổi được độ khó
		Settings.Difficulty = savedDiff;
		Settings.Save(); // form Cài đặt tự lưu khi đóng: ghi lại giá trị cũ để các lần chạy test sau không bị ảnh hưởng
		string tmpScores = Path.Combine(Path.GetTempPath(), "catsurvivor_selftest_board.txt");
		File.WriteAllLines(tmpScores, new[]
		{
			"Mèo Con|9200|1020|27|420|1|2026-10-18 20:15", "Bạch Tuyết|3150|780|22|290|0|2026-10-18 21:40",
			"Tí Nị|1480|450|15|180|0|2026-10-19 09:05", "Tester Việt|900|300|9|70|0|2026-10-20 10:05", "Một cái tên rất là dài dòng|120|60|3|7|0|2026-10-20 11:00",
		});
		Storage.ScoreFileOverride = tmpScores;
		Snap(new LeaderboardForm(new ScoreEntry { Name = "Tester Việt", Score = 900, Date = "2026-10-20 10:05" }), "form_leaderboard");
		Storage.ScoreFileOverride = null;
		Snap(new HelpForm(), "form_help");
		Snap(new AboutForm(), "form_about");
		Snap(new GameOverForm(w.State, false), "form_gameover");
		Snap(new UpgradeForm(Upgrades.BuildChoices(w.Player), 2), "form_upgrade");
		var longChoices = new List<Upgrade>
		{
			new() { Title = "Giáp +2", Desc = "Giảm mỗi đòn nhận vào: 0 -> 2 (trần giáp gốc 10), mèo nặng hơn", Apply = () => { } },
			new() { Title = "★ HỢP NHẤT: Magic Bolt + Orbit Blades", Desc = "-> Star Ring: quả cầu xoay quanh người, tự bắn đạn vào quái gần nhất", IsFusion = true, Apply = () => { } },
			new() { Title = "Magic Bolt: bắn nhanh +25%", Desc = "Tốc độ đánh riêng của vũ khí này: x1.00 -> x1.25 (trần x2)", Apply = () => { } },
		};
		Snap(new UpgradeForm(longChoices, 12), "form_upgrade_long");
		Say("  đã chụp 7 form");
	}
}
