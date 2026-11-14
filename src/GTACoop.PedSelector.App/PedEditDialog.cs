using System;
using System.Drawing;
using System.Windows.Forms;
using GTACoop.Sync;

namespace GTACoop.PedSelector.App
{
    /// <summary>
    /// Tek bir pedin alanlarını düzenleyen küçük koyu tema penceresi.
    /// Model adı GTA V kuralı gereği küçük harfe çevrilir.
    /// </summary>
    internal sealed class PedEditDialog : Form
    {
        private static readonly Color Bg = Color.FromArgb(22, 22, 26);
        private static readonly Color Surface = Color.FromArgb(32, 32, 38);
        private static readonly Color Border = Color.FromArgb(58, 58, 68);

        private readonly TextBox _name;
        private readonly TextBox _model;
        private readonly ComboBox _kind;
        private readonly NumericUpDown _health;
        private readonly TextBox _baseModel;
        private readonly ComboBox _ragdoll;
        private readonly CheckBox _enabled;

        public string PedName => _name.Text.Trim();
        public string Model => _model.Text.Trim().ToLowerInvariant();
        public string Kind => (_kind.SelectedItem as string) ?? "human";
        public int Health => (int)_health.Value;
        public string BaseModel => _baseModel.Text.Trim().ToLowerInvariant();
        public string Ragdoll => (_ragdoll.SelectedItem as string) ?? "auto";
        public bool Enabled => _enabled.Checked;

        public PedEditDialog(AddonPedInfo ped)
        {
            Text = "Pedi Düzenle";
            ClientSize = new Size(470, 372);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Bg;
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 9.5f);

            _name = MakeTextBox(ped.Name);
            _model = MakeTextBox(ped.Model);
            _baseModel = MakeTextBox(ped.BaseModel);

            _kind = MakeCombo(new[] { "human", "female", "animal" }, ped.Kind);
            _ragdoll = MakeCombo(new[] { "auto", "on", "off" },
                string.IsNullOrWhiteSpace(ped.Ragdoll) ? "auto" : ped.Ragdoll);

            _health = new NumericUpDown
            {
                Minimum = 1,
                Maximum = 10000,
                Value = ped.Health > 0 ? Math.Min(ped.Health, 10000) : 200,
                BackColor = Surface,
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Width = 200
            };

            _enabled = new CheckBox
            {
                Text = "Etkin (pakete ve menüye dahil edilsin)",
                Checked = ped.Enabled,
                AutoSize = true,
                ForeColor = Color.White
            };

            var y = 16;
            AddLabel("Ad", y);
            _name.Location = new Point(150, y - 2);
            y += 38;
            AddLabel("Model", y);
            _model.Location = new Point(150, y - 2);
            y += 38;
            AddLabel("Tür", y);
            _kind.Location = new Point(150, y - 2);
            y += 38;
            AddLabel("Sağlık", y);
            _health.Location = new Point(150, y - 2);
            y += 38;
            AddLabel("Temel model", y);
            _baseModel.Location = new Point(150, y - 2);
            y += 38;
            AddLabel("Ragdoll", y);
            _ragdoll.Location = new Point(150, y - 2);
            y += 38;
            _enabled.Location = new Point(150, y);

            var info = new Label
            {
                Text = "Model, .yft dosyasının adıdır. Temel model boşsa tür şablonu kullanılır.",
                Location = new Point(20, y + 30),
                Size = new Size(430, 34),
                ForeColor = Color.FromArgb(165, 165, 178)
            };

            var ok = MakeButton("Kaydet", DialogResult.OK);
            ok.Location = new Point(ClientSize.Width - 210, ClientSize.Height - 52);
            var cancel = MakeButton("İptal", DialogResult.Cancel);
            cancel.Location = new Point(ClientSize.Width - 110, ClientSize.Height - 52);

            Controls.AddRange(new Control[]
            {
                _name, _model, _kind, _health, _baseModel, _ragdoll, _enabled, info, ok, cancel
            });

            AcceptButton = ok;
            CancelButton = cancel;

            FormClosing += (_, e) =>
            {
                if (DialogResult != DialogResult.OK) return;
                if (Model.Length == 0)
                {
                    MessageBox.Show(this, "Model adı boş olamaz.", "Eksik bilgi",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    e.Cancel = true;
                }
            };
        }

        private void AddLabel(string text, int y)
        {
            Controls.Add(new Label
            {
                Text = text,
                Location = new Point(20, y + 1),
                Size = new Size(125, 22),
                ForeColor = Color.FromArgb(210, 210, 218)
            });
        }

        private TextBox MakeTextBox(string value) => new()
        {
            Text = value ?? "",
            Location = new Point(150, 14),
            Width = 290,
            BackColor = Surface,
            ForeColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle
        };

        private ComboBox MakeCombo(string[] items, string selected)
        {
            var combo = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 150,
                BackColor = Surface,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            combo.Items.AddRange(items);
            var index = Array.FindIndex(items,
                i => i.Equals(selected ?? "", StringComparison.OrdinalIgnoreCase));
            combo.SelectedIndex = index >= 0 ? index : 0;
            return combo;
        }

        private Button MakeButton(string text, DialogResult result)
        {
            var button = new Button
            {
                Text = text,
                DialogResult = result,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(12, 5, 12, 5),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(40, 40, 48),
                ForeColor = Color.White
            };
            button.FlatAppearance.BorderColor = Border;
            return button;
        }
    }
}
