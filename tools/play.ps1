# Build rồi chạy game từ thư mục riêng "play\" (không khoá file trong bin\ nên Visual Studio vẫn build/chạy F5 bình thường).
#   powershell -ExecutionPolicy Bypass -File tools\play.ps1
$sol = Split-Path $PSScriptRoot
$play = Join-Path $sol "play"

# đóng bản đang chạy ở play\ (nếu có) - không đụng tới bản chạy từ Visual Studio
Get-Process CatSurvivor -ErrorAction SilentlyContinue | Where-Object { $_.Path -like "$play\*" } | Stop-Process -Force
Start-Sleep -Milliseconds 500

dotnet build (Join-Path $sol "CatSurvivorWin.sln") -nologo -v q
if ($LASTEXITCODE -ne 0) { Write-Error "Build lỗi"; exit 1 }

# chép bản build sang play\, giữ lại điểm số và cài đặt cũ
robocopy (Join-Path $sol "CatSurvivorWin\bin\Debug\net8.0-windows") $play /MIR /XF thanhtichs.txt settings.txt /NFL /NDL /NJH /NJS | Out-Null
Start-Process (Join-Path $play "CatSurvivor.exe") -WorkingDirectory $play
