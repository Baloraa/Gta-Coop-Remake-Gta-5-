using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace GTACoop.Remake;

/// <summary>Paylaşılan tek bir dosya: göreli yol, boyut ve SHA256 özeti.</summary>
internal sealed class P2PFile
{
    public string Path { get; set; } = "";
    public long Size { get; set; }
    public string Sha256 { get; set; } = "";

    public string SizeText => P2PShare.FormatSize(Size);
}

/// <summary>Sunucunun istemciye gönderdiği dosya listesi.</summary>
internal sealed class P2PManifest
{
    public string Type { get; set; } = "manifest";
    public string Name { get; set; } = "";
    public string SharedBy { get; set; } = "";
    public List<P2PFile> Files { get; set; } = new();

    public long TotalBytes
    {
        get
        {
            long total = 0;
            foreach (var file in Files) total += file.Size;
            return total;
        }
    }
}

/// <summary>Tek satırlık protokol çerçevesi (get / file / error / bye).</summary>
internal sealed class P2PFrame
{
    public string Type { get; set; } = "";
    public string Path { get; set; } = "";
    public long Size { get; set; }
}

/// <summary>
/// Satır tabanlı küçük bir protokol: her başlık UTF-8 JSON + '\n', ardından
/// (dosya çerçevesinde) ham baytlar gelir. Okuma tamponu kullanılmaz; böylece
/// başlıktan sonra gelen ham veri kaybolmaz.
/// </summary>
internal static class P2PWire
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);

    public static T? Deserialize<T>(string json)
    {
        try { return JsonSerializer.Deserialize<T>(json, Options); }
        catch { return default; }
    }

    public static async Task WriteLineAsync(Stream stream, string line, CancellationToken ct)
    {
        var bytes = Encoding.UTF8.GetBytes(line + "\n");
        await stream.WriteAsync(bytes, ct);
        await stream.FlushAsync(ct);
    }

    public static async Task<string?> ReadLineAsync(Stream stream, CancellationToken ct)
    {
        var buffer = new List<byte>(128);
        var one = new byte[1];

        while (true)
        {
            var read = await stream.ReadAsync(one.AsMemory(0, 1), ct);
            if (read == 0)
                return buffer.Count == 0 ? null : Encoding.UTF8.GetString(buffer.ToArray());

            if (one[0] == (byte)'\n')
                return Encoding.UTF8.GetString(buffer.ToArray()).TrimEnd('\r');

            buffer.Add(one[0]);
            if (buffer.Count > 4_000_000)
                throw new InvalidDataException("P2P başlığı çok büyük.");
        }
    }
}

/// <summary>P2P kodunu (ip:port) çözer ve üretir.</summary>
internal static class P2PCode
{
    public static bool TryParse(string? code, out string host, out int port)
    {
        host = "";
        port = 0;
        var text = (code ?? "").Trim();
        if (text.Length == 0) return false;

        var index = text.LastIndexOf(':');
        if (index <= 0 || index == text.Length - 1) return false;

        host = text[..index].Trim();
        if (host.StartsWith("[") && host.EndsWith("]")) host = host[1..^1];
        if (host.Length == 0) return false;

        return int.TryParse(text[(index + 1)..].Trim(), out port) && port is > 0 and <= 65535;
    }

    public static string Format(string host, int port) => $"{host}:{port}";
}

internal static class P2PShare
{
    public static string FormatSize(long bytes)
    {
        if (bytes >= 1024L * 1024 * 1024) return $"{bytes / 1024.0 / 1024 / 1024:0.00} GB";
        if (bytes >= 1024L * 1024) return $"{bytes / 1024.0 / 1024:0.0} MB";
        if (bytes >= 1024) return $"{bytes / 1024.0:0.0} KB";
        return $"{bytes} B";
    }

    /// <summary>Klasörü (alt klasörleriyle) paylaşılabilir bir zip paketine yazar.</summary>
    public static void ExportZip(string folder, string zipPath, Action<string> log)
    {
        if (!Directory.Exists(folder)) throw new DirectoryNotFoundException(folder);
        if (File.Exists(zipPath)) File.Delete(zipPath);
        ZipFile.CreateFromDirectory(folder, zipPath, CompressionLevel.Optimal, includeBaseDirectory: true);
        log($"Mod klasörü zip olarak dışa aktarıldı: {zipPath}");
    }

    internal static async Task<string> HashFileAsync(string path, CancellationToken ct)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
        using var sha = SHA256.Create();
        var hash = await sha.ComputeHashAsync(stream, ct);
        return Convert.ToHexString(hash);
    }

    /// <summary>Göreli yolu kök klasörün içinde kalacak şekilde birleştirir.</summary>
    internal static string? ResolveInside(string root, string relative)
    {
        if (string.IsNullOrWhiteSpace(relative)) return null;
        if (System.IO.Path.IsPathRooted(relative)) return null;

        var full = System.IO.Path.GetFullPath(System.IO.Path.Combine(root, relative));
        var rootFull = System.IO.Path.GetFullPath(root)
            .TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);

        return full.StartsWith(rootFull + System.IO.Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            ? full
            : null;
    }
}

