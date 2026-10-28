using System.Windows.Forms;
using GTACoop.Sync;

namespace GTACoop.Remake;

/// <summary>
/// Sunucu sahibinin arkadaşına göndereceği katılım kodunu üretir.
/// Kısa kod (ör. Gel.com) internet üzerinden bulunur; uzun GTC1 kodu bağlantı
/// bilgilerini kendi taşır ve internet/port yönlendirme olmadan da kullanılabilir.
/// </summary>
public sealed class ShareCodeDialog : Form
{
    private readonly MyServerSettings _settings;
    private readonly ComboBox _address = new();
    private readonly TextBox _code = new();
    private readonly Label _status = new();
    private readonly TextBox _shortCodeBox = new();
    private readonly Label _shortStatus = new();
    private readonly Button _copyShortBtn;
    private readonly bool _serverRunning;
    private readonly string _publishStatus;
    private readonly bool _publishOk;
    private readonly Func<RelayEndpoints?>? _relayProvider;
    private RelayEndpoints? _relay;
    private readonly string _relayStatus;

    public ShareCodeDialog(MyServerSettings settings, bool serverRunning = false,
        string? publishStatus = null, bool publishOk = false,
        RelayEndpoints? relay = null, string? relayStatus = null,
        Func<RelayEndpoints?>? relayProvider = null)
    {
        _settings = settings;
        _serverRunning = serverRunning;
        _publishStatus = publishStatus ?? "";
        _publishOk = publishOk;
        _relayProvider = relayProvider;
        _relay = relay ?? relayProvider?.Invoke();
        _relayStatus = relayStatus ?? "";

        Text = T("Katılım Kodu Oluştur");
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96f, 96f);
        Width = 680;
        Height = 700;
        MinimumSize = new Size(580, 620);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 9,
            Padding = new Padding(12)
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int i = 0; i < 9; i++) root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles[5] = new RowStyle(SizeType.Percent, 100f); // uzun kod kutusu kalan alanı doldurur

        // --- kısa kod
        var hasShortCode = !string.IsNullOrWhiteSpace(_settings.ShortCode);
        var shortGroup = new GroupBox
        {
            Text = T("Kısa Kod (arkadaşınızın yazacağı kod)"),
            Dock = DockStyle.Fill,
            Height = 150,
            Padding = new Padding(8),
            Margin = new Padding(2, 0, 2, 8)
        };

        var shortTable = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 2
        };
        shortTable.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        shortTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        shortTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
        shortTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        shortTable.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var shortLabel = new Label
        {
            Text = T("Kısa kod:"),
            AutoSize = true,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            Margin = new Padding(0, 6, 6, 0)
        };

        _shortCodeBox.Name = "ShareShortCodeBox";
        _shortCodeBox.ReadOnly = true;
        _shortCodeBox.Dock = DockStyle.Fill;
        _shortCodeBox.Font = new Font("Consolas", 11f, FontStyle.Bold);
        _shortCodeBox.Text = hasShortCode ? _settings.ShortCode.Trim() : T("(ayarlanmamış)");
        _shortCodeBox.Margin = new Padding(0, 2, 6, 2);

        _copyShortBtn = Ui.Btn(T("Kısa Kodu Kopyala"), (_, _) => CopyShortCode());
        _copyShortBtn.AutoSize = false;
        _copyShortBtn.Dock = DockStyle.Fill;
        _copyShortBtn.Margin = new Padding(0, 2, 0, 2);
        _copyShortBtn.Enabled = hasShortCode;

        _shortStatus.Name = "ShareShortCodeStatus";
        _shortStatus.AutoSize = true;
        _shortStatus.MaximumSize = new Size(600, 0);
        _shortStatus.ForeColor = ShortStatusColor();
        _shortStatus.Text = ShortStatusText();

        shortTable.Controls.Add(shortLabel, 0, 0);
        shortTable.Controls.Add(_shortCodeBox, 1, 0);
        shortTable.Controls.Add(_copyShortBtn, 2, 0);
        shortTable.Controls.Add(_shortStatus, 0, 1);
        shortTable.SetColumnSpan(_shortStatus, 3);
        shortGroup.Controls.Add(shortTable);

        // --- uzun kodun adresi
        var addressLabel = new Label
        {
            Text = T("Sunucu adresi (arkadaşınızın kullanacağı adres):"),
            AutoSize = true,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            Margin = new Padding(2, 0, 2, 4)
        };

        _address.Name = "ShareAddressBox";
        _address.DropDownStyle = ComboBoxStyle.DropDown;
        _address.Dock = DockStyle.Fill;
        _address.Margin = new Padding(2, 0, 2, 2);
        foreach (var a in LocalAddresses.Detect())
            _address.Items.Add(a);
        if (_address.Items.Count > 0)
            _address.SelectedIndex = 0;

        var addressHint = new Label
        {
            AutoSize = true,
            ForeColor = Color.DimGray,
            MaximumSize = new Size(630, 0),
            Text = T("Listeden bir adres seçin (Radmin VPN 26.x, Wi-Fi/yerel IP) veya arkadaşınızın kullanacağı adresi elle yazın. " +
                     "Aynı ağdaysanız yerel IP yeterlidir; internet üzerinden bağlanacaksanız Radmin VPN ya da port yönlendirme gerekir."),
            Margin = new Padding(2, 0, 2, 8)
        };

        var codeLabel = new Label
        {
            Text = T("Uzun katılım kodu (GTC1...):"),
            AutoSize = true,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            Margin = new Padding(2, 0, 2, 4)
        };

        _code.Name = "ShareCodeBox";
        _code.Multiline = true;
        _code.ReadOnly = true;
        _code.Dock = DockStyle.Fill;
        _code.ScrollBars = ScrollBars.Vertical;
        _code.WordWrap = true;
        _code.Font = new Font("Consolas", 9.5f);
        _code.BackColor = Color.WhiteSmoke;
        _code.Margin = new Padding(2, 0, 2, 6);

        _status.Name = "ShareStatus";
        _status.AutoSize = true;
        _status.ForeColor = Color.DimGray;
        _status.MaximumSize = new Size(630, 0);
        _status.Margin = new Padding(2, 0, 2, 6);

        var warning = new Label
        {
            AutoSize = true,
            ForeColor = WarningColor(),
            MaximumSize = new Size(630, 0),
            Text = BuildWarning(),
            Margin = new Padding(2, 0, 2, 8)
        };

        var buttons = Ui.Flow();
        buttons.Dock = DockStyle.Fill;
        buttons.Margin = new Padding(0);
        var copyBtn = Ui.Btn(T("Uzun Kodu Kopyala"), (_, _) => CopyCode());
        var refreshBtn = Ui.Btn(T("Kodu Yenile"), (_, _) => RefreshCode());
        var closeBtn = Ui.Btn(T("Kapat"), (_, _) => Close());
        buttons.Controls.Add(copyBtn);
        buttons.Controls.Add(refreshBtn);
        buttons.Controls.Add(closeBtn);

        root.Controls.Add(shortGroup, 0, 0);
        root.Controls.Add(addressLabel, 0, 1);
        root.Controls.Add(_address, 0, 2);
        root.Controls.Add(addressHint, 0, 3);
        root.Controls.Add(codeLabel, 0, 4);
        root.Controls.Add(_code, 0, 5);
        root.Controls.Add(_status, 0, 6);
        root.Controls.Add(warning, 0, 7);
        root.Controls.Add(buttons, 0, 8);
        Controls.Add(root);

        _address.TextChanged += (_, _) => RefreshCode();
        AcceptButton = _copyShortBtn.Enabled ? _copyShortBtn : copyBtn;
        CancelButton = closeBtn;

        RefreshCode();
    }

    private string ShortStatusText()
    {
        var code = _settings.ShortCode.Trim();
        if (code.Length == 0)
            return T("Kısa kod kapalı. 'Sunucum' sekmesindeki 'Kısa kod' alanına ör. Gel.com yazın.");
        if (!_serverRunning)
            return T("Sunucu kapalı: kısa kod ancak sunucu başlatılınca çalışır.");

        var text = !string.IsNullOrEmpty(_publishStatus)
            ? _publishStatus
            : T("Kısa kod yayınlanıyor... Arkadaşınız 'Kod ile Katıl' ekranına bu kodu yazsın.");

        if (_relay != null)
            text += "\r\n" + T("Otomatik relay hazır: {0} (sync {1}, oyun {2}). Port yönlendirme/Radmin gerekmez.",
                _relay.Host, _relay.SyncPort, _relay.GamePort);
        else if (_relayStatus.Length > 0)
            text += "\r\n" + _relayStatus;
        else
            text += "\r\n" + T("Otomatik relay başlatılıyor...");

        return text;
    }

    private Color ShortStatusColor()
    {
        if (string.IsNullOrWhiteSpace(_settings.ShortCode) || !_serverRunning)
            return Color.DimGray;
        if (_publishOk) return Ui.OkColor;
        return _publishStatus.Length > 0 ? Ui.WarnColor : Color.DimGray;
    }

    private string BuildWarning()
    {
        if (!string.IsNullOrWhiteSpace(_settings.ShortCode))
        {
            var text = T("Arkadaşınıza sadece kısa kodu gönderin; 'Sunucular' sekmesinde 'Kod ile Katıl...' deyip kodu yazsın. Kısa kod, sunucu bilgilerini şifreli olarak internet üzerinden yayınlar. Sunucu ve launcher açık olmalı. Otomatik relay sayesinde port yönlendirme veya Radmin VPN gerekmez.");
            text += "\r\n" + T("Kısa kodu yalnızca güvendiğiniz kişilerle paylaşın; kodu bilen sunucuya bağlanabilir.");

            if (!string.IsNullOrEmpty(_settings.Password) || !string.IsNullOrEmpty(_settings.SyncToken))
                text += "\r\n" + T("Şifre/token kısa kodun içinde şifreli taşınır.");

            return text;
        }

        return string.IsNullOrEmpty(_settings.Password) && string.IsNullOrEmpty(_settings.SyncToken)
            ? T("Bu kodu arkadaşınıza gönderin; 'Sunucular' sekmesinde 'Kod ile Katıl...' deyip yapıştırsın. Kod internet gerektirmez, bağlantı bilgilerini kendi taşır.")
            : T("DİKKAT: Kod, sunucu şifresini ve/veya sync token'ını içeriyor. Yalnızca güvendiğiniz kişilere verin.");
    }

    private Color WarningColor()
    {
        if (!string.IsNullOrWhiteSpace(_settings.ShortCode))
            return Color.DimGray;

        return string.IsNullOrEmpty(_settings.Password) && string.IsNullOrEmpty(_settings.SyncToken)
            ? Color.DimGray
            : Ui.WarnColor;
    }

    private string CurrentAddress()
    {
        var text = _address.Text.Trim();
        if (_address.SelectedItem is LocalAddress a &&
            string.Equals(a.ToString(), text, StringComparison.Ordinal))
            return a.Ip;

        // Kullanıcı elle yazdıysa ilk boşluğa kadar olan kısmı adres say.
        var space = text.IndexOf(' ');
        return space > 0 ? text[..space] : text;
    }

    private void RefreshCode()
    {
        // Relay, pencere açıldıktan sonra hazır olmuş olabilir; kodu
        // yenilerken güncel uç noktaları tekrar al.
        _relay = _relayProvider?.Invoke() ?? _relay;

        var host = CurrentAddress();
        if (host.Length == 0)
        {
            _code.Text = "";
            _status.ForeColor = Ui.WarnColor;
            _status.Text = T("Önce bir adres seçin veya yazın.");
            return;
        }

        var entry = new ServerEntry
        {
            Name = _settings.ServerName,
            Host = host,
            GamePort = _settings.GamePort,
            SyncPort = _settings.SyncPort,
            Password = _settings.Password,
            SyncToken = _settings.SyncToken,
            AutoConnect = true,
            ShortCode = ShortCodeDirectory.TryNormalize(
                _settings.ShortCode, out var normalizedShort, out _)
                ? normalizedShort
                : ""
        };

        if (_relay != null)
        {
            entry.RelayHost = _relay.Host;
            entry.RelaySyncPort = _relay.SyncPort;
            entry.RelayGamePort = _relay.GamePort;
        }

        _code.Text = JoinCode.Create(entry);
        _status.ForeColor = Color.DimGray;
        _status.Text = _relay != null
            ? T("Adres: {0}  |  Oyun portu: {1}  |  Sync portu: {2}\r\nOtomatik relay koda eklendi: {3} (sync {4}, oyun {5}). Arkadaşınız relay öncelikli bağlanır.",
                host, _settings.GamePort, _settings.SyncPort,
                _relay.Host, _relay.SyncPort, _relay.GamePort)
            : T("Adres: {0}  |  Oyun portu: {1}  |  Sync portu: {2}",
                host, _settings.GamePort, _settings.SyncPort);

        if (entry.ShortCode.Length > 0)
        {
            _status.Text += "\r\n" + T(
                "Kısa kod da koda eklendi ({0}); sunucu yeniden başlarsa relay bilgisi bağlanırken otomatik yenilenir.",
                entry.ShortCode);
        }

        if (_shortStatus.Text.Length > 0)
        {
            _shortStatus.Text = ShortStatusText();
            _shortStatus.ForeColor = ShortStatusColor();
        }
    }

    private void CopyCode()
    {
        if (string.IsNullOrEmpty(_code.Text)) return;
        try
        {
            Clipboard.SetText(_code.Text);
            _status.ForeColor = Ui.OkColor;
            _status.Text = T("Uzun kod kopyalandı ✓  (arkadaşınıza gönderin)");
        }
        catch (Exception ex)
        {
            Ui.Warn(this, T("Pano erişimi başarısız: ") + ex.Message);
        }
    }

    private void CopyShortCode()
    {
        var code = _settings.ShortCode.Trim();
        if (code.Length == 0) return;
        try
        {
            Clipboard.SetText(code);
            _shortStatus.ForeColor = Ui.OkColor;
            _shortStatus.Text = T("Kısa kod kopyalandı ✓ Arkadaşınıza gönderin.");
        }
        catch (Exception ex)
        {
            Ui.Warn(this, T("Pano erişimi başarısız: ") + ex.Message);
        }
    }
}
