using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace GTACoop.Rpf;

/// <summary>
/// mods/update/update.rpf içindeki common/data/dlclist.xml dosyasını
/// güvenli biçimde okuma ve düzenleme yardımcıları.
///
/// Yapı:
///   &lt;SMandatoryPacksData&gt;
///     &lt;Paths&gt;
///       &lt;Item&gt;platform:/dlcPacks/mpApartment/&lt;/Item&gt;
///       ...
///     &lt;/Paths&gt;
///   &lt;/SMandatoryPacksData&gt;
/// </summary>
public static class Dlclist
{
    public const string RelativePath = "common/data/dlclist.xml";

    /// <summary>Tüm dlcPacks item adlarını (ör. "mpApartment") döndürür.</summary>
    public static List<string> GetPackNames(string xml)
    {
        var doc = XDocument.Parse(xml);
        var result = new List<string>();
        foreach (var item in doc.Descendants().Where(e => e.Name.LocalName == "Item"))
        {
            var name = ParseItem(item.Value);
            if (name != null) result.Add(name);
        }
        return result;
    }

    public static string? ParseItem(string value)
    {
        var v = value.Trim();
        // İki şema desteklenir: eski "platform:/dlcPacks/<ad>/" ve modern "dlcpacks:/<ad>/"
        string[] prefixes = { "platform:/dlcPacks/", "dlcpacks:/" };
        foreach (var prefix in prefixes)
        {
            if (v.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                v = v[prefix.Length..].TrimEnd('/');
                return v.Length == 0 ? null : v;
            }
        }
        return null;
    }

    /// <summary>Yeni girdiler modern "dlcpacks:/" şemasıyla yazılır (b2802+ oyunlar bunu kullanır).</summary>
    public static string FormatItem(string packName) => $"dlcpacks:/{packName}/";

    /// <summary>
    /// Verilen paket adlarını listeye ekler (zaten varsa atlanır).
    /// Orijinal biçimi korumaya çalışır; yeni satırlar son Item'dan sonra eklenir.
    /// </summary>
    public static string AddPackNames(string xml, IEnumerable<string> packNames)
    {
        var doc = LoadPreservingFormat(xml);
        var paths = FindPathsElement(doc);
        var existing = new HashSet<string>(
            paths.Descendants().Where(e => e.Name.LocalName == "Item")
                 .Select(e => ParseItem(e.Value))
                 .Where(n => n != null)!,
            StringComparer.OrdinalIgnoreCase);

        var additions = packNames
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Select(n => n.Trim())
            .Where(n => !existing.Contains(n))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (additions.Count == 0) return Serialize(doc, xml);

        var lastItem = paths.Descendants().LastOrDefault(e => e.Name.LocalName == "Item");
        string indent = lastItem != null ? GetIndent(lastItem) : "        ";
        string nl = xml.Contains("\r\n") ? "\r\n" : "\n";

        XElement anchor = lastItem ?? paths.Elements().LastOrDefault() ?? paths;
        if (lastItem == null)
        {
            foreach (var name in additions)
            {
                var el = new XElement(anchor.Name.Namespace + "Item", FormatItem(name));
                paths.Add(new XText(nl + indent));
                paths.Add(el);
            }
            paths.Add(new XText(nl));
        }
        else
        {
            foreach (var name in additions)
            {
                var el = new XElement(anchor.Name.Namespace + "Item", FormatItem(name));
                anchor.AddAfterSelf(el);
                el.AddBeforeSelf(new XText(nl + indent));
                anchor = el;   // sıradaki ekleme sırayı bozmasın diye son eklenen öğeye geç
            }
        }

        return Serialize(doc, xml);
    }

    /// <summary>Verilen paket adlarını listeden çıkarır.</summary>
    public static string RemovePackNames(string xml, IEnumerable<string> packNames)
    {
        var targets = new HashSet<string>(packNames, StringComparer.OrdinalIgnoreCase);
        var doc = LoadPreservingFormat(xml);
        var paths = FindPathsElement(doc);

        var toRemove = paths.Descendants()
            .Where(e => e.Name.LocalName == "Item")
            .Where(e =>
            {
                var n = ParseItem(e.Value);
                return n != null && targets.Contains(n);
            })
            .ToList();

        foreach (var el in toRemove)
        {
            var next = el.NextNode;
            // yanındaki boşluk düğümünü de temizle
            var prev = el.PreviousNode;
            el.Remove();
            if (prev is XText t && string.IsNullOrWhiteSpace(t.Value)) t.Remove();
            // eski sürümlerin bıraktığı çift boşluk artığını da temizle
            // (ör. "...</Item>\n\t\t\n\t</Paths>")
            if (next is XText n && string.IsNullOrWhiteSpace(n.Value) &&
                n.NextNode is XText n2 && string.IsNullOrWhiteSpace(n2.Value))
                n.Remove();
        }

        return Serialize(doc, xml);
    }

    /// <summary>dlclist.xml dosyasının bayt kodlaması.</summary>
    public enum DlclistEncoding
    {
        Utf8,
        Utf8Bom,
        Utf16LeBom,
        Utf16BeBom
    }

