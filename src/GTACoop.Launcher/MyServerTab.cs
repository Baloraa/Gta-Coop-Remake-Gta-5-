using System.Net;
using System.Windows.Forms;
using GTACoop.Sync;

namespace GTACoop.Remake;

public sealed class MyServerTab : UserControl
{
    private readonly TextBox _name = new();
    private readonly NumericUpDown _gamePort = new();
    private readonly NumericUpDown _syncPort = new();
    private readonly NumericUpDown _maxClients = new();
    private readonly TextBox _password = new();
    private readonly TextBox _motd = new();
    private readonly TextBox _token = new();
    private readonly Label _gameServerStatus = new();
    private readonly Label _syncServerStatus = new();
    private readonly Label _addressInfo = new();
    private readonly System.Windows.Forms.Timer _timer = new();

    private MyServer? _server;

    public MyServerTab()
    {
        Dock = DockStyle.Fill;

        var s = MyServerSettings.Load(AppEnv.DataFolder);

        var info = new Label
        {
            Dock = DockStyle.Top,
            AutoSize = false,
            Height = 50,
            Text = T("Kendi GTACoOp sunucunuzu burada yönetin. Addons klasörüne eklediğiniz her şey, sunucuya bağlanan oyunculara otomatik iner.\r\nSunucu modları oyuncuların oyununa yalnızca oynarken kurulur. Arkadaşlarınız 'Sunucular' sekmesinden bağlanır."),
            ForeColor = Color.DimGray,
            Padding = new Padding(4, 4, 4, 4)
        };

        // --- ayarlar
        var settingsTable = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 7,
            Padding = new Padding(8, 4, 8, 4)
        };
        settingsTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
        settingsTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        AddRow(settingsTable, T("Sunucu adı:"), _name, s.ServerName);
        AddRow(settingsTable, T("Oyun portu:"), _gamePort, null, 1, 65535, s.GamePort);
        AddRow(settingsTable, T("Sync portu:"), _syncPort, null, 1, 65535, s.SyncPort);
        AddRow(settingsTable, T("Maks. oyuncu:"), _maxClients, null, 1, 64, s.MaxClients);
        AddRow(settingsTable, T("Şifre (boş=yok):"), _password, s.Password);
        AddRow(settingsTable, T("Motd (karşılama):"), _motd, s.Motd);
        AddRow(settingsTable, T("Sync token:"), _token, s.SyncToken);

        var settingsGroup = new GroupBox
        {
            Text = T("Sunucu Ayarları"),
            Dock = DockStyle.Top,
            Height = 325,
            Padding = new Padding(8)
        };
        var settingsButtons = Ui.Flow(DockStyle.Bottom);
        settingsButtons.Controls.Add(Ui.Btn(T("Ayarları Kaydet"), (_, _) => SaveSettings()));
        settingsButtons.Controls.Add(Ui.Btn(T("Sunucu Klasörünü Aç"), (_, _) => OpenFolder(AppEnv.ServerDir)));
        settingsButtons.Controls.Add(Ui.Btn(T("Addons Klasörünü Aç"), (_, _) =>
            OpenFolder(Path.Combine(AppEnv.ServerDir, "Addons"))));

        settingsGroup.Controls.Add(settingsTable);
        settingsGroup.Controls.Add(settingsButtons);

        // --- modlar
        var modsGroup = new GroupBox
        {
            Text = T("Sunucu Modları"),
            Dock = DockStyle.Top,
            Height = 170,
            Padding = new Padding(8)
        };

        var modsRow1 = Ui.Flow();
        modsRow1.Controls.Add(Ui.Btn(T("DLC Paketi Ekle (araç/ped)..."), (_, _) => AddDlcPack()));
        modsRow1.Controls.Add(Ui.Btn(T("İstemci Scripti Ekle (dll + ini/xml/pdb)..."), (_, _) => AddScriptFiles()));
        modsRow1.Controls.Add(Ui.Btn(T("Script Klasörü Ekle..."), (_, _) => AddScriptFolder()));

