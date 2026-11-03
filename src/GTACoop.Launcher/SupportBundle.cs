using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Windows.Forms;
using GTACoop.Sync;

namespace GTACoop.Remake;

/// <summary>
/// Sorun bildirirken istenen her şeyi tek bir .zip içinde toplar: sürüm ve
/// sistem bilgisi, son launcher günlükleri, oyun tarafı günlükleri
/// (ScriptHookVDotNet / asiloader / GTACoOp) ve parolasız ayar özeti.
///
/// Parola, sync token gibi gizli alanlar pakete hiç yazılmaz.
/// </summary>
public static class SupportBundle
{
    private static readonly string[] GameLogs =
    {
        "ScriptHookVDotNet.log", "asiloader.log", "ScriptHookV.log", "OpenIV.log"
    };

    public static string Create()
    {
        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var folder = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            folder = AppEnv.DataFolder;

        var path = Path.Combine(folder, $"GTACoop-Remake-destek-{stamp}.zip");
        var temp = Path.Combine(Path.GetTempPath(), "GTACoopRemakeSupport-" + stamp);
        Directory.CreateDirectory(temp);

        try
        {
            File.WriteAllText(Path.Combine(temp, "bilgi.txt"), BuildInfo(), new UTF8Encoding(true));
            File.WriteAllText(Path.Combine(temp, "ayarlar.json"), SafeSettings(), new UTF8Encoding(true));

            CopyLauncherLogs(temp);
            CopyGameLogs(temp);
            CopyServerSummary(temp);

            File.WriteAllText(Path.Combine(temp, "OKUBENI.txt"),
                "Bu paket GTACoop Remake launcher tarafından sorun bildirimi için oluşturuldu.\r\n" +
                "Parola ve sync token gibi gizli alanlar pakete eklenmez.\r\n" +
                "bilgi.txt: sürüm, sistem ve oyun bilgisi\r\n" +
                "launcher-*.log: launcher günlükleri (son 3 dosya)\r\n" +
                "oyun-*.log: ScriptHookVDotNet / asiloader günlükleri\r\n",
                new UTF8Encoding(true));

            ZipFile.CreateFromDirectory(temp, path, CompressionLevel.Optimal, false);
        }
        finally
        {
            try { Directory.Delete(temp, true); } catch { }
        }

        AppEnv.Log("Destek paketi oluşturuldu: " + path);
        return path;
    }

    private static void CopyLauncherLogs(string temp)
    {
        try
        {
            var logs = Path.Combine(AppEnv.DataFolder, "logs");
            if (!Directory.Exists(logs)) return;
            foreach (var file in Directory.GetFiles(logs, "launcher-*.log")
                         .OrderByDescending(File.GetLastWriteTimeUtc).Take(3))
            {
                try { CopyShared(file, Path.Combine(temp, Path.GetFileName(file))); }
                catch (Exception ex) { AppEnv.Log("Günlük kopyalanamadı: " + ex.Message); }
            }
        }
        catch (Exception ex)
        {
            AppEnv.Log("Launcher günlükleri toplanamadı: " + ex.Message);
        }
    }

    private static void CopyGameLogs(string temp)
    {
        var game = AppEnv.Settings.GameFolder;
        if (string.IsNullOrWhiteSpace(game) || !Directory.Exists(game)) return;

        foreach (var name in GameLogs)
        {
            var src = Path.Combine(game, name);
            if (!File.Exists(src)) continue;
            try { CopyShared(src, Path.Combine(temp, "oyun-" + name)); }
            catch (Exception ex) { AppEnv.Log($"'{name}' kopyalanamadı: {ex.Message}"); }
        }

        var coopLog = Path.Combine(game, "scripts", "GTACoOp.log");
        if (File.Exists(coopLog))
        {
            try { CopyShared(coopLog, Path.Combine(temp, "oyun-GTACoOp.log")); }
            catch (Exception ex) { AppEnv.Log("GTACoOp.log kopyalanamadı: " + ex.Message); }
        }
    }

    private static void CopyServerSummary(string temp)
    {
        try
        {
            var sb = new StringBuilder();
            var settings = MyServerSettings.Load(AppEnv.DataFolder);
            sb.AppendLine("Sunucu adı: " + settings.ServerName);
            sb.AppendLine($"Portlar: oyun {settings.GamePort} UDP / sync {settings.SyncPort} TCP");
            sb.AppendLine("Maksimum oyuncu: " + settings.MaxClients);
            sb.AppendLine("Şifre: " + (string.IsNullOrEmpty(settings.Password) ? "kapalı" : "açık"));
            sb.AppendLine("Kısa kod: " + (string.IsNullOrWhiteSpace(settings.ShortCode) ? "kapalı" : "açık"));
            sb.AppendLine("Relay sunucusu: " +
                          (string.IsNullOrWhiteSpace(settings.RelayServer) ? "bore.pub" : settings.RelayServer));
            sb.AppendLine("Devre dışı mod sayısı: " + settings.DisabledMods.Count);

            var addons = Path.Combine(AppEnv.ServerDir, "Addons");
            if (Directory.Exists(addons))
            {
                sb.AppendLine();
                sb.AppendLine("--- Addons klasörü ---");
                foreach (var dir in Directory.GetDirectories(addons))
                    sb.AppendLine("dizin: " + Path.GetFileName(dir));
                foreach (var file in Directory.GetFiles(addons))
                    sb.AppendLine("dosya: " + Path.GetFileName(file));
            }

            File.WriteAllText(Path.Combine(temp, "sunucu.txt"), sb.ToString(), new UTF8Encoding(true));
        }
        catch (Exception ex)
        {
            AppEnv.Log("Sunucu özeti hazırlanamadı: " + ex.Message);
        }
    }

