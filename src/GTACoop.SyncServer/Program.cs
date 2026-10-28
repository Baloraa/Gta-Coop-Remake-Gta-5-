using System.Diagnostics;
using System.Text;
using GTACoop.Sync;

// ---------------------------------------------------------------------------
// GTACoop.SyncServer
// Sunucudaki Addons\ klasörünü tarar; manifest ve dosyaları HTTP üzerinden sunar.
//
// Kullanım:
//   GTACoop.SyncServer.exe --addons "C:\...\Addons" [--port 4599] [--name "Sunucum"] [--token XYZ]
// ---------------------------------------------------------------------------

Console.OutputEncoding = Encoding.UTF8;
Console.WriteLine("== GTACoop Senkronizasyon Sunucusu v1.0 ==");

string addonsDir = "";
string serverName = "GTACoop Sunucu";
string token = "";
string settingsPath = "";
int port = 4599;

for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--addons" when i + 1 < args.Length: addonsDir = args[++i]; break;
        case "--port" when i + 1 < args.Length: port = int.Parse(args[++i]); break;
        case "--name" when i + 1 < args.Length: serverName = args[++i]; break;
        case "--token" when i + 1 < args.Length: token = args[++i]; break;
        case "--settings" when i + 1 < args.Length: settingsPath = args[++i]; break;
        case "--help":
            Console.WriteLine("Kullanım: GTACoop.SyncServer.exe --addons <klasör> [--port 4599] [--name \"Sunucum\"] [--token ŞİFRE] [--settings <myServer.json>]");
            return 0;
    }
}

if (string.IsNullOrWhiteSpace(addonsDir))
{
    Console.Error.WriteLine("HATA: --addons parametresi gerekli.");
    return 1;
}

addonsDir = Path.GetFullPath(addonsDir);
ServerScanner.EnsureLayout(addonsDir);
if (!string.IsNullOrWhiteSpace(settingsPath))
    settingsPath = Path.GetFullPath(settingsPath);

// Sunucu mod yöneticisinde devre dışı bırakılan modların anahtarları.
HashSet<string> DisabledMods()
{
    if (string.IsNullOrWhiteSpace(settingsPath))
        return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    try
    {
        var settings = SyncJson.Load<MyServerSettings>(settingsPath);
        return new HashSet<string>(settings?.DisabledMods ?? new List<string>(),
            StringComparer.OrdinalIgnoreCase);
    }
    catch
    {
        return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }
}

// Kalıcı sunucu kimliği
var idPath = Path.Combine(addonsDir, ".gtacoop-server.json");
var idDoc = SyncJson.Load<Dictionary<string, string>>(idPath) ?? new Dictionary<string, string>();
if (!idDoc.TryGetValue("serverId", out var serverId) || string.IsNullOrEmpty(serverId))
{
    serverId = "srv_" + Guid.NewGuid().ToString("N")[..8];
    idDoc["serverId"] = serverId;
}
idDoc["serverName"] = serverName;
SyncJson.Save(idPath, idDoc);

var scanner = new ServerScanner(addonsDir);

var builder = WebApplication.CreateBuilder();
builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(o => { o.SingleLine = true; o.TimestampFormat = "HH:mm:ss "; });
builder.WebHost.ConfigureKestrel(k => k.ListenAnyIP(port));

var app = builder.Build();

// Dosya isteklerini (özellikle paralel Range parçalarını) sunucu tarafında
// görünür kıl. Böylece "indirme takıldı" durumunda hangi parçanın istendiği,
// hangi HTTP koduyla ve ne kadar sürede döndüğü logdan anlaşılır.
app.Use(async (ctx, next) =>
{
    var isFile = ctx.Request.Path.StartsWithSegments("/api/v1/files");
    var started = Stopwatch.GetTimestamp();
    await next();
    if (!isFile) return;

    var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
    var range = ctx.Request.Headers.Range.ToString();
    Console.WriteLine(
        $"[{DateTime.Now:HH:mm:ss}] HTTP {ctx.Response.StatusCode} " +
        $"{elapsed:0} ms range={(range.Length == 0 ? "-" : range)} " +
        $"len={ctx.Response.ContentLength?.ToString() ?? "?"} " +
        $"<- {ctx.Connection.RemoteIpAddress} {ctx.Request.Path}");
});

