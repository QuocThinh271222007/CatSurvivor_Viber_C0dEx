using System.Numerics;

namespace CatSurvivor;

/// <summary>HUD vẽ trực tiếp lên màn hình game: thanh máu, thanh XP, level, thời gian chơi, icon trang bị, dash, kĩ năng và thanh máu boss (không hiện kills/số quái/giáp/độ khó/nhắc tự động ngắm).</summary>
public static class Hud
{
	private static void Bar(Canvas c, Vector2 pos, Vector2 size, float value, float max, Rgba fill)
	{
		c.FillRect(pos.X, pos.Y, size.X, size.Y, new Rgba(0f, 0f, 0f, 0.5f));
		float ratio = max > 0f ? Mx.Clamp(value / max, 0f, 1f) : 0f;
		c.FillRect(pos.X, pos.Y, size.X * ratio, size.Y, fill);
	}

	/// <summary>
	/// Viền đỏ quanh màn hình khi gần hết máu (dưới 35%), 	hở theo nhịp tim: càng ít máu viền càng đậm, dày và nhịp càng nhanh.
	/// </summary>
	private static void LowHealthVignette(Canvas c, World w)
	{
		var p = w.Player;
		if (p.Dead || p.MaxHp <= 0f) return;
		const float Threshold = 0.35f;
		float ratio = p.Hp / p.MaxHp;
		if (ratio >= Threshold) return;

		float severity = Mx.Clamp((Threshold - ratio) / Threshold, 0f, 1f);   // 0 = vừa chạm ngưỡng, 1 = sắp chết
		float t = (float)(Environment.TickCount64 % 100000L) / 1000f;
		float speed = 2.4f + 4.2f * severity;                                   // rad/giây: máu càng thấp tim càng đập nhanh
		float pulse = MathF.Pow(0.5f + 0.5f * MathF.Sin(t * speed), 1.5f);      // 0..1, nhịp thở/đập
		float alpha = (0.20f + 0.45f * severity) * (0.40f + 0.60f * pulse);
		float thick = 44f + 24f * severity + 18f * pulse;                       // viền dày lên khi hít vào

		var red = new Rgba(0.85f, 0.04f, 0.06f, alpha);
		var clear = red.WithAlpha(0f);
		float W = Balance.ViewWidth, H = Balance.ViewHeight;
		c.FillGradientRect(new RectangleF(0f, 0f, W, thick), red, clear, 90f);           // trên
		c.FillGradientRect(new RectangleF(0f, H - thick, W, thick), clear, red, 90f);    // dưới
		c.FillGradientRect(new RectangleF(0f, 0f, thick, H), red, clear, 0f);            // trái
		c.FillGradientRect(new RectangleF(W - thick, 0f, thick, H), clear, red, 0f);     // phải
	}

