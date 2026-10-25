using System.Numerics;
using System.Text.RegularExpressions;

namespace CatSurvivor;

/// <summary>
/// Font bitmap của Aseprite (Assets/ui/aseprite_font.fnt + .png, © David Capello, CC BY 4.0) để vẽ chữ trong game (HUD, biểu ngữ).
/// Ký tự có trong font thì vẽ bằng hình, ký tự thiếu (nhiều chữ tiếng Việt có dấu hỏi/nặng/mũ-dấu) thì vẽ bằng font hệ thống.
/// Font gốc cao 8px nên chỉ sắc nét khi vẽ ở tỉ lệ nguyên (scale 1, 2, 3...).
/// </summary>
public static class PixelFont
{
	private struct Glyph { public int X, Y, W, H, Advance, Top, Bottom, Left, Right; }

	/// <summary>Chữ có dấu mà font không có sẵn: ghép từ chữ cái gốc của font + các dấu tự vẽ bằng pixel (sắc, huyền, hỏi, ngã, nặng, mũ, trăng, móc).</summary>
	private sealed class Composite { public Glyph Base; public int[] Marks; }
	private static readonly Dictionary<char, Composite> Composites = new();
	private static readonly HashSet<int> KnownMarks = new() { 0x0301, 0x0300, 0x0309, 0x0303, 0x0323, 0x0302, 0x0306, 0x031B };

	private static readonly Dictionary<int, Glyph> Glyphs = new();
	private static Bitmap _atlas;
	private static bool _loaded;
	private static Font _fallback;
	private static readonly StringFormat Fmt = StringFormat.GenericTypographic;

	public const int LineHeight = 8;

	private static void EnsureLoaded()
	{
		if (_loaded) return;
		_loaded = true;
		try
		{
			string fnt = AssetStore.PathOf("ui/aseprite_font.fnt");
			if (!File.Exists(fnt)) throw new FileNotFoundException("không tìm thấy font", fnt);
			_atlas = AssetStore.Image("ui/aseprite_font.png");

			var kv = new Regex(@"(\w+)=(-?\d+)");
			foreach (var line in File.ReadAllLines(fnt))
			{
				if (!line.StartsWith("char ")) continue;
				var d = new Dictionary<string, int>();
				foreach (Match m in kv.Matches(line)) d[m.Groups[1].Value] = int.Parse(m.Groups[2].Value);
				Glyphs[d["id"]] = new Glyph { X = d["x"], Y = d["y"], W = d["width"], H = d["height"], Advance = d["xadvance"] };
			}
			DropHornedO();
			ComputeExtents();
		}
		catch (Exception ex)
		{
			AssetStore.Report("Assets/ui/aseprite_font.fnt", ex.Message);
			Glyphs.Clear(); // không có font bitmap: toàn bộ chữ dùng font hệ thống
		}
	}

	/// <summary>
	/// Glyph "ơ" thường (và ớ ờ ở ỡ ợ) trong file font nhìn y hệt "ư" ở cỡ nhỏ. Bỏ chúng đi để các chữ này được GHÉP từ chữ "o" + móc
	/// (cùng cách "ư" = "u" + móc), nhờ vậy "ơ" và "ư" khác nhau rõ ràng.
	/// </summary>
	private static void DropHornedO()
	{
		foreach (int key in Glyphs.Keys.ToArray())
		{
			if (key > char.MaxValue) continue;
			string d = ((char)key).ToString().Normalize(System.Text.NormalizationForm.FormD);
			if (d.Length > 1 && d[0] == 'o' && d.Contains('̛')) Glyphs.Remove(key);
		}
	}

