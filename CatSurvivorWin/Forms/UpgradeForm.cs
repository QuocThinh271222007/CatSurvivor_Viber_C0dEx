namespace CatSurvivor;

/// <summary>
/// Menu chọn nâng cấp khi lên level: game tạm dừng, người chơi chọn 1 trong các phần thưởng (bấm thẻ hoặc phím 1/2/3).
/// Danh sách lựa chọn do Upgrades.BuildChoices tạo; form chỉ hiển thị mỗi lựa chọn thành một thẻ (PixelButton): tiêu đề + mô tả.
/// Thẻ hợp nhất vũ khí được tô vàng. Giao diện dùng skin chung (xem docs/UI_ASSETS.md).
/// </summary>
public partial class UpgradeForm : SkinnedForm
{
	private readonly List<Upgrade> _choices;

	/// <summary>Phần thưởng đã chọn (GameForm áp dụng sau khi form đóng).</summary>
	public Upgrade Chosen { get; private set; }

	public UpgradeForm(List<Upgrade> choices, int level)
	{
		InitializeComponent();
		_choices = choices;
		lblTitle.Text = $"LEVEL UP!  Cấp {level}";
	}

	/// <summary>Tạo các thẻ sau khi WinForms đã scale form theo DPI (nên dùng toạ độ pixel thực, không bị scale đôi).</summary>
	protected override void OnLoad(EventArgs e)
	{
		base.OnLoad(e);

		// Mỗi lựa chọn một thẻ: dòng 1 = tên (to), phần sau = mô tả (nhỏ, tự xuống dòng). Chiều cao thẻ tự tính theo nội dung.
		var choices = _choices;
		float s = DeviceDpi / 96f;
		int x = (int)(40 * s), width = ClientSize.Width - 2 * x, y = (int)(116 * s), gap = (int)(10 * s);
		for (int i = 0; i < choices.Count; i++)
		{
			var u = choices[i];
			var b = new PixelButton
			{
				Text = $"[{i + 1}]  {u.Title}\n{u.Desc}",
				PixelScale = 2,
				Highlight = u.IsFusion,
				IconImage = u.Icon,
				TextAlign = ContentAlignment.MiddleLeft,
				Location = new Point(x, y),
				Width = width,
				TabIndex = i,
			};
			b.Height = Math.Max(b.MeasureHeight(), (int)(64 * s));
			b.Click += (_, _) => Pick(u);
			Controls.Add(b);
			y += b.Height + gap;
		}
		ClientSize = new Size(ClientSize.Width, y + (int)(24 * s) - gap);
		CenterToParent();
	}

	private void Pick(Upgrade u)
	{
		Chosen = u;
		Close();
	}

	private void UpgradeForm_KeyDown(object sender, KeyEventArgs e)
	{
		int i = e.KeyCode - Keys.D1;
		if (i < 0 || i >= _choices.Count) i = e.KeyCode - Keys.NumPad1;
		if (i >= 0 && i < _choices.Count) Pick(_choices[i]);
	}

	private void UpgradeForm_FormClosing(object sender, FormClosingEventArgs e)
	{
		// Bắt buộc phải chọn một phần thưởng (không đóng được bằng Alt+F4 khi chưa chọn)
		if (Chosen == null && _choices.Count > 0) e.Cancel = true;
	}
}
