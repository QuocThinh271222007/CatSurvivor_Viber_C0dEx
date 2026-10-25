using System.Numerics;

namespace CatSurvivor;

/// <summary>
/// Chú mèo – nhân vật top-down. Di chuyển 8 hướng, vũ khí tự động tấn công,
/// có thời gian bất tử ngắn sau khi trúng đòn (i-frames).
/// </summary>
public sealed class Player
{
	public event Action Died;

	public readonly World W;
	public Vector2 Pos;

	// Các chỉ số dưới đây đều có trần (xem Balance.cs): gán vượt trần sẽ bị cắt về trần.
	private float _speed = 110f, _maxHp = 100f, _magnet = 60f, _regen, _damageMul = 1f;
	public float Speed { get => _speed; set => _speed = MathF.Min(value, Balance.PlayerSpeed); }
	public float MaxHp { get => _maxHp; set { _maxHp = MathF.Min(value, Balance.PlayerMaxHp); Hp = MathF.Min(Hp, _maxHp); } }
	public float Hp;
	public float Radius = 9f;

	/// <summary>Mana cho kĩ năng chủ động (thanh xanh trên HUD): tự hồi theo thời gian; kĩ năng gọi TrySpendMana để trả phí.</summary>
	public float Mana = Balance.PlayerMaxMana;
	private float _maxMana = Balance.PlayerMaxMana, _manaRegen = Balance.PlayerManaRegen;
	public float MaxMana { get => _maxMana; set { _maxMana = MathF.Min(value, Balance.PlayerManaCap); Mana = MathF.Min(Mana, _maxMana); } }
	public float ManaRegen { get => _manaRegen; set => _manaRegen = MathF.Min(value, Balance.PlayerManaRegenCap); }

	/// <summary>Kĩ năng chủ động (Q: sóng xung kích, E: làm chậm thời gian) - xem Skills.cs.</summary>
	public SkillSet Skills { get; }

	/// <summary>Trừ mana nếu đủ (true = đã trừ, kĩ năng được phép dùng); không đủ thì không trừ gì.</summary>
	public bool TrySpendMana(float cost)
	{
		if (Dead || Mana < cost) return false;
		Mana -= cost;
		return true;
	}

	/// <summary>
	/// Trọng lượng của mèo (gốc 1). Mèo nặng hơn thì bị quái đẩy lùi ít hơn VÀ bị giật lùi ít hơn khi vung vũ khí nặng
	/// (xem Weapon.HitEnemy). Quái có Weight riêng nên độ lùi của hai bên phụ thuộc vào tỉ lệ weight giữa mèo và quái.
	/// </summary>
	public float BaseWeight = 1f;

	/// <summary>Trọng lượng thực = trọng lượng gốc + giáp x ArmorWeightPerPoint (mặc giáp nặng thì khó bị đẩy hơn).</summary>
	public float Weight => BaseWeight + Armor * Balance.ArmorWeightPerPoint;

	/// <summary>
	/// Giáp: giảm sát thương phẳng của MỖI đòn nhận vào (nhưng luôn nhận ít nhất ArmorMinFraction sát thương gốc).
	/// Có trần Balance.PlayerArmor. Mỗi điểm giáp còn làm mèo nặng hơn một chút (xem Weight).
	/// </summary>
	public float BaseArmor { get => _armor; set => _armor = Mx.Clamp(value, 0f, Balance.PlayerBaseArmor); }
	private float _armor;

	/// <summary>Tổng giáp = giáp gốc + giáp trang bị, chặn bởi Balance.PlayerArmor.</summary>
	public float Armor => MathF.Min(BaseArmor + EquipSum(e => e.Armor), Balance.PlayerArmor);

	// ---- Trang bị giáp (xem Equipment.cs) ----
	public readonly Dictionary<EquipSlot, Equipment> Equipped = new();

	private float EquipSum(Func<Equipment, float> stat)
	{
		float sum = 0f;
		foreach (var e in Equipped.Values) sum += stat(e);
		return sum;
	}

