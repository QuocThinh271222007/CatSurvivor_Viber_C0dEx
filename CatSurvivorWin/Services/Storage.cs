using System.Globalization;

namespace CatSurvivor;

/// <summary>Một dòng trong bảng thành tích.</summary>
public class ScoreEntry
{
	public string Name = "Ẩn danh";
	public int Score;
	public int Seconds;       // thời gian chơi
	public int Level;
	public int Kills;
	public bool Victory;      // true: đã hạ boss
	public string Date = "";
	public int Mode;          // độ khó 0..3 (0 Dễ, 1 Trung bình, 2 Khó, 3 Siêu khó); Điểm đã được nhân theo độ khó

	/// <summary>Định dạng một dòng trong thanhtichs.txt: Tên|Điểm|Giây|Cấp|Kills|Thắng(0/1)|Ngày giờ|Độ khó(0..3)</summary>
	public string ToLine() =>
		$"{Name.Replace('|', ' ').Replace('\r', ' ').Replace('\n', ' ').Trim()}|{Score}|{Seconds}|{Level}|{Kills}|{(Victory ? 1 : 0)}|{Date}|{Mode}";

	public static bool TryParse(string line, out ScoreEntry entry)
	{
		entry = null;
		if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith('#')) return false;
		var p = line.Split('|');
		if (p.Length < 6) return false;
		if (!int.TryParse(p[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int score)) return false;
		if (!int.TryParse(p[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int sec)) return false;
		if (!int.TryParse(p[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out int lvl)) return false;
		if (!int.TryParse(p[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out int kills)) return false;
		entry = new ScoreEntry
		{
			Name = p[0].Trim().Length > 0 ? p[0].Trim() : "Ẩn danh",
			Score = score, Seconds = sec, Level = lvl, Kills = kills,
			Victory = p[5].Trim() == "1",
			Date = p.Length > 6 ? p[6].Trim() : "",
			// Dòng cũ chưa có cột độ khó -> coi như Dễ
			Mode = p.Length > 7 && int.TryParse(p[7], NumberStyles.Integer, CultureInfo.InvariantCulture, out int mode) ? Mx.Clamp(mode, 0, 3) : 0,
		};
		return true;
	}
}

/// <summary>
/// Đọc/ghi file bằng System.IO, MỌI thao tác đều bọc try...catch và trả về thông báo lỗi (không ném ngoại lệ ra ngoài)
/// để game không bị văng khi file thiếu/hỏng/không có quyền ghi.
///   thanhtichs.txt   : bảng thành tích (thêm dòng mới ở cuối - append)
///   settings.txt     : cài đặt (bật/tắt âm thanh)
///   thongtinnhom.txt : thông tin nhóm (Họ tên|MSSV|Lớp) hiện ở màn hình About
/// Thư mục dữ liệu: ưu tiên thư mục chứa file .exe, nếu không ghi được thì %AppData%\CatSurvivor.
/// </summary>
public static class Storage
{
	public const string ScoreFile = "thanhtichs.txt";
	public const string SettingsFile = "settings.txt";
	public const string TeamFile = "thongtinnhom.txt";

	/// <summary>Chỉ dùng cho kiểm thử: ép đường dẫn file thành tích (vd. trỏ vào một thư mục để thử bẫy lỗi).</summary>
	public static string ScoreFileOverride;

	private static string _dir;

	/// <summary>Thư mục dữ liệu ghi được (tìm một lần rồi nhớ).</summary>
	public static string DataDir
	{
		get
		{
			if (_dir != null) return _dir;
			foreach (var cand in Candidates())
			{
				try
				{
					if (string.IsNullOrEmpty(cand)) continue;
					Directory.CreateDirectory(cand);
					string probe = Path.Combine(cand, ".write_test");
					File.WriteAllText(probe, "x");
					File.Delete(probe);
					return _dir = cand;
				}
				catch (Exception)
				{
					// thư mục này không ghi được -> thử ứng viên kế tiếp
				}
			}
			return _dir = "";
		}
	}

	private static IEnumerable<string> Candidates()
	{
		yield return AppContext.BaseDirectory;
		string appData = null;
		try { appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CatSurvivor"); } catch (Exception) { }
		yield return appData;
	}

	internal static string PathOf(string file) => Path.Combine(DataDir, file);
	public static string ScorePath => ScoreFileOverride ?? PathOf(ScoreFile);

	// ------------------------------------------------------------------ bảng thành tích
	/// <summary>Đọc toàn bộ thành tích, sắp xếp điểm cao -> thấp (bằng điểm thì thời gian ngắn hơn xếp trên). Lỗi -> `error` có nội dung.</summary>
	public static List<ScoreEntry> ReadScores(out string error)
	{
		error = null;
		var list = new List<ScoreEntry>();
		try
		{
			string path = ScorePath;
			if (!File.Exists(path)) return list; // chưa có ai chơi: bảng trống, không phải lỗi
			foreach (var line in File.ReadAllLines(path))
				if (ScoreEntry.TryParse(line, out var e)) list.Add(e);
			list.Sort((a, b) => b.Score != a.Score ? b.Score.CompareTo(a.Score) : a.Seconds.CompareTo(b.Seconds));
		}
		catch (Exception ex)
		{
			error = $"Không đọc được file thành tích ({ScoreFile}): {ex.Message}";
		}
		return list;
	}

	/// <summary>Ghi nối thêm một thành tích vào cuối file (append). Trả về false và `error` nếu thất bại.</summary>
	public static bool AppendScore(ScoreEntry entry, out string error)
	{
		error = null;
		try
		{
			File.AppendAllText(ScorePath, entry.ToLine() + Environment.NewLine);
			return true;
		}
		catch (Exception ex)
		{
			error = $"Không ghi được file thành tích ({ScoreFile}): {ex.Message}";
			return false;
		}
	}

	// ------------------------------------------------------------------ thông tin nhóm
	private const string TeamTemplate =
		"# Thông tin nhóm thực hiện - mỗi dòng: Họ tên|MSSV|Lớp  (dòng bắt đầu bằng # là chú thích)\n" +
		"# Hãy sửa các dòng dưới đây thành thông tin thật của nhóm.\n" +
		"Nguyễn Văn A|MSSV0001|Lớp ...\n" +
		"Trần Thị B|MSSV0002|Lớp ...\n";

	/// <summary>Đọc thông tin nhóm; chưa có file thì tạo file mẫu. Mỗi phần tử: [Họ tên, MSSV, Lớp].</summary>
	public static List<string[]> ReadTeam(out string error)
	{
		error = null;
		var rows = new List<string[]>();
		try
		{
			string path = PathOf(TeamFile);
			if (!File.Exists(path)) File.WriteAllText(path, TeamTemplate);
			foreach (var raw in File.ReadAllLines(path))
			{
				string line = raw.Trim();
				if (line.Length == 0 || line.StartsWith('#')) continue;
				var p = line.Split('|');
				rows.Add(new[] { p[0].Trim(), p.Length > 1 ? p[1].Trim() : "", p.Length > 2 ? p[2].Trim() : "" });
			}
		}
		catch (Exception ex)
		{
			error = $"Không đọc được thông tin nhóm ({TeamFile}): {ex.Message}";
		}
		return rows;
	}
}
