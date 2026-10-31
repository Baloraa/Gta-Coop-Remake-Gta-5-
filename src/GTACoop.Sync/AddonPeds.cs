using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using GTACoop.Rpf;

namespace GTACoop.Sync;

/// <summary>Sunucunun sunduğu tek bir addon ped.</summary>
public sealed class AddonPedInfo
{
    /// <summary>GTA V model adı (küçük harf, ör. "superman", "a_c_bull").</summary>
    public string Model { get; set; } = "";

    /// <summary>Oyuncuya gösterilecek ad.</summary>
    public string Name { get; set; } = "";

    /// <summary>"human", "female" veya "animal".</summary>
    public string Kind { get; set; } = "human";

    /// <summary>Sunucu bu pedi dağıtıyor mu?</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Ped bileşenleri bir klasörde mi (streamed ped)?</summary>
    public bool Streamed { get; set; }

    /// <summary>Oyuncu modeli değişince uygulanacak sağlık değeri.</summary>
    public int Health { get; set; } = 200;

    /// <summary>İsteğe bağlı meta veri alanları (boşsa şablon değeri kullanılır).</summary>
    public string ClipDictionary { get; set; } = "";
    public string MovementClipSet { get; set; } = "";
    public string DecisionMaker { get; set; } = "";
    public string Personality { get; set; } = "";
    public string CombatInfo { get; set; } = "";

    /// <summary>Bu pede ait gevşek dosyalar (paket klasörüne göreli, "/" ayraçlı).</summary>
    public List<string> Files { get; set; } = new();
}

/// <summary>Addons\peds\&lt;paket&gt;\peds.json içeriği.</summary>
public sealed class AddonPedPackFile
{
    public string DisplayName { get; set; } = "";
    public string Description { get; set; } = "";
    public List<AddonPedInfo> Peds { get; set; } = new();
}

/// <summary>İstemciye yazılan scripts\GTACoop-addonpeds.json içeriği.</summary>
public sealed class ClientAddonPedsConfig
{
    public string ServerName { get; set; } = "";
    public DateTime GeneratedUtc { get; set; } = DateTime.UtcNow;
    public List<ClientAddonPed> Peds { get; set; } = new();
}

public sealed class ClientAddonPed
{
    public string Model { get; set; } = "";
    public string Name { get; set; } = "";
    public string Kind { get; set; } = "human";
    public string Pack { get; set; } = "";
    public int Health { get; set; } = 200;
}

/// <summary>
/// Sunucudaki gevşek ped dosyalarını (.ydd/.ytd/.ymt/.yft) AddonPeds'in
/// peds.rpf yapısına benzer tek bir RPF içinde paketler. Kullanıcının kendi
/// AddonPeds kurulumuna, ap_m.xml dosyasına veya dlcpacks klasörüne dokunmaz.
/// </summary>
public static class AddonPeds
{
    public const string DirectoryName = "peds";
    public const string PackFileName = "peds.json";
    public const string PreparedDirectoryName = ".gtacoop-peds";
    public const string ClientConfigFileName = "GTACoop-addonpeds.json";
    public const string EmbeddedMetadataName = "gtacoop_peds.xml";
    private const int PrepareFormat = 3;
    private const string HumanKind = "human";
    private const string FemaleKind = "female";
    private const string AnimalKind = "animal";

    private static readonly string[] PedExtensions = { ".ydd", ".ytd", ".ymt", ".yft" };

    private static readonly HashSet<string> ContainerFolders = new(StringComparer.OrdinalIgnoreCase)
    {
        "streamed", "peds", "models", "addonpeds", "dlc", "files", "source", "loose"
    };

    // ---------------------------------------------------------------- keşif

    /// <summary>Paket klasöründeki tek bir mantıksal ped.</summary>
    public sealed class LoosePed
    {
        public string Model { get; init; } = "";
        public string Name { get; init; } = "";
        public string Kind { get; init; } = HumanKind;
        public bool Streamed { get; init; }
        public bool HasModelFile { get; init; }
        public List<string> Files { get; init; } = new();
    }

    public static bool IsPedFile(string path) =>
        PedExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Eski dlc.rpf/peds.rpf paketlerini bir kez gevşek dosyalara açar.
    /// Kaynak arşiv silinmez; yalnızca yoksa dosya eklenir.
    /// </summary>
    public static bool EnsureLooseSource(string packDir, Action<string>? log = null)
    {
        try
        {
            if (Directory.EnumerateFiles(packDir, "*", SearchOption.AllDirectories)
                .Any(IsPedFile))
                return true;

            var direct = Directory.EnumerateFiles(packDir, "peds.rpf", SearchOption.AllDirectories)
                .OrderBy(p => p.Count(c => c == Path.DirectorySeparatorChar))
                .FirstOrDefault();

            if (direct != null)
                return ExtractPedFiles(direct, packDir, log);

            var dlc = FindDlcRpf(packDir);
            if (dlc == null) return false;

            string? temp = null;
            try
            {
                using var outer = RpfArchive.Open(dlc);
                var stream = outer.Find("peds.rpf");
                if (stream != null)
                {
                    temp = Path.Combine(Path.GetTempPath(),
                        "gtacoop-peds-" + Guid.NewGuid().ToString("N") + ".rpf");
                    File.WriteAllBytes(temp, outer.ReadFile(stream));
                    if (ExtractPedFiles(temp, packDir, log)) return true;
                }

                // Bazı paketler ped dosyalarını doğrudan dlc.rpf içinde tutar.
                var extracted = 0;
                foreach (var file in outer.AllFiles())
                {
                    if (!IsPedFile(file.Path)) continue;
                    if (WriteExtracted(packDir, file.Path, outer.ReadFile(file), log)) extracted++;
                }
                return extracted > 0;
            }
            finally
            {
                if (temp != null)
                {
                    try { File.Delete(temp); } catch { }
                }
            }
        }
        catch (Exception ex)
        {
            log?.Invoke($"Ped kaynağı açılamadı ({Path.GetFileName(packDir)}): {ex.Message}");
            return false;
        }
    }

