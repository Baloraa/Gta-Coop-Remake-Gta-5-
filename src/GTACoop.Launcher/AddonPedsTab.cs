using System.Diagnostics;
using System.IO.Compression;
using System.Windows.Forms;
using GTACoop.Sync;

namespace GTACoop.Remake;

/// <summary>
/// Sunucunun Addons\peds klasöründeki gevşek ped dosyalarını yönetir.
/// Dosyalar AddonPeds uyumlu tek bir peds.rpf içinde paketlenir; kullanıcının
/// kendi AddonPeds kurulumuna ve ap_m.xml dosyasına dokunulmaz.
/// </summary>
public sealed class AddonPedsTab : UserControl
{
    private readonly ListView _list = new();
    private readonly Label _status = new();

    public AddonPedsTab()
    {
        Dock = DockStyle.Fill;

        var info = new Label
        {
            Dock = DockStyle.Top,
            AutoSize = false,
            Height = 58,
            ForeColor = Color.DimGray,
            Padding = new Padding(6, 6, 6, 0),
            Text = T("Sunucunun Addons\\peds klasöründeki ped dosyalarını (.ydd/.ytd/.ymt/.yft) " +
                     "buradan yönetin. Pedler tek bir peds.rpf içinde paketlenir; kullanıcının " +
                     "kendi AddonPeds kurulumu ve ap_m.xml dosyası değiştirilmez.")
        };

        var listLabel = new Label
        {
            Dock = DockStyle.Top,
            Height = 24,
            Text = T("Pedler (işaretli = oyuncu menüsünde görünür):"),
            ForeColor = Color.DimGray,
            Padding = new Padding(6, 6, 0, 0)
        };

        Ui.StyleList(_list,
            (T("Ped"), 210),
            (T("Model"), 190),
            (T("Tür"), 80),
            (T("Streamed"), 80),
            (T("Sağlık"), 65),
            (T("Dosya"), 55),
            (T("Paket"), 170));
        _list.Dock = DockStyle.Fill;
        _list.CheckBoxes = true;
        _list.MultiSelect = true;
        _list.FullRowSelect = true;
        _list.HeaderStyle = ColumnHeaderStyle.Nonclickable;
        _list.SelectedIndexChanged += (_, _) => UpdateStatus();
        _list.DoubleClick += (_, _) => EditSelected();
        // ItemChecked, liste programatik olarak doldurulurken de tetiklenir
        // ve ped ayarlarını yanlışlıkla değiştirebilir. Bunun yerine yalnızca
        // kullanıcının fare/klavye eylemini dinle; onay durumunu eylem
        // tamamlandıktan sonra oku.
        _list.MouseUp += OnListMouseUp;
        _list.KeyUp += OnListKeyUp;

        var buttons = Ui.Flow();
        buttons.Controls.Add(Ui.Btn(T("Ped Ekle..."), (_, _) => AddPedFiles()));
        buttons.Controls.Add(Ui.Btn(T("Streamed Ped Klasörü Ekle..."), (_, _) => AddPedFolder()));
        buttons.Controls.Add(Ui.Btn(T("İçe Aktar..."), (_, _) => ShowImportMenu()));
        buttons.Controls.Add(Ui.Btn(T("Düzenle..."), (_, _) => EditSelected()));
        buttons.Controls.Add(Ui.Btn(T("Görüntüle..."), (_, _) => ViewSelected()));
        buttons.Controls.Add(Ui.Btn(T("Kaldır..."), (_, _) => RemoveSelected()));
        buttons.Controls.Add(Ui.Btn(T("Yenile"), (_, _) => RefreshList()));
        buttons.Controls.Add(Ui.Btn(T("Yedekleri Aç"), (_, _) => OpenFolder(BackupsDir)));
        buttons.Controls.Add(Ui.Btn(T("Ped Klasörünü Aç"), (_, _) => OpenFolder(PedsDir)));

        _status.Dock = DockStyle.Bottom;
        _status.Height = 26;
        _status.TextAlign = ContentAlignment.MiddleLeft;
        _status.ForeColor = Color.DimGray;
        _status.Padding = new Padding(6, 0, 0, 0);

        var hint = new Label
        {
            Dock = DockStyle.Bottom,
            AutoSize = false,
            Height = 58,
            ForeColor = Color.DimGray,
            Padding = new Padding(6, 2, 6, 0),
            Text = T("Ped Ekle: .yft/.ydd/.ytd/.ymt dosyalarını seçin. Streamed pedler için " +
                     "bileşen klasörünü kullanın. Hayvan pedleri (a_c_...) otomatik algılanır; " +
                     "tür ve meta verileri Düzenle ile değiştirebilirsiniz.")
        };

        var body = new Panel { Dock = DockStyle.Fill };
        body.Controls.Add(_list);
        body.Controls.Add(listLabel);

        Controls.Add(body);
        Controls.Add(_status);
        Controls.Add(hint);
        Controls.Add(buttons);
        Controls.Add(info);

        RefreshList();
    }

