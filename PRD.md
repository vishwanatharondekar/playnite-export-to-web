# PRD: Playnite Library Exporter

## 1. Product Summary

**Product name:** Playnite Library Exporter
**Product type:** Playnite C# Generic Plugin
**Primary purpose:** Automatically export the user’s current Playnite game library to a local file whenever the Playnite library is updated, with configurable export formats and an additional manual export command available from the Playnite UI.

The plugin should use the official Playnite SDK/database API rather than directly parsing Playnite’s internal library files. The export should provide a stable list of games in the Playnite library, including each game’s name and Steam AppID when the Steam AppID can be confidently inferred from Playnite’s existing metadata. JSON should remain the default machine-readable format, with additional user-selectable outputs for plain text, Markdown, and CSV.

------

## 2. Background and Problem Statement

The user maintains a Playnite library containing games from Steam and other libraries. They want a reliable export file that can be consumed by other scripts, applications, documents, or spreadsheets, without manually exporting the library each time it changes.

Playnite already maintains the authoritative game library. The safest and most maintainable approach is to create a Playnite plugin that runs inside Playnite, accesses `PlayniteApi.Database.Games`, and writes an export file after Playnite finishes updating the library.

The plugin should avoid direct parsing of Playnite’s internal database/library files because those files are implementation details and may change. The plugin should also avoid external Steam matching in the MVP because title-based matching can create false positives.

------

## 3. Goals

### 3.1 Primary Goals

1. Export Playnite library games to a local file.
2. Automatically refresh the export after Playnite completes a library update.
3. Provide a manual “Export now” action from the Playnite UI.
4. Include the game name for every exported game.
5. Include a Steam AppID when it can be confidently inferred from Playnite metadata.
6. Use the official Playnite SDK/database API.
7. Make the output deterministic, stable, and easy for downstream tools to consume.
8. Provide basic settings for export path, automatic export behavior, selected output formats, and format-specific formatting.
9. Provide a user-selectable option to exclude games with a tag or genre named “visual novel” in Playnite metadata.
10. Log errors clearly without disrupting normal Playnite usage.

### 3.2 Secondary Goals

1. Include useful provider/source metadata for each game.
2. Include export metadata such as schema version, timestamp, total game count, filtered game count, selected output formats, and plugin version.
3. Support atomic file writes to avoid partial/corrupt exports.
4. Keep automatic exports quiet unless there is an error.
5. Show clear confirmation after manual export.
6. Minimize performance impact on Playnite startup and library updates.

------

## 4. Non-Goals for MVP

The MVP should **not** attempt to:

1. Query Steam’s public app list.
2. Fuzzy-match non-Steam games to Steam AppIDs.
3. Parse Playnite’s raw library/database files directly.
4. Provide a command-line interface.
5. Register custom `playnite://` URI commands.
6. Export cover images, icons, screenshots, trailers, or other media files.
7. Sync to cloud services directly.
8. Upload data to a web service.
9. Modify Playnite library data.
10. Act as a general-purpose backup tool.

These may be considered future enhancements.

------

## 5. Target User

The initial target user is an advanced Windows/Playnite user who wants to keep an automatically updated machine-readable game list for personal automation.

Secondary future users may include Playnite users who want structured or human-readable exports for dashboards, game recommendation tools, inventory comparisons, launchers, web apps, local AI workflows, notes, or spreadsheets.

------

## 6. Target Environment

### 6.1 Platform

- Windows 10 or Windows 11.
- Playnite Desktop mode.
- Current supported Playnite SDK for C# plugin development.
- Plugin implemented as a Playnite `GenericPlugin`.

### 6.2 Development Environment

- C#.
- Visual Studio or compatible .NET IDE.
- Playnite SDK package/reference.
- Use the Playnite extension structure with an `extension.yaml` manifest.
- Use the Playnite Toolbox utility if appropriate for generating the extension skeleton and packaging.

------

## 7. High-Level Design

The plugin should run inside Playnite and subscribe to Playnite lifecycle events.

### 7.1 Automatic Flow

1. Playnite starts.
2. Playnite updates one or more libraries, either automatically or manually.
3. Playnite fires the plugin’s library-updated lifecycle event.
4. The plugin schedules an export using the configured output formats.
5. The plugin reads a snapshot of `PlayniteApi.Database.Games`.
6. The plugin applies configured filters and serializes the library to each configured output format.
7. The plugin writes each output file atomically to the configured output path.
8. The plugin logs success or failure.

### 7.2 Manual Flow

1. User opens the Playnite main menu.
2. User chooses the plugin’s “Export library now” menu item.
3. Plugin immediately performs the export.
4. Plugin displays success/failure feedback to the user.
5. Plugin logs details.

