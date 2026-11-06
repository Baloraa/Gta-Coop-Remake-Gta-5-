using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Windows.Forms;
using GTA;
using GTA.Native;
using GTA.UI;
using Newtonsoft.Json;

namespace GTACoOp
{
    /// <summary>
    /// Launcher'daki "Arayüz Tasarımı" sayfasının oyun içi çizicisi.
    /// Yerleşim %LOCALAPPDATA%\GTACoopRemake\ui\hud.json dosyasından okunur;
    /// dosya değişince oyun yeniden başlatılmadan yeniden yüklenir.
    /// </summary>
    internal sealed class HudOverlay
    {
        private sealed class Element
        {
            public string type { get; set; }
            public double x { get; set; }
            public double y { get; set; }
            public double w { get; set; }
            public double h { get; set; }
            public string color { get; set; }
            public double opacity { get; set; }
            public string text { get; set; }
            public double fontSize { get; set; }
            public string align { get; set; }
            public string png { get; set; }
            public int cols { get; set; }
            public int rows { get; set; }
            public string pixels { get; set; }

            [JsonIgnore] public bool IsPanel => string.Equals(type, "panel", StringComparison.OrdinalIgnoreCase);
        }

        private sealed class Shortcut
        {
            public string key { get; set; }
            public string action { get; set; }
        }

        private sealed class Layout
        {
            public string name { get; set; }
            public bool enabled { get; set; }
            public bool hideGameHud { get; set; }
            public bool hideRadar { get; set; }
            public bool hideMinimapMap { get; set; }
            public bool hideWantedStars { get; set; }
            public bool hideStreetName { get; set; }
            public bool hideVehicleInfo { get; set; }
            public bool hideHelpText { get; set; }
            public List<Element> elements { get; set; }
            public List<Shortcut> shortcuts { get; set; }
        }

        private static readonly string LayoutPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "GTACoopRemake", "ui", "hud.json");

        private Layout _layout;
        private DateTime _stamp;
        private DateTime _nextCheck = DateTime.MinValue;
        private bool _hudVisible = true;
        private bool _radarVisible = true;
        private bool _applied;

        // HUD_COMPONENT_* kimlikleri: tek tek gizlenen oyun arayüzü parçaları.
        private const int HudWantedStars = 1;
        private const int HudVehicleName = 6;
        private const int HudAreaName = 7;
        private const int HudVehicleClass = 8;
        private const int HudStreetName = 9;
        private const int HudHelpText = 10;

        public void Tick(bool menuVisible)
        {
            try
            {
                Reload();

                // Özellik kapalı (varsayılan) ya da HUD kapatıldı: oyunun kendi
                // arayüzü aynen geri gelir, hiçbir şey gizli kalmaz.
                if (_layout == null || !_layout.enabled)
                {
                    Restore();
                    return;
                }

                HandleKeys();
                if (menuVisible || !_hudVisible)
                {
                    Restore();
                    return;
                }

                if (_layout.hideGameHud)
                {
                    Function.Call(Hash.HIDE_HUD_AND_RADAR_THIS_FRAME);
                    _applied = true;
                }

                if (_layout.hideWantedStars) HideComponent(HudWantedStars);
                if (_layout.hideStreetName)
                {
                    HideComponent(HudStreetName);
                    HideComponent(HudAreaName);
                }
                if (_layout.hideVehicleInfo)
                {
                    HideComponent(HudVehicleName);
                    HideComponent(HudVehicleClass);
                }
                if (_layout.hideHelpText) HideComponent(HudHelpText);

                if (_layout.hideMinimapMap)
                {
                    Function.Call(Hash.HIDE_MINIMAP_EXTERIOR_MAP_THIS_FRAME);
                    Function.Call(Hash.HIDE_MINIMAP_INTERIOR_MAP_THIS_FRAME);
                    _applied = true;
                }

                if (!_radarVisible || _layout.hideRadar)
                {
                    Function.Call(Hash.DISPLAY_RADAR, false);
                    _applied = true;
                }

                foreach (var element in _layout.elements ?? new List<Element>())
                    Draw(element);
            }
            catch
            {
                // Çizim hatası oyunu kilitlemesin; sonraki karede yeniden denenir.
            }
        }

        private static void HideComponent(int id)
        {
            Function.Call(Hash.HIDE_HUD_COMPONENT_THIS_FRAME, id);
        }

        /// <summary>Gizlenen her şeyi geri açar; oyun/sunucu dosyalarına dokunmaz.</summary>
        private void Restore()
        {
            if (!_applied) return;
            _applied = false;

            try
            {
                Function.Call(Hash.DISPLAY_RADAR, true);
            }
            catch
            {
                // Sürüm bu native'i tanımıyorsa oyun yine çalışsın.
            }
        }

