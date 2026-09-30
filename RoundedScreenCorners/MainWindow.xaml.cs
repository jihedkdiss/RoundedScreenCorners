using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using RoundedScreenCorners.Models;
using RoundedScreenCorners.Services;
using Windows.Foundation;
using Windows.Graphics;
using WinRT.Interop;

namespace RoundedScreenCorners;

public sealed partial class MainWindow : Window
{
    private readonly SettingsStore _settingsStore = new();
    private readonly ObservableCollection<MonitorInfo> _monitors = new();
    private CornerSettings _settings;
    private readonly OverlayCoordinator _overlayCoordinator;
    private readonly TrayIconService _trayIconService;
    private bool _loading;

    public MainWindow()
    {
        _loading = true;
        InitializeComponent();
        ConfigureTitleBar();
        ApplyMicaBackdrop();

        _settings = _settingsStore.Load();
        _overlayCoordinator = new OverlayCoordinator(_settings);
        _trayIconService = new TrayIconService(this);
        _trayIconService.TrayIconClicked += OnTrayIconClicked;
        _trayIconService.ExitRequested += OnExitRequested;
        _trayIconService.Initialize();

        Closed += OnClosed;
        AppWindow.Closing += OnAppWindowClosing;

        try
        {
            ConfigureControls();
            ResizeWindow();
            RefreshMonitors();
            LoadSettingsIntoControls();
            PopulateAboutInfo();
            ApplyTrayIconVisibility();
        }
        finally
        {
            _loading = false;
        }

        ApplySettings();
        _overlayCoordinator.Start();
    }

    private void ConfigureTitleBar()
    {
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        nint hwnd = WindowNative.GetWindowHandle(this);
        WindowId windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
        AppWindow appWindow = AppWindow.GetFromWindowId(windowId);
        appWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
        appWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
        appWindow.TitleBar.ButtonHoverBackgroundColor = Colors.Transparent;
        appWindow.TitleBar.ButtonPressedBackgroundColor = Colors.Transparent;

        string iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
        if (File.Exists(iconPath))
        {
            appWindow.SetIcon(iconPath);
        }
    }

    private void ApplyMicaBackdrop()
    {
        SystemBackdrop = new MicaBackdrop { Kind = Microsoft.UI.Composition.SystemBackdrops.MicaKind.BaseAlt };
    }

    private void ConfigureControls()
    {
        RadiusSlider.Minimum = CornerSettings.MinRadius;
        RadiusSlider.Maximum = CornerSettings.MaxRadius;
        RadiusSlider.StepFrequency = 1;

        OpacitySlider.Minimum = 20;
        OpacitySlider.Maximum = 100;
        OpacitySlider.StepFrequency = 1;

        EdgePaddingBox.Minimum = 0;
        EdgePaddingBox.Maximum = 48;
        EdgePaddingBox.SmallChange = 1;
        EdgePaddingBox.LargeChange = 4;
    }

    private void ResizeWindow()
    {
        nint hwnd = WindowNative.GetWindowHandle(this);
        WindowId windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
        AppWindow appWindow = AppWindow.GetFromWindowId(windowId);
        appWindow.Resize(new SizeInt32(820, 680));
    }

    private void RefreshMonitors()
    {
        _monitors.Clear();
        foreach (MonitorInfo monitor in _overlayCoordinator.GetMonitors())
        {
            _monitors.Add(monitor);
        }

        MonitorListView.ItemsSource = _monitors;
        SpecificMonitorComboBox.ItemsSource = _monitors;

        MonitorInfo? selected = _monitors.FirstOrDefault(monitor => monitor.DeviceName == _settings.SpecificMonitorDeviceName)
            ?? _monitors.FirstOrDefault(monitor => monitor.IsPrimary)
            ?? _monitors.FirstOrDefault();

        SpecificMonitorComboBox.SelectedItem = selected;
        if (selected is not null)
        {
            _settings.SpecificMonitorDeviceName = selected.DeviceName;
        }
    }

