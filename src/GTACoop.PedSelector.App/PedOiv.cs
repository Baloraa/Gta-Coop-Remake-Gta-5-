using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using GTACoop.Sync;

namespace GTACoop.PedSelector.App
{
    /// <summary>
    /// Ped paketini OpenIV ile kurulup kaldırılabilen (.oiv) bağımsız bir moda
    /// dönüştürür: dlcpacks altına kendi DLC'si, scripts altına ped seçici
    /// scripti ve listesi, update.rpf dlclist girdisi.
    /// </summary>
    internal static class PedOiv
    {
        public const string PackId = "gtacoop_peds";

        /// <summary>OpenIV paket kimliği GUID olmak zorunda.</summary>
        public const string PackageGuid = "7f3a1c92-5b64-4d0e-9a71-2c8e5d41b603";
        public const string ScriptDllName = "AddonPedPlus.dll";
        public const string IniFileName = "AddonPedPlus.ini";

        /// <summary>Oyun içi menü tuşu bu ini dosyasından değiştirilir.</summary>
        public static readonly string IniContent = string.Join(Environment.NewLine, new[]
        {
            "; AddonPed+ ayarlari",
            "; MenuKey: menuyu acan tus (F10, F7, M, INSERT...)",
            "[AddonPedPlus]",
            "MenuKey=F10",
            ""
        });

        public const string ListFileName = "GTACoop-peds.txt";

        public static string ScriptDllPath
        {
            get
            {
                var candidates = new[]
                {
                    Path.Combine(AppContext.BaseDirectory, "payload", ScriptDllName),
                    Path.Combine(AppContext.BaseDirectory, ScriptDllName)
                };
                return candidates.FirstOrDefault(File.Exists) ?? candidates[0];
            }
        }

        public static CardListText BuildListText(IEnumerable<AddonPedInfo> peds)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# model|ad|tur|saglik|temel model|ragdoll|meta dogrulandi");
            foreach (var ped in peds)
            {
                sb.Append(ped.Model).Append('|')
                  .Append((ped.Name ?? "").Replace('|', ' ')).Append('|')
                  .Append(ped.Kind ?? "human").Append('|')
                  .Append(ped.Health > 0 ? ped.Health : 200).Append('|')
                  .Append(ped.BaseModel ?? "").Append('|')
                  .Append(AddonPeds.NormalizeRagdoll(ped.Ragdoll)).Append('|')
                  .Append(ped.MetaVerified ? "true" : "false")
                  .AppendLine();
            }
            return new CardListText { Text = sb.ToString(), Count = peds.Count() };
        }

        public sealed class CardListText
        {
            public string Text { get; init; } = "";
            public int Count { get; init; }
        }

        public static string Build(string packDir, string outputOiv, Action<string> log)
        {
            log ??= _ => { };

            var packFile = AddonPeds.LoadOrCreatePackFile(packDir,
                AddonPeds.DiscoverLoosePeds(packDir, log));
            var enabled = packFile.Peds
                .Where(p => p.Enabled && !string.IsNullOrWhiteSpace(p.Model) &&
                            p.Files != null && p.Files.Count > 0)
                .ToList();
            if (enabled.Count == 0)
                throw new InvalidOperationException("Etkin ped yok. En az bir ped seçin.");

            var scriptDll = ScriptDllPath;
            if (!File.Exists(scriptDll))
                log("UYARI: ped seçici scripti bulunamadı: " + scriptDll);

            var list = BuildListText(enabled);
            var tempRoot = Path.Combine(Path.GetTempPath(), "gtacoop-ped-oiv-" +
                                                        Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);
            try
            {
                var dlc = AddonPeds.PrepareSelfContained(packDir, PackId,
                    Path.Combine(tempRoot, "pack"), packFile, log);
                if (!File.Exists(dlc))
                    throw new InvalidOperationException("DLC paketi oluşturulamadı.");

                var target = outputOiv;
                if (File.Exists(target)) File.Delete(target);
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(target))!);