### 7.3 Optional Startup Flow

The plugin may include a setting to export on Playnite startup. Recommended default behavior:

- If the export file does not exist yet, export once on startup.
- Otherwise, rely primarily on library update events.

This prevents the user from having no export file after first installation, while avoiding unnecessary exports on every launch.

------

## 8. Functional Requirements

### FR-001: Export All Games

The plugin must export all games visible to the Playnite database API.

The default behavior should be to include every game in the Playnite library, including installed, uninstalled, hidden, manually added, emulated, and third-party-library games.

Filtering must be disabled by default unless the user explicitly enables a filter setting.

------

### FR-001A: Optional Visual Novel Tag Exclusion

The plugin must provide a user-facing menu/settings option to exclude games with a tag or genre named “visual novel” in Playnite metadata.

Recommended behavior:

- Default: disabled.
- Matching should be case-insensitive.
- Matching should trim leading/trailing whitespace.
- The initial exact tag match should be `visual novel`.
- The filter should use Playnite tag and genre metadata, not game title, category, source, or description text.
- A game with multiple tags or genres should be excluded if any tag or genre matches `visual novel`.
- Games without matching tags or genres should remain included.
- The filter should apply consistently to automatic exports, manual exports, and all supported export formats.
- Export summary metadata should report how many games were excluded by filters when the output format supports metadata.

The setting label should be clear and direct, such as:

```text
Exclude games with tag/genre "visual novel"
```

------

### FR-002: Automatic Export After Library Update

The plugin must automatically export using the configured output formats when Playnite finishes a library update.

The automatic export should be triggered by Playnite’s library-updated lifecycle event.

Automatic export should be enabled by default.

The plugin should not display a success dialog for automatic exports by default. It should log success silently.

If automatic export fails, the plugin should log the error. User-facing error notifications may be enabled via a setting but should not be overly intrusive.

------

### FR-003: Manual Export Menu Item

The plugin must provide a manual export command in Playnite Desktop mode.

Recommended menu location:

```text
Extensions → Playnite Library Exporter → Export library now
```

The menu item should trigger the same export logic used by automatic export.

After manual export:

- On success, show a short success notification or dialog including the output path.
- On failure, show an error message with a brief explanation and direct the user to Playnite logs for details.

------

### FR-003A: Export Options Menu Items

The plugin must expose menu options in Playnite Desktop mode for the most common export controls.

Recommended menu structure:

```text
Extensions → Playnite Library Exporter → Export library now
Extensions → Playnite Library Exporter → Output formats → JSON
Extensions → Playnite Library Exporter → Output formats → Plain text (.txt)
Extensions → Playnite Library Exporter → Output formats → Markdown (.md)
Extensions → Playnite Library Exporter → Output formats → Spreadsheet (.csv)
Extensions → Playnite Library Exporter → Exclude games with tag/genre "visual novel"
```

Menu behavior:

- Output formats should behave like independent toggles, allowing one or more selected formats at the same time.
- JSON should be selected by default.
- The current selected output formats should be visibly indicated if Playnite menu APIs support checked states.
- The plugin must prevent the user from saving or applying a state with zero output formats selected.
- The visual novel exclusion option should behave like a toggle.
- Menu selections must persist to plugin settings.
- Menu selections must apply to both manual and automatic exports.
- If checked menu states are not supported by the Playnite API, the menu labels should still be clear and the settings UI should show the current values.

The settings UI should provide the same controls so users can review or change them outside the main menu.

------

### FR-004: Settings UI

The plugin must provide a settings UI through Playnite’s extension settings.

Recommended settings:

| Setting                                      | Type           | Default                                 | Description                                                  |
| -------------------------------------------- | -------------- | --------------------------------------- | ------------------------------------------------------------ |
| Enable automatic export after library update | Boolean        | true                                    | Export whenever Playnite finishes updating libraries         |
| Export once on startup if file is missing    | Boolean        | true                                    | Creates the initial export file after installation           |
| Export on every Playnite startup             | Boolean        | false                                   | Optional full startup export                                 |
| Output formats                              | Multi-select   | JSON selected                           | One or more of JSON, plain text (`.txt`), Markdown (`.md`), and CSV (`.csv`) |
| Exclude games with tag/genre "visual novel" | Boolean        | false                                   | Exclude games with a matching Playnite tag or genre from all exports |
| Output directory                             | Directory path | User documents or plugin data directory | Folder where export files are written                        |
| Output file base name                        | String         | `playnite-library`                      | Base name used for all selected export files                  |
| Pretty-print JSON                            | Boolean        | true                                    | Human-readable indentation for JSON exports                  |
| Include export metadata envelope             | Boolean        | true                                    | Use top-level object with metadata and games array for JSON exports |
| Include only core fields                     | Boolean        | false                                   | Future-compatible option; default should include useful metadata |
| Show notification after automatic export     | Boolean        | false                                   | Quiet by default                                             |
| Show notification after manual export        | Boolean        | true                                    | Confirm manual action                                        |

