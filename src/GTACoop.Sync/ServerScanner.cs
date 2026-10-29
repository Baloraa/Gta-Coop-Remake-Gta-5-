using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace GTACoop.Sync;

/// <summary>Sunucu eklenti klasörünü tarayıp manifest üretir.</summary>
public sealed class ServerScanner
{
    public const string AddonsDirName = "Addons";
    public const int ManifestVersion = 1;

    private readonly string _addonsDir;
    private readonly string _cachePath;
    private Dictionary<string, CacheEntry> _cache = new();
    private readonly object _scanLock = new();

    public ServerScanner(string addonsDir)
    {
        _addonsDir = addonsDir;
        _cachePath = Path.Combine(addonsDir, ".gtacoop-scan-cache.json");
        LoadCache();
    }

    /// <summary>Sunucu klasör yapısını oluşturur.</summary>
    public static void EnsureLayout(string addonsDir)
    {
        foreach (var sub in new[] { "dlcpacks", "oiv", "scripts", "asi", "files", "plugins" })
            Directory.CreateDirectory(Path.Combine(addonsDir, sub));
    }

    public sealed class ScanResult
    {
        public Manifest Manifest { get; set; } = new();
        public List<string> Warnings { get; set; } = new();

        /// <summary>"eklentiId/dosyaYolu" -> disk yolu eşlemesi (güvenli dosya sunumu için).</summary>
        internal Dictionary<string, string> PhysicalFiles { get; } =
            new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Manifest'te listelenen bir dosyanın fiziksel yolunu bulur.</summary>
        public bool TryGetPhysicalPath(string addonId, string relPath, out string fullPath)
        {
            var key = addonId + "/" + relPath.Replace('\\', '/').TrimStart('/');
            return PhysicalFiles.TryGetValue(key, out fullPath!);
        }
    }

    public ScanResult Scan(string serverName, string serverId, int syncPort)
    {
        lock (_scanLock)
        {
            return ScanCore(serverName, serverId, syncPort);
        }
    }

    private ScanResult ScanCore(string serverName, string serverId, int syncPort)
    {
        var result = new ScanResult();
        var manifest = result.Manifest;
        manifest.ServerName = serverName;
        manifest.ServerId = serverId;
        manifest.SyncPort = syncPort;
        manifest.GeneratedUtc = DateTime.UtcNow;

        if (!Directory.Exists(_addonsDir))
        {
            result.Warnings.Add($"Eklenti klasörü yok: {_addonsDir}");
            SaveCache();
            return result;
        }

        var before = _cache.Count;

        ScanDlcPacks(manifest, result);
        ScanOivPackages(manifest, result);
        ScanScripts(manifest, result);
        ScanAsi(manifest, result);
        ScanFiles(manifest, result);
        ScanPlugins(manifest, result);

        SaveCache();
        result.Warnings.Add($"Tarama tamamlandı: {manifest.Addons.Count} eklenti " +
            $"({before} önbellek girdisi yeniden kullanıldı/kontrol edildi).");
        return result;
    }

    private void ScanDlcPacks(Manifest manifest, ScanResult result)
    {
        var dir = Path.Combine(_addonsDir, "dlcpacks");
        if (!Directory.Exists(dir)) return;

        foreach (var packDir in Directory.GetDirectories(dir))
        {
            var id = Path.GetFileName(packDir);
            if (id.StartsWith('.')) continue;

            // En sığ dlc.rpf'i bul
            var rpf = Directory.EnumerateFiles(packDir, "dlc.rpf", SearchOption.AllDirectories)
                .OrderBy(p => p.Count(c => c == Path.DirectorySeparatorChar))
                .FirstOrDefault();

            if (rpf == null)
            {
                result.Warnings.Add($"DlcPack '{id}' atlandı: içinde dlc.rpf bulunamadı.");
                continue;
            }

            var relPackPath = Path.GetRelativePath(packDir, Path.GetDirectoryName(rpf)!);
            if (relPackPath == ".") relPackPath = "";

            var meta = ReadAddonMeta(packDir);
            manifest.Addons.Add(new AddonInfo
            {
                Id = id,
                Type = AddonType.DlcPack,
                DisplayName = meta?.DisplayName ?? id,
                Description = meta?.Description ?? "",
                Required = meta?.Required ?? true,
                RelativePackPath = relPackPath.Replace('\\', '/'),
                Files = HashTree(packDir, result, id)
            });
        }
    }

