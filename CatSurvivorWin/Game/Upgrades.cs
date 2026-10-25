namespace CatSurvivor;

/// <summary>Một lựa chọn phần thưởng khi lên cấp.</summary>
public class Upgrade
{
	public string Title, Desc;
	public Action Apply;
	public bool IsFusion;

	/// <summary>Hình minh hoạ cạnh thẻ (hiện không thẻ nào dùng: thẻ giáp không còn icon; null = không có icon).</summary>
	public Bitmap Icon;

	/// <summary>Là phần thưởng liên quan vũ khí (nâng cấp / vũ khí mới / hợp nhất).</summary>
	public bool IsWeapon;

	/// <summary>Trọng số khi rút ngẫu nhiên: càng lớn càng dễ xuất hiện trong 3 lựa chọn.</summary>
	public float Weight = 1f;
}

/// <summary>
/// Tạo danh sách lựa chọn nâng cấp khi lên level: rút ngẫu nhiên 3 lựa chọn
/// từ "pool" (vũ khí mới / nâng cấp vũ khí đang có / chỉ số bị động / trang bị).
/// Chỉ chứa logic - hiển thị là việc của UpgradeForm.
/// </summary>
public static class Upgrades
{
	public const int ChoiceCount = 3;

	// Trọng số rút ngẫu nhiên (các phần thưởng khác = 1): vũ khí được ưu tiên để xuất hiện nhiều hơn hẳn
	private const float WeaponUpgradeWeight = 5f;
	private const float NewWeaponWeight = 3f;
	private const float FireRateWeight = 3f;       // thẻ tốc độ bắn chung
	private const float WeaponRateWeight = 2f;     // thẻ tốc độ bắn riêng từng vũ khí