    private static bool ExtractPedFiles(string archivePath, string packDir, Action<string>? log)
    {
        var extracted = 0;
        using var archive = RpfArchive.Open(archivePath);
        foreach (var file in archive.AllFiles())
        {
            if (!IsPedFile(file.Path)) continue;
            if (WriteExtracted(packDir, file.Path, archive.ReadFile(file), log)) extracted++;
        }
        if (extracted > 0)
            log?.Invoke($"Ped arşivi gevşek dosyalara açıldı: {Path.GetFileName(packDir)} ({extracted} dosya)");
        return extracted > 0;
    }

    private static bool WriteExtracted(string packDir, string relPath, byte[] content,
        Action<string>? log)
    {
        try
        {
            var rel = relPath.Replace('/', '\\').TrimStart('\\');
            var target = Path.GetFullPath(Path.Combine(packDir, rel));
            var root = Path.GetFullPath(packDir);
            if (!target.StartsWith(root + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase))
                return false;
            if (File.Exists(target)) return false;

            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.WriteAllBytes(target, content);
            return true;
        }
        catch (Exception ex)
        {
            log?.Invoke($"Ped dosyası çıkarılamadı ({relPath}): {ex.Message}");
            return false;
        }
    }

    /// <summary>Paket klasöründeki gevşek ped dosyalarını mantıksal pedlere gruplar.</summary>
    public static List<LoosePed> DiscoverLoosePeds(string packDir, Action<string>? log = null,
        bool extractLegacy = true)
    {
        if (extractLegacy) EnsureLooseSource(packDir, log);

        var groups = new Dictionary<string, LooseGroup>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in Directory.EnumerateFiles(packDir, "*", SearchOption.AllDirectories))
        {
            if (!IsPedFile(file)) continue;

            var rel = Path.GetRelativePath(packDir, file).Replace('\\', '/');
            var parts = rel.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) continue;

            var skip = false;
            foreach (var part in parts)
            {
                if (part.StartsWith('.'))
                {
                    skip = true;
                    break;
                }
            }
            if (skip) continue;

            string key;
            bool inFolder = false;
            if (parts.Length > 1)
            {
                inFolder = true;
                var index = 0;
                if (ContainerFolders.Contains(parts[0]) && parts.Length > 2) index = 1;
                key = parts[index];
            }
            else
            {
                key = Path.GetFileNameWithoutExtension(parts[0]);
                var caret = key.IndexOf('^');
                if (caret > 0) key = key[..caret];
            }

            key = key.Trim().ToLowerInvariant();
            if (key.Length == 0) continue;

            if (!groups.TryGetValue(key, out var group))
            {
                group = new LooseGroup { Model = key };
                groups[key] = group;
            }

