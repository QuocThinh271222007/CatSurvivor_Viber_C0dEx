using System.Drawing.Imaging;
using System.Numerics;
using System.Runtime.InteropServices;

namespace CatSurvivor;

/// <summary>
/// Trái tim của game: quản lý hàng trăm quái trong MỘT đối tượng.
///  - Không tạo đối tượng/vật lý cho từng quái -> rất nhẹ
///  - Spatial hash (lưới ô) để: tách nhau ra (separation), truy vấn vùng cho đạn/vũ khí
///  - Spawner: sinh quái ở vòng ngoài màn hình, tăng độ khó theo thời gian, có đợt "swarm"
///  - Quái ở quá xa được "tái chế" sang phía đối diện thay vì xoá -> mật độ luôn dày
///  - Trúng đòn: khựng lại một nhịp (hit-stop) + nháy trắng, rồi mới bị văng góc 45° (có độ cao Z giả),
///    nảy tâng tâng rồi trở lại bình thường; quái chết cũng bị văng và nảy trước khi biến mất
///  - Bomber: tới gần thì châm ngòi, đếm ngược rồi nổ gây sát thương vùng
/// </summary>
public sealed class EnemyManager
{
	private readonly World _w;

	// --- Mật độ sinh quái (chỉnh ở đây để dễ/khó hơn) ---
	public const int MaxEnemies = 120;               // số quái tối đa cùng lúc ở cấp 1; tăng thêm 2 con mỗi cấp nhân vật
	private const int MaxEnemiesCap = 170;           // trần tuyệt đối (giữ game chạy mượt)

	// --- Độ khó theo CẤP nhân vật: mỗi cấp coi như quái đã chơi lâu thêm chừng này giây.
	// Dùng chung cho mọi thứ vốn tăng theo thời gian: máu/sát thương/giáp/tốc độ quái, loại quái & bậc màu mở khoá, kích cỡ đội hình...
	// Càng lên cấp nhanh quái càng mạnh + đông (cấp 14: như chơi thêm ~4,3 phút; cấp 27: ~8,7 phút).
	private const float LevelSecondsBonus = 20f;
	private const float LevelSpawnFaster = 0.008f;   // mỗi cấp rút ngắn khoảng cách giữa các lần sinh thêm 1,2%
	private const float SpawnIntervalStart = 2.6f;   // giây giữa các lần sinh lúc mới vào game
	private const float SpawnIntervalMin = 1.2f;
	private const float SpawnIntervalDecay = 0.004f; // giảm bao nhiêu giây mỗi giây chơi (chạm mức nhanh nhất ở ~4,5 phút)
	private const float SpawnGrowthSeconds = 190f;   // cứ chừng này giây thì mỗi lần sinh thêm 1 con
	private const int SwarmBase = 22;

	// --- Đội hình đặc biệt (xuất hiện định kỳ, mở khoá dần theo thời gian) ---
	private const float FormationFirst = 40f;         // đội hình đầu tiên xuất hiện sau chừng này giây
	private const float FormationIntervalStart = 40f; // khoảng cách giữa các đội hình, rút ngắn dần
	private const float FormationIntervalMin = 28f;
	private const int FormationHeadroom = 40;          // đội hình được phép đẩy tổng số quái vượt MaxEnemies thêm chừng này
	private const float FormationLeash = 140f;        // thành viên bị văng/tụt xa ô của mình hơn mức này thì rời đội hình

	// --- Brute: gồng (báo hiệu) rồi lao thẳng một đường, sau đó kiệt sức ---
	private const float BruteChargeRange = 170f;      // người chơi trong tầm này (và hết hồi) thì bắt đầu gồng
	private const float BruteWindup = 0.7f;           // gồng; nửa đầu còn ngắm theo người chơi, nửa sau khoá hướng -> né được
	private const float BruteChargeTime = 0.75f;
	private const float BruteChargeSpeed = 250f;
	private const float BruteTired = 1.0f;            // kiệt sức đứng yên sau khi lao
	private const float BruteCooldown = 3.5f;
	private const float BruteChargeDamageMul = 1.5f;
	private const float BruteTelegraphLen = 190f;
	private const float BruteBrokenSpeedMul = 1f;         // khiên vỡ: vẫn đi chậm như cũ (nhân hệ số này), chỉ không lao nữa
	private const float ShieldBreakTime = 0.45f;

	// --- Slime đặc biệt: đòn vồ tới - gồng rồi lao thẳng một cú về phía người chơi, sau đó khựng lại ---
	private const float PounceMinRange = 60f;           // người chơi phải ở trong khoảng [Min, Max] mới bị vồ (gần quá thì cứ cắn)
	private const float PounceMaxRange = 190f;
	private const float PounceChancePerSecond = 0.15f;  // mỗi giây (khi hết hồi và trong tầm) có 15% cơ hội ra đòn
	private const float PounceCooldown = 8f;
	private const float PounceWindup = 0.65f;           // gồng báo hiệu; nửa đầu còn ngắm theo người chơi, nửa sau khoá hướng
	private const float PounceTrackFrac = 0.5f;
	private const float PounceTime = 0.45f;             // thời gian bay
	private const float PounceDistance = 170f;          // quãng bay tối đa (px)
	private static float PounceAimSpeed(float dist) => Mx.Clamp(dist + 40f, 90f, PounceDistance) / PounceTime; // tốc độ bay dự kiến của cú vồ (để dự đoán chỗ mèo sẽ đứng)
	private const float PounceRecover = 0.8f;           // khựng sau khi vồ: cơ hội để phản công
	private const float PounceDamageMul = 2.2f;
	private const float PounceImpactMul = 1.4f;
	private const float PounceHeight = 20f;             // độ cao vòng cung khi bay (nhân Size, chỉ để vẽ)

	// --- Bat: khi chưa sẵn sàng gào thì lượn vòng quanh người chơi ở bán kính này ---
	private const float BatOrbitRadius = 95f;

	private const float CellSize = 40f;
	private const float MaxEnemyRadius = 26f;     // bán kính lớn nhất có thể có (quái khổng lồ) - dùng khi truy vấn lưới
	private const float RecycleDistance = 900f;

	// --- Hit-stop / nháy trắng ---
	private const float HitStopTime = 0.10f;      // khựng lại khi bị đánh thường
	private const float KillStopTime = 0.14f;     // khựng lâu hơn một chút ở nhát kết liễu
	private const float HitFlashTime = 0.22f;     // nháy trắng, kéo dài hơn thời gian khựng
	private const float CorpseLinger = 0.30f;     // xác nằm lại sau khi đáp xuống

	// --- Văng / nảy ---
	private const float Gravity = 900f;
	private const float LaunchThreshold = 170f; // lực đẩy (sau khi chia cho Weight) tối thiểu để bị văng lên không
	private const float LaunchSpeedScale = 0.6f; // hệ số vận tốc văng: nhỏ hơn -> văng gần hơn (quãng bay ~ v²)
	private const float MaxLaunch = 190f;       // trần vận tốc văng
	private const float DeathLaunch = 170f;     // quái chết nhẹ vẫn nảy lên một chút (chia thêm cho Weight)
	private const float BlastKnock = 280f;      // lực vụ nổ Bomber đẩy các quái xung quanh (chia cho Weight)

	// --- Bat: gào tạo vùng phễu 2 bên (trái & phải) ---
	private const float ScreechRange = 64f;
	private const float ScreechStandOff = 0.6f; // bat bay tới điểm cách người chơi ~60% tầm gào, cùng hàng ngang
	private const float ScreechFireRange = 0.85f; // chỉ gào khi người chơi nằm trong 85% tầm
	private const float ScreechFireAngle = 0.6f;  // ... và nằm gọn trong lòng phễu (<= 60% góc mở)
	private const float ScreechHalfAngle = 0.66f; // ~38° so với trục ngang
	private const float ScreechWindup = 0.30f;  // báo hiệu (người chơi kịp né)
	private const float ScreechWave = 0.35f;    // sóng âm lan ra; sát thương tính ở đầu pha này
	private const float ScreechRecover = 0.20f;
	private const float ScreechTotal = ScreechWindup + ScreechWave + ScreechRecover;
	private const float ScreechCooldown = 2.6f;
	private const float ScreechDamageMul = 1.3f;
	private const float ScreechImpact = 130f;
	private const float Restitution = 0.55f;    // giữ lại bao nhiêu vận tốc đứng sau mỗi lần nảy
	private const float BounceFriction = 0.7f;  // vận tốc ngang giữ lại sau mỗi lần nảy
	private const float MinBounce = 45f;        // vận tốc đứng nhỏ hơn mức này -> hết nảy, đáp hẳn xuống

	// --- Bomber ---
	private const float BombTrigger = 34f;      // tới gần người chơi cỡ này thì châm ngòi
	private const float BombFuse = 1.1f;        // thời gian đếm ngược
	private const float BombRadius = 44f;       // bán kính vụ nổ
	private const float BombAnim = 0.40f;       // thời gian chạy hiệu ứng nổ
	private const float BombSprintRange = 260f;   // tới gần cỡ này thì nước rút
	private const float BombChaseRatio = 1.35f;    // nước rút nhanh hơn tốc độ chạy hiện tại của mèo 1.35 lần -> luôn bắt kịp (kể cả khi mèo nâng tốc độ), trừ khi mèo dash
	private const float BombMaxSpeed = 300f;       // trần tốc độ nước rút
	private const float BombFuseSpeed = 0.35f;  // lúc đã châm ngòi vẫn bò chậm theo người chơi

	// --- Sprite: dải ngang các frame 64x64 xuất từ Aseprite ---
	private const int FrameSize = 64;

	private class SpriteSet
	{
		public Bitmap[] Tex;        // một ảnh cho mỗi bậc biến thể màu (0 = màu gốc)
		public Rectangle[] Trim;    // với mỗi khung: hình chữ nhật bao phần CÓ HÌNH (bỏ viền trong suốt) - chỉ vẽ phần này cho đỡ tốn
		public Vector2 Center;      // tâm thân quái trong frame (để căn vào vị trí quái)
		public bool FacesLeft;      // frame gốc quay mặt sang trái
		public float ShadowDown;    // bóng nằm thấp hơn tâm quái bao nhiêu px
	}

	private readonly SpriteSet _slime, _bat, _bomber, _brute;

	public readonly List<Enemy> Enemies = new();

	/// <summary>Quái được sinh ra TRONG lúc đang duyệt Enemies (vd. boss triệu hồi): xếp hàng đợi, thêm vào sau khi duyệt xong.</summary>
	public readonly List<Enemy> PendingSpawns = new();

	/// <summary>Chưởng (viên plasma) của các mắt con đang bay (xem MiniEyes.cs).</summary>
	public readonly List<EyeBolt> Bolts = new();

	/// <summary>Số mắt con còn sống (kể cả đang chờ thêm vào danh sách).</summary>
	public int MiniCount()
	{
		int n = 0;
		foreach (var e in Enemies) if (e.Kind == EnemyTypes.MiniEye && !e.Dead) n++;
		foreach (var e in PendingSpawns) if (e.Kind == EnemyTypes.MiniEye && !e.Dead) n++;
		return n;
	}
	/// <summary>Mắt con còn sống của một mắt chính (kể cả đang chờ thêm vào danh sách).</summary>
	public IEnumerable<Enemy> MinisOf(Enemy owner)
	{
		foreach (var e in Enemies) if (e.Kind == EnemyTypes.MiniEye && !e.Dead && e.Mini?.Owner == owner) yield return e;
		foreach (var e in PendingSpawns) if (e.Kind == EnemyTypes.MiniEye && !e.Dead && e.Mini?.Owner == owner) yield return e;
	}

	public int MiniCount(Enemy owner)
	{
		int n = 0;
		foreach (var _ in MinisOf(owner)) n++;
		return n;
	}

	/// <summary>Còn mắt chính nào chưa chết không (giai đoạn 3 có hai con).</summary>
	public bool AnyBossAlive()
	{
		foreach (var e in Enemies) if (e.Kind == EnemyTypes.Boss && !e.Dead) return true;
		foreach (var e in PendingSpawns) if (e.Kind == EnemyTypes.Boss && !e.Dead) return true;
		return false;
	}

	/// <summary>Mắt chính chết: mắt con của nó chết theo (không rơi XP).</summary>
	private void KillOwnedMinis(Enemy owner)
	{
		foreach (var e in Enemies)
		{
			if (e.Kind != EnemyTypes.MiniEye || e.Dead || e.Mini?.Owner != owner) continue;
			e.Dead = true; e.NoDrop = true; e.Linger = 0.3f;
		}
		Bolts.Clear();
	}

	/// <summary>Thông báo nổi ở góc màn hình (BossAI dùng báo giai đoạn).</summary>
	internal void Toast(string text, float seconds) => _w.ShowToast(text, seconds);

	private readonly Dictionary<long, List<Enemy>> _grid = new();
	private readonly Stack<List<Enemy>> _listPool = new();
	private readonly float[] _nearestDist = new float[32];
	private float _spawnTimer = 0.5f;
	private float _formationTimer = FormationFirst;
	private float _formationTime;                  // thời gian chơi lúc tạo đội hình (để chọn bậc màu)
	private FormationType _lastFormation = (FormationType)(-1);
	private readonly List<Formation> _formations = new();

	private enum FormationType { Ring, Wall, VFlock, Pincer, Squad, Escort }

	// Hai lớp vẽ quanh lớp của mèo: "sau lưng" (vẽ trước mèo) và "trước mặt" (vẽ sau mèo)
	private readonly List<Enemy> _backList = new();
	private readonly List<Enemy> _frontList = new();
	private readonly Comparison<Enemy> _byDepth;

	public EnemyManager(World w)
	{
		_w = w;
		_byDepth = ByDepth;

		// slime: frame 0-6 nhún đi · 8 bay lên · 9 rơi · 5 bẹp · 15 trắng · 17-20 chết
		_slime = Load("enemies/slime.png", new Vector2(32, 40), faceLeft: true, shadowDown: 7f);
		// bat: 0-7 vỗ cánh · 18 trắng · 21 bay/ngã · 22 nằm
		_bat = Load("enemies/bat.png", new Vector2(32, 31), faceLeft: false, shadowDown: 16f);
		// bomber: 0-3 đi · 4 hoảng · 5-12 nhấp nháy ngòi · 13-15 nổ · 16 trắng (bị đánh) · 17-20 chết
		_bomber = Load("enemies/bomber.png", new Vector2(30, 40), faceLeft: true, shadowDown: 6f);
		// brute (khủng long cầm khiên, nhìn sang TRÁI), 10 khung: 0 gồng/lao · 1-2 KHIÊN rời (nhấp nhô, vẽ đè lên thân) · 3,6,7,8 đi · 4 trắng (hình dáng CÓ khiên) · 5 trắng (bị đánh, không khiên) · 9 kiệt sức/chết
		// Chưa có file thì Brute vẫn là hình tròn tạm (DrawPlaceholder)
		if (File.Exists(AssetStore.PathOf("enemies/brute.png")))
			_brute = Load("enemies/brute.png", new Vector2(38, 37), faceLeft: true, shadowDown: 10f);
	}

