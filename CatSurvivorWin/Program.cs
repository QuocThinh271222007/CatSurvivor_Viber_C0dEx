namespace CatSurvivor;

/// <summary>Cách một ván chơi kết thúc (GameForm.Outcome) -> Program quyết định mở form nào kế tiếp.</summary>
public enum GameOutcome { Restart, Menu, Quit }

internal static class Program
{
	/// <summary>
	/// Luồng chương trình: Menu chính (Start) -> GameForm (chơi, tạm dừng, nâng cấp, kết thúc) -> Chơi lại / về Menu / Thoát.
	/// </summary>
	[STAThread]
	private static void Main(string[] args)
	{
		ApplicationConfiguration.Initialize();

		// Lỗi không lường trước: báo cho người dùng thay vì để chương trình văng
		Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
		Application.ThreadException += (_, e) => ShowError(e.Exception);

		try
		{
			SoundService.Init();

			if (args.Contains("--selftest"))
			{
				SelfTest.Run(args);
				return;
			}

			bool showMenu = true;
			while (true)
			{
				if (showMenu)
				{
					using var menu = new MainMenuForm();
					menu.ShowDialog();
					if (menu.Choice != MenuChoice.Start) break;
				}

				// Đã mở khoá Sans (easter egg) thì chọn nhân vật trước (đóng/Esc = quay về menu)
				if (!CharacterPick.Ask()) { showMenu = true; continue; }

				// Chọn kĩ năng bằng lá bài: Dễ / Trung bình được HAI, Khó / Siêu khó MỘT (đóng/Esc = quay về menu)
				SkillSet.RunChoice = SkillPick.Ask(out var second);
				SkillSet.RunChoice2 = second;
				if (SkillSet.RunChoice == null) { showMenu = true; continue; }

				using var game = new GameForm();
				game.ShowDialog();
				if (game.Outcome == GameOutcome.Quit) break;
				showMenu = game.Outcome == GameOutcome.Menu; // Restart: vào thẳng ván mới, bỏ qua menu
			}
		}
		catch (Exception ex)
		{
			ShowError(ex);
		}
		finally
		{
			SoundService.Shutdown();
		}
	}

	internal static void ShowError(Exception ex)
	{
		MessageBox.Show("Có lỗi không mong muốn:\n" + ex.Message, "Cat Survivor", MessageBoxButtons.OK, MessageBoxIcon.Error);
	}

	/// <summary>Hiện (một lần) các cảnh báo thiếu/lỗi tài nguyên đã gom được.</summary>
	internal static void ShowAssetReport(IWin32Window owner)
	{
		string report = AssetStore.TakeReport();
		if (report != null)
			MessageBox.Show(owner, report, "Cảnh báo tài nguyên", MessageBoxButtons.OK, MessageBoxIcon.Warning);
	}
}