	/// <summary>Tìm hàng/cột pixel đầu-cuối thật sự có chữ của mỗi glyph (để đặt dấu ngay trên/dưới chữ).</summary>
	private static void ComputeExtents()
	{
		if (_atlas == null) return;
		var rect = new Rectangle(0, 0, _atlas.Width, _atlas.Height);
		var data = _atlas.LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
		try
		{
			int[] px = new int[_atlas.Width * _atlas.Height];
			System.Runtime.InteropServices.Marshal.Copy(data.Scan0, px, 0, px.Length);
			foreach (var key in Glyphs.Keys.ToArray())
			{
				var g = Glyphs[key];
				int top = int.MaxValue, bottom = -1, left = int.MaxValue, right = -1;
				for (int yy = 0; yy < g.H; yy++)
					for (int xx = 0; xx < g.W; xx++)
					{
						int ax = g.X + xx, ay = g.Y + yy;
						if (ax >= _atlas.Width || ay >= _atlas.Height) continue;
						if ((px[ay * _atlas.Width + ax] >> 24 & 255) == 0) continue;
						top = Math.Min(top, yy); bottom = Math.Max(bottom, yy); left = Math.Min(left, xx); right = Math.Max(right, xx);
					}
				if (bottom < 0) { top = 0; bottom = g.H - 1; left = 0; right = g.W - 1; }
				g.Top = top; g.Bottom = bottom; g.Left = left; g.Right = right;
				Glyphs[key] = g;
			}
		}
		finally { _atlas.UnlockBits(data); }
	}

	/// <summary>Phân rã chữ có dấu thành (chữ gốc có trong font) + các dấu; null nếu không ghép được.</summary>
	private static Composite Compose(char ch)
	{
		if (Composites.TryGetValue(ch, out var cached)) return cached;
		Composite result = null;
		try
		{
			string d = ch.ToString().Normalize(System.Text.NormalizationForm.FormD);
			for (int k = d.Length - 1; k >= 1 && result == null; k--)
			{
				string pre = d.Substring(0, k).Normalize(System.Text.NormalizationForm.FormC);
				if (pre.Length != 1 || !Glyphs.TryGetValue(pre[0], out var g)) continue;
				var marks = d.Substring(k).Select(m => (int)m).ToArray();
				if (marks.All(KnownMarks.Contains)) result = new Composite { Base = g, Marks = marks };
			}
		}
		catch (Exception) { }
		Composites[ch] = result;
		return result;
	}

	/// <summary>Vẽ các dấu lên chữ cái gốc (toạ độ pixel của font, gốc ở góc trên-trái ô chữ; hàng âm = phía trên ô).</summary>
	private static void DrawMarks(Canvas c, Composite comp, float x, float y, int scale, Rgba color)
	{
		var g = comp.Base;
		int cx = (g.Left + g.Right) / 2;
		int up = 0;
		void Px(int gx, int gy) => c.FillRect(x + gx * scale, y + gy * scale, scale, scale, color);
		foreach (int m in comp.Marks)
		{
			int top = g.Top - 3 * up; // dấu thứ hai chồng lên cao hơn (vd. ế = ê + sắc)
			switch (m)
			{
				case 0x0323: Px(cx, g.Bottom + 2); continue;                                   // nặng: chấm dưới chân
				case 0x031B: Px(g.Right + 1, g.Top); Px(g.Right + 1, g.Top + 1); continue;     // móc (ơ, ư): nét nhỏ bên phải
				case 0x0301: Px(cx + 1, top - 3); Px(cx, top - 2); break;                      // sắc
				case 0x0300: Px(cx - 1, top - 3); Px(cx, top - 2); break;                      // huyền
				case 0x0309: Px(cx, top - 4); Px(cx + 1, top - 4); Px(cx + 1, top - 3); Px(cx, top - 2); break; // hỏi
				case 0x0303: Px(cx - 2, top - 2); Px(cx - 1, top - 3); Px(cx, top - 2); Px(cx + 1, top - 3); break; // ngã
				case 0x0302: Px(cx - 1, top - 2); Px(cx, top - 3); Px(cx + 1, top - 2); break; // mũ
				case 0x0306: Px(cx - 1, top - 3); Px(cx, top - 2); Px(cx + 1, top - 3); break; // trăng
			}
			up++;
		}
	}

