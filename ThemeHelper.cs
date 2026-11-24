using Microsoft.Win32;

namespace QuickUp;

internal sealed record ThemePalette(
    bool IsDark,
    Color BackgroundPrimary,
    Color BackgroundSecondary,
    Color BackgroundInput,
    Color BackgroundHover,
    Color SidebarBackground,
    Color CanvasBackground,
    Color CardBackground,
    Color CardSubtle,
    Color Border,
    Color BorderHover,
    Color TextPrimary,
    Color TextSecondary,
    Color TextMuted,
    Color TextOnPrimary,
    Color AccentBlue,
    Color AccentBlueHover,
    Color Danger,
    Color SelectedBackground,
    Color SelectedForeground);

/// <summary>
/// Reads Windows light/dark theme preferences and supplies matching Chrome/Antigravity color tokens.
/// </summary>
internal static class ThemeHelper
{
    private const string PersonalizeSubKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    public static readonly ThemePalette Light = new(
        IsDark: false,
        BackgroundPrimary: Color.FromArgb(255, 255, 255),
        BackgroundSecondary: Color.FromArgb(241, 243, 244),
        BackgroundInput: Color.FromArgb(255, 255, 255),
        BackgroundHover: Color.FromArgb(232, 235, 240),
        SidebarBackground: Color.FromArgb(244, 245, 247),
        CanvasBackground: Color.FromArgb(250, 250, 251),
        CardBackground: Color.FromArgb(255, 255, 255),
        CardSubtle: Color.FromArgb(241, 243, 246),
        Border: Color.FromArgb(224, 227, 232),
        BorderHover: Color.FromArgb(189, 193, 198),
        TextPrimary: Color.FromArgb(32, 33, 36),
        TextSecondary: Color.FromArgb(95, 99, 104),
        TextMuted: Color.FromArgb(115, 120, 128),
        TextOnPrimary: Color.FromArgb(255, 255, 255),
        AccentBlue: Color.FromArgb(26, 115, 232),
        AccentBlueHover: Color.FromArgb(23, 101, 204),
        Danger: Color.FromArgb(217, 48, 37),
        SelectedBackground: Color.FromArgb(232, 240, 254),
        SelectedForeground: Color.FromArgb(26, 115, 232));

    public static readonly ThemePalette Dark = new(
        IsDark: true,
        BackgroundPrimary: Color.FromArgb(31, 31, 31),
        BackgroundSecondary: Color.FromArgb(43, 43, 43),
        BackgroundInput: Color.FromArgb(26, 27, 30),
        BackgroundHover: Color.FromArgb(50, 53, 60),
        SidebarBackground: Color.FromArgb(24, 25, 28),
        CanvasBackground: Color.FromArgb(18, 19, 22),
        CardBackground: Color.FromArgb(28, 30, 34),
        CardSubtle: Color.FromArgb(37, 40, 46),
        Border: Color.FromArgb(46, 49, 56),
        BorderHover: Color.FromArgb(74, 79, 89),
        TextPrimary: Color.FromArgb(232, 234, 237),
        TextSecondary: Color.FromArgb(184, 189, 196),
        TextMuted: Color.FromArgb(132, 138, 148),
        TextOnPrimary: Color.FromArgb(20, 22, 26),
        AccentBlue: Color.FromArgb(168, 199, 250),
        AccentBlueHover: Color.FromArgb(144, 184, 248),
        Danger: Color.FromArgb(242, 139, 130),
        SelectedBackground: Color.FromArgb(35, 48, 68),
        SelectedForeground: Color.FromArgb(168, 199, 250));

    public static bool IsAppDarkMode()
    {
        return ReadPersonalizeFlag("AppsUseLightTheme") == 0;
    }

    public static bool IsTaskbarDarkMode()
    {
        int? systemUsesLight = ReadPersonalizeFlag("SystemUsesLightTheme");
        if (systemUsesLight.HasValue)
        {
            return systemUsesLight.Value == 0;
        }
        return IsAppDarkMode();
    }

    public static ThemePalette GetPalette(bool? forceDark = null)
    {
        bool dark = forceDark ?? IsAppDarkMode();
        return dark ? Dark : Light;
    }

    public static ToolStripProfessionalRenderer CreateMenuRenderer(ThemePalette palette)
    {
        return new ThemedMenuRenderer(palette);
    }

    private static int? ReadPersonalizeFlag(string valueName)
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(PersonalizeSubKey);
            object? value = key?.GetValue(valueName);
            return value is int intVal ? intVal : null;
        }
        catch
        {
            return null;
        }
    }

    private sealed class ThemedMenuRenderer : ToolStripProfessionalRenderer
    {
        private readonly ThemePalette _palette;

        public ThemedMenuRenderer(ThemePalette palette)
            : base(new ThemedMenuColorTable(palette))
        {
            _palette = palette;
            RoundedEdges = true;
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Enabled ? _palette.TextPrimary : _palette.TextMuted;
            base.OnRenderItemText(e);
        }
    }

    private sealed class ThemedMenuColorTable(ThemePalette palette) : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => palette.BackgroundPrimary;
        public override Color MenuBorder => palette.Border;
        public override Color MenuItemBorder => Color.Transparent;
        public override Color MenuItemSelected => palette.BackgroundHover;
        public override Color MenuItemSelectedGradientBegin => palette.BackgroundHover;
        public override Color MenuItemSelectedGradientEnd => palette.BackgroundHover;
        public override Color ImageMarginGradientBegin => palette.BackgroundPrimary;
        public override Color ImageMarginGradientMiddle => palette.BackgroundPrimary;
        public override Color ImageMarginGradientEnd => palette.BackgroundPrimary;
        public override Color SeparatorDark => palette.Border;
        public override Color SeparatorLight => palette.Border;
    }
}
