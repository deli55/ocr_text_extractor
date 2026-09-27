using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using FormApplication = System.Windows.Forms.Application;
using FormNotifyIcon = System.Windows.Forms.NotifyIcon;
using FormContextMenu = System.Windows.Forms.ContextMenuStrip;
using FormToolStripMenuItem = System.Windows.Forms.ToolStripButton;
using FormToolStripSeparator = System.Windows.Forms.ToolStripSeparator;
using WpfMessageBox = System.Windows.MessageBox;
using WpfMessageBoxImage = System.Windows.MessageBoxImage;

namespace OcrTextExtractor;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    // Hotkey IDs
    private const int HotkeyIdRegionOcr = 1001;
    private const int HotkeyIdFullscreenOcr = 1002;

    // Current hotkey configurations (key + modifiers)
    private string _regionHotkey = "F1";
    private uint _regionHotkeyModifiers = 0;
    private string _fullscreenHotkey = "F2";
    private uint _fullscreenHotkeyModifiers = 0;

    // Rebinding state
    private bool _isRebindingRegion = false;
    private bool _isRebindingFullscreen = false;
    private uint _pendingModifiers = 0;

    // Window handle for hotkey registration
    private IntPtr _hWnd = IntPtr.Zero;

    // OCR service instance
    private readonly OcrService _ocrService = new OcrService();

    // Application settings
    private AppSettings _settings = new AppSettings();

    // System Tray Icon
    private NotifyIcon? _notifyIcon;
    private bool _isMinimizedToTray = false;
    private bool _isClosing = false;

    public MainWindow()
    {
        InitializeComponent();
        InitializeTrayIcon();
    }

    /// <summary>
    /// Initializes the system tray icon with context menu.
    /// </summary>
    private void InitializeTrayIcon()
    {
        // Create the notify icon
        _notifyIcon = new FormNotifyIcon
        {
            Visible = true,
            Icon = System.Drawing.Icon.ExtractAssociatedIcon(Process.GetCurrentProcess().MainModule?.FileName ?? ""),
            Text = "OCR Text Extractor"
        };

        // Create context menu
        var contextMenu = new ContextMenuStrip();
        
        var openMenuItem = new ToolStripMenuItem("Open OCR Extractor");
        openMenuItem.Click += (s, e) => RestoreWindow();
        contextMenu.Items.Add(openMenuItem);

        contextMenu.Items.Add(new ToolStripSeparator());

        var ocrRegionMenuItem = new ToolStripMenuItem("OCR Region");
        ocrRegionMenuItem.Click += (s, e) => _ = Task.Run(() => ExecuteOcrRegionAsync());
        contextMenu.Items.Add(ocrRegionMenuItem);

        var ocrFullscreenMenuItem = new ToolStripMenuItem("OCR Fullscreen");
        ocrFullscreenMenuItem.Click += (s, e) => _ = Task.Run(() => ExecuteOcrFullscreenAsync());
        contextMenu.Items.Add(ocrFullscreenMenuItem);

        contextMenu.Items.Add(new ToolStripSeparator());

        var exitMenuItem = new ToolStripMenuItem("Exit");
        exitMenuItem.Click += (s, e) => CloseApplication();
        contextMenu.Items.Add(exitMenuItem);

        _notifyIcon.ContextMenuStrip = contextMenu;

        // Double-click on tray icon restores window
        _notifyIcon.DoubleClick += (s, e) => RestoreWindow();
    }

    /// <summary>
    /// Restores the window from minimized/tray state.
    /// </summary>
    private void RestoreWindow()
    {
        this.Show();
        this.WindowState = WindowState.Normal;
        this.Activate();
        this.Focus();
        _isMinimizedToTray = false;
    }

    /// <summary>
    /// Shows a toast/balloon notification from the system tray.
    /// </summary>
    private void ShowToastNotification(string title, string message)
    {
        if (_notifyIcon != null)
        {
            _notifyIcon.ShowBalloonTip(2000, title, message, ToolTipIcon.Info);
        }
    }

    /// <summary>
    /// Called when the window's underlying HWND is created.
    /// Loads settings and registers hotkeys here.
    /// </summary>
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        // Get the HWND for this window
        _hWnd = new WindowInteropHelper(this).Handle;

        // Attach a WndProc hook to receive WM_HOTKEY messages
        var source = HwndSource.FromHwnd(_hWnd);
        if (source != null)
        {
            source.AddHook(WndProc);
        }

        // Load persisted settings
        LoadSettings();
    }

    /// <summary>
    /// Called when the window state changes (handles minimize to tray).
    /// </summary>
    protected void MainWindow_StateChanged(object? sender, EventArgs e)
    {
        if (this.WindowState == WindowState.Minimized && !_isClosing)
        {
            // Hide the window and minimize to tray
            this.ShowInTaskbar = false;
            this.Hide();
            _isMinimizedToTray = true;

            // Show a balloon tip when minimized
            if (_notifyIcon != null)
            {
                _notifyIcon.ShowBalloonTip(1000, "OCR Text Extractor", "Minimized to system tray. Double-click the tray icon to restore.", ToolTipIcon.None);
            }
        }
    }

    /// <summary>
    /// Called when the window is closing.
    /// </summary>
    protected void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        // If user clicks the X button, minimize to tray instead of closing
        if (!_isClosing)
        {
            e.Cancel = true;
            this.Hide();
            _isMinimizedToTray = true;

            if (_notifyIcon != null)
            {
                _notifyIcon.ShowBalloonTip(1000, "OCR Text Extractor", "Minimized to system tray. Use the tray icon to exit or activate the app.", ToolTipIcon.None);
            }
        }
    }

    /// <summary>
    /// Called when the window is closed. Saves settings.
    /// </summary>
    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);

        // Save settings on close
        SaveSettings();

        // Unregister all hotkeys
        UnregisterAllHotkeys();
    }

    /// <summary>
    /// Closes the application completely from the tray context menu.
    /// </summary>
    private void CloseApplication()
    {
        _isClosing = true;
        _isMinimizedToTray = false;

        // Save settings
        SaveSettings();

        // Unregister hotkeys
        UnregisterAllHotkeys();

        // Clean up tray icon
        if (_notifyIcon != null)
        {
            _notifyIcon.Dispose();
            _notifyIcon = null;
        }

        // Close the window
        System.Windows.Application.Current.Shutdown();
    }

    /// <summary>
    /// Loads settings from disk and applies them to the UI and hotkeys.
    /// </summary>
    private void LoadSettings()
    {
        _settings = SettingsManager.Load();

        // Restore hotkey values
        _regionHotkey = _settings.RegionHotkeyKey;
        _regionHotkeyModifiers = _settings.RegionHotkeyModifiers;
        _fullscreenHotkey = _settings.FullscreenHotkeyKey;
        _fullscreenHotkeyModifiers = _settings.FullscreenHotkeyModifiers;

        // Restore AutoCopy checkbox
        AutoCopyCheckbox.IsChecked = _settings.AutoCopy;

        // Register hotkeys with modifiers
        RegisterHotkeys();

        // Update UI to reflect settings
        UpdateHotkeyButtonLabels();
        UpdateOcrButtonHotkeyLabels();
    }

    /// <summary>
    /// Saves current settings to disk.
    /// </summary>
    private void SaveSettings()
    {
        _settings.RegionHotkeyKey = _regionHotkey;
        _settings.RegionHotkeyModifiers = _regionHotkeyModifiers;
        _settings.FullscreenHotkeyKey = _fullscreenHotkey;
        _settings.FullscreenHotkeyModifiers = _fullscreenHotkeyModifiers;
        _settings.AutoCopy = AutoCopyCheckbox.IsChecked == true;

        SettingsManager.Save(_settings);
    }

    /// <summary>
    /// Registers all hotkeys using the current configuration.
    /// </summary>
    private void RegisterHotkeys()
    {
        if (_hWnd == IntPtr.Zero) return;

        // Unregister any existing hotkeys first
        UnregisterAllHotkeys();

        // Register region OCR hotkey with modifiers
        var regionKey = ParseKey(_regionHotkey);
        HotkeyManager.Register(_hWnd, HotkeyIdRegionOcr, regionKey, _regionHotkeyModifiers);

        // Register fullscreen OCR hotkey with modifiers
        var fullscreenKey = ParseKey(_fullscreenHotkey);
        HotkeyManager.Register(_hWnd, HotkeyIdFullscreenOcr, fullscreenKey, _fullscreenHotkeyModifiers);
    }

    /// <summary>
    /// Unregisters all registered hotkeys.
    /// </summary>
    private void UnregisterAllHotkeys()
    {
        if (_hWnd == IntPtr.Zero) return;

        HotkeyManager.Unregister(_hWnd, HotkeyIdRegionOcr);
        HotkeyManager.Unregister(_hWnd, HotkeyIdFullscreenOcr);
    }

    /// <summary>
    /// Parses a string representation of a key into a System.Windows.Input.Key.
    /// </summary>
    private static Key ParseKey(string keyName)
    {
        if (Enum.TryParse<Key>(keyName, ignoreCase: true, out var key))
        {
            return key;
        }

        // Default to [ if parsing fails
        return Key.Oem4;
    }

    /// <summary>
    /// Updates the hotkey button labels to reflect current configuration including modifiers.
    /// </summary>
    private void UpdateHotkeyButtonLabels()
    {
        var regionDisplay = FormatHotkeyDisplay(_regionHotkey, _regionHotkeyModifiers);
        var fullscreenDisplay = FormatHotkeyDisplay(_fullscreenHotkey, _fullscreenHotkeyModifiers);
        BtnChangeRegionHotkey.Content = $"Change Region Hotkey (Current: {regionDisplay})";
        BtnChangeFullscreenHotkey.Content = $"Change Fullscreen Hotkey (Current: {fullscreenDisplay})";
    }

    /// <summary>
    /// Updates the OCR action button hotkey labels (e.g., "OCR Specific Part [Q]").
    /// </summary>
    private void UpdateOcrButtonHotkeyLabels()
    {
        var regionDisplay = FormatHotkeyDisplay(_regionHotkey, _regionHotkeyModifiers);
        var fullscreenDisplay = FormatHotkeyDisplay(_fullscreenHotkey, _fullscreenHotkeyModifiers);
        BtnOcrRegion.Content = $"OCR Specific Part        [{regionDisplay}]";
        BtnOcrFullscreen.Content = $"OCR Entire Screen        [{fullscreenDisplay}]";
    }

    /// <summary>
    /// Formats a hotkey display string from a key name and modifier flags.
    /// </summary>
    private static string FormatHotkeyDisplay(string key, uint modifiers)
    {
        var parts = new List<string>();

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

    /// <summary>
    /// Computes the modifier flags from the current state of modifier keys.
    /// </summary>
    private uint ComputeModifiers()
    {
        uint modifiers = 0;
        if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
            modifiers |= HotkeyManager.MOD_CONTROL;
        if (Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt))
            modifiers |= HotkeyManager.MOD_ALT;
        if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
            modifiers |= HotkeyManager.MOD_SHIFT;
        if (Keyboard.IsKeyDown(Key.LWin) || Keyboard.IsKeyDown(Key.RWin))
            modifiers |= HotkeyManager.MOD_WIN;
        return modifiers;
    }

    /// <summary>
    /// WndProc hook to receive and process WM_HOTKEY messages.
    /// </summary>
    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == HotkeyManager.WM_HOTKEY)
        {
            int hotkeyId = wParam.ToInt32();

            switch (hotkeyId)
            {
                case HotkeyIdRegionOcr:
                    _ = Task.Run(() => ExecuteOcrRegionAsync());
                    break;
                case HotkeyIdFullscreenOcr:
                    _ = Task.Run(() => ExecuteOcrFullscreenAsync());
                    break;
            }

            handled = true;
        }

        return IntPtr.Zero;
    }

    /// <summary>
    /// Executes the region OCR operation asynchronously.
    /// Shows selection overlay, captures the selected region, and processes it with OCR.
    /// </summary>
    private async Task ExecuteOcrRegionAsync()
    {
        System.Drawing.Bitmap capturedBitmap = null;
        try
        {
            // Hide the main window temporarily
            Dispatcher.Invoke(() =>
            {
                this.Hide();
                _isMinimizedToTray = false;
            });
            System.Windows.Forms.Application.DoEvents();

            // Create and show the selection overlay
            var selectionWindow = new SelectionWindow();
            System.Drawing.Rectangle? selectedRegion = null;

            // Subscribe to the selection event
            selectionWindow.SelectedRegionCaptured += (sender, rect) =>
            {
                selectedRegion = rect;
            };

            // Show dialog (blocking)
            selectionWindow.ShowDialog();

            // Check if a valid region was selected
            if (selectedRegion.HasValue && selectedRegion.Value.Width > 0 && selectedRegion.Value.Height > 0)
            {
                var region = selectedRegion.Value;

                // Capture the selected region
                capturedBitmap = ScreenCapture.CaptureRegion(region);

                // Process with OCR
                await ProcessOcrAsync(capturedBitmap, $"Capturing region ({region.Width}x{region.Height}px)...");
            }
            else
            {
                // Selection was cancelled (Escape or click outside), restore the window
                Dispatcher.Invoke(() =>
                {
                    RestoreWindow();
                    StatusText.Text = "Selection cancelled";
                });
            }
        }
        catch (Exception ex)
        {
            // Ensure window is restored even on error
            try
            {
                Dispatcher.Invoke(() =>
                {
                    RestoreWindow();
                });
            }
            catch { }
            StatusText.Text = $"Error: {ex.Message}";
        }
        finally
        {
            capturedBitmap?.Dispose();
        }
    }

    /// <summary>
    /// Executes the fullscreen OCR operation asynchronously.
    /// Captures the entire screen and processes it with OCR.
    /// </summary>
    private async Task ExecuteOcrFullscreenAsync()
    {
        System.Drawing.Bitmap capturedBitmap = null;
        try
        {
            // Hide the main window temporarily
            Dispatcher.Invoke(() =>
            {
                this.Hide();
                _isMinimizedToTray = false;
            });
            System.Windows.Forms.Application.DoEvents();

            // Capture the full screen
            capturedBitmap = ScreenCapture.CaptureFullScreen();

            // Process with OCR
            await ProcessOcrAsync(capturedBitmap, "Capturing full screen...");
        }
        catch (Exception ex)
        {
            // Ensure window is restored even on error
            try
            {
                Dispatcher.Invoke(() =>
                {
                    RestoreWindow();
                });
            }
            catch { }
            StatusText.Text = $"Error: {ex.Message}";
        }
        finally
        {
            capturedBitmap?.Dispose();
        }
    }

    /// <summary>
    /// Processes captured bitmap with OCR and updates the UI with results.
    /// </summary>
    private async Task ProcessOcrAsync(System.Drawing.Bitmap bitmap, string statusStarting)
    {
        try
        {
            StatusText.Text = "Processing OCR...";
            System.Windows.Forms.Application.DoEvents();

            var stopwatch = Stopwatch.StartNew();

            var text = await _ocrService.ExtractTextAsync(bitmap);

            stopwatch.Stop();
            long elapsedMs = stopwatch.ElapsedMilliseconds;

            Dispatcher.Invoke(() =>
            {
                OutputTextBox.Text = text;
            });

            // Auto-copy if enabled
            var textCopied = CopyToClipboard(text);

            // Update status with results
            Dispatcher.Invoke(() =>
            {
                StatusText.Text = $"Extracted {text.Length} characters in {elapsedMs} ms";
            });

            // Show toast notification if auto-copy was enabled and successful
            if (textCopied)
            {
                var preview = text.Length > 50 ? text.Substring(0, 50) + "..." : text;
                Dispatcher.Invoke(() =>
                {
                    ShowToastNotification("OCR Text Copied", preview);
                });
            }
        }
        catch (Exception ex)
        {
            Dispatcher.Invoke(() =>
            {
                StatusText.Text = $"OCR Error: {ex.Message}";
            });
            WpfMessageBox.Show($"OCR processing failed:\n\n{ex.Message}\n\nThis may be due to missing OCR language packs or image decoding issues.",
                "OCR Error", MessageBoxButton.OK, WpfMessageBoxImage.Error);
        }
        finally
        {
            // Restore window
            Dispatcher.Invoke(() =>
            {
                RestoreWindow();
            });
        }
    }

    /// <summary>
    /// Copies text to the clipboard if auto-copy is enabled and text is not empty.
    /// Returns true if text was copied successfully.
    /// </summary>
    private bool CopyToClipboard(string text)
    {
        if (AutoCopyCheckbox.IsChecked == true && !string.IsNullOrEmpty(text))
        {
            try
            {
                System.Windows.Clipboard.SetText(text);
                return true;
            }
            catch (Exception)
            {
                // Silently fail - clipboard may be in use
                return false;
            }
        }
        return false;
    }

    /// <summary>
    /// Handler for "OCR Specific Part" button click
    /// </summary>
    private async void BtnOcrRegion_Click(object? sender, RoutedEventArgs e)
    {
        await ExecuteOcrRegionAsync();
    }

    /// <summary>
    /// Handler for "OCR Entire Screen" button click
    /// </summary>
    private async void BtnOcrFullscreen_Click(object? sender, RoutedEventArgs e)
    {
        await ExecuteOcrFullscreenAsync();
    }

    /// <summary>
    /// Handler for "Change Region Hotkey" button click
    /// </summary>
    private void BtnChangeRegionHotkey_Click(object? sender, RoutedEventArgs e)
    {
        _isRebindingRegion = true;
        _isRebindingFullscreen = false;
        _pendingModifiers = 0;
        BtnChangeRegionHotkey.Content = "Press any key combination...";
        BtnChangeFullscreenHotkey.IsEnabled = false;
    }

    /// <summary>
    /// Handler for "Change Fullscreen Hotkey" button click
    /// </summary>
    private void BtnChangeFullscreenHotkey_Click(object? sender, RoutedEventArgs e)
    {
        _isRebindingRegion = false;
        _isRebindingFullscreen = true;
        _pendingModifiers = 0;
        BtnChangeFullscreenHotkey.Content = "Press any key combination...";
        BtnChangeRegionHotkey.IsEnabled = false;
    }

    /// <summary>
    /// Handle keyboard events for global hotkeys and hotkey rebinding.
    /// </summary>
    protected override void OnPreviewKeyDown(System.Windows.Input.KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);

        // Handle hotkey rebinding mode
        if (_isRebindingRegion || _isRebindingFullscreen)
        {
            // Track modifier key presses during rebinding
            if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl ||
                e.Key == Key.LeftAlt || e.Key == Key.RightAlt ||
                e.Key == Key.LeftShift || e.Key == Key.RightShift ||
                e.Key == Key.LWin || e.Key == Key.RWin)
            {
                // Update pending modifiers without assigning yet
                _pendingModifiers = ComputeModifiers();

                // Show current combination preview
                string preview = _pendingModifiers > 0
                    ? FormatHotkeyDisplay("...", _pendingModifiers)
                    : "Press main key...";
                if (_isRebindingRegion)
                {
                    BtnChangeRegionHotkey.Content = $"Region Hotkey: {preview}";
                }
                else
                {
                    BtnChangeFullscreenHotkey.Content = $"Fullscreen Hotkey: {preview}";
                }
                return;
            }

            // Ignore standalone non-modifier keys that shouldn't be hotkeys
            if (IsIgnoredKey(e.Key))
            {
                return;
            }

            // Complete the hotkey binding with modifiers
            string newKey = e.Key.ToString();
            uint newModifiers = _pendingModifiers;

            if (_isRebindingRegion)
            {
                _regionHotkey = newKey;
                _regionHotkeyModifiers = newModifiers;

                // Re-register the hotkey with modifiers
                if (_hWnd != IntPtr.Zero)
                {
                    var newKeyEnum = ParseKey(_regionHotkey);
                    HotkeyManager.Unregister(_hWnd, HotkeyIdRegionOcr);
                    HotkeyManager.Register(_hWnd, HotkeyIdRegionOcr, newKeyEnum, _regionHotkeyModifiers);
                }

                // Update UI
                UpdateHotkeyButtonLabels();
                UpdateOcrButtonHotkeyLabels();
                _isRebindingRegion = false;
            }
            else if (_isRebindingFullscreen)
            {
                _fullscreenHotkey = newKey;
                _fullscreenHotkeyModifiers = newModifiers;

                // Re-register the hotkey with modifiers
                if (_hWnd != IntPtr.Zero)
                {
                    var newKeyEnum = ParseKey(_fullscreenHotkey);
                    HotkeyManager.Unregister(_hWnd, HotkeyIdFullscreenOcr);
                    HotkeyManager.Register(_hWnd, HotkeyIdFullscreenOcr, newKeyEnum, _fullscreenHotkeyModifiers);
                }

                // Update UI
                UpdateHotkeyButtonLabels();
                UpdateOcrButtonHotkeyLabels();
                _isRebindingFullscreen = false;
            }

            // Reset pending modifiers
            _pendingModifiers = 0;

            // Re-enable the other button and update content
            BtnChangeRegionHotkey.IsEnabled = true;
            BtnChangeFullscreenHotkey.IsEnabled = true;
            UpdateHotkeyButtonLabels();

            // Reset focus to the main window
            this.Focus();

            // Save settings immediately after hotkey change
            SaveSettings();
        }
    }

    /// <summary>
    /// Handle AutoCopy checkbox changes - save settings immediately.
    /// </summary>
    private void AutoCopyCheckbox_Checked(object? sender, RoutedEventArgs e)
    {
        SaveSettings();
    }

    private void AutoCopyCheckbox_Unchecked(object? sender, RoutedEventArgs e)
    {
        SaveSettings();
    }

    /// <summary>
    /// Checks if a key should be ignored during hotkey rebinding.
    /// </summary>
    private static bool IsIgnoredKey(Key key)
    {
        return key switch
        {
            Key.LeftCtrl or Key.RightCtrl or
            Key.LeftShift or Key.RightShift or
            Key.LeftAlt or Key.RightAlt or
            Key.LWin or Key.RWin or
            Key.Tab or Key.Escape or Key.CapsLock or Key.NumLock or Key.Scroll => true,
            _ => false
        };
    }

    /// <summary>
    /// Handler for "Copy to Clipboard" button click
    /// </summary>
    private void BtnCopy_Click(object? sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(OutputTextBox.Text))
        {
            try
            {
                System.Windows.Clipboard.SetText(OutputTextBox.Text);
                StatusText.Text = "Copied to clipboard!";
                Dispatcher.Invoke(() =>
                {
                    StatusText.Text = "Ready";
                }, System.Windows.Threading.DispatcherPriority.Background);
            }
            catch (Exception ex)
            {
                WpfMessageBox.Show($"Failed to copy to clipboard: {ex.Message}",
                    "Error", MessageBoxButton.OK, WpfMessageBoxImage.Error);
            }
        }
        else
        {
            WpfMessageBox.Show("No text to copy!", "Warning",
                MessageBoxButton.OK, WpfMessageBoxImage.Warning);
        }
    }

    /// <summary>
    /// Overload of ExecuteOcrRegion that accepts captured bitmap path for processing.
    /// </summary>
    private void ExecuteOcrRegionWithBitmap(BitmapImage bitmap)
    {
        // TODO: Implement OCR on the provided bitmap
        // For now, just update the status
        StatusText.Text = "Ready";
    }

    /// <summary>
    /// Overload of ExecuteOcrFullscreen that accepts captured bitmap path for processing.
    /// </summary>
    private void ExecuteOcrFullscreenWithBitmap(BitmapImage bitmap)
    {
        // TODO: Implement OCR on the provided bitmap
        // For now, just update the status
        StatusText.Text = "Ready";
    }
}
