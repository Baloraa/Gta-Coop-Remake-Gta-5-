using GTACoop.Sync;

var passed = 0;
var failed = 0;
var skipped = 0;

void Check(string name, Action test)
{
    try
    {
        test();
        Console.WriteLine($"PASS  {name}");
        passed++;
    }
    catch (SkipTestException ex)
    {
        Console.WriteLine($"SKIP  {name}: {ex.Message}");
        skipped++;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"FAIL  {name}: {ex.Message}");
        failed++;
    }
}

void ExpectSyncException(Action action)
{
    try { action(); }
    catch (SyncException) { return; }
    throw new InvalidOperationException("SyncException bekleniyordu.");
}

Check("normal relative path is accepted", () =>
{
    var normalized = AddonPathSafety.NormalizeRelativePath("scripts/player/main.dll");
    if (!normalized.Contains(Path.DirectorySeparatorChar))
        throw new InvalidOperationException("Yol ayıracı normalize edilmedi.");
});

Check("parent traversal is rejected", () =>
    ExpectSyncException(() => AddonPathSafety.NormalizeRelativePath("scripts/../../outside.dll")));

Check("absolute and drive paths are rejected", () =>
{
    ExpectSyncException(() => AddonPathSafety.NormalizeRelativePath("/etc/passwd"));
    ExpectSyncException(() => AddonPathSafety.NormalizeRelativePath("C:/Windows/win.ini"));
});

Check("control characters and overlong path segments are rejected", () =>
{
    ExpectSyncException(() => AddonPathSafety.NormalizeRelativePath("scripts/bad\u0001.dll"));
    ExpectSyncException(() => AddonPathSafety.NormalizeRelativePath("scripts/" + new string('a', 256) + ".dll"));
});

Check("Windows device names are rejected", () =>
    ExpectSyncException(() => AddonPathSafety.NormalizeRelativePath("scripts/CON.txt")));

Check("duplicate manifest paths are rejected case-insensitively", () =>
{
    var hash = new string('a', 64);
    var files = new[]
    {
        new AddonFileInfo { Path = "scripts/A.dll", Size = 1, Sha256 = hash },
        new AddonFileInfo { Path = "scripts/a.dll", Size = 1, Sha256 = hash }
    };
    ExpectSyncException(() => AddonPathSafety.ValidateFiles(files));
});

Check("invalid addon identity and enum values are rejected", () =>
{
    ExpectSyncException(() => AddonPathSafety.ValidateAddon(new AddonInfo
    {
        Id = "../escape",
        Type = AddonType.ClientScript,
        Files = new List<AddonFileInfo>()
    }));
    ExpectSyncException(() => AddonPathSafety.ValidateAddon(new AddonInfo
    {
        Id = "valid-addon",
        Type = (AddonType)999,
        Files = new List<AddonFileInfo>()
    }));
});

Check("invalid manifest metadata is rejected", () =>
{
    ExpectSyncException(() => AddonPathSafety.ValidateFiles(new[]
    {
        new AddonFileInfo { Path = "scripts/a.dll", Size = -1, Sha256 = new string('a', 64) }
    }));
    ExpectSyncException(() => AddonPathSafety.ValidateFiles(new[]
    {
        new AddonFileInfo { Path = "scripts/a.dll", Size = 1, Sha256 = "not-a-hash" }
    }));
});

