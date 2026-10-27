# Cat Survivor (bản Windows Forms) – Tài liệu kĩ thuật: công nghệ, thuật toán, hàm

Tài liệu này mô tả **những gì thật sự có trong mã nguồn** `CatSurvivorWin/` (C#, .NET 11, Windows Forms + GDI+): công nghệ dùng, từng thuật toán
(kèm công thức và vị trí trong code), các lớp/hàm chính, cấu trúc dữ liệu, cách xử lí file/lỗi, kĩ thuật tối ưu và cách kiểm thử.
Mọi đường dẫn tính từ thư mục `CatSurvivorWin/CatSurvivorWin/`; `Lớp.Hàm` là tên trong mã. Số liệu (hằng số) lấy đúng từ `Core/Balance.cs`,
`Core/Difficulty.cs`, `Game/EnemyManager.cs`... nên nếu chỉnh cân bằng game thì chỉnh ở các file đó (không phải ở tài liệu này).

## Mục lục
1. [Tổng quan kiến trúc](#1-tổng-quan-kiến-trúc)
2. [Công nghệ sử dụng](#2-công-nghệ-sử-dụng)
3. [Bảng tổng hợp thuật toán](#3-bảng-tổng-hợp-thuật-toán)
4. [Chi tiết thuật toán](#4-chi-tiết-thuật-toán)
5. [Bảng các lớp và hàm chính](#5-bảng-các-lớp-và-hàm-chính)
6. [Cấu trúc dữ liệu](#6-cấu-trúc-dữ-liệu)
7. [File, bộ nhớ và xử lí lỗi](#7-file-bộ-nhớ-và-xử-lí-lỗi)
8. [Kĩ thuật tối ưu hiệu năng](#8-kĩ-thuật-tối-ưu-hiệu-năng)
9. [Kiểm thử tự động](#9-kiểm-thử-tự-động)
10. [Giới hạn đã biết](#10-giới-hạn-đã-biết)
11. [Giao diện: phần không dùng asset bạn vẽ](#11-giao-diện--những-phần-không-dùng-asset-do-bạn-vẽ-tự-vẽ-bằng-code)

---

## 1. Tổng quan kiến trúc

```
Program.Main
  └─ MainMenuForm ──Start──► (CharacterPick) ► SkillPick (chọn lá bài kĩ năng) ► GameForm ──► Restart / Menu / Quit
                                                                  │
                              GameForm (vòng lặp + phím + hộp thoại Pause/Upgrade/GameOver)
                                    │ gọi mỗi khung hình
                                    ▼
                               World.Update(dt)  ◄── logic game THUẦN, không phụ thuộc Form
                                    ├─ GameState      thời gian, kills, cấp, XP, điểm
                                    ├─ PickupManager  XP gem, bình máu, XP tự sinh
                                    ├─ EnemyManager   hàng trăm quái trong MỘT đối tượng (spawner, AI, va chạm, vẽ)
                                    │    ├─ Formation / BossAI / MiniEyes / Prediction
                                    ├─ ProjectileManager  đạn
                                    ├─ Player         di chuyển, dash, nhận sát thương, vũ khí, kĩ năng
                                    └─ BossArena      khoá vùng + khởi tạo boss + camera
                               World.Draw(canvas)
                                    └─ Canvas (bọc GDI+) ► FrameBuffer 640x360 (DIB) ► StretchBlt lên GameCanvas
```

Nguyên tắc thiết kế:
- **Tách logic khỏi giao diện**: thư mục `Game/` + `Core/` không dùng `Form`; `World` chỉ phát sự kiện (`Ended`, `LeveledUp`), form lo hiển thị.
- **Một đối tượng quản lí nhiều thực thể** (`EnemyManager`, `ProjectileManager`, `PickupManager`) thay vì mỗi quái/đạn một control hay một node:
  rất nhẹ, dễ gom nhóm để tối ưu (spatial hash, swap-remove).
- **Độ phân giải logic cố định 640×360** (`Balance.ViewWidth/ViewHeight`), vẽ vào bộ đệm nhỏ rồi phóng to một lần → pixel-art sắc nét và nhanh.
- **Mọi “trần nhà” chỉ số gom ở `Core/Balance.cs`**; độ khó gom ở `Core/Difficulty.cs`.
- **Thiếu tài nguyên không làm văng game** (xem mục 7).

---

## 2. Công nghệ sử dụng

| Nhóm | Công nghệ / API | Dùng để làm gì | Nằm ở |
|---|---|---|---|
| Ngôn ngữ / nền tảng | **C#**, **.NET 11** (`net11.0-windows`), `ImplicitUsings`, `Nullable` tắt, `System.Numerics` (`Vector2`) | Toàn bộ game; `Vector2` của .NET (SIMD) cho vị trí/vận tốc | `CatSurvivorWin.csproj` |
| Giao diện | **Windows Forms** (`Form`, `Control`, `Timer`, Designer `.Designer.cs`) | Menu, Pause, Upgrade, GameOver, Leaderboard, Help, About, Cheat, Settings | `Forms/` |
| Đồ hoạ 2D | **GDI+** (`System.Drawing`: `Graphics`, `Bitmap`, `Matrix`, `ImageAttributes`, `ColorMatrix`, `LinearGradientBrush`) | Vẽ sprite, hình học, viền gradient, tô màu (tint) | `Rendering/Canvas.cs` |
| Đồ hoạ mức thấp | **P/Invoke `gdi32.dll`**: `CreateDIBSection`, `CreateCompatibleDC`, `SelectObject`, `StretchBlt`, `SetStretchBltMode` | Khung hình 640×360 là DIB section bọc thành `Bitmap`; phóng to bằng `StretchBlt` nearest-neighbor | `Rendering/FrameBuffer.cs` |
| Nhịp khung hình | **`dwmapi.dll DwmFlush`**, `user32 PeekMessage`, `GetDC/GetDeviceCaps(VREFRESH)`, `winmm timeBeginPeriod` | Vòng lặp `Application.Idle` khoá theo tần số quét màn hình (60–144 Hz) | `Forms/GameForm.cs` |
| Âm thanh | **P/Invoke `winmm.dll` waveOut\***: `waveOutOpen/PrepareHeader/Write/Reset/Close`; luồng `Thread` riêng; `ConcurrentQueue` | Bộ trộn âm tự viết (nhạc nền + 32 hiệu ứng chồng), không cần thư viện ngoài | `Services/SoundService.cs` |
| Tệp | **`System.IO`** (`File.ReadAllLines`, `AppendAllText`, `OpenRead`), `System.Globalization` (`InvariantCulture`) | Bảng thành tích, cài đặt, thông tin nhóm, nạp ảnh/âm thanh | `Services/Storage.cs`, `Settings.cs`, `AssetStore.cs` |
| Sinh ngẫu nhiên | `System.Random` bọc trong `Rng` (`Randf`, `Randi`, `RandRange`) | Spawn, chọn nâng cấp, chọn đòn boss... | `Core/MathX.cs` |
| Cấu trúc dữ liệu | `List<T>`, `Dictionary<K,V>`, `HashSet<T>`, `Stack<T>`, `Queue`, `record`, `struct`, `enum`, sự kiện (`event Action`), lambda `Func/Action` | Xem mục 6 | khắp nơi |
| Tài nguyên | **Aseprite** (tự vẽ) → PNG/`.fnt`; xuất bằng `tools/export_assets.ps1` | Sprite, font bitmap (David Capello, CC BY 4.0), icon, thanh HUD | `Assets/`, `tools/` |
| Kiểm thử | Chế độ tự kiểm tra `--selftest` (không dùng framework ngoài) | Mô phỏng ván, chụp ảnh form, đo FPS... | `SelfTest*.cs` |
| Công cụ | Visual Studio / `dotnet` CLI, `.sln`, `global.json`, `.vsconfig`, PowerShell | Build, đóng gói, chạy thử | gốc repo, `tools/` |

---

## 3. Bảng tổng hợp thuật toán

| # | Thuật toán / kĩ thuật | Bài toán giải quyết | Vị trí trong code | Ý tưởng một dòng |
|---|---|---|---|---|
| 1 | **Spatial hash (lưới băm không gian)** | Va chạm hàng trăm quái ↔ đạn/vũ khí mà không so sánh O(n²) | `EnemyManager.RebuildGrid / QueryCircle` | Chia thế giới thành ô 40 px, mỗi quái nằm trong ô của nó; truy vấn chỉ duyệt các ô chạm vòng tròn |
| 2 | **Separation (tách nhau theo trọng lượng)** | Quái không chồng lên nhau | `EnemyManager.Simulate` | Mỗi quái bị đẩy khỏi hàng xóm chồng lấn trong 9 ô lân cận, bên nặng đẩy bên nhẹ nhiều hơn |
| 3 | **K-nearest bằng chèn (insertion)** | Tìm n quái gần nhất cho tự ngắm / mũi tên | `EnemyManager.FindNearest` | Giữ mảng n phần tử có khoảng cách bình phương, chèn và dịch lên đúng chỗ |
| 4 | **Dự đoán đường đi / chặn đầu (intercept)** | Quái nhắm vào chỗ mèo *sẽ* đứng | `Game/Prediction.cs` | Giải phương trình bậc 2 \|d + v·t\| = s·t để ra thời gian gặp nhau |
| 5 | **Máy trạng thái hữu hạn (FSM)** | AI Brute, Slime vồ, Dơi gào, Bomber, Boss, mắt con | `EnemyManager.Simulate`, `BossAI.Step`, `MiniEyes.Step` | Mỗi quái có `State/ChargeState/PounceState...` + bộ đếm giờ, chuyển trạng thái theo điều kiện |
| 6 | **Chọn đòn có trọng số theo tình huống** | Boss “biết nhìn” (độ khó cao) | `BossAI.ChooseAttack` | Trọng số cơ bản 40/35/25 nhân/hạ theo khoảng cách, vận tốc mèo, sát tường, đòn vừa dùng |
| 7 | **Rút ngẫu nhiên có trọng số, không lặp** | 3 thẻ nâng cấp mỗi lần lên cấp | `Upgrades.BuildChoices` | “Bánh xe roulette” trên túi thẻ, rút xong bỏ khỏi túi; sau đó ép có ≥1 thẻ vũ khí và thẻ hợp nhất nếu đủ điều kiện |
| 8 | **Đội hình quái (steering theo ô)** | Tường quét, gọng kìm, đàn dơi chữ V, hộ tống Brute | `Game/Formation.cs`, `EnemyManager.SpawnWall/...` | Tâm đội hình đi theo luật riêng; mỗi quái bám vào ô (slot) quanh tâm bằng lực tỉ lệ độ lệch |
| 9 | **Vật lí bay–rơi–nảy giả 3D** | Quái bị đánh văng góc 45°, nảy tâng | `EnemyManager.Launch / StepAirborne / ResolveHit` | Thêm trục Z giả: vz giảm theo trọng lực, chạm đất nảy với hệ số 0,55 đến khi hết nảy |
| 10 | **Hit-stop** | Cảm giác “đòn trúng” | `EnemyManager.Damage / ResolveHit` | Quái đứng khựng 0,10 s (0,14 s khi kết liễu); lực đẩy dồn lại và áp dụng sau khi hết khựng |
| 11 | **Làm mượt mũ (exponential smoothing)** | Camera, vận tốc mèo, xoay cung, đuổi theo | `World.Update`, `Player.Update`, `MagicBolt.Update` | `x += (target − x)·(1 − e^(−k·dt))` – độc lập tốc độ khung hình |
| 12 | **Sắp xếp theo chiều sâu (painter’s algorithm)** | Quái ở trước/sau mèo che nhau đúng | `EnemyManager.PrepareDraw / ByDepth` | Tách 2 lớp theo Y của “chân”, sắp xếp từng lớp theo Y rồi X |
| 13 | **Culling ngoài màn hình** | Giảm số sprite phải vẽ | `EnemyManager.PrepareDraw` | Bỏ quái ngoài khung nhìn cộng lề (80 px, 240 px nếu đang có vùng báo hiệu) |
| 14 | **Swap-remove** | Xoá phần tử O(1) khỏi `List` | `Reap`, `ProjectileManager.Update`, `PickupManager.Update` | Ghi đè phần tử cuối vào chỗ cần xoá rồi `RemoveAt(Count−1)` |
| 15 | **Va chạm vòng tròn / đoạn thẳng** | Mèo ↔ quái, chùm tia, chưởng | `QueryCircle`, `MiniEyes.DistToSegment` | Bình phương khoảng cách ≤ (r1+r2)²; đoạn thẳng: chiếu điểm lên đoạn rồi kẹp t∈[0,1] |
| 16 | **Hình quạt (sector) hit-test** | Claw, tiếng gào của dơi | `ClawSlash.Strike`, `EnemyManager.InScreechCone` | So sánh góc lệch đã chuẩn hoá (`Mx.Wrap`) với nửa góc quạt |
| 17 | **Vòng sóng lan (ring) hit-test** | Sóng xung kích boss, Shockwave | `BossAI.UpdateRings`, `Shockwave.Update` | Trúng khi \|khoảng cách − bán kính vòng\| ≤ độ dày (+ bán kính mục tiêu); mỗi vòng chỉ đánh mỗi mục tiêu 1 lần |
| 18 | **Cung (arc) lượng giác / quỹ đạo** | Dao xoay, thánh giá, cầu quay | `OrbitBlades`, `HolyCross.Slot`, `StarRing` | `pos = tâm + (cos(θ+i·2π/n), sin(θ+i·2π/n))·R` |
| 19 | **Công thức sát thương trừ giáp có sàn** | Giáp mèo và quái | `Player.TakeDamage`, `EnemyManager.Damage` | `max(dmg − giáp, dmg·0,2)` – không miễn nhiễm hoàn toàn |
| 20 | **Đường cong tăng trưởng quái theo thời gian** | Độ khó tăng dần có trần | `EnemyTypes.Create` | Máu ×(1+0,6m+0,03m²), sát thương ×(1+0,3m), tốc độ +4%/phút, đều bị kẹp trần |
| 21 | **Scale theo kích cỡ quái** | Quái to/nhỏ | `EnemyTypes.Create` | Máu, cân nặng, XP ∝ size²; sát thương, bán kính ∝ size; tốc độ ∝ 1/√size |
| 22 | **Phóng to pixel-art “sharp”** | Cửa sổ/toàn màn hình tỉ lệ lẻ vẫn nét | `GameCanvas.UpdateLayout`, `FrameBuffer.BlitSharp` | Phóng nguyên số lần (nearest) rồi thu nhẹ bằng bilinear |
| 23 | **Nhịp khung hình theo tần số quét** | Hết giật hình | `GameForm.OnAppIdle` | Chọn n chu kỳ quét/khung theo thời gian dựng trung bình trượt, đồng bộ bằng `DwmFlush` |
| 24 | **Bộ trộn âm (software mixer)** | Âm thanh nhiều kênh, không khựng | `SoundService.MixLoop / Mix` | Cộng mẫu nhiều “voice” vào buffer float, kẹp (clip) về 16-bit, đẩy vào 4 buffer waveOut |
| 25 | **Đường cong âm lượng bình phương** | Thanh trượt nghe đều | `SoundService.Curve` | gain = (phần trăm/100)² |
| 26 | **Nội suy tuyến tính khi đổi tốc độ phát** | Phát âm nhanh/chậm | `SoundService.Mix` (nhánh `Rate != 1`) | Đọc theo bước Rate, nội suy giữa 2 khung mẫu kề nhau |
| 27 | **Đổi màu theo độ sáng (palette ramp)** | Biến thể màu quái từ 1 sprite | `EnemyManager.BuildVariantTextures` | Luma = 0,299R+0,587G+0,114B → ánh xạ lên dải tối→vừa→sáng; giữ pixel trắng tinh |
| 28 | **9-slice** | Nút/khung giãn không méo viền pixel | `Canvas.DrawNineSlice`, `UiSkin` | 4 góc giữ nguyên, 4 cạnh giãn 1 chiều, giữa giãn 2 chiều |
| 29 | **Ghép glyph tiếng Việt** | Font bitmap thiếu dấu | `PixelFont.Compose / DrawMarks` | Phân rã Unicode (FormD) thành chữ gốc + dấu, tự vẽ dấu bằng pixel |
| 30 | **Ngắt dòng theo từ** | Hộp mô tả | `PixelFont.Wrap` | Thêm từng từ, vượt chiều rộng thì xuống dòng |
| 31 | **Thu nhỏ 1/2 bảo toàn nét (box filter có ngưỡng)** | Icon giáp ở HUD | `EquipmentAssets.Small` | Mỗi pixel mới = trung bình khối 2×2 (chỉ pixel đặc), đặc nếu ≥ nửa khối đặc |
| 32 | **Cắt sát viền (crop to content)** | Bỏ vùng trong suốt của ảnh | `EquipmentAssets.CropToContent`, `EnemyManager` trims, `Hud.MeasureSkillIcons` | Quét alpha > ngưỡng tìm hộp bao nhỏ nhất |
| 33 | **Trình thông dịch kịch bản CSV** | Trận Sans (easter egg) | `Sans/SansTimeline.cs` | Chạy từng dòng “thời gian,lệnh,tham số”, có biến, nhãn, nhảy, toán; giới hạn 20 000 dòng/khung chống vòng lặp vô hạn |
| 34 | **Sắp xếp xếp hạng** | Bảng thành tích | `Storage.ReadScores` | `List.Sort` so sánh: điểm giảm dần, hoà thì thời gian ít hơn |
| 35 | **Phân tích dòng (parser) chịu lỗi** | Đọc file điểm/cài đặt | `ScoreEntry.TryParse`, `Settings.Load`, `Cheats` | Tách bằng `\|` / `=`, kiểm tra từng trường, bỏ qua dòng hỏng/chú thích `#` |

---

## 4. Chi tiết thuật toán

### 4.1 Vòng lặp trò chơi và thời gian (`Forms/GameForm.cs`)
- **Delta-time**: `dt = (now − lastTicks)/Stopwatch.Frequency`, kẹp `dt ≤ 0,05 s` để kéo cửa sổ/giật lag không làm game “nhảy vọt”.
- **Time-scale (cheat)**: `scaled = dt·World.TimeScale`; chia thành `steps = ceil(scaled/0,025)` bước nhỏ để mô phỏng chạy nhanh không bị xuyên va chạm.
- **Hai chế độ vòng lặp**:
  1. *Application.Idle + DwmFlush* (mặc định): khi hàng đợi tin nhắn trống (`PeekMessage` trả false) thì chạy khung liên tục; có phím/chuột thì nhường ngay.
     `period = 1000/refresh` (đọc bằng `GetDeviceCaps(VREFRESH)`). Thời gian dựng khung được làm mượt `workMs += (work − workMs)·0,08`,
     số chu kỳ quét mỗi khung `n = clamp(ceil(workMs·1,1/period), 1, 3)` và chờ `DwmFlush()` cho tới khi đủ `(n − 0,5)·period`. Nhờ đó khi nặng thì
     chạy *đều* ở 72 FPS thay vì lúc 144 lúc 72 gây giật.
  2. *Timer dự phòng* (`gameTimer`, 1 ms với `timeBeginPeriod(1)`) khi `DwmFlush` lỗi (DWM tắt).
- **Dừng game khi mở hộp thoại**: `_dialogOpen` chặn `Frame`; `ShowModal` bao quanh mọi form con (Pause, Upgrade, GameOver, Cheat).
- **Mất tiêu điểm** (`Deactivate`) → `Input.Clear()` để mèo không tự chạy.

### 4.2 Kết xuất (`Rendering/`)
- `World.DrawWorld`: `Canvas.WorldMode(cam)` đặt ma trận gốc `M = [scale 0 0 scale | offset − cam·scale]`; `SetTransform(offset, rot, scale)` nhân thêm
  tịnh tiến–xoay–co giãn cục bộ cho từng vũ khí/đạn (`Matrix.Multiply/Translate/Rotate/Scale`, dùng lại một `Matrix` tạm để không cấp phát).
- **Thứ tự lớp**: nền cỏ → XP → quái *sau* mèo → mèo → quái *trước* mèo → hiệu ứng mắt → đạn → vũ khí → (hitbox) → `ResetTransform` → HUD.
- **Nền vô hạn**: `DrawFloor` chỉ lát các tile nằm trong khung nhìn: `x0 = floor(tl.X/tw)·tw`; mỗi tile vẽ rộng thêm 0,6 px chồng nhẹ để không hở khe khi phóng số lẻ.
- **Rung màn hình**: `shake = (sin(frame·12,9898), cos(frame·78,233))·Shake`, `Shake` giảm 14 px/s (hàm hash lượng giác giả ngẫu nhiên, không cần RNG).
- **Tint (nhân màu/độ trong suốt)**: `Canvas.Attributes(tint)` tạo `ImageAttributes` + `ColorMatrix` chéo (R,G,B,A) và **lưu đệm theo khoá 32-bit** (màu lượng tử hoá 8-bit);
  đường nhanh `_plain` khi không tô màu; `SetWrapMode(TileFlipXY)` tránh lem mép khi phóng.
- **Vẽ sprite không qua ma trận**: `DrawSpriteDirect(tex, src, pos, pivot, sx, sy)` dùng `DrawImage(PointF[3] parallelogram, ...)`, tọa độ `x = pos.X − pivot.X·sx`;
  `sx<0` = lật ngang. Hàng trăm quái mỗi khung nên bỏ chi phí tạo/đặt `Matrix`.
- **Bóng động**: sprite gốc có bóng đổ vẽ cứng (pixel bán trong suốt alpha 1–251) bị loại ở `BuildVariantTextures`; game tự vẽ elip bóng (`FillEllipse`) tại “chân” nên khi quái
  bị văng lên (trục Z) thì bóng ở lại mặt đất.
- **Pixel-art sắc nét**: `NearestNeighbor`, `PixelOffsetMode.Half`, `CompositingQuality.HighSpeed`, thế giới không khử răng cưa (`SmoothingMode.None`), HUD hình học có khử.

### 4.3 FrameBuffer và phóng to (`Rendering/FrameBuffer.cs`, `Forms/GameCanvas.cs`)
- `CreateDIBSection` (32 bpp, chiều cao âm = từ trên xuống) → bọc vùng nhớ thành `Bitmap(Format32bppPArgb, stride, ptr)` → `Graphics.FromImage`. Game vẽ thẳng vào bộ nhớ DIB.
- **Hiển thị**: `StretchBlt` với `COLORONCOLOR` (nearest) lên `HDC` của control — một lệnh, không qua bộ đệm kép của WinForms (`OptimizedDoubleBuffer = false`, `Opaque`).
- **Chọn tỉ lệ** (`UpdateLayout`): `fit = min(W/640, H/360)`; nếu `fit ≥ 1` và `floor(fit)/fit ≥ 0,8` thì dùng *đúng số nguyên* (pixel đều, viền đen mỏng);
  ngược lại dùng `fit` lẻ ở chế độ **sharp**: `BlitSharp` phóng nguyên `k = ceil(max(destW/640, destH/360))` lần bằng nearest sang `scratch`, rồi thu về đúng cỡ bằng
  `DrawImage` với `InterpolationMode.Bilinear` + `PixelOffsetMode.Half` + `CompositingMode.SourceCopy`.
- Viền đen chỉ tô lại khi cần (`_barsDirty` hoặc vùng vẽ nhỏ hơn client) để không nhấp nháy.
- Chuột: `OnMouseMove` đổi toạ độ cửa sổ → logic `(p − offset)/scale`, lưu `Input.MouseView`; `World.MouseWorld = Cam − ViewSize/2 + MouseView`.

### 4.4 Spatial hash (`EnemyManager`)
- `Cell(v) = floor(v / 40)`; khoá ô `Key(cx,cy) = ((long)cx << 32) ^ (uint)cy` trong `Dictionary<long, List<Enemy>>`.
- `RebuildGrid()` mỗi khung: trả mọi `List` về `Stack` **pool**, `Clear()` từ điển, đưa quái *còn sống và đang chạm đất* vào ô → gần như không cấp phát.
- `QueryCircle(c, r, out)`: duyệt các ô trong `[c−r−26, c+r+26]` (26 = `MaxEnemyRadius`), lọc `|e.Pos − c|² ≤ (r + e.Radius)²` (so sánh bình phương, không `Sqrt`).
- Độ phức tạp: xây O(n); truy vấn O(số quái trong vài ô) thay vì O(n).

### 4.5 Tách nhau – *separation* (`EnemyManager.Simulate`)
Với mỗi quái e, xét 9 ô lân cận; với hàng xóm o chồng lấn (`d² < (re + ro)²`):
```
len = |e.Pos − o.Pos|,  min = re + ro
share = 2·o.Weight / (e.Weight + o.Weight)             // bên nặng đẩy bên nhẹ nhiều hơn; cùng cân nặng → 1
push += (d/len)·((min − len)/min)·share
vel   = dir·speed + push·60 + e.Knock
```
Quái đang bay (`Airborne`) hoặc đã chết không tham gia lưới nên không đẩy/bị đẩy.

### 4.6 Dự đoán đường đi (`Game/Prediction.cs`)
Quái muốn chặn đầu mèo có vị trí `P`, vận tốc `v`, quái ở `from` chạy tốc độ `s`, có thời gian gồng `delay`:
```
d = P + v·delay − from
|d + v·t| = s·t   ⇒   (v·v − s²)·t² + 2(d·v)·t + d·d = 0
a = v·v − s²,  b = 2(d·v),  c = d·d,  Δ = b² − 4ac
t = nghiệm dương nhỏ nhất (a≈0 → t = −c/b nếu b<0); Δ<0 → không chặn được (mèo chạy thoát), quay về t = |d|/s
total = min(delay + t, Horizon)
aim   = P + v·total·Strength          (kẹp vào BossArena nếu đang đánh boss)
```
- Độ khó chỉnh 3 tham số (`Difficulty`): **Strength** 0/0,3/0,8/1; **Horizon** 0/0,5/1/2 giây; **Exact** (chỉ Siêu khó: dùng nghiệm đúng của phương trình bậc 2; các mức khác ước lượng `t = |d|/s`).
- **Vận tốc mèo cho dự đoán** là hai bộ lọc thông thấp: `VelSlow` (hằng số thời gian 0,5 s) và `VelFast` (0,1 s):
  `VelX += (Velocity − VelX)·(1 − e^(−dt/τ))`. Trung bình dùng `VelSlow` (chậm nhận ra đổi hướng), Khó dùng trung bình hai bộ, Siêu khó dùng `VelFast`.
- Ở **Dễ**, `Strength = 0` ⇒ `InterceptPoint` trả đúng chỗ mèo đang đứng nên hành vi y hệt không có dự đoán.
- Hàm tiện ích: `Dir(...)` (hướng đơn vị), `PositionAfter(p, seconds)` (chỗ mèo sẽ đứng sau t giây, dùng để quyết định có nên tung chiêu không).
- Được gọi từ: Slime (đuổi + vồ), Brute (đuổi + lao), Dơi (chọn sườn đứng + thời điểm gào), Bomber (từ Khó), Boss (đuổi, gồng lao, bẻ lái khi lao), mắt con (ngắm chưởng).

### 4.7 AI từng loại quái (`EnemyManager.Simulate`)
| Quái | Hành vi | Chi tiết thuật toán |
|---|---|---|
| **Slime** | Đuổi + “vây khéo” + nhún | `flank = Bias·0,9·clamp((dist−40)/160, 0, 1)`; `dir = dir.Rotated(flank)` mỗi con một `Bias ∈ [−1,1]` nên bầy toả ra bao quanh. Nhún: `p = 0,5+0,5·sin(2π·t/0,7)`; `speed ·= 0,35 + 1,7·p²`. **Slime đặc biệt** (hiếm) có FSM vồ: 0 chờ (mỗi giây thử 1 lần, xác suất 15%, trong tầm [60,190], hồi 8 s) → 1 gồng 0,65 s (nửa đầu còn ngắm, nửa sau khoá hướng) → 2 bay 0,45 s tốc độ `clamp(dist+40, 90, 170)/0,45` → 3 khựng 0,8 s. |
| **Dơi (Bat)** | Lượn, phóng ngang, gào hình phễu | Hồi chiêu: xoắn quanh mèo ở bán kính 95 (`radial = clamp((dist−95)/60, −1, 1)`; `dir = (dir·radial·1,2 + tangent).Normalized()`), đổi chiều ngẫu nhiên mỗi 1,5–3,5 s. Lắt léo: `wob = 0,5·sin(3,1t+φ) + 0,3·sin(7,3t+2φ)` quay hướng; thỉnh thoảng phóng ngang 0,22 s (quay 1,3 rad, tốc độ ×1,8). Sẵn sàng gào: bay tới sườn ngang hàng với chỗ mèo *sẽ* đứng, gào khi mèo nằm gọn trong phễu (`≤ 60%` nửa góc 0,66 rad, `≤ 85%` tầm 64). FSM gào: windup 0,30 → sóng 0,35 (tính sát thương 1 lần) → hồi 0,20; chờ 2,6 s. |
| **Bomber** | Chạy tới, châm ngòi, nổ vùng | Đón đầu `aim = pp + Velocity·min(dist/speed, 0,9)`; trong 260 px nước rút `speed = min(max(speed·1,25, RunSpeed·1,35), 300)` (luôn nhanh hơn mèo trừ khi mèo dash). Gần `34·size` thì châm ngòi 1,1 s (bò chậm ×0,35) rồi `Explode`: sát thương + đẩy mèo trong bán kính 44, hất các quái xung quanh (lực 280 ÷ Weight). |
| **Brute** | Gồng → lao → kiệt sức; có **khiên** | FSM `ChargeState`: 0 đi + dự đoán → 1 gồng 0,7 s (nửa đầu còn ngắm) → 2 lao 0,75 s tốc độ 250 theo hướng *đã khoá* (né ngang là thoát) → 3 kiệt sức 1,0 s; hồi 3,5 s. Lao gây ×1,5 sát thương. Khiên `ShieldHp = MaxHp·0,6`: còn khiên thì sát thương trừ vào khiên; vỡ khiên (kể cả giữa lúc gồng/lao) thì đứng khựng 0,45 s, từ đó chỉ đi chậm, không lao nữa. |
| **Thành viên đội hình** | Bám ô | `off = SlotPos − Pos`; `mv = (off·8).LimitLength(max(speed·2, groupSpeed·1,5+60))`; rời đội hình nếu `|off| > 140` (bị văng xa) hoặc đội giải thể. |
| **Tái chế quái** | Giữ mật độ | Quái xa mèo > 900 px được dịch sang vòng ngoài màn hình (`pp + RingOffset()`) thay vì xoá. |

Vòng sinh: `RingOffset = FromAngle(rand·2π)·(|view|/2 + 40)` – quái xuất hiện ngay ngoài mép màn hình.

### 4.8 Bộ sinh quái và độ khó theo thời gian (`SpawnTick`, `EnemyTypes.Create`)
- **Độ khó hiệu dụng** `DiffTime = thời gian chơi + 20 s · (cấp − 1)` (mỗi cấp nhân vật coi như quái đã chơi thêm 20 s).
- **Khoảng sinh**: `interval = max(1,2·faster, 2,6 − t·0,004)`, `faster = max(1 − 0,008·(lv−1), 0,5)`; **số con mỗi lần** `n = min(1 + t/190, 5 + (lv−1)/8)`;
  **trần số quái** `MaxNow = min(120 + 2·(cấp − 1), 170)` (`MaxEnemies`, `MaxEnemiesCap`; đội hình được vượt thêm 40).
- **Chọn loại theo trọng số** (`PickKind`): Slime 1; Dơi 0,8 sau 30 s; Bomber `min(0,8, 0,3 + (t−60)/200)` sau 60 s; Brute `min(0,5, 0,15 + (t−150)/400)` sau 150 s. Rút bằng cộng dồn trọng số rồi trừ `roll`.
- **Tăng sức mạnh** với `m = t/60`:
  - máu × `min(1 + 0,6m + 0,03m², 15)` · sát thương × `min(1 + 0,3m, 4)` · tốc độ × `1 + min(0,04m, 0,4)` · giáp `+0,3/phút` (trần 12).
  - Sau đó: biến thể màu (bậc 0–4: hệ số máu 1/1,25/1,6/2,2/3, sát thương 1/1,1/1,25/1,5/1,8, XP 1/1,3/1,8/2,5/4), **size** (máu, cân nặng, XP ∝ size²; sát thương, bán kính, lực ∝ size; tốc độ ∝ 1/√size; `size ≤ 2`).
  - Rồi **kẹp trần tuyệt đối** (`EnemyHpHard 2000`, `EnemyDamageHard 60`, `EnemySpeedHard 130`), cuối cùng mới nhân hệ số độ khó (×1/2/4/6 máu và sát thương) — để trần là của chỉ số gốc.
- **Đội hình** xuất hiện sau 40 s, định kì `max(28, 40 − t·0,02)` giây, loại mở dần theo thời gian (xem 4.9); **vòng vây** `Swarm`: `n = min(22 + t/15, 70)` slime rải đều trên vòng tròn `i·2π/n`.

### 4.9 Đội hình (`Game/Formation.cs`)
Ba kiểu, mỗi kiểu một luật di chuyển cho **tâm** `Center`; vị trí thành viên `= origin + Dir·slot.X + Perp·slot.Y` (`Perp = (−Dir.Y, Dir.X)`):
- **March** (tường quét, gọng kìm): đi thẳng; nếu `TurnRate > 0` thì xoay hướng về mèo nhưng *giới hạn tốc độ xoay* (`Mx.Wrap(góc đích − góc hiện tại)` kẹp `±TurnRate·dt`) nên vẫn đọc được đường đi. Giải thể khi đã quét qua mèo (`rel·Dir < −70`) hoặc tới gần (`BreakDist`).
- **Weave** (đàn dơi chữ V, đội bomber): `Dir` lerp về hướng mèo; tâm lượn `heading = (Dir + Perp·sin(Age·f + φ)·biên độ).Normalized()` → cả đội uốn như rắn.
- **Escort** (hộ tống Brute): hộ vệ bám sau thủ lĩnh, quay mặt về mèo; thủ lĩnh chết/đang lao/bay thì giải thể.
- **Tường quét** (`SpawnWall`): 2–3 hàng slime, hàng lẻ lệch nửa ô, chừa khe 2 ô ngẫu nhiên (`gap`), hướng ban đầu đón đầu bằng `AimAt`.

### 4.10 Trúng đòn: hit-stop, đẩy, bay–nảy (`Damage`, `ResolveHit`, `Launch`, `StepAirborne`)
1. `Damage(e, amount, knock)`: trừ máu `max(amount − Armor, amount·0,2)`; đặt `HitStop = 0,10 s` (kết liễu 0,14 s), `Flash` 0,22 s; **dồn** `PendingKnock += knock/Weight`.
2. Khi hết khựng, `ResolveHit`: quái chết → luôn bị hất (`max(speed, 170/Weight)`); lực ≥ `LaunchThreshold 170` → `Launch`; không thì `Knock += k` (đẩy trượt, tắt dần `Knock *= e^(−8·dt)`).
3. `Launch`: góc 45° giả – `Vel = dir·v`, `Vz = v` với `v = min(speed·0,6, 190)`.
4. `StepAirborne`: `Pos += Vel·dt; Vz −= 900·dt; Z += Vz·dt`. Chạm đất (`Z ≤ 0`): nếu `Vz < −45` thì `Vz = −Vz·0,55`, `Vel *= 0,7` (nảy tiếp), ngược lại đáp hẳn.
5. Quái chết chỉ bị `Reap` (thả XP, bình máu 0,8%, swap-remove) khi đã **hết bay, hết khựng và hết thời gian nằm lại**.
- **Boss/mắt con** không bị khựng/đẩy; boss “vừa xuất hiện / đang chuyển giai đoạn” bất khả xâm phạm; kiệt sức chịu thêm ×1,5 sát thương.

### 4.11 Mèo: di chuyển, dash, sát thương (`Game/Player.cs`)
- **Di chuyển 8 hướng**: `GameInput.Move()` lấy tổng 4 phím rồi `LimitLength(1)` (đi chéo không nhanh hơn); `Velocity = dir·RunSpeed + knockVel`.
- **Dash**: tốc độ `max(440/√Weight, 440·0,7)`, kéo dài 0,11 s (~48 px), **bất tử** trong lúc lao + 0,08 s sau (`DashGrace`); thoát được choáng. Số lượt ban đầu 2 (tối đa 4), hồi 20 s/lượt (nhanh nhất 8 s).
  `DashBash`: mỗi quái chạm trong lúc lao bị đánh đúng 1 lần (`HashSet<Enemy> _dashHit`), hướng đẩy = `(dashDir + hướng từ mèo ra quái).Normalized()`, lực `Weight·130`. Có “bóng ma” (ghost) mỗi 0,025 s.
- **Nhận sát thương** (`TakeDamage`), theo thứ tự: bỏ qua nếu God/chết/đang bất tử khung/đang dash/kĩ năng Bất tử hoặc Ngừng thời gian → **Holy Shield** (`AbsorbHit` của từng vũ khí) → **khiên trang bị** (xác suất `4%/cấp`, trần 20%) → tính `taken = max(dmg − Armor, dmg·0,2)`; bất tử 0,5 s; đẩy lùi `impact·(1 − KnockResist)/Weight` (trần 450); `impact ≥ HeavyImpact` thì choáng (giảm theo mũ).
- **Cân nặng mèo** `Weight = BaseWeight + 0,05·giáp` (`Balance.ArmorWeightPerPoint`; giáp gồm cả trang bị): nặng hơn → bị đẩy ít hơn và giật lùi ít hơn khi vung vũ khí nặng.
- **Giật lùi vũ khí nặng** (`Weapon.HitEnemy`): `recoil = Force·0,6·[wQuái/(wQuái + wMèo)]/wMèo` (chỉ khi `Weight ≥ 1,5`), cộng dồn có trần 260.
- **Hồi máu/mana**: `Heal(Regen·dt)`, `Mana = min(MaxMana, Mana + ManaRegen·dt)`; Siêu khó: `Difficulty.AllowHealing = false` chặn mọi nguồn hồi.
- **Cái chết**: `_deathVy = −DeathJump`, rơi theo trọng lực, camera đứng yên, `World.Update` gọi `RaiseEnded(false)` khi mèo ra khỏi màn (hoặc sau 2,6 s).

### 4.12 Hệ thống vũ khí (`Game/Weapon.cs`, `FusedWeapons.cs`, `HolyCross.cs`)
Lớp trừu tượng `Weapon`: `Level (1..8)`, `Rate = min(FireRate chung × RateMul riêng, 3)` — mọi bộ đếm hồi chiêu chạy `cd −= dt·Rate`.
Công thức theo cấp `L`:
| Vũ khí | Công thức chính | Thuật toán |
|---|---|---|
| **Bow** (`MagicBolt`) | Hồi `max(1,1·0,9^(L−1), 0,25)`; số mũi tên `1 + (L−1)/2`; sát thương `8+3L`; xuyên `(L−1)/3` | Bắn khi có quái trong tầm 320. Tự ngắm: mỗi mũi tên một quái gần nhất (`FindNearest(n)`), thiếu mục tiêu thì bắn lệch `±0,25 rad`. Tắt tự ngắm: toả quanh chuột `(i − (n−1)/2)·0,16`. Cung quay mượt `1 − e^(−18dt)`. Mỗi `Projectile` có `HashSet<Enemy> Hit` để không đánh một quái hai lần; `Pierce` giảm mỗi lần trúng. |
| **Knife** (`OrbitBlades`) | `2 + (L−1)/2` lưỡi; bán kính quỹ đạo `46+2L`; tốc độ góc `2,6+0,15L`; sát thương `6+2L` | Mỗi 0,35 s kiểm tra `QueryCircle` tại từng lưỡi (bán kính 7) rồi `HitEnemy`. |
| **Claw** (`ClawSlash`) | Hồi `max(0,9·0,92^(L−1), 0,25)`; tầm `56+4L`; nửa góc `32°+5°L`; sát thương `14+4L`; hướng 1 (L<4) / 2 (L≥4) / 4 (L≥7) | Chỉ quật khi có quái trong tầm; **luôn ngắm bằng chuột**. Một nhát = windup 0,36·0,30 → giáng ở `0,64·0,30 s` (lúc đó mới tính sát thương, rung màn 2,8, âm `slam`). Trúng nếu `dist < r + 14` hoặc `|Wrap(góc − hướng)| ≤ nửa góc`. |
| **Holy Shield** | `1 + L/3` lớp; hồi mỗi lớp `max(12 − L, 4)` s | Không gây sát thương: `AbsorbHit` chặn trọn một đòn, hất các quái trong 56 px (lực 240 ÷ Weight). |
| **Holy Cross** | `1 + L/2` cây; hồi `max(1,5 − 0,1L, 0,5)`; tốc độ lao `300+12L`; tầm tìm `170+6L` | FSM mỗi cây: Orbit (bám ô vòng quanh, `1 − e^(−12dt)`) → Strike (đuổi mục tiêu, xuyên mọi quái trên đường, mỗi quái 1 lần, tối đa 1,2 s) → Return (420 px/s). Mục tiêu chọn bằng `FindNearest(origin, n+4)` và loại các quái cây khác đang nhắm. |

**Hợp nhất** (`Fusions`): 6 công thức cặp từ 4 vũ khí gốc (Bow, Knife, Claw, Holy Shield). Điều kiện: có cả hai, cả hai ≥ cấp `FusionMinLevel (5)`, chưa có vũ khí hợp nhất đó.
`Apply`: cấp mới `= ceil((a.Level + b.Level)/2)`, xoá hai vũ khí gốc, thêm vũ khí mới, giữ `RateMul` lớn hơn. Ví dụ: *Wave Slash* bắn `Waves = 1/3/5` luồng xuyên thấu (tốc độ 340, pierce 99); *Star Nova* aura `48+4L` + mỗi `max(2,6−0,15L, 1,2)` s nổ `8+L` đạn đều theo `i·2π/n`; *Shockwave* vòng sóng bán kính tăng 280 px/s tới `100+8L`, mỗi vòng đánh mỗi quái 1 lần (`HashSet`).

### 4.13 Nâng cấp khi lên cấp (`Game/Upgrades.cs`, `Core/GameState.cs`)
- **XP**: cần `4 + 3·level` XP lên cấp kế; `AddXp` dùng vòng `while` nên nhặt nhiều XP có thể lên nhiều cấp (`PendingLevelUps` đếm chưa chọn). Cấp tối đa **27** → khoá XP, kích hoạt màn boss.
- **Túi thẻ** (`BuildChoices`): vũ khí đang có chưa max (trọng số 5), vũ khí mới (3), tốc độ bắn chung (3), tốc độ bắn riêng (2), chỉ số bị động, trang bị giáp, dash... (mặc định 1). Thẻ đã chạm trần (`Balance`) **không** vào túi.
- **Rút 3 thẻ không lặp**: `total = Σ weight; roll = rand·total;` trừ dần trọng số tới khi âm; chọn, bỏ khỏi túi, lặp.
- **Hiệu chỉnh sau rút**: nếu chưa có thẻ vũ khí thì thay thẻ cuối bằng một thẻ vũ khí ngẫu nhiên còn lại; nếu có công thức hợp nhất hợp lệ thì **luôn** đưa ra một thẻ ★ HỢP NHẤT.
- **Điểm**: `base = kills·10 + (level − 1)·100`; thắng thêm `5000 + 2·max(0, 1800 − giây)`; `điểm = base × ScoreMul(độ khó)` (1/2/4/6).

### 4.14 Giáp và sát thương chung
`thực nhận = max(sát thương − giáp, sát thương × 0,2)` (`Balance.ArmorMinFraction`), dùng cho mèo, quái, boss, mắt con. Giáp mèo = giáp gốc (thẻ, ≤ 10) + giáp trang bị, tổng ≤ 24.
Trang bị (`Equipment`): 4 ô × 5 cấp (Vải→Sắt→Thép→Mithril→Rồng) với giáp/đặc tính mỗi cấp: Mũ +1,5 giáp & −15% choáng; Áo +3 giáp, +15 máu, −3% tốc độ; Giày +1 giáp, +4% tốc độ, −10% bị đẩy; Khiên +2 giáp, +4% đỡ trọn đòn. Mọi đặc tính có trần ở `Balance`.

### 4.15 Kĩ năng chủ động và “đồng hồ riêng” cho quái (`Game/Skills.cs`)
- Phím Q/E được `GameInput` **xếp hàng** (bit `_skillQueued`), `SkillSet.Update` tiêu thụ bằng `ConsumeSkill(slot)` – nhấn một lần dùng một lần, không giữ phím. Dễ/Trung bình: 2 kĩ năng (Q và E riêng); Khó/Siêu khó: 1 kĩ năng (Q hoặc E đều được).
- 4 kĩ năng: **Nova** (QueryCircle bán kính 120, sát thương 60·DamageMul + hất), **Warp** (quái ~45% tốc độ, boss ~70%), **Stop** (1,5 s: quái và đòn của chúng đứng yên), **Guard** (2 s bất tử).
- **Thời gian cục bộ**: trong `Simulate`, mỗi quái có `dt = dtAll × (Boss ? BossTimeScale : EnemyTimeScale)` — làm chậm/ngừng quái mà mèo vẫn chạy bình thường; đội hình và chưởng của mắt con cũng nhân cùng hệ số.

### 4.16 Boss “Mắt Quỷ Đột Biến” (`Game/BossAI.cs`, `MiniEyes.cs`, `BossArena.cs`)
- **Khởi động**: khi mèo đạt cấp 27 và đóng menu nâng cấp cuối, `BossArena.StartBoss` khoá vùng `Rect2` 1000×560 quanh mèo, dọn quái, dừng sinh quái/XP, sinh boss ở đỉnh vùng.
- **Ghim vào vùng**: `Clamp(p, r) = (clamp(x, L+r, R−r), clamp(y, T+r, B−r))` cho mèo, quái, boss. **Camera**: theo mèo nhưng kẹp để mép màn hình dừng đúng mép vùng, trục nào vùng nhỏ hơn màn hình thì đặt giữa vùng (`CameraCenter`).
- **Ba giai đoạn = ba thanh máu** (`BossBarHp 1000 × hệ số độ khó`); cạn thanh → `BarEmpty` → `AdvanceStage` (thanh đầy lại). Giai đoạn 2: 4 mắt con, mắt chính bất khả xâm phạm tới khi diệt hết (rồi choáng lâu để phản công); giai đoạn 3: thêm mắt chính thứ hai (+4 mắt con của nó); Khó/Siêu khó gọi bù mắt con mỗi 40 s.
- **FSM của `BossAI.Step`** (`BossData.State`): 0 xuất hiện · 1 đuổi · 2 gồng lao · 3 đang lao · 4 kiệt sức · 5 gồng giậm · 6 khựng sau giậm (phát sóng) · 7 gầm triệu hồi · 8 gầm gọi 4 mắt con · 9 chưởng chữ thập.
  - *Gồng lao*: nửa đầu `ChargeDir = Prediction.Dir(...)` còn ngắm, nửa sau khoá. *Đang lao*: mức Khó trở lên bẻ lái `TurnToward(ChargeDir, hướng dự đoán, homing·dt)` (0,9/2,2 rad/s); đâm tường (`Clamp` đổi vị trí) → kiệt sức (Siêu khó: biết dừng trước tường, không tự choáng).
  - *Giậm*: sát thương trực tiếp nếu mèo trong `SlamRadius`; phát `Phase` vòng sóng cách nhau 0,5 s; vòng lan `R += RingSpeed·dt`, trúng khi `|d − R| ≤ RingWidth + r` (mỗi vòng 1 lần; dash xuyên qua được vì `TakeDamage` bỏ qua lúc dash).
  - `speedMul = (1 + (Phase−1)·0,25) × BossSpeedMul × (khiên ? 0,75 : 1)`.
- **Chọn đòn** (`ChooseAttack`): trọng số gốc `Lao 40, Giậm 35, Gầm 25` (gầm chỉ khi < 6 quái và không có khiên). Với `s = BossSmartness (0..1)`:
  `Giậm ×(1 + 2,5s)` nếu mèo *sẽ còn trong tầm* lúc giậm xong (dự đoán), ngược lại `×(1 − 0,9s)`; `Lao ×(1 + 1,2s)` nếu mèo xa > 140, `×(1 + 0,6s)` nếu mèo đang chạy nhanh (`VelFast > 60`), `×(1 + 0,8s)` nếu mèo bị dồn sát tường, và `×(1 − 0,7s)` cho đòn vừa tung (không lặp). Rút bằng roulette.
- **Mắt con** (`MiniEyes.Step`): FSM hợp thể (bay về ô chữ thập `CrossSlot = centre + FromAngle(angle + i·π/2)·R`) / gồng bắn chưởng (nửa đầu còn chỉnh hướng bằng `Prediction.Dir`) / **chạy trốn**: lực đẩy xa mèo + thành phần tiếp tuyến `Strafe` + lực đẩy khỏi tường `wall` (tỉ lệ độ gần tường trong 70 px);
  bị dồn góc (`wall` lớn và mèo < 140) thì lách vuông góc về phía còn nhiều chỗ, tốc độ ×2,3.
- **Hình học trúng đòn**: chưởng = đoạn thẳng, `DistToSegment(p, a, b)`: `t = clamp((p−a)·(b−a)/|b−a|², 0, 1)`, trúng khi `|p − (a + (b−a)t)| ≤ r + độ dày/2`. Chùm tia chữ thập xoay `CrossAngle += CrossSpin·dt`, 4 đoạn thẳng mỗi khung.

### 4.17 Nhặt XP và XP tự sinh (`Game/PickupManager.cs`)
- **Nam châm**: vào bán kính `MagnetRadius` thì `Attracted = true`, tốc độ tăng đều `Speed += 900·dt`, di chuyển `min(Speed·dt, khoảng cách)` (không vượt qua mèo); nhặt khi `len < Radius + 4`.
- Sprite theo giá trị: `< 1,5` gem xanh, `< 3` gem đỏ, còn lại cầu vàng; nhấp nhô `sin(t·3 + pha)·1,5`.
- **XP tự sinh** (không vô tận): mỗi 0,5 s thử sinh trong vòng `[140, 420]` px quanh mèo; tối đa `AmbientMaxAlive 60` viên cùng lúc và `AmbientPerMinute 100` viên/phút (đếm lại mỗi phút chơi); giá trị 1/2/4 với xác suất 50/30/20%; viên bị bỏ xa > 850 px biến mất; dừng khi đánh boss hoặc đã max cấp. Trần `MaxPickups 800` — vượt thì cộng XP thẳng.

### 4.18 Camera và rung (`World.Update`)
`Cam += (target − Cam)·(1 − e^(−8·dt))` với `target = mèo` (hoặc `CameraCenter` khi đánh boss). Hàm mũ cho độ trễ *không phụ thuộc FPS* (cùng độ trễ ở 60 hay 144 FPS).

### 4.19 HUD, viền máu thấp, thanh 3 mảnh (`Game/Hud.cs`, `HudBars.cs`)
- **Cảnh báo máu thấp** (`LowHealthVignette`): khi `ratio < 0,35`, `severity = (0,35 − ratio)/0,35`; nhịp `pulse = (0,5+0,5·sin(t·(2,4+4,2·severity)))^1,5`; `alpha = (0,20+0,45·severity)·(0,4+0,6·pulse)`; dày `44+24·severity+18·pulse` px. Vẽ bằng 4 `LinearGradientBrush` ở 4 cạnh (đỏ → trong suốt).
- **Thanh máu/mana/XP kiểu 3 mảnh**: đầu trái (8 px) + phần giữa kéo giãn + đầu phải; phần còn lại cắt theo tỉ lệ, vẽ đè lên thanh tối; vị trí các thanh trong ảnh nguồn được **dò tự động** bằng quét pixel. Chưa có asset → thanh phẳng.
- **Icon kĩ năng**: `MeasureSkillIcons` tìm hộp bao mỗi khung vuông trong `skills.png` rồi vẽ sát hình; ô kĩ năng tự to theo icon (tối thiểu 30 px), tối dần từ trên xuống theo `cd/cdMax`.

### 4.20 Phân tích font và tiếng Việt (`Rendering/PixelFont.cs`)
- Đọc `aseprite_font.fnt` bằng **Regex** `(\w+)=(-?\d+)` trên các dòng `char ...` → `Dictionary<int, Glyph>`.
- Glyph “ơ” trong font giống “ư” nên bị bỏ (`DropHornedO`) để *ghép lại*.
- Ký tự thiếu: `Normalize(FormD)` → chữ gốc + dấu; `Compose` tìm tiền tố có trong font, kiểm tra các dấu thuộc tập biết (sắc, huyền, hỏi, ngã, nặng, mũ, trăng, móc); `DrawMarks` vẽ dấu bằng `FillRect` pixel đặt theo hộp bao thật của glyph (`ComputeExtents` quét alpha). Chồng dấu (vd. ế) đặt cao hơn 3 hàng.
- Không ghép được → font hệ thống (Segoe UI) làm dự phòng. Có `Measure` (đo chiều rộng) và `Wrap` (ngắt dòng theo từ).

### 4.21 Âm thanh – bộ trộn phần mềm (`Services/SoundService.cs`)
- Nạp WAV PCM 8/16 bit, mono/stereo, tần số bất kì → **đổi về stereo 16 bit 44 100 Hz** (`LoadClip`).
- `waveOutOpen` một thiết bị; **4 buffer × 1024 khung** (~23 ms); luồng `SoundMixer` (ưu tiên AboveNormal) lặp: buffer nào đã `WHDR_DONE` thì `Mix` rồi `waveOutWrite`.
- `Play(name)` chỉ **xếp `Voice` vào `ConcurrentQueue`** (rẻ, không khoá luồng game); có `minGap` chống phát dồn; tối đa 32 voice.
- `Mix`: cộng nhạc nền (lặp, gain đổi tuyến tính trong buffer để không “lách tách” khi kéo thanh trượt) + mọi voice vào `float[]`, kẹp `[−32768, 32767]`.
- Âm lượng = nhân thẳng vào mẫu → kéo về 0 là im hẳn (`Curve(p) = (p/100)²`). `LastPeak` cho kiểm thử tự động.

### 4.22 Tiện ích toán (`Core/MathX.cs`)
`Mx.Clamp/Lerp/Wrap(v, min, max)` (đưa góc về `[−π, π]`), `V.FromAngle`, `Normalized` (chống chia 0), `Rotated(a)` (ma trận xoay 2×2), `LimitLength`, `Rect2.Grow`, `Rgba.Lerp`, `Rng`.
Quy ước toạ độ: x phải, y xuống (như GDI+), góc `Atan2(y, x)` tính bằng radian.

### 4.23 Trình thông dịch kịch bản (Sans – easter egg) (`Sans/SansTimeline.cs`)
Mỗi dòng CSV `thời gian chờ, lệnh, tham số...`; `$tên` thay bằng giá trị biến lúc nạp; nhãn bắt đầu bằng `:`; lệnh toán (`SET ADD SUB MUL DIV MOD FLOOR DEG RAD SIN COS ANGLE RND`) và nhảy
(`JMPABS JMPREL JMPZ JMPNZ JMPE JMPNE JMPL JMPNL JMPG JMPNG`) chạy tại chỗ, lệnh khác chuyển cho `Call` của trận đấu. Có `Pause/Resume`, nhiều dòng “chờ 0” chạy liền trong một khung,
**giới hạn 20 000 dòng/khung** chống vòng lặp vô hạn. Trái tim: di chuyển từng bước ≤ 1 px và dừng khi chạm vật rắn (chống xuyên tường); chế độ xanh có trọng lực theo góc, nhảy, “slam”.
*(Tài sản Undertale không nằm trong repo; mã port được giữ cho bản nộp.)*

---

## 5. Bảng các lớp và hàm chính

### `Core/` – nền tảng
| Lớp / hàm | Chức năng |
|---|---|
| `Balance` | Hằng số cân bằng và mọi trần chỉ số (máu mèo 400, tốc độ 220, giáp 24, cấp 27, vũ khí cấp 8, trần quái...) |
| `Difficulty` | `Level`, `StatMul`, `ScoreMul`, `PredictionStrength/Horizon/Exact`, `Boss*` (Smartness, SpeedMul, CooldownMul, StunMul, Homing, ComboChance, AvoidsWalls, ResummonInterval), `AllowHealing`, `ShowEnemyTelegraphs` |
| `GameState` | `Update`, `AddXp`, `XpForLevel`, `ComputeScore/ComputeBaseScore`, sự kiện `LeveledUp`, `ReachedMaxLevel` |
| `GameInput` | `KeyDown/KeyUp/Clear`, `Move()`, `ConsumeDash()`, `ConsumeSkill(slot)`, `MouseView` – cầu nối phím/chuột ↔ logic |
| `MathX` (`Mx`, `V`, `Rect2`, `Rgba`, `Rng`) | Toán vector/góc/màu/ngẫu nhiên |

### `Game/` – logic
| Lớp | Hàm / thành phần tiêu biểu |
|---|---|
| `World` | `Update(dt)`, `Draw/DrawWorld/DrawOverlay`, `RaiseEnded`, `AddShake`, `ShowToast`, `MouseWorld`, `DrawFloor`, sự kiện `Ended` |
| `Player` | `Update`, `StartDash`, `DashBash`, `TakeDamage`, `Heal`, `TrySpendMana`, `Equip`, `AddWeapon`, `Recoil`, `AimAngle/MouseAngle`, `VelSlow/VelFast` |
| `EnemyManager` | `Update`, `SpawnTick`, `PickKind`, `Swarm`, `SpawnWall/VFlock/Pincer/Squad/Escort`, `RebuildGrid`, `QueryCircle`, `FindNearest`, `Simulate`, `Damage`, `ResolveHit`, `Launch`, `StepAirborne`, `Explode`, `UpdateScreech`, `Reap`, `PrepareDraw`, `DrawLayer`, `DrawHitboxes`, `BuildVariantTextures` |
| `Enemy` / `EnemyTypes` | Dữ liệu một quái (vị trí, máu, trạng thái FSM...), `Create(kind, t, pos, size, variant)`, `PickVariant`, `RandomSize`, `Palette` |
| `Formation` | `SlotPos`, `Update` (3 kiểu March/Weave/Escort) |
| `Prediction` | `InterceptPoint`, `Dir`, `PositionAfter`, `SolveIntercept`, `PlayerVelocity` |
| `BossAI` / `BossData` | `Step` (FSM), `ChooseAttack`, `AdvanceStage`, `SpawnMiniEyes`, `Resummon`, `SummonMinions`, `UpdateRings`, `TurnToward`, `IsCornered` |
| `MiniEyes` | `Step`, `UpdateBolts`, `StepCross`, `Beam`, `CrossSlot`, `DistToSegment` |
| `BossArena` | `Clamp`, `Update`, `StartBoss`, `CameraCenter`, `DrawBanner` |
| `Weapon` + 5 gốc + 6 hợp nhất | `Update`, `Draw`, `DrawHitbox`, `AbsorbHit`, `HitEnemy`, `LevelUp`, `Rate`, `Force` |
| `Fusions` | `Available`, `Apply`, `Find`, bảng `All` (công thức) |
| `Upgrades` | `BuildChoices` (túi thẻ + rút có trọng số) |
| `Equipment` / `EquipmentAssets` | `ArmorAt`, `SpeedBonusAt`, `KnockResistAt`, `StunResistAt`, `BlockChanceAt`, `Describe`; `Small`, `CropToContent` |
| `SkillSet` | `Update`, `Try`, `TryNova/Warp/Stop/Guard`, `EnemyTimeScale/BossTimeScale`, `Draw` |
| `PickupManager` | `DropGem`, `DropHeal`, `AmbientTick`, `Update`, `Draw` |
| `ProjectileManager` | `Spawn`, `Update`, `Draw`, `DrawHitboxes` |
| `Hud` / `HudBars` | `Draw`, `LowHealthVignette`, `DrawBars`, `DrawSkills`, `SkillBox`, `DrawEquipIcons`; thanh 3 mảnh |
| `SkillPick` / `CharacterSkin` | Màn chọn lá bài, hình nhân vật |
| `Cheats` | Console debug (chỉ bản Debug, `#if DEBUG`): phân tích lệnh `god, heal, xp, level, boss, bosshp, stage2/3, weapon, fuse, equip, stats, spawn, formation, killall, time, timescale, info...` |

### `Rendering/`, `Services/`, `Forms/`
| Lớp | Chức năng |
|---|---|
| `Canvas` | `Begin`, `WorldMode/ScreenMode`, `SetTransform/ResetTransform`, `FillRect/DrawRect/FillGradientRect`, `FillEllipse/DrawEllipse/DrawCircle/DrawArc`, `DrawLine/DrawPolyline/DrawPolygon`, `DrawTexture`, `DrawSpriteDirect`, `DrawNineSlice`, `Attributes(tint)` |
| `FrameBuffer` | `BlitTo` (nearest), `BlitSharp`, `Dispose` |
| `PixelFont` | `Draw`, `DrawCentered`, `Measure`, `Wrap`, `Compose`, `DrawMarks` |
| `UiSkin` | `Load`, `DrawButton`, `DrawPanel`, `DrawBackground`, `PixelFactor(dpi)` – da giao diện có/không asset |
| `Storage` | `ReadScores`, `AppendScore`, `ReadTeam`, `DataDir` (exe → `%AppData%`) |
| `Settings` | `Load`, `Save` (key=value) |
| `AssetStore` | `Image`, `OptionalImage`, `ImageStraight`, `ToPArgb`, `Report/TakeReport`, `Placeholder` |
| `SoundService` | `Init`, `Shutdown`, `Play`, `Toggle/SetEnabled`, `SetMusicVolume/SetSfxVolume`, `MixLoop`, `Mix`, `LoadClip` |
| `GameForm` | `OnAppIdle`, `Frame`, `ApplyDisplayMode` (toàn màn hình), `ShowPause/ShowUpgrades/ShowCheat`, `OnEnded`, `GameForm_KeyDown/KeyUp` |
| `GameCanvas` | `UpdateLayout`, `OnPaint`, `OnMouseMove` |
| `MainMenuForm`, `PauseForm`, `UpgradeForm`, `GameOverForm`, `LeaderboardForm`, `HelpForm`, `AboutForm`, `SettingsForm`, `SkillPickForm`, `CharacterPickForm`, `CheatForm` | Các màn hình; thuộc `SkinnedForm`, dùng `PixelButton/PixelLabel/PixelTable/PixelSlider/CatBanner`; bố cục tạo bằng Designer |

---

## 6. Cấu trúc dữ liệu

| Cấu trúc | Dùng ở | Lí do chọn |
|---|---|---|
| `List<Enemy>` + **swap-remove** | `EnemyManager.Enemies`, đạn, XP | Duyệt nhanh theo bộ nhớ liên tục; xoá O(1) vì thứ tự không quan trọng |
| `Dictionary<long, List<Enemy>>` + `Stack<List<Enemy>>` (pool) | Spatial hash | Truy vấn ô O(1); tái dùng `List` mỗi khung nên không sinh rác (GC) |
| `HashSet<Enemy>` | Đạn (`Hit`), dash (`_dashHit`), vòng sóng, thánh giá | Đảm bảo mỗi đòn chỉ trúng mỗi quái một lần |
| `HashSet<Keys>` | `GameInput._down` | Trạng thái phím; `Add` trả false khi phím tự lặp → không tính nhấn lại |
| `Queue`/`ConcurrentQueue<Voice>` | Âm thanh | Luồng game → luồng trộn âm không khoá |
| mảng `float[]`/`short[]` | Bộ đệm trộn âm | Không cấp phát trong vòng lặp âm thanh |
| `Dictionary<int, Glyph>`, `Dictionary<char, Composite>` | Font | Tra cứu glyph, nhớ kết quả ghép dấu |
| `Dictionary<string, Bitmap>` (`Cache`) | `AssetStore` | Mỗi ảnh nạp đúng một lần |
| `Dictionary<int, ImageAttributes>` | `Canvas.Attributes` | Tránh tạo `ColorMatrix` mỗi lần tô màu |
| `struct` (`Glyph`, `Ghost`, `Slash`, `Rgba`, `Rect2`) | Dữ liệu nhỏ | Không cấp phát heap |
| `enum` (`SkillKind`, `EquipSlot`, `DifficultyLevel`, `Formation.Mode`, `GameOutcome`) | Trạng thái, lựa chọn | An toàn kiểu, đọc dễ |
| `record Recipe(A, B, Name, Desc, Make)` | `Fusions` | Công thức hợp nhất dạng dữ liệu, `Make` là hàm tạo (factory) |
| `event Action` / `Action<bool>` | `World.Ended`, `GameState.LeveledUp`, `Player.Died` | Tách logic khỏi giao diện |
| mảng hằng `float[]` | Hệ số biến thể màu | Tra bảng theo bậc |

---

## 7. File, bộ nhớ và xử lí lỗi

- **`Storage`** (`System.IO`): mọi thao tác bọc `try…catch`, trả về chuỗi lỗi qua `out string error` thay vì ném ngoại lệ — form hiện `MessageBox` thân thiện.
  - `thanhtichs.txt`: mỗi dòng `Tên|Điểm|Giây|Cấp|Kills|Thắng(0/1)|Ngày giờ|Độ khó`; ghi **nối thêm** (`AppendAllText`); dòng cũ thiếu cột độ khó coi là Dễ; dòng `#` là chú thích; tên được lọc ký tự `|`, xuống dòng.
  - `thongtinnhom.txt` (`Họ tên|MSSV|Lớp`): chưa có file thì tự tạo file mẫu.
  - `settings.txt` (`key=value`): `sound, music, sfx, fullscreen, autoaim, hitbox, difficulty, sans, character` — đọc/ghi chịu lỗi, giá trị sai quay về mặc định.
  - **Thư mục dữ liệu**: ưu tiên thư mục chứa `.exe`, không ghi được thì `%AppData%\CatSurvivor`.
- **`AssetStore`**: nạp PNG qua `FileStream` (không khoá file), chuyển sang `Format32bppPArgb` (định dạng GDI+ vẽ nhanh nhất). Thiếu/hỏng ảnh → **hình thay thế ô caro hồng-đen 64×64** và ghi vào báo cáo; `TakeReport` gom để hiện *một lần* hộp cảnh báo.
  `OptionalImage` cho asset không bắt buộc (không báo lỗi). `ImageStraight` giữ ARGB thẳng (không premultiply) khi cần xử lí từng pixel.
- **Âm thanh** thiếu file/không có thiết bị → bỏ qua và báo một lần, game vẫn chạy.
- **Ngoại lệ chưa lường**: `Application.ThreadException` + `try/catch` quanh `Main` → hộp thoại lỗi rồi thoát gọn; `SoundService.Shutdown()` trong `finally`.
- **Quản lí tài nguyên GDI**: `FrameBuffer.Dispose` giải phóng `Graphics`, `Bitmap`, DC, HBITMAP; `using` cho `Bitmap/FileStream/Brush` tạm.
- **Đóng gói tài nguyên**: `Assets/**` và hai file `.txt` được `CopyToOutputDirectory = PreserveNewest` trong `.csproj` nên chạy từ Visual Studio hay `dotnet run` đều có đủ file cạnh `.exe`.

---

## 8. Kĩ thuật tối ưu hiệu năng

| Kĩ thuật | Hiệu quả |
|---|---|
| Vẽ ở 640×360 vào DIB rồi `StretchBlt` một lần | 140 sprite từ ~20 ms xuống vài ms; pixel-art đúng chuẩn |
| Không dùng bộ đệm kép của WinForms cho `GameCanvas` | Bỏ một lần chép cả màn hình (vài ms ở 1080p) |
| Spatial hash + pool danh sách + so sánh bình phương khoảng cách | Va chạm gần O(n), không `Sqrt`, không rác GC |
| `DrawSpriteDirect` (không tạo `Matrix` cho từng sprite) | Hàng trăm quái mỗi khung |
| Chỉ vẽ phần có hình của mỗi khung (hộp bao `trims`) và culling ngoài màn hình | Bớt thao tác `DrawImage` |
| Bóng quái = elip `FillEllipse` không đổi ma trận | Rẻ hơn `SetTransform + DrawCircle` |
| Đệm `ImageAttributes` theo màu lượng tử hoá, tái dùng `Matrix`, `PointF[3]`, `Pen`, `SolidBrush` | Không cấp phát mỗi lệnh vẽ |
| Swap-remove, một đối tượng quản lí nhiều thực thể, tái chế quái xa | Mật độ cao nhưng nhẹ |
| Âm thanh trên luồng riêng, hàng đợi lock-free | Game không khựng khi phát nhiều hiệu ứng |
| `timeBeginPeriod(1)`, vòng Idle + `DwmFlush` theo tần số quét | Hết kẹt ~62 FPS, khung đều trên màn 144 Hz |
| `BlitSharp` chỉ dùng khi tỉ lệ lệch xa bội nguyên | Bilinear ~10 ms @1080p đắt hơn nearest ~3 ms nên không dùng thường xuyên |
| Đo bằng bot (1280×720, quái vây quanh mèo): cấp 27 với ~190 quái | Từ 11,3 ms xuống ~6,2 ms mỗi khung vẽ (xem `--selftest --prof`) |

---

## 9. Kiểm thử tự động

Không dùng framework ngoài: `CatSurvivor.exe --selftest [chế độ] --out <thư_mục>` chạy các mô phỏng headless (không cần người chơi) và ghi `selftest.log` + ảnh PNG.
Nhập cụm từ khoá để chọn phần kiểm thử:

| Cờ | Kiểm tra |
|---|---|
| *(không cờ)* | Mô phỏng ván 150 s, đánh boss, chết/điểm, đọc–ghi file (kể cả đường lỗi), âm thanh, chụp ảnh các form, đo FPS |
| `--forms` | Chụp ảnh và kiểm tra các form |
| `--difficulty`, `--bossbrain` | Hệ số độ khó, dự đoán đường đi (mèo chạy ngang), hành vi boss theo mức |
| `--phases`, `--eyes`, `--eyefx` | 3 giai đoạn boss, mắt con, hiệu ứng mắt |
| `--brute`, `--shield`, `--bomber` | Brute có khiên, Holy Shield, Bomber |
| `--weapons`, `--armor`, `--skills`, `--pick`, `--cheats` | Hình vũ khí, giáp, kĩ năng, màn chọn lá bài, spawn/tốc độ bắn/xác suất nâng cấp/XP tự sinh/cheat/cài đặt |
| `--sound`, `--font`, `--present`, `--hitbox`, `--lowhp`, `--autoaim`, `--death`, `--level`, `--prof`, `--bench` | Âm lượng 0 → im hẳn, font tiếng Việt, phóng to/khung hình, hitbox, viền máu thấp, tự ngắm, cái chết & điểm, tăng độ khó theo cấp, đo thời gian từng phần |
| `--sans` | Easter egg Sans: chạy từng kịch bản, bot đánh hết trận, menu, vật lí trái tim |

---

## 10. Giới hạn đã biết
- **Chỉ chạy trên Windows** (WinForms + GDI+ + P/Invoke `gdi32/winmm/dwmapi/user32`). Port sang Godot (có Android) là dự án riêng `CatSurvivor_Godot`.
- **GDI+ là bộ vẽ CPU**: số quái cùng lúc bị giới hạn (trần 120 → 170 theo cấp, đội hình vượt thêm 40) để giữ khung hình; không dùng GPU.
- **Mọi hằng số cân bằng nằm trong code**, chưa tách ra file cấu hình ngoài.
- **Console cheat chỉ có ở bản Debug** (`#if DEBUG`), bản Release tự tắt.
- **.NET 11 đang ở giai đoạn RC** tại thời điểm viết (cần SDK 11 + Visual Studio bản mới; xem `README.md`).
- Trận Sans dùng tài sản của Undertale/Toby Fox nên **không** nằm trong repo; thiếu thư mục `Assets/sans` thì easter egg tự tắt.

---

## 11. Giao diện – những phần KHÔNG dùng asset do bạn vẽ (tự vẽ bằng code)

Mục này tách rõ **cái gì trên màn hình là hình do bạn vẽ trong Aseprite** và **cái gì do mã C# tự vẽ** (hình học GDI+, gradient, chữ, ghép ảnh), để khi bảo vệ/báo cáo
bạn nói đúng nguồn gốc từng phần. Mọi mô tả dưới đây đối chiếu với trạng thái thư mục `Assets/` hiện tại.

### 11.1 Nguyên tắc: “có asset thì dùng asset, không có thì tự vẽ”
`UiSkin.Load()` (và các `Hud`, `WeaponArt`, `EquipmentAssets`...) gọi `AssetStore.OptionalImage(...)`: trả `null` nếu file không tồn tại và **không báo lỗi**;
khi `null`, mã vẽ một hình thay thế bằng `Canvas` (hình chữ nhật, elip, cung, đường, gradient). Nhờ vậy game luôn hiển thị được, và bạn chỉ cần thả PNG đúng tên vào
`Assets/ui/` là phần tự vẽ bị thay bằng hình của bạn (kích thước/quy ước: `docs/UI_ASSETS.md`). Ngược lại `AssetStore.Image(...)` dành cho asset *bắt buộc*: thiếu thì hiện ô caro hồng–đen
và gom cảnh báo (xem mục 7).

### 11.2 Asset bạn đã cung cấp (đang được dùng)
| Nhóm | File (trong `Assets/`) | Dùng ở |
|---|---|---|
| Nhân vật, quái | `characters/main_character.png`, `enemies/{slime,bat,bomber,brute,eyeboss,minieye,plasma}.png` | Sprite trong ván, **và cả dải minh hoạ động `CatBanner` ở menu chính** |
| Nền, vật phẩm | `environment/grass.png`, `items/experiences.png` | Nền cỏ lát vô hạn; XP gem; hoa văn mờ của nền menu |
| Vũ khí | `weapons/{bow,claw,knife,holy}.png` | Cung, vuốt, dao, thánh giá, bong bóng khiên |
| HUD | `ui/hud_bars.png`, `ui/hud_head.png`, `ui/boss_bar.png`, `ui/boss_bar_head.png`, `ui/skills.png` | Thanh máu/mana/XP, thanh máu boss, icon kĩ năng |
| Lá bài kĩ năng | `ui/poker.png` | Hình lá bài ở màn chọn kĩ năng (dùng chung cho 4 kĩ năng) |
| Giáp | `equipment/icons/{helm,body,boots,shield}_{1,2}.png` | Icon ở thẻ nâng cấp và HUD – **chỉ có cấp 1 và 2** (xem 11.5) |

Ngoại lệ không phải của bạn: font `ui/aseprite_font.*` (David Capello, CC BY 4.0). Âm thanh `audio/` ban đầu được **sinh bằng chương trình** (tiếng tổng hợp tạm) chứ không phải thu âm/vẽ tay;
nếu bạn đã thay bằng file thật cùng tên thì bỏ ghi chú này.

### 11.3 Các form WinForms: phần tự vẽ bằng code
Chưa có `button_*.png`, `panel.png`, `menu_bg.png`, `logo.png`, nên **toàn bộ “da” menu hiện do `Rendering/UiSkin.cs` vẽ bằng code**:

| Thành phần giao diện | Asset thay thế (chưa có) | Cách code tự vẽ | File |
|---|---|---|---|
| **Nút bấm** (`PixelButton`: Bắt đầu, Cài đặt, Tiếp tục, thẻ nâng cấp...) | `button_normal/hover/pressed/disabled.png` | `DrawFallbackButton`: hình chữ nhật **cắt 4 góc** (`Notched`, mỗi góc bớt một “pixel” `u`) gồm viền đen ngoài + viền màu + nền **hai dải màu** (giả gradient kiểu pixel) + vạch sáng trên/vạch tối dưới. 4 trạng thái Normal/Hover/Pressed/Disabled đổi bảng màu (hover viền vàng sáng, pressed tối, disabled xám); nút nhấn mạnh (`Highlight`) nền vàng | `UiSkin.DrawFallbackButton`, `Forms/Controls/PixelButton.cs` |
| **Khung nội dung** (panel của mọi form) | `panel.png` (9-slice) | `DrawPanel`: 3 lớp `Notched` lồng nhau (viền đen → viền vàng sẫm → nền tối trong suốt 90%) + đường viền mảnh bên trong; độ dày = `PixelFactor(dpi)·2` | `UiSkin.DrawPanel`, `SkinnedForm.OnPaintBackground` |
| **Nền menu** | `menu_bg.png` | `DrawBackground`: `LinearGradientBrush` dọc xanh rêu → gần đen, lát `grass.png` (asset của bạn) **rất mờ** (tint alpha 0,16) làm hoa văn, rồi **tối dần về 4 góc** bằng `PathGradientBrush` (vignette). Cắt bằng `SetClip` | `UiSkin.DrawBackground` |
| **Logo / tiêu đề** | `logo.png` | `CatBanner`: dải **hoạt ảnh bố cục bằng code** – mèo ở giữa, slime/bomber/dơi chạy hai bên, nhún theo `Timer` 90 ms; sprite là của bạn nhưng **vị trí, tỉ lệ, nhịp nhún, bóng** do code tính (bóng = elip đen bẹt `Scale(1, 0,35)`). Chữ “CAT SURVIVOR” là `PixelLabel` | `Forms/Controls/CatBanner.cs` |
| **Chữ** (mọi menu, bảng, HUD) | – | Font bitmap Aseprite (của David Capello) vẽ bằng `PixelFont`; **chữ tiếng Việt có dấu thiếu trong font được code ghép** từ chữ gốc + dấu vẽ bằng pixel (`Compose`/`DrawMarks`, xem 4.20); không ghép được thì rơi về Segoe UI. Có đổ bóng (`PixelLabel.Shadow`) và ngắt dòng theo từ (`Wrap`) | `Rendering/PixelFont.cs`, `Forms/Controls/PixelLabel.cs` |
| **Thanh trượt âm lượng** | – | `PixelSlider`: tự vẽ rãnh, phần đã đầy, núm kéo; tự xử lí chuột (kéo/bấm vào thanh), phím ← → (±5), Home/End; sự kiện `ValueChanged` (kéo) và `Committed` (nhả) | `Forms/Controls/PixelSlider.cs` |
| **Bảng thành tích, bảng thông tin nhóm** | – | `PixelTable`: không dùng `DataGridView`/`ListView`; tự vẽ tiêu đề cột, dòng xen kẽ, tô vàng dòng vừa lưu, cắt chữ dài bằng “…”, nội dung “Chưa có thành tích…” khi rỗng | `Forms/Controls/PixelTable.cs` |
| **Thẻ nâng cấp** (`UpgradeForm`) | – | Các `PixelButton` được **tạo bằng code** trong `OnLoad`: dòng đầu = tên (to), phần sau = mô tả tự xuống dòng; chiều cao tính theo nội dung (`MeasureHeight`); icon giáp (nếu có) vẽ phóng nguyên số lần bên trái; thẻ hợp nhất tô vàng | `Forms/UpgradeForm.cs` |
| **Lá bài chọn kĩ năng** | `skill_card_{nova,warp,stop,guard}.png` (riêng từng kĩ năng) | Hiện dùng chung `poker.png` của bạn; **tên, mô tả, huy hiệu phím Q/E, hiệu ứng nhô lên khi rê chuột/đang chọn do code vẽ** (`CardControl`). Nếu cả `poker.png` cũng mất thì tự vẽ lá bài tạm 48×68 (khung vàng, nền tối) | `Forms/SkillPickForm.cs`, `Game/SkillPick.cs` |
| **Control gốc của WinForms** (không tự vẽ) | – | `TextBox` (nhập tên ở `GameOverForm`, có `PlaceholderText`), `RichTextBox` (nội dung hướng dẫn ở `HelpForm`), `Label`/`TextBox` của `CheatForm` (chỉ bản Debug). Được tô lại màu/phông bằng `SkinnedForm.StyleField` cho hợp tông | `Forms/*.Designer.cs`, `SkinnedForm.cs` |

**Khung chứa (layout) của các form**: dựng bằng **Designer với toạ độ tuyệt đối** (`Location`, `Size`, `TabIndex`); chỉ `GameForm` dùng `Dock = Fill` cho `GameCanvas`.
Cỡ chữ/viền pixel luôn là **bội số nguyên** nhân theo DPI (`PixelFactor = max(1, floor(dpi/96 + 0,25))`) để nét pixel không bị mờ khi Windows scale 125–200%.
`SkinnedForm` bật `UserPaint | AllPaintingInWmPaint | OptimizedDoubleBuffer | ResizeRedraw` và tự vẽ nền; bọc `try/catch`, lỗi vẽ skin thì rơi về nền phẳng.

### 11.4 Trong ván chơi (HUD và hiệu ứng): phần tự vẽ bằng code
Hình dạng ở đây không có sprite tương ứng, hoàn toàn là hình học `Canvas`:

| Thành phần | Cách vẽ | Nằm ở |
|---|---|---|
| **Đồng hồ thời gian** `mm:ss` | `PixelFont` cỡ 2 ở (200, 8) | `Hud.Draw` |
| **Dãy chấm Dash** | chấm đặc `DrawCircle` r=3 = còn lượt; vòng rỗng `DrawArc` r=2,5 = đang hồi; kèm `(x,xs)` thời gian hồi lượt kế (font pixel không có ký tự ●/○ nên vẽ bằng hình) | `Hud.Draw` |
| **Chữ “Bắn x…”, thông báo (toast), nhãn DEBUG** | `PixelFont` cỡ 1, màu vàng/đỏ | `Hud.Draw` |
| **Viền đỏ “thở” khi máu < 35%** | 4 `LinearGradientBrush` ở 4 cạnh (đỏ → trong suốt); độ đậm/dày/nhịp theo `severity` (công thức ở 4.19) | `Hud.LowHealthVignette` |
| **Lớp phủ Làm chậm thời gian** | phủ xanh dương 9% + 4 dải gradient viền, vào/ra mượt theo `WarpOverlay` | `Hud.WarpTint` |
| **Lớp phủ Ngừng thời gian** | phủ trắng xám 14% + khung viền trắng | `Hud.StopTint` |
| **Ô kĩ năng** (khung, giá mana, số giây hiệu lực, hồi chiêu) | `FillRect` nền đen 55%, viền theo trạng thái (vàng = dùng được, xám = thiếu mana/đang hồi, xanh = đang hiệu lực); hồi chiêu = **tấm tối phủ từ trên xuống** theo `cd/cdMax`; icon bên trong là của bạn (`skills.png`) | `Hud.SkillBox` |
| **Biểu ngữ “! BOSS !”** + tên boss | `PixelFont` cỡ 3, màu đỏ cam, mờ dần sau 3 s | `BossArena.DrawBanner` |
| **Tên boss, “GIAI ĐOẠN n/3”, số máu thanh boss** | `PixelFont` cỡ 1 có đổ bóng; chữ đổi màu xanh khi bất khả xâm phạm | `Hud.DrawBossBars` |
| **Hiệu ứng kĩ năng quanh mèo** | Nova: vòng tròn + 2 cung nở ra theo `easeOut`; Bất tử: hào quang vàng nhấp nháy + cung đếm thời gian; Làm chậm/Ngừng: cung “đồng hồ” trắng/xanh = thời gian còn lại | `SkillSet.Draw` |
| **Vùng báo hiệu đòn đánh của quái** (tắt từ độ Khó) | dải lao của Brute/boss, vòng báo giậm, vòng vồ của Slime, **hai phễu gào của Dơi**, vòng nổ Bomber: `DrawPolygon`/`DrawArc`/`FillRect` bán trong suốt; vùng vẽ **đúng bằng vùng tính sát thương** | `EnemyManager.DrawChargeTelegraph/DrawPounceTelegraph/DrawScreechCones/DrawBlast` |
| **Sóng xung kích boss, hào quang nổi điên, khiên mắt chính** | vòng `DrawArc` lan ra, hào quang đỏ nhấp nháy, vòng xanh khi `ShieldUp` | `EnemyManager.DrawBossGround`, `DrawShieldFx` |
| **Khiên Brute (khi chưa có hình khiên)** | thanh/vòng xanh nhạt theo `ShieldHp/ShieldMax`; hiệu ứng vỡ khiên | `EnemyManager.DrawBruteShield`, `DrawShieldFx` |
| **Bóng dưới chân quái/mèo** | `FillEllipse` đen mờ; quái bay lên thì bóng ở lại mặt đất | `EnemyManager.DrawFrame` |
| **Nháy trắng khi trúng đòn** | tint trắng lên sprite (`ColorMatrix` trong `Canvas.Attributes`) | `EnemyManager.DrawFrame` |
| **Bình máu (hộp cứu thương)** | `FillRect` 8×8 trắng + chữ thập đỏ (không có sprite XP/hồi máu riêng) | `PickupManager.Draw` |
| **Đạn không phải mũi tên** | vệt `DrawLine` + `DrawCircle` đầu đạn (mũi tên dùng sprite của bạn) | `ProjectileManager.Draw` |
| **Vũ khí hợp nhất** (Star Ring, Wave Slash, Star Nova, Whirlwind, Shockwave) | chủ yếu hình học: quả cầu, vầng trăng kiếm khí, cánh quạt, vòng sóng (`DrawArc/DrawPolygon/DrawCircle`); chỉ *Holy Blades* tái dùng sprite dao | `Game/FusedWeapons.cs` (6 hàm `Draw`) |
| **Plasma của mắt con** (quả cầu, tia, chưởng) | quả cầu và tia dùng `plasma.png` của bạn kéo giãn/xoay; **quầng sáng, đuôi, vệt bay do code** (`DrawPlasmaOrb`, `DrawBolt`, `DrawBeam`) | `EnemyManager.DrawEyeEffects` |
| **Hitbox** (Cài đặt → Hiện hitbox) | vòng/đoạn màu: mèo xanh lá, quái đỏ, đạn vàng, vùng vũ khí xanh dương, vùng đòn quái cam | `World.DrawHitboxes`, `EnemyManager.DrawHitboxes` |
| **“Bóng ma” khi Dash** | các bản sao mờ của sprite mèo mỗi 0,025 s, mờ dần trong 0,28 s | `Player.Draw` |
| **Rung màn hình** | dời gốc toạ độ vẽ theo hàm sin/cos của số khung | `World.DrawWorld` |

### 11.5 Chỗ đang thiếu asset (game vẫn chạy bằng hình thay thế)
| Chỗ thiếu | Hiện tượng hiện tại | Cách thay bằng asset của bạn |
|---|---|---|
| `ui/button_*.png`, `panel.png`, `menu_bg.png`, `logo.png` | Toàn bộ menu dùng da vẽ bằng code (11.3) | Thả PNG đúng tên vào `Assets/ui/`; chỉnh `skin.txt` (`button_margin`, `button_scale`, `bg_mode`...). Xem `docs/UI_ASSETS.md` |
| `equipment/icons/*_{3,4,5}.png` (giáp cấp 3–5) | `EquipmentAssets.Item` trả `null` → **thẻ nâng cấp và HUD không có icon** cho các cấp này (vẫn có tên/chỉ số) | Vẽ thêm đủ 20 món (4 ô × 5 cấp); xem `docs/ARMOR_DESIGN.md` |
| `ui/skill_card_{nova,warp,stop,guard}.png` | Cả 4 lá bài dùng chung `poker.png` | Thả file riêng từng kĩ năng |
| `enemies/{slime,bat,bomber}_v{1..4}.png` | Quái không đổi màu theo bậc (`Balance.ShowColorVariants = false`) | Thả sprite vẽ tay từng bậc |
| Khung 1–2 của `brute.png` (khiên rời) nếu để trống | Khiên Brute vẽ tạm bằng ô vuông | Vẽ lại khung 1–2 (hiện đã có) |
| Thiếu bất kì sprite bắt buộc | Ô caro hồng–đen 64×64 + một hộp cảnh báo | Khôi phục file |

### 11.6 Về quy định “tự thiết kế UI” của đề
- **Phần thật sự do bạn làm**: hình vẽ (sprite, HUD, lá bài, icon) và các quyết định bố cục bạn đã tự chỉnh trong Designer.
- **Phần do AI viết khung**: mã của các form trong `Forms/*.cs` + `*.Designer.cs`, các control tự vẽ `Forms/Controls/*` và da `UiSkin` (như ghi ở README). Đề **cấm lạm dụng AI sinh toàn bộ mã UI**, nên
  để an toàn khi nộp bạn nên tự mở từng form trong Visual Studio, kéo–thả/chỉnh lại bố cục bằng tay (có thể đổi sang `TableLayoutPanel`/`Anchor`/`Dock` cho đúng tinh thần “nắm Layout Panel, Controls, Event Handler”),
  nắm được từng `*_Click` làm gì, rồi **cập nhật mục này và README cho đúng với phần bạn tự làm**. Tài liệu này mô tả hiện trạng, không thay thế việc đó.
