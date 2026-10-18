using System.Text;
using GTACoop.Rpf;

namespace GTACoop.RpfTool;

internal static class Program
{
    static string? GameExe;

    static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        if (args.Length == 0) { PrintUsage(); return 1; }

        // global: --gameexe <path>
        int gi = Array.IndexOf(args, "--gameexe");
        if (gi >= 0 && gi + 1 < args.Length)
        {
            GameExe = args[gi + 1];
            args = args.Where((_, i) => i != gi && i != gi + 1).ToArray();
        }

        try
        {
            switch (args[0].ToLowerInvariant())
            {
                case "header": return Header(args[1]);
                case "list": return List(args);
                case "find": return Find(args);
                case "extract": return Extract(args);
                case "verify": return Verify(args);
                case "dlclist-list": return DlclistList(args[1]);
                case "dlclist-add": return DlclistAdd(args);
                case "dlclist-remove": return DlclistRemove(args);
                case "replace": return Replace(args);
                case "peek": return Peek(args);
                case "copy": return Copy(args[1], args[2]);
                case "keyfind": return KeyFind(args[1]);
                default: PrintUsage(); return 1;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[HATA] {ex.Message}");
            if (Environment.GetEnvironmentVariable("RPFTOOL_DEBUG") == "1")
                Console.Error.WriteLine(ex);
            return 2;
        }
    }

    static void PrintUsage()
    {
        Console.WriteLine("""
            GTACoop.RpfTool - RPF7 test araci

            Kullanim:
              header         <rpf> [--gameexe <GTA5.exe>]
              list           <rpf> [filtre] [--gameexe <GTA5.exe>]
              find           <rpf> <yol> [--gameexe <GTA5.exe>]
              extract        <rpf> <yol> [cikti] [--gameexe <GTA5.exe>]
              peek           <rpf> <yol> [baytSayisi] [--gameexe <GTA5.exe>]
              verify         <rpf> [--gameexe <GTA5.exe>]
              dlclist-list   <rpf> [--gameexe <GTA5.exe>]
              dlclist-add    <rpf> <paketAdi...> [--gameexe <GTA5.exe>]
              dlclist-remove <rpf> <paketAdi...> [--gameexe <GTA5.exe>]
              replace        <rpf> <yol> <dosya> [--no-verify] [--gameexe <GTA5.exe>]
              copy           <kaynak> <hedef>
              keyfind        <GTA5.exe yolu>
            """);
    }

    static RpfArchive OpenSmart(string path)
    {
        if (GameExe != null)
        {
            try
            {
                var probe = RpfArchive.Open(path);
                if (probe.HasEncryptedFiles || probe.Encryption == RpfEncryption.Aes)
                {
                    probe.Dispose();
                    var key = RpfCrypto.FindPcAesKeyInExe(GameExe);
                    var archive = RpfArchive.Open(path, key);
                    archive.SetAesKey(key);
                    return archive;
                }
                return probe;
            }
            catch (AesKeyRequiredException)
            {
                var key = RpfCrypto.FindPcAesKeyInExe(GameExe);
                var archive = RpfArchive.Open(path, key);
                archive.SetAesKey(key);
                return archive;
            }
        }

        return RpfArchive.Open(path);
    }

    static void OpenInfo(RpfArchive a)
    {
        Console.WriteLine($"Arsiv      : {a.Path}");
        Console.WriteLine($"Girdi      : {a.EntryCount}");
        Console.WriteLine($"Isimler    : {a.NamesLength} bayt");
        Console.WriteLine($"Sifreleme  : {a.Encryption}");
        Console.WriteLine($"Boyut      : {a.FileLength:N0} bayt");
    }

    static int Header(string path)
    {
        using var a = OpenSmart(path);
        OpenInfo(a);
        return 0;
    }

