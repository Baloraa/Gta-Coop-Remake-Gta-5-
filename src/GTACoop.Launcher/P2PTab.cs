using System.Diagnostics;
using System.Net;
using System.Windows.Forms;

namespace GTACoop.Remake;

/// <summary>
/// P2P mod paylaşımı: bir klasör (sunucu modları) doğrudan arkadaşa açılır,
/// arkadaş kod ile bağlanıp dosyaları özet doğrulamasıyla indirir. Arada
/// sunucu, relay veya hesap yoktur.
/// </summary>
public sealed class P2PTab : UserControl
{
    private readonly TextBox _shareFolder = new();
    private readonly NumericUpDown _port = new();
    private readonly Label _shareStatus = new();
    private readonly Label _codes = new();
    private readonly Button _startBtn;
    private readonly Button _stopBtn;

    private readonly TextBox _code = new();
    private readonly TextBox _destination = new();
    private readonly Label _peerInfo = new();
    private readonly ListView _files = new();
    private readonly ProgressBar _progress = new();
    private readonly Label _downloadStatus = new();
    private readonly Button _connectBtn;
    private readonly Button _downloadBtn;
    private readonly Button _cancelBtn;

    private readonly P2PShareServer _server = new();
    private readonly List<P2PFile> _manifestFiles = new();

    private P2PShareClient? _client;
    private CancellationTokenSource? _cts;
    private bool _busy;

