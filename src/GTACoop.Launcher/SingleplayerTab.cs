using System.Security.Cryptography;
using System.Windows.Forms;
using GTACoop.Sync;

namespace GTACoop.Remake;

/// <summary>
/// Tek oyunculu mod yöneticisi. Modlar launcher'ın yanındaki
/// <c>Singleplayer\Mods\&lt;paket&gt;</c> klasöründe durur; etkinleştirildiğinde
/// oyun klasörüne kopyalanır. Oyunun kendi arşivleri (.rpf) hiçbir zaman
/// değiştirilmez: yazılan her dosya kaydedilir, devre dışı bırakıldığında
/// yalnızca bizim yazdığımız içerik silinir ve üzerine yazılan dosyalar
/// yedekten geri getirilir.
/// </summary>
public sealed class SingleplayerTab : UserControl
{
    private readonly ListView _list = new();
    private readonly Label _status = new();
    private readonly Label _details = new();
    private SpState _state = new();

    private static string? _root;

    /// <summary>Launcher'ın yanındaki klasör; yazılamazsa veri klasörüne düşer.</summary>
    public static string SpRoot => _root ??= ResolveRoot();

    private static string ResolveRoot()
    {
        string dir;
        try
        {
            dir = Path.Combine(AppContext.BaseDirectory, "Singleplayer");
            Directory.CreateDirectory(dir);
        }
        catch
        {
            dir = Path.Combine(AppEnv.DataFolder, "Singleplayer");
            Directory.CreateDirectory(dir);
        }

        Directory.CreateDirectory(Path.Combine(dir, "Mods"));
        Directory.CreateDirectory(Path.Combine(dir, "Backup"));
        return dir;
    }

    private static string ModsFolder => Path.Combine(SpRoot, "Mods");
    private static string BackupFolder => Path.Combine(SpRoot, "Backup");
    private static string StatePath => Path.Combine(SpRoot, "state.json");

    /// <summary>Yalnızca bu klasörlere yazılır; oyunun kök arşivleri korunur.</summary>
    private static readonly string[] AllowedRoots = { "scripts", "asi", "mods", "plugins", "lua" };

    /// <summary>Kök dizine bırakılabilen dosya türleri (mod manager davranışı).</summary>
    private static readonly string[] AllowedRootExtensions =
        { ".asi", ".ini", ".xml", ".json", ".txt", ".log", ".dat", ".dll" };

    public SingleplayerTab()
    {
        Dock = DockStyle.Fill;

        _state = SyncJson.Load<SpState>(StatePath) ?? new SpState();
        _state.Packs ??= new List<SpPackState>();

        var info = new Label
        {
            Dock = DockStyle.Top,
            AutoSize = false,
            Height = 64,
            ForeColor = Color.DimGray,
            Padding = new Padding(4, 4, 4, 4),
            Text = T("Tek oyunculu modlarınızı oyun dosyalarını bozmadan yönetin. " +
                     "Modlar bu klasörde durur; etkinleştirdiğinizde oyun klasörüne kopyalanır. " +
                     "Devre dışı bıraktığınızda yalnızca eklenen dosyalar kaldırılır, " +
                     "üzerine yazılan dosyalar yedekten geri gelir. Oyunun .rpf arşivleri değiştirilmez.")
        };

        var pathInfo = new Label
        {
            Dock = DockStyle.Top,
            AutoSize = false,
            Height = 24,
            ForeColor = Color.DimGray,
            Padding = new Padding(4, 2, 4, 2),
            AutoEllipsis = true,
            Text = T("Mod klasörü: {0}", ModsFolder)
        };

        Ui.StyleList(_list,
            (T("Paket"), 250),
            (T("Dosya"), 70),
            (T("Durum"), 90),
            (T("Yazar"), 130),
            (T("Sürüm"), 80),
            (T("Açıklama"), 280));
        _list.Dock = DockStyle.Top;
        _list.Height = 320;
        _list.MultiSelect = false;
        _list.SelectedIndexChanged += (_, _) => ShowDetails();

        _status.Dock = DockStyle.Bottom;
        _status.AutoSize = false;
        _status.Height = 24;
        _status.TextAlign = ContentAlignment.MiddleLeft;
        _status.ForeColor = Color.DimGray;
        _status.Padding = new Padding(4, 0, 0, 0);
        _status.Text = T("Hazır.");

        var buttons = Ui.Flow(DockStyle.Bottom);
        buttons.Controls.Add(Ui.Btn(T("Yenile"), (_, _) => RefreshList()));
        buttons.Controls.Add(Ui.Btn(T("Paket Ekle..."), (_, _) => AddPack()));
        buttons.Controls.Add(Ui.Btn(T("Etkinleştir"), (_, _) => EnableSelected()));
        buttons.Controls.Add(Ui.Btn(T("Devre Dışı Bırak"), (_, _) => DisableSelected()));
        buttons.Controls.Add(Ui.Btn(T("Mod Klasörünü Aç"), (_, _) => OpenFolder(ModsFolder)));
        buttons.Controls.Add(Ui.Btn(T("Yedekleri Aç"), (_, _) => OpenFolder(BackupFolder)));

        _details.Dock = DockStyle.Bottom;
        _details.AutoSize = false;
        _details.Height = 66;
        _details.ForeColor = Color.DimGray;
        _details.Padding = new Padding(4, 2, 4, 2);
        _details.Text = T("Paket seçin: açıklama ve sürüm bilgisi burada görünür.");

        Controls.Add(_list);
        Controls.Add(pathInfo);
        Controls.Add(info);
        Controls.Add(_details);
        Controls.Add(_status);
        Controls.Add(buttons);

        RefreshList();
    }

