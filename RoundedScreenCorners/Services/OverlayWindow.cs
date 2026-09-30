using System;
using System.Runtime.InteropServices;
using RoundedScreenCorners.Models;

namespace RoundedScreenCorners.Services;

public sealed class OverlayWindow : IDisposable
{
    private const string WindowClassName = "RoundedScreenCorners.NativeOverlay";
    private const int CornerCount = 4;
    private const int BitmapHeaderSize = 40;
    private const int AntiAliasSamplesPerAxis = 4;
    private const double SquircleExponent = 4.0;

    private static readonly NativeMethods.WndProc WindowProc = WndProc;
    private static ushort _classAtom;

    private readonly nint[] _cornerWindows = new nint[CornerCount];
    private readonly bool[] _cornerVisible = new bool[CornerCount];
    private MonitorInfo _monitor;
    private bool _isDisposed;

    public OverlayWindow(MonitorInfo monitor)
    {
        _monitor = monitor;
        EnsureWindowClass();
    }

    public string DeviceName => _monitor.DeviceName;

    public void UpdateMonitor(MonitorInfo monitor)
    {
        _monitor = monitor;
    }

    public void Apply(CornerSettings settings)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        int size = Math.Max(1, settings.Radius + settings.EdgePadding);
        for (int i = 0; i < CornerCount; i++)
        {
            CornerPlacement placement = (CornerPlacement)i;
            if (!settings.IsEnabled || !IsCornerEnabled(settings, placement))
            {
                HideCorner(placement);
                continue;
            }

            nint hwnd = EnsureCornerWindow(placement);
            PositionCornerWindow(hwnd, placement, size);
            PaintCornerWindow(hwnd, placement, settings, size);
            _cornerVisible[i] = true;
            EnsureTopMost(hwnd);
        }
    }

    public void EnsureTopMost()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        for (int i = 0; i < _cornerWindows.Length; i++)
        {
            nint hwnd = _cornerWindows[i];
            if (hwnd != nint.Zero && _cornerVisible[i])
            {
                EnsureTopMost(hwnd);
            }
        }
    }

    public void Close()
    {
        Dispose();
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        for (int i = 0; i < _cornerWindows.Length; i++)
        {
            if (_cornerWindows[i] != nint.Zero)
            {
                NativeMethods.DestroyWindow(_cornerWindows[i]);
                _cornerWindows[i] = nint.Zero;
                _cornerVisible[i] = false;
            }
        }

        _isDisposed = true;
    }

    private static bool IsCornerEnabled(CornerSettings settings, CornerPlacement placement)
    {
        return placement switch
        {
            CornerPlacement.TopLeft => settings.IsTopLeftEnabled,
            CornerPlacement.TopRight => settings.IsTopRightEnabled,
            CornerPlacement.BottomRight => settings.IsBottomRightEnabled,
            CornerPlacement.BottomLeft => settings.IsBottomLeftEnabled,
            _ => true,
        };
    }

    private static void EnsureWindowClass()
    {
        if (_classAtom != 0)
        {
            return;
        }

        NativeMethods.WindowClass windowClass = new()
        {
            Style = 0,
            WndProc = Marshal.GetFunctionPointerForDelegate(WindowProc),
            Instance = NativeMethods.GetModuleHandle(null),
            ClassName = WindowClassName,
        };

        _classAtom = NativeMethods.RegisterClass(ref windowClass);
        if (_classAtom == 0)
        {
            int error = Marshal.GetLastWin32Error();
            if (error != NativeMethods.ErrorClassAlreadyExists)
            {
                throw new InvalidOperationException($"Unable to register overlay window class. Win32 error: {error}");
            }
        }
    }

    private nint EnsureCornerWindow(CornerPlacement placement)
    {
        int index = (int)placement;
        if (_cornerWindows[index] != nint.Zero)
        {
            return _cornerWindows[index];
        }

        nint hwnd = NativeMethods.CreateWindowEx(
            NativeMethods.WsExLayered | NativeMethods.WsExNoActivate | NativeMethods.WsExToolWindow | NativeMethods.WsExTransparent,
            WindowClassName,
            "Rounded screen corner mask",
            NativeMethods.WsPopup,
            0,
            0,
            1,
            1,
            nint.Zero,
            nint.Zero,
            NativeMethods.GetModuleHandle(null),
            nint.Zero);

        if (hwnd == nint.Zero)
        {
            throw new InvalidOperationException($"Unable to create overlay window. Win32 error: {Marshal.GetLastWin32Error()}");
        }

        _cornerWindows[index] = hwnd;
        return hwnd;
    }

    private void HideCorner(CornerPlacement placement)
    {
        nint hwnd = _cornerWindows[(int)placement];
        if (hwnd != nint.Zero)
        {
            NativeMethods.ShowWindow(hwnd, NativeMethods.SwHide);
            _cornerVisible[(int)placement] = false;
        }
    }

    private void PositionCornerWindow(nint hwnd, CornerPlacement placement, int size)
    {
        DisplayRect bounds = _monitor.Bounds;
        int x = placement is CornerPlacement.TopRight or CornerPlacement.BottomRight
            ? bounds.Right - size
            : bounds.Left;
        int y = placement is CornerPlacement.BottomLeft or CornerPlacement.BottomRight
            ? bounds.Bottom - size
            : bounds.Top;

        NativeMethods.SetWindowPos(
            hwnd,
            new nint(NativeMethods.HwndTopmost),
            x,
            y,
            size,
            size,
            NativeMethods.SwpNoActivate | NativeMethods.SwpShowWindow);
    }

    private static void EnsureTopMost(nint hwnd)
    {
        NativeMethods.SetWindowPos(
            hwnd,
            new nint(NativeMethods.HwndTopmost),
            0,
            0,
            0,
            0,
            NativeMethods.SwpNoMove | NativeMethods.SwpNoSize | NativeMethods.SwpNoActivate | NativeMethods.SwpShowWindow);
        NativeMethods.ShowWindow(hwnd, NativeMethods.SwShowNoActivate);
    }

    private static unsafe void PaintCornerWindow(nint hwnd, CornerPlacement placement, CornerSettings settings, int size)
    {
        using NativeBitmap bitmap = NativeBitmap.Create(size, size);
        byte baseAlpha = (byte)Math.Clamp((int)Math.Round(settings.MaskColor.A * settings.Opacity), 0, 255);

        uint* pixels = (uint*)bitmap.Bits;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                double coverage = GetMaskCoverage(x, y, size, placement, settings.RoundingMode);
                if (coverage > 0)
                {
                    byte alpha = (byte)Math.Clamp((int)Math.Round(baseAlpha * coverage), 0, 255);
                    byte red = Premultiply(settings.MaskColor.R, alpha);
                    byte green = Premultiply(settings.MaskColor.G, alpha);
                    byte blue = Premultiply(settings.MaskColor.B, alpha);
                    pixels[(y * size) + x] = (uint)((alpha << 24) | (red << 16) | (green << 8) | blue);
                }
            }
        }

        nint screenDc = NativeMethods.GetDC(nint.Zero);
        if (screenDc == nint.Zero)
        {
            return;
        }

        try
        {
            NativeMethods.Point source = new();
            NativeMethods.Size windowSize = new(size, size);
            NativeMethods.BlendFunction blend = new()
            {
                BlendOp = NativeMethods.AcSrcOver,
                SourceConstantAlpha = 255,
                AlphaFormat = NativeMethods.AcSrcAlpha,
            };

            NativeMethods.UpdateLayeredWindow(
                hwnd,
                screenDc,
                nint.Zero,
                ref windowSize,
                bitmap.MemoryDc,
                ref source,
                0,
                ref blend,
                NativeMethods.UlwAlpha);
        }
        finally
        {
            NativeMethods.ReleaseDC(nint.Zero, screenDc);
        }
    }

    private static double GetMaskCoverage(int x, int y, int size, CornerPlacement placement, RoundingMode roundingMode)
    {
        int maskedSamples = 0;
        int sampleCount = AntiAliasSamplesPerAxis * AntiAliasSamplesPerAxis;

        for (int sampleY = 0; sampleY < AntiAliasSamplesPerAxis; sampleY++)
        {
            for (int sampleX = 0; sampleX < AntiAliasSamplesPerAxis; sampleX++)
            {
                double px = x + ((sampleX + 0.5) / AntiAliasSamplesPerAxis);
                double py = y + ((sampleY + 0.5) / AntiAliasSamplesPerAxis);
                if (ShouldMaskPoint(px, py, size, placement, roundingMode))
                {
                    maskedSamples++;
                }
            }
        }

        return maskedSamples / (double)sampleCount;
    }

    private static bool ShouldMaskPoint(double x, double y, int size, CornerPlacement placement, RoundingMode roundingMode)
    {
        double cornerX = placement is CornerPlacement.TopRight or CornerPlacement.BottomRight ? size - x : x;
        double cornerY = placement is CornerPlacement.BottomLeft or CornerPlacement.BottomRight ? size - y : y;
        double normalizedX = (size - cornerX) / size;
        double normalizedY = (size - cornerY) / size;

        return roundingMode == RoundingMode.Squircle
            ? Math.Pow(normalizedX, SquircleExponent) + Math.Pow(normalizedY, SquircleExponent) > 1.0
            : (normalizedX * normalizedX) + (normalizedY * normalizedY) > 1.0;
    }

    private static byte Premultiply(byte channel, byte alpha)
    {
        return (byte)((channel * alpha + 127) / 255);
    }

    private static nint WndProc(nint hwnd, uint message, nint wParam, nint lParam)
    {
        return NativeMethods.DefWindowProc(hwnd, message, wParam, lParam);
    }

    private enum CornerPlacement
    {
        TopLeft,
        TopRight,
        BottomRight,
        BottomLeft,
    }

    private sealed class NativeBitmap : IDisposable
    {
        private NativeBitmap(nint memoryDc, nint bitmap, nint oldBitmap, nint bits)
        {
            MemoryDc = memoryDc;
            Bitmap = bitmap;
            OldBitmap = oldBitmap;
            Bits = bits;
        }

        public nint MemoryDc { get; }

        public nint Bits { get; }

        private nint Bitmap { get; }

        private nint OldBitmap { get; }

        public static NativeBitmap Create(int width, int height)
        {
            nint screenDc = NativeMethods.GetDC(nint.Zero);
            if (screenDc == nint.Zero)
            {
                throw new InvalidOperationException($"Unable to get screen DC. Win32 error: {Marshal.GetLastWin32Error()}");
            }

            try
            {
                nint memoryDc = NativeMethods.CreateCompatibleDC(screenDc);
                if (memoryDc == nint.Zero)
                {
                    throw new InvalidOperationException($"Unable to create memory DC. Win32 error: {Marshal.GetLastWin32Error()}");
                }

                NativeMethods.BitmapInfo bitmapInfo = new()
                {
                    Header = new NativeMethods.BitmapInfoHeader
                    {
                        Size = BitmapHeaderSize,
                        Width = width,
                        Height = -height,
                        Planes = 1,
                        BitCount = 32,
                        Compression = NativeMethods.BiRgb,
                    },
                };

                nint bitmap = NativeMethods.CreateDIBSection(memoryDc, ref bitmapInfo, NativeMethods.DibRgbColors, out nint bits, nint.Zero, 0);
                if (bitmap == nint.Zero || bits == nint.Zero)
                {
                    NativeMethods.DeleteDC(memoryDc);
                    throw new InvalidOperationException($"Unable to create overlay bitmap. Win32 error: {Marshal.GetLastWin32Error()}");
                }

                nint oldBitmap = NativeMethods.SelectObject(memoryDc, bitmap);
                return new NativeBitmap(memoryDc, bitmap, oldBitmap, bits);
            }
            finally
            {
                NativeMethods.ReleaseDC(nint.Zero, screenDc);
            }
        }

        public void Dispose()
        {
            if (MemoryDc != nint.Zero)
            {
                NativeMethods.SelectObject(MemoryDc, OldBitmap);
                NativeMethods.DeleteObject(Bitmap);
                NativeMethods.DeleteDC(MemoryDc);
            }
        }
    }

    private static class NativeMethods
    {
        public const int ErrorClassAlreadyExists = 1410;
        public const int HwndTopmost = -1;
        public const int SwHide = 0;
        public const int SwShowNoActivate = 4;
        public const uint SwpNoActivate = 0x0010;
        public const uint SwpNoMove = 0x0002;
        public const uint SwpNoSize = 0x0001;
        public const uint SwpShowWindow = 0x0040;
        public const uint WsPopup = 0x80000000;
        public const uint WsExLayered = 0x00080000;
        public const uint WsExNoActivate = 0x08000000;
        public const uint WsExToolWindow = 0x00000080;
        public const uint WsExTransparent = 0x00000020;
        public const byte AcSrcOver = 0;
        public const byte AcSrcAlpha = 1;
        public const uint BiRgb = 0;
        public const uint DibRgbColors = 0;
        public const uint UlwAlpha = 0x00000002;

        public delegate nint WndProc(nint hwnd, uint message, nint wParam, nint lParam);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct WindowClass
        {
            public uint Style;
            public nint WndProc;
            public int ClassExtra;
            public int WindowExtra;
            public nint Instance;
            public nint Icon;
            public nint Cursor;
            public nint Background;
            public string? MenuName;
            public string ClassName;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct Point
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct Size
        {
            public Size(int cx, int cy)
            {
                Cx = cx;
                Cy = cy;
            }

            public int Cx;
            public int Cy;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct BlendFunction
        {
            public byte BlendOp;
            public byte BlendFlags;
            public byte SourceConstantAlpha;
            public byte AlphaFormat;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct BitmapInfoHeader
        {
            public int Size;
            public int Width;
            public int Height;
            public ushort Planes;
            public ushort BitCount;
            public uint Compression;
            public uint SizeImage;
            public int XPelsPerMeter;
            public int YPelsPerMeter;
            public uint ClrUsed;
            public uint ClrImportant;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct BitmapInfo
        {
            public BitmapInfoHeader Header;
            public uint Colors;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern nint GetModuleHandle(string? moduleName);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern ushort RegisterClass(ref WindowClass windowClass);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern nint CreateWindowEx(
            uint extendedStyle,
            string className,
            string windowName,
            uint style,
            int x,
            int y,
            int width,
            int height,
            nint parentWindow,
            nint menu,
            nint instance,
            nint parameter);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool DestroyWindow(nint hwnd);

        [DllImport("user32.dll")]
        public static extern bool ShowWindow(nint hwnd, int commandShow);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool SetWindowPos(nint hwnd, nint hwndInsertAfter, int x, int y, int cx, int cy, uint flags);

        [DllImport("user32.dll")]
        public static extern nint DefWindowProc(nint hwnd, uint message, nint wParam, nint lParam);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern nint GetDC(nint hwnd);

        [DllImport("user32.dll")]
        public static extern int ReleaseDC(nint hwnd, nint hdc);

        [DllImport("gdi32.dll", SetLastError = true)]
        public static extern nint CreateCompatibleDC(nint hdc);

        [DllImport("gdi32.dll", SetLastError = true)]
        public static extern bool DeleteDC(nint hdc);

        [DllImport("gdi32.dll", SetLastError = true)]
        public static extern nint CreateDIBSection(nint hdc, ref BitmapInfo pbmi, uint usage, out nint bits, nint section, uint offset);

        [DllImport("gdi32.dll", SetLastError = true)]
        public static extern nint SelectObject(nint hdc, nint hgdiobj);

        [DllImport("gdi32.dll", SetLastError = true)]
        public static extern bool DeleteObject(nint obj);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool UpdateLayeredWindow(
            nint hwnd,
            nint hdcDestination,
            nint pptDestination,
            ref Size psize,
            nint hdcSource,
            ref Point pptSource,
            uint colorKey,
            ref BlendFunction blend,
            uint flags);
    }
}
