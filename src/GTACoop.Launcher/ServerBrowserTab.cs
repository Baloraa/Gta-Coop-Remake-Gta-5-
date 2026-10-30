using System.Text;
using System.Text.Json;
using System.Windows.Forms;
using GTACoop.Sync;

namespace GTACoop.Remake;

/// <summary>
/// GTACoOp'un oyun içi sunucu tarayıcısının launcher karşılığı.
/// Master listesi + LAN + doğrudan adres ile sunucu bulur; sürüm, oyuncu
/// sayısı, mod ve ping bilgilerini gösterir. Bağlantıda sync sunucusu varsa
/// modları kurar, yoksa oyunu modsuz başlatır.
/// </summary>
public sealed class ServerBrowserTab : UserControl
{
    private readonly ServersTab _serversTab;
    private readonly ListView _list = new();
    private readonly TextBox _details = new();
    private readonly TextBox _masterUrl = new();
    private readonly TextBox _directAddress = new();
    private readonly Label _status = new();
    private readonly ProgressBar _progress = new();
    private readonly Button _refreshBtn;
    private readonly Button _lanBtn;
    private readonly Button _cancelBtn;
    private readonly Button _connectBtn;
    private readonly Button _gameOnlyBtn;
    private readonly Button _addBtn;
    private readonly Button _directBtn;
    private readonly Dictionary<string, ListViewItem> _rows = new(StringComparer.OrdinalIgnoreCase);
    private CancellationTokenSource? _scanCts;
    private bool _scanning;
    private bool _connecting;
    private bool _autoScanned;

