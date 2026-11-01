# ====== [GoogleSheetsIntegration] BUSINESS LOGIC ======

Status: Implemented (stage S4; secret protection, minimal scope and write safety in S6). FR-21 is still open.

## Purpose
`GoogleSheetsIntegration` owns:
- Two-way exchange with the Google Sheet: reading directories, people and responses, appending new directory items and people, writing the "Status" column.
- Google authorization (OAuth 2.0, Desktop app), token cache, "Forget token".
- Mapping response columns to fields by header keywords (`ResponseColumnMap`), A1 ranges, connection diagnostics, readable API errors.
- The stable row key (`RowKey`) and the demo source (`DemoResponseSource`) for work without internet.
- Exchange contracts (`IResponseSource`, `IResponseSink`, DTOs) and `PublishService`, which collects the package to publish.

**Not here:**
- Turning raw rows into `Mission`, dedup, status choice: `Episodes`. Saving and merging directories: `ReferenceData`.
- `SpreadsheetId`, sheet names and `UseDemoData`: `Settings` (this module only reads them).
- Apps Script on the same sheet: an external system, we only respect its writes.

## Entities
No persistent entities. Contracts and service types:

| Type | Basic Fields | Description | Invariants |
|---|---|---|---|
| `IResponseSource` *(contract, Core)* | `LoadDirectoriesAsync`, `LoadResponsesAsync` | Data source: Google Sheet or demo data. | - Returns raw data only.<br>- Access errors are `SourceAccessException` from Core, not Google types. |
| `IResponseSink` *(contract, Core)* | `PublishAsync(PublishPayload)` | Write-back target (`GoogleSheetsWriter`). | - Add-only: foreign rows are never deleted or rewritten.<br>- The only in-place update is the "Status" column, only for records with a changed status. |
| `RawResponseRow` *(DTO)* | `RowKey`, `RowNumber`, `SubmittedAt`, `Date?`, `OperatorEmail`, names, `UavCount`, `StatusMarker` | Raw response row. | - `RowKey` is the stable key.<br>- `RowNumber` is only a locator in the current sheet state. |
| `PublishPayload` *(DTO)* | directories, `People`, `Statuses` (`StatusChange`: `SourceRowKey`, `Marker`, `ExpectedCurrentMarker`) | What the app wants to write. | - `Statuses` has only changed records.<br>- Immutable. |
| `Diagnosis` | found sheets, matched, missing and ambiguous fields, `IsUsable` | Connection check result. | - Usable only when the responses sheet is found and no field is missing or ambiguous. |

Expected sheet layout (names set in Settings): Responses (row 1 headers, data from row 2), Directories (four blocks side by side: UAV, targets, munitions, settlements with region, data from row 3), People (`Email, Full name, Rank, Position`, data from row 2). If the row count exceeds a reading limit (value in SRS), the sync summary says that data was truncated.

Row key: same format as in `Episodes` (`g1:<32 hex chars of SHA-256 of normalized content>`, status column not hashed, `#2` for identical rows, `demo|N` for the demo source). It never contains a row number. This is the target format. Current state: row keys are still timestamp plus row number, and the switch to the content hash is FR-21, still open.

## Sync and publish flows
Read: the source is chosen by `UseDemoData` (default true, demo mode has no sink). Load directories, merge them in `ReferenceData`, load responses (a `RowKey` for each row), import in `Episodes`.

Publish (`PublishService.PublishAsync`, disabled in demo mode):
1. `CollectAsync` builds the package: directory items (trimmed, no empty names, no duplicates), people with an email, and statuses only for records whose local marker differs from `LastSyncedMarker`. An empty package means nothing to publish.
2. `GoogleSheetsWriter` reads the current sheets, appends missing directory items and people (case-insensitive), and finds each status row by recomputing `RowKey` for the current sheet, never by a stored row number. A status cell is changed only if its value equals `ExpectedCurrentMarker`, otherwise it is reported as a conflict.
3. All changes go in one `BatchUpdate` with `ValueInputOption = "RAW"`. 401/403 gives a `SourceAccessException` with advice, 429/5xx are retried with exponential delay (limited attempts).
4. After success, `LastSyncedMarker` is updated for the written statuses.

