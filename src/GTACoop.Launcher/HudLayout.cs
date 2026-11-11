using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GTACoop.Remake;

/// <summary>Tasarımcıdaki tek bir HUD öğesi (panel, yazı, resim, saat...).</summary>
internal sealed class HudElement
{
    public string Type { get; set; } = "panel";           // panel | text | image | clock | ping | players | bar | outline | circle | line | gradient | speed | weapon | compass | zone | coords | fps
    public double X { get; set; } = 0.02;                 // sol üst köşe (ekranın oranı)
    public double Y { get; set; } = 0.02;
    public double W { get; set; } = 0.22;
    public double H { get; set; } = 0.06;
    public string Color { get; set; } = "#121215";
    public double Opacity { get; set; } = 0.85;
    public string Text { get; set; } = "";
    public double FontSize { get; set; } = 0.35;
    public string Align { get; set; } = "left";           // left | center | right
    public string Png { get; set; } = "";                 // assets klasöründeki dosya adı
    public int Cols { get; set; }
    public int Rows { get; set; }
    public string Pixels { get; set; } = "";              // satırlar ';' ile, hücreler RRGGBBAA

    /// <summary>Kalınlık (çerçeve, daire halkası ve çizgi için ekranın oranı).</summary>
    public double Thickness { get; set; } = 0.006;

    /// <summary>
    /// HUD kapalıyken (F9) ya da launcher menüsü açıkken de çizilsin. Varsayılan
    /// olarak kapalıdır: PNG'ler de diğer öğeler gibi HUD ile birlikte gizlenir.
    /// </summary>
    public bool AlwaysVisible { get; set; }

    /// <summary>
    /// Kaç saniye görünsün? 0 = süresiz. Süre dolunca öğe oturum boyunca bir daha
    /// çizilmez ("saniye seç, sonra asla görünmesin").
    /// </summary>
    public double VisibleSeconds { get; set; }

    [JsonIgnore]
    public string DisplayLabel => Type switch
    {
        "panel" => "Panel",
        "text" => "Yazı: " + (Text.Length > 18 ? Text[..18] + "…" : Text),
        "image" => "Resim: " + (Png.Length > 0 ? Png : "(boş)"),
        "clock" => "Saat",
        "ping" => "Ping",
        "players" => "Oyuncu listesi",
        "bar" => "Sağlık/zırh çubuğu",
        "outline" => "Çerçeve",
        "circle" => "Daire",
        "line" => "Çizgi",
        "gradient" => "Gradyan panel",
        "speed" => "Hız göstergesi",
        "weapon" => "Silah / mermi",
        "compass" => "Yön (pusula)",
        "zone" => "Bölge / sokak",
        "coords" => "Konum (X,Y,Z)",
        "fps" => "FPS",
        _ => Type
    };
}

/// <summary>Tasarımcıda tanımlanan klavye kısayolu.</summary>
internal sealed class HudShortcut
{
    public string Key { get; set; } = "F7";
    public string Action { get; set; } = "toggle";        // toggle | players | voice | radar | hide
    public string Label { get; set; } = "";

    [JsonIgnore]
    public string ActionText => Action switch
    {
        "toggle" => "HUD'u aç/kapat",
        "players" => "Oyuncu listesi",
        "voice" => "Mikrofonu aç/kapat",
        "radar" => "Radarı aç/kapat",
        "hide" => "Arayüzü gizle",
        _ => Action
    };
}

/// <summary>
/// Oyuncu giriş/çıkışında gösterilecek isteğe bağlı bildirim: PNG (assets
/// klasöründe) ve ses dosyası (wav/mp3). Resim yumuşak geçişle kaybolur.
/// </summary>
internal sealed class HudNotification
{
    public bool Enabled { get; set; }
    public string JoinPng { get; set; } = "";
    public string JoinSound { get; set; } = "";
    public double JoinSeconds { get; set; } = 4;
    public string LeavePng { get; set; } = "";
    public string LeaveSound { get; set; } = "";
    public double LeaveSeconds { get; set; } = 4;
    public double FadeSeconds { get; set; } = 0.6;
}

