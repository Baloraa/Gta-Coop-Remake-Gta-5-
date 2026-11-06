using System.Diagnostics;
using System.Text;

namespace GTACoop.Sync;

/// <summary>Sunucu modunun (VDS) canlı durumu: server-host.json içinde tutulur.</summary>
public sealed class ServerModeStatus
{
    public int HostPid { get; set; }
    public int GamePid { get; set; }
    public int SyncPid { get; set; }
    public int Restarts { get; set; }
    public int RamLimitMb { get; set; }
    public long GameRamMb { get; set; }
    public long SyncRamMb { get; set; }
    public bool Running { get; set; }
    public string LastEvent { get; set; } = "";
    public DateTime StartedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
}

/// <summary>
/// Sunucu modu (VDS) yardımcıları: sunucu barındırıcı sürecini başlatma,
/// durumu okuma, durdurma ve Windows açılışında otomatik başlatma.
///
/// Sunucu modu, oyunu açmadan çalışan <c>gtaserver.core</c> sürecini launcher'dan
/// bağımsız bir barındırıcıya devreder; böylece launcher (ve oyun) kapalıyken de
/// oyuncular sunucuya bağlanabilir.
/// </summary>
public static class ServerMode
{
    public const string TaskName = "GTACoop Remake Sunucu Modu";
    public const string HostExeName = "GTACoop.ServerHost.exe";

    public static string StatusPath(string dataFolder) => Path.Combine(dataFolder, "server-host.json");

    public static string StopFlagPath(string dataFolder) => Path.Combine(dataFolder, "server-host.stop");

    public static string LogPath(string dataFolder) => Path.Combine(dataFolder, "logs", "server-host.log");

    public static ServerModeStatus? ReadStatus(string dataFolder)
    {
        try
        {
            var path = StatusPath(dataFolder);
            return File.Exists(path) ? SyncJson.Load<ServerModeStatus>(path) : null;
        }
        catch
        {
            return null;
        }
    }

    public static void WriteStatus(string dataFolder, ServerModeStatus status)
    {
        try
        {
            SyncJson.Save(StatusPath(dataFolder), status);
        }
        catch
        {
            // Durum dosyası yalnızca bilgi amaçlı; yazılamazsa sunucu çalışmaya devam eder.
        }
    }

    /// <summary>Durum dosyasındaki PID gerçekten yaşıyor mu?</summary>
    public static bool IsHostAlive(ServerModeStatus? status)
    {
        if (status == null || status.HostPid <= 0 || !status.Running) return false;

        try
        {
            using var process = Process.GetProcessById(status.HostPid);
            return !process.HasExited;
        }
        catch
        {
            return false;
        }
    }

    public static bool IsRunning(string dataFolder) => IsHostAlive(ReadStatus(dataFolder));

    public static void RequestStop(string dataFolder)
    {
        try
        {
            File.WriteAllText(StopFlagPath(dataFolder), DateTime.UtcNow.ToString("o"));
        }
        catch
        {
            // Durdurma isteği yazılamazsa çağıran taraf süreci sonlandırabilir.
        }
    }

    public static void ClearStopRequest(string dataFolder)
    {
        try
        {
            if (File.Exists(StopFlagPath(dataFolder))) File.Delete(StopFlagPath(dataFolder));
        }
        catch
        {
            // Bir sonraki denemede silinir.
        }
    }

    public static bool StopRequested(string dataFolder)
    {
        try
        {
            return File.Exists(StopFlagPath(dataFolder));
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Uygulama klasöründe barındırıcı exe'yi arar.</summary>
    public static string? FindHostExe(string baseDirectory)
    {
        var candidates = new[]
        {
            Path.Combine(baseDirectory, "tools", "ServerHost", HostExeName),
            Path.Combine(baseDirectory, HostExeName),
            Path.Combine(baseDirectory, "Distribution", "ServerHost", HostExeName)
        };

        return candidates.FirstOrDefault(File.Exists);
    }

    /// <summary>Barındırıcı sürecine verilecek komut satırı.</summary>
    public static string BuildArguments(string serverDir, string dataFolder, string distributionRoot,
        int ramLimitMb, int restartDelaySeconds)
    {
        var args = new StringBuilder();
        args.Append($"--server-dir \"{serverDir}\"");
        args.Append($" --settings \"{MyServerSettings.SettingsPath(dataFolder)}\"");
        args.Append($" --distribution \"{distributionRoot}\"");
        args.Append($" --ram {Math.Max(0, ramLimitMb)}");
        args.Append($" --restart-delay {Math.Clamp(restartDelaySeconds, 2, 300)}");
        args.Append($" --status \"{StatusPath(dataFolder)}\"");
        args.Append($" --stop \"{StopFlagPath(dataFolder)}\"");
        args.Append($" --log \"{LogPath(dataFolder)}\"");
        return args.ToString();
    }

    /// <summary>Sunucu modunu launcher'dan bağımsız bir süreç olarak başlatır.</summary>
    public static Process StartHost(string hostExe, string arguments)
    {
        var psi = new ProcessStartInfo(hostExe)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(hostExe) ?? AppContext.BaseDirectory,
            Arguments = arguments
        };

        return Process.Start(psi) ?? throw new SyncException("Sunucu modu başlatılamadı.");
    }

    /// <summary>Windows açılışında (oturum açılınca) sunucu modunu başlatan görevi kurar.</summary>
    public static bool InstallAutostart(string hostExe, string arguments, out string error)
    {
        error = "";
        var taskCommand = $"\"{hostExe}\" {arguments}";

        if (!RunSchtasks(new[]
            {
                "/Create", "/F", "/SC", "ONLOGON", "/RL", "LIMITED",
                "/TN", TaskName, "/TR", taskCommand
            }, out var output))
        {
            error = output;
            return false;
        }

        return true;
    }

    public static bool RemoveAutostart(out string error)
    {
        error = "";
        if (!IsAutostartInstalled()) return true;

        if (!RunSchtasks(new[] { "/Delete", "/F", "/TN", TaskName }, out var output))
        {
            error = output;
            return false;
        }

        return true;
    }

    public static bool IsAutostartInstalled() =>
        RunSchtasks(new[] { "/Query", "/TN", TaskName }, out _);

    private static bool RunSchtasks(string[] arguments, out string output)
    {
        try
        {
            var psi = new ProcessStartInfo("schtasks")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            foreach (var argument in arguments) psi.ArgumentList.Add(argument);

            using var process = Process.Start(psi);
            if (process == null)
            {
                output = "schtasks başlatılamadı.";
                return false;
            }

            output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
            if (!process.WaitForExit(15000))
            {
                try { process.Kill(entireProcessTree: true); } catch { }
                output = "schtasks zaman aşımına uğradı.";
                return false;
            }

            return process.ExitCode == 0;
        }
        catch (Exception ex)
        {
            output = ex.Message;
            return false;
        }
    }
}
