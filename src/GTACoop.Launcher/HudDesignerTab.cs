using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace GTACoop.Remake;

/// <summary>
/// "Kendi arayüzünü tasarla": HUD öğelerini (panel, yazı, PNG resim, saat,
/// ping, oyuncu listesi) sürükleyerek yerleştirir, kısayol tuşları tanımlar ve
/// yerleşimi oyun içi çizicinin okuduğu dosyaya yazar.
/// </summary>
public sealed class HudDesignerTab : UserControl
{
    private static readonly (string Key, string Label)[] Actions =
    {
        ("toggle", "HUD'u aç/kapat"),
        ("players", "Oyuncu listesi"),
        ("voice", "Mikrofonu aç/kapat"),
        ("radar", "Radarı aç/kapat"),
        ("hide", "Arayüzü gizle")
    };

    private readonly HudLayout _layout;
    private readonly HudCanvas _canvas;

    private readonly ListView _list = new();
    private readonly ListView _shortcutList = new();

    private readonly NumericUpDown _x = Num(0, 0.98m, 0.01m, 3);
    private readonly NumericUpDown _y = Num(0, 0.98m, 0.01m, 3);
    private readonly NumericUpDown _w = Num(0.01m, 1m, 0.01m, 3);
    private readonly NumericUpDown _h = Num(0.01m, 1m, 0.01m, 3);
    private readonly NumericUpDown _font = Num(0.1m, 1.5m, 0.05m, 2);
    private readonly NumericUpDown _opacity = Num(0, 100, 5, 0);
    private readonly TextBox _text = new();
    private readonly Button _colorBtn = new();
    private readonly CheckBox _hideHud = new();
    private readonly CheckBox _hideRadar = new();
    private readonly CheckBox _hideMap = new();
    private readonly CheckBox _hideStars = new();
    private readonly CheckBox _hideStreet = new();
    private readonly CheckBox _hideVehicle = new();
    private readonly CheckBox _hideHelp = new();
    private readonly CheckBox _enabled = new();
    private readonly TextBox _key = new();
    private readonly ComboBox _action = new();

    private HudElement? _selected;
    private HudShortcut? _selectedShortcut;
    private bool _syncing;

    public HudDesignerTab()
    {
        Dock = DockStyle.Fill;
        _layout = HudStore.Load();

        // ------------------------------------------------------------ üst çubuk
        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            WrapContents = true,
            Padding = new Padding(6, 6, 6, 4)
        };
        toolbar.Controls.Add(Ui.Btn(T("Kaydet ve Oyuna Uygula"), (_, _) => SaveLayout()));
        toolbar.Controls.Add(Ui.Btn(T("Dışa Aktar (JSON)..."), (_, _) => ExportLayout()));
        toolbar.Controls.Add(Ui.Btn(T("İçe Aktar (JSON)..."), (_, _) => ImportLayout()));
        toolbar.Controls.Add(Ui.Btn(T("Örnek Yerleşim"), (_, _) => LoadSample()));
        toolbar.Controls.Add(Ui.Btn(T("Klasörü Aç"), (_, _) => OpenUiFolder()));

