using System.Drawing;
using System.Windows.Forms;

namespace GTACoop.Remake;

/// <summary>
/// Soldaki gezinme çubuğunun tek öğesi. Düz siyah zemin üzerine, etkinken
/// solda yeşil bir şerit ve yeşil yazı çizer; üzerine gelince hafif aydınlanır.
/// </summary>
internal sealed class NavButton : Button
{
    private bool _active;
    private bool _hover;

    public NavButton(string text)
    {
        Text = text;
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        UseVisualStyleBackColor = false;
        TextAlign = ContentAlignment.MiddleLeft;
        AutoSize = false;
        Height = 42;
        Width = 196;
        Margin = new Padding(0, 2, 0, 2);
        Padding = new Padding(18, 0, 0, 0);
        Cursor = Cursors.Hand;
        TabStop = true;

        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }

    public bool Active
    {
        get => _active;
        set
        {
            if (_active == value) return;
            _active = value;
            Invalidate();
        }
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        _hover = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hover = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var background = _active
            ? Theme.SurfaceAlt
            : _hover ? Color.FromArgb(23, 23, 27) : Theme.Surface;

        using (var brush = new SolidBrush(background))
            e.Graphics.FillRectangle(brush, ClientRectangle);

        if (_active)
        {
            using var accent = new SolidBrush(Theme.Accent);
            e.Graphics.FillRectangle(accent, 0, 9, 3, Height - 18);
        }

        var bounds = new Rectangle(18, 0, Width - 24, Height);
        TextRenderer.DrawText(e.Graphics, Text, Font, bounds,
            _active ? Theme.Accent : Theme.Text,
            TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis |
            TextFormatFlags.NoPrefix);
    }
}
