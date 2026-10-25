namespace CatSurvivor;

partial class CheatForm
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
		lblPrompt = new Label();
		txtCommand = new TextBox();
		txtOutput = new TextBox();
		SuspendLayout();
		//
		// lblPrompt
		//
		lblPrompt.Font = new Font("Consolas", 10F, FontStyle.Bold);
		lblPrompt.Location = new Point(12, 12);
		lblPrompt.Name = "lblPrompt";
		lblPrompt.Size = new Size(20, 24);
		lblPrompt.Text = ">";
		lblPrompt.TextAlign = ContentAlignment.MiddleLeft;
		//
		// txtCommand
		//
		txtCommand.Font = new Font("Consolas", 11F);
		txtCommand.Location = new Point(34, 10);
		txtCommand.Name = "txtCommand";
		txtCommand.Size = new Size(626, 26);
		txtCommand.KeyDown += txtCommand_KeyDown;
		//
		// txtOutput
		//
		txtOutput.BackColor = Color.FromArgb(30, 30, 30);
		txtOutput.Font = new Font("Consolas", 9.5F);
		txtOutput.ForeColor = Color.Gainsboro;
		txtOutput.Location = new Point(12, 46);
		txtOutput.Multiline = true;
		txtOutput.Name = "txtOutput";
		txtOutput.ReadOnly = true;
		txtOutput.ScrollBars = ScrollBars.Vertical;
		txtOutput.Size = new Size(648, 330);
		txtOutput.TabStop = false;
		//
		// CheatForm
		//
		AutoScaleDimensions = new SizeF(96F, 96F);
		AutoScaleMode = AutoScaleMode.Dpi;
		ClientSize = new Size(672, 388);
		Controls.Add(lblPrompt);
		Controls.Add(txtCommand);
		Controls.Add(txtOutput);
		FormBorderStyle = FormBorderStyle.FixedToolWindow;
		Name = "CheatForm";
		ShowInTaskbar = false;
		StartPosition = FormStartPosition.CenterParent;
		Text = "Cheat console (debug) - gõ help";
		ResumeLayout(false);
		PerformLayout();
	}

	#endregion

	private Label lblPrompt;
	private TextBox txtCommand;
	private TextBox txtOutput;
}