        var modsRow2 = Ui.Flow();
        modsRow2.Controls.Add(Ui.Btn(T("ASI Eklentisi Ekle (.asi)..."), (_, _) => AddAsi()));
        modsRow2.Controls.Add(Ui.Btn(T("Sunucu Eklentisi Ekle (plugin dll)..."), (_, _) => AddPlugin()));

        var modsInfo = new Label
        {
            Dock = DockStyle.Bottom,
            AutoSize = false,
            Height = 40,
            ForeColor = Color.DimGray,
            Text = T("DLC paketleri: Addons\\dlcpacks\\  |  Scriptler: Addons\\scripts\\  |  ASI eklentileri: Addons\\asi\\  |  Sunucu eklentileri: Plugins\\\r\nScript/ASI eklentileri yanındaki ini/xml/pdb gibi dosyalarla birlikte eklenir. Meta için: addon.json")
        };
        modsGroup.Controls.Add(modsInfo);
        modsGroup.Controls.Add(modsRow2);
        modsGroup.Controls.Add(modsRow1);

        // --- kontrol
        var controlGroup = new GroupBox
        {
            Text = T("Sunucu Kontrolü"),
            Dock = DockStyle.Top,
            Height = 174,
            Padding = new Padding(8)
        };
        var controlButtons = Ui.Flow();
        controlButtons.Controls.Add(Ui.Btn(T("Sunucuyu Başlat"), (_, _) => StartServer()));
        controlButtons.Controls.Add(Ui.Btn(T("Durdur"), (_, _) => StopServer()));
        controlButtons.Controls.Add(Ui.Btn(T("Durumu Yenile"), (_, _) => RefreshStatus()));

        _gameServerStatus.Text = T("Oyun sunucusu: kapalı");
        _gameServerStatus.AutoSize = true;
        _syncServerStatus.Text = T("Senkronizasyon sunucusu: kapalı");
        _syncServerStatus.AutoSize = true;

        _addressInfo.Dock = DockStyle.Bottom;
        _addressInfo.AutoSize = false;
        _addressInfo.Height = 46;
        _addressInfo.ForeColor = Color.DimGray;

