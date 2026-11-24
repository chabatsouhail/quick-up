using System.ComponentModel;
using System.Diagnostics;
using System.Drawing.Drawing2D;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace QuickUp;

/// <summary>
/// Frameless tray popup hosting a native header bar, onboarding state, and a lazily-created WebView2 control.
/// </summary>
internal sealed class PopupForm : Form
{
    private const int HeaderHeightDip = 48;
    private const int LoadingBarHeightDip = 3;
    private const int TrayGapDip = 12;

    private AppSettings _settings;
    private ThemePalette _palette;
    private float _dpiScale = 1f;

    private readonly Panel _headerPanel;
    private readonly Panel _contentHost;
    private readonly LoadingBarControl _loadingBar;
    private readonly OnboardingPanel _onboardingPanel;
    private readonly ToolTip _toolTip;

    private readonly HeaderIconButton _btnBack;
    private readonly HeaderIconButton _btnForward;
    private readonly HeaderIconButton _btnHome;
    private readonly HeaderIconButton _btnRefresh;
    private readonly HeaderIconButton _btnOpenBrowser;
    private readonly HeaderIconButton _btnPin;

    private readonly System.Windows.Forms.Timer _idleTeardownTimer;

    private Bitmap? _headerIconBitmap;
    private WebView2? _webView;
    private CoreWebView2Environment? _webViewEnvironment;
    private bool _isInitializingWebView;
    private int _webViewGeneration;
    private int _suspendGeneration;
    private string _loadedHomeUrl = string.Empty;
    private bool _isProgrammaticNavigation;
    private bool _isPinned;
    private bool _isContextMenuOpen;
    private string? _initErrorMessage;

    public event EventHandler? OpenSettingsRequested;

    /// <summary>
    /// Timestamp (Environment.TickCount64) of the most recent hide, used to debounce tray left-click reopen.
    /// </summary>
    public long LastHideTick { get; private set; }