    public P2PTab()
    {
        Dock = DockStyle.Fill;

        _server.Log += line => UiThread(() => AppendShareLog(line));

        var scroll = new ScrollPanel { Dock = DockStyle.Fill, Padding = new Padding(10, 8, 10, 8) };

        // ---------------------------------------------------------- paylaş
        var hostGroup = new DarkGroup
        {
            Text = T("Paylaş (eşe aç)"),
            Dock = DockStyle.Top,
            Padding = new Padding(10, 24, 10, 10)
        };

        _shareFolder.ReadOnly = true;
        _shareFolder.Width = 330;
        _shareFolder.Margin = new Padding(4, 5, 4, 4);

        var shareRow = Ui.Flow();
        shareRow.Controls.Add(Dim(T("Klasör:")));
        shareRow.Controls.Add(_shareFolder);
        shareRow.Controls.Add(Ui.Btn(T("Gözat..."), (_, _) => BrowseShareFolder()));
        shareRow.Controls.Add(Ui.Btn(T("Sunucum Modları"), (_, _) => UseServerModsFolder()));
        shareRow.Controls.Add(Ui.Btn(T("Klasörü Aç"), (_, _) => OpenFolder(_shareFolder.Text)));

        _port.Minimum = 1024;
        _port.Maximum = 65535;
        _port.Value = Math.Clamp(AppEnv.Settings.P2PPort, 1024, 65535);
        _port.Width = 90;
        _port.Margin = new Padding(4, 4, 10, 4);

        _startBtn = Ui.Btn(T("Paylaşımı Başlat"), async (_, _) => await StartShareAsync());
        _stopBtn = Ui.Btn(T("Durdur"), (_, _) => StopShare());
        _stopBtn.Enabled = false;

        var portRow = Ui.Flow();
        portRow.Controls.Add(Dim(T("Port:")));
        portRow.Controls.Add(_port);
        portRow.Controls.Add(_startBtn);
        portRow.Controls.Add(_stopBtn);
        portRow.Controls.Add(Ui.Btn(T("Güvenlik Duvarı Kuralı Ekle"), (_, _) => AddFirewallRule()));
        portRow.Controls.Add(Ui.Btn(T("ZIP Olarak Dışa Aktar..."), (_, _) => ExportZip()));

        _shareStatus.AutoSize = false;
        _shareStatus.Height = 22;
        _shareStatus.Dock = DockStyle.Top;
        _shareStatus.TextAlign = ContentAlignment.MiddleLeft;
        _shareStatus.ForeColor = Color.DimGray;
        _shareStatus.Text = T("Paylaşım kapalı.");

        _codes.AutoSize = false;
        _codes.Height = 44;
        _codes.Dock = DockStyle.Top;
        _codes.ForeColor = Theme.AccentAlt;
        _codes.Text = T("Paylaşımı başlatınca arkadaşına vereceğin kod burada görünür.");

        hostGroup.Controls.Add(_shareStatus);
        hostGroup.Controls.Add(_codes);
        hostGroup.Controls.Add(portRow);
        hostGroup.Controls.Add(shareRow);

        // ---------------------------------------------------------- indir
        var joinGroup = new DarkGroup
        {
            Text = T("Katıl ve indir"),
            Dock = DockStyle.Top,
            Padding = new Padding(10, 24, 10, 10)
        };

        _code.Width = 210;
        _code.Margin = new Padding(4, 5, 4, 4);
        _code.PlaceholderText = T("192.168.1.20:47900");

        _connectBtn = Ui.Btn(T("Bağlan ve Listele"), async (_, _) => await ConnectAsync());
        _downloadBtn = Ui.Btn(T("İndir"), async (_, _) => await DownloadAsync());
        _downloadBtn.Enabled = false;
        _cancelBtn = Ui.Btn(T("İptal"), (_, _) => _cts?.Cancel());
        _cancelBtn.Enabled = false;

        var codeRow = Ui.Flow();
        codeRow.Controls.Add(Dim(T("Kod:")));
        codeRow.Controls.Add(_code);
        codeRow.Controls.Add(_connectBtn);
        codeRow.Controls.Add(Ui.Btn(T("Panodan Yapıştır"), (_, _) => PasteCode()));

        _peerInfo.AutoSize = false;
        _peerInfo.Height = 22;
        _peerInfo.Dock = DockStyle.Top;
        _peerInfo.ForeColor = Color.DimGray;
        _peerInfo.Text = T("Henüz bağlanılmadı.");

        Ui.StyleList(_files, (T("Dosya"), 460), (T("Boyut"), 110), (T("Durum"), 190));
        _files.Dock = DockStyle.Top;
        _files.Height = 150;
        _files.CheckBoxes = true;

        _destination.ReadOnly = true;
        _destination.Width = 330;
        _destination.Margin = new Padding(4, 5, 4, 4);
        _destination.Text = AppEnv.Settings.P2PFolder;

        var destRow = Ui.Flow();
        destRow.Controls.Add(Dim(T("Hedef klasör:")));
        destRow.Controls.Add(_destination);
        destRow.Controls.Add(Ui.Btn(T("Gözat..."), (_, _) => BrowseDestination()));
        destRow.Controls.Add(Ui.Btn(T("Klasörü Aç"), (_, _) => OpenFolder(_destination.Text)));

        _progress.Dock = DockStyle.Top;
        _progress.Height = 12;
        _progress.Maximum = 1000;
        _progress.Style = ProgressBarStyle.Continuous;

        _downloadStatus.AutoSize = false;
        _downloadStatus.Height = 24;
        _downloadStatus.Dock = DockStyle.Top;
        _downloadStatus.TextAlign = ContentAlignment.MiddleLeft;
        _downloadStatus.ForeColor = Color.DimGray;
        _downloadStatus.Text = T("Hedef klasör seçip 'İndir'e basın. Aynı dosyalar yeniden indirilmez.");

        var downloadRow = Ui.Flow();
        downloadRow.Controls.Add(_downloadBtn);
        downloadRow.Controls.Add(_cancelBtn);
        downloadRow.Controls.Add(Ui.Btn(T("Tümünü Seç"), (_, _) => CheckAll(true)));
        downloadRow.Controls.Add(Ui.Btn(T("Seçimi Kaldır"), (_, _) => CheckAll(false)));

        joinGroup.Controls.Add(_downloadStatus);
        joinGroup.Controls.Add(_progress);
        joinGroup.Controls.Add(downloadRow);
        joinGroup.Controls.Add(destRow);
        joinGroup.Controls.Add(_files);
        joinGroup.Controls.Add(_peerInfo);
        joinGroup.Controls.Add(codeRow);

        // ---------------------------------------------------------- nasıl
        var helpGroup = new DarkGroup
        {
            Text = T("Nasıl çalışır?"),
            Dock = DockStyle.Top,
            Padding = new Padding(10, 24, 10, 10)
        };
        helpGroup.Controls.Add(new Label
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ForeColor = Color.DimGray,
            Text = T("1) Paylaşan kişi bir klasör seçip 'Paylaşımı Başlat'a basar; ekranda çıkan IP:port kodunu arkadaşına gönderir.\r\n" +
                     "2) Arkadaş kod ile bağlanır, dosya listesini görür, hedef klasörü seçip indirir. Her dosya SHA256 ile doğrulanır.\r\n" +
                     "3) Aynı ağdaysanız (Wi-Fi/hotspot/Radmin VPN) kod doğrudan çalışır. İnternet üzerinden paylaşım için yönlendirici portu veya sanal ağ gerekir.\r\n" +
                     "4) İndirilen klasörü sunucu modlarına taşıyabilir ya da doğrudan oyunun mods klasörüne kopyalayabilirsin. Oyun açıkken dosyalar kilitli olabilir.")
        });

