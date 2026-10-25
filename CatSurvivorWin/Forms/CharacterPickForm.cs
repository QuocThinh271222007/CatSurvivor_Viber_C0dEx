namespace CatSurvivor;

/// <summary>
/// Màn chọn nhân vật trước mỗi ván (chỉ hiện khi đã mở khoá Sans): hai thẻ Mèo / Sans, bấm thẻ hoặc phím 1-2; Esc = về menu.
/// Chỉ đổi hình dáng nhân vật, mọi chỉ số giữ nguyên. Lựa chọn được nhớ (Settings.Character).
/// </summary>
public sealed class CharacterPickForm : SkinnedForm
{
	private readonly PixelLabel _lblTitle = new(), _lblSub = new();
	private readonly List<PixelButton> _buttons = new();

	/// <summary>Nhân vật đã chọn (0 mèo, 1 Sans); null nếu đóng hộp thoại.</summary>
	public int? Chosen { get; private set; }

	public CharacterPickForm()
	{
		AutoScaleMode = AutoScaleMode.None;
		FormBorderStyle = FormBorderStyle.FixedDialog;
		MaximizeBox = false;
		MinimizeBox = false;
		ShowInTaskbar = false;
		KeyPreview = true;
		StartPosition = FormStartPosition.CenterParent;
		Text = "Chọn nhân vật";
		ClientSize = new Size(760, 420);

		_lblTitle.Text = "CHỌN NHÂN VẬT";
		_lblTitle.PixelScale = 4;
		_lblTitle.ForeColor = Color.FromArgb(255, 219, 97);
		_lblSub.Text = "Chỉ khác về hình dáng, chỉ số giữ nguyên. Bấm một thẻ (hoặc phím 1-2). Esc = về menu";
		_lblSub.PixelScale = 1;
		_lblSub.ForeColor = Color.FromArgb(168, 176, 199);
		Controls.Add(_lblTitle);
		Controls.Add(_lblSub);
	}

	protected override void OnLoad(EventArgs e)
	{
		base.OnLoad(e);
		float s = DeviceDpi / 96f;
		int x = (int)(40 * s), width = ClientSize.Width - 2 * x, y = (int)(116 * s), gap = (int)(10 * s);
		_lblTitle.SetBounds(x, (int)(26 * s), width, (int)(44 * s));
		_lblSub.SetBounds(x, (int)(76 * s), width, (int)(30 * s));

		var options = new (int id, string title, string desc)[]
		{
			(CharacterSkin.Cat, "MÈO", "Chú mèo pixel quen thuộc."),
			(CharacterSkin.Sans, "SANS", "Ông chú xương lười biếng - mở khoá khi bạn đánh bại Sans. \"you're gonna have a bad time.\""),
		};
		for (int i = 0; i < options.Length; i++)
		{
			var (id, title, desc) = options[i];
			var b = new PixelButton
			{
				Text = $"[{i + 1}]  {title}\n{desc}",
				PixelScale = 2,
				Highlight = id == CharacterSkin.Selected,
				IconImage = CharacterSkin.Preview(id, 3),
				TextAlign = ContentAlignment.MiddleLeft,
				Location = new Point(x, y),
				Width = width,
				TabIndex = i,
			};
			b.Height = Math.Max(b.MeasureHeight(), (int)(110 * s));
			int pick = id;
			b.Click += (_, _) => Pick(pick);
			_buttons.Add(b);
			Controls.Add(b);
			y += b.Height + gap;
		}
		ClientSize = new Size(ClientSize.Width, y + (int)(24 * s) - gap);
		CenterToParent();
		if (_buttons.Count > 0) ActiveControl = _buttons[CharacterSkin.Selected];
	}

	/// <summary>Chọn một nhân vật như khi bấm thẻ (cho kiểm thử).</summary>
	internal void Pick(int id)
	{
		Chosen = id;
		Close();
	}

	protected override void OnKeyDown(KeyEventArgs e)
	{
		base.OnKeyDown(e);
		if (e.KeyCode == Keys.Escape) { Close(); return; }
		if (e.KeyCode is Keys.D1 or Keys.NumPad1) Pick(CharacterSkin.Cat);
		else if (e.KeyCode is Keys.D2 or Keys.NumPad2) Pick(CharacterSkin.Sans);
	}
}

/// <summary>Hiện màn chọn nhân vật trước ván (chỉ khi đã mở khoá Sans) và nhớ lựa chọn.</summary>
public static class CharacterPick
{
	/// <summary>Trả về false nếu người chơi đóng hộp thoại (về menu). Chưa mở khoá Sans thì không hỏi, luôn chơi mèo.</summary>
	public static bool Ask(IWin32Window owner = null)
	{
		if (!Settings.SansUnlocked)
		{
			Settings.Character = CharacterSkin.Cat;
			return true;
		}
		using var form = new CharacterPickForm();
		if (owner != null) form.ShowDialog(owner); else form.ShowDialog();
		if (form.Chosen == null) return false;
		Apply(form.Chosen.Value);
		return true;
	}

	/// <summary>Đặt nhân vật, lưu cài đặt và nạp lại phần HUD phụ thuộc nhân vật.</summary>
	public static void Apply(int character)
	{
		Settings.Character = Settings.SansUnlocked ? character : CharacterSkin.Cat;
		Settings.Save();
		HudBars.Reload();
	}
}
