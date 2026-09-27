using System.Threading;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxImage = System.Windows.MessageBoxImage;
using StartupEventArgs = System.Windows.StartupEventArgs;

namespace OcrTextExtractor;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App
{
    private static readonly string MutexName = "OcrTextExtractor_SingleInstance_Mutex";
    private static System.Threading.Mutex? _mutex;

    public App()
    {
        Startup += OnStartupInternal;
    }

    private void OnStartupInternal(object sender, StartupEventArgs e)
    {
        _mutex = new System.Threading.Mutex(true, MutexName, out bool createdNew);

        if (!createdNew)
        {
            MessageBox.Show(
                "OCR Text Extractor is already running in the system tray.",
                "OCR Text Extractor",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            Environment.Exit(0);
            return;
        }
    }

    protected override void OnExit(System.Windows.ExitEventArgs e)
    {
        _mutex?.ReleaseMutex();
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
