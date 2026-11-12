namespace GTACoop.Sync;

/// <summary>
/// Sunucunun dünya (saat) ayarını istemcinin okuyacağı dosyaya yazar.
/// GTACoOp istemcisi scripts\GTACoop-world.json dosyasını okuyup oyun saatini
/// ayarlar; böylece ağ protokolü değişmeden sunucu saati paylaşılır.
/// </summary>
public static class WorldConfig
{
    public const string FileName = "GTACoop-world.json";
    private const string BackupSuffix = ".gtacoop-backup";

    public static void Write(string gameFolder, Manifest manifest)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(gameFolder)) return;

            var scripts = Path.Combine(gameFolder, "scripts");
            Directory.CreateDirectory(scripts);
            var path = Path.Combine(scripts, FileName);
            var backup = path + BackupSuffix;
            if (File.Exists(path) && !File.Exists(backup))
                File.Copy(path, backup, overwrite: false);

            SyncJson.Save(path, new WorldSettings
            {
                ServerName = manifest.ServerName,
                TimeMode = string.IsNullOrWhiteSpace(manifest.TimeMode) ? "off" : manifest.TimeMode,
                TimeHour = Math.Min(23, Math.Max(0, manifest.TimeHour)),
                Weather = string.IsNullOrWhiteSpace(manifest.Weather) ? "dynamic" : manifest.Weather,
                GeneratedUtc = DateTime.UtcNow
            });
        }
        catch
        {
            // Saat ayarı isteğe bağlıdır; yazılamazsa oyun normal saatinde kalır.
        }
    }

    /// <summary>Oturum kapanırken istemci ayarını kaldırır (saat zorlaması biter).</summary>
    public static void Remove(string gameFolder)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(gameFolder)) return;
            var path = Path.Combine(gameFolder, "scripts", FileName);
            var backup = path + BackupSuffix;
            if (File.Exists(backup))
            {
                File.Move(backup, path, overwrite: true);
            }
            else if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch { }
    }
}
