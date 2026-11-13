using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace GTACoop.Sync;

/// <summary>
/// Windows NTFS junction helper used as a narrow DLC overlay mount.
/// It does not implement a process-wide GTA V filesystem hook: it redirects
/// one DLC directory entry to an immutable server cache directory.
/// </summary>
internal static class DlcJunction
{
    private const uint IoReparseTagMountPoint = 0xA0000003;
    private const uint FsctlSetReparsePoint = 0x000900A4;
    private const uint GenericWrite = 0x40000000;
    private const uint ShareAll = 0x00000001 | 0x00000002 | 0x00000004;
    private const uint OpenExisting = 3;
    private const uint FileFlagOpenReparsePoint = 0x00200000;
    private const uint FileFlagBackupSemantics = 0x02000000;

    /// <summary>Create a directory junction without invoking cmd.exe/mklink.</summary>
    public static bool TryCreate(string linkPath, string targetPath)
    {
        if (!OperatingSystem.IsWindows()) return false;

        string link = Path.GetFullPath(linkPath);
        string target = NormalizePath(targetPath);
        if (!Directory.Exists(target) || Directory.Exists(link) || File.Exists(link)) return false;
        if (!Directory.Exists(Path.GetDirectoryName(link))) return false;

        // NTFS mount points require a local drive path; do not attempt UNC or device paths.
        if (target.StartsWith("\\\\", StringComparison.Ordinal) ||
            target.StartsWith("\\\\?\\", StringComparison.Ordinal) ||
            Path.GetPathRoot(target)?.Length != 3)
            return false;

        try
        {
            Directory.CreateDirectory(link);
            string substituteName = "\\??\\" + target;
            string printName = target;
            byte[] substitute = Encoding.Unicode.GetBytes(substituteName + "\0");
            byte[] print = Encoding.Unicode.GetBytes(printName + "\0");
            byte[] pathBuffer = new byte[substitute.Length + print.Length];
            Buffer.BlockCopy(substitute, 0, pathBuffer, 0, substitute.Length);
            Buffer.BlockCopy(print, 0, pathBuffer, substitute.Length, print.Length);

            // REPARSE_DATA_BUFFER header (8 bytes), mount-point header (8 bytes), path buffer.
            byte[] buffer = new byte[16 + pathBuffer.Length];
            WriteUInt32(buffer, 0, IoReparseTagMountPoint);
            WriteUInt16(buffer, 4, checked((ushort)(8 + pathBuffer.Length)));
            WriteUInt16(buffer, 6, 0);
            WriteUInt16(buffer, 8, 0); // SubstituteNameOffset
            WriteUInt16(buffer, 10, checked((ushort)(substitute.Length - 2)));
            WriteUInt16(buffer, 12, checked((ushort)substitute.Length)); // PrintNameOffset
            WriteUInt16(buffer, 14, checked((ushort)(print.Length - 2)));
            Buffer.BlockCopy(pathBuffer, 0, buffer, 16, pathBuffer.Length);

            using SafeFileHandle handle = CreateFileW(link, GenericWrite, ShareAll, IntPtr.Zero,
                OpenExisting, FileFlagOpenReparsePoint | FileFlagBackupSemantics, IntPtr.Zero);
            if (handle.IsInvalid) throw new Win32Exception(Marshal.GetLastWin32Error());

            if (!DeviceIoControl(handle, FsctlSetReparsePoint, buffer, (uint)buffer.Length,
                    IntPtr.Zero, 0, out _, IntPtr.Zero))
                throw new Win32Exception(Marshal.GetLastWin32Error());

            if (IsJunctionTo(link, target)) return true;
            // We created this mountpoint in this call; if verification failed,
            // remove only that reparse point, never its target.
            try
            {
                if (Directory.Exists(link) &&
                    (new DirectoryInfo(link).Attributes & FileAttributes.ReparsePoint) != 0)
                    Directory.Delete(link, recursive: false);
            }
            catch { }
            return false;
        }
        catch
        {
            // Only remove the empty mountpoint we just created. Never recurse into a target.
            try
            {
                if (Directory.Exists(link) &&
                    (new DirectoryInfo(link).Attributes & FileAttributes.ReparsePoint) == 0 &&
                    !Directory.EnumerateFileSystemEntries(link).Any())
                    Directory.Delete(link, recursive: false);
            }
            catch { }
            return false;
        }
    }

    /// <summary>True only when the path is a reparse point resolving to the recorded target.</summary>
    public static bool IsJunctionTo(string linkPath, string targetPath)
    {
        if (!OperatingSystem.IsWindows() || !Directory.Exists(linkPath) || string.IsNullOrWhiteSpace(targetPath))
            return false;

        try
        {
            var info = new DirectoryInfo(Path.GetFullPath(linkPath));
            if ((info.Attributes & FileAttributes.ReparsePoint) == 0) return false;
            var resolved = info.ResolveLinkTarget(returnFinalTarget: true);
            return resolved != null && string.Equals(
                NormalizePath(resolved.FullName), NormalizePath(targetPath),
                StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    /// <summary>Remove only the junction itself, and only if its target matches the persisted owner record.</summary>
    public static void RemoveOwned(string linkPath, string expectedTarget)
    {
        if (!IsJunctionTo(linkPath, expectedTarget))
            throw new IOException("DLC mount sahipliği doğrulanamadı; bağlantı kaldırılmadı.");
        Directory.Delete(Path.GetFullPath(linkPath), recursive: false);
    }

    private static string NormalizePath(string path)
    {
        string full = Path.GetFullPath(path);
        if (full.StartsWith("\\\\?\\UNC\\", StringComparison.OrdinalIgnoreCase))
            full = "\\\\" + full[8..];
        else if (full.StartsWith("\\\\?\\", StringComparison.Ordinal))
            full = full[4..];
        return Path.TrimEndingDirectorySeparator(full);
    }

    private static void WriteUInt16(byte[] buffer, int offset, ushort value)
    {
        buffer[offset] = (byte)value;
        buffer[offset + 1] = (byte)(value >> 8);
    }

    private static void WriteUInt32(byte[] buffer, int offset, uint value)
    {
        WriteUInt16(buffer, offset, (ushort)value);
        WriteUInt16(buffer, offset + 2, (ushort)(value >> 16));
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "CreateFileW")]
    private static extern SafeFileHandle CreateFileW(string fileName, uint desiredAccess, uint shareMode,
        IntPtr securityAttributes, uint creationDisposition, uint flagsAndAttributes, IntPtr templateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeviceIoControl(SafeFileHandle device, uint controlCode,
        byte[] inBuffer, uint inBufferSize, IntPtr outBuffer, uint outBufferSize,
        out uint bytesReturned, IntPtr overlapped);
}
