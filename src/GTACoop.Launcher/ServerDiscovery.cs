using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Lidgren.Network;

namespace GTACoop.Remake;

/// <summary>GTACoOp sunucu tarayıcısının gördüğü tek bir sunucu.</summary>
public sealed record DiscoveredServer
{
    public string Name { get; init; } = "";
    public string Host { get; init; } = "";
    public int GamePort { get; init; }
    public int PlayerCount { get; init; }
    public int MaxPlayers { get; init; }
    public bool PasswordProtected { get; init; }
    public string Gamemode { get; init; } = "";
    public int ProtocolVersion { get; init; }
    public int PingMs { get; init; }
    public string Source { get; init; } = "";
    public DateTime LastSeenUtc { get; init; } = DateTime.UtcNow;

    public string Address => Host.Contains(':') ? $"[{Host}]:{GamePort}" : $"{Host}:{GamePort}";
    public bool IsCurrent => ProtocolVersion == ServerBrowserDiscovery.CurrentProtocolVersion;
}

/// <summary>Tarama sırasında ilerleme bilgisi.</summary>
public sealed record ServerScanProgress(int Sent, int Total, int Responses, string Message);

/// <summary>Tarama sonucu.</summary>
public sealed class ServerScanResult
{
    public List<DiscoveredServer> Servers { get; } = new();
    public int Queried { get; set; }
    public int Responded { get; set; }
    public string? MasterUrlUsed { get; set; }
    public List<string> Errors { get; } = new();
}

/// <summary>Master listesindeki veya elle girilen bir sunucu uç noktası.</summary>
public sealed record ServerEndpoint(string Host, int Port, string Source);

/// <summary>
/// GTACoOp'un oyun içi sunucu tarayıcısıyla aynı protokolü kullanır:
/// master listesinden IP:port alır, Lidgren discovery paketi gönderir ve
/// sunucunun adı, oyuncu sayısı, modu ve protokol sürümünü okur.
/// </summary>
public static class ServerBrowserDiscovery
{
    public const int CurrentProtocolVersion = 9;
    public const string DefaultMasterUrl = "https://master.gtacoop.com";
    public const string BackupMasterUrl = "http://clan-banderos.de/gta/";

    private const int MaxMasterBytes = 2 * 1024 * 1024;
    private const int MaxEndpoints = 2000;

    public static string VersionText(int version) => version switch
    {
        0 => T("bilinmiyor"),
        1 => "0.6",
        2 => "0.6.1",
        3 => "0.7",
        4 => "0.8.1",
        5 => "0.9",
        6 => "0.9.1",
        7 => "0.9.2",
        8 => "0.9.3",
        9 => "0.9.4",
        _ => $"0.9.{version - 5}"
    };

    public static string VersionState(int version) => version switch
    {
        0 => T("eski / sürüm yok"),
        < CurrentProtocolVersion => T("eski"),
        > CurrentProtocolVersion => T("daha yeni"),
        _ => T("güncel")
    };

    /// <summary>Liste sütununda kısa durum metni.</summary>
    public static string VersionShortState(int version) => version switch
    {
        0 => T("bilinmiyor"),
        < CurrentProtocolVersion => T("eski"),
        > CurrentProtocolVersion => T("daha yeni"),
        _ => T("güncel")
    };

    public static bool TryParseEndpoint(string text, out string host, out int port)
    {
        host = "";
        port = 0;
        text = (text ?? "").Trim();
        if (text.Length == 0) return false;

        // [::1]:4499
        if (text.StartsWith('['))
        {
            int close = text.IndexOf(']');
            if (close <= 1 || close + 1 >= text.Length || text[close + 1] != ':') return false;
            host = text[1..close];
            return int.TryParse(text[(close + 2)..], out port) && port is > 0 and <= 65535;
        }

        int colon = text.LastIndexOf(':');
        if (colon <= 0 || colon == text.Length - 1) return false;
        host = text[..colon].Trim();
        return host.Length > 0 &&
               int.TryParse(text[(colon + 1)..].Trim(), out port) &&
               port is > 0 and <= 65535;
    }

