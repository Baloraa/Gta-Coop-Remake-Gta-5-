using System.Text;
using GTACoop.Sync;

namespace GTACoop.Remake;

/// <summary>Uygulama ortamı: veri klasörü, ayarlar, günlük.</summary>
public static class AppEnv
{
    private static readonly object _logLock = new();

    public static string DataFolder { get; private set; } = "";
    public static LauncherSettings Settings { get; private set; } = new();
    public static string CacheRoot => Path.Combine(DataFolder, "cache");
    public static string ServerDir => Path.Combine(DataFolder, "server");
    private static string? _serversFolder;

    /// <summary>
    /// Sunucuya özel mod klasörlerinin kökü: uygulama klasörü\Servers.
    /// Uygulama klasörü yazılamazsa veri klasörüne düşer.
    /// </summary>
    public static string ServersFolder => _serversFolder ??= ResolveServersFolder();
    public static string LogFile => Path.Combine(DataFolder, "logs", $"launcher-{DateTime.Now:yyyyMMdd}.log");
    public static string SettingsPath => Path.Combine(DataFolder, "settings.json");

    public static event Action<string>? LogMessage;

    public static void Init()
    {
        DataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "GTACoopRemake");

        Directory.CreateDirectory(DataFolder);
        Directory.CreateDirectory(CacheRoot);
        Directory.CreateDirectory(ServerDir);
        Directory.CreateDirectory(Path.Combine(DataFolder, "logs"));
        Directory.CreateDirectory(ServersFolder);

        Settings = SyncJson.Load<LauncherSettings>(SettingsPath) ?? new LauncherSettings();
        Settings.DataFolder = DataFolder;

        // Günlük dosyası ilk satırdan itibaren seçili dilde olsun.
        SyncText.English = string.Equals(Settings.Language, "en", StringComparison.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(Settings.GameFolder) || !GameFolder.IsValid(Settings.GameFolder))
        {
            var detected = GameFolder.TryDetect();
            if (detected != null) Settings.GameFolder = detected;
        }

        if (string.IsNullOrWhiteSpace(Settings.Username))
            Settings.Username = Environment.UserName;

        SaveSettings();
        Log($"Launcher başlatıldı. Veri klasörü: {DataFolder}");
        if (!string.IsNullOrEmpty(Settings.GameFolder))
            Log($"GTA V klasörü: {Settings.GameFolder}");
    }

    public static void SaveSettings() => SyncJson.Save(SettingsPath, Settings);

    private static string ResolveServersFolder()
    {
        try
        {
            var dir = Path.Combine(AppContext.BaseDirectory, "Servers");
            Directory.CreateDirectory(dir);
            return dir;
        }
        catch (Exception ex)
        {
            var fallback = Path.Combine(DataFolder, "Servers");
            Directory.CreateDirectory(fallback);
            Log($"Uygulama klasöründe 'Servers' oluşturulamadı ({ex.Message}); " +
                $"veri klasörü kullanılıyor: {fallback}");
            return fallback;
        }
    }

    /// <summary>
    /// Bir sunucunun mod klasörünü döndürür: <c>Servers\&lt;sunucu adı&gt;__&lt;sunucu kimliği&gt;</c>.
    /// Klasör ada göre değil, kararlı sunucu kimliğine göre bulunur; sunucu adı değişse bile
    /// indirilen modlar yeniden indirilmez. İçine okunabilir bir <c>server.json</c> yazılır.
    /// </summary>
    public static string ServerContentFolder(ServerEntry entry, Manifest manifest)
    {
        var idPart = AddonApplier.Safe(manifest.ServerId);
        var root = ServersFolder;

        string? existing = null;
        try
        {
            existing = Directory.EnumerateDirectories(root)
                .FirstOrDefault(d => Path.GetFileName(d)
                    .EndsWith("__" + idPart, StringComparison.OrdinalIgnoreCase));
        }
        catch { /* klasör taranamazsa yeni klasör adı denenir */ }

        var displayName = string.IsNullOrWhiteSpace(manifest.ServerName)
            ? entry.Name
            : manifest.ServerName;
        var dir = existing ?? Path.Combine(root, AddonApplier.Safe(displayName) + "__" + idPart);
        Directory.CreateDirectory(dir);

        try
        {
            SyncJson.Save(Path.Combine(dir, "server.json"), new ServerFolderInfo
            {
                Name = displayName,
                ServerId = manifest.ServerId,
                Host = entry.Host,
                GamePort = entry.GamePort,
                SyncPort = entry.SyncPort,
                LastUsedUtc = DateTime.UtcNow
            });
        }
        catch { /* bilgi dosyası kritik değil */ }

        return dir;
    }

    /// <summary>Bir sunucunun indirilmiş mod dosyalarını siler (server.json kalır).</summary>
    public static void ResetServerContent(string dir)
    {
        if (!Directory.Exists(dir)) return;
        foreach (var entry in Directory.EnumerateFileSystemEntries(dir))
        {
            if (string.Equals(Path.GetFileName(entry), "server.json", StringComparison.OrdinalIgnoreCase))
                continue;
            if (Directory.Exists(entry)) Directory.Delete(entry, recursive: true);
            else File.Delete(entry);
        }
    }

    /// <summary>Sunucu klasöründeki okunabilir bilgi dosyası (server.json).</summary>
    public sealed class ServerFolderInfo
    {
        public string Name { get; set; } = "";
        public string ServerId { get; set; } = "";
        public string Host { get; set; } = "";
        public int GamePort { get; set; } = 4499;
        public int SyncPort { get; set; } = 4599;
        public DateTime LastUsedUtc { get; set; }
    }

    public static void Log(string message)
    {
        // Sync katmanı ve eski launcher mesajları Türkçe üretilebilir; İngilizce
        // arayüzde günlük dosyası ve günlük sekmesi de tutarlı olsun.
        message = SyncText.Translate(message);
        var line = $"[{DateTime.Now:HH:mm:ss}] {message}";
        try
        {
            lock (_logLock)
            {
                File.AppendAllText(LogFile, line + Environment.NewLine, Encoding.UTF8);
            }
        }
        catch { /* günlük dosyası kritik değil */ }

        try { LogMessage?.Invoke(line); } catch { }
    }

    public static void LogError(string message) => Log("HATA: " + message);
}
