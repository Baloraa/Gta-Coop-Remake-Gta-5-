# GTA V VFS / dosya güvenliği — teknik analiz

## Mevcut davranış

GTACoop'un sunucu modları önce uygulamanın yerel sunucu önbelleğine indirilir. Ardından `AddonApplier` bazı türleri oyun klasörüne yerleştirir:

- `DlcPack`: `mods/update/x64/dlcpacks/...` altına dosya yerleştirir ve `mods/update/update.rpf` içindeki `dlclist.xml` kaydını günceller.
- `OivPackage`: OIV içeriğini `mods` / oyun mod yollarına uygular; gerektiğinde RPF arşivine müdahale eder.
- `ClientScript`: `scripts/` altına kopyalanır.
- `AsiPlugin` ve `ClientFiles`: izin verilen dosyalar oyun köküne kopyalanır.

`SessionService.Finish` oturum bitince sunucuya ait dosyaları kaldırır. Önceki akışta bazı kaldırma yolları doğrudan `File.Delete` / `Directory.Delete` kullanabiliyordu. Bu sürümde normal senkronizasyon kaldırmaları ve oturum sonu temizliği geri alınabilir `removed/` karantinasına taşınır; karantinaya taşıma başarısızsa kaynak dosya/klasör artık son çare olarak silinmez.

`DownloadAddonAsync` içinde manifestte artık bulunmayan dosyaların silinmesi ise GTA klasöründen değil, uygulamanın yerel indirme önbelleğinden yapılır. Sunucudaki kaynak dosyalara istemci tarafından erişilmez ve sunucudan dosya silinmez.

## VFS gerçeği

GTA V, ScriptHookVDotNet / ASI eklentilerinin tüm dosya türlerini harici bir dizinden otomatik okuyabileceği, herkese açık ve genel amaçlı bir .NET VFS API'si sunmaz. OpenIV.asi'nin `mods` klasörü bir overlay/redirect yaklaşımı sağlar; ancak bu, her dosya türünü oyun köküne hiç mount noktası oluşturmadan dışarıdan yükleyebilen genel VFS değildir.

Özellikle `scripts/` DLL'leri, ASI eklentileri ve bazı config dosyaları için oyun/loader'ın beklediği gerçek yollar vardır. Tamamen dosyasız mount için GTA V sürümlerine göre test edilmiş native ASI dosya sistemi hook'u veya bunu destekleyen ayrı bir loader gerekir. Böyle bir hook'u yalnızca C# launcher'a eklemek mümkün değildir; hatalı hook oyun çökmesine veya GTA güncellemelerinde uyumsuzluğa neden olabilir.

## Güvenli uygulama sırası

1. Önce tüm server içeriği `%LOCALAPPDATA%\GTACoop Remake\Cache\<server>` altında tutulmalı.
2. OpenIV.asi `mods` overlay'i yalnızca desteklenen DLC/RPF içerikleri için kullanılmalı; orijinal GTA arşivlerine yazılmamalı. `mods/update/update.rpf` gerekiyorsa kullanıcıya açıkça bildirilip yedeklenmeli.
3. `scripts` / ASI dosyaları için ayrı, geri alınabilir mount yöntemi (dosya sembolik bağlantısı veya test edilmiş loader) seçilmeli; mevcut kullanıcı dosyaları asla üzerine yazılmamalı.
4. Her mount bir `mount-manifest.json` ile sahiplik, kaynak hash'i ve hedef yol bilgisi tutmalı. Temizlik yalnızca bu manifestte GTACoop tarafından oluşturulduğu kanıtlanan mount noktalarını kaldırmalı.
5. Native VFS hook'u geliştirilmeden önce ayrı test GTA kurulumu, oyun sürümü matrisi ve çökme testi gereklidir.

**Bu kaynak paketinde tam native VFS hook'u uygulanmış değildir.** Bilerek sahte bir VFS çözümü eklenmedi; bu, mevcut GTA kurulumuna zarar verme riski taşır.
