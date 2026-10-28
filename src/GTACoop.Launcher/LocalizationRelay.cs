namespace GTACoop.Remake;

/// <summary>
/// 2.3.x relay-first join akışında eklenen yeni arayüz metinlerinin İngilizce
/// karşılıkları. Ana sözlük korunur; bu dosya yalnızca yeni anahtarları taşır.
/// </summary>
public static partial class Loc
{
    private static readonly Dictionary<string, string> RelayEn = new()
    {
        // --- Katılım kodu / bağlantı modu ---
        ["Bağlantı:"] = "Connection:",
        ["Otomatik (relay öncelikli)"] = "Automatic (relay preferred)",
        ["Doğrudan (aynı ağ / Radmin VPN)"] = "Direct (same network / Radmin VPN)",
        ["Varsayılan 'Otomatik' mod relay'i önce dener. Aynı Wi-Fi/hotspot veya Radmin VPN üzerindeyseniz 'Doğrudan' modunu seçebilirsiniz."] =
            "The default 'Automatic' mode tries the relay first. If you are on the same Wi-Fi/hotspot or Radmin VPN, you can choose 'Direct' mode.",
        ["Otomatik relay: {0} (sync {1}, oyun {2}) — relay öncelikli kullanılacak."] =
            "Automatic relay: {0} (sync {1}, game {2}) — the relay will be used first.",
        ["Bu kodda otomatik relay bilgisi yok; yalnızca doğrudan bağlantı denenir."] =
            "This code has no automatic relay information; only a direct connection will be tried.",
        ["Kısa kod: {0} — bağlanırken güncel relay bilgisi bu koddan alınır."] =
            "Short code: {0} — the current relay information is fetched from this code when connecting.",
        ["Bu uzun kod tek oturumluk relay bilgisi taşır; sunucu yeniden başlatıldıysa yeni kod isteyin."] =
            "This long code contains relay information for a single session; ask for a new code if the server was restarted.",
        ["Kısa kod da koda eklendi ({0}); sunucu yeniden başlarsa relay bilgisi bağlanırken otomatik yenilenir."] =
            "The short code is included in this code ({0}); if the server restarts, the relay information is refreshed automatically when connecting.",
        ["Zaten bir bağlantı işlemi sürüyor. Bitmesini bekleyin veya 'İptal' düğmesine basın."] =
            "A connection is already in progress. Wait for it to finish or press 'Cancel'.",
        ["Sunucuya ulaşılamadı: zaman aşımı."] =
            "Could not reach the server: timeout.",
        ["Sunucuya ulaşılamadı (zaman aşımı). Sunucu açık mı? Kısa kod/uzun kod güncel mi? 'Yeniden Ara' veya yeni kod ile tekrar deneyin."] =
            "Could not reach the server (timeout). Is the server running? Are the short/long codes current? Try 'Search Again' or use a new code.",
        ["Sunucuya ulaşılamadı (zaman aşımı). Sunucu açık mı? Kısa kod/uzun kod güncel mi? Yeni kod ile tekrar deneyin."] =
            "Could not reach the server (timeout). Is the server running? Are the short/long codes current? Try again with a new code.",
        ["Relay bilgisi yenileniyor; yeniden deneniyor... ({0}/{1})"] =
            "Refreshing relay information; retrying... ({0}/{1})",

        // --- Kısa kod araması ---
        ["Kısa kod henüz yayında değil; yeniden deneniyor... ({0}/{1})"] =
            "The short code is not published yet; retrying... ({0}/{1})",
        ["Sunucu bulundu; otomatik relay'in hazır olması bekleniyor... ({0}/{1})"] =
            "Server found; waiting for the automatic relay to become ready... ({0}/{1})",
        ["Sunucu bulundu; otomatik relay öncelikli deneniyor..."] =
            "Server found; trying the automatic relay first...",
        ["Sunucu bulundu; doğrudan adresler deneniyor..."] =
            "Server found; trying direct addresses...",
        ["Otomatik relay yanıt vermedi; uygun doğrudan adresler deneniyor..."] =
            "The automatic relay did not respond; trying suitable direct addresses...",
        ["Sunucuya ulaşılamadı: kısa kodda otomatik relay bilgisi yok ve doğrudan adresler aynı ağda değil.\r\nSunucu sahibinden sunucuyu yeniden başlatmasını veya güncel sürümü kullanmasını isteyin. Aynı Wi-Fi/hotspot'taysanız 'Doğrudan' modunu deneyin."] =
            "Could not reach the server: the short code has no automatic relay information and the direct addresses are not on the same network.\r\nAsk the server owner to restart the server or to use the current version. If you are on the same Wi-Fi/hotspot, try the 'Direct' mode.",
        ["Sunucuya ulaşılamadı. Otomatik relay ve doğrudan adresler denendi.\r\nSunucu açık mı? İkiniz de güncel sürümü kullanıyor musunuz? 'Yeniden Ara' ile tekrar deneyin."] =
            "Could not reach the server. The automatic relay and direct addresses were tried.\r\nIs the server running? Are you both using the current version? Try 'Search Again'.",
        ["Kısa kod bulundu ✓\r\nSunucu: {0}\r\nOtomatik relay: {1} (sync {2}, oyun {3})\r\nYedek doğrudan adres: {4}\r\nOyun portu: {5}  |  Sync portu: {6}\r\nŞifre: {7}"] =
            "Short code found ✓\r\nServer: {0}\r\nAutomatic relay: {1} (sync {2}, game {3})\r\nFallback direct address: {4}\r\nGame port: {5}  |  Sync port: {6}\r\nPassword: {7}",
        ["Kısa kod bulundu ✓\r\nSunucu: {0}\r\nAdres: {1}:{2} ({3})\r\nOyun portu: {4}  |  Sync portu: {5}\r\nŞifre: {6}"] =
            "Short code found ✓\r\nServer: {0}\r\nAddress: {1}:{2} ({3})\r\nGame port: {4}  |  Sync port: {5}\r\nPassword: {6}",

        // --- Bağlantı / relay hataları ---
        ["Sunucunun güncel relay bilgisi alınıyor..."] =
            "Getting the server's current relay information...",
        ["Otomatik relay bağlantısı kurulamadı. Sunucu ve launcher açık mı? İnternet bağlantınızı kontrol edip yeniden deneyin."] =
            "Could not establish the automatic relay connection. Are the server and launcher running? Check your internet connection and try again.",
        ["Otomatik relay bağlantısı kurulamadı; bu sunucuya doğrudan da ulaşılamıyor. Sunucu ve launcher açık mı? Güncel kodu kullanın."] =
            "Could not establish the automatic relay connection, and this server cannot be reached directly either. Are the server and launcher running? Use the current code.",
        ["Bu sunucuya doğrudan ulaşılamaz: adres özel bir ağda (ör. hotspot/ICS) ve otomatik relay bilgisi yok.\r\nSunucu sahibinden güncel kısa kodu veya uzun GTC1 kodunu isteyip 'Kod ile Katıl...' ile yeniden ekleyin."] =
            "This server cannot be reached directly: the address is on a private network (e.g. hotspot/ICS) and there is no automatic relay information.\r\nAsk the server owner for the current short code or long GTC1 code and add it again with 'Join with Code...'.",
        ["Bu sunucuya doğrudan ulaşılamaz: adres özel bir ağda ve otomatik relay bilgisi yok. Sunucu sahibinden güncel kısa kodu isteyin."] =
            "This server cannot be reached directly: the address is on a private network and there is no automatic relay information. Ask the server owner for the current short code.",

        // --- Sunucu tarafı relay durumu ---
        ["Otomatik relay yeniden deneniyor... (deneme {0})"] =
            "Retrying the automatic relay... (attempt {0})",
        ["Otomatik relay şu an kurulamadı; birkaç saniye içinde yeniden denenecek."] =
            "The automatic relay could not be established right now; it will be retried in a few seconds.",
        ["Adres: {0}  |  Oyun portu: {1}  |  Sync portu: {2}\r\nOtomatik relay koda eklendi: {3} (sync {4}, oyun {5}). Arkadaşınız relay öncelikli bağlanır."] =
            "Address: {0}  |  Game port: {1}  |  Sync port: {2}\r\nAutomatic relay added to the code: {3} (sync {4}, game {5}). Your friend will connect with the relay preferred.",

        // --- 2.3.4: özel relay sunucusu ---
        ["Relay sunucusu:"] = "Relay server:",
        ["bore.pub (varsayılan)"] = "bore.pub (default)",
        ["Relay sunucusu geçersiz. 'host' veya 'host:port' biçiminde yazın (ör. bore.pub veya relay.ornek.com:7835)."] =
            "Invalid relay server. Use 'host' or 'host:port' (for example bore.pub or relay.example.com:7835).",

        // --- 2.3.4: UPnP ---
        ["UPnP deneniyor..."] = "Trying UPnP...",
        ["uygun yerel ağ adresi bulunamadı"] = "no suitable local network address was found",
        ["bu Windows sürümünde UPnP desteği yok"] = "UPnP is not supported on this Windows version",
        ["UPnP başlatılamadı"] = "UPnP could not be started",
        ["yönlendirici UPnP'yi desteklemiyor veya kapalı"] =
            "the router does not support UPnP or it is disabled",
        ["UPnP hatası: {0}"] = "UPnP error: {0}",
        ["oyun UDP {0} ve sync TCP {1}"] = "game UDP {0} and sync TCP {1}",
        ["oyun UDP {0}"] = "game UDP {0}",
        ["sync TCP {0}"] = "sync TCP {0}",
        ["{0} yönlendirildi (yerel {1})."] = "{0} mapped (local {1}).",
        ["{0} yönlendirildi (yerel {1}, dış IP {2})."] = "{0} mapped (local {1}, external IP {2}).",
        ["UPnP kısmen hazır: yalnızca {0} yönlendirildi (yerel {1})."] =
            "UPnP partially ready: only {0} was mapped (local {1}).",
        ["UPnP portları yönlendirilemedi; relay kullanılacak."] =
            "UPnP ports could not be mapped; relay will be used.",

        // --- 2.3.4: tanılama ---
        ["Tanılama Kopyala"] = "Copy Diagnostics",
        ["Durum"] = "Status",
        ["Çevrimiçi"] = "Online",
        ["Çevrimdışı"] = "Offline",
        ["Kontrol ediliyor..."] = "Checking...",
        ["Bilinmiyor"] = "Unknown",
        ["Tanılama"] = "Diagnostics",
        ["İşletim sistemi: {0}"] = "Operating system: {0}",
        [".NET: {0}"] = ".NET: {0}",
        ["Dil: {0}"] = "Language: {0}",
        ["GTA klasörü: {0}"] = "GTA folder: {0}",
        ["(seçilmemiş)"] = "(not selected)",
        ["Sunucu: {0} | oyun: {1} UDP | sync: {2} TCP"] =
            "Server: {0} | game: {1} UDP | sync: {2} TCP",
        ["Relay sunucusu: {0}"] = "Relay server: {0}",
        ["Kısa kod: açık"] = "Short code: on",
        ["Kısa kod: kapalı"] = "Short code: off",
        ["(yok)"] = "(none)",
        ["Yerel adresler: {0}"] = "Local addresses: {0}",
        ["--- son 80 günlük satırı ---"] = "--- last 80 log lines ---",
        ["Tanılama bilgisi panoya kopyalandı. Bir sorun bildirirken bu metni yapıştırabilirsiniz."] =
            "Diagnostics copied to the clipboard. You can paste this text when reporting an issue.",
        ["Tanılama kopyalanamadı:\r\n"] = "Could not copy diagnostics:\r\n"
    };
}
