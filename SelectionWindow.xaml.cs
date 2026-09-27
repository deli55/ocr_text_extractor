using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Controls;
using WpfPoint = System.Windows.Point;
using WpfMouseEventArgs = System.Windows.Input.MouseEventArgs;
using WpfKeyEventArgs = System.Windows.Input.KeyEventArgs;
using WpfMouseButtonState = System.Windows.Input.MouseButtonState;
using WpfKey = System.Windows.Input.Key;

namespace OcrTextExtractor;

/// <summary>
/// Full-screen overlay window for selecting a region via mouse drag.
/// Raises SelectedRegionCaptured event with the selected screen rectangle.
/// </summary>
public partial class SelectionWindow : Window
{
    public event EventHandler<System.Drawing.Rectangle?>? SelectedRegionCaptured;

    private WpfPoint _startPoint;
    private bool _isSelecting = false;
    private bool _cancelled = false;

    // Virtual screen bounds (all monitors)
    private readonly System.Drawing.Rectangle _virtualScreenBounds;

    public SelectionWindow()
    {
        InitializeComponent();

        // Get virtual screen bounds covering all monitors using WinForms Screen
        var primaryScreen = System.Windows.Forms.Screen.PrimaryScreen;
        if (primaryScreen != null)
        {
            _virtualScreenBounds = primaryScreen.Bounds;
        }
        else
        {
            // Fallback to default screen
            _virtualScreenBounds = new System.Drawing.Rectangle(0, 0, 1920, 1080);
        }

        SetupWindow();
        AttachEventHandlers();
    }

    private void SetupWindow()
    {
        // Position across virtual screen bounds (all monitors)
        this.Left = _virtualScreenBounds.Left;
        this.Top = _virtualScreenBounds.Top;
        this.Width = _virtualScreenBounds.Width;
        this.Height = _virtualScreenBounds.Height;
        this.Topmost = true;
        this.WindowState = WindowState.Maximized;
        this.WindowStyle = WindowStyle.None;
        this.ResizeMode = ResizeMode.NoResize;
    }

    /// <summary>
    /// Called when the window is fully loaded and has an HWND.
    /// Ensures the window is focused for keyboard input (Escape key).
    /// </summary>
    private void WindowLoaded(object sender, RoutedEventArgs e)
    {
        this.Focus();
    }

    private void AttachEventHandlers()
    {
        SelectionCanvas.PreviewMouseLeftButtonDown += OnMouseDown;
        SelectionCanvas.PreviewMouseMove += OnMouseMove;
        SelectionCanvas.PreviewMouseLeftButtonUp += OnMouseUp;
        this.PreviewKeyDown += OnKeyDown;
    }

    private void OnMouseDown(object? sender, WpfMouseEventArgs e)
    {
        if (e.LeftButton != WpfMouseButtonState.Pressed)
            return;

        _isSelecting = true;
        _cancelled = false;

        // Capture mouse for continuous tracking across the entire canvas
        SelectionCanvas.CaptureMouse();

        // Record the start point relative to the canvas
        _startPoint = e.GetPosition(SelectionCanvas);

        // Show and reset the selection rectangle
        SelectionRectangle.Visibility = Visibility.Visible;
        Canvas.SetLeft(SelectionRectangle, _startPoint.X);
        Canvas.SetTop(SelectionRectangle, _startPoint.Y);
        SelectionRectangle.Width = 0;
        SelectionRectangle.Height = 0;
    }

    private void OnMouseMove(object? sender, WpfMouseEventArgs e)
    {
        // Only draw while the left button is held and we're selecting
        if (!_isSelecting || !SelectionCanvas.IsMouseCaptured)
            return;

        var currentPoint = e.GetPosition(SelectionCanvas);

        // Calculate rectangle position and size (support all 4 drag directions)
        double x = Math.Min(_startPoint.X, currentPoint.X);
        double y = Math.Min(_startPoint.Y, currentPoint.Y);
        double width = Math.Abs(currentPoint.X - _startPoint.X);
        double height = Math.Abs(currentPoint.Y - _startPoint.Y);

        Canvas.SetLeft(SelectionRectangle, x);
        Canvas.SetTop(SelectionRectangle, y);
        SelectionRectangle.Width = width;
        SelectionRectangle.Height = height;
    }

