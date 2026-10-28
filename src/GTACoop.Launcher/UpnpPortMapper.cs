using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace GTACoop.Remake;

/// <summary>
/// Windows'un yerleşik UPnP istemcisi (HNetCfg.NATUPnP) ile oyun UDP ve sync
/// TCP portlarını yönlendirir. Tamamen isteğe bağlıdır: yönlendirici UPnP'yi
/// desteklemiyorsa veya kapalıysa launcher relay ile çalışmaya devam eder.
/// Yalnızca bu sınıfın eklediği eşlemeler durdurulurken kaldırılır; mevcut
/// veya başka bir uygulamaya ait eşlemelere dokunulmaz.
/// </summary>
internal sealed class UpnpPortMapper : IDisposable
{
    private const string Description = "GTACoop Remake";
    private const int DiscoveryTimeoutMs = 15000;
    private const int CleanupTimeoutMs = 3000;

    private readonly Action<string> _log;
    private readonly List<(int Port, string Protocol)> _owned = new();
    private readonly object _gate = new();
    private volatile bool _disposed;

    public UpnpPortMapper(Action<string> log) => _log = log;

    /// <summary>Kullanıcıya gösterilecek durum; Loc.T ile yerelleştirilir.</summary>
    public string Status { get; private set; } = "";
    public bool IsMapped { get; private set; }
    public string ExternalIp { get; private set; } = "";
    public string InternalClient { get; private set; } = "";

    /// <summary>Keşfi arka planda başlatır; UI iş parçacığını asla bekletmez.</summary>
    public void Start(int gamePort, int syncPort)
    {
        _ = Task.Run(() =>
        {
            try
            {
                RunOnSta(() => Map(gamePort, syncPort), DiscoveryTimeoutMs);
            }
            catch (TimeoutException)
            {
                _log("UPnP: yönlendirici 15 saniye içinde yanıt vermedi; arka planda denenecek.");
            }
            catch (Exception ex)
            {
                Complete(T("UPnP hatası: {0}", ex.Message));
            }
        });
    }

    private void Map(int gamePort, int syncPort)
    {
        if (_disposed) return;

        var client = PickInternalClient();
        if (client == null)
        {
            Complete(T("uygun yerel ağ adresi bulunamadı"));
            return;
        }
        InternalClient = client;

        var type = Type.GetTypeFromProgID("HNetCfg.NATUPnP");
        if (type == null)
        {
            Complete(T("bu Windows sürümünde UPnP desteği yok"));
            return;
        }

        object? nat = null;
        try
        {
            nat = Activator.CreateInstance(type);
            if (nat == null)
            {
                Complete(T("UPnP başlatılamadı"));
                return;
            }

            dynamic com = nat;
            object? mappings;
            try
            {
                mappings = com.StaticPortMappingCollection;
            }
            catch
            {
                Complete(T("yönlendirici UPnP'yi desteklemiyor veya kapalı"));
                return;
            }

            if (mappings == null)
            {
                // Windows UPnP servisi çalışsa bile yönlendirici UPnP'yi
                // kapatmışsa koleksiyon boş döner.
                Complete(T("yönlendirici UPnP'yi desteklemiyor veya kapalı"));
                return;
            }

            try
            {
                ExternalIp = Convert.ToString(((dynamic)mappings).ExternalIPAddress) ?? "";
            }
            catch
            {
                ExternalIp = "";
            }

            bool gameOk = MapOne(mappings, gamePort, "UDP", client);
            bool syncOk = MapOne(mappings, syncPort, "TCP", client);

            var ports = T("oyun UDP {0} ve sync TCP {1}", gamePort, syncPort);
            if (gameOk && syncOk)
            {
                IsMapped = true;
                Complete(string.IsNullOrEmpty(ExternalIp)
                    ? T("{0} yönlendirildi (yerel {1}).", ports, client)
                    : T("{0} yönlendirildi (yerel {1}, dış IP {2}).", ports, client, ExternalIp));
            }
            else if (gameOk || syncOk)
            {
                IsMapped = true;
                var only = gameOk
                    ? T("oyun UDP {0}", gamePort)
                    : T("sync TCP {0}", syncPort);
                Complete(T("UPnP kısmen hazır: yalnızca {0} yönlendirildi (yerel {1}).", only, client));
            }
            else
            {
                IsMapped = false;
                Complete(T("UPnP portları yönlendirilemedi; relay kullanılacak."));
            }
        }
        catch (COMException ex)
        {
            Complete(T("UPnP hatası: {0}", ex.Message));
        }
        catch (Exception ex)
        {
            Complete(T("UPnP hatası: {0}", ex.Message));
        }
        finally
        {
            if (nat != null)
            {
                try { Marshal.FinalReleaseComObject(nat); } catch { /* COM zaten kapanmış */ }
            }
        }
    }

