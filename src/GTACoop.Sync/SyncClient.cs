using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;

namespace GTACoop.Sync;

/// <summary>İlerleme bildirimi.</summary>
public sealed record SyncProgress(string Message, double Fraction, long DoneBytes = 0, long TotalBytes = 0);

/// <summary>Sunucudan manifest ve dosya indiren HTTP istemcisi.</summary>
public sealed class SyncClient : IDisposable
{
    private readonly HttpClient _http;

    public SyncClient(string host, int syncPort, string token = "")
    {
        var h = host.Trim();
        if (h.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            h.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            h = h[(h.IndexOf("://", StringComparison.Ordinal) + 3)..];
        if (h.Contains(':')) h = h[..h.IndexOf(':')];

        var baseUrl = $"http://{h}:{syncPort}/";
        _http = new HttpClient
        {
            BaseAddress = new Uri(baseUrl),
            Timeout = TimeSpan.FromMinutes(60)
        };
        if (!string.IsNullOrEmpty(token))
            _http.DefaultRequestHeaders.Add("X-Sync-Token", token);
    }

    /// <summary>Sunucuya erişimi sınar; sunucu adını döndürür.</summary>
    public async Task<string> PingAsync(CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.GetAsync("api/v1/ping", ct);
            if (!resp.IsSuccessStatusCode)
                throw new SyncException($"Sunucu hata döndürdü: {(int)resp.StatusCode} {resp.ReasonPhrase}");
            var text = await resp.Content.ReadAsStringAsync(ct);
            var doc = System.Text.Json.JsonDocument.Parse(text);
            return doc.RootElement.TryGetProperty("serverName", out var n) ? n.GetString() ?? "" : "";
        }
        catch (TaskCanceledException)
        {
            throw new SyncException("Sunucu yanıt vermedi (zaman aşımı). Adres ve bağlantı noktasını kontrol edin.");
        }
        catch (HttpRequestException ex)
        {
            throw new SyncException($"Sunucuya bağlanılamadı ({_http.BaseAddress}): {ex.Message}");
        }
    }

    /// <summary>Eklenti bildirimini (manifest) indirir.</summary>
    public async Task<Manifest> GetManifestAsync(CancellationToken ct = default)
    {
        try
        {
            var manifest = await _http.GetFromJsonAsync<Manifest>("api/v1/manifest", SyncJson.Options, ct);
            if (manifest == null)
                throw new SyncException("Sunucudan geçersiz manifest alındı.");
            return manifest;
        }
        catch (HttpRequestException ex)
        {
            throw new SyncException($"Manifest alınamadı ({_http.BaseAddress}): {ex.Message}");
        }
        catch (System.Text.Json.JsonException ex)
        {
            throw new SyncException("Manifest çözümlenemedi: " + ex.Message);
        }
    }

    /// <summary>
    /// Tek dosyayı indirir, SHA-256 doğrular ve hedefe taşır.
    /// Geçici dosya: hedef + ".part"
    /// </summary>
    public async Task DownloadFileAsync(
        string addonId, AddonFileInfo file, string destPath,
        IProgress<long>? bytesProgress = null, CancellationToken ct = default)
    {
        var url = "api/v1/files/" + Uri.EscapeDataString(addonId) + "/" + EscapePath(file.Path);
        var tmp = destPath + ".part";
        Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);

        try
        {
            using var resp = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
            if (resp.StatusCode == HttpStatusCode.NotFound)
                throw new SyncException($"Sunucuda dosya bulunamadı: {addonId}/{file.Path}");
            if (resp.StatusCode == HttpStatusCode.Unauthorized)
                throw new SyncException("Sunucu kimlik doğrulaması istiyor (token hatalı olabilir).");
            if (!resp.IsSuccessStatusCode)
                throw new SyncException($"İndirme hatası ({file.Path}): {(int)resp.StatusCode} {resp.ReasonPhrase}");

            await using (var src = await resp.Content.ReadAsStreamAsync(ct))
            await using (var dst = File.Create(tmp))
            {
                var buffer = new byte[1 << 20];
                long total = 0;
                int read;
                while ((read = await src.ReadAsync(buffer, ct)) > 0)
                {
                    await dst.WriteAsync(buffer.AsMemory(0, read), ct);
                    total += read;
                    bytesProgress?.Report(total);
                }
            }

            // SHA-256 doğrulaması
            string actual;
            using (var sha = SHA256.Create())
            await using (var fs = File.OpenRead(tmp))
            {
                actual = Convert.ToHexString(await sha.ComputeHashAsync(fs, ct)).ToLowerInvariant();
            }

            if (!string.Equals(actual, file.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                File.Delete(tmp);
                throw new SyncException(
                    $"Dosya bozuk indirildi: {file.Path}\nBeklenen: {file.Sha256}\nİnen: {actual}");
            }

            File.Move(tmp, destPath, overwrite: true);
        }
        catch
        {
            try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
            throw;
        }
    }

    private static string EscapePath(string relPath) =>
        string.Join('/', relPath.Replace('\\', '/').Split('/')
            .Select(Uri.EscapeDataString));

    public void Dispose() => _http.Dispose();
}

/// <summary>Senkronizasyon hatalarında kullanıcı dostu mesaj taşıyan istisna.</summary>
public sealed class SyncException : Exception
{
    public SyncException(string message) : base(message) { }
    public SyncException(string message, Exception inner) : base(message, inner) { }
}