Settings should be validated before saving or before export.

Validation requirements:

- Output directory must be non-empty.
- Output file base name must be non-empty.
- At least one output format must be selected.
- Each selected output format must be one of JSON, plain text, Markdown, or CSV.
- Output file base name must not include a file extension.
- If the user enters an extension in the base name, the plugin should either warn or strip the known extension before saving.
- Generated output extensions: `.json`, `.txt`, `.md`, `.csv`.
- Plugin should create the output directory if it does not exist.
- Plugin should reject invalid path characters.
- Plugin should handle permission failures gracefully.

------

### FR-005: JSON Export File Format

JSON should be the default export format.

The default JSON export should use a top-level metadata envelope:

```json
{
  "schemaVersion": 1,
  "generatedAtUtc": "2026-06-05T00:00:00Z",
  "generator": {
    "name": "Playnite Library Exporter",
    "version": "1.0.0"
  },
  "playnite": {
    "databaseGameCount": 123
  },
  "export": {
    "totalGames": 123,
    "databaseGameCount": 123,
    "excludedGameCount": 0,
    "steamAppIdCount": 80,
    "formats": ["json"]
  },
  "games": [
    {
      "playniteId": "00000000-0000-0000-0000-000000000000",
      "name": "Example Game",
      "sortingName": "Example Game",
      "providerGameId": "123456",
      "pluginId": "00000000-0000-0000-0000-000000000000",
      "sourceId": "00000000-0000-0000-0000-000000000000",
      "sourceName": "Steam",
      "steamAppId": 123456,
      "steamAppIdSource": "playnite-provider-game-id",
      "steamAppIdConfidence": "exact"
    }
  ]
}
```

The plugin should keep the JSON schema stable. Future breaking changes should increment `schemaVersion`.

------

### FR-005A: Supported Export Formats

The plugin must allow the user to select one or more output formats:

| Format     | Extension | Intended use                                      |
| ---------- | --------- | ------------------------------------------------- |
| JSON       | `.json`   | Machine-readable structured automation            |
| Plain text | `.txt`    | Simple human-readable list                        |
| Markdown   | `.md`     | Notes, documentation, GitHub, and rendered preview |
| CSV        | `.csv`    | Spreadsheet tools and tabular analysis            |

The same game snapshot, sorting logic, Steam AppID inference, and configured filters must apply to all selected formats.

Valid examples:

- JSON only.
- CSV only.
- JSON plus plain text.
- JSON plus plain text, Markdown, and CSV.

Format-specific requirements:

- JSON should include the full metadata envelope by default.
- Plain text should use UTF-8 and include one game per line by default. Recommended line format: `Name [Steam AppID: 123456]`; omit the Steam AppID segment when unavailable.
- Markdown should use UTF-8 and include a concise heading, export summary, and a table of games.
- CSV should use UTF-8, include a header row, quote fields according to standard CSV rules, and use one row per exported game.
- CSV should include stable columns for the required game fields and should include recommended optional fields only when they can be represented predictably.
- Plain text and Markdown should be deterministic and suitable for version control diffs.
- Format-specific serialization errors should be logged with the affected output format.
- When multiple output formats are selected, the plugin should render and write each selected format during the same export operation.
- Each selected format should produce a separate file in the output directory using the configured base name and the format's extension.

Recommended CSV columns:

```text
playniteId,name,sortingName,providerGameId,pluginId,sourceId,sourceName,steamAppId,steamAppIdSource,steamAppIdConfidence,isInstalled,hidden,favorite,tags
```

------

### FR-006: Required Game Fields

Each exported game object must include:

| Field                  | Type                | Required | Description                                         |
| ---------------------- | ------------------- | -------- | --------------------------------------------------- |
| `playniteId`           | String GUID         | Yes      | Playnite’s internal game ID                         |
| `name`                 | String              | Yes      | Game title                                          |
| `sortingName`          | String or null      | Yes      | Playnite sorting name, if available                 |
| `providerGameId`       | String or null      | Yes      | Playnite provider/library game ID                   |
| `pluginId`             | String GUID or null | Yes      | Playnite library plugin ID associated with the game |
| `sourceId`             | String GUID or null | Yes      | Playnite source ID                                  |
| `sourceName`           | String or null      | Yes      | Playnite source name                                |
| `steamAppId`           | Integer or null     | Yes      | Steam AppID if confidently known                    |
| `steamAppIdSource`     | String or null      | Yes      | Method used to derive the Steam AppID               |
| `steamAppIdConfidence` | String              | Yes      | `exact`, `none`, or future values                   |

