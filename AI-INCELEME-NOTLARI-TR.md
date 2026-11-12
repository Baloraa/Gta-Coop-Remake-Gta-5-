# GTACoop Remake — AI inceleme ve değişiklik notları

Bu dosya, 11 Kasım 2026 tarihinde sağlanan kaynak ZIP üzerinde yapılan ilk güvenlik/iyileştirme turunu açıklar.

## Bu turda yapılan değişiklikler

### 1. Manifest dosya yolu güvenliği
- `AddonPathSafety` eklendi.
- Mutlak yollar, `..` / `.` segmentleri, boş segmentler, Windows ADS (`:`), geçersiz Windows karakterleri, aygıt adları, sondaki nokta/boşluk reddedilir.
- Manifest zarfı ve eklenti kimlikleri manifest indirilirken ve ön indirme/kurulum girişlerinde doğrulanır. Dosya listesinde yinelenen yollar, negatif dosya boyutları, boş/bozuk SHA-256 değerleri reddedilir.
- Yol kontrolleri indirme başlamadan önce ve kurulum sırasında uygulanır.
- Cache ve oyun klasörü hedefleri kök klasör dışına çıkamayacak şekilde tam yol üzerinden doğrulanır.
- OIV/DLC yedek geri yükleme yol kontrolü aynı güvenli yardımcıyı kullanır.

### 2. HTTPS adresinin korunması
- `SyncClient` artık kullanıcı `https://` ile adres girdiyse bunu korur.
- Önceki davranış HTTPS şemasını kaldırıp her durumda HTTP kuruyordu; bu nedenle TLS reverse proxy arkasındaki sunucular şifrelenmemiş HTTP'ye düşebiliyordu.
- Sync port aralığı ve host alanı doğrulanır; path/query içeren URL girdileri reddedilir.
- Düz host/IP girdileri eski davranışla uyumlu olarak HTTP kullanmaya devam eder. Uzak sunucular için TLS/HTTPS reverse proxy önerilir.

### 3. WPF kabuk iyileştirmesi
- WPF sürüm numarası WinForms launcher ile 2.13.0 olarak hizalandı.
- WPF kabuğuna Genel Bakış sayfası ve hızlı gezinme kartları eklendi.
- Var olan WinForms sekmeleri WPF içinde barındırılmaya devam ediyor. Bu bilinçli bir uyumluluk kararıdır: bütün sekmeleri tek seferde yerel WPF'ye çevirmek, kapsamlı UI yeniden yazımı ve Windows üzerinde test gerektirir. Bu ZIP, tüm sekmelerin WPF'ye tamamen taşındığını iddia etmez.

## Bilinen doğrulama sınırları

- Bu çalışma ortamında .NET SDK / MSBuild bulunmadığından derleme çalıştırılamadı.
- GTA V üzerinde gerçek çok oyunculu oturum, relay, araç/NPC senkronizasyonu veya oyun içi test yapılmadı.
- Değişiklikler kaynak kodu seviyesinde uygulandı; DeepSeek 4.1 ile Windows üzerinde `dotnet build` ve `wpf-paket.ps1` doğrulaması yapılmalıdır.
- HTTPS yalnızca adres `https://` olarak girildiğinde kullanılır. Düz host/IP girdilerinde geriye dönük uyumluluk için HTTP korunur; token içeren internet bağlantılarında TLS kullanılması önerilir.

## Windows üzerinde önerilen doğrulama

1. `dotnet build src/GTACoop.Sync/GTACoop.Sync.csproj -c Release`
2. `dotnet build src/GTACoop.Launcher/GTACoop.Launcher.csproj -c Release`
3. `dotnet publish src/GTACoop.Launcher.Wpf/GTACoop.Launcher.Wpf.csproj -c Release -o dist/GTACoopRemake-Wpf`
4. `powershell -ExecutionPolicy Bypass -File wpf-paket.ps1`
5. Yerel test sunucusunda normal addon, iç içe klasörlü addon, boş dosya ve büyük dosya indirmelerini dene.
6. Test manifestlerinde `../outside.txt`, `C:/outside.txt`, `/outside.txt`, `folder/../../outside.txt`, `CON.txt`, `file.txt:stream` gibi yolların indirme başlamadan reddedildiğini doğrula.
7. `https://` reverse proxy adresinin Ping/Manifest/Download isteklerinde HTTPS olarak kaldığını doğrula.
8. Mevcut WinForms launcher ile normal sunucuya katılma, mod senkronizasyonu, OIV/DLC kurma-kaldırma ve oturum temizleme akışlarını regresyon testinden geçir.

## DeepSeek 4.1 için talimat

Bu ZIP'i açıp önce `AI-INCELEME-NOTLARI-TR.md` dosyasını oku. Değişiklikleri körlemesine yeniden yazma. Önce Windows ortamında derle. Hata varsa yalnızca ilgili hatayı düzelt; çalışan mod/sunucu/relay/OIV/RPF özelliklerini kaldırma veya davranışını değiştirme. Özellikle `AddonPathSafety` kontrollerini gevşetme. Değişiklik sonrası aynı build ve regresyon adımlarını çalıştır, hata çıktısını ve değiştirdiğin dosyaları raporla. Derleme başarılı olmadan ZIP'i yayınlanabilir olarak işaretleme.

