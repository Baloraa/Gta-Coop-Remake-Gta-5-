using System.Windows.Forms;
using GTACoop.Sync;

namespace GTACoop.Remake;

public sealed class MainForm : Form
{
    private readonly TextBox _gamePathBox = new();
    private readonly ToolStripStatusLabel _status = new();
    private readonly ComboBox _langCombo = new();
    private readonly TabControl _tabs = new();

    private readonly GameTab _gameTab = new();
    private readonly ComponentsTab _componentsTab = new();
    private readonly ServersTab _serversTab = new();
    private readonly MyServerTab _serverTab = new();
    private readonly LogTab _logTab = new();
    private readonly AboutTab _aboutTab = new();

    public MainForm()
    {
        // Yüksek DPI ekranlarda (125%, 150%...) arayüzün düzgün ölçeklenmesi için:
        // pencere ve içerik, ekran ölçeğine göre orantılı büyür; tuşlar kesilmez.
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96f, 96f);

        Text = T("GTACoop Remake — Mod ve Sunucu Yöneticisi");
        Width = 1150;
        Height = 870;
        MinimumSize = new Size(980, 660);
        Font = new Font("Segoe UI", 9f);
        StartPosition = FormStartPosition.CenterScreen;

        BuildHeader();
        BuildTabs();
        BuildStatusBar();

        AppEnv.LogMessage += OnLogMessage;
        FormClosing += (_, _) =>
        {
            AppEnv.Log("Launcher kapatılıyor.");
            _serverTab.ShutdownServer();
            AppEnv.SaveSettings();
        };
        Load += (_, _) =>
        {
            FitToScreen();
            RefreshGamePath();
            _gameTab.RefreshStatus();
            _serversTab.RecoverSessionIfAny();
        };

