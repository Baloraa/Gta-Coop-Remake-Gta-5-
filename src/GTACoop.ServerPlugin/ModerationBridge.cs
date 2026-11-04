using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using GTAServer;
using GTAServer.PluginAPI.Events;
using GTAServer.ProtocolMessages;

namespace GTACoop.Remake.Plugin
{
    /// <summary>
    /// Sunucu yöneticisi (launcher) ile oyun sunucusu arasında dosya tabanlı köprü.
    ///
    /// - Bağlı oyuncular <c>gtacoop-players.json</c> dosyasına yazılır (launcher okur).
    /// - Launcher'ın bıraktığı <c>gtacoop-cmd-*.json</c> komutları uygulanır (at / mesaj / duyuru).
    /// - <c>gtacoop-moderation.json</c> içindeki yasak listesi ve beyaz liste denetlenir.
    ///
    /// Ağ protokolü değişmez; oyun dosyalarına dokunulmaz ve her hata yutulur ki
    /// bu köprü sunucuyu hiçbir koşulda düşürmesin.
    /// </summary>
    internal static class ModerationBridge
    {
        private const string RosterFile = "gtacoop-players.json";
        private const string SettingsFile = "gtacoop-moderation.json";
        private const string CommandPattern = "gtacoop-cmd-*.json";

        private static readonly JsonSerializerOptions Json = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };

        private static readonly Stopwatch Clock = Stopwatch.StartNew();
        private static GameServer _server;
        private static double _nextRoster;
        private static double _nextSettings;
        private static double _nextCommands;
        private static DateTime _settingsStamp = DateTime.MinValue;
        private static ModerationSettings _settings = new();
        private static string _lastRosterSignature = "";

        private static string BaseDir => AppDomain.CurrentDomain.BaseDirectory;

        public static void Attach(GameServer server)
        {
            if (server == null) return;
            _server = server;

            try
            {
                GameEvents.OnTick.Add(_ => Pump());
            }
            catch
            {
                // Sunucu eklenti zinciri kabul etmezse sessizce devre dışı kal.
            }
        }

        private static void Pump()
        {
            try
            {
                var now = Clock.Elapsed.TotalSeconds;

                if (now >= _nextCommands)
                {
                    _nextCommands = now + 1.0;
                    ProcessCommands();
                }

                if (now >= _nextSettings)
                {
                    _nextSettings = now + 2.0;
                    ReloadSettings();
                }

                if (now >= _nextRoster)
                {
                    _nextRoster = now + 1.5;
                    EnforceAccess();
                    WriteRoster();
                }
            }
            catch
            {
                // Köprü hatası sunucuyu etkilemez.
            }
        }

        // ------------------------------------------------------------------ oyuncular

        private static List<Client> Snapshot()
        {
            try
            {
                var clients = _server?.Clients;
                return clients == null ? new List<Client>() : clients.ToList();
            }
            catch
            {
                return new List<Client>();
            }
        }

        private static string NameOf(Client client)
        {
            if (client == null) return "Player";
            if (!string.IsNullOrWhiteSpace(client.DisplayName)) return client.DisplayName.Trim();
            if (!string.IsNullOrWhiteSpace(client.Name)) return client.Name.Trim();
            return "Player";
        }

        private static long IdOf(Client client)
        {
            try
            {
                return client?.NetConnection == null ? 0 : client.NetConnection.RemoteUniqueIdentifier;
            }
            catch
            {
                return 0;
            }
        }

        private static void WriteRoster()
        {
            var players = new List<RosterPlayer>();

            foreach (var client in Snapshot())
            {
                try
                {
                    var latency = client.Latency > 0 ? (int)Math.Round(client.Latency * 1000f) : 0;
                    players.Add(new RosterPlayer
                    {
                        Id = IdOf(client),
                        Name = NameOf(client),
                        LatencyMs = latency,
                        Health = client.Health
                    });
                }
                catch
                {
                    // Tek oyuncu okunamazsa listeyi bozmayalım.
                }
            }

            var signature = string.Join("|", players
                .OrderBy(p => p.Id)
                .Select(p => p.Id + ":" + p.Name + ":" + p.LatencyMs + ":" + p.Health));

            if (signature == _lastRosterSignature && File.Exists(Path.Combine(BaseDir, RosterFile)))
                return;

            _lastRosterSignature = signature;

            var document = new RosterDocument
            {
                UpdatedUtc = DateTime.UtcNow,
                Players = players
            };

            WriteAtomic(Path.Combine(BaseDir, RosterFile), JsonSerializer.Serialize(document, Json));
        }

        // ------------------------------------------------------------------ yasak / beyaz liste

