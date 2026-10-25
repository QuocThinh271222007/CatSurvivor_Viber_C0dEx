namespace CatSurvivor;

/// <summary>
/// Console cheat (debug): gõ lệnh rồi Enter. Lệnh thành công thì đóng console và chơi tiếp (kết quả hiện ở góc màn hình);
/// help/info/lỗi thì giữ console mở để đọc. Mũi tên Lên/Xuống để gọi lại lệnh cũ. Esc hoặc F1 để đóng.
/// Danh sách lệnh nằm ở Game/Cheats.cs.
/// </summary>
public partial class CheatForm : Form
{
	private static readonly List<string> History = new();
	private int _historyPos;
	private readonly World _world;

	public CheatForm(World world)
	{
		InitializeComponent();
		_world = world;
		_historyPos = History.Count;
		txtOutput.Text = "Gõ lệnh rồi Enter. Ví dụ:  god  ·  level 27  ·  weapon all 5  ·  spawn brute 3  ·  help (xem đủ lệnh)";
	}

	private void txtCommand_KeyDown(object sender, KeyEventArgs e)
	{
		switch (e.KeyCode)
		{
			case Keys.Enter:
				Run();
				e.SuppressKeyPress = true;
				break;
			case Keys.Up:
				if (_historyPos > 0) txtCommand.Text = History[--_historyPos];
				txtCommand.SelectionStart = txtCommand.TextLength;
				e.Handled = true;
				break;
			case Keys.Down:
				txtCommand.Text = _historyPos < History.Count - 1 ? History[++_historyPos] : "";
				_historyPos = Math.Min(_historyPos, History.Count);
				txtCommand.SelectionStart = txtCommand.TextLength;
				e.Handled = true;
				break;
			case Keys.Escape:
			case Keys.F1:
			case Keys.Oemtilde:
				if (e.KeyCode != Keys.Oemtilde || txtCommand.TextLength == 0) { Close(); e.SuppressKeyPress = true; }
				break;
		}
	}

	private void Run()
	{
		string line = txtCommand.Text.Trim();
		if (line.Length == 0) return;
		if (History.Count == 0 || History[^1] != line) History.Add(line);
		_historyPos = History.Count;
		txtCommand.Clear();

		var res = Cheats.Execute(_world, line);
		txtOutput.Text = res.Message;
		if (res.KeepOpen) return;

		_world.ShowToast("> " + line + "  →  " + res.Message);
		Close();
	}
}
