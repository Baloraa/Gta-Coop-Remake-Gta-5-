using System.Windows.Forms;
using GTACoop.Sync;

namespace GTACoop.Remake;

public sealed class MainForm : Form
{
    private readonly TextBox _gamePathBox = new();
    private readonly ToolStripStatusLabel _status = new();
    private readonly ComboBox _langCombo = new();
    private readonly TabControl _tabs = new();
    private readonly FlowLayoutPanel _navBar = new();
    private readonly Panel _content = new();
    private Control? _headerPanel;
    private readonly Dictionary<TabPage, NavButton> _navButtons = new();
    private readonly Dictionary<TabPage, Control> _pageContent = new();
    private TabPage? _currentPage;

    private readonly GameTab _gameTab = new();
    private readonly ComponentsTab _componentsTab = new();
    private readonly ServersTab _serversTab = new();
    private readonly ServerBrowserTab _serverBrowserTab;
    private readonly MyServerTab _serverTab = new();
    private readonly SingleplayerTab _singleplayerTab = new();
    private readonly LogTab _logTab = new();
    private readonly AboutTab _aboutTab = new();
    private readonly P2PTab _p2pTab = new();
    private readonly HudDesignerTab _hudTab = new();
    private AddonPedsTab? _addonPedsTab;
    private TabPage? _addonPedsPage;

    public MainForm()
    {
        _serverBrowserTab = new ServerBrowserTab(_serversTab);

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
            Theme.Apply(this);
            // İçerik, üst şeridin altından başlasın: aksi halde her sayfanın
            // ilk satırları şeridin arkasında kalıyordu.
            var headerHeight = _headerPanel?.Height ?? 58;
            _content.Padding = new Padding(10, headerHeight + 6, 10, 8);
            FitToScreen();
            RefreshGamePath();
            _gameTab.RefreshStatus();
            _serversTab.RecoverSessionIfAny();
        };

