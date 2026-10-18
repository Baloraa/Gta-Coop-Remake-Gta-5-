using System.Windows.Forms;

namespace GTACoop.Remake;

/// <summary>Küçük arayüz yardımcıları.</summary>
internal static class Ui
{
    public static readonly Color OkColor = Color.FromArgb(22, 122, 54);
    public static readonly Color ErrColor = Color.FromArgb(176, 40, 40);
    public static readonly Color WarnColor = Color.FromArgb(170, 110, 0);

    public static Button Btn(string text, EventHandler onClick, int width = 0)
    {
        var b = new Button
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
        return new GroupBox
        {
            Text = title,
            Dock = DockStyle.Top,
            Height = height,
            Padding = new Padding(8),
        }.With(c => c.Controls.Add(content));
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
    }

    public static void Info(Form owner, string text, string title = "GTACoop Remake") =>
        MessageBox.Show(owner, text, title, MessageBoxButtons.OK, MessageBoxIcon.Information);

    public static void Warn(Form owner, string text, string title = "GTACoop Remake") =>
        MessageBox.Show(owner, text, title, MessageBoxButtons.OK, MessageBoxIcon.Warning);

    public static void Error(Form owner, string text, string title = "GTACoop Remake") =>
        MessageBox.Show(owner, text, title, MessageBoxButtons.OK, MessageBoxIcon.Error);

    public static bool Confirm(Form owner, string text, string title = "GTACoop Remake") =>
        MessageBox.Show(owner, text, title, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
}

internal static class ControlExtensions
{
    public static T With<T>(this T control, Action<T> action)
    {
        action(control);
        return control;
    }
}
