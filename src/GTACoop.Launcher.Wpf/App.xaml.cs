using System.Threading;
using GTACoop.Remake;
using GTACoop.Sync;

namespace GTACoop.Remake.Wpf;

public partial class App : System.Windows.Application
{
    private Mutex? _mutex;

    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);

        // WinForms sekmeleri bu kabukta barındırıldığı için görsel stiller
        // herhangi bir denetim oluşturulmadan önce açılmalı.
        System.Windows.Forms.Application.EnableVisualStyles();
        System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);

        _mutex = new Mutex(true, "GTACoopRemakeLauncher_v2", out var isNew);
        if (!isNew)
        {
            System.Windows.Forms.MessageBox.Show(
                "GTACoop Remake Launcher zaten çalışıyor. / GTACoop Remake Launcher is already running.",
                "GTACoop Remake",
                System.Windows.Forms.MessageBoxButtons.OK,
                System.Windows.Forms.MessageBoxIcon.Information);
            Shutdown();
            return;
        }

        Theme.InitDarkMode();
        Theme.WatchForms();

        try
        {
            AppEnv.Init();
        }
        catch (Exception ex)
        {
            System.Windows.Forms.MessageBox.Show(
                "Başlatma hatası / Startup error: " + ex.Message,
                "GTACoop Remake",
                System.Windows.Forms.MessageBoxButtons.OK,
                System.Windows.Forms.MessageBoxIcon.Error);
            Shutdown();
            return;
        }

        SelfUpdater.CleanupOld();

        if (string.IsNullOrEmpty(AppEnv.Settings.Language)) AppEnv.Settings.Language = "tr";
        AppEnv.Settings.Language = Loc.NormalizeLanguage(AppEnv.Settings.Language);
        Loc.Language = AppEnv.Settings.Language;
        SyncText.Language = Loc.Language;
    }

    protected override void OnExit(System.Windows.ExitEventArgs e)
    {
        try { _mutex?.ReleaseMutex(); } catch { }
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
