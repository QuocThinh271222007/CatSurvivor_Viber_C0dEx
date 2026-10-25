namespace CatSurvivor;

/// <summary>
/// Màn hình thông tin nhóm (About): Họ tên / MSSV / Lớp đọc từ file thongtinnhom.txt (tự tạo file mẫu nếu chưa có).
/// Giao diện dùng skin chung (xem docs/UI_ASSETS.md).
/// </summary>
public partial class AboutForm : SkinnedForm
{
	public AboutForm()
	{
		InitializeComponent();

		tblTeam.AddColumn("Họ và tên", 260);
		tblTeam.AddColumn("MSSV", 150);
		tblTeam.AddColumn("Lớp", 210);

		var rows = Storage.ReadTeam(out string error);
		foreach (var r in rows) tblTeam.Rows.Add(new PixelTableRow { Cells = r });
		tblTeam.Invalidate();

		if (error != null)
		{
			lblError.Text = error;
			MessageBox.Show(this, error, "Lỗi thông tin nhóm", MessageBoxButtons.OK, MessageBoxIcon.Warning);
		}
		lblNote.Text = $"(Sửa thông tin nhóm trong file {Storage.TeamFile} cạnh file chạy)";
	}

	private void btnClose_Click(object sender, EventArgs e) => Close();
}
