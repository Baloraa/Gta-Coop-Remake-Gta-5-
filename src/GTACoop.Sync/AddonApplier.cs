using System.Diagnostics;
using System.Text;
using GTACoop.Rpf;

namespace GTACoop.Sync;

/// <summary>
/// Sunucu eklentilerini oyun klasörüne kurar/kaldırır.
/// Tüm değişiklikler: dlclist (update.rpf), dlcpacks/&lt;gcs_*&gt;, scripts/, mods/GTACoop/.
/// Orijinal oyun dosyalarına (update.rpf kökü dışında) dokunulmaz.
/// </summary>
public sealed class AddonApplier
{
    private readonly string _gameFolder;
    private readonly Action<string> _log;
    private static readonly object _rpfLock = new();

    public AddonApplier(string gameFolder, Action<string>? log = null)
    {
        _gameFolder = gameFolder;
        _log = log ?? (_ => { });
    }

    public string GameFolder => _gameFolder;

    // ------------------------------------------------------------------ plan

    public sealed class InstallPlan
    {
        public List<AddonInfo> ToInstall { get; } = new();
        public List<InstalledAddon> ToRemove { get; } = new();
        public List<AddonInfo> UpToDate { get; } = new();

        /// <summary>Kullanıcının bilgisayarında zaten olduğu için kurulmayacak eklentiler.</summary>
        public List<SkippedAddon> LocalSkipped { get; } = new();
    }

    /// <summary>Atlama nedeni: hedefte zaten var olan dosya/klasör.</summary>
    public sealed class SkippedAddon
    {
        public AddonInfo Addon { get; set; } = null!;
        public string Reason { get; set; } = "";
    }

    /// <summary>Manifest ile yerel durumu karşılaştırıp kurulum planı çıkarır.</summary>
    public InstallPlan BuildPlan(Manifest manifest, SyncState state)
    {
        var plan = new InstallPlan();

        foreach (var addon in manifest.Addons)
        {
            if (addon.Type == AddonType.ServerPlugin) continue; // istemciye kurulmaz

            var version = ServerScanner.ComputeVersion(addon);
            var installed = state.Addons.FirstOrDefault(a => a.Id == addon.Id);

            if (installed == null || installed.Version != version || !IsInstalledIntact(installed))
            {
                // Kullanıcı bu moda zaten sahipse (kendi kurulumu) kabul etme, üzerine yazma.
                var conflict = LocalConflictReason(addon, state);
                if (conflict != null)
                    plan.LocalSkipped.Add(new SkippedAddon { Addon = addon, Reason = conflict });
                else
                    plan.ToInstall.Add(addon);
            }
            else
            {
                plan.UpToDate.Add(addon);
            }
        }

        foreach (var inst in state.Addons)
        {
            if (!manifest.Addons.Any(a => a.Id == inst.Id))
                plan.ToRemove.Add(inst);
        }

        return plan;
    }

    /// <summary>
    /// Aynı mod kullanıcının bilgisayarında zaten var mı? Varsa eklenti hiç indirilmez/kurulmaz,
    /// mevcut dosyalara asla dokunulmaz. DLC paketleri: kullanıcının kendi paket klasörü
    /// (gcs_ öneki olmayan aynı ad). Script/ASI/dosya: hedef dosyalardan biri zaten varsa.
    /// Addon pedler dahil her tür için geçerlidir (ped içerikleri karşılaştırılmaz/ellenmez).
    /// </summary>
    private string? LocalConflictReason(AddonInfo addon, SyncState state)
    {
        // Bizim kurduğumuz (kayıtlı) bir mod güncelleniyorsa atlama yapılmaz:
        // güncelleme akışı dosya bazında kullanıcı dosyalarını zaten korur.
        if (state.Addons.Any(x => x.Id == addon.Id)) return null;

        switch (addon.Type)
        {
            case AddonType.DlcPack:
            {
                var dlcpacksRoot = Path.Combine(_gameFolder, "mods", "update", "x64", "dlcpacks");
                if (!Directory.Exists(dlcpacksRoot)) return null;
                var wanted = Safe(addon.Id);
                foreach (var dir in Directory.GetDirectories(dlcpacksRoot))
                {
                    var name = Path.GetFileName(dir);
                    if (name.StartsWith("gcs_", StringComparison.OrdinalIgnoreCase)) continue; // bizim bağladıklarımız
                    if (Safe(name) == wanted) return dir;
                }
                return null;
            }
            default:
            {
                var owned = state.Addons
                    .SelectMany(a => a.InstalledPaths)
                    .Select(p => p.Replace('/', '\\'))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                foreach (var file in addon.Files)
                {
                    var rel = file.Path.Replace('\\', '/');
                    if (addon.Type == AddonType.ClientScript) rel = "scripts/" + rel;
                    var relWin = rel.Replace('/', '\\');
                    if (owned.Contains(relWin)) continue; // bizim kurduğumuz dosya
                    var dest = Path.Combine(_gameFolder, relWin);
                    if (File.Exists(dest)) return dest;
                }
                return null;
            }
        }
    }

