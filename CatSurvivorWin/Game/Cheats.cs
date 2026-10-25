using System.Globalization;
using System.Numerics;
using System.Text;

namespace CatSurvivor;

/// <summary>
/// Cheat code để debug: gõ lệnh vào console (phím F1 hoặc ` trong lúc chơi), xem `help` để biết danh sách.
/// CHỈ bật ở bản Debug (F5 trong Visual Studio); bản Release tự tắt hoàn toàn nên không ảnh hưởng khi nộp/demo.
/// Chỉ chứa logic - form hiển thị là CheatForm.
/// </summary>
public static class Cheats
{
#if DEBUG
	public const bool Enabled = true;
#else
	public const bool Enabled = false;
#endif

	/// <summary>Kết quả một lệnh. KeepOpen: giữ console mở (help, info, lỗi) thay vì đóng để chơi tiếp.</summary>
	public readonly record struct Result(string Message, bool Ok, bool KeepOpen = false);

	public const string HelpText =
		"god [on|off]            bất tử\r\n" +
		"heal                    hồi đầy máu + dash\r\n" +
		"xp <n>                  cộng n XP (lên cấp thì hiện menu nâng cấp)\r\n" +
		"level <n>               lên thẳng cấp n (1-27), tự chọn nâng cấp ngẫu nhiên; level 27 = mở boss\r\n" +
		"boss                    = level 27, vào trận boss ngay\r\n" +
		"bosshp <n>              đặt máu boss;  bossdie: giết boss ngay\r\n" +
		"stage2 | stage3         cạn thanh máu boss hiện tại -> sang giai đoạn 2 (4 mắt con) / 3 (thêm mắt chính thứ hai) ngay\r\n" +
		"weapon <bow|knife|claw|shield|cross|all> [cấp]   thêm vũ khí / đặt cấp (mặc định 8)\r\n" +
		"fuse                    hợp nhất mọi cặp vũ khí đủ điều kiện (cần 2 vũ khí cấp >= 5)\r\n" +
		"equip <helm|body|boots|shield|all> [cấp]   mặc giáp (mặc định cấp 5)\r\n" +
		"stats max               tăng mọi chỉ số tới trần (gồm tốc độ bắn)\r\n" +
		"firerate <x>            đặt tốc độ bắn chung (1-2)\r\n" +
		"autoaim [on|off]       bật/tắt tự động ngắm (cũng là phím F khi chơi; Claw & Wave Slash luôn ngắm bằng chuột)\r\n" +
		"hitbox [on|off]        hiện/ẩn hitbox (cũng có trong Cài đặt)\r\n" +
		"spawn <slime|bat|bomber|brute> [số] [special|kích_cỡ]   tạo quái quanh mèo\r\n" +
		"formation <ring|wall|vflock|pincer|squad|escort>        tạo đội hình ngay\r\n" +
		"killall                 giết mọi quái (trừ boss)\r\n" +
		"gems <n>                rải n viên XP quanh mèo\r\n" +
		"time <giây>             đặt thời gian chơi (quái mạnh/đội hình mở khoá theo thời gian)\r\n" +
		"timescale <0.1-10>      tốc độ game (1 = bình thường)\r\n" +
		"dash                    hồi đầy lượt dash\r\n" +
		"info                    xem trạng thái hiện tại";

