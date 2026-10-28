using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;

namespace GTACoop.Sync;

/// <summary>İlerleme bildirimi.</summary>
public sealed record SyncProgress(string Message, double Fraction, long DoneBytes = 0, long TotalBytes = 0);

/// <summary>Sunucudan manifest ve dosya indiren HTTP istemcisi.</summary>
public sealed class SyncClient : IDisposable
{
    /// <summary>Bu süre boyunca tek bayt bile gelmezse indirme takılı sayılır ve yeniden denenir.</summary>
    private static readonly TimeSpan StallTimeout = TimeSpan.FromSeconds(45);

    private const int MaxDownloadAttempts = 8;

    /// <summary>Bu boyutun üzerindeki dosyalar birden fazla TCP bağlantısıyla indirilir.</summary>
    private const long ParallelThreshold = 4L * 1024 * 1024;
    private const long LargeThreshold = 12L * 1024 * 1024;
    private const int MaxSegments = 4;

    private readonly HttpClient _http;
    private readonly Action<string>? _log;
    private bool? _rangeSupported;

    public SyncClient(string host, int syncPort, string token = "", Action<string>? log = null)
    {
        _log = log;
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
    /// Geçici dosya: hedef + ".part". Bağlantı koparsa veya 45 sn boyunca
    /// veri gelmezse kaldığı yerden (HTTP Range) devam eder.
    /// Büyük dosyalar sunucu Range destekliyorsa birden fazla paralel
    /// bağlantıyla indirilir; her parça ayrı ayrı devam ettirilebilir.
    /// </summary>
    public async Task DownloadFileAsync(
        string addonId, AddonFileInfo file, string destPath,
        IProgress<long>? bytesProgress = null, CancellationToken ct = default)
    {
        var url = "api/v1/files/" + Uri.EscapeDataString(addonId) + "/" + EscapePath(file.Path);
        var tmp = destPath + ".part";
        Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);

        var have = ResumeLength(tmp, file.Size);
        if (have == file.Size && file.Size > 0)
        {
            if (await HashMatchesAsync(tmp, file.Sha256, ct))
            {
                File.Move(tmp, destPath, overwrite: true);
                return;
            }
            TryDelete(tmp);
            have = 0;
        }

        var segments = 1;
        if (file.Size >= ParallelThreshold && await RangeSupportedAsync(url, ct))
            segments = file.Size >= LargeThreshold ? MaxSegments : 2;

        if (segments > 1)
        {
            for (var wholeAttempt = 1; wholeAttempt <= 2; wholeAttempt++)
            {
                try
                {
                    await DownloadSegmentsAsync(url, file.Path, file.Size, file.Sha256,
                        destPath, segments, bytesProgress, ct);
                    return;
                }
                catch (RangeNotSupportedException)
                {
                    _rangeSupported = false;
                    _log?.Invoke("Sunucu HTTP Range desteklemiyor; tek bağlantıyla indirilecek: " + file.Path);
                    CleanupParts(destPath);
                    break;
                }
                catch (HashMismatchException ex)
                {
                    _log?.Invoke($"Parçalı indirme doğrulanamadı ({ex.Message}); " +
                                 (wholeAttempt < 2 ? "baştan denenecek" : "vazgeçildi") +
                                 ": " + file.Path);
                    CleanupParts(destPath);
                    TryDelete(tmp);
                    if (wholeAttempt >= 2)
                        throw new SyncException(
                            $"Dosya doğrulanamadı (SHA-256 uyuşmuyor): {file.Path}. " +
                            "İndirme bozuk geldi; sunucudaki dosyayı ve bağlantınızı kontrol edip yeniden deneyin.");
                }
            }
        }

        await DownloadSingleAsync(url, file, destPath, tmp, have, bytesProgress, ct);
    }

    // ------------------------------------------------------------------ tek bağlantı

