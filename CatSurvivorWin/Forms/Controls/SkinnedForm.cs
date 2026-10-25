using System.ComponentModel;

namespace CatSurvivor;

/// <summary>
/// Form nền chung của mọi menu: vẽ nền (menu_bg.png hoặc nền tự vẽ) + khung panel theo skin (UiSkin).
/// Kế thừa SkinnedForm thay cho Form là form tự có giao diện thống nhất; control con dùng PixelButton / PixelLabel / PixelTable.
/// </summary>
public class SkinnedForm : Form
{
	private readonly Canvas _canvas = new();

	/// <summary>Vẽ khung panel bao quanh nội dung (tắt nếu ảnh nền đã có sẵn khung).</summary>
	[DefaultValue(true)] public bool ShowPanel { get; set; } = true;

	public SkinnedForm()
	{
		SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
		DoubleBuffered = true;
		BackColor = UiSkin.FormBack;
		ForeColor = Color.FromArgb(235, 236, 244);
		Font = new Font("Segoe UI", 9.75f);
	}

	protected override void OnPaintBackground(PaintEventArgs e)
	{
		try
		{
			_canvas.Begin(e.Graphics, 1f, PointF.Empty);
			var r = ClientRectangle;
			UiSkin.DrawBackground(_canvas, r, DeviceDpi);
			if (ShowPanel)
			{
				int inset = (int)(10 * DeviceDpi / 96f);
				UiSkin.DrawPanel(_canvas, Rectangle.Inflate(r, -inset, -inset), DeviceDpi);
			}
		}
		catch (Exception)
		{
			base.OnPaintBackground(e); // lỗi skin thì dùng nền phẳng, không văng
		}
	}

	/// <summary>Style chung cho ô nhập/đọc văn bản gốc của WinForms (TextBox, RichTextBox) cho hợp tông.</summary>
	protected static void StyleField(Control c)
	{
		c.BackColor = UiSkin.FieldBack;
		c.ForeColor = UiSkin.FieldText;
		c.Font = new Font("Segoe UI", 11f);
		if (c is TextBoxBase tb) tb.BorderStyle = BorderStyle.FixedSingle;
	}
}
