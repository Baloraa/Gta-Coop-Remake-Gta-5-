using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using GTA;
using GTA.Native;
using GTA.UI;

namespace GTACoOp.PedSelector
{
    /// <summary>
    /// Bağımsız ped seçici: sunucuya veya GTACoop launcher'ına ihtiyaç duymaz.
    /// Ped listesini kendi klasöründeki GTACoop-peds.txt dosyasından okur.
    /// F10 menüyü açar/kapatır.
    /// </summary>
    public class PedSelectorScript : Script
    {
        private const Keys ToggleKey = Keys.F10;
        private const int VisibleRows = 12;

        private sealed class PedEntry
        {
            public string Model = "";
            public string Name = "";
            public string Kind = "human";
            public string BaseModel = "";
            public string Ragdoll = "auto";
            public int Health = 200;
            public bool MetaVerified;
        }

        private readonly List<PedEntry> _peds = new List<PedEntry>();
        private bool _menuOpen;
        private int _index;
        private int _scroll;

        public PedSelectorScript()
        {
            Interval = 0;
            Tick += OnTick;
            KeyDown += OnKeyDown;
            LoadConfig();
            Log("yüklendi, " + _peds.Count + " ped");
        }

        private static string ConfigPath
        {
            get
            {
                var dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? ".";
                return Path.Combine(dir, "GTACoop-peds.txt");
            }
        }

        private static void Log(string message)
        {
            try
            {
                var dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? ".";
                File.AppendAllText(Path.Combine(dir, "GTACoop-pedselector.log"),
                    DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture) + " " + message +
                    Environment.NewLine);
            }
            catch { }
        }