    public PopupForm(AppSettings settings)
    {
        _settings = settings.Clone();
        _palette = ThemeHelper.GetPalette();

        AutoScaleMode = AutoScaleMode.None;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        DoubleBuffered = true;
        StartPosition = FormStartPosition.Manual;
        Padding = new Padding(1, 0, 1, 1);
        Icon = IconHelper.GetDefaultAppIcon();

        _toolTip = new ToolTip
        {
            AutoPopDelay = 4000,
            InitialDelay = 450,
            ReshowDelay = 200
        };

        _headerPanel = new HeaderPanel(this)
        {
            Dock = DockStyle.Top,
            Height = HeaderHeightDip
        };

        _btnBack = CreateHeaderButton(TablerIcon.ChevronLeft, "Go Back", (_, _) => GoBack());
        _btnForward = CreateHeaderButton(TablerIcon.ChevronRight, "Go Forward", (_, _) => GoForward());
        _btnHome = CreateHeaderButton(TablerIcon.Home, "Go to Start Page", (_, _) => NavigateHome());
        _btnRefresh = CreateHeaderButton(TablerIcon.Refresh, "Reload Page", (_, _) => ReloadPage());
        _btnOpenBrowser = CreateHeaderButton(TablerIcon.ExternalLink, "Open in Browser", (_, _) => OpenCurrentInBrowser());
        _btnPin = CreateHeaderButton(TablerIcon.Pin, "Pin to Foreground", (_, _) => TogglePin(), TablerIcon.Pinned);

        _btnBack.Enabled = false;
        _btnForward.Enabled = false;

        _headerPanel.Controls.AddRange([_btnBack, _btnForward, _btnHome, _btnRefresh, _btnOpenBrowser, _btnPin]);

        _contentHost = new Panel
        {
            Dock = DockStyle.Fill
        };

        _loadingBar = new LoadingBarControl
        {
            Dock = DockStyle.Top,
            Height = LoadingBarHeightDip,
            Visible = false
        };

        _onboardingPanel = new OnboardingPanel
        {
            Dock = DockStyle.Fill,
            Visible = true
        };
        _onboardingPanel.SetupClicked += (_, _) => OpenSettingsRequested?.Invoke(this, EventArgs.Empty);

        _contentHost.Controls.Add(_onboardingPanel);
        _contentHost.Controls.Add(_loadingBar);

        Controls.Add(_contentHost);
        Controls.Add(_headerPanel);

        _idleTeardownTimer = new System.Windows.Forms.Timer();
        _idleTeardownTimer.Tick += IdleTeardownTimer_Tick;

        Deactivate += PopupForm_Deactivate;
        Paint += PopupForm_Paint;

        RefreshHeaderIconBitmap();
        ApplyTheme();
        UpdateNavButtonStates();
        LayoutScaledControls();
    }

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;
            cp.ClassStyle |= 0x00020000; // CS_DROPSHADOW
            cp.ExStyle |= 0x00000080;    // WS_EX_TOOLWINDOW (exclude from Alt+Tab)
            return cp;
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        NativeMethods.ApplyRoundedCorners(Handle);
        NativeMethods.ApplyBorderColor(Handle, _palette.Border);
    }

    public void ApplySettings(AppSettings settings)
    {
        string oldNormalizedUrl = AppSettings.NormalizeUrlForCompare(_settings.CustomUrl);
        _settings = settings.Clone();
        string newNormalizedUrl = AppSettings.NormalizeUrlForCompare(_settings.CustomUrl);

        RefreshHeaderIconBitmap();
        _headerPanel.Invalidate();
        UpdateNavButtonStates();

        if (Visible)
        {
            PositionNearTray(null);
        }

        if (string.IsNullOrWhiteSpace(_settings.CustomUrl))
        {
            DisposeWebView();
            _loadedHomeUrl = string.Empty;
            _initErrorMessage = null;
            ShowOnboardingState();
            return;
        }

        if (_webView is { IsDisposed: false, CoreWebView2: not null })
        {
            if (!string.Equals(oldNormalizedUrl, newNormalizedUrl, StringComparison.OrdinalIgnoreCase))
            {
                _loadedHomeUrl = _settings.CustomUrl;
                NavigateInternal(_webView.CoreWebView2, _settings.CustomUrl);
            }
        }
        else if (Visible)
        {
            _onboardingPanel.Visible = false;
            _ = EnsureWebViewAndNavigateAsync();
        }
    }

    public void ApplyTheme()
    {
        _palette = ThemeHelper.GetPalette();

        BackColor = _palette.Border;
        _headerPanel.BackColor = _palette.BackgroundPrimary;
        _contentHost.BackColor = _palette.BackgroundPrimary;
        _loadingBar.AccentColor = _palette.AccentBlue;
        _loadingBar.TrackColor = _palette.BackgroundPrimary;

        foreach (HeaderIconButton btn in new[] { _btnBack, _btnForward, _btnHome, _btnRefresh, _btnOpenBrowser, _btnPin })
        {
            btn.UpdatePalette(_palette);
        }

        _onboardingPanel.UpdateTheme(_palette);
        RefreshHeaderIconBitmap();
        _headerPanel.Invalidate();

        if (IsHandleCreated)
        {
            NativeMethods.ApplyDarkTitleBar(Handle, _palette.IsDark);
            NativeMethods.ApplyBorderColor(Handle, _palette.Border);
        }

        if (_webView is { IsDisposed: false, CoreWebView2: not null })
        {
            _webView.DefaultBackgroundColor = _palette.BackgroundPrimary;
        }

        Invalidate();
    }

    public void TogglePopup(Point? anchorPoint = null)
    {
        if (Visible)
        {
            HidePopup();
        }
        else
        {
            ShowPopup(anchorPoint);
        }
    }

    public void ShowPopup(Point? anchorPoint = null)
    {
        _idleTeardownTimer.Stop();
        _suspendGeneration++;

        ApplyTheme();
        PositionNearTray(anchorPoint);

        if (!Visible)
        {
            Show();
        }

        TopMost = true;
        Activate();
        _ = NativeMethods.SetForegroundWindow(Handle);

        if (string.IsNullOrWhiteSpace(_settings.CustomUrl))
        {
            ShowOnboardingState();
            return;
        }

        _onboardingPanel.Visible = false;

        if (_webView is { IsDisposed: false } wv && wv.CoreWebView2 is { } core)
        {
            if (core.IsSuspended)
            {
                core.Resume();
            }

            wv.Visible = true;
            wv.BringToFront();
            _loadingBar.BringToFront();

            if (!string.Equals(
                AppSettings.NormalizeUrlForCompare(_loadedHomeUrl),
                AppSettings.NormalizeUrlForCompare(_settings.CustomUrl),
                StringComparison.OrdinalIgnoreCase))
            {
                _loadedHomeUrl = _settings.CustomUrl;
                NavigateInternal(core, _settings.CustomUrl);
            }

            UpdateNavButtonStates();
            wv.Focus();
        }
        else if (!_isInitializingWebView)
        {
            _ = EnsureWebViewAndNavigateAsync();
        }
    }

    public void HidePopup()
    {
        if (!Visible) return;

        LastHideTick = Environment.TickCount64;
        Hide();
        SetLoading(false);

        if (_webView is { IsDisposed: false } wv)
        {
            wv.Visible = false;
            int suspendGen = ++_suspendGeneration;
            _ = SuspendWebViewAsync(wv, suspendGen);
        }

        StartIdleTeardownTimer();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _idleTeardownTimer.Stop();
            _idleTeardownTimer.Dispose();
            DisposeWebView();
            _headerIconBitmap?.Dispose();
            _headerIconBitmap = null;
            _toolTip.Dispose();
        }
        base.Dispose(disposing);
    }

    private HeaderIconButton CreateHeaderButton(
        TablerIcon icon,
        string tooltip,
        EventHandler onClick,
        TablerIcon? activeIcon = null)
    {
        var btn = new HeaderIconButton(icon, activeIcon);
        btn.Click += onClick;
        _toolTip.SetToolTip(btn, tooltip);
        return btn;
    }

    private void PopupForm_Deactivate(object? sender, EventArgs e)
    {
        if (!_isPinned && !_isContextMenuOpen)
        {
            HidePopup();
        }
    }

    private void PopupForm_Paint(object? sender, PaintEventArgs e)
    {
        using var pen = new Pen(_palette.Border, 1f);
        e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
    }

    private void ShowOnboardingState()
    {
        SetLoading(false);
        _onboardingPanel.SetErrorState(_initErrorMessage);
        _onboardingPanel.Visible = true;
        _onboardingPanel.BringToFront();
        _loadingBar.BringToFront();
        UpdateNavButtonStates();
    }

    private void StartIdleTeardownTimer()
    {
        _idleTeardownTimer.Stop();
        if (_isPinned || _webView is null || _webView.IsDisposed) return;

        if (_settings.IdleTeardownSeconds == 0)
        {
            BeginInvoke(() =>
            {
                if (!Visible && !_isPinned && !_isContextMenuOpen)
                {
                    DisposeWebView();
                }
            });
            return;
        }

        _idleTeardownTimer.Interval = Math.Max(1000, _settings.IdleTeardownSeconds * 1000);
        _idleTeardownTimer.Start();
    }

    private void IdleTeardownTimer_Tick(object? sender, EventArgs e)
    {
        _idleTeardownTimer.Stop();
        if (!Visible && !_isPinned && !_isContextMenuOpen)
        {
            DisposeWebView();
        }
    }

    private async Task SuspendWebViewAsync(WebView2 wv, int suspendGen)
    {
        try
        {
            if (suspendGen != _suspendGeneration || Visible || wv.IsDisposed || wv.CoreWebView2 is null)
            {
                return;
            }
            await wv.CoreWebView2.TrySuspendAsync();
        }
        catch
        {
            // Ignore transient suspension failures when a frame is navigating or tearing down.
        }
    }

    private async Task EnsureWebViewAndNavigateAsync()
    {
        if (_isInitializingWebView || IsDisposed || string.IsNullOrWhiteSpace(_settings.CustomUrl))
        {
            return;
        }

        _isInitializingWebView = true;
        int myGeneration = ++_webViewGeneration;
        _initErrorMessage = null;
        SetLoading(true);

        try
        {
            NativeMethods.ConfigureWebView2LoaderPath();

            var options = new CoreWebView2EnvironmentOptions(
                "--disk-cache-size=52428800 " +
                "--disable-background-networking " +
                "--disable-component-update " +
                "--disable-domain-reliability " +
                "--disable-features=Translate,OptimizationHints,MediaRouter,DialMediaRouteProvider");

            _webViewEnvironment ??= await CoreWebView2Environment.CreateAsync(
                browserExecutableFolder: null,
                userDataFolder: AppSettings.WebViewUserDataPath,
                options: options);

            if (IsDisposed || myGeneration != _webViewGeneration) return;

            var webView = new WebView2
            {
                Dock = DockStyle.Fill,
                DefaultBackgroundColor = _palette.BackgroundPrimary,
                Visible = true
            };

            _contentHost.Controls.Add(webView);
            webView.BringToFront();
            _loadingBar.BringToFront();
            _webView = webView;

            await webView.EnsureCoreWebView2Async(_webViewEnvironment);

            if (IsDisposed || webView.IsDisposed || myGeneration != _webViewGeneration || webView.CoreWebView2 is null)
            {
                if (!webView.IsDisposed)
                {
                    _contentHost.Controls.Remove(webView);
                    webView.Dispose();
                }
                if (ReferenceEquals(_webView, webView))
                {
                    _webView = null;
                }
                return;
            }

            ConfigureCoreWebView(webView.CoreWebView2);

            if (!string.IsNullOrWhiteSpace(_settings.CustomUrl))
            {
                _loadedHomeUrl = _settings.CustomUrl;
                NavigateInternal(webView.CoreWebView2, _settings.CustomUrl);
            }

            if (!Visible && !_isPinned)
            {
                webView.Visible = false;
                int suspendGen = ++_suspendGeneration;
                _ = SuspendWebViewAsync(webView, suspendGen);
                StartIdleTeardownTimer();
            }
            else
            {
                webView.Focus();
            }
        }
        catch (Exception ex)
        {
            DisposeWebView();
            _initErrorMessage = ex is WebView2RuntimeNotFoundException
                ? "Microsoft Edge WebView2 Runtime is required to load pages."
                : $"Failed to initialize WebView2: {ex.Message}";
            ShowOnboardingState();
        }
        finally
        {
            _isInitializingWebView = false;
        }
    }

    private void ConfigureCoreWebView(CoreWebView2 core)
    {
        core.Settings.IsStatusBarEnabled = false;
        core.Settings.IsZoomControlEnabled = true;
        core.Settings.AreBrowserAcceleratorKeysEnabled = true;
#if DEBUG
        core.Settings.AreDevToolsEnabled = true;
#else
        core.Settings.AreDevToolsEnabled = false;
#endif
        core.Profile.PreferredColorScheme = CoreWebView2PreferredColorScheme.Auto;

        core.NavigationStarting += CoreWebView2_NavigationStarting;
        core.NavigationCompleted += CoreWebView2_NavigationCompleted;
        core.HistoryChanged += (_, _) => UpdateNavButtonStates();
        core.SourceChanged += (_, _) => UpdateNavButtonStates();
        core.NewWindowRequested += CoreWebView2_NewWindowRequested;
        core.ContextMenuRequested += CoreWebView2_ContextMenuRequested;
    }

    private bool IsBrowserLinkBehavior() =>
        string.Equals(_settings.LinkBehavior, "browser", StringComparison.OrdinalIgnoreCase);

    private void CoreWebView2_NavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        if (IsBrowserLinkBehavior() &&
            e.IsUserInitiated &&
            !e.IsRedirected &&
            !_isProgrammaticNavigation &&
            e.NavigationKind != CoreWebView2NavigationKind.BackOrForward &&
            e.NavigationKind != CoreWebView2NavigationKind.Reload)
        {
            if (TryOpenExternalUrl(e.Uri))
            {
                e.Cancel = true;
                return;
            }
        }

        _isProgrammaticNavigation = false;
        SetLoading(true);
    }

    private void CoreWebView2_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        _isProgrammaticNavigation = false;
        SetLoading(false);
        UpdateNavButtonStates();
    }

    private void CoreWebView2_NewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
    {
        e.Handled = true;
        bool ctrlHeld = (ModifierKeys & Keys.Control) == Keys.Control;
        bool middleMouse = (MouseButtons & MouseButtons.Middle) == MouseButtons.Middle;

        if (ctrlHeld || middleMouse || IsBrowserLinkBehavior())
        {
            if (!string.IsNullOrWhiteSpace(e.Uri))
            {
                TryOpenExternalUrl(e.Uri);
            }
            return;
        }

        if (_webView?.CoreWebView2 is { } core)
        {
            if (!string.IsNullOrWhiteSpace(e.Uri) &&
                !e.Uri.Equals("about:blank", StringComparison.OrdinalIgnoreCase))
            {
                string targetUri = e.Uri;
                BeginInvoke(() =>
                {
                    if (!IsDisposed && _webView?.CoreWebView2 is { } activeCore)
                    {
                        NavigateInternal(activeCore, targetUri);
                    }
                });
            }
            else
            {
                e.NewWindow = core;
            }
        }
    }

    private void CoreWebView2_ContextMenuRequested(object? sender, CoreWebView2ContextMenuRequestedEventArgs e)
    {
        e.Handled = true;
        CoreWebView2Deferral deferral = e.GetDeferral();
        Point screenLocation = Cursor.Position;

        BeginInvoke(() =>
        {
            try
            {
                ShowWebViewContextMenu(e.ContextMenuTarget, screenLocation);
            }
            finally
            {
                deferral.Complete();
            }
        });
    }

    private void ShowWebViewContextMenu(CoreWebView2ContextMenuTarget target, Point screenLocation)
    {
        if (IsDisposed || _webView?.CoreWebView2 is not { } core) return;

        var menu = new ContextMenuStrip
        {
            Renderer = ThemeHelper.CreateMenuRenderer(_palette),
            ShowImageMargin = false
        };

        var backItem = new ToolStripMenuItem("Back") { Enabled = core.CanGoBack };
        backItem.Click += (_, _) => GoBack();

        var reloadItem = new ToolStripMenuItem("Reload");
        reloadItem.Click += (_, _) => ReloadPage();

        var copyItem = new ToolStripMenuItem("Copy") { Enabled = target.HasSelection };
        copyItem.Click += (_, _) =>
        {
            if (!string.IsNullOrEmpty(target.SelectionText))
            {
                try
                {
                    Clipboard.SetText(target.SelectionText);
                }
                catch
                {
                    FocusWebViewAndSendCtrl((ushort)Keys.C);
                }
            }
            else
            {
                FocusWebViewAndSendCtrl((ushort)Keys.C);
            }
        };

        bool canPaste = false;
        try
        {
            canPaste = target.IsEditable && Clipboard.ContainsText();
        }
        catch
        {
            canPaste = target.IsEditable;
        }

        var pasteItem = new ToolStripMenuItem("Paste") { Enabled = canPaste };
        pasteItem.Click += (_, _) => FocusWebViewAndSendCtrl((ushort)Keys.V);

        var selectAllItem = new ToolStripMenuItem("Select All");
        selectAllItem.Click += (_, _) => FocusWebViewAndSendCtrl((ushort)Keys.A);

        menu.Items.AddRange([
            backItem,
            reloadItem,
            new ToolStripSeparator(),
            copyItem,
            pasteItem,
            selectAllItem
        ]);

        if (target.HasLinkUri && !string.IsNullOrWhiteSpace(target.LinkUri))
        {
            string linkUri = target.LinkUri;
            var openLinkItem = new ToolStripMenuItem("Open Link in Browser");
            openLinkItem.Click += (_, _) => TryOpenExternalUrl(linkUri);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(openLinkItem);
        }

        _isContextMenuOpen = true;
        menu.Closed += (_, _) =>
        {
            BeginInvoke(() =>
            {
                _isContextMenuOpen = false;
                menu.Dispose();
                if (Visible && Form.ActiveForm != this && !_isPinned)
                {
                    HidePopup();
                }
            });
        };

        menu.Show(screenLocation);
    }

    private void FocusWebViewAndSendCtrl(ushort virtualKey)
    {
        if (_webView is { IsDisposed: false } wv)
        {
            Activate();
            wv.Focus();
            BeginInvoke(() => NativeMethods.SendCtrlShortcut(virtualKey));
        }
    }

    private void DisposeWebView()
    {
        _webViewGeneration++;
        _suspendGeneration++;

        if (_webView is not null)
        {
            WebView2 wv = _webView;
            _webView = null;

            try
            {
                if (!wv.IsDisposed)
                {
                    _contentHost.Controls.Remove(wv);
                    wv.Dispose();
                }
            }
            catch
            {
                // Best effort during shutdown.
            }
        }

        _webViewEnvironment = null;
        _loadedHomeUrl = string.Empty;
        UpdateNavButtonStates();
        GC.Collect(2, GCCollectionMode.Optimized, blocking: false);
    }

    private void NavigateInternal(CoreWebView2 core, string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        _isProgrammaticNavigation = true;
        SetLoading(true);
        core.Navigate(url);
    }

    private void GoBack()
    {
        if (_webView?.CoreWebView2 is { CanGoBack: true } core)
        {
            _isProgrammaticNavigation = true;
            core.GoBack();
        }
    }

    private void GoForward()
    {
        if (_webView?.CoreWebView2 is { CanGoForward: true } core)
        {
            _isProgrammaticNavigation = true;
            core.GoForward();
        }
    }

    private void NavigateHome()
    {
        if (string.IsNullOrWhiteSpace(_settings.CustomUrl)) return;

        if (_webView?.CoreWebView2 is { } core)
        {
            _loadedHomeUrl = _settings.CustomUrl;
            NavigateInternal(core, _settings.CustomUrl);
        }
        else if (!_isInitializingWebView)
        {
            _ = EnsureWebViewAndNavigateAsync();
        }
    }

    private void ReloadPage()
    {
        if (_webView?.CoreWebView2 is { } core)
        {
            _isProgrammaticNavigation = true;
            SetLoading(true);
            core.Reload();
        }
        else if (!string.IsNullOrWhiteSpace(_settings.CustomUrl) && !_isInitializingWebView)
        {
            _ = EnsureWebViewAndNavigateAsync();
        }
    }

    private void OpenCurrentInBrowser()
    {
        string targetUrl = _settings.CustomUrl;
        if (_webView?.CoreWebView2 is { } core &&
            !string.IsNullOrWhiteSpace(core.Source) &&
            !core.Source.Equals("about:blank", StringComparison.OrdinalIgnoreCase))
        {
            targetUrl = core.Source;
        }

        if (string.IsNullOrWhiteSpace(targetUrl))
        {
            targetUrl = "https://www.google.com";
        }

        TryOpenExternalUrl(targetUrl);
    }

    private void TogglePin()
    {
        _isPinned = !_isPinned;
        _btnPin.IsActive = _isPinned;
        _toolTip.SetToolTip(_btnPin, _isPinned ? "Unpin from Foreground" : "Pin to Foreground");
        if (_isPinned)
        {
            _idleTeardownTimer.Stop();
            TopMost = true;
        }
    }

    private void UpdateNavButtonStates()
    {
        bool canBack = _webView?.CoreWebView2?.CanGoBack ?? false;
        bool canForward = _webView?.CoreWebView2?.CanGoForward ?? false;
        bool hasUrl = !string.IsNullOrWhiteSpace(_settings.CustomUrl);

        _btnBack.Enabled = canBack;
        _btnForward.Enabled = canForward;
        _btnHome.Enabled = hasUrl;
        _btnRefresh.Enabled = hasUrl;
    }

    private void SetLoading(bool loading)
    {
        if (IsDisposed) return;
        _loadingBar.SetActive(loading);
    }

    private static bool TryOpenExternalUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri)) return false;
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) return false;

        try
        {
            Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void RefreshHeaderIconBitmap()
    {
        _headerIconBitmap?.Dispose();
        _headerIconBitmap = IconHelper.ResolveHeaderBitmap(_settings, _palette.IsDark);
    }

    private void PositionNearTray(Point? anchorPoint)
    {
        NativeMethods.GetTaskbarPosition(out TaskbarEdge globalEdge, out Rectangle globalTaskbar);
        Point anchor = anchorPoint ?? GetDefaultTrayAnchor(globalEdge, globalTaskbar);
        Screen screen = Screen.FromPoint(anchor);

        int dpi = NativeMethods.GetDpiForPoint(anchor);
        float scale = dpi / 96f;

        if (Math.Abs(_dpiScale - scale) > 0.01f)
        {
            _dpiScale = scale;
            LayoutScaledControls();
        }

        int width = (int)Math.Round(_settings.PopupWidth * scale);
        int height = (int)Math.Round(AppSettings.PopupHeight * scale);
        int gap = (int)Math.Round(TrayGapDip * scale);

        Rectangle bounds = screen.Bounds;
        Rectangle work = screen.WorkingArea;
        TaskbarEdge edge = DetectScreenTaskbarEdge(bounds, work, globalEdge);

        int x;
        int y;

        switch (edge)
        {
            case TaskbarEdge.Top:
                x = anchor.X - width / 2;
                int topBaseline = work.Top > bounds.Top ? work.Top : (bounds.Contains(globalTaskbar) ? globalTaskbar.Bottom : bounds.Top);
                y = topBaseline + gap;
                break;

            case TaskbarEdge.Left:
                int leftBaseline = work.Left > bounds.Left ? work.Left : (bounds.Contains(globalTaskbar) ? globalTaskbar.Right : bounds.Left);
                x = leftBaseline + gap;
                y = anchor.Y - height / 2;
                break;

            case TaskbarEdge.Right:
                int rightBaseline = work.Right < bounds.Right ? work.Right : (bounds.Contains(globalTaskbar) ? globalTaskbar.Left : bounds.Right);
                x = rightBaseline - width - gap;
                y = anchor.Y - height / 2;
                break;

            default: // Bottom
                x = anchor.X - width / 2;
                int bottomBaseline = work.Bottom < bounds.Bottom ? work.Bottom : (bounds.Contains(globalTaskbar) ? globalTaskbar.Top : bounds.Bottom);
                y = bottomBaseline - height - gap;
                break;
        }

        x = Math.Clamp(x, work.Left, Math.Max(work.Left, work.Right - width));
        y = Math.Clamp(y, work.Top, Math.Max(work.Top, work.Bottom - height));

        Bounds = new Rectangle(x, y, width, height);
        LayoutHeaderButtons();
    }

    private static TaskbarEdge DetectScreenTaskbarEdge(Rectangle bounds, Rectangle work, TaskbarEdge fallback)
    {
        if (work.Bottom < bounds.Bottom) return TaskbarEdge.Bottom;
        if (work.Top > bounds.Top) return TaskbarEdge.Top;
        if (work.Left > bounds.Left) return TaskbarEdge.Left;
        if (work.Right < bounds.Right) return TaskbarEdge.Right;
        return fallback;
    }

    private static Point GetDefaultTrayAnchor(TaskbarEdge edge, Rectangle taskbar)
    {
        return edge switch
        {
            TaskbarEdge.Top => new Point(taskbar.Right - 140, taskbar.Bottom),
            TaskbarEdge.Left => new Point(taskbar.Right, taskbar.Bottom - 140),
            TaskbarEdge.Right => new Point(taskbar.Left, taskbar.Bottom - 140),
            _ => new Point(taskbar.Right - 140, taskbar.Top)
        };
    }

    private void LayoutScaledControls()
    {
        _headerPanel.Height = (int)Math.Round(HeaderHeightDip * _dpiScale);
        _loadingBar.Height = Math.Max(2, (int)Math.Round(LoadingBarHeightDip * _dpiScale));
        _onboardingPanel.SetDpiScale(_dpiScale);
        LayoutHeaderButtons();
    }

    private void LayoutHeaderButtons()
    {
        if (_btnBack is null || _btnForward is null || _btnHome is null ||
            _btnRefresh is null || _btnOpenBrowser is null || _btnPin is null)
        {
            return;
        }

        int btnSize = Math.Max(22, (int)Math.Round(28 * _dpiScale));
        int gap = Math.Max(2, (int)Math.Round(4 * _dpiScale));
        int rightPad = Math.Max(8, (int)Math.Round(12 * _dpiScale));
        int top = (_headerPanel.Height - btnSize) / 2;

        HeaderIconButton[] buttons = [_btnBack, _btnForward, _btnHome, _btnRefresh, _btnOpenBrowser, _btnPin];
        int x = _headerPanel.ClientSize.Width - rightPad;

        for (int i = buttons.Length - 1; i >= 0; i--)
        {
            x -= btnSize;
            buttons[i].SetBounds(x, top, btnSize, btnSize);
            buttons[i].IconScale = _dpiScale;
            x -= gap;
        }
    }

    // -------------------------------------------------------------------------
    // Custom Controls (Header bar, vector buttons, loading bar, onboarding)
    // -------------------------------------------------------------------------

    private sealed class HeaderPanel : Panel
    {
        private readonly PopupForm _owner;

        public HeaderPanel(PopupForm owner)
        {
            _owner = owner;
            DoubleBuffered = true;
            Resize += (_, _) => _owner.LayoutHeaderButtons();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;

            float s = _owner._dpiScale;
            ThemePalette p = _owner._palette;

            int leftPad = (int)Math.Round(12 * s);
            int iconSize = (int)Math.Round(20 * s);
            int iconY = (Height - iconSize) / 2;

            if (_owner._headerIconBitmap is not null)
            {
                var iconRect = new Rectangle(leftPad, iconY, iconSize, iconSize);
                using GraphicsPath clip = IconHelper.CreateRoundedRect(iconRect, 4 * s);
                GraphicsState state = g.Save();
                g.SetClip(clip);
                g.DrawImage(_owner._headerIconBitmap, iconRect);
                g.Restore(state);
            }

            int titleX = leftPad + iconSize + (int)Math.Round(10 * s);
            int rightLimit = _owner._btnBack.Left - (int)Math.Round(8 * s);
            int titleWidth = Math.Max(20, rightLimit - titleX);

            using var titleFont = new Font("Segoe UI", 13f * s, FontStyle.Bold, GraphicsUnit.Pixel);
            var titleRect = new Rectangle(titleX, 0, titleWidth, Height);
            TextRenderer.DrawText(
                g,
                _owner._settings.GetDisplayTitle(),
                titleFont,
                titleRect,
                p.TextPrimary,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

            using var dividerPen = new Pen(p.Border, 1f);
            g.DrawLine(dividerPen, 0, Height - 1, Width, Height - 1);
        }
    }

    private sealed class HeaderIconButton : Control
    {
        private readonly TablerIcon _icon;
        private readonly TablerIcon? _activeIcon;
        private ThemePalette _palette = ThemeHelper.Light;
        private bool _isHovered;
        private bool _isPressed;
        private bool _isActive;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public float IconScale { get; set; } = 1f;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool IsActive
        {
            get => _isActive;
            set
            {
                if (_isActive != value)
                {
                    _isActive = value;
                    Invalidate();
                }
            }
        }

        public HeaderIconButton(TablerIcon icon, TablerIcon? activeIcon = null)
        {
            _icon = icon;
            _activeIcon = activeIcon;
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.UserPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.SupportsTransparentBackColor,
                true);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
        }

        public void UpdatePalette(ThemePalette palette)
        {
            _palette = palette;
            Invalidate();
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            base.OnEnabledChanged(e);
            Cursor = Enabled ? Cursors.Hand : Cursors.Default;
            Invalidate();
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _isHovered = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _isHovered = false;
            _isPressed = false;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left && Enabled)
            {
                _isPressed = true;
                Invalidate();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (_isPressed)
            {
                _isPressed = false;
                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            if (Enabled && (_isHovered || _isActive || _isPressed))
            {
                Color bg = _isPressed
                    ? Color.FromArgb(_palette.IsDark ? 72 : 216, _palette.BackgroundHover)
                    : _palette.BackgroundHover;
                using var bgBrush = new SolidBrush(bg);
                g.FillEllipse(bgBrush, 1, 1, Width - 2, Height - 2);
            }

            Color fg = !Enabled
                ? Color.FromArgb(90, _palette.TextSecondary)
                : _isActive
                    ? _palette.AccentBlue
                    : _isHovered
                        ? _palette.TextPrimary
                        : _palette.TextSecondary;

            float iconSize = 16f * IconScale;
            float ox = (Width - iconSize) / 2f;
            float oy = (Height - iconSize) / 2f;
            TablerIcon activeGlyph = (_isActive && _activeIcon.HasValue) ? _activeIcon.Value : _icon;
            TablerIcons.Draw(g, activeGlyph, ox, oy, iconSize, fg, strokeWidth24: 1.9f);
        }
    }

    private sealed class LoadingBarControl : Control
    {
        private readonly System.Windows.Forms.Timer _animTimer;
        private float _progress;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color AccentColor { get; set; } = ThemeHelper.Light.AccentBlue;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color TrackColor { get; set; } = ThemeHelper.Light.BackgroundPrimary;

        public LoadingBarControl()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.UserPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw,
                true);

            _animTimer = new System.Windows.Forms.Timer { Interval = 16 };
            _animTimer.Tick += (_, _) =>
            {
                _progress += 0.025f;
                if (_progress > 1.35f) _progress = -0.35f;
                Invalidate();
            };
        }

        public void SetActive(bool active)
        {
            if (active)
            {
                if (!Visible)
                {
                    _progress = -0.35f;
                    Visible = true;
                }
                if (!_animTimer.Enabled)
                {
                    _animTimer.Start();
                }
            }
            else
            {
                _animTimer.Stop();
                Visible = false;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            using var trackBrush = new SolidBrush(TrackColor);
            e.Graphics.FillRectangle(trackBrush, ClientRectangle);

            float barWidth = Width * 0.35f;
            float x = _progress * Width;
            using var barBrush = new SolidBrush(AccentColor);
            e.Graphics.FillRectangle(barBrush, x, 0, barWidth, Height);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _animTimer.Stop();
                _animTimer.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    private sealed class OnboardingPanel : Control
    {
        private ThemePalette _palette = ThemeHelper.Light;
        private float _scale = 1f;
        private string? _errorMessage;
        private RectangleF _ctaRect;
        private bool _ctaHovered;
        private bool _ctaPressed;

        public event EventHandler? SetupClicked;

        public OnboardingPanel()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.UserPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw,
                true);
        }

        public void UpdateTheme(ThemePalette palette)
        {
            _palette = palette;
            Invalidate();
        }

        public void SetDpiScale(float scale)
        {
            _scale = scale;
            Invalidate();
        }

        public void SetErrorState(string? errorMessage)
        {
            _errorMessage = errorMessage;
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            bool over = _ctaRect.Contains(e.Location);
            if (over != _ctaHovered)
            {
                _ctaHovered = over;
                Cursor = over ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_ctaHovered || _ctaPressed)
            {
                _ctaHovered = false;
                _ctaPressed = false;
                Cursor = Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left && _ctaRect.Contains(e.Location))
            {
                _ctaPressed = true;
                Invalidate();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            bool wasPressed = _ctaPressed;
            _ctaPressed = false;
            Invalidate();

            if (wasPressed && _ctaRect.Contains(e.Location))
            {
                SetupClicked?.Invoke(this, EventArgs.Empty);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            using var bgBrush = new SolidBrush(_palette.BackgroundPrimary);
            g.FillRectangle(bgBrush, ClientRectangle);

            float s = _scale;
            float tileSize = 88f * s;
            float contentWidth = Math.Min(Width - 48f * s, 300f * s);
            float centerX = Width / 2f;

            float totalHeight = 88f * s + 22f * s + 30f * s + 10f * s + 52f * s + 24f * s + 38f * s;
            float startY = Math.Max(24f * s, (Height - totalHeight) / 2f);

            // Squircle icon tile
            var tileRect = new RectangleF(centerX - tileSize / 2f, startY, tileSize, tileSize);
            using (GraphicsPath tilePath = IconHelper.CreateRoundedRect(tileRect, 20f * s))
            using (var tileBrush = new SolidBrush(_palette.BackgroundSecondary))
            using (var tilePen = new Pen(_palette.Border, 1f))
            {
                g.FillPath(tileBrush, tilePath);
                g.DrawPath(tilePen, tilePath);
            }

            float heroIconSize = 44f * s;
            TablerIcons.Draw(
                g,
                TablerIcon.Settings,
                centerX - heroIconSize / 2f,
                startY + (tileSize - heroIconSize) / 2f,
                heroIconSize,
                _errorMessage is null ? _palette.TextPrimary : _palette.Danger,
                strokeWidth24: 1.75f);

            // Title
            float titleY = startY + tileSize + 22f * s;
            using var titleFont = new Font("Segoe UI", 20f * s, FontStyle.Bold, GraphicsUnit.Pixel);
            var titleRect = Rectangle.Round(new RectangleF(centerX - contentWidth / 2f, titleY, contentWidth, 32f * s));
            TextRenderer.DrawText(
                g,
                _errorMessage is null ? "Dynamic Web Panel" : "Unable to Load WebView2",
                titleFont,
                titleRect,
                _palette.TextPrimary,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

            // Subtitle
            float subY = titleY + 34f * s;
            string subtitle = _errorMessage ?? "Streamline your workspace. Open any web application directly from your Windows system tray.";
            using var subFont = new Font("Segoe UI", 13f * s, FontStyle.Regular, GraphicsUnit.Pixel);
            var subRect = Rectangle.Round(new RectangleF(centerX - contentWidth / 2f, subY, contentWidth, 56f * s));
            TextRenderer.DrawText(
                g,
                subtitle,
                subFont,
                subRect,
                _palette.TextSecondary,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.WordBreak);

            // Primary pill button
            float btnW = 164f * s;
            float btnH = 38f * s;
            float btnY = subY + 66f * s;
            _ctaRect = new RectangleF(centerX - btnW / 2f, btnY, btnW, btnH);

            Color btnFill = _ctaPressed || _ctaHovered ? _palette.AccentBlueHover : _palette.AccentBlue;
            using (GraphicsPath btnPath = IconHelper.CreateRoundedRect(_ctaRect, btnH / 2f))
            using (var btnBrush = new SolidBrush(btnFill))
            {
                g.FillPath(btnBrush, btnPath);
            }

            float btnIconSize = 16f * s;
            TablerIcons.Draw(
                g,
                TablerIcon.Settings,
                _ctaRect.Left + 18f * s,
                _ctaRect.Top + (btnH - btnIconSize) / 2f,
                btnIconSize,
                _palette.TextOnPrimary,
                strokeWidth24: 2.1f);

            using var btnFont = new Font("Segoe UI", 13.5f * s, FontStyle.Bold, GraphicsUnit.Pixel);
            var btnTextRect = Rectangle.Round(new RectangleF(_ctaRect.Left + 34f * s, _ctaRect.Top, btnW - 44f * s, btnH));
            TextRenderer.DrawText(
                g,
                "Set custom URL",
                btnFont,
                btnTextRect,
                _palette.TextOnPrimary,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }
}
