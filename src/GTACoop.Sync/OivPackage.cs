using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using GTACoop.Rpf;

namespace GTACoop.Sync;

/// <summary>OIV paketinden oyuna kopyalanacak tek dosya.</summary>
public sealed class OivFileCopy
{
    /// <summary>Zip içindeki tam girdi adı (content/...).</summary>
    public string SourceEntry { get; init; } = "";

    /// <summary>Oyun köküne göre hedef ("/" ayraçlı, örn. mods/update/x64/dlcpacks/x/dlc.rpf).</summary>
    public string Destination { get; init; } = "";

    public long Size { get; init; }
}

/// <summary>
/// OIV paketinin mevcut bir RPF arşivine ekleyeceği/değiştireceği tek dosya.
/// Yalnızca mods klasöründeki kopyalar hedeflenir; kök oyun arşivlerine dokunulmaz.
/// </summary>
public sealed class OivArchiveEdit
{
    /// <summary>Oyun köküne göre arşiv yolu ("/" ayraçlı, örn. mods/x64a.rpf).</summary>
    public string Archive { get; init; } = "";

    /// <summary>Arşiv içindeki hedef ("/" ayraçlı, örn. textures/rope.ytd).</summary>
    public string EntryPath { get; init; } = "";

    /// <summary>Zip içindeki tam girdi adı (content/...).</summary>
    public string SourceEntry { get; init; } = "";

    public long Size { get; init; }
}

/// <summary>
/// OpenIV (.oiv) paketlerini güvenli bir alt kümeyle okur.
///
/// Desteklenenler:
/// - Doğrudan dosya ekleme: scripts/, mods/ ve update/x64/dlcpacks/ (mods/ katmanına yönlendirilir).
/// - update/update.rpf içindeki dlclist.xml'e girdi ekleme (xml/text veya tam dlclist kaynağı).
/// - mods klasöründeki mevcut RPF arşivlerine (x64a.rpf, common.rpf, update.rpf,
///   dlcpacks/.../dlc.rpf) dosya ekleme/değiştirme. Yalnızca değişen girdi ve
///   küçük TOC başlığı yazılır; arşivin tamamı kopyalanmaz.
///
/// Desteklenmeyen işlemler (silme, iç içe arşiv, dlclist dışı XML/metin düzenlemesi)
/// uygulanmaz; paket yalnızca güvenli kısmıyla kurulur ve bu durum günlüğe yazılır.
/// </summary>
public sealed class OivPackage : IDisposable
{
    public const long MaxPackageBytes = 32L * 1024 * 1024 * 1024;
    public const long MaxEntryBytes = 16L * 1024 * 1024 * 1024;
    private const long MaxAssemblyBytes = 8L * 1024 * 1024;
    private const long MaxDlclistSourceBytes = 8L * 1024 * 1024;

    private readonly ZipArchive _zip;
    private readonly Dictionary<string, ZipArchiveEntry> _entries =
        new(StringComparer.OrdinalIgnoreCase);

    public string FilePath { get; }
    public string Name { get; private set; } = "";
    public string Description { get; private set; } = "";
    public string Version { get; private set; } = "";
    public string PackageId { get; private set; } = "";
    public List<OivFileCopy> FileAdds { get; } = new();
    public List<OivArchiveEdit> ArchiveEdits { get; } = new();
    public List<string> DlclistEntries { get; } = new();
    public List<string> Unsupported { get; } = new();

    public bool HasSupportedOps =>
        FileAdds.Count > 0 || ArchiveEdits.Count > 0 || DlclistEntries.Count > 0;

    private OivPackage(string path, ZipArchive zip)
    {
        FilePath = path;
        _zip = zip;
    }

