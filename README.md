# ⚡ G-Helper — ROG Flow X16 & Convertible Edition

Small, fast, and lightweight **Armoury Crate** alternative for **ASUS ROG Flow X16 (GV601)** and ASUS convertible/gaming laptops. Offers all the functionality of Armoury Crate and MyASUS—plus specialized **Mini-LED**, **360° Tablet/Tent Posture**, **Stylus Palm Rejection**, **3-Fan Frost Force**, **Smart Battery Bypass**, and **XG Mobile** controls—in a single standalone `.exe` with zero background bloat.

[![GitHub Release](https://img.shields.io/github/v/release/wolfydog1/g-helper-flow-x16-2022?style=for-the-badge&logo=github&label=Release&color=0078D4)](https://github.com/wolfydog1/g-helper-flow-x16-2022/releases/latest)
[![Downloads](https://img.shields.io/github/downloads/wolfydog1/g-helper-flow-x16-2022/total?style=for-the-badge&logo=github&label=Downloads&color=107C41)](https://github.com/wolfydog1/g-helper-flow-x16-2022/releases)
[![Build Status](https://img.shields.io/github/actions/workflow/status/wolfydog1/g-helper-flow-x16-2022/build.yml?branch=main&style=for-the-badge&logo=githubactions&label=Build)](https://github.com/wolfydog1/g-helper-flow-x16-2022/actions)
[![License](https://img.shields.io/github/license/wolfydog1/g-helper-flow-x16-2022?style=for-the-badge&color=5c2d91)](LICENSE)

<p align="left">
  <a href="https://github.com/wolfydog1/g-helper-flow-x16-2022/releases/latest/download/GHelper.zip">
    <img width="250" alt="Download G-Helper ZIP" src="https://g-helper.com/button/download.svg?lang=en" />
  </a>
</p>

- **📦 [Download Latest `GHelper.zip` (Recommended)](https://github.com/wolfydog1/g-helper-flow-x16-2022/releases/latest/download/GHelper.zip)** — Extract `GHelper.exe` to any folder and run!
- **⚡ [Download Standalone `GHelper.exe`](https://github.com/wolfydog1/g-helper-flow-x16-2022/releases/latest/download/GHelper.exe)** — Direct single-file executable
- **📖 [New User Guide (Step-by-Step Walkthrough)](docs/USER_GUIDE.md)**
- **📋 [All Releases & Changelog](https://github.com/wolfydog1/g-helper-flow-x16-2022/releases)**
- **❓ [FAQ & Troubleshooting](docs/USER_GUIDE.md#-6-faq--troubleshooting)**

---

## 🚀 Quick Start for New Users (Under 2 Minutes)

1. **Install Prerequisites** *(if not already installed)*:
   - **[.NET 10.0 Desktop Runtime (x64)](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)** — Required to run the app.
   - **ASUS System Control Interface v3** — Pre-installed on ASUS laptops via Windows Update / MyASUS.
2. **Download & Extract**:
   - Download **[`GHelper.zip`](https://github.com/wolfydog1/g-helper-flow-x16-2022/releases/latest/download/GHelper.zip)** and extract `GHelper.exe` into a permanent folder (for example, `C:\Program Files\GHelper` or `C:\Users\<You>\Apps\GHelper`).
   - *Note: Do not run `GHelper.exe` directly from inside a temporary ZIP preview window.*
3. **Launch & Enable Startup**:
   - Double-click **`GHelper.exe`** (if Windows SmartScreen prompts you, click **More info** → **Run anyway**).
   - Check **`Run on Startup`** in the bottom-left corner of the G-Helper window.
4. **Stop Background ASUS Bloatware *(Optional but Recommended)***:
   - Click **`Extra`** → **`System`** tab → click **`Stop`** next to **Asus Services Running** so Armoury Crate / MyASUS background services don't override your fan curves or hotkeys.

👉 **New to G-Helper?** Read the complete **[📖 Beginner User Guide](docs/USER_GUIDE.md)** for a full tour of every button, tab, and shortcut!

---

## 🎁 Why Use G-Helper?

1. **Single Portable `.exe`**: Doesn't install background bloatware, telemetry, or kernel services.
2. **Seamless GPU Switching**: Automatically turns off the NVIDIA dGPU on battery (**Eco**) and re-enables it on AC (**Standard**).
3. **Full 3-Fan Frost Force Control**: Independent custom curves and 1-click acoustic presets for **CPU**, **GPU**, and **Mid System** fans.
4. **Built for 2-in-1 Convertibles**: Native 360° hinge detection (`Laptop` / `Tent` / `Tablet`), hardware stylus palm rejection (`Pen Only`), auto-rotation lock, and Ambient Light Sensor (ALS) curves.
5. **Protects Battery Longevity**: Charge limit slider (`40%–100%`), **Smart `<60%` AC Bypass** (prevents micro-cycling wear), and USB-C charger wattage protection.

---

## 🌟 Flow X16 & Convertible Edition Exclusive Features

### 1. 🖊️ 3-State Touchscreen & Hardware Palm Rejection (`Pen Only`)
- **`Touch: On`**: Standard finger touch + stylus pen both enabled.
- **`Pen Only`**: Disables capacitive finger touch (`HID-compliant touch screen`) while keeping the MPP 2.0 stylus / ASUS Pen (`HID-compliant pen`) active—sketch or take notes with zero accidental palm inputs!
- **`Touch: Off`**: Completely disables touch and stylus input.
- **Auto Touch Keyboard & Tablet Safety**: Automatically enables Windows Touch Keyboard / Handwriting pop-up in **Tablet**, **Tent**, or **Pen Only** mode, and automatically re-enables touch if folded 360° into Tablet mode.

### 2. 🔄 360° Hinge Posture Automation & Ambient Light Sensor (ALS)
- **Live Posture & Lux Header**: Displays live 2-in-1 posture (`Laptop`, `Tent`, or `Tablet`) and room light (`Screen: 165Hz • Laptop | 140 Lux`).
- **Auto-Rotate & Auto-Silent in Tablet Mode**: Automatically unlocks screen rotation and switches to **Silent** mode when folded into Tablet posture so the chassis stays cool in your hands, and restores your previous mode when flipped back.
- **Interactive ALS Lux Curve Editor**: Smooth EMA-filtered auto screen brightness and auto keyboard backlight with a custom 8-point drag-and-drop Lux curve editor *(Right-click `Auto` in the Screen section)*.

### 3. 🖥️ Mini-LED Nebula HDR (`One Zone` vs `512-Zone`) & HDR Automation
- **Instant Zone Switching**: Toggle between **One Zone** (uniform backlight for color work & zero haloing) and **Multi Zone** (512-zone local dimming for deep HDR contrast). Right-click to cycle **Multi Zone Strong** ↔ **Multi Zone Balanced** ↔ **One Zone**.
- **Battery Savers (`1-Zone Bat` & `No HDR Bat`)**: Automatically switches to single-zone backlight and disables HDR when unplugged on battery.
- **Creator Mode**: Automatically enables **512-Zone Mini-LED + 165Hz** when launching creative apps like Photoshop, Premiere, Lightroom, or DaVinci Resolve.

### 4. 🛡️ Smart Charge (`Smart <60%`), AC Bypass & USB-C Power Guard
- **`Smart <60%` Anti-Micro-Cycling**: If your battery is already above 60% (e.g., 75%) when you plug in, G-Helper keeps the laptop on pure **AC bypass** without topping up. Charging only resumes when the battery drops below 60% (`<50%`, `<60%`, or `<70%` via right-click).
- **`Pause` (`Pause Charge`)**: One-click AC bypass at any battery percentage.
- **USB-C Charger Protection**: Configurable **USB-C CPU Power Cap** (`35W` / `45W` / `54W` in **Extra → Power**) prevents 65W/100W USB-C PD chargers from overloading or draining the battery under heavy load.
- **Battery Health Inspector**: Live wattage, smoothed ETA, wear %, and one-click Windows Battery Report generator.

### 5. ❄️ 3-Fan Frost Force Acoustic Presets & Anti-Pulse
- Inside **Fans + Power**, switch between 1-click acoustic presets above the fan charts:
  - **`0-RPM <55°C`** (Silent passive cooling) • **`38dB Whisper`** • **`Frost Force`** (3-fan balanced intake) • **`Max Cooling`** • **`Anti-Pulse: ON`** (hysteresis smoothing against brief CPU temp spikes).

### 6. 🎮 In-Game Telemetry HUD Overlay (`Ctrl + Shift + Alt + O`)
- Built-in DirectX/OpenGL/Vulkan overlay showing live **FPS**, **1% Low FPS**, **Frametime graph**, **CPU/GPU Temps & Clocks**, **3-Fan RPMs**, **Battery Wattage**, **VRAM/RAM**, and **512Z/HDR badge**.
- 5 layout presets (`Light`, `Default`, `Full`, `Complete`, `Strip`), customizable fonts, themes, transparency, and corner positioning *(Right-click `Overlay` on the main window)*.

### 7. 🔌 XG Mobile eGPU Dock Safety Suite (`Extra → Light`)
- **Handshake & Dock XG Mobile** wizard, **Hardware Latch Interlock** (auto-parks PCIe before undock), **Independent XGM Fan Curve**, **Link Mid Fan to CPU when docked**, and **Limit battery to 80% when docked**.

### 8. 🎨 6-Tab Extra Settings & Theme Engine (`Extra`)
- Organized into 6 uniform tabs: **`Keys`**, **`Light`**, **`Power`**, **`Auto`**, **`System`**, and **`Style`**.
- Choose from 7 color themes (*Nebula Midnight Blue*, *ROG Cyber Crimson*, *OLED Pitch Black*, *Catppuccin Mocha*, *Nord Frost*, *Emerald Matrix*, *ROG Ally Violet*), custom fonts, UI text scaling (`85%–125%`), corner radius, live tray temperature/wattage icons, and 1-click JSON **Backup / Restore**.

---

## 💻 GPU Modes Explained

1. **🍃 Eco**: Only low-power AMD Radeon iGPU is enabled; NVIDIA dGPU is completely powered off for maximum battery life.
2. **🌸 Standard** *(MS Hybrid)*: Both iGPU and dGPU are enabled; iGPU drives the internal display and Windows assigns demanding apps to the NVIDIA dGPU.
3. **🎮 Ultimate** *(MUX Switch)*: NVIDIA dGPU drives the internal display directly for maximum gaming performance *(requires reboot)*.
4. **💡 Optimized**: Automatically disables the dGPU on battery (**Eco**) and re-enables it when plugged in (**Standard**).

<img width="900" alt="GPU Modes" src="https://raw.githubusercontent.com/seerge/g-helper/main/docs/gpu-modes-en.svg" />

---

## 🪄 Quick Right-Click & Hotkey Cheat Sheet

| Shortcut / Action | Function |
| :--- | :--- |
| **Right-click `Touch` button** | Toggle **`Pen Only`** (Hardware Stylus Palm Rejection) |
| **Right-click `Multi Zone` / `One Zone`** | Cycle **Multi Zone Strong** → **Balanced** → **One Zone** |
| **Right-click `Overlay` button** | Open **Overlay Settings & Telemetry HUD** customizer |
| **Right-click `Auto` (Screen Brightness)** | Open interactive **ALS Lux Curve Editor** |
| **Right-click `CPU` / `GPU` temp label** | Trim Windows standby RAM immediately |
| **Right-click `Mic` button** | Switch active Windows **Audio Output Device** (Speakers ↔ Headphones) |
| **Right-click `Power Saver` button** | Cycle **Keep Awake (Caffeine)** timer (`30m` / `1h` / `2h` / `∞` / `Off`) |
| **Right-click `Smart <60%` button** | Cycle Smart Charge threshold (`<50%` / `<60%` / `<70%`) |
| **Right-click `No Boost` button** | Toggle reversible **Ultra-Eco Travel Mode** |
| **`Ctrl + Shift + Alt + O`** | Toggle In-Game Telemetry HUD Overlay ON / OFF |
| **`Ctrl + Shift + Alt + P`** | Cycle In-Game HUD Overlay preset (`Light` → `Default` → `Full` → `Complete` → `Strip`) |
| **`Ctrl + Shift + Alt + M`** | Toggle Mini-LED (`One Zone` ↔ `Multi Zone`) |
| **`Ctrl + Shift + Alt + F`** | Toggle Refresh Rate (`60Hz` ↔ `165Hz`) |
| **`Fn + F5` / `Ctrl + Shift + F5`** | Cycle Performance Modes (`Silent` → `Balanced` → `Turbo`) |
| **`Ctrl + Shift + F12` (or `M4 / ROG` key)** | Open / Hide G-Helper window |

---

## 🛠️ Building from Source

Requires [.NET 10.0 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/10.0):

```powershell
# Clone the repository
git clone https://github.com/wolfydog1/g-helper-flow-x16-2022.git
cd g-helper-flow-x16-2022

# Publish single-file Release executable
dotnet publish app/GHelper.sln -c Release -r win-x64 -p:PublishSingleFile=true --no-self-contained -o ./publish
```

The compiled executable will be placed in `./publish/GHelper.exe`.

---

## 🔖 Important Notice & Credits

> [!NOTE]
> G-Helper is **NOT** an operating system, firmware, or kernel driver. It selects predefined operating modes baked into your laptop's BIOS by ASUS and communicates through the official **ASUS System Control Interface v3** driver—the exact same interface used by Armoury Crate.

- Built on top of the open-source **[G-Helper](https://github.com/seerge/g-helper)** project by **[@seerge](https://github.com/seerge)**.
- Licensed under the **[GNU General Public License v3.0 (GPL-3.0)](LICENSE)**.
