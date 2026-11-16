using System;
using System.IO;
using System.Text.Json;

namespace GTACoop.PedSelector.App
{
    /// <summary>
    /// AddonPed+ arayüz dili. Varsayılan İngilizce; Türkçe seçilebilir.
    /// Seçim %LOCALAPPDATA%\GTACoop Ped Selector\settings.json içinde tutulur.
    /// </summary>
    internal static class L
    {
        public const string English = "en";
        public const string TurkishCode = "tr";

        public static string Language { get; private set; } = English;

        public static bool Turkish => Language == TurkishCode;

        /// <summary>İngilizce/Türkçe metin çiftinden geçerli dili döndürür.</summary>
        public static string T(string en, string tr) => Turkish ? tr : en;

        private sealed class Settings
        {
            public string Language { get; set; } = English;
        }

        private static string SettingsPath(string root) => Path.Combine(root, "settings.json");

        public static void Load(string root)
        {
            try
            {
                var path = SettingsPath(root);
                if (!File.Exists(path))
                {
                    Language = English;
                    return;
                }

                var settings = JsonSerializer.Deserialize<Settings>(File.ReadAllText(path));
                Language = string.Equals(settings?.Language, TurkishCode,
                    StringComparison.OrdinalIgnoreCase)
                    ? TurkishCode
                    : English;
            }
            catch
            {
                Language = English;
            }
        }

        public static void SetLanguage(string language, string root)
        {
            Language = string.Equals(language, TurkishCode, StringComparison.OrdinalIgnoreCase)
                ? TurkishCode
                : English;
            try
            {
                Directory.CreateDirectory(root);
                File.WriteAllText(SettingsPath(root),
                    JsonSerializer.Serialize(new Settings { Language = Language },
                        new JsonSerializerOptions { WriteIndented = true }));
            }
            catch
            {
                // Ayar yazılamazsa uygulama çalışmaya devam eder.
            }
        }
    }
}