    private void ScanOivPackages(Manifest manifest, ScanResult result)
    {
        var dir = Path.Combine(_addonsDir, "oiv");
        if (!Directory.Exists(dir)) return;

        foreach (var packDir in Directory.GetDirectories(dir))
        {
            var id = Path.GetFileName(packDir);
            if (id.StartsWith('.')) continue;

            // Her OIV paketi kendi klasöründe durur; klasör içinde tek .oiv beklenir.
            var oiv = Directory.EnumerateFiles(packDir, "*.oiv", SearchOption.AllDirectories)
                .OrderBy(p => p.Count(c => c == Path.DirectorySeparatorChar))
                .FirstOrDefault();
            if (oiv == null)
            {
                result.Warnings.Add($"OIV '{id}' atlandı: içinde .oiv dosyası bulunamadı.");
                continue;
            }

            try
            {
                using var pkg = OivPackage.Open(oiv);
                var files = HashTree(packDir, result, id);
                if (files.Count == 0)
                {
                    result.Warnings.Add($"OIV '{id}' atlandı: boş klasör.");
                    continue;
                }

                var meta = ReadAddonMeta(packDir);
                manifest.Addons.Add(new AddonInfo
                {
                    Id = id,
                    Type = AddonType.OivPackage,
                    DisplayName = meta?.DisplayName ?? pkg.Name,
                    Description = meta?.Description ??
                        (pkg.Description.Length > 0 ? pkg.Description : ""),
                    Version = pkg.Version,
                    Required = meta?.Required ?? true,
                    Files = files,
                    OivTargets = pkg.FileAdds.Select(f => f.Destination).ToList(),
                    OivDlclistEntries = pkg.DlclistEntries.ToList(),
                    OivUnsupportedCount = pkg.Unsupported.Count
                });
            }
            catch (Exception ex)
            {
                result.Warnings.Add($"OIV '{id}' atlandı: {ex.Message}");
            }
        }
    }

    /// <summary>OIV klasöründeki paketin görünen adını okur (sunucu mod listesi için).</summary>
    public static string? ReadOivDisplayName(string dir)
    {
        try
        {
            var oiv = Directory.EnumerateFiles(dir, "*.oiv", SearchOption.AllDirectories)
                .OrderBy(p => p.Count(c => c == Path.DirectorySeparatorChar))
                .FirstOrDefault();
            return oiv == null ? null : OivPackage.ReadDisplayName(oiv);
        }
        catch
        {
            return null;
        }
    }

    private void ScanScripts(Manifest manifest, ScanResult result)
    {
        var dir = Path.Combine(_addonsDir, "scripts");
        if (!Directory.Exists(dir)) return;

        foreach (var scriptDir in Directory.GetDirectories(dir))
        {
            var id = Path.GetFileName(scriptDir);
            if (id.StartsWith('.')) continue;

            var files = HashTree(scriptDir, result, id);
            if (files.Count == 0)
            {
                result.Warnings.Add($"ClientScript '{id}' atlandı: boş klasör.");
                continue;
            }

            var meta = ReadAddonMeta(scriptDir);
            manifest.Addons.Add(new AddonInfo
            {
                Id = id,
                Type = AddonType.ClientScript,
                DisplayName = meta?.DisplayName ?? id,
                Description = meta?.Description ?? "",
                Required = meta?.Required ?? true,
                Files = files
            });
        }
    }

