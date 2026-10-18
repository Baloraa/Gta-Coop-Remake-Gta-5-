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

    /// <summary>Dosyanın şifreli olup olmadığını test etmek için basit doğrulama.</summary>
    public static bool LooksLikeXml(byte[] data)
    {
        if (data.Length == 0) return false;
        int start = 0;
        if (data.Length >= 3 && data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF) start = 3;
        return data.Length > start && data[start] == (byte)'<';
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
