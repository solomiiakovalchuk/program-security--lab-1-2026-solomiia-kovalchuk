# ====== [Audit] BUSINESS LOGIC ======

Status: Planned (stage S6)

The audit log complements the report journal (`JournalEntry` in `Reporting`) and the technical `error.log`, it does not replace them. Numeric limits are design values, the final ones are fixed in the SRS.

## Purpose
`Audit` owns:
- The audit log (`AuditEvent`): immutable records of user actions and security events: when (UTC), who (actor), what (action), on what (target), result, details.
- The registry of event types (`AuditAction`) and the rules for building and sanitizing details (no secrets).
- Append-only guarantees, retention, queries by period, actor and type, and export.

**Not here:**
- Authentication, roles, session: `Identity`. Audit stores a snapshot of the actor login and receives security events from it.
- The report journal `JournalEntry`: `Reporting`. Audit records "user X generated a report", the journal holds the report attributes.
- Technical logging of failures (`error.log`, stack traces). Stack traces never go into audit.
- Deciding which actions are allowed: other modules and `Identity`. Audit does not block anything, it only records.

## Entities
| Entity | Basic Fields | Description | Invariants |
|---|---|---|---|
| `AuditEvent` *(extends `Entity`, append-only)* | `EventUuid`, `OccurredAtUtc`, `ActorAccountId?`, `ActorLogin`, `ActionType`, `TargetType`, `TargetId?`, `Outcome`, `Details`, `OsUser`, `MachineName` | One log record (table `AuditEvents`). | - Immutable after creation, no update or delete through the app.<br>- `OccurredAtUtc` is UTC and set by the service, not by client code.<br>- `ActorLogin` is a snapshot at event time (kept if the account is deleted). Use `system` for system actions and `anonymous` before login, with a null account id.<br>- `ActionType` and `Outcome` come only from the registry.<br>- `Details` max 2000 chars, sanitized, no secrets. `EventUuid` is unique. |
| `AuditAction` *(registry)* | key like `Auth.LoginSucceeded` | Catalog of event types. | - Keys are stable and never reused. |
| `AuditOutcome` *(enum)* | `Success`, `Failure`, `Denied` | Result. | - `Denied`: blocked by access policy. `Failure`: executed but failed. |

`TargetType` is one of `Account`, `Mission`, `ReferenceItem`, `Person`, `Settings`, `Report`, `GoogleSync`, `Audit`, `Application`. `TargetId` is an identifier (record id, login, report file name), never content.

Event types by area:
- Authentication and accounts: `Auth.LoginSucceeded/LoginFailed/AccountLockedOut/Logout`, `Password.Changed/ResetByAdmin`, `Account.Created/Deleted/Blocked/Unblocked/RoleChanged`, `Access.Denied`.
- Work data: `Report.Generated`, `Mission.Cancelled/Restored`, `Sync.Import`, `Reference.*`, `Person.*`.
- Google and settings: `Google.SyncCompleted/PublishCompleted/TokenForgotten/ConnectionChecked` (never the token or client secret), `Settings.Changed` and `Template.Rebuilt` (names of changed fields, not values).
- Audit itself: `Audit.Exported`, `Audit.RetentionApplied`.

## Recording flow
1. A source module performs the action and calls `IAuditSink.Record(...)` with action, target, result and raw details. Identity events come from its domain events.
2. `AuditRecordingService` adds `OccurredAtUtc`, the actor from `ISessionContext`, OS user and machine name, and sanitizes `Details`.
3. The record is appended to `AuditEvents`. For security events of `Identity` (account, password, lockout changes) it is written in the same transaction as the change. For other events a write failure does not cancel the business operation, it goes to `error.log`.
4. The administrator views the log (`ListAuditEventsAQ`) and may export it (`ExportAuditAC`).

*Boundary:* Audit receives, stores, queries and exports. The source module decides what is an event and calls `IAuditSink`.

## Value Objects
- `AuditQuery`: `FromUtc`, `ToUtc`, `ActorLogin?`, `ActionType?`, `Outcome?`, `Skip`, `Take`. Immutable, `FromUtc <= ToUtc`, `Take <= 1000`.
- `AuditDetails`: sanitized description, immutable, max 2000 chars, no control chars (CR/LF replaced to prevent fake log lines), built only from a whitelist of fields per action.
- `ActorRef`: snapshot `(accountId?, login)`, immutable, `system` and `anonymous` are reserved.

## Domain Policies
| Domain Policy | Description |
|---|---|
| `AppendOnlyDPolicy` | The repository has `Append` and queries, no `Update` or `Delete`. An optional SQLite trigger can forbid `UPDATE`. The only deletion is retention. |
| `NoSecretsDPolicy` | Never logged: passwords, hashes, salts, tokens, client secret, `credentials.json` content, full settings values, report or submission content. `Details` is built from a whitelist, so a new sensitive field does not leak automatically. |
| `PersonalDataDPolicy` | Minimize personal data: ids instead of names where possible. Operator email appears only in People directory events. |
| `RetentionDPolicy` | Records are kept 365 days by default (an administrator can change it, minimum 90 days). Older records are deleted only by `ApplyAuditRetentionAC` (Administrator) after a successful export, and this is logged as `Audit.RetentionApplied`. No automatic background deletion. |
| `ReadAccessDPolicy` | Viewing and export only for `Administrator`. |
| `ExportSafetyDPolicy` | Export to CSV or JSON. Cells starting with `=`, `+`, `-`, `@` get a `'` prefix (no formula injection). The export is logged. |
| `OrderingDPolicy` | Read order: `OccurredAtUtc` descending, then `Id`. Times are UTC (from `IClock`), shown as local time in the UI. A hash chain (`PrevHash`, `Hash`) is an optional later improvement. |

## Domain Services
- `AuditRecordingService`: completes the record with actor, time, host, sanitizes details, appends it. `AuditDetailsSanitizer` *(internal - no UC)*: builds `AuditDetails` from the whitelist.
- `AuditQueryService` (paged query), `AuditExportService` (CSV or JSON with safe cells, logs the export), `AuditRetentionService` (finds old records, requires an export, deletes, logs it).

## Domain Events
None published. Audit consumes events of other modules (mainly `Identity`) through `IAuditSink`. `Audit.RetentionApplied` is a log record, not a domain event.

## Application Commands & Queries
- Commands (`AC`): `RecordAuditEventAC` (internal, called through `IAuditSink`), `ExportAuditAC` (period, CSV or JSON), `ApplyAuditRetentionAC` (needs a ready export), `SetAuditRetentionAC`. Export and retention are for Administrator only.
- Queries (`AQ`): `ListAuditEventsAQ` (page by period, actor, action, result), `GetAuditEventAQ`, `ListAuditActorsAQ` (for the UI filter).

## Infrastructure
- Table `AuditEvents` in the same `reports.db` (transactions together with account changes), with indexes on `OccurredAtUtc`, `ActorLogin`, `ActionType` and a unique `EventUuid`. The schema comes from an EF Core migration.
- `IAuditRepository` (contract in Core, implementation in `Reports.Data`): `AppendAsync`, `QueryAsync`, `CountAsync`, `DeleteOlderThanAsync` (only for retention).
- The code lives in the separate project `Reports.Audit`, which depends only on `Reports.Core`. The UI has an "Audit log" tab visible only to `Administrator`.
