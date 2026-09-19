# ====== [Episodes] BUSINESS LOGIC ======

Status: Planned (stage S3)

## Purpose

`Episodes` owns:
- Episodes: one form submission is one sortie (`Mission`) with a target (`TargetEngagement`) and munitions (`MunitionUse`).
- Import of raw source rows into the domain model (matching directory names, date, status) with deduplication: a submission is never imported twice (stable `SourceRowKey`).
- Record statuses (`RecordStatus`). A wrong submission is cancelled, not deleted.

**Not here:**
- Reading the sheet, OAuth, computing the row key: `GoogleSheetsIntegration` (only the `IResponseSource` contract is used).
- Directories: `ReferenceData`. Selecting episodes for a report: `Reporting` (Episodes only give `CountsTowardsReport`).
- Writing statuses back to the sheet: `GoogleSheetsIntegration`.

## Entities

| Entity | Basic Fields | Description | Invariants |
|---|---|---|---|
| `Mission` *(extends `Entity`, Aggregate Root)* | `SourceRowKey`, `Date`, `SubmittedAt`, `OperatorEmail`, `Settlement?`, `Status`, `LastSyncedMarker?`, `Targets` | One form submission. | - `SourceRowKey` is required, unique, stable, independent of row position.<br>- `Date` is the sortie date, or the submission day if the form field is empty.<br>- `Status` is `Active` by default.<br>- `LastSyncedMarker` is the status marker last equal to the sheet.<br>- `CountsTowardsReport` only when `Status == Active`.<br>- Deleting a mission cascades to targets and munitions. |
| `TargetEngagement` *(internal Entity, owned by `Mission`)* | `UavType?`, `TargetType?`, `UavCount`, `Munitions` (`MunitionUse`, internal Entity with `MunitionType?`) | One engaged target and its munitions. | - `UavCount >= 0` and below a bound (value in SRS), otherwise `NeedsAttention`.<br>- Import creates one target per mission.<br>- An unknown munition name is skipped and goes to `UnknownValues`. |

Dedup key `SourceRowKey` identifies a submission, not a row position, so sorting or deleting rows in the sheet does not cause re-import. Format: `g1:<first 32 hex chars of SHA-256 of the normalized row content>`. Content is timestamp text, operator email, settlement, UAV type, target type, count and munitions, trimmed and case-normalized. The status column is not hashed. Identical rows get a counter (`#2`, `#3`). The demo source uses `demo|N`.

## Status Lifecycle

`RecordStatus` values: `Active` (empty marker in the sheet, goes into reports), `Cancelled` (marker `СКАСОВАНО`) and `NeedsAttention` (marker `ПОТРЕБУЄ УВАГИ`). The last two are not in reports.

- Start: status comes from the source marker (empty or unknown means `Active`). An `Active` row whose UAV type or target type is not in directories, or whose count or date is out of bounds, becomes `NeedsAttention`. A missing settlement or munition does not cause it.

| From | User | System |
|---|---|---|
| `Active` / `Cancelled` | -> `Cancelled` / -> `Active` (toggle) | - |
| `NeedsAttention` | -> `Cancelled`, -> `Active` after assigning values | -> `Active` when all values match after directories are updated |

- A status changed in the sheet is accepted on sync if the local status did not change since `LastSyncedMarker`. If both changed, it is a conflict: the local status stays and the user sees a warning.

## Import flow
`ImportService.ImportAsync(IResponseSource)`:
1. Read raw rows (`RawResponseRow`) and imported keys, index directories by normalized `ShortName`.
2. For each row: a known key is skipped (status reconciliation is applied). Otherwise find directory items, collect unknown values, choose the status, create one target and one `MunitionUse` per known munition, create the mission.
3. Save all new missions and status updates in one transaction. Return an `ImportResult` summary.

## Value Objects

None. `RecordStatus` is an enum with `Parse`, `ToMarker`, `ToDisplay` helpers.

## Domain Policies

| Domain Policy | Description |
|---|---|
| Import dedup | A row with an existing `SourceRowKey` is skipped, a unique index also blocks duplicates. |
| Cancel instead of delete | No deleting in the UI. Only `Active` records go into reports, a cancelled one is ignored until restored. |
| Write only changed statuses | Records with `Status.ToMarker() != LastSyncedMarker` go to the sheet, then the marker is updated. |
| Existing missions | Import changes only the status of existing missions. Out-of-bounds values give `NeedsAttention`, the row is not dropped. Sheet values are untrusted, never treated as markup or formulas. |

## Domain Services
- `ImportService`: raw rows to missions with dedup, status choice and reconciliation. `MissionStatusService`: user status changes with the transition check, re-matching after directory updates.

## Application Commands & Queries

Services, the repository and `MissionsViewModel` play this role.
- Commands: `SyncCommand` (directory merge, then import), `CancelCommand`, `ResolveCommand`, `ReResolveCommand`, `PublishCommand` (logic in `GoogleSheetsIntegration`).
- Queries: `GetAllAsync`, `GetForPeriodAsync(period, status)`, `GetByIdAsync`, `GetImportedKeysAsync`, `GetPendingStatusChangesAsync`.

## Infrastructure

- Tables `Missions` (unique `SourceRowKey`, index on `Date`), `TargetEngagements`, `MunitionUses`. Directory keys use `SetNull`, parent links use `Cascade`. EF Core migrations. Cancel, restore and sync events go to `IAuditSink` from S6.
