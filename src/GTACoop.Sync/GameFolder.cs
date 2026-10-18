using Microsoft.Win32;

namespace GTACoop.Sync;

/// <summary>GTA V klasörü tespiti ve doğrulaması.</summary>
public static class GameFolder
{
    /// <summary>Bilinen yöntemlerle GTA V klasörünü bulmayı dener.</summary>
    public static string? TryDetect()
    {
        // 1) Rockstar kayıt defteri
        foreach (var hive in new[] { RegistryHive.LocalMachine, RegistryHive.CurrentUser })
        {
            foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
            {
                try
                {
                    using var baseKey = RegistryKey.OpenBaseKey(hive, view);
                    using var key = baseKey.OpenSubKey(
                        @"SOFTWARE\WOW6432Node\Rockstar Games\Grand Theft Auto V")
                        ?? baseKey.OpenSubKey(@"SOFTWARE\Rockstar Games\Grand Theft Auto V");
                    var path = key?.GetValue("InstallFolder") as string;
                    if (IsValid(path)) return path!;
                }
                catch { /* yok say */ }
            }
        }

        // 2) Steam kütüphaneleri
        try
        {
            using var steamKey = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
            var steamPath = steamKey?.GetValue("SteamPath") as string;
            if (!string.IsNullOrEmpty(steamPath))
            {
                var direct = Path.Combine(steamPath, "steamapps", "common", "Grand Theft Auto V");
                if (IsValid(direct)) return direct;

                var vdf = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
                if (File.Exists(vdf))
                {
                    foreach (var line in File.ReadAllLines(vdf))
                    {
                        var t = line.Trim();
                        // "path"		"E:\\SteamLibrary"
                        if (t.StartsWith("\"path\"", StringComparison.OrdinalIgnoreCase))
                        {
                            var parts = t.Split('"', StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length >= 4)
                            {
                                var lib = parts[3].Replace("\\\\", "\\");
                                var candidate = Path.Combine(lib, "steamapps", "common", "Grand Theft Auto V");
                                if (IsValid(candidate)) return candidate;
                            }
                        }
                    }
                }
            }
        }
        catch { /* yok say */ }

        // 3) Yaygın kurulum yerleri
        foreach (var drive in new[] { "C:", "D:", "E:", "F:", "G:" })
        {
            foreach (var rel in new[]
            {
                @"Program Files\Rockstar Games\Grand Theft Auto V",
                @"Program Files (x86)\Rockstar Games\Grand Theft Auto V",
                @"SteamLibrary\steamapps\common\Grand Theft Auto V",
                @"Games\Grand Theft Auto V",
                @"GTAV"
            })
            {
                var candidate = Path.Combine(drive + "\\", rel);
                if (IsValid(candidate)) return candidate;
            }
        }

        return null;
    }

    /// <summary>Klasör geçerli bir GTA V kurulumu mu?</summary>
    public static bool IsValid(string? folder) =>
        !string.IsNullOrWhiteSpace(folder) &&
        (File.Exists(Path.Combine(folder, "GTA5.exe")) ||
         File.Exists(Path.Combine(folder, "GTA5_Enhanced.exe")));

    /// <summary>GTA V ile ilgili bileşenlerin durumu.</summary>
    public static GameFolderStatus Inspect(string folder) =>
        new()
        {
            Folder = folder,
            HasGameExe = File.Exists(Path.Combine(folder, "GTA5.exe")),
            HasEnhancedExe = File.Exists(Path.Combine(folder, "GTA5_Enhanced.exe")),
            HasScriptHookV = File.Exists(Path.Combine(folder, "ScriptHookV.dll")),
            HasShvdn = File.Exists(Path.Combine(folder, "ScriptHookVDotNet.asi")),
            HasOpenIvAsi = File.Exists(Path.Combine(folder, "OpenIV.asi")),
            HasModsFolder = Directory.Exists(Path.Combine(folder, "mods")),
            HasModsUpdateRpf = File.Exists(Path.Combine(folder, "mods", "update", "update.rpf")),
            HasScriptsFolder = Directory.Exists(Path.Combine(folder, "scripts")),
            HasGtaCoopClient = File.Exists(Path.Combine(folder, "scripts", "GTACoOp.dll"))
        };

    /// <summary>Başlatılacak oyun exe yolu (varsa Enhanced, yoksa klasik).</summary>
    public static string GameExe(string folder)
    {
        var enhanced = Path.Combine(folder, "GTA5_Enhanced.exe");
        return File.Exists(enhanced) ? enhanced : Path.Combine(folder, "GTA5.exe");
    }
}

public sealed class GameFolderStatus
{
    public string Folder { get; set; } = "";
    public bool HasGameExe;
    public bool HasEnhancedExe;
    public bool HasScriptHookV;
    public bool HasShvdn;
    public bool HasOpenIvAsi;
    public bool HasModsFolder;
    public bool HasModsUpdateRpf;
    public bool HasScriptsFolder;
    public bool HasGtaCoopClient;

    public bool IsUsable => HasGameExe && HasScriptsFolder;
    public bool IsModReady => IsUsable && HasScriptHookV && HasShvdn && HasOpenIvAsi;

    public override string ToString() =>
        $"GTA5.exe: {HasGameExe}, ScriptHookV: {HasScriptHookV}, SHVDN: {HasShvdn}, " +
        $"OpenIV.asi: {HasOpenIvAsi}, mods/update.rpf: {HasModsUpdateRpf}, " +
        $"GTACoOp.dll: {HasGtaCoopClient}";
}
