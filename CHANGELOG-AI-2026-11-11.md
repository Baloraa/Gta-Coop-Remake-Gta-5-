# GTACoop Remake — AI geliştirme turu

## Kaynak kod güvenliği
- Manifest dosya yolları canonicalize edilip doğrulanıyor.
- Var olan symlink/junction bileşenleri cache ve oyun klasörüne yazma sırasında reddediliyor.
- Sunucu addon taraması ve AddonPeds keşfi symlink/junction hedeflerini takip etmiyor.
- Manifest, eklenti kimliği ve dosya metaverisi ağdan alındığı anda doğrulanıyor.
- Manifest dosya listesinde duplicate path, negatif boyut ve bozuk SHA-256 reddediliyor.

## Senkronizasyon ve cache
- HTTPS şeması korunuyor.
- Cache anahtarları addon türünü ve kimlik parmak izini içeriyor.
- DLC mount klasör adı çakışması manifest aşamasında tespit ediliyor.
- Ping yanıt kaynakları dispose ediliyor; iptal ile timeout ayrıştırılıyor.

## Sunucu güvenliği
- Sync token karşılaştırması sabit zamanlı.
- Token yoksa veya TLS yapılandırması eksikse konsolda güvenlik uyarısı gösteriliyor.

## Geliştirici deneyimi
- Harici NuGet test framework'ü gerektirmeyen smoke test projesi eklendi.
- WPF launcher ve smoke test projesi solution'a eklendi.
- DeepSeek 4.1 için build/test/regresyon talimatı güncellendi.

## Bilinen sınırlar
- Tam `build.ps1` dağıtım akışı, ZIP'te bulunmayan `vendor/Build/ScriptHookVDotNet3.dll` dosyasına bağlı.
- `GTACoop.ServerPlugin` ayrı .NET 10 projesidir; .NET 8 solution build kapsamına alınmamıştır.
- Bu pakette derleme yapılmadı: çalışma ortamında .NET SDK/MSBuild yok.
- WPF kabuğu henüz tam yerel WPF değildir; mevcut WinForms sekmelerini barındırır.
- GTA V gerçek oyun içi/çok oyunculu regresyon testi yapılmadı.
- GitHub Actions için Windows CI workflow eklendi; .NET 8 solution build ve smoke testleri otomatik çalıştırılır.
