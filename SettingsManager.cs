using System.IO;
using System.Text.Json;

namespace OcrTextExtractor;

/// <summary>
/// Manages loading and saving application settings to a JSON file.
/// </summary>
public static class SettingsManager
{
    private static readonly string SettingsPath = 
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Loads settings from the JSON file. Returns defaults if the file is missing or corrupted.
    /// </summary>
    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
            {
                return new AppSettings();
            }

            var json = File.ReadAllText(SettingsPath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);

            if (settings == null)
            {
                return new AppSettings();
            }

            return settings;
        }
        catch
        {
            // Handle corrupted or invalid JSON gracefully
            return new AppSettings();
        }
    }

    /// <summary>
    /// Saves the given settings to the JSON file.
    /// </summary>
    public static void Save(AppSettings settings)
    {
        try
        {
            var json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(SettingsPath, json);
        }
        catch
        {
            // Silently fail - settings persistence is not critical
        }
    }
}