    private void ScanAsi(Manifest manifest, ScanResult result)
    {
        var dir = Path.Combine(_addonsDir, "asi");
        if (!Directory.Exists(dir)) return;

        foreach (var asiDir in Directory.GetDirectories(dir))
        {
            var id = Path.GetFileName(asiDir);
            if (id.StartsWith('.')) continue;

            var files = HashTree(asiDir, result, id);
            var valid = new List<AddonFileInfo>();
            foreach (var f in files)
            {
                if (IsAllowedAsiFile(f.Path)) valid.Add(f);
                else result.Warnings.Add(
                    $"ASI '{id}': '{f.Path}' atlandı (oyun köküne yalnızca şu uzantılar kurulur: " +
                    string.Join(", ", AsiExtensions) + ").");
            }

            if (valid.Count == 0)
            {
                result.Warnings.Add($"ASI '{id}' atlandı: geçerli dosya yok " +
                    "(alt klasörler desteklenmez, dosyalar doğrudan asi\\<ad> içine konmalı).");
                continue;
            }

            var meta = ReadAddonMeta(asiDir);
            manifest.Addons.Add(new AddonInfo
            {
                Id = id,
                Type = AddonType.AsiPlugin,
                DisplayName = meta?.DisplayName ?? id,
                Description = meta?.Description ?? "",
                Required = meta?.Required ?? true,
                Files = valid
            });
        }
    }

    /// <summary>ASI eklentileri için oyun köküne kurulabilecek dosya uzantıları.</summary>
    public static readonly string[] AsiExtensions =
        { ".asi", ".ini", ".xml", ".pdb", ".log", ".txt", ".cfg", ".config" };