    private void LoadSettingsIntoControls()
    {
        bool wasLoading = _loading;
        _loading = true;

        EnabledToggle.IsOn = _settings.IsEnabled;
        RadiusSlider.Value = _settings.Radius;
        RoundingModeComboBox.SelectedIndex = _settings.RoundingMode == RoundingMode.Circle ? 0 : 1;
        MaskColorComboBox.SelectedIndex = GetColorIndex(_settings.MaskColor);
        OpacitySlider.Value = _settings.Opacity * 100;
        EdgePaddingBox.Value = _settings.EdgePadding;
        TopLeftCheckBox.IsChecked = _settings.IsTopLeftEnabled;
        TopRightCheckBox.IsChecked = _settings.IsTopRightEnabled;
        BottomRightCheckBox.IsChecked = _settings.IsBottomRightEnabled;
        BottomLeftCheckBox.IsChecked = _settings.IsBottomLeftEnabled;
        AllDisplaysToggle.IsOn = _settings.ShowOnAllDisplays;
        StartupToggle.IsOn = _settings.StartWithWindows;
        HideTrayIconToggle.IsOn = _settings.HideTrayIcon;
        StartMinimizedToggle.IsOn = _settings.StartMinimized;
        ThemeComboBox.SelectedIndex = Math.Clamp(_settings.Theme, 0, 2);

        UpdateDynamicState();
        ApplyTheme();
        _loading = wasLoading;
    }

    private void CaptureSettingsFromControls()
    {
        if (_loading)
        {
            return;
        }

        _settings.IsEnabled = EnabledToggle.IsOn;
        _settings.Radius = (int)Math.Round(RadiusSlider.Value);
        _settings.RoundingMode = RoundingModeComboBox.SelectedIndex == 0 ? RoundingMode.Circle : RoundingMode.Squircle;
        _settings.MaskColor = GetSelectedColor();
        _settings.Opacity = OpacitySlider.Value / 100.0;
        _settings.EdgePadding = SanitizeNumber(EdgePaddingBox.Value, _settings.EdgePadding);
        _settings.IsTopLeftEnabled = TopLeftCheckBox.IsChecked == true;
        _settings.IsTopRightEnabled = TopRightCheckBox.IsChecked == true;
        _settings.IsBottomRightEnabled = BottomRightCheckBox.IsChecked == true;
        _settings.IsBottomLeftEnabled = BottomLeftCheckBox.IsChecked == true;
        _settings.ShowOnAllDisplays = AllDisplaysToggle.IsOn;
        _settings.StartWithWindows = StartupToggle.IsOn;
        _settings.HideTrayIcon = HideTrayIconToggle.IsOn;
        _settings.StartMinimized = StartMinimizedToggle.IsOn;
        _settings.Theme = ThemeComboBox.SelectedIndex < 0 ? 0 : ThemeComboBox.SelectedIndex;

        if (SpecificMonitorComboBox.SelectedItem is MonitorInfo monitor)
        {
            _settings.SpecificMonitorDeviceName = monitor.DeviceName;
        }

        _settings.Sanitize();
    }

    private void ApplySettings()
    {
        if (_loading)
        {
            return;
        }

        CaptureSettingsFromControls();
        UpdateDynamicState();
        _settingsStore.Save(_settings);
        _overlayCoordinator.UpdateSettings(_settings);
        ApplyTrayIconVisibility();
    }

    private void UpdateDynamicState()
    {
        RadiusLabel.Text = $"{(int)Math.Round(RadiusSlider.Value)} px";
        OpacityLabel.Text = $"{(int)Math.Round(OpacitySlider.Value)}%";
        SpecificMonitorCard.Visibility = AllDisplaysToggle.IsOn ? Visibility.Collapsed : Visibility.Visible;
        UpdatePreview();
    }

    private void ApplyTheme()
    {
        RootGrid.RequestedTheme = ThemeComboBox.SelectedIndex switch
        {
            1 => ElementTheme.Light,
            2 => ElementTheme.Dark,
            _ => ElementTheme.Default,
        };
    }

    private void ApplyTrayIconVisibility()
    {
        _trayIconService.SetIconVisible(!_settings.HideTrayIcon);
    }

