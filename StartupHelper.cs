using Microsoft.Win32;

namespace QuickUp;

/// <summary>
/// Manages automatic launch at Windows sign-in via HKCU\...\Run and cleans up legacy Electron entries.
/// </summary>
internal static class StartupHelper
{
    private const string RunSubKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "DynamicWebPanel";
    private const string LegacyValueName = "dynamic-web-panel";

    public static void Apply(bool openAtLogin)
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunSubKey, writable: true);
            if (key is null) return;

            // Remove legacy Electron startup entries if present so the user never gets duplicate launches.
            key.DeleteValue(LegacyValueName, throwOnMissingValue: false);
            if (key.GetValue("electron.app.Electron") is string legacyElectronCmd &&
                (legacyElectronCmd.Contains("QuickUp", StringComparison.OrdinalIgnoreCase) ||
                 legacyElectronCmd.Contains("DynamicWebPanel", StringComparison.OrdinalIgnoreCase) ||
                 legacyElectronCmd.Contains("dynamic-web-panel", StringComparison.OrdinalIgnoreCase)))
            {
                key.DeleteValue("electron.app.Electron", throwOnMissingValue: false);
            }

            if (openAtLogin)
            {
                string? exePath = Environment.ProcessPath;
                if (!string.IsNullOrWhiteSpace(exePath))
                {
                    key.SetValue(ValueName, $"\"{exePath}\" --hidden", RegistryValueKind.String);
                }
            }
            else
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
            }
        }
        catch
        {
            // Do not crash startup if registry policy blocks Run-key writes.
        }
    }
}
