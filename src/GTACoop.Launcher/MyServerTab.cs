using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Windows.Forms;
using GTACoop.Sync;

namespace GTACoop.Remake;

public sealed class MyServerTab : UserControl
{
    private readonly TextBox _name = new();
    private readonly NumericUpDown _gamePort = new();
    private readonly NumericUpDown _syncPort = new();
    private readonly NumericUpDown _maxClients = new();
    private readonly TextBox _password = new();
    private readonly TextBox _motd = new();
    private readonly TextBox _token = new();
    private readonly TextBox _shortCode = new();
    private readonly Label _gameServerStatus = new();
    private readonly Label _syncServerStatus = new();
    private readonly Label _shortCodeStatus = new();
    private readonly Label _addressInfo = new();
    private readonly System.Windows.Forms.Timer _timer = new();

    private MyServer? _server;
    private CancellationTokenSource? _publishCts;
    private CancellationTokenSource? _relayCts;
    private RelayHostService? _relayService;
    private string _relayStatus = "";
    private readonly SemaphoreSlim _publishWake = new(0);
    private string _publisherCode = "";
    private string _publishStatus = "";
    private bool _publishOk;

    public MyServerTab()
    {
        Dock = DockStyle.Fill;

        var s = MyServerSettings.Load(AppEnv.DataFolder);

        var info = new Label
        {
            Dock = DockStyle.Top,
            AutoSize = false,
            Height = 50,
            Text = T("Kendi GTACoOp sunucunuzu burada yönetin. Addons klasörüne eklediğiniz her şey, sunucuya bağlanan oyunculara otomatik iner.\r\nSunucu modları oyuncuların oyununa yalnızca oynarken kurulur. Arkadaşlarınız 'Sunucular' sekmesinden bağlanır."),
            ForeColor = Color.DimGray,
            Padding = new Padding(4, 4, 4, 4)
        };

        // --- ayarlar
        var settingsTable = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 8,
            Padding = new Padding(8, 4, 8, 4)
        };
        settingsTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200));
        settingsTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        AddRow(settingsTable, T("Sunucu adı:"), _name, s.ServerName);
        AddRow(settingsTable, T("Oyun portu:"), _gamePort, null, 1, 65535, s.GamePort);
        AddRow(settingsTable, T("Sync portu:"), _syncPort, null, 1, 65535, s.SyncPort);
        AddRow(settingsTable, T("Maks. oyuncu:"), _maxClients, null, 1, 64, s.MaxClients);
        AddRow(settingsTable, T("Şifre (boş=yok):"), _password, s.Password);
        AddRow(settingsTable, T("Motd (karşılama):"), _motd, s.Motd);
        AddRow(settingsTable, T("Sync token:"), _token, s.SyncToken);
        AddRow(settingsTable, T("Kısa kod (boş = kapalı):"), _shortCode, s.ShortCode);
        _shortCode.Name = "ShortCodeBox";
        _shortCode.AccessibleName = T("Kısa kod (boş = kapalı):");
        _shortCode.PlaceholderText = "Gel.com";
        _shortCode.TextChanged += (_, _) => RefreshShortCodeStatus();

        var settingsGroup = new GroupBox
        {
            Text = T("Sunucu Ayarları"),
            Dock = DockStyle.Top,
            Height = 385,
            Padding = new Padding(8)
        };
        var settingsButtons = Ui.Flow(DockStyle.Bottom);
        settingsButtons.Controls.Add(Ui.Btn(T("Ayarları Kaydet"), (_, _) => SaveSettings()));
        settingsButtons.Controls.Add(Ui.Btn(T("Sunucu Klasörünü Aç"), (_, _) => OpenFolder(AppEnv.ServerDir)));
        settingsButtons.Controls.Add(Ui.Btn(T("Addons Klasörünü Aç"), (_, _) =>
            OpenFolder(Path.Combine(AppEnv.ServerDir, "Addons"))));

        settingsGroup.Controls.Add(settingsTable);

        _shortCodeStatus.Name = "ShortCodeStatus";
        _shortCodeStatus.Text = T("Kısa kod: kapalı (isteğe bağlı)");
        _shortCodeStatus.AutoSize = false;
        _shortCodeStatus.Height = 24;
        _shortCodeStatus.Dock = DockStyle.Bottom;
        _shortCodeStatus.TextAlign = ContentAlignment.MiddleLeft;
        _shortCodeStatus.AutoEllipsis = true;
        _shortCodeStatus.ForeColor = Color.DimGray;
        // Tablodaki "Kısa kod" alanının hizasında başlasın (200 px etiket + 8 px iç boşluk).
        _shortCodeStatus.Padding = new Padding(208, 0, 0, 0);
        settingsGroup.Controls.Add(_shortCodeStatus);
        settingsGroup.Controls.Add(settingsButtons);

        // --- modlar
        var modsGroup = new GroupBox
        {
            Text = T("Sunucu Modları"),
            Dock = DockStyle.Top,
            Height = 170,
            Padding = new Padding(8)
        };

        var modsRow1 = Ui.Flow();
        modsRow1.Controls.Add(Ui.Btn(T("DLC Paketi Ekle (araç/ped)..."), (_, _) => AddDlcPack()));
        modsRow1.Controls.Add(Ui.Btn(T("İstemci Scripti Ekle (dll + ini/xml/pdb)..."), (_, _) => AddScriptFiles()));
        modsRow1.Controls.Add(Ui.Btn(T("Script Klasörü Ekle..."), (_, _) => AddScriptFolder()));

        var modsRow2 = Ui.Flow();
        modsRow2.Controls.Add(Ui.Btn(T("ASI Eklentisi Ekle (.asi)..."), (_, _) => AddAsi()));
        modsRow2.Controls.Add(Ui.Btn(T("Sunucu Eklentisi Ekle (plugin dll)..."), (_, _) => AddPlugin()));

        var modsInfo = new Label
        {
            Dock = DockStyle.Bottom,
            AutoSize = false,
            Height = 40,
            ForeColor = Color.DimGray,
            Text = T("DLC paketleri: Addons\\dlcpacks\\  |  Scriptler: Addons\\scripts\\  |  ASI eklentileri: Addons\\asi\\  |  Sunucu eklentileri: Plugins\\\r\nScript/ASI eklentileri yanındaki ini/xml/pdb gibi dosyalarla birlikte eklenir. Meta için: addon.json")
        };
        modsGroup.Controls.Add(modsInfo);
        modsGroup.Controls.Add(modsRow2);
        modsGroup.Controls.Add(modsRow1);

        // --- kontrol
        var controlGroup = new GroupBox
        {
            Text = T("Sunucu Kontrolü"),
            Dock = DockStyle.Top,
            Height = 205,
            Padding = new Padding(8)
        };
        var controlButtons = Ui.Flow();
        controlButtons.Controls.Add(Ui.Btn(T("Sunucuyu Başlat"), (_, _) => StartServer()));
        controlButtons.Controls.Add(Ui.Btn(T("Durdur"), (_, _) => StopServer()));
        controlButtons.Controls.Add(Ui.Btn(T("Durumu Yenile"), (_, _) => RefreshStatus()));
        controlButtons.Controls.Add(Ui.Btn(T("Katılım Kodu Oluştur..."), (_, _) => ShowJoinCode()));
        controlButtons.Controls.Add(Ui.Btn(T("Sunucu Klasörlerini Aç"), (_, _) => OpenFolder(AppEnv.ServersFolder)));

        _gameServerStatus.Text = T("Oyun sunucusu: kapalı");
        _gameServerStatus.AutoSize = true;
        _syncServerStatus.Text = T("Senkronizasyon sunucusu: kapalı");
        _syncServerStatus.AutoSize = true;
        _addressInfo.Dock = DockStyle.Bottom;
        _addressInfo.AutoSize = false;
        _addressInfo.Height = 88;
        _addressInfo.ForeColor = Color.DimGray;

        var statusPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            AutoSize = true
        };
        statusPanel.Controls.Add(_gameServerStatus);
        statusPanel.Controls.Add(_syncServerStatus);

        controlGroup.Controls.Add(_addressInfo);
        controlGroup.Controls.Add(statusPanel);
        controlGroup.Controls.Add(controlButtons);

        // İçerik pencereden uzun olabilir: gerektiğinde dikey kaydırma çubuğu çıksın
        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
        scroll.Controls.Add(controlGroup);
        scroll.Controls.Add(modsGroup);
        scroll.Controls.Add(settingsGroup);
        scroll.Controls.Add(info);
        Controls.Add(scroll);

        _timer.Interval = 1500;
        _timer.Tick += (_, _) => RefreshStatus();
        _timer.Start();

        RefreshStatus();
    }

    private static void AddRow(TableLayoutPanel table, string label, Control control,
        string? value = null, int min = 1, int max = 65535, int numValue = 1)
    {
        table.Controls.Add(new Label { Text = label, AutoSize = true, Padding = new Padding(0, 4, 0, 0) });
        if (control is TextBox tb)
        {
            tb.Text = value ?? "";
            tb.Width = 320;
        }
        else if (control is NumericUpDown num)
        {
            num.Minimum = min;
            num.Maximum = max;
            num.Value = numValue;
            num.Width = 100;
        }
        table.Controls.Add(control);
    }

    private MyServerServerInfo CurrentSettings() => new(
        _name.Text.Trim(),
        (int)_gamePort.Value,
        (int)_syncPort.Value,
        (int)_maxClients.Value,
        _password.Text,
        _motd.Text,
        _token.Text.Trim(),
        _shortCode.Text.Trim());

    private readonly record struct MyServerServerInfo(
        string Name, int GamePort, int SyncPort, int MaxClients,
        string Password, string Motd, string Token, string ShortCode);

    private MyServerSettings ToSettings(MyServerServerInfo i) => new()
    {
        ServerName = i.Name,
        GamePort = i.GamePort,
        SyncPort = i.SyncPort,
        MaxClients = i.MaxClients,
        Password = i.Password,
        Motd = i.Motd,
        SyncToken = i.Token,
        ShortCode = i.ShortCode
    };

    private MyServer Server => _server ??= new MyServer(AppEnv.ServerDir, AppEnv.Log);

    private bool SaveSettings()
    {
        try
        {
            var info = CurrentSettings();

            if (info.ShortCode.Length > 0 &&
                !ShortCodeDirectory.TryNormalize(info.ShortCode, out _, out var shortError))
            {
                Ui.Warn(FindForm()!, T("Kısa kod geçersiz: {0}", shortError));
                return false;
            }

            ToSettings(info).Save(AppEnv.DataFolder);
            Server.EnsureLayout();
            AppEnv.Log($"Sunucu ayarları kaydedildi: {info.Name} (oyun: {info.GamePort}, sync: {info.SyncPort})" +
                       (info.ShortCode.Length > 0 ? $", kısa kod: {info.ShortCode}" : ""));
            return true;
        }
        catch (Exception ex)
        {
            AppEnv.LogError(ex.Message);
            Ui.Error(FindForm()!, T("Ayarlar kaydedilemedi:\r\n") + ex.Message);
            return false;
        }
    }

    /// <summary>Arkadaşla paylaşılacak katılım kodunu üretir (sunucu kapalıyken de çalışır).</summary>
    private void ShowJoinCode()
    {
        var info = CurrentSettings();
        if (string.IsNullOrWhiteSpace(info.Name))
        {
            Ui.Warn(FindForm()!, T("Sunucu adı boş olamaz."));
            return;
        }

        if (info.ShortCode.Length > 0 &&
            !ShortCodeDirectory.TryNormalize(info.ShortCode, out _, out var shortError))
        {
            Ui.Warn(FindForm()!, T("Kısa kod geçersiz: {0}", shortError));
            return;
        }

        var serverRunning = Server.IsGameServerRunning && Server.IsSyncServerRunning;
        var status = _publishStatus;
        var ok = _publishOk;

        if (serverRunning)
        {
            var normalized = "";
            if (ShortCodeDirectory.TryNormalize(info.ShortCode, out normalized, out _) &&
                !string.Equals(normalized, _publisherCode, StringComparison.Ordinal))
            {
                status = T("Kısa kod değişti; yayına almak için sunucuyu yeniden başlatın.");
                ok = false;
            }
        }

        using var dlg = new ShareCodeDialog(ToSettings(info), serverRunning, status, ok,
            _relayService?.Endpoints, _relayStatus);
        dlg.ShowDialog(FindForm());
    }

    private void StartServer()
    {
        try
        {
            var info = CurrentSettings();
            if (string.IsNullOrWhiteSpace(info.Name))
            {
                Ui.Warn(FindForm()!, T("Sunucu adı boş olamaz."));
                return;
            }

            if (!SaveSettings()) return;
            var settings = ToSettings(info);

            Server.EnsureLayout();
            Server.GetOrCreateIdentity(info.Name);
            Server.WriteSettings(settings);

            StopRelay();
            Server.StartGameServer();
            Server.StartSyncServer(settings);
            StartRelay(settings);
            StartPublisher(settings);

            AppEnv.Log($"Sunucu başlatıldı: {info.Name} " +
                       $"(oyun portu {info.GamePort}, sync portu {info.SyncPort})" +
                       (info.ShortCode.Length > 0 ? $", kısa kod: {info.ShortCode}" : ""));

            RefreshStatus();
        }
        catch (Exception ex)
        {
            AppEnv.LogError(ex.Message);
            Ui.Error(FindForm()!, T("Sunucu başlatılamadı:\r\n") + ex.Message);
        }
    }

    private void StopServer()
    {
        try
        {
            StopPublisher(publishOffline: true);
            StopRelay();
            Server.StopAll();
            RefreshStatus();
        }
        catch (Exception ex)
        {
            AppEnv.LogError(ex.Message);
        }
    }

    /// <summary>Launcher kapanırken çağrılır: çalışan sunucu süreçlerini durdurur.</summary>
    public void ShutdownServer()
    {
        try
        {
            _timer.Stop();
            // Uygulama kapatılırken de kısa kodu çevrimdışı bırak; aksi halde
            // arkadaşlar 5 dakikaya kadar eski sunucu bilgisini görebilir.
            StopPublisher(publishOffline: true);
            StopRelay();
            Server.StopAll();
        }
        catch (Exception ex)
        {
            AppEnv.LogError("Sunucu kapatılırken hata: " + ex.Message);
        }
    }

    public void RefreshStatus()
    {
        var info = CurrentSettings();
        bool game = Server.IsGameServerRunning;
        bool sync = Server.IsSyncServerRunning;

        _gameServerStatus.Text = (game ? "✔" : "✘") + " " +
                                 T("Oyun sunucusu: {0} (port {1})", T(game ? "ÇALIŞIYOR" : "kapalı"), info.GamePort);
        _gameServerStatus.ForeColor = game ? Ui.OkColor : Color.DimGray;

        _syncServerStatus.Text = (sync ? "✔" : "✘") + " " +
                                 T("Senkronizasyon sunucusu: {0} (port {1})", T(sync ? "ÇALIŞIYOR" : "kapalı"), info.SyncPort);
        _syncServerStatus.ForeColor = sync ? Ui.OkColor : Color.DimGray;

        var ips = GetLocalIPv4();
        var baseText = game || sync
            ? T("Bağlantı adresleri: ") + string.Join(", ", ips.Select(ip =>
                T("{0}:{1} (oyun) / {2}:{3} (sync)", ip, info.GamePort, ip, info.SyncPort)))
            : T("Sunucu kapalıyken modlar değiştirilebilir; başlatınca otomatik güncellenir.");

        var relay = _relayService?.Endpoints;
        var lines = new List<string> { baseText };
        if (relay != null)
        {
            lines.Add(T("Otomatik relay: {0} (sync {1}, oyun {2})",
                relay.Host, relay.SyncPort, relay.GamePort));
        }
        else if ((game || sync) && _relayStatus.Length > 0)
        {
            lines.Add(_relayStatus);
        }
        _addressInfo.Text = string.Join("\r\n", lines);

        RefreshShortCodeStatus();
    }

    // ------------------------------------------------------------------ otomatik relay

    private void StartRelay(MyServerSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.ShortCode)) return;

        var cts = new CancellationTokenSource();
        _relayCts = cts;
        _relayStatus = T("Otomatik relay başlatılıyor...");
        AppEnv.Log("Otomatik relay başlatılıyor (port yönlendirme/Radmin gerekmez)...");

        _ = Task.Run(async () =>
        {
            try
            {
                var svc = await RelayHostService.StartAsync(
                    settings.GamePort, settings.SyncPort, AppEnv.Log, cts.Token).ConfigureAwait(false);

                if (svc == null)
                {
                    _relayStatus = T("Otomatik relay kullanılamıyor; doğrudan bağlantı adresleri yayınlanacak.");
                    AppEnv.Log("Otomatik relay kullanılamıyor; yalnızca doğrudan adresler yayınlanacak.");
                    return;
                }

                if (cts.IsCancellationRequested || !ReferenceEquals(_relayCts, cts))
                {
                    await svc.DisposeAsync().ConfigureAwait(false);
                    return;
                }

                svc.Changed += OnRelayChanged;
                _relayService = svc;

                // StopRelay, yukarıdaki ilk kontrol ile bu atama arasında
                // çalışmış olabilir; bu durumda servis sahipsiz kalmasın.
                if (cts.IsCancellationRequested || !ReferenceEquals(_relayCts, cts))
                {
                    if (ReferenceEquals(_relayService, svc)) _relayService = null;
                    svc.Changed -= OnRelayChanged;
                    await svc.DisposeAsync().ConfigureAwait(false);
                    return;
                }

                _relayStatus = "";
                _publishWake.Release();

                try
                {
                    if (IsHandleCreated && !IsDisposed) BeginInvoke(RefreshStatus);
                }
                catch { /* pencere kapanırken */ }
            }
            catch (Exception ex)
            {
                _relayStatus = T("Otomatik relay kullanılamıyor; doğrudan bağlantı adresleri yayınlanacak.");
                AppEnv.LogError("Otomatik relay hatası: " + ex.Message);
            }
            finally
            {
                // StopRelay yalnızca iptal eder; CTS'yi onu kullanan görev kapatır.
                cts.Dispose();
            }
        });
    }

    private void OnRelayChanged()
    {
        try { _publishWake.Release(); } catch { }
        try
        {
            if (IsHandleCreated && !IsDisposed) BeginInvoke(RefreshStatus);
        }
        catch { /* pencere kapanırken */ }
    }

    private void StopRelay()
    {
        var cts = _relayCts;
        _relayCts = null;
        try { cts?.Cancel(); } catch { /* görev zaten bitmiş olabilir */ }
        _relayStatus = "";

        var svc = _relayService;
        _relayService = null;
        if (svc != null)
        {
            try
            {
                // UI iş parçacığını kilitlemeden kısa süre bekle.
                if (!Task.Run(async () => await svc.DisposeAsync().ConfigureAwait(false))
                        .Wait(TimeSpan.FromSeconds(6)))
                    AppEnv.Log("Otomatik relay kapatılması arka planda sürüyor.");
            }
            catch (Exception ex)
            {
                AppEnv.Log("Otomatik relay kapatılırken hata: " + ex.Message);
            }
        }
    }

    // ------------------------------------------------------------------ kısa kod yayını

    private void StartPublisher(MyServerSettings settings)
    {
        StopPublisher(publishOffline: false);
        while (_publishWake.Wait(0)) { /* eski uyandırmaları temizle */ }
        _publishStatus = "";
        _publishOk = false;

        if (string.IsNullOrWhiteSpace(settings.ShortCode)) return;
        if (!ShortCodeDirectory.TryNormalize(settings.ShortCode, out var code, out _)) return;

        _publisherCode = code;
        var cts = new CancellationTokenSource();
        _publishCts = cts;
        _ = Task.Run(() => PublishLoopAsync(settings, code, cts.Token));
    }

    private void StopPublisher(bool publishOffline)
    {
        var code = _publisherCode;
        _publisherCode = "";
        var cts = _publishCts;
        _publishCts = null;

        try { cts?.Cancel(); } catch { /* zaten kapanmış */ }

        _publishStatus = "";
        _publishOk = false;

        if (publishOffline && !string.IsNullOrEmpty(code))
        {
            try
            {
                // UI iş parçacığında doğrudan .Wait() çağrısı, async metodun
                // devamının UI'ye post edilmesiyle kilitlenir. Task.Run ile
                // arka planda çalıştırıp en fazla 5 sn bekle.
                if (!Task.Run(() => ShortCodeDirectory.PublishOfflineAsync(
                        code, CancellationToken.None)).Wait(TimeSpan.FromSeconds(5)))
                    AppEnv.Log("Kısa kod çevrimdışı yayını 5 sn içinde tamamlanamadı (arka planda sürüyor).");
            }
            catch (Exception ex)
            {
                AppEnv.Log("Kısa kod çevrimdışı yayını başarısız: " + ex.Message);
            }
        }
    }

    private async Task PublishLoopAsync(MyServerSettings settings, string code, CancellationToken ct)
    {
        var hosts = new List<string>();
        var hostsAt = DateTime.MinValue;
        var lastOk = false;
        var firstPublish = true;
        string lastLogged = "";

        while (!ct.IsCancellationRequested)
        {
            try
            {
                // Relay genellikle 1-2 saniyede hazır olur. İlk yayını hemen
                // yaparsak arkadaş kısa kodu çok erken sorguladığında yalnızca
                // doğrudan adresleri görür; kısa bir süre relay'i bekle.
                if (firstPublish)
                {
                    firstPublish = false;
                    for (int i = 0; i < 10 && _relayCts != null && _relayService == null; i++)
                    {
                        if (ct.IsCancellationRequested) break;
                        await Task.Delay(200, ct);
                    }
                }

                if (hosts.Count == 0 || (DateTime.UtcNow - hostsAt).TotalMinutes >= 10)
                {
                    hosts = await BuildAdvertiseHostsAsync(ct);
                    hostsAt = DateTime.UtcNow;
                }

                var relay = _relayService?.Endpoints;
                var offer = new ShortCodeDirectory.ShortCodeOffer(
                    settings.ServerName,
                    hosts,
                    settings.GamePort,
                    settings.SyncPort,
                    settings.Password,
                    settings.SyncToken,
                    DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    relay?.Host ?? "",
                    relay?.SyncPort ?? 0,
                    relay?.GamePort ?? 0);

                await ShortCodeDirectory.PublishAsync(code, offer, ct);
                SetPublishStatus(T("Kısa kod yayında: {0}", settings.ShortCode), ok: true);

                var logKey = string.Join(",", hosts) + "|" + (relay?.Host ?? "") +
                             ":" + (relay?.SyncPort ?? 0) + "/" + (relay?.GamePort ?? 0);
                if (!lastOk || !string.Equals(lastLogged, logKey, StringComparison.Ordinal))
                {
                    AppEnv.Log($"Kısa kod yayınlandı: {settings.ShortCode} " +
                               $"({hosts.Count} adres: {string.Join(", ", hosts)})" +
                               (relay != null
                                   ? $"; relay {relay.Host} sync {relay.SyncPort}, oyun {relay.GamePort}"
                                   : "; relay yok"));
                    lastLogged = logKey;
                }
                lastOk = true;
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                SetPublishStatus(T("Kısa kod yayınlanamadı: {0}", ex.Message), ok: false);
                if (lastOk) AppEnv.LogError("Kısa kod yayını: " + ex.Message);
                lastOk = false;
            }

            try
            {
                var delay = Task.Delay(TimeSpan.FromSeconds(ShortCodeDirectory.HeartbeatSeconds), ct);
                var wake = _publishWake.WaitAsync(ct);
                await Task.WhenAny(delay, wake);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private static async Task<List<string>> BuildAdvertiseHostsAsync(CancellationToken ct)
    {
        var list = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up) continue;
                if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

                foreach (var ua in ni.GetIPProperties().UnicastAddresses)
                {
                    if (ua.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                    var ip = ua.Address.ToString();
                    if (ip.StartsWith("169.254.")) continue;
                    if (seen.Add(ip)) list.Add(ip);
                }
            }
        }
        catch { /* adres listesi kritik değil */ }

        var publicIp = await ShortCodeDirectory.GetPublicIpAsync(ct);
        if (!string.IsNullOrEmpty(publicIp) && seen.Add(publicIp))
            list.Add(publicIp);

        // Radmin VPN (26.x) ve genel IP önce denenir; istemci adresleri paralel dener.
        list.Sort((a, b) => ScoreHost(a, publicIp).CompareTo(ScoreHost(b, publicIp)));
        if (list.Count > 12) list = list.Take(12).ToList();
        return list;
    }

    private static int ScoreHost(string ip, string? publicIp)
    {
        if (ip.StartsWith("26.")) return 0;
        if (ip == publicIp) return 1;
        if (ip.StartsWith("192.168.") || ip.StartsWith("10.") ||
            ip.StartsWith("172.")) return 2;
        return 3;
    }

    private void SetPublishStatus(string text, bool ok)
    {
        _publishStatus = text;
        _publishOk = ok;

        try
        {
            if (IsDisposed || !IsHandleCreated) return;
            if (InvokeRequired) BeginInvoke(RefreshShortCodeStatus);
            else RefreshShortCodeStatus();
        }
        catch { /* pencere kapanırken */ }
    }

    private void RefreshShortCodeStatus()
    {
        var code = _shortCode.Text.Trim();
        var normalized = ShortCodeDirectory.TryNormalize(code, out var n, out _) ? n : "";
        bool running = Server.IsGameServerRunning && Server.IsSyncServerRunning;

        if (code.Length == 0)
        {
            _shortCodeStatus.Text = T("Kısa kod: kapalı (isteğe bağlı)");
            _shortCodeStatus.ForeColor = Color.DimGray;
        }
        else if (!running)
        {
            _shortCodeStatus.Text = T("Kısa kod: {0} (sunucu başlatılınca yayınlanır)", code);
            _shortCodeStatus.ForeColor = Color.DimGray;
        }
        else if (normalized.Length > 0 &&
                 !string.Equals(normalized, _publisherCode, StringComparison.Ordinal))
        {
            _shortCodeStatus.Text =
                T("Kısa kod değişti; yayına almak için sunucuyu yeniden başlatın.");
            _shortCodeStatus.ForeColor = Ui.WarnColor;
        }
        else if (!string.IsNullOrEmpty(_publishStatus))
        {
            _shortCodeStatus.Text = _publishStatus;
            _shortCodeStatus.ForeColor = _publishOk ? Ui.OkColor : Ui.WarnColor;
        }
        else
        {
            _shortCodeStatus.Text = T("Kısa kod: {0} (yayın hazırlanıyor...)", code);
            _shortCodeStatus.ForeColor = Color.DimGray;
        }
    }

    private static List<string> GetLocalIPv4()
    {
        try
        {
            return Dns.GetHostAddresses(Dns.GetHostName())
                .Where(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                .Select(a => a.ToString())
                .ToList();
        }
        catch
        {
            return new List<string> { "127.0.0.1" };
        }
    }

    private void OpenFolder(string path)
    {
        try
        {
            Directory.CreateDirectory(path);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            AppEnv.LogError("Klasör açılamadı: " + ex.Message);
        }
    }

    // ------------------------------------------------------------------ mod ekleme

    private void AddDlcPack()
    {
        using var dlg = new FolderBrowserDialog
        {
            Description = T("İçinde dlc.rpf bulunan DLC paketi klasörünü seçin.")
        };
        if (dlg.ShowDialog(FindForm()) != DialogResult.OK) return;

        try
        {
            var name = new DirectoryInfo(dlg.SelectedPath).Name;
            var target = Path.Combine(ServerDirAddons(), "dlcpacks", name);
            if (Directory.Exists(target))
            {
                if (!Ui.Confirm(FindForm()!, T("'{0}' zaten Addons\\dlcpacks içinde. Üzerine yazılsın mı?", name)))
                    return;
                Directory.Delete(target, recursive: true);
            }

            UseWaitCursor = true;
            FileUtil.CopyTree(dlg.SelectedPath, target);
            Ui.Info(FindForm()!, T("DLC paketi eklendi: {0}\r\nSunucu çalışıyorsa bir sonraki manifest isteğinde otomatik dağıtılır.", name));
            AppEnv.Log($"Sunucu DLC paketi eklendi: {name}");
        }
        catch (Exception ex)
        {
            AppEnv.LogError(ex.Message);
            Ui.Error(FindForm()!, T("Paket eklenemedi:\r\n") + ex.Message);
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private void AddScriptFiles()
    {
        using var dlg = new OpenFileDialog
        {
            Title = T("İstemci script dosyalarını seçin (dll ve yanındaki ini/xml/pdb vb.)."),
            Filter = T("Script ve destek dosyaları|*.dll;*.cs;*.vb;*.ini;*.xml;*.pdb;*.json;*.txt;*.log;*.cfg;*.config|Tüm dosyalar (*.*)|*.*"),
            Multiselect = true
        };
        if (dlg.ShowDialog(FindForm()) != DialogResult.OK) return;

        try
        {
            // Grup adı: ilk dll/cs/vb dosyasının adı; yoksa ilk dosyanın adı.
            var main = dlg.FileNames.FirstOrDefault(f =>
                new[] { ".dll", ".cs", ".vb" }.Contains(Path.GetExtension(f).ToLowerInvariant()))
                ?? dlg.FileNames[0];
            var group = Path.GetFileNameWithoutExtension(main);
            var targetDir = Path.Combine(ServerDirAddons(), "scripts", group);
            Directory.CreateDirectory(targetDir);

            foreach (var file in dlg.FileNames)
                File.Copy(file, Path.Combine(targetDir, Path.GetFileName(file)), overwrite: true);

            Ui.Info(FindForm()!, T("{0} dosya eklendi: Addons\\scripts\\{1}\r\nOyunculara bir sonraki bağlantıda otomatik indirilir.",
                dlg.FileNames.Length, group));
            AppEnv.Log($"Sunucu scripti eklendi: {group} ({dlg.FileNames.Length} dosya)");
        }
        catch (Exception ex)
        {
            AppEnv.LogError(ex.Message);
            Ui.Error(FindForm()!, T("Script eklenemedi:\r\n") + ex.Message);
        }
    }

    private void AddScriptFolder()
    {
        using var dlg = new FolderBrowserDialog
        {
            Description = T("Script klasörünü seçin; içindeki tüm dosyalar (dll, ini, alt klasörler) kopyalanır.")
        };
        if (dlg.ShowDialog(FindForm()) != DialogResult.OK) return;

        try
        {
            var name = new DirectoryInfo(dlg.SelectedPath).Name;
            var target = Path.Combine(ServerDirAddons(), "scripts", name);
            if (Directory.Exists(target))
            {
                if (!Ui.Confirm(FindForm()!, T("Addons\\scripts\\{0} zaten var. Üzerine yazılsın mı?", name)))
                    return;
                Directory.Delete(target, recursive: true);
            }

            UseWaitCursor = true;
            FileUtil.CopyTree(dlg.SelectedPath, target);
            Ui.Info(FindForm()!, T("Script klasörü eklendi: {0}\r\nOyunculara bir sonraki bağlantıda otomatik indirilir.", name));
            AppEnv.Log($"Sunucu script klasörü eklendi: {name}");
        }
        catch (Exception ex)
        {
            AppEnv.LogError(ex.Message);
            Ui.Error(FindForm()!, T("Klasör eklenemedi:\r\n") + ex.Message);
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private void AddAsi()
    {
        using var dlg = new OpenFileDialog
        {
            Title = T("ASI eklentisini ve varsa destek dosyalarını (ini/xml) seçin."),
            Filter = T("ASI eklentisi ve destek dosyaları|*.asi;*.ini;*.xml;*.pdb;*.log;*.txt;*.cfg;*.config|Tüm dosyalar (*.*)|*.*"),
            Multiselect = true
        };
        if (dlg.ShowDialog(FindForm()) != DialogResult.OK) return;

        try
        {
            var allowed = ServerScanner.AsiExtensions;
            var files = dlg.FileNames
                .Where(f => allowed.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
                .ToList();
            var rejected = dlg.FileNames.Length - files.Count;

            if (files.Count == 0)
            {
                Ui.Warn(FindForm()!,
                    T("Seçilen dosyalar desteklenmiyor.\r\nDesteklenen uzantılar: {0}", string.Join(", ", ServerScanner.AsiExtensions)));
                return;
            }

            var asi = files.FirstOrDefault(f =>
                Path.GetExtension(f).Equals(".asi", StringComparison.OrdinalIgnoreCase)) ?? files[0];
            var group = Path.GetFileNameWithoutExtension(asi);
            var targetDir = Path.Combine(ServerDirAddons(), "asi", group);
            Directory.CreateDirectory(targetDir);

            foreach (var file in files)
                File.Copy(file, Path.Combine(targetDir, Path.GetFileName(file)), overwrite: true);

            Ui.Info(FindForm()!, T("ASI eklentisi eklendi: {0}\r\n{1}Oyuncuların oyun kök klasörüne oynarken kurulur; oyundan çıkınca otomatik kaldırılır.",
                group, rejected > 0 ? T("{0} dosya desteklenmeyen türde olduğu için atlandı.\r\n", rejected) : ""));
            AppEnv.Log($"Sunucu ASI eklentisi eklendi: {group} ({files.Count} dosya)");
        }
        catch (Exception ex)
        {
            AppEnv.LogError(ex.Message);
            Ui.Error(FindForm()!, T("ASI eklentisi eklenemedi:\r\n") + ex.Message);
        }
    }

    private void AddPlugin()
    {
        using var dlg = new OpenFileDialog
        {
            Title = T("Sunucu eklentisi (plugin) dll dosyalarını seçin."),
            Filter = T("Sunucu eklentisi (*.dll)|*.dll"),
            Multiselect = true
        };
        if (dlg.ShowDialog(FindForm()) != DialogResult.OK) return;

        try
        {
            var pluginsDir = Path.Combine(AppEnv.ServerDir, "Plugins");
            Directory.CreateDirectory(pluginsDir);
            foreach (var file in dlg.FileNames)
                File.Copy(file, Path.Combine(pluginsDir, Path.GetFileName(file)), overwrite: true);

            Ui.Info(FindForm()!, T("Eklenti(ler) Plugins klasörüne eklendi.\r\nSunucuyu yeniden başlatınca yüklenecek (ayarlar otomatik güncellenir)."));
        }
        catch (Exception ex)
        {
            AppEnv.LogError(ex.Message);
            Ui.Error(FindForm()!, T("Eklenti eklenemedi:\r\n") + ex.Message);
        }
    }

    private static string ServerDirAddons() => Path.Combine(AppEnv.ServerDir, "Addons");
}
