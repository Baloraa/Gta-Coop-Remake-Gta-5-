using System.Windows.Forms;
using GTACoop.Sync;

namespace GTACoop.Remake;

public sealed class ServersTab : UserControl
{
    private readonly ListView _list = new();
    private readonly ProgressBar _progress = new();
    private readonly Label _status = new();
    private readonly TextBox _details = new();
    private readonly Button _connectBtn;
    private readonly Button _cancelBtn;
    private readonly CheckBox _sessionMode = new();
    private readonly Label _sessionInfo = new();
    private readonly Button _endSessionBtn;
    private readonly System.Windows.Forms.Timer _sessionTimer = new();
    private CancellationTokenSource? _cts;
    private CancellationTokenSource? _fallbackWatcherCts;
    private bool _busy;

    public ServersTab()
    {
        Dock = DockStyle.Fill;

        var info = new Label
        {
            Dock = DockStyle.Top,
            AutoSize = false,
            Height = 48,
            Text = T("Sunucu listesine sunucu ekleyin (arkadaşınızın IP'si veya kendi sunucunuz), seçip 'Bağlan ve Oyna'ya basın.\r\nSunucunun tüm modları (araç/ped paketleri, scriptler, ASI eklentileri) otomatik indirilir ve yalnızca siz oynarken kurulu kalır; oyundan çıkınca kaldırılır."),
            ForeColor = Color.DimGray,
            Padding = new Padding(4, 4, 4, 4)
        };

        Ui.StyleList(_list,
            (T("Sunucu"), 240), (T("Adres"), 200), (T("Oyun Portu"), 90), ("Sync", 80), (T("Şifre"), 80));
        _list.Height = 190;
        _list.Dock = DockStyle.Top;
        _list.DoubleClick += (_, _) => EditServer();

        var listButtons = Ui.Flow();
        listButtons.Controls.Add(Ui.Btn(T("Kod ile Katıl..."), (_, _) => JoinWithCode()));
        listButtons.Controls.Add(Ui.Btn(T("Sunucu Ekle..."), (_, _) => AddServer()));
        listButtons.Controls.Add(Ui.Btn(T("Düzenle..."), (_, _) => EditServer()));
        listButtons.Controls.Add(Ui.Btn(T("Sil"), (_, _) => DeleteServer()));
        listButtons.Controls.Add(Ui.Btn(T("Yukarı"), (_, _) => MoveServer(-1)));
        listButtons.Controls.Add(Ui.Btn(T("Aşağı"), (_, _) => MoveServer(1)));
        listButtons.Controls.Add(Ui.Btn(T("kendi sunucum (127.0.0.1) ekle"), (_, _) => AddLocalServer()));

        // Bağlantı grubu
        var connectGroup = new GroupBox
        {
            Text = T("Bağlantı"),
            Dock = DockStyle.Top,
            Height = 290,
            Padding = new Padding(10)
        };

        var actions = Ui.Flow();
        _connectBtn = Ui.Btn(T("Bağlan ve Oyna"), (_, _) => _ = ConnectAsync(launch: true));
        _connectBtn.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
        actions.Controls.Add(_connectBtn);
        actions.Controls.Add(Ui.Btn(T("Sunucu Modlarını İndir (ön yükleme)"), (_, _) => _ = PrefetchAsync()));
        actions.Controls.Add(Ui.Btn(T("Yeniden İndir..."), (_, _) => _ = PrefetchAsync(force: true)));
        actions.Controls.Add(Ui.Btn(T("Sadece Oyunu Başlat"), (_, _) => LaunchOnly()));
        _cancelBtn = Ui.Btn(T("İptal"), (_, _) => _cts?.Cancel());
        _cancelBtn.Enabled = false;
        actions.Controls.Add(_cancelBtn);

        _sessionMode.Text = T("Oyun kapanınca sunucu modları otomatik kaldırılsın (oturum modu)");
        _sessionMode.AutoSize = true;
        _sessionMode.Checked = AppEnv.Settings.RemoveModsOnExit;
        _sessionMode.Margin = new Padding(4, 8, 12, 4);
        _sessionMode.CheckedChanged += (_, _) =>
        {
            AppEnv.Settings.RemoveModsOnExit = _sessionMode.Checked;
            AppEnv.SaveSettings();
            AppEnv.Log(_sessionMode.Checked
                ? "Oturum modu açıldı: oyun kapanınca sunucu modları kaldırılacak."
                : "Oturum modu kapatıldı: sunucu modları kurulu kalacak.");
        };

        _endSessionBtn = Ui.Btn(T("Oturumu Bitir (modları kaldır)"), (_, _) => _ = EndSessionAsync());
        _endSessionBtn.Enabled = false;

        _sessionInfo.AutoSize = true;
        _sessionInfo.Margin = new Padding(4, 9, 4, 4);

        var sessionRow = Ui.Flow();
        sessionRow.Controls.Add(_sessionMode);
        sessionRow.Controls.Add(_endSessionBtn);
        sessionRow.Controls.Add(_sessionInfo);

        _progress.Dock = DockStyle.Top;
        _progress.Height = 18;
        _progress.Style = ProgressBarStyle.Continuous;

        _status.Dock = DockStyle.Top;
        _status.AutoSize = false;
        _status.Height = 22;
        _status.ForeColor = Color.DimGray;

        _details.Dock = DockStyle.Fill;
        _details.Multiline = true;
        _details.ReadOnly = true;
        _details.ScrollBars = ScrollBars.Vertical;
        _details.Font = new Font("Consolas", 9f);
        _details.BackColor = Color.WhiteSmoke;

        connectGroup.Controls.Add(_details);
        connectGroup.Controls.Add(_status);
        connectGroup.Controls.Add(sessionRow);
        connectGroup.Controls.Add(_progress);
        connectGroup.Controls.Add(actions);

        Controls.Add(connectGroup);
        Controls.Add(listButtons);
        Controls.Add(_list);
        Controls.Add(info);

        _sessionTimer.Interval = 3000;
        _sessionTimer.Tick += (_, _) => RefreshSessionUi();
        _sessionTimer.Start();

        RefreshList();
        RefreshSessionUi();
    }

