using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace GTACoOp
{
    /// <summary>Launcher'ın scripts\GTACoop-addonpeds.json dosyasından okunur.</summary>
    public sealed class ClientAddonPedsConfig
    {
        public string ServerName { get; set; }
        public DateTime GeneratedUtc { get; set; }
        public List<ClientAddonPed> Peds { get; set; }
    }

    public sealed class ClientAddonPed
    {
        public string Model { get; set; }
        public string Name { get; set; }
        public string Kind { get; set; }
        public string Pack { get; set; }
        public int Health { get; set; }
    }

    internal static class AddonPedsClient
    {
        public static string ConfigPath
        {
            get { return Path.Combine(Program.Location, "GTACoop-addonpeds.json"); }
        }

        public static ClientAddonPedsConfig Load()
        {
            try
            {
                if (!File.Exists(ConfigPath)) return null;
                var json = File.ReadAllText(ConfigPath);
                var config = JsonConvert.DeserializeObject<ClientAddonPedsConfig>(json);
                if (config == null || config.Peds == null) return null;
                config.Peds.RemoveAll(p => p == null || string.IsNullOrWhiteSpace(p.Model));
                return config.Peds.Count == 0 ? null : config;
            }
            catch
            {
                return null;
            }
        }
    }
}