	/// <summary>Hệ số tốc độ do trang bị (giày +, áo giáp -), không thấp hơn Balance.EquipSpeedMin.</summary>
	public float EquipSpeedMul => MathF.Max(1f + EquipSum(e => e.SpeedBonus), Balance.EquipSpeedMin);
	public float KnockResist => MathF.Min(EquipSum(e => e.KnockResist), Balance.KnockResistMax);
	public float StunResist => MathF.Min(EquipSum(e => e.StunResist), Balance.StunResistMax);
	public float BlockChance => MathF.Min(EquipSum(e => e.BlockChance), Balance.BlockChanceMax);

	/// <summary>Số đòn đã đỡ hoàn toàn nhờ khiên.</summary>
	public int BlockCount { get; private set; }

	/// <summary>Nhận món trang bị ở ô `slot` (chưa có thì tạo cấp 1, có rồi thì nâng một cấp).</summary>
	public void Equip(EquipSlot slot)
	{
		if (Equipped.TryGetValue(slot, out var cur)) cur.LevelUp();
		else Equipped[slot] = new Equipment(slot);

		float hp = Equipment.MaxHpPerLevel(slot); // áo giáp: mỗi cấp +15 máu tối đa (và hồi chừng đó)
		if (hp > 0f) { MaxHp += hp; Heal(hp); }
	}

	/// <summary>Chân mèo thấp hơn tâm bao nhiêu px. Dùng làm mốc chiều sâu (Y) khi sắp xếp thứ tự vẽ với quái.</summary>
	public const float FootOffset = 12f;
	public float MagnetRadius { get => _magnet; set => _magnet = MathF.Min(value, Balance.PlayerMagnet); }   // bán kính hút XP/vật phẩm
	public float Regen { get => _regen; set => _regen = MathF.Min(value, Balance.PlayerRegen); }              // HP hồi mỗi giây
	public float DamageMul { get => _damageMul; set => _damageMul = MathF.Min(value, Balance.PlayerDamageMul); } // nhân vào sát thương mọi vũ khí

	/// <summary>Tốc độ bắn chung (gốc 1): nhân vào tốc độ hồi chiêu/nhịp đánh của MỌI vũ khí (có trần Balance.FireRateMax).</summary>
	private float _fireRate = 1f;
	public float FireRate { get => _fireRate; set => _fireRate = MathF.Min(value, Balance.FireRateMax); }

	public Vector2 Facing { get; private set; } = V.Right;

	/// <summary>Vận tốc hiện tại (px/s) – quái thông minh dùng để đón đầu (vd. Bomber).</summary>
	public Vector2 Velocity { get; private set; }

	/// <summary>Tốc độ chạy hiện tại (px/s): trang bị ảnh hưởng tốc độ, vẫn bị chặn bởi trần. Quái nước rút (Bomber) dùng để biết cần chạy nhanh cỡ nào mới bắt kịp.</summary>
	public float RunSpeed => MathF.Min(Speed * EquipSpeedMul, Balance.PlayerSpeed);

	/// <summary>Vận tốc làm mượt theo thời gian (hằng số 0.5s / 0.1s): quái đọc để dự đoán đường đi (Prediction.cs).</summary>
	public Vector2 VelSlow { get; private set; }
	public Vector2 VelFast { get; private set; }

	/// <summary>Sát thương thực nhận (sau giáp) của đòn gần nhất.</summary>
	public float LastDamageTaken { get; private set; }
	public bool Dead { get; private set; }

	// Chết kiểu "Mario": mèo tưng lên rồi rơi khỏi màn hình
	private const float DeathJump = 330f;     // vận tốc nảy lên lúc chết (px/s)
	private const float DeathGravity = 950f;
	private float _deathVy;

	/// <summary>Thời gian từ lúc chết (giây) - dùng cho hiệu ứng tưng lên rồi rơi.</summary>
	public float DeathTime { get; private set; }
	public readonly List<Weapon> Weapons = new();

	// Hình dáng nhân vật (Mèo hoặc Sans đã mở khoá): xem CharacterSkin - mỗi skin có sprite sheet + các khung đi/đứng yên/trúng đòn/chết riêng
	private readonly CharacterSkin _skin;
	private float _animTime, _idleTime;
	private bool _moving;
	private int _flip = 1; // 1: nhìn phải, -1: nhìn trái
	private float _invuln;

