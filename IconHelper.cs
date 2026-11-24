using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Reflection;

namespace QuickUp;

/// <summary>
/// Loads embedded assets, decodes/encodes custom tray icons safely, and draws crisp vector preset icons.
/// </summary>
internal static class IconHelper
{
    public static readonly string[] PresetKeys =
    [
        "globe", "book", "chat", "code", "kanban", "star", "calendar", "home"
    ];

    private static readonly Lazy<Icon> DefaultAppIcon = new(LoadEmbeddedIcon);
    private static readonly Lazy<Bitmap> DefaultPngBitmap = new(LoadEmbeddedPngBitmap);

    public static Icon GetDefaultAppIcon() => DefaultAppIcon.Value;

    public static Bitmap CreateDefaultBitmapCopy() => new(DefaultPngBitmap.Value);

    /// <summary>
    /// Resolves the appropriate header bitmap for the popup top bar (always freshly owned by caller).
    /// </summary>
    public static Bitmap ResolveHeaderBitmap(AppSettings settings, bool isAppDark)
    {
        string? dataUrl = isAppDark && !string.IsNullOrWhiteSpace(settings.CustomIconDark)
            ? settings.CustomIconDark
            : settings.CustomIcon;

        Bitmap? decoded = DecodeDataUrlToBitmap(dataUrl);
        return decoded ?? CreateDefaultBitmapCopy();
    }

    /// <summary>
    /// Resolves a freshly allocated Icon for the Windows notification area matching the taskbar theme.
    /// </summary>
    public static Icon ResolveTrayIcon(AppSettings settings, bool isTaskbarDark)
    {
        string? dataUrl = isTaskbarDark && !string.IsNullOrWhiteSpace(settings.CustomIconDark)
            ? settings.CustomIconDark
            : settings.CustomIcon;

        if (!string.IsNullOrWhiteSpace(dataUrl))
        {
            using Bitmap? decoded = DecodeDataUrlToBitmap(dataUrl);
            if (decoded is not null)
            {
                return BitmapToTrayIcon(decoded, 32);
            }
        }

        return (Icon)DefaultAppIcon.Value.Clone();
    }