    /// <summary>Kurulu kaydın dosyaları hâlâ yerinde mi (hafif kontrol)?</summary>
    private bool IsInstalledIntact(InstalledAddon inst)
    {
        if (inst.InstalledPaths.Count == 0) return false;

        switch (inst.Type)
        {
            case AddonType.DlcPack:
            {
                var dir = Path.Combine(_gameFolder,
                    "mods", "update", "x64", "dlcpacks", inst.MountFolder);
                if (!Directory.Exists(dir)) return false;
                // dlc.rpf hâlâ var mı?
                return Directory.EnumerateFiles(dir, "dlc.rpf", SearchOption.AllDirectories).Any();
            }
            default:
                return inst.InstalledPaths.Any(p =>
                    File.Exists(Path.Combine(_gameFolder, p.Replace('/', Path.DirectorySeparatorChar))));
        }
    }

    // ------------------------------------------------------------------ senkronizasyon

    /// <summary>
    /// Tam senkronizasyon: eksik/güncel olmayan eklentileri indirir ve kurar,
    /// sunucudan kaldırılanları temizler.
    /// </summary>
    public async Task<SyncState> SyncAsync(
        SyncClient client, Manifest manifest, string dataFolder, string cacheRoot,
        IProgress<SyncProgress>? progress = null, CancellationToken ct = default)
    {
        var state = SyncStateStore.Load(dataFolder, manifest.ServerId);
        state.ServerId = manifest.ServerId;
        state.ServerName = manifest.ServerName;

        var plan = BuildPlan(manifest, state);
        _log($"Plan: {plan.ToInstall.Count} kurulacak, {plan.UpToDate.Count} güncel, " +
             $"{plan.ToRemove.Count} kaldırılacak, {plan.LocalSkipped.Count} sizde var (atlanacak).");

        // Kullanıcının bilgisayarında zaten olan modlar: indirilmez, kurulmaz, dokunulmaz.
        foreach (var skip in plan.LocalSkipped)
        {
            _log($"Atlandı (bilgisayarınızda zaten var): {skip.Addon.DisplayName} -> {skip.Reason}");
            progress?.Report(new SyncProgress(
                $"Atlandı: {skip.Addon.DisplayName} (zaten bilgisayarınızda var)", 0));
        }

        // 1) Sunucudan kaldırılanları temizle
        foreach (var inst in plan.ToRemove)
        {
            ct.ThrowIfCancellationRequested();
            progress?.Report(new SyncProgress($"Kaldırılıyor: {inst.Id}", 0));
            _log($"Kaldırılıyor: {inst.Id} ({inst.Type})");
            Uninstall(inst);
            state.Addons.Remove(inst);
            SyncStateStore.Save(dataFolder, state); // çökme güvenliği: artımlı kayıt
        }

        // 2) İndir + kur
        long totalBytes = plan.ToInstall.Sum(a => a.TotalSize);
        long doneBytes = 0;

        foreach (var addon in plan.ToInstall)
        {
            ct.ThrowIfCancellationRequested();
            var addonCacheDir = Path.Combine(cacheRoot, Safe(manifest.ServerId), Safe(addon.Id));
            var version = ServerScanner.ComputeVersion(addon);

            // Güncelleme senaryosu: eskisini kaldır
            var old = state.Addons.FirstOrDefault(a => a.Id == addon.Id);
            if (old != null)
            {
                _log($"Güncelleniyor: {addon.Id} ({old.Version} -> {version})");
                Uninstall(old);
                state.Addons.Remove(old);
            }

            doneBytes = await DownloadAddonAsync(
                client, addon, addonCacheDir, progress, doneBytes, totalBytes, ct);

            progress?.Report(new SyncProgress($"Kuruluyor: {addon.DisplayName}", Fraction(doneBytes, totalBytes)));
            var installed = Install(addon, addonCacheDir, version);
            state.Addons.Add(installed);
            SyncStateStore.Save(dataFolder, state); // çökme güvenliği: artımlı kayıt
            _log($"Kuruldu: {addon.DisplayName} ({addon.Type})");
        }

        state.LastSyncUtc = DateTime.UtcNow;
        SyncStateStore.Save(dataFolder, state);
        progress?.Report(new SyncProgress("Senkronizasyon tamamlandı.", 1.0));
        return state;
    }

