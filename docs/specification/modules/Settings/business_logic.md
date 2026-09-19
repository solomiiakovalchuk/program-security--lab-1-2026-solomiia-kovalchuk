# ====== [Settings] BUSINESS LOGIC ======

Status: Planned (stage S3, UI in S5)

## Purpose
`Settings` owns:
- Permanent application settings (`AppSettings`): unit data for the report template, font, data source (demo or Google), spreadsheet id and sheet names, output folder, default signer.
- Saving and loading `settings.json` (`SettingsStore`), with a fallback to defaults and a message to the user.
- One rule for the output folder (`IOutputFolderProvider`) used by all consumers.
- The Settings tab (`SettingsViewModel`): edit, save, check connection, rebuild template, open folders.

**Not here:**
- Google credentials and token: `GoogleSheetsIntegration`. Settings keep only the spreadsheet id and sheet names.
- Rebuilding the template: `Documents`. Connection check and "Forget token": `GoogleSheetsIntegration`. Settings only provide the buttons.
- Passwords and roles: `Identity`. Use of order, subunit and signer in the report: `Reporting`.

## Entities
| Entity | Basic Fields | Description | Invariants |
|---|---|---|---|
| `AppSettings` *(POCO, JSON)* | see below | The single settings object, read through `ISettingsProvider`. | - Not an `Entity`, not stored in the database.<br>- Every property has a default, so a missing file still gives a valid configuration.<br>- Defaults are fictional placeholders (unit `А0000`).<br>- Contains no secrets.<br>- Validated before saving. |

Fields (defaults in brackets):
- Template data: `Classification`, `Addressee`, `Resolution`, `ResolutionRank`, `ResolutionName`, `SubunitText`, `OrderDefault`, `FontName` (Times New Roman), `FontSize` (14, allowed range in SRS, for example 8-72).
- `DefaultSignerEmail` (empty, chosen from People), `SpreadsheetId` (empty), `ResponsesSheet`, `DirectoriesSheet`, `PeopleSheet` (default sheet names).
- `OutputFolder` (empty, then `%AppData%\Рапорти\Рапорти` is used), `UseDemoData` (true).

## Save and apply flow
1. At startup `SettingsStore.Load()` reads `%AppData%\Рапорти\settings.json`. A missing file gives defaults. A broken JSON is logged, the file is renamed to `settings.json.bad`, defaults are used and the user sees a message. Settings are never reset silently.
2. Editing happens in a draft (`SettingsDraft`) and does not affect generation until saved. The tab shows "unsaved changes" and asks before leaving.
3. "Save" validates, writes the file atomically (temporary file and replace) and publishes the new settings (`SettingsChanged`). Saving also happens before "Rebuild template" and after a successful connection check.
4. Template parts (classification, addressee, resolution, font) reach the template file only after "Rebuild template". `SubunitText` and the order are used on every generation.

## Value Objects
None.

## Domain Policies
| Domain Policy | Description |
|---|---|
| Fallback to defaults | Broken JSON or `null`: rename to `.bad`, log, tell the user, use defaults. The app does not crash. |
| Validation | `SettingsValidator` before saving: `OutputFolder` empty or a writable path, `FontSize` in range, sheet names non-empty, `SpreadsheetId` normalized (a pasted URL is cut to the id). Errors are shown at the field, saving is blocked. |
| Sheet names after check | After diagnostics, found sheet names are written to settings and saved. |
| Demo mode | Connection check and publish are available only when `UseDemoData` is false. |
| Default output folder | `IOutputFolderProvider` returns `OutputFolder`, or `<data folder>\Рапорти` if empty, and creates it. Everyone uses this provider. |
| Saved settings only | Consumers see only saved settings, the draft is a separate object. |
| Atomic write | A failure never leaves an empty or cut file. |
| Access control | Changing settings, rebuilding the template and "Forget token" are for `Administrator` only. Changes are audited (names of changed fields, not values) from S6. |

## Domain Services
- `SettingsStore` *(`ISettingsStore`)*: `Load()` and `Save()`, creates the folder, atomic write, backup of a broken file.
- `OutputFolderProvider` *(`IOutputFolderProvider`)*: returns (and creates) the output folder by the single rule.
- `SettingsValidator` *(internal - no UC)*: validates `AppSettings` fields.

## Application Commands & Queries
`SettingsViewModel` plays the use-case role.
- Commands: `SaveCommand`, `RebuildTemplateCommand` (save, then rebuild), `OpenTemplateCommand`, `OpenDataFolderCommand`, `CheckConnectionCommand`, `ForgetTokenCommand`.
- Queries: `SettingsStore.Load`, `SourceDescription` (demo data or Google Sheet), `CredentialsStatus` (does `credentials.json` exist).

## Infrastructure
Files in the data folder `%AppData%\Рапорти\`:

| File | Owner | Purpose |
|---|---|---|
| `settings.json` (+ `.bad` after a fallback) | Settings | `AppSettings` as open JSON, no secrets. |
| `reports.db` | ReferenceData, Episodes, Reporting (later Identity, Audit) | SQLite database, EF Core migrations. |
| `template.docx` | Documents | Report template and backups. |
| `credentials.json`, `token-rw\` | GoogleSheetsIntegration | OAuth client and token cache (DPAPI from S6). |
| `error.log` | UI | Technical log of unhandled exceptions, rotated by size, no secrets. |
| `Рапорти\` | Reporting, Documents | Generated `.docx` files. |

`AppSettings` is serialized with `System.Text.Json`.
