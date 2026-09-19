# Project Policy - ReportsWpf

| | |
| --- | --- |
| Name | Project Policy - ReportsWpf (UAV and Ammunition Usage Reporting System) |
| Version | 0.1.0 |
| Status | Draft |
| Classification | Internal |
| Last update | 2026-09-19 |
| Owner | Solomiia Kovalchuk |

> This policy sets the mandatory rules for code and architecture of `ReportsWpf`, a planned Windows desktop app (C# / .NET 8, WPF, MVVM, EF Core + SQLite). The app will collect UAV and ammunition usage episodes from a Google Sheet (or a demo source) and build a `.docx` report. Key rules: strictly typed OOP with SOLID, clean code, layered architecture with enforced project boundaries, nullable reference types and tests.
>
> Out of scope for the product: it is not a server or web app, and it does not implement the data collection form (the form and Apps Script stay on the Google side).

Secure development follows NIST SP 800-218 (SSDF). The security policy, privacy policy and threat model will be written in Lab 2 and placed in [docs/security](../security/README.md).

## Policy change rules

- No changes on the side. The policy is changed only by a separate task.
- Any change must be approved by the project owner first.
- A policy change goes in a separate commit (`[DOCS]`) and the document version is increased.

## Contents

Scope, Technologies, Fundamental principles, Architecture and structure, Naming policy, Annotation policy, Exception policy, Null policy, Testing policy, Git workflow, Development documentation policy, Anti-patterns.

## Scope

This policy covers all source code of `ReportsWpf` (`src/`: planned projects `Reports.Core`, `Reports.Data`, `Reports.Documents`, `Reports.Integration`, `Reports.Desktop`, `Reports.Identity`, `Reports.Audit`), tests (`tests/`), configuration, scripts and documentation (`docs/`). It is mandatory for everyone who changes them, developers and AI agents included.

## Technologies

| Category | Technology | Stage |
| --- | --- | --- |
| Platform | .NET 8 (`net8.0`, `net8.0-windows` for `Reports.Desktop`), C# with `Nullable=enable` | S2 |
| UI | WPF, MVVM (own `ObservableObject`, `RelayCommand`), WPF-UI 3.0.5 | S5 |
| Database | Microsoft.EntityFrameworkCore.Sqlite 8.0.10, EF Core migrations | S3 |
| Documents | DocumentFormat.OpenXml 3.1.0 | S4 |
| Integration | Google.Apis.Sheets.v4 1.68.0.3561, OAuth 2.0 (Desktop app) | S4 |
| Checks | Roslyn analyzers, `dotnet format`, architecture tests | S2 |
| Tests | xUnit, `coverlet.collector` | S2 |
| Packages, VCS | NuGet with Central Package Management, Git and GitHub over SSH | S0 |

A new NuGet dependency goes in a separate commit with a reason. Versions are set only in `Directory.Packages.props`.

## Fundamental principles

- Layer separation. UI, application logic and infrastructure (database, file system, Google, OpenXml) are not mixed.
- DDD for business logic, as far as it makes sense. It lives in `Reports.Core`: entities with invariants, value objects (`DatePeriod`), strategies (`IAggregationRule`).
- Foreign logic stays outside. The project has no logic of Google Forms, Apps Script or Word. It talks to them only through `IResponseSource`, `IResponseSink`, `IReportRenderer`.
- Frameworks do not enter Core. WPF, EF Core, OpenXml and Google SDK are not used in `Reports.Core`.
- Strictly typed OOP, SOLID, clean code.
- Secrets and user data are not in the repository: `credentials.json`, tokens, `*.db`, `settings.json`, `error.log`.
- The policy is not optional. If code differs from it, it is fixed before the change is merged.

## Architecture and structure

The solution will consist of isolated layer projects. Dependencies point only towards `Reports.Core`, which depends on nothing. The projects are created in stage S2.

| Layer | Project / folders | Role |
| --- | --- | --- |
| Domain | `Reports.Core/Domain`, `Periods`, `Aggregation` | Entities, value objects, enums, aggregation rules, periods |
| Application | `Reports.Core/Services`, `Abstractions` | `ImportService`, `PublishService`, `ReportService`; contracts to the outside (`I*Repository`, `IResponseSource`, `IResponseSink`, `IReportRenderer`) |
| Infrastructure | `Reports.Data`, `Documents`, `Integration`, `Identity`, `Audit` | Implementations of the contracts: EF Core/SQLite, `.docx`, Google Sheets and demo source, authentication, audit log |
| Presentation | `Reports.Desktop` | Views, ViewModels, composition root, global error handler |

Test projects in `tests/` mirror the `src/` projects (`Reports.Core.Tests`, `Reports.Data.Tests`, and so on). `Reports.Identity` and `Reports.Audit` are planned for S6.

Allowed `ProjectReference` directions:

```
Reports.Desktop ──► Reports.Core
Reports.Desktop ──► Data | Documents | Integration | Identity | Audit   (composition root only)
Data | Documents | Integration | Identity | Audit ──► Reports.Core
Reports.Core ──► (nothing)
tests/* ──► the src project under test
```

Forbidden: `Reports.Core` referencing any other project; infrastructure projects referencing each other; any `src/` project referencing `Reports.Desktop`; EF Core, OpenXml, Google.Apis or WPF-UI packages in `Reports.Core`.

### Domain

Domain is split into bounded contexts that match the modules in the [module registry](../specification/modules.md). Links between contexts use only stable references (id and name).

- Entities inherit `Entity`. Invariants are written in the entity or value object, not in a ViewModel or repository.
- Value objects are `record` or `readonly record struct`. Entities are `sealed` or `abstract`. Collections are initialized.
- Domain does not know EF Core, WPF or the file system. Time comes from a parameter or `TimeProvider`, not `DateTime.Now`.
- A new aggregation rule is a new `IAggregationRule` class. A new period preset inherits `PeriodPreset`.

### Application

A thin orchestration layer without business rules. It depends on Domain and `Abstractions/` only. File access goes through contracts (`IOutputFolderProvider`, `IReportFileStore`). Every async method takes `CancellationToken ct = default`.

### Infrastructure

`Reports.Data` has `AppDbContext`, `IDbContextFactory<AppDbContext>` and repositories. The schema is managed by EF Core migrations, `EnsureCreated` is not used. `Reports.Documents` generates `.docx` from a template with tags. `Reports.Integration` holds everything that knows about Google, plus the demo source. Third-party exceptions are wrapped at the project boundary.

### Presentation

`Reports.Desktop` is the only project that knows all others (composition root). Views have markup and bindings only. ViewModels hold screen state and commands, no business rules, and use only Core abstractions and `ReportsException`, never Google, EF Core or OpenXml types. `async void` only for top-level event handlers.

## Naming policy

- Types, methods, properties, namespaces: `PascalCase`. Parameters and locals: `camelCase`. Private fields: `_camelCase`. Async methods end with `Async`.
- One public type per file, with the same name.
- Identifiers and code comments are in English. UI texts are in Ukrainian.

| Type | Convention | Example |
| --- | --- | --- |
| Interface | prefix `I` | `IMissionRepository` |
| Entity, value object | no suffix | `Mission`, `DatePeriod` |
| Repository | suffix `Repository` | `MissionRepository` |
| Application service | suffix `Service` | `ImportService` |
| Aggregation rule | suffix `Rule` | `UavSummaryRule` |
| Source / sink | suffix `Source`, `Sink`, `Writer` | `GoogleSheetsResponseSource` |
| Renderer | suffix `Renderer` | `DocxReportRenderer` |
| Exception | suffix `Exception` | `ReportException` |
| ViewModel | suffix `ViewModel` | `MissionsViewModel` |
| Test class | `<Type>Tests` | `ImportServiceTests` |

Test methods: `Method_Scenario_ExpectedResult`, for example `Generate_EmptyPeriod_ThrowsReportException`. `using` directives are at the top of the file, `System.*` first.

## Annotation policy (XML doc comments)

`///` comments are required for every public and protected type and member (CS1591 is a build error once `GenerateDocumentationFile` is on in S2). Comments are in English and explain purpose and limits, not retell the code.

- Method: `<summary>` (what and why), `<param>` for each parameter, `<returns>` if it returns a value, `<exception cref>` for each exception it can throw.
- Type: `<summary>`. For `Entity` and value objects also `<remarks>` with the invariants.
- `<inheritdoc/>` is allowed for interface implementations. A `TODO` without an issue reference is not allowed.

## Exception policy

There is one hierarchy. All own exceptions inherit `ReportsException` (abstract, in `Reports.Core`). Two main subclasses are planned: `ReportException` (module Reporting) and `SourceAccessException` (errors of the data source or Google, declared in `Reports.Core.Abstractions` so ViewModels can catch it without Google types).

External exceptions (`DbUpdateException`, `GoogleApiException`, `IOException`, `OpenXmlPackageException`, `JsonException`) are wrapped at the infrastructure project boundary. The original is kept as `InnerException`.

Each exception has a code `RPT.<Segment>.<number>`, for example `RPT.REP.1` (empty period). The segment is a module token (`REF`, `EPI`, `REP`, `DOC`, `GSI`, `SET`, `IDN`, `AUD`), `INF` for infrastructure or `CORE` for the rest. `ReportsException` has `Code` and `UserMessage`. A code is never changed or reused. A removed exception stays in the registry with `[Obsolete]`.

- A ViewModel catches `ReportsException` and shows `UserMessage`. `OperationCanceledException` is ignored. Any other exception is a defect: it is logged and shown as a general message with `RPT.CORE.*`.
- No empty `catch`, no `throw new Exception(...)`. Argument errors use `ArgumentException`, user or data errors use `ReportsException`.
- Secrets, tokens and personal data are not written to messages or logs.

## Null policy

`Nullable=enable` in all projects. Nullable warnings are build errors (from S2). Avoiding `null` in business logic is a recommendation, not a hard ban.

- The `!` operator needs a comment that explains it.
- Use `string.Empty` and empty collections instead of `null`. Public methods check input with `ArgumentNullException.ThrowIfNull`.
- Required properties of requests use `required` and `init`. An optional value with a meaning in the domain is `T?`.
- A method that may find nothing uses `TryXxx(out T value)` or returns `T?` and says so in `<returns>`.
- `null` is not an error or status marker. Use `RecordStatus`, result types and exceptions.

## Testing policy

The framework is xUnit, run with `dotnet test`. Test projects appear in S2, new unit tests are written together with the code. Details are in the [test strategy](../testing/test_strategy.md).

- External dependencies (Google, user file system) are replaced with fakes through Core contracts. No real network calls in tests.
- Tests never touch the user's `%AppData%`. They use temp folders or SQLite in-memory and clean up.
- WPF is not loaded in unit tests. ViewModels are tested without windows.
- Unit tests come first. Each area has valid and invalid cases and checks the exact exception (`Assert.Throws<...>`).
- Test projects mirror `src/` (`Reports.Core.Tests`, `Reports.Data.Tests`, and so on).
- Goals: Domain, Aggregation and Periods at least 90% of lines, Application at least 80%.
- Test classes use `[Trait]` for `Layer`, `Module` and `Category` (`Unit`, `Integration`, `Smoke`), for example `dotnet test --filter "Layer=Domain"`.

## Git workflow

Repository: `program-security--lab-1-2026-solomiia-kovalchuk` on GitHub. Access is over SSH, the remote is named `github`. Before any work, local `master` is synced with the remote (`git fetch`, `git pull`).

| Branch | Purpose |
| --- | --- |
| `master` | Main branch. From S2 changes come only by PR. In S0 and S1 (docs only, one author) direct commits are allowed. |
| `develop` | Integration branch for planned development (from S2) |
| Task branches | `dev-XXX-<short>`, `fix-XXX-<short>`, `docs-<short>`, `sec-<short>`. Deleted after merge. |

One PR is one task. The checklist is in the [PR template](../../.github/pull_request_template.md).

Commit message format: `[TYPE] summary`.

| Type | Meaning |
| --- | --- |
| `INIT` | Initial creation of the repository |
| `DEV-XXX` | Feature work, XXX is the task number |
| `FIX-XXX` | Defect fix |
| `DOCS` | Documentation |
| `SEC` | Security related changes |
| `RELEASE` | Release (version, tag) |

The summary is up to about 72 characters, imperative, in English. Example: `[DEV-012] Add unique check for Person.Email`. Secrets and user data never go into commits. A secret committed by mistake is treated as leaked and rotated.

The app uses SemVer. A release gets the tag `vX.Y.Z` and a `[RELEASE]` commit. CI (GitHub Actions, `windows-latest`) is added in S2. Every PR runs build with warnings as errors, `dotnet format --verify-no-changes`, tests with coverage, layer checks, `dotnet list package --vulnerable` and a secret scan. Before CI exists, the PR template checklist is used by hand.

## Development documentation policy

Documentation is a deliverable. Outdated or missing documentation is a defect. Changes start from the requirements and the module description, then code.

The structure is: `docs/specification/` (stakeholder_requirements.md, software_requirements.md, modules.md, roadmap.md, modules/<Module>/business_logic.md), `docs/architecture/architecture_description.md`, `docs/policies/project_policy.md`, `docs/security/README.md` (Lab 2 adds security_policy.md, privacy_policy.md, threat_model.md), `docs/testing/test_strategy.md`.

- `modules.md` is the entry point to the modules: description, boundaries, status, dependencies.
- `business_logic.md` is the only file per module. It describes purpose, entities and invariants, rules, what does not live here, and dependencies. Status is "Planned" with a target stage.
- Requirements have simple IDs: `STK-01`, `FR-01`, `NFR-01`, `SEC-01`, `CON-01`.
- Whoever changes a module updates its `business_logic.md` and the related requirements in the same change.
- Documents are in English, short, with lists and tables. Date and version are in the header.

## Anti-patterns

| # | Anti-pattern | Meaning |
| --- | --- | --- |
| 1 | Layer leak | Business logic in a ViewModel or repository; WPF, EF Core, OpenXml or Google SDK in `Reports.Core`. |
| 2 | Foreign logic in the project | Rules of Google Forms, Apps Script or Word implemented inside the project. |
| 3 | Bypassing abstractions | Core or ViewModels use the file system, Google or database directly. |
| 4 | Core depends on Infrastructure | Any reference from `Reports.Core` to other projects, or between infrastructure projects. |
| 5 | Leaking foreign exceptions | Raw `DbUpdateException` or `GoogleApiException` leaves a project. |
| 6 | Vague or empty handling | `throw new Exception("error")`, empty `catch`, changing an assigned error code. |
| 7 | Fat service or God ViewModel | Business rules in the application service or one huge ViewModel. |
| 8 | `null!` everywhere | Silencing nullable analysis instead of modelling missing values. |
| 9 | Happy-path-only tests | No negative cases, real network calls or `%AppData%` access in tests. |
| 10 | Secrets in the repository | `credentials.json`, tokens, `*.db`, `settings.json`, real operator data in git or logs. |
| 11 | Schema change without migration | Changing the EF model without a migration. |
| 12 | Many types in one file | `Services.cs` instead of one type per file. |