	public static void Draw(Canvas c, World w)
	{
		LowHealthVignette(c, w); // vẽ trước để HUD luôn nằm trên viền đỏ
		WarpTint(c, w.Player);
		StopTint(c, w.Player);
		var gs = w.State;
		var p = w.Player;
		var white = Rgba.White;

		bool maxLevel = gs.Level >= GameState.MaxLevel;
		float xpRatio = maxLevel || gs.XpToNext <= 0f ? 1f : gs.Xp / gs.XpToNext; // cấp tối đa: thanh XP đầy
		DrawBars(c, p, gs, maxLevel, xpRatio);

		int t = (int)gs.Time;
		PixelFont.Draw(c, $"{t / 60:00}:{t % 60:00}", new Vector2(200, 8), 2, white); // chỉ hiện thời gian (kills / quái / giáp / độ khó đã ẩn)

		// Dash: chấm đặc = còn lượt, vòng rỗng = đang hồi (kèm thời gian hồi lượt kế tiếp) - vẽ bằng hình thay cho ký tự ●/○ (font pixel không có)
		float ty = TextTop; // các dòng chữ nằm ngay dưới cụm thanh máu + mana
		float ex = DrawEquipIcons(c, p, ty); // giáp đang mặc: chỉ hiện icon ngay dưới thanh mana (không chữ, không số)
		PixelFont.Draw(c, $"Bắn x{p.FireRate:0.##}", new Vector2(ex + 4f, ty + 4f), 1, white);
		ty += 6f;                            // các dòng dưới nằm sau hàng icon (cao 16px)
		PixelFont.Draw(c, "Dash [Space]:", new Vector2(8, ty + 12), 1, white);
		float dx = 8f + PixelFont.Measure(c, "Dash [Space]: ", 1) + 3f;
		for (int i = 0; i < p.DashMaxCharges; i++)
		{
			var center = new Vector2(dx + i * 9f + 3f, ty + 15.5f);
			if (i < p.DashCharges) c.DrawCircle(center, 3f, white);
			else c.DrawArc(center, 2.5f, 0f, Mx.Tau, white, 1f);
		}
		if (p.DashRechargeLeft > 0f)
			PixelFont.Draw(c, $"({p.DashRechargeLeft:0.0}s)", new Vector2(dx + p.DashMaxCharges * 9f + 4f, ty + 12), 1, white);

		DrawSkills(c, p);

		// Cheat (debug): nhãn + thông báo kết quả lệnh
		if (w.State.CheatsUsed)
			PixelFont.Draw(c, "DEBUG" + (w.Player.God ? " GOD" : "") + (w.TimeScale != 1f ? $" x{w.TimeScale:0.##}" : ""), new Vector2(520, 38), 1, new Rgba(1f, 0.4f, 0.4f));
		if (w.Toast != null)
			PixelFont.Draw(c, w.Toast, new Vector2(8, 270), 1, new Rgba(1f, 0.95f, 0.4f));

		// Boss: mỗi mắt chính một thanh máu (giai đoạn 3 có hai con -> hai thanh cạnh nhau); mỗi GIAI ĐOẠN là một thanh đầy lại
		DrawBossBars(c, w);
	}

	// ------------------------------------------------------------------ thanh máu boss
/// <summary>
/// Thanh máu boss ở đáy màn hình (asset ui/boss_bar.png, vẽ nguyên cỡ): MỖI MẮT CHÍNH MỘT THANH (giai đoạn 3 có hai con -> hai thanh cạnh nhau);
/// mỗi GIAI ĐOẠN (1/2/3) là một thanh đầy lại. Đang bất khả xâm phạm (còn mắt con) thì thanh đổi sang xanh dương. Chưa có asset thì vẽ thanh phẳng.
/// </summary>
private static void DrawBossBars(Canvas c, World w)
{
	var bosses = new List<Enemy>();
	foreach (var e in w.Enemies.Enemies) if (e.Kind == EnemyTypes.Boss && e.Boss != null && !e.Dead) bosses.Add(e);
	if (bosses.Count == 0) return;
	bosses.Sort((a, b) => a.Boss.Index.CompareTo(b.Boss.Index));

	bool art = HudBars.BossBarAvailable;
	Vector2 size = art ? HudBars.BossBarSize : new Vector2(230f, 14f);
	float overhang = art ? HudBars.BossHeadOverhang : 0f;
	const float gap = 24f;
	float total = bosses.Count * size.X + (bosses.Count - 1) * gap;
	float x0 = (Balance.ViewWidth - total) / 2f;
	float y = Balance.ViewHeight - 32f - size.Y;      // phía trên thanh XP ở đáy màn hình
	var white = Rgba.White;
	var blue = new Rgba(0.6f, 0.85f, 1f);

	for (int i = 0; i < bosses.Count; i++)
	{
		var e = bosses[i];
		var bd = e.Boss;
		float x = x0 + i * (size.X + gap);
		float cx = x + size.X / 2f;
		bool shielded = bd.ShieldUp;
		float ratio = e.MaxHp > 0f ? e.Hp / e.MaxHp : 0f;
		if (art) HudBars.DrawBossBar(c, new Vector2(x, y), ratio, shielded);
		else Bar(c, new Vector2(x, y), size, e.Hp, e.MaxHp, shielded ? new Rgba(0.45f, 0.65f, 0.95f) : new Rgba(0.85f, 0.15f, 0.15f));

		// Số máu của thanh hiện tại, giữa thanh (có bóng đổ cho dễ đọc trên sọc đỏ)
		string hp = $"{(int)MathF.Ceiling(e.Hp)} / {(int)MathF.Ceiling(e.MaxHp)}";
		float ty = y + size.Y / 2f - 4f;
		PixelFont.DrawCentered(c, hp, cx + 1f, ty + 1f, 1, new Rgba(0f, 0f, 0f, 0.85f));
		PixelFont.DrawCentered(c, hp, cx, ty, 1, white);

		// Tên + giai đoạn phía trên đám mắt
		float top = y - overhang - 20f;
		string name = bosses.Count > 1 ? $"{BossArena.BossName} {(char)('A' + bd.Index)}" : BossArena.BossName;
		PixelFont.DrawCentered(c, name, cx, top, 1, white);
		string phase = $"GIAI ĐOẠN {bd.Stage}/3" + (bd.Stage >= 3 ? " - NỔI ĐIÊN" : "");
		if (shielded && bd.MinisSpawned)
			phase = $"BẤT KHẢ XÂM PHẠM: {w.Enemies.MiniCount(e)}/{MiniEyes.Count} mắt con";
		PixelFont.DrawCentered(c, phase, cx, top + 10f, 1, shielded ? blue : new Rgba(1f, 0.8f, 0.6f));
	}
}

// ------------------------------------------------------------------ kĩ năng chủ động
	/// <summary>Lớp phủ xanh dương + viền mờ khi đang "làm chậm thời gian" (kĩ năng E), vào/ra mượt.</summary>
	private static void WarpTint(Canvas c, Player p)
	{
		float ov = p.Skills.WarpOverlay;
		if (ov <= 0f) return;
		float W = Balance.ViewWidth, H = Balance.ViewHeight, thick = 40f;
		c.FillRect(0f, 0f, W, H, new Rgba(0.35f, 0.5f, 1f, 0.09f * ov));
		var edge = new Rgba(0.4f, 0.6f, 1f, 0.30f * ov);
		var clear = edge.WithAlpha(0f);
		c.FillGradientRect(new RectangleF(0f, 0f, W, thick), edge, clear, 90f);
		c.FillGradientRect(new RectangleF(0f, H - thick, W, thick), clear, edge, 90f);
		c.FillGradientRect(new RectangleF(0f, 0f, thick, H), edge, clear, 0f);
		c.FillGradientRect(new RectangleF(W - thick, 0f, thick, H), clear, edge, 0f);
	}

