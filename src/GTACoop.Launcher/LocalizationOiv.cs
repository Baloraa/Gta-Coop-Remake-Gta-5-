namespace GTACoop.Remake;

/// <summary>OIV paket yönetimi için Türkçe/İngilizce/Rusça metinler.</summary>
public static partial class Loc
{
    private static readonly Dictionary<string, string> OivEn = new()
    {
        ["OIV paketi"] = "OIV pack",
        ["OIV Paketi"] = "OIV Pack",
        ["OIV Paketi Ekle (.oiv)..."] = "Add OIV Package (.oiv)...",
        ["OpenIV paketini seçin (.oiv)."] = "Select the OpenIV package (.oiv).",
        ["OpenIV paketi (*.oiv)|*.oiv|Tüm dosyalar (*.*)|*.*"] =
            "OpenIV package (*.oiv)|*.oiv|All files (*.*)|*.*",
        ["'{0}' zaten Addons\\oiv içinde. Üzerine yazılsın mı?"] =
            "'{0}' already exists in Addons\\oiv. Overwrite?",
        ["OIV paketi eklendi: {0}\r\nSunucu çalışıyorsa bir sonraki manifest isteğinde otomatik dağıtılır."] =
            "OIV package added: {0}\r\nIf the server is running, it will be distributed automatically on the next manifest request.",
        ["\r\n{0} pakette desteklenmeyen işlemler atlanacak."] =
            "\r\nUnsupported operations will be skipped in {0} package(s).",
        ["\r\n{0} paket atlandı."] = "\r\n{0} package(s) skipped.",
        ["Bazı OIV paketleri eklenemedi:\r\n"] = "Some OIV packages could not be added:\r\n",
        ["OIV paketi eklenemedi:\r\n"] = "Could not add OIV package:\r\n",
        ["DLC/OIV paketleri kurulamıyor (update.rpf güvenli değil) ({0}): {1}\r\n{2}\r\n\r\n"] =
            "DLC/OIV packs cannot be installed (update.rpf is not safe) ({0}): {1}\r\n{2}\r\n\r\n",
        ["DLC paketleri: Addons\\dlcpacks\\  |  OIV paketleri: Addons\\oiv\\  |  Scriptler: Addons\\scripts\\  |  ASI: Addons\\asi\\  |  Sunucu eklentileri: Plugins\\\r\nHer OIV paketi kendi klasöründe saklanır. Meta için: addon.json"] =
            "DLC packs: Addons\\dlcpacks\\  |  OIV packs: Addons\\oiv\\  |  Scripts: Addons\\scripts\\  |  ASI: Addons\\asi\\  |  Server plugins: Plugins\\\r\nEach OIV package is kept in its own folder. For meta: addon.json",
        ["mods/update/update.rpf bulunamadı. DLC/OIV paketleri için OpenIV ile mods klasörü oluşturulmalı."] =
            "mods/update/update.rpf was not found. Create a mods folder with OpenIV to use DLC/OIV packs.",
        ["OIV paketi bulunamadı: {0}"] = "OIV package not found: {0}",
        ["OIV paketi boş: {0}"] = "OIV package is empty: {0}",
        ["OIV paketi boyutu sınırı aşıyor: {0}"] = "OIV package exceeds the size limit: {0}",
        ["OIV paketi açılamadı (geçersiz zip): {0}"] = "OIV package could not be opened (invalid zip): {0}",
        ["OIV içindeki dosya çok büyük: {0}"] = "File inside the OIV is too large: {0}",
        ["OIV paketinde assembly.xml yok."] = "The OIV package has no assembly.xml.",
        ["OIV assembly.xml çok büyük."] = "OIV assembly.xml is too large.",
        ["OIV assembly.xml geçersiz."] = "OIV assembly.xml is invalid.",
        ["OIV assembly.xml kökü 'package' değil."] = "OIV assembly.xml root is not 'package'.",
        ["OIV hedefi desteklenmiyor: {0}"] = "Unsupported OIV target: {0}",
        ["OIV 1.x biçimi desteklenmiyor: {0}"] = "OIV 1.x format is not supported: {0}",
        ["OIV assembly.xml içinde <content> yok."] = "OIV assembly.xml has no <content>.",
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
        ["{0} -> {1}: {2}"] = "{0} -> {1}: {2}",
        ["Arşiv düzenlemesi desteklenmiyor (yalnızca update.rpf/dlclist): {0}"] =
            "Archive editing is not supported (only update.rpf/dlclist): {0}",
        ["update.rpf içinde {0} güvenlik için uygulanmaz: {1}"] =
            "{0} inside update.rpf is not applied for safety: {1}",
        ["Desteklenmeyen update.rpf işlemi: {0}"] = "Unsupported update.rpf operation: {0}",
        ["update.rpf XML düzenlemesi desteklenmiyor (yalnızca dlclist.xml): {0}"] =
            "update.rpf XML editing is not supported (only dlclist.xml): {0}",
        ["dlclist XML işleminde <Item> bulunamadı."] = "No <Item> found in the dlclist XML operation.",
        ["dlclist XML işlemi güvenlik için uygulanmaz: {0}"] =
            "dlclist XML operation is not applied for safety: {0}",
        ["update.rpf metin düzenlemesi desteklenmiyor (yalnızca dlclist.xml): {0}"] =
            "update.rpf text editing is not supported (only dlclist.xml): {0}",
        ["dlclist metin işleminde <Item> bulunamadı."] = "No <Item> found in the dlclist text operation.",
        ["update.rpf dosya ekleme desteklenmiyor (yalnızca dlclist.xml): {0}"] =
            "Adding files to update.rpf is not supported (only dlclist.xml): {0}",
        ["dlclist kaynağı bulunamadı: {0}"] = "dlclist source not found: {0}",
        ["dlclist kaynağı çok büyük: {0}"] = "dlclist source is too large: {0}",
        ["dlclist kaynağı okunamadı: {0} ({1})"] = "dlclist source could not be read: {0} ({1})",
        ["boş hedef"] = "empty destination",
        ["geçersiz/güvenlik dışı yol"] = "invalid/unsafe path",
        ["update.rpf doğrudan değiştirilemez"] = "update.rpf cannot be changed directly",
        ["update/ altındaki gevşek dosyalar güvenli değil (yalnızca dlcpacks)"] =
            "loose files under update/ are not safe (dlcpacks only)",
        ["oyun köküne yalnızca güvenli eklenti/veri dosyaları eklenir ({0})"] =
            "only safe plugin/data files can be added to the game root ({0})",
        ["desteklenmeyen hedef klasör"] = "unsupported destination folder",
        ["GTA V açıkken OIV paketi kurulamaz. Lütfen oyunu kapatın."] =
            "OIV packages cannot be installed while GTA V is running. Close the game first.",
        ["OIV: paket dosyası manifestte yok."] = "OIV: package file is missing from the manifest.",
        ["OIV paketi önbellekte bulunamadı: {0}"] = "OIV package not found in cache: {0}",
        ["OIV: {0} ({1})"] = "OIV: {0} ({1})",
        ["OIV: bilinmeyen/eski dosya korundu: {0}"] = "OIV: unknown/legacy file preserved: {0}",
        ["OIV: kullanıcı dosyası korundu: {0}"] = "OIV: user file preserved: {0}",
        ["OIV: güvenlik dışı hedef: {0}"] = "OIV: unsafe destination: {0}",
        ["OIV: mevcut dosya korundu: {0}"] = "OIV: existing file preserved: {0}",
        ["UYARI: eski OIV dlclist girdisi kaldırılamadı; eski dosyalar korundu."] =
            "WARNING: the old OIV dlclist entry could not be removed; old files were preserved.",
        ["OIV: {0} dosya kuruldu, {1} dosya korundu, {2} dlclist girdisi eklendi."] =
            "OIV: {0} file(s) installed, {1} file(s) preserved, {2} dlclist entry(ies) added.",
        ["UYARI: eski OIV dosyası geri getirilemedi: {0} ({1})"] =
            "WARNING: old OIV file could not be restored: {0} ({1})",
        ["Kullanıcı dosyası korundu: {0}"] = "User file preserved: {0}",
        ["UYARI: OIV dlclist girdisi kaldırılamadı; dosyalar korundu."] =
            "WARNING: the OIV dlclist entry could not be removed; files were preserved.",
        ["UYARI: OIV paketi kurulamadı: {0} ({1})"] =
            "WARNING: OIV package could not be installed: {0} ({1})",
        ["Atlandı: {0} (OIV kurulamadı)"] = "Skipped: {0} (OIV could not be installed)",
        ["Atlandı (paket kurulamıyor): {0} -> {1}"] =
            "Skipped (package cannot be installed): {0} -> {1}",
        ["Atlandı: {0} (paket kurulamıyor)"] = "Skipped: {0} (package cannot be installed)"
    };