	private static SpriteSet Load(string path, Vector2 center, bool faceLeft, float shadowDown) => new()
	{
		Tex = BuildVariantTextures(path, out var trims),
		Trim = trims,
		Center = center,
		FacesLeft = faceLeft,
		ShadowDown = shadowDown,
	};

	/// <summary>
	/// Tạo ảnh cho mọi bậc biến thể màu từ một sprite gốc:
	///  - Bỏ pixel bán trong suốt (bóng đổ "vẽ cứng" trong sprite) để tự vẽ bóng động, nhờ vậy khi quái bay lên thì bóng ở lại mặt đất.
	///  - Bậc 0 giữ nguyên màu gốc. Các bậc khác: ánh xạ ĐỘ SÁNG của từng pixel lên dải màu tối -> vừa -> sáng của bậc đó
	///    (giữ nguyên đường viền, đổ bóng và điểm sáng; pixel trắng tinh như mắt, khung nháy trắng được giữ nguyên).
	/// </summary>
	private static Bitmap[] BuildVariantTextures(string path, out Rectangle[] trims)
	{
		using var img = AssetStore.ImageStraight(path);
		int w = img.Width, h = img.Height;
		byte[] data = new byte[w * h * 4];
		var rect = new Rectangle(0, 0, w, h);
		var bd = img.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
		try { Marshal.Copy(bd.Scan0, data, 0, data.Length); }
		finally { img.UnlockBits(bd); }

		// Bộ nhớ theo thứ tự B, G, R, A
		for (int i = 3; i < data.Length; i += 4)
			if (data[i] > 0 && data[i] < 252) data[i] = 0; // bóng đổ vẽ cứng

		// Hộp bao phần có hình của từng khung (alpha > 0) để lúc vẽ bỏ qua vùng trong suốt
		int frames = w / FrameSize;
		trims = new Rectangle[frames];
		for (int f = 0; f < frames; f++)
		{
			int minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1;
			for (int y = 0; y < h; y++)
				for (int x = f * FrameSize; x < (f + 1) * FrameSize; x++)
					if (data[(y * w + x) * 4 + 3] > 0)
					{
						if (x < minX) minX = x; if (x > maxX) maxX = x;
						if (y < minY) minY = y; if (y > maxY) maxY = y;
					}
			trims[f] = maxX < 0 ? Rectangle.Empty : new Rectangle(minX - f * FrameSize, minY, maxX - minX + 1, maxY - minY + 1);
		}

		var result = new Bitmap[EnemyTypes.VariantCount];
		result[0] = FromBytes(data, w, h);
		for (int v = 1; v < EnemyTypes.VariantCount; v++)
		{
			// Ưu tiên sprite vẽ tay của bậc này (vd. enemies/slime_v2.png) nếu có
			var hand = AssetStore.OptionalImage(path.Replace(".png", $"_v{v}.png"));
			if (hand != null) { result[v] = hand; continue; }
			if (!Balance.ShowColorVariants) { result[v] = result[0]; continue; } // màu đang ẩn: dùng sprite gốc

			var pal = EnemyTypes.Palette[v];
			byte[] d = (byte[])data.Clone();
			for (int i = 0; i < d.Length; i += 4)
			{
				if (d[i + 3] == 0) continue;
				float b = d[i] / 255f, g = d[i + 1] / 255f, r = d[i + 2] / 255f;
				float luma = 0.299f * r + 0.587f * g + 0.114f * b;
				float max = MathF.Max(r, MathF.Max(g, b)), min = MathF.Min(r, MathF.Min(g, b));
				float sat = max <= 0f ? 0f : (max - min) / max;
				if (luma > 0.97f && sat < 0.08f) continue; // trắng tinh: giữ nguyên

				Rgba c = luma < 0.5f ? pal.dark.Lerp(pal.mid, luma * 2f) : pal.mid.Lerp(pal.light, (luma - 0.5f) * 2f);
				d[i] = (byte)MathF.Round(c.B * 255f);
				d[i + 1] = (byte)MathF.Round(c.G * 255f);
				d[i + 2] = (byte)MathF.Round(c.R * 255f);
			}
			result[v] = FromBytes(d, w, h);
		}
		return result;
	}

	private static Bitmap FromBytes(byte[] data, int w, int h)
	{
		var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
		var bd = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
		try { Marshal.Copy(data, 0, bd.Scan0, data.Length); }
		finally { bmp.UnlockBits(bd); }
		var fast = AssetStore.ToPArgb(bmp);
		bmp.Dispose();
		return fast;
	}

	public void Update(float dt)
	{
		var player = _w.Player;
		if (player.Dead) return;
		SpawnTick(dt, player);
		UpdateFormations(dt * player.Skills.EnemyTimeScale, player); // kĩ năng E (làm chậm thời gian): đội hình cũng chạy chậm
		RebuildGrid();
		Simulate(dt, player);
		MiniEyes.UpdateBolts(Bolts, dt * player.Skills.EnemyTimeScale, player, _w.Arena); // ngừng thời gian: chưởng đứng yên
		Reap();
	}

	// ================================================================ spawner
	private void SpawnTick(float dt, Player player)
	{
		if (_w.State.BossActive) return; // đang đánh boss: không sinh quái thường / đội hình
		float t = DiffTime;                        // độ khó hiệu dụng (thời gian + cấp nhân vật)
		int lv = _w.State.Level;
		float faster = MathF.Max(1f - LevelSpawnFaster * (lv - 1), 0.5f);

		_spawnTimer -= dt;
		if (_spawnTimer <= 0f && Enemies.Count < MaxNow)
		{
			_spawnTimer = MathF.Max(SpawnIntervalMin * faster, SpawnIntervalStart - t * SpawnIntervalDecay);
			int n = Math.Min(1 + (int)(t / SpawnGrowthSeconds), Balance.EnemySpawnPerTick + (lv - 1) / 8);
			for (int i = 0; i < n && Enemies.Count < MaxNow; i++)
				Enemies.Add(EnemyTypes.Create(PickKind(t), t, player.Pos + RingOffset()));
		}

		_formationTimer -= dt;
		if (_formationTimer <= 0f)
		{
			_formationTimer = MathF.Max(FormationIntervalMin, FormationIntervalStart - t * 0.02f);
			SpawnFormation(t, player);
		}
	}

	/// <summary>Chọn loại quái theo trọng số; loại mạnh mở khoá dần theo thời gian.</summary>
	private static int PickKind(float t)
	{
		float wSlime = 1f;
		float wBat = t > 30f ? 0.8f : 0f;
		float wBomber = t > 60f ? MathF.Min(0.8f, 0.3f + (t - 60f) / 200f) : 0f;
		float wBrute = t > 150f ? MathF.Min(0.5f, 0.15f + (t - 150f) / 400f) : 0f;

		float roll = Rng.Randf() * (wSlime + wBat + wBomber + wBrute);
		if ((roll -= wSlime) < 0f) return EnemyTypes.Slime;
		if ((roll -= wBat) < 0f) return EnemyTypes.Bat;
		if ((roll -= wBomber) < 0f) return EnemyTypes.Bomber;
		return EnemyTypes.Brute;
	}

	// ================================================================ boss
	/// <summary>Dọn sạch quái thường và đội hình khi bắt đầu màn boss.</summary>
	public void ClearForBoss()
	{
		Enemies.Clear();
		_formations.Clear();
		Bolts.Clear();
	}

	public Enemy SpawnBoss(Vector2 pos)
	{
		var boss = EnemyTypes.Create(EnemyTypes.Boss, 0f, pos);
		Enemies.Add(boss);
		return boss;
	}
	/// <summary>Giai đoạn 3: gọi mắt chính THỨ HAI ở phía đối diện mắt chính thứ nhất (xuất hiện bất khả xâm phạm rồi gầm gọi 4 mắt con của riêng nó).</summary>
	public void SpawnSecondBoss(Enemy first, BossArena arena)
	{
		Vector2 pos = arena.Clamp(arena.Rect.Center * 2f - first.Pos, 60f);
		var second = EnemyTypes.Create(EnemyTypes.Boss, 0f, pos);
		var d = second.Boss;
		d.Index = 1;
		d.Stage = 3;
		d.Phase = 3;
		d.ShieldUp = true;
		d.ResummonCd = Difficulty.BossResummonInterval;
		PendingSpawns.Add(second);
	}

	/// <summary>Boss chết thì mọi quái nhỏ cùng biến mất (không rơi XP).</summary>
	public void KillMinions(Enemy boss)
	{
		Bolts.Clear();
		foreach (var e in Enemies)
		{
			if (e == boss || e.Dead) continue;
			e.Dead = true;
			e.NoDrop = true;
			e.Airborne = false;
			e.HitStop = 0f;
			e.Linger = 0.3f;
		}
	}

	// ================================================================ đội hình đặc biệt
	// Mỗi đội hình có luật di chuyển riêng (xem Formation.cs) và tồn tại cho tới khi tới gần / đi qua người chơi /
	// hết giờ, sau đó mỗi con quay về AI riêng của loại quái đó.

	/// <summary>Thời gian hiệu dụng tính độ khó = thời gian chơi + bonus theo cấp nhân vật.</summary>
	public float DiffTime => _w.State.Time + LevelSecondsBonus * (_w.State.Level - 1);

	/// <summary>Số quái tối đa cùng lúc hiện tại (tăng theo cấp).</summary>
	private int MaxNow => Math.Min(MaxEnemies + 2 * (_w.State.Level - 1), MaxEnemiesCap);

	private bool RoomFor(int n) => Enemies.Count + n <= MaxNow + FormationHeadroom;

	/// <summary>Hướng từ `from` tới chỗ người chơi SẼ ĐỨNG khi đội hình tới nơi (đón đầu theo vận tốc hiện tại, tối đa 2 giây).</summary>
	private static Vector2 AimAt(Vector2 from, Player player, float speed)
	{
		Vector2 to = player.Pos - from;
		float travel = MathF.Min(to.Length() / MathF.Max(speed, 1f), 2f);
		return (player.Pos + player.Velocity * travel - from).Normalized();
	}

	private Formation NewFormation(Formation.Mode mode, Vector2 center, Vector2 dir, float speed, float life, float breakDist, float turnRate = 0f)
	{
		// Cả đội hình cùng một bậc màu; đội hình mạnh hơn quái lẻ một chút (như thể sinh sau một phút)
		var g = new Formation { Kind = mode, Center = center, Dir = dir, Speed = speed, Life = life, BreakDist = breakDist, TurnRate = turnRate, Variant = EnemyTypes.PickVariant(_formationTime + 60f) };
		_formations.Add(g);
		return g;
	}

	private Enemy AddMember(int kind, float t, Vector2 pos, Formation g, Vector2 slot)
	{
		var e = EnemyTypes.Create(kind, t, pos, 0.95f + Rng.Randf() * 0.1f, g.Variant); // thành viên đội hình gần như đồng cỡ, đồng màu
		e.Group = g;
		e.Slot = slot;
		g.Members.Add(e);
		Enemies.Add(e);
		return e;
	}

	/// <summary>Cheat (debug): tạo ngay một đội hình theo tên (ring, wall, vflock, pincer, squad, escort). Trả về false nếu tên sai.</summary>
	public bool SpawnFormationNow(string name)
	{
		FormationType? type = name.ToLowerInvariant() switch
		{
			"ring" => FormationType.Ring,
			"wall" => FormationType.Wall,
			"vflock" => FormationType.VFlock,
			"pincer" => FormationType.Pincer,
			"squad" => FormationType.Squad,
			"escort" => FormationType.Escort,
			_ => null,
		};
		if (type == null) return false;

		float t = MathF.Max(DiffTime, 150f); // đủ thời gian để mọi loại quái/đội hình hợp lệ
		_formationTime = t;
		var player = _w.Player;
		switch (type.Value)
		{
			case FormationType.Ring: Swarm(t, player); break;
			case FormationType.Wall: SpawnWall(t, player); break;
			case FormationType.VFlock: SpawnVFlock(t, player); break;
			case FormationType.Pincer: SpawnPincer(t, player); break;
			case FormationType.Squad: SpawnSquad(t, player); break;
			case FormationType.Escort: SpawnEscort(t, player); break;
		}
		return true;
	}

	/// <summary>Chọn một đội hình (có trọng số, không lặp lại đội hình vừa rồi) rồi tạo nó ở rìa màn hình.</summary>
	private void SpawnFormation(float t, Player player)
	{
		_formationTime = t;
		var pool = new List<(FormationType type, float w)> { (FormationType.Ring, 1f) };
		if (t > 30f) pool.Add((FormationType.Wall, 1f));
		if (t > 45f) pool.Add((FormationType.VFlock, 1f));
		if (t > 60f) pool.Add((FormationType.Pincer, 1f));
		if (t > 90f) pool.Add((FormationType.Squad, 0.8f));
		if (t > 150f) pool.Add((FormationType.Escort, 0.8f));
		if (pool.Count > 1) pool.RemoveAll(p => p.type == _lastFormation);

		float total = 0f;
		foreach (var p in pool) total += p.w;
		float roll = Rng.Randf() * total;
		var pick = pool[0].type;
		foreach (var p in pool)
		{
			if ((roll -= p.w) < 0f) { pick = p.type; break; }
		}
		_lastFormation = pick;

		switch (pick)
		{
			case FormationType.Ring: Swarm(t, player); break;
			case FormationType.Wall: SpawnWall(t, player); break;
			case FormationType.VFlock: SpawnVFlock(t, player); break;
			case FormationType.Pincer: SpawnPincer(t, player); break;
			case FormationType.Squad: SpawnSquad(t, player); break;
			case FormationType.Escort: SpawnEscort(t, player); break;
		}
	}

	/// <summary>
	/// Tường quét: 2-3 hàng slime ngang (hàng sau lệch nửa ô) tiến thẳng qua chỗ người chơi,
	/// chừa khe hở 2 ô để chui qua hoặc né sang bên.
	/// </summary>
	private void SpawnWall(float t, Player player)
	{
		int perRow = Math.Min(12 + (int)(t / 50f), 20);
		int rows = t > 90f ? 3 : 2;
		if (!RoomFor(perRow * rows)) return;
		Vector2 pp = player.Pos;
		Vector2 center = pp + RingOffset();
		var g = NewFormation(Formation.Mode.March, center, AimAt(center, player, 55f), 55f, 20f, 0f, 0.35f); // đón đầu + xoay chậm theo mèo
		int gap = Rng.RandRange(2, perRow - 4);
		for (int r = 0; r < rows; r++)
			for (int i = 0; i < perRow; i++)
			{
				if (i == gap || i == gap + 1) continue;
				var slot = new Vector2(-r * 24f, (i - (perRow - 1) / 2f + (r % 2) * 0.5f) * 24f);
				AddMember(EnemyTypes.Slime, t, center + g.Dir * slot.X + g.Perp * slot.Y, g, slot);
			}
	}

