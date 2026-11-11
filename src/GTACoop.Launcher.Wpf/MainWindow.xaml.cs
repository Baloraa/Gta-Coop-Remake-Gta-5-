using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms.Integration;
using System.Windows.Interop;
using GTACoop.Remake;
using Wf = System.Windows.Forms;

namespace GTACoop.Remake.Wpf;

public partial class MainWindow : Window
{
    private static readonly (string Key, string Title)[] Sections =
    {
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
    private bool _sessionRecovered;

    public MainWindow()
    {
        InitializeComponent();

        var version = typeof(AppEnv).Assembly.GetName().Version;
        VersionText.Text = Loc.T("Sürüm {0}", version == null ? "?" : version.ToString(3));
        SourceInitialized += (_, _) => TryDarkTitleBar();
        Closing += (_, _) => Shutdown();

        MainForm.NavigationRequested += OnNavigationRequested;

        foreach (var (key, title) in Sections)
            Nav.Items.Add(new ListBoxItem { Content = Loc.T(title), Tag = key });

        Nav.SelectedIndex = 0;
    }

    private void OnNavChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Nav.SelectedItem is not ListBoxItem item || item.Tag is not string key) return;

        Host.Content = Page(key);
        StatusText.Text = item.Content + " — hazır.";

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

    private Wf.Control CreateControl(string key)
    {
        switch (key)
        {
            case "game": return new GameTab();
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
