using System.Windows.Forms;

namespace GTACoop.Remake;

/// <summary>İlk açılışta dil seçimi penceresi (metinler iki dillidir).</summary>
public sealed class LanguageDialog : Form
{
    /// <summary>Seçilen dil: "tr" veya "en".</summary>
    public string Selected { get; private set; } = "tr";

    public LanguageDialog()
    {
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96f, 96f);

        Text = "Dil / Language";
        ClientSize = new Size(380, 190);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9f);

        var title = new Label
        {
            Text = "Dil seçin / Choose your language",
            Font = new Font("Segoe UI", 11f, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(24, 20)
        };

        var hint = new Label
        {
            Text = "Bu seçimi daha sonra üst çubuktan değiştirebilirsiniz.\r\nYou can change this later from the top bar.",
            ForeColor = Color.DimGray,
            AutoSize = true,
            Location = new Point(24, 52)
        };

        var btnTr = new Button
        {
            Text = "Türkçe",
            Width = 140,
            Height = 42,
            Location = new Point(24, 100),
            Font = new Font("Segoe UI", 10f)
        };
        var btnEn = new Button
        {
            Text = "English",
            Width = 140,
            Height = 42,
            Location = new Point(184, 100),
            Font = new Font("Segoe UI", 10f)
        };

        btnTr.Click += (_, _) => { Selected = "tr"; DialogResult = DialogResult.OK; };
        btnEn.Click += (_, _) => { Selected = "en"; DialogResult = DialogResult.OK; };

        Controls.Add(title);
        Controls.Add(hint);
        Controls.Add(btnTr);
        Controls.Add(btnEn);
        AcceptButton = btnTr;
    }
}
