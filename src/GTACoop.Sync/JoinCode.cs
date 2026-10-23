using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace GTACoop.Sync;

/// <summary>
/// Katılım kodu: sunucu bağlantı bilgilerini (ad, adres, portlar, şifre, token)
/// tek bir metne paketler. Sunucu/internet gerektirmez; kodun kendisi bilgileri taşır.
/// Biçim: <c>GTC1.</c> + sıkıştırılmış (Deflate) + Base64Url metin.
/// </summary>
public static class JoinCode
{
    /// <summary>Kodların başladığı sabit önek.</summary>
    public const string Prefix = "GTC1.";

    private sealed class Payload
    {
        public string n { get; set; } = "";
        public string h { get; set; } = "";
        public int g { get; set; } = 4499;
        public int s { get; set; } = 4599;
        public string? p { get; set; }
        public string? t { get; set; }
    }

    /// <summary>Sunucu kaydından katılım kodu üretir.</summary>
    public static string Create(ServerEntry entry)
    {
        var payload = new Payload
        {
            n = entry.Name ?? "",
            h = entry.Host ?? "",
            g = entry.GamePort,
            s = entry.SyncPort,
            p = string.IsNullOrEmpty(entry.Password) ? null : entry.Password,
            t = string.IsNullOrEmpty(entry.SyncToken) ? null : entry.SyncToken
        };

        var json = JsonSerializer.SerializeToUtf8Bytes(payload);
        using var ms = new MemoryStream();
        using (var ds = new DeflateStream(ms, CompressionLevel.Optimal, leaveOpen: true))
            ds.Write(json, 0, json.Length);

        return Prefix + ToBase64Url(ms.ToArray());
    }

    /// <summary>
    /// Katılım kodunu çözer. Metin kopyala-yapıştır sırasında satırlara bölünmüş
    /// olsa bile boşluklar temizlenerek denenir.
    /// </summary>
    public static bool TryParse(string? code, out ServerEntry? entry, out string error)
    {
        entry = null;
        error = "";

        if (string.IsNullOrWhiteSpace(code))
        {
            error = "Kod boş.";
            return false;
        }

        // Satır sonları/boşluklar (sohbet uygulamaları kodu bölebilir) temizlenir.
        var cleaned = new string(code.Where(c => !char.IsWhiteSpace(c)).ToArray());
        if (cleaned.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
            cleaned = cleaned[Prefix.Length..];
        else if (cleaned.Length > 0 && cleaned[0] == '\uFEFF')
            cleaned = cleaned[1..];

        try
        {
            var compressed = FromBase64Url(cleaned);
            using var input = new MemoryStream(compressed);
            using var ds = new DeflateStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            ds.CopyTo(output);

            var payload = JsonSerializer.Deserialize<Payload>(output.ToArray());
            if (payload == null)
            {
                error = "Kod içeriği okunamadı.";
                return false;
            }

            var host = (payload.h ?? "").Trim();
            if (host.Length == 0)
            {
                error = "Kodda sunucu adresi yok.";
                return false;
            }

            if (host.Contains(' '))
            {
                error = "Sunucu adresi geçersiz.";
                return false;
            }

            var gamePort = payload.g is >= 1 and <= 65535 ? payload.g : 4499;
            var syncPort = payload.s is >= 1 and <= 65535 ? payload.s : 4599;

            var name = (payload.n ?? "").Trim();
            if (name.Length == 0) name = host;

            entry = new ServerEntry
            {
                Name = name,
                Host = host,
                GamePort = gamePort,
                SyncPort = syncPort,
                Password = payload.p ?? "",
                SyncToken = payload.t ?? "",
                AutoConnect = true
            };
            return true;
        }
        catch (Exception)
        {
            error = "Kod okunamadı (eksik veya bozuk olabilir).";
            return false;
        }
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
