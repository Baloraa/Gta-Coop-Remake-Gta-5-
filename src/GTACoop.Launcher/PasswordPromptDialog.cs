using System.Windows.Forms;

namespace GTACoop.Remake;

/// <summary>Şifreli sunucular için küçük şifre penceresi.</summary>
public sealed class PasswordPromptDialog : Form
{
    private readonly TextBox _password = new();

    public string Password => _password.Text;

    public PasswordPromptDialog(string serverName)
    {
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96f, 96f);
        Text = T("Sunucu Şifresi");
        Width = 440;
        Height = 175;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Segoe UI", 9f);
        ShowInTaskbar = false;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 3,
            Padding = new Padding(14)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        layout.Controls.Add(new Label
        {
            Text = T("Sunucu:"),
            AutoSize = true,
            Padding = new Padding(0, 8, 0, 0)
        });
        layout.Controls.Add(new Label
        {
            Text = serverName,
            AutoSize = true,
            Padding = new Padding(0, 8, 0, 0),
            AutoEllipsis = true,
            MaximumSize = new Size(270, 0)
        });

        layout.Controls.Add(new Label
        {
            Text = T("Şifre:"),
            AutoSize = true,
            Padding = new Padding(0, 8, 0, 0)
        });
        _password.Width = 260;
        _password.UseSystemPasswordChar = true;
        _password.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        layout.Controls.Add(_password);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            Height = 46,
            Padding = new Padding(8)
        };
        var ok = new Button { Text = T("Bağlan"), DialogResult = DialogResult.OK, Width = 100, Height = 30 };
        var cancel = new Button { Text = T("İptal"), DialogResult = DialogResult.Cancel, Width = 90, Height = 30 };
        buttons.Controls.Add(ok);
        buttons.Controls.Add(cancel);

        Controls.Add(layout);
        Controls.Add(buttons);
        AcceptButton = ok;
        CancelButton = cancel;
        Shown += (_, _) => _password.Focus();
    }
}
