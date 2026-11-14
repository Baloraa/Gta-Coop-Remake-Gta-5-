# AddonPed+ dagitim paketi
# Kullanim: powershell -ExecutionPolicy Bypass -File tools\pack-addonpedplus.ps1 [-Version 1.0.0] [-PackDir <ped klasoru>] [-SelfContained]
#
# Zip icerigi:
#   AddonPed+.exe            Masaustu araci
#   payload\AddonPedPlus.dll Oyun ici ped secici scripti
#   AddonPedPlus.ini         Menu tusu ayari
#   OKUBENI.txt              Kurulum/kaldirma belgesi
#   AddonPedPlus.oiv         OpenIV paketi (ped klasoru varsa)

param(
    [string]$Version = "1.0.0",
    [string]$PackDir = "",
    [switch]$SelfContained
)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$dist = Join-Path $root "dist"
$out = Join-Path $dist "AddonPedPlus"
$oivDir = Join-Path $dist ".addonpedplus-oiv"
$appProject = Join-Path $root "src\GTACoop.PedSelector.App\GTACoop.PedSelector.App.csproj"
$scriptProject = Join-Path $root "src\GTACoOp.PedSelector\GTACoOp.PedSelector.csproj"
$scriptDll = Join-Path $root "src\GTACoOp.PedSelector\bin\Release\net48\AddonPedPlus.dll"

Write-Host "== AddonPed+ paketleme ==" -ForegroundColor Cyan

# 1) Oyun ici script
dotnet build $scriptProject -c Release --nologo
if ($LASTEXITCODE -ne 0) { throw "Script derlenemedi." }
if (-not (Test-Path $scriptDll)) { throw "Script DLL bulunamadi: $scriptDll" }

# 2) Masaustu araci
foreach ($dir in @($out, $oivDir)) {
    if (Test-Path $dir) { Remove-Item $dir -Recurse -Force }
}

$publishArgs = @(
    "-c", "Release",
    "-p:Version=$Version",
    "-p:DebugType=none",
    "-o", $out,
    "--nologo"
)
if ($SelfContained) {
    # Kendi kendine yeterli (buyuk ~160 MB, .NET gerekmez)
    $publishArgs = @("-r", "win-x64", "--self-contained", "true",
                     "-p:PublishSingleFile=true",
                     "-p:IncludeNativeLibrariesForSelfExtract=true") + $publishArgs
} else {
    # Kucuk paket: .NET 8 Desktop Runtime gerekir (~2 MB)
    $publishArgs = @("--no-self-contained",
                     "-p:PublishSingleFile=true") + $publishArgs
}

dotnet publish $appProject @publishArgs
if ($LASTEXITCODE -ne 0) { throw "Uygulama yayinlanamadi." }

# Kullanici adi AddonPed+ olsun; exe tek dosya oldugu icin yeniden adlandirilabilir.
Rename-Item (Join-Path $out "AddonPedPlus.exe") "AddonPed+.exe"

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

# 4) OIV — araci calistirarak uret (ayri klasorde, kilit sorunu olmasin)
if ([string]::IsNullOrWhiteSpace($PackDir)) {
    $PackDir = Join-Path $env:LOCALAPPDATA "GTACoop Ped Selector\peds\paket"
}
$PackDir = (Resolve-Path -LiteralPath $PackDir).Path

$oiv = Join-Path $oivDir "AddonPedPlus.oiv"
New-Item -ItemType Directory -Force $oivDir | Out-Null

$hasPeds = (Test-Path $PackDir) -and
           ((Get-ChildItem $PackDir -Recurse -File -Include *.yft, *.ydd, *.ytd, *.ymt |
             Measure-Object).Count -gt 0)

if ($hasPeds) {
    $proc = Start-Process -FilePath (Join-Path $out "AddonPed+.exe") `
        -ArgumentList @("--build-oiv", ('"' + $PackDir + '"'), ('"' + $oiv + '"')) `
        -Wait -PassThru -NoNewWindow
    if ($proc.ExitCode -ne 0) { throw "OIV olusturulamadi." }
    if (-not (Test-Path $oiv)) { throw "OIV dosyasi olusmadi: $oiv" }
    Copy-Item $oiv (Join-Path $out "AddonPedPlus.oiv") -Force
    Write-Host "OIV eklendi: $oiv"
} else {
    Write-Host "UYARI: ped dosyasi bulunamadi, OIV pakete eklenmedi." -ForegroundColor Yellow
    Write-Host "       'OIV Olustur...' ile uretip zip'e elle ekleyebilirsiniz."
}

# 5) Gereksiz dosyalar
Remove-Item (Join-Path $out "*.pdb") -ErrorAction SilentlyContinue

# 6) Zip — icerik acikca listelenir (oiv ve payload dahil)
$items = @()
$items += (Join-Path $out "AddonPed+.exe")
$items += $payload
$items += (Join-Path $out "AddonPedPlus.ini")
$items += (Join-Path $out "OKUBENI.txt")
if (Test-Path (Join-Path $out "AddonPedPlus.oiv")) {
    $items += (Join-Path $out "AddonPedPlus.oiv")
}

foreach ($item in $items) {
    if (-not (Test-Path $item)) { throw "Paketlenecek dosya yok: $item" }
}

$zip = Join-Path $dist ("AddonPedPlus-" + $Version + ".zip")
if (Test-Path $zip) { Remove-Item $zip -Force }
Start-Sleep -Milliseconds 500
Compress-Archive -Path $items -DestinationPath $zip -CompressionLevel Optimal

$sizeMb = [math]::Round((Get-Item $zip).Length / 1MB, 1)
Write-Host ""
Write-Host ("HAZIR: {0} ({1} MB)" -f $zip, $sizeMb) -ForegroundColor Green
Write-Host "Icerik:"
$items | ForEach-Object { Write-Host ("  " + (Split-Path $_ -Leaf)) }
