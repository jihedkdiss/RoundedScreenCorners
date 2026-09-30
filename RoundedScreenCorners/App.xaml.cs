using System;
using System.IO;
using System.Linq;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using RoundedScreenCorners.Services;
using WinRT.Interop;

namespace RoundedScreenCorners;

public partial class App : Application
{
    private readonly string _crashLogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "RoundedScreenCorners",
        "crash.log");

    private MainWindow? _window;

    public App()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_crashLogPath)!);
        UnhandledException += OnUnhandledException;
        InitializeComponent();
        AppInstance.GetCurrent().Activated += OnAppInstanceActivated;
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();

        bool startupActivation = AppInstance.GetCurrent().GetActivatedEventArgs()?.Kind == ExtendedActivationKind.StartupTask;
        bool startMinimized =
            Environment.GetCommandLineArgs().Contains("--minimized", StringComparer.OrdinalIgnoreCase)
            || (startupActivation && new SettingsStore().Load().StartMinimized);

        if (!startMinimized)
        {
            _window.Activate();
        }
    }

    private void OnAppInstanceActivated(object? sender, AppActivationArguments args)
    {
        if (_window is null)
        {
            return;
        }

        _window.DispatcherQueue.TryEnqueue(() =>
        {
            if (_window is null)
            {
                return;
            }

            nint hwnd = WindowNative.GetWindowHandle(_window);
            WindowId windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
            AppWindow appWindow = AppWindow.GetFromWindowId(windowId);
            appWindow.Show();
            _window.Activate();
            NativeMethods.SetForegroundWindow(hwnd);
        });
    }

    private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        File.AppendAllText(_crashLogPath, $"{DateTime.Now:u} {e.Exception}\n\n");
    }

    private static class NativeMethods
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(nint hwnd);
    }
}