------

### FR-007: Recommended Optional Game Fields

The plugin should include these fields if they are readily available from the Playnite SDK and do not add meaningful complexity:

| Field              | Type                     | Description                                  |
| ------------------ | ------------------------ | -------------------------------------------- |
| `isInstalled`      | Boolean                  | Whether Playnite marks the game as installed |
| `hidden`           | Boolean                  | Whether the game is hidden                   |
| `favorite`         | Boolean                  | Whether the game is marked favorite          |
| `installDirectory` | String or null           | Install directory if available               |
| `platforms`        | Array of strings         | Platform names if available                  |
| `genres`           | Array of strings         | Genre names if available                     |
| `categories`       | Array of strings         | Category names if available                  |
| `tags`             | Array of strings         | Tag names if available                       |
| `lastActivity`     | String datetime or null  | Last played/activity timestamp if available  |
| `playtimeSeconds`  | Integer or null          | Playtime if available                        |
| `added`            | String datetime or null  | Date added if available                      |
| `modified`         | String datetime or null  | Date modified if available                   |
| `releaseDate`      | String or object or null | Release date if readily serializable         |

Optional fields should not block MVP completion if the SDK properties require more nuanced handling.

------

### FR-008: Steam AppID Detection Logic

The plugin must distinguish between:

1. Playnite’s internal game ID.
2. The provider/library `GameId`.
3. The Steam AppID.

For MVP, the plugin should set `steamAppId` only when the Steam AppID can be derived confidently from Playnite metadata.

Recommended MVP logic:

- If the game appears to be from Steam and `providerGameId` is numeric, set:
  - `steamAppId` = parsed integer provider game ID.
  - `steamAppIdSource` = `playnite-provider-game-id`.
  - `steamAppIdConfidence` = `exact`.
- Otherwise:
  - `steamAppId` = null.
  - `steamAppIdSource` = null.
  - `steamAppIdConfidence` = `none`.

The plugin should avoid title-based or fuzzy Steam matching in MVP.

Steam detection should be conservative. A false null is preferable to an incorrect Steam AppID.

Recommended Steam detection signals:

1. `sourceName` equals `Steam`, case-insensitive.
2. The importing plugin/source is identifiable as Steam.
3. `providerGameId` is a positive integer string.

Implementation should avoid hardcoding fragile assumptions when possible. If a known Steam plugin identifier is available from Playnite metadata or documentation, it may be used as an additional signal, but `sourceName == "Steam"` plus numeric provider ID is acceptable for MVP.

------

### FR-009: Deterministic Output Ordering

The exported `games` array must be sorted deterministically.

Recommended sorting:

1. Case-insensitive `sortingName` if present.
2. Case-insensitive `name`.
3. `playniteId`.

This makes diffs cleaner and avoids unnecessary churn in version-controlled exports.

------

### FR-010: Atomic File Writes

The plugin must avoid leaving a partially written export file.

Recommended behavior:

1. Serialize each selected output format into memory or a temporary file.
2. Write each output to a temporary file in the target directory.
3. Replace each target export file atomically or as close to atomically as practical on Windows.
4. If replacement fails for one format, preserve the previous export file for that format when possible.
5. Clean up stale temporary files when safe.

When multiple output formats are selected, each file write should be independently atomic. A failure writing one format should be logged clearly and reported after manual exports, including which formats succeeded and which failed.

------

### FR-011: Debounce Automatic Exports

The plugin should avoid excessive repeated exports during bursts of library changes.

Recommended debounce behavior:

- When an automatic export event occurs, schedule export after a short delay, such as 2–5 seconds.
- If another export-triggering event occurs during that delay, reset or coalesce the pending export.
- Never run two exports concurrently.
- Manual export should either:
  - Run immediately after any current export completes, or
  - Cancel/replace a pending automatic export and run now.

------

### FR-012: Concurrency and UI Responsiveness

The plugin must not freeze Playnite’s UI during normal use.

Recommended implementation approach:

- Capture the required Playnite game data safely.
- Perform serialization and file writing off the UI thread when appropriate.
- If Playnite SDK access must occur on the UI thread, take a lightweight snapshot first, then serialize/write in the background.
- Use a lock, semaphore, or single-flight export queue to prevent concurrent writes.

------

### FR-013: Logging

The plugin must log:

