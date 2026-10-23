using System.Windows.Forms;
using GTACoop.Sync;

namespace GTACoop.Remake;

/// <summary>
/// Sunucu sahibinin arkadaşına göndereceği katılım kodunu üretir.
/// Adres olarak bilgisayardaki IPv4 adreslerinden biri seçilir veya elle yazılır.
/// </summary>
public sealed class ShareCodeDialog : Form
{
    private readonly MyServerSettings _settings;
    private readonly ComboBox _address = new();
    private readonly TextBox _code = new();
    private readonly Label _status = new();

    public ShareCodeDialog(MyServerSettings settings)
    {
        _settings = settings;

        Text = T("Katılım Kodu Oluştur");
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96f, 96f);
        Width = 660;
        Height = 470;
        MinimumSize = new Size(560, 420);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 8,
            Padding = new Padding(12)
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int i = 0; i < 8; i++) root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles[4] = new RowStyle(SizeType.Percent, 100f); // kod kutusu kalan alanı doldurur

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
            MaximumSize = new Size(610, 0),
            Text = T("Listeden bir adres seçin (Radmin VPN 26.x, Wi-Fi/yerel IP) veya arkadaşınızın kullanacağı adresi elle yazın. " +
                     "Aynı ağdaysanız yerel IP yeterlidir; internet üzerinden bağlanacaksanız Radmin VPN ya da port yönlendirme gerekir."),
            Margin = new Padding(2, 0, 2, 8)
        };

        var codeLabel = new Label
        {
            Text = T("Katılım kodu:"),
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
        _status.MaximumSize = new Size(610, 0);
        _status.Margin = new Padding(2, 0, 2, 6);

        var warning = new Label
        {
            AutoSize = true,
            ForeColor = string.IsNullOrEmpty(_settings.Password) && string.IsNullOrEmpty(_settings.SyncToken)
                ? Color.DimGray
                : Ui.WarnColor,
            MaximumSize = new Size(610, 0),
            Text = string.IsNullOrEmpty(_settings.Password) && string.IsNullOrEmpty(_settings.SyncToken)
                ? T("Bu kodu arkadaşınıza gönderin; 'Sunucular' sekmesinde 'Kod ile Katıl...' deyip yapıştırsın. Kod internet gerektirmez, bağlantı bilgilerini kendi taşır.")
                : T("DİKKAT: Kod, sunucu şifresini ve/veya sync token'ını içeriyor. Yalnızca güvendiğiniz kişilere verin."),
            Margin = new Padding(2, 0, 2, 8)
        };

        var buttons = Ui.Flow();
        buttons.Dock = DockStyle.Fill;
        buttons.Margin = new Padding(0);
        var copyBtn = Ui.Btn(T("Kopyala"), (_, _) => CopyCode());
        var refreshBtn = Ui.Btn(T("Kodu Yenile"), (_, _) => RefreshCode());
        var closeBtn = Ui.Btn(T("Kapat"), (_, _) => Close());
        buttons.Controls.Add(copyBtn);
        buttons.Controls.Add(refreshBtn);
        buttons.Controls.Add(closeBtn);

        root.Controls.Add(addressLabel, 0, 0);
        root.Controls.Add(_address, 0, 1);
        root.Controls.Add(addressHint, 0, 2);
        root.Controls.Add(codeLabel, 0, 3);
        root.Controls.Add(_code, 0, 4);
        root.Controls.Add(_status, 0, 5);
        root.Controls.Add(warning, 0, 6);
        root.Controls.Add(buttons, 0, 7);
        Controls.Add(root);

        _address.TextChanged += (_, _) => RefreshCode();
        AcceptButton = copyBtn;
        CancelButton = closeBtn;

        RefreshCode();
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
            AutoConnect = true
        };

        _code.Text = JoinCode.Create(entry);
        _status.ForeColor = Color.DimGray;
        _status.Text = T("Adres: {0}  |  Oyun portu: {1}  |  Sync portu: {2}",
            host, _settings.GamePort, _settings.SyncPort);
    }

    private void CopyCode()
    {
        if (string.IsNullOrEmpty(_code.Text)) return;
        try
        {
            Clipboard.SetText(_code.Text);
            _status.ForeColor = Ui.OkColor;
            _status.Text = T("Kopyalandı ✓  (arkadaşınıza gönderin)");
        }
        catch (Exception ex)
        {
            Ui.Warn(this, T("Pano erişimi başarısız: ") + ex.Message);
        }
    }
}
