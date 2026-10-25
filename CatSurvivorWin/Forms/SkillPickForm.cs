using System.ComponentModel;
using System.Numerics;

namespace CatSurvivor;

/// <summary>Một lá bài trong màn chọn kĩ năng: hình lá bài + tên + mô tả.</summary>
public sealed class SkillCardInfo
{
	public SkillKind Kind;
	public string Name, Desc;
	public Bitmap Art;
}

/// <summary>
/// Màn chọn kĩ năng trước ván, hiện như một hàng LÁ BÀI poker: rê chuột / Tab vào lá nào thì lá đó nhô lên và mô tả hiện bên dưới;
/// bấm lá (hoặc phím 1-4, Enter) để chọn, Esc để về menu. Hình lá bài phóng NGUYÊN số lần (pixel-art sắc nét).
/// </summary>
public sealed class SkillPickForm : SkinnedForm
{
	private readonly List<SkillCardInfo> _cards;
	private readonly List<CardControl> _controls = new();
	private readonly PixelLabel _lblTitle = new(), _lblSub = new(), _lblDesc = new();

	/// <summary>Kĩ năng đã chọn (null nếu đóng hộp thoại).</summary>
	public SkillKind? Chosen { get; private set; }

	public SkillPickForm(List<SkillCardInfo> cards)
	{
		_cards = cards;
		AutoScaleMode = AutoScaleMode.None; // toạ độ tính thẳng bằng pixel thật theo DPI trong OnLoad
		FormBorderStyle = FormBorderStyle.FixedDialog;
		MaximizeBox = false;
		MinimizeBox = false;
		ShowInTaskbar = false;
		KeyPreview = true;
		StartPosition = FormStartPosition.CenterParent;
		Text = "Chọn kĩ năng";
		ClientSize = new Size(900, 560);

		_lblTitle.Text = "CHỌN KĨ NĂNG";
		_lblTitle.PixelScale = 4;
		_lblTitle.ForeColor = Color.FromArgb(255, 219, 97);
		_lblSub.Text = "Mỗi ván chỉ mang MỘT kĩ năng, dùng bằng phím Q hoặc E. Bấm một lá bài (hoặc phím 1-4). Esc = về menu";
		_lblSub.PixelScale = 1;
		_lblSub.ForeColor = Color.FromArgb(168, 176, 199);
		_lblDesc.PixelScale = 1;
		_lblDesc.ForeColor = Color.FromArgb(235, 236, 244);
		Controls.Add(_lblTitle);
		Controls.Add(_lblSub);
		Controls.Add(_lblDesc);
	}

	protected override void OnLoad(EventArgs e)
	{
		base.OnLoad(e);
		float sc = DeviceDpi / 96f;
		int f = UiSkin.PixelFactor(DeviceDpi);
		int pad = (int)(28 * sc), gap = (int)(16 * sc);

		// Cỡ phóng lá bài: 2 lần nếu cả hàng + chữ còn vừa màn hình, không thì 1 lần
		var work = Screen.FromControl(this).WorkingArea;
		int maxW = _cards.Max(c => c.Art.Width), maxH = _cards.Max(c => c.Art.Height);
		int k = 2 * f;
		int nameH() => (int)(2 * (PixelFont.LineHeight + 2) * 2 * f);
		int Total() => (int)(130 * sc) + maxH * k + (int)(20 * sc) + nameH() + (int)(100 * sc);
		while (k > 1 && (Total() > work.Height - 80 || 4 * maxW * k + 3 * gap + 2 * pad > work.Width - 40)) k--;

		int cellW = maxW * k, raise = (int)(12 * sc), margin = (int)(6 * sc);
		int rowW = _cards.Count * cellW + (_cards.Count - 1) * gap;
		int clientW = Math.Max(rowW + 2 * pad, (int)(640 * sc));
		int y = (int)(120 * sc);

		_lblTitle.SetBounds(pad, (int)(26 * sc), clientW - 2 * pad, (int)(44 * sc));
		_lblSub.SetBounds(pad, (int)(76 * sc), clientW - 2 * pad, (int)(30 * sc));

		int ctlH = raise + maxH * k + (int)(10 * sc) + nameH();
		int x = (clientW - rowW) / 2;
		for (int i = 0; i < _cards.Count; i++)
		{
			// Control rộng hơn lá bài một chút để viền vàng khi nhô lên không bị cắt
			var c = new CardControl(_cards[i], i + 1, k, raise) { Location = new Point(x - margin, y), Size = new Size(cellW + 2 * margin, ctlH), TabIndex = i };
			c.Highlighted += (_, _) => ShowDesc(c);
			c.Picked += (_, _) => Pick(c.Info);
			_controls.Add(c);
			Controls.Add(c);
			x += cellW + gap;
		}

		_lblDesc.SetBounds(pad, y + ctlH + (int)(8 * sc), clientW - 2 * pad, (int)(64 * sc));
		ClientSize = new Size(clientW, y + ctlH + (int)(8 * sc) + _lblDesc.Height + (int)(28 * sc));
		CenterToParent();
		ShowDesc(_controls[0]);
	}

