# Universal Video Downloader

A modern, high-performance open-source download manager powered by **yt-dlp**, **FFmpeg**, and **aria2**. Redesigned with a beautiful dark/light mode visual studio inspired interface, localized in both English and Vietnamese.

## Key Features

- **Advanced Queue Manager**: Run concurrent downloads with thread controls, pause, resume, retry, or delete items on the fly.
- **Real-time Logging Console**: View live process outputs and logs for each active downloading task.
- **Bandwidth Speed Limiter**: Limit download speed (500 KB/s, 1 MB/s, 2 MB/s, etc.) to keep your network responsive.
- **Automated Clipboard Link Monitor**: Scans clipboard for copy-pasted video links and automatically parses them.
- **Queue Scheduler**: Automatically start download queues at designated times and perform power actions (Sleep or Shutdown) when finished.
- **History Tab with Live Search**: Browse through completed and failed items with instant filtering by title, URL, or platform.
- **Visual Studio Aesthetics**: Modern dark/light theme switching with custom bottom-right sliding notification toasts and system tray minimization.
- **Localization**: Supports complete language switching between English and Vietnamese.

## Quick Installation

Run the PowerShell installer to automatically set up the application, create shortcuts on your Desktop/Start Menu, and register it in Windows Programs (for Control Panel uninstallation):

1. Right-click `Install.ps1` -> Select **Run with PowerShell**
2. Or run via PowerShell:
   ```powershell
   powershell.exe -ExecutionPolicy Bypass -File Install.ps1
   ```

## Development & Credits

Developed by **Kaka91** using C#, WPF (.NET 10), and CommunityToolkit.Mvvm.

## License & Copyright

Copyright © 2026 **Kaka91**. All rights reserved.

This project is licensed under the [MIT License](LICENSE). You are free to use, modify, and distribute this software **provided that**:

- The original **copyright notice** and license text are retained in all copies or substantial portions of the software.
- Proper **attribution** to the original author (**Kaka91**) is given in any derivative work.
- This software **may not be rebranded, republished, or claimed** as another person's original work.

Any unauthorized removal of copyright notices, license headers, or author attribution constitutes a violation of the license terms.

---

> **Made with ❤️ by Kaka91** — [github.com/iamk1102](https://github.com/iamk1102)