    private async Task DownloadSingleAsync(
        string url, AddonFileInfo file, string destPath, string tmp,
        long have, IProgress<long>? bytesProgress, CancellationToken ct)
    {
        var hashFailures = 0;

        for (var attempt = 1; attempt <= MaxDownloadAttempts; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            var startOffset = have;

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                if (startOffset > 0)
                    request.Headers.Range = new RangeHeaderValue(startOffset, null);

                using var resp = await _http.SendAsync(
                    request, HttpCompletionOption.ResponseHeadersRead, ct);

                if (resp.StatusCode == HttpStatusCode.NotFound)
                    throw new SyncException($"Sunucuda dosya bulunamadı: {file.Path}");
                if (resp.StatusCode == HttpStatusCode.Unauthorized)
                    throw new SyncException("Sunucu kimlik doğrulaması istiyor (token hatalı olabilir).");
                if (resp.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable)
                {
                    // .part dosyası bozulmuş/eskimiş; baştan indir.
                    _log?.Invoke($"Kısmi indirme geçersiz; baştan indirilecek: {file.Path}");
                    TryDelete(tmp);
                    have = 0;
                    continue;
                }
                if (!resp.IsSuccessStatusCode)
                    throw new SyncException(
                        $"İndirme hatası ({file.Path}): {(int)resp.StatusCode} {resp.ReasonPhrase}");

                if (startOffset > 0 && resp.StatusCode != HttpStatusCode.PartialContent)
                {
                    // Sunucu Range isteğini yok saydı: dosyayı baştan yaz.
                    TryDelete(tmp);
                    have = 0;
                }

                using var stallCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                stallCts.CancelAfter(StallTimeout);

                await using (var src = await resp.Content.ReadAsStreamAsync(stallCts.Token))
                await using (var dst = new FileStream(
                    tmp, have > 0 ? FileMode.Append : FileMode.Create,
                    FileAccess.Write, FileShare.None, 1 << 20,
                    FileOptions.Asynchronous | FileOptions.SequentialScan))
                {
                    var buffer = new byte[1 << 20];
                    int read;
                    while ((read = await src.ReadAsync(buffer, stallCts.Token)) > 0)
                    {
                        await dst.WriteAsync(buffer.AsMemory(0, read), stallCts.Token);
                        have += read;
                        bytesProgress?.Report(have);
                        // Her veri geldiğinde stall sayacını sıfırla.
                        stallCts.CancelAfter(StallTimeout);
                    }
                }

                if (have < file.Size)
                {
                    _log?.Invoke(
                        $"İndirme erken kapandı ({have:N0}/{file.Size:N0} bayt); " +
                        $"kaldığı yerden sürüyor: {file.Path}");
                    await DelayBeforeRetryAsync(attempt, ct);
                    continue;
                }

                if (!await HashMatchesAsync(tmp, file.Sha256, ct))
                {
                    hashFailures++;
                    _log?.Invoke($"İndirilen dosya doğrulanamadı; baştan indirilecek: {file.Path}");
                    TryDelete(tmp);
                    have = 0;
                    if (hashFailures >= 2)
                        throw new SyncException(
                            $"Dosya doğrulanamadı (SHA-256 uyuşmuyor): {file.Path}. " +
                            "İndirme bozuk geldi; sunucudaki dosyayı ve bağlantınızı kontrol edip yeniden deneyin.");
                    await DelayBeforeRetryAsync(attempt, ct);
                    continue;
                }

                File.Move(tmp, destPath, overwrite: true);
                return;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw; // kullanıcı iptal etti; .part dosyası kalır
            }
            catch (OperationCanceledException ex)
            {
                // HttpClient zaman aşımı veya stall sayacı: bağlantı sessizce ölmüş.
                _log?.Invoke(
                    $"İndirme {StallTimeout.TotalSeconds:0} sn ilerlemedi ({file.Path}); " +
                    $"kaldığı yerden yeniden denenecek ({attempt}/{MaxDownloadAttempts}): {ex.Message}");
                have = ResumeLength(tmp, file.Size);
                await DelayBeforeRetryAsync(attempt, ct);
            }
            catch (Exception ex) when (ex is HttpRequestException or IOException)
            {
                _log?.Invoke(
                    $"İndirme kesildi ({file.Path}); yeniden denenecek " +
                    $"({attempt}/{MaxDownloadAttempts}): {ex.Message}");
                have = ResumeLength(tmp, file.Size);
                await DelayBeforeRetryAsync(attempt, ct);
            }
        }