    /// <summary>Master listesini indirir ve sunucu uç noktalarını döndürür.</summary>
    public static async Task<(List<ServerEndpoint> Endpoints, string? UsedUrl, List<string> Errors)> FetchMasterAsync(
        string? masterUrl, CancellationToken ct)
    {
        var errors = new List<string>();
        var urls = new List<string>();
        foreach (var part in (masterUrl ?? "").Split(new[] { ';', '\r', '\n' },
                     StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!urls.Contains(part, StringComparer.OrdinalIgnoreCase)) urls.Add(part);
        }
        if (urls.Count == 0) urls.Add(DefaultMasterUrl);
        if (!urls.Contains(BackupMasterUrl, StringComparer.OrdinalIgnoreCase))
            urls.Add(BackupMasterUrl);

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("GTACoop-Remake-ServerBrowser/2.2");

        foreach (var raw in urls)
        {
            ct.ThrowIfCancellationRequested();
            var url = raw.Trim();
            if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                url = "http://" + url;

            try
            {
                // Bozuk/bekleyen master adresi tüm taramayı geciktirmesin:
                // her adres için kısa bir süre tanınır, sonra yedeğe geçilir.
                using var requestCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                requestCts.CancelAfter(TimeSpan.FromSeconds(4));

                using var resp = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, requestCts.Token);
                if (!resp.IsSuccessStatusCode)
                    throw new HttpRequestException($"HTTP {(int)resp.StatusCode} {resp.ReasonPhrase}");

                await using var stream = await resp.Content.ReadAsStreamAsync(requestCts.Token);
                using var ms = new MemoryStream();
                var buffer = new byte[32 * 1024];
                int read;
                while ((read = await stream.ReadAsync(buffer, requestCts.Token)) > 0)
                {
                    if (ms.Length + read > MaxMasterBytes)
                        throw new InvalidDataException(T("Master listesi beklenenden büyük."));
                    ms.Write(buffer, 0, read);
                }

                var body = Encoding.UTF8.GetString(ms.ToArray());
                var endpoints = ParseMasterBody(body, out var parseError);
                if (endpoints.Count == 0)
                    throw new InvalidDataException(parseError ?? T("Master listesi boş."));

                return (endpoints, url, errors);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                errors.Add($"{url}: {ex.Message}");
            }
        }

