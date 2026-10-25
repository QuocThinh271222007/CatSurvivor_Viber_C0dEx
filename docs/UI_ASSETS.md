# Asset giao diện menu (nút, nền, khung, logo)

Mọi menu (Start, Pause, Nâng cấp, Kết thúc, Bảng thành tích, Hướng dẫn, Thông tin nhóm) dùng chung một "da" giao diện
(`Rendering/UiSkin.cs`). Chưa có asset thì game **tự vẽ kiểu pixel-art** theo màu của game; có asset thì dùng asset.
Chỉ cần **thả file PNG vào `Assets/ui/`** (trong project, VS tự chép cạnh .exe khi build) rồi chạy lại – không phải sửa code.

| File (trong `Assets/ui/`) | Dùng cho | Ghi chú |
|---|---|---|
| `button_normal.png` | nút bình thường | **9-slice**; bắt buộc nếu muốn dùng nút ảnh |
| `button_hover.png` | khi rê chuột / đang chọn bằng phím | thiếu thì dùng `button_normal` sáng hơn |
| `button_pressed.png` | khi đang nhấn | thiếu thì dùng `button_normal` tối hơn |
| `button_disabled.png` | nút bị vô hiệu | thiếu thì dùng `button_normal` mờ xám |
| `panel.png` | khung bao ngoài của form | **9-slice** |
| `menu_bg.png` | ảnh nền menu | phủ kín form (xem `bg_mode`); nên 16:10 hoặc lớn hơn ~ 960x640 |
| `logo.png` | logo tên game ở menu chính | thay dải minh hoạ mèo/quái; nền trong suốt, nên rộng ~ 380x100 |
| `skin.txt` | cấu hình | xem bên dưới |

## 9-slice (nút và khung)
Ảnh được chia 3x3: 4 góc giữ nguyên, 4 cạnh giãn một chiều, phần giữa giãn hai chiều. Bạn vẽ **một** ảnh nhỏ (vd. nút 48x16 px)
và game kéo ra mọi kích thước. Trong `skin.txt`:
- `button_margin` / `panel_margin` = độ dày viền **trong ảnh** (px) – phần góc không bị méo.
- `button_scale` / `panel_scale` = phóng viền lên bao nhiêu lần khi vẽ (vẽ pixel-art nhỏ rồi phóng 2-3 lần cho sắc nét).

Ví dụ: nút 48x16 có viền 4px, muốn hiện viền dày 12px → `button_margin=4`, `button_scale=3`.
Ảnh nên dùng PNG có kênh trong suốt (góc bo/khuyết). Bắt buộc `margin*2 < ` chiều rộng và chiều cao ảnh.

## `skin.txt`
```
button_margin=6      # px trong ảnh
button_scale=2       # số lần phóng viền
panel_margin=6
panel_scale=2
bg_mode=cover        # cover | stretch | tile
accent=F2A633        # màu nhấn (hex RRGGBB), tuỳ chọn
text=F0F0F7          # màu chữ, tuỳ chọn
```

## Chữ trên nút / tiêu đề
Chữ luôn là font pixel Aseprite (`Assets/ui/aseprite_font.*`) nên đồng bộ với game. Dấu tiếng Việt còn thiếu trong font được
tự ghép lên chữ cái gốc. Màu chữ trên nút: bình thường trắng, rê chuột vàng, vô hiệu xám; có thể đổi `text` trong `skin.txt`.

## Muốn chỉnh bố cục
Mở form trong Designer (`Forms/*.cs`): `PixelButton` (nút), `PixelLabel` (chữ), `PixelTable` (bảng), `CatBanner` (dải minh hoạ),
`SkinnedForm` (form nền). Cỡ chữ đặt bằng thuộc tính `PixelScale` (1 = 8px, 2 = 16px, ...).
Thẻ nâng cấp (`UpgradeForm`) tự tính chiều cao theo nội dung nên asset nút cần co giãn được (9-slice).