        private void Reload()
        {
            if (DateTime.UtcNow < _nextCheck) return;
            _nextCheck = DateTime.UtcNow.AddSeconds(2);

            try
            {
                if (!File.Exists(LayoutPath))
                {
                    _layout = null;
                    return;
                }

                var stamp = File.GetLastWriteTimeUtc(LayoutPath);
                if (_layout != null && stamp == _stamp) return;

                _layout = JsonConvert.DeserializeObject<Layout>(File.ReadAllText(LayoutPath));
                _stamp = stamp;
            }
            catch
            {
                // Dosya yazılırken okunmuş olabilir; sonraki denemede alınır.
            }
        }

        private void HandleKeys()
        {
            if (_layout?.shortcuts == null) return;

            foreach (var shortcut in _layout.shortcuts)
            {
                if (shortcut == null || string.IsNullOrWhiteSpace(shortcut.key)) continue;
                if (!Enum.TryParse(shortcut.key, true, out Keys key) || key == Keys.None) continue;
                if (!Game.IsKeyPressed(key)) continue;

                switch ((shortcut.action ?? "").ToLowerInvariant())
                {
                    case "toggle": _hudVisible = !_hudVisible; break;
                    case "hide": _hudVisible = false; break;
                    case "radar": _radarVisible = !_radarVisible; break;
                    case "players": ShowPlayerList(); break;
                    case "voice": ToggleVoice(); break;
                }

                // Aynı tuşun her karede tetiklenmesini engelle.
                System.Threading.Thread.Sleep(120);
            }
        }

        private static void ToggleVoice()
        {
            try
            {
                Main.PlayerSettings.VoiceEnabled = !Main.PlayerSettings.VoiceEnabled;
                Notification.Show("Sesli sohbet: " + (Main.PlayerSettings.VoiceEnabled ? "açık" : "kapalı"));
            }
            catch { }
        }

        private static void ShowPlayerList()
        {
            try
            {
                Function.Call(Hash.SET_TEXT_FONT, 0);
                Function.Call(Hash.SET_TEXT_SCALE, 0.36f, 0.36f);
                Function.Call(Hash.SET_TEXT_COLOUR, 53, 199, 89, 255);
                Function.Call(Hash.SET_TEXT_WRAP, 0.02f, 0.4f);
                Function.Call(Hash.BEGIN_TEXT_COMMAND_DISPLAY_TEXT, "STRING");
                Function.Call(Hash.ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME,
                    "GTACoop Remake - " + (Main.Opponents == null ? 0 : Main.Opponents.Count + 1) + " oyuncu");
                Function.Call(Hash.END_TEXT_COMMAND_DISPLAY_TEXT, 0.02f, 0.2f);
            }
            catch { }
        }

        // ------------------------------------------------------------- çizim

        private static void Draw(Element e)
        {
            if (e == null) return;

            var x = (float)e.x;
            var y = (float)e.y;
            var w = (float)e.w;
            var h = (float)e.h;

            switch ((e.type ?? "").ToLowerInvariant())
            {
                case "panel":
                    Rect(x, y, w, h, ParseColor(e.color, (int)Math.Round(255 * Clamp01(e.opacity))));
                    break;

                case "text":
                    Text(e.text ?? "", x, y, w, h, (float)Math.Max(0.12, e.fontSize),
                        ParseColor(e.color, 255), e.align);
                    break;

                case "clock":
                {
                    var hour = Function.Call<int>(Hash.GET_CLOCK_HOURS);
                    var minute = Function.Call<int>(Hash.GET_CLOCK_MINUTES);
                    Text($"{hour:00}:{minute:00}", x, y, w, h, (float)Math.Max(0.12, e.fontSize),
                        ParseColor(e.color, 255), e.align);
                    break;
                }

                case "ping":
                    Text($"{Main.Latency * 1000f:0} ms", x, y, w, h, (float)Math.Max(0.12, e.fontSize),
                        ParseColor(e.color, 255), e.align);
                    break;

                case "players":
                    DrawPlayers(e, x, y, w, h);
                    break;

                case "bar":
                    DrawBar(e, x, y, w, h);
                    break;

                case "image":
                    DrawPixels(e, x, y, w, h);
                    break;
            }
        }

