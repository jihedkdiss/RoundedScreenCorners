using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using WinRT.Interop;

namespace RoundedScreenCorners.Services;

public sealed class TrayIconService : IDisposable
{
    private const int WmTrayIcon = 0x0401;
    private const int NidAdd = 0;
    private const int NidDelete = 2;
    private const int NifMessage = 0x00000001;
    private const int NifIcon = 0x00000002;
    private const int NifTip = 0x00000004;
    private const int WmLeftButtonUp = 0x0202;
    private const int WmRightButtonUp = 0x0205;
    private const int WmCommand = 0x0111;
    private const int MfString = 0x00000000;
    private const int TpmLeftAlign = 0;
    private const int TpmRightButton = 0x0002;
    private const int MenuShowCmd = 1001;
    private const int MenuExitCmd = 1002;

    private readonly Window _window;
    private nint _hwnd;
    private nint _oldWndProc;
    private nint _contextMenu;
    private nint _hIcon;
    private bool _isIconAdded;
    private NativeMethods.WndProc? _newWndProc;

    public TrayIconService(Window window)
    {
        _window = window;
    }

    public event EventHandler? TrayIconClicked;

    public event EventHandler? ExitRequested;

    public bool IconVisible { get; private set; }

    public void Initialize()
    {
        _hwnd = WindowNative.GetWindowHandle(_window);
        CreateContextMenu();
        EnsureSubclassed();
        AddIcon();
    }

    public void SetIconVisible(bool visible)
    {
        if (visible == IconVisible)
        {
            return;
        }

        if (visible)
        {
            AddIcon();
        }
        else
        {
            RemoveIcon();
        }
    }

    public void Dispose()
    {
        RemoveIcon();
        DestroyContextMenu();
        if (_hwnd != nint.Zero && _oldWndProc != nint.Zero)
        {
            NativeMethods.SetWindowLongPtr(_hwnd, NativeMethods.GwlpWndProc, _oldWndProc);
            _oldWndProc = nint.Zero;
        }
    }

    private void CreateContextMenu()
    {
        _contextMenu = NativeMethods.CreatePopupMenu();
        NativeMethods.AppendMenuW(_contextMenu, MfString, (nint)MenuShowCmd, "Open Rounded Screen Corners");
        NativeMethods.AppendMenuW(_contextMenu, MfString, (nint)MenuExitCmd, "Exit");
    }

    private void DestroyContextMenu()
    {
        if (_contextMenu != nint.Zero)
        {
            NativeMethods.DestroyMenu(_contextMenu);
            _contextMenu = nint.Zero;
        }
    }

    private void EnsureSubclassed()
    {
        _newWndProc = WndProc;
        _oldWndProc = NativeMethods.SetWindowLongPtr(_hwnd, NativeMethods.GwlpWndProc, Marshal.GetFunctionPointerForDelegate(_newWndProc));
    }

    private nint WndProc(nint hwnd, uint message, nint wParam, nint lParam)
    {
        if (message == WmTrayIcon)
        {
            int eventId = (int)(lParam.ToInt64() & 0xFFFF);
            if (eventId == WmLeftButtonUp)
            {
                TrayIconClicked?.Invoke(this, EventArgs.Empty);
            }
            else if (eventId == WmRightButtonUp)
            {
                ShowContextMenu();
            }
        }
        else if (message == WmCommand)
        {
            long commandId = wParam.ToInt64() & 0xFFFF;
            if (commandId == MenuShowCmd)
            {
                TrayIconClicked?.Invoke(this, EventArgs.Empty);
            }
            else if (commandId == MenuExitCmd)
            {
                ExitRequested?.Invoke(this, EventArgs.Empty);
            }
        }

        return NativeMethods.CallWindowProc(_oldWndProc, hwnd, message, wParam, lParam);
    }

    private void ShowContextMenu()
    {
        NativeMethods.GetCursorPos(out NativeMethods.Point point);
        NativeMethods.SetForegroundWindow(_hwnd);
        NativeMethods.TrackPopupMenu(_contextMenu, TpmLeftAlign | TpmRightButton, point.X, point.Y, 0, _hwnd, nint.Zero);
        NativeMethods.PostMessage(_hwnd, 0, nint.Zero, nint.Zero);
    }

