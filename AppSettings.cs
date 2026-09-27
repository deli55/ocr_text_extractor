namespace OcrTextExtractor;

/// <summary>
/// Represents the application settings that are persisted to disk.
/// </summary>
public class AppSettings
{
    /// <summary>
    /// The key for the region OCR hotkey. Default: "F1".
    /// </summary>
    public string RegionHotkeyKey { get; set; } = "F1";

    /// <summary>
    /// The modifier flags for the region OCR hotkey. Default: 0 (MOD_NONE).
    /// Possible values: 0 (none), 2 (MOD_CONTROL), 4 (MOD_SHIFT), 8 (MOD_WIN), 1 (MOD_ALT), or combinations.
    /// </summary>
    public uint RegionHotkeyModifiers { get; set; } = 0;

    /// <summary>
    /// The key for the fullscreen OCR hotkey. Default: "F2".
    /// </summary>
    public string FullscreenHotkeyKey { get; set; } = "F2";

    /// <summary>
    /// The modifier flags for the fullscreen OCR hotkey. Default: 0 (MOD_NONE).
    /// Possible values: 0 (none), 2 (MOD_CONTROL), 4 (MOD_SHIFT), 8 (MOD_WIN), 1 (MOD_ALT), or combinations.
    /// </summary>
    public uint FullscreenHotkeyModifiers { get; set; } = 0;

    /// <summary>
    /// Whether to automatically copy extracted text to clipboard. Default: true.
    /// </summary>
    public bool AutoCopy { get; set; } = true;

    /// <summary>
    /// Returns a human-readable display string for the hotkey, e.g. "Ctrl + F1" or "F1".
    /// </summary>
    public string RegionHotkeyDisplay => FormatHotkeyDisplay(RegionHotkeyKey, RegionHotkeyModifiers);

    /// <summary>
    /// Returns a human-readable display string for the hotkey, e.g. "Ctrl + F2" or "F2".
    /// </summary>
    public string FullscreenHotkeyDisplay => FormatHotkeyDisplay(FullscreenHotkeyKey, FullscreenHotkeyModifiers);

    /// <summary>
    /// Formats a hotkey display string from a key name and modifier flags.
    /// </summary>
    private static string FormatHotkeyDisplay(string key, uint modifiers)
    {
        var parts = new System.Collections.Generic.List<string>();

        if ((modifiers & HotkeyManager.MOD_CONTROL) != 0)
            parts.Add("Ctrl");
        if ((modifiers & HotkeyManager.MOD_ALT) != 0)
            parts.Add("Alt");
        if ((modifiers & HotkeyManager.MOD_SHIFT) != 0)
            parts.Add("Shift");
        if ((modifiers & HotkeyManager.MOD_WIN) != 0)
            parts.Add("Win");

        parts.Add(key);

        return string.Join(" + ", parts);
    }
}
