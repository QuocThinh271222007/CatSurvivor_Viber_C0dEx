using System.Numerics;

namespace CatSurvivor;

/// <summary>
/// Hình vẽ của vũ khí (xuất từ Aseprite bằng tools/export_assets.ps1 vào Assets/weapons/). Thiếu file thì vũ khí tự vẽ hình tròn/đường tạm như trước.
/// Mọi khung là 64x64; các hằng số bên dưới là toạ độ (px trong khung) của asset hiện tại - đổi nếu bạn vẽ lại lệch vị trí.
///   bow.png   : 4 khung ngang. Khung 0 = cung thả dây + MŨI TÊN riêng ở góc trên-trái; khung 1-3 = kéo dây dần. Cung hướng sang PHẢI.
///   knife.png : 1 khung, dao đứng, mũi hướng LÊN.
///   claw.png  : 1 khung, găng vuốt, mũi vuốt hướng sang PHẢI.
/// </summary>
public static class WeaponArt
{
	public static Bitmap Bow => AssetStore.OptionalImage("weapons/bow.png");
	public static Bitmap Knife => AssetStore.OptionalImage("weapons/knife.png");
	public static Bitmap Claw => AssetStore.OptionalImage("weapons/claw.png");

	/// <summary>holy.png: 2 khung 64x64 - khung 0 = thánh giá (Holy Cross), khung 1 = khiên/bong bóng thánh (Holy Shield).</summary>
	public static Bitmap Holy => AssetStore.OptionalImage("weapons/holy.png");

	public const int Frame = 64;

	// Thánh giá (khung 0): vùng cắt + điểm gốc ở giữa
	public static readonly Rect2 CrossSrc = new(23f, 29f, 5f, 7f);
	public static readonly Vector2 CrossPivot = new(2.5f, 3.5f);

	// Khiên thánh (khung 1): vùng cắt (toạ độ trong cả dải ảnh) + điểm gốc ở tâm hình tròn
	public static readonly Rect2 ShieldSrc = new(64f + 21f, 22f, 17f, 16f);
	public static readonly Vector2 ShieldPivot = new(8.5f, 8f);

	// Điểm gốc (tâm xoay) trong khung 64x64
	public static readonly Vector2 BowPivot = new(28f, 39.5f);
	public static readonly Vector2 KnifePivot = new(29f, 36.5f);
	public static readonly Vector2 ClawPivot = new(24.5f, 40.5f);

	// Vùng cắt phần THÂN CUNG trong khung (loại mũi tên rời nằm ở góc trên của khung 0): x, y, rộng, cao
	public static readonly Rect2 BowCrop = new(16f, 28f, 22f, 24f);

	// Vị trí dây cung (x trong khung) ở 4 khung: càng kéo càng lùi trái
	public static readonly float[] BowStringX = { 25f, 23f, 22f, 21f };

	// Mũi tên (nằm ở khung 0 của bow.png): vùng cắt + điểm gốc ở giữa thân
	public static readonly Rect2 ArrowSrc = new(19f, 19f, 9f, 3f);
	public static readonly Vector2 ArrowPivot = new(4.5f, 1.5f);

	/// <summary>Sprite của đạn (vd. mũi tên): ảnh + vùng cắt + điểm gốc + tỉ lệ. Mũi sprite hướng +x, game tự xoay theo hướng bay.</summary>
	public sealed record ProjectileSprite(Bitmap Tex, Rect2 Src, Vector2 Pivot, float Scale);

	/// <summary>Sprite mũi tên cho đạn của cung (null nếu thiếu bow.png).</summary>
	public static ProjectileSprite Arrow(float scale = 1.5f)
	{
		var tex = Bow;
		return tex == null ? null : new ProjectileSprite(tex, ArrowSrc, ArrowPivot, scale);
	}
}