    // ------------------------------------------------------------------ liste

    public void RefreshList()
    {
        _list.BeginUpdate();
        _list.Items.Clear();
        foreach (var s in AppEnv.Settings.Servers)
        {
            var item = new ListViewItem(new[]
            {
                s.Name,
                s.Host,
                s.GamePort.ToString(),
                s.SyncPort.ToString(),
                string.IsNullOrEmpty(s.Password) ? "-" : T("var")
            });
            item.Tag = s;
            _list.Items.Add(item);
        }
        _list.EndUpdate();
    }

    private ServerEntry? Selected =>
        _list.SelectedItems.Count > 0 ? (ServerEntry)_list.SelectedItems[0].Tag! : null;

    private void AddServer()
    {
        using var dlg = new ServerEditDialog();
        if (dlg.ShowDialog(FindForm()) != DialogResult.OK) return;
        AppEnv.Settings.Servers.Add(dlg.Entry);
        AppEnv.SaveSettings();
        RefreshList();
        AppEnv.Log($"Sunucu eklendi: {dlg.Entry.Name} ({dlg.Entry.Host})");
    }

    /// <summary>Arkadaşın gönderdiği katılım kodunu çözer, sunucuyu ekler ve istenirse bağlanır.</summary>
    private void JoinWithCode()
    {
        using var dlg = new JoinCodeDialog();
        if (dlg.ShowDialog(FindForm()) != DialogResult.OK || dlg.Entry == null) return;

        var entry = dlg.Entry;
        var existing = AppEnv.Settings.Servers.FirstOrDefault(s =>
            string.Equals(s.Host, entry.Host, StringComparison.OrdinalIgnoreCase) &&
            s.GamePort == entry.GamePort && s.SyncPort == entry.SyncPort);

        if (existing != null)
        {
            existing.Name = entry.Name;
            existing.Password = entry.Password;
            existing.SyncToken = entry.SyncToken;
            entry = existing;
            AppEnv.Log($"Katılım kodu mevcut kaydı güncelledi: {entry.Name} ({entry.Host})");
        }
        else
        {
            AppEnv.Settings.Servers.Add(entry);
            AppEnv.Log($"Katılım kodu ile sunucu eklendi: {entry.Name} " +
                       $"({entry.Host}:{entry.GamePort}, sync {entry.SyncPort})");
        }

        AppEnv.SaveSettings();
        RefreshList();
        foreach (ListViewItem item in _list.Items)
        {
            if (ReferenceEquals(item.Tag, entry))
            {
                item.Selected = true;
                item.Focused = true;
                item.EnsureVisible();
                _list.Select();
                break;
            }
        }

        if (dlg.ConnectRequested) _ = ConnectAsync(launch: true);
    }

