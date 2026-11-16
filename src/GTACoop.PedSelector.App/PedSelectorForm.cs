using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using GTACoop.Sync;

namespace GTACoop.PedSelector.App
{
    /// <summary>
    /// Bağımsız ped seçici arayüzü: ped ekle, düzenle, kaydet, OpenIV (.oiv)
    /// paketi üret. GTACoop launcher'ı veya sunucusu olmadan çalışır.
    /// Arayüz dili İngilizce (varsayılan) veya Türkçe olabilir.
    /// </summary>
    internal sealed class PedSelectorForm : Form
    {
        private static readonly Color Bg = Color.FromArgb(18, 18, 21);
        private static readonly Color Surface = Color.FromArgb(26, 26, 30);
        private static readonly Color Border = Color.FromArgb(52, 52, 60);

        private readonly string _root;
        private readonly string _packDir;
        private readonly ListView _list;
        private readonly TextBox _log;
        private readonly Label _header;
        private readonly Label _hint;
        private readonly Label _languageLabel;
        private readonly ComboBox _language;
        private readonly List<(Button Button, string En, string Tr)> _buttons = new();

        private AddonPedPackFile _packFile;
        private bool _loading;

        public PedSelectorForm()
        {
            _root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "GTACoop Ped Selector");
            _packDir = Path.Combine(_root, "peds", "paket");
            Directory.CreateDirectory(_packDir);
            L.Load(_root);

            Text = "AddonPed+";
            Width = 1000;
            Height = 660;
            MinimumSize = new Size(800, 520);
            BackColor = Bg;
            ForeColor = Color.White;
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9.5f);

            _header = new Label
            {
                Dock = DockStyle.Top,
                Height = 54,
                Padding = new Padding(16, 14, 16, 0),
                Font = new Font("Segoe UI", 13f, FontStyle.Bold),
                ForeColor = Color.White
            };

            _hint = new Label
            {
                Dock = DockStyle.Top,
                Height = 30,
                Padding = new Padding(17, 0, 16, 0),
                ForeColor = Color.FromArgb(170, 170, 180)
            };

            _languageLabel = new Label
            {
                AutoSize = true,
                ForeColor = Color.FromArgb(190, 190, 200),
                Margin = new Padding(12, 6, 4, 0)
            };

