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
