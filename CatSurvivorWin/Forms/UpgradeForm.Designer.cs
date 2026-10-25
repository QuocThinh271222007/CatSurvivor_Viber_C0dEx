namespace CatSurvivor;

partial class UpgradeForm
{
	private System.ComponentModel.IContainer components = null;

	protected override void Dispose(bool disposing)
	{
		if (disposing && components != null) components.Dispose();
		base.Dispose(disposing);
	}

	#region Windows Form Designer generated code

	private void InitializeComponent()
	{
		lblTitle = new PixelLabel();
		lblSubtitle = new PixelLabel();
		SuspendLayout();
		//
		// lblTitle
		//
		lblTitle.ForeColor = Color.FromArgb(255, 219, 97);
		lblTitle.Location = new Point(20, 26);
		lblTitle.Name = "lblTitle";
		lblTitle.PixelScale = 4;
		lblTitle.Size = new Size(640, 44);
		lblTitle.Text = "LEVEL UP!";
		//
		// lblSubtitle
		//
		lblSubtitle.ForeColor = Color.FromArgb(168, 176, 199);
		lblSubtitle.Location = new Point(20, 76);
		lblSubtitle.Name = "lblSubtitle";
		lblSubtitle.PixelScale = 2;
		lblSubtitle.Size = new Size(640, 24);
		lblSubtitle.Text = "Chọn một phần thưởng (bấm hoặc phím 1 2 3)";
		//
		// UpgradeForm
		//
		AutoScaleDimensions = new SizeF(96F, 96F);
		AutoScaleMode = AutoScaleMode.Dpi;
		ClientSize = new Size(680, 400);
		Controls.Add(lblTitle);
		Controls.Add(lblSubtitle);
		FormBorderStyle = FormBorderStyle.FixedDialog;
		KeyPreview = true;
		MaximizeBox = false;
		MinimizeBox = false;
		Name = "UpgradeForm";
		ShowInTaskbar = false;
		StartPosition = FormStartPosition.CenterParent;
		Text = "Nâng cấp";
		FormClosing += UpgradeForm_FormClosing;
		KeyDown += UpgradeForm_KeyDown;
		ResumeLayout(false);
	}

	#endregion

	private PixelLabel lblTitle;
	private PixelLabel lblSubtitle;
}