	public static Result Execute(World w, string line)
	{
		var parts = (line ?? "").Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
		if (parts.Length == 0) return new Result("", false, true);
		string cmd = parts[0].ToLowerInvariant();
		string arg(int i) => i < parts.Length ? parts[i].ToLowerInvariant() : "";
		var p = w.Player;
		var gs = w.State;

		try
		{
			if (cmd == "help" || cmd == "?") return new Result(HelpText, true, true);
			if (cmd == "info") return new Result(Info(w), true, true);

			gs.CheatsUsed = true;
			switch (cmd)
			{
				case "god":
					p.God = arg(1) == "off" ? false : arg(1) == "on" ? true : !p.God;
					return new Result($"God mode: {(p.God ? "BẬT" : "TẮT")}", true);

				case "heal":
					p.Hp = p.MaxHp;
					p.Mana = p.MaxMana;
					p.RefillDash();
					return new Result("Đã hồi đầy máu, mana và dash", true);

				case "xp":
					if (!Num(arg(1), out float xp) || xp <= 0f) return Bad("xp <số dương>");
					gs.AddXp(xp);
					return new Result($"+{xp:0.#} XP (cấp {gs.Level})", true);

				case "level":
					if (!Num(arg(1), out float lv)) return Bad("level <1-27>");
					return new Result(GoToLevel(w, (int)lv), true);

				case "boss":
					return new Result(GoToLevel(w, GameState.MaxLevel), true);

				case "bosshp":
				{
					var b = w.Arena.Boss;
					if (b == null || b.Dead) return Bad("chưa có boss (gõ: boss)");
					if (!Num(arg(1), out float hp) || hp <= 0f) return Bad("bosshp <số dương>");
					b.Hp = MathF.Min(hp, b.MaxHp);
					return new Result($"Máu thanh hiện tại của boss = {b.Hp:0}", true);
				}

				case "stage2":
				case "stage3":
				{
					var b = w.Arena.Boss;
					if (b == null || b.Dead) return Bad("chưa có boss (gõ: boss)");
					int target = cmd == "stage3" ? 3 : 2;
					if (b.Boss.State == 0) b.Boss.State = 1;
					if (b.Boss.Stage >= target) return Bad($"boss đã ở giai đoạn {b.Boss.Stage}");
					if (target == 3 && b.Boss.Stage == 1)
					{
						b.Boss.Stage = 2; b.Boss.ShieldUp = false; b.Boss.MinisSpawned = true; b.Boss.MinisCleared = true;   // bỏ qua giai đoạn 2
					}
					b.Boss.BarEmpty = true;                                            // cạn thanh máu hiện tại -> BossAI chuyển giai đoạn
					return new Result($"Cạn thanh máu: sang giai đoạn {target}", true);
				}
				case "bossdie":
				{
					var b = w.Arena.Boss;
					if (b == null || b.Dead) return Bad("chưa có boss (gõ: boss)");
					foreach (var e in w.Enemies.Enemies.ToArray())
					{
						if (e.Kind != EnemyTypes.Boss || e.Dead) continue;
						if (e.Boss.State == 0) e.Boss.State = 1;                     // đang xuất hiện thì bất khả xâm phạm
						e.Boss.Stage = 3; e.Boss.ShieldUp = false; e.Boss.MinisSpawned = true; e.Boss.MinisCleared = true; e.Boss.BarEmpty = false;   // bỏ qua mọi giai đoạn / khiên
						w.Enemies.Damage(e, 1e7f, Vector2.Zero);
					}
					return new Result("Boss đã bị hạ", true);
				}
				case "weapon":
					return WeaponCmd(w, arg(1), arg(2));

				case "fuse":
				{
					int n = 0;
					for (var r = Fusions.Available(p); r.Count > 0; r = Fusions.Available(p))
					{
						Fusions.Apply(p, r[0]);
						n++;
					}
					return n > 0 ? new Result($"Đã hợp nhất {n} cặp vũ khí", true) : Bad("không có cặp nào đủ điều kiện (cần cả hai vũ khí cấp >= 5: gõ weapon all 5)");
				}

				case "equip":
					return Equip(w, arg(1), arg(2));

				case "stats":
					if (arg(1) != "max") return Bad("stats max");
					p.MaxHp = Balance.PlayerMaxHp;
					p.Speed = Balance.PlayerSpeed;
					p.MagnetRadius = Balance.PlayerMagnet;
					p.Regen = Balance.PlayerRegen;
					p.MaxMana = Balance.PlayerManaCap;
					p.ManaRegen = Balance.PlayerManaRegenCap;
					p.Mana = p.MaxMana;
					p.DamageMul = Balance.PlayerDamageMul;
					p.FireRate = Balance.FireRateMax;
					foreach (var wp in p.Weapons) wp.RateMul = Balance.WeaponRateMax;
					p.BaseArmor = Balance.PlayerBaseArmor;
					p.DashMaxCharges = Balance.DashMaxCharges;
					p.DashRecharge = Balance.DashRechargeMin;
					p.Hp = p.MaxHp;
					p.RefillDash();
					return new Result("Mọi chỉ số đã tới trần", true);

				case "spawn":
					return Spawn(w, parts);

				case "formation":
					return w.Enemies.SpawnFormationNow(arg(1))
						? new Result($"Đã tạo đội hình {arg(1)}", true)
						: Bad("formation <ring|wall|vflock|pincer|squad|escort>");

				case "killall":
				{
					int n = 0;
					foreach (var e in w.Enemies.Enemies.ToArray())
					{
						if (e.Kind == EnemyTypes.Boss || e.Dead) continue;
						w.Enemies.Damage(e, 1e7f, Vector2.Zero);
						n++;
					}
					return new Result($"Đã giết {n} quái", true);
				}

				case "gems":
				{
					if (!Num(arg(1), out float n) || n <= 0f) return Bad("gems <số>");
					for (int i = 0; i < (int)n; i++)
						w.Pickups.DropGem(p.Pos + V.FromAngle(Rng.Randf() * Mx.Tau) * (30f + Rng.Randf() * 150f), 1f + (i % 3));
					return new Result($"Đã rải {(int)n} viên XP", true);
				}

				case "time":
					if (!Num(arg(1), out float sec) || sec < 0f) return Bad("time <giây>");
					gs.SetTime(sec);
					return new Result($"Thời gian chơi = {sec:0}s ({sec / 60f:0.#} phút)", true);

				case "timescale":
					if (!Num(arg(1), out float ts)) return Bad("timescale <0.1-10>");
					w.TimeScale = Mx.Clamp(ts, 0.1f, 10f);
					return new Result($"Tốc độ game x{w.TimeScale:0.##}", true);

				case "firerate":
					if (!Num(arg(1), out float fr)) return Bad("firerate <1-2>");
					p.FireRate = Mx.Clamp(fr, 0.1f, Balance.FireRateMax);
					return new Result($"Tốc độ bắn chung x{p.FireRate:0.##}", true);

				case "autoaim":
					Settings.AutoAim = arg(1) == "off" ? false : arg(1) == "on" ? true : !Settings.AutoAim;
					return new Result($"Tự động ngắm: {(Settings.AutoAim ? "BẬT" : "TẮT (ngắm bằng chuột)")}", true);

				case "hitbox":
					Settings.ShowHitbox = arg(1) == "off" ? false : arg(1) == "on" ? true : !Settings.ShowHitbox;
					return new Result($"Hiện hitbox: {(Settings.ShowHitbox ? "BẬT" : "TẮT")}", true);

				case "dash":
					p.RefillDash();
					return new Result("Đã hồi đầy dash", true);

				default:
					return Bad($"không có lệnh '{cmd}' (gõ help)");
			}
		}
		catch (Exception ex)
		{
			return new Result("Lỗi: " + ex.Message, false, true);
		}
	}

