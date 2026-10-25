# Cat Survivor – bản Windows Forms (.NET 8)

Game sinh tồn top-down kiểu *Vampire Survivors*: điều khiển chú mèo giữa bầy quái ngày càng đông, nhặt XP lên cấp (tối đa **cấp 27**),
chọn nâng cấp / hợp nhất vũ khí / mặc giáp, rồi đánh **boss** trong một vùng bị khoá. Đây là bản chuyển từ Godot sang **Windows Forms**
theo đúng yêu cầu của đề (Lập trình Windows – 2511COMP1019), giữ nguyên toàn bộ gameplay.

## Mở và chạy
1. Mở `CatSurvivorWin.sln` bằng **Visual Studio 2022** (cần workload *.NET desktop development*, .NET 8 SDK).
2. Nhấn **F5** (hoặc *Start*). Tài nguyên (`Assets/`) và `thongtinnhom.txt` tự được chép cạnh file `.exe`.
3. Dòng lệnh: `dotnet run --project CatSurvivorWin` · kiểm tra tự động: `CatSurvivor.exe --selftest --out <thư_mục>` (xem mục *Tự kiểm tra*).

## Điều khiển
| Phím | Tác dụng |
|---|---|
| `W A S D` / mũi tên | di chuyển |
| `Space` / `Shift` | **dash** (bất tử khi lao, húc văng quái; mỗi lượt hồi 20 giây) |
| Chuột | ngắm hướng cho *Claw* / *Wave Slash*; *Bow* tự ngắm quái gần nhất |
| F | bật/tắt **tự động ngắm** cho *Bow* và *Holy Cross* (bật: tự ngắm vào quái gần nhất; tắt: ngắm theo **con trỏ chuột**). *Claw* và *Wave Slash* luôn ngắm bằng chuột |
| `Q` hoặc `E` | dùng **kĩ năng đã chọn** - độ **Dễ / Trung bình mang HAI** trong 4 kĩ năng (lá chọn đầu = phím `Q`, lá thứ hai = phím `E`), độ **Khó / Siêu khó chỉ MỘT** (Q hoặc E đều dùng được); chọn bằng lá bài trước khi vào ván (Esc = về menu). Màn chọn hiện 4 **lá bài poker**: `Assets/ui/poker.png` (xuất từ `poker.aseprite`, 1 khung, game tự cắt sát viền và phóng nguyên số lần) dùng chung cho mọi kĩ năng; muốn mỗi kĩ năng một hình riêng thì thêm `Assets/ui/skill_card_{nova|warp|stop|guard}.png` (từ `skill_card_{...}.aseprite`), lá nào có file riêng thì dùng file đó |
| ↳ Sóng xung kích | 30 mana, hồi 1s: gây sát thương (60 x hệ số sát thương) và hất văng mọi quái trong bán kính 120 |
| ↳ Làm chậm thời gian | 45 mana, 4 giây, hồi 8s: quái chạy và ra đòn còn ~45% tốc độ (boss ~70%), mèo chạy bình thường |
| ↳ Ngừng thời gian | 60 mana, **1,5 giây**, hồi 15s: mọi quái và boss (cả đòn của chúng) đứng yên hoàn toàn, không gây sát thương; mèo vẫn đi và đánh được |
| ↳ Bất tử | 50 mana, **2 giây**, hồi 12s: mọi sát thương lên mèo bị bỏ qua (hào quang vàng quanh mèo) |
| `Esc` / `P` | tạm dừng |
| `M` | bật/tắt âm thanh |
| `1` `2` `3` | chọn nhanh phần thưởng khi lên cấp |

## Đối chiếu yêu cầu đề bài
| Yêu cầu | Nằm ở đâu |
|---|---|
| Màn hình **Start** | `Forms/MainMenuForm` (Bắt đầu, Bảng thành tích, Hướng dẫn, Thông tin nhóm, Âm thanh, Thoát) |
| **Pause** / Restart / End | `Forms/PauseForm` (Esc/P), `Forms/GameOverForm` (thua hoặc hạ boss), luồng ở `Program.cs` + `Forms/GameForm` |
| **Bảng thành tích + xếp hạng** lưu **file** (`System.IO`) | `Services/Storage.cs` → `thanhtichs.txt`; hiển thị `Forms/LeaderboardForm` (top 10) |
| **Âm thanh bật/tắt** + Cài đặt | `Forms/SettingsForm` (mở từ menu chính / Tạm dừng: bật-tắt âm thanh, âm lượng nhạc nền & hiệu ứng, toàn màn hình), `Services/SoundService.cs` (bộ trộn âm tự viết trên waveOut/winmm), `Services/Settings.cs` (lưu `settings.txt`); phím `M` vẫn bật/tắt nhanh khi chơi |
| **Hướng dẫn** + **Thông tin nhóm** (Tên, MSSV, Lớp) | `Forms/HelpForm`, `Forms/AboutForm` (đọc `thongtinnhom.txt`) |
| **try…catch** khi đọc/ghi file, nạp tài nguyên | `Storage` (mọi hàm trả về thông báo lỗi), `AssetStore` (thiếu ảnh/âm thanh → hình thay thế + hộp cảnh báo), `SoundService`, `Program.ShowError` |