    private void EditServer()
    {
        var sel = Selected;
        if (sel == null) { Ui.Warn(FindForm()!, T("Önce bir sunucu seçin.")); return; }

        using var dlg = new ServerEditDialog(sel);
        if (dlg.ShowDialog(FindForm()) != DialogResult.OK) return;
        AppEnv.SaveSettings();
        RefreshList();
    }

    private void DeleteServer()
    {
        var sel = Selected;
        if (sel == null) { Ui.Warn(FindForm()!, T("Önce bir sunucu seçin.")); return; }
        if (!Ui.Confirm(FindForm()!,
            T("'{0}' listeden silinsin mi?\r\n(Kurulu modlar silinmez; 'Araçlar' sekmesinden temizleyebilirsiniz.)", sel.Name))) return;

        AppEnv.Settings.Servers.Remove(sel);
        AppEnv.SaveSettings();
        RefreshList();
    }

    private void MoveServer(int delta)
    {
        var sel = Selected;
        if (sel == null) return;
        var list = AppEnv.Settings.Servers;
        int i = list.IndexOf(sel);
        int j = i + delta;
        if (i < 0 || j < 0 || j >= list.Count) return;
        (list[i], list[j]) = (list[j], list[i]);
        AppEnv.SaveSettings();
        RefreshList();
        foreach (ListViewItem item in _list.Items)
            if (ReferenceEquals(item.Tag, sel)) { item.Selected = true; break; }
    }

    private void AddLocalServer()
    {
        var entry = new ServerEntry
        {
            Name = T("Kendi Sunucum"),
            Host = "127.0.0.1",
            GamePort = 4499,
            SyncPort = 4599
        };
        AppEnv.Settings.Servers.Add(entry);
        AppEnv.SaveSettings();
        RefreshList();
        AppEnv.Log("Yerel sunucu eklendi (127.0.0.1). 'Sunucum' sekmesinden başlatabilirsiniz.");
    }

    // ------------------------------------------------------------------ bağlantı