        private static void DrawPlayers(Element e, float x, float y, float w, float h)
        {
            var color = ParseColor(e.color, 255);
            var scale = (float)Math.Max(0.1, e.fontSize);
            var rows = Math.Max(1, (int)(h / (scale * 0.045f)));

            var lines = new List<string>();
            var local = (Main.IsLocalTalking ? "[MIC] " : "") + Main.PlayerSettings.Username;
            lines.Add($"{local}  {Main.Latency * 1000f:0}ms");

            if (Main.Opponents != null)
            {
                foreach (var opponent in Main.Opponents)
                {
                    if (lines.Count >= rows) break;
                    var ped = opponent.Value;
                    var name = (ped.IsTalking ? "[MIC] " : "") + ped.Name;
                    lines.Add($"{name}  {ped.Latency * 1000f:0}ms");
                }
            }

            for (var i = 0; i < lines.Count; i++)
                Text(lines[i], x, y + i * scale * 0.032f, w, h, scale, color, e.align);
        }

        private static void DrawBar(Element e, float x, float y, float w, float h)
        {
            Rect(x, y, w, h, ParseColor("#000000", 150));

            var ped = Game.Player.Character;
            var health = ped.MaxHealth <= 0 ? 0f : (float)ped.Health / ped.MaxHealth;
            var armor = Math.Max(0f, Math.Min(1f, ped.Armor / 100f));
            var split = h * 0.55f;

            Rect(x + 0.001f, y + 0.001f, (w - 0.002f) * Clamp01(health), split - 0.002f,
                ParseColor(e.color, 255));
            Rect(x + 0.001f, y + split + 0.001f, (w - 0.002f) * armor, h - split - 0.002f,
                ParseColor("#7FB3FF", 255));
        }

        private static void DrawPixels(Element e, float x, float y, float w, float h)
        {
            if (e.cols <= 0 || e.rows <= 0 || string.IsNullOrEmpty(e.pixels)) return;

            var rows = e.pixels.Split(';');
            var cellW = w / e.cols;
            var cellH = h / e.rows;

            for (var r = 0; r < rows.Length && r < e.rows; r++)
            {
                var cells = rows[r].Split(',');
                for (var c = 0; c < cells.Length && c < e.cols; c++)
                {
                    var cell = cells[c];
                    if (cell.Length != 8) continue;
                    if (!int.TryParse(cell.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var red)) continue;
                    if (!int.TryParse(cell.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var green)) continue;
                    if (!int.TryParse(cell.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var blue)) continue;
                    if (!int.TryParse(cell.Substring(6, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var alpha)) continue;

                    Rect(x + c * cellW, y + r * cellH, cellW * 1.02f, cellH * 1.05f,
                        System.Drawing.Color.FromArgb(alpha, red, green, blue));
                }
            }
        }

        private static void Rect(float x, float y, float w, float h, System.Drawing.Color color)
        {
            Function.Call(Hash.DRAW_RECT, x + w / 2f, y + h / 2f, w, h,
                color.R, color.G, color.B, color.A, false);
        }

        private static void Text(string text, float x, float y, float w, float h, float scale,
            System.Drawing.Color color, string align)
        {
            if (string.IsNullOrEmpty(text)) return;

            Function.Call(Hash.SET_TEXT_FONT, 0);
            Function.Call(Hash.SET_TEXT_SCALE, scale, scale);
            Function.Call(Hash.SET_TEXT_COLOUR, color.R, color.G, color.B, color.A);
            Function.Call(Hash.SET_TEXT_DROPSHADOW, 0, 0, 0, 0, 200);
            Function.Call(Hash.SET_TEXT_EDGE, 1, 0, 0, 0, 200);
            Function.Call(Hash.SET_TEXT_WRAP, x, x + w);

            var mode = (align ?? "left").ToLowerInvariant();
            Function.Call(Hash.SET_TEXT_CENTRE, mode == "center");
            if (mode == "right") Function.Call(Hash.SET_TEXT_RIGHT_JUSTIFY, true);

            Function.Call(Hash.BEGIN_TEXT_COMMAND_DISPLAY_TEXT, "STRING");
            Function.Call(Hash.ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME, text);
            Function.Call(Hash.END_TEXT_COMMAND_DISPLAY_TEXT, x, y + h * 0.28f);
        }

        private static System.Drawing.Color ParseColor(string hex, int alpha)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(hex))
                {
                    var text = hex.TrimStart('#');
                    if (text.Length == 6)
                    {
                        var r = Convert.ToInt32(text.Substring(0, 2), 16);
                        var g = Convert.ToInt32(text.Substring(2, 2), 16);
                        var b = Convert.ToInt32(text.Substring(4, 2), 16);
                        return System.Drawing.Color.FromArgb(Math.Max(0, Math.Min(255, alpha)), r, g, b);
                    }
                }
            }
            catch { }

            return System.Drawing.Color.FromArgb(Math.Max(0, Math.Min(255, alpha)), 237, 237, 237);
        }

        private static float Clamp01(double value) => (float)Math.Max(0, Math.Min(1, value));
    }
}
