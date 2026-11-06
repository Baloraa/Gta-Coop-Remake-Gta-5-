using System.Diagnostics;
using System.Text;
using GTACoop.Sync;

// ---------------------------------------------------------------------------
// GTACoop.ServerHost
//
// Sunucu modu (VDS): oyunu ve launcher'ı açmadan çalışan gtaserver.core ile
// senkronizasyon sunucusunu yönetir. Launcher kapansa da süreçler ayakta kalır,
// çöken süreç otomatik yeniden başlar ve istenirse RAM sınırı uygulanır.
//
// Kullanım:
//   GTACoop.ServerHost.exe --server-dir <dir> --settings <myServer.json>
//       [--distribution <dir>] [--ram 2048] [--restart-delay 10]
//       [--status <json>] [--stop <dosya>] [--log <dosya>]
// ---------------------------------------------------------------------------

Console.OutputEncoding = Encoding.UTF8;

string serverDir = "";
string settingsPath = "";
string distributionRoot = "";
string statusPath = "";
string stopPath = "";
string logPath = "";
var ramLimitMb = 0;
var restartDelaySeconds = 10;

for (var i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--server-dir" when i + 1 < args.Length: serverDir = args[++i]; break;
        case "--settings" when i + 1 < args.Length: settingsPath = args[++i]; break;
        case "--distribution" when i + 1 < args.Length: distributionRoot = args[++i]; break;
        case "--ram" when i + 1 < args.Length: int.TryParse(args[++i], out ramLimitMb); break;
        case "--restart-delay" when i + 1 < args.Length: int.TryParse(args[++i], out restartDelaySeconds); break;
        case "--status" when i + 1 < args.Length: statusPath = args[++i]; break;
        case "--stop" when i + 1 < args.Length: stopPath = args[++i]; break;
        case "--log" when i + 1 < args.Length: logPath = args[++i]; break;
        case "--help":
            Console.WriteLine("Kullanım: GTACoop.ServerHost.exe --server-dir <klasör> --settings <myServer.json> " +
                              "[--distribution <klasör>] [--ram MB] [--restart-delay sn] " +
                              "[--status json] [--stop dosya] [--log dosya]");
            return 0;
    }
}

if (string.IsNullOrWhiteSpace(serverDir))
{
    Console.Error.WriteLine("HATA: --server-dir parametresi gerekli.");
    return 1;
}

serverDir = Path.GetFullPath(serverDir);
if (string.IsNullOrWhiteSpace(settingsPath))
{
    var dataFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GTACoopRemake");
    settingsPath = MyServerSettings.SettingsPath(dataFolder);
}

settingsPath = Path.GetFullPath(settingsPath);
var dataRoot = Path.GetDirectoryName(settingsPath)!;

statusPath = string.IsNullOrWhiteSpace(statusPath)
    ? ServerMode.StatusPath(dataRoot)
    : Path.GetFullPath(statusPath);
stopPath = string.IsNullOrWhiteSpace(stopPath)
    ? ServerMode.StopFlagPath(dataRoot)
    : Path.GetFullPath(stopPath);
logPath = string.IsNullOrWhiteSpace(logPath)
    ? ServerMode.LogPath(dataRoot)
    : Path.GetFullPath(logPath);