Check("child path remains under trusted root", () =>
{
    var root = Path.Combine(Path.GetTempPath(), "gtacoop-safe-path-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(root);
    try
    {
        var child = AddonPathSafety.GetChildPath(root, "scripts/player.dll");
        var prefix = Path.EndsInDirectorySeparator(Path.GetFullPath(root))
            ? Path.GetFullPath(root)
            : Path.GetFullPath(root) + Path.DirectorySeparatorChar;
        if (!child.StartsWith(prefix, OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            throw new InvalidOperationException("Hedef kök klasörün dışında.");
    }
    finally { Directory.Delete(root, recursive: true); }
});

Check("server scanner rejects colliding sanitized DLC mount names", () =>
{
    var addons = Path.Combine(Path.GetTempPath(), "gtacoop-dlc-collision-" + Guid.NewGuid().ToString("N"));
    var first = Path.Combine(addons, "dlcpacks", "my-car");
    var second = Path.Combine(addons, "dlcpacks", "my_car");
    Directory.CreateDirectory(first);
    Directory.CreateDirectory(second);
    try
    {
        File.WriteAllText(Path.Combine(first, "dlc.rpf"), "first");
        File.WriteAllText(Path.Combine(second, "dlc.rpf"), "second");
        var scanner = new ServerScanner(addons);
        var manifest = scanner.Scan("test", "test-server", 4599).Manifest;
        var collisions = manifest.Addons.Where(a => a.Type == AddonType.DlcPack &&
            AddonApplier.MountFolderName(a.Id) == AddonApplier.MountFolderName("my-car")).ToList();
        if (collisions.Count != 1)
            throw new InvalidOperationException($"Aynı mount adına {collisions.Count} paket eklendi; 1 bekleniyordu.");
    }
    finally { try { Directory.Delete(addons, recursive: true); } catch { } }
});

Check("existing symbolic-link directory is rejected when supported", () =>
{
    var root = Path.Combine(Path.GetTempPath(), "gtacoop-link-root-" + Guid.NewGuid().ToString("N"));
    var outside = Path.Combine(Path.GetTempPath(), "gtacoop-link-outside-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(root);
    Directory.CreateDirectory(outside);
    try
    {
        try { Directory.CreateSymbolicLink(Path.Combine(root, "linked"), outside); }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or PlatformNotSupportedException)
        {
            throw new SkipTestException("OS permission not available for symbolic links.");
        }
        ExpectSyncException(() => AddonPathSafety.GetChildPath(root, "linked/escape.txt"));
    }
    finally
    {
        try { Directory.Delete(root, recursive: true); } catch { }
        try { Directory.Delete(outside, recursive: true); } catch { }
    }
});

Check("DLC junction mount is owned, target-checked, and unmount preserves cache", () =>
{
    if (!OperatingSystem.IsWindows())
        throw new SkipTestException("NTFS junction test requires Windows.");

    var root = Path.Combine(Path.GetTempPath(), "gtacoop-junction-test-" + Guid.NewGuid().ToString("N"));
    var cache = Path.Combine(root, "cache");
    var mount = Path.Combine(root, "game", "mods", "dlcpacks", "gcs_test");
    Directory.CreateDirectory(cache);
    Directory.CreateDirectory(Path.GetDirectoryName(mount)!);
    var marker = Path.Combine(cache, "dlc.rpf");
    File.WriteAllText(marker, "cache-content");
    try
    {
        if (!DlcJunction.TryCreate(mount, cache))
            throw new SkipTestException("This Windows environment does not allow creating/verifying NTFS junctions.");
        if (!DlcJunction.IsJunctionTo(mount, cache))
            throw new InvalidOperationException("Created mount did not resolve to its cache target.");

        bool refusedWrongOwner = false;
        try { DlcJunction.RemoveOwned(mount, root); }
        catch (IOException) { refusedWrongOwner = true; }
        if (!refusedWrongOwner || !Directory.Exists(mount))
            throw new InvalidOperationException("Mount was removed with a mismatched owner target.");

        DlcJunction.RemoveOwned(mount, cache);
        if (Directory.Exists(mount))
            throw new InvalidOperationException("Mount point still exists after unmount.");
        if (!File.Exists(marker) || File.ReadAllText(marker) != "cache-content")
            throw new InvalidOperationException("Unmount modified or removed the cache target.");
    }
    finally
    {
        try { if (Directory.Exists(mount)) Directory.Delete(mount, recursive: false); } catch { }
        try { Directory.Delete(root, recursive: true); } catch { }
    }
});

Check("addon ped discovery does not follow symbolic links when supported", () =>
{
    var root = Path.Combine(Path.GetTempPath(), "gtacoop-peds-root-" + Guid.NewGuid().ToString("N"));
    var outside = Path.Combine(Path.GetTempPath(), "gtacoop-peds-outside-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(root);
    Directory.CreateDirectory(outside);
    try
    {
        File.WriteAllText(Path.Combine(root, "local_model.ydd"), "local");
        File.WriteAllText(Path.Combine(outside, "private_model.ydd"), "private");
        try { Directory.CreateSymbolicLink(Path.Combine(root, "external"), outside); }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or PlatformNotSupportedException)
        {
            throw new SkipTestException("OS permission not available for symbolic links.");
        }

        var discovered = AddonPeds.DiscoverLoosePeds(root, extractLegacy: false);
        if (discovered.Any(p => p.Model.Contains("private_model", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Dış bağlantıdaki ped dosyası keşfedildi.");
        if (!discovered.Any(p => p.Model.Contains("local_model", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Yerel ped dosyası keşfedilemedi.");
    }
    finally
    {
        try { Directory.Delete(root, recursive: true); } catch { }
        try { Directory.Delete(outside, recursive: true); } catch { }
    }
});

Check("server scanner does not publish files through a symbolic link when supported", () =>
{
    var addons = Path.Combine(Path.GetTempPath(), "gtacoop-scan-addons-" + Guid.NewGuid().ToString("N"));
    var outside = Path.Combine(Path.GetTempPath(), "gtacoop-scan-outside-" + Guid.NewGuid().ToString("N"));
    var script = Path.Combine(addons, "scripts", "sample");
    Directory.CreateDirectory(script);
    Directory.CreateDirectory(outside);
    try
    {
        File.WriteAllText(Path.Combine(script, "normal.dll"), "normal");
        File.WriteAllText(Path.Combine(outside, "private.txt"), "not for publishing");
        try { Directory.CreateSymbolicLink(Path.Combine(script, "external"), outside); }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or PlatformNotSupportedException)
        {
            throw new SkipTestException("OS permission not available for symbolic links.");
        }

        var scanner = new ServerScanner(addons);
        var manifest = scanner.Scan("test", "test-server", 4599).Manifest;
        var sample = manifest.Addons.FirstOrDefault(a => a.Id == "sample");
        if (sample is null || sample.Files.Any(f => f.Path.Contains("private", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Sembolik bağlantı hedefindeki dosya manifeste girdi.");
        if (!sample.Files.Any(f => f.Path == "normal.dll"))
            throw new InvalidOperationException("Normal dosya taramada kayboldu.");
    }
    finally
    {
        try { Directory.Delete(addons, recursive: true); } catch { }
        try { Directory.Delete(outside, recursive: true); } catch { }
    }
});

Console.WriteLine($"\nSonuç: {passed} geçti, {failed} başarısız, {skipped} atlandı.");
return failed == 0 ? 0 : 1;

sealed class SkipTestException : Exception
{
    public SkipTestException(string message) : base(message) { }
}
