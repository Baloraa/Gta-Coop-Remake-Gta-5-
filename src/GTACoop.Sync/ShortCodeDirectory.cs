using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace GTACoop.Sync;

/// <summary>
/// Kısa katılım kodu ("Gel.com" gibi): sunucu bağlantı bilgileri, koddan türetilen
/// bir konuya AES-GCM ile şifrelenmiş olarak yayınlanır. Varsayılan servis ntfy.sh'tir;
/// hesap gerekmez. Uzun GTC1 kodu internet olmadan da çalışmaya devam eder.
///
/// Konu adı koddan SHA-256 ile türetilir; kodun kendisi servise gönderilmez.
/// Yük (adresler, portlar, şifre, token) koddan türetilen anahtarla şifrelenir.
/// </summary>
public static class ShortCodeDirectory
{
    public const int MinLength = 3;
    public const int MaxLength = 64;

    /// <summary>Sunucu açıkken yayın tekrar aralığı (saniye).</summary>
    public const int HeartbeatSeconds = 60;

    private const string TopicPrefix = "gtacoop-v1-";
    private const string LookupSince = "10m";
    private const byte BlobVersion = 1;
    private const int NonceSize = 12;
    private const int TagSize = 16;

    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(10)
    };

    /// <summary>Kısa kod servisinin kökü. GTACOOP_SHORTCODE_URL ile değiştirilebilir.</summary>
    public static string BaseUrl { get; set; } = ResolveBaseUrl();

    private sealed class OfferPayload
    {
        public int v { get; set; } = 1;
        public bool off { get; set; }
        public string n { get; set; } = "";
        public string[] h { get; set; } = Array.Empty<string>();
        public int g { get; set; } = 4499;
        public int s { get; set; } = 4599;
        public string? p { get; set; }
        public string? t { get; set; }
        public long ts { get; set; }

        // Otomatik relay (bore.pub uyumlu TCP tüneli). Eski istemciler bu alanları yok sayar.
        public string? rh { get; set; }
        public int rs { get; set; }
        public int rg { get; set; }
    }

    /// <summary>Kısa kodun çözdüğü sunucu bilgisi.</summary>
    public sealed record ShortCodeOffer(
        string Name,
        IReadOnlyList<string> Hosts,
        int GamePort,
        int SyncPort,
        string Password,
        string SyncToken,
        long TimestampUnix,
        string RelayHost = "",
        int RelaySyncPort = 0,
        int RelayGamePort = 0);

    /// <summary>
    /// Kullanıcının yazdığı kısa kodu doğrular ve karşılaştırma için küçük harfe çevirir.
    /// Yalnızca ASCII harf, rakam, nokta, tire ve alt çizgi kabul edilir.
    /// </summary>
    public static bool TryNormalize(string? input, out string normalized, out string error)
    {
        normalized = "";
        error = "";
        var code = (input ?? "").Trim();

        if (code.Length == 0)
        {
            error = "Kod boş.";
            return false;
        }

        if (code.StartsWith(JoinCode.Prefix, StringComparison.OrdinalIgnoreCase))
        {
            error = "Bu bir uzun GTC1 kodu; kısa kod değil.";
            return false;
        }

        if (code.Length < MinLength)
        {
            error = $"Kısa kod en az {MinLength} karakter olmalı.";
            return false;
        }

        if (code.Length > MaxLength)
        {
            error = $"Kısa kod en fazla {MaxLength} karakter olabilir.";
            return false;
        }

        foreach (var c in code)
        {
            var ok = c is >= 'a' and <= 'z'
                or >= 'A' and <= 'Z'
                or >= '0' and <= '9'
                or '.' or '-' or '_';
            if (!ok)
            {
                error = "Kısa kodda yalnızca A-Z, a-z, 0-9, nokta (.), tire (-) ve alt çizgi (_) kullanılabilir.";
                return false;
            }
        }

        normalized = code.ToLowerInvariant();
        return true;
    }

    public static bool IsShortCode(string? input) =>
        TryNormalize(input, out _, out _);

    /// <summary>Koddan ntfy konu adını üretir (kodu düz metin olarak göndermez).</summary>
    public static string TopicFor(string normalizedCode)
    {
        var hash = SHA256.HashData(
            Encoding.UTF8.GetBytes("GTACoopShortCodeTopic|v1|" + normalizedCode));
        return TopicPrefix + ToBase64Url(hash);
    }

    /// <summary>Sunucu bilgisini kısa kod konusuna yayınlar.</summary>
    public static async Task PublishAsync(string code, ShortCodeOffer offer, CancellationToken ct)
    {
        if (!TryNormalize(code, out var normalized, out var error))
            throw new SyncException("Kısa kod geçersiz: " + error);

        var payload = new OfferPayload
        {
            n = offer.Name ?? "",
            h = (offer.Hosts ?? Array.Empty<string>())
                .Where(h => !string.IsNullOrWhiteSpace(h))
                .Select(h => h.Trim())
                .ToArray(),
            g = offer.GamePort,
            s = offer.SyncPort,
            p = string.IsNullOrEmpty(offer.Password) ? null : offer.Password,
            t = string.IsNullOrEmpty(offer.SyncToken) ? null : offer.SyncToken,
            ts = offer.TimestampUnix > 0
                ? offer.TimestampUnix
                : DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            rh = string.IsNullOrWhiteSpace(offer.RelayHost) ? null : offer.RelayHost.Trim(),
            rs = offer.RelaySyncPort is > 0 and <= 65535 ? offer.RelaySyncPort : 0,
            rg = offer.RelayGamePort is > 0 and <= 65535 ? offer.RelayGamePort : 0
        };

        await PublishPayloadAsync(normalized, payload, ct);
    }

    /// <summary>Sunucu kapanırken kısa kodu "çevrimdışı" olarak işaretler.</summary>
    public static async Task PublishOfflineAsync(string code, CancellationToken ct)
    {
        if (!TryNormalize(code, out var normalized, out _)) return;
        await PublishPayloadAsync(normalized, new OfferPayload
        {
            off = true,
            ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        }, ct);
    }

    private static async Task PublishPayloadAsync(string normalized, OfferPayload payload, CancellationToken ct)
    {
        var blob = Encrypt(normalized, payload);
        var topic = TopicFor(normalized);

        using var req = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/{topic}");
        req.Content = new StringContent(blob, Encoding.UTF8, "text/plain");
        req.Headers.TryAddWithoutValidation("Title", "GTACoop");
        req.Headers.TryAddWithoutValidation("Priority", "min");

        using var resp = await Http.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode)
            throw new SyncException(
                $"Kısa kod servisi hata verdi: {(int)resp.StatusCode} {resp.ReasonPhrase}");
    }

    /// <summary>
    /// Kısa kodu arar; en güncel yayını çözer. Yayın yoksa veya sunucu çevrimdışı
    /// işaretlediyse <c>null</c> döner. Servise ulaşılamazsa SyncException fırlatır.
    /// </summary>
    public static async Task<ShortCodeOffer?> LookupAsync(string code, CancellationToken ct)
    {
        if (!TryNormalize(code, out var normalized, out var error))
            throw new SyncException("Kısa kod geçersiz: " + error);

        var topic = TopicFor(normalized);
        var url = $"{BaseUrl}/{topic}/json?poll=1&since={LookupSince}";

        // ntfy'nin önbelleği yayından hemen sonra birkaç yüz ms gecikebilir.
        // Boş yanıtta kısa aralıklarla birkaç kez dene.
        string? body = null;
        for (int attempt = 0; attempt < 3; attempt++)
        {
            body = await FetchTopicAsync(url, ct);
            if (body == null) return null; // konu henüz yok (404)

            if (string.IsNullOrWhiteSpace(body))
            {
                if (attempt < 2) { await Task.Delay(700, ct); continue; }
                return null;
            }

            var payload = ParseNewestPayload(body, normalized);
            if (payload == null) return null;

            if (payload.off)
            {
                // Eski "çevrimdışı" işareti görünüp yeni yayın henüz önbelleğe
                // düşmemiş olabilir; kısa bir süre daha dene.
                if (attempt < 2) { await Task.Delay(700, ct); continue; }
                return null;
            }

            var offer = ToOffer(payload);
            if (offer == null) return null;

            // Yayıncı 60 saniyede bir kalp atışı gönderir. 5 dakikadan eski bir
            // teklif, sunucu kapanmış (veya uygulama çökmüş) olsa bile önbellekte
            // kalmış olabilir; çevrimdışı say ve yenisini kısa süre bekle.
            var age = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - offer.TimestampUnix;
            if (offer.TimestampUnix > 0 && Math.Abs(age) > 300)
            {
                if (attempt < 2) { await Task.Delay(700, ct); continue; }
                return null;
            }

            return offer;
        }

        return null;
    }

    private static OfferPayload? ParseNewestPayload(string body, string normalized)
    {
        // ntfy poll yanıtı satır başına bir JSON nesnesidir (NDJSON).
        (long Time, string Message)? newest = null;
        foreach (var line in body.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                using var doc = JsonDocument.Parse(line);
                var root = doc.RootElement;

                if (root.TryGetProperty("event", out var ev) &&
                    !string.Equals(ev.GetString(), "message", StringComparison.Ordinal))
                    continue;

                if (!root.TryGetProperty("message", out var msgEl)) continue;
                var msg = msgEl.GetString();
                if (string.IsNullOrWhiteSpace(msg)) continue;

                long time = root.TryGetProperty("time", out var tEl) && tEl.TryGetInt64(out var t)
                    ? t
                    : 0;

                if (newest == null || time >= newest.Value.Time)
                    newest = (time, msg);
            }
            catch (JsonException)
            {
                // Bozuk satırı atla; diğer yayınlar denenebilir.
            }
        }

        return newest == null ? null : Decrypt(normalized, newest.Value.Message);
    }

    private static ShortCodeOffer? ToOffer(OfferPayload payload)
    {
        if (payload.v != 1) return null;

        var hosts = (payload.h ?? Array.Empty<string>())
            .Select(h => (h ?? "").Trim())
            .Where(h => h.Length is > 0 and <= 255)
            .Where(h => IPAddress.TryParse(h, out var ip) &&
                        ip.AddressFamily == AddressFamily.InterNetwork)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(12)
            .ToArray();

        var relayHost = NormalizeRelayHost(payload.rh);
        var relaySync = payload.rs is > 0 and <= 65535 ? payload.rs : 0;
        var relayGame = payload.rg is > 0 and <= 65535 ? payload.rg : 0;
        if (relayHost.Length == 0 || relaySync == 0 || relayGame == 0)
        {
            relayHost = "";
            relaySync = 0;
            relayGame = 0;
        }

        // Relay tek başına yeterli olabilir; doğrudan adres yoksa da teklifi kabul et.
        if (hosts.Length == 0 && relayHost.Length == 0) return null;

        var gamePort = payload.g is >= 1 and <= 65535 ? payload.g : 4499;
        var syncPort = payload.s is >= 1 and <= 65535 ? payload.s : 4599;
        var name = (payload.n ?? "").Trim();
        if (name.Length == 0) name = "GTACoop Sunucu";
        if (name.Length > 128) name = name[..128];

        return new ShortCodeOffer(
            name, hosts, gamePort, syncPort,
            payload.p ?? "", payload.t ?? "", payload.ts,
            relayHost, relaySync, relayGame);
    }

    /// <summary>
    /// Relay sunucusu alan adı (ör. bore.pub) veya IPv4 adresi. Şema/port kabul edilmez;
    /// yalnızca harf, rakam, nokta ve tire kullanılabilir.
    /// </summary>
    private static string NormalizeRelayHost(string? input)
    {
        var host = (input ?? "").Trim();
        if (host.Length is 0 or > 255) return "";
        foreach (var c in host)
        {
            var ok = c is >= 'a' and <= 'z'
                or >= 'A' and <= 'Z'
                or >= '0' and <= '9'
                or '.' or '-';
            if (!ok) return "";
        }
        if (host.StartsWith('.') || host.EndsWith('.') || host.Contains("..")) return "";
        return host;
    }

    private static async Task<string?> FetchTopicAsync(string url, CancellationToken ct)
    {
        try
        {
            using var resp = await Http.GetAsync(url, ct);
            if (!resp.IsSuccessStatusCode)
            {
                if ((int)resp.StatusCode == 404) return null;
                throw new SyncException(
                    $"Kısa kod servisi hata verdi: {(int)resp.StatusCode} {resp.ReasonPhrase}");
            }
            return await resp.Content.ReadAsStringAsync(ct);
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new SyncException(
                "Kısa kod servisi yanıt vermedi (zaman aşımı). İnternet bağlantınızı kontrol edin.");
        }
        catch (HttpRequestException ex)
        {
            throw new SyncException("Kısa kod servisine ulaşılamadı: " + ex.Message);
        }
    }

    /// <summary>Sunucunun genel IPv4 adresini bulur; bulunamazsa null.</summary>
    public static async Task<string?> GetPublicIpAsync(CancellationToken ct)
    {
        try
        {
            using var resp = await Http.GetAsync("https://api.ipify.org", ct);
            if (!resp.IsSuccessStatusCode) return null;

            var text = (await resp.Content.ReadAsStringAsync(ct)).Trim();
            return IPAddress.TryParse(text, out var ip) && IsPublicIPv4(ip)
                ? text
                : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Adres gerçekten internete açılabilir bir IPv4 mü? Özel ağ, CGNAT,
    /// loopback, link-local ve çoklu yayın adresleri genel IP sayılmaz.
    /// </summary>
    public static bool IsPublicIPv4(IPAddress ip)
    {
        if (ip.AddressFamily != AddressFamily.InterNetwork) return false;
        var b = ip.GetAddressBytes();
        if (b[0] == 0 || b[0] == 10 || b[0] == 127) return false;
        if (b[0] == 100 && b[1] is >= 64 and <= 127) return false; // CGNAT
        if (b[0] == 169 && b[1] == 254) return false;              // link-local
        if (b[0] == 172 && b[1] is >= 16 and <= 31) return false;
        if (b[0] == 192 && b[1] == 168) return false;
        if (b[0] >= 224) return false;                             // multicast/reserved
        return true;
    }

    private static string Encrypt(string normalized, OfferPayload payload)
    {
        var key = DeriveKey(normalized);
        var plain = JsonSerializer.SerializeToUtf8Bytes(payload, SyncJson.Options);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var cipher = new byte[plain.Length];
        var tag = new byte[TagSize];

        using (var aes = new AesGcm(key, TagSize))
            aes.Encrypt(nonce, plain, cipher, tag);

        var blob = new byte[1 + NonceSize + TagSize + cipher.Length];
        blob[0] = BlobVersion;
        Buffer.BlockCopy(nonce, 0, blob, 1, NonceSize);
        Buffer.BlockCopy(tag, 0, blob, 1 + NonceSize, TagSize);
        Buffer.BlockCopy(cipher, 0, blob, 1 + NonceSize + TagSize, cipher.Length);
        return ToBase64Url(blob);
    }

    private static OfferPayload? Decrypt(string normalized, string blobText)
    {
        try
        {
            var blob = FromBase64Url(blobText.Trim());
            if (blob.Length < 1 + NonceSize + TagSize || blob[0] != BlobVersion)
                return null;

            var nonce = blob.AsSpan(1, NonceSize);
            var tag = blob.AsSpan(1 + NonceSize, TagSize);
            var cipher = blob.AsSpan(1 + NonceSize + TagSize);
            var plain = new byte[cipher.Length];
            var key = DeriveKey(normalized);

            using (var aes = new AesGcm(key, TagSize))
                aes.Decrypt(nonce, cipher, tag, plain);

            return JsonSerializer.Deserialize<OfferPayload>(plain, SyncJson.Options);
        }
        catch
        {
            return null;
        }
    }

    private static byte[] DeriveKey(string normalized) =>
        SHA256.HashData(Encoding.UTF8.GetBytes("GTACoopShortCodeKey|v1|" + normalized));

    private static string ResolveBaseUrl()
    {
        var env = Environment.GetEnvironmentVariable("GTACOOP_SHORTCODE_URL");
        return string.IsNullOrWhiteSpace(env)
            ? "https://ntfy.sh"
            : env.Trim().TrimEnd('/');
    }

    private static string ToBase64Url(byte[] data) =>
        Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] FromBase64Url(string text)
    {
        var s = text.Replace('-', '+').Replace('_', '/');
        s = (s.Length % 4) switch
        {
            2 => s + "==",
            3 => s + "=",
            _ => s
        };
        return Convert.FromBase64String(s);
    }
}
