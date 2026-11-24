using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Drawing.Drawing2D;

namespace QuickUp;

/// <summary>
/// Antigravity-inspired two-pane native WinForms settings window using Tabler outline icons.
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

        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(720, 560);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        Text = "Settings — Dynamic Web Panel";
        Icon = IconHelper.GetDefaultAppIcon();
        Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);

        // ---------------------------------------------------------------------
        // Left Sidebar (196px)
        // ---------------------------------------------------------------------
        _sidebarPanel = new SidebarPanel(_brandBitmap)
        {
            Dock = DockStyle.Left,
            Width = 196
        };

        _navGeneral = new NavRailButton(TablerIcon.AdjustmentsHorizontal, "General")
        {
            Location = new Point(12, 76),
            Size = new Size(172, 38)
        };
        _navGeneral.Click += (_, _) => SwitchSection(SettingsSection.General);

        _navAppearance = new NavRailButton(TablerIcon.Palette, "Appearance")
        {
            Location = new Point(12, 120),
            Size = new Size(172, 38)
        };
        _navAppearance.Click += (_, _) => SwitchSection(SettingsSection.Appearance);

        _navBehavior = new NavRailButton(TablerIcon.AppWindow, "Behavior")
        {
            Location = new Point(12, 164),
            Size = new Size(172, 38)
        };
        _navBehavior.Click += (_, _) => SwitchSection(SettingsSection.Behavior);

        _lblSidebarVersion = new Label
        {
            Text = "v2.0 · .NET 10 · WebView2",
            Location = new Point(16, 524),
            Size = new Size(164, 20),
            Anchor = AnchorStyles.Left | AnchorStyles.Bottom,
            Font = new Font("Segoe UI", 8f, FontStyle.Regular)
        };

        _sidebarPanel.Controls.AddRange([_navGeneral, _navAppearance, _navBehavior, _lblSidebarVersion]);

        // ---------------------------------------------------------------------
        // Right Pane (Footer + Scrollable Content)
        // ---------------------------------------------------------------------
        _mainPane = new Panel { Dock = DockStyle.Fill };

        _footerPanel = new FooterPanel
        {
            Dock = DockStyle.Bottom,
            Height = 60
        };

        _lblFooterHint = new Label
        {
            Text = "Changes apply immediately after saving",
            Location = new Point(28, 20),
            Size = new Size(250, 20),
            Font = new Font("Segoe UI", 8.5f, FontStyle.Regular)
        };

        _btnCancel = new ModernButton(ModernButtonKind.Secondary)
        {
            Text = "Cancel",
            DialogResult = DialogResult.Cancel,
            Size = new Size(92, 34)
        };
        _btnCancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };

        _btnSave = new ModernButton(ModernButtonKind.Primary)
        {
            Text = "Save Changes",
            Size = new Size(124, 34)
        };
        _btnSave.Click += BtnSave_Click;

        _footerPanel.Controls.AddRange([_lblFooterHint, _btnCancel, _btnSave]);
        _footerPanel.Resize += (_, _) => PositionFooterButtons();

        _scrollHost = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true
        };

        const int contentLeft = 28;
        const int cardW = 468;

        // ---------------------------------------------------------------------
        // Section 1: General
        // ---------------------------------------------------------------------
        _secGeneral = new Panel
        {
            Location = new Point(0, 0),
            Size = new Size(524, 480)
        };

        _lblGenHeading = CreateSectionHeading("General", contentLeft, 22);
        _lblGenSub = CreateSectionSubtitle("Configure the web application URL, title, and startup preference.", contentLeft, 50, cardW);

        _cardWebApp = new RoundedCard
        {
            Location = new Point(contentLeft, 84),
            Size = new Size(cardW, 208)
        };

        _lblUrlTitle = CreateFieldHeading("Link URL", 18, 16);
        _lblUrlDesc = CreateFieldDescription("Web address (https://) to load inside the popup panel.", 18, 36, cardW - 36);
        _txtUrl = new OutlinedTextBox(TablerIcon.Link)
        {
            Location = new Point(18, 60),
            Size = new Size(cardW - 36, 34),
            PlaceholderText = "https://notion.so",
            Text = _originalSettings.CustomUrl
        };

        _webAppDivider = new Panel
        {
            Location = new Point(18, 108),
            Size = new Size(cardW - 36, 1)
        };

        _lblTitleHeading = CreateFieldHeading("Panel Title", 18, 120);
        _lblTitleDesc = CreateFieldDescription("Custom title shown in the panel header and tray tooltip.", 18, 140, cardW - 36);
        _txtTitle = new OutlinedTextBox(null)
        {
            Location = new Point(18, 158),
            Size = new Size(cardW - 36, 34),
            PlaceholderText = "Dynamic Web Panel",
            Text = _originalSettings.CustomTitle
        };

        _cardWebApp.Controls.AddRange([
            _lblUrlTitle, _lblUrlDesc, _txtUrl,
            _webAppDivider,
            _lblTitleHeading, _lblTitleDesc, _txtTitle
        ]);

        _cardSystem = new RoundedCard
        {
            Location = new Point(contentLeft, 306),
            Size = new Size(cardW, 68)
        };

        _lblStartupHeading = CreateFieldHeading("Start with Windows", 18, 14);
        _lblStartupDesc = CreateFieldDescription("Launch silently in the tray when you sign in to Windows.", 18, 34, 360);
        _tglStartup = new ToggleSwitch
        {
            Location = new Point(cardW - 18 - 42, 23),
            Size = new Size(42, 22),
            Checked = _originalSettings.OpenAtLogin
        };
        _cardSystem.Controls.AddRange([_lblStartupHeading, _lblStartupDesc, _tglStartup]);

        _secGeneral.Controls.AddRange([_lblGenHeading, _lblGenSub, _cardWebApp, _cardSystem]);

        // ---------------------------------------------------------------------
        // Section 2: Appearance
        // ---------------------------------------------------------------------
        _secAppearance = new Panel
        {
            Location = new Point(0, 0),
            Size = new Size(524, 480),
            Visible = false
        };

        _lblAppHeading = CreateSectionHeading("Appearance", contentLeft, 22);
        _lblAppSub = CreateSectionSubtitle("Customize your taskbar tray icon and popup panel width.", contentLeft, 50, cardW);

        _cardTrayIcon = new RoundedCard
        {
            Location = new Point(contentLeft, 84),
            Size = new Size(cardW, 214)
        };

        _lblTrayIconHeading = CreateFieldHeading("System Tray Icon", 18, 14);

        _segIconTheme = new SegmentedControl<string>(
        [
            new SegmentOption<string>("Light Taskbar", "light", TablerIcon.Sun),
            new SegmentOption<string>("Dark Taskbar", "dark", TablerIcon.Moon)
        ])
        {
            Location = new Point(18, 38),
            Size = new Size(cardW - 36, 32),
            SelectedValue = "light"
        };
        _segIconTheme.SelectedValueChanged += (_, _) =>
        {
            if (_segIconTheme.SelectedValue is string tab)
            {
                SwitchIconTab(tab);
            }
        };

        _previewBox = new PreviewTilePanel
        {
            Location = new Point(18, 82),
            Size = new Size(46, 46)
        };
        _picPreview = new PictureBox
        {
            Location = new Point(9, 9),
            Size = new Size(28, 28),
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.Transparent
        };
        _previewBox.Controls.Add(_picPreview);

        _btnUpload = new ModernButton(ModernButtonKind.Secondary, TablerIcon.Upload)
        {
            Text = "Upload Image",
            Location = new Point(76, 89),
            Size = new Size(128, 32)
        };
        _btnUpload.Click += BtnUpload_Click;

        _btnResetIcon = new ModernButton(ModernButtonKind.GhostDanger, TablerIcon.Restore)
        {
            Text = "Restore Default",
            Location = new Point(212, 89),
            Size = new Size(134, 32)
        };
        _btnResetIcon.Click += BtnResetIcon_Click;

        _lblPresets = CreateFieldDescription("Or choose a Tabler preset icon:", 18, 138, cardW - 36);

        int presetGap = 7;
        int presetW = (cardW - 36 - (7 * presetGap)) / 8;
        for (int i = 0; i < IconHelper.PresetKeys.Length; i++)
        {
            string key = IconHelper.PresetKeys[i];
            var pBtn = new PresetIconButton(key)
            {
                Location = new Point(18 + i * (presetW + presetGap), 160),
                Size = new Size(presetW, 38)
            };
            pBtn.Click += (_, _) => SelectPreset(key);
            _presetButtons.Add(pBtn);
            _cardTrayIcon.Controls.Add(pBtn);
        }

        _cardTrayIcon.Controls.AddRange([
            _lblTrayIconHeading, _segIconTheme, _previewBox, _btnUpload, _btnResetIcon, _lblPresets
        ]);

        _cardDimensions = new RoundedCard
        {
            Location = new Point(contentLeft, 312),
            Size = new Size(cardW, 108)
        };

        _lblWidthHeading = CreateFieldHeading("Popup Width", 18, 14);
        _lblWidthDesc = CreateFieldDescription("Width of the tray popup panel in logical pixels.", 18, 34, cardW - 36);

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
            Location = new Point(18, 60),
            Size = new Size(cardW - 36, 32),
            SelectedValue = _originalSettings.PopupWidth
        };

        _cardDimensions.Controls.AddRange([_lblWidthHeading, _lblWidthDesc, _segWidth]);
        _secAppearance.Controls.AddRange([_lblAppHeading, _lblAppSub, _cardTrayIcon, _cardDimensions]);

        // ---------------------------------------------------------------------
        // Section 3: Behavior
        // ---------------------------------------------------------------------
        _secBehavior = new Panel
        {
            Location = new Point(0, 0),
            Size = new Size(524, 480),
            Visible = false
        };

        _lblBehHeading = CreateSectionHeading("Behavior", contentLeft, 22);
        _lblBehSub = CreateSectionSubtitle("Control how links open and how memory is reclaimed while idle.", contentLeft, 50, cardW);

        _cardLinks = new RoundedCard
        {
            Location = new Point(contentLeft, 84),
            Size = new Size(cardW, 110)
        };

        _lblLinkHeading = CreateFieldHeading("Link Click Behavior", 18, 14);
        _lblLinkDesc = CreateFieldDescription("Ctrl+Click or middle-click always opens in your default browser.", 18, 34, cardW - 36);
        _segLinkBehavior = new SegmentedControl<string>(
        [
            new SegmentOption<string>("Inside Panel", "popup"),
            new SegmentOption<string>("Default Browser", "browser")
        ])
        {
            Location = new Point(18, 62),
            Size = new Size(cardW - 36, 32),
            SelectedValue = _originalSettings.LinkBehavior == "browser" ? "browser" : "popup"
        };
        _cardLinks.Controls.AddRange([_lblLinkHeading, _lblLinkDesc, _segLinkBehavior]);

        _cardMemory = new RoundedCard
        {
            Location = new Point(contentLeft, 208),
            Size = new Size(cardW, 118)
        };

        _hdrMemory = new IconHeadingRow(TablerIcon.Bolt, "Idle WebView Teardown")
        {
            Location = new Point(18, 14),
            Size = new Size(cardW - 36, 22)
        };
        _lblMemoryDesc = CreateFieldDescription(
            "Disposes WebView2 after staying hidden so idle RAM returns to near zero.",
            18,
            38,
            cardW - 36);

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
            Location = new Point(18, 68),
            Size = new Size(cardW - 36, 32),
            SelectedValue = _originalSettings.IdleTeardownSeconds
        };
        _cardMemory.Controls.AddRange([_hdrMemory, _lblMemoryDesc, _segIdleTeardown]);

        _secBehavior.Controls.AddRange([_lblBehHeading, _lblBehSub, _cardLinks, _cardMemory]);

        // Assemble layout
        _scrollHost.Controls.AddRange([_secGeneral, _secAppearance, _secBehavior]);
        _mainPane.Controls.Add(_scrollHost);
        _mainPane.Controls.Add(_footerPanel);

        Controls.Add(_mainPane);
        Controls.Add(_sidebarPanel);

        AcceptButton = _btnSave;
        CancelButton = _btnCancel;

        PositionFooterButtons();
        SwitchSection(SettingsSection.General);
        ApplyTheme();
        RefreshPreviewImage();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        NativeMethods.ApplyDarkTitleBar(Handle, _palette.IsDark);
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

        _sidebarPanel.UpdateTheme(_palette);
        _navGeneral.UpdateTheme(_palette);
        _navAppearance.UpdateTheme(_palette);
        _navBehavior.UpdateTheme(_palette);
        _lblSidebarVersion.ForeColor = _palette.TextMuted;

        _mainPane.BackColor = _palette.CanvasBackground;
        _scrollHost.BackColor = _palette.CanvasBackground;
        _secGeneral.BackColor = _palette.CanvasBackground;
        _secAppearance.BackColor = _palette.CanvasBackground;
        _secBehavior.BackColor = _palette.CanvasBackground;

        _footerPanel.UpdateTheme(_palette);
        _lblFooterHint.ForeColor = _palette.TextMuted;
        _btnCancel.UpdateTheme(_palette);
        _btnSave.UpdateTheme(_palette);

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
            card.UpdateTheme(_palette);
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
        _txtUrl.UpdateTheme(_palette);
        _txtTitle.UpdateTheme(_palette);
        _tglStartup.UpdateTheme(_palette);

        _segIconTheme.UpdateTheme(_palette);
        UpdatePreviewContainerColor();
        _btnUpload.UpdateTheme(_palette);
        _btnResetIcon.UpdateTheme(_palette);
        foreach (PresetIconButton btn in _presetButtons)
        {
            btn.UpdateTheme(_palette);
        }

        _segWidth.UpdateTheme(_palette);
        _segLinkBehavior.UpdateTheme(_palette);
        _hdrMemory.UpdateTheme(_palette);
        _segIdleTeardown.UpdateTheme(_palette);
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

    private void PositionFooterButtons()
    {
        int right = _footerPanel.ClientSize.Width - 28;
        int top = (_footerPanel.ClientSize.Height - _btnSave.Height) / 2;
        _btnSave.Location = new Point(right - _btnSave.Width, top);
        _btnCancel.Location = new Point(_btnSave.Left - 10 - _btnCancel.Width, top);
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
            _palette.Border);
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

    private static Label CreateSectionHeading(string text, int x, int y) => new()
    {
        Text = text,
        Location = new Point(x, y),
        AutoSize = true,
        Font = new Font("Segoe UI", 15f, FontStyle.Bold)
    };

    private static Label CreateSectionSubtitle(string text, int x, int y, int width) => new()
    {
        Text = text,
        Location = new Point(x, y),
        Size = new Size(width, 20),
        Font = new Font("Segoe UI", 9f, FontStyle.Regular)
    };

    private static Label CreateFieldHeading(string text, int x, int y) => new()
    {
        Text = text,
        Location = new Point(x, y),
        AutoSize = true,
        Font = new Font("Segoe UI", 9.5f, FontStyle.Bold)
    };

    private static Label CreateFieldDescription(string text, int x, int y, int width) => new()
    {
        Text = text,
        Location = new Point(x, y),
        Size = new Size(width, 18),
        Font = new Font("Segoe UI", 8.25f, FontStyle.Regular)
    };

    // -------------------------------------------------------------------------
    // Antigravity UI Controls
    // -------------------------------------------------------------------------

    private sealed class SidebarPanel : Panel
    {
        private readonly Bitmap _brandBitmap;
        private ThemePalette _palette = ThemeHelper.Light;

        public SidebarPanel(Bitmap brandBitmap)
        {
            _brandBitmap = brandBitmap;
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
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;

            var iconRect = new Rectangle(16, 20, 24, 24);
            using (GraphicsPath clip = IconHelper.CreateRoundedRect(iconRect, 6f))
            {
                GraphicsState state = g.Save();
                g.SetClip(clip);
                g.DrawImage(_brandBitmap, iconRect);
                g.Restore(state);
            }

            using var titleFont = new Font("Segoe UI", 11f, FontStyle.Bold);
            TextRenderer.DrawText(
                g,
                "QuickUp",
                titleFont,
                new Rectangle(48, 16, 130, 20),
                _palette.TextPrimary,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter);

            using var subFont = new Font("Segoe UI", 8f, FontStyle.Regular);
            TextRenderer.DrawText(
                g,
                "Settings",
                subFont,
                new Rectangle(48, 34, 130, 16),
                _palette.TextMuted,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter);

            using var borderPen = new Pen(_palette.Border, 1f);
            g.DrawLine(borderPen, Width - 1, 0, Width - 1, Height);
        }
    }

    private sealed class NavRailButton : Control
    {
        private readonly TablerIcon _icon;
        private readonly string _label;
        private ThemePalette _palette = ThemeHelper.Light;
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

        public void UpdateTheme(ThemePalette palette)
        {
            _palette = palette;
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

            if (_active || _hovered)
            {
                Color bg = _active ? _palette.SelectedBackground : _palette.BackgroundHover;
                using GraphicsPath pill = IconHelper.CreateRoundedRect(new RectangleF(0, 0, Width - 1, Height - 1), 8f);
                using var brush = new SolidBrush(bg);
                g.FillPath(brush, pill);
            }

            Color fg = _active ? _palette.SelectedForeground : _hovered ? _palette.TextPrimary : _palette.TextSecondary;
            TablerIcons.Draw(g, _icon, 12f, (Height - 17f) / 2f, 17f, fg, strokeWidth24: 1.9f);

            using var font = new Font("Segoe UI", 9.5f, _active ? FontStyle.Bold : FontStyle.Regular);
            TextRenderer.DrawText(
                g,
                _label,
                font,
                new Rectangle(38, 0, Width - 46, Height),
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

        public RoundedCard()
        {
            DoubleBuffered = true;
        }

        public void UpdateTheme(ThemePalette palette)
        {
            _palette = palette;
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
            using GraphicsPath path = IconHelper.CreateRoundedRect(rect, 10f);
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

        public PreviewTilePanel()
        {
            DoubleBuffered = true;
        }

        public void UpdateAppearance(Color fill, Color border)
        {
            _fill = fill;
            _border = border;
            BackColor = fill;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var rect = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            using GraphicsPath path = IconHelper.CreateRoundedRect(rect, 8f);
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

        public void UpdateTheme(ThemePalette palette)
        {
            _palette = palette;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            TablerIcons.Draw(g, _icon, 0f, (Height - 16f) / 2f, 16f, _palette.AccentBlue, strokeWidth24: 2.0f);
            using var font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            TextRenderer.DrawText(
                g,
                _title,
                font,
                new Rectangle(22, 0, Width - 22, Height),
                _palette.TextPrimary,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
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

        public void UpdateTheme(ThemePalette palette)
        {
            _palette = palette;
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

            var rect = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            using GraphicsPath path = IconHelper.CreateRoundedRect(rect, 8f);

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
            if (_icon.HasValue)
            {
                Size textSize = TextRenderer.MeasureText(g, Text, font);
                float totalW = 15f + 6f + textSize.Width;
                float startX = Math.Max(8f, (Width - totalW) / 2f);
                TablerIcons.Draw(g, _icon.Value, startX, (Height - 15f) / 2f, 15f, fg, strokeWidth24: 2.0f);
                TextRenderer.DrawText(
                    g,
                    Text,
                    font,
                    new Rectangle((int)(startX + 20f), 0, Width - (int)(startX + 22f), Height),
                    fg,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            }
            else
            {
                TextRenderer.DrawText(
                    g,
                    Text,
                    font,
                    ClientRectangle,
                    fg,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
        }
    }

    private sealed record SegmentOption<T>(string Label, T Value, TablerIcon? Icon = null);

    private sealed class SegmentedControl<T> : Control
    {
        private readonly List<SegmentOption<T>> _options;
        private ThemePalette _palette = ThemeHelper.Light;
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

        public void UpdateTheme(ThemePalette palette)
        {
            _palette = palette;
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
            if (_options.Count == 0 || Width <= 0) return -1;
            float segW = (Width - 6f) / _options.Count;
            int idx = (int)((pt.X - 3f) / segW);
            return idx >= 0 && idx < _options.Count ? idx : -1;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var trackRect = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            using (GraphicsPath trackPath = IconHelper.CreateRoundedRect(trackRect, 8f))
            using (var trackBrush = new SolidBrush(_palette.CardSubtle))
            using (var trackPen = new Pen(_palette.Border, 1f))
            {
                g.FillPath(trackBrush, trackPath);
                g.DrawPath(trackPen, trackPath);
            }

            if (_options.Count == 0) return;
            float segW = (Width - 6f) / _options.Count;

            for (int i = 0; i < _options.Count; i++)
            {
                SegmentOption<T> opt = _options[i];
                bool selected = EqualityComparer<T>.Default.Equals(_selectedValue, opt.Value);
                bool hovered = i == _hoveredIndex && !selected;

                var segRect = new RectangleF(3f + i * segW, 3f, segW, Height - 6f);
                if (selected || hovered)
                {
                    using GraphicsPath segPath = IconHelper.CreateRoundedRect(segRect, 6f);
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
                    Size textSize = TextRenderer.MeasureText(g, opt.Label, font);
                    float totalW = 14f + 6f + textSize.Width;
                    float startX = segRect.Left + Math.Max(4f, (segRect.Width - totalW) / 2f);
                    TablerIcons.Draw(g, opt.Icon.Value, startX, segRect.Top + (segRect.Height - 14f) / 2f, 14f, fg, strokeWidth24: 2.0f);
                    TextRenderer.DrawText(
                        g,
                        opt.Label,
                        font,
                        new Rectangle((int)(startX + 19f), (int)segRect.Top, (int)Math.Max(10f, segRect.Right - startX - 20f), (int)segRect.Height),
                        fg,
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                }
                else
                {
                    TextRenderer.DrawText(
                        g,
                        opt.Label,
                        font,
                        Rectangle.Round(segRect),
                        fg,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                }
            }
        }
    }

    private sealed class OutlinedTextBox : Panel
    {
        private readonly TextBox _inner;
        private readonly TablerIcon? _leadingIcon;
        private ThemePalette _palette = ThemeHelper.Light;
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

        public OutlinedTextBox(TablerIcon? leadingIcon)
        {
            _leadingIcon = leadingIcon;
            DoubleBuffered = true;
            Padding = new Padding(leadingIcon.HasValue ? 32 : 11, 7, 11, 7);

            _inner = new TextBox
            {
                BorderStyle = BorderStyle.None,
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Regular)
            };

            _inner.GotFocus += (_, _) => { _focused = true; Invalidate(); };
            _inner.LostFocus += (_, _) => { _focused = false; Invalidate(); };

            Controls.Add(_inner);
            Click += (_, _) => _inner.Focus();
        }

        public void FocusInput() => _inner.Focus();

        public void UpdateTheme(ThemePalette palette)
        {
            _palette = palette;
            BackColor = palette.BackgroundInput;
            _inner.BackColor = palette.BackgroundInput;
            _inner.ForeColor = palette.TextPrimary;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var rect = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            using GraphicsPath path = IconHelper.CreateRoundedRect(rect, 8f);
            using var fill = new SolidBrush(_palette.BackgroundInput);
            g.FillPath(fill, path);

            Color border = _focused ? _palette.AccentBlue : _palette.Border;
            using var pen = new Pen(border, _focused ? 1.5f : 1f);
            g.DrawPath(pen, path);

            if (_leadingIcon.HasValue)
            {
                Color iconColor = _focused ? _palette.AccentBlue : _palette.TextMuted;
                TablerIcons.Draw(g, _leadingIcon.Value, 10f, (Height - 15f) / 2f, 15f, iconColor, strokeWidth24: 1.9f);
            }
        }
    }

    private sealed class PresetIconButton : Control
    {
        private readonly Bitmap _thumbnail;
        private ThemePalette _palette = ThemeHelper.Light;
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
            _thumbnail = IconHelper.RenderPresetIconBitmap(presetKey, 48);
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.UserPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw,
                true);
            Cursor = Cursors.Hand;
        }

        public void UpdateTheme(ThemePalette palette)
        {
            _palette = palette;
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
            using (GraphicsPath path = IconHelper.CreateRoundedRect(rect, 8f))
            using (var bgBrush = new SolidBrush(bg))
            using (var borderPen = new Pen(border, 1f))
            {
                g.FillPath(bgBrush, path);
                g.DrawPath(borderPen, path);
            }

            int iconSize = Math.Min(Width, Height) - 10;
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

        public void UpdateTheme(ThemePalette palette)
        {
            _palette = palette;
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

            var trackRect = new RectangleF(1f, 1f, Width - 2f, Height - 2f);
            Color trackColor = _checked ? _palette.AccentBlue : _palette.BorderHover;

            using (GraphicsPath trackPath = IconHelper.CreateRoundedRect(trackRect, trackRect.Height / 2f))
            using (var trackBrush = new SolidBrush(trackColor))
            {
                g.FillPath(trackBrush, trackPath);
            }

            float thumbSize = trackRect.Height - 6f;
            float thumbX = _checked ? trackRect.Right - thumbSize - 3f : trackRect.Left + 3f;
            float thumbY = trackRect.Top + 3f;
            Color thumbColor = _checked && _palette.IsDark ? _palette.TextOnPrimary : Color.White;

            using var thumbBrush = new SolidBrush(thumbColor);
            g.FillEllipse(thumbBrush, thumbX, thumbY, thumbSize, thumbSize);
        }
    }
}