    public ServerBrowserTab(ServersTab serversTab)
    {
        _serversTab = serversTab;
        Dock = DockStyle.Fill;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 7,
            Padding = new Padding(0)
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 142));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));

        var info = new Label
        {
            Dock = DockStyle.Fill,
            AutoSize = false,
            Text = T("GTA CooP sunucularını master listesinden, LAN'dan veya doğrudan adresten bulun. " +
                     "Sürüm, oyuncu sayısı, mod, şifre ve ping bilgileri discovery ile okunur."),
            ForeColor = Color.DimGray,
            Padding = new Padding(2, 4, 2, 4)
        };

        var toolbar = Ui.Flow();
        _refreshBtn = Ui.Btn(T("Sunucuları Tara"), (_, _) => _ = ScanAsync(false));
        _lanBtn = Ui.Btn(T("LAN Tara"), (_, _) => _ = ScanAsync(true));
        _cancelBtn = Ui.Btn(T("İptal"), (_, _) => _scanCts?.Cancel());
        _cancelBtn.Enabled = false;
        var addServerBtn = Ui.Btn(T("Sunucu Ekle..."), (_, _) => AddServerManually());
        toolbar.Controls.Add(_refreshBtn);
        toolbar.Controls.Add(_lanBtn);
        toolbar.Controls.Add(_cancelBtn);
        toolbar.Controls.Add(addServerBtn);

        var addressRow = Ui.Flow();
        addressRow.Controls.Add(new Label
        {
            Text = T("Liste adresi:"),
            AutoSize = true,
            Margin = new Padding(4, 8, 2, 2)
        });
        _masterUrl.Width = 390;
        _masterUrl.Margin = new Padding(2, 5, 8, 4);
        _masterUrl.Text = string.IsNullOrWhiteSpace(AppEnv.Settings.ServerBrowserMasterUrl)
            ? ServerBrowserDiscovery.DefaultMasterUrl
            : AppEnv.Settings.ServerBrowserMasterUrl;
        _masterUrl.PlaceholderText = ServerBrowserDiscovery.DefaultMasterUrl;
        addressRow.Controls.Add(_masterUrl);
        addressRow.Controls.Add(new Label
        {
            Text = T("Doğrudan:"),
            AutoSize = true,
            Margin = new Padding(10, 8, 2, 2)
        });
        _directAddress.Width = 150;
        _directAddress.Margin = new Padding(2, 5, 4, 4);
        _directAddress.PlaceholderText = "IP:port";
        _directBtn = Ui.Btn(T("Bağlan"), (_, _) => _ = DirectConnectAsync());
        addressRow.Controls.Add(_directAddress);
        addressRow.Controls.Add(_directBtn);

        Ui.StyleList(_list,
            (T("Sunucu"), 250),
            (T("Adres"), 170),
            (T("Oyuncular"), 80),
            (T("Durum"), 90),
            (T("Mod"), 100),
            (T("Şifre"), 60),
            (T("Ping"), 65),
            (T("Kaynak"), 110));
        _list.Dock = DockStyle.Fill;
        _list.DoubleClick += (_, _) => _ = ConnectSelectedAsync(gameOnly: false);
        _list.SelectedIndexChanged += (_, _) => UpdateDetails();

        var detailsGroup = new GroupBox
        {
            Text = T("Sunucu Detayları"),
            Dock = DockStyle.Fill,
            Padding = new Padding(8)
        };
        _details.Dock = DockStyle.Fill;
        _details.Multiline = true;
        _details.ReadOnly = true;
        _details.ScrollBars = ScrollBars.Vertical;
        _details.BackColor = Color.WhiteSmoke;
        _details.Font = new Font("Consolas", 9f);
        _details.Text = T("Bir sunucu seçin.");
        detailsGroup.Controls.Add(_details);

        var actions = Ui.Flow();
        _connectBtn = Ui.Btn(T("Bağlan ve Oyna"), (_, _) => _ = ConnectSelectedAsync(gameOnly: false));
        _connectBtn.Font = new Font(_connectBtn.Font, FontStyle.Bold);
        _gameOnlyBtn = Ui.Btn(T("Modsuz Bağlan"), (_, _) => _ = ConnectSelectedAsync(gameOnly: true));
        _addBtn = Ui.Btn(T("Sunuculara Ekle"), (_, _) => AddSelectedToList());
        actions.Controls.Add(_connectBtn);
        actions.Controls.Add(_gameOnlyBtn);
        actions.Controls.Add(_addBtn);

        var statusPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0)
        };
        statusPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 260));
        statusPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _progress.Dock = DockStyle.Fill;
        _progress.Minimum = 0;
        _progress.Maximum = 100;
        _progress.Style = ProgressBarStyle.Continuous;
        _progress.Margin = new Padding(0, 10, 8, 10);
        _status.Dock = DockStyle.Fill;
        _status.AutoEllipsis = true;
        _status.TextAlign = ContentAlignment.MiddleLeft;
        _status.ForeColor = Color.DimGray;
        _status.Text = T("Hazır. Sunucuları taramak için 'Sunucuları Tara' düğmesine basın.");
        statusPanel.Controls.Add(_progress, 0, 0);
        statusPanel.Controls.Add(_status, 1, 0);

        root.Controls.Add(info, 0, 0);
        root.Controls.Add(toolbar, 0, 1);
        root.Controls.Add(addressRow, 0, 2);
        root.Controls.Add(_list, 0, 3);
        root.Controls.Add(detailsGroup, 0, 4);
        root.Controls.Add(actions, 0, 5);
        root.Controls.Add(statusPanel, 0, 6);
        Controls.Add(root);
    }

    /// <summary>Sekme ilk kez açıldığında master listesini otomatik tarar.</summary>
    public void OnShown()
    {
        if (_autoScanned) return;
        _autoScanned = true;
        _ = ScanAsync(false);
    }

    private async Task ScanAsync(bool lanOnly)
    {
        if (_scanning || _connecting) return;

        _scanning = true;
        _scanCts?.Cancel();
        _scanCts?.Dispose();
        _scanCts = new CancellationTokenSource();
        var ct = _scanCts.Token;
        SetScanUi(true);

        _rows.Clear();
        _list.Items.Clear();
        _details.Text = T("Bir sunucu seçin.");
        SetProgress(0);
        SetStatus(lanOnly ? T("LAN taranıyor...") : T("Sunucular taranıyor..."), Color.DimGray);

        var progress = new Progress<ServerScanProgress>(p =>
        {
            if (IsDisposed) return;
            SetStatus(p.Message, Color.DimGray);
            if (p.Total > 0)
                SetProgress((int)Math.Clamp(100.0 * p.Sent / p.Total, 0, 100));
        });
        var add = new Action<DiscoveredServer>(server =>
        {
            if (IsDisposed) return;
            if (InvokeRequired)
            {
                try { BeginInvoke(() => AddOrUpdateRow(server)); } catch { }
                return;
            }
            AddOrUpdateRow(server);
        });

        try
        {
            if (!lanOnly)
            {
                AppEnv.Settings.ServerBrowserMasterUrl = _masterUrl.Text.Trim();
                AppEnv.SaveSettings();
            }

            var result = lanOnly
                ? await ServerBrowserDiscovery.ScanLanAsync(progress, add, ct)
                : await ServerBrowserDiscovery.ScanAsync(_masterUrl.Text, progress, add, ct);

            if (ct.IsCancellationRequested) return;
            foreach (var server in result.Servers) AddOrUpdateRow(server);
            SortRows();

            SetProgress(100);
            if (lanOnly)
            {
                SetStatus(T("{0} yerel sunucu yanıt verdi.", result.Responded),
                    result.Responded > 0 ? Ui.OkColor : Ui.WarnColor);
            }
            else
            {
                var source = result.MasterUrlUsed != null
                    ? T("Master: {0}", result.MasterUrlUsed)
                    : T("Master listesine ulaşılamadı; LAN/doğrudan bağlantı kullanılabilir.");
                SetStatus(T("{0} sunucu yanıt verdi ({1} sorgu). {2}",
                        result.Responded, result.Queried, source),
                    result.Responded > 0 ? Ui.OkColor : Ui.WarnColor);
            }

            foreach (var error in result.Errors)
                AppEnv.Log("Sunucu Tarayıcı: " + error);

            // Oyun içi F9 tarayıcısı launcher kapalıyken de bu listeyi
            // gösterebilsin diye son başarılı taramayı önbelleğe yaz.
            if (!lanOnly && result.Servers.Count > 0)
                SaveClientServerCache(result.Servers);

            if (_list.Items.Count > 0)
            {
                _list.Items[0].Selected = true;
                _list.Items[0].Focused = true;
            }
            else
            {
                SetStatus(T("Yanıt veren sunucu bulunamadı. LAN veya doğrudan bağlantı deneyin."), Ui.WarnColor);
            }
        }
        catch (OperationCanceledException)
        {
            SetStatus(T("Tarama iptal edildi."), Ui.WarnColor);
        }
        catch (Exception ex)
        {
            SetStatus(T("Tarama hatası: ") + ex.Message, Ui.ErrColor);
            AppEnv.LogError("Sunucu Tarayıcı: " + ex.Message);
        }
        finally
        {
            _scanning = false;
            SetScanUi(false);
            _scanCts?.Dispose();
            _scanCts = null;
        }
    }

    private async Task ConnectSelectedAsync(bool gameOnly)
    {
        if (_scanning || _connecting) return;
        var server = Selected;
        if (server == null)
        {
            Ui.Warn(FindForm()!, T("Önce listeden bir sunucu seçin."));
            return;
        }

        if (!ConfirmVersion(server)) return;
        var password = ResolvePassword(server);
        if (password == null) return;

        if (_serversTab.IsBusy)
        {
            Ui.Warn(FindForm()!, T("Launcher zaten bir sunucuya bağlanıyor. Lütfen bekleyin."));
            return;
        }

        var entry = BuildEntry(server);
        entry.Password = password;

        _connecting = true;
        SetActionButtons(false);
        SetStatus(gameOnly ? T("Modsuz bağlantı hazırlanıyor...") : T("Sunucuya bağlanılıyor..."), Color.DimGray);

        try
        {
            if (FindForm() is MainForm main) main.ShowServersTab();
            if (gameOnly)
                await _serversTab.ConnectGameOnlyFromBrowserAsync(entry, launch: true);
            else
                await _serversTab.ConnectFromBrowserAsync(entry, launch: true);
        }
        finally
        {
            _connecting = false;
            SetActionButtons(true);
        }
    }

    private async Task DirectConnectAsync()
    {
        if (_scanning || _connecting) return;
        if (!ServerBrowserDiscovery.TryParseEndpoint(_directAddress.Text, out var host, out var port))
        {
            Ui.Warn(FindForm()!, T("Adresi IP:port biçiminde girin (ör. 127.0.0.1:4499)."));
            return;
        }
        if (_serversTab.IsBusy)
        {
            Ui.Warn(FindForm()!, T("Launcher zaten bir sunucuya bağlanıyor. Lütfen bekleyin."));
            return;
        }

        _connecting = true;
        SetActionButtons(false);
        SetStatus(T("Doğrudan sunucu yoklanıyor..."), Color.DimGray);

        try
        {
            DiscoveredServer? found = null;
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                found = await ServerBrowserDiscovery.ProbeAsync(host, port, T("Doğrudan"), cts.Token);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                AppEnv.Log("Doğrudan sunucu yoklaması başarısız: " + ex.Message);
            }

            if (found != null)
            {
                AddOrUpdateRow(found);
                if (!ConfirmVersion(found)) return;
            }
            else if (!Ui.Confirm(FindForm()!,
                         T("Sunucu discovery yanıtı vermedi. Yine de doğrudan bağlanılsın mı?\r\n" +
                           "Sunucu açık ve port yönlendirmesi doğruysa bağlantı kurulabilir.")))
            {
                return;
            }

            var password = found != null
                ? ResolvePassword(found)
                : AppEnv.Settings.Servers.FirstOrDefault(s =>
                    string.Equals(s.Host, host, StringComparison.OrdinalIgnoreCase) && s.GamePort == port)?.Password;
            if (password == null) return;

            var entry = new ServerEntry
            {
                Name = found?.Name ?? T("Doğrudan Sunucu {0}:{1}", host, port),
                Host = found?.Host ?? host,
                GamePort = found?.GamePort ?? port,
                SyncPort = (found?.GamePort ?? port) + 100,
                Password = password,
                AutoConnect = true
            };

            if (FindForm() is MainForm main) main.ShowServersTab();
            await _serversTab.ConnectFromBrowserAsync(entry, launch: true);
        }
        finally
        {
            _connecting = false;
            SetActionButtons(true);
        }
    }

    private void AddSelectedToList()
    {
        var server = Selected;
        if (server == null)
        {
            Ui.Warn(FindForm()!, T("Önce listeden bir sunucu seçin."));
            return;
        }

        var password = ResolvePassword(server);
        if (password == null) return;

        var entry = BuildEntry(server);
        entry.Password = password;
        _serversTab.AddServerFromBrowser(entry);
        SetStatus(T("Sunucu 'Sunucular' listesine eklendi."), Ui.OkColor);
    }

    private void AddServerManually()
    {
        using var dlg = new ServerEditDialog();
        if (dlg.ShowDialog(FindForm()) != DialogResult.OK) return;
        _serversTab.AddServerFromBrowser(dlg.Entry);
        SetStatus(T("Sunucu 'Sunucular' listesine eklendi."), Ui.OkColor);
    }

    private static ServerEntry BuildEntry(DiscoveredServer server) => new()
    {
        Name = server.Name,
        Host = server.Host,
        GamePort = server.GamePort,
        SyncPort = server.GamePort + 100,
        AutoConnect = true
    };

    /// <summary>
    /// Oyun içi GTACoOp sunucu tarayıcısının okuduğu önbelleği yazar.
    /// Böylece launcher kapalıyken (normal başlatmada) F9 listesi boş kalmaz.
    /// </summary>
    private static void SaveClientServerCache(IReadOnlyList<DiscoveredServer> servers)
    {
        try
        {
            var path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "cgmp", "gtacoop", "servers.json");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            var entries = new List<ClientServerCacheEntry>();
            if (File.Exists(path))
            {
                try
                {
                    entries = JsonSerializer.Deserialize<List<ClientServerCacheEntry>>(
                        File.ReadAllText(path)) ?? new List<ClientServerCacheEntry>();
                }
                catch
                {
                    entries = new List<ClientServerCacheEntry>();
                }
            }

            foreach (var server in servers)
            {
                var entry = entries.FirstOrDefault(e =>
                    string.Equals(e.Host, server.Host, StringComparison.OrdinalIgnoreCase) &&
                    e.Port == server.GamePort);
                if (entry == null)
                {
                    entry = new ClientServerCacheEntry();
                    entries.Add(entry);
                }

                entry.Name = server.Name;
                entry.Host = server.Host;
                entry.Port = server.GamePort;
                entry.PlayerCount = server.PlayerCount;
                entry.MaxPlayers = server.MaxPlayers;
                entry.PasswordProtected = server.PasswordProtected;
                entry.Gamemode = server.Gamemode;
                entry.ProtocolVersion = server.ProtocolVersion;
                entry.Description = server.Source;
                entry.LastSeenUtc = DateTime.UtcNow;
            }

            var ordered = entries
                .OrderByDescending(e => e.LastSeenUtc)
                .Take(300)
                .ToList();

            var tmp = path + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(ordered,
                new JsonSerializerOptions { WriteIndented = true }));
            File.Move(tmp, path, overwrite: true);
        }
        catch (Exception ex)
        {
            AppEnv.Log("Sunucu Tarayıcı önbelleği yazılamadı: " + ex.Message);
        }
    }

    private sealed class ClientServerCacheEntry
    {
        public string Name { get; set; } = "";
        public string Host { get; set; } = "";
        public int Port { get; set; }
        public int PlayerCount { get; set; }
        public int MaxPlayers { get; set; }
        public bool PasswordProtected { get; set; }
        public string Gamemode { get; set; } = "";
        public int ProtocolVersion { get; set; }
        public string Description { get; set; } = "";
        public DateTime LastSeenUtc { get; set; }
    }

    private string? ResolvePassword(DiscoveredServer server)
    {
        var existing = AppEnv.Settings.Servers.FirstOrDefault(s =>
            string.Equals(s.Host, server.Host, StringComparison.OrdinalIgnoreCase) &&
            s.GamePort == server.GamePort);
        if (!string.IsNullOrEmpty(existing?.Password)) return existing.Password;
        if (!server.PasswordProtected) return "";

        using var dlg = new PasswordPromptDialog(server.Name);
        return dlg.ShowDialog(FindForm()) == DialogResult.OK ? dlg.Password : null;
    }

    private bool ConfirmVersion(DiscoveredServer server)
    {
        if (server.ProtocolVersion == ServerBrowserDiscovery.CurrentProtocolVersion) return true;

        var version = ServerBrowserDiscovery.VersionText(server.ProtocolVersion);
        var state = ServerBrowserDiscovery.VersionState(server.ProtocolVersion);
        var note = server.ProtocolVersion == 0
            ? T("Sunucu protokol sürümünü bildirmiyor. Eski bir GTACoOp sunucusu olabilir; " +
                "güncel istemci sunucu tarafından reddedilebilir.")
            : T("Sunucu protokolü: {0} ({1}). Güncel istemci: {2}. " +
                "Sunucu eski/yeni istemcileri kabul etmiyorsa bağlantı reddedilebilir.",
                version, state, ServerBrowserDiscovery.VersionText(ServerBrowserDiscovery.CurrentProtocolVersion));

        return Ui.Confirm(FindForm()!,
            T("{0}\r\n\r\nYine de bağlanılsın mı?", note));
    }

    private DiscoveredServer? Selected =>
        _list.SelectedItems.Count > 0 ? _list.SelectedItems[0].Tag as DiscoveredServer : null;

    private void AddOrUpdateRow(DiscoveredServer server)
    {
        var key = server.Host + ":" + server.GamePort;

        if (!_rows.TryGetValue(key, out var item))
        {
            item = new ListViewItem(new[]
            {
                server.Name,
                server.Address,
                $"{server.PlayerCount}/{server.MaxPlayers}",
                FormatVersion(server),
                server.Gamemode,
                server.PasswordProtected ? T("var") : "-",
                server.PingMs > 0 ? $"{server.PingMs} ms" : "-",
                server.Source
            });
            item.Tag = server;
            item.ForeColor = server.IsCurrent ? Ui.OkColor : (server.ProtocolVersion == 0 ? Ui.WarnColor : Ui.ErrColor);
            _rows[key] = item;
            _list.Items.Add(item);
        }
        else
        {
            item.SubItems[0].Text = server.Name;
            item.SubItems[1].Text = server.Address;
            item.SubItems[2].Text = $"{server.PlayerCount}/{server.MaxPlayers}";
            item.SubItems[3].Text = FormatVersion(server);
            item.SubItems[4].Text = server.Gamemode;
            item.SubItems[5].Text = server.PasswordProtected ? T("var") : "-";
            item.SubItems[6].Text = server.PingMs > 0 ? $"{server.PingMs} ms" : "-";
            item.SubItems[7].Text = server.Source;
            item.Tag = server;
            item.ForeColor = server.IsCurrent ? Ui.OkColor : (server.ProtocolVersion == 0 ? Ui.WarnColor : Ui.ErrColor);
        }

        if (ReferenceEquals(Selected, server)) UpdateDetails();
    }

    private static string FormatVersion(DiscoveredServer server) =>
        ServerBrowserDiscovery.VersionShortState(server.ProtocolVersion);

    private void SortRows()
    {
        if (_list.Items.Count < 2) return;
        var sorted = _list.Items.Cast<ListViewItem>()
            .OrderByDescending(i => (i.Tag as DiscoveredServer)?.PlayerCount ?? 0)
            .ThenBy(i => (i.Tag as DiscoveredServer)?.Name ?? "", StringComparer.CurrentCultureIgnoreCase)
            .ToArray();

        _list.BeginUpdate();
        try
        {
            _list.Items.Clear();
            _list.Items.AddRange(sorted);
        }
        finally
        {
            _list.EndUpdate();
        }
    }

    private void UpdateDetails()
    {
        var server = Selected;
        if (server == null)
        {
            _details.Text = T("Bir sunucu seçin.");
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine(T("Sunucu   : {0}", server.Name));
        sb.AppendLine(T("Adres    : {0}", server.Address));
        sb.AppendLine(T("Oyuncular: {0}/{1}", server.PlayerCount, server.MaxPlayers));
        sb.AppendLine(T("Sürüm    : {0} ({1})", ServerBrowserDiscovery.VersionText(server.ProtocolVersion),
            ServerBrowserDiscovery.VersionState(server.ProtocolVersion)));
        sb.AppendLine(T("Mod      : {0}", server.Gamemode));
        sb.AppendLine(T("Şifre    : {0}", server.PasswordProtected ? T("var") : T("yok")));
        sb.AppendLine(T("Ping     : {0}", server.PingMs > 0 ? $"{server.PingMs} ms" : T("bilinmiyor")));
        sb.AppendLine(T("Kaynak   : {0}", server.Source));
        sb.AppendLine();
        if (server.ProtocolVersion == 0)
            sb.AppendLine(T("Not: Sunucu protokol sürümü bildirmiyor; eski bir GTACoOp sunucusu olabilir."));
        sb.AppendLine(T("Bağlanınca GTACoop Remake mod senkronizasyonu otomatik denenir; " +
                        "sync sunucusu yoksa oyun modsuz başlatılır."));
        _details.Text = sb.ToString();
    }

    private void SetScanUi(bool scanning)
    {
        _refreshBtn.Enabled = !scanning && !_connecting;
        _lanBtn.Enabled = !scanning && !_connecting;
        _cancelBtn.Enabled = scanning;
        SetActionButtons(!scanning && !_connecting);
        _directBtn.Enabled = !scanning && !_connecting;
    }

    private void SetActionButtons(bool enabled)
    {
        _connectBtn.Enabled = enabled;
        _gameOnlyBtn.Enabled = enabled;
        _addBtn.Enabled = enabled;
        if (!_scanning) _directBtn.Enabled = enabled;
    }

    private void SetProgress(int value)
    {
        _progress.Value = Math.Clamp(value, _progress.Minimum, _progress.Maximum);
    }

    private void SetStatus(string text, Color color)
    {
        _status.Text = text;
        _status.ForeColor = color;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            try { _scanCts?.Cancel(); } catch { }
            _scanCts?.Dispose();
            _scanCts = null;
        }
        base.Dispose(disposing);
    }
}