    private static string BuildInfo()
    {
        var sb = new StringBuilder();
        sb.AppendLine("GTACoop Remake - destek paketi");
        sb.AppendLine("Oluşturma: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        sb.AppendLine("Launcher sürümü: " + Application.ProductVersion);
        sb.AppendLine("Kurulu sürüm: " + SelfUpdater.CurrentVersionText);
        sb.AppendLine("İşletim sistemi: " + RuntimeInformation.OSDescription +
                      (Environment.Is64BitOperatingSystem ? " (64-bit)" : " (32-bit)"));
        sb.AppendLine(".NET: " + Environment.Version);
        sb.AppendLine("Dil: " + Loc.LanguageName);
        sb.AppendLine("İşlemci sayısı: " + Environment.ProcessorCount);

        var game = AppEnv.Settings.GameFolder;
        sb.AppendLine("GTA klasörü: " + (string.IsNullOrWhiteSpace(game) ? "(seçilmemiş)" : game));
        sb.AppendLine("Çalıştırma dosyası: " +
                      (string.IsNullOrWhiteSpace(game)
                          ? "(yok)"
                          : GameFolder.ResolveLaunchExe(game, AppEnv.Settings.LaunchExe)));

        if (!string.IsNullOrWhiteSpace(game) && Directory.Exists(game))
        {
            sb.AppendLine();
            sb.AppendLine("--- oyun klasörü ---");
            try
            {
                var s = GameFolder.Inspect(game);
                sb.AppendLine("GTA5.exe: " + (s.HasGameExe ? "var" : "yok"));
                sb.AppendLine("GTA5_Enhanced.exe: " + (s.HasEnhancedExe ? "var" : "yok"));
                sb.AppendLine("ScriptHookV.dll: " + (s.HasScriptHookV ? "var" : "yok"));
                sb.AppendLine("ScriptHookVDotNet.asi: " + (s.HasShvdn ? "var" : "yok"));
                sb.AppendLine("OpenIV.asi / RageOpenV.asi: " + (s.HasOpenIvAsi ? "var" : "yok"));
                sb.AppendLine("mods/update/update.rpf: " +
                              (s.HasModsUpdateRpf ? s.ModsUpdateRpfEncryption.ToString() : "yok"));
                sb.AppendLine("scripts klasörü: " + (s.HasScriptsFolder ? "var" : "yok"));
                sb.AppendLine("GTACoOp.dll: " + (s.HasGtaCoopClient ? "kurulu" : "kurulu değil"));
            }
            catch (Exception ex)
            {
                sb.AppendLine("Oyun klasörü okunamadı: " + ex.Message);
            }
        }

        sb.AppendLine();
        sb.AppendLine("--- yerel adresler ---");
        try
        {
            var ips = LocalAddresses.Detect().Select(a => a.Ip).ToList();
            sb.AppendLine(ips.Count == 0 ? "(yok)" : string.Join(", ", ips));
        }
        catch
        {
            sb.AppendLine("(okunamadı)");
        }

        sb.AppendLine();
        sb.AppendLine("--- kurulu sunucu modları ---");
        try
        {
            var states = SyncStateStore.LoadAll(AppEnv.DataFolder);
            if (states.Count == 0) sb.AppendLine("(yok)");
            foreach (var state in states)
                sb.AppendLine($"{(string.IsNullOrWhiteSpace(state.ServerName) ? state.ServerId : state.ServerName)}: {state.Addons.Count} mod");
        }
        catch (Exception ex)
        {
            sb.AppendLine("Okunamadı: " + ex.Message);
        }

        return sb.ToString();
    }

    /// <summary>Parola ve token içermeyen ayar özeti.</summary>
    private static string SafeSettings()
    {
        try
        {
            var settings = AppEnv.Settings;
            var safe = new
            {
                username = settings.Username,
                language = settings.Language,
                removeModsOnExit = settings.RemoveModsOnExit,
                lowDesyncProfile = settings.LowDesyncProfile,
                launchExe = settings.LaunchExe,
                serverBrowserMasterUrl = settings.ServerBrowserMasterUrl,
                servers = settings.Servers.Select(s => new
                {
                    s.Name,
                    s.Host,
                    s.GamePort,
                    s.SyncPort,
                    s.Favorite,
                    hasPassword = !string.IsNullOrEmpty(s.Password),
                    s.ShortCode,
                    s.RelayHost,
                    s.RelayGamePort,
                    s.RelaySyncPort
                })
            };
            return JsonSerializer.Serialize(safe, SyncJson.Options);
        }
        catch (Exception ex)
        {
            return "{ \"error\": \"" + ex.Message.Replace("\"", "'") + "\" }";
        }
    }

    /// <summary>Oyun çalışırken kilitli olabilecek günlükleri paylaşımlı okur.</summary>
    private static void CopyShared(string source, string dest)
    {
        using var input = new FileStream(source, FileMode.Open, FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        using var output = File.Create(dest);
        input.CopyTo(output);
    }
}