	/// <summary>Đàn dơi chữ V: thủ lĩnh ở đỉnh, cánh dang ra phía sau; cả đàn uốn lượn khi bay, tới gần thì tản ra bay lắt léo.</summary>
	private void SpawnVFlock(float t, Player player)
	{
		int wings = Math.Min(4 + (int)(t / 60f), 9);
		if (!RoomFor(1 + 2 * wings)) return;
		Vector2 pp = player.Pos;
		Vector2 center = pp + RingOffset();
		var g = NewFormation(Formation.Mode.Weave, center, (pp - center).Normalized(), 85f, 14f, 100f);
		g.WeaveAmp = 0.8f;
		g.WeavePhase = Rng.Randf() * Mx.Tau;
		AddMember(EnemyTypes.Bat, t, center, g, Vector2.Zero);
		for (int k = 1; k <= wings; k++)
			foreach (float side in new[] { 1f, -1f })
			{
				var slot = new Vector2(-k * 20f, side * k * 18f);
				AddMember(EnemyTypes.Bat, t, center + g.Dir * slot.X + g.Perp * slot.Y, g, slot);
			}
	}

	/// <summary>Gọng kìm: hai hàng dọc slime từ hai phía đối diện cùng ép vào người chơi.</summary>
	private void SpawnPincer(float t, Player player)
	{
		int per = Math.Min(9 + (int)(t / 60f), 16);
		if (!RoomFor(2 * per)) return;
		Vector2 pp = player.Pos;
		float a = Rng.Randf() * Mx.Tau;
		for (int side = 0; side < 2; side++)
		{
			Vector2 center = pp + V.FromAngle(a + side * Mx.Pi) * SpawnDistance() * 0.9f;
			var g = NewFormation(Formation.Mode.March, center, AimAt(center, player, 75f), 75f, 12f, 90f, 1.0f); // gọng kìm bám sát hơn
			for (int i = 0; i < per; i++)
			{
				var slot = new Vector2(-i * 22f, 0f);
				AddMember(EnemyTypes.Slime, t, center + g.Dir * slot.X, g, slot);
			}
		}
	}

	/// <summary>Đội bomber: dàn hàng ngang giãn cách rộng (để vụ nổ không chồng nhau, khó né hơn) tiến về phía người chơi.</summary>
	private void SpawnSquad(float t, Player player)
	{
		int n = Math.Min(5 + (int)(t / 90f), 9);
		if (!RoomFor(n)) return;
		Vector2 pp = player.Pos;
		Vector2 center = pp + RingOffset();
		var g = NewFormation(Formation.Mode.Weave, center, (pp - center).Normalized(), 62f, 16f, 120f);
		g.WeaveAmp = 0.25f;
		g.WeavePhase = Rng.Randf() * Mx.Tau;
		for (int i = 0; i < n; i++)
		{
			var slot = new Vector2(0f, (i - (n - 1) / 2f) * 50f);
			AddMember(EnemyTypes.Bomber, t, center + g.Perp * slot.Y, g, slot);
		}
	}

	/// <summary>Brute dẫn đầu, hộ vệ (slime/dơi) đi sau lưng nó. Brute lao tới hoặc chết thì hộ vệ tản ra.</summary>
	private void SpawnEscort(float t, Player player)
	{
		int guards = Math.Min(8 + (int)(t / 45f), 16);
		if (!RoomFor(1 + guards)) return;
		Vector2 pp = player.Pos;
		Vector2 center = pp + RingOffset();
		Vector2 dir = (pp - center).Normalized();

		var leader = EnemyTypes.Create(EnemyTypes.Brute, t, center, 1.3f, EnemyTypes.PickVariant(t + 60f));
		Enemies.Add(leader);

		var g = NewFormation(Formation.Mode.Escort, center, dir, 0f, 25f, 75f);
		g.Leader = leader;
		for (int i = 0; i < guards; i++)
		{
			var slot = new Vector2(-(26f + (i / 2) * 22f), (i % 2 == 0 ? 1f : -1f) * (16f + (i / 2) * 14f));
			int kind = (i % 3 == 2 && t > 45f) ? EnemyTypes.Bat : EnemyTypes.Slime;
			AddMember(kind, t, center + dir * slot.X + g.Perp * slot.Y, g, slot);
		}
	}

	private void UpdateFormations(float dt, Player player)
	{
		for (int i = _formations.Count - 1; i >= 0; i--)
		{
			var g = _formations[i];
			g.Update(dt, player);
			if (g.Active && !g.Members.TrueForAll(m => m.Dead)) continue;

			// Giải thể: mọi thành viên quay về AI riêng
			foreach (var m in g.Members)
				if (m.Group == g) m.Group = null;
			_formations.RemoveAt(i);
		}
	}

	/// <summary>Vòng vây: một vòng slime bao quanh người chơi, cùng lúc lao vào từ mọi phía.</summary>
	private void Swarm(float t, Player player)
	{
		int n = Math.Min(SwarmBase + (int)(t / 15f), Balance.SwarmMax);
		float dist = SpawnDistance() * 0.8f;
		for (int i = 0; i < n && Enemies.Count < MaxNow + FormationHeadroom; i++)
		{
			Vector2 off = V.FromAngle(i * Mx.Tau / n) * dist;
			Enemies.Add(EnemyTypes.Create(EnemyTypes.Slime, t, player.Pos + off));
		}
	}

	private static float SpawnDistance() => new Vector2(Balance.ViewWidth, Balance.ViewHeight).Length() * 0.5f + 40f;
	private static Vector2 RingOffset() => V.FromAngle(Rng.Randf() * Mx.Tau) * SpawnDistance();

	// ================================================================ spatial hash
	private static long Key(int cx, int cy) => ((long)cx << 32) ^ (uint)cy;
	private static int Cell(float v) => (int)MathF.Floor(v / CellSize);

	private void RebuildGrid()
	{
		foreach (var list in _grid.Values)
		{
			list.Clear();
			_listPool.Push(list);
		}
		_grid.Clear();

		foreach (var e in Enemies)
		{
			if (e.Dead || e.Airborne) continue; // quái đang bay/đã chết không va chạm, không tách nhau
			long k = Key(Cell(e.Pos.X), Cell(e.Pos.Y));
			if (!_grid.TryGetValue(k, out var list))
			{
				list = _listPool.Count > 0 ? _listPool.Pop() : new List<Enemy>();
				_grid[k] = list;
			}
			list.Add(e);
		}
	}

	/// <summary>Mọi quái chạm đất còn sống mà vòng tròn (c, r) chạm tới. `result` được xoá trước khi điền.</summary>
	public void QueryCircle(Vector2 c, float r, List<Enemy> result)
	{
		result.Clear();
		int x0 = Cell(c.X - r - MaxEnemyRadius), x1 = Cell(c.X + r + MaxEnemyRadius);
		int y0 = Cell(c.Y - r - MaxEnemyRadius), y1 = Cell(c.Y + r + MaxEnemyRadius);
		for (int cx = x0; cx <= x1; cx++)
			for (int cy = y0; cy <= y1; cy++)
			{
				if (!_grid.TryGetValue(Key(cx, cy), out var list)) continue;
				foreach (var e in list)
				{
					if (e.Dead) continue;
					float reach = r + e.Radius;
					if ((e.Pos - c).LengthSquared() <= reach * reach)
						result.Add(e);
				}
			}
	}

	/// <summary>n quái gần `p` nhất trong tầm `range`, sắp xếp gần -> xa.</summary>
	public void FindNearest(Vector2 p, int n, float range, List<Enemy> result)
	{
		result.Clear();
		n = Mx.Clamp(n, 1, _nearestDist.Length);
		float r2 = range * range;

		foreach (var e in Enemies)
		{
			if (e.Dead || e.Airborne) continue;
			if (e.Kind == EnemyTypes.Boss && e.Boss.ShieldUp) continue; // mắt chính đang bất khả xâm phạm: vũ khí tự ngắm đừng phí đạn
			float d = (e.Pos - p).LengthSquared();
			if (d > r2) continue;

			int i;
			if (result.Count < n)
			{
				result.Add(e);
				i = result.Count - 1;
			}
			else if (d < _nearestDist[n - 1])
			{
				i = n - 1;
				result[i] = e;
			}
			else continue;

			_nearestDist[i] = d;
			// insertion sort: đẩy phần tử mới lên đúng vị trí
			while (i > 0 && _nearestDist[i - 1] > _nearestDist[i])
			{
				(_nearestDist[i - 1], _nearestDist[i]) = (_nearestDist[i], _nearestDist[i - 1]);
				(result[i - 1], result[i]) = (result[i], result[i - 1]);
				i--;
			}
		}
	}

