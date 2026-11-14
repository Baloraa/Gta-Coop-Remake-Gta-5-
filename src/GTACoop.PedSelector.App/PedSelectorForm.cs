using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using GTACoop.Sync;

namespace GTACoop.PedSelector.App
{
    /// <summary>
    /// Bağımsız ped seçici arayüzü: ped ekle, listeyi düzenle, OpenIV (.oiv)
    /// paketi üret. GTACoop launcher'ı veya sunucusu olmadan çalışır.
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

        private AddonPedPackFile _packFile;
        private bool _loading;

        public PedSelectorForm()
        {
            _root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "GTACoop Ped Selector");
            _packDir = Path.Combine(_root, "peds", "paket");
            Directory.CreateDirectory(_packDir);

            Text = "AddonPed+";
            Width = 980;
            Height = 640;
            MinimumSize = new Size(780, 520);
            BackColor = Bg;
            ForeColor = Color.White;
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9.5f);

            _header = new Label
            {
                Text = "AddonPed+ — bağımsız addon ped modu",
                Dock = DockStyle.Top,
                Height = 54,
                Padding = new Padding(16, 14, 16, 0),
                Font = new Font("Segoe UI", 13f, FontStyle.Bold),
                ForeColor = Color.White
            };

            var hint = new Label
            {
                Text = "Ped dosyalarını ekleyin, seçin ve OpenIV paketi (.oiv) oluşturun. " +
                       "Oyun içinde F10 ile ped seçilir.",
                Dock = DockStyle.Top,
                Height = 30,
                Padding = new Padding(17, 0, 16, 0),
                ForeColor = Color.FromArgb(170, 170, 180)
            };

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
            _list.Columns.Add("Tür", 110);
            _list.Columns.Add("Sağlık", 80);
            _list.Columns.Add("Dosya", 80);
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
            buttons.Controls.Add(MakeButton("Ped Ekle...", (_, _) => AddPedFiles()));
            buttons.Controls.Add(MakeButton("Streamed Klasör Ekle...", (_, _) => AddPedFolder()));
            buttons.Controls.Add(MakeButton("Düzenle...", (_, _) => EditSelected()));
            buttons.Controls.Add(MakeButton("Kaydet", (_, _) => SavePack(announce: true)));
            buttons.Controls.Add(MakeButton("OIV Oluştur...", (_, _) => BuildOiv()));
            buttons.Controls.Add(MakeButton("Oyuna Kur...", (_, _) => InstallToGame()));
            buttons.Controls.Add(MakeButton("Kurulumu Kaldır", (_, _) => UninstallFromGame()));
            buttons.Controls.Add(MakeButton("Yenile", (_, _) => Reload()));
            buttons.Controls.Add(MakeButton("Klasörü Aç", (_, _) => OpenFolder(_root)));

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
            Controls.Add(hint);
            Controls.Add(_header);

            Reload();
        }

        private Button MakeButton(string text, EventHandler onClick)
        {
            var button = new Button
            {
                Text = text,
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
            return button;
        }

        private void Log(string message)
        {
            _log.AppendText(DateTime.Now.ToString("HH:mm:ss") + "  " + message + Environment.NewLine);
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

                _header.Text = "AddonPed+ — " + _packFile.Peds.Count + " ped";
                Log($"Liste: {_packFile.Peds.Count} ped, {_packFile.Peds.Count(p => p.Enabled)} etkin.");
            }
            catch (Exception ex)
            {
                Log("Liste okunamadı: " + ex.Message);
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
                    Log($"Kaydedildi: {_packFile.Peds.Count} ped, " +
                        $"{_packFile.Peds.Count(p => p.Enabled)} etkin.");
            }
            catch (Exception ex)
            {
                Log("peds.json yazılamadı: " + ex.Message);
            }
        }

        // -------------------------------------------------------------- düzenleme

        private void EditSelected()
        {
            if (_list.SelectedItems.Count == 0)
            {
                Log("Düzenlemek için listeden bir ped seçin.");
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
            Log($"Güncellendi: {ped.Model}" +
                (string.IsNullOrWhiteSpace(ped.Name) ? "" : " (" + ped.Name + ")"));
        }

        // ---------------------------------------------------------------- ekleme

        private void AddPedFiles()
        {
            using var dialog = new OpenFileDialog
            {
                Title = "Ped dosyaları (.yft .ydd .ytd .ymt)",
                Filter = "Ped dosyaları|*.yft;*.ydd;*.ytd;*.ymt|Tüm dosyalar|*.*",
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
            Log($"Ped dosyası eklendi: {copied}");
            Reload();
        }

        private void AddPedFolder()
        {
            using var dialog = new FolderBrowserDialog
            {
                Description = "Streamed ped klasörü (.ydd/.ytd içeren klasör)"
            };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;

            var source = dialog.SelectedPath;
            var name = Path.GetFileName(source.TrimEnd(Path.DirectorySeparatorChar));
            var target = Path.Combine(_packDir, string.IsNullOrWhiteSpace(name) ? "ped" : name);
            if (Directory.Exists(target))
            {
                Log("Bu klasör zaten ekli: " + name);
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
            Log($"Klasör eklendi: {name} ({copied} dosya)");
            Reload();
        }

        // ------------------------------------------------------------------- OIV

        private void BuildOiv()
        {
            using var dialog = new SaveFileDialog
            {
                Title = "OpenIV paketini kaydet",
                Filter = "OpenIV paketi|*.oiv",
                FileName = "GTACoop-AddonPeds.oiv",
                InitialDirectory = _root
            };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;

            try
            {
                Cursor = Cursors.WaitCursor;
                Log("Paket oluşturuluyor...");
                var path = PedOiv.Build(_packDir, dialog.FileName, Log);
                Log("Hazır: " + path);
                Log("Kurulum: OpenIV → Tools → Package Installer → bu .oiv dosyasını seç. " +
                    "Kaldırma: aynı paketi OpenIV üzerinden kaldır.");
            }
            catch (Exception ex)
            {
                Log("HATA: " + ex.Message);
                MessageBox.Show(this, ex.Message, "OIV oluşturulamadı",
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
                Description = "GTA V klasörü (GTA5.exe veya GTA5_Enhanced.exe olan klasör)"
            };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;

            var root = dialog.SelectedPath;
            if (!File.Exists(Path.Combine(root, "GTA5.exe")) &&
                !File.Exists(Path.Combine(root, "GTA5_Enhanced.exe")))
            {
                if (MessageBox.Show(this, "GTA5.exe bulunamadı. Yine de devam edilsin mi?",
                        "Emin misiniz?", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                    return;
            }

            try
            {
                Cursor = Cursors.WaitCursor;
                var oiv = Path.Combine(_root, "GTACoop-AddonPeds.oiv");
                Log("Paket hazırlanıyor...");
                PedOiv.Build(_packDir, oiv, Log);
                PedInstaller.Install(root, oiv, Log);
                MessageBox.Show(this,
                    "Kuruldu. Oyunda F10 ile ped seçebilirsiniz.\n\nKaldırmak için 'Kurulumu Kaldır'.",
                    "Tamam", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                Log("HATA: " + ex.Message);
                MessageBox.Show(this, ex.Message, "Kurulamadı",
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
                Log("Kurulum kaydı yok; kaldırılacak bir şey bulunamadı.");
                return;
            }

            if (MessageBox.Show(this, "Ped modu oyundan kaldırılsın mı?", "Onay",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            try
            {
                Cursor = Cursors.WaitCursor;
                PedInstaller.Uninstall(Log);
                MessageBox.Show(this, "Kaldırıldı.", "Tamam",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                Log("HATA: " + ex.Message);
                MessageBox.Show(this, ex.Message, "Kaldırılamadı",
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
                Log("Klasör açılamadı: " + ex.Message);
            }
        }
    }
}
