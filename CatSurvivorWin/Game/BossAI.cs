using System.Numerics;

namespace CatSurvivor;

/// <summary>Trạng thái riêng của boss (gắn vào Enemy.Boss).</summary>
public class BossData
{
	public const float IntroTime = 2.2f;

	/// <summary>
	/// 0 xuất hiện (bất khả xâm phạm) · 1 đi đuổi · 2 gồng lao · 3 đang lao · 4 kiệt sức (chịu thêm sát thương)
	/// 5 gồng giậm · 6 khựng sau giậm (phát sóng xung kích) · 7 gầm triệu hồi quái
	/// 8 gầm gọi 4 mắt con (vào giai đoạn 2) · 9 chưởng chữ thập (đứng yên, 4 mắt con xếp quanh)
	/// </summary>
	public int State;
	public float Timer;
	public float AttackCd = 1.5f;
	public int Phase = 1;              // 1: > 60% máu · 2: > 30% · 3: còn lại (nổi điên)  (mức "hung hăng" của các đòn; khác với Stage bên dưới)

	// --- GIAI ĐOẠN 2 (còn <= 50% máu): 4 mắt con hỗ trợ, xem MiniEyes.cs ---
	public int Stage = 1;              // GIAI ĐOẠN = THANH MÁU: 1 mắt chính một mình · 2 có 4 mắt con · 3 nổi điên: thêm mắt chính thứ hai (mỗi mắt 4 mắt con)
	public bool ShieldUp;              // true: mắt chính bất khả xâm phạm (còn mắt con sống)
	public float ShieldFlash;          // > 0: vừa bị đánh vào lúc đang bất khả xâm phạm (nháy khiên)
	public bool MinisSpawned, MinisCleared;
	public int Index;                  // 0 = mắt chính, 1 = mắt chính thứ hai (xuất hiện ở giai đoạn 3)
	public bool BarEmpty;              // thanh máu của giai đoạn hiện tại vừa cạn: BossAI.Step đổi sang giai đoạn kế (chưa chuyển xong thì bất khả xâm phạm)
	public float ResummonCd = 40f;     // Khó / Siêu khó: đếm ngược tới lần gọi bù mắt con kế tiếp
	public int CrossState;             // chưởng chữ thập: 0 không · 1 xếp hình + báo hiệu · 2 đang quét
	public float CrossTimer, CrossAngle;
	public float CrossCd = 4f;         // hồi chiêu chữ thập
	public int LastAttack;             // đòn vừa tung: 1 lao, 2 giậm, 3 gầm (boss thông minh tránh lặp lại đòn)
	public int Look = -1;              // hướng nhìn của con mắt (0..7, xem EnemyManager.EyeLook), -1 = chưa chọn
	public Vector2 ChargeDir = V.Left;
	public int PendingRings;
	public float RingDelay;
	public bool Summoned;

	/// <summary>Sóng xung kích lan tròn từ chỗ boss giậm xuống. Né bằng cách DASH xuyên qua vòng (dash bất tử).</summary>
	public class Ring
	{
		public Vector2 Center;
		public float R = 10f;
		public bool Hit;
	}
	public readonly List<Ring> Rings = new();
}

/// <summary>
/// AI của boss. Chu kỳ: đi đuổi mèo -> chọn một đòn: LAO (gồng báo hiệu, lao thẳng, đâm vào tường thì tự choáng và chịu thêm
/// sát thương) · GIẬM (nhảy lên giậm xuống, phát 1-3 vòng sóng xung kích lan ra) · GẦM (triệu hồi quái nhỏ).
/// Mất tới 60% / 30% máu thì đổi pha: gầm triệu hồi miễn phí, nhanh hơn, hồi chiêu ngắn hơn, nhiều vòng sóng hơn.
/// </summary>
public static class BossAI
{
	private const float ChargeWindup = 0.85f;
	private const float ChargeTime = 0.95f;
	private const float ChargeSpeed = 360f;
	private const float StunTime = 2.0f;
	private const float SlamWindup = 0.9f;
	private const float SlamRecover = 0.8f;
	private const float SlamRadius = 62f;      // sát thương trực tiếp quanh chân boss khi giậm
	private const float RingSpeed = 230f;
	private const float RingWidth = 10f;
	private const float RingMax = 330f;
	private const float RoarTime = 1.1f;
	private const int MaxMinions = 12;

