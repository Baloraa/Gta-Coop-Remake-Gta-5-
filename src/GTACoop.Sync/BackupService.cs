using GTACoop.Rpf;

namespace GTACoop.Sync;

/// <summary>Oyun dosyalarının yedeklenmesi ve geri yüklenmesi.</summary>
public static class BackupService
{
    /// <summary>
    /// RPF7 arşivlerinde yeni dosya eklemek için güvenli üst sınır.
    /// 4 GB üzerindeki arşivlerde 24 bit sektör adreslemesi taşar; OpenIV
    /// yeni dosya ekleyemez (yalnızca mevcut dosyaları değiştirebilir).
    /// </summary>
    public const long MaxEditableUpdateRpfBytes = 3_900_000_000L;

    public static string BackupDir(string gameFolder) =>
        Path.Combine(gameFolder, "mods", "GTACoop", "backups");

    /// <summary>
    /// update.rpf düzenlemeye/OpenIV eklemelerine uygun mu?
    /// Boyut (4 GB sınırı), RPF7 başlığı, TOC yapısı ve dlclist okunabilirliği kontrol edilir.
    /// </summary>
    public static bool IsUpdateRpfHealthy(string path, out string reason)
    {
        reason = "";
        try
        {
            if (!File.Exists(path))
            {
                reason = SyncText.T("dosya yok");
                return false;
            }

            var fi = new FileInfo(path);
            if (fi.Length < 0x100)
            {
                reason = SyncText.T("dosya çok küçük / geçersiz");
                return false;
            }

            if (fi.Length > MaxEditableUpdateRpfBytes)
            {
                reason = SyncText.T(
                    "arşiv çok büyük ({0} GB; RPF7 güvenli sınırı 3,9 GB) — OpenIV yeni dosya ekleyemez",
                    (fi.Length / 1_000_000_000.0).ToString("0.00",
                        System.Globalization.CultureInfo.InvariantCulture));
                return false;
            }

            return IsUpdateRpfStructurallyHealthy(path, out reason);
        }
        catch (Exception ex)
        {
            reason = ex.Message;
            return false;
        }
    }

