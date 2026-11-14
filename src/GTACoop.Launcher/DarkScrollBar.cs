using System.Windows.Forms;

namespace GTACoop.Remake;

/// <summary>
/// ListView için kendi çizdiğimiz ince koyu kaydırma çubuğu. Windows'un beyaz
/// native çubuğu gizlenir; bu denetim hem görünümü hem sürüklemeyi üstlenir.
/// </summary>
internal sealed class DarkScrollBar : Control
{
    private readonly ListView _list;
    private bool _dragging;
    private bool _hover;

    public DarkScrollBar(ListView list)
    {
        _list = list;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Width = 10;
        BackColor = Theme.Surface;
        Cursor = Cursors.Hand;
    }

    /// <summary>Görünür satır sayısı.</summary>
    private int VisibleRows
    {
        get
        {
            if (_list.Items.Count == 0) return 1;
            var rowHeight = Math.Max(1, _list.GetItemRect(0).Height);
            return Math.Max(1, _list.ClientSize.Height / rowHeight);
        }
    }

    private int MaxTop => Math.Max(0, _list.Items.Count - VisibleRows);

    private int TopIndex
    {
        get
        {
            try { return _list.TopItem?.Index ?? 0; } catch { return 0; }
        }
        set
        {
            if (_list.Items.Count == 0) return;
            var clamped = Math.Max(0, Math.Min(MaxTop, value));
            try { _list.TopItem = _list.Items[clamped]; } catch { }
        }
    }

    private Rectangle Thumb
    {
        get
        {
            var count = Math.Max(1, _list.Items.Count);
            var visible = Math.Min(count, VisibleRows);
            var ratio = (double)visible / count;
            var height = Math.Max(24, (int)(Height * ratio));
            var travel = Math.Max(1, Height - height);
            var max = Math.Max(1, MaxTop);
            var top = (int)(travel * ((double)Math.Min(TopIndex, max) / max));
            return new Rectangle(2, top, Math.Max(1, Width - 4), height);
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        using (var track = new SolidBrush(Theme.Surface))
            g.FillRectangle(track, ClientRectangle);

        var thumb = Thumb;
        var color = _hover || _dragging ? Theme.Selection : Theme.Border;
        using (var brush = new SolidBrush(color))
        using (var path = new System.Drawing.Drawing2D.GraphicsPath())
        {
            var r = Math.Min(thumb.Width, 8) / 2;
            path.AddArc(thumb.Left, thumb.Top, r * 2, r * 2, 180, 90);
            path.AddArc(thumb.Right - r * 2, thumb.Top, r * 2, r * 2, 270, 90);
            path.AddArc(thumb.Right - r * 2, thumb.Bottom - r * 2, r * 2, r * 2, 0, 90);
            path.AddArc(thumb.Left, thumb.Bottom - r * 2, r * 2, r * 2, 90, 90);
            path.CloseFigure();
            g.FillPath(brush, path);
        }
    }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        var thumb = Thumb;
        if (!thumb.Contains(e.Location))
        {
            // Çubuğun boşluğuna tıklama: sayfa kaydır.
            TopIndex = TopIndex + (e.Y < thumb.Top ? -VisibleRows : VisibleRows);
            Invalidate();
            return;
        }
        _dragging = true;
        Invalidate();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!_dragging) return;

        var thumb = Thumb;
        var travel = Math.Max(1, Height - thumb.Height);
        var ratio = Math.Max(0.0, Math.Min(1.0, (double)(e.Y - thumb.Height / 2) / travel));
        TopIndex = (int)Math.Round(ratio * MaxTop);
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        _dragging = false;
        Invalidate();
        base.OnMouseUp(e);
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        TopIndex = TopIndex - Math.Sign(e.Delta) * 3;
        Invalidate();
    }
}