	/// <summary>Lớp phủ trắng xám nhạt khi "ngừng thời gian" (vào/ra mượt).</summary>
	private static void StopTint(Canvas c, Player p)
	{
		float ov = p.Skills.StopOverlay;
		if (ov <= 0f) return;
		float W = Balance.ViewWidth, H = Balance.ViewHeight;
		c.FillRect(0f, 0f, W, H, new Rgba(0.85f, 0.85f, 0.95f, 0.14f * ov));
		c.DrawRect(new Rect2(1f, 1f, W - 2f, H - 2f), new Rgba(1f, 1f, 1f, 0.5f * ov), false, 2f);
	}

	// Icon kĩ năng (bạn tự vẽ): Assets/ui/skills.png = dải ngang các khung VUÔNG (cạnh = chiều cao ảnh): khung 0 = Sóng xung kích, 1 = Làm chậm thời gian, 2 = Ngừng thời gian, 3 = Bất tử.
	// Xuất từ skills.aseprite bằng tools\export_assets.ps1. Chưa có file thì HUD tự vẽ hình tạm; ô kĩ năng tự to ra theo kích thước icon (tối thiểu 30px).
	private const string SkillIconsPath = "ui/skills.png";
	private static Bitmap _skillIcons;
	private static bool _skillIconsLoaded;

	// Thứ tự khung trong skills.png do bạn vẽ: 0 đồng hồ bấm giờ, 1 hình vàng, 2 bông tuyết xanh, 3 sóng trắng -> gán theo ý nghĩa:
	// Sóng xung kích = sóng trắng (3), Làm chậm = đồng hồ (0), Ngừng thời gian = bông tuyết (2), Bất tử = hình vàng (1). Đổi mảng này nếu muốn gán khác.
	private static readonly int[] SkillIconFrame = { 3, 0, 2, 1 };
	private static Rectangle[] _iconRects;      // vùng có hình (cắt sát viền) của từng khung, toạ độ trong ảnh
	private static int _iconBoxDim;             // cạnh lớn nhất của các vùng đó

