namespace CatSurvivor;

/// <summary>Màn hình hướng dẫn chơi. Nội dung nằm trong mảng Sections bên dưới - form tạm: tự thiết kế lại trong Designer.</summary>
public partial class HelpForm : SkinnedForm
{
	private static readonly (string Heading, string Body)[] Sections =
	{
		("MỤC TIÊU",
			"Điều khiển chú mèo sống sót giữa bầy quái ngày càng đông, nhặt XP để lên cấp (tối đa cấp 27). " +
			"Đạt cấp 27 sẽ mở trận đánh BOSS trong một vùng bị khoá. Hạ boss để chiến thắng."),
		("ĐIỀU KHIỂN",
			"• W A S D hoặc phím mũi tên: di chuyển\n" +
			"• Space hoặc Shift: DASH (lao nhanh, bất tử trong lúc lao, húc văng quái). Mỗi lượt hồi 20 giây\n" +
			"• Chuột: ngắm hướng cho Claw (vuốt) / Wave Slash; Bow (cung) tự ngắm quái gần nhất\n" +
			"• F: bật/tắt TỰ ĐỘNG NGẮM cho Bow / Holy Cross (tắt thì ngắm theo con trỏ chuột). Claw và Wave Slash luôn ngắm bằng chuột\n" +
			"• Q hoặc E: dùng KĨ NĂNG đã chọn bằng lá bài trước ván (mỗi ván chỉ mang MỘT trong 4 kĩ năng): SÓNG XUNG KÍCH (30 mana) - gây sát thương và hất văng quái quanh bạn, LÀM CHẬM THỜI GIAN (45 mana) - quái chậm lại 4 giây, NGỪNG THỜI GIAN (60 mana) - quái đứng yên 1,5 giây, hoặc BẤT TỬ (50 mana) - 2 giây. Mana (thanh xanh dương) tự hồi dần\n" +
				"• Esc hoặc P: tạm dừng   •   M: bật/tắt âm thanh   •   Cài đặt ở menu chính / Tạm dừng\n" +
			"Vũ khí tự động tấn công, bạn chỉ cần di chuyển và né."),
		("LÊN CẤP & NÂNG CẤP",
			"Mỗi lần lên cấp game tạm dừng và bạn chọn 1 trong 3 phần thưởng: vũ khí mới / nâng cấp vũ khí (tối đa cấp 8), " +
			"chỉ số (máu, tốc độ chạy, tốc độ bắn, nam châm...), giáp, trang bị (Mũ, Áo, Giày, Khiên - 5 cấp). " +
			"Khi 2 vũ khí cùng đạt cấp 5 sẽ có lựa chọn ★ HỢP NHẤT thành vũ khí mạnh hơn."),
		("QUÁI VẬT",
			"• Slime nhún đi vây quanh, Dơi bay lắt léo và gào thành 2 phễu hai bên, Bomber nước rút NHANH HƠN bạn để bắt kịp, châm ngòi rồi nổ (chạy ra xa hoặc dash để thoát), Brute (khủng long cầm KHIÊN) gồng rồi lao thẳng.\n" +
			"• Brute còn khiên thì đòn đánh chỉ trừ vào khiên (không mất máu): hãy đánh VỠ khiên - khiên vỡ thì Brute không lao nữa, chỉ còn đi chậm chậm theo bạn và bắt đầu mất máu.\n" +
			"• Quái đặc biệt (vòng đỏ dưới chân) có đòn vồ tới; quái khổng lồ rất trâu và khó bị đẩy.\n" +
			"• Quái xuất hiện theo từng đợt và đội hình (tường quét, đàn dơi chữ V, gọng kìm...). Hãy chú ý dải/vòng cảnh báo màu đỏ để né.\n" +
			"• Mỗi quái có giáp: đòn nhỏ bị giảm mạnh, cần đòn lớn/vũ khí nặng để xuyên giáp."),
		("TRẬN BOSS",
			"Boss có 3 đòn: LAO (đâm vào tường sẽ choáng - cơ hội đánh), GIẬM (phát vòng sóng: dùng DASH xuyên qua để né), " +
			"GẦM (triệu hồi quái). Boss có 3 GIAI ĐOẠN, mỗi giai đoạn một THANH MÁU riêng (cạn thanh thì sang giai đoạn kế, thanh đầy lại; thanh xanh = đang bất khả xâm phạm). Giai đoạn 2: 4 MẮT CON xuất hiện, mắt chính bất khả xâm phạm " +
			"cho tới khi bạn diệt hết chúng. Mắt con chạy trốn bạn và bắn chưởng (viên plasma); khi đủ 4 con chúng hợp thể thành CHƯỞNG CHỮ THẬP xoay vòng - " +
			"né bằng DASH hoặc diệt một con để phá chiêu. Diệt hết mắt con, mắt chính sẽ choáng lâu để bạn phản công. Giai đoạn 3 (nổi điên): XUẤT HIỆN THÊM một mắt chính nữa với thanh máu riêng, mỗi mắt có 4 mắt con (tổng 8) - phải hạ cả hai. " +
			"Ở độ Khó và Siêu khó, cứ 40 giây mắt chính lại gọi bù mắt con đã mất."),
		("ĐỘ KHÓ",
				"Chọn ở Cài đặt của menu chính (đang chơi không đổi được). Điểm thành tích được nhân theo độ khó:\n" +
				"• Dễ (điểm x1): quái bình thường, có vùng báo hiệu đòn đánh của quái.\n" +
				"• Trung bình (x2): quái máu và sát thương x2; quái bắt đầu dự đoán đường chạy của bạn (còn yếu); boss nhanh và tung đòn dồn dập hơn một chút.\n" +
				"• Khó (x4): quái máu và sát thương x4, chặn đầu hiệu quả, KHÔNG hiện vùng báo hiệu đòn đánh - phải nhìn hoạt ảnh của quái để né.\n" +
				"• Siêu khó (x6): quái máu và sát thương x6, dự đoán cực kì chuẩn, không thể hồi máu và không có thẻ nâng cấp máu (trừ giáp). Boss chọn đòn theo tình huống, lao bám theo bạn, tung đòn liên hoàn và không còn tự choáng.\n" +
				"Mẹo: đổi hướng đột ngột hoặc DASH để làm quái đoán sai."),
			("TÍNH ĐIỂM",
			"Điểm = (số quái hạ × 10 + (cấp − 1) × 100, hạ boss: +5000 và thưởng thêm nếu hoàn thành nhanh) × hệ số độ khó (x1 / x2 / x4 / x6). " +
			"Cuối ván nhập tên để lưu vào Bảng thành tích (file thanhtichs.txt)."),
		("BÍ MẬT",
			"Nghe đồn có một ông chú xương lười biếng, mặc áo hoodie xanh, đang chờ ai đó ở Bảng thành tích... " +
			"Thử nhập TÊN của ông ấy (chữ hoa hay thường đều được) khi lưu thành tích ở cuối ván. Đánh bại ông ấy sẽ mở khoá một nhân vật mới - " +
			"sau đó mỗi ván bạn sẽ được chọn chơi bằng Mèo hay nhân vật mới."),
	};

	public HelpForm()
	{
		InitializeComponent();
		StyleField(rtbHelp);
		rtbHelp.Font = new Font("Segoe UI", 11f);

		var bold = new Font(rtbHelp.Font, FontStyle.Bold);
		foreach (var (heading, body) in Sections)
		{
			rtbHelp.SelectionFont = bold;
			rtbHelp.SelectionColor = Color.FromArgb(255, 190, 80);
			rtbHelp.AppendText(heading + "\n");
			rtbHelp.SelectionFont = rtbHelp.Font;
			rtbHelp.SelectionColor = rtbHelp.ForeColor;
			rtbHelp.AppendText(body + "\n\n");
		}
		rtbHelp.SelectionStart = 0;
		rtbHelp.ScrollToCaret();
	}

	private void btnClose_Click(object sender, EventArgs e) => Close();
}
