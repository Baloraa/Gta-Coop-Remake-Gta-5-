# =============================================================================
#  GTACoop Remake - WPF Launcher Kur / Güncelle
#  dist\GTACoopRemake-Wpf klasörünü şuraya kopyalar:
#      %LOCALAPPDATA%\Programs\GTACoop Remake WPF
#  Masaüstü ve Başlat menüsüne "GTACoop Remake (WPF)" kısayolu ekler.
#  WinForms sürümünün kurulumuna ve kısayollarına dokunmaz.
#  Kullanım:  powershell -ExecutionPolicy Bypass -File wpf-kur.ps1
# =============================================================================

$ErrorActionPreference = 'Stop'

$root       = Split-Path -Parent $MyInvocation.MyCommand.Path
$src        = Join-Path $root 'dist\GTACoopRemake-Wpf'
$exeName    = 'GTACoop.Launcher.Wpf.exe'
$installDir = Join-Path $env:LOCALAPPDATA 'Programs\GTACoop Remake WPF'
$target     = Join-Path $installDir $exeName
$shortcut   = 'GTACoop Remake (WPF).lnk'

Write-Host "== GTACoop Remake WPF Kurulum ==" -ForegroundColor Cyan
Write-Host "Kaynak : $src"
Write-Host "Hedef  : $installDir"
Write-Host ""

if (-not (Test-Path (Join-Path $src $exeName))) {
    Write-Host "HATA: dist bulunamadi. Once 'wpf-paket.ps1' calistirin." -ForegroundColor Red
    exit 1
}

$proc = Get-Process -Name 'GTACoop.Launcher.Wpf' -ErrorAction SilentlyContinue
if ($proc) {
    Write-Host "HATA: 'GTACoop.Launcher.Wpf' su anda calisiyor. Lutfen kapatip tekrar deneyin." -ForegroundColor Red
    exit 1
}

# --- 1) Kopyala (uzerine yaz, hicbir sey silme)
New-Item -ItemType Directory -Force -Path $installDir | Out-Null
Copy-Item (Join-Path $src '*') $installDir -Recurse -Force
Write-Host "[1/3] Uygulama dosyalari kuruldu." -ForegroundColor Green

# --- 2) Masaustu kisayolu
$ws = New-Object -ComObject WScript.Shell
$desktop = [Environment]::GetFolderPath('Desktop')
$lnkPath = Join-Path $desktop $shortcut

$s = $ws.CreateShortcut($lnkPath)
$s.TargetPath = $target
$s.WorkingDirectory = $installDir
$s.IconLocation = "$target,0"
$s.Description = 'GTACoop Remake - GTA V mod ve sunucu yoneticisi (WPF)'
$s.Save()
Write-Host "[2/3] Masaustu kisayolu hazir: $shortcut" -ForegroundColor Green

# --- 3) Baslat menusu kisayolu
$startMenu = Join-Path $env:APPDATA ('Microsoft\Windows\Start Menu\Programs\' + $shortcut)
$sm = $ws.CreateShortcut($startMenu)
$sm.TargetPath = $target
$sm.WorkingDirectory = $installDir
$sm.IconLocation = "$target,0"
$sm.Description = 'GTACoop Remake - GTA V mod ve sunucu yoneticisi (WPF)'
$sm.Save()
Write-Host "[3/3] Baslat menusu kisayolu hazir." -ForegroundColor Green

Write-Host ""
Write-Host "TAMAM! GTACoop Remake (WPF) kuruldu:" -ForegroundColor Cyan
Write-Host "  $target"
Write-Host "Masaustu ve Baslat menusundeki 'GTACoop Remake (WPF)' kisayolundan acabilirsiniz."
Write-Host ""
Write-Host "Not: WinForms surumu ve kendi kisayolu degismedi." -ForegroundColor Yellow
