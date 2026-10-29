using System.Diagnostics;
using System.Net.Sockets;
using System.Windows.Forms;
using GTACoop.Sync;

namespace GTACoop.Remake;

public sealed class ServersTab : UserControl
{
    private readonly ListView _list = new();
    private readonly ProgressBar _progress = new();
    private readonly Label _status = new();
    private readonly TextBox _details = new();
    private readonly Button _connectBtn;
    private readonly Button _cancelBtn;
    private readonly CheckBox _sessionMode = new();
    private readonly CheckBox _lowDesync = new();
    private readonly Label _sessionInfo = new();
    private readonly Button _endSessionBtn;
    private readonly System.Windows.Forms.Timer _sessionTimer = new();
    private readonly System.Windows.Forms.Timer _statusTimer = new();
    private readonly Dictionary<string, ServerStatus> _statusCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTime> _codeRefreshAt = new(StringComparer.OrdinalIgnoreCase);
    private bool _statusChecking;
    private CancellationTokenSource? _cts;
    private CancellationTokenSource? _fallbackWatcherCts;
    private RelaySession? _activeRelay;
    private DateTime? _relayIdleSince;
    private bool _relayGameSeen;
    private bool _busy;
    private bool _browserConnectBusy;

    public ServersTab()
    {
        Dock = DockStyle.Fill;

        var info = new Label
        {
            Dock = DockStyle.Top,
            AutoSize = false,
            Height = 72,
            Text = T("Sunucu listesine sunucu ekleyin (arkadaşınızın IP'si veya kendi sunucunuz), seçip 'Bağlan ve Oyna'ya basın.\r\nSunucunun tüm modları (araç/ped paketleri, scriptler, ASI eklentileri) otomatik indirilir ve yalnızca siz oynarken kurulu kalır; oyundan çıkınca kaldırılır."),
            ForeColor = Color.DimGray,
            Padding = new Padding(4, 4, 4, 4)
        };

        Ui.StyleList(_list,
            (T("Sunucu"), 220), (T("Adres"), 190), (T("Oyun Portu"), 85), ("Sync", 70),
            (T("Şifre"), 65), (T("Durum"), 180));
        _list.Height = 190;
        _list.Dock = DockStyle.Top;
        _list.DoubleClick += (_, _) => EditServer();

        var listButtons = Ui.Flow();
        listButtons.Controls.Add(Ui.Btn(T("Kod ile Katıl..."), (_, _) => JoinWithCode()));
        listButtons.Controls.Add(Ui.Btn(T("Sunucu Ekle..."), (_, _) => AddServer()));
        listButtons.Controls.Add(Ui.Btn(T("Düzenle..."), (_, _) => EditServer()));
        listButtons.Controls.Add(Ui.Btn(T("Sil"), (_, _) => DeleteServer()));
        listButtons.Controls.Add(Ui.Btn(T("Yukarı"), (_, _) => MoveServer(-1)));
        listButtons.Controls.Add(Ui.Btn(T("Aşağı"), (_, _) => MoveServer(1)));
        listButtons.Controls.Add(Ui.Btn(T("kendi sunucum (127.0.0.1) ekle"), (_, _) => AddLocalServer()));

        // Bağlantı grubu
        var connectGroup = new GroupBox
        {
            Text = T("Bağlantı"),
            Dock = DockStyle.Top,
            Height = 315,
            Padding = new Padding(10)
        };

        var actions = Ui.Flow();
        _connectBtn = Ui.Btn(T("Bağlan ve Oyna"), (_, _) => _ = ConnectAsync(launch: true));
        _connectBtn.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
        actions.Controls.Add(_connectBtn);
        actions.Controls.Add(Ui.Btn(T("Sunucu Modlarını İndir (ön yükleme)"), (_, _) => _ = PrefetchAsync()));
        actions.Controls.Add(Ui.Btn(T("Yeniden İndir..."), (_, _) => _ = PrefetchAsync(force: true)));
        actions.Controls.Add(Ui.Btn(T("Sadece Oyunu Başlat"), (_, _) => LaunchOnly()));
        _cancelBtn = Ui.Btn(T("İptal"), (_, _) => _cts?.Cancel());
        _cancelBtn.Enabled = false;
        actions.Controls.Add(_cancelBtn);

        _sessionMode.Text = T("Oyun kapanınca sunucu modları otomatik kaldırılsın (oturum modu)");
        _sessionMode.AutoSize = true;
        _sessionMode.Checked = AppEnv.Settings.RemoveModsOnExit;
        _sessionMode.Margin = new Padding(4, 8, 12, 4);
        _sessionMode.CheckedChanged += (_, _) =>
        {
            AppEnv.Settings.RemoveModsOnExit = _sessionMode.Checked;
            AppEnv.SaveSettings();
            AppEnv.Log(_sessionMode.Checked
                ? "Oturum modu açıldı: oyun kapanınca sunucu modları kaldırılacak."
                : "Oturum modu kapatıldı: sunucu modları kurulu kalacak.");
        };

        _lowDesync.Text = T("Düşük gecikme profili (trafik/ped azalt, 4 NPC)");
        _lowDesync.AutoSize = true;
        _lowDesync.Checked = AppEnv.Settings.LowDesyncProfile;
        _lowDesync.Margin = new Padding(12, 8, 4, 4);
        _lowDesync.CheckedChanged += (_, _) =>
        {
            AppEnv.Settings.LowDesyncProfile = _lowDesync.Checked;
            AppEnv.SaveSettings();
            AppEnv.Log(_lowDesync.Checked
                ? "Düşük gecikme profili açıldı: bağlantıda trafik/ped kapatılacak, NPC sayısı 4 olacak."
                : "Düşük gecikme profili kapatıldı: istemci ayarları varsayılana dönecek.");

            var gameFolder = AppEnv.Settings.GameFolder;
            if (AddonApplier.IsGameRunning())
            {
                AppEnv.Log("Oyun açıkken profil yazılmadı; bir sonraki bağlantıda uygulanacak.");
            }
            else if (!string.IsNullOrWhiteSpace(gameFolder) && GameFolder.IsValid(gameFolder))
            {
                ClientSettingsEditor.SetLowDesyncProfile(gameFolder, _lowDesync.Checked, AppEnv.Log);
            }
        };

        _endSessionBtn = Ui.Btn(T("Oturumu Bitir (modları kaldır)"), (_, _) => _ = EndSessionAsync());
        _endSessionBtn.Enabled = false;

        _sessionInfo.AutoSize = true;
        _sessionInfo.Margin = new Padding(4, 9, 4, 4);

        var sessionRow = Ui.Flow();
        sessionRow.Controls.Add(_sessionMode);
        sessionRow.Controls.Add(_lowDesync);
        sessionRow.Controls.Add(_endSessionBtn);
        sessionRow.Controls.Add(_sessionInfo);

        _progress.Dock = DockStyle.Top;
        _progress.Height = 18;
        _progress.Style = ProgressBarStyle.Continuous;

        _status.Dock = DockStyle.Top;
        _status.AutoSize = false;
        _status.Height = 22;
        _status.ForeColor = Color.DimGray;

        _details.Dock = DockStyle.Fill;
        _details.Multiline = true;
        _details.ReadOnly = true;
        _details.ScrollBars = ScrollBars.Vertical;
        _details.Font = new Font("Consolas", 9f);
        _details.BackColor = Color.WhiteSmoke;

        connectGroup.Controls.Add(_details);
        connectGroup.Controls.Add(_status);
        connectGroup.Controls.Add(sessionRow);
        connectGroup.Controls.Add(_progress);
        connectGroup.Controls.Add(actions);

        Controls.Add(connectGroup);
        Controls.Add(listButtons);
        Controls.Add(_list);
        Controls.Add(info);

        _sessionTimer.Interval = 3000;
        _sessionTimer.Tick += (_, _) => RefreshSessionUi();
        _sessionTimer.Start();

        // Minecraft benzeri canlı durum: sekme görünürken 5 saniyede bir
        // sync portuna kısa TCP bağlantısı dener, yeşil/kırmızı gösterir.
        _statusTimer.Interval = 5000;
        _statusTimer.Tick += (_, _) =>
        {
            if (Visible && !_busy) _ = CheckStatusesAsync();
        };
        _statusTimer.Start();

        VisibleChanged += (_, _) =>
        {
            if (Visible && !_busy) _ = CheckStatusesAsync();
        };

        RefreshList();
        RefreshSessionUi();
    }

    public bool IsBusy => _busy || _browserConnectBusy;

