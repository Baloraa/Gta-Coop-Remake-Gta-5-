using System.Text;
using System.Windows.Forms;
using GTACoop.Sync;

namespace GTACoop.Remake;

public sealed class LogTab : UserControl
{
    private readonly RichTextBox _log = new();
    private readonly ComboBox _backups = new();

    public LogTab()
    {
        Dock = DockStyle.Fill;

        // --- araçlar
        var tools = new GroupBox
        {
            Text = T("Araçlar ve Kurtarma"),
            Dock = DockStyle.Top,
            Height = 160,
            Padding = new Padding(8)
        };

        var row1 = Ui.Flow();
        row1.Controls.Add(Ui.Btn(T("Yedekleri Listele"), (_, _) => RefreshBackups()));
        _backups.Width = 260;
        _backups.DropDownStyle = ComboBoxStyle.DropDownList;
        _backups.Margin = new Padding(4, 6, 4, 4);
        row1.Controls.Add(_backups);
        row1.Controls.Add(Ui.Btn(T("Seçili Yedeği Geri Yükle"), (_, _) => RestoreSelected()));

        var row2 = Ui.Flow();
        row2.Controls.Add(Ui.Btn(T("Tanılama Kopyala"), (_, _) => CopyDiagnostics()));
        row2.Controls.Add(Ui.Btn(T("gcs_ Kalıntılarını Temizle"), (_, _) => PruneOrphans()));
        row2.Controls.Add(Ui.Btn(T("AutoConnect Kapat"), (_, _) => DisableAutoConnect()));
        row2.Controls.Add(Ui.Btn(T("Günlük Dosyasını Aç"), (_, _) => OpenLogFile()));
        row2.Controls.Add(Ui.Btn(T("Günlüğü Temizle"), (_, _) => _log.Clear()));

        var hint = new Label
        {
            Dock = DockStyle.Bottom,
            AutoSize = false,
            Height = 42,
            ForeColor = Color.DimGray,
            Text = T("Geri yükleme yalnızca GTA V kapalıyken yapılabilir.\r\nKalıntı temizliği; sunuculardan kurulmuş tüm eklentileri (script, ASI, dlc paketi) kaldırır — kendi modlarınıza dokunmaz.")
        };

        tools.Controls.Add(hint);
        tools.Controls.Add(row2);
        tools.Controls.Add(row1);

        // --- günlük
        _log.Dock = DockStyle.Fill;
        _log.ReadOnly = true;
        _log.Font = new Font("Consolas", 8.5f);
        _log.BackColor = Color.FromArgb(30, 30, 30);
        _log.ForeColor = Color.Gainsboro;
        _log.WordWrap = false;
        _log.ScrollBars = RichTextBoxScrollBars.Both;

        Controls.Add(_log);
        Controls.Add(tools);
    }

    public void Append(string line)
    {
        if (_log.Lines.Length > 5000)
        {
            _log.Clear();
            _log.AppendText(T("... (günlük kısaltıldı) ...") + Environment.NewLine);
        }

        _log.AppendText(SyncText.Translate(line) + Environment.NewLine);
        _log.SelectionStart = _log.TextLength;
        _log.ScrollToCaret();
    }

    private void RefreshBackups()
    {
        _backups.Items.Clear();
        var gameFolder = AppEnv.Settings.GameFolder;
        if (string.IsNullOrWhiteSpace(gameFolder))
        {
            Ui.Warn(FindForm()!, T("Önce GTA V klasörünü seçin."));
            return;
        }

        var backups = BackupService.ListUpdateRpfBackups(gameFolder);
        foreach (var b in backups)
            _backups.Items.Add(Path.GetFileName(b));

        if (_backups.Items.Count > 0)
            _backups.SelectedIndex = 0;

        AppEnv.Log($"Yedekler: {backups.Count} adet.");
    }