            _language = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 130,
                BackColor = Surface,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(0, 3, 0, 0)
            };
            _language.Items.AddRange(new object[] { "English", "Türkçe" });
            _language.SelectedIndex = L.Turkish ? 1 : 0;
            _language.SelectedIndexChanged += OnLanguageChanged;

            _list = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                CheckBoxes = true,
                FullRowSelect = true,
                HideSelection = false,
                BackColor = Surface,
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Margin = new Padding(16)
            };
            _list.Columns.Add("Ped", 260);
            _list.Columns.Add("Model", 220);
            _list.Columns.Add("Type", 110);
            _list.Columns.Add("Health", 80);
            _list.Columns.Add("Files", 80);
            _list.ItemChecked += OnItemChecked;
            _list.DoubleClick += (_, _) => EditSelected();

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 84,
                Padding = new Padding(14, 8, 14, 0),
                BackColor = Bg,
                WrapContents = true
            };
            buttons.Controls.Add(MakeButton("Add Peds...", "Ped Ekle...", (_, _) => AddPedFiles()));
            buttons.Controls.Add(MakeButton("Add Streamed Folder...", "Streamed Klasör Ekle...",
                (_, _) => AddPedFolder()));
            buttons.Controls.Add(MakeButton("Edit...", "Düzenle...", (_, _) => EditSelected()));
            buttons.Controls.Add(MakeButton("Save", "Kaydet", (_, _) => SavePack(announce: true)));
            buttons.Controls.Add(MakeButton("Build OIV...", "OIV Oluştur...", (_, _) => BuildOiv()));
            buttons.Controls.Add(MakeButton("Install to Game...", "Oyuna Kur...", (_, _) => InstallToGame()));
            buttons.Controls.Add(MakeButton("Uninstall", "Kurulumu Kaldır", (_, _) => UninstallFromGame()));
            buttons.Controls.Add(MakeButton("Refresh", "Yenile", (_, _) => Reload()));
            buttons.Controls.Add(MakeButton("Open Folder", "Klasörü Aç", (_, _) => OpenFolder(_root)));
            buttons.Controls.Add(_languageLabel);
            buttons.Controls.Add(_language);

            _log = new TextBox
            {
                Dock = DockStyle.Bottom,
                Height = 130,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                BackColor = Surface,
                ForeColor = Color.FromArgb(200, 200, 210),
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Consolas", 9f)
            };

            var listHost = new Panel { Dock = DockStyle.Fill, Padding = new Padding(14, 4, 14, 8) };
            listHost.Controls.Add(_list);

            Controls.Add(listHost);
            Controls.Add(_log);
            Controls.Add(buttons);
            Controls.Add(_hint);
            Controls.Add(_header);

            ApplyLanguage();
            Reload();
        }

        // -------------------------------------------------------------------- dil

        private void OnLanguageChanged(object? sender, EventArgs e)
        {
            var code = _language.SelectedIndex == 1 ? L.TurkishCode : L.English;
            if (code == L.Language) return;

            L.SetLanguage(code, _root);
            ApplyLanguage();
            Log(L.T("Language: English.", "Dil: Türkçe."));
        }

        private void ApplyLanguage()
        {
            _header.Text = L.T("AddonPed+ — standalone addon ped mod",
                "AddonPed+ — bağımsız addon ped modu") +
                (_packFile == null ? "" : "  —  " + _packFile.Peds.Count + L.T(" peds", " ped"));

            _hint.Text = L.T(
                "Add your ped files, pick the ones you want and build an OpenIV package (.oiv). " +
                "In game, press F10 to open the ped menu.",
                "Ped dosyalarını ekleyin, istediklerinizi seçin ve OpenIV paketi (.oiv) oluşturun. " +
                "Oyun içinde F10 ile ped menüsü açılır.");

            _languageLabel.Text = L.T("Language:", "Dil:");

            foreach (var (button, en, tr) in _buttons)
                button.Text = L.T(en, tr);

            _list.Columns[0].Text = L.T("Ped", "Ped");
            _list.Columns[1].Text = L.T("Model", "Model");
            _list.Columns[2].Text = L.T("Type", "Tür");
            _list.Columns[3].Text = L.T("Health", "Sağlık");
            _list.Columns[4].Text = L.T("Files", "Dosya");
        }

        private Button MakeButton(string en, string tr, EventHandler onClick)
        {
            var button = new Button
            {
                Text = L.T(en, tr),
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(10, 5, 10, 5),
                Margin = new Padding(4, 0, 4, 0),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(38, 38, 44),
                ForeColor = Color.White
            };
            button.FlatAppearance.BorderColor = Border;
            button.Click += onClick;
            _buttons.Add((button, en, tr));
            return button;
        }

        private void Log(string message)
        {
            _log.AppendText(DateTime.Now.ToString("HH:mm:ss") + "  " +
                            TranslateEngineLog(message) + Environment.NewLine);
        }

        /// <summary>
        /// Motor (ped paketi / OIV / kurulum) günlük satırlarını İngilizce modda
        /// çevirir; Türkçe modda metin olduğu gibi kalır.
        /// </summary>
        private static string TranslateEngineLog(string message)
        {
            if (L.Turkish || string.IsNullOrEmpty(message)) return message;

            (string Tr, string En)[] rules =
            {
                ("Kopyalandı: ", "Copied: "),
                ("Silindi: ", "Deleted: "),
                ("dlclist eklendi: ", "dlclist entry added: "),
                ("dlclist kaldırıldı: ", "dlclist entry removed: "),
                ("Ped paketi hazırlandı: ", "Ped pack prepared: "),
                ("Ped paketi dogrulandi: ", "Ped pack verified: "),
                ("peds.rpf oluşturuldu: ", "peds.rpf created: "),
                ("dlc.rpf oluşturuldu: ", "dlc.rpf created: "),
                ("Kurulum tamam: ", "Install complete: "),
                ("Kurulum kaldırıldı.", "Uninstall complete."),
                ("Meta uyarısı: ", "Meta warning: "),
                ("OIV hazır: ", "OIV ready: ")
            };

            foreach (var (tr, en) in rules)
            {
                if (!message.StartsWith(tr, StringComparison.Ordinal) &&
                    !message.Contains(": " + tr, StringComparison.Ordinal))
                    continue;
                message = message.Replace(tr, en, StringComparison.Ordinal);
            }

            message = Regex.Replace(message, @"(\d+) ped\b", "$1 ped(s)");
            return message;
        }

        // ------------------------------------------------------------------ liste

        private void Reload()
        {
            _loading = true;
            try
            {
                var loose = AddonPeds.DiscoverLoosePeds(_packDir, Log);
                _packFile = AddonPeds.LoadOrCreatePackFile(_packDir, loose);
                _packFile.Peds ??= new List<AddonPedInfo>();

                _list.BeginUpdate();
                _list.Items.Clear();
                foreach (var ped in _packFile.Peds)
                {
                    var item = new ListViewItem { Checked = ped.Enabled, Tag = ped };
                    FillItem(item, ped);
                    _list.Items.Add(item);
                }
                _list.EndUpdate();

                ApplyLanguage();
                Log(L.T($"{_packFile.Peds.Count} peds, {_packFile.Peds.Count(p => p.Enabled)} enabled.",
                    $"{_packFile.Peds.Count} ped, {_packFile.Peds.Count(p => p.Enabled)} etkin."));
            }
            catch (Exception ex)
            {
                Log(L.T("Could not read the list: ", "Liste okunamadı: ") + ex.Message);
            }
            finally
            {
                _loading = false;
            }
        }

        private static void FillItem(ListViewItem item, AddonPedInfo ped)
        {
            item.Text = string.IsNullOrWhiteSpace(ped.Name) ? ped.Model : ped.Name;
            while (item.SubItems.Count < 5) item.SubItems.Add("");
            item.SubItems[1].Text = ped.Model;
            item.SubItems[2].Text = ped.Kind;
            item.SubItems[3].Text = (ped.Health > 0 ? ped.Health : 200).ToString();
            item.SubItems[4].Text = (ped.Files?.Count ?? 0).ToString();
        }

        private void OnItemChecked(object sender, ItemCheckedEventArgs e)
        {
            if (_loading || e.Item.Tag is not AddonPedInfo ped) return;
            ped.Enabled = e.Item.Checked;
            SavePack();
        }

        private void SavePack(bool announce = false)
        {
            try
            {
                SyncJson.Save(Path.Combine(_packDir, AddonPeds.PackFileName), _packFile);
                if (announce)
                    Log(L.T($"Saved: {_packFile.Peds.Count} peds, " +
                            $"{_packFile.Peds.Count(p => p.Enabled)} enabled.",
                        $"Kaydedildi: {_packFile.Peds.Count} ped, " +
                        $"{_packFile.Peds.Count(p => p.Enabled)} etkin."));
            }
            catch (Exception ex)
            {
                Log(L.T("Could not write peds.json: ", "peds.json yazılamadı: ") + ex.Message);
            }
        }

        // -------------------------------------------------------------- düzenleme

        private void EditSelected()
        {
            if (_list.SelectedItems.Count == 0)
            {
                Log(L.T("Select a ped from the list to edit it.",
                    "Düzenlemek için listeden bir ped seçin."));
                return;
            }
            if (_list.SelectedItems[0].Tag is not AddonPedInfo ped) return;

            using var dialog = new PedEditDialog(ped);
            if (dialog.ShowDialog(this) != DialogResult.OK) return;

            ped.Name = dialog.PedName;
            ped.Model = dialog.Model;
            ped.Kind = dialog.Kind;
            ped.Health = dialog.Health;
            ped.BaseModel = dialog.BaseModel;
            ped.Ragdoll = dialog.Ragdoll;
            ped.Enabled = dialog.Enabled;

            FillItem(_list.SelectedItems[0], ped);
            SavePack();
            Log(L.T("Updated: ", "Güncellendi: ") + ped.Model +
                (string.IsNullOrWhiteSpace(ped.Name) ? "" : " (" + ped.Name + ")"));
        }

        // ---------------------------------------------------------------- ekleme

        private void AddPedFiles()
        {
            using var dialog = new OpenFileDialog
            {
                Title = L.T("Ped files (.yft .ydd .ytd .ymt)", "Ped dosyaları (.yft .ydd .ytd .ymt)"),
                Filter = L.T("Ped files|*.yft;*.ydd;*.ytd;*.ymt|All files|*.*",
                    "Ped dosyaları|*.yft;*.ydd;*.ytd;*.ymt|Tüm dosyalar|*.*"),
                Multiselect = true
            };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;

            var copied = 0;
            foreach (var file in dialog.FileNames)
            {
                if (!AddonPeds.IsPedFile(file)) continue;
                var target = Path.Combine(_packDir, Path.GetFileName(file));
                if (File.Exists(target)) continue;
                File.Copy(file, target);
                copied++;
            }
            Log(L.T($"Ped files added: {copied}", $"Ped dosyası eklendi: {copied}"));
            Reload();
        }

        private void AddPedFolder()
        {
            using var dialog = new FolderBrowserDialog
            {
                Description = L.T("Streamed ped folder (contains .ydd/.ytd files)",
                    "Streamed ped klasörü (.ydd/.ytd içeren klasör)")
            };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;

            var source = dialog.SelectedPath;
            var name = Path.GetFileName(source.TrimEnd(Path.DirectorySeparatorChar));
            var target = Path.Combine(_packDir, string.IsNullOrWhiteSpace(name) ? "ped" : name);
            if (Directory.Exists(target))
            {
                Log(L.T("This folder is already added: ", "Bu klasör zaten ekli: ") + name);
                Reload();
                return;
            }

            var copied = 0;
            foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
            {
                if (!AddonPeds.IsPedFile(file)) continue;
                var rel = Path.GetRelativePath(source, file);
                var dest = Path.Combine(target, rel);
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                File.Copy(file, dest, overwrite: true);
                copied++;
            }
            Log(L.T($"Folder added: {name} ({copied} files)",
                $"Klasör eklendi: {name} ({copied} dosya)"));
            Reload();
        }

        // ------------------------------------------------------------------- OIV

        private void BuildOiv()
        {
            using var dialog = new SaveFileDialog
            {
                Title = L.T("Save the OpenIV package", "OpenIV paketini kaydet"),
                Filter = L.T("OpenIV package|*.oiv", "OpenIV paketi|*.oiv"),
                FileName = "GTACoop-AddonPeds.oiv",
                InitialDirectory = _root
            };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;

            try
            {
                Cursor = Cursors.WaitCursor;
                Log(L.T("Building the package...", "Paket oluşturuluyor..."));
                var path = PedOiv.Build(_packDir, dialog.FileName, Log);
                Log(L.T("Ready: ", "Hazır: ") + path);
                Log(L.T("Install: OpenIV → Tools → Package Installer → select this .oiv file. " +
                        "Remove: uninstall the same package in OpenIV.",
                    "Kurulum: OpenIV → Tools → Package Installer → bu .oiv dosyasını seç. " +
                    "Kaldırma: aynı paketi OpenIV üzerinden kaldır."));
            }
            catch (Exception ex)
            {
                Log(L.T("ERROR: ", "HATA: ") + ex.Message);
                MessageBox.Show(this, ex.Message, L.T("Could not build the OIV", "OIV oluşturulamadı"),
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        // ---------------------------------------------------------------- kurulum

        private void InstallToGame()
        {
            using var dialog = new FolderBrowserDialog
            {
                Description = L.T("GTA V folder (the folder that contains GTA5.exe or GTA5_Enhanced.exe)",
                    "GTA V klasörü (GTA5.exe veya GTA5_Enhanced.exe olan klasör)")
            };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;

            var root = dialog.SelectedPath;
            if (!File.Exists(Path.Combine(root, "GTA5.exe")) &&
                !File.Exists(Path.Combine(root, "GTA5_Enhanced.exe")))
            {
                if (MessageBox.Show(this,
                        L.T("GTA5.exe was not found. Continue anyway?", "GTA5.exe bulunamadı. Yine de devam edilsin mi?"),
                        L.T("Are you sure?", "Emin misiniz?"),
                        MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                    return;
            }

            try
            {
                Cursor = Cursors.WaitCursor;
                var oiv = Path.Combine(_root, "GTACoop-AddonPeds.oiv");
                Log(L.T("Preparing the package...", "Paket hazırlanıyor..."));
                PedOiv.Build(_packDir, oiv, Log);
                PedInstaller.Install(root, oiv, Log);
                MessageBox.Show(this,
                    L.T("Installed. Press F10 in game to choose a ped.\n\nUse 'Uninstall' to remove it.",
                        "Kuruldu. Oyunda F10 ile ped seçebilirsiniz.\n\nKaldırmak için 'Kurulumu Kaldır'."),
                    L.T("Done", "Tamam"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                Log(L.T("ERROR: ", "HATA: ") + ex.Message);
                MessageBox.Show(this, ex.Message, L.T("Could not install", "Kurulamadı"),
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        private void UninstallFromGame()
        {
            if (!PedInstaller.IsInstalled)
            {
                Log(L.T("No install record; nothing to remove.",
                    "Kurulum kaydı yok; kaldırılacak bir şey bulunamadı."));
                return;
            }

            if (MessageBox.Show(this,
                    L.T("Remove the ped mod from the game?", "Ped modu oyundan kaldırılsın mı?"),
                    L.T("Confirm", "Onay"),
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            try
            {
                Cursor = Cursors.WaitCursor;
                PedInstaller.Uninstall(Log);
                MessageBox.Show(this, L.T("Removed.", "Kaldırıldı."), L.T("Done", "Tamam"),
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                Log(L.T("ERROR: ", "HATA: ") + ex.Message);
                MessageBox.Show(this, ex.Message, L.T("Could not remove", "Kaldırılamadı"),
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        private void OpenFolder(string path)
        {
            try
            {
                Directory.CreateDirectory(path);
                Process.Start(new ProcessStartInfo("explorer.exe", "\"" + path + "\"")
                {
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Log(L.T("Could not open the folder: ", "Klasör açılamadı: ") + ex.Message);
            }
        }
    }
}
