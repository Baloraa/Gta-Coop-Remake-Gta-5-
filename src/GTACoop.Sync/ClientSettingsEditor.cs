using System.Xml.Linq;

namespace GTACoop.Sync;

/// <summary>
/// GTACoOp istemci ayarları düzenleyicisi.
/// GTACoOp, ayarları scriptin yanındaki scripts/ClientSettings.xml dosyasından
/// okur; eski sürümlerde mods/GTACoop altındaki kopya kullanılmıyordu.
/// Dosya yoksa varsayılan içerikle oluşturur; mevcut alanlar korunur.
/// </summary>
public static class ClientSettingsEditor
{
    public static string SettingsPath(string gameFolder)
    {
        var scripts = Path.Combine(gameFolder, "scripts", "ClientSettings.xml");
        var scriptsDir = Path.Combine(gameFolder, "scripts");
        var legacy = Path.Combine(gameFolder, "mods", "GTACoop", "ClientSettings.xml");

        // Normal kurulumda oyun scripts/ClientSettings.xml dosyasını okur.
        // scripts klasörü yoksa eski mods/GTACoop konumuna geri düş.
        return Directory.Exists(scriptsDir) || !File.Exists(legacy) ? scripts : legacy;
    }

    /// <summary>
    /// AutoConnect için sunucu bilgilerini yazar.
    /// lowDesync true ise trafik/ped yoğunluğunu azaltan isteğe bağlı profili uygular.
    /// </summary>
    public static void Apply(
        string gameFolder, string username, string host, int port,
        string password, bool autoConnect, bool lowDesync = false,
        string? fallbackHost = null, int fallbackPort = 0,
        Action<string>? log = null)
    {
        var path = SettingsPath(gameFolder);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        XDocument doc;
        if (File.Exists(path))
        {
            // İlk düzenlemeden önce bir kez yedek al
            var bak = path + ".bak";
            if (!File.Exists(bak))
            {
                File.Copy(path, bak);
                log?.Invoke($"ClientSettings.xml yedeklendi: {Path.GetFileName(bak)}");
            }

            try
            {
                doc = XDocument.Load(path);
            }
            catch
            {
                doc = CreateDefault();
            }
        }
        else
        {
            doc = CreateDefault();
        }

        var root = doc.Root ?? throw new SyncException("ClientSettings.xml kökü okunamadı.");

        // Eski sürümlerdeki master.gtacoop.com artık HTTP 526 döndürüyor.
        // Yalnızca bu bilinen ölü adresi, çalışan yedek listeyle değiştir;
        // kullanıcının özel master adresine dokunma.
        var master = Get(root, "MasterServerAddress").Trim();
        if (string.Equals(master.TrimEnd('/'), "https://master.gtacoop.com",
                StringComparison.OrdinalIgnoreCase))
        {
            var backup = Get(root, "BackupMasterServerAddress").Trim();
            if (string.IsNullOrWhiteSpace(backup))
                backup = "http://clan-banderos.de/gta/";
            Set(root, "MasterServerAddress", backup);
            Set(root, "BackupMasterServerAddress", "https://master.gtacoop.com");
        }

        Set(root, "Username", username);
        Set(root, "LastIP", host);
        Set(root, "LastPort", port.ToString());
        // Doğrudan oyun bağlantısı kurulamazsa istemcinin döneceği yerel
        // relay uç noktası. Relay kullanılmıyorsa temizlenir.
        Set(root, "FallbackIP", fallbackHost ?? "");
        Set(root, "FallbackPort", fallbackPort > 0 ? fallbackPort.ToString() : "0");
        Set(root, "LastPassword", password);
        Set(root, "AutoConnect", autoConnect ? "true" : "false");
        Set(root, "SyncWorld", "true");

        if (lowDesync)
        {
            // İsteğe bağlı profil: yoğunluk ve NPC trafiğini azaltarak
            // senkronizasyon yükünü düşürür.
            ApplyLowDesyncProfile(root, true);
            log?.Invoke("Düşük gecikme profili uygulandı (trafik/ped kapalı, 4 NPC).");
        }
        else if (IsLowDesyncProfile(root))
        {
            // Kullanıcı profili kapattıysa (veya oyun açıkken kapattıysa) eski
            // profil değerleri takılı kalmasın; yalnızca profil değerleri
            // görülüyorsa varsayılana dön, özel ayarlara dokunma.
            ApplyLowDesyncProfile(root, false);
            log?.Invoke("Düşük gecikme profili kapatıldı; istemci ayarları varsayılana döndü.");
        }

        var tmp = path + ".tmp";
        doc.Save(tmp);
        File.Move(tmp, path, overwrite: true);
        log?.Invoke($"İstemci ayarları yazıldı: {host}:{port} (AutoConnect={autoConnect}).");
    }