    private static string PedsDir => Path.Combine(AppEnv.ServerDir, "Addons", AddonPeds.DirectoryName);
    private static string BackupsDir => Path.Combine(AppEnv.ServerDir, "Addons-Removed");
    private static string T(string text, params object?[] args) => Loc.T(text, args);

    private sealed class PedRow
    {
        public string PackPath { get; init; } = "";
        public string PackId { get; init; } = "";
        public string PackName { get; init; } = "";
        public string Model { get; init; } = "";
        public string Name { get; init; } = "";
        public string Kind { get; init; } = "human";
        public bool Enabled { get; set; }
        public bool Streamed { get; init; }
        public int Health { get; init; }
        public int Files { get; init; }
    }

    public void RefreshList()
    {
        try
        {
            UseWaitCursor = true;
            Directory.CreateDirectory(PedsDir);
            var rows = new List<PedRow>();

            foreach (var packDir in EnumeratePackDirs())
            {
                try
                {
                    var loose = AddonPeds.DiscoverLoosePeds(packDir, msg => AppEnv.Log(msg));
                    if (loose.Count == 0) continue;

                    var packFile = AddonPeds.LoadOrCreatePackFile(packDir, loose);
                    var id = Path.GetFileName(packDir);
                    var packName = string.IsNullOrWhiteSpace(packFile.DisplayName)
                        ? id
                        : packFile.DisplayName;

                    foreach (var ped in packFile.Peds)
                    {
                        rows.Add(new PedRow
                        {
                            PackPath = packDir,
                            PackId = id,
                            PackName = packName,
                            Model = ped.Model,
                            Name = string.IsNullOrWhiteSpace(ped.Name) ? ped.Model : ped.Name,
                            Kind = ped.Kind,
                            Enabled = ped.Enabled,
                            Streamed = ped.Streamed,
                            Health = ped.Health <= 0 ? 200 : ped.Health,
                            Files = ped.Files.Count
                        });
                    }
                }
                catch (Exception ex)
                {
                    AppEnv.LogError($"Ped paketi okunamadı ({Path.GetFileName(packDir)}): {ex.Message}");
                }
            }

            var selected = _list.SelectedItems.Cast<ListViewItem>()
                .Select(i => (i.Tag as PedRow)?.PackPath + "|" + (i.Tag as PedRow)?.Model)
                .Where(s => s != null)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            _list.BeginUpdate();
            _list.Items.Clear();
            foreach (var row in rows
                         .OrderBy(r => r.PackName, StringComparer.CurrentCultureIgnoreCase)
                         .ThenBy(r => r.Kind == "animal" ? 1 : 0)
                         .ThenBy(r => r.Name, StringComparer.CurrentCultureIgnoreCase))
            {
                var item = new ListViewItem(row.Name) { Tag = row };
                item.SubItems.Add(row.Model);
                item.SubItems.Add(T(row.Kind == "animal" ? "Hayvan" : row.Kind == "female" ? "Kadın" : "İnsan"));
                item.SubItems.Add(row.Streamed ? T("Evet") : T("Hayır"));
                item.SubItems.Add(row.Health.ToString());
                item.SubItems.Add(row.Files.ToString());
                item.SubItems.Add(row.PackName);
                _list.Items.Add(item);
                item.Checked = row.Enabled;
                if (selected.Contains(row.PackPath + "|" + row.Model))
                    item.Selected = true;
            }
            _list.EndUpdate();
            UpdateStatus();
        }
        catch (Exception ex)
        {
            AppEnv.LogError("Ped listesi yenilenemedi: " + ex.Message);
            Ui.Error(FindForm()!, T("Ped listesi yenilenemedi:\r\n") + ex.Message);
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private static IEnumerable<string> EnumeratePackDirs()
    {
        try
        {
            return Directory.GetDirectories(PedsDir)
                .Where(d => !Path.GetFileName(d).StartsWith('.'))
                .ToList();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private void OnListMouseUp(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        var hit = _list.HitTest(e.Location);
        if (hit.Item?.Tag is not PedRow row) return;
        if ((hit.Location & ListViewHitTestLocations.StateImage) == 0) return;
        var item = hit.Item;
        // Onay kutusu durumu fare olayı işlendikten sonra değişir; kaydetmeyi
        // bir sonraki UI turuna bırak ve güncel durumu oku.
        BeginInvoke(new Action(() => ApplyCheck(row, item)));
    }

    private void OnListKeyUp(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode != Keys.Space) return;
        var items = _list.SelectedItems.Cast<ListViewItem>()
            .Where(i => i.Tag is PedRow)
            .ToList();
        BeginInvoke(new Action(() =>
        {
            foreach (var item in items)
                if (item.Tag is PedRow row) ApplyCheck(row, item);
        }));
    }

    private void ApplyCheck(PedRow row, ListViewItem item)
    {
        var wanted = item.Checked;
        if (wanted == row.Enabled) return;

        try
        {
            var path = Path.Combine(row.PackPath, AddonPeds.PackFileName);
            var file = SyncJson.Load<AddonPedPackFile>(path)
                       ?? new AddonPedPackFile();
            file.Peds ??= new List<AddonPedInfo>();
            var ped = file.Peds.FirstOrDefault(p =>
                string.Equals(p.Model, row.Model, StringComparison.OrdinalIgnoreCase));
            if (ped == null)
            {
                item.Checked = row.Enabled;
                return;
            }

            ped.Enabled = wanted;
            row.Enabled = wanted;
            SyncJson.Save(path, file);
            AppEnv.Log((wanted ? "Ped etkinleştirildi: " : "Ped devre dışı: ") +
                       row.Name + " (" + row.PackName + ")");
            UpdateStatus();
        }
        catch (Exception ex)
        {
            item.Checked = row.Enabled;
            AppEnv.LogError("Ped ayarı kaydedilemedi: " + ex);
            Ui.Error(FindForm()!, T("Ped ayarı kaydedilemedi:\r\n") + ex.Message);
        }
    }

    private void UpdateStatus()
    {
        var rows = _list.Items.Cast<ListViewItem>()
            .Select(i => i?.Tag as PedRow)
            .Where(r => r != null)
            .Cast<PedRow>()
            .ToList();
        var total = rows.Count;
        var enabled = rows.Count(r => r.Enabled);
        var packs = rows.Select(r => r.PackPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
        _status.Text = T("Paket: {0} | Ped: {1} | Etkin: {2} | Devre dışı: {3}",
            packs, total, enabled, total - enabled);
    }

    private string? EnsureTargetPack()
    {
        var selected = SelectedPackDir();
        if (selected != null) return selected;

        var packs = EnumeratePackDirs().ToList();
        if (packs.Count == 1) return packs[0];
        if (packs.Count > 1)
        {
            Ui.Info(FindForm()!, T("Birden çok ped paketi var. Önce listeden bir ped seçin."));
            return null;
        }

        using var dlg = new PedTextPromptDialog(T("Yeni Ped Paketi"),
            T("Paket adı:"), "peds");
        if (dlg.ShowDialog(FindForm()) != DialogResult.OK) return null;

        var name = AddonApplier.Safe(dlg.Value);
        if (name.Length == 0) name = "peds";
        var target = Path.Combine(PedsDir, name);
        Directory.CreateDirectory(target);
        return target;
    }

    private void AddPedFiles()
    {
        var packDir = EnsureTargetPack();
        if (packDir == null) return;

        using var dlg = new OpenFileDialog
        {
            Title = T("Eklenecek ped dosyalarını seçin."),
            Filter = T("Ped dosyaları (*.ydd;*.ytd;*.ymt;*.yft)|*.ydd;*.ytd;*.ymt;*.yft|" +
                       "Tüm dosyalar (*.*)|*.*"),
            Multiselect = true
        };
        if (dlg.ShowDialog(FindForm()) != DialogResult.OK) return;

        var files = dlg.FileNames.Where(AddonPeds.IsPedFile).ToList();
        if (files.Count == 0)
        {
            Ui.Warn(FindForm()!, T("Seçilen dosyalar arasında ped dosyası yok."));
            return;
        }

        var hasModelFile = files.Any(f =>
            Path.GetExtension(f).Equals(".yft", StringComparison.OrdinalIgnoreCase) ||
            Path.GetExtension(f).Equals(".ymt", StringComparison.OrdinalIgnoreCase));
        if (!hasModelFile && files.Any(f => LooksLikeComponent(Path.GetFileName(f))))
        {
            Ui.Warn(FindForm()!, T(
                "Seçilen dosyalar ped bileşeni gibi görünüyor. Streamed pedler için " +
                "'Streamed Ped Klasörü Ekle...' düğmesini kullanın."));
            return;
        }

        var existing = files.Count(f =>
            File.Exists(Path.Combine(packDir, Path.GetFileName(f))));
        if (existing > 0 && !Ui.Confirm(FindForm()!, T(
                "{0} dosya zaten pakette. Üzerine yazılsın mı?", existing)))
            return;

        try
        {
            UseWaitCursor = true;
            foreach (var file in files)
            {
                var target = Path.Combine(packDir, Path.GetFileName(file));
                File.Copy(file, target, overwrite: true);
            }

            var models = files.Select(ModelFromFile)
                .Where(m => m.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            AppEnv.Log($"Ped dosyaları eklendi: {Path.GetFileName(packDir)} ({files.Count} dosya)");
            RefreshList();

            var single = models.Count == 1 ? models[0] : null;
            if (single != null)
                EditPed(packDir, single, true);
            else
                Ui.Info(FindForm()!, T("Ped dosyaları eklendi: {0} dosya. " +
                    "Yeni pedleri listeden Düzenle ile ayarlayabilirsiniz.", files.Count));
        }
        catch (Exception ex)
        {
            AppEnv.LogError(ex.Message);
            Ui.Error(FindForm()!, T("Ped dosyaları eklenemedi:\r\n") + ex.Message);
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private static string ModelFromFile(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        var caret = name.IndexOf('^');
        if (caret > 0) name = name[..caret];
        return name.Trim().ToLowerInvariant();
    }

    private static bool LooksLikeComponent(string fileName)
    {
        var name = Path.GetFileNameWithoutExtension(fileName).ToLowerInvariant();
        var caret = name.IndexOf('^');
        if (caret >= 0) name = name[(caret + 1)..];
        string[] prefixes =
        {
            "head_", "feet_", "hand_", "lowr_", "uppr_", "jbib_", "decl_", "teef_",
            "berd_", "hair_", "task_", "accs_", "p_head", "p_eyes", "p_ears",
            "p_mouth", "p_lips", "p_teeth"
        };
        return prefixes.Any(p => name.StartsWith(p, StringComparison.Ordinal));
    }

    private void AddPedFolder()
    {
        var packDir = EnsureTargetPack();
        if (packDir == null) return;

        using var dlg = new FolderBrowserDialog
        {
            Description = T("Bileşenlerini içeren tek bir streamed ped klasörü seçin.")
        };
        if (dlg.ShowDialog(FindForm()) != DialogResult.OK) return;

        try
        {
            UseWaitCursor = true;
            var folder = dlg.SelectedPath.TrimEnd(Path.DirectorySeparatorChar);
            if (!Directory.EnumerateFiles(folder, "*", SearchOption.TopDirectoryOnly)
                    .Any(AddonPeds.IsPedFile))
            {
                Ui.Warn(FindForm()!, T(
                    "Seçilen klasörün içinde doğrudan ped dosyası yok. Birden çok ped " +
                    "içeren bir paket için 'İçe Aktar...' seçeneğini kullanın."));
                return;
            }

            var name = AddonApplier.Safe(Path.GetFileName(folder));
            if (name.Length == 0) name = "ped";
            var target = Path.Combine(packDir, name);
            if (Directory.Exists(target) &&
                !Ui.Confirm(FindForm()!, T(
                    "'{0}' klasörü zaten var. Dosyalar birleştirilsin mi?", name)))
                return;

            FileUtil.CopyTree(folder, target);
            AppEnv.Log($"Streamed ped klasörü eklendi: {name}");
            RefreshList();
            EditPed(packDir, name, true);
        }
        catch (Exception ex)
        {
            AppEnv.LogError(ex.Message);
            Ui.Error(FindForm()!, T("Ped klasörü eklenemedi:\r\n") + ex.Message);
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private void ShowImportMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add(T("Klasörden İçe Aktar..."), null, (_, _) => ImportPackFolder());
        menu.Items.Add(T("Arşivden İçe Aktar (zip/rar/7z)..."), null, (_, _) => ImportPackArchive());
        menu.Show(Cursor.Position);
    }

    private void ImportPackFolder()
    {
        using var dlg = new FolderBrowserDialog
        {
            Description = T("İçe aktarılacak ped paketini seçin (dlc.rpf veya gevşek dosyalar).")
        };
        if (dlg.ShowDialog(FindForm()) != DialogResult.OK) return;

        try
        {
            UseWaitCursor = true;
            var source = ResolvePackSource(dlg.SelectedPath);
            if (source == null)
            {
                Ui.Warn(FindForm()!, T("Seçilen klasörde ped dosyası bulunamadı."));
                return;
            }

            var name = new DirectoryInfo(source).Name;
            if (name.Length == 0 || source == Path.GetPathRoot(source))
                name = "peds";
            ImportPack(source, name);
            RefreshList();
        }
        catch (Exception ex)
        {
            AppEnv.LogError(ex.Message);
            Ui.Error(FindForm()!, T("Ped paketi eklenemedi:\r\n") + ex.Message);
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private void ImportPackArchive()
    {
        using var dlg = new OpenFileDialog
        {
            Title = T("Addon ped arşivini seçin."),
            Filter = T("Ped arşivi (*.zip;*.rar;*.7z)|*.zip;*.rar;*.7z|Tüm dosyalar (*.*)|*.*"),
            Multiselect = true
        };
        if (dlg.ShowDialog(FindForm()) != DialogResult.OK) return;

        int added = 0;
        var errors = new List<string>();
        try
        {
            UseWaitCursor = true;
            foreach (var archive in dlg.FileNames)
            {
                var temp = Path.Combine(Path.GetTempPath(),
                    "gtacoop-ped-" + Guid.NewGuid().ToString("N"));
                try
                {
                    Directory.CreateDirectory(temp);
                    ExtractArchive(archive, temp);
                    var source = ResolvePackSource(temp);
                    if (source == null)
                        throw new InvalidDataException("Arşivde ped dosyası bulunamadı.");

                    ImportPack(source, Path.GetFileNameWithoutExtension(archive));
                    added++;
                }
                catch (Exception ex)
                {
                    errors.Add(Path.GetFileName(archive) + ": " + ex.Message);
                }
                finally
                {
                    try { Directory.Delete(temp, recursive: true); } catch { }
                }
            }

            if (added > 0)
            {
                Ui.Info(FindForm()!, T(
                    "Ped paketi eklendi: {0}\r\nSunucu çalışıyorsa bir sonraki manifest " +
                    "isteğinde otomatik dağıtılır.", added));
                AppEnv.Log($"Sunucu ped paketi eklendi: {added} paket");
            }
            if (errors.Count > 0)
                Ui.Warn(FindForm()!, T("Bazı ped paketleri eklenemedi:\r\n") +
                    string.Join("\r\n", errors.Take(6)));
            RefreshList();
        }
        catch (Exception ex)
        {
            AppEnv.LogError(ex.Message);
            Ui.Error(FindForm()!, T("Ped paketi eklenemedi:\r\n") + ex.Message);
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private static string? ResolvePackSource(string folder)
    {
        var dlc = AddonPeds.FindDlcRpf(folder);
        if (dlc != null) return Path.GetDirectoryName(dlc);

        if (Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories)
            .Any(AddonPeds.IsPedFile))
            return folder;

        // Arşiv tek bir üst klasör içeriyorsa onun içine bak.
        var dirs = Directory.GetDirectories(folder)
            .Where(d => !Path.GetFileName(d).StartsWith('.'))
            .ToList();
        if (dirs.Count == 1)
        {
            var inner = ResolvePackSource(dirs[0]);
            if (inner != null) return inner;
        }
        return null;
    }

    private void ImportPack(string sourceDir, string name)
    {
        name = AddonApplier.Safe(name);
        if (name.Length == 0) name = "peds";
        var target = Path.Combine(PedsDir, name);
        var sourceFull = Path.GetFullPath(sourceDir).TrimEnd(Path.DirectorySeparatorChar);
        var targetFull = Path.GetFullPath(target).TrimEnd(Path.DirectorySeparatorChar);
        if (string.Equals(sourceFull, targetFull, StringComparison.OrdinalIgnoreCase))
        {
            AddonPeds.EnsureLooseSource(target);
            return;
        }

        string? backup = null;
        if (Directory.Exists(target))
        {
            if (!Ui.Confirm(FindForm()!, T(
                    "'{0}' zaten Addons\\peds içinde. Mevcut paket yedeklere taşınıp " +
                    "yenisi eklensin mi?", name)))
                return;
            backup = MoveDirectoryToBackup(target, name);
        }

        try
        {
            Directory.CreateDirectory(target);
            FileUtil.CopyTree(sourceDir, target);
            AddonPeds.EnsureLooseSource(target,
                msg => AppEnv.Log(msg));

            var loose = AddonPeds.DiscoverLoosePeds(target,
                msg => AppEnv.Log(msg), extractLegacy: false);
            if (loose.Count == 0)
                throw new InvalidDataException(
                    "Pakette ped dosyası (.ydd/.ytd/.ymt/.yft) bulunamadı.");

            // Kaynak arşivler gevşek dosyalara açıldı; hedefte kopyasını tutma.
            foreach (var rpf in Directory.EnumerateFiles(target, "*.rpf",
                         SearchOption.AllDirectories))
            {
                try { File.Delete(rpf); } catch { }
            }

            AddonPeds.LoadOrCreatePackFile(target, loose);
            AppEnv.Log($"Sunucu ped paketi eklendi: {name} ({loose.Count} ped)");
        }
        catch
        {
            try
            {
                if (Directory.Exists(target)) Directory.Delete(target, recursive: true);
                if (backup != null && Directory.Exists(backup))
                    Directory.Move(backup, target);
            }
            catch { }
            throw;
        }
    }

    private static string MoveDirectoryToBackup(string sourceDir, string name)
    {
        var backupRoot = Path.Combine(BackupsDir,
            DateTime.Now.ToString("yyyyMMdd-HHmmss-fff"), "peds");
        Directory.CreateDirectory(backupRoot);
        var target = Path.Combine(backupRoot, name);
        Directory.Move(sourceDir, target);
        return target;
    }

    private AddonPedInfo? SelectedPed(out string? packDir)
    {
        packDir = null;
        var row = _list.SelectedItems.Cast<ListViewItem>()
            .Select(i => i.Tag as PedRow)
            .FirstOrDefault(r => r != null);
        if (row == null)
        {
            Ui.Info(FindForm()!, T("Önce listeden bir ped seçin."));
            return null;
        }

        packDir = row.PackPath;
        return LoadPed(row.PackPath, row.Model);
    }

    private static AddonPedInfo? LoadPed(string packDir, string model)
    {
        var loose = AddonPeds.DiscoverLoosePeds(packDir);
        var packFile = AddonPeds.LoadOrCreatePackFile(packDir, loose);
        return packFile.Peds.FirstOrDefault(p =>
            string.Equals(p.Model, model, StringComparison.OrdinalIgnoreCase));
    }

    private void EditSelected()
    {
        var ped = SelectedPed(out var packDir);
        if (ped == null || packDir == null) return;
        EditPed(packDir, ped.Model, false);
    }

    public void EditPed(string packDir, string model, bool isNew)
    {
        try
        {
            var ped = LoadPed(packDir, model);
            if (ped == null)
            {
                Ui.Warn(FindForm()!, T("Ped bulunamadı: {0}", model));
                return;
            }

            using var dlg = new PedEditDialog(ped, isNew);
            if (dlg.ShowDialog(FindForm()) != DialogResult.OK) return;

            var path = Path.Combine(packDir, AddonPeds.PackFileName);
            var file = SyncJson.Load<AddonPedPackFile>(path) ?? new AddonPedPackFile();
            var stored = file.Peds.FirstOrDefault(p =>
                string.Equals(p.Model, model, StringComparison.OrdinalIgnoreCase));
            if (stored == null) return;

            stored.Name = dlg.Ped.Name;
            stored.Kind = dlg.Ped.Kind;
            stored.Health = dlg.Ped.Health;
            stored.Enabled = dlg.Ped.Enabled;
            stored.ClipDictionary = dlg.Ped.ClipDictionary;
            stored.MovementClipSet = dlg.Ped.MovementClipSet;
            stored.DecisionMaker = dlg.Ped.DecisionMaker;
            stored.Personality = dlg.Ped.Personality;
            stored.CombatInfo = dlg.Ped.CombatInfo;
            SyncJson.Save(path, file);
            AppEnv.Log($"Ped güncellendi: {stored.Name} ({stored.Model})");
            RefreshList();
        }
        catch (Exception ex)
        {
            AppEnv.LogError(ex.Message);
            Ui.Error(FindForm()!, T("Ped güncellenemedi:\r\n") + ex.Message);
        }
    }

    private void ViewSelected()
    {
        var ped = SelectedPed(out var packDir);
        if (ped == null || packDir == null) return;

        try
        {
            var dest = Path.Combine(Path.GetTempPath(), "GTACoop-PedView",
                AddonApplier.Safe(Path.GetFileName(packDir)), AddonApplier.Safe(ped.Model));
            if (Directory.Exists(dest)) Directory.Delete(dest, recursive: true);
            Directory.CreateDirectory(dest);
            var count = AddonPeds.ExtractPed(packDir, ped, dest);
            if (count == 0)
            {
                Ui.Warn(FindForm()!, T("Ped dosyaları bulunamadı."));
                return;
            }

            OpenFolder(dest);
            AppEnv.Log($"Ped görüntülendi: {ped.Model} ({count} dosya)");
        }
        catch (Exception ex)
        {
            AppEnv.LogError(ex.Message);
            Ui.Error(FindForm()!, T("Ped görüntülenemedi:\r\n") + ex.Message);
        }
    }

    private void RemoveSelected()
    {
        var rows = _list.SelectedItems.Cast<ListViewItem>()
            .Select(i => i.Tag as PedRow)
            .Where(r => r != null)
            .GroupBy(r => r!.PackPath + "|" + r.Model, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First()!)
            .ToList();
        if (rows.Count == 0)
        {
            Ui.Info(FindForm()!, T("Kaldırmak için listeden en az bir ped seçin."));
            return;
        }

        var names = string.Join("\r\n", rows.Take(12)
            .Select(r => "• " + r.Name + " (" + r.PackName + ")"));
        if (!Ui.Confirm(FindForm()!, T(
                "Seçilen pedler sunucudan kaldırılacak:\r\n{0}\r\n\r\n" +
                "Dosyalar silinmez; Addons-Removed klasörüne yedeklenir. Devam edilsin mi?",
                names)))
            return;

        try
        {
            UseWaitCursor = true;
            var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
            foreach (var row in rows)
            {
                var path = Path.Combine(row.PackPath, AddonPeds.PackFileName);
                var file = SyncJson.Load<AddonPedPackFile>(path) ?? new AddonPedPackFile();
                var ped = file.Peds.FirstOrDefault(p =>
                    string.Equals(p.Model, row.Model, StringComparison.OrdinalIgnoreCase));
                if (ped == null) continue;

                var backup = Path.Combine(BackupsDir, stamp, "peds", row.PackId);
                AddonPeds.MovePedToBackup(row.PackPath, ped, backup,
                    msg => AppEnv.Log(msg));
                file.Peds.Remove(ped);
                SyncJson.Save(path, file);
                AppEnv.Log($"Sunucu pedi kaldırıldı: {row.Name} ({row.PackName})");
            }
            RefreshList();
        }
        catch (Exception ex)
        {
            AppEnv.LogError(ex.Message);
            Ui.Error(FindForm()!, T("Ped kaldırılamadı:\r\n") + ex.Message);
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private string? SelectedPackDir()
    {
        var packs = _list.SelectedItems.Cast<ListViewItem>()
            .Select(i => (i.Tag as PedRow)?.PackPath)
            .Where(p => p != null)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (packs.Count == 1) return packs[0];
        if (packs.Count > 1) return null;

        var all = _list.Items.Cast<ListViewItem>()
            .Select(i => (i.Tag as PedRow)?.PackPath)
            .Where(p => p != null)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        return all.Count == 1 ? all[0] : null;
    }

    private static void ExtractArchive(string file, string dest)
    {
        var ext = Path.GetExtension(file).ToLowerInvariant();
        if (ext == ".zip")
        {
            ZipFile.ExtractToDirectory(file, dest, overwriteFiles: true);
            return;
        }

        var seven = Find7Zip();
        if (seven == null)
            throw new InvalidOperationException(
                "RAR/7Z arşivi için 7-Zip gerekir. 7-Zip kurun veya arşivi elle açıp " +
                "klasör olarak ekleyin.");

        var psi = new ProcessStartInfo(seven)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            Arguments = $"x -y -o\"{dest}\" \"{file}\""
        };
        using var p = Process.Start(psi)
            ?? throw new InvalidOperationException("7-Zip başlatılamadı.");
        p.WaitForExit();
        if (p.ExitCode != 0)
            throw new InvalidOperationException(
                "Arşiv açılamadı (7-Zip çıkış kodu " + p.ExitCode + ").");
    }

    private static string? Find7Zip()
    {
        foreach (var root in new[]
                 {
                     Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                     Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
                 })
        {
            if (string.IsNullOrWhiteSpace(root)) continue;
            var path = Path.Combine(root, "7-Zip", "7z.exe");
            if (File.Exists(path)) return path;
        }

        var pathVar = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in pathVar.Split(Path.PathSeparator))
        {
            try
            {
                var path = Path.Combine(dir.Trim(), "7z.exe");
                if (File.Exists(path)) return path;
            }
            catch { }
        }
        return null;
    }

    private void OpenFolder(string path)
    {
        try
        {
            Directory.CreateDirectory(path);
            Process.Start(new ProcessStartInfo
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
}