    private async Task ConnectAsync(bool launch)
    {
        if (_busy) return;
        var sel = Selected;
        if (sel == null) { Ui.Warn(FindForm()!, T("Önce listeden bir sunucu seçin.")); return; }
        if (!MainForm.RequireGameFolder(FindForm()!, out var gameFolder)) return;

        if (AddonApplier.IsGameRunning())
        {
            Ui.Warn(FindForm()!, T("GTA V açık. Modları değiştirmeden önce oyunu kapatın."));
            return;
        }

        // Önceki oturumdan kalıntı varsa önce onu temizle (oyun kapalıyken)
        var leftover = SessionStore.Load(AppEnv.DataFolder);
        if (leftover != null)
        {
            AppEnv.Log("Önceki oturum kalıntıları temizleniyor...");
            SetStatus(T("Önceki oturum kalıntıları temizleniyor..."), Ui.WarnColor);
            SessionService.KillWatcher(leftover, AppEnv.Log);
            await Task.Run(() => SessionService.Finish(AppEnv.DataFolder, gameFolder, AppEnv.Log));
            RefreshSessionUi();
        }

        _busy = true;
        _connectBtn.Enabled = false;
        _endSessionBtn.Enabled = false;
        _cancelBtn.Enabled = true;
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;
        SetStatus(T("Sunucuya bağlanılıyor..."), Ui.WarnColor);
        SetProgress(0);

        try
        {
            var applier = new AddonApplier(gameFolder, AppEnv.Log);
            var progress = new Progress<SyncProgress>(p =>
            {
                SetStatus(p.Message, Color.DimGray);
                SetProgress((int)(p.Fraction * 100));
            });

            var manifest = await Task.Run<Manifest?>(async () =>
            {
                using var client = new SyncClient(sel.Host, sel.SyncPort, sel.SyncToken);

                AppEnv.Log($"Sunucuya bağlanılıyor: {sel.Host}:{sel.SyncPort}");
                var serverName = await client.PingAsync(ct);
                AppEnv.Log($"Sunucu adı: {serverName}");

                var mf = await client.GetManifestAsync(ct);
                var state = SyncStateStore.Load(AppEnv.DataFolder, mf.ServerId);
                var plan = applier.BuildPlan(mf, state);

                ShowDetails(sel, mf, plan, state);

                // Onay (indirilecek varsa)
                if (plan.ToInstall.Count > 0)
                {
                    var mb = plan.ToInstall.Sum(a => a.TotalSize) / 1024.0 / 1024.0;
                    var note = AppEnv.Settings.RemoveModsOnExit
                        ? T("Modlar oyun kapanınca otomatik kaldırılacak (dosyalar bilgisayarınızda kalır).\r\n\r\n")
                        : "";
                    if (plan.LocalSkipped.Count > 0)
                        note += T("Zaten bilgisayarınızda olduğu için kurulmayacak ({0}): {1}\r\n\r\n",
                            plan.LocalSkipped.Count,
                            string.Join(", ", plan.LocalSkipped.Take(5).Select(s => s.Addon.DisplayName)) +
                            (plan.LocalSkipped.Count > 5 ? "..." : ""));
                    bool go = true;
                    Invoke(() => go = Ui.Confirm(FindForm()!,
                        T("Sunucu: {0}\r\n\r\nKurulacak/güncellenecek: {1} eklenti ({2:F1} MB)\r\nKaldırılacak: {3}\r\n\r\n{4}Devam edilsin mi?",
                            mf.ServerName, plan.ToInstall.Count, mb, plan.ToRemove.Count, note)));
                    if (!go)
                    {
                        AppEnv.Log("Kullanıcı senkronizasyonu iptal etti.");
                        return null;
                    }
                }

                var contentDir = AppEnv.ServerContentFolder(sel, mf);
                AppEnv.Log($"Sunucu içerik klasörü: {contentDir}");
                var result = await applier.SyncAsync(client, mf, AppEnv.DataFolder,
                    contentDir, progress, ct);
                AppEnv.Log($"Senkronizasyon tamam: {result.Addons.Count} eklenti kurulu.");
                return (Manifest?)mf;
            }, ct);

            if (manifest == null)
            {
                SetStatus(T("Senkronizasyon kullanıcı tarafından iptal edildi."), Ui.WarnColor);
                SetProgress(0);
                return;
            }

            // İstemci ayarları: oyun açılınca bu sunucuya otomatik bağlan (isteğe göre)
            ClientSettingsEditor.Apply(gameFolder, AppEnv.Settings.Username, sel.Host,
                sel.GamePort, sel.Password, autoConnect: sel.AutoConnect, AppEnv.Log);

            if (launch)
            {
                if (AppEnv.Settings.RemoveModsOnExit)
                {
                    var rec = new SessionRecord
                    {
                        ServerId = manifest.ServerId,
                        ServerName = manifest.ServerName,
                        Host = sel.Host,
                        GamePort = sel.GamePort,
                        StartedUtc = DateTime.UtcNow
                    };
                    SessionStore.Save(AppEnv.DataFolder, rec);
                    EnsureWatcher(rec);

                    SetStatus(T("Oyun açılıyor... Oturum aktif: çıkınca modlar otomatik kaldırılacak."), Ui.OkColor);
                }
                else
                {
                    SetStatus(T("Oyun açılıyor... (Oturum modu kapalı: modlar kurulu kalacak.)"), Ui.OkColor);
                }

                SetProgress(100);
                AppEnv.Log("Oyun başlatılıyor...");
                GameLauncher.Launch(gameFolder, AppEnv.Log);
            }
            else
            {
                SetStatus(T("Hazır: sunucu modları kurulu."), Ui.OkColor);
                SetProgress(100);
            }
        }
        catch (OperationCanceledException)
        {
            SetStatus(T("İşlem iptal edildi."), Ui.WarnColor);
            AppEnv.Log("Senkronizasyon iptal edildi.");
        }
        catch (Exception ex)
        {
            SetStatus(T("Hata: ") + ex.Message, Ui.ErrColor);
            AppEnv.LogError(ex.Message);
            Ui.Error(FindForm()!, ex.Message);
        }
        finally
        {
            _busy = false;
            _connectBtn.Enabled = true;
            _cancelBtn.Enabled = false;
            _cts?.Dispose();
            _cts = null;
            RefreshSessionUi();
        }
    }

