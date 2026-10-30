using System.IO.Compression;
using System.Security.Cryptography;

namespace GTACoop.Rpf;

/// <summary>
/// RPF7 için gerekli düşük seviye kripto/sıkıştırma yardımcıları.
/// AES anahtarı kaynak koda gömülmez; gerekirse oyunun exe dosyasından türetilir.
/// </summary>
public static class RpfCrypto
{
    // PC sürümü AES anahtarının SHA1 özeti (kamuya açık, anahtarın kendisi değil).
    // CodeWalker/Neodymium araştırmalarında yıllardır kullanılan bilinen değer.
    private static readonly byte[] PcAesKeyHash =
    {
        0xA0, 0x79, 0x61, 0x28, 0xA7, 0x75, 0x72, 0x0A,
        0xC2, 0x04, 0xD9, 0x81, 0x9F, 0x68, 0xC1, 0x72,
        0xE3, 0x95, 0x2C, 0x6D
    };

    public static byte[] DecryptAes(byte[] data, byte[] key)
    {
        var rijndael = Aes.Create();
        rijndael.KeySize = 256;
        rijndael.Key = key;
        rijndael.BlockSize = 128;
        rijndael.Mode = CipherMode.ECB;
        rijndael.Padding = PaddingMode.None;

        var buffer = (byte[])data.Clone();
        int length = data.Length - data.Length % 16;
        if (length > 0)
        {
            using var decryptor = rijndael.CreateDecryptor();
            decryptor.TransformBlock(buffer, 0, length, buffer, 0);
        }
        return buffer;
    }

    public static byte[] EncryptAes(byte[] data, byte[] key)
    {
        var rijndael = Aes.Create();
        rijndael.KeySize = 256;
        rijndael.Key = key;
        rijndael.BlockSize = 128;
        rijndael.Mode = CipherMode.ECB;
        rijndael.Padding = PaddingMode.None;

        int length = data.Length - data.Length % 16;
        byte[] buffer = new byte[length];
        Array.Copy(data, buffer, length);
        if (length > 0)
        {
            using var encryptor = rijndael.CreateEncryptor();
            encryptor.TransformBlock(buffer, 0, length, buffer, 0);
        }
        return buffer;
    }

    /// <summary>DEFLATE sıkıştırma açma; zlib ve ham formatı otomatik algılar.</summary>
    public static byte[] Inflate(byte[] data, int expectedSize, out RpfCompression mode)
    {
        Exception? first = null;
        // zlib önce denenir; sonuç beklenen boyutta değilse ham denenir.
        try
        {
            var r = InflateCore(data, expectedSize, zlib: true);
            if (expectedSize <= 0 || r.Length == expectedSize)
            {
                mode = RpfCompression.Zlib;
                return r;
            }
        }
        catch (Exception ex) { first = ex; }

        try
        {
            var r = InflateCore(data, expectedSize, zlib: false);
            if (expectedSize <= 0 || r.Length == expectedSize)
            {
                mode = RpfCompression.Raw;
                return r;
            }
            first ??= new InvalidDataException($"Beklenen boyut {expectedSize}, açılan {r.Length}");
        }
        catch (Exception ex) { first ??= ex; }

        throw new InvalidDataException("Deflate açılamadı: " + first?.Message, first);
    }

    /// <summary>Belirtilen formatta DEFLATE sıkıştırma.</summary>
    public static byte[] Compress(byte[] data, RpfCompression mode)
    {
        using var output = new MemoryStream();
        if (mode == RpfCompression.Zlib)
        {
            using (var s = new ZLibStream(output, CompressionLevel.Optimal, leaveOpen: true))
                s.Write(data, 0, data.Length);
        }
        else
        {
            using (var s = new DeflateStream(output, CompressionLevel.Optimal, leaveOpen: true))
                s.Write(data, 0, data.Length);
        }
        return output.ToArray();
    }

    private static byte[] InflateCore(byte[] data, int expectedSize, bool zlib)
    {
        using var input = new MemoryStream(data);
        Stream s = zlib
            ? new ZLibStream(input, CompressionMode.Decompress)
            : new DeflateStream(input, CompressionMode.Decompress);
        using (s)
        {
            var output = new MemoryStream(Math.Max(expectedSize, 1024));
            s.CopyTo(output);
            return output.ToArray();
        }
    }

    /// <summary>Ham DEFLATE sıkıştırma.</summary>
    public static byte[] Deflate(byte[] data)
    {
        using var output = new MemoryStream();
        using (var deflate = new DeflateStream(output, CompressionLevel.Optimal, leaveOpen: true))
        {
            deflate.Write(data, 0, data.Length);
        }
        return output.ToArray();
    }

