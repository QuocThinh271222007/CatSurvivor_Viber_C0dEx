# Bảng thiết kế trang bị giáp (để vẽ asset)

Có 4 ô trang bị, mỗi ô 5 cấp (tên cấp: **Vải → Sắt → Thép → Mithril → Rồng**) = **20 món**. Nhận ở menu nâng cấp khi lên cấp; mỗi lần nâng là lên 1 cấp (đổi sang hình của cấp đó).
Game đã sẵn sàng nhận asset: chỉ cần thả file đúng tên vào đúng thư mục là **tự hiện** (chưa có file thì bỏ qua, game vẫn chạy).

**Đã có (bạn vẽ xong): cấp 1 (Vải, màu be) và cấp 2 (Sắt, hiệp sĩ có thánh giá vàng) của cả 4 món** = 8 file `helmet_n / chestplate_n / leg_n / shield_n.aseprite` trong `Documents\Aseprite\`. Còn thiếu cấp 3 (Thép), 4 (Mithril), 5 (Rồng).

> Lưu ý tên gọi: ô trang bị **"Khiên"** (đỡ hoàn toàn một đòn theo xác suất, mèo cầm bên cạnh) KHÁC với vũ khí **Holy Shield** (bong bóng thánh quanh mèo, đỡ trọn một đòn rồi vỡ - vẽ trong `holy.png`).

## 1. Quy cách file

| Loại | Đường dẫn | Kích thước | Ghi chú |
|---|---|---|---|
| **Ảnh món đồ** (đang dùng) | `Assets/equipment/icons/{helm|body|boots|shield}_{1..5}.png` | **64×64**, 1 khung, nền trong suốt (món vẽ ở giữa) | Dùng cho **icon ở thẻ nâng cấp** (nguyên bản) và **hàng icon dưới thanh mana ở HUD** (thu nhỏ 1/2). File nguồn: `Documents\Aseprite\{helmet|chestplate|leg|shield}_{n}.aseprite` (`tools\export_assets.ps1` tự xuất). |
| ~~Lớp phủ lên mèo~~ (đã bỏ) | | | **Mèo KHÔNG mặc giáp trên người** (theo yêu cầu): giáp chỉ cho chỉ số và hiện ở HUD / thẻ nâng cấp, nên không cần vẽ lớp phủ 192×32 nữa. Các mục 2 và "6 khung" bên dưới chỉ để tham khảo. |

Ví dụ: `helm_3.png` = Mũ Sắt (cấp 3); `icons/boots_5.png` = icon Giày Mithril.

**6 khung của dải lớp phủ** (giống main_character): `0-3` = chu kỳ đi/đứng yên (vẽ theo từng khung vì mèo hơi nhún) · `4` = khung nháy trắng khi trúng đòn (**để trống**, game tự ẩn lớp phủ ở khung này) · `5` = khung chết.
Mẹo: mở `main_character.aseprite`, thêm layer mới cho từng món, vẽ chồng lên mèo, tắt layer mèo rồi xuất dải ngang 192×32.

## 2. Bố cục mèo trong khung 32×32 (để biết vẽ chỗ nào)

Mèo chiếm cột **7-23**, hàng **4-27** (khung 0). Mèo đen, tai hồng (hàng 4-8, cột 10-11 và 16-17), **mắt trắng** ở hàng 13-14 (cột 14-15 và 20-21), **cổ áo đỏ** hàng 19-20 (cột 11-22), thân hàng 20-26, đuôi bên trái (cột 8-10, hàng 20-23), **chân trắng** hàng 27 (cột 14-15 và 18-19). Mèo nhìn thẳng; game tự **lật ngang** khi mèo quay trái nên vẽ cân đối hoặc chấp nhận bị lật.

| Ô | Vị trí vẽ trên mèo | Thứ tự vẽ |
|---|---|---|
| Mũ (`helm`) | đầu + hai tai (hàng 4-12, cột 7-23); chừa mắt ở hàng 13-14 trừ khi vẽ mặt nạ | lớp 3 (1 = dưới cùng, 4 = trên cùng) |
| Áo giáp (`body`) | thân + cổ áo (hàng 19-26, cột 10-22); tay/vai nếu có thì nhô ra hai bên | lớp 2 (1 = dưới cùng, 4 = trên cùng) |
| Giày (`boots`) | hai bàn chân (hàng 26-28; chân trái cột 13-16, chân phải cột 17-20) | lớp 1 (1 = dưới cùng, 4 = trên cùng) |
| Khiên (`shield`) | cầm bên cạnh thân (hàng 18-27, cột 19-27, phía phải mèo khi nhìn phải; tự lật khi mèo quay trái) | lớp 4 (1 = dưới cùng, 4 = trên cùng) |

## 3. Bảng 20 món: tên, chỉ số, gợi ý màu

Chỉ số tính theo **cấp của món** (không cộng dồn giữa các cấp). Tổng giáp của mèo = giáp gốc (tối đa 10) + giáp trang bị, **tối đa 24**. Mỗi 1 giáp làm mèo nặng thêm 0.05.
Màu gợi ý đi theo cấp để người chơi nhìn là biết món mạnh hay yếu (cùng tông với biến thể màu của quái):

- **Cấp 1 – Vải**: be/xám nhạt (#C8B89A, bóng #8E8068)
- **Cấp 2 – Sắt**: bạc xám xanh (#A9B7C6, bóng #6F7F8F)
- **Cấp 3 – Thép**: thép đậm có viền sáng (#5F7084, sáng #C5D3E0)
- **Cấp 4 – Mithril**: xanh ngọc mithril phát sáng (#4FE0D0, sáng #E6FFFB)
- **Cấp 5 – Rồng**: đỏ sẫm vảy rồng + viền vàng (#8C1C1C, sáng #FFC84A)

### Mũ (`helm`)

| Cấp | Tên | File lớp phủ | File icon | Giáp | Hiệu ứng |
|---|---|---|---|---|---|
| 1 | Mũ Vải | `helm_1.png` | `icons/helm_1.png` | +1.5 | giảm choáng 15% |
| 2 | Mũ Sắt | `helm_2.png` | `icons/helm_2.png` | +3 | giảm choáng 30% |
| 3 | Mũ Thép | `helm_3.png` | `icons/helm_3.png` | +4.5 | giảm choáng 45% |
| 4 | Mũ Mithril | `helm_4.png` | `icons/helm_4.png` | +6 | giảm choáng 60% |
| 5 | Mũ Rồng | `helm_5.png` | `icons/helm_5.png` | +7.5 | giảm choáng 75% |

### Áo giáp (`body`)

| Cấp | Tên | File lớp phủ | File icon | Giáp | Hiệu ứng |
|---|---|---|---|---|---|
| 1 | Áo giáp Vải | `body_1.png` | `icons/body_1.png` | +3 | máu tối đa +15, tốc độ -3% |
| 2 | Áo giáp Sắt | `body_2.png` | `icons/body_2.png` | +6 | máu tối đa +30, tốc độ -6% |
| 3 | Áo giáp Thép | `body_3.png` | `icons/body_3.png` | +9 | máu tối đa +45, tốc độ -9% |
| 4 | Áo giáp Mithril | `body_4.png` | `icons/body_4.png` | +12 | máu tối đa +60, tốc độ -12% |
| 5 | Áo giáp Rồng | `body_5.png` | `icons/body_5.png` | +15 | máu tối đa +75, tốc độ -15% |

### Giày (`boots`)

| Cấp | Tên | File lớp phủ | File icon | Giáp | Hiệu ứng |
|---|---|---|---|---|---|
| 1 | Giày Vải | `boots_1.png` | `icons/boots_1.png` | +1 | tốc độ +4%, giảm bị đẩy lùi 10% |
| 2 | Giày Sắt | `boots_2.png` | `icons/boots_2.png` | +2 | tốc độ +8%, giảm bị đẩy lùi 20% |
| 3 | Giày Thép | `boots_3.png` | `icons/boots_3.png` | +3 | tốc độ +12%, giảm bị đẩy lùi 30% |
| 4 | Giày Mithril | `boots_4.png` | `icons/boots_4.png` | +4 | tốc độ +16%, giảm bị đẩy lùi 40% |
| 5 | Giày Rồng | `boots_5.png` | `icons/boots_5.png` | +5 | tốc độ +20%, giảm bị đẩy lùi 50% |

### Khiên (`shield`)

| Cấp | Tên | File lớp phủ | File icon | Giáp | Hiệu ứng |
|---|---|---|---|---|---|
| 1 | Khiên Vải | `shield_1.png` | `icons/shield_1.png` | +2 | đỡ hoàn toàn đòn 4% |
| 2 | Khiên Sắt | `shield_2.png` | `icons/shield_2.png` | +4 | đỡ hoàn toàn đòn 8% |
| 3 | Khiên Thép | `shield_3.png` | `icons/shield_3.png` | +6 | đỡ hoàn toàn đòn 12% |
| 4 | Khiên Mithril | `shield_4.png` | `icons/shield_4.png` | +8 | đỡ hoàn toàn đòn 16% |
| 5 | Khiên Rồng | `shield_5.png` | `icons/shield_5.png` | +10 | đỡ hoàn toàn đòn 20% |

## 4. Gợi ý thiết kế từng món (hình dáng)

- **Mũ**: Vải = mũ tròn be (đã vẽ) · Sắt = mũ trụ hiệp sĩ có thánh giá vàng (đã vẽ) · Thép = mũ giáp có mào/sừng nhỏ · Mithril = mũ miện phát sáng · Rồng = mũ có sừng rồng.
- **Áo giáp**: Vải = áo vải be (đã vẽ) · Sắt = giáp ngực sắt có vai gai (đã vẽ) · Thép = giáp thép có vai lớn · Mithril = giáp ngọc có hoa văn sáng · Rồng = giáp vảy rồng.
- **Giày/quần** (file `leg_n`): Vải = quần be (đã vẽ) · Sắt = giáp chân sắt (đã vẽ) · Thép = ủng thép có gai · Mithril = ủng phát sáng · Rồng = ủng vảy rồng.
- **Khiên**: Vải = khiên gỗ tròn (đã vẽ) · Sắt = khiên tròn sắt có thánh giá (đã vẽ) · Thép = khiên thép có gai giữa · Mithril = khiên phát sáng (hợp với vòng sáng BLOCK) · Rồng = khiên mặt rồng.


## 5. Cách kiểm tra

1. Vẽ trong Aseprite rồi xuất, hoặc thả thẳng file PNG vào `Assets/equipment/`. File nguồn ảnh món đồ: `Documents\Aseprite\{helmet|chestplate|leg|shield}_{1..5}.aseprite` -> `tools\export_assets.ps1` tự xuất vào `Assets/equipment/icons/`. (Lớp phủ 192×32 tuỳ chọn: `Documents\Aseprite\equipment\{helm|body|boots|shield}_{1..5}.aseprite`.)
2. Chạy game bằng `tools\play.ps1` (bản Debug có cheat): gõ `F1` rồi `equip all 2` (hoặc `equip helm 2`...) để nhận giáp ngay, không cần chờ lên cấp; icon hiện dưới thanh mana.