        return (new List<ServerEndpoint>(), null, errors);
    }

    /// <summary>Master listesini ve LAN'ı tarar; yanıt veren sunucuları döndürür.</summary>
    public static async Task<ServerScanResult> ScanAsync(
        string? masterUrl,
        IProgress<ServerScanProgress>? progress,
        Action<DiscoveredServer>? onServer,
        CancellationToken ct)
    {
        var result = new ServerScanResult();
        progress?.Report(new ServerScanProgress(0, 0, 0, T("Master listesi alınıyor...")));

        var master = await FetchMasterAsync(masterUrl, ct);
        result.MasterUrlUsed = master.UsedUrl;
        result.Errors.AddRange(master.Errors);

        var endpoints = new List<ServerEndpoint>(master.Endpoints);
        // LAN yayını ayrıca yapılır; burada yalnızca bilinen uç noktalar sorgulanır.
        var scan = await DiscoverAsync(endpoints, includeLan: true, timeoutMs: 3200, progress, onServer, ct);
        scan.MasterUrlUsed = result.MasterUrlUsed;
        scan.Errors.InsertRange(0, result.Errors);
        return scan;
    }

    /// <summary>Yalnızca LAN yayını ile yerel sunucuları arar.</summary>
    public static Task<ServerScanResult> ScanLanAsync(
        IProgress<ServerScanProgress>? progress,
        Action<DiscoveredServer>? onServer,
        CancellationToken ct)
    {
        return DiscoverAsync(Array.Empty<ServerEndpoint>(), includeLan: true,
            timeoutMs: 2600, progress, onServer, ct);
    }

    /// <summary>Tek bir sunucuyu kısa süreliğine yoklar (doğrudan bağlantı için).</summary>
    public static async Task<DiscoveredServer?> ProbeAsync(
        string host, int port, string source, CancellationToken ct)
    {
        var result = await DiscoverAsync(
            new[] { new ServerEndpoint(host, port, source) },
            includeLan: false, timeoutMs: 2200, null, null, ct);
        return result.Servers.FirstOrDefault();
    }

    private static async Task<ServerScanResult> DiscoverAsync(
        IReadOnlyList<ServerEndpoint> endpoints,
        bool includeLan,
        int timeoutMs,
        IProgress<ServerScanProgress>? progress,
        Action<DiscoveredServer>? onServer,
        CancellationToken ct)
    {
        var result = new ServerScanResult();
        var seen = new ConcurrentDictionary<string, DiscoveredServer>(StringComparer.OrdinalIgnoreCase);
        var requestTimes = new ConcurrentDictionary<IPEndPoint, DateTime>();
        var sourceByEndpoint = new ConcurrentDictionary<IPEndPoint, string>();
        var endpointList = new List<IPEndPoint>();
        var endpointKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var e in endpoints)
        {
            ct.ThrowIfCancellationRequested();
            if (!TryResolve(e.Host, out var ip)) continue;
            var ep = new IPEndPoint(ip, e.Port);
            var key = ep.Address + ":" + ep.Port;
            if (!endpointKeys.Add(key)) continue;
            endpointList.Add(ep);
            sourceByEndpoint[ep] = e.Source;
            if (endpointList.Count >= MaxEndpoints) break;
        }

        var config = new NetPeerConfiguration("GTAVOnlineRaces")
        {
            Port = GetFreeUdpPort(),
            ReceiveBufferSize = 1024 * 1024
        };
        config.EnableMessageType(NetIncomingMessageType.DiscoveryResponse);

        var client = new NetClient(config);
        try
        {
            client.Start();
            var startDeadline = DateTime.UtcNow.AddSeconds(2);
            while (client.Status != NetPeerStatus.Running && DateTime.UtcNow < startDeadline)
                await Task.Delay(20, ct);

            using var receiveCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var receiveTask = Task.Run(async () =>
            {
                try
                {
                    while (!receiveCts.IsCancellationRequested)
                    {
                        NetIncomingMessage? msg;
                        while ((msg = client.ReadMessage()) != null)
                        {
                            if (msg.MessageType != NetIncomingMessageType.DiscoveryResponse) continue;
                            try
                            {
                                int packetType = msg.ReadInt32();
                                int length = msg.ReadInt32();
                                if (packetType != 7 || length <= 0 || length > 256 * 1024) continue;
                                var bytes = msg.ReadBytes(length);
                                if (bytes == null) continue;

                                var payload = ParseDiscoveryPayload(bytes);
                                if (payload == null) continue;

                                var sender = msg.SenderEndPoint;
                                var gamePort = payload.Port > 0 ? payload.Port : sender.Port;
                                var key = sender.Address + ":" + gamePort;
                                var ping = 0;
                                if (requestTimes.TryGetValue(sender, out var sentAt))
                                    ping = (int)Math.Clamp((DateTime.UtcNow - sentAt).TotalMilliseconds, 0, 9999);

                                var source = sourceByEndpoint.TryGetValue(sender, out var s)
                                    ? s
                                    : IsPrivateAddress(sender.Address) ? T("LAN") : T("Keşif");

                                var server = new DiscoveredServer
                                {
                                    Name = string.IsNullOrWhiteSpace(payload.ServerName)
                                        ? $"{sender.Address}:{gamePort}"
                                        : payload.ServerName.Trim(),
                                    Host = sender.Address.ToString(),
                                    GamePort = gamePort,
                                    PlayerCount = Math.Max(0, payload.PlayerCount),
                                    MaxPlayers = Math.Max(0, payload.MaxPlayers),
                                    PasswordProtected = payload.PasswordProtected,
                                    Gamemode = string.IsNullOrWhiteSpace(payload.Gamemode) ? "none" : payload.Gamemode.Trim(),
                                    ProtocolVersion = payload.Version,
                                    PingMs = ping,
                                    Source = source,
                                    LastSeenUtc = DateTime.UtcNow
                                };

                                seen[key] = server;
                                onServer?.Invoke(server);
                                progress?.Report(new ServerScanProgress(
                                    endpointList.Count, endpointList.Count, seen.Count,
                                    T("Yanıt veren sunucu: {0}", server.Name)));
                            }
                            catch
                            {
                                // Bozuk/eksik discovery paketi tüm taramayı bozmasın.
                            }
                        }

                        await Task.Delay(15, receiveCts.Token);
                    }
                }
                catch (OperationCanceledException) { }
            }, receiveCts.Token);

            int sent = 0;
            try
            {
                foreach (var ep in endpointList)
                {
                    ct.ThrowIfCancellationRequested();
                    requestTimes[ep] = DateTime.UtcNow;
                    try { client.DiscoverKnownPeer(ep); }
                    catch { continue; }
                    sent++;
                    if (sent % 64 == 0)
                    {
                        progress?.Report(new ServerScanProgress(sent, endpointList.Count, seen.Count,
                            T("Sunucular sorgulanıyor... {0}/{1}", sent, endpointList.Count)));
                        await Task.Delay(20, ct);
                    }
                }

                if (includeLan)
                {
                    foreach (var lanPort in new[] { 4499, 4498, 44499 })
                    {
                        try { client.DiscoverLocalPeers(lanPort); }
                        catch { }
                    }
                }

                result.Queried = sent;
                progress?.Report(new ServerScanProgress(sent, endpointList.Count, seen.Count,
                    T("Yanıtlar bekleniyor... ({0} sunucu sorgulandı)", sent)));

                await Task.Delay(timeoutMs, ct);
            }
            finally
            {
                receiveCts.Cancel();
                try { await receiveTask; } catch { }
            }

            result.Servers.AddRange(seen.Values
                .OrderByDescending(s => s.PlayerCount)
                .ThenBy(s => s.Name, StringComparer.CurrentCultureIgnoreCase));
            result.Responded = seen.Count;
            return result;
        }
        finally
        {
            try { client.Shutdown("server browser scan finished"); } catch { }
        }
    }

    private static bool TryResolve(string host, out IPAddress address)
    {
        address = IPAddress.None;
        if (IPAddress.TryParse(host.Trim('[', ']'), out var parsed))
        {
            address = parsed;
            return true;
        }

        try
        {
            var addresses = Dns.GetHostAddresses(host);
            var first = addresses.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork)
                        ?? addresses.FirstOrDefault();
            if (first == null) return false;
            address = first;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static int GetFreeUdpPort()
    {
        using var udp = new UdpClient(0);
        return ((IPEndPoint)udp.Client.LocalEndPoint!).Port;
    }

    private static bool IsPrivateAddress(IPAddress address)
    {
        if (IPAddress.IsLoopback(address)) return true;
        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var b = address.GetAddressBytes();
            return b[0] == 10 ||
                   (b[0] == 172 && b[1] is >= 16 and <= 31) ||
                   (b[0] == 192 && b[1] == 168) ||
                   (b[0] == 169 && b[1] == 254);
        }
        if (address.AddressFamily == AddressFamily.InterNetworkV6)
            return address.IsIPv6LinkLocal || address.IsIPv6SiteLocal ||
                   address.ToString().StartsWith("fc", StringComparison.OrdinalIgnoreCase) ||
                   address.ToString().StartsWith("fd", StringComparison.OrdinalIgnoreCase);
        return false;
    }

    private static List<ServerEndpoint> ParseMasterBody(string body, out string? error)
    {
        error = null;
        var list = new List<ServerEndpoint>();
        try
        {
            using var doc = JsonDocument.Parse(body);
            JsonElement root = doc.RootElement;
            JsonElement array;
            if (root.ValueKind == JsonValueKind.Array)
                array = root;
            else if (root.ValueKind == JsonValueKind.Object &&
                     (root.TryGetProperty("list", out array) ||
                      root.TryGetProperty("servers", out array)))
            {
                // array atandı
            }
            else
            {
                error = T("Master listesi tanınmayan bir JSON biçiminde.");
                return list;
            }

            if (array.ValueKind != JsonValueKind.Array)
            {
                error = T("Master listesi bir dizi değil.");
                return list;
            }

            foreach (var item in array.EnumerateArray())
            {
                string? text = null;
                if (item.ValueKind == JsonValueKind.String)
                {
                    text = item.GetString();
                }
                else if (item.ValueKind == JsonValueKind.Object)
                {
                    var host = GetString(item, "address") ?? GetString(item, "ip") ?? GetString(item, "host");
                    var port = GetInt(item, "port");
                    if (!string.IsNullOrWhiteSpace(host) && port is > 0 and <= 65535)
                        text = host + ":" + port;
                }

                if (string.IsNullOrWhiteSpace(text)) continue;
                if (!TryParseEndpoint(text, out var hostName, out var portNumber)) continue;
                if (list.Any(x => string.Equals(x.Host, hostName, StringComparison.OrdinalIgnoreCase) &&
                                  x.Port == portNumber)) continue;
                list.Add(new ServerEndpoint(hostName, portNumber, T("Master listesi")));
                if (list.Count >= MaxEndpoints) break;
            }
        }
        catch (Exception ex)
        {
            error = T("Master listesi okunamadı: {0}", ex.Message);
        }
        return list;
    }

    private static string? GetString(JsonElement obj, string name) =>
        obj.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static int GetInt(JsonElement obj, string name) =>
        obj.TryGetProperty(name, out var v) && v.TryGetInt32(out var n) ? n : 0;

    private sealed class DiscoveryPayload
    {
        public string ServerName { get; set; } = "";
        public int MaxPlayers { get; set; }
        public int PlayerCount { get; set; }
        public bool PasswordProtected { get; set; }
        public int Port { get; set; }
        public string Gamemode { get; set; } = "";
        public int Version { get; set; }
    }

    /// <summary>
    /// GTACoOp DiscoveryResponse protobuf'unu bağımlılık olmadan okur.
    /// Alanlar: 1 ad, 2 max, 3 oyuncu, 4 şifre, 5 port, 6 mod, 7 protokol.
    /// </summary>
    private static DiscoveryPayload? ParseDiscoveryPayload(byte[] data)
    {
        try
        {
            var payload = new DiscoveryPayload();
            int pos = 0;
            while (pos < data.Length)
            {
                int key = ReadVarint(data, ref pos);
                int field = key >> 3;
                int wire = key & 7;
                switch (field)
                {
                    case 1 when wire == 2:
                        payload.ServerName = ReadString(data, ref pos);
                        break;
                    case 2 when wire == 0:
                        payload.MaxPlayers = ReadVarint(data, ref pos);
                        break;
                    case 3 when wire == 0:
                        payload.PlayerCount = ReadVarint(data, ref pos);
                        break;
                    case 4 when wire == 0:
                        payload.PasswordProtected = ReadVarint(data, ref pos) != 0;
                        break;
                    case 5 when wire == 0:
                        payload.Port = ReadVarint(data, ref pos);
                        break;
                    case 6 when wire == 2:
                        payload.Gamemode = ReadString(data, ref pos);
                        break;
                    case 7 when wire == 0:
                        payload.Version = ReadVarint(data, ref pos);
                        break;
                    default:
                        SkipField(data, ref pos, wire);
                        break;
                }
            }
            return payload;
        }
        catch
        {
            return null;
        }
    }

    private static int ReadVarint(byte[] data, ref int pos)
    {
        int value = 0;
        int shift = 0;
        while (pos < data.Length)
        {
            byte b = data[pos++];
            value |= (b & 0x7f) << shift;
            if ((b & 0x80) == 0) return value;
            shift += 7;
            if (shift > 35) break;
        }
        throw new InvalidDataException("Geçersiz protobuf varint.");
    }

    private static string ReadString(byte[] data, ref int pos)
    {
        int len = ReadVarint(data, ref pos);
        if (len < 0 || pos + len > data.Length) throw new InvalidDataException("Geçersiz protobuf metni.");
        var text = Encoding.UTF8.GetString(data, pos, len);
        pos += len;
        return text;
    }

    private static void SkipField(byte[] data, ref int pos, int wire)
    {
        switch (wire)
        {
            case 0:
                ReadVarint(data, ref pos);
                break;
            case 1:
                pos += 8;
                break;
            case 2:
                pos += ReadVarint(data, ref pos);
                break;
            case 5:
                pos += 4;
                break;
            default:
                throw new InvalidDataException("Geçersiz protobuf alanı.");
        }
        if (pos > data.Length) throw new InvalidDataException("Protobuf alanı taşdı.");
    }
}
