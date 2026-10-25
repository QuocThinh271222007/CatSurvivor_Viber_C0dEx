using System.Numerics;

namespace CatSurvivor;

/// <summary>Dữ liệu một con quái. Chỉ là data – EnemyManager mô phỏng và vẽ tất cả.</summary>
public class Enemy
{
	public int Kind;
	public Vector2 Pos;            // vị trí trên mặt đất (không tính độ cao Z)
	public Vector2 Knock;          // đẩy lùi nhẹ khi đang chạm đất, giảm dần
	public float Hp, MaxHp;
	public float Speed, Damage, Radius, Xp;
	public int Variant;            // BIẾN THỂ MÀU (0 = màu gốc, 1 Lục, 2 Vàng, 3 Đỏ, 4 Tím): màu càng nóng thì càng mạnh
	public bool IsSpecial;         // biến thể ĐẶC BIỆT (hiếm, có dấu nhận biết): máu x1.5, XP x2, và dùng được đòn riêng (slime: vồ tới)
	public float Size = 1f;        // độ lớn (hệ số kích thước): scale hình vẽ, vùng va chạm, phạm vi chiêu; kéo theo máu/sát thương/weight/XP
	public float Armor;            // giáp: mỗi đòn nhận vào bị trừ chừng này (tối thiểu còn ArmorMinFraction sát thương gốc)
	public float Weight = 1f;      // trọng lượng: lực đẩy nhận vào = lực / Weight; quái nặng cũng đẩy quái nhẹ khi chen nhau
	public float Impact = 100f;    // độ mạnh đẩy lùi người chơi khi chạm
	public float Flash;            // > 0: đang nháy trắng khi trúng đòn
	public float HitStop;          // > 0: đứng khựng lại (hit-stop) sau khi trúng đòn, hết khựng mới bị văng/chết
	public Vector2 PendingKnock;   // lực đẩy dồn lại trong lúc khựng, áp dụng khi hết khựng
	public float AnimTime;         // để chạy animation, lệch pha mỗi con
	public float FaceX = 1f;       // hướng nhìn theo CHIỀU DI CHUYỂN thực tế (dương = sang phải)
	public Rgba Color;
	public bool Dead;              // hết máu / đã nổ: không còn bị đánh, không gây sát thương
	public float Linger;           // thời gian xác nằm lại (sau khi đáp xuống) trước khi biến mất
	public bool NoDrop;            // true: không rơi XP (vd. bomber tự nổ)

	// --- Trạng thái bị văng: mô phỏng "độ cao" Z để tạo cảm giác 3D (top-down) ---
	public bool Airborne;
	public float Z, Vz;            // độ cao và vận tốc đứng
	public Vector2 Vel;            // vận tốc ngang khi đang bay
	public int Bounces;            // số lần đã nảy
	public float LandTimer;        // > 0: vừa chạm đất (để hiện frame bẹp)

	// --- Đội hình: đang thuộc đội hình nào thì bám theo "ô" (Slot) của mình thay vì tự do truy đuổi ---
	public Formation Group;
	public Vector2 Slot;           // vị trí trong đội hình: X = dọc hướng tiến (âm = phía sau), Y = ngang

	// --- "Tính cách" riêng từng con ---
	public float Phase;            // pha ngẫu nhiên cho dao động/lượn
	public float Bias;             // Slime: lệch hướng khi áp sát (-1..1) để mỗi con vây từ một phía khác nhau

	// --- Riêng Bat: bay lắt léo (lượn + phóng ngang bất chợt) và vòng quanh người chơi ---
	public float DartTimer;        // đếm ngược tới lần phóng ngang kế tiếp
	public float DartLeft;         // > 0: đang phóng ngang
	public float DartSign;
	public float OrbitSign = 1f;   // chiều lượn vòng quanh người chơi
	public float OrbitTimer;

	// --- Riêng Slime đặc biệt: đòn vồ tới (gồng -> lao một cú về phía người chơi -> khựng) ---
	public int PounceState;        // 0 bình thường · 1 gồng (báo hiệu) · 2 đang vồ · 3 khựng sau khi vồ
	public float PounceTimer, PounceCd, PounceAttempt, PounceSpeed;
	public Vector2 PounceDir;
	public bool PounceHit;         // đã trúng người chơi trong lần vồ này chưa (mỗi lần vồ chỉ trúng một lần)

