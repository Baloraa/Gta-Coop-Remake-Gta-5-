using System.Diagnostics;
using System.Security.Principal;

namespace GTACoop.Remake;

/// <summary>
/// Sunucu portları için Windows Güvenlik Duvarı kuralları ekler. Yalnızca
/// launcher yönetici olarak çalışıyorsa uygulanır; UAC penceresi açılmaz.
/// Relay zaten giden bağlantı kullandığı için bu adım isteğe bağlıdır.
/// </summary>
internal static class FirewallHelper
{
    public static void EnsureRulesAsync(int gamePort, int syncPort, Action<string> log)
    {
        _ = Task.Run(() =>
        {
            try
            {
                if (!IsElevated())
                {
                    log("Güvenlik duvarı kuralları atlandı (launcher yönetici değil). " +
                        "Otomatik relay bu kurallar olmadan da çalışır.");
                    return;
                }

                EnsureRule("GTACoop Remake Oyun (UDP " + gamePort + ")", "UDP", gamePort, log);
                EnsureRule("GTACoop Remake Sync (TCP " + syncPort + ")", "TCP", syncPort, log);
            }
            catch (Exception ex)
            {
                log("Güvenlik duvarı kuralı eklenemedi: " + ex.Message);
            }
        });
    }

    private static void EnsureRule(string name, string protocol, int port, Action<string> log)
    {
        if (RuleExists(name)) return;

        var args = "advfirewall firewall add rule " +
                   $"name=\"{name}\" dir=in action=allow protocol={protocol} " +
                   $"localport={port} profile=any";
        var (code, output) = RunNetsh(args);
        if (code == 0)
            log($"Güvenlik duvarı kuralı eklendi: {name}");
        else
            log($"Güvenlik duvarı kuralı eklenemedi ({name}): {output.Trim()}");
    }

    private static bool RuleExists(string name)
    {
        var (code, _) = RunNetsh($"advfirewall firewall show rule name=\"{name}\"");
        return code == 0;
    }

    private static bool IsElevated()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    private static (int Code, string Output) RunNetsh(string arguments)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "netsh",
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            StandardErrorEncoding = System.Text.Encoding.UTF8
        };

        using var process = Process.Start(psi);
        if (process == null) return (-1, "netsh başlatılamadı");
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit(8000);
        return (process.HasExited ? process.ExitCode : -1, output + error);
    }
}
