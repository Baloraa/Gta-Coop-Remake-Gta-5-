param([string]$Version = "2.13.4", [string]$Repo = "Baloraa/Gta-Coop-Remake-Gta-5-")

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$dist = Join-Path $root 'dist\GTACoopRemake'

# Token: git kimlik deposundan alinir, ekrana yazilmaz.
$cred = "protocol=https`nhost=github.com`n`n" | git credential fill
$token = ($cred | Select-String '^password=').ToString().Substring(9).Trim()
if ([string]::IsNullOrWhiteSpace($token)) { throw 'GitHub token bulunamadi.' }
$headers = @{ Authorization = "token $token"; 'User-Agent' = 'GTACoop-Release'; Accept = 'application/vnd.github+json' }

$notes = @"
## GTACoop Remake $Version

### Arayuz
- Arayuz artik WPF kabugu: sol menu, dil secimi (Turkce / English / Русский), menu simgeleri.
- Tum yazilar beyaz, koyu tema tutarli: acilir kutular, pasif dugmeler ve liste satirlari koyu.
- Listede fareyle uzerine gelinen satir siyah olmuyor; secim yesil, uzerine gelme acik gri.
- Liste ve sutun cizgileri koyu tonlarda ciziliyor; sütunlarin sagindaki beyaz serit kapandi.
- Pencere ekranin calisma alanina sigdiriliyor, alt satirlar artik kesilmiyor.
- Grup basliklari ve aciklamalar tam okunuyor; tekrar eden dugmeler kaldirildi yerine tek satir.
- Oyun durumu sekmeye girildiginde kendiliginden yukleniyor.
- Sunucular, Sunucu Tarayici, Oyun ve Bilesenler metinleri Ingilizceye cevrildi.

### Notlar
- Oyun ici akis (GTA V oturumu, mod senkronizasyonu) bu surumde degistirilmedi.
- Tek dosya yayin: guncelleme yalnizca GTACoop.Launcher.exe dosyasini degistirir.
"@

$json = @{ tag_name = "v$Version"; name = "GTACoop Remake $Version"; body = $notes; draft = $false; prerelease = $false } | ConvertTo-Json -Depth 5
# PowerShell 5.1 gövdeyi varsayılan olarak ASCII gönderiyor; Türkçe karakterler
# JSON'u bozuyordu. Açıkça UTF-8 baytları gönderilir.
$body = [System.Text.Encoding]::UTF8.GetBytes($json)
$api = "https://api.github.com/repos/$Repo/releases"

$existing = Invoke-RestMethod -Uri $api -Headers $headers -Method Get |
    Where-Object { $_.tag_name -eq "v$Version" } | Select-Object -First 1

if ($existing) {
    Write-Host "Release zaten var: $($existing.html_url)"
    $release = $existing
} else {
    $release = Invoke-RestMethod -Uri $api -Headers $headers -Method Post -Body $body -ContentType 'application/json; charset=utf-8'
    Write-Host "Release olusturuldu: $($release.html_url)"
}

$assets = @(
    @{ Path = Join-Path $dist 'GTACoop.Launcher.exe'; Name = 'GTACoop.Launcher.exe'; Type = 'application/octet-stream' },
    @{ Path = Join-Path $dist 'Distribution\Scripts\GTACoOp.dll'; Name = 'GTACoOp.dll'; Type = 'application/octet-stream' },
    @{ Path = Join-Path $dist 'Distribution\SyncServer\GTACoop.Sync.dll'; Name = 'GTACoop.Sync.dll'; Type = 'application/octet-stream' },
    @{ Path = Join-Path $root "dist\GTACoop-Remake-Share-$Version.zip"; Name = "GTACoop-Remake-Share-$Version.zip"; Type = 'application/zip' }
)

foreach ($a in $assets) {
    if (-not (Test-Path $a.Path)) { Write-Host "ATLANDI (yok): $($a.Path)"; continue }
    $name = $a.Name
    $upload = "https://uploads.github.com/repos/$Repo/releases/$($release.id)/assets?name=$name"
    $bytes = [System.IO.File]::ReadAllBytes($a.Path)
    try {
        $res = Invoke-RestMethod -Uri $upload -Headers $headers -Method Post -Body $bytes -ContentType $a.Type
        Write-Host "Yuklendi: $($res.name) ($([math]::Round($res.size/1MB,2)) MB)"
    } catch {
        Write-Host "HATA ($name): $($_.Exception.Message)"
    }
}

Write-Host "TAMAM: $($release.html_url)"