if (string.IsNullOrWhiteSpace(distributionRoot))
{
    var baseDir = AppContext.BaseDirectory;
    var parent = Directory.GetParent(baseDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
    distributionRoot = parent != null && Directory.Exists(Path.Combine(parent.FullName, "Server"))
        ? parent.FullName
        : Path.Combine(baseDir, "Distribution");
}

if (!Directory.Exists(distributionRoot))
{
    Console.Error.WriteLine($"HATA: dağıtım klasörü bulunamadı: {distributionRoot}");
    return 1;
}

// GTACoOp sunucu şablonu ve SyncServer bu kökten çözülür.
Environment.SetEnvironmentVariable("GTACOOP_DISTRIBUTION_ROOT", distributionRoot);

// Aynı anda iki barındırıcı çalışmasın (aynı portları iki kez dinlemeye çalışır).
using var instanceLock = new Mutex(true, "GTACoop.ServerHost.Single", out var isFirstInstance);
if (!isFirstInstance)
{
    Console.Error.WriteLine("Sunucu modu zaten çalışıyor; yeni süreç kapatılıyor.");
    return 3;
}

restartDelaySeconds = Math.Clamp(restartDelaySeconds, 2, 300);
ramLimitMb = Math.Max(0, ramLimitMb);

void Log(string message)
{
    var line = $"[{DateTime.Now:HH:mm:ss}] {message}";
    Console.WriteLine(line);

    try
    {
        Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
        RotateIfLarge(logPath);
        File.AppendAllText(logPath, line + Environment.NewLine, Encoding.UTF8);
    }
    catch
    {
        // Günlük yazılamazsa sunucu yine çalışsın.
    }
}

static void RotateIfLarge(string path)
{
    try
    {
        var info = new FileInfo(path);
        if (!info.Exists || info.Length < 8L * 1024 * 1024) return;
        var backup = path + ".1";
        if (File.Exists(backup)) File.Delete(backup);
        File.Move(path, backup);
    }
    catch
    {
        // Döndürme başarısızsa dosya büyümeye devam eder.
    }
}

Log("== GTACoop Sunucu Modu (VDS) ==");
Log($"Sunucu klasörü: {serverDir}");
Log($"Ayarlar       : {settingsPath}");
Log(ramLimitMb > 0 ? $"RAM sınırı     : {ramLimitMb} MB (süreç başına)" : "RAM sınırı     : sınırsız");

// Eski bir durdurma isteği varsa temizle; aksi halde sunucu hemen kapanırdı.
try
{
    if (File.Exists(stopPath)) File.Delete(stopPath);
}
catch
{
    // Silinemezse aşağıdaki döngü yine de çalışır.
}

var status = new ServerModeStatus
{
    HostPid = Environment.ProcessId,
    Running = true,
    RamLimitMb = ramLimitMb,
    StartedUtc = DateTime.UtcNow,
    UpdatedUtc = DateTime.UtcNow,
    LastEvent = "Başlatılıyor"
};
ServerMode.WriteStatus(dataRoot, status);

using var budget = ramLimitMb > 0 ? new MemoryJob(ramLimitMb) : null;
var server = new MyServer(serverDir, Log) { Budget = budget };
var restarts = 0;
var lastTrim = DateTime.UtcNow;

try
{
    server.EnsureLayout();

    var settings = MyServerSettings.Load(dataRoot);
    server.GetOrCreateIdentity(settings.ServerName);
    server.WriteSettings(settings);

    Log($"Sunucu adı: {settings.ServerName} (oyun portu {settings.GamePort}, sync portu {settings.SyncPort})");

    server.StartGameServer();
    server.StartSyncServer(settings, settingsPath);

    status.LastEvent = "Çalışıyor";
    ServerMode.WriteStatus(dataRoot, status);

    while (true)
    {
        Thread.Sleep(1000);

        if (File.Exists(stopPath))
        {
            Log("Durdurma isteği alındı.");
            break;
        }

        var gameAlive = server.IsGameServerRunning;
        var syncAlive = server.IsSyncServerRunning;

        if (!gameAlive || !syncAlive)
        {
            restarts++;
            Log(!gameAlive
                ? "Oyun sunucusu kapandı; yeniden başlatılıyor."
                : "Senkronizasyon sunucusu kapandı; yeniden başlatılıyor.");

            status.Restarts = restarts;
            status.LastEvent = $"Yeniden başlatma #{restarts}";
            ServerMode.WriteStatus(dataRoot, status);

            server.StopAll();
            Thread.Sleep(TimeSpan.FromSeconds(restartDelaySeconds));

            try
            {
                var reloaded = MyServerSettings.Load(dataRoot);
                server.EnsureLayout();
                server.WriteSettings(reloaded);
                server.StartGameServer();
                server.StartSyncServer(reloaded, settingsPath);
                status.LastEvent = "Çalışıyor";
            }
            catch (Exception ex)
            {
                Log("Yeniden başlatma hatası: " + ex.Message);
                status.LastEvent = "Hata: " + ex.Message;
            }

            ServerMode.WriteStatus(dataRoot, status);
            continue;
        }

        if (DateTime.UtcNow - lastTrim > TimeSpan.FromSeconds(10))
        {
            lastTrim = DateTime.UtcNow;
            server.TrimIfNeeded();
        }

        // Durum dosyasını saniyede bir yazmak gereksiz: 2 saniyede bir yeterli.
        if ((DateTime.UtcNow - status.UpdatedUtc).TotalSeconds >= 2)
        {
            status.GamePid = server.GameServerPid;
            status.SyncPid = server.SyncServerPid;
            status.GameRamMb = server.GameServerRamMb;
            status.SyncRamMb = server.SyncServerRamMb;
            status.Restarts = restarts;
            status.UpdatedUtc = DateTime.UtcNow;
            ServerMode.WriteStatus(dataRoot, status);
        }
    }
}
catch (Exception ex)
{
    Log("HATA: " + ex);
    status.LastEvent = "Hata: " + ex.Message;
}
finally
{
    Log("Sunucu modu kapatılıyor...");
    server.Dispose();

    status.Running = false;
    status.GamePid = 0;
    status.SyncPid = 0;
    status.LastEvent = "Durduruldu";
    status.UpdatedUtc = DateTime.UtcNow;
    ServerMode.WriteStatus(dataRoot, status);

    try
    {
        if (File.Exists(stopPath)) File.Delete(stopPath);
    }
    catch
    {
        // Bir sonraki başlatmada temizlenir.
    }

    Log("Sunucu modu kapandı.");
}

return 0;
