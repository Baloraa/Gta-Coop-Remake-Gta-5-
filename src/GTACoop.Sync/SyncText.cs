using System.Text.RegularExpressions;

namespace GTACoop.Sync;

/// <summary>
/// Sync katmanındaki günlük/ilerleme/hata metinlerini arayüz diline çevirir.
/// Launcher Türkçe ise metinler olduğu gibi kalır; İngilizce ise bilinen
/// Türkçe kalıplar İngilizceye çevrilir. Dosya yolları ve kullanıcı adları
/// değiştirilmez; yalnızca metin parçaları eşleştirilir.
/// </summary>
public static class SyncText
{
    public static bool English { get; set; }

    public static string T(string tr) =>
        English && En.TryGetValue(tr, out var en) ? en : tr;

    public static string T(string tr, params object?[] args) =>
        string.Format(T(tr), args);

    /// <summary>
    /// Serbest metinli günlük/hata iletilerini çevirir. Tam eşleşme yoksa
    /// bilinen Türkçe ifadeler metin içinde değiştirilir (ör. "Kuruluyor: X").
    /// </summary>
    public static string Translate(string? message)
    {
        if (!English || string.IsNullOrWhiteSpace(message)) return message ?? "";

        if (En.TryGetValue(message, out var exact)) return exact;

        var text = message;

        // Plan özeti dinamik sayılar içerir; önce kalıp olarak çevir.
        text = PlanWithBlockedRegex.Replace(text,
            m => $"Plan: {m.Groups[1].Value} to install, {m.Groups[2].Value} up-to-date, " +
                 $"{m.Groups[3].Value} to remove, {m.Groups[4].Value} on your PC (skipped), " +
                 $"{m.Groups[5].Value} blocked.");
        text = PlanRegex.Replace(text,
            m => $"Plan: {m.Groups[1].Value} to install, {m.Groups[2].Value} up-to-date, " +
                 $"{m.Groups[3].Value} to remove, {m.Groups[4].Value} on your PC (skipped).");
        text = DlclistAlreadyRegex.Replace(text,
            m => $"dlclist: '{m.Groups[1].Value}' already listed; no change.");

        // Uzun ifadeler önce değiştirilsin (kısa önekler uzunları bozmasın).
        foreach (var (tr, en) in Replacements)
            text = text.Replace(tr, en, StringComparison.Ordinal);

        return text;
    }

    private static readonly Regex PlanWithBlockedRegex = new(
        @"^Plan: (\d+) kurulacak, (\d+) güncel, (\d+) kaldırılacak, " +
        @"(\d+) sizde var \(atlanacak\), (\d+) engellendi\.$",
        RegexOptions.Compiled);

    private static readonly Regex PlanRegex = new(
        @"^Plan: (\d+) kurulacak, (\d+) güncel, (\d+) kaldırılacak, " +
        @"(\d+) sizde var \(atlanacak\)\.$",
        RegexOptions.Compiled);

    private static readonly Regex DlclistAlreadyRegex = new(
        @"^dlclist: '(.+)' zaten kayıtlı, değişiklik yok\.$",
        RegexOptions.Compiled);