	// --- Riêng Brute: lao tới (Walk -> Windup -> Charge -> Tired) ---
	public int ChargeState;        // 0 đi bộ · 1 gồng (báo hiệu) · 2 lao · 3 kiệt sức
	public float ChargeTimer, ChargeCd;
	public Vector2 ChargeDir;

	// --- Riêng Brute: KHIÊN. Còn khiên thì đòn đánh chỉ trừ vào khiên (không mất máu) và brute vẫn lao tới; khiên vỡ thì brute lộ máu,
	//     vẫn đi chậm chậm theo người chơi nhưng KHÔNG lao nữa ---
	public float ShieldHp, ShieldMax;
	public bool ShieldBroken;        // khiên đã vỡ (không bao giờ có lại)
	public float ShieldFlash;        // > 0: khiên vừa bị đánh (nháy)
	public float ShieldBreakT = -1f; // >= 0: thời gian từ lúc khiên vỡ (hiệu ứng mảnh khiên bay ra)

	// --- Riêng Bat: tiếng gào tạo vùng phễu 2 bên ---
	public float ScreechTime = -1f; // < 0: không gào; >= 0: thời gian đã trôi qua của chu kỳ gào
	public float ScreechCd;         // hồi chiêu
	public bool ScreechHit;         // đã tính sát thương của lần gào này chưa

	// --- Riêng Bomber ---
	public float FuseTime = -1f;   // < 0: chưa châm ngòi; >= 0: đếm ngược tới khi nổ
	public float FuseElapsed;
	public bool Exploded;

	// --- Riêng Boss ---
	public BossData Boss;          // trạng thái AI của boss (null với quái thường)

	// --- Riêng Mắt con (giai đoạn 2 của boss) ---
	public MiniEyeData Mini;       // trạng thái AI mắt con (null với quái khác)
}

public static class EnemyTypes
{
	public const int Slime = 0;   // quái cơ bản, đi đông
	public const int Bat = 1;     // bay nhanh, lượn zigzag, nhẹ nên dễ bị văng xa
	public const int Bomber = 2;  // chạy tới gần rồi châm ngòi, nổ gây sát thương vùng
	public const int Brute = 3;   // chậm, trâu, đòn rất nặng, cầm KHIÊN (còn khiên thì lao tới; vỡ khiên thì chỉ đi chậm)
	public const int Boss = 4;    // boss cuối: chỉ xuất hiện ở màn đánh boss (xem BossArena / BossAI)
	public const int MiniEye = 5; // mắt con: 4 con hỗ trợ boss ở giai đoạn 2, chạy trốn mèo và bắn chưởng (xem MiniEyes.cs)

	// --- Quái mạnh lên theo thời gian (phút chơi m = t/60) ---
	//   máu    x (1 + 0.6*m + 0.03*m²)   ->  1 phút: x1.6 · 5 phút: x4.8 · 10 phút: x10
	//   sát thương x (1 + 0.3*m)         ->  1 phút: x1.3 · 5 phút: x2.5 · 10 phút: x4
	//   tốc độ  +4%/phút
	//   Tất cả đều có trần: máu tối đa x15, sát thương tối đa x4, tốc độ tối đa +40% (và trần tuyệt đối sau khi nhân Size)
	private const float HpPerMinute = 0.6f;
	private const float HpPerMinuteSq = 0.03f;
	private const float DamagePerMinute = 0.3f;
	private const float SpeedPerMinute = 0.04f;
	private const float ArmorPerMinute = 0.3f;     // giáp cộng thêm mỗi phút chơi (cho mọi loại quái)