- Plugin startup.
- Settings load/save failures.
- Automatic export trigger.
- Manual export trigger.
- Export start.
- Export success, including output path, selected output formats, exported game count, excluded game count, and Steam AppID count.
- Export failure, including exception details.
- Validation failures.

Use Playnite’s logging facilities rather than writing a separate custom log file in MVP.

------

### FR-014: Error Handling

The plugin must handle common errors gracefully:

| Error                          | Expected behavior                                        |
| ------------------------------ | -------------------------------------------------------- |
| Output directory missing       | Create it if possible                                    |
| Invalid output path            | Show settings validation error or export failure message |
| Permission denied              | Log error and show clear message for manual exports      |
| File locked by another process | Log error and show clear message for manual exports      |
| Serialization error            | Log full exception                                       |
| Playnite API data missing/null | Export nulls where appropriate; do not crash             |
| Duplicate game names           | Preserve distinct `playniteId` entries                   |
| Non-numeric provider IDs       | Do not infer Steam AppID                                 |

Automatic export failures should not crash Playnite.

------

### FR-015: Privacy and Security

The plugin must operate locally.

MVP must not:

- Send library data over the network.
- Query external APIs.
- Upload logs.
- Require credentials.
- Modify game metadata.
- Delete user files except temporary files created by this plugin.

The exported data may include local install paths if optional fields are enabled. If install paths are included by default, the settings UI should make this clear.

Recommended default:

- Include local install path only if readily available and useful.
- Consider a setting to exclude local paths for privacy.

------

## 9. User Experience Requirements

### UX-001: Manual Export Discoverability

Manual export should be easy to find in Desktop mode:

```text
Extensions → Playnite Library Exporter → Export library now
```

Optional additional menu items:

```text
Extensions → Playnite Library Exporter → Open export folder
Extensions → Playnite Library Exporter → Open settings
```

### UX-002: Manual Export Feedback

After manual export success, show concise feedback:

```text
Playnite library exported successfully.

Formats: JSON, Markdown, CSV
Games exported: 123
Games excluded by filters: 0
Steam AppIDs found: 80
Outputs:
C:\Users\<User>\Documents\PlayniteExports\playnite-library.json
C:\Users\<User>\Documents\PlayniteExports\playnite-library.md
C:\Users\<User>\Documents\PlayniteExports\playnite-library.csv
```

After manual export failure, show concise feedback:

```text
Playnite library export failed.

Reason: Access to the output path was denied.
Please check the plugin settings and Playnite logs.
```

### UX-003: Automatic Export Quietness

Automatic exports should be silent by default.

The plugin may log success but should not interrupt the user after every library update unless the user enables automatic export notifications.

------

## 10. Settings Defaults

Recommended defaults:

```text
Automatic export after library update: Enabled
Export once on startup if missing: Enabled
Export on every startup: Disabled
Output directory: %USERPROFILE%\Documents\PlayniteLibraryExport
Output formats: JSON
Output file base name: playnite-library
Exclude games with tag/genre "visual novel": Disabled
Pretty-print JSON: Enabled
Include metadata envelope: Enabled
Show automatic export notifications: Disabled
Show manual export notifications: Enabled
Include local install paths: Enabled or configurable
```

If environment variable expansion is supported, paths like `%USERPROFILE%` should be expanded before validation. The plugin should derive output file names from the base name and selected formats, such as `playnite-library.json`, `playnite-library.txt`, `playnite-library.md`, and `playnite-library.csv`.

------

## 11. Data Model

### 11.1 JSON Export Envelope

| Field            | Type            | Description               |
| ---------------- | --------------- | ------------------------- |
| `schemaVersion`  | Integer         | Export schema version     |
| `generatedAtUtc` | String datetime | UTC timestamp             |
| `generator`      | Object          | Plugin name/version       |
| `playnite`       | Object          | Playnite-related metadata |
| `export`         | Object          | Export summary            |
| `games`          | Array           | Exported games            |

### 11.2 Generator Object

| Field     | Type   | Description    |
| --------- | ------ | -------------- |
| `name`    | String | Plugin name    |
| `version` | String | Plugin version |

### 11.3 Export Summary Object

| Field             | Type    | Description                                                  |
| ----------------- | ------- | ------------------------------------------------------------ |
| `totalGames`      | Integer | Number of exported games after filters                       |
| `databaseGameCount` | Integer | Number of games visible before filters                      |
| `excludedGameCount` | Integer | Number of games excluded by filters                         |
| `steamAppIdCount` | Integer | Number of games with non-null Steam AppID                    |
| `formats`         | Array of strings | Selected output formats, such as `json`, `txt`, `markdown`, and `csv` |
| `automatic`       | Boolean | Whether the export was automatic                             |
| `trigger`         | String  | `manual`, `library-updated`, `startup-missing-file`, or `startup` |

