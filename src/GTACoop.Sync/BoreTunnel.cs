using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace GTACoop.Sync;

/// <summary>
/// Küçük, hesapsız bir TCP tüneli: bore.pub (veya GTACOOP_RELAY_SERVER ile
/// belirtilen uyumlu sunucu) üzerinden yerel bir TCP portunu internete açar.
/// Protokol bore v0.5 ile uyumludur; JSON iletileri null baytı ile ayrılır.
/// </summary>
public sealed class BoreTunnel : IAsyncDisposable
{
    public const int DefaultControlPort = 7835;

    private readonly string _server;
    private readonly int _controlPort;
    private readonly int _localPort;
    private readonly CancellationTokenSource _cts = new();
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private Task? _loop;
    private volatile bool _disposed;

    public int RemotePort { get; private set; }
    public bool IsConnected { get; private set; }
    public string LastError { get; private set; } = "";
    public string ServerHost => _server;

    /// <summary>Yeniden bağlanınca yeni genel portu bildirir.</summary>
    public event Action<int>? RemotePortChanged;

    private BoreTunnel(string server, int controlPort, int localPort)
    {
        _server = server;
        _controlPort = controlPort;
        _localPort = localPort;
    }

    /// <summary>
    /// Tüneli başlatır ve ilk genel port atanana kadar bekler.
    /// Sunucuya ulaşılamazsa SyncException fırlatır.
    /// </summary>
    public static async Task<BoreTunnel> StartAsync(
        string server, int localPort, CancellationToken ct = default)
    {
        var (host, port) = SplitServer(server);
        var tunnel = new BoreTunnel(host, port, localPort);
        var first = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        tunnel._loop = Task.Run(() => tunnel.RunAsync(first, tunnel._cts.Token));

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(12));
        using var reg = timeout.Token.Register(() => first.TrySetCanceled(timeout.Token));