## Cấu trúc mã
```
CatSurvivorWin.sln
└─ CatSurvivorWin/
   ├─ Program.cs              luồng: Menu → GameForm → Chơi lại / Menu / Thoát
   ├─ Forms/                  MainMenu, Game(+GameCanvas), Pause, Upgrade, GameOver, Leaderboard, Help, About, Cheat  (mỗi form: .cs + .Designer.cs)
   │  └─ Controls/            PixelButton, PixelLabel, PixelTable, CatBanner, SkinnedForm (giao diện dùng chung)
   ├─ Game/                   logic game thuần (không phụ thuộc form): World, Player, Weapon/FusedWeapons, EnemyManager, BossAI, BossArena...
   ├─ Rendering/              Canvas (bọc GDI+), PixelFont (font bitmap Aseprite), UiSkin (da giao diện)
   ├─ Services/               Storage (file), SoundService (âm thanh), AssetStore (nạp tài nguyên an toàn)
   ├─ Core/                   Balance (mọi "trần" chỉ số), GameState, GameInput, MathX
   ├─ Sans/                   easter egg: trận đánh Sans (port C# từ Construct 2) - xem mục "Easter egg: Sans"
   ├─ Assets/                 hình + âm thanh (chép cạnh .exe)
   └─ SelfTest.cs             chế độ kiểm tra tự động
```
Game chạy ở độ phân giải logic **640×360** rồi phóng to đều lên cửa sổ (giữ tỉ lệ 16:9, viền đen nếu cửa sổ lệch tỉ lệ).
Vòng lặp: `GameForm.gameTimer` (~60 FPS) gọi `World.Update(dt)` rồi vẽ `GameCanvas` bằng GDI+: thế giới vẽ ở 640×360 vào `FrameBuffer` rồi phóng to bằng StretchBlt (nhanh khi đông quái), HUD vẽ ở độ phân giải cửa sổ. Khi hộp thoại (Pause/Nâng cấp/Kết thúc) mở thì game dừng hẳn.

## Vũ khí & asset vũ khí
| Vũ khí | Hình (`Assets/weapons/`) | Cách hoạt động |
|---|---|---|
| **Bow** (cung) | `bow.png` | cung lơ lửng cạnh mèo, kéo dây rồi bắn **mũi tên** vào quái gần nhất |
| **Knife** (dao) | `knife.png` | những con dao xoay quanh mèo |
| **Claw** (vuốt) | `claw.png` | vung nhát hình quạt theo hướng chuột, găng vuốt quét theo cung |
| **Holy Shield** (khiên thánh) | `holy.png` khung 1 | KHÔNG gây sát thương: mỗi bong bóng khiên đỡ trọn một đòn vào mèo (Lv3: 2 lớp, Lv6: 3 lớp), vỡ rồi tự hồi (`HolyShield` trong `Weapon.cs`) |
| **Holy Cross** (thánh giá) | `holy.png` khung 0 | thánh giá bay quanh mèo, **tự lao vào quái gần nhất** rồi bay về |
Các vũ khí hợp nhất (Star Ring, Wave Slash, Star Nova, Whirlwind, Holy Blades, Shockwave) dùng lại hình của vũ khí gốc.
Xuất lại hình từ Aseprite: `powershell -ExecutionPolicy Bypass -File tools\export_assets.ps1` (đọc `Documents\Aseprite\*.aseprite`).
Toạ độ cắt/điểm gốc của từng sprite nằm ở `Game/WeaponArt.cs` (chỉnh nếu bạn vẽ lại lệch vị trí). Thiếu file hình thì vũ khí tự vẽ hình tạm.

