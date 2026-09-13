# 📝 CyberPunkNoteWidget

[![.NET](https://img.shields.io/badge/.NET-8.0--windows-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![WPF](https://img.shields.io/badge/UI-WPF-0078D4?logo=windows&logoColor=white)](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/)
[![Platform](https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011-00ADEF?logo=windows11&logoColor=white)](https://www.microsoft.com/windows)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

A modern, lightweight, frameless desktop note-taking and code-scratchpad widget for Windows with retro-futuristic cyberpunk CRT visual effects, dual independent transparency controls, and dynamic background image scaling.

Built with the exact design DNA and visual styling of **`CyberpunkTerminalWidget`** and **`CyberpunkSlideshowWidget`**.

---

## ✨ Key Features

- **Borderless & Draggable HUD:** A chrome-free, transparent glass panel that floats seamlessly over your desktop wallpaper, IDEs, or games. Click and drag the header or background to reposition, or grab the bottom-right grip to resize.
- **Multi-Format Text Engine:**
  - Read, edit, and save `.txt`, `.md` (Markdown), `.css` (Stylesheets), `.xaml` / `.xml`, `.html` / `.htm`, `.json` / `.yaml`, and script files (`.ps1`, `.bat`, `.cmd`, `.js`, `.ts`, `.py`, `.cs`).
  - Dynamic file type badge (e.g. `[ .MD ]`, `[ .TXT ]`, `[ .CSS ]`, `[ .XAML ]`) with document title and unsaved changes indicator (`*`).
- **Drag & Drop Loading:** Drag any supported note or code file directly from Windows Explorer onto the widget window to open it immediately.
- **Dual Transparency Sliders:**
  - **Window Opacity (0% – 100%):** Dims or completely removes the window backdrop, glass borders, and background image. Setting this to 0% produces a completely invisible window container.
  - **Font Opacity (10% – 100%):** Controls the note text opacity independently.
  - **Floating HUD Notes Mode:** Set Window Opacity to 0% and Font Opacity to 100% to have crisp, glowing cyberpunk notes floating directly over your wallpaper or active workspace.
- **Dynamic Background Image Scaling:**
  - Select any `.png`, `.jpg`, `.jpeg`, `.bmp`, or `.webp` image as your note backdrop.
  - Smoothly scales (`UniformToFill`) with 8px rounded corner clipping as you resize the window.
  - Background image transparency is bound directly to the Window Opacity slider.
- **Font & Color Customization:**
  - **System Font Picker:** WordPad-style searchable font selector dialog (`FontPickerWindow`) with live sample preview across all installed Windows fonts.
  - **Monospace Quick Presets:** Fast selection of top monospace coding fonts (`Cascadia Code`, `Consolas`, `Lucida Console`, `Courier New`, `Fira Code`, `JetBrains Mono`).
  - **Font Size:** 10pt, 11pt, 12pt, 13pt (default), 14pt, 16pt, 18pt, 20pt, 24pt.
  - **Vibrant Cyberpunk Palette:** Quick-switch between *Matrix Green* (`#00FF66`), *Neon Cyan* (`#00F0FF`), *Synthwave Magenta* (`#FF007F`), *Solar Amber* (`#FFB000`), *Phosphor White* (`#F0F0F0`), *Glitch Red* (`#FF2244`), and *Night City Violet* (`#A040FF`), or enter a custom hex color code.
- **Retro Cyberpunk Visual FX Suite:**
  - **Cyberpunk Rainbow Border:** Rotating animated multi-stop RGB gradient with cyan outer glow halo and width slider (1px – 20px).
  - **Vintage CRT Scanlines:** Hardware-accelerated horizontal scanlines with live thickness tuning (1px – 20px).
  - **CRT Glitch Effect:** Retro sync jitter, horizontal scan drops, and chromatic aberration slices with random percentage chance slider (0% – 100%).
  - **CRT Snow Static:** Analog TV phosphor static noise with adjustable strength slider (1% – 100%).
- **Ultra-Minimalist Scrollbar:**
  - 2px/3px ultra-narrow minimalist scrollbar track and thumb sharing widget transparency and color tinting.
- **Status Footer:**
  - Live line and column counter (`Ln 1, Col 1`).
  - Real-time word and character counts (`42 words • 280 chars`).
  - Encoding and Word Wrap status indicator (`UTF-8 • Wrap: ON`).
- **Session Persistence & Auto-Save:**
  - Automatically restores window position, size, opacities, fonts, colors, effects, and your last active file or scratchpad note across restarts.
  - Settings stored cleanly in `%APPDATA%\CyberPunkNoteWidget\settings.json`.
- **Zero-Footprint Teardown:**
  - Clean process exit releases all DirectX, WIC, and background timers, leaving 0 lingering processes in Windows Task Manager.

---

## 🎛️ Context Menu & Controls

Right-click anywhere on the note widget to access the control HUD:

| Menu Item | Description |
| :--- | :--- |
| **New Note (Ctrl+N)** | Clears buffer for a new note (prompts if unsaved changes). |
| **Open File... (Ctrl+O)** | Opens standard file dialog to load any note or code file. |
| **Save (Ctrl+S)** | Saves changes directly to file, or prompts Save As if untitled. |
| **Save As... (Ctrl+Shift+S)** | Opens file dialog to save note under a new filename/path. |
| **Clear Note** | Clears note text. |
| **Word Wrap (Alt+Z)** | Toggles word wrapping on or off. |
| **Font Family** | Opens System Font Picker or selects top monospace favorites. |
| **Font Size** | Select note font size from 10pt to 24pt. |
| **Font Color** | Choose cyberpunk color presets or enter a custom hex color. |
| **Window Opacity (0% - 100%)** | Adjust transparency of glass background panel and image. |
| **Font Opacity (10% - 100%)** | Adjust transparency of note text independently. |
| **Select Background Image...** | Set a custom image as widget backdrop. |
| **Clear Background Image** | Reverts to frosted dark acrylic glass backdrop. |
| **Effects > Rainbow Border** | Toggle rotating neon gradient border with 1px–20px width slider. |
| **Effects > Vintage CRT Scanlines** | Toggle vintage CRT scanlines with 1px–20px thickness slider. |
| **Effects > CRT Glitch** | Toggle retro CRT sync glitching with 0%–100% chance slider. |
| **Effects > CRT Snow** | Toggle continuous analog TV static noise with 1%–100% amount slider. |
| **Always on Top** | Pin note widget above all other windows. |
| **Window Shadow** | Toggle soft 3D floating desktop drop shadow. |
| **New Note Window** | Spawns an additional independent note widget. |
| **Close Widget** | Closes active note widget (prompts if unsaved changes). |
| **Exit All** | Cleanly terminates all open note widgets and shuts down. |

---

## ⌨️ Keyboard Shortcuts & Quick Actions

- <kbd>Ctrl</kbd> + <kbd>N</kbd>: New Note.
- <kbd>Ctrl</kbd> + <kbd>O</kbd>: Open Note / Code file.
- <kbd>Ctrl</kbd> + <kbd>S</kbd>: Save Note.
- <kbd>Ctrl</kbd> + <kbd>Shift</kbd> + <kbd>S</kbd>: Save As...
- <kbd>Alt</kbd> + <kbd>Z</kbd>: Toggle Word Wrap.
- <kbd>Tab</kbd>: Insert indentation.
- <kbd>Ctrl</kbd> + <kbd>A</kbd>: Select all text.
- <kbd>Ctrl</kbd> + <kbd>Z</kbd> / <kbd>Ctrl</kbd> + <kbd>Y</kbd>: Undo / Redo.
- **Top Quick Action Buttons:**
  - `NEW`: New note
  - `OPN`: Open file
  - `SAV`: Save file
  - `CLR`: Clear note
  - `✕`: Close widget
- **Drag & Drop:** Drop any text, markdown, or code file directly onto the window to open.
- **Left-Click & Drag:** Reposition widget on screen.
- **Corner Resize:** Drag bottom-right grip to resize window.
- **Mouse Wheel on Sliders:** Scroll mouse wheel over any slider in the menu for fine adjustments.

---

## 🛠️ Build & Run

### Prerequisites
- Windows 10 or Windows 11
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or higher

### Compiling & Running
```powershell
cd "D:\Other Coding Projects\CyberPunkNoteWidget"

# Build debug binary
dotnet build

# Run application
dotnet run
```