	private static Bitmap SkillIcons(out int frame)
	{
		if (!_skillIconsLoaded)
		{
			_skillIconsLoaded = true;
			try { _skillIcons = AssetStore.OptionalImage(SkillIconsPath); }
			catch (Exception) { _skillIcons = null; }
			if (_skillIcons != null && (_skillIcons.Height <= 0 || _skillIcons.Width < _skillIcons.Height)) { AssetStore.Report("Assets/" + SkillIconsPath, "cần dải ngang các khung vuông (ít nhất 1 khung)"); _skillIcons = null; }
			if (_skillIcons != null) { try { MeasureSkillIcons(); } catch (Exception) { _iconRects = null; _iconBoxDim = 0; } }
		}
		frame = _skillIcons?.Height ?? 0;
		return _skillIcons;
	}

	/// <summary>Tìm vùng có hình của từng khung icon (khung 64x64 thường chỉ vẽ ~30px ở giữa) để HUD vẽ sát hình thay vì chừa cả khung.</summary>
	private static void MeasureSkillIcons()
	{
		int fs = _skillIcons.Height, n = _skillIcons.Width / fs;
		_iconRects = new Rectangle[n];
		_iconBoxDim = 0;
		for (int i = 0; i < n; i++)
		{
			int x0 = fs, y0 = fs, x1 = -1, y1 = -1;
			for (int y = 0; y < fs; y++)
				for (int x = 0; x < fs; x++)
					if (_skillIcons.GetPixel(i * fs + x, y).A > 8) { x0 = Math.Min(x0, x); y0 = Math.Min(y0, y); x1 = Math.Max(x1, x); y1 = Math.Max(y1, y); }
			_iconRects[i] = x1 < 0 ? new Rectangle(i * fs, 0, fs, fs) : new Rectangle(i * fs + x0, y0, x1 - x0 + 1, y1 - y0 + 1);
			_iconBoxDim = Math.Max(_iconBoxDim, Math.Max(_iconRects[i].Width, _iconRects[i].Height));
		}
	}

	/// <summary>Hàng icon giáp đang mặc, ngay dưới thanh mana: mỗi ô 16x16 một món (không chữ/số). Trả về toạ độ x ngay sau hàng icon.</summary>
	private static float DrawEquipIcons(Canvas c, Player p, float y)
	{
		const float Box = 16f, Gap = 2f;
		float x = 8f;
		foreach (var slot in new[] { EquipSlot.Helm, EquipSlot.Body, EquipSlot.Boots, EquipSlot.Shield })
		{
			if (p.Equipped.TryGetValue(slot, out var eq) && EquipmentAssets.Small(slot, eq.Level) is Bitmap icon)
			{
				// Bản thu nhỏ 1/2 (đã lọc sắc nét), vẽ 1:1 tại toạ độ nguyên
				c.DrawTexture(icon, new Rect2(MathF.Round(x + (Box - icon.Width) / 2f), MathF.Round(y + (Box - icon.Height) / 2f), icon.Width, icon.Height));
			}
			x += Box + Gap;
		}
		return x;
	}

	/// <summary>Hai ô kĩ năng ở góc dưới trái (trên thanh XP): phím, giá mana, hồi chiêu; sáng khi dùng được, tối khi thiếu mana / đang hồi.</summary>
	private static void DrawSkills(Canvas c, Player p)
	{
		var sk = p.Skills;
		SkillIcons(out _);
		float size = MathF.Max(30f, _iconBoxDim + 8f);
		float y = Balance.ViewHeight - (HudBars.Available ? HudBars.BarHeight : 8f) - 3f - 5f - size;
		// Kĩ năng chọn bằng lá bài trước ván: Dễ / Trung bình có HAI (Q và E), Khó / Siêu khó MỘT (Q/E); Chosen == null (kiểm thử/cheat) thì hiện Nova (Q) + Warp (E)
		float x = 8f;
		var shown = sk.Chosen is SkillKind one
			? (sk.Chosen2 is SkillKind two ? new[] { one, two } : new[] { one })
			: new[] { SkillKind.Nova, SkillKind.Warp };
		for (int i = 0; i < shown.Length; i++)
		{
			var k = shown[i];
			string key = shown.Length == 1 ? "Q/E" : i == 0 ? "Q" : "E";
			switch (k)
			{
				case SkillKind.Nova: SkillBox(c, x, y, size, key, SkillSet.NovaCost, sk.NovaCd, SkillSet.NovaCooldown, p.Mana >= SkillSet.NovaCost, false, 0f, 0); break;
				case SkillKind.Warp: SkillBox(c, x, y, size, key, SkillSet.WarpCost, sk.WarpCd, SkillSet.WarpCooldown, p.Mana >= SkillSet.WarpCost, sk.WarpActive, sk.WarpLeft, 1); break;
				case SkillKind.Stop: SkillBox(c, x, y, size, key, SkillSet.StopCost, sk.StopCd, SkillSet.StopCooldown, p.Mana >= SkillSet.StopCost, sk.StopActive, sk.StopLeft, 2); break;
				default: SkillBox(c, x, y, size, key, SkillSet.GuardCost, sk.GuardCd, SkillSet.GuardCooldown, p.Mana >= SkillSet.GuardCost, sk.GuardActive, sk.GuardLeft, 3); break;
			}
			x += size + 4f;
		}
	}

