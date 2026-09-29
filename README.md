# DFCMAD

**DFCMAD** is **Don't Fucking Change My Audio Device**: a Windows tray utility that keeps your selected default output and input audio devices enforced.

The app watches Windows Core Audio endpoint notifications, device state changes, default-device changes, sleep/resume, and display changes. When Windows or another app changes the default audio device, DFCMAD sets the configured preferred device back.

## Requirements

- Windows 10 or Windows 11
- .NET 10 SDK to build
- No administrator rights required

## Build

```powershell
dotnet build .\DFCMAD.sln
```

## Run

```powershell
dotnet run --project .\src\DFCMAD.App\DFCMAD.App.csproj
```

On first run, the bottom-right setup panel opens. Later launches stay in the tray unless another instance asks the running app to show the panel.

## Test

```powershell
dotnet test .\src\DFCMAD.Tests\DFCMAD.Tests.csproj
```

The tests cover settings persistence and corrupt-file recovery, enforcement decisions with a fake audio device service, startup registration logic, and diagnostics output. They do not change the real Windows default audio devices.

## Publish Single Executable

```powershell
.\build\publish.ps1
```

Equivalent command:

```powershell
dotnet publish .\src\DFCMAD.App\DFCMAD.App.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:EnableCompressionInSingleFile=true `
  -p:DebugType=None `
  -p:DebugSymbols=false
```

The published executable is under `artifacts\publish\win-x64`.

## Settings And Logs

- Settings: `%AppData%\DFCMAD\settings.json`
- Logs: `%AppData%\DFCMAD\Logs`

Settings are written atomically. If the settings file is corrupt, DFCMAD backs it up with a `.bak` suffix and creates a clean default settings file.

## Windows Audio Caveats

Windows exposes public Core Audio APIs for enumerating endpoints and receiving notifications, but it does not provide a supported public .NET API for setting the system default endpoint. DFCMAD uses NAudio's WASAPI/Core Audio wrappers for stable endpoint enumeration and notifications, and isolates the known `IPolicyConfig` COM approach in `DFCMAD.WindowsAudio.PolicyConfig` behind an interface so the enforcement engine and UI do not depend on that implementation detail.

DFCMAD sets the configured endpoint for `Console`, `Multimedia`, and `Communications` roles by default. If the preferred device is missing, unplugged, disabled, or not present, DFCMAD does not pick a random replacement; it waits and re-applies the selected endpoint when it returns.

## Manual Verification

1. Run the app and choose a preferred output and input device.
2. Change the Windows default playback or recording device in Settings.
3. Confirm DFCMAD changes it back after the debounce delay.
4. Unplug or disable the preferred device and confirm the UI reports it as unavailable without crashing.
5. Reconnect or re-enable the device and confirm DFCMAD re-applies it.
6. Toggle **Start with Windows** and verify `HKCU\Software\Microsoft\Windows\CurrentVersion\Run\DFCMAD`.
7. Open **Diagnostics** from the tray menu and confirm current defaults, available devices, recent enforcement events, and log location are shown.
