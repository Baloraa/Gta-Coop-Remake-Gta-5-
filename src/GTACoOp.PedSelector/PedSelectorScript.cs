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
        private const int VisibleRows = 12;
        private Keys _toggleKey = Keys.F10;

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
            LoadIni();
            LoadConfig();
            Log("yüklendi, " + _peds.Count + " ped");
        }

        /// <summary>
        /// Scriptin aranacağı klasörler. SHVDN scripti belleğe yüklediğinde
        /// Assembly.Location boş kalabildiği için tek yola güvenilmez; oyun
        /// kökü ve scripts klasörü de denenir.
        /// </summary>
        private static List<string> _dirs;

        private static List<string> Dirs
        {
            get
            {
                if (_dirs != null) return _dirs;

                var dirs = new List<string>();
                void Add(string path)
                {
                    if (string.IsNullOrWhiteSpace(path)) return;
                    foreach (var known in dirs)
                        if (known.Equals(path, StringComparison.OrdinalIgnoreCase)) return;
                    dirs.Add(path);
                }

                try { Add(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? ""); }
                catch { }

                try
                {
                    var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                    Add(Path.Combine(baseDir, "scripts"));
                    Add(baseDir);
                }
                catch { }

                try
                {
                    var cwd = Directory.GetCurrentDirectory();
                    Add(Path.Combine(cwd, "scripts"));
                    Add(cwd);
                }
                catch { }

                if (dirs.Count == 0) dirs.Add(".");
                _dirs = dirs;
                return _dirs;
            }
        }

        /// <summary>Script klasörünü (log ve ayar dosyaları için) bulur.</summary>
        private static string ScriptDir
        {
            get
            {
                foreach (var dir in Dirs)
                {
                    try
                    {
                        if (File.Exists(Path.Combine(dir, "AddonPedPlus.dll"))) return dir;
                    }
                    catch { }
                }
                return Dirs[0];
            }
        }

        /// <summary>Verilen dosyayı bilinen klasörlerde arar; yoksa ilk klasörü döndürür.</summary>
        private static string FindFile(string fileName)
        {
            foreach (var dir in Dirs)
            {
                try
                {
                    var path = Path.Combine(dir, fileName);
                    if (File.Exists(path)) return path;
                }
                catch { }
            }
            return Path.Combine(Dirs[0], fileName);
        }

        /// <summary>Desteklenen ped listesi dosya adları (sırayla denenir).</summary>
        private static readonly string[] ListFileNames =
        {
            "GTACoop-peds.txt", "AddonPedPlus-peds.txt", "peds.txt"
        };

        private static void Log(string message)
        {
            var line = DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture) + " " +
                       message + Environment.NewLine;

            // Önce script klasörü; yazılamıyorsa kullanıcı profilindeki klasör.
            var targets = new List<string> { ScriptDir };
            try
            {
                targets.Add(Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "GTACoop Ped Selector"));
            }
            catch { }

            foreach (var dir in targets)
            {
                try
                {
                    Directory.CreateDirectory(dir);
                    File.AppendAllText(Path.Combine(dir, "GTACoop-pedselector.log"), line);
                    return;
                }
                catch { }
            }
        }

        /// <summary>
        /// Menü tuşunu yapılandırır: scripts klasöründeki AddonPedPlus.ini
        /// içindeki MenuKey= değeri (ör. F10, F7, M). Yoksa F10 kullanılır.
        /// </summary>
        private void LoadIni()
        {
            try
            {
                var ini = FindFile("AddonPedPlus.ini");
                if (!File.Exists(ini)) return;

                foreach (var raw in File.ReadAllLines(ini))
                {
                    var line = (raw ?? "").Trim();
                    if (line.Length == 0 || line.StartsWith(";", StringComparison.Ordinal) ||
                        line.StartsWith("#", StringComparison.Ordinal) ||
                        line.StartsWith("[", StringComparison.Ordinal))
                        continue;

                    var split = line.IndexOf('=');
                    if (split <= 0) continue;
                    var key = line.Substring(0, split).Trim();
                    var value = line.Substring(split + 1).Trim();
                    if (!key.Equals("MenuKey", StringComparison.OrdinalIgnoreCase)) continue;

                    if (Enum.TryParse<Keys>(value, true, out var parsed))
                    {
                        _toggleKey = parsed;
                        Log("menü tuşu: " + parsed);
                    }
                }
            }
            catch (Exception ex)
            {
                Log("ini okunamadı: " + ex.Message);
            }
        }

        /// <summary>model|ad|tür|sağlık|temel model|ragdoll|meta doğrulandı</summary>
        private void LoadConfig()
        {
            try
            {
                _peds.Clear();

                var configPath = "";
                foreach (var name in ListFileNames)
                {
                    configPath = FindFile(name);
                    if (File.Exists(configPath)) break;
                    configPath = "";
                }
                if (configPath.Length == 0)
                {
                    Log("liste yok. Aranan klasörler: " + string.Join(" | ", Dirs));
                    return;
                }

                Log("liste: " + configPath);
                foreach (var raw in File.ReadAllLines(configPath))
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
            if (e.KeyCode == _toggleKey)
            {
                _menuOpen = !_menuOpen;
                // Liste ilk açılışta boş kaldıysa (oyun içinde kurulum, geç
                // yüklenen dosya) menü her açılışta yeniden okumayı dener.
                if (_menuOpen && _peds.Count == 0) LoadConfig();
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
                DrawText(ScriptDir, 0.05f, 0.09f, 0.3f, System.Drawing.Color.Gainsboro);
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
