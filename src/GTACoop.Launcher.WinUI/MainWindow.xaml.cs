using Microsoft.UI.Xaml.Controls;

namespace GTACoop.Remake.WinUI;

public sealed partial class MainWindow : Microsoft.UI.Xaml.Window
{
    public MainWindow()
    {
        InitializeComponent();
        Nav.SelectedItem = Nav.MenuItems[0];
    }

    private void OnNavSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is not NavigationViewItem item) return;

        SectionTitle.Text = item.Content?.ToString() ?? "";
        SectionHint.Text = item.Tag switch
        {
            "servers" => "Kayıtlı sunucular, hızlı bağlan ve düzenleme işlemleri buraya taşınacak.",
            "myserver" => "Kendi sunucunu başlatma, portlar ve kısa kod buraya taşınacak.",
            "browser" => "Genel sunucu listesi, LAN taraması ve filtreler buraya taşınacak.",
            "game" => "Oyun yolu, yedekleme ve başlatma ayarları buraya taşınacak.",
            "hud" => "HUD tasarımcısı ve oyuncu giriş/çıkış bildirimleri buraya taşınacak.",
            "components" => "SHVDN, ScriptHook ve bileşen kurulumları buraya taşınacak.",
            "p2p" => "Dosya paylaşımı ve kod üretimi buraya taşınacak.",
            "log" => "Canlı günlük ve destek paketi buraya taşınacak.",
            _ => "Sürüm bilgisi ve güncelleme denetimi buraya taşınacak."
        };
    }
}
