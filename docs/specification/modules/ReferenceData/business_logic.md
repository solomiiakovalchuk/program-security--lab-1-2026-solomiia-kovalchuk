# ====== [ReferenceData] BUSINESS LOGIC ======

Status: Planned (stage S3)

## Purpose

`ReferenceData` owns:
- Directories used in the form and the report text: UAV types, target types, munition types, settlements (with region).
- The People directory: signers and operators (full name, rank, position, email).
- The two-name rule: `ShortName` (what the operator sees) and `FullName` (what goes into the report).
- Seed data and the rules for merging directories read from the Google Sheet.

**Not here:**
- Episodes and their references to directories: `Episodes`.
- Putting names into the report sentence and counting: `Reporting`.
- Reading and appending directories in the sheet: `GoogleSheetsIntegration`.
- User accounts: `Identity`. A `Person` has no password and no role.

## Entities

| Entity | Basic Fields | Description | Invariants |
|---|---|---|---|
| `ReferenceItem` *(abstract, extends `Entity`)* | `ShortName`, `FullName`, `ForDocument()` | Base of `UavType`, `TargetType`, `MunitionType`, `Settlement`. | - `ShortName` is required (max 200) and unique in its table, case-insensitive.<br>- `FullName` max 400, may be empty.<br>- `ForDocument()` returns `FullName`, or `ShortName` if it is empty. |
| `TargetType` *(extends `ReferenceItem`)* | inherited | Target type. | - `FullName` is in the genitive case, because it is put into the phrase "for destruction of ...". |
| `Settlement` *(extends `ReferenceItem`)* | inherited + `Region` (max 200) | Settlement with its region. | - `ForDocument()` returns `ShortName`.<br>- `Region` goes into the `{{ОБЛАСТЬ}}` tag. |
| `Person` *(extends `Entity`)* | `Email`, `FullName`, `Rank`, `Position`, `CanSign` | Signer and/or operator. | - `Email` is required (max 320), trimmed, valid, unique case-insensitive.<br>- `CanSign` only if `FullName`, `Rank` and `Position` are all non-empty.<br>- No foreign key from `Mission.OperatorEmail`. |

Episodes refer to directory items by optional foreign keys. On delete the key becomes `NULL` (`SetNull`), old records stay.

Case-insensitive uniqueness: "Liski" and "liski" are the same item. Comparison is culture-independent (`OrdinalIgnoreCase`). The database uses a unique index on a normalized column (`ShortNameNormalized`, `EmailNormalized`: trimmed, upper case, invariant culture), not `COLLATE NOCASE`, which does not work for Cyrillic in SQLite. The service checks the same rule first and returns a validation error instead of a database exception.

Seed data: `DatabaseInitializer` applies migrations and, only if `UavTypes` is empty, adds fictional example directories and two people in one transaction. The set is defined once in `DemoDataSet` (Core) and reused by the demo source.

## Directory merge flow

Runs before the episode import, in `DirectoryMergeService`, in one transaction.
1. The source returns `RawDirectories` (pairs short/full or short/region, and people).
2. A pair with a non-empty trimmed name is skipped if an item with the same `ShortName` exists, otherwise created. Duplicates inside the package collapse to one. People are matched by normalized `Email`.
3. Existing records are never updated or deleted (add-only). The user sees how many were added and skipped.

## Value Objects

None. `DirectoryPair` and `RawDirectories` are exchange types of `GoogleSheetsIntegration`.

## Domain Policies

| Domain Policy | Description |
|---|---|
| Unique names | `ShortName` and `Email` are unique case-insensitive (service check and unique index). |
| Name for document | `FullName`, otherwise `ShortName`. Settlements always use `ShortName`. |
| Can sign | A signer needs full name, rank and position, otherwise no report is generated (checked in `Reporting`). |
| Input validation | Email format, max lengths and required fields are checked before saving, errors shown at the field. |
| Delete with confirmation | The user confirms and sees how many records refer to the item. |
| Add-only merge, seed for empty database | Merge never changes existing items. Seed runs only when `UavTypes` is empty. |

## Domain Services

- `DirectoryMergeService`: add-only merge of directories and people in one transaction, returns a summary.
- `ReferenceValidationService` *(internal - no UC)*: normalization, field validation, uniqueness check.

## Application Commands & Queries

No separate Application layer, services and ViewModel commands play this role.
- Commands: `SaveAsync` and `DeleteAsync` of `IReferenceRepository` and `IPersonRepository`, directory merge on sync, `DatabaseInitializer` at startup.
- Queries: get all items of each directory (sorted by `ShortName`, `uk-UA`), `GetAllAsync` and `FindByEmailAsync` (case-insensitive) for people.

## Infrastructure

- Tables `UavTypes`, `TargetTypes`, `MunitionTypes`, `Settlements`, `People`, each with a normalized column and unique index. Computed properties are ignored by EF. Domain classes are used as EF entities.
- Schema by EF Core migrations. Repositories open a short-lived `AppDbContext` per operation.
