using System.Text;
using GTACoop.Sync;

// ---------------------------------------------------------------------------
// GTACoop.SyncTool — senkronizasyon sisteminin uçtan uca test aracı.
//
//   sync <host> <port> <oyunKlasörü> <veriKlasörü> <önbellek> [--token X]
//   prefetch <host> <port> <veriKlasörü> <önbellek> [--token X]
//   status <veriKlasörü>
//   uninstall-all <oyunKlasörü> <veriKlasörü>
//   session-watch --data <veriKlasörü> [--process-name X] [--timeout-min N]
//   session-cleanup <oyunKlasörü> <veriKlasörü>
// ---------------------------------------------------------------------------

try { Console.OutputEncoding = Encoding.UTF8; } catch { /* konsolsuz çalıştırılabilir */ }

if (args.Length == 0)
{
    Console.WriteLine("Kullanım:");
    Console.WriteLine("  sync <host> <port> <oyunKlasörü> <veriKlasörü> <önbellek> [--token X]");
    Console.WriteLine("  prefetch <host> <port> <veriKlasörü> <önbellek> [--token X]");
    Console.WriteLine("  status <veriKlasörü>");
    Console.WriteLine("  uninstall-all <oyunKlasörü> <veriKlasörü>");
    Console.WriteLine("  session-watch --data <veriKlasörü> [--process-name X] [--timeout-min N]");
    Console.WriteLine("  session-cleanup <oyunKlasörü> <veriKlasörü>");
    return 1;
}

