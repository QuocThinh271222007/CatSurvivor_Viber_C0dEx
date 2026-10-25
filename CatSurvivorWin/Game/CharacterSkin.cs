using System.Numerics;

namespace CatSurvivor;

/// <summary>
/// Hình dáng nhân vật người chơi: Mèo (mặc định) hoặc Sans (mở khoá khi đánh bại Sans trong easter egg - xem Sans/SansEasterEgg.cs).
/// Chỉ khác về hình vẽ/hoạt ảnh; chỉ số, hitbox và mọi thứ khác giữ nguyên.
///   Mèo : Assets/characters/main_character.png - 6 khung 32x32: 0-3 đi/đứng yên, 4 nháy trắng khi trúng đòn, 5 chết
///   Sans: Assets/characters/sans.png           - 5 khung 64x64: 0-1 chạy (nhìn phải), 2-4 đứng yên (nhìn thẳng); không có khung trúng đòn/chết nên
///         trúng đòn thì nhấp nháy mờ và chết thì dùng khung đứng yên (vẫn tưng lên rồi rơi như mèo)
/// Anchor: điểm trên khung ảnh đặt đúng vị trí nhân vật (chân nằm khoảng Pos + FootOffset), vẽ nguyên cỡ để pixel vuông vức.
/// </summary>
public sealed class CharacterSkin
{
	public const int Cat = 0, Sans = 1;

	public int Id { get; private init; }
	public string DisplayName { get; private init; }
	public Bitmap Sheet { get; private init; }
	public int FrameSize { get; private init; }
	public Vector2 Anchor { get; private init; }
	public int[] WalkFrames { get; private init; }
	public int[] IdleFrames { get; private init; }
	public float WalkFps { get; private init; }
	public float IdleFps { get; private init; }
	/// <summary>Khung nháy khi trúng đòn; &lt; 0 = không có, vẽ nhấp nháy mờ.</summary>
	public int HitFrame { get; private init; }
	public int DeadFrame { get; private init; }

	/// <summary>Chỗ vẽ khung ảnh (toạ độ cục bộ quanh vị trí nhân vật).</summary>
	public Rect2 Dest => new(-Anchor.X, -Anchor.Y, FrameSize, FrameSize);
	public Rect2 Src(int frame) => new(frame * FrameSize, 0, FrameSize, FrameSize);
	public int WalkFrame(float t) => WalkFrames[(int)(t * WalkFps) % WalkFrames.Length];
	public int IdleFrame(float t) => IdleFrames[(int)(t * IdleFps) % IdleFrames.Length];

	/// <summary>Nhân vật đang được chọn (Sans chỉ khi đã mở khoá và còn file ảnh), mặc định là Mèo.</summary>
	public static int Selected => Settings.SansUnlocked && Settings.Character == Sans ? Sans : Cat;

	public static CharacterSkin Load(int id)
	{
		if (id == Sans)
		{
			var sheet = AssetStore.OptionalImage("characters/sans.png");
			if (sheet != null)
				return new CharacterSkin
				{
					Id = Sans, DisplayName = "SANS", Sheet = sheet, FrameSize = 64,
					Anchor = new Vector2(31.5f, 36f),            // chân Sans ở khoảng y=47,5 của khung 64 -> Pos + FootOffset
					WalkFrames = new[] { 0, 1 }, WalkFps = 8f,
					IdleFrames = new[] { 2, 3, 4, 3 }, IdleFps = 4f,   // thở nhấp nhô
					HitFrame = -1, DeadFrame = 2,
				};
			AssetStore.Report("characters/sans.png", "thiếu ảnh nhân vật Sans (xuất từ Sans.aseprite bằng tools\\export_assets.ps1) - dùng mèo");
		}
		return new CharacterSkin
		{
			Id = Cat, DisplayName = "MÈO", Sheet = AssetStore.Image("characters/main_character.png"), FrameSize = 32,
			Anchor = new Vector2(16f, 16f),
			WalkFrames = new[] { 0, 1, 2, 3 }, WalkFps = 10f,
			IdleFrames = new[] { 0 }, IdleFps = 1f,
			HitFrame = 4, DeadFrame = 5,
		};
	}

	/// <summary>Ảnh xem trước (khung đứng yên cắt sát viền, phóng nguyên số lần) cho màn chọn nhân vật; null nếu chưa có ảnh.</summary>
	public static Bitmap Preview(int id, int scale)
	{
		var skin = Load(id);
		if (skin.Id != id || skin.Sheet == null) return null;
		int f = skin.IdleFrames[0];
		using var frame = skin.Sheet.Clone(new Rectangle(f * skin.FrameSize, 0, skin.FrameSize, skin.FrameSize), System.Drawing.Imaging.PixelFormat.Format32bppArgb);
		var crop = EquipmentAssets.CropToContent(frame);
		if (crop == null) return null;
		var big = new Bitmap(crop.Width * scale, crop.Height * scale, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
		using (var g = Graphics.FromImage(big))
		{
			g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
			g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
			g.DrawImage(crop, 0, 0, big.Width, big.Height);
		}
		crop.Dispose();
		return big;
	}
}
