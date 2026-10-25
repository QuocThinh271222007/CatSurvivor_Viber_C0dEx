using System.Numerics;

namespace CatSurvivor;

public enum EquipSlot { Helm, Body, Boots, Shield }

/// <summary>
/// Trang bị giáp: 4 ô (Mũ, Áo giáp, Giày, Khiên), mỗi ô có một món nâng được tới cấp Balance.EquipmentMaxLevel.
/// Tên theo cấp: Vải -> Sắt -> Thép -> Mithril -> Rồng. Mỗi món cho GIÁP kèm một đặc tính riêng:
///
///   Mũ       : giáp +1.5/cấp · giảm thời gian bị choáng 15%/cấp
///   Áo giáp  : giáp +3/cấp   · máu tối đa +15/cấp · chậm đi 3%/cấp (nặng)
///   Giày     : giáp +1/cấp   · tốc độ +4%/cấp · giảm lực bị đẩy lùi 10%/cấp
///   Khiên    : giáp +2/cấp   · 4%/cấp cơ hội đỡ hoàn toàn một đòn (không mất máu, không bị đẩy)
///
/// Tổng giáp = giáp gốc (nâng ở menu) + giáp trang bị, bị chặn bởi Balance.PlayerArmor. Các đặc tính cũng có trần.
/// </summary>
public class Equipment
{
	public const int MaxLevel = Balance.EquipmentMaxLevel;
	private static readonly string[] Tiers = { "Vải", "Sắt", "Thép", "Mithril", "Rồng" };

	public EquipSlot Slot { get; }
	public int Level { get; private set; }

	public Equipment(EquipSlot slot, int level = 1)
	{
		Slot = slot;
		Level = Math.Clamp(level, 1, MaxLevel);
	}

	public void LevelUp() { if (Level < MaxLevel) Level++; }

	public static string SlotLabel(EquipSlot s) => s switch
	{
		EquipSlot.Helm => "Mũ",
		EquipSlot.Body => "Áo giáp",
		EquipSlot.Boots => "Giày",
		_ => "Khiên",
	};

	public string TierName => Tiers[Level - 1];
	public string Name => $"{SlotLabel(Slot)} {TierName}";

	// ---- Chỉ số theo cấp (hàm tĩnh để menu xem trước cấp kế tiếp) ----
	public static float ArmorAt(EquipSlot s, int level) => level * s switch
	{
		EquipSlot.Helm => 1.5f,
		EquipSlot.Body => 3f,
		EquipSlot.Boots => 1f,
		_ => 2f,
	};

	public static float SpeedBonusAt(EquipSlot s, int level) => s switch
	{
		EquipSlot.Body => -0.03f * level,
		EquipSlot.Boots => 0.04f * level,
		_ => 0f,
	};

	public static float KnockResistAt(EquipSlot s, int level) => s == EquipSlot.Boots ? 0.10f * level : 0f;
	public static float StunResistAt(EquipSlot s, int level) => s == EquipSlot.Helm ? 0.15f * level : 0f;
	public static float BlockChanceAt(EquipSlot s, int level) => s == EquipSlot.Shield ? 0.04f * level : 0f;
	public static float MaxHpPerLevel(EquipSlot s) => s == EquipSlot.Body ? 15f : 0f;

	public float Armor => ArmorAt(Slot, Level);
	public float SpeedBonus => SpeedBonusAt(Slot, Level);
	public float KnockResist => KnockResistAt(Slot, Level);
	public float StunResist => StunResistAt(Slot, Level);
	public float BlockChance => BlockChanceAt(Slot, Level);

	/// <summary>Mô tả chỉ số của món ở một cấp nào đó (dùng cho menu nâng cấp).</summary>
	public static string Describe(EquipSlot s, int level)
	{
		string text = $"Giáp +{ArmorAt(s, level):0.#}";
		if (MaxHpPerLevel(s) > 0f) text += $", máu +{MaxHpPerLevel(s) * level:0}";
		float sp = SpeedBonusAt(s, level);
		if (sp != 0f) text += $", tốc độ {(sp > 0 ? "+" : "")}{sp * 100f:0}%";
		if (KnockResistAt(s, level) > 0f) text += $", giảm bị đẩy lùi {KnockResistAt(s, level) * 100f:0}%";
		if (StunResistAt(s, level) > 0f) text += $", giảm choáng {StunResistAt(s, level) * 100f:0}%";
		if (BlockChanceAt(s, level) > 0f) text += $", đỡ đòn {BlockChanceAt(s, level) * 100f:0}%";
		return text;
	}
}

