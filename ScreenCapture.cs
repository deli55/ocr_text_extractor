using WinFormsScreen = System.Windows.Forms.Screen;

namespace OcrTextExtractor;

/// <summary>
/// Utility class for capturing screen regions using GDI+.
/// </summary>
public static class ScreenCapture
{
    /// <summary>
    /// Captures a specific region of the screen.
    /// </summary>
    /// <param name="bounds">The screen rectangle to capture (in screen coordinates).</param>
    /// <returns>A Bitmap containing the captured screen region.</returns>
    public static System.Drawing.Bitmap CaptureRegion(System.Drawing.Rectangle bounds)
    {
        // Validate bounds
        if (bounds.Width <= 0 || bounds.Height <= 0)
            throw new ArgumentException("Capture bounds must have positive width and height.", nameof(bounds));

        // Create a bitmap of the specified size
        using var tempBitmap = new System.Drawing.Bitmap(bounds.Width, bounds.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

        // Use a Graphics object from the bitmap to copy screen pixels
        using (System.Drawing.Graphics graphics = System.Drawing.Graphics.FromImage(tempBitmap))
        {
            // Get the device context of the screen and copy pixels
            graphics.CopyFromScreen(bounds.X, bounds.Y, 0, 0, bounds.Size, System.Drawing.CopyPixelOperation.SourceCopy);
        }

        // Return a new bitmap (since the original will be disposed)
        return new System.Drawing.Bitmap(tempBitmap);
    }

    /// <summary>
    /// Captures the entire virtual screen (all monitors).
    /// </summary>
    /// <returns>A Bitmap containing the full virtual screen capture.</returns>
    public static System.Drawing.Bitmap CaptureFullScreen()
    {
        var primaryScreen = WinFormsScreen.PrimaryScreen;
        System.Drawing.Rectangle bounds;
        
        if (primaryScreen != null)
        {
            bounds = primaryScreen.Bounds;
        }
        else
        {
            // Fallback to default screen
            bounds = new System.Drawing.Rectangle(0, 0, 1920, 1080);
        }

        return CaptureRegion(bounds);
    }
}