	private static void SkillBox(Canvas c, float x, float y, float S, string key, float cost, float cd, float cdMax, bool enough, bool active, float activeLeft, int icon)
	{
		bool ready = cd <= 0f && enough;
		var border = active ? (icon == 3 ? new Rgba(1f, 0.9f, 0.45f) : new Rgba(0.6f, 0.8f, 1f)) : ready ? new Rgba(0.95f, 0.82f, 0.4f) : new Rgba(0.4f, 0.42f, 0.5f);
		var ink = ready || active ? new Rgba(1f, 1f, 1f) : new Rgba(0.55f, 0.57f, 0.65f);
		c.FillRect(x, y, S, S, new Rgba(0f, 0f, 0f, 0.55f));

		var tex = SkillIcons(out int fs);
		int fr = SkillIconFrame[icon];
		if (tex != null && _iconRects != null && fr < _iconRects.Length)
		{
			// Icon vẽ tay: đúng kích thước gốc (cắt sát hình), căn giữa ô; tối đi khi chưa dùng được
			var r = _iconRects[fr];
			c.DrawTexture(tex, new Rect2(MathF.Round(x + (S - r.Width) / 2f), MathF.Round(y + (S - r.Height) / 2f), r.Width, r.Height), new Rect2(r.X, r.Y, r.Width, r.Height), ready || active ? Rgba.White : new Rgba(0.5f, 0.52f, 0.6f));
		}
		else
		{
			// Chưa có icon: hình tạm
			Vector2 mid = new(x + S / 2f, y + S / 2f - 1f);
			if (icon == 0)
			{
				c.DrawArc(mid, 9f, 0f, Mx.Tau, ink, 1.5f);                         // sóng lan toả
				c.DrawArc(mid, 4.5f, 0f, Mx.Tau, ink.WithAlpha(0.7f), 1f);
			}
			else if (icon == 1)
			{
				c.DrawArc(mid, 8.5f, 0f, Mx.Tau, ink, 1.5f);                       // đồng hồ
				c.DrawLine(mid, mid + new Vector2(0f, -6f), ink, 1.5f);
				c.DrawLine(mid, mid + new Vector2(4f, 2f), ink, 1.5f);
			}
			else if (icon == 2)
			{
				c.DrawArc(mid, 9f, 0f, Mx.Tau, ink, 1.5f);                         // nút tạm dừng: hai vạch đứng
				c.FillRect(mid.X - 4f, mid.Y - 5f, 3f, 10f, ink);
				c.FillRect(mid.X + 1f, mid.Y - 5f, 3f, 10f, ink);
			}
			else
			{
				c.DrawArc(mid, 9f, 0f, Mx.Tau, ink, 1.5f);                         // khiên bất tử: vòng + dấu cộng
				c.DrawLine(mid + new Vector2(0f, -5f), mid + new Vector2(0f, 5f), ink, 1.5f);
				c.DrawLine(mid + new Vector2(-5f, 0f), mid + new Vector2(5f, 0f), ink, 1.5f);
			}
		}

		if (cd > 0f && cdMax > 0f)
			c.FillRect(x, y, S, S * Mx.Clamp(cd / cdMax, 0f, 1f), new Rgba(0f, 0f, 0f, 0.6f));   // hồi chiêu: tối dần từ trên xuống
		c.DrawRect(new Rect2(x, y, S, S), border, false, 1f);

		PixelFont.Draw(c, key, new Vector2(x + 3f, y + 2f), 1, ink);
		PixelFont.Draw(c, ((int)cost).ToString(), new Vector2(x + 3f, y + S - 9f), 1, enough ? new Rgba(0.55f, 0.75f, 1f) : new Rgba(1f, 0.45f, 0.4f));
		if (active)
			PixelFont.Draw(c, activeLeft.ToString("0.0"), new Vector2(x + S - 17f, y + 2f), 1, new Rgba(0.7f, 0.85f, 1f));
	}
	// ------------------------------------------------------------------ thanh máu / mana / XP
	private const float BarsLeft = 6f;
	private static float TextTop => HudBars.Available ? BarsTop + BarsBlockHeight + 6f : 36f;
	private static float BarsTop => 2f + HudBars.HeadOverhang;                  // trên cùng của THANH máu (đầu mèo nhô lên phía trên)
	private static float BarsBlockHeight => HudBars.HealthSize.Y + 3f + HudBars.BarHeight; // thanh máu + khe + thanh mana

