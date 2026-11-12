namespace GTACoop.Remake;

/// <summary>
/// Yeni özelliklerin İngilizce karşılıkları. Ayrı sözlükte tutulur; böylece
/// ana sözlükte yinelenen anahtar riski olmadan ekleme yapılabilir.
/// </summary>
public static partial class Loc
{
    private static readonly Dictionary<string, string> ExtraEn = new()
    {
        // --- tek oyunculu mod yöneticisi
        ["Tek Oyunculu"] = "Single-Player",
        ["Tek oyunculu modlarınızı oyun dosyalarını bozmadan yönetin. Modlar bu klasörde durur; etkinleştirdiğinizde oyun klasörüne kopyalanır. Devre dışı bıraktığınızda yalnızca eklenen dosyalar kaldırılır, üzerine yazılan dosyalar yedekten geri gelir. Oyunun .rpf arşivleri değiştirilmez."] =
            "Manage single-player mods without touching the game's own files. Mods live in this folder; enabling one copies it into the game folder. Disabling removes only the files we added and restores anything that was overwritten from backup. The game's .rpf archives are never modified.",
        ["Mod klasörü: {0}"] = "Mods folder: {0}",
        ["Paket"] = "Pack",
        ["Dosya"] = "Files",
        ["Durum"] = "Status",
        ["Etkin"] = "Enabled",
        ["Kapalı"] = "Off",
        ["Hazır."] = "Ready.",
        ["Paket Ekle..."] = "Add Pack...",
        ["Etkinleştir"] = "Enable",
        ["Devre Dışı Bırak"] = "Disable",
        ["Mod Klasörünü Aç"] = "Open Mods Folder",
        ["Yedekleri Aç"] = "Open Backups",
        ["Önce bir paket seçin."] = "Select a pack first.",
        ["GTA V klasörü ayarlı değil. Önce Oyun sekmesinden klasörü seçin."] =
            "The GTA V folder is not set. Choose it on the Game tab first.",
        ["GTA V açık. Modları değiştirmeden önce oyunu kapatın."] =
            "GTA V is running. Close the game before changing mods.",
        ["Paket klasörü bulunamadı: {0}"] = "Pack folder not found: {0}",
        ["Paket etkinleştirildi: {0} ({1} dosya)"] = "Pack enabled: {0} ({1} files)",
        ["({0} dosya yazılamadı)"] = "({0} files could not be written)",
        ["Güvenlik: yalnızca scripts, asi, mods, plugins ve lua klasörlerine yazılır. Atlanan: {0}"] =
            "Safety: only scripts, asi, mods, plugins and lua folders are written. Skipped: {0}",
        ["Paket devre dışı bırakıldı: {0} ({1} dosya kaldırıldı)"] =
            "Pack disabled: {0} ({1} files removed)",
        ["({0} dosya korundu: siz değiştirmişsiniz)"] = "({0} files kept: you modified them)",
        ["Tek oyunculu mod paketini seçin (klasörün içeriği kopyalanır)."] =
            "Choose a single-player mod pack (the folder contents are copied).",
        ["Klasör adı okunamadı."] = "The folder name could not be read.",
        ["Bu isimde bir paket zaten var: {0}"] = "A pack with this name already exists: {0}",
        ["Paket eklendi: {0}"] = "Pack added: {0}",
        ["Paket eklenemedi: {0}"] = "Pack could not be added: {0}",
        ["{0} paket, {1} etkin. Oyun klasörü: {2}"] = "{0} packs, {1} enabled. Game folder: {2}",
        ["seçilmedi"] = "not selected",
        ["Klasör açılamadı: "] = "Could not open the folder: ",

        // --- sunucu saat ayarı
        ["Saat:"] = "Time:",
        ["Kapalı (oyun saati)"] = "Off (game clock)",
        ["Gerçek zamanlı"] = "Real time",
        ["Sabit saat"] = "Fixed time",
        ["saat"] = "hour",
        ["Saat ayarı sunucuya bağlanan herkese uygulanır."] =
            "The time setting applies to everyone on the server.",

        // --- addon ped değişimi
        ["Ped hazırlanıyor: "] = "Preparing ped: ",
        ["Ped değişimi uygulanamadı: "] = "Ped change could not be applied: ",
        [" (araçtan inin veya düşmeyi bekleyin)"] = " (exit the vehicle or wait until you land)",
        ["Addon ped modeli yüklenemedi: "] = "The add-on ped model could not be loaded: ",
        [" (sunucudaki paket eksik olabilir)"] = " (the pack may be missing on the server)",
        ["Seçilen model bir ped değil: "] = "The selected model is not a ped: ",

        // --- hava durumu kilidi
        ["Hava durumu:"] = "Weather:",
        ["Dinamik (varsayılan)"] = "Dynamic (default)",
        ["Açık"] = "Clear",
        ["Bulutlu"] = "Cloudy",
        ["Yağmur"] = "Rain",
        ["Fırtına"] = "Thunder",
        ["Sis"] = "Foggy",
        ["Dumanlı"] = "Smog",
        ["Kar"] = "Snow",
        ["Tipi"] = "Blizzard",

        // --- tek oyunculu paket detayları
        ["Yazar"] = "Author",
        ["Sürüm"] = "Version",
        ["Açıklama"] = "Description",
        ["Paket: {0}"] = "Pack: {0}",
        ["Yazar: {0}"] = "Author: {0}",
        ["Sürüm: {0}"] = "Version: {0}",
        ["Tür: {0}"] = "Category: {0}",
        ["Durum: etkin ({0} dosya)"] = "Status: enabled ({0} files)",
        ["Durum: kapalı"] = "Status: off",
        ["Paket seçin: açıklama ve sürüm bilgisi burada görünür."] =
            "Select a pack to see its description and version.",

        // --- Sunucular / Sunucu Tarayıcı / Oyun / Bileşenler
        ["Ara (ad, adres veya kod)"] = "Search (name, address or code)",
        ["Sırala: Favoriler"] = "Sort: Favourites",
        ["Sırala: Ad"] = "Sort: Name",
        ["Sırala: Ping"] = "Sort: Ping",
        ["Sırala: Son girilenler"] = "Sort: Recently played",
        ["Arayüz Tasarımı"] = "UI Designer",
        ["P2P Paylaşım"] = "P2P Sharing",
        ["Sunucu : {0}"] = "Server : {0}",
        ["Oyun   : doğrudan ({0}:{1})"] = "Game   : direct ({0}:{1})",
        ["Oyun   : otomatik relay (oyun sırasında launcher açık kalmalı)."] =
            "Game   : automatic relay (the launcher must stay open while playing).",
        ["Sync   : doğrudan ({0}:{1})"] = "Sync   : direct ({0}:{1})",
        ["Sync   : otomatik relay ({0})"] = "Sync   : automatic relay ({0})",
        ["Oturum bitirilemedi:\r\n"] = "The session could not be ended:\r\n",
        ["Modlar oyun kapanınca otomatik kaldırılacak (dosyalar bilgisayarınızda kalır).\r\n\r\n"] =
            "Mods will be removed automatically when the game closes (the files stay on your computer).\r\n\r\n",
        ["Bu sunucunun indirilmiş mod dosyaları silinip sunucudan yeniden indirilecek.\r\n"] =
            "This server's downloaded mod files will be deleted and downloaded again.\r\n",
        ["DLC/OIV paketleri kurulamıyor (update.rpf güvenli değil) ({0}): {1}\r\n{2}\r\n\r\n"] =
            "DLC/OIV packs cannot be installed (update.rpf is not safe) ({0}): {1}\r\n{2}\r\n\r\n",
        ["Zaten bilgisayarınızda olduğu için kurulmayacak ({0}): {1}\r\n\r\n"] =
            "Not installed because it is already on your computer ({0}): {1}\r\n\r\n",
        ["'{0}' listeden silinsin mi?\r\n(Kurulu modlar silinmez; 'Araçlar' sekmesinden temizleyebilirsiniz.)"] =
            "Delete '{0}' from the list?\r\n(Installed mods are not deleted; you can clean them up on the 'Tools' tab.)",
        ["'{0}' oturumu bitirilecek ve sunucu modları oyun klasöründen kaldırılacak.\r\nKendi modlarınız ve diğer dosyalar etkilenmez. Devam edilsin mi?"] =
            "The '{0}' session will be ended and the server mods will be removed from the game folder.\r\nYour own mods and other files are not affected. Continue?",
        ["Sunucu: {0}\r\n\r\nKurulacak/güncellenecek: {1} eklenti ({2:F1} MB)\r\nKaldırılacak: {3}\r\n\r\n{4}Devam edilsin mi?"] =
            "Server: {0}\r\n\r\nTo install/update: {1} plugins ({2:F1} MB)\r\nTo remove: {3}\r\n\r\n{4}Continue?",
        ["Sunucu listesine sunucu ekleyin (arkadaşınızın IP'si veya kendi sunucunuz), seçip 'Bağlan ve Oyna'ya basın.\r\nSunucunun tüm modları (araç/ped paketleri, scriptler, ASI eklentileri) otomatik indirilir ve yalnızca siz oynarken kurulu kalır; oyundan çıkınca kaldırılır."] =
            "Add a server to the list (a friend's IP or your own), then select it and press 'Connect && Play'.\r\nAll of the server's mods (vehicle/ped packs, scripts, ASI plugins) are downloaded automatically and stay installed only while you play; they are removed when you exit the game.",
        ["Sunucunun scriptleriyle çakışabilecek yerel dosyalar bulundu:\r\n\r\n{0}\r\n\r\nBunlar oyun çökmesine yol açabilir. Yine de bağlanılsın mı?"] =
            "Local files were found that may conflict with the server's scripts:\r\n\r\n{0}\r\n\r\nThese can cause game crashes. Connect anyway?",
        ["GTA CooP sunucularını master listesinden, LAN'dan veya doğrudan adresten bulun. "] =
            "Find GTA CooP servers on the master list, over LAN or by direct address. ",
        ["Bağlanınca GTACoop Remake mod senkronizasyonu otomatik denenir; "] =
            "On connect, GTACoop Remake tries to sync mods automatically; ",
        ["Sunucu protokolü: {0} ({1}). Güncel istemci: {2}. "] =
            "Server protocol: {0} ({1}). Current client: {2}. ",
        ["Sunucu protokol sürümünü bildirmiyor. Eski bir GTACoOp sunucusu olabilir; "] =
            "The server does not report its protocol version. It may be an older GTACoOp server; ",
        ["Sunucu discovery yanıtı vermedi. Yine de doğrudan bağlanılsın mı?\r\n"] =
            "The server did not answer discovery. Connect directly anyway?\r\n",
        ["{0}\r\n\r\nYine de bağlanılsın mı?"] = "{0}\r\n\r\nConnect anyway?",
        ["Oyuncu adı kaydedildi: {0}\r\nSunucuya 'Bağlan ve Oyna' dediğinizde bu ad kullanılır."] =
            "Player name saved: {0}\r\nThis name is used when you press 'Connect && Play'.",
        ["Seçilen çalıştırma dosyası bulunamadı:\r\n{0}"] = "The selected executable was not found:\r\n{0}",
        ["Oyun başlatılamadı:\n"] = "The game could not be started:\n",
        ["İstemci kurulamadı:\n"] = "The client could not be installed:\n",
        ["İstemci kurulumu tamam.\r\n\r\nKopyalanan: {0}\r\nAtlanan: {1}"] =
            "Client installation complete.\r\n\r\nCopied: {0}\r\nSkipped: {1}",
        ["Enhanced v3"] = "Enhanced v3",
        ["Bileşen kurulamadı:\r\n"] = "The component could not be installed:\r\n",
        ["Bu bileşen zaten kurulu ve dosyaları sağlam.\r\nİndirme yapılmadı."] =
            "This component is already installed and its files are intact.\r\nNothing was downloaded.",
        ["Geri yükleme başarısız:\r\n"] = "Restore failed:\r\n",
        ["Sürüm listesi alınamadı:\r\n"] = "The version list could not be fetched:\r\n",
        ["Klasör açılamadı:\r\n"] = "The folder could not be opened:\r\n",
        ["Yedekten geri yükleme tamamlandı: {0} dosya.\r\n{1}"] =
            "Restored from backup: {0} files.\r\n{1}",

        // --- WPF kabuğu (pano, kartlar, durum satırı)
        ["GTA V multiplayer companion"] = "GTA V multiplayer companion",
        ["Dil / Language"] = "Language",
        ["Unofficial community project"] = "Unofficial community project",
        ["Genel Bakış"] = "Overview",
        ["Oturumunu ve araçlarını tek yerden yönet."] = "Manage your session and tools in one place.",
        ["Birlikte oynayın. Dünyayı paylaşın."] = "Play together. Share the world.",
        ["GTA V co-op oturumlarını, sunucularını ve modlarını GTACoop Remake üzerinden yönet."] =
            "Manage your GTA V co-op sessions, servers and mods from GTACoop Remake.",
        ["Oyuna Git"] = "Go to Game",
        ["Sunucularım"] = "My Servers",
        ["Sunucu Bul"] = "Find a Server",
        ["Sunucuya Katıl"] = "Join a Server",
        ["Arkadaşının sunucusuna bağlan veya kayıtlı sunucularını yönet."] =
            "Join a friend's server or manage your saved servers.",
        ["Sunucuları Aç"] = "Open Servers",
        ["Kendi Sunucun"] = "Your Own Server",
        ["Kendi co-op oturumunu oluştur, ayarlarını düzenle ve arkadaşlarını davet et."] =
            "Create your own co-op session, edit its settings and invite friends.",
        ["Sunucumu Yönet"] = "Manage My Server",
        ["Modlar ve İçerik"] = "Mods and Content",
        ["Addon ped paketlerini ve sunucu içeriklerini yönet."] =
            "Manage add-on ped packs and server content.",
        ["HUD Tasarımcısı"] = "HUD Designer",
        ["Oyun içi bildirim ve arayüz düzenini kişiselleştir."] =
            "Customise in-game notifications and layout.",
        ["Tasarımı Aç"] = "Open Designer",
        ["İpucu: Sunucuya katılmadan önce oyun klasörünün ve modlarının doğru algılandığını Oyun sekmesinden kontrol et."] =
            "Tip: before joining, check on the Game tab that your game folder and mods are detected correctly.",
        ["{0} — hazır."] = "{0} — ready.",
        ["GTACoop Remake · {0}"] = "GTACoop Remake · {0}",

        // --- mod çakışma denetimi
        ["Sunucunun scriptleriyle çakışabilecek yerel dosyalar bulundu:\r\n\r\n{0}\r\n\r\nBunlar oyun çökmesine yol açabilir. Yine de bağlanılsın mı?"] =
            "Local files were found that may conflict with the server's scripts:\r\n\r\n{0}\r\n\r\nThese can cause game crashes. Connect anyway?"
    };
}