	// ================================================================ simulation
	private void Simulate(float dtAll, Player player)
	{
		Vector2 pp = player.Pos;
		float knockDecay = MathF.Exp(-8f * dtAll);
		float slow = player.Skills.EnemyTimeScale, bossSlow = player.Skills.BossTimeScale;

		foreach (var e in Enemies)
		{
			float dt = dtAll * (e.Kind == EnemyTypes.Boss ? bossSlow : slow); // làm chậm thời gian: mỗi quái có "đồng hồ" riêng
			e.Flash = MathF.Max(e.Flash - dt, 0f);
			e.LandTimer = MathF.Max(e.LandTimer - dt, 0f);

			// Hit-stop: đứng khựng (không di chuyển, không gây sát thương). Hết khựng mới áp lực đẩy / cho chết.
			if (e.HitStop > 0f)
			{
				e.HitStop -= dt;
				if (e.HitStop <= 0f) ResolveHit(e);
				continue;
			}

			if (e.Airborne)
			{
				StepAirborne(e, dt);
				continue;
			}
			if (e.Dead)
			{
				e.Linger = MathF.Max(e.Linger - dt, 0f); // xác / hiệu ứng nổ đang chạy
				continue;
			}
			if (e.Kind == EnemyTypes.Boss)
			{
				BossAI.Step(e, dt, player, this, _w.Arena);
				continue;
			}
			if (e.Kind == EnemyTypes.MiniEye)
			{
				var owner = e.Mini?.Owner;
				if (owner != null && !owner.Dead) MiniEyes.Step(e, dt, player, this, _w.Arena, owner);
				continue;
			}

			e.AnimTime += dt;
			Vector2 to = pp - e.Pos;
			float dist = to.Length();

			// Quá xa -> dịch sang vòng ngoài màn hình để tái sử dụng
			if (dist > RecycleDistance)
			{
				e.Pos = pp + RingOffset();
				continue;
			}

			// --- Thành viên đội hình: bám theo ô của mình, chưa dùng AI riêng ---
			if (e.Group != null)
			{
				if (!e.Group.Active)
				{
					e.Group = null;
				}
				else
				{
					Vector2 off = e.Group.SlotPos(e) - e.Pos;
					if (off.Length() > FormationLeash)
					{
						e.Group = null; // bị văng / tụt xa ô của mình -> tự chiến đấu
					}
					else
					{
						float follow = MathF.Max(e.Speed * 2f, e.Group.Speed * 1.5f + 60f);
						Vector2 mv = (off * 8f).LimitLength(follow);
						e.Pos += (mv + e.Knock) * dt;
						// Nhìn theo hướng đang đi thật sự của đội hình (hướng đội hình + độ lệch bám ô), không phải hướng mèo
						Vector2 heading = e.Group.Dir * MathF.Max(e.Group.Speed, 30f) + mv;
						if (MathF.Abs(heading.X) > 2f) e.FaceX = heading.X;
						e.Knock *= knockDecay;
						if (e.Kind != EnemyTypes.Bomber && dist < e.Radius + player.Radius)
							player.TakeDamage(e.Damage, e.Pos, e.Impact);
						continue;
					}
				}
			}

			Vector2 dir = dist > 0.01f ? to / dist : Vector2.Zero;
			float speed = e.Speed;

			// --- AI riêng của từng loại ---
			switch (e.Kind)
			{
				case EnemyTypes.Slime:
				{
					// Slime đặc biệt: thỉnh thoảng ra đòn vồ tới (xem hằng số Pounce*)
					if (e.IsSpecial)
					{
						e.PounceCd = MathF.Max(e.PounceCd - dt, 0f);
						switch (e.PounceState)
						{
							case 0:
								if (e.PounceCd <= 0f && dist > PounceMinRange && dist < PounceMaxRange)
								{
									e.PounceAttempt -= dt;
									if (e.PounceAttempt <= 0f)
									{
										e.PounceAttempt = 1f; // thử một lần mỗi giây
										if (Rng.Randf() < PounceChancePerSecond)
										{
											e.PounceState = 1;
											e.PounceTimer = 0f; e.PounceDir = Prediction.Dir(e.Pos, player, PounceAimSpeed(dist), dir, PounceWindup, _w.Arena); // chặn đầu: ngắm chỗ mèo sẽ tới (tuỳ độ khó)
										}
									}
								}
								break;
							case 1: // gồng: đứng yên, nửa đầu ngắm theo người chơi, nửa sau khoá hướng
								speed = 0f;
								e.PounceTimer += dt;
								if (e.PounceTimer < PounceWindup * PounceTrackFrac) e.PounceDir = Prediction.Dir(e.Pos, player, PounceAimSpeed(dist), dir, PounceWindup - e.PounceTimer, _w.Arena);
								if (MathF.Abs(e.PounceDir.X) > 0.05f) e.FaceX = e.PounceDir.X;
								if (e.PounceTimer >= PounceWindup)
								{
									e.PounceState = 2;
									e.PounceTimer = 0f;
									e.PounceHit = false;
									// Bay quá chỗ người chơi đứng một chút (tối đa PounceDistance)
									e.PounceSpeed = Mx.Clamp(dist + 40f, 90f, PounceDistance) / PounceTime;
								}
								break;
							case 2: // vồ: lao thẳng theo hướng đã khoá, không đổi hướng
								dir = e.PounceDir;
								speed = e.PounceSpeed;
								e.PounceTimer += dt;
								if (e.PounceTimer >= PounceTime) { e.PounceState = 3; e.PounceTimer = 0f; }
								break;
							default: // khựng sau khi vồ
								speed = 0f;
								e.PounceTimer += dt;
								if (e.PounceTimer >= PounceRecover) { e.PounceState = 0; e.PounceCd = PounceCooldown; }
								break;
						}
						if (e.PounceState != 0) break; // đang ra đòn: không lượn / nhún
					}

					// Vây khéo: mỗi con lệch hướng một góc riêng khi còn xa, càng gần càng thẳng vào
					// -> cả bầy tản ra bao quanh người chơi thay vì xếp hàng một.
					dir = Prediction.Dir(e.Pos, player, e.Speed, dir, 0f, _w.Arena); // đuổi theo chỗ mèo sẽ tới thay vì chỗ đang đứng (Dễ: như cũ)
					float flank = e.Bias * 0.9f * Mx.Clamp((dist - 40f) / 160f, 0f, 1f);
					dir = dir.Rotated(flank);
					// Nhún: tốc độ theo nhịp nhảy của animation (lao nhanh lúc bật, gần như đứng yên lúc bẹp xuống)
					float p01 = 0.5f + 0.5f * MathF.Sin(e.AnimTime * Mx.Tau / 0.7f);
					speed *= 0.35f + 1.7f * p01 * p01;
					break;
				}

				case EnemyTypes.Bat:
				{
					e.ScreechCd = MathF.Max(e.ScreechCd - dt, 0f);
					if (e.ScreechTime >= 0f)
					{
						speed = 0f; // đứng lại gào
						UpdateScreech(e, dt, player);
						break;
					}

					if (e.ScreechCd <= 0f)
					{
						// Sẵn sàng gào: bay vòng ra SƯỜN, cùng hàng ngang với người chơi (phễu mở sang trái/phải),
						// rồi chỉ gào khi người chơi đã nằm gọn trong phễu.
						Vector2 pred = Prediction.InterceptPoint(e.Pos, player, e.Speed, 0f, _w.Arena); // bay tới sườn của chỗ mèo SẼ đứng
						float side = e.Pos.X < pred.X ? -1f : 1f; // đứng bên gần hơn
						Vector2 goal = pred + new Vector2(side * ScreechRange * e.Size * ScreechStandOff, 0f);
						Vector2 toGoal = goal - e.Pos;
						float goalDist = toGoal.Length();
						dir = goalDist > 4f ? toGoal / goalDist : Vector2.Zero;
						speed *= MathF.Min(1f, goalDist / 20f + 0.3f); // tới gần điểm đứng thì chậm lại

						Vector2 hitPos = Prediction.PositionAfter(player, ScreechWindup, _w.Arena); // gào khi mèo SẼ nằm trong phễu lúc sóng tới
							if (InScreechCone(e.Pos, hitPos, 0f, e.Size, ScreechHalfAngle * ScreechFireAngle) && (hitPos - e.Pos).Length() <= ScreechRange * e.Size * ScreechFireRange)
						{
							e.ScreechTime = 0f;
							e.ScreechHit = false;
						}
					}
					else
					{
						// Hồi chiêu: lượn vòng quanh người chơi (xoắn vào nếu còn xa, đẩy ra nếu quá gần), thỉnh thoảng đổi chiều
						e.OrbitTimer -= dt;
						if (e.OrbitTimer <= 0f)
						{
							if (Rng.Randf() < 0.5f) e.OrbitSign = -e.OrbitSign;
							e.OrbitTimer = 1.5f + Rng.Randf() * 2f;
						}
						float radial = Mx.Clamp((dist - BatOrbitRadius) / 60f, -1f, 1f);
						Vector2 tangent = new Vector2(-dir.Y, dir.X) * e.OrbitSign;
						dir = (dir * radial * 1.2f + tangent).Normalized();
					}

					// Lắt léo: lượn bằng hai sóng lệch tần số + thỉnh thoảng phóng ngang một nhát ngắn
					float wob = MathF.Sin(e.AnimTime * 3.1f + e.Phase) * 0.5f + MathF.Sin(e.AnimTime * 7.3f + e.Phase * 2f) * 0.3f;
					dir = dir.Rotated(wob * (e.ScreechCd <= 0f ? 0.5f : 1f));
					e.DartTimer -= dt;
					if (e.DartLeft > 0f)
					{
						e.DartLeft -= dt;
						dir = dir.Rotated(1.3f * e.DartSign);
						speed *= 1.8f;
					}
					else if (e.DartTimer <= 0f)
					{
						e.DartTimer = 0.6f + Rng.Randf() * 0.9f;
						e.DartLeft = 0.22f;
						e.DartSign = Rng.Randf() < 0.5f ? -1f : 1f;
					}
					break;
				}

				case EnemyTypes.Bomber:
				{
					if (e.FuseTime >= 0f)
					{
						e.FuseTime -= dt;
						e.FuseElapsed += dt;
						speed *= BombFuseSpeed;
						if (e.FuseTime <= 0f) { Explode(e, player); continue; }
					}
					else if (dist < BombTrigger * e.Size)
					{
						e.FuseTime = BombFuse; // châm ngòi
					}
					else
					{
						// Đón đầu: nhắm vào chỗ người chơi SẼ ĐỨNG (theo vận tốc hiện tại) chứ không phải chỗ đang đứng
						float tLead = MathF.Min(dist / MathF.Max(speed, 1f), 0.9f);
						Vector2 aim = pp + player.Velocity * tLead - e.Pos;
						if (aim.LengthSquared() > 1f) dir = aim.Normalized();
							if (Difficulty.Level >= DifficultyLevel.Hard) dir = Prediction.Dir(e.Pos, player, speed * 1.25f, dir, 0f, _w.Arena); // Khó trở lên: chặn đầu chuẩn hơn
						if (dist < BombSprintRange) speed = MathF.Min(MathF.Max(speed * 1.25f, player.RunSpeed * BombChaseRatio), BombMaxSpeed); // nước rút: nhanh hơn cả mèo để bắt kịp mà nổ
					}
					break;
				}

				case EnemyTypes.Brute:
				{
					// Gồng -> lao thẳng -> kiệt sức. Trong lúc lao không đổi hướng: né sang ngang là thoát.
					e.ChargeCd = MathF.Max(e.ChargeCd - dt, 0f);
					e.ShieldFlash = MathF.Max(e.ShieldFlash - dt, 0f);
					if (e.ShieldBreakT >= 0f) e.ShieldBreakT += dt;
					switch (e.ChargeState)
					{
						case 0:
							dir = Prediction.Dir(e.Pos, player, e.Speed, dir, 0f, _w.Arena); // đuổi theo chỗ mèo sẽ tới
							if (e.ShieldBroken) { speed *= BruteBrokenSpeedMul; break; }       // khiên vỡ: chỉ đi chậm, không gồng/lao nữa
								if (e.ChargeCd <= 0f && dist < BruteChargeRange)
							{
								e.ChargeState = 1;
								e.ChargeTimer = BruteWindup; e.ChargeDir = Prediction.Dir(e.Pos, player, BruteChargeSpeed, dir, BruteWindup, _w.Arena);
							}
							break;
						case 1:
							speed = 0f;
							e.ChargeTimer -= dt;
							if (e.ChargeTimer > BruteWindup * 0.5f) e.ChargeDir = Prediction.Dir(e.Pos, player, BruteChargeSpeed, dir, e.ChargeTimer, _w.Arena); // nửa đầu còn ngắm, nửa sau khoá hướng
							if (e.ChargeTimer <= 0f) { e.ChargeState = 2; e.ChargeTimer = BruteChargeTime; }
							break;
						case 2:
							dir = e.ChargeDir;
							speed = BruteChargeSpeed;
							e.ChargeTimer -= dt;
							if (e.ChargeTimer <= 0f) { e.ChargeState = 3; e.ChargeTimer = BruteTired; }
							break;
						default:
							speed = 0f;
							e.ChargeTimer -= dt;
							if (e.ChargeTimer <= 0f) { e.ChargeState = 0; e.ChargeCd = BruteCooldown; }
							break;
					}
					break;
				}
			}
			// Separation: đẩy khỏi các quái chồng lấn (chỉ xét 9 ô lân cận)
			Vector2 push = Vector2.Zero;
			int cx = Cell(e.Pos.X), cy = Cell(e.Pos.Y);
			for (int dx = -1; dx <= 1; dx++)
				for (int dy = -1; dy <= 1; dy++)
				{
					if (!_grid.TryGetValue(Key(cx + dx, cy + dy), out var list)) continue;
					foreach (var o in list)
					{
						if (o == e) continue;
						Vector2 d = e.Pos - o.Pos;
						float min = e.Radius + o.Radius;
						float d2 = d.LengthSquared();
						if (d2 < min * min && d2 > 0.0001f)
						{
							float len = MathF.Sqrt(d2);
							// Weight: bên nặng hơn đẩy bên nhẹ nhiều hơn (cùng weight -> hệ số 1)
							float share = 2f * o.Weight / (e.Weight + o.Weight);
							push += d / len * ((min - len) / min) * share;
						}
					}
				}

			Vector2 vel = dir * speed + push * 60f + e.Knock;
			if (MathF.Abs(vel.X) > 2f) e.FaceX = vel.X; // nhìn theo hướng đang đi
			e.Pos += vel * dt;
			e.Knock *= knockDecay;

			// Bomber không gây sát thương khi chạm; nó gây sát thương bằng vụ nổ. Brute đang lao đau hơn.
			if (e.Kind != EnemyTypes.Bomber && dist < e.Radius + player.Radius)
			{
				float dmg = e.Kind == EnemyTypes.Brute && e.ChargeState == 2 ? e.Damage * BruteChargeDamageMul : e.Damage;
				float impact = e.Impact;
				if (e.Kind == EnemyTypes.Slime && e.PounceState == 2)
				{
					// Đang vồ: đòn nặng, nhưng mỗi cú vồ chỉ trúng một lần
					if (!e.PounceHit)
					{
						e.PounceHit = true;
						player.TakeDamage(MathF.Min(e.Damage * PounceDamageMul, Balance.EnemyDamageHard), e.Pos, e.Impact * PounceImpactMul);
					}
				}
				else
				{
					player.TakeDamage(dmg, e.Pos, impact);
				}
			}
		}

		// Nhận quái sinh ra trong lúc duyệt (boss triệu hồi)
		if (PendingSpawns.Count > 0)
		{
			Enemies.AddRange(PendingSpawns);
			PendingSpawns.Clear();
		}

		// Đang đánh boss: quái nhỏ cũng không ra khỏi vùng khoá
		if (_w.Arena.Active)
			foreach (var e in Enemies)
				if (e.Kind != EnemyTypes.Boss) e.Pos = _w.Arena.Clamp(e.Pos, e.Radius);
	}

	/// <summary>
	/// Bomber nổ: sát thương + đẩy lùi nếu người chơi trong bán kính; hất các quái xung quanh
	/// (không gây sát thương, lực chia cho Weight nên quái nhẹ bay xa, quái nặng gần như đứng yên);
	/// sau đó chạy hiệu ứng rồi biến mất.
	/// </summary>
	private void Explode(Enemy e, Player player)
	{
		e.Exploded = true;
		e.Dead = true;
		e.NoDrop = true;
		e.Linger = BombAnim;
		float blast = BombRadius * e.Size; // bomber to -> vụ nổ rộng hơn
		if ((player.Pos - e.Pos).Length() <= blast + player.Radius)
			player.TakeDamage(e.Damage, e.Pos, e.Impact);

		var near = new List<Enemy>();
		QueryCircle(e.Pos, blast, near);
		foreach (var o in near)
		{
			if (o == e) continue;
			Vector2 away = o.Pos - e.Pos;
			away = away.LengthSquared() > 0.01f ? away.Normalized() : V.FromAngle(Rng.Randf() * Mx.Tau);
			o.PendingKnock += away * BlastKnock / o.Weight;
			o.HitStop = MathF.Max(o.HitStop, 0.05f);
			o.ScreechTime = -1f;
		}
	}

	/// <summary>
	/// Bat gào: Windup (báo hiệu) -> Wave (sóng âm, tính sát thương một lần ở đầu pha) -> Recover.
	/// Vùng sát thương là 2 hình quạt/phễu ở bên TRÁI và PHẢI con dơi; đứng phía trên/dưới thì an toàn.
	/// </summary>
	private void UpdateScreech(Enemy e, float dt, Player player)
	{
		e.ScreechTime += dt;
		if (!e.ScreechHit && e.ScreechTime >= ScreechWindup)
		{
			e.ScreechHit = true;
			if (InScreechCone(e.Pos, player.Pos, player.Radius, e.Size))
				player.TakeDamage(e.Damage * ScreechDamageMul, e.Pos, ScreechImpact);
		}
		if (e.ScreechTime >= ScreechTotal)
		{
			e.ScreechTime = -1f;
			e.ScreechCd = ScreechCooldown;
		}
	}

	private static bool InScreechCone(Vector2 origin, Vector2 target, float targetRadius, float size, float halfAngle = ScreechHalfAngle)
	{
		Vector2 v = target - origin;
		if (v.Length() > ScreechRange * size + targetRadius) return false; // dơi to -> phễu dài hơn
		// Góc lệch so với trục ngang (cả trái lẫn phải đều tính: lấy trị tuyệt đối)
		return MathF.Atan2(MathF.Abs(v.Y), MathF.Abs(v.X)) <= halfAngle;
	}

	/// <summary>
	/// Một bước vật lý khi quái đang bay: chuyển động ngang đều + rơi tự do theo Z.
	/// Chạm đất thì nảy lại (mất dần năng lượng) cho tới khi không còn nảy được nữa.
	/// </summary>
	private static void StepAirborne(Enemy e, float dt)
	{
		e.Pos += e.Vel * dt;
		e.Vz -= Gravity * dt;
		e.Z += e.Vz * dt;
		if (e.Z > 0f) return;

		e.Z = 0f;
		e.LandTimer = 0.1f;
		if (e.Vz < -MinBounce)
		{
			e.Vz = -e.Vz * Restitution;
			e.Vel *= BounceFriction;
			e.Bounces++;
		}
		else
		{
			e.Vz = 0f;
			e.Vel = Vector2.Zero;
			e.Airborne = false; // đáp hẳn xuống: quay về trạng thái bình thường (hoặc nằm lại nếu đã chết)
		}
	}

	/// <summary>Gom quái đã chết VÀ đã bay xong VÀ hết thời gian nằm lại: thả XP, xoá bằng swap-remove.</summary>
	private void Reap()
	{
		for (int i = Enemies.Count - 1; i >= 0; i--)
		{
			var e = Enemies[i];
			if (!e.Dead || e.Airborne || e.HitStop > 0f || e.Linger > 0f) continue;

			if (!e.NoDrop)
			{
				_w.Pickups.DropGem(e.Pos, e.Xp);
				if (Difficulty.AllowHealing && Rng.Randf() < 0.008f) _w.Pickups.DropHeal(e.Pos); // Siêu khó: không rơi bình máu
			}

			Enemies[i] = Enemies[^1];
			Enemies.RemoveAt(Enemies.Count - 1);
		}
	}

