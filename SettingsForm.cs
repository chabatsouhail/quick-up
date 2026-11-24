using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Drawing.Drawing2D;

namespace QuickUp;

/// <summary>
/// Antigravity-inspired two-pane native WinForms settings window using Tabler outline icons
/// and explicit DPI-scaled dynamic stacking to prevent clipping or overlap at any display scale.
/// </summary>
internal sealed class SettingsForm : Form
{
    private enum SettingsSection
    {
        General,
        Appearance,
        Behavior
    }

    private readonly AppSettings _originalSettings;
    private ThemePalette _palette;
    private SettingsSection _activeSection = SettingsSection.General;
    private float _s = 1f;

    private string _customIconLight = string.Empty;
    private bool _isPresetLight;
    private string _customIconDark = string.Empty;
    private bool _isPresetDark;
    private string _activeIconTab = "light";
    private string? _selectedPresetLight;
    private string? _selectedPresetDark;

    private readonly Bitmap _brandBitmap;

    // Sidebar
    private readonly SidebarPanel _sidebarPanel;
    private readonly NavRailButton _navGeneral;
    private readonly NavRailButton _navAppearance;
    private readonly NavRailButton _navBehavior;
    private readonly Label _lblSidebarVersion;

    // Right pane
    private readonly Panel _mainPane;
    private readonly FooterPanel _footerPanel;
    private readonly Label _lblFooterHint;
    private readonly ModernButton _btnCancel;
    private readonly ModernButton _btnSave;
    private readonly Panel _scrollHost;

    // Section panels
    private readonly Panel _secGeneral;
    private readonly Panel _secAppearance;
    private readonly Panel _secBehavior;

    // General controls
    private readonly Label _lblGenHeading;
    private readonly Label _lblGenSub;
    private readonly RoundedCard _cardWebApp;
    private readonly Label _lblUrlTitle;
    private readonly Label _lblUrlDesc;
    private readonly OutlinedTextBox _txtUrl;
    private readonly Panel _webAppDivider;
    private readonly Label _lblTitleHeading;
    private readonly Label _lblTitleDesc;
    private readonly OutlinedTextBox _txtTitle;
    private readonly RoundedCard _cardSystem;
    private readonly Label _lblStartupHeading;
    private readonly Label _lblStartupDesc;
    private readonly ToggleSwitch _tglStartup;

    // Appearance controls
    private readonly Label _lblAppHeading;
    private readonly Label _lblAppSub;
    private readonly RoundedCard _cardTrayIcon;
    private readonly Label _lblTrayIconHeading;
    private readonly SegmentedControl<string> _segIconTheme;
    private readonly PreviewTilePanel _previewBox;
    private readonly PictureBox _picPreview;
    private readonly ModernButton _btnUpload;
    private readonly ModernButton _btnResetIcon;
    private readonly Label _lblPresets;
    private readonly List<PresetIconButton> _presetButtons = [];
    private readonly RoundedCard _cardDimensions;
    private readonly Label _lblWidthHeading;
    private readonly Label _lblWidthDesc;
    private readonly SegmentedControl<int> _segWidth;

    // Behavior controls
    private readonly Label _lblBehHeading;
    private readonly Label _lblBehSub;
    private readonly RoundedCard _cardLinks;
    private readonly Label _lblLinkHeading;
    private readonly Label _lblLinkDesc;
    private readonly SegmentedControl<string> _segLinkBehavior;
    private readonly RoundedCard _cardMemory;
    private readonly IconHeadingRow _hdrMemory;
    private readonly Label _lblMemoryDesc;
    private readonly SegmentedControl<int> _segIdleTeardown;

    public event EventHandler<AppSettings>? SettingsSaved;

    public SettingsForm(AppSettings currentSettings)
    {
        _originalSettings = currentSettings.Clone();
        _palette = ThemeHelper.GetPalette();
        _brandBitmap = IconHelper.CreateDefaultBitmapCopy();

        _customIconLight = _originalSettings.CustomIcon;
        _isPresetLight = _originalSettings.IsPresetIcon;
        _customIconDark = _originalSettings.CustomIconDark;
        _isPresetDark = _originalSettings.IsPresetIconDark;

        Screen targetScreen = Screen.FromPoint(Cursor.Position);
        Point screenCenter = new(
            targetScreen.WorkingArea.Left + targetScreen.WorkingArea.Width / 2,
            targetScreen.WorkingArea.Top + targetScreen.WorkingArea.Height / 2);
        int targetDpi = NativeMethods.GetDpiForPoint(screenCenter);
        _s = Math.Max(1f, Math.Max(DeviceDpi, targetDpi) / 96f);

        AutoScaleMode = AutoScaleMode.None;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.Manual;
        Text = "Settings — Dynamic Web Panel";
        Icon = IconHelper.GetDefaultAppIcon();
        Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);

        // ---------------------------------------------------------------------
        // Left Sidebar
        // ---------------------------------------------------------------------
        _sidebarPanel = new SidebarPanel(_brandBitmap) { Dock = DockStyle.Left };

        _navGeneral = new NavRailButton(TablerIcon.AdjustmentsHorizontal, "General");
        _navGeneral.Click += (_, _) => SwitchSection(SettingsSection.General);

        _navAppearance = new NavRailButton(TablerIcon.Palette, "Appearance");
        _navAppearance.Click += (_, _) => SwitchSection(SettingsSection.Appearance);

        _navBehavior = new NavRailButton(TablerIcon.AppWindow, "Behavior");
        _navBehavior.Click += (_, _) => SwitchSection(SettingsSection.Behavior);

        _lblSidebarVersion = new Label
        {
            Text = "v2.0 · .NET 10 · WebView2",
            Dock = DockStyle.Bottom,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true,
            Font = new Font("Segoe UI", 8f, FontStyle.Regular)
        };

        _sidebarPanel.Controls.AddRange([_navGeneral, _navAppearance, _navBehavior, _lblSidebarVersion]);

        // ---------------------------------------------------------------------
        // Right Pane (Footer + Scrollable Section Host)
        // ---------------------------------------------------------------------
        _mainPane = new Panel { Dock = DockStyle.Fill };

        _footerPanel = new FooterPanel { Dock = DockStyle.Bottom };