    /// <summary>
    /// Sunucu Tarayıcı sekmesinden bağlanır. Sunucuda GTACoop Remake mod
    /// senkronizasyonu varsa modları kurar; yoksa oyunu modsuz başlatır.
    /// </summary>
    public async Task ConnectFromBrowserAsync(ServerEntry entry, bool launch = true)
    {
        if (IsBusy) return;
        _browserConnectBusy = true;
        try
        {
            // Tarayıcıdan bağlanırken sunucuyu "Sunucular" listesine ekleme:
            // doğrudan keşfedilen adresle çalış. Kullanıcı isterse ayrıca
            // "Sunuculara Ekle" düğmesini kullanabilir.
            SetStatus(T("Sunucu Tarayıcı: mod senkronizasyonu kontrol ediliyor..."), Ui.WarnColor);
            var syncPort = await FindSyncPortAsync(entry, CancellationToken.None);
            if (syncPort > 0)
            {
                entry.SyncPort = syncPort;
                AppEnv.Log($"Sunucu Tarayıcı: mod senkronizasyonu bulundu (port {syncPort}).");
                await ConnectAsync(launch, entry);
            }
            else
            {
                AppEnv.Log("Sunucu Tarayıcı: mod senkronizasyonu yok; modsuz bağlantıya geçiliyor.");
                await ConnectGameOnlyAsync(entry, launch);
            }
        }
        catch (Exception ex)
        {
            SetStatus(T("Hata: ") + ex.Message, Ui.ErrColor);
            AppEnv.LogError("Sunucu Tarayıcı bağlantısı: " + ex.Message);
            Ui.Error(FindForm()!, ex.Message);
        }
        finally
        {
            _browserConnectBusy = false;
            RefreshSessionUi();
        }
    }

    /// <summary>Sunucu Tarayıcı'dan mod senkronizasyonunu atlayarak bağlanır.</summary>
    public async Task ConnectGameOnlyFromBrowserAsync(ServerEntry entry, bool launch = true)
    {
        if (IsBusy) return;
        _browserConnectBusy = true;
        try
        {
            // Sunucuyu listeye eklemeden doğrudan modsuz bağlan.
            await ConnectGameOnlyAsync(entry, launch);
        }
        catch (Exception ex)
        {
            SetStatus(T("Hata: ") + ex.Message, Ui.ErrColor);
            AppEnv.LogError("Sunucu Tarayıcı (modsuz) bağlantısı: " + ex.Message);
            Ui.Error(FindForm()!, ex.Message);
        }
        finally
        {
            _browserConnectBusy = false;
            RefreshSessionUi();
        }
    }

    /// <summary>Sunucu Tarayıcı'dan sunucuyu yalnızca listeye ekler.</summary>
    public void AddServerFromBrowser(ServerEntry entry)
    {
        var saved = AddOrUpdateServer(entry);
        RefreshList();
        SelectEntry(saved);
        AppEnv.Log($"Sunucu Tarayıcı: listeye eklendi: {saved.Name} ({saved.Host}:{saved.GamePort})");
    }

    private ServerEntry AddOrUpdateServer(ServerEntry entry)
    {
        var existing = AppEnv.Settings.Servers.FirstOrDefault(s =>
            string.Equals(s.Host, entry.Host, StringComparison.OrdinalIgnoreCase) &&
            s.GamePort == entry.GamePort);

        if (existing != null)
        {
            existing.Name = string.IsNullOrWhiteSpace(entry.Name) ? existing.Name : entry.Name;
            if (!string.IsNullOrWhiteSpace(entry.Password)) existing.Password = entry.Password;
            if (entry.SyncPort is > 0 and <= 65535) existing.SyncPort = entry.SyncPort;
            if (!string.IsNullOrWhiteSpace(entry.SyncToken)) existing.SyncToken = entry.SyncToken;
            if (!string.IsNullOrWhiteSpace(entry.ShortCode)) existing.ShortCode = entry.ShortCode;
            if (entry.UsesRelay)
            {
                existing.RelayHost = entry.RelayHost;
                existing.RelaySyncPort = entry.RelaySyncPort;
                existing.RelayGamePort = entry.RelayGamePort;
                if (LocalAddresses.IsDirectReachable(entry.Host) ||
                    !LocalAddresses.IsDirectReachable(existing.Host))
                    existing.Host = entry.Host;
            }
            if (entry.AutoConnect) existing.AutoConnect = true;
            AppEnv.SaveSettings();
            return existing;
        }

        AppEnv.Settings.Servers.Add(entry);
        AppEnv.SaveSettings();
        return entry;
    }

    private void SelectEntry(ServerEntry entry)
    {
        foreach (ListViewItem item in _list.Items)
        {
            if (!ReferenceEquals(item.Tag, entry)) continue;
            item.Selected = true;
            item.Focused = true;
            item.EnsureVisible();
            _list.Select();
            break;
        }
    }

