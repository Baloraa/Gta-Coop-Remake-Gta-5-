using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms.Integration;
using System.Windows.Interop;
using GTACoop.Remake;
using GTACoop.Sync;
using Wf = System.Windows.Forms;

namespace GTACoop.Remake.Wpf;

public partial class MainWindow : Window
{
    private static readonly (string Key, string Title)[] Sections =
    {
        ("home", "Genel Bakış"),
        ("game", "Oyun"),
        ("servers", "Sunucular"),
        ("browser", "Sunucu Tarayıcı"),
        ("myserver", "Sunucum"),
        ("singleplayer", "Tek Oyunculu"),
        ("hud", "Arayüz Tasarımı"),
        ("components", "Bileşenler"),
        ("p2p", "P2P"),
        ("addonpeds", "Addon Peds"),
        ("log", "Günlük"),
        ("about", "Hakkında")
    };

    private readonly Dictionary<string, FrameworkElement> _pages = new();
    private readonly Dictionary<string, Wf.Form> _forms = new();
    private readonly List<Wf.Control> _controls = new();

    private ServersTab? _serversTab;
    private MyServerTab? _myServerTab;
    private AddonPedsTab? _addonPedsTab;
    private GameTab? _gameTab;
    private bool _sessionRecovered;
    private bool _languageReady;

    public MainWindow()
    {
        InitializeComponent();

        var version = typeof(AppEnv).Assembly.GetName().Version;
        VersionText.Text = Loc.T("Sürüm {0}", version == null ? "?" : version.ToString(3));
        TaglineText.Text = Loc.T("GTA V multiplayer companion");
        LanguageLabel.Text = Loc.T("Dil / Language");
        UnofficialText.Text = Loc.T("Unofficial community project");
        SourceInitialized += (_, _) => TryDarkTitleBar();
        Closing += (_, _) => Shutdown();

        MainForm.NavigationRequested += OnNavigationRequested;

        // Bölüm simgeleri: soldaki menüde her satır bir simgeyle başlar.
        var icons = new[] { "⌂", "▶", "☰", "◎", "◈", "✦", "▣", "⚙", "⇄", "☺", "✎", "ⓘ" };
        var i = 0;
        foreach (var (key, title) in Sections)
        {
            var icon = i < icons.Length ? icons[i] : "•";
            i++;
            Nav.Items.Add(new ListBoxItem { Content = icon + "   " + Loc.T(title), Tag = key });
        }

        SyncText.Language = Loc.Language;
        LangCombo.SelectionChanged -= OnLanguageChanged;
        LangCombo.Items.Add("Türkçe");
        LangCombo.Items.Add("English");
        LangCombo.Items.Add("Русский");
        LangCombo.SelectedIndex = Math.Clamp(Loc.LanguageIndex, 0, 2);
        LangCombo.SelectionChanged += OnLanguageChanged;
        _languageReady = true;

        // Pencere, ekranın altına taşıp alt satırları gizlemesin: doğrudan
        // WPF'in DIP cinsinden çalışma alanına sınırla.
        var wa = SystemParameters.WorkArea;
        MaxWidth = wa.Width;
        MaxHeight = wa.Height;
        if (Width > wa.Width - 20) Width = Math.Max(760, wa.Width - 20);
        if (Height > wa.Height - 20) Height = Math.Max(560, wa.Height - 20);
        Left = wa.Left + (wa.Width - Width) / 2;
        Top = wa.Top + Math.Max(0, (wa.Height - Height) / 2);
        Loaded += (_, _) => ClampToWorkArea();
        Nav.SelectedIndex = 0;
    }

    /// <summary>Pencere ekrandan taşmasın; içerik sağdan kesilmesin.</summary>
    private void ClampToWorkArea()
    {
        // Pencere 1000 piksele kadar büyüyüp ekranın altına taşıyordu; bu yüzden
        // alt satırlar görünmüyordu. Fiziksel çalışma alanı DIP'e çevrilip
        // pencere kesin olarak içine alınır.
        var handle = new WindowInteropHelper(this).Handle;
        var work = handle != IntPtr.Zero
            ? Wf.Screen.FromHandle(handle).WorkingArea
            : Wf.Screen.PrimaryScreen!.WorkingArea;

        var dip = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformFromDevice;
        var scaleX = dip?.M11 ?? 1.0;
        var scaleY = dip?.M22 ?? 1.0;
        var maxWidth = work.Width * scaleX - 20;
        var maxHeight = work.Height * scaleY - 20;

        MinWidth = Math.Min(MinWidth, maxWidth);
        MinHeight = Math.Min(MinHeight, maxHeight);
        if (Width > maxWidth) Width = maxWidth;
        if (Height > maxHeight) Height = maxHeight;

        Left = Math.Max(0, (work.Width * scaleX - Width) / 2 + work.Left * scaleX);
        Top = Math.Max(0, (work.Height * scaleY - Height) / 2 + work.Top * scaleY);
    }

