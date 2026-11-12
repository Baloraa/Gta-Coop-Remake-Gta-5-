# =============================================================================
#  GTACoop Remake - WPF Launcher Paylaşım Paketi
#  src\GTACoop.Launcher.Wpf -> dist\GTACoopRemake-Wpf + dağıtım dosyaları
#  -> dist\GTACoop-Remake-<sürüm>.zip
#  Kullanım:  powershell -ExecutionPolicy Bypass -File wpf-paket.ps1
#  Not: WinForms sürümünün build.ps1/paylas.ps1 dosyalarına dokunmaz.
# =============================================================================

$ErrorActionPreference = 'Stop'

$root    = Split-Path -Parent $MyInvocation.MyCommand.Path
$proj    = Join-Path $root 'src\GTACoop.Launcher.Wpf\GTACoop.Launcher.Wpf.csproj'
$out     = Join-Path $root 'dist\GTACoopRemake-Wpf'
$vendor  = Join-Path $root 'vendor\Distribution'

$version = ([xml](Get-Content $proj -Raw)).Project.PropertyGroup |
    ForEach-Object { $_.Version } |
    Where-Object { $_ } |
    Select-Object -First 1
if ([string]::IsNullOrWhiteSpace($version)) { $version = 'dev' }
$version = ([string]$version).Trim()

$zip = Join-Path $root "dist\GTACoop-Remake-WPF-$version.zip"

Write-Host "== GTACoop Remake launcher paketi ==" -ForegroundColor Cyan
Write-Host "Surum : $version"
Write-Host "Hedef : $out"
Write-Host ""

# --- 1) Yayinla
if (Test-Path $out) { Remove-Item $out -Recurse -Force }

dotnet publish $proj -c Release -o $out --nologo
if ($LASTEXITCODE -ne 0) { throw "WPF launcher yayinlama basarisiz." }

$exe = Join-Path $out 'GTACoop.Launcher.exe'
if (-not (Test-Path $exe)) { throw "Launcher exe bulunamadi: $exe" }
Write-Host "[1/7] Uygulama yayinlandi." -ForegroundColor Green

# --- 2) Dagitim dosyalari (istemci + sunucu + lisans)
robocopy (Join-Path $vendor 'Server') (Join-Path $out 'Distribution\Server') /E /NFL /NDL /NJH /NJS /NP | Out-Null
robocopy (Join-Path $vendor 'Scripts') (Join-Path $out 'Distribution\Scripts') /E /NFL /NDL /NJH /NJS /NP | Out-Null

$components = Join-Path $vendor 'Components'
if (Test-Path $components) {
    robocopy $components (Join-Path $out 'Distribution\Components') /E /NFL /NDL /NJH /NJS /NP | Out-Null
}

Copy-Item (Join-Path $vendor 'LICENSE.md') (Join-Path $out 'Distribution') -Force
$attribution = Join-Path $vendor 'README.md'
if (Test-Path $attribution) {
    Copy-Item $attribution (Join-Path $out 'Distribution\ATIF-README.md') -Force
}
Write-Host "[2/7] Dagitim dosyalari kopyalandi." -ForegroundColor Green

# --- 3) SyncServer (kendi sunucunu barindirma)
$syncOut = Join-Path $out 'Distribution\SyncServer'
dotnet publish (Join-Path $root 'src\GTACoop.SyncServer\GTACoop.SyncServer.csproj') -c Release -o $syncOut --nologo
if ($LASTEXITCODE -ne 0) { throw "SyncServer yayinlama basarisiz." }
Write-Host "[3/7] SyncServer eklendi." -ForegroundColor Green

# --- 4) Oturum izleyicisi (launcher kapansa da mod temizliği için)
$toolOut = Join-Path $out 'tools\SyncTool'
dotnet publish (Join-Path $root 'src\GTACoop.SyncTool\GTACoop.SyncTool.csproj') -c Release -o $toolOut --nologo
if ($LASTEXITCODE -ne 0) { throw "SyncTool yayinlama basarisiz." }
Write-Host "[4/7] SyncTool eklendi." -ForegroundColor Green

# --- 5) Başsız sunucu modu aracı
$hostOut = Join-Path $out 'tools\ServerHost'
dotnet publish (Join-Path $root 'src\GTACoop.ServerHost\GTACoop.ServerHost.csproj') -c Release -o $hostOut --nologo
if ($LASTEXITCODE -ne 0) { throw "ServerHost yayinlama basarisiz." }
Write-Host "[5/7] ServerHost eklendi." -ForegroundColor Green

# --- 6) Arkadaş dokümanları

foreach ($file in 'ARKADAS-OKUBENI.txt', 'KODLA-KATIL.txt', 'READ-ME-EN.txt', 'JOIN-WITH-CODE-EN.txt') {
    $path = Join-Path $root $file
    if (Test-Path $path) { Copy-Item $path (Join-Path $out $file) -Force }
}

New-Item -ItemType Directory -Force (Join-Path $out 'Servers') | Out-Null
Write-Host "[6/7] Ek dosyalar hazir." -ForegroundColor Green

# --- 7) ZIP
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path $out -DestinationPath $zip -CompressionLevel Optimal

$mb = [math]::Round((Get-Item $zip).Length / 1MB, 1)
$hash = (Get-FileHash $zip -Algorithm SHA256).Hash
Write-Host "[7/7] ZIP olusturuldu." -ForegroundColor Green

Write-Host ""
Write-Host "TAMAM! launcher paketi hazir:" -ForegroundColor Green
Write-Host "  $zip  ($mb MB)"
Write-Host "  SHA256: $hash"
Write-Host ""
Write-Host "Kurulum icin:  powershell -ExecutionPolicy Bypass -File wpf-kur.ps1" -ForegroundColor Yellow