	// --- Biến thể MÀU: màu đại diện cho sức mạnh ---
	//   bậc  màu         máu    sát thương  tốc độ  giáp  XP     xuất hiện từ
	//   0    gốc/xanh    x1.0   x1.0        x1.0    +0    x1.0   đầu ván
	//   1    Lục         x1.25  x1.1        x1.0    +0    x1.3   30s
	//   2    Vàng        x1.6   x1.25       x1.05   +1    x1.8   2 phút
	//   3    Đỏ          x2.2   x1.5        x1.10   +2    x2.5   4 phút
	//   4    Tím         x3.0   x1.8        x1.15   +3    x4.0   7 phút
	public const int VariantCount = 5;
	public static readonly string[] VariantNames = { "Thường", "Lục", "Vàng", "Đỏ", "Tím" };
	private static readonly float[] VariantHp = { 1f, 1.25f, 1.6f, 2.2f, 3.0f };
	private static readonly float[] VariantDamage = { 1f, 1.1f, 1.25f, 1.5f, 1.8f };
	private static readonly float[] VariantSpeed = { 1f, 1f, 1.05f, 1.10f, 1.15f };
	private static readonly float[] VariantArmor = { 0f, 0f, 1f, 2f, 3f };
	private static readonly float[] VariantXp = { 1f, 1.3f, 1.8f, 2.5f, 4f };

	/// <summary>Bảng màu mỗi bậc: tối (viền, bóng) -> vừa (thân) -> sáng (điểm sáng). Bậc 0 giữ nguyên màu gốc của sprite.</summary>
	public static readonly (Rgba dark, Rgba mid, Rgba light)[] Palette =
	{
		(new Rgba(0, 0, 0), new Rgba(0, 0, 0), new Rgba(0, 0, 0)), // bậc 0: không dùng (màu gốc)
		(new Rgba(0.04f, 0.22f, 0.07f), new Rgba(0.25f, 0.75f, 0.30f), new Rgba(0.75f, 1.00f, 0.75f)), // Lục
		(new Rgba(0.30f, 0.20f, 0.02f), new Rgba(0.95f, 0.75f, 0.15f), new Rgba(1.00f, 0.97f, 0.70f)), // Vàng
		(new Rgba(0.30f, 0.03f, 0.03f), new Rgba(0.90f, 0.20f, 0.20f), new Rgba(1.00f, 0.80f, 0.75f)), // Đỏ
		(new Rgba(0.15f, 0.05f, 0.30f), new Rgba(0.60f, 0.25f, 0.90f), new Rgba(0.92f, 0.80f, 1.00f)), // Tím
	};

	/// <summary>Chọn bậc biến thể theo thời gian chơi: lúc đầu toàn màu gốc, các bậc mạnh mở khoá và tăng tỉ lệ dần.</summary>
	public static int PickVariant(float t)
	{
		float m = t / 60f;
		float[] w =
		{
			MathF.Max(0.35f, 1f - m * 0.07f),
			t > 30f ? MathF.Min(0.9f, 0.25f + m * 0.15f) : 0f,
			t > 120f ? MathF.Min(0.8f, 0.15f + (m - 2f) * 0.12f) : 0f,
			t > 240f ? MathF.Min(0.6f, 0.10f + (m - 4f) * 0.10f) : 0f,
			t > 420f ? MathF.Min(0.4f, 0.05f + (m - 7f) * 0.08f) : 0f,
		};
		float total = 0f;
		foreach (float x in w) total += x;
		float roll = Rng.Randf() * total;
		for (int i = 0; i < w.Length; i++)
			if ((roll -= w[i]) < 0f) return i;
		return 0;
	}

	// --- Độ lớn (Size) ---
	// Mỗi con có Size ngẫu nhiên quanh 1 (0.85-1.2); thỉnh thoảng xuất hiện "khổng lồ" (Size 1.5-2.0, Brute 1.25-1.5),
	// xác suất tăng dần theo thời gian (1% lúc đầu -> 20% ở phút 15). Size ảnh hưởng tới chỉ số như sau (s = Size):
	//   bán kính, sát thương, lực đẩy người chơi : x s
	//   máu, weight (độ nặng), XP rơi            : x s²
	//   tốc độ                                   : x 1/√s
	// --- Quái ĐẶC BIỆT (special): chỉ slime, chỉ khi sinh ngẫu nhiên (không thuộc đội hình). Hiếm, tăng dần theo thời gian ---
	private const float SpecialBaseChance = 0.06f;
	private const float SpecialExtraChance = 0.08f;  // cộng thêm dần tới phút 10 (6% -> 14%)
	private const float SpecialHpMul = 1.5f;
	private const float SpecialXpMul = 2f;

	// Quái KHỔNG LỒ: lúc đầu rất hiếm (1%), tăng dần theo thời gian chơi tới 20% ở phút 15 (tuyến tính)
	private const float GiantBaseChance = 0.01f;
	private const float GiantExtraChance = 0.19f;
	private const float GiantMinutesToMax = 15f;

