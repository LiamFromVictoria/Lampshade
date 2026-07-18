using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Lampshade;

/// <summary>
/// The "Settings" window opened from the tray menu. A segmented pill nav swaps
/// between two panels — general dimming behavior, and the low-blue-light filter —
/// styled dark/flat to match the tray menu instead of the default WinForms chrome.
/// Every control applies and persists immediately; there is no separate Save button.
/// </summary>
internal sealed class SettingsForm : Form
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int valueSize);

    private const int DwmwaUseImmersiveDarkMode = 20;

    private readonly Action<int> _onDimPercentChanged;
    private readonly Action<int> _onLowBlueLightPercentChanged;
    private readonly Action<bool> _onStartWithWindowsChanged;
    private readonly Action<DimMethod> _onDimMethodChanged;

    private readonly Button _generalNavButton;
    private readonly Button _lowBlueLightNavButton;
    private readonly Panel _generalPanel;
    private readonly Panel _lowBlueLightPanel;

    public SettingsForm(
        AppSettings settings,
        bool startWithWindows,
        Icon windowIcon,
        Action<int> onDimPercentChanged,
        Action<int> onLowBlueLightPercentChanged,
        Action<bool> onStartWithWindowsChanged,
        Action<DimMethod> onDimMethodChanged)
    {
        _onDimPercentChanged = onDimPercentChanged;
        _onLowBlueLightPercentChanged = onLowBlueLightPercentChanged;
        _onStartWithWindowsChanged = onStartWithWindowsChanged;
        _onDimMethodChanged = onDimMethodChanged;

        Text = "Lampshade Settings";
        Icon = windowIcon;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = true;
        TopMost = true;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(380, 370);
        BackColor = Theme.WindowBackground;
        ForeColor = Theme.TextPrimary;
        Font = Theme.FontRegular;
        Padding = new Padding(20);

        // --- Segmented nav ---
        var navHost = new Panel
        {
            Location = new Point(20, 20),
            Size = new Size(340, 34),
            BackColor = Theme.NavBackground,
        };
        _generalNavButton = CreateNavButton("General", new Point(2, 2), 168);
        _lowBlueLightNavButton = CreateNavButton("Low Blue Light", new Point(170, 2), 168);
        navHost.Controls.Add(_generalNavButton);
        navHost.Controls.Add(_lowBlueLightNavButton);
        _generalNavButton.Click += (_, _) => SelectTab(general: true);
        _lowBlueLightNavButton.Click += (_, _) => SelectTab(general: false);

        // --- General panel ---
        _generalPanel = new Panel
        {
            Location = new Point(20, 68),
            Size = new Size(340, 270),
            BackColor = Theme.WindowBackground,
        };

        var dimLabel = new Label
        {
            Text = "Dim intensity",
            Font = Theme.FontSemibold,
            ForeColor = Theme.TextPrimary,
            Location = new Point(0, 8),
            AutoSize = true,
        };
        var dimSlider = new ModernSlider
        {
            Minimum = 10,
            Maximum = 90,
            Value = Math.Clamp(settings.DimPercent, 10, 90),
            Location = new Point(0, 36),
            Size = new Size(270, 28),
        };
        var dimValueLabel = new Label
        {
            Text = $"{dimSlider.Value}%",
            ForeColor = Theme.TextSecondary,
            Location = new Point(280, 40),
            AutoSize = true,
        };
        dimSlider.ValueChanged += (_, _) =>
        {
            dimValueLabel.Text = $"{dimSlider.Value}%";
            _onDimPercentChanged(dimSlider.Value);
        };

        var divider = new Panel
        {
            Location = new Point(0, 80),
            Size = new Size(340, 1),
            BackColor = Theme.Border,
        };

        var methodLabel = new Label
        {
            Text = "Dimming method",
            Font = Theme.FontSemibold,
            ForeColor = Theme.TextPrimary,
            Location = new Point(0, 96),
            AutoSize = true,
        };
        var methodDropdown = new ModernDropdown
        {
            Items = new[] { "Overlay", "Gamma Ramp (Native)" },
            SelectedIndex = settings.DimMethod == DimMethod.GammaRamp ? 1 : 0,
            Location = new Point(0, 122),
            Size = new Size(210, 30),
        };
        methodDropdown.SelectedIndexChanged += (_, _) =>
            _onDimMethodChanged(methodDropdown.SelectedIndex == 1 ? DimMethod.GammaRamp : DimMethod.Overlay);
        var methodHint = new Label
        {
            Text = "Overlay draws a per-monitor window and works on any GPU. Gamma Ramp adjusts "
                + "the display driver directly (NVIDIA/AMD/Intel) and also dims fullscreen-exclusive "
                + "games, but very strong settings may be ignored by some drivers.",
            Font = Theme.FontSmall,
            ForeColor = Theme.TextSecondary,
            Location = new Point(0, 160),
            Size = new Size(340, 48),
        };

        var divider2 = new Panel
        {
            Location = new Point(0, 216),
            Size = new Size(340, 1),
            BackColor = Theme.Border,
        };

        var startupLabel = new Label
        {
            Text = "Start with Windows",
            Font = Theme.FontSemibold,
            ForeColor = Theme.TextPrimary,
            Location = new Point(0, 232),
            AutoSize = true,
        };
        var startupHint = new Label
        {
            Text = "Launch automatically at sign-in.",
            Font = Theme.FontSmall,
            ForeColor = Theme.TextSecondary,
            Location = new Point(0, 254),
            AutoSize = true,
        };
        var startupToggle = new ModernToggle
        {
            Location = new Point(300, 234),
        };
        startupToggle.SetInitialChecked(startWithWindows);
        startupToggle.CheckedChanged += (_, _) => _onStartWithWindowsChanged(startupToggle.Checked);

        _generalPanel.Controls.Add(dimLabel);
        _generalPanel.Controls.Add(dimSlider);
        _generalPanel.Controls.Add(dimValueLabel);
        _generalPanel.Controls.Add(divider);
        _generalPanel.Controls.Add(methodLabel);
        _generalPanel.Controls.Add(methodDropdown);
        _generalPanel.Controls.Add(methodHint);
        _generalPanel.Controls.Add(divider2);
        _generalPanel.Controls.Add(startupLabel);
        _generalPanel.Controls.Add(startupHint);
        _generalPanel.Controls.Add(startupToggle);

        // --- Low Blue Light panel ---
        _lowBlueLightPanel = new Panel
        {
            Location = new Point(20, 68),
            Size = new Size(340, 270),
            BackColor = Theme.WindowBackground,
            Visible = false,
        };

        var warmthLabel = new Label
        {
            Text = "Warmth intensity",
            Font = Theme.FontSemibold,
            ForeColor = Theme.TextPrimary,
            Location = new Point(0, 8),
            AutoSize = true,
        };
        var warmthSlider = new ModernSlider
        {
            Minimum = 10,
            Maximum = 100,
            Value = Math.Clamp(settings.LowBlueLightPercent, 10, 100),
            Location = new Point(0, 36),
            Size = new Size(270, 28),
        };
        var warmthValueLabel = new Label
        {
            Text = $"{warmthSlider.Value}%",
            ForeColor = Theme.TextSecondary,
            Location = new Point(280, 40),
            AutoSize = true,
        };
        warmthSlider.ValueChanged += (_, _) =>
        {
            warmthValueLabel.Text = $"{warmthSlider.Value}%";
            _onLowBlueLightPercentChanged(warmthSlider.Value);
        };

        var warmthHint = new Label
        {
            Text = "Warms the display to reduce blue light output, independent of Dim Mode.",
            Font = Theme.FontSmall,
            ForeColor = Theme.TextSecondary,
            Location = new Point(0, 80),
            Size = new Size(330, 40),
        };

        _lowBlueLightPanel.Controls.Add(warmthLabel);
        _lowBlueLightPanel.Controls.Add(warmthSlider);
        _lowBlueLightPanel.Controls.Add(warmthValueLabel);
        _lowBlueLightPanel.Controls.Add(warmthHint);

        // --- Close button ---
        var closeButton = new Button
        {
            Text = "Close",
            DialogResult = DialogResult.Cancel,
            Location = new Point(280, 332),
            Size = new Size(80, 28),
            FlatStyle = FlatStyle.Flat,
            BackColor = Theme.PanelBackground,
            ForeColor = Theme.TextPrimary,
            Font = Theme.FontRegular,
            Cursor = Cursors.Hand,
        };
        closeButton.FlatAppearance.BorderColor = Theme.Border;
        closeButton.FlatAppearance.MouseOverBackColor = Theme.Border;
        closeButton.FlatAppearance.MouseDownBackColor = Theme.Border;
        closeButton.Click += (_, _) => Close();

        Controls.Add(navHost);
        Controls.Add(_generalPanel);
        Controls.Add(_lowBlueLightPanel);
        Controls.Add(closeButton);
        CancelButton = closeButton;
        AcceptButton = closeButton;

        SelectTab(general: true);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        var useDarkMode = 1;
        DwmSetWindowAttribute(Handle, DwmwaUseImmersiveDarkMode, ref useDarkMode, sizeof(int));
    }

    private Button CreateNavButton(string text, Point location, int width)
    {
        var button = new Button
        {
            Text = text,
            Location = location,
            Size = new Size(width, 30),
            FlatStyle = FlatStyle.Flat,
            Font = Theme.FontSemibold,
            Cursor = Cursors.Hand,
            TextAlign = ContentAlignment.MiddleCenter,
        };
        button.FlatAppearance.BorderSize = 0;
        return button;
    }

    private void SelectTab(bool general)
    {
        _generalPanel.Visible = general;
        _lowBlueLightPanel.Visible = !general;

        StyleNavButton(_generalNavButton, selected: general);
        StyleNavButton(_lowBlueLightNavButton, selected: !general);
    }

    private static void StyleNavButton(Button button, bool selected)
    {
        button.BackColor = selected ? Theme.Accent : Theme.NavBackground;
        button.ForeColor = selected ? Theme.TextOnAccent : Theme.TextSecondary;
        button.FlatAppearance.MouseOverBackColor = selected ? Theme.AccentHover : Theme.Border;
        button.FlatAppearance.MouseDownBackColor = selected ? Theme.AccentPressed : Theme.Border;
    }
}
