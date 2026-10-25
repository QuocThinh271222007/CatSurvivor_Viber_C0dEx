using System.Numerics;

namespace CatSurvivor;

/// <summary>
/// Mọi "trần nhà" (giá trị tối đa) của chỉ số sức mạnh, gom về một chỗ để dễ cân bằng.
/// Không có chỉ số nào được tăng vô hạn: chơi lâu bao nhiêu cũng không vượt các mức dưới đây.
/// </summary>
public static class Balance
{
	// ---------------------------------------------------------------- Người chơi (mèo)
	public const float PlayerMaxHp = 400f;        // máu tối đa (gốc 100, +20 mỗi lần nâng)
	public const float PlayerSpeed = 220f;        // tốc độ chạy (gốc 110; quái nhanh nhất ~130 nên vẫn có thể đuổi kịp)
	public const float PlayerMagnet = 240f;       // bán kính hút XP (gốc 60)
	public const float PlayerRegen = 5f;          // hồi máu mỗi giây (gốc 0)
	public const float PlayerMaxMana = 100f;      // mana tối đa ban đầu: tiêu cho các kĩ năng chủ động (xem Skills.cs)
	public const float PlayerManaRegen = 6f;      // mana tự hồi mỗi giây ban đầu
	public const float PlayerManaCap = 200f;      // trần mana tối đa (thẻ "Mana tối đa +20")
	public const float PlayerManaRegenCap = 15f;  // trần hồi mana mỗi giây (thẻ "Hồi mana +1.5/s")
	public const float PlayerDamageMul = 3f;      // nhân sát thương mọi vũ khí (gốc 1)
	public const float PlayerBaseArmor = 10f;     // giáp gốc tối đa (nâng ở menu "Giáp +2")
	public const float PlayerArmor = 24f;         // TỔNG giáp tối đa (giáp gốc + trang bị)
	public const int EquipmentMaxLevel = 5;       // cấp tối đa của mỗi món trang bị
	public const float KnockResistMax = 0.5f;     // trần giảm bị đẩy lùi
	public const float StunResistMax = 0.75f;     // trần giảm thời gian choáng
	public const float BlockChanceMax = 0.2f;     // trần cơ hội đỡ đòn
	public const float EquipSpeedMin = 0.6f;      // trần phạt tốc độ do trang bị (hệ số tốc độ không thấp hơn mức này)
	// Dash: lao nhanh một đoạn ngắn, bất tử trong lúc lao, húc văng quái chạm phải
	public const float DashSpeed = 440f;          // tốc độ lao (px/s); mèo nặng thì lao chậm hơn (chia căn weight, tối thiểu 70%)
	public const float DashTime = 0.11f;          // thời gian lao (giây) ~ 48px
	public const float DashGrace = 0.08f;         // bất tử thêm sau khi lao xong
	public const int DashBaseCharges = 2;         // số lượt dash ban đầu
	public const int DashMaxCharges = 4;          // trần số lượt dash
	public const float DashBaseRecharge = 20f;    // giây để hồi một lượt
	public const float DashRechargeMin = 8f;      // trần tốc độ hồi (hồi nhanh nhất)
	public const float DashForceScale = 130f;     // lực húc quái = Weight mèo x hệ số này
	public const float DashDamage = 6f;           // sát thương húc (nhân DamageMul)
	public const float PlayerMaxImpact = 450f;    // trần lực một đòn đẩy lùi mèo
	public const float PlayerMaxRecoil = 260f;    // trần vận tốc giật lùi khi vung vũ khí nặng trúng quái
	public const int MaxPlayerLevel = 27;         // cấp tối đa của người chơi (khoá ở đây); đạt cấp này thì mở màn đánh boss
	public const int WeaponMaxLevel = 8;          // cấp tối đa của mỗi vũ khí
	public const float WeaponMinCooldown = 0.25f; // thời gian hồi chiêu ngắn nhất của vũ khí (giây)
	public const float WeaponForceScale = 110f;   // lực đẩy (px/s) của vũ khí có Weight = 1
	public const float RecoilMinWeight = 1.5f;    // vũ khí có Weight từ mức này trở lên mới giật lùi người chơi khi trúng quái
	public const float WeaponRecoilScale = 0.6f;  // hệ số giật lùi (xem Weapon.HitEnemy)
	public const float FireRateMax = 2f;          // trần tốc độ bắn CHUNG (thẻ "Tốc độ bắn"): x1 -> x2
	public const float WeaponRateMax = 2f;        // trần tốc độ bắn RIÊNG của một vũ khí (thẻ "<vũ khí>: bắn nhanh")
	public const float FireRateTotalMax = 3f;     // trần tổng (chung x riêng) - hồi chiêu không bao giờ nhanh hơn 1/3
	public const int FusionMinLevel = 5;          // cả hai vũ khí phải đạt cấp này mới được hợp nhất

