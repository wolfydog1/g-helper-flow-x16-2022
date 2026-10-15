# ⚡ G-Helper (Flow X16 & Convertible Edition)

A lightweight Armoury Crate alternative for ASUS ROG & TUF laptops, extended with specialized hardware controls for 2-in-1 convertibles (**ROG Flow X16, Flow X13, Flow Z13**) and enhanced mobile productivity.

---

## 🌟 What's New in this Edition

### 1. 🖊️ Pen / Stylus Mode (Hardware Palm Rejection)
- **3-State Touchscreen Toggle**:
  - **`Touch: On`**: Standard finger touch + stylus pen both enabled.
  - **`Pen Only`**: Disables the capacitive finger touchscreen (`HID-compliant touch screen`) while keeping the MPP 2.0 stylus / ASUS Pen (`HID-compliant pen`) active. Perfect for sketching, drawing, or note-taking with zero accidental hand/palm inputs!
  - **`Touch: Off`**: Completely disables touch and stylus input to save power or prevent all accidental screen touches.
- **Quick Right-Click**: Right-clicking the Touchscreen button instantly jumps directly into/out of **`Pen Only`** mode.

### 2. 🔄 Rotation Lock & Smart Tablet / Tent Detection
- **`Rotate: Lock` / `Rotate: On` Button**: Direct one-click toggle in the screen switches row to lock or unlock screen auto-rotation without opening the Windows Action Center.
- **Auto Tablet / Tent Mode Rotation**: When folding the Flow X16 360° into Tablet or Tent mode, G-Helper automatically unlocks screen auto-rotation. When flipped back into standard Clamshell laptop mode, rotation is safely re-locked. (Right-click the button to toggle this auto-behavior on or off).

### 3. ⚡ Live Battery Wattage & Remaining Time in Header
- Displays the live battery discharge or charge rate in Watts and estimated time remaining directly in the **Battery Charge Limit** header at all times:
  - Discharging: `78% | Discharging: 14.2W (~4h 15m left)`
  - Charging: `78% | Charging: +28.4W (~45m)`
  - AC Bypass: `80% | AC Bypass (0.0W)`
- Also shown continuously in the system tray icon tooltip.

### 4. 🛡️ Smart Charge Scheduler (Hysteresis Threshold)
- **`Smart <60%` Button**: Prevents battery micro-cycling wear when plugging and unplugging throughout the day.
  - If your battery is already above the threshold (e.g. 75%), plugging in keeps the laptop on pure **AC bypass** without topping up.
  - Charging only resumes when the battery level drops below the lower threshold.
- **Configurable Threshold**: Right-click cycles between `<50%`, `<60%`, and `<70%`.

### 5. 💡 Ambient Light Sensor (ALS) Auto Brightness & Custom Curves
- **Auto Screen & Auto Keyboard Brightness**: Automatically reads the laptop's built-in ambient light sensor and adjusts screen brightness and keyboard backlight.
- **Customizable Lux Curves**: Fine-tune your preferred brightness levels for pitch black, indoor room lighting, and direct sunlight.

### 6. 🎙️ Quick Mic Mute / Audio Switcher & Power Saver
- **`Mic: On` / `Mic: Muted`**: Left-click toggles microphone mute and syncs the ASUS hardware mic mute LED. Right-click quickly cycles active audio output devices (e.g. laptop speakers ↔ headphones).
- **`Power Saver`**: One-click button to toggle Windows Best Power Efficiency and Energy Saver modes.
- **No Boost on Battery**: Automatically turns off CPU Turbo Boost when running on battery power to drastically reduce temperatures and extend battery life.

---

## 🚀 Installation & Usage

1. **Prerequisites**: Windows 11 / Windows 10 with [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) (or [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)).
2. **Download**: Grab `GHelper-FlowX16-Edition.zip` from the [Releases](releases) tab.
3. **Run**: Extract the ZIP file and run `GHelper.exe`. No installer required!
4. **Autostart**: Check the "Run on startup" box in G-Helper.

---

## 🛠️ Building from Source

Requires .NET SDK 10.0:
```powershell
# Clone the repository
git clone https://github.com/<YOUR_USERNAME>/g-helper-flow-x16.git
cd g-helper-flow-x16/app

# Build standalone Release executable
dotnet publish GHelper.csproj -c Release -r win-x64 -p:PublishSingleFile=true --no-self-contained
```
The compiled executable will be located in `app/bin/x64/Release/net10.0-windows10.0.19041.0/win-x64/publish/`.

---

## 📄 License
Based on the open-source G-Helper project by [seerge](https://github.com/seerge/g-helper), licensed under GNU General Public License v3.0 (GPL-3.0).
