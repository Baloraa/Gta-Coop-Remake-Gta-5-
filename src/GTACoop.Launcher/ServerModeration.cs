using System.Text.Json;
using GTACoop.Sync;

namespace GTACoop.Remake;

/// <summary>Sunucudaki tek oyuncu (gtacoop-players.json).</summary>
internal sealed class ModerationPlayer
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
    public int LatencyMs { get; set; }
    public int Health { get; set; }
}

/// <summary>Sunucu eklentisinin yazdığı canlı oyuncu listesi.</summary>
internal sealed class ModerationRoster
{
    public DateTime UpdatedUtc { get; set; }
    public List<ModerationPlayer> Players { get; set; } = new();
}

/// <summary>Yasak listesi ve beyaz liste ayarları (gtacoop-moderation.json).</summary>
internal sealed class ModerationSettings
{
    public List<string> Banned { get; set; } = new();
    public bool WhitelistEnabled { get; set; }
    public List<string> Whitelist { get; set; } = new();

    public ModerationSettings Clone() => new()
    {
        Banned = new List<string>(Banned),
        WhitelistEnabled = WhitelistEnabled,
        Whitelist = new List<string>(Whitelist)
    };
}

/// <summary>Launcher'ın sunucu eklentisine bıraktığı tek komut.</summary>
internal sealed class ModerationCommand
{
    public string Action { get; set; } = "";
    public long TargetId { get; set; }
    public string TargetName { get; set; } = "";
    public string Reason { get; set; } = "";
    public string Message { get; set; } = "";
}

/// <summary>
/// Sunucu klasöründeki JSON dosyaları üzerinden oyuncu listesini okur ve
/// at / yasakla / mesaj / beyaz liste komutlarını sunucu eklentisine bırakır.
/// Ağ protokolü ve oyun dosyaları değişmez.
/// </summary>
internal static class ServerModeration
{
    private const string RosterFile = "gtacoop-players.json";
    private const string SettingsFile = "gtacoop-moderation.json";
    private const string CommandPrefix = "gtacoop-cmd-";

    private static string Dir => AppEnv.ServerDir;

    public static string RosterPath => Path.Combine(Dir, RosterFile);
    public static string SettingsPath => Path.Combine(Dir, SettingsFile);

    public static ModerationRoster LoadRoster()
    {
        try
        {
            if (!File.Exists(RosterPath)) return new ModerationRoster();
            var roster = JsonSerializer.Deserialize<ModerationRoster>(ReadShared(RosterPath), SyncJson.Options);
            if (roster == null) return new ModerationRoster();
            roster.Players ??= new List<ModerationPlayer>();
            return roster;
        }
        catch
        {
            return new ModerationRoster();
        }
    }

    public static ModerationSettings LoadSettings()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return new ModerationSettings();
            var settings = JsonSerializer.Deserialize<ModerationSettings>(ReadShared(SettingsPath), SyncJson.Options)
                           ?? new ModerationSettings();
            settings.Banned ??= new List<string>();
            settings.Whitelist ??= new List<string>();
            return settings;
        }
        catch
        {
            return new ModerationSettings();
        }
    }

    public static void SaveSettings(ModerationSettings settings)
    {
        settings.Banned = settings.Banned
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Select(n => n.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToList();

        settings.Whitelist = settings.Whitelist
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Select(n => n.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToList();

        Directory.CreateDirectory(Dir);
        SyncJson.Save(SettingsPath, settings);
    }

    public static void Kick(ModerationPlayer player, string reason) =>
        Send(new ModerationCommand
        {
            Action = "kick",
            TargetId = player.Id,
            TargetName = player.Name,
            Reason = reason
        });

    public static void SendMessage(ModerationPlayer player, string message) =>
        Send(new ModerationCommand
        {
            Action = "message",
            TargetId = player.Id,
            TargetName = player.Name,
            Message = message
        });

    public static void Broadcast(string message) =>
        Send(new ModerationCommand { Action = "broadcast", Message = message });

    public static bool Ban(string name)
    {
        name = (name ?? "").Trim();
        if (name.Length == 0) return false;

        var settings = LoadSettings();
        if (settings.Banned.Any(b => string.Equals(b, name, StringComparison.OrdinalIgnoreCase)))
            return false;

        settings.Banned.Add(name);
        SaveSettings(settings);
        return true;
    }

    public static void Unban(string name)
    {
        var settings = LoadSettings();
        settings.Banned.RemoveAll(b => string.Equals(b, name, StringComparison.OrdinalIgnoreCase));
        SaveSettings(settings);
    }

    /// <summary>Eski komut dosyalarını temizler (sunucu kapalıyken birikmesin).</summary>
    public static void CleanupCommands()
    {
        try
        {
            foreach (var file in Directory.GetFiles(Dir, CommandPrefix + "*.json"))
            {
                if (DateTime.UtcNow - File.GetLastWriteTimeUtc(file) > TimeSpan.FromMinutes(2))
                    File.Delete(file);
            }
        }
        catch
        {
            // Temizlik kritik değil.
        }
    }

    private static void Send(ModerationCommand command)
    {
        Directory.CreateDirectory(Dir);
        var path = Path.Combine(Dir, CommandPrefix + Guid.NewGuid().ToString("N") + ".json");
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(command, SyncJson.Options));
        File.Move(temp, path, true);
    }

    private static string ReadShared(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
