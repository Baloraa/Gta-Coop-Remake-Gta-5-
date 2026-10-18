# =============================================================================
#  GTACoop Remake - Kur / Güncelle
#  dist\GTACoopRemake klasörünü gerçek uygulama konumuna kopyalar:
#      %LOCALAPPDATA%\Programs\GTACoop Remake
#  Masaüstü ve Başlat menüsü kısayollarını buraya bağlar.
#
#  Not: Hiçbir şey silmez. Eski dist klasörü olduğu yerde kalır.
#  Kullanım:  powershell -ExecutionPolicy Bypass -File kur.ps1
# =============================================================================

$ErrorActionPreference = 'Stop'

$root      = Split-Path -Parent $MyInvocation.MyCommand.Path
$distRoot  = Join-Path $root 'dist\GTACoopRemake'
$exeName   = 'GTACoop.Launcher.exe'
$installDir = Join-Path $env:LOCALAPPDATA 'Programs\GTACoop Remake'
$target    = Join-Path $installDir $exeName

Write-Host "== GTACoop Remake Kurulum ==" -ForegroundColor Cyan
Write-Host "Kaynak : $distRoot"
Write-Host "Hedef  : $installDir"
Write-Host ""

# --- 0) Ön kontrol: kaynak hazır mı? ---
if (-not (Test-Path (Join-Path $distRoot $exeName))) {
    Write-Host "HATA: dist bulunamadi. Once 'build.ps1' calistirin." -ForegroundColor Red
    exit 1
}

# --- 1) Çalışan uygulama kontrolü (dosya kilidi olmasın) ---
foreach ($procName in 'GTACoop.Launcher', 'GTACoop.SyncTool') {
    $proc = Get-Process -Name $procName -ErrorAction SilentlyContinue
    if ($proc) {
        Write-Host "HATA: '$procName' su anda calisiyor. Lutfen kapatip tekrar deneyin." -ForegroundColor Red
        exit 1
    }
}

# --- 2) Kopyala (üzerine yaz, hiçbir şey silme) ---
New-Item -ItemType Directory -Force -Path $installDir | Out-Null
Copy-Item (Join-Path $distRoot '*') $installDir -Recurse -Force
Write-Host "[1/3] Uygulama dosyalari kuruldu." -ForegroundColor Green

# --- 3) Masaüstü kısayolu (varsa güncelle, yoksa oluştur) ---
$ws = New-Object -ComObject WScript.Shell
$desktop = [Environment]::GetFolderPath('Desktop')
$updated = $false

Get-ChildItem $desktop -Filter *.lnk -ErrorAction SilentlyContinue | ForEach-Object {
    try {
        $s = $ws.CreateShortcut($_.FullName)
        if ($s.TargetPath -and $s.TargetPath -match 'GTACoop' -and $s.TargetPath.EndsWith($exeName)) {
            if ($s.TargetPath -ne $target) {
                $s.TargetPath = $target
                $s.WorkingDirectory = $installDir
                $s.IconLocation = "$target,0"
                $s.Save()
                Write-Host "[2/3] Masaustu kisayolu guncellendi: $($_.Name)" -ForegroundColor Green
            } else {
                Write-Host "[2/3] Masaustu kisayolu zaten dogru: $($_.Name)" -ForegroundColor Green
            }
            $updated = $true
        }
    } catch { }
}

if (-not $updated) {
    $lnkPath = Join-Path $desktop 'GTACoop Remake.lnk'
    $s = $ws.CreateShortcut($lnkPath)
    $s.TargetPath = $target
    $s.WorkingDirectory = $installDir
    $s.IconLocation = "$target,0"
    $s.Description = 'GTACoop Remake - GTA V mod ve sunucu yoneticisi'
    $s.Save()
    Write-Host "[2/3] Masaustu kisayolu olusturuldu: GTACoop Remake.lnk" -ForegroundColor Green
}

# --- 4) Başlat menüsü kısayolu ---
$startMenu = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\GTACoop Remake.lnk'
$sm = $ws.CreateShortcut($startMenu)
$sm.TargetPath = $target
$sm.WorkingDirectory = $installDir
$sm.IconLocation = "$target,0"
$sm.Description = 'GTACoop Remake - GTA V mod ve sunucu yoneticisi'
$sm.Save()
Write-Host "[3/3] Baslat menusu kisayolu hazir." -ForegroundColor Green

Write-Host ""
Write-Host "TAMAM! GTACoop Remake artik surada yasiyor:" -ForegroundColor Cyan
Write-Host "  $target"
Write-Host "Masaustu ve Baslat menusundeki 'GTACoop Remake' kisayollarindan acabilirsiniz."
Write-Host ""
Write-Host "Not: Eski dist klasoru silinmedi; istediginizde build.ps1 ile yeniden uretilir."
