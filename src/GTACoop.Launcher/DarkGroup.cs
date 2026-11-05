using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using System.Windows.Forms.VisualStyles;

namespace GTACoop.Remake;

/// <summary>
/// Windows'un kendi (açık renkli) GroupBox çerçevesi yerine tamamen koyu,
/// yuvarlatılmış çerçeveli ve yeşil başlıklı grup kutusu çizer.
/// </summary>
internal sealed class DarkGroup : GroupBox
{
    public DarkGroup()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        BackColor = Theme.Surface;
        ForeColor = Theme.Accent;
        Padding = new Padding(10, 26, 10, 8);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        using (var back = new SolidBrush(Theme.Surface))
            g.FillRectangle(back, ClientRectangle);

        var text = Text ?? string.Empty;
        var size = TextRenderer.MeasureText(g, text, Font);
        const int gapLeft = 9;
        var gapWidth = size.Width + 10;

        var frame = new Rectangle(0, 9, Width - 1, Height - 10);
        if (frame.Width > 10 && frame.Height > 10)
        {
            using var path = Rounded(frame, 6);
            using (var pen = new Pen(Theme.Border))
                g.DrawPath(pen, path);

            // Başlık, çerçevenin üst çizgisinin üstüne oturur; çizgi
            // başlığın altında kalmasın diye o aralık yeniden boyanır.
            using (var fill = new SolidBrush(Theme.Surface))
                g.FillRectangle(fill, gapLeft, 8, gapWidth, 3);
        }

        TextRenderer.DrawText(g, text, Font,
            new Rectangle(gapLeft + 2, 0, Math.Max(20, gapWidth), 18), Theme.Accent,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
    }

    private static GraphicsPath Rounded(Rectangle r, int radius)
    {
        var path = new GraphicsPath();
        var d = radius * 2;
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}