    private void AddIcon()
    {
        if (_isIconAdded)
        {
            return;
        }

        _hIcon = LoadAppIcon();
        NativeMethods.NotifyIconData data = new()
        {
            CbSize = (uint)Marshal.SizeOf<NativeMethods.NotifyIconData>(),
            HWnd = _hwnd,
            UId = 1,
            UFlags = NifMessage | NifIcon | NifTip,
            UCallbackMessage = WmTrayIcon,
            HIcon = _hIcon,
            SzTip = "Rounded Screen Corners",
        };

        _isIconAdded = NativeMethods.Shell_NotifyIcon(NidAdd, ref data);
        IconVisible = _isIconAdded;
    }

    private void RemoveIcon()
    {
        if (!_isIconAdded)
        {
            return;
        }

        NativeMethods.NotifyIconData data = new()
        {
            CbSize = (uint)Marshal.SizeOf<NativeMethods.NotifyIconData>(),
            HWnd = _hwnd,
            UId = 1,
        };

        NativeMethods.Shell_NotifyIcon(NidDelete, ref data);
        _isIconAdded = false;
        IconVisible = false;

        if (_hIcon != nint.Zero)
        {
            NativeMethods.DestroyIcon(_hIcon);
            _hIcon = nint.Zero;
        }
    }

    private static nint LoadAppIcon()
    {
        string iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
        if (File.Exists(iconPath))
        {
            int cx = NativeMethods.GetSystemMetrics(NativeMethods.SmCxSmIcon);
            int cy = NativeMethods.GetSystemMetrics(NativeMethods.SmCySmIcon);
            nint handle = NativeMethods.LoadImage(
                nint.Zero,
                iconPath,
                NativeMethods.ImageIcon,
                cx,
                cy,
                NativeMethods.LrLoadFromFile | NativeMethods.LrDefaultSize);
            if (handle != nint.Zero)
            {
                return handle;
            }
        }

        return NativeMethods.LoadIcon(nint.Zero, (nint)32512);
    }

    private static class NativeMethods
    {
        public const int GwlpWndProc = -4;
        public const uint ImageIcon = 1;
        public const uint LrLoadFromFile = 0x00000010;
        public const uint LrDefaultSize = 0x00000040;
        public const int SmCxSmIcon = 49;
        public const int SmCySmIcon = 50;

        public delegate nint WndProc(nint hwnd, uint message, nint wParam, nint lParam);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct NotifyIconData
        {
            public uint CbSize;
            public nint HWnd;
            public uint UId;
            public uint UFlags;
            public uint UCallbackMessage;
            public nint HIcon;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string SzTip;

            public uint DwState;
            public uint DwStateMask;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string SzInfo;

            public uint UTimeoutOrVersion;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
            public string SzInfoTitle;

            public uint DwInfoFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct Point
        {
            public int X;
            public int Y;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        public static extern bool Shell_NotifyIcon(uint dwMessage, [In] ref NotifyIconData lpData);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
        public static extern nint SetWindowLongPtr(nint hWnd, int nIndex, nint dwNewLong);

        [DllImport("user32.dll", EntryPoint = "CallWindowProcW")]
        public static extern nint CallWindowProc(nint lpPrevWndFunc, nint hWnd, uint msg, nint wParam, nint lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern nint LoadIcon(nint hInstance, nint lpIconName);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern nint LoadImage(nint hInst, string name, uint type, int cx, int cy, uint fuLoad);

        [DllImport("user32.dll")]
        public static extern bool DestroyIcon(nint hIcon);

        [DllImport("user32.dll")]
        public static extern int GetSystemMetrics(int nIndex);

        [DllImport("user32.dll")]
        public static extern nint CreatePopupMenu();

        [DllImport("user32.dll")]
        public static extern bool DestroyMenu(nint hMenu);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern bool AppendMenuW(nint hMenu, uint flags, nint idNewItem, string newItem);

        [DllImport("user32.dll")]
        public static extern int TrackPopupMenu(nint hMenu, uint flags, int x, int y, int reserved, nint hwnd, nint rect);

        [DllImport("user32.dll")]
        public static extern bool GetCursorPos(out Point point);

        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(nint hwnd);

        [DllImport("user32.dll")]
        public static extern bool PostMessage(nint hwnd, uint message, nint wParam, nint lParam);
    }
}
