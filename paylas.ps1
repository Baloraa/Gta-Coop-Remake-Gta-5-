# =============================================================================
#  GTACoop Remake - Paylaşım Paketi Oluştur
#  dist\GTACoopRemake + arkadaş okubeni + lisans -> dist\GTACoop-Remake-Paylas.zip
#  Kullanım:  powershell -ExecutionPolicy Bypass -File paylas.ps1
#  (Önce build.ps1 ile dist üretilmiş olmalı.)
# =============================================================================

$ErrorActionPreference = 'Stop'

$root  = Split-Path -Parent $MyInvocation.MyCommand.Path
$dist  = Join-Path $root 'dist\GTACoopRemake'
$zip   = Join-Path $root 'dist\GTACoop-Remake-Paylas.zip'
$okubeni = Join-Path $root 'ARKADAS-OKUBENI.txt'
$stage = Join-Path $env:TEMP ('gtacoop-paylas-' + [Guid]::NewGuid().ToString('N'))

Write-Host "== GTACoop Remake Paylaşım Paketi ==" -ForegroundColor Cyan

if (-not (Test-Path (Join-Path $dist 'GTACoop.Launcher.exe'))) {
    Write-Host "HATA: dist bulunamadı. Önce 'build.ps1' çalıştırın." -ForegroundColor Red
    exit 1
}
if (-not (Test-Path (Join-Path $dist 'Distribution\LICENSE.md'))) {
    Write-Host "HATA: lisans dosyası dağıtımda yok. build.ps1 güncel mi?" -ForegroundColor Red
    exit 1
}
if (-not (Test-Path $okubeni)) {
    Write-Host "HATA: ARKADAS-OKUBENI.txt bulunamadı." -ForegroundColor Red
    exit 1
}

# --- Aşama klasörüne kopyala ---
New-Item -ItemType Directory -Force -Path (Join-Path $stage 'GTACoop-Remake') | Out-Null
Copy-Item (Join-Path $dist '*') (Join-Path $stage 'GTACoop-Remake') -Recurse -Force
Copy-Item $okubeni (Join-Path $stage 'GTACoop-Remake\OKUBENI-ARKADASLAR.txt') -Force

# --- Zip'le ---
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path (Join-Path $stage 'GTACoop-Remake') -DestinationPath $zip -CompressionLevel Optimal

# --- Aşamayı temizle (sadece geçici klasör) ---
Remove-Item $stage -Recurse -Force

$mb = [math]::Round((Get-Item $zip).Length / 1MB, 1)
Write-Host ""
Write-Host "TAMAM! Paylaşım paketi hazır:" -ForegroundColor Green
Write-Host "  $zip  ($mb MB)"
Write-Host ""
Write-Host "Bu ZIP'i arkadaşlarınıza gönderebilirsiniz. İçinde:"
Write-Host "  - Uygulama (GTACoop.Launcher.exe ve tüm dosyalar)"
Write-Host "  - OKUBENI-ARKADASLAR.txt (arkadaşınız için kurulum talimatı)"
Write-Host "  - Distribution\LICENSE.md (GPL v3 lisans metni) + atıf"