## Hiệu năng (FPS)
- Thế giới **và HUD** vẽ ở độ phân giải gốc 640×360 vào `FrameBuffer`, rồi phóng to lên cửa sổ bằng **một** lệnh StretchBlt thẳng (không qua bộ đệm kép của WinForms).
- Sprite quái vẽ thẳng bằng toạ độ thế giới (không đổi ma trận cho từng con) và chỉ vẽ phần có hình của mỗi khung (bỏ viền trong suốt); bóng quái là elip vẽ trực tiếp.
- Âm thanh chạy trên **luồng riêng** (`SoundService`): một bộ trộn âm đẩy dữ liệu vào waveOut nên game không bị khựng. Âm lượng nhạc / hiệu ứng được nhân thẳng vào mẫu âm thanh (kéo về 0 là im hẳn). Lưu ý: bản đầu dùng MCI `setaudio volume` nhưng driver waveaudio KHÔNG hỗ trợ lệnh này (lỗi 261) nên thanh âm lượng từng không có tác dụng.
- Vòng lặp dùng `timeBeginPeriod(1)` + timer 1 ms tự canh nhịp theo tần số quét màn hình (60–144 Hz), thay cho timer mặc định kẹt ~62 FPS.
- Đo bằng bot (1280×720, quái vây quanh mèo): cấp 27 với ~190 quái từ 11,3 ms xuống ~6,2 ms mỗi khung vẽ. `CatSurvivor.exe --selftest --prof` in thời gian từng phần.

## Hiệu ứng chết
Khi máu về 0, mèo **tưng lên rồi rơi khỏi màn hình** (camera đứng yên, vũ khí biến mất), sau đó mới hiện màn hình kết thúc (`Player.Update`, `World.Update`).
## Cảnh báo máu thấp
Khi máu dưới 35%, viền màn hình chuyển đỏ và "thở" theo nhịp tim: càng ít máu viền càng đậm, dày và nhịp càng nhanh (`Hud.LowHealthVignette` trong `Game/Hud.cs`).

## Độ khó theo cấp nhân vật
Quái mạnh lên theo **thời gian chơi cộng thêm 20 giây cho mỗi cấp** nhân vật (hằng số `LevelSecondsBonus` trong `Game/EnemyManager.cs`): máu, sát thương, giáp, tốc độ, loại quái và đội hình mở khoá đều tính theo mốc này.
Cấp càng cao quái càng đông (trần số quái cùng lúc: 150 + 2/cấp, tối đa 200) và sinh nhanh hơn.
**Bomber**: tốc độ gốc 80 (trước 55); khi cách mèo dưới 260px nó nước rút với tốc độ 1.35 x tốc độ chạy hiện tại của mèo (tối đa 300) nên luôn bắt kịp mèo chạy thẳng để châm ngòi và nổ - chỉ dash hoặc đổi hướng mới thoát (hằng số `BombSprintRange` / `BombChaseRatio` / `BombMaxSpeed` trong `Game/EnemyManager.cs`; kiểm tra `--selftest --bomber`).
## Chế độ độ khó
Chọn ở **Cài đặt của menu chính** (nút "Độ khó"; mở Cài đặt từ menu Tạm dừng thì bị khoá, không đổi giữa ván được). Lưu trong `settings.txt` (`difficulty=0..3`, mặc định 0). Các mức cộng dồn, logic ở `Core/Difficulty.cs`:

| Mức | Máu / sát thương quái (cả boss) | Quái dự đoán đường đi | Vùng báo hiệu đòn đánh | Hồi máu / thẻ máu | Điểm |
|---|---|---|---|---|---|
| **Dễ** | x1 | không có | có | bình thường | x1 |
| **Trung bình** | x2 | yếu | có | bình thường | x2 |
| **Khó** | x4 | hiệu quả | **tắt** | bình thường | x4 |
| **Siêu khó** | x6 | cực kì hiệu quả | tắt | **không hồi máu**, không có thẻ Máu tối đa / Hồi máu / Gà quay (giáp vẫn có) | x6 |

