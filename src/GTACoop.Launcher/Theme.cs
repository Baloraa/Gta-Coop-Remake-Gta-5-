using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace GTACoop.Remake;

/// <summary>
/// Tam siyah, düz arayüz teması. Yerleşimi ve davranışı değiştirmez; yalnızca
/// renkleri uygular. Liste başlıkları/satırları ve pencere başlık çubuğu da
/// koyu çizilir, böylece beyaz alan kalmaz. Anlam taşıyan durum renkleri
/// (yeşil/kırmızı) korunur.
/// </summary>
internal static class Theme
{
    // Saf siyah değil: göz yormayan çok koyu gri tonları.
    public static readonly Color Background = Color.FromArgb(13, 13, 15);
    public static readonly Color Surface = Color.FromArgb(18, 18, 21);
    public static readonly Color SurfaceAlt = Color.FromArgb(26, 26, 31);
    public static readonly Color Selection = Color.FromArgb(19, 60, 34);
    public static readonly Color Border = Color.FromArgb(42, 42, 49);
    public static readonly Color Text = Color.FromArgb(237, 237, 237);
    public static readonly Color Muted = Color.FromArgb(154, 160, 166);
    public static readonly Color Accent = Color.FromArgb(53, 199, 89);
    public static readonly Color AccentAlt = Color.FromArgb(47, 163, 107);

