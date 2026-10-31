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

    /// <summary>"human" veya "animal".</summary>
    public string Kind { get; set; } = "human";

    /// <summary>Sunucu bu pedi dağıtıyor mu?</summary>
    public bool Enabled { get; set; } = true;
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
}

/// <summary>
/// AddonPeds uyumlu DLC paketlerini sunucu tarafında inceler ve kendi kendine
/// yeterli (self-contained) bir kopya üretir. Kullanıcının kendi AddonPeds
/// kurulumuna ve ap_m.xml dosyasına asla dokunulmaz.
/// </summary>
public static class AddonPeds
{
    public const string DirectoryName = "peds";
    public const string PackFileName = "peds.json";
    public const string PreparedDirectoryName = ".gtacoop-peds";
    public const string ClientConfigFileName = "GTACoop-addonpeds.json";
    private const string EmbeddedMetadataName = "gtacoop_peds.xml";
    private const int PrepareFormat = 2;
    private const string HumanKind = "human";
    private const string AnimalKind = "animal";

    // ---------------------------------------------------------------- paket dosyası

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

    /// <summary>dlc.rpf içindeki peds.rpf'ten ped model adlarını okur.</summary>
    public static List<string> DiscoverModels(string dlcRpf, Action<string>? log = null)
    {
        var models = new List<string>();
        string? temp = null;
        try
        {
            using var outer = RpfArchive.Open(dlcRpf);
            var entry = outer.Find("peds.rpf");
            if (entry != null)
            {
                temp = Path.Combine(Path.GetTempPath(), "gtacoop-peds-" + Guid.NewGuid().ToString("N") + ".rpf");
                File.WriteAllBytes(temp, outer.ReadFile(entry));
                using var inner = RpfArchive.Open(temp);

                foreach (var file in inner.Root.Files)
                {
                    if (!file.NameLower.EndsWith(".yft", StringComparison.OrdinalIgnoreCase)) continue;
                    var name = Path.GetFileNameWithoutExtension(file.NameLower);
                    if (name.Length > 0) models.Add(name);
                }

                // Bazı paketlerde modeller alt klasörde olabilir; en sığ .yft'leri kullan.
                if (models.Count == 0)
                {
                    var files = inner.AllEntries.OfType<RpfFileEntry>()
                        .Where(f => f.Path.EndsWith(".yft", StringComparison.OrdinalIgnoreCase))
                        .OrderBy(f => f.Path.Count(c => c == '\\'))
                        .ThenBy(f => f.Path, StringComparer.OrdinalIgnoreCase);
                    foreach (var file in files)
                    {
                        var name = Path.GetFileNameWithoutExtension(file.Path);
                        if (name.Length == 0 || models.Contains(name, StringComparer.OrdinalIgnoreCase))
                            continue;
                        models.Add(name);
                        // Alt klasör yapısı model adıyla eşleşmiyorsa yalnızca ilk grubu al.
                        if (models.Count >= 512) break;
                    }
                }
            }
            else
            {
                // Standart DLC ped paketi: doğrudan dlc.rpf içinde modeller olabilir.
                var files = outer.AllEntries.OfType<RpfFileEntry>()
                    .Where(f => f.Path.EndsWith(".yft", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(f => f.Path.Count(c => c == '\\'))
                    .ThenBy(f => f.Path, StringComparer.OrdinalIgnoreCase);
                foreach (var file in files)
                {
                    var name = Path.GetFileNameWithoutExtension(file.Path);
                    if (name.Length == 0 || models.Contains(name, StringComparer.OrdinalIgnoreCase)) continue;
                    models.Add(name);
                    if (models.Count >= 512) break;
                }
            }
        }
        catch (Exception ex)
        {
            log?.Invoke($"Ped modelleri okunamadı ({Path.GetFileName(dlcRpf)}): {ex.Message}");
        }
        finally
        {
            if (temp != null)
            {
                try { File.Delete(temp); } catch { }
            }
        }

        models.Sort(StringComparer.OrdinalIgnoreCase);
        return models;
    }

    /// <summary>peds.json dosyasını okur; yoksa modellerden oluşturur ve kaydeder.</summary>
    public static AddonPedPackFile LoadOrCreatePackFile(string packDir, IList<string> models)
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
            file.Description = SyncJson.Load<AddonMetaFile>(Path.Combine(packDir, "addon.json"))?.Description ?? "";

        if (models.Count > 0)
        {
            var metadata = LoadMetadata(packDir, models);
            var known = file.Peds
                .GroupBy(p => p.Model, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
            var discovered = new HashSet<string>(models, StringComparer.OrdinalIgnoreCase);

            // Artık pakette olmayan kayıtları listeden çıkar (yanlış menü girdisi bırakma).
            file.Peds.RemoveAll(p => string.IsNullOrWhiteSpace(p.Model) ||
                !discovered.Contains(p.Model));

            foreach (var model in models)
            {
                metadata.TryGetValue(model, out var metaItem);
                var metaKind = metaItem == null ? null : GuessKind(model, metaItem);

                if (known.TryGetValue(model, out var existing))
                {
                    if (string.IsNullOrWhiteSpace(existing.Name)) existing.Name = PrettifyName(model);
                    if (metaKind != null) existing.Kind = metaKind;
                    else if (existing.Kind is not (HumanKind or AnimalKind))
                        existing.Kind = GuessKind(model, null);
                    continue;
                }

                file.Peds.Add(new AddonPedInfo
                {
                    Model = model,
                    Name = PrettifyName(model),
                    Kind = metaKind ?? GuessKind(model, null),
                    Enabled = true
                });
            }
        }

        file.Peds = file.Peds
            .OrderBy(p => p.Kind == AnimalKind ? 1 : 0)
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
        if (!string.IsNullOrWhiteSpace(pedType) &&
            pedType.Contains("ANIMAL", StringComparison.OrdinalIgnoreCase))
            return AnimalKind;

        return model.StartsWith("a_c_", StringComparison.OrdinalIgnoreCase) ||
               model.StartsWith("a_c", StringComparison.OrdinalIgnoreCase) &&
               model.Length > 3 && !char.IsLetter(model[3])
            ? AnimalKind
            : HumanKind;
    }

    // ---------------------------------------------------------------- hazırlama

    private sealed class PrepareStamp
    {
        public int Format { get; set; }
        public long SourceLength { get; set; }
        public long SourceTicks { get; set; }
        public string MetadataHash { get; set; } = "";
    }

    /// <summary>
    /// Paketi kendi kendine yeterli hale getirir: cihaz adını benzersiz yapar,
    /// ped meta verisini dlc.rpf içine gömer. Dönen klasörde yalnızca dlc.rpf bulunur.
    /// Kaynak paket ve kullanıcının ap_m.xml dosyası değiştirilmez.
    /// </summary>
    public static string PrepareSelfContained(string packDir, string addonId,
        string preparedRoot, AddonPedPackFile packFile, Action<string>? log = null)
    {
        var source = FindDlcRpf(packDir)
            ?? throw new FileNotFoundException($"'{packDir}' içinde dlc.rpf bulunamadı.");
        Directory.CreateDirectory(preparedRoot);
        var target = Path.Combine(preparedRoot, "dlc.rpf");
        var stampPath = Path.Combine(preparedRoot, ".prepare.json");
        var sourceInfo = new FileInfo(source);

        var items = LoadMetadata(packDir, packFile.Peds.Select(p => p.Model));
        var metadataXml = BuildMetadataXml(packFile.Peds, items);
        var metadataHash = HashMetadata(packFile.Peds, metadataXml);

        var stamp = SyncJson.Load<PrepareStamp>(stampPath);
        if (stamp != null && File.Exists(target) &&
            stamp.Format == PrepareFormat &&
            stamp.SourceLength == sourceInfo.Length &&
            stamp.SourceTicks == sourceInfo.LastWriteTimeUtc.Ticks &&
            string.Equals(stamp.MetadataHash, metadataHash, StringComparison.Ordinal))
        {
            return target;
        }

        var temp = target + ".new";
        var old = target + ".old";
        try
        {
            if (File.Exists(temp)) File.Delete(temp);
            File.Copy(source, temp, overwrite: true);

            var mountFolder = AddonApplier.MountFolderName(addonId);
            var device = "o_" + mountFolder;
            using (var archive = RpfArchive.Open(temp))
            {
                RewriteSetup(archive, device, mountFolder);
                RewriteAppdata(archive, device);
                archive.SetFile(EmbeddedMetadataName, Encoding.UTF8.GetBytes(metadataXml));
            }

            // Yapısal doğrulama: yeni girdi ve XML gerçekten okunabiliyor mu?
            using (var check = RpfArchive.Open(temp))
            {
                _ = check.Find(EmbeddedMetadataName)
                    ?? throw new InvalidDataException("Gömülü ped meta verisi yazılamadı.");
                _ = check.Find("appdata.xml")
                    ?? throw new InvalidDataException("appdata.xml bulunamadı.");
            }

            if (File.Exists(old)) File.Delete(old);
            if (File.Exists(target)) File.Move(target, old);
            File.Move(temp, target);
            try { File.Delete(old); } catch { }

            SyncJson.Save(stampPath, new PrepareStamp
            {
                Format = PrepareFormat,
                SourceLength = sourceInfo.Length,
                SourceTicks = sourceInfo.LastWriteTimeUtc.Ticks,
                MetadataHash = metadataHash
            });
            log?.Invoke($"Ped paketi hazırlandı: {addonId} ({packFile.Peds.Count(p => p.Enabled)} etkin ped)");
            return target;
        }
        catch
        {
            if (File.Exists(temp))
            {
                try { File.Delete(temp); } catch { }
            }
            if (File.Exists(old) && !File.Exists(target))
            {
                try { File.Move(old, target); } catch { }
            }
            throw;
        }
    }

    private static void RewriteSetup(RpfArchive archive, string device, string nameHash)
    {
        var entry = archive.Find("setup2.xml")
            ?? throw new InvalidDataException("setup2.xml bulunamadı (geçerli bir DLC paketi değil).");
        var doc = XDocument.Parse(DecodeXml(archive.ReadFile(entry)));
        var root = doc.Root ?? throw new InvalidDataException("setup2.xml kökü yok.");

        var deviceEl = root.Element("deviceName");
        if (deviceEl == null) root.Add(new XElement("deviceName", device));
        else deviceEl.Value = device;

        var nameHashEl = root.Element("nameHash");
        if (nameHashEl != null) nameHashEl.Value = nameHash;

        // İki AddonPeds paketi aynı değişiklik kümesi adını kullanırsa çakışabilir.
        var newSet = "GTACOOP_" + nameHash.ToUpperInvariant() + "_AUTOGEN";
        foreach (var set in root.Descendants("ContentChangeSets").Elements("Item"))
            set.Value = newSet;
        foreach (var set in root.Descendants("changeSetName"))
            set.Value = newSet;

        archive.SetFile("setup2.xml", SerializeXml(doc));
    }

    private static void RewriteAppdata(RpfArchive archive, string device)
    {
        var entry = archive.Find("appdata.xml")
            ?? throw new InvalidDataException("appdata.xml bulunamadı (geçerli bir DLC paketi değil).");
        var doc = XDocument.Parse(DecodeXml(archive.ReadFile(entry)));
        var root = doc.Root ?? throw new InvalidDataException("appdata.xml kökü yok.");

        var metadataPath = device + ":/" + EmbeddedMetadataName;
        var streamPath = device + ":/peds.rpf";

        foreach (var dataFile in root.Descendants("dataFiles").Elements("Item"))
        {
            var type = dataFile.Element("fileType")?.Value ?? "";
            var filenameEl = dataFile.Element("filename");
            if (filenameEl == null) continue;

            if (type.Equals("PED_METADATA_FILE", StringComparison.OrdinalIgnoreCase))
            {
                filenameEl.Value = metadataPath;
                continue;
            }

            var name = filenameEl.Value.Trim();
            if (type.Equals("PEDSTREAM_FILE", StringComparison.OrdinalIgnoreCase) ||
                name.EndsWith("/peds.rpf", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "peds.rpf", StringComparison.OrdinalIgnoreCase))
            {
                filenameEl.Value = streamPath;
                continue;
            }

            // Aynı cihazı kullanan diğer yolları da yeni benzersiz cihaza taşı.
            var colon = name.IndexOf(':');
            if (colon > 0 && name[..colon].StartsWith("o_", StringComparison.OrdinalIgnoreCase))
                filenameEl.Value = device + name[colon..];
        }

        foreach (var item in root.Descendants("filesToEnable").Elements("Item"))
        {
            var name = item.Value.Trim();
            if (name.EndsWith("ap_m.xml", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("UserMods", StringComparison.OrdinalIgnoreCase))
            {
                item.Value = metadataPath;
                continue;
            }

            var colon = name.IndexOf(':');
            if (colon > 0 && name[..colon].StartsWith("o_", StringComparison.OrdinalIgnoreCase))
                item.Value = device + name[colon..];
        }

        foreach (var set in root.Descendants("changeSetName"))
            set.Value = "GTACOOP_" + device[2..].ToUpperInvariant() + "_AUTOGEN";

        archive.SetFile("appdata.xml", SerializeXml(doc));
    }

    private static byte[] SerializeXml(XDocument doc)
    {
        using var ms = new MemoryStream();
        doc.Save(ms, SaveOptions.DisableFormatting);
        return ms.ToArray();
    }

    private static string DecodeXml(byte[] bytes) =>
        Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF');

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

    private static string BuildMetadataXml(IEnumerable<AddonPedInfo> peds,
        IReadOnlyDictionary<string, XElement> known)
    {
        var root = new XElement("CPedModelInfo__InitDataList",
            new XElement("InitDatas"));
        var datas = root.Element("InitDatas")!;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var ped in peds)
        {
            if (string.IsNullOrWhiteSpace(ped.Model) || !seen.Add(ped.Model)) continue;
            if (known.TryGetValue(ped.Model, out var item))
            {
                var clone = new XElement(item);
                clone.SetElementValue("Name", ped.Model);
                datas.Add(clone);
                continue;
            }

            var template = ped.Kind == AnimalKind ? AnimalTemplate : HumanTemplate;
            var generated = XElement.Parse(template);
            generated.SetElementValue("Name", ped.Model);
            datas.Add(generated);
        }

        using var ms = new MemoryStream();
        var doc = new XDocument(new XDeclaration("1.0", "utf-8", null), root);
        doc.Save(ms, SaveOptions.DisableFormatting);
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    private static string HashMetadata(IEnumerable<AddonPedInfo> peds, string metadataXml)
    {
        var sb = new StringBuilder();
        foreach (var p in peds.OrderBy(p => p.Model, StringComparer.OrdinalIgnoreCase))
            sb.Append(p.Model).Append(':').Append(p.Kind).Append(';');
        sb.Append(metadataXml);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString())));
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
                    Kind = ped.Kind,
                    Pack = string.IsNullOrWhiteSpace(addon.DisplayName) ? addon.Id : addon.DisplayName
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