	/// <summary>
	/// Gây sát thương. Quái khựng lại (hit-stop) + nháy trắng; lực đẩy `knock` (px/s) được dồn lại
	/// và chỉ áp dụng khi hết khựng (xem ResolveHit). Quái hết máu bị đánh dấu Dead ngay (không bị đánh nữa).
	/// </summary>
	public void Damage(Enemy e, float amount, Vector2 knock)
	{
		if (e.Dead) return;

		// Mắt con: không bị khựng/đẩy văng (chỉ nháy trắng); chết thì mờ dần
		if (e.Kind == EnemyTypes.MiniEye)
		{
			e.Hp -= MathF.Max(amount - e.Armor, amount * Balance.ArmorMinFraction);
			e.Flash = 0.08f;
			if (e.Hp <= 0f)
			{
				e.Hp = 0f;
				e.Dead = true;
				e.NoDrop = true;
				e.Linger = 0.45f;
				_w.State.Kills++;
				SoundService.Play("kill", -8f, 0.05f);
			}
			return;
		}

		// Boss: không bị khựng/đẩy văng; xuất hiện thì bất khả xâm phạm; kiệt sức (đâm tường) thì chịu thêm 50% sát thương
		if (e.Kind == EnemyTypes.Boss)
		{
			var bd = e.Boss;
			if (bd.State == 0 || bd.BarEmpty) return;                 // vừa xuất hiện / đang chuyển giai đoạn: bất khả xâm phạm
			if (bd.ShieldUp) { bd.ShieldFlash = 0.12f; return; }      // còn mắt con thì mắt chính không bị thương
			float reduced = MathF.Max(amount - e.Armor, amount * Balance.ArmorMinFraction) * (bd.State == 4 ? 1.5f : 1f);
			e.Hp -= reduced;
			e.Flash = 0.08f;
			if (e.Hp <= 0f)
			{
				e.Hp = 0f;
				if (bd.Stage < 3)
				{
					bd.BarEmpty = true;                               // cạn THANH MÁU của giai đoạn: BossAI.Step chuyển sang giai đoạn kế, thanh đầy lại
				}
				else
				{
					e.Dead = true;
					e.NoDrop = true;
					e.Linger = 3.5f;
					KillOwnedMinis(e);
					_w.State.Kills++;
				}
			}
			return;
		}

		// Giáp: trừ phẳng vào mỗi đòn (đòn nhỏ bị giảm rất mạnh -> cần vũ khí nặng / sát thương lớn để xuyên giáp)
		amount = MathF.Max(amount - e.Armor, amount * Balance.ArmorMinFraction);

		// Brute còn KHIÊN: đòn trừ vào khiên trước (không mất máu, không nháy trắng); phần dư làm vỡ khiên mới tràn sang máu
		if (e.Kind == EnemyTypes.Brute && e.ShieldHp > 0f)
		{
			e.ShieldFlash = 0.12f;
			e.PendingKnock += knock / e.Weight;
			float absorbed = MathF.Min(amount, e.ShieldHp);
			e.ShieldHp -= absorbed;
			amount -= absorbed;
			if (e.ShieldHp <= 0f)
			{
				e.ShieldHp = 0f;
				BreakShield(e);
			}
			if (amount <= 0f)
			{
				e.HitStop = MathF.Max(e.HitStop, e.ShieldBroken ? HitStopTime * 2f : HitStopTime);
				return;
			}
		}

		e.Hp -= amount;
		e.Flash = HitFlashTime;
		e.PendingKnock += knock / e.Weight;

		// Bị đánh là bị ngắt tiếng gào
		if (e.ScreechTime >= 0f)
		{
			e.ScreechTime = -1f;
			e.ScreechCd = 1.2f;
		}

		if (e.Hp <= 0f)
		{
			e.Dead = true;
			e.Linger = CorpseLinger;
			_w.State.Kills++;
			SoundService.Play("kill", -10f, 0.04f);
			e.HitStop = MathF.Max(e.HitStop, KillStopTime);
		}
		else
		{
			e.HitStop = MathF.Max(e.HitStop, HitStopTime);
		}
	}

	/// <summary>Khiên Brute vỡ: huỷ đòn lao đang gồng/lao/kiệt sức, từ giờ chỉ đi chậm (xem AI Brute); phát tiếng + mảnh khiên bay ra.</summary>
	private void BreakShield(Enemy e)
	{
		e.ShieldBroken = true;
		e.ShieldBreakT = 0f;
		e.ChargeState = 0;
		e.ChargeTimer = 0f;
		SoundService.Play("slam", -8f, 0.1f);
	}

	/// <summary>Hết khựng: đủ mạnh thì văng góc 45°; yếu hơn thì trượt nhẹ; chết thì luôn nảy lên một nhịp.</summary>
	private static void ResolveHit(Enemy e)
	{
		Vector2 k = e.PendingKnock;
		e.PendingKnock = Vector2.Zero;
		float speed = k.Length();

		if (e.Dead)
		{
			Vector2 d = speed > 1f ? k / speed : V.FromAngle(Rng.Randf() * Mx.Tau);
			Launch(e, d, MathF.Max(speed, DeathLaunch / e.Weight)); // chết cũng nảy; quái nặng nảy thấp hơn
		}
		else if (speed >= LaunchThreshold)
		{
			Launch(e, k / speed, speed);
		}
		else if (e.Airborne)
		{
			e.Vel += k * 0.5f; // đang bay mà bị đánh nhẹ: chỉ lệch hướng một chút
		}
		else
		{
			e.Knock += k;
		}
	}

	/// <summary>Văng lên góc 45°: vận tốc đứng = vận tốc ngang = speed.</summary>
	private static void Launch(Enemy e, Vector2 dir, float speed)
	{
		speed = MathF.Min(speed * LaunchSpeedScale, MaxLaunch);
		e.Vel = dir * speed;
		e.Vz = speed;
		e.Z = MathF.Max(e.Z, 0.5f);
		e.Airborne = true;
		e.Knock = Vector2.Zero;
		e.PounceState = 0;                                // bị văng thì đòn vồ bị huỷ
		e.PounceCd = MathF.Max(e.PounceCd, 3f);
		if (!e.Dead) e.Bounces = 0;
	}

	// ================================================================ drawing
	private Canvas _c; // đang vẽ: các hàm Draw* bên dưới đều vẽ lên đây

	// ---- Chiều sâu: lấy MÈO làm tâm ----
	// Mọi thứ được so sánh theo Y của "chân" (vị trí tiếp đất, không tính độ cao Z):
	//   chân quái cao hơn chân mèo (Y nhỏ hơn) -> quái đứng sau mèo -> lớp "sau" (vẽ trước mèo)
	//   chân quái thấp hơn chân mèo (Y lớn hơn) -> quái đứng trước mèo -> lớp "trước" (vẽ sau mèo)
	// Trong mỗi lớp quái được sắp xếp tiếp theo Y của chân (nhỏ vẽ trước).
	private float FootY(Enemy e) => e.Pos.Y + ShadowDown(e);

	private int ByDepth(Enemy a, Enemy b)
	{
		float fa = FootY(a), fb = FootY(b);
		return fa != fb ? fa.CompareTo(fb) : a.Pos.X.CompareTo(b.Pos.X); // hoà thì xét X để thứ tự ổn định, không nhấp nháy
	}

	/// <summary>Chia quái thành lớp sau/trước mèo và sắp xếp theo chiều sâu. Gọi mỗi khung hình trước khi vẽ.</summary>
	public void PrepareDraw(Vector2 viewTopLeft, Vector2 viewSize)
	{
		_backList.Clear();
		_frontList.Clear();
		float playerFootY = _w.Player.Pos.Y + Player.FootOffset;
		foreach (var e in Enemies)
		{
			// Quái ngoài màn hình không cần vẽ (đông quái thì tiết kiệm rất nhiều). Quái đang có vùng báo hiệu rộng thì chừa lề lớn hơn; boss luôn vẽ.
			if (e.Kind != EnemyTypes.Boss)
			{
				bool telegraph = e.ChargeState == 1 || e.PounceState == 1 || e.ScreechTime >= 0f || e.FuseTime >= 0f;
				float m = telegraph ? 240f : 80f;
				if (e.Pos.X < viewTopLeft.X - m || e.Pos.X > viewTopLeft.X + viewSize.X + m
					|| e.Pos.Y < viewTopLeft.Y - m || e.Pos.Y > viewTopLeft.Y + viewSize.Y + m) continue;
			}
			(FootY(e) <= playerFootY ? _backList : _frontList).Add(e);
		}
		_backList.Sort(_byDepth);
		_frontList.Sort(_byDepth);
	}

	/// <summary>
	/// Hitbox quái (Cài đặt > Hiện hitbox), vẽ đúng vùng gây sát thương thật (không phụ thuộc độ khó có hiện vùng báo hiệu hay không):
	///  - vòng ĐỎ = va chạm thân; CAM đậm = thân đang tung đòn lao/vồ (Brute lao, slime vồ, boss lao); XÁM = boss không gây sát thương (xuất hiện / kiệt sức)
	///  - CAM: dải lao/vồ, hai phễu gào của dơi, vùng nổ bomber, vòng giậm + sóng xung kích của boss, chưởng và chùm tia chữ thập của mắt con. Gọi sau PrepareDraw.
	/// </summary>
	public void DrawHitboxes(Canvas c)
	{
		var attack = new Rgba(1f, 0.55f, 0.1f, 0.85f);
		void Lane(Vector2 a, Vector2 b, float r)
		{
			Vector2 n = (b - a).Normalized().Rotated(Mx.Pi / 2f) * r;
			c.DrawPolyline(new[] { a + n, b + n, b - n, a - n, a + n }, attack, 1f);
		}
		void Capsule(Vector2 a, Vector2 b, float r, Rgba col)
		{
			Vector2 d = (b - a).Normalized();
			Vector2 n = d.Rotated(Mx.Pi / 2f) * r;
			c.DrawLine(a + n, b + n, col, 1f);
			c.DrawLine(a - n, b - n, col, 1f);
			float ang = d.Angle();
			c.DrawArc(b, r, ang - Mx.Pi / 2f, ang + Mx.Pi / 2f, col, 1f);
			c.DrawArc(a, r, ang + Mx.Pi / 2f, ang + Mx.Pi * 1.5f, col, 1f);
		}

		void One(Enemy e)
		{
			if (e.Dead) return;
			bool striking = e.Kind == EnemyTypes.Brute && e.ChargeState == 2
				|| e.Kind == EnemyTypes.Slime && e.PounceState == 2
				|| e.Kind == EnemyTypes.Boss && e.Boss != null && e.Boss.State == 3;
			bool harmless = e.Kind == EnemyTypes.Boss && e.Boss != null && (e.Boss.State == 0 || e.Boss.State == 4);
			c.DrawArc(e.Pos, e.Radius, 0f, Mx.Tau,
				harmless ? new Rgba(0.65f, 0.65f, 0.65f, 0.8f) : striking ? new Rgba(1f, 0.5f, 0.1f, 1f) : new Rgba(1f, 0.25f, 0.25f, 0.9f), striking ? 2f : 1f);

			if (e.Kind == EnemyTypes.Brute && e.ChargeState == 1)
				Lane(e.Pos, e.Pos + e.ChargeDir * BruteTelegraphLen, e.Radius);                 // dải sắp lao
			if (e.Kind == EnemyTypes.Slime && e.PounceState == 1)
				Lane(e.Pos, e.Pos + e.PounceDir * Mx.Clamp((_w.Player.Pos - e.Pos).Length() + 40f, 90f, PounceDistance), e.Radius);   // dải sắp vồ

			if (e.Kind == EnemyTypes.Bat && e.ScreechTime >= 0f)
			{
				float range = ScreechRange * e.Size;
				for (int side = 0; side < 2; side++)                                              // hai phễu hai bên: sát thương tính một lần khi hết gồng
				{
					float mid = side == 0 ? 0f : Mx.Pi;
					Vector2 p0 = e.Pos + V.FromAngle(mid - ScreechHalfAngle) * range, p1 = e.Pos + V.FromAngle(mid + ScreechHalfAngle) * range;
					c.DrawPolyline(new[] { p0, e.Pos, p1 }, attack, 1f);
					c.DrawArc(e.Pos, range, mid - ScreechHalfAngle, mid + ScreechHalfAngle, attack, 1f);
				}
			}

			if (e.Kind == EnemyTypes.Bomber && e.FuseTime >= 0f)
				c.DrawArc(e.Pos, BombRadius * e.Size, 0f, Mx.Tau, attack, 1f);
			if (e.Kind == EnemyTypes.Boss && e.Boss != null)
			{
				if (e.Boss.State == 5) c.DrawArc(e.Pos, BossAI.SlamRadiusValue, 0f, Mx.Tau, attack, 1f);
				foreach (var ring in e.Boss.Rings)
				{
					c.DrawArc(ring.Center, MathF.Max(ring.R - 10f, 1f), 0f, Mx.Tau, attack, 1f);
					c.DrawArc(ring.Center, ring.R + 10f, 0f, Mx.Tau, attack, 1f);
				}
				if (e.Boss.CrossState != 0)                                                       // chùm tia chữ thập: gồng = đường mảnh mờ, quét = vùng sát thương thật
					for (int i = 0; i < MiniEyes.Count; i++)
					{
						var (a, b) = MiniEyes.Beam(e, i);
						if (e.Boss.CrossState == 2) Capsule(a, b, MiniEyes.BeamWidth * 0.5f, attack);
						else c.DrawLine(a, b, new Rgba(attack.R, attack.G, attack.B, 0.35f), 1f);
					}
			}
		}
		foreach (var e in _backList) One(e);
		foreach (var e in _frontList) One(e);

		foreach (var bolt in Bolts)                                                          // chưởng (viên plasma) của mắt con
			if (!bolt.Dead) Capsule(bolt.Pos - bolt.Dir * bolt.Length, bolt.Pos, MiniEyes.BoltThickness * 0.5f, attack);
	}

	public void DrawBack(Canvas c) => DrawLayer(c, _backList);
	public void DrawFront(Canvas c) => DrawLayer(c, _frontList);

