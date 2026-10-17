# ⚡ G-Helper (Flow X16 & Convertible Edition)

A lightweight Armoury Crate alternative for ASUS Flow laptops, extended with specialized hardware controls and enhanced mobile productivity.

---

## 🌟 What's New in this Edition

### 1. 🖊️ Pen / Stylus Mode (Hardware Palm Rejection) & Auto Touch Keyboard
- **3-State Touchscreen Toggle**:
  - **`Touch: On`**: Standard finger touch + stylus pen both enabled.
  - **`Pen Only`**: Disables the capacitive finger touchscreen (`HID-compliant touch screen`) while keeping the MPP 2.0 stylus / ASUS Pen (`HID-compliant pen`) active. Perfect for sketching, drawing, or note-taking with zero accidental hand/palm inputs!
  - **`Touch: Off`**: Completely disables touch and stylus input to save power or prevent all accidental screen touches.
- **Quick Right-Click, Tray Menu & Hardware Keybinding**: Right-clicking the Touchscreen button jumps directly into/out of **`Pen Only`** mode, select it from the system tray right-click menu, or bind **`Toggle Pen Only Mode`** to any M-key (`M3`, `M4`, `Fn+F4`) in **Extra**.
- **Auto Windows Touch Keyboard / Handwriting Pop-Up**: Automatically enables Windows Touch Keyboard / Handwriting auto-invoke when entering **Tablet**, **Tent**, or **Pen Only** mode so tapping any text field pops up the touch keyboard or stylus handwriting pad even while the physical keyboard is attached.
- **Tablet Mode Input Safety**: If touch was set to `Touch: Off` in laptop mode, folding 360° into Tablet mode automatically re-enables touch so you are never locked out of input, and restores your previous setting when flipped back.

### 2. 🔄 Rotation Lock, Posture Profiles & Live Hinge / Lux Header
- **Live Hinge Posture & Room Lux Header**: The **Laptop Screen** header displays your live 2-in-1 posture (`Laptop`, `Tablet`, or `Tent`) and real-time room ambient light (`• Laptop | 140 Lux`).
- **`Rotate: Lock` / `Rotate: On` Button**: Direct one-click toggle in the screen switches row (or bindable to a hardware key) to lock or unlock screen auto-rotation.
- **Auto Tablet / Tent Mode Rotation**: When folding the Flow X16 360° into Tablet or Tent mode, G-Helper automatically unlocks screen auto-rotation and re-locks it in Clamshell laptop mode. (Right-click the `Rotate` button to toggle auto-unlock).
- **Auto-Silent Posture Profile**: Automatically switches to **Silent** performance mode when folded into Tablet mode so the chassis stays cool and quiet in your hands, and restores your previous performance mode when returned to Laptop mode. (Right-click the **Silent** button to toggle).

### 3. ⚡ Color-Coded Live Battery Wattage, Smoothed Time Remaining & Battery Health
- Displays the live battery discharge or charge rate in Watts and EMA-smoothed estimated time remaining directly in the **Battery Charge Limit** header at all times, dynamically color-coded by power draw:
  - **Green (`< 12W` or AC Bypass)**: `80% | AC Bypass (0.0W)` or light eco discharge
  - **Blue (`12W – 25W` or Charging)**: `78% | Discharging: 14.2W (~4h 15m left)` or `78% | Charging: +28.4W (~45m)`
  - **Orange/Red (`> 25W`)**: Heavy discharge warning
- **Battery Health & Wear Indicator**: Hovering the battery header displays full battery health and capacity wear (e.g. `Battery Health: 94.2% (84.8 / 90.0 Wh)`).

### 4. 🛡️ Smart Charge Scheduler, AC Bypass & Reversible Ultra-Eco Travel Mode
- **`Smart <60%` Button**: Prevents battery micro-cycling wear when plugging and unplugging throughout the day.
  - If your battery is already above the threshold (e.g. 75%), plugging in keeps the laptop on pure **AC bypass** without topping up.
  - Charging only resumes when the battery level drops below the lower threshold.
  - Right-click cycles between `<50%`, `<60%`, and `<70%`. Also accessible from the system tray menu.
- **Reversible Ultra-Eco Travel Mode & Low-Battery Guard**: Right-click **`No Boost on Bat`** (or toggle from the system tray menu / hardware keybinding) to engage **Travel Mode** (Silent + Eco GPU + 60Hz + 1-Zone Mini-LED + No CPU Boost + Windows Power Saver), and right-click again to restore all previous settings. Also includes an **Auto Low-Battery Saver Guard** (`≤ 20%` on battery) that automatically engages Windows Power Saver and switches high refresh rates down to 60Hz.

### 5. 💡 Ambient Light Sensor (ALS) Auto Brightness, Custom Curves & Idle Backlight Timer
- **Auto Screen & Auto Keyboard Brightness**: Smoothly adjusts screen brightness and keyboard backlight using EMA-filtered lux readings and deadband hysteresis, while respecting Tablet/Tent mode, lid close, display off, and session lock states. Sliders stay active and glow green (`Eco`) in Auto mode—dragging a slider at any time smoothly overrides and disengages Auto mode.
- **Interactive Lux Curve Editor**: Live room Lux readout (`Room Light: XX Lux`), live vertical dashed Lux marker line on both charts, double-click anywhere on a chart to add control points (up to 8), right-click a point to remove it, and one-click **Reset Defaults**.
- **Keyboard Backlight Idle Auto-Off Timer**: Right-click the `Backlight:` label to quickly cycle the keyboard idle auto-off timeout (`Off / 30s / 60s / 120s`).

### 6. 🎙️ Quick Mic Mute / Audio Switcher, Timed Keep Awake (Caffeine) & RAM Trim
- **`Mic: On` / `Mic: Muted`**: Left-click toggles microphone mute and syncs the ASUS hardware mic mute LED. Right-click quickly cycles active audio output devices (e.g. laptop speakers ↔ headphones).
- **`Power Saver` + Timed Keep Awake (Caffeine)**: Left-click toggles Windows Best Power Efficiency and Energy Saver modes. Right-click cycles **Keep Awake (Caffeine)** timer presets (`30m` → `1h` → `2h` → `∞` → `Off`, also selectable in the system tray **Power & Sleep** menu) to prevent sleep and screen dimming with live remaining-time display on the button (`Awake: 30m`).
- **Thermal Color-Coding, Live Fan Curve Markers & 1-Click RAM Trim**:
  - `CPU` and `GPU` headers dynamically color-code by temperature (Normal → Warm Amber `≥ 75°C` → Hot Coral-Red `≥ 85°C`) and show live system RAM usage on hover (`System RAM: X.X GB used (XX%)`).
  - Left-click toggles RPM ↔ %, or **right-click** to immediately trim standby working-set RAM.
  - The **Fans & Power** window renders live vertical dashed temperature marker lines on the CPU, GPU, Mid, and XG Mobile fan curve charts.
- **Expanded Hardware Keybindings**: Bind custom M-keys (`M3`, `M4`, `Fn+F4`, etc.) in **Extra** to `Toggle Pen Only Mode`, `Toggle Auto-Rotation Lock`, `Toggle Ultra-Eco Travel Mode`, `Cycle Keep Awake (Caffeine)`, `Toggle Smart Charge (<60%)`, `Switch Audio Output Device`, or `Trim Standby RAM`.
- **Zero-Allocation Background Polling**: Persistent Win32 battery device handle & tag caching, reused WMI `root\wmi` scope with brightness deduplication, smart ALS suspension when the lid is closed or screen is off, and config dirty-checking eliminate redundant disk writes and background CPU/GC overhead.

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
