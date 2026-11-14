using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using GTACoop.Rpf;
using GTACoop.Sync;

namespace GTACoop.PedSelector.App
{
    /// <summary>
    /// Aynı .oiv paketini GTACoop kurulumu olmadan doğrudan oyuna uygular ve
    /// kaldırır. Kurulan dosyalar ve eklenen dlclist girdileri bir manifestte
    /// tutulur; kaldırma yalnızca bu kayıtları geri alır.
    /// </summary>
    internal static class PedInstaller
    {
        private const string ManifestName = "installed.json";

        public static string ManifestPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "GTACoop Ped Selector", ManifestName);

        private sealed class Manifest
        {
            public string GameRoot { get; set; } = "";
            public List<string> Files { get; set; } = new();
            public List<string> DlclistEntries { get; set; } = new();
            public string InstalledUtc { get; set; } = "";
        }

        public static bool IsInstalled => File.Exists(ManifestPath);

        public static string UpdateRpfPath(string gameRoot) =>
            Path.Combine(gameRoot, "mods", "update", "update.rpf");

        public static void Install(string gameRoot, string oivPath, Action<string> log)
        {
            log ??= _ => { };
            if (!Directory.Exists(gameRoot))
                throw new InvalidOperationException("Oyun klasörü bulunamadı: " + gameRoot);
            if (!File.Exists(UpdateRpfPath(gameRoot)))
                throw new InvalidOperationException(
                    "mods/update/update.rpf yok. OpenIV ile mods klasörünü oluşturun " +
                    "(Tools → ASI Manager değil, OpenIV → Edit mode → mods kurulumu).");
            if (IsInstalled)
                throw new InvalidOperationException("Zaten kurulu. Önce 'Kurulumu Kaldır'.");

            using var pkg = OivPackage.Open(oivPath);
            var manifest = new Manifest { GameRoot = gameRoot };
            var created = new List<string>();
            try
            {
                foreach (var add in pkg.FileAdds)
                {
                    var dest = Path.Combine(gameRoot, add.Destination.Replace('/', '\\'));
                    if (File.Exists(dest))
                        throw new InvalidOperationException("Dosya zaten var: " + add.Destination);
                    pkg.CopyEntryTo(add, dest);
                    manifest.Files.Add(add.Destination);
                    created.Add(dest);
                    log("Kopyalandı: " + add.Destination);
                }

                if (pkg.DlclistEntries.Count > 0)
                {
                    WriteDlclist(gameRoot, pkg.DlclistEntries, add: true, log);
                    manifest.DlclistEntries.AddRange(pkg.DlclistEntries);
                }

                manifest.InstalledUtc = DateTime.UtcNow.ToString("o");
                Directory.CreateDirectory(Path.GetDirectoryName(ManifestPath)!);
                File.WriteAllText(ManifestPath, JsonSerializer.Serialize(manifest,
                    new JsonSerializerOptions { WriteIndented = true }));
                log("Kurulum tamam: " + gameRoot);
            }
            catch
            {
                foreach (var file in created)
                {
                    try { if (File.Exists(file)) File.Delete(file); } catch { }
                }
                throw;
            }
        }

        public static void Uninstall(Action<string> log)
        {
            log ??= _ => { };
            if (!File.Exists(ManifestPath))
                throw new InvalidOperationException("Kurulum kaydı yok.");

            var manifest = JsonSerializer.Deserialize<Manifest>(File.ReadAllText(ManifestPath))
                           ?? throw new InvalidOperationException("Kurulum kaydı okunamadı.");

            if (manifest.DlclistEntries.Count > 0 && File.Exists(UpdateRpfPath(manifest.GameRoot)))
            {
                try
                {
                    WriteDlclist(manifest.GameRoot, manifest.DlclistEntries, add: false, log);
                }
                catch (Exception ex)
                {
                    log("dlclist girdisi kaldırılamadı: " + ex.Message);
                }
            }

            foreach (var rel in manifest.Files)
            {
                var path = Path.Combine(manifest.GameRoot, rel.Replace('/', '\\'));
                try
                {
                    if (!File.Exists(path)) continue;
                    File.Delete(path);
                    log("Silindi: " + rel);
                    RemoveEmptyDirs(Path.GetDirectoryName(path), manifest.GameRoot);
                }
                catch (Exception ex)
                {
                    log("Silinemedi (" + rel + "): " + ex.Message);
                }
            }

            File.Delete(ManifestPath);
            log("Kurulum kaldırıldı.");
        }

        private static void WriteDlclist(string gameRoot, IReadOnlyCollection<string> entries,
            bool add, Action<string> log)
        {
            var updateRpf = UpdateRpfPath(gameRoot);
            using var archive = RpfArchive.Open(updateRpf);
            var file = archive.Find(Dlclist.RelativePath)
                       ?? throw new InvalidOperationException(
                           "update.rpf içinde dlclist.xml bulunamadı.");

            var (xml, enc) = Dlclist.Decode(archive.ReadFile(file));
            var existing = Dlclist.GetPackNames(xml);

            if (add)
            {
                var missing = entries
                    .Where(e => !existing.Contains(e, StringComparer.OrdinalIgnoreCase))
                    .ToList();
                if (missing.Count == 0) return;
                var updated = Dlclist.AddPackNames(xml, missing);
                var bytes = Dlclist.Encode(updated, Dlclist.Normalize(updated, enc));
                archive.SetFile(Dlclist.RelativePath, bytes, verify: true);
                log("dlclist eklendi: " + string.Join(", ", missing));
            }
            else
            {
                var present = entries
                    .Where(e => existing.Contains(e, StringComparer.OrdinalIgnoreCase))
                    .ToList();
                if (present.Count == 0) return;
                var updated = Dlclist.RemovePackNames(xml, present);
                var bytes = Dlclist.Encode(updated, Dlclist.Normalize(updated, enc));
                archive.SetFile(Dlclist.RelativePath, bytes, verify: true);
                log("dlclist kaldırıldı: " + string.Join(", ", present));
            }
        }

        private static void RemoveEmptyDirs(string? dir, string root)
        {
            if (string.IsNullOrWhiteSpace(dir)) return;
            var full = Path.GetFullPath(dir);
            var rootFull = Path.GetFullPath(root);
            while (full.Length > rootFull.Length && full.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    if (Directory.EnumerateFileSystemEntries(full).Any()) return;
                    Directory.Delete(full);
                }
                catch
                {
                    return;
                }
                full = Path.GetDirectoryName(full)!;
            }
        }
    }
}
