using System.Drawing.Imaging;
using System.Text;

namespace CatSurvivor;

/// <summary>
/// Nạp hình/âm thanh từ thư mục Assets (cạnh file .exe). MỌI thao tác nạp đều bọc try...catch:
/// thiếu hoặc hỏng file thì dùng hình thay thế (ô caro hồng-đen) và ghi vào báo cáo để hiện một lần, game KHÔNG bị văng.
/// </summary>
public static class AssetStore
{
	public static readonly string Root = Path.Combine(AppContext.BaseDirectory, "Assets");

	private static readonly Dictionary<string, Bitmap> Cache = new();
	private static readonly List<string> Problems = new();

	public static string PathOf(string relative) => Path.Combine(Root, relative.Replace('/', Path.DirectorySeparatorChar));

	public static void Report(string what, string reason)
	{
		string line = $"{what}: {reason}";
		lock (Problems) { if (!Problems.Contains(line)) Problems.Add(line); }
	}

	public static bool HasProblems { get { lock (Problems) return Problems.Count > 0; } }

	/// <summary>Lấy báo cáo lỗi tài nguyên (và xoá đi); null nếu không có lỗi.</summary>
	public static string TakeReport()
	{
		lock (Problems)
		{
			if (Problems.Count == 0) return null;
			var sb = new StringBuilder("Một số tài nguyên bị thiếu hoặc lỗi (game vẫn chạy bằng hình/âm thanh thay thế):\n");
			foreach (var p in Problems) sb.Append("  • ").Append(p).Append('\n');
			Problems.Clear();
			return sb.ToString();
		}
	}

	/// <summary>Nạp một ảnh PNG; thiếu/lỗi thì báo và trả về hình thay thế.</summary>
	public static Bitmap Image(string relative)
	{
		if (Cache.TryGetValue(relative, out var cached)) return cached;
		Bitmap bmp = null;
		try
		{
			string path = PathOf(relative);
			if (!File.Exists(path)) throw new FileNotFoundException("không tìm thấy file", path);
			bmp = Load(path);
		}
		catch (Exception ex)
		{
			Report(relative, ex.Message);
			bmp = Placeholder();
		}
		Cache[relative] = bmp;
		return bmp;
	}

	/// <summary>Nạp ảnh tuỳ chọn (vd. asset trang bị chưa vẽ): không có thì trả về null, không báo lỗi.</summary>
	public static Bitmap OptionalImage(string relative)
	{
		if (Cache.TryGetValue(relative, out var cached)) return cached;
		Bitmap bmp = null;
		try
		{
			string path = PathOf(relative);
			if (File.Exists(path)) bmp = Load(path);
		}
		catch (Exception ex)
		{
			Report(relative, ex.Message);
		}
		Cache[relative] = bmp;
		return bmp;
	}

	/// <summary>Nạp ảnh ở dạng ARGB thẳng (không premultiply) để xử lý từng pixel; không lưu đệm. Thiếu/lỗi thì báo và trả hình thay thế.</summary>
	public static Bitmap ImageStraight(string relative)
	{
		try
		{
			string path = PathOf(relative);
			if (!File.Exists(path)) throw new FileNotFoundException("không tìm thấy file", path);
			using var fs = File.OpenRead(path);
			using var raw = new Bitmap(fs);
			var dst = new Bitmap(raw.Width, raw.Height, PixelFormat.Format32bppArgb);
			using var g = Graphics.FromImage(dst);
			g.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
			g.DrawImage(raw, 0, 0, raw.Width, raw.Height);
			return dst;
		}
		catch (Exception ex)
		{
			Report(relative, ex.Message);
			return Placeholder();
		}
	}

	/// <summary>Đọc PNG qua stream (không khoá file) rồi đổi sang định dạng vẽ nhanh (PArgb).</summary>
	private static Bitmap Load(string path)
	{
		using var fs = File.OpenRead(path);
		using var raw = new Bitmap(fs);
		return ToPArgb(raw);
	}

	public static Bitmap ToPArgb(Bitmap src)
	{
		var dst = new Bitmap(src.Width, src.Height, PixelFormat.Format32bppPArgb);
		using var g = Graphics.FromImage(dst);
		g.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
		g.DrawImage(src, 0, 0, src.Width, src.Height);
		return dst;
	}

	/// <summary>Hình thay thế 64x64 (ô caro hồng - đen) khi thiếu asset.</summary>
	private static Bitmap Placeholder()
	{
		var bmp = new Bitmap(64, 64, PixelFormat.Format32bppPArgb);
		using var g = Graphics.FromImage(bmp);
		using var a = new SolidBrush(Color.Magenta);
		using var b = new SolidBrush(Color.Black);
		for (int y = 0; y < 8; y++)
			for (int x = 0; x < 8; x++)
				g.FillRectangle((x + y) % 2 == 0 ? a : b, x * 8, y * 8, 8, 8);
		return bmp;
	}
}