        scroll.Controls.Add(helpGroup);
        scroll.Controls.Add(joinGroup);
        scroll.Controls.Add(hostGroup);
        Controls.Add(scroll);

        Ui.FitToContent(hostGroup);
        Ui.FitToContent(joinGroup);
        Ui.FitToContent(helpGroup);

        var initial = AppEnv.Settings.P2PLastFolder;
        if (!string.IsNullOrWhiteSpace(initial) && Directory.Exists(initial)) _shareFolder.Text = initial;
        else if (Directory.Exists(AppEnv.ServersFolder)) _shareFolder.Text = AppEnv.ServersFolder;

        if (!string.IsNullOrWhiteSpace(AppEnv.Settings.P2PLastCode)) _code.Text = AppEnv.Settings.P2PLastCode;
        if (string.IsNullOrWhiteSpace(_destination.Text))
            _destination.Text = Path.Combine(AppEnv.ServersFolder, "P2P");

        UpdateShareState();
    }

    // ------------------------------------------------------------- yardımcı

    private static Label Dim(string text) => new()
    {
        Text = text,
        AutoSize = true,
        ForeColor = Color.DimGray,
        Margin = new Padding(4, 7, 4, 4)
    };

    private void UiThread(Action action)
    {
        if (IsDisposed) return;
        try
        {
            if (IsHandleCreated && InvokeRequired) BeginInvoke(action);
            else action();
        }
        catch { /* pencere kapanmış olabilir */ }
    }

    private void AppendShareLog(string line)
    {
        _shareStatus.Text = line;
        AppEnv.Log("P2P: " + line);
    }

    private void UpdateShareState()
    {
        _startBtn.Enabled = !_server.Running;
        _stopBtn.Enabled = _server.Running;
        _port.Enabled = !_server.Running;
    }

    // ------------------------------------------------------------- paylaşım

    private void BrowseShareFolder()
    {
        using var dlg = new FolderBrowserDialog
        {
            Description = T("Paylaşılacak mod klasörünü seçin."),
            SelectedPath = Directory.Exists(_shareFolder.Text) ? _shareFolder.Text : AppEnv.ServersFolder
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        _shareFolder.Text = dlg.SelectedPath;
        AppEnv.Settings.P2PLastFolder = dlg.SelectedPath;
        AppEnv.SaveSettings();
    }

    private void UseServerModsFolder()
    {
        if (!Directory.Exists(AppEnv.ServersFolder))
        {
            Ui.Warn(FindForm()!, T("Sunucu mod klasörü henüz yok. Önce Sunucum sekmesinde mod ekleyin."));
            return;
        }

        var dirs = Directory.GetDirectories(AppEnv.ServersFolder);
        if (dirs.Length == 1) _shareFolder.Text = dirs[0];
        else _shareFolder.Text = AppEnv.ServersFolder;
    }

    private async Task StartShareAsync()
    {
        if (_busy) return;

        var folder = _shareFolder.Text.Trim();
        if (!Directory.Exists(folder))
        {
            Ui.Warn(FindForm()!, T("Paylaşılacak klasör bulunamadı. 'Gözat...' ile seçin."));
            return;
        }

        _busy = true;
        _startBtn.Enabled = false;
        _shareStatus.ForeColor = Color.DimGray;
        _cts = new CancellationTokenSource();

        try
        {
            var port = (int)_port.Value;
            await _server.StartAsync(folder, port,
                progress => _shareStatus.Text = progress, _cts.Token);

            AppEnv.Settings.P2PPort = _server.Port;
            AppEnv.Settings.P2PLastFolder = folder;
            AppEnv.SaveSettings();

            _shareStatus.ForeColor = Ui.OkColor;
            var addresses = LocalAddresses.Detect();
            _codes.Text = addresses.Count == 0
                ? T("Ağ adresi bulunamadı. Kodu elle girin: 127.0.0.1:{0}", _server.Port)
                : string.Join("   ", addresses.Take(2).Select(a => P2PCode.Format(a.Ip, _server.Port)));
            _codes.ForeColor = Theme.Accent;
            _shareStatus.Text = T("Paylaşım açık — {0} dosya, {1}. Kodu arkadaşınla paylaş.",
                _server.Manifest!.Files.Count, P2PShare.FormatSize(_server.Manifest.TotalBytes));
            UpdateShareState();
        }
        catch (Exception ex)
        {
            _shareStatus.ForeColor = Ui.ErrColor;
            _shareStatus.Text = T("Paylaşım başlatılamadı: ") + ex.Message;
            _server.Stop();
            UpdateShareState();
            Ui.Error(FindForm()!, T("Paylaşım başlatılamadı:\r\n") + ex.Message);
        }
        finally
        {
            _busy = false;
            if (!_server.Running) _startBtn.Enabled = true;
        }
    }

    private void StopShare()
    {
        _server.Stop();
        _codes.Text = T("Paylaşım kapalı.");
        _codes.ForeColor = Theme.Muted;
        _shareStatus.ForeColor = Color.DimGray;
        _shareStatus.Text = T("Paylaşım kapalı.");
        UpdateShareState();
    }

    private void AddFirewallRule()
    {
        if (_server.Running)
            FirewallHelper.EnsureTcpRuleAsync(_server.Port, line => UiThread(() => AppendShareLog(line)));
        else
            FirewallHelper.EnsureTcpRuleAsync((int)_port.Value, line => UiThread(() => AppendShareLog(line)));
    }

    private void ExportZip()
    {
        var folder = _shareFolder.Text.Trim();
        if (!Directory.Exists(folder))
        {
            Ui.Warn(FindForm()!, T("Paylaşılacak klasör bulunamadı."));
            return;
        }

        using var dlg = new SaveFileDialog
        {
            Title = T("Mod klasörünü zip olarak kaydet"),
            Filter = "Zip paketi (*.zip)|*.zip",
            FileName = new DirectoryInfo(folder).Name + ".zip"
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            UseWaitCursor = true;
            P2PShare.ExportZip(folder, dlg.FileName, AppEnv.Log);
            Ui.Info(FindForm()!, T("Klasör zip olarak kaydedildi:\r\n{0}", dlg.FileName));
        }
        catch (Exception ex)
        {
            AppEnv.LogError("Zip oluşturulamadı: " + ex.Message);
            Ui.Error(FindForm()!, T("Zip oluşturulamadı:\r\n") + ex.Message);
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    // ------------------------------------------------------------- indirme

    private void PasteCode()
    {
        try
        {
            if (Clipboard.ContainsText()) _code.Text = Clipboard.GetText().Trim();
        }
        catch { /* pano erişilemez */ }
    }

    private void BrowseDestination()
    {
        using var dlg = new FolderBrowserDialog
        {
            Description = T("İndirilen dosyaların kaydedileceği klasörü seçin."),
            SelectedPath = Directory.Exists(_destination.Text) ? _destination.Text : AppEnv.ServersFolder
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        _destination.Text = dlg.SelectedPath;
        AppEnv.Settings.P2PFolder = dlg.SelectedPath;
        AppEnv.SaveSettings();
    }

    private async Task ConnectAsync()
    {
        if (_busy) return;

        if (!P2PCode.TryParse(_code.Text, out var host, out var port))
        {
            Ui.Warn(FindForm()!, T("Kod geçersiz. Örnek: 192.168.1.20:47900"));
            return;
        }

        _busy = true;
        _connectBtn.Enabled = false;
        _peerInfo.ForeColor = Color.DimGray;
        _peerInfo.Text = T("Bağlanılıyor: {0}:{1}", host, port);
        _files.Items.Clear();
        _manifestFiles.Clear();
        _cts = new CancellationTokenSource();

        try
        {
            _client?.Dispose();
            _client = new P2PShareClient();
            var manifest = await _client.ConnectAsync(host, port, _cts.Token);

            _manifestFiles.AddRange(manifest.Files);
            _files.BeginUpdate();
            foreach (var file in manifest.Files)
            {
                var item = new ListViewItem(file.Path) { Checked = true };
                item.SubItems.Add(file.SizeText);
                item.SubItems.Add("");
                _files.Items.Add(item);
            }
            _files.EndUpdate();

            _peerInfo.ForeColor = Ui.OkColor;
            _peerInfo.Text = T("Bağlandı: {0} — {1} dosya, {2} ({3})",
                string.IsNullOrWhiteSpace(manifest.Name) ? host : manifest.Name,
                manifest.Files.Count, P2PShare.FormatSize(manifest.TotalBytes),
                string.IsNullOrWhiteSpace(manifest.SharedBy) ? T("bilinmeyen eş") : manifest.SharedBy);

            AppEnv.Settings.P2PLastCode = P2PCode.Format(host, port);
            AppEnv.SaveSettings();
            _downloadBtn.Enabled = manifest.Files.Count > 0;
            AppEnv.Log($"P2P: {host}:{port} adresine bağlandı, {manifest.Files.Count} dosya listelendi.");
        }
        catch (Exception ex)
        {
            _peerInfo.ForeColor = Ui.ErrColor;
            _peerInfo.Text = T("Bağlanılamadı: ") + ex.Message;
            Ui.Error(FindForm()!, T("Eşe bağlanılamadı:\r\n") + ex.Message);
        }
        finally
        {
            _busy = false;
            _connectBtn.Enabled = true;
        }
    }

    private void CheckAll(bool value)
    {
        _files.BeginUpdate();
        foreach (ListViewItem item in _files.Items) item.Checked = value;
        _files.EndUpdate();
    }

    private async Task DownloadAsync()
    {
        if (_busy || _client?.Manifest == null) return;

        var destination = _destination.Text.Trim();
        if (string.IsNullOrWhiteSpace(destination))
        {
            Ui.Warn(FindForm()!, T("Önce hedef klasörü seçin."));
            return;
        }

        var selected = new List<string>();
        foreach (ListViewItem item in _files.Items)
            if (item.Checked && item.Tag is not string) selected.Add(item.Text);

        if (selected.Count == 0)
        {
            Ui.Warn(FindForm()!, T("İndirilecek dosya seçilmedi."));
            return;
        }

        _busy = true;
        _downloadBtn.Enabled = false;
        _cancelBtn.Enabled = true;
        _connectBtn.Enabled = false;
        _progress.Value = 0;
        _downloadStatus.ForeColor = Color.DimGray;

        var target = Path.Combine(destination,
            string.IsNullOrWhiteSpace(_client.Manifest.Name) ? "P2P" : _client.Manifest.Name);

        _cts?.Dispose();
        _cts = new CancellationTokenSource();

        try
        {
            await _client.DownloadAsync(
                target,
                selected,
                (file, done, total) =>
                {
                    var index = _manifestFiles.FindIndex(f => f.Path == file.Path);
                    if (index < 0) return;
                    UiThread(() =>
                    {
                        if (index < _files.Items.Count)
                            _files.Items[index].SubItems[2].Text =
                                total > 0 ? $"%{done * 100 / total}" : "";
                    });
                },
                (done, total) =>
                {
                    UiThread(() =>
                    {
                        _progress.Value = total <= 0 ? 1000 : (int)Math.Clamp(done * 1000 / total, 0, 1000);
                        _downloadStatus.Text = T("İndiriliyor: {0} / {1}", P2PShare.FormatSize(done), P2PShare.FormatSize(total));
                    });
                },
                AppEnv.Log,
                _cts.Token);

            _progress.Value = 1000;
            _downloadStatus.ForeColor = Ui.OkColor;
            _downloadStatus.Text = T("İndirme tamamlandı: {0}", target);
            Ui.Info(FindForm()!, T("Dosyalar indirildi ve doğrulandı:\r\n{0}", target));
        }
        catch (OperationCanceledException)
        {
            _downloadStatus.ForeColor = Ui.WarnColor;
            _downloadStatus.Text = T("İndirme iptal edildi.");
        }
        catch (Exception ex)
        {
            _downloadStatus.ForeColor = Ui.ErrColor;
            _downloadStatus.Text = T("İndirme hatası: ") + ex.Message;
            AppEnv.LogError("P2P indirme hatası: " + ex.Message);
        }
        finally
        {
            _busy = false;
            _downloadBtn.Enabled = true;
            _cancelBtn.Enabled = false;
            _connectBtn.Enabled = true;
        }
    }

    private void OpenFolder(string path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            if (!Directory.Exists(path))
            {
                Ui.Warn(FindForm()!, T("Klasör yok: ") + path);
                return;
            }
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            AppEnv.LogError("Klasör açılamadı: " + ex.Message);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _server.Dispose();
            _client?.Dispose();
            _cts?.Dispose();
        }
        base.Dispose(disposing);
    }
}
