<p align="center"><img src="desktop-app/assets/airplay.png" width="128" alt="AirPlay Control Center icon"></p>

# AirPlay Control Center

A Windows desktop control panel for [Java AirPlay](https://github.com/serezhka/java-airplay), with configurable mirroring settings and a resizable player window.

**Original AirPlay implementation:** [serezhka/java-airplay](https://github.com/serezhka/java-airplay), created by [serezhka](https://github.com/serezhka). This project adds the Windows control panel and a small player-window customization. It is an independent derivative project, not an official Apple product or an official upstream release.

## Download

Download **AirPlay-Control-Center-1.0.0-windows-x64.zip** from [Releases](https://github.com/laosun124/airplay-control-center/releases/latest), extract the entire archive, and open **AirPlayControl.exe**. Keep the other extracted files alongside the executable.

The ZIP includes the compiled Windows app, its windowed AirPlay receiver, interface resources, documentation, and license notices. **Java is not included.** You do not need to build the source or run a JAR manually.

GitHub's automatic **Source code** archives are for development. Choose the named Windows ZIP for the ready-to-run app.

## Requirements

- Windows 11 x64. Windows 10 x64 may work but has not been tested.
- Java **21 or later**, installed separately and available through `JAVA_HOME` or `PATH`.
- Microsoft Edge or Google Chrome in a standard installation location.
- The PC and sending Apple device on the same local network.

### Install Java

Open **Download Java.html** from the ZIP, or visit the official [Oracle Java download page](https://www.oracle.com/java/technologies/downloads/).

Choose a Windows x64 JDK installer for Java 21 or later (Java 25 is supported). Reopen the control panel after installation. If Java is not detected, set `JAVA_HOME` to the installed JDK directory or add its `bin` directory to `PATH`. Restart Windows if desktop apps still do not see the updated environment.

To verify your installation in PowerShell:

```powershell
java -version
```

Java 8 and Java 17 are too old for this receiver. No FFmpeg or GStreamer installation is required for the included MSE backend.

## Usage

1. Open **AirPlayControl.exe**.
2. Set your **Device name**, **Resolution**, **Frame rate**, and optional **HLS playback**.
3. Click **Start receiver**.
4. On your iPhone, iPad, or Mac, open **Screen Mirroring** and select **AirPlay Receiver**, or the custom name you entered.
5. The player opens in a normal 1120 x 760 window. Minimize, maximize, resize, and close it with the standard Windows controls. Maximizing respects the taskbar; automatic taskbar hiding follows Windows settings.
6. Use **Save and restart** to apply settings while running, then reconnect your Apple device.

Closing the control panel stops the receiver and its player processes. **Details and logs** shows receiver output and provides a shortcut to the log folder.

Resolution and frame rate are receiver preferences; actual output is negotiated with the sending device and depends on hardware and network conditions.

## Settings and troubleshooting

- Settings: `%LOCALAPPDATA%\AirPlayControl\application.properties`
- Logs: `%LOCALAPPDATA%\AirPlayControl\logs\control.log`
- Self-check report: `%LOCALAPPDATA%\AirPlayControl\logs\self-test.txt`

If Java is missing or too old, the app explains how to install it. If the receiver is not listed on your Apple device, check the local network, network isolation, VPN routing, and Windows firewall permissions. The app does not automatically change firewall settings.

DRM-protected content may not play. Connection stability, latency, and application compatibility depend on upstream support and your network.

## Build from source

The control panel uses WPF and the Windows .NET Framework C# compiler; no separate .NET SDK is required. Building the receiver customization requires **Python 3** and **JDK 21 or later** (`javac` and `java` on `PATH`).

From a checkout on Windows, run:

```powershell
.\desktop-app\package.ps1
```

This downloads the official upstream v1.1.1 JAR, builds the modified browser launcher, compiles the Windows app, and creates a software-only ZIP in `dist/`. Java is never copied into the package. Close any running executable from this checkout before rebuilding it.

Individual build and check commands:

```powershell
python .\desktop-app\build-windowed.py
.\desktop-app\build.ps1
Start-Process .\AirPlayControl.exe -ArgumentList '--self-test' -Wait
Get-Content "$env:LOCALAPPDATA\AirPlayControl\logs\self-test.txt"
```

`build-windowed.py` retains the original JAR and verifies that only `ChromiumHost.class` and `mse/player.html` change. The receiver uses a normal 1120 x 760 window, isolated browser preferences, and player-page attributes to suppress translation prompts and picture-in-picture entry points. The launch-command self-check also covers the headless path. The control-panel self-check covers the default device name, Java-version parsing, Unicode settings, atomic saving, input validation, and dependency discovery.

## Credits and licenses

- Original receiver: **Sergei Fedorov (serezhka)**, [java-airplay](https://github.com/serezhka/java-airplay), MIT license.
- Windows control panel and packaging: **laosun124**.
- Application icon: generated artwork created for this project.
- Java download provider: [Oracle](https://www.oracle.com/java/technologies/downloads/).

See [LICENSE](LICENSE), [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md), and [the upstream license](desktop-app/LICENSE.upstream). Embedded upstream libraries retain their own licenses and notices.
