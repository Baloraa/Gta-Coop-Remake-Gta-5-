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
        ["Seçilen model bir ped değil: "] = "The selected model is not a ped: "
    };
}
