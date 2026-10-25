using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;

namespace GTACoop.Sync;

/// <summary>
/// Sunucu tarafı UDP köprüsü: relay üzerinden gelen her TCP bağlantısı için
/// ayrı bir yerel UDP soketi açar ve paketleri 127.0.0.1:gamePort adresine taşır.
/// Böylece oyun sunucusu her oyuncuyu farklı bir kaynak portu olarak görür.
/// </summary>
public sealed class RelayGameHost : IAsyncDisposable
{
    private const int MaxDatagram = 65535;
    private readonly int _gamePort;
    private readonly TcpListener _listener;
    private readonly CancellationTokenSource _cts = new();
    private Task? _acceptLoop;

    public int Port { get; }

    public RelayGameHost(int gamePort)
    {
        _gamePort = gamePort;
        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        _acceptLoop = Task.Run(() => AcceptLoopAsync(_cts.Token));
    }

    private async Task AcceptLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            TcpClient client;
            try { client = await _listener.AcceptTcpClientAsync(ct).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }
            catch { continue; }

            _ = Task.Run(() => HandleAsync(client, ct), ct);
        }
    }

    private async Task HandleAsync(TcpClient client, CancellationToken ct)
    {
        using var _ = client;
        client.NoDelay = true;
        using var udp = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        udp.Connect(IPAddress.Loopback, _gamePort);

        var tcp = client.GetStream();
        using var link = CancellationTokenSource.CreateLinkedTokenSource(ct);

        var fromRelay = Task.Run(async () =>
        {
            try
            {
                while (!link.IsCancellationRequested)
                {
                    var packet = await ReadFrameAsync(tcp, link.Token).ConfigureAwait(false);
                    if (packet == null) break;
                    await udp.SendAsync(packet, link.Token).ConfigureAwait(false);
                }
            }
            catch { }
            finally { link.Cancel(); }
        }, link.Token);

        var toRelay = Task.Run(async () =>
        {
            try
            {
                while (!link.IsCancellationRequested)
                {
                    var result = await udp.ReceiveAsync(link.Token).ConfigureAwait(false);
                    if (result.Buffer.Length == 0) continue;
                    await WriteFrameAsync(tcp, result.Buffer, link.Token).ConfigureAwait(false);
                }
            }
            catch { }
            finally { link.Cancel(); }
        }, link.Token);

        await Task.WhenAny(fromRelay, toRelay).ConfigureAwait(false);
    }

    private static async Task<byte[]?> ReadFrameAsync(NetworkStream stream, CancellationToken ct)
    {
        var header = new byte[4];
        if (!await ReadExactAsync(stream, header, ct).ConfigureAwait(false)) return null;
        int len = BinaryPrimitives.ReadInt32LittleEndian(header);
        if (len is <= 0 or > MaxDatagram) return null;
        var payload = new byte[len];
        return await ReadExactAsync(stream, payload, ct).ConfigureAwait(false) ? payload : null;
    }

    private static async Task<bool> ReadExactAsync(NetworkStream stream, byte[] buf, CancellationToken ct)
    {
        int off = 0;
        while (off < buf.Length)
        {
            int n = await stream.ReadAsync(buf.AsMemory(off), ct).ConfigureAwait(false);
            if (n == 0) return false;
            off += n;
        }
        return true;
    }

    private static async Task WriteFrameAsync(NetworkStream stream, byte[] payload, CancellationToken ct)
    {
        var frame = new byte[4 + payload.Length];
        BinaryPrimitives.WriteInt32LittleEndian(frame, payload.Length);
        Buffer.BlockCopy(payload, 0, frame, 4, payload.Length);
        await stream.WriteAsync(frame, ct).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        try { _cts.Cancel(); } catch { }
        try { _listener.Stop(); } catch { }
        if (_acceptLoop != null)
        {
            try { await _acceptLoop.ConfigureAwait(false); } catch { }
        }
        _cts.Dispose();
    }
}

/// <summary>
/// İstemci tarafı UDP köprüsü: oyun 127.0.0.1:LocalPort adresine bağlanır,
/// paketler relay üzerinden TCP ile sunucudaki köprüye taşınır.
/// </summary>
public sealed class RelayGameClient : IAsyncDisposable
{
    private const int MaxDatagram = 65535;
    private readonly UdpClient _udp;
    private readonly TcpClient _tcp;
    private readonly NetworkStream _stream;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _loops;
    private IPEndPoint? _gameEndpoint;

    public int LocalPort { get; }

    private RelayGameClient(TcpClient tcp, UdpClient udp)
    {
        _tcp = tcp;
        _stream = tcp.GetStream();
        _udp = udp;
        LocalPort = ((IPEndPoint)udp.Client.LocalEndPoint!).Port;
        _loops = Task.Run(() => RunAsync(_cts.Token));
    }

