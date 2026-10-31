using System.Text.Json;
using System.Text.Json.Serialization;
using GTACoop.Rpf;

namespace GTACoop.Sync;

/// <summary>Sunucu eklentisi türleri.</summary>
public enum AddonType
{
    /// <summary>dlcpacks/&lt;ad&gt;: dlc.rpf içeren oyun paketi (araç, ped, harita).</summary>
    DlcPack,

    /// <summary>scripts/&lt;ad&gt;: ScriptHookVDotNet istemci scripti (dll + ini/xml gibi destek dosyaları).</summary>
    ClientScript,

    /// <summary>asi/&lt;ad&gt;: oyun kök klasörüne kurulan ASI eklentisi (.asi + yanındaki ini/xml vb.).</summary>
    AsiPlugin,

    /// <summary>files/&lt;ad&gt;: oyun klasörüne kopyalanan dosyalar (yalnızca scripts/ ve mods/GTACoop/).</summary>
    ClientFiles,

    /// <summary>oiv/&lt;ad&gt;: OpenIV .oiv paketi (güvenli alt küme; kendi klasöründe saklanır).</summary>
    OivPackage,

    /// <summary>plugins/&lt;ad&gt;: yalnızca sunucuda çalışan eklenti (istemciye indirilmez).</summary>
    ServerPlugin
}

/// <summary>Eklenti içindeki tek dosya.</summary>
public sealed class AddonFileInfo
{
    /// <summary>Eklenti klasörüne göreli yol ("/" ayraçlı).</summary>
    public string Path { get; set; } = "";
    public long Size { get; set; }
    public string Sha256 { get; set; } = "";
}

/// <summary>Sunucunun sunduğu tek eklenti.</summary>
public sealed class AddonInfo
{
    public string Id { get; set; } = "";
    public AddonType Type { get; set; }
    public string DisplayName { get; set; } = "";
    public string Description { get; set; } = "";
    public string Version { get; set; } = "";
    public bool Required { get; set; } = true;

    /// <summary>
    /// DlcPack: dlc.rpf'in bulunduğu klasörün eklenti köküne göreli yolu
    /// (alt klasör yoksa boş). Örn: "x64/dlcpacks/kfbmods1".
    /// </summary>
    public string RelativePackPath { get; set; } = "";

    public List<AddonFileInfo> Files { get; set; } = new();

    /// <summary>
    /// DlcPack: sunucunun istemci menüsünde gösterdiği addon pedler.
    /// Boşsa paket normal bir DLC paketidir.
    /// </summary>
    public List<AddonPedInfo> Peds { get; set; } = new();

    /// <summary>
    /// OivPackage: paketin güvenli biçimde yazacağı oyun içi hedefler ("/" ayraçlı).
    /// İndirmeden önce kullanıcı çakışmalarını tespit etmek için kullanılır.
    /// </summary>
    public List<string> OivTargets { get; set; } = new();

    /// <summary>OivPackage: dlclist.xml'e eklenecek paket adları.</summary>
    public List<string> OivDlclistEntries { get; set; } = new();

    /// <summary>OivPackage: güvenlik için uygulanmayacak işlem sayısı.</summary>
    public int OivUnsupportedCount { get; set; }

    /// <summary>Sunucu tarafında mod yöneticisi için benzersiz anahtar: "Tür:Id".</summary>
    [JsonIgnore]
    public string Key => $"{Type}:{Id}";

    [JsonIgnore]
    public long TotalSize => Files.Sum(f => f.Size);
}

/// <summary>Sunucu eklenti bildirimi (manifest).</summary>
public sealed class Manifest
{
    public int ManifestVersion { get; set; } = 1;
    public string ServerName { get; set; } = "";
    public string ServerId { get; set; } = "";
    public DateTime GeneratedUtc { get; set; } = DateTime.UtcNow;
    public int SyncPort { get; set; } = 4599;
    public List<AddonInfo> Addons { get; set; } = new();
}

/// <summary>İstemcide kurulu tek eklentinin kaydı.</summary>
public sealed class InstalledAddon
{
    public string Id { get; set; } = "";
    public AddonType Type { get; set; }
    public string Version { get; set; } = "";

    /// <summary>DlcPack için: dlcpacks altındaki klasör adı (gcs_ önekli).</summary>
    public string MountFolder { get; set; } = "";

    /// <summary>DlcPack için: dlclist.xml'e yazılan girdi.</summary>
    public string DlclistEntry { get; set; } = "";

    /// <summary>
    /// DlcPack için: klasör kopyalanmak yerine NTFS bağlantısı (junction) kullanıldıysa
    /// bağlantının hedefi (önbellek klasörü). Boşsa normal klasör olarak kurulmuştur.
    /// </summary>
    public string LinkTarget { get; set; } = "";

    /// <summary>Oyun köküne göre kurulan dosyaların listesi.</summary>
    public List<string> InstalledPaths { get; set; } = new();

    /// <summary>
    /// Kurulan dosyaların SHA-256 değerleri. Kaldırırken dosya kullanıcı tarafından
    /// değiştirildiyse silinmez; yalnızca bizim yazdığımız içerik kaldırılır.
    /// </summary>
    public Dictionary<string, string> InstalledHashes { get; set; } = new();

