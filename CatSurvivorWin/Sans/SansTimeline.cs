using System.Globalization;

namespace CatSurvivor;

/// <summary>
/// Bộ chạy kịch bản đòn đánh của Sans (port từ event sheet "Timeline" của repo Jcw87/c2-sans-fight).
/// Mỗi dòng: thời gian chờ (giây, tính từ dòng trước), tên lệnh, các tham số, cách nhau dấu phẩy. Ví dụ "0.4,BoneV,503,286,100,2,300,1".
///   - "$tên" trong bất kì ô nào được thay bằng giá trị biến lúc dòng được nạp; nhãn là dòng có tên lệnh bắt đầu bằng ":" (":vong")
///   - Lệnh toán/nhảy chạy tại đây: SET ADD SUB MUL DIV MOD FLOOR DEG RAD SIN COS ANGLE RND, JMPABS JMPREL JMPZ JMPNZ JMPE JMPNE JMPL JMPNL JMPG JMPNG
///   - Mọi lệnh khác (BoneV, GasterBlaster, TLPause...) chuyển cho Call của trận đấu
/// Dừng chờ bằng Pause()/Resume() (lệnh TLPause/TLResume), nhiều dòng "chờ 0" chạy liền trong cùng một khung hình.
/// </summary>
internal sealed class SansTimeline
{
	/// <summary>Chống vòng lặp vô hạn: số dòng tối đa chạy trong MỘT khung hình.</summary>
	private const int MaxLinesPerTick = 20000;

	private string[][] _lines = Array.Empty<string[]>();
	private string[] _cur = Array.Empty<string>();
	private readonly Dictionary<string, int> _labels = new();
	private readonly Dictionary<string, string> _vars = new();
	private int _line;          // dòng hiện tại, đếm từ 1 (0 = không có)
	private double _t;          // thời gian đã tích luỹ cho dòng hiện tại
	private readonly Random _rng;

	public bool Running { get; private set; }

	/// <summary>Chạy một lệnh của trận đấu: (tên lệnh, các tham số dạng chuỗi).</summary>
	public Action<string, string[]> Call;
	/// <summary>Báo lỗi kịch bản (vòng lặp vô hạn, nhãn không tồn tại).</summary>
	public Action<string> Panic;
	/// <summary>Vị trí hiện tại của trái tim (cho lệnh GetHeartPos).</summary>
	public Func<(float x, float y)> HeartPos;

	public SansTimeline(Random rng) { _rng = rng; }

	private static double F(string s) =>
		double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double v) && !double.IsNaN(v) ? v : 0.0;

	private static string S(double v) => v.ToString("R", CultureInfo.InvariantCulture);

	// Hàm lượng giác dùng ĐỘ (như Construct 2): to_radians(x) = x / (180 / pi)
	private static double Rad(double deg) => deg / (180.0 / Math.PI);

	public void Play(string text)
	{
		var raw = text.Replace("\r", "").Split('\n');
		_lines = new string[raw.Length][];
		_labels.Clear();
		_vars.Clear();
		_vars["pi"] = S(Math.PI);
		for (int i = 0; i < raw.Length; i++)
		{
			_lines[i] = raw[i].Split(',');
			if (_lines[i].Length > 1 && _lines[i][1].StartsWith(':')) _labels[_lines[i][1].Substring(1)] = i + 1;
		}
		_t = 0;
		_line = 1;
		LoadLine();
		Running = true;
	}

	public void Pause() => Running = false;
	public void Resume() => Running = true;

	public void Stop()
	{
		_lines = Array.Empty<string[]>();
		_cur = Array.Empty<string>();
		Running = false;
		_line = 0;
	}

	private string Tok(int i) => i < _cur.Length ? _cur[i] : "";

	private void LoadLine()
	{
		if (_line < 1 || _line > _lines.Length) { _cur = Array.Empty<string>(); return; }
		var src = _lines[_line - 1];
		_cur = new string[src.Length];
		for (int i = 0; i < src.Length; i++)
		{
			string t = src[i];
			_cur[i] = t.StartsWith('$') ? (_vars.TryGetValue(t.Substring(1), out var v) ? v : "0") : t;
		}
	}

	public void Update(float dt)
	{
		int guard = 0;
		while (Running && _line > 0 && _line <= _lines.Length && _t >= F(Tok(0)))
		{
			string fn = Tok(1);
			if (!fn.StartsWith(':'))
			{
				var args = new string[9];
				for (int i = 0; i < 9; i++) args[i] = Tok(2 + i);
				Dispatch(fn, args);
			}
			_t -= F(Tok(0));
			_line++;
			LoadLine();
			if (++guard >= MaxLinesPerTick)
			{
				Running = false;
				Panic?.Invoke($"Infinite loop detected line {_line}");
				return;
			}
		}
		if (Running) _t += dt;
	}

	private void Jump(string target)
	{
		if (target.Length > 0 && target.All(char.IsAsciiDigit)) _line = int.Parse(target, CultureInfo.InvariantCulture) - 1;
		else if (_labels.TryGetValue(target, out int l)) _line = l - 1;
		else { Running = false; Panic?.Invoke($"Label {target} does not exist line {_line}"); }
	}

	private void Dispatch(string fn, string[] a)
	{
		void Set(double v) => _vars[a[0]] = S(v);
		switch (fn)
		{
			case "SET": _vars[a[0]] = a[1]; break;
			case "ADD": Set(F(a[1]) + F(a[2])); break;
			case "SUB": Set(F(a[1]) - F(a[2])); break;
			case "MUL": Set(F(a[1]) * F(a[2])); break;
			case "DIV": Set(F(a[1]) / F(a[2])); break;
			case "MOD": Set(F(a[1]) % F(a[2])); break;
			case "FLOOR": Set(Math.Floor(F(a[1]))); break;
			case "DEG": Set(F(a[1]) * 180 / Math.PI); break;
			case "RAD": Set(F(a[1]) * Math.PI / 180); break;
			case "SIN": Set(Math.Sin(Rad(F(a[1])))); break;
			case "COS": Set(Math.Cos(Rad(F(a[1])))); break;
			case "ANGLE":
			{
				double deg = Math.Atan2(F(a[4]) - F(a[2]), F(a[3]) - F(a[1])) * 180.0 / Math.PI;
				Set(deg < 0 ? deg + 360.0 : deg);
				break;
			}
			case "RND": Set(Math.Floor(_rng.NextDouble() * (int)F(a[1]))); break;
			case "JMPABS": Jump(a[0]); break;
			case "JMPREL": _line += (int)F(a[0]) - 1; break;
			case "JMPZ": if (F(a[1]) == 0) Jump(a[0]); break;
			case "JMPNZ": if (F(a[1]) != 0) Jump(a[0]); break;
			case "JMPE": if (F(a[1]) == F(a[2])) Jump(a[0]); break;
			case "JMPNE": if (F(a[1]) != F(a[2])) Jump(a[0]); break;
			case "JMPL": if (F(a[1]) < F(a[2])) Jump(a[0]); break;
			case "JMPNL": if (F(a[1]) >= F(a[2])) Jump(a[0]); break;
			case "JMPG": if (F(a[1]) > F(a[2])) Jump(a[0]); break;
			case "JMPNG": if (F(a[1]) <= F(a[2])) Jump(a[0]); break;
			case "GetHeartPos":
			{
				var (x, y) = HeartPos?.Invoke() ?? (0f, 0f);
				_vars[a[0]] = S(x);
				_vars[a[1]] = S(y);
				break;
			}
			case "Debug": break;
			default: Call?.Invoke(fn, a); break;
		}
	}
}
