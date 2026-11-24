using System.Text.Json;
using System.Text.Json.Serialization;

namespace QuickUp;

/// <summary>
/// Persistent application settings stored in %APPDATA%\dynamic-web-panel\config.json,
/// keeping backward compatibility with the legacy Electron config schema.
/// </summary>
internal sealed class AppSettings
{
    public const int DefaultWidth = 400;
    public const int MinWidth = 320;
    public const int MaxWidth = 900;
    public const int PopupHeight = 600;
    public const int DefaultIdleTeardownSeconds = 30;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    [JsonPropertyName("customUrl")]
    public string CustomUrl { get; set; } = string.Empty;

    [JsonPropertyName("customTitle")]
    public string CustomTitle { get; set; } = string.Empty;

    [JsonPropertyName("customIcon")]
    public string CustomIcon { get; set; } = string.Empty;

    [JsonPropertyName("isPresetIcon")]
    public bool IsPresetIcon { get; set; }

    [JsonPropertyName("customIconDark")]
    public string CustomIconDark { get; set; } = string.Empty;

    [JsonPropertyName("isPresetIconDark")]
    public bool IsPresetIconDark { get; set; }

    [JsonPropertyName("popupWidth")]
    public int PopupWidth { get; set; } = DefaultWidth;

    [JsonPropertyName("linkBehavior")]
    public string LinkBehavior { get; set; } = "popup";

    [JsonPropertyName("openAtLogin")]
    public bool OpenAtLogin { get; set; }

    [JsonPropertyName("idleTeardownSeconds")]
    public int IdleTeardownSeconds { get; set; } = DefaultIdleTeardownSeconds;

    public static string ConfigPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "dynamic-web-panel",
        "config.json");

    public static string WebViewUserDataPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DynamicWebPanel",
        "WebView2");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                string json = File.ReadAllText(ConfigPath);
                AppSettings? loaded = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                if (loaded is not null)
                {
                    loaded.Normalize();
                    return loaded;
                }
            }
        }
        catch
        {
            // Fall back to defaults on unreadable or corrupted config files.
        }

        return new AppSettings();
    }

    public void Save()
    {
        Normalize();

        string? directory = Path.GetDirectoryName(ConfigPath);
        if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string json = JsonSerializer.Serialize(this, JsonOptions);
        string tempPath = ConfigPath + ".tmp";
        File.WriteAllText(tempPath, json);
        File.Move(tempPath, ConfigPath, overwrite: true);
    }

    public AppSettings Clone() => new()
    {
        CustomUrl = CustomUrl,
        CustomTitle = CustomTitle,
        CustomIcon = CustomIcon,
        IsPresetIcon = IsPresetIcon,
        CustomIconDark = CustomIconDark,
        IsPresetIconDark = IsPresetIconDark,
        PopupWidth = PopupWidth,
        LinkBehavior = LinkBehavior,
        OpenAtLogin = OpenAtLogin,
        IdleTeardownSeconds = IdleTeardownSeconds
    };

    public void Normalize()
    {
        CustomUrl = CustomUrl?.Trim() ?? string.Empty;
        CustomTitle = CustomTitle?.Trim() ?? string.Empty;
        CustomIcon ??= string.Empty;
        CustomIconDark ??= string.Empty;
        PopupWidth = PopupWidth <= 0 ? DefaultWidth : Math.Clamp(PopupWidth, MinWidth, MaxWidth);
        LinkBehavior = string.Equals(LinkBehavior, "browser", StringComparison.OrdinalIgnoreCase)
            ? "browser"
            : "popup";
        IdleTeardownSeconds = IdleTeardownSeconds < 0 ? 0 : IdleTeardownSeconds;
    }

    public string GetDisplayTitle()
    {
        if (!string.IsNullOrWhiteSpace(CustomTitle))
        {
            return CustomTitle.Trim();
        }

        if (Uri.TryCreate(CustomUrl, UriKind.Absolute, out Uri? uri) && !string.IsNullOrWhiteSpace(uri.Host))
        {
            string host = uri.Host;
            if (host.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
            {
                host = host[4..];
            }
            if (host.Length > 0)
            {
                return char.ToUpperInvariant(host[0]) + host[1..];
            }
        }

        return "Dynamic Web Panel";
    }

    public static string NormalizeUrlForCompare(string? url)
    {
        return string.IsNullOrWhiteSpace(url) ? string.Empty : url.Trim().TrimEnd('/');
    }

    public static bool TryNormalizeWebUrl(string raw, out string normalized)
    {
        normalized = string.Empty;
        string candidate = raw.Trim();
        if (string.IsNullOrEmpty(candidate))
        {
            return false;
        }

        if (!candidate.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !candidate.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            candidate = "https://" + candidate;
        }

        if (!Uri.TryCreate(candidate, UriKind.Absolute, out Uri? uri))
        {
            return false;
        }

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            return false;
        }

        normalized = uri.AbsoluteUri;
        return true;
    }
}