    /// <summary>Yalnızca indirme: sunucu modlarını önbelleğe alır, oyuna kurmaz.</summary>
    private async Task PrefetchAsync(bool force = false)
    {
        if (_busy) return;
        var sel = Selected;
        if (sel == null) { Ui.Warn(FindForm()!, T("Önce listeden bir sunucu seçin.")); return; }
        if (!MainForm.RequireGameFolder(FindForm()!, out var gameFolder)) return;

        if (force && !Ui.Confirm(FindForm()!,
                T("Bu sunucunun indirilmiş mod dosyaları silinip sunucudan yeniden indirilecek.\r\n" +
                  "Oyun açıksa önce kapatın.\r\n\r\nDevam edilsin mi?")))
            return;

        _busy = true;
        _connectBtn.Enabled = false;
        _endSessionBtn.Enabled = false;
        _cancelBtn.Enabled = true;
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;
        SetStatus(T(force ? "Mod dosyaları yeniden indiriliyor (oyuna kurulmayacak)..." : "Mod dosyaları indiriliyor (oyuna kurulmayacak)..."), Ui.WarnColor);
        SetProgress(0);

        try
        {
            var applier = new AddonApplier(gameFolder, AppEnv.Log);
            var progress = new Progress<SyncProgress>(p =>
            {
                SetStatus(p.Message, Color.DimGray);
                SetProgress((int)(p.Fraction * 100));
            });

            await Task.Run(async () =>
            {
                using var client = new SyncClient(sel.Host, sel.SyncPort, sel.SyncToken);
                var mf = await client.GetManifestAsync(ct);
                AppEnv.Log($"Ön indirme: {mf.ServerName} ({mf.Addons.Count} eklenti)");
                var contentDir = AppEnv.ServerContentFolder(sel, mf);
                AppEnv.Log($"Sunucu içerik klasörü: {contentDir}");

                if (force)
                {
                    var state = SyncStateStore.Load(AppEnv.DataFolder, mf.ServerId);
                    if (state.Addons.Count > 0)
                        throw new SyncException(
                            T("Bu sunucunun modları şu anda oyuna kurulu. Önce 'Oturumu Bitir' ile kaldırın."));
                    AppEnv.ResetServerContent(contentDir);
                    AppEnv.Log($"Yeniden indirme için sunucu içeriği sıfırlandı: {contentDir}");
                }

                await applier.PrefetchAsync(client, mf, contentDir, progress, ct);
            }, ct);

            SetStatus(T(force
                ? "Tüm mod dosyaları yeniden indirildi (oyuna kurulmadı)."
                : "Tüm mod dosyaları indirildi (oyuna kurulmadı). Bir sonraki girişte yeniden indirilmez."), Ui.OkColor);
            SetProgress(100);
        }
        catch (OperationCanceledException)
        {
            SetStatus(T("İndirme iptal edildi."), Ui.WarnColor);
            AppEnv.Log("Ön indirme iptal edildi.");
        }
        catch (Exception ex)
        {
            SetStatus(T("Hata: ") + ex.Message, Ui.ErrColor);
            AppEnv.LogError(ex.Message);
            Ui.Error(FindForm()!, ex.Message);
        }
        finally
        {
            _busy = false;
            _connectBtn.Enabled = true;
            _cancelBtn.Enabled = false;
            _cts?.Dispose();
            _cts = null;
            RefreshSessionUi();
        }
    }

