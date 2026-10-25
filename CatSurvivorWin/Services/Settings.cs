using System.Globalization;

namespace CatSurvivor;

/// <summary>
/// Cài đặt người dùng, lưu trong settings.txt (mỗi dòng key=value). Chỉnh ở menu Cài đặt (menu chính / Tạm dừng), KHÔNG hiện lúc đang chơi.
///   sound=1/0       bật/tắt toàn bộ âm thanh
///   music=0..100    âm lượng nhạc nền
///   sfx=0..100      âm lượng hiệu ứng
///   fullscreen=1/0  toàn màn hình khi chơi
///   autoaim=1/0     tự động ngắm (phím F khi chơi)
///   hitbox=1/0      hiện hitbox (debug)
///   difficulty=0..3 độ khó: 0 Dễ (mặc định), 1 Trung bình, 2 Khó, 3 Siêu khó
///   sans=1/0        đã mở khoá nhân vật Sans (thắng trận easter egg: nhập tên SANS khi lưu thành tích)
///   character=0/1   nhân vật đang chơi: 0 Mèo (mặc định), 1 Sans (chỉ khi đã mở khoá)
/// Đọc/ghi bọc try...catch: lỗi thì dùng giá trị mặc định / bỏ qua, không làm văng game.
/// </summary>
public static class Settings
{
	public static bool Sound = true;
	public static int MusicVolume = 70;
	public static int SfxVolume = 100;
	public static bool Fullscreen;

	/// <summary>Tự động ngắm: BẬT = vũ khí tự ngắm vào quái gần nhất; TẮT = ngắm bằng CHUỘT (vũ khí vẫn tự bắn, theo hướng con trỏ). Phím F bật/tắt khi chơi.</summary>
	public static bool AutoAim = true;

	/// <summary>Hiện hitbox (debug): vẽ vòng va chạm của mèo, quái, đạn và vùng sát thương của vũ khí.</summary>
	public static bool ShowHitbox;

	/// <summary>Độ khó 0..3 (xem Difficulty.cs): 0 Dễ, 1 Trung bình, 2 Khó, 3 Siêu khó.</summary>
	public static int Difficulty;

	/// <summary>Đã mở khoá nhân vật Sans (đánh bại Sans trong easter egg).</summary>
	public static bool SansUnlocked;

	/// <summary>Nhân vật đang chọn: 0 Mèo, 1 Sans. Luôn là 0 nếu chưa mở khoá Sans.</summary>
	public static int Character;

	/// <summary>Đọc settings.txt (thiếu file = giá trị mặc định). error != null nếu file hỏng/không đọc được.</summary>
	public static void Load(out string error)
	{
		error = null;
		try
		{
			string path = Storage.PathOf(Storage.SettingsFile);
			if (!File.Exists(path)) return;
			foreach (var raw in File.ReadAllLines(path))
			{
				int eq = raw.IndexOf('=');
				if (eq <= 0) continue;
				string key = raw.Substring(0, eq).Trim().ToLowerInvariant(), val = raw.Substring(eq + 1).Trim();
				if (!int.TryParse(val, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v)) continue;
				switch (key)
				{
					case "sound": Sound = v != 0; break;
					case "music": MusicVolume = Mx.Clamp(v, 0, 100); break;
					case "sfx": SfxVolume = Mx.Clamp(v, 0, 100); break;
					case "fullscreen": Fullscreen = v != 0; break;
					case "hitbox": ShowHitbox = v != 0; break;
					case "difficulty": Difficulty = Mx.Clamp(v, 0, 3); break;
					case "sans": SansUnlocked = v != 0; break;
					case "character": Character = Mx.Clamp(v, 0, 1); break;
					case "autoaim":
					case "autofire": AutoAim = v != 0; break; // khoá cũ vẫn đọc được
				}
			}
		}
		catch (Exception ex)
		{
			error = $"Không đọc được cài đặt ({Storage.SettingsFile}): {ex.Message}";
		}
	}

	/// <summary>Ghi settings.txt. Lỗi ghi thì bỏ qua (cài đặt chỉ mất khi thoát game), không báo lỗi gây phiền.</summary>
	public static void Save()
	{
		try
		{
			File.WriteAllText(Storage.PathOf(Storage.SettingsFile),
				$"sound={(Sound ? 1 : 0)}{Environment.NewLine}music={MusicVolume}{Environment.NewLine}sfx={SfxVolume}{Environment.NewLine}fullscreen={(Fullscreen ? 1 : 0)}{Environment.NewLine}autoaim={(AutoAim ? 1 : 0)}{Environment.NewLine}hitbox={(ShowHitbox ? 1 : 0)}{Environment.NewLine}difficulty={Difficulty}{Environment.NewLine}sans={(SansUnlocked ? 1 : 0)}{Environment.NewLine}character={(SansUnlocked ? Character : 0)}{Environment.NewLine}");
		}
		catch (Exception) { }
	}
}