    /// <summary>
    /// Düşük gecikme profilini mevcut ClientSettings.xml dosyasına hemen uygular.
    /// Profil kapatılınca GTACoOp varsayılanlarına (trafik/ped açık, 10 NPC) döner.
    /// </summary>
    public static void SetLowDesyncProfile(string gameFolder, bool enabled, Action<string>? log = null)
    {
        var path = SettingsPath(gameFolder);
        if (!File.Exists(path)) return;

        try
        {
            var bak = path + ".bak";
            if (!File.Exists(bak))
            {
                File.Copy(path, bak);
                log?.Invoke($"ClientSettings.xml yedeklendi: {Path.GetFileName(bak)}");
            }

            var doc = XDocument.Load(path);
            if (doc.Root == null) return;

            ApplyLowDesyncProfile(doc.Root, enabled);

            var tmp = path + ".tmp";
            doc.Save(tmp);
            File.Move(tmp, path, overwrite: true);
            log?.Invoke(enabled
                ? "Düşük gecikme profili istemci ayarlarına uygulandı (trafik/ped kapalı, 4 NPC)."
                : "Düşük gecikme profili kapatıldı; istemci ayarları varsayılana döndü.");
        }
        catch (Exception ex)
        {
            log?.Invoke("Düşük gecikme profili yazılamadı: " + ex.Message);
        }
    }

    private static void ApplyLowDesyncProfile(XElement root, bool enabled)
    {
        Set(root, "DisableTraffic", enabled ? "true" : "false");
        Set(root, "DisablePeds", enabled ? "true" : "false");
        Set(root, "MaxStreamedNpcs", enabled ? "4" : "10");
    }

    private static bool IsLowDesyncProfile(XElement root) =>
        Get(root, "DisableTraffic") == "true" &&
        Get(root, "DisablePeds") == "true" &&
        Get(root, "MaxStreamedNpcs") == "4";

    private static string Get(XElement root, string name) =>
        root.Elements().FirstOrDefault(e => e.Name.LocalName == name)?.Value ?? "";

    /// <summary>Yalnızca oyuncu adını (Username) günceller; diğer alanlara dokunmaz.</summary>
    public static void SetUsername(string gameFolder, string username, Action<string>? log = null)
    {
        var path = SettingsPath(gameFolder);
        if (!File.Exists(path)) return;
        try
        {
            var doc = XDocument.Load(path);
            if (doc.Root != null) Set(doc.Root, "Username", username);
            var tmp = path + ".tmp";
            doc.Save(tmp);
            File.Move(tmp, path, overwrite: true);
            log?.Invoke($"Oyuncu adı istemci ayarlarına yazıldı: {username}");
        }
        catch (Exception ex)
        {
            log?.Invoke("Oyuncu adı yazılamadı: " + ex.Message);
        }
    }

    /// <summary>AutoConnect'i kapatır.</summary>
    public static void DisableAutoConnect(string gameFolder, Action<string>? log = null)
    {
        var path = SettingsPath(gameFolder);
        if (!File.Exists(path)) return;
        try
        {
            var doc = XDocument.Load(path);
            if (doc.Root != null) Set(doc.Root, "AutoConnect", "false");
            var tmp = path + ".tmp";
            doc.Save(tmp);
            File.Move(tmp, path, overwrite: true);
            log?.Invoke("AutoConnect kapatıldı.");
        }
        catch (Exception ex)
        {
            log?.Invoke("ClientSettings.xml güncellenemedi: " + ex.Message);
        }
    }

    private static void Set(XElement root, string name, string value)
    {
        var el = root.Elements().FirstOrDefault(e => e.Name.LocalName == name);
        if (el == null)
        {
            el = new XElement(name);
            root.Add(el);
        }
        el.Value = value;
    }

    private static XDocument CreateDefault() => new(
        new XElement("PlayerSettings",
            new XAttribute(XNamespace.Xmlns + "xsd", "http://www.w3.org/2001/XMLSchema"),
            new XAttribute(XNamespace.Xmlns + "xsi", "http://www.w3.org/2001/XMLSchema-instance"),
            new XElement("Username", "Player"),
            new XElement("LastIP", "127.0.0.1"),
            new XElement("LastPort", "4499"),
            new XElement("FallbackIP", ""),
            new XElement("FallbackPort", "0"),
            new XElement("LastPassword", ""),
            new XElement("SyncWorld", "true"),
            new XElement("DisableTraffic", "false"),
            new XElement("DisablePeds", "false"),
            new XElement("Logging", "false"),
            new XElement("ChatLog", "false"),
            new XElement("MaxStreamedNpcs", "10"),
            new XElement("MasterServerAddress", "http://clan-banderos.de/gta/"),
            new XElement("BackupMasterServerAddress", "https://master.gtacoop.com"),
            new XElement("ActivationKey", "F9"),
            new XElement("VoiceEnabled", "true"),
            new XElement("VoiceKey", "N"),
            new XElement("HidePasswords", "false"),
            new XElement("AutoConnect", "true"),
            new XElement("AutoReconnect", "true"),
            new XElement("AutoLogin"),
            new XElement("AutoRegister", "false"),
            new XElement("AutoStartServer", "false"),
            new XElement("ShowNetGraph", "false")));
}
