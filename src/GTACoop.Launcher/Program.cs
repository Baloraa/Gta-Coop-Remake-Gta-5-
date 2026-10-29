using System.Diagnostics;
using System.Windows.Forms;

namespace GTACoop.Remake;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        var mutex = new Mutex(true, "GTACoopRemakeLauncher_v2", out var isNew);
        if (!isNew)
        {
            MessageBox.Show("GTACoop Remake Launcher zaten çalışıyor. / GTACoop Remake Launcher is already running.",
                "GTACoop Remake", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        ApplicationConfiguration.Initialize();

        try
        {
            AppEnv.Init();
        }
        catch (Exception ex)
        {
            MessageBox.Show("Başlatma hatası / Startup error: " + ex.Message, "GTACoop Remake",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        // Dil: ilk açılışta sorulur; sonra üst çubuktaki kutudan değiştirilebilir.
        if (string.IsNullOrEmpty(AppEnv.Settings.Language))
        {
            var chosen = "tr";
            using (var dlg = new LanguageDialog())
                if (dlg.ShowDialog() == DialogResult.OK) chosen = dlg.Selected;
            AppEnv.Settings.Language = chosen;
            AppEnv.SaveSettings();
        }
        AppEnv.Settings.Language = Loc.NormalizeLanguage(AppEnv.Settings.Language);
        Loc.Language = AppEnv.Settings.Language;
        GTACoop.Sync.SyncText.Language = Loc.Language;

        Application.Run(new MainForm());

        // Dil değişikliğinden sonra yeniden başlatma istendiyse: mutex bırakıldıktan sonra başlat.
        var restart = MainForm.RestartRequested;
        try { mutex.ReleaseMutex(); } catch { }
        mutex.Dispose();

        if (restart)
        {
            try
            {
                Process.Start(new ProcessStartInfo(Application.ExecutablePath) { UseShellExecute = true });
            }
            catch { }
        }
    }
}