    static int List(string[] args)
    {
        using var a = OpenSmart(args[1]);
        string? filter = args.Length > 2 && !args[2].StartsWith("--") ? args[2].ToLowerInvariant() : null;
        int n = 0;
        foreach (var f in a.AllFiles())
        {
            if (filter != null && !f.Path.Contains(filter)) continue;
            Console.WriteLine($"{f.Path}  [{f.FileSize} bayt]" +
                (f is RpfBinaryFileEntry b && b.FileSize > 0 && b.FileUncompressedSize > b.FileSize ? " (sikistirilmis)" : "") +
                (f.IsEncrypted ? " (sifreli)" : ""));
            n++;
        }
        Console.WriteLine($"-- {n} dosya --");
        return 0;
    }

    static int Find(string[] args)
    {
        using var a = OpenSmart(args[1]);
        var f = a.Find(args[2]) ?? throw new FileNotFoundException($"Bulunamadi: {args[2]}");
        Console.WriteLine($"Bulundu: {f.Path}");
        Console.WriteLine($"  FileOffset : {f.FileOffset} (sektor) = {f.FileOffset * 512L:N0} bayt");
        Console.WriteLine($"  FileSize   : {f.FileSize:N0}");
        if (f is RpfBinaryFileEntry b)
        {
            Console.WriteLine($"  Uncompressed: {b.FileUncompressedSize:N0}");
            Console.WriteLine($"  Encrypted   : {b.IsEncrypted}");
        }
        return 0;
    }

    static int Extract(string[] args)
    {
        using var a = OpenSmart(args[1]);
        var f = a.Find(args[2]) ?? throw new FileNotFoundException($"Bulunamadi: {args[2]}");
        var data = a.ReadFile(f);
        string outPath = args.Length > 3 && !args[3].StartsWith("--")
            ? args[3]
            : Path.GetFileName(f.Path);
        File.WriteAllBytes(outPath, data);
        Console.WriteLine($"Yazildi: {outPath} ({data.Length:N0} bayt)");
        if (Dlclist.LooksLikeXml(data) && data.Length < 8192)
        {
            Console.WriteLine("--- icerik ---");
            Console.WriteLine(Encoding.UTF8.GetString(data));
        }
        return 0;
    }

    static int Peek(string[] args)
    {
        using var a = OpenSmart(args[1]);
        var f = a.Find(args[2]) ?? throw new FileNotFoundException($"Bulunamadi: {args[2]}");
        if (f is not RpfBinaryFileEntry bin) throw new InvalidOperationException("Binary dosya degil.");
        var raw = a.ReadStoredBytes(f);
        Console.WriteLine($"Ham: {raw.Length:N0} bayt, Uncompressed={bin.FileUncompressedSize:N0}, Encrypted={bin.IsEncrypted}");
        if (bin.IsEncrypted)
        {
            var key = GameExe != null ? RpfCrypto.FindPcAesKeyInExe(GameExe) : null;
            if (key == null) throw new AesKeyRequiredException(a.Path);
            raw = RpfCrypto.DecryptAes(raw, key);
            Console.WriteLine("-- sifre cozuldu --");
        }
        int n = Math.Min(args.Length > 3 && int.TryParse(args[3], out var k) ? k : 64, raw.Length);
        Console.WriteLine(Convert.ToHexString(raw.AsSpan(0, n)));
        var ascii = Encoding.ASCII.GetString(raw, 0, n)
            .Select(c => c >= 32 && c < 127 ? c : '.').ToArray();
        Console.WriteLine(new string(ascii));

        try
        {
            var outp = RpfCrypto.Inflate(raw, 0, out var mode);
            Console.WriteLine($"Inflate: BASARILI ({mode}) -> {outp.Length:N0} bayt");
            Console.WriteLine(Encoding.UTF8.GetString(outp.AsSpan(0, Math.Min(200, outp.Length))));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Inflate: basarisiz ({ex.Message})");
        }
        return 0;
    }

    static int Verify(string[] args)
    {
        using var a = OpenSmart(args[1]);
        OpenInfo(a);
        var r = a.VerifyStructure();
        Console.WriteLine(r);
        return r.IsHealthy ? 0 : 3;
    }