try
{
    switch (args[0])
    {
        case "sync":
        {
            if (args.Length < 6) { Console.Error.WriteLine("Eksik parametre."); return 1; }
            var host = args[1];
            var port = int.Parse(args[2]);
            var gameDir = Path.GetFullPath(args[3]);
            var dataDir = Path.GetFullPath(args[4]);
            var cacheDir = Path.GetFullPath(args[5]);
            var token = args.Length > 7 && args[6] == "--token" ? args[7] : "";

            Console.WriteLine($"[1/4] Sunucuya bağlanılıyor: {host}:{port}");
            using var client = new SyncClient(host, port, token);
            var name = await client.PingAsync();
            Console.WriteLine($"      Sunucu: {name}");

            Console.WriteLine("[2/4] Manifest alınıyor...");
            var manifest = await client.GetManifestAsync();
            Console.WriteLine($"      {manifest.Addons.Count} eklenti:");
            foreach (var a in manifest.Addons)
                Console.WriteLine($"        - [{a.Type}] {a.DisplayName} ({a.Id}) " +
                                  $"{a.TotalSize / 1024.0 / 1024.0:F2} MB, relPack={a.RelativePackPath}");

            Console.WriteLine("[3/4] Plan ve indirme...");
            var applier = new AddonApplier(gameDir, s => Console.WriteLine("      " + s));
            var state = SyncStateStore.Load(dataDir, manifest.ServerId);
            var plan = applier.BuildPlan(manifest, state);
            Console.WriteLine($"      Kurulacak: {plan.ToInstall.Count}, güncel: {plan.UpToDate.Count}, " +
                              $"kaldırılacak: {plan.ToRemove.Count}");

            var progress = new Progress<SyncProgress>(p =>
                Console.WriteLine($"      [{p.Fraction,6:P0}] {p.Message}"));

            Console.WriteLine("[4/4] Senkronizasyon...");
            var result = await applier.SyncAsync(client, manifest, dataDir,
                Path.Combine(cacheDir, AddonApplier.Safe(manifest.ServerId)), progress);
            Console.WriteLine($"TAMAM: {result.Addons.Count} eklenti kurulu.");
            foreach (var a in result.Addons)
                Console.WriteLine($"  - {a.Id} v{a.Version} " +
                                  (a.Type == AddonType.DlcPack ? $"mount={a.DlclistEntry}" : $""));
            return 0;
        }

        case "status":
        {
            var dataDir = Path.GetFullPath(args[1]);
            var states = SyncStateStore.LoadAll(dataDir);
            if (states.Count == 0) { Console.WriteLine("Kayıtlı durum yok."); return 0; }
            foreach (var s in states)
            {
                Console.WriteLine($"Sunucu: {s.ServerName} ({s.ServerId}) — son: {s.LastSyncUtc:u}");
                foreach (var a in s.Addons)
                    Console.WriteLine($"  - [{a.Type}] {a.Id} v{a.Version} " +
                                      (a.DlclistEntry != "" ? $"entry={a.DlclistEntry}" : ""));
            }
            return 0;
        }

        case "uninstall-all":
        {
            var gameDir = Path.GetFullPath(args[1]);
            var dataDir = Path.GetFullPath(args[2]);
            var applier = new AddonApplier(gameDir, s => Console.WriteLine("  " + s));
            foreach (var s in SyncStateStore.LoadAll(dataDir))
            {
                Console.WriteLine($"Sunucu kaldırılıyor: {s.ServerName}");
                applier.UninstallAll(s, dataDir);
            }
            Console.WriteLine("Sahipsiz gcs_ kalıntıları temizleniyor...");
            applier.PruneOrphanMounts();
            Console.WriteLine("Temizlik tamam.");
            return 0;
        }

        case "prefetch":
        {
            if (args.Length < 5) { Console.Error.WriteLine("Eksik parametre."); return 1; }
            var host = args[1];
            var port = int.Parse(args[2]);
            var dataDir = Path.GetFullPath(args[3]);
            var cacheDir = Path.GetFullPath(args[4]);
            var token = args.Length > 6 && args[5] == "--token" ? args[6] : "";

            using var client = new SyncClient(host, port, token);
            var manifest = await client.GetManifestAsync();
            Console.WriteLine($"Ön indirme: {manifest.ServerName} ({manifest.Addons.Count} eklenti)");
            var applier = new AddonApplier("", s => Console.WriteLine("      " + s));
            var progress = new Progress<SyncProgress>(p =>
                Console.WriteLine($"      [{p.Fraction,6:P0}] {p.Message}"));
            await applier.PrefetchAsync(client, manifest,
                Path.Combine(cacheDir, AddonApplier.Safe(manifest.ServerId)), progress);
            Console.WriteLine("TAMAM: dosyalar önbelleğe indirildi (oyuna kurulmadı).");
            return 0;
        }

        case "session-watch":
        {
            var dataDir = "";
            var procName = "";
            var timeoutMin = 12;
            for (int i = 1; i < args.Length - 1; i++)
            {
                if (args[i] == "--data") dataDir = args[++i];
                else if (args[i] == "--process-name") procName = args[++i];
                else if (args[i] == "--timeout-min") timeoutMin = int.Parse(args[++i]);
            }
            if (string.IsNullOrWhiteSpace(dataDir)) { Console.Error.WriteLine("--data gerekli."); return 1; }
            dataDir = Path.GetFullPath(dataDir);

            // Sessiz mod: günlük dosyaya yazılır (launcher kapanmış olabilir).
            var logPath = Path.Combine(dataDir, "logs", "session-watch.log");
            void Wlog(string m)
            {
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
                    File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss}] {m}\r\n");
                }
                catch { }
            }

            var rec = SessionStore.Load(dataDir);
            if (rec == null) { Wlog("Oturum kaydı yok; izleyici çıkıyor."); return 0; }

            var settings = SyncJson.Load<LauncherSettings>(Path.Combine(dataDir, "settings.json"));
            var gameFolder = settings?.GameFolder ?? "";
            if (string.IsNullOrWhiteSpace(gameFolder) || !Directory.Exists(gameFolder))
            {
                Wlog("Oyun klasörü bilinmiyor; izleyici çıkıyor.");
                return 2;
            }

            Wlog($"İzleyici başladı: {rec.ServerName} ({rec.ServerId}), süre sınırı {timeoutMin} dk.");
            var names = string.IsNullOrEmpty(procName) ? null : new[] { procName };
            try
            {
                await SessionService.WatchAsync(dataDir, gameFolder, names, Wlog, timeoutMin);
                return 0;
            }
            catch (Exception ex)
            {
                Wlog("İzleyici hatası: " + ex.Message);
                return 3;
            }
        }

        case "session-cleanup":
        {
            var gameDir = Path.GetFullPath(args[1]);
            var dataDir = Path.GetFullPath(args[2]);
            var ok = SessionService.Finish(dataDir, gameDir, s => Console.WriteLine("  " + s));
            Console.WriteLine(ok ? "Oturum temizlendi." : "Temizlik yapılamadı.");
            return ok ? 0 : 2;
        }

        case "install-client":
        {
            var gameDir = Path.GetFullPath(args[1]);
            var result = ClientInstaller.InstallClient(gameDir, s => Console.WriteLine("  " + s));
            Console.WriteLine($"İstemci kurulumu: {result.Copied} kopyalandı, {result.Skipped} atlandı.");
            foreach (var d in result.Details) Console.WriteLine("  + " + d);
            return 0;
        }

        case "settings":
        {
            var gameDir = Path.GetFullPath(args[1]);
            var host = args[2];
            var port = int.Parse(args[3]);
            var password = args.Length > 4 ? args[4] : "";
            ClientSettingsEditor.Apply(gameDir, "TestOyuncu", host, port, password, true,
                s => Console.WriteLine("  " + s));
            Console.WriteLine("--- ClientSettings.xml: ---");
            Console.WriteLine(File.ReadAllText(ClientSettingsEditor.SettingsPath(gameDir)));
            ClientSettingsEditor.DisableAutoConnect(gameDir, s => Console.WriteLine("  " + s));
            return 0;
        }

        case "server-selftest":
        {
            var dir = args.Length > 1
                ? Path.GetFullPath(args[1])
                : Path.Combine(Path.GetTempPath(), "gtacoop-server-selftest");

            Console.WriteLine($"Sunucu dizini: {dir}");
            var my = new MyServer(dir, s => Console.WriteLine("  " + s));
            my.EnsureLayout();
            var (id, _) = my.GetOrCreateIdentity("Selftest Sunucu");

            var settings = new MyServerSettings
            {
                ServerName = "Selftest Sunucu",
                GamePort = 4497,
                SyncPort = 4597,
                MaxClients = 8,
                Motd = "Selftest"
            };
            my.WriteSettings(settings);
            Console.WriteLine($"Kimlik: {id}");

            my.StartGameServer();
            my.StartSyncServer(settings);

            Console.WriteLine("Bekleniyor (8 sn)...");
            await Task.Delay(8000);

            using var client = new SyncClient("127.0.0.1", settings.SyncPort);
            try
            {
                var pingName = await client.PingAsync();
                Console.WriteLine($"PING OK: {pingName}");
                var manifest = await client.GetManifestAsync();
                Console.WriteLine($"Manifest OK: sunucu={manifest.ServerName}, eklenti={manifest.Addons.Count}");
            }
            catch (Exception ex)
            {
                Console.WriteLine("PING HATASI: " + ex.Message);
                my.StopAll();
                return 2;
            }

            my.StopAll();
            Console.WriteLine("SELFTEST TAMAM.");
            return 0;
        }

        default:
            Console.Error.WriteLine($"Bilinmeyen komut: {args[0]}");
            return 1;
    }
}
catch (SyncException ex)
{
    Console.Error.WriteLine("HATA: " + ex.Message);
    return 2;
}
catch (Exception ex)
{
    Console.Error.WriteLine("BEKLENMEYEN HATA: " + ex);
    return 3;
}