        private static void ReloadSettings()
        {
            var path = Path.Combine(BaseDir, SettingsFile);
            try
            {
                if (!File.Exists(path))
                {
                    if (_settingsStamp != DateTime.MinValue)
                    {
                        _settings = new ModerationSettings();
                        _settingsStamp = DateTime.MinValue;
                    }
                    return;
                }

                var stamp = File.GetLastWriteTimeUtc(path);
                if (stamp == _settingsStamp) return;

                var parsed = JsonSerializer.Deserialize<ModerationSettings>(ReadShared(path), Json);
                _settings = parsed ?? new ModerationSettings();
                _settings.Banned ??= new List<string>();
                _settings.Whitelist ??= new List<string>();
                _settingsStamp = stamp;
            }
            catch
            {
                // Dosya yazılıyor olabilir; eski ayarlarla devam et.
            }
        }

        private static void EnforceAccess()
        {
            var banned = new HashSet<string>(_settings.Banned ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
            var whitelist = new HashSet<string>(_settings.Whitelist ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
            var whitelistOnly = _settings.WhitelistEnabled;

            if (banned.Count == 0 && !whitelistOnly) return;

            foreach (var client in Snapshot())
            {
                try
                {
                    var name = NameOf(client);
                    if (banned.Contains(name))
                    {
                        client.Kick("Bu oyuncu adı sunucudan yasaklandı.", false, null);
                        continue;
                    }

                    if (whitelistOnly && !whitelist.Contains(name))
                        client.Kick("Sunucu şu an yalnızca beyaz listedeki oyuncuları kabul ediyor.", false, null);
                }
                catch
                {
                    // Tek istemci hatası diğerlerini etkilemesin.
                }
            }
        }

        // ------------------------------------------------------------------ komutlar

        private static void ProcessCommands()
        {
            string[] files;
            try
            {
                files = Directory.GetFiles(BaseDir, CommandPattern);
            }
            catch
            {
                return;
            }

            foreach (var file in files)
            {
                try
                {
                    var command = JsonSerializer.Deserialize<ModerationCommand>(ReadShared(file), Json);
                    if (command != null) Apply(command);
                }
                catch
                {
                    // Bozuk komut dosyası yok sayılır.
                }
                finally
                {
                    try { File.Delete(file); } catch { }
                }
            }
        }

        private static void Apply(ModerationCommand command)
        {
            var action = (command.Action ?? "").Trim().ToLowerInvariant();
            if (action.Length == 0) return;

            if (action == "broadcast")
            {
                if (!string.IsNullOrWhiteSpace(command.Message))
                {
                    try { _server?.SendChatMessageToAll(command.Message); } catch { }
                }
                return;
            }

            var target = Find(command.TargetId, command.TargetName);
            if (target == null) return;

            try
            {
                if (action == "kick")
                {
                    var reason = string.IsNullOrWhiteSpace(command.Reason)
                        ? "Sunucu yöneticisi tarafından atıldınız."
                        : command.Reason;
                    target.Kick(reason, false, null);
                }
                else if (action == "message" && !string.IsNullOrWhiteSpace(command.Message))
                {
                    target.SendMessage(command.Message);
                }
            }
            catch
            {
                // Atma/mesaj gönderme başarısız olsa bile sunucu çalışmaya devam eder.
            }
        }

        private static Client Find(long id, string name)
        {
            var clients = Snapshot();

            if (id != 0)
            {
                foreach (var client in clients)
                {
                    if (IdOf(client) == id) return client;
                }
            }

            if (!string.IsNullOrWhiteSpace(name))
            {
                foreach (var client in clients)
                {
                    if (string.Equals(NameOf(client), name.Trim(), StringComparison.OrdinalIgnoreCase))
                        return client;
                }
            }

            return null;
        }

        // ------------------------------------------------------------------ dosya yardımcıları

        private static string ReadShared(string path)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                       FileShare.ReadWrite | FileShare.Delete))
            using (var reader = new StreamReader(stream, Encoding.UTF8))
            {
                return reader.ReadToEnd();
            }
        }

        private static void WriteAtomic(string path, string text)
        {
            var temp = path + ".tmp";
            File.WriteAllText(temp, text, new UTF8Encoding(false));
            File.Move(temp, path, true);
        }

        // ------------------------------------------------------------------ dosya biçimleri

        private sealed class RosterDocument
        {
            public DateTime UpdatedUtc { get; set; }
            public List<RosterPlayer> Players { get; set; } = new();
        }

        private sealed class RosterPlayer
        {
            public long Id { get; set; }
            public string Name { get; set; } = "";
            public int LatencyMs { get; set; }
            public int Health { get; set; }
        }

        private sealed class ModerationSettings
        {
            public List<string> Banned { get; set; } = new();
            public bool WhitelistEnabled { get; set; }
            public List<string> Whitelist { get; set; } = new();
        }

        private sealed class ModerationCommand
        {
            public string Action { get; set; } = "";
            public long TargetId { get; set; }
            public string TargetName { get; set; } = "";
            public string Reason { get; set; } = "";
            public string Message { get; set; } = "";
        }
    }
}
