using System.Diagnostics;

namespace CatSurvivor;

// Đo chi phí đưa khung hình lên cửa sổ theo kích thước (cửa sổ thường vs toàn màn hình): CatSurvivor.exe --selftest --present --out <thư_mục>
internal static partial class SelfTest
{
	private static void TestPresent()
	{
		Say("== PRESENT: vẽ khung 640x360 vs đưa lên cửa sổ các cỡ khác nhau (ms/khung)");
		var w = new World();
		w.Player.God = true;
		for (int i = 0; i < 60 * 45; i++) { w.Player.Hp = w.Player.MaxHp; w.Update(1f / 60f); }
		Say($"  quái trên bản đồ: {w.Enemies.Enemies.Count}");

		using (var fb = new FrameBuffer((int)Balance.ViewWidth, (int)Balance.ViewHeight))
		{
			var cv = new Canvas();
			for (int i = 0; i < 20; i++) { fb.G.Clear(Color.Black); cv.Begin(fb.G, 1f, PointF.Empty, false); w.DrawWorld(cv); w.DrawOverlay(cv); }
			long t0 = Stopwatch.GetTimestamp();
			for (int i = 0; i < 100; i++) { fb.G.Clear(Color.Black); cv.Begin(fb.G, 1f, PointF.Empty, false); w.DrawWorld(cv); w.DrawOverlay(cv); }
			Say($"  dựng khung 640x360 (thế giới + HUD): {(Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency / 100:F2} ms");
		}

		var refresh = typeof(GameForm).GetMethod("DisplayRefreshRate", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
		Say($"  tần số quét màn hình game đọc được: {refresh.Invoke(null, null)} Hz");
		foreach (var (full, timerLoop) in new[] { (true, true), (true, false), (false, false) })
		{
			Settings.Fullscreen = full;
			GameForm.ForceTimerLoop = timerLoop;
			var game = new GameForm();
			var gw = game.CurrentWorld;
			gw.Player.God = true;
			for (int i = 0; i < 120; i++) gw.Enemies.Enemies.Add(EnemyTypes.Create(i % 4, 0f, gw.Player.Pos + V.FromAngle(i * 0.7f) * (90f + i)));
			game.FrameStamps = new List<double>();
			var probe = new System.Windows.Forms.Timer { Interval = 250 };
			var sw = Stopwatch.StartNew();
			probe.Tick += (_, _) =>
			{
				gw.Player.Hp = gw.Player.MaxHp;
				if (sw.Elapsed.TotalSeconds > 8)
				{
					probe.Stop();
					var st = game.FrameStamps.Where(x => x - game.FrameStamps[0] > 2000).ToList();
					var gaps = new List<double>();
					for (int i = 1; i < st.Count; i++) gaps.Add(st[i] - st[i - 1]);
					gaps.Sort();
					if (gaps.Count > 10)
						Say($"  vòng lặp {(timerLoop ? "Timer cũ  " : "Idle+DWM  ")} {(full ? "TOÀN MÀN HÌNH" : "cửa sổ     ")} ({game.ClientSize.Width}x{game.ClientSize.Height}): update {game.UpdateTicks * 1000.0 / Stopwatch.Frequency / game.FrameStamps.Count:F1} ms + vẽ {game.PaintTicks * 1000.0 / Stopwatch.Frequency / game.FrameStamps.Count:F1} ms/khung · {gaps.Count / ((st[^1] - st[0]) / 1000.0):F0} khung/giây · khoảng cách giữa 2 khung (ms): min {gaps[0]:F1} · trung vị {gaps[gaps.Count / 2]:F1} · p95 {gaps[(int)(gaps.Count * 0.95)]:F1} · p99 {gaps[(int)(gaps.Count * 0.99)]:F1} · max {gaps[^1]:F1}");
					game.Close();
				}
			};
			game.Shown += (_, _) => probe.Start();
			game.ShowDialog();
		}
		Settings.Fullscreen = false;
		GameForm.ForceTimerLoop = false;

		foreach (var (cw, ch) in new[] { (1280, 720), (1920, 1080), (1536, 864), (1366, 768), (1600, 900), (2560, 1440) })
		{
			using var form = new Form { FormBorderStyle = FormBorderStyle.None, StartPosition = FormStartPosition.Manual, Bounds = new Rectangle(0, 0, cw, ch) };
			var canvas = new GameCanvas { Dock = DockStyle.Fill, World = w };
			form.Controls.Add(canvas);
			form.Show();
			Application.DoEvents();
			string mode = "?";
			for (int i = 0; i < 10; i++) { canvas.Invalidate(); canvas.Update(); }
			long t0 = Stopwatch.GetTimestamp();
			int n = 120;
			for (int i = 0; i < n; i++) { canvas.Invalidate(); canvas.Update(); }
			double ms = (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency / n;
			Say($"  {cw}x{ch}: cả khung (dựng + phóng + lên màn hình) {ms:F2} ms  ({canvas.ClientSize.Width}x{canvas.ClientSize.Height}) {mode}");
			form.Close();
		}
	}
}
