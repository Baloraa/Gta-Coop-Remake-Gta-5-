using System.Text;

namespace GTACoop.Rpf;

/// <summary>
/// GTA V RPF7 arşivleri için okuma/yazma kütüphanesi.
/// Yalnızca mods klasöründeki arşivlerde güvenli değişiklik yapmak için tasarlandı:
/// - Dosya içeriği değiştirme (in-place veya sona ekleme)
/// - Yeni dosya ekleme / kaldırma; TOC/isim tablosu büyürken yalnızca
///   başlığın önündeki ilk veri blokları arşivin sonuna taşınır. Böylece
///   arşivin tamamı kopyalanmaz; yalnızca değişen dosya verisi ve küçük
///   TOC yazılır.
/// - RSC7 resource dosyaları (ytd, yft, ymap vb.) ve şifreli binary girdiler.
/// </summary>
public sealed class RpfArchive : IDisposable
{
    public const uint Magic = 0x52504637;
    public const int EntrySize = 16;
    public const uint DirectoryIdent = 0x7FFFFF00;
    public const int MaxFileStoredBytes = 0xFFFFFF;
    public const int MaxNameOffset = 0xFFFF;

    private readonly FileStream _fs;
    private byte[] _toc;
    private byte[] _names;
    private readonly List<RpfEntry> _entries = new();

    public string Path { get; }
    public uint EntryCount { get; private set; }
    public uint NamesLength { get; private set; }
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
        _names = names;

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

