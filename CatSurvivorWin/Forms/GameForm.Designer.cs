namespace CatSurvivor;

partial class GameForm
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
		components = new System.ComponentModel.Container();
		canvas = new GameCanvas();
		gameTimer = new System.Windows.Forms.Timer(components);
		SuspendLayout();
		//
		// canvas
		//
		canvas.Dock = DockStyle.Fill;
		canvas.Location = new Point(0, 0);
		canvas.Name = "canvas";
		canvas.TabIndex = 0;
		//
		// gameTimer
		//
		gameTimer.Interval = 8;
		gameTimer.Tick += gameTimer_Tick;
		//
		// GameForm
		//
		AutoScaleMode = AutoScaleMode.None;
		BackColor = Color.Black;
		ClientSize = new Size(1280, 720);
		Controls.Add(canvas);
		DoubleBuffered = true;
		KeyPreview = true;
		MinimumSize = new Size(656, 399);
		Name = "GameForm";
		StartPosition = FormStartPosition.CenterScreen;
		Text = "Cat Survivor";
		Deactivate += GameForm_Deactivate;
		KeyDown += GameForm_KeyDown;
		KeyUp += GameForm_KeyUp;
		ResumeLayout(false);
	}

	#endregion

	private GameCanvas canvas;
	private System.Windows.Forms.Timer gameTimer;
}