/// <summary>
/// Nạp asset trang bị nếu có (không có thì bỏ qua, game vẫn chạy bình thường).
///   Ảnh món đồ : Assets/equipment/icons/{helm|body|boots|shield}_{1..5}.png   (một khung 64x64, nền trong suốt). Dùng làm icon ở thẻ nâng cấp (nguyên bản)
///                và ở HUD (bản thu nhỏ 1/2 - xem Small). Mèo KHÔNG mặc giáp trên người: giáp chỉ cho chỉ số và hiện ở HUD.
/// </summary>
public static class EquipmentAssets
{
	private static readonly Dictionary<(EquipSlot, int), Bitmap> Items = new();
	private static readonly Dictionary<(EquipSlot, int), Bitmap> Smalls = new();

	/// <summary>Ảnh món đồ đã cắt sát viền (null nếu chưa có file).</summary>
	public static Bitmap Item(EquipSlot s, int level)
	{
		if (Items.TryGetValue((s, level), out var cached)) return cached;
		Bitmap result = null;
		try
		{
			var src = AssetStore.OptionalImage($"equipment/icons/{Key(s)}_{level}.png");
			if (src != null) result = CropToContent(src);
		}
		catch (Exception ex) { AssetStore.Report($"equipment/icons/{Key(s)}_{level}.png", ex.Message); }
		Items[(s, level)] = result;
		return result;
	}

	/// <summary>
	/// Bản thu nhỏ 1/2 của món đồ (cho HUD). Không dùng nearest/bilinear của GDI+ (nearest bỏ cách một điểm ảnh làm nét vẽ đứt gãy,
	/// bilinear làm mờ): mỗi điểm ảnh mới = trung bình màu của khối 2x2 (chỉ tính điểm ảnh đặc) và đặc nếu >= một nửa khối đặc -> vẫn sắc nét, không viền mờ.
	/// </summary>
	public static Bitmap Small(EquipSlot s, int level)
	{
		if (Smalls.TryGetValue((s, level), out var cached)) return cached;
		var big = Item(s, level);
		Bitmap result = null;
		if (big != null)
		{
			int w = (big.Width + 1) / 2, h = (big.Height + 1) / 2;
			result = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
			for (int y = 0; y < h; y++)
				for (int x = 0; x < w; x++)
				{
					int n = 0, r = 0, g = 0, b = 0;
					for (int dy = 0; dy < 2; dy++)
						for (int dx = 0; dx < 2; dx++)
						{
							int sx = x * 2 + dx, sy = y * 2 + dy;
							if (sx >= big.Width || sy >= big.Height) continue;
							var p = big.GetPixel(sx, sy);
							if (p.A < 128) continue;
							n++; r += p.R; g += p.G; b += p.B;
						}
					if (n >= 2) result.SetPixel(x, y, Color.FromArgb(255, r / n, g / n, b / n));
				}
		}
		Smalls[(s, level)] = result;
		return result;
	}

	/// <summary>Cắt ảnh sát vùng có điểm ảnh (bỏ viền trong suốt); null nếu ảnh trống.</summary>
	public static Bitmap CropToContent(Bitmap src)
	{
		int x0 = src.Width, y0 = src.Height, x1 = -1, y1 = -1;
		for (int y = 0; y < src.Height; y++)
			for (int x = 0; x < src.Width; x++)
				if (src.GetPixel(x, y).A > 0)
				{
					if (x < x0) x0 = x; if (x > x1) x1 = x;
					if (y < y0) y0 = y; if (y > y1) y1 = y;
				}
		if (x1 < 0) return null;
		return src.Clone(new Rectangle(x0, y0, x1 - x0 + 1, y1 - y0 + 1), System.Drawing.Imaging.PixelFormat.Format32bppArgb);
	}

	public static string Key(EquipSlot s) => s switch
	{
		EquipSlot.Helm => "helm",
		EquipSlot.Body => "body",
		EquipSlot.Boots => "boots",
		_ => "shield",
	};
}
