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
                StyleList(list);
                break;

            case TextBoxBase text:
                text.BackColor = SurfaceAlt;
                text.ForeColor = Text;
                text.BorderStyle = BorderStyle.FixedSingle;
                AttachEditMenu(text);
                break;

            case NumericUpDown num:
                num.BackColor = SurfaceAlt;
                num.ForeColor = Text;
                num.BorderStyle = BorderStyle.FixedSingle;
                break;

            case ComboBox combo:
                combo.BackColor = SurfaceAlt;
                combo.ForeColor = Text;
                combo.FlatStyle = FlatStyle.Flat;
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
                button.BackColor = SurfaceAlt;
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

    /// <summary>Listeyi koyu çizer: başlık, satırlar ve varsa onay kutuları.</summary>
    private static void StyleList(ListView list)
    {
        list.BackColor = Surface;
        list.ForeColor = Text;
        list.BorderStyle = BorderStyle.FixedSingle;

        if (list.OwnerDraw) return;

        list.OwnerDraw = true;
        list.DrawColumnHeader += DrawHeader;
        list.DrawItem += DrawItem;
        list.DrawSubItem += DrawSubItem;
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
        using var back = new SolidBrush(e.Item.Selected ? Selection : Surface);
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

        var background = e.Item.Selected
            ? Selection
            : e.ItemIndex % 2 == 0 ? Surface : Background;

        using (var back = new SolidBrush(background))
            e.Graphics.FillRectangle(back, e.Bounds);

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
