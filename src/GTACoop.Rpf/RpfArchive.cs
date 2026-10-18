using System.Text;

namespace GTACoop.Rpf;

/// <summary>
/// GTA V RPF7 arşivleri için okuma/yazma kütüphanesi.
/// Yalnızca mods klasöründeki arşivlerde güvenli değişiklik yapmak için tasarlandı:
/// - Dosya içeriği değiştirme (in-place veya sona ekleme)
/// - Yeni dosya eklenmez, dosya silinmez, TOC boyutu asla değişmez.
/// </summary>
public sealed class RpfArchive : IDisposable
{
    public const uint Magic = 0x52504637;
    public const int EntrySize = 16;
    public const uint DirectoryIdent = 0x7FFFFF00;

    private readonly FileStream _fs;
    private readonly byte[] _toc;
    private readonly List<RpfEntry> _entries = new();

    public string Path { get; }
    public uint EntryCount { get; }
    public uint NamesLength { get; }
    public RpfEncryption Encryption { get; }
    public RpfDirectoryEntry Root { get; }
    public long FileLength => _fs.Length;

    public IReadOnlyList<RpfEntry> AllEntries => _entries;

    public bool HasEncryptedFiles
    {
        get
        {
            foreach (var e in _entries)
                if (e is RpfFileEntry f && f.IsEncrypted) return true;
            return false;
        }
    }

    private RpfArchive(string path, FileStream fs, uint entryCount, uint namesLength,
        RpfEncryption encryption, byte[] toc, byte[] names)
    {
        Path = path;
        _fs = fs;
        EntryCount = entryCount;
        NamesLength = namesLength;
        Encryption = encryption;
        _toc = toc;

        ParseEntries(names);

        if (_entries.Count == 0 || _entries[0] is not RpfDirectoryEntry root)
            throw new InvalidDataException("RPF7: kök dizin girdisi bulunamadı.");

        Root = root;
        BuildTree();
    }

    // ---------------------------------------------------------------- Open

    public static RpfArchive Open(string path, byte[]? aesKey = null)
    {
        // Geçici kilitlere (antivirüs taraması vb.) karşı birkaç kez dene
        Exception? last = null;
        for (int attempt = 0; attempt < 4; attempt++)
        {
            try
            {
                return OpenCore(path, aesKey);
            }
            catch (IOException ex)
            {
                last = ex;
                Thread.Sleep(400 * (attempt + 1));
            }
        }
        throw last!;
    }

