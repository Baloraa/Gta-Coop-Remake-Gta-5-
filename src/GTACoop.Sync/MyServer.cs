using System.Diagnostics;
using System.Text;
using System.Xml.Linq;

namespace GTACoop.Sync;

/// <summary>Kendi sunucumuzun ayarları.</summary>
public sealed class MyServerSettings
{
    public string ServerName { get; set; } = "GTACoop Sunucum";
    public int GamePort { get; set; } = 4499;
    public int SyncPort { get; set; } = 4599;
    public int MaxClients { get; set; } = 16;
    public string Password { get; set; } = "";
    public string Motd { get; set; } = "GTACoop Remake sunucusuna hos geldiniz!";
    public string GamemodeName { get; set; } = "none";
    public string SyncToken { get; set; } = "";

    /// <summary>
    /// Sunucu modu (VDS): sunucu, launcher kapandıktan sonra da çalışmaya devam
    /// eder. İsteğe bağlıdır ve varsayılan olarak kapalıdır.
    /// </summary>
    public bool ServerMode { get; set; }

    /// <summary>Sunucu modunda süreç başına RAM sınırı (MB). 0 = sınırsız.</summary>
    public int ServerModeRamMb { get; set; } = 2048;

    /// <summary>Windows açılışında sunucu modunu otomatik başlat.</summary>
    public bool ServerModeAutostart { get; set; }

    /// <summary>
    /// Sunucunun varsayılan saat ayarı: "off" (oyun saati), "realtime"
    /// (bilgisayar saati) veya "fixed" (TimeHour). İstemcilere manifest
    /// üzerinden iletilir.
    /// </summary>
    public string TimeMode { get; set; } = "off";

    /// <summary>"fixed" modunda kullanılacak saat (0-23).</summary>
    public int TimeHour { get; set; } = 12;

    /// <summary>
    /// Sunucunun hava durumu kilidi: "dynamic" (varsayılan) veya oyunun hava
    /// adı ("EXTRASUNNY", "RAIN", "THUNDER", ...). Boyunca tüm istemcilere
    /// manifest ile bildirilir.
    /// </summary>
    public string Weather { get; set; } = "dynamic";

    /// <summary>
    /// İsteğe bağlı kısa katılım kodu (ör. "Gel.com"). Boşsa kısa kod yayını kapalıdır.
    /// Uzun GTC1 kodu bu ayardan bağımsız olarak çalışır.
    /// </summary>
    public string ShortCode { get; set; } = "";

    /// <summary>
    /// Otomatik relay için kullanılacak sunucu. "host" veya "host:port"
    /// biçiminde olabilir; boşsa varsayılan olarak bore.pub kullanılır.
    /// Özel bir sunucu girildiğinde launcher sessizce bore.pub'a dönmez.
    /// </summary>
    public string RelayServer { get; set; } = "bore.pub";

    /// <summary>
    /// Tek tek devre dışı bırakılan modlar. Anahtar biçimi: "Tür:Id"
    /// (ör. "DlcPack:araclarim", "RootPlugin:Plugin.dll").
    /// </summary>
    public List<string> DisabledMods { get; set; } = new();

    public static string SettingsPath(string dataFolder) =>
        Path.Combine(dataFolder, "myServer.json");

    public static MyServerSettings Load(string dataFolder)
    {
        var settings = SyncJson.Load<MyServerSettings>(SettingsPath(dataFolder)) ?? new MyServerSettings();
        settings.DisabledMods ??= new();
        return settings;
    }

    public void Save(string dataFolder) => SyncJson.Save(SettingsPath(dataFolder), this);
}

/// <summary>
/// "Sunucum": GTACoOp sunucu süreci + senkronizasyon (SyncServer) sürecini yönetir.
/// </summary>
public sealed class MyServer : IDisposable
{
    public const string BundledBridgePlugin = "GTACoop.Remake.Plugin.dll";

    private readonly string _serverDir;
    private Process? _gameServer;
    private Process? _syncServer;
    private readonly Action<string> _log;

    /// <summary>
    /// Sunucu modunda süreçlerin RAM sınırı. Null ise sınırsız çalışır
    /// (launcher'dan doğrudan başlatma).
    /// </summary>
    public MemoryJob? Budget { get; set; }

    private string BridgeRemovedMarker => Path.Combine(_serverDir, ".gtacoop-remake-bridge-removed");

    public MyServer(string serverDir, Action<string>? log = null)
    {
        _serverDir = serverDir;
        _log = log ?? (_ => { });
    }