    private bool MapOne(object mappings, int port, string protocol, string client)
    {
        if (_disposed) return false;

        var existing = FindExisting(mappings, port, protocol, client, out var detail);
        if (existing == ExistingMapping.Same)
        {
            _log($"UPnP: {protocol} {port} zaten yönlendirilmiş ({detail}); yeniden eklenmedi.");
            return true;
        }
        if (existing == ExistingMapping.Conflict)
        {
            _log($"UPnP: {protocol} {port} başka bir hedefe yönlendirilmiş ({detail}); dokunulmadı.");
            return false;
        }

        try
        {
            dynamic collection = mappings;
            collection.Add(port, protocol, port, client, true, Description);

            bool disposeNow;
            lock (_gate)
            {
                disposeNow = _disposed;
                if (!disposeNow) _owned.Add((port, protocol));
            }

            if (disposeNow)
            {
                // Dispose, Add tamamlanmadan önce çalışmış olabilir; bu
                // durumda eşlemeyi hemen geri al ki arkada kalmasın.
                try { collection.Remove(port, protocol); } catch { }
                return false;
            }

            _log($"UPnP: {protocol} {port} -> {client}:{port} eklendi.");
            return true;
        }
        catch (Exception ex)
        {
            // Add, mevcut bir eşleme yüzünden reddetmiş olabilir. Mevcut
            // eşlemeyi silmeyiz/değiştirmeyiz; yalnızca bilgi veririz.
            _log($"UPnP: {protocol} {port} eklenemedi (mevcut eşleme korunuyor): {ex.Message}");
            return false;
        }
    }

    private static ExistingMapping FindExisting(object mappings, int port,
        string protocol, string client, out string detail)
    {
        detail = "";
        try
        {
            foreach (var item in EnumerateMappings(mappings))
            {
                dynamic mapping = item;
                int externalPort = Convert.ToInt32(mapping.ExternalPort);
                var existingProtocol = Convert.ToString(mapping.Protocol) ?? "";
                if (externalPort != port ||
                    !string.Equals(existingProtocol, protocol, StringComparison.OrdinalIgnoreCase))
                    continue;

                var existingClient = Convert.ToString(mapping.InternalClient) ?? "";
                int internalPort = Convert.ToInt32(mapping.InternalPort);
                detail = $"{existingClient}:{internalPort}";
                return internalPort == port &&
                       string.Equals(existingClient, client, StringComparison.OrdinalIgnoreCase)
                    ? ExistingMapping.Same
                    : ExistingMapping.Conflict;
            }
        }
        catch
        {
            // Bazı COM/Windows sürümlerinde koleksiyon numaralandırılamaz.
            // Bu durumda Add'in mevcut eşlemeyi reddetmesine güveniriz;
            // asla mevcut bir eşlemeyi silmeyiz.
        }
        return ExistingMapping.None;
    }