	private static Font Fallback(int scale)
	{
		// font hệ thống dự phòng, cỡ tương ứng chiều cao chữ bitmap
		float px = 7.5f * scale;
		if (_fallback == null || MathF.Abs(_fallback.Size - px) > 0.01f)
		{
			_fallback?.Dispose();
			_fallback = new Font("Segoe UI", px, FontStyle.Regular, GraphicsUnit.Pixel);
		}
		return _fallback;
	}

	/// <summary>Chiều rộng của chuỗi (pixel màn hình logic) khi vẽ ở tỉ lệ `scale`.</summary>
	public static float Measure(Canvas c, string text, int scale = 1)
	{
		EnsureLoaded();
		float w = 0f;
		foreach (char ch in text)
		{
			if (Glyphs.TryGetValue(ch, out var g)) w += g.Advance * scale;
			else if (Compose(ch) is { } comp) w += comp.Base.Advance * scale;
			else w += c.G.MeasureString(ch.ToString(), Fallback(scale), PointF.Empty, Fmt).Width + scale;
		}
		return w;
	}

	/// <summary>Ngắt dòng theo từ sao cho mỗi dòng không rộng quá maxWidth (giữ nguyên các dấu xuống dòng có sẵn).</summary>
	public static List<string> Wrap(Canvas c, string text, float maxWidth, int scale = 1)
	{
		EnsureLoaded();
		var lines = new List<string>();
		foreach (var para in (text ?? "").Split('\n'))
		{
			string cur = "";
			foreach (var word in para.Split(' '))
			{
				string trial = cur.Length == 0 ? word : cur + " " + word;
				if (cur.Length > 0 && Measure(c, trial, scale) > maxWidth) { lines.Add(cur); cur = word; }
				else cur = trial;
			}
			lines.Add(cur);
		}
		return lines;
	}

	/// <summary>Vẽ chuỗi (có thể nhiều dòng, ngăn bằng \n) với góc trên-trái ở `pos`.</summary>
	public static void Draw(Canvas c, string text, Vector2 pos, int scale, Rgba color)
	{
		EnsureLoaded();
		float y = pos.Y;
		foreach (var line in text.Split('\n'))
		{
			DrawLine(c, line, new Vector2(pos.X, y), scale, color);
			y += (LineHeight + 2) * scale;
		}
	}

	/// <summary>Vẽ căn giữa theo chiều ngang quanh toạ độ X = centerX.</summary>
	public static void DrawCentered(Canvas c, string text, float centerX, float y, int scale, Rgba color)
	{
		EnsureLoaded();
		foreach (var line in text.Split('\n'))
		{
			DrawLine(c, line, new Vector2(centerX - Measure(c, line, scale) / 2f, y), scale, color);
			y += (LineHeight + 2) * scale;
		}
	}

	private static void DrawLine(Canvas c, string text, Vector2 pos, int scale, Rgba color)
	{
		float x = pos.X;
		Color drawColor = color.ToColor();
		using var brush = new SolidBrush(drawColor);
		foreach (char ch in text)
		{
			if (Glyphs.TryGetValue(ch, out var g) && _atlas != null)
			{
				if (g.W > 0 && ch != ' ')
					c.DrawTexture(_atlas, new Rect2(x, pos.Y, g.W * scale, g.H * scale), new Rect2(g.X, g.Y, g.W, g.H), color);
				x += g.Advance * scale;
			}
			else if (_atlas != null && Compose(ch) is { } comp)
			{
				var bg = comp.Base;
				c.DrawTexture(_atlas, new Rect2(x, pos.Y, bg.W * scale, bg.H * scale), new Rect2(bg.X, bg.Y, bg.W, bg.H), color);
				DrawMarks(c, comp, x, pos.Y, scale, color);
				x += bg.Advance * scale;
			}
			else
			{
				var f = Fallback(scale);
				c.G.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit; // chữ xám, không viền màu ClearType
				c.G.DrawString(ch.ToString(), f, brush, x, pos.Y - scale * 0.5f, Fmt);
				x += c.G.MeasureString(ch.ToString(), f, PointF.Empty, Fmt).Width + scale;
			}
		}
	}
}