    // Tam eşleşen iletiler (T() ile de kullanılır).
    private static readonly Dictionary<string, string> En = new()
    {
        ["Senkronizasyon tamamlandı."] = "Synchronization complete.",
        ["Tüm mod dosyaları indirildi (oyuna kurulmadı)."] = "All mod files downloaded (not installed into the game).",
        ["Geri yükleme tamamlandı."] = "Restore complete.",
        ["Yedek alındı."] = "Backup created.",
        ["Yedek yenilendi."] = "Backup refreshed.",
        ["update.rpf onarıldı; işleme devam ediliyor."] = "update.rpf repaired; continuing.",
        ["dosya yok"] = "file not found",
        ["dosya çok küçük / geçersiz"] = "file too small / invalid",
        ["common/data/dlclist.xml bulunamadı"] = "common/data/dlclist.xml not found",
        ["RPF7 yapısı sağlıksız"] = "RPF7 structure is unhealthy",
        ["Girdi bu arşive ait değil."] = "Entry does not belong to this archive.",
        ["Yalnızca binary (RSC olmayan) dosyalar değiştirilebilir."] =
            "Only binary (non-RSC) files can be replaced.",
        ["Şifreli dosya yerinde değiştirilemez."] =
            "Encrypted files cannot be replaced in place.",
        ["mevcut alan yetersiz (yerinde yazma için boşluk yok)."] =
            "not enough space (no room for an in-place write).",
        ["Dizin girdileri değiştirilemez."] = "Directory entries cannot be replaced.",
        ["RPF7: tek girdi için depolanan veri 16 MB sınırını aşıyor."] =
            "RPF7: stored data for a single entry exceeds the 16 MB limit.",
        ["GTA V açıkken dlclist düzenlenemez. Lütfen oyunu kapatın."] = "The dlclist cannot be edited while GTA V is running. Close the game first.",
        ["update.rpf içinde dlclist.xml bulunamadı."] = "dlclist.xml was not found inside update.rpf.",
        ["update.rpf sağlıksız/çok büyük; otomatik geri yükleme yapılmadı. OpenIV ile mods klasörünü onarın veya Launcher > Yedekler bölümünden geri dönün."] = "update.rpf is unhealthy/too large; no automatic restore was performed. Repair the mods folder with OpenIV or restore a backup from Launcher > Backups.",
        ["mods/update/update.rpf bulunamadı. DLC paketleri için OpenIV ile mods klasörü oluşturulmalı."] = "mods/update/update.rpf was not found. Create a mods folder with OpenIV to use DLC packs.",
        ["mods/update/update.rpf bulunamadı. dlclist girdisi çıkarılamıyor."] = "mods/update/update.rpf was not found. The dlclist entry cannot be removed.",
        ["Önceki dosya 'update.rpf.before-restore' olarak korunuyor."] = "The previous file is kept as 'update.rpf.before-restore'.",
        ["Sunucudan geçersiz manifest alındı."] = "The server returned an invalid manifest.",
        ["Sunucu kimlik doğrulaması istiyor (token hatalı olabilir)."] = "The server requires authentication (the token may be wrong).",
        ["Sunucu yanıt vermedi (zaman aşımı). Adres ve bağlantı noktasını kontrol edin."] = "The server did not respond (timeout). Check the address and port.",
        ["Oyun relay bağlantısı kurulamadı."] = "Could not establish the game relay connection.",
        ["Sunucuda relay bilgisi yok."] = "The server has no relay information.",
        ["Relay sunucusu bağlantıyı kapattı."] = "The relay server closed the connection.",
        ["Relay sunucusu beklenmeyen yanıt verdi."] = "The relay server returned an unexpected response.",
        ["Relay bağlantısı kapandı."] = "The relay connection was closed.",
        ["Relay kontrol bağlantısı zaman aşımına uğradı."] = "The relay control connection timed out.",
        ["Relay çerçevesi çok uzun."] = "The relay frame is too long.",
        ["bilinmeyen hata"] = "unknown error",
        ["GTA V klasörü geçerli değil."] = "The GTA V folder is not valid.",
        ["GTA V açıkken bileşen kurulamaz. Önce oyunu kapatın."] = "Components cannot be installed while GTA V is running. Close the game first.",
        ["Bileşen türü ile seçilen sürüm uyuşmuyor."] = "The selected version does not match the component type.",
        ["Bileşen kanalı ile seçilen sürüm uyuşmuyor."] = "The selected version does not match the component channel.",
        ["Kaynakta indirilebilir sürüm bulunamadı."] = "No downloadable version was found at the source.",
        ["Paket içi OpenIV.asi bulunamadı. Launcher paketini yeniden kurun."] = "The bundled OpenIV.asi was not found. Reinstall the launcher package.",
        ["Paket içi OpenIV.asi boyutu geçersiz."] = "The bundled OpenIV.asi size is invalid.",
        ["Paket içi OpenIV.asi doğrulanamadı (hash uyuşmuyor)."] = "The bundled OpenIV.asi could not be verified (hash mismatch).",
        ["Resmî ScriptHookV indirme bağlantısı bulunamadı."] = "The official ScriptHookV download link was not found.",
        ["Bilinmeyen ScriptHookVDotNet kanalı."] = "Unknown ScriptHookVDotNet channel.",
        ["Bu bileşen GitHub'dan gelmiyor."] = "This component does not come from GitHub.",
        ["Bileşen indirme boyutu güvenlik sınırını aşıyor."] = "The component download exceeds the safety limit.",
        ["İndirilen dosya boş."] = "The downloaded file is empty.",
        ["GTA V açıkken geri yükleme yapılamaz."] = "Restore cannot be performed while GTA V is running.",
        ["Bu bileşen için yedek bulunamadı."] = "No backup was found for this component.",
        ["Oyun süreci başlatılamadı."] = "The game process could not be started.",
        ["Oyun çalıştırılabilir dosyası bulunamadı: {0}"] = "Game executable not found: {0}",
        ["İndirme adresi güvenli değil."] = "The download address is not safe.",
        ["İndirilen dosya geçerli bir ZIP arşivi değil."] = "The downloaded file is not a valid ZIP archive.",
        ["ScriptHookVDotNet çekirdek DLL'i arşivde bulunamadı."] = "The ScriptHookVDotNet core DLL was not found in the archive.",
        ["Bilinmeyen bileşen."] = "Unknown component.",
        ["GTA V açık; atlandı."] = "GTA V is running; skipped.",
        ["ENGELLENDİ"] = "BLOCKED",
        ["DLC paketi kurulamadı."] = "The DLC pack could not be installed.",
        ["DLC paketleri kurulamadı; diğer modlarla devam ediliyor."] = "DLC packs could not be installed; continuing with the other mods.",

        // Dinamik formatlı iletiler (T() ile kullanılır).
        ["Plan: {0} kurulacak, {1} güncel, {2} kaldırılacak, {3} sizde var (atlanacak), {4} engellendi."] =
            "Plan: {0} to install, {1} up-to-date, {2} to remove, {3} on your PC (skipped), {4} blocked.",
        ["Atlandı (bilgisayarınızda zaten var): {0} -> {1}"] =
            "Skipped (already on your PC): {0} -> {1}",
        ["Atlandı: {0} (zaten bilgisayarınızda var)"] =
            "Skipped: {0} (already on your PC)",
        ["Atlandı (DLC kurulamıyor): {0} -> {1}"] =
            "Skipped (DLC cannot be installed): {0} -> {1}",
        ["Atlandı: {0} (DLC kurulamıyor)"] =
            "Skipped: {0} (DLC cannot be installed)",
        ["Güncelleniyor: {0} ({1} -> {2})"] = "Updating: {0} ({1} -> {2})",
        ["Kuruluyor: {0}"] = "Installing: {0}",
        ["Kuruldu: {0} ({1})"] = "Installed: {0} ({1})",
        ["UYARI: DLC paketi kurulamadı: {0} ({1})"] =
            "WARNING: DLC pack could not be installed: {0} ({1})",
        ["Atlandı: {0} (DLC kurulamadı)"] = "Skipped: {0} (DLC could not be installed)",
        ["update.rpf düzenlemeye uygun değil: {0}. OpenIV ile mods klasörünü onarın veya Launcher > Yedekler bölümünden geri dönün."] =
            "update.rpf is not safe to edit: {0}. Repair the mods folder with OpenIV or restore a backup from Launcher > Backups.",
        ["update.rpf çok büyük ve dlclist.xml yerinde güncellenemiyor: {0}"] =
            "update.rpf is too large and dlclist.xml cannot be updated in place: {0}",
        ["update.rpf okunamadı: {0}"] = "Could not read update.rpf: {0}",
        ["UYARI: update.rpf düzenlemeye uygun değil: {0}"] =
            "WARNING: update.rpf is not safe to edit: {0}",
        ["UYARI: update.rpf büyük; yalnızca yerinde dlclist düzenlemesi denenecek."] =
            "WARNING: update.rpf is large; only an in-place dlclist edit will be attempted.",
        ["update.rpf çok büyük ve dlclist.xml yerinde güncellenemedi; mevcut dosya korundu."] =
            "update.rpf is too large and dlclist.xml could not be updated in place; the existing file was preserved.",
        ["dlclist: '{0}' zaten kayıtlı, değişiklik yok."] =
            "dlclist: '{0}' is already listed; no change.",
        ["dlclist: '{0}' kayıtlı değil, değişiklik yok."] =
            "dlclist: '{0}' is not listed; no change.",
        ["dlclist: eklendi -> {0}"] = "dlclist: added -> {0}",
        ["dlclist: çıkarıldı -> {0}"] = "dlclist: removed -> {0}",
        ["dlclist: önceki içerik geri yazıldı."] = "dlclist: previous content restored.",
        ["UYARI: dlclist geri alınamadı: {0}"] = "WARNING: dlclist could not be rolled back: {0}",
        ["dlclist: {0} gcs_ girdisi temizlendi."] = "dlclist: {0} gcs_ entries cleaned.",
        ["DLC paketi güncellenemedi: eski klasör taşınamadı ({0})."] =
            "DLC pack could not be updated: old folder could not be moved ({0}).",
        ["DLC paketi güncellenemedi: eski yedek geri getirilemedi ({0})."] =
            "DLC pack could not be updated: the old backup could not be restored ({0}).",
        ["DLC paketi güncellenemedi: yeni klasör yerleştirilemedi ({0})."] =
            "DLC pack could not be updated: new folder could not be placed ({0}).",
        ["UYARI: eski DLC yedeği silinemedi: {0}"] =
            "WARNING: old DLC backup could not be deleted: {0}",
        ["DLC: önceki sürüm geri getirildi."] = "DLC: previous version restored.",
        ["UYARI: önceki DLC sürümü geri getirilemedi: {0}"] =
            "WARNING: previous DLC version could not be restored: {0}",
        ["UYARI: eski DLC yedeği korunuyor: {0}"] =
            "WARNING: old DLC backup is being preserved: {0}",
        ["UYARI: dlclist girdisi kaldırılamadı; DLC klasörü korundu: {0}"] =
            "WARNING: dlclist entry could not be removed; DLC folder was preserved: {0}",
        ["UYARI: dlclist girdisi çıkarılamadı ({0}): {1}"] =
            "WARNING: dlclist entry could not be removed ({0}): {1}",
        ["UYARI: dlclist temizlenemedi; gcs_ klasörleri korundu."] =
            "WARNING: dlclist could not be cleaned; gcs_ folders were preserved.",
        ["dlclist temizliği atlandı: {0}"] = "dlclist cleanup skipped: {0}",
        ["Temizleniyor: {0}"] = "Cleaning: {0}",
        ["{0}: oyuna bağlandı (bağlantı ile; kopyalama yapılmadı)."] =
            "{0}: linked into the game (no copy).",
        ["{0}: oyuna kopyalandı (bağlantı kurulamadı)."] =
            "{0}: copied into the game (link could not be created).",
        ["arşiv çok büyük ({0} GB; RPF7 güvenli sınırı 3,9 GB) — OpenIV yeni dosya ekleyemez"] =
            "archive is too large ({0} GB; RPF7 safe limit is 3.9 GB) — OpenIV cannot add new files",
        ["Sağlıksız yedek atlandı: {0} ({1})"] = "Unhealthy backup skipped: {0} ({1})",
        ["UYARI: mods/update/update.rpf bulunamadı; dlclist düzenlemesi yapılamaz."] =
            "WARNING: mods/update/update.rpf was not found; dlclist cannot be edited.",
        ["Mevcut günlük yedek sağlıksız ({0}); sağlıklı içerikle yenileniyor..."] =
            "The current daily backup is unhealthy ({0}); refreshing with healthy content...",
        ["Yedek zaten var: {0}"] = "Backup already exists: {0}",
        ["UYARI: update.rpf sağlıksız ({0}); yedek alınmadı, eski sağlıklı yedek(ler) korunuyor."] =
            "WARNING: update.rpf is unhealthy ({0}); no backup was taken, existing healthy backup(s) are preserved.",
        ["update.rpf yedekleniyor -> {0} ..."] = "Backing up update.rpf -> {0} ...",
        ["Eski yedek silindi: {0}"] = "Old backup deleted: {0}",
        ["Eski DLC önbelleği temizlendi: {0}"] = "Old DLC cache cleaned: {0}",
        ["UYARI: eski DLC önbelleği temizlenemedi: {0}"] =
            "WARNING: old DLC cache could not be cleaned: {0}",
        ["Yedek bulunamadı: {0}"] = "Backup not found: {0}",
        ["Yedek düzenlemeye uygun değil: {0} ({1})"] =
            "Backup is not safe to edit: {0} ({1})",
        ["Geri yükleniyor: {0} -> mods/update/update.rpf"] =
            "Restoring: {0} -> mods/update/update.rpf",
        ["Geri yükleme sonrası update.rpf sağlıksız ({0}). Önceki dosya 'update.rpf.before-restore' olarak korunuyor."] =
            "update.rpf is unhealthy after restore ({0}). The previous file is kept as 'update.rpf.before-restore'.",
        ["dlclist yedeği alındı: {0}"] = "dlclist backup created: {0}",
        ["dlclist: temizlenecek gcs_ girdisi yok."] = "dlclist: no gcs_ entries to clean.",
        ["dlclist: kodlama beyana uygun UTF-16'ya düzeltildi (OpenIV uyumu)."] =
            "dlclist: encoding normalized to UTF-16 (OpenIV compatibility).",
        ["Sync   : otomatik relay ({0})"] = "Sync   : automatic relay ({0})",
        ["Sync   : doğrudan ({0}:{1})"] = "Sync   : direct ({0}:{1})",
        ["Oyun   : otomatik relay (oyun sırasında launcher açık kalmalı)."] =
            "Game   : automatic relay (launcher must stay open while playing).",
        ["Oyun   : doğrudan ({0}:{1})"] = "Game   : direct ({0}:{1})",
        ["Sunucuya ne otomatik relay ne de doğrudan bağlantı kurulamadı. " +
         "Sunucu ve launcher açık mı? İnternet bağlantınızı kontrol edip yeniden deneyin."] =
            "Could not connect to the server via automatic relay or directly. " +
            "Is the server and launcher running? Check your internet connection and try again.",
        ["Sunucunun güncel relay bilgisi alınıyor..."] =
            "Fetching the server's current relay information...",
        ["Otomatik relay bağlantısı kuruluyor..."] =
            "Establishing the automatic relay connection...",
        ["Otomatik relay kurulamadı; doğrudan adres deneniyor..."] =
            "Automatic relay failed; trying the direct address...",
        ["Otomatik relay bağlantısı kurulamadı. Sunucu ve launcher açık mı? " +
         "İnternet bağlantınızı kontrol edip yeniden deneyin."] =
            "Could not establish the automatic relay connection. Is the server and launcher running? " +
            "Check your internet connection and try again.",
        ["Otomatik relay bağlantısı kurulamadı; bu sunucuya doğrudan da ulaşılamıyor. " +
         "Sunucu ve launcher açık mı? Güncel kodu kullanın."] =
            "Could not establish the automatic relay connection, and the server is not reachable directly either. " +
            "Is the server and launcher running? Use the current code.",
        ["Bu sunucuya doğrudan ulaşılamaz: adres özel bir ağda ve otomatik relay bilgisi yok. " +
         "Sunucu sahibinden güncel kısa kodu isteyin."] =
            "This server cannot be reached directly: the address is on a private network and no automatic relay information is available. " +
            "Ask the server owner for the current short code.",
        ["Bu sunucuya doğrudan ulaşılamaz: adres özel bir ağda (ör. hotspot/ICS) ve otomatik relay bilgisi yok.\r\n" +
         "Sunucu sahibinden güncel kısa kodu veya uzun GTC1 kodunu isteyip 'Kod ile Katıl...' ile yeniden ekleyin."] =
            "This server cannot be reached directly: the address is on a private network (e.g. hotspot/ICS) and no automatic relay information is available.\r\n" +
            "Ask the server owner for the current short code or long GTC1 code and add it again with 'Join with Code...'.",
        ["Modsuz bağlantı hazırlanıyor..."] = "Preparing a mod-free connection...",
    };

