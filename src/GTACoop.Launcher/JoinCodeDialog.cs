using System.Windows.Forms;
using GTACoop.Sync;

namespace GTACoop.Remake;

/// <summary>
/// Arkadaşın gönderdiği katılım kodunu yapıştırıp sunucuyu tek adımda ekleme penceresi.
/// Kod, bağlantı bilgilerini kendi içinde taşır; internet/sunucu gerekmez.
/// </summary>
public sealed class JoinCodeDialog : Form
{
    private readonly TextBox _code = new();
    private readonly Label _summary = new();
    private readonly Button _addBtn;
    private readonly Button _connectBtn;
    private ServerEntry? _parsed;

    /// <summary>Çözülen sunucu kaydı (yalnızca OK ile kapanırsa dolu).</summary>
    public ServerEntry? Entry { get; private set; }

    /// <summary>Kullanıcı "Ekle ve Bağlan" seçti mi?</summary>
    public bool ConnectRequested { get; private set; }

    public JoinCodeDialog()
    {
        Text = T("Katılım Kodu");
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96f, 96f);
        Width = 620;
        Height = 440;
        MinimumSize = new Size(520, 380);
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
            MaximumSize = new Size(560, 0),
            Text = T("İpucu: Kodu sunucu sahibi 'Sunucum' sekmesinde 'Katılım Kodu Oluştur...' ile üretir. " +
                     "Kod sunucu şifresini/token'ını içerebilir; yalnızca güvendiğiniz kişilerle paylaşın."),
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

        // Panoda hazır bir kod varsa otomatik doldur (kopyala → launcher → yapıştır gerekmesin).
        TryFillFromClipboard();
    }

    private void TryFillFromClipboard()
    {
        try
        {
            if (!Clipboard.ContainsText()) return;
            var text = Clipboard.GetText().Trim();
            if (text.StartsWith(JoinCode.Prefix, StringComparison.OrdinalIgnoreCase))
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

    private void Decode()
    {
        if (string.IsNullOrWhiteSpace(_code.Text))
        {
            _parsed = null;
            _summary.ForeColor = Color.DimGray;
            _summary.Text = T("Kod yapıştırıldığında sunucu bilgileri burada görünür.");
            _addBtn.Enabled = _connectBtn.Enabled = false;
            return;
        }

        if (JoinCode.TryParse(_code.Text, out var entry, out var error) && entry != null)
        {
            _parsed = entry;
            _summary.ForeColor = Ui.OkColor;
            _summary.Text = T("Sunucu: {0}\r\nAdres: {1}\r\nOyun portu: {2}  |  Sync portu: {3}\r\nŞifre: {4}",
                entry.Name, entry.Host, entry.GamePort, entry.SyncPort,
                string.IsNullOrEmpty(entry.Password) ? T("yok") : T("var"));
            _addBtn.Enabled = _connectBtn.Enabled = true;
        }
        else
        {
            _parsed = null;
            _summary.ForeColor = Ui.ErrColor;
            _summary.Text = T("Geçersiz kod: {0}", error);
            _addBtn.Enabled = _connectBtn.Enabled = false;
        }
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