            group.Files.Add(rel);
            if (inFolder) group.Streamed = true;
            if (parts.Length == 1 &&
                (Path.GetExtension(file).Equals(".yft", StringComparison.OrdinalIgnoreCase) ||
                 Path.GetExtension(file).Equals(".ymt", StringComparison.OrdinalIgnoreCase)))
                group.HasModelFile = true;
            if (inFolder &&
                (Path.GetExtension(file).Equals(".ydd", StringComparison.OrdinalIgnoreCase) ||
                 Path.GetExtension(file).Equals(".ytd", StringComparison.OrdinalIgnoreCase)))
                group.HasModelFile = true;
        }

        var models = groups.Keys.ToList();
        var metadata = LoadMetadata(packDir, models);
        var result = new List<LoosePed>();
        foreach (var group in groups.Values)
        {
            group.Files.Sort(StringComparer.OrdinalIgnoreCase);
            metadata.TryGetValue(group.Model, out var item);
            result.Add(new LoosePed
            {
                Model = group.Model,
                Name = PrettifyName(group.Model),
                Kind = GuessKind(group.Model, item),
                Streamed = group.Streamed,
                HasModelFile = group.HasModelFile,
                Files = group.Files
            });
        }

        result.Sort((a, b) =>
        {
            var kind = KindSort(a.Kind).CompareTo(KindSort(b.Kind));
            return kind != 0 ? kind : StringComparer.CurrentCultureIgnoreCase.Compare(a.Name, b.Name);
        });
        return result;
    }

    private sealed class LooseGroup
    {
        public string Model = "";
        public bool Streamed;
        public bool HasModelFile;
        public List<string> Files = new();
    }

    private static int KindSort(string kind) =>
        kind == AnimalKind ? 1 : 0;

    /// <summary>peds.json dosyasını okur; yoksa keşfedilen dosyalardan oluşturur ve kaydeder.</summary>
    public static AddonPedPackFile LoadOrCreatePackFile(string packDir, IList<LoosePed> discovered)
    {
        var path = Path.Combine(packDir, PackFileName);
        var file = SyncJson.Load<AddonPedPackFile>(path) ?? new AddonPedPackFile();

        if (string.IsNullOrWhiteSpace(file.DisplayName))
        {
            var addon = SyncJson.Load<AddonMetaFile>(Path.Combine(packDir, "addon.json"));
            file.DisplayName = !string.IsNullOrWhiteSpace(addon?.DisplayName)
                ? addon!.DisplayName!
                : Path.GetFileName(packDir);
        }

        if (string.IsNullOrWhiteSpace(file.Description))
            file.Description = SyncJson.Load<AddonMetaFile>(
                Path.Combine(packDir, "addon.json"))?.Description ?? "";

        var remaining = file.Peds
            .Where(p => !string.IsNullOrWhiteSpace(p.Model))
            .GroupBy(p => p.Model, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();

        var merged = new List<AddonPedInfo>();
        foreach (var loose in discovered)
        {
            var existing = remaining.FirstOrDefault(p =>
                    p.Files != null && p.Files.Count > 0 &&
                    p.Files.Intersect(loose.Files, StringComparer.OrdinalIgnoreCase).Any())
                ?? remaining.FirstOrDefault(p =>
                    string.Equals(p.Model, loose.Model, StringComparison.OrdinalIgnoreCase));

            existing ??= new AddonPedInfo { Enabled = true };
            remaining.Remove(existing);

            existing.Model = loose.Model;
            existing.Files = loose.Files;
            existing.Streamed = loose.Streamed;
            if (string.IsNullOrWhiteSpace(existing.Name))
                existing.Name = loose.Name;
            if (existing.Kind is not (HumanKind or FemaleKind or AnimalKind))
                existing.Kind = loose.Kind;
            if (existing.Health <= 0) existing.Health = 200;
            existing.ClipDictionary ??= "";
            existing.MovementClipSet ??= "";
            existing.DecisionMaker ??= "";
            existing.Personality ??= "";
            existing.CombatInfo ??= "";
            merged.Add(existing);
        }

        file.Peds = merged
            .OrderBy(p => KindSort(p.Kind))
            .ThenBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(p => p.Model, StringComparer.OrdinalIgnoreCase)
            .ToList();

        try { SyncJson.Save(path, file); } catch { /* okunamayan disk: tarama devam eder */ }
        return file;
    }

    public sealed class AddonMetaFile
    {
        public string? DisplayName { get; set; }
        public string? Description { get; set; }
    }

    public static string? FindDlcRpf(string packDir)
    {
        try
        {
            return Directory.EnumerateFiles(packDir, "dlc.rpf", SearchOption.AllDirectories)
                .OrderBy(p => p.Count(c => c == Path.DirectorySeparatorChar))
                .ThenBy(p => p, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }

    public static string PrettifyName(string model)
    {
        var text = model.Replace('_', ' ').Trim();
        if (text.StartsWith("a c ", StringComparison.OrdinalIgnoreCase))
            text = text[4..].Trim();
        if (text.Length == 0) return model;
        return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(text);
    }

    public static string GuessKind(string model, XElement? metadata)
    {
        var pedType = metadata?.Element("Pedtype")?.Value;
        if (!string.IsNullOrWhiteSpace(pedType))
        {
            if (pedType.Contains("ANIMAL", StringComparison.OrdinalIgnoreCase))
                return AnimalKind;
            if (pedType.Contains("FEMALE", StringComparison.OrdinalIgnoreCase))
                return FemaleKind;
        }

        return model.StartsWith("a_c_", StringComparison.OrdinalIgnoreCase) ||
               model.StartsWith("a_c", StringComparison.OrdinalIgnoreCase) &&
               model.Length > 3 && !char.IsLetter(model[3])
            ? AnimalKind
            : HumanKind;
    }

    public static bool IsAnimal(string kind) =>
        string.Equals(kind, AnimalKind, StringComparison.OrdinalIgnoreCase);

    // ---------------------------------------------------------------- hazırlama

    private sealed class PrepareStamp
    {
        public int Format { get; set; }
        public string SourceHash { get; set; } = "";
    }

    /// <summary>
    /// Paketi kendi kendine yeterli hale getirir: gevşek dosyalardan AddonPeds
    /// uyumlu peds.rpf, onu taşıyan benzersiz bir dlc.rpf ve gömülü ped meta
    /// verisi üretir. Kaynak dosyalar ve kullanıcının ap_m.xml dosyası değiştirilmez.
    /// </summary>
    public static string PrepareSelfContained(string packDir, string addonId,
        string preparedRoot, AddonPedPackFile packFile, Action<string>? log = null)
    {
        Directory.CreateDirectory(preparedRoot);
        var target = Path.Combine(preparedRoot, "dlc.rpf");
        var stampPath = Path.Combine(preparedRoot, ".prepare.json");

        var enabled = packFile.Peds
            .Where(p => p.Enabled && !string.IsNullOrWhiteSpace(p.Model) &&
                        p.Files != null && p.Files.Count > 0)
            .ToList();
        if (enabled.Count == 0)
            throw new InvalidDataException("Etkin ped yok.");

        var items = LoadMetadata(packDir, enabled.Select(p => p.Model));
        var metadataXml = BuildMetadataXml(enabled, items);
        var sourceHash = ComputeSourceHash(packDir, enabled, metadataXml);

        var stamp = SyncJson.Load<PrepareStamp>(stampPath);
        if (stamp != null && File.Exists(target) &&
            stamp.Format == PrepareFormat &&
            string.Equals(stamp.SourceHash, sourceHash, StringComparison.Ordinal))
        {
            return target;
        }

        var buildDir = Path.Combine(preparedRoot, ".building-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(buildDir);
        var tempPeds = Path.Combine(buildDir, "peds.rpf");
        var tempDlc = Path.Combine(buildDir, "dlc.rpf");
        var old = target + ".old";
        try
        {
            var fileCount = BuildLoosePedRpf(packDir, enabled, tempPeds, log);
            BuildDlcRpf(tempDlc, addonId, tempPeds, metadataXml, log);

            using (var check = RpfArchive.Open(tempDlc))
            {
                _ = check.Find("setup2.xml")
                    ?? throw new InvalidDataException("setup2.xml yazılamadı.");
                _ = check.Find("appdata.xml")
                    ?? throw new InvalidDataException("appdata.xml yazılamadı.");
                _ = check.Find(EmbeddedMetadataName)
                    ?? throw new InvalidDataException("Gömülü ped meta verisi yazılamadı.");
                var stream = check.Find("peds.rpf")
                    ?? throw new InvalidDataException("peds.rpf yazılamadı.");
                if (stream.FileSize == 0 && stream.GetDataSize() == 0)
                    throw new InvalidDataException("peds.rpf boş.");
            }

            if (File.Exists(old)) File.Delete(old);
            if (File.Exists(target)) File.Move(target, old);
            File.Move(tempDlc, target);
            try { File.Delete(old); } catch { }

            SyncJson.Save(stampPath, new PrepareStamp
            {
                Format = PrepareFormat,
                SourceHash = sourceHash
            });
            log?.Invoke($"Ped paketi hazırlandı: {addonId} " +
                        $"({enabled.Count} ped, {fileCount} dosya)");
            return target;
        }
        catch
        {
            if (File.Exists(old) && !File.Exists(target))
            {
                try { File.Move(old, target); } catch { }
            }
            throw;
        }
        finally
        {
            try { Directory.Delete(buildDir, recursive: true); } catch { }
        }
    }

    private static string ComputeSourceHash(string packDir, IEnumerable<AddonPedInfo> peds,
        string metadataXml)
    {
        var sb = new StringBuilder();
        foreach (var ped in peds.OrderBy(p => p.Model, StringComparer.OrdinalIgnoreCase))
        {
            sb.Append("P:").Append(ped.Model).Append('|').Append(ped.Kind).Append('|')
              .Append(ped.Streamed ? 1 : 0).Append(';');
            foreach (var rel in ped.Files.OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
            {
                var full = Path.Combine(packDir, rel.Replace('/', Path.DirectorySeparatorChar));
                var info = new FileInfo(full);
                sb.Append(rel).Append(':').Append(info.Exists ? info.Length : -1).Append(':')
                  .Append(info.Exists ? info.LastWriteTimeUtc.Ticks : 0).Append(';');
            }
        }
        sb.Append(metadataXml);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString())));
    }

    private static int BuildLoosePedRpf(string packDir, IEnumerable<AddonPedInfo> peds,
        string target, Action<string>? log)
    {
        var files = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var ped in peds)
            foreach (var rel in ped.Files)
                files.Add(rel.Replace('/', '\\'));

        if (files.Count == 0)
            throw new InvalidDataException("Ped dosyası yok.");

        using (var rpf = RpfArchive.Create(target, overwrite: true, headerReserve: 1024 * 1024))
        {
            foreach (var rel in files)
            {
                var full = Path.Combine(packDir, rel);
                if (!File.Exists(full))
                    throw new FileNotFoundException($"Ped dosyası bulunamadı: {rel}");

                var bytes = File.ReadAllBytes(full);
                if (!RpfCrypto.HasResourceHeader(bytes))
                    throw new InvalidDataException(
                        $"'{rel}' geçerli bir GTA kaynak dosyası değil (RSC7 başlığı yok). " +
                        "Dosyayı OpenIV ile dışa aktarın.");

                rpf.SetFile(rel, bytes, verify: false);
            }
        }

        using (var verify = RpfArchive.Open(target))
        {
            var count = verify.AllFiles().Count();
            if (count != files.Count)
                throw new InvalidDataException(
                    $"peds.rpf doğrulanamadı ({count}/{files.Count} dosya).");
        }

        log?.Invoke($"peds.rpf oluşturuldu: {files.Count} dosya");
        return files.Count;
    }

    private static void BuildDlcRpf(string target, string addonId, string pedsRpf,
        string metadataXml, Action<string>? log)
    {
        var mountFolder = AddonApplier.MountFolderName(addonId);
        var device = "o_" + mountFolder;
        var changeSet = "GTACOOP_" + mountFolder.ToUpperInvariant() + "_AUTOGEN";

        var setup = new XDocument(new XDeclaration("1.0", "UTF-8", null),
            new XElement("SSetupData",
                new XElement("deviceName", device),
                new XElement("datFile", "appdata.xml"),
                new XElement("timeStamp", DateTime.UtcNow.ToString("dd/MM/yyyy HH:mm:ss",
                    System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("nameHash", mountFolder),
                new XElement("contentChangeSetGroups",
                    new XElement("Item",
                        new XElement("NameHash", "GROUP_STARTUP"),
                        new XElement("ContentChangeSets",
                            new XElement("Item", changeSet)))),
                new XElement("type", "EXTRACONTENT_COMPAT_PACK"),
                new XElement("order", new XAttribute("value", "1"))));

        var appdata = new XDocument(new XDeclaration("1.0", "UTF-8", null),
            new XElement("CDataFileMgr__ContentsOfDataFileXml",
                new XElement("disabledFiles"),
                new XElement("includedXmlFiles"),
                new XElement("includedDataFiles"),
                new XElement("dataFiles",
                    new XElement("Item",
                        new XElement("filename", device + ":/peds.rpf"),
                        new XElement("fileType", "PEDSTREAM_FILE"),
                        new XElement("overlay", new XAttribute("value", "false")),
                        new XElement("disabled", new XAttribute("value", "true")),
                        new XElement("persistent", new XAttribute("value", "true"))),
                    new XElement("Item",
                        new XElement("filename", device + ":/" + EmbeddedMetadataName),
                        new XElement("fileType", "PED_METADATA_FILE"),
                        new XElement("overlay", new XAttribute("value", "false")),
                        new XElement("disabled", new XAttribute("value", "true")),
                        new XElement("persistent", new XAttribute("value", "false")))),
                new XElement("contentChangeSets",
                    new XElement("Item",
                        new XElement("changeSetName", changeSet),
                        new XElement("mapChangeSetData"),
                        new XElement("filesToInvalidate"),
                        new XElement("filesToDisable"),
                        new XElement("filesToEnable",
                            new XElement("Item", device + ":/peds.rpf"),
                            new XElement("Item", device + ":/" + EmbeddedMetadataName)),
                        new XElement("txdToLoad"),
                        new XElement("txdToUnload"),
                        new XElement("residentResources"),
                        new XElement("unregisterResources"))),
                new XElement("patchFiles")));

        var pedsBytes = File.ReadAllBytes(pedsRpf);
        using var rpf = RpfArchive.Create(target, overwrite: true, headerReserve: 64 * 1024);
        rpf.SetFile("setup2.xml", SerializeXml(setup), verify: false);
        rpf.SetFile("appdata.xml", SerializeXml(appdata), verify: false);
        rpf.SetFile(EmbeddedMetadataName, Utf8NoBom(metadataXml), verify: false);
        rpf.SetFile("peds.rpf", pedsBytes, verify: false, compress: false);

        log?.Invoke($"dlc.rpf oluşturuldu: {device}");
    }

    private static byte[] SerializeXml(XDocument doc)
    {
        using var ms = new MemoryStream();
        doc.Save(ms, SaveOptions.DisableFormatting);
        return StripUtf8Bom(ms.ToArray());
    }

    private static byte[] StripUtf8Bom(byte[] bytes) =>
        bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF
            ? bytes[3..]
            : bytes;

    private static byte[] Utf8NoBom(string text) =>
        StripUtf8Bom(Encoding.UTF8.GetBytes(text));

    // ---------------------------------------------------------------- meta veri

    private static Dictionary<string, XElement> LoadMetadata(string packDir, IEnumerable<string> models)
    {
        var wanted = new HashSet<string>(models, StringComparer.OrdinalIgnoreCase);
        var result = new Dictionary<string, XElement>(StringComparer.OrdinalIgnoreCase);

        var candidates = new List<string>();
        foreach (var name in new[] { "pedmeta.xml", "ap_m.xml", "peds.xml", "pedmodelinfo.meta" })
        {
            var path = Path.Combine(packDir, name);
            if (File.Exists(path)) candidates.Add(path);
        }

        // Sunucuda kurulu AddonPeds meta verisi (yalnızca okunur; değiştirilmez).
        try
        {
            var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var apM = Path.Combine(docs, "Rockstar Games", "GTA V", "UserMods", "ap_m.xml");
            if (File.Exists(apM)) candidates.Add(apM);
        }
        catch { }

        foreach (var path in candidates)
        {
            try
            {
                var doc = XDocument.Load(path);
                foreach (var item in doc.Descendants("Item"))
                {
                    var name = item.Element("Name")?.Value?.Trim();
                    if (string.IsNullOrWhiteSpace(name) || !wanted.Contains(name)) continue;
                    if (!result.ContainsKey(name)) result[name] = new XElement(item);
                }
            }
            catch { /* bozuk meta dosyası diğerlerini engellemez */ }
        }

        return result;
    }

    public static string BuildMetadataXml(IEnumerable<AddonPedInfo> peds,
        IReadOnlyDictionary<string, XElement>? known = null)
    {
        known ??= new Dictionary<string, XElement>(StringComparer.OrdinalIgnoreCase);
        var root = new XElement("CPedModelInfo__InitDataList", new XElement("InitDatas"));
        var datas = root.Element("InitDatas")!;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var ped in peds)
        {
            if (string.IsNullOrWhiteSpace(ped.Model) || !seen.Add(ped.Model)) continue;
            datas.Add(BuildMetadataItem(ped, known));
        }

        using var ms = new MemoryStream();
        var doc = new XDocument(new XDeclaration("1.0", "utf-8", null), root);
        doc.Save(ms, SaveOptions.DisableFormatting);
        return Encoding.UTF8.GetString(StripUtf8Bom(ms.ToArray()));
    }

    public static XElement BuildMetadataItem(AddonPedInfo ped,
        IReadOnlyDictionary<string, XElement>? known = null)
    {
        XElement item;
        if (known != null && known.TryGetValue(ped.Model, out var source))
        {
            var sourceKind = GuessKind(ped.Model, source);
            if ((ped.Kind == AnimalKind) == (sourceKind == AnimalKind))
                item = new XElement(source);
            else
                item = XElement.Parse(TemplateFor(ped.Kind));
        }
        else
        {
            item = XElement.Parse(TemplateFor(ped.Kind));
        }

        item.SetElementValue("Name", ped.Model);

        if (ped.Kind == FemaleKind)
        {
            item.SetElementValue("Pedtype", "CIVFEMALE");
            item.SetElementValue("ClipDictionaryName", "move_f@generic");
            item.SetElementValue("MovementClipSet", "move_f@generic");
            item.SetElementValue("ExpressionSetName", "expr_set_ambient_female");
            item.SetElementValue("DefaultGestureClipSet", "ANIM_GROUP_GESTURE_F_GENERIC");
            item.SetElementValue("FacialClipsetGroupName", "facial_clipset_group_gen_female");
            item.SetElementValue("DefaultVisemeClipSet", "ANIM_GROUP_VISEMES_F_LO");
            item.SetElementValue("PoseMatcherName", "Female");
            item.SetElementValue("PoseMatcherProneName", "Female_prone");
            item.SetElementValue("PedCapsuleName", "STANDARD_FEMALE");
            item.SetElementValue("RelationshipGroup", "CIVFEMALE");
        }
        else if (ped.Kind == AnimalKind)
        {
            item.SetElementValue("Pedtype", "ANIMAL");
        }
        else
        {
            item.SetElementValue("Pedtype", "CIVMALE");
        }

        if (!string.IsNullOrWhiteSpace(ped.ClipDictionary))
            item.SetElementValue("ClipDictionaryName", ped.ClipDictionary.Trim());
        if (!string.IsNullOrWhiteSpace(ped.MovementClipSet))
            item.SetElementValue("MovementClipSet", ped.MovementClipSet.Trim());
        if (!string.IsNullOrWhiteSpace(ped.DecisionMaker))
            item.SetElementValue("DecisionMakerName", ped.DecisionMaker.Trim());
        if (!string.IsNullOrWhiteSpace(ped.Personality))
            item.SetElementValue("Personality", ped.Personality.Trim());
        if (!string.IsNullOrWhiteSpace(ped.CombatInfo))
            item.SetElementValue("CombatInfo", ped.CombatInfo.Trim());

        var streamed = item.Element("IsStreamedGfx");
        if (streamed != null)
            streamed.SetAttributeValue("value", ped.Streamed ? "true" : "false");
        else
            item.Add(new XElement("IsStreamedGfx",
                new XAttribute("value", ped.Streamed ? "true" : "false")));

        return item;
    }

    private static string TemplateFor(string kind) =>
        IsAnimal(kind) ? AnimalTemplate : HumanTemplate;

    // ---------------------------------------------------------------- istemci yapılandırması

    public static string ClientConfigPath(string gameFolder) =>
        Path.Combine(gameFolder, "scripts", ClientConfigFileName);

    public static void WriteClientConfig(string gameFolder, Manifest manifest,
        IEnumerable<InstalledAddon> installed)
    {
        var installedKeys = new HashSet<string>(
            installed.Select(a => a.Id + "|" + a.Type), StringComparer.OrdinalIgnoreCase);
        var config = new ClientAddonPedsConfig { ServerName = manifest.ServerName };

        foreach (var addon in manifest.Addons.Where(a => a.Type == AddonType.DlcPack &&
                                                         a.Peds.Count > 0))
        {
            if (!installedKeys.Contains(addon.Id + "|" + addon.Type)) continue;
            foreach (var ped in addon.Peds.Where(p => p.Enabled && !string.IsNullOrWhiteSpace(p.Model)))
            {
                config.Peds.Add(new ClientAddonPed
                {
                    Model = ped.Model,
                    Name = string.IsNullOrWhiteSpace(ped.Name) ? ped.Model : ped.Name,
                    Kind = ped.Kind,
                    Pack = string.IsNullOrWhiteSpace(addon.DisplayName) ? addon.Id : addon.DisplayName,
                    Health = ped.Health <= 0 ? 200 : ped.Health
                });
            }
        }

        var path = ClientConfigPath(gameFolder);
        try
        {
            if (config.Peds.Count == 0)
            {
                if (File.Exists(path)) File.Delete(path);
                return;
            }
            SyncJson.Save(path, config);
        }
        catch { /* istemci menüsü kritik değil; senkronizasyon devam eder */ }
    }

    public static void DeleteClientConfig(string gameFolder)
    {
        try
        {
            var path = ClientConfigPath(gameFolder);
            if (File.Exists(path)) File.Delete(path);
        }
        catch { }
    }

    // ---------------------------------------------------------------- düzenleme yardımcıları

    /// <summary>Pedin dosyalarını bir klasöre çıkarır (görüntüleme / yedek).</summary>
    public static int ExtractPed(string packDir, AddonPedInfo ped, string destination)
    {
        var count = 0;
        foreach (var rel in ped.Files)
        {
            var source = Path.Combine(packDir, rel.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(source)) continue;
            var target = Path.Combine(destination, rel.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(source, target, overwrite: true);
            count++;
        }

        var meta = BuildMetadataItem(ped);
        var metaPath = Path.Combine(destination, "pedmeta.xml");
        var doc = new XDocument(new XDeclaration("1.0", "utf-8", null),
            new XElement("CPedModelInfo__InitDataList",
                new XElement("InitDatas", meta)));
        File.WriteAllBytes(metaPath, SerializeXml(doc));
        return count;
    }

    /// <summary>Bir pedin dosyalarını yedek klasörüne taşır ve boş klasörleri temizler.</summary>
    public static int MovePedToBackup(string packDir, AddonPedInfo ped, string backupRoot,
        Action<string>? log = null)
    {
        var count = 0;
        foreach (var rel in ped.Files)
        {
            var source = Path.Combine(packDir, rel.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(source)) continue;
            var target = Path.Combine(backupRoot, rel.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Move(source, target, overwrite: true);
            count++;
        }

        var root = Path.GetFullPath(packDir);
        foreach (var rel in ped.Files)
        {
            var dir = Path.GetDirectoryName(Path.Combine(packDir,
                rel.Replace('/', Path.DirectorySeparatorChar)));
            while (dir != null && dir.Length > root.Length &&
                   dir.StartsWith(root + Path.DirectorySeparatorChar,
                       StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    if (Directory.EnumerateFileSystemEntries(dir).Any()) break;
                    Directory.Delete(dir);
                }
                catch { break; }
                dir = Path.GetDirectoryName(dir);
            }
        }

        log?.Invoke($"Ped yedeğe taşındı: {ped.Model} ({count} dosya)");
        return count;
    }

    // ---------------------------------------------------------------- şablonlar

    // AddonPeds ap_m.xml biçimindeki insan pedi şablonu.
    private const string HumanTemplate =
        "<Item><Name>template</Name><PropsName>null</PropsName>" +
        "<ClipDictionaryName>move_m@generic</ClipDictionaryName><BlendShapeFileName>null</BlendShapeFileName>" +
        "<ExpressionSetName>expr_set_ambient_male</ExpressionSetName><ExpressionDictionaryName>null</ExpressionDictionaryName>" +
        "<ExpressionName>null</ExpressionName><Pedtype>CIVMALE</Pedtype><MovementClipSet>move_m@generic</MovementClipSet>" +
        "<StrafeClipSet>move_ped_strafing</StrafeClipSet><MovementToStrafeClipSet>move_ped_to_strafe</MovementToStrafeClipSet>" +
        "<InjuredStrafeClipSet>move_strafe_injured</InjuredStrafeClipSet><FullBodyDamageClipSet>dam_ko</FullBodyDamageClipSet>" +
        "<AdditiveDamageClipSet>dam_ad</AdditiveDamageClipSet><DefaultGestureClipSet>ANIM_GROUP_GESTURE_M_GENERIC</DefaultGestureClipSet>" +
        "<FacialClipsetGroupName>facial_clipset_group_gen_male</FacialClipsetGroupName>" +
        "<DefaultVisemeClipSet>ANIM_GROUP_VISEMES_M_LO</DefaultVisemeClipSet><SidestepClipSet>CLIP_SET_ID_INVALID</SidestepClipSet>" +
        "<PoseMatcherName>Male</PoseMatcherName><PoseMatcherProneName>Male_prone</PoseMatcherProneName>" +
        "<GetupSetHash>NMBS_SLOW_GETUPS</GetupSetHash><CreatureMetadataName>null</CreatureMetadataName>" +
        "<DecisionMakerName>DEFAULT</DecisionMakerName><MotionTaskDataSetName>STANDARD_PED</MotionTaskDataSetName>" +
        "<DefaultTaskDataSetName>STANDARD_PED</DefaultTaskDataSetName><PedCapsuleName>STANDARD_MALE</PedCapsuleName>" +
        "<PedLayoutName /><PedComponentSetName /><PedComponentClothName /><PedIKSettingsName /><TaskDataName />" +
        "<IsStreamedGfx value=\"false\" /><AmbulanceShouldRespondTo value=\"true\" /><CanRideBikeWithNoHelmet value=\"false\" />" +
        "<CanSpawnInCar value=\"true\" /><IsHeadBlendPed value=\"false\" /><bOnlyBulkyItemVariations value=\"false\" />" +
        "<RelationshipGroup>CIVMALE</RelationshipGroup><NavCapabilitiesName>STANDARD_PED</NavCapabilitiesName>" +
        "<PerceptionInfo>DEFAULT_PERCEPTION</PerceptionInfo><DefaultBrawlingStyle>BS_AI</DefaultBrawlingStyle>" +
        "<DefaultUnarmedWeapon>WEAPON_UNARMED</DefaultUnarmedWeapon><Personality>DEFAULT</Personality>" +
        "<CombatInfo>DEFAULT</CombatInfo><VfxInfoName>VFXPEDINFO_HUMAN_GENERIC</VfxInfoName>" +
        "<AmbientClipsForFlee>FLEE</AmbientClipsForFlee><Radio1>RADIO_GENRE_PUNK</Radio1><Radio2>RADIO_GENRE_JAZZ</Radio2>" +
        "<PedVoiceGroup>BAR_PERSON_PVG</PedVoiceGroup><AbilityType>SAT_NONE</AbilityType><ThermalBehaviour>TB_WARM</ThermalBehaviour>" +
        "<SuperlodType>SLOD_HUMAN</SuperlodType><ScenarioPopStreamingSlot>SCENARIO_POP_STREAMING_NORMAL</ScenarioPopStreamingSlot>" +
        "<DefaultSpawningPreference>DSP_NORMAL</DefaultSpawningPreference>" +
        "<DefaultRemoveRangeMultiplier value=\"1.000000\" /><AllowCloseSpawning value=\"false\" /></Item>";

    // Çoğu dört ayaklı hayvan için makul bir başlangıç; paket kendi metasını
    // sağlıyorsa her zaman o kullanılır.
    private const string AnimalTemplate =
        "<Item><Name>template</Name><PropsName>null</PropsName>" +
        "<ClipDictionaryName>CREATURES@ROTTWEILER@MOVE</ClipDictionaryName><BlendShapeFileName>null</BlendShapeFileName>" +
        "<ExpressionSetName></ExpressionSetName><ExpressionDictionaryName>A_C_Rottweiler</ExpressionDictionaryName>" +
        "<ExpressionName>A_C_Rottweiler</ExpressionName><Pedtype>ANIMAL</Pedtype><MovementClipSet>ROTTWEILER@MOVE</MovementClipSet>" +
        "<StrafeClipSet>move_ped_strafing</StrafeClipSet><MovementToStrafeClipSet>move_ped_to_strafe</MovementToStrafeClipSet>" +
        "<InjuredStrafeClipSet>move_strafe_injured</InjuredStrafeClipSet><FullBodyDamageClipSet>dam_ko</FullBodyDamageClipSet>" +
        "<AdditiveDamageClipSet>dam_ad</AdditiveDamageClipSet><DefaultGestureClipSet>ANIM_GROUP_GESTURE_M_GENERIC</DefaultGestureClipSet>" +
        "<FacialClipsetGroupName>facial_clipset_group_gen_male</FacialClipsetGroupName>" +
        "<DefaultVisemeClipSet>ANIM_GROUP_VISEMES_M_LO</DefaultVisemeClipSet><SidestepClipSet>CREATURES@ROTTWEILER@STEP</SidestepClipSet>" +
        "<PoseMatcherName>rottweiler</PoseMatcherName><PoseMatcherProneName>null</PoseMatcherProneName>" +
        "<GetupSetHash>NMBS_ROTTWEILER_GETUPS</GetupSetHash><CreatureMetadataName>null</CreatureMetadataName>" +
        "<DecisionMakerName>DOG</DecisionMakerName><MotionTaskDataSetName>rottweiler</MotionTaskDataSetName>" +
        "<DefaultTaskDataSetName>Animal</DefaultTaskDataSetName><PedCapsuleName>Shepherd</PedCapsuleName>" +
        "<PedLayoutName /><PedComponentSetName>Animal</PedComponentSetName><PedComponentClothName /><PedIKSettingsName>NO_IK</PedIKSettingsName>" +
        "<TaskDataName>Animal</TaskDataName><IsStreamedGfx value=\"true\" /><AmbulanceShouldRespondTo value=\"true\" />" +
        "<CanRideBikeWithNoHelmet value=\"false\" /><CanSpawnInCar value=\"true\" /><IsHeadBlendPed value=\"false\" />" +
        "<bOnlyBulkyItemVariations value=\"false\" /><RelationshipGroup>WILD_ANIMAL</RelationshipGroup>" +
        "<NavCapabilitiesName>STANDARD_PED</NavCapabilitiesName><PerceptionInfo>DEFAULT_PERCEPTION</PerceptionInfo>" +
        "<DefaultBrawlingStyle>BS_ANIMAL</DefaultBrawlingStyle><DefaultUnarmedWeapon>WEAPON_ANIMAL</DefaultUnarmedWeapon>" +
        "<Personality>DOG</Personality><CombatInfo>SCARED_ANIMAL</CombatInfo><VfxInfoName>VFXPEDINFO_DOG_CAT_GENERIC</VfxInfoName>" +
        "<AmbientClipsForFlee>FLEE</AmbientClipsForFlee><Radio1>RADIO_GENRE_PUNK</Radio1><Radio2>RADIO_GENRE_JAZZ</Radio2>" +
        "<PedVoiceGroup>BAR_PERSON_PVG</PedVoiceGroup><AbilityType>SAT_NONE</AbilityType><ThermalBehaviour>TB_WARM</ThermalBehaviour>" +
        "<SuperlodType>SLOD_KEEP_LOWEST</SuperlodType><ScenarioPopStreamingSlot>SCENARIO_POP_STREAMING_NORMAL</ScenarioPopStreamingSlot>" +
        "<DefaultSpawningPreference>DSP_NORMAL</DefaultSpawningPreference>" +
        "<DefaultRemoveRangeMultiplier value=\"1.000000\" /><AllowCloseSpawning value=\"false\" /></Item>";
}
