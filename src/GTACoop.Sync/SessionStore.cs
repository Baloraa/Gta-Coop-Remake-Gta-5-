using System.Diagnostics;

namespace GTACoop.Sync;

/// <summary>Etkin oyun oturumu kaydı (session.json).</summary>
public sealed class SessionRecord
{
    public string ServerId { get; set; } = "";
    public string ServerName { get; set; } = "";
    public string Host { get; set; } = "";
    public int GamePort { get; set; } = 4499;

    /// <summary>Arka planda çalışan izleyicinin (SyncTool session-watch) PID'i; 0 = yok.</summary>
    public int WatcherPid { get; set; }

    public DateTime StartedUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>session.json okuma/yazma.</summary>
public static class SessionStore
{
    public static string RecordPath(string dataFolder) => Path.Combine(dataFolder, "session.json");

    public static SessionRecord? Load(string dataFolder) =>
        SyncJson.Load<SessionRecord>(RecordPath(dataFolder));

    public static void Save(string dataFolder, SessionRecord record) =>
        SyncJson.Save(RecordPath(dataFolder), record);

    public static void Clear(string dataFolder)
    {
        try { File.Delete(RecordPath(dataFolder)); } catch { }
    }
}

/// <summary>
/// Oturum modu: sunucu modları yalnızca oyun açıkken kurulu kalır; oyun kapanınca
/// otomatik kaldırılır ve oyun eski (modsuz) hâline döner.
/// Kullanıcının kendi modlarına dokunulmaz; yalnızca sunucudan kurulan dosyalar kaldırılır.
/// </summary>
public static class SessionService
{
    /// <summary>
    /// Oturum sonu temizliği: sunucu modlarını kaldırır, AutoConnect'i kapatır, kaydı siler.
    /// Dönüş: temizlik tamamlandı mı (oyun klasörü geçersizse false).
    /// </summary>
    public static bool Finish(string dataFolder, string gameFolder, Action<string>? log = null)
    {
        if (string.IsNullOrWhiteSpace(gameFolder) || !Directory.Exists(gameFolder))
        {
            log?.Invoke("Oyun klasörü bilinmiyor; oturum temizliği yapılamadı.");
            return false;
        }

        var rec = SessionStore.Load(dataFolder);
        if (rec != null)
        {
            var state = SyncStateStore.Load(dataFolder, rec.ServerId);
            if (state.Addons.Count > 0)
            {
                var applier = new AddonApplier(gameFolder, log);
                applier.UninstallAll(state, dataFolder);
            }
        }

        ClientSettingsEditor.DisableAutoConnect(gameFolder, log);
        SessionStore.Clear(dataFolder);
        return true;
    }

    /// <summary>
    /// Oyunu izler; oyun kapanınca (ya da oyun hiç başlamazsa zaman aşımında)
    /// oturum temizliğini yapar.
    /// </summary>
    public static async Task WatchAsync(
        string dataFolder, string gameFolder, string[]? processNames,
        Action<string>? log = null, double startTimeoutMinutes = 12,
        CancellationToken ct = default)
    {
        var names = processNames is { Length: > 0 }
            ? processNames
            : new[] { "GTA5", "GTA5_Enhanced", "PlayGTAV" };

        var started = IsAnyRunning(names);
        DateTime? lastSeen = started ? DateTime.UtcNow : null;
        var begin = DateTime.UtcNow;
        log?.Invoke($"Oturum izleme başladı ({(started ? "oyun açık" : "oyun bekleniyor")}).");

        while (true)
        {
            ct.ThrowIfCancellationRequested();

            if (IsAnyRunning(names))
            {
                if (!started)
                {
                    started = true;
                    log?.Invoke("Oyun algılandı; kapanınca sunucu modları kaldırılacak.");
                }
                lastSeen = DateTime.UtcNow;
            }
            else if (started && lastSeen.HasValue &&
                     (DateTime.UtcNow - lastSeen.Value).TotalSeconds > 20)
            {
                log?.Invoke("Oyun kapandı.");
                break;
            }
            else if (!started && (DateTime.UtcNow - begin).TotalMinutes > startTimeoutMinutes)
            {
                log?.Invoke("Oyun başlatılmadı (zaman aşımı); oturum kapatılıyor.");
                break;
            }

            await Task.Delay(2500, ct);
        }

        await Task.Delay(2000, ct); // dosya tanıtıcılarının serbest kalmasını bekle

        log?.Invoke("Oturum temizliği başlıyor...");
        if (Finish(dataFolder, gameFolder, log))
            log?.Invoke("Oturum bitti: sunucu modları kaldırıldı, oyun uygulama öncesi hâline döndü.");
        else
            log?.Invoke("Oturum temizliği tamamlanamadı; bir sonraki launcher açılışında yeniden denenecek.");
    }

    /// <summary>
    /// Bağımsız izleyiciyi (SyncTool session-watch) başlatır; launcher kapansa bile
    /// oyun çıkışında temizlik yapılır. Başlatılamazsa 0 döner.
    /// </summary>
    public static int TryStartWatcher(string dataFolder)
    {
        try
        {
            var exe = Path.Combine(AppContext.BaseDirectory, "tools", "SyncTool", "GTACoop.SyncTool.exe");
            if (!File.Exists(exe)) return 0;

            var psi = new ProcessStartInfo(exe)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(exe)!,
                Arguments = $"session-watch --data \"{dataFolder}\""
            };
            var p = Process.Start(psi);
            return p?.Id ?? 0;
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>Aktif izleyiciyi durdurur (temizliği launcher üstlenecekse).</summary>
    public static void KillWatcher(SessionRecord rec, Action<string>? log = null)
    {
        if (rec.WatcherPid <= 0) return;
        try
        {
            var p = Process.GetProcessById(rec.WatcherPid);
            if (p.ProcessName.Contains("SyncTool", StringComparison.OrdinalIgnoreCase))
            {
                p.Kill(entireProcessTree: true);
                p.WaitForExit(5000);
                log?.Invoke($"Oturum izleyicisi durduruldu (PID {rec.WatcherPid}).");
            }
        }
        catch { /* süreç zaten kapanmış olabilir */ }
    }

    /// <summary>İzleyicinin hâlâ çalışıp çalışmadığı.</summary>
    public static bool IsWatcherAlive(SessionRecord rec)
    {
        if (rec.WatcherPid <= 0) return false;
        try
        {
            var p = Process.GetProcessById(rec.WatcherPid);
            return !p.HasExited;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsAnyRunning(string[] names)
    {
        foreach (var n in names)
        {
            try
            {
                if (Process.GetProcessesByName(n).Length > 0) return true;
            }
            catch { }
        }
        return false;
    }
}