	/// <summary>
	/// Vẽ một lớp quái. Danh sách đã được sắp xếp theo chiều sâu (Y của CHÂN tăng dần: xa -> gần)
	/// nên quái ở phía trước luôn đè lên quái ở phía sau.
	/// </summary>
	private void DrawLayer(Canvas c, List<Enemy> list)
	{
		_c = c;

		// Lượt 1: bóng + vùng cảnh báo, nằm dưới mặt đất (không bị nhấc theo Z)
		foreach (var e in list)
		{
			if (e.Exploded) continue;

			if (Difficulty.ShowEnemyTelegraphs && e.Kind == EnemyTypes.Bomber && e.FuseTime >= 0f && !e.Dead) // độ khó Khó trở lên: không vẽ vùng báo hiệu đòn của quái
			{
				float pulse = 0.5f + 0.5f * MathF.Sin(e.FuseElapsed * 18f);
				_c.ResetTransform();
				_c.DrawCircle(e.Pos, BombRadius * e.Size, new Rgba(1f, 0.2f, 0.1f, 0.07f + 0.08f * pulse));
				_c.DrawArc(e.Pos, BombRadius * e.Size, 0f, Mx.Tau, new Rgba(1f, 0.25f, 0.1f, 0.4f + 0.4f * pulse), 1.5f);
			}

			if (Difficulty.ShowEnemyTelegraphs && e.Kind == EnemyTypes.Bat && e.ScreechTime >= 0f && !e.Dead)
				DrawScreechCones(e);

			if (Difficulty.ShowEnemyTelegraphs && e.Kind == EnemyTypes.Brute && e.ChargeState == 1 && !e.Dead)
				DrawChargeTelegraph(e);

			if (Difficulty.ShowEnemyTelegraphs && e.Kind == EnemyTypes.Slime && e.PounceState == 1 && !e.Dead)
				DrawPounceTelegraph(e);

			if (e.Kind == EnemyTypes.Boss && !e.Dead)
				DrawBossGround(e);

			// Bóng (elip dẹt, thu nhỏ và mờ dần khi quái bay cao) - vẽ thẳng, không đổi ma trận
			float s = 1f / (1f + (e.Z + PounceLift(e) * e.Size) / 60f);
			Vector2 foot = e.Pos + new Vector2(0f, ShadowDown(e));
			_c.FillEllipse(foot, e.Radius * s, e.Radius * 0.4f * s, new Rgba(0f, 0f, 0f, 0.28f * s + 0.07f));

			// Quái đặc biệt: vòng đỏ nhấp nháy dưới chân (cùng mặt phẳng với bóng)
			if (e.IsSpecial && !e.Dead)
				_c.DrawEllipse(foot, e.Radius * 1.3f * s, e.Radius * 1.3f * 0.4f * s, new Rgba(1f, 0.25f, 0.15f, 0.55f + 0.35f * MathF.Sin(e.AnimTime * 6f)), 2f);
		}
		_c.ResetTransform();

		// Lượt 2: thân quái (theo thứ tự xa -> gần), nhấc lên theo Z
		foreach (var e in list)
		{
			switch (e.Kind)
			{
				case EnemyTypes.Slime: DrawSlime(e); break;
				case EnemyTypes.Bat: DrawBat(e); break;
				case EnemyTypes.Bomber: DrawBomber(e); break;
				case EnemyTypes.Boss: DrawBoss(e); break;
				case EnemyTypes.MiniEye: DrawMiniEye(e); break;
				case EnemyTypes.Brute when _brute != null: DrawBrute(e); break;
				default: DrawPlaceholder(e); break;
			}
		}
		_c.ResetTransform();
	}

	private float ShadowDown(Enemy e) => e.Kind switch
	{
		EnemyTypes.MiniEye => 24f,   // lơ lửng cao hơn mặt đất
		EnemyTypes.Slime => _slime.ShadowDown * e.Size,
		EnemyTypes.Bat => _bat.ShadowDown * e.Size,
		EnemyTypes.Bomber => _bomber.ShadowDown * e.Size,
		EnemyTypes.Brute when _brute != null => _brute.ShadowDown * e.Size,
		_ => e.Radius * 0.7f, // Radius đã bao gồm Size
	};

	/// <summary>Vẽ một frame của sprite, nhấc lên theo Z, rung nhẹ khi đang khựng, lật theo hướng mặt.</summary>
	private void DrawFrame(Enemy e, SpriteSet s, int frame, float scale = 1f, float lift = 0f, Rgba? tint = null)
	{
		// Rung nhẹ khi đang khựng (hoặc đang gồng) cho cảm giác "va mạnh"
		float shake = (e.HitStop > 0f || (e.Kind == EnemyTypes.Slime && e.PounceState == 1)) ? (_w.Frame % 2 == 0 ? 1.5f : -1.5f) : 0f;

		// Quay mặt về phía người chơi (đang bay thì theo hướng bay)
		float faceX = e.Airborne ? e.Vel.X : e.FaceX;
		float flip = (faceX > 0f) == s.FacesLeft ? -1f : 1f;

		float k = scale * e.Size; // độ lớn của quái nhân vào hình vẽ
		// Vẽ thẳng bằng toạ độ thế giới (không SetTransform): đặt tâm thân sprite tại vị trí quái, co giãn theo độ lớn, lật theo hướng mặt
		var tex = s.Tex[Mx.Clamp(e.Variant, 0, s.Tex.Length - 1)];
		Vector2 at = e.Pos + new Vector2(shake, -e.Z - lift * e.Size);
		if (frame < s.Trim.Length)
		{
			// Chỉ vẽ phần có hình của khung (nhỏ hơn nhiều so với cả ô 64x64): điểm gốc dời theo góc của hộp bao
			var t = s.Trim[frame];
			if (t.IsEmpty) return;
			_c.DrawSpriteDirect(tex, new Rect2(frame * FrameSize + t.X, t.Y, t.Width, t.Height), at, s.Center - new Vector2(t.X, t.Y), flip * k, k, tint);
		}
		else
			_c.DrawSpriteDirect(tex, new Rect2(frame * FrameSize, 0, FrameSize, FrameSize), at, s.Center, flip * k, k, tint);
	}

	private void DrawSlime(Enemy e)
	{
		int frame;
		if (e.Flash > 0f) frame = 15;                              // nháy trắng khi trúng đòn
		else if (e.Dead) frame = 17 + Math.Min(e.Bounces, 3);      // mắt X -> bẹp dần theo mỗi lần nảy
		else if (e.Airborne) frame = e.Vz > 0f ? 8 : 9;            // bay lên / rơi xuống
		else if (e.PounceState == 1) frame = 5;                    // gồng: ép xuống
		else if (e.PounceState == 2) frame = e.PounceTimer / PounceTime < 0.5f ? 8 : 9; // vồ: bay lên / lao xuống
		else if (e.PounceState == 3) frame = 5;                    // khựng sau khi vồ
		else if (e.LandTimer > 0f) frame = 5;                      // bẹp lúc vừa chạm đất
		else frame = (int)(e.AnimTime * 10f) % 7;                  // nhún đi
		// (Màu sprite dành riêng cho biến thể sức mạnh; quái đặc biệt nhận biết bằng vòng đỏ dưới chân)
		DrawFrame(e, _slime, frame, 1f, PounceLift(e));
	}

	private void DrawBat(Enemy e)
	{
		int frame;
		if (e.Flash > 0f) frame = 18;                              // nháy trắng
		else if (e.Dead) frame = e.Airborne ? 21 : 22;             // ngã -> nằm bẹp
		else if (e.Airborne) frame = 21;                           // bị văng: chao đảo
		else if (e.ScreechTime >= 0f) frame = ScreechFrame(e.ScreechTime);
		else frame = (int)(e.AnimTime * 12f) % 8;                  // vỗ cánh

		// Nhấp nhô lên xuống khi lơ lửng (bóng vẫn ở mặt đất)
		float bob = (e.Dead || e.Airborne) ? 0f : 2.5f * MathF.Sin(e.AnimTime * 6f + e.Phase);
		DrawFrame(e, _bat, frame, 1f, bob);
	}

	/// <summary>Độ cao vòng cung (đơn vị chưa nhân Size) khi slime đang vồ: lên rồi xuống theo parabol.</summary>
	private static float PounceLift(Enemy e)
	{
		if (e.PounceState != 2) return 0f;
		return PounceHeight * MathF.Sin(Mx.Clamp(e.PounceTimer / PounceTime, 0f, 1f) * Mx.Pi);
	}

	/// <summary>Báo hiệu hướng vồ của slime đặc biệt: dải cam đậm dần trong lúc gồng, kết thúc bằng mũi tên.</summary>
	private void DrawPounceTelegraph(Enemy e)
	{
		float k = Mx.Clamp(e.PounceTimer / PounceWindup, 0f, 1f);
		float len = Mx.Clamp((_w.Player.Pos - e.Pos).Length() + 40f, 90f, PounceDistance);
		Vector2 d = e.PounceDir;
		Vector2 n = new Vector2(-d.Y, d.X) * MathF.Max(e.Radius * 0.7f, 6f);
		Vector2 a = e.Pos, b = e.Pos + d * len;
		_c.ResetTransform();
		_c.DrawPolygon(new[] { a + n, b + n, b - n, a - n }, new Rgba(1f, 0.55f, 0.1f, 0.10f + 0.28f * k));
		var edge = new Rgba(1f, 0.6f, 0.15f, 0.35f + 0.55f * k);
		_c.DrawLine(a + n, b + n, edge, 1.5f);
		_c.DrawLine(a - n, b - n, edge, 1.5f);
		_c.DrawPolygon(new[] { b + d * 14f, b + n * 1.8f, b - n * 1.8f }, edge); // mũi tên chỉ điểm đáp
	}

	/// <summary>Báo hiệu đường lao của Brute: dải đỏ đậm dần trong lúc gồng (nửa sau đã khoá hướng).</summary>
	private void DrawChargeTelegraph(Enemy e)
	{
		float k = 1f - e.ChargeTimer / BruteWindup;
		Vector2 a = e.Pos;
		Vector2 b = a + e.ChargeDir * BruteTelegraphLen;
		Vector2 n = e.ChargeDir.Rotated(Mx.Pi / 2f) * e.Radius;
		_c.ResetTransform();
		_c.DrawPolygon(new[] { a + n, b + n, b - n, a - n }, new Rgba(1f, 0.1f, 0.1f, 0.14f + 0.32f * k));
		var edge = new Rgba(1f, 0.15f, 0.1f, 0.5f + 0.5f * k);
		_c.DrawLine(a + n, b + n, edge, 1.5f);
		_c.DrawLine(a - n, b - n, edge, 1.5f);
	}

	/// <summary>Frame gào: 8-9 há miệng (báo hiệu) · 10-13 sóng âm · 14-17 thu lại.</summary>
	private static int ScreechFrame(float t)
	{
		if (t < ScreechWindup) return 8 + (t < ScreechWindup * 0.5f ? 0 : 1);
		if (t < ScreechWindup + ScreechWave)
			return 10 + Mx.Clamp((int)((t - ScreechWindup) / ScreechWave * 4f), 0, 3);
		return 14 + Mx.Clamp((int)((t - ScreechWindup - ScreechWave) / ScreechRecover * 4f), 0, 3);
	}

	/// <summary>
	/// Vùng phễu 2 bên (trái/phải) của tiếng gào, đúng bằng vùng tính sát thương.
	/// Windup: viền mờ báo hiệu · Wave: sóng lan ra và tô đậm · Recover: mờ dần.
	/// </summary>
	private void DrawScreechCones(Enemy e)
	{
		float t = e.ScreechTime;
		float range = ScreechRange * e.Size;
		float fill, edge;
		if (t < ScreechWindup)
		{
			float k = t / ScreechWindup;
			fill = 0.04f + 0.06f * k;
			edge = 0.25f + 0.35f * k;
		}
		else if (t < ScreechWindup + ScreechWave)
		{
			float k = (t - ScreechWindup) / ScreechWave;
			range *= 0.35f + 0.65f * k; // sóng lan dần ra
			fill = 0.28f;
			edge = 0.9f;
		}
		else
		{
			float k = (t - ScreechWindup - ScreechWave) / ScreechRecover;
			fill = 0.28f * (1f - k);
			edge = 0.9f * (1f - k);
		}

		_c.ResetTransform();
		Vector2 c = e.Pos + new Vector2(0f, -e.Z);
		Rgba fillCol = new(0.7f, 0.85f, 1f, fill);
		Rgba edgeCol = new(0.85f, 0.95f, 1f, edge);
		for (int side = 0; side < 2; side++)
		{
			float center = side == 0 ? 0f : Mx.Pi; // phải, trái
			const int steps = 10;
			var pts = new Vector2[steps + 2];
			pts[0] = c;
			for (int i = 0; i <= steps; i++)
				pts[i + 1] = c + V.FromAngle(center - ScreechHalfAngle + 2f * ScreechHalfAngle * i / steps) * range;
			_c.DrawPolygon(pts, fillCol);
			_c.DrawPolyline(new[] { pts[1], c, pts[steps + 1] }, edgeCol, 1.5f); // 2 cạnh bên của phễu
			_c.DrawArc(c, range, center - ScreechHalfAngle, center + ScreechHalfAngle, edgeCol, 1.5f);
		}
	}

	// bomber.png: 0-3 đi · 4 hoảng · 5-12 nhấp nháy ngòi · 13-15 HIỆU ỨNG NỔ (nhỏ -> lớn) · 16 trắng hoàn toàn (khi bị đánh) · 17-20 chết
	private const int BombHitFlashFrame = 16;
	private const int BombBlastFirst = 13, BombBlastFrames = 3;

	/// <summary>Hiệu ứng nổ: 3 khung lửa phóng to sao cho khung lớn nhất rộng đúng bằng đường kính vùng nổ; đáy vòm lửa nằm ngay tại chỗ nổ.</summary>
	private void DrawBlast(Enemy e)
	{
		float progress = 1f - e.Linger / BombAnim;
		int frame = BombBlastFirst + Mx.Clamp((int)(progress * BombBlastFrames), 0, BombBlastFrames - 1);
		var t = _bomber.Trim[frame];
		if (t.IsEmpty) return;
		float widest = _bomber.Trim[BombBlastFirst + BombBlastFrames - 1].Width;      // khung nổ lớn nhất
		float k = 2f * BombRadius * e.Size / MathF.Max(widest, 1f);
		var tex = _bomber.Tex[Mx.Clamp(e.Variant, 0, _bomber.Tex.Length - 1)];
		// Điểm neo: giữa đáy đống lửa (cột 31, hàng 42 của khung), dời theo hộp bao đã cắt
		_c.DrawSpriteDirect(tex, new Rect2(frame * FrameSize + t.X, t.Y, t.Width, t.Height), e.Pos, new Vector2(31f - t.X, 42f - t.Y), k, k);
	}

	/// <summary>brute.png: 0 gồng/lao (cầm khiên) · 1-2 khiên rời · 3,5,6,7 đi · 4 trắng · 8 kiệt sức/chết/bị văng.</summary>
	private const int BruteFrameWindup = 0, BruteFrameHit = 5, BruteFrameTired = 9, BruteShieldA = 1, BruteShieldB = 2;
	private static readonly int[] BruteWalk = { 3, 6, 7, 8 };

