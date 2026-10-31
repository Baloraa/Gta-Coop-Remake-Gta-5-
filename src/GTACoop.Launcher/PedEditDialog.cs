using System.Windows.Forms;
using GTACoop.Sync;

namespace GTACoop.Remake;

/// <summary>Tek bir addon pedin görünen adı, türü ve meta veri alanlarını düzenler.</summary>
public sealed class PedEditDialog : Form
{
    private readonly TextBox _name = new();
    private readonly ComboBox _kind = new();
    private readonly NumericUpDown _health = new();
    private readonly CheckBox _enabled = new();
    private readonly TextBox _clip = new();
    private readonly TextBox _movement = new();
    private readonly TextBox _decision = new();
    private readonly TextBox _personality = new();
    private readonly TextBox _combat = new();

    public AddonPedInfo Ped { get; }

    public PedEditDialog(AddonPedInfo ped, bool isNew)
    {
        Ped = new AddonPedInfo
        {
            Model = ped.Model,
            Name = ped.Name,
            Kind = ped.Kind,
            Enabled = ped.Enabled,
            Streamed = ped.Streamed,
            Health = ped.Health,
            ClipDictionary = ped.ClipDictionary,
            MovementClipSet = ped.MovementClipSet,
            DecisionMaker = ped.DecisionMaker,
            Personality = ped.Personality,
            CombatInfo = ped.CombatInfo,
            Files = new List<string>(ped.Files)
        };

        Text = isNew ? T("Ped Ekle") : T("Pedi Düzenle");
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(560, 470);
        Font = new Font("Segoe UI", 9f);
        Padding = new Padding(10);

        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 12,
            Padding = new Padding(4)
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int i = 0; i < 11; i++)
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var model = new TextBox
        {
            Text = Ped.Model,
            ReadOnly = true,
            Dock = DockStyle.Fill,
            BackColor = SystemColors.Control
        };
        var streamed = new CheckBox
        {
            Text = Ped.Streamed ? T("Evet (bileşen klasörü)") : T("Hayır"),
            Checked = Ped.Streamed,
            Enabled = false,
            AutoSize = true
        };

        _name.Text = Ped.Name;
        _name.Dock = DockStyle.Fill;
        _kind.DropDownStyle = ComboBoxStyle.DropDownList;
        _kind.Items.AddRange(new object[] { T("İnsan"), T("Kadın"), T("Hayvan") });
        _kind.SelectedIndex = Ped.Kind switch
        {
            "female" => 1,
            "animal" => 2,
            _ => 0
        };
        _health.Minimum = 1;
        _health.Maximum = 10000;
        _health.Value = Math.Clamp(Ped.Health <= 0 ? 200 : Ped.Health, 1, 10000);
        _health.Width = 100;
        _enabled.Text = T("Etkin (oyuncu menüsünde görünür)");
        _enabled.Checked = Ped.Enabled;
        _enabled.AutoSize = true;

        _clip.Text = Ped.ClipDictionary;
        _movement.Text = Ped.MovementClipSet;
        _decision.Text = Ped.DecisionMaker;
        _personality.Text = Ped.Personality;
        _combat.Text = Ped.CombatInfo;
        foreach (var box in new[] { _clip, _movement, _decision, _personality, _combat })
            box.Dock = DockStyle.Fill;

        AddRow(table, 0, T("Model (dosyalardan)"), model);
        AddRow(table, 1, T("Görünen Ad"), _name);
        AddRow(table, 2, T("Tür"), _kind);
        AddRow(table, 3, T("Streamed"), streamed);
        AddRow(table, 4, T("Sağlık"), _health);
        AddRow(table, 5, T("Clip Dictionary"), _clip);
        AddRow(table, 6, T("Movement Clip Set"), _movement);
        AddRow(table, 7, T("Decision Maker"), _decision);
        AddRow(table, 8, T("Personality"), _personality);
        AddRow(table, 9, T("Combat Info"), _combat);
        AddRow(table, 10, T("Etkin"), _enabled);

        var files = Ui.Lbl(T("Dosya: {0} • {1}", Ped.Files.Count,
            Ped.Streamed ? T("bileşen klasörü") : T("gevşek dosya")));
        files.ForeColor = Color.DimGray;
        table.Controls.Add(files, 0, 11);
        table.SetColumnSpan(files, 2);

        var buttons = Ui.Flow(DockStyle.Bottom);
        buttons.FlowDirection = FlowDirection.RightToLeft;
        var save = Ui.Btn(T("Kaydet"), (_, _) => SaveAndClose());
        var cancel = Ui.Btn(T("İptal"), (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        });
        buttons.Controls.Add(save);
        buttons.Controls.Add(cancel);

        Controls.Add(table);
        Controls.Add(buttons);
        AcceptButton = save;
        CancelButton = cancel;
    }

    private static void AddRow(TableLayoutPanel table, int row, string label, Control control)
    {
        var lbl = new Label
        {
            Text = label,
            AutoSize = true,
            Anchor = AnchorStyles.Left | AnchorStyles.Top,
            Margin = new Padding(4, 7, 4, 2)
        };
        control.Margin = new Padding(4, 4, 4, 2);
        if (!(control is CheckBox)) control.Height = 26;
        table.Controls.Add(lbl, 0, row);
        table.Controls.Add(control, 1, row);
    }

    private void SaveAndClose()
    {
        if (string.IsNullOrWhiteSpace(_name.Text))
            _name.Text = AddonPeds.PrettifyName(Ped.Model);

        Ped.Name = _name.Text.Trim();
        Ped.Kind = _kind.SelectedIndex switch
        {
            1 => "female",
            2 => "animal",
            _ => "human"
        };
        Ped.Health = (int)_health.Value;
        Ped.Enabled = _enabled.Checked;
        Ped.ClipDictionary = _clip.Text.Trim();
        Ped.MovementClipSet = _movement.Text.Trim();
        Ped.DecisionMaker = _decision.Text.Trim();
        Ped.Personality = _personality.Text.Trim();
        Ped.CombatInfo = _combat.Text.Trim();

        DialogResult = DialogResult.OK;
        Close();
    }

    private static string T(string text, params object?[] args) => Loc.T(text, args);
}

/// <summary>Kısa bir metin isteyen basit iletişim kutusu.</summary>
public sealed class PedTextPromptDialog : Form
{
    private readonly TextBox _box = new();

    public string Value => _box.Text.Trim();

    public PedTextPromptDialog(string title, string label, string initial)
    {
        Text = title;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(420, 130);
        Font = new Font("Segoe UI", 9f);
        Padding = new Padding(12);

        var lbl = new Label { Text = label, AutoSize = true, Dock = DockStyle.Top };
        _box.Text = initial;
        _box.Dock = DockStyle.Top;
        _box.Margin = new Padding(0, 8, 0, 8);

        var buttons = Ui.Flow(DockStyle.Bottom);
        buttons.FlowDirection = FlowDirection.RightToLeft;
        var ok = Ui.Btn(Loc.T("Tamam"), (_, _) => DialogResult = DialogResult.OK);
        var cancel = Ui.Btn(Loc.T("İptal"), (_, _) => DialogResult = DialogResult.Cancel);
        buttons.Controls.Add(ok);
        buttons.Controls.Add(cancel);

        var spacer = new Panel { Dock = DockStyle.Top, Height = 8 };
        Controls.Add(spacer);
        Controls.Add(_box);
        Controls.Add(lbl);
        Controls.Add(buttons);
        AcceptButton = ok;
        CancelButton = cancel;
    }
}