        throw new SyncException(
            $"Dosya indirilemedi: {file.Path} ({have:N0}/{file.Size:N0} bayt). " +
            "Bağlantı koptu; tekrar bağlandığınızda kaldığı yerden devam eder.");
    }

    // ------------------------------------------------------------------ paralel parçalar

    private sealed record Segment(int Index, long Start, long End, string Path, long Have)
    {
        public long Length => End - Start + 1;
    }

    private async Task DownloadSegmentsAsync(
        string url, string filePath, long fileSize, string expectedSha,
        string destPath, int count, IProgress<long>? bytesProgress, CancellationToken ct)
    {
        var segmentSize = (fileSize + count - 1) / count;
        var segments = new List<Segment>(count);
        var doneBytes = new long[count];

        for (var i = 0; i < count; i++)
        {
            var start = i * segmentSize;
            if (start >= fileSize) break;
            var end = Math.Min(fileSize, start + segmentSize) - 1;
            var partPath = destPath + $".part.{i}";
            var length = end - start + 1;
            var have = ResumeLength(partPath, length);
            doneBytes[i] = have;
            segments.Add(new Segment(i, start, end, partPath, have));
        }

        bytesProgress?.Report(doneBytes.Sum());
        var remaining = segments.Where(s => s.Have < s.Length).ToList();
        _log?.Invoke(
            $"Parçalı indirme: {filePath} — {segments.Count} parça, " +
            $"{remaining.Count} aktif, {FormatBytes(fileSize)}");

        var tasks = remaining
            .Select(s => DownloadSegmentAsync(url, filePath, s, doneBytes, bytesProgress, ct))
            .ToList();
        await Task.WhenAll(tasks);
        bytesProgress?.Report(fileSize);

        // Parçaları tek dosyada birleştir, sonra SHA-256 doğrula.
        var tmp = destPath + ".part";
        await using (var output = new FileStream(
            tmp, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 20,
            FileOptions.Asynchronous | FileOptions.SequentialScan))
        {
            foreach (var segment in segments)
            {
                await using var input = new FileStream(
                    segment.Path, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 20,
                    FileOptions.Asynchronous | FileOptions.SequentialScan);
                await input.CopyToAsync(output, 1 << 20, ct);
            }
        }

        CleanupParts(destPath);

        if (!await HashMatchesAsync(tmp, expectedSha, ct))
        {
            TryDelete(tmp);
            throw new HashMismatchException(filePath);
        }

        File.Move(tmp, destPath, overwrite: true);
    }

    private async Task DownloadSegmentAsync(
        string url, string filePath, Segment segment, long[] doneBytes,
        IProgress<long>? bytesProgress, CancellationToken ct)
    {
        var have = segment.Have;

        for (var attempt = 1; attempt <= MaxDownloadAttempts; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            var from = segment.Start + have;

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Range = new RangeHeaderValue(from, segment.End);

                using var resp = await _http.SendAsync(
                    request, HttpCompletionOption.ResponseHeadersRead, ct);

                if (resp.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable)
                {
                    if (have >= segment.Length) return;
                    _log?.Invoke($"Parça aralığı geçersiz; parça baştan indirilecek: {filePath} [{segment.Index}]");
                    TryDelete(segment.Path);
                    have = 0;
                    Volatile.Write(ref doneBytes[segment.Index], 0);
                    continue;
                }
                if (resp.StatusCode == HttpStatusCode.OK)
                    throw new RangeNotSupportedException();
                if (resp.StatusCode == HttpStatusCode.NotFound)
                    throw new SyncException($"Sunucuda dosya bulunamadı: {filePath}");
                if (resp.StatusCode == HttpStatusCode.Unauthorized)
                    throw new SyncException("Sunucu kimlik doğrulaması istiyor (token hatalı olabilir).");
                if (resp.StatusCode != HttpStatusCode.PartialContent)
                    throw new SyncException(
                        $"İndirme hatası ({filePath}): {(int)resp.StatusCode} {resp.ReasonPhrase}");

                var contentRange = resp.Content.Headers.ContentRange;
                if (contentRange?.From is long actualFrom && actualFrom != from)
                {
                    _log?.Invoke(
                        $"Parça yanlış konumdan geldi ({actualFrom} != {from}); " +
                        $"baştan indirilecek: {filePath} [{segment.Index}]");
                    TryDelete(segment.Path);
                    have = 0;
                    Volatile.Write(ref doneBytes[segment.Index], 0);
                    continue;
                }

                using var stallCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                stallCts.CancelAfter(StallTimeout);

                await using (var src = await resp.Content.ReadAsStreamAsync(stallCts.Token))
                await using (var dst = new FileStream(
                    segment.Path, have > 0 ? FileMode.Append : FileMode.Create,
                    FileAccess.Write, FileShare.None, 1 << 20,
                    FileOptions.Asynchronous | FileOptions.SequentialScan))
                {
                    var buffer = new byte[1 << 20];
                    int read;
                    while ((read = await src.ReadAsync(buffer, stallCts.Token)) > 0)
                    {
                        var left = segment.Length - have;
                        if (left <= 0) break;
                        if (read > left) read = (int)left;

                        await dst.WriteAsync(buffer.AsMemory(0, read), stallCts.Token);
                        have += read;
                        Volatile.Write(ref doneBytes[segment.Index], have);
                        bytesProgress?.Report(doneBytes.Sum());
                        stallCts.CancelAfter(StallTimeout);
                    }
                }

                if (have >= segment.Length) return;

                _log?.Invoke(
                    $"Parça erken kapandı ({have:N0}/{segment.Length:N0} bayt); " +
                    $"kaldığı yerden sürüyor: {filePath} [{segment.Index}]");
                await DelayBeforeRetryAsync(attempt, ct);
            }
            catch (RangeNotSupportedException)
            {
                throw;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw; // kullanıcı iptal etti; parça dosyaları kalır
            }
            catch (OperationCanceledException ex)
            {
                _log?.Invoke(
                    $"Parça {StallTimeout.TotalSeconds:0} sn ilerlemedi ({filePath} [{segment.Index}]); " +
                    $"kaldığı yerden yeniden denenecek ({attempt}/{MaxDownloadAttempts}): {ex.Message}");
                have = ResumeLength(segment.Path, segment.Length);
                Volatile.Write(ref doneBytes[segment.Index], have);
                await DelayBeforeRetryAsync(attempt, ct);
            }
            catch (Exception ex) when (ex is HttpRequestException or IOException)
            {
                _log?.Invoke(
                    $"Parça indirmesi kesildi ({filePath} [{segment.Index}]); yeniden denenecek " +
                    $"({attempt}/{MaxDownloadAttempts}): {ex.Message}");
                have = ResumeLength(segment.Path, segment.Length);
                Volatile.Write(ref doneBytes[segment.Index], have);
                await DelayBeforeRetryAsync(attempt, ct);
            }
        }

        throw new SyncException(
            $"Dosya parçası indirilemedi: {filePath} [{segment.Index}] " +
            $"({have:N0}/{segment.Length:N0} bayt). " +
            "Bağlantı koptu; tekrar bağlandığınızda kaldığı yerden devam eder.");
    }

    // ------------------------------------------------------------------ yardımcılar

    /// <summary>Sunucu tek aralık isteğine 206 döndürüyor mu? Sonuç istemci başına önbelleklenir.</summary>
    private async Task<bool> RangeSupportedAsync(string url, CancellationToken ct)
    {
        if (_rangeSupported.HasValue) return _rangeSupported.Value;

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Range = new RangeHeaderValue(0, 0);
            using var resp = await _http.SendAsync(
                request, HttpCompletionOption.ResponseHeadersRead, ct);
            _rangeSupported = resp.StatusCode == HttpStatusCode.PartialContent &&
                              resp.Content.Headers.ContentRange?.From == 0;
            if (!_rangeSupported.Value)
                _log?.Invoke("Sunucu HTTP Range desteklemiyor; tek bağlantı kullanılacak.");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _rangeSupported = false;
            _log?.Invoke("Range desteği sınanamadı; tek bağlantı kullanılacak: " + ex.Message);
        }

        return _rangeSupported.Value;
    }

    private static long ResumeLength(string path, long expectedSize)
    {
        try
        {
            if (!File.Exists(path)) return 0;
            var length = new FileInfo(path).Length;
            if (length <= 0 || (expectedSize > 0 && length > expectedSize))
            {
                File.Delete(path);
                return 0;
            }
            return length;
        }
        catch
        {
            return 0;
        }
    }

    private static async Task<bool> HashMatchesAsync(
        string path, string expected, CancellationToken ct)
    {
        using var sha = SHA256.Create();
        await using var fs = File.OpenRead(path);
        var actual = Convert.ToHexString(await sha.ComputeHashAsync(fs, ct)).ToLowerInvariant();
        return string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);
    }

    private static Task DelayBeforeRetryAsync(int attempt, CancellationToken ct) =>
        Task.Delay(TimeSpan.FromSeconds(Math.Min(attempt * 2, 10)), ct);

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }

    private static void CleanupParts(string destPath)
    {
        for (var i = 0; i < MaxSegments; i++)
            TryDelete(destPath + $".part.{i}");
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes >= 1024L * 1024 * 1024) return $"{bytes / 1073741824.0:0.0} GB";
        if (bytes >= 1024L * 1024) return $"{bytes / 1048576.0:0.0} MB";
        if (bytes >= 1024) return $"{bytes / 1024.0:0} KB";
        return $"{bytes} B";
    }

    private static string EscapePath(string relPath) =>
        string.Join('/', relPath.Replace('\\', '/').Split('/')
            .Select(Uri.EscapeDataString));

    public void Dispose() => _http.Dispose();

    private sealed class RangeNotSupportedException : Exception
    {
        public RangeNotSupportedException() : base("Sunucu HTTP Range desteklemiyor.") { }
    }

    private sealed class HashMismatchException : Exception
    {
        public HashMismatchException(string path) : base("SHA-256 doğrulanamadı: " + path) { }
    }
}

/// <summary>Senkronizasyon hatalarında kullanıcı dostu mesaj taşıyan istisna.</summary>
public sealed class SyncException : Exception
{
    public SyncException(string message) : base(message) { }
    public SyncException(string message, Exception inner) : base(message, inner) { }
}