    /// <summary>
    /// Ham baytları metne çevirir. Orijinal oyun dosyası UTF-16 (BOM'lu) olduğundan
    /// BOM algılaması şarttır; aksi hâlde düzenlemeler dosyayı bozar.
    /// </summary>
    public static (string Text, DlclistEncoding Enc) Decode(byte[] data)
    {
        if (data.Length >= 2 && data[0] == 0xFF && data[1] == 0xFE)
            return (Encoding.Unicode.GetString(data, 2, data.Length - 2), DlclistEncoding.Utf16LeBom);
        if (data.Length >= 2 && data[0] == 0xFE && data[1] == 0xFF)
            return (Encoding.BigEndianUnicode.GetString(data, 2, data.Length - 2), DlclistEncoding.Utf16BeBom);
        if (data.Length >= 3 && data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF)
            return (Encoding.UTF8.GetString(data, 3, data.Length - 3), DlclistEncoding.Utf8Bom);
        return (Encoding.UTF8.GetString(data), DlclistEncoding.Utf8);
    }

    /// <summary>Metni özgün kodlamasıyla (BOM korunarak) baytlara çevirir.</summary>
    public static byte[] Encode(string text, DlclistEncoding enc)
    {
        switch (enc)
        {
            case DlclistEncoding.Utf16LeBom:
                return Prepend(new byte[] { 0xFF, 0xFE }, Encoding.Unicode.GetBytes(text));
            case DlclistEncoding.Utf16BeBom:
                return Prepend(new byte[] { 0xFE, 0xFF }, Encoding.BigEndianUnicode.GetBytes(text));
            case DlclistEncoding.Utf8Bom:
                return Prepend(new byte[] { 0xEF, 0xBB, 0xBF }, Encoding.UTF8.GetBytes(text));
            default:
                return Encoding.UTF8.GetBytes(text);
        }
    }

    /// <summary>
    /// XML beyanı "utf-16" olduğu hâlde dosya UTF-8 ise (bozuk araçlardan kalma
    /// yaygın bir durum) beyana uyup UTF-16LE+BOM üretir. OpenIV'in XML düzenleyicisi
    /// bu uyumsuzlukta dosyayı okuyamayabilir.
    /// </summary>
    public static DlclistEncoding Normalize(string text, DlclistEncoding current)
    {
        var head = text.Length > 200 ? text[..200] : text;
        if (head.Contains("encoding=\"utf-16\"", StringComparison.OrdinalIgnoreCase) &&
            (current == DlclistEncoding.Utf8 || current == DlclistEncoding.Utf8Bom))
        {
            return DlclistEncoding.Utf16LeBom;
        }
        return current;
    }

    private static byte[] Prepend(byte[] head, byte[] body)
    {
        var full = new byte[head.Length + body.Length];
        Buffer.BlockCopy(head, 0, full, 0, head.Length);
        Buffer.BlockCopy(body, 0, full, head.Length, body.Length);
        return full;
    }

    /// <summary>Dosyanın XML olup olmadığını (UTF-8/UTF-16 BOM'larıyla) test eder.</summary>
    public static bool LooksLikeXml(byte[] data)
    {
        if (data.Length == 0) return false;
        if (data.Length >= 3 && data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF)
            return data.Length > 3 && data[3] == (byte)'<';
        if (data.Length >= 4 && data[0] == 0xFF && data[1] == 0xFE)
            return data[2] == (byte)'<' && data[3] == 0x00;
        if (data.Length >= 4 && data[0] == 0xFE && data[1] == 0xFF)
            return data[2] == 0x00 && data[3] == (byte)'<';
        return data[0] == (byte)'<';
    }

    // ------------------------------------------------------------ helpers

    private static XDocument LoadPreservingFormat(string xml)
    {
        var settings = new XmlReaderSettings
        {
            IgnoreWhitespace = false,
            DtdProcessing = DtdProcessing.Prohibit
        };
        using var sr = new StringReader(xml);
        using var reader = XmlReader.Create(sr, settings);
        return XDocument.Load(reader, LoadOptions.PreserveWhitespace);
    }

    private static string Serialize(XDocument doc, string original)
    {
        bool hadBom = original.Length > 0 && original[0] == '\uFEFF';
        var sb = new StringBuilder();
        using (var sw = new StringWriter(sb))
        using (var xw = XmlWriter.Create(sw, new XmlWriterSettings
        {
            Indent = false,
            OmitXmlDeclaration = false,
            Encoding = new UTF8Encoding(false)
        }))
        {
            doc.Save(xw);
        }
        var text = sb.ToString();
        if (!hadBom) text = text.TrimStart('\uFEFF');
        return text;
    }

    private static XElement FindPathsElement(XDocument doc)
    {
        var paths = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "Paths")
            ?? throw new InvalidDataException("dlclist.xml: <Paths> öğesi bulunamadı.");
        return paths;
    }

    private static string GetIndent(XElement element)
    {
        var prev = element.PreviousNode as XText;
        if (prev != null)
        {
            var text = prev.Value;
            int idx = text.LastIndexOf('\n');
            if (idx >= 0) return text[(idx + 1)..].TrimEnd('\r');
        }
        return "        ";
    }
}
