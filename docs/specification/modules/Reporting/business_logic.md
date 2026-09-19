# ====== [Reporting] BUSINESS LOGIC ======

Status: Planned (stage S3)

## Purpose
`Reporting` owns:
- Report periods (`DatePeriod`) and one-click presets.
- The report request (`ReportRequest`: period, signer, filters, order, subunit, format) and the episode selection rule `Matches`.
- Aggregation rules that turn selected episodes into values of 12 template tags (`ReportContent`, with the main sentence).
- The generation flow (`ReportService`): validation, preview, file name, journal entry.
- The journal of generated reports (`JournalEntry`), append-only.

**Not here:**
- Creating the `.docx` file: `Documents`. Reporting only defines the `IReportRenderer` contract.
- Episodes: `Episodes`. Directories and signers: `ReferenceData`. Unit data and output folder: `Settings`.
- Log of user actions and security events: `Audit`. `JournalEntry` is only the business log of reports.

## Entities
| Entity | Basic Fields | Description | Invariants |
|---|---|---|---|
| `JournalEntry` *(extends `Entity`)* | `GeneratedAtUtc`, `PeriodFrom`, `PeriodTo`, `SignerName`, filters, `Order`, `MissionCount`, `Format`, `TemplateVersion`, `IncludedMissionKeys`, `FilePath` | One generated report. | - Created only after a successful render.<br>- Never edited or deleted (repository has only `GetAllAsync` and `AddAsync`).<br>- Time is UTC.<br>- The settlement filter is stored as id and as name at that moment.<br>- `IncludedMissionKeys` is a snapshot of included submissions. |
| `ReportRequest` *(input)* | `Period`, `Signer`, `OperatorEmail?`, `SettlementId?`, `Order`, `Subunit`, `Format` | What the user sets in the report window. | - The signer is chosen separately (the commander signs, not the operator). |
| `ReportContent` *(result)* | `Values` (tag to value), `MissionCount`, `IsEmpty`, `BuildSentence` | Computed tag values. | - `IsEmpty` means `MissionCount == 0`.<br>- Replacement is single-pass, inserted values are not scanned again.<br>- The sentence text is a constant in code, so preview works before a file exists. |

`ReportFormat` is an enum (`Word`, `Pdf`), only `Word` in the first release. Tags (12): `{{ДАТА_З}}`, `{{ДАТА_ПО}}`, `{{ТИП_ТА_КІЛЬКІСТЬ_БПЛА}}`, `{{БОЄПРИПАСИ}}`, `{{ТИП_ЦІЛІ}}`, `{{ПІДРОЗДІЛ}}`, `{{НАСЕЛЕНИЙ_ПУНКТ}}`, `{{ОБЛАСТЬ}}`, `{{НАКАЗ}}`, `{{ПОСАДА}}`, `{{ЗВАННЯ}}`, `{{ПІБ}}`.

## Report generation flow

`ReportService.GenerateAsync(request)`:
1. Preview: validate (signer can sign, period at most 366 days), load `Active` missions for the period, aggregate. An empty result stops with `ReportException`. Preview alone never creates a file or a journal record.
2. Find the renderer for the format, get the output folder from `IOutputFolderProvider`, build the file name from signer and period (invalid characters become `_`) and reserve a unique path with `IReportFileStore`.
3. The renderer writes the file (temporary file, then atomic rename).
4. Add a `JournalEntry`. If this fails, the created file is deleted, so a report without a journal record never stays.

## Value Objects
| Value Object | Description | Invariants |
|---|---|---|
| `DatePeriod` *(record struct: `From`, `To`)* | Period by calendar days. | - Immutable.<br>- Time part is dropped, `from > to` is swapped.<br>- Both ends are inclusive, `DayCount = (To - From).Days + 1`. |
| `PeriodPreset` *(abstract)* | Today, Yesterday, Last 7 days (default), Previous week, Current month, Previous month. | - Today comes from `IClock`. |

## Domain Policies
| Domain Policy | Description |
|---|---|
| Episode selection (`Matches`) | An episode goes into the report only if it is `Active`, its date is inside the period, the operator email matches (if set, case-insensitive) and the settlement matches (if set). |
| Period limit | `DayCount <= 366` (366 is allowed, 367 is not), same validator for preview and generation. |
| Signer and empty result | `Signer.CanSign` is required (hint to fill the People directory). Generation with no selected episodes is forbidden. |
| Journal | Append-only. If the journal write fails, the file is deleted. |
| Stable order, no culture | Episodes are sorted by `Date`, `SubmittedAt`, `SourceRowKey`. Grouping uses `OrdinalIgnoreCase`, dates use `InvariantCulture`. |

### Aggregation rules (Strategy pattern, one rule per tag)

- Dates: `Period.From` and `Period.To` as `dd.MM.yyyy`. Subunit and order come from the request (defaults from `Settings`). Signer fields come from the signer.
- UAV type and count: group by UAV name (`ForDocument()`, case-insensitive), sum `UavCount`, sort by count descending then name, format "Name - N pcs", joined by commas.
- Munitions, targets, settlements, regions: unique names in order of first appearance (targets use the genitive full name, munitions are not counted).
- A `null` result becomes an empty string. Adding a tag means adding an `IAggregationRule`.

## Domain Services
- `ReportAggregator`: selects and sorts episodes, runs all rules, returns `ReportContent`. `ReportService`: `PreviewAsync` and `GenerateAsync` with journal compensation.
- `ReportRequestValidator` *(internal - no UC)*: checks signer, period limit and request values.

## Application Commands & Queries
Core services and ViewModels play this role.
- Commands: `GenerateCommand`, `PreviewCommand`, `ApplyPresetCommand`, `OpenFileCommand`, `OpenFolderCommand`.
- Queries: `ReportViewModel.LoadAsync` (signers, settlements, operators), `IJournalRepository.GetAllAsync`.

## Infrastructure
- Table `JournalEntries`, no foreign keys to `Missions`, so the journal does not depend on later changes of episodes or directories. `JournalRepository` in `Reports.Data`, EF Core migrations.
- Periods, rules and `ReportContent` are not stored. Generation events go to `IAuditSink` from S6.