### 11.4 Game Object

Required fields:

```json
{
  "playniteId": "string-guid",
  "name": "string",
  "sortingName": "string-or-null",
  "providerGameId": "string-or-null",
  "pluginId": "string-guid-or-null",
  "sourceId": "string-guid-or-null",
  "sourceName": "string-or-null",
  "steamAppId": 123456,
  "steamAppIdSource": "playnite-provider-game-id",
  "steamAppIdConfidence": "exact"
}
```

For games without a Steam AppID:

```json
{
  "steamAppId": null,
  "steamAppIdSource": null,
  "steamAppIdConfidence": "none"
}
```

------

## 12. Technical Requirements

### TR-001: Plugin Type

Implement as a Playnite C# `GenericPlugin`.

### TR-002: Manifest

Include a valid `extension.yaml` manifest with:

- Unique extension ID.
- Name.
- Author.
- Version.
- Module DLL name.
- Type: `GenericPlugin`.
- Optional icon and links.

### TR-003: SDK Usage

Use the Playnite SDK to access:

- Plugin lifecycle events.
- Game database collection.
- Game model metadata.
- Main menu integration.
- Settings integration.
- Logging.

Do not read Playnite library files directly.

### TR-004: Export Serialization

Use reliable serializers compatible with the plugin’s target framework and Playnite environment.

Requirements:

- Stable property names.
- Null values included for required JSON fields.
- Pretty print setting for JSON.
- UTF-8 output.
- Safe handling of nullable values.
- No circular reference serialization from raw Playnite objects.
- Correct CSV escaping and quoting.
- Deterministic Markdown and plain-text rendering.

The plugin should map Playnite SDK objects into simple export DTOs before serialization, then render each selected output format from those DTOs.

### TR-005: Build and Packaging

The project should build into a Playnite extension package or loadable extension directory.

Deliverables:

- Source code.
- Project file.
- Manifest.
- Settings view files if needed.
- README.
- Build/package instructions.
- Example output files for supported formats, at minimum JSON and CSV.

### TR-006: No Direct Playnite Source Modification

Do not modify the Playnite source repository. This should be a standalone plugin project.

------

## 13. Acceptance Criteria

### AC-001: Plugin Loads

Given the plugin is installed in Playnite, when Playnite starts, the plugin loads without errors and appears in the extension settings list.

### AC-002: Manual Export Works

Given Playnite is open, when the user selects “Export library now,” then the plugin writes the configured export file or files and shows a success message.

### AC-003: Automatic Export Works

Given automatic export is enabled, when Playnite finishes updating the library, then the plugin writes updated export file or files without requiring manual user action.

### AC-004: Export Contains Expected Games

Given the user has N games in the Playnite library, when the plugin exports with no filters enabled, then the export contains N games.

Given the visual novel exclusion filter is enabled, when the plugin exports, then games with a Playnite tag or genre matching `visual novel` are excluded and other games remain included.

### AC-005: Steam AppID Detection Works for Steam Games

Given a Playnite game imported from Steam with a numeric provider `GameId`, when exported, then the game object contains that value as `steamAppId`, with `steamAppIdConfidence` set to `exact`.

### AC-006: Non-Steam Games Are Not Incorrectly Matched

Given a non-Steam game with no confident Steam metadata, when exported, then `steamAppId` is null and `steamAppIdConfidence` is `none`.

### AC-007: Output Is Deterministic

Given the same Playnite library state, when the export runs multiple times, then the games appear in the same order and the output is stable except for timestamp metadata.

### AC-008: Invalid Path Handling

Given the output path is invalid or inaccessible, when manual export is triggered, then the plugin shows an error and logs details without crashing Playnite.

### AC-009: Atomic Write Behavior

Given an existing valid export file, when an export fails during writing, then the previous export file should remain intact whenever practical.

### AC-010: Settings Persist

Given the user changes plugin settings and restarts Playnite, then the settings persist and are applied to subsequent exports.

### AC-011: Output Format Selection Works

Given the user selects one output format from the plugin menu or settings, when an export runs, then the plugin writes only that selected format with the expected file extension and deterministic content.

Given the user selects multiple output formats from the plugin menu or settings, when an export runs, then the plugin writes one file per selected format using the configured base name and each format's expected extension.

Given the user attempts to deselect every output format, when the setting is saved or applied, then the plugin prevents the zero-output state and shows a clear validation message.

### AC-012: Visual Novel Exclusion Menu Works

Given the user toggles “Exclude games with tag/genre "visual novel"” from the plugin menu, when an export runs, then the toggle state is applied to the export and persists in settings.

------

