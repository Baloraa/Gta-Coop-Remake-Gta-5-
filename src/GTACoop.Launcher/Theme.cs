using System.Drawing;
using System.Windows.Forms;

namespace GTACoop.Remake;

/// <summary>
/// Karanlık arayüz teması. Yerleşimi ve davranışı değiştirmez; yalnızca
/// renkleri ve kenarlıkları uygular. Anlam taşıyan renkler (yeşil/kırmızı
/// durum yazıları) korunur.
/// </summary>
internal static class Theme
{
    public static readonly Color Background = Color.FromArgb(23, 26, 30);
    public static readonly Color Surface = Color.FromArgb(31, 35, 40);
    public static readonly Color SurfaceAlt = Color.FromArgb(41, 46, 53);
    public static readonly Color Border = Color.FromArgb(60, 67, 76);
    public static readonly Color Text = Color.FromArgb(229, 233, 238);
    public static readonly Color Muted = Color.FromArgb(150, 158, 167);
    public static readonly Color Accent = Color.FromArgb(224, 164, 69);
    public static readonly Color AccentAlt = Color.FromArgb(74, 168, 156);

    public static bool Enabled { get; private set; }

    /// <summary>Formu ve içindeki tüm denetimleri temaya uyarlar.</summary>
    public static void Apply(Form form)
    {
        Enabled = true;
        form.BackColor = Background;
        form.ForeColor = Text;
        ApplyToChildren(form);

        if (form is MainForm main)
            main.ApplyTabStyle();
    }

    /// <summary>Sonradan oluşturulan sekmeler için.</summary>
    public static void Apply(Control control)
    {
        if (!Enabled) return;
        control.BackColor = Background;
        control.ForeColor = Text;
        ApplyToChildren(control);
    }

    private static void ApplyToChildren(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            Style(child);
            if (child.HasChildren) ApplyToChildren(child);
        }
    }

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
                page.BackColor = Background;
                page.ForeColor = Text;
                break;

            case GroupBox group:
                group.BackColor = Surface;
                group.ForeColor = Accent;
                break;

            case ListView list:
                list.BackColor = Surface;
                list.ForeColor = Text;
                list.BorderStyle = BorderStyle.FixedSingle;
                break;

            case TextBoxBase text:
                text.BackColor = SurfaceAlt;
                text.ForeColor = Text;
                text.BorderStyle = BorderStyle.FixedSingle;
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
                button.FlatAppearance.MouseOverBackColor = Color.FromArgb(52, 58, 66);
                button.FlatAppearance.MouseDownBackColor = Color.FromArgb(64, 71, 80);
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