	public static float RingMaxValue => RingMax;
	public static float SlamRadiusValue => SlamRadius;
	public static float ChargeWindupValue => ChargeWindup;
	public static float SlamWindupValue => SlamWindup;

	private static float AttackCooldown(BossData b)
	{
		// Đòn liên hoàn (từ Trung bình): đôi khi tung ngay đòn kế tiếp, không nghỉ
		if (Rng.Randf() < Difficulty.BossComboChance) return 0.25f;
		return (b.Phase switch { 1 => 2.4f, 2 => 1.8f, _ => 1.2f }) * Difficulty.BossCooldownMul * (b.ShieldUp ? 1.5f : 1f); // còn mắt con: mắt chính ra đòn thưa hơn để mèo còn rảnh đuổi mắt con
	}

	public static void Step(Enemy e, float dt, Player player, EnemyManager em, BossArena arena)
	{
		var b = e.Boss;
		e.AnimTime += dt;
		Vector2 pp = player.Pos;
		Vector2 to = pp - e.Pos;
		float dist = to.Length();
		Vector2 dir = dist > 0.01f ? to / dist : Vector2.Zero;
		if (b.State != 2 && b.State != 3 && MathF.Abs(to.X) > 4f) e.FaceX = to.X; // nhìn về phía mèo (lao thì nhìn hướng lao)

		UpdateRings(b, dt, player);

		// Mỗi giai đoạn có một THANH MÁU riêng: cạn thanh thì sang giai đoạn kế (thanh đầy lại), cạn thanh cuối thì chết (xem EnemyManager.Damage)
		if (b.BarEmpty && b.State != 0) AdvanceStage(e, b, em, arena);
		b.Phase = b.Stage;                                // mức hung hăng của các đòn theo giai đoạn
		int minisAlive = em.MiniCount(e);                 // chỉ tính mắt con của MẮT CHÍNH NÀY
		if (b.Stage >= 2)
		{
			// Giai đoạn 2-3: mắt chính bất khả xâm phạm cho tới khi diệt hết 4 mắt con của nó
			bool shield = !b.MinisSpawned || minisAlive > 0;
			if (b.ShieldUp && !shield && !b.MinisCleared)
			{
				b.MinisCleared = true;   // diệt đủ 4 mắt con: mắt chính lộ sơ hở (choáng lâu hơn thường)
				b.CrossState = 0;
				b.State = 4;
				b.Timer = -1.5f;
				em.Toast("Mắt chính lộ sơ hở - đánh ngay!", 3f);
			}
			b.ShieldUp = shield;
		}
		b.ShieldFlash = MathF.Max(b.ShieldFlash - dt, 0f);
		if (b.Stage >= 2 && b.MinisSpawned && Difficulty.BossResummonInterval > 0f) b.ResummonCd -= dt;   // đồng hồ gọi bù chạy suốt giai đoạn 2-3 (đòn gọi chỉ được tung lúc mắt chính đang đi đuổi)

		float speedMul = (1f + (b.Phase - 1) * 0.25f) * Difficulty.BossSpeedMul * (b.ShieldUp ? 0.75f : 1f); // pha càng cao và độ khó càng cao boss càng nhanh

		switch (b.State)
		{
			case 0: // xuất hiện
				b.Timer += dt;
				if (b.Timer >= BossData.IntroTime) { b.State = b.Stage >= 2 && !b.MinisSpawned ? 8 : 1; b.Timer = 0f; b.AttackCd = 1.2f; }   // mắt chính thứ hai vào thẳng bước gọi mắt con
				break;

			case 1: // đi đuổi
				e.Pos += Prediction.Dir(e.Pos, player, e.Speed * speedMul, dir, 0f, arena) * e.Speed * speedMul * dt; // đuổi theo chỗ mèo sẽ tới
				b.AttackCd -= dt;
				b.CrossCd -= dt;
				if (Resummon(e, b, em, minisAlive)) break;
				if (b.AttackCd <= 0f && b.ShieldUp && b.CrossCd <= 0f && minisAlive >= MiniEyes.Count) StartCross(b);
				else if (b.AttackCd <= 0f) ChooseAttack(e, b, em, player, arena, speedMul, Prediction.Dir(e.Pos, player, ChargeSpeed * speedMul, dir, ChargeWindup / speedMul, arena));
				break;

			case 2: // gồng lao: nửa đầu còn ngắm theo mèo, nửa sau khoá hướng
			{
				b.Timer += dt;
				float windup = ChargeWindup / speedMul;
				if (b.Timer < windup * 0.5f) b.ChargeDir = Prediction.Dir(e.Pos, player, ChargeSpeed * speedMul, dir, windup - b.Timer, arena);
				if (MathF.Abs(b.ChargeDir.X) > 0.05f) e.FaceX = b.ChargeDir.X;
				if (b.Timer >= windup) { b.State = 3; b.Timer = 0f; }
				break;
			}

			case 3: // đang lao
			{
				b.Timer += dt;
				// Khó trở lên: vừa lao vừa bẻ lái bám theo chỗ mèo SẼ tới (độ gắt tuỳ độ khó)
				float turn = Difficulty.BossHoming;
				if (turn > 0f)
					b.ChargeDir = TurnToward(b.ChargeDir, Prediction.Dir(e.Pos, player, ChargeSpeed * speedMul, b.ChargeDir, 0f, arena), turn * dt);
				e.Pos += b.ChargeDir * ChargeSpeed * speedMul * dt;
				Vector2 clamped = arena.Clamp(e.Pos, e.Radius);
				if ((clamped - e.Pos).LengthSquared() > 0.01f)
				{
					e.Pos = clamped;
					if (Difficulty.BossAvoidsWalls)
					{
						// Siêu khó: biết dừng trước tường, KHÔNG tự choáng - tung đòn kế tiếp rất nhanh
						b.State = 1;
						b.Timer = 0f;
						b.AttackCd = 0.35f;
					}
					else
					{
						b.State = 4; // đâm vào tường: tự choáng
						b.Timer = 0f;
					}
				}
				else if (b.Timer >= ChargeTime)
				{
					b.State = 1;
					b.Timer = 0f;
					b.AttackCd = AttackCooldown(b);
				}
				break;
			}

			case 4: // kiệt sức
				b.Timer += dt;
				if (b.Timer >= StunTime * Difficulty.BossStunMul) { b.State = 1; b.Timer = 0f; b.AttackCd = AttackCooldown(b) * 0.7f; }
				break;

			case 5: // gồng giậm
				b.Timer += dt;
				if (b.Timer >= SlamWindup / speedMul)
				{
					if ((pp - e.Pos).Length() < SlamRadius + player.Radius)
						player.TakeDamage(Balance.BossRingDamage * 1.4f * Difficulty.EnemyDamageMul, e.Pos, 380f);
					SoundService.Play("slam", 0f, 0.3f);
					b.PendingRings = b.Phase; // pha càng cao càng nhiều vòng liên tiếp
					b.RingDelay = 0f;
					b.State = 6;
					b.Timer = 0f;
				}
				break;

			case 6: // khựng sau giậm, phát sóng xung kích
				b.Timer += dt;
				if (b.PendingRings > 0)
				{
					b.RingDelay -= dt;
					if (b.RingDelay <= 0f)
					{
						b.Rings.Add(new BossData.Ring { Center = e.Pos });
						b.PendingRings--;
						b.RingDelay = 0.5f;
					}
				}
				if (b.Timer >= SlamRecover && b.PendingRings == 0) { b.State = 1; b.Timer = 0f; b.AttackCd = AttackCooldown(b); }
				break;

			case 8: // gầm gọi 4 mắt con
				b.Timer += dt;
				if (!b.MinisSpawned && b.Timer >= 0.7f) { b.MinisSpawned = true; SpawnMiniEyes(e, em, arena); }
				if (b.Timer >= 1.8f) { b.State = 1; b.Timer = 0f; b.AttackCd = 2.5f; b.CrossCd = 4f; b.ResummonCd = Difficulty.BossResummonInterval; }
				break;

			case 9: // chưởng chữ thập: mắt chính đứng yên, 4 mắt con xếp quanh rồi quét 4 chùm tia; thiếu một con là tan
				if (minisAlive < MiniEyes.Count)
				{
					b.CrossState = 0; b.State = 1; b.Timer = 0f; b.AttackCd = 1.2f; b.CrossCd = MiniEyes.CrossCooldown * 0.6f;
					break;
				}
				MiniEyes.StepCross(e, dt, player);
				if (b.CrossState == 2 && b.CrossTimer >= MiniEyes.CrossDuration)
				{
					b.CrossState = 0; b.State = 1; b.Timer = 0f; b.AttackCd = 1.5f; b.CrossCd = MiniEyes.CrossCooldown;
				}
				break;

			case 7: // gầm triệu hồi
				b.Timer += dt;
				if (!b.Summoned && b.Timer >= 0.6f) { b.Summoned = true; SummonMinions(e, b, em, player, arena); }
				if (b.Timer >= RoarTime) { b.State = 1; b.Timer = 0f; b.AttackCd = AttackCooldown(b); }
				break;
		}

		if (b.State != 3) e.Pos = arena.Clamp(e.Pos, e.Radius);

		// Chạm mèo gây sát thương (lúc đang lao đau hơn); không chạm được khi vừa xuất hiện
		// (lúc kiệt sức boss không gây sát thương: đó là khoảng thời gian để phản công)
		if (b.State != 0 && b.State != 4 && dist < e.Radius + player.Radius)
		{
			bool charging = b.State == 3;
			player.TakeDamage(e.Damage * (charging ? 1.6f : 1f), e.Pos, e.Impact * (charging ? 1.3f : 1f));
		}
	}