    /// <summary>OivPackage: bu paket tarafından eklenen dlclist girdileri.</summary>
    public List<string> DlclistEntries { get; set; } = new();

    /// <summary>
    /// OivPackage: mods klasöründeki RPF arşivlerinde yapılan dosya ekleme/
    /// değiştirme kayıtları. Kaldırma ve güncelleme geri alımı için kullanılır.
    /// </summary>
    public List<RpfEditRecord> RpfEdits { get; set; } = new();
}

/// <summary>
/// RPF arşivinde tek bir dosya değişikliğinin geri alınabilir kaydı.
/// Eski veri bloğu arşivden silinmediği için içerik yedeği tutulmaz.
/// </summary>
public sealed class RpfEditRecord
{
    /// <summary>Oyun köküne göre arşiv yolu (örn. mods/x64a.rpf).</summary>
    public string Archive { get; set; } = "";

    /// <summary>Arşiv içindeki dosya yolu ("\" ayraçlı).</summary>
    public string EntryPath { get; set; } = "";

    /// <summary>Değişiklikten önce girdi var mıydı?</summary>
    public bool WasPresent { get; set; }

    public bool IsResource { get; set; }
    public uint OriginalOffset { get; set; }
    public uint OriginalSize { get; set; }
    public uint OriginalUncompressedSize { get; set; }
    public uint OriginalSystemFlags { get; set; }
    public uint OriginalGraphicsFlags { get; set; }
    public bool OriginalEncrypted { get; set; }
    public RpfCompression OriginalCompression { get; set; } = RpfCompression.Unknown;

    /// <summary>Bizim yazdığımız içeriğin SHA-256 özeti (kullanıcı değişikliğini ayırt etmek için).</summary>
    public string InstalledHash { get; set; } = "";

    public string Key => Archive + "|" + EntryPath;
}

/// <summary>Bir sunucudan yapılan kurulumun durumu.</summary>
public sealed class SyncState
{
    public string ServerId { get; set; } = "";
    public string ServerName { get; set; } = "";
    public DateTime LastSyncUtc { get; set; }
    public List<InstalledAddon> Addons { get; set; } = new();
}

/// <summary>Launcher ayarları (settings.json).</summary>
public sealed class LauncherSettings
{
    public string GameFolder { get; set; } = "";
    public string Username { get; set; } = "Player";
    public string DataFolder { get; set; } = "";
    public List<ServerEntry> Servers { get; set; } = new();

    /// <summary>Arayüz dili: "tr" veya "en" (boş = henüz seçilmedi).</summary>
    public string Language { get; set; } = "";

    /// <summary>Oturum modu: oyun kapanınca sunucu modları otomatik kaldırılsın mı?</summary>
    public bool RemoveModsOnExit { get; set; } = true;

    /// <summary>
    /// Düşük gecikme profili: bağlantıda trafiği/ped'leri azaltır ve akışa
    /// alınan NPC sayısını düşürür. Bu bir semptom azaltma profilidir;
    /// çekirdek senkronizasyon protokolünü değiştirmez.
    /// </summary>
    public bool LowDesyncProfile { get; set; } = false;

    /// <summary>
    /// Sunucu Tarayıcı sekmesinin kullandığı GTACoOp master listesi.
    /// Boşsa yerleşik varsayılan adres kullanılır.
    /// </summary>
    public string ServerBrowserMasterUrl { get; set; } = "https://master.gtacoop.com";

    /// <summary>
    /// Kullanıcının seçtiği oyun başlatma dosyası. Boşsa otomatik seçim:
    /// PlayGTAV.exe -> GTA5_Enhanced.exe -> GTA5.exe.
    /// Oyun klasörü içindeyse göreli dosya adı olarak saklanır.
    /// </summary>
    public string LaunchExe { get; set; } = "";
}

/// <summary>Sunucu listesindeki kayıt.</summary>
public sealed class ServerEntry
{
    public string Name { get; set; } = "";
    public string Host { get; set; } = "127.0.0.1";
    public int GamePort { get; set; } = 4499;
    public int SyncPort { get; set; } = 4599;
    public string Password { get; set; } = "";
    public string SyncToken { get; set; } = "";
    public bool AutoConnect { get; set; } = true;

    /// <summary>
    /// Sunucuyu eklerken kullanılan kısa kod (varsa). Relay bilgisi eskirse
    /// bağlantı öncesinde bu kodla güncel relay uç noktaları yeniden alınır.
    /// </summary>
    public string ShortCode { get; set; } = "";

    // Otomatik relay (port yönlendirme/Radmin gerekmeden). Boşsa doğrudan bağlantı.
    public string RelayHost { get; set; } = "";
    public int RelaySyncPort { get; set; }
    public int RelayGamePort { get; set; }

    [JsonIgnore]
    public bool UsesRelay =>
        !string.IsNullOrWhiteSpace(RelayHost) && RelaySyncPort > 0 && RelayGamePort > 0;
}

public static class SyncJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static T? Load<T>(string path) where T : class
    {
        if (!File.Exists(path)) return null;
        try
        {
            return JsonSerializer.Deserialize<T>(File.ReadAllText(path), Options);
        }
        catch
        {
            return null;
        }
    }

    public static void Save<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(value, Options));
        File.Move(tmp, path, overwrite: true);
    }

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);
    public static T? Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json, Options);
}