        /// <summary>model|ad|tür|sağlık|temel model|ragdoll|meta doğrulandı</summary>
        private void LoadConfig()
        {
            try
            {
                if (!File.Exists(ConfigPath))
                {
                    Log("liste yok: " + ConfigPath);
                    return;
                }

                foreach (var raw in File.ReadAllLines(ConfigPath))
                {
                    var line = (raw ?? "").Trim();
                    if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal)) continue;
                    var parts = line.Split('|');
                    if (parts.Length < 1 || parts[0].Trim().Length == 0) continue;

                    var ped = new PedEntry
                    {
                        Model = parts[0].Trim().ToLowerInvariant(),
                        Name = parts.Length > 1 && parts[1].Trim().Length > 0
                            ? parts[1].Trim() : parts[0].Trim(),
                        Kind = parts.Length > 2 ? parts[2].Trim() : "human",
                        Health = parts.Length > 3 && int.TryParse(parts[3], out var hp) ? hp : 200,
                        BaseModel = parts.Length > 4 ? parts[4].Trim() : "",
                        Ragdoll = parts.Length > 5 ? parts[5].Trim() : "auto",
                        MetaVerified = parts.Length > 6 &&
                                       bool.TryParse(parts[6].Trim(), out var verified) && verified
                    };
                    _peds.Add(ped);
                }
            }
            catch (Exception ex)
            {
                Log("liste okunamadı: " + ex.Message);
            }
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == ToggleKey)
            {
                _menuOpen = !_menuOpen;
                e.SuppressKeyPress = true;
                return;
            }

            if (!_menuOpen || _peds.Count == 0) return;

            switch (e.KeyCode)
            {
                case Keys.Up:
                    _index = (_index - 1 + _peds.Count) % _peds.Count;
                    e.SuppressKeyPress = true;
                    break;
                case Keys.Down:
                    _index = (_index + 1) % _peds.Count;
                    e.SuppressKeyPress = true;
                    break;
                case Keys.Enter:
                    Apply(_peds[_index]);
                    _menuOpen = false;
                    e.SuppressKeyPress = true;
                    break;
                case Keys.Back:
                case Keys.Escape:
                    Restore();
                    _menuOpen = false;
                    e.SuppressKeyPress = true;
                    break;
            }
        }

        private void OnTick(object sender, EventArgs e)
        {
            if (!_menuOpen) return;

            if (_peds.Count == 0)
            {
                DrawText("Ped listesi boş. GTACoop-peds.txt bulunamadı.", 0.05f, 0.05f, 0.3f,
                    System.Drawing.Color.OrangeRed);
                return;
            }

            if (_index < _scroll) _scroll = _index;
            if (_index >= _scroll + VisibleRows) _scroll = _index - VisibleRows + 1;

            var x = 0.05f;
            var y = 0.05f;
            var width = 0.22f;
            var rows = Math.Min(VisibleRows, _peds.Count);
            Rectangle(x, y, width, 0.035f, 20, 20, 20, 210);
            DrawText("Ped Seçici  (" + (_index + 1) + "/" + _peds.Count + ")", x + 0.005f, y, 0.22f,
                System.Drawing.Color.White);

            for (var row = 0; row < rows; row++)
            {
                var ped = _peds[_scroll + row];
                var rowY = y + 0.04f + row * 0.026f;
                if (_scroll + row == _index)
                    Rectangle(x, rowY - 0.002f, width, 0.024f, 30, 90, 40, 220);

                var label = ped.Name + (ped.Kind == "animal" ? "  [hayvan]" : "");
                DrawText(label, x + 0.005f, rowY, 0.3f,
                    _scroll + row == _index ? System.Drawing.Color.White : System.Drawing.Color.LightGray);
            }

            var infoY = y + 0.04f + rows * 0.026f + 0.005f;
            DrawText("ENTER uygula · BACKSPACE normal ped · F10 kapat", x, infoY, 0.24f,
                System.Drawing.Color.Gainsboro);
        }

        private static void Apply(PedEntry ped)
        {
            try
            {
                var hash = Game.GenerateHash(ped.Model);
                var model = new Model(hash);
                if (!model.IsValid)
                {
                    Notification.Show("~r~Geçersiz model: " + ped.Model);
                    Log("geçersiz model: " + ped.Model);
                    return;
                }

                if (!model.IsInCdImage)
                    Notification.Show("~o~Ped yüklü değil. Addon ped paketine OIV ile kuruldu mu?");

                model.Request(1500);
                var waited = 0;
                while (!model.IsLoaded && waited < 3000)
                {
                    Script.Yield();
                    waited += 100;
                }

                if (!model.IsLoaded)
                {
                    Notification.Show("~r~Model yüklenemedi: " + ped.Model);
                    return;
                }

                Game.Player.ChangeModel(model);
                Script.Yield();
                Game.Player.Character.Health = ped.Health > 0 ? ped.Health : 200;
                Notification.Show("~g~Ped: " + ped.Name);
                Log("uygulandı: " + ped.Model);
            }
            catch (Exception ex)
            {
                Log("uygulanamadı (" + ped.Model + "): " + ex.Message);
                Notification.Show("~r~Ped değiştirilemedi.");
            }
        }

        private static void Restore()
        {
            try
            {
                var model = new Model(PedHash.Michael);
                model.Request(1000);
                Game.Player.ChangeModel(model);
                Notification.Show("~g~Normal pede dönüldü.");
            }
            catch (Exception ex)
            {
                Log("geri dönülemedi: " + ex.Message);
            }
        }

        private static void Rectangle(float x, float y, float width, float height,
            int r, int g, int b, int a)
        {
            Function.Call(Hash.DRAW_RECT, x + width / 2f, y + height / 2f, width, height,
                r, g, b, a);
        }

        private static void DrawText(string text, float x, float y, float scale,
            System.Drawing.Color color)
        {
            Function.Call(Hash.SET_TEXT_FONT, 0);
            Function.Call(Hash.SET_TEXT_SCALE, 0.0, scale);
            Function.Call(Hash.SET_TEXT_COLOUR, color.R, color.G, color.B, 255);
            Function.Call(Hash.SET_TEXT_DROPSHADOW, 1, 0, 0, 0, 255);
            Function.Call(Hash.SET_TEXT_EDGE, 1, 0, 0, 0, 255);
            Function.Call(Hash.BEGIN_TEXT_COMMAND_DISPLAY_TEXT, "STRING");
            Function.Call(Hash.ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME, text ?? "");
            Function.Call(Hash.END_TEXT_COMMAND_DISPLAY_TEXT, x, y);
        }
    }
}