/// <summary>Launcher'da tasarlanan, oyun içinde çizilen HUD yerleşimi.</summary>
internal sealed class HudLayout
{
    public string Name { get; set; } = "Benim HUD";
    // İsteğe bağlı: varsayılan olarak kapalı. Kapalıyken oyunun kendi HUD'u
    // hiç değiştirilmez ve oyun/sunucu dosyalarına dokunulmaz.
    public bool Enabled { get; set; }
    public bool HideGameHud { get; set; }
    public bool HideRadar { get; set; }
    public bool HideMinimapMap { get; set; }   // haritanın kendisi (radar çerçevesi kalır)
    public bool HideWantedStars { get; set; }
    public bool HideStreetName { get; set; }
    public bool HideVehicleInfo { get; set; }
    public bool HideHelpText { get; set; }
    public List<HudElement> Elements { get; set; } = new();
    public List<HudShortcut> Shortcuts { get; set; } = new();
    public HudNotification Notifications { get; set; } = new();

    /// <summary>
    /// 2.11 öncesi kaydedilmiş yerleşimlerde "her zaman görünür" alanı yoktu; bu
    /// bayrak eski PNG'lerin bir kez taşındığını işaretler.
    /// </summary>
    public bool OptionsMigrated { get; set; }

    /// <summary>PNG'lerin F9'da gizlenmesine geçişin bir kez uygulandığını işaretler.</summary>
    public bool ImagesFollowHud { get; set; }
}

/// <summary>Yerleşim dosyalarını okur/yazar, PNG'leri oyun içi ızgaraya çevirir.</summary>
internal static class HudStore
{
    public static string UiFolder => Path.Combine(AppEnv.DataFolder, "ui");
    public static string AssetsFolder => Path.Combine(UiFolder, "assets");
    public static string DefaultPath => Path.Combine(UiFolder, "hud.json");

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static HudLayout Load(string? path = null)
    {
        var file = path ?? DefaultPath;
        try
        {
            if (File.Exists(file))
            {
                var layout = JsonSerializer.Deserialize<HudLayout>(File.ReadAllText(file), Options);
                if (layout != null)
                {
                    Migrate(layout);
                    return layout;
                }
            }
        }
        catch (Exception ex)
        {
            AppEnv.LogError("HUD yerleşimi okunamadı: " + ex.Message);
        }

        return CreateSample();
    }

    public static void Save(HudLayout layout, string? path = null)
    {
        Directory.CreateDirectory(UiFolder);
        var file = path ?? DefaultPath;
        File.WriteAllText(file, JsonSerializer.Serialize(layout, Options));
    }

    /// <summary>
    /// Eski yerleşimleri taşır: 2.11'de PNG'ler "her zaman görünür" yapılmıştı;
    /// bu yüzden F9'a basınca ekranda kalıyorlardı. Artık PNG'ler de HUD ile
    /// birlikte gizlenir; kullanıcı isterse tek tek "her zaman görünür" işaretler.
    /// </summary>
    private static void Migrate(HudLayout layout)
    {
        layout.Elements ??= new List<HudElement>();
        layout.Shortcuts ??= new List<HudShortcut>();
        layout.Notifications ??= new HudNotification();

        if (!layout.OptionsMigrated)
            layout.OptionsMigrated = true;

        if (!layout.ImagesFollowHud)
        {
            foreach (var element in layout.Elements)
            {
                if (element != null &&
                    string.Equals(element.Type, "image", StringComparison.OrdinalIgnoreCase))
                    element.AlwaysVisible = false;
            }

            layout.ImagesFollowHud = true;
        }
    }

