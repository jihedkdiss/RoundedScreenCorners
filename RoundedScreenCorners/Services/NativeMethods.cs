using System;
using System.Runtime.InteropServices;

namespace RoundedScreenCorners.Services;

internal static partial class NativeMethods
{
    internal const int GwlExStyle = -20;
    internal const int GwlStyle = -16;
    internal const int HwndTopmost = -1;
    internal const int SwpNoActivate = 0x0010;
    internal const int SwpNoMove = 0x0002;
    internal const int SwpNoSize = 0x0001;
    internal const int WsCaption = 0x00C00000;
    internal const int WsExNoActivate = 0x08000000;
    internal const int WsExToolWindow = 0x00000080;
    internal const int WsExTransparent = 0x00000020;
    internal const int WsThickFrame = 0x00040000;

    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    internal static partial IntPtr GetWindowLongPtr(IntPtr hwnd, int index);

    [LibraryImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    internal static partial IntPtr SetWindowLongPtr(IntPtr hwnd, int index, IntPtr newStyle);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool SetWindowPos(IntPtr hwnd, IntPtr hwndInsertAfter, int x, int y, int cx, int cy, uint flags);
}
