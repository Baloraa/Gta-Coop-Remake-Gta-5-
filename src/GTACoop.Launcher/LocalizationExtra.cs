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
