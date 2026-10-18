# GTACoop Remake v2 — Yapım Betiği
# Kullanım:  powershell -ExecutionPolicy Bypass -File build.ps1
# Çıktı:     dist\GTACoopRemake\  (GTACoop.Launcher.exe buradan çalıştırılır)

$ErrorActionPreference = "Stop"

$root = $PSScriptRoot
$dist = Join-Path $root "dist\GTACoopRemake"
$vendor = Join-Path $root "vendor\Distribution"

Write-Host "== GTACoop Remake v2 yapımı ==" -ForegroundColor Cyan
Write-Host "Kök : $root"
Write-Host "Çıktı: $dist"
Write-Host ""

# --- temizlik
if (Test-Path $dist) {
    Write-Host "[1/5] Eski çıktı temizleniyor..."
    Remove-Item $dist -Recurse -Force
} else {
    Write-Host "[1/5] Temizlik gerekmiyor."
}
New-Item -ItemType Directory -Force $dist | Out-Null

# --- launcher publish
Write-Host "[2/5] Launcher yayınlanıyor..."
dotnet publish (Join-Path $root "src\GTACoop.Launcher\GTACoop.Launcher.csproj") `
    -c Release -o $dist --nologo
if ($LASTEXITCODE -ne 0) { throw "Launcher yayını başarısız." }

# --- sync sunucusu publish
Write-Host "[3/5] Senkronizasyon sunucusu yayınlanıyor..."
$syncOut = Join-Path $dist "Distribution\SyncServer"
dotnet publish (Join-Path $root "src\GTACoop.SyncServer\GTACoop.SyncServer.csproj") `
    -c Release -o $syncOut --nologo
if ($LASTEXITCODE -ne 0) { throw "SyncServer yayını başarısız." }

# --- GTACoOp istemci + sunucu dağıtımı (vendor)
Write-Host "[4/5] GTACoOp istemci/sunucu dosyaları kopyalanıyor..."
$serverSrc = Join-Path $vendor "Server"
$scriptsSrc = Join-Path $vendor "Scripts"
if (-not (Test-Path $serverSrc)) { throw "Dağıtım bulunamadı: $serverSrc" }

robocopy $serverSrc (Join-Path $dist "Distribution\Server") /E /NFL /NDL /NJH /NJS /NP | Out-Null
robocopy $scriptsSrc (Join-Path $dist "Distribution\Scripts") /E /NFL /NDL /NJH /NJS /NP | Out-Null

# --- lisans + atıf dosyaları (GPL v3 paylaşım uyumu) ---
Copy-Item (Join-Path $vendor "LICENSE.md") (Join-Path $dist "Distribution") -Force
Copy-Item (Join-Path $vendor "README.md") (Join-Path $dist "Distribution\ATIF-README.md") -Force

# --- SyncTool (ileri düzey araç)
Write-Host "[5/5] SyncTool yayınlanıyor..."
$toolOut = Join-Path $dist "tools\SyncTool"
dotnet publish (Join-Path $root "src\GTACoop.SyncTool\GTACoop.SyncTool.csproj") `
    -c Release -o $toolOut --nologo
if ($LASTEXITCODE -ne 0) { throw "SyncTool yayını başarısız." }

# --- kök klasöre sızan gereksiz SyncServer apphost dosyalarını temizle
# (gerçek kopya Distribution\SyncServer içindedir)
Remove-Item (Join-Path $dist "GTACoop.SyncServer.exe") -ErrorAction SilentlyContinue
Remove-Item (Join-Path $dist "GTACoop.SyncServer.deps.json") -ErrorAction SilentlyContinue
Remove-Item (Join-Path $dist "GTACoop.SyncServer.runtimeconfig.json") -ErrorAction SilentlyContinue

Write-Host ""
Write-Host "== YAPIM TAMAM ==" -ForegroundColor Green
Write-Host "Çalıştır: $dist\GTACoop.Launcher.exe"
Write-Host ""
Write-Host "İpucu: Güzel konuma kurmak/güncellemek için:  powershell -ExecutionPolicy Bypass -File kur.ps1" -ForegroundColor Yellow
Write-Host ""
Write-Host "İçerik:"
Get-ChildItem $dist | ForEach-Object { Write-Host "  $($_.Name)" }
