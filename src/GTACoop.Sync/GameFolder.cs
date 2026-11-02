using GTACoop.Rpf;
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
            HasPlayGtaVExe = File.Exists(Path.Combine(folder, "PlayGTAV.exe")),
            HasScriptHookV = File.Exists(Path.Combine(folder, "ScriptHookV.dll")),
            HasShvdn = File.Exists(Path.Combine(folder, "ScriptHookVDotNet.asi")),
            // RageOpenV, OpenIV.asi'nin açık kaynak alternatifidir; ikisi de mods/ köprüsünü açar.
            HasOpenIvAsi = File.Exists(Path.Combine(folder, "OpenIV.asi")) ||
                           File.Exists(Path.Combine(folder, "RageOpenV.asi")),
            HasRageOpenVAsi = File.Exists(Path.Combine(folder, "RageOpenV.asi")),
            HasModsFolder = Directory.Exists(Path.Combine(folder, "mods")),
            HasModsUpdateRpf = File.Exists(Path.Combine(folder, "mods", "update", "update.rpf")),
            ModsUpdateRpfEncryption = ProbeArchiveEncryption(
                Path.Combine(folder, "mods", "update", "update.rpf")),
            IsEnhanced = File.Exists(Path.Combine(folder, "GTA5_Enhanced.exe")) ||
                         ProbeArchiveEncryption(Path.Combine(folder, "update", "update.rpf")) ==
                         RpfEncryption.Ng,
            HasScriptsFolder = Directory.Exists(Path.Combine(folder, "scripts")),
            HasGtaCoopClient = File.Exists(Path.Combine(folder, "scripts", "GTACoOp.dll"))
        };

    /// <summary>
    /// Bir RPF arşivinin başlık şifrelemesini okur. null: dosya yok, çok küçük veya RPF değil.
    /// Gen9 (GTA V Enhanced) arşivleri <see cref="RpfEncryption.Ng"/> döner; bunlar yazılamaz.
    /// </summary>
    public static RpfEncryption? ProbeArchiveEncryption(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            if (fs.Length < 16) return null;
            var header = new byte[16];
            fs.ReadExactly(header, 0, 16);
            if (BitConverter.ToUInt32(header, 0) != RpfArchive.Magic) return null;
            var encryption = (RpfEncryption)BitConverter.ToUInt32(header, 12);
            // "None" yazan arşivler OpenIV'nin şifresiz (OPEN) biçimidir.
            return encryption == RpfEncryption.None ? RpfEncryption.Open : encryption;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Oyun GTA V Enhanced (Gen9) mi?</summary>
    public static bool IsEnhanced(string folder) =>
        File.Exists(Path.Combine(folder, "GTA5_Enhanced.exe")) ||
        ProbeArchiveEncryption(Path.Combine(folder, "update", "update.rpf")) == RpfEncryption.Ng;

    /// <summary>Günlük için tek satırlık oyun sürümü + mods arşivi özeti.</summary>
    public static string DescribeModsArchive(string folder)
    {
        var edition = SyncText.T(IsEnhanced(folder) ? "GTA V Enhanced" : "GTA V Legacy");
        var path = Path.Combine(folder, "mods", "update", "update.rpf");
        if (!File.Exists(path))
            return SyncText.T(
                "Oyun: {0}; mods/update/update.rpf: yok. Addon DLC paketleri kurulamaz, " +
                "diğer modlar kurulur. Launcher oyun dosyalarını kopyalamaz veya değiştirmez.",
                edition);

        var state = ProbeArchiveEncryption(path) switch
        {
            RpfEncryption.Open => "OPEN",
            RpfEncryption.Aes => "AES",
            RpfEncryption.Ng => "NG (Gen9)",
            _ => "?"
        };
        return SyncText.T("Oyun: {0}; mods/update/update.rpf: {1}.", edition, state);
    }

    /// <summary>
    /// Varsayılan başlatma dosyası: önce PlayGTAV.exe, sonra Enhanced, sonra klasik GTA5.exe.
    /// </summary>
    public static string DefaultLaunchExe(string folder)
    {
        var play = Path.Combine(folder, "PlayGTAV.exe");
        if (File.Exists(play)) return play;

        var enhanced = Path.Combine(folder, "GTA5_Enhanced.exe");
        if (File.Exists(enhanced)) return enhanced;

        return Path.Combine(folder, "GTA5.exe");
    }

    /// <summary>
    /// Ayarlanmış başlatma dosyasını çözer. Dosya yoksa null döner.
    /// Göreli yol oyun klasörüne göre yorumlanır.
    /// </summary>
    public static string? ResolveConfiguredLaunchExe(string folder, string? configured)
    {
        if (string.IsNullOrWhiteSpace(configured)) return null;

        try
        {
            var path = Path.IsPathRooted(configured)
                ? configured
                : Path.Combine(folder, configured);
            return File.Exists(path) ? Path.GetFullPath(path) : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Ayarlanmış dosya yoksa varsayılana döner.</summary>
    public static string ResolveLaunchExe(string folder, string? configured) =>
        ResolveConfiguredLaunchExe(folder, configured) ?? DefaultLaunchExe(folder);
}

public sealed class GameFolderStatus
{
    public string Folder { get; set; } = "";
    public bool HasGameExe;
    public bool HasEnhancedExe;
    public bool HasPlayGtaVExe;
    public bool HasScriptHookV;
    public bool HasShvdn;
    public bool HasOpenIvAsi;
    public bool HasRageOpenVAsi;
    public bool HasModsFolder;
    public bool HasModsUpdateRpf;
    public bool IsEnhanced;
    public RpfEncryption? ModsUpdateRpfEncryption;
    public bool HasScriptsFolder;
    public bool HasGtaCoopClient;

    public bool IsUsable => HasGameExe && HasScriptsFolder;
    public bool IsModReady => IsUsable && HasScriptHookV && HasShvdn && HasOpenIvAsi;

    /// <summary>mods kopyası yazılabilir mi? NG/Gen9 arşivler yazılamaz.</summary>
    public bool CanEditModsArchive =>
        HasModsUpdateRpf &&
        ModsUpdateRpfEncryption is RpfEncryption.Open or RpfEncryption.Aes;

    public override string ToString() =>
        $"GTA5.exe: {HasGameExe}, PlayGTAV.exe: {HasPlayGtaVExe}, " +
        $"ScriptHookV: {HasScriptHookV}, SHVDN: {HasShvdn}, " +
        $"OpenIV.asi: {HasOpenIvAsi}, mods/update.rpf: {HasModsUpdateRpf} " +
        $"({ModsUpdateRpfEncryption}), Enhanced: {IsEnhanced}, " +
        $"GTACoOp.dll: {HasGtaCoopClient}";
}
