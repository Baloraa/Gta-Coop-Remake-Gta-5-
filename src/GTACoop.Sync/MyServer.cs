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
    /// İsteğe bağlı kısa katılım kodu (ör. "Gel.com"). Boşsa kısa kod yayını kapalıdır.
    /// Uzun GTC1 kodu bu ayardan bağımsız olarak çalışır.
    /// </summary>
    public string ShortCode { get; set; } = "";

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
    private readonly string _serverDir;
    private Process? _gameServer;
    private Process? _syncServer;
    private readonly Action<string> _log;

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

    /// <summary>Sunucu klasörünü hazırlar (şablon kopyalama + klasörler).</summary>
    public void EnsureLayout()
    {
        if (!File.Exists(GameServerExe))
        {
            if (!Directory.Exists(Distribution.ServerTemplate))
                throw new SyncException(
                    $"Sunucu şablonu bulunamadı: {Distribution.ServerTemplate}\n" +
                    "Dağıtım klasörünün uygulamayla birlikte geldiğinden emin olun.");

            _log("Sunucu dosyaları hazırlanıyor (şablondan kopyalanıyor)...");
            CopyTree(Distribution.ServerTemplate, _serverDir);
            _log("Sunucu dosyaları hazır.");
        }

        Directory.CreateDirectory(AddonsDir);
        ServerScanner.EnsureLayout(AddonsDir);
        Directory.CreateDirectory(PluginsDir);
        Directory.CreateDirectory(ConfigDir);
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
                new XElement("UPnP", "false"),
                new XElement("DualStack", "false"),
                new XElement("RconPassword", ""),
                new XElement("ServerPlugins",
                    pluginNames.Select(p => new XElement("string", p))),
                new XElement("ServerVariables")));

        doc.Save(ConfigPath);
        _log($"Sunucu ayarları yazıldı: {ConfigPath}" +
             (pluginNames.Count > 0 ? $" ({pluginNames.Count} eklenti etkin)" : ""));
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
        _log($"Senkronizasyon sunucusu başlatıldı: http://0.0.0.0:{settings.SyncPort}/ (PID {_syncServer.Id})");
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
