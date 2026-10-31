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

        /// <summary>
        /// Güvenli biçimde kurulamayan eklentiler (ör. update.rpf çok büyük/bozuk
        /// olduğu için DLC paketi). Bu eklentiler indirilmez; mevcut dosyalara dokunulmaz.
        /// </summary>
        public List<SkippedAddon> Blocked { get; } = new();
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
            var installed = state.Addons.FirstOrDefault(a =>
                a.Id == addon.Id && a.Type == addon.Type);

            if (installed == null || installed.Version != version ||
                HasLegacyScriptLayout(installed) || !IsInstalledIntact(installed))
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

        // DLC paketleri update.rpf içindeki dlclist'i düzenlemeyi gerektirir.
        // Dosya çok büyük/bozuksa veya dlclist yerinde güncellenemiyorsa paketi
        // indirmeye bile başlamayız; böylece kullanıcının mevcut update.rpf'si
        // ve modları korunur, diğer eklentiler kurulmaya devam eder.
        var dlcToInstall = plan.ToInstall
            .Where(a => a.Type == AddonType.DlcPack)
            .ToList();
        if (dlcToInstall.Count > 0 && !CanInstallDlcPacks(dlcToInstall, out var dlcReason))
        {
            foreach (var addon in dlcToInstall)
            {
                plan.ToInstall.Remove(addon);
                plan.Blocked.Add(new SkippedAddon { Addon = addon, Reason = dlcReason });
            }
        }

        // OIV paketleri de dlclist düzenleyebilir. update.rpf güvenli değilse
        // paketi indirmeye başlamadan engelle; mevcut dosyalara dokunma.
        foreach (var addon in plan.ToInstall
                     .Where(a => a.Type == AddonType.OivPackage && a.OivDlclistEntries.Count > 0)
                     .ToList())
        {
            if (CanAddDlclistEntries(addon.OivDlclistEntries, out var oivReason)) continue;
            plan.ToInstall.Remove(addon);
            plan.Blocked.Add(new SkippedAddon { Addon = addon, Reason = oivReason });
        }

        foreach (var inst in state.Addons)
        {
            if (!manifest.Addons.Any(a => a.Id == inst.Id && a.Type == inst.Type))
                plan.ToRemove.Add(inst);
        }

        return plan;
    }

    /// <summary>
    /// DLC paketlerinin dlclist'e güvenle eklenip eklenemeyeceğini önceden kontrol eder.
    /// - update.rpf yoksa veya yapısal olarak bozuksa: hayır (dosyaya dokunulmaz).
    /// - 4 GB'a yakın ama yapısal olarak sağlıklıysa: yalnızca yeni dlclist mevcut
    ///   alana sığıyorsa evet (arşiv büyütülmez).
    /// - Küçük ve sağlıklıysa: normal ekleme yapılabilir.
    /// Hiçbir durumda mevcut update.rpf otomatik olarak yedekle değiştirilmez.
    /// </summary>
    public bool CanInstallDlcPacks(IEnumerable<AddonInfo> addons, out string reason)
    {
        reason = "";
        var list = addons.Where(a => a.Type == AddonType.DlcPack).ToList();
        if (list.Count == 0) return true;
        return CanAddDlclistEntries(list.Select(DlclistEntryFor), out reason);
    }

    /// <summary>
    /// Verilen dlclist girdileri update.rpf'ye güvenle yazılabilir mi?
    /// DLC ve OIV paketleri aynı koruma kurallarını kullanır.
    /// </summary>
    public bool CanAddDlclistEntries(IEnumerable<string> entries, out string reason)
    {
        reason = "";
        var wanted = entries
            .Where(e => !string.IsNullOrWhiteSpace(e))
            .Select(e => e.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (wanted.Count == 0) return true;

        if (!File.Exists(UpdateRpfPath))
        {
            reason = SyncText.T(
                "mods/update/update.rpf bulunamadı. DLC/OIV paketleri için OpenIV ile mods klasörü oluşturulmalı.");
            return false;
        }

        bool healthy = BackupService.IsUpdateRpfHealthy(UpdateRpfPath, out _);
        if (!healthy &&
            !BackupService.IsUpdateRpfStructurallyHealthy(UpdateRpfPath, out var structuralWhy))
        {
            reason = SyncText.T(
                "update.rpf düzenlemeye uygun değil: {0}. OpenIV ile mods klasörünü onarın " +
                "veya Launcher > Yedekler bölümünden geri dönün.",
                SyncText.Translate(structuralWhy));
            return false;
        }

        try
        {
            using var archive = RpfArchive.Open(UpdateRpfPath);
            var file = archive.Find(Dlclist.RelativePath)
                ?? throw new SyncException(SyncText.T("update.rpf içinde dlclist.xml bulunamadı."));

            var (xml, enc) = Dlclist.Decode(archive.ReadFile(file));
            var existing = Dlclist.GetPackNames(xml);
            var missing = wanted
                .Where(e => !existing.Contains(e, StringComparer.OrdinalIgnoreCase))
                .ToList();

            // Girdiler zaten varsa update.rpf'ye yazma gerekmez; dosyalar
            // güncellenebilir, mevcut (sağlıksız/büyük) arşive dokunulmaz.
            if (missing.Count == 0) return true;

            // Küçük ve sağlıklı arşivlerde normal (gerekirse sona ekleyen) yazma yolu.
            if (healthy) return true;

            // Yalnızca büyük ama yapısal olarak sağlıklı arşiv: yerinde yazma şart.
            var updated = Dlclist.AddPackNames(xml, missing);
            ValidateXml(updated);
            var writeEnc = Dlclist.Normalize(updated, enc);
            var bytes = Dlclist.Encode(updated, writeEnc);
            if (!archive.CanReplaceInPlace(file, bytes, out var why))
            {
                reason = SyncText.T(
                    "update.rpf çok büyük ve dlclist.xml yerinde güncellenemiyor: {0}",
                    SyncText.Translate(why));
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            reason = SyncText.T("update.rpf okunamadı: {0}", SyncText.Translate(ex.Message));
            return false;
        }
    }

    private static string DlclistEntryFor(AddonInfo addon) =>
        MountFolderName(addon.Id) +
        (string.IsNullOrEmpty(addon.RelativePackPath) ? "" : "/" + addon.RelativePackPath);

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
        if (state.Addons.Any(x => x.Id == addon.Id && x.Type == addon.Type)) return null;

        // OpenIV.asi / OpenRPF.asi / RageOpenV.asi aynı mods-folder köprüsünü açar.
        // Bir arkadaşta bunlardan biri varsa diğerini üzerine kurup çakışma yaratmayalım.
        if (addon.Type == AddonType.AsiPlugin)
        {
            var hooks = new[] { "OpenIV.asi", "OpenRPF.asi", "RageOpenV.asi" };
            var addonHooks = addon.Files
                .Select(f => Path.GetFileName(f.Path))
                .Where(n => hooks.Contains(n, StringComparer.OrdinalIgnoreCase))
                .ToList();
            if (addonHooks.Count > 0)
            {
                foreach (var hook in hooks)
                {
                    if (addonHooks.Contains(hook, StringComparer.OrdinalIgnoreCase)) continue;
                    var existing = Path.Combine(_gameFolder, hook);
                    if (File.Exists(existing)) return existing;
                }
            }
        }

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
            case AddonType.OivPackage:
            {
                var owned = state.Addons
                    .SelectMany(a => a.InstalledPaths)
                    .Select(p => p.Replace('\\', '/').TrimStart('/'))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                foreach (var target in addon.OivTargets)
                {
                    var rel = target.Replace('\\', '/').TrimStart('/');
                    if (rel.Length == 0 || rel.Contains("..")) continue;

                    const string dlcPrefix = "mods/update/x64/dlcpacks/";
                    if (rel.StartsWith(dlcPrefix, StringComparison.OrdinalIgnoreCase))
                    {
                        var rest = rel[dlcPrefix.Length..];
                        var folder = rest.Split('/')[0];
                        if (folder.Length == 0) continue;
                        var dir = Path.Combine(_gameFolder, "mods", "update", "x64", "dlcpacks", folder);
                        if (!Directory.Exists(dir)) continue;
                        if (state.Addons.SelectMany(a => a.InstalledPaths).Any(p =>
                                p.Replace('\\', '/').StartsWith(dlcPrefix + folder + "/",
                                    StringComparison.OrdinalIgnoreCase)))
                            continue; // bizim kurduğumuz paket klasörü
                        return dir;
                    }

                    var full = Path.Combine(_gameFolder, rel.Replace('/', Path.DirectorySeparatorChar));
                    if (File.Exists(full) && !owned.Contains(rel)) return full;
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
                    var rel = file.Path.Replace('\\', '/').TrimStart('/');
                    if (addon.Type == AddonType.ClientScript &&
                        !rel.StartsWith("scripts/", StringComparison.OrdinalIgnoreCase))
                        rel = "scripts/" + rel;
                    var relWin = rel.Replace('/', '\\');
                    if (owned.Contains(relWin)) continue; // bizim kurduğumuz dosya
                    var dest = Path.Combine(_gameFolder, relWin);
                    if (File.Exists(dest)) return dest;
                }
                return null;
            }
        }
    }

    /// <summary>
    /// Eski sürümler, sunucudaki "scripts/..." yollarını ikinci kez öne ekleyerek
    /// dosyaları <c>scripts\scripts\...</c> altına kuruyordu. Böyle bir kayıt varsa
    /// mod "güncel" sayılmaz; bir sonraki bağlantıda doğru yola yeniden kurulur.
    /// </summary>
    private static bool HasLegacyScriptLayout(InstalledAddon inst)
    {
        if (inst.Type != AddonType.ClientScript) return false;
        return inst.InstalledPaths.Any(p =>
            p.Replace('\\', '/').StartsWith("scripts/scripts/", StringComparison.OrdinalIgnoreCase));
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
                if (!Directory.EnumerateFiles(dir, "dlc.rpf", SearchOption.AllDirectories).Any())
                    return false;

                // Klasör duruyor ama dlclist girdisi (ör. update.rpf geri yüklendiği
                // için) kaybolmuş olabilir. Girdi yoksa paket oyunda yüklenmez;
                // yeniden kurulum planlanır. Okunamıyorsa (null) mevcut kabul edilir.
                if (string.IsNullOrEmpty(inst.DlclistEntry)) return true;
                if (!File.Exists(UpdateRpfPath)) return false;
                return DlclistEntryExists(inst.DlclistEntry) != false;
            }
            case AddonType.OivPackage:
            {
                var entries = InstalledDlclistEntries(inst);
                if (inst.InstalledPaths.Count == 0 && entries.Count == 0 &&
                    inst.RpfEdits.Count == 0) return false;
                foreach (var p in inst.InstalledPaths)
                {
                    if (!File.Exists(Path.Combine(_gameFolder,
                            p.Replace('/', Path.DirectorySeparatorChar))))
                        return false;
                }
                if (entries.Count > 0 && !File.Exists(UpdateRpfPath)) return false;
                foreach (var entry in entries)
                {
                    if (DlclistEntryExists(entry) == false) return false;
                }
                if (!RpfEditsIntact(inst.RpfEdits)) return false;
                return true;
            }
            default:
                return inst.InstalledPaths.Any(p =>
                    File.Exists(Path.Combine(_gameFolder, p.Replace('/', Path.DirectorySeparatorChar))));
        }
    }

    private static List<string> InstalledDlclistEntries(InstalledAddon inst)
    {
        var entries = new List<string>();
        if (inst.DlclistEntries.Count > 0) entries.AddRange(inst.DlclistEntries);
        else if (!string.IsNullOrWhiteSpace(inst.DlclistEntry)) entries.Add(inst.DlclistEntry);
        return entries
            .Where(e => !string.IsNullOrWhiteSpace(e))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Kaldırılacak paketin dlclist girdilerinden hangileri başka bir kurulu
    /// eklenti tarafından hâlâ gerekiyor? Yalnızca gerçekten dlcpack klasörünü
    /// sağlayan (veya kendi DLC kaydı olan) paketler koruma sayılır; böylece
    /// yalnızca liste yöneten paketler gereksiz girdileri canlı tutmaz.
    /// </summary>
    private static HashSet<string> DlclistEntriesStillNeededByOthers(
        InstalledAddon removing, IEnumerable<InstalledAddon> all)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var removingEntries = InstalledDlclistEntries(removing);
        if (removingEntries.Count == 0) return result;

        foreach (var other in all)
        {
            if (ReferenceEquals(other, removing)) continue;
            if (other.Id == removing.Id && other.Type == removing.Type) continue;

            var paths = other.InstalledPaths
                .Select(p => p.Replace('\\', '/').TrimStart('/'))
                .ToList();

            foreach (var entry in removingEntries)
            {
                if (ProvidesDlcFolder(paths, entry) ||
                    (other.Type == AddonType.DlcPack &&
                     string.Equals(other.DlclistEntry, entry, StringComparison.OrdinalIgnoreCase)))
                    result.Add(entry);
            }
        }
        return result;
    }

    private static bool ProvidesDlcFolder(IEnumerable<string> installedPaths, string entry)
    {
        var prefix = "mods/update/x64/dlcpacks/" + entry + "/";
        return installedPaths.Any(p => p.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    // ------------------------------------------------------------------ senkronizasyon

    /// <summary>
    /// Tam senkronizasyon: eksik/güncel olmayan eklentileri indirir ve kurar,
    /// sunucudan kaldırılanları temizler. <paramref name="serverCacheDir"/> sunucuya
    /// özel içerik klasörüdür (ör. uygulama klasörü\Servers\sunucum__srv_xxx).
    /// </summary>
    public async Task<SyncState> SyncAsync(
        SyncClient client, Manifest manifest, string dataFolder, string serverCacheDir,
        IProgress<SyncProgress>? progress = null, CancellationToken ct = default)
    {
        var state = SyncStateStore.Load(dataFolder, manifest.ServerId);
        state.ServerId = manifest.ServerId;
        state.ServerName = manifest.ServerName;

        var plan = BuildPlan(manifest, state);
        _log(SyncText.T(
            "Plan: {0} kurulacak, {1} güncel, {2} kaldırılacak, {3} sizde var (atlanacak), {4} engellendi.",
            plan.ToInstall.Count, plan.UpToDate.Count, plan.ToRemove.Count,
            plan.LocalSkipped.Count, plan.Blocked.Count));

        // Kullanıcının bilgisayarında zaten olan modlar: indirilmez, kurulmaz, dokunulmaz.
        foreach (var skip in plan.LocalSkipped)
        {
            _log(SyncText.T("Atlandı (bilgisayarınızda zaten var): {0} -> {1}",
                skip.Addon.DisplayName, skip.Reason));
            progress?.Report(new SyncProgress(
                SyncText.T("Atlandı: {0} (zaten bilgisayarınızda var)", skip.Addon.DisplayName), 0));
        }

        // Güvenli biçimde kurulamayan eklentiler (ör. update.rpf çok büyük/bozuk).
        // İndirme yapılmaz, mevcut dosyalara dokunulmaz; diğer eklentiler devam eder.
        foreach (var blocked in plan.Blocked)
        {
            _log(SyncText.T("Atlandı (paket kurulamıyor): {0} -> {1}",
                blocked.Addon.DisplayName, blocked.Reason));
            progress?.Report(new SyncProgress(
                SyncText.T("Atlandı: {0} (paket kurulamıyor)", blocked.Addon.DisplayName), 0));
        }

        // 1) Sunucudan kaldırılanları temizle
        foreach (var inst in plan.ToRemove)
        {
            ct.ThrowIfCancellationRequested();
            progress?.Report(new SyncProgress(
                SyncText.Translate($"Kaldırılıyor: {inst.Id}"), 0));
            _log(SyncText.Translate($"Kaldırılıyor: {inst.Id} ({inst.Type})"));
            Uninstall(inst,
                protectedDlclistEntries: DlclistEntriesStillNeededByOthers(inst, state.Addons));
            state.Addons.Remove(inst);
            SyncStateStore.Save(dataFolder, state); // çökme güvenliği: artımlı kayıt
        }

        // 2) İndir + kur
        long totalBytes = plan.ToInstall.Sum(a => a.TotalSize);
        long doneBytes = 0;

        foreach (var addon in plan.ToInstall)
        {
            ct.ThrowIfCancellationRequested();
            var version = ServerScanner.ComputeVersion(addon);
            // DLC güncellemelerinde eski sürümün önbelleğini EZMEYELİM: hedef
            // klasör eski önbelleğe junction olabilir. Sürüm başına ayrı önbellek
            // kullanınca indirme/kurulum başarısız olsa bile eski DLC sağlam kalır.
            var addonCacheDir = addon.Type is AddonType.DlcPack or AddonType.OivPackage
                ? Path.Combine(serverCacheDir, Safe(addon.Id) + "__" + Safe(version))
                : Path.Combine(serverCacheDir, Safe(addon.Id));

            // Güncelleme senaryosu. DLC ve OIV paketlerinde eski dosyaları önceden
            // SİLMEYİZ: yeni sürüm başarıyla kurulana kadar eski sürüm korunur.
            var old = state.Addons.FirstOrDefault(a =>
                a.Id == addon.Id && a.Type == addon.Type);
            if (old != null)
            {
                _log(SyncText.T("Güncelleniyor: {0} ({1} -> {2})", addon.Id, old.Version, version));
                if (addon.Type is not (AddonType.DlcPack or AddonType.OivPackage))
                {
                    Uninstall(old);
                    state.Addons.Remove(old);
                }
            }

            try
            {
                doneBytes = await DownloadAddonAsync(
                    client, addon, addonCacheDir, progress, doneBytes, totalBytes, ct);

                progress?.Report(new SyncProgress(
                    SyncText.T("Kuruluyor: {0}", addon.DisplayName),
                    Fraction(doneBytes, totalBytes)));
                var protectedEntries = old == null
                    ? null
                    : DlclistEntriesStillNeededByOthers(old, state.Addons);
                var installed = Install(addon, addonCacheDir, version, old, protectedEntries);

                if (old != null && addon.Type is (AddonType.DlcPack or AddonType.OivPackage))
                {
                    state.Addons.Remove(old);
                    // Yeni sürüm başarıyla kuruldu; eski sürümün önbelleği artık
                    // kullanılmıyorsa temizle. Yedek hâlâ duruyorsa dokunma.
                    if (addon.Type == AddonType.DlcPack)
                        CleanupOldDlcCache(old, addonCacheDir);
                    else
                        CleanupOldOivCache(old, addonCacheDir);
                }

                state.Addons.Add(installed);
                SyncStateStore.Save(dataFolder, state); // çökme güvenliği: artımlı kayıt
                _log(SyncText.T("Kuruldu: {0} ({1})", addon.DisplayName, addon.Type));
            }
            catch (Exception ex) when (ex is not OperationCanceledException &&
                                       addon.Type is (AddonType.DlcPack or AddonType.OivPackage))
            {
                // Paket hatası tüm senkronizasyonu durdurmasın; diğer modlar kurulur.
                // DLC/OIV kurulumu hata durumunda eski sürümü geri getirmeye çalışır.
                if (addon.Type == AddonType.OivPackage)
                {
                    _log(SyncText.T("UYARI: OIV paketi kurulamadı: {0} ({1})",
                        addon.DisplayName, ex.Message));
                    progress?.Report(new SyncProgress(
                        SyncText.T("Atlandı: {0} (OIV kurulamadı)", addon.DisplayName),
                        Fraction(doneBytes, totalBytes)));
                }
                else
                {
                    _log(SyncText.T("UYARI: DLC paketi kurulamadı: {0} ({1})",
                        addon.DisplayName, ex.Message));
                    progress?.Report(new SyncProgress(
                        SyncText.T("Atlandı: {0} (DLC kurulamadı)", addon.DisplayName),
                        Fraction(doneBytes, totalBytes)));
                }
            }
        }

        state.LastSyncUtc = DateTime.UtcNow;
        SyncStateStore.Save(dataFolder, state);
        AddonPeds.WriteClientConfig(_gameFolder, manifest, state.Addons);
        progress?.Report(new SyncProgress(
            SyncText.T("Senkronizasyon tamamlandı."), 1.0));
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
                var fileStart = doneBytes + addonDone;
                var lastAt = Stopwatch.StartNew();
                long lastBytes = -1;
                var speed = 0.0;

                var fileProgress = new Progress<long>(b =>
                {
                    var elapsed = lastAt.Elapsed.TotalSeconds;
                    if (lastBytes >= 0 && elapsed > 0.2)
                        speed = (b - lastBytes) / elapsed;
                    lastBytes = b;
                    lastAt.Restart();

                    var message = SyncText.Translate(
                        $"İndiriliyor: {addon.DisplayName} / {file.Path} — " +
                        $"{FormatBytes(b)} / {FormatBytes(file.Size)}" +
                        (speed > 1 ? $" ({FormatBytes((long)speed)}/s)" : ""));
                    progress?.Report(new SyncProgress(
                        message, Fraction(fileStart + b, totalBytes),
                        fileStart + b, totalBytes));
                });

                progress?.Report(new SyncProgress(
                    SyncText.Translate($"İndiriliyor: {addon.DisplayName} / {file.Path} " +
                                       $"({FormatBytes(file.Size)})"),
                    Fraction(fileStart, totalBytes), fileStart, totalBytes));
                await client.DownloadFileAsync(addon.Id, file, dest, fileProgress, ct);
            }

            addonDone += file.Size;
            sidecar[file.Path] = file.Sha256;
            progress?.Report(new SyncProgress(
                SyncText.Translate($"İndirildi: {addon.DisplayName} / {file.Path}"),
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

    private static string FormatBytes(long bytes)
    {
        if (bytes >= 1024L * 1024 * 1024) return $"{bytes / 1073741824.0:0.0} GB";
        if (bytes >= 1024L * 1024) return $"{bytes / 1048576.0:0.0} MB";
        if (bytes >= 1024) return $"{bytes / 1024.0:0} KB";
        return $"{bytes} B";
    }

    // ------------------------------------------------------------------ kurulum

    private InstalledAddon Install(AddonInfo addon, string cacheDir, string version,
        InstalledAddon? old = null, ISet<string>? protectedDlclistEntries = null)
    {
        return addon.Type switch
        {
            AddonType.DlcPack => InstallDlcPack(addon, cacheDir, version),
            AddonType.OivPackage => InstallOivPackage(addon, cacheDir, version, old,
                protectedDlclistEntries),
            AddonType.ClientScript => InstallFiles(addon, cacheDir, version, "scripts"),
            AddonType.AsiPlugin => InstallFiles(addon, cacheDir, version, null),
            AddonType.ClientFiles => InstallFiles(addon, cacheDir, version, null),
            _ => throw new SyncException($"Bu eklenti türü istemciye kurulamaz: {addon.Type}")
        };
    }

    /// <summary>
    /// OIV paketini güvenli alt kümeyle kurar. Eski sürümün dosyaları önce
    /// uygulama önbelleğine yedeklenir; yeni sürüm başarısız olursa geri getirilir.
    /// Kullanıcı tarafından değiştirilmiş dosyalar asla taşınmaz/silinmez.
    /// </summary>
    private InstalledAddon InstallOivPackage(AddonInfo addon, string cacheDir, string version,
        InstalledAddon? old, ISet<string>? protectedDlclistEntries)
    {
        if (IsGameRunning())
            throw new SyncException(SyncText.T("GTA V açıkken OIV paketi kurulamaz. Lütfen oyunu kapatın."));

        var packageFile = addon.Files.FirstOrDefault(f =>
            f.Path.EndsWith(".oiv", StringComparison.OrdinalIgnoreCase))
            ?? throw new SyncException(SyncText.T("OIV: paket dosyası manifestte yok."));
        var packagePath = Path.Combine(cacheDir,
            packageFile.Path.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(packagePath))
            throw new SyncException(SyncText.T("OIV paketi önbellekte bulunamadı: {0}", packagePath));

        using var pkg = OivPackage.Open(packagePath, _log);
        _log(SyncText.T("OIV: {0} ({1})", pkg.Name,
            string.IsNullOrWhiteSpace(pkg.Version) ? addon.Id : pkg.Version));

        var oldEntries = old == null ? new List<string>() : InstalledDlclistEntries(old);
        var backupRoot = Path.Combine(
            Path.GetDirectoryName(cacheDir.TrimEnd('\\', '/')) ?? cacheDir,
            ".oiv-backup-" + Safe(addon.Id) + "-" + Guid.NewGuid().ToString("N")[..8]);
        var moved = new List<(string Rel, string Backup)>();
        var copied = new List<(string Rel, string Hash)>();
        var addedEntries = new List<string>();
        var rpfState = new RpfApplyState();
        bool oldRpfRolledBack = false;
        int preserved = 0;

        try
        {
            // 1) Eski sürümün bizim yazdığımız dosyalarını yedekle.
            if (old != null)
            {
                foreach (var rel in old.InstalledPaths.Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    if (!TryGamePath(rel, out var full) || !File.Exists(full)) continue;
                    if (!old.InstalledHashes.TryGetValue(rel, out var known) ||
                        string.IsNullOrWhiteSpace(known))
                    {
                        preserved++;
                        _log(SyncText.T("OIV: bilinmeyen/eski dosya korundu: {0}", rel));
                        continue;
                    }
                    if (!FileHashMatches(full, known))
                    {
                        preserved++;
                        _log(SyncText.T("OIV: kullanıcı dosyası korundu: {0}", rel));
                        continue;
                    }

                    var backup = Path.Combine(backupRoot,
                        rel.Replace('/', Path.DirectorySeparatorChar));
                    MoveFileSafe(full, backup);
                    moved.Add((rel, backup));
                }

                // 1b) Eski sürümün RPF düzenlemelerini geri al. Kullanıcı
                // tarafından değiştirilen girdiler korunur ve yeni sürümde
                // aynı girdiye dokunulmaz.
                if (old.RpfEdits.Count > 0)
                {
                    _log(SyncText.T("OIV: önceki sürümün arşiv düzenlemeleri geri alınıyor."));
                    RollbackRpfEdits(old.RpfEdits, rpfState.Protected);
                    oldRpfRolledBack = true;
                }
            }

            // 2) Yeni dosyaları yaz. Var olan farklı dosyanın üzerine ASLA yazma.
            foreach (var copy in pkg.FileAdds)
            {
                if (!TryGamePath(copy.Destination, out var full))
                    throw new SyncException(SyncText.T("OIV: güvenlik dışı hedef: {0}",
                        copy.Destination));

                if (File.Exists(full))
                {
                    preserved++;
                    _log(SyncText.T("OIV: mevcut dosya korundu: {0}", copy.Destination));
                    continue;
                }

                var hash = pkg.CopyEntryTo(copy, full);
                copied.Add((copy.Destination, hash));
            }

            // 2b) mods klasöründeki mevcut RPF arşivlerine dosya ekle/değiştir.
            if (pkg.ArchiveEdits.Count > 0)
                ApplyRpfEdits(pkg, rpfState);

            // 3) dlclist girdilerini ekle (yalnızca eksik olanlar).
            foreach (var entry in pkg.DlclistEntries)
            {
                if (AddDlclistEntry(entry)) addedEntries.Add(entry);
            }

            var ownedEntries = pkg.DlclistEntries
                .Where(e => oldEntries.Contains(e, StringComparer.OrdinalIgnoreCase) ||
                            addedEntries.Contains(e, StringComparer.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            // 4) Yeni sürümde olmayan eski dlclist girdilerini kaldır.
            bool oldEntriesOk = true;
            foreach (var entry in oldEntries.Except(pkg.DlclistEntries,
                         StringComparer.OrdinalIgnoreCase))
            {
                if (protectedDlclistEntries != null &&
                    protectedDlclistEntries.Contains(entry))
                    continue;
                if (!TryRemoveDlclistEntry(entry)) oldEntriesOk = false;
            }

            if (!oldEntriesOk)
            {
                // Girdi kaldırılamadıysa eski dosyaları geri getir; böylece dlclist
                // var olmayan bir klasöre işaret etmez.
                RestoreMovedFiles(moved);
                _log(SyncText.T(
                    "UYARI: eski OIV dlclist girdisi kaldırılamadı; eski dosyalar korundu."));
            }
            else if (Directory.Exists(backupRoot))
            {
                DeleteDirectoryWithRetry(backupRoot, _log);
            }

            _log(SyncText.T(
                "OIV: {0} dosya kuruldu, {1} dosya korundu, {2} dlclist girdisi eklendi.",
                copied.Count, preserved, addedEntries.Count));

            // Eski sürümün indirme önbelleği artık kullanılmıyor.
            if (old != null) CleanupOldOivCache(old, cacheDir);

            return new InstalledAddon
            {
                Id = addon.Id,
                Type = addon.Type,
                Version = version,
                DlclistEntries = ownedEntries,
                DlclistEntry = ownedEntries.FirstOrDefault() ?? "",
                InstalledPaths = copied.Select(c => c.Rel).ToList(),
                InstalledHashes = copied.ToDictionary(c => c.Rel, c => c.Hash,
                    StringComparer.OrdinalIgnoreCase),
                RpfEdits = rpfState.Records
            };
        }
        catch
        {
            // Yeni eklenenleri geri al; eski sürümü yerine koy.
            foreach (var entry in addedEntries)
                TryRemoveDlclistEntry(entry);
            RollbackRpfEdits(rpfState.Applied, null);
            if (oldRpfRolledBack && old != null)
                ReapplyOldRpfEdits(old, cacheDir);
            foreach (var (rel, hash) in copied)
            {
                if (!TryGamePath(rel, out var full) || !File.Exists(full)) continue;
                try
                {
                    if (FileHashMatches(full, hash)) File.Delete(full);
                }
                catch { }
                PruneEmptyParents(Path.GetDirectoryName(full));
            }
            RestoreMovedFiles(moved);
            if (Directory.Exists(backupRoot))
                DeleteDirectoryWithRetry(backupRoot, _log);
            throw;
        }
    }

    // ------------------------------------------------------------------ RPF arşiv düzenlemeleri

    /// <summary>
    /// Tek bir OIV arşiv girdisinin bellekte tutulabilecek üst sınırı.
    /// Daha büyük dosyalar kurulumu yarıda bırakmamak için baştan reddedilir.
    /// </summary>
    private const long MaxRpfEditBytes = 768L * 1024 * 1024;

    private sealed class RpfApplyState
    {
        public List<RpfEditRecord> Records { get; } = new();
        public List<RpfEditRecord> Applied { get; } = new();

        /// <summary>Kullanıcı değiştirdiği için yeni sürümde dokunulmayacak girdiler.</summary>
        public HashSet<string> Protected { get; } = new(StringComparer.OrdinalIgnoreCase);

        public int Skipped { get; set; }
        public int Preserved { get; set; }
    }

    /// <summary>
    /// OIV paketindeki arşiv düzenlemelerini mods klasöründeki mevcut kopyalara
    /// uygular. Yeni veri arşivin sonuna eklenir; eski blok korunur; yalnızca
    /// küçük TOC/isim başlığı güncellenir. Hiçbir arşiv baştan kopyalanmaz.
    /// </summary>
    private void ApplyRpfEdits(OivPackage pkg, RpfApplyState state,
        IReadOnlyList<RpfEditRecord>? forcedBase = null, bool quiet = false)
    {
        Dictionary<string, RpfEditRecord>? baseByKey = null;
        if (forcedBase != null)
        {
            baseByKey = new Dictionary<string, RpfEditRecord>(StringComparer.OrdinalIgnoreCase);
            foreach (var rec in forcedBase)
                baseByKey[rec.Key] = rec;
        }

        foreach (var group in pkg.ArchiveEdits.GroupBy(
                     e => e.Archive, StringComparer.OrdinalIgnoreCase))
        {
            if (!TryModsArchivePath(group.Key, out var archiveFull) ||
                !File.Exists(archiveFull))
            {
                state.Skipped += group.Count();
                _log(SyncText.T(
                    "OIV: mods arşivi bulunamadı, arşiv düzenlemesi atlandı: {0}",
                    group.Key));
                continue;
            }

            lock (_rpfLock)
            {
                using var archive = RpfArchive.Open(archiveFull);
                var health = archive.VerifyStructure(sampleReads: false);
                if (!health.IsHealthy)
                {
                    throw new SyncException(SyncText.T(
                        "OIV: arşiv yapısı sağlıklı değil, düzenleme yapılmadı: {0}", group.Key));
                }

                foreach (var edit in group)
                {
                    var key = RpfKey(edit.Archive, edit.EntryPath);
                    if (state.Protected.Contains(key))
                    {
                        state.Preserved++;
                        _log(SyncText.T(
                            "OIV: kullanıcı değişikliği korundu (arşiv): {0} :: {1}",
                            edit.Archive, edit.EntryPath));
                        continue;
                    }

                    var entryPath = edit.EntryPath.Replace('/', '\\');
                    var existing = archive.Find(entryPath);
                    var content = pkg.ReadArchiveEntry(edit, MaxRpfEditBytes);
                    bool isResource = RpfCrypto.HasResourceHeader(content);

                    if (existing != null && isResource != (existing is RpfResourceFileEntry))
                    {
                        throw new SyncException(SyncText.T(
                            "OIV: arşiv girdisi türü uyuşmuyor, güvenlik için kurulum durduruldu: " +
                            "{0} :: {1}", edit.Archive, edit.EntryPath));
                    }

                    RpfEntryState original;
                    if (baseByKey != null && baseByKey.TryGetValue(key, out var oldRec))
                    {
                        original = StateFromRecord(oldRec);
                    }
                    else if (existing != null)
                    {
                        original = archive.CaptureState(existing);
                    }
                    else
                    {
                        original = new RpfEntryState { Exists = false, IsResource = isResource };
                    }

                    archive.SetFile(entryPath, content, verify: true, appendOnly: true);
                    var installedHash = isResource
                        ? ResourcePayloadHash(content)
                        : Sha256Hex(content);

                    var record = new RpfEditRecord
                    {
                        Archive = edit.Archive.Replace('\\', '/'),
                        EntryPath = edit.EntryPath.Replace('\\', '/'),
                        WasPresent = original.Exists,
                        IsResource = isResource,
                        OriginalOffset = original.FileOffset,
                        OriginalSize = original.FileSize,
                        OriginalUncompressedSize = original.FileUncompressedSize,
                        OriginalSystemFlags = original.SystemFlags,
                        OriginalGraphicsFlags = original.GraphicsFlags,
                        OriginalEncrypted = original.IsEncrypted,
                        OriginalCompression = original.Compression,
                        InstalledHash = installedHash
                    };

                    state.Records.Add(record);
                    state.Applied.Add(record);
                }
            }
        }

        if (!quiet)
        {
            if (state.Records.Count > 0)
                _log(SyncText.T("OIV: {0} arşiv dosyası eklendi/değiştirildi.",
                    state.Records.Count));
            if (state.Skipped > 0)
                _log(SyncText.T("OIV: {0} arşiv düzenlemesi atlandı (eksik arşiv).",
                    state.Skipped));
        }
    }

    /// <summary>
    /// Başarısız bir güncellemeden sonra eski sürümün arşiv düzenlemelerini
    /// indirme önbelleğindeki .oiv paketinden yeniden uygular.
    /// </summary>
    private void ReapplyOldRpfEdits(InstalledAddon old, string newCacheDir)
    {
        try
        {
            if (old.RpfEdits.Count == 0) return;
            var oldCache = OldOivCacheDir(old, newCacheDir);
            if (oldCache == null || !Directory.Exists(oldCache))
            {
                _log(SyncText.T("UYARI: eski OIV arşiv önbelleği bulunamadı: {0}",
                    old.Version));
                return;
            }

            var oiv = Directory.EnumerateFiles(oldCache, "*.oiv",
                SearchOption.AllDirectories).FirstOrDefault();
            if (oiv == null)
            {
                _log(SyncText.T("UYARI: eski OIV paketi önbellekte yok: {0}", old.Id));
                return;
            }

            using var pkg = OivPackage.Open(oiv, _log);
            var state = new RpfApplyState();
            ApplyRpfEdits(pkg, state, old.RpfEdits, quiet: true);
            _log(SyncText.T("OIV: önceki sürümün arşiv düzenlemeleri geri getirildi."));
        }
        catch (Exception ex)
        {
            _log(SyncText.T("UYARI: önceki OIV arşiv düzenlemeleri geri getirilemedi: {0}",
                ex.Message));
        }
    }

    /// <summary>
    /// Arşiv düzenlemelerini geri alır. Kullanıcı tarafından değiştirilmiş
    /// girdiler asla zorla geri yazılmaz; yalnızca bizim yazdığımız içerik
    /// (SHA-256 eşleşiyorsa) eski TOC durumuna döndürülür.
    /// </summary>
    private int RollbackRpfEdits(IEnumerable<RpfEditRecord> edits,
        ISet<string>? protectedKeys)
    {
        int rolledBack = 0;
        foreach (var rec in edits)
        {
            if (protectedKeys != null && protectedKeys.Contains(rec.Key)) continue;

            var outcome = TryRollbackRpfEdit(rec, out var detail);
            switch (outcome)
            {
                case RpfRollbackOutcome.RolledBack:
                    rolledBack++;
                    break;
                case RpfRollbackOutcome.UserChanged:
                    protectedKeys?.Add(rec.Key);
                    _log(SyncText.T(
                        "OIV: kullanıcı değiştirdiği için arşiv girdisi korundu: {0} :: {1}",
                        rec.Archive, rec.EntryPath));
                    break;
                case RpfRollbackOutcome.Failed:
                    protectedKeys?.Add(rec.Key);
                    _log(SyncText.T(
                        "UYARI: arşiv girdisi geri alınamadı, korundu: {0} :: {1} ({2})",
                        rec.Archive, rec.EntryPath, detail));
                    break;
                default:
                    _log(SyncText.T("OIV: arşiv girdisi zaten yok, atlandı: {0} :: {1}",
                        rec.Archive, rec.EntryPath));
                    break;
            }
        }
        return rolledBack;
    }

    private enum RpfRollbackOutcome
    {
        RolledBack,
        Missing,
        UserChanged,
        Failed
    }

    private RpfRollbackOutcome TryRollbackRpfEdit(RpfEditRecord rec, out string detail)
    {
        detail = "";
        if (!TryModsArchivePath(rec.Archive, out var archiveFull))
        {
            detail = SyncText.T("geçersiz arşiv yolu");
            return RpfRollbackOutcome.Missing;
        }
        if (!File.Exists(archiveFull))
        {
            detail = SyncText.T("arşiv bulunamadı");
            return RpfRollbackOutcome.Missing;
        }

        try
        {
            lock (_rpfLock)
            {
                using var archive = RpfArchive.Open(archiveFull);
                var entry = archive.Find(rec.EntryPath.Replace('/', '\\'));

                if (!rec.WasPresent)
                {
                    if (entry == null) return RpfRollbackOutcome.RolledBack;

                    var current = TryEntryHash(archive, entry, rec.IsResource);
                    if (!HashMatches(current, rec.InstalledHash))
                        return RpfRollbackOutcome.UserChanged;

                    archive.RemoveFile(entry);
                    return RpfRollbackOutcome.RolledBack;
                }

                if (entry == null)
                    return RpfRollbackOutcome.Missing;

                var hash = TryEntryHash(archive, entry, rec.IsResource);
                if (!HashMatches(hash, rec.InstalledHash))
                    return RpfRollbackOutcome.UserChanged;

                archive.RestoreState(rec.EntryPath.Replace('/', '\\'), StateFromRecord(rec));
                return RpfRollbackOutcome.RolledBack;
            }
        }
        catch (Exception ex)
        {
            detail = ex.Message;
            return RpfRollbackOutcome.Failed;
        }
    }

    private bool TryModsArchivePath(string rel, out string full)
    {
        full = "";
        var normalized = (rel ?? "").Replace('\\', '/').TrimStart('/');
        if (!normalized.StartsWith("mods/", StringComparison.OrdinalIgnoreCase))
            return false;
        if (!normalized.EndsWith(".rpf", StringComparison.OrdinalIgnoreCase))
            return false;
        if (normalized.Contains(".rpf/", StringComparison.OrdinalIgnoreCase))
            return false;
        return TryGamePath(normalized, out full);
    }

    private static string? TryEntryHash(RpfArchive archive, RpfFileEntry entry,
        bool isResource)
    {
        try
        {
            var bytes = archive.ReadFile(entry);
            return isResource ? ResourcePayloadHash(bytes) : Sha256Hex(bytes);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Resource içeriğini sıkıştırma farkından bağımsız olarak özetler:
    /// 16 baytlık başlığın bayrakları + açılmış yük. Böylece OpenIV/CodeWalker
    /// farklı DEFLATE seviyesiyle yazsa bile kullanıcı değişikliği doğru ayırt edilir.
    /// </summary>
    private static string ResourcePayloadHash(byte[] raw)
    {
        ReadOnlySpan<byte> flags = raw.Length >= 16 ? raw.AsSpan(8, 8) : ReadOnlySpan<byte>.Empty;
        var payload = raw.Length > 16 ? raw[16..] : Array.Empty<byte>();
        if (RpfCrypto.TryInflateRaw(payload, out var inflated))
            payload = inflated;

        var data = new byte[flags.Length + payload.Length];
        flags.CopyTo(data);
        Buffer.BlockCopy(payload, 0, data, flags.Length, payload.Length);
        return Sha256Hex(data);
    }

    private static bool HashMatches(string? current, string expected) =>
        !string.IsNullOrWhiteSpace(current) &&
        !string.IsNullOrWhiteSpace(expected) &&
        string.Equals(current, expected, StringComparison.OrdinalIgnoreCase);

    private static string Sha256Hex(byte[] bytes) =>
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes))
            .ToLowerInvariant();

    private static RpfEntryState StateFromRecord(RpfEditRecord rec) => new()
    {
        Exists = rec.WasPresent,
        IsResource = rec.IsResource,
        FileOffset = rec.OriginalOffset,
        FileSize = rec.OriginalSize,
        FileUncompressedSize = rec.OriginalUncompressedSize,
        SystemFlags = rec.OriginalSystemFlags,
        GraphicsFlags = rec.OriginalGraphicsFlags,
        IsEncrypted = rec.OriginalEncrypted,
        Compression = rec.OriginalCompression
    };

    private static string RpfKey(string archive, string entryPath) =>
        (archive ?? "").Replace('\\', '/').TrimStart('/') + "|" +
        (entryPath ?? "").Replace('\\', '/').TrimStart('/');

    private static string? OldOivCacheDir(InstalledAddon old, string newCacheDir)
    {
        if (string.IsNullOrWhiteSpace(old.Version)) return null;
        var newCache = Path.GetFullPath(newCacheDir);
        var root = Path.GetDirectoryName(newCache);
        if (string.IsNullOrWhiteSpace(root)) return null;

        var oldCache = Path.GetFullPath(Path.Combine(root,
            Safe(old.Id) + "__" + Safe(old.Version)));
        var prefix = root.TrimEnd('\\', '/') + Path.DirectorySeparatorChar;
        return oldCache.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? oldCache
            : null;
    }

    private bool RpfEditsIntact(IReadOnlyList<RpfEditRecord> edits)
    {
        foreach (var group in edits.GroupBy(
                     e => e.Archive, StringComparer.OrdinalIgnoreCase))
        {
            if (!TryModsArchivePath(group.Key, out var full) || !File.Exists(full))
                return false;
            try
            {
                lock (_rpfLock)
                {
                    using var archive = RpfArchive.Open(full);
                    foreach (var edit in group)
                    {
                        if (archive.Find(edit.EntryPath.Replace('/', '\\')) == null)
                            return false;
                    }
                }
            }
            catch
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// Yedeklenen eski dosyaları geri taşır. Hedefte yeni sürümün dosyası varsa
    /// (başarılı güncelleme) üzerine yazmaz; kullanıcı dosyası varsa da dokunmaz.
    /// </summary>
    private void RestoreMovedFiles(List<(string Rel, string Backup)> moved)
    {
        foreach (var (rel, backup) in moved.AsEnumerable().Reverse())
        {
            if (!File.Exists(backup)) continue;
            if (!TryGamePath(rel, out var full)) continue;
            if (File.Exists(full)) continue;
            try
            {
                MoveFileSafe(backup, full);
            }
            catch (Exception ex)
            {
                _log(SyncText.T("UYARI: eski OIV dosyası geri getirilemedi: {0} ({1})",
                    rel, ex.Message));
            }
        }
    }

    private bool TryGamePath(string rel, out string full)
    {
        full = "";
        var normalized = (rel ?? "").Replace('\\', '/').TrimStart('/');
        if (normalized.Length == 0 || normalized.Contains("..")) return false;
        var root = Path.GetFullPath(_gameFolder).TrimEnd('\\', '/') +
                   Path.DirectorySeparatorChar;
        var candidate = Path.GetFullPath(Path.Combine(_gameFolder,
            normalized.Replace('/', Path.DirectorySeparatorChar)));
        if (!candidate.StartsWith(root, StringComparison.OrdinalIgnoreCase)) return false;
        full = candidate;
        return true;
    }

    private static void MoveFileSafe(string source, string destination)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        if (File.Exists(destination)) File.Delete(destination);
        try
        {
            File.Move(source, destination);
        }
        catch (IOException)
        {
            File.Copy(source, destination, overwrite: true);
            File.Delete(source);
        }
    }

    private static bool FileHashMatches(string path, string expected)
    {
        try
        {
            using var sha = System.Security.Cryptography.SHA256.Create();
            using var fs = File.OpenRead(path);
            var hash = Convert.ToHexString(sha.ComputeHash(fs)).ToLowerInvariant();
            return string.Equals(hash, expected, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private InstalledAddon InstallDlcPack(AddonInfo addon, string cacheDir, string version)
    {
        var mountFolder = MountFolderName(addon.Id);
        var dlcpacksRoot = Path.Combine(_gameFolder, "mods", "update", "x64", "dlcpacks");
        Directory.CreateDirectory(dlcpacksRoot);
        var target = Path.Combine(dlcpacksRoot, mountFolder);
        var staging = target + ".gtacoop-new";
        var backup = target + ".gtacoop-old";

        if (Directory.Exists(staging)) DeleteDirectoryWithRetry(staging, _log);
        // Yedek varsa ve hedef de varsa hedef yarım kalmış olabilir; bilinen
        // eski sürümü geri getir. Yedek tek sağlam kopyaysa asla silme.
        if (Directory.Exists(backup) && Directory.Exists(target))
        {
            RestoreDlcBackup(backup, target);
            if (Directory.Exists(backup))
                throw new SyncException(SyncText.T(
                    "DLC paketi güncellenemedi: eski yedek geri getirilemedi ({0}).",
                    mountFolder));
        }

        string linkTarget = "";
        try
        {
            // Önce yeni sürümü geçici klasöre hazırla; eski sürüm hâlâ yerinde kalsın.
            // Oyun klasörüne GB'larca dosya kopyalamamak için NTFS bağlantısı (junction)
            // denenir. Başarısız olursa normal kopyalamaya düşülür.
            if (TryCreateJunction(staging, cacheDir))
            {
                linkTarget = cacheDir;
                _log(SyncText.T("{0}: oyuna bağlandı (bağlantı ile; kopyalama yapılmadı).",
                    addon.DisplayName));
            }
            else
            {
                CopyListedFiles(addon, cacheDir, staging);
                _log(SyncText.T("{0}: oyuna kopyalandı (bağlantı kurulamadı).", addon.DisplayName));
            }

            // Eski DLC klasörünü silme; başarısızlıkta geri getirmek için yanına taşı.
            if (Directory.Exists(target))
            {
                try
                {
                    Directory.Move(target, backup);
                }
                catch (Exception ex)
                {
                    throw new SyncException(SyncText.T(
                        "DLC paketi güncellenemedi: eski klasör taşınamadı ({0}).", mountFolder), ex);
                }
            }

            try
            {
                Directory.Move(staging, target);
            }
            catch (Exception ex)
            {
                RestoreDlcBackup(backup, target);
                throw new SyncException(SyncText.T(
                    "DLC paketi güncellenemedi: yeni klasör yerleştirilemedi ({0}).", mountFolder), ex);
            }

            var entry = DlclistEntryFor(addon);
            try
            {
                AddDlclistEntry(entry);
            }
            catch
            {
                // dlclist yazılamadıysa yeni klasörü kaldır, eski sürümü geri getir.
                if (Directory.Exists(target)) DeleteDirectoryWithRetry(target, _log);
                RestoreDlcBackup(backup, target);
                throw;
            }

            // Başarılı: eski sürüm yedeğini temizle. Silinemezse klasör kalabilir;
            // dlclist'te karşılığı olmadığı için oyun tarafından yüklenmez.
            if (Directory.Exists(backup))
            {
                try { DeleteDirectoryWithRetry(backup, _log); }
                catch (Exception ex)
                {
                    _log(SyncText.T("UYARI: eski DLC yedeği silinemedi: {0}", ex.Message));
                }
            }

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
        catch
        {
            if (Directory.Exists(staging)) DeleteDirectoryWithRetry(staging, _log);
            throw;
        }
    }

    /// <summary>
    /// Başarılı DLC güncellemesinden sonra eski sürümün önbelleğini siler.
    /// Yalnızca sunucu önbellek kökü altındaki, yeni önbelleğin üstü olmayan
    /// ve artık .gtacoop-old yedeği tarafından kullanılmayan klasörler silinir.
    /// </summary>
    private void CleanupOldDlcCache(InstalledAddon old, string newCacheDir)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(old.LinkTarget) ||
                string.IsNullOrWhiteSpace(old.MountFolder))
                return;

            var oldCache = Path.GetFullPath(old.LinkTarget);
            var newCache = Path.GetFullPath(newCacheDir);
            if (string.Equals(oldCache, newCache, StringComparison.OrdinalIgnoreCase))
                return;

            var cacheRoot = Path.GetDirectoryName(newCache);
            if (string.IsNullOrWhiteSpace(cacheRoot)) return;
            var rootPrefix = cacheRoot.TrimEnd('\\', '/') + Path.DirectorySeparatorChar;
            if (!oldCache.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
                return;

            // Yalnızca bu eklentiye ait önbellek klasörünü temizle.
            // Ad tam eşleşmeli (eski biçim) veya "<id>__<sürüm>" olmalı;
            // böylece "testdlc_other" gibi başka bir eklentinin önbelleği silinmez.
            var oldName = Path.GetFileName(oldCache);
            var cacheKey = Safe(old.Id);
            if (!string.Equals(oldName, cacheKey, StringComparison.OrdinalIgnoreCase) &&
                !oldName.StartsWith(cacheKey + "__", StringComparison.OrdinalIgnoreCase))
                return;

            // Eski önbellek yeni önbelleğin üst klasörüyse silme.
            var oldPrefix = oldCache.TrimEnd('\\', '/') + Path.DirectorySeparatorChar;
            if (newCache.StartsWith(oldPrefix, StringComparison.OrdinalIgnoreCase))
                return;

            // .gtacoop-old hâlâ eski önbelleğe işaret ediyorsa koru.
            var backup = Path.Combine(_gameFolder, "mods", "update", "x64",
                "dlcpacks", old.MountFolder + ".gtacoop-old");
            if (Directory.Exists(backup)) return;

            if (!Directory.Exists(oldCache)) return;
            DeleteDirectoryWithRetry(oldCache, _log);
            if (!Directory.Exists(oldCache))
                _log(SyncText.T("Eski DLC önbelleği temizlendi: {0}", old.MountFolder));
        }
        catch (Exception ex)
        {
            _log(SyncText.T("UYARI: eski DLC önbelleği temizlenemedi: {0}", ex.Message));
        }
    }

    /// <summary>
    /// OIV güncellemesi başarılı olduktan sonra eski sürümün indirme önbelleğini
    /// temizler. Yalnızca bu eklentiye ait "&lt;id&gt;__&lt;sürüm&gt;" klasörü silinir.
    /// </summary>
    private void CleanupOldOivCache(InstalledAddon old, string newCacheDir)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(old.Version)) return;

            var newCache = Path.GetFullPath(newCacheDir);
            var cacheRoot = Path.GetDirectoryName(newCache);
            if (string.IsNullOrWhiteSpace(cacheRoot)) return;

            var oldCache = Path.GetFullPath(Path.Combine(cacheRoot,
                Safe(old.Id) + "__" + Safe(old.Version)));
            if (string.Equals(oldCache, newCache, StringComparison.OrdinalIgnoreCase))
                return;

            var rootPrefix = cacheRoot.TrimEnd('\\', '/') + Path.DirectorySeparatorChar;
            if (!oldCache.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
                return;
            if (!Directory.Exists(oldCache)) return;

            DeleteDirectoryWithRetry(oldCache, _log);
            if (!Directory.Exists(oldCache))
                _log(SyncText.T("Eski OIV önbelleği temizlendi: {0}", old.Id));
        }
        catch (Exception ex)
        {
            _log(SyncText.T("UYARI: eski OIV önbelleği temizlenemedi: {0}", ex.Message));
        }
    }

    /// <summary>DLC güncellemesi başarısızsa eski klasörü hedefe geri taşır.</summary>
    private void RestoreDlcBackup(string backup, string target)
    {
        if (!Directory.Exists(backup)) return;
        try
        {
            if (Directory.Exists(target)) DeleteDirectoryWithRetry(target, _log);
            Directory.Move(backup, target);
            _log(SyncText.T("DLC: önceki sürüm geri getirildi."));
        }
        catch (Exception ex)
        {
            _log(SyncText.T("UYARI: önceki DLC sürümü geri getirilemedi: {0}", ex.Message));
        }
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

    /// <summary>Yalnızca indirme: eklentileri sunucu içerik klasörüne alır, oyuna KURMAZ.</summary>
    public async Task PrefetchAsync(
        SyncClient client, Manifest manifest, string serverCacheDir,
        IProgress<SyncProgress>? progress = null, CancellationToken ct = default)
    {
        var addons = manifest.Addons.Where(a => a.Type != AddonType.ServerPlugin).ToList();
        long totalBytes = addons.Sum(a => a.TotalSize);
        long doneBytes = 0;

        foreach (var addon in addons)
        {
            ct.ThrowIfCancellationRequested();
            var version = ServerScanner.ComputeVersion(addon);
            // Ön indirme de DLC'lerde sürüm başına ayrı önbellek kullanmalı;
            // aksi halde eski sürüme junction olan hedef klasörün dosyaları ezilir.
            var addonCacheDir = addon.Type is (AddonType.DlcPack or AddonType.OivPackage)
                ? Path.Combine(serverCacheDir, Safe(addon.Id) + "__" + Safe(version))
                : Path.Combine(serverCacheDir, Safe(addon.Id));
            doneBytes = await DownloadAddonAsync(
                client, addon, addonCacheDir, progress, doneBytes, totalBytes, ct);
        }

        progress?.Report(new SyncProgress(
            SyncText.T("Tüm mod dosyaları indirildi (oyuna kurulmadı)."), 1.0));
    }

    /// <summary>ClientScript / ClientFiles kurulumu (oyun köküne göreli kopyalama).</summary>
    private InstalledAddon InstallFiles(AddonInfo addon, string cacheDir, string version, string? forceRoot)
    {
        var installed = new InstalledAddon { Id = addon.Id, Type = addon.Type, Version = version };
        int copied = 0, skipped = 0;

        foreach (var file in addon.Files)
        {
            var rel = file.Path.Replace('\\', '/').TrimStart('/');
            if (forceRoot != null)
            {
                // Sunucudaki klasör yapısı bazen "scripts/..." ile gelir
                // (ör. bir mod arşivi MyMod\scripts\MyMod.dll olarak paketlenmişse).
                // Bu durumda ikinci kez "scripts/" eklemek dosyayı
                // scripts\scripts\... altına kurar ve oyun modu yüklemez.
                var root = forceRoot.TrimEnd('/');
                if (!rel.StartsWith(root + "/", StringComparison.OrdinalIgnoreCase))
                    rel = root + "/" + rel;
            }

            if (rel.Contains(".."))
                throw new SyncException($"Güvenlik: geçersiz hedef yol: {rel}");

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
                if (!string.IsNullOrWhiteSpace(file.Sha256))
                    installed.InstalledHashes[rel] = file.Sha256;
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
    public void Uninstall(InstalledAddon inst, string? quarantineRoot = null,
        ISet<string>? protectedDlclistEntries = null,
        ISet<string>? protectedRpfKeys = null)
    {
        switch (inst.Type)
        {
            case AddonType.DlcPack:
            {
                // dlclist girdisi kaldırılamadıysa klasörü SİLME/TAŞIMA: aksi hâlde
                // oyun var olmayan bir DLC paketini yüklemeye çalışıp hata verebilir.
                bool dlclistOk = string.IsNullOrEmpty(inst.DlclistEntry) ||
                                 TryRemoveDlclistEntry(inst.DlclistEntry);
                if (!dlclistOk)
                {
                    _log(SyncText.T(
                        "UYARI: dlclist girdisi kaldırılamadı; DLC klasörü korundu: {0}",
                        inst.MountFolder));
                    break;
                }

                var relDir = Path.Combine("mods", "update", "x64", "dlcpacks", inst.MountFolder);
                var dir = Path.Combine(_gameFolder, relDir);
                if (Directory.Exists(dir))
                {
                    if (quarantineRoot != null) MoveDirectoryToQuarantine(dir, relDir, quarantineRoot);
                    else DeleteDirectoryWithRetry(dir, _log);
                }
                break;
            }
            case AddonType.OivPackage:
            {
                // Önce dlclist girdilerini kaldır. Kaldırılamazsa dosyaları silme:
                // aksi hâlde oyun var olmayan bir paketi yüklemeye çalışabilir.
                var entries = InstalledDlclistEntries(inst);
                bool entriesOk = true;
                foreach (var entry in entries)
                {
                    // Başka bir kurulu paket bu girdiye ait klasörü sağlıyorsa
                    // girdiyi bırak; aksi hâlde o modun DLC'si yüklenmez.
                    if (protectedDlclistEntries != null &&
                        protectedDlclistEntries.Contains(entry))
                        continue;
                    if (!TryRemoveDlclistEntry(entry)) entriesOk = false;
                }
                if (!entriesOk)
                {
                    _log(SyncText.T(
                        "UYARI: OIV dlclist girdisi kaldırılamadı; dosyalar korundu."));
                    break;
                }

                // Arşiv düzenlemelerini geri al. Kullanıcı değiştirdiği girdiler
                // ve başka bir kurulu paketin hâlâ kullandığı girdiler korunur.
                if (inst.RpfEdits.Count > 0)
                    RollbackRpfEdits(inst.RpfEdits, protectedRpfKeys);

                foreach (var relPath in inst.InstalledPaths)
                {
                    var full = Path.Combine(_gameFolder,
                        relPath.Replace('/', Path.DirectorySeparatorChar));
                    try
                    {
                        if (!File.Exists(full)) continue;
                        if (inst.InstalledHashes.TryGetValue(relPath, out var known) &&
                            !string.IsNullOrWhiteSpace(known) && !FileHashMatches(full, known))
                        {
                            _log(SyncText.T("Kullanıcı dosyası korundu: {0}", relPath));
                            continue;
                        }

                        if (quarantineRoot != null)
                            MoveFileToQuarantine(full, relPath, quarantineRoot);
                        else
                            File.Delete(full);
                        PruneEmptyParents(Path.GetDirectoryName(full));
                    }
                    catch (Exception ex)
                    {
                        _log($"UYARI: kaldırılamadı: {relPath} ({ex.Message})");
                    }
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
                        if (inst.InstalledHashes.TryGetValue(relPath, out var known) &&
                            !string.IsNullOrWhiteSpace(known) && !FileHashMatches(full, known))
                        {
                            _log(SyncText.T("Kullanıcı dosyası korundu: {0}", relPath));
                            continue;
                        }
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
            var protectedRpf = state.Addons
                .Where(other => !ReferenceEquals(other, inst))
                .SelectMany(other => other.RpfEdits)
                .Select(rec => rec.Key)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            Uninstall(inst, quarantine,
                DlclistEntriesStillNeededByOthers(inst, state.Addons),
                protectedRpf);
            state.Addons.Remove(inst);
            SyncStateStore.Save(dataFolder, state); // çökme güvenliği: artımlı kayıt
        }
        state.LastSyncUtc = DateTime.UtcNow;
        SyncStateStore.Save(dataFolder, state);
        AddonPeds.DeleteClientConfig(_gameFolder);

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
            Path.Combine(_gameFolder, "mods", "GTACoop").TrimEnd('\\', '/'),
            Path.Combine(_gameFolder, "mods", "update").TrimEnd('\\', '/'),
            Path.Combine(_gameFolder, "mods", "update", "x64").TrimEnd('\\', '/'),
            Path.Combine(_gameFolder, "mods", "update", "x64", "dlcpacks").TrimEnd('\\', '/')
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
    /// update.rpf düzenlemeye uygun mu? Otomatik geri yükleme YAPMAZ: kullanıcının
    /// mevcut modlarını eski bir yedekle ezmek riskli olabilir. Büyük ama yapısal
    /// olarak sağlıklı arşivlerde yalnızca yerinde yazmaya izin verir; bozuk arşivde
    /// net bir hata verir ve dosyaya hiç dokunmaz. Onarım kullanıcıya bırakılır
    /// (Launcher > Yedekler).
    /// </summary>
    private void EnsureUpdateRpfUsable()
    {
        if (!File.Exists(UpdateRpfPath))
            throw new SyncException(SyncText.T(
                "mods/update/update.rpf bulunamadı. DLC paketleri için OpenIV ile mods klasörü oluşturulmalı."));

        if (BackupService.IsUpdateRpfHealthy(UpdateRpfPath, out var reason))
            return;

        // 4 GB'a yakın olsa bile yapısal olarak sağlıklıysa yalnızca yerinde
        // yazma denenecek. ReplaceFile(requireInPlace: true) sığmazsa hata verir.
        if (BackupService.IsUpdateRpfStructurallyHealthy(UpdateRpfPath, out _) &&
            new FileInfo(UpdateRpfPath).Length > BackupService.MaxEditableUpdateRpfBytes)
        {
            _log(SyncText.T("UYARI: update.rpf büyük; yalnızca yerinde dlclist düzenlemesi denenecek."));
            return;
        }

        _log(SyncText.T("UYARI: update.rpf düzenlemeye uygun değil: {0}",
            SyncText.Translate(reason)));
        throw new SyncException(SyncText.T(
            "update.rpf sağlıksız/çok büyük; otomatik geri yükleme yapılmadı. " +
            "OpenIV ile mods klasörünü onarın veya Launcher > Yedekler bölümünden geri dönün."));
    }

    /// <summary>Girdi eklendiyse true, zaten varsa false döner.</summary>
    public bool AddDlclistEntry(string entry)
    {
        if (IsGameRunning())
            throw new SyncException(SyncText.T("GTA V açıkken dlclist düzenlenemez. Lütfen oyunu kapatın."));
        if (!File.Exists(UpdateRpfPath))
            throw new SyncException(SyncText.T(
                "mods/update/update.rpf bulunamadı. DLC/OIV paketleri için OpenIV ile mods klasörü oluşturulmalı."));

        // Girdi zaten varsa update.rpf'ye yazma gerekmez. Bu sayede büyük/bozuk
        // arşivlerde bile yalnızca DLC dosyaları güncellenebilir; arşive dokunulmaz.
        // Okunamıyorsa (null) normal akışa devam edilir; EnsureUpdateRpfUsable
        // net bir hata verir.
        if (DlclistEntryExists(entry) == true)
        {
            _log(SyncText.T("dlclist: '{0}' zaten kayıtlı, değişiklik yok.", entry));
            return false;
        }

        EnsureUpdateRpfUsable();
        BackupService.EnsureUpdateRpfBackup(_gameFolder, _log);
        bool large = new FileInfo(UpdateRpfPath).Length > BackupService.MaxEditableUpdateRpfBytes;

        lock (_rpfLock)
        {
            using var archive = RpfArchive.Open(UpdateRpfPath);
            var file = archive.Find(Dlclist.RelativePath)
                ?? throw new SyncException(SyncText.T("update.rpf içinde dlclist.xml bulunamadı."));

            var oldBytes = archive.ReadFile(file);
            var (xml, enc) = Dlclist.Decode(oldBytes);
            var existing = Dlclist.GetPackNames(xml);
            if (existing.Contains(entry, StringComparer.OrdinalIgnoreCase))
            {
                _log(SyncText.T("dlclist: '{0}' zaten kayıtlı, değişiklik yok.", entry));
                return false;
            }

            if (large)
                BackupService.BackupDlclist(_gameFolder, oldBytes, _log);

            var updated = Dlclist.AddPackNames(xml, new[] { entry });
            ValidateXml(updated);
            try
            {
                ReplaceDlclist(archive, file, updated, enc, requireInPlace: large);
            }
            catch (Exception ex) when (large)
            {
                TryRollbackDlclist(archive, file, xml, enc);
                throw new SyncException(SyncText.T(
                    "update.rpf çok büyük ve dlclist.xml yerinde güncellenemedi; " +
                    "mevcut dosya korundu."), ex);
            }

            _log(SyncText.T("dlclist: eklendi -> {0}", entry));
        }
        return true;
    }

    public void RemoveDlclistEntry(string entry)
    {
        if (IsGameRunning())
            throw new SyncException(SyncText.T("GTA V açıkken dlclist düzenlenemez. Lütfen oyunu kapatın."));
        if (!File.Exists(UpdateRpfPath))
            throw new SyncException(SyncText.T(
                "mods/update/update.rpf bulunamadı. dlclist girdisi çıkarılamıyor."));

        var exists = DlclistEntryExists(entry);
        if (exists == false)
        {
            _log(SyncText.T("dlclist: '{0}' kayıtlı değil, değişiklik yok.", entry));
            return;
        }

        // exists == null: arşiv okunamadı. EnsureUpdateRpfUsable net hata verir;
        // böylece çağıran DLC klasörünü yanlışlıkla silmez.
        EnsureUpdateRpfUsable();
        BackupService.EnsureUpdateRpfBackup(_gameFolder, _log);
        bool large = new FileInfo(UpdateRpfPath).Length > BackupService.MaxEditableUpdateRpfBytes;

        lock (_rpfLock)
        {
            using var archive = RpfArchive.Open(UpdateRpfPath);
            var file = archive.Find(Dlclist.RelativePath)
                ?? throw new SyncException(SyncText.T("update.rpf içinde dlclist.xml bulunamadı."));

            var oldBytes = archive.ReadFile(file);
            var (xml, enc) = Dlclist.Decode(oldBytes);
            var updated = Dlclist.RemovePackNames(xml, new[] { entry });
            ValidateXml(updated);

            if (large)
                BackupService.BackupDlclist(_gameFolder, oldBytes, _log);

            try
            {
                ReplaceDlclist(archive, file, updated, enc, requireInPlace: large);
            }
            catch (Exception ex) when (large)
            {
                TryRollbackDlclist(archive, file, xml, enc);
                throw new SyncException(SyncText.T(
                    "update.rpf çok büyük ve dlclist.xml yerinde güncellenemedi; " +
                    "mevcut dosya korundu."), ex);
            }

            _log(SyncText.T("dlclist: çıkarıldı -> {0}", entry));
        }
    }

    /// <summary>
    /// dlclist'te girdi var mı? true/false; arşiv okunamıyorsa null döner.
    /// Çağıran null durumunda EnsureUpdateRpfUsable ile net hata üretmelidir.
    /// </summary>
    private bool? DlclistEntryExists(string entry)
    {
        try
        {
            using var archive = RpfArchive.Open(UpdateRpfPath);
            var file = archive.Find(Dlclist.RelativePath);
            if (file == null) return false;
            var (xml, _) = Dlclist.Decode(archive.ReadFile(file));
            return Dlclist.GetPackNames(xml).Contains(entry, StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Yerinde yazma başarısız olursa eski dlclist içeriğini geri yazmayı dener.</summary>
    private void TryRollbackDlclist(RpfArchive archive, RpfFileEntry file, string oldXml,
        Dlclist.DlclistEncoding enc)
    {
        try
        {
            ReplaceDlclist(archive, file, oldXml, enc, requireInPlace: true);
            _log(SyncText.T("dlclist: önceki içerik geri yazıldı."));
        }
        catch (Exception ex)
        {
            _log(SyncText.T("UYARI: dlclist geri alınamadı: {0}", ex.Message));
        }
    }

    private bool TryRemoveDlclistEntry(string entry)
    {
        try
        {
            RemoveDlclistEntry(entry);
            return true;
        }
        catch (Exception ex)
        {
            _log(SyncText.T("UYARI: dlclist girdisi çıkarılamadı ({0}): {1}",
                entry, ex.Message));
            return false;
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
    private void ReplaceDlclist(RpfArchive archive, RpfFileEntry file, string updated,
        Dlclist.DlclistEncoding enc, bool requireInPlace = false)
    {
        var writeEnc = Dlclist.Normalize(updated, enc);
        if (writeEnc != enc)
            _log(SyncText.T("dlclist: kodlama beyana uygun UTF-16'ya düzeltildi (OpenIV uyumu)."));
        archive.ReplaceFile(file, Dlclist.Encode(updated, writeEnc),
            requireInPlace: requireInPlace);
    }

    /// <summary>dlclist'te gcs_ ile başlayan sahipsiz girdileri ve klasörleri temizler.</summary>
    public void PruneOrphanMounts()
    {
        var dlcpacksRoot = Path.Combine(_gameFolder, "mods", "update", "x64", "dlcpacks");
        bool dlclistCleanupOk = true;

        // ÖNCE dlclist girdilerini temizle. Bu başarısız olursa gcs_ klasörlerini
        // ASLA silmeyiz: aksi hâlde oyun var olmayan DLC paketlerini yüklemeye
        // çalışıp hata verebilir. update.rpf yoksa klasörler zaten yüklenemez.
        try
        {
            if (IsGameRunning())
                throw new SyncException(SyncText.T("GTA V açık; atlandı."));

            if (File.Exists(UpdateRpfPath))
            {
                EnsureUpdateRpfUsable();

                lock (_rpfLock)
                {
                    using var archive = RpfArchive.Open(UpdateRpfPath);
                    var file = archive.Find(Dlclist.RelativePath);
                    if (file == null)
                        throw new SyncException(SyncText.T("update.rpf içinde dlclist.xml bulunamadı."));

                    var (xml, enc) = Dlclist.Decode(archive.ReadFile(file));
                    var orphans = Dlclist.GetPackNames(xml)
                        .Where(n => n.StartsWith("gcs_", StringComparison.OrdinalIgnoreCase) ||
                                    n.Contains("/gcs_", StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    if (orphans.Count == 0)
                    {
                        _log(SyncText.T("dlclist: temizlenecek gcs_ girdisi yok."));
                    }
                    else
                    {
                        BackupService.EnsureUpdateRpfBackup(_gameFolder, _log);
                        var oldBytes = archive.ReadFile(file);
                        bool large = new FileInfo(UpdateRpfPath).Length >
                                     BackupService.MaxEditableUpdateRpfBytes;
                        if (large)
                            BackupService.BackupDlclist(_gameFolder, oldBytes, _log);
                        var updated = Dlclist.RemovePackNames(xml, orphans);
                        ValidateXml(updated);
                        try
                        {
                            ReplaceDlclist(archive, file, updated, enc, requireInPlace: large);
                        }
                        catch when (large)
                        {
                            TryRollbackDlclist(archive, file, xml, enc);
                            throw;
                        }
                        _log(SyncText.T("dlclist: {0} gcs_ girdisi temizlendi.", orphans.Count));
                    }
                }
            }
        }
        catch (Exception ex)
        {
            dlclistCleanupOk = false;
            _log(SyncText.T("dlclist temizliği atlandı: {0}", ex.Message));
        }

        if (!dlclistCleanupOk)
        {
            _log(SyncText.T(
                "UYARI: dlclist temizlenemedi; gcs_ klasörleri korundu."));
            return;
        }

        // dlclist temizliği başarılıysa artık gcs_ klasörlerini silebiliriz.
        if (Directory.Exists(dlcpacksRoot))
        {
            foreach (var dir in Directory.GetDirectories(dlcpacksRoot, "gcs_*"))
            {
                var name = Path.GetFileName(dir);
                // Başarısız bir DLC güncellemesinden kalan eski sürüm yedeği,
                // hedef klasör yoksa silinmez; böylece kullanıcının paketi kaybolmaz.
                if (name.EndsWith(".gtacoop-old", StringComparison.OrdinalIgnoreCase))
                {
                    var target = dir[..^".gtacoop-old".Length];
                    if (!Directory.Exists(target))
                    {
                        _log(SyncText.T("UYARI: eski DLC yedeği korunuyor: {0}", name));
                        continue;
                    }
                }

                _log(SyncText.T("Temizleniyor: {0}", name));
                DeleteDirectoryWithRetry(dir, _log);
            }
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
