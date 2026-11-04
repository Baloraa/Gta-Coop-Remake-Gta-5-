using GTACoop.Sync;

namespace GTACoop.Remake;

/// <summary>
/// Sunucudan gelecek script/ASI dosyaları ile oyuncunun kendi kurduğu
/// dosyalar arasındaki çakışma riskini bulur. Hiçbir dosya yazmaz veya
/// silmez; yalnızca bağlanmadan önce uyarı göstermek için kullanılır.
/// </summary>
internal static class ModConflicts
{
    public static List<string> Find(string gameFolder, Manifest manifest, SyncState? state)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(gameFolder) || !Directory.Exists(gameFolder) || manifest == null)
            return result;

        // Sunucunun yönetmediği (bizim kurmadığımız) yerel dosyaları topla.
        var local = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in LocalFiles(gameFolder))
            if (!local.ContainsKey(name)) local[name] = name;

        var managed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (state != null)
        {
            foreach (var addon in state.Addons)
                foreach (var path in addon.InstalledPaths)
                    managed.Add(Path.GetFileName(path));
        }

        foreach (var addon in manifest.Addons)
        {
            if (addon.Type != AddonType.ClientScript &&
                addon.Type != AddonType.AsiPlugin &&
                addon.Type != AddonType.ClientFiles)
                continue;

            foreach (var file in addon.Files)
            {
                var name = Path.GetFileName(file.Path);
                if (string.IsNullOrWhiteSpace(name)) continue;

                var ext = Path.GetExtension(name);
                if (!ext.Equals(".dll", StringComparison.OrdinalIgnoreCase) &&
                    !ext.Equals(".asi", StringComparison.OrdinalIgnoreCase) &&
                    !ext.Equals(".lua", StringComparison.OrdinalIgnoreCase) &&
                    !ext.Equals(".cs", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!local.ContainsKey(name) || managed.Contains(name)) continue;

                var entry = $"{name}  ({addon.DisplayName})";
                if (!result.Contains(entry)) result.Add(entry);
            }
        }

        result.Sort(StringComparer.CurrentCultureIgnoreCase);
        return result;
    }

    /// <summary>scripts/ ve asi/ altındaki yerel script dosya adları.</summary>
    private static IEnumerable<string> LocalFiles(string gameFolder)
    {
        foreach (var folder in new[] { "scripts", "asi", "lua" })
        {
            var dir = Path.Combine(gameFolder, folder);
            if (!Directory.Exists(dir)) continue;

            string[] files;
            try
            {
                files = Directory.GetFiles(dir, "*.*", SearchOption.AllDirectories);
            }
            catch
            {
                continue;
            }

            foreach (var file in files)
                yield return Path.GetFileName(file);
        }
    }
}