                using (var zip = ZipFile.Open(target, ZipArchiveMode.Create))
                {
                    WriteText(zip, "assembly.xml", AssemblyXml().ToString());
                    WriteFile(zip,
                        "content/update-x64-dlcpacks-" + PackId + "/dlc.rpf", dlc);
                    WriteText(zip, "content/scripts/" + ListFileName, list.Text);
                    WriteText(zip, "content/scripts/" + IniFileName, IniContent);
                    if (File.Exists(scriptDll))
                        WriteFile(zip, "content/scripts/" + ScriptDllName, scriptDll);
                }

                Verify(target, log, list.Count, File.Exists(scriptDll));
                return target;
            }
            finally
            {
                try { Directory.Delete(tempRoot, true); } catch { }
            }
        }

        private static void Verify(string oivPath, Action<string> log, int pedCount,
            bool hasScript)
        {
            using var pkg = OivPackage.Open(oivPath);
            if (pkg.FileAdds.Count == 0)
                throw new InvalidOperationException("OIV paketinde dosya ekleme yok.");

            var wants = new List<string>
            {
                "mods/update/x64/dlcpacks/" + PackId + "/dlc.rpf",
                "scripts/" + ListFileName,
                "scripts/" + IniFileName
            };
            if (hasScript) wants.Add("scripts/" + ScriptDllName);

            foreach (var want in wants)
            {
                if (!pkg.FileAdds.Any(f =>
                        f.Destination.Equals(want, StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidOperationException("OIV doğrulanamadı, eksik: " + want);
            }

            if (!pkg.DlclistEntries.Any(e =>
                    e.Equals(PackId, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("OIV dlclist girdisi doğrulanamadı.");

            log($"OIV hazır: {pedCount} ped, {pkg.FileAdds.Count} dosya, " +
                $"dlclist: {string.Join(", ", pkg.DlclistEntries)}");
        }

        private static XDocument AssemblyXml() => new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement("package",
                new XAttribute("version", "2.2"),
                new XAttribute("id", PackageGuid),
                new XAttribute("target", "Five"),
                new XElement("metadata",
                    new XElement("name", "AddonPed+"),
                    new XElement("description",
                        "AddonPed+ : addon ped paketi ve oyun ici ped secici. " +
                        "GTACoop sunucusu veya launcher'i gerektirmez."),
                    new XElement("version",
                        new XElement("major", "1"),
                        new XElement("minor", "0"),
                        new XElement("tag", "gtacoop")),
                    new XElement("author",
                        new XElement("displayName", "GTACoop Remake"))),
                new XElement("content",
                    new XElement("add",
                        new XAttribute("source",
                            "content\\update-x64-dlcpacks-" + PackId + "\\dlc.rpf"),
                        "update/x64/dlcpacks/" + PackId + "/dlc.rpf"),
                    new XElement("add",
                        new XAttribute("source", "content\\scripts\\" + ListFileName),
                        "scripts/" + ListFileName),
                    new XElement("add",
                        new XAttribute("source", "content\\scripts\\" + ScriptDllName),
                        "scripts/" + ScriptDllName),
                    new XElement("add",
                        new XAttribute("source", "content\\scripts\\" + IniFileName),
                        "scripts/" + IniFileName),
                    new XElement("archive",
                        new XAttribute("path", "update/update.rpf"),
                        new XAttribute("createIfNotExist", "False"),
                        new XElement("xml",
                            new XAttribute("path", "common/data/dlclist.xml"),
                            new XElement("add",
                                new XElement("Item", "dlcpacks:/" + PackId + "/")))))));

        private static void WriteText(ZipArchive zip, string name, string content)
        {
            var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
            using var stream = entry.Open();
            var bytes = new UTF8Encoding(false).GetBytes(content);
            stream.Write(bytes, 0, bytes.Length);
        }

        private static void WriteFile(ZipArchive zip, string name, string source)
        {
            var entry = zip.CreateEntry(name, CompressionLevel.NoCompression);
            using var output = entry.Open();
            using var input = File.OpenRead(source);
            input.CopyTo(output);
        }
    }
}