- **Vùng báo hiệu đòn đánh** (tắt từ Khó): phễu dơi, vòng nổ bomber, dải báo lao của Brute/boss, vòng báo giậm của boss, vòng báo vồ của Slime. Sóng xung kích của boss và hào quang nổi điên vẫn hiện. "Hiện hitbox" ở Cài đặt (debug) là tuỳ chọn riêng và luôn hiện vùng sát thương thật của các đòn này (kể cả khi độ khó đã tắt vùng báo hiệu).
- **Dự đoán đường đi** (`Game/Prediction.cs`): quái nhắm vào chỗ mèo SẼ đứng thay vì chỗ đang đứng, cho cả lúc đuổi lẫn lúc chọn hướng chiêu: Slime (đuổi + vồ), Brute (đuổi + lao), Dơi (chọn sườn để đứng + chỉ gào khi mèo sẽ nằm trong phễu lúc sóng tới), Bomber (có sẵn đón đầu từ trước; từ Khó dùng bản chuẩn hơn), Boss (đuổi + lao). Gồng chiêu có tính cả thời gian gồng. Mức tin / tầm nhìn trước: Trung bình 30% / 0.5s (vận tốc mèo bị làm mượt chậm nên chậm nhận ra đổi hướng), Khó 80% / 1s, Siêu khó 100% / 2s với vận tốc gần tức thời và giải đúng bài toán chặn đầu (có tính tốc độ quái). Dash của mèo vẫn là cách thoát.
- **Boss GIAI ĐOẠN 2** (`Game/MiniEyes.cs`, nối vào `BossAI`/`EnemyManager`): mỗi GIAI ĐOẠN là một **thanh máu riêng** (xem mục "Boss 3 giai đoạn" bên dưới); sang giai đoạn 2 mắt chính gầm gọi **4 mắt con**. Mắt chính **bất khả xâm phạm** (khiên xanh, thanh máu đổi màu, vũ khí tự ngắm bỏ qua nó) cho tới khi diệt hết 4 con; xong thì mắt chính choáng lâu hơn thường (cơ hội phản công). Mắt con: máu 110 x hệ số độ khó, **chủ động chạy trốn** mèo (lượn dọc tường, bị dồn góc thì lách ngang, tốc độ 125/148/171/194 px/s theo độ khó, mèo chạy 220) và dừng lại gồng ~0,6s để bắn **chưởng** (viên plasma 300 px/s, ngắm theo chỗ mèo sắp tới tuỳ độ khó; dash xuyên qua được). Khi còn đủ 4 con, mắt chính ra hiệu cho chúng **hợp thể**: xếp thành hình chữ thập quanh nó rồi quét **chưởng chữ thập xoay vòng** 5 giây (4 chùm tia dài 300px, quay 0,8-1,2 rad/s theo độ khó); diệt một con giữa chừng là chữ thập tan. Thân mắt con xoay mượt để con mắt luôn nhìn về mèo (gồng bắn thì nhìn đúng hướng ngắm, hợp thể chữ thập thì nhìn ra ngoài theo chùm tia); chưởng và chùm tia mọc ra từ đúng con mắt (quả cầu plasma dồn tụ khi gồng, chớp sáng ở nòng khi bắn, viên chưởng dài ra từ nòng và mờ dần về đuôi). Asset: small eye ball.aseprite -> Assets/enemies/minieye.png (6 khung 64x64, mắt nhìn TRÁI; khung 1 = trắng hoàn toàn khi bị đánh, 0·2-5 = bay), plasma-eyeball skill.aseprite -> Assets/enemies/plasma.png (dải plasma nằm ngang, tự cắt sát viền và kéo giãn). Cheat: `stage2` / `stage3` (cạn thanh máu boss hiện tại -> sang giai đoạn 2 / 3) để thử ngay.
- **Boss thông minh hơn** (`Core/Difficulty.cs` mục "boss", logic ở `Game/BossAI.cs`), cộng dồn theo mức:

  | | Dễ | Trung bình | Khó | Siêu khó |
  |---|---|---|---|---|
  | Chọn đòn theo tình huống (giậm chỉ khi mèo SẼ còn trong tầm; lao khi mèo ở xa / chạy thẳng / sát tường; không lặp đòn) | không (ngẫu nhiên) | 35% | 75% | 100% |
  | Tốc độ chạy / gồng / lao | x1 | x1.08 | x1.2 | x1.35 |
  | Hồi chiêu giữa hai đòn | x1 | x0.9 | x0.7 | x0.5 |
  | Đòn liên hoàn (tung ngay đòn kế tiếp) | 0 | 10% | 25% | 50% |
  | Vừa lao vừa bẻ lái bám theo chỗ mèo sẽ tới (rad/s) | không | không | 0.9 | 2.2 |
  | Thời gian kiệt sức khi đâm tường | x1 | x0.85 | x0.6 | x0.35, và biết dừng trước tường nên không tự choáng |

  Kiểm tra: `--selftest --bossbrain` (mèo chạy vòng 40s: tổng số đòn trong 40s ~9 / 10 / 12 / 18, tự choáng giảm dần về 0).
- **Điểm thành tích** nhân theo độ khó (bảng ở `Difficulty.ScoreMul`); bảng thành tích có cột "Độ khó".

