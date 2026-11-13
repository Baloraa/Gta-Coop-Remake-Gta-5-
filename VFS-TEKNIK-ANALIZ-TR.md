# GTA V VFS / dosya güvenliği — teknik analiz ve mevcut uygulama

## Bu sürümde gerçekten uygulanan yöntem

GTACoop artık DLC paketleri için dar kapsamlı bir **NTFS junction mount** kullanır:

- Sunucu eklentisi önce GTACoop'un sunucuya özel indirme önbelleğine alınır.
- `DlcPack` için `mods/update/x64/dlcpacks/gcs_<id>` mount noktası, önbellekteki DLC klasörüne junction ile yönlendirilmeyi dener.
- Junction oluşturulursa DLC dosyaları oyun klasörüne tekrar kopyalanmaz. Windows/NTFS junction oluşturma veya doğrulama başarısızsa mevcut uyumluluk davranışı olarak dosyalar mount staging klasörüne kopyalanır.
- Mount hedefi `InstalledAddon.LinkTarget` alanında, mount noktası ise `InstalledAddon.InstalledPaths` içinde senkronizasyon durumuna kaydedilir. Bu kayıt DLC mount manifesti görevi görür.
- Kaldırma sırasında yalnızca kayıtlı hedefle birebir eşleşen junction kaldırılır. Junction hedefindeki önbellek dosyaları silinmez.
- Hedefi eşleşmeyen veya sahipliği doğrulanamayan bağlantı/klasör korunur.
- Güncelleme sırasında yalnızca önceki durum kaydında junction hedefi bulunan ve hâlâ aynı hedefe bağlanan mount değiştirilebilir. Eski kopya kurulumları otomatik olarak üzerine yazılmaz.

Junction işlemi `cmd.exe` / `mklink` komutu çalıştırmak yerine Windows `DeviceIoControl` reparse-point API'siyle yapılır. Böylece kabuk komutu kaçış/karakter sorunları azaltılır ve mount hedefi ayrıca doğrulanır.

## Nelerin VFS olmadığı

Bu **genel amaçlı, oyun süreci içindeki native VFS hook'u değildir**. Yalnızca Windows dosya sistemi seviyesinde DLC klasörü yönlendirmesi yapar. OpenIV.asi'nin `mods` overlay mekanizması ve GTA'nın kendi yükleyicisi bu dosya yapısını tüketir.

Aşağıdaki türler bu junction mount tarafından sanallaştırılmaz:

- `OivPackage`: OIV işlemleri desteklenen `mods` yollarına fiziksel olarak uygulanmaya devam eder.
- `ClientScript`: ScriptHookVDotNet'in beklediği `scripts/` dizinine fiziksel kurulum kullanır.
- `AsiPlugin` ve `ClientFiles`: Loader/oyun tarafından beklenen konumlara fiziksel kurulum kullanır.

Bu türleri sanallaştırmak için GTA V sürümü ve loader kombinasyonlarıyla test edilmiş native ASI dosya sistemi hook'u veya uyumlu ayrı bir loader gerekir. Launcher içinde doğrulanmamış bir hook eklenmedi.

## Güvenlik sınırları

1. Sunucu içeriği önce `%LOCALAPPDATA%\\GTACoop Remake\\Cache\\<server>` altında tutulur.
2. Junction hedefi ayrı cache klasörüdür; mount kaldırılırken hedefe recursive silme uygulanmaz.
3. Sahiplik kaydı ve junction'ın çözümlenen gerçek hedefi eşleşmeden bağlantı kaldırılmaz.
4. Sahipliği doğrulanamayan mevcut DLC klasörünün üzerine yazılmaz.
5. `update.rpf` / `dlclist.xml` düzenlemesi bu mount'tan bağımsızdır; DLC'nin oyunda tanınması için mevcut OpenIV `mods/update/update.rpf` akışı gerekir.
6. Junction davranışı için Windows smoke testi eklendi. Windows dışı ortamlarda test atlanır.

## Bilinen sınırlamalar ve test gereksinimleri

- Junction desteği Windows ve uygun NTFS/izin koşulları gerektirir. Başarısız olursa kopyalama fallback'i devreye girer.
- GTA V'in Legacy/Enhanced sürümlerinde OpenIV.asi / OpenRPF / RageOpenV ile gerçek oyun testi yapılmalıdır.
- Bu kaynak paketi genel amaçlı native VFS hook'u içermez; bu bilinçli bir güvenlik sınırıdır.


## Sonraki aşama: tüm mod türleri için sanal/overlay yönetimi

Hedef kapsam DLC, OIV, ASI ve Script'tir; fakat bu türlerin yüklenme biçimleri farklıdır. OIV çalışma zamanında yüklenen bir dosya değil, kurulum paketidir. ASI ve Script dosyaları kendi loader'larının beklediği konum/keşif kurallarına uyar. Bu nedenle hepsi için tek tip klasör junction'ı yeterli değildir.

DeepSeek devam talimatı `DEEPSEEK-SONRAKI-ADIMLAR-VFS-UI-TR.txt` dosyasındadır. GTA V'in gerçek Rage VFS katmanına uyumlu native hook doğrulanmadan, genel amaçlı VFS tamamlandı şeklinde sunulmamalıdır. Güvenli kısmi overlay, doğrulanmamış oyun içi hook'tan daha iyidir.