	// --- Knockback khi bị đánh ---
	private const float Gravity = 900f;
	private const float KnockDecay = 5f;        // vận tốc đẩy lùi giảm theo e^(-KnockDecay*t)
	private const float MaxImpact = Balance.PlayerMaxImpact; // trần độ mạnh của một đòn
	private const float HeavyImpact = 250f;    // đòn nặng: choáng ngắn, không điều khiển được
	private const float HeavyStun = 0.25f;
	private Vector2 _knockVel;
	private float _stun;
	private float _blockFlash;

	// ---- Dash ----
	// Bấm Space/Shift: lao nhanh theo hướng đang chạy (đứng yên thì theo hướng đang nhìn). Trong lúc lao (và một chút sau đó)
	// mèo BẤT TỬ, không bị ngắt bởi choáng, và HÚC văng quái chạm phải với lực = Weight x DashForceScale
	// (mặc giáp nặng thì húc mạnh hơn nhưng lao ngắn hơn). Có nhiều lượt, mỗi lượt hồi riêng.
	private int _dashCharges = Balance.DashBaseCharges;
	private int _dashMax = Balance.DashBaseCharges;
	private float _dashRecharge = Balance.DashBaseRecharge;
	private float _dashTimer;           // đếm ngược tới khi hồi xong một lượt
	private float _dashLeft;            // > 0: đang lao
	private float _dashGrace;           // bất tử thêm sau khi lao
	private Vector2 _dashDir = V.Right;
	private readonly HashSet<Enemy> _dashHit = new();
	private readonly List<Enemy> _bashList = new();

	private struct Ghost { public Vector2 World; public int Frame; public int Flip; public float Life; }
	private const float GhostLife = 0.28f;
	private readonly List<Ghost> _ghosts = new();
	private float _ghostTimer;

	/// <summary>
	/// Hướng ngắm chung của vũ khí (radian), cập nhật mỗi khung hình: TỰ ĐỘNG NGẮM bật -> về phía quái gần nhất (không có quái thì theo chuột);
	/// tắt -> luôn theo con trỏ chuột.
	/// </summary>
	public float AimAngle { get; private set; }

	/// <summary>Hướng con trỏ chuột (radian) - luôn theo chuột bất kể tự động ngắm (cho Claw, Wave Slash).</summary>
	public float MouseAngle { get; private set; }
	private readonly List<Enemy> _aimTmp = new();

	/// <summary>Cheat (debug): bất tử.</summary>
	public bool God;

	/// <summary>Cheat (debug): hồi đầy lượt dash.</summary>
	public void RefillDash() { _dashCharges = _dashMax; _dashTimer = 0f; }

	public int DashCharges => _dashCharges;
	public bool IsDashing => _dashLeft > 0f;

	/// <summary>Số lượt dash tối đa (có trần Balance.DashMaxCharges). Tăng thì được cộng luôn một lượt.</summary>
	public int DashMaxCharges
	{
		get => _dashMax;
		set
		{
			int v = Mx.Clamp(value, 1, Balance.DashMaxCharges);
			if (v > _dashMax) _dashCharges = Math.Min(_dashCharges + (v - _dashMax), v);
			_dashMax = v;
			_dashCharges = Math.Min(_dashCharges, _dashMax);
		}
	}

	/// <summary>Thời gian hồi một lượt dash (giây); nhỏ hơn = hồi nhanh hơn, có trần Balance.DashRechargeMin.</summary>
	public float DashRecharge { get => _dashRecharge; set => _dashRecharge = MathF.Max(value, Balance.DashRechargeMin); }

	/// <summary>Thời gian còn lại tới khi hồi xong một lượt (0 nếu đã đầy) – cho HUD.</summary>
	public float DashRechargeLeft => _dashCharges < _dashMax ? MathF.Max(_dashTimer, 0f) : 0f;

	private float _z, _vz;                      // nảy nhẹ khi trúng đòn (chỉ để vẽ)

	public Player(World w)
	{
		W = w;
		Skills = new SkillSet(this);
		_skin = CharacterSkin.Load(CharacterSkin.Selected);
		Hp = MaxHp;
		AddWeapon(new MagicBolt(this));
	}

