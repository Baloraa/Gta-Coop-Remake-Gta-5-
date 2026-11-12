using System.Windows.Forms;
using GTACoop.Sync;

namespace GTACoop.Remake;

/// <summary>Küçük arayüz yardımcıları.</summary>
internal static class Ui
{
    public static readonly Color OkColor = Color.FromArgb(22, 122, 54);
    public static readonly Color ErrColor = Color.FromArgb(176, 40, 40);
    public static readonly Color WarnColor = Color.FromArgb(170, 110, 0);

    public static Button Btn(string text, EventHandler onClick, int width = 0)
    {
        var b = new DarkButton
        {
            Text = text,
            AutoSize = width == 0,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(8, 3, 8, 3),
            Margin = new Padding(4, 4, 4, 4)
        };
        b.Click += onClick;
        if (width > 0) { b.AutoSize = false; b.Width = width; b.Height = 30; }
        return b;
    }

    public static Label Lbl(string text, bool bold = false)
    {
        return new Label
        {
            Text = text,
            AutoSize = true,
            Font = bold ? new Font("Segoe UI", 9.5f, FontStyle.Bold) : new Font("Segoe UI", 9f),
            Margin = new Padding(4, 6, 4, 2)
        };
    }

    public static Label StatusLbl(string text = "")
    {
        return new Label
        {
            Text = text,
            AutoSize = true,
            ForeColor = Color.DimGray,
            Margin = new Padding(4, 6, 4, 2)
        };
    }

    public static GroupBox GroupBox(string title, Control content, int height)
    {
        return new DarkGroup
        {
            Text = title,
            Dock = DockStyle.Top,
            Height = height,
            Padding = new Padding(8),
        }.With(c => c.Controls.Add(content));
    }

    /// <summary>
    /// Grubun yüksekliğini içeriğine göre ayarlar. Sabit yükseklikler DPI ve
    /// pencere boyutuna göre tutmadığı için satırlar kesilebiliyordu; burada
    /// üst/alt yaslı satırların gerçek yüksekliği toplanır ve grup ona göre büyür.
    /// </summary>
    public static void FitToContent(GroupBox box)
    {
        var fitting = false;

        void Apply()
        {
            if (fitting || box.IsDisposed) return;
            fitting = true;
            try
            {
                var need = box.Padding.Vertical + 4;
                foreach (Control c in box.Controls)
                {
                    if (!c.Visible) continue;
                    need += c.Dock switch
                    {
                        DockStyle.Top or DockStyle.Bottom => c.Height + c.Margin.Vertical,
                        DockStyle.Fill => c.PreferredSize.Height + c.Margin.Vertical,
                        _ => 0
                    };
                }

                if (need > 0 && box.Height != need) box.Height = need;
            }
            finally
            {
                fitting = false;
            }
        }

        foreach (Control c in box.Controls)
        {
            c.SizeChanged += (_, _) => Apply();
            c.VisibleChanged += (_, _) => Apply();
        }

        box.Layout += (_, _) => Apply();
        Apply();
    }

    public static FlowLayoutPanel Flow(DockStyle dock = DockStyle.Top)
    {
        return new FlowLayoutPanel
        {
            Dock = dock,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            Padding = new Padding(2)
        };
    }

    public static void StyleList(ListView lv, params (string Name, int Width)[] columns)
    {
        lv.View = View.Details;
        lv.FullRowSelect = true;
        lv.GridLines = true;
        lv.HideSelection = false;
        lv.Columns.Clear();
        foreach (var (name, width) in columns)
            lv.Columns.Add(name, width);

        // Son sütun kalan genişliği doldursun: aksi halde sütunların sağında
        // Windows'un beyaz şeridi kalıyor.
        if (lv.Columns.Count > 0) lv.Columns[^1].Width = -2;
    }

    public static void Info(Form owner, string text, string title = "GTACoop Remake") =>
        MessageBox.Show(owner, SyncText.Translate(text), title, MessageBoxButtons.OK, MessageBoxIcon.Information);

    public static void Warn(Form owner, string text, string title = "GTACoop Remake") =>
        MessageBox.Show(owner, SyncText.Translate(text), title, MessageBoxButtons.OK, MessageBoxIcon.Warning);

    public static void Error(Form owner, string text, string title = "GTACoop Remake") =>
        MessageBox.Show(owner, SyncText.Translate(text), title, MessageBoxButtons.OK, MessageBoxIcon.Error);

    public static bool Confirm(Form owner, string text, string title = "GTACoop Remake") =>
        MessageBox.Show(owner, SyncText.Translate(text), title, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
}

/// <summary>
/// Koyu temalı, kendi çizilen düğme. Windows pasif düğmelerin yazısını koyu
/// griye çevirip okunamaz hale getiriyordu; burada yazı her durumda açık renk.
/// </summary>
internal sealed class DarkButton : Button
{
    public DarkButton()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        UseVisualStyleBackColor = false;
        BackColor = Theme.SurfaceAlt;
        ForeColor = Theme.Text;
    }

    private bool _hover;

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        var back = !Enabled ? Theme.Surface : _hover ? Color.FromArgb(32, 32, 38) : Theme.SurfaceAlt;
        using (var brush = new SolidBrush(back))
            g.FillRectangle(brush, ClientRectangle);

        using (var pen = new Pen(Enabled ? Theme.Border : Color.FromArgb(32, 32, 38)))
            g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);

        var color = Enabled ? Theme.Text : Color.FromArgb(170, 172, 178);
        TextRenderer.DrawText(g, Text ?? string.Empty, Font, ClientRectangle, color,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
            TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
    }
}

internal static class ControlExtensions
{
    public static T With<T>(this T control, Action<T> action)
    {
        action(control);
        return control;
    }
}
