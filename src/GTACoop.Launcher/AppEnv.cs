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

        Settings = SyncJson.Load<LauncherSettings>(SettingsPath) ?? new LauncherSettings();
        Settings.DataFolder = DataFolder;

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

    public static void Log(string message)
    {
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
