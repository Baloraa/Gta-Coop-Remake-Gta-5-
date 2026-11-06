using System.Diagnostics;
using System.Runtime.InteropServices;

namespace GTACoop.Sync;

/// <summary>
/// Bir sürecin RAM kullanımına işletim sistemi düzeyinde sınır koyar (Windows
/// Job Object). Sunucu modunda gtaserver.core ve SyncServer süreçleri bu sınırın
/// içinde çalışır; sınır aşılırsa süreç Windows tarafından sonlandırılır ve
/// sunucu modu süreci yeniden başlatır.
///
/// Oyun/sunucu dosyalarına hiçbir şey yazılmaz: yalnızca çalışan süreçlerin
/// bellek politikası değişir.
/// </summary>
public sealed class MemoryJob : IDisposable
{
    private const uint InfoClassExtendedLimit = 9;
    private const uint LimitProcessMemory = 0x00000100;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateJobObject(IntPtr securityAttributes, string? name);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetInformationJobObject(IntPtr job, uint infoClass,
        ref JobObjectExtendedLimitInformation info, uint infoLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);

    [DllImport("psapi.dll")]
    private static extern bool EmptyWorkingSet(IntPtr process);

    [StructLayout(LayoutKind.Sequential)]
    private struct JobObjectBasicLimitInformation
    {
        public long PerProcessUserTimeLimit;
        public long PerJobUserTimeLimit;
        public uint LimitFlags;
        public UIntPtr MinimumWorkingSetSize;
        public UIntPtr MaximumWorkingSetSize;
        public uint ActiveProcessLimit;
        public UIntPtr Affinity;
        public uint PriorityClass;
        public uint SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IoCounters
    {
        public ulong ReadOperationCount;
        public ulong WriteOperationCount;
        public ulong OtherOperationCount;
        public ulong ReadTransferCount;
        public ulong WriteTransferCount;
        public ulong OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JobObjectExtendedLimitInformation
    {
        public JobObjectBasicLimitInformation BasicLimitInformation;
        public IoCounters IoInfo;
        public UIntPtr ProcessMemoryLimit;
        public UIntPtr JobMemoryLimit;
        public UIntPtr PeakProcessMemoryUsed;
        public UIntPtr PeakJobMemoryUsed;
    }

    private IntPtr _handle;

    /// <summary>Megabayt cinsinden süreç başına bellek sınırı.</summary>
    public int LimitMb { get; }

    public bool IsActive => _handle != IntPtr.Zero;

    public MemoryJob(int limitMb)
    {
        LimitMb = Math.Max(0, limitMb);

        try
        {
            _handle = CreateJobObject(IntPtr.Zero, null);
            if (_handle == IntPtr.Zero) return;

            var info = new JobObjectExtendedLimitInformation
            {
                BasicLimitInformation = new JobObjectBasicLimitInformation
                {
                    LimitFlags = LimitProcessMemory
                },
                ProcessMemoryLimit = new UIntPtr((ulong)LimitMb * 1024UL * 1024UL)
            };

            if (!SetInformationJobObject(_handle, InfoClassExtendedLimit,
                    ref info, (uint)Marshal.SizeOf<JobObjectExtendedLimitInformation>()))
            {
                CloseHandle(_handle);
                _handle = IntPtr.Zero;
            }
        }
        catch
        {
            // Sınır kurulamazsa sunucu yine çalışsın (sınırsız mod).
            _handle = IntPtr.Zero;
        }
    }

    /// <summary>Süreci sınıra bağlar. Başarısız olursa sunucu yine çalışır.</summary>
    public bool Assign(Process process)
    {
        if (!IsActive) return false;

        try
        {
            return AssignProcessToJobObject(_handle, process.Handle);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Boşta duran belleği işletim sistemine geri verir (yumuşak temizlik).</summary>
    public static void Trim(Process process)
    {
        try
        {
            if (process.HasExited) return;
            EmptyWorkingSet(process.Handle);
        }
        catch
        {
            // Yetki/kapanmış süreç: sessizce geç.
        }
    }

    public void Dispose()
    {
        if (_handle == IntPtr.Zero) return;

        try
        {
            CloseHandle(_handle);
        }
        catch
        {
            // Kapanışta hata sunucuyu etkilemesin.
        }
        finally
        {
            _handle = IntPtr.Zero;
        }
    }
}