	private static Result Bad(string msg) => new(msg.StartsWith("cú pháp") ? msg : "Sai cú pháp/điều kiện: " + msg, false, true);

	private static bool Num(string s, out float v) =>
		float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v);

	private static string Info(World w)
	{
		var p = w.Player;
		var gs = w.State;
		var sb = new StringBuilder();
		sb.Append($"Cấp {gs.Level}/{GameState.MaxLevel}  XP {gs.Xp:0.#}/{gs.XpToNext:0.#}  Thời gian {gs.Time:0}s  Kills {gs.Kills}\r\n");
		sb.Append($"Máu {p.Hp:0}/{p.MaxHp:0}  Giáp {p.Armor:0.#}  Tốc độ {p.Speed:0}  Nam châm {p.MagnetRadius:0}  Hồi máu {p.Regen:0.#}/s  Sát thương x{p.DamageMul:0.##}  Tốc độ bắn x{p.FireRate:0.##}\r\n");
		sb.Append($"Dash {p.DashCharges}/{p.DashMaxCharges} (hồi {p.DashRecharge:0.#}s)  God={p.God}  TimeScale x{w.TimeScale:0.##}\r\n");
		sb.Append("Vũ khí: " + string.Join(", ", p.Weapons.Select(x => $"{x.Name} Lv{x.Level}")) + "\r\n");
		sb.Append("Trang bị: " + (p.Equipped.Count == 0 ? "(chưa có)" : string.Join(", ", p.Equipped.Values.Select(e => $"{e.Name} Lv{e.Level}"))) + "\r\n");
		sb.Append($"Quái {w.Enemies.Enemies.Count}  XP tự sinh trên map {w.Pickups.AmbientAlive}  Boss {(w.Arena.Boss == null ? "chưa" : w.Arena.Boss.Dead ? "đã hạ" : $"{w.Arena.Boss.Hp:0}/{w.Arena.Boss.MaxHp:0} pha {w.Arena.Boss.Boss.Phase}")}");
		return sb.ToString();
	}

	/// <summary>Lên thẳng tới cấp n: mỗi cấp tự chọn một nâng cấp ngẫu nhiên (không hiện menu).</summary>
	private static string GoToLevel(World w, int target)
	{
		var gs = w.State;
		target = Mx.Clamp(target, 1, GameState.MaxLevel);
		if (target <= gs.Level) return $"Đang ở cấp {gs.Level} rồi (chỉ lên được, không hạ cấp)";

		int guard = 0;
		while (gs.Level < target && guard++ < 100)
		{
			gs.AddXp(gs.XpToNext + 0.01f);
			while (w.PendingLevelUps > 0)
			{
				var choices = Upgrades.BuildChoices(w.Player);
				if (choices.Count > 0) choices[(int)(Rng.Randi() % (uint)choices.Count)].Apply();
				w.PendingLevelUps--;
			}
		}
		return gs.Level >= GameState.MaxLevel ? "Đã lên cấp 27: trận boss sẽ bắt đầu" : $"Đã lên cấp {gs.Level} (tự chọn nâng cấp ngẫu nhiên)";
	}

	private static Result WeaponCmd(World w, string which, string levelArg)
	{
		var p = w.Player;
		int level = WeaponLevel(levelArg);
		var makers = new (string name, Type type, Func<Player, Weapon> make)[]
		{
			("bow", typeof(MagicBolt), pl => new MagicBolt(pl)),
			("knife", typeof(OrbitBlades), pl => new OrbitBlades(pl)),
			("claw", typeof(ClawSlash), pl => new ClawSlash(pl)),
			("shield", typeof(HolyShield), pl => new HolyShield(pl)),
			("cross", typeof(HolyCross), pl => new HolyCross(pl)),
		};

		var chosen = which == "all" ? makers : makers.Where(m => m.name == which).ToArray();
		if (chosen.Length == 0) return Bad("weapon <bow|knife|claw|shield|cross|all> [cấp]");

		foreach (var m in chosen)
		{
			var wp = p.Weapons.Find(x => x.GetType() == m.type);
			if (wp == null)
			{
				// đã hợp nhất mất vũ khí gốc thì không thêm lại nếu vũ khí hợp nhất chứa nó
				wp = m.make(p);
				p.AddWeapon(wp);
			}
			while (wp.Level < level) wp.LevelUp();
		}
		return new Result("Vũ khí: " + string.Join(", ", p.Weapons.Select(x => $"{x.Name} Lv{x.Level}")), true);
	}

	private static int WeaponLevel(string s) => int.TryParse(s, out int l) ? Mx.Clamp(l, 1, Balance.WeaponMaxLevel) : Balance.WeaponMaxLevel;

	private static Result Equip(World w, string which, string levelArg)
	{
		var p = w.Player;
		int level = int.TryParse(levelArg, out int l) ? Mx.Clamp(l, 1, Equipment.MaxLevel) : Equipment.MaxLevel;
		var all = new[] { ("helm", EquipSlot.Helm), ("body", EquipSlot.Body), ("boots", EquipSlot.Boots), ("shield", EquipSlot.Shield) };
		var chosen = which == "all" ? all : all.Where(x => x.Item1 == which).ToArray();
		if (chosen.Length == 0) return Bad("equip <helm|body|boots|shield|all> [cấp]");

		foreach (var (_, slot) in chosen)
		{
			if (!p.Equipped.ContainsKey(slot)) p.Equip(slot);
			while (p.Equipped[slot].Level < level) p.Equip(slot);
		}
		return new Result($"Trang bị: {string.Join(", ", p.Equipped.Values.Select(e => e.Name))} - giáp {p.Armor:0.#}", true);
	}

	private static Result Spawn(World w, string[] parts)
	{
		string kindName = parts.Length > 1 ? parts[1].ToLowerInvariant() : "";
		int kind = kindName switch { "slime" => EnemyTypes.Slime, "bat" => EnemyTypes.Bat, "bomber" => EnemyTypes.Bomber, "brute" => EnemyTypes.Brute, _ => -1 };
		if (kind < 0) return Bad("spawn <slime|bat|bomber|brute> [số] [special|kích_cỡ]");

		int count = parts.Length > 2 && int.TryParse(parts[2], out int n) ? Mx.Clamp(n, 1, 100) : 1;
		bool special = false;
		float size = 1f;
		for (int i = 3; i < parts.Length; i++)
		{
			if (parts[i].ToLowerInvariant() == "special") special = true;
			else if (Num(parts[i], out float s)) size = Mx.Clamp(s, 0.5f, Balance.EnemySizeMax);
		}

		var p = w.Player;
		for (int i = 0; i < count; i++)
		{
			Vector2 pos = p.Pos + V.FromAngle(Rng.Randf() * Mx.Tau) * (90f + Rng.Randf() * 80f);
			var e = EnemyTypes.Create(kind, w.Enemies.DiffTime, pos, size, 0);
			if (special && kind == EnemyTypes.Slime)
			{
				e.IsSpecial = true;
				e.MaxHp *= 1.5f;
				e.Hp = e.MaxHp;
			}
			w.Enemies.Enemies.Add(e);
		}
		return new Result($"Đã tạo {count} {kindName}{(special ? " đặc biệt" : "")} (cỡ {size:0.##})", true);
	}
}