	public static List<Upgrade> BuildChoices(Player p)
	{
		var pool = new List<Upgrade>();

		// Vũ khí đang có -> nâng cấp
		foreach (var w in p.Weapons)
		{
			if (w.Level >= Weapon.MaxLevel) continue;
			var weapon = w;
			pool.Add(new Upgrade
			{
				Title = $"{w.Name}  Lv {w.Level} -> {w.Level + 1}",
				Desc = w.NextLevelText,
				Apply = () => weapon.LevelUp(),
				IsWeapon = true,
				Weight = WeaponUpgradeWeight,
			});
		}

		// Vũ khí chưa có -> mở khoá
		if (!p.Weapons.Exists(w => w is OrbitBlades))
			pool.Add(new Upgrade { Title = "Knife (mới)", Desc = "Những con dao xoay quanh bạn", Apply = () => p.AddWeapon(new OrbitBlades(p)), IsWeapon = true, Weight = NewWeaponWeight });
		if (!p.Weapons.Exists(w => w is ClawSlash))
			pool.Add(new Upgrade { Title = "Claw (mới)", Desc = "Vuốt hình quạt theo hướng chuột", Apply = () => p.AddWeapon(new ClawSlash(p)), IsWeapon = true, Weight = NewWeaponWeight });
		if (!p.Weapons.Exists(w => w is HolyShield))
			pool.Add(new Upgrade { Title = "Holy Shield (mới)", Desc = "Khiên thánh đỡ trọn đòn đánh vào bạn, vỡ rồi tự hồi", Apply = () => p.AddWeapon(new HolyShield(p)), IsWeapon = true, Weight = NewWeaponWeight });
		if (!p.Weapons.Exists(w => w is HolyCross))
			pool.Add(new Upgrade { Title = "Holy Cross (mới)", Desc = "Thánh giá bay quanh bạn, tự lao vào quái gần nhất", Apply = () => p.AddWeapon(new HolyCross(p)), IsWeapon = true, Weight = NewWeaponWeight });

		// Tốc độ bắn: một thẻ CHUNG cho mọi vũ khí + một thẻ RIÊNG cho từng vũ khí đang có (không ra khi đã chạm trần)
		if (p.FireRate < Balance.FireRateMax - 0.001f)
			pool.Add(new Upgrade
			{
				Title = "Tốc độ bắn +15%",
				Desc = $"Mọi vũ khí tấn công đánh nhanh hơn (không tính Holy Shield): x{p.FireRate:0.00} -> x{Math.Min(p.FireRate + 0.15f, Balance.FireRateMax):0.00} (trần x{Balance.FireRateMax:0.#})",
				Apply = () => p.FireRate += 0.15f,
				IsWeapon = true,
				Weight = FireRateWeight,
			});
		foreach (var w in p.Weapons)
		{
			if (w.RateMul >= Balance.WeaponRateMax - 0.001f) continue;
			var weapon = w;
			pool.Add(new Upgrade
			{
				Title = $"{w.Name}: {w.RateCardName} +25%",
				Desc = $"{w.RateCardDesc}: x{w.RateMul:0.00} -> x{Math.Min(w.RateMul + 0.25f, Balance.WeaponRateMax):0.00} (trần x{Balance.WeaponRateMax:0.#})",
				Apply = () => weapon.RateMul = MathF.Min(weapon.RateMul + 0.25f, Balance.WeaponRateMax),
				IsWeapon = true,
				Weight = WeaponRateWeight,
			});
		}

		// Chỉ số bị động: không đưa ra lựa chọn khi đã chạm trần (xem Balance.cs)
		// Siêu khó: bỏ mọi thẻ liên quan máu (Máu tối đa, Hồi máu, Gà quay); giáp thì vẫn có
		bool healthCards = Difficulty.AllowHealing;
		if (healthCards && p.MaxHp < Balance.PlayerMaxHp - 0.01f)
			pool.Add(new Upgrade { Title = "Máu tối đa +20", Desc = $"{p.MaxHp:F0} -> {Math.Min(p.MaxHp + 20f, Balance.PlayerMaxHp):F0} (trần {Balance.PlayerMaxHp:F0}), hồi 20 máu", Apply = () => { p.MaxHp += 20f; p.Heal(20f); } });
		if (p.Speed < Balance.PlayerSpeed - 0.01f)
			pool.Add(new Upgrade { Title = "Tốc độ chạy +10%", Desc = $"{p.Speed:F0} -> {Math.Min(p.Speed * 1.1f, Balance.PlayerSpeed):F0} (trần {Balance.PlayerSpeed:F0})", Apply = () => p.Speed *= 1.1f });
		if (p.MagnetRadius < Balance.PlayerMagnet - 0.01f)
			pool.Add(new Upgrade { Title = "Nam châm +40%", Desc = $"Hút XP từ xa hơn: {p.MagnetRadius:F0} -> {Math.Min(p.MagnetRadius * 1.4f, Balance.PlayerMagnet):F0} (trần {Balance.PlayerMagnet:F0})", Apply = () => p.MagnetRadius *= 1.4f });
		if (healthCards && p.Regen < Balance.PlayerRegen - 0.01f)
			pool.Add(new Upgrade { Title = "Hồi máu +0.5/s", Desc = $"{p.Regen:F1} -> {Math.Min(p.Regen + 0.5f, Balance.PlayerRegen):F1} (trần {Balance.PlayerRegen:F0})", Apply = () => p.Regen += 0.5f });
		if (p.DamageMul < Balance.PlayerDamageMul - 0.001f)
			pool.Add(new Upgrade { Title = "Sát thương +15%", Desc = $"Mọi vũ khí: x{p.DamageMul:F2} -> x{Math.Min(p.DamageMul + 0.15f, Balance.PlayerDamageMul):F2} (trần x{Balance.PlayerDamageMul:F0})", Apply = () => p.DamageMul += 0.15f });
		if (p.BaseArmor < Balance.PlayerBaseArmor - 0.01f)
			pool.Add(new Upgrade { Title = "Giáp +2", Desc = $"Giảm mỗi đòn nhận vào: {p.BaseArmor:F0} -> {Math.Min(p.BaseArmor + 2f, Balance.PlayerBaseArmor):F0} (trần giáp gốc {Balance.PlayerBaseArmor:F0}), mèo nặng hơn", Apply = () => p.BaseArmor += 2f });

		// Mana cho kĩ năng chủ động Q / E (xem Skills.cs)
		if (p.MaxMana < Balance.PlayerManaCap - 0.01f)
			pool.Add(new Upgrade { Title = "Mana tối đa +20", Desc = $"{p.MaxMana:F0} -> {Math.Min(p.MaxMana + 20f, Balance.PlayerManaCap):F0} (trần {Balance.PlayerManaCap:F0}), hồi 20 mana", Apply = () => { p.MaxMana += 20f; p.Mana = MathF.Min(p.MaxMana, p.Mana + 20f); } });
		if (p.ManaRegen < Balance.PlayerManaRegenCap - 0.01f)
			pool.Add(new Upgrade { Title = "Hồi mana +1.5/s", Desc = $"{p.ManaRegen:F1} -> {Math.Min(p.ManaRegen + 1.5f, Balance.PlayerManaRegenCap):F1} (trần {Balance.PlayerManaRegenCap:F0})", Apply = () => p.ManaRegen += 1.5f });

		// Trang bị giáp: món chưa có -> nhận cấp 1; món đã có -> nâng một cấp (xem Equipment.cs)
		foreach (EquipSlot slot in Enum.GetValues(typeof(EquipSlot)))
		{
			var s = slot;
			if (!p.Equipped.TryGetValue(s, out var cur))
				pool.Add(new Upgrade { Title = $"Trang bị: {new Equipment(s).Name} (mới)", Desc = Equipment.Describe(s, 1), Apply = () => p.Equip(s) });
			else if (cur.Level < Equipment.MaxLevel)
				pool.Add(new Upgrade { Title = $"{cur.Name} Lv {cur.Level} -> {new Equipment(s, cur.Level + 1).Name} Lv {cur.Level + 1}", Desc = Equipment.Describe(s, cur.Level + 1), Apply = () => p.Equip(s) });
		}
		if (p.DashMaxCharges < Balance.DashMaxCharges)
			pool.Add(new Upgrade { Title = "Dash +1 lượt", Desc = $"Số lượt dash: {p.DashMaxCharges} -> {p.DashMaxCharges + 1} (trần {Balance.DashMaxCharges})", Apply = () => p.DashMaxCharges += 1 });
		if (p.DashRecharge > Balance.DashRechargeMin + 0.01f)
			pool.Add(new Upgrade { Title = "Dash hồi nhanh +15%", Desc = $"Hồi một lượt: {p.DashRecharge:0.00}s -> {Math.Max(p.DashRecharge * 0.85f, Balance.DashRechargeMin):0.00}s (tối thiểu {Balance.DashRechargeMin:0.0}s)", Apply = () => p.DashRecharge *= 0.85f });
		if (healthCards)
			pool.Add(new Upgrade { Title = "Gà quay", Desc = "Hồi 40 máu", Apply = () => p.Heal(40f) });

		// Rút ChoiceCount phần tử KHÔNG lặp, theo trọng số (vũ khí có trọng số cao nên ra nhiều hơn)
		var choices = new List<Upgrade>();
		var bag = new List<Upgrade>(pool);
		while (choices.Count < ChoiceCount && bag.Count > 0)
		{
			float total = 0f;
			foreach (var u in bag) total += u.Weight;
			float roll = Rng.Randf() * total;
			int idx = 0;
			for (; idx < bag.Count - 1; idx++)
			{
				roll -= bag[idx].Weight;
				if (roll < 0f) break;
			}
			choices.Add(bag[idx]);
			bag.RemoveAt(idx);
		}

		// Đảm bảo luôn có ít nhất một phần thưởng vũ khí (nếu còn) trong 3 lựa chọn
		if (choices.Count > 0 && !choices.Exists(c => c.IsWeapon))
		{
			var weaponLeft = bag.FindAll(u => u.IsWeapon);
			if (weaponLeft.Count > 0)
				choices[choices.Count - 1] = weaponLeft[(int)(Rng.Randi() % (uint)weaponLeft.Count)];
		}

		// Hợp nhất vũ khí: nếu có công thức đủ điều kiện thì LUÔN đưa ra một lựa chọn hợp nhất trong số các lựa chọn
		var recipes = Fusions.Available(p);
		if (recipes.Count > 0 && !choices.Exists(c => c.IsFusion))
		{
			var r = recipes[(int)(Rng.Randi() % (uint)recipes.Count)];
			var fusion = new Upgrade
			{
				Title = $"★ HỢP NHẤT: {Fusions.Find(p, r.A).Name} + {Fusions.Find(p, r.B).Name}",
				Desc = $"-> {r.Name}: {r.Desc}",
				IsFusion = true,
				IsWeapon = true,
				Apply = () => Fusions.Apply(p, r),
			};
			if (choices.Count >= ChoiceCount) choices[choices.Count - 1] = fusion;
			else choices.Add(fusion);
		}
		return choices;
	}
}
