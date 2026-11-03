using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using System.Windows.Forms;

namespace GTACoop.Remake;

/// <summary>
/// GitHub Releases üzerinden sürüm kontrolü ve kendi kendini güncelleme.
///
/// Yalnızca launcher exe'si değiştirilir: oyun klasörü, ayarlar, önbellek ve
/// sunucu klasörleri ellenmez. Çalışan bir exe Windows'ta silinemez ama
/// yeniden adlandırılabilir; bu yüzden güncelleme "eski exe'yi .old yap, yeni
/// exe'yi yerine koy, yeniden başlat" sırasıyla yapılır. Kopyalama başarısız
/// olursa eski exe geri taşınır ve uygulama çalışmaya devam eder.
/// </summary>
public static class SelfUpdater
{
    private const string LatestApi =
        "https://api.github.com/repos/Baloraa/Gta-Coop-Remake-Gta-5-/releases/latest";

    private const string ExeAsset = "GTACoop.Launcher.exe";

    /// <summary>Bilinen en son sürüm (varsa); Hakkında sekmesi bunu gösterir.</summary>
    public static ReleaseInfo? Latest { get; private set; }

    public sealed class ReleaseInfo
    {
        public string Tag { get; init; } = "";
        public string Name { get; init; } = "";
        public string Body { get; init; } = "";
        public string ExeUrl { get; init; } = "";
        public string HtmlUrl { get; init; } = "";
        public Version? Version { get; init; }
    }

    public static Version CurrentVersion =>
        typeof(SelfUpdater).Assembly.GetName().Version ?? new Version(0, 0, 0);

    public static string CurrentVersionText => CurrentVersion.ToString(3);

    private static HttpClient CreateHttp()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("GTACoopRemakeLauncher/" + CurrentVersionText);
        http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return http;
    }

    /// <summary>En son sürümü sorar. Ağ yoksa null döner, hata fırlatmaz.</summary>
    public static async Task<ReleaseInfo?> CheckAsync(CancellationToken ct = default)
    {
        try
        {
            using var http = CreateHttp();
            using var resp = await http.GetAsync(LatestApi, ct).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
            {
                AppEnv.Log($"Sürüm kontrolü: HTTP {(int)resp.StatusCode}.");
                return null;
            }

            await using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
            var root = doc.RootElement;

            var tag = root.TryGetProperty("tag_name", out var t) ? (t.GetString() ?? "") : "";
            if (!Version.TryParse(tag.TrimStart('v', 'V'), out var version))
            {
                AppEnv.Log($"Sürüm kontrolü: etiket okunamadı ('{tag}').");
                return null;
            }

            var exeUrl = "";
            if (root.TryGetProperty("assets", out var assets) &&
                assets.ValueKind == JsonValueKind.Array)
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    var name = asset.TryGetProperty("name", out var n) ? (n.GetString() ?? "") : "";
                    if (!string.Equals(name, ExeAsset, StringComparison.OrdinalIgnoreCase)) continue;
                    if (asset.TryGetProperty("browser_download_url", out var url))
                        exeUrl = url.GetString() ?? "";
                    break;
                }
            }

            var info = new ReleaseInfo
            {
                Tag = tag,
                Name = root.TryGetProperty("name", out var nm) ? (nm.GetString() ?? tag) : tag,
                Body = root.TryGetProperty("body", out var b) ? (b.GetString() ?? "") : "",
                HtmlUrl = root.TryGetProperty("html_url", out var h) ? (h.GetString() ?? "") : "",
                ExeUrl = exeUrl,
                Version = version
            };

            Latest = info;
            AppEnv.Log($"Sürüm kontrolü: en son {info.Tag}, kurulu {CurrentVersionText}.");
            return info;
        }
        catch (Exception ex)
        {
            AppEnv.Log("Sürüm kontrolü yapılamadı: " + ex.Message);
            return null;
        }
    }

    /// <summary>Verilen sürüm kurulu sürümden yeni mi?</summary>
    public static bool IsNewer(ReleaseInfo? info)
    {
        if (info?.Version == null) return false;
        return info.Version > CurrentVersion;
    }

    /// <summary>Güncelleme exe'sini geçici klasöre indirir ve yolunu döner.</summary>
    public static async Task<string> DownloadAsync(string url, IProgress<int>? progress = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new InvalidOperationException("Sürümde indirilebilir launcher exe'si yok.");

        var dir = Path.Combine(Path.GetTempPath(), "GTACoopRemakeUpdate");
        Directory.CreateDirectory(dir);
        var target = Path.Combine(dir, ExeAsset);
        if (File.Exists(target)) File.Delete(target);

        using var http = CreateHttp();
        http.Timeout = TimeSpan.FromMinutes(10);
        using var resp = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct)
            .ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();

        var total = resp.Content.Headers.ContentLength ?? -1;
        long done = 0;
        await using (var src = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false))
        await using (var dst = new FileStream(target, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            var buffer = new byte[81920];
            int read;
            while ((read = await src.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
            {
                await dst.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
                done += read;
                if (total > 0) progress?.Report((int)(done * 100 / total));
            }
        }

        // HTML hata sayfası ya da yarım indirme exe diye kurulmasın.
        var size = new FileInfo(target).Length;
        if (size < 200_000)
            throw new InvalidDataException(
                $"İndirilen dosya beklenenden küçük ({size} bayt); güncelleme iptal edildi.");

        AppEnv.Log($"Güncelleme indirildi: {size} bayt.");
        return target;
    }

    /// <summary>
    /// İndirilen exe'yi kurar ve uygulamayı yeniden başlatır. Çağıran taraf
    /// döndükten sonra uygulamayı kapatmalıdır.
    /// </summary>
    public static void ApplyAndRestart(string downloadedExe)
    {
        if (!File.Exists(downloadedExe))
            throw new FileNotFoundException("İndirilen güncelleme bulunamadı.", downloadedExe);

        var target = Application.ExecutablePath;
        var old = target + ".old";

        try { if (File.Exists(old)) File.Delete(old); }
        catch { /* eski .old kilitliyse yeni ad kullanılır */ }

        var movedOld = false;
        try
        {
            File.Move(target, old, overwrite: true);
            movedOld = true;
            File.Copy(downloadedExe, target, overwrite: true);
            AppEnv.Log("Güncelleme kuruldu; uygulama yeniden başlatılıyor.");
        }
        catch
        {
            // Yeni exe yerleştirilemediyse eski exe'yi geri koy.
            if (movedOld && !File.Exists(target))
            {
                try { File.Move(old, target, overwrite: true); } catch { }
            }
            throw;
        }

        if (!string.IsNullOrWhiteSpace(Latest?.HtmlUrl))
            AppEnv.Log("Sürüm notları: " + Latest!.HtmlUrl);

        try
        {
            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            AppEnv.LogError("Yeniden başlatma başarısız: " + ex.Message);
        }
    }

    /// <summary>Önceki güncellemeden kalan .old dosyasını siler (başlangıçta).</summary>
    public static void CleanupOld()
    {
        try
        {
            var old = Application.ExecutablePath + ".old";
            if (File.Exists(old)) File.Delete(old);
        }
        catch
        {
            // Kilitliyse sonraki açılışta denenir.
        }
    }
}