    /// <summary>Seçili paketin açıklamasını ve sürüm bilgisini gösterir.</summary>
    private void ShowDetails()
    {
        if (_list.SelectedItems.Count == 0)
        {
            _details.Text = T("Paket seçin: açıklama ve sürüm bilgisi burada görünür.");
            return;
        }

        var packName = _list.SelectedItems[0].Tag as string ?? _list.SelectedItems[0].Text;
        var meta = SpModInfo.Read(Path.Combine(ModsFolder, packName));
        var pack = _state.Packs.FirstOrDefault(p =>
            string.Equals(p.Name, packName, StringComparison.OrdinalIgnoreCase));

        var lines = new List<string>
        {
            T("Paket: {0}", packName) + (string.IsNullOrWhiteSpace(meta.Name) ? "" : "  —  " + meta.Name)
        };
        if (!string.IsNullOrWhiteSpace(meta.Author)) lines.Add(T("Yazar: {0}", meta.Author));
        if (!string.IsNullOrWhiteSpace(meta.Version)) lines.Add(T("Sürüm: {0}", meta.Version));
        if (!string.IsNullOrWhiteSpace(meta.Category)) lines.Add(T("Tür: {0}", meta.Category));
        if (!string.IsNullOrWhiteSpace(meta.Description)) lines.Add(meta.Description);
        lines.Add(pack is { Enabled: true }
            ? T("Durum: etkin ({0} dosya)", pack.Files.Count)
            : T("Durum: kapalı"));

        _details.Text = string.Join("\r\n", lines);
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

    private static bool IsAllowed(string relativePath)
    {
        var norm = relativePath.Replace('\\', '/').TrimStart('/');
        if (norm.Length == 0) return false;

        var top = norm.Split('/')[0].ToLowerInvariant();
        if (Array.Exists(AllowedRoots, r => r == top)) return true;

        // Kök dizine yalnızca bilinen mod dosyası türleri bırakılabilir.
        if (norm.Contains('/')) return false;
        return Array.Exists(AllowedRootExtensions,
            ext => ext.Equals(Path.GetExtension(norm), StringComparison.OrdinalIgnoreCase));
    }

    private static string Hash(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private void SaveState() => SyncJson.Save(StatePath, _state);

    private void RefreshList()
    {
        _state = SyncJson.Load<SpState>(StatePath) ?? new SpState();
        _state.Packs ??= new List<SpPackState>();

        // Diskte olup kayıtta olmayan paketleri ekle.
        try
        {
            foreach (var dir in Directory.GetDirectories(ModsFolder))
            {
                var name = Path.GetFileName(dir);
                if (_state.Packs.All(p => !string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)))
                    _state.Packs.Add(new SpPackState { Name = name });
            }
        }
        catch { }

        _list.BeginUpdate();
        _list.Items.Clear();
        foreach (var pack in _state.Packs.OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            var dir = Path.Combine(ModsFolder, pack.Name);
            var exists = Directory.Exists(dir);
            var count = pack.Files.Count;
            if (!exists && count == 0) continue;

            var meta = SpModInfo.Read(dir);
            var item = new ListViewItem(pack.Name);
            item.SubItems.Add(exists ? Directory.GetFiles(dir, "*", SearchOption.AllDirectories).Length.ToString() : "0");
            item.SubItems.Add(pack.Enabled ? T("Etkin") : T("Kapalı"));
            item.SubItems.Add(meta.Author ?? "");
            item.SubItems.Add(meta.Version ?? "");
            item.SubItems.Add(meta.Description ?? "");
            item.Tag = pack.Name;
            item.ForeColor = pack.Enabled ? Color.FromArgb(22, 122, 54) : _list.ForeColor;
            _list.Items.Add(item);
        }
        _list.EndUpdate();

        var enabled = _state.Packs.Count(p => p.Enabled);
        _status.Text = T("{0} paket, {1} etkin. Oyun klasörü: {2}",
            _list.Items.Count, enabled,
            GameFolder.IsValid(AppEnv.Settings.GameFolder) ? AppEnv.Settings.GameFolder : T("seçilmedi"));
    }

    private SpPackState? Selected()
    {
        if (_list.SelectedItems.Count == 0)
        {
            Ui.Warn(FindForm()!, T("Önce bir paket seçin."));
            return null;
        }

        var name = _list.SelectedItems[0].Tag as string ?? _list.SelectedItems[0].Text;
        return _state.Packs.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    private void EnableSelected()
    {
        var pack = Selected();
        if (pack == null) return;
        EnablePack(pack);
    }

    private void DisableSelected()
    {
        var pack = Selected();
        if (pack == null) return;
        DisablePack(pack);
    }

    private void EnablePack(SpPackState pack)
    {
        var game = AppEnv.Settings.GameFolder;
        if (!GameFolder.IsValid(game))
        {
            Ui.Warn(FindForm()!, T("GTA V klasörü ayarlı değil. Önce Oyun sekmesinden klasörü seçin."));
            return;
        }

        if (AddonApplier.IsGameRunning())
        {
            Ui.Warn(FindForm()!, T("GTA V açık. Modları değiştirmeden önce oyunu kapatın."));
            return;
        }

        var source = Path.Combine(ModsFolder, pack.Name);
        if (!Directory.Exists(source))
        {
            Ui.Warn(FindForm()!, T("Paket klasörü bulunamadı: {0}", source));
            return;
        }

        var files = new List<SpFileState>();
        var rejected = new List<string>();
        var failed = 0;

        foreach (var src in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(source, src).Replace('\\', '/');
            if (!IsAllowed(rel))
            {
                rejected.Add(rel);
                continue;
            }

            try
            {
                var dest = Path.Combine(game, rel.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);

                var backup = "";
                if (File.Exists(dest))
                {
                    backup = Path.Combine(BackupFolder, pack.Name,
                        rel.Replace('/', Path.DirectorySeparatorChar));
                    Directory.CreateDirectory(Path.GetDirectoryName(backup)!);
                    if (!File.Exists(backup)) File.Copy(dest, backup, overwrite: true);
                }

                File.Copy(src, dest, overwrite: true);
                files.Add(new SpFileState
                {
                    RelativePath = rel,
                    Sha256 = Hash(dest),
                    BackupPath = backup
                });
            }
            catch
            {
                failed++;
            }
        }

        pack.Files = files;
        pack.Enabled = files.Count > 0;
        SaveState();
        RefreshList();

        var message = T("Paket etkinleştirildi: {0} ({1} dosya)", pack.Name, files.Count);
        if (failed > 0) message += " " + T("({0} dosya yazılamadı)", failed);
        if (rejected.Count > 0)
            message += "\r\n" + T("Güvenlik: yalnızca scripts, asi, mods, plugins ve lua klasörlerine yazılır. Atlanan: {0}",
                string.Join(", ", rejected.Take(5)));

        _status.Text = message.Replace("\r\n", " ");
        AppEnv.Log("Tek oyunculu paket etkinleştirildi: " + pack.Name + " (" + files.Count + " dosya)");

        if (rejected.Count > 0)
            Ui.Warn(FindForm()!, message);
        else
            Ui.Info(FindForm()!, message);
    }

    private void DisablePack(SpPackState pack)
    {
        var game = AppEnv.Settings.GameFolder;
        var removed = 0;
        var kept = 0;

        if (GameFolder.IsValid(game))
        {
            foreach (var file in pack.Files)
            {
                try
                {
                    var dest = Path.Combine(game, file.RelativePath.Replace('/', Path.DirectorySeparatorChar));
                    var backupExists = !string.IsNullOrEmpty(file.BackupPath) && File.Exists(file.BackupPath);

                    if (File.Exists(dest))
                    {
                        // Yalnızca bizim yazdığımız içerik silinir; kullanıcı
                        // dosyayı değiştirdiyse dokunulmaz.
                        if (string.Equals(Hash(dest), file.Sha256, StringComparison.OrdinalIgnoreCase))
                        {
                            File.Delete(dest);
                            removed++;
                        }
                        else
                        {
                            kept++;
                        }
                    }

                    if (backupExists)
                    {
                        var dest2 = Path.Combine(game, file.RelativePath.Replace('/', Path.DirectorySeparatorChar));
                        if (!File.Exists(dest2))
                            File.Copy(file.BackupPath, dest2, overwrite: true);
                    }
                }
                catch
                {
                    kept++;
                }
            }
        }

        pack.Enabled = false;
        pack.Files = new List<SpFileState>();
        SaveState();
        RefreshList();

        var message = T("Paket devre dışı bırakıldı: {0} ({1} dosya kaldırıldı)", pack.Name, removed);
        if (kept > 0) message += " " + T("({0} dosya korundu: siz değiştirmişsiniz)", kept);
        _status.Text = message;
        AppEnv.Log(message);
        Ui.Info(FindForm()!, message);
    }

    private void AddPack()
    {
        using var dlg = new FolderBrowserDialog
        {
            Description = T("Tek oyunculu mod paketini seçin (klasörün içeriği kopyalanır)."),
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false
        };
        if (dlg.ShowDialog(FindForm()) != DialogResult.OK) return;

        var source = dlg.SelectedPath;
        var name = Path.GetFileName(source.TrimEnd(Path.DirectorySeparatorChar));
        if (string.IsNullOrWhiteSpace(name))
        {
            Ui.Warn(FindForm()!, T("Klasör adı okunamadı."));
            return;
        }

        var target = Path.Combine(ModsFolder, name);
        if (Directory.Exists(target))
        {
            Ui.Warn(FindForm()!, T("Bu isimde bir paket zaten var: {0}", name));
            return;
        }

        try
        {
            CopyTree(source, target);
            _state.Packs.Add(new SpPackState { Name = name });
            SaveState();
            RefreshList();
            _status.Text = T("Paket eklendi: {0}", name);
            AppEnv.Log("Tek oyunculu paket eklendi: " + name);
        }
        catch (Exception ex)
        {
            Ui.Error(FindForm()!, T("Paket eklenemedi: {0}", ex.Message));
            AppEnv.LogError("Tek oyunculu paket eklenemedi: " + ex.Message);
        }
    }

    private static void CopyTree(string source, string target)
    {
        Directory.CreateDirectory(target);
        foreach (var dir in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(source, dir);
            Directory.CreateDirectory(Path.Combine(target, rel));
        }
        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(source, file);
            var dest = Path.Combine(target, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            File.Copy(file, dest, overwrite: true);
        }
    }
}

/// <summary>Paket klasöründeki mod.json / mod.ini bilgisi (isteğe bağlı).</summary>
public sealed class SpModInfo
{
    public string Name { get; set; } = "";
    public string Author { get; set; } = "";
    public string Version { get; set; } = "";
    public string Category { get; set; } = "";
    public string Description { get; set; } = "";

    public static SpModInfo Read(string packDir)
    {
        try
        {
            var json = Path.Combine(packDir, "mod.json");
            if (File.Exists(json)) return SyncJson.Load<SpModInfo>(json) ?? new SpModInfo();

            var ini = Path.Combine(packDir, "mod.ini");
            if (!File.Exists(ini)) return new SpModInfo();

            var info = new SpModInfo();
            foreach (var line in File.ReadAllLines(ini))
            {
                var idx = line.IndexOf('=');
                if (idx <= 0) continue;
                var key = line[..idx].Trim().ToLowerInvariant();
                var value = line[(idx + 1)..].Trim();
                switch (key)
                {
                    case "name": info.Name = value; break;
                    case "author": info.Author = value; break;
                    case "version": info.Version = value; break;
                    case "category": info.Category = value; break;
                    case "description": info.Description = value; break;
                }
            }
            return info;
        }
        catch
        {
            return new SpModInfo();
        }
    }
}

/// <summary>Tek oyunculu mod yöneticisinin kalıcı durumu.</summary>
public sealed class SpState
{
    public List<SpPackState> Packs { get; set; } = new();
}

public sealed class SpPackState
{
    public string Name { get; set; } = "";
    public bool Enabled { get; set; }
    public List<SpFileState> Files { get; set; } = new();
}

public sealed class SpFileState
{
    /// <summary>Oyun köküne göre yol ("/" ayraçlı).</summary>
    public string RelativePath { get; set; } = "";

    /// <summary>Bizim yazdığımız içeriğin özeti; kullanıcı değişikliğini ayırt eder.</summary>
    public string Sha256 { get; set; } = "";

    /// <summary>Üzerine yazılan dosyanın yedeği (yoksa boş).</summary>
    public string BackupPath { get; set; } = "";
}