    /// <summary>ASI dosyası oyun köküne kurulabilir mi? (tek dosya adı + izinli uzantı)</summary>
    public static bool IsAllowedAsiFile(string relPath)
    {
        var p = relPath.Replace('\\', '/').TrimStart('/');
        if (p.Contains("..") || p.Contains('/')) return false;
        var ext = Path.GetExtension(p);
        return AsiExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase);
    }

    private void ScanFiles(Manifest manifest, ScanResult result)
    {
        var dir = Path.Combine(_addonsDir, "files");
        if (!Directory.Exists(dir)) return;

        foreach (var fileDir in Directory.GetDirectories(dir))
        {
            var id = Path.GetFileName(fileDir);
            if (id.StartsWith('.')) continue;

            var files = HashTree(fileDir, result, id);
            var valid = new List<AddonFileInfo>();
            foreach (var f in files)
            {
                if (IsAllowedClientPath(f.Path)) valid.Add(f);
                else result.Warnings.Add(
                    $"ClientFiles '{id}': '{f.Path}' atlandı (yalnızca scripts/ ve mods/GTACoop/ yollarına izin var).");
            }

            if (valid.Count == 0)
            {
                result.Warnings.Add($"ClientFiles '{id}' atlandı: geçerli dosya yok.");
                continue;
            }

            var meta = ReadAddonMeta(fileDir);
            manifest.Addons.Add(new AddonInfo
            {
                Id = id,
                Type = AddonType.ClientFiles,
                DisplayName = meta?.DisplayName ?? id,
                Description = meta?.Description ?? "",
                Required = meta?.Required ?? true,
                Files = valid
            });
        }
    }

    private void ScanPlugins(Manifest manifest, ScanResult result)
    {
        var dir = Path.Combine(_addonsDir, "plugins");
        if (!Directory.Exists(dir)) return;

        // Tekil dll dosyaları
        foreach (var dll in Directory.GetFiles(dir, "*.dll"))
        {
            var id = Path.GetFileNameWithoutExtension(dll);
            manifest.Addons.Add(new AddonInfo
            {
                Id = id,
                Type = AddonType.ServerPlugin,
                DisplayName = id,
                Required = true,
                Files = new List<AddonFileInfo> { HashFile(Path.GetFileName(dll), dll) }
            });
            result.PhysicalFiles[id + "/" + Path.GetFileName(dll)] = dll;
        }

        // Alt klasörler (eklenti + bağımlılıkları)
        foreach (var sub in Directory.GetDirectories(dir))
        {
            var id = Path.GetFileName(sub);
            if (id.StartsWith('.')) continue;
            var files = HashTree(sub, result, id);
            if (files.Count == 0) continue;

            var meta = ReadAddonMeta(sub);
            manifest.Addons.Add(new AddonInfo
            {
                Id = id,
                Type = AddonType.ServerPlugin,
                DisplayName = meta?.DisplayName ?? id,
                Description = meta?.Description ?? "",
                Required = meta?.Required ?? true,
                Files = files
            });
        }
    }

    /// <summary>ClientFiles hedef yolu izinli mi? (güvenlik sınırı)</summary>
    public static bool IsAllowedClientPath(string relPath)
    {
        var p = relPath.Replace('\\', '/').TrimStart('/');
        if (p.Contains("..")) return false;
        return p.StartsWith("scripts/", StringComparison.OrdinalIgnoreCase)
            || p.StartsWith("mods/GTACoop/", StringComparison.OrdinalIgnoreCase);
    }

    private List<AddonFileInfo> HashTree(string root, ScanResult result, string addonId)
    {
        var list = new List<AddonFileInfo>();
        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(root, file).Replace('\\', '/');
            if (rel.StartsWith('.')) continue;
            // addon.json yalnızca meta veridir; istemciye içerik olarak gönderilmez.
            if (rel.Equals("addon.json", StringComparison.OrdinalIgnoreCase)) continue;
            list.Add(HashFile(rel, file));
            result.PhysicalFiles[addonId + "/" + rel] = file;
        }
        list.Sort((a, b) => string.CompareOrdinal(a.Path, b.Path));
        return list;
    }

    private AddonFileInfo HashFile(string rel, string full)
    {
        var fi = new FileInfo(full);
        var key = full.ToLowerInvariant();

        if (_cache.TryGetValue(key, out var hit) &&
            hit.Size == fi.Length &&
            hit.Mtime == fi.LastWriteTimeUtc.Ticks &&
            !string.IsNullOrEmpty(hit.Sha256))
        {
            return new AddonFileInfo { Path = rel, Size = fi.Length, Sha256 = hit.Sha256 };
        }

        using var sha = SHA256.Create();
        using (var fs = File.OpenRead(full))
        {
            var hash = Convert.ToHexString(sha.ComputeHash(fs)).ToLowerInvariant();
            _cache[key] = new CacheEntry { Size = fi.Length, Mtime = fi.LastWriteTimeUtc.Ticks, Sha256 = hash };
            return new AddonFileInfo { Path = rel, Size = fi.Length, Sha256 = hash };
        }
    }

    private sealed class CacheEntry
    {
        public long Size { get; set; }
        public long Mtime { get; set; }
        public string Sha256 { get; set; } = "";
    }

    private void LoadCache()
    {
        try
        {
            if (File.Exists(_cachePath))
            {
                _cache = JsonSerializer.Deserialize<Dictionary<string, CacheEntry>>(
                    File.ReadAllText(_cachePath)) ?? new();
            }
        }
        catch { _cache = new(); }
    }

    private void SaveCache()
    {
        try
        {
            var tmp = _cachePath + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(_cache,
                new JsonSerializerOptions { WriteIndented = false }));
            File.Move(tmp, _cachePath, overwrite: true);
        }
        catch { /* önbellek kaydı kritik değil */ }
    }

    private sealed class AddonMeta
    {
        public string? DisplayName { get; set; }
        public string? Description { get; set; }
        public string? Version { get; set; }
        public bool? Required { get; set; }
    }

    private static AddonMeta? ReadAddonMeta(string dir)
    {
        var path = Path.Combine(dir, "addon.json");
        if (!File.Exists(path)) return null;
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            var root = doc.RootElement;
            return new AddonMeta
            {
                DisplayName = root.TryGetProperty("displayName", out var dn) ? dn.GetString() : null,
                Description = root.TryGetProperty("description", out var de) ? de.GetString() : null,
                Version = root.TryGetProperty("version", out var v) ? v.GetString() : null,
                Required = root.TryGetProperty("required", out var r) && r.ValueKind is JsonValueKind.True or JsonValueKind.False
                    ? r.GetBoolean() : null
            };
        }
        catch { return null; }
    }

    /// <summary>Eklenti sürümü: dosya listesinden türetilen kısa karma.</summary>
    public static string ComputeVersion(AddonInfo addon)
    {
        var sb = new StringBuilder();
        foreach (var f in addon.Files.OrderBy(f => f.Path, StringComparer.Ordinal))
            sb.Append(f.Path).Append(':').Append(f.Sha256).Append(';');
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
        return Convert.ToHexString(hash)[..12].ToLowerInvariant();
    }
}
