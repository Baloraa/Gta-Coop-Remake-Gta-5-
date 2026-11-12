namespace GTACoop.Sync;

/// <summary>
/// Validates paths received from a remote server before they are used for cache
/// or game-folder IO. Manifest paths are untrusted input, even when the server
/// is password protected.
/// </summary>
internal static class AddonPathSafety
{
    private static readonly HashSet<string> WindowsDeviceNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
        "COM¹", "COM²", "COM³", "LPT¹", "LPT²", "LPT³"
    };

    /// <summary>Returns a normalized, safe relative path or throws SyncException.</summary>
    public static string NormalizeRelativePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new SyncException("Güvenlik: manifest içinde boş dosya yolu bulundu.");

        var normalized = path.Replace('\\', '/');
        if (normalized.StartsWith('/') || Path.IsPathRooted(normalized) ||
            normalized.Contains(':') || normalized.Contains('\0'))
            throw new SyncException($"Güvenlik: mutlak veya geçersiz dosya yolu reddedildi: {path}");

        if (normalized.Length > 1024)
            throw new SyncException("Güvenlik: dosya yolu izin verilen uzunluğu aşıyor.");

        var segments = normalized.Split('/');
        foreach (var segment in segments)
        {
            if (segment.Length == 0 || segment.Length > 255 || segment is "." or "..")
                throw new SyncException($"Güvenlik: yol geçişi içeren dosya yolu reddedildi: {path}");

            if (segment.EndsWith(' ') || segment.EndsWith('.') || segment.Any(char.IsControl) ||
                segment.IndexOfAny(new[] { '<', '>', ':', '"', '|', '?', '*', '\\' }) >= 0)
                throw new SyncException($"Güvenlik: Windows üzerinde geçersiz dosya adı reddedildi: {path}");

            var deviceStem = segment.Split('.')[0].TrimEnd(' ', '.');
            if (WindowsDeviceNames.Contains(deviceStem))
                throw new SyncException($"Güvenlik: ayrılmış Windows aygıt adı reddedildi: {path}");
        }

        return string.Join(Path.DirectorySeparatorChar, segments);
    }

    /// <summary>Combines a trusted root and an untrusted relative path without allowing escape.</summary>
    public static string GetChildPath(string root, string relativePath)
    {
        var safeRelative = NormalizeRelativePath(relativePath);
        var fullRoot = Path.GetFullPath(root);
        var fullPath = Path.GetFullPath(Path.Combine(fullRoot, safeRelative));
        var rootWithSeparator = Path.EndsInDirectorySeparator(fullRoot)
            ? fullRoot
            : fullRoot + Path.DirectorySeparatorChar;
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        if (!fullPath.StartsWith(rootWithSeparator, comparison))
            throw new SyncException($"Güvenlik: hedef klasör dışına çıkan yol reddedildi: {relativePath}");

        // A path can be lexically inside the root and still escape through an
        // existing junction/symlink. Reject reparse points in every existing
        // component below the trusted root before opening or creating the file.
        RejectExistingReparsePoints(fullRoot, safeRelative, relativePath);
        return fullPath;
    }

    private static void RejectExistingReparsePoints(string root, string safeRelative, string originalPath)
    {
        // The root itself is user-selected/trusted and may legitimately be a
        // redirected game/library folder. Only reject links introduced below it.
        var current = root;
        foreach (var segment in safeRelative.Split(Path.DirectorySeparatorChar))
        {
            current = Path.Combine(current, segment);
            if (!TryGetAttributes(current, out var attributes))
                break; // Bu ve sonraki bileşenler henüz oluşturulmadı.

            if ((attributes & FileAttributes.ReparsePoint) != 0)
                throw new SyncException($"Güvenlik: sembolik bağlantı/junction üzerinden dosya erişimi reddedildi: {originalPath}");
        }
    }

    private static bool TryGetAttributes(string path, out FileAttributes attributes)
    {
        try
        {
            attributes = File.GetAttributes(path);
            return true;
        }
        catch (FileNotFoundException) { }
        catch (DirectoryNotFoundException) { }
        attributes = default;
        return false;
    }

    /// <summary>Validates every addon in an untrusted server manifest.</summary>
    public static void ValidateManifest(Manifest? manifest)
    {
        if (manifest is null || manifest.Addons is null)
            throw new SyncException("Sunucudan geçersiz manifest alındı: eklenti listesi eksik.");

        foreach (var addon in manifest.Addons)
            ValidateAddon(addon);
    }

    /// <summary>Validates the untrusted addon envelope before it is used by the installer.</summary>
    public static void ValidateAddon(AddonInfo? addon)
    {
        if (addon is null)
            throw new SyncException("Güvenlik: manifest içinde boş eklenti kaydı bulundu.");

        if (string.IsNullOrWhiteSpace(addon.Id) || addon.Id.Length > 128 ||
            addon.Id.Contains('/') || addon.Id.Contains('\\') || addon.Id.Contains(':'))
            throw new SyncException("Güvenlik: manifest içinde geçersiz eklenti kimliği bulundu.");

        // IDs originate from a single folder name on the server. Apply Windows
        // filename rules as well, even when the ID is not used as a raw path.
        NormalizeRelativePath(addon.Id);

        if (!Enum.IsDefined(typeof(AddonType), addon.Type))
            throw new SyncException($"Güvenlik: bilinmeyen eklenti türü: {addon.Type}");
        if (addon.DisplayName?.Length > 256 || addon.Description?.Length > 4096 || addon.Version?.Length > 128)
            throw new SyncException($"Güvenlik: eklenti metaverisi izin verilen uzunluğu aşıyor: {addon.Id}");

        ValidateFiles(addon.Files);
        if (!string.IsNullOrWhiteSpace(addon.RelativePackPath))
            NormalizeRelativePath(addon.RelativePackPath);
    }

    /// <summary>Validates manifest file metadata before any download starts.</summary>
    public static void ValidateFiles(IEnumerable<AddonFileInfo>? files)
    {
        if (files is null)
            throw new SyncException("Güvenlik: manifest dosya listesi boş veya geçersiz.");

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in files)
        {
            if (file is null)
                throw new SyncException("Güvenlik: manifest içinde boş dosya kaydı bulundu.");

            var normalized = NormalizeRelativePath(file.Path);
            if (!seen.Add(normalized))
                throw new SyncException($"Güvenlik: manifest aynı dosyayı birden fazla kez içeriyor: {file.Path}");
            if (file.Size < 0)
                throw new SyncException($"Güvenlik: negatif dosya boyutu reddedildi: {file.Path}");
            if (string.IsNullOrEmpty(file.Sha256) || file.Sha256.Length != 64 ||
                file.Sha256.Any(c => !Uri.IsHexDigit(c)))
                throw new SyncException($"Güvenlik: geçersiz SHA-256 değeri: {file.Path}");
        }
    }
}