## 14. Testing Plan

### 14.1 Unit-Level Tests, If Practical

Test pure logic functions separately where possible:

- Steam AppID inference.
- Export DTO mapping.
- Path validation.
- Sorting.
- JSON serialization.
- Plain text rendering.
- Markdown rendering.
- CSV serialization and escaping.
- Visual novel tag filtering.
- Output format selection/default extension behavior.
- Settings defaulting/migration.

### 14.2 Manual Integration Tests

Test with a Playnite library containing:

1. Several Steam games.
2. Several GOG/Epic/Xbox/manual games.
3. Hidden games.
4. Installed games.
5. Uninstalled games.
6. Games with duplicate or similar names.
7. Games with missing provider IDs.
8. Games with non-numeric provider IDs.
9. Games with special characters in names.
10. Large library, ideally 1,000+ games if available.
11. Games tagged or genre-labeled `visual novel`, `Visual Novel`, and unrelated similar values.
12. Games with multiple tags or genres where one value is `visual novel`.

### 14.3 Export Trigger Tests

Verify export occurs after:

- Manual menu action.
- Manual library update.
- Automatic Playnite library update.
- Startup when file is missing, if enabled.
- Startup always, if setting enabled.

### 14.4 Export Format Tests

Verify output format selection:

- JSON only writes only `.json`.
- CSV only writes only `.csv`.
- Selecting all formats writes `.json`, `.txt`, `.md`, and `.csv`.
- Each generated file uses the configured base name and expected extension.
- Uses deterministic game ordering.
- Applies the visual novel exclusion filter when enabled.
- Includes Steam AppID data when available.
- Handles commas, quotes, line breaks, and non-ASCII game names correctly.
- Produces useful output for an empty filtered result set.

### 14.5 Failure Tests

Verify behavior when:

- Output directory does not exist.
- Output directory cannot be created.
- Output file is locked.
- Output path is invalid.
- User lacks write permission.
- Serialization encounters unexpected nulls.
- No output formats are selected.
- Output file base name includes an extension or invalid path characters.

------

## 15. Performance Requirements

The plugin should handle large Playnite libraries without noticeable UI degradation.

Targets:

- For a library of 5,000 games, export should complete within a few seconds on a modern PC.
- Manual export may briefly show progress or block the command, but should not freeze Playnite for an extended period.
- Automatic export should be debounced and non-intrusive.
- Plugin should not repeatedly serialize the library during a burst of changes.

------

## 16. Compatibility Requirements

The plugin should target the current Playnite SDK and supported Playnite release.

Compatibility goals:

- Current Playnite 10-compatible SDK.
- Windows 10/11.
- Desktop mode support for menu items.
- Should not depend on a specific installation path.
- Should work with installed and portable Playnite installations.

The implementation should avoid hardcoded assumptions about the user’s Playnite data directory.

------

## 17. README Requirements

The repository should include a README with:

1. Project purpose.
2. Installation instructions.
3. Build instructions.
4. Playnite version/SDK requirements.
5. How to configure output path.
6. How to select one or more output formats: JSON, plain text, Markdown, and/or CSV.
7. How to exclude games tagged `visual novel`.
8. How to manually export.
9. When automatic export runs.
10. Explanation of Steam AppID detection.
11. Example outputs, including JSON and CSV.
12. Troubleshooting section.
13. Limitations and future enhancements.

Suggested troubleshooting topics:

- Plugin does not appear.
- Export file is not created.
- Permission denied writing export file.
- Steam AppIDs missing for non-Steam games.
- Automatic export does not appear to run.
- Output format or generated file extension is not what the user expected.
- Games with a `visual novel` tag or genre are still present because the metadata text does not exactly match.
- Where to find Playnite logs.

------

## 18. Future Enhancements

These should not be implemented in MVP unless requested later.

### FE-001: Command-Line or URI Trigger

Add a custom `playnite://` URI command to trigger export externally.

Example future behavior:

```text
playnite://library-exporter/export
```

This would allow Windows Task Scheduler, PowerShell, or another app to request an export while still using the Playnite plugin as the authority.

### FE-002: Steam AppID Enrichment for Non-Steam Games

Add optional Steam catalog matching for games imported from non-Steam libraries.

Requirements for future version:

- Download/cache Steam app list.
- Match by exact normalized title first.
- Provide confidence scoring.
- Never overwrite exact Playnite Steam metadata.
- Mark fuzzy matches as `probable` or `candidate`, not `exact`.
- Optionally export multiple candidates.
- Allow user to disable external network access.

### FE-003: Rich Spreadsheet Export

Add optional `.xlsx` export with formatted tables, filters, and multiple worksheets.