	private void StartDash(Vector2 direction)
	{
		if (_dashCharges == _dashMax) _dashTimer = _dashRecharge; // bắt đầu hồi lượt đầu tiên bị dùng
		_dashCharges--;
		SoundService.Play("dash", -4f, 0.1f);
		_dashLeft = Balance.DashTime;
		_dashDir = direction;
		_dashHit.Clear();
		Facing = direction;
		_stun = 0f;            // dash thoát được choáng
		_knockVel = Vector2.Zero;
		_ghostTimer = 0f;
	}

	/// <summary>Húc: mỗi quái chạm phải trong lúc lao bị đánh một lần, văng theo hướng lao (trộn hướng từ mèo tới quái).</summary>
	private void DashBash()
	{
		W.Enemies.QueryCircle(Pos, Radius + 4f, _bashList);
		foreach (var e in _bashList)
		{
			if (!_dashHit.Add(e)) continue;
			Vector2 outward = e.Pos - Pos;
			outward = outward.LengthSquared() > 0.01f ? outward.Normalized() : _dashDir;
			Vector2 push = (_dashDir + outward).Normalized();
			W.Enemies.Damage(e, Balance.DashDamage * DamageMul, push * Weight * Balance.DashForceScale);
		}
	}

	public void Update(float dt)
	{
		if (Dead)
		{
			// Đang chết: tưng lên rồi rơi theo trọng lực (không va chạm gì nữa)
			DeathTime += dt;
			_deathVy += DeathGravity * dt;
			Pos.Y += _deathVy * dt;
			return;
		}

		Vector2 raw = W.Input.Move();
		bool dashPressed = W.Input.ConsumeDash();
		Vector2 dir = _stun > 0f ? Vector2.Zero : raw;
		float moveSpeed = RunSpeed;

		// Dash: hướng theo phím đang giữ (kể cả đang choáng), không có thì theo hướng đang nhìn
		if (dashPressed && !IsDashing && _dashCharges > 0)
			StartDash(raw != Vector2.Zero ? raw.Normalized() : Facing);

		if (IsDashing)
		{
			float dashSpeed = MathF.Max(Balance.DashSpeed / MathF.Sqrt(Weight), Balance.DashSpeed * 0.7f);
			Velocity = _dashDir * dashSpeed;
			Pos += Velocity * dt;
			DashBash();
			dir = _dashDir; // để hoạt ảnh/lật hình theo hướng lao

			_ghostTimer -= dt;
			if (_ghostTimer <= 0f)
			{
				_ghostTimer = 0.025f;
				_ghosts.Add(new Ghost { World = Pos, Frame = _skin.WalkFrame(_animTime), Flip = _dashDir.X < 0f ? -1 : 1, Life = GhostLife });
			}
			_dashLeft -= dt;
			if (_dashLeft <= 0f) { _dashLeft = 0f; _dashGrace = Balance.DashGrace; }
		}
		else
		{
			if (dir != Vector2.Zero) Facing = dir.Normalized();
			Velocity = dir * moveSpeed + _knockVel;
			Pos += Velocity * dt;
		}

		// Vận tốc làm mượt (chậm / nhanh): quái dùng để dự đoán đường đi của mèo (xem Prediction.cs)
		VelSlow += (Velocity - VelSlow) * (1f - MathF.Exp(-dt / 0.5f));
		VelFast += (Velocity - VelFast) * (1f - MathF.Exp(-dt / 0.1f));

		if (W.Arena.Active) Pos = W.Arena.Clamp(Pos, Radius); // đang đánh boss: không ra khỏi vùng khoá

		// Hồi lượt dash
		_dashGrace = MathF.Max(_dashGrace - dt, 0f);
		if (_dashCharges < _dashMax)
		{
			_dashTimer -= dt;
			if (_dashTimer <= 0f)
			{
				_dashCharges++;
				_dashTimer = _dashCharges < _dashMax ? _dashRecharge : 0f;
			}
		}
		for (int i = _ghosts.Count - 1; i >= 0; i--)
		{
			var g = _ghosts[i];
			g.Life -= dt;
			if (g.Life <= 0f) _ghosts.RemoveAt(i);
			else _ghosts[i] = g;
		}

		_moving = dir != Vector2.Zero;
		if (_moving) _animTime += dt;
		_idleTime += dt;
		if (dir.X != 0f) _flip = dir.X > 0f ? 1 : -1;

		_knockVel *= MathF.Exp(-KnockDecay * dt);
		_stun = MathF.Max(_stun - dt, 0f);
		_blockFlash = MathF.Max(_blockFlash - dt, 0f);
		if (_z > 0f || _vz > 0f)
		{
			_vz -= Gravity * dt;
			_z += _vz * dt;
			if (_z <= 0f)
			{
				_z = 0f;
				_vz = _vz < -50f ? -_vz * 0.4f : 0f; // nảy lại 1 nhịp nhỏ
			}
		}

		_invuln = MathF.Max(_invuln - dt, 0f);
		Heal(Regen * dt);
		Mana = MathF.Min(MaxMana, Mana + ManaRegen * dt);

		Vector2 toMouse = W.MouseWorld - Pos;
		MouseAngle = toMouse.LengthSquared() > 1f ? toMouse.Angle() : Facing.Angle();
		AimAngle = MouseAngle;
		if (Settings.AutoAim)
		{
			W.Enemies.FindNearest(Pos, 1, 320f, _aimTmp);
			if (_aimTmp.Count > 0) AimAngle = (_aimTmp[0].Pos - Pos).Angle();
		}

		foreach (var w in Weapons) w.Update(dt);
		Skills.Update(dt);
	}