    /// <summary>
    /// Boyut sınırını yok sayarak RPF7 başlığını, TOC yapısını ve dlclist
    /// okunabilirliğini kontrol eder. 4 GB'a yakın ama yapısal olarak sağlıklı
    /// arşivlerde yalnızca yerinde (arşivi büyütmeyen) düzenlemeye izin vermek için.
    /// </summary>
    public static bool IsUpdateRpfStructurallyHealthy(string path, out string reason)
    {
        reason = "";
        try
        {
            if (!File.Exists(path))
            {
                reason = SyncText.T("dosya yok");
                return false;
            }

            var fi = new FileInfo(path);
            if (fi.Length < 0x100)
            {
                reason = SyncText.T("dosya çok küçük / geçersiz");
                return false;
            }

            using var archive = RpfArchive.Open(path);
            if (archive.Find(Dlclist.RelativePath) is not RpfBinaryFileEntry dlclist)
            {
                reason = SyncText.T("common/data/dlclist.xml bulunamadı");
                return false;
            }

            _ = archive.ReadFile(dlclist); // okunabilirlik kontrolü

            var check = archive.VerifyStructure(sampleReads: false);
            if (!check.IsHealthy)
            {
                reason = SyncText.T("RPF7 yapısı sağlıksız");
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            reason = ex.Message;
            return false;
        }
    }

    /// <summary>En yeni sağlıklı update.rpf yedeğini döner (yoksa null).</summary>
    public static string? FindNewestHealthyBackup(string gameFolder, Action<string>? log = null)
    {
        foreach (var backup in ListUpdateRpfBackups(gameFolder))
        {
            if (IsUpdateRpfHealthy(backup, out var why))
                return backup;

            log?.Invoke(SyncText.T("Sağlıksız yedek atlandı: {0} ({1})",
                Path.GetFileName(backup), why));
        }

        return null;
    }

    /// <summary>
    /// mods/update/update.rpf'yi günlük olarak yedekler (aynı gün içinde tek yedek).
    /// Sağlıksız (aşırı büyük/bozuk) dosya yedek olarak yazılmaz; eski sağlıklı
    /// yedeklerin üzerine yazılmaz. Dönen değer: yedek dosya yolu veya null.
    /// </summary>
    public static string? EnsureUpdateRpfBackup(string gameFolder, Action<string>? log = null)
    {
        var src = Path.Combine(gameFolder, "mods", "update", "update.rpf");
        if (!File.Exists(src))
        {
            log?.Invoke(SyncText.T("UYARI: mods/update/update.rpf bulunamadı; dlclist düzenlemesi yapılamaz."));
            return null;
        }

        var dir = BackupDir(gameFolder);
        Directory.CreateDirectory(dir);
        var stamp = DateTime.Now.ToString("yyyyMMdd");
        var target = Path.Combine(dir, $"update.rpf.{stamp}.bak");

        var currentHealthy = IsUpdateRpfHealthy(src, out var currentWhy);

        if (File.Exists(target))
        {
            // Günün yedeği bozuksa (ör. 4 GB üzeri bir dosyadan alınmışsa) ve mevcut
            // dosya sağlıklıysa yedeği sağlıklı içerikle yenile.
            if (currentHealthy && !IsUpdateRpfHealthy(target, out var backupWhy))
            {
                log?.Invoke(SyncText.T("Mevcut günlük yedek sağlıksız ({0}); sağlıklı içerikle yenileniyor...",
                    backupWhy));
                var tmp = target + ".tmp";
                File.Copy(src, tmp, overwrite: true);
                File.Move(tmp, target, overwrite: true);
                log?.Invoke(SyncText.T("Yedek yenilendi."));
            }
            else
            {
                log?.Invoke(SyncText.T("Yedek zaten var: {0}", target));
            }
        }
        else if (!currentHealthy)
        {
            log?.Invoke(SyncText.T("UYARI: update.rpf sağlıksız ({0}); yedek alınmadı, " +
                                   "eski sağlıklı yedek(ler) korunuyor.", currentWhy));
        }
        else
        {
            log?.Invoke(SyncText.T("update.rpf yedekleniyor -> {0} ...", Path.GetFileName(target)));
            var tmp = target + ".tmp";
            File.Copy(src, tmp, overwrite: true);
            File.Move(tmp, target, overwrite: true);
            log?.Invoke(SyncText.T("Yedek alındı."));
        }

        Rotate(dir, keep: 3, log);
        return target;
    }

    /// <summary>
    /// Yerinde dlclist düzenlemesinden önce yalnızca XML içeriğini saklar.
    /// 4 GB'a yakın update.rpf'nin tamamını kopyalamadan geri dönüş imkânı verir.
    /// </summary>
    public static string BackupDlclist(string gameFolder, byte[] content, Action<string>? log = null)
    {
        var dir = Path.Combine(BackupDir(gameFolder), "dlclist");
        Directory.CreateDirectory(dir);
        var target = Path.Combine(dir, $"dlclist-{DateTime.Now:yyyyMMdd-HHmmssfff}.xml");
        File.WriteAllBytes(target, content);
        log?.Invoke(SyncText.T("dlclist yedeği alındı: {0}", Path.GetFileName(target)));

        try
        {
            foreach (var old in Directory.GetFiles(dir, "dlclist-*.xml")
                         .OrderByDescending(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase)
                         .Skip(5))
            {
                try { File.Delete(old); } catch { }
            }
        }
        catch { /* rotasyon kritik değil */ }

        return target;
    }

    private static void Rotate(string dir, int keep, Action<string>? log)
    {
        try
        {
            // Dosya adındaki tarih (yyyyMMdd) sıralaması, kopyalama mtime'ından daha güvenilir.
            var backups = Directory.GetFiles(dir, "update.rpf.*.bak")
                .OrderByDescending(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var old in backups.Skip(keep))
            {
                File.Delete(old);
                log?.Invoke(SyncText.T("Eski yedek silindi: {0}", Path.GetFileName(old)));
            }
        }
        catch { /* rotasyon kritik değil */ }
    }

    public static List<string> ListUpdateRpfBackups(string gameFolder)
    {
        var dir = BackupDir(gameFolder);
        if (!Directory.Exists(dir)) return new List<string>();
        return Directory.GetFiles(dir, "update.rpf.*.bak")
            .OrderByDescending(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>Yedekten update.rpf'yi geri yükler (oyun kapalı olmalı).</summary>
    public static void RestoreUpdateRpf(string gameFolder, string backupPath, Action<string>? log = null)
    {
        var target = Path.Combine(gameFolder, "mods", "update", "update.rpf");
        if (!File.Exists(backupPath))
            throw new SyncException(SyncText.T("Yedek bulunamadı: {0}", backupPath));

        if (!IsUpdateRpfHealthy(backupPath, out var why))
            throw new SyncException(SyncText.T(
                "Yedek düzenlemeye uygun değil: {0} ({1})",
                Path.GetFileName(backupPath), why));

        log?.Invoke(SyncText.T("Geri yükleniyor: {0} -> mods/update/update.rpf",
            Path.GetFileName(backupPath)));

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

        if (!IsUpdateRpfHealthy(target, out var after))
            throw new SyncException(SyncText.T(
                "Geri yükleme sonrası update.rpf sağlıksız ({0}). " +
                "Önceki dosya 'update.rpf.before-restore' olarak korunuyor.", after));

        log?.Invoke(SyncText.T("Geri yükleme tamamlandı."));
    }
}