    private void PopulateAboutInfo()
    {
        Version? version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        string versionText = version is null ? "1.0" : $"{version.Major}.{version.Minor}.{version.Build}";
        HomeVersionText.Text = $"v{versionText}";
        AboutVersionText.Text = $"Version {versionText}";
    }

    private void UpdatePreview()
    {
        int size = (int)Math.Round(RadiusSlider.Value + EdgePaddingBox.Value);
        SolidColorBrush brush = new(GetSelectedColor())
        {
            Opacity = OpacitySlider.Value / 100.0,
        };
        ApplyPreviewPath(PreviewTopLeft, CreatePreviewGeometry(size), brush, size, EnabledToggle.IsOn && TopLeftCheckBox.IsChecked == true);
        ApplyPreviewPath(PreviewTopRight, CreatePreviewGeometry(size), brush, size, EnabledToggle.IsOn && TopRightCheckBox.IsChecked == true);
        ApplyPreviewPath(PreviewBottomRight, CreatePreviewGeometry(size), brush, size, EnabledToggle.IsOn && BottomRightCheckBox.IsChecked == true);
        ApplyPreviewPath(PreviewBottomLeft, CreatePreviewGeometry(size), brush, size, EnabledToggle.IsOn && BottomLeftCheckBox.IsChecked == true);
    }

    private Geometry CreatePreviewGeometry(int size)
    {
        return RoundingModeComboBox.SelectedIndex == 0
            ? CreateCircularCornerGeometry(size)
            : CreateSquircleCornerGeometry(size);
    }

    private static void ApplyPreviewPath(Microsoft.UI.Xaml.Shapes.Path path, Geometry geometry, Brush brush, int size, bool isVisible)
    {
        path.Width = size;
        path.Height = size;
        path.Data = geometry;
        path.Fill = brush;
        path.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
    }

    private Windows.UI.Color GetSelectedColor()
    {
        return MaskColorComboBox.SelectedIndex switch
        {
            1 => Colors.DarkGray,
            2 => Colors.White,
            _ => Colors.Black,
        };
    }

    private static int GetColorIndex(Windows.UI.Color color)
    {
        if (color.R > 220 && color.G > 220 && color.B > 220)
        {
            return 2;
        }

        if (color.R > 40 || color.G > 40 || color.B > 40)
        {
            return 1;
        }

        return 0;
    }

    private static int SanitizeNumber(double value, int fallback)
    {
        return double.IsNaN(value) ? fallback : (int)Math.Round(value);
    }

    private static Geometry CreateSquircleCornerGeometry(double size)
    {
        PathGeometry geometry = new();
        PathFigure mask = new()
        {
            StartPoint = new Point(size, size),
            IsClosed = true,
            IsFilled = true,
        };
        mask.Segments.Add(new LineSegment { Point = new Point(0, size) });

        const int Samples = 36;
        const double Exponent = 4.0;
        for (int i = 1; i <= Samples; i++)
        {
            double t = (Math.PI / 2.0) * (1.0 - (i / (double)Samples));
            mask.Segments.Add(new LineSegment
            {
                Point = new Point(
                    size * Math.Pow(Math.Abs(Math.Cos(t)), 2.0 / Exponent),
                    size * Math.Pow(Math.Abs(Math.Sin(t)), 2.0 / Exponent)),
            });
        }

        mask.Segments.Add(new LineSegment { Point = new Point(size, size) });
        geometry.Figures.Add(mask);
        return geometry;
    }

    private static Geometry CreateCircularCornerGeometry(double size)
    {
        PathGeometry geometry = new();
        PathFigure mask = new()
        {
            StartPoint = new Point(size, size),
            IsClosed = true,
            IsFilled = true,
        };

        mask.Segments.Add(new LineSegment { Point = new Point(0, size) });
        const int Samples = 36;
        for (int i = 1; i <= Samples; i++)
        {
            double t = (Math.PI / 2.0) * (1.0 - (i / (double)Samples));
            mask.Segments.Add(new LineSegment
            {
                Point = new Point(size * Math.Cos(t), size * Math.Sin(t)),
            });
        }

        mask.Segments.Add(new LineSegment { Point = new Point(size, size) });
        geometry.Figures.Add(mask);
        return geometry;
    }

