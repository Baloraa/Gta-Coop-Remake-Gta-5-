using System;
using System.IO;
using GTA;
using GTA.Native;
using Newtonsoft.Json;

namespace GTACoOp
{
    /// <summary>Launcher'ın scripts\GTACoop-world.json dosyasından okunur.</summary>
    public sealed class ClientWorldConfig
    {
        public string ServerName { get; set; }
        public string TimeMode { get; set; }
        public int TimeHour { get; set; }
        public string Weather { get; set; }
        public DateTime GeneratedUtc { get; set; }
    }

    /// <summary>
    /// Sunucunun seçtiği saat ayarını uygular. Mod "realtime" ise oyun saati
    /// bilgisayarın saatini izler; "fixed" ise seçilen saate sabitlenir;
    /// boş/"off" ise hiçbir şey yapılmaz ve önceki zorlama temizlenir.
    ///
    /// Ayar dosya üzerinden gelir: ağ protokolü değişmez, sunucu eski
    /// istemcilerle uyumlu kalır.
    /// </summary>
    internal static class WorldTime
    {
        private static ClientWorldConfig _config;
        private static int _reloadAt;
        private static int _applyAt;
        private static int _weatherAt;
        private static bool _applied;
        private static string _appliedWeather = "";

        public static string ConfigPath
        {
            get { return Path.Combine(Program.Location, "GTACoop-world.json"); }
        }

        public static void Process()
        {
            // Sunucuya bağlı değilken saat zorlanmaz: tek oyunculu oyun ve
            // menüdeki saat normal akışında kalır.
            if (!Main.IsOnServer())
            {
                Clear();
                return;
            }

            var now = Game.GameTime;

            if (now >= _reloadAt)
            {
                _reloadAt = now + 3000;
                _config = Load();
            }

            ApplyWeather(now);

            var mode = _config == null ? null : _config.TimeMode;
            if (string.IsNullOrWhiteSpace(mode) ||
                string.Equals(mode, "off", StringComparison.OrdinalIgnoreCase))
            {
                Clear();
                return;
            }

            if (now < _applyAt) return;
            _applyAt = now + 10000;

            try
            {
                if (string.Equals(mode, "realtime", StringComparison.OrdinalIgnoreCase))
                {
                    var t = DateTime.Now;
                    Function.Call(Hash.NETWORK_OVERRIDE_CLOCK_TIME, t.Hour, t.Minute, t.Second);
                }
                else
                {
                    var hour = Math.Min(23, Math.Max(0, _config.TimeHour));
                    Function.Call(Hash.NETWORK_OVERRIDE_CLOCK_TIME, hour, 0, 0);
                }

                _applied = true;
            }
            catch
            {
                // Native başarısız olursa oyun saati normal akışında kalır.
            }
        }

        /// <summary>
        /// Sunucunun hava kilidini uygular. "dynamic" ise zorlama kaldırılır ve
        /// oyun kendi hava döngüsüne döner.
        /// </summary>
        private static void ApplyWeather(int now)
        {
            var weather = _config == null ? null : _config.Weather;
            if (string.IsNullOrWhiteSpace(weather)) weather = "dynamic";

            if (string.Equals(weather, _appliedWeather, StringComparison.OrdinalIgnoreCase)) return;
            if (now < _weatherAt) return;
            _weatherAt = now + 3000;

            try
            {
                if (string.Equals(weather, "dynamic", StringComparison.OrdinalIgnoreCase))
                {
                    // CLEAR_OVERRIDE_WEATHER (isim sabiti eski SHVDN sürümlerinde yok)
                    Function.Call((Hash)0x338D2E3477711050);
                }
                else
                {
                    // SET_WEATHER_TYPE_NOW_PERSISTENT
                    Function.Call((Hash)0x29B487C359E19889, weather.ToUpperInvariant());
                }

                _appliedWeather = weather;
            }
            catch
            {
                // Geçersiz hava adı: oyunun kendi döngüsü devam eder.
            }
        }

        /// <summary>Zorlamayı kaldırır (ayar kapandığında veya oturum bitince).</summary>
        public static void Clear()
        {
            if (!_applied) return;
            _applied = false;

            try { Function.Call(Hash.NETWORK_CLEAR_CLOCK_TIME_OVERRIDE); } catch { }
        }

        private static ClientWorldConfig Load()
        {
            try
            {
                if (!File.Exists(ConfigPath)) return null;
                return JsonConvert.DeserializeObject<ClientWorldConfig>(File.ReadAllText(ConfigPath));
            }
            catch
            {
                return null;
            }
        }
    }
}
