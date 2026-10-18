# Desktop Cat / 桌面猫咪

**English** | [简体中文](README.zh-CN.md) | [日本語](README.ja.md)

A little cat that wanders around your Windows desktop, grooms its paws, jumps, zooms and curls up for a nap. Built with C#, Windows Forms and GDI+, with smoothly changing directions, pickup reactions, cursor following and optional sounds.

[Website & download](https://desktop-cat.aitoast.cn/) · [Report an issue](https://github.com/KaMiYiLu/desktop-cat/issues)

This repository provides the application's source code. The project license has not yet been finalized; see [source and licensing status](SOURCE-STATUS.md) (Chinese). The Windows application's interface is currently in Chinese; the website supports Chinese and English.

## Features

- Hold the left mouse button on the cat to pick it up. Put it down for a random reaction.
- Roaming, paw grooming, jumps in place, forward leaps, running, sprinting, muddy pawprints and curled-up naps.
- Occasional single-ear or double-ear twitches while walking; tail swishes and cursor watching while idle.
- A control panel with switches for cursor following, cursor attacks, pickup interactions, all sounds and footsteps separately.
- Cursor attacks are allowed by default. Cursor following and periodic random attacks are off. A random reaction after pickup may trigger a cursor chase; disable it in the control panel if you prefer.
- Startup shows only the cat. Right-click its system tray icon and choose **打开控制台** (Open Control Panel) to show the controls. Hold **Esc** for about 3 seconds, or use the tray menu, to quit.
- Settings are saved in `config.ini` next to the application. Normal use does not create log files.

## Build and run

Requires Windows, PowerShell and the system .NET Framework 4.x C# compiler. The project builds on the development machine, but compatibility testing across Windows versions, high-DPI settings and multiple monitors is not yet complete. No website, original Desktop Goose executable or private server is required to build or run the application.

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
.\artifacts\bin\DesktopCat-v73.exe
```

[VERSION](VERSION) is the single source of the version number. Build scripts generate the EXE version metadata and package filenames. The first build copies `config.example.ini` into the output directory; subsequent builds preserve existing user settings. Use the `OutputDirectory` parameter of `build.ps1` to choose another output directory.

## Checks and distribution

```powershell
# Rendering, poses, idle movements, grooming and package checks
powershell -ExecutionPolicy Bypass -File .\tools\test.ps1

# With an interactive desktop: controls, pickup, action completion and no-log checks
powershell -ExecutionPolicy Bypass -File .\tools\test.ps1 -Desktop

# With an audio output device: native audio-buffer checks using silent samples
powershell -ExecutionPolicy Bypass -File .\tools\test.ps1 -Audio

powershell -ExecutionPolicy Bypass -File .\tools\package-release.ps1
```

Distribution files are written to `artifacts/release`. The user ZIP contains exactly three files: `DesktopCat.exe`, `config.ini` and `使用说明.txt` (the Chinese user guide). The SHA-256 file is kept outside the ZIP. Diagnostic commands write test reports only when explicitly run, under `artifacts`.

To export a source archive for review:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\export-source.ps1
```

[SOURCE-FILES.txt](SOURCE-FILES.txt) explicitly lists the files included in the archive. The output is written to `artifacts/source`. Update the manifest when adding source files intended for distribution. All three language versions of this README are included. The source archive and the three-file user package serve different purposes.

[The Windows CI workflow](.github/workflows/ci.yml) builds, checks and packages the application on GitHub. It does not automatically publish releases and requires no website deployment credentials. The basic Windows checks have passed on GitHub runners; desktop interaction and audio-buffer checks have also been performed locally.

## Repository layout

```text
src/                    Application source and diagnostic entry points
Assets/                 Cat icon, selected sounds and text needed for the build
tools/                  Packaging, validation and icon-generation tools
docs/USER-GUIDE.txt      User-guide template (Chinese)
VERSION                 Single source of the version number
config.example.ini      Default configuration without personal settings
README.md               English introduction and build instructions
README.zh-CN.md         Chinese introduction and build instructions
README.ja.md            Japanese introduction and build instructions
THIRD-PARTY-NOTICES.md   Sound credits and source links
SOURCE-STATUS.md         Source provenance and licensing status
```

## Inspiration and credits

An independent, unofficial cat adaptation inspired by [samperson's Desktop Goose](https://samperson.itch.io/desktop-goose). No endorsement by the original creator is claimed. Third-party sounds are credited in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md); the final licensing scope for the application code and icon remains to be confirmed.

Bug reports and feature suggestions are welcome through [Issues](https://github.com/KaMiYiLu/desktop-cat/issues). Include the application version, the action that triggered the problem and any error message. For contributions, see [CONTRIBUTING.md](CONTRIBUTING.md) (Chinese).