    public static bool Enabled { get; private set; }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr value);

    /// <summary>Formu ve içindeki tüm denetimleri temaya uyarlar.</summary>
    public static void Apply(Form form)
    {
        Enabled = true;
        InitDarkMode();
        form.BackColor = Background;
        form.ForeColor = Text;
        ApplyToChildren(form);
        DarkenScrollbars(form);

        if (form is MainForm main)
            main.ApplyTabStyle();

        ApplyDarkTitleBar(form);
    }

    /// <summary>Sonradan oluşturulan sekmeler için.</summary>
    public static void Apply(Control control)
    {
        if (!Enabled) return;
        control.BackColor = Background;
        control.ForeColor = Text;
        ApplyToChildren(control);
        DarkenScrollbars(control);
    }

    // Windows 11'de yerel kaydırma çubuklarının koyu çizilmesi için uygulama
    // karanlık moda zorlanır (belgelenmemiş ama yaygın kullanılan ordinal).
    [DllImport("uxtheme.dll", EntryPoint = "#135")]
    private static extern int SetPreferredAppMode(int mode);

    [DllImport("uxtheme.dll", EntryPoint = "#136")]
    private static extern void FlushMenuThemes();

    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    private static extern int SetWindowTheme(IntPtr hWnd, string subAppName, string? subIdList);

    [DllImport("user32.dll")]
    private static extern bool EnumChildWindows(IntPtr parent, EnumWindowsProc callback, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder name, int max);

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    /// <summary>
    /// ListView/TextBox gibi denetimlerin kaydırma çubuklarını Windows açık
    /// renkte çizer. Yalnızca kaydırma çubukları koyu temaya çevrilir; diğer
    /// denetimlerin görünümü değişmez.
    /// </summary>
    public static void DarkenScrollbars(Control root)
    {
        if (!root.IsHandleCreated) return;
        try
        {
            EnumChildWindows(root.Handle, (h, _) =>
            {
                var name = new StringBuilder(64);
                if (GetClassName(h, name, name.Capacity) > 0 &&
                    name.ToString().Contains("ScrollBar", StringComparison.OrdinalIgnoreCase))
                {
                    SetWindowTheme(h, "DarkMode_Explorer", null);
                }
                return true;
            }, IntPtr.Zero);
        }
        catch
        {
            // Eski Windows sürümlerinde kaydırma çubukları sistem renginde kalır.
        }
    }

    /// <summary>Yerel (Windows) denetimlerin koyu çizilmesi için uygulama
    /// karanlık moda alınır. Pencere oluşturulmadan önce çağrılmalıdır.</summary>
    public static void InitDarkMode()
    {
        if (_darkModeTried) return;
        _darkModeTried = true;
        try
        {
            SetPreferredAppMode(2); // ForceDark
            FlushMenuThemes();
        }
        catch
        {
            // Windows 10'da bu giriş noktası yoksa varsayılan görünüm kalır.
        }
    }

    private static bool _darkModeTried;

    private static readonly HashSet<Form> Themed = new();

    /// <summary>
    /// Sonradan açılan pencereleri (sunucu düzenle, kısa kod, ped, dil...) de
    /// temaya uyarlar. Bu pencereler kendi temalarını uygulamadığı için koyu
    /// zeminde siyah yazılar kalıyordu.
    /// </summary>
    public static void WatchForms()
    {
        Application.Idle += (_, _) =>
        {
            foreach (Form form in Application.OpenForms)
            {
                if (form is MainForm || form.IsDisposed) continue;
                if (!Themed.Add(form)) continue;

                try { Apply(form); }
                catch { }
            }
        };
    }

    /// <summary>
    /// Metin kutularına sağ tık menüsü: Geri Al (Ctrl+Z), Kes, Kopyala,
    /// Yapıştır, Tümünü Seç. Kısayollar metin kutusunda da çalışır.
    /// </summary>
    /// <summary>Açılır listeyi koyu temaya çevirir (kapalı alan dahil).</summary>
    private static void AttachComboDraw(ComboBox combo)
    {
        if (combo.Tag is "gtacoop-dark-combo") return;
        combo.Tag = "gtacoop-dark-combo";

        // Görsel stil tema'sız bırakılınca Windows kutuyu BackColor ile çiziyor.
        void Untheme()
        {
            try { SetWindowTheme(combo.Handle, string.Empty, string.Empty); } catch { }
        }
        if (combo.IsHandleCreated) Untheme();
        else combo.HandleCreated += (_, _) => Untheme();

        // DropDownList alanı Windows tarafından beyaz çiziliyor; düzenlenebilir
        // moda alıp yazmayı kilitliyoruz, böylece alan BackColor ile çizilir.
        if (combo.DropDownStyle == ComboBoxStyle.DropDownList)
        {
            combo.DropDownStyle = ComboBoxStyle.DropDown;
            combo.KeyPress += (_, e) => e.Handled = true;
        }

        // Düz stil: Windows'un beyaz ok düğmesi yerine koyu çizim.
        combo.FlatStyle = FlatStyle.Flat;
        combo.ForeColor = Text;
        combo.BackColor = Surface;

        // Alan odaklanınca metnin mavi seçili görünmemesi için seçim temizlenir.
        void NoSelection()
        {
            try
            {
                combo.SelectionStart = combo.Text.Length;
                combo.SelectionLength = 0;
            }
            catch { }
        }
        combo.Enter += (_, _) => NoSelection();
        combo.GotFocus += (_, _) => combo.BeginInvoke((Action)NoSelection);
        combo.SelectedIndexChanged += (_, _) => combo.BeginInvoke((Action)NoSelection);
        combo.TextChanged += (_, _) => NoSelection();

        // Windows'un kapalı alanı (metin + ok) kendi renkleriyle çiziliyordu:
        // metin mavi seçili görünüyor, ok beyaz kalıyordu. Tüm kapalı alanı
        // kendi panelimizle kaplayıp metni ve oku biz çiziyoruz.
        // Metin kutusu (edit) alt denetimi odaklanınca kendi beyaz zeminini
        // bizim panelimizin üstüne çiziyordu; düzenlenemez stile geçiyoruz.
        combo.DropDownStyle = ComboBoxStyle.DropDownList;

        var face = new Panel { BackColor = SurfaceAlt, Cursor = Cursors.Hand };
        face.Paint += (_, e) =>
        {
            var g = e.Graphics;
            var textRect = new Rectangle(6, 0, Math.Max(0, face.Width - 26), face.Height);
            TextRenderer.DrawText(g, combo.Text, combo.Font, textRect, Text,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix |
                TextFormatFlags.EndEllipsis);

            using var pen = new Pen(Text, 1.6f);
            var cx = face.Width - 12;
            var cy = face.Height / 2;
            g.DrawLines(pen, new[]
            {
                new Point(cx - 4, cy - 2), new Point(cx, cy + 3), new Point(cx + 4, cy - 2)
            });
        };
        face.MouseDown += (_, _) => { try { combo.DroppedDown = true; } catch { } };
        combo.Controls.Add(face);

        void PlaceFace()
        {
            if (combo.IsDisposed) return;
            face.Bounds = new Rectangle(1, 1, Math.Max(1, combo.ClientSize.Width - 2),
                Math.Max(1, combo.ClientSize.Height - 2));
            face.BringToFront();
            face.Invalidate();
        }
        combo.SizeChanged += (_, _) => PlaceFace();
        combo.HandleCreated += (_, _) => PlaceFace();
        combo.SelectedIndexChanged += (_, _) => face.Invalidate();
        combo.TextChanged += (_, _) => face.Invalidate();
        PlaceFace();

        combo.DrawMode = DrawMode.OwnerDrawFixed;
        combo.ItemHeight = Math.Max(20, combo.Font.Height + 8);
        combo.DrawItem += (_, e) =>
        {
            if (e.Index < 0) return;
            var selected = (e.State & DrawItemState.Selected) != 0;
            using (var back = new SolidBrush(selected ? Selection : SurfaceAlt))
                e.Graphics.FillRectangle(back, e.Bounds);

            var text = Convert.ToString(combo.Items[e.Index]) ?? string.Empty;
            TextRenderer.DrawText(e.Graphics, text, combo.Font, e.Bounds, Text,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        };
    }

    private static void AttachEditMenu(TextBoxBase box)
    {
        if (box.ContextMenuStrip != null) return;

        var menu = new ContextMenuStrip();

        ToolStripMenuItem Item(string text, string shortcut, Action action)
        {
            var item = new ToolStripMenuItem(text, null, (_, _) =>
            {
                try { action(); } catch { }
            });
            item.ShortcutKeyDisplayString = shortcut;
            return item;
        }

        var undo = Item(Loc.T("Geri Al"), "Ctrl+Z", () => { if (box.CanUndo) box.Undo(); });
        undo.Enabled = box.CanUndo;
        box.TextChanged += (_, _) => undo.Enabled = box.CanUndo;

        menu.Items.Add(undo);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(Item(Loc.T("Kes"), "Ctrl+X", () => box.Cut()));
        menu.Items.Add(Item(Loc.T("Kopyala"), "Ctrl+C", () => box.Copy()));
        menu.Items.Add(Item(Loc.T("Yapıştır"), "Ctrl+V", () => box.Paste()));
        menu.Items.Add(Item(Loc.T("Tümünü Seç"), "Ctrl+A", () => box.SelectAll()));

        box.ContextMenuStrip = menu;
    }

    private static void ApplyDarkTitleBar(Form form)
    {
        try
        {
            var handle = form.Handle;
            int on = 1;
            // 20 = DWMWA_USE_IMMERSIVE_DARK_MODE (Win10 2004+), 19 = eski yapı.
            if (DwmSetWindowAttribute(handle, 20, ref on, sizeof(int)) != 0)
                DwmSetWindowAttribute(handle, 19, ref on, sizeof(int));
        }
        catch
        {
            // Eski Windows sürümlerinde başlık çubuğu sistem renginde kalır.
        }
    }

    private static void ApplyToChildren(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            Style(child);
            if (child.HasChildren) ApplyToChildren(child);

            // Kaydırma çubukları ancak pencere tanıtıcısı oluştuktan sonra
            // koyulaştırılabilir; o yüzden oluşunca bir kez daha denenir.
            if (!child.IsHandleCreated)
                child.HandleCreated += (_, _) => DarkenScrollbars(child);
        }
    }

    private static bool IsStatusColor(Color c) =>
        c == Ui.OkColor || c == Ui.ErrColor || c == Ui.WarnColor ||
        c == Accent || c == AccentAlt || c == Color.Red || c == Color.DarkRed;

    private static bool IsDefaultFore(Color c) =>
        c == Control.DefaultForeColor || c == Color.Black || c == Color.White ||
        c == SystemColors.ControlText || c == SystemColors.WindowText;

    private static void Style(Control control)
    {
        switch (control)
        {
            case TabControl tabs:
                tabs.BackColor = Background;
                tabs.ForeColor = Text;
                break;

            case TabPage page:
                // UseVisualStyleBackColor açıkken TabPage kendi rengini yok sayar
                // ve beyaz kalır; bu yüzden kapatılır.
                page.UseVisualStyleBackColor = false;
                page.BackColor = Background;
                page.ForeColor = Text;
                break;

            case GroupBox group:
                group.BackColor = Surface;
                group.ForeColor = Accent;
                // Başlık yazısı docked ilk satırın altında kalmasın.
                if (group.Padding.Top < 22)
                    group.Padding = new Padding(group.Padding.Left, 22,
                        group.Padding.Right, group.Padding.Bottom);
                break;

            case ListView list:
                // Sütunların sağında kalan boş alan ve kaydırma şeridi Windows'ta
                // beyaz kalıyordu.
                list.BackColor = Surface;
                list.ForeColor = Text;
                StyleList(list);

                // Seçilen satır bazen siyah kalıyordu (yeniden çizim eksikliği):
                // çift tampon + seçim değişiminde yeniden çizim.
                try
                {
                    typeof(Control).GetProperty("DoubleBuffered",
                            System.Reflection.BindingFlags.Instance |
                            System.Reflection.BindingFlags.NonPublic)
                        ?.SetValue(list, true);
                }
                catch { }
                list.SelectedIndexChanged += (_, _) => list.Invalidate();
                void DarkScroll()
                {
                    try { SetWindowTheme(list.Handle, "DarkMode_Explorer", null); } catch { }
                    try { SetWindowTheme(list.Handle, "DarkMode_ItemsView", null); } catch { }
                }
                if (list.IsHandleCreated) DarkScroll();
                else list.HandleCreated += (_, _) => DarkScroll();
                list.VisibleChanged += (_, _) => { if (list.IsHandleCreated) DarkScroll(); };

                // Kaydırma çubuğu yine beyaz kalırsa üzerine koyu bir şerit
                // çizilir; tekerlek/klavye kaydırması listeye iletilir.
                // Windows'un beyaz kaydırma çubuğu yerine kendi koyu
                // çubuğumuzu çiziyoruz; native çubuk gizlenir.
                var vbar = new DarkScrollBar(list) { Visible = false };
                list.Parent?.Controls.Add(vbar);
                vbar.BringToFront();

                void PlaceBar()
                {
                    if (list.IsDisposed || !list.IsHandleCreated) return;

                    const int GwlStyle = -16;
                    const int WsVscroll = 0x00200000;
                    var style = GetWindowLongPtr(list.Handle, GwlStyle).ToInt64();
                    if ((style & WsVscroll) != 0)
                    {
                        // Native çubuğu gizle: beyaz şerit oluşmasın.
                        SetWindowLongPtr(list.Handle, GwlStyle, (IntPtr)(style & ~WsVscroll));
                        list.Refresh();
                    }

                    var parent = list.Parent;
                    if (parent == null) { vbar.Visible = false; return; }
                    const int barWidth = 10;
                    vbar.SetBounds(list.Left + list.Width - barWidth, list.Top, barWidth, list.Height);
                    vbar.Visible = true;
                    vbar.BringToFront();
                }
                list.SizeChanged += (_, _) => PlaceBar();
                list.HandleCreated += (_, _) => PlaceBar();
                list.ItemChecked += (_, _) => PlaceBar();
                break;

            case TextBoxBase text:
                text.BackColor = SurfaceAlt;
                text.ForeColor = Text;
                text.BorderStyle = BorderStyle.FixedSingle;
                AttachEditMenu(text);
                break;

            case NumericUpDown num:
                // Sayı kutusunun okları Windows tarafından beyaz çiziliyordu;
                // koyu explorer temasıyla çizilir.
                num.BackColor = SurfaceAlt;
                num.ForeColor = Text;
                num.BorderStyle = BorderStyle.FixedSingle;
                if (num.IsHandleCreated)
                {
                    try { SetWindowTheme(num.Handle, "DarkMode_Explorer", null); } catch { }
                }
                else
                {
                    num.HandleCreated += (_, _) =>
                    {
                        try { SetWindowTheme(num.Handle, "DarkMode_Explorer", null); } catch { }
                    };
                }
                break;

            case ComboBox combo:
                combo.BackColor = SurfaceAlt;
                combo.ForeColor = Text;
                combo.FlatStyle = FlatStyle.Flat;
                // DropDownList + Flat iken Windows kutuyu beyaz çiziyordu;
                // liste ve kapalı alan kendimiz boyuyoruz.
                AttachComboDraw(combo);
                break;

            case CheckBox check:
                check.ForeColor = Text;
                check.BackColor = Color.Transparent;
                break;

            case RadioButton radio:
                radio.ForeColor = Text;
                radio.BackColor = Color.Transparent;
                break;

            case Button button:
                button.FlatStyle = FlatStyle.Flat;
                button.UseVisualStyleBackColor = false;
                button.BackColor = SurfaceAlt;
                // Windows varsayılanı koyu zeminde siyah yazı bırakıyor;
                // düğme yazısı her durumda açık renk olmalı.
                button.ForeColor = Text;
                button.FlatAppearance.BorderColor = Border;
                button.FlatAppearance.BorderSize = 1;
                button.FlatAppearance.MouseOverBackColor = Color.FromArgb(28, 28, 28);
                button.FlatAppearance.MouseDownBackColor = Color.FromArgb(15, 44, 26);
                break;

            case Label label:
                if (label is LinkLabel link)
                {
                    link.LinkColor = AccentAlt;
                    link.ActiveLinkColor = Accent;
                }
                // Anlam taşıyan (yeşil/kırmızı) yazılar korunur.
                if (IsDefaultFore(label.ForeColor)) label.ForeColor = Text;
                else if (label.ForeColor == Color.DimGray) label.ForeColor = Muted;
                // Koyu zeminde okunmayan koyu yazılar açık renge çevrilir.
                else if (label.ForeColor.GetBrightness() < 0.45f && !IsStatusColor(label.ForeColor))
                    label.ForeColor = Text;
                break;

            case TableLayoutPanel table:
                table.BackColor = Background;
                break;

            case FlowLayoutPanel flow:
                flow.BackColor = Background;
                break;

            case Panel panel:
                if (panel.BackColor == SystemColors.Control || panel.BackColor == Color.Transparent)
                    panel.BackColor = Background;
                break;

            case StatusStrip strip:
                strip.BackColor = Surface;
                strip.ForeColor = Muted;
                break;

            case ProgressBar bar:
                bar.BackColor = SurfaceAlt;
                bar.ForeColor = Accent;
                break;

            case SplitContainer split:
                split.BackColor = Border;
                break;
        }
    }

    // Farenin üzerinde durduğu satır (indeks). ListView başına tutulur.
    private static readonly Dictionary<ListView, int> Hover = new();

    // Çizim kancaları bir kez bağlansın (çift bağlama satırları gizliyordu).
    private static readonly HashSet<ListView> StyledLists = new();

    /// <summary>Listeyi koyu çizer: başlık, satırlar ve varsa onay kutuları.</summary>
    private static void StyleList(ListView list)
    {
        list.BackColor = Surface;
        list.ForeColor = Text;
        list.BorderStyle = BorderStyle.FixedSingle;

        if (!StyledLists.Add(list)) return;

        list.OwnerDraw = true;
        list.DrawColumnHeader += DrawHeader;
        list.DrawItem += DrawItem;
        list.DrawSubItem += DrawSubItem;

        // Windows, farenin üzerinde durduğu satırı kendi (siyah) rengiyle
        // boyuyordu. Satır vurgusunu kendimiz çiziyoruz.
        list.MouseMove += (s, e) =>
        {
            var lv = (ListView)s!;
            var index = lv.GetItemAt(e.X, e.Y)?.Index ?? -1;
            if (Hover.TryGetValue(lv, out var old) && old == index) return;
            if (index >= 0) Hover[lv] = index;
            else Hover.Remove(lv);
            lv.Invalidate();
        };
        list.MouseLeave += (s, _) =>
        {
            var lv = (ListView)s!;
            if (Hover.Remove(lv)) lv.Invalidate();
        };

        // Son sütunu her boyut değişiminde kalan genişliğe yay; aksi hâlde
        // sütunların sağında beyaz bir şerit (başlık + satırlar) kalıyor.
        list.SizeChanged += (_, _) => FillLastColumn(list);
        list.Resize += (_, _) => FillLastColumn(list);
        FillLastColumn(list);
    }

    private static void FillLastColumn(ListView list)
    {
        if (list.Columns.Count == 0) return;
        var last = list.Columns[list.Columns.Count - 1];
        if (last.Width != -2) last.Width = -2;
    }

    /// <summary>Satır arka planı: seçili → yeşil, fare üzerinde → açık gri, değilse zebra.</summary>
    private static Color RowBack(ListView list, int index, bool selected)
    {
        if (selected) return Selection;
        if (Hover.TryGetValue(list, out var hot) && hot == index) return SurfaceAlt;
        return index % 2 == 0 ? Surface : Background;
    }

    /// <summary>Satır/sütun çizgilerini belirgin ama koyu tonda çizer.</summary>
    private static void DrawGrid(Graphics g, Rectangle bounds, bool right)
    {
        using var pen = new Pen(Border);
        g.DrawLine(pen, bounds.Left, bounds.Bottom - 1, bounds.Right, bounds.Bottom - 1);
        if (right) g.DrawLine(pen, bounds.Right - 1, bounds.Top, bounds.Right - 1, bounds.Bottom - 1);
    }

    private static void DrawHeader(object? sender, DrawListViewColumnHeaderEventArgs e)
    {
        using (var back = new SolidBrush(SurfaceAlt))
            e.Graphics.FillRectangle(back, e.Bounds);

        using (var pen = new Pen(Border))
        {
            e.Graphics.DrawLine(pen, e.Bounds.Left, e.Bounds.Bottom - 1,
                e.Bounds.Right, e.Bounds.Bottom - 1);
            e.Graphics.DrawLine(pen, e.Bounds.Right - 1, e.Bounds.Top + 3,
                e.Bounds.Right - 1, e.Bounds.Bottom - 3);
        }

        // Son sütunun sağında kalan şerit beyaz kalmasın.
        if (sender is ListView headerList && e.Bounds.Right < headerList.ClientSize.Width)
        {
            using var rest = new SolidBrush(SurfaceAlt);
            e.Graphics.FillRectangle(rest, e.Bounds.Right, e.Bounds.Top,
                headerList.ClientSize.Width - e.Bounds.Right, e.Bounds.Height);
        }

        var text = e.Header?.Text ?? "";
        TextRenderer.DrawText(e.Graphics, text, e.Font ?? Control.DefaultFont,
            new Rectangle(e.Bounds.X + 6, e.Bounds.Y, e.Bounds.Width - 10, e.Bounds.Height),
            Muted, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis |
                    TextFormatFlags.NoPrefix);
    }

    private static void DrawItem(object? sender, DrawListViewItemEventArgs e)
    {
        var list = (ListView)sender!;
        using var back = new SolidBrush(RowBack(list, e.ItemIndex, e.Item.Selected));
        e.Graphics.FillRectangle(back, e.Bounds);

        // Satır, sütunların toplamından kısa olabilir; sağdaki boşluğu da boya.
        if (e.Bounds.Right < list.ClientSize.Width)
            e.Graphics.FillRectangle(back, e.Bounds.Right, e.Bounds.Top,
                list.ClientSize.Width - e.Bounds.Right, e.Bounds.Height);

        // Onay kutusu (yalnızca CheckBoxes açıksa) elle çizilir.
        if (!list.CheckBoxes) return;

        var box = new Rectangle(e.Bounds.Left + 3, e.Bounds.Top + (e.Bounds.Height - 13) / 2, 13, 13);
        using (var fill = new SolidBrush(SurfaceAlt))
            e.Graphics.FillRectangle(fill, box);
        using (var pen = new Pen(e.Item.Checked ? Accent : Border))
            e.Graphics.DrawRectangle(pen, box);

        if (!e.Item.Checked) return;

        using var tick = new Pen(Accent, 2f);
        e.Graphics.DrawLines(tick, new[]
        {
            new Point(box.Left + 3, box.Top + 6),
            new Point(box.Left + 6, box.Top + 9),
            new Point(box.Left + 10, box.Top + 3)
        });
    }

    private static void DrawSubItem(object? sender, DrawListViewSubItemEventArgs e)
    {
        var list = (ListView)sender!;
        var bump = list.CheckBoxes && e.ColumnIndex == 0 ? 18 : 0;

        var background = RowBack(list, e.ItemIndex, e.Item.Selected);

        using (var back = new SolidBrush(background))
            e.Graphics.FillRectangle(back, e.Bounds);

        if (list.GridLines) DrawGrid(e.Graphics, e.Bounds, true);

        var fore = e.Item.ForeColor == Color.Empty || IsDefaultFore(e.Item.ForeColor)
            ? Text
            : e.Item.ForeColor;

        TextRenderer.DrawText(e.Graphics, e.SubItem?.Text ?? "", e.Item.Font ?? list.Font,
            new Rectangle(e.Bounds.X + 4 + bump, e.Bounds.Y, e.Bounds.Width - 8 - bump, e.Bounds.Height),
            e.Item.Selected ? Color.White : fore,
            TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis |
            TextFormatFlags.NoPrefix);
    }

    /// <summary>Sekme şeridini koyu, düz bir görünüme çevirir.</summary>
    public static void DrawTab(object? sender, DrawItemEventArgs e)
    {
        if (sender is not TabControl tabs) return;
        var page = tabs.TabPages[e.Index];
        var selected = e.Index == tabs.SelectedIndex;
        var bounds = e.Bounds;

        using (var back = new SolidBrush(selected ? Background : Surface))
            e.Graphics.FillRectangle(back, bounds);

        if (selected)
        {
            using var accent = new SolidBrush(Accent);
            e.Graphics.FillRectangle(accent, bounds.Left, bounds.Bottom - 3, bounds.Width, 3);
        }

        var flags = TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter |
                    TextFormatFlags.EndEllipsis;
        TextRenderer.DrawText(e.Graphics, page.Text, tabs.Font, bounds,
            selected ? Text : Muted, flags);

        using var pen = new Pen(Border);
        e.Graphics.DrawLine(pen, bounds.Right - 1, bounds.Top + 6, bounds.Right - 1, bounds.Bottom - 6);
    }
}
