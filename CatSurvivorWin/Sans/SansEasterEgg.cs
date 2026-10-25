namespace CatSurvivor;

/// <summary>
/// Easter egg: nhập tên SANS (hoa/thường tuỳ ý) khi lưu thành tích -> mở trận đánh Sans (port từ github.com/Jcw87/c2-sans-fight).
/// Đánh bại Sans (sống sót tới khi Sans kiệt sức) thì mở khoá nhân vật SANS để chơi thay mèo (chọn ở màn "Chọn nhân vật" trước mỗi ván).
/// </summary>
internal static class SansEasterEgg
{
	public const string Trigger = "sans";

	/// <summary>Hiện thông báo kết quả (đổi được để kiểm thử không bật hộp thoại): (cửa sổ cha, nội dung, tiêu đề).</summary>
	internal static Action<IWin32Window, string, string> Notify = (owner, text, caption) =>
		MessageBox.Show(owner, text, caption, MessageBoxButtons.OK, MessageBoxIcon.Information);

	/// <summary>Tên có phải "sans" không: đổi hết về chữ thường rồi so sánh nên SaNs, SANS, sAnS... đều nhận.</summary>
	public static bool Matches(string name) => Available && name != null && name.Trim().ToLowerInvariant() == Trigger;

	/// <summary>Hình/âm thanh của trận Sans (Assets/sans) không có trong repo (tài sản Undertale): thiếu thì tắt easter egg, nhập tên SANS chỉ là lưu thành tích bình thường.</summary>
	public static bool Available => Directory.Exists(AssetStore.PathOf("sans/Textures"));

	/// <summary>Mở trận đánh Sans (modal) rồi báo kết quả; thắng thì mở khoá nhân vật Sans.</summary>
	public static void Run(IWin32Window owner)
	{
		SansFight.PreloadMusic();
		SansFight.Outcome result;
		using (var form = new SansFightForm())
		{
			form.ShowDialog(owner);
			result = form.Result;
		}
		Finish(owner, result);
	}

	/// <summary>Xử lý kết quả trận đánh: thắng -> mở khoá + lưu cài đặt + báo; thua -> báo; bỏ cuộc (Running) -> không làm gì.</summary>
	internal static void Finish(IWin32Window owner, SansFight.Outcome result)
	{
		if (result == SansFight.Outcome.Won)
		{
			bool first = !Settings.SansUnlocked;
			Settings.SansUnlocked = true;
			Settings.Save();
			Notify(owner,
				first
					? "huff... puff... bạn đã đánh bại Sans!\n\nĐã MỞ KHOÁ nhân vật SANS.\nTừ ván sau, màn \"Chọn nhân vật\" sẽ cho bạn chơi bằng Sans."
					: "Sans lại thua bạn một lần nữa.\n\n(Nhân vật SANS đã được mở khoá từ trước.)",
				"* You win.");
		}
		else if (result == SansFight.Outcome.Lost)
		{
			Notify(owner, "you're gonna have a bad time.\n\nBạn đã gục ngã... Lưu thành tích với tên SANS lần nữa để thử lại.", "* Bad Time");
		}
	}
}
