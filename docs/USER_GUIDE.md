# 📖 G-Helper (Flow X16 & Convertible Edition) — New User Guide

Welcome! **G-Helper** is a small, fast, and lightweight replacement for **ASUS Armoury Crate** and **MyASUS**. Instead of installing gigabytes of background services, G-Helper runs from a **single `.exe` file** and talks directly to the official ASUS BIOS / ACPI driver already built into your laptop.

This guide walks you through installation, initial setup, and every section of the app in plain English.

---

## 📥 1. Download & First-Time Setup

### Step 1: Check Prerequisites
Before running G-Helper, make sure you have:
1. **[.NET 10.0 Desktop Runtime (x64)](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)** — Required to run the app (Windows will prompt you if it's missing).
2. **ASUS System Control Interface v3** — The official ASUS hardware driver. This is pre-installed on almost all ASUS laptops via Windows Update.

### Step 2: Download G-Helper
Grab the latest version from the [GitHub Releases Page](https://github.com/wolfydog1/g-helper-flow-x16-2022/releases/latest):
- **[📦 Download GHelper.zip](https://github.com/wolfydog1/g-helper-flow-x16-2022/releases/latest/download/GHelper.zip)** *(Recommended)*
- **[⚡ Download GHelper.exe](https://github.com/wolfydog1/g-helper-flow-x16-2022/releases/latest/download/GHelper.exe)** *(Direct standalone file)*

### Step 3: Put `GHelper.exe` in a Permanent Folder
> [!IMPORTANT]
> Do **not** run `GHelper.exe` directly from inside a temporary `.zip` preview window or your `Downloads` folder if you clean it often.
1. Create a folder such as `C:\Users\<YourName>\Apps\GHelper` or `C:\Program Files\GHelper`.
2. Move `GHelper.exe` into that folder and double-click it to launch.
3. *(Optional)* If Windows SmartScreen shows *"Windows protected your PC"*, click **More info** → **Run anyway**.

### Step 4: Enable Startup & Stop Bloatware Services
1. In the bottom-left of the G-Helper main window, check **`Run on Startup`**. This creates a Windows Scheduled Task so G-Helper starts automatically with Administrator privileges when you log in.
2. Click **`Extra`** at the bottom → switch to the **`System`** tab → click **`Stop`** next to **Asus Services Running**. This cleanly stops background ASUS services so they don't conflict with G-Helper's fan, lighting, or hotkey controls.

---

## 🖥️ 2. Main Window Tour

Clicking the **G-Helper icon** in your Windows system tray (bottom-right near the clock) or pressing the **ROG / M4 key** opens the main dashboard.

### 🚀 Performance Modes (`Silent` / `Balanced` / `Turbo`)
Performance modes are baked directly into your laptop's BIOS and paired with matching Windows Power Modes:
- **🚲 Silent**: Quiet operation for web browsing, office work, video playback, or tablet use. Keeps fans off (`0 RPM`) at low temperatures.
- **🚗 Balanced**: Everyday default mode with balanced CPU/GPU power limits and moderate fan speeds.
- **🚀 Turbo**: Maximum CPU and GPU wattage with aggressive cooling for heavy gaming, 3D rendering, or video editing.
- **⚙️ Fans + Power**: Opens the advanced tuning window where you can customize CPU wattage sliders (`SPL`, `sPPT`, `fPPT`), custom 3-fan curves, GPU clock offsets, and AMD CPU undervolting.

### 🎮 GPU Modes (`Eco` / `Standard` / `Ultimate` / `Optimized`)
Controls whether the dedicated NVIDIA RTX GPU (`dGPU`) is powered on or completely turned off to save battery:
- **🍃 Eco**: Completely powers off the NVIDIA dGPU. Only the AMD Radeon iGPU runs. **Doubles your battery life** on the go!
- **🌸 Standard (MS Hybrid)**: Both iGPU and NVIDIA dGPU are active; Windows automatically uses the NVIDIA GPU for games and creative apps.
- **🎮 Ultimate (MUX Switch)**: Routes the internal display directly to the NVIDIA dGPU, bypassing the iGPU for maximum gaming FPS *(requires a reboot when toggling)*.
- **💡 Optimized *(Recommended)***: Automatically switches to **Eco** when unplugged on battery, and **Standard** when plugged into AC power.

### 💻 Laptop Screen & Convertible Controls
Tailored for the Flow X16's **2560×1600 165Hz Mini-LED Nebula HDR** touchscreen and 360° hinge:
- **Header (`Screen: 165Hz • Laptop | 140 Lux`)**: Shows your current refresh rate, live 360° hinge posture (`Laptop`, `Tent`, or `Tablet`), and ambient room brightness in Lux.
- **`Auto` / `60Hz` / `165Hz`**:
  - `60Hz` saves battery power.
  - `165Hz` gives ultra-smooth motion.
  - `Auto` automatically switches to `60Hz` on battery and `165Hz` when plugged in.
- **`Overdrive`**: Enables panel response-time overdrive to reduce motion blur at 165Hz.
- **`One Zone` / `Multi Zone` (Mini-LED)**:
  - **One Zone**: Uniform single-zone backlight — ideal for color-accurate photo editing, dark-mode web browsing without haloing, and saving ~4–6W of battery.
  - **Multi Zone (512-Zone Local Dimming)**: Deep OLED-like blacks and 1100-nit peak brightness for HDR movies and games. *(Right-click this button to cycle `Multi Zone Strong` ↔ `Multi Zone Balanced` ↔ `One Zone`)*.
- **`Touch: On` / `Pen Only` / `Touch: Off`**:
  - **Touch: On**: Finger touch and stylus pen both enabled.
  - **Pen Only (Hardware Palm Rejection)**: Disables finger touch while keeping your ASUS Pen / MPP 2.0 stylus active so you can rest your palm on the glass while drawing or taking notes! *(Tip: **Right-click** the Touch button to jump straight to `Pen Only`)*
  - **Touch: Off**: Disables touchscreen input completely.
- **`Rotate: Lock` / `Rotate: On`**: Locks or unlocks screen auto-rotation. By default, folding the laptop into **Tablet** or **Tent** posture automatically unlocks rotation!
- **`HDR` & `Auto HDR`**: One-click toggles for Windows HDR and Windows 11 Auto-HDR for games.
- **`Overlay: On` / `Off`**: Left-click toggles the in-game FPS & hardware telemetry HUD (`Ctrl + Shift + Alt + O`). **Right-click** opens the full **Overlay Settings & Telemetry HUD** customizer.
- **Brightness Slider + `Auto` (ALS)**: Drag to set screen brightness, or toggle `Auto` to let the Ambient Light Sensor smoothly adjust screen brightness to your room. **Right-click `Auto`** to open the interactive **Lux Curve Editor**.

### ⌨️ Laptop Keyboard
- **Aura Mode & Color**: Choose from `Static`, `Breathe`, `Strobe`, `Rainbow`, `Heatmap (Thermal)`, or `Battery Charge Level`.
- **`FN-Lock`**: Swaps `F1–F12` between standard function keys and ASUS media/hotkey actions without holding `Fn`.
- **`Mic: On` / `Muted`**: Left-click mutes/unmutes your microphone (and syncs the hardware mic LED). **Right-click** quickly switches your active Windows audio output device (e.g., Speakers ↔ Headphones).
- **`Power Saver` / `Awake (Caffeine)`**:
  - **Left-click**: Toggles Windows Energy Saver immediately (even while plugged in).
  - **Right-click**: Cycles the **Keep Awake (Caffeine)** timer (`30m` → `1h` → `2h` → `∞` → `Off`) to keep your laptop from sleeping during downloads or presentations.
- **Backlight Slider**: Drag from `Off` to `Max`. **Right-click** the `Backlight:` label to cycle the idle auto-off timeout (`Off / 30s / 60s / 120s`).

### 🔋 Battery Charge Limit & Smart Power Row
- **Charge Limit Slider (`40% – 100%`)**: Setting a limit of **70%–80%** if you mostly use the laptop plugged in drastically slows down long-term lithium-ion battery aging.
- **Live Battery Header**: Displays real-time wattage draw (`Discharging: 12.4W (~5h 10m left)` or `AC Bypass (0.0W)`), color-coded Green (`<12W`), Blue (`12–25W`), or Orange (`>25W`).
- **`Pause` (`Pause Charge`)**: Immediately freezes battery charging and runs on AC power bypass.
- **`Smart <60%` (Anti-Micro-Cycling)**: When enabled, plugging in the laptop when your battery is already between 60% and 80% keeps it on pure **AC bypass** instead of repeatedly micro-charging from 76% → 80%. Charging only starts when the battery drops below 60%. *(Right-click to choose `<50%`, `<60%`, or `<70%`)*.
- **`No Boost` (`No Boost on Bat`)**: Left-click disables aggressive CPU Turbo Boost while on battery to prevent sudden wattage spikes and fan spin-up. **Right-click** toggles **Ultra-Eco Travel Mode** (one-click switch to Silent + Eco GPU + 60Hz + 1-Zone + Power Saver).
- **`1-Zone Bat` & `No HDR Bat`**: Automatically switches to single-zone backlight and turns off HDR when unplugged on battery, restoring your settings when plugged back in.

---

## 🌡️ 3. Fans & Power Window (`Fans + Power`)

Click **`Fans + Power`** on the main window to fine-tune each performance mode (`Silent`, `Balanced`, `Turbo`, or custom `+` profiles):

1. **CPU Tab**:
   - **Windows Power Mode**: Pair the profile with `Best power efficiency`, `Balanced`, or `Best performance`.
   - **CPU Boost**: Choose `Disabled`, `Enabled`, `Aggressive`, or `Efficient Enabled` *(recommended for Ryzen 6000/7000/8000/9000)*.
   - **Power Limits (`SPL`, `sPPT`, `fPPT`)**: Set sustained (`SPL`), 2-minute boost (`sPPT`), and 2-second burst (`fPPT`) CPU wattage caps. Check **Apply Power Limits** to activate.
2. **GPU Tab**:
   - Adjust **GPU Core Clock Offset**, **GPU Memory Clock Offset**, **GPU Power / Dynamic Boost**, and **GPU Temperature Target**.
3. **Advanced Tab**:
   - Configure **AMD Ryzen CPU Temperature Limit** and **CPU Undervolting (Curve Optimizer)** to run cooler at the same clock speeds.
4. **4-Fan BIOS & USB Fan Curves & Acoustic Presets**:
   - Customize all Flow X16 internal fans (**CPU Fan**, **GPU Fan**, **Mid System Fan**) as well as your connected **ROG XG Mobile Fan** by dragging points on the temperature/RPM graphs.
   - When the XG Mobile is connected, an orange **XG Mobile Fan Profile** graph appears alongside live GPU core/hotspot temperature and RPM telemetry. Click **`Test XGM`** to briefly test fan ramp-up and verify live RPM feedback.
   - Use the top **Acoustic Preset Bar** for instant 1-click tuning across all active fans:
     - **`0-RPM <55°C`**: Dead-silent passive cooling below 55°C.
     - **`38dB Whisper`**: Smooth, low-noise curve capped for quiet rooms.
     - **`Frost Force`**: Optimized multi-fan balanced intake curve.
     - **`Max Cooling`**: Aggressive 95%–100% full-blast ramp for heavy rendering or benchmarking.
     - **`Anti-Pulse: ON`**: Smooths out sudden fan revving caused by brief 1-second CPU temperature spikes.

---

## ⚙️ 4. Extra Settings (6 Organized Tabs)

Click **`Extra`** at the bottom of the main window to access deep hardware, automation, and UI settings across 6 clean tabs:

| Tab | What You Can Configure |
| :--- | :--- |
| **⌨️ Keys** | Remap `M1`, `M2`, `M3`, `M4/ROG`, `Fn+F4`, `Fn+C`, and `Fn+V` to any action (Open G-Helper, Pen-Only Mode, Rotate Lock, Travel Mode, Keep Awake, Touchpad Toggle, RAM Trim, Custom App/Macro, etc.). |
| **💡 Light** | Configure Keyboard Awake/Sleep/Boot animations, Backlight timeouts, Auto-dim on battery, Thermal/Battery Aura modes, **Laptop Backlight** curve, and **XG Mobile eGPU Dock** controls (`XG Mobile LED`, `Limit battery to 80% when docked`, `Link Mid Fan to CPU`, `Independent XGM Fan Curve`, `Handshake & Dock XG Mobile`, and `Hardware Latch Interlock`). |
| **⚡ Power** | Full Windows **Power & Sleep** matrix (Screen off, Dim, Adaptive brightness, Sleep, Hibernate, Lid close, and Power button actions for both **Plugged in** and **On battery**), **USB-C CPU Power Cap** (`35W` / `45W` / `54W` to prevent battery drain on 65W/100W USB-C chargers), and the **Battery Health Inspector & Report** tool. |
| **🤖 Auto** | Smart posture and app automations: **Manage Per-App Custom Performance & Fan Profiles (`.exe` Rules)**, **Indie / 2D Games 35W Silent Mode**, **Game Detection Low-Latency Wi-Fi**, **Tablet Mode Auto-Silent**, **Auto-Rotate in Tablet/Tent**, **Palm Rejection in drawing/note apps**, **Tent Mode Thermal Boost**, **Posture Touch Bar**, **Creator Mode (512-Zone + 165Hz)**, **Clamshell Mode**, and **Low-Battery Guard (≤20%)**. |
| **⚙️ System** | Hardware & OS optimizations: **Wi-Fi Low-Latency Mode** (eliminates MediaTek/Intel ping spikes), **Disable PCIe ASPM on AC**, **Turn Off Wi-Fi in Modern Standby** (prevents hot bag drain), **Pause NVIDIA Services when dGPU is off**, **Keep dGPU Asleep on USB-C Chargers**, **Cap Power Draw on USB-C**, **ASUS Startup Chime**, **Display Overdrive**, and **Asus Services Start/Stop**. |
| **🎨 Style** | Personalize G-Helper: **Theme Color Palette** (*Nebula Midnight Blue*, *ROG Cyber Crimson*, *OLED Pitch Black*, *Catppuccin Mocha*, *Nord Frost*, *Emerald Matrix*, *ROG Ally Violet*), **Typography Font Family**, **Interface Text Size Scale (`85%–125%`)**, **Button Corner Radius**, **System Tray Sensor Icon** (Live CPU Temp, Battery Wattage, Dual Temps, etc.), **Export / Restore Settings Backup**, **Live 60s Sensor Graph**, and **Pin Window on Top**. |

---

## 🪄 5. Hidden Power-User Right-Click & Keyboard Shortcuts

Many buttons in G-Helper have a fast **Right-Click** secondary action:

| Control / Key | Action |
| :--- | :--- |
| **Right-click `CPU` or `GPU` temp label** | Immediately trims Windows standby RAM and shows how many MB of memory were freed |
| **Left-click `CPU` or `GPU` temp label** | Toggles fan readout between **RPM** (`4200RPM`) and **Percentage** (`45%`) |
| **Right-click `Silent` mode button** | Toggles **Auto-Silent in Tablet Mode** |
| **Right-click `Touch` button** | Jumps directly in/out of **`Pen Only`** (Hardware Palm Rejection) mode |
| **Right-click `Rotate` button** | Toggles **Auto-Unlock Rotation in Tablet/Tent Mode** |
| **Right-click `Multi Zone` / `One Zone`** | Cycles **Multi Zone Strong** → **Multi Zone Balanced** → **One Zone** |
| **Right-click `Overlay` button** | Opens the **Overlay Settings & Telemetry HUD** configuration window |
| **Right-click `Auto` (Screen Brightness)** | Opens the interactive **Ambient Light Sensor (ALS) Lux Curve Editor** |
| **Right-click `Mic: On / Muted` button** | Cycles active Windows **Audio Output Devices** (Speakers ↔ Headphones) |
| **Right-click `Power Saver` button** | Cycles **Keep Awake (Caffeine)** timer (`30m` → `1h` → `2h` → `∞` → `Off`) |
| **Right-click `Backlight:` label** | Cycles keyboard backlight idle timeout (`Off` / `30s` / `60s` / `120s`) |
| **Right-click `Smart <60%` button** | Cycles Smart Charge resume threshold (`<50%` → `<60%` → `<70%`) |
| **Right-click `No Boost` button** | Toggles reversible **Ultra-Eco Travel Mode** |
| **`Ctrl + Shift + Alt + O`** | Toggle In-Game Telemetry HUD Overlay ON / OFF |
| **`Ctrl + Shift + Alt + P`** | Cycle In-Game HUD Overlay preset (`Light` → `Default` → `Full` → `Complete` → `Strip`) |
| **`Ctrl + Shift + Alt + M`** | Quick-toggle Mini-LED (`One Zone` ↔ `Multi Zone`) from anywhere |
| **`Ctrl + Shift + Alt + F`** | Quick-toggle Refresh Rate (`60Hz` ↔ `165Hz`) from anywhere |
| **`Fn + F5` / `Ctrl + Shift + F5`** | Cycle Performance Modes (`Silent` → `Balanced` → `Turbo`) |
| **`Ctrl + Shift + F12` (or `M4 / ROG` key)** | Show / Hide G-Helper main window |

---

## ❓ 6. FAQ & Troubleshooting

### Q: Does G-Helper modify my BIOS or void my warranty?
**No.** G-Helper is not firmware or a kernel driver. It sends the exact same official commands to the **ASUS System Control Interface v3** driver that Armoury Crate and MyASUS use. Closing G-Helper or uninstalling it leaves your hardware in its standard factory state.

### Q: How do I update G-Helper when a new release comes out?
G-Helper checks [GitHub Releases](https://github.com/wolfydog1/g-helper-flow-x16-2022/releases) automatically once every 12 hours. When an update is available, the `Version` link in the bottom-left turns into a **Download Update** button—click it to update in place, or download the latest `GHelper.zip` and overwrite `GHelper.exe`.

### Q: Why won't my GPU switch to `Eco` mode?
If an application is actively running on the NVIDIA GPU, Windows blocks disabling the dGPU. Either:
1. Click the G-Helper tray icon or GPU status message to see which apps are using the NVIDIA GPU and close them, **or**
2. Open **Extra → System** and check **`Auto-Close Apps Using GPU When Switching to Eco Mode`**.

### Q: How do I safely dock, control fans, and undock the ROG XG Mobile?
1. **Docking**: Plug the proprietary PCIe + USB-C connector into the side of your Flow X16 and lock the physical cable latch. Open G-Helper and click **`XGM`** on the main window (or click **`⚡ Handshake & Dock XG Mobile`** in **Extra → Light**). The PCIe bus will handshake and initialize the external GPU cleanly.
2. **Fan Control**: Open **Fans + Power**. The orange **XG Mobile Fan Profile** graph appears alongside CPU, GPU, and Mid fan curves. Drag temperature and fan percentage points to set your curve, or click presets like **`Frost Force`** or **`38dB Whisper`** to configure all 4 fans simultaneously. Click **`Test XGM`** to verify fan response.
3. **Safe Undocking**: Click **`⏏ Safely Undock XG Mobile (Park PCIe)`** in **Extra → Light** (or toggle XGM on the main dashboard). G-Helper will close active GPU applications, flush and park the PCIe bus, and alert you with a chime when it is 100% safe to slide the mechanical cable latch and disconnect. Never pull the cable while PCIe is active to avoid Windows blue screens (BSOD)!

### Q: How do I completely uninstall G-Helper?
1. Open G-Helper and uncheck **`Run on Startup`**.
2. Click **`Quit`**.
3. Delete `GHelper.exe` (and optionally `%AppData%\GHelper` if you want to remove saved preferences). Nothing else is installed on your PC!