	/// <summary>Độ bền khiên của Brute = tỉ lệ này x máu tối đa của nó (đánh hết khiên mới bắt đầu mất máu).</summary>
	public const float BruteShieldRatio = 0.6f;

	/// <summary>Chọn Size ngẫu nhiên cho một con quái thường (không thuộc đội hình).</summary>
	public static float RandomSize(int kind, float t)
	{
		float giantChance = GiantBaseChance + GiantExtraChance * MathF.Min(t / 60f / GiantMinutesToMax, 1f);
		bool giant = Rng.Randf() < giantChance;
		if (kind == Brute)
			return giant ? 1.25f + Rng.Randf() * 0.25f : 0.95f + Rng.Randf() * 0.2f;
		return giant ? 1.5f + Rng.Randf() * 0.5f : 0.85f + Rng.Randf() * 0.35f;
	}

	/// <summary>Boss: chỉ số cố định (không scale theo thời gian/size/biến thể), rất nặng nên gần như không bị đẩy văng.</summary>
	private static Enemy CreateBoss(Vector2 pos)
	{
		var b = new Enemy
		{
			Kind = Boss,
			Pos = pos,
			MaxHp = Balance.BossBarHp * Difficulty.EnemyHpMul,   // máu của THANH hiện tại (hết thanh thì sang giai đoạn kế, xem BossAI)
			Speed = 45f,
			Damage = Balance.BossDamage * Difficulty.EnemyDamageMul,
			Radius = 32f,
			Xp = 0f,
			Weight = 25f,
			Impact = 420f,
			Armor = Balance.BossArmor,
			Color = new Rgba(0.16f, 0.12f, 0.14f),
			NoDrop = true,
			Boss = new BossData(),
		};
		b.Hp = b.MaxHp;
		return b;
	}

	/// <summary>Mắt con: chỉ số cố định theo độ khó (máu x hệ số độ khó), không gây sát thương chạm - đòn của chúng là chưởng (EyeBolt) và chữ thập.</summary>
	private static Enemy CreateMiniEye(Vector2 pos)
	{
		var e = new Enemy
		{
			Kind = MiniEye,
			Pos = pos,
			MaxHp = MiniEyes.Hp * Difficulty.EnemyHpMul,
			Speed = MiniEyes.FleeSpeed,
			Damage = 0f,
			Radius = MiniEyes.Radius,
			Weight = 5f,
			Impact = 0f,
			Armor = 2f,
			Color = new Rgba(0.7f, 0.15f, 0.15f),
			NoDrop = true,
			AnimTime = Rng.Randf() * 10f,
			Mini = new MiniEyeData { Strafe = Rng.Randf() < 0.5f ? -1f : 1f },
		};
		e.Hp = e.MaxHp;
		return e;
	}

