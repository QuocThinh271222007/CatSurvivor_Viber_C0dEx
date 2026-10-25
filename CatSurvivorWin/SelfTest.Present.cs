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

		foreach (bool full in new[] { false, true })
		{
			Settings.Fullscreen = full;
			var game = new GameForm();
			var gw = game.CurrentWorld;
			gw.Player.God = true;
			for (int i = 0; i < 120; i++) gw.Enemies.Enemies.Add(EnemyTypes.Create(i % 5, 0f, gw.Player.Pos + V.FromAngle(i * 0.7f) * (90f + i)));
			var timer = new System.Windows.Forms.Timer { Interval = 500 };
			int ticks = 0; long f0 = 0, p0 = 0;
			var canvasCtl = game.Controls.OfType<GameCanvas>().FirstOrDefault() ?? game.Controls.Find("canvas", true).OfType<GameCanvas>().First();
			int paints = 0;
			canvasCtl.Paint += (_, _) => paints++;
			timer.Tick += (_, _) =>
			{
				ticks++;
				gw.Player.Hp = gw.Player.MaxHp;
				if (ticks == 3) { f0 = gw.Frame; p0 = paints; }
				if (ticks == 11)
				{
					Say($"  vòng lặp thật {(full ? "TOÀN MÀN HÌNH" : "cửa sổ     ")}: client {canvasCtl.ClientSize.Width}x{canvasCtl.ClientSize.Height}, {(gw.Frame - f0) / 4.0:F0} khung/giây (mô phỏng), {(paints - p0) / 4.0:F0} lần vẽ/giây, quái {gw.Enemies.Enemies.Count}");
					timer.Stop();
					game.Close();
				}
			};
			game.Shown += (_, _) => timer.Start();
			game.ShowDialog();
		}
		Settings.Fullscreen = false;

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