    private void ShowPage(string tag)
    {
        HomePage.Visibility = tag == "home" ? Visibility.Visible : Visibility.Collapsed;
        CornersPage.Visibility = tag == "corners" ? Visibility.Visible : Visibility.Collapsed;
        DisplaysPage.Visibility = tag == "displays" ? Visibility.Visible : Visibility.Collapsed;
        SettingsPage.Visibility = tag == "settings" ? Visibility.Visible : Visibility.Collapsed;
        AboutPage.Visibility = tag == "about" ? Visibility.Visible : Visibility.Collapsed;
    }

    private void NavigateTo(string tag)
    {
        if (tag == "settings")
        {
            ShellNavigation.SelectedItem = ShellNavigation.SettingsItem;
            return;
        }

        foreach (NavigationViewItem item in ShellNavigation.MenuItems.OfType<NavigationViewItem>().Concat(ShellNavigation.FooterMenuItems.OfType<NavigationViewItem>()))
        {
            if (item.Tag is string itemTag && itemTag == tag)
            {
                ShellNavigation.SelectedItem = item;
                return;
            }
        }
    }

    private void ExitApplication()
    {
        Closed -= OnClosed;
        AppWindow.Closing -= OnAppWindowClosing;
        _loading = false;
        CaptureSettingsFromControls();
        _settingsStore.Save(_settings);
        _overlayCoordinator.Dispose();
        _trayIconService.Dispose();
        Close();
    }

    private void OnShellNavigationSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.IsSettingsSelected)
        {
            ShowPage("settings");
        }
        else if (args.SelectedItem is NavigationViewItem item && item.Tag is string tag)
        {
            ShowPage(tag);
        }
    }

    private void OnDashboardCardClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.Tag is string tag)
        {
            NavigateTo(tag);
        }
    }

    private void OnSettingChanged(object sender, RoutedEventArgs e)
    {
        ApplySettings();
    }

    private void OnRangeSettingChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        ApplySettings();
    }

    private void OnNumberSettingChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        ApplySettings();
    }

    private void OnSelectionSettingChanged(object sender, SelectionChangedEventArgs e)
    {
        ApplySettings();
    }

    private async void OnStartupToggled(object sender, RoutedEventArgs e)
    {
        if (_loading)
        {
            return;
        }

        ApplySettings();
        await StartupService.SetStartupAsync(_settings.StartWithWindows);
    }

    private void OnHideTrayIconToggled(object sender, RoutedEventArgs e)
    {
        ApplySettings();
    }

    private void OnThemeChanged(object sender, SelectionChangedEventArgs e)
    {
        ApplySettings();
        ApplyTheme();
    }

    private void OnRefreshDisplaysClick(object sender, RoutedEventArgs e)
    {
        RefreshMonitors();
        ApplySettings();
    }

    private void OnResetAllClick(object sender, RoutedEventArgs e)
    {
        _settings = new CornerSettings();
        RefreshMonitors();
        LoadSettingsIntoControls();
        ApplySettings();
    }

    private void OnExitClick(object sender, RoutedEventArgs e)
    {
        ExitApplication();
    }

    private void OnTrayIconClicked(object? sender, EventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            AppWindow.Show();
            Activate();
            NativeMethods.SetForegroundWindow(WindowNative.GetWindowHandle(this));
        });
    }

    private void OnExitRequested(object? sender, EventArgs e)
    {
        DispatcherQueue.TryEnqueue(ExitApplication);
    }

    private void OnAppWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        args.Cancel = true;
        sender.Hide();
    }

    private void OnClosed(object sender, WindowEventArgs args)
    {
        _loading = false;
        CaptureSettingsFromControls();
        _settingsStore.Save(_settings);
        _overlayCoordinator.Dispose();
        _trayIconService.Dispose();
    }

    private static class NativeMethods
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(nint hwnd);
    }
}
