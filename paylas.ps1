# =============================================================================
#  GTACoop Remake - Paylaşım Paketi Oluştur
#  dist\GTACoopRemake + arkadaş okubeni (TR/EN) + lisans -> dist\GTACoop-Remake-Share.zip
#  Kullanım:  powershell -ExecutionPolicy Bypass -File paylas.ps1
#  (Önce build.ps1 ile dist üretilmiş olmalı.)
# =============================================================================

$ErrorActionPreference = 'Stop'

$root  = Split-Path -Parent $MyInvocation.MyCommand.Path
$dist  = Join-Path $root 'dist\GTACoopRemake'
$zip   = Join-Path $root 'dist\GTACoop-Remake-Share.zip'
$oldZip = Join-Path $root 'dist\GTACoop-Remake-Paylas.zip'
$okubeni = Join-Path $root 'ARKADAS-OKUBENI.txt'
$katil   = Join-Path $root 'KODLA-KATIL.txt'
$okubeniEn = Join-Path $root 'READ-ME-EN.txt'
$katilEn   = Join-Path $root 'JOIN-WITH-CODE-EN.txt'
$stage = Join-Path $env:TEMP ('gtacoop-share-' + [Guid]::NewGuid().ToString('N'))

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
if (-not (Test-Path $katil)) {
    Write-Host "HATA: KODLA-KATIL.txt bulunamadı." -ForegroundColor Red
    exit 1
}
if (-not (Test-Path $okubeniEn)) {
    Write-Host "HATA: READ-ME-EN.txt bulunamadı." -ForegroundColor Red
    exit 1
}
if (-not (Test-Path $katilEn)) {
    Write-Host "HATA: JOIN-WITH-CODE-EN.txt bulunamadı." -ForegroundColor Red
    exit 1
}

# --- Aşama klasörüne kopyala ---
New-Item -ItemType Directory -Force -Path (Join-Path $stage 'GTACoop-Remake') | Out-Null
Copy-Item (Join-Path $dist '*') (Join-Path $stage 'GTACoop-Remake') -Recurse -Force
Copy-Item $okubeni (Join-Path $stage 'GTACoop-Remake\OKUBENI-ARKADASLAR.txt') -Force
Copy-Item $katil (Join-Path $stage 'GTACoop-Remake\KODLA-KATIL.txt') -Force
Copy-Item $okubeniEn (Join-Path $stage 'GTACoop-Remake\READ-ME-EN.txt') -Force
Copy-Item $katilEn (Join-Path $stage 'GTACoop-Remake\JOIN-WITH-CODE-EN.txt') -Force

# --- Zip'le ---
if (Test-Path $zip) { Remove-Item $zip -Force }
if (Test-Path $oldZip) { Remove-Item $oldZip -Force }  # eski Türkçe adlı paket
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
Write-Host "  - OKUBENI-ARKADASLAR.txt / READ-ME-EN.txt (TR/EN arkadaş kurulum talimatı)"
Write-Host "  - KODLA-KATIL.txt / JOIN-WITH-CODE-EN.txt (TR/EN katılım kodu ve mod klasörleri rehberi)"
Write-Host "  - Distribution\Components\OpenIV.asi (paket içi OpenIV.asi)"
Write-Host "  - Distribution\LICENSE.md (GPL v3 lisans metni) + atıf"