        _componentsTab.ComponentsChanged += _gameTab.RefreshStatus;
        _tabs.SelectedIndexChanged += (_, _) =>
        {
            _serverTab.RefreshStatus();
            if (_tabs.SelectedTab?.Controls.Contains(_componentsTab) == true)
                _componentsTab.OnShown();
        };
    }

    /// <summary>Pencereyi ekrana sığdır (yüksek DPI'da taşmasın).</summary>
    private void FitToScreen()
    {
        var wa = Screen.FromControl(this).WorkingArea;
        if (MinimumSize.Height > wa.Height - 40)
            MinimumSize = new Size(MinimumSize.Width, 360);
        if (Height > wa.Height - 20) Height = wa.Height - 20;
        if (Width > wa.Width - 20) Width = wa.Width - 20;
    }

    private void BuildHeader()
    {
        // Otomatik yerleşim: yazı tipleri büyüse bile öğeler üst üste binmez
        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 58,
            ColumnCount = 4,
            RowCount = 1,
            Padding = new Padding(10, 4, 10, 4)
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var title = new Label
        {
            Text = T("GTACoop Remake"),
            Font = new Font("Segoe UI", 14f, FontStyle.Bold),
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 0, 18, 0)
        };

        var gameLabel = new Label
        {
            Text = T("GTA V klasörü:"),
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 0, 4, 0)
        };

        _gamePathBox.ReadOnly = true;
        _gamePathBox.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        _gamePathBox.Margin = new Padding(0, 0, 8, 0);

        var headerButtons = new FlowLayoutPanel
        {
            AutoSize = true,
            WrapContents = false,
            FlowDirection = FlowDirection.LeftToRight,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0)
        };
        headerButtons.Controls.Add(Ui.Btn(T("Gözat..."), (_, _) => BrowseGameFolder()));
        headerButtons.Controls.Add(Ui.Btn(T("Kontrol Et"), (_, _) =>
        {
            _gameTab.RefreshStatus();
            _tabs.SelectedTab = _tabs.TabPages[0];
        }));

        headerButtons.Controls.Add(new Label
        {
            Text = T("Dil:"),
            AutoSize = true,
            Margin = new Padding(12, 8, 2, 0)
        });
        _langCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        _langCombo.Width = 110;
        _langCombo.Margin = new Padding(2, 5, 4, 4);
        _langCombo.Items.AddRange(new object[] { "Türkçe", "English" });
        _langCombo.SelectedIndex = Loc.English ? 1 : 0;
        _langCombo.SelectedIndexChanged += (_, _) => ChangeLanguage(_langCombo.SelectedIndex);
        headerButtons.Controls.Add(_langCombo);

        header.Controls.Add(title, 0, 0);
        header.Controls.Add(gameLabel, 1, 0);
        header.Controls.Add(_gamePathBox, 2, 0);
        header.Controls.Add(headerButtons, 3, 0);
        Controls.Add(header);
    }

    private void BuildTabs()
    {
        _tabs.Dock = DockStyle.Fill;

        AddTab(T("Oyun"), _gameTab);
        AddTab(T("Bileşenler"), _componentsTab);
        AddTab(T("Sunucular"), _serversTab);
        AddTab(T("Sunucum"), _serverTab);
        AddTab(T("Günlük / Araçlar"), _logTab);
        AddTab(T("Hakkında"), _aboutTab);

        Controls.Add(_tabs);
        _tabs.BringToFront();
    }

    private void AddTab(string title, UserControl control)
    {
        control.Dock = DockStyle.Fill;
        var page = new TabPage(title) { Padding = new Padding(8) };
        page.Controls.Add(control);
        _tabs.TabPages.Add(page);
    }

    private void BuildStatusBar()
    {
        var strip = new StatusStrip();
        _status.Text = T("Hazır");
        strip.Items.Add(_status);
        Controls.Add(strip);
        strip.BringToFront();
    }

    /// <summary>Dil değişince ayara yazılır; uygulanması için yeniden başlatma önerilir.</summary>
    private void ChangeLanguage(int index)
    {
        var newLang = index == 1 ? "en" : "tr";
        if (newLang == AppEnv.Settings.Language) return;

        AppEnv.Settings.Language = newLang;
        AppEnv.SaveSettings();

        var wasEnglish = Loc.English;
        Loc.English = newLang == "en";
        SyncText.English = Loc.English;
        var answer = MessageBox.Show(this,
            T("Dil değiştirildi. Uygulamak için launcher yeniden başlatılsın mı?"),
            T("Dil Değişikliği"), MessageBoxButtons.YesNo, MessageBoxIcon.Question);

        if (answer == DialogResult.Yes)
        {
            RestartRequested = true;
            Close();
        }
        else
        {
            Loc.English = wasEnglish; // seçim kaydedildi; sonraki açılışta geçerli olur
            SyncText.English = Loc.English;
        }
    }

    /// <summary>Launcher kapanırken true ise Program yeni örnek başlatır.</summary>
    public static bool RestartRequested { get; private set; }

    private void BrowseGameFolder()
    {
        using var dlg = new FolderBrowserDialog
        {
            Description = T("GTA V'in kurulu olduğu klasörü seçin (GTA5.exe'nin bulunduğu klasör)."),
            SelectedPath = AppEnv.Settings.GameFolder
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        if (!GameFolder.IsValid(dlg.SelectedPath))
        {
            if (!Ui.Confirm(this,
                T("Seçilen klasörde GTA5.exe bulunamadı. Yine de kullanmak istiyor musunuz?")))
                return;
        }

        AppEnv.Settings.GameFolder = dlg.SelectedPath;
        AppEnv.SaveSettings();
        AppEnv.Log($"GTA V klasörü ayarlandı: {dlg.SelectedPath}");
        RefreshGamePath();
        _gameTab.RefreshStatus();
    }

    public void RefreshGamePath() => _gamePathBox.Text = AppEnv.Settings.GameFolder;

    public void ShowComponentsTab()
    {
        var page = _tabs.TabPages.Cast<TabPage>()
            .FirstOrDefault(p => p.Controls.Contains(_componentsTab));
        if (page != null) _tabs.SelectedTab = page;
    }

    public void SetStatus(string text) => _status.Text = text;

    private void OnLogMessage(string line)
    {
        if (InvokeRequired)
        {
            try { BeginInvoke(() => OnLogMessage(line)); } catch { }
            return;
        }
        _logTab.Append(line);
    }

    /// <summary>Test edilebilirlik: oyun klasörü geçerli mi?</summary>
    public static bool RequireGameFolder(Form owner, out string gameFolder)
    {
        gameFolder = AppEnv.Settings.GameFolder;
        if (string.IsNullOrWhiteSpace(gameFolder) || !GameFolder.IsValid(gameFolder))
        {
            Ui.Warn(owner, T("Önce geçerli bir GTA V klasörü seçin (Oyun sekmesi)."));
            return false;
        }
        return true;
    }
}
