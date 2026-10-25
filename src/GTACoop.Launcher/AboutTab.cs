using System.Diagnostics;
using System.Windows.Forms;

namespace GTACoop.Remake;

/// <summary>Hakkında sekmesi: avatar, geliştirici bağlantıları ve lisans bilgisi.</summary>
public sealed class AboutTab : UserControl
{
    private const string UrlGta5Mods = "https://www.gta5-mods.com/users/Turkish%20Mods";
    private const string UrlYouTube = "https://www.youtube.com/@Reelone123";
    private const string UrlX = "https://x.com/imp_loyal38310";
    private const string UrlGitHub = "https://github.com/oldnapalm/GTACoop";

    private readonly ToolTip _tips = new();

    public AboutTab()
    {
        Dock = DockStyle.Fill;
        Padding = new Padding(14);

        // ---- Bilgi / lisans (kalan alanı doldurur)
        var infoGroup = new GroupBox
        {
            Text = T("Lisans ve Bilgi"),
            Dock = DockStyle.Fill,
            Padding = new Padding(10, 6, 10, 6)
        };
        infoGroup.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            AutoSize = false,
            ForeColor = Color.DimGray,
            Text = T("GTACoop Remake, GTACoop (GTACoOp) açık kaynak projesini temel alan, GNU GPL v3 lisanslı ücretsiz bir araçtır; kaynak kodu GitHub'da açıktır.\r\nRockstar Games / Take-Two Interactive ile bağlantılı değildir; resmî bir ürün değildir. Orijinal GTA V gerektirir.\r\nÜcretsizdir: satılamaz, ticari amaçla dağıtılamaz.")
        });

        // ---- Bağlantılar
        var linksGroup = new GroupBox
        {
            Text = T("Bağlantılar"),
            Dock = DockStyle.Top,
            Height = 162,
            Padding = new Padding(10, 6, 10, 6)
        };
        var links = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = false,
            Padding = new Padding(2)
        };
        links.Controls.Add(MakeLink(T("YouTube kanalım: @Reelone123"), UrlYouTube));
        links.Controls.Add(MakeLink(T("X (Twitter): @imp_loyal38310"), UrlX));
        links.Controls.Add(MakeLink(T("gta5-mods profilim: Turkish Mods"), UrlGta5Mods));
        links.Controls.Add(MakeLink(T("GTACoop kaynak kodu (GitHub)"), UrlGitHub));
        linksGroup.Controls.Add(links);

        // ---- Üst bölüm: avatar + başlık
        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 200,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(0, 0, 0, 8)
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 178));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var avatar = new PictureBox
        {
            Size = new Size(164, 180),
            SizeMode = PictureBoxSizeMode.Zoom,
            Image = LoadAvatar(),
            Margin = new Padding(0, 0, 12, 0)
        };

        var who = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false
        };
        who.Controls.Add(new Label
        {
            Text = "GTACoop Remake",
            Font = new Font("Segoe UI", 18f, FontStyle.Bold),
            AutoSize = true,
            Margin = new Padding(2, 6, 2, 0)
        });
        who.Controls.Add(new Label
        {
            Text = T("GTA V hikâye modu için arkadaş-arası co-op aracı (GTACoOp tabanlı)."),
            AutoSize = true,
            ForeColor = Color.DimGray,
            Margin = new Padding(4, 4, 2, 10)
        });
        who.Controls.Add(new Label
        {
            Text = T("Geliştirici: Turkish Mods"),
            Font = new Font("Segoe UI", 11f, FontStyle.Bold),
            AutoSize = true,
            Margin = new Padding(4, 0, 2, 4)
        });
        who.Controls.Add(new Label
        {
            Text = T("Sürüm: {0}", AppVersion()),
            AutoSize = true,
            ForeColor = Color.DimGray,
            Margin = new Padding(4, 0, 2, 0)
        });

        header.Controls.Add(avatar, 0, 0);
        header.Controls.Add(who, 1, 0);

        Controls.Add(infoGroup);
        Controls.Add(linksGroup);
        Controls.Add(header);
    }

    private static string AppVersion()
    {
        var v = typeof(AboutTab).Assembly.GetName().Version;
        return v == null ? "2.2.1" : $"{v.Major}.{v.Minor}.{v.Build}";
    }

    private LinkLabel MakeLink(string text, string url)
    {
        var link = new LinkLabel
        {
            Text = text,
            AutoSize = true,
            Font = new Font("Segoe UI", 10f),
            Margin = new Padding(4, 4, 4, 2)
        };
        _tips.SetToolTip(link, url);
        link.LinkClicked += (_, _) => OpenUrl(url);
        return link;
    }

    private void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            AppEnv.Log("Bağlantı açıldı: " + url);
        }
        catch (Exception ex)
        {
            AppEnv.LogError("Bağlantı açılamadı: " + ex.Message);
            var owner = FindForm();
            if (owner != null) Ui.Warn(owner, T("Bağlantı açılamadı: ") + url);
        }
    }

    /// <summary>Avatar, derlemeye gömülü kaynaktan yüklenir (Assets\avatar.png).</summary>
    private static Image? LoadAvatar()
    {
        try
        {
            var asm = typeof(AboutTab).Assembly;
            var name = Array.Find(asm.GetManifestResourceNames(),
                n => n.EndsWith("avatar.png", StringComparison.OrdinalIgnoreCase));
            if (name == null) return null;

            using var stream = asm.GetManifestResourceStream(name);
            if (stream == null) return null;

            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            ms.Position = 0;
            return Image.FromStream(ms);
        }
        catch
        {
            return null;
        }
    }
}