        _lblFooterHint = new Label
        {
            Text = "Changes apply immediately after saving",
            AutoEllipsis = true,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Regular)
        };

        _btnCancel = new ModernButton(ModernButtonKind.Secondary)
        {
            Text = "Cancel",
            DialogResult = DialogResult.Cancel
        };
        _btnCancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };

        _btnSave = new ModernButton(ModernButtonKind.Primary)
        {
            Text = "Save Changes"
        };
        _btnSave.Click += BtnSave_Click;

        _footerPanel.Controls.AddRange([_lblFooterHint, _btnCancel, _btnSave]);
        _footerPanel.Resize += (_, _) => LayoutFooter();

        _scrollHost = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true
        };

        // ---------------------------------------------------------------------
        // Section 1: General
        // ---------------------------------------------------------------------
        _secGeneral = new Panel();
        _lblGenHeading = CreateSectionHeading("General");
        _lblGenSub = CreateSectionSubtitle("Configure the web application URL, title, and startup preference.");

        _cardWebApp = new RoundedCard();
        _lblUrlTitle = CreateFieldHeading("Link URL");
        _lblUrlDesc = CreateFieldDescription("Web address (https://) to load inside the popup panel.");
        _txtUrl = new OutlinedTextBox(TablerIcon.Link)
        {
            PlaceholderText = "https://notion.so",
            Text = _originalSettings.CustomUrl
        };

        _webAppDivider = new Panel();

        _lblTitleHeading = CreateFieldHeading("Panel Title");
        _lblTitleDesc = CreateFieldDescription("Custom title shown in the panel header and tray tooltip.");
        _txtTitle = new OutlinedTextBox(null)
        {
            PlaceholderText = "Dynamic Web Panel",
            Text = _originalSettings.CustomTitle
        };

        _cardWebApp.Controls.AddRange([
            _lblUrlTitle, _lblUrlDesc, _txtUrl,
            _webAppDivider,
            _lblTitleHeading, _lblTitleDesc, _txtTitle
        ]);

        _cardSystem = new RoundedCard();
        _lblStartupHeading = CreateFieldHeading("Start with Windows");
        _lblStartupDesc = CreateFieldDescription("Launch silently in the tray when you sign in to Windows.");
        _tglStartup = new ToggleSwitch { Checked = _originalSettings.OpenAtLogin };
        _cardSystem.Controls.AddRange([_lblStartupHeading, _lblStartupDesc, _tglStartup]);

        _secGeneral.Controls.AddRange([_lblGenHeading, _lblGenSub, _cardWebApp, _cardSystem]);

        // ---------------------------------------------------------------------
        // Section 2: Appearance
        // ---------------------------------------------------------------------
        _secAppearance = new Panel { Visible = false };
        _lblAppHeading = CreateSectionHeading("Appearance");
        _lblAppSub = CreateSectionSubtitle("Customize your taskbar tray icon and popup panel width.");

        _cardTrayIcon = new RoundedCard();
        _lblTrayIconHeading = CreateFieldHeading("System Tray Icon");

        _segIconTheme = new SegmentedControl<string>(
        [
            new SegmentOption<string>("Light Taskbar", "light", TablerIcon.Sun),
            new SegmentOption<string>("Dark Taskbar", "dark", TablerIcon.Moon)
        ])
        {
            SelectedValue = "light"
        };
        _segIconTheme.SelectedValueChanged += (_, _) =>
        {
            if (_segIconTheme.SelectedValue is string tab)
            {
                SwitchIconTab(tab);
            }
        };

        _previewBox = new PreviewTilePanel();
        _picPreview = new PictureBox
        {
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.Transparent
        };
        _previewBox.Controls.Add(_picPreview);

        _btnUpload = new ModernButton(ModernButtonKind.Secondary, TablerIcon.Upload)
        {
            Text = "Upload Image"
        };
        _btnUpload.Click += BtnUpload_Click;

        _btnResetIcon = new ModernButton(ModernButtonKind.GhostDanger, TablerIcon.Restore)
        {
            Text = "Restore Default"
        };
        _btnResetIcon.Click += BtnResetIcon_Click;

        _lblPresets = CreateFieldDescription("Or choose a Tabler preset icon:");

        foreach (string key in IconHelper.PresetKeys)
        {
            var pBtn = new PresetIconButton(key);
            pBtn.Click += (_, _) => SelectPreset(key);
            _presetButtons.Add(pBtn);
            _cardTrayIcon.Controls.Add(pBtn);
        }

        _cardTrayIcon.Controls.AddRange([
            _lblTrayIconHeading, _segIconTheme, _previewBox, _btnUpload, _btnResetIcon, _lblPresets
        ]);

        _cardDimensions = new RoundedCard();
        _lblWidthHeading = CreateFieldHeading("Popup Width");
        _lblWidthDesc = CreateFieldDescription("Width of the tray popup panel in logical pixels.");

        var widthOptions = new List<SegmentOption<int>>
        {
            new("Compact (400px)", 400),
            new("Medium (500px)", 500),
            new("Wide (600px)", 600)
        };
        if (_originalSettings.PopupWidth is not (400 or 500 or 600))
        {
            widthOptions.Add(new($"Custom ({_originalSettings.PopupWidth}px)", _originalSettings.PopupWidth));
        }

        _segWidth = new SegmentedControl<int>(widthOptions)
        {
            SelectedValue = _originalSettings.PopupWidth
        };

        _cardDimensions.Controls.AddRange([_lblWidthHeading, _lblWidthDesc, _segWidth]);
        _secAppearance.Controls.AddRange([_lblAppHeading, _lblAppSub, _cardTrayIcon, _cardDimensions]);

        // ---------------------------------------------------------------------
        // Section 3: Behavior
        // ---------------------------------------------------------------------
        _secBehavior = new Panel { Visible = false };
        _lblBehHeading = CreateSectionHeading("Behavior");
        _lblBehSub = CreateSectionSubtitle("Control how links open and how memory is reclaimed while idle.");

        _cardLinks = new RoundedCard();
        _lblLinkHeading = CreateFieldHeading("Link Click Behavior");
        _lblLinkDesc = CreateFieldDescription("Ctrl+Click or middle-click always opens in your default browser.");
        _segLinkBehavior = new SegmentedControl<string>(
        [
            new SegmentOption<string>("Inside Panel", "popup"),
            new SegmentOption<string>("Default Browser", "browser")
        ])
        {
            SelectedValue = _originalSettings.LinkBehavior == "browser" ? "browser" : "popup"
        };
        _cardLinks.Controls.AddRange([_lblLinkHeading, _lblLinkDesc, _segLinkBehavior]);

        _cardMemory = new RoundedCard();
        _hdrMemory = new IconHeadingRow(TablerIcon.Bolt, "Idle WebView Teardown");
        _lblMemoryDesc = CreateFieldDescription("Disposes WebView2 after staying hidden so idle RAM returns to near zero.");

        var teardownOptions = new List<SegmentOption<int>>
        {
            new("Instant", 0),
            new("15s", 15),
            new("30s", 30),
            new("60s", 60)
        };
        if (_originalSettings.IdleTeardownSeconds is not (0 or 15 or 30 or 60))
        {
            teardownOptions.Add(new($"{_originalSettings.IdleTeardownSeconds}s", _originalSettings.IdleTeardownSeconds));
        }

        _segIdleTeardown = new SegmentedControl<int>(teardownOptions)
        {
            SelectedValue = _originalSettings.IdleTeardownSeconds
        };
        _cardMemory.Controls.AddRange([_hdrMemory, _lblMemoryDesc, _segIdleTeardown]);

        _secBehavior.Controls.AddRange([_lblBehHeading, _lblBehSub, _cardLinks, _cardMemory]);

        // ---------------------------------------------------------------------
        // Assemble & Layout
        // ---------------------------------------------------------------------
        _scrollHost.Controls.AddRange([_secGeneral, _secAppearance, _secBehavior]);
        _mainPane.Controls.Add(_scrollHost);
        _mainPane.Controls.Add(_footerPanel);

        Controls.Add(_mainPane);
        Controls.Add(_sidebarPanel);

        AcceptButton = _btnSave;
        CancelButton = _btnCancel;

        LayoutAllControls();
        CenterOnScreen(targetScreen);

        Shown += (_, _) =>
        {
            _txtUrl.ClearSelection();
            _txtTitle.ClearSelection();
        };

        SwitchSection(SettingsSection.General);
        ApplyTheme();
        RefreshPreviewImage();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        NativeMethods.ApplyDarkTitleBar(Handle, _palette.IsDark);

        float actualScale = Math.Max(1f, DeviceDpi / 96f);
        if (Math.Abs(actualScale - _s) > 0.02f)
        {
            _s = actualScale;
            LayoutAllControls();
            CenterOnScreen(Screen.FromHandle(Handle));
        }
    }

    protected override void OnDpiChanged(DpiChangedEventArgs e)
    {
        base.OnDpiChanged(e);
        _s = Math.Max(1f, e.DeviceDpiNew / 96f);
        LayoutAllControls();
    }

    public void ApplyTheme()
    {
        _palette = ThemeHelper.GetPalette();

        if (IsHandleCreated)
        {
            NativeMethods.ApplyDarkTitleBar(Handle, _palette.IsDark);
        }

        BackColor = _palette.CanvasBackground;
        ForeColor = _palette.TextPrimary;

        _sidebarPanel.UpdateTheme(_palette, _s);
        _navGeneral.UpdateTheme(_palette, _s);
        _navAppearance.UpdateTheme(_palette, _s);
        _navBehavior.UpdateTheme(_palette, _s);
        _lblSidebarVersion.ForeColor = _palette.TextMuted;

        _mainPane.BackColor = _palette.CanvasBackground;
        _scrollHost.BackColor = _palette.CanvasBackground;
        _secGeneral.BackColor = _palette.CanvasBackground;
        _secAppearance.BackColor = _palette.CanvasBackground;
        _secBehavior.BackColor = _palette.CanvasBackground;

        _footerPanel.UpdateTheme(_palette);
        _lblFooterHint.ForeColor = _palette.TextMuted;
        _btnCancel.UpdateTheme(_palette, _s);
        _btnSave.UpdateTheme(_palette, _s);

        foreach (Label h in new[] { _lblGenHeading, _lblAppHeading, _lblBehHeading })
        {
            h.ForeColor = _palette.TextPrimary;
        }
        foreach (Label sub in new[] { _lblGenSub, _lblAppSub, _lblBehSub })
        {
            sub.ForeColor = _palette.TextSecondary;
        }

        foreach (RoundedCard card in new[] { _cardWebApp, _cardSystem, _cardTrayIcon, _cardDimensions, _cardLinks, _cardMemory })
        {
            card.UpdateTheme(_palette, _s);
        }

        foreach (Label fh in new[] { _lblUrlTitle, _lblTitleHeading, _lblStartupHeading, _lblTrayIconHeading, _lblWidthHeading, _lblLinkHeading })
        {
            fh.ForeColor = _palette.TextPrimary;
        }
        foreach (Label fd in new[] { _lblUrlDesc, _lblTitleDesc, _lblStartupDesc, _lblPresets, _lblWidthDesc, _lblLinkDesc, _lblMemoryDesc })
        {
            fd.ForeColor = _palette.TextSecondary;
        }

        _webAppDivider.BackColor = _palette.Border;
        _txtUrl.UpdateTheme(_palette, _s);
        _txtTitle.UpdateTheme(_palette, _s);
        _tglStartup.UpdateTheme(_palette, _s);

        _segIconTheme.UpdateTheme(_palette, _s);
        UpdatePreviewContainerColor();
        _btnUpload.UpdateTheme(_palette, _s);
        _btnResetIcon.UpdateTheme(_palette, _s);
        foreach (PresetIconButton btn in _presetButtons)
        {
            btn.UpdateTheme(_palette, _s);
        }

        _segWidth.UpdateTheme(_palette, _s);
        _segLinkBehavior.UpdateTheme(_palette, _s);
        _hdrMemory.UpdateTheme(_palette, _s);
        _segIdleTeardown.UpdateTheme(_palette, _s);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Image? old = _picPreview.Image;
            _picPreview.Image = null;
            old?.Dispose();
            _brandBitmap.Dispose();
        }
        base.Dispose(disposing);
    }

    private int Dp(float dip) => (int)Math.Round(dip * _s);

    private void CenterOnScreen(Screen screen)
    {
        Rectangle area = screen.WorkingArea;
        int x = area.Left + Math.Max(0, (area.Width - Width) / 2);
        int y = area.Top + Math.Max(0, (area.Height - Height) / 2);
        Location = new Point(x, y);
    }

    private void LayoutAllControls()
    {
        int clientW = Dp(740);
        int clientH = Dp(580);
        int sidebarW = Dp(196);
        int paneW = clientW - sidebarW;
        int contentPadX = Dp(28);
        int cardW = paneW - (contentPadX * 2);

        ClientSize = new Size(clientW, clientH);

        // Sidebar
        _sidebarPanel.Width = sidebarW;
        _sidebarPanel.UpdateTheme(_palette, _s);

        int navX = Dp(12);
        int navW = sidebarW - (navX * 2);
        int navH = Math.Max(Dp(38), Font.Height + Dp(16));
        int navTop = Dp(78);
        int navGap = Dp(6);

        _navGeneral.SetBounds(navX, navTop, navW, navH);
        _navAppearance.SetBounds(navX, navTop + (navH + navGap), navW, navH);
        _navBehavior.SetBounds(navX, navTop + 2 * (navH + navGap), navW, navH);

        _lblSidebarVersion.Height = Math.Max(Dp(34), _lblSidebarVersion.Font.Height + Dp(14));
        _lblSidebarVersion.Padding = new Padding(Dp(16), 0, Dp(12), Dp(8));

        // Footer
        _btnSave.UpdateTheme(_palette, _s);
        _btnCancel.UpdateTheme(_palette, _s);
        _btnSave.Size = _btnSave.MeasurePreferredSize(_s);
        _btnCancel.Size = _btnCancel.MeasurePreferredSize(_s);
        _footerPanel.Height = Math.Max(Dp(60), _btnSave.Height + Dp(24));
        LayoutFooter();

        // Sections
        LayoutGeneralSection(paneW, contentPadX, cardW);
        LayoutAppearanceSection(paneW, contentPadX, cardW);
        LayoutBehaviorSection(paneW, contentPadX, cardW);
    }

    private void LayoutFooter()
    {
        int rightMargin = Dp(28);
        int leftMargin = Dp(28);
        int gap = Dp(10);
        int centerY = (_footerPanel.ClientSize.Height - _btnSave.Height) / 2;

        _btnSave.Location = new Point(_footerPanel.ClientSize.Width - rightMargin - _btnSave.Width, centerY);
        _btnCancel.Location = new Point(_btnSave.Left - gap - _btnCancel.Width, centerY);

        int hintH = Math.Max(Dp(20), _lblFooterHint.Font.Height + Dp(4));
        int hintW = Math.Max(Dp(80), _btnCancel.Left - leftMargin - Dp(16));
        _lblFooterHint.SetBounds(leftMargin, (_footerPanel.ClientSize.Height - hintH) / 2, hintW, hintH);
    }

    private void LayoutGeneralSection(int paneW, int contentLeft, int cardW)
    {
        int padX = Dp(18);
        int innerW = cardW - (padX * 2);
        int y = Dp(22);

        _lblGenHeading.MaximumSize = new Size(cardW, 0);
        _lblGenHeading.Location = new Point(contentLeft, y);
        y += _lblGenHeading.PreferredSize.Height + Dp(4);

        _lblGenSub.MaximumSize = new Size(cardW, 0);
        _lblGenSub.Location = new Point(contentLeft, y);
        y += _lblGenSub.PreferredSize.Height + Dp(16);

        // Card 1: Web Application
        int cy = Dp(16);
        _lblUrlTitle.MaximumSize = new Size(innerW, 0);
        _lblUrlTitle.Location = new Point(padX, cy);
        cy += _lblUrlTitle.PreferredSize.Height + Dp(3);

        _lblUrlDesc.MaximumSize = new Size(innerW, 0);
        _lblUrlDesc.Location = new Point(padX, cy);
        cy += _lblUrlDesc.PreferredSize.Height + Dp(8);

        _txtUrl.UpdateTheme(_palette, _s);
        _txtUrl.SetBounds(padX, cy, innerW, _txtUrl.PreferredControlHeight);
        cy = _txtUrl.Bottom + Dp(14);

        _webAppDivider.SetBounds(padX, cy, innerW, 1);
        cy += 1 + Dp(14);

        _lblTitleHeading.MaximumSize = new Size(innerW, 0);
        _lblTitleHeading.Location = new Point(padX, cy);
        cy += _lblTitleHeading.PreferredSize.Height + Dp(3);

        _lblTitleDesc.MaximumSize = new Size(innerW, 0);
        _lblTitleDesc.Location = new Point(padX, cy);
        cy += _lblTitleDesc.PreferredSize.Height + Dp(8);

        _txtTitle.UpdateTheme(_palette, _s);
        _txtTitle.SetBounds(padX, cy, innerW, _txtTitle.PreferredControlHeight);
        cy = _txtTitle.Bottom + Dp(16);

        _cardWebApp.SetBounds(contentLeft, y, cardW, cy);
        y = _cardWebApp.Bottom + Dp(14);

        // Card 2: System
        int toggleW = Dp(44);
        int toggleH = Dp(24);
        int textMaxW = innerW - toggleW - Dp(16);
        int sy = Dp(14);

        _lblStartupHeading.MaximumSize = new Size(textMaxW, 0);
        _lblStartupHeading.Location = new Point(padX, sy);
        sy += _lblStartupHeading.PreferredSize.Height + Dp(3);

        _lblStartupDesc.MaximumSize = new Size(textMaxW, 0);
        _lblStartupDesc.Location = new Point(padX, sy);
        sy += _lblStartupDesc.PreferredSize.Height + Dp(14);

        int sysCardH = Math.Max(sy, toggleH + Dp(28));
        _tglStartup.SetBounds(cardW - padX - toggleW, (sysCardH - toggleH) / 2, toggleW, toggleH);
        _cardSystem.SetBounds(contentLeft, y, cardW, sysCardH);
        y = _cardSystem.Bottom + Dp(20);

        _secGeneral.SetBounds(0, 0, paneW, y);
    }

    private void LayoutAppearanceSection(int paneW, int contentLeft, int cardW)
    {
        int padX = Dp(18);
        int innerW = cardW - (padX * 2);
        int y = Dp(22);

        _lblAppHeading.MaximumSize = new Size(cardW, 0);
        _lblAppHeading.Location = new Point(contentLeft, y);
        y += _lblAppHeading.PreferredSize.Height + Dp(4);

        _lblAppSub.MaximumSize = new Size(cardW, 0);
        _lblAppSub.Location = new Point(contentLeft, y);
        y += _lblAppSub.PreferredSize.Height + Dp(16);

        // Card 1: System Tray Icon
        int cy = Dp(16);
        _lblTrayIconHeading.MaximumSize = new Size(innerW, 0);
        _lblTrayIconHeading.Location = new Point(padX, cy);
        cy += _lblTrayIconHeading.PreferredSize.Height + Dp(10);

        int segH = Math.Max(Dp(34), Font.Height + Dp(14));
        _segIconTheme.UpdateTheme(_palette, _s);
        _segIconTheme.SetBounds(padX, cy, innerW, segH);
        cy = _segIconTheme.Bottom + Dp(12);

        int previewSize = Dp(48);
        int picSize = Dp(30);
        _previewBox.SetBounds(padX, cy, previewSize, previewSize);
        _picPreview.SetBounds((previewSize - picSize) / 2, (previewSize - picSize) / 2, picSize, picSize);

        _btnUpload.UpdateTheme(_palette, _s);
        _btnResetIcon.UpdateTheme(_palette, _s);
        _btnUpload.Size = _btnUpload.MeasurePreferredSize(_s);
        _btnResetIcon.Size = _btnResetIcon.MeasurePreferredSize(_s);

        int btnY = cy + (previewSize - _btnUpload.Height) / 2;
        _btnUpload.Location = new Point(_previewBox.Right + Dp(12), btnY);
        _btnResetIcon.Location = new Point(_btnUpload.Right + Dp(8), btnY);

        cy = _previewBox.Bottom + Dp(14);

        _lblPresets.MaximumSize = new Size(innerW, 0);
        _lblPresets.Location = new Point(padX, cy);
        cy += _lblPresets.PreferredSize.Height + Dp(8);

        int presetGap = Dp(7);
        int presetCount = _presetButtons.Count;
        int presetW = (innerW - ((presetCount - 1) * presetGap)) / presetCount;
        int presetH = Dp(40);

        for (int i = 0; i < presetCount; i++)
        {
            _presetButtons[i].SetBounds(padX + i * (presetW + presetGap), cy, presetW, presetH);
        }
        cy += presetH + Dp(16);

        _cardTrayIcon.SetBounds(contentLeft, y, cardW, cy);
        y = _cardTrayIcon.Bottom + Dp(14);

        // Card 2: Popup Dimensions
        int dy = Dp(16);
        _lblWidthHeading.MaximumSize = new Size(innerW, 0);
        _lblWidthHeading.Location = new Point(padX, dy);
        dy += _lblWidthHeading.PreferredSize.Height + Dp(3);

        _lblWidthDesc.MaximumSize = new Size(innerW, 0);
        _lblWidthDesc.Location = new Point(padX, dy);
        dy += _lblWidthDesc.PreferredSize.Height + Dp(10);

        _segWidth.UpdateTheme(_palette, _s);
        _segWidth.SetBounds(padX, dy, innerW, segH);
        dy = _segWidth.Bottom + Dp(16);

        _cardDimensions.SetBounds(contentLeft, y, cardW, dy);
        y = _cardDimensions.Bottom + Dp(20);

        _secAppearance.SetBounds(0, 0, paneW, y);
    }

    private void LayoutBehaviorSection(int paneW, int contentLeft, int cardW)
    {
        int padX = Dp(18);
        int innerW = cardW - (padX * 2);
        int y = Dp(22);

        _lblBehHeading.MaximumSize = new Size(cardW, 0);
        _lblBehHeading.Location = new Point(contentLeft, y);
        y += _lblBehHeading.PreferredSize.Height + Dp(4);

        _lblBehSub.MaximumSize = new Size(cardW, 0);
        _lblBehSub.Location = new Point(contentLeft, y);
        y += _lblBehSub.PreferredSize.Height + Dp(16);

        int segH = Math.Max(Dp(34), Font.Height + Dp(14));

        // Card 1: Link Click Behavior
        int cy = Dp(16);
        _lblLinkHeading.MaximumSize = new Size(innerW, 0);
        _lblLinkHeading.Location = new Point(padX, cy);
        cy += _lblLinkHeading.PreferredSize.Height + Dp(3);

        _lblLinkDesc.MaximumSize = new Size(innerW, 0);
        _lblLinkDesc.Location = new Point(padX, cy);
        cy += _lblLinkDesc.PreferredSize.Height + Dp(10);

        _segLinkBehavior.UpdateTheme(_palette, _s);
        _segLinkBehavior.SetBounds(padX, cy, innerW, segH);
        cy = _segLinkBehavior.Bottom + Dp(16);

        _cardLinks.SetBounds(contentLeft, y, cardW, cy);
        y = _cardLinks.Bottom + Dp(14);

        // Card 2: Memory & Performance
        int my = Dp(16);
        _hdrMemory.UpdateTheme(_palette, _s);
        _hdrMemory.SetBounds(padX, my, innerW, Math.Max(Dp(24), Font.Height + Dp(4)));
        my = _hdrMemory.Bottom + Dp(4);

        _lblMemoryDesc.MaximumSize = new Size(innerW, 0);
        _lblMemoryDesc.Location = new Point(padX, my);
        my += _lblMemoryDesc.PreferredSize.Height + Dp(10);

        _segIdleTeardown.UpdateTheme(_palette, _s);
        _segIdleTeardown.SetBounds(padX, my, innerW, segH);
        my = _segIdleTeardown.Bottom + Dp(16);

        _cardMemory.SetBounds(contentLeft, y, cardW, my);
        y = _cardMemory.Bottom + Dp(20);

        _secBehavior.SetBounds(0, 0, paneW, y);
    }

    private void SwitchSection(SettingsSection section)
    {
        _activeSection = section;
        _navGeneral.IsActive = section == SettingsSection.General;
        _navAppearance.IsActive = section == SettingsSection.Appearance;
        _navBehavior.IsActive = section == SettingsSection.Behavior;

        _secGeneral.Visible = section == SettingsSection.General;
        _secAppearance.Visible = section == SettingsSection.Appearance;
        _secBehavior.Visible = section == SettingsSection.Behavior;
        _scrollHost.AutoScrollPosition = Point.Empty;
    }

    private void SwitchIconTab(string tab)
    {
        _activeIconTab = tab;
        UpdatePreviewContainerColor();
        RefreshPreviewImage();
        UpdatePresetSelectionState();
    }

    private void UpdatePreviewContainerColor()
    {
        _previewBox.UpdateAppearance(
            _activeIconTab == "dark" ? Color.FromArgb(15, 23, 42) : Color.White,
            _palette.Border,
            _s);
    }

    private void RefreshPreviewImage()
    {
        string? dataUrl = _activeIconTab == "light" ? _customIconLight : _customIconDark;
        Bitmap next = IconHelper.DecodeDataUrlToBitmap(dataUrl) ?? IconHelper.CreateDefaultBitmapCopy();

        Image? old = _picPreview.Image;
        _picPreview.Image = next;
        old?.Dispose();
    }

    private void UpdatePresetSelectionState()
    {
        string? selectedKey = _activeIconTab == "light" ? _selectedPresetLight : _selectedPresetDark;
        foreach (PresetIconButton btn in _presetButtons)
        {
            btn.IsSelected = string.Equals(btn.PresetKey, selectedKey, StringComparison.OrdinalIgnoreCase);
        }
    }

    private void SelectPreset(string presetKey)
    {
        string dataUrl = IconHelper.RenderPresetDataUrl(presetKey);
        if (_activeIconTab == "light")
        {
            _customIconLight = dataUrl;
            _isPresetLight = true;
            _selectedPresetLight = presetKey;
        }
        else
        {
            _customIconDark = dataUrl;
            _isPresetDark = true;
            _selectedPresetDark = presetKey;
        }

        UpdatePresetSelectionState();
        RefreshPreviewImage();
    }

    private void BtnUpload_Click(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Choose Tray Icon Image",
            Filter = "Image Files|*.png;*.jpg;*.jpeg;*.ico;*.bmp;*.gif|All Files|*.*"
        };

        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            string encoded = IconHelper.ResizeAndEncodePngDataUrl(dialog.FileName, 128);
            if (_activeIconTab == "light")
            {
                _customIconLight = encoded;
                _isPresetLight = false;
                _selectedPresetLight = null;
            }
            else
            {
                _customIconDark = encoded;
                _isPresetDark = false;
                _selectedPresetDark = null;
            }

            UpdatePresetSelectionState();
            RefreshPreviewImage();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                $"Could not load that image file: {ex.Message}",
                "Image Load Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    private void BtnResetIcon_Click(object? sender, EventArgs e)
    {
        if (_activeIconTab == "light")
        {
            _customIconLight = string.Empty;
            _isPresetLight = false;
            _selectedPresetLight = null;
        }
        else
        {
            _customIconDark = string.Empty;
            _isPresetDark = false;
            _selectedPresetDark = null;
        }

        UpdatePresetSelectionState();
        RefreshPreviewImage();
    }

    private void BtnSave_Click(object? sender, EventArgs e)
    {
        if (!AppSettings.TryNormalizeWebUrl(_txtUrl.Text, out string normalizedUrl))
        {
            SwitchSection(SettingsSection.General);
            MessageBox.Show(
                this,
                "Please enter a valid web address (e.g. https://notion.so).",
                "Invalid URL",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            _txtUrl.FocusInput();
            return;
        }

        var updated = new AppSettings
        {
            CustomUrl = normalizedUrl,
            CustomTitle = _txtTitle.Text.Trim(),
            CustomIcon = _customIconLight,
            IsPresetIcon = _isPresetLight,
            CustomIconDark = _customIconDark,
            IsPresetIconDark = _isPresetDark,
            PopupWidth = _segWidth.SelectedValue,
            LinkBehavior = _segLinkBehavior.SelectedValue ?? "popup",
            OpenAtLogin = _tglStartup.Checked,
            IdleTeardownSeconds = _segIdleTeardown.SelectedValue
        };
        updated.Normalize();

        SettingsSaved?.Invoke(this, updated);
        DialogResult = DialogResult.OK;
        Close();
    }

    private static Label CreateSectionHeading(string text) => new()
    {
        Text = text,
        AutoSize = true,
        Font = new Font("Segoe UI", 15f, FontStyle.Bold)
    };

    private static Label CreateSectionSubtitle(string text) => new()
    {
        Text = text,
        AutoSize = true,
        Font = new Font("Segoe UI", 9f, FontStyle.Regular)
    };

    private static Label CreateFieldHeading(string text) => new()
    {
        Text = text,
        AutoSize = true,
        Font = new Font("Segoe UI", 9.5f, FontStyle.Bold)
    };

    private static Label CreateFieldDescription(string text) => new()
    {
        Text = text,
        AutoSize = true,
        Font = new Font("Segoe UI", 8.25f, FontStyle.Regular)
    };

    // -------------------------------------------------------------------------
    // Antigravity UI Controls (DPI-scaled)
    // -------------------------------------------------------------------------

    private sealed class SidebarPanel : Panel
    {
        private readonly Bitmap _brandBitmap;
        private ThemePalette _palette = ThemeHelper.Light;
        private float _scale = 1f;

        public SidebarPanel(Bitmap brandBitmap)
        {
            _brandBitmap = brandBitmap;
            DoubleBuffered = true;
        }

        public void UpdateTheme(ThemePalette palette, float scale)
        {
            _palette = palette;
            _scale = scale;
            BackColor = palette.SidebarBackground;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;

            float s = _scale;
            int padX = (int)Math.Round(16 * s);
            int topY = (int)Math.Round(18 * s);

            using var titleFont = new Font("Segoe UI", 11f, FontStyle.Bold);
            using var subFont = new Font("Segoe UI", 8.25f, FontStyle.Regular);

            Size titleSz = TextRenderer.MeasureText(g, "QuickUp", titleFont, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding);
            Size subSz = TextRenderer.MeasureText(g, "Settings", subFont, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding);

            int textBlockH = titleSz.Height + subSz.Height;
            int iconSize = (int)Math.Round(26 * s);
            int iconY = topY + Math.Max(0, (textBlockH - iconSize) / 2);

            var iconRect = new Rectangle(padX, iconY, iconSize, iconSize);
            using (GraphicsPath clip = IconHelper.CreateRoundedRect(iconRect, 6f * s))
            {
                GraphicsState state = g.Save();
                g.SetClip(clip);
                g.DrawImage(_brandBitmap, iconRect);
                g.Restore(state);
            }

            int textX = iconRect.Right + (int)Math.Round(10 * s);
            int textW = Math.Max(40, Width - textX - (int)Math.Round(10 * s));

            TextRenderer.DrawText(
                g,
                "QuickUp",
                titleFont,
                new Rectangle(textX, topY, textW, titleSz.Height),
                _palette.TextPrimary,
                TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.NoPadding);

            TextRenderer.DrawText(
                g,
                "Settings",
                subFont,
                new Rectangle(textX, topY + titleSz.Height, textW, subSz.Height),
                _palette.TextMuted,
                TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.NoPadding);

            using var borderPen = new Pen(_palette.Border, 1f);
            g.DrawLine(borderPen, Width - 1, 0, Width - 1, Height);
        }
    }

    private sealed class NavRailButton : Control
    {
        private readonly TablerIcon _icon;
        private readonly string _label;
        private ThemePalette _palette = ThemeHelper.Light;
        private float _scale = 1f;
        private bool _hovered;
        private bool _active;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool IsActive
        {
            get => _active;
            set
            {
                if (_active != value)
                {
                    _active = value;
                    Invalidate();
                }
            }
        }

        public NavRailButton(TablerIcon icon, string label)
        {
            _icon = icon;
            _label = label;
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

        public void UpdateTheme(ThemePalette palette, float scale)
        {
            _palette = palette;
            _scale = scale;
            Invalidate();
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _hovered = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hovered = false;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            float s = _scale;
            if (_active || _hovered)
            {
                Color bg = _active ? _palette.SelectedBackground : _palette.BackgroundHover;
                using GraphicsPath pill = IconHelper.CreateRoundedRect(new RectangleF(0, 0, Width - 1, Height - 1), 8f * s);
                using var brush = new SolidBrush(bg);
                g.FillPath(brush, pill);
            }

            Color fg = _active ? _palette.SelectedForeground : _hovered ? _palette.TextPrimary : _palette.TextSecondary;
            float iconSize = 17f * s;
            float iconX = 12f * s;
            TablerIcons.Draw(g, _icon, iconX, (Height - iconSize) / 2f, iconSize, fg, strokeWidth24: 1.9f);

            int textX = (int)Math.Round(iconX + iconSize + 10f * s);
            using var font = new Font("Segoe UI", 9.5f, _active ? FontStyle.Bold : FontStyle.Regular);
            TextRenderer.DrawText(
                g,
                _label,
                font,
                new Rectangle(textX, 0, Math.Max(20, Width - textX - (int)Math.Round(8 * s)), Height),
                fg,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }

    private sealed class FooterPanel : Panel
    {
        private ThemePalette _palette = ThemeHelper.Light;

        public FooterPanel()
        {
            DoubleBuffered = true;
        }

        public void UpdateTheme(ThemePalette palette)
        {
            _palette = palette;
            BackColor = palette.SidebarBackground;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using var pen = new Pen(_palette.Border, 1f);
            e.Graphics.DrawLine(pen, 0, 0, Width, 0);
        }
    }

    private sealed class RoundedCard : Panel
    {
        private ThemePalette _palette = ThemeHelper.Light;
        private float _scale = 1f;

        public RoundedCard()
        {
            DoubleBuffered = true;
        }

        public void UpdateTheme(ThemePalette palette, float scale)
        {
            _palette = palette;
            _scale = scale;
            BackColor = palette.CardBackground;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            using var outerBrush = new SolidBrush(_palette.CanvasBackground);
            g.FillRectangle(outerBrush, ClientRectangle);

            var rect = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            using GraphicsPath path = IconHelper.CreateRoundedRect(rect, 10f * _scale);
            using var cardBrush = new SolidBrush(_palette.CardBackground);
            using var borderPen = new Pen(_palette.Border, 1f);
            g.FillPath(cardBrush, path);
            g.DrawPath(borderPen, path);
        }
    }

    private sealed class PreviewTilePanel : Panel
    {
        private Color _fill = Color.White;
        private Color _border = ThemeHelper.Light.Border;
        private float _scale = 1f;

        public PreviewTilePanel()
        {
            DoubleBuffered = true;
        }

        public void UpdateAppearance(Color fill, Color border, float scale)
        {
            _fill = fill;
            _border = border;
            _scale = scale;
            BackColor = fill;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var rect = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            using GraphicsPath path = IconHelper.CreateRoundedRect(rect, 8f * _scale);
            using var fillBrush = new SolidBrush(_fill);
            using var borderPen = new Pen(_border, 1f);
            g.FillPath(fillBrush, path);
            g.DrawPath(borderPen, path);
        }
    }

    private sealed class IconHeadingRow : Control
    {
        private readonly TablerIcon _icon;
        private readonly string _title;
        private ThemePalette _palette = ThemeHelper.Light;
        private float _scale = 1f;

        public IconHeadingRow(TablerIcon icon, string title)
        {
            _icon = icon;
            _title = title;
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.UserPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.SupportsTransparentBackColor,
                true);
            BackColor = Color.Transparent;
        }

        public void UpdateTheme(ThemePalette palette, float scale)
        {
            _palette = palette;
            _scale = scale;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            float iconSize = 16f * _scale;
            TablerIcons.Draw(g, _icon, 0f, (Height - iconSize) / 2f, iconSize, _palette.AccentBlue, strokeWidth24: 2.0f);

            int textX = (int)Math.Round(iconSize + 7f * _scale);
            using var font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            TextRenderer.DrawText(
                g,
                _title,
                font,
                new Rectangle(textX, 0, Math.Max(20, Width - textX), Height),
                _palette.TextPrimary,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }
    }

    private enum ModernButtonKind
    {
        Primary,
        Secondary,
        GhostDanger
    }

    private sealed class ModernButton : Control, IButtonControl
    {
        private readonly ModernButtonKind _kind;
        private readonly TablerIcon? _icon;
        private ThemePalette _palette = ThemeHelper.Light;
        private float _scale = 1f;
        private bool _hovered;
        private bool _pressed;
        private bool _isDefault;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public DialogResult DialogResult { get; set; } = DialogResult.None;

        public ModernButton(ModernButtonKind kind, TablerIcon? icon = null)
        {
            _kind = kind;
            _icon = icon;
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.UserPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.Selectable |
                ControlStyles.SupportsTransparentBackColor,
                true);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
        }

        public Size MeasurePreferredSize(float scale)
        {
            using var font = new Font("Segoe UI", 9f, FontStyle.Bold);
            Size textSz = TextRenderer.MeasureText(Text, font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding);
            int padX = (int)Math.Round((_icon.HasValue ? 46f : 32f) * scale);
            int minW = (int)Math.Round(90f * scale);
            int w = Math.Max(minW, textSz.Width + padX);
            int h = Math.Max((int)Math.Round(34f * scale), textSz.Height + (int)Math.Round(16f * scale));
            return new Size(w, h);
        }

        public void NotifyDefault(bool value)
        {
            _isDefault = value;
            Invalidate();
        }

        public void PerformClick()
        {
            if (CanSelect)
            {
                OnClick(EventArgs.Empty);
            }
        }

        public void UpdateTheme(ThemePalette palette, float scale)
        {
            _palette = palette;
            _scale = scale;
            Invalidate();
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _hovered = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hovered = false;
            _pressed = false;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left)
            {
                _pressed = true;
                Invalidate();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (_pressed)
            {
                _pressed = false;
                Invalidate();
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode is Keys.Space or Keys.Enter)
            {
                PerformClick();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            float s = _scale;
            var rect = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            using GraphicsPath path = IconHelper.CreateRoundedRect(rect, 8f * s);

            Color bg;
            Color fg;
            Color? border = null;

            switch (_kind)
            {
                case ModernButtonKind.Primary:
                    bg = _pressed || _hovered ? _palette.AccentBlueHover : _palette.AccentBlue;
                    fg = _palette.TextOnPrimary;
                    break;

                case ModernButtonKind.GhostDanger:
                    bg = _hovered ? Color.FromArgb(24, _palette.Danger) : Color.Transparent;
                    fg = _palette.Danger;
                    break;

                default:
                    bg = _hovered ? _palette.BackgroundHover : _palette.CardBackground;
                    fg = _palette.TextPrimary;
                    border = _isDefault ? _palette.AccentBlue : _palette.Border;
                    break;
            }

            if (bg != Color.Transparent)
            {
                using var brush = new SolidBrush(bg);
                g.FillPath(brush, path);
            }

            if (border.HasValue)
            {
                using var pen = new Pen(border.Value, 1f);
                g.DrawPath(pen, path);
            }

            using var font = new Font("Segoe UI", 9f, FontStyle.Bold);
            Size textSize = TextRenderer.MeasureText(g, Text, font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding);

            if (_icon.HasValue)
            {
                float iconSize = 15f * s;
                float gap = 7f * s;
                float totalW = iconSize + gap + textSize.Width;
                float startX = Math.Max(8f * s, (Width - totalW) / 2f);
                TablerIcons.Draw(g, _icon.Value, startX, (Height - iconSize) / 2f, iconSize, fg, strokeWidth24: 2.0f);

                int textX = (int)Math.Round(startX + iconSize + gap);
                TextRenderer.DrawText(
                    g,
                    Text,
                    font,
                    new Rectangle(textX, 0, Math.Max(10, Width - textX - (int)Math.Round(6f * s)), Height),
                    fg,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            }
            else
            {
                TextRenderer.DrawText(
                    g,
                    Text,
                    font,
                    ClientRectangle,
                    fg,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            }
        }
    }

    private sealed record SegmentOption<T>(string Label, T Value, TablerIcon? Icon = null);

    private sealed class SegmentedControl<T> : Control
    {
        private readonly List<SegmentOption<T>> _options;
        private ThemePalette _palette = ThemeHelper.Light;
        private float _scale = 1f;
        private T? _selectedValue;
        private int _hoveredIndex = -1;

        public event EventHandler? SelectedValueChanged;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public T? SelectedValue
        {
            get => _selectedValue;
            set
            {
                if (!EqualityComparer<T>.Default.Equals(_selectedValue, value))
                {
                    _selectedValue = value;
                    Invalidate();
                }
            }
        }

        public SegmentedControl(IEnumerable<SegmentOption<T>> options)
        {
            _options = [.. options];
            if (_options.Count > 0)
            {
                _selectedValue = _options[0].Value;
            }

            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.UserPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw,
                true);
            Cursor = Cursors.Hand;
        }

        public void UpdateTheme(ThemePalette palette, float scale)
        {
            _palette = palette;
            _scale = scale;
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int idx = HitTest(e.Location);
            if (idx != _hoveredIndex)
            {
                _hoveredIndex = idx;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hoveredIndex != -1)
            {
                _hoveredIndex = -1;
                Invalidate();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button != MouseButtons.Left) return;

            int idx = HitTest(e.Location);
            if (idx >= 0 && idx < _options.Count)
            {
                T next = _options[idx].Value;
                if (!EqualityComparer<T>.Default.Equals(_selectedValue, next))
                {
                    _selectedValue = next;
                    Invalidate();
                    SelectedValueChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        private int HitTest(Point pt)
        {
            float pad = 3f * _scale;
            if (_options.Count == 0 || Width <= pad * 2) return -1;
            float segW = (Width - pad * 2) / _options.Count;
            int idx = (int)((pt.X - pad) / segW);
            return idx >= 0 && idx < _options.Count ? idx : -1;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            float s = _scale;
            float pad = 3f * s;

            var trackRect = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            using (GraphicsPath trackPath = IconHelper.CreateRoundedRect(trackRect, 8f * s))
            using (var trackBrush = new SolidBrush(_palette.CardSubtle))
            using (var trackPen = new Pen(_palette.Border, 1f))
            {
                g.FillPath(trackBrush, trackPath);
                g.DrawPath(trackPen, trackPath);
            }

            if (_options.Count == 0) return;
            float segW = (Width - pad * 2) / _options.Count;

            for (int i = 0; i < _options.Count; i++)
            {
                SegmentOption<T> opt = _options[i];
                bool selected = EqualityComparer<T>.Default.Equals(_selectedValue, opt.Value);
                bool hovered = i == _hoveredIndex && !selected;

                var segRect = new RectangleF(pad + i * segW, pad, segW, Height - pad * 2);
                if (selected || hovered)
                {
                    using GraphicsPath segPath = IconHelper.CreateRoundedRect(segRect, 6f * s);
                    Color segFill = selected ? _palette.CardBackground : _palette.BackgroundHover;
                    using var segBrush = new SolidBrush(segFill);
                    g.FillPath(segBrush, segPath);

                    if (selected)
                    {
                        using var segBorder = new Pen(_palette.Border, 1f);
                        g.DrawPath(segBorder, segPath);
                    }
                }

                Color fg = selected
                    ? _palette.SelectedForeground
                    : hovered
                        ? _palette.TextPrimary
                        : _palette.TextSecondary;

                using var font = new Font("Segoe UI", 8.75f, selected ? FontStyle.Bold : FontStyle.Regular);
                if (opt.Icon.HasValue)
                {
                    float iconSize = 14f * s;
                    float gap = 6f * s;
                    Size textSize = TextRenderer.MeasureText(g, opt.Label, font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding);
                    float totalW = iconSize + gap + textSize.Width;
                    float startX = segRect.Left + Math.Max(4f * s, (segRect.Width - totalW) / 2f);
                    TablerIcons.Draw(g, opt.Icon.Value, startX, segRect.Top + (segRect.Height - iconSize) / 2f, iconSize, fg, strokeWidth24: 2.0f);

                    int textX = (int)Math.Round(startX + iconSize + gap);
                    TextRenderer.DrawText(
                        g,
                        opt.Label,
                        font,
                        new Rectangle(textX, (int)segRect.Top, (int)Math.Max(10f, segRect.Right - textX - 2f * s), (int)segRect.Height),
                        fg,
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
                }
                else
                {
                    TextRenderer.DrawText(
                        g,
                        opt.Label,
                        font,
                        Rectangle.Round(segRect),
                        fg,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
                }
            }
        }
    }

    private sealed class OutlinedTextBox : Panel
    {
        private readonly TextBox _inner;
        private readonly TablerIcon? _leadingIcon;
        private ThemePalette _palette = ThemeHelper.Light;
        private float _scale = 1f;
        private bool _focused;

        [AllowNull]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public override string Text
        {
            get => _inner.Text;
            set => _inner.Text = value ?? string.Empty;
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string PlaceholderText
        {
            get => _inner.PlaceholderText;
            set => _inner.PlaceholderText = value;
        }

        public int PreferredControlHeight => Math.Max((int)Math.Round(36 * _scale), _inner.PreferredHeight + (int)Math.Round(14 * _scale));

        public OutlinedTextBox(TablerIcon? leadingIcon)
        {
            _leadingIcon = leadingIcon;
            DoubleBuffered = true;

            _inner = new TextBox
            {
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Regular)
            };

            _inner.GotFocus += (_, _) => { _focused = true; Invalidate(); };
            _inner.LostFocus += (_, _) => { _focused = false; Invalidate(); };

            Controls.Add(_inner);
            Resize += (_, _) => LayoutInnerTextBox();
            Click += (_, _) => _inner.Focus();
        }

        public void FocusInput() => _inner.Focus();

        public void ClearSelection()
        {
            _inner.SelectionStart = _inner.TextLength;
            _inner.SelectionLength = 0;
        }

        public void UpdateTheme(ThemePalette palette, float scale)
        {
            _palette = palette;
            _scale = scale;
            BackColor = palette.BackgroundInput;
            _inner.BackColor = palette.BackgroundInput;
            _inner.ForeColor = palette.TextPrimary;
            LayoutInnerTextBox();
            Invalidate();
        }

        private void LayoutInnerTextBox()
        {
            int leftPad = (int)Math.Round((_leadingIcon.HasValue ? 34f : 12f) * _scale);
            int rightPad = (int)Math.Round(12f * _scale);
            int innerH = Math.Max(14, _inner.PreferredHeight);
            int innerY = Math.Max(4, (Height - innerH) / 2);
            _inner.SetBounds(leftPad, innerY, Math.Max(20, Width - leftPad - rightPad), innerH);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            float s = _scale;
            var rect = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            using GraphicsPath path = IconHelper.CreateRoundedRect(rect, 8f * s);
            using var fill = new SolidBrush(_palette.BackgroundInput);
            g.FillPath(fill, path);

            Color border = _focused ? _palette.AccentBlue : _palette.Border;
            using var pen = new Pen(border, _focused ? 1.5f : 1f);
            g.DrawPath(pen, path);

            if (_leadingIcon.HasValue)
            {
                float iconSize = 15f * s;
                Color iconColor = _focused ? _palette.AccentBlue : _palette.TextMuted;
                TablerIcons.Draw(g, _leadingIcon.Value, 11f * s, (Height - iconSize) / 2f, iconSize, iconColor, strokeWidth24: 1.9f);
            }
        }
    }

    private sealed class PresetIconButton : Control
    {
        private readonly Bitmap _thumbnail;
        private ThemePalette _palette = ThemeHelper.Light;
        private float _scale = 1f;
        private bool _hovered;
        private bool _selected;

        public string PresetKey { get; }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool IsSelected
        {
            get => _selected;
            set
            {
                if (_selected != value)
                {
                    _selected = value;
                    Invalidate();
                }
            }
        }

        public PresetIconButton(string presetKey)
        {
            PresetKey = presetKey;
            _thumbnail = IconHelper.RenderPresetIconBitmap(presetKey, 64);
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.UserPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw,
                true);
            Cursor = Cursors.Hand;
        }

        public void UpdateTheme(ThemePalette palette, float scale)
        {
            _palette = palette;
            _scale = scale;
            Invalidate();
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _hovered = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hovered = false;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;

            Color bg = _selected
                ? Color.FromArgb(255, 152, 0)
                : _hovered
                    ? (_palette.IsDark ? Color.FromArgb(78, 52, 46) : Color.FromArgb(255, 224, 178))
                    : (_palette.IsDark ? Color.FromArgb(62, 39, 35) : Color.FromArgb(255, 243, 224));

            Color border = _selected
                ? Color.FromArgb(245, 124, 0)
                : _hovered
                    ? Color.FromArgb(255, 183, 77)
                    : (_palette.IsDark ? Color.FromArgb(93, 64, 55) : Color.FromArgb(255, 224, 178));

            var rect = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            using (GraphicsPath path = IconHelper.CreateRoundedRect(rect, 8f * _scale))
            using (var bgBrush = new SolidBrush(bg))
            using (var borderPen = new Pen(border, 1f))
            {
                g.FillPath(bgBrush, path);
                g.DrawPath(borderPen, path);
            }

            int pad = (int)Math.Round(10 * _scale);
            int iconSize = Math.Max(16, Math.Min(Width, Height) - pad);
            int ix = (Width - iconSize) / 2;
            int iy = (Height - iconSize) / 2;
            g.DrawImage(_thumbnail, ix, iy, iconSize, iconSize);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _thumbnail.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    private sealed class ToggleSwitch : Control
    {
        private ThemePalette _palette = ThemeHelper.Light;
        private float _scale = 1f;
        private bool _checked;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool Checked
        {
            get => _checked;
            set
            {
                if (_checked != value)
                {
                    _checked = value;
                    Invalidate();
                }
            }
        }

        public ToggleSwitch()
        {
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

        public void UpdateTheme(ThemePalette palette, float scale)
        {
            _palette = palette;
            _scale = scale;
            Invalidate();
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            Checked = !Checked;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            float pad = 1f * _scale;
            var trackRect = new RectangleF(pad, pad, Width - pad * 2f, Height - pad * 2f);
            Color trackColor = _checked ? _palette.AccentBlue : _palette.BorderHover;

            using (GraphicsPath trackPath = IconHelper.CreateRoundedRect(trackRect, trackRect.Height / 2f))
            using (var trackBrush = new SolidBrush(trackColor))
            {
                g.FillPath(trackBrush, trackPath);
            }

            float inset = 3f * _scale;
            float thumbSize = trackRect.Height - (inset * 2f);
            float thumbX = _checked ? trackRect.Right - thumbSize - inset : trackRect.Left + inset;
            float thumbY = trackRect.Top + inset;
            Color thumbColor = _checked && _palette.IsDark ? _palette.TextOnPrimary : Color.White;

            using var thumbBrush = new SolidBrush(thumbColor);
            g.FillEllipse(thumbBrush, thumbX, thumbY, thumbSize, thumbSize);
        }
    }
}