	/// <summary>Chọn lá thứ i (0..) như khi rê chuột vào (cho kiểm thử).</summary>
	internal void Highlight(int i) => ShowDesc(_controls[i]);

	private void ShowDesc(CardControl c)
	{
		foreach (var o in _controls) o.Active = o == c;
		_lblDesc.Text = $"{c.Info.Name}: {c.Info.Desc}";
	}

	private void Pick(SkillCardInfo info)
	{
		Chosen = info.Kind;
		Close();
	}

	protected override void OnKeyDown(KeyEventArgs e)
	{
		base.OnKeyDown(e);
		if (e.KeyCode == Keys.Escape) { Close(); return; }
		int i = e.KeyCode - Keys.D1;
		if (i < 0 || i >= _cards.Count) i = e.KeyCode - Keys.NumPad1;
		if (i >= 0 && i < _cards.Count) Pick(_cards[i]);
	}

	/// <summary>Một lá bài tự vẽ: nhô lên khi rê chuột/focus, viền vàng khi đang chọn.</summary>
	private sealed class CardControl : Control
	{
		private readonly Canvas _canvas = new();
		private readonly int _scale, _raise;

		public SkillCardInfo Info { get; }
		public int Number { get; }
		private bool _active;
		public bool Active { get => _active; set { if (_active == value) return; _active = value; Parent?.Invalidate(Bounds); } } // vẽ lại cả vùng nền phía sau (control trong suốt)
		public event EventHandler Highlighted, Picked;

		public CardControl(SkillCardInfo info, int number, int scale, int raise)
		{
			Info = info; Number = number; _scale = scale; _raise = raise;
			SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
				| ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor | ControlStyles.Selectable, true);
			BackColor = Color.Transparent;
			Cursor = Cursors.Hand;
			TabStop = true;
		}

		// Chỉ MỘT lá được chọn tại một thời điểm (lá rê chuột / focus gần nhất): lá đó nhô lên + viền vàng, các lá khác nằm yên
		private bool Lifted => Active;

		protected override void OnMouseEnter(EventArgs e) { Highlighted?.Invoke(this, e); base.OnMouseEnter(e); }
		protected override void OnGotFocus(EventArgs e) { Highlighted?.Invoke(this, e); base.OnGotFocus(e); }
		protected override void OnClick(EventArgs e) { SoundService.Play("click", -6f, 0.03f); Picked?.Invoke(this, e); }
		protected override bool IsInputKey(Keys keyData) => keyData is Keys.Enter or Keys.Space || base.IsInputKey(keyData);
		protected override void OnKeyDown(KeyEventArgs e)
		{
			if (e.KeyCode is Keys.Enter or Keys.Space) { e.Handled = true; OnClick(EventArgs.Empty); return; }
			base.OnKeyDown(e);
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			try
			{
				_canvas.Begin(e.Graphics, 1f, PointF.Empty);
				int f = UiSkin.PixelFactor(DeviceDpi);
				var art = Info.Art;
				int w = art.Width * _scale, h = art.Height * _scale;
				float top = Lifted ? 4 * f : _raise; // nhô lên khi rê chuột / focus
				float x = (Width - w) / 2f;

				// Viền vàng quanh lá bài đang chọn / rê chuột
				if (Lifted)
				{
					int b = 3 * f;
					_canvas.FillRect(x - b, top - b, w + 2 * b, h + 2 * b, UiSkin.AccentBright);
				}
				// Bóng đổ dưới lá bài
				_canvas.FillRect(x + 4 * f, top + 4 * f, w, h, new Rgba(0f, 0f, 0f, 0.45f));
				_canvas.DrawTexture(art, new Rect2(x, top, w, h), Lifted ? Rgba.White : new Rgba(0.82f, 0.82f, 0.88f));

				// Tên kĩ năng dưới lá bài
				int ts = 2 * f;
				float ty = top + h + 10 * f;
				var col = Lifted ? UiSkin.AccentBright : UiSkin.Text;
				foreach (var line in PixelFont.Wrap(_canvas, $"[{Number}] {Info.Name}", Width, ts).Take(2))
				{
					float lw = PixelFont.Measure(_canvas, line, ts);
					PixelFont.Draw(_canvas, line, new Vector2((Width - lw) / 2f + f, ty + f), ts, new Rgba(0f, 0f, 0f, 0.7f));
					PixelFont.Draw(_canvas, line, new Vector2((Width - lw) / 2f, ty), ts, col);
					ty += (PixelFont.LineHeight + 2) * ts;
				}
			}
			catch (Exception) { base.OnPaint(e); }
		}
	}
}