	/// <summary>
	/// Chọn đòn kế tiếp. Dễ: ngẫu nhiên theo trọng số cố định. Độ khó càng cao boss càng "biết nhìn tình huống" (BossSmartness):
	///  - GIẬM chỉ có giá trị khi mèo SẼ còn nằm trong tầm lúc giậm xong (dự đoán đường đi); ở ngoài tầm thì ít khi giậm.
	///  - LAO hợp khi mèo ở xa, đang chạy thẳng (đón đầu dễ trúng) hoặc bị dồn sát tường; mèo đứng sát thì ít lao.
	///  - Không lặp lại đòn vừa tung.
	/// </summary>
	private static void ChooseAttack(Enemy e, BossData b, EnemyManager em, Player player, BossArena arena, float speedMul, Vector2 chargeDir)
	{
		int minions = 0;
		foreach (var o in em.Enemies) if (o != e && !o.Dead) minions++;

		float wCharge = 40f, wSlam = 35f, wSummon = minions < 6 && !b.ShieldUp ? 25f : 0f;
		float s = Difficulty.BossSmartness;
		if (s > 0f)
		{
			float dist = (player.Pos - e.Pos).Length();
			Vector2 landing = Prediction.PositionAfter(player, SlamWindup / speedMul, arena);       // mèo sẽ đứng đâu khi cú giậm tới
			bool slamHits = (landing - e.Pos).Length() < SlamRadius * 1.25f + player.Radius;
			wSlam *= slamHits ? 1f + 2.5f * s : 1f - 0.9f * s;
			wCharge *= dist > 140f ? 1f + 1.2f * s : 1f - 0.5f * s;
			if (player.VelFast.Length() > 60f) wCharge *= 1f + 0.6f * s;                              // mèo chạy thẳng: đón đầu rất dễ trúng
			if (IsCornered(player.Pos, arena)) wCharge *= 1f + 0.8f * s;                              // mèo sát tường: lao là hết đường né
			if (b.LastAttack == 1) wCharge *= 1f - 0.7f * s;
			else if (b.LastAttack == 2) wSlam *= 1f - 0.7f * s;
			else if (b.LastAttack == 3) wSummon *= 1f - 0.7f * s;
		}

		float roll = Rng.Randf() * (wCharge + wSlam + wSummon);
		b.Timer = 0f;
		if ((roll -= wCharge) < 0f) { b.State = 2; b.ChargeDir = chargeDir; b.LastAttack = 1; }
		else if ((roll -= wSlam) < 0f) { b.State = 5; b.LastAttack = 2; }
		else { EnterRoar(b); b.LastAttack = 3; }
	}

