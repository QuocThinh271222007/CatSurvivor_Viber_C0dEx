using System.Numerics;

namespace CatSurvivor;

// Chụp hitbox các đòn đánh của quái (Cài đặt > Hiện hitbox) và khung trắng của mắt con: CatSurvivor.exe --selftest --hitbox --out <thư_mục>
internal static partial class SelfTest
{
	private static Enemy Put(World w, int kind, Vector2 offset)
	{
		var e = EnemyTypes.Create(kind, 0f, w.Player.Pos + offset);
		w.Enemies.Enemies.Add(e);
		return e;
	}

	private static void TestHitbox()
	{
		Say("== HITBOX ĐÒN ĐÁNH CỦA QUÁI + KHUNG TRẮNG MẮT CON");
		int savedDiff = Settings.Difficulty;
		bool savedBox = Settings.ShowHitbox;
		try
		{
			Settings.Difficulty = 2;          // Khó: không có vùng báo hiệu, chỉ còn hitbox
			Settings.ShowHitbox = true;
			var w = new World();
			w.Player.God = true;
			w.Player.Weapons.Clear();
			w.Cam = w.Player.Pos;

			var bat = Put(w, EnemyTypes.Bat, new Vector2(-170f, -90f)); bat.ScreechTime = 0.1f;
			var brute = Put(w, EnemyTypes.Brute, new Vector2(60f, -110f)); brute.ChargeState = 1; brute.ChargeDir = new Vector2(1f, 0.35f).Normalized(); brute.ChargeTimer = 0.3f;
			var slime = Put(w, EnemyTypes.Slime, new Vector2(-150f, 80f)); slime.PounceState = 1; slime.PounceDir = new Vector2(1f, -0.2f).Normalized(); slime.PounceTimer = 0.3f;
			var bomb = Put(w, EnemyTypes.Bomber, new Vector2(90f, 90f)); bomb.FuseTime = 0.2f;
			var mini = Put(w, EnemyTypes.MiniEye, new Vector2(-20f, -40f));
			var mini2 = Put(w, EnemyTypes.MiniEye, new Vector2(30f, -40f)); mini2.Flash = 1f;
			w.Enemies.Bolts.Add(new EyeBolt { Pos = w.Player.Pos + new Vector2(0f, 40f), Dir = new Vector2(1f, 0f), Life = 5f, Age = 1f });
			ShotHud(w, "hitbox_normal");

			// boss: chùm tia chữ thập đang quét, vòng giậm
			w = new World();
			w.Player.God = true;
			w.Player.Weapons.Clear();
			Cheats.Execute(w, "boss");
			Run(w, 3.2f);
			Cheats.Execute(w, "stage2");
			Run(w, 3f);
			var a = w.Arena.Boss;
			w.Cam = a.Pos;
			a.Boss.CrossState = 2;
			a.Boss.CrossAngle = 0.4f;
			ShotHud(w, "hitbox_cross");
			Say("  đã chụp hitbox_normal.png, hitbox_cross.png");
		}
		finally { Settings.Difficulty = savedDiff; Settings.ShowHitbox = savedBox; }
	}
}
