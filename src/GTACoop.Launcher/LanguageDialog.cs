using System.Windows.Forms;

namespace GTACoop.Remake;

/// <summary>İlk açılışta dil seçimi penceresi (metinler üç dillidir).</summary>
public sealed class LanguageDialog : Form
{
    /// <summary>Seçilen dil: "tr", "en" veya "ru".</summary>
    public string Selected { get; private set; } = "tr";

    public LanguageDialog()
    {
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96f, 96f);

        Text = "Dil / Language / Язык";
        ClientSize = new Size(560, 200);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9f);

        var title = new Label
        {
            Text = "Dil seçin / Choose your language / Выберите язык",
            Font = new Font("Segoe UI", 11f, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(24, 20)
        };

        var hint = new Label
        {
            Text = "Bu seçimi daha sonra üst çubuktan değiştirebilirsiniz.\r\n" +
                   "You can change this later from the top bar.\r\n" +
                   "Вы можете изменить это позже на верхней панели.",
            ForeColor = Color.DimGray,
            AutoSize = true,
            Location = new Point(24, 50)
        };

        var btnTr = new Button
        {
            Text = "Türkçe",
            Width = 160,
            Height = 42,
            Location = new Point(24, 130),
            Font = new Font("Segoe UI", 10f)
        };
        var btnEn = new Button
        {
            Text = "English",
            Width = 160,
            Height = 42,
            Location = new Point(200, 130),
            Font = new Font("Segoe UI", 10f)
        };
        var btnRu = new Button
        {
            Text = "Русский",
            Width = 160,
            Height = 42,
            Location = new Point(376, 130),
            Font = new Font("Segoe UI", 10f)
        };

        btnTr.Click += (_, _) => { Selected = "tr"; DialogResult = DialogResult.OK; };
        btnEn.Click += (_, _) => { Selected = "en"; DialogResult = DialogResult.OK; };
        btnRu.Click += (_, _) => { Selected = "ru"; DialogResult = DialogResult.OK; };

        Controls.Add(title);
        Controls.Add(hint);
        Controls.Add(btnTr);
        Controls.Add(btnEn);
        Controls.Add(btnRu);
        AcceptButton = btnTr;
    }
}