    /// <summary>
    /// COM koleksiyonunu iki yolla numaralandırır: doğrudan IEnumerable veya
    /// _NewEnum (IEnumVARIANT). İkisi de olmazsa boş liste döner.
    /// </summary>
    private static IEnumerable<object> EnumerateMappings(object mappings)
    {
        if (mappings is System.Collections.IEnumerable enumerable)
        {
            foreach (var item in enumerable)
                if (item != null) yield return item;
            yield break;
        }

        object? newEnum = null;
        try
        {
            newEnum = mappings.GetType().InvokeMember(
                "_NewEnum",
                BindingFlags.InvokeMethod | BindingFlags.GetProperty,
                null, mappings, null);
        }
        catch { /* numaralandırma desteklenmiyor */ }

        if (newEnum is not IEnumVARIANT variant) yield break;

        var buffer = new object[1];
        while (variant.Next(1, buffer, IntPtr.Zero) == 0)
        {
            if (buffer[0] != null) yield return buffer[0];
            buffer[0] = null!;
        }
    }

    private static string? PickInternalClient()
    {
        try
        {
            var candidates = new List<(string Ip, int Score)>();
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up) continue;
                if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                if (ni.NetworkInterfaceType == NetworkInterfaceType.Tunnel) continue;

                var properties = ni.GetIPProperties();
                bool hasGateway = properties.GatewayAddresses.Any(g =>
                    g.Address != null &&
                    g.Address.AddressFamily == AddressFamily.InterNetwork &&
                    !g.Address.Equals(IPAddress.Any));

                foreach (var unicast in properties.UnicastAddresses)
                {
                    if (unicast.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                    var ip = unicast.Address.ToString();
                    if (ip.StartsWith("127.") || ip.StartsWith("169.254.")) continue;

                    // Varsayılan geçidi olan arayüz en uygun adaydır. Radmin VPN
                    // (26.x) ve sanal adaptörler UPnP için geri planda kalır.
                    int score = hasGateway ? 0 : 10;
                    if (ip.StartsWith("26.")) score += 20;
                    if (ip.StartsWith("192.168.")) score += 1;
                    else if (ip.StartsWith("10.")) score += 2;
                    else if (ip.StartsWith("172.")) score += 3;
                    else score += 4;
                    candidates.Add((ip, score));
                }
            }

            return candidates
                .OrderBy(c => c.Score)
                .ThenBy(c => c.Ip, StringComparer.Ordinal)
                .Select(c => c.Ip)
                .FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }

    private void Complete(string status)
    {
        Status = status;
        _log("UPnP: " + status);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        (int Port, string Protocol)[] owned;
        lock (_gate)
        {
            owned = _owned.ToArray();
            _owned.Clear();
        }

        IsMapped = false;
        if (owned.Length == 0) return;

        try
        {
            RunOnSta(() => RemoveOwned(owned), CleanupTimeoutMs, throwOnTimeout: false);
        }
        catch (Exception ex)
        {
            _log("UPnP: eşlemeler kaldırılamadı: " + ex.Message);
        }
    }

    private void RemoveOwned((int Port, string Protocol)[] owned)
    {
        var type = Type.GetTypeFromProgID("HNetCfg.NATUPnP");
        if (type == null) return;

        object? nat = null;
        try
        {
            nat = Activator.CreateInstance(type);
            if (nat == null) return;

            dynamic com = nat;
            object? mappings = com.StaticPortMappingCollection;
            if (mappings == null) return;

            foreach (var (port, protocol) in owned)
            {
                try
                {
                    dynamic collection = mappings;
                    collection.Remove(port, protocol);
                    _log($"UPnP: {protocol} {port} eşlemesi kaldırıldı.");
                }
                catch (Exception ex)
                {
                    _log($"UPnP: {protocol} {port} eşlemesi kaldırılamadı: {ex.Message}");
                }
            }
        }
        finally
        {
            if (nat != null)
            {
                try { Marshal.FinalReleaseComObject(nat); } catch { /* COM zaten kapanmış */ }
            }
        }
    }

    private static void RunOnSta(Action action, int timeoutMs, bool throwOnTimeout = true)
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception ex) { error = ex; }
        })
        {
            IsBackground = true,
            Name = "GTACoop UPnP"
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        if (!thread.Join(timeoutMs))
        {
            if (throwOnTimeout) throw new TimeoutException("UPnP yanıt vermedi.");
            return;
        }

        if (error != null)
            ExceptionDispatchInfo.Capture(error).Throw();
    }

    private enum ExistingMapping
    {
        None,
        Same,
        Conflict
    }
}
