using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;

namespace QuickUp;

/// <summary>
/// Owns the system tray icon, theme/display listeners, single-instance IPC pipe, and lazy window creation.
/// </summary>
internal sealed class TrayAppContext : ApplicationContext
{
    public const string SingleInstanceMutexName = @"Local\QuickUp_DynamicWebPanel_SingleInstance";
    public const string IpcPipeName = "QuickUp_DynamicWebPanel_IPC";

    private const int LeftClickDebounceMs = 280;

    private AppSettings _settings;
    private readonly NotifyIcon _trayIcon;
    private readonly ContextMenuStrip _trayMenu;
    private readonly MessageWatcherControl _watcher;
    private readonly CancellationTokenSource _pipeCts = new();

    private Icon? _currentTrayIcon;
    private PopupForm? _popupForm;
    private SettingsForm? _settingsForm;
    private bool _disposed;

    public TrayAppContext(bool startHidden)
    {
        _settings = AppSettings.Load();
        StartupHelper.Apply(_settings.OpenAtLogin);

        _watcher = new MessageWatcherControl();
        _ = _watcher.Handle; // Create HWND immediately for UI marshaling and WM_SETTINGCHANGE.
        _watcher.ThemeChanged += (_, _) => HandleThemeChanged();

        _trayMenu = new ContextMenuStrip
        {
            Renderer = ThemeHelper.CreateMenuRenderer(ThemeHelper.GetPalette()),
            ShowImageMargin = false
        };
        _trayMenu.Items.Add("Open Web Panel", null, (_, _) => TogglePopup(null));
        _trayMenu.Items.Add("Settings", null, (_, _) => OpenSettings());
        _trayMenu.Items.Add(new ToolStripSeparator());
        _trayMenu.Items.Add("Exit", null, (_, _) => ExitApplication());

        _currentTrayIcon = IconHelper.ResolveTrayIcon(_settings, ThemeHelper.IsTaskbarDarkMode());
        _trayIcon = new NotifyIcon
        {
            Icon = _currentTrayIcon,
            Text = TruncateTooltip(_settings.GetDisplayTitle()),
            ContextMenuStrip = _trayMenu,
            Visible = true
        };
        _trayIcon.MouseUp += TrayIcon_MouseUp;

        SystemEvents.DisplaySettingsChanged += SystemEvents_DisplaySettingsChanged;
        SystemEvents.UserPreferenceChanged += SystemEvents_UserPreferenceChanged;

        _ = RunPipeServerAsync(_pipeCts.Token);

        if (!startHidden)
        {
            _watcher.BeginInvoke(() => ShowPopup(null));
        }
    }

    private void TrayIcon_MouseUp(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;

        if (_popupForm is { IsDisposed: false, Visible: false } &&
            Environment.TickCount64 - _popupForm.LastHideTick < LeftClickDebounceMs)
        {
            return;
        }

        TogglePopup(Cursor.Position);
    }

    private void TogglePopup(Point? anchorPoint)
    {
        PopupForm popup = EnsurePopupForm();
        popup.TogglePopup(anchorPoint);
    }

    private void ShowPopup(Point? anchorPoint)
    {
        PopupForm popup = EnsurePopupForm();
        popup.ShowPopup(anchorPoint);
    }

    private PopupForm EnsurePopupForm()
    {
        if (_popupForm is null || _popupForm.IsDisposed)
        {
            _popupForm = new PopupForm(_settings);
            _popupForm.OpenSettingsRequested += (_, _) => OpenSettings();
        }
        return _popupForm;
    }

    private void OpenSettings()
    {
        if (_settingsForm is { IsDisposed: false })
        {
            if (!_settingsForm.Visible) _settingsForm.Show();
            _settingsForm.Activate();
            _ = NativeMethods.SetForegroundWindow(_settingsForm.Handle);
            return;
        }

        _settingsForm = new SettingsForm(_settings);
        _settingsForm.SettingsSaved += (_, updated) => ApplyNewSettings(updated);
        _settingsForm.FormClosed += (_, _) =>
        {
            _settingsForm?.Dispose();
            _settingsForm = null;
        };
        _settingsForm.Show();
        _settingsForm.Activate();
    }

