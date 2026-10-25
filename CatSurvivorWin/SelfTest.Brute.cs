using System.Numerics;

namespace CatSurvivor;

// Kiểm tra Brute có khiên: CatSurvivor.exe --selftest --brute --out <thư mục>
internal static partial class SelfTest
{
	private static Enemy SpawnLoneBrute(World w, Vector2 offset, float size = 1f)
	{
		w.Enemies.Enemies.Clear();
		w.Player.God = true;
		w.Player.Weapons.Clear();
		var b = EnemyTypes.Create(EnemyTypes.Brute, 200f, w.Player.Pos + offset, size, 0);
		w.Enemies.Enemies.Add(b);
		return b;
	}

	private static void TestBrute()
	{
		Say("== BRUTE CÓ KHIÊN");
		const float dt = 1f / 60f;
		bool ok = true;

		// 1) khiên hấp thụ đòn: máu đứng yên tới khi khiên vỡ, sau đó mới mất máu
		{
			var w = new World();
			var b = SpawnLoneBrute(w, new Vector2(300f, 0f));
			float hp0 = b.Hp, sh0 = b.ShieldHp;
			Say($"  brute: máu {b.MaxHp:0}, khiên {b.ShieldMax:0} (= {EnemyTypes.BruteShieldRatio:0.##} x máu), giáp {b.Armor:0.0}");
			ok &= sh0 > 0f && MathF.Abs(sh0 - b.MaxHp * EnemyTypes.BruteShieldRatio) < 0.01f;
			int hits = 0;
			while (!b.ShieldBroken && hits < 500)
			{
				w.Enemies.Damage(b, 12f, Vector2.Zero);
				hits++;
				if (!b.ShieldBroken) ok &= b.Hp == hp0;      // còn khiên: máu không đổi
				for (int i = 0; i < 10; i++) w.Update(dt);   // hết khựng
			}
			Say($"  đánh {hits} đòn (12 sát thương, trừ giáp): khiên vỡ={b.ShieldBroken}, máu trước khi vỡ={hp0:0} -> {b.Hp:0}");
			ok &= b.ShieldBroken && b.ShieldHp == 0f;
			float hpAfter = b.Hp;
			w.Enemies.Damage(b, 12f, Vector2.Zero);
			Say($"  đòn kế tiếp (khiên đã vỡ): máu {hpAfter:0} -> {b.Hp:0}");
			ok &= b.Hp < hpAfter;
		}

		// 2) còn khiên: brute vẫn gồng + lao tới
		{
			var w = new World();
			var b = SpawnLoneBrute(w, new Vector2(120f, 0f));
			bool windup = false, charge = false;
			for (int i = 0; i < 60 * 10; i++)
			{
				w.Player.Pos = new Vector2(0f, 0f) + (w.Player.Pos - w.Player.Pos); // mèo đứng yên (không bị đẩy)
				w.Update(dt);
				w.Player.Hp = w.Player.MaxHp;
				windup |= b.ChargeState == 1; charge |= b.ChargeState == 2;
			}
			Say($"  còn khiên, 10s gần mèo: có gồng={windup}, có lao={charge}");
			ok &= windup && charge;
		}

		// 3) khiên vỡ: KHÔNG gồng/lao nữa nhưng vẫn đi theo mèo
		{
			var w = new World();
			var b = SpawnLoneBrute(w, new Vector2(240f, 0f));
			w.Enemies.Damage(b, b.ShieldMax + 50f, Vector2.Zero);          // một đòn đủ phá khiên
			ok &= b.ShieldBroken;
			for (int i = 0; i < 20; i++) w.Update(dt);
			b.Hp = b.MaxHp;
			float d0 = (b.Pos - w.Player.Pos).Length();
			bool anyCharge = false; float maxSpeedSeen = 0f; Vector2 last = b.Pos;
			for (int i = 0; i < 60 * 12; i++)
			{
				w.Update(dt);
				w.Player.Hp = w.Player.MaxHp;
				anyCharge |= b.ChargeState != 0;
				maxSpeedSeen = MathF.Max(maxSpeedSeen, (b.Pos - last).Length() / dt);
				last = b.Pos;
			}
			float d1 = (b.Pos - w.Player.Pos).Length();
			Say($"  khiên vỡ, 12s: có gồng/lao={anyCharge}, khoảng cách tới mèo {d0:0} -> {d1:0}, tốc độ cao nhất ~{maxSpeedSeen:0} px/s (tốc độ đi {b.Speed:0})");
			ok &= !anyCharge && d1 < d0 && maxSpeedSeen < 80f;
		}

		// 4) phá khiên đúng lúc đang gồng/lao: huỷ ngay đòn lao
		foreach (int state in new[] { 1, 2, 3 })
		{
			var w = new World();
			var b = SpawnLoneBrute(w, new Vector2(120f, 0f));
			b.ChargeState = state; b.ChargeTimer = 0.5f; b.ChargeDir = Vector2.UnitX;
			w.Enemies.Damage(b, b.ShieldMax + 5f, Vector2.Zero);
			Say($"  phá khiên khi ChargeState={state}: -> ChargeState={b.ChargeState}");
			ok &= b.ChargeState == 0 && b.ShieldBroken;
		}

		// 5) killall (sát thương cực lớn) vẫn giết được brute dù có khiên
		{
			var w = new World();
			var b = SpawnLoneBrute(w, new Vector2(200f, 0f));
			Cheats.Execute(w, "killall");
			Say($"  killall: brute chết={b.Dead}");
			ok &= b.Dead;
		}

		// 6) ảnh: các tư thế (đi có khiên, gồng, kiệt sức, vừa bị đánh khiên, vỡ khiên, đi không khiên, trúng đòn khi lộ máu)
		Say("  -- chụp ảnh các tư thế (brute_states.png) --");
		{
			var w = new World();
			var b = SpawnLoneBrute(w, new Vector2(0f, 0f), 1.3f);
			w.Player.Pos = b.Pos + new Vector2(-400f, 0f);               // cất mèo ra xa
			w.Cam = b.Pos;
			var crops = new List<(string, Bitmap)>();
			void Grab(string label)
			{
				w.Cam = b.Pos;                                   // camera bám brute (nó đang đi về phía mèo ở xa)
				using var fb = new FrameBuffer((int)Balance.ViewWidth, (int)Balance.ViewHeight);
				fb.G.Clear(Color.FromArgb(60, 100, 60));
				var cv = new Canvas();
				cv.Begin(fb.G, 1f, PointF.Empty, false);
				w.DrawWorld(cv);
				var tl = w.Cam - World.ViewSize / 2f;
				var c = b.Pos - tl;
				var src = new Rectangle((int)c.X - 40, (int)c.Y - 40, 80, 70);
				var bmp = new Bitmap(src.Width * 4, src.Height * 4);
				using (var g = Graphics.FromImage(bmp))
				{
					g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor; g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
					g.DrawImage(fb.Bitmap, new Rectangle(0, 0, bmp.Width, bmp.Height), src, GraphicsUnit.Pixel);
					g.DrawString(label, new Font("Arial", 9f, FontStyle.Bold), Brushes.Yellow, 4, 4);
				}
				crops.Add((label, bmp));
			}
			void Run(int ticks) { for (int i = 0; i < ticks; i++) { w.Player.Pos = b.Pos + new Vector2(-400f, 0f); w.Update(dt); } }
			b.ChargeCd = 99f; b.FaceX = -1f;
			Run(5);   Grab("di co khien 1");
			Run(12);  Grab("di co khien 2");
			b.ChargeState = 1; b.ChargeTimer = 0.7f; Run(1); Grab("gong (frame 0)");
			b.ChargeState = 3; b.ChargeTimer = 1f; Run(1); Grab("kiet suc (8)");
			b.ChargeState = 0; Run(2);
			w.Enemies.Damage(b, 3f, Vector2.Zero); Run(2); Grab("khien bi danh");
			w.Enemies.Damage(b, b.ShieldMax + 20f, Vector2.Zero); Run(6); Grab("khien vo");
			Run(40); b.FaceX = 1f; Grab("di khong khien (quay phai)");
			b.FaceX = -1f;
			b.Hp = b.MaxHp; w.Enemies.Damage(b, 3f, Vector2.Zero); Grab("trung don (trang)");
			int cols = 4, cw = crops[0].Item2.Width, ch = crops[0].Item2.Height;
			using var sheet = new Bitmap(cols * cw, ((crops.Count + cols - 1) / cols) * ch);
			using (var g = Graphics.FromImage(sheet))
			{
				g.Clear(Color.Black);
				for (int i = 0; i < crops.Count; i++) g.DrawImage(crops[i].Item2, (i % cols) * cw, (i / cols) * ch);
			}
			sheet.Save(Path.Combine(_out, "brute_states.png"));
			foreach (var c in crops) c.Item2.Dispose();
		}

		Say($"  -> {(ok ? "OK" : "!! SAI")}");
	}
}
