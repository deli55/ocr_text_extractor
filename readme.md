
---

# OcrTextExtractor

A high-performance, lightweight, system-wide Windows desktop utility built in **.NET 8 (WPF)** for instant screen-to-text extraction. Utilizing native **Windows Runtime OCR (`Windows.Media.Ocr`)**, low-level **Win32 global key hooks**, and a **hardware-accelerated multi-monitor selection overlay**, `OcrTextExtractor` allows users to capture any on-screen text via customizable global hotkeys and copy it directly to the clipboard in under 100 milliseconds.

Designed to run silently as a background system tray utility with minimal memory overhead (~35–50 MB RAM), zero external runtime dependencies, and a single-file executable footprint.

---

## Key Features

* **Sub-100ms Local OCR Pipeline:** Powered by Windows 10/11 native `Windows.Media.Ocr` engine—no external Tesseract binaries, language pack downloads, or cloud API keys required.
* **System-Wide Global Hotkeys:** Powered by Win32 `RegisterHotKey` hooks with support for custom key combinations and modifiers (`Ctrl`, `Alt`, `Shift`, `Win`). Rebind hotkeys dynamically on the fly (Defaults: `F1` for Region, `F2` for Fullscreen).
* **Interactive Multi-Monitor Screen Overlay:** Semi-transparent canvas supporting multi-display setups with DPI-aware rubber-band selection rectangle and instant `Escape` cancellation.
* **System Tray & Single-Instance Engine:** Minimizes cleanly to the Windows notification area with context controls, toast notification feedback, and named `Mutex` single-instance enforcement.
* **Automatic Clipboard Sync:** Extracted text is instantly normalized (`\r\n`) and copied to the Windows clipboard with visual tray toast confirmations.
* **Persistent Settings:** Auto-saves hotkey configurations, modifier masks, and auto-copy preferences to a lightweight local `settings.json`.
* **Zero-Installer Portability:** Builds to a fully self-contained, single-file `.exe` targeting `win-x64`.

---

## Technical Architecture

```text
 ┌──────────────────────┐      ┌─────────────────────────┐      ┌──────────────────────┐
 │ Win32 Global Hook    │ ───> │  Interactive Overlay    │ ───> │ GDI+ Screen Capture  │
 │ (user32.dll WndProc) │      │  (Multi-Monitor Canvas) │      │ (System.Drawing)     │
 └──────────────────────┘      └─────────────────────────┘      └──────────────────────┘
                                                                           │
 ┌──────────────────────┐      ┌─────────────────────────┐                 ▼
 │ System Clipboard &   │ <─── │ Async WinRT OCR Engine  │ <─── ┌──────────────────────┐
 │ Tray Toast Feedback  │      │ (Windows.Media.Ocr)     │      │ MemoryStream / WinRT │
 └──────────────────────┘      └─────────────────────────┘      │ SoftwareBitmap       │
                                                                └──────────────────────┘

```

### Core Architecture Components

| Component | Technology / API | Responsibility |
| --- | --- | --- |
| **UI Framework** | WPF (.NET 8.0) | Compact main window, tray integration, and custom control styling. |
| **OCR Engine** | `Windows.Media.Ocr` (WinRT) | Hardware-accelerated local text recognition and line normalization. |
| **Screen Capture** | `System.Drawing.Common` (GDI+) | Virtual screen bound detection and pixel copying via `CopyFromScreen`. |
| **System Interop** | Win32 P/Invoke (`user32.dll`) | Non-blocking global hotkey intercept via `WM_HOTKEY` message loops. |
| **Process Lifecycle** | `System.Threading.Mutex` | Single-instance process lock (`OcrTextExtractor_SingleInstance_Mutex`). |
| **Persistence** | `System.Text.Json` | Asynchronous serialization/deserialization of user `settings.json`. |

---

## Prerequisites

* **Operating System:** Windows 10 (Build 19041 or higher) / Windows 11 (x64)
* **Build Tools (Development only):** .NET 8.0 SDK or higher
* **Runtime:** None (When built using the self-contained publish flag).

---

## Getting Started

### Building from Source

1. **Clone the repository:**
```powershell
git clone https://github.com/your-username/OcrTextExtractor.git
cd OcrTextExtractor

```


2. **Build the project:**
```powershell
dotnet build OcrTextExtractor.csproj -c Release

```


3. **Run the application:**
```powershell
dotnet run --project OcrTextExtractor.csproj

```



---

## Standalone Executable Packaging

To build a zero-dependency, single-file `.exe` that runs on any modern Windows machine without requiring pre-installed .NET runtimes:

```powershell
dotnet publish OcrTextExtractor.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true

```

The output standalone executable will be generated at:

```text
bin/Release/net8.0-windows10.0.19041.0/win-x64/publish/OcrTextExtractor.exe

```

---

## Usage Guide

1. **Start the Utility:** Launch `OcrTextExtractor.exe`. The application opens a compact control panel and initializes its icon in the system tray.
2. **Select Region OCR:**
* Press your Region Hotkey (Default: `F1` or custom e.g., `Ctrl + F1`).
* The screen will dim. Click and drag a bounding box over any text on screen.
* Release the mouse button to execute OCR (or press `Escape` to cancel).


3. **Fullscreen OCR:** Press your Fullscreen Hotkey (Default: `F2`) to extract all text across your primary display instantly.
4. **Hotkey Customization:**
* Click **Change Region Hotkey** or **Change Fullscreen Hotkey** in the main UI.
* Press any key combination (e.g., `Ctrl + Shift + F1`). The binding updates and saves automatically.


5. **System Tray Operation:**
* Closing or minimizing the main window hides it directly to the notification tray.
* Double-click the tray icon to restore the main window, or right-click to access quick action menus.



---

## Configuration (`settings.json`)

Settings persist automatically upon modification and are loaded on application startup. The `settings.json` file resides in the application root directory:

```json
{
  "RegionHotkeyKey": "F1",
  "RegionHotkeyModifiers": 0,
  "FullscreenHotkeyKey": "F2",
  "FullscreenHotkeyModifiers": 0,
  "AutoCopy": true
}

```

* **Modifiers Bitmask Flags:** `0` (None), `1` (Alt), `2` (Control), `4` (Shift), `8` (Windows).

---

## Performance Benchmarks

| Operation | Latency / Memory Footprint |
| --- | --- |
| **Startup Overhead** | ~35 MB RAM |
| **Screen Overlay Activation** | < 15 ms |
| **Full 1080p Screen Capture** | ~8–12 ms |
| **WinRT OCR Recognition Latency** | ~40–80 ms |
| **Total Pipeline (Trigger to Clipboard)** | **< 100 ms** |

---

## License

Distributed under the MIT License. See `LICENSE` for more information.