# Xuất lại asset từ file Aseprite của bạn vào Assets/ (chạy lại mỗi khi sửa file .aseprite):
#   powershell -ExecutionPolicy Bypass -File tools\export_assets.ps1
# Cần Aseprite (đường dẫn bên dưới) và các file .aseprite trong thư mục $SourceDir.
param(
  [string]$Aseprite = "C:\Program Files (x86)\Steam\steamapps\common\Aseprite\aseprite.exe",
  [string]$SourceDir = "$env:USERPROFILE\Documents\Aseprite"
)
$assets = Join-Path $PSScriptRoot "..\CatSurvivorWin\Assets"

# file .aseprite -> đường dẫn PNG trong Assets (xuất thành dải ngang các khung hình)
$map = @(
  @{ From = "Bow.aseprite";   To = "weapons\bow.png"   },   # 4 khung 64x64: 0 thả dây (+ mũi tên riêng ở góc trên), 1-3 kéo dây
  @{ From = "knife.aseprite"; To = "weapons\knife.png" },   # dao, mũi hướng LÊN
  @{ From = "Claw.aseprite";  To = "weapons\claw.png"  },   # găng vuốt, mũi hướng sang PHẢI
  @{ From = "skills.aseprite"; To = "ui\skills.png" },   # ICON KĨ NĂNG (HUD): dải ngang 4 khung vuông 64x64: 0 đồng hồ (Làm chậm thời gian), 1 hình vàng (Bất tử), 2 bông tuyết (Ngừng thời gian), 3 sóng trắng (Sóng xung kích); game cắt sát hình khi vẽ ở HUD; chưa vẽ thì bỏ qua
  @{ From = "poker.aseprite"; To = "ui\poker.png" },   # LÁ BÀI poker dùng chung cho màn chọn kĩ năng (1 khung; game tự cắt sát viền, phóng nguyên số lần). Mỗi kĩ năng muốn hình riêng thì thêm skill_card_*.aseprite bên dưới
  @{ From = "skill_card_nova.aseprite"; To = "ui\skill_card_nova.png" },   # LÁ BÀI chọn kĩ năng Sóng xung kích (màn chọn trước ván, 1 khung, nên cỡ 64-192px); chưa vẽ thì thẻ chỉ có chữ
  @{ From = "skill_card_warp.aseprite"; To = "ui\skill_card_warp.png" },   # LÁ BÀI chọn kĩ năng Làm chậm thời gian
  @{ From = "skill_card_stop.aseprite"; To = "ui\skill_card_stop.png" },   # LÁ BÀI kĩ năng Ngừng thời gian
  @{ From = "skill_card_guard.aseprite"; To = "ui\skill_card_guard.png" },   # LÁ BÀI kĩ năng Bất tử
  @{ From = "small eye ball.aseprite"; To = "enemies\minieye.png" },   # MẮT CON (giai đoạn 2 của boss): 6 khung 64x64, con mắt NHÌN SANG TRÁI (đuôi phấp phới bên phải); khung 1 = TRẮNG hoàn toàn (bị đánh), khung 0,2,3,4,5 = bay; game tự lật theo hướng mèo
  @{ From = "plasma-eyeball skill.aseprite"; To = "enemies\plasma.png" },   # CHƯỞNG của mắt con / chùm tia chữ thập: 1 khung, dải plasma nằm NGANG (game tự cắt sát viền, kéo giãn theo chiều dài, xoay theo hướng)
  @{ From = "Sans.aseprite"; To = "characters\sans.png" },   # NHÂN VẬT SANS (mở khoá khi đánh bại Sans - easter egg nhập tên SANS): 5 khung 64x64, 0-1 = chạy (nhìn PHẢI), 2-4 = đứng yên (nhìn thẳng); game vẽ nguyên cỡ, chân ở khoảng y=47
  @{ From = "brute.aseprite"; To = "enemies\brute.png" },   # BRUTE (khủng long cầm khiên, nhìn TRÁI): 10 khung 64x64 - 0 gồng/lao · 1-2 KHIÊN rời (vẽ đè lên thân khi còn khiên; để trống thì game vẽ khiên tạm) · 3,6,7,8 đi · 4 trắng (dáng có khiên) · 5 trắng (bị đánh, không khiên) · 9 kiệt sức/chết. Còn khiên thì lao tới; khiên vỡ thì chỉ đi chậm
  @{ From = "EyeBoss.aseprite"; To = "enemies\eyeboss.png" },   # BOSS: 4 khung 64x64 (con mắt đảo tròng), mắt nằm giữa khung
  @{ From = "holy shield and holy cross.aseprite"; To = "weapons\holy.png" }   # 2 khung 64x64: 0 = thánh giá (Holy Cross), 1 = khiên/bong bóng thánh (Holy Shield)
)

if (-not (Test-Path $Aseprite)) { Write-Error "Không thấy Aseprite: $Aseprite (dùng -Aseprite để chỉ đường dẫn)"; exit 1 }
foreach ($m in $map) {
  $src = Join-Path $SourceDir $m.From
  $dst = Join-Path $assets $m.To
  if (-not (Test-Path $src)) { Write-Warning "Bỏ qua (không thấy): $src"; continue }
  New-Item -ItemType Directory -Force (Split-Path $dst) | Out-Null
  & $Aseprite -b $src --sheet $dst --sheet-type horizontal | Out-Null
  Write-Host "OK  $($m.From)  ->  Assets\$($m.To)"
}

# ẢNH MÓN GIÁP (icon menu/HUD + mèo mang trên người): Documents\Aseprite\{helmet|chestplate|leg|shield}_{1..5}.aseprite (1 khung 64x64, nền trong suốt) -> Assets\equipment\icons\{helm|body|boots|shield}_{n}.png. Game tự cắt sát viền và thu nhỏ 1/2 khi mèo mang. Món nào chưa vẽ thì bỏ qua (im lặng).
$itemFiles = @{ helm = "helmet"; body = "chestplate"; boots = "leg"; shield = "shield" }
foreach ($slot in "helm","body","boots","shield") {
  foreach ($lv in 1..5) {
    $src = Join-Path $SourceDir "$($itemFiles[$slot])_$lv.aseprite"
    if (-not (Test-Path $src)) { continue }
    $dst = Join-Path $assets "equipment\icons\${slot}_$lv.png"
    New-Item -ItemType Directory -Force (Split-Path $dst) | Out-Null
    & $Aseprite -b $src --sheet $dst --sheet-type horizontal | Out-Null
    Write-Host "OK  $($itemFiles[$slot])_$lv.aseprite  ->  Assets\equipment\icons\${slot}_$lv.png"
  }
}
# Trang bị giáp: Documents\Aseprite\equipment\{helm|body|boots|shield}_{1..5}.aseprite -> Assets\equipment\{...}.png (dải ngang 6 khung 32x32 = 192x32, xem docs/ARMOR_DESIGN.md). Món nào chưa vẽ thì bỏ qua (im lặng).
foreach ($slot in "helm","body","boots","shield") {
  foreach ($lv in 1..5) {
    $src = Join-Path $SourceDir "equipment\${slot}_$lv.aseprite"
    if (-not (Test-Path $src)) { continue }
    $dst = Join-Path $assets "equipment\${slot}_$lv.png"
    New-Item -ItemType Directory -Force (Split-Path $dst) | Out-Null
    & $Aseprite -b $src --sheet $dst --sheet-type horizontal | Out-Null
    Write-Host "OK  equipment\${slot}_$lv.aseprite  ->  Assets\equipment\${slot}_$lv.png"
  }
}
# Thanh máu / mana (HUD): cùng một khung 256x256, tách đầu mèo (Layer 2) ra file riêng để game vẽ đầu mèo ĐÈ LÊN phần máu đang đầy.
#   hud_bars.png = mọi layer trừ "Layer 2" (thanh đỏ máu, xanh dương mana, xanh lá XP - game tự dò vị trí các thanh từ trên xuống)
#   hud_head.png = chỉ layer "Layer 2" (đầu + hai chân mèo thò lên trên thanh máu)
$bars = Join-Path $SourceDir "heal bar and mana bar.aseprite"
if (Test-Path $bars) {
  New-Item -ItemType Directory -Force (Join-Path $assets "ui") | Out-Null
  & $Aseprite -b $bars --ignore-layer "Layer 2" --save-as (Join-Path $assets "ui\hud_bars.png") | Out-Null
  & $Aseprite -b $bars --layer "Layer 2" --save-as (Join-Path $assets "ui\hud_head.png") | Out-Null
  Write-Host "OK  heal bar and mana bar.aseprite  ->  Assets\ui\hud_bars.png + hud_head.png"
} else { Write-Warning "Bỏ qua (không thấy): $bars" }

# (Tuỳ chọn) đầu Sans thò lên trên thanh máu khi chơi nhân vật Sans: cùng khung 256x256 như hud_head.png (layer "Layer 2" của file "hud head sans.aseprite", hoặc cả file nếu chỉ có 1 layer).
# Chưa vẽ thì khi chơi Sans thanh máu không có đầu (không dùng đầu mèo).
$sansHead = Join-Path $SourceDir "hud head sans.aseprite"
if (Test-Path $sansHead) {
  New-Item -ItemType Directory -Force (Join-Path $assets "ui") | Out-Null
  & $Aseprite -b $sansHead --save-as (Join-Path $assets "ui\hud_head_sans.png") | Out-Null
  Write-Host "OK  hud head sans.aseprite  ->  Assets\ui\hud_head_sans.png"
}

# Thanh máu BOSS (HUD): cùng một khung 256x256, "Layer 1" = thanh (viền đen + ruột đỏ sọc), các layer còn lại = đám mắt thò lên trên thanh (vẽ ĐÈ lên phần máu).
#   boss_bar.png = chỉ "Layer 1" · boss_bar_head.png = mọi layer trừ "Layer 1". Game vẽ nguyên cỡ (không kéo giãn để sọc không méo); khiên bật thì đổi sang màu xanh.
$bossBar = Join-Path $SourceDir "boss_health_bar.aseprite"
if (Test-Path $bossBar) {
  New-Item -ItemType Directory -Force (Join-Path $assets "ui") | Out-Null
  & $Aseprite -b --layer "Layer 1" $bossBar --save-as (Join-Path $assets "ui\boss_bar.png") | Out-Null
  & $Aseprite -b --ignore-layer "Layer 1" $bossBar --save-as (Join-Path $assets "ui\boss_bar_head.png") | Out-Null
  Write-Host "OK  boss_health_bar.aseprite  ->  Assets\ui\boss_bar.png + boss_bar_head.png"
} else { Write-Warning "Bỏ qua (không thấy): $bossBar" }