    public string ServerDir => _serverDir;
    public string AddonsDir => Path.Combine(_serverDir, "Addons");
    public string PluginsDir => Path.Combine(_serverDir, "Plugins");
    public string ConfigDir => Path.Combine(_serverDir, "Configuration");
    public string ConfigPath => Path.Combine(ConfigDir, "serverSettings.xml");
    public string GameServerExe => Path.Combine(_serverDir, "gtaserver.core.exe");
    public bool IsGameServerRunning => _gameServer is { HasExited: false };
    public bool IsSyncServerRunning => _syncServer is { HasExited: false };

    /// <summary>Çalışan sunucu süreçlerinin kimlikleri (0 = çalışmıyor).</summary>
    public int GameServerPid => _gameServer is { HasExited: false } game ? game.Id : 0;
    public int SyncServerPid => _syncServer is { HasExited: false } sync ? sync.Id : 0;

    /// <summary>Sunucu süreçlerinin anlık bellek kullanımı (MB).</summary>
    public long GameServerRamMb => WorkingSetMb(_gameServer);
    public long SyncServerRamMb => WorkingSetMb(_syncServer);

    private static long WorkingSetMb(Process? process)
    {
        try
        {
            return process is { HasExited: false } ? process.WorkingSet64 / (1024 * 1024) : 0;
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>Bellek sınırına yaklaşan süreçlerin çalışma kümesini boşaltır.</summary>
    public void TrimIfNeeded()
    {
        if (Budget is not { IsActive: true } budget) return;
        var threshold = budget.LimitMb * 0.75;
        if (threshold <= 0) return;

        if (GameServerRamMb > threshold && _gameServer != null) MemoryJob.Trim(_gameServer);
        if (SyncServerRamMb > threshold && _syncServer != null) MemoryJob.Trim(_syncServer);
    }

    /// <summary>Sunucu klasörünü hazırlar (şablon kopyalama + klasörler).</summary>
    public void EnsureLayout()
    {
        if (!Directory.Exists(Distribution.ServerTemplate))
            throw new SyncException(
                $"Sunucu şablonu bulunamadı: {Distribution.ServerTemplate}\n" +
                "Dağıtım klasörünün uygulamayla birlikte geldiğinden emin olun.");

        if (!File.Exists(GameServerExe))
        {
            _log("Sunucu dosyaları hazırlanıyor (şablondan kopyalanıyor)...");
            CopyTree(Distribution.ServerTemplate, _serverDir);
            _log("Sunucu dosyaları hazır.");
        }
        else
        {
            // Klasör yarım kalmış olabilir (kesilen kopyalama, elle silinen dosya,
            // başka bir sürümün artığı). Eksik şablon dosyaları tamamlanır; var
            // olan dosyalara dokunulmaz. Bu olmadan gtaserver.core,
            // runtimeconfig.json olmadığı için hiç açılmıyordu.
            var repaired = RestoreMissing(Distribution.ServerTemplate, _serverDir);
            if (repaired > 0)
                _log($"Sunucu dosyaları tamamlandı ({repaired} eksik dosya kopyalandı).");
        }

        Directory.CreateDirectory(AddonsDir);
        ServerScanner.EnsureLayout(AddonsDir);
        Directory.CreateDirectory(PluginsDir);
        Directory.CreateDirectory(ConfigDir);

        EnsureBundledPlugins();
    }

    /// <summary>
    /// Copies the GTACoop Remake bridge plugin into the server's Plugins folder.
    /// Existing servers are upgraded in place. If the user explicitly removed
    /// the bridge from Sunucum &gt; Sunucu Modları, the marker prevents it from
    /// silently coming back.
    /// </summary>
    private void EnsureBundledPlugins()
    {
        try
        {
            if (File.Exists(BridgeRemovedMarker)) return;

            var source = Path.Combine(Distribution.ServerTemplate, "Plugins", BundledBridgePlugin);
            if (!File.Exists(source)) return;

            var target = Path.Combine(PluginsDir, BundledBridgePlugin);
            if (File.Exists(target))
            {
                var oldBytes = File.ReadAllBytes(target);
                var newBytes = File.ReadAllBytes(source);
                if (oldBytes.Length == newBytes.Length &&
                    oldBytes.AsSpan().SequenceEqual(newBytes))
                    return;
            }

            File.Copy(source, target, overwrite: true);
            _log($"Sunucu köprü eklentisi kuruldu: {BundledBridgePlugin}");
        }
        catch (Exception ex)
        {
            _log($"Sunucu köprü eklentisi kurulamadı: {ex.Message}");
        }
    }

    /// <summary>Sunucu kimliğini okur/oluşturur (Addons\.gtacoop-server.json).</summary>
    public (string ServerId, string ServerName) GetOrCreateIdentity(string desiredName)
    {
        var path = Path.Combine(AddonsDir, ".gtacoop-server.json");
        var doc = SyncJson.Load<Dictionary<string, string>>(path) ?? new();
        if (!doc.TryGetValue("serverId", out var id) || string.IsNullOrEmpty(id))
        {
            id = "srv_" + Guid.NewGuid().ToString("N")[..8];
            doc["serverId"] = id;
        }
        doc["serverName"] = desiredName;
        SyncJson.Save(path, doc);
        return (id, desiredName);
    }

    /// <summary>Configuration\serverSettings.xml dosyasını yazar.</summary>
    public void WriteSettings(MyServerSettings s)
    {
        Directory.CreateDirectory(ConfigDir);
        var disabled = new HashSet<string>(s.DisabledMods, StringComparer.OrdinalIgnoreCase);
        var pluginNames = Directory.Exists(PluginsDir)
            ? Directory.GetFiles(PluginsDir, "*.dll")
                .Select(f => Path.GetFileName(f)!)
                .Where(f => !disabled.Contains("RootPlugin:" + f))
                .Select(f => Path.GetFileNameWithoutExtension(f)!)
                .ToList()
            : new List<string>();

        var doc = new XDocument(
            new XElement("ServerConfiguration",
                new XElement("Port", s.GamePort),
                new XElement("MaxClients", s.MaxClients),
                new XElement("GamemodeName", s.GamemodeName),
                new XElement("ServerName", s.ServerName),
                new XElement("Password", s.Password),
                new XElement("PrimaryMasterServer", "https://master.gtacoop.com/"),
                new XElement("BackupMasterServer", ""),
                new XElement("AnnounceSelf", "false"),
                new XElement("AllowNicknames", "true"),
                new XElement("DebugMode", "false"),
                new XElement("Motd", s.Motd),
                new XElement("UseGroups", "true"),
                new XElement("UPnP", "true"),
                new XElement("DualStack", "false"),
                new XElement("RconPassword", ""),
                new XElement("ServerPlugins",
                    pluginNames.Select(p => new XElement("string", p))),
                new XElement("ServerVariables")));

        doc.Save(ConfigPath);
        WriteWorldSettings(s);
        _log($"Sunucu ayarları yazıldı: {ConfigPath}" +
             (pluginNames.Count > 0 ? $" ({pluginNames.Count} eklenti etkin)" : ""));
    }

    /// <summary>
    /// Sunucunun saat ayarını Configuration\gtacoop-world.json dosyasına yazar.
    /// SyncServer bu dosyayı manifeste ekler; istemci de manifestten alıp
    /// oyun saatini ayarlar. Ağ protokolü değişmez.
    /// </summary>
    public void WriteWorldSettings(MyServerSettings s)
    {
        try
        {
            Directory.CreateDirectory(ConfigDir);
            SyncJson.Save(Path.Combine(ConfigDir, "gtacoop-world.json"), new WorldSettings
            {
                ServerName = s.ServerName,
                TimeMode = string.IsNullOrWhiteSpace(s.TimeMode) ? "off" : s.TimeMode,
                TimeHour = Math.Min(23, Math.Max(0, s.TimeHour)),
                Weather = string.IsNullOrWhiteSpace(s.Weather) ? "dynamic" : s.Weather,
                GeneratedUtc = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _log("Dünya ayarı yazılamadı: " + ex.Message);
        }
    }

    /// <summary>Oyun sunucusunu başlatır.</summary>
    public void StartGameServer()
    {
        if (IsGameServerRunning) return;
        EnsureLayout();

        var psi = new ProcessStartInfo(GameServerExe)
        {
            WorkingDirectory = _serverDir,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            CreateNoWindow = true
        };

        _gameServer = new Process { StartInfo = psi, EnableRaisingEvents = true };
        _gameServer.OutputDataReceived += (_, e) => { if (e.Data != null) _log("[Sunucu] " + e.Data); };
        _gameServer.ErrorDataReceived += (_, e) => { if (e.Data != null) _log("[Sunucu!] " + e.Data); };
        _gameServer.Exited += (_, _) => _log("[Sunucu] Süreç kapandı.");
        _gameServer.Start();
        _gameServer.BeginOutputReadLine();
        _gameServer.BeginErrorReadLine();
        AttachBudget(_gameServer, "Oyun sunucusu");
        _log($"Oyun sunucusu başlatıldı (PID {_gameServer.Id}).");
    }

    /// <summary>Senkronizasyon sunucusunu başlatır.</summary>
    public void StartSyncServer(MyServerSettings settings, string? settingsPath = null)
    {
        if (IsSyncServerRunning) return;

        var exe = Path.Combine(Distribution.SyncServer, "GTACoop.SyncServer.exe");
        if (!File.Exists(exe))
            throw new SyncException($"Senkronizasyon sunucusu bulunamadı: {exe}");

        var args = new StringBuilder();
        args.Append($"--port {settings.SyncPort}");
        args.Append($" --addons \"{AddonsDir}\"");
        args.Append($" --name \"{settings.ServerName}\"");
        if (!string.IsNullOrWhiteSpace(settingsPath))
            args.Append($" --settings \"{settingsPath}\"");
        if (!string.IsNullOrEmpty(settings.SyncToken))
            args.Append($" --token \"{settings.SyncToken}\"");

        var psi = new ProcessStartInfo(exe)
        {
            WorkingDirectory = Path.GetDirectoryName(exe)!,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            CreateNoWindow = true,
            Arguments = args.ToString()
        };

        _syncServer = new Process { StartInfo = psi, EnableRaisingEvents = true };
        _syncServer.OutputDataReceived += (_, e) => { if (e.Data != null) _log("[Sync] " + e.Data); };
        _syncServer.ErrorDataReceived += (_, e) => { if (e.Data != null) _log("[Sync!] " + e.Data); };
        _syncServer.Exited += (_, _) => _log("[Sync] Süreç kapandı.");
        _syncServer.Start();
        _syncServer.BeginOutputReadLine();
        _syncServer.BeginErrorReadLine();
        AttachBudget(_syncServer, "Senkronizasyon sunucusu");
        _log($"Senkronizasyon sunucusu başlatıldı: http://0.0.0.0:{settings.SyncPort}/ (PID {_syncServer.Id})");
    }

    /// <summary>RAM sınırını sürece uygular ve sonucu günlüğe yazar.</summary>
    private void AttachBudget(Process process, string label)
    {
        if (Budget is not { IsActive: true } budget) return;

        _log(Budget.Assign(process)
            ? $"{label}: RAM sınırı uygulandı ({budget.LimitMb} MB)."
            : $"{label}: RAM sınırı uygulanamadı; süreç sınırsız bellekle çalışıyor.");
    }

    public void StopAll()
    {
        StopProcess(ref _syncServer, "Senkronizasyon sunucusu");
        StopProcess(ref _gameServer, "Oyun sunucusu");
        KillOrphans("GTACoop.SyncServer", "Senkronizasyon sunucusu");
        KillOrphans("gtaserver.core", "Oyun sunucusu");
    }

    /// <summary>Önceki oturumdan kalan (referansı kaybolmuş) sunucu süreçlerini temizler.</summary>
    private void KillOrphans(string processName, string label)
    {
        Process[] leftovers;
        try
        {
            leftovers = Process.GetProcessesByName(processName);
        }
        catch
        {
            return;
        }

        foreach (var p in leftovers)
        {
            try
            {
                if (p.HasExited) continue;
                _log($"{label}: kalan süreç temizleniyor (PID {p.Id}).");
                p.Kill(entireProcessTree: true);
                p.WaitForExit(5000);
            }
            catch (Exception ex)
            {
                _log($"{label}: kalan süreç kapatılamadı: {ex.Message}");
            }
            finally
            {
                p.Dispose();
            }
        }
    }

    private void StopProcess(ref Process? proc, string label)
    {
        if (proc is not { HasExited: false }) return;
        try
        {
            proc.Kill(entireProcessTree: true);
            proc.WaitForExit(5000);
            _log($"{label} durduruldu.");
        }
        catch (Exception ex)
        {
            _log($"{label} durdurulamadı: {ex.Message}");
        }
        finally
        {
            proc?.Dispose();
            proc = null;
        }
    }

    /// <summary>Şablonda olup sunucu klasöründe olmayan dosyaları kopyalar.</summary>
    private static int RestoreMissing(string source, string dest)
    {
        var copied = 0;

        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(dest, Path.GetRelativePath(source, file));
            if (File.Exists(target)) continue;

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(file, target, overwrite: false);
                copied++;
            }
            catch
            {
                // Tek dosya kopyalanamazsa diğerleri yine denenir.
            }
        }

        return copied;
    }

    private static void CopyTree(string source, string dest)
    {
        Directory.CreateDirectory(dest);
        foreach (var dir in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(Path.Combine(dest, Path.GetRelativePath(source, dir)));
        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(dest, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }
    }

    public void Dispose() => StopAll();
}