	public void AddWeapon(Weapon w) => Weapons.Add(w);

	/// <summary>Giật lùi do vung vũ khí nặng trúng quái (cộng dồn khi trúng nhiều quái, có trần).</summary>
	public void Recoil(Vector2 velocity)
	{
		if (Dead) return;
		_knockVel = (_knockVel + velocity).LimitLength(Balance.PlayerMaxRecoil);
	}

	/// <summary>Hồi máu (bình máu, thẻ Gà quay, áo giáp, hút máu...). Độ khó Siêu khó: KHÔNG hồi được.</summary>
	public void Heal(float amount)
	{
		if (!Difficulty.AllowHealing) return;
		Hp = MathF.Min(MaxHp, Hp + amount);
	}

	/// <summary>
	/// fromPos: vị trí nguồn đòn (để đẩy lùi ra xa nguồn). impact: độ nặng của đòn —
	/// càng nặng càng bị đẩy xa và nảy cao; từ HeavyImpact trở lên còn bị choáng.
	/// </summary>
	public void TakeDamage(float amount, Vector2 fromPos, float impact)
	{
		if (God || Dead || _invuln > 0f || IsDashing || _dashGrace > 0f || Skills.GuardActive || Skills.StopActive) return; // kĩ năng Bất tử / Ngừng thời gian (quái đứng yên, không gây sát thương) // dash: bất tử (God: cheat bất tử)

		// Holy Shield: còn lớp khiên thì đỡ trọn đòn này
		bool shielded = false;
		foreach (var w in Weapons) if (w.AbsorbHit(fromPos)) { shielded = true; break; }

		// Khiên trang bị: có cơ hội đỡ hoàn toàn đòn này
		if (shielded || (BlockChance > 0f && Rng.Randf() < BlockChance))
		{
			BlockCount++;
			SoundService.Play("block", -2f, 0.1f);
			_blockFlash = 0.35f;
			_invuln = 0.25f;
			LastDamageTaken = 0f;
			return;
		}

		impact = MathF.Min(impact, MaxImpact) * (1f - KnockResist) / Weight; // giày giảm bị đẩy; mèo nặng thì lùi ít hơn
		float taken = MathF.Max(amount - Armor, amount * Balance.ArmorMinFraction); // áp dụng giáp
		LastDamageTaken = taken;
		Hp -= taken;
		SoundService.Play("hurt", -2f, 0.12f);
		_invuln = 0.5f;

		Vector2 away = Pos - fromPos;
		away = away.LengthSquared() > 0.01f ? away.Normalized() : -Facing;
		_knockVel = away * impact * 1.5f;
		_vz = impact * 0.6f;
		_z = MathF.Max(_z, 0.5f);
		if (impact >= HeavyImpact) _stun = HeavyStun * (1f - StunResist); // mũ giảm thời gian choáng

		if (Hp <= 0f)
		{
			Hp = 0f;
			Dead = true;
			_deathVy = -DeathJump;
			_dashLeft = 0f; _knockVel = Vector2.Zero; _stun = 0f; _z = 0f; _vz = 0f;
			Died?.Invoke();
		}
	}