/// <summary>Bir klasörü yerel ağdaki/arkadaşın bilgisayarındaki eşlere açar.</summary>
internal sealed class P2PShareServer : IDisposable
{
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;

    public int Port { get; private set; }
    public string Root { get; private set; } = "";
    public string ShareName { get; private set; } = "";
    public P2PManifest? Manifest { get; private set; }
    public bool Running => _listener != null;

    public event Action<string>? Log;

    /// <summary>Dosya özetleri hesaplanır, sonra dinleyici açılır.</summary>
    public async Task StartAsync(string root, int port, Action<string>? progress, CancellationToken ct)
    {
        if (Running) throw new InvalidOperationException("Paylaşım zaten açık.");

        Root = System.IO.Path.GetFullPath(root);
        if (!Directory.Exists(Root)) throw new DirectoryNotFoundException(Root);

        ShareName = new DirectoryInfo(Root).Name;
        Manifest = await BuildManifestAsync(ct, progress).ConfigureAwait(true);

        _listener = new TcpListener(IPAddress.Any, port);
        _listener.Start();
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;

        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _ = AcceptLoopAsync(_cts.Token);

        Log?.Invoke($"Paylaşım açıldı: {ShareName} ({Manifest.Files.Count} dosya, " +
                    $"{P2PShare.FormatSize(Manifest.TotalBytes)}), port {Port}");
    }

    private async Task<P2PManifest> BuildManifestAsync(CancellationToken ct, Action<string>? progress)
    {
        var manifest = new P2PManifest
        {
            Name = ShareName,
            SharedBy = AppEnv.Settings.Username
        };

        var files = Directory.EnumerateFiles(Root, "*", SearchOption.AllDirectories).ToList();
        var index = 0;

        foreach (var file in files)
        {
            ct.ThrowIfCancellationRequested();
            index++;

            var relative = System.IO.Path.GetRelativePath(Root, file);
            // Sunucu klasörü bilgi dosyası paylaşımda anlamsız.
            if (string.Equals(relative, "server.json", StringComparison.OrdinalIgnoreCase)) continue;

            var info = new FileInfo(file);
            if (!info.Exists) continue;

            progress?.Invoke($"Hazırlanıyor: {index}/{files.Count} — {relative}");
            var hash = await P2PShare.HashFileAsync(file, ct).ConfigureAwait(true);

            manifest.Files.Add(new P2PFile
            {
                Path = relative.Replace('\\', '/'),
                Size = info.Length,
                Sha256 = hash
            });
        }

        progress?.Invoke($"{manifest.Files.Count} dosya hazır.");
        return manifest;
    }

