# GTACoop Ped Selector — bağımsız addon ped modu

GTACoop launcher'ından ve sunucusundan **tamamen bağımsız** çalışan ped modu.
Kendi exesi, kendi oyun içi scripti ve OpenIV ile kurulup kaldırılabilen `.oiv`
paketi vardır. Sunucudaki/launcher'daki Addon Peds sistemi bundan etkilenmez;
onlar olduğu gibi kalır.

## Ne var?

| Parça | Dosya | Görev |
| --- | --- | --- |
| Masaüstü aracı | `GTACoop.PedSelector.exe` | Ped ekle, seç, OIV üret, oyuna kur / kaldır |
| Oyun içi script | `GTACoOp.PedSelector.dll` | Oyunda **F10** ile ped seçimi |
| Paket | `GTACoop-AddonPeds.oiv` | OpenIV ile kurulum/kaldırma |
| DLC | `gtacoop_peds/dlc.rpf` | Ped modelleri + ped meta verisi |

## Kurulum (iki yol)

### 1) Aracın içinden

1. `GTACoop.PedSelector.exe` çalıştır.
2. **Ped Ekle...** / **Streamed Klasör Ekle...** ile ped dosyalarını (.yft/.ydd/.ytd/.ymt) ekle.
3. Listede istemediklerinin işaretini kaldır.
4. **Oyuna Kur...** → GTA V klasörünü seç (içinde `GTA5.exe` veya `GTA5_Enhanced.exe` olan).
5. Oyunda **F10** ile ped seç.

Kaldırma: aynı araçta **Kurulumu Kaldır**. Araç yalnızca kendi kurduğu dosyaları
ve kendi dlclist girdisini (`gtacoop_peds`) siler; başka modlara dokunmaz.

### 2) OpenIV ile

1. Araçta **OIV Oluştur...** → `GTACoop-AddonPeds.oiv` kaydet.
2. OpenIV → **Tools → Package Installer** → `.oiv` dosyasını seç → **Install**.
3. Kaldırmak için aynı paketi OpenIV üzerinden **Uninstall** et.

## Gereksinimler

- ScriptHookV (oyun sürümüne uygun) + ScriptHookVDotNet 3 (`scripts/` klasörü)
- `mods/update/update.rpf` (OpenIV ile mods klasörü kurulu olmalı)

## Oyun içi kullanım

- **F10**: menüyü aç/kapat
- **Yukarı/Aşağı**: ped seç
- **Enter**: seçili pedi uygula
- **Backspace / Esc**: normal pede (Michael) dön
- Teşhis günlüğü: `scripts/GTACoop-pedselector.log`

## Ped listesi

`scripts/GTACoop-peds.txt` — araç otomatik yazar. Biçim:

```
model|ad|tür|sağlık|temel model|ragdoll|meta doğrulandı
superman|Superman|human|200||auto|false
```

## Notlar

- Paket tek bir DLC klasörü (`gtacoop_peds`) kullanır; her yeni OIV aynı klasörü
  günceller, dlclist'te tek girdi kalır.
- AddonPeds (ap_m.xml) kurulumu ile birlikte kullanılabilir; bu mod onun
  dosyalarına dokunmaz.
- Launcher'ın kendi Addon Peds sekmesi/sunucu ped dağıtımı ayrı çalışmaya devam eder.