        try
        {
            tunnel.RemotePort = await first.Task.ConfigureAwait(false);
            return tunnel;
        }
        catch
        {
            await tunnel.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private static (string Host, int Port) SplitServer(string server)
    {
        var text = (server ?? "").Trim();
        if (text.Length == 0) text = "bore.pub";
        if (text.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            text.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            text = text[(text.IndexOf("://", StringComparison.Ordinal) + 3)..];
        var slash = text.IndexOf('/');
        if (slash >= 0) text = text[..slash];
        var colon = text.LastIndexOf(':');
        if (colon > 0 && int.TryParse(text[(colon + 1)..], out var p) && p is > 0 and <= 65535)
            return (text[..colon], p);
        return (text, DefaultControlPort);
    }

    private async Task RunAsync(TaskCompletionSource<int> first, CancellationToken ct)
    {
        int delayMs = 1000;
        while (!ct.IsCancellationRequested)
        {
            TcpClient? control = null;
            try
            {
                control = await ConnectAsync(_server, _controlPort, ct).ConfigureAwait(false);
                var stream = control.GetStream();
                await SendJsonAsync(stream, new { Hello = 0 }, ct).ConfigureAwait(false);
                var frame = await ReadFrameAsync(stream, ct).ConfigureAwait(false);
                if (frame == null)
                    throw new SyncException("Relay sunucusu bağlantıyı kapattı.");

                using var doc = JsonDocument.Parse(frame);
                if (doc.RootElement.TryGetProperty("Hello", out var hello) &&
                    hello.TryGetInt32(out var remote) && remote is > 0 and <= 65535)
                {
                    RemotePort = remote;
                    IsConnected = true;
                    LastError = "";
                    if (!first.Task.IsCompleted) first.TrySetResult(remote);
                    RemotePortChanged?.Invoke(remote);
                }
                else if (doc.RootElement.TryGetProperty("Error", out var err))
                {
                    throw new SyncException("Relay sunucusu: " + (err.GetString() ?? "bilinmeyen hata"));
                }
                else
                {
                    throw new SyncException("Relay sunucusu beklenmeyen yanıt verdi.");
                }

                delayMs = 1000;
                await ReadControlAsync(stream, ct).ConfigureAwait(false);
                throw new SyncException("Relay bağlantısı kapandı.");
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                IsConnected = false;
                LastError = ex.Message;
                if (!first.Task.IsCompleted)
                {
                    first.TrySetException(new SyncException(
                        "Otomatik relay sunucusuna ulaşılamadı: " + ex.Message, ex));
                    return;
                }
            }
            finally
            {
                try { control?.Close(); } catch { }
            }

            try { await Task.Delay(delayMs, ct).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }
            delayMs = Math.Min(delayMs * 2, 15000);
        }
    }

    private async Task ReadControlAsync(NetworkStream stream, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            // Sunucu yaklaşık 500 ms'de bir Heartbeat gönderir. 10 saniye boyunca
            // hiçbir çerçeve gelmezse bağlantı kopmuş say ve yeniden bağlan.
            var frame = await ReadFrameWithTimeoutAsync(
                stream, TimeSpan.FromSeconds(10), ct).ConfigureAwait(false);
            if (frame == null) return;

            using var doc = JsonDocument.Parse(frame);
            var root = doc.RootElement;

            if (root.ValueKind == JsonValueKind.String &&
                string.Equals(root.GetString(), "Heartbeat", StringComparison.Ordinal))
                continue;

            if (root.TryGetProperty("Connection", out var conn) &&
                conn.ValueKind == JsonValueKind.String &&
                Guid.TryParse(conn.GetString(), out var id))
            {
                _ = Task.Run(() => AcceptAsync(id, ct), ct);
            }
            else if (root.TryGetProperty("Error", out var err))
            {
                throw new SyncException("Relay sunucusu: " + (err.GetString() ?? "bilinmeyen hata"));
            }
        }
    }

    private static async Task<string?> ReadFrameWithTimeoutAsync(
        NetworkStream stream, TimeSpan timeout, CancellationToken ct)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(timeout);
        try
        {
            return await ReadFrameAsync(stream, timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new SyncException("Relay kontrol bağlantısı zaman aşımına uğradı.");
        }
    }

    private async Task AcceptAsync(Guid id, CancellationToken ct)
    {
        TcpClient? remote = null;
        TcpClient? local = null;
        try
        {
            remote = await ConnectAsync(_server, _controlPort, ct).ConfigureAwait(false);
            var rs = remote.GetStream();
            await SendJsonAsync(rs, new { Accept = id.ToString("D") }, ct).ConfigureAwait(false);

            local = await ConnectAsync("127.0.0.1", _localPort, ct).ConfigureAwait(false);
            var ls = local.GetStream();

            var up = rs.CopyToAsync(ls, ct);
            var down = ls.CopyToAsync(rs, ct);
            await Task.WhenAny(up, down).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LastError = ex.Message;
        }
        finally
        {
            try { remote?.Close(); } catch { }
            try { local?.Close(); } catch { }
        }
    }

    private static async Task<TcpClient> ConnectAsync(string host, int port, CancellationToken ct)
    {
        var client = new TcpClient { NoDelay = true };
        try
        {
            try
            {
                client.Client.SetSocketOption(
                    SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
            }
            catch { /* platform desteklemiyorsa önemli değil */ }

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));
            await client.ConnectAsync(host, port, timeout.Token).ConfigureAwait(false);
            return client;
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }

    private async Task SendJsonAsync(NetworkStream stream, object message, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(message);
        var bytes = Encoding.UTF8.GetBytes(json + "\0");
        await _writeLock.WaitAsync(ct).ConfigureAwait(false);
        try { await stream.WriteAsync(bytes, ct).ConfigureAwait(false); }
        finally { _writeLock.Release(); }
    }

    private static async Task<string?> ReadFrameAsync(NetworkStream stream, CancellationToken ct)
    {
        // Kontrol çerçeveleri en fazla 256 bayttır; null baytına kadar oku.
        var buf = new byte[256];
        int len = 0;
        while (len < buf.Length)
        {
            int n = await stream.ReadAsync(buf.AsMemory(len, 1), ct).ConfigureAwait(false);
            if (n == 0) return null;
            if (buf[len] == 0)
                return Encoding.UTF8.GetString(buf, 0, len);
            len++;
        }
        throw new SyncException("Relay çerçevesi çok uzun.");
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        try { _cts.Cancel(); } catch { }
        if (_loop != null)
        {
            try { await _loop.ConfigureAwait(false); } catch { }
        }
        _cts.Dispose();
        _writeLock.Dispose();
    }
}