	/// <summary>Tạo quái, chỉ số tăng dần theo thời gian chơi t (giây). size = 0: chọn ngẫu nhiên.</summary>
	public static Enemy Create(int kind, float t, Vector2 pos, float size = 0f, int variant = -1)
	{
		if (kind == Boss) return CreateBoss(pos);
		if (kind == MiniEye) return CreateMiniEye(pos);
		bool randomSpawn = size <= 0f; // size = 0: sinh ngẫu nhiên (đội hình thì truyền size cụ thể)
		// variant < 0: sinh ngẫu nhiên thì chọn theo thời gian; nếu truyền size cụ thể thì mặc định bậc 0
		if (variant < 0) variant = randomSpawn ? PickVariant(t) : 0;
		variant = Mx.Clamp(variant, 0, VariantCount - 1);
		if (size <= 0f) size = RandomSize(kind, t);
		size = MathF.Min(size, Balance.EnemySizeMax);
		float m = t / 60f;
		// Mọi hệ số đều có trần (Balance.cs)
		float hpMul = MathF.Min(1f + HpPerMinute * m + HpPerMinuteSq * m * m, Balance.EnemyHpMul);
		float spdMul = 1f + MathF.Min(SpeedPerMinute * m, Balance.EnemySpeedBonus);
		float dmgMul = MathF.Min(1f + DamagePerMinute * m, Balance.EnemyDamageMul);

		float baseArmor = 0f;
		var e = new Enemy
		{
			Kind = kind,
			Pos = pos,
			AnimTime = Rng.Randf() * 10f,
			Phase = Rng.Randf() * Mx.Tau,
			Bias = Rng.Randf() * 2f - 1f,
			DartTimer = 0.3f + Rng.Randf(),
			OrbitSign = Rng.Randf() < 0.5f ? -1f : 1f,
			OrbitTimer = 1f + Rng.Randf() * 2f,
			ChargeCd = 1.5f + Rng.Randf() * 2f,
			PounceCd = 3f + Rng.Randf() * 3f,
		};
		switch (kind)
		{
			case Slime:
				e.MaxHp = 8f; e.Speed = 48f; e.Damage = 5f; e.Radius = 10f; e.Xp = 1f;
				e.Weight = 1f; e.Impact = 110f; baseArmor = 0f;
				e.Color = new Rgba(0.4f, 0.6f, 1f);
				break;
			case Bat:
				e.MaxHp = 6f; e.Speed = 75f; e.Damage = 6f; e.Radius = 8f; e.Xp = 1f;
				e.Weight = 0.6f; e.Impact = 90f; baseArmor = 0f;      // nhẹ nhất: bị đánh là bay xa
				e.Color = new Rgba(0.6f, 0.6f, 0.7f);
				break;
			case Bomber:
				e.MaxHp = 12f; e.Speed = 80f; e.Damage = 16f; e.Radius = 9f; e.Xp = 3f;
				e.Weight = 1.2f; e.Impact = 320f; baseArmor = 1f; // vụ nổ: đẩy xa + choáng người chơi
				e.Color = new Rgba(0.15f, 0.15f, 0.15f);
				break;
			default:
				e.MaxHp = 70f; e.Speed = 28f; e.Damage = 18f; e.Radius = 16f; e.Xp = 6f;
				e.Weight = 3f; e.Impact = 300f; baseArmor = 4f; // đòn nặng: đẩy xa + choáng người chơi
				e.Color = new Rgba(0.62f, 0.52f, 0.46f); // màu gốc trung tính (đỏ dành cho biến thể bậc 3)
				break;
		}
		e.MaxHp *= hpMul;
		e.Speed *= spdMul;
		e.Damage *= dmgMul;

		// Áp dụng biến thể màu
		e.Variant = variant;
		e.MaxHp *= VariantHp[variant];
		e.Damage *= VariantDamage[variant];
		e.Speed *= VariantSpeed[variant];
		e.Xp *= VariantXp[variant];
		if (variant > 0 && Balance.ShowColorVariants) e.Color = Palette[variant].mid;

		// Áp dụng Size
		e.Size = size;
		e.Radius *= size;
		e.Damage *= size;
		e.Impact *= size;
		e.MaxHp *= size * size;
		e.Weight *= size * size;
		e.Xp *= size * size;
		e.Speed /= MathF.Sqrt(size);
		e.Armor = MathF.Min((baseArmor + ArmorPerMinute * m + VariantArmor[variant]) * size, Balance.EnemyArmorMax); // to hơn -> giáp dày hơn

		// Trần tuyệt đối của giá trị cuối cùng (sau khi nhân cả Size)
		e.MaxHp = MathF.Min(e.MaxHp, Balance.EnemyHpHard);
		e.Damage = MathF.Min(e.Damage, Balance.EnemyDamageHard);
		e.Speed = MathF.Min(e.Speed, Balance.EnemySpeedHard);
		e.MaxHp *= Difficulty.EnemyHpMul;         // độ khó Trung bình trở lên: quái trâu hơn / đau hơn (áp sau trần: trần là của chỉ số gốc)
		e.Damage *= Difficulty.EnemyDamageMul;
		// Quái đặc biệt: hiếm, trâu hơn, thưởng nhiều hơn, và dùng được đòn riêng
		if (kind == Slime && randomSpawn && Rng.Randf() < SpecialBaseChance + SpecialExtraChance * MathF.Min(t / 600f, 1f))
		{
			e.IsSpecial = true;
			e.MaxHp *= SpecialHpMul;
			e.Xp *= SpecialXpMul;
		}

		e.Hp = e.MaxHp;
		if (kind == Brute) e.ShieldHp = e.ShieldMax = e.MaxHp * BruteShieldRatio;
		return e;
	}
}
