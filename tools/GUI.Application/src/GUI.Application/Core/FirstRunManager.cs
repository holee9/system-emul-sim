using System.IO;
using System.Text.Json;

namespace XrayDetector.Gui.Core;

/// <summary>
/// Manages first-run detection for the application (SPEC-HELP-001 Wave 2).
/// Stores state in %LOCALAPPDATA%/XrayDetector/settings.json by default.
/// </summary>
public class FirstRunManager
{
    private readonly string _settingsFilePath;

    /// <summary>
    /// Creates a FirstRunManager using the default settings path
    /// (%LOCALAPPDATA%/XrayDetector/settings.json).
    /// </summary>
    public FirstRunManager()
        : this(GetDefaultSettingsPath())
    {
    }

    /// <summary>
    /// Creates a FirstRunManager using a custom settings path (for testing).
    /// </summary>
    public FirstRunManager(string settingsFilePath)
    {
        _settingsFilePath = settingsFilePath;
    }

    /// <summary>
    /// Returns true if this is the first run (settings file does not exist
    /// or does not contain firstRunCompleted: true).
    /// </summary>
    public bool IsFirstRun()
    {
        if (!File.Exists(_settingsFilePath))
            return true;

        try
        {
            var json = File.ReadAllText(_settingsFilePath);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("firstRunCompleted", out var prop))
                return !prop.GetBoolean();
        }
        catch
        {
            // Corrupted or unreadable settings → treat as first run
        }

        return true;
    }

    /// <summary>
    /// Writes {"firstRunCompleted": true} to the settings file,
    /// creating the directory if it does not exist.
    /// </summary>
    public void MarkFirstRunComplete()
    {
        var dir = Path.GetDirectoryName(_settingsFilePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(new { firstRunCompleted = true },
            new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_settingsFilePath, json);
    }

    private static string GetDefaultSettingsPath()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, "XrayDetector", "settings.json");
    }
}