    /// <summary>
    /// Yeni, boş bir RPF7 arşivi oluşturur. <paramref name="headerReserve"/>
    /// baytlık başlangıç alanı TOC/isim tablosunun büyümesi için ayrılır;
    /// böylece çok sayıda dosya eklerken veri blokları yeniden taşınmaz.
    /// </summary>
    public static RpfArchive Create(string path, bool overwrite = false,
        int headerReserve = 512)
    {
        if (headerReserve < 512) headerReserve = 512;
        headerReserve = (headerReserve + 511) / 512 * 512;

        var dir = System.IO.Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        using (var fs = new FileStream(path,
                   overwrite ? FileMode.Create : FileMode.CreateNew,
                   FileAccess.Write, FileShare.None))
        {
            var header = new byte[headerReserve];
            BitConverter.TryWriteBytes(header.AsSpan(0, 4), Magic);
            BitConverter.TryWriteBytes(header.AsSpan(4, 4), 1u);   // yalnızca kök dizin
            BitConverter.TryWriteBytes(header.AsSpan(8, 4), 16u);  // 16 baytlık boş isim tablosu
            BitConverter.TryWriteBytes(header.AsSpan(12, 4), (uint)RpfEncryption.Open);

            // Kök dizin girdisi
            BitConverter.TryWriteBytes(header.AsSpan(16, 4), 0u);  // isim ofseti
            BitConverter.TryWriteBytes(header.AsSpan(20, 4), DirectoryIdent);
            BitConverter.TryWriteBytes(header.AsSpan(24, 4), 1u);  // ilk alt girdi
            BitConverter.TryWriteBytes(header.AsSpan(28, 4), 0u);  // alt girdi sayısı

            fs.Write(header, 0, header.Length);
            fs.Flush(true);
        }

        return Open(path);
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
                // 16 MB üstü resource dosyalarında gerçek boyut 16 baytlık
                // RSC7 başlığının belirli baytlarında saklanır (CodeWalker kuralı).
                if (res.FileSize == 0xFFFFFF)
                {
                    long pos = (long)res.FileOffset * 512;
                    if (pos + 16 <= _fs.Length)
                    {
                        var hb = new byte[16];
                        _fs.Position = pos;
                        _fs.ReadExactly(hb, 0, 16);
                        res.FileSize = ((uint)hb[7])
                                     | ((uint)hb[14] << 8)
                                     | ((uint)hb[5] << 16)
                                     | ((uint)hb[2] << 24);
                    }
                }
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
        if (entry is RpfResourceFileEntry res)
            return ReadResourceFile(res);

        if (entry is not RpfBinaryFileEntry bin)
            throw new InvalidOperationException("Bilinmeyen RPF girdi türü.");

        byte[] stored = ReadStoredBytes(bin);

        if (bin.IsEncrypted)
        {
            if (Encryption != RpfEncryption.Aes)
                throw new NotSupportedException(
                    $"'{entry.Path}' NG/opak şifreli (OpenIV'nin dokunmadığı orijinal dosyalarda görülür). " +
                    "İçerik okunamaz; dosya yine de güvenli biçimde değiştirilebilir.");
            if (_aesKey == null)
                throw new AesKeyRequiredException(Path);

            stored = RpfCrypto.DecryptAes(stored, _aesKey);
            int actual = (int)(bin.FileSize == 0 ? bin.FileUncompressedSize : bin.FileSize);
            if (actual >= 0 && actual < stored.Length)
                Array.Resize(ref stored, actual);
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

    private byte[] ReadResourceFile(RpfResourceFileEntry res)
    {
        var stored = ReadStoredBytes(res);
        if (stored.Length == 0)
            return RpfCrypto.BuildResourceHeader(res.SystemFlags, res.GraphicsFlags);

        if (res.IsEncrypted)
        {
            if (Encryption != RpfEncryption.Aes || _aesKey == null)
                throw new NotSupportedException(
                    $"'{res.Path}' şifreli resource dosyası okunamıyor.");
            int headerLen = Math.Min(16, stored.Length);
            var encPart = new byte[(stored.Length - headerLen + 15) / 16 * 16];
            Array.Copy(stored, headerLen, encPart, 0, stored.Length - headerLen);
            var decPart = RpfCrypto.DecryptAes(encPart, _aesKey);
            var merged = new byte[stored.Length];
            Array.Copy(stored, 0, merged, 0, headerLen);
            Array.Copy(decPart, 0, merged, headerLen, stored.Length - headerLen);
            stored = merged;
        }

        // RSC7 kaynakları diskte 16 baytlık başlık + DEFLATE yükü olarak tutulur.
        // OpenIV'nin dışa aktardığı biçim de budur; ham veriyi döndürüyoruz.
        return stored;
    }

    /// <summary>Ham kayıtlı baytları döndürür (şifre çözme/açma yapmaz).</summary>
    public byte[] ReadStoredBytes(RpfFileEntry entry)
    {
        long dataLen;
        if (entry is RpfBinaryFileEntry b)
        {
            dataLen = b.FileSize == 0 ? b.FileUncompressedSize : b.FileSize;
            if (b.IsEncrypted) dataLen = Align16(dataLen);
        }
        else
        {
            dataLen = entry.FileSize;
        }

        if (dataLen <= 0) return Array.Empty<byte>();
        if (dataLen > int.MaxValue)
            throw new InvalidOperationException("RPF7: girdi boyutu belleğe sığmıyor.");

        var buf = new byte[dataLen];
        _fs.Position = (long)entry.FileOffset * 512;
        _fs.ReadExactly(buf, 0, buf.Length);
        return buf;
    }

    private byte[]? _aesKey;
    public void SetAesKey(byte[] key) => _aesKey = key;

    /// <summary>Girdi içeriği bu arşivde okunabilir mi? (NG/opak şifreli dosyalar hayır)</summary>
    public bool CanReadContent(RpfFileEntry entry)
    {
        if (!entry.IsEncrypted) return true;
        if (entry is not RpfBinaryFileEntry) return false;
        return Encryption == RpfEncryption.Aes && _aesKey != null;
    }

    // ------------------------------------------------------------ Write

    /// <summary>
    /// Bir dosyayı ekler veya (varsa) değiştirir. İçerik "RSC7" ile başlıyorsa
    /// resource (ytd/yft/ymap) olarak, aksi halde binary olarak yazılır.
    /// </summary>
    public RpfFileEntry SetFile(string path, byte[] content, bool verify = true,
        bool? compress = null, bool replaceOnly = false, bool appendOnly = false)
    {
        if (content == null) throw new ArgumentNullException(nameof(content));
        var normalized = NormalizeRpfPath(path);
        bool isResource = RpfCrypto.HasResourceHeader(content);

        var existing = Find(normalized);
        if (existing != null)
        {
            if (isResource && existing is RpfResourceFileEntry res)
            {
                ReplaceResourceFile(res, content, verify, appendOnly);
                return res;
            }
            if (!isResource && existing is RpfBinaryFileEntry bin)
            {
                ReplaceFile(bin, content, verify, compress, appendOnly: appendOnly);
                return bin;
            }
            throw new InvalidOperationException(
                $"RPF7: '{normalized}' girdi türü uyuşmuyor " +
                $"({(existing is RpfResourceFileEntry ? "resource" : "binary")} -> " +
                $"{(isResource ? "resource" : "binary")}).");
        }

        if (replaceOnly)
            throw new FileNotFoundException($"RPF7: değiştirilecek girdi bulunamadı: {normalized}");

        return isResource
            ? AddResourceFile(normalized, content, verify)
            : AddBinaryFile(normalized, content, verify, compress);
    }

    /// <summary>Yeni binary dosya ekler (dizinler gerekirse oluşturulur).</summary>
    public RpfBinaryFileEntry AddBinaryFile(string path, byte[] content, bool verify = true,
        bool? compress = null)
    {
        var normalized = NormalizeRpfPath(path);
        if (Find(normalized) != null)
            throw new InvalidOperationException($"RPF7: girdi zaten var: {normalized}");

        bool shouldCompress = compress ?? content.Length > 1024;
        byte[] stored = content;
        bool isCompressed = false;
        if (shouldCompress)
        {
            var compressedBytes = RpfCrypto.Compress(content, RpfCompression.Raw);
            if (compressedBytes.Length < content.Length)
            {
                stored = compressedBytes;
                isCompressed = true;
            }
        }
        if (stored.Length > MaxFileStoredBytes)
        {
            // 16 MB üstü sıkıştırılmış veri 24 bit boyut alanına sığmaz;
            // CodeWalker gibi ham (FileSize=0) olarak saklanır.
            stored = content;
            isCompressed = false;
        }

        var parts = SplitRpfPath(normalized);
        var dirNames = CollectMissingDirectories(parts);
        long nameOffset = PreflightAdd(dirNames, parts[^1], stored.Length);

        var parent = EnsureDirectories(parts, dirNames);
        long sector = AppendNewData(stored);

        var entry = new RpfBinaryFileEntry
        {
            File = this,
            Parent = parent,
            Name = parts[^1],
            NameLower = parts[^1].ToLowerInvariant(),
            Path = parent.Path.Length == 0 ? parts[^1].ToLowerInvariant()
                : parent.Path + "\\" + parts[^1].ToLowerInvariant(),
            NameOffset = (uint)nameOffset,
            FileOffset = (uint)sector,
            FileSize = isCompressed ? (uint)stored.Length : 0,
            FileUncompressedSize = (uint)content.Length,
            IsEncrypted = false,
            Compression = isCompressed ? RpfCompression.Raw : RpfCompression.None
        };

        parent.Files.Add(entry);
        _entries.Add(entry);
        RebuildEntries();
        WriteHeader();

        if (verify) VerifyWritten(normalized, content);
        return entry;
    }

    /// <summary>Yeni RSC7 resource dosyası ekler (16 baytlık başlık korunur).</summary>
    public RpfResourceFileEntry AddResourceFile(string path, byte[] content, bool verify = true)
    {
        var normalized = NormalizeRpfPath(path);
        if (Find(normalized) != null)
            throw new InvalidOperationException($"RPF7: girdi zaten var: {normalized}");
        if (!RpfCrypto.HasResourceHeader(content))
            throw new InvalidOperationException("RPF7: resource içeriği RSC7 başlığı ile başlamalı.");

        var (systemFlags, graphicsFlags, header, compressed) = PrepareResourcePayload(content);
        var stored = new byte[header.Length + compressed.Length];
        Buffer.BlockCopy(header, 0, stored, 0, header.Length);
        Buffer.BlockCopy(compressed, 0, stored, header.Length, compressed.Length);
        EncodeResourceSize(stored);

        var parts = SplitRpfPath(normalized);
        var dirNames = CollectMissingDirectories(parts);
        long nameOffset = PreflightAdd(dirNames, parts[^1], stored.Length);

        var parent = EnsureDirectories(parts, dirNames);
        long sector = AppendNewData(stored);

        var entry = new RpfResourceFileEntry
        {
            File = this,
            Parent = parent,
            Name = parts[^1],
            NameLower = parts[^1].ToLowerInvariant(),
            Path = parent.Path.Length == 0 ? parts[^1].ToLowerInvariant()
                : parent.Path + "\\" + parts[^1].ToLowerInvariant(),
            NameOffset = (uint)nameOffset,
            FileOffset = (uint)sector,
            FileSize = (uint)stored.Length,
            SystemFlags = systemFlags,
            GraphicsFlags = graphicsFlags,
            IsEncrypted = false
        };

        parent.Files.Add(entry);
        _entries.Add(entry);
        RebuildEntries();
        WriteHeader();

        if (verify) VerifyWritten(normalized, content);
        return entry;
    }

    /// <summary>Girdiyi arşivden kaldırır (yalnızca TOC/isim tablosu; veri baytları kalabilir).</summary>
    public void RemoveFile(RpfFileEntry entry, bool verify = true)
    {
        if (entry.File is not RpfArchive owner || !ReferenceEquals(owner, this))
            throw new InvalidOperationException("Girdi bu arşive ait değil.");
        if (entry.Parent == null)
            throw new InvalidOperationException("Girdinin üst dizini yok.");

        var path = entry.Path;
        var parent = entry.Parent;
        parent.Files.Remove(entry);

        // Boş kalan (yalnızca bizim eklediğimiz) dizinleri de temizle; böylece
        // kurulum/kaldırma döngüleri başlık alanını gereksiz yere doldurmaz.
        var dir = parent;
        while (dir != null && dir != Root &&
               dir.Files.Count == 0 && dir.Directories.Count == 0)
        {
            var up = dir.Parent;
            up?.Directories.Remove(dir);
            dir = up;
        }

        // İsim tablosunu sıkıştır: kaldırılan girdilerin adları artık yer kaplamaz.
        RebuildEntries();
        RebuildNamesTable();
        for (int i = 0; i < _entries.Count; i++)
            PatchEntryBytesInMemory(i);
        WriteHeader();

        if (verify)
        {
            using var check = Open(Path, _aesKey);
            if (check.Find(path) != null)
                throw new RpfVerificationException(
                    $"Doğrulama başarısız: '{path}' kaldırıldıktan sonra hâlâ görünüyor.");
        }
    }

    /// <summary>
    /// Binary dosya içeriğini değiştirir. Sıkıştırma davranışı varsayılan olarak
    /// dosyanın özgün formatını korur (sıkıştırılmışsa aynı formatta sıkıştırır).
    /// Yeni içerik eskisinden küçük/eşitse yerinde yazar, büyükse arşivin sonuna
    /// (512 bayt hizalı) ekler ve TOC girdisini günceller.
    /// </summary>
    /// <param name="compress">null: özgün formatı koru; true: sıkıştır; false: ham yaz.</param>
    /// <param name="requireInPlace">
    /// true ise yeni içerik mevcut dosya alanına sığmazsa arşiv büyütülmez; hata verilir.
    /// 4 GB'a yakın update.rpf dosyalarında güvenli düzenleme için kullanılır.
    /// </param>
    /// <param name="appendOnly">
    /// true ise eski veri bloğu asla üzerine yazılmaz; yeni içerik arşivin sonuna
    /// eklenir. Geri alma için eski TOC konumu yeterli olur, içerik yedeği gerekmez.
    /// </param>
    public void ReplaceFile(RpfFileEntry entry, byte[] content, bool verify = true, bool? compress = null,
        bool requireInPlace = false, bool appendOnly = false)
    {
        if (entry.File is not RpfArchive owner || !ReferenceEquals(owner, this))
            throw new InvalidOperationException("Girdi bu arşive ait değil.");
        if (entry is not RpfBinaryFileEntry bin0)
            throw new InvalidOperationException(
                "Yalnızca binary (RSC olmayan) dosyalar bu yolla değiştirilebilir.");

        bool readable = CanReadContent(bin0);
        bool opaque = bin0.IsEncrypted && !readable;

        if (!opaque)
        {
            if (bin0.Compression == RpfCompression.Unknown &&
                bin0.FileUncompressedSize > 0 && bin0.FileUncompressedSize != bin0.FileSize)
            {
                // Sıkıştırma formatını öğren (aynı zamanda okunabilirliği doğrular;
                // hata fırlatırsa yazma yapılmaz).
                ReadFile(bin0);
            }

            // İçerik zaten aynıysa hiç yazma: gereksiz dosya büyümesini ve TOC değişimini önler.
            try
            {
                if (ReadFile(bin0).AsSpan().SequenceEqual(content))
                    return;
            }
            catch
            {
                // Karşılaştırma yapılamazsa normal yazma akışına devam edilir.
            }
        }

        if (opaque && requireInPlace)
            throw new InvalidOperationException(
                $"RPF7: '{entry.Path}' opak şifreli; yerinde yazma güvenli değil.");

        bool shouldCompress = compress ?? (!opaque &&
            bin0.Compression is RpfCompression.Raw or RpfCompression.Zlib);
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

        if (stored.Length > MaxFileStoredBytes)
        {
            stored = content;
            isCompressed = false;
        }

        var prior = CaptureState(bin0);
        byte[]? priorContent = readable && !appendOnly ? ReadFile(bin0) : null;

        try
        {
            WriteEntryData(bin0, stored, requireInPlace,
                preserveOldBlock: opaque || appendOnly);

            // RPF7 kuralı: ham yazımda FileSize = 0, gerçek boyut un'da tutulur.
            bin0.FileSize = isCompressed ? (uint)stored.Length : 0;
            bin0.FileUncompressedSize = (uint)content.Length;
            bin0.IsEncrypted = false;
            bin0.Compression = isCompressed ? mode : RpfCompression.None;

            PatchEntryBytes(bin0.Index);
            WriteHeader();

            if (verify) VerifyWritten(entry.Path, content);
        }
        catch
        {
            try
            {
                RestoreState(entry.Path, prior);
                if (priorContent != null)
                {
                    var current = Find(entry.Path);
                    if (current is RpfBinaryFileEntry b2)
                        ReplaceFile(b2, priorContent, verify: false, compress: false);
                }
            }
            catch { }
            throw;
        }
    }

    /// <summary>Mevcut RSC7 resource girdisini yeni içerikle değiştirir.</summary>
    public void ReplaceResourceFile(RpfResourceFileEntry res, byte[] content, bool verify = true,
        bool appendOnly = false)
    {
        if (res.File is not RpfArchive owner || !ReferenceEquals(owner, this))
            throw new InvalidOperationException("Girdi bu arşive ait değil.");
        if (!RpfCrypto.HasResourceHeader(content))
            throw new InvalidOperationException("RPF7: resource içeriği RSC7 başlığı ile başlamalı.");
        if (res.IsEncrypted)
            throw new NotSupportedException(
                $"'{res.Path}' şifreli resource; güvenlik için değiştirilmiyor.");

        var (systemFlags, graphicsFlags, header, compressed) = PrepareResourcePayload(content);
        var prior = CaptureState(res);
        byte[] priorContent;
        try { priorContent = appendOnly ? Array.Empty<byte>() : ReadFile(res); }
        catch { priorContent = Array.Empty<byte>(); }

        var stored = new byte[header.Length + compressed.Length];
        Buffer.BlockCopy(header, 0, stored, 0, header.Length);
        Buffer.BlockCopy(compressed, 0, stored, header.Length, compressed.Length);
        EncodeResourceSize(stored);

        try
        {
            WriteEntryData(res, stored, requireInPlace: false, preserveOldBlock: appendOnly);
            res.FileSize = (uint)stored.Length;
            res.SystemFlags = systemFlags;
            res.GraphicsFlags = graphicsFlags;
            res.IsEncrypted = false;

            PatchEntryBytes(res.Index);
            WriteHeader();

            if (verify) VerifyWritten(res.Path, content);
        }
        catch
        {
            try
            {
                RestoreState(res.Path, prior);
                var current = Find(res.Path);
                if (current is RpfResourceFileEntry r2 && priorContent.Length > 0)
                    ReplaceResourceFile(r2, priorContent, verify: false);
            }
            catch { }
            throw;
        }
    }

    /// <summary>
    /// İçerik arşivi büyütmeden mevcut dosya alanına sığar mı? Yalnızca okuma
    /// yapar; hiçbir bayt yazmaz. Büyük update.rpf dosyalarında dlclist.xml'i
    /// yerinde değiştirmenin güvenli olup olmadığını önceden anlamak için kullanılır.
    /// </summary>
    public bool CanReplaceInPlace(RpfFileEntry entry, byte[] content, out string reason,
        bool? compress = null)
    {
        reason = "";
        if (entry.File is not RpfArchive owner || !ReferenceEquals(owner, this))
        {
            reason = "Girdi bu arşive ait değil.";
            return false;
        }
        if (entry is not RpfBinaryFileEntry bin0)
        {
            reason = "Yalnızca binary (RSC olmayan) dosyalar değiştirilebilir.";
            return false;
        }
        if (bin0.IsEncrypted && !CanReadContent(bin0))
        {
            reason = "Opak şifreli dosya yerinde değiştirilemez.";
            return false;
        }

        if (bin0.Compression == RpfCompression.Unknown &&
            bin0.FileUncompressedSize > 0 && bin0.FileUncompressedSize != bin0.FileSize)
        {
            try { ReadFile(bin0); }
            catch (Exception ex)
            {
                reason = ex.Message;
                return false;
            }
        }

        bool shouldCompress = compress ??
            (bin0.Compression is RpfCompression.Raw or RpfCompression.Zlib);
        var mode = bin0.Compression is RpfCompression.Raw or RpfCompression.Zlib
            ? bin0.Compression
            : RpfCompression.Raw;

        byte[] stored = content;
        if (shouldCompress)
        {
            var compressedBytes = RpfCrypto.Compress(content, mode);
            if (compressedBytes.Length < content.Length) stored = compressedBytes;
        }

        if (stored.Length > MaxFileStoredBytes)
        {
            reason = "RPF7: tek girdi için depolanan veri 16 MB sınırını aşıyor.";
            return false;
        }

        long oldSize = StoredDataSize(bin0);
        long oldPos = (long)bin0.FileOffset * 512;
        long headerEnd = HeaderEnd(EntryCount, NamesLength);
        if (oldSize > 0 && (oldPos < headerEnd || oldPos + oldSize > _fs.Length))
        {
            reason = $"RPF7: '{entry.Path}' veri konumu arşiv sınırlarının dışında.";
            return false;
        }

        // İçerik zaten aynıysa yazma gerekmez.
        try
        {
            if (CanReadContent(bin0) && ReadFile(bin0).AsSpan().SequenceEqual(content))
                return true;
        }
        catch { /* normal alan kontrolüne devam */ }

        if (oldSize == 0) return false;

        long freeEnd = _fs.Length;
        bool collision = false;
        foreach (var other in AllFiles())
        {
            if (other.Index == bin0.Index) continue;
            long op = (long)other.FileOffset * 512;
            long olen = StoredDataSize(other);
            if (olen == 0 || op + olen <= oldPos) continue;

            if (op < oldPos + stored.Length)
            {
                collision = true;
                break;
            }
            if (op < freeEnd) freeEnd = op;
        }

        if (collision || stored.Length > freeEnd - oldPos)
        {
            reason = "mevcut alan yetersiz (yerinde yazma için boşluk yok).";
            return false;
        }

        return true;
    }

    // --------------------------------------------------- state / rollback

    /// <summary>Girdinin ham TOC durumunu (içeriksiz) döndürür.</summary>
    public RpfEntryState CaptureState(RpfFileEntry entry)
    {
        if (entry is RpfBinaryFileEntry bin)
        {
            return new RpfEntryState
            {
                Exists = true,
                IsResource = false,
                FileOffset = bin.FileOffset,
                FileSize = bin.FileSize,
                FileUncompressedSize = bin.FileUncompressedSize,
                IsEncrypted = bin.IsEncrypted,
                Compression = bin.Compression
            };
        }
        if (entry is RpfResourceFileEntry res)
        {
            return new RpfEntryState
            {
                Exists = true,
                IsResource = true,
                FileOffset = res.FileOffset,
                FileSize = res.FileSize,
                SystemFlags = res.SystemFlags,
                GraphicsFlags = res.GraphicsFlags,
                IsEncrypted = res.IsEncrypted
            };
        }
        throw new InvalidOperationException("Bilinmeyen RPF girdi türü.");
    }

    /// <summary>
    /// Daha önce alınan ham TOC durumunu geri yazar. İçerik okunamayan (NG/opak)
    /// girdilerde eski veri bloğuna yeniden işaret eder; veri bloğu korunmuş olmalıdır.
    /// </summary>
    public void RestoreState(string path, RpfEntryState state)
    {
        var normalized = NormalizeRpfPath(path);
        var entry = Find(normalized);

        if (!state.Exists)
        {
            if (entry != null) RemoveFile(entry);
            return;
        }

        if (entry == null)
            throw new InvalidOperationException(
                $"RPF7: geri alınacak girdi bulunamadı: {normalized}");

        if (entry is RpfBinaryFileEntry bin && !state.IsResource)
        {
            bin.FileOffset = state.FileOffset;
            bin.FileSize = state.FileSize;
            bin.FileUncompressedSize = state.FileUncompressedSize;
            bin.IsEncrypted = state.IsEncrypted;
            bin.Compression = state.Compression;
        }
        else if (entry is RpfResourceFileEntry res && state.IsResource)
        {
            res.FileOffset = state.FileOffset;
            res.FileSize = state.FileSize;
            res.SystemFlags = state.SystemFlags;
            res.GraphicsFlags = state.GraphicsFlags;
            res.IsEncrypted = state.IsEncrypted;
        }
        else
        {
            throw new InvalidOperationException(
                $"RPF7: geri alma türü uyuşmuyor: {normalized}");
        }

        PatchEntryBytes(entry.Index);
        WriteHeader();
    }

    // ------------------------------------------------------- write helpers

    private void WriteEntryData(RpfFileEntry entry, byte[] stored, bool requireInPlace,
        bool preserveOldBlock)
    {
        long oldSize = StoredDataSize(entry);
        long oldPos = (long)entry.FileOffset * 512;
        long headerEnd = HeaderEnd(EntryCount, NamesLength);
        if (oldSize > 0 && (oldPos < headerEnd || oldPos + oldSize > _fs.Length))
            throw new InvalidDataException(
                $"RPF7: '{entry.Path}' veri konumu arşiv sınırlarının dışında; yazma iptal edildi.");

        long freeEnd = _fs.Length;
        bool collision = false;
        if (oldSize > 0)
        {
            foreach (var other in AllFiles())
            {
                if (other.Index == entry.Index) continue;
                long op = (long)other.FileOffset * 512;
                long olen = StoredDataSize(other);
                if (olen == 0 || op + olen <= oldPos) continue;

                if (op < oldPos + stored.Length)
                {
                    collision = true;
                    break;
                }
                if (op < freeEnd) freeEnd = op;
            }
        }
        else
        {
            collision = true; // boş girdi: her zaman sona ekle
        }

        if (!collision && !preserveOldBlock && stored.Length <= freeEnd - oldPos)
        {
            long clear = Math.Min(freeEnd - oldPos, Math.Max(oldSize, (long)stored.Length));
            _fs.Position = oldPos;
            _fs.Write(stored, 0, stored.Length);
            WriteZeros(clear - stored.Length);
        }
        else
        {
            if (requireInPlace)
                throw new InvalidOperationException(
                    $"RPF7: '{entry.Path}' yeni içerik mevcut alana sığmıyor; " +
                    "arşiv büyütülmedi (yerinde yazma gerekli).");

            long newSector = AppendNewData(stored);
            entry.FileOffset = (uint)newSector;

            // Eski blok artık kullanılmıyor. Opak şifreli dosyalarda eski veri
            // geri alma için korunur; okunabilen dosyalarda temizlenir.
            if (!preserveOldBlock && oldSize > 0)
            {
                _fs.Position = oldPos;
                WriteZeros(oldSize);
            }
        }
    }

    /// <summary>Veriyi 512 bayt hizalı olarak arşivin sonuna ekler; sektör döndürür.</summary>
    private long AppendNewData(byte[] stored)
    {
        long newPos = (_fs.Length + 511) / 512 * 512;
        long sector = newPos / 512;
        long blocks = (stored.Length + 511) / 512;
        if (sector + blocks > 0x7FFFFF)
            throw new InvalidOperationException(
                "RPF7: arşiv 4 GB veri sınırını aşıyor; ekleme yapılamaz. " +
                "Lütfen Launcher > Yedekler bölümünden sağlıklı bir yedeğe dönün.");

        _fs.Position = newPos;
        if (stored.Length > 0) _fs.Write(stored, 0, stored.Length);
        long tail = newPos + stored.Length;
        long aligned = (tail + 511) / 512 * 512;
        WriteZeros(aligned - tail);
        return sector;
    }

    private long PreflightAdd(List<string> newDirNames, string fileName, int storedLength)
    {
        long namesLen = _names.Length;
        long fileNameOffset = 0;
        foreach (var name in newDirNames)
        {
            namesLen += Encoding.UTF8.GetByteCount(name) + 1;
        }
        fileNameOffset = namesLen;
        namesLen += Encoding.UTF8.GetByteCount(fileName) + 1;
        namesLen = Align16(namesLen);

        long newCount = EntryCount + newDirNames.Count + 1L;
        long headerEnd = HeaderEnd(newCount, namesLen);
        long firstData = FirstDataOffset();
        if (firstData <= 0) firstData = _fs.Length;
        if (headerEnd > firstData)
        {
            MakeHeaderRoom(headerEnd);
            firstData = FirstDataOffset();
            if (firstData <= 0) firstData = _fs.Length;
        }
        if (headerEnd > firstData)
            throw new RpfHeaderSpaceException(
                $"RPF7: '{fileName}' eklemek için başlık boşluğu yetersiz " +
                $"(gerekli {headerEnd:N0}, kullanılabilir {firstData:N0} bayt). " +
                "Arşiv büyütülmedi; başka bir dosya değiştirilebilir.");

        if (fileNameOffset > MaxNameOffset)
            throw new RpfHeaderSpaceException(
                $"RPF7: isim tablosu 16 bit sınırını aşıyor; '{fileName}' eklenemez.");

        long newPos = (_fs.Length + 511) / 512 * 512;
        long blocks = ((long)storedLength + 511) / 512;
        if (newPos / 512 + blocks > 0x7FFFFF)
            throw new InvalidOperationException(
                "RPF7: arşiv 4 GB veri sınırını aşıyor; ekleme yapılamaz.");

        return fileNameOffset;
    }

    /// <summary>
    /// Başlık (TOC + isim tablosu) büyürken ilk veri bloklarının üzerine
    /// taşarsa, taşan blokları arşivin sonuna taşır. Yalnızca birkaç küçük
    /// blok yeniden konumlanır; arşivin tamamı kopyalanmaz. Taşıma sırasında
    /// eski bloklar yerinde bırakılır: yeni başlık yazılmadan önce bir hata
    /// olursa eski TOC hâlâ eski veriyi gösterir ve arşiv bozulmaz.
    /// </summary>
    private void MakeHeaderRoom(long requiredEnd)
    {
        var blocks = new List<(long Pos, RpfFileEntry Entry)>();
        foreach (var f in AllFiles())
        {
            long size = StoredDataSize(f);
            if (size <= 0 || f.FileOffset == 0) continue;
            blocks.Add(((long)f.FileOffset * 512, f));
        }
        blocks.Sort((a, b) => a.Pos.CompareTo(b.Pos));

        foreach (var (pos, entry) in blocks)
        {
            if (pos >= requiredEnd) break;

            // Ham (şifreli/sıkıştırılmış) baytları olduğu gibi taşı; içerik
            // çözülmez, böylece opak girdiler de bozulmadan korunur.
            byte[] raw = ReadStoredBytes(entry);
            long sector = AppendNewData(raw);
            entry.FileOffset = (uint)sector;
            PatchEntryBytes(entry.Index);
        }

        long first = FirstDataOffset();
        if (first > 0 && requiredEnd > first)
            throw new RpfHeaderSpaceException(
                $"RPF7: başlık için yeterli alan açılamadı " +
                $"(gerekli {requiredEnd:N0}, kullanılabilir {first:N0} bayt).");
    }

    /// <summary>Yeni dosya için oluşturulması gereken ara dizin adlarını döndürür.</summary>
    private List<string> CollectMissingDirectories(string[] parts)
    {
        var result = new List<string>();
        var dir = Root;
        for (int i = 0; i < parts.Length - 1; i++)
        {
            var key = parts[i].ToLowerInvariant();
            RpfDirectoryEntry? next = null;
            foreach (var d in dir.Directories)
                if (d.NameLower == key) { next = d; break; }
            if (next == null)
            {
                result.Add(parts[i]);
                // Sanal olarak alt dizine geç: sonraki eksik adları da topla.
                var virtualDir = new RpfDirectoryEntry { NameLower = key };
                dir = virtualDir;
            }
            else
            {
                dir = next;
            }
        }
        return result;
    }

    private RpfDirectoryEntry EnsureDirectories(string[] parts, List<string> newDirNames)
    {
        var dir = Root;
        int newIndex = 0;
        for (int i = 0; i < parts.Length - 1; i++)
        {
            var key = parts[i].ToLowerInvariant();
            RpfDirectoryEntry? next = null;
            foreach (var d in dir.Directories)
                if (d.NameLower == key) { next = d; break; }

            if (next == null)
            {
                var name = newDirNames[newIndex++];
                next = new RpfDirectoryEntry
                {
                    File = this,
                    Parent = dir,
                    Name = name,
                    NameLower = key,
                    Path = dir.Path.Length == 0 ? key : dir.Path + "\\" + key,
                    NameOffset = AppendName(name)
                };
                dir.Directories.Add(next);
            }
            dir = next;
        }

        // Dosya adını da tabloya ekle (offset PreflightAdd ile hesaplananla aynı).
        _names = AppendNameRaw(_names, parts[^1]);
        return dir;
    }

    private uint AppendName(string name)
    {
        uint offset = (uint)_names.Length;
        _names = AppendNameRaw(_names, name);
        return offset;
    }

    private static byte[] AppendNameRaw(byte[] names, string name)
    {
        var bytes = Encoding.UTF8.GetBytes(name);
        var result = new byte[names.Length + bytes.Length + 1];
        Buffer.BlockCopy(names, 0, result, 0, names.Length);
        Buffer.BlockCopy(bytes, 0, result, names.Length, bytes.Length);
        return result;
    }

    /// <summary>Bir girişin TOC baytlarını bellekte (dosyaya yazmadan) günceller.</summary>
    private void PatchEntryBytes(int index)
    {
        var entry = _entries[index];
        int off = index * EntrySize;

        if (entry is RpfDirectoryEntry dir)
        {
            BitConverter.TryWriteBytes(_toc.AsSpan(off, 4), dir.NameOffset);
            BitConverter.TryWriteBytes(_toc.AsSpan(off + 4, 4), DirectoryIdent);
            BitConverter.TryWriteBytes(_toc.AsSpan(off + 8, 4), dir.EntriesIndex);
            BitConverter.TryWriteBytes(_toc.AsSpan(off + 12, 4), dir.EntriesCount);
            return;
        }

        if (entry is RpfBinaryFileEntry bin)
        {
            if (bin.NameOffset > MaxNameOffset)
                throw new RpfHeaderSpaceException(
                    $"RPF7: '{bin.Path}' isim ofseti 16 bit sınırını aşıyor.");
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

        // Şifreli TOC'lerde (AES) hemen yazılır; OPEN arşivlerde WriteHeader yazar.
        if (Encryption == RpfEncryption.Aes)
            WriteHeader();
    }

    /// <summary>
    /// İsim tablosunu ağaçtaki girdilerden yeniden oluşturur. Kaldırılan
    /// girdilerin adları tablodan çıkar; aynı adlar paylaşılır. Böylece
    /// tekrarlanan ekle/kaldır işlemleri başlık boşluğunu tüketmez.
    /// </summary>
    private void RebuildNamesTable()
    {
        var bytes = new List<byte>(_names.Length);
        var offsets = new Dictionary<string, uint>(StringComparer.Ordinal);

        foreach (var entry in _entries)
        {
            if (string.IsNullOrEmpty(entry.Name))
            {
                entry.NameOffset = 0;
                continue;
            }

            if (!offsets.TryGetValue(entry.Name, out var offset))
            {
                offset = (uint)bytes.Count;
                bytes.AddRange(Encoding.UTF8.GetBytes(entry.Name));
                bytes.Add(0);
                offsets[entry.Name] = offset;
            }
            entry.NameOffset = offset;
        }

        _names = bytes.ToArray();
    }

    /// <summary>
    /// Ağaçtaki dizinleri ve dosyaları CodeWalker uyumlu biçimde yeniden sıralar:
    /// her dizinin doğrudan çocukları TOC'ta bitişik olur ve isme göre sıralanır.
    /// </summary>
    private void RebuildEntries()
    {
        var order = new List<RpfEntry>(_entries.Count)
        {
            Root
        };
        Root.Index = 0;

        var queue = new Queue<RpfDirectoryEntry>();
        queue.Enqueue(Root);

        while (queue.Count > 0)
        {
            var dir = queue.Dequeue();
            dir.Directories.Sort((a, b) => StringComparer.Ordinal.Compare(a.Name, b.Name));
            dir.Files.Sort((a, b) => StringComparer.Ordinal.Compare(a.Name, b.Name));

            dir.EntriesIndex = (uint)order.Count;
            var children = new List<RpfEntry>(dir.Directories.Count + dir.Files.Count);
            children.AddRange(dir.Directories);
            children.AddRange(dir.Files);
            children.Sort((a, b) => StringComparer.Ordinal.Compare(a.Name, b.Name));

            foreach (var child in children)
            {
                child.Index = order.Count;
                order.Add(child);
                if (child is RpfDirectoryEntry sub) queue.Enqueue(sub);
            }
            dir.EntriesCount = (uint)(order.Count - (int)dir.EntriesIndex);
        }

        if (order.Count != _entries.Count + (order.Count - _entries.Count))
        {
            // (no-op; yalnızca derleyici uyarısını önlemek için değil, okunabilirlik için)
        }

        _entries.Clear();
        _entries.AddRange(order);
        EntryCount = (uint)_entries.Count;
        _toc = new byte[EntryCount * EntrySize];

        for (int i = 0; i < _entries.Count; i++)
        {
            _entries[i].Index = i;
            _entries[i].File = this;
            PatchEntryBytesInMemory(i);
        }
    }

    private void PatchEntryBytesInMemory(int index)
    {
        var encryption = Encryption;
        // PatchEntryBytes AES'te hemen yazar; burada yalnızca bellekte kalmak istiyoruz.
        if (encryption == RpfEncryption.Aes)
        {
            // Geçici olarak OPEN davranışı: aynı kod yolunu kullanmak için
            // doğrudan TOC'a yaz.
        }

        var entry = _entries[index];
        int off = index * EntrySize;

        if (entry is RpfDirectoryEntry dir)
        {
            BitConverter.TryWriteBytes(_toc.AsSpan(off, 4), dir.NameOffset);
            BitConverter.TryWriteBytes(_toc.AsSpan(off + 4, 4), DirectoryIdent);
            BitConverter.TryWriteBytes(_toc.AsSpan(off + 8, 4), dir.EntriesIndex);
            BitConverter.TryWriteBytes(_toc.AsSpan(off + 12, 4), dir.EntriesCount);
        }
        else if (entry is RpfBinaryFileEntry bin)
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
    }

    private void WriteHeader()
    {
        int pad = (int)(Align16(_names.Length) - _names.Length);
        if (pad > 0)
        {
            var padded = new byte[_names.Length + pad];
            Buffer.BlockCopy(_names, 0, padded, 0, _names.Length);
            _names = padded;
        }

        NamesLength = (uint)_names.Length;
        long firstData = FirstDataOffset();
        if (firstData <= 0) firstData = _fs.Length;
        long headerEnd = HeaderEnd(EntryCount, NamesLength);
        if (headerEnd > firstData)
            throw new RpfHeaderSpaceException(
                $"RPF7: başlık mevcut veri alanına sığmıyor " +
                $"(gerekli {headerEnd:N0}, kullanılabilir {firstData:N0} bayt).");

        byte[] tocOut = Encryption switch
        {
            RpfEncryption.Aes => RpfCrypto.EncryptAes(_toc, _aesKey!),
            _ => _toc
        };
        byte[] namesOut = Encryption switch
        {
            RpfEncryption.Aes => RpfCrypto.EncryptAes(_names, _aesKey!),
            _ => _names
        };

        _fs.Position = 0;
        var header = new byte[16];
        BitConverter.TryWriteBytes(header.AsSpan(0, 4), Magic);
        BitConverter.TryWriteBytes(header.AsSpan(4, 4), EntryCount);
        BitConverter.TryWriteBytes(header.AsSpan(8, 4), NamesLength);
        BitConverter.TryWriteBytes(header.AsSpan(12, 4), (uint)Encryption);
        _fs.Write(header, 0, header.Length);
        _fs.Write(tocOut, 0, tocOut.Length);
        _fs.Write(namesOut, 0, namesOut.Length);
        _fs.Flush(true);
    }

    private void VerifyWritten(string path, byte[] content)
    {
        using var check = Open(Path, _aesKey);
        var re = check.Find(path)
            ?? throw new RpfVerificationException($"Doğrulama: '{path}' bulunamadı.");
        var readBack = check.ReadFile(re);
        bool equal = re is RpfResourceFileEntry
            ? ResourceContentEquals(content, readBack)
            : readBack.AsSpan().SequenceEqual(content);
        if (!equal)
            throw new RpfVerificationException(
                $"Doğrulama başarısız: '{path}' içeriği yazdıktan sonra eşleşmiyor " +
                $"(yazılan {content.Length:N0}, okunan {readBack.Length:N0} bayt).");
    }

    /// <summary>
    /// Resource içeriğini karşılaştırır: 16 baytlık başlık bayrakları aynı olmalı;
    /// yüklerden biri sıkıştırılmış, diğeri ham olabilir. İki yük de açılarak
    /// karşılaştırılır.
    /// </summary>
    private static bool ResourceContentEquals(byte[] expected, byte[] actual)
    {
        if (!RpfCrypto.HasResourceHeader(expected))
            return expected.AsSpan().SequenceEqual(actual);
        if (!RpfCrypto.HasResourceHeader(actual))
            return false;

        var expectedFlags = expected.AsSpan(8, 8);
        var actualFlags = actual.AsSpan(8, 8);
        if (!expectedFlags.SequenceEqual(actualFlags)) return false;

        uint sys = BitConverter.ToUInt32(expected, 8);
        uint gfx = BitConverter.ToUInt32(expected, 12);
        long total = RpfCrypto.GetResourceTotalSize(sys, gfx);

        static byte[] Decompress(byte[] payload, long total)
        {
            if (RpfCrypto.TryInflateRaw(payload, out var inflated) &&
                (total <= 0 || inflated.Length == total))
                return inflated;
            return payload;
        }

        var e = Decompress(expected[16..], total);
        var a = Decompress(actual[16..], total);
        return e.AsSpan().SequenceEqual(a);
    }

    /// <summary>
    /// 16 MB üstü resource dosyalarında gerçek boyutu 16 baytlık RSC7
    /// başlığının CodeWalker uyumlu baytlarına yazar.
    /// </summary>
    private static void EncodeResourceSize(byte[] stored)
    {
        if (stored.Length <= MaxFileStoredBytes || stored.Length < 16) return;
        uint len = (uint)stored.Length;
        stored[7] = (byte)(len & 0xFF);
        stored[14] = (byte)((len >> 8) & 0xFF);
        stored[5] = (byte)((len >> 16) & 0xFF);
        stored[2] = (byte)((len >> 24) & 0xFF);
    }

    private static (uint SystemFlags, uint GraphicsFlags, byte[] Header, byte[] Payload)
        PrepareResourcePayload(byte[] content)
    {        uint systemFlags = BitConverter.ToUInt32(content, 8);
        uint graphicsFlags = BitConverter.ToUInt32(content, 12);
        long expected = RpfCrypto.GetResourceTotalSize(systemFlags, graphicsFlags);
        byte[] payload = content[16..];
        var header = content[..16];

        // OpenIV/CodeWalker biçiminde dosya zaten sıkıştırılmışsa olduğu gibi
        // yazılır; aksi halde yük ham DEFLATE ile sıkıştırılır.
        bool alreadyCompressed = RpfCrypto.TryInflateRaw(payload, out var inflated) &&
                                 (expected <= 0 || inflated.Length == expected);
        if (alreadyCompressed)
            return (systemFlags, graphicsFlags, header, payload);

        var compressed = RpfCrypto.Deflate(payload);
        var newHeader = RpfCrypto.BuildResourceHeader(systemFlags, graphicsFlags);
        return (systemFlags, graphicsFlags, newHeader, compressed);
    }

    private long FirstDataOffset()
    {
        long min = long.MaxValue;
        foreach (var f in AllFiles())
        {
            if (StoredDataSize(f) <= 0 || f.FileOffset == 0) continue;
            long pos = (long)f.FileOffset * 512;
            if (pos < min) min = pos;
        }
        return min == long.MaxValue ? 0 : min;
    }

    private long HeaderEnd(long entryCount, long namesLength) =>
        16 + entryCount * EntrySize + namesLength;

    private static long StoredDataSize(RpfFileEntry entry)
    {
        if (entry is RpfBinaryFileEntry bin)
        {
            long size = bin.FileSize == 0 ? bin.FileUncompressedSize : bin.FileSize;
            return bin.IsEncrypted ? Align16(size) : size;
        }
        return entry.FileSize;
    }

    private static long Align16(long value) => (value + 15) / 16 * 16;

    private static string NormalizeRpfPath(string path)
    {
        var p = (path ?? "").Replace('/', '\\').Trim().Trim('\\');
        if (p.Length == 0)
            throw new InvalidOperationException("RPF7: boş dosya yolu.");
        var parts = SplitRpfPath(p);
        return string.Join("\\", parts);
    }

    private static string[] SplitRpfPath(string path)
    {
        var parts = path.Split('\\', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            throw new InvalidOperationException("RPF7: boş dosya yolu.");

        foreach (var part in parts)
        {
            if (part is "." or ".." || part.Contains("..", StringComparison.Ordinal))
                throw new InvalidOperationException($"RPF7: güvenlik dışı yol: {path}");
            if (part.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) >= 0 ||
                part.Contains(':') || part.Contains('*') || part.Contains('?') ||
                part.Contains('"') || part.Contains('<') || part.Contains('>') ||
                part.Contains('|') || part.EndsWith('.') || part.EndsWith(' '))
                throw new InvalidOperationException($"RPF7: geçersiz dosya adı: {part}");
        }
        return parts;
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
                long end = (long)f.FileOffset * 512 + StoredDataSize(f);
                if (end > _fs.Length) result.OutOfBoundsFiles++;
                if (f is RpfBinaryFileEntry b && b.FileUncompressedSize > 0 &&
                    b.FileSize > 0 && b.FileUncompressedSize < b.FileSize)
                    result.SuspiciousSizes++;
            }
        }

        // örnek okumalar: ilk 8 sıkıştırılmış ve ilk 4 ham dosya
        if (sampleReads)
        {
            int compressed = 0, raw = 0;
            foreach (var f in AllFiles())
            {
                bool isCompressed = f is RpfBinaryFileEntry bb && bb.FileSize > 0 &&
                                    bb.FileUncompressedSize > bb.FileSize;
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

public sealed class RpfHeaderSpaceException(string message) : Exception(message);

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
