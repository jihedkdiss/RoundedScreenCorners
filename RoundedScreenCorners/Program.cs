using System;
using System.Threading;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;

namespace RoundedScreenCorners;

public static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        WinRT.ComWrappersSupport.InitializeComWrappers();

        if (DecideRedirection())
        {
            return 0;
        }

        Application.Start(parameters =>
        {
            SynchronizationContext.SetSynchronizationContext(
                new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread()));
            _ = new App();
        });

        return 0;
    }

    private static bool DecideRedirection()
    {
        AppInstance keyInstance = AppInstance.FindOrRegisterForKey("RoundedScreenCorners.SingleInstance");
        if (keyInstance.IsCurrent)
        {
            return false;
        }

        AppActivationArguments activatedArgs = AppInstance.GetCurrent().GetActivatedEventArgs();
        keyInstance.RedirectActivationToAsync(activatedArgs).AsTask().Wait();
        return true;
    }
}