	/// <summary>Bắt đầu chưởng chữ thập: mắt chính đứng yên, 4 mắt con bay vào 4 hướng quanh nó.</summary>
	private static void StartCross(BossData b)
	{
		b.State = 9;
		b.Timer = 0f;
		b.CrossState = 1;
		b.CrossTimer = 0f;
		b.CrossAngle = Rng.Randf() * Mx.Tau;
		SoundService.Play("boss", -4f, 0.3f);
	}

	/// <summary>Gọi mắt con ra ở 4 phía quanh mắt chính cho đủ 4 con (chỉ thêm những vị trí còn thiếu: lần gọi bù không nhân đôi con còn sống).</summary>
	private static void SpawnMiniEyes(Enemy boss, EnemyManager em, BossArena arena)
	{
		var used = new bool[MiniEyes.Count];
		foreach (var o in em.MinisOf(boss)) if (o.Mini.Index >= 0 && o.Mini.Index < used.Length) used[o.Mini.Index] = true;
		for (int i = 0; i < MiniEyes.Count; i++)
		{
			if (used[i]) continue;
			Vector2 pos = arena.Clamp(boss.Pos + V.FromAngle(Mx.Pi / 4f + i * Mx.Pi / 2f) * 80f, MiniEyes.Radius);
			var m = EnemyTypes.Create(EnemyTypes.MiniEye, 0f, pos);
			m.Mini.Index = i;
			m.Mini.Owner = boss;
			m.Mini.ShootCd = 2f + 0.5f * i;
			em.PendingSpawns.Add(m);
		}
	}