    private void OnMouseUp(object? sender, WpfMouseEventArgs e)
    {
        if (!_isSelecting)
            return;

        _isSelecting = false;
        SelectionCanvas.ReleaseMouseCapture();

        // If cancelled via Escape, exit without capturing
        if (_cancelled)
        {
            SelectionRectangle.Visibility = Visibility.Hidden;
            SelectedRegionCaptured?.Invoke(this, null);
            CleanClose();
            return;
        }

        // Get the end point relative to the canvas
        var endPoint = e.GetPosition(SelectionCanvas);
        double width = Math.Abs(endPoint.X - _startPoint.X);
        double height = Math.Abs(endPoint.Y - _startPoint.Y);

        // If the selection is too small (less than 5px), treat as cancelled
        if (width < 5 || height < 5)
        {
            SelectionRectangle.Visibility = Visibility.Hidden;
            SelectedRegionCaptured?.Invoke(this, null);
            CleanClose();
            return;
        }

        // Calculate the selection bounds relative to the canvas
        double selectX = Math.Min(_startPoint.X, endPoint.X);
        double selectY = Math.Min(_startPoint.Y, endPoint.Y);

        // Convert the top-left corner from logical (WPF) to screen coordinates
        // using the window's Position, then convert from device-independent to physical pixels
        // via the PresentationSource TransformFromDevice matrix.
        var presentationSource = PresentationSource.FromVisual(this);
        var transformFromDevice = presentationSource?.CompositionTarget.TransformFromDevice ?? Matrix.Identity;

        // PointToScreen returns logical screen coordinates (in pixels at the source monitor's DPI)
        var topLeftScreen = SelectionCanvas.PointToScreen(new WpfPoint(selectX, selectY));

        // Apply the device-to-device transform to get physical pixel coordinates
        double physX = topLeftScreen.X * transformFromDevice.M11;
        double physY = topLeftScreen.Y * transformFromDevice.M22;

        // Calculate physical width/height by transforming the size
        double physW = width * transformFromDevice.M11;
        double physH = height * transformFromDevice.M22;

        // Account for the virtual screen offset (multi-monitor support)
        int screenX = _virtualScreenBounds.Left + (int)Math.Round(physX - _virtualScreenBounds.Left);
        int screenY = _virtualScreenBounds.Top + (int)Math.Round(physY - _virtualScreenBounds.Top);
        int screenW = (int)Math.Round(physW);
        int screenH = (int)Math.Round(physH);

        // Clamp to valid values
        screenW = Math.Max(1, screenW);
        screenH = Math.Max(1, screenH);

        System.Drawing.Rectangle rect = new System.Drawing.Rectangle(screenX, screenY, screenW, screenH);

        SelectionRectangle.Visibility = Visibility.Hidden;
        SelectedRegionCaptured?.Invoke(this, rect);
        CleanClose();
    }

    /// <summary>
    /// Handles keyboard input (Escape to cancel).
    /// </summary>
    private void OnKeyDown(object? sender, WpfKeyEventArgs e)
    {
        if (e.Key == WpfKey.Escape)
        {
            _cancelled = true;
            _isSelecting = false;
            SelectionRectangle.Visibility = Visibility.Hidden;
            SelectionCanvas.ReleaseMouseCapture();
            SelectedRegionCaptured?.Invoke(this, null);
            CleanClose();
        }
    }

    /// <summary>
    /// Performs cleanup and closes the window cleanly.
    /// </summary>
    private void CleanClose()
    {
        // Hide the selection rectangle before closing
        SelectionRectangle.Visibility = Visibility.Hidden;
        // Close the window (Dispatcher processes the Close() asynchronously)
        Dispatcher.BeginInvoke(new Action(() => { Close(); }), System.Windows.Threading.DispatcherPriority.Background);
    }
}
