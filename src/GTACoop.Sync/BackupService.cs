namespace GTACoop.Sync;

/// <summary>Oyun dosyalarının yedeklenmesi ve geri yüklenmesi.</summary>
public static class BackupService
{
    public static string BackupDir(string gameFolder) =>
        Path.Combine(gameFolder, "mods", "GTACoop", "backups");

    /// <summary>
    /// mods/update/update.rpf'yi günlük olarak yedekler (aynı gün içinde tek yedek).
    /// Dönen değer: yedek dosya yolu veya null (dosya yoksa).
    /// </summary>
    public static string? EnsureUpdateRpfBackup(string gameFolder, Action<string>? log = null)
    {
        var src = Path.Combine(gameFolder, "mods", "update", "update.rpf");
        if (!File.Exists(src))
        {
            log?.Invoke("UYARI: mods/update/update.rpf bulunamadı; dlclist düzenlemesi yapılamaz.");
            return null;
        }

        var dir = BackupDir(gameFolder);
        Directory.CreateDirectory(dir);
        var stamp = DateTime.Now.ToString("yyyyMMdd");
        var target = Path.Combine(dir, $"update.rpf.{stamp}.bak");

        if (File.Exists(target))
        {
            log?.Invoke($"Yedek zaten var: {target}");
        }
        else
        {
            log?.Invoke($"update.rpf yedekleniyor -> {Path.GetFileName(target)} ...");
            var tmp = target + ".tmp";
            File.Copy(src, tmp, overwrite: true);
            File.Move(tmp, target, overwrite: true);
            log?.Invoke("Yedek alındı.");
        }

        Rotate(dir, keep: 3, log);
        return target;
    }

    private static void Rotate(string dir, int keep, Action<string>? log)
    {
        try
        {
            var backups = Directory.GetFiles(dir, "update.rpf.*.bak")
                .Select(f => new FileInfo(f))
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .ToList();

            foreach (var old in backups.Skip(keep))
            {
                old.Delete();
                log?.Invoke($"Eski yedek silindi: {old.Name}");
            }
        }
        catch { /* rotasyon kritik değil */ }
    }

    public static List<string> ListUpdateRpfBackups(string gameFolder)
    {
        var dir = BackupDir(gameFolder);
        if (!Directory.Exists(dir)) return new List<string>();
        return Directory.GetFiles(dir, "update.rpf.*.bak")
            .OrderByDescending(f => File.GetLastWriteTimeUtc(f))
            .ToList();
    }

    /// <summary>Yedekten update.rpf'yi geri yükler (oyun kapalı olmalı).</summary>
    public static void RestoreUpdateRpf(string gameFolder, string backupPath, Action<string>? log = null)
    {
        var target = Path.Combine(gameFolder, "mods", "update", "update.rpf");
        if (!File.Exists(backupPath))
            throw new SyncException($"Yedek bulunamadı: {backupPath}");

        log?.Invoke($"Geri yükleniyor: {Path.GetFileName(backupPath)} -> mods/update/update.rpf");

        // Mevcut dosyayı da bir kenara al (son çare)
        if (File.Exists(target))
        {
            var prior = target + ".before-restore";
            try
            {
                if (File.Exists(prior)) File.Delete(prior);
                File.Move(target, prior);
            }
            catch { /* devam: doğrudan üzerine yazılacak */ }
        }

        File.Copy(backupPath, target, overwrite: true);
        log?.Invoke("Geri yükleme tamamlandı.");
    }
}
