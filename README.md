# Playnite Library Web

A [Playnite](https://playnite.link/) extension that publishes your game library as a web page that stays up to date: browse, filter and sort by store, genre, tags and play stats, on any device, and share the link.

Based on [Playnite Library Exporter](https://github.com/Dawnflare/PlayniteLibraryExport) by Dawnflare (MIT), and still includes its local JSON, plain text, Markdown and CSV exports.

The website lives in a separate repository ([playnite-library](https://github.com/vishwanatharondekar/playnite-library)).

## Getting started

1. Install the extension (see [Install](#install)) and restart Playnite.
2. Open `Extensions -> Playnite Library Web -> Link web library account...`, or click **Link account...** in the extension's settings.
3. A browser window opens. Sign in, choose your handle the first time, check the code matches the one Playnite shows, and click **Link**.
4. Your library uploads within a few seconds and is public at `https://<server>/u/<handle>`.

Each PC links separately; manage linked PCs, your handle and search engine visibility from **Manage on website**.

## What gets uploaded, and when

Uploads happen automatically (unless turned off in settings):

| When | Delay |
| --- | --- |
| Playnite starts | 5 s |
| Playnite finishes a library update | 5 s |
| A game session ends (playtime, last played) | 10 s |
| Games are edited, added or removed (tags, favourites, hiding, …) | 30 s after the last change |

Bursts of changes upload once, and unchanged libraries are not re-sent. `Upload to web library now` uploads immediately.

- **Uploaded:** each game's name, store and store ID, platforms, genres, tags, categories, playtime, last played, added/modified dates, release date and installed/favourite/hidden flags.
- **Never uploaded:** install folders.
- **Not uploaded by default:** hidden games (`Include hidden games` in settings).
- **Not uploaded when the visual novel filter is on:** games with a tag or genre named `visual novel`.

The link is stored per Windows user in the extension's data folder, encrypted with Windows DPAPI. `Unlink this PC` revokes it on the server too.

## Configuration

| Setting | Default |
| --- | --- |
| Upload automatically | On |
| Include hidden games | Off |
| PC name (shown on the website) | The Windows computer name |
| Server | `https://playnite-library.vercel.app` |

The default server is `WebConfig.DefaultServerUrl` in [`WebConfig.cs`](PlayniteLibraryExporter/WebConfig.cs); change it there when the site moves to its own domain. Users can also override it in settings (linking again is required after a change).

The local file export settings (formats, folder, automatic export after library updates, notifications) work as in Playnite Library Exporter; exports go to `%USERPROFILE%\Documents\PlayniteLibraryExport` by default.

## Install

Download the `.pext` from the [latest release](https://github.com/vishwanatharondekar/PlayniteLibraryWeb/releases/latest), close Playnite, and double-click it.

To install a build of your own instead, either:

- Pack it with `Toolbox.exe pack` and double-click the `.pext` file, or
- Copy `PlayniteLibraryExporter\bin\Release\*` into `%AppData%\Playnite\Extensions\PlayniteLibraryWeb_6470f7f6-0f8e-4f44-a3f9-86c1cc82028f` (for a portable Playnite, `Playnite\Extensions\…` next to `Playnite.DesktopApp.exe`), so that `extension.yaml` and `PlayniteLibraryWeb.dll` sit directly inside that folder.

Playnite Library Web has its own add-on id, so it can be installed next to Playnite Library Exporter.

## Developer build

A .NET Framework 4.6.2 WPF project; it builds on Windows only. It references the Playnite SDK at `Playnite\Playnite.SDK.dll` in the repository root (a portable Playnite copy, git-ignored).

```powershell
& 'C:\Program Files (x86)\Microsoft Visual Studio\18\BuildTools\MSBuild\Current\Bin\amd64\MSBuild.exe' `
  'PlayniteLibraryExporter\PlayniteLibraryExporter.sln' `
  /t:Build `
  /p:Configuration=Release

.\scripts\Install-Portable.ps1 -Configuration Release   # copy into the portable Playnite
.\Playnite\Toolbox.exe pack .\PlayniteLibraryExporter\bin\Release .\dist
```

## Releasing

The **Build** workflow builds every push on Windows and uploads the `.pext` as an artifact. To publish a release:

1. Bump `Version` in `PlayniteLibraryExporter/extension.yaml` (and `AssemblyVersion` in `Properties/AssemblyInfo.cs`).
2. Add the version to `installer.yaml`, with `PackageUrl` pointing at `releases/download/v<version>/PlayniteLibraryWeb_6470f7f6-0f8e-4f44-a3f9-86c1cc82028f_<version with underscores>.pext`.
3. Commit, then tag and push: `git tag v<version> && git push origin v<version>`. The workflow attaches the `.pext` to a GitHub release.

`installer.yaml` is the manifest for Playnite's add-on update system; register this URL in the Playnite add-on database:

```text
https://raw.githubusercontent.com/vishwanatharondekar/PlayniteLibraryWeb/main/installer.yaml
```

## Troubleshooting

- **Linking says the code expired:** codes last 10 minutes. Start again from the settings.
- **"This PC was unlinked":** the PC was unlinked from the website, or the server changed. Link again.
- **Uploads fail:** check `Extensions -> Playnite Library Web` errors in Playnite's notifications and `extensions.log` in the Playnite folder.
- **Plugin does not appear:** confirm `extension.yaml` and `PlayniteLibraryWeb.dll` are directly inside the extension folder.

## Examples

See `examples\playnite-library.example.json` and `examples\playnite-library.example.csv` for the file export format.
