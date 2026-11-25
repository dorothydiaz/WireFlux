# WireFlux — Network, Radio & Speed Hub

A clean, modern minimalist Windows desktop & system tray application (`WireFlux.exe`) for controlling Ethernet, Wi-Fi, Airplane Mode, quick network diagnostics, and testing live internet Download & Upload speeds.

## Features

1. **Internet Download & Upload Speed Tester**
   - Multi-stream HTTPS Download (`↓ Mbps`), Upload (`↑ Mbps`), and Ping Latency (`ms`) speed tester with live real-time streaming readouts, an animated progress bar, and instant cancellation (`Stop`).

2. **Network & Radio Controls**
   - **Ethernet**: Enable, disable, or reconnect physical Ethernet adapters with live connection state, IPv4 address, link speed (`Gbps` / `Mbps`), real-time traffic meter (`↓ / ↑`), and live ping latency.
   - **Wi-Fi**: One-click switch to turn wireless adapters on or off.
   - **Airplane Mode**: System-wide Windows Airplane Mode toggle powered by the native Windows Radio Management API (`RMapi.dll`).

3. **Quick Network Utilities**
   - **Refresh Status** — Re-queries all network interfaces and radio states.
   - **Flush DNS** — Clears Windows DNS resolver cache (`ipconfig /flushdns`).
   - **Copy IP Address** — Copies your active local IPv4 address to the clipboard.
   - **Adapter Settings** — Opens classic Windows Network Connections (`ncpa.cpl`).
   - **Reconnect Ethernet** *(in System Tray menu)* — Cycles the Ethernet adapter off and back on.

4. **System Tray & Windows Startup**
   - Minimizes to the Windows notification area when closed (`X`), with double-click to reopen and a full right-click quick-action menu (including **Run Speed Test**).
   - **Run at Windows Startup** launches `WireFlux.exe --startup` silently in the background.
   - Supports flicker-free **Dark Mode** and **Light Mode** switching with smooth 60fps hover zoom micro-animations.

## Distribution (`dist\`)

- **`WireFluxSetup.exe`** — Self-contained single-file installer for Windows 10/11:
  - Default per-user install location (`%LocalAppData%\Programs\WireFlux`) or custom folder (automatically elevates via UAC if a protected folder such as `Program Files` is chosen).
  - Optional **Desktop Shortcut**, **Start Menu Shortcut**, and **Run at Windows Startup** switches.
  - Registers **WireFlux** (by **codilumi**) in Windows **Installed Apps / Add or Remove Programs** with a bundled graphical **`Uninstall.exe`** (also supports silent install/uninstall via `--silent`).
- **`WireFlux.exe` & `Uninstall.exe`** — Standalone portable executables:
  - Run `WireFlux.exe` directly from any folder or USB drive with zero installation required.
  - Run `Uninstall.exe` to remove WireFlux, clean up Windows Startup entries, shortcuts, and optional saved user preferences without affecting unrelated files in a shared folder.

## Building from Source

```powershell
.\build.ps1
```
Outputs portable `dist\WireFlux.exe`, portable `dist\Uninstall.exe`, and the standalone single-file installer `dist\WireFluxSetup.exe`.

## Copyright

Created by **codilumi**.  
Copyright © 2026 codilumi. All rights reserved.
