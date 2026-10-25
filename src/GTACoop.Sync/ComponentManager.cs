using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace GTACoop.Sync;

/// <summary>Launcher'ın oyun köküne kurabildiği temel bileşenler.</summary>
public enum ComponentKind
{
    ScriptHookV,
    ScriptHookVDotNet,
    OpenIV,
    OpenIvAlternative
}

/// <summary>ScriptHookVDotNet dağıtım kanalı.</summary>
public enum ShvdnChannel
{
    Stable,
    Nightly,
    Enhanced
}

/// <summary>Bir bileşen için indirilebilir sürüm.</summary>
public sealed class ComponentRelease
{
    public ComponentKind Kind { get; set; }
    public ShvdnChannel Channel { get; set; }
    public string Tag { get; set; } = "";
    public string Version { get; set; } = "";
    public string AssetName { get; set; } = "";
    public string DownloadUrl { get; set; } = "";
    public string Source { get; set; } = "";
    public string Notes { get; set; } = "";
    public bool Prerelease { get; set; }
    public DateTimeOffset PublishedAt { get; set; }
    public long DownloadSize { get; set; }

    public string Key => $"{Kind}:{Channel}:{Tag}";
}

/// <summary>Kurulmuş bileşenlerin kalıcı kaydı (components.json).</summary>
public sealed class InstalledComponentsState
{
    public List<InstalledComponent> Items { get; set; } = new();
}

public sealed class InstalledComponent
{
    public ComponentKind Kind { get; set; }
    public ShvdnChannel Channel { get; set; }
    public string Tag { get; set; } = "";
    public string Version { get; set; } = "";
    public string Source { get; set; } = "";
    public DateTime InstalledUtc { get; set; }
    public string BackupDir { get; set; } = "";
    public List<InstalledComponentFile> Files { get; set; } = new();
}

public sealed class InstalledComponentFile
{
    public string RelativePath { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public long Size { get; set; }
}

/// <summary>Kurulumdan önce alınan dosya yedeği.</summary>
public sealed class ComponentBackupInfo
{
    public ComponentKind Kind { get; set; }
    public ShvdnChannel Channel { get; set; }
    public string Tag { get; set; } = "";
    public string Version { get; set; } = "";
    public string Source { get; set; } = "";
    public DateTime CreatedUtc { get; set; }
    public List<ComponentBackupFile> Files { get; set; } = new();
}

public sealed class ComponentBackupFile
{
    public string RelativePath { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public long Size { get; set; }
}

/// <summary>Diskte bulunan bir bileşen yedeği.</summary>
public sealed class ComponentBackup
{
    public string Directory { get; set; } = "";
    public ComponentBackupInfo Info { get; set; } = new();
}

public sealed class ComponentInstallResult
{
    public bool Installed { get; set; }
    public bool AlreadyCurrent { get; set; }
    public string SkipReason { get; set; } = "";
    public int Copied { get; set; }
    public int Preserved { get; set; }
    public int Unchanged { get; set; }
    public string BackupDir { get; set; } = "";
    public string Message { get; set; } = "";
    public List<string> Details { get; set; } = new();
}

public sealed class ComponentRestoreResult
{
    public int Restored { get; set; }
    public string BackupDir { get; set; } = "";
    public string Version { get; set; } = "";
}

/// <summary>
/// ScriptHookV, ScriptHookVDotNet (stable/nightly/enhanced) ve RageOpenV'yi
/// resmî/GitHub kaynaklarından indirir; kullanıcı tarafından sağlanan OpenIV.asi'yi
/// uygulama paketinden kurar.
///
/// Önemli güvenlik kuralları:
/// - ScriptHookV/SHVDN/RageOpenV indirilir; OpenIV.asi yalnızca paket içindeki
///   doğrulanmış kullanıcı dosyasından alınır.
/// - Yalnızca bilinen dosyalar oyun köküne kopyalanır; scripts/ ve mods/ taranmaz.
/// - NativeTrainer.asi hiçbir zaman kurulmaz; mevcut dosyalar silinmez.
/// - Değiştirilen her dosya önce mods/GTACoop/backups/components altına yedeklenir.
/// </summary>
public static class ComponentManager
{
    private const string StateFileName = "components.json";
    private const int CacheHours = 6;
    private const long MaxDownloadBytes = 64L * 1024 * 1024;
    private const long MaxEntryBytes = 32L * 1024 * 1024;
    private const string ShvPage = "https://www.dev-c.com/gtav/scripthookv/";
    private const string BundledOpenIvSha256 =
        "B12DDC9CE256DC89A9C8BB77D6DD1C61683D18F0884097458658AB48BD1A4EC2";
    private const string BrowserUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
        "(KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36";

    private static readonly HttpClient Http = CreateHttpClient(
        "GTACoop-Remake/2.2 (+https://github.com/)");

    private static readonly HttpClient BrowserHttp = CreateHttpClient(BrowserUserAgent);
    private static readonly SemaphoreSlim InstallLock = new(1, 1);

    private static HttpClient CreateHttpClient(string userAgent)
    {
        var handler = new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip |
                                     DecompressionMethods.Deflate |
                                     DecompressionMethods.Brotli,
            AllowAutoRedirect = true,
            ConnectTimeout = TimeSpan.FromSeconds(20)
        };
        var client = new HttpClient(handler) { Timeout = TimeSpan.FromMinutes(2) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);
        return client;
    }

    // ------------------------------------------------------------------ kaynaklar