    // ------------------------------------------------------------------ oturum yönetimi

    /// <summary>Launcher açılışında çağrılır: yarım kalan oturumu kurtarır.</summary>
    public async void RecoverSessionIfAny()
    {
        try
        {
            var rec = SessionStore.Load(AppEnv.DataFolder);
            if (rec == null) { RefreshSessionUi(); return; }

            if (AddonApplier.IsGameRunning())
            {
                AppEnv.Log("Aktif oyun oturumu algılandı; oyun kapanınca sunucu modları kaldırılacak.");
                EnsureWatcher(rec);
                SetStatus(T("Oturum sürüyor: {0} (oyun kapanınca modlar kaldırılır).", rec.ServerName), Ui.WarnColor);
            }
            else
            {
                SetStatus(T("Önceki oturumdan kalan modlar temizleniyor..."), Ui.WarnColor);
                SessionService.KillWatcher(rec, AppEnv.Log);
                await Task.Run(() => SessionService.Finish(AppEnv.DataFolder, AppEnv.Settings.GameFolder, AppEnv.Log));
                SetStatus(T("Önceki oturum kalıntıları temizlendi."), Ui.OkColor);
            }

            RefreshSessionUi();
        }
        catch (Exception ex)
        {
            AppEnv.LogError("Oturum kurtarma hatası: " + ex.Message);
        }
    }

    /// <summary>Bağımsız izleyiciyi başlatır; yoksa launcher içinde izler.</summary>
    private void EnsureWatcher(SessionRecord rec)
    {
        if (SessionService.IsWatcherAlive(rec)) return;

        var pid = SessionService.TryStartWatcher(AppEnv.DataFolder);
        if (pid > 0)
        {
            rec.WatcherPid = pid;
            SessionStore.Save(AppEnv.DataFolder, rec);
            AppEnv.Log($"Oturum izleyicisi başlatıldı (PID {pid}); launcher kapansa bile temizlik yapılır.");
            return;
        }

        // Yedek plan: launcher içinde izle (launcher kapanırsa bir sonraki açılışta kurtarılır).
        AppEnv.Log("Oturum izleyicisi (SyncTool) bulunamadı; izleme launcher içinde yapılacak.");
        _fallbackWatcherCts?.Cancel();
        _fallbackWatcherCts = new CancellationTokenSource();
        var ct = _fallbackWatcherCts.Token;
        var gameFolder = AppEnv.Settings.GameFolder;
        _ = Task.Run(async () =>
        {
            try
            {
                await SessionService.WatchAsync(AppEnv.DataFolder, gameFolder, null, AppEnv.Log, 12, ct);
                BeginInvoke(() =>
                {
                    SetStatus(T("Oturum bitti: sunucu modları kaldırıldı."), Ui.OkColor);
                    RefreshSessionUi();
                });
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                AppEnv.LogError("Oturum izleme hatası: " + ex.Message);
            }
        });
    }

    private async Task EndSessionAsync()
    {
        var rec = SessionStore.Load(AppEnv.DataFolder);
        if (rec == null) { RefreshSessionUi(); return; }

        if (AddonApplier.IsGameRunning())
        {
            Ui.Warn(FindForm()!, T("Oyun açıkken oturum bitirilemez. Önce oyunu kapatın."));
            return;
        }

        if (!Ui.Confirm(FindForm()!,
            T("'{0}' oturumu bitirilecek ve sunucu modları oyun klasöründen kaldırılacak.\r\nKendi modlarınız ve diğer dosyalar etkilenmez. Devam edilsin mi?", rec.ServerName)))
            return;

        try
        {
            _endSessionBtn.Enabled = false;
            _fallbackWatcherCts?.Cancel();
            SessionService.KillWatcher(rec, AppEnv.Log);
            await Task.Run(() => SessionService.Finish(AppEnv.DataFolder, AppEnv.Settings.GameFolder, AppEnv.Log));
            SetStatus(T("Oturum bitirildi; sunucu modları kaldırıldı."), Ui.OkColor);
        }
        catch (Exception ex)
        {
            AppEnv.LogError(ex.Message);
            Ui.Error(FindForm()!, T("Oturum bitirilemedi:\r\n") + ex.Message);
        }
        finally
        {
            RefreshSessionUi();
        }
    }