    /// <summary>
    /// Decodes a data:image/...;base64,... URI into an independent 32bpp ARGB Bitmap.
    /// </summary>
    public static Bitmap? DecodeDataUrlToBitmap(string? dataUrl)
    {
        if (string.IsNullOrWhiteSpace(dataUrl)) return null;

        int commaIndex = dataUrl.IndexOf(',');
        if (commaIndex <= 0) return null;

        string header = dataUrl[..commaIndex];
        if (!header.Contains("base64", StringComparison.OrdinalIgnoreCase)) return null;

        try
        {
            byte[] bytes = Convert.FromBase64String(dataUrl[(commaIndex + 1)..].Trim());
            if (bytes.Length < 4) return null;

            using var ms = new MemoryStream(bytes);

            // Detect ICO signature (00 00 01 00) since GDI+ Image.FromStream cannot decode multi-entry .ico directly in all paths.
            if (bytes[0] == 0x00 && bytes[1] == 0x00 && bytes[2] == 0x01 && bytes[3] == 0x00)
            {
                using var ico = new Icon(ms, 128, 128);
                using Bitmap raw = ico.ToBitmap();
                return CloneAs32bppArgb(raw);
            }

            using Image img = Image.FromStream(ms, useEmbeddedColorManagement: true, validateImageData: true);
            return CloneAs32bppArgb(img);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Loads a user-selected image or .ico file, scales proportionally within maxSize, and encodes as PNG data URL.
    /// </summary>
    public static string ResizeAndEncodePngDataUrl(string filePath, int maxSize = 128)
    {
        byte[] fileBytes = File.ReadAllBytes(filePath);
        using var inputStream = new MemoryStream(fileBytes);

        Bitmap? source = null;
        try
        {
            bool isIco = Path.GetExtension(filePath).Equals(".ico", StringComparison.OrdinalIgnoreCase) ||
                         (fileBytes.Length >= 4 && fileBytes[0] == 0x00 && fileBytes[1] == 0x00 && fileBytes[2] == 0x01 && fileBytes[3] == 0x00);

            if (isIco)
            {
                using var ico = new Icon(inputStream, maxSize, maxSize);
                source = ico.ToBitmap();
            }
            else
            {
                using Image img = Image.FromStream(inputStream, useEmbeddedColorManagement: true, validateImageData: true);
                source = CloneAs32bppArgb(img);
            }

            int targetW = source.Width;
            int targetH = source.Height;
            if (targetW > maxSize || targetH > maxSize)
            {
                double ratio = Math.Min((double)maxSize / targetW, (double)maxSize / targetH);
                targetW = Math.Max(1, (int)Math.Round(targetW * ratio));
                targetH = Math.Max(1, (int)Math.Round(targetH * ratio));
            }

            using var resized = new Bitmap(targetW, targetH, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(resized))
            {
                ConfigureHighQuality(g);
                g.DrawImage(source, 0, 0, targetW, targetH);
            }

            using var outputStream = new MemoryStream();
            resized.Save(outputStream, ImageFormat.Png);
            return "data:image/png;base64," + Convert.ToBase64String(outputStream.ToArray());
        }
        finally
        {
            source?.Dispose();
        }
    }

    /// <summary>
    /// Converts a Bitmap into a properly sized tray Icon without leaking native GDI icon handles.
    /// </summary>
    public static Icon BitmapToTrayIcon(Bitmap source, int size = 32)
    {
        using var scaled = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(scaled))
        {
            ConfigureHighQuality(g);
            g.DrawImage(source, 0, 0, size, size);
        }

        IntPtr hIcon = scaled.GetHicon();
        try
        {
            using Icon temp = Icon.FromHandle(hIcon);
            return (Icon)temp.Clone();
        }
        finally
        {
            NativeMethods.DestroyIcon(hIcon);
        }
    }

    public static string RenderPresetDataUrl(string presetKey, int size = 128)
    {
        using Bitmap bmp = RenderPresetIconBitmap(presetKey, size);
        using var ms = new MemoryStream();
        bmp.Save(ms, ImageFormat.Png);
        return "data:image/png;base64," + Convert.ToBase64String(ms.ToArray());
    }

    /// <summary>
    /// Renders one of the 8 built-in presets onto an orange-gradient rounded squircle with a crisp vector glyph.
    /// </summary>
    public static Bitmap RenderPresetIconBitmap(string presetKey, int size = 128)
    {
        var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using Graphics g = Graphics.FromImage(bmp);
        ConfigureHighQuality(g);
        g.Clear(Color.Transparent);

        float s = size / 128f;
        var rect = new RectangleF(8f * s, 8f * s, 112f * s, 112f * s);
        float radius = 28f * s;

        using (GraphicsPath squircle = CreateRoundedRect(rect, radius))
        {
            using var brush = new LinearGradientBrush(
                new PointF(0, 0),
                new PointF(size, size),
                Color.FromArgb(255, 158, 34),
                Color.FromArgb(255, 69, 0));
            g.FillPath(brush, squircle);

            using var borderPen = new Pen(Color.FromArgb(62, 255, 255, 255), Math.Max(1.2f, 4f * s));
            g.DrawPath(borderPen, squircle);
        }

        DrawPresetGlyph(g, presetKey, s);
        return bmp;
    }

    public static GraphicsPath CreateRoundedRect(RectangleF bounds, float radius)
    {
        var path = new GraphicsPath();
        float d = Math.Min(radius * 2f, Math.Min(bounds.Width, bounds.Height));
        if (d <= 0.5f)
        {
            path.AddRectangle(bounds);
            return path;
        }

        path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
        path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
        path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    private static void DrawPresetGlyph(Graphics g, string presetKey, float s)
    {
        TablerIcon icon = presetKey.ToLowerInvariant() switch
        {
            "globe" => TablerIcon.World,
            "book" => TablerIcon.Book,
            "chat" => TablerIcon.MessageCircle,
            "code" => TablerIcon.Code,
            "kanban" => TablerIcon.LayoutKanban,
            "star" => TablerIcon.Star,
            "calendar" => TablerIcon.Calendar,
            _ => TablerIcon.Home
        };

        TablerIcons.Draw(g, icon, 26f * s, 26f * s, 76f * s, Color.White, strokeWidth24: 2.0f);
    }

    private static void ConfigureHighQuality(Graphics g)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.CompositingQuality = CompositingQuality.HighQuality;
    }

    private static Bitmap CloneAs32bppArgb(Image source)
    {
        var copy = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);
        using Graphics g = Graphics.FromImage(copy);
        ConfigureHighQuality(g);
        g.DrawImage(source, 0, 0, source.Width, source.Height);
        return copy;
    }

    private static Icon LoadEmbeddedIcon()
    {
        Assembly asm = Assembly.GetExecutingAssembly();
        string? name = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("icon.ico", StringComparison.OrdinalIgnoreCase));

        if (name is not null)
        {
            using Stream? stream = asm.GetManifestResourceStream(name);
            if (stream is not null)
            {
                return new Icon(stream);
            }
        }

        return SystemIcons.Application;
    }

    private static Bitmap LoadEmbeddedPngBitmap()
    {
        Assembly asm = Assembly.GetExecutingAssembly();
        string? name = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("icon.png", StringComparison.OrdinalIgnoreCase));

        if (name is not null)
        {
            using Stream? stream = asm.GetManifestResourceStream(name);
            if (stream is not null)
            {
                using Image img = Image.FromStream(stream);
                return CloneAs32bppArgb(img);
            }
        }

        return DefaultAppIcon.Value.ToBitmap();
    }
}
