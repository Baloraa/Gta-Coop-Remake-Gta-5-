# WPF geçiş durumu

Bu paket WPF kabuğunu mevcut ürünün aynı çalıştırılabilir dosya adına bağlar: `GTACoop.Launcher.exe`. Mevcut launcher mantık dosyaları WPF projesine kaynak olarak bağlanır; böylece ayrı bir Legacy DLL gerekmez, WPF uygulaması ürünün beklediği exe adını ve mevcut self-updater yayın varlığını korur. `wpf-kur.ps1` var olan kurulum klasörünü ve aynı masaüstü kısayol adını kullanır; mevcut exe'yi ilk seferde `.pre-wpf.bak` olarak yedekler.

**Önemli dürüstlük notu:** Bu, tam WPF portu değildir. Dashboard ve ana pencere WPF'dir; sunucu, oyun, P2P, HUD, bileşenler, Addon Peds ve günlük gibi mevcut sekmeler hâlâ `WindowsFormsHost` ile eski WinForms kontrollerini kullanır. `UseWindowsForms` bu nedenle şimdilik kapatılamaz. Bu sekmelerin her birinin WPF `UserControl` / `Page` olarak yeniden yazılması, mevcut iş mantığının UI'dan ayrılması ve Windows üzerinde etkileşimli test edilmesi gerekir.

Bu aşamada exe adı korunmuştur; WPF'ye tam geçiş tamamlandı iddiası yoktur.