	// ---------------------------------------------------------------- Giáp (chung cho cả mèo và quái)
	// Sát thương thực nhận = max(sát thương - giáp, sát thương x ArmorMinFraction): giáp không bao giờ miễn nhiễm hoàn toàn.
	public const float ArmorMinFraction = 0.2f;
	public const float ArmorWeightPerPoint = 0.05f; // mỗi điểm giáp của mèo cộng thêm vào Weight

	// ---------------------------------------------------------------- Quái vật
	public const float EnemyHpMul = 15f;          // máu tối đa gấp 15 lần gốc
	public const float EnemyDamageMul = 4f;       // sát thương tối đa gấp 4 lần gốc
	public const float EnemySpeedBonus = 0.4f;    // tốc độ tối đa +40%
	public const float EnemyArmorMax = 12f;       // giáp tối đa của quái (sau khi nhân Size)

	// Trần tuyệt đối của giá trị cuối cùng (sau khi nhân cả Size khổng lồ)
	public const float EnemyHpHard = 2000f;
	public const float EnemyDamageHard = 60f;
	public const float EnemySpeedHard = 130f;
	public const float EnemySizeMax = 2f;

	/// <summary>
	/// Biến thể MÀU của quái (Lục/Vàng/Đỏ/Tím theo sức mạnh) đang tạm ẨN: quái vẫn có chỉ số theo bậc nhưng vẽ bằng sprite gốc.
	/// Muốn dùng sprite vẽ tay: đặt file Assets/enemies/{slime|bat|bomber}_v{1..4}.png (cùng bố cục dải frame với file gốc) - game tự dùng.
	/// Đặt true để bật lại màu tự động đổi từ sprite gốc cho các bậc chưa có file vẽ tay.
	/// </summary>
	public static bool ShowColorVariants = false;

	// Boss
	public const float BossBarHp = 1000f;        // máu MỖI THANH của boss (mỗi giai đoạn một thanh riêng, x hệ số độ khó); 3 giai đoạn + mắt chính thứ hai ở giai đoạn 3 = 4 thanh
	public const float BossDamage = 26f;          // sát thương chạm / vụ húc (x1.6 khi đang lao)
	public const float BossRingDamage = 22f;      // sát thương sóng xung kích khi giậm
	public const float BossArmor = 5f;
	public static readonly Vector2 BossArenaSize = new(1000f, 560f); // vùng khoá khi đánh boss: RỘNG HƠN màn hình 640x360, camera đi theo mèo trong vùng

	// XP tự sinh trên map (không vô tận)
	public const int AmbientMaxAlive = 60;        // (trước đây 40, ban đầu 15)
	public const int AmbientPerMinute = 100;      // (trước đây 50, ban đầu 12)

	// Số lượng
	public const int EnemySpawnPerTick = 5;
	public const int SwarmMax = 70;

	// Kích thước màn hình logic của game (pixel-art 16:9). Cửa sổ có thể to hơn, game tự phóng to đều.
	public const float ViewWidth = 640f;
	public const float ViewHeight = 360f;
}
