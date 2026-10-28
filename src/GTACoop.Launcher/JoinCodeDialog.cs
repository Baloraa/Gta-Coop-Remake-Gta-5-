using System.Windows.Forms;
using GTACoop.Sync;

namespace GTACoop.Remake;

/// <summary>
/// Arkadaşın gönderdiği katılım kodunu yapıştırıp sunucuyu tek adımda ekleme penceresi.
/// Uzun GTC1 kodu bağlantı bilgilerini kendi taşır; kısa kod (ör. Gel.com) ise
/// internet üzerindeki şifreli yayından çözülür.
/// </summary>
public sealed class JoinCodeDialog : Form
{
    private readonly TextBox _code = new();
    private readonly Label _summary = new();
    private readonly Button _addBtn;
    private readonly Button _connectBtn;
    private readonly Button _retryBtn;
    private readonly ComboBox _mode = new();
    private readonly System.Windows.Forms.Timer _lookupTimer = new();
    private ServerEntry? _parsed;
    private CancellationTokenSource? _lookupCts;
    private string? _pendingCode;

    /// <summary>Çözülen sunucu kaydı (yalnızca OK ile kapanırsa dolu).</summary>
    public ServerEntry? Entry { get; private set; }

    /// <summary>Kullanıcı "Ekle ve Bağlan" seçti mi?</summary>
    public bool ConnectRequested { get; private set; }

