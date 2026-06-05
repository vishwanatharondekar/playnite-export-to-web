# Playnite Library Exporter

Playnite Library Exporter is a Playnite Generic Plugin that exports the current Playnite game library to local files.

Supported output formats:

- JSON (`.json`)
- Plain text (`.txt`)
- Markdown (`.md`)
- CSV (`.csv`)

JSON is enabled by default. Users can enable one or more output formats at the same time from the plugin menu or settings.

## Quick Install

Use the prebuilt package in this repository:

```text
dist\PlayniteLibraryExporter_7d089a0e-b862-44df-bca8-df3dc13165ee_1_0_0.pext
```

The matching SHA-256 checksum is stored next to it:

```text
dist\PlayniteLibraryExporter_7d089a0e-b862-44df-bca8-df3dc13165ee_1_0_0.sha256
```

Recommended installation:

1. Close Playnite.
2. Double-click the `.pext` file above.
3. Allow Playnite to install the extension.
4. Start or restart Playnite.
5. Open `Extensions -> Playnite Library Exporter`.
6. Choose `Export library now`, or open settings to configure automatic exports.

If double-click installation is not associated with Playnite on your system, use the manual install method below.

## Manual Install

Manual install does not require building the project.

1. Close Playnite.
2. Create this folder inside your Playnite extensions directory:

   ```text
   Extensions\PlayniteLibraryExporter_7d089a0e-b862-44df-bca8-df3dc13165ee
   ```

3. Open the prebuilt package in `dist`. A `.pext` file is a ZIP-compatible archive.
4. Extract the package contents into the folder from step 2.
5. Confirm the folder contains these files directly:

   ```text
   extension.yaml
   PlayniteLibraryExporter.dll
   icon.png
   Localization\
   ```

6. Start Playnite.
7. Confirm `Extensions -> Playnite Library Exporter` appears in the main menu.

For the portable Playnite instance included in this repository, the final install folder is:

```text
Playnite\Extensions\PlayniteLibraryExporter_7d089a0e-b862-44df-bca8-df3dc13165ee
```

## First Export

After installation:

1. Open Playnite.
2. Select `Extensions -> Playnite Library Exporter -> Export library now`.
3. The default output is written to:

   ```text
   %USERPROFILE%\Documents\PlayniteLibraryExport\playnite-library.json
   ```

If your Windows Documents folder is redirected to OneDrive, the file may appear under:

```text
%USERPROFILE%\OneDrive\Documents\PlayniteLibraryExport
```

## Settings

Open plugin settings from either:

```text
Extensions -> Playnite Library Exporter -> Open settings
```

or Playnite's add-on/settings UI.

Default settings:

- Automatic export after library update: enabled
- Export once on startup if output file is missing: enabled
- Export on every startup: disabled
- Output formats: JSON
- Output directory: `%USERPROFILE%\Documents\PlayniteLibraryExport`
- Output file base name: `playnite-library`
- Exclude games with tag/genre `visual novel`: disabled
- Pretty-print JSON: enabled

When multiple formats are selected, the plugin writes one file per format, such as:

```text
playnite-library.json
playnite-library.txt
playnite-library.md
playnite-library.csv
```

## Features

- Manual export from `Extensions -> Playnite Library Exporter -> Export library now`
- Automatic export after Playnite library updates
- Optional startup export
- Optional exclusion of games with a tag or genre named `visual novel`
- Conservative Steam AppID inference from Steam source plus numeric provider game ID
- Deterministic output ordering
- Atomic file writes per selected output file
- Local-only operation with no network calls

## Steam AppID Detection

Steam AppIDs are exported only when the game source is `Steam` and Playnite's provider game ID is a positive integer. Non-Steam games are left with `steamAppId: null`.

## Troubleshooting

- Plugin does not appear: confirm `extension.yaml` and `PlayniteLibraryExporter.dll` are directly inside the plugin extension folder, not nested one folder deeper.
- Export file is not created: check output directory permissions and Playnite logs.
- No output format can be saved: at least one output format must remain selected.
- Visual novels are still present: confirm the Playnite tag or genre is exactly `visual novel`, ignoring case and surrounding spaces.
- Steam AppIDs are missing: confirm the game source is Steam and the provider game ID is numeric.
- `.pext` does not open with Playnite: use the manual install method above.

## Developer Build

Building is only needed if you want to modify the plugin source. Normal installation can use the prebuilt `.pext` package in `dist`.

The project is a standalone .NET Framework plugin project. It references the portable Playnite SDK at `..\Playnite\Playnite.SDK.dll` from the plugin project directory.

```powershell
& 'C:\Program Files (x86)\Microsoft Visual Studio\18\BuildTools\MSBuild\Current\Bin\amd64\MSBuild.exe' `
  'PlayniteLibraryExporter\PlayniteLibraryExporter.sln' `
  /t:Build `
  /p:Configuration=Release
```

The compiled extension files are written to:

```text
PlayniteLibraryExporter\bin\Release
```

To copy a Release build into the included portable Playnite instance:

```powershell
.\scripts\Install-Portable.ps1 -Configuration Release
```

## Developer Package

Playnite recommends Toolbox for extension packaging. Toolbox is optional for development and runtime; the plugin project does not depend on it.

```powershell
.\Playnite\Toolbox.exe pack .\PlayniteLibraryExporter\bin\Release .\dist
```

## Playnite Add-on Database Publishing

This repository includes an installer manifest for Playnite's add-on update system:

```text
installer.yaml
```

The official Playnite add-on database entry should point its `InstallerManifestUrl` to:

```text
https://raw.githubusercontent.com/Dawnflare/PlayniteLibraryExport/main/installer.yaml
```

The installer manifest points to the prebuilt `.pext` package committed under `dist`.

## Examples

See `examples\playnite-library.example.json` and `examples\playnite-library.example.csv`.