    /// <summary>Bileşenin sürüm listesini getirir. Önbellek 6 saat geçerlidir.</summary>
    public static async Task<List<ComponentRelease>> GetReleasesAsync(
        ComponentKind kind,
        ShvdnChannel channel,
        string dataFolder,
        bool forceRefresh,
        CancellationToken cancellationToken = default,
        Action<string>? log = null)
    {
        log ??= _ => { };
        if (kind == ComponentKind.OpenIV)
        {
            var bundled = GetBundledOpenIvRelease();
            log($"Paket içi OpenIV.asi hazır: {bundled.Version}");
            return new List<ComponentRelease> { bundled };
        }

        Directory.CreateDirectory(dataFolder);
        var cachePath = CachePath(dataFolder, kind, channel);
        var cache = SyncJson.Load<ComponentCache>(cachePath);
        if (cache != null) cache.Releases ??= new();

        if (!forceRefresh && cache != null &&
            DateTimeOffset.UtcNow - cache.FetchedUtc < TimeSpan.FromHours(CacheHours))
        {
            log($"Bileşen sürümleri önbellekten okundu: {kind}/{channel}");
            return cache.Releases;
        }

        try
        {
            var releases = kind switch
            {
                ComponentKind.ScriptHookV => await FetchScriptHookVAsync(cancellationToken),
                ComponentKind.ScriptHookVDotNet => await FetchGitHubAsync(kind, channel, cancellationToken),
                ComponentKind.OpenIvAlternative => await FetchGitHubAsync(kind, channel, cancellationToken),
                _ => throw new SyncException("Bilinmeyen bileşen.")
            };

            releases = releases
                .Where(r => !string.IsNullOrWhiteSpace(r.Tag) &&
                            !string.IsNullOrWhiteSpace(r.DownloadUrl))
                .GroupBy(r => r.Tag, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .OrderByDescending(r => r.PublishedAt)
                .ToList();

            if (releases.Count == 0)
                throw new SyncException("Kaynakta indirilebilir sürüm bulunamadı.");

            SyncJson.Save(cachePath, new ComponentCache
            {
                FetchedUtc = DateTimeOffset.UtcNow,
                Releases = releases
            });
            log($"Bileşen sürümleri alındı: {kind}/{channel} ({releases.Count})");
            return releases;
        }
        catch (Exception ex)
        {
            if (cache != null && cache.Releases.Count > 0)
            {
                log($"Bileşen sürümleri yenilenemedi ({ex.Message}); önbellek kullanılıyor.");
                return cache.Releases;
            }
            throw;
        }
    }

    private static string GetBundledOpenIvPathOrThrow()
    {
        var candidates = new List<string>();
        void AddBase(string? baseDir)
        {
            if (string.IsNullOrWhiteSpace(baseDir)) return;
            candidates.Add(Path.Combine(baseDir, "Distribution", "Components", "OpenIV.asi"));
            candidates.Add(Path.Combine(baseDir, "Components", "OpenIV.asi"));
        }

        AddBase(AppContext.BaseDirectory);
        AddBase(Path.GetDirectoryName(Environment.ProcessPath));

        var path = candidates.FirstOrDefault(File.Exists);
        if (path == null)
            throw new SyncException("Paket içi OpenIV.asi bulunamadı. Launcher paketini yeniden kurun.");

        var info = new FileInfo(path);
        if (info.Length <= 0 || info.Length > MaxEntryBytes)
            throw new SyncException("Paket içi OpenIV.asi boyutu geçersiz.");

        var hash = HashFile(path);
        if (!hash.Equals(BundledOpenIvSha256, StringComparison.OrdinalIgnoreCase))
            throw new SyncException("Paket içi OpenIV.asi doğrulanamadı (hash uyuşmuyor).");

        return path;
    }

    private static ComponentRelease GetBundledOpenIvRelease()
    {
        var path = GetBundledOpenIvPathOrThrow();
        var version = FileVersionInfo.GetVersionInfo(path).FileVersion;
        if (string.IsNullOrWhiteSpace(version)) version = "1.2.0.1";

        return new ComponentRelease
        {
            Kind = ComponentKind.OpenIV,
            Channel = ShvdnChannel.Stable,
            Tag = $"openiv-{version}-{BundledOpenIvSha256[..12].ToLowerInvariant()}",
            Version = version,
            AssetName = "OpenIV.asi",
            DownloadUrl = "",
            Source = "Paket içi OpenIV.asi",
            Notes = "Kullanıcı tarafından sağlanan OpenIV.asi.",
            PublishedAt = File.GetLastWriteTimeUtc(path),
            DownloadSize = new FileInfo(path).Length
        };
    }

    private static async Task<List<ComponentRelease>> FetchScriptHookVAsync(
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, ShvPage);
        request.Headers.Accept.ParseAdd("text/html,application/xhtml+xml");
        request.Headers.Referrer = new Uri(ShvPage);

        using var response = await BrowserHttp.SendAsync(
            request, HttpCompletionOption.ResponseContentRead, cancellationToken);
        var html = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new SyncException($"Resmî ScriptHookV sayfası alınamadı (HTTP {(int)response.StatusCode}).");

        // Sayfadaki bağlantı göreli olabilir: /files/ScriptHookV_3889.0_1158.13.zip
        var match = Regex.Match(
            html,
            @"/files/ScriptHookV_(?<game>[0-9.]+)_(?<build>[0-9.]+)\.zip",
            RegexOptions.IgnoreCase);
        if (!match.Success)
            throw new SyncException("Resmî ScriptHookV indirme bağlantısı bulunamadı.");

        var game = match.Groups["game"].Value;
        var build = match.Groups["build"].Value;
        var url = new Uri(new Uri(ShvPage), match.Value).ToString();

        return new List<ComponentRelease>
        {
            new()
            {
                Kind = ComponentKind.ScriptHookV,
                Channel = ShvdnChannel.Stable,
                Tag = $"ScriptHookV_{game}_{build}",
                Version = $"v{game} / {build}",
                AssetName = Path.GetFileName(new Uri(url).LocalPath),
                DownloadUrl = url,
                Source = "dev-c.com (resmî)",
                Notes = "Alexander Blade'in resmî ScriptHookV sürümü.",
                PublishedAt = DateTimeOffset.UtcNow
            }
        };
    }

