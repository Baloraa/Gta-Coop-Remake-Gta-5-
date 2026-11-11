using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace GTACoop.Remake.Wpf.Pages;

/// <summary>Hakkında sayfası: sürüm, lisans metni ve gerçek bağlantılar.</summary>
public sealed class AboutPage : StackPanel
{
    private static readonly (string Label, string Url)[] Links =
    {
        ("YouTube kanalım: @Reelone123", "https://www.youtube.com/@Reelone123"),
        ("X (Twitter): @imp_loyal38310", "https://x.com/imp_loyal38310"),
        ("gta5-mods profilim: Turkish Mods", "https://www.gta5-mods.com/users/Turkish%20Mods"),
        ("GTACoop kaynak kodu (GitHub)", "https://github.com/oldnapalm/GTACoop")
    };

    public AboutPage()
    {
        VerticalAlignment = VerticalAlignment.Top;

        Children.Add(new TextBlock
        {
            Text = "GTACoop Remake",
            Style = (Style)Application.Current.Resources["H1"],
            Margin = new Thickness(0, 0, 0, 4)
        });

        Children.Add(new TextBlock
        {
            Text = "GTA V hikâye modu için arkadaş-arası co-op aracı · Sürüm " + Version(),
            Style = (Style)Application.Current.Resources["Muted"],
            Margin = new Thickness(0, 0, 0, 18)
        });

        var links = new Border { Style = (Style)Application.Current.Resources["Card"], Margin = new Thickness(0, 0, 0, 12) };
        var linksPanel = new StackPanel();
        linksPanel.Children.Add(new TextBlock
        {
            Text = "Bağlantılar",
            Style = (Style)Application.Current.Resources["H2"],
            Margin = new Thickness(0, 0, 0, 8)
        });

        foreach (var (label, url) in Links)
        {
            var link = new TextBlock { Margin = new Thickness(0, 2, 0, 2) };
            var hyperlink = new Hyperlink(new Run(label)) { NavigateUri = new Uri(url) };
            hyperlink.RequestNavigate += (_, e) => Open(e.Uri.AbsoluteUri);
            link.Inlines.Add(hyperlink);
            linksPanel.Children.Add(link);
        }

        links.Child = linksPanel;
        Children.Add(links);

        var license = new Border { Style = (Style)Application.Current.Resources["Card"] };
        license.Child = new TextBlock
        {
            Style = (Style)Application.Current.Resources["Muted"],
            Text = "GTACoop Remake, GTACoop (GTACoOp) açık kaynak projesini temel alan, GNU GPL v3 " +
                   "lisanslı ücretsiz bir araçtır; kaynak kodu GitHub'da açıktır.\n\n" +
                   "Rockstar Games / Take-Two Interactive ile bağlantılı değildir; resmî bir ürün " +
                   "değildir. Orijinal GTA V gerektirir. Ücretsizdir: satılamaz, ticari amaçla dağıtılamaz."
        };
        Children.Add(license);
    }

    private static string Version()
    {
        var info = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString()
            ?? "?";
        return info.Split('+')[0];
    }

    private static void Open(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch
        {
            // Tarayıcı açılamazsa bağlantı sessizce yok sayılır.
        }
    }
}
