namespace GTACoop.Sync;

/// <summary>
/// OIV paket yönetimi günlük/hata metinlerinin İngilizce karşılıkları.
/// Anahtarlar Türkçe kalıplardır; T() ve Translate() bu sözlüğü En'den sonra
/// kontrol eder.
/// </summary>
public static partial class SyncText
{
    private static readonly Dictionary<string, string> OivEn = new()
    {
        ["OIV paketi bulunamadı: {0}"] = "OIV package not found: {0}",
        ["OIV paketi boş: {0}"] = "OIV package is empty: {0}",
        ["OIV paketi boyutu sınırı aşıyor: {0}"] = "OIV package exceeds the size limit: {0}",
        ["OIV paketi açılamadı (geçersiz zip): {0}"] =
            "OIV package could not be opened (invalid zip): {0}",
        ["OIV içindeki dosya çok büyük: {0}"] = "File inside the OIV is too large: {0}",
        ["OIV paketinde assembly.xml yok."] = "The OIV package has no assembly.xml.",
        ["OIV assembly.xml çok büyük."] = "OIV assembly.xml is too large.",
        ["OIV assembly.xml geçersiz."] = "OIV assembly.xml is invalid.",
        ["OIV assembly.xml kökü 'package' değil."] =
            "OIV assembly.xml root is not 'package'.",
        ["OIV hedefi desteklenmiyor: {0}"] = "Unsupported OIV target: {0}",
        ["OIV 1.x biçimi desteklenmiyor: {0}"] =
            "OIV 1.x format is not supported: {0}",
        ["OIV assembly.xml içinde <content> yok."] =
            "OIV assembly.xml has no <content>.",
        ["{0}: {1} (silme/değiştirme güvenlik için uygulanmaz)"] =
            "{0}: {1} (delete/replace is not applied for safety)",
        ["Desteklenmeyen OIV işlemi: {0}"] = "Unsupported OIV operation: {0}",
        ["OIV paketinde güvenle uygulanabilecek işlem yok. Desteklenmeyen: {0}"] =
            "The OIV package has no operation that can be applied safely. Unsupported: {0}",
        ["OIV: {0} desteklenmeyen işlem atlanacak."] =
            "OIV: {0} unsupported operation(s) will be skipped.",
        ["<add> kaynağı yok: {0}"] = "<add> has no source: {0}",
        ["<add> hedefi yok: {0}"] = "<add> has no destination: {0}",
        ["update.rpf doğrudan değiştirilemez; yalnızca dlclist girdileri eklenir."] =
            "update.rpf cannot be changed directly; only dlclist entries are added.",
        ["OIV kaynağı bulunamadı: {0}"] = "OIV source not found: {0}",
        ["Arşiv düzenlemesi desteklenmiyor (yalnızca update.rpf/dlclist): {0}"] =
            "Archive editing is not supported (only update.rpf/dlclist): {0}",
        ["update.rpf içinde {0} güvenlik için uygulanmaz: {1}"] =
            "{0} inside update.rpf is not applied for safety: {1}",
        ["Desteklenmeyen update.rpf işlemi: {0}"] =
            "Unsupported update.rpf operation: {0}",
        ["update.rpf XML düzenlemesi desteklenmiyor (yalnızca dlclist.xml): {0}"] =
            "update.rpf XML editing is not supported (only dlclist.xml): {0}",
        ["dlclist XML işleminde <Item> bulunamadı."] =
            "No <Item> found in the dlclist XML operation.",
        ["dlclist XML işlemi güvenlik için uygulanmaz: {0}"] =
            "dlclist XML operation is not applied for safety: {0}",
        ["update.rpf metin düzenlemesi desteklenmiyor (yalnızca dlclist.xml): {0}"] =
            "update.rpf text editing is not supported (only dlclist.xml): {0}",
        ["dlclist metin işleminde <Item> bulunamadı."] =
            "No <Item> found in the dlclist text operation.",
        ["update.rpf dosya ekleme desteklenmiyor (yalnızca dlclist.xml): {0}"] =
            "Adding files to update.rpf is not supported (only dlclist.xml): {0}",
        ["dlclist kaynağı bulunamadı: {0}"] = "dlclist source not found: {0}",
        ["dlclist kaynağı çok büyük: {0}"] = "dlclist source is too large: {0}",
        ["dlclist kaynağı okunamadı: {0} ({1})"] =
            "dlclist source could not be read: {0} ({1})",
        ["boş hedef"] = "empty destination",
        ["geçersiz/güvenlik dışı yol"] = "invalid/unsafe path",
        ["update.rpf doğrudan değiştirilemez"] =
            "update.rpf cannot be changed directly",
        ["update/ altındaki gevşek dosyalar güvenli değil (yalnızca dlcpacks)"] =
            "loose files under update/ are not safe (dlcpacks only)",
        ["oyun köküne yalnızca güvenli eklenti/veri dosyaları eklenir ({0})"] =
            "only safe plugin/data files can be added to the game root ({0})",
        ["desteklenmeyen hedef klasör"] = "unsupported destination folder",
        ["OIV dosyası çok büyük: {0}"] = "OIV file is too large: {0}",
        ["UYARI: OIV paketi kurulamadı: {0} ({1})"] =
            "WARNING: OIV package could not be installed: {0} ({1})",
        ["Atlandı: {0} (OIV kurulamadı)"] =
            "Skipped: {0} (OIV could not be installed)",
        ["GTA V açıkken OIV paketi kurulamaz. Lütfen oyunu kapatın."] =
            "OIV packages cannot be installed while GTA V is running. Close the game first.",
        ["OIV: paket dosyası manifestte yok."] =
            "OIV: package file is missing from the manifest.",
        ["OIV paketi önbellekte bulunamadı: {0}"] =
            "OIV package not found in cache: {0}",
        ["OIV: bilinmeyen/eski dosya korundu: {0}"] =
            "OIV: unknown/legacy file preserved: {0}",
        ["OIV: kullanıcı dosyası korundu: {0}"] =
            "OIV: user file preserved: {0}",
        ["OIV: güvenlik dışı hedef: {0}"] = "OIV: unsafe destination: {0}",
        ["OIV: mevcut dosya korundu: {0}"] =
            "OIV: existing file preserved: {0}",
        ["UYARI: eski OIV dlclist girdisi kaldırılamadı; eski dosyalar korundu."] =
            "WARNING: the old OIV dlclist entry could not be removed; old files were preserved.",
        ["OIV: {0} dosya kuruldu, {1} dosya korundu, {2} dlclist girdisi eklendi."] =
            "OIV: {0} file(s) installed, {1} file(s) preserved, {2} dlclist entry(ies) added.",
        ["UYARI: eski OIV dosyası geri getirilemedi: {0} ({1})"] =
            "WARNING: old OIV file could not be restored: {0} ({1})",
        ["Kullanıcı dosyası korundu: {0}"] = "User file preserved: {0}",
        ["UYARI: OIV dlclist girdisi kaldırılamadı; dosyalar korundu."] =
            "WARNING: the OIV dlclist entry could not be removed; files were preserved.",
        ["Atlandı (paket kurulamıyor): {0} -> {1}"] =
            "Skipped (package cannot be installed): {0} -> {1}",
        ["Atlandı: {0} (paket kurulamıyor)"] =
            "Skipped: {0} (package cannot be installed)",
        ["mods/update/update.rpf bulunamadı. DLC/OIV paketleri için OpenIV ile mods klasörü oluşturulmalı."] =
            "mods/update/update.rpf was not found. Create a mods folder with OpenIV to use DLC/OIV packs.",
        ["Eski OIV önbelleği temizlendi: {0}"] =
            "Old OIV cache cleaned: {0}",
        ["UYARI: eski OIV önbelleği temizlenemedi: {0}"] =
            "WARNING: old OIV cache could not be cleaned: {0}",

        // mods klasöründeki RPF arşiv düzenlemeleri
        ["Arşiv düzenlemesi atlandı: {0} ({1})"] =
            "Archive edit skipped: {0} ({1})",
        ["Yalnızca dlclist.xml XML düzenlemesi destekleniyor: {0}"] =
            "Only dlclist.xml XML editing is supported: {0}",
        ["Yalnızca dlclist.xml metin düzenlemesi destekleniyor: {0}"] =
            "Only dlclist.xml text editing is supported: {0}",
        ["dlclist.xml'in tamamını değiştirmek güvenlik için uygulanmaz."] =
            "Replacing the whole dlclist.xml is not applied for safety.",
        ["Arşivden dosya silme güvenlik için uygulanmaz: {0} ({1})"] =
            "Deleting files from an archive is not applied for safety: {0} ({1})",
        ["Desteklenmeyen arşiv işlemi ({0}): {1}"] =
            "Unsupported archive operation ({0}): {1}",
        ["arşiv yolu boş"] = "archive path is empty",
        ["desteklenmeyen arşiv türü"] = "unsupported archive type",
        ["iç içe arşiv düzenlemesi desteklenmiyor"] =
            "nested archive editing is not supported",
        ["geçersiz/güvenlik dışı arşiv yolu"] = "invalid/unsafe archive path",
        ["yalnızca mods klasöründeki arşivler düzenlenebilir"] =
            "only archives inside the mods folder can be edited",
        ["Arşiv dosya işleminin kaynağı yok: {0}"] =
            "Archive file operation has no source: {0}",
        ["Arşiv dosya hedefi geçersiz: {0}"] =
            "Archive file destination is invalid: {0}",
        ["dlclist.xml yalnızca <xml>/<text> veya tam kaynak <add> ile güncellenir."] =
            "dlclist.xml is only updated via <xml>/<text> or a full-source <add>.",
        ["OIV arşiv kaynağı bulunamadı: {0}"] = "OIV archive source not found: {0}",
        ["Arşiv içi hedef geçersiz: {0}"] = "Archive entry destination is invalid: {0}",
        ["OIV arşiv dosyası çok büyük: {0}"] = "OIV archive file is too large: {0}",
        ["OIV: önceki sürümün arşiv düzenlemeleri geri alınıyor."] =
            "OIV: rolling back the previous version's archive edits.",
        ["OIV: mods arşivi bulunamadı, arşiv düzenlemesi atlandı: {0}"] =
            "OIV: mods archive not found, archive edit skipped: {0}",
        ["OIV: arşiv yapısı sağlıklı değil, düzenleme yapılmadı: {0}"] =
            "OIV: archive structure is not healthy, no edit was made: {0}",
        ["OIV: kullanıcı değişikliği korundu (arşiv): {0} :: {1}"] =
            "OIV: user change preserved (archive): {0} :: {1}",
        ["OIV: arşiv girdisi türü uyuşmuyor, güvenlik için kurulum durduruldu: {0} :: {1}"] =
            "OIV: archive entry type mismatch, installation stopped for safety: {0} :: {1}",
        ["OIV: {0} arşiv dosyası eklendi/değiştirildi."] =
            "OIV: {0} archive file(s) added/replaced.",
        ["OIV: {0} arşiv düzenlemesi atlandı (eksik arşiv)."] =
            "OIV: {0} archive edit(s) skipped (missing archive).",
        ["UYARI: eski OIV arşiv önbelleği bulunamadı: {0}"] =
            "WARNING: old OIV archive cache not found: {0}",
        ["UYARI: eski OIV paketi önbellekte yok: {0}"] =
            "WARNING: old OIV package is not in the cache: {0}",
        ["OIV: önceki sürümün arşiv düzenlemeleri geri getirildi."] =
            "OIV: previous version's archive edits were restored.",
        ["UYARI: önceki OIV arşiv düzenlemeleri geri getirilemedi: {0}"] =
            "WARNING: previous OIV archive edits could not be restored: {0}",
        ["OIV: kullanıcı değiştirdiği için arşiv girdisi korundu: {0} :: {1}"] =
            "OIV: archive entry preserved because the user changed it: {0} :: {1}",
        ["UYARI: arşiv girdisi geri alınamadı, korundu: {0} :: {1} ({2})"] =
            "WARNING: archive entry could not be rolled back and was preserved: {0} :: {1} ({2})",
        ["OIV: arşiv girdisi zaten yok, atlandı: {0} :: {1}"] =
            "OIV: archive entry is already gone, skipped: {0} :: {1}",
        ["geçersiz arşiv yolu"] = "invalid archive path",
        ["arşiv bulunamadı"] = "archive not found"
    };
}
