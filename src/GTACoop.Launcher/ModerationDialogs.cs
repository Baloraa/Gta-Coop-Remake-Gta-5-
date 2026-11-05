using System.Windows.Forms;

namespace GTACoop.Remake;

/// <summary>Yasak listesi ve beyaz liste yönetimi.</summary>
internal sealed class ModerationDialog : Form
{
    private readonly ListBox _banned = new();
    private readonly ListBox _whitelist = new();
    private readonly CheckBox _whitelistOnly = new();

    public ModerationSettings Result { get; }

    public ModerationDialog(ModerationSettings current)
    {
        Result = current.Clone();

        Text = Loc.T("Yasaklar ve Beyaz Liste");
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(760, 470);

        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(10)
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        table.Controls.Add(BuildGroup(
            Loc.T("Yasaklı oyuncular (ad)"),
            _banned,
            Result.Banned), 0, 0);

        var whitelistGroup = BuildGroup(
            Loc.T("Beyaz liste (ad)"),
            _whitelist,
            Result.Whitelist);

        _whitelistOnly.Text = Loc.T("Sunucuya yalnızca beyaz listedekiler girebilsin");
        _whitelistOnly.AutoSize = true;
        _whitelistOnly.Checked = Result.WhitelistEnabled;
        _whitelistOnly.Margin = new Padding(6, 6, 4, 4);
        whitelistGroup.Controls.Add(_whitelistOnly);
        _whitelistOnly.Dock = DockStyle.Top;

        table.Controls.Add(whitelistGroup, 1, 0);

        var footer = Ui.Flow(DockStyle.Bottom);
        var save = Ui.Btn(Loc.T("Kaydet"), (_, _) => SaveAndClose());
        footer.Controls.Add(save);
        footer.Controls.Add(Ui.Btn(Loc.T("İptal"), (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }));

        Controls.Add(table);
        Controls.Add(footer);
        AcceptButton = save;
    }

    private static GroupBox BuildGroup(string title, ListBox list, IEnumerable<string> values)
    {
        var group = new DarkGroup
        {
            Text = title,
            Dock = DockStyle.Fill,
            Padding = new Padding(8)
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));

        var input = new TextBox { Dock = DockStyle.Fill, Margin = new Padding(2, 4, 2, 2) };
        input.PlaceholderText = Loc.T("Oyuncu adı yazın");

        list.Dock = DockStyle.Fill;
        list.Margin = new Padding(2);
        foreach (var value in values.OrderBy(v => v, StringComparer.OrdinalIgnoreCase))
            list.Items.Add(value);

        var buttons = Ui.Flow(DockStyle.Fill);
        buttons.Controls.Add(Ui.Btn(Loc.T("Ekle"), (_, _) =>
        {
            var name = input.Text.Trim();
            if (name.Length == 0) return;

            var exists = list.Items.Cast<string>()
                .Any(i => string.Equals(i, name, StringComparison.OrdinalIgnoreCase));
            if (!exists) list.Items.Add(name);
            input.Clear();
        }));
        buttons.Controls.Add(Ui.Btn(Loc.T("Seçileni Kaldır"), (_, _) =>
        {
            if (list.SelectedIndex >= 0) list.Items.RemoveAt(list.SelectedIndex);
        }));

        layout.Controls.Add(input, 0, 0);
        layout.Controls.Add(list, 0, 1);
        layout.Controls.Add(buttons, 0, 2);
        group.Controls.Add(layout);
        return group;
    }

    private void SaveAndClose()
    {
        Result.Banned = _banned.Items.Cast<string>().ToList();
        Result.Whitelist = _whitelist.Items.Cast<string>().ToList();
        Result.WhitelistEnabled = _whitelistOnly.Checked;
        DialogResult = DialogResult.OK;
        Close();
    }
}

/// <summary>Tek satır metin girişi (mesaj/duyuru için).</summary>
internal sealed class TextInputDialog : Form
{
    private readonly TextBox _input = new();

    public string Value => _input.Text.Trim();

    public TextInputDialog(string title, string label, string initial = "")
    {
        Text = title;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(480, 150);

        var caption = new Label
        {
            Text = label,
            Dock = DockStyle.Top,
            Height = 44,
            Padding = new Padding(10, 8, 10, 0)
        };

        _input.Dock = DockStyle.Top;
        _input.Margin = new Padding(10);
        _input.Text = initial;
        _input.Padding = new Padding(6);

        var footer = Ui.Flow(DockStyle.Bottom);
        var ok = Ui.Btn(Loc.T("Gönder"), (_, _) => SaveAndClose());
        footer.Controls.Add(ok);
        footer.Controls.Add(Ui.Btn(Loc.T("İptal"), (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }));

        Controls.Add(footer);
        Controls.Add(_input);
        Controls.Add(caption);
        AcceptButton = ok;
    }

    private void SaveAndClose()
    {
        if (Value.Length == 0) return;
        DialogResult = DialogResult.OK;
        Close();
    }
}