        _componentsTab.ComponentsChanged += _gameTab.RefreshStatus;
    }

    /// <summary>"Sunucum" sekmesindeki Addon Peds düğmesinden ayrı sekmeyi açar.</summary>
    public void ShowAddonPedsTab()
    {
        if (_addonPedsPage == null)
        {
            _addonPedsTab = new AddonPedsTab();
            var tabIndex = Math.Max(0, _tabs.TabPages.Count - 2);
            _addonPedsPage = AddTab(T("Addon Peds"), _addonPedsTab, tabIndex);
        }

        _addonPedsTab!.RefreshList();
        SelectPage(_addonPedsPage);
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
            SelectPage(_tabs.TabPages[0]);
        }));

        headerButtons.Controls.Add(new Label
        {
            Text = T("Dil:"),
            AutoSize = true,
            Margin = new Padding(12, 8, 2, 0)
        });
        _langCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        _langCombo.Width = 120;
        _langCombo.Margin = new Padding(2, 5, 4, 4);
        _langCombo.Items.AddRange(new object[] { "Türkçe", "English", "Русский" });
        _langCombo.SelectedIndex = Loc.LanguageIndex;
        _langCombo.SelectedIndexChanged += (_, _) => ChangeLanguage(_langCombo.SelectedIndex);
        headerButtons.Controls.Add(_langCombo);

        header.Controls.Add(title, 0, 0);
        header.Controls.Add(gameLabel, 1, 0);
        header.Controls.Add(_gamePathBox, 2, 0);
        header.Controls.Add(headerButtons, 3, 0);
        _headerPanel = header;
        Controls.Add(header);
    }

    private void BuildTabs()
    {
        AddTab(T("Oyun"), _gameTab);
        AddTab(T("Bileşenler"), _componentsTab);
        AddTab(T("Sunucular"), _serversTab);
        AddTab(T("Sunucu Tarayıcı"), _serverBrowserTab);
        AddTab(T("Sunucum"), _serverTab);
        AddTab(T("P2P Paylaşım"), _p2pTab);
        AddTab(T("Arayüz Tasarımı"), _hudTab);
        AddTab(T("Tek Oyunculu"), _singleplayerTab);
        AddTab(T("Günlük / Araçlar"), _logTab);
        AddTab(T("Hakkında"), _aboutTab);

        // Gerçek TabControl hiç gösterilmez: yalnızca sekme listesini tutar.
        // Böylece hiçbir yerde sistemin beyaz şeridi kalmaz; üstte düğmeli
        // bir gezinme çubuğu ve altında tek bir içerik paneli kullanılır.
        _tabs.Visible = false;

        // Solda dikey gezinme çubuğu, sağında tek içerik paneli.
        _navBar.Dock = DockStyle.Left;
        _navBar.Width = 214;
        _navBar.FlowDirection = FlowDirection.TopDown;
        _navBar.WrapContents = false;
        _navBar.AutoScroll = true;
        _navBar.BackColor = Theme.Surface;
        _navBar.Padding = new Padding(8, 10, 8, 8);

        _content.Dock = DockStyle.Fill;
        _content.BackColor = Theme.Background;
        _content.Padding = new Padding(10, 8, 10, 8);

        Controls.Add(_content);
        Controls.Add(_navBar);

        RebuildNav();
        if (_tabs.TabPages.Count > 0) SelectPage(_tabs.TabPages[0]);
    }

    /// <summary>Sekmeyi seçer, içeriğini gösterir ve sekmeye özel tazelemeleri yapar.</summary>
    private void SelectPage(TabPage? page)
    {
        if (page == null) return;
        _currentPage = page;

        // Sahne arkasındaki TabControl yalnızca kayıt tutar; görünür bir
        // ebeveyni olmadığı için olaylarına güvenilmez.
        if (_tabs.TabPages.Contains(page))
        {
            try { _tabs.SelectedTab = page; } catch { }
        }

        ShowPage(page);
        UpdateNav();
        _serverTab.RefreshStatus();

        if (!_pageContent.TryGetValue(page, out var control)) return;
        if (ReferenceEquals(control, _componentsTab)) _componentsTab.OnShown();
        else if (ReferenceEquals(control, _serverBrowserTab)) _serverBrowserTab.OnShown();
        else if (_addonPedsTab != null && ReferenceEquals(control, _addonPedsTab))
            _addonPedsTab.RefreshList();
    }

    /// <summary>Sayfanın içeriğini tek içerik panelinde gösterir.</summary>
    private void ShowPage(TabPage page)
    {
        if (!_pageContent.TryGetValue(page, out var control)) return;

        _content.SuspendLayout();
        _content.Controls.Clear();
        control.Dock = DockStyle.Fill;
        // Sayfa ilk kez gösterilirken temaya uyarlanır: sekmeler görünür
        // ağaçta olmadığı için açılışta taranamıyorlar.
        Theme.Apply(control);
        _content.Controls.Add(control);
        // Sayfa her açılışta en üstten görünsün (odak kaydırması üstü kesmesin).
        if (control is ScrollableControl scrollable)
            scrollable.AutoScrollPosition = new Point(0, 0);
        _content.ResumeLayout();
    }

    /// <summary>Üstteki gezinme düğmelerini sekme listesine göre yeniden kurar.</summary>
    private void RebuildNav()
    {
        _navBar.SuspendLayout();
        foreach (Control old in _navBar.Controls.Cast<Control>().ToList()) old.Dispose();
        _navBar.Controls.Clear();
        _navButtons.Clear();

        var icons = new[] { "▶", "⚙", "☰", "◎", "⌂", "⇄", "▣", "✦", "✎", "ⓘ" };
        var index = 0;
        foreach (TabPage page in _tabs.TabPages)
        {
            var icon = index < icons.Length ? icons[index] : "\u2022";
            index++;
            var button = new NavButton(page.Text) { Font = new Font(Font.FontFamily, 9.5f), Icon = icon };
            var target = page;
            button.Click += (_, _) => SelectPage(target);

            _navBar.Controls.Add(button);
            _navButtons[page] = button;
        }

        _navBar.ResumeLayout();
        UpdateNav();
    }

    /// <summary>Etkin sekmeyi yeşil vurgular.</summary>
    private void UpdateNav()
    {
        foreach (var pair in _navButtons)
            pair.Value.Active = ReferenceEquals(_currentPage, pair.Key);
    }

    /// <summary>Gezinme çubuğunu koyu temaya geçirir (içerik yerleşimi değişmez).</summary>
    public void ApplyTabStyle()
    {
        _navBar.BackColor = Theme.Surface;
        _content.BackColor = Theme.Background;
        RebuildNav();
        UpdateNav();
    }

    private TabPage AddTab(string title, UserControl control, int index = -1)
    {
        control.Dock = DockStyle.Fill;
        var page = new TabPage(title) { Padding = new Padding(8), UseVisualStyleBackColor = false };
        if (Theme.Enabled)
        {
            page.BackColor = Theme.Background;
            page.ForeColor = Theme.Text;
            Theme.Apply(control);
        }

        // İçerik doğrudan sayfaya değil, seçimde gösterilecek panele eklenir.
        _pageContent[page] = control;

        if (index >= 0 && index <= _tabs.TabPages.Count)
            _tabs.TabPages.Insert(index, page);
        else
            _tabs.TabPages.Add(page);

        if (_navBar.IsHandleCreated || _navButtons.Count > 0)
            RebuildNav();

        return page;
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
        var newLang = index switch { 1 => "en", 2 => "ru", _ => "tr" };
        if (newLang == AppEnv.Settings.Language) return;

        AppEnv.Settings.Language = newLang;
        AppEnv.SaveSettings();

        var previous = Loc.Language;
        Loc.Language = newLang;
        SyncText.Language = newLang;
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
            Loc.Language = previous; // seçim kaydedildi; sonraki açılışta geçerli olur
            SyncText.Language = previous;
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

    public void ShowComponentsTab() => SelectPageByContent(_componentsTab);

    public void ShowServersTab() => SelectPageByContent(_serversTab);

    /// <summary>
    /// İçeriği gösterilen sayfayı seçer. Sekme sayfaları içeriklerini
    /// barındırmadığı (içerik ayrı panelde tutulduğu) için eşleşme sayfa
    /// listesinden yapılır; önceki sürümde bu düğmeler hiç çalışmıyordu.
    /// </summary>
    private void SelectPageByContent(Control content)
    {
        foreach (var pair in _pageContent)
        {
            if (!ReferenceEquals(pair.Value, content)) continue;
            SelectPage(pair.Key);
            return;
        }
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
