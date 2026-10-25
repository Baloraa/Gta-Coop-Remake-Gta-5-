using System.Windows.Forms;
using GTACoop.Sync;

namespace GTACoop.Remake;

/// <summary>Sunucu ekleme/düzenleme penceresi.</summary>
public sealed class ServerEditDialog : Form
{
    private readonly TextBox _name = new();
    private readonly TextBox _host = new();
    private readonly NumericUpDown _gamePort = new();
    private readonly NumericUpDown _syncPort = new();
    private readonly TextBox _password = new();
    private readonly TextBox _token = new();
    private readonly CheckBox _autoConnect = new();
    private readonly Label _testResult = new();

    public ServerEntry Entry { get; private set; }

    public ServerEditDialog(ServerEntry? existing = null)
    {
        Entry = existing ?? new ServerEntry();

        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96f, 96f);

        Text = existing == null ? T("Sunucu Ekle") : T("Sunucuyu Düzenle");
        Width = 480;
        Height = 400;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Segoe UI", 9f);

        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            Padding = new Padding(14),
            RowCount = 8
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        AddRow(table, T("Sunucu adı:"), _name, Entry.Name);
        AddRow(table, T("Sunucu adresi (IP):"), _host, Entry.Host);
        AddRow(table, T("Oyun portu:"), _gamePort, null, 1, 65535, Entry.GamePort);
        AddRow(table, T("Sync portu:"), _syncPort, null, 1, 65535, Entry.SyncPort);
        AddRow(table, T("Sunucu şifresi:"), _password, Entry.Password);
        AddRow(table, T("Sync token (varsa):"), _token, Entry.SyncToken);

        _autoConnect.Text = T("Oyun başlatılınca bu sunucuya otomatik bağlan");
        _autoConnect.Checked = Entry.AutoConnect;
        _autoConnect.AutoSize = false;
        _autoConnect.Width = 440;
        _autoConnect.Height = 24;
        table.Controls.Add(_autoConnect);
        table.SetColumnSpan(_autoConnect, 2);

        _testResult.AutoSize = true;
        _testResult.ForeColor = Color.DimGray;
        table.Controls.Add(new Label());
        table.Controls.Add(_testResult);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            Height = 46,
            Padding = new Padding(8)
        };
        var ok = new Button { Text = T("Kaydet"), DialogResult = DialogResult.OK, Width = 90, Height = 30 };
        var cancel = new Button { Text = T("İptal"), DialogResult = DialogResult.Cancel, Width = 90, Height = 30 };
        var test = new Button { Text = T("Bağlantıyı Test Et"), Width = 130, Height = 30 };
        test.Click += async (_, _) => await TestConnectionAsync();
        buttons.Controls.Add(ok);
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(test);

        Controls.Add(table);
        Controls.Add(buttons);
        AcceptButton = ok;
        CancelButton = cancel;

        ok.Click += (_, _) =>
        {
            var oldHost = Entry.Host;
            var oldGamePort = Entry.GamePort;
            var oldSyncPort = Entry.SyncPort;

            Entry.Name = string.IsNullOrWhiteSpace(_name.Text) ? _host.Text : _name.Text.Trim();
            Entry.Host = _host.Text.Trim();
            Entry.GamePort = (int)_gamePort.Value;
            Entry.SyncPort = (int)_syncPort.Value;
            Entry.Password = _password.Text;
            Entry.SyncToken = _token.Text.Trim();
            Entry.AutoConnect = _autoConnect.Checked;

            // Adres veya port değiştiyse eski relay uç noktaları artık bu
            // sunucuya ait değildir; yanlış yere bağlanmayı önle.
            if (!string.Equals(oldHost, Entry.Host, StringComparison.OrdinalIgnoreCase) ||
                oldGamePort != Entry.GamePort || oldSyncPort != Entry.SyncPort)
            {
                Entry.RelayHost = "";
                Entry.RelaySyncPort = 0;
                Entry.RelayGamePort = 0;
            }
        };
    }

    private static void AddRow(TableLayoutPanel table, string label, Control control,
        string? value, int numMin = 1, int numMax = 65535, int numValue = 1)
    {
        table.Controls.Add(new Label { Text = label, AutoSize = true, Padding = new Padding(0, 8, 0, 0) });

        if (control is TextBox tb)
        {
            tb.Text = value ?? "";
            tb.Width = 260;
            tb.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        }
        else if (control is NumericUpDown num)
        {
            num.Minimum = numMin;
            num.Maximum = numMax;
            num.Value = numValue >= numMin && numValue <= numMax ? numValue : numMin;
            num.Width = 100;
        }

        table.Controls.Add(control);
    }

    private async Task TestConnectionAsync()
    {
        _testResult.ForeColor = Color.DimGray;
        _testResult.Text = T("Bağlanılıyor...");
        try
        {
            var host = string.IsNullOrWhiteSpace(_host.Text) ? Entry.Host : _host.Text.Trim();
            var port = (int)_syncPort.Value;
            using var client = new SyncClient(host, port, _token.Text.Trim());
            var name = await client.PingAsync();
            _testResult.ForeColor = Ui.OkColor;
            _testResult.Text = T("✔ Bağlantı başarılı: {0}", name);
        }
        catch (Exception ex)
        {
            _testResult.ForeColor = Ui.ErrColor;
            _testResult.Text = "✘ " + ex.Message;
        }
    }
}
