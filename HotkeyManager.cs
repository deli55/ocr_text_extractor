using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Windows.Input;

namespace OcrTextExtractor;

/// <summary>
/// Manages global Win32 hotkeys via P/Invoke to user32.dll.
/// </summary>
public static class HotkeyManager
{
    // Win32 API Constants
    public const int WM_HOTKEY = 0x0312;
    public const uint MOD_NONE = 0x0000;
    public const uint MOD_NOREPEAT = 0x4000;
    public const uint MOD_ALT = 0x0001;
    public const uint MOD_CONTROL = 0x0002;
    public const uint MOD_SHIFT = 0x0004;
    public const uint MOD_WIN = 0x0008;

    // P/Invoke declarations
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    /// <summary>
    /// Registers a global hotkey.
    /// </summary>
    /// <param name="hWnd">Window handle that will receive the WM_HOTKEY message.</param>
    /// <param name="id">Unique identifier for the hotkey (must be unique per hotkey).</param>
    /// <param name="key">The main key to register.</param>
    /// <param name="modifiers">Optional modifiers (e.g., MOD_CONTROL | MOD_SHIFT). Default is 0 (no modifiers).</param>
    /// <returns>True if the hotkey was registered successfully; otherwise, false.</returns>
    public static bool Register(IntPtr hWnd, int id, Key key, uint modifiers = 0)
    {
        // Convert System.Windows.Input.Key to VirtualKey
        uint virtualKey = (uint)KeyInterop.VirtualKeyFromKey(key);
        return RegisterHotKey(hWnd, id, modifiers | MOD_NOREPEAT, virtualKey);
    }

    /// <summary>
    /// Unregisters a hotkey by its ID.
    /// </summary>
    /// <param name="hWnd">Window handle that registered the hotkey.</param>
    /// <param name="id">The unique identifier of the hotkey to unregister.</param>
    /// <returns>True if the hotkey was unregistered successfully; otherwise, false.</returns>
    public static bool Unregister(IntPtr hWnd, int id)
    {
        return UnregisterHotKey(hWnd, id);
    }
}
