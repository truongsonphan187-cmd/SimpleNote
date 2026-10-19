# SimpleNote

A lightweight, standalone Windows 11 desktop background application and quick edge scratchpad built with **C# (.NET 8.0, WPF)**.

Inspired by the Samsung Edge Panel handle and quick desktop scratchpads, **SimpleNote** rests unobtrusively at the right edge of your screen and expands with a glassmorphic dark UI whenever you need to capture a thought.

---

## 🌟 Key Features

### 1. Edge Panel Scratchpad
- **Resting Edge Handle:** A sleek 14px × 120px dark translucent acrylic pill (`#800D0D11`) anchored at the lower-right edge of your primary display (`SystemParameters.WorkArea`), resting comfortably above the taskbar.
- **Hover Slide-Out:** Moving the mouse over the edge handle smoothly animates the note panel out to the left using `DoubleAnimation` and `CubicEase` easing.
- **Responsive Sizing:** Tuned for 16-inch 16:10 2.5K screens (2560×1600) and DPI-aware with Per-Monitor V2 scaling. Panel width is dynamically sized to ~1/4 screen width (500px–540px) and ~55% screen height.

### 2. Critical Hit-Test Bridge & Auto-Collapse
- **Hit-Test Bridge:** Column 1 between the floating note card and the screen edge is backed by an invisible transparent container (`Background="#00000000"`). Moving your cursor across the gap into the note card never causes a false `MouseLeave`.
- **Instant Auto-Collapse:** Moving the cursor outside the panel + bridge boundaries smoothly slides the panel back into the edge handle and physically resizes the OS window down to 14px wide, never blocking clicks on other windows.
- **📌 Pin Mode:** Toggleable pin button in the header (and tray menu). When active, the scratchpad stays expanded even when the mouse leaves, letting you cross-reference notes while typing in other applications.

### 3. Bullet Note Editor & Smart Backspace
- **Automatic Bullets:** Every note line begins with a bullet point (`• `).
- **Hard Enter:** Pressing `Enter` automatically creates a new line prefixed with `• `.
- **Smart Backspace:** Pressing `Backspace` on a line with only an empty bullet cleanly deletes the bullet/line instead of awkwardly deleting previous characters.
- **Soft Wrap (Word Wrap):** Text wraps within the card without generating redundant bullets.
- **Hidden Scrollbars:** Mouse-wheel vertical scrolling is enabled while keeping scrollbars invisible (`VerticalScrollBarVisibility="Hidden"`).

### 4. Auto-Persistence
- Automatically saves notes to `%AppData%\SimpleNote\notes.json` with debouncing (350ms after typing ceases) and on every collapse/exit event.
- Automatically reloads your notes and pin preferences on startup.

### 5. Native Win32 Integration (Alt+Tab Stripping)
- Uses Win32 P/Invoke (`user32.dll`) to apply `WS_EX_TOOLWINDOW` (0x00000080) and clear `WS_EX_APPWINDOW` on source initialization, completely hiding SimpleNote from Alt+Tab and Task View.

### 6. System Tray (NotifyIcon)
- Procedural custom-drawn vector pencil/note badge in the system tray.
- **Master On/Off Switch:** Left-clicking the tray icon toggles the edge handle completely on or off.
- **Dark Glassmorphic Context Menu:** Right-clicking opens options to:
  - Toggle SimpleNote (Expand / Collapse)
  - Pin / Unpin Scratchpad
  - Clear All Notes
  - Exit SimpleNote

---

## 🚀 Build and Run

### Prerequisites
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) with Windows Desktop workload.

### Commands

To build the project:
```powershell
dotnet build
```

To run the application:
```powershell
dotnet run
```

To run in the background (detached process):
```powershell
Start-Process ".\bin\Debug\net8.0-windows10.0.19041.0\SimpleNote.exe"
```