    /// <summary>
    /// GTACoop Remake sunucularında sync portu genellikle oyun portu + 100'dür.
    /// Bilinen portlar sırayla ve kısa zaman aşımıyla denenir.
    /// </summary>
    private static async Task<int> FindSyncPortAsync(ServerEntry entry, CancellationToken ct)
    {
        var candidates = new[] { entry.SyncPort, 4599 }
            .Where(p => p is > 0 and <= 65535)
            .Distinct()
            .ToList();

        foreach (var port in candidates)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromMilliseconds(1500));
            try
            {
                using var client = new SyncClient(entry.Host, port, entry.SyncToken);
                await client.PingAsync(cts.Token);
                return port;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                // Bu portta sync sunucusu yok; sıradaki adayı dene.
            }
        }
        return 0;
    }

    // ------------------------------------------------------------------ liste

    public void RefreshList()
    {
        _list.BeginUpdate();
        _list.Items.Clear();
        foreach (var s in AppEnv.Settings.Servers)
        {
            var key = StatusKey(s);
            var status = _statusCache.TryGetValue(key, out var cached) ? cached : default;
            var item = new ListViewItem(new[]
            {
                s.Name,
                s.UsesRelay ? T("{0} (relay)", s.Host) : s.Host,
                s.UsesRelay ? s.RelayGamePort.ToString() : s.GamePort.ToString(),
                s.UsesRelay ? s.RelaySyncPort.ToString() : s.SyncPort.ToString(),
                string.IsNullOrEmpty(s.Password) ? "-" : T("var"),
                status.Known
                    ? (status.Online
                        ? "● " + T("Çevrimiçi") + $" ({status.Ms} ms)"
                        : "● " + T("Çevrimdışı"))
                    : "◌ " + T("Kontrol ediliyor...")
            });
            item.Tag = s;
            item.UseItemStyleForSubItems = false;
            item.SubItems[5].ForeColor = !status.Known
                ? Color.DimGray
                : status.Online ? Ui.OkColor : Ui.ErrColor;
            _list.Items.Add(item);
        }
        _list.EndUpdate();

        if (Visible && !_busy) _ = CheckStatusesAsync();
    }

    // ------------------------------------------------------------------ canlı durum

    private readonly record struct ServerStatus(bool Known, bool Online, int Ms);

    private static string StatusKey(ServerEntry s) =>
        $"{s.Host}|{s.SyncPort}|{s.RelayHost}|{s.RelaySyncPort}";

    /// <summary>
    /// Her sunucunun sync portuna kısa bir TCP bağlantısı dener. Relay varsa
    /// önce doğrudan adres, sonra relay uç noktası denenir. Hiçbiri yoksa
    /// (ör. yalnızca özel ağ adresi taşıyan eski kısa kod kaydı) durum
    /// "bilinmiyor" kalır; kısa kod seyrek aralıkla tazelenir.
    /// </summary>
    private async Task CheckStatusesAsync()
    {
        if (_statusChecking) return;

        var servers = AppEnv.Settings.Servers.ToList();
        if (servers.Count == 0) return;

        _statusChecking = true;
        try
        {
            var now = DateTime.UtcNow;
            foreach (var s in servers)
            {
                if (string.IsNullOrWhiteSpace(s.ShortCode)) continue;

                var key = StatusKey(s);
                if (_statusCache.TryGetValue(key, out var cached) && cached.Online) continue;
                if (_codeRefreshAt.TryGetValue(key, out var last) &&
                    now - last < TimeSpan.FromSeconds(30)) continue;

                _codeRefreshAt[key] = now;
                await RefreshRelayInfoAsync(s, CancellationToken.None, quiet: true);
                break; // bir turda en fazla bir kısa kod sorgusu
            }

            var results = await Task.WhenAll(servers.Select(ProbeStatusAsync));

            for (int i = 0; i < servers.Count; i++)
            {
                var server = servers[i];
                var result = results[i];
                var key = StatusKey(server);

                if (_statusCache.TryGetValue(key, out var old) &&
                    old.Known && result.Known && old.Online != result.Online)
                {
                    AppEnv.Log(result.Online
                        ? $"Sunucu çevrimiçi: {server.Name} ({result.Ms} ms)"
                        : $"Sunucu çevrimdışı: {server.Name}");
                }

                _statusCache[key] = result;
                UpdateItemStatus(server, result);
            }
        }
        catch (Exception ex)
        {
            AppEnv.Log("Sunucu durumu kontrol edilemedi: " + ex.Message);
        }
        finally
        {
            _statusChecking = false;
        }
    }

    private static async Task<ServerStatus> ProbeStatusAsync(ServerEntry s)
    {
        var targets = new List<(string Host, int Port)>();

        if (s.SyncPort is > 0 and <= 65535)
        {
            if (LocalAddresses.IsLocalAddress(s.Host))
                targets.Add(("127.0.0.1", s.SyncPort));
            else if (LocalAddresses.IsDirectReachable(s.Host))
                targets.Add((s.Host, s.SyncPort));
        }

        if (s.UsesRelay && s.RelaySyncPort is > 0 and <= 65535)
            targets.Add((s.RelayHost, s.RelaySyncPort));

        if (targets.Count == 0) return default;

        foreach (var (host, port) in targets)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(1400));
                using var client = new TcpClient();
                await client.ConnectAsync(host, port, cts.Token);
                sw.Stop();
                return new ServerStatus(true, true,
                    (int)Math.Min(sw.ElapsedMilliseconds, 9999));
            }
            catch
            {
                // Bu adres kapalı; sıradaki uç noktayı dene.
            }
        }

        return new ServerStatus(true, false, 0);
    }

    private void UpdateItemStatus(ServerEntry server, ServerStatus status)
    {
        foreach (ListViewItem item in _list.Items)
        {
            if (!ReferenceEquals(item.Tag, server)) continue;

            var cell = item.SubItems[5];
            if (!status.Known)
            {
                cell.Text = "◌ " + T("Bilinmiyor");
                cell.ForeColor = Color.DimGray;
            }
            else if (status.Online)
            {
                cell.Text = "● " + T("Çevrimiçi") + $" ({status.Ms} ms)";
                cell.ForeColor = Ui.OkColor;
            }
            else
            {
                cell.Text = "● " + T("Çevrimdışı");
                cell.ForeColor = Ui.ErrColor;
            }
            break;
        }
    }

    private ServerEntry? Selected =>
        _list.SelectedItems.Count > 0 ? (ServerEntry)_list.SelectedItems[0].Tag! : null;

    private void AddServer()
    {
        using var dlg = new ServerEditDialog();
        if (dlg.ShowDialog(FindForm()) != DialogResult.OK) return;
        AppEnv.Settings.Servers.Add(dlg.Entry);
        AppEnv.SaveSettings();
        RefreshList();
        AppEnv.Log($"Sunucu eklendi: {dlg.Entry.Name} ({dlg.Entry.Host})");
    }

    /// <summary>Arkadaşın gönderdiği katılım kodunu çözer, sunucuyu ekler ve istenirse bağlanır.</summary>
    private void JoinWithCode()
    {
        using var dlg = new JoinCodeDialog();
        if (dlg.ShowDialog(FindForm()) != DialogResult.OK || dlg.Entry == null) return;

        var entry = dlg.Entry;
        var shortCode = NormalizeShortCode(entry.ShortCode);
        if (shortCode.Length > 0) entry.ShortCode = shortCode;

        // Aynı sunucu farklı bir adresle (ör. önce 192.168.137.1, sonra genel
        // IP/relay) çözülebilir. Önce kısa koda, sonra adrese, en son aynı
        // ad + portlara bakarak kopya kayıt oluşmasını engelle.
        var existing = shortCode.Length > 0
            ? AppEnv.Settings.Servers.FirstOrDefault(s =>
                string.Equals(NormalizeShortCode(s.ShortCode), shortCode, StringComparison.Ordinal))
            : null;
        existing ??= AppEnv.Settings.Servers.FirstOrDefault(s =>
            string.Equals(s.Host, entry.Host, StringComparison.OrdinalIgnoreCase) &&
            s.GamePort == entry.GamePort && s.SyncPort == entry.SyncPort);
        if (existing == null && !string.IsNullOrWhiteSpace(entry.Name))
        {
            existing = AppEnv.Settings.Servers.FirstOrDefault(s =>
                string.Equals(s.Name, entry.Name, StringComparison.OrdinalIgnoreCase) &&
                s.GamePort == entry.GamePort && s.SyncPort == entry.SyncPort);
        }

        if (existing != null)
        {
            existing.Name = entry.Name;
            existing.Password = entry.Password;
            existing.SyncToken = entry.SyncToken;
            if (shortCode.Length > 0) existing.ShortCode = shortCode;
            if (entry.SyncPort is > 0 and <= 65535) existing.SyncPort = entry.SyncPort;
            if (entry.GamePort is > 0 and <= 65535) existing.GamePort = entry.GamePort;
            if (LocalAddresses.IsDirectReachable(entry.Host) ||
                !LocalAddresses.IsDirectReachable(existing.Host))
                existing.Host = entry.Host;
            if (entry.UsesRelay)
            {
                existing.RelayHost = entry.RelayHost;
                existing.RelaySyncPort = entry.RelaySyncPort;
                existing.RelayGamePort = entry.RelayGamePort;
            }
            else if (shortCode.Length == 0)
            {
                // Kısa kodu olmayan eski uzun kodlarda relay uç noktaları
                // güncellenemez. Önceki oturumdan kalan ölü relay portlarını
                // denemek yerine doğrudan bağlantıya düş.
                existing.RelayHost = "";
                existing.RelaySyncPort = 0;
                existing.RelayGamePort = 0;
            }
            entry = existing;
            AppEnv.Log($"Katılım kodu mevcut kaydı güncelledi: {entry.Name} ({entry.Host})" +
                       (entry.UsesRelay ? $" [relay {entry.RelayHost}]" : ""));
        }
        else
        {
            AppEnv.Settings.Servers.Add(entry);
            AppEnv.Log($"Katılım kodu ile sunucu eklendi: {entry.Name} " +
                       $"({entry.Host}:{entry.GamePort}, sync {entry.SyncPort})" +
                       (entry.UsesRelay ? $" [relay {entry.RelayHost}]" : ""));
        }

        AppEnv.SaveSettings();
        RefreshList();
        foreach (ListViewItem item in _list.Items)
        {
            if (ReferenceEquals(item.Tag, entry))
            {
                item.Selected = true;
                item.Focused = true;
                item.EnsureVisible();
                _list.Select();
                break;
            }
        }

        if (dlg.ConnectRequested)
        {
            AppEnv.Log($"Katılım kodu bağlantısı başlatılıyor: {entry.Name} " +
                       $"({entry.Host}:{entry.GamePort}, sync {entry.SyncPort})" +
                       (entry.UsesRelay ? $" [relay {entry.RelayHost}]" : "") +
                       (string.IsNullOrWhiteSpace(entry.ShortCode) ? "" : $" [kısa kod {entry.ShortCode}]"));
            // Seçili öğeye güvenmek yerine çözülen kaydı doğrudan kullan.
            _ = ConnectAsync(launch: true, entry);
        }
    }

    private void EditServer()
    {
        var sel = Selected;
        if (sel == null) { Ui.Warn(FindForm()!, T("Önce bir sunucu seçin.")); return; }

        using var dlg = new ServerEditDialog(sel);
        if (dlg.ShowDialog(FindForm()) != DialogResult.OK) return;
        AppEnv.SaveSettings();
        RefreshList();
    }

    private void DeleteServer()
    {
        var sel = Selected;
        if (sel == null) { Ui.Warn(FindForm()!, T("Önce bir sunucu seçin.")); return; }
        if (!Ui.Confirm(FindForm()!,
            T("'{0}' listeden silinsin mi?\r\n(Kurulu modlar silinmez; 'Araçlar' sekmesinden temizleyebilirsiniz.)", sel.Name))) return;

        AppEnv.Settings.Servers.Remove(sel);
        AppEnv.SaveSettings();
        RefreshList();
    }

    private void MoveServer(int delta)
    {
        var sel = Selected;
        if (sel == null) return;
        var list = AppEnv.Settings.Servers;
        int i = list.IndexOf(sel);
        int j = i + delta;
        if (i < 0 || j < 0 || j >= list.Count) return;
        (list[i], list[j]) = (list[j], list[i]);
        AppEnv.SaveSettings();
        RefreshList();
        foreach (ListViewItem item in _list.Items)
            if (ReferenceEquals(item.Tag, sel)) { item.Selected = true; break; }
    }

    private void AddLocalServer()
    {
        var entry = new ServerEntry
        {
            Name = T("Kendi Sunucum"),
            Host = "127.0.0.1",
            GamePort = 4499,
            SyncPort = 4599
        };
        AppEnv.Settings.Servers.Add(entry);
        AppEnv.SaveSettings();
        RefreshList();
        AppEnv.Log("Yerel sunucu eklendi (127.0.0.1). 'Sunucum' sekmesinden başlatabilirsiniz.");
    }

    // ------------------------------------------------------------------ bağlantı

    private async Task ConnectAsync(bool launch, ServerEntry? entry = null)
    {
        if (_busy)
        {
            AppEnv.Log("Bağlantı isteği yok sayıldı: başka bir işlem sürüyor.");
            Ui.Warn(FindForm()!, T("Zaten bir bağlantı işlemi sürüyor. Bitmesini bekleyin veya 'İptal' düğmesine basın."));
            return;
        }
        var sel = entry ?? Selected;
        if (sel == null) { Ui.Warn(FindForm()!, T("Önce listeden bir sunucu seçin.")); return; }
        if (!MainForm.RequireGameFolder(FindForm()!, out var gameFolder)) return;

        if (AddonApplier.IsGameRunning())
        {
            Ui.Warn(FindForm()!, T("GTA V açık. Modları değiştirmeden önce oyunu kapatın."));
            return;
        }

        try
        {
            await CleanupLeftoverSessionAsync(gameFolder);
        }
        catch (Exception ex)
        {
            SetStatus(T("Hata: ") + ex.Message, Ui.ErrColor);
            AppEnv.LogError("Oturum kalıntıları temizlenemedi: " + ex.Message);
            Ui.Error(FindForm()!, ex.Message);
            return;
        }

        _busy = true;
        _connectBtn.Enabled = false;
        _endSessionBtn.Enabled = false;
        _cancelBtn.Enabled = true;
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;
        var keepRelay = false;
        SetStatus(T("Sunucuya bağlanılıyor..."), Ui.WarnColor);
        SetProgress(0);

        try
        {
            var applier = new AddonApplier(gameFolder, AppEnv.Log);
            var progress = new Progress<SyncProgress>(p =>
            {
                SetStatus(p.Message, Color.DimGray);
                SetProgress((int)(p.Fraction * 100));
            });

            DisposeActiveRelay();

            var syncHost = sel.Host;
            var syncPort = sel.SyncPort;
            var gameHost = sel.Host;
            var gamePort = sel.GamePort;
            var syncViaRelay = false;
            var gameViaRelay = false;

            AppEnv.Log($"Bağlantı hazırlanıyor: {sel.Name} ({sel.Host}:{sel.GamePort}, sync {sel.SyncPort})");

            // Kısa kodla eklenen kayıtlarda relay portları sunucu her
            // başlatıldığında değişir. Bağlantıdan önce güncel uç noktaları
            // koddan yenile; böylece eski portlar yüzünden başarısız olunmaz.
            if (!string.IsNullOrWhiteSpace(sel.ShortCode))
            {
                SetStatus(T("Sunucunun güncel relay bilgisi alınıyor..."), Ui.WarnColor);
                await RefreshRelayInfoAsync(sel, ct);
                AppEnv.Log($"Kısa kod {sel.ShortCode}: " +
                           (sel.UsesRelay
                               ? $"güncel relay {sel.RelayHost} sync {sel.RelaySyncPort}, oyun {sel.RelayGamePort}"
                               : "relay yok; doğrudan bağlantı denenecek"));
            }

            // Aynı bilgisayar veya aynı yerel ağ: relay gereksiz gecikme ve
            // yük getirir. Doğrudan sync + doğrudan oyun kullan.
            var localDirect = LocalAddresses.IsLocalAddress(sel.Host) ||
                              LocalAddresses.IsSameSubnet(sel.Host);

            if (localDirect)
            {
                if (LocalAddresses.IsLocalAddress(sel.Host))
                {
                    syncHost = "127.0.0.1";
                    gameHost = "127.0.0.1";
                }
                AppEnv.Log($"Yerel/aynı ağ sunucusu: relay atlanıyor, doğrudan bağlantı ({sel.Host}).");
            }
            else if (sel.UsesRelay || !string.IsNullOrWhiteSpace(sel.ShortCode))
            {
                SetStatus(T("Otomatik relay bağlantısı kuruluyor..."), Ui.WarnColor);
                var relay = await TryRelayWithRefreshAsync(sel, ct);

                if (relay != null)
                {
                    _activeRelay = relay;
                    gameHost = "127.0.0.1";
                    gamePort = relay.GamePort;
                    gameViaRelay = true;

                    // Sync için doğrudan TCP erişimi varsa (port yönlendirme,
                    // UPnP, Radmin/Tailscale, genel IP) büyük dosyaları relay
                    // üzerinden taşımaya gerek yok. Oyun UDP'si yine relay'de
                    // kalabilir; iki taşıma birbirinden bağımsızdır.
                    var directSync = CanTryDirect(sel) && await ProbeDirectSyncAsync(sel, ct);
                    if (directSync)
                    {
                        syncHost = sel.Host;
                        syncPort = sel.SyncPort;
                        AppEnv.Log($"Sync doğrudan: {sel.Host}:{sel.SyncPort}; " +
                                   $"oyun relay: {sel.RelayHost}:{sel.RelayGamePort} -> yerel {relay.GamePort}");
                    }
                    else
                    {
                        syncHost = "127.0.0.1";
                        syncPort = relay.SyncPort;
                        syncViaRelay = true;
                        AppEnv.Log($"Otomatik relay bağlantısı kuruldu: {sel.RelayHost} " +
                                   $"(sync {sel.RelaySyncPort} -> yerel {relay.SyncPort}, " +
                                   $"oyun {sel.RelayGamePort} -> yerel {relay.GamePort})");
                    }
                }
                else if (CanTryDirect(sel))
                {
                    SetStatus(T("Otomatik relay kurulamadı; doğrudan adres deneniyor..."), Ui.WarnColor);
                    if (!await ProbeDirectSyncAsync(sel, ct))
                        throw new SyncException(T(
                            "Sunucuya ne otomatik relay ne de doğrudan bağlantı kurulamadı. " +
                            "Sunucu ve launcher açık mı? İnternet bağlantınızı kontrol edip yeniden deneyin."));

                    AppEnv.Log($"Otomatik relay kurulamadı; doğrudan adres kullanılacak: " +
                               $"{sel.Host}:{sel.SyncPort}");
                    if (LocalAddresses.IsLocalAddress(sel.Host))
                    {
                        syncHost = "127.0.0.1";
                        gameHost = "127.0.0.1";
                    }
                }
                else
                {
                    throw new SyncException(T(
                        "Otomatik relay bağlantısı kurulamadı. Sunucu ve launcher açık mı? İnternet bağlantınızı kontrol edip yeniden deneyin."));
                }
            }
            else
            {
                // Sunucu bu bilgisayarda çalışıyorsa yerel/ICS adresi yerine
                // loopback kullan; aksi halde kapalı bir adaptör yüzünden
                // bağlantı zaman aşımına uğrayabilir.
                if (LocalAddresses.IsLocalAddress(sel.Host))
                {
                    syncHost = "127.0.0.1";
                    gameHost = "127.0.0.1";
                    AppEnv.Log($"Yerel sunucu adresi loopback'e çevrildi: {sel.Host} -> 127.0.0.1");
                }
                else if (!CanTryDirect(sel))
                {
                    throw new SyncException(T(
                        "Bu sunucuya doğrudan ulaşılamaz: adres özel bir ağda (ör. hotspot/ICS) ve otomatik relay bilgisi yok.\r\n" +
                        "Sunucu sahibinden güncel kısa kodu veya uzun GTC1 kodunu isteyip 'Kod ile Katıl...' ile yeniden ekleyin."));
                }
            }

            var manifest = await Task.Run<Manifest?>(async () =>
            {
                using var client = new SyncClient(syncHost, syncPort, sel.SyncToken, AppEnv.Log);

                AppEnv.Log(syncViaRelay
                    ? $"Sunucuya bağlanılıyor (sync relay): {sel.RelayHost}:{sel.RelaySyncPort}"
                    : $"Sunucuya bağlanılıyor (sync doğrudan): {syncHost}:{syncPort}");

                // İlk erişim denemesi sınırlı sürede tamamlanmalı; aksi halde
                // ulaşılamayan bir doğrudan adres uzun süre asılı kalabilir.
                using (var pingCts = CancellationTokenSource.CreateLinkedTokenSource(ct))
                {
                    pingCts.CancelAfter(TimeSpan.FromSeconds(15));
                    var serverName = await client.PingAsync(pingCts.Token);
                    AppEnv.Log($"Sunucu adı: {serverName}");
                }

                var mf = await client.GetManifestAsync(ct);
                var state = SyncStateStore.Load(AppEnv.DataFolder, mf.ServerId);
                var plan = applier.BuildPlan(mf, state);

                ShowDetails(sel, mf, plan, state, syncViaRelay, gameViaRelay);

                // Onay (indirilecek veya güvenli kurulamayacak bir şey varsa)
                if (plan.ToInstall.Count > 0 || plan.Blocked.Count > 0)
                {
                    var mb = plan.ToInstall.Sum(a => a.TotalSize) / 1024.0 / 1024.0;
                    var note = AppEnv.Settings.RemoveModsOnExit
                        ? T("Modlar oyun kapanınca otomatik kaldırılacak (dosyalar bilgisayarınızda kalır).\r\n\r\n")
                        : "";
                    if (plan.LocalSkipped.Count > 0)
                        note += T("Zaten bilgisayarınızda olduğu için kurulmayacak ({0}): {1}\r\n\r\n",
                            plan.LocalSkipped.Count,
                            string.Join(", ", plan.LocalSkipped.Take(5).Select(s => s.Addon.DisplayName)) +
                            (plan.LocalSkipped.Count > 5 ? "..." : ""));
                    if (plan.Blocked.Count > 0)
                    {
                        note += T("DLC/OIV paketleri kurulamıyor (update.rpf güvenli değil) ({0}): {1}\r\n{2}\r\n\r\n",
                            plan.Blocked.Count,
                            string.Join(", ", plan.Blocked.Take(5).Select(s => s.Addon.DisplayName)) +
                            (plan.Blocked.Count > 5 ? "..." : ""),
                            plan.Blocked[0].Reason);
                    }
                    bool go = true;
                    Invoke(() => go = Ui.Confirm(FindForm()!,
                        T("Sunucu: {0}\r\n\r\nKurulacak/güncellenecek: {1} eklenti ({2:F1} MB)\r\nKaldırılacak: {3}\r\n\r\n{4}Devam edilsin mi?",
                            mf.ServerName, plan.ToInstall.Count, mb, plan.ToRemove.Count, note)));
                    if (!go)
                    {
                        AppEnv.Log("Kullanıcı senkronizasyonu iptal etti.");
                        return null;
                    }
                }

                var contentDir = AppEnv.ServerContentFolder(sel, mf);
                AppEnv.Log($"Sunucu içerik klasörü: {contentDir}");
                var result = await applier.SyncAsync(client, mf, AppEnv.DataFolder,
                    contentDir, progress, ct);
                AppEnv.Log($"Senkronizasyon tamam: {result.Addons.Count} eklenti kurulu.");
                return (Manifest?)mf;
            }, ct);

            if (manifest == null)
            {
                SetStatus(T("Senkronizasyon kullanıcı tarafından iptal edildi."), Ui.WarnColor);
                SetProgress(0);
                return;
            }

            // "Bağlan ve Oyna" açıkça bağlanma isteğidir; AutoConnect her zaman açılır.
            ClientSettingsEditor.Apply(gameFolder, AppEnv.Settings.Username, gameHost,
                gamePort, sel.Password, autoConnect: true,
                lowDesync: AppEnv.Settings.LowDesyncProfile, log: AppEnv.Log);

            if (launch)
            {
                if (AppEnv.Settings.RemoveModsOnExit)
                {
                    var rec = new SessionRecord
                    {
                        ServerId = manifest.ServerId,
                        ServerName = manifest.ServerName,
                        Host = gameHost,
                        GamePort = gamePort,
                        StartedUtc = DateTime.UtcNow
                    };
                    SessionStore.Save(AppEnv.DataFolder, rec);
                    EnsureWatcher(rec);

                    SetStatus(T("Oyun açılıyor... Oturum aktif: çıkınca modlar otomatik kaldırılacak."), Ui.OkColor);
                }
                else
                {
                    SetStatus(T("Oyun açılıyor... (Oturum modu kapalı: modlar kurulu kalacak.)"), Ui.OkColor);
                }

                SetProgress(100);
                AppEnv.Log("Oyun başlatılıyor...");
                GameLauncher.Launch(gameFolder, AppEnv.Settings.LaunchExe, AppEnv.Log);
            }
            else
            {
                SetStatus(T("Hazır: sunucu modları kurulu."), Ui.OkColor);
                SetProgress(100);
            }

            keepRelay = true;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            SetStatus(T("İşlem iptal edildi."), Ui.WarnColor);
            AppEnv.Log("Senkronizasyon iptal edildi.");
        }
        catch (OperationCanceledException)
        {
            // Zaman aşımı (kullanıcı iptali değil): sessizce "iptal edildi"
            // demek yerine nedeni açıkça göster.
            const string msg = "Sunucuya ulaşılamadı (zaman aşımı). Sunucu açık mı? " +
                               "Kısa kod/uzun kod güncel mi? 'Yeniden Ara' veya yeni kod ile tekrar deneyin.";
            SetStatus(T("Sunucuya ulaşılamadı: zaman aşımı."), Ui.ErrColor);
            AppEnv.LogError("Bağlantı zaman aşımına uğradı: " + sel.Name);
            Ui.Error(FindForm()!, T(msg));
        }
        catch (Exception ex)
        {
            SetStatus(T("Hata: ") + ex.Message, Ui.ErrColor);
            AppEnv.LogError(ex.Message);
            Ui.Error(FindForm()!, ex.Message);
        }
        finally
        {
            if (!keepRelay) DisposeActiveRelay();
            _busy = false;
            _connectBtn.Enabled = true;
            _cancelBtn.Enabled = false;
            _cts?.Dispose();
            _cts = null;
            RefreshSessionUi();
        }
    }

    private async Task CleanupLeftoverSessionAsync(string gameFolder)
    {
        var leftover = SessionStore.Load(AppEnv.DataFolder);
        if (leftover == null) return;

        AppEnv.Log("Önceki oturum kalıntıları temizleniyor...");
        SetStatus(T("Önceki oturum kalıntıları temizleniyor..."), Ui.WarnColor);
        SessionService.KillWatcher(leftover, AppEnv.Log);
        await Task.Run(() => SessionService.Finish(AppEnv.DataFolder, gameFolder, AppEnv.Log));
        RefreshSessionUi();
    }

    /// <summary>
    /// Mod senkronizasyonu olmayan (veya istenmeyen) sunuculara bağlanır.
    /// Yalnızca istemci ayarlarını yazar ve oyunu başlatır; mod kurmaz.
    /// </summary>
    private async Task ConnectGameOnlyAsync(ServerEntry sel, bool launch)
    {
        if (_busy) return;
        if (!MainForm.RequireGameFolder(FindForm()!, out var gameFolder)) return;

        if (AddonApplier.IsGameRunning())
        {
            Ui.Warn(FindForm()!, T("GTA V açık. Modları değiştirmeden önce oyunu kapatın."));
            return;
        }

        _busy = true;
        _connectBtn.Enabled = false;
        _endSessionBtn.Enabled = false;
        _cancelBtn.Enabled = true;
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;
        SetStatus(T("Modsuz bağlantı hazırlanıyor..."), Ui.WarnColor);
        SetProgress(0);
        RelaySession? relay = null;
        var keepRelay = false;

        try
        {
            await CleanupLeftoverSessionAsync(gameFolder);
            DisposeActiveRelay();

            var gameHost = sel.Host;
            var gamePort = sel.GamePort;

            if (!string.IsNullOrWhiteSpace(sel.ShortCode))
                await RefreshRelayInfoAsync(sel, ct);

            var localDirect = LocalAddresses.IsLocalAddress(sel.Host) ||
                              LocalAddresses.IsSameSubnet(sel.Host);

            if (localDirect)
            {
                if (LocalAddresses.IsLocalAddress(sel.Host))
                    gameHost = "127.0.0.1";
                AppEnv.Log($"Modsuz bağlantı: yerel/aynı ağ sunucusu, relay atlanıyor ({sel.Host}).");
            }
            else if (sel.UsesRelay)
            {
                SetStatus(T("Otomatik relay bağlantısı kuruluyor..."), Ui.WarnColor);
                relay = await TryStartRelayAsync(sel, ct, requireSync: false);
                if (relay != null)
                {
                    gameHost = "127.0.0.1";
                    gamePort = relay.GamePort;
                    AppEnv.Log($"Modsuz bağlantı relay üzerinden: {sel.RelayHost} " +
                               $"(oyun {sel.RelayGamePort} -> yerel {relay.GamePort})");
                }
                else if (!CanTryDirect(sel, requireSync: false))
                {
                    throw new SyncException(T(
                        "Otomatik relay bağlantısı kurulamadı; bu sunucuya doğrudan da ulaşılamıyor. " +
                        "Sunucu ve launcher açık mı? Güncel kodu kullanın."));
                }
                else
                {
                    AppEnv.Log($"Modsuz bağlantı: relay kurulamadı; doğrudan adres denenecek: {sel.Host}:{sel.GamePort}");
                }
            }

            if (relay == null && !localDirect)
            {
                if (LocalAddresses.IsLocalAddress(sel.Host))
                {
                    gameHost = "127.0.0.1";
                }
                else if (!CanTryDirect(sel, requireSync: false))
                {
                    throw new SyncException(T(
                        "Bu sunucuya doğrudan ulaşılamaz: adres özel bir ağda ve otomatik relay bilgisi yok. " +
                        "Sunucu sahibinden güncel kısa kodu isteyin."));
                }
            }

            ClientSettingsEditor.Apply(gameFolder, AppEnv.Settings.Username, gameHost,
                gamePort, sel.Password, autoConnect: true,
                lowDesync: AppEnv.Settings.LowDesyncProfile, log: AppEnv.Log);

            if (relay != null)
            {
                // Ayar dosyası artık relay'in yerel portunu gösteriyor; oyun
                // ister şimdi ister sonra başlatılsın tünel açık kalmalı.
                _activeRelay = relay;
                keepRelay = true;
            }

            if (launch)
            {
                SetStatus(T("Oyun açılıyor... (modsuz bağlantı)"), Ui.OkColor);
                SetProgress(100);
                AppEnv.Log("Oyun başlatılıyor (modsuz bağlantı)...");
                GameLauncher.Launch(gameFolder, AppEnv.Settings.LaunchExe, AppEnv.Log);
            }
            else
            {
                SetStatus(T("Hazır: modsuz bağlantı ayarlandı."), Ui.OkColor);
                SetProgress(100);
            }
        }
        catch (Exception ex)
        {
            SetStatus(T("Hata: ") + ex.Message, Ui.ErrColor);
            AppEnv.LogError(ex.Message);
            Ui.Error(FindForm()!, ex.Message);
        }
        finally
        {
            if (!keepRelay && relay != null)
            {
                try { await relay.DisposeAsync(); }
                catch (Exception ex) { AppEnv.Log("Relay kapatılırken hata: " + ex.Message); }
            }

            _busy = false;
            _connectBtn.Enabled = true;
            _cancelBtn.Enabled = false;
            _cts?.Dispose();
            _cts = null;
            RefreshSessionUi();
        }
    }

    /// <summary>Yalnızca indirme: sunucu modlarını önbelleğe alır, oyuna kurmaz.</summary>
    private async Task PrefetchAsync(bool force = false)
    {
        if (_busy) return;
        var sel = Selected;
        if (sel == null) { Ui.Warn(FindForm()!, T("Önce listeden bir sunucu seçin.")); return; }
        if (!MainForm.RequireGameFolder(FindForm()!, out var gameFolder)) return;

        if (force && !Ui.Confirm(FindForm()!,
                T("Bu sunucunun indirilmiş mod dosyaları silinip sunucudan yeniden indirilecek.\r\n" +
                  "Oyun açıksa önce kapatın.\r\n\r\nDevam edilsin mi?")))
            return;

        _busy = true;
        _connectBtn.Enabled = false;
        _endSessionBtn.Enabled = false;
        _cancelBtn.Enabled = true;
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;
        RelaySession? relay = null;
        SetStatus(T(force ? "Mod dosyaları yeniden indiriliyor (oyuna kurulmayacak)..." : "Mod dosyaları indiriliyor (oyuna kurulmayacak)..."), Ui.WarnColor);
        SetProgress(0);

        try
        {
            var applier = new AddonApplier(gameFolder, AppEnv.Log);
            var progress = new Progress<SyncProgress>(p =>
            {
                SetStatus(p.Message, Color.DimGray);
                SetProgress((int)(p.Fraction * 100));
            });

            var syncHost = sel.Host;
            var syncPort = sel.SyncPort;

            if (!string.IsNullOrWhiteSpace(sel.ShortCode))
            {
                SetStatus(T("Sunucunun güncel relay bilgisi alınıyor..."), Ui.WarnColor);
                await RefreshRelayInfoAsync(sel, ct);
            }

            var localDirect = LocalAddresses.IsLocalAddress(sel.Host) ||
                              LocalAddresses.IsSameSubnet(sel.Host);

            if (localDirect)
            {
                if (LocalAddresses.IsLocalAddress(sel.Host))
                    syncHost = "127.0.0.1";
                AppEnv.Log($"Ön indirme: yerel/aynı ağ sunucusu, relay atlanıyor ({sel.Host}).");
            }
            else if (sel.UsesRelay)
            {
                SetStatus(T("Otomatik relay bağlantısı kuruluyor..."), Ui.WarnColor);
                relay = await TryStartRelayAsync(sel, ct, requireSync: true);
                if (relay != null)
                {
                    if (CanTryDirect(sel) && await ProbeDirectSyncAsync(sel, ct))
                    {
                        syncHost = sel.Host;
                        syncPort = sel.SyncPort;
                        AppEnv.Log($"Ön indirme sync doğrudan: {sel.Host}:{sel.SyncPort}");
                    }
                    else
                    {
                        syncHost = "127.0.0.1";
                        syncPort = relay.SyncPort;
                        AppEnv.Log($"Ön indirme relay bağlantısı: {sel.RelayHost} " +
                                   $"(sync {sel.RelaySyncPort} -> yerel {relay.SyncPort})");
                    }
                }
                else if (CanTryDirect(sel))
                {
                    SetStatus(T("Otomatik relay kurulamadı; doğrudan adres deneniyor..."), Ui.WarnColor);
                    if (!await ProbeDirectSyncAsync(sel, ct))
                        throw new SyncException(T(
                            "Sunucuya ne otomatik relay ne de doğrudan bağlantı kurulamadı. " +
                            "Sunucu ve launcher açık mı? İnternet bağlantınızı kontrol edip yeniden deneyin."));
                    AppEnv.Log($"Ön indirme: relay kurulamadı; doğrudan adres kullanılacak: " +
                               $"{sel.Host}:{sel.SyncPort}");
                    if (LocalAddresses.IsLocalAddress(sel.Host))
                        syncHost = "127.0.0.1";
                }
                else
                {
                    throw new SyncException(T(
                        "Otomatik relay bağlantısı kurulamadı. Sunucu ve launcher açık mı? İnternet bağlantınızı kontrol edip yeniden deneyin."));
                }
            }
            else
            {
                if (LocalAddresses.IsLocalAddress(sel.Host))
                    syncHost = "127.0.0.1";
                else if (!CanTryDirect(sel))
                    throw new SyncException(T(
                        "Bu sunucuya doğrudan ulaşılamaz: adres özel bir ağda ve otomatik relay bilgisi yok. " +
                        "Sunucu sahibinden güncel kısa kodu isteyin."));
            }

            await Task.Run(async () =>
            {
                using var client = new SyncClient(syncHost, syncPort, sel.SyncToken, AppEnv.Log);

                using (var pingCts = CancellationTokenSource.CreateLinkedTokenSource(ct))
                {
                    pingCts.CancelAfter(TimeSpan.FromSeconds(15));
                    await client.PingAsync(pingCts.Token);
                }

                var mf = await client.GetManifestAsync(ct);
                AppEnv.Log($"Ön indirme: {mf.ServerName} ({mf.Addons.Count} eklenti)");
                var contentDir = AppEnv.ServerContentFolder(sel, mf);
                AppEnv.Log($"Sunucu içerik klasörü: {contentDir}");

                if (force)
                {
                    var state = SyncStateStore.Load(AppEnv.DataFolder, mf.ServerId);
                    if (state.Addons.Count > 0)
                        throw new SyncException(
                            T("Bu sunucunun modları şu anda oyuna kurulu. Önce 'Oturumu Bitir' ile kaldırın."));
                    AppEnv.ResetServerContent(contentDir);
                    AppEnv.Log($"Yeniden indirme için sunucu içeriği sıfırlandı: {contentDir}");
                }

                await applier.PrefetchAsync(client, mf, contentDir, progress, ct);
            }, ct);

            SetStatus(T(force
                ? "Tüm mod dosyaları yeniden indirildi (oyuna kurulmadı)."
                : "Tüm mod dosyaları indirildi (oyuna kurulmadı). Bir sonraki girişte yeniden indirilmez."), Ui.OkColor);
            SetProgress(100);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            SetStatus(T("İndirme iptal edildi."), Ui.WarnColor);
            AppEnv.Log("Ön indirme iptal edildi.");
        }
        catch (OperationCanceledException)
        {
            const string msg = "Sunucuya ulaşılamadı (zaman aşımı). Sunucu açık mı? " +
                               "Kısa kod/uzun kod güncel mi? Yeni kod ile tekrar deneyin.";
            SetStatus(T("Sunucuya ulaşılamadı: zaman aşımı."), Ui.ErrColor);
            AppEnv.LogError("Ön indirme zaman aşımına uğradı: " + sel.Name);
            Ui.Error(FindForm()!, T(msg));
        }
        catch (Exception ex)
        {
            SetStatus(T("Hata: ") + ex.Message, Ui.ErrColor);
            AppEnv.LogError(ex.Message);
            Ui.Error(FindForm()!, ex.Message);
        }
        finally
        {
            if (relay != null)
            {
                try { await relay.DisposeAsync(); }
                catch (Exception ex) { AppEnv.Log("Relay kapatılırken hata: " + ex.Message); }
            }

            _busy = false;
            _connectBtn.Enabled = true;
            _cancelBtn.Enabled = false;
            _cts?.Dispose();
            _cts = null;
            RefreshSessionUi();
        }
    }

    // ------------------------------------------------------------------ oturum yönetimi

    /// <summary>Launcher açılışında çağrılır: yarım kalan oturumu kurtarır.</summary>
    public async void RecoverSessionIfAny()
    {
        try
        {
            var rec = SessionStore.Load(AppEnv.DataFolder);
            if (rec == null) { RefreshSessionUi(); return; }

            if (AddonApplier.IsGameRunning())
            {
                AppEnv.Log("Aktif oyun oturumu algılandı; oyun kapanınca sunucu modları kaldırılacak.");
                EnsureWatcher(rec);
                SetStatus(T("Oturum sürüyor: {0} (oyun kapanınca modlar kaldırılır).", rec.ServerName), Ui.WarnColor);
            }
            else
            {
                SetStatus(T("Önceki oturumdan kalan modlar temizleniyor..."), Ui.WarnColor);
                SessionService.KillWatcher(rec, AppEnv.Log);
                await Task.Run(() => SessionService.Finish(AppEnv.DataFolder, AppEnv.Settings.GameFolder, AppEnv.Log));
                SetStatus(T("Önceki oturum kalıntıları temizlendi."), Ui.OkColor);
            }

            RefreshSessionUi();
        }
        catch (Exception ex)
        {
            AppEnv.LogError("Oturum kurtarma hatası: " + ex.Message);
        }
    }

    /// <summary>Bağımsız izleyiciyi başlatır; yoksa launcher içinde izler.</summary>
    private void EnsureWatcher(SessionRecord rec)
    {
        if (SessionService.IsWatcherAlive(rec)) return;

        var pid = SessionService.TryStartWatcher(AppEnv.DataFolder);
        if (pid > 0)
        {
            rec.WatcherPid = pid;
            SessionStore.Save(AppEnv.DataFolder, rec);
            AppEnv.Log($"Oturum izleyicisi başlatıldı (PID {pid}); launcher kapansa bile temizlik yapılır.");
            return;
        }

        // Yedek plan: launcher içinde izle (launcher kapanırsa bir sonraki açılışta kurtarılır).
        AppEnv.Log("Oturum izleyicisi (SyncTool) bulunamadı; izleme launcher içinde yapılacak.");
        _fallbackWatcherCts?.Cancel();
        _fallbackWatcherCts = new CancellationTokenSource();
        var ct = _fallbackWatcherCts.Token;
        var gameFolder = AppEnv.Settings.GameFolder;
        _ = Task.Run(async () =>
        {
            try
            {
                await SessionService.WatchAsync(AppEnv.DataFolder, gameFolder, null, AppEnv.Log, 12, ct);
                BeginInvoke(() =>
                {
                    SetStatus(T("Oturum bitti: sunucu modları kaldırıldı."), Ui.OkColor);
                    RefreshSessionUi();
                });
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                AppEnv.LogError("Oturum izleme hatası: " + ex.Message);
            }
        });
    }

    private async Task EndSessionAsync()
    {
        var rec = SessionStore.Load(AppEnv.DataFolder);
        if (rec == null) { RefreshSessionUi(); return; }

        if (AddonApplier.IsGameRunning())
        {
            Ui.Warn(FindForm()!, T("Oyun açıkken oturum bitirilemez. Önce oyunu kapatın."));
            return;
        }

        if (!Ui.Confirm(FindForm()!,
            T("'{0}' oturumu bitirilecek ve sunucu modları oyun klasöründen kaldırılacak.\r\nKendi modlarınız ve diğer dosyalar etkilenmez. Devam edilsin mi?", rec.ServerName)))
            return;

        try
        {
            _endSessionBtn.Enabled = false;
            _fallbackWatcherCts?.Cancel();
            SessionService.KillWatcher(rec, AppEnv.Log);
            await Task.Run(() => SessionService.Finish(AppEnv.DataFolder, AppEnv.Settings.GameFolder, AppEnv.Log));
            SetStatus(T("Oturum bitirildi; sunucu modları kaldırıldı."), Ui.OkColor);
        }
        catch (Exception ex)
        {
            AppEnv.LogError(ex.Message);
            Ui.Error(FindForm()!, T("Oturum bitirilemedi:\r\n") + ex.Message);
        }
        finally
        {
            DisposeActiveRelay();
            RefreshSessionUi();
        }
    }

    private static bool CanTryDirect(ServerEntry entry, bool requireSync = true) =>
        !string.IsNullOrWhiteSpace(entry.Host) &&
        !string.Equals(entry.Host, entry.RelayHost, StringComparison.OrdinalIgnoreCase) &&
        entry.GamePort is > 0 and <= 65535 &&
        (!requireSync || entry.SyncPort is > 0 and <= 65535) &&
        (LocalAddresses.IsDirectReachable(entry.Host) || LocalAddresses.IsLocalAddress(entry.Host));

    /// <summary>
    /// Sync sunucusuna doğrudan TCP erişimi olup olmadığını kısa sürede sınar.
    /// Relay kurulmuş olsa bile doğrudan erişim varsa büyük dosyalar relay
    /// üzerinden taşınmaz.
    /// </summary>
    private static async Task<bool> ProbeDirectSyncAsync(ServerEntry entry, CancellationToken ct)
    {
        if (entry.SyncPort is <= 0 or > 65535) return false;

        try
        {
            using var probeCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            probeCts.CancelAfter(TimeSpan.FromSeconds(3));
            using var client = new SyncClient(entry.Host, entry.SyncPort, entry.SyncToken);
            await client.PingAsync(probeCts.Token);
            AppEnv.Log($"Doğrudan sync erişilebilir: {entry.Host}:{entry.SyncPort}");
            return true;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            AppEnv.Log($"Doğrudan sync yok ({entry.Host}:{entry.SyncPort}): {ex.Message}");
            return false;
        }
    }

    private static string NormalizeShortCode(string? code) =>
        ShortCodeDirectory.TryNormalize(code, out var normalized, out _) ? normalized : "";

    /// <summary>
    /// Kayıtlı kısa koddan güncel relay uç noktalarını ve doğrudan adresleri
    /// yeniler. Sunucu her başlatıldığında relay portları değişebildiği için
    /// bağlantı öncesinde çağrılır. Hata durumunda mevcut kayıt korunur.
    /// </summary>
    private static async Task<bool> RefreshRelayInfoAsync(
        ServerEntry entry, CancellationToken ct, bool quiet = false)
    {
        if (string.IsNullOrWhiteSpace(entry.ShortCode)) return entry.UsesRelay;

        try
        {
            using var lookupCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            lookupCts.CancelAfter(TimeSpan.FromSeconds(15));

            var offer = await ShortCodeDirectory.LookupAsync(entry.ShortCode, lookupCts.Token);
            if (offer == null)
            {
                if (!quiet)
                    AppEnv.Log($"Kısa kod güncel değil/çevrimdışı: {entry.ShortCode}; kayıtlı relay bilgisi korunuyor.");
                return entry.UsesRelay;
            }

            var changed = false;

            if (offer.HasRelay &&
                (!string.Equals(entry.RelayHost, offer.RelayHost, StringComparison.OrdinalIgnoreCase) ||
                 entry.RelaySyncPort != offer.RelaySyncPort ||
                 entry.RelayGamePort != offer.RelayGamePort))
            {
                entry.RelayHost = offer.RelayHost;
                entry.RelaySyncPort = offer.RelaySyncPort;
                entry.RelayGamePort = offer.RelayGamePort;
                changed = true;
            }

            if (entry.GamePort != offer.GamePort) { entry.GamePort = offer.GamePort; changed = true; }
            if (entry.SyncPort != offer.SyncPort) { entry.SyncPort = offer.SyncPort; changed = true; }
            if (!string.Equals(entry.Password, offer.Password, StringComparison.Ordinal))
            {
                entry.Password = offer.Password;
                changed = true;
            }
            if (!string.Equals(entry.SyncToken, offer.SyncToken, StringComparison.Ordinal))
            {
                entry.SyncToken = offer.SyncToken;
                changed = true;
            }

            // Eski kayıt yalnızca hotspot/ICS adresi taşıyorsa ve yayında
            // ulaşılabilir bir adres varsa onu kullan.
            if (!LocalAddresses.IsDirectReachable(entry.Host) &&
                !LocalAddresses.IsLocalAddress(entry.Host))
            {
                var best = offer.Hosts.FirstOrDefault(LocalAddresses.IsDirectReachable);
                if (!string.IsNullOrWhiteSpace(best))
                {
                    entry.Host = best;
                    changed = true;
                }
            }

            if (changed)
            {
                AppEnv.SaveSettings();
                AppEnv.Log($"Kısa koddan relay bilgisi güncellendi: {entry.ShortCode} " +
                           $"(relay {entry.RelayHost}:{entry.RelaySyncPort}/{entry.RelayGamePort})");
            }

            return entry.UsesRelay;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            if (!quiet)
                AppEnv.Log("Kısa koddan relay bilgisi yenilenemedi: " + ex.Message);
            return entry.UsesRelay;
        }
    }

    /// <summary>
    /// Otomatik relay'i kurmayı dener. ntfy önbelleği yeni yayını hemen
    /// göstermediğinde kısa kod bir önceki oturumun relay portlarını
    /// döndürebilir; bu durumda kısa kod birkaç kez yeniden sorgulanır ve
    /// uç noktalar tazelenince relay yeniden denenir. Böylece sunucu yeniden
    /// başlatıldığında eski portlar yüzünden bağlantı sessizce başarısız olmaz.
    /// </summary>
    private async Task<RelaySession?> TryRelayWithRefreshAsync(
        ServerEntry entry, CancellationToken ct)
    {
        // Uzun kodun kendi taşıdığı uç noktalar, kod yeni oluşturulduysa
        // günceldir. Kısa kod yayını ise ntfy önbelleği yüzünden eski
        // olabilir; bu yüzden ikisi de denenir.
        var originalHost = entry.RelayHost;
        var originalSyncPort = entry.RelaySyncPort;
        var originalGamePort = entry.RelayGamePort;
        var hasOriginal = entry.UsesRelay;
        var triedOriginal = hasOriginal;

        var relay = hasOriginal
            ? await TryStartRelayAsync(entry, ct, requireSync: true)
            : null;

        if (relay != null || string.IsNullOrWhiteSpace(entry.ShortCode))
            return relay;

        const int attempts = 5;
        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            SetStatus(T("Relay bilgisi yenileniyor; yeniden deneniyor... ({0}/{1})",
                attempt, attempts), Ui.WarnColor);
            await Task.Delay(TimeSpan.FromSeconds(2), ct);

            var before = RelayKey(entry);
            await RefreshRelayInfoAsync(entry, ct);
            var after = RelayKey(entry);
            AppEnv.Log($"Relay yenileme denemesi {attempt}/{attempts}: " +
                       $"{(before.Length == 0 ? "relay yok" : before)} -> " +
                       $"{(entry.UsesRelay ? after : "relay yok")}");

            if (entry.UsesRelay)
            {
                relay = await TryStartRelayAsync(entry, ct, requireSync: true);
                if (relay != null)
                {
                    AppEnv.Log($"Relay güncel uç noktalarla kuruldu: {after}");
                    return relay;
                }
            }
            else
            {
                AppEnv.Log($"Kısa kodda henüz relay yayını yok: {entry.ShortCode}");
            }

            // Kısa kod önbelleği eski bir oturumun portlarını döndürüp kodun
            // içindeki çalışan uç noktaları ezmiş olabilir; koddakini de dene.
            if (!triedOriginal && hasOriginal)
            {
                triedOriginal = true;
                entry.RelayHost = originalHost;
                entry.RelaySyncPort = originalSyncPort;
                entry.RelayGamePort = originalGamePort;
                AppEnv.Log($"Kısa kod önbelleği eski olabilir; koddaki relay deneniyor: {RelayKey(entry)}");
                SetStatus(T("Otomatik relay bağlantısı kuruluyor..."), Ui.WarnColor);
                relay = await TryStartRelayAsync(entry, ct, requireSync: true);
                if (relay != null)
                {
                    AppEnv.Log($"Relay koddaki uç noktalarla kuruldu: {RelayKey(entry)}");
                    return relay;
                }
            }
        }

        return null;
    }

    private static string RelayKey(ServerEntry entry) =>
        $"{entry.RelayHost}:{entry.RelaySyncPort}/{entry.RelayGamePort}";

    /// <summary>
    /// Relay kurulamazsa doğrudan bağlantıya düşebilmek için hatayı yutar.
    /// Kullanıcı iptal ettiyse iptal istisnası yukarı çıkar.
    /// </summary>
    private static async Task<RelaySession?> TryStartRelayAsync(
        ServerEntry entry, CancellationToken ct, bool requireSync = true)
    {
        try
        {
            return await RelaySession.StartAsync(entry, ct, requireSync, AppEnv.Log);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            AppEnv.Log("Otomatik relay bağlantısı kurulamadı: " + ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Aktif relay oturumunu kapatır. Relay üzerinden oynanan oyunda bağlantı
    /// launcher süreci üzerinden geçtiği için launcher açık kalmalıdır.
    /// </summary>
    private void DisposeActiveRelay()
    {
        var relay = _activeRelay;
        _activeRelay = null;
        _relayIdleSince = null;
        _relayGameSeen = false;
        if (relay == null) return;

        try
        {
            if (!Task.Run(async () => await relay.DisposeAsync().ConfigureAwait(false))
                    .Wait(TimeSpan.FromSeconds(4)))
                AppEnv.Log("Relay oturumu arka planda kapatılıyor.");
        }
        catch (Exception ex)
        {
            AppEnv.Log("Relay oturumu kapatılırken hata: " + ex.Message);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            try { _cts?.Cancel(); } catch { }
            try { _fallbackWatcherCts?.Cancel(); } catch { }
            _sessionTimer.Stop();
            DisposeActiveRelay();
        }
        base.Dispose(disposing);
    }

    private void RefreshSessionUi()
    {
        var rec = SessionStore.Load(AppEnv.DataFolder);
        bool active = rec != null;
        _endSessionBtn.Enabled = active && !_busy;

        // Relay oyun bağlantısı launcher üzerinden geçer. Oyun çalışırken relay'i
        // açık tut; oyun kapandıktan sonra kısa bir süre sonra temizle.
        if (_activeRelay != null && !_busy)
        {
            if (AddonApplier.IsGameRunning())
            {
                _relayGameSeen = true;
                _relayIdleSince = null;
            }
            else if (_relayGameSeen)
            {
                _relayIdleSince ??= DateTime.UtcNow;
                if ((DateTime.UtcNow - _relayIdleSince.Value).TotalSeconds >= 12)
                    DisposeActiveRelay();
            }
        }
        else if (_activeRelay == null)
        {
            _relayGameSeen = false;
            _relayIdleSince = null;
        }

        if (active)
        {
            bool oyun = AddonApplier.IsGameRunning();
            _sessionInfo.Text = "● " + T("OTURUM AKTİF: {0} — {1}", rec!.ServerName,
                T(oyun ? "oyun çalışıyor; çıkınca modlar kaldırılacak." : "oyun bekleniyor..."));
            _sessionInfo.ForeColor = Ui.WarnColor;
        }
        else
        {
            _sessionInfo.Text = T("");
        }
    }

    private void ShowDetails(ServerEntry sel, Manifest manifest, AddonApplier.InstallPlan plan, SyncState state,
        bool syncViaRelay = false, bool gameViaRelay = false)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine(T("Sunucu : {0}", manifest.ServerName));
        sb.AppendLine(syncViaRelay
            ? T("Sync   : otomatik relay ({0})", sel.RelayHost)
            : T("Sync   : doğrudan ({0}:{1})", sel.Host, sel.SyncPort));
        sb.AppendLine(gameViaRelay
            ? T("Oyun   : otomatik relay (oyun sırasında launcher açık kalmalı).")
            : T("Oyun   : doğrudan ({0}:{1})", sel.Host, sel.GamePort));
        sb.AppendLine(T("Kimlik : {0}", manifest.ServerId));
        sb.AppendLine(T("Eklenti: {0}  |  kurulacak: {1}  |  güncel: {2}  |  kaldırılacak: {3}  |  sizde var: {4}  |  engellendi: {5}",
            manifest.Addons.Count, plan.ToInstall.Count, plan.UpToDate.Count, plan.ToRemove.Count,
            plan.LocalSkipped.Count, plan.Blocked.Count));
        sb.AppendLine(new string('-', 70));

        foreach (var a in manifest.Addons)
        {
            string status;
            var blocked = plan.Blocked.FirstOrDefault(s => s.Addon.Id == a.Id);
            if (a.Type == AddonType.ServerPlugin) status = T("sunucu tarafı");
            else if (plan.LocalSkipped.Any(s => s.Addon.Id == a.Id)) status = T("SİZDE VAR");
            else if (blocked != null) status = T("ENGELLENDİ");
            else if (plan.ToInstall.Contains(a)) status = state.Addons.Any(x => x.Id == a.Id)
                ? T("GÜNCELLENECEK")
                : T("KURULACAK");
            else status = T("güncel");

            var typeName = a.Type switch
            {
                AddonType.DlcPack => T("DLC paketi"),
                AddonType.OivPackage => T("OIV paketi"),
                AddonType.ClientScript => "script",
                AddonType.AsiPlugin => T("ASI eklentisi"),
                AddonType.ClientFiles => T("dosya"),
                AddonType.ServerPlugin => T("sunucu eklentisi"),
                _ => "?"
            };

            sb.AppendLine($"[{status,-14}] {a.DisplayName} ({typeName}, {a.TotalSize / 1024.0 / 1024.0:F1} MB)");
            if (!string.IsNullOrEmpty(a.Description))
                sb.AppendLine($"                 {a.Description}");
            if (blocked != null)
                sb.AppendLine($"                 {T("Neden")}: {blocked.Reason}");
        }

        if (plan.ToRemove.Count > 0)
        {
            sb.AppendLine(new string('-', 70));
            sb.AppendLine(T("Kaldırılacak (sunucudan çıkarılmış):"));
            foreach (var r in plan.ToRemove)
                sb.AppendLine($"  - {r.Id}");
        }

        var text = sb.ToString();
        if (InvokeRequired) BeginInvoke(() => _details.Text = text);
        else _details.Text = text;
    }

    private void LaunchOnly()
    {
        if (!MainForm.RequireGameFolder(FindForm()!, out var gameFolder)) return;
        if (AddonApplier.IsGameRunning()) { Ui.Warn(FindForm()!, T("GTA V zaten açık.")); return; }

        try
        {
            ClientSettingsEditor.DisableAutoConnect(gameFolder, AppEnv.Log);
            GameLauncher.Launch(gameFolder, AppEnv.Settings.LaunchExe, AppEnv.Log);
        }
        catch (Exception ex)
        {
            AppEnv.LogError(ex.Message);
            Ui.Error(FindForm()!, ex.Message);
        }
    }

    private void SetStatus(string text, Color color)
    {
        if (InvokeRequired) { BeginInvoke(() => SetStatus(text, color)); return; }
        _status.Text = SyncText.Translate(text);
        _status.ForeColor = color;
    }

    private void SetProgress(int value)
    {
        if (InvokeRequired) { BeginInvoke(() => SetProgress(value)); return; }
        _progress.Value = Math.Clamp(value, 0, 100);
    }
}