    private async Task<long> DownloadAddonAsync(
        SyncClient client, AddonInfo addon, string addonCacheDir,
        IProgress<SyncProgress>? progress, long doneBytes, long totalBytes, CancellationToken ct)
    {
        Directory.CreateDirectory(addonCacheDir);
        var sidecarPath = addonCacheDir.TrimEnd('\\', '/') + ".files.json";
        var sidecar = SyncJson.Load<Dictionary<string, string>>(sidecarPath) ?? new();
        long addonDone = 0;

        foreach (var file in addon.Files)
        {
            ct.ThrowIfCancellationRequested();
            var dest = Path.Combine(addonCacheDir, file.Path.Replace('/', Path.DirectorySeparatorChar));

            bool cached = File.Exists(dest)
                && sidecar.TryGetValue(file.Path, out var knownSha)
                && knownSha == file.Sha256
                && new FileInfo(dest).Length == file.Size;

            if (!cached)
            {
                var fileDone = 0L;
                var fileProgress = new Progress<long>(b => fileDone = b);
                progress?.Report(new SyncProgress(
                    $"İndiriliyor: {addon.DisplayName} / {file.Path}",
                    Fraction(doneBytes + addonDone, totalBytes)));
                await client.DownloadFileAsync(addon.Id, file, dest, fileProgress, ct);
            }

            addonDone += file.Size;
            sidecar[file.Path] = file.Sha256;
            progress?.Report(new SyncProgress(
                $"İndirildi: {addon.DisplayName} / {file.Path}",
                Fraction(doneBytes + addonDone, totalBytes),
                doneBytes + addonDone, totalBytes));
        }

        SyncJson.Save(sidecarPath, sidecar);

        // İndirilmeyenleri de temizle (güncellemede silinen dosyalar)
        var expected = addon.Files.Select(f => f.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var existing in Directory.EnumerateFiles(addonCacheDir, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(addonCacheDir, existing).Replace('\\', '/');
            if (!expected.Contains(rel))
            {
                try { File.Delete(existing); } catch { }
            }
        }

        return doneBytes + addonDone;
    }

    private static double Fraction(long done, long total) =>
        total <= 0 ? 0 : Math.Clamp((double)done / total, 0, 1);

    // ------------------------------------------------------------------ kurulum

    private InstalledAddon Install(AddonInfo addon, string cacheDir, string version)
    {
        return addon.Type switch
        {
            AddonType.DlcPack => InstallDlcPack(addon, cacheDir, version),
            AddonType.ClientScript => InstallFiles(addon, cacheDir, version, "scripts"),
            AddonType.AsiPlugin => InstallFiles(addon, cacheDir, version, null),
            AddonType.ClientFiles => InstallFiles(addon, cacheDir, version, null),
            _ => throw new SyncException($"Bu eklenti türü istemciye kurulamaz: {addon.Type}")
        };
    }

    private InstalledAddon InstallDlcPack(AddonInfo addon, string cacheDir, string version)
    {
        var mountFolder = MountFolderName(addon.Id);
        var dlcpacksRoot = Path.Combine(_gameFolder, "mods", "update", "x64", "dlcpacks");
        Directory.CreateDirectory(dlcpacksRoot);
        var target = Path.Combine(dlcpacksRoot, mountFolder);

        if (Directory.Exists(target)) DeleteDirectoryWithRetry(target, _log);

        // Oyun klasörüne GB'larca dosya kopyalamamak için NTFS bağlantısı (junction) denenir.
        // Başarısız olursa (ör. desteklenmeyen disk biçimi) normal kopyalamaya düşülür.
        string linkTarget = "";
        if (TryCreateJunction(target, cacheDir))
        {
            linkTarget = cacheDir;
            _log($"{addon.DisplayName}: oyuna bağlandı (bağlantı ile; kopyalama yapılmadı).");
        }
        else
        {
            CopyListedFiles(addon, cacheDir, target);
            _log($"{addon.DisplayName}: oyuna kopyalandı (bağlantı kurulamadı).");
        }

        var entry = mountFolder +
            (string.IsNullOrEmpty(addon.RelativePackPath) ? "" : "/" + addon.RelativePackPath);

        AddDlclistEntry(entry);

        return new InstalledAddon
        {
            Id = addon.Id,
            Type = addon.Type,
            Version = version,
            MountFolder = mountFolder,
            DlclistEntry = entry,
            LinkTarget = linkTarget,
            InstalledPaths = { Path.GetRelativePath(_gameFolder, target).Replace('\\', '/') }
        };
    }

    /// <summary>NTFS junction oluşturur (mklink /J). Başarılıysa true döner.</summary>
    private static bool TryCreateJunction(string linkPath, string targetPath)
    {
        try
        {
            if (!Directory.Exists(targetPath)) return false;

            var psi = new ProcessStartInfo("cmd.exe")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                Arguments = $"/c mklink /J \"{linkPath}\" \"{targetPath}\""
            };

            using var p = Process.Start(psi);
            if (p == null) return false;
            p.StandardOutput.ReadToEnd();
            p.StandardError.ReadToEnd();
            p.WaitForExit(15000);

            return p.ExitCode == 0 &&
                   Directory.Exists(linkPath) &&
                   new DirectoryInfo(linkPath).Attributes.HasFlag(FileAttributes.ReparsePoint);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Yalnızca indirme: eklentileri önbelleğe alır, oyuna KURMAZ.</summary>
    public async Task PrefetchAsync(
        SyncClient client, Manifest manifest, string cacheRoot,
        IProgress<SyncProgress>? progress = null, CancellationToken ct = default)
    {
        var addons = manifest.Addons.Where(a => a.Type != AddonType.ServerPlugin).ToList();
        long totalBytes = addons.Sum(a => a.TotalSize);
        long doneBytes = 0;

        foreach (var addon in addons)
        {
            ct.ThrowIfCancellationRequested();
            var addonCacheDir = Path.Combine(cacheRoot, Safe(manifest.ServerId), Safe(addon.Id));
            doneBytes = await DownloadAddonAsync(
                client, addon, addonCacheDir, progress, doneBytes, totalBytes, ct);
        }

        progress?.Report(new SyncProgress("Tüm mod dosyaları indirildi (oyuna kurulmadı).", 1.0));
    }

    /// <summary>ClientScript / ClientFiles kurulumu (oyun köküne göreli kopyalama).</summary>
    private InstalledAddon InstallFiles(AddonInfo addon, string cacheDir, string version, string? forceRoot)
    {
        var installed = new InstalledAddon { Id = addon.Id, Type = addon.Type, Version = version };
        int copied = 0, skipped = 0;

        foreach (var file in addon.Files)
        {
            var rel = file.Path.Replace('\\', '/');
            if (forceRoot != null) rel = forceRoot.TrimEnd('/') + "/" + rel;

            if (addon.Type == AddonType.ClientFiles && !ServerScanner.IsAllowedClientPath(rel))
                throw new SyncException($"Güvenlik: izin verilmeyen hedef yol: {rel}");
            if (addon.Type == AddonType.AsiPlugin && !ServerScanner.IsAllowedAsiFile(rel))
                throw new SyncException($"Güvenlik: izin verilmeyen ASI dosyası: {rel}");

            var dest = Path.Combine(_gameFolder, rel.Replace('/', Path.DirectorySeparatorChar));
            var src = Path.Combine(cacheDir, file.Path.Replace('/', Path.DirectorySeparatorChar));

            var action = CopyFileWithPolicy(src, dest, file.Sha256, out var keptExisting);
            if (action)
            {
                installed.InstalledPaths.Add(rel);
                copied++;
            }
            else if (keptExisting)
            {
                skipped++;
            }
        }

        if (skipped > 0)
            _log($"UYARI: {addon.DisplayName}: {skipped} dosya zaten farklı sürümde mevcuttu, korundu.");
        _log($"{addon.DisplayName}: {copied} dosya kuruldu.");

        return installed;
    }

    /// <summary>
    /// Dosyayı kopyalar; hedef zaten aynıysa atlar (false, false),
    /// farklı içerikle varsa KORUR (false, true), yoksa kopyalar (true, false).
    /// </summary>
    private static bool CopyFileWithPolicy(string src, string dest, string expectedSha, out bool keptExisting)
    {
        keptExisting = false;
        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);

        if (File.Exists(dest))
        {
            using var sha = System.Security.Cryptography.SHA256.Create();
            using var fs = File.OpenRead(dest);
            var existing = Convert.ToHexString(sha.ComputeHash(fs)).ToLowerInvariant();
            if (string.Equals(existing, expectedSha, StringComparison.OrdinalIgnoreCase))
                return false; // zaten aynı
            keptExisting = true;
            return false; // farklı: kullanıcının dosyası korunur
        }

        File.Copy(src, dest, overwrite: false);
        return true;
    }

    // ------------------------------------------------------------------ kaldırma

    /// <summary>
    /// Eklentiyi oyundan kaldırır. <paramref name="quarantineRoot"/> verilirse dosyalar
    /// silinmez; o klasörün altına (klasör yapısı korunarak) taşınır — hiçbir şey kaybolmaz.
    /// </summary>
    public void Uninstall(InstalledAddon inst, string? quarantineRoot = null)
    {
        switch (inst.Type)
        {
            case AddonType.DlcPack:
            {
                if (!string.IsNullOrEmpty(inst.DlclistEntry))
                    TryRemoveDlclistEntry(inst.DlclistEntry);

                var relDir = Path.Combine("mods", "update", "x64", "dlcpacks", inst.MountFolder);
                var dir = Path.Combine(_gameFolder, relDir);
                if (Directory.Exists(dir))
                {
                    if (quarantineRoot != null) MoveDirectoryToQuarantine(dir, relDir, quarantineRoot);
                    else DeleteDirectoryWithRetry(dir, _log);
                }
                break;
            }
            default:
            {
                foreach (var relPath in inst.InstalledPaths)
                {
                    var full = Path.Combine(_gameFolder, relPath.Replace('/', Path.DirectorySeparatorChar));
                    try
                    {
                        if (!File.Exists(full)) continue;
                        if (quarantineRoot != null) MoveFileToQuarantine(full, relPath, quarantineRoot);
                        else File.Delete(full);
                        PruneEmptyParents(Path.GetDirectoryName(full));
                    }
                    catch (Exception ex)
                    {
                        _log($"UYARI: kaldırılamadı: {relPath} ({ex.Message})");
                    }
                }
                break;
            }
        }
    }

    /// <summary>
    /// Bir sunucunun tüm eklentilerini kaldırır. Dosyalar SİLİNMEZ; veri klasöründeki
    /// 'removed' karantinasına (tarihli klasör) taşınır. Yalnızca en yeni 3 çalıştırma saklanır.
    /// </summary>
    public void UninstallAll(SyncState state, string dataFolder)
    {
        _quarantineMoved = 0;
        var quarantine = QuarantineRoot(dataFolder, DateTime.Now);

        foreach (var inst in state.Addons.ToList())
        {
            _log($"Kaldırılıyor: {inst.Id}");
            Uninstall(inst, quarantine);
            state.Addons.Remove(inst);
            SyncStateStore.Save(dataFolder, state); // çökme güvenliği: artımlı kayıt
        }
        state.LastSyncUtc = DateTime.UtcNow;
        SyncStateStore.Save(dataFolder, state);

        if (_quarantineMoved > 0)
        {
            _log($"Modlar silinmedi; güvenli klasöre taşındı: {quarantine}");
            PruneQuarantineRoot(dataFolder, keep: 3, _log);
        }
        else
        {
            try { if (Directory.Exists(quarantine)) Directory.Delete(quarantine, recursive: true); } catch { }
        }
    }

    private void PruneEmptyParents(string? dir)
    {
        if (dir == null) return;
        var stopRoots = new[]
        {
            _gameFolder.TrimEnd('\\', '/'),
            Path.Combine(_gameFolder, "scripts").TrimEnd('\\', '/'),
            Path.Combine(_gameFolder, "mods").TrimEnd('\\', '/'),
            Path.Combine(_gameFolder, "mods", "GTACoop").TrimEnd('\\', '/')
        };

        while (dir != null &&
               !stopRoots.Contains(dir.TrimEnd('\\', '/'), StringComparer.OrdinalIgnoreCase) &&
               Directory.Exists(dir) &&
               !Directory.EnumerateFileSystemEntries(dir).Any())
        {
            try { Directory.Delete(dir); } catch { return; }
            dir = Path.GetDirectoryName(dir);
        }
    }

    // ------------------------------------------------------------------ karantina (güvenli taşıma)

    private int _quarantineMoved;

    /// <summary>Kaldırılan dosyaların taşındığı tarihli klasör (silme yok, geri alınabilir).</summary>
    public static string QuarantineRoot(string dataFolder, DateTime when) =>
        Path.Combine(dataFolder, "removed", when.ToString("yyyyMMdd-HHmmss"));

    /// <summary>Yalnızca en yeni <paramref name="keep"/> karantina çalıştırmasını bırakır.</summary>
    private static void PruneQuarantineRoot(string dataFolder, int keep, Action<string>? log)
    {
        try
        {
            var root = Path.Combine(dataFolder, "removed");
            if (!Directory.Exists(root)) return;
            foreach (var dir in Directory.GetDirectories(root).OrderByDescending(d => d).Skip(keep))
            {
                try
                {
                    Directory.Delete(dir, recursive: true);
                    log?.Invoke($"Eski karantina klasörü silindi: {Path.GetFileName(dir)}");
                }
                catch { }
            }
        }
        catch { }
    }

    private void MoveFileToQuarantine(string full, string relPath, string quarantineRoot)
    {
        var dest = Path.Combine(quarantineRoot, relPath.Replace('/', Path.DirectorySeparatorChar));
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            if (File.Exists(dest)) File.Delete(dest);
            try
            {
                File.Move(full, dest);
            }
            catch (IOException)
            {
                // Farklı disk olabilir: kopyala + kaynağı sil.
                File.Copy(full, dest, overwrite: true);
                File.Delete(full);
            }
            _quarantineMoved++;
        }
        catch (Exception ex)
        {
            // Taşınamıyorsa oyunu temiz bırakmak için silmeyi dene.
            try
            {
                File.Delete(full);
                _log($"UYARI: karantinaya taşınamadı, silindi: {relPath} ({ex.Message})");
            }
            catch
            {
                _log($"UYARI: taşınamadı ve silinemedi: {relPath} ({ex.Message})");
            }
        }
    }

