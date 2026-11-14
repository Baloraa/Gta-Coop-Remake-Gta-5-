using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
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
        /// <summary>Izgaraya çevrilmiş tek bir resim hücresi (A=0 ise çizilmez).</summary>
        private struct Cell
        {
            public byte R;
            public byte G;
            public byte B;
            public byte A;
        }

        /// <summary>Bir satırdaki aynı renkli hücre dizisi; tek dikdörtgende çizilir.</summary>
        private struct Run
        {
            public int Col;
            public int Row;
            public int Len;
            public byte R;
            public byte G;
            public byte B;
            public byte A;
        }

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
            public double thickness { get; set; }

            // Kaç saniye görünsün (0 = süresiz). Süre dolunca oturum boyunca çizilmez.
            public double visibleSeconds { get; set; }

            // Launcher "alwaysVisible" yazar; eski dosyalarda "always" olabilir.
            [JsonProperty("alwaysVisible")] public bool alwaysVisible { get; set; }
            [JsonProperty("always")] public bool alwaysLegacy { get; set; }

            [JsonIgnore] public bool AlwaysVisible => alwaysVisible || alwaysLegacy;

            // Kare başına JSON ayrıştırması yapmamak için bir kez doldurulur.
            [JsonIgnore] public Run[] Runs;
            [JsonIgnore] public int RunCols = 1;
            [JsonIgnore] public int RunRows = 1;
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
            public AlertConfig notifications { get; set; }
        }

        /// <summary>Oyuncu giriş/çıkışında gösterilen isteğe bağlı resim ve ses.</summary>
        private sealed class AlertConfig
        {
            public bool enabled { get; set; }
            public string joinPng { get; set; }
            public string joinSound { get; set; }
            public double joinSeconds { get; set; }
            public string leavePng { get; set; }
            public string leaveSound { get; set; }
            public double leaveSeconds { get; set; }
            public double fadeSeconds { get; set; }
        }

        private sealed class Alert
        {
            public string Png;
            public DateTime Started;
            public double Seconds;
            public double Fade;
        }

        private static readonly string LayoutPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "GTACoopRemake", "ui", "hud.json");

        private static readonly string AssetsPath = Path.Combine(
            Path.GetDirectoryName(LayoutPath) ?? "", "assets");

        // Gerçek PNG çizimi: SHVDN 3.6+ CustomSprite dosyadan doku yükler.
        // Daha eski bir SHVDN kuruluysa ızgara yedeğine düşülür.
        private static readonly Dictionary<string, SpriteImage?> SpriteCache =
            new Dictionary<string, SpriteImage?>(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> SpriteFailed =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private sealed class SpriteImage
        {
            public object Instance = null!;
            public PropertyInfo? Position;
            public PropertyInfo? Size;
            public PropertyInfo? Enabled;
            public PropertyInfo? Color;
            public MethodInfo? Draw;
        }

        private readonly HashSet<Keys> _heldKeys = new HashSet<Keys>();

        private Layout _layout;
        private DateTime _stamp;
        private DateTime _nextCheck = DateTime.MinValue;
        private bool _hudVisible = true;
        private bool _radarVisible = true;
        private bool _applied;

        // Görünme süresi sayacı ve oyuncu giriş/çıkış bildirimi durumu.
        private DateTime _startedAt = DateTime.UtcNow;
        private readonly Dictionary<long, DateTime> _seenPlayers = new Dictionary<long, DateTime>();
        private readonly HashSet<string> _expiredSprites = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private DateTime _lastPlayerScan = DateTime.MinValue;
        private bool _playersPrimed;
        private Alert _alert;

        // Oyun her karede sınırlı sayıda çizim kabul eder; bu sınırı aşınca
        // sonraki dikdörtgenler sessizce düşer (resmin alt kısmı kaybolur).
        private const int MaxRectsPerFrame = 2600;
        private const int MaxRunsPerElement = 1500;
        private static int _frameRects;

        // HUD_COMPONENT_* kimlikleri: tek tek gizlenen oyun arayüzü parçaları.
        private const int HudWantedStars = 1;
        private const int HudVehicleName = 6;
        private const int HudAreaName = 7;
        private const int HudVehicleClass = 8;
        private const int HudStreetName = 9;
        private const int HudHelpText = 10;

        /// <summary>Launcher oturum açtıysa true (session-active.flag).</summary>
        private static bool SessionActive()
        {
            try
            {
                var data = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                return File.Exists(Path.Combine(data, "GTACoopRemake", "session-active.flag"));
            }
            catch { return false; }
        }

        public void Tick(bool menuVisible)
        {
            try
            {
                _frameRects = 0;
                Reload();

                // HUD yalnızca sunucu oturumunda görünür: normal (tek oyunculu)
                // oyunda hiçbir şey çizilmez, oyunun kendi arayüzü kalır.
                if (!SessionActive())
                {
                    Restore();
                    return;
                }

                // Özellik kapalı (varsayılan) ya da HUD kapatıldı: oyunun kendi
                // arayüzü aynen geri gelir, hiçbir şey gizli kalmaz.
                if (_layout == null || !_layout.enabled)
                {
                    Restore();
                    return;
                }

                HandleKeys();

                // HUD gizliyken ya da launcher menüsü açıkken oyun arayüzüne
                // dokunulmaz; yalnızca "her zaman görünür" işaretli öğeler çizilir.
                var hudHidden = menuVisible || !_hudVisible;
                if (hudHidden)
                {
                    // HUD gizlenince daha önce çizilmiş PNG'ler ekranda kalmasın.
                    SetSpritesEnabled(false);
                    Restore();
                }
                else
                {
                    ApplyGameHudHiding();
                }

                var elements = _layout.elements;
                if (elements == null) return;

                var elapsed = (DateTime.UtcNow - _startedAt).TotalSeconds;

                foreach (var element in elements)
                {
                    if (element == null) continue;
                    if (hudHidden && !element.AlwaysVisible) continue;

                    // "Kaç saniye görünsün" süresi dolduysa öğe oturum boyunca
                    // bir daha çizilmez; resimse sprite'ı da kapatılır.
                    if (element.visibleSeconds > 0 && elapsed >= element.visibleSeconds)
                    {
                        if (!string.IsNullOrWhiteSpace(element.png))
                        {
                            _expiredSprites.Add(element.png);
                            SetSpriteEnabled(element.png, false);
                        }

                        continue;
                    }

                    // Tek bir bozuk öğe diğerlerini (ve oyunu) etkilemesin.
                    try
                    {
                        Draw(element);
                    }
                    catch
                    {
                        // Sonraki karede yeniden denenir.
                    }
                }

                if (!hudHidden) TickAlerts();
            }
            catch
            {
                // Çizim hatası oyunu kilitlemesin; sonraki karede yeniden denenir.
            }
        }

        /// <summary>Oyunun kendi arayüzünü seçeneklere göre gizler.</summary>
        private void ApplyGameHudHiding()
        {
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

        /// <summary>Oyuncu giriş/çıkış bildirimlerini izler (resim + isteğe bağlı ses).</summary>
        private void TickAlerts()
        {
            DrawAlert();

            var config = _layout?.notifications;
            if (config == null || !config.enabled) return;
            if (DateTime.UtcNow < _lastPlayerScan.AddMilliseconds(400)) return;
            _lastPlayerScan = DateTime.UtcNow;

            var opponents = Main.Opponents;
            if (opponents == null) return;

            List<long> current;
            try
            {
                lock (opponents) current = new List<long>(opponents.Keys);
            }
            catch
            {
                return;
            }

            // Sunucuya ilk girişte mevcut oyuncular "yeni gelen" sayılmasın.
            if (!_playersPrimed)
            {
                _playersPrimed = true;
                _seenPlayers.Clear();
                foreach (var key in current) _seenPlayers[key] = DateTime.UtcNow;
                return;
            }

            var joined = false;
            var left = false;

            foreach (var key in current)
            {
                if (_seenPlayers.ContainsKey(key)) continue;
                _seenPlayers[key] = DateTime.UtcNow;
                joined = true;
            }

            foreach (var key in _seenPlayers.Keys.ToList())
            {
                if (current.Contains(key)) continue;
                _seenPlayers.Remove(key);
                left = true;
            }

            if (joined) ShowAlert(config.joinPng, config.joinSeconds, config.joinSound, config.fadeSeconds);
            else if (left) ShowAlert(config.leavePng, config.leaveSeconds, config.leaveSound, config.fadeSeconds);
        }

        private void ShowAlert(string png, double seconds, string sound, double fade)
        {
            if (string.IsNullOrWhiteSpace(png)) return;
            if (seconds <= 0) seconds = 4;
            if (fade <= 0) fade = 0.6;

            _alert = new Alert
            {
                Png = png,
                Started = DateTime.UtcNow,
                Seconds = seconds,
                Fade = fade
            };

            PlaySound(sound);
        }

        /// <summary>Bildirim resmini seçilen süre boyunca yumuşak geçişle gösterir.</summary>
        private void DrawAlert()
        {
            var alert = _alert;
            if (alert == null) return;

            var elapsed = (DateTime.UtcNow - alert.Started).TotalSeconds;
            if (elapsed >= alert.Seconds)
            {
                _alert = null;
                return;
            }

            var alpha = 1.0;
            if (alert.Fade > 0)
            {
                if (elapsed < alert.Fade) alpha = elapsed / alert.Fade;
                else if (elapsed > alert.Seconds - alert.Fade)
                    alpha = Math.Max(0, (alert.Seconds - elapsed) / alert.Fade);
            }

            const float width = 0.36f;
            const float height = 0.20f;
            DrawSprite(alert.Png, (1f - width) / 2f, 0.10f, width, height, (float)alpha);
        }

        /// <summary>Seçilen ses dosyasını (wav/mp3) oyunu kilitlemeden çalar.</summary>
        private static void PlaySound(string file)
        {
            if (string.IsNullOrWhiteSpace(file)) return;

            var path = Path.Combine(AssetsPath, file);
            if (!File.Exists(path)) return;

            try
            {
                var reader = new NAudio.Wave.AudioFileReader(path);
                var output = new NAudio.Wave.WaveOutEvent();
                output.Init(reader);
                output.PlaybackStopped += (_, _) =>
                {
                    try { output.Dispose(); reader.Dispose(); } catch { }
                };
                output.Play();
            }
            catch
            {
                // Ses aygıtı yoksa bildirim sessiz gösterilir.
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

                var layout = JsonConvert.DeserializeObject<Layout>(File.ReadAllText(LayoutPath));
                if (layout?.elements != null)
                {
                    foreach (var element in layout.elements)
                        if (element != null) Prepare(element);
                }

                _layout = layout;
                _stamp = stamp;

                // Yeni yerleşim: süre sayaçları ve önbellekler baştan başlar.
                _startedAt = DateTime.UtcNow;
                _expiredSprites.Clear();
                _seenPlayers.Clear();
                _playersPrimed = false;
                _alert = null;
            }
            catch
            {
                // Dosya yazılırken okunmuş olabilir; sonraki denemede alınır.
            }
        }

        /// <summary>Kaydedilmiş piksel ızgarasını hücre dizisine çevirir (bir kez).</summary>
        private static Cell[] ParseCells(Element element)
        {
            if (element.cols <= 0 || element.rows <= 0 || string.IsNullOrEmpty(element.pixels))
                return null;

            var cells = new Cell[element.cols * element.rows];
            var rows = element.pixels.Split(';');

            for (var r = 0; r < rows.Length && r < element.rows; r++)
            {
                var line = rows[r];
                if (line.Length == 0) continue;

                var parts = line.Split(',');
                for (var c = 0; c < parts.Length && c < element.cols; c++)
                {
                    var cell = parts[c];
                    if (cell.Length != 8) continue;
                    if (!byte.TryParse(cell.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var red)) continue;
                    if (!byte.TryParse(cell.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var green)) continue;
                    if (!byte.TryParse(cell.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var blue)) continue;
                    if (!byte.TryParse(cell.Substring(6, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var alpha)) continue;

                    cells[r * element.cols + c] = new Cell { R = red, G = green, B = blue, A = alpha };
                }
            }

            return cells;
        }

        /// <summary>
        /// Piksel ızgarasını çizime hazırlar: yatayda aynı renkli hücreler tek
        /// dikdörtgende birleştirilir. Izgara 5000'den fazla hücre olabildiği için
        /// bu olmadan oyun çizim sınırına takılıyor ve resmin altı görünmüyordu.
        /// </summary>
        private static void Prepare(Element element)
        {
            element.Runs = null;
            element.RunCols = 1;
            element.RunRows = 1;

            var cells = ParseCells(element);
            if (cells == null) return;

            var cols = element.cols;
            var rows = element.rows;
            List<Run>? last = null;

            for (var pass = 0; pass < 8; pass++)
            {
                var runs = Compress(cells, cols, rows);
                last = runs;

                if (runs.Count <= MaxRunsPerElement)
                {
                    element.Runs = runs.ToArray();
                    element.RunCols = cols;
                    element.RunRows = rows;
                    return;
                }

                if (cols <= 2 && rows <= 2) break;

                cells = Downsample(cells, cols, rows, out var nextCols, out var nextRows);
                cols = nextCols;
                rows = nextRows;
            }

            // Sınıra inilemediyse son sürüm kullanılır; resim yine tam görünür.
            element.Runs = last?.ToArray() ?? Array.Empty<Run>();
            element.RunCols = cols;
            element.RunRows = rows;
        }

        /// <summary>Bir satırdaki aynı renkli komşu hücreleri tek çizime birleştirir.</summary>
        private static List<Run> Compress(Cell[] cells, int cols, int rows)
        {
            var runs = new List<Run>(cols * 2);

            for (var r = 0; r < rows; r++)
            {
                var c = 0;
                while (c < cols)
                {
                    var first = cells[r * cols + c];
                    if (first.A < 8)
                    {
                        c++;
                        continue;
                    }

                    var start = c;
                    c++;

                    while (c < cols)
                    {
                        var next = cells[r * cols + c];
                        if (next.A < 8) break;
                        if (next.R != first.R || next.G != first.G || next.B != first.B || next.A != first.A) break;
                        c++;
                    }

                    runs.Add(new Run
                    {
                        Col = start,
                        Row = r,
                        Len = c - start,
                        R = first.R,
                        G = first.G,
                        B = first.B,
                        A = first.A
                    });
                }
            }

            return runs;
        }

        /// <summary>Izgarayı yarıya indirir (alfa ağırlıklı ortalama).</summary>
        private static Cell[] Downsample(Cell[] source, int cols, int rows, out int newCols, out int newRows)
        {
            newCols = Math.Max(1, (cols + 1) / 2);
            newRows = Math.Max(1, (rows + 1) / 2);

            var target = new Cell[newCols * newRows];

            for (var y = 0; y < newRows; y++)
            {
                for (var x = 0; x < newCols; x++)
                {
                    var alphaSum = 0;
                    var weight = 0;
                    var red = 0;
                    var green = 0;
                    var blue = 0;
                    var count = 0;

                    for (var dy = 0; dy < 2; dy++)
                    {
                        var sy = y * 2 + dy;
                        if (sy >= rows) continue;

                        for (var dx = 0; dx < 2; dx++)
                        {
                            var sx = x * 2 + dx;
                            if (sx >= cols) continue;

                            var cell = source[sy * cols + sx];
                            alphaSum += cell.A;
                            weight += cell.A;
                            red += cell.R * cell.A;
                            green += cell.G * cell.A;
                            blue += cell.B * cell.A;
                            count++;
                        }
                    }

                    if (count == 0 || alphaSum == 0 || weight == 0) continue;

                    target[y * newCols + x] = new Cell
                    {
                        R = (byte)Math.Min(255, red / weight),
                        G = (byte)Math.Min(255, green / weight),
                        B = (byte)Math.Min(255, blue / weight),
                        A = (byte)Math.Min(255, alphaSum / count)
                    };
                }
            }

            return target;
        }

        /// <summary>
        /// Kısayolları kenar algılamayla işler: tuş basılı tutulduğu sürece
        /// eylem bir kez çalışır. (Önceki sürüm her karede Thread.Sleep
        /// çağırdığı için oyunda kısa takılmalar oluyordu.)
        /// </summary>
        private void HandleKeys()
        {
            if (_layout?.shortcuts == null) return;

            foreach (var shortcut in _layout.shortcuts)
            {
                if (shortcut == null || string.IsNullOrWhiteSpace(shortcut.key)) continue;
                if (!Enum.TryParse(shortcut.key, true, out Keys key) || key == Keys.None) continue;

                var down = Game.IsKeyPressed(key);
                var wasDown = _heldKeys.Contains(key);

                if (down && !wasDown)
                {
                    _heldKeys.Add(key);
                    RunAction(shortcut.action);
                }
                else if (!down && wasDown)
                {
                    _heldKeys.Remove(key);
                }
            }
        }

        private void RunAction(string action)
        {
            switch ((action ?? "").ToLowerInvariant())
            {
                case "toggle": _hudVisible = !_hudVisible; break;
                case "hide": _hudVisible = false; break;
                case "radar": _radarVisible = !_radarVisible; break;
                case "players": ShowPlayerList(); break;
                case "voice": ToggleVoice(); break;
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
                Function.Call(Hash.SET_TEXT_CENTRE, false);
                Function.Call(Hash.SET_TEXT_RIGHT_JUSTIFY, false);
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
            var w = (float)Math.Max(0.002, e.w);
            var h = (float)Math.Max(0.002, e.h);
            var thickness = (float)Math.Max(0.0015, e.thickness);
            var color = ParseColor(e.color, 255);

            switch ((e.type ?? "").ToLowerInvariant())
            {
                case "panel":
                    Rect(x, y, w, h, ParseColor(e.color, (int)Math.Round(255 * Clamp01(e.opacity))));
                    break;

                case "outline":
                    RectOutline(x, y, w, h, thickness, color);
                    break;

                case "circle":
                    Circle(x, y, w, h, thickness, color);
                    break;

                case "line":
                    Line(x, y, w, h, thickness, color);
                    break;

                case "gradient":
                    Gradient(x, y, w, h, ParseColor(e.color, (int)Math.Round(255 * Clamp01(e.opacity))));
                    break;

                case "text":
                    Text(e.text ?? "", x, y, w, h, (float)Math.Max(0.12, e.fontSize), color, e.align);
                    break;

                case "clock":
                {
                    var hour = Function.Call<int>(Hash.GET_CLOCK_HOURS);
                    var minute = Function.Call<int>(Hash.GET_CLOCK_MINUTES);
                    Text($"{hour:00}:{minute:00}", x, y, w, h, (float)Math.Max(0.12, e.fontSize), color, e.align);
                    break;
                }

                case "ping":
                    Text($"{Main.Latency * 1000f:0} ms", x, y, w, h, (float)Math.Max(0.12, e.fontSize), color, e.align);
                    break;

                case "fps":
                    Text(Game.FPS + " FPS", x, y, w, h, (float)Math.Max(0.12, e.fontSize), color, e.align);
                    break;

                case "players":
                    DrawPlayers(e, x, y, w, h);
                    break;

                case "bar":
                    DrawBar(e, x, y, w, h);
                    break;

                case "speed":
                    Text($"{SpeedKmh():0} km/s", x, y, w, h, (float)Math.Max(0.12, e.fontSize), color, e.align);
                    break;

                case "weapon":
                    Text(WeaponText(), x, y, w, h, (float)Math.Max(0.12, e.fontSize), color, e.align);
                    break;

                case "compass":
                    Text(CompassText(), x, y, w, h, (float)Math.Max(0.12, e.fontSize), color, e.align);
                    break;

                case "zone":
                    Text(ZoneText(), x, y, w, h, (float)Math.Max(0.12, e.fontSize), color, e.align);
                    break;

                case "coords":
                {
                    var p = Game.Player.Character.Position;
                    Text($"{p.X:0}, {p.Y:0}, {p.Z:0}", x, y, w, h, (float)Math.Max(0.12, e.fontSize), color, e.align);
                    break;
                }

                case "image":
                    if (!TryDrawSprite(e, x, y, w, h)) DrawPixels(e, x, y, w, h);
                    break;
            }
        }

        // ------------------------------------------------------------- şekiller

        private static void RectOutline(float x, float y, float w, float h, float thickness, System.Drawing.Color color)
        {
            var t = Math.Min(thickness, Math.Min(w, h) / 2f);
            Rect(x, y, w, t, color);
            Rect(x, y + h - t, w, t, color);
            Rect(x, y + t, t, Math.Max(0.0005f, h - 2 * t), color);
            Rect(x + w - t, y + t, t, Math.Max(0.0005f, h - 2 * t), color);
        }

        /// <summary>Daire: dolgu için yatay şeritler, halka için çevre üzerinde noktalar.</summary>
        private static void Circle(float x, float y, float w, float h, float thickness, System.Drawing.Color color)
        {
            var rx = w / 2f;
            var ry = h / 2f;
            var cx = x + rx;
            var cy = y + ry;

            // Kalınlık yarıçapa yakınsa dolgulu daire çiz.
            if (thickness * 2f >= Math.Min(rx, ry))
            {
                const int strips = 56;
                var step = (2f * ry) / strips;

                for (var i = 0; i < strips; i++)
                {
                    var t = (i + 0.5f) / strips;
                    var normalized = t * 2f - 1f;
                    var half = rx * (float)Math.Sqrt(Math.Max(0.0, 1.0 - normalized * normalized));

                    if (half <= 0.0008f) continue;
                    Rect(cx - half, cy - ry + i * step, half * 2f, step, color);
                }

                return;
            }

            var segments = Math.Max(48, (int)Math.Min(240, (rx + ry) * 3.14159f / Math.Max(0.0015f, thickness)));
            var size = thickness * 1.25f;

            for (var i = 0; i < segments; i++)
            {
                var angle = i * 2.0 * Math.PI / segments;
                var px = cx + rx * (float)Math.Cos(angle);
                var py = cy + ry * (float)Math.Sin(angle);
                Rect(px - size / 2f, py - size / 2f, size, size, color);
            }
        }

        /// <summary>Çizgi: kutunun sol üst köşesinden sağ alt köşesine.</summary>
        private static void Line(float x, float y, float w, float h, float thickness, System.Drawing.Color color)
        {
            var length = (float)Math.Sqrt(w * w + h * h);
            var segments = Math.Max(2, (int)Math.Min(240, length / Math.Max(0.0015f, thickness)));

            for (var i = 0; i <= segments; i++)
            {
                var t = (float)i / segments;
                var px = x + w * t;
                var py = y + h * t;
                Rect(px - thickness / 2f, py - thickness / 2f, thickness, thickness, color);
            }
        }

        /// <summary>Gradyan: üstte renk, altta saydam (yumuşak geçişli panel).</summary>
        private static void Gradient(float x, float y, float w, float h, System.Drawing.Color color)
        {
            const int strips = 40;
            var step = h / strips;

            for (var i = 0; i < strips; i++)
            {
                var factor = 1.0 - (double)i / strips;
                var alpha = (int)Math.Round(color.A * factor * factor);
                if (alpha <= 0) continue;

                Rect(x, y + i * step, w, step * 1.02f,
                    System.Drawing.Color.FromArgb(Math.Min(255, alpha), color.R, color.G, color.B));
            }
        }

        // ------------------------------------------------------------- bilgi öğeleri

        private static float SpeedKmh()
        {
            try
            {
                var ped = Game.Player.Character;
                var handle = ped.IsInVehicle() ? ped.CurrentVehicle.Handle : ped.Handle;
                return Function.Call<float>(Hash.GET_ENTITY_SPEED, handle) * 3.6f;
            }
            catch
            {
                return 0f;
            }
        }

        private static string WeaponText()
        {
            try
            {
                var ped = Game.Player.Character;
                var raw = Function.Call<int>(Hash.GET_SELECTED_PED_WEAPON, ped.Handle);
                var hash = unchecked((uint)raw);
                if (hash == 0) return "Yumruk";

                var ammo = Function.Call<int>(Hash.GET_AMMO_IN_PED_WEAPON, ped.Handle, hash);
                var name = ((WeaponHash)hash).ToString();
                return ammo > 0 ? $"{name}  {ammo}" : name;
            }
            catch
            {
                return "";
            }
        }

        private static string CompassText()
        {
            try
            {
                var heading = Function.Call<float>(Hash.GET_ENTITY_HEADING, Game.Player.Character.Handle);
                var dirs = new[] { "K", "KD", "D", "GD", "G", "GB", "B", "KB" };
                var index = (int)Math.Round(heading / 45.0) % 8;
                if (index < 0) index += 8;
                return $"{dirs[index]}  {heading:0}°";
            }
            catch
            {
                return "";
            }
        }

        private static string ZoneText()
        {
            try
            {
                var position = Game.Player.Character.Position;
                var zone = World.GetZoneLocalizedName(position);
                var street = World.GetStreetName(position);

                if (!string.IsNullOrWhiteSpace(zone) && !string.IsNullOrWhiteSpace(street))
                    return zone + " · " + street;
                return string.IsNullOrWhiteSpace(zone) ? street : zone;
            }
            catch
            {
                return "";
            }
        }

        // ------------------------------------------------------------- liste/çubuk/resim

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

        /// <summary>
        /// PNG'yi oyunun kendi doku çizicisiyle (CustomSprite) çizer: piksel piksel
        /// ızgara yerine tek bir sprite, yani resim tam ve keskin görünür.
        /// SHVDN sürümü CustomSprite içermiyorsa false döner.
        /// </summary>
        private static bool TryDrawSprite(Element e, float x, float y, float w, float h)
            => DrawSprite(e.png, x, y, w, h, 1f);

        /// <summary>PNG'yi verilen saydamlıkla çizer (bildirimlerde yumuşak geçiş).</summary>
        private static bool DrawSprite(string name, float x, float y, float w, float h, float alpha)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            if (SpriteFailed.Contains(name)) return false;
            if (alpha <= 0.01f) return false;

            if (!SpriteCache.TryGetValue(name, out var image))
            {
                image = CreateSprite(name);
                SpriteCache[name] = image;
                if (image == null) SpriteFailed.Add(name);
            }

            if (image == null) return false;

            try
            {
                // SHVDN konum/boyutu 1080p piksel biriminde bekler; ekran
                // oranlarımızı o birime çeviriyoruz.
                var width = GTA.UI.Screen.Width;
                var height = GTA.UI.Screen.Height;

                image.Position?.SetValue(image.Instance,
                    new System.Drawing.PointF(x * width, y * height));
                image.Size?.SetValue(image.Instance,
                    new System.Drawing.SizeF(w * width, h * height));
                image.Color?.SetValue(image.Instance, System.Drawing.Color.FromArgb(
                    (int)Math.Round(Math.Max(0, Math.Min(1, alpha)) * 255), 255, 255, 255));
                image.Enabled?.SetValue(image.Instance, true);
                image.Draw?.Invoke(image.Instance, null);
                return true;
            }
            catch
            {
                SpriteFailed.Add(name);
                return false;
            }
        }

        /// <summary>Tüm PNG'leri anında gizler (F9/ara menü); çizim sırasında yeniden açılır.</summary>
        private static void SetSpritesEnabled(bool enabled)
        {
            foreach (var image in SpriteCache.Values)
            {
                if (image == null) continue;
                try { image.Enabled?.SetValue(image.Instance, enabled); } catch { }
            }
        }

        private static void SetSpriteEnabled(string name, bool enabled)
        {
            if (!SpriteCache.TryGetValue(name, out var image) || image == null) return;
            try { image.Enabled?.SetValue(image.Instance, enabled); } catch { }
        }

        private static SpriteImage? CreateSprite(string png)
        {
            try
            {
                var path = Path.Combine(AssetsPath, png);
                if (!File.Exists(path)) return null;

                var type = typeof(Game).Assembly.GetType("GTA.UI.CustomSprite");
                if (type == null) return null;

                var ctor = type.GetConstructor(new[]
                {
                    typeof(string), typeof(System.Drawing.SizeF), typeof(System.Drawing.PointF),
                    typeof(System.Drawing.Color)
                });

                if (ctor == null) return null;

                var instance = ctor.Invoke(new object[]
                {
                    path, new System.Drawing.SizeF(1f, 1f), new System.Drawing.PointF(0f, 0f),
                    System.Drawing.Color.White
                });

                return new SpriteImage
                {
                    Instance = instance,
                    Position = type.GetProperty("Position"),
                    Size = type.GetProperty("Size"),
                    Enabled = type.GetProperty("Enabled"),
                    Color = type.GetProperty("Color"),
                    Draw = type.GetMethod("Draw", Type.EmptyTypes)
                };
            }
            catch
            {
                return null;
            }
        }

        /// <summary>Yedek çizim: birleştirilmiş renk şeritleri (hücre hücre değil).</summary>
        private static void DrawPixels(Element e, float x, float y, float w, float h)
        {
            var runs = e.Runs;
            if (runs == null || runs.Length == 0) return;
            if (e.RunCols <= 0 || e.RunRows <= 0) return;

            var cellW = w / e.RunCols;
            var cellH = h / e.RunRows;

            foreach (var run in runs)
            {
                if (_frameRects >= MaxRectsPerFrame) return;

                Rect(x + run.Col * cellW, y + run.Row * cellH, run.Len * cellW, cellH,
                    System.Drawing.Color.FromArgb(run.A, run.R, run.G, run.B));
            }
        }

        private static void Rect(float x, float y, float w, float h, System.Drawing.Color color)
        {
            _frameRects++;

            Function.Call(Hash.DRAW_RECT, x + w / 2f, y + h / 2f, w, h,
                color.R, color.G, color.B, color.A, false);
        }

        private static void Text(string text, float x, float y, float w, float h, float scale,
            System.Drawing.Color color, string align)
        {
            if (string.IsNullOrEmpty(text)) return;

            var mode = (align ?? "left").ToLowerInvariant();

            Function.Call(Hash.SET_TEXT_FONT, 0);
            Function.Call(Hash.SET_TEXT_SCALE, scale, scale);
            Function.Call(Hash.SET_TEXT_COLOUR, color.R, color.G, color.B, color.A);
            Function.Call(Hash.SET_TEXT_DROPSHADOW, 0, 0, 0, 0, 200);
            Function.Call(Hash.SET_TEXT_EDGE, 1, 0, 0, 0, 200);
            Function.Call(Hash.SET_TEXT_WRAP, x, x + w);

            // Bu iki ayar oyun genelinde yapışkandır; her çizimde sıfırlanmazsa
            // sonraki yazılar da sağa yaslı/ortalanmış kalır.
            Function.Call(Hash.SET_TEXT_CENTRE, mode == "center");
            Function.Call(Hash.SET_TEXT_RIGHT_JUSTIFY, mode == "right");

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
