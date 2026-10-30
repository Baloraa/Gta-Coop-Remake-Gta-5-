namespace GTACoop.Rpf;

public enum RpfEncryption : uint
{
    None = 0,
    Open = 0x4E45504F,   // "OPEN" — OpenIV'nin şifresiz TOC formatı
    Aes = 0x0FFFFFF9,
    Ng = 0x0FEFFFFF      // Gen9 — desteklenmiyor
}

public abstract class RpfEntry
{
    public RpfArchive File { get; set; } = null!;
    public RpfDirectoryEntry? Parent { get; set; }
    public int Index { get; set; }
    public uint NameOffset { get; set; }
    public string Name { get; set; } = string.Empty;
    public string NameLower { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;

    public override string ToString() => Path;
}

public sealed class RpfDirectoryEntry : RpfEntry
{
    public uint EntriesIndex { get; set; }
    public uint EntriesCount { get; set; }
    public List<RpfDirectoryEntry> Directories { get; } = new();
    public List<RpfFileEntry> Files { get; } = new();
}

public abstract class RpfFileEntry : RpfEntry
{
    public uint FileOffset { get; set; }  // 512 baytlık sektör cinsinden
    public uint FileSize { get; set; }
    public bool IsEncrypted { get; set; }
    public abstract long GetDataSize();
}

public sealed class RpfBinaryFileEntry : RpfFileEntry
{
    public uint FileUncompressedSize { get; set; }

    /// <summary>Okuma sırasında algılanan sıkıştırma formatı (yazarken aynısını kullanmak için).</summary>
    public RpfCompression Compression { get; set; } = RpfCompression.Unknown;

    public override long GetDataSize() =>
        FileSize == 0 ? FileUncompressedSize : FileSize;
}

public enum RpfCompression
{
    Unknown = 0,
    None = 1,   // ham depolanmış
    Raw = 2,    // ham DEFLATE
    Zlib = 3    // zlib sarmalı (RFC1950)
}

public sealed class RpfResourceFileEntry : RpfFileEntry
{
    public uint SystemFlags { get; set; }
    public uint GraphicsFlags { get; set; }

    public override long GetDataSize() => FileSize == 0 ? 0xFFFFFF : FileSize;
}

/// <summary>
/// Bir RPF girdisinin ham TOC durumu. Şifreli (okunamayan) dosyaları geri
/// alabilmek için içerik yerine yalnızca konum/bayrak bilgisi saklanır.
/// </summary>
public sealed class RpfEntryState
{
    public bool Exists { get; set; }
    public bool IsResource { get; set; }
    public uint FileOffset { get; set; }
    public uint FileSize { get; set; }
    public uint FileUncompressedSize { get; set; }
    public uint SystemFlags { get; set; }
    public uint GraphicsFlags { get; set; }
    public bool IsEncrypted { get; set; }
    public RpfCompression Compression { get; set; } = RpfCompression.Unknown;
}
