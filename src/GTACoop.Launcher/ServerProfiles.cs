using System.Windows.Forms;
using GTACoop.Sync;

namespace GTACoop.Remake;

/// <summary>
/// Sunucu profilleri: server.json dosyasının adlandırılmış kopyaları.
/// Sınırsız sayıda profil tutulabilir (RP, freeroam, test...). Profil
/// değiştirmek mevcut ayarları önce profile yazar, sonra seçileni yükler.
/// </summary>
internal static class ServerProfiles
{
    public static string Folder => Path.Combine(AppEnv.DataFolder, "profiles");

    public static List<string> List()
    {
        try
        {
            if (!Directory.Exists(Folder)) return new List<string>();
            return Directory.GetFiles(Folder, "*.json")
                .Select(Path.GetFileNameWithoutExtension)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Select(n => n!)
                .OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }
        catch { return new List<string>(); }
    }

    /// <summary>Şu anki sunucu ayarlarını verilen ada kaydeder.</summary>
    public static void SaveCurrent(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return;
        Directory.CreateDirectory(Folder);
        var source = MyServerSettings.SettingsPath(AppEnv.DataFolder);
        if (!File.Exists(source)) return;
        File.Copy(source, PathFor(name), overwrite: true);
    }

    /// <summary>Profildeki ayarları etkin sunucu ayarlarına yazar.</summary>
    public static bool Apply(string name)
    {
        var file = PathFor(name);
        if (!File.Exists(file)) return false;
        File.Copy(file, MyServerSettings.SettingsPath(AppEnv.DataFolder), overwrite: true);
        return true;
    }

    public static void Delete(string name)
    {
        try { File.Delete(PathFor(name)); } catch { }
    }

    /// <summary>Boş olmayan, benzersiz bir profil adı üretir.</summary>
    public static string UniqueName(string baseName)
    {
        var name = string.IsNullOrWhiteSpace(baseName) ? "Profil" : baseName.Trim();
        var existing = List();
        if (!existing.Contains(name, StringComparer.CurrentCultureIgnoreCase)) return name;
        for (var i = 2; i < 999; i++)
        {
            var candidate = name + " " + i;
            if (!existing.Contains(candidate, StringComparer.CurrentCultureIgnoreCase)) return candidate;
        }
        return name + " " + Guid.NewGuid().ToString("N")[..4];
    }

    private static string PathFor(string name)
    {
        var safe = string.Concat(name.Where(c => !Path.GetInvalidFileNameChars().Contains(c))).Trim();
        if (string.IsNullOrWhiteSpace(safe)) safe = "Profil";
        return Path.Combine(Folder, safe + ".json");
    }

    /// <summary>Launcher'ı yeniden başlatır (profil değişikliği uygulanmış olsun).</summary>
    public static void RestartLauncher()
    {
        try
        {
            var exe = Environment.ProcessPath;
            if (!string.IsNullOrWhiteSpace(exe))
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(exe)
                {
                    UseShellExecute = true,
                    WorkingDirectory = Path.GetDirectoryName(exe)!
                });
        }
        catch { }
        Application.Exit();
    }
}