        // ------------------------------------------------------------ tuval + panel
        _canvas = new HudCanvas(_layout)
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(8)
        };
        _canvas.SelectionChanged += element => SelectElement(element);
        _canvas.DragMoved += () =>
        {
            if (_selected == null) return;
            _syncing = true;
            _x.Value = (decimal)Math.Clamp(_selected.X, 0, 0.98);
            _y.Value = (decimal)Math.Clamp(_selected.Y, 0, 0.98);
            _syncing = false;
        };

        var right = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(4),
            Width = 350
        };

        // --- öğeler
        var itemsGroup = new DarkGroup
        {
            Text = T("Öğeler"),
            Dock = DockStyle.Top,
            Padding = new Padding(8, 24, 8, 8)
        };

        Ui.StyleList(_list, (T("Öğe"), 210), (T("Tür"), 130));
        _list.Height = 150;
        _list.Dock = DockStyle.None;
        _list.Width = 344;
        _list.SelectedIndexChanged += (_, _) =>
        {
            if (_syncing) return;
            if (_list.SelectedIndices.Count > 0 && _list.SelectedIndices[0] < _layout.Elements.Count)
                SelectElement(_layout.Elements[_list.SelectedIndices[0]]);
        };

        var addRow = Ui.Flow();
        addRow.Controls.Add(Ui.Btn(T("+ Panel"), (_, _) => AddElement("panel")));
        addRow.Controls.Add(Ui.Btn(T("+ Yazı"), (_, _) => AddElement("text")));
        addRow.Controls.Add(Ui.Btn(T("+ Saat"), (_, _) => AddElement("clock")));
        addRow.Controls.Add(Ui.Btn(T("+ Ping"), (_, _) => AddElement("ping")));
        addRow.Controls.Add(Ui.Btn(T("+ Oyuncular"), (_, _) => AddElement("players")));
        addRow.Controls.Add(Ui.Btn(T("+ Çubuk"), (_, _) => AddElement("bar")));
        addRow.Controls.Add(Ui.Btn(T("+ PNG Resim..."), (_, _) => AddImage()));

        var itemActions = Ui.Flow();
        itemActions.Controls.Add(Ui.Btn(T("Sil"), (_, _) => RemoveElement()));
        itemActions.Controls.Add(Ui.Btn(T("Yukarı"), (_, _) => MoveElement(-1)));
        itemActions.Controls.Add(Ui.Btn(T("Aşağı"), (_, _) => MoveElement(1)));

        var itemsBody = Stack();
        itemsBody.Controls.Add(_list);
        itemsBody.Controls.Add(addRow);
        itemsBody.Controls.Add(itemActions);
        itemsGroup.Controls.Add(itemsBody);

        // --- özellikler
        var propsGroup = new DarkGroup
        {
            Text = T("Seçili öğe"),
            Dock = DockStyle.Top,
            Padding = new Padding(8, 24, 8, 8)
        };

        _text.Width = 238;
        _text.Margin = new Padding(4, 3, 4, 3);
        _text.TextChanged += (_, _) => ApplyProperties();

        _colorBtn.Text = T("Renk...");
        _colorBtn.AutoSize = true;
        _colorBtn.Margin = new Padding(4, 3, 4, 3);
        _colorBtn.Click += (_, _) => PickColor();

        foreach (var num in new[] { _x, _y, _w, _h, _font, _opacity })
            num.ValueChanged += (_, _) => ApplyProperties();

        var posGrid = new TableLayoutPanel
        {
            Dock = DockStyle.None,
            Width = 348,
            Height = 66,
            ColumnCount = 4,
            AutoSize = false,
            Margin = new Padding(0, 2, 0, 4)
        };
        posGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 34));
        posGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96));
        posGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 34));
        posGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96));
        posGrid.Controls.Add(Dim("X"), 0, 0);
        posGrid.Controls.Add(_x, 1, 0);
        posGrid.Controls.Add(Dim("Y"), 2, 0);
        posGrid.Controls.Add(_y, 3, 0);
        posGrid.Controls.Add(Dim("W"), 0, 1);
        posGrid.Controls.Add(_w, 1, 1);
        posGrid.Controls.Add(Dim("H"), 2, 1);
        posGrid.Controls.Add(_h, 3, 1);

        var styleGrid = new TableLayoutPanel
        {
            Dock = DockStyle.None,
            Width = 348,
            Height = 34,
            ColumnCount = 4,
            AutoSize = false,
            Margin = new Padding(0, 2, 0, 4)
        };
        styleGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 94));
        styleGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96));
        styleGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
        styleGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96));
        styleGrid.Controls.Add(Dim(T("Yazı boyutu")), 0, 0);
        styleGrid.Controls.Add(_font, 1, 0);
        styleGrid.Controls.Add(Dim(T("Opaklık")), 2, 0);
        styleGrid.Controls.Add(_opacity, 3, 0);

        var textRow = Ui.Flow();
        textRow.Controls.Add(Dim(T("Metin")));
        textRow.Controls.Add(_text);

        var hint = new Label
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            MaximumSize = new Size(360, 0),
            ForeColor = Color.DimGray,
            Padding = new Padding(4, 2, 4, 2),
            Text = T("Öğeyi tuvalde fareyle sürükleyebilirsin. PNG resimler oyun içinde piksel ızgarası olarak çizilir.")
        };

        var propsBody = Stack();
        propsBody.Controls.Add(posGrid);
        propsBody.Controls.Add(styleGrid);
        propsBody.Controls.Add(_colorBtn);
        propsBody.Controls.Add(textRow);
        propsBody.Controls.Add(hint);
        propsGroup.Controls.Add(propsBody);

        // --- kısayollar
        var keysGroup = new DarkGroup
        {
            Text = T("Kısayollar"),
            Dock = DockStyle.Top,
            Padding = new Padding(8, 24, 8, 8)
        };

        Ui.StyleList(_shortcutList, (T("Tuş"), 80), (T("Eylem"), 270));
        _shortcutList.Height = 110;
        _shortcutList.Dock = DockStyle.None;
        _shortcutList.Width = 344;
        _shortcutList.SelectedIndexChanged += (_, _) =>
        {
            if (_syncing) return;
            if (_shortcutList.SelectedIndices.Count > 0 && _shortcutList.SelectedIndices[0] < _layout.Shortcuts.Count)
            {
                _selectedShortcut = _layout.Shortcuts[_shortcutList.SelectedIndices[0]];
                _key.Text = _selectedShortcut.Key;
                _action.SelectedIndex = Math.Max(0, Array.FindIndex(Actions, a => a.Key == _selectedShortcut.Action));
            }
        };

        _key.Width = 70;
        _key.MaxLength = 8;
        _key.Margin = new Padding(4, 4, 4, 4);

        foreach (var action in Actions) _action.Items.Add(T(action.Label));
        _action.SelectedIndex = 0;
        _action.DropDownStyle = ComboBoxStyle.DropDownList;
        _action.Width = 170;
        _action.Margin = new Padding(4, 4, 4, 4);

        var keyRow = Ui.Flow();
        keyRow.Controls.Add(Dim(T("Tuş")));
        keyRow.Controls.Add(_key);
        keyRow.Controls.Add(_action);
        keyRow.Controls.Add(Ui.Btn(T("Ekle / Güncelle"), (_, _) => SaveShortcut()));
        keyRow.Controls.Add(Ui.Btn(T("Sil"), (_, _) => RemoveShortcut()));

        var keysBody = Stack();
        keysBody.Controls.Add(keyRow);
        keysBody.Controls.Add(_shortcutList);
        keysGroup.Controls.Add(keysBody);

        // --- ayarlar
        var optionsGroup = new DarkGroup
        {
            Text = T("Oyun içi ayarlar"),
            Dock = DockStyle.Top,
            Padding = new Padding(8, 24, 8, 8)
        };

        CheckBox Opt(CheckBox box, string label, bool value, Action<bool> apply)
        {
            box.Text = label;
            box.AutoSize = true;
            box.Checked = value;
            box.CheckedChanged += (_, _) =>
            {
                if (_syncing) return;
                apply(box.Checked);
                SaveLayout(silent: true);
            };
            return box;
        }

        var enabledBox = Opt(_enabled, T("Bu özellik etkin (oyun içinde kendi HUD'um)"), _layout.Enabled,
            v => _layout.Enabled = v);
        var hideHudBox = Opt(_hideHud, T("Oyunun kendi HUD'unu gizle (radar dahil)"), _layout.HideGameHud,
            v => _layout.HideGameHud = v);
        var hideRadarBox = Opt(_hideRadar, T("Haritayı/radarı tamamen gizle"), _layout.HideRadar,
            v => _layout.HideRadar = v);
        var hideMapBox = Opt(_hideMap, T("Haritanın resmini gizle (radar çerçevesi kalır)"), _layout.HideMinimapMap,
            v => _layout.HideMinimapMap = v);
        var hideStarsBox = Opt(_hideStars, T("Aranan yıldızlarını gizle"), _layout.HideWantedStars,
            v => _layout.HideWantedStars = v);
        var hideStreetBox = Opt(_hideStreet, T("Sokak/cadde adını gizle"), _layout.HideStreetName,
            v => _layout.HideStreetName = v);
        var hideVehicleBox = Opt(_hideVehicle, T("Araç adı ve sınıfını gizle"), _layout.HideVehicleInfo,
            v => _layout.HideVehicleInfo = v);
        var hideHelpBox = Opt(_hideHelp, T("Yardım metnini gizle"), _layout.HideHelpText,
            v => _layout.HideHelpText = v);

        var resetBtn = Ui.Btn(T("Kapat ve varsayılana dön"), (_, _) => ResetHud());
        resetBtn.AutoSize = true;
        resetBtn.Margin = new Padding(4, 8, 4, 4);

        var note = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(360, 0),
            ForeColor = Color.DimGray,
            Padding = new Padding(4, 2, 4, 2),
            Text = T("Kapalıyken oyunun kendi arayüzü aynen kalır. Bu sayfa oyun ve sunucu dosyalarına hiçbir şey yazmaz; istediğin an kapatabilirsin.")
        };

        var optionsBody = Stack();
        optionsBody.Controls.Add(enabledBox);
        optionsBody.Controls.Add(hideHudBox);
        optionsBody.Controls.Add(hideRadarBox);
        optionsBody.Controls.Add(hideMapBox);
        optionsBody.Controls.Add(hideStarsBox);
        optionsBody.Controls.Add(hideStreetBox);
        optionsBody.Controls.Add(hideVehicleBox);
        optionsBody.Controls.Add(hideHelpBox);
        optionsBody.Controls.Add(resetBtn);
        optionsBody.Controls.Add(note);
        optionsGroup.Controls.Add(optionsBody);

        foreach (var group in new[] { itemsGroup, propsGroup, keysGroup, optionsGroup })
        {
            group.Dock = DockStyle.None;
            group.Width = 386;
            group.Margin = new Padding(2, 2, 2, 8);
        }

        itemsGroup.Height = 396;
        propsGroup.Height = 312;
        keysGroup.Height = 246;
        optionsGroup.Height = 380;

        right.Controls.Add(itemsGroup);
        right.Controls.Add(propsGroup);
        right.Controls.Add(keysGroup);
        right.Controls.Add(optionsGroup);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(0)
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 420));
        root.Controls.Add(_canvas, 0, 0);
        root.Controls.Add(right, 1, 0);

        Controls.Add(root);
        Controls.Add(toolbar);

        RefreshElements();
        RefreshShortcuts();
        SelectElement(_layout.Elements.FirstOrDefault());

    }

    // ------------------------------------------------------------- yardımcı

    private static FlowLayoutPanel Stack() => new()
    {
        Dock = DockStyle.Fill,
        FlowDirection = FlowDirection.TopDown,
        WrapContents = false,
        AutoScroll = true,
        Padding = new Padding(0)
    };

    private static Label Dim(string text) => new()
    {
        Text = text,
        AutoSize = true,
        ForeColor = Color.DimGray,
        Margin = new Padding(4, 6, 4, 4)
    };

    private static NumericUpDown Num(decimal min, decimal max, decimal step, int decimals) => new()
    {
        Minimum = min,
        Maximum = max,
        Increment = step,
        DecimalPlaces = decimals,
        Width = 78,
        Margin = new Padding(2, 3, 8, 3)
    };

    private void SaveLayout(bool silent = false)
    {
        try
        {
            _layout.Name = string.IsNullOrWhiteSpace(_layout.Name) ? "Benim HUD" : _layout.Name;
            HudStore.Save(_layout);
            if (silent) return;
            Ui.Info(FindForm()!, T("Yerleşim kaydedildi ve oyuna uygulandı:\r\n{0}\r\n\r\nOyun içinde F7 ile HUD'u açıp kapatabilirsin.", HudStore.DefaultPath));
        }
        catch (Exception ex)
        {
            AppEnv.LogError("HUD kaydedilemedi: " + ex.Message);
            if (!silent) Ui.Error(FindForm()!, T("Yerleşim kaydedilemedi:\r\n") + ex.Message);
        }
    }

    /// <summary>Özelliği kapatır ve tüm gizlemeleri geri alır (dosya silinmez).</summary>
    private void ResetHud()
    {
        _layout.Enabled = false;
        _layout.HideGameHud = false;
        _layout.HideRadar = false;
        _layout.HideMinimapMap = false;
        _layout.HideWantedStars = false;
        _layout.HideStreetName = false;
        _layout.HideVehicleInfo = false;
        _layout.HideHelpText = false;

        try { HudStore.Save(_layout); }
        catch (Exception ex) { AppEnv.LogError("HUD kaydedilemedi: " + ex.Message); }

        _syncing = true;
        _enabled.Checked = false;
        _hideHud.Checked = false;
        _hideRadar.Checked = false;
        _hideMap.Checked = false;
        _hideStars.Checked = false;
        _hideStreet.Checked = false;
        _hideVehicle.Checked = false;
        _hideHelp.Checked = false;
        _syncing = false;

        Ui.Info(FindForm()!, T("HUD kapatıldı. Oyun içinde oyunun kendi arayüzü geri gelir."));
    }

    private void ExportLayout()
    {
        using var dlg = new SaveFileDialog
        {
            Title = T("HUD yerleşimini dışa aktar"),
            Filter = "HUD yerleşimi (*.json)|*.json",
            FileName = "hud.json"
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            HudStore.Save(_layout, dlg.FileName);
            Ui.Info(FindForm()!, T("Yerleşim dışa aktarıldı:\r\n{0}", dlg.FileName));
        }
        catch (Exception ex)
        {
            Ui.Error(FindForm()!, T("Dışa aktarılamadı:\r\n") + ex.Message);
        }
    }

    private void ImportLayout()
    {
        using var dlg = new OpenFileDialog
        {
            Title = T("HUD yerleşimi içe aktar"),
            Filter = "HUD yerleşimi (*.json)|*.json"
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            var loaded = HudStore.Load(dlg.FileName);
            _layout.Name = loaded.Name;
            _layout.Enabled = loaded.Enabled;
            _layout.HideGameHud = loaded.HideGameHud;
            _layout.HideRadar = loaded.HideRadar;
            _layout.Elements.Clear();
            _layout.Elements.AddRange(loaded.Elements);
            _layout.Shortcuts.Clear();
            _layout.Shortcuts.AddRange(loaded.Shortcuts);

            _enabled.Checked = _layout.Enabled;
            _hideHud.Checked = _layout.HideGameHud;
            _hideRadar.Checked = _layout.HideRadar;

            RefreshElements();
            RefreshShortcuts();
            SelectElement(_layout.Elements.FirstOrDefault());
            SaveLayout(silent: true);
            Ui.Info(FindForm()!, T("Yerleşim içe aktarıldı ve kaydedildi."));
        }
        catch (Exception ex)
        {
            Ui.Error(FindForm()!, T("İçe aktarılamadı:\r\n") + ex.Message);
        }
    }

    private void LoadSample()
    {
        var sample = HudStore.CreateSample();
        _layout.Elements.Clear();
        _layout.Elements.AddRange(sample.Elements);
        _layout.Shortcuts.Clear();
        _layout.Shortcuts.AddRange(sample.Shortcuts);

        RefreshElements();
        RefreshShortcuts();
        SelectElement(_layout.Elements.FirstOrDefault());
        SaveLayout(silent: true);
    }

    private void OpenUiFolder()
    {
        try
        {
            Directory.CreateDirectory(HudStore.UiFolder);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(HudStore.UiFolder)
            {
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            AppEnv.LogError("HUD klasörü açılamadı: " + ex.Message);
        }
    }

    private void RefreshElements()
    {
        _syncing = true;
        _list.BeginUpdate();
        _list.Items.Clear();
        foreach (var element in _layout.Elements)
        {
            var item = new ListViewItem(element.DisplayLabel);
            item.SubItems.Add(element.Type);
            _list.Items.Add(item);
        }
        _list.EndUpdate();
        _syncing = false;

        _canvas.Selected = _selected;
        _canvas.Invalidate();
    }

    private void RefreshShortcuts()
    {
        _syncing = true;
        _shortcutList.BeginUpdate();
        _shortcutList.Items.Clear();
        foreach (var shortcut in _layout.Shortcuts)
        {
            var item = new ListViewItem(shortcut.Key);
            item.SubItems.Add(T(shortcut.ActionText));
            _shortcutList.Items.Add(item);
        }
        _shortcutList.EndUpdate();
        _syncing = false;
    }

    private void SelectElement(HudElement? element)
    {
        _selected = element;
        _canvas.Selected = element;

        _syncing = true;
        try
        {
            var index = element == null ? -1 : _layout.Elements.IndexOf(element);
            _list.SelectedIndices.Clear();
            if (index >= 0) _list.SelectedIndices.Add(index);

            var enabled = element != null;
            foreach (var control in new Control[] { _x, _y, _w, _h, _font, _opacity, _text, _colorBtn })
                control.Enabled = enabled;

            if (element != null)
            {
                _x.Value = (decimal)Math.Clamp(element.X, 0, 0.98);
                _y.Value = (decimal)Math.Clamp(element.Y, 0, 0.98);
                _w.Value = (decimal)Math.Clamp(element.W, 0.01, 1);
                _h.Value = (decimal)Math.Clamp(element.H, 0.01, 1);
                _font.Value = (decimal)Math.Clamp(element.FontSize, 0.1, 1.5);
                _opacity.Value = (decimal)Math.Clamp(element.Opacity * 100, 0, 100);
                _text.Text = element.Text;
                _colorBtn.BackColor = ParseColor(element.Color);
            }
        }
        finally
        {
            _syncing = false;
        }

        _canvas.Invalidate();
    }

    private void ApplyProperties()
    {
        if (_syncing || _selected == null) return;

        _selected.X = (double)_x.Value;
        _selected.Y = (double)_y.Value;
        _selected.W = (double)_w.Value;
        _selected.H = (double)_h.Value;
        _selected.FontSize = (double)_font.Value;
        _selected.Opacity = (double)_opacity.Value / 100.0;
        _selected.Text = _text.Text;

        var index = _layout.Elements.IndexOf(_selected);
        if (index >= 0 && index < _list.Items.Count)
            _list.Items[index].Text = _selected.DisplayLabel;

        _canvas.Invalidate();
    }

    private void PickColor()
    {
        if (_selected == null) return;

        using var dlg = new ColorDialog { Color = ParseColor(_selected.Color), FullOpen = true };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        _selected.Color = $"#{dlg.Color.R:X2}{dlg.Color.G:X2}{dlg.Color.B:X2}";
        _colorBtn.BackColor = dlg.Color;
        _canvas.Invalidate();
    }

    private static Color ParseColor(string hex)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(hex))
            {
                var text = hex.TrimStart('#');
                if (text.Length == 6)
                    return Color.FromArgb(
                        Convert.ToInt32(text[..2], 16),
                        Convert.ToInt32(text.Substring(2, 2), 16),
                        Convert.ToInt32(text.Substring(4, 2), 16));
            }
        }
        catch { }
        return Theme.Text;
    }

    private void AddElement(string type)
    {
        var element = new HudElement
        {
            Type = type,
            X = 0.03,
            Y = 0.03,
            W = type == "panel" ? 0.24 : 0.2,
            H = type == "panel" ? 0.1 : 0.032,
            Text = type switch
            {
                "text" => T("Yeni yazı"),
                "clock" => "",
                "ping" => "",
                "players" => "",
                _ => ""
            }
        };

        _layout.Elements.Add(element);
        RefreshElements();
        SelectElement(element);
    }

    private void AddImage()
    {
        using var dlg = new OpenFileDialog
        {
            Title = T("HUD'a resim ekle"),
            Filter = "PNG resmi (*.png)|*.png|Resim (*.jpg;*.jpeg)|*.jpg;*.jpeg"
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            using var probe = Image.FromFile(dlg.FileName);
            var aspect = (double)probe.Height / probe.Width;
            var width = 0.16;
            var height = Math.Clamp(width * aspect * (16.0 / 9.0), 0.02, 0.6);

            var element = new HudElement
            {
                Type = "image",
                X = 0.03,
                Y = 0.03,
                W = width,
                H = height
            };

            HudStore.AttachImage(element, dlg.FileName);
            _layout.Elements.Add(element);
            RefreshElements();
            SelectElement(element);
            _canvas.Invalidate();
        }
        catch (Exception ex)
        {
            AppEnv.LogError("Resim eklenemedi: " + ex.Message);
            Ui.Error(FindForm()!, T("Resim eklenemedi:\r\n") + ex.Message);
        }
    }

    private void RemoveElement()
    {
        if (_selected == null) return;
        _layout.Elements.Remove(_selected);
        _selected = null;
        RefreshElements();
        SelectElement(_layout.Elements.FirstOrDefault());
    }

    private void MoveElement(int delta)
    {
        if (_selected == null) return;
        var index = _layout.Elements.IndexOf(_selected);
        var target = index + delta;
        if (index < 0 || target < 0 || target >= _layout.Elements.Count) return;

        _layout.Elements.RemoveAt(index);
        _layout.Elements.Insert(target, _selected);
        RefreshElements();
        SelectElement(_selected);
    }

    private void SaveShortcut()
    {
        var key = _key.Text.Trim().ToUpperInvariant();
        if (key.Length == 0)
        {
            Ui.Warn(FindForm()!, T("Bir tuş yazın (ör. F7 veya K)."));
            return;
        }

        var action = Actions[Math.Max(0, _action.SelectedIndex)].Key;

        if (_selectedShortcut != null)
        {
            _selectedShortcut.Key = key;
            _selectedShortcut.Action = action;
        }
        else
        {
            _layout.Shortcuts.Add(new HudShortcut { Key = key, Action = action });
            _selectedShortcut = null;
        }

        RefreshShortcuts();
        SaveLayout(silent: true);
    }

    private void RemoveShortcut()
    {
        if (_selectedShortcut == null) return;
        _layout.Shortcuts.Remove(_selectedShortcut);
        _selectedShortcut = null;
        RefreshShortcuts();
        SaveLayout(silent: true);
    }
}