	private void DrawBrute(Enemy e)
	{
		bool shield = e.ShieldHp > 0f;
		int frame;
		bool walking = false;
		if (e.Flash > 0f) frame = BruteFrameHit;                       // trúng đòn (khi đã lộ máu): nháy trắng
		else if (e.Dead || e.Airborne || e.ChargeState == 3) frame = BruteFrameTired; // chết / bị văng / kiệt sức sau khi lao: gục xuống
		else if (e.ChargeState == 1 || e.ChargeState == 2) frame = BruteFrameWindup; // gồng / lao: nghiêng người, cầm khiên chắn phía trước
		else
		{
			int w = (int)(e.AnimTime * 6f) % BruteWalk.Length;
			frame = BruteWalk[w];
			walking = true;
			DrawFrame(e, _brute, frame);
			if (shield) DrawBruteShield(e, (w & 1) == 0 ? BruteShieldA : BruteShieldB); // còn khiên: vẽ khiên rời (nhấp nhô) đè lên tay
		}
		if (!walking) DrawFrame(e, _brute, frame);
		if (!e.Dead && (shield || (e.ShieldBreakT >= 0f && e.ShieldBreakT < ShieldBreakTime))) DrawShieldFx(e);
	}

	/// <summary>Khiên rời: lấy từ khung 1-2 của brute.png; nếu 2 khung đó TRỐNG (chưa vẽ khiên) thì vẽ khiên tạm bằng ô vuông để vẫn nhìn ra brute đang có khiên.</summary>
	private void DrawBruteShield(Enemy e, int frame)
	{
		if (!_brute.Trim[frame].IsEmpty) { DrawFrame(e, _brute, frame); return; }
		float faceX = e.Airborne ? e.Vel.X : e.FaceX;
		float side = faceX > 0f ? 1f : -1f, k = e.Size;
		Vector2 c = e.Pos + new Vector2(side * 14.5f * k, 3f * k - e.Z - (frame == BruteShieldB ? 1f : 0f));
		_c.ResetTransform();
		float x = MathF.Round(c.X - 5f * k), y = MathF.Round(c.Y - 7f * k), w = MathF.Round(10f * k), h = MathF.Round(14f * k);
		_c.FillRect(x - 1f, y - 1f, w + 2f, h + 2f, new Rgba(0.05f, 0.05f, 0.08f, 1f));
		_c.FillRect(x, y, w, h, new Rgba(0.62f, 0.68f, 0.78f, 1f));
		_c.FillRect(MathF.Round(c.X - k), y, MathF.Max(2f, MathF.Round(2f * k)), h, new Rgba(0.98f, 0.85f, 0.2f, 1f));
		_c.FillRect(x, MathF.Round(c.Y - k), w, MathF.Max(2f, MathF.Round(2f * k)), new Rgba(0.98f, 0.85f, 0.2f, 1f));
	}

	/// <summary>Hiệu ứng khiên: quầng sáng khi vừa bị đánh, thanh độ bền nhỏ trên đầu, và mảnh khiên bay ra khi vỡ.</summary>
	private void DrawShieldFx(Enemy e)
	{
		float faceX = e.Airborne ? e.Vel.X : e.FaceX;
		float side = faceX > 0f ? 1f : -1f;                            // sprite nhìn trái: khiên nằm phía trước mặt
		float k = e.Size;
		Vector2 sp = e.Pos + new Vector2(side * 14.5f * k, 3f * k - e.Z);
		_c.ResetTransform();
		if (e.ShieldHp > 0f)
		{
			if (e.ShieldFlash > 0f)
				_c.DrawArc(sp, 9f * k, 0f, Mx.Tau, new Rgba(1f, 1f, 0.8f, Mx.Clamp(e.ShieldFlash / 0.12f, 0f, 1f)), 2f);
			if (e.ShieldHp < e.ShieldMax)                              // thanh độ bền khiên (chỉ hiện khi đã bị đánh)
			{
				float w = 20f * k, y = e.Pos.Y - e.Z - 34f * k, x = e.Pos.X - w / 2f;
				_c.FillRect(x - 1f, y - 1f, w + 2f, 4f, new Rgba(0f, 0f, 0f, 0.7f));
				_c.FillRect(x, y, w * e.ShieldHp / e.ShieldMax, 2f, new Rgba(0.65f, 0.8f, 1f, 1f));
			}
		}
		else
		{
			float t = e.ShieldBreakT / ShieldBreakTime, a = 1f - t;      // mảnh khiên văng ra rồi mờ dần
			for (int i = 0; i < 6; i++)
			{
				float ang = i * Mx.Tau / 6f + 0.4f;
				Vector2 p = sp + V.FromAngle(ang) * (4f + 22f * t) * k + new Vector2(0f, 12f * t * t * k);
				_c.FillRect(MathF.Round(p.X) - 1f, MathF.Round(p.Y) - 1f, 3f, 3f, i % 2 == 0 ? new Rgba(0.75f, 0.8f, 0.9f, a) : new Rgba(1f, 0.85f, 0.2f, a));
			}
		}
	}

	private void DrawBomber(Enemy e)
	{
		if (e.Exploded)
		{
			DrawBlast(e); // khung 13-15, phóng theo bán kính nổ (khung 16 là frame TRẮNG lúc bị đánh, không thuộc hiệu ứng nổ)
			return;
		}

		int f;
		if (e.Flash > 0f) f = BombHitFlashFrame;                   // bị đánh: nháy TRẮNG hoàn toàn (khung 16)
		else if (e.Dead) f = 17 + Math.Min(e.Bounces, 3);          // xẹp dần
		else if (e.Airborne) f = 4;                                // bị văng: hoảng
		else if (e.FuseTime >= 0f) f = 5 + (int)(e.FuseElapsed * 14f) % 8; // ngòi nhấp nháy trắng/đỏ/đen
		else f = (int)(e.AnimTime * 8f) % 4;                       // lạch bạch đi
		DrawFrame(e, _bomber, f);
	}

	// ---------------------------------------------------------------- mắt con (giai đoạn 2 của boss)
	// small eye ball.aseprite -> Assets/enemies/minieye.png: dải 6 khung 64x64, con mắt NHÌN SANG TRÁI, vẽ đúng cỡ gốc (1x): khung 1 = TRẮNG hoàn toàn (bị đánh), khung 0,2,3,4,5 = đuôi bay phấp phới
	// plasma-eyeball skill.aseprite -> Assets/enemies/plasma.png: một khung chứa dải plasma nằm NGANG; game tự cắt sát viền, dùng làm chưởng và chùm tia chữ thập
	private SpriteSet _mini;
	private const int MiniFrameHit = 1;
	private static readonly int[] MiniFly = { 0, 2, 3, 4, 5 };
	private Bitmap _plasma;
	private bool _miniChecked;

	private void EnsureMiniAssets()
	{
		if (_miniChecked) return;
		_miniChecked = true;
		try
		{
			if (File.Exists(AssetStore.PathOf("enemies/minieye.png")))
				_mini = Load("enemies/minieye.png", new Vector2(33f, 24f), faceLeft: true, shadowDown: 24f); // tâm (33,24) = tâm thân; con mắt ở (21.5,23.5)
			var raw = AssetStore.OptionalImage("enemies/plasma.png");
			if (raw != null) _plasma = EquipmentAssets.CropToContent(raw);
		}
		catch (Exception ex) { AssetStore.Report("enemies/minieye.png / plasma.png", ex.Message); }
	}

	private void DrawMiniEye(Enemy e)
	{
		EnsureMiniAssets();
		if (_mini == null) { DrawPlaceholder(e); return; }
		var m = e.Mini;
		float alpha = e.Dead ? Mx.Clamp(e.Linger / 0.45f, 0f, 1f) : 1f;
		Rgba tint = new Rgba(1f, 1f, 1f, alpha);
		if (m != null && m.Windup >= 0f && !e.Dead) tint = _w.Frame % 8 < 4 ? new Rgba(1f, 0.5f, 0.5f, alpha) : tint; // gồng bắn: nhấp nháy đỏ
		int frame = MiniFly[(int)(e.AnimTime * 10f) % MiniFly.Length];
		if (e.Flash > 0f) { frame = MiniFrameHit; tint = new Rgba(1f, 1f, 1f, alpha); }   // bị đánh: nháy TRẮNG hoàn toàn (khung 1)
		float bob = MiniEyes.Bob(e);

		// Thân XOAY để con mắt luôn nhìn về hướng đang quan tâm (mèo / hướng ngắm / hướng chùm tia). Sprite gốc nhìn TRÁI:
		// hướng nhìn sang trái -> xoay (heading - 180°) để đuôi không bị lộn ngược; sang phải -> lật ngang rồi xoay theo heading.
		float th = m?.Heading ?? Mx.Pi;
		bool right = MathF.Cos(th) > 0f;
		var tex = _mini.Tex[0];
		_c.SetTransform(e.Pos + new Vector2(0f, -bob), right ? th : th - Mx.Pi, new Vector2(right ? -1f : 1f, 1f));
		_c.DrawTexture(tex, new Rect2(-_mini.Center.X, -_mini.Center.Y, FrameSize, FrameSize), new Rect2(frame * FrameSize, 0f, FrameSize, FrameSize), tint);
		_c.ResetTransform();
	}

	/// <summary>Hiệu ứng giai đoạn 2-3 vẽ TRÊN quái: dây nối mắt con-mắt chính, khiên bất khả xâm phạm, vạch báo bắn, chưởng, chùm tia chữ thập (mỗi mắt chính có khiên / chữ thập riêng).</summary>
	public void DrawEyeEffects(Canvas c)
	{
		if (_w.Arena.Boss == null) return;
		_c = c;
		c.ResetTransform();
		EnsureMiniAssets();

		foreach (var boss in Enemies)
		{
			if (boss.Kind != EnemyTypes.Boss || boss.Boss == null || boss.Dead || !boss.Boss.ShieldUp) continue;
			// Khiên quanh mắt chính + dây nối tới các mắt con còn sống CỦA NÓ
			var bd = boss.Boss;
			float pulse = 0.5f + 0.5f * MathF.Sin(Environment.TickCount64 / 1000f * 5f);
			float flash = Mx.Clamp(bd.ShieldFlash / 0.12f, 0f, 1f);
			float r = boss.Radius * 1.45f;
			c.DrawCircle(boss.Pos, r, new Rgba(0.4f, 0.7f, 1f, 0.10f + 0.10f * pulse + 0.35f * flash));
			c.DrawArc(boss.Pos, r, 0f, Mx.Tau, new Rgba(0.6f, 0.85f, 1f, 0.6f + 0.3f * pulse + 0.1f * flash), 3f);
			foreach (var e in Enemies)
				if (e.Kind == EnemyTypes.MiniEye && !e.Dead && e.Mini?.Owner == boss)
					c.DrawLine(boss.Pos, e.Pos + new Vector2(0f, -8f), new Rgba(0.55f, 0.8f, 1f, 0.22f + 0.15f * pulse), 1f);
		}

		// Mắt con: vạch báo hiệu (Dễ / Trung bình) + quả cầu plasma dồn tụ ở con mắt khi gồng + chớp sáng ở nòng khi vừa bắn
		foreach (var e in Enemies)
		{
			if (e.Kind != EnemyTypes.MiniEye || e.Dead || e.Mini == null) continue;
			var m = e.Mini;
			Vector2 mz = MiniEyes.Muzzle(e);
			if (m.Windup >= 0f)
			{
				float k = Mx.Clamp(m.Windup / 0.6f, 0f, 1f);
				if (Difficulty.ShowEnemyTelegraphs)
					c.DrawLine(mz, mz + m.Aim * 240f, new Rgba(1f, 0.3f, 0.25f, 0.15f + 0.45f * k), 1f + 1.5f * k);
				DrawPlasmaOrb(c, mz, 2f + 7f * k, 0.5f + 0.5f * k);
			}
			else if (m.FlashLeft > 0f)
				DrawPlasmaOrb(c, mz, 9f * (m.FlashLeft / 0.14f) + 3f, m.FlashLeft / 0.14f);
		}

		// Chùm tia chữ thập (mỗi mắt chính một cái, từ 4 mắt con của nó)
		foreach (var boss in Enemies)
		{
			if (boss.Kind != EnemyTypes.Boss || boss.Boss == null || boss.Dead || boss.Boss.CrossState <= 0) continue;
			var bd = boss.Boss;
			for (int i = 0; i < MiniEyes.Count; i++)
			{
				var (a, b) = MiniEyes.Beam(boss, i);
				Vector2 dir = (b - a).Normalized();
				a -= dir * 8f;   // Beam() trả điểm ngay tại con mắt; phần vẽ dịch ra sau 8px rồi cộng lại bên dưới để quả cầu nằm gọn ở mắt
				if (bd.CrossState == 1)
				{
					float k = Mx.Clamp(bd.CrossTimer / MiniEyes.CrossWindup, 0f, 1f);
					if (Difficulty.ShowEnemyTelegraphs)
						c.DrawLine(a, b, new Rgba(1f, 0.3f, 0.3f, 0.15f + 0.45f * k), 1.5f + 3f * k);
					DrawPlasmaOrb(c, a + dir * 8f, 3f + 9f * k, 0.4f + 0.6f * k);   // mỗi mắt con dồn plasma ở con mắt trước khi phóng
				}
				else
				{
					// Chùm tia mọc ra từ mắt con (rồi dài ra trong 0,25s đầu), có quả cầu sáng ở gốc
					float grow = Mx.Clamp(bd.CrossTimer / 0.25f, 0.1f, 1f);
					DrawBeam(c, a + dir * 8f, a + dir * (8f + (MiniEyes.BeamLength - 8f) * grow), MiniEyes.BeamWidth);
					DrawPlasmaOrb(c, a + dir * 8f, 10f + 2f * MathF.Sin(Environment.TickCount64 / 1000f * 30f), 1f);
				}
			}
		}
		// Chưởng
		foreach (var bolt in Bolts)
		{
			Vector2 tail = bolt.Pos - bolt.Dir * bolt.Length;
			DrawBolt(c, tail, bolt.Pos);
			DrawPlasmaOrb(c, bolt.Pos, 6f, 0.9f);   // đầu viên chưởng sáng rực
		}
		c.ResetTransform();
	}

	/// <summary>Quả cầu plasma (halo xanh + lõi trắng): dùng cho đầu chưởng, chỗ dồn năng lượng và chớp ở nòng.</summary>
	private static void DrawPlasmaOrb(Canvas c, Vector2 p, float r, float strength)
	{
		strength = Mx.Clamp(strength, 0f, 1f);
		c.DrawCircle(p, r * 1.7f, new Rgba(0.35f, 0.55f, 1f, 0.22f * strength));
		c.DrawCircle(p, r, new Rgba(0.45f, 0.7f, 1f, 0.55f * strength));
		c.DrawCircle(p, r * 0.55f, new Rgba(1f, 1f, 1f, 0.95f * strength));
	}

