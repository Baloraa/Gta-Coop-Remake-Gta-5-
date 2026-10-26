using System.Diagnostics;
using System.Windows.Forms;
using GTACoop.Sync;

namespace GTACoop.Remake;

/// <summary>
/// ScriptHookV / ScriptHookVDotNet / OpenIV.asi (paket içi) / RageOpenV alternatifini
/// kurar. Yalnızca bilinen dosyaları değiştirir; mevcut modları taramaz veya silmez.
/// </summary>
public sealed class ComponentsTab : UserControl
{
    private readonly ComboBox _component = new();
    private readonly ComboBox _channel = new();
    private readonly ComboBox _version = new();
    private readonly Label _source = new();
    private readonly Label _installed = new();
    private readonly Label _backup = new();
    private readonly Label _status = new();
    private readonly ProgressBar _progress = new();
    private readonly TextBox _details = new();
    private readonly Button _fetchBtn;
    private readonly Button _installBtn;
    private readonly Button _restoreBtn;
    private readonly Button _openBackupBtn;
    private readonly FlowLayoutPanel _channelRow;
    private readonly Label _channelLabel;
    private readonly List<ComponentRelease> _releases = new();
    private bool _busy;

    /// <summary>Kurulum/geri yükleme sonrası Oyun sekmesinin durumu yenilenir.</summary>
    public event Action? ComponentsChanged;

