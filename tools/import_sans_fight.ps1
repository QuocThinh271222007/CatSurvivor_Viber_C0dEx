# Nhập dữ liệu trận đánh Sans (easter egg) từ repo https://github.com/Jcw87/c2-sans-fight vào game:
#   powershell -ExecutionPolicy Bypass -File tools\import_sans_fight.ps1 [-Repo <thư mục đã clone>]
# Việc làm:
#   1) (nếu chưa có) git clone repo về thư mục tạm
#   2) chép hình (Animations\*, Textures\*) -> CatSurvivorWin\Assets\sans\
#   3) đổi âm thanh .ogg -> .wav (ffmpeg): hiệu ứng 44,1 kHz stereo, nhạc nền 32 kHz mono cho nhẹ file
#   4) sinh CatSurvivorWin\Sans\SansAttackData.cs từ các file Files\*.csv (kịch bản các đòn của Sans, nhúng thẳng vào mã C#)
# Chạy lại an toàn (ghi đè). Chỉ cần chạy khi muốn cập nhật từ repo gốc; kết quả đã nằm sẵn trong dự án.
param(
  [string]$Repo = "",
  [string]$Ffmpeg = ""
)
$ErrorActionPreference = "Stop"
$proj = Join-Path $PSScriptRoot "..\CatSurvivorWin"
$assets = Join-Path $proj "Assets\sans"

if (-not $Repo) {
  $Repo = Join-Path $env:TEMP "c2-sans-fight"
  if (-not (Test-Path $Repo)) { git clone --depth 1 https://github.com/Jcw87/c2-sans-fight.git $Repo }
}
if (-not (Test-Path (Join-Path $Repo "Files"))) { Write-Error "Không thấy thư mục Files trong repo: $Repo"; exit 1 }

# ---- 2) hình ----
$anims = @("GasterBlaster","HeartShard","HP","KR","MenuBoneBottom","MenuBoneLeft","PlayerHeart","SansBody","SansHead","SansLegs","SansSweat","SansTorso","SpeechBubble","Strike","Target","TargetChoice","UIAct","UIFight","UIItem","UIMercy")
foreach ($a in $anims) {
  $dst = Join-Path $assets "Animations\$a"
  New-Item -ItemType Directory -Force $dst | Out-Null
  Copy-Item (Join-Path $Repo "Animations\$a\*") $dst -Recurse -Force
}
$tex = Join-Path $assets "Textures"
New-Item -ItemType Directory -Force $tex | Out-Null
foreach ($t in "BattleFont","DamageFont","DefaultFont","SansFont","BoneH","BoneV","BoneStabH","BoneStabV","BoneStabWarn","CombatZone","Platform1","Platform2","GasterBlast1","GasterBlast2","GasterBlast3")  {
  Copy-Item (Join-Path $Repo "Textures\$t.png") $tex -Force
}
Write-Host "OK  hình -> $assets"

# ---- 3) âm thanh ----
if (-not $Ffmpeg) { $Ffmpeg = (Get-Command ffmpeg -ErrorAction SilentlyContinue).Source }
if (-not $Ffmpeg) { Write-Warning "Không có ffmpeg: bỏ qua bước đổi âm thanh (game vẫn chạy, chỉ không có tiếng cho trận Sans)."; }
else {
  $aud = Join-Path $assets "audio"
  New-Item -ItemType Directory -Force $aud | Out-Null
  foreach ($ogg in Get-ChildItem (Join-Path $Repo "Files") -Filter *.ogg) {
    $wav = Join-Path $aud ($ogg.BaseName + ".wav")
    if ($ogg.BaseName -like "mus_*") { & $Ffmpeg -y -v error -i $ogg.FullName -ar 32000 -ac 1 -sample_fmt s16 $wav }
    else { & $Ffmpeg -y -v error -i $ogg.FullName -ar 44100 -ac 2 -sample_fmt s16 $wav }
  }
  Write-Host "OK  âm thanh -> $aud"
}

# ---- 4) kịch bản đòn đánh -> C# ----
$out = Join-Path $proj "Sans\SansAttackData.cs"
New-Item -ItemType Directory -Force (Split-Path $out) | Out-Null
$sb = New-Object System.Text.StringBuilder
[void]$sb.AppendLine("namespace CatSurvivor;")
[void]$sb.AppendLine("")
[void]$sb.AppendLine("/// <summary>")
[void]$sb.AppendLine("/// Kịch bản các đòn đánh của Sans (nhúng thẳng vào mã, sinh bởi tools/import_sans_fight.ps1 từ Files/*.csv của repo Jcw87/c2-sans-fight).")
[void]$sb.AppendLine("/// Mỗi dòng: thời gian chờ (giây), tên lệnh, các tham số - SansTimeline đọc và chạy từng dòng, xem Documentation của repo gốc để biết các lệnh.")
[void]$sb.AppendLine("/// </summary>")
[void]$sb.AppendLine("internal static class SansAttackData")
[void]$sb.AppendLine("{")
[void]$sb.AppendLine("`tpublic static readonly Dictionary<string, string> Scripts = new()")
[void]$sb.AppendLine("`t{")
foreach ($csv in Get-ChildItem (Join-Path $Repo "Files") -Filter *.csv | Sort-Object Name) {
  $text = ([System.IO.File]::ReadAllText($csv.FullName) -replace "`r`n","`n").Trim("`n"," ")
  # bỏ các dấu phẩy thừa ở cuối dòng cho gọn
  $lines = $text.Split("`n") | ForEach-Object { $_.TrimEnd(",", " ", "`r") }
  [void]$sb.AppendLine("`t`t[`"$($csv.BaseName)`"] = `"`"`"")
  foreach ($l in $lines) { [void]$sb.AppendLine("`t`t`t$l") }
  [void]$sb.AppendLine("`t`t`t`"`"`",")
}
[void]$sb.AppendLine("`t};")
[void]$sb.AppendLine("}")
[System.IO.File]::WriteAllText($out, $sb.ToString(), (New-Object System.Text.UTF8Encoding($false)))
Write-Host "OK  kịch bản -> $out"