## Value Objects
- `DirectoryPair`, `StatusChange`: immutable exchange values (record structs).
- `SheetRange` *(static)*: A1 range builder. Sheet name in apostrophes with `'` escaped as `''`, column index 0 is A and 26 is AA, a negative index is an error.

## Domain Policies
| Domain Policy | Description |
|---|---|
| Write only changed statuses | An unchanged `Active` record is not written, so an empty marker never erases a value (like `СКАСОВАНО`) set by an external script. A mismatch of the expected cell value is a conflict, the write is skipped. |
| RAW writes | Everything is written as `RAW`, so values starting with `=`, `+`, `-`, `@` stay text (no formula injection). Data read from the sheet is untrusted. |
| Columns by headers | Fields are matched from the most specific keyword to the most general, each column to one field. Ambiguous or missing fields go to `Diagnosis`. Required: UAV type, target type, count, settlement, at least one munition. |
| Parsing | Fixed set of date formats with `InvariantCulture`, then Excel serial numbers. Bad or negative count becomes 0. No dependence on OS culture. |
| Least privilege | Reading asks only `spreadsheets.readonly`. The write scope is requested only when `UploadEnabled` is on (default off, Administrator only). |
| Secrets | Client secret and token never go to the repository, error texts, technical log or audit log. The token is stored encrypted (AES-256-GCM, key protected by DPAPI, S6). "Forget token" needs user confirmation and is audited. |
| Errors and retries | 429/5xx and timeouts are retried with backoff. API errors are explained by HTTP code (404, 403, 401, 429, 400). |

## Domain Services
- `GoogleAuthenticator`: authorization, `ForgetToken`, `HasCredentialsFile`. `GoogleSheetsResponseSource`: load, `ListSheetsAsync`, `DiagnoseAsync`.
- `GoogleSheetsWriter`: `PublishAsync` with the optimistic check and retries. `DemoResponseSource`: fictional data from `DemoDataSet`. `PublishService` *(Core)*: `CollectAsync`, `PublishAsync(sink)`.

## Application Commands & Queries
Services and ViewModels play this role.
- Commands: `SyncCommand`, `PublishCommand`, `CheckConnectionCommand` (diagnose and save found sheet names), `ForgetTokenCommand`.
- Queries: `ListSheetsAsync`, `DiagnoseAsync`, `PublishService.CollectAsync` (preview of changes without writing).

## Infrastructure
- Library `Google.Apis.Sheets.v4`, version pinned in `Directory.Packages.props`. `Reports.Integration` depends only on `Reports.Core`.
- Files in `%AppData%\Рапорти\`: `credentials.json` (OAuth client, placed by the user; from S6 it is imported into `credentials.enc` and the plain file is deleted), `token-ro\` and `token-rw\` (token caches). From S6 they are encrypted with AES-256-GCM, and the key comes from HKDF over a DPAPI-protected `master.key`.
- Setup: Google Cloud Console, enable Sheets API, OAuth consent (External, test users), OAuth client "Desktop app". Sync, publish, token and connection-check events go to `IAuditSink` from S6, without token and client secret.

## Implemented security behaviour (S6)

- SEC-05: the OAuth token and `credentials.json` are stored with `ProtectedDataStore` (AES-256-GCM, key from HKDF over a DPAPI-protected `master.key`). A `credentials.json` placed by the user is imported into `credentials.enc`, and the plain file is overwritten and deleted. Token folders are `token-ro` and `token-rw`.
- SEC-06 and SEC-07: import uses the read-only scope. The write scope is used only when `UploadEnabled` is on (default off, Administrator only). Writes use RAW, and values starting with `=`, `+`, `-`, `@` get a leading apostrophe.
- SEC-14 and SEC-15: the spreadsheet title and ID are shown for confirmation before the first write and after the ID changes, and the change is audited. Limits are 5000 rows and 500 cells per value, with cancel and a truncation note in the summary.
- FR-21 is open: row keys are still timestamp plus row number.