    public ComponentsTab()
    {
        Dock = DockStyle.Fill;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 8,
            Padding = new Padding(8)
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // açıklama
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // seçim
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // kaynak
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // düğmeler
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 118));  // kurulu
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // ayrıntı başlığı
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));   // ayrıntılar
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // ilerleme

        var info = new Label
        {
            Text = T("Bileşenler yalnızca oyun köküne kurulur: ScriptHookV.dll, ScriptHookVDotNet.asi/DLL'leri ve isteğe bağlı OpenIV.asi veya RageOpenV alternatifi. NativeTrainer.asi asla kurulmaz. scripts/ ve mods/ klasörleriniz silinmez; değiştirilen dosyalar önce yedeklenir."),
            AutoSize = true,
            MaximumSize = new Size(1020, 0),
            ForeColor = Color.DimGray,
            Margin = new Padding(4, 2, 4, 8)
        };
        root.Controls.Add(info, 0, 0);

        // --- seçim grubu
        var selectionGroup = new GroupBox
        {
            Text = T("Bileşen Yöneticisi"),
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(10),
            Margin = new Padding(4, 2, 4, 4)
        };

        var form = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Margin = new Padding(0)
        };

        var componentRow = Row();
        componentRow.Controls.Add(new Label
        {
            Text = T("Bileşen:"),
            AutoSize = true,
            Margin = new Padding(2, 8, 10, 4)
        });
        _component.DropDownStyle = ComboBoxStyle.DropDownList;
        _component.Width = 440;
        _component.Margin = new Padding(2, 5, 4, 4);
        _component.Items.Add(new ComponentOption(ComponentKind.ScriptHookV, T("ScriptHookV (resmî)")));
        _component.Items.Add(new ComponentOption(ComponentKind.ScriptHookVDotNet, T("ScriptHookVDotNet (stable / nightly / enhanced)")));
        _component.Items.Add(new ComponentOption(ComponentKind.OpenIV, T("OpenIV.asi (paket içi)")));
        _component.Items.Add(new ComponentOption(ComponentKind.OpenIvAlternative, T("RageOpenV (alternatif)")));
        _component.SelectedIndex = 0;
        componentRow.Controls.Add(_component);
        form.Controls.Add(componentRow);

        _channelRow = Row();
        _channelLabel = new Label
        {
            Text = T("Kanal:"),
            AutoSize = true,
            Margin = new Padding(2, 8, 10, 4)
        };
        _channel.DropDownStyle = ComboBoxStyle.DropDownList;
        _channel.Width = 200;
        _channel.Margin = new Padding(2, 5, 8, 4);
        _channel.Items.Add(new ChannelOption(ShvdnChannel.Stable, T("Kararlı")));
        _channel.Items.Add(new ChannelOption(ShvdnChannel.Nightly, T("Gecelik (nightly)")));
        _channel.Items.Add(new ChannelOption(ShvdnChannel.Enhanced, T("Enhanced v3")));
        _channel.SelectedIndex = 0;
        _channelRow.Controls.Add(_channelLabel);
        _channelRow.Controls.Add(_channel);
        _channelRow.Controls.Add(new Label
        {
            Text = T("Stable resmî sürüm; nightly günlük derleme; Enhanced Legacy + Enhanced uyumlu."),
            AutoSize = true,
            ForeColor = Color.DimGray,
            Margin = new Padding(2, 8, 4, 2)
        });
        form.Controls.Add(_channelRow);

        var versionRow = Row();
        versionRow.Controls.Add(new Label
        {
            Text = T("Sürüm:"),
            AutoSize = true,
            Margin = new Padding(2, 8, 10, 4)
        });
        _version.DropDownStyle = ComboBoxStyle.DropDownList;
        _version.Width = 430;
        _version.Margin = new Padding(2, 5, 4, 4);
        _fetchBtn = Ui.Btn(T("Sürümleri Getir"), (_, _) => _ = LoadReleasesAsync(forceRefresh: true));
        _fetchBtn.Margin = new Padding(8, 4, 4, 4);
        versionRow.Controls.Add(_version);
        versionRow.Controls.Add(_fetchBtn);
        form.Controls.Add(versionRow);

        _source.AutoSize = true;
        _source.MaximumSize = new Size(820, 0);
        _source.ForeColor = Color.DimGray;
        _source.Margin = new Padding(2, 6, 4, 2);

        selectionGroup.Controls.Add(form);
        root.Controls.Add(selectionGroup, 0, 1);

        root.Controls.Add(_source, 0, 2);

        var actions = Ui.Flow();
        _installBtn = Ui.Btn(T("İndir ve Kur"), (_, _) => _ = InstallAsync());
        _installBtn.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
        _installBtn.Enabled = false;
        _restoreBtn = Ui.Btn(T("Son Yedeği Geri Yükle"), (_, _) => RestoreLatestBackup());
        _restoreBtn.Enabled = false;
        _openBackupBtn = Ui.Btn(T("Yedek Klasörünü Aç"), (_, _) => OpenBackupFolder());
        actions.Controls.Add(_installBtn);
        actions.Controls.Add(_restoreBtn);
        actions.Controls.Add(_openBackupBtn);
        root.Controls.Add(actions, 0, 3);

        // --- kurulu durum
        var installedGroup = new GroupBox
        {
            Text = T("Kurulu Bileşen"),
            Dock = DockStyle.Fill,
            Padding = new Padding(10),
            Margin = new Padding(4, 4, 4, 4)
        };
        var installedPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3
        };
        installedPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        installedPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        installedPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        installedPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _installed.AutoSize = true;
        _installed.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
        _installed.Margin = new Padding(4, 4, 4, 2);
        _backup.AutoSize = false;
        _backup.Height = 20;
        _backup.Dock = DockStyle.Fill;
        _backup.AutoEllipsis = true;
        _backup.ForeColor = Color.DimGray;
        _backup.Margin = new Padding(4, 2, 4, 2);
        _status.AutoSize = true;
        _status.ForeColor = Color.DimGray;
        _status.Margin = new Padding(4, 2, 4, 2);
        installedPanel.Controls.Add(_installed, 0, 0);
        installedPanel.Controls.Add(_backup, 0, 1);
        installedPanel.Controls.Add(_status, 0, 2);
        installedGroup.Controls.Add(installedPanel);
        root.Controls.Add(installedGroup, 0, 4);

        root.Controls.Add(new Label
        {
            Text = T("İşlem Ayrıntıları"),
            AutoSize = true,
            ForeColor = Color.DimGray,
            Margin = new Padding(4, 6, 4, 2)
        }, 0, 5);

        _details.Dock = DockStyle.Fill;
        _details.Multiline = true;
        _details.ReadOnly = true;
        _details.ScrollBars = ScrollBars.Vertical;
        _details.Font = new Font("Consolas", 8.5f);
        _details.BackColor = Color.White;
        _details.Margin = new Padding(4, 2, 4, 4);
        root.Controls.Add(_details, 0, 6);

        _progress.Dock = DockStyle.Top;
        _progress.Height = 18;
        _progress.Minimum = 0;
        _progress.Maximum = 100;
        _progress.Style = ProgressBarStyle.Continuous;
        _progress.Margin = new Padding(4, 2, 4, 2);
        root.Controls.Add(_progress, 0, 7);

        Controls.Add(root);

        _component.SelectedIndexChanged += (_, _) => SelectionChanged();
        _channel.SelectedIndexChanged += (_, _) => SelectionChanged();
        _version.SelectedIndexChanged += (_, _) => UpdateInstallButton();

        SelectionChanged(load: false);
    }

    private ComponentKind SelectedKind =>
        (_component.SelectedItem as ComponentOption)?.Kind ?? ComponentKind.ScriptHookV;

    private ShvdnChannel SelectedChannel =>
        SelectedKind == ComponentKind.ScriptHookVDotNet
            ? (_channel.SelectedItem as ChannelOption)?.Channel ?? ShvdnChannel.Stable
            : ShvdnChannel.Stable;

    /// <summary>Sekme ilk kez açıldığında sürümleri (önbellekten) yükler.</summary>
    public void OnShown()
    {
        if (_busy) return;
        if (_version.Items.Count == 0)
            _ = LoadReleasesAsync(forceRefresh: false);
        else
        {
            UpdateInstalledInfo();
            UpdateInstallButton();
        }
    }

    private void SelectionChanged(bool load = true)
    {
        if (_busy) return;

        var showChannel = SelectedKind == ComponentKind.ScriptHookVDotNet;
        _channelLabel.Visible = showChannel;
        _channelRow.Visible = showChannel;
        _channel.Enabled = showChannel;

        _releases.Clear();
        _version.Items.Clear();
        UpdateSourceHint();
        UpdateInstalledInfo();
        UpdateInstallButton();

        if (load && Visible)
            _ = LoadReleasesAsync(forceRefresh: false);
    }

    private async Task LoadReleasesAsync(bool forceRefresh)
    {
        if (_busy) return;
        _busy = true;
        SetBusy(true);
        _status.Text = T("Sürüm listesi alınıyor...");
        _progress.Style = ProgressBarStyle.Marquee;
        _progress.MarqueeAnimationSpeed = 25;

        try
        {
            var kind = SelectedKind;
            var channel = SelectedChannel;
            var releases = await ComponentManager.GetReleasesAsync(
                kind, channel, AppEnv.DataFolder, forceRefresh, default, AppEnv.Log);

            _releases.Clear();
            _releases.AddRange(releases);
            PopulateVersions();
            _status.Text = T("Sürüm listesi alındı: {0}", _releases.Count);
            AppEnv.Log($"Bileşen listesi: {kind}/{channel}, {_releases.Count} sürüm.");
        }
        catch (Exception ex)
        {
            AppEnv.LogError("Bileşen sürümleri alınamadı: " + ex.Message);
            _status.Text = T("Sürüm listesi alınamadı.");
            AppendDetail(T("Sürüm listesi alınamadı: ") + ex.Message);
            if (forceRefresh && FindForm() is { } form)
                Ui.Warn(form, T("Sürüm listesi alınamadı:\r\n") + ex.Message);
        }
        finally
        {
            _busy = false;
            _progress.Style = ProgressBarStyle.Continuous;
            _progress.MarqueeAnimationSpeed = 0;
            _progress.Value = 0;
            SetBusy(false);
            UpdateInstalledInfo();
            UpdateInstallButton();
        }
    }

    private void PopulateVersions()
    {
        _version.Items.Clear();
        var installed = ComponentManager.GetInstalled(SelectedKind, AppEnv.DataFolder);
        var gameFolder = AppEnv.Settings.GameFolder;
        var intact = installed != null &&
                     !string.IsNullOrWhiteSpace(gameFolder) &&
                     GameFolder.IsValid(gameFolder) &&
                     ComponentManager.IsIntact(installed, gameFolder);

        foreach (var release in _releases)
        {
            var text = release.Version;
            if (release.Prerelease) text += " (" + T("ön sürüm") + ")";
            if (installed != null &&
                installed.Tag.Equals(release.Tag, StringComparison.OrdinalIgnoreCase) &&
                installed.Channel == release.Channel)
            {
                text += " — " + (intact ? T("kurulu") : T("onarım gerekli"));
            }
            _version.Items.Add(new ReleaseOption(release, text));
        }

        if (_version.Items.Count > 0)
            _version.SelectedIndex = 0;
    }

    private async Task InstallAsync()
    {
        if (_busy) return;
        if (_version.SelectedItem is not ReleaseOption option)
        {
            Ui.Warn(FindForm()!, T("Önce bir bileşen sürümü seçin."));
            return;
        }
        if (!MainForm.RequireGameFolder(FindForm()!, out var gameFolder)) return;
        if (AddonApplier.IsGameRunning())
        {
            Ui.Warn(FindForm()!, T("GTA V açıkken bileşen kurulamaz. Önce oyunu kapatın."));
            return;
        }

        var release = option.Release;
        var message = T(
            "Aşağıdaki bileşen oyun köküne kurulacak:\r\n\r\n{0}  ({1})\r\nKaynak: {2}\r\n\r\nDeğiştirilecek dosyalar önce mods/GTACoop/backups/components altına yedeklenir. scripts/ ve mods/ klasörleriniz taranmaz/silinmez. Devam edilsin mi?",
            release.Version, release.Tag, release.Source);
        if (!Ui.Confirm(FindForm()!, message)) return;

        _busy = true;
        SetBusy(true);
        _status.Text = release.Kind == ComponentKind.OpenIV
            ? T("Bileşen kuruluyor...")
            : T("Bileşen indiriliyor ve kuruluyor...");
        _progress.Style = ProgressBarStyle.Continuous;
        _progress.Value = 0;
        AppendDetail($"=== {release.Version} ({release.Source}) ===");
        AppEnv.Log($"Bileşen kurulumu başladı: {release.Kind}/{release.Channel} {release.Version}");

        try
        {
            var progress = new Progress<double>(p =>
            {
                if (!IsDisposed)
                    _progress.Value = Math.Clamp((int)Math.Round(p * 100), 0, 100);
            });

            var result = await ComponentManager.InstallAsync(
                release.Kind, release.Channel, release, gameFolder, AppEnv.DataFolder,
                msg =>
                {
                    AppEnv.Log(msg);
                    AppendDetail(msg);
                },
                progress);

            if (result.SkipReason == "already-current")
            {
                _status.Text = T("Bu bileşen zaten kurulu.");
                Ui.Info(FindForm()!, T("Bu bileşen zaten kurulu ve dosyaları sağlam.\r\nİndirme yapılmadı."));
            }
            else if (result.SkipReason is "openiv-present" or "openrpf-present" or "rageopenv-present")
            {
                if (release.Kind == ComponentKind.OpenIV)
                {
                    _status.Text = T("OpenIV.asi kurulmadı (mevcut mods köprüsü korundu).");
                    Ui.Info(FindForm()!, T("OpenIV.asi, OpenRPF.asi veya RageOpenV.asi zaten kurulu. Çakışmayı önlemek için yeni dosya kurulmadı; mevcut mods köprünüz korunuyor."));
                }
                else
                {
                    _status.Text = T("RageOpenV kurulmadı (mevcut mods köprüsü korundu).");
                    Ui.Info(FindForm()!, T("OpenIV.asi, OpenRPF.asi veya RageOpenV.asi zaten kurulu. Çakışmayı önlemek için RageOpenV kurulmadı; mevcut mods köprünüz korunuyor."));
                }
            }
            else
            {
                _progress.Value = 100;
                _status.Text = T("Bileşen kuruldu: {0}", release.Version);
                var backupText = string.IsNullOrWhiteSpace(result.BackupDir)
                    ? T("Yedek alınmadı (yeni kurulum).")
                    : T("Yedek: {0}", result.BackupDir);
                Ui.Info(FindForm()!, T(
                    "Bileşen kuruldu: {0}\r\n\r\nKopyalanan: {1}\r\nKorunan kullanıcı dosyası: {2}\r\nAynı olduğu için atlanan: {3}\r\n\r\n{4}",
                    release.Version, result.Copied, result.Preserved, result.Unchanged, backupText));
            }
        }
        catch (Exception ex)
        {
            _status.Text = T("Bileşen kurulamadı.");
            AppEnv.LogError("Bileşen kurulumu: " + ex.Message);
            AppendDetail(T("Hata: ") + ex.Message);
            Ui.Error(FindForm()!, T("Bileşen kurulamadı:\r\n") + ex.Message);
        }
        finally
        {
            _busy = false;
            SetBusy(false);
            UpdateInstalledInfo();
            PopulateVersions();
            UpdateInstallButton();
            ComponentsChanged?.Invoke();
        }
    }

    private void RestoreLatestBackup()
    {
        if (_busy) return;
        if (!MainForm.RequireGameFolder(FindForm()!, out var gameFolder)) return;
        if (AddonApplier.IsGameRunning())
        {
            Ui.Warn(FindForm()!, T("GTA V açıkken geri yükleme yapılamaz."));
            return;
        }

        var kind = SelectedKind;
        var backup = ComponentManager.GetLatestBackup(kind, gameFolder);
        if (backup == null)
        {
            Ui.Warn(FindForm()!, T("Yedek bulunamadı."));
            return;
        }

        var version = string.IsNullOrWhiteSpace(backup.Info.Version)
            ? T("bilinmeyen / kullanıcı sürümü")
            : backup.Info.Version;
        if (!Ui.Confirm(FindForm()!, T(
            "Bu yedek geri yüklensin mi?\r\n\r\nSürüm: {0}\r\nYedek: {1}\r\n\r\nMevcut dosyalar ayrıca saklanır; scripts/ ve mods/ klasörleriniz korunur.",
            version, backup.Directory)))
            return;

        _busy = true;
        SetBusy(true);
        _status.Text = T("Yedek geri yükleniyor...");
        try
        {
            var result = ComponentManager.RestoreLatestBackup(
                kind, gameFolder, AppEnv.DataFolder,
                msg =>
                {
                    AppEnv.Log(msg);
                    AppendDetail(msg);
                });
            _status.Text = T("Yedek geri yüklendi: {0}", result.Version);
            Ui.Info(FindForm()!, T("Yedekten geri yükleme tamamlandı: {0} dosya.\r\n{1}",
                result.Restored, result.BackupDir));
        }
        catch (Exception ex)
        {
            AppEnv.LogError("Bileşen geri yükleme: " + ex.Message);
            AppendDetail(T("Hata: ") + ex.Message);
            Ui.Error(FindForm()!, T("Geri yükleme başarısız:\r\n") + ex.Message);
        }
        finally
        {
            _busy = false;
            SetBusy(false);
            UpdateInstalledInfo();
            PopulateVersions();
            UpdateInstallButton();
            ComponentsChanged?.Invoke();
        }
    }

    private void OpenBackupFolder()
    {
        if (!MainForm.RequireGameFolder(FindForm()!, out var gameFolder)) return;
        try
        {
            var dir = ComponentManager.BackupRoot(SelectedKind, gameFolder);
            Directory.CreateDirectory(dir);
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{dir}\"")
            {
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            AppEnv.LogError("Yedek klasörü açılamadı: " + ex.Message);
            Ui.Error(FindForm()!, T("Klasör açılamadı:\r\n") + ex.Message);
        }
    }

    private void SetBusy(bool busy)
    {
        _component.Enabled = !busy;
        _channel.Enabled = !busy && SelectedKind == ComponentKind.ScriptHookVDotNet;
        _version.Enabled = !busy;
        _fetchBtn.Enabled = !busy;
        _openBackupBtn.Enabled = !busy;
        if (busy)
        {
            _installBtn.Enabled = false;
            _restoreBtn.Enabled = false;
        }
        else
        {
            UpdateInstallButton();
            UpdateInstalledInfo();
        }
    }

    private void UpdateInstallButton()
    {
        if (_busy || _version.SelectedItem is not ReleaseOption option)
        {
            _installBtn.Text = SelectedKind == ComponentKind.OpenIV ? T("Kur") : T("İndir ve Kur");
            _installBtn.Enabled = false;
            return;
        }

        var release = option.Release;
        var installed = ComponentManager.GetInstalled(SelectedKind, AppEnv.DataFolder);
        var gameFolder = AppEnv.Settings.GameFolder;
        var intact = installed != null &&
                     !string.IsNullOrWhiteSpace(gameFolder) &&
                     GameFolder.IsValid(gameFolder) &&
                     ComponentManager.IsIntact(installed, gameFolder);

        if (installed != null &&
            installed.Tag.Equals(release.Tag, StringComparison.OrdinalIgnoreCase) &&
            installed.Channel == release.Channel &&
            intact)
        {
            _installBtn.Text = T("Kurulu");
            _installBtn.Enabled = false;
        }
        else if (installed != null &&
                 installed.Tag.Equals(release.Tag, StringComparison.OrdinalIgnoreCase) &&
                 installed.Channel == release.Channel)
        {
            _installBtn.Text = T("Onar");
            _installBtn.Enabled = true;
        }
        else if (installed != null)
        {
            _installBtn.Text = T("Güncelle");
            _installBtn.Enabled = true;
        }
        else
        {
            _installBtn.Text = release.Kind == ComponentKind.OpenIV ? T("Kur") : T("İndir ve Kur");
            _installBtn.Enabled = true;
        }
    }

    private void UpdateInstalledInfo()
    {
        var kind = SelectedKind;
        var installed = ComponentManager.GetInstalled(kind, AppEnv.DataFolder);
        var gameFolder = AppEnv.Settings.GameFolder;
        var hasGame = !string.IsNullOrWhiteSpace(gameFolder) && GameFolder.IsValid(gameFolder);

        if (installed == null)
        {
            _installed.Text = T("Kurulu değil.");
            _installed.ForeColor = Color.DimGray;
            _backup.Text = "";
            _restoreBtn.Enabled = hasGame && !_busy &&
                                  ComponentManager.GetLatestBackup(kind, gameFolder!) != null;
            return;
        }

        var intact = hasGame && ComponentManager.IsIntact(installed, gameFolder!);
        var channelText = kind == ComponentKind.ScriptHookVDotNet
            ? " · " + ChannelText(installed.Channel)
            : "";
        _installed.Text = T("Kurulu: {0}{1}", installed.Version, channelText) +
                          (intact ? "" : " — " + T("dosyalar eksik/bozuk"));
        _installed.ForeColor = intact ? Ui.OkColor : Ui.WarnColor;
        _backup.Text = string.IsNullOrWhiteSpace(installed.BackupDir)
            ? T("Yedek: henüz yok.")
            : T("Son yedek: {0}", installed.BackupDir);
        _restoreBtn.Enabled = hasGame && !_busy &&
                              ComponentManager.GetLatestBackup(kind, gameFolder!) != null;
    }

    private void UpdateSourceHint()
    {
        _source.Text = SelectedKind switch
        {
            ComponentKind.ScriptHookV =>
                T("Kaynak: dev-c.com (resmî). GitHub'da resmî ScriptHookV sürümü yoktur; yeniden dağıtım yasaktır."),
            ComponentKind.ScriptHookVDotNet =>
                T("Kaynak: resmî GitHub sürümleri (stable / nightly / enhanced)."),
            ComponentKind.OpenIV =>
                T("Kaynak: paket içi OpenIV.asi (kullanıcı tarafından sağlandı). Mevcut OpenIV.asi, OpenRPF.asi veya RageOpenV.asi varsa korunur. ASI loader gerekir (ScriptHookV ile birlikte kurun)."),
            ComponentKind.OpenIvAlternative =>
                T("Kaynak: GitHub · RageOpenV (GPL). OpenIV.asi resmî GitHub'da yok; kuruluysa korunur. RageOpenV için ASI loader gerekir (ScriptHookV ile birlikte kurun)."),
            _ => ""
        };
    }

    private static FlowLayoutPanel Row() => new()
    {
        AutoSize = true,
        AutoSizeMode = AutoSizeMode.GrowAndShrink,
        FlowDirection = FlowDirection.LeftToRight,
        WrapContents = false,
        Dock = DockStyle.Top,
        Margin = new Padding(0)
    };

    private static string ChannelText(ShvdnChannel channel) => channel switch
    {
        ShvdnChannel.Stable => "Stable",
        ShvdnChannel.Nightly => "Nightly",
        ShvdnChannel.Enhanced => "Enhanced v3",
        _ => channel.ToString()
    };

    private void AppendDetail(string line)
    {
        if (IsDisposed) return;
        if (InvokeRequired)
        {
            try { BeginInvoke(() => AppendDetail(line)); } catch { }
            return;
        }
        line = SyncText.Translate(line);
        _details.AppendText($"[{DateTime.Now:HH:mm:ss}] {line}{Environment.NewLine}");
        _details.SelectionStart = _details.TextLength;
        _details.ScrollToCaret();
    }

    private sealed class ComponentOption
    {
        public ComponentOption(ComponentKind kind, string text)
        {
            Kind = kind;
            Text = text;
        }

        public ComponentKind Kind { get; }
        private string Text { get; }
        public override string ToString() => Text;
    }

    private sealed class ChannelOption
    {
        public ChannelOption(ShvdnChannel channel, string text)
        {
            Channel = channel;
            Text = text;
        }

        public ShvdnChannel Channel { get; }
        private string Text { get; }
        public override string ToString() => Text;
    }

    private sealed class ReleaseOption
    {
        public ReleaseOption(ComponentRelease release, string text)
        {
            Release = release;
            Text = text;
        }

        public ComponentRelease Release { get; }
        private string Text { get; }
        public override string ToString() => Text;
    }
}
