using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GTACoop.Remake;

/// <summary>Tasarımcıdaki tek bir HUD öğesi (panel, yazı, resim, saat...).</summary>
internal sealed class HudElement
{
    public string Type { get; set; } = "panel";           // panel | text | image | clock | ping | players | bar
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
                if (layout != null) return layout;
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

    /// <summary>PNG'yi assets klasörüne kopyalar ve öğeyi oyun içi ızgaraya çevirir.</summary>
    public static void AttachImage(HudElement element, string pngPath, int cols = 64)
    {
        Directory.CreateDirectory(AssetsFolder);
        var name = Guid.NewGuid().ToString("N")[..8] + Path.GetExtension(pngPath).ToLowerInvariant();
        var target = Path.Combine(AssetsFolder, name);
        File.Copy(pngPath, target, overwrite: true);
        element.Png = name;
        Bake(element, target, cols);
    }

    /// <summary>PNG'yi kaba bir renk ızgarasına indirger; oyun içinde her hücre dikdörtgen çizilir.</summary>
    public static void Bake(HudElement element, string pngPath, int cols = 64)
    {
        cols = Math.Clamp(cols, 16, 96);
        using var source = Image.FromFile(pngPath);

        var rows = Math.Max(1, (int)Math.Round(cols * (double)source.Height / source.Width));
        rows = Math.Clamp(rows, 1, 96);

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
                cells[x] = c.A < 28 ? "" : $"{c.R:X2}{c.G:X2}{c.B:X2}{c.A:X2}";
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
