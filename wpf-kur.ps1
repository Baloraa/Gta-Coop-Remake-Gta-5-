# =============================================================================
#  GTACoop Remake - WPF Launcher Kur / Güncelle
#  dist\GTACoopRemake-Wpf klasörünü mevcut uygulama klasörüne kopyalar:
#      %LOCALAPPDATA%\Programs\GTACoop Remake
#  Mevcut GTACoop.Launcher.exe adını ve GTACoop Remake kısayolunu korur.
#  Var olan exe'yi ilk kurulumda .pre-wpf.bak olarak yedekler.
#  Kullanım:  powershell -ExecutionPolicy Bypass -File wpf-kur.ps1
# =============================================================================

$ErrorActionPreference = 'Stop'

$root       = Split-Path -Parent $MyInvocation.MyCommand.Path
$src        = Join-Path $root 'dist\GTACoopRemake-Wpf'
$exeName    = 'GTACoop.Launcher.exe'
$installDir = Join-Path $env:LOCALAPPDATA 'Programs\GTACoop Remake'
$target     = Join-Path $installDir $exeName
$shortcut   = 'GTACoop Remake.lnk'

Write-Host "== GTACoop Remake Kurulum ==" -ForegroundColor Cyan
Write-Host "Kaynak : $src"
Write-Host "Hedef  : $installDir"
Write-Host ""

if (-not (Test-Path (Join-Path $src $exeName))) {
    Write-Host "HATA: dist bulunamadi. Once 'wpf-paket.ps1' calistirin." -ForegroundColor Red
    exit 1
}

$proc = Get-Process -Name 'GTACoop.Launcher' -ErrorAction SilentlyContinue
if ($proc) {
    Write-Host "HATA: 'GTACoop.Launcher' su anda calisiyor. Lutfen kapatip tekrar deneyin." -ForegroundColor Red
    exit 1
}

# --- 1) Kopyala (mevcut exe'nin yedegini bir kez al)
New-Item -ItemType Directory -Force -Path $installDir | Out-Null
$existingExe = Join-Path $installDir $exeName
$backupExe = Join-Path $installDir ($exeName + '.pre-wpf.bak')
if ((Test-Path $existingExe) -and -not (Test-Path $backupExe)) { Copy-Item $existingExe $backupExe -Force }
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
$s.Description = 'GTACoop Remake - GTA V mod ve sunucu yoneticisi'
$s.Save()
Write-Host "[2/3] Masaustu kisayolu hazir: $shortcut" -ForegroundColor Green

# --- 3) Baslat menusu kisayolu
$startMenu = Join-Path $env:APPDATA ('Microsoft\Windows\Start Menu\Programs\' + $shortcut)
$sm = $ws.CreateShortcut($startMenu)
$sm.TargetPath = $target
$sm.WorkingDirectory = $installDir
$sm.IconLocation = "$target,0"
$sm.Description = 'GTACoop Remake - GTA V mod ve sunucu yoneticisi'
$sm.Save()
Write-Host "[3/3] Baslat menusu kisayolu hazir." -ForegroundColor Green

Write-Host ""
Write-Host "TAMAM! GTACoop Remake guncellendi:" -ForegroundColor Cyan
Write-Host "  $target"
Write-Host "Masaustu ve Baslat menusundeki 'GTACoop Remake' kisayolundan acabilirsiniz."
Write-Host ""
Write-Host "Not: Ayni exe adi ve ayni masaustu kisayolu kullanilir. Kurulumdan once mevcut exe yedeklenmelidir." -ForegroundColor Yellow