/// <summary>Tasarım tuvali: 16:9 önizleme, sürükle-bırak ve seçim.</summary>
internal sealed class HudCanvas : Panel
{
    private readonly HudLayout _layout;
    private readonly Dictionary<string, Image?> _images = new(StringComparer.OrdinalIgnoreCase);
    private HudElement? _drag;
    private Point _dragOffset;

    public HudElement? Selected { get; set; }

    public event Action<HudElement?>? SelectionChanged;

    public HudCanvas(HudLayout layout)
    {
        _layout = layout;
        DoubleBuffered = true;
        BackColor = Theme.Background;
        Cursor = Cursors.Default;
    }

    private RectangleF Viewport()
    {
        var available = ClientRectangle;
        available.Inflate(-10, -10);
        if (available.Width <= 0 || available.Height <= 0) return RectangleF.Empty;

        float width = available.Width;
        var height = width * 9f / 16f;
        if (height > available.Height)
        {
            height = available.Height;
            width = height * 16f / 9f;
        }

        return new RectangleF(
            available.Left + (available.Width - width) / 2f,
            available.Top + (available.Height - height) / 2f,
            width, height);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        var view = Viewport();
        if (view.Width <= 1) return;

        g.SmoothingMode = SmoothingMode.AntiAlias;

        using (var back = new SolidBrush(Color.FromArgb(24, 26, 30)))
            g.FillRectangle(back, view);

        using (var grid = new Pen(Color.FromArgb(38, 40, 46)))
        {
            for (var i = 1; i < 10; i++)
            {
                var x = view.Left + view.Width * i / 10f;
                var y = view.Top + view.Height * i / 10f;
                g.DrawLine(grid, x, view.Top, x, view.Bottom);
                g.DrawLine(grid, view.Left, y, view.Right, y);
            }
        }

        foreach (var element in _layout.Elements)
            Draw(g, element, view);

        if (Selected != null)
        {
            var r = Box(Selected, view);
            using var pen = new Pen(Theme.Accent, 1.5f) { DashStyle = DashStyle.Dash };
            g.DrawRectangle(pen, r.X, r.Y, r.Width, r.Height);
        }

        using var border = new Pen(Theme.Border);
        g.DrawRectangle(border, view.X, view.Y, view.Width, view.Height);
    }