bool Authorized(HttpContext ctx) =>
    string.IsNullOrEmpty(token) ||
    (ctx.Request.Headers.TryGetValue("X-Sync-Token", out var t) && t.ToString() == token);

// ------------------------------------------------------------------ uç noktalar

app.MapGet("/", () => Results.Text(
    $"GTACoop Senkronizasyon Sunucusu\nSunucu: {serverName}\nKimlik: {serverId}\n" +
    $"Devre dışı mod: {DisabledMods().Count}\n" +
    $"Manifest: /api/v1/manifest\nDosyalar: /api/v1/files/<eklenti>/<yol>\n"));

app.MapGet("/api/v1/ping", () => Results.Json(new
{
    serverName,
    serverId,
    syncPort = port,
    disabledMods = DisabledMods().Count,
    app = "GTACoop.SyncServer",
    version = "1.1",
    range = true
}, SyncJson.Options));

app.MapGet("/api/v1/manifest", (HttpContext ctx) =>
{
    if (!Authorized(ctx)) return Results.Unauthorized();

    var scan = scanner.Scan(serverName, serverId, port);
    var manifest = scan.Manifest;

    var disabled = DisabledMods();
    var hidden = disabled.Count == 0 ? 0 : manifest.Addons.RemoveAll(a => disabled.Contains(a.Key));

    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Manifest istendi: {manifest.Addons.Count} eklenti " +
                      $"({hidden} devre dışı, {scan.Warnings.Count} not) <- {ctx.Connection.RemoteIpAddress}");

    ctx.Response.Headers["X-Warnings"] = scan.Warnings.Count.ToString();
    return Results.Json(manifest, SyncJson.Options);
});

app.MapGet("/api/v1/files/{addonId}/{**relPath}", (HttpContext ctx, string addonId, string relPath) =>
{
    if (!Authorized(ctx)) return Results.Unauthorized();

    var disabledIds = DisabledMods()
        .Select(k => k[(k.IndexOf(':') + 1)..])
        .ToHashSet(StringComparer.OrdinalIgnoreCase);
    if (disabledIds.Contains(addonId))
        return Results.NotFound(new { error = "Mod devre dışı." });

    var scan = scanner.Scan(serverName, serverId, port);
    if (!scan.TryGetPhysicalPath(addonId, relPath, out var fullPath))
        return Results.NotFound(new { error = $"Dosya bulunamadı: {addonId}/{relPath}" });

    var fi = new FileInfo(fullPath);
    if (!fi.Exists)
        return Results.NotFound(new { error = "Dosya diskte yok." });

    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] İndirme: {addonId}/{relPath} ({fi.Length:N0} bayt) " +
                      $"<- {ctx.Connection.RemoteIpAddress}");

    return Results.File(fullPath, "application/octet-stream",
        fileDownloadName: Path.GetFileName(fullPath), enableRangeProcessing: true);
});

// ------------------------------------------------------------------ çalıştır

app.Lifetime.ApplicationStarted.Register(() =>
{
    Console.WriteLine($"Sunucu adı : {serverName}");
    Console.WriteLine($"Sunucu kimliği: {serverId}");
    Console.WriteLine($"Eklenti klasörü: {addonsDir}");
    Console.WriteLine($"Devre dışı mod: {DisabledMods().Count}");
    Console.WriteLine($"Dinleniyor : http://0.0.0.0:{port}/");
    Console.WriteLine(token.Length > 0 ? "Kimlik doğrulama: AÇIK (X-Sync-Token)" : "Kimlik doğrulama: kapalı");
    Console.WriteLine("Durdurmak için Ctrl+C.");
});

var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    lifetime.StopApplication();
};

await app.RunAsync();
return 0;