	/// <summary>Cạn thanh máu: sang giai đoạn kế với thanh máu ĐẦY LẠI. 1 -> 2: gọi 4 mắt con; 2 -> 3: nổi điên, gọi thêm một mắt chính nữa (mỗi mắt 4 mắt con).</summary>
	private static void AdvanceStage(Enemy e, BossData b, EnemyManager em, BossArena arena)
	{
		b.BarEmpty = false;
		b.Stage = Math.Min(b.Stage + 1, 3);
		b.Phase = b.Stage;
		e.Hp = e.MaxHp;
		b.ShieldUp = true;
		b.State = 8;
		b.Timer = 0f;
		b.MinisSpawned = false;
		b.MinisCleared = false;
		b.CrossState = 0;
		b.ResummonCd = Difficulty.BossResummonInterval;
		b.Rings.Clear();
		SoundService.Play("boss", -2f, 0.5f);
		if (b.Stage == 2)
			em.Toast("GIAI ĐOẠN 2: diệt 4 mắt con để đánh được mắt chính!", 4f);
		else
		{
			em.Toast("GIAI ĐOẠN 3: Mắt Quỷ thứ hai xuất hiện - mỗi mắt có 4 mắt con!", 5f);
			em.SpawnSecondBoss(e, arena);
		}
	}

