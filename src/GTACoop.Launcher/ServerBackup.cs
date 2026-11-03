using System.IO.Compression;
using System.Text;

namespace GTACoop.Remake;

/// <summary>
/// Sunucu yedeği: sunucu ayarları, Addons klasörü ve sunucuya özel mod
/// klasörlerini tek bir .zip içine alır; başka bir bilgisayarda geri yükler.
///
/// Geri yükleme önce mevcut durumun güvenlik yedeğini alır, sonra dosyaları
/// üzerine yazar. Oyun klasörüne ve kullanıcının kendi modlarına dokunulmaz.
/// </summary>
public static class ServerBackup
{
    private const string InfoName = "sunucu-yedegi.txt";

    public sealed class ImportResult
    {
        public int Files { get; init; }
        public string SafetyPath { get; init; } = "";
    }

    public static string DefaultExportPath()
    {
        var folder = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            folder = AppEnv.DataFolder;
        return Path.Combine(folder, $"GTACoop-Remake-sunucu-{DateTime.Now:yyyyMMdd-HHmmss}.zip");
    }

    public static string Export(string path)
    {
        var temp = Path.Combine(Path.GetTempPath(), "GTACoopServerBackup-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);

        try
        {
            var written = 0;
            if (Directory.Exists(AppEnv.ServerDir))
            {
                FileUtil.CopyTree(AppEnv.ServerDir, Path.Combine(temp, "server"));
                written++;
            }
            if (Directory.Exists(AppEnv.ServersFolder))
            {
                FileUtil.CopyTree(AppEnv.ServersFolder, Path.Combine(temp, "Servers"));
                written++;
            }

            if (written == 0)
                throw new InvalidOperationException("Yedeklenecek sunucu klasörü bulunamadı.");

            var info = new StringBuilder();
            info.AppendLine("GTACoop Remake - sunucu yedeği");
            info.AppendLine("Oluşturma: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            info.AppendLine("Launcher sürümü: " + SelfUpdater.CurrentVersionText);

            var settings = GTACoop.Sync.MyServerSettings.Load(AppEnv.DataFolder);
            info.AppendLine("Sunucu adı: " + settings.ServerName);
            info.AppendLine($"Portlar: oyun {settings.GamePort} UDP / sync {settings.SyncPort} TCP");
            info.AppendLine("Maksimum oyuncu: " + settings.MaxClients);
            info.AppendLine("Not: şifre ve sync token bu dosyada saklanmaz; geri yükledikten sonra " +
                            "Sunucum sekmesinden yeniden girin.");
            info.AppendLine();
            info.AppendLine("Geri yüklemek için: Sunucum > Yedeği Geri Yükle...");
            File.WriteAllText(Path.Combine(temp, InfoName), info.ToString(), new UTF8Encoding(true));

            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);
            if (File.Exists(path)) File.Delete(path);

            ZipFile.CreateFromDirectory(temp, path, CompressionLevel.Optimal, false);
            AppEnv.Log($"Sunucu yedeği alındı: {path}");
            return path;
        }
        finally
        {
            try { Directory.Delete(temp, true); } catch { }
        }
    }

    public static ImportResult Import(string zipPath)
    {
        if (!File.Exists(zipPath))
            throw new FileNotFoundException("Yedek dosyası bulunamadı.", zipPath);

        var temp = Path.Combine(Path.GetTempPath(), "GTACoopServerRestore-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);

        try
        {
            ZipFile.ExtractToDirectory(zipPath, temp, true);

            var serverSrc = Path.Combine(temp, "server");
            var serversSrc = Path.Combine(temp, "Servers");
            if (!Directory.Exists(serverSrc) && !Directory.Exists(serversSrc))
                throw new InvalidDataException(
                    "Bu dosya bir GTACoop sunucu yedeği değil (server/ veya Servers/ klasörü yok).");

            // Önce mevcut durumun güvenlik yedeği: geri alma her zaman mümkün olsun.
            var safetyPath = "";
            try
            {
                var backupDir = Path.Combine(AppEnv.DataFolder, "backup");
                Directory.CreateDirectory(backupDir);
                safetyPath = Path.Combine(backupDir, $"sunucu-{DateTime.Now:yyyyMMdd-HHmmss}.zip");
                Export(safetyPath);
            }
            catch (Exception ex)
            {
                AppEnv.Log("Güvenlik yedeği alınamadı: " + ex.Message);
                safetyPath = "";
            }

            var files = 0;
            if (Directory.Exists(serverSrc))
            {
                Directory.CreateDirectory(AppEnv.ServerDir);
                files += CopyInto(serverSrc, AppEnv.ServerDir);
            }
            if (Directory.Exists(serversSrc))
            {
                Directory.CreateDirectory(AppEnv.ServersFolder);
                files += CopyInto(serversSrc, AppEnv.ServersFolder);
            }

            AppEnv.Log($"Sunucu yedeği geri yüklendi: {files} dosya" +
                       (string.IsNullOrEmpty(safetyPath) ? "" : $", güvenlik yedeği: {safetyPath}"));

            return new ImportResult { Files = files, SafetyPath = safetyPath };
        }
        finally
        {
            try { Directory.Delete(temp, true); } catch { }
        }
    }

    private static int CopyInto(string source, string dest)
    {
        var count = 0;
        Directory.CreateDirectory(dest);
        foreach (var dir in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(Path.Combine(dest, Path.GetRelativePath(source, dir)));

        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(dest, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
            count++;
        }
        return count;
    }
}
