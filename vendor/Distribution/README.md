# GTA Coop Remake

Bu Windows projesi, eklenen GTA Coop kaynak kodunu temel alır. Var olan istemci/sunucu ağ protokolünü korur; launcher, düzenli mod verisi ve yerel sunucu yönetimi ekler.

Eski `Lidgren.Network.MultiTarget` paketi NuGet.org yerine upstream projenin herkese açık GitLab paket deposunda yayımlanıyor. `NuGet.Config`, istemci ve sunucunun aynı ağ kütüphanesini kullanarak derlenebilmesi için bu kaynağı ekler.

## Klasör düzeni

- `upstream/` — GPL-3.0 lisanslı kaynak, mevcut istemci, sunucu ve oyun modları.
- `Launcher/` — Windows launcher kaynağı.
- `Distribution/Scripts/` — derleme sırasında oluşturulan ScriptHookVDotNet istemcisi.
- `Distribution/Server/` — derleme sırasında oluşturulan özel sunucu.
- `GTA V/mods/GTACoop/` — launcher tarafından oluşturulan ayar ve yedek klasörü.
- `GTA V/scripts/` — ScriptHookVDotNet'in yüklediği istemci DLL'si ve bağımlılıkları.

ScriptHookVDotNet, script DLL'lerini GTA V'in `scripts` klasöründen yükler. Bu nedenle çalışan eklenti oraya kurulur; GTA Coop'a ait ayarlar ve yedekler `mods/GTACoop` altında tutulur.

## Derleme (Windows)

1. Windows derleme makinesine .NET 10 SDK'yı kur.
2. Bu klasörde `.\build.ps1` komutunu çalıştır.
3. `Distribution\Launcher\GTACoop.Launcher.exe` dosyasını aç.
4. Launcher'da GTA V klasörünü seç. Sunucu klasörü otomatik bulunmazsa derlenen sunucu klasörünü seç.

İstemci, kaynak modun aynı bağımlılık sürümleriyle derlenir. Launcher ve sunucu küçük Windows x64 paketleri olarak yayımlanır; çalıştırmak için .NET 10 Desktop Runtime gerekir. GTA V, ScriptHook V, ScriptHookVDotNet ve gerekli Microsoft Visual C++ çalışma zamanı ayrıca kurulmalıdır; bu projeyle dağıtılmaz.

## Kullanım

- `GTA5.exe` dosyasının bulunduğu GTA V klasörünü seç.
- ScriptHook V ve ScriptHookVDotNet dosyalarını doğrula.
- Derlenmiş istemciyi kur. Aynı isimli dosyalar değiştirilmeden önce `mods/GTACoop/Backups/` altına yedeklenir.
- Oyuncu adını ve sunucu adresi/portunu girip profili kaydet.
- Oyunu aç ve bağlanmak için GTA Coop oyun içi menüsünü kullan (varsayılan tuş F9).
- İstersen sunucu klasörünü seçip yerel sunucuyu başlat. Launcher `Configuration/serverSettings.xml` ayarını yazar ve sunucuya düzgün kapanma komutu gönderebilir.

İstemci ScriptHookVDotNet v3.6.0 API'sine ve .NET Framework 4.8'e göre derlenir. Launcher gerekli dosya adlarını denetler; dosyaların sürümünü veya mevcut GTA V oyun derlemesiyle uyumunu doğrulayamaz. ScriptHookVDotNet'in `.asi`, `2.dll` ve `3.dll` dosyalarını aynı uyumlu yayından kur.

Launcher, kurulu `GTA5.exe` dosyasını herhangi bir atlatma yöntemi veya çevrimiçi servis bağlantısı eklemeden başlatır. Bu topluluk modunu Story Mode'da kullan; Rockstar GTA Online servisleriyle etkileşime girmez. Yerel sunucunun herkese açık listede duyurulması ve hata raporlarının gönderilmesi varsayılan olarak kapalıdır.

## Lisans ve atıf

Dahil edilen upstream proje GNU GPL v3 lisanslıdır; lisansı ve özgün atıfları `upstream/LICENSE.md` içinde korunur. Launcher ve değiştirilen kod da GPL v3 altında dağıtılır.
