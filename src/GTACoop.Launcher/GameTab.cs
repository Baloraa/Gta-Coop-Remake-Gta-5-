using System.Windows.Forms;
using GTACoop.Sync;

namespace GTACoop.Remake;

public sealed class GameTab : UserControl
{
    private readonly TableLayoutPanel _table = new();
    private readonly Label _summary = new();
    private readonly Label _backupInfo = new();
    private readonly Label _launchInfo = new();
    private readonly Button _clientBtn;
    private readonly TextBox _playerName = new();

    public GameTab()
    {
        Dock = DockStyle.Fill;

        _table.Dock = DockStyle.Top;
        _table.ColumnCount = 2;
        _table.AutoSize = true;
        _table.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 260));
        _table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var statusGroup = new GroupBox
        {
            Text = T("Oyun Durumu"),
            Dock = DockStyle.Top,
            Height = 272,
            Padding = new Padding(10)
        };
        statusGroup.Controls.Add(_table);

        _summary.Dock = DockStyle.Top;
        _summary.AutoSize = false;
        _summary.Height = 24;
        _summary.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);

        _backupInfo.Dock = DockStyle.Top;
        _backupInfo.AutoSize = false;
        _backupInfo.Height = 22;
        _backupInfo.ForeColor = Color.DimGray;

        _launchInfo.Dock = DockStyle.Top;
        _launchInfo.AutoSize = false;
        _launchInfo.Height = 22;
        _launchInfo.AutoEllipsis = true;
        _launchInfo.ForeColor = Color.DimGray;

        var buttons = Ui.Flow();
        buttons.Controls.Add(Ui.Btn(T("Durumu Yenile"), (_, _) => RefreshStatus()));
        _clientBtn = Ui.Btn(T("İstemciyi Kur / Güncelle"), (_, _) => InstallClient());
        buttons.Controls.Add(_clientBtn);
        buttons.Controls.Add(Ui.Btn(T("Bileşenleri Yönet..."), (_, _) => (FindForm() as MainForm)?.ShowComponentsTab()));
        buttons.Controls.Add(Ui.Btn(T("Oyunu Başlat"), (_, _) => LaunchGame()));

        // Çalıştırma dosyası seçimi ana buton satırını taşırmasın diye ayrı satırda.
        var launchButtons = Ui.Flow();
        launchButtons.Controls.Add(Ui.Btn(T("Çalıştırma Yolu..."), (_, _) => ChooseLaunchExe()));
        launchButtons.Controls.Add(Ui.Btn(T("Varsayılana Dön"), (_, _) => ResetLaunchExe()));

        // --- oyuncu adı
        var playerGroup = new GroupBox
        {
            Text = T("Oyuncu"),
            Dock = DockStyle.Top,
            Height = 100,
            Padding = new Padding(10, 6, 10, 6)
        };

        _playerName.Width = 220;
        _playerName.MaxLength = 24;
        _playerName.Margin = new Padding(4, 6, 8, 4);
        _playerName.Text = AppEnv.Settings.Username;

        var playerRow = Ui.Flow();
        playerRow.Controls.Add(Ui.Lbl(T("Oyuncu adı:")));
        playerRow.Controls.Add(_playerName);
        playerRow.Controls.Add(Ui.Btn(T("Kaydet"), (_, _) => SavePlayerName()));

        var playerRow2 = Ui.Flow();
        playerRow2.Controls.Add(new Label
        {
            Text = T("Sunucularda ve oyun içi listelerde görünecek adınız (en fazla 24 karakter)."),
            AutoSize = true,
            ForeColor = Color.DimGray,
            Margin = new Padding(4, 2, 4, 0)
        });

        playerGroup.Controls.Add(playerRow2);
        playerGroup.Controls.Add(playerRow);

        var info = new Label
        {
            Dock = DockStyle.Fill,
            Text = T("Oyun dosyalarınız değiştirilmez; sunucu modları scripts/ ve mods/ içine kurulur, çıkışta kaldırılır."),
            ForeColor = Color.DimGray,
            TextAlign = ContentAlignment.TopLeft,
            Padding = new Padding(4, 2, 4, 0)
        };

        // Yerleşim: durum -> özet -> çalıştırma yolu -> yedek -> butonlar -> oyuncu -> bilgi
        Controls.Add(info);
        Controls.Add(playerGroup);
        Controls.Add(buttons);
        Controls.Add(_backupInfo);
        Controls.Add(launchButtons);
        Controls.Add(_launchInfo);
        Controls.Add(_summary);
        Controls.Add(statusGroup);
    }

    public void RefreshStatus()
    {
        if (!_playerName.Focused)
            _playerName.Text = AppEnv.Settings.Username;

        _table.Controls.Clear();
        _table.RowStyles.Clear();

        var gameFolder = AppEnv.Settings.GameFolder;
        if (string.IsNullOrWhiteSpace(gameFolder))
        {
            _summary.Text = T("⚠ GTA V klasörü seçilmedi (üstteki 'Gözat...' düğmesini kullanın).");
            _summary.ForeColor = Ui.WarnColor;
            _backupInfo.Text = T("");
            _launchInfo.Text = T("Çalıştırma dosyası: GTA V klasörü seçilmedi.");
            _launchInfo.ForeColor = Ui.WarnColor;
            _clientBtn.Enabled = false;
            return;
        }

        var s = GameFolder.Inspect(gameFolder);
        AddRow("GTA5.exe", s.HasGameExe, s.HasGameExe ? T("var") : T("YOK"));
        AddRow("GTA5_Enhanced.exe", s.HasEnhancedExe, s.HasEnhancedExe ? T("var") : T("yok"));
        AddRow("PlayGTAV.exe", s.HasPlayGtaVExe, s.HasPlayGtaVExe ? T("var") : T("yok"));
        AddRow("ScriptHookV.dll", s.HasScriptHookV, s.HasScriptHookV ? T("var") : T("YOK - oyun modlanamaz"));
        AddRow("ScriptHookVDotNet.asi", s.HasShvdn, s.HasShvdn ? T("var") : T("YOK - scriptler çalışmaz"));
        AddRow(T("OpenIV.asi / RageOpenV.asi"), s.HasOpenIvAsi, s.HasOpenIvAsi ? T("var") : T("YOK - mods klasörü okunmaz"));
        AddRow("mods/update/update.rpf", s.HasModsUpdateRpf,
            s.HasModsUpdateRpf ? T("var") : T("YOK - dlc paketleri kurulamaz"));
        AddRow(T("scripts klasörü"), s.HasScriptsFolder, s.HasScriptsFolder ? T("var") : T("yok (oluşturulur)"));
        AddRow(T("GTACoOp.dll (istemci)"), s.HasGtaCoopClient, s.HasGtaCoopClient ? T("kurulu") : T("kurulu değil"));

        var missing = new List<string>();
        if (!s.HasGameExe) missing.Add("GTA5.exe");
        if (!s.HasScriptHookV) missing.Add("ScriptHookV");
        if (!s.HasShvdn) missing.Add("ScriptHookVDotNet");
        if (!s.HasOpenIvAsi) missing.Add(T("OpenIV.asi / RageOpenV.asi"));

        if (missing.Count == 0)
        {
            _summary.Text = T("✔ Oyun mod için hazır.");
            _summary.ForeColor = Ui.OkColor;
        }
        else
        {
            _summary.Text = T("⚠ Eksikler: ") + string.Join(", ", missing);
            _summary.ForeColor = Ui.WarnColor;
        }

        var backups = BackupService.ListUpdateRpfBackups(gameFolder);
        _backupInfo.Text = backups.Count == 0
            ? T("update.rpf yedeği: henüz yok (ilk dlclist düzenlemesinde otomatik alınır).")
            : T("update.rpf yedeği: {0} adet (en yeni: {1}).", backups.Count, Path.GetFileName(backups[0]));

        var configured = GameFolder.ResolveConfiguredLaunchExe(gameFolder, AppEnv.Settings.LaunchExe);
        var launchExe = configured ?? GameFolder.DefaultLaunchExe(gameFolder);
        var suffix = configured == null ? T(" (varsayılan)") : T(" (özel)");
        _launchInfo.Text = T("Çalıştırma dosyası: {0}{1}", launchExe, suffix);
        _launchInfo.ForeColor = File.Exists(launchExe) ? Color.DimGray : Ui.ErrColor;

        _clientBtn.Enabled = s.IsUsable;
    }

    private void AddRow(string name, bool ok, string detail)
    {
        var nameLabel = new Label
        {
            Text = name,
            AutoSize = true,
            Padding = new Padding(2, 2, 2, 2)
        };
        var valueLabel = new Label
        {
            Text = (ok ? "✔ " : "✘ ") + detail,
            AutoSize = true,
            ForeColor = ok ? Ui.OkColor : Ui.ErrColor,
            Padding = new Padding(2, 2, 2, 2)
        };
        _table.Controls.Add(nameLabel);
        _table.Controls.Add(valueLabel);
    }

    private void SavePlayerName()
    {
        var name = _playerName.Text.Trim();
        if (name.Length == 0)
        {
            Ui.Warn(FindForm()!, T("Oyuncu adı boş olamaz."));
            _playerName.Text = AppEnv.Settings.Username;
            return;
        }
        if (name.Length > 24) name = name[..24];

        AppEnv.Settings.Username = name;
        AppEnv.SaveSettings();
        AppEnv.Log($"Oyuncu adı kaydedildi: {name}");

        var gameFolder = AppEnv.Settings.GameFolder;
        if (!string.IsNullOrWhiteSpace(gameFolder) && GameFolder.IsValid(gameFolder))
            ClientSettingsEditor.SetUsername(gameFolder, name, AppEnv.Log);

        _playerName.Text = name;
        Ui.Info(FindForm()!,
            T("Oyuncu adı kaydedildi: {0}\r\nSunucuya 'Bağlan ve Oyna' dediğinizde bu ad kullanılır.", name));
    }

    private void InstallClient()
    {
        if (!MainForm.RequireGameFolder(FindForm()!, out var gameFolder)) return;

        try
        {
            var result = ClientInstaller.InstallClient(gameFolder, AppEnv.Log);
            Ui.Info(FindForm()!,
                T("İstemci kurulumu tamam.\r\n\r\nKopyalanan: {0}\r\nAtlanan: {1}", result.Copied, result.Skipped));
            RefreshStatus();
        }
        catch (Exception ex)
        {
            AppEnv.LogError(ex.Message);
            Ui.Error(FindForm()!, T("İstemci kurulamadı:\n") + ex.Message);
        }
    }

    private void ChooseLaunchExe()
    {
        var gameFolder = AppEnv.Settings.GameFolder;
        if (string.IsNullOrWhiteSpace(gameFolder) || !Directory.Exists(gameFolder))
        {
            Ui.Warn(FindForm()!, T("Önce GTA V klasörünü seçin."));
            return;
        }

        var current = GameFolder.ResolveLaunchExe(gameFolder, AppEnv.Settings.LaunchExe);
        using var dlg = new OpenFileDialog
        {
            Title = T("GTA V çalıştırma dosyasını seçin"),
            Filter = T("Çalıştırılabilir dosya (*.exe)|*.exe"),
            InitialDirectory = gameFolder,
            FileName = Path.GetFileName(current),
            CheckFileExists = true,
            Multiselect = false
        };
        if (dlg.ShowDialog(FindForm()) != DialogResult.OK) return;

        var selected = Path.GetFullPath(dlg.FileName);
        if (!selected.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) || !File.Exists(selected))
        {
            Ui.Warn(FindForm()!, T("Seçilen çalıştırma dosyası bulunamadı:\r\n{0}", selected));
            return;
        }

        var root = Path.GetFullPath(gameFolder)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        AppEnv.Settings.LaunchExe = selected.StartsWith(root, StringComparison.OrdinalIgnoreCase)
            ? Path.GetRelativePath(gameFolder, selected)
            : selected;
        AppEnv.SaveSettings();
        AppEnv.Log($"Çalıştırma dosyası ayarlandı: {selected}");
        RefreshStatus();
    }

    private void ResetLaunchExe()
    {
        AppEnv.Settings.LaunchExe = "";
        AppEnv.SaveSettings();
        AppEnv.Log("Varsayılan çalıştırma dosyası PlayGTAV.exe olarak ayarlandı.");
        RefreshStatus();
    }

    private void LaunchGame()
    {
        if (!MainForm.RequireGameFolder(FindForm()!, out var gameFolder)) return;

        if (AddonApplier.IsGameRunning())
        {
            Ui.Warn(FindForm()!, T("GTA V zaten açık."));
            return;
        }

        try
        {
            GameLauncher.Launch(gameFolder, AppEnv.Settings.LaunchExe, AppEnv.Log);
        }
        catch (Exception ex)
        {
            AppEnv.LogError(ex.Message);
            Ui.Error(FindForm()!, T("Oyun başlatılamadı:\n") + ex.Message);
        }
    }
}