    private static async Task<List<ComponentRelease>> FetchGitHubAsync(
        ComponentKind kind,
        ShvdnChannel channel,
        CancellationToken cancellationToken)
    {
        var repo = kind switch
        {
            ComponentKind.ScriptHookVDotNet => channel switch
            {
                ShvdnChannel.Stable => "scripthookvdotnet/scripthookvdotnet",
                ShvdnChannel.Nightly => "scripthookvdotnet/scripthookvdotnet-nightly",
                ShvdnChannel.Enhanced => "Chiheb-Bacha/ScriptHookVDotNetEnhanced",
                _ => throw new SyncException("Bilinmeyen ScriptHookVDotNet kanalı.")
            },
            ComponentKind.OpenIvAlternative => "Chiheb-Bacha/RageOpenV",
            _ => throw new SyncException("Bu bileşen GitHub'dan gelmiyor.")
        };

        var releases = new List<ComponentRelease>();
        for (var page = 1; page <= 5; page++)
        {
            var url = $"https://api.github.com/repos/{repo}/releases?per_page=100&page={page}";
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Accept.ParseAdd("application/vnd.github+json");
            request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");

            using var response = await Http.SendAsync(
                request, HttpCompletionOption.ResponseContentRead, cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new SyncException(
                    $"GitHub sürümleri alınamadı (HTTP {(int)response.StatusCode}). " +
                    "API sınırına takılmış olabilir; bir süre sonra yeniden deneyin.");
            }

            var pageItems = ParseGitHubReleases(kind, channel, repo, json);
            releases.AddRange(pageItems);
            if (pageItems.Count < 100) break;
        }

        return releases;
    }

    private static List<ComponentRelease> ParseGitHubReleases(
        ComponentKind kind,
        ShvdnChannel channel,
        string repo,
        string json)
    {
        var result = new List<ComponentRelease>();
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Array) return result;

        foreach (var item in document.RootElement.EnumerateArray())
        {
            if (item.TryGetProperty("draft", out var draft) && draft.GetBoolean()) continue;

            var tag = GetString(item, "tag_name");
            if (string.IsNullOrWhiteSpace(tag)) continue;

            var assets = item.TryGetProperty("assets", out var assetsElement) &&
                         assetsElement.ValueKind == JsonValueKind.Array
                ? assetsElement.EnumerateArray().ToList()
                : new List<JsonElement>();

            var asset = SelectAsset(kind, assets);
            if (asset == null) continue;

            var assetName = GetString(asset.Value, "name");
            var url = GetString(asset.Value, "browser_download_url");
            if (!IsAllowedDownloadUrl(kind, url)) continue;

            var size = asset.Value.TryGetProperty("size", out var sizeElement) &&
                       sizeElement.TryGetInt64(out var parsedSize)
                ? parsedSize
                : 0;

            var published = DateTimeOffset.MinValue;
            var publishedText = GetString(item, "published_at");
            if (!string.IsNullOrWhiteSpace(publishedText))
                DateTimeOffset.TryParse(publishedText, out published);

            result.Add(new ComponentRelease
            {
                Kind = kind,
                Channel = channel,
                Tag = tag,
                Version = tag,
                AssetName = assetName,
                DownloadUrl = url,
                Source = "GitHub · " + repo,
                Notes = GetString(item, "name"),
                Prerelease = item.TryGetProperty("prerelease", out var pre) && pre.GetBoolean(),
                PublishedAt = published,
                DownloadSize = size
            });
        }

        return result;
    }

