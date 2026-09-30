using System;
using System.Text.Json.Serialization;
using Microsoft.UI;
using Windows.UI;

namespace RoundedScreenCorners.Models;

public sealed class CornerSettings
{
    public const int MinRadius = 4;
    public const int MaxRadius = 96;

    public int Radius { get; set; } = 28;

    public RoundingMode RoundingMode { get; set; } = RoundingMode.Squircle;

    public string MaskColorHex { get; set; } = "#FF000000";

    [JsonIgnore]
    public Color MaskColor
    {
        get => ParseColor(MaskColorHex);
        set => MaskColorHex = FormattableString.Invariant($"#{value.A:X2}{value.R:X2}{value.G:X2}{value.B:X2}");
    }

    public double Opacity { get; set; } = 1.0;

    public bool IsTopLeftEnabled { get; set; } = true;

    public bool IsTopRightEnabled { get; set; } = true;

    public bool IsBottomRightEnabled { get; set; } = true;

    public bool IsBottomLeftEnabled { get; set; } = true;

    public bool IsEnabled { get; set; } = true;

    public bool ShowOnAllDisplays { get; set; } = true;

    public bool StartWithWindows { get; set; } = true;

    public string? SpecificMonitorDeviceName { get; set; }

    public int Theme { get; set; }

    public bool HideTrayIcon { get; set; }

    public bool StartMinimized { get; set; } = true;

    public int EdgePadding { get; set; }

    public int PreviewScale { get; set; } = 100;

    public CornerSettings Clone()
    {
        return (CornerSettings)MemberwiseClone();
    }

    public void Sanitize()
    {
        Radius = Math.Clamp(Radius, MinRadius, MaxRadius);
        Opacity = Math.Clamp(Opacity, 0.2, 1.0);
        EdgePadding = Math.Clamp(EdgePadding, 0, 48);
        PreviewScale = Math.Clamp(PreviewScale, 75, 150);
        Theme = Math.Clamp(Theme, 0, 2);
    }

    private static Color ParseColor(string? value)
    {
        if (!string.IsNullOrWhiteSpace(value) && value.Length == 9 && value[0] == '#')
        {
            try
            {
                return Color.FromArgb(
                    Convert.ToByte(value.Substring(1, 2), 16),
                    Convert.ToByte(value.Substring(3, 2), 16),
                    Convert.ToByte(value.Substring(5, 2), 16),
                    Convert.ToByte(value.Substring(7, 2), 16));
            }
            catch (FormatException)
            {
            }
        }

        return Colors.Black;
    }
}
