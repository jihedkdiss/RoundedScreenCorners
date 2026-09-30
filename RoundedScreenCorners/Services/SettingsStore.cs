using System;
using System.IO;
using System.Text.Json;
using Microsoft.Win32;
using RoundedScreenCorners.Models;
using Windows.UI;

namespace RoundedScreenCorners.Services;

public sealed class SettingsStore
{
    private const string LegacyRegistryKeyPath = @"SOFTWARE\RoundedScreen";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    private readonly string _settingsPath;
    private readonly string _tempPath;

    public SettingsStore()
    {
        string directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RoundedScreenCorners");
        Directory.CreateDirectory(directory);
        _settingsPath = Path.Combine(directory, "settings.json");
        _tempPath = Path.Combine(directory, "settings.tmp");
    }

    public CornerSettings Load()
    {
        if (!File.Exists(_settingsPath))
        {
            return LoadLegacySettings();
        }

        try
        {
            string json = File.ReadAllText(_settingsPath);
            return JsonSerializer.Deserialize<CornerSettings>(json, JsonOptions) ?? new CornerSettings();
        }
        catch
        {
            return new CornerSettings();
        }
    }

    public void Save(CornerSettings settings)
    {
        settings.Sanitize();
        string json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(_tempPath, json);
        File.Move(_tempPath, _settingsPath, overwrite: true);
    }

    private static CornerSettings LoadLegacySettings()
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(LegacyRegistryKeyPath, false);
        if (key is null)
        {
            return new CornerSettings();
        }

        CornerSettings settings = new()
        {
            Radius = ReadInt(key, "CornerSize", 28),
            RoundingMode = ReadInt(key, "RoundingMode", 1) == 0 ? RoundingMode.Circle : RoundingMode.Squircle,
            MaskColor = Microsoft.UI.Colors.Black,
        };
        settings.Sanitize();
        return settings;
    }

    private static int ReadInt(RegistryKey key, string name, int fallback)
    {
        try
        {
            return Convert.ToInt32(key.GetValue(name, fallback), System.Globalization.CultureInfo.InvariantCulture);
        }
        catch (FormatException)
        {
            return fallback;
        }
        catch (InvalidCastException)
        {
            return fallback;
        }
    }
}