    private static RectangleF Box(HudElement element, RectangleF view) => new(
        view.Left + (float)element.X * view.Width,
        view.Top + (float)element.Y * view.Height,
        Math.Max(2, (float)element.W * view.Width),
        Math.Max(2, (float)element.H * view.Height));

    private void Draw(Graphics g, HudElement element, RectangleF view)
    {
        var box = Box(element, view);
        var alpha = (int)Math.Round(255 * Math.Clamp(element.Opacity, 0, 1));

        switch ((element.Type ?? "").ToLowerInvariant())
        {
            case "panel":
                using (var brush = new SolidBrush(Color.FromArgb(alpha, Parse(element.Color))))
                    g.FillRectangle(brush, box);
                break;

            case "text":
                DrawText(g, element.Text, box, element, view, Parse(element.Color));
                break;

            case "clock":
                DrawText(g, DateTime.Now.ToString("HH:mm"), box, element, view, Parse(element.Color));
                break;

            case "ping":
                DrawText(g, "42 ms", box, element, view, Parse(element.Color));
                break;

            case "players":
            {
                var scale = Math.Max(7f, (float)element.FontSize * view.Height * 0.075f);
                var lines = new[] { "Reelone  12ms", "[MIC] zaza  41ms", "Ahmet  88ms" };
                for (var i = 0; i < lines.Length; i++)
                {
                    using var font = new Font("Segoe UI", scale, FontStyle.Regular, GraphicsUnit.Pixel);
                    g.DrawString(lines[i], font, new SolidBrush(Parse(element.Color)),
                        box.Left + 2, box.Top + i * scale * 1.25f);
                }
                break;
            }

            case "bar":
                using (var back = new SolidBrush(Color.FromArgb(160, 0, 0, 0)))
                    g.FillRectangle(back, box);
                using (var hp = new SolidBrush(Parse(element.Color)))
                    g.FillRectangle(hp, box.Left + 1, box.Top + 1, (box.Width - 2) * 0.72f, box.Height * 0.55f - 2);
                using (var armor = new SolidBrush(Color.FromArgb(127, 179, 255)))
                    g.FillRectangle(armor, box.Left + 1, box.Top + box.Height * 0.55f + 1,
                        (box.Width - 2) * 0.35f, box.Height * 0.45f - 2);
                break;

            case "image":
            {
                var image = LoadImage(element.Png);
                if (image != null)
                {
                    g.DrawImage(image, box);
                }
                else
                {
                    using var pen = new Pen(Theme.Border);
                    g.DrawRectangle(pen, box.X, box.Y, box.Width, box.Height);
                    DrawText(g, T("resim: ") + element.Png, box, element, view, Theme.Muted);
                }
                break;
            }
        }
    }