    /// <summary>Ham DEFLATE açma; başarısızsa false (RSC kaynakları için).</summary>
    public static bool TryInflateRaw(byte[] data, out byte[] output)
    {
        try
        {
            using var input = new MemoryStream(data);
            using var deflate = new DeflateStream(input, CompressionMode.Decompress);
            using var result = new MemoryStream(Math.Max(data.Length * 4, 4096));
            deflate.CopyTo(result);
            output = result.ToArray();
            return true;
        }
        catch
        {
            output = Array.Empty<byte>();
            return false;
        }
    }

    // --------------------------------------------------- RSC7 kaynak boyutları

    /// <summary>
    /// RPF resource (RSC7) sayfa bayraklarından sistem/graphics bölüm boyutunu
    /// hesaplar. CodeWalker'daki RpfResourcePageFlags formülünün aynısıdır.
    /// </summary>
    public static long GetResourcePageSize(uint flags)
    {
        long s0 = ((flags >> 27) & 0x1) << 0;
        long s1 = ((flags >> 26) & 0x1) << 1;
        long s2 = ((flags >> 25) & 0x1) << 2;
        long s3 = ((flags >> 24) & 0x1) << 3;
        long s4 = ((flags >> 17) & 0x7F) << 4;
        long s5 = ((flags >> 11) & 0x3F) << 5;
        long s6 = ((flags >> 7) & 0xF) << 6;
        long s7 = ((flags >> 5) & 0x3) << 7;
        long s8 = ((flags >> 4) & 0x1) << 8;
        uint shift = flags & 0xF;
        long baseSize = 0x200L << (int)shift;
        return baseSize * (s0 + s1 + s2 + s3 + s4 + s5 + s6 + s7 + s8);
    }

    public static long GetResourceTotalSize(uint systemFlags, uint graphicsFlags) =>
        GetResourcePageSize(systemFlags) + GetResourcePageSize(graphicsFlags);

    /// <summary>RSC7 sürümü, TOC bayraklarının üst nibble'larından türetilir.</summary>
    public static uint GetResourceVersion(uint systemFlags, uint graphicsFlags) =>
        (uint)((((systemFlags >> 28) & 0xF) << 4) | ((graphicsFlags >> 28) & 0xF));

    /// <summary>OpenIV uyumlu RSC7 başlığı oluşturur (16 bayt).</summary>
    public static byte[] BuildResourceHeader(uint systemFlags, uint graphicsFlags)
    {
        var header = new byte[16];
        BitConverter.TryWriteBytes(header.AsSpan(0, 4), 0x37435352u); // "RSC7"
        BitConverter.TryWriteBytes(header.AsSpan(4, 4),
            GetResourceVersion(systemFlags, graphicsFlags));
        BitConverter.TryWriteBytes(header.AsSpan(8, 4), systemFlags);
        BitConverter.TryWriteBytes(header.AsSpan(12, 4), graphicsFlags);
        return header;
    }

    public static bool HasResourceHeader(byte[] data) =>
        data.Length >= 16 && BitConverter.ToUInt32(data, 0) == 0x37435352u;

    // ------------------------------------------------------- anahtar arama

    private const int ScanAlignment = 8;
    private const int ChunkSize = 8 * 1024 * 1024;

    /// <summary>
    /// PC AES anahtarını GTA5.exe içinden SHA1 özetiyle bulur.
    /// Anahtar bir sır değildir (dosya şifreleme anahtarıdır); kaynak kodda tutmamak
    /// için oyun dosyasından türetiyoruz.
    /// </summary>
    public static byte[] FindPcAesKeyInExe(string exePath, CancellationToken ct = default)
    {
        if (!File.Exists(exePath))
            throw new FileNotFoundException("GTA5.exe bulunamadı; AES anahtarı aranamıyor.", exePath);

        using var fs = new FileStream(exePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        long length = fs.Length;
        long chunkCount = (length + ChunkSize - 1) / ChunkSize;
        byte[][] results = new byte[chunkCount][];
        var sha1 = SHA1.Create();

        Parallel.For(0, chunkCount, new ParallelOptions { CancellationToken = ct }, k =>
        {
            long start = k * ChunkSize;
            long end = Math.Min(length, start + ChunkSize + 32);
            int size = (int)(end - start);
            var chunk = new byte[size];
            lock (fs) { fs.Position = start; fs.ReadExactly(chunk, 0, size); }

            using var sha1 = SHA1.Create();
            var window = new byte[32];
            for (int i = 0; i + 32 <= size; i += ScanAlignment)
            {
                Array.Copy(chunk, i, window, 0, 32);
                var h = sha1.ComputeHash(window);
                if (h.AsSpan().SequenceEqual(PcAesKeyHash))
                {
                    results[k] = (byte[])window.Clone();
                    return;
                }
            }
        });

        foreach (var r in results)
            if (r != null) return r;

        throw new InvalidDataException(
            "GTA5.exe içinde PC AES anahtarı bulunamadı. Oyun sürümü çok yeni olabilir.");
    }
}