    private void MoveDirectoryToQuarantine(string dir, string relDest, string quarantineRoot)
    {
        // Bağlantıysa (junction) yalnızca bağlantı kaldırılır; hedef klasöre asla dokunulmaz.
        try
        {
            if (new DirectoryInfo(dir).Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                Directory.Delete(dir, recursive: false);
                _log($"Bağlantı kaldırıldı: {relDest}");
                return;
            }
        }
        catch { }

        var dest = Path.Combine(quarantineRoot, relDest.Replace('/', Path.DirectorySeparatorChar));
        for (int i = 0; i < 4; i++)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                if (Directory.Exists(dest)) Directory.Delete(dest, recursive: true);
                try
                {
                    Directory.Move(dir, dest);
                }
                catch (IOException)
                {
                    // Farklı disk olabilir: kopyala + kaynağı sil.
                    CopyDirectoryRecursive(dir, dest);
                    Directory.Delete(dir, recursive: true);
                }
                _quarantineMoved++;
                return;
            }
            catch (Exception ex)
            {
                if (i == 3)
                {
                    // Son çare: oyun yine de eski hâline dönsün.
                    _log($"UYARI: karantinaya taşınamadı: {relDest} ({ex.Message})");
                    DeleteDirectoryWithRetry(dir, _log);
                    return;
                }
                Thread.Sleep(250 * (i + 1));
            }
        }
    }

    private static void CopyDirectoryRecursive(string src, string dst)
    {
        Directory.CreateDirectory(dst);
        foreach (var file in Directory.GetFiles(src))
            File.Copy(file, Path.Combine(dst, Path.GetFileName(file)), overwrite: true);
        foreach (var sub in Directory.GetDirectories(src))
            CopyDirectoryRecursive(sub, Path.Combine(dst, Path.GetFileName(sub)));
    }

    // ------------------------------------------------------------------ dlclist

    public static string MountFolderName(string addonId) => "gcs_" + Safe(addonId);

    public static string Safe(string name)
    {
        var sb = new StringBuilder();
        foreach (var ch in name.ToLowerInvariant())
        {
            if (char.IsAsciiLetterOrDigit(ch)) sb.Append(ch);
            else if (sb.Length > 0 && sb[^1] != '_') sb.Append('_');
        }
        var s = sb.ToString().Trim('_');
        if (s.Length == 0) s = "pack";
        return s.Length > 40 ? s[..40] : s;
    }

    private string UpdateRpfPath => Path.Combine(_gameFolder, "mods", "update", "update.rpf");

    /// <summary>
    /// update.rpf düzenlemeye uygun mu? Değilse (4 GB RPF7 sınırına yakın veya bozuk)
    /// en yeni sağlıklı yedekten otomatik geri yükler. Böylece GTACoop şişmiş/bozuk bir
    /// arşivi asla düzenlemez ve OpenIV'nin yeni dosya ekleme yeteneği korunur.
    /// </summary>
    private void EnsureUpdateRpfUsable()
    {
        if (!File.Exists(UpdateRpfPath))
            throw new SyncException(
                "mods/update/update.rpf bulunamadı. DLC paketleri için OpenIV ile mods klasörü oluşturulmalı.");

        if (BackupService.IsUpdateRpfHealthy(UpdateRpfPath, out var reason))
            return;

        _log($"UYARI: update.rpf düzenlemeye uygun değil: {reason}");

        var backup = BackupService.FindNewestHealthyBackup(_gameFolder, _log);
        if (backup == null)
            throw new SyncException(
                "update.rpf sağlıksız/çok büyük ve sağlıklı yedek bulunamadı. " +
                "OpenIV ile mods klasörünü onarın veya Launcher > Yedekler bölümünden geri dönün.");

        _log($"Otomatik onarım: {Path.GetFileName(backup)} geri yükleniyor...");
        BackupService.RestoreUpdateRpf(_gameFolder, backup, _log);

        if (!BackupService.IsUpdateRpfHealthy(UpdateRpfPath, out var after))
            throw new SyncException("update.rpf otomatik onarımı başarısız: " + after);

        _log("update.rpf onarıldı; işleme devam ediliyor.");
    }

    public void AddDlclistEntry(string entry)
    {
        if (IsGameRunning())
            throw new SyncException("GTA V açıkken dlclist düzenlenemez. Lütfen oyunu kapatın.");
        if (!File.Exists(UpdateRpfPath))
            throw new SyncException(
                "mods/update/update.rpf bulunamadı. DLC paketleri için OpenIV ile mods klasörü oluşturulmalı.");

        EnsureUpdateRpfUsable();
        BackupService.EnsureUpdateRpfBackup(_gameFolder, _log);

        lock (_rpfLock)
        {
            using var archive = RpfArchive.Open(UpdateRpfPath);
            var file = archive.Find(Dlclist.RelativePath)
                ?? throw new SyncException("update.rpf içinde dlclist.xml bulunamadı.");

            var (xml, enc) = Dlclist.Decode(archive.ReadFile(file));
            var existing = Dlclist.GetPackNames(xml);
            if (existing.Contains(entry, StringComparer.OrdinalIgnoreCase))
            {
                _log($"dlclist: '{entry}' zaten kayıtlı, değişiklik yok.");
                return;
            }

            var updated = Dlclist.AddPackNames(xml, new[] { entry });
            ValidateXml(updated);
            ReplaceDlclist(archive, file, updated, enc);
            _log($"dlclist: eklendi -> {entry}");
        }
    }

    public void RemoveDlclistEntry(string entry)
    {
        if (IsGameRunning())
            throw new SyncException("GTA V açıkken dlclist düzenlenemez. Lütfen oyunu kapatın.");
        if (!File.Exists(UpdateRpfPath))
            throw new SyncException(
                "mods/update/update.rpf bulunamadı. dlclist girdisi çıkarılamıyor.");

        EnsureUpdateRpfUsable();
        BackupService.EnsureUpdateRpfBackup(_gameFolder, _log);

        lock (_rpfLock)
        {
            using var archive = RpfArchive.Open(UpdateRpfPath);
            var file = archive.Find(Dlclist.RelativePath)
                ?? throw new SyncException("update.rpf içinde dlclist.xml bulunamadı.");

            var (xml, enc) = Dlclist.Decode(archive.ReadFile(file));
            var updated = Dlclist.RemovePackNames(xml, new[] { entry });
            ValidateXml(updated);
            ReplaceDlclist(archive, file, updated, enc);
            _log($"dlclist: çıkarıldı -> {entry}");
        }
    }

    private void TryRemoveDlclistEntry(string entry)
    {
        try
        {
            RemoveDlclistEntry(entry);
        }
        catch (Exception ex)
        {
            _log($"UYARI: dlclist girdisi çıkarılamadı ({entry}): {ex.Message}");
        }
    }

    private static void ValidateXml(string xml)
    {
        try
        {
            System.Xml.Linq.XDocument.Parse(xml);
        }
        catch (Exception ex)
        {
            throw new SyncException("dlclist düzenlemesi geçersiz XML üretti; yazma iptal edildi: " + ex.Message);
        }
    }

    /// <summary>
    /// dlclist'i özgün kodlamasıyla yazar (UTF-16 BOM'lu dosyalar dahil).
    /// Beyan "utf-16" iken dosya UTF-8 kalmışsa UTF-16'ya normalleştirir; OpenIV'in
    /// XML düzenleyicisi bu uyumsuzlukta dosyayı düzenleyemeyebiliyor.
    /// </summary>
    private void ReplaceDlclist(RpfArchive archive, RpfFileEntry file, string updated, Dlclist.DlclistEncoding enc)
    {
        var writeEnc = Dlclist.Normalize(updated, enc);
        if (writeEnc != enc)
            _log("dlclist: kodlama beyana uygun UTF-16'ya düzeltildi (OpenIV uyumu).");
        archive.ReplaceFile(file, Dlclist.Encode(updated, writeEnc));
    }

    /// <summary>dlclist'te gcs_ ile başlayan sahipsiz girdileri ve klasörleri temizler.</summary>
    public void PruneOrphanMounts()
    {
        var dlcpacksRoot = Path.Combine(_gameFolder, "mods", "update", "x64", "dlcpacks");

        // 1) gcs_ klasörlerini sil
        if (Directory.Exists(dlcpacksRoot))
        {
            foreach (var dir in Directory.GetDirectories(dlcpacksRoot, "gcs_*"))
            {
                _log($"Temizleniyor: {Path.GetFileName(dir)}");
                DeleteDirectoryWithRetry(dir, _log);
            }
        }

        // 2) dlclist'ten gcs_ girdilerini çıkar
        try
        {
            if (IsGameRunning()) throw new SyncException("GTA V açık; atlandı.");
            EnsureUpdateRpfUsable();

            lock (_rpfLock)
            {
                using var archive = RpfArchive.Open(UpdateRpfPath);
                var file = archive.Find(Dlclist.RelativePath);
                if (file == null) return;

                var (xml, enc) = Dlclist.Decode(archive.ReadFile(file));
                var orphans = Dlclist.GetPackNames(xml)
                    .Where(n => n.StartsWith("gcs_", StringComparison.OrdinalIgnoreCase) ||
                                n.Contains("/gcs_", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (orphans.Count == 0)
                {
                    _log("dlclist: temizlenecek gcs_ girdisi yok.");
                    return;
                }

                BackupService.EnsureUpdateRpfBackup(_gameFolder, _log);
                var updated = Dlclist.RemovePackNames(xml, orphans);
                ValidateXml(updated);
                ReplaceDlclist(archive, file, updated, enc);
                _log($"dlclist: {orphans.Count} gcs_ girdisi temizlendi.");
            }
        }
        catch (Exception ex)
        {
            _log("dlclist temizliği atlandı: " + ex.Message);
        }
    }

    // ------------------------------------------------------------------ yardımcılar

    public static bool IsGameRunning()
    {
        foreach (var name in new[] { "GTA5", "GTA5_Enhanced", "PlayGTAV" })
        {
            try
            {
                if (Process.GetProcessesByName(name).Length > 0) return true;
            }
            catch { }
        }
        return false;
    }

    private static void CopyListedFiles(AddonInfo addon, string cacheDir, string destRoot)
    {
        foreach (var file in addon.Files)
        {
            var rel = file.Path.Replace('/', Path.DirectorySeparatorChar);
            var src = Path.Combine(cacheDir, rel);
            var dst = Path.Combine(destRoot, rel);
            if (!File.Exists(src))
                throw new SyncException($"Önbellekte eksik dosya: {file.Path}");
            Directory.CreateDirectory(Path.GetDirectoryName(dst)!);
            File.Copy(src, dst, overwrite: true);
        }
        // Paket kökünün varlığından emin ol (içi boş olsa bile)
        Directory.CreateDirectory(destRoot);
    }

    private static void DeleteDirectoryWithRetry(string dir, Action<string> log)
    {
        for (int i = 0; i < 4; i++)
        {
            try
            {
                // Bağlantıysa (junction) YALNIZCA bağlantıyı sil; hedef klasöre asla dokunma.
                if (new DirectoryInfo(dir).Attributes.HasFlag(FileAttributes.ReparsePoint))
                {
                    Directory.Delete(dir, recursive: false);
                    return;
                }

                Directory.Delete(dir, recursive: true);
                return;
            }
            catch (Exception ex)
            {
                if (i == 3)
                {
                    log($"UYARI: klasör silinemedi: {dir} ({ex.Message})");
                    return;
                }
                Thread.Sleep(250 * (i + 1));
            }
        }
    }
}