    static int DlclistList(string path)
    {
        using var a = OpenSmart(path);
        var f = a.Find(Dlclist.RelativePath)
            ?? throw new FileNotFoundException("dlclist.xml bulunamadi: " + Dlclist.RelativePath);
        var data = a.ReadFile(f);
        if (!Dlclist.LooksLikeXml(data))
            throw new InvalidDataException("dlclist.xml ham verisi XML degil.");
        var xml = Encoding.UTF8.GetString(data);
        var names = Dlclist.GetPackNames(xml);
        Console.WriteLine($"dlclist.xml: {names.Count} paket ({data.Length:N0} bayt)");
        foreach (var name in names) Console.WriteLine("  " + name);
        return 0;
    }

    static int DlclistAdd(string[] args)
    {
        using var a = OpenSmart(args[1]);
        var f = a.Find(Dlclist.RelativePath)
            ?? throw new FileNotFoundException("dlclist.xml bulunamadi.");
        var data = a.ReadFile(f);
        var xml = Encoding.UTF8.GetString(data);
        var before = Dlclist.GetPackNames(xml);
        var newXml = Dlclist.AddPackNames(xml, args.Skip(2));
        var after = Dlclist.GetPackNames(newXml);
        if (before.Count == after.Count)
        {
            Console.WriteLine("Degisiklik yok (paketler zaten listeli).");
            return 0;
        }
        var bytes = Encoding.UTF8.GetBytes(newXml);
        a.ReplaceFile(f, bytes);
        Console.WriteLine($"Eklendi: {string.Join(", ", after.Except(before, StringComparer.OrdinalIgnoreCase))}");
        Console.WriteLine($"Yeni boyut: {bytes.Length:N0} bayt (eski {data.Length:N0})");
        return 0;
    }

    static int DlclistRemove(string[] args)
    {
        using var a = OpenSmart(args[1]);
        var f = a.Find(Dlclist.RelativePath)
            ?? throw new FileNotFoundException("dlclist.xml bulunamadi.");
        var data = a.ReadFile(f);
        var xml = Encoding.UTF8.GetString(data);
        var before = Dlclist.GetPackNames(xml);
        var newXml = Dlclist.RemovePackNames(xml, args.Skip(2));
        var after = Dlclist.GetPackNames(newXml);
        if (before.Count == after.Count)
        {
            Console.WriteLine("Degisiklik yok (paketler listede degil).");
            return 0;
        }
        var bytes = Encoding.UTF8.GetBytes(newXml);
        a.ReplaceFile(f, bytes);
        Console.WriteLine($"Cikarildi: {string.Join(", ", before.Except(after, StringComparer.OrdinalIgnoreCase))}");
        Console.WriteLine($"Yeni boyut: {bytes.Length:N0} bayt (eski {data.Length:N0})");
        return 0;
    }

    static int Replace(string[] args)
    {
        using var a = OpenSmart(args[1]);
        var f = a.Find(args[2]) ?? throw new FileNotFoundException($"Bulunamadi: {args[2]}");
        var content = File.ReadAllBytes(args[3]);
        bool verify = !args.Contains("--no-verify");
        bool? compress = args.Contains("--raw") ? false : args.Contains("--compress") ? true : null;
        var oldSize = f.FileSize;
        a.ReplaceFile(f, content, verify, compress);
        Console.WriteLine($"Degistirildi: {f.Path}");
        Console.WriteLine($"  Eski depolama: {oldSize:N0} -> Yeni: {f.FileSize:N0} bayt (icerik: {content.Length:N0})");
        Console.WriteLine($"  Konum: {(f.FileSize <= oldSize ? "yerinde (in-place)" : "dosya sonuna eklendi")}");
        Console.WriteLine($"  Dogrulama: {(verify ? "basarili" : "atlandi")}");
        return 0;
    }

    static int Copy(string src, string dst)
    {
        File.Copy(src, dst, overwrite: true);
        var fi = new FileInfo(dst);
        Console.WriteLine($"Kopyalandi: {src} -> {dst} ({fi.Length:N0} bayt)");
        return 0;
    }

    static int KeyFind(string exePath)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var key = RpfCrypto.FindPcAesKeyInExe(exePath);
        sw.Stop();
        Console.WriteLine($"Anahtar bulundu ({sw.ElapsedMilliseconds} ms): " +
            Convert.ToHexString(key));
        return 0;
    }
}