    private static void DrawText(Graphics g, string text, RectangleF box, HudElement element,
        RectangleF view, Color color)
    {
        if (string.IsNullOrEmpty(text)) return;

        var size = Math.Max(7f, (float)element.FontSize * view.Height * 0.075f);
        using var font = new Font("Segoe UI", size, FontStyle.Regular, GraphicsUnit.Pixel);
        using var brush = new SolidBrush(color);

        var format = new StringFormat
        {
            Alignment = (element.Align ?? "left").ToLowerInvariant() switch
            {
                "center" => StringAlignment.Center,
                "right" => StringAlignment.Far,
                _ => StringAlignment.Near
            },
            LineAlignment = StringAlignment.Center,
            Trimming = StringTrimming.EllipsisCharacter
        };

        g.DrawString(text, font, brush, box, format);
    }

    private Image? LoadImage(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        if (_images.TryGetValue(name, out var cached)) return cached;

        try
        {
            var path = Path.Combine(HudStore.AssetsFolder, name);
            var image = File.Exists(path) ? Image.FromFile(path) : null;
            _images[name] = image;
            return image;
        }
        catch
        {
            _images[name] = null;
            return null;
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        var view = Viewport();
        if (view.Width <= 1) return;

        _drag = null;
        for (var i = _layout.Elements.Count - 1; i >= 0; i--)
        {
            var element = _layout.Elements[i];
            var box = Box(element, view);
            if (!box.Contains(e.Location)) continue;

            _drag = element;
            _dragOffset = new Point((int)(e.X - box.X), (int)(e.Y - box.Y));
            Selected = element;
            SelectionChanged?.Invoke(element);
            Invalidate();
            return;
        }

        Selected = null;
        SelectionChanged?.Invoke(null);
        Invalidate();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_drag == null || e.Button != MouseButtons.Left) return;

        var view = Viewport();
        if (view.Width <= 1) return;

        var x = (e.X - _dragOffset.X - view.Left) / view.Width;
        var y = (e.Y - _dragOffset.Y - view.Top) / view.Height;

        _drag.X = Math.Clamp(x, 0, 1 - _drag.W);
        _drag.Y = Math.Clamp(y, 0, 1 - _drag.H);

        Invalidate();
        DragMoved?.Invoke();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        _drag = null;
    }

    /// <summary>Sürükleme bittiğinde paneldeki X/Y alanlarını güncellemek için.</summary>
    public event Action? DragMoved;

    private static Color Parse(string hex)
    {
        try
        {
            var text = (hex ?? "").TrimStart('#');
            if (text.Length == 6)
                return Color.FromArgb(
                    Convert.ToInt32(text[..2], 16),
                    Convert.ToInt32(text.Substring(2, 2), 16),
                    Convert.ToInt32(text.Substring(4, 2), 16));
        }
        catch { }
        return Theme.Text;
    }
}