    private void ApplyNewSettings(AppSettings updated)
    {
        _settings = updated;
        _settings.Save();
        StartupHelper.Apply(_settings.OpenAtLogin);

        RefreshTrayIcon();
        _trayIcon.Text = TruncateTooltip(_settings.GetDisplayTitle());

        if (_popupForm is { IsDisposed: false })
        {
            _popupForm.ApplySettings(_settings);
        }
    }

    private void HandleThemeChanged()
    {
        if (_disposed) return;

        RefreshTrayIcon();
        _trayMenu.Renderer = ThemeHelper.CreateMenuRenderer(ThemeHelper.GetPalette());

        if (_popupForm is { IsDisposed: false })
        {
            _popupForm.ApplyTheme();
        }
        if (_settingsForm is { IsDisposed: false })
        {
            _settingsForm.ApplyTheme();
        }
    }

    private void RefreshTrayIcon()
    {
        Icon next = IconHelper.ResolveTrayIcon(_settings, ThemeHelper.IsTaskbarDarkMode());
        Icon? previous = _currentTrayIcon;
        _currentTrayIcon = next;
        _trayIcon.Icon = next;
        previous?.Dispose();
    }

    private void SystemEvents_DisplaySettingsChanged(object? sender, EventArgs e)
    {
        if (_disposed || !_watcher.IsHandleCreated) return;
        _watcher.BeginInvoke(() =>
        {
            if (_popupForm is { IsDisposed: false, Visible: true })
            {
                _popupForm.ShowPopup(null);
            }
        });
    }

    private void SystemEvents_UserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
    {
        if (_disposed || !_watcher.IsHandleCreated) return;
        if (e.Category is UserPreferenceCategory.General or UserPreferenceCategory.Color or UserPreferenceCategory.VisualStyle)
        {
            _watcher.BeginInvoke(HandleThemeChanged);
        }
    }

    private async Task RunPipeServerAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                using var server = new NamedPipeServerStream(
                    IpcPipeName,
                    PipeDirection.In,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                await server.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);

                using var reader = new StreamReader(server, Encoding.UTF8);
                string? command = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);

                if (string.Equals(command?.Trim(), "SHOW", StringComparison.OrdinalIgnoreCase) &&
                    !_disposed &&
                    _watcher.IsHandleCreated)
                {
                    _watcher.BeginInvoke(() => ShowPopup(null));
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                // Briefly back off before recreating the pipe listener on transient errors.
                try
                {
                    await Task.Delay(150, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    private void ExitApplication()
    {
        if (_disposed) return;
        _disposed = true;

        _pipeCts.Cancel();
        SystemEvents.DisplaySettingsChanged -= SystemEvents_DisplaySettingsChanged;
        SystemEvents.UserPreferenceChanged -= SystemEvents_UserPreferenceChanged;

        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        _currentTrayIcon?.Dispose();
        _currentTrayIcon = null;
        _trayMenu.Dispose();

        _settingsForm?.Dispose();
        _settingsForm = null;

        _popupForm?.Dispose();
        _popupForm = null;

        _watcher.Dispose();
        _pipeCts.Dispose();

        ExitThread();
    }

    private static string TruncateTooltip(string title)
    {
        string value = string.IsNullOrWhiteSpace(title) ? "Dynamic Web Panel" : title.Trim();
        return value.Length <= 63 ? value : value[..60] + "...";
    }

    private sealed class MessageWatcherControl : Control
    {
        private const int WM_SETTINGCHANGE = 0x001A;

        public event EventHandler? ThemeChanged;

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_SETTINGCHANGE)
            {
                string? area = m.LParam != IntPtr.Zero ? Marshal.PtrToStringUni(m.LParam) : null;
                if (area is null || area.Equals("ImmersiveColorSet", StringComparison.OrdinalIgnoreCase))
                {
                    ThemeChanged?.Invoke(this, EventArgs.Empty);
                }
            }

            base.WndProc(ref m);
        }
    }
}
