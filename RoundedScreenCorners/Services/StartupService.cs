using System.Threading.Tasks;
using Windows.ApplicationModel;

namespace RoundedScreenCorners.Services;

public static class StartupService
{
    private const string TaskId = "RoundedScreenCornersStartup";

    public static async Task SetStartupAsync(bool enable)
    {
        StartupTask task = await StartupTask.GetAsync(TaskId);

        if (enable)
        {
            if (task.State == StartupTaskState.Disabled)
            {
                await task.RequestEnableAsync();
            }
        }
        else if (task.State == StartupTaskState.Enabled)
        {
            task.Disable();
        }
    }

    public static async Task<bool> IsStartupEnabledAsync()
    {
        StartupTask task = await StartupTask.GetAsync(TaskId);
        return task.State is StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy;
    }
}