    public static OivPackage Open(string path, Action<string>? log = null)
    {
        var fi = new FileInfo(path);
        if (!fi.Exists) throw new SyncException(SyncText.T("OIV paketi bulunamadı: {0}", path));
        if (fi.Length <= 0) throw new SyncException(SyncText.T("OIV paketi boş: {0}", path));
        if (fi.Length > MaxPackageBytes)
            throw new SyncException(SyncText.T("OIV paketi boyutu sınırı aşıyor: {0}", path));

        ZipArchive zip;
        try
        {
            zip = ZipFile.OpenRead(path);
        }
        catch (Exception ex)
        {
            throw new SyncException(SyncText.T("OIV paketi açılamadı (geçersiz zip): {0}", path), ex);
        }

        var pkg = new OivPackage(path, zip);
        try
        {
            pkg.IndexEntries();
            pkg.ParseAssembly(log);
            return pkg;
        }
        catch
        {
            pkg.Dispose();
            throw;
        }
    }

    /// <summary>Sunucu tarayıcısı için yalnızca görünen adı okur.</summary>
    public static string? ReadDisplayName(string oivPath)
    {
        try
        {
            using var pkg = Open(oivPath);
            return string.IsNullOrWhiteSpace(pkg.Name) ? null : pkg.Name;
        }
        catch
        {
            return null;
        }
    }

    private void IndexEntries()
    {
        foreach (var entry in _zip.Entries)
        {
            var name = NormalizeEntryName(entry.FullName);
            if (name.Length == 0 || !IsSafeRelative(name, allowWildcard: false)) continue;
            if (!name.EndsWith('/') && entry.Length > MaxEntryBytes)
                throw new SyncException(SyncText.T("OIV içindeki dosya çok büyük: {0}", name));
            _entries[name] = entry;
        }
    }

    private void ParseAssembly(Action<string>? log)
    {
        var assembly = _entries.TryGetValue("assembly.xml", out var entry) ? entry : null;
        if (assembly == null)
            throw new SyncException(SyncText.T("OIV paketinde assembly.xml yok."));
        if (assembly.Length > MaxAssemblyBytes)
            throw new SyncException(SyncText.T("OIV assembly.xml çok büyük."));

        XDocument doc;
        using (var stream = assembly.Open())
        using (var reader = XmlReader.Create(stream, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            MaxCharactersInDocument = MaxAssemblyBytes,
            IgnoreComments = true
        }))
        {
            doc = XDocument.Load(reader, LoadOptions.None);
        }

        var root = doc.Root ?? throw new SyncException(SyncText.T("OIV assembly.xml geçersiz."));
        if (!string.Equals(root.Name.LocalName, "package", StringComparison.OrdinalIgnoreCase))
            throw new SyncException(SyncText.T("OIV assembly.xml kökü 'package' değil."));

        var target = (string?)root.Attribute("target") ?? "";
        if (target.Length > 0 && !target.Equals("Five", StringComparison.OrdinalIgnoreCase))
            throw new SyncException(SyncText.T("OIV hedefi desteklenmiyor: {0}", target));

        PackageId = (string?)root.Attribute("id") ?? "";
        var format = (string?)root.Attribute("version") ?? "";
        if (format.Length > 0 && format.StartsWith("1", StringComparison.Ordinal))
            throw new SyncException(SyncText.T("OIV 1.x biçimi desteklenmiyor: {0}", format));

        ReadMetadata(root);

        var content = root.Elements().FirstOrDefault(e =>
            e.Name.LocalName.Equals("content", StringComparison.OrdinalIgnoreCase));
        if (content == null)
            throw new SyncException(SyncText.T("OIV assembly.xml içinde <content> yok."));

        foreach (var child in content.Elements())
        {
            var local = child.Name.LocalName.ToLowerInvariant();
            switch (local)
            {
                case "add":
                    ParseFileAdd(child);
                    break;
                case "archive":
                    ParseArchive(child);
                    break;
                case "delete":
                case "replace":
                    Unsupported.Add(SyncText.T("{0}: {1} (silme/değiştirme güvenlik için uygulanmaz)",
                        local, Shorten(child.Value)));
                    break;
                default:
                    Unsupported.Add(SyncText.T("Desteklenmeyen OIV işlemi: {0}", local));
                    break;
            }
        }

        if (!HasSupportedOps)
            throw new SyncException(SyncText.T(
                "OIV paketinde güvenle uygulanabilecek işlem yok. Desteklenmeyen: {0}",
                Unsupported.Count == 0 ? "-" : string.Join("; ", Unsupported.Take(4))));

