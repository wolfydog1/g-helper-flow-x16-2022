# ⚡ G-Helper (Flow X16 & Convertible Edition)

A lightweight Armoury Crate alternative for ASUS Flow laptops, extended with specialized hardware controls and enhanced mobile productivity.

---

## 🌟 What's New in this Edition

### 1. 🖊️ Pen / Stylus Mode (Hardware Palm Rejection)
- **3-State Touchscreen Toggle**:
  - **`Touch: On`**: Standard finger touch + stylus pen both enabled.
  - **`Pen Only`**: Disables the capacitive finger touchscreen (`HID-compliant touch screen`) while keeping the MPP 2.0 stylus / ASUS Pen (`HID-compliant pen`) active. Perfect for sketching, drawing, or note-taking with zero accidental hand/palm inputs!
  - **`Touch: Off`**: Completely disables touch and stylus input to save power or prevent all accidental screen touches.
- **Quick Right-Click & Hardware Keybinding**: Right-clicking the Touchscreen button jumps directly into/out of **`Pen Only`** mode, or bind **`Toggle Pen Only Mode`** to any M-key (`M3`, `M4`, `Fn+F4`) in **Extra**.
- **Tablet Mode Input Safety**: If touch was set to `Touch: Off` in laptop mode, folding 360° into Tablet mode automatically re-enables touch so you are never locked out of input, and restores your previous setting when flipped back.

### 2. 🔄 Rotation Lock, Posture Profiles & Smart Tablet / Tent Detection
- **`Rotate: Lock` / `Rotate: On` Button**: Direct one-click toggle in the screen switches row (or bindable to a hardware key) to lock or unlock screen auto-rotation.
- **Auto Tablet / Tent Mode Rotation**: When folding the Flow X16 360° into Tablet or Tent mode, G-Helper automatically unlocks screen auto-rotation and re-locks it in Clamshell laptop mode. (Right-click the `Rotate` button to toggle auto-unlock).
- **Auto-Silent Posture Profile**: Automatically switches to **Silent** performance mode when folded into Tablet mode so the chassis stays cool and quiet in your hands, and restores your previous performance mode when returned to Laptop mode. (Right-click the **Silent** button to toggle).

### 3. ⚡ Live Battery Wattage, Smoothed Time Remaining & Battery Health
- Displays the live battery discharge or charge rate in Watts and EMA-smoothed estimated time remaining directly in the **Battery Charge Limit** header at all times:
  - Discharging: `78% | Discharging: 14.2W (~4h 15m left)`
  - Charging: `78% | Charging: +28.4W (~45m)`
  - AC Bypass: `80% | AC Bypass (0.0W)`
- **Battery Health & Wear Indicator**: Hovering the battery header displays full battery health and capacity wear (e.g. `Battery Health: 94.2% (84.8 / 90.0 Wh)`).

### 4. 🛡️ Smart Charge Scheduler (Hysteresis Threshold)
- **`Smart <60%` Button**: Prevents battery micro-cycling wear when plugging and unplugging throughout the day.
  - If your battery is already above the threshold (e.g. 75%), plugging in keeps the laptop on pure **AC bypass** without topping up.
  - Charging only resumes when the battery level drops below the lower threshold.
- **Configurable Threshold**: Right-click cycles between `<50%`, `<60%`, and `<70%`.

### 5. 💡 Ambient Light Sensor (ALS) Auto Brightness, Custom Curves & Idle Backlight Timer
- **Auto Screen & Auto Keyboard Brightness**: Smoothly adjusts screen brightness and keyboard backlight using EMA-filtered lux readings and deadband hysteresis, while respecting Tablet/Tent mode and idle backlight off states.
- **Interactive Lux Curve Editor**: Live room Lux readout (`Current Room Light: XX Lux`), point coordinate labels, and one-click **Reset Defaults**.
- **Keyboard Backlight Idle Auto-Off Timer**: Right-click the `Backlight:` label to quickly cycle the keyboard idle auto-off timeout (`Off / 30s / 60s / 120s`).

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
git clone https://github.com/wolfydog1/g-helper-flow-x16-2022.git
cd g-helper-flow-x16-2022/app

# Build standalone Release executable
dotnet publish GHelper.csproj -c Release -r win-x64 -p:PublishSingleFile=true --no-self-contained
```
The compiled executable will be located in `app/bin/x64/Release/net10.0-windows10.0.19041.0/win-x64/publish/`.

---

## 📄 License
Based on the open-source G-Helper project by [seerge](https://github.com/seerge/g-helper), licensed under GNU General Public License v3.0 (GPL-3.0).
