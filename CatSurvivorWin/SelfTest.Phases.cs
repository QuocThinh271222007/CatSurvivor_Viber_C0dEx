using System.Numerics;

namespace CatSurvivor;

// Kiểm tra boss 3 giai đoạn (mỗi giai đoạn một thanh máu, giai đoạn 3 có hai mắt chính + 8 mắt con): CatSurvivor.exe --selftest --phases --out <thư_mục>
internal static partial class SelfTest
{
	private static List<Enemy> Bosses(World w) => w.Enemies.Enemies.Where(e => e.Kind == EnemyTypes.Boss).OrderBy(e => e.Boss.Index).ToList();

	private static void Run(World w, float seconds)
	{
		int n = (int)(seconds * 60f);
		for (int i = 0; i < n; i++) { w.Player.Weapons.Clear(); w.Update(1f / 60f); w.Player.Hp = w.Player.MaxHp; }   // không vũ khí: chỉ đánh boss bằng tay (Damage) trong test
	}

	/// <summary>Đánh cạn thanh máu hiện tại của một mắt chính bằng các đòn như người chơi (qua EnemyManager.Damage).</summary>
	private static void DrainBar(World w, Enemy boss)
	{
		if (boss.Boss.State == 4) { /* đang choáng: chịu thêm sát thương, vẫn dùng chung hàm */ }
		for (int i = 0; i < 400 && !boss.Dead && !boss.Boss.BarEmpty && boss.Hp > 0f; i++)
		{
			w.Enemies.Damage(boss, 200f, Vector2.Zero);
			if (boss.Boss.ShieldUp) break;
		}
	}

	private static void KillMinisOf(World w, Enemy boss)
	{
		foreach (var m in w.Enemies.MinisOf(boss).ToList()) w.Enemies.Damage(m, 1e6f, Vector2.Zero);
	}

	private static void ShotHud(World w, string name)
	{
		using var fb = new FrameBuffer((int)Balance.ViewWidth, (int)Balance.ViewHeight);
		fb.G.Clear(Color.FromArgb(60, 100, 60));
		var cv = new Canvas();
		cv.Begin(fb.G, 1f, PointF.Empty, false);
		w.DrawWorld(cv);
		w.DrawOverlay(cv);
		using var bmp = new Bitmap((int)Balance.ViewWidth * 2, (int)Balance.ViewHeight * 2);
		using (var g = Graphics.FromImage(bmp)) fb.BlitTo(g, new Rectangle(0, 0, bmp.Width, bmp.Height));
		bmp.Save(Path.Combine(_out, name + ".png"));
	}