    /// <summary>Dil seçimi: ayarı yazar ve onay verilirse launcher'ı yeniden başlatır.</summary>
    private void OnLanguageChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_languageReady) return;

        var newLang = LangCombo.SelectedIndex switch { 1 => "en", 2 => "ru", _ => "tr" };
        if (newLang == AppEnv.Settings.Language) return;

        AppEnv.Settings.Language = newLang;
        AppEnv.SaveSettings();

        var answer = System.Windows.MessageBox.Show(this,
            "Dil değiştirildi. Uygulamak için launcher yeniden başlatılsın mı?\n\n" +
            "Language changed. Restart the launcher to apply it?",
            "GTACoop Remake", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (answer != MessageBoxResult.Yes) return;

        try
        {
            var exe = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(exe))
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(exe)
                {
                    UseShellExecute = true,
                    WorkingDirectory = AppContext.BaseDirectory
                });
        }
        catch { }

        System.Windows.Application.Current.Shutdown();
    }

    private void OnNavChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Nav.SelectedItem is not ListBoxItem item || item.Tag is not string key) return;

        Host.Content = Page(key);
        var title = Loc.T(Sections.First(s => s.Key == key).Title);
        PageTitle.Text = title;
        PageSubtitle.Text = key == "home"
            ? Loc.T("Oturumunu ve araçlarını tek yerden yönet.")
            : "GTACoop Remake · " + title;
        StatusText.Text = string.Format(Loc.T("{0} — hazır."), title);

        if (key == "game") _gameTab?.RefreshStatus();
        if (key == "addonpeds") _addonPedsTab?.RefreshList();
        if (key == "servers" && !_sessionRecovered)
        {
            _sessionRecovered = true;
            _serversTab?.RecoverSessionIfAny();
        }
    }

    /// <summary>WinForms sekmelerinden gelen "şu bölümü aç" istekleri.</summary>
    private void OnNavigationRequested(string key)
    {
        Dispatcher.Invoke(() =>
        {
            var index = Array.FindIndex(Sections, s => s.Key == key);
            if (index >= 0) Nav.SelectedIndex = index;
        });
    }

    private FrameworkElement Page(string key)
    {
        if (_pages.TryGetValue(key, out var cached)) return cached;
        if (key == "home")
        {
            var dashboard = BuildHomePage();
            _pages[key] = dashboard;
            return dashboard;
        }

        var control = CreateControl(key);
        control.Dock = Wf.DockStyle.Fill;
        _controls.Add(control);

        // Sekmeler FindForm() ile iletişim kutularını açıyor; WPF içinde de
        // bir WinForms formu sahibi olsun diye görünmez bir kap kullanılır.
        var form = new Wf.Form
        {
            TopLevel = false,
            FormBorderStyle = Wf.FormBorderStyle.None,
            Dock = Wf.DockStyle.Fill,
            AutoScaleMode = Wf.AutoScaleMode.Dpi,
            AutoScaleDimensions = new System.Drawing.SizeF(96f, 96f),
            Size = new System.Drawing.Size(880, 700),
            BackColor = System.Drawing.Color.FromArgb(13, 13, 15)
        };
        form.Controls.Add(control);
        Theme.Apply(form);
        _forms[key] = form;

        var host = new WindowsFormsHost { Child = form };
        _pages[key] = host;
        return host;
    }


    private FrameworkElement BuildHomePage()
    {
        var root = new StackPanel { Orientation = System.Windows.Controls.Orientation.Vertical };

        var hero = new Border
        {
            Background = (System.Windows.Media.Brush)FindResource("SurfaceBrush"),
            BorderBrush = (System.Windows.Media.Brush)FindResource("BorderBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(24),
            Margin = new Thickness(0, 0, 0, 18)
        };
        var heroContent = new StackPanel();
        heroContent.Children.Add(new TextBlock
        {
            Text = Loc.T("Birlikte oynayın. Dünyayı paylaşın."),
            FontSize = 25,
            FontWeight = FontWeights.SemiBold,
            Foreground = (System.Windows.Media.Brush)FindResource("TextBrush"),
            Margin = new Thickness(0, 0, 0, 8)
        });
        heroContent.Children.Add(new TextBlock
        {
            Text = Loc.T("GTA V co-op oturumlarını, sunucularını ve modlarını GTACoop Remake üzerinden yönet."),
            FontSize = 14,
            Foreground = (System.Windows.Media.Brush)FindResource("MutedBrush"),
            Margin = new Thickness(0, 0, 0, 18)
        });
        var heroActions = new WrapPanel();
        heroActions.Children.Add(CreateDashboardButton(Loc.T("Oyuna Git"), "game"));
        heroActions.Children.Add(CreateDashboardButton(Loc.T("Sunucularım"), "servers"));
        heroActions.Children.Add(CreateDashboardButton(Loc.T("Sunucu Bul"), "browser"));
        heroContent.Children.Add(heroActions);
        hero.Child = heroContent;
        root.Children.Add(hero);

        var cards = new WrapPanel();
        cards.Children.Add(CreateDashboardCard(
            Loc.T("Sunucuya Katıl"), Loc.T("Arkadaşının sunucusuna bağlan veya kayıtlı sunucularını yönet."),
            Loc.T("Sunucuları Aç"), "servers"));
        cards.Children.Add(CreateDashboardCard(
            Loc.T("Kendi Sunucun"), Loc.T("Kendi co-op oturumunu oluştur, ayarlarını düzenle ve arkadaşlarını davet et."),
            Loc.T("Sunucumu Yönet"), "myserver"));
        cards.Children.Add(CreateDashboardCard(
            Loc.T("Modlar ve İçerik"), Loc.T("Addon ped paketlerini ve sunucu içeriklerini yönet."),
            Loc.T("Addon Peds"), "addonpeds"));
        cards.Children.Add(CreateDashboardCard(
            Loc.T("HUD Tasarımcısı"), Loc.T("Oyun içi bildirim ve arayüz düzenini kişiselleştir."),
            Loc.T("Tasarımı Aç"), "hud"));
        root.Children.Add(cards);

        root.Children.Add(new TextBlock
        {
            Text = Loc.T("İpucu: Sunucuya katılmadan önce oyun klasörünün ve modlarının doğru algılandığını Oyun sekmesinden kontrol et."),
            Foreground = (System.Windows.Media.Brush)FindResource("MutedBrush"),
            Margin = new Thickness(2, 18, 2, 0),
            TextWrapping = TextWrapping.Wrap
        });

        return new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = root
        };
    }

    private System.Windows.Controls.Button CreateDashboardButton(string label, string destination)
    {
        var button = new System.Windows.Controls.Button
        {
            Content = label,
            MinWidth = 112,
            Margin = new Thickness(0, 0, 8, 0),
            Padding = new Thickness(14, 9, 14, 9)
        };
        button.Click += (_, _) => NavigateTo(destination);
        return button;
    }

    private Border CreateDashboardCard(string title, string description, string action, string destination)
    {
        var card = new Border
        {
            Background = (System.Windows.Media.Brush)FindResource("SurfaceBrush"),
            BorderBrush = (System.Windows.Media.Brush)FindResource("BorderBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(16),
            Margin = new Thickness(0, 0, 12, 12),
            Width = 270,
            MinHeight = 166
        };
        var content = new StackPanel();
        content.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 7)
        });
        content.Children.Add(new TextBlock
        {
            Text = description,
            Foreground = (System.Windows.Media.Brush)FindResource("MutedBrush"),
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 52,
            Margin = new Thickness(0, 0, 0, 12)
        });
        var button = new System.Windows.Controls.Button
        {
            Content = action,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
            Padding = new Thickness(12, 7, 12, 7)
        };
        button.Click += (_, _) => NavigateTo(destination);
        content.Children.Add(button);
        card.Child = content;
        return card;
    }

    private void NavigateTo(string key)
    {
        var index = Array.FindIndex(Sections, s => s.Key == key);
        if (index >= 0) Nav.SelectedIndex = index;
    }

    private Wf.Control CreateControl(string key)
    {
        switch (key)
        {
            case "game": return _gameTab ??= new GameTab();
            case "servers": return _serversTab ??= new ServersTab();
            case "browser": return new ServerBrowserTab(_serversTab ??= new ServersTab());
            case "myserver": return _myServerTab ??= new MyServerTab();
            case "singleplayer": return new SingleplayerTab();
            case "hud": return new HudDesignerTab();
            case "components": return new ComponentsTab();
            case "p2p": return new P2PTab();
            case "addonpeds": return _addonPedsTab ??= new AddonPedsTab();
            case "log": return new LogTab();
            default: return new AboutTab();
        }
    }

    private void Shutdown()
    {
        MainForm.NavigationRequested -= OnNavigationRequested;

        try { _myServerTab?.ShutdownServer(); } catch { }
        try
        {
            AppEnv.Log("Launcher kapatılıyor.");
            AppEnv.SaveSettings();
        }
        catch { }

        foreach (var form in _forms.Values)
        {
            try { form.Dispose(); } catch { }
        }

        _forms.Clear();
        _pages.Clear();
        _controls.Clear();
    }

    // Koyu tema ile uyumlu başlık çubuğu (Windows 10 2004+ / Windows 11).
    [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    private void TryDarkTitleBar()
    {
        try
        {
            var handle = new WindowInteropHelper(this).Handle;
            var on = 1;
            if (DwmSetWindowAttribute(handle, 20, ref on, sizeof(int)) != 0)
                DwmSetWindowAttribute(handle, 19, ref on, sizeof(int));
        }
        catch
        {
            // Eski Windows sürümlerinde başlık çubuğu sistem renginde kalır.
        }
    }
}
