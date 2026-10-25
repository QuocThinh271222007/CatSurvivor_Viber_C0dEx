namespace CatSurvivor;

/// <summary>
/// Màn chọn kĩ năng chủ động trước mỗi ván: độ Dễ / Trung bình mang HAI trong 4 kĩ năng (Q = lá chọn đầu, E = lá thứ hai), độ Khó / Siêu khó chỉ MỘT; hiện thành hàng lá bài poker (SkillPickForm).
/// Hình lá bài bạn tự vẽ: Assets/ui/poker.png (xuất từ poker.aseprite) dùng chung cho mọi kĩ năng; muốn mỗi kĩ năng một hình riêng thì thêm
/// Assets/ui/skill_card_{nova|warp|stop|guard}.png (từ skill_card_{...}.aseprite) - lá nào có file riêng thì dùng file đó. Ảnh được cắt sát viền tự động.
/// Không có hình nào thì dùng lá bài vẽ tạm.
/// </summary>
public static class SkillPick
{
	private static readonly Dictionary<string, Bitmap> ArtCache = new();

	private static Bitmap Art(string name)
	{
		if (ArtCache.TryGetValue(name, out var cached)) return cached;
		Bitmap result = null;
		try
		{
			var src = AssetStore.OptionalImage($"ui/{name}.png");
			if (src != null) result = EquipmentAssets.CropToContent(src);
		}
		catch (Exception ex) { AssetStore.Report($"ui/{name}.png", ex.Message); }
		ArtCache[name] = result;
		return result;
	}

	/// <summary>Lá bài tạm (khi chưa có hình nào): khung vàng nền tối 48x68.</summary>
	private static Bitmap Placeholder()
	{
		var bmp = new Bitmap(48, 68, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
		using var g = Graphics.FromImage(bmp);
		g.Clear(Color.Black);
		using (var b = new SolidBrush(Color.FromArgb(40, 44, 70))) g.FillRectangle(b, 2, 2, 44, 64);
		using (var p = new Pen(Color.FromArgb(240, 166, 51), 2)) g.DrawRectangle(p, 4, 4, 39, 59);
		return bmp;
	}

	public static List<SkillCardInfo> Cards()
	{
		Bitmap fallback = Art("poker") ?? Placeholder();
		SkillCardInfo Card(SkillKind k, string key, string desc) => new()
		{
			Kind = k,
			Name = SkillSet.NameOf(k),
			Desc = desc,
			Art = Art($"skill_card_{key}") ?? fallback,
		};
		return new List<SkillCardInfo>
		{
			Card(SkillKind.Nova, "nova", $"Xả một đợt sóng quanh mèo: gây sát thương và hất văng mọi quái trong tầm. Tốn {SkillSet.NovaCost:0} mana, hồi chiêu {SkillSet.NovaCooldown:0.#}s."),
			Card(SkillKind.Warp, "warp", $"Làm chậm quái (boss chậm nhẹ hơn) trong {SkillSet.WarpDuration:0}s, mèo vẫn chạy bình thường. Tốn {SkillSet.WarpCost:0} mana, hồi chiêu {SkillSet.WarpCooldown:0}s."),
			Card(SkillKind.Stop, "stop", $"Ngừng hẳn mọi quái và boss trong {SkillSet.StopDuration:0.#}s (đòn của chúng cũng đứng yên), mèo vẫn đi và đánh được. Tốn {SkillSet.StopCost:0} mana, hồi chiêu {SkillSet.StopCooldown:0}s."),
			Card(SkillKind.Guard, "guard", $"Bất tử {SkillSet.GuardDuration:0.#}s: mọi sát thương lên mèo bị bỏ qua. Tốn {SkillSet.GuardCost:0} mana, hồi chiêu {SkillSet.GuardCooldown:0}s."),
		};
	}

	/// <summary>Số kĩ năng được mang theo độ khó hiện tại: Dễ (0) và Trung bình (1) được HAI, Khó và Siêu khó chỉ MỘT.</summary>
	public static int PicksAllowed => Settings.Difficulty <= 1 ? 2 : 1;

	/// <summary>Hiện hộp thoại chọn; trả về kĩ năng đầu (null nếu người chơi đóng hộp thoại). `second` = kĩ năng thứ hai (null nếu độ khó chỉ cho một).</summary>
	public static SkillKind? Ask(out SkillKind? second, IWin32Window owner = null)
	{
		using var form = new SkillPickForm(Cards(), PicksAllowed);
		if (owner != null) form.ShowDialog(owner); else form.ShowDialog();
		second = form.Chosen == null ? null : form.Chosen2;
		return form.Chosen;
	}
}