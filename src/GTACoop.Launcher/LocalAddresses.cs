using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace GTACoop.Remake;

/// <summary>Yerel ağ arayüzündeki bir IPv4 adresi.</summary>
internal sealed record LocalAddress(string Ip, string Adapter)
{
    public override string ToString() => $"{Ip}  ({Adapter})";
}

/// <summary>Bilgisayardaki kullanılabilir IPv4 adreslerini bulur (internet gerektirmez).</summary>
internal static class LocalAddresses
{
    public static List<LocalAddress> Detect()
    {
        var list = new List<LocalAddress>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up) continue;
                if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

                foreach (var ua in ni.GetIPProperties().UnicastAddresses)
                {
                    if (ua.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                    var ip = ua.Address.ToString();
                    if (ip.StartsWith("169.254.")) continue; // Windows'un otomatik (APIPA) adresi
                    if (!seen.Add(ip)) continue;
                    list.Add(new LocalAddress(ip, ni.Name));
                }
            }
        }
        catch { /* adres listesi kritik değil; kullanıcı elle yazabilir */ }

        // Radmin VPN (26.x) ve ev/hotspot ağları listenin başında görünsün.
        list.Sort((a, b) => Score(a.Ip).CompareTo(Score(b.Ip)));
        return list;
    }

    private static int Score(string ip) =>
        ip.StartsWith("26.") ? 0 :
        ip.StartsWith("192.168.") ? 1 :
        ip.StartsWith("10.") ? 2 :
        ip.StartsWith("172.") ? 3 : 4;

    /// <summary>Adres bu bilgisayarın ağ arayüzlerinden birine mi ait?</summary>
    public static bool IsLocalAddress(string? host)
    {
        if (!IPAddress.TryParse((host ?? "").Trim(), out var target) ||
            target.AddressFamily != AddressFamily.InterNetwork)
            return false;

        if (IPAddress.IsLoopback(target)) return true;

        try
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up) continue;
                if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

                foreach (var ua in ni.GetIPProperties().UnicastAddresses)
                {
                    if (ua.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                    if (ua.Address.Equals(target)) return true;
                }
            }
        }
        catch { /* arayüz bilgisi alınamadı */ }

        return false;
    }

    /// <summary>
    /// Adres, bu bilgisayarın bağlı olduğu bir ağın alt ağında mı?
    /// Aynı Wi-Fi/hotspot'taki arkadaşlar doğrudan bağlanabilir.
    /// </summary>
    public static bool IsSameSubnet(string? host)
    {
        if (!IPAddress.TryParse((host ?? "").Trim(), out var target) ||
            target.AddressFamily != AddressFamily.InterNetwork)
            return false;

        try
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up) continue;
                if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

                foreach (var ua in ni.GetIPProperties().UnicastAddresses)
                {
                    if (ua.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                    if (ua.IPv4Mask == null) continue;
                    if (InSameSubnet(target, ua.Address, ua.IPv4Mask)) return true;
                }
            }
        }
        catch { /* arayüz bilgisi alınamadı */ }

        return false;
    }

    /// <summary>
    /// Doğrudan bağlantı bu adres üzerinden denenmeye değer mi?
    /// Genel IP'ler, alan adları, aynı alt ağ ve Radmin/Tailscale gibi sanal
    /// ağlar kabul edilir. Windows ICS (192.168.137.x) gibi yalnızca kendi
    /// hotspot'una açık adresler, aynı alt ağda değilsek reddedilir.
    /// </summary>
    public static bool IsDirectReachable(string? host)
    {
        var text = (host ?? "").Trim();
        if (text.Length == 0) return false;

        if (!IPAddress.TryParse(text, out var ip))
            return true; // alan adı / DNS adı; doğrudan denenebilir

        if (ip.AddressFamily != AddressFamily.InterNetwork) return false;
        if (IPAddress.IsLoopback(ip)) return true;

        var b = ip.GetAddressBytes();

        // Radmin VPN (26.x) ve Tailscale (100.64/10) sanal ağları.
        if (b[0] == 26) return true;
        if (b[0] == 100 && b[1] is >= 64 and <= 127) return true;

        if (IsPublicIPv4(ip)) return true;

        // Özel ağ adresi: yalnızca gerçekten aynı alt ağdaysak doğrudan dene.
        return IsSameSubnet(text);
    }

    /// <summary>Adres özel/CGNAT/link-local mı (internetten doğrudan erişilemez)?</summary>
    public static bool IsPrivateIPv4(IPAddress ip)
    {
        if (ip.AddressFamily != AddressFamily.InterNetwork) return false;
        var b = ip.GetAddressBytes();
        if (b[0] is 0 or 10 or 127) return true;
        if (b[0] == 100 && b[1] is >= 64 and <= 127) return true; // CGNAT (Tailscale dahil)
        if (b[0] == 169 && b[1] == 254) return true;
        if (b[0] == 172 && b[1] is >= 16 and <= 31) return true;
        if (b[0] == 192 && b[1] == 168) return true;
        return false;
    }

    /// <summary>Adres internete açılabilir bir IPv4 mü?</summary>
    public static bool IsPublicIPv4(IPAddress ip)
    {
        if (ip.AddressFamily != AddressFamily.InterNetwork) return false;
        if (IsPrivateIPv4(ip)) return false;
        var b = ip.GetAddressBytes();
        return b[0] < 224; // multicast/reserved değil
    }

    private static bool InSameSubnet(IPAddress a, IPAddress b, IPAddress mask)
    {
        var ab = a.GetAddressBytes();
        var bb = b.GetAddressBytes();
        var mb = mask.GetAddressBytes();
        if (ab.Length != 4 || bb.Length != 4 || mb.Length != 4) return false;

        for (int i = 0; i < 4; i++)
        {
            if ((ab[i] & mb[i]) != (bb[i] & mb[i])) return false;
        }
        return true;
    }
}