    private static RpfArchive OpenCore(string path, byte[]? aesKey)
    {
        FileStream? fs = null;
        try
        {
            fs = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);

            var header = new byte[16];
            fs.ReadExactly(header, 0, 16);

            uint magic = BitConverter.ToUInt32(header, 0);
            uint entryCount = BitConverter.ToUInt32(header, 4);
            uint namesLength = BitConverter.ToUInt32(header, 8);
            uint encRaw = BitConverter.ToUInt32(header, 12);

            if (magic != Magic)
                throw new InvalidDataException($"Bu dosya bir GTA V RPF7 arşivi değil: {path}");
            if (entryCount == 0 || entryCount > 5_000_000)
                throw new InvalidDataException("RPF7: girdi sayısı geçersiz.");
            if (namesLength > 512 * 1024 * 1024)
                throw new InvalidDataException("RPF7: isim tablosu boyutu geçersiz.");

            var encryption = (RpfEncryption)encRaw;
            if (encryption == RpfEncryption.None) encryption = RpfEncryption.Open;

            long tocSize = (long)entryCount * EntrySize;
            if (16 + tocSize + namesLength > fs.Length)
                throw new InvalidDataException("RPF7: TOC/isim tablosu dosya boyutunu aşıyor.");

            var tocRaw = new byte[tocSize];
            fs.ReadExactly(tocRaw, 0, tocRaw.Length);
            var namesRaw = new byte[namesLength];
            fs.ReadExactly(namesRaw, 0, namesRaw.Length);

            byte[] toc = encryption switch
            {
                RpfEncryption.Open => tocRaw,
                RpfEncryption.Aes => RpfCrypto.DecryptAes(tocRaw,
                    aesKey ?? throw new AesKeyRequiredException(path)),
                RpfEncryption.Ng => throw new NotSupportedException(
                    "NG (Gen9) şifreli RPF arşivleri desteklenmiyor. Bu dosya büyük olasılıkla " +
                    "orijinal oyun arşividir; lütfen yalnızca mods klasöründeki kopyalar üzerinde çalışın."),
                _ => throw new InvalidDataException($"RPF7: bilinmeyen şifreleme türü 0x{encRaw:X8}")
            };

            byte[] names = encryption switch
            {
                RpfEncryption.Open => namesRaw,
                RpfEncryption.Aes => RpfCrypto.DecryptAes(namesRaw,
                    aesKey ?? throw new AesKeyRequiredException(path)),
                _ => namesRaw
            };


            return new RpfArchive(path, fs, entryCount, namesLength, encryption, toc, names);
        }
        catch
        {
            fs?.Dispose();
            throw;
        }
    }

    // ----------------------------------------------------------- Parse

    private void ParseEntries(byte[] names)
    {
        string ReadName(uint offset)
        {
            if (offset >= names.Length) return string.Empty;
            int end = (int)offset;
            while (end < names.Length && names[end] != 0) end++;
            return Encoding.UTF8.GetString(names, (int)offset, end - (int)offset);
        }

        for (uint i = 0; i < EntryCount; i++)
        {
            int off = (int)(i * EntrySize);
            uint y = BitConverter.ToUInt32(_toc, off);
            uint x = BitConverter.ToUInt32(_toc, off + 4);

            RpfEntry entry;
            if (x == DirectoryIdent)
            {
                var dir = new RpfDirectoryEntry
                {
                    NameOffset = y,
                    EntriesIndex = BitConverter.ToUInt32(_toc, off + 8),
                    EntriesCount = BitConverter.ToUInt32(_toc, off + 12)
                };
                entry = dir;
            }
            else if ((x & 0x80000000) == 0)
            {
                // varolan dosya: ulong buf = NameOffset(16)|FileSize(24)<<16|FileOffset(24)<<40
                ulong buf = y | ((ulong)x << 32);
                var bin = new RpfBinaryFileEntry
                {
                    NameOffset = (uint)(buf & 0xFFFF),
                    FileSize = (uint)((buf >> 16) & 0xFFFFFF),
                    FileOffset = (uint)((buf >> 40) & 0xFFFFFF),
                    FileUncompressedSize = BitConverter.ToUInt32(_toc, off + 8),
                    IsEncrypted = BitConverter.ToUInt32(_toc, off + 12) == 1
                };
                entry = bin;
            }
            else
            {
                var res = new RpfResourceFileEntry
                {
                    NameOffset = (uint)BitConverter.ToUInt16(_toc, off),
                    FileSize = (uint)(_toc[off + 2] | (_toc[off + 3] << 8) | (_toc[off + 4] << 16)),
                    FileOffset = (uint)(_toc[off + 5] | (_toc[off + 6] << 8) | (_toc[off + 7] << 16)) & 0x7FFFFF,
                    SystemFlags = BitConverter.ToUInt32(_toc, off + 8),
                    GraphicsFlags = BitConverter.ToUInt32(_toc, off + 12)
                };
                entry = res;
            }

            entry.Index = (int)i;
            entry.File = this;
            entry.Name = ReadName(entry.NameOffset);
            entry.NameLower = entry.Name.ToLowerInvariant();
            _entries.Add(entry);
        }
    }

    private void BuildTree()
    {
        var stack = new Stack<RpfDirectoryEntry>();
        stack.Push(Root);
        Root.Path = string.Empty;
        Root.Name = string.Empty;

        while (stack.Count > 0)
        {
            var dir = stack.Pop();
            long start = dir.EntriesIndex;
            long end = start + dir.EntriesCount;
            if (start < 0 || end > _entries.Count)
                throw new InvalidDataException(
                    $"RPF7: '{dir.Path}' dizin aralığı geçersiz ({start}..{end}/{_entries.Count}).");

            for (long i = start; i < end; i++)
            {
                var e = _entries[(int)i];
                e.Parent = dir;
                if (e is RpfDirectoryEntry sub)
                {
                    sub.Path = dir.Path.Length == 0 ? sub.NameLower : dir.Path + "\\" + sub.NameLower;
                    dir.Directories.Add(sub);
                    stack.Push(sub);
                }
                else
                {
                    e.Path = dir.Path.Length == 0 ? e.NameLower : dir.Path + "\\" + e.NameLower;
                    dir.Files.Add((RpfFileEntry)e);
                }
            }
        }
    }

    // ------------------------------------------------------------ Query

    public RpfFileEntry? Find(string path)
    {
        var parts = path.Replace('/', '\\').ToLowerInvariant()
            .Split('\\', StringSplitOptions.RemoveEmptyEntries);
        var dir = Root;
        for (int i = 0; i < parts.Length; i++)
        {
            bool last = i == parts.Length - 1;
            if (last)
            {
                foreach (var f in dir.Files)
                    if (f.NameLower == parts[i]) return f;
                return null;
            }
            RpfDirectoryEntry? next = null;
            foreach (var d in dir.Directories)
                if (d.NameLower == parts[i]) { next = d; break; }
            if (next == null) return null;
            dir = next;
        }
        return null;
    }

    public IEnumerable<RpfFileEntry> AllFiles()
    {
        foreach (var e in _entries)
            if (e is RpfFileEntry f) yield return f;
    }

    // ------------------------------------------------------------- Read

    public byte[] ReadFile(RpfFileEntry entry)
    {
        byte[] stored = ReadStoredBytes(entry);

        if (entry is RpfBinaryFileEntry bin)
        {
            if (bin.IsEncrypted)
            {
                if (Encryption != RpfEncryption.Aes)
                    throw new NotSupportedException(
                        $"'{entry.Path}' NG şifreli (OpenIV'nin dokunmadığı orijinal dosyalarda görülür). " +
                        "Bu dosya şu an düzenlenemez.");
                if (_aesKey == null)
                    throw new AesKeyRequiredException(Path);
                stored = RpfCrypto.DecryptAes(stored, _aesKey);
            }

            uint un = bin.FileUncompressedSize;
            // RPF7 kuralı: FileSize = 0 => ham veri (gerçek boyut un'da);
            // FileSize > 0 && un > 0 && un != FileSize => sıkıştırılmış.
            bool compressed = bin.FileSize != 0 && un > 0 && un != bin.FileSize;
            if (compressed)
            {
                var inflated = RpfCrypto.Inflate(stored, (int)Math.Min(un, int.MaxValue), out var mode);
                bin.Compression = mode;
                return inflated;
            }

            bin.Compression = RpfCompression.None;
            return stored;
        }

        return stored;
    }

    /// <summary>Ham kayıtlı baytları döndürür (şifre çözme/açma yapmaz).</summary>
    public byte[] ReadStoredBytes(RpfFileEntry entry)
    {
        long dataLen = entry is RpfBinaryFileEntry b && b.FileSize == 0
            ? b.FileUncompressedSize
            : entry.FileSize;
        var buf = new byte[dataLen];
        if (buf.Length == 0) return buf;
        _fs.Position = (long)entry.FileOffset * 512;
        _fs.ReadExactly(buf, 0, buf.Length);
        return buf;
    }

    private byte[] ReadRaw(RpfFileEntry entry) => ReadStoredBytes(entry);

    private byte[]? _aesKey;
    public void SetAesKey(byte[] key) => _aesKey = key;

    // ------------------------------------------------------------ Write

    /// <summary>
    /// Binary dosya içeriğini değiştirir. Sıkıştırma davranışı varsayılan olarak
    /// dosyanın özgün formatını korur (sıkıştırılmışsa aynı formatta sıkıştırır).
    /// Yeni içerik eskisinden küçük/eşitse yerinde yazar, büyükse arşivin sonuna
    /// (512 bayt hizalı) ekler ve TOC girdisini günceller.
    /// </summary>
    /// <param name="compress">null: özgün formatı koru; true: sıkıştır; false: ham yaz.</param>
    public void ReplaceFile(RpfFileEntry entry, byte[] content, bool verify = true, bool? compress = null)
    {
        if (entry.File is not RpfArchive owner || !ReferenceEquals(owner, this))
            throw new InvalidOperationException("Girdi bu arşive ait değil.");
        if (entry is not RpfBinaryFileEntry bin0)
            throw new InvalidOperationException(
                "Yalnızca binary (RSC olmayan) dosyalar değiştirilebilir.");

        if (bin0.IsEncrypted && Encryption != RpfEncryption.Aes)
            throw new NotSupportedException(
                $"'{entry.Path}' NG şifreli (OpenIV'nin dokunmadığı orijinal dosyalarda görülür); " +
                "güvenlik için bu dosya düzenlenmez.");
        if (bin0.IsEncrypted)
            throw new NotSupportedException(
                $"'{entry.Path}' AES şifreli; bu sürümde şifreli dosya yazımı desteklenmiyor. " +
                "Dosyayı OpenIV ile şifresiz olarak kaydedebilirsiniz.");

        if (bin0.Compression == RpfCompression.Unknown &&
            bin0.FileUncompressedSize > 0 && bin0.FileUncompressedSize != bin0.FileSize)
        {
            // Sıkıştırma formatını öğren (aynı zamanda okunabilirliği doğrular;
            // hata fırlatırsa yazma yapılmaz).
            ReadFile(bin0);
        }

        bool shouldCompress = compress ??
            (bin0.Compression is RpfCompression.Raw or RpfCompression.Zlib);
        var mode = bin0.Compression is RpfCompression.Raw or RpfCompression.Zlib
            ? bin0.Compression
            : RpfCompression.Raw;

        byte[] stored = content;
        bool isCompressed = false;
        if (shouldCompress)
        {
            var compressedBytes = RpfCrypto.Compress(content, mode);
            if (compressedBytes.Length < content.Length)
            {
                stored = compressedBytes;
                isCompressed = true;
            }
        }

        if (stored.Length > 0xFFFFFF)
            throw new InvalidOperationException(
                "RPF7: tek girdi için depolanan veri 16 MB sınırını aşıyor.");

        uint oldSize = bin0.FileSize == 0 ? bin0.FileUncompressedSize : bin0.FileSize;
        long oldPos = (long)bin0.FileOffset * 512;

        if (stored.Length <= oldSize)
        {
            _fs.Position = oldPos;
            _fs.Write(stored, 0, stored.Length);
            WriteZeros(oldSize - stored.Length);
        }
        else
        {
            long newPos = (_fs.Length + 511) / 512 * 512;
            long sector = newPos / 512;
            if (sector > 0x7FFFFF)
                throw new InvalidOperationException(
                    "RPF7: arşiv 4 GB veri sınırını aşıyor; ekleme yapılamaz.");

            _fs.Position = newPos;
            _fs.Write(stored, 0, stored.Length);
            long tail = newPos + stored.Length;
            long aligned = (tail + 511) / 512 * 512;
            WriteZeros(aligned - tail);
            bin0.FileOffset = (uint)sector;
        }

        // RPF7 kuralı: ham yazımda FileSize = 0, gerçek boyut un'da tutulur.
        bin0.FileSize = isCompressed ? (uint)stored.Length : 0;
        bin0.FileUncompressedSize = (uint)content.Length;
        bin0.IsEncrypted = false;

        PatchEntryBytes(bin0.Index);
        _fs.Flush(true);

        if (verify)
        {
            using var check = Open(Path, _aesKey);
            var re = check.Find(entry.Path) as RpfBinaryFileEntry
                ?? throw new RpfVerificationException($"Doğrulama: '{entry.Path}' bulunamadı.");
            var readBack = check.ReadFile(re);
            if (!readBack.AsSpan().SequenceEqual(content))
                throw new RpfVerificationException(
                    $"Doğrulama başarısız: '{entry.Path}' içeriği yazdıktan sonra eşleşmiyor.");
        }
    }

    private void WriteZeros(long count)
    {
        if (count <= 0) return;
        var zeros = new byte[(int)Math.Min(count, 1 << 20)];
        while (count > 0)
        {
            int chunk = (int)Math.Min(count, zeros.Length);
            _fs.Write(zeros, 0, chunk);
            count -= chunk;
        }
    }

    private void PatchEntryBytes(int index)
    {
        var entry = _entries[index];
        int off = index * EntrySize;

        if (entry is RpfDirectoryEntry)
            throw new InvalidOperationException("Dizin girdileri değiştirilemez.");

        if (entry is RpfBinaryFileEntry bin)
        {
            ulong buf = (ulong)(bin.NameOffset & 0xFFFF)
                      | ((ulong)(bin.FileSize & 0xFFFFFF) << 16)
                      | ((ulong)(bin.FileOffset & 0xFFFFFF) << 40);
            BitConverter.TryWriteBytes(_toc.AsSpan(off, 4), (uint)buf);
            BitConverter.TryWriteBytes(_toc.AsSpan(off + 4, 4), (uint)(buf >> 32));
            BitConverter.TryWriteBytes(_toc.AsSpan(off + 8, 4), bin.FileUncompressedSize);
            BitConverter.TryWriteBytes(_toc.AsSpan(off + 12, 4), bin.IsEncrypted ? 1u : 0u);
        }
        else if (entry is RpfResourceFileEntry res)
        {
            ushort name = (ushort)(res.NameOffset & 0xFFFF);
            uint size = Math.Min(res.FileSize, 0xFFFFFF);
            uint foff = res.FileOffset & 0x7FFFFF;
            _toc[off] = (byte)(name & 0xFF);
            _toc[off + 1] = (byte)(name >> 8);
            _toc[off + 2] = (byte)(size & 0xFF);
            _toc[off + 3] = (byte)((size >> 8) & 0xFF);
            _toc[off + 4] = (byte)((size >> 16) & 0xFF);
            _toc[off + 5] = (byte)(foff & 0xFF);
            _toc[off + 6] = (byte)((foff >> 8) & 0xFF);
            _toc[off + 7] = (byte)(((foff >> 16) & 0x7F) | 0x80);
            BitConverter.TryWriteBytes(_toc.AsSpan(off + 8, 4), res.SystemFlags);
            BitConverter.TryWriteBytes(_toc.AsSpan(off + 12, 4), res.GraphicsFlags);
        }

        byte[] tocOut = Encryption switch
        {
            RpfEncryption.Aes => RpfCrypto.EncryptAes(_toc, _aesKey!),
            _ => _toc
        };

        _fs.Position = 16;
        _fs.Write(tocOut, 0, tocOut.Length);
    }

    // ------------------------------------------------------------ Verify

    public RpfVerifyResult VerifyStructure(bool sampleReads = true)
    {
        var result = new RpfVerifyResult();
        result.Encryption = Encryption;
        result.EntryCount = EntryCount;
        result.NamesLength = NamesLength;

        // isimler ve aralıklar
        foreach (var e in _entries)
        {
            if (e.NameOffset >= NamesLength) result.InvalidNameOffsets++;
            if (e is RpfDirectoryEntry d)
            {
                if (d.EntriesIndex + d.EntriesCount > EntryCount) result.InvalidDirRanges++;
            }
            else if (e is RpfFileEntry f)
            {
                result.FileCount++;
                long end = (long)f.FileOffset * 512 + f.FileSize;
                if (end > _fs.Length) result.OutOfBoundsFiles++;
                if (f is RpfBinaryFileEntry b && b.FileUncompressedSize > 0 &&
                    b.FileUncompressedSize < b.FileSize)
                    result.SuspiciousSizes++;
            }
        }

        // örnek okumalar: ilk 8 sıkıştırılmış ve ilk 4 ham dosya
        if (sampleReads)
        {
            int compressed = 0, raw = 0;
            foreach (var f in AllFiles())
            {
                bool isCompressed = f is RpfBinaryFileEntry bb && bb.FileSize > 0 && bb.FileUncompressedSize > bb.FileSize;
                if (isCompressed && compressed < 8)
                {
                    compressed++;
                    Classify(result, f);
                }
                else if (!isCompressed && raw < 4)
                {
                    raw++;
                    Classify(result, f);
                }
                if (compressed >= 8 && raw >= 4) break;
            }
        }

        return result;
    }

    private void Classify(RpfVerifyResult result, RpfFileEntry f)
    {
        try
        {
            ReadFile(f);
            result.SampleReadsOk++;
        }
        catch (NotSupportedException) { result.EncryptedSkipped++; }
        catch (AesKeyRequiredException) { result.EncryptedSkipped++; }
        catch { result.SampleReadsFailed++; }
    }

    public void Dispose() => _fs.Dispose();
}

