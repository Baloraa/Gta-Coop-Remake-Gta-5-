namespace GTACoop.Sync;

/// <summary>Uygulamayla birlikte gelen dağıtım klasörleri.</summary>
public static class Distribution
{
    public static string Root
    {
        get
        {
            // Test/geliştirme için ortam değişkeniyle geçersiz kılınabilir.
            var overridden = Environment.GetEnvironmentVariable("GTACOOP_DISTRIBUTION_ROOT");
            if (!string.IsNullOrEmpty(overridden) && Directory.Exists(overridden))
                return overridden;
            return Path.Combine(AppContext.BaseDirectory, "Distribution");
        }
    }

    /// <summary>GTACoOp sunucu şablonu (gtaserver.core.exe vb.).</summary>
    public static string ServerTemplate => Path.Combine(Root, "Server");

    /// <summary>GTACoOp istemci script dosyaları (GTACoOp.dll vb.).</summary>
    public static string ClientScripts => Path.Combine(Root, "Scripts");

    /// <summary>Senkronizasyon sunucusu (GTACoop.SyncServer.exe).</summary>
    public static string SyncServer => Path.Combine(Root, "SyncServer");

    public static bool IsAvailable => Directory.Exists(Root) && Directory.Exists(ServerTemplate);
}

/// <summary>GTACoOp istemci dosyalarını oyunun scripts klasörüne kurar.</summary>
public static class ClientInstaller
{
    public sealed class InstallResult
    {
        public int Copied;
        public int Skipped;
        public List<string> Details { get; } = new();
    }

    /// <summary>
    /// İstemci dosyalarını kurar:
    /// - GTACoOp.dll / .config her zaman bizim sürümümüzle güncellenir,
    /// - bağımlılık dll'leri yalnızca eksikse kopyalanır (kullanıcının yenileri korunur).
    /// </summary>
    public static InstallResult InstallClient(string gameFolder, Action<string>? log = null)
    {
        var src = Distribution.ClientScripts;
        if (!Directory.Exists(src))
            throw new SyncException($"İstemci dosyaları bulunamadı: {src}");

        var dest = Path.Combine(gameFolder, "scripts");
        Directory.CreateDirectory(dest);
        var result = new InstallResult();

        foreach (var file in Directory.GetFiles(src))
        {
            var name = Path.GetFileName(file);
            var target = Path.Combine(dest, name);
            bool isCore = name.StartsWith("GTACoOp", StringComparison.OrdinalIgnoreCase);

            bool need = !File.Exists(target);
            if (!need && isCore)
                need = !HashEqual(file, target);

            if (!need)
            {
                result.Skipped++;
                continue;
            }

            if (File.Exists(target) && isCore)
            {
                var bak = target + ".bak";
                if (!File.Exists(bak))
                {
                    try { File.Copy(target, bak); } catch { }
                }
            }

            File.Copy(file, target, overwrite: true);
            result.Copied++;
            result.Details.Add(name);
            log?.Invoke($"İstemci dosyası kuruldu: {name}");
        }

        if (result.Copied == 0)
            log?.Invoke("İstemci dosyaları güncel; kopyalama gerekmedi.");
        return result;
    }

    private static bool HashEqual(string a, string b)
    {
        try
        {
            using var sha = System.Security.Cryptography.SHA256.Create();
            using var fa = File.OpenRead(a);
            var ha = Convert.ToHexString(sha.ComputeHash(fa));
            using var fb = File.OpenRead(b);
            var hb = Convert.ToHexString(sha.ComputeHash(fb));
            return string.Equals(ha, hb, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }
}

/// <summary>Oyunu başlatma yardımcıları.</summary>
public static class GameLauncher
{
    public static bool IsRunning() => AddonApplier.IsGameRunning();

    /// <summary>GTA5.exe'yi başlatır (Steam açık olmalı).</summary>
    public static System.Diagnostics.Process Launch(string gameFolder, Action<string>? log = null)
    {
        var exe = GameFolder.GameExe(gameFolder);
        if (!File.Exists(exe))
            throw new SyncException($"Oyun çalıştırılabilir dosyası bulunamadı: {exe}");

        log?.Invoke($"Oyun başlatılıyor: {Path.GetFileName(exe)}");
        var psi = new System.Diagnostics.ProcessStartInfo(exe)
        {
            WorkingDirectory = gameFolder,
            UseShellExecute = true
        };
        var proc = System.Diagnostics.Process.Start(psi)
            ?? throw new SyncException("Oyun süreci başlatılamadı.");
        return proc;
    }
}