    private async Task AcceptLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await _listener!.AcceptTcpClientAsync(ct);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                if (ct.IsCancellationRequested) break;
                Log?.Invoke("Bağlantı beklenirken hata: " + ex.Message);
                continue;
            }

            _ = HandleClientAsync(client, ct);
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken ct)
    {
        var remote = (client.Client.RemoteEndPoint as IPEndPoint)?.Address.ToString() ?? "?";
        try
        {
            using (client)
            using (var stream = client.GetStream())
            {
                client.NoDelay = true;
                await P2PWire.WriteLineAsync(stream, P2PWire.Serialize(Manifest!), ct);
                Log?.Invoke($"Eş bağlandı: {remote}");

                while (!ct.IsCancellationRequested)
                {
                    var line = await P2PWire.ReadLineAsync(stream, ct);
                    if (line == null) break;

                    var frame = P2PWire.Deserialize<P2PFrame>(line);
                    if (frame == null) continue;
                    if (frame.Type == "bye") break;
                    if (frame.Type != "get") continue;

                    var full = P2PShare.ResolveInside(Root, frame.Path);
                    if (full == null || !File.Exists(full))
                    {
                        await P2PWire.WriteLineAsync(stream,
                            P2PWire.Serialize(new P2PFrame { Type = "error", Path = frame.Path }), ct);
                        continue;
                    }

                    var info = new FileInfo(full);
                    await P2PWire.WriteLineAsync(stream,
                        P2PWire.Serialize(new P2PFrame { Type = "file", Path = frame.Path, Size = info.Length }), ct);

                    await using var file = new FileStream(full, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
                    await file.CopyToAsync(stream, 81920, ct);
                    await stream.FlushAsync(ct);

                    Log?.Invoke($"Gönderildi: {frame.Path} ({P2PShare.FormatSize(info.Length)}) → {remote}");
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Log?.Invoke($"Eş hatası ({remote}): {ex.Message}");
        }
        finally
        {
            try { client.Close(); } catch { }
        }
    }

    public void Stop()
    {
        try { _cts?.Cancel(); } catch { }
        try { _listener?.Stop(); } catch { }
        _cts?.Dispose();
        _cts = null;
        _listener = null;
        Log?.Invoke("Paylaşım durduruldu.");
    }

    public void Dispose() => Stop();
}

/// <summary>Bir eşe bağlanıp dosyaları özet doğrulamasıyla indirir.</summary>
internal sealed class P2PShareClient : IDisposable
{
    private TcpClient? _client;
    private NetworkStream? _stream;

    public P2PManifest? Manifest { get; private set; }

    public async Task<P2PManifest> ConnectAsync(string host, int port, CancellationToken ct)
    {
        Disconnect();

        _client = new TcpClient { NoDelay = true };
        await _client.ConnectAsync(host, port, ct);
        _stream = _client.GetStream();

        var line = await P2PWire.ReadLineAsync(_stream, ct)
                   ?? throw new IOException("Eş beklenen dosya listesini göndermedi.");
        var manifest = P2PWire.Deserialize<P2PManifest>(line)
                       ?? throw new IOException("Eşin dosya listesi okunamadı.");
        if (!string.Equals(manifest.Type, "manifest", StringComparison.OrdinalIgnoreCase))
            throw new IOException("Eş beklenmeyen bir yanıt gönderdi.");

        Manifest = manifest;
        return manifest;
    }

    /// <summary>
    /// Tüm dosyaları indirir. Hedef klasörde aynı boyut ve özetle bulunan
    /// dosyalar yeniden indirilmez (artımlı eşitleme).
    /// </summary>
    public async Task DownloadAsync(
        string destination,
        IReadOnlyCollection<string>? only,
        Action<P2PFile, long, long>? onFile,
        Action<long, long>? onTotal,
        Action<string>? log,
        CancellationToken ct)
    {
        if (_stream == null || Manifest == null)
            throw new InvalidOperationException("Önce eşe bağlanın.");

        Directory.CreateDirectory(destination);

        var wanted = Manifest.Files
            .Where(f => only == null || only.Count == 0 || only.Contains(f.Path))
            .ToList();

        long totalDone = 0;
        var totalBytes = wanted.Sum(f => f.Size);

        foreach (var file in wanted)
        {
            ct.ThrowIfCancellationRequested();

            var target = P2PShare.ResolveInside(destination, file.Path)
                         ?? throw new IOException("Geçersiz dosya yolu: " + file.Path);
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(target) ?? destination);

            if (File.Exists(target))
            {
                var existing = new FileInfo(target);
                if (existing.Length == file.Size &&
                    string.Equals(await P2PShare.HashFileAsync(target, ct), file.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    totalDone += file.Size;
                    onFile?.Invoke(file, file.Size, file.Size);
                    onTotal?.Invoke(totalDone, totalBytes);
                    log?.Invoke($"Zaten güncel: {file.Path}");
                    continue;
                }
            }

            await P2PWire.WriteLineAsync(_stream,
                P2PWire.Serialize(new P2PFrame { Type = "get", Path = file.Path }), ct);

            var headerLine = await P2PWire.ReadLineAsync(_stream, ct)
                             ?? throw new IOException("Bağlantı kapandı: " + file.Path);
            var header = P2PWire.Deserialize<P2PFrame>(headerLine);
            if (header == null || !string.Equals(header.Type, "file", StringComparison.OrdinalIgnoreCase))
                throw new IOException("Dosya gönderilemedi: " + file.Path);

            var temp = target + ".p2p-part";
            await using (var output = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
            {
                var remaining = header.Size;
                var buffer = new byte[81920];
                long done = 0;

                while (remaining > 0)
                {
                    var read = await _stream.ReadAsync(buffer.AsMemory(0, (int)Math.Min(buffer.Length, remaining)), ct);
                    if (read == 0) throw new IOException("İndirme yarıda kesildi: " + file.Path);
                    await output.WriteAsync(buffer.AsMemory(0, read), ct);
                    remaining -= read;
                    done += read;
                    onFile?.Invoke(file, done, header.Size);
                    onTotal?.Invoke(totalDone + done, totalBytes);
                }
            }

            var hash = await P2PShare.HashFileAsync(temp, ct);
            if (!string.Equals(hash, file.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                File.Delete(temp);
                throw new IOException("Dosya özeti tutmadı (bozuk indirme): " + file.Path);
            }

            if (File.Exists(target)) File.Delete(target);
            File.Move(temp, target);

            totalDone += file.Size;
            onTotal?.Invoke(totalDone, totalBytes);
            log?.Invoke($"İndirildi: {file.Path}");
        }

        try { await P2PWire.WriteLineAsync(_stream, P2PWire.Serialize(new P2PFrame { Type = "bye" }), ct); }
        catch { /* bağlantı zaten kapanmış olabilir */ }
    }

    private void Disconnect()
    {
        try { _stream?.Dispose(); } catch { }
        try { _client?.Close(); } catch { }
        _stream = null;
        _client = null;
    }

    public void Dispose()
    {
        Disconnect();
        Manifest = null;
    }
}
