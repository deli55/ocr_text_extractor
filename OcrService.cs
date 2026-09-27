using Windows.Media.Ocr;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace OcrTextExtractor;

/// <summary>
/// Wrapper around the Windows.Media.Ocr.OcrEngine for text extraction from images.
/// </summary>
public class OcrService : IDisposable
{
    private readonly OcrEngine _ocrEngine;
    private bool _disposed = false;

    /// <summary>
    /// Creates a new instance of the OcrService.
    /// Initializes the underlying OcrEngine with the first available language pack.
    /// </summary>
    public OcrService()
    {
        // Try to create an OCR engine using user-configured languages first
        _ocrEngine = OcrEngine.TryCreateFromUserProfileLanguages();

        // Fall back to the first available language
        if (_ocrEngine == null && OcrEngine.AvailableRecognizerLanguages.Count > 0)
        {
            _ocrEngine = OcrEngine.TryCreateFromLanguage(OcrEngine.AvailableRecognizerLanguages[0]);
        }

        // If no language pack is installed, throw a descriptive exception
        if (_ocrEngine == null)
        {
            throw new InvalidOperationException(
                "No OCR language pack is installed on this system. " +
                "Please install a language pack via Windows Settings > Time & Language > Speech > Add a language, " +
                "or install the OneNote Offline Package from the Microsoft Store.");
        }
    }

    /// <summary>
    /// Extracts text from a System.Drawing.Bitmap using the Windows Media OCR engine.
    /// </summary>
    /// <param name="bitmap">The bitmap to extract text from.</param>
    /// <returns>The extracted text, or an empty string if no text could be recognized.</returns>
    public async Task<string> ExtractTextAsync(System.Drawing.Bitmap bitmap)
    {
        if (bitmap == null)
            throw new ArgumentNullException(nameof(bitmap));

        // Save bitmap to memory stream as PNG
        byte[] pngBytes;
        using var memoryStream = new System.IO.MemoryStream();
        {
            // Save the bitmap in PNG format
            bitmap.Save(memoryStream, GetImageFormat(bitmap.RawFormat));

            // Copy to byte array
            pngBytes = memoryStream.ToArray();
        }

        // Write bytes to InMemoryRandomAccessStream and decode to SoftwareBitmap
        SoftwareBitmap softwareBitmap;
        using var randomAccessStream = new InMemoryRandomAccessStream();
        using (var dataWriter = new DataWriter(randomAccessStream))
        {
            dataWriter.WriteBytes(pngBytes);
            await dataWriter.StoreAsync();
            await dataWriter.FlushAsync();

            // Reset position for reading
            randomAccessStream.Seek(0);

            // Create a BitmapDecoder from the stream
            var decoder = await BitmapDecoder.CreateAsync(randomAccessStream);

            // Get the SoftwareBitmap from the decoder
            softwareBitmap = await decoder.GetSoftwareBitmapAsync();
        }

        // Convert format if needed to BGRA8 with premultiplied alpha
        SoftwareBitmap? convertedBitmap = null;
        if (softwareBitmap.BitmapPixelFormat != BitmapPixelFormat.Bgra8 ||
            softwareBitmap.BitmapAlphaMode != BitmapAlphaMode.Premultiplied)
        {
            convertedBitmap = SoftwareBitmap.Convert(
                softwareBitmap,
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Premultiplied);
        }

        try
        {
            // Perform OCR recognition
            OcrResult result = await _ocrEngine.RecognizeAsync(convertedBitmap ?? softwareBitmap);

            if (result == null || result.Lines.Count == 0)
                return string.Empty;

            // Build the extracted text from recognized lines
            var sb = new System.Text.StringBuilder();
            foreach (var line in result.Lines)
            {
                if (sb.Length > 0)
                    sb.AppendLine();
                sb.Append(line.Text);
            }

            // Return with normalized line endings
            return sb.ToString();
        }
        finally
        {
            // Dispose the converted bitmap if one was created
            convertedBitmap?.Dispose();
        }
    }

    /// <summary>
    /// Gets the ImageFormat for a given image format.
    /// </summary>
    private static System.Drawing.Imaging.ImageFormat GetImageFormat(System.Drawing.Imaging.ImageFormat format)
    {
        // Use PNG for best quality/size ratio for OCR
        return System.Drawing.Imaging.ImageFormat.Png;
    }

    /// <summary>
    /// Disposes the OcrService and its underlying OCR engine.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            // OcrEngine does not implement IDisposable in WinRT interop
            _disposed = true;
        }
    }
}
