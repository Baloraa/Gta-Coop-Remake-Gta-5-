using System.Xml.Linq;

namespace GTACoop.Sync;

/// <summary>
/// mods/GTACoop/ClientSettings.xml (GTACoOp istemci ayarları) düzenleyicisi.
/// Dosya yoksa varsayılan içerikle oluşturur; mevcut alanlar korunur.
/// </summary>
public static class ClientSettingsEditor
{
    public static string SettingsPath(string gameFolder) =>
        Path.Combine(gameFolder, "mods", "GTACoop", "ClientSettings.xml");

    /// <summary>AutoConnect için sunucu bilgilerini yazar.</summary>
    public static void Apply(
        string gameFolder, string username, string host, int port,
        string password, bool autoConnect, Action<string>? log = null)
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
        Set(root, "Username", username);
        Set(root, "LastIP", host);
        Set(root, "LastPort", port.ToString());
        Set(root, "LastPassword", password);
        Set(root, "AutoConnect", autoConnect ? "true" : "false");
        Set(root, "SyncWorld", "true");

        var tmp = path + ".tmp";
        doc.Save(tmp);
        File.Move(tmp, path, overwrite: true);
        log?.Invoke($"İstemci ayarları yazıldı: {host}:{port} (AutoConnect={autoConnect}).");
    }

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
            new XElement("LastPassword", ""),
            new XElement("SyncWorld", "true"),
            new XElement("DisableTraffic", "false"),
            new XElement("DisablePeds", "false"),
            new XElement("Logging", "false"),
            new XElement("ChatLog", "false"),
            new XElement("MaxStreamedNpcs", "10"),
            new XElement("MasterServerAddress", "https://master.gtacoop.com"),
            new XElement("BackupMasterServerAddress", ""),
            new XElement("ActivationKey", "F9"),
            new XElement("HidePasswords", "false"),
            new XElement("AutoConnect", "true"),
            new XElement("AutoReconnect", "true"),
            new XElement("AutoLogin"),
            new XElement("AutoRegister", "false"),
            new XElement("AutoStartServer", "false"),
            new XElement("ShowNetGraph", "false")));
}