## İkinci iyileştirme turu — ek güvenlik ve kararlılık

### 4. Sembolik bağlantı / junction koruması
- `AddonPathSafety.GetChildPath` artık hedef yolun mevcut bileşenlerinde `ReparsePoint` denetimi yapar. Cache veya oyun klasörü altında var olan symlink/junction üzerinden kök dışına çıkma girişimi reddedilir.
- `ServerScanner` addon klasörlerini tararken symlink/junction dizinlerine ve dosyalarına girmez. Böylece sunucu addon klasörünün dışındaki dosyalar yanlışlıkla manifestte yayınlanmaz.
- `AddonPeds` ped keşfi, metadata ve RPF taraması da aynı güvenli dosya gezginini kullanır; addon içindeki bağlantı hedefleri ped paketine dahil edilmez.
- Bu denetimler dosya sistemi üzerinde yarış koşullarına (TOCTOU) karşı tek başına mutlak garanti değildir; kritik kurulumlar yine de standart kullanıcı izinleriyle ve güvenilir addon klasörleriyle çalıştırılmalıdır.

### 5. Eklenti cache çakışması
- İndirme cache klasör anahtarı artık eklenti türünü ve eklenti kimliğinin kısa SHA-256 parmak izini içerir. `scripts/abc` ile `files/abc` gibi farklı türlerde aynı ada sahip paketlerin aynı cache klasörünü kullanması engellenir.
- Eski cache isimleriyle kurulu DLC/OIV güncellemeleri için geri alma ve eski cache temizleme yolları geriye dönük uyumlu bırakıldı. Eski, artık kullanılmayan genel cache klasörleri otomatik silinmez.
- DLC paketlerinde normalize edilmiş mount klasör adı çakışıyorsa ikinci paket manifestten atlanır ve uyarı yazılır.

### 6. SyncServer kimlik doğrulaması ve bağlantı geri bildirimi
- `X-Sync-Token` karşılaştırması sabit zamanlı karşılaştırmaya taşındı.
- Token tanımlanmadan sunucu başlatılırsa konsolda erişim uyarısı gösterilir. Token kullanıldığında da tokenın trafiği şifrelemediği ve internet erişiminde HTTPS/TLS reverse proxy gerektiği açıkça belirtilir.
- `SyncClient.PingAsync` HTTP yanıtını ve JSON belgesini dispose eder; kullanıcı iptali artık yanlışlıkla zaman aşımı mesajına çevrilmez.

### 7. Otomatik smoke testleri
- `tests/GTACoop.Sync.SmokeTests` eklendi; harici test paketi/NuGet bağımlılığı yoktur.
- Yol traversal, mutlak yol, Windows aygıt adı, kontrol karakterleri/uzunluk sınırları, addon kimliği ve enum doğrulaması, manifest hash/boyut doğrulaması, yinelenen yollar, cache symlink koruması, server/ped tarama symlink koruması ve DLC mount çakışması için kontroller içerir.
- Proje Visual Studio çözümüne eklendi. Komut: `dotnet run --project tests/GTACoop.Sync.SmokeTests/GTACoop.Sync.SmokeTests.csproj -c Release`.

## Bu ZIP'in doğrulama durumu
- Bu ortamda `dotnet`, MSBuild veya C# derleyicisi bulunmadığı için bu turda derleme/test çalıştırılamadı. XML proje/XAML dosyaları ve solution içindeki proje yolları statik olarak doğrulandı.
- Dolayısıyla bu paket kaynak kodu iyileştirme adayıdır; henüz yayınlanmaya hazır/derlenmiş sürüm olarak değerlendirilmemelidir.
- Windows'ta ilk adım `dotnet build GTACoopRemake.sln -c Release` ve ardından yukarıdaki smoke test komutudur. Hatalar düzeltilmeden dağıtım ZIP'i olarak yayımlamayın.

### 8. Windows CI
- `.github/workflows/windows-ci.yml` eklendi. GitHub Actions Windows runner üzerinde .NET 8 ile solution restore/build ve güvenlik smoke testlerini push, pull request veya manuel tetiklemede çalıştırır.
- Deneysel WinUI 3 projesi ve .NET 10 hedefleyen sunucu eklentisi mevcut solution/CI kapsamına bilerek eklenmedi; bunların ayrı SDK/Windows App SDK gereksinimleri var.

### 9. Dağıtım derlemesi için mevcut depo bağımlılığı
- `build.ps1` içindeki GTACoOp.Client derlemesi `vendor/Build/ScriptHookVDotNet3.dll` dosyasını zorunlu tutuyor; bu dosya kullanıcı tarafından verilen ZIP'te bulunmuyor.
- Bu nedenle solution build ve smoke test geçse bile tam dağıtım paketi için `GTACoOp.Client` derlemesi ayrıca engellenebilir. Eksik DLL'i internetten rastgele indirme veya başka dosyayla değiştirme; kullanıcıdaki güvenilir ScriptHookVDotNet3 kurulumundan temin edilmeden build.ps1'in tam başarılı olduğu iddia edilmemeli.
- `GTACoop.ServerPlugin` ayrı olarak .NET 10 hedefliyor ve mevcut .NET 8 solution build'ine dahil değil. .NET 10 SDK ile ayrıca derlenmesi gerekir.
