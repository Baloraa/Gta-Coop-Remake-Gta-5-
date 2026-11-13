# VFS / WPF arayüz güncellemesi — 2026-11-13

- WPF sol gezinme alanına daralt/genişlet kontrolü eklendi. Daraltılmış görünümde simgeler ve araç ipuçları kalır; ana içerik alanı genişler.
- WPF başlık/alt başlık ve topluluk etiketi Türkçe kaynak metinlere bağlandı; İngilizce karşılıkları localization sözlüğüne eklendi.
- WPF gezinme bölümü ve ana sayfa metinleri için eksik temel İngilizce localization anahtarları eklendi.
- DeepSeek için tüm mod türlerini (DLC, OIV, ASI, Script) kapsayan VFS araştırma/uygulama, Türkçe metin denetimi ve Windows Release test talimatı eklendi: `DEEPSEEK-SONRAKI-ADIMLAR-VFS-UI-TR.txt`.

## VFS kapsamı hakkında dürüst durum

Bu güncelleme genel amaçlı native GTA V VFS hook'u eklemez. Önceki DLC junction kodu korunmuştur. OIV, ASI ve Script dosyalarının tümünü gerçek zamanlı sanallaştırmak; GTA V'in yükleyicileri ve ilgili loader sürümleriyle uyumlu bir yönlendirme katmanı gerektirir. Bu çalışma ortamında Windows/GTA oyun testi yapılamadığından, doğrulanmamış bir hook eklenmemiştir.
