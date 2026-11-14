# AddonPed+ dagitim paketi
# Kullanim: powershell -ExecutionPolicy Bypass -File tools\pack-addonpedplus.ps1
#           [-Version 1.0.0] [-PackDir <ped klasoru>]
# Cikti: dist\AddonPedPlus-<surum>.zip
#   AddonPedPlus\AddonPedPlus.exe      (self-contained, .NET gerektirmez)
#   AddonPedPlus\payload\AddonPedPlus.dll
#   AddonPedPlus\AddonPedPlus.ini
#   AddonPedPlus\OKUBENI.txt
#   AddonPedPlus\AddonPedPlus.oiv      (ped klasoru varsa)

param(
    [string]$Version = "1.0.0",
    [string]$PackDir = ""
)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$dist = Join-Path $root "dist"
$out = Join-Path $dist "AddonPedPlus"
$appProject = Join-Path $root "src\GTACoop.PedSelector.App\GTACoop.PedSelector.App.csproj"
$scriptProject = Join-Path $root "src\GTACoOp.PedSelector\GTACoOp.PedSelector.csproj"
$scriptDll = Join-Path $root "src\GTACoOp.PedSelector\bin\Release\net48\AddonPedPlus.dll"

Write-Host "== AddonPed+ paketleme ==" -ForegroundColor Cyan

# 1) Oyun ici script
dotnet build $scriptProject -c Release --nologo
if ($LASTEXITCODE -ne 0) { throw "Script derlenemedi." }
if (-not (Test-Path $scriptDll)) { throw "Script DLL bulunamadi: $scriptDll" }

# 2) Masaustu araci: tek dosya, kendi kendine yeterli
if (Test-Path $out) { Remove-Item $out -Recurse -Force }
dotnet publish $appProject -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:Version=$Version -o $out --nologo
Rename-Item (Join-Path $out "AddonPedPlus.exe") "AddonPed+.exe"
if ($LASTEXITCODE -ne 0) { throw "Uygulama yayinlanamadi." }

$payload = Join-Path $out "payload"
New-Item -ItemType Directory -Force $payload | Out-Null
Copy-Item $scriptDll (Join-Path $payload "AddonPedPlus.dll") -Force

# 3) Ini ve beni oku
$ini = Join-Path $out "AddonPedPlus.ini"
@(
    "; AddonPed+ ayarlari"
    "; MenuKey: menuyu acan tus (F10, F7, M, INSERT...)"
    "[AddonPedPlus]"
    "MenuKey=F10"
) | Set-Content -Path $ini -Encoding UTF8

Copy-Item (Join-Path $root "ADDONPEDPLUS-OKUBENI.txt") (Join-Path $out "OKUBENI.txt") -Force

# Tek exe: AddonPed+.exe (uygulama adi AddonPed+).

# 4) OIV (ped klasoru verildiyse veya varsayilan klasorde ped varsa)
if ([string]::IsNullOrWhiteSpace($PackDir)) {
    $PackDir = Join-Path $env:LOCALAPPDATA "GTACoop Ped Selector\peds\paket"
}

$oiv = Join-Path $out "AddonPedPlus.oiv"
$hasPeds = (Test-Path $PackDir) -and
           ((Get-ChildItem $PackDir -Recurse -File -Include *.yft, *.ydd, *.ytd, *.ymt |
             Measure-Object).Count -gt 0)

if ($hasPeds) {
    $proc = Start-Process -FilePath (Join-Path $out "AddonPed+.exe") `
        -ArgumentList @("--build-oiv", $PackDir, $oiv) -Wait -PassThru -NoNewWindow
    if ($proc.ExitCode -ne 0) { throw "OIV olusturulamadi." }
    Start-Sleep -Milliseconds 800
    Write-Host "OIV eklendi: $oiv"
} else {
    Write-Host "UYARI: ped dosyasi bulunamadi, OIV pakete eklenmedi." -ForegroundColor Yellow
    Write-Host "       Araci acip ped ekleyin, sonra 'OIV Olustur...' ile uretin."
}

# 5) Gereksiz dosyalar
Remove-Item (Join-Path $out "*.pdb") -ErrorAction SilentlyContinue

# 6) Zip
$zip = Join-Path $dist ("AddonPedPlus-" + $Version + ".zip")
if (Test-Path $zip) { Remove-Item $zip -Force }
Start-Sleep -Seconds 2
Compress-Archive -Path (Join-Path $out "*") -DestinationPath $zip -CompressionLevel Optimal
Write-Host ""
Write-Host "HAZIR: $zip" -ForegroundColor Green
Write-Host "Icerik:"
Get-ChildItem $out | ForEach-Object { Write-Host ("  " + $_.Name) }
