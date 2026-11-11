using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using GTACoop.Remake.Wpf.Pages;

namespace GTACoop.Remake.Wpf;

public partial class MainWindow : Window
{
    private static readonly (string Title, string Hint)[] Sections =
    {
        ("Oyun", "GTA V klasörü, yedekleme ve başlatma ayarları buraya taşınacak."),
        ("Sunucular", "Kayıtlı sunucular ve hızlı bağlanma buraya taşınacak."),
        ("Sunucu Tarayıcı", "Genel sunucu listesi, LAN taraması ve filtreler buraya taşınacak."),
        ("Sunucum", "Kendi sunucunu başlatma, portlar ve kısa kod buraya taşınacak."),
        ("Tek Oyunculu", "Tek oyunculu mod paketleri buraya taşınacak."),
        ("Arayüz Tasarımı", "HUD tasarımcısı ve oyuncu bildirimleri buraya taşınacak."),
        ("Bileşenler", "SHVDN, ScriptHook ve bileşen kurulumları buraya taşınacak."),
        ("P2P", "Dosya paylaşımı ve kod üretimi buraya taşınacak."),
        ("Günlük", null!),
        ("Hakkında", null!)
    };

    private readonly Dictionary<string, FrameworkElement?> _pages = new();

    public MainWindow()
    {
        InitializeComponent();

        VersionText.Text = "Sürüm " + Version();
        SourceInitialized += (_, _) => TryDarkTitleBar();

        foreach (var section in Sections)
            Nav.Items.Add(new ListBoxItem { Content = section.Title });

        Nav.SelectedIndex = 0;
    }

    private static string Version()
    {
        var info = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString()
            ?? "?";
        return info.Split('+')[0];
    }

    private void OnNavChanged(object sender, SelectionChangedEventArgs e)
    {
        var index = Nav.SelectedIndex;
        if (index < 0 || index >= Sections.Length) return;

        var (title, hint) = Sections[index];
        Host.Content = Page(title, hint);
        StatusText.Text = title + " — hazır.";
    }

    private FrameworkElement Page(string title, string hint)
    {
        if (_pages.TryGetValue(title, out var cached) && cached != null)
            return cached;

        FrameworkElement page = title switch
        {
            "Günlük" => new LogPage(),
            "Hakkında" => new AboutPage(),
            _ => new PlaceholderPage(title, hint)
        };

        _pages[title] = page;
        return page;
    }

    // Koyu tema ile uyumlu başlık çubuğu (Windows 10 2004+ / Windows 11).
    [DllImport("dwmapi.dll")]
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