    public JoinCodeDialog()
    {
        Text = T("Katılım Kodu");
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96f, 96f);
        Width = 760;
        Height = 620;
        MinimumSize = new Size(680, 560);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(12)
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 38f));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 62f));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var header = new Label
        {
            Text = T("Arkadaşınızın gönderdiği kodu buraya yapıştırın:"),
            AutoSize = true,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            Margin = new Padding(2, 0, 2, 6)
        };

        _code.Name = "JoinCodeBox";
        _code.Multiline = true;
        _code.Dock = DockStyle.Fill;
        _code.ScrollBars = ScrollBars.Both;
        _code.WordWrap = false;
        _code.Font = new Font("Consolas", 9.5f);
        _code.Margin = new Padding(2, 0, 2, 8);
        _code.TextChanged += (_, _) => Decode();

        _summary.Name = "JoinSummary";
        _summary.Dock = DockStyle.Fill;
        _summary.AutoSize = false;
        _summary.BackColor = Color.WhiteSmoke;
        _summary.BorderStyle = BorderStyle.FixedSingle;
        _summary.Padding = new Padding(8);
        _summary.Font = new Font("Segoe UI", 9.5f);
        _summary.Text = T("Kod yapıştırıldığında sunucu bilgileri burada görünür.");
        _summary.Margin = new Padding(2, 0, 2, 8);

        var buttons = Ui.Flow();
        buttons.Dock = DockStyle.Fill;
        buttons.Margin = new Padding(0, 0, 0, 4);
        buttons.Controls.Add(Ui.Btn(T("Panodan Yapıştır"), (_, _) => PasteCode()));
        _retryBtn = Ui.Btn(T("Yeniden Ara"), (_, _) => RetryLookup());
        buttons.Controls.Add(_retryBtn);

        var modeLabel = new Label
        {
            Text = T("Bağlantı:"),
            AutoSize = true,
            Margin = new Padding(12, 9, 4, 4)
        };
        _mode.Name = "JoinModeBox";
        _mode.DropDownStyle = ComboBoxStyle.DropDownList;
        _mode.Width = 285;
        _mode.Margin = new Padding(0, 5, 4, 4);
        _mode.Items.Add(T("Otomatik (relay öncelikli)"));
        _mode.Items.Add(T("Doğrudan (aynı ağ / Radmin VPN)"));
        _mode.SelectedIndex = 0;
        _mode.SelectedIndexChanged += (_, _) =>
        {
            if (_pendingCode != null) RetryLookup();
        };
        buttons.Controls.Add(modeLabel);
        buttons.Controls.Add(_mode);

        _addBtn = Ui.Btn(T("Ekle"), (_, _) => Accept(connect: false));
        _addBtn.Enabled = false;
        _connectBtn = Ui.Btn(T("Ekle ve Bağlan"), (_, _) => Accept(connect: true));
        _connectBtn.Enabled = false;
        buttons.Controls.Add(_addBtn);
        buttons.Controls.Add(_connectBtn);
        var cancelBtn = Ui.Btn(T("İptal"), (_, _) => Close());
        buttons.Controls.Add(cancelBtn);

        var hint = new Label
        {
            AutoSize = true,
            ForeColor = Color.DimGray,
            MaximumSize = new Size(720, 0),
            Text = T("İpucu: Kısa kod (ör. Gel.com) sunucuyu internet üzerinden bulur; sunucu açık olmalı. " +
                     "Uzun GTC1 kodu bağlantı bilgilerini kendi taşır ve internet gerektirmez. " +
                     "Kod şifre/token içerebilir; yalnızca güvendiğiniz kişilerle paylaşın.") +
                   "\r\n" + T("Otomatik relay sayesinde port yönlendirme/Radmin gerekmez; oyun sırasında launcher açık kalmalı.") +
                   "\r\n" + T("Varsayılan 'Otomatik' mod relay'i önce dener. Aynı Wi-Fi/hotspot veya Radmin VPN üzerindeyseniz 'Doğrudan' modunu seçebilirsiniz."),
            Margin = new Padding(2, 2, 2, 0)
        };

        root.Controls.Add(header, 0, 0);
        root.Controls.Add(_code, 0, 1);
        root.Controls.Add(_summary, 0, 2);
        root.Controls.Add(buttons, 0, 3);
        root.Controls.Add(hint, 0, 4);
        Controls.Add(root);

        AcceptButton = _connectBtn;
        CancelButton = cancelBtn;

        _lookupTimer.Interval = 600;
        _lookupTimer.Tick += (_, _) =>
        {
            _lookupTimer.Stop();
            var code = _pendingCode;
            if (code != null) _ = LookupShortCodeAsync(code);
        };

        FormClosed += (_, _) =>
        {
            _lookupTimer.Stop();
            _lookupCts?.Cancel();
        };

        // Panoda hazır bir kod varsa otomatik doldur (kopyala → launcher → yapıştır gerekmesin).
        TryFillFromClipboard();
    }

    private void TryFillFromClipboard()
    {
        try
        {
            if (!Clipboard.ContainsText()) return;
            var text = Clipboard.GetText().Trim();
            if (text.StartsWith(JoinCode.Prefix, StringComparison.OrdinalIgnoreCase) ||
                ShortCodeDirectory.IsShortCode(text))
                _code.Text = text;
        }
        catch { /* pano erişilemezse kullanıcı elle yapıştırır */ }
    }

    private void PasteCode()
    {
        try
        {
            if (!Clipboard.ContainsText())
            {
                Ui.Warn(this, T("Panoda metin yok."));
                return;
            }
            _code.Text = Clipboard.GetText().Trim();
        }
        catch (Exception ex)
        {
            Ui.Warn(this, T("Pano okunamadı: ") + ex.Message);
        }
    }

    private void RetryLookup()
    {
        Decode();
        if (_pendingCode == null) return;

        _lookupTimer.Stop();
        var code = _pendingCode;
        _ = LookupShortCodeAsync(code);
    }

    private void Decode()
    {
        _lookupTimer.Stop();
        _lookupCts?.Cancel();
        _pendingCode = null;

        var text = _code.Text.Trim();
        if (text.Length == 0)
        {
            _parsed = null;
            SetSummary(T("Kod yapıştırıldığında sunucu bilgileri burada görünür."), Color.DimGray);
            _addBtn.Enabled = _connectBtn.Enabled = false;
            return;
        }

        // 1) Uzun (GTC1) kod: bağlantı bilgilerini kendi taşır.
        if (JoinCode.TryParse(text, out var entry, out var longError) && entry != null)
        {
            _parsed = entry;
            var summary = T("Sunucu: {0}\r\nAdres: {1}\r\nOyun portu: {2}  |  Sync portu: {3}\r\nŞifre: {4}",
                entry.Name, entry.Host, entry.GamePort, entry.SyncPort,
                string.IsNullOrEmpty(entry.Password) ? T("yok") : T("var"));
            if (entry.UsesRelay)
                summary += "\r\n" + T("Otomatik relay: {0} (sync {1}, oyun {2}) — relay öncelikli kullanılacak.",
                    entry.RelayHost, entry.RelaySyncPort, entry.RelayGamePort);
            else
                summary += "\r\n" + T("Bu kodda otomatik relay bilgisi yok; yalnızca doğrudan bağlantı denenir.");
            if (!string.IsNullOrWhiteSpace(entry.ShortCode))
                summary += "\r\n" + T("Kısa kod: {0} — bağlanırken güncel relay bilgisi bu koddan alınır.",
                    entry.ShortCode);
            else if (entry.UsesRelay)
                summary += "\r\n" + T("Bu uzun kod tek oturumluk relay bilgisi taşır; sunucu yeniden başlatıldıysa yeni kod isteyin.");
            SetSummary(summary, Ui.OkColor);
            _addBtn.Enabled = _connectBtn.Enabled = true;
            return;
        }

        if (text.StartsWith(JoinCode.Prefix, StringComparison.OrdinalIgnoreCase) ||
            text.Length > ShortCodeDirectory.MaxLength)
        {
            _parsed = null;
            SetSummary(T("Geçersiz kod: {0}", longError), Ui.ErrColor);
            _addBtn.Enabled = _connectBtn.Enabled = false;
            return;
        }

        // 2) Kısa kod: internet üzerinden şifreli yayından çözülür.
        if (ShortCodeDirectory.TryNormalize(text, out var code, out var shortError))
        {
            _parsed = null;
            _addBtn.Enabled = _connectBtn.Enabled = false;
            SetSummary(T("Kısa kod aranıyor: {0} ...", text), Color.DimGray);
            _pendingCode = code;
            _lookupTimer.Start();
            return;
        }

        _parsed = null;
        SetSummary(T("Geçersiz kod: {0}", shortError), Ui.ErrColor);
        _addBtn.Enabled = _connectBtn.Enabled = false;
    }

    private async Task LookupShortCodeAsync(string code)
    {
        _lookupCts?.Cancel();
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        _lookupCts = cts;

        var display = _code.Text.Trim();
        SetSummary(T("Kısa kod aranıyor: {0} ...\r\n(İnternet üzerinden sunucu bilgileri alınıyor)", display),
            Color.DimGray);

        try
        {
            var auto = _mode.SelectedIndex == 0;
            ShortCodeDirectory.ShortCodeOffer? offer = null;

            // Sunucu relay'i kısa kod yayınından birkaç saniye sonra hazır
            // olabilir. Otomatik modda relay görünene kadar kodu birkaç kez
            // yeniden sorgula; aynı ağdaki doğrudan bağlantı varsa bekleme.
            const int maxLookups = 5;
            for (int attempt = 0; attempt < maxLookups; attempt++)
            {
                offer = await ShortCodeDirectory.LookupAsync(code, cts.Token);
                if (cts.IsCancellationRequested || IsDisposed || !IsCurrentShortCode(code)) return;

                if (offer == null)
                {
                    if (attempt < maxLookups - 1)
                    {
                        SetSummary(T("Kısa kod henüz yayında değil; yeniden deneniyor... ({0}/{1})",
                            attempt + 2, maxLookups), Color.DimGray);
                        await Task.Delay(1500, cts.Token);
                        continue;
                    }
                    break;
                }

                if (!auto || offer.HasRelay) break;

                if (attempt < maxLookups - 1)
                {
                    SetSummary(T("Sunucu bulundu; otomatik relay'in hazır olması bekleniyor... ({0}/{1})",
                        attempt + 2, maxLookups), Color.DimGray);
                    await Task.Delay(2000, cts.Token);
                }
            }

            if (offer == null)
            {
                SetSummary(T("Kısa kod bulunamadı.\r\nSunucu açık mı? Kodu doğru yazdınız mı? İnternet bağlantınız var mı?\r\nUzun GTC1 kodunu da deneyebilirsiniz."),
                    Ui.WarnColor);
                return;
            }

            SetSummary(auto
                ? T("Sunucu bulundu; otomatik relay öncelikli deneniyor...")
                : T("Sunucu bulundu; doğrudan adresler deneniyor..."), Color.DimGray);

            var resolved = await ResolveOfferAsync(offer, cts.Token);
            if (cts.IsCancellationRequested || IsDisposed || !IsCurrentShortCode(code)) return;

            if (resolved == null)
            {
                var noRelay = auto && !offer.HasRelay;
                SetSummary(noRelay
                        ? T("Sunucuya ulaşılamadı: kısa kodda otomatik relay bilgisi yok ve doğrudan adresler aynı ağda değil.\r\n" +
                            "Sunucu sahibinden sunucuyu yeniden başlatmasını veya güncel sürümü kullanmasını isteyin. " +
                            "Aynı Wi-Fi/hotspot'taysanız 'Doğrudan' modunu deneyin.")
                        : T("Sunucuya ulaşılamadı. Otomatik relay ve doğrudan adresler denendi.\r\n" +
                            "Sunucu açık mı? İkiniz de güncel sürümü kullanıyor musunuz? 'Yeniden Ara' ile tekrar deneyin."),
                    Ui.WarnColor);
                return;
            }

            resolved.ShortCode = code;
            _parsed = resolved;

            if (resolved.UsesRelay)
            {
                var direct = offer.Hosts.FirstOrDefault(LocalAddresses.IsDirectReachable);
                SetSummary(T("Kısa kod bulundu ✓\r\nSunucu: {0}\r\nOtomatik relay: {1} (sync {2}, oyun {3})\r\nYedek doğrudan adres: {4}\r\nOyun portu: {5}  |  Sync portu: {6}\r\nŞifre: {7}",
                    resolved.Name, resolved.RelayHost, resolved.RelaySyncPort, resolved.RelayGamePort,
                    string.IsNullOrWhiteSpace(direct) ? T("yok") : direct,
                    resolved.GamePort, resolved.SyncPort,
                    string.IsNullOrEmpty(resolved.Password) ? T("yok") : T("var")), Ui.OkColor);
            }
            else
            {
                SetSummary(T("Kısa kod bulundu ✓\r\nSunucu: {0}\r\nAdres: {1}:{2} ({3})\r\nOyun portu: {4}  |  Sync portu: {5}\r\nŞifre: {6}",
                    resolved.Name, resolved.Host, resolved.GamePort,
                    T("doğrudan"),
                    resolved.GamePort, resolved.SyncPort,
                    string.IsNullOrEmpty(resolved.Password) ? T("yok") : T("var")), Ui.OkColor);
            }
            _addBtn.Enabled = _connectBtn.Enabled = true;
        }
        catch (OperationCanceledException)
        {
            // Yeni bir arama başladı veya pencere kapandı.
        }
        catch (Exception ex)
        {
            if (!cts.IsCancellationRequested && !IsDisposed && IsCurrentShortCode(code))
                SetSummary(T("Kısa kod aranamadı: {0}", ex.Message), Ui.ErrColor);
        }
        finally
        {
            if (ReferenceEquals(_lookupCts, cts)) _lookupCts = null;
            cts.Dispose();
        }
    }

    private async Task<ServerEntry?> ResolveOfferAsync(
        ShortCodeDirectory.ShortCodeOffer offer, CancellationToken ct)
    {
        // Otomatik mod: relay varsa her zaman önce relay denenir. Böylece
        // 192.168.137.x gibi yalnızca sunucunun kendi hotspot'unda geçerli
        // adresler, uzaktaki arkadaş tarafından yanlışlıkla seçilmez.
        var preferDirect = _mode.SelectedIndex == 1;

        if (!preferDirect && offer.HasRelay)
        {
            var relayEntry = await TryRelayAsync(offer, ct);
            if (relayEntry != null) return relayEntry;
            if (ct.IsCancellationRequested) return null;
            SetSummary(T("Otomatik relay yanıt vermedi; uygun doğrudan adresler deneniyor..."),
                Ui.WarnColor);
        }

        var hosts = preferDirect
            ? offer.Hosts.ToList()
            : offer.Hosts.Where(LocalAddresses.IsDirectReachable).ToList();

        if (hosts.Count == 0)
            return null;

        var direct = await TryHostsAsync(hosts, offer, ct);
        if (direct != null) return direct;

        // Relay geçici bir aksaklık yaşadıysa son bir kez daha dene.
        if (!preferDirect && offer.HasRelay && !ct.IsCancellationRequested)
            return await TryRelayAsync(offer, ct);

        return null;
    }

    private static async Task<ServerEntry?> TryHostsAsync(
        IReadOnlyList<string> hosts, ShortCodeDirectory.ShortCodeOffer offer, CancellationToken ct)
    {
        var pending = hosts
            .Select(host => TryHostAsync(host, offer, ct))
            .ToList();

        while (pending.Count > 0)
        {
            var done = await Task.WhenAny(pending);
            pending.Remove(done);
            var entry = await done;
            if (entry != null) return entry;
        }

        return null;
    }

    private static async Task<ServerEntry?> TryRelayAsync(
        ShortCodeDirectory.ShortCodeOffer offer, CancellationToken ct)
    {
        if (!offer.HasRelay) return null;

        for (int attempt = 0; attempt < 2; attempt++)
        {
            if (ct.IsCancellationRequested) return null;

            try
            {
                using var linkCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                linkCts.CancelAfter(TimeSpan.FromSeconds(15));

                using var client = new SyncClient(offer.RelayHost, offer.RelaySyncPort, offer.SyncToken);
                var mf = await client.GetManifestAsync(linkCts.Token);

                var host = offer.Hosts.FirstOrDefault(LocalAddresses.IsDirectReachable)
                           ?? offer.Hosts.FirstOrDefault()
                           ?? offer.RelayHost;

                return new ServerEntry
                {
                    Name = string.IsNullOrWhiteSpace(mf.ServerName) ? offer.Name : mf.ServerName,
                    Host = host,
                    GamePort = offer.GamePort,
                    SyncPort = offer.SyncPort,
                    Password = offer.Password,
                    SyncToken = offer.SyncToken,
                    AutoConnect = true,
                    RelayHost = offer.RelayHost,
                    RelaySyncPort = offer.RelaySyncPort,
                    RelayGamePort = offer.RelayGamePort
                };
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return null;
            }
            catch
            {
                if (attempt == 0)
                {
                    try { await Task.Delay(1000, ct); }
                    catch (OperationCanceledException) { return null; }
                }
            }
        }

        return null;
    }

    private static async Task<ServerEntry?> TryHostAsync(string host,
        ShortCodeDirectory.ShortCodeOffer offer, CancellationToken ct)
    {
        try
        {
            using var linkCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            linkCts.CancelAfter(TimeSpan.FromSeconds(7));

            using var client = new SyncClient(host, offer.SyncPort, offer.SyncToken);
            var mf = await client.GetManifestAsync(linkCts.Token);

            return new ServerEntry
            {
                Name = string.IsNullOrWhiteSpace(mf.ServerName) ? offer.Name : mf.ServerName,
                Host = host,
                GamePort = offer.GamePort,
                SyncPort = offer.SyncPort,
                Password = offer.Password,
                SyncToken = offer.SyncToken,
                AutoConnect = true
            };
        }
        catch
        {
            // Bu adres çalışmadı; diğerleri paralel deneniyor.
            return null;
        }
    }

    private bool IsCurrentShortCode(string normalized)
    {
        var text = _code.Text.Trim();
        return ShortCodeDirectory.TryNormalize(text, out var current, out _) &&
               string.Equals(current, normalized, StringComparison.Ordinal);
    }

    private void SetSummary(string text, Color color)
    {
        if (IsDisposed) return;
        _summary.ForeColor = color;
        _summary.Text = text;
    }

    private void Accept(bool connect)
    {
        if (_parsed == null)
        {
            Ui.Warn(this, T("Önce geçerli bir katılım kodu yapıştırın."));
            return;
        }

        Entry = _parsed;
        ConnectRequested = connect;
        DialogResult = DialogResult.OK;
        Close();
    }
}
