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
}
