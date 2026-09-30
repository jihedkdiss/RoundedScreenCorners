using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Dispatching;
using RoundedScreenCorners.Models;

namespace RoundedScreenCorners.Services;

public sealed class OverlayCoordinator : IDisposable
{
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly DispatcherQueueTimer _topmostTimer;
    private readonly MonitorService _monitorService = new();
    private readonly Dictionary<string, OverlayWindow> _windowsByMonitor = new(StringComparer.OrdinalIgnoreCase);
    private CornerSettings _settings;
    private bool _isDisposed;

    public OverlayCoordinator(CornerSettings settings)
    {
        _settings = settings.Clone();
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        _topmostTimer = _dispatcherQueue.CreateTimer();
        _topmostTimer.Interval = TimeSpan.FromSeconds(2);
        _topmostTimer.Tick += OnTopmostTimerTick;
    }

    public void Start()
    {
        ReconcileWindows();
        _topmostTimer.Start();
    }

    public IReadOnlyList<MonitorInfo> GetMonitors()
    {
        return _monitorService.GetMonitors();
    }

    public void UpdateSettings(CornerSettings settings)
    {
        _settings = settings.Clone();
        ReconcileWindows();
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        foreach (OverlayWindow window in _windowsByMonitor.Values)
        {
            window.Close();
        }

        _topmostTimer.Stop();
        _topmostTimer.Tick -= OnTopmostTimerTick;
        _windowsByMonitor.Clear();
        _isDisposed = true;
    }

    private void ReconcileWindows()
    {
        Dictionary<string, MonitorInfo> monitors = GetTargetMonitors()
            .ToDictionary(monitor => monitor.DeviceName, StringComparer.OrdinalIgnoreCase);

        foreach (MonitorInfo monitor in monitors.Values)
        {
            if (!_windowsByMonitor.TryGetValue(monitor.DeviceName, out OverlayWindow? window))
            {
                window = new OverlayWindow(monitor);
                _windowsByMonitor[monitor.DeviceName] = window;
            }
            else
            {
                window.UpdateMonitor(monitor);
            }

            window.Apply(_settings);
        }

        foreach (string staleDeviceName in _windowsByMonitor.Keys.Except(monitors.Keys, StringComparer.OrdinalIgnoreCase).ToList())
        {
            _windowsByMonitor[staleDeviceName].Close();
            _windowsByMonitor.Remove(staleDeviceName);
        }
    }

    private void OnTopmostTimerTick(DispatcherQueueTimer sender, object args)
    {
        foreach (OverlayWindow window in _windowsByMonitor.Values)
        {
            window.EnsureTopMost();
        }
    }

    private IEnumerable<MonitorInfo> GetTargetMonitors()
    {
        IReadOnlyList<MonitorInfo> monitors = _monitorService.GetMonitors();
        if (_settings.ShowOnAllDisplays)
        {
            return monitors;
        }

        MonitorInfo? specificMonitor = _monitorService.GetMonitorByDeviceName(_settings.SpecificMonitorDeviceName);
        return new[] { specificMonitor ?? monitors.FirstOrDefault(monitor => monitor.IsPrimary) ?? monitors.First() };
    }
}
