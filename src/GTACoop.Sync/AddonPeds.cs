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

    /// <summary>
    /// Pedin kendi meta verisi yoksa kullanılacak temel model
    /// (ör. "a_c_rottweiler"). Boşsa tür şablonu kullanılır.
    /// </summary>
    public string BaseModel { get; set; } = "";

    /// <summary>
    /// "auto" (varsayılan), "on" veya "off". auto: insanlarda oyunun varsayılanı
    /// korunur, hayvanlarda ragdoll yalnızca meta verisi gerçek bir hayvan
    /// tanımından kopyalandıysa açılır; şablonla üretilen hayvanda çökme
    /// koruması olarak kapalı kalır.
    /// </summary>
    public string Ragdoll { get; set; } = "auto";

    /// <summary>Teşhis: meta verisinin okunduğu kaynak (dosya, RPF veya şablon).</summary>
    public string MetaSource { get; set; } = "";

    /// <summary>
    /// Meta verisi gerçek bir hayvan tanımından mı kopyalandı? Şablonla üretilen
    /// hayvanlarda ragdoll verisi doğrulanamadığı için istemci ragdoll'u kapatır.
    /// </summary>
    public bool MetaVerified { get; set; }

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
    public string BaseModel { get; set; } = "";
    public string Ragdoll { get; set; } = "auto";

    /// <summary>
    /// Meta verisi gerçek bir hayvan tanımından kopyalandı mı? Hayır ise
    /// istemci ragdoll'u kapatır (çökme koruması).
    /// </summary>
    public bool MetaVerified { get; set; }
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
    private const int PrepareFormat = 5;
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
        public string MetaSource { get; init; } = "";
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
        var metadata = LoadMetadata(packDir, models, log);
        var result = new List<LoosePed>();
        foreach (var group in groups.Values)
        {
            group.Files.Sort(StringComparer.OrdinalIgnoreCase);
            metadata.Items.TryGetValue(group.Model, out var item);
            result.Add(new LoosePed
            {
                Model = group.Model,
                Name = PrettifyName(group.Model),
                Kind = GuessKind(group.Model, item),
                Streamed = group.Streamed,
                HasModelFile = group.HasModelFile,
                MetaSource = metadata.Sources.TryGetValue(group.Model, out var source)
                    ? source
                    : "",
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

            existing ??= new AddonPedInfo { Enabled = true, Kind = loose.Kind };
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
            existing.BaseModel ??= "";
            existing.Ragdoll = NormalizeRagdoll(existing.Ragdoll);
            if (string.IsNullOrWhiteSpace(existing.MetaSource))
                existing.MetaSource = loose.MetaSource;
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
        var sourceKind = MetadataKind(metadata);
        if (sourceKind != HumanKind) return sourceKind;

        return model.StartsWith("a_c_", StringComparison.OrdinalIgnoreCase) ||
               model.StartsWith("a_c", StringComparison.OrdinalIgnoreCase) &&
               model.Length > 3 && !char.IsLetter(model[3])
            ? AnimalKind
            : HumanKind;
    }

    /// <summary>
    /// Yalnızca meta verideki Pedtype alanına bakar. Model adı öneki burada
    /// kullanılmaz; kaynak meta ile hedef türün uyuşup uyuşmadığını anlamak
    /// için güvenilir tek sinyal Pedtype'tır.
    /// </summary>
    private static string MetadataKind(XElement? metadata)
    {
        var pedType = metadata?.Element("Pedtype")?.Value;
        if (!string.IsNullOrWhiteSpace(pedType))
        {
            if (pedType.Contains("ANIMAL", StringComparison.OrdinalIgnoreCase))
                return AnimalKind;
            if (pedType.Contains("FEMALE", StringComparison.OrdinalIgnoreCase))
                return FemaleKind;
        }
        return HumanKind;
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

        var items = LoadMetadata(packDir, enabled.Select(p => p.Model), log);
        var build = BuildMetadata(enabled, items, log);
        var metadataXml = build.Xml;
        ValidatePack(packDir, enabled, log);
        ValidateGeneratedMetadata(metadataXml, enabled);
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

                var metaEntry = check.Find(EmbeddedMetadataName)!;
                var generated = Encoding.UTF8.GetString(check.ReadFile(metaEntry));
                ValidateGeneratedMetadata(generated, enabled);
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

    /// <summary>
    /// Paket dosyalarini ve manifest tutarliligini denetler. Kritik hatalar
    /// istemciye bozuk DLC gitmesini engellemek icin istisna firlatir;
    /// supheli durumlar yalnizca loglanir.
    /// </summary>
    public static void ValidatePack(string packDir, IReadOnlyCollection<AddonPedInfo> peds,
        Action<string>? log = null)
    {
        var errors = new List<string>();
        var warnings = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var ped in peds)
        {
            var model = (ped.Model ?? "").Trim();
            if (model.Length == 0)
            {
                errors.Add("Model adi bos olan ped var.");
                continue;
            }
            if (!seen.Add(model))
                errors.Add($"'{model}' birden fazla kez tanimli.");
            if (!model.Equals(model.ToLowerInvariant(), StringComparison.Ordinal) ||
                model.Any(c => char.IsWhiteSpace(c)))
                warnings.Add($"'{model}' model adi kucuk harf ve bosluksuz olmali.");

            var files = (ped.Files ?? new List<string>())
                .Where(f => !string.IsNullOrWhiteSpace(f))
                .ToList();
            if (files.Count == 0)
            {
                errors.Add($"'{model}' icin dosya listesi bos.");
                continue;
            }

            var hasModelFile = false;
            var matchesModelName = false;
            foreach (var rel in files)
            {
                var full = Path.Combine(packDir, rel.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(full))
                {
                    errors.Add($"'{model}' dosyasi bulunamadi: {rel}");
                    continue;
                }

                var ext = Path.GetExtension(full);
                if (!PedExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
                {
                    warnings.Add($"'{model}' ped olmayan dosya iceriyor: {rel}");
                    continue;
                }

                var header = new byte[16];
                using (var stream = File.OpenRead(full))
                {
                    var read = stream.Read(header, 0, header.Length);
                    if (read < header.Length || !RpfCrypto.HasResourceHeader(header))
                        errors.Add($"'{rel}' gecerli bir GTA kaynak dosyasi degil (RSC7 yok).");
                }

                var name = Path.GetFileNameWithoutExtension(full);
                var caret = name.IndexOf('^');
                if (caret > 0) name = name[..caret];
                if (name.Equals(model, StringComparison.OrdinalIgnoreCase))
                    matchesModelName = true;

                if (ped.Streamed)
                {
                    if (ext.Equals(".ydd", StringComparison.OrdinalIgnoreCase) ||
                        ext.Equals(".ytd", StringComparison.OrdinalIgnoreCase))
                        hasModelFile = true;
                }
                else if (ext.Equals(".yft", StringComparison.OrdinalIgnoreCase) ||
                         ext.Equals(".ymt", StringComparison.OrdinalIgnoreCase))
                {
                    hasModelFile = true;
                }
            }

            if (!hasModelFile)
                errors.Add(ped.Streamed
                    ? $"'{model}' streamed ped ama .ydd/.ytd bileseni yok."
                    : $"'{model}' icin .yft/.ymt model dosyasi yok.");
            if (!matchesModelName)
                warnings.Add($"'{model}' adiyla eslesen dosya yok; ad degistirilmis olabilir.");
        }

        foreach (var warning in warnings)
            log?.Invoke("Ped uyarisi: " + warning);
        if (errors.Count > 0)
            throw new InvalidDataException("Ped paketi dogrulanamadi:\r\n - " +
                                           string.Join("\r\n - ", errors));
        log?.Invoke($"Ped paketi dogrulandi: {peds.Count} ped.");
    }

    /// <summary>
    /// Uretilen meta XML'inde her etkin ped icin bir Item, gecerli bir
    /// Pedtype ve IsStreamedGfx alani bulundugunu denetler.
    /// </summary>
    public static void ValidateGeneratedMetadata(string metadataXml,
        IReadOnlyCollection<AddonPedInfo> peds)
    {
        XDocument doc;
        try
        {
            doc = XDocument.Parse(metadataXml);
        }
        catch (Exception ex)
        {
            throw new InvalidDataException("Uretilen ped meta XML'i okunamadi: " + ex.Message);
        }

        var items = new Dictionary<string, XElement>(StringComparer.OrdinalIgnoreCase);
        var errors = new List<string>();
        foreach (var candidate in doc.Descendants("Item"))
        {
            var name = candidate.Element("Name")?.Value?.Trim();
            if (string.IsNullOrWhiteSpace(name)) continue;
            if (!items.TryAdd(name, candidate))
                errors.Add($"'{name}' meta veride birden fazla kez tanimli.");
        }

        foreach (var ped in peds)
        {
            if (!items.TryGetValue(ped.Model, out var item))
            {
                errors.Add($"'{ped.Model}' icin meta Item eksik.");
                continue;
            }

            var pedType = item.Element("Pedtype")?.Value?.Trim() ?? "";
            if (pedType.Length == 0)
                errors.Add($"'{ped.Model}' icin Pedtype eksik.");
            var kind = EffectiveKind(ped);
            if (kind == AnimalKind &&
                !pedType.Contains("ANIMAL", StringComparison.OrdinalIgnoreCase))
                errors.Add($"'{ped.Model}' hayvan ama Pedtype '{pedType}'.");
            if (kind == FemaleKind &&
                !pedType.Contains("FEMALE", StringComparison.OrdinalIgnoreCase))
                errors.Add($"'{ped.Model}' kadin ama Pedtype '{pedType}'.");

            var streamed = item.Element("IsStreamedGfx");
            if (streamed == null)
                errors.Add($"'{ped.Model}' icin IsStreamedGfx eksik.");
            else if (!bool.TryParse(streamed.Attribute("value")?.Value, out var flag) ||
                     flag != ped.Streamed)
                errors.Add($"'{ped.Model}' IsStreamedGfx degeri yanlis.");

            // Şablonla üretilen hayvan meta'sinda ragdoll için gereken alanların
            // hepsi dolu olmalı. Eksik alan oyunu ragdoll sirasinda cokertiyor.
            if (kind == AnimalKind && !ped.MetaVerified)
            {
                foreach (var field in AnimalTemplateFields)
                {
                    var value = item.Element(field)?.Value?.Trim() ?? "";
                    if (value.Length == 0 ||
                        value.Equals("null", StringComparison.OrdinalIgnoreCase))
                        errors.Add($"'{ped.Model}' hayvan meta alani eksik: {field}.");
                }

                var nav = item.Element("NavCapabilitiesName")?.Value?.Trim() ?? "";
                if (nav.Equals("STANDARD_PED", StringComparison.OrdinalIgnoreCase))
                    errors.Add($"'{ped.Model}' hayvan ama NavCapabilitiesName STANDARD_PED.");

                var capsule = item.Element("PedCapsuleName")?.Value?.Trim() ?? "";
                if (capsule.StartsWith("STANDARD_", StringComparison.OrdinalIgnoreCase))
                    errors.Add($"'{ped.Model}' hayvan ama PedCapsuleName insan kapsulu ({capsule}).");
            }
        }

        if (errors.Count > 0)
            throw new InvalidDataException("Uretilen ped meta verisi gecersiz:\r\n - " +
                                           string.Join("\r\n - ", errors));
    }

    private static string ComputeSourceHash(string packDir, IEnumerable<AddonPedInfo> peds,
        string metadataXml)
    {
        var sb = new StringBuilder();
        foreach (var ped in peds.OrderBy(p => p.Model, StringComparer.OrdinalIgnoreCase))
        {
            sb.Append("P:").Append(ped.Model).Append('|').Append(EffectiveKind(ped)).Append('|')
              .Append(ped.Streamed ? 1 : 0).Append('|')
              .Append(ped.BaseModel ?? "").Append('|')
              .Append(NormalizeRagdoll(ped.Ragdoll)).Append(';');
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

    /// <summary>
    /// Pakette bulunan tüm ped meta verileri. <see cref="Items"/> yalnızca
    /// istenen modelleri, <see cref="AllItems"/> ise BaseModel çözümlemesi için
    /// tüm Item'ları tutar.
    /// </summary>
    public sealed class PedMetadata
    {
        public Dictionary<string, XElement> Items { get; } =
            new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> Sources { get; } =
            new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, XElement> AllItems { get; } =
            new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> AllSources { get; } =
            new(StringComparer.OrdinalIgnoreCase);
        public List<string> Files { get; } = new();
    }

    private static readonly string[] MetadataFileNames =
    {
        "peds.meta", "pedmeta.xml", "ap_m.xml", "peds.xml",
        "pedmodelinfo.meta", "pedalternatevariations.meta"
    };

    private static bool IsMetadataFile(string path)
    {
        var name = Path.GetFileName(path);
        if (MetadataFileNames.Contains(name, StringComparer.OrdinalIgnoreCase)) return true;
        return name.EndsWith(".meta", StringComparison.OrdinalIgnoreCase) &&
               name.Contains("ped", StringComparison.OrdinalIgnoreCase);
    }

    private static PedMetadata LoadMetadata(string packDir, IEnumerable<string> models,
        Action<string>? log = null)
    {
        var wanted = new HashSet<string>(models.Where(m => !string.IsNullOrWhiteSpace(m)),
            StringComparer.OrdinalIgnoreCase);
        var result = new PedMetadata();

        // 1) Paket içindeki gevşek meta dosyaları (alt klasörler/streamed dahil).
        try
        {
            foreach (var file in Directory.EnumerateFiles(packDir, "*", SearchOption.AllDirectories))
            {
                if (!IsMetadataFile(file)) continue;
                AddMetadataFile(result, file, Path.GetRelativePath(packDir, file)
                    .Replace('\\', '/'), wanted, log);
            }
        }
        catch (Exception ex)
        {
            log?.Invoke("Meta dosyaları taranamadı: " + ex.Message);
        }

        // 2) Paketteki RPF arşivlerindeki gömülü meta (dlc.rpf/peds.rpf).
        try
        {
            foreach (var rpf in Directory.EnumerateFiles(packDir, "*.rpf",
                         SearchOption.AllDirectories).Take(12))
                TryReadMetadataFromRpf(result, rpf, wanted, log);
        }
        catch (Exception ex)
        {
            log?.Invoke("RPF meta taraması atlandı: " + ex.Message);
        }

        // 3) Sunucuda kurulu AddonPeds ap_m.xml (yalnızca okunur).
        try
        {
            var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var apM = Path.Combine(docs, "Rockstar Games", "GTA V", "UserMods", "ap_m.xml");
            if (File.Exists(apM)) AddMetadataFile(result, apM, "ap_m.xml", wanted, log);
        }
        catch { }

        return result;
    }

    private static void AddMetadataFile(PedMetadata result, string path, string source,
        HashSet<string> wanted, Action<string>? log)
    {
        if (!result.Files.Contains(source, StringComparer.OrdinalIgnoreCase))
            result.Files.Add(source);
        try
        {
            using var stream = File.OpenRead(path);
            AddMetadataDocument(result, XDocument.Load(stream), source, wanted);
        }
        catch (Exception ex)
        {
            log?.Invoke($"Meta okunamadı ({source}): {ex.Message}");
        }
    }

    private static void AddMetadataDocument(PedMetadata result, XDocument doc, string source,
        HashSet<string> wanted)
    {
        foreach (var item in doc.Descendants("Item"))
        {
            var name = item.Element("Name")?.Value?.Trim();
            if (string.IsNullOrWhiteSpace(name)) continue;
            if (!result.AllItems.ContainsKey(name))
            {
                result.AllItems[name] = new XElement(item);
                result.AllSources[name] = source;
            }
            if (!wanted.Contains(name) || result.Items.ContainsKey(name)) continue;
            result.Items[name] = new XElement(item);
            result.Sources[name] = source;
        }
    }

    private static void TryReadMetadataFromRpf(PedMetadata result, string path,
        HashSet<string> wanted, Action<string>? log)
    {
        try
        {
            using var rpf = RpfArchive.OpenReadOnly(path);
            foreach (var file in rpf.AllFiles())
            {
                var name = Path.GetFileName(file.Path);
                if (!IsMetadataFile(name) &&
                    !name.Equals(EmbeddedMetadataName, StringComparison.OrdinalIgnoreCase))
                    continue;

                var bytes = rpf.ReadFile(file);
                using var stream = new MemoryStream(bytes);
                AddMetadataDocument(result, XDocument.Load(stream),
                    "rpf:" + Path.GetFileName(path) + ":" + file.Path, wanted);
            }
        }
        catch (NotSupportedException)
        {
            // NG/opaque arşiv: diğer meta kaynakları kullanılmaya devam eder.
        }
        catch (Exception ex)
        {
            log?.Invoke($"RPF meta okunamadı ({Path.GetFileName(path)}): {ex.Message}");
        }
    }

    /// <summary>Üretilen meta XML'i ve ped başına kaynak/uyarı bilgisi.</summary>
    public sealed class MetadataBuild
    {
        public string Xml { get; set; } = "";
        public Dictionary<string, string> Sources { get; } =
            new(StringComparer.OrdinalIgnoreCase);
        public List<string> Warnings { get; } = new();
    }

    private static PedMetadata ToMetadata(IReadOnlyDictionary<string, XElement>? known)
    {
        var result = new PedMetadata();
        if (known == null) return result;
        foreach (var pair in known)
        {
            result.Items[pair.Key] = pair.Value;
            result.AllItems[pair.Key] = pair.Value;
            result.Sources[pair.Key] = "known";
            result.AllSources[pair.Key] = "known";
        }
        return result;
    }

    public static string BuildMetadataXml(IEnumerable<AddonPedInfo> peds,
        IReadOnlyDictionary<string, XElement>? known = null) =>
        BuildMetadata(peds, ToMetadata(known)).Xml;

    public static MetadataBuild BuildMetadata(IEnumerable<AddonPedInfo> peds,
        PedMetadata? metadata, Action<string>? log = null)
    {
        metadata ??= new PedMetadata();
        var root = new XElement("CPedModelInfo__InitDataList", new XElement("InitDatas"));
        var datas = root.Element("InitDatas")!;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new MetadataBuild();

        foreach (var ped in peds)
        {
            if (string.IsNullOrWhiteSpace(ped.Model) || !seen.Add(ped.Model)) continue;
            var item = BuildMetadataItem(ped, metadata, out var source, out var warning);
            ped.MetaSource = source;
            // Şablonla üretilen meta doğrulanmamıştır: gerçek bir vanilla hayvan
            // tanımından kopyalanmadığı için ragdoll verisi garanti değildir.
            ped.MetaVerified = !source.StartsWith("template:", StringComparison.Ordinal);
            result.Sources[ped.Model] = source;
            if (!string.IsNullOrWhiteSpace(warning))
            {
                result.Warnings.Add(ped.Model + ": " + warning);
                log?.Invoke("Meta uyarısı: " + ped.Model + ": " + warning);
            }
            datas.Add(item);
        }

        using var ms = new MemoryStream();
        var doc = new XDocument(new XDeclaration("1.0", "utf-8", null), root);
        doc.Save(ms, SaveOptions.DisableFormatting);
        result.Xml = Encoding.UTF8.GetString(StripUtf8Bom(ms.ToArray()));
        return result;
    }

    public static string NormalizeRagdoll(string? value)
    {
        var text = (value ?? "").Trim().ToLowerInvariant();
        return text is "on" or "off" ? text : "auto";
    }

    /// <summary>
    /// Sunucu manifestosu Kind alanini bos veya "human" biraksa bile a_c_
    /// onekli modeller hayvandir. Yanlis CIVMALE meta verisi pedi gorunmez
    /// birakiyor ve ragdoll sirasinda oyunu cokertiyor.
    /// </summary>
    public static string EffectiveKind(AddonPedInfo ped) => EffectiveKind(ped, null);

    public static string EffectiveKind(AddonPedInfo ped, XElement? metadata)
    {
        // a_c_ öneki güvenlik sinyalidir: meta yanlış olsa bile hayvan kalır.
        if (!string.IsNullOrWhiteSpace(ped.Model) &&
            ped.Model.StartsWith("a_c_", StringComparison.OrdinalIgnoreCase))
            return AnimalKind;

        var kind = (ped.Kind ?? string.Empty).Trim();
        if (kind.Equals(AnimalKind, StringComparison.OrdinalIgnoreCase))
            return AnimalKind;
        if (kind.Equals(FemaleKind, StringComparison.OrdinalIgnoreCase))
            return FemaleKind;

        if (metadata != null)
        {
            var sourceKind = GuessKind(ped.Model, metadata);
            if (sourceKind == AnimalKind) return AnimalKind;
            if (sourceKind == FemaleKind) return FemaleKind;
        }
        return HumanKind;
    }

    public static XElement BuildMetadataItem(AddonPedInfo ped,
        IReadOnlyDictionary<string, XElement>? known = null) =>
        BuildMetadataItem(ped, ToMetadata(known), out _, out _);

    public static XElement BuildMetadataItem(AddonPedInfo ped, PedMetadata? metadata,
        out string source, out string? warning)
    {
        metadata ??= new PedMetadata();
        source = "";
        warning = null;

        XElement? sourceItem = null;
        if (!string.IsNullOrWhiteSpace(ped.Model) &&
            metadata.Items.TryGetValue(ped.Model, out var own))
        {
            sourceItem = own;
            source = metadata.Sources.TryGetValue(ped.Model, out var ownSource)
                ? ownSource : "pack";
        }
        else if (!string.IsNullOrWhiteSpace(ped.BaseModel) &&
                 metadata.AllItems.TryGetValue(ped.BaseModel.Trim(), out var baseItem))
        {
            sourceItem = baseItem;
            var baseSource = metadata.AllSources.TryGetValue(ped.BaseModel.Trim(), out var bs)
                ? bs : "?";
            source = "base:" + ped.BaseModel.Trim() + " (" + baseSource + ")";
        }

        var kind = EffectiveKind(ped, sourceItem);
        XElement item;
        if (sourceItem != null)
        {
            var sourceKind = MetadataKind(sourceItem);
            if ((kind == AnimalKind) == (sourceKind == AnimalKind))
            {
                item = new XElement(sourceItem);
            }
            else
            {
                item = XElement.Parse(TemplateFor(kind));
                source = "template:" + kind + " (meta türü uyuşmadı)";
                warning = "kaynak meta türü (" + sourceKind + "), ped türü (" + kind +
                          ") ile uyuşmuyor; şablon kullanıldı.";
            }
        }
        else
        {
            item = XElement.Parse(TemplateFor(kind));
            source = "template:" + kind;
            var templateName = kind == AnimalKind
                ? "vanilla hayvan şablonu (retriever)"
                : "insan şablonu";
            warning = string.IsNullOrWhiteSpace(ped.BaseModel)
                ? "pakette ped meta verisi yok; " + templateName + " kullanıldı."
                : "BaseModel '" + ped.BaseModel + "' meta içinde bulunamadı; " +
                  templateName + " kullanıldı.";
        }

        item.SetElementValue("Name", ped.Model);

        // Kaynak meta türü doğruysa mümkün olduğunca korunur. Şablon
        // kullanıldığında veya tür uyuşmadığında tür alanları garanti edilir.
        var sourceIsFemale = sourceItem != null &&
                             MetadataKind(sourceItem) == FemaleKind;
        if (kind == FemaleKind)
        {
            if (!sourceIsFemale)
            {
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
            item.SetElementValue("Pedtype", "CIVFEMALE");
        }
        else if (kind == AnimalKind)
        {
            // a_c_ önekli modellerde ped türü her koşulda ANIMAL olmalı;
            // aksi hâlde oyun hayvan iskeletinde insan kodu çalıştırır.
            item.SetElementValue("Pedtype", "ANIMAL");

            // Hareket seti ped meta'sına yazılır: oyun pedi bu clipset ile
            // yürütür, oyun içi menüden seçmeye gerek kalmaz. Şablondan gelen
            // pedlerde ırk model adından çıkarılır; pakette hazır meta varsa
            // yalnızca boş / "null" alanlar doldurulur.
            var breed = DefaultAnimalBreed(ped.Model);
            var fromTemplate = source != null &&
                source.StartsWith("template", StringComparison.OrdinalIgnoreCase);
            var dictValue = item.Element("ClipDictionaryName")?.Value?.Trim();
            var moveValue = item.Element("MovementClipSet")?.Value?.Trim();

            if (fromTemplate || string.IsNullOrEmpty(dictValue) ||
                dictValue.Equals("null", StringComparison.OrdinalIgnoreCase))
                item.SetElementValue("ClipDictionaryName", "creatures@" + breed + "@move");

            if (fromTemplate || string.IsNullOrEmpty(moveValue) ||
                moveValue.Equals("null", StringComparison.OrdinalIgnoreCase))
                item.SetElementValue("MovementClipSet", breed + "@move");
        }
        else if (sourceItem == null)
        {
            item.SetElementValue("Pedtype", "CIVMALE");
        }

        if (!string.IsNullOrWhiteSpace(ped.ClipDictionary))
            item.SetElementValue("ClipDictionaryName", ped.ClipDictionary.Trim());
        if (!string.IsNullOrWhiteSpace(ped.MovementClipSet))
        {
            var clipset = ped.MovementClipSet.Trim();
            item.SetElementValue("MovementClipSet", clipset);

            // Sözlük ayrıca verilmediyse clipset'ten türet. Sözlüksüz kalan
            // clipset oyunda yüklenmez ve hayvan yerinde durur.
            if (kind == AnimalKind && string.IsNullOrWhiteSpace(ped.ClipDictionary))
            {
                var at = clipset.LastIndexOf('@');
                var breedPart = at > 0 ? clipset.Substring(0, at) : clipset;
                item.SetElementValue("ClipDictionaryName",
                    ("creatures@" + breedPart + "@move").ToLowerInvariant());
            }
        }
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

    // Varsayılan hareket seti seçilebilen hayvan ırkları. Köpek ırkları aynı
    // iskeleti paylaşır; her ırkın kendi clipset'i vardır
    // (shepherd -> SHEPHERD@MOVE / creatures@shepherd@move).
    private static readonly string[] AnimalBreeds =
    {
        "rottweiler", "shepherd", "husky", "retriever", "pug", "westy", "poodle", "cat"
    };

    /// <summary>
    /// Hayvan modelinin hareket ırkını döner. Tanınmayan addon hayvanlarında
    /// rottweiler (Chop) seti kullanılır.
    /// </summary>
    private static string DefaultAnimalBreed(string model)
    {
        var name = (model ?? string.Empty).ToLowerInvariant();
        if (name.Contains("chop")) return "rottweiler";
        foreach (var breed in AnimalBreeds)
            if (name.Contains(breed)) return breed;
        return "rottweiler";
    }

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
                    Kind = EffectiveKind(ped),
                    Pack = string.IsNullOrWhiteSpace(addon.DisplayName) ? addon.Id : addon.DisplayName,
                    Health = ped.Health <= 0 ? 200 : ped.Health,
                    BaseModel = ped.BaseModel ?? "",
                    Ragdoll = NormalizeRagdoll(ped.Ragdoll),
                    MetaVerified = ped.MetaVerified
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

    /// <summary>
    /// Şablonla üretilen hayvan meta'sında ragdoll için zorunlu alanlar.
    /// Bunlardan biri boş ya da "null" olursa oyun ragdoll sırasında çöküyor.
    /// </summary>
    private static readonly string[] AnimalTemplateFields =
    {
        "ClipDictionaryName", "MovementClipSet", "GetupSetHash", "PoseMatcherName",
        "PoseMatcherProneName", "DecisionMakerName", "MotionTaskDataSetName",
        "DefaultTaskDataSetName", "PedCapsuleName", "TaskDataName",
        "NavCapabilitiesName", "DefaultBrawlingStyle", "DefaultUnarmedWeapon",
        "Personality", "CombatInfo", "AnimalAudioObject"
    };

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

    // Oyunun dört ayaklı hayvan tanımının kopyası. Elle uydurulan kısa şablon
    // ragdoll için gereken alanları (PoseMatcherProneName, TaskDataName,
    // AnimalAudioObject, MinActivationImpulse, NavCapabilitiesName ...) eksik
    // bıraktığı için hayvan düşünce/araca çarpınca oyun çöküyordu; o alanlar
    // vanilla retriever pedinden aynen alınır.
    //
    // Hareket alanları rottweiler (Chop): ClipDictionaryName
    // creatures@rottweiler@move + MovementClipSet rottweiler@move. Retriever
    // setiyle eklenen köpek yerinde duruyordu ve oyun içi menüden clipset
    // değiştirmek gerekiyordu; artık hareket seti ped meta'sıyla birlikte
    // geliyor ve köpek eklendiği gibi yürüyor.
    private const string AnimalTemplate =
        "<Item><Name>template</Name><PropsName>null</PropsName>" +
        "<ClipDictionaryName>creatures@rottweiler@move</ClipDictionaryName><BlendShapeFileName>null</BlendShapeFileName>" +
        "<ExpressionSetName /><ExpressionDictionaryName>a_c_retriever</ExpressionDictionaryName>" +
        "<ExpressionName>a_c_retriever</ExpressionName><Pedtype>ANIMAL</Pedtype><MovementClipSet>rottweiler@move</MovementClipSet>" +
        "<StrafeClipSet>MOVE_PED_STRAFING</StrafeClipSet><MovementToStrafeClipSet>MOVE_PED_TO_STRAFE</MovementToStrafeClipSet>" +
        "<InjuredStrafeClipSet>move_strafe_injured</InjuredStrafeClipSet><FullBodyDamageClipSet>dam_ko</FullBodyDamageClipSet>" +
        "<AdditiveDamageClipSet>dam_ad</AdditiveDamageClipSet><DefaultGestureClipSet>ANIM_GROUP_GESTURE_M_GENERIC</DefaultGestureClipSet>" +
        "<FacialClipsetGroupName>facial_clipset_group_gen_male</FacialClipsetGroupName>" +
        "<DefaultVisemeClipSet>ANIM_GROUP_VISEMES_M_LO</DefaultVisemeClipSet><SidestepClipSet>CREATURES@RETRIEVER@STEP</SidestepClipSet>" +
        "<PoseMatcherName>retriever</PoseMatcherName><PoseMatcherProneName>Male_prone</PoseMatcherProneName>" +
        "<GetupSetHash>NMBS_RETRIEVER_GETUPS</GetupSetHash><CreatureMetadataName>null</CreatureMetadataName>" +
        "<DecisionMakerName>DOG</DecisionMakerName><MotionTaskDataSetName>retriever</MotionTaskDataSetName>" +
        "<DefaultTaskDataSetName>Animal</DefaultTaskDataSetName><PedCapsuleName>retriever</PedCapsuleName>" +
        "<PedLayoutName /><PedComponentSetName /><PedComponentClothName /><PedIKSettingsName />" +
        "<TaskDataName>DOG</TaskDataName><IsStreamedGfx value=\"true\" />" +
        "<AmbulanceShouldRespondTo value=\"false\" /><CanRideBikeWithNoHelmet value=\"false\" />" +
        "<CanSpawnInCar value=\"true\" /><IsHeadBlendPed value=\"false\" />" +
        "<bOnlyBulkyItemVariations value=\"false\" /><RelationshipGroup>GUARD_DOG</RelationshipGroup>" +
        "<NavCapabilitiesName>STANDARD_QUADRUPED</NavCapabilitiesName><PerceptionInfo>DEFAULT_PERCEPTION</PerceptionInfo>" +
        "<DefaultBrawlingStyle>BS_DOG_RETRIEVER</DefaultBrawlingStyle>" +
        "<DefaultUnarmedWeapon>WEAPON_ANIMAL_RETRIEVER</DefaultUnarmedWeapon>" +
        "<Personality>retriever</Personality><CombatInfo>rottweiler</CombatInfo>" +
        "<VfxInfoName>VFXPEDINFO_DOG_CAT_GENERIC</VfxInfoName>" +
        "<AmbientClipsForFlee>FLEE</AmbientClipsForFlee><Radio1>RADIO_GENRE_PUNK</Radio1><Radio2>RADIO_GENRE_JAZZ</Radio2>" +
        "<FUpOffset value=\"0.000000\" /><RUpOffset value=\"0.000000\" />" +
        "<FFrontOffset value=\"0.000000\" /><RFrontOffset value=\"0.147000\" />" +
        "<MinActivationImpulse value=\"20.000000\" /><Stubble value=\"0.000000\" />" +
        "<HDDist value=\"3.000000\" /><TargetingThreatModifier value=\"1.000000\" />" +
        "<KilledPerceptionRangeModifer value=\"-1.000000\" /><Sexiness /><Age value=\"0\" />" +
        "<MaxPassengersInCar value=\"0\" /><ExternallyDrivenDOFs />" +
        "<PedVoiceGroup>MALE_BRAVE_PVG</PedVoiceGroup>" +
        "<AnimalAudioObject>ANIMAL_PARAMS_SHEPHERD</AnimalAudioObject>" +
        "<AbilityType>SAT_NONE</AbilityType><ThermalBehaviour>TB_WARM</ThermalBehaviour>" +
        "<SuperlodType>SLOD_KEEP_LOWEST</SuperlodType><ScenarioPopStreamingSlot>SCENARIO_POP_STREAMING_NORMAL</ScenarioPopStreamingSlot>" +
        "<DefaultSpawningPreference>DSP_NORMAL</DefaultSpawningPreference>" +
        "<DefaultRemoveRangeMultiplier value=\"1.000000\" /><AllowCloseSpawning value=\"false\" /></Item>";
}