	/// <summary>Hitbox của mọi vũ khí (xem Settings.ShowHitbox).</summary>
	public void DrawWeaponHitboxes(Canvas c)
	{
		c.SetTransform(Pos, 0f, Vector2.One);
		foreach (var w in Weapons) w.DrawHitbox(c);
		c.ResetTransform();
	}

	/// <summary>Vẽ hiệu ứng vũ khí (lưỡi dao, vuốt, aura, mũi tên chỉ hướng chuột...) ở lớp cao hơn mọi quái; toạ độ cục bộ của mèo.</summary>
	public void DrawWeapons(Canvas c)
	{
		if (Dead) return; // chết rồi thì vũ khí biến mất
		c.SetTransform(Pos, 0f, Vector2.One);
		foreach (var w in Weapons) w.Draw(c);
		Skills.Draw(c);
		c.ResetTransform();
	}

	/// <summary>Vẽ thân mèo + bóng + ảnh tàn dash + trang bị (toạ độ thế giới).</summary>
	public void Draw(Canvas c)
	{
		bool flash = _invuln > 0f && ((int)(_invuln * 20f)) % 2 == 0;
		int frame = Dead ? _skin.DeadFrame
			: flash && _skin.HitFrame >= 0 ? _skin.HitFrame
			: _moving ? _skin.WalkFrame(_animTime)
			: _skin.IdleFrame(_idleTime);

		// Bóng dưới chân (ở lại mặt đất khi nhân vật nảy lên, nhỏ và mờ dần theo độ cao)
		if (!Dead) // đang rơi khỏi màn hình thì không còn bóng
		{
			float shadowScale = 1f / (1f + _z / 40f);
			c.SetTransform(Pos + new Vector2(0f, FootOffset), 0f, new Vector2(shadowScale, 0.4f * shadowScale));
			c.DrawCircle(Vector2.Zero, 8f, new Rgba(0f, 0f, 0f, 0.25f));
		}

		var dest = _skin.Dest;

		// Vệt mờ của dash (các "ảnh tàn" nhạt dần phía sau)
		foreach (var g in _ghosts)
		{
			float a = g.Life / GhostLife * 0.5f;
			c.SetTransform(g.World, 0f, new Vector2(g.Flip, 1f));
			c.DrawTexture(_skin.Sheet, dest, _skin.Src(g.Frame), new Rgba(0.6f, 0.9f, 1f, a));
		}

		// Lật ngang theo hướng chạy; nhấc lên theo độ cao _z
		float tumble = Dead ? MathF.Sin(DeathTime * 9f) * 0.35f : 0f; // chết: lắc lư khi tưng và rơi
		c.SetTransform(Pos + new Vector2(0f, -_z), tumble, new Vector2(_flip, 1f));
		// Skin không có khung "trúng đòn" riêng (Sans): nhấp nháy mờ thay cho khung trắng của mèo
		bool blink = flash && _skin.HitFrame < 0;
		c.DrawTexture(_skin.Sheet, dest, _skin.Src(frame), blink ? new Rgba(1f, 1f, 1f, 0.35f) : null);
		c.ResetTransform();

		// Khiên đỡ được đòn: vòng sáng + chữ BLOCK
		if (_blockFlash > 0f)
		{
			float a = Mx.Clamp(_blockFlash / 0.35f, 0f, 1f);
			c.DrawArc(Pos + new Vector2(0f, -_z), 17f, 0f, Mx.Tau, new Rgba(0.5f, 0.9f, 1f, a), 2.5f);
			PixelFont.Draw(c, "BLOCK", Pos + new Vector2(-12f, -30f - (1f - a) * 8f), 1, new Rgba(0.6f, 0.95f, 1f, a));
		}
	}
}
