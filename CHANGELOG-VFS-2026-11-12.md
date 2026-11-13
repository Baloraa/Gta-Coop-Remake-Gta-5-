# VFS / DLC mount güvenlik güncellemesi — 2026-11-12

- DLC junction oluşturma `cmd.exe /c mklink` yerine Windows `DeviceIoControl` reparse-point API'sine taşındı.
- Junction hedefi `ResolveLinkTarget` ile doğrulanıyor.
- Unmount işlemi yalnızca state kaydındaki hedefle eşleşen junction'ı kaldırıyor; cache hedefi korunuyor.
- DLC güncellemesi, sahipliği doğrulanamayan mevcut klasörün üzerine yazmıyor.
- Staging/backup çakışmalarında mevcut dosya/klasörler otomatik silinmek yerine korunuyor.
- NTFS junction sahiplik ve cache koruma smoke testi eklendi.
- VFS dokümanı gerçek kapsamı ve fiziksel kurulum gerektiren mod türlerini açıklayacak şekilde güncellendi.

Not: Bu değişiklik genel amaçlı native GTA V VFS hook'u değildir; DLC klasörleri için junction tabanlı overlay mount'tur. Windows Release derlemesi ve GTA V oyun içi doğrulaması bu çalışma ortamında yapılamadı.
