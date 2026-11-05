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

    private readonly ScrollPanel _scroll = new()
    {
        Dock = DockStyle.Fill,
        // Üst boşluk: içerik pencere başlık şeridinin altından başlasın.
        Padding = new Padding(12, 10, 12, 10)
    };
    private readonly ToolTip _tips = new();
    private readonly Label _updateStatus = Ui.StatusLbl();
    private readonly Button _updateBtn;
    private readonly Button _installBtn;

    public AboutTab()
    {
        Dock = DockStyle.Fill;
        // Sayfa tek bir kaydırılabilir kaba yığılır: pencere kısa olduğunda
        // hiçbir bölüm kesilmez, tamamı kaydırılarak görülür.

        // ---- Bilgi / lisans (kalan alanı doldurur)
        var infoGroup = new DarkGroup
        {
            Text = T("Lisans ve Bilgi"),
            Dock = DockStyle.None,
            Height = 142,
            Padding = new Padding(10, 22, 10, 6)
        };
        infoGroup.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            AutoSize = false,
            ForeColor = Color.DimGray,
            Text = T("GTACoop Remake, GTACoop (GTACoOp) açık kaynak projesini temel alan, GNU GPL v3 lisanslı ücretsiz bir araçtır; kaynak kodu GitHub'da açıktır.\r\nRockstar Games / Take-Two Interactive ile bağlantılı değildir; resmî bir ürün değildir. Orijinal GTA V gerektirir.\r\nÜcretsizdir: satılamaz, ticari amaçla dağıtılamaz.")
        });

        // ---- Bağlantılar
        var linksGroup = new DarkGroup
        {
            Text = T("Bağlantılar"),
            Dock = DockStyle.None,
            Height = 176,
            Padding = new Padding(10, 22, 10, 6)
        };
        var links = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
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

        // ---- Güncelleme
        var updateGroup = new DarkGroup
        {
            Text = T("Güncelleme"),
            Dock = DockStyle.None,
            Height = 104,
            Padding = new Padding(10, 22, 10, 6)
        };

        _updateStatus.Text = T("Sürüm {0}. Güncelleme kontrol ediliyor...", AppVersion());

        var updateRow = Ui.Flow(DockStyle.Top);
        updateRow.Controls.Add(_updateStatus);
        _updateBtn = Ui.Btn(T("Güncellemeyi Kontrol Et"), (_, _) => CheckForUpdates());
        updateRow.Controls.Add(_updateBtn);
        _installBtn = Ui.Btn(T("İndir ve Kur"), (_, _) => InstallUpdate());
        _installBtn.Visible = false;
        updateRow.Controls.Add(_installBtn);
        updateGroup.Controls.Add(updateRow);

        // ---- Üst bölüm: avatar + başlık
        var header = new TableLayoutPanel
        {
            Dock = DockStyle.None,
            Height = 218,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(0, 0, 0, 8)
        };
        // Tek satır, metinlerin toplam yüksekliğine sabit: başlık kesilmez.
        header.RowStyles.Add(new RowStyle(SizeType.Absolute, 210));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 178));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var avatar = new PictureBox
        {
            Size = new Size(164, 180),
            Anchor = AnchorStyles.Top | AnchorStyles.Left,
            SizeMode = PictureBoxSizeMode.Zoom,
            Image = LoadAvatar(),
            Margin = new Padding(0, 0, 12, 0)
        };

        var who = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Margin = new Padding(10, 0, 0, 0)
        };
        who.Controls.Add(new Label
        {
            Text = "GTACoop Remake",
            Font = new Font("Segoe UI", 18f, FontStyle.Bold),
            ForeColor = Theme.Text,
            AutoSize = true,
            Margin = new Padding(2, 6, 2, 0)
        });
        who.Controls.Add(new Label
        {
            Text = T("GTA V hikâye modu için arkadaş-arası co-op aracı (GTACoOp tabanlı)."),
            AutoSize = true,
            ForeColor = Theme.Muted,
            Margin = new Padding(4, 4, 2, 10)
        });
        who.Controls.Add(new Label
        {
            Text = T("Geliştirici: Reelone"),
            Font = new Font("Segoe UI", 11f, FontStyle.Bold),
            ForeColor = Theme.Text,
            AutoSize = true,
            Margin = new Padding(4, 0, 2, 4)
        });

        // ---- İlk test eden: fotoğraf + test eden bilgisi
        var testerRow = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(2, 2, 2, 6)
        };
        testerRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 76));
        testerRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        testerRow.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));

        var testerPhoto = new PictureBox
        {
            Size = new Size(64, 64),
            SizeMode = PictureBoxSizeMode.Zoom,
            Image = LoadTesterPhoto(),
            BorderStyle = BorderStyle.FixedSingle,
            Margin = new Padding(2, 0, 10, 0)
        };
        _tips.SetToolTip(testerPhoto, T("zaza specialist (Discord) - ilk test eden"));

        var testerName = new Label
        {
            Text = T("İlk test eden: zaza specialist (Discord)"),
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            ForeColor = Theme.Text,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0)
        };

        testerRow.Controls.Add(testerPhoto, 0, 0);
        testerRow.Controls.Add(testerName, 1, 0);
        who.Controls.Add(testerRow);

        who.Controls.Add(new Label
        {
            Text = T("Sürüm: {0}", AppVersion()),
            AutoSize = true,
            ForeColor = Theme.Muted,
            Margin = new Padding(4, 0, 2, 0)
        });

        header.Controls.Add(avatar, 0, 0);
        header.Controls.Add(who, 1, 0);

        // Bölümler sabit sırayla, hesaplanan konumlara yerleştirilir; böylece
        // hiçbir bölüm üst üste binmez ve üst kısım kesilmez.
        var body = new Panel { Dock = DockStyle.None, Width = 800 };
        body.Controls.Add(header);
        body.Controls.Add(updateGroup);
        body.Controls.Add(linksGroup);
        body.Controls.Add(infoGroup);
        _scroll.Controls.Add(body);
        Controls.Add(_scroll);

        void Reflow()
        {
            var width = Math.Max(420, _scroll.ClientSize.Width - _scroll.Padding.Horizontal);
            body.SuspendLayout();
            body.Width = width;
            var y = 0;
            foreach (var block in new Control[] { header, updateGroup, linksGroup, infoGroup })
            {
                block.Location = new Point(0, y);
                block.Width = width;
                y += block.Height + 10;
            }

            body.Height = y;
            body.ResumeLayout();
        }

        _scroll.Resize += (_, _) => Reflow();
        Reflow();

        // Açılışta sessiz sürüm kontrolü: kullanıcı sekmeyi açmasa da haber
        // vermek için arka planda çalışır, hata durumunda sessiz kalır.
        _ = AutoCheckAsync();
    }

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        if (!Visible) return;
        // Sayfa açılırken odak kaydırması üst kısmı kesmesin: yerleşim
        // tamamlandıktan sonra kaydırma konumu en üste alınır.
        BeginInvoke(new Action(() => _scroll.AutoScrollPosition = Point.Empty));
    }

    private static string AppVersion()
    {
        var v = typeof(AboutTab).Assembly.GetName().Version;
        return v == null ? "2.2.2" : $"{v.Major}.{v.Minor}.{v.Build}";
    }

    // ------------------------------------------------------------- güncelleme

    /// <summary>Açılışta çalışan sessiz kontrol; hiçbir pencere açmaz.</summary>
    private async Task AutoCheckAsync()
    {
        try
        {
            ApplyUpdateInfo(await SelfUpdater.CheckAsync(), silent: true);
        }
        catch
        {
            // Sessiz kontrol kullanıcıyı rahatsız etmez.
        }
    }

    private async void CheckForUpdates()
    {
        _updateBtn.Enabled = false;
        _updateStatus.ForeColor = Color.DimGray;
        _updateStatus.Text = T("Güncelleme kontrol ediliyor...");
        try
        {
            ApplyUpdateInfo(await SelfUpdater.CheckAsync(), silent: false);
        }
        finally
        {
            _updateBtn.Enabled = true;
        }
    }

    private void ApplyUpdateInfo(SelfUpdater.ReleaseInfo? info, bool silent)
    {
        var owner = FindForm();

        if (info == null)
        {
            _updateStatus.Text = T("Sürüm {0}. Güncelleme sunucusuna ulaşılamadı.", AppVersion());
            _installBtn.Visible = false;
            if (!silent && owner != null)
                Ui.Warn(owner, T("Güncelleme sunucusuna ulaşılamadı. İnternet bağlantınızı kontrol edin."));
            return;
        }

        if (!SelfUpdater.IsNewer(info))
        {
            _updateStatus.ForeColor = Ui.OkColor;
            _updateStatus.Text = T("Sürüm {0}. En son sürümü kullanıyorsunuz.", AppVersion());
            _installBtn.Visible = false;
            if (!silent && owner != null)
                Ui.Info(owner, T("En son sürümü kullanıyorsunuz ({0}).", AppVersion()));
            return;
        }

        _updateStatus.ForeColor = Ui.WarnColor;
        _updateStatus.Text = T("Yeni sürüm: {0} (kurulu {1}).", info.Tag, AppVersion());
        _installBtn.Text = T("Sürüm {0} - İndir ve Kur", info.Tag);
        _installBtn.Visible = !string.IsNullOrWhiteSpace(info.ExeUrl);

        if (!silent && owner != null)
            Ui.Info(owner, T("Yeni sürüm hazır: {0}\r\n\r\n'İndir ve Kur' ile güncelleyebilirsiniz.\r\nLauncher kapanıp yeniden açılır; oyun dosyalarınız, modlarınız ve ayarlarınız değişmez.", info.Name));
    }

    private async void InstallUpdate()
    {
        var info = SelfUpdater.Latest;
        var owner = FindForm();
        if (info == null || owner == null) return;

        if (!Ui.Confirm(owner, T("Sürüm {0} indirilip kurulacak.\r\nLauncher kapanıp yeniden açılacak; oyun dosyalarınız, modlarınız ve ayarlarınız değişmez. Devam edilsin mi?", info.Tag)))
            return;

        _installBtn.Enabled = false;
        _updateBtn.Enabled = false;
        var progress = new Progress<int>(p => _updateStatus.Text = T("Güncelleme indiriliyor... %{0}", p));

        try
        {
            var path = await SelfUpdater.DownloadAsync(info.ExeUrl, progress);
            _updateStatus.Text = T("Güncelleme kuruluyor...");
            SelfUpdater.ApplyAndRestart(path);
            Application.Exit();
        }
        catch (Exception ex)
        {
            AppEnv.LogError("Güncelleme kurulamadı: " + ex.Message);
            _installBtn.Enabled = true;
            _updateBtn.Enabled = true;
            _updateStatus.ForeColor = Ui.ErrColor;
            _updateStatus.Text = T("Güncelleme kurulamadı.");
            Ui.Error(owner, T("Güncelleme kurulamadı:\r\n") + ex.Message);
        }
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
    private static Image? LoadAvatar() => LoadEmbeddedImage("avatar.png");

    /// <summary>İlk test edenin fotoğrafı (Assets\tester.png).</summary>
    private static Image? LoadTesterPhoto() => LoadEmbeddedImage("tester.png");

    private static Image? LoadEmbeddedImage(string fileName)
    {
        try
        {
            var asm = typeof(AboutTab).Assembly;
            var name = Array.Find(asm.GetManifestResourceNames(),
                n => n.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));
            if (name == null) return null;

            using var stream = asm.GetManifestResourceStream(name);
            if (stream == null) return null;

            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            ms.Position = 0;

            // GDI+ görüntüyü akış kapanınca kullanamayabilir; kopyasını al.
            using var temp = Image.FromStream(ms);
            return new Bitmap(temp);
        }
        catch
        {
            return null;
        }
    }
}