	/// <summary>Khó / Siêu khó: mỗi BossResummonInterval giây mắt chính gầm lên gọi BÙ mắt con (đủ lại 4 con) nếu đã mất con nào. Trả về true nếu vừa chuyển sang gầm.</summary>
	private static bool Resummon(Enemy e, BossData b, EnemyManager em, int minisAlive)
	{
		float every = Difficulty.BossResummonInterval;
		if (every <= 0f || b.Stage < 2 || !b.MinisSpawned) return false;
		if (b.ResummonCd > 0f) return false;
		b.ResummonCd = every;
		if (minisAlive >= MiniEyes.Count) return false;      // còn đủ mắt con thì bỏ lượt này
		b.State = 8;
		b.Timer = 0f;
		b.MinisSpawned = false;
		b.MinisCleared = false;
		b.CrossState = 0;
		SoundService.Play("boss", -2f, 0.5f);
		em.Toast("Mắt chính gọi thêm mắt con!", 3f);
		return true;
	}
	/// <summary>Mèo đang đứng sát mép vùng khoá (dưới 80px)?</summary>
	private static bool IsCornered(Vector2 p, BossArena arena)
	{
		var r = arena.Rect;
		float edge = MathF.Min(MathF.Min(p.X - r.Position.X, r.End.X - p.X), MathF.Min(p.Y - r.Position.Y, r.End.Y - p.Y));
		return edge < 80f;
	}

	/// <summary>Xoay hướng `from` về phía hướng `to` nhưng tối đa maxAngle radian.</summary>
	private static Vector2 TurnToward(Vector2 from, Vector2 to, float maxAngle)
	{
		if (to.LengthSquared() < 0.0001f) return from;
		float diff = Mx.Wrap(to.Angle() - from.Angle(), -Mx.Pi, Mx.Pi);
		return V.FromAngle(from.Angle() + Mx.Clamp(diff, -maxAngle, maxAngle));
	}
	private static void EnterRoar(BossData b)
	{
		b.State = 7;
		b.Timer = 0f;
		b.Summoned = false;
		SoundService.Play("boss", -2f, 0.5f);
	}

	/// <summary>Triệu hồi quái nhỏ (slime/dơi/bomber, màu theo pha) rải trong vùng, cách mèo ít nhất 100px.</summary>
	private static void SummonMinions(Enemy boss, BossData b, EnemyManager em, Player player, BossArena arena)
	{
		int alive = 0;
		foreach (var o in em.Enemies) if (o != boss && !o.Dead) alive++;
		int count = Math.Min(3 + 2 * b.Phase, MaxMinions - alive);
		float t = 300f + b.Phase * 120f;      // dùng thang thời gian để quái đủ mạnh cho giai đoạn cuối
		int variant = Math.Min(1 + b.Phase, EnemyTypes.VariantCount - 1);
		Rect2 r = arena.Rect;

		for (int i = 0; i < count; i++)
		{
			Vector2 pos = Vector2.Zero;
			for (int tries = 0; tries < 8; tries++)
			{
				pos = r.Position + new Vector2(Rng.Randf() * r.Size.X, Rng.Randf() * r.Size.Y);
				if ((pos - player.Pos).Length() > 100f) break;
			}
			float k = Rng.Randf();
			int kind = k < 0.6f ? EnemyTypes.Slime : k < 0.9f ? EnemyTypes.Bat : EnemyTypes.Bomber;
			var m = EnemyTypes.Create(kind, t, pos, 1f, variant);
			m.NoDrop = true; // trận boss không rơi XP
			em.PendingSpawns.Add(m); // không thêm thẳng vào Enemies vì đang nằm trong vòng duyệt Simulate
		}
	}

	private static void UpdateRings(BossData b, float dt, Player player)
	{
		for (int i = b.Rings.Count - 1; i >= 0; i--)
		{
			var ring = b.Rings[i];
			ring.R += RingSpeed * dt;
			if (!ring.Hit)
			{
				float d = (player.Pos - ring.Center).Length();
				if (MathF.Abs(d - ring.R) <= RingWidth + player.Radius)
				{
					ring.Hit = true; // dash xuyên qua lúc vòng đi qua: TakeDamage bị bỏ qua (bất tử) và vòng coi như đã qua
					player.TakeDamage(Balance.BossRingDamage * Difficulty.EnemyDamageMul, ring.Center, 300f);
				}
			}
			if (ring.R > RingMax) b.Rings.RemoveAt(i);
		}
	}
}
