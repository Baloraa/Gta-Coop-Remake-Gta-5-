using GTACoop.Sync;

namespace GTACoop.Remake;

/// <summary>Sunucunun otomatik relay uç noktaları.</summary>
public sealed record RelayEndpoints(string Host, int SyncPort, int GamePort);

/// <summary>
/// Sunucu tarafında iki otomatik TCP tüneli (senkronizasyon + oyun UDP köprüsü)
/// çalıştırır. Böylece port yönlendirme veya Radmin VPN olmadan da arkadaşlar
/// bağlanabilir. Tüneller bore.pub uyumlu bir sunucu üzerinden geçer.
/// </summary>
public sealed class RelayHostService : IAsyncDisposable
{
    private readonly RelayGameHost _gameHost;
    private readonly BoreTunnel _syncTunnel;
    private readonly BoreTunnel _gameTunnel;
    private bool _disposed;

    public RelayEndpoints Endpoints { get; private set; }

    private RelayHostService(RelayGameHost gameHost,
        BoreTunnel syncTunnel, BoreTunnel gameTunnel)
    {
        _gameHost = gameHost;
        _syncTunnel = syncTunnel;
        _gameTunnel = gameTunnel;
        Endpoints = new RelayEndpoints(syncTunnel.ServerHost, syncTunnel.RemotePort, gameTunnel.RemotePort);
        _syncTunnel.RemotePortChanged += _ => RefreshEndpoints();
        _gameTunnel.RemotePortChanged += _ => RefreshEndpoints();
    }

    /// <summary>Değişen relay portlarını bildirir.</summary>
    public event Action? Changed;

    public static async Task<RelayHostService?> StartAsync(
        int gamePort, int syncPort, Action<string> log, CancellationToken ct = default)
    {
        var server = (Environment.GetEnvironmentVariable("GTACOOP_RELAY_SERVER") ?? "bore.pub").Trim();
        if (server.Length == 0) server = "bore.pub";

        RelayGameHost? gameHost = null;
        BoreTunnel? syncTunnel = null;
        BoreTunnel? gameTunnel = null;
        Task<BoreTunnel>? syncTask = null;
        Task<BoreTunnel>? gameTask = null;
        using var startCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        try
        {
            gameHost = new RelayGameHost(gamePort);
            syncTask = BoreTunnel.StartAsync(server, syncPort, startCts.Token);
            gameTask = BoreTunnel.StartAsync(server, gameHost.Port, startCts.Token);

            try
            {
                await Task.WhenAll(syncTask, gameTask).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // Biri başarısız olursa diğerini de iptal et; yarım kalan tünel
                // internete açık kalmasın.
                startCts.Cancel();
                try { await Task.WhenAll(syncTask, gameTask).ConfigureAwait(false); } catch { }
                throw new SyncException("Relay tüneli kurulamadı: " + ex.Message, ex);
            }

            syncTunnel = syncTask.Result;
            gameTunnel = gameTask.Result;

            var svc = new RelayHostService(gameHost, syncTunnel, gameTunnel);
            log($"Otomatik relay hazır: {svc.Endpoints.Host} " +
                $"(sync {svc.Endpoints.SyncPort}, oyun {svc.Endpoints.GamePort})");
            return svc;
        }
        catch (Exception ex)
        {
            log("Otomatik relay başlatılamadı: " + ex.Message);

            // Task.WhenAll başarısız olsa bile diğer tünel çoktan açılmış olabilir.
            if (syncTunnel == null && syncTask is { IsCompletedSuccessfully: true })
                syncTunnel = syncTask.Result;
            if (gameTunnel == null && gameTask is { IsCompletedSuccessfully: true })
                gameTunnel = gameTask.Result;

            if (syncTunnel != null) await syncTunnel.DisposeAsync().ConfigureAwait(false);
            if (gameTunnel != null) await gameTunnel.DisposeAsync().ConfigureAwait(false);
            if (gameHost != null) await gameHost.DisposeAsync().ConfigureAwait(false);
            return null;
        }
    }

    private void RefreshEndpoints()
    {
        if (_disposed || !_syncTunnel.IsConnected || !_gameTunnel.IsConnected) return;
        var next = new RelayEndpoints(
            _syncTunnel.ServerHost, _syncTunnel.RemotePort, _gameTunnel.RemotePort);
        if (next == Endpoints) return;
        Endpoints = next;
        Changed?.Invoke();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        await _syncTunnel.DisposeAsync().ConfigureAwait(false);
        await _gameTunnel.DisposeAsync().ConfigureAwait(false);
        await _gameHost.DisposeAsync().ConfigureAwait(false);
    }
}

/// <summary>
/// İstemci tarafında relay oturumu: senkronizasyon için yerel TCP proxy,
/// oyun için yerel UDP köprüsü açar. Böylece mevcut bağlantı akışı
/// 127.0.0.1 üzerinden aynen çalışır.
/// </summary>
public sealed class RelaySession : IAsyncDisposable
{
    private readonly TcpRelayProxy _syncProxy;
    private readonly RelayGameClient _gameClient;
    private bool _disposed;

    public int SyncPort => _syncProxy.LocalPort;
    public int GamePort => _gameClient.LocalPort;

    private RelaySession(TcpRelayProxy syncProxy, RelayGameClient gameClient)
    {
        _syncProxy = syncProxy;
        _gameClient = gameClient;
    }

    public static async Task<RelaySession> StartAsync(ServerEntry entry, CancellationToken ct = default)
    {
        if (!entry.UsesRelay)
            throw new SyncException("Sunucuda relay bilgisi yok.");

        var sync = new TcpRelayProxy(entry.RelayHost, entry.RelaySyncPort);
        RelayGameClient? game = null;
        try
        {
            game = await RelayGameClient.StartAsync(entry.RelayHost, entry.RelayGamePort, ct)
                .ConfigureAwait(false);
            return new RelaySession(sync, game);
        }
        catch
        {
            await sync.DisposeAsync().ConfigureAwait(false);
            if (game != null) await game.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        await _syncProxy.DisposeAsync().ConfigureAwait(false);
        await _gameClient.DisposeAsync().ConfigureAwait(false);
    }
}