    // Dinamik iletilerde geçen Türkçe kalıplar. Uzun olanlar önce denenir.
    private static readonly (string Tr, string En)[] Replacements = new (string Tr, string En)[]
    {
        ("Atlandı (bilgisayarınızda zaten var): ", "Skipped (already on your PC): "),
        ("Atlandı (DLC kurulamıyor): ", "Skipped (DLC cannot be installed): "),
        ("Atlandı: ", "Skipped: "),
        ("Kuruluyor: ", "Installing: "),
        ("Kuruldu: ", "Installed: "),
        ("İndiriliyor: ", "Downloading: "),
        ("İndirildi: ", "Downloaded: "),
        ("Kaldırılıyor: ", "Removing: "),
        ("Güncelleniyor: ", "Updating: "),
        ("UYARI: ", "WARNING: "),
        ("HATA: ", "ERROR: "),
        ("Hata: ", "Error: "),
        ("Plan: ", "Plan: "),
        ("Sunucuya bağlanılıyor (relay): ", "Connecting to server (relay): "),
        ("Sunucuya bağlanılıyor: ", "Connecting to server: "),
        ("Sunucu adı: ", "Server name: "),
        ("Sunucu içerik klasörü: ", "Server content folder: "),
        ("Senkronizasyon tamam: ", "Sync complete: "),
        ("Otomatik relay bağlantısı kuruldu: ", "Automatic relay connected: "),
        ("Otomatik relay kurulamadı; doğrudan adres denenecek: ", "Automatic relay failed; trying direct address: "),
        ("Otomatik relay bağlantısı kurulamadı: ", "Could not establish the automatic relay: "),
        ("Otomatik relay başlatılamadı: ", "Could not start the automatic relay: "),
        ("Otomatik relay başlatılıyor (port yönlendirme/Radmin gerekmez)...", "Starting automatic relay (no port forwarding/Radmin needed)..."),
        ("Relay sunucusu: ", "Relay server: "),
        ("Sunucu çevrimiçi: ", "Server online: "),
        ("Sunucu çevrimdışı: ", "Server offline: "),
        ("Sunucu durumu kontrol edilemedi: ", "Could not check server status: "),
        ("UPnP denemesi başlatıldı (oyun UDP ", "UPnP mapping attempt started (game UDP "),
        ("yönlendirici UPnP'yi desteklemiyor veya kapalı", "the router does not support UPnP or it is disabled"),
        ("uygun yerel ağ adresi bulunamadı", "no suitable local network address was found"),
        ("bu Windows sürümünde UPnP desteği yok", "UPnP is not supported on this Windows version"),
        ("UPnP başlatılamadı", "UPnP could not be started"),
        ("UPnP hatası: ", "UPnP error: "),
        ("UPnP portları yönlendirilemedi; relay kullanılacak.", "UPnP ports could not be mapped; relay will be used."),
        ("yönlendirici 15 saniye içinde yanıt vermedi; arka planda denenecek.", "the router did not respond within 15 seconds; it will keep trying in the background."),
        ("zaten yönlendirilmiş (", "already mapped ("),
        ("); yeniden eklenmedi.", "); not added again."),
        ("başka bir hedefe yönlendirilmiş (", "mapped to another target ("),
        ("); dokunulmadı.", "); left untouched."),
        ("eklenemedi (mevcut eşleme korunuyor): ", "could not be added (existing mapping preserved): "),
        (" eşlemesi kaldırıldı.", " mapping removed."),
        (" eşlemesi kaldırılamadı: ", " mapping could not be removed: "),
        ("eşlemeler kaldırılamadı: ", "mappings could not be removed: "),
        ("UPnP eşlemeleri arka planda kaldırılıyor.", "UPnP mappings are being removed in the background."),
        ("UPnP kapatılırken hata: ", "Error while closing UPnP: "),
        ("Otomatik relay kullanılamıyor; yalnızca doğrudan adresler yayınlanacak.", "Automatic relay is unavailable; only direct addresses will be published."),
        ("Otomatik relay kapatılması arka planda sürüyor.", "Automatic relay is still shutting down in the background."),
        ("Otomatik relay kapatılırken hata: ", "Error while closing the automatic relay: "),
        ("Relay kapatılırken hata: ", "Error while closing the relay: "),
        ("İndirme iptal edildi.", "Download cancelled."),
        ("Ön indirme iptal edildi.", "Prefetch cancelled."),
        ("Kısa kod çevrimdışı yayını 5 sn içinde tamamlanamadı (arka planda sürüyor).", "The short-code offline publish did not finish within 5 seconds (continuing in the background)."),
        ("Kısa kod çevrimdışı yayını başarısız: ", "Short-code offline publish failed: "),
        ("Relay oturumu arka planda kapatılıyor.", "Relay session is closing in the background."),
        ("Relay oturumu kapatılırken hata: ", "Error while closing the relay session: "),
        ("Aktif oyun oturumu algılandı; oyun kapanınca sunucu modları kaldırılacak.", "Active game session detected; server mods will be removed when the game closes."),
        ("Oyun klasörü bilinmiyor; oturum temizliği yapılamadı.", "Game folder is unknown; session cleanup could not be performed."),
        ("Oturum izleme başladı (oyun açık).", "Session watch started (game is running)."),
        ("Oturum izleme başladı (oyun bekleniyor).", "Session watch started (waiting for the game)."),
        ("Oyun algılandı; kapanınca sunucu modları kaldırılacak.", "Game detected; server mods will be removed when it closes."),
        ("Oyun kapandı.", "Game closed."),
        ("Oyun başlatılmadı (zaman aşımı); oturum kapatılıyor.", "Game did not start (timeout); closing the session."),
        ("Oturum temizliği başlıyor...", "Starting session cleanup..."),
        ("Oturum bitti: sunucu modları kaldırıldı, oyun uygulama öncesi hâline döndü.", "Session ended: server mods were removed; the game is back to its pre-session state."),
        ("Oturum temizliği tamamlanamadı; bir sonraki launcher açılışında yeniden denenecek.", "Session cleanup could not be completed; it will be retried on the next launcher start."),
        ("Oturum izleyicisi durduruldu (PID ", "Session watcher stopped (PID "),
        ("Oturum izleyicisi başlatıldı (PID ", "Session watcher started (PID "),
        ("Oturum izleyicisi (SyncTool) bulunamadı; izleme launcher içinde yapılacak.", "Session watcher (SyncTool) was not found; monitoring will be done inside the launcher."),
        ("Sunucu eklendi: ", "Server added: "),
        ("Katılım kodu mevcut kaydı güncelledi: ", "Join code updated the existing entry: "),
        ("Katılım kodu ile sunucu eklendi: ", "Server added via join code: "),
        ("Yerel sunucu eklendi (127.0.0.1). 'Sunucum' sekmesinden başlatabilirsiniz.", "Local server added (127.0.0.1). You can start it from the 'My Server' tab."),
        ("Önceki oturum kalıntıları temizleniyor...", "Cleaning leftovers from the previous session..."),
        ("Önceki oturumdan kalan modlar temizleniyor...", "Cleaning mods left from the previous session..."),
        ("Kullanıcı senkronizasyonu iptal etti.", "The user cancelled synchronization."),
        ("Mod etkinleştirildi: ", "Mod enabled: "),
        ("Mod devre dışı bırakıldı: ", "Mod disabled: "),
        ("Modlar kaldırıldı; yedek: ", "Mods removed; backup: "),
        ("Sunucu eklentisi değişikliği sunucu yeniden başlatılınca etkili olur.", "The server plugin change takes effect after the server restarts."),
        ("Oyun başlatılıyor...", "Launching game..."),
        ("Oyun başlatılıyor: ", "Launching game: "),
        ("Çalıştırma dosyası: ", "Launch executable: "),
        ("Çalıştırma dosyası ayarlandı: ", "Launch executable set: "),
        ("Varsayılan çalıştırma dosyası PlayGTAV.exe olarak ayarlandı.", "Default launch executable set to PlayGTAV.exe."),
        ("ClientSettings.xml yedeklendi: ", "ClientSettings.xml backed up: "),
        ("ClientSettings.xml güncellenemedi: ", "Could not update ClientSettings.xml: "),
        ("İstemci ayarları yazıldı: ", "Client settings written: "),
        ("Oyuncu adı istemci ayarlarına yazıldı: ", "Player name written to client settings: "),
        ("Oyuncu adı yazılamadı: ", "Could not write player name: "),
        ("AutoConnect kapatıldı.", "AutoConnect disabled."),
        ("İstemci dosyası kuruldu: ", "Client file installed: "),
        ("İstemci dosyaları güncel; kopyalama gerekmedi.", "Client files are up to date; no copy was needed."),
        ("Senkronizasyon iptal edildi.", "Synchronization cancelled."),
        ("Modlar silinmedi; güvenli klasöre taşındı: ", "Mods were not deleted; moved to a safe folder: "),
        ("Eski karantina klasörü silindi: ", "Old quarantine folder deleted: "),
        ("Eski yedek silindi: ", "Old backup deleted: "),
        ("Yedek zaten var: ", "Backup already exists: "),
        ("update.rpf yedekleniyor -> ", "Backing up update.rpf -> "),
        ("Geri yükleniyor: ", "Restoring: "),
        ("Sağlıksız yedek atlandı: ", "Unhealthy backup skipped: "),
        ("Mevcut günlük yedek sağlıksız (", "The current daily backup is unhealthy ("),
        ("update.rpf düzenlemeye uygun değil: ", "update.rpf is not safe to edit: "),
        ("Otomatik onarım: ", "Automatic repair: "),
        ("dlclist: eklendi -> ", "dlclist: added -> "),
        ("dlclist: çıkarıldı -> ", "dlclist: removed -> "),
        ("dlclist: kodlama beyana uygun UTF-16'ya düzeltildi (OpenIV uyumu).", "dlclist: encoding normalized to UTF-16 (OpenIV compatibility)."),
        ("dlclist: temizlenecek gcs_ girdisi yok.", "dlclist: no gcs_ entries to clean."),
        ("dlclist: ", "dlclist: "),
        ("Temizleniyor: ", "Cleaning: "),
        ("Bağlantı kaldırıldı: ", "Junction removed: "),
        ("Sunucu ayarları kaydedildi: ", "Server settings saved: "),
        ("Sunucu ayarları yazıldı: ", "Server settings written: "),
        ("Oyun sunucusu başlatıldı (PID ", "Game server started (PID "),
        ("Senkronizasyon sunucusu başlatıldı: ", "Sync server started: "),
        ("Oyun sunucusu durduruldu.", "Game server stopped."),
        ("Senkronizasyon sunucusu durduruldu.", "Sync server stopped."),
        ("[Sunucu!] ", "[Server!] "),
        ("[Sunucu] ", "[Server] "),
        ("Sunucu dosyaları hazırlanıyor (şablondan kopyalanıyor)...", "Preparing server files (copying from template)..."),
        ("Sunucu dosyaları hazır.", "Server files ready."),
        ("Sunucu şablonu bulunamadı: ", "Server template not found: "),
        ("Dağıtım klasörünün uygulamayla birlikte geldiğinden emin olun.", "Make sure the distribution folder is included with the app."),
        ("Senkronizasyon sunucusu bulunamadı: ", "Sync server not found: "),
        ("Süreç kapandı.", "Process exited."),
        (" eklenti etkin)", " addon(s) enabled)"),
        ("kalan süreç temizleniyor (PID ", "leftover process cleanup (PID "),
        ("kalan süreç kapatılamadı: ", "leftover process could not be closed: "),
        ("durdurulamadı: ", "could not be stopped: "),
        ("Oyun sunucusu: ", "Game server: "),
        ("Senkronizasyon sunucusu: ", "Sync server: "),
        ("Oyun sunucusu", "Game server"),
        ("Senkronizasyon sunucusu", "Sync server"),
        ("Sunucu adı : ", "Server name : "),
        ("Sunucu kimliği: ", "Server ID: "),
        ("Eklenti klasörü: ", "Addons folder: "),
        ("Dinleniyor : ", "Listening : "),
        ("Kimlik doğrulama: AÇIK (X-Sync-Token)", "Authentication: ON (X-Sync-Token)"),
        ("Kimlik doğrulama: kapalı", "Authentication: OFF"),
        ("Durdurmak için Ctrl+C.", "Press Ctrl+C to stop."),
        ("== GTACoop Senkronizasyon Sunucusu v1.0 ==", "== GTACoop Sync Server v1.0 =="),
        ("GTACoop Senkronizasyon Sunucusu", "GTACoop Sync Server"),
        ("Manifest istendi: ", "Manifest requested: "),
        (" eklenti ", " addon(s) "),
        (" not) <- ", " note(s)) <- "),
        ("İndirme: ", "Download: "),
        (" bayt) <- ", " bytes) <- "),
        ("Dosya bulunamadı: ", "File not found: "),
        ("Dosya diskte yok.", "File is not on disk."),
        ("Kullanım: GTACoop.SyncServer.exe", "Usage: GTACoop.SyncServer.exe"),
        ("--addons <klasör>", "--addons <folder>"),
        ("--name \"Sunucum\"", "--name \"MyServer\""),
        ("--token ŞİFRE", "--token PASSWORD"),
        ("HATA: --addons parametresi gerekli.", "ERROR: --addons parameter is required."),
        ("Sunucu: ", "Server: "),
        ("Kimlik: ", "ID: "),
        ("Dosyalar: ", "Files: "),
        ("<eklenti>/<yol>", "<addon>/<path>"),
        ("Bileşen listesi: ", "Component list: "),
        ("Bileşen kurulumu başladı: ", "Component installation started: "),
        ("Bileşen kurulumu: ", "Component installation: "),
        ("Bileşen geri yükleme: ", "Component restore: "),
        ("Yedek klasörü açılamadı: ", "Could not open the backup folder: "),
        ("Yedekler: ", "Backups: "),
        ("Günlük açılamadı: ", "Could not open the log: "),
        ("Launcher başlatıldı. Veri klasörü: ", "Launcher started. Data folder: "),
        ("GTA V klasörü: ", "GTA V folder: "),
        ("GTA V klasörü ayarlandı: ", "GTA V folder set: "),
        ("Bağlantı açıldı: ", "Link opened: "),
        ("Launcher kapatılıyor.", "Closing launcher."),
        ("Sunucu hata döndürdü: ", "Server returned an error: "),
        ("Sunucuya bağlanılamadı (", "Could not connect to the server ("),
        ("Manifest alınamadı (", "Could not get the manifest ("),
        ("Manifest çözümlenemedi: ", "Could not parse the manifest: "),
        ("Sunucuda dosya bulunamadı: ", "File not found on the server: "),
        ("İndirme hatası (", "Download error ("),
        ("Dosya bozuk indirildi: ", "Corrupt download: "),
        ("Relay tüneli kurulamadı: ", "Could not establish the relay tunnel: "),
        ("Relay sunucusu: ", "Relay server: "),
        ("Kısa kod geçersiz: ", "Invalid short code: "),
        ("Kısa kod servisine ulaşılamadı: ", "Could not reach the short-code service: "),
        ("Kısa kod bulunamadı.", "Short code not found."),
        ("Güvenlik: izin verilmeyen hedef yol: ", "Security: target path not allowed: "),
        ("Güvenlik: izin verilmeyen ASI dosyası: ", "Security: ASI file not allowed: "),
        ("Önbellekte eksik dosya: ", "Missing file in cache: "),
        ("Yedek bulunamadı: ", "Backup not found: "),
        ("Yedek düzenlemeye uygun değil: ", "Backup is not safe to edit: "),
        ("Geri yükleme sonrası update.rpf sağlıksız (", "update.rpf is unhealthy after restore ("),
        ("update.rpf otomatik onarımı başarısız: ", "update.rpf automatic repair failed: "),
        ("dlclist düzenlemesi geçersiz XML üretti; yazma iptal edildi: ", "dlclist edit produced invalid XML; write cancelled: "),
        ("Bu eklenti türü istemciye kurulamaz: ", "This addon type cannot be installed on the client: "),
        ("Resmî ScriptHookV sayfası alınamadı (HTTP ", "Could not fetch the official ScriptHookV page (HTTP "),
        ("Bileşen indirilemedi (HTTP ", "Component could not be downloaded (HTTP "),
        ("Yedekte eksik dosya: ", "Missing file in backup: "),
        ("Geçersiz dosya yolu: ", "Invalid file path: "),
        ("İstemci dosyaları bulunamadı: ", "Client files not found: "),
        ("Oyun çalıştırılabilir dosyası bulunamadı: ", "Game executable not found: "),
        ("Arşivde beklenen dosya bulunamadı: ", "Expected file not found in archive: "),
        ("Arşivdeki dosya çok büyük: ", "File in archive is too large: "),
        ("DLC paketleri kurulamıyor (update.rpf güvenli değil): ", "DLC packs cannot be installed (update.rpf is not safe): "),
        ("DLC paketi kurulamadı: ", "DLC pack could not be installed: "),
        ("update.rpf büyük; yalnızca yerinde dlclist düzenlemesi denenecek.", "update.rpf is large; only an in-place dlclist edit will be attempted."),
        ("update.rpf çok büyük ve dlclist.xml yerinde güncellenemiyor: ", "update.rpf is too large and dlclist.xml cannot be updated in place: "),
        ("update.rpf okunamadı: ", "Could not read update.rpf: "),
        ("dlclist yedeği alındı: ", "dlclist backup created: "),
        ("dlclist yedeği alınamadı: ", "Could not create a dlclist backup: "),
        ("Oyuncu adı kaydedildi: ", "Player name saved: "),
        ("Relay oturumu kapatılıyor.", "Closing relay session."),
        ("dlclist temizliği atlandı: ", "dlclist cleanup skipped: "),
        ("dlclist girdisi çıkarılamadı (", "dlclist entry could not be removed ("),
        ("UYARI: kaldırılamadı: ", "WARNING: could not remove: "),
        ("UYARI: karantinaya taşınamadı, silindi: ", "WARNING: could not move to quarantine; deleted: "),
        ("UYARI: taşınamadı ve silinemedi: ", "WARNING: could not move or delete: "),
        ("UYARI: karantinaya taşınamadı: ", "WARNING: could not move to quarantine: "),
        ("UYARI: klasör silinemedi: ", "WARNING: folder could not be deleted: "),
        ("dosya zaten farklı sürümde mevcuttu, korundu.", "file(s) already existed in a different version and were preserved."),
        (" dosya kuruldu.", " file(s) installed."),
        ("Sunucuya bağlanılıyor...", "Connecting to server..."),
        ("Sunucuya bağlanılıyor (relay): ", "Connecting to server (relay): "),
        ("update.rpf sağlıksız/çok büyük; otomatik geri yükleme yapılmadı. OpenIV ile mods klasörünü onarın veya Launcher > Yedekler bölümünden geri dönün.",
            "update.rpf is unhealthy/too large; no automatic restore was performed. Repair the mods folder with OpenIV or restore a backup from Launcher > Backups."),
        ("arşiv çok büyük (", "archive is too large ("),
        ("arşiv çok büyük", "archive is too large"),
        ("RPF7 güvenli sınırı 3,9 GB) — OpenIV yeni dosya ekleyemez",
            "RPF7 safe limit is 3.9 GB) — OpenIV cannot add new files"),
        ("Bu dosya bir GTA V RPF7 arşivi değil: ", "This is not a GTA V RPF7 archive: "),
        ("RPF7: kök dizin girdisi bulunamadı.", "RPF7: root directory entry not found."),
        ("RPF7: girdi sayısı geçersiz.", "RPF7: entry count is invalid."),
        ("RPF7: isim tablosu boyutu geçersiz.", "RPF7: name table size is invalid."),
        ("RPF7: TOC/isim tablosu dosya boyutunu aşıyor.", "RPF7: TOC/name table exceeds the file size."),
        ("RPF7: bilinmeyen şifreleme türü ", "RPF7: unknown encryption type "),
        ("NG (Gen9) şifreli RPF arşivleri desteklenmiyor. Bu dosya büyük olasılıkla orijinal oyun arşividir; lütfen yalnızca mods klasöründeki kopyalar üzerinde çalışın.",
            "NG (Gen9) encrypted RPF archives are not supported. This file is probably an original game archive; only work on copies in the mods folder."),
        ("RPF7: tek girdi için depolanan veri 16 MB sınırını aşıyor.",
            "RPF7: stored data for a single entry exceeds the 16 MB limit."),
        ("RPF7: arşiv 4 GB veri sınırını aşıyor; ekleme yapılamaz. Lütfen Launcher > Yedekler bölümünden sağlıklı bir yedeğe dönün.",
            "RPF7: the archive exceeds the 4 GB data limit; cannot append. Restore a healthy backup from Launcher > Backups."),
        ("RPF7: yeni içerik mevcut alana sığmıyor; arşiv büyütülmedi (yerinde yazma gerekli).",
            "RPF7: new content does not fit in the existing space; the archive was not enlarged (in-place write required)."),
        ("Doğrulama: ", "Verification: "),
        ("Doğrulama başarısız: ", "Verification failed: "),
        ("içeriği yazdıktan sonra eşleşmiyor.", "content does not match after writing."),
        ("yeni içerik mevcut alana sığmıyor; arşiv büyütülmedi (yerinde yazma gerekli).",
            "new content does not fit in the existing space; the archive was not enlarged (in-place write required)."),
        ("veri konumu arşiv sınırlarının dışında.", "data location is outside the archive bounds."),
        ("veri konumu arşiv sınırlarının dışında; yazma iptal edildi.",
            "data location is outside the archive bounds; write cancelled."),
        ("Kısmi indirme geçersiz; baştan indirilecek: ", "Partial download is invalid; restarting: "),
        ("Sunucu HTTP Range desteklemiyor; tek bağlantıyla indirilecek: ",
            "Server does not support HTTP Range; downloading with a single connection: "),
        ("Range desteği sınanamadı; tek bağlantı kullanılacak: ",
            "Could not test Range support; using a single connection: "),
        ("Parçalı indirme doğrulanamadı (", "Segmented download failed verification ("),
        ("Parçalı indirme: ", "Segmented download: "),
        ("Parça aralığı geçersiz; parça baştan indirilecek: ",
            "Segment range is invalid; restarting segment: "),
        ("Parça yanlış konumdan geldi (", "Segment arrived at the wrong offset ("),
        ("Parça erken kapandı (", "Segment ended early ("),
        ("Parça indirmesi kesildi (", "Segment download interrupted ("),
        ("Dosya parçası indirilemedi: ", "Could not download file segment: "),
        ("Sync   : otomatik relay (", "Sync   : automatic relay ("),
        ("Sync   : doğrudan (", "Sync   : direct ("),
        ("Oyun   : otomatik relay (oyun sırasında launcher açık kalmalı).",
            "Game   : automatic relay (launcher must stay open while playing)."),
        ("Oyun   : doğrudan (", "Game   : direct ("),
        ("Sunucuya ne otomatik relay ne de doğrudan bağlantı kurulamadı. ",
            "Could not connect to the server via automatic relay or directly. "),
        ("Yerel/aynı ağ sunucusu: relay atlanıyor, doğrudan bağlantı", "Local/same-network server: skipping relay, direct connection"),
        ("Yerel/aynı ağ sunucusu: relay atlanıyor", "Local/same-network server: skipping relay"),
        ("Ön indirme: yerel/aynı ağ sunucusu, relay atlanıyor", "Prefetch: local/same-network server, skipping relay"),
        ("Modsuz bağlantı: yerel/aynı ağ sunucusu, relay atlanıyor", "Game-only: local/same-network server, skipping relay"),
        ("Modsuz bağlantı relay üzerinden: ", "Game-only connection via relay: "),
        ("Modsuz bağlantı: relay kurulamadı; doğrudan adres denenecek: ", "Game-only: relay failed; trying direct address: "),
        ("Sync doğrudan: ", "Sync direct: "),
        ("Doğrudan sync erişilebilir: ", "Direct sync reachable: "),
        ("Doğrudan sync yok (", "No direct sync ("),
        ("Ön indirme sync doğrudan: ", "Prefetch sync direct: "),
        ("Ön indirme relay bağlantısı: ", "Prefetch relay connection: "),
        ("Ön indirme: relay kurulamadı; doğrudan adres kullanılacak: ", "Prefetch: relay failed; using direct address: "),
        ("Dosya doğrulanamadı (SHA-256 uyuşmuyor): ", "File verification failed (SHA-256 mismatch): "),
        ("İndirme bozuk geldi; sunucudaki dosyayı ve bağlantınızı kontrol edip yeniden deneyin.",
            "The download was corrupted; check the file on the server and your connection, then try again."),
        ("İndirme erken kapandı (", "Download ended early ("),
        ("kaldığı yerden sürüyor: ", "resuming: "),
        ("İndirilen dosya doğrulanamadı; baştan indirilecek: ", "Downloaded file failed verification; restarting: "),
        (" sn ilerlemedi (", "s without progress ("),
        ("kaldığı yerden yeniden denenecek (", "resuming from where it left off ("),
        ("İndirme kesildi (", "Download interrupted ("),
        ("yeniden denenecek (", "will retry ("),
        ("Dosya indirilemedi: ", "Could not download file: "),
        ("Bağlantı koptu; tekrar bağlandığınızda kaldığı yerden devam eder.",
            "The connection dropped; it will resume where it left off when you reconnect."),
        ("İndirme ", "Download "),
    }
    .OrderByDescending(x => x.Tr.Length)
    .ToArray();
}