### FE-004: Export Only Changed Games

Add incremental export metadata or diff export.

### FE-005: Export on Game Add/Edit/Delete Events

Listen to lower-level database collection changes and export after debounced changes, not only after full library updates.

### FE-006: UI Status Panel

Add a small settings/status panel showing:

- Last export time.
- Last export path.
- Last export result.
- Games exported.
- Steam AppIDs found.

### FE-007: Multiple Export Profiles

Support multiple export targets, such as:

- Full JSON.
- Minimal JSON.
- CSV.
- Filtered Steam-only export.

------

## 19. Suggested Implementation Phases

### Phase 1: Skeleton Plugin

- Create C# GenericPlugin project.
- Add manifest.
- Confirm plugin loads.
- Add logger.
- Add basic settings object.
- Add manual menu item that logs when clicked.

### Phase 2: Core Export

- Read games from Playnite API.
- Map games to DTOs.
- Apply configured filters.
- Serialize each selected output format.
- Write to configured path.
- Add manual success/failure UI.

### Phase 3: Automatic Export

- Hook library-updated lifecycle event.
- Add debouncing/single-flight protection.
- Add startup-missing-file export behavior.
- Add quiet logging.

### Phase 4: Settings UI

- Add settings view.
- Add validation.
- Persist settings.
- Support output path, output format selection, visual novel tag exclusion, and format-specific options.
- Add menu options for output format toggles and visual novel tag exclusion.

### Phase 5: Robustness

- Atomic writes.
- Better error handling.
- Edge-case handling for null fields.
- Large-library testing.
- README and example output.

### Phase 6: Polish

- Add “Open export folder.”
- Add “Open settings.”
- Add optional notification settings.
- Package as `.pext` if desired.

------

## 20. Codex Implementation Instructions

When implementing this PRD:

1. Build a standalone Playnite C# Generic Plugin.
2. Do not modify Playnite source code.
3. Do not parse Playnite’s internal database/library files.
4. Use Playnite SDK APIs for all game data access.
5. Keep MVP fully local with no network calls.
6. Make Steam AppID detection conservative.
7. Prefer null Steam AppID over an incorrect Steam AppID.
8. Write exports atomically.
9. Avoid blocking the Playnite UI.
10. Add useful logging.
11. Include README and example outputs.
12. Keep the code organized into clear components:
    - Plugin entry point.
    - Settings model.
    - Settings view/model.
    - Export service.
    - Game DTO mapper.
    - Steam AppID inference helper.
    - Visual novel tag filter.
    - Format renderers.
    - File writer.
13. Include comments only where they clarify non-obvious behavior.
14. Avoid overengineering the MVP.
15. Keep future enhancements out of MVP unless specifically requested.

------

## 21. Open Questions

These can be decided during implementation:

1. Should local install paths be included by default?
2. Should hidden games be included by default? Recommended: yes.
3. Should the default output folder be Documents, the Playnite user data folder, or the plugin data folder?
4. Should the plugin export on every startup or only when the output file is missing? Recommended: only when missing.
5. Should manual export show a dialog or a Playnite notification? Either is acceptable; prefer the least intrusive standard Playnite UI pattern.
6. Should the plugin include genre/category/tag metadata in MVP? Recommended: include if easy and safe; otherwise defer.
7. Should the plugin include platform metadata in MVP? Recommended: include if easy and safe; otherwise defer.
8. Should future filter matching support synonyms or variants such as `visual-novel`, or should MVP remain exact to `visual novel` only? Recommended MVP: exact tag match only.

------

## 22. MVP Definition of Done

The MVP is complete when:

1. The plugin loads successfully in Playnite.
2. The user can configure an output path.
3. The user can manually export the library from Playnite’s menu.
4. The plugin automatically exports after Playnite finishes updating the library.
5. The user can select one or more output formats from JSON, plain text, Markdown, and CSV from the plugin menu or settings.
6. The user can enable or disable exclusion of games tagged `visual novel` from the plugin menu or settings.
7. With filters disabled, the export contains all Playnite games.
8. With the visual novel exclusion filter enabled, games tagged or genre-labeled `visual novel` are excluded from all supported export formats.
9. Each exported game contains at least:
   - Playnite ID.
   - Name.
   - Sorting name.
   - Provider game ID.
   - Plugin ID.
   - Source ID.
   - Source name.
   - Steam AppID if confidently known.
10. Steam games imported by the Steam library plugin receive correct Steam AppIDs when provider IDs are numeric.
11. Non-Steam games are not falsely assigned Steam AppIDs.
12. The export file is written atomically.
13. Errors are logged and do not crash Playnite.
14. The repository includes README, build instructions, and example outputs.