Kiểm tra bằng `CatSurvivor.exe --selftest --difficulty --out <thư_mục>` (hệ số, hồi máu, thẻ nâng cấp, điểm và mô phỏng quái đuổi mèo đang chạy ngang).## Giao diện menu & thay asset
Mọi menu dùng chung một "da" giao diện kiểu pixel-art (`Rendering/UiSkin.cs`) và font Aseprite của game: nút `PixelButton`, chữ `PixelLabel`,
bảng `PixelTable`, form nền `SkinnedForm`, dải minh hoạ `CatBanner` (thư mục `Forms/Controls/`).
**Chưa có asset thì game tự vẽ; có asset thì chỉ cần thả PNG vào `Assets/ui/`**: `button_normal/hover/pressed/disabled.png`, `panel.png`, `menu_bg.png`, `logo.png`
(chi tiết kích thước, 9-slice, `skin.txt` trong **`docs/UI_ASSETS.md`**).
Muốn chỉnh bố cục: mở form trong **Designer** (giữ nguyên tên control + trình xử lý sự kiện như `btnStart` / `btnStart_Click` là logic vẫn chạy).
Phần HUD trong game (thanh máu, mana, XP, chữ) vẽ ở `Game/Hud.cs`. Thanh máu/mana/XP dùng asset của bạn: `heal bar and mana bar.aseprite` -> `tools\export_assets.ps1` xuất ra
`Assets/ui/hud_bars.png` (thanh đỏ trên, thanh xanh dưới, game tự dò vị trí) + `hud_head.png` (layer "Layer 2": đầu mèo vẽ đè lên thanh máu). Thanh được vẽ 3 mảnh (đầu trái, giữa kéo giãn, đầu phải)
nên đổi chiều dài thoải mái; thanh XP là thanh mana đổi sang xanh lá. Chỉnh vị trí/chiều dài trong `Hud.DrawBars`. Mana (`Player.Mana`, tối đa 100 - nâng tới 200 bằng thẻ "Mana tối đa +20", tự hồi 6/giây - nâng tới 15 bằng thẻ "Hồi mana +1.5/s"; xem `Balance.cs`) dành cho các kĩ năng chủ động Q / E (`Game/Skills.cs`); kĩ năng mới chỉ cần gọi `player.TrySpendMana(phí)`. Hai ô kĩ năng ở góc dưới trái HUD (`Hud.DrawSkills`, hiện vẽ bằng hình học tạm - thay bằng icon của bạn khi có).
## Cheat code (debug)
Trong lúc chơi bấm **F1** (hoặc phím **`**) để mở console, gõ lệnh rồi Enter (mũi tên Lên/Xuống gọi lại lệnh cũ, Esc đóng). Gõ `help` để xem đủ lệnh. **Chỉ có ở bản Debug** (F5 trong Visual Studio); bản Release tự tắt hẳn (`Cheats.Enabled` trong `Game/Cheats.cs`).

| Lệnh | Tác dụng |
|---|---|
| `god [on/off]` · `heal` · `dash` | bất tử · hồi đầy máu + mana + dash · hồi dash |
| `xp <n>` · `level <n>` · `boss` | cộng XP · lên thẳng cấp n (tự chọn nâng cấp ngẫu nhiên) · vào trận boss ngay |
| `bosshp <n>` · `bossdie` | đặt máu boss · giết boss |
| `weapon <bow/knife/claw/shield/cross/all> [cấp]` · `fuse` | thêm/đặt cấp vũ khí · hợp nhất mọi cặp đủ điều kiện |
| `equip <helm/body/boots/shield/all> [cấp]` · `stats max` | mặc giáp · chỉ số lên trần |
| `spawn <slime/bat/bomber/brute> [số] [special/cỡ]` · `formation <ring/wall/vflock/pincer/squad/escort>` | tạo quái / đội hình |
| `killall` · `gems <n>` | giết mọi quái · rải XP |
| `time <giây>` · `timescale <0.1-10>` | đặt thời gian chơi (quái mạnh dần theo giờ) · tăng/giảm tốc game |
| `info` | xem trạng thái |

Khi đã dùng cheat, góc phải màn hình hiện nhãn `DEBUG`.
## Boss 3 giai đoạn (mỗi giai đoạn một thanh máu)
- **Thanh máu theo giai đoạn**: mắt chính có 3 thanh máu nối tiếp, mỗi thanh `Balance.BossBarHp` (1000) x hệ số độ khó (Dễ 1000 · Trung bình 2000 · Khó 4000 · Siêu khó 6000). Cạn thanh thì sang giai đoạn kế và thanh **đầy lại** (trong lúc chuyển mắt chính bất khả xâm phạm); cạn thanh thứ 3 thì mắt chính chết. `BossData.Stage` = 1..3, `Phase` (mức hung hăng) bám theo giai đoạn.
  1. **Giai đoạn 1**: mắt chính một mình (lao / giậm / gầm triệu hồi).
  2. **Giai đoạn 2**: gầm gọi **4 mắt con**; mắt chính bất khả xâm phạm (thanh xanh dương) tới khi diệt hết 4 con, xong thì choáng lâu hơn để phản công.
  3. **Giai đoạn 3 (nổi điên)**: gọi thêm **mắt chính thứ hai** (xuất hiện ở phía đối diện, thanh máu riêng -> tổng 2 thanh) - **mỗi mắt chính có 4 mắt con của riêng nó (tổng 8 mắt con)**, khiên và chữ thập tính theo 4 con của mắt đó (`MiniEyeData.Owner`). Hạ cả hai mới thắng; một mắt chết thì 4 mắt con của nó biến mất.
- **Khó / Siêu khó**: cứ **40 giây** mắt chính gầm gọi **bù** mắt con (đủ lại 4 con mỗi mắt chính, không nhân đôi con còn sống) nếu đã mất con nào - khiên bật lại (`Difficulty.BossResummonInterval`; đồng hồ chạy suốt giai đoạn 2-3, đòn chỉ tung lúc mắt chính đang đi đuổi).
- **Mắt con trâu hơn ở cả 4 chế độ**: `MiniEyes.Hp` 110 -> **260** (x độ khó: Dễ 260 · Trung bình 520 · Khó 1040 · Siêu khó 1560).
- **HUD**: thanh máu từ `Assets/ui/boss_bar.png` + `boss_bar_head.png` (xuất từ `boss_health_bar.aseprite` bằng `tools\export_assets.ps1`: "Layer 1" = thanh, các layer còn lại = đám mắt thò lên), vẽ NGUYÊN cỡ ở đáy màn hình, mỗi mắt chính một thanh (hai mắt -> hai thanh cạnh nhau), ghi tên + giai đoạn + số máu; bất khả xâm phạm thì thanh đổi sang xanh dương. Chưa có asset thì vẽ thanh phẳng.
- Cheat: `stage2` / `stage3` (cạn thanh hiện tại), `bosshp <n>` (máu thanh hiện tại), `bossdie` (hạ mọi mắt chính). Kiểm tra: `CatSurvivor.exe --selftest --phases --out <thư_mục>`.

## Brute có khiên
Brute (khủng long, `Assets/enemies/brute.png` xuất từ `brute.aseprite`) **cầm khiên**: khiên có độ bền = `EnemyTypes.BruteShieldRatio` (0,6) x máu tối đa. Còn khiên thì đòn đánh chỉ trừ vào khiên (sau giáp, không mất máu, không nháy trắng; phần dư của đòn làm vỡ khiên mới tràn sang máu) và Brute vẫn gồng + lao như cũ. **Khiên vỡ** thì huỷ ngay đòn đang gồng/lao/kiệt sức, từ đó Brute **không bao giờ lao nữa**, chỉ đi chậm theo mèo (tốc độ đi như cũ, chỉnh ở `BruteBrokenSpeedMul`) và bắt đầu mất máu. Khiên vỡ rồi không có lại.
- Hình (10 khung 64x64, nhìn TRÁI): 0 gồng/lao · 1-2 KHIÊN rời (vẽ đè lên thân khi còn khiên; để trống thì game vẽ khiên tạm bằng ô vuông) · 3,6,7,8 đi · 4 trắng (dáng có khiên, hiện chưa dùng) · 5 trắng (trúng đòn khi đã lộ máu) · 9 kiệt sức/chết/bị văng. Khiên bị đánh: quầng sáng + thanh độ bền nhỏ trên đầu; vỡ: mảnh khiên bay ra. Chưa có `brute.png` thì Brute là hình tròn tạm (có vòng khiên).
- Kiểm tra: `CatSurvivor.exe --selftest --brute --out <thư_mục>` (khiên hấp thụ đòn, vỡ rồi không lao nữa nhưng vẫn đi, phá khiên giữa lúc gồng/lao, killall, ảnh các tư thế `brute_states.png`).

## Easter egg: Sans
> **Tài sản Undertale KHÔNG nằm trong repo** (`Assets/sans/**` và `Assets/characters/sans.png` bị `.gitignore` chặn vì thuộc Toby Fox). Thiếu thư mục `Assets/sans` thì easter egg tự TẮT (nhập tên SANS chỉ lưu thành tích bình thường, game không lỗi). Muốn bật: chạy `tools\import_sans_fight.ps1` (tải hình/âm thanh từ repo gốc về `Assets/sans`) và `tools\export_assets.ps1` (xuất `characters/sans.png` từ `Sans.aseprite` của bạn).

Cuối ván, ở màn hình kết quả, nhập tên **SANS** (hoa/thường tuỳ ý: `sans`, `SANS`, `SaNs`...) rồi **Lưu thành tích** -> mở trận đánh với Sans (`SansEasterEgg.Matches` đổi tên về chữ thường rồi so sánh). Thành tích vẫn được lưu như bình thường.
- **Trận đánh** là bản **port sang C#** của dự án Construct 2 *Bad Time Simulator* (github.com/Jcw87/c2-sans-fight, cả logic lẫn hình/âm thanh), chạy ở 640x480 trong cửa sổ riêng `SansFightForm`. Mũi tên/WASD: di chuyển trái tim · **Z/Enter**: xác nhận · **X/Shift**: huỷ + đi chậm · Esc: bỏ cuộc · M: bật/tắt âm.
  Menu FIGHT / ACT / ITEM / MERCY như bản gốc; mỗi lần bấm FIGHT là một lượt Sans né rồi tấn công. Trái tim đỏ đi tự do, **xanh** có trọng lực (nhảy bằng mũi tên ngược hướng trọng lực, giữ phím để nhảy cao); xương **xanh** chỉ đau khi bạn đang di chuyển, xương **cam** chỉ đau khi bạn đứng yên; có Gaster Blaster, xương đâm, bục, đập trái tim xuống (slam), KR (karma rút máu dần).
  Thắng = sống sót tới khi Sans kiệt sức (23 lần FIGHT, kể cả đòn cuối); thua = hết máu.
- **Phần thưởng**: đánh bại Sans mở khoá **nhân vật SANS** (lưu trong `settings.txt`: `sans=1`). Từ đó mỗi ván có màn **Chọn nhân vật** (Mèo / Sans) trước màn chọn kĩ năng; Sans chỉ khác **hình dáng** (chỉ số/vũ khí/kĩ năng giữ nguyên). Hình nhân vật: `Assets/characters/sans.png` (xuất từ `Sans.aseprite` bằng `tools\export_assets.ps1`: 5 khung 64x64, 0-1 chạy nhìn phải, 2-4 đứng yên). Đầu Sans thò lên thanh máu: tuỳ chọn `Assets/ui/hud_head_sans.png` (từ `hud head sans.aseprite`); chưa vẽ thì thanh máu để trống phần đầu khi chơi Sans.
- **Cấu trúc port** (`Sans/`): `SansTimeline` = bộ chạy kịch bản CSV của repo gốc (biến, nhãn, nhảy, toán); `SansAttackData` = 24 kịch bản đòn đánh nhúng thẳng trong C# (sinh bởi `tools\import_sans_fight.ps1` từ các file `.csv` của repo); `SansFight*.cs` = logic + vẽ (chia theo mảng: trái tim, đòn đánh, menu, Sans, vẽ); `SansFightForm` = cửa sổ/vòng lặp; `SansPad` = phím. Hình/âm thanh ở `Assets/sans/` (cũng do script trên chép/đổi `.ogg` -> `.wav`).
- Debug: trong cửa sổ trận Sans, **F2** nhảy tới lượt cuối, **F3** bật/tắt bất tử (chỉ bản Debug).
- Kiểm tra: `CatSurvivor.exe --selftest --sans --out <thư_mục>` (chạy từng kịch bản, bot đánh hết trận, chết, menu, vật lý trái tim, xương xanh/cam, skin Sans, chụp ảnh `sans_*.png`).
- *Undertale* và nhạc/hình/nhân vật Sans thuộc Toby Fox; trận đánh này là sản phẩm fan (repo gốc không có giấy phép riêng) - chỉ dùng cho bài tập, đừng phát hành rộng rãi cùng file `Assets/sans`.

## Dữ liệu và file
- **`thanhtichs.txt`** (có sẵn file MẪU 5 dòng ở thư mục dự án, chép cạnh file .exe khi build; chưa có file thì tự tạo khi lưu điểm lần đầu; dòng `#` là chú thích, bị bỏ qua khi đọc): mỗi dòng `Tên|Điểm|Giây|Cấp|Kills|Thắng(1/0)|Ngày giờ|Độ khó(0..3)` (dòng cũ thiếu cột độ khó được coi là Dễ). Dòng hỏng bị bỏ qua, không văng.
  Xếp hạng: điểm cao → thấp, bằng điểm thì thời gian ngắn hơn xếp trên.
- **Công thức điểm**: `Kills × 10 + (Cấp − 1) × 100`; hạ boss thêm `5000 + 2 × (giây còn lại so với mốc 1800)`; tổng được **nhân theo độ khó** (x1 / x2 / x4 / x6).
- **`thongtinnhom.txt`** (nằm ngay trong project): mỗi dòng `Họ tên|MSSV|Lớp` – **hãy sửa thành thông tin thật của nhóm**.
- **`settings.txt`**: `sound=1/0`, `music=0..100` (âm lượng nhạc), `sfx=0..100` (âm lượng hiệu ứng), `fullscreen=1/0`, `autoaim=1/0`, `hitbox=1/0` (Cài đặt > Hiện hitbox: vẽ vòng va chạm của mèo/quái/đạn, vùng sát thương vũ khí và vùng sát thương các đòn của quái: dải lao/vồ, phễu gào của dơi, vùng nổ, vòng giậm + sóng của boss, chưởng và chùm tia của mắt con), `difficulty=0..3` (0 Dễ, 1 Trung bình, 2 Khó, 3 Siêu khó), `sans=1/0` (đã mở khoá nhân vật Sans), `character=0/1` (0 Mèo, 1 Sans). Các mục trước chỉnh trong menu Cài đặt (không hiện lúc đang chơi); `sans`/`character` do game tự ghi.
- Thư mục dữ liệu = thư mục chứa `.exe`; nếu không ghi được thì dùng `%AppData%\CatSurvivor`.

## Thay asset
- Hình: ghi đè file cùng tên trong `Assets/` (nhân vật `characters/main_character.png`, quái `enemies/{slime,bat,bomber}.png` (bomber: khung 13-15 là hiệu ứng nổ, tự phóng theo bán kính nổ; khung 16 là khung trắng hoàn toàn khi bị đánh), nền `environment/grass.png`, XP `items/experiences.png`; vùng boss rộng 1000x560 - lớn hơn màn hình, camera đi theo mèo và dừng đúng ở mép vùng (không còn ảnh viền), chỉnh ở `Balance.BossArenaSize`).
- **Boss** ("Mắt Quỷ Đột Biến"): dùng `Assets/enemies/eyeboss.png` (xuất từ `EyeBoss.aseprite` bằng `tools\export_assets.ps1`; 4 khung 64x64: 0 nhìn thẳng · 1 trái · 2 lên · 3 trái-lên; game lật ngang/dọc để ra đủ 8 hướng, mắt luôn nhìn theo mèo trừ lúc lao mình tới; mắt nằm giữa khung, vẽ phóng 2 lần, hitbox bán kính 32). Không có file này thì dùng `enemies/boss.png` (ảnh tĩnh), không có nữa thì vẽ hình tạm. **Giáp**: ảnh món đồ 64x64 `Assets/equipment/icons/{helm|body|boots|shield}_{1..5}.png` (xuất từ `helmet/chestplate/leg/shield_n.aseprite`) hiện ở HUD (hàng icon dưới thanh mana, thu nhỏ 1/2); thẻ nâng cấp KHÔNG hiện icon giáp; mèo KHÔNG mặc giáp trên người; đã có cấp 1 (Vải) và 2 (Sắt), tên cấp: Vải → Sắt → Thép → Mithril → Rồng. (xem `docs/ARMOR_DESIGN.md`).
- **Biến thể màu của quái đang ẨN** (`Balance.ShowColorVariants = false`): quái vẫn mạnh dần theo bậc nhưng vẽ bằng sprite gốc.
  Sprite vẽ tay cho từng bậc: đặt `Assets/enemies/{slime|bat|bomber}_v{1..4}.png` (cùng bố cục dải frame với file gốc) – game tự dùng.
- Icon kĩ năng Q / E (HUD): `Assets/ui/skills.png` = dải ngang các khung VUÔNG (cạnh = chiều cao ảnh), khung 0 = Sóng xung kích, 1 = Làm chậm thời gian, 2 = Ngừng thời gian, 3 = Bất tử; vẽ trong `skills.aseprite` rồi chạy `tools\export_assets.ps1`. Icon vẽ đúng kích thước gốc, căn giữa ô; ô tự to ra theo icon (tối thiểu 30px); chưa có file thì dùng hình tạm.
- Âm thanh: ghi đè `Assets/audio/sfx/*.wav` (click, hurt, block, kill, levelup, dash, pickup, shot, boss, slam, win, lose) và `Assets/audio/bgm.wav` (WAV PCM 8/16 bit, mono hoặc stereo, tần số bất kì - tự đổi sang 44100 Hz). Hiện là âm thanh **tạm tự sinh**.
- Font chữ trong game: font Aseprite (`Assets/ui/aseprite_font.*`). Font chỉ có một phần chữ tiếng Việt có dấu – chữ thiếu tự dùng Segoe UI.

## Tự kiểm tra
`CatSurvivor.exe --selftest --out <thư_mục>` chạy mô phỏng 150 giây, đánh boss, kiểm tra chết/điểm, đọc-ghi file (kể cả đường lỗi),
âm thanh, chụp ảnh các form và đo FPS; kết quả ghi vào `selftest.log` cùng các ảnh PNG. (`--forms` chỉ chạy phần form.) `--sans` kiểm tra riêng trận đánh Sans (easter egg).

## Ghi công
Font: *Aseprite Font* © David Capello, giấy phép CC BY 4.0 (xem `Assets/ui/aseprite_font_LICENSE.txt`). Hình ảnh: tự vẽ bằng Aseprite. Easter egg Sans: logic và hình/âm thanh từ *Bad Time Simulator (Sans Fight)* của Jcw87 (Construct 2, github.com/Jcw87/c2-sans-fight) được port sang C#; *Undertale* © Toby Fox. Hình nhân vật Sans chơi được do bạn tự vẽ.

> Lưu ý về quy định của đề: giao diện các form hiện là bản tạm do công cụ AI viết khung; nếu đề cấm UI do AI tạo thì hãy tự thiết kế lại các form trong Designer như trên.