    private void RestoreSelected()
    {
        if (_backups.SelectedItem == null)
        {
            Ui.Warn(FindForm()!, T("Önce 'Yedekleri Listele' ile yedekleri yükleyin."));
            return;
        }

        if (!MainForm.RequireGameFolder(FindForm()!, out var gameFolder)) return;

        if (AddonApplier.IsGameRunning())
        {
            Ui.Warn(FindForm()!, T("GTA V açıkken geri yükleme yapılamaz."));
            return;
        }

        var name = _backups.SelectedItem.ToString()!;
        var full = Path.Combine(BackupService.BackupDir(gameFolder), name);

        if (!Ui.Confirm(FindForm()!,
            T("mods/update/update.rpf şu yedekle değiştirilecek:\r\n{0}\r\n\r\nMevcut dosya 'update.rpf.before-restore' olarak saklanır. Devam edilsin mi?", name)))
            return;

        try
        {
            UseWaitCursor = true;
            BackupService.RestoreUpdateRpf(gameFolder, full, AppEnv.Log);
            Ui.Info(FindForm()!, T("Geri yükleme tamamlandı.\r\nNot: Sunucu modlarının dlclist girdileri bu yedekteki duruma döner."));
        }
        catch (Exception ex)
        {
            AppEnv.LogError(ex.Message);
            Ui.Error(FindForm()!, T("Geri yükleme başarısız:\r\n") + ex.Message);
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private void PruneOrphans()
    {
        if (!MainForm.RequireGameFolder(FindForm()!, out var gameFolder)) return;

        if (AddonApplier.IsGameRunning())
        {
            Ui.Warn(FindForm()!, T("GTA V açıkken temizlik yapılamaz."));
            return;
        }

        if (!Ui.Confirm(FindForm()!,
            T("Sunuculardan kurulmuş tüm modlar kaldırılacak:\r\n• script ve ASI eklentileri (scripts/ ve oyun kökü)\r\n• gcs_ önekli DLC paketleri ve dlclist girdileri\r\n\r\nKendi modlarınız ve diğer dosyalar etkilenmez. Devam edilsin mi?")))
            return;

        try
        {
            UseWaitCursor = true;

            // Oturum izleyicisi çalışıyorsa durdur (çakışma olmasın)
            var rec = SessionStore.Load(AppEnv.DataFolder);
            if (rec != null) SessionService.KillWatcher(rec, AppEnv.Log);

            // 1) Durum kayıtlarındaki tüm eklentileri düzgünce kaldır
            var applier = new AddonApplier(gameFolder, AppEnv.Log);
            foreach (var state in SyncStateStore.LoadAll(AppEnv.DataFolder))
            {
                if (state.Addons.Count > 0)
                    applier.UninstallAll(state, AppEnv.DataFolder);
            }

            // 2) Kayıt dışı kalmış gcs_ kalıntılarını temizle
            applier.PruneOrphanMounts();

            // 3) Oturum kaydını sil
            SessionStore.Clear(AppEnv.DataFolder);

            Ui.Info(FindForm()!, T("Temizlik tamamlandı.\r\nSunucu modları kaldırıldı; oyun eski hâlinde."));
        }
        catch (Exception ex)
        {
            AppEnv.LogError(ex.Message);
            Ui.Error(FindForm()!, T("Temizlik hatası:\r\n") + ex.Message);
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private void DisableAutoConnect()
    {
        if (!MainForm.RequireGameFolder(FindForm()!, out var gameFolder)) return;
        ClientSettingsEditor.DisableAutoConnect(gameFolder, AppEnv.Log);
        Ui.Info(FindForm()!, T("AutoConnect kapatıldı. Oyun artık otomatik bağlanmaz."));
    }

    private void OpenLogFile()
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = AppEnv.LogFile,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            AppEnv.LogError("Günlük açılamadı: " + ex.Message);
        }
    }

    /// <summary>
    /// Sorun bildirirken paylaşılabilecek sürüm, sistem, sunucu ve son günlük
    /// satırlarını panoya kopyalar. Parola/token gibi gizli alanlar eklenmez.
    /// </summary>
    private void CopyDiagnostics()
    {
        try
        {
            var server = MyServerSettings.Load(AppEnv.DataFolder);
            var sb = new StringBuilder();
            sb.AppendLine("GTACoop Remake - " + T("Tanılama"));
            sb.AppendLine(T("Sürüm: {0}", Application.ProductVersion));
            sb.AppendLine(T("İşletim sistemi: {0}", System.Runtime.InteropServices.RuntimeInformation.OSDescription +
                (Environment.Is64BitOperatingSystem ? " (64-bit)" : " (32-bit)")));
            sb.AppendLine(T(".NET: {0}", Environment.Version));
            sb.AppendLine(T("Dil: {0}", Loc.LanguageName));
            sb.AppendLine(T("GTA klasörü: {0}",
                string.IsNullOrWhiteSpace(AppEnv.Settings.GameFolder) ? T("(seçilmemiş)") : AppEnv.Settings.GameFolder));
            sb.AppendLine(T("Sunucu: {0} | oyun: {1} UDP | sync: {2} TCP",
                server.ServerName, server.GamePort, server.SyncPort));
            sb.AppendLine(T("Relay sunucusu: {0}",
                string.IsNullOrWhiteSpace(server.RelayServer) ? "bore.pub" : server.RelayServer));
            sb.AppendLine(T(string.IsNullOrWhiteSpace(server.ShortCode) ? "Kısa kod: kapalı" : "Kısa kod: açık"));

            var ips = LocalAddresses.Detect().Select(a => a.Ip).ToList();
            sb.AppendLine(T("Yerel adresler: {0}",
                ips.Count == 0 ? T("(yok)") : string.Join(", ", ips)));

            sb.AppendLine();
            sb.AppendLine(T("--- son 80 günlük satırı ---"));

            // Önce gerçek günlük dosyasını oku: Log sekmesi hiç açılmadıysa
            // arayüz kutusu boş olabilir. Dosya okunamazsa arayüzdeki
            // satırlara dön.
            var logLines = Array.Empty<string>();
            try
            {
                if (File.Exists(AppEnv.LogFile))
                    logLines = File.ReadLines(AppEnv.LogFile).TakeLast(80).ToArray();
            }
            catch
            {
                // Günlük dosyası kilitliyse arayüz kopyası kullanılır.
            }

            if (logLines.Length == 0)
            {
                var uiLines = _log.Lines;
                logLines = uiLines.Skip(Math.Max(0, uiLines.Length - 80)).ToArray();
            }

            foreach (var line in logLines)
                sb.AppendLine(line);

            var text = sb.ToString();
            try { Clipboard.SetDataObject(text, true); }
            catch { Clipboard.SetText(text); }

            Ui.Info(FindForm()!, T("Tanılama bilgisi panoya kopyalandı. Bir sorun bildirirken bu metni yapıştırabilirsiniz."));
        }
        catch (Exception ex)
        {
            AppEnv.LogError("Tanılama kopyalanamadı: " + ex.Message);
            Ui.Error(FindForm()!, T("Tanılama kopyalanamadı:\r\n") + ex.Message);
        }
    }
}