	private static void TestPhases()
	{
		Say("== BOSS 3 GIAI ĐOẠN (mỗi giai đoạn một thanh máu)");
		int savedDiff = Settings.Difficulty;
		bool ok = true;
		try
		{
			// ---- chỉ số theo độ khó ----
			foreach (int d in new[] { 0, 1, 2, 3 })
			{
				Settings.Difficulty = d;
				var mini = EnemyTypes.Create(EnemyTypes.MiniEye, 0f, Vector2.Zero);
				var boss = EnemyTypes.Create(EnemyTypes.Boss, 0f, Vector2.Zero);
				Say($"  {Difficulty.Name((DifficultyLevel)d),-11}: máu mắt con {mini.MaxHp:0} (trước đây {110f * Difficulty.EnemyHpMul:0}), máu mỗi thanh boss {boss.MaxHp:0}, gọi bù mắt con mỗi {Difficulty.BossResummonInterval:0}s");
				ok &= mini.MaxHp > 110f * Difficulty.EnemyHpMul && MathF.Abs(boss.MaxHp - Balance.BossBarHp * Difficulty.EnemyHpMul) < 0.01f;
				ok &= (d >= 2) == (Difficulty.BossResummonInterval == 40f);
			}

			// ---- luồng 3 giai đoạn ở độ Dễ ----
			Settings.Difficulty = 0;
			var w = new World();
			w.Player.God = true;
			w.Player.Weapons.Clear();
			bool? ended = null;
			w.Ended += v => ended = v;
			Cheats.Execute(w, "boss");
			Run(w, 3.2f);
			var A = w.Arena.Boss;
			ok &= A != null && A.Boss.Stage == 1 && A.Hp > A.MaxHp * 0.9f && Bosses(w).Count == 1;
			Say($"  giai đoạn 1: boss {A.Hp:0}/{A.MaxHp:0}, stage={A.Boss.Stage}, số mắt chính={Bosses(w).Count}");
			ShotHud(w, "phase1_hud");

			// cạn thanh 1 -> giai đoạn 2, thanh đầy lại, 4 mắt con, bất khả xâm phạm
			DrainBar(w, A);
			ok &= A.Boss.BarEmpty && !A.Dead;
			Run(w, 3f);
			ok &= A.Boss.Stage == 2 && A.Hp == A.MaxHp && A.Boss.ShieldUp && w.Enemies.MiniCount(A) == 4 && !A.Dead;
			Say($"  cạn thanh 1 -> stage={A.Boss.Stage}, thanh đầy lại {A.Hp:0}/{A.MaxHp:0}, khiên={A.Boss.ShieldUp}, mắt con={w.Enemies.MiniCount(A)}");
			float hpBefore = A.Hp;
			w.Enemies.Damage(A, 500f, Vector2.Zero);
			ok &= A.Hp == hpBefore;                                       // còn mắt con: không mất máu
			ShotHud(w, "phase2_hud");

			// diệt 4 mắt con -> lộ sơ hở -> cạn thanh 2 -> giai đoạn 3 với mắt chính thứ hai
			KillMinisOf(w, A);
			Run(w, 0.3f);
			ok &= !A.Boss.ShieldUp;
			DrainBar(w, A);
			ok &= A.Boss.BarEmpty;
			Run(w, 0.2f);
			var all = Bosses(w);
			Say($"  cạn thanh 2 -> số mắt chính={all.Count} (mong đợi 2), A stage={A.Boss.Stage} thanh {A.Hp:0}/{A.MaxHp:0}");
			ok &= all.Count == 2 && A.Boss.Stage == 3 && A.Hp == A.MaxHp;
			var B = all.Count > 1 ? all[1] : null;
			ok &= B != null && B.Boss.Index == 1 && B.Boss.Stage == 3 && B.MaxHp == A.MaxHp && B.Hp == B.MaxHp;
			Run(w, 5f);                                                   // B xuất hiện (2,2s) rồi gầm gọi mắt con
			int ma = w.Enemies.MiniCount(A), mb = w.Enemies.MiniCount(B), total = w.Enemies.MiniCount();
			Say($"  giai đoạn 3: mắt con của A={ma}, của B={mb}, tổng={total} (mong đợi 4 + 4 = 8); A khiên={A.Boss.ShieldUp}, B khiên={B.Boss.ShieldUp}");
			ok &= ma == 4 && mb == 4 && total == 8 && A.Boss.ShieldUp && B.Boss.ShieldUp;
			ShotHud(w, "phase3_hud");

			// chỉ diệt mắt con của A: A lộ sơ hở, B vẫn khiên
			KillMinisOf(w, A);
			Run(w, 0.3f);
			ok &= !A.Boss.ShieldUp && B.Boss.ShieldUp && w.Enemies.MiniCount(B) == 4;
			float hb = B.Hp;
			w.Enemies.Damage(B, 500f, Vector2.Zero);
			ok &= B.Hp == hb;
			DrainBar(w, A);                                              // cạn thanh cuối của A -> A chết, mắt chính B vẫn sống
			Run(w, 1f);
			ok &= A.Dead && !B.Dead && ended == null;
			Say($"  A hết thanh cuối: A chết={A.Dead}, B sống={!B.Dead}, trận chưa kết thúc={ended == null}");

			// diệt mắt con của B, cạn thanh B -> chiến thắng
			KillMinisOf(w, B);
			Run(w, 0.3f);
			DrainBar(w, B);
			Run(w, 5f);
			ok &= B.Dead && ended == true;
			Say($"  B chết -> thắng={ended}");

			// ---- gọi bù mắt con: Dễ không có, Khó có (mỗi 40s) ----
			foreach (int d in new[] { 0, 2 })
			{
				Settings.Difficulty = d;
				var w2 = new World();
				w2.Player.God = true;
				w2.Player.Weapons.Clear();
				Cheats.Execute(w2, "boss");
				Run(w2, 3.2f);
				var a2 = w2.Arena.Boss;
				Cheats.Execute(w2, "stage2");
				Run(w2, 4f);
				KillMinisOf(w2, a2);
				var two = w2.Enemies.MinisOf(a2).Count();
				// hồi sinh 2 con rồi diệt 2 con: còn 2
				Run(w2, 0.2f);
				ok &= two == 0;
				for (int k = 0; k < 11; k++)
				{
					Run(w2, 4f);
					Say($"      t+{(k + 1) * 4}s: mắt con={w2.Enemies.MiniCount(a2)} state={a2.Boss.State} stage={a2.Boss.Stage} minisSpawned={a2.Boss.MinisSpawned} cleared={a2.Boss.MinisCleared}");
				}
				int after = w2.Enemies.MiniCount(a2);
				Say($"  {Difficulty.Name((DifficultyLevel)d),-10}: diệt hết mắt con rồi chờ 44s -> mắt con = {after} (mong đợi {(d == 2 ? 4 : 0)}), khiên={a2.Boss.ShieldUp}");
				ok &= d == 2 ? after == 4 && a2.Boss.ShieldUp : after == 0 && !a2.Boss.ShieldUp;
			}

			// bossdie hạ mọi mắt chính
			Settings.Difficulty = 0;
			var w3 = new World();
			w3.Player.God = true;
			bool? ended3 = null;
			w3.Ended += v => ended3 = v;
			Cheats.Execute(w3, "boss");
			Run(w3, 3.2f);
			Cheats.Execute(w3, "stage3");
			Run(w3, 5f);
			ok &= Bosses(w3).Count == 2;
			Cheats.Execute(w3, "bossdie");
			Run(w3, 5f);
			Say($"  stage3 rồi bossdie: số mắt chính={Bosses(w3).Count}, thắng={ended3}");
			ok &= ended3 == true;
		}
		finally { Settings.Difficulty = savedDiff; }
		Say($"  -> {(ok ? "OK" : "!! SAI")}");
	}
}