    /// <summary>Bir dosyayı (PNG/ses) assets klasörüne kopyalar ve yeni adını döndürür.</summary>
    public static string AttachAsset(string sourcePath)
    {
        Directory.CreateDirectory(AssetsFolder);
        var name = Guid.NewGuid().ToString("N")[..8] + Path.GetExtension(sourcePath).ToLowerInvariant();
        File.Copy(sourcePath, Path.Combine(AssetsFolder, name), overwrite: true);
        return name;
    }

    /// <summary>PNG'yi assets klasörüne kopyalar ve öğeyi oyun içi çizime hazırlar.</summary>
    public static void AttachImage(HudElement element, string pngPath)
    {
        var target = Path.Combine(AssetsFolder, AttachAsset(pngPath));
        element.Png = Path.GetFileName(target);
        element.AlwaysVisible = false;
        Bake(element, target);
    }

    /// <summary>
    /// PNG'yi renk ızgarasına indirger; SHVDN'in CustomSprite'ı yoksa oyun içinde
    /// her hücre bir dikdörtgen olarak çizilir. Izgara çözünürlüğü görselin oranına
    /// göre seçilir: küçük resimlerde daha çok sütun kullanılır, böylece detay kaybı
    /// azalır ama kare başına çizim sayısı sınırlı kalır.
    /// </summary>
    public static void Bake(HudElement element, string pngPath)
    {
        using var source = Image.FromFile(pngPath);
        var aspect = (double)source.Height / Math.Max(1, source.Width);

        const int cellBudget = 5200;
        var cols = (int)Math.Round(Math.Sqrt(cellBudget / Math.Max(0.05, aspect)));
        cols = Math.Clamp(cols, 32, 140);

        var rows = Math.Max(1, (int)Math.Round(cols * aspect));
        if (rows > 140)
        {
            rows = 140;
            cols = Math.Clamp((int)Math.Round(rows / Math.Max(0.05, aspect)), 8, 140);
        }

        using var small = new Bitmap(cols, rows, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(small))
        {
            g.InterpolationMode = InterpolationMode.HighQualityBilinear;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.DrawImage(source, 0, 0, cols, rows);
        }

        var lines = new List<string>(rows);
        for (var y = 0; y < rows; y++)
        {
            var cells = new string[cols];
            for (var x = 0; x < cols; x++)
            {
                var c = small.GetPixel(x, y);
                // Çok saydam pikselleri at: ızgarada görünmez, çizim maliyeti var.
                cells[x] = c.A < 16 ? "" : $"{c.R:X2}{c.G:X2}{c.B:X2}{c.A:X2}";
            }
            lines.Add(string.Join(",", cells));
        }

        element.Cols = cols;
        element.Rows = rows;
        element.Pixels = string.Join(";", lines);
    }

    /// <summary>İlk açılışta boş ekran yerine örnek bir HUD.</summary>
    public static HudLayout CreateSample() => new()
    {
        Name = "Varsayılan HUD",
        Elements =
        {
            new HudElement { Type = "panel", X = 0.02, Y = 0.03, W = 0.26, H = 0.13, Color = "#0D0D0F", Opacity = 0.78 },
            new HudElement { Type = "text", X = 0.032, Y = 0.035, W = 0.24, H = 0.035, Text = "GTACoop Remake", Color = "#35C759", FontSize = 0.42 },
            new HudElement { Type = "clock", X = 0.032, Y = 0.075, W = 0.1, H = 0.03, Color = "#EDEDED", FontSize = 0.32 },
            new HudElement { Type = "ping", X = 0.15, Y = 0.075, W = 0.1, H = 0.03, Color = "#9AA0A6", FontSize = 0.32 },
            new HudElement { Type = "players", X = 0.032, Y = 0.105, W = 0.24, H = 0.05, Color = "#EDEDED", FontSize = 0.3 }
        },
        Shortcuts =
        {
            new HudShortcut { Key = "F7", Action = "toggle" },
            new HudShortcut { Key = "F8", Action = "players" },
            new HudShortcut { Key = "F9", Action = "voice" }
        }
    };
}