public sealed class AesKeyRequiredException(string path)
    : Exception($"'{path}' AES şifreli ve anahtar yüklü değil. " +
                "GTA5.exe içinden anahtar arama özelliğini kullanın veya dosyayı OpenIV ile OPEN formatında yeniden kaydedin.");

public sealed class RpfVerificationException(string message) : Exception(message);

public sealed class RpfVerifyResult
{
    public RpfEncryption Encryption;
    public uint EntryCount;
    public uint NamesLength;
    public int FileCount;
    public int InvalidNameOffsets;
    public int InvalidDirRanges;
    public int OutOfBoundsFiles;
    public int SuspiciousSizes;
    public int SampleReadsOk;
    public int SampleReadsFailed;
    public int EncryptedSkipped;

    public bool IsHealthy =>
        InvalidNameOffsets == 0 && InvalidDirRanges == 0 &&
        OutOfBoundsFiles == 0 && SampleReadsFailed == 0;

    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Şifreleme     : {Encryption}");
        sb.AppendLine($"Girdi sayısı  : {EntryCount} (dosya: {FileCount})");
        sb.AppendLine($"İsim tablosu  : {NamesLength} bayt");
        sb.AppendLine($"Geçersiz isim : {InvalidNameOffsets}");
        sb.AppendLine($"Geçersiz dizin: {InvalidDirRanges}");
        sb.AppendLine($"Sınır dışı dosya: {OutOfBoundsFiles}");
        sb.AppendLine($"Şüpheli boyut : {SuspiciousSizes}");
        sb.AppendLine($"Örnek okuma   : {SampleReadsOk} başarılı, {SampleReadsFailed} başarısız, " +
                      $"{EncryptedSkipped} şifreli-atlandı");
        sb.AppendLine($"Sağlık        : {(IsHealthy ? "SAĞLIKLI" : "SORUN VAR")}");
        return sb.ToString();
    }
}