    public static async Task<RelayGameClient> StartAsync(
        string relayHost, int relayPort, CancellationToken ct = default)
    {
        var tcp = new TcpClient { NoDelay = true };
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));
            await tcp.ConnectAsync(relayHost, relayPort, timeout.Token).ConfigureAwait(false);
        }
        catch
        {
            tcp.Dispose();
            throw new SyncException("Oyun relay bağlantısı kurulamadı.");
        }

        var udp = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        return new RelayGameClient(tcp, udp);
    }

    private async Task RunAsync(CancellationToken ct)
    {
        using var link = CancellationTokenSource.CreateLinkedTokenSource(ct);

        var fromGame = Task.Run(async () =>
        {
            try
            {
                while (!link.IsCancellationRequested)
                {
                    var result = await _udp.ReceiveAsync(link.Token).ConfigureAwait(false);
                    if (result.Buffer.Length == 0) continue;
                    _gameEndpoint = result.RemoteEndPoint;
                    await WriteFrameAsync(_stream, result.Buffer, link.Token).ConfigureAwait(false);
                }
            }
            catch { }
            finally { link.Cancel(); }
        }, link.Token);

        var toGame = Task.Run(async () =>
        {
            try
            {
                while (!link.IsCancellationRequested)
                {
                    var packet = await ReadFrameAsync(_stream, link.Token).ConfigureAwait(false);
                    if (packet == null) break;
                    var endpoint = _gameEndpoint;
                    if (endpoint != null)
                        await _udp.SendAsync(packet, endpoint, link.Token).ConfigureAwait(false);
                }
            }
            catch { }
            finally { link.Cancel(); }
        }, link.Token);

        await Task.WhenAny(fromGame, toGame).ConfigureAwait(false);
    }

    private static async Task<byte[]?> ReadFrameAsync(NetworkStream stream, CancellationToken ct)
    {
        var header = new byte[4];
        if (!await ReadExactAsync(stream, header, ct).ConfigureAwait(false)) return null;
        int len = BinaryPrimitives.ReadInt32LittleEndian(header);
        if (len is <= 0 or > MaxDatagram) return null;
        var payload = new byte[len];
        return await ReadExactAsync(stream, payload, ct).ConfigureAwait(false) ? payload : null;
    }

    private static async Task<bool> ReadExactAsync(NetworkStream stream, byte[] buf, CancellationToken ct)
    {
        int off = 0;
        while (off < buf.Length)
        {
            int n = await stream.ReadAsync(buf.AsMemory(off), ct).ConfigureAwait(false);
            if (n == 0) return false;
            off += n;
        }
        return true;
    }

    private static async Task WriteFrameAsync(NetworkStream stream, byte[] payload, CancellationToken ct)
    {
        var frame = new byte[4 + payload.Length];
        BinaryPrimitives.WriteInt32LittleEndian(frame, payload.Length);
        Buffer.BlockCopy(payload, 0, frame, 4, payload.Length);
        await stream.WriteAsync(frame, ct).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        try { _cts.Cancel(); } catch { }
        try { _tcp.Close(); } catch { }
        try { _udp.Dispose(); } catch { }
        try { await _loops.ConfigureAwait(false); } catch { }
        _cts.Dispose();
    }
}

/// <summary>
/// Yerel TCP portunu relay üzerinden uzak TCP portuna bağlayan küçük proxy.
/// Senkronizasyon (HTTP) trafiği için kullanılır.
/// </summary>
public sealed class TcpRelayProxy : IAsyncDisposable
{
    private readonly TcpListener _listener;
    private readonly string _remoteHost;
    private readonly int _remotePort;
    private readonly CancellationTokenSource _cts = new();
    private Task? _loop;

    public int LocalPort { get; }

    public TcpRelayProxy(string remoteHost, int remotePort)
    {
        _remoteHost = remoteHost;
        _remotePort = remotePort;
        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        LocalPort = ((IPEndPoint)_listener.LocalEndpoint).Port;
        _loop = Task.Run(() => AcceptLoopAsync(_cts.Token));
    }

    private async Task AcceptLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            TcpClient client;
            try { client = await _listener.AcceptTcpClientAsync(ct).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }
            catch { continue; }

            _ = Task.Run(() => HandleAsync(client, ct), ct);
        }
    }

    private async Task HandleAsync(TcpClient client, CancellationToken ct)
    {
        using var _ = client;
        TcpClient remote;
        try
        {
            remote = new TcpClient { NoDelay = true };
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));
            await remote.ConnectAsync(_remoteHost, _remotePort, timeout.Token).ConfigureAwait(false);
        }
        catch
        {
            return;
        }

        using var __ = remote;
        using var link = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var a = client.GetStream();
        var b = remote.GetStream();
        var up = a.CopyToAsync(b, link.Token);
        var down = b.CopyToAsync(a, link.Token);
        await Task.WhenAny(up, down).ConfigureAwait(false);
        try { link.Cancel(); } catch { }
    }

    public async ValueTask DisposeAsync()
    {
        try { _cts.Cancel(); } catch { }
        try { _listener.Stop(); } catch { }
        if (_loop != null)
        {
            try { await _loop.ConfigureAwait(false); } catch { }
        }
        _cts.Dispose();
    }
}
