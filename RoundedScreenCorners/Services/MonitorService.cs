using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using RoundedScreenCorners.Models;

namespace RoundedScreenCorners.Services;

public sealed class MonitorService
{
    public IReadOnlyList<MonitorInfo> GetMonitors()
    {
        List<MonitorInfo> monitors = new();
        NativeMethods.EnumDisplayMonitors(
            nint.Zero,
            nint.Zero,
            (monitorHandle, _, _, _) =>
            {
                if (TryGetMonitor(monitorHandle, monitors.Count, out MonitorInfo monitor))
                {
                    monitors.Add(monitor);
                }

                return true;
            },
            nint.Zero);

        return monitors;
    }

    public MonitorInfo? GetPrimaryMonitor()
    {
        return GetMonitors().FirstOrDefault(monitor => monitor.IsPrimary);
    }

    public MonitorInfo? GetMonitorByDeviceName(string? deviceName)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
        {
            return null;
        }

        return GetMonitors().FirstOrDefault(monitor =>
            string.Equals(monitor.DeviceName, deviceName, StringComparison.OrdinalIgnoreCase));
    }

    private static bool TryGetMonitor(nint monitorHandle, int index, out MonitorInfo monitor)
    {
        NativeMethods.MonitorInfoEx nativeInfo = new()
        {
            Size = Marshal.SizeOf<NativeMethods.MonitorInfoEx>(),
        };

        if (!NativeMethods.GetMonitorInfo(monitorHandle, ref nativeInfo))
        {
            monitor = null!;
            return false;
        }

        monitor = new MonitorInfo(
            monitorHandle,
            nativeInfo.DeviceName,
            (nativeInfo.Flags & NativeMethods.MonitorInfoPrimary) != 0,
            ToDisplayRect(nativeInfo.Monitor),
            ToDisplayRect(nativeInfo.WorkArea),
            index);
        return true;
    }

    private static DisplayRect ToDisplayRect(NativeMethods.Rect rect)
    {
        return new DisplayRect(rect.Left, rect.Top, rect.Right, rect.Bottom);
    }

    private static class NativeMethods
    {
        public const uint MonitorInfoPrimary = 0x00000001;

        public delegate bool MonitorEnumProc(nint hMonitor, nint hdcMonitor, nint lprcMonitor, nint dwData);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool EnumDisplayMonitors(nint hdc, nint lprcClip, MonitorEnumProc lpfnEnum, nint dwData);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetMonitorInfo(nint hMonitor, ref MonitorInfoEx lpmi);

        [StructLayout(LayoutKind.Sequential)]
        public struct Rect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct MonitorInfoEx
        {
            public int Size;
            public Rect Monitor;
            public Rect WorkArea;
            public uint Flags;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string DeviceName;
        }
    }
}