	private static void Shadowed(Canvas c, string text, Vector2 pos, Rgba color)
	{
		PixelFont.Draw(c, text, pos + new Vector2(1f, 1f), 1, new Rgba(0f, 0f, 0f, 0.85f));
		PixelFont.Draw(c, text, pos, 1, color);
	}

	private static void DrawBars(Canvas c, Player p, GameState gs, bool maxLevel, float xpRatio)
	{
		if (!HudBars.Available)
		{
			// Chưa có asset thanh: thanh phẳng đơn giản
			Bar(c, new Vector2(8, 8), new Vector2(160, 10), p.Hp, p.MaxHp, new Rgba(0.9f, 0.2f, 0.25f));
			Bar(c, new Vector2(8, 20), new Vector2(160, 6), p.Mana, p.MaxMana, new Rgba(0.35f, 0.45f, 0.95f));
			Bar(c, new Vector2(8, 28), new Vector2(624, 4), xpRatio, 1f, new Rgba(0.3f, 0.7f, 1f));
			PixelFont.Draw(c, maxLevel ? $"LV {gs.Level} MAX" : $"LV {gs.Level}", new Vector2(8, 340), 2, Rgba.White);
			return;
		}

		var white = Rgba.White;
		float hw = HudBars.HealthSize.X, bh = HudBars.BarHeight;
		float textDy = MathF.Floor((bh - 7f) / 2f);   // chữ 1x cao ~7px nằm giữa thanh

		// Máu: thanh đỏ có đầu mèo; con số nằm ở bên còn chỗ trống (đầu + chân mèo che đoạn giữa thanh)
		var hp = new Vector2(BarsLeft, BarsTop);
		HudBars.DrawHealth(c, hp, p.MaxHp > 0f ? p.Hp / p.MaxHp : 0f);
		string hpText = $"{(int)MathF.Ceiling(p.Hp)}/{(int)MathF.Ceiling(p.MaxHp)}";
		float tw = PixelFont.Measure(c, hpText, 1);
		var head = HudBars.HeadSpan;
		float leftRoom = head.X - 12f, rightRoom = hw - head.Y - 12f;
		float tx = tw <= leftRoom || leftRoom >= rightRoom ? 8f : hw - 8f - tw;
		Shadowed(c, hpText, new Vector2(hp.X + tx, hp.Y + textDy), white);

		// Mana: thanh xanh dương ngay dưới thanh máu, cùng chiều dài
		var mp = new Vector2(BarsLeft, hp.Y + HudBars.HealthSize.Y + 3f);
		HudBars.DrawMana(c, mp, hw, p.MaxMana > 0f ? p.Mana / p.MaxMana : 0f);
		Shadowed(c, $"MP {(int)p.Mana}/{(int)p.MaxMana}", new Vector2(mp.X + 8f, mp.Y + textDy), white);

		// XP: thanh xanh lá chạy suốt đáy màn hình; LV ở bên trái, số XP ở bên phải
		float ex = 8f, ew = Balance.ViewWidth - 16f, ey = Balance.ViewHeight - bh - 3f;
		HudBars.DrawExp(c, new Vector2(ex, ey), ew, xpRatio);
		Shadowed(c, maxLevel ? $"LV {gs.Level} MAX" : $"LV {gs.Level}", new Vector2(ex + 10f, ey + textDy), white);
		string xpText = maxLevel ? "MAX" : $"{(int)gs.Xp}/{(int)gs.XpToNext}";
		Shadowed(c, xpText, new Vector2(ex + ew - 10f - PixelFont.Measure(c, xpText, 1), ey + textDy), white);
	}
}
