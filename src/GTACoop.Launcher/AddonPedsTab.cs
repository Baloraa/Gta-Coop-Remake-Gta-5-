using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;
using System.Windows.Forms;
using GTACoop.Sync;

namespace GTACoop.Remake;

/// <summary>
/// Sunucunun Addons\peds klasöründeki addon ped paketlerini yönetir.
/// Kullanıcının kendi AddonPeds kurulumuna ve ap_m.xml dosyasına dokunmaz.
/// </summary>
public sealed class AddonPedsTab : UserControl
{
    private readonly ListView _list = new();
    private readonly Label _status = new();
    private readonly Dictionary<string, ModelCache> _models =
        new(StringComparer.OrdinalIgnoreCase);
    private bool _suppressCheck;

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
            Text = T("Sunucunun Addons\\peds klasöründeki ped paketlerini buradan yönetin. " +
                     "Paketler oyunculara otomatik indirilir; kullanıcının kendi AddonPeds " +
                     "kurulumu ve ap_m.xml dosyası değiştirilmez.")
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
            (T("Ped"), 230),
            (T("Paket"), 220),
            (T("Tür"), 90),
            (T("Model"), 220));
        _list.Dock = DockStyle.Fill;
        _list.CheckBoxes = true;
        _list.MultiSelect = true;
        _list.FullRowSelect = true;
        _list.HeaderStyle = ColumnHeaderStyle.Nonclickable;
        _list.ItemChecked += OnItemChecked;
        _list.SelectedIndexChanged += (_, _) => UpdateStatus();

        var buttons = Ui.Flow();
        buttons.Controls.Add(Ui.Btn(T("Yenile"), (_, _) => RefreshList()));
        buttons.Controls.Add(Ui.Btn(T("Ped Paketi Klasörü Ekle..."), (_, _) => AddPackFolder()));
        buttons.Controls.Add(Ui.Btn(T("Ped Arşivi Ekle (zip/rar/7z)..."), (_, _) => AddPackArchive()));
        buttons.Controls.Add(Ui.Btn(T("Ped Meta Verisi Ekle (ap_m.xml)..."), (_, _) => AddMetadata()));
        buttons.Controls.Add(Ui.Btn(T("Seçili Paketi Kaldır..."), (_, _) => RemoveSelectedPacks()));
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
            Height = 62,
            ForeColor = Color.DimGray,
            Padding = new Padding(6, 2, 6, 0),
            Text = T("Desteklenen biçim: dlc.rpf içeren AddonPeds / ped DLC paketi. " +
                     "Hayvan pedleri (a_c_...) otomatik algılanır. Meta veri yoksa güvenli " +
                     "bir şablon kullanılır; en iyi sonuç için paketin ap_m.xml meta verisini ekleyin.")
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
    private static string T(string text, params object?[] args) => Loc.T(text, args);

    private sealed class ModelCache
    {
        public long Ticks { get; set; }
        public long Length { get; set; }
        public List<string> Models { get; set; } = new();
    }

    private sealed class PedRow
    {
        public string PackPath { get; init; } = "";
        public string PackId { get; init; } = "";
        public string PackName { get; init; } = "";
        public string Model { get; init; } = "";
        public string Name { get; init; } = "";
        public string Kind { get; init; } = "human";
        public bool Enabled { get; set; }
    }

    public void RefreshList()
    {
        try
        {
            UseWaitCursor = true;
            Directory.CreateDirectory(PedsDir);
            var rows = new List<PedRow>();

            foreach (var packDir in Directory.GetDirectories(PedsDir))
            {
                var id = Path.GetFileName(packDir);
                if (id.StartsWith('.')) continue;
                var dlc = AddonPeds.FindDlcRpf(packDir);
                if (dlc == null) continue;

                var packFile = LoadPack(packDir, dlc);
                foreach (var ped in packFile.Peds)
                {
                    rows.Add(new PedRow
                    {
                        PackPath = packDir,
                        PackId = id,
                        PackName = string.IsNullOrWhiteSpace(packFile.DisplayName)
                            ? id
                            : packFile.DisplayName,
                        Model = ped.Model,
                        Name = string.IsNullOrWhiteSpace(ped.Name) ? ped.Model : ped.Name,
                        Kind = ped.Kind,
                        Enabled = ped.Enabled
                    });
                }
            }

            _suppressCheck = true;
            _list.BeginUpdate();
            _list.Items.Clear();
            foreach (var row in rows
                         .OrderBy(r => r.PackName, StringComparer.CurrentCultureIgnoreCase)
                         .ThenBy(r => r.Kind == "animal" ? 1 : 0)
                         .ThenBy(r => r.Name, StringComparer.CurrentCultureIgnoreCase))
            {
                var item = new ListViewItem(row.Name)
                {
                    Tag = row,
                    Checked = row.Enabled
                };
                item.SubItems.Add(row.PackName);
                item.SubItems.Add(T(row.Kind == "animal" ? "Hayvan" : "İnsan"));
                item.SubItems.Add(row.Model);
                _list.Items.Add(item);
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
            _suppressCheck = false;
            UseWaitCursor = false;
        }
    }

    private AddonPedPackFile LoadPack(string packDir, string dlc)
    {
        var packPath = Path.Combine(packDir, AddonPeds.PackFileName);
        var info = new FileInfo(dlc);
        var pedsTime = File.Exists(packPath) ? File.GetLastWriteTimeUtc(packPath) : DateTime.MinValue;

        List<string> models;
        if (_models.TryGetValue(dlc, out var cached) &&
            cached.Ticks == info.LastWriteTimeUtc.Ticks && cached.Length == info.Length)
        {
            models = cached.Models;
        }
        else
        {
            models = AddonPeds.DiscoverModels(dlc);
            _models[dlc] = new ModelCache
            {
                Ticks = info.LastWriteTimeUtc.Ticks,
                Length = info.Length,
                Models = models
            };
        }

        // peds.json yoksa veya paket ondan daha yeni ise model listesini yenile.
        if (!File.Exists(packPath) || info.LastWriteTimeUtc > pedsTime)
            return AddonPeds.LoadOrCreatePackFile(packDir, models);

        return SyncJson.Load<AddonPedPackFile>(packPath)
               ?? AddonPeds.LoadOrCreatePackFile(packDir, models);
    }

    private void OnItemChecked(object? sender, ItemCheckedEventArgs e)
    {
        if (_suppressCheck || e.Item.Tag is not PedRow row) return;
        try
        {
            var path = Path.Combine(row.PackPath, AddonPeds.PackFileName);
            var file = SyncJson.Load<AddonPedPackFile>(path) ?? new AddonPedPackFile();
            var ped = file.Peds.FirstOrDefault(p =>
                string.Equals(p.Model, row.Model, StringComparison.OrdinalIgnoreCase));
            if (ped == null) return;

            ped.Enabled = e.Item.Checked;
            row.Enabled = e.Item.Checked;
            SyncJson.Save(path, file);
            AppEnv.Log((e.Item.Checked ? "Ped etkinleştirildi: " : "Ped devre dışı: ") +
                       row.Name + " (" + row.PackName + ")");
            UpdateStatus();
        }
        catch (Exception ex)
        {
            _suppressCheck = true;
            e.Item.Checked = !e.Item.Checked;
            _suppressCheck = false;
            AppEnv.LogError("Ped ayarı kaydedilemedi: " + ex.Message);
            Ui.Error(FindForm()!, T("Ped ayarı kaydedilemedi:\r\n") + ex.Message);
        }
    }

    private void UpdateStatus()
    {
        var total = _list.Items.Count;
        var enabled = _list.Items.Cast<ListViewItem>().Count(i => i.Checked);
        var packs = _list.Items.Cast<ListViewItem>()
            .Select(i => (i.Tag as PedRow)?.PackPath)
            .Where(p => p != null)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
        _status.Text = T("Ped paketi: {0} | Ped: {1} | Etkin: {2} | Devre dışı: {3}",
            packs, total, enabled, total - enabled);
    }

    private void AddPackFolder()
    {
        using var dlg = new FolderBrowserDialog
        {
            Description = T("İçinde dlc.rpf bulunan addon ped paketi klasörünü seçin.")
        };
        if (dlg.ShowDialog(FindForm()) != DialogResult.OK) return;

        try
        {
            UseWaitCursor = true;
            var dlc = AddonPeds.FindDlcRpf(dlg.SelectedPath);
            if (dlc == null)
            {
                Ui.Warn(FindForm()!, T("Seçilen klasörde dlc.rpf bulunamadı."));
                return;
            }

            var sourceDir = Path.GetDirectoryName(dlc)!;
            var name = new DirectoryInfo(sourceDir).Name;
            ImportPack(sourceDir, name);
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

    private void AddPackArchive()
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
                    var dlc = AddonPeds.FindDlcRpf(temp);
                    if (dlc == null)
                        throw new InvalidDataException("Arşivde dlc.rpf bulunamadı.");

                    var sourceDir = Path.GetDirectoryName(dlc)!;
                    var name = new DirectoryInfo(sourceDir).Name;
                    if (name.Length == 0 || name == temp)
                        name = Path.GetFileNameWithoutExtension(archive);
                    ImportPack(sourceDir, name);
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
                    "Ped paketi eklendi: {0}\r\nSunucu çalışıyorsa bir sonraki manifest isteğinde otomatik dağıtılır.",
                    added));
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

    private void ImportPack(string sourceDir, string name)
    {
        name = AddonApplier.Safe(name);
        if (name.Length == 0) name = "peds";
        var target = Path.Combine(PedsDir, name);

        if (Directory.Exists(target))
        {
            if (!Ui.Confirm(FindForm()!, T(
                    "'{0}' zaten Addons\\peds içinde. Üzerine yazılsın mı?", name)))
                return;
            Directory.Delete(target, recursive: true);
        }

        FileUtil.CopyTree(sourceDir, target);
        var dlc = AddonPeds.FindDlcRpf(target)
            ?? throw new InvalidDataException("Kopyalanan pakette dlc.rpf bulunamadı.");
        var models = AddonPeds.DiscoverModels(dlc);
        if (models.Count == 0)
            throw new InvalidDataException(
                "Pakette ped modeli (.yft) bulunamadı. Geçerli bir AddonPeds DLC paketi seçin.");
        AddonPeds.LoadOrCreatePackFile(target, models);
        AppEnv.Log($"Sunucu ped paketi eklendi: {name} ({models.Count} ped)");
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
                "RAR/7Z arşivi için 7-Zip gerekir. 7-Zip kurun veya arşivi elle açıp klasör olarak ekleyin.");

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

    private void AddMetadata()
    {
        var packDir = SelectedPackDir();
        if (packDir == null)
        {
            Ui.Info(FindForm()!, T("Önce listeden bir ped paketi seçin."));
            return;
        }

        using var dlg = new OpenFileDialog
        {
            Title = T("Ped meta verisini seçin (ap_m.xml / pedmeta.xml)."),
            Filter = T("Ped meta verisi (*.xml;*.meta)|*.xml;*.meta|Tüm dosyalar (*.*)|*.*")
        };
        if (dlg.ShowDialog(FindForm()) != DialogResult.OK) return;

        try
        {
            var doc = System.Xml.Linq.XDocument.Load(dlg.FileName);
            if (!doc.Descendants("Item").Any(i => i.Element("Name") != null))
                throw new InvalidDataException("Seçilen dosyada ped meta kaydı (<Item><Name>) bulunamadı.");

            var target = Path.Combine(packDir, "pedmeta.xml");
            File.Copy(dlg.FileName, target, overwrite: true);
            var dlc = AddonPeds.FindDlcRpf(packDir)!;
            AddonPeds.LoadOrCreatePackFile(packDir, AddonPeds.DiscoverModels(dlc));
            RefreshList();
            Ui.Info(FindForm()!, T("Ped meta verisi eklendi: {0}\r\n" +
                "Paket bir sonraki manifest isteğinde yeniden hazırlanır.", Path.GetFileName(packDir)));
            AppEnv.Log($"Ped meta verisi eklendi: {packDir}");
        }
        catch (Exception ex)
        {
            AppEnv.LogError(ex.Message);
            Ui.Error(FindForm()!, T("Meta veri eklenemedi:\r\n") + ex.Message);
        }
    }

    private string? SelectedPackDir()
    {
        var selected = _list.SelectedItems.Cast<ListViewItem>()
            .Select(i => i.Tag as PedRow)
            .Where(r => r != null)
            .Select(r => r!.PackPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (selected.Count == 1) return selected[0];
        if (selected.Count > 1) return null;

        var all = _list.Items.Cast<ListViewItem>()
            .Select(i => (i.Tag as PedRow)?.PackPath)
            .Where(p => p != null)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        return all.Count == 1 ? all[0] : null;
    }

    private void RemoveSelectedPacks()
    {
        var packs = _list.SelectedItems.Cast<ListViewItem>()
            .Select(i => i.Tag as PedRow)
            .Where(r => r != null)
            .GroupBy(r => r!.PackPath, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First()!)
            .ToList();
        if (packs.Count == 0)
        {
            Ui.Info(FindForm()!, T("Kaldırmak için listeden en az bir ped paketi seçin."));
            return;
        }

        var names = string.Join("\r\n", packs.Take(12).Select(p => "• " + p.PackName));
        if (!Ui.Confirm(FindForm()!, T(
                "Seçilen ped paketleri sunucudan kaldırılacak:\r\n{0}\r\n\r\n" +
                "Dosyalar silinmez; güvenli bir klasöre taşınır. Devam edilsin mi?", names)))
            return;

        try
        {
            UseWaitCursor = true;
            var backupRoot = Path.Combine(AppEnv.ServerDir, "Addons-Removed",
                DateTime.Now.ToString("yyyyMMdd-HHmmss-fff"), "peds");
            Directory.CreateDirectory(backupRoot);
            foreach (var pack in packs)
            {
                if (!Directory.Exists(pack.PackPath)) continue;
                var dlc = AddonPeds.FindDlcRpf(pack.PackPath);
                if (dlc != null) _models.Remove(dlc);
                var target = Path.Combine(backupRoot, pack.PackId);
                Directory.Move(pack.PackPath, target);
                AppEnv.Log($"Sunucu ped paketi kaldırıldı: {pack.PackName}");
            }
            RefreshList();
        }
        catch (Exception ex)
        {
            AppEnv.LogError(ex.Message);
            Ui.Error(FindForm()!, T("Ped paketi kaldırılamadı:\r\n") + ex.Message);
        }
        finally
        {
            UseWaitCursor = false;
        }
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
