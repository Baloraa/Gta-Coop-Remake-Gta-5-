using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace GTACoop.Remake.Wpf.Pages;

/// <summary>Günlük sayfası: bugünün launcher günlüğünü gösterir (gerçek dosya).</summary>
public sealed class LogPage : DockPanel
{
    private static string LogFile => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "GTACoopRemake", "logs", $"launcher-{DateTime.Now:yyyyMMdd}.log");

    private readonly TextBox _text = new()
    {
        IsReadOnly = true,
        AcceptsReturn = true,
        FontFamily = new System.Windows.Media.FontFamily("Consolas"),
        FontSize = 12,
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
        TextWrapping = TextWrapping.NoWrap
    };

    private readonly TextBlock _path = new();

    public LogPage()
    {
        LastChildFill = true;

        var header = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
        header.Children.Add(new TextBlock
        {
            Text = "Günlük",
            Style = (Style)Application.Current.Resources["H1"],
            Margin = new Thickness(0, 0, 0, 4)
        });

        _path.Style = (Style)Application.Current.Resources["Muted"];
        _path.Margin = new Thickness(0, 0, 0, 10);
        header.Children.Add(_path);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal };
        var refresh = new Button { Content = "Yenile" };
        refresh.Click += (_, _) => Reload();
        var open = new Button { Content = "Klasörü aç", Margin = new Thickness(8, 0, 0, 0) };
        open.Click += (_, _) => OpenFolder();
        buttons.Children.Add(refresh);
        buttons.Children.Add(open);
        header.Children.Add(buttons);

        SetDock(header, Dock.Top);
        Children.Add(header);
        Children.Add(_text);

        Reload();
    }

    private void Reload()
    {
        _path.Text = LogFile;

        try
        {
            _text.Text = File.Exists(LogFile)
                ? File.ReadAllText(LogFile)
                : "Bugün için günlük dosyası yok. Launcher bir kez çalıştığında burada görünür.";
            _text.ScrollToEnd();
        }
        catch (Exception ex)
        {
            _text.Text = "Günlük okunamadı: " + ex.Message;
        }
    }

    private void OpenFolder()
    {
        try
        {
            var folder = Path.GetDirectoryName(LogFile);
            if (folder == null) return;
            Directory.CreateDirectory(folder);
            Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true });
        }
        catch
        {
            // Klasör açılamazsa sayfa çalışmaya devam eder.
        }
    }
}
