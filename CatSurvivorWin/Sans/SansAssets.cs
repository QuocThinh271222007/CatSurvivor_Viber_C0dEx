using System.Numerics;

namespace CatSurvivor;

/// <summary>
/// Nạp hình của trận đánh Sans từ Assets/sans (chép từ repo Jcw87/c2-sans-fight bởi tools/import_sans_fight.ps1):
///   Textures/{tên}.png                    ảnh đơn (xương, font, vùng chiến đấu...)
///   Animations/{vật}/{hoạt ảnh}/000.png.. từng khung hình của một hoạt ảnh
/// Thiếu file thì AssetStore báo một lần và dùng ô caro thay thế (game không văng).
/// </summary>
internal static class SansAssets
{
	private static readonly Dictionary<string, Bitmap[]> Frames = new();

	public static Bitmap Tex(string name) => AssetStore.Image($"sans/Textures/{name}.png");

	/// <summary>Các khung hình của một hoạt ảnh (ít nhất một khung).</summary>
	public static Bitmap[] Anim(string obj, string anim)
	{
		string key = obj + "/" + anim;
		if (Frames.TryGetValue(key, out var f)) return f;
		var list = new List<Bitmap>();
		for (int i = 0; ; i++)
		{
			var b = AssetStore.OptionalImage($"sans/Animations/{obj}/{anim}/{i:000}.png");
			if (b == null) break;
			list.Add(b);
		}
		if (list.Count == 0) list.Add(AssetStore.Image($"sans/Animations/{obj}/{anim}/000.png")); // báo thiếu + hình thay thế
		return Frames[key] = list.ToArray();
	}

	public static Bitmap Frame(string obj, string anim, int i)
	{
		var a = Anim(obj, anim);
		return a[Math.Clamp(i, 0, a.Length - 1)];
	}
}

/// <summary>
/// Font bitmap của trận Sans: ảnh lưới ô (cellW x cellH) chứa các ký tự ASCII liên tiếp từ khoảng trắng (mã 32), mỗi ký tự có thể có
/// độ rộng riêng (bảng độ rộng của repo gốc - lệnh "Set character width" của plugin Spritefont2: ký tự được cắt theo độ rộng này và con trỏ tiến đúng bấy nhiêu).
/// </summary>
internal sealed class SansFont
{
	private readonly string _tex;
	private readonly int _cellW, _cellH, _cols;
	private readonly int[] _widths = new int[128];

	public int CellHeight => _cellH;

	private SansFont(string tex, int cellW, int cellH, params (string chars, int w)[] widths)
	{
		_tex = tex; _cellW = cellW; _cellH = cellH;
		_cols = Math.Max(1, SansAssets.Tex(tex).Width / cellW);
		Array.Fill(_widths, cellW);
		foreach (var (chars, w) in widths)        // áp dụng lần lượt: bảng sau đè bảng trước (như các lệnh gốc)
			foreach (char ch in chars)
				if (ch < 128) _widths[ch] = w;
	}

	private static SansFont _battle, _dmg, _def, _sans;

	/// <summary>Font nhỏ 6x6 (chữ hoa) cho tên + máu.</summary>
	public static SansFont Battle => _battle ??= new SansFont("BattleFont", 6, 6,
		("!\"#%-/0123456789<=>?ABCDEFGHIJKLNOPQRSTUVXYZ[\\]_", 5), ("\"()<>[]", 4), (" -", 3), ("',.:;", 2));

	/// <summary>Font số sát thương / chữ MISS (ô 33x32).</summary>
	public static SansFont Damage => _dmg ??= new SansFont("DamageFont", 33, 32,
		("~", 29), ("\"/<>I^j{}", 25), ("(),1[]`", 21), ("!'.:;il|", 17));

	/// <summary>Font chữ thông tin trong khung (ô 10x16).</summary>
	public static SansFont Default => _def ??= new SansFont("DefaultFont", 10, 16,
		("#%&MWmw~", 9), (" $*+-./0123456789=?@ABCDEFGHIJKLNOPQRSTUVXYZ\\^abcdefghijklnopqrstuvxyz", 8), ("\"<>{}", 7), ("!()[]_", 6), ("`", 5), ("',:;|", 4));

	/// <summary>Font lời thoại Sans (ô 16x16).</summary>
	public static SansFont Sans => _sans ??= new SansFont("SansFont", 16, 16,
		("W", 15), ("@", 14), ("%Q", 13), ("MO", 12), ("#&GNVX_", 11), (" $ACHJSTUYZmw", 10), ("247?BDEKdxy~", 9),
		("*+/0135689FILR\\^abcefghknopqrtuvz", 8), ("-=Pjs{}", 7), ("()<>[]", 6), ("\";`", 5), ("!',.:il|", 4));

	private int WidthOf(char ch) => ch >= 32 && ch < 127 ? _widths[ch] : _cellW;

	public float Measure(string text, float scale)
	{
		float best = 0f;
		foreach (var line in text.Split('\n'))
		{
			float w = 0f;
			foreach (char ch in line) w += WidthOf(ch);
			best = MathF.Max(best, w * scale);
		}
		return best;
	}

	/// <summary>Ngắt dòng theo từ để vừa wrapWidth (px, đã tính scale); giữ nguyên các dấu xuống dòng có sẵn. wrapWidth &lt;= 0 = không ngắt.</summary>
	public List<string> Wrap(string text, float scale, float wrapWidth)
	{
		var result = new List<string>();
		foreach (var para in text.Split('\n'))
		{
			if (wrapWidth <= 0f) { result.Add(para); continue; }
			string line = "";
			foreach (var word in para.Split(' '))
			{
				string trial = line.Length == 0 ? word : line + " " + word;
				if (line.Length > 0 && Measure(trial, scale) > wrapWidth) { result.Add(line); line = word; }
				else line = trial;
			}
			result.Add(line);
		}
		return result;
	}

	/// <summary>Vẽ chữ (góc trên-trái tại pos). tint nhân vào màu trắng của ký tự.</summary>
	public void Draw(Canvas c, string text, Vector2 pos, float scale, Rgba tint, float wrapWidth = 0f)
	{
		var sheet = SansAssets.Tex(_tex);
		float y = pos.Y;
		foreach (var line in Wrap(text, scale, wrapWidth))
		{
			float x = pos.X;
			foreach (char ch in line)
			{
				int w = WidthOf(ch);
				int idx = ch >= 32 && ch < 127 ? ch - 32 : 0;
				if (ch != ' ')
					c.DrawTexture(sheet, new Rect2(x, y, w * scale, _cellH * scale),
						new Rect2((idx % _cols) * _cellW, (idx / _cols) * _cellH, w, _cellH), tint);
				x += w * scale;
			}
			y += _cellH * scale;
		}
	}
}