    private static JsonElement? SelectAsset(ComponentKind kind, List<JsonElement> assets)
    {
        var zips = assets
            .Where(a => GetString(a, "name").EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (zips.Count == 0) return null;

        if (kind == ComponentKind.OpenIvAlternative)
        {
            var exact = zips.Where(a =>
                    GetString(a, "name").Equals("RageOpenV.zip", StringComparison.OrdinalIgnoreCase))
                .Cast<JsonElement?>()
                .FirstOrDefault();
            if (exact != null) return exact;

            var named = zips.Where(a =>
                    GetString(a, "name").Contains("RageOpenV", StringComparison.OrdinalIgnoreCase) &&
                    !GetString(a, "name").Contains("MoreLogs", StringComparison.OrdinalIgnoreCase))
                .Cast<JsonElement?>()
                .FirstOrDefault();
            return named ?? zips[0];
        }

        return zips.Where(a =>
                GetString(a, "name").Contains("ScriptHookVDotNet", StringComparison.OrdinalIgnoreCase))
            .Cast<JsonElement?>()
            .FirstOrDefault() ?? zips[0];
    }

    private static string GetString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? ""
            : "";

    private static bool IsAllowedDownloadUrl(ComponentKind kind, string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps)
            return false;

        return kind == ComponentKind.ScriptHookV
            ? uri.Host.Equals("www.dev-c.com", StringComparison.OrdinalIgnoreCase) ||
              uri.Host.Equals("dev-c.com", StringComparison.OrdinalIgnoreCase)
            : uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase);
    }

    private static string CachePath(string dataFolder, ComponentKind kind, ShvdnChannel channel) =>
        Path.Combine(dataFolder, "component-cache",
            $"{kind.ToString().ToLowerInvariant()}-{channel.ToString().ToLowerInvariant()}.json");

    private sealed class ComponentCache
    {
        public DateTimeOffset FetchedUtc { get; set; }
        public List<ComponentRelease> Releases { get; set; } = new();
    }

    // ------------------------------------------------------------------ kurulum

    /// <summary>Bileşeni indirir, yedekleri alır ve oyun köküne kurar.</summary>
    public static async Task<ComponentInstallResult> InstallAsync(
        ComponentKind kind,
        ShvdnChannel channel,
        ComponentRelease release,
        string gameFolder,
        string dataFolder,
        Action<string>? log = null,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        log ??= _ => { };
        progress ??= new Progress<double>();
        if (!GameFolder.IsValid(gameFolder))
            throw new SyncException("GTA V klasörü geçerli değil.");
        if (AddonApplier.IsGameRunning())
            throw new SyncException("GTA V açıkken bileşen kurulamaz. Önce oyunu kapatın.");
        if (kind != release.Kind)
            throw new SyncException("Bileşen türü ile seçilen sürüm uyuşmuyor.");
        if (channel != release.Channel)
            throw new SyncException("Bileşen kanalı ile seçilen sürüm uyuşmuyor.");

        var installed = GetInstalled(kind, dataFolder);
        if (installed != null &&
            installed.Tag.Equals(release.Tag, StringComparison.OrdinalIgnoreCase) &&
            installed.Channel == channel &&
            IsIntact(installed, gameFolder))
        {
            return new ComponentInstallResult
            {
                AlreadyCurrent = true,
                SkipReason = "already-current",
                Message = "Bu bileşen zaten kurulu ve dosyaları sağlam."
            };
        }

        // OpenIV.asi/OpenRPF.asi/RageOpenV.asi aynı mods-folder köprüsünü açar.
        // Mevcut kullanıcı dosyasını ezmeden yalnızca uyumlu durumda kur.
        if (kind == ComponentKind.OpenIV)
        {
            var bundledPath = GetBundledOpenIvPathOrThrow();
            var bundledHash = HashFile(bundledPath);
            var target = Path.Combine(gameFolder, "OpenIV.asi");
            if (File.Exists(Path.Combine(gameFolder, "OpenRPF.asi")))
                return Skipped("openrpf-present", "OpenRPF.asi zaten kurulu; OpenIV.asi kurulmadı.");
            if (File.Exists(Path.Combine(gameFolder, "RageOpenV.asi")))
                return Skipped("rageopenv-present", "RageOpenV.asi zaten kurulu; OpenIV.asi kurulmadı.");
            if (File.Exists(target))
            {
                var targetHash = HashFile(target);
                var previousFile = installed?.Files?.FirstOrDefault(f =>
                    f.RelativePath.Equals("OpenIV.asi", StringComparison.OrdinalIgnoreCase));
                if (!targetHash.Equals(bundledHash, StringComparison.OrdinalIgnoreCase) &&
                    (previousFile == null ||
                     !targetHash.Equals(previousFile.Sha256, StringComparison.OrdinalIgnoreCase)))
                    return Skipped("openiv-present", "OpenIV.asi zaten kurulu; mevcut dosya korundu.");
            }
        }
        else if (kind == ComponentKind.OpenIvAlternative)
        {
            if (File.Exists(Path.Combine(gameFolder, "OpenIV.asi")))
                return Skipped("openiv-present", "OpenIV.asi zaten kurulu; RageOpenV kurulmadı.");
            if (File.Exists(Path.Combine(gameFolder, "OpenRPF.asi")))
                return Skipped("openrpf-present", "OpenRPF.asi zaten kurulu; RageOpenV kurulmadı.");
            var ragePath = Path.Combine(gameFolder, "RageOpenV.asi");
            if (File.Exists(ragePath))
            {
                var previousFile = installed?.Files?.FirstOrDefault(f =>
                    f.RelativePath.Equals("RageOpenV.asi", StringComparison.OrdinalIgnoreCase));
                if (previousFile == null ||
                    !HashFile(ragePath).Equals(previousFile.Sha256, StringComparison.OrdinalIgnoreCase))
                    return Skipped("rageopenv-present", "RageOpenV.asi zaten kurulu; mevcut dosya korundu.");
            }
        }

        await InstallLock.WaitAsync(cancellationToken);
        string? stagingRoot = null;
        try
        {
            log(kind == ComponentKind.OpenIV
                ? $"Bileşen hazırlanıyor: {release.Version} ({release.Source})"
                : $"Bileşen indiriliyor: {release.Version} ({release.Source})");
            var staging = kind == ComponentKind.OpenIV
                ? StageBundledOpenIv()
                : await DownloadAndStageAsync(kind, release, progress, log, cancellationToken);
            stagingRoot = staging.RootDirectory;
            if (AddonApplier.IsGameRunning())
                throw new SyncException("GTA V açıkken bileşen kurulamaz. Önce oyunu kapatın.");
            var result = ApplyStaged(kind, channel, release, staging, gameFolder, dataFolder, log);
            return result;
        }
        finally
        {
            if (stagingRoot != null) TryDeleteDirectory(stagingRoot);
            InstallLock.Release();
        }
    }

    private static ComponentInstallResult Skipped(string reason, string message) => new()
    {
        SkipReason = reason,
        Message = message
    };

    private static ComponentInstallResult ApplyStaged(
        ComponentKind kind,
        ShvdnChannel channel,
        ComponentRelease release,
        Staging staging,
        string gameFolder,
        string dataFolder,
        Action<string> log)
    {
        var state = LoadState(dataFolder);
        var previous = state.Items.FirstOrDefault(i => i.Kind == kind);
        var installedFiles = new List<InstalledComponentFile>();
        var backupInfo = new ComponentBackupInfo
        {
            Kind = kind,
            Channel = previous?.Channel ?? channel,
            Tag = previous?.Tag ?? "",
            Version = previous?.Version ?? "",
            Source = previous?.Source ?? "",
            CreatedUtc = DateTime.UtcNow
        };
        string? backupDir = null;
        var changes = new List<AppliedChange>();
        var result = new ComponentInstallResult();

        try
        {
            foreach (var file in staging.Files)
            {
                var target = SafeTarget(gameFolder, file.TargetRelative);
                var exists = File.Exists(target);
                var targetHash = exists ? HashFile(target) : "";
                var previousFile = previous?.Files?.FirstOrDefault(f =>
                    f.RelativePath.Equals(file.TargetRelative, StringComparison.OrdinalIgnoreCase));
                // Yalnızca bizim kurduğumuz ve kullanıcı tarafından değiştirilmemiş
                // yardımcı dosyalar güncellenir. Kullanıcının sonradan koyduğu
                // loader/ini dosyalarına dokunulmaz.
                var managed = previousFile != null && exists &&
                              targetHash.Equals(previousFile.Sha256, StringComparison.OrdinalIgnoreCase);

                if (exists && file.Policy == FilePolicy.PreserveAlways)
                {
                    result.Preserved++;
                    result.Details.Add($"Korundu (kullanıcı ayarı): {file.TargetRelative}");
                    log($"Bileşen: mevcut {file.TargetRelative} korundu.");
                    continue;
                }

                if (exists && file.Policy == FilePolicy.PreserveIfUnmanaged && !managed)
                {
                    result.Preserved++;
                    result.Details.Add($"Korundu (kullanıcı dosyası): {file.TargetRelative}");
                    log($"Bileşen: mevcut {file.TargetRelative} korundu (kullanıcı dosyası).");
                    continue;
                }

                var sourceHash = HashFile(file.StagedPath);
                var sourceSize = new FileInfo(file.StagedPath).Length;

                if (exists)
                {
                    if (targetHash.Equals(sourceHash, StringComparison.OrdinalIgnoreCase))
                    {
                        installedFiles.Add(new InstalledComponentFile
                        {
                            RelativePath = file.TargetRelative,
                            Sha256 = targetHash,
                            Size = new FileInfo(target).Length
                        });
                        result.Unchanged++;
                        continue;
                    }

                    backupDir ??= CreateBackupDirectory(kind, gameFolder);
                    var backupPath = BackupFile(target, file.TargetRelative, backupDir, backupInfo);
                    changes.Add(new AppliedChange(target, backupPath));
                }
                else
                {
                    changes.Add(new AppliedChange(target, null));
                }

                CopyReplacing(file.StagedPath, target);
                installedFiles.Add(new InstalledComponentFile
                {
                    RelativePath = file.TargetRelative,
                    Sha256 = sourceHash,
                    Size = sourceSize
                });
                result.Copied++;
                result.Details.Add($"Kuruldu: {file.TargetRelative}");
            }

            var item = new InstalledComponent
            {
                Kind = kind,
                Channel = channel,
                Tag = release.Tag,
                Version = release.Version,
                Source = release.Source,
                InstalledUtc = DateTime.UtcNow,
                BackupDir = backupDir ?? previous?.BackupDir ?? "",
                Files = installedFiles
                    .GroupBy(f => f.RelativePath, StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.First())
                    .ToList()
            };

            state.Items.RemoveAll(i => i.Kind == kind);
            if (item.Files.Count > 0) state.Items.Add(item);

            if (backupDir != null && backupInfo.Files.Count > 0)
            {
                SyncJson.Save(Path.Combine(backupDir, "backup.json"), backupInfo);
                result.BackupDir = backupDir;
                log($"Bileşen yedeği: {backupDir}");
            }

            SyncJson.Save(StatePath(dataFolder), state);
            result.Installed = true;
            result.Message = "Bileşen kuruldu.";
            log($"Bileşen kuruldu: {kind}/{channel} {release.Version}; " +
                $"kopyalanan {result.Copied}, korunan {result.Preserved}, aynı {result.Unchanged}.");
            return result;
        }
        catch
        {
            Rollback(changes, log);
            throw;
        }
    }

    private sealed record AppliedChange(string Target, string? BackupPath);

    private enum FilePolicy
    {
        Replace,
        PreserveIfUnmanaged,
        PreserveAlways
    }

    private sealed class StagedFile
    {
        public string StagedPath { get; init; } = "";
        public string TargetRelative { get; init; } = "";
        public FilePolicy Policy { get; init; }
    }

    private sealed class Staging
    {
        public string RootDirectory { get; init; } = "";
        public List<StagedFile> Files { get; init; } = new();
    }

    private sealed class FileSpec
    {
        public string SourcePath { get; init; } = "";
        public string BaseName { get; init; } = "";
        public string TargetRelative { get; init; } = "";
        public FilePolicy Policy { get; init; }
        public bool Required { get; init; }
    }

    private static List<FileSpec> GetFileSpecs(ComponentKind kind) => kind switch
    {
        ComponentKind.ScriptHookV => new List<FileSpec>
        {
            new() { SourcePath = "bin/ScriptHookV.dll", BaseName = "ScriptHookV.dll", TargetRelative = "ScriptHookV.dll", Policy = FilePolicy.Replace, Required = true },
            new() { SourcePath = "bin/dinput8.dll", BaseName = "dinput8.dll", TargetRelative = "dinput8.dll", Policy = FilePolicy.PreserveIfUnmanaged },
            new() { SourcePath = "bin/xinput1_4.dll", BaseName = "xinput1_4.dll", TargetRelative = "xinput1_4.dll", Policy = FilePolicy.PreserveIfUnmanaged }
        },
        ComponentKind.ScriptHookVDotNet => new List<FileSpec>
        {
            new() { SourcePath = "ScriptHookVDotNet.asi", BaseName = "ScriptHookVDotNet.asi", TargetRelative = "ScriptHookVDotNet.asi", Policy = FilePolicy.Replace, Required = true },
            new() { SourcePath = "ScriptHookVDotNet2.dll", BaseName = "ScriptHookVDotNet2.dll", TargetRelative = "ScriptHookVDotNet2.dll", Policy = FilePolicy.Replace },
            new() { SourcePath = "ScriptHookVDotNet3.dll", BaseName = "ScriptHookVDotNet3.dll", TargetRelative = "ScriptHookVDotNet3.dll", Policy = FilePolicy.Replace },
            new() { SourcePath = "ScriptHookVDotNet.ini", BaseName = "ScriptHookVDotNet.ini", TargetRelative = "ScriptHookVDotNet.ini", Policy = FilePolicy.PreserveAlways },
            new() { SourcePath = "MinHook.x64.dll", BaseName = "MinHook.x64.dll", TargetRelative = "MinHook.x64.dll", Policy = FilePolicy.PreserveIfUnmanaged }
        },
        ComponentKind.OpenIV => new List<FileSpec>
        {
            new() { SourcePath = "OpenIV.asi", BaseName = "OpenIV.asi", TargetRelative = "OpenIV.asi", Policy = FilePolicy.Replace, Required = true }
        },
        ComponentKind.OpenIvAlternative => new List<FileSpec>
        {
            new() { SourcePath = "RageOpenV.asi", BaseName = "RageOpenV.asi", TargetRelative = "RageOpenV.asi", Policy = FilePolicy.Replace, Required = true }
        },
        _ => new List<FileSpec>()
    };

    private static Staging StageBundledOpenIv()
    {
        var source = GetBundledOpenIvPathOrThrow();
        var root = Path.Combine(Path.GetTempPath(), "GTACoopRemake", "components",
            Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(root);
            var stagedPath = Path.Combine(root, "OpenIV.asi");
            File.Copy(source, stagedPath, overwrite: true);
            return new Staging
            {
                RootDirectory = root,
                Files = new List<StagedFile>
                {
                    new()
                    {
                        StagedPath = stagedPath,
                        TargetRelative = "OpenIV.asi",
                        Policy = FilePolicy.Replace
                    }
                }
            };
        }
        catch
        {
            TryDeleteDirectory(root);
            throw;
        }
    }

    private static async Task<Staging> DownloadAndStageAsync(
        ComponentKind kind,
        ComponentRelease release,
        IProgress<double> progress,
        Action<string> log,
        CancellationToken cancellationToken)
    {
        if (!IsAllowedDownloadUrl(kind, release.DownloadUrl))
            throw new SyncException("İndirme adresi güvenli değil.");

        var root = Path.Combine(Path.GetTempPath(), "GTACoopRemake", "components",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var zipPath = Path.Combine(root, "download.zip");
        var stageDir = Path.Combine(root, "stage");
        Directory.CreateDirectory(stageDir);

        try
        {
            await DownloadFileAsync(kind, release.DownloadUrl, zipPath, progress, cancellationToken);
            log($"Bileşen indirildi: {release.AssetName} ({new FileInfo(zipPath).Length / 1024} KB)");

            if (!LooksLikeZip(zipPath))
                throw new SyncException("İndirilen dosya geçerli bir ZIP arşivi değil.");

            var files = new List<StagedFile>();
            using var archive = ZipFile.OpenRead(zipPath);
            foreach (var spec in GetFileSpecs(kind))
            {
                if (IsForbiddenName(spec.BaseName)) continue;

                var entry = FindEntry(archive, spec);
                if (entry == null)
                {
                    if (spec.Required)
                        throw new SyncException($"Arşivde beklenen dosya bulunamadı: {spec.BaseName}");
                    continue;
                }
                if (IsForbiddenName(entry.Name)) continue;
                if (entry.Length > MaxEntryBytes)
                    throw new SyncException($"Arşivdeki dosya çok büyük: {entry.Name}");

                var stagedPath = Path.Combine(stageDir, spec.TargetRelative);
                await using (var source = entry.Open())
                await using (var target = new FileStream(
                    stagedPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                    81920, useAsync: true))
                {
                    await source.CopyToAsync(target, cancellationToken);
                }

                files.Add(new StagedFile
                {
                    StagedPath = stagedPath,
                    TargetRelative = spec.TargetRelative,
                    Policy = spec.Policy
                });
            }

            if (kind == ComponentKind.ScriptHookVDotNet &&
                !files.Any(f => f.TargetRelative is "ScriptHookVDotNet2.dll" or "ScriptHookVDotNet3.dll"))
            {
                throw new SyncException("ScriptHookVDotNet çekirdek DLL'i arşivde bulunamadı.");
            }

            return new Staging { RootDirectory = root, Files = files };
        }
        catch
        {
            TryDeleteDirectory(root);
            throw;
        }
    }

    private static async Task DownloadFileAsync(
        ComponentKind kind,
        string url,
        string destination,
        IProgress<double> progress,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Accept.ParseAdd("application/zip,application/octet-stream,*/*");
        if (kind == ComponentKind.ScriptHookV)
            request.Headers.Referrer = new Uri(ShvPage);

        using var response = await BrowserHttp.SendAsync(
            request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new SyncException($"Bileşen indirilemedi (HTTP {(int)response.StatusCode}).");

        var total = response.Content.Headers.ContentLength ?? 0;
        if (total > MaxDownloadBytes)
            throw new SyncException("Bileşen indirme boyutu güvenlik sınırını aşıyor.");

        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var target = new FileStream(
            destination, FileMode.CreateNew, FileAccess.Write, FileShare.None,
            81920, useAsync: true);

        var buffer = new byte[81920];
        long readTotal = 0;
        int read;
        while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
        {
            readTotal += read;
            if (readTotal > MaxDownloadBytes)
                throw new SyncException("Bileşen indirme boyutu güvenlik sınırını aşıyor.");
            await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            if (total > 0) progress.Report(Math.Min(0.99, (double)readTotal / total));
        }

        if (readTotal == 0)
            throw new SyncException("İndirilen dosya boş.");
        progress.Report(1);
    }

    private static ZipArchiveEntry? FindEntry(ZipArchive archive, FileSpec spec)
    {
        var entries = archive.Entries
            .Where(e => !string.IsNullOrEmpty(e.Name))
            .Where(e => !IsForbiddenName(e.Name))
            .ToList();

        var exactPath = Normalize(spec.SourcePath);
        var exact = entries.FirstOrDefault(e =>
            Normalize(e.FullName).Equals(exactPath, StringComparison.OrdinalIgnoreCase));
        if (exact != null) return exact;

        if (spec.TargetRelative.Equals("RageOpenV.asi", StringComparison.OrdinalIgnoreCase))
        {
            return entries
                .Where(e => e.Name.Equals(spec.BaseName, StringComparison.OrdinalIgnoreCase))
                .Where(e => !Normalize(e.FullName).Contains("MoreLogs/", StringComparison.OrdinalIgnoreCase))
                .OrderBy(e => Depth(e.FullName))
                .ThenBy(e => e.FullName.Length)
                .FirstOrDefault();
        }

        return entries
            .Where(e => e.Name.Equals(spec.BaseName, StringComparison.OrdinalIgnoreCase))
            .OrderBy(e => Depth(e.FullName))
            .ThenBy(e => e.FullName.Length)
            .FirstOrDefault();
    }

    private static int Depth(string path) => path.Count(c => c is '/' or '\\');

    private static string Normalize(string path) =>
        path.Replace('\\', '/').TrimStart('/').TrimStart('.', '/');

    private static bool IsForbiddenName(string name) =>
        name.Contains("NativeTrainer", StringComparison.OrdinalIgnoreCase);

    private static bool LooksLikeZip(string path)
    {
        using var stream = File.OpenRead(path);
        var first = stream.ReadByte();
        var second = stream.ReadByte();
        return first == 'P' && second == 'K';
    }

    // ------------------------------------------------------------------ yedek / geri yükleme

    public static string BackupRoot(ComponentKind kind, string gameFolder) =>
        Path.Combine(gameFolder, "mods", "GTACoop", "backups", "components", kind.ToString());

    private static string CreateBackupDirectory(ComponentKind kind, string gameFolder)
    {
        var root = BackupRoot(kind, gameFolder);
        var dir = Path.Combine(root,
            DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + "-" + Guid.NewGuid().ToString("N")[..6]);
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static string BackupFile(
        string target,
        string relativePath,
        string backupDir,
        ComponentBackupInfo info)
    {
        var dest = Path.Combine(backupDir, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        File.Copy(target, dest, overwrite: true);

        if (!info.Files.Any(f =>
                f.RelativePath.Equals(relativePath, StringComparison.OrdinalIgnoreCase)))
        {
            info.Files.Add(new ComponentBackupFile
            {
                RelativePath = relativePath,
                Sha256 = HashFile(dest),
                Size = new FileInfo(dest).Length
            });
        }
        return dest;
    }

    /// <summary>Seçili bileşen için en yeni yedeği döndürür.</summary>
    public static ComponentBackup? GetLatestBackup(ComponentKind kind, string gameFolder)
    {
        var root = BackupRoot(kind, gameFolder);
        if (!Directory.Exists(root)) return null;

        foreach (var dir in Directory.EnumerateDirectories(root)
                     .OrderByDescending(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
        {
            var info = SyncJson.Load<ComponentBackupInfo>(Path.Combine(dir, "backup.json"));
            if (info == null) continue;
            info.Files ??= new();
            if (info.Files.Count == 0) continue;
            return new ComponentBackup { Directory = dir, Info = info };
        }
        return null;
    }

    /// <summary>En yeni yedeği geri yükler; mevcut dosyaları da ayrıca saklar.</summary>
    public static ComponentRestoreResult RestoreLatestBackup(
        ComponentKind kind,
        string gameFolder,
        string dataFolder,
        Action<string>? log = null)
    {
        log ??= _ => { };
        if (!GameFolder.IsValid(gameFolder))
            throw new SyncException("GTA V klasörü geçerli değil.");
        if (AddonApplier.IsGameRunning())
            throw new SyncException("GTA V açıkken geri yükleme yapılamaz.");

        var backup = GetLatestBackup(kind, gameFolder)
                     ?? throw new SyncException("Bu bileşen için yedek bulunamadı.");
        var state = LoadState(dataFolder);
        var current = state.Items.FirstOrDefault(i => i.Kind == kind);
        var backupPaths = new HashSet<string>(
            backup.Info.Files.Select(f => f.RelativePath), StringComparer.OrdinalIgnoreCase);

        // Önce tüm yedek dosyalarının yolu ve varlığı doğrulanır; yarıda kalmasın.
        foreach (var file in backup.Info.Files)
        {
            var source = SafeTarget(backup.Directory, file.RelativePath);
            if (!File.Exists(source))
                throw new SyncException($"Yedekte eksik dosya: {file.RelativePath}");
        }

        var safetyDir = Path.Combine(backup.Directory,
            "before-restore-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"));
        Directory.CreateDirectory(safetyDir);

        foreach (var file in backup.Info.Files)
        {
            var source = SafeTarget(backup.Directory, file.RelativePath);
            var target = SafeTarget(gameFolder, file.RelativePath);
            if (File.Exists(target))
            {
                var safety = SafeTarget(safetyDir, file.RelativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(safety)!);
                File.Copy(target, safety, overwrite: true);
            }
            CopyReplacing(source, target);
            log($"Geri yüklendi: {file.RelativePath}");
        }

        // Güncellemeyle eklenen ama yedekte bulunmayan bileşen dosyalarını temizle.
        // Kullanıcının değiştirdiği dosyalar ve .ini ayarları korunur.
        if (current?.Files != null)
        {
            foreach (var file in current.Files)
            {
                if (backupPaths.Contains(file.RelativePath)) continue;
                if (file.RelativePath.EndsWith(".ini", StringComparison.OrdinalIgnoreCase))
                {
                    log($"Korundu (ayar dosyası): {file.RelativePath}");
                    continue;
                }

                try
                {
                    var target = SafeTarget(gameFolder, file.RelativePath);
                    if (!File.Exists(target)) continue;
                    if (!HashFile(target).Equals(file.Sha256, StringComparison.OrdinalIgnoreCase))
                    {
                        log($"Korundu (kullanıcı değiştirmiş): {file.RelativePath}");
                        continue;
                    }

                    var safety = SafeTarget(safetyDir, file.RelativePath);
                    Directory.CreateDirectory(Path.GetDirectoryName(safety)!);
                    File.Copy(target, safety, overwrite: true);
                    File.Delete(target);
                    log($"Kaldırıldı (yedekte yok): {file.RelativePath}");
                }
                catch (Exception ex)
                {
                    log($"Kaldırılamadı ({file.RelativePath}): {ex.Message}");
                }
            }
        }

        state.Items.RemoveAll(i => i.Kind == kind);
        if (!string.IsNullOrWhiteSpace(backup.Info.Tag) && backup.Info.Files.Count > 0)
        {
            state.Items.Add(new InstalledComponent
            {
                Kind = kind,
                Channel = backup.Info.Channel,
                Tag = backup.Info.Tag,
                Version = backup.Info.Version,
                Source = backup.Info.Source,
                InstalledUtc = DateTime.UtcNow,
                BackupDir = backup.Directory,
                Files = backup.Info.Files.Select(f => new InstalledComponentFile
                {
                    RelativePath = f.RelativePath,
                    Sha256 = f.Sha256,
                    Size = f.Size
                }).ToList()
            });
        }
        SyncJson.Save(StatePath(dataFolder), state);

        return new ComponentRestoreResult
        {
            Restored = backup.Info.Files.Count,
            BackupDir = backup.Directory,
            Version = backup.Info.Version
        };
    }

    // ------------------------------------------------------------------ durum / yardımcılar

    public static InstalledComponent? GetInstalled(ComponentKind kind, string dataFolder)
    {
        var state = LoadState(dataFolder);
        return state.Items.FirstOrDefault(i => i.Kind == kind);
    }

    public static bool IsIntact(InstalledComponent installed, string gameFolder)
    {
        if (installed.Files == null || installed.Files.Count == 0) return false;
        try
        {
            foreach (var file in installed.Files)
            {
                var path = SafeTarget(gameFolder, file.RelativePath);
                if (!File.Exists(path)) return false;
                var info = new FileInfo(path);
                if (info.Length != file.Size) return false;
                if (!HashFile(path).Equals(file.Sha256, StringComparison.OrdinalIgnoreCase))
                    return false;
            }
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static InstalledComponentsState LoadState(string dataFolder)
    {
        if (string.IsNullOrWhiteSpace(dataFolder)) return new InstalledComponentsState();
        var state = SyncJson.Load<InstalledComponentsState>(StatePath(dataFolder))
                    ?? new InstalledComponentsState();
        state.Items ??= new();
        foreach (var item in state.Items) item.Files ??= new();
        return state;
    }

    private static string StatePath(string dataFolder) =>
        Path.Combine(dataFolder, StateFileName);

    private static string HashFile(string path)
    {
        using var sha = SHA256.Create();
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(sha.ComputeHash(stream));
    }

    /// <summary>Yedek/state içindeki göreli yolun oyun klasörü dışına çıkmadığını doğrular.</summary>
    private static string SafeTarget(string root, string relativePath)
    {
        var rootFull = Path.GetFullPath(root)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
            Path.DirectorySeparatorChar;
        var target = Path.GetFullPath(Path.Combine(root, relativePath));
        if (!target.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
            throw new SyncException($"Geçersiz dosya yolu: {relativePath}");
        return target;
    }

    private static void CopyReplacing(string source, string target)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        var temp = target + ".gtacoop-new-" + Guid.NewGuid().ToString("N")[..8];
        try
        {
            File.Copy(source, temp, overwrite: true);
            File.Move(temp, target, overwrite: true);
        }
        finally
        {
            try { if (File.Exists(temp)) File.Delete(temp); } catch { }
        }
    }

    private static void Rollback(List<AppliedChange> changes, Action<string> log)
    {
        foreach (var change in changes.AsEnumerable().Reverse())
        {
            try
            {
                if (change.BackupPath != null && File.Exists(change.BackupPath))
                {
                    CopyReplacing(change.BackupPath, change.Target);
                    log($"Geri alındı: {change.Target}");
                }
                else if (File.Exists(change.Target))
                {
                    File.Delete(change.Target);
                    log($"Geri alındı (yeni dosya silindi): {change.Target}");
                }
            }
            catch (Exception ex)
            {
                log($"Geri alma hatası ({change.Target}): {ex.Message}");
            }
        }
    }

    private static void TryDeleteDirectory(string dir)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
                return;
            }
            catch
            {
                Thread.Sleep(100);
            }
        }
    }

    // ------------------------------------------------------------------ test / teşhis

    /// <summary>Yalnızca teşhis için: sürüm listesini indirmeden önbelleği okur.</summary>
    public static List<ComponentRelease>? LoadCachedReleases(
        ComponentKind kind,
        ShvdnChannel channel,
        string dataFolder)
    {
        var cache = SyncJson.Load<ComponentCache>(CachePath(dataFolder, kind, channel));
        return cache?.Releases;
    }
}