        if (Unsupported.Count > 0)
        {
            log?.Invoke(SyncText.T("OIV: {0} desteklenmeyen işlem atlanacak.", Unsupported.Count));
            foreach (var reason in Unsupported.Take(8))
                log?.Invoke("  - " + reason);
            if (Unsupported.Count > 8)
                log?.Invoke("  - ...");
        }
    }

    private void ReadMetadata(XElement root)
    {
        var metadata = root.Elements().FirstOrDefault(e =>
            e.Name.LocalName.Equals("metadata", StringComparison.OrdinalIgnoreCase));

        Name = metadata?.Elements().FirstOrDefault(e =>
            e.Name.LocalName.Equals("name", StringComparison.OrdinalIgnoreCase))?.Value.Trim() ?? "";
        if (Name.Length == 0)
            Name = Path.GetFileNameWithoutExtension(FilePath);

        var desc = metadata?.Elements().FirstOrDefault(e =>
            e.Name.LocalName.Equals("description", StringComparison.OrdinalIgnoreCase))?.Value ?? "";
        Description = CleanDescription(desc);

        var version = metadata?.Elements().FirstOrDefault(e =>
            e.Name.LocalName.Equals("version", StringComparison.OrdinalIgnoreCase));
        if (version != null)
        {
            var major = version.Elements().FirstOrDefault(e =>
                e.Name.LocalName.Equals("major", StringComparison.OrdinalIgnoreCase))?.Value.Trim();
            var minor = version.Elements().FirstOrDefault(e =>
                e.Name.LocalName.Equals("minor", StringComparison.OrdinalIgnoreCase))?.Value.Trim();
            var tag = version.Elements().FirstOrDefault(e =>
                e.Name.LocalName.Equals("tag", StringComparison.OrdinalIgnoreCase))?.Value.Trim();
            var parts = new[] { major, minor }.Where(p => !string.IsNullOrWhiteSpace(p));
            Version = string.Join(".", parts);
            if (!string.IsNullOrWhiteSpace(tag)) Version += " " + tag;
        }
    }

    private void ParseFileAdd(XElement add)
    {
        var source = (string?)add.Attribute("source");
        if (string.IsNullOrWhiteSpace(source))
        {
            Unsupported.Add(SyncText.T("<add> kaynağı yok: {0}", Shorten(add.Value)));
            return;
        }

        var destination = NormalizeEntryName(add.Value);
        if (destination.Length == 0)
        {
            Unsupported.Add(SyncText.T("<add> hedefi yok: {0}", source));
            return;
        }

        if (destination.Equals("update/update.rpf", StringComparison.OrdinalIgnoreCase) ||
            destination.Equals("mods/update/update.rpf", StringComparison.OrdinalIgnoreCase))
        {
            Unsupported.Add(SyncText.T("update.rpf doğrudan değiştirilemez; yalnızca dlclist girdileri eklenir."));
            return;
        }

        var sources = ResolveSources(source, out var sourceIsFolder);
        if (sources.Count == 0)
        {
            Unsupported.Add(SyncText.T("OIV kaynağı bulunamadı: {0}", source));
            return;
        }

        foreach (var (entryName, size, rel) in sources)
        {
            // Klasör/çoklu kaynakta hedef klasördür; dosyanın kaynak içindeki
            // göreli yolu korunur (alt klasörler düzleştirilmez).
            var finalDest = destination.EndsWith('/') || sourceIsFolder || sources.Count > 1
                ? CombineDestination(destination, rel)
                : destination;

            var mapped = MapDestination(finalDest, out var reason);
            if (mapped == null)
            {
                Unsupported.Add(SyncText.T("{0} -> {1}: {2}", source, finalDest, reason));
                continue;
            }

            if (FileAdds.Any(f => f.Destination.Equals(mapped, StringComparison.OrdinalIgnoreCase)))
                continue;

            FileAdds.Add(new OivFileCopy
            {
                SourceEntry = entryName,
                Destination = mapped,
                Size = size
            });
        }
    }

    private void ParseArchive(XElement archive)
    {
        var rawPath = NormalizeEntryName((string?)archive.Attribute("path") ?? "");
        var target = MapArchiveToMods(rawPath, out var mapReason);
        if (target == null)
        {
            Unsupported.Add(SyncText.T("Arşiv düzenlemesi atlandı: {0} ({1})", rawPath, mapReason));
            return;
        }

        bool isUpdate = target.Equals("mods/update/update.rpf", StringComparison.OrdinalIgnoreCase);

        foreach (var child in archive.Elements())
        {
            var local = child.Name.LocalName.ToLowerInvariant();
            switch (local)
            {
                case "xml":
                    if (isUpdate) ParseDlclistXml(child);
                    else Unsupported.Add(SyncText.T(
                        "Yalnızca dlclist.xml XML düzenlemesi destekleniyor: {0}", rawPath));
                    break;

                case "text":
                    if (isUpdate) ParseDlclistText(child);
                    else Unsupported.Add(SyncText.T(
                        "Yalnızca dlclist.xml metin düzenlemesi destekleniyor: {0}", rawPath));
                    break;

                case "add":
                {
                    var destination = NormalizeEntryName(child.Value);
                    if (isUpdate && destination.Equals("common/data/dlclist.xml",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        ParseDlclistFile(child);
                        break;
                    }
                    ParseArchiveFile(child, target);
                    break;
                }

                case "replace":
                {
                    var destination = NormalizeEntryName(child.Value);
                    if (isUpdate && destination.Equals("common/data/dlclist.xml",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        Unsupported.Add(SyncText.T(
                            "dlclist.xml'in tamamını değiştirmek güvenlik için uygulanmaz."));
                        break;
                    }
                    ParseArchiveFile(child, target);
                    break;
                }

                case "delete":
                    Unsupported.Add(SyncText.T(
                        "Arşivden dosya silme güvenlik için uygulanmaz: {0} ({1})",
                        rawPath, Shorten(child.Value)));
                    break;

                default:
                    Unsupported.Add(SyncText.T("Desteklenmeyen arşiv işlemi ({0}): {1}",
                        local, rawPath));
                    break;
            }
        }
    }

    /// <summary>
    /// OIV arşiv yolunu yalnızca mods klasöründeki mevcut kopyaya eşler.
    /// Kök oyun arşivleri ve iç içe (nested) arşivler desteklenmez.
    /// </summary>
    private static string? MapArchiveToMods(string path, out string reason)
    {
        reason = "";
        var p = NormalizeEntryName(path).TrimEnd('/');
        if (p.Length == 0)
        {
            reason = SyncText.T("arşiv yolu boş");
            return null;
        }
        if (!p.EndsWith(".rpf", StringComparison.OrdinalIgnoreCase))
        {
            reason = SyncText.T("desteklenmeyen arşiv türü");
            return null;
        }
        if (p.Contains(".rpf/", StringComparison.OrdinalIgnoreCase) ||
            p.Contains(".rpf\\", StringComparison.OrdinalIgnoreCase))
        {
            reason = SyncText.T("iç içe arşiv düzenlemesi desteklenmiyor");
            return null;
        }
        if (!IsSafeRelative(p, allowWildcard: false))
        {
            reason = SyncText.T("geçersiz/güvenlik dışı arşiv yolu");
            return null;
        }

        // OpenIV, mods klasörü açıkken güncelleme arşivlerini ve diğer kök
        // arşivleri otomatik olarak mods katmanına yönlendirir. Bu yüzden
        // x64a.rpf, common.rpf, update/update2.rpf, x64/audio/... gibi tüm
        // göreli .rpf yolları mods/<yol> olarak denenir. Kopya yoksa
        // ApplyRpfEdits düzenlemeyi güvenle atlar.
        if (p.Equals("update.rpf", StringComparison.OrdinalIgnoreCase) ||
            p.Equals("update/update.rpf", StringComparison.OrdinalIgnoreCase))
            return "mods/update/update.rpf";

        if (p.StartsWith("mods/", StringComparison.OrdinalIgnoreCase))
            return p;

        return "mods/" + p;
    }

    private void ParseArchiveFile(XElement op, string archive)
    {
        var source = (string?)op.Attribute("source");
        if (string.IsNullOrWhiteSpace(source))
        {
            Unsupported.Add(SyncText.T("Arşiv dosya işleminin kaynağı yok: {0}",
                Shorten(op.Value)));
            return;
        }

        var destination = NormalizeEntryName(op.Value);
        if (destination.Length == 0 || !IsSafeRelative(destination, allowWildcard: false))
        {
            Unsupported.Add(SyncText.T("Arşiv dosya hedefi geçersiz: {0}", Shorten(op.Value)));
            return;
        }
        if (destination.Equals("common/data/dlclist.xml", StringComparison.OrdinalIgnoreCase) &&
            archive.Equals("mods/update/update.rpf", StringComparison.OrdinalIgnoreCase))
        {
            Unsupported.Add(SyncText.T(
                "dlclist.xml yalnızca <xml>/<text> veya tam kaynak <add> ile güncellenir."));
            return;
        }

        var sources = ResolveSources(source, out var sourceIsFolder);
        if (sources.Count == 0)
        {
            Unsupported.Add(SyncText.T("OIV arşiv kaynağı bulunamadı: {0}", source));
            return;
        }

        foreach (var (entryName, size, rel) in sources)
        {
            var finalDest = destination.EndsWith('/') || sourceIsFolder || sources.Count > 1
                ? CombineDestination(destination, rel)
                : destination;
            finalDest = finalDest.Replace('\\', '/').Trim('/');
            if (finalDest.Length == 0 ||
                !IsSafeRelative(finalDest, allowWildcard: false))
            {
                Unsupported.Add(SyncText.T("Arşiv içi hedef geçersiz: {0}", finalDest));
                continue;
            }

            if (ArchiveEdits.Any(e =>
                    e.Archive.Equals(archive, StringComparison.OrdinalIgnoreCase) &&
                    e.EntryPath.Equals(finalDest, StringComparison.OrdinalIgnoreCase)))
                continue;

            ArchiveEdits.Add(new OivArchiveEdit
            {
                Archive = archive,
                EntryPath = finalDest,
                SourceEntry = entryName,
                Size = size
            });
        }
    }

    private void ParseDlclistXml(XElement xml)
    {
        var path = NormalizeEntryName((string?)xml.Attribute("path") ?? "");
        if (!path.Equals("common/data/dlclist.xml", StringComparison.OrdinalIgnoreCase))
        {
            Unsupported.Add(SyncText.T(
                "update.rpf XML düzenlemesi desteklenmiyor (yalnızca dlclist.xml): {0}", path));
            return;
        }

        foreach (var op in xml.Elements())
        {
            var local = op.Name.LocalName.ToLowerInvariant();
            if (local is "add" or "insert" or "replace")
            {
                int before = DlclistEntries.Count;
                ExtractItems(op);
                if (DlclistEntries.Count == before)
                    Unsupported.Add(SyncText.T("dlclist XML işleminde <Item> bulunamadı."));
            }
            else
            {
                Unsupported.Add(SyncText.T("dlclist XML işlemi güvenlik için uygulanmaz: {0}", local));
            }
        }
    }

    private void ParseDlclistText(XElement text)
    {
        var path = NormalizeEntryName((string?)text.Attribute("path") ?? "");
        if (!path.Equals("common/data/dlclist.xml", StringComparison.OrdinalIgnoreCase))
        {
            Unsupported.Add(SyncText.T(
                "update.rpf metin düzenlemesi desteklenmiyor (yalnızca dlclist.xml): {0}", path));
            return;
        }

        var value = text.Value ?? "";
        int before = DlclistEntries.Count;
        foreach (Match m in Regex.Matches(value, @"<Item>\s*(.*?)\s*</Item>",
                     RegexOptions.IgnoreCase | RegexOptions.Singleline))
        {
            AddDlclistEntry(WebUtility.HtmlDecode(m.Groups[1].Value));
        }
        if (DlclistEntries.Count == before)
            Unsupported.Add(SyncText.T("dlclist metin işleminde <Item> bulunamadı."));
    }

    private void ParseDlclistFile(XElement add)
    {
        var destination = NormalizeEntryName(add.Value);
        if (!destination.Equals("common/data/dlclist.xml", StringComparison.OrdinalIgnoreCase))
        {
            Unsupported.Add(SyncText.T(
                "update.rpf dosya ekleme desteklenmiyor (yalnızca dlclist.xml): {0}", destination));
            return;
        }

        var source = (string?)add.Attribute("source");
        var entry = string.IsNullOrWhiteSpace(source) ? null : FindEntry(source);
        if (entry == null)
        {
            Unsupported.Add(SyncText.T("dlclist kaynağı bulunamadı: {0}", source ?? "-"));
            return;
        }
        if (entry.Length > MaxDlclistSourceBytes)
        {
            Unsupported.Add(SyncText.T("dlclist kaynağı çok büyük: {0}", source));
            return;
        }

        try
        {
            using var stream = entry.Open();
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            var (xml, _) = Dlclist.Decode(ms.ToArray());
            foreach (var name in Dlclist.GetPackNames(xml))
                AddPackName(name);
        }
        catch (Exception ex)
        {
            Unsupported.Add(SyncText.T("dlclist kaynağı okunamadı: {0} ({1})", source, ex.Message));
        }
    }

    private void ExtractItems(XElement op)
    {
        foreach (var item in op.DescendantsAndSelf()
                     .Where(e => e.Name.LocalName.Equals("Item", StringComparison.OrdinalIgnoreCase)))
        {
            AddDlclistEntry(item.Value);
        }

        // CDATA/metin içinde kalmış <Item> kayıtları için yedek yol.
        if (!op.DescendantsAndSelf().Any(e =>
                e.Name.LocalName.Equals("Item", StringComparison.OrdinalIgnoreCase)))
        {
            foreach (Match m in Regex.Matches(op.Value ?? "", @"<Item>\s*(.*?)\s*</Item>",
                         RegexOptions.IgnoreCase | RegexOptions.Singleline))
                AddDlclistEntry(WebUtility.HtmlDecode(m.Groups[1].Value));
        }
    }

    private void AddDlclistEntry(string value)
    {
        var name = Dlclist.ParseItem(value);
        if (string.IsNullOrWhiteSpace(name)) return;
        AddPackName(name);
    }

    private void AddPackName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return;
        name = name.Trim();
        if (name.Any(ch => ch is '/' or '\\' or ':' or '*' or '?' or '"' or '<' or '>' or '|')) return;
        if (name is "." or ".." || name.Contains("..", StringComparison.Ordinal)) return;
        if (!DlclistEntries.Contains(name, StringComparer.OrdinalIgnoreCase))
            DlclistEntries.Add(name);
    }

    private List<(string EntryName, long Size, string Rel)> ResolveSources(
        string source, out bool sourceIsFolder)
    {
        sourceIsFolder = false;
        var raw = NormalizeEntryName(source);
        var result = new List<(string, long, string)>();

        if (raw.Contains('*') || raw.Contains('?'))
        {
            bool contentRelative =
                raw.StartsWith("content/", StringComparison.OrdinalIgnoreCase);
            var regex = new Regex("^" + Regex.Escape(raw)
                    .Replace("\\*", ".*").Replace("\\?", ".") + "$",
                RegexOptions.IgnoreCase | RegexOptions.Singleline |
                RegexOptions.CultureInvariant);

            int firstWild = raw.IndexOfAny(new[] { '*', '?' });
            var basePrefix = raw[..firstWild].TrimEnd('/');
            var baseWithSlash = basePrefix.Length == 0 ? "" : basePrefix + "/";

            foreach (var kv in _entries)
            {
                if (kv.Key.EndsWith('/')) continue;
                var candidate = contentRelative ? kv.Key : StripContentPrefix(kv.Key);
                if (!regex.IsMatch(candidate)) continue;

                var rel = candidate.StartsWith(baseWithSlash,
                        StringComparison.OrdinalIgnoreCase)
                    ? candidate[baseWithSlash.Length..]
                    : Path.GetFileName(candidate);
                if (rel.Length == 0 || rel.Contains("..")) continue;
                result.Add((kv.Key, kv.Value.Length, rel));
            }

            sourceIsFolder = result.Count > 1;
            return result;
        }

        var entry = FindEntry(raw);
        if (entry != null)
        {
            var name = NormalizeEntryName(entry.FullName);
            return new List<(string, long, string)>
            {
                (name, entry.Length, Path.GetFileName(name))
            };
        }

        // Klasör kaynağı: altındaki tüm dosyaları göreli yollarıyla ekle.
        foreach (var root in SourceRoots(raw))
        {
            foreach (var kv in _entries)
            {
                if (kv.Key.EndsWith('/')) continue;
                if (!kv.Key.StartsWith(root + "/", StringComparison.OrdinalIgnoreCase)) continue;
                var rel = kv.Key[(root.Length + 1)..];
                if (rel.Length == 0 || rel.Contains("..")) continue;
                result.Add((kv.Key, kv.Value.Length, rel));
            }
            if (result.Count > 0)
            {
                sourceIsFolder = true;
                break;
            }
        }
        return result;
    }

    private static string StripContentPrefix(string entry) =>
        entry.StartsWith("content/", StringComparison.OrdinalIgnoreCase)
            ? entry["content/".Length..]
            : entry;

    private IEnumerable<string> SourceRoots(string source)
    {
        var s = NormalizeEntryName(source).TrimEnd('/');
        if (s.StartsWith("content/", StringComparison.OrdinalIgnoreCase))
        {
            yield return s;
            yield break;
        }
        yield return "content/" + s;
        yield return s;
    }

    private ZipArchiveEntry? FindEntry(string source)
    {
        var s = NormalizeEntryName(source);
        foreach (var root in SourceRoots(s))
        {
            if (_entries.TryGetValue(root, out var entry) && !root.EndsWith('/'))
                return entry;
        }
        return null;
    }

    /// <summary>OIV hedef yolunu güvenli oyun yoluyla eşler. Desteklenmiyorsa null.</summary>
    public static string? MapDestination(string destination, out string reason)
    {
        reason = "";
        var p = NormalizeEntryName(destination);
        if (p.Length == 0)
        {
            reason = SyncText.T("boş hedef");
            return null;
        }
        if (p.EndsWith('/'))
            p = p.TrimEnd('/');
        if (!IsSafeRelative(p, allowWildcard: false))
        {
            reason = SyncText.T("geçersiz/güvenlik dışı yol");
            return null;
        }

        if (p.Equals("update/update.rpf", StringComparison.OrdinalIgnoreCase) ||
            p.Equals("mods/update/update.rpf", StringComparison.OrdinalIgnoreCase))
        {
            reason = SyncText.T("update.rpf doğrudan değiştirilemez");
            return null;
        }

        // OpenIV'nin mods klasörü davranışı: update/... -> mods/update/...
        if (p.StartsWith("update/", StringComparison.OrdinalIgnoreCase))
        {
            if (!p.StartsWith("update/x64/dlcpacks/", StringComparison.OrdinalIgnoreCase))
            {
                reason = SyncText.T("update/ altındaki gevşek dosyalar güvenli değil (yalnızca dlcpacks)");
                return null;
            }
            return "mods/" + p;
        }

        if (p.StartsWith("mods/", StringComparison.OrdinalIgnoreCase))
            return p;

        if (p.StartsWith("scripts/", StringComparison.OrdinalIgnoreCase))
            return p;

        // Oyun köküne yalnızca veri/eklenti dosyaları; çalıştırılabilir dosyalar engellenir.
        if (!p.Contains('/'))
        {
            var ext = Path.GetExtension(p);
            if (RootExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
                return p;
            reason = SyncText.T("oyun köküne yalnızca güvenli eklenti/veri dosyaları eklenir ({0})", ext);
            return null;
        }

        reason = SyncText.T("desteklenmeyen hedef klasör");
        return null;
    }

    private static readonly string[] RootExtensions =
    {
        ".asi", ".dll", ".ini", ".xml", ".cfg", ".config", ".txt", ".log",
        ".pdb", ".dat", ".json", ".cs", ".vb"
    };

    /// <summary>
    /// RPF arşivine yazılacak kaynağı belleğe okur. Çok büyük girdiler bellek
    /// şişmesini önlemek için reddedilir; paket bütünüyle geri alınır.
    /// </summary>
    public byte[] ReadArchiveEntry(OivArchiveEdit edit, long maxBytes)
    {
        if (!_entries.TryGetValue(edit.SourceEntry, out var entry))
            throw new SyncException(SyncText.T("OIV kaynağı bulunamadı: {0}", edit.SourceEntry));
        if (entry.Length > maxBytes || entry.Length > int.MaxValue - 64)
            throw new SyncException(SyncText.T("OIV arşiv dosyası çok büyük: {0}", edit.SourceEntry));

        using var stream = entry.Open();
        using var ms = new MemoryStream((int)entry.Length);
        stream.CopyTo(ms);
        return ms.ToArray();
    }

    public string CopyEntryTo(OivFileCopy copy, string destination)
    {
        if (!_entries.TryGetValue(copy.SourceEntry, out var entry))
            throw new SyncException(SyncText.T("OIV kaynağı bulunamadı: {0}", copy.SourceEntry));
        if (entry.Length > MaxEntryBytes)
            throw new SyncException(SyncText.T("OIV dosyası çok büyük: {0}", copy.SourceEntry));

        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        using var input = entry.Open();
        using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write,
            FileShare.None, 128 * 1024, FileOptions.SequentialScan);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[128 * 1024];
        try
        {
            int read;
            while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
            {
                output.Write(buffer, 0, read);
                hash.AppendData(buffer, 0, read);
            }
            output.Flush(true);
            return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
        }
        catch
        {
            try { output.Dispose(); } catch { }
            try { File.Delete(destination); } catch { }
            throw;
        }
    }

    private static string CombineDestination(string destination, string relativePath)
    {
        var d = NormalizeEntryName(destination).TrimEnd('/');
        var f = NormalizeEntryName(relativePath).Trim('/');
        return d.Length == 0 ? f : d + "/" + f;
    }

    private static string NormalizeEntryName(string value) =>
        (value ?? "").Replace('\\', '/').Trim().TrimStart('/');

    private static bool IsSafeRelative(string path, bool allowWildcard)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        if (path.Contains('\0')) return false;
        if (path.StartsWith("/", StringComparison.Ordinal) ||
            path.StartsWith("\\", StringComparison.Ordinal)) return false;
        if (path.Length >= 2 && char.IsLetter(path[0]) && path[1] == ':') return false;

        var parts = path.Split('/');
        foreach (var raw in parts)
        {
            var part = raw.Trim();
            if (part.Length == 0 || part == "." || part == "..") return false;
            if (!allowWildcard && part.Contains('*')) return false;
            if (part.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) return false;
            if (part.Contains(':') || part.Contains('?') || part.Contains('"') ||
                part.Contains('<') || part.Contains('>') || part.Contains('|')) return false;
            if (part.EndsWith('.') || part.EndsWith(' ')) return false;

            var stem = part.Split('.')[0].ToUpperInvariant();
            if (stem is "CON" or "PRN" or "AUX" or "NUL" or "COM1" or "COM2" or "COM3" or
                "COM4" or "COM5" or "COM6" or "COM7" or "COM8" or "COM9" or
                "LPT1" or "LPT2" or "LPT3" or "LPT4" or "LPT5" or "LPT6" or "LPT7" or
                "LPT8" or "LPT9")
                return false;
        }
        return true;
    }

    private static string CleanDescription(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        var sb = new StringBuilder(value.Length);
        bool lastSpace = false;
        foreach (var ch in value)
        {
            if (char.IsWhiteSpace(ch))
            {
                if (!lastSpace) sb.Append(' ');
                lastSpace = true;
            }
            else
            {
                sb.Append(ch);
                lastSpace = false;
            }
            if (sb.Length >= 400) break;
        }
        return sb.ToString().Trim();
    }

    private static string Shorten(string value)
    {
        var s = (value ?? "").Replace('\r', ' ').Replace('\n', ' ').Trim();
        return s.Length > 120 ? s[..120] + "..." : s;
    }

    public void Dispose() => _zip.Dispose();
}