    private static readonly Dictionary<string, string> OivRu = new()
    {
        ["OIV pack"] = "OIV-пакет",
        ["OIV Pack"] = "OIV-пакет",
        ["Add OIV Package (.oiv)..."] = "Добавить OIV-пакет (.oiv)...",
        ["Select the OpenIV package (.oiv)."] = "Выберите пакет OpenIV (.oiv).",
        ["OpenIV package (*.oiv)|*.oiv|All files (*.*)|*.*"] =
            "Пакет OpenIV (*.oiv)|*.oiv|Все файлы (*.*)|*.*",
        ["'{0}' already exists in Addons\\oiv. Overwrite?"] =
            "'{0}' уже существует в Addons\\oiv. Перезаписать?",
        ["OIV package added: {0}\r\nIf the server is running, it will be distributed automatically on the next manifest request."] =
            "OIV-пакет добавлен: {0}\r\nЕсли сервер запущен, он будет распространён автоматически при следующем запросе манифеста.",
        ["\r\nUnsupported operations will be skipped in {0} package(s)."] =
            "\r\nВ {0} пакете(ах) неподдерживаемые операции будут пропущены.",
        ["\r\n{0} package(s) skipped."] = "\r\nПропущено пакетов: {0}.",
        ["Some OIV packages could not be added:\r\n"] = "Некоторые OIV-пакеты не удалось добавить:\r\n",
        ["Could not add OIV package:\r\n"] = "Не удалось добавить OIV-пакет:\r\n",
        ["DLC/OIV packs cannot be installed (update.rpf is not safe) ({0}): {1}\r\n{2}\r\n\r\n"] =
            "DLC/OIV-пакеты нельзя установить (update.rpf небезопасен) ({0}): {1}\r\n{2}\r\n\r\n",
        ["DLC packs: Addons\\dlcpacks\\  |  OIV packs: Addons\\oiv\\  |  Scripts: Addons\\scripts\\  |  ASI: Addons\\asi\\  |  Server plugins: Plugins\\\r\nEach OIV package is kept in its own folder. For meta: addon.json"] =
            "DLC-пакеты: Addons\\dlcpacks\\  |  OIV-пакеты: Addons\\oiv\\  |  Скрипты: Addons\\scripts\\  |  ASI: Addons\\asi\\  |  Серверные плагины: Plugins\\\r\nКаждый OIV-пакет хранится в своей папке. Метаданные: addon.json",
        ["mods/update/update.rpf was not found. Create a mods folder with OpenIV to use DLC/OIV packs."] =
            "mods/update/update.rpf не найден. Создайте папку mods с помощью OpenIV для DLC/OIV-пакетов.",
        ["OIV package not found: {0}"] = "OIV-пакет не найден: {0}",
        ["OIV package is empty: {0}"] = "OIV-пакет пуст: {0}",
        ["OIV package exceeds the size limit: {0}"] = "OIV-пакет превышает лимит размера: {0}",
        ["OIV package could not be opened (invalid zip): {0}"] =
            "Не удалось открыть OIV-пакет (неверный zip): {0}",
        ["File inside the OIV is too large: {0}"] = "Файл внутри OIV слишком велик: {0}",
        ["The OIV package has no assembly.xml."] = "В OIV-пакете нет assembly.xml.",
        ["OIV assembly.xml is too large."] = "OIV assembly.xml слишком велик.",
        ["OIV assembly.xml is invalid."] = "OIV assembly.xml недействителен.",
        ["OIV assembly.xml root is not 'package'."] = "Корень OIV assembly.xml не 'package'.",
        ["Unsupported OIV target: {0}"] = "Неподдерживаемая цель OIV: {0}",
        ["OIV 1.x format is not supported: {0}"] = "Формат OIV 1.x не поддерживается: {0}",
        ["OIV assembly.xml has no <content>."] = "В OIV assembly.xml нет <content>.",
        ["{0}: {1} (delete/replace is not applied for safety)"] =
            "{0}: {1} (удаление/замена не применяется в целях безопасности)",
        ["Unsupported OIV operation: {0}"] = "Неподдерживаемая операция OIV: {0}",
        ["The OIV package has no operation that can be applied safely. Unsupported: {0}"] =
            "В OIV-пакете нет операций, которые можно безопасно применить. Не поддерживается: {0}",
        ["OIV: {0} unsupported operation(s) will be skipped."] =
            "OIV: неподдерживаемых операций будет пропущено: {0}.",
        ["<add> has no source: {0}"] = "У <add> нет источника: {0}",
        ["<add> has no destination: {0}"] = "У <add> нет назначения: {0}",
        ["update.rpf cannot be changed directly; only dlclist entries are added."] =
            "update.rpf нельзя изменять напрямую; добавляются только записи dlclist.",
        ["OIV source not found: {0}"] = "Источник OIV не найден: {0}",
        ["{0} -> {1}: {2}"] = "{0} -> {1}: {2}",
        ["Archive editing is not supported (only update.rpf/dlclist): {0}"] =
            "Редактирование архивов не поддерживается (только update.rpf/dlclist): {0}",
        ["{0} inside update.rpf is not applied for safety: {1}"] =
            "{0} внутри update.rpf не применяется в целях безопасности: {1}",
        ["Unsupported update.rpf operation: {0}"] = "Неподдерживаемая операция update.rpf: {0}",
        ["update.rpf XML editing is not supported (only dlclist.xml): {0}"] =
            "Редактирование XML update.rpf не поддерживается (только dlclist.xml): {0}",
        ["No <Item> found in the dlclist XML operation."] =
            "В XML-операции dlclist не найден <Item>.",
        ["dlclist XML operation is not applied for safety: {0}"] =
            "XML-операция dlclist не применяется в целях безопасности: {0}",
        ["update.rpf text editing is not supported (only dlclist.xml): {0}"] =
            "Редактирование текста update.rpf не поддерживается (только dlclist.xml): {0}",
        ["No <Item> found in the dlclist text operation."] =
            "В текстовой операции dlclist не найден <Item>.",
        ["Adding files to update.rpf is not supported (only dlclist.xml): {0}"] =
            "Добавление файлов в update.rpf не поддерживается (только dlclist.xml): {0}",
        ["dlclist source not found: {0}"] = "Источник dlclist не найден: {0}",
        ["dlclist source is too large: {0}"] = "Источник dlclist слишком велик: {0}",
        ["dlclist source could not be read: {0} ({1})"] =
            "Не удалось прочитать источник dlclist: {0} ({1})",
        ["empty destination"] = "пустое назначение",
        ["invalid/unsafe path"] = "недопустимый/небезопасный путь",
        ["update.rpf cannot be changed directly"] = "update.rpf нельзя изменять напрямую",
        ["loose files under update/ are not safe (dlcpacks only)"] =
            "отдельные файлы в update/ небезопасны (только dlcpacks)",
        ["only safe plugin/data files can be added to the game root ({0})"] =
            "в корень игры можно добавлять только безопасные файлы плагинов/данных ({0})",
        ["unsupported destination folder"] = "неподдерживаемая папка назначения",
        ["OIV packages cannot be installed while GTA V is running. Close the game first."] =
            "OIV-пакеты нельзя устанавливать во время работы GTA V. Сначала закройте игру.",
        ["OIV: package file is missing from the manifest."] =
            "OIV: файл пакета отсутствует в манифесте.",
        ["OIV package not found in cache: {0}"] = "OIV-пакет не найден в кэше: {0}",
        ["OIV: {0} ({1})"] = "OIV: {0} ({1})",
        ["OIV: unknown/legacy file preserved: {0}"] =
            "OIV: неизвестный/старый файл сохранён: {0}",
        ["OIV: user file preserved: {0}"] = "OIV: пользовательский файл сохранён: {0}",
        ["OIV: unsafe destination: {0}"] = "OIV: небезопасное назначение: {0}",
        ["OIV: existing file preserved: {0}"] = "OIV: существующий файл сохранён: {0}",
        ["WARNING: the old OIV dlclist entry could not be removed; old files were preserved."] =
            "ВНИМАНИЕ: старую запись dlclist OIV не удалось удалить; старые файлы сохранены.",
        ["OIV: {0} file(s) installed, {1} file(s) preserved, {2} dlclist entry(ies) added."] =
            "OIV: установлено файлов: {0}, сохранено: {1}, добавлено записей dlclist: {2}.",
        ["WARNING: old OIV file could not be restored: {0} ({1})"] =
            "ВНИМАНИЕ: старый файл OIV не удалось восстановить: {0} ({1})",
        ["User file preserved: {0}"] = "Пользовательский файл сохранён: {0}",
        ["WARNING: the OIV dlclist entry could not be removed; files were preserved."] =
            "ВНИМАНИЕ: запись dlclist OIV не удалось удалить; файлы сохранены.",
        ["WARNING: OIV package could not be installed: {0} ({1})"] =
            "ВНИМАНИЕ: OIV-пакет не удалось установить: {0} ({1})",
        ["Skipped: {0} (OIV could not be installed)"] =
            "Пропущено: {0} (OIV не удалось установить)",
        ["Skipped (package cannot be installed): {0} -> {1}"] =
            "Пропущено (пакет нельзя установить): {0} -> {1}",
        ["Skipped: {0} (package cannot be installed)"] =
            "Пропущено: {0} (пакет нельзя установить)"
    };
}