	/// <summary>Viên chưởng: dải plasma mờ dần về phía đuôi (3 đoạn, độ đậm 50% / 80% / 100%).</summary>
	private void DrawBolt(Canvas c, Vector2 tail, Vector2 head)
	{
		Vector2 d = head - tail;
		float len = d.Length();
		if (len < 1f) return;
		float thick = MiniEyes.BoltThickness;
		if (_plasma == null) { DrawBeam(c, tail, head, thick); return; }
		c.SetTransform(tail, MathF.Atan2(d.Y, d.X), Vector2.One);
		float[] alpha = { 0.5f, 0.8f, 1f };
		for (int i = 0; i < 3; i++)
		{
			float sx = _plasma.Width * i / 3f, sw = _plasma.Width / 3f;
			c.DrawTexture(_plasma, new Rect2(len * i / 3f, -thick / 2f, len / 3f + 0.5f, thick), new Rect2(sx, 0f, sw, _plasma.Height), new Rgba(1f, 1f, 1f, alpha[i]));
		}
		c.ResetTransform();
	}

	/// <summary>Vẽ dải plasma từ a tới b (kéo giãn ảnh plasma theo chiều dài, xoay theo hướng); chưa có ảnh thì vẽ hình tạm.</summary>
	private void DrawBeam(Canvas c, Vector2 a, Vector2 b, float width)
	{
		Vector2 d = b - a;
		float len = d.Length();
		if (len < 1f) return;
		if (_plasma != null)
		{
			c.SetTransform(a, MathF.Atan2(d.Y, d.X), Vector2.One);
			c.DrawTexture(_plasma, new Rect2(0f, -width / 2f, len, width));
			c.ResetTransform();
		}
		else
		{
			c.DrawLine(a, b, new Rgba(0.35f, 0.5f, 1f, 0.9f), width);
			c.DrawLine(a, b, new Rgba(1f, 1f, 1f, 0.95f), width * 0.4f);
		}
	}

	// ---------------------------------------------------------------- boss
	// Chưa có sprite boss -> vẽ hình tạm (thân tròn lớn, sừng, gai lưng, mắt đỏ). Khi có sprite, đặt file này vào Assets là tự dùng:
	private const string BossSpritePath = "enemies/boss.png";
	// EyeBoss (từ EyeBoss.aseprite): dải 4 khung 64x64, con mắt nằm giữa khung (đường kính ~32px); vẽ phóng đúng 2 lần cho pixel đều
	private const string BossEyePath = "enemies/eyeboss.png";
	private const float BossEyeScale = 2f;
	private Bitmap _bossEye;
	private Bitmap _bossTex;

	// 4 khung vẽ tay: 0 nhìn thẳng · 1 nhìn TRÁI · 2 nhìn LÊN · 3 nhìn TRÁI-LÊN. Các hướng còn lại = lật ngang/dọc.
	// Chỉ số hướng 0..7 đi theo chiều kim đồng hồ trên màn hình, bắt đầu từ PHẢI.
	private static readonly (int frame, bool flipX, bool flipY)[] EyeLook =
	{
		(1, true,  false),  // 0 phải
		(3, true,  true),   // 1 phải-xuống
		(2, false, true),   // 2 xuống
		(3, false, true),   // 3 trái-xuống
		(1, false, false),  // 4 trái
		(3, false, false),  // 5 trái-lên
		(2, false, false),  // 6 lên
		(3, true,  false),  // 7 phải-lên
	};

	/// <summary>Chọn hướng nhìn 0..7 theo vector `gaze`; có trễ nhẹ (chỉ đổi khi lệch hẳn sang ô khác) để con ngươi không nhấp nháy ở ranh giới.</summary>
	private static int PickLook(BossData b, Vector2 gaze)
	{
		float sector = Mx.Tau / 8f;
		float a = Mx.Wrap(MathF.Atan2(gaze.Y, gaze.X), 0f, Mx.Tau);
		if (b.Look >= 0)
		{
			float diff = MathF.Abs(Mx.Wrap(a - b.Look * sector, -Mx.Pi, Mx.Pi));
			if (diff <= sector * 0.5f + Mx.DegToRad(8f)) return b.Look;
		}
		return b.Look = (int)MathF.Round(a / sector) % 8;
	}
	private bool _bossTexChecked;

	/// <summary>Vùng báo hiệu dưới chân boss: dải lao, vòng giậm, các vòng sóng xung kích, hào quang nổi điên.</summary>
	private void DrawBossGround(Enemy e)
	{
		var b = e.Boss;
		_c.ResetTransform();

		bool zones = Difficulty.ShowEnemyTelegraphs; // Khó trở lên: bỏ dải báo lao + vòng báo giậm (sóng xung kích và hào quang nổi điên vẫn hiện)
		if (zones && b.State == 2) // gồng lao: dải đỏ về phía hướng lao
		{
			float k = Mx.Clamp(b.Timer / BossAI.ChargeWindupValue, 0f, 1f);
			Vector2 d = b.ChargeDir;
			Vector2 n = new Vector2(-d.Y, d.X) * e.Radius;
			Vector2 a = e.Pos, far = e.Pos + d * 520f;
			_c.DrawPolygon(new[] { a + n, far + n, far - n, a - n }, new Rgba(1f, 0.1f, 0.1f, 0.10f + 0.30f * k));
			var edge = new Rgba(1f, 0.2f, 0.1f, 0.4f + 0.5f * k);
			_c.DrawLine(a + n, far + n, edge, 2f);
			_c.DrawLine(a - n, far - n, edge, 2f);
		}
		else if (zones && b.State == 5) // gồng giậm: vòng đỏ to dần quanh chân
		{
			float k = Mx.Clamp(b.Timer / BossAI.SlamWindupValue, 0f, 1f);
			_c.DrawCircle(e.Pos, BossAI.SlamRadiusValue, new Rgba(1f, 0.15f, 0.1f, 0.10f + 0.25f * k));
			_c.DrawArc(e.Pos, BossAI.SlamRadiusValue * (0.4f + 0.6f * k), 0f, Mx.Tau, new Rgba(1f, 0.3f, 0.15f, 0.9f), 3f);
		}

		// Sóng xung kích lan ra (né bằng DASH xuyên qua vòng)
		foreach (var ring in b.Rings)
		{
			float a = 1f - 0.6f * ring.R / BossAI.RingMaxValue;
			_c.DrawArc(ring.Center, ring.R, 0f, Mx.Tau, new Rgba(1f, 0.55f, 0.15f, a), 7f);
			_c.DrawArc(ring.Center, MathF.Max(ring.R - 5f, 1f), 0f, Mx.Tau, new Rgba(1f, 0.95f, 0.8f, 0.6f * a), 2f);
		}

		if (b.Phase >= 3) // nổi điên: hào quang đỏ nhấp nháy
			_c.DrawArc(e.Pos, e.Radius * 1.35f + 3f * MathF.Sin(e.AnimTime * 8f), 0f, Mx.Tau, new Rgba(1f, 0.1f, 0.1f, 0.55f), 3f);
	}

	private void DrawBoss(Enemy e)
	{
		var b = e.Boss;
		if (!_bossTexChecked)
		{
			_bossTexChecked = true;
			_bossTex = AssetStore.OptionalImage(BossSpritePath);
			_bossEye = AssetStore.OptionalImage(BossEyePath);
		}

		float t = e.AnimTime;
		float alpha = b.State == 0 ? Mx.Clamp(b.Timer / 1.2f, 0.15f, 1f) : e.Dead ? Mx.Clamp(e.Linger / 1.5f, 0f, 1f) : 1f;
		float lift = b.State == 5 ? 30f * MathF.Sin(Mx.Clamp(b.Timer / BossAI.SlamWindupValue, 0f, 1f) * Mx.Pi * 0.5f) : 0f;
		float sx = 1f, sy = 1f;
		float shake = 0f;
		switch (b.State)
		{
			case 2: shake = _w.Frame % 2 == 0 ? 2f : -2f; sx = 1.08f; sy = 0.93f; break; // gồng lao: rung + ép xuống
			case 3: sx = 1.12f; sy = 0.90f; break;                                        // đang lao
			case 6: sx = 1.15f; sy = 0.85f; break;                                        // vừa giậm xong
			case 7: sx = sy = 1f + 0.06f * MathF.Sin(t * 35f); break;                     // gầm
			default: sy = 1f + 0.025f * MathF.Sin(t * 4f); break;                         // thở
		}
		float face = e.FaceX >= 0f ? 1f : -1f;
		float r = e.Radius;

		Rgba normal = new(0.16f, 0.12f, 0.14f);
		Rgba body = e.Flash > 0f ? Rgba.White
			: b.State == 2 ? normal.Lerp(new Rgba(0.8f, 0.15f, 0.1f), 0.5f)
			: b.State == 4 ? new Rgba(0.45f, 0.45f, 0.5f)                // kiệt sức: xám
			: b.Phase >= 3 ? new Rgba(0.28f, 0.1f, 0.12f)                // nổi điên: đỏ sẫm
			: normal;
		body = body.WithAlpha(alpha);

		_c.SetTransform(e.Pos + new Vector2(shake, -lift), e.Dead ? 0.25f * face : 0f, new Vector2(sx, sy));

		if (_bossEye != null)
		{
			// Con mắt luôn nhìn theo VỊ TRÍ mèo (8 hướng từ 4 khung vẽ tay, lật ngang/dọc), trừ lúc đang lao mình tới: nhìn theo hướng lao.
			// Kiệt sức: nhìn thẳng (khung 0) và xám đi; gồng lao / nổi điên ửng đỏ.
			Vector2 gaze = b.State == 3 ? b.ChargeDir : _w.Player.Pos - e.Pos;
			int look = b.State == 4 || gaze.LengthSquared() < 12f * 12f ? -1 : PickLook(b, gaze);
			var (frame, flipX, flipY) = look < 0 ? (0, false, false) : EyeLook[look];
			float d = FrameSize * BossEyeScale;
			Rgba tint = e.Flash > 0f ? new Rgba(1f, 0.6f, 0.6f)
				: b.State == 4 ? new Rgba(0.6f, 0.6f, 0.7f)
				: b.State == 2 ? new Rgba(1f, 0.65f, 0.65f)
				: b.Phase >= 3 ? new Rgba(1f, 0.6f, 0.6f)
				: Rgba.White;
			var dest = new Rect2(flipX ? d / 2f : -d / 2f, flipY ? d / 2f : -d / 2f, flipX ? -d : d, flipY ? -d : d);
			_c.DrawTexture(_bossEye, dest, new Rect2(frame * FrameSize, 0f, FrameSize, FrameSize), tint.WithAlpha(alpha));
		}
		else if (_bossTex != null)
		{
			float w = _bossTex.Width, h = _bossTex.Height;
			_c.DrawTexture(_bossTex, new Rect2(w / 2f * face, -h / 2f, -w * face, h), new Rgba(1f, 1f, 1f, alpha));
		}
		else
		{
			Rgba dark = new(0.08f, 0.05f, 0.07f, alpha);
			// Gai trên lưng
			for (int i = 0; i < 5; i++)
			{
				float ang = -Mx.Pi * 0.9f + i * (Mx.Pi * 0.8f / 4f);
				Vector2 dirv = V.FromAngle(ang);
				Vector2 perp = new(-dirv.Y, dirv.X);
				_c.DrawPolygon(new[] { dirv * r * 0.95f + perp * r * 0.16f, dirv * r * 1.32f, dirv * r * 0.95f - perp * r * 0.16f }, new Rgba(0.35f, 0.38f, 0.45f, alpha));
			}
			_c.DrawCircle(Vector2.Zero, r, body);                                       // thân
			_c.DrawArc(Vector2.Zero, r, 0f, Mx.Tau, dark, 3f);                          // viền
			// Sừng phía trước
			_c.DrawPolygon(new[]
			{
				new Vector2(face * r * 0.55f, -r * 0.05f), new Vector2(face * r * 1.30f, -r * 0.70f), new Vector2(face * r * 0.85f, -r * 0.45f),
			}, new Rgba(0.85f, 0.82f, 0.75f, alpha));
			// Mắt đỏ
			Rgba eye = b.Phase >= 3 ? new Rgba(1f, 0.9f, 0.2f, alpha) : new Rgba(0.95f, 0.15f, 0.12f, alpha);
			_c.DrawCircle(new Vector2(face * r * 0.40f, -r * 0.22f), r * 0.13f, eye);
			_c.DrawCircle(new Vector2(face * r * 0.05f, -r * 0.30f), r * 0.10f, eye);
		}
		_c.ResetTransform();

		if (b.State == 4 && !e.Dead) // choáng: sao quay trên đầu
			for (int i = 0; i < 3; i++)
			{
				float ang = t * 4f + i * Mx.Tau / 3f;
				_c.DrawCircle(e.Pos + new Vector2(MathF.Cos(ang) * 22f, -r - 12f + MathF.Sin(ang) * 5f), 3f, new Rgba(1f, 0.9f, 0.2f));
			}
	}

	/// <summary>Quái chưa có sprite (Brute): vẽ hình tròn tạm.</summary>
	private void DrawPlaceholder(Enemy e)
	{
		_c.ResetTransform();
		Vector2 p = e.Pos + new Vector2(0f, -e.Z);
		Rgba col = e.Flash > 0f ? Rgba.White : e.Dead ? new Rgba(e.Color, 0.6f) : e.Color;

		// Brute: đổi màu/rung theo trạng thái để người chơi đọc được ý đồ (chưa có sprite)
		if (e.Kind == EnemyTypes.Brute && !e.Dead && e.Flash <= 0f)
		{
			switch (e.ChargeState)
			{
				case 1:
					p.X += _w.Frame % 2 == 0 ? 1.5f : -1.5f;                          // gồng: rung
					col = col.Lerp(new Rgba(1f, 0.85f, 0.3f), 0.5f);
					break;
				case 2: col = new Rgba(1f, 0.55f, 0.1f); break;                       // lao: cam rực
				case 3: col = col.Lerp(new Rgba(0.55f, 0.55f, 0.55f), 0.6f); break;    // kiệt sức: xám
			}
		}
		_c.DrawCircle(p, e.Radius, col);
		if (e.Kind == EnemyTypes.Brute && e.ShieldHp > 0f && !e.Dead) _c.DrawArc(p, e.Radius + 2f, 0f, Mx.Tau, new Rgba(0.65f, 0.8f, 1f, 0.5f + 0.4f * (e.ShieldHp / e.ShieldMax)), 2f); // khiên (hình tạm)
		if (e.Radius >= 10f)
			_c.DrawCircle(p + new Vector2(0, -2), 2f, new Rgba(0, 0, 0, 0.7f));
	}
}