        var statusPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            AutoSize = true
        };
        statusPanel.Controls.Add(_gameServerStatus);
        statusPanel.Controls.Add(_syncServerStatus);

        controlGroup.Controls.Add(_addressInfo);
        controlGroup.Controls.Add(statusPanel);
        controlGroup.Controls.Add(controlButtons);

        // İçerik pencereden uzun olabilir: gerektiğinde dikey kaydırma çubuğu çıksın
        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
        scroll.Controls.Add(controlGroup);
        scroll.Controls.Add(modsGroup);
        scroll.Controls.Add(settingsGroup);
        scroll.Controls.Add(info);
        Controls.Add(scroll);

        _timer.Interval = 1500;
        _timer.Tick += (_, _) => RefreshStatus();
        _timer.Start();

        RefreshStatus();
    }

    private static void AddRow(TableLayoutPanel table, string label, Control control,
        string? value = null, int min = 1, int max = 65535, int numValue = 1)
    {
        table.Controls.Add(new Label { Text = label, AutoSize = true, Padding = new Padding(0, 4, 0, 0) });
        if (control is TextBox tb)
        {
            tb.Text = value ?? "";
            tb.Width = 320;
        }
        else if (control is NumericUpDown num)
        {
            num.Minimum = min;
            num.Maximum = max;
            num.Value = numValue;
            num.Width = 100;
        }
        table.Controls.Add(control);
    }

    private MyServerServerInfo CurrentSettings() => new(
        _name.Text.Trim(),
        (int)_gamePort.Value,
        (int)_syncPort.Value,
        (int)_maxClients.Value,
        _password.Text,
        _motd.Text,
        _token.Text.Trim());

    private readonly record struct MyServerServerInfo(
        string Name, int GamePort, int SyncPort, int MaxClients,
        string Password, string Motd, string Token);

    private MyServerSettings ToSettings(MyServerServerInfo i) => new()
    {
        ServerName = i.Name,
        GamePort = i.GamePort,
        SyncPort = i.SyncPort,
        MaxClients = i.MaxClients,
        Password = i.Password,
        Motd = i.Motd,
        SyncToken = i.Token
    };

    private MyServer Server => _server ??= new MyServer(AppEnv.ServerDir, AppEnv.Log);

    private void SaveSettings()
    {
        try
        {
            var info = CurrentSettings();
            ToSettings(info).Save(AppEnv.DataFolder);
            Server.EnsureLayout();
            AppEnv.Log($"Sunucu ayarları kaydedildi: {info.Name} (oyun: {info.GamePort}, sync: {info.SyncPort})");
        }
        catch (Exception ex)
        {
            AppEnv.LogError(ex.Message);
            Ui.Error(FindForm()!, T("Ayarlar kaydedilemedi:\r\n") + ex.Message);
        }
    }

    private void StartServer()
    {
        try
        {
            var info = CurrentSettings();
            if (string.IsNullOrWhiteSpace(info.Name))
            {
                Ui.Warn(FindForm()!, T("Sunucu adı boş olamaz."));
                return;
            }

            SaveSettings();
            var settings = ToSettings(info);

            Server.EnsureLayout();
            Server.GetOrCreateIdentity(info.Name);
            Server.WriteSettings(settings);

            Server.StartGameServer();
            Server.StartSyncServer(settings);

            AppEnv.Log($"Sunucu başlatıldı: {info.Name} " +
                       $"(oyun portu {info.GamePort}, sync portu {info.SyncPort})");

            RefreshStatus();
        }
        catch (Exception ex)
        {
            AppEnv.LogError(ex.Message);
            Ui.Error(FindForm()!, T("Sunucu başlatılamadı:\r\n") + ex.Message);
        }
    }

    private void StopServer()
    {
        try
        {
            Server.StopAll();
            RefreshStatus();
        }
        catch (Exception ex)
        {
            AppEnv.LogError(ex.Message);
        }
    }

    /// <summary>Launcher kapanırken çağrılır: çalışan sunucu süreçlerini durdurur.</summary>
    public void ShutdownServer()
    {
        try
        {
            _timer.Stop();
            Server.StopAll();
        }
        catch (Exception ex)
        {
            AppEnv.LogError("Sunucu kapatılırken hata: " + ex.Message);
        }
    }

    public void RefreshStatus()
    {
        var info = CurrentSettings();
        bool game = Server.IsGameServerRunning;
        bool sync = Server.IsSyncServerRunning;

        _gameServerStatus.Text = (game ? "✔" : "✘") + " " +
                                 T("Oyun sunucusu: {0} (port {1})", T(game ? "ÇALIŞIYOR" : "kapalı"), info.GamePort);
        _gameServerStatus.ForeColor = game ? Ui.OkColor : Color.DimGray;

        _syncServerStatus.Text = (sync ? "✔" : "✘") + " " +
                                 T("Senkronizasyon sunucusu: {0} (port {1})", T(sync ? "ÇALIŞIYOR" : "kapalı"), info.SyncPort);
        _syncServerStatus.ForeColor = sync ? Ui.OkColor : Color.DimGray;

        var ips = GetLocalIPv4();
        _addressInfo.Text = game || sync
            ? T("Bağlantı adresleri: ") + string.Join(", ", ips.Select(ip =>
                T("{0}:{1} (oyun) / {2}:{3} (sync)", ip, info.GamePort, ip, info.SyncPort)))
            : T("Sunucu kapalıyken modlar değiştirilebilir; başlatınca otomatik güncellenir.");
    }

    private static List<string> GetLocalIPv4()
    {
        try
        {
            return Dns.GetHostAddresses(Dns.GetHostName())
                .Where(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                .Select(a => a.ToString())
                .ToList();
        }
        catch
        {
            return new List<string> { "127.0.0.1" };
        }
    }

    private void OpenFolder(string path)
    {
        try
        {
            Directory.CreateDirectory(path);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            AppEnv.LogError("Klasör açılamadı: " + ex.Message);
        }
    }

    // ------------------------------------------------------------------ mod ekleme

    private void AddDlcPack()
    {
        using var dlg = new FolderBrowserDialog
        {
            Description = T("İçinde dlc.rpf bulunan DLC paketi klasörünü seçin.")
        };
        if (dlg.ShowDialog(FindForm()) != DialogResult.OK) return;

        try
        {
            var name = new DirectoryInfo(dlg.SelectedPath).Name;
            var target = Path.Combine(ServerDirAddons(), "dlcpacks", name);
            if (Directory.Exists(target))
            {
                if (!Ui.Confirm(FindForm()!, T("'{0}' zaten Addons\\dlcpacks içinde. Üzerine yazılsın mı?", name)))
                    return;
                Directory.Delete(target, recursive: true);
            }

            UseWaitCursor = true;
            FileUtil.CopyTree(dlg.SelectedPath, target);
            Ui.Info(FindForm()!, T("DLC paketi eklendi: {0}\r\nSunucu çalışıyorsa bir sonraki manifest isteğinde otomatik dağıtılır.", name));
            AppEnv.Log($"Sunucu DLC paketi eklendi: {name}");
        }
        catch (Exception ex)
        {
            AppEnv.LogError(ex.Message);
            Ui.Error(FindForm()!, T("Paket eklenemedi:\r\n") + ex.Message);
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private void AddScriptFiles()
    {
        using var dlg = new OpenFileDialog
        {
            Title = T("İstemci script dosyalarını seçin (dll ve yanındaki ini/xml/pdb vb.)."),
            Filter = T("Script ve destek dosyaları|*.dll;*.cs;*.vb;*.ini;*.xml;*.pdb;*.json;*.txt;*.log;*.cfg;*.config|Tüm dosyalar (*.*)|*.*"),
            Multiselect = true
        };
        if (dlg.ShowDialog(FindForm()) != DialogResult.OK) return;

        try
        {
            // Grup adı: ilk dll/cs/vb dosyasının adı; yoksa ilk dosyanın adı.
            var main = dlg.FileNames.FirstOrDefault(f =>
                new[] { ".dll", ".cs", ".vb" }.Contains(Path.GetExtension(f).ToLowerInvariant()))
                ?? dlg.FileNames[0];
            var group = Path.GetFileNameWithoutExtension(main);
            var targetDir = Path.Combine(ServerDirAddons(), "scripts", group);
            Directory.CreateDirectory(targetDir);

            foreach (var file in dlg.FileNames)
                File.Copy(file, Path.Combine(targetDir, Path.GetFileName(file)), overwrite: true);

            Ui.Info(FindForm()!, T("{0} dosya eklendi: Addons\\scripts\\{1}\r\nOyunculara bir sonraki bağlantıda otomatik indirilir.",
                dlg.FileNames.Length, group));
            AppEnv.Log($"Sunucu scripti eklendi: {group} ({dlg.FileNames.Length} dosya)");
        }
        catch (Exception ex)
        {
            AppEnv.LogError(ex.Message);
            Ui.Error(FindForm()!, T("Script eklenemedi:\r\n") + ex.Message);
        }
    }

    private void AddScriptFolder()
    {
        using var dlg = new FolderBrowserDialog
        {
            Description = T("Script klasörünü seçin; içindeki tüm dosyalar (dll, ini, alt klasörler) kopyalanır.")
        };
        if (dlg.ShowDialog(FindForm()) != DialogResult.OK) return;

        try
        {
            var name = new DirectoryInfo(dlg.SelectedPath).Name;
            var target = Path.Combine(ServerDirAddons(), "scripts", name);
            if (Directory.Exists(target))
            {
                if (!Ui.Confirm(FindForm()!, T("Addons\\scripts\\{0} zaten var. Üzerine yazılsın mı?", name)))
                    return;
                Directory.Delete(target, recursive: true);
            }

            UseWaitCursor = true;
            FileUtil.CopyTree(dlg.SelectedPath, target);
            Ui.Info(FindForm()!, T("Script klasörü eklendi: {0}\r\nOyunculara bir sonraki bağlantıda otomatik indirilir.", name));
            AppEnv.Log($"Sunucu script klasörü eklendi: {name}");
        }
        catch (Exception ex)
        {
            AppEnv.LogError(ex.Message);
            Ui.Error(FindForm()!, T("Klasör eklenemedi:\r\n") + ex.Message);
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private void AddAsi()
    {
        using var dlg = new OpenFileDialog
        {
            Title = T("ASI eklentisini ve varsa destek dosyalarını (ini/xml) seçin."),
            Filter = T("ASI eklentisi ve destek dosyaları|*.asi;*.ini;*.xml;*.pdb;*.log;*.txt;*.cfg;*.config|Tüm dosyalar (*.*)|*.*"),
            Multiselect = true
        };
        if (dlg.ShowDialog(FindForm()) != DialogResult.OK) return;

        try
        {
            var allowed = ServerScanner.AsiExtensions;
            var files = dlg.FileNames
                .Where(f => allowed.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
                .ToList();
            var rejected = dlg.FileNames.Length - files.Count;

            if (files.Count == 0)
            {
                Ui.Warn(FindForm()!,
                    T("Seçilen dosyalar desteklenmiyor.\r\nDesteklenen uzantılar: {0}", string.Join(", ", ServerScanner.AsiExtensions)));
                return;
            }

            var asi = files.FirstOrDefault(f =>
                Path.GetExtension(f).Equals(".asi", StringComparison.OrdinalIgnoreCase)) ?? files[0];
            var group = Path.GetFileNameWithoutExtension(asi);
            var targetDir = Path.Combine(ServerDirAddons(), "asi", group);
            Directory.CreateDirectory(targetDir);

            foreach (var file in files)
                File.Copy(file, Path.Combine(targetDir, Path.GetFileName(file)), overwrite: true);

            Ui.Info(FindForm()!, T("ASI eklentisi eklendi: {0}\r\n{1}Oyuncuların oyun kök klasörüne oynarken kurulur; oyundan çıkınca otomatik kaldırılır.",
                group, rejected > 0 ? T("{0} dosya desteklenmeyen türde olduğu için atlandı.\r\n", rejected) : ""));
            AppEnv.Log($"Sunucu ASI eklentisi eklendi: {group} ({files.Count} dosya)");
        }
        catch (Exception ex)
        {
            AppEnv.LogError(ex.Message);
            Ui.Error(FindForm()!, T("ASI eklentisi eklenemedi:\r\n") + ex.Message);
        }
    }

    private void AddPlugin()
    {
        using var dlg = new OpenFileDialog
        {
            Title = T("Sunucu eklentisi (plugin) dll dosyalarını seçin."),
            Filter = T("Sunucu eklentisi (*.dll)|*.dll"),
            Multiselect = true
        };
        if (dlg.ShowDialog(FindForm()) != DialogResult.OK) return;

        try
        {
            var pluginsDir = Path.Combine(AppEnv.ServerDir, "Plugins");
            Directory.CreateDirectory(pluginsDir);
            foreach (var file in dlg.FileNames)
                File.Copy(file, Path.Combine(pluginsDir, Path.GetFileName(file)), overwrite: true);

            Ui.Info(FindForm()!, T("Eklenti(ler) Plugins klasörüne eklendi.\r\nSunucuyu yeniden başlatınca yüklenecek (ayarlar otomatik güncellenir)."));
        }
        catch (Exception ex)
        {
            AppEnv.LogError(ex.Message);
            Ui.Error(FindForm()!, T("Eklenti eklenemedi:\r\n") + ex.Message);
        }
    }

    private static string ServerDirAddons() => Path.Combine(AppEnv.ServerDir, "Addons");
}