    private void RefreshSessionUi()
    {
        var rec = SessionStore.Load(AppEnv.DataFolder);
        bool active = rec != null;
        _endSessionBtn.Enabled = active && !_busy;

        if (active)
        {
            bool oyun = AddonApplier.IsGameRunning();
            _sessionInfo.Text = "● " + T("OTURUM AKTİF: {0} — {1}", rec!.ServerName,
                T(oyun ? "oyun çalışıyor; çıkınca modlar kaldırılacak." : "oyun bekleniyor..."));
            _sessionInfo.ForeColor = Ui.WarnColor;
        }
        else
        {
            _sessionInfo.Text = T("");
        }
    }

    private void ShowDetails(ServerEntry sel, Manifest manifest, AddonApplier.InstallPlan plan, SyncState state)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine(T("Sunucu : {0}  ({1}:{2})", manifest.ServerName, sel.Host, sel.SyncPort));
        sb.AppendLine(T("Kimlik : {0}", manifest.ServerId));
        sb.AppendLine(T("Eklenti: {0}  |  kurulacak: {1}  |  güncel: {2}  |  kaldırılacak: {3}  |  sizde var: {4}",
            manifest.Addons.Count, plan.ToInstall.Count, plan.UpToDate.Count, plan.ToRemove.Count,
            plan.LocalSkipped.Count));
        sb.AppendLine(new string('-', 70));

        foreach (var a in manifest.Addons)
        {
            string status;
            if (a.Type == AddonType.ServerPlugin) status = T("sunucu tarafı");
            else if (plan.LocalSkipped.Any(s => s.Addon.Id == a.Id)) status = T("SİZDE VAR");
            else if (plan.ToInstall.Contains(a)) status = state.Addons.Any(x => x.Id == a.Id)
                ? T("GÜNCELLENECEK")
                : T("KURULACAK");
            else status = T("güncel");

            var typeName = a.Type switch
            {
                AddonType.DlcPack => T("DLC paketi"),
                AddonType.ClientScript => "script",
                AddonType.AsiPlugin => T("ASI eklentisi"),
                AddonType.ClientFiles => T("dosya"),
                AddonType.ServerPlugin => T("sunucu eklentisi"),
                _ => "?"
            };

            sb.AppendLine($"[{status,-14}] {a.DisplayName} ({typeName}, {a.TotalSize / 1024.0 / 1024.0:F1} MB)");
            if (!string.IsNullOrEmpty(a.Description))
                sb.AppendLine($"                 {a.Description}");
        }

        if (plan.ToRemove.Count > 0)
        {
            sb.AppendLine(new string('-', 70));
            sb.AppendLine(T("Kaldırılacak (sunucudan çıkarılmış):"));
            foreach (var r in plan.ToRemove)
                sb.AppendLine($"  - {r.Id}");
        }

        var text = sb.ToString();
        if (InvokeRequired) BeginInvoke(() => _details.Text = text);
        else _details.Text = text;
    }

    private void LaunchOnly()
    {
        if (!MainForm.RequireGameFolder(FindForm()!, out var gameFolder)) return;
        if (AddonApplier.IsGameRunning()) { Ui.Warn(FindForm()!, T("GTA V zaten açık.")); return; }

        try
        {
            ClientSettingsEditor.DisableAutoConnect(gameFolder, AppEnv.Log);
            GameLauncher.Launch(gameFolder, AppEnv.Log);
        }
        catch (Exception ex)
        {
            AppEnv.LogError(ex.Message);
            Ui.Error(FindForm()!, ex.Message);
        }
    }

    private void SetStatus(string text, Color color)
    {
        if (InvokeRequired) { BeginInvoke(() => SetStatus(text, color)); return; }
        _status.Text = text;
        _status.ForeColor = color;
    }

    private void SetProgress(int value)
    {
        if (InvokeRequired) { BeginInvoke(() => SetProgress(value)); return; }
        _progress.Value = Math.Clamp(value, 0, 100);
    }
}
