# VFS / DLC gerçek oyun test adımları

Bu adımlar, 2.13.6 sürümündeki DLC junction tabanlı mod yönetimini gerçek GTA V
üzerinde sınamak için. Aşağıdakileri ben çalıştırmadım; sen test edeceksin.
Sonuçları "geçti / kaldı" olarak not et.

## 0) Ön koşullar

- GTA V **Legacy** (Steam/Epic/Rockstar) kurulu ve bir kez çalıştırılmış olmalı.
- Launcher'da **Oyun** sekmesinde GTA V klasörü doğru görünmeli (test: "Klasörü
  Kontrol Et" → `GTA5.exe` bulunmalı).
- ScriptHookV + ScriptHookVDotNet kurulu (Bileşenler sekmesi → ScriptHookV +
  ScriptHookVDotNet) — bunlar DLC testi için şart değil, ama script testi için şart.
- OpenIV.asi kurulu olmalı; **mods** klasörü ve `mods/update/update.rpf` mevcut
  olmalı (OpenIV → "Edit mode" ile bir kez oluşturulur).
- Test için **yalnızca kendi bilgisayarında** çalışan bir sunucu kullan
  (Sunucum sekmesi → Sunucuyu Başlat), böylece kimse etkilenmez.

## 1) DLC paketi mount (temel test)

1. `Sunucum` → **DLC Paketi Ekle (araç/ped)...** ile küçük bir dlcpacks klasörü seç
   (ör. `gcs_test_pack` içinde `dlc.rpf`).
2. Mod listesinde satırın **Tür = DLC** ve **Durum = etkin** olduğunu doğrula.
3. `Sunucular` → kendi sunucuna **Bağlan ve Oyna**.
4. Oyun açıldığında şunları kontrol et:
   - `mods/update/x64/dlcpacks/gcs_*` klasörü **junction** olmalı
     (`cmd` → `dir /AL "…\mods\update\x64\dlcpacks"` → `<JUNCTION>` görünür).
   - Junction hedefi launcher cache klasörünü göstermeli, oyun klasörünü değil.
5. Oyunda paketi çağır (araç: trainer ile spawn, ped: model adı) — içerik yükleniyorsa
   mount çalışıyor.

## 2) dlclist kaydı

1. `mods/update/update.rpf` içindeki `common/data/dlclist.xml` dosyasında
   `dlcpacks:/gcs_test_pack/` satırı eklenmiş olmalı (OpenIV ile bak).
2. Oyundan çık, launcher'da modun işaretini kaldır (devre dışı bırak).
3. `dlclist.xml` satırının kaldırıldığını ve junction'ın silindiğini doğrula.
4. `cache` klasöründeki paket dosyalarının **silinmediğini** doğrula (beklenen:
   dosyalar kalır).

## 3) Çökme sonrası geri alma (crash recovery)

1. Mod etkinken oyunu **Görev Yöneticisi'nden zorla kapat** (launcher oyunu
   kapatamadan).
2. Launcher'ı yeniden aç, `Sunucular` → aynı sunucuya bağlan.
3. Beklenen: yarım kalan mount/yedek tespit edilir; ya güvenli yedek geri gelir ya
   da işlem "sahipliği kanıtlanamadı" diyerek durur. **Oyun klasöründe veri
   kaybı olmamalı.**
4. `mods/update/x64/dlcpacks` içinde `__gtacoop*` kalıntısı kalmamalı.

## 4) Yabancı klasöre dokunmama (güvenlik)

1. `mods/update/x64/dlcpacks` içinde elle `gcs_test_pack.gtacoop-staging` adlı bir
   klasör oluştur (içine bir dosya koy).
2. Launcher'da aynı DLC paketini yeniden kur.
3. Beklenen: kurulum **reddedilir**, senin oluşturduğun klasör silinmez/üzerine
   yazılmaz.

## 5) OIV paketi (beklenen davranış)

1. `Sunucum` → **OIV Paketi Ekle (.oiv)...** ile bir paket ekle.
2. Beklenen: paket açılır, desteklenen içerik `mods` katmanına yönlendirilir;
   `update.rpf` **değiştirilmez**. Konsol/günlük "OIV ... mods overlay" mesajı verir.
3. Oyun içinde içeriğin görünmesi OpenIV mods katmanının çalışmasına bağlıdır.

## 6) ASI ve script (fiziksel kurulum — VFS değil)

1. `Sunucum` → **ASI Eklentisi Ekle (.asi)...** ile bir `.asi` ve varsa `.ini`
   dosyalarını ekle.
2. Oyuna bağlan: dosyalar oyun **kök klasörüne kopyalanır** (bağlantı değil).
3. Oyundan çık: dosyalar kaldırılır, varsa üzerine yazılan dosya yedekten döner.
4. `Script Klasörü Ekle...` ile bir `.dll` script klasörü ekle → `scripts/`
   klasörüne kopyalanır, çıkışta kaldırılır.

## 7) Kullanıcı dosyalarına dokunmama

1. `scripts/` içine kendi bir script dosyanı koy.
2. Sunucu modlarını bağla/çıkar.
3. Beklenen: senin dosyan yerinde kalır, değiştirilmez.

## 8) Enhanced (GTA V Enhanced) notu

- Enhanced'da `mods` katmanı ve ScriptHookVDotNet desteği farklıdır. Bu sürümde
  Enhanced için ayrı bir test yapılmadı; Legacy testleri geçmeden Enhanced'ı
  denenmiş sayma. Enhanced testi için: `GTA5_Enhanced.exe` + OpenIV'ın Enhanced
  desteği + uyumlu ASI loader gerekir.

## Sonuç tablosu (doldur)

| Test | Beklenen | Sonuç |
|---|---|---|
| 1 DLC mount | junction + içerik yüklenir | |
| 2 dlclist + cache | kayıt eklenir/silinir, cache kalır | |
| 3 çökme geri alma | veri kaybı yok | |
| 4 yabancı klasör | reddedilir | |
| 5 OIV | mods katmanı, update.rpf değişmez | |
| 6 ASI/script | kopyala/kaldır/yedek | |
| 7 kullanıcı dosyaları | dokunulmaz | |
