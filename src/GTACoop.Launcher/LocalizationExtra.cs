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

        // --- Sunucum (mod yönetimi, moderasyon, sunucu modu)
        ["Kurulu modlar (işaretli = etkin):"] = "Installed mods (checked = enabled):",
        ["Seçilenleri Kaldır..."] = "Remove Selected...",
        ["DLC Paketi Ekle (araç/ped)..."] = "Add DLC Pack (vehicle/ped)...",
        ["Addon Peds"] = "Add-on Peds",
        ["İstemci Scripti Ekle (dll + ini/xml/pdb)..."] = "Add Client Script (dll + ini/xml/pdb)...",
        ["Script Klasörü Ekle..."] = "Add Script Folder...",
        ["ASI Eklentisi Ekle (.asi)..."] = "Add ASI Plugin (.asi)...",
        ["Sunucu Eklentisi Ekle (plugin dll)..."] = "Add Server Plugin (plugin dll)...",
        ["Sunucu Kontrolü"] = "Server Control",
        ["Sunucuyu Başlat"] = "Start Server",
        ["Sunucumu Başlat ve Gir"] = "Start My Server and Join",
        ["Durumu Yenile"] = "Refresh Status",
        ["Katılım Kodu Oluştur..."] = "Create Join Code...",
        ["Sunucu Klasörlerini Aç"] = "Open Server Folders",
        ["Sunucu Yedeği Al..."] = "Back Up Server...",
        ["Yedeği Geri Yükle..."] = "Restore Backup...",
        ["Port Kontrolü"] = "Port Check",
        ["Oyun sunucusu: kapalı"] = "Game server: stopped",
        ["Senkronizasyon sunucusu: kapalı"] = "Sync server: stopped",
        ["Oyuncular (canlı)"] = "Players (live)",
        ["Bağlı oyuncu yok."] = "No players connected.",
        ["Bağlı oyuncu: {0} | Son güncelleme: {1:HH:mm:ss}"] = "Connected: {0} | Last update: {1:HH:mm:ss}",
        ["Oyuncu listesi hazırlanıyor..."] = "Preparing player list...",
        ["Sunucu kapalı. Oyuncu listesi sunucu başlatılınca dolar."] = "Server stopped. The player list fills once the server starts.",
        ["At (Kick)"] = "Kick",
        ["Yasakla"] = "Ban",
        ["Yasak / Beyaz Liste..."] = "Bans / Whitelist...",
        ["Mesaj Gönder..."] = "Send Message...",
        ["Oyuncuya mesaj"] = "Message player",
        ["Tüm oyunculara duyuru"] = "Announcement to all players",
        ["Duyuru..."] = "Announcement...",
        ["Sunucudaki herkese gönderilecek mesaj:"] = "Message to send to everyone on the server:",
        ["'{0}' oyuncusuna gönderilecek mesaj:"] = "Message to send to '{0}':",
        ["'{0}' sunucudan atılsın mı?"] = "Kick '{0}' from the server?",
        ["Önce listeden bir oyuncu seçin."] = "Select a player from the list first.",
        ["Bu oyuncu adı sunucudan yasaklandı."] = "This player name is banned from the server.",
        ["Sunucu yöneticisi tarafından atıldınız."] = "You were kicked by the server administrator.",
        ["Oyuncu atılamadı:\r\n"] = "The player could not be kicked:\r\n",
        ["Oyuncu yasaklanamadı:\r\n"] = "The player could not be banned:\r\n",
        ["Mesaj gönderilemedi:\r\n"] = "The message could not be sent:\r\n",
        ["Duyuru gönderilemedi:\r\n"] = "The announcement could not be sent:\r\n",
        ["Ayarlar kaydedilemedi:\r\n"] = "Settings could not be saved:\r\n",
        ["Mod ayarı kaydedilemedi:\r\n"] = "The mod setting could not be saved:\r\n",
        ["Mod kaldırılamadı:\r\n"] = "The mod could not be removed:\r\n",
        ["Klasör eklenemedi:\r\n"] = "The folder could not be added:\r\n",
        ["Paket eklenemedi:\r\n"] = "The pack could not be added:\r\n",
        ["Eklenti eklenemedi:\r\n"] = "The plugin could not be added:\r\n",
        ["Script eklenemedi:\r\n"] = "The script could not be added:\r\n",
        ["OIV paketi eklenemedi:\r\n"] = "The OIV package could not be added:\r\n",
        ["Bazı OIV paketleri eklenemedi:\r\n"] = "Some OIV packages could not be added:\r\n",
        ["ASI eklentisi eklenemedi:\r\n"] = "The ASI plugin could not be added:\r\n",
        ["Sunucu başlatılamadı:\r\n"] = "The server could not be started:\r\n",
        ["Sunucu modu başlatılamadı:\r\n"] = "Server mode could not be started:\r\n",
        ["Sunucu yedeği alınamadı:\r\n"] = "The server backup could not be created:\r\n",
        ["Yedek geri yüklenemedi:\r\n"] = "The backup could not be restored:\r\n",
        ["Sunucu günlüğünü aç"] = "Open Server Log",
        ["Henüz sunucu modu günlüğü yok.\r\n{0}"] = "No server mode log yet.\r\n{0}",
        ["Sunucu bu portlarla başlatılabilir."] = "The server can be started with these ports.",
        ["Oyun portu (UDP {0}): {1}"] = "Game port (UDP {0}): {1}",
        ["Sync portu (TCP {0}): {1}"] = "Sync port (TCP {0}): {1}",
        ["Kendi GTACoOp sunucunuzu burada yönetin. Addons klasörüne eklediğiniz her şey, sunucuya bağlanan oyunculara otomatik iner.\r\nSunucu modları oyuncuların oyununa yalnızca oynarken kurulur. Arkadaşlarınız 'Sunucular' sekmesinden bağlanır."] =
            "Manage your own GTACoOp server here. Anything you add to the Addons folder is downloaded automatically by players who join.\r\nServer mods are installed on players' games only while they play. Your friends join from the 'Servers' tab.",
        ["Sunucu modu, sunucuyu launcher'dan ve oyundan bağımsız çalıştırır: bu pencereyi kapatabilir, oyuna girmeyebilirsin. "] =
            "Server mode runs the server independently of the launcher and the game: you can close this window and do not have to join. ",
        ["Sunucu modu: etkin ama çalışmıyor (başlatmak için 'Sunucu modunu başlat')."] =
            "Server mode: enabled but not running (press 'Start server mode').",
        ["Sunucu modu: kapalı (isteğe bağlı)"] = "Server mode: off (optional)",
        ["Sunucu modunu başlat"] = "Start server mode",
        ["Sunucu modunu kullan (launcher kapalıyken de çalışsın)"] =
            "Use server mode (keeps running while the launcher is closed)",
        ["Sunucu modu zaten çalışıyor."] = "Server mode is already running.",
        ["Açılış görevini kaldır"] = "Remove startup task",
        ["Açılış görevi kaldırılamadı:\r\n"] = "The startup task could not be removed:\r\n",
        ["Windows açılış görevi kaldırıldı."] = "The Windows startup task was removed.",
        ["Windows açılış görevi zaten kurulu değil."] = "The Windows startup task is not installed.",
        ["Windows açılışında otomatik başlat"] = "Start automatically with Windows",
        ["RAM sınırı (MB, 0 = sınırsız):"] = "RAM limit (MB, 0 = unlimited):",
        ["sınırsız"] = "unlimited",
        ["kendi sunucumuz kullanıyor"] = "used by our own server",
        ["Durumu yenile"] = "Refresh status",
        ["DLC paketi eklendi: {0}\r\nSunucu çalışıyorsa bir sonraki manifest isteğinde otomatik dağıtılır."] =
            "DLC pack added: {0}\r\nIf the server is running it is distributed on the next manifest request.",
        ["{0} dosya eklendi: Addons\\scripts\\{1}\r\nOyunculara bir sonraki bağlantıda otomatik indirilir."] =
            "{0} files added: Addons\\scripts\\{1}\r\nThey are downloaded automatically on players' next connection.",
        ["Script klasörü eklendi: {0}\r\nOyunculara bir sonraki bağlantıda otomatik indirilir."] =
            "Script folder added: {0}\r\nIt is downloaded automatically on players' next connection.",
        ["{0} dosya desteklenmeyen türde olduğu için atlandı.\r\n"] =
            "{0} files were skipped because their type is not supported.\r\n",
        ["Seçilen dosyalar desteklenmiyor.\r\nDesteklenen uzantılar: {0}"] =
            "The selected files are not supported.\r\nSupported extensions: {0}",
        ["Eklenti(ler) Plugins klasörüne eklendi.\r\nSunucuyu yeniden başlatınca yüklenecek (ayarlar otomatik güncellenir)."] =
            "Plugin(s) added to the Plugins folder.\r\nThey load when you restart the server (settings update automatically).",
        ["Sunucu yedeği kaydedildi:\r\n{0}\r\n\r\nŞifre ve sync token yedeğe yazılmaz; geri yükledikten sonra yeniden girin."] =
            "Server backup saved:\r\n{0}\r\n\r\nThe password and sync token are not written to the backup; re-enter them after restoring.",
        ["Sunucu yedeği geri yüklendi ({0} dosya).\r\n{1}\r\n\r\nAyarların etkin olması için launcher'ı yeniden başlatın."] =
            "Server backup restored ({0} files).\r\n{1}\r\n\r\nRestart the launcher for the settings to take effect.",
        ["Yedekteki sunucu ayarları ve mod dosyaları mevcut sunucunun üzerine yazılacak.\r\nÖnce mevcut durumun güvenlik yedeği alınır. Devam edilsin mi?"] =
            "The backup's server settings and mod files will overwrite the current server.\r\nA safety backup of the current state is taken first. Continue?",
        ["\r\n{0} paket atlandı."] = "\r\n{0} packs skipped.",
        ["\r\n{0} pakette desteklenmeyen işlemler atlanacak."] =
            "\r\nUnsupported operations will be skipped in {0} packs.",

        // --- HUD Tasarımcısı
        ["+ Bölge"] = "+ Zone", ["+ Daire"] = "+ Circle", ["+ FPS"] = "+ FPS",
        ["+ Gradyan"] = "+ Gradient", ["+ Hız"] = "+ Speed", ["+ Konum"] = "+ Location",
        ["+ Oyuncular"] = "+ Players", ["+ PNG Resim..."] = "+ PNG Image...",
        ["+ Panel"] = "+ Panel", ["+ Ping"] = "+ Ping", ["+ Saat"] = "+ Time",
        ["+ Silah"] = "+ Weapon", ["+ Yazı"] = "+ Text", ["+ Yön"] = "+ Heading",
        ["+ Çerçeve"] = "+ Frame", ["+ Çizgi"] = "+ Line", ["+ Çubuk"] = "+ Bar",
        ["Aranan yıldızlarını gizle"] = "Hide wanted stars",
        ["Araç adı ve sınıfını gizle"] = "Hide vehicle name and class",
        ["Bildirim dosyalarını temizle"] = "Clear notification files",
        ["Bildirim resmi seç"] = "Choose notification image",
        ["Bildirim sesi seç"] = "Choose notification sound",
        ["Bildirimleri göster (resim ve/veya ses)"] = "Show notifications (image and/or sound)",
        ["Bir tuş yazın (ör. F7 veya K)."] = "Press a key (e.g. F7 or K).",
        ["Bu özellik etkin (oyun içinde kendi HUD'um)"] = "This feature is on (my own HUD in game)",
        ["Dışa Aktar (JSON)..."] = "Export (JSON)...",
        ["Dışa aktarılamadı:\r\n"] = "Export failed:\r\n",
        ["İçe Aktar (JSON)..."] = "Import (JSON)...",
        ["İçe aktarılamadı:\r\n"] = "Import failed:\r\n",
        ["Ekle / Güncelle"] = "Add / Update",
        ["Eylem"] = "Action",
        ["Giriş resmi..."] = "Join image...", ["Giriş resmi: "] = "Join image: ",
        ["Giriş sesi..."] = "Join sound...", ["Giriş sesi: "] = "Join sound: ",
        ["Çıkış resmi..."] = "Leave image...", ["Çıkış resmi: "] = "Leave image: ",
        ["Çıkış sesi..."] = "Leave sound...", ["Çıkış sesi: "] = "Leave sound: ",
        ["resim: "] = "image: ", ["sn"] = "s",
        ["Görünme süresi (sn, 0 = süresiz)"] = "Duration (s, 0 = permanent)",
        ["HUD kapatıldı. Oyun içinde oyunun kendi arayüzü geri gelir."] =
            "HUD turned off. The game's own interface returns in game.",
        ["HUD yerleşimi içe aktar"] = "Import HUD layout",
        ["HUD yerleşimini dışa aktar"] = "Export HUD layout",
        ["HUD'a resim ekle"] = "Add image to HUD",
        ["Haritanın resmini gizle (radar çerçevesi kalır)"] = "Hide minimap image (radar frame stays)",
        ["Haritayı/radarı tamamen gizle"] = "Hide minimap/radar completely",
        ["Her zaman görünür (HUD kapalıyken de çiz)"] = "Always visible (draw even when HUD is off)",
        ["Kapalıyken oyunun kendi arayüzü aynen kalır. Bu sayfa oyun ve sunucu dosyalarına hiçbir şey yazmaz; istediğin an kapatabilirsin."] =
            "When off, the game's own interface stays as is. This page writes nothing to game or server files; you can turn it off whenever you like.",
        ["Kapat ve varsayılana dön"] = "Close and reset to default",
        ["Kaydet ve Oyuna Uygula"] = "Save and Apply to Game",
        ["Kısayollar"] = "Shortcuts",
        ["Metin"] = "Text",
        ["Opaklık"] = "Opacity",
        ["Oyun içi ayarlar"] = "In-game settings",
        ["Oyuncu giriş/çıkış bildirimi"] = "Player join/leave notification",
        ["Oyunun kendi HUD'unu gizle (radar dahil)"] = "Hide the game's own HUD (including radar)",
        ["Renk..."] = "Colour...",
        ["Resim eklenemedi:\r\n"] = "The image could not be added:\r\n",
        ["Resim ekranın üst ortasında yumuşak geçişle belirir ve seçtiğin saniye sonunda kaybolur. Ses wav veya mp3 olabilir; seçtiğin dosya ui/assets klasörüne kopyalanır."] =
            "The image fades in at the top centre of the screen and disappears after the seconds you choose. The sound can be wav or mp3; the file you pick is copied into the ui/assets folder.",
        ["Ses eklenemedi:\r\n"] = "The sound could not be added:\r\n",
        ["Seçili öğe"] = "Selected element",
        ["Sokak/cadde adını gizle"] = "Hide street name",
        ["Tuş"] = "Key",
        ["Yardım metnini gizle"] = "Hide help text",
        ["Yazı boyutu"] = "Font size",
        ["Yeni yazı"] = "New text",
        ["Yerleşim dışa aktarıldı:\r\n{0}"] = "Layout exported:\r\n{0}",
        ["Yerleşim içe aktarıldı ve kaydedildi."] = "Layout imported and saved.",
        ["Yerleşim kaydedildi ve oyuna uygulandı:\r\n{0}\r\n\r\nOyun içinde F7 ile HUD'u açıp kapatabilirsin."] =
            "Layout saved and applied to the game:\r\n{0}\r\n\r\nPress F7 in game to toggle the HUD.",
        ["Yerleşim kaydedilemedi:\r\n"] = "The layout could not be saved:\r\n",
        ["Örnek Yerleşim"] = "Example Layout",
        ["Öğe"] = "Element", ["Öğeler"] = "Elements",
        ["Öğeyi tuvalde fareyle sürükleyebilirsin. PNG resimler oyun içinde piksel ızgarası olarak çizilir."] =
            "Drag the element on the canvas with the mouse. PNG images are drawn as a pixel grid in game.",
        ["Klasörü Aç"] = "Open Folder",

        // --- P2P / Modlar
        ["1) Paylaşan kişi bir klasör seçip 'Paylaşımı Başlat'a basar; ekranda çıkan IP:port kodunu arkadaşına gönderir.\r\n"] =
            "1) The sender picks a folder and presses 'Start Sharing'; they send the IP:port code shown to their friend.\r\n",
        ["192.168.1.20:47900"] = "192.168.1.20:47900",
        ["Ağ adresi bulunamadı. Kodu elle girin: 127.0.0.1:{0}"] =
            "Network address not found. Enter the code manually: 127.0.0.1:{0}",
        ["Bağlan ve Listele"] = "Connect and List",
        ["Bağlandı: {0} — {1} dosya, {2} ({3})"] = "Connected: {0} — {1} files, {2} ({3})",
        ["Bağlanılamadı: "] = "Could not connect: ",
        ["Bağlanılıyor: {0}:{1}"] = "Connecting: {0}:{1}",
        ["Boyut"] = "Size",
        ["Dosyalar indirildi ve doğrulandı:\r\n{0}"] = "Files downloaded and verified:\r\n{0}",
        ["Eşe bağlanılamadı:\r\n"] = "Could not connect to the peer:\r\n",
        ["Güvenlik Duvarı Kuralı Ekle"] = "Add Firewall Rule",
        ["Hedef klasör seçip 'İndir'e basın. Aynı dosyalar yeniden indirilmez."] =
            "Choose a target folder and press 'Download'. Files already present are not downloaded again.",
        ["Hedef klasör:"] = "Target folder:",
        ["Henüz bağlanılmadı."] = "Not connected yet.",
        ["Katıl ve indir"] = "Join and download",
        ["Klasör yok: "] = "Folder missing: ",
        ["Klasör zip olarak kaydedildi:\r\n{0}"] = "Folder saved as a zip:\r\n{0}",
        ["Klasör:"] = "Folder:",
        ["Kod geçersiz. Örnek: 192.168.1.20:47900"] = "Invalid code. Example: 192.168.1.20:47900",
        ["Kod:"] = "Code:",
        ["Mod klasörünü zip olarak kaydet"] = "Save the mod folder as a zip",
        ["Nasıl çalışır?"] = "How does it work?",
        ["Paylaş (eşe aç)"] = "Share (open to peer)",
        ["Paylaşılacak klasör bulunamadı."] = "No folder to share was found.",
        ["Paylaşılacak klasör bulunamadı. 'Gözat...' ile seçin."] =
            "No folder to share was found. Pick one with 'Browse...'.",
        ["Paylaşılacak mod klasörünü seçin."] = "Choose the mod folder to share.",
        ["Paylaşım açık — {0} dosya, {1}. Kodu arkadaşınla paylaş."] =
            "Sharing is on — {0} files, {1}. Share the code with your friend.",
        ["Paylaşım başlatılamadı:\r\n"] = "Sharing could not be started:\r\n",
        ["Paylaşım başlatılamadı: "] = "Sharing could not be started: ",
        ["Paylaşım kapalı."] = "Sharing is off.",
        ["Paylaşımı Başlat"] = "Start Sharing",
        ["Paylaşımı başlatınca arkadaşına vereceğin kod burada görünür."] =
            "Once sharing starts, the code you give your friend appears here.",
        ["Port:"] = "Port:",
        ["Seçimi Kaldır"] = "Clear Selection",
        ["Sunucu mod klasörü henüz yok. Önce Sunucum sekmesinde mod ekleyin."] =
            "No server mod folder yet. Add mods on the My Server tab first.",
        ["Sunucum Modları"] = "My Server Mods",
        ["Tümünü Seç"] = "Select All",
        ["ZIP Olarak Dışa Aktar..."] = "Export as ZIP...",
        ["Zip oluşturulamadı:\r\n"] = "The zip could not be created:\r\n",
        ["bilinmeyen eş"] = "unknown peer",
        ["İndir"] = "Download",
        ["İndirilecek dosya seçilmedi."] = "No files were selected to download.",
        ["İndirilen dosyaların kaydedileceği klasörü seçin."] = "Choose the folder the downloads are saved to.",
        ["İndiriliyor: {0} / {1}"] = "Downloading: {0} / {1}",
        ["İndirme hatası: "] = "Download error: ",
        ["İndirme tamamlandı: {0}"] = "Download complete: {0}",
        ["Önce hedef klasörü seçin."] = "Choose the target folder first.",

        // --- Addon Peds
        ["Bazı ped paketleri eklenemedi:\r\n"] = "Some ped packs could not be added:\r\n",
        ["Ped ayarı kaydedilemedi:\r\n"] = "The ped setting could not be saved:\r\n",
        ["Ped dosyaları (*.ydd;*.ytd;*.ymt;*.yft)|*.ydd;*.ytd;*.ymt;*.yft|"] =
            "Ped files (*.ydd;*.ytd;*.ymt;*.yft)|*.ydd;*.ytd;*.ymt;*.yft|",
        ["Ped dosyaları eklendi: {0} dosya. "] = "Ped files added: {0} files. ",
        ["Ped dosyaları eklenemedi:\r\n"] = "The ped files could not be added:\r\n",
        ["Ped görüntülenemedi:\r\n"] = "The ped could not be previewed:\r\n",
        ["Ped güncellenemedi:\r\n"] = "The ped could not be updated:\r\n",
        ["Ped kaldırılamadı:\r\n"] = "The ped could not be removed:\r\n",
        ["Ped klasörü eklenemedi:\r\n"] = "The ped folder could not be added:\r\n",
        ["Ped listesi yenilenemedi:\r\n"] = "The ped list could not be refreshed:\r\n",
        ["Ped paketi eklenemedi:\r\n"] = "The ped pack could not be added:\r\n",
        ["Streamed"] = "Streamed",

        // --- Moderasyon
        ["Beyaz liste (ad)"] = "Whitelist (name)",
        ["Gönder"] = "Send",
        ["Oyuncu adı yazın"] = "Enter a player name",
        ["Seçileni Kaldır"] = "Remove Selected",
        ["Yasaklar ve Beyaz Liste"] = "Bans and Whitelist",
        ["Yasaklı oyuncular (ad)"] = "Banned players (name)",
        ["Sunucuya yalnızca beyaz listedekiler girebilsin"] = "Only whitelisted players may join",

        // --- Katılım kodu
        ["Kısa kod aranıyor: {0} ...\r\n(İnternet üzerinden sunucu bilgileri alınıyor)"] =
            "Searching short code: {0} ...\r\n(fetching server details over the internet)",
        ["Kısa kod bulunamadı.\r\nSunucu açık mı? Kodu doğru yazdınız mı? İnternet bağlantınız var mı?\r\nUzun GTC1 kodunu da deneyebilirsiniz."] =
            "Short code not found.\r\nIs the server running? Did you type the code correctly? Is your internet connection up?\r\nYou can also try the long GTC1 code.",
        ["Sunucu: {0}\r\nAdres: {1}\r\nOyun portu: {2}  |  Sync portu: {3}\r\nŞifre: {4}"] =
            "Server: {0}\r\nAddress: {1}\r\nGame port: {2}  |  Sync port: {3}\r\nPassword: {4}",
        ["Sunucuya ulaşılamadı. Otomatik relay ve doğrudan adresler denendi.\r\n"] =
            "The server could not be reached. Automatic relay and direct addresses were tried.\r\n",
        ["Sunucuya ulaşılamadı: kısa kodda otomatik relay bilgisi yok ve doğrudan adresler aynı ağda değil.\r\n"] =
            "The server could not be reached: the short code has no relay information and the direct addresses are not on the same network.\r\n",
        ["İpucu: Kısa kod (ör. Gel.com) sunucuyu internet üzerinden bulur; sunucu açık olmalı. "] =
            "Tip: a short code (e.g. Gel.com) finds the server over the internet; the server must be running. ",

        // --- Günlük / Hakkında / paylaşım kodu / metin menüsü
        ["Destek paketi oluşturulamadı:\r\n"] = "The support bundle could not be created:\r\n",
        ["Destek paketi oluşturuldu:\r\n{0}\r\n\r\nYolu panoya da kopyaladım. Sorun bildirirken bu dosyayı paylaşabilirsiniz.\r\nParola ve token bilgileri pakete eklenmez."] =
            "Support bundle created:\r\n{0}\r\n\r\nI also copied the path to the clipboard. You can share this file when reporting a problem.\r\nPasswords and tokens are not included.",
        ["Tanılama kopyalanamadı:\r\n"] = "Diagnostics could not be copied:\r\n",
        ["Temizlik hatası:\r\n"] = "Cleanup error:\r\n",
        ["Temizlik tamamlandı.\r\nSunucu modları kaldırıldı; oyun eski hâlinde."] =
            "Cleanup complete.\r\nServer mods were removed; the game is back to its original state.",
        ["Geri yükleme tamamlandı.\r\nNot: Sunucu modlarının dlclist girdileri bu yedekteki duruma döner."] =
            "Restore complete.\r\nNote: the dlclist entries of server mods return to the state in this backup.",
        ["Geliştirici: Reelone"] = "Developer: Reelone",
        ["Güncelleme kontrol ediliyor..."] = "Checking for updates...",
        ["Güncelleme kurulamadı:\r\n"] = "The update could not be installed:\r\n",
        ["Sürüm {0}. Güncelleme kontrol ediliyor..."] = "Version {0}. Checking for updates...",
        ["Sürüm {0} indirilip kurulacak.\r\nLauncher kapanıp yeniden açılacak; oyun dosyalarınız, modlarınız ve ayarlarınız değişmez. Devam edilsin mi?"] =
            "Version {0} will be downloaded and installed.\r\nThe launcher closes and reopens; your game files, mods and settings are not changed. Continue?",
        ["Adres: {0}  |  Oyun portu: {1}  |  Sync portu: {2}\r\nOtomatik relay koda eklendi: {3} (sync {4}, oyun {5}). Arkadaşınız relay öncelikli bağlanır."] =
            "Address: {0}  |  Game port: {1}  |  Sync port: {2}\r\nAutomatic relay added to the code: {3} (sync {4}, game {5}). Your friend connects through the relay first.",
        ["Listeden bir adres seçin (Radmin VPN 26.x, Wi-Fi/yerel IP) veya arkadaşınızın kullanacağı adresi elle yazın. "] =
            "Pick an address from the list (Radmin VPN 26.x, Wi-Fi/LAN IP) or type the address your friend will use. ",
        ["Geri Al"] = "Undo", ["Kes"] = "Cut", ["Yapıştır"] = "Paste",
        ["Tek oyunculu modlarınızı oyun dosyalarını bozmadan yönetin. "] =
            "Manage your single-player mods without touching the game's own files. ",

        // --- Kalan tekil etiketler
        ["Dil"] = "Language",
        ["Sürüm {0}"] = "Version {0}",
        ["Yeniden Başlat"] = "Restart",
        ["Sunucu Modu (VDS) — isteğe bağlı"] = "Server Mode (VDS) — optional",
        ["Sunucuya bağlanılamadı:\r\n"] = "Could not connect to the server:\r\n",
        ["Ped Ekle: .yft/.ydd/.ytd/.ymt dosyalarını seçin. Streamed pedler için "] =
            "Add Ped: select .yft/.ydd/.ytd/.ymt files. For streamed peds ",
        ["auto (doğrulanmış meta varsa açık)"] = "auto (on when verified meta exists)",
        ["off (kapalı)"] = "off",
        ["on (açık)"] = "on",
        ["Temel Model (meta)"] = "Base Model (meta)",
        ["Menüyü daralt"] = "Collapse menu",
        ["Menüyü genişlet"] = "Expand menu",
        ["Dil seçimi"] = "Language selection",
        ["GTA V çok oyunculu oyun yardımcısı"] = "GTA V multiplayer companion",
        ["Topluluk projesi"] = "Community project",

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
