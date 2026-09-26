# Software Requirements Specification (SRS)

Outline follows ISO/IEC/IEEE 29148:2018, adapted to the size of the project.

| Field | Value |
|---|---|
| Title | ReportsWpf - UAV and Ammunition Usage Reporting System. Software requirements |
| Version / status | 0.1.0 / Draft |
| Date | 2026-09-19 |
| Author | Solomiia Kovalchuk, group IPZs-24-1, KhNU |
| Course | Program and Data Security, Lab 1 |
| Input | [StRS](stakeholder_requirements.md) |
| Related | [modules](modules.md), [architecture](../architecture/architecture_description.md), [roadmap](roadmap.md), [test strategy](../testing/test_strategy.md) |

## 1 Introduction

**Purpose.** This document lists the verifiable requirements for ReportsWpf. The system is not built yet. Every requirement is planned and has a priority (MoSCoW) and a target stage S0-S7 from the [roadmap](roadmap.md). Tests are derived from these requirements.

**Scope.** ReportsWpf is a local Windows desktop application. It imports episodes from a Google Sheet (or from demo data), stores them in SQLite, summarises them for a period and generates a .docx report. It also has login, roles and an audit log.

**Product overview.** The application is stand-alone, there is no server. It talks to Google Sheets API, the Windows file system and Word templates. The layered architecture has the projects Reports.Core, Reports.Data, Reports.Documents, Reports.Integration and Reports.Desktop, plus Reports.Identity and Reports.Audit later. Details are in the architecture description.

Modules (all planned): ReferenceData, Episodes, Reporting, Settings (S3); Documents, GoogleSheetsIntegration (S4); Identity, Audit (S6).

Users: the report writer (role Operator) uses sync, episodes, reports and the journal. The administrator (role Administrator) can also manage accounts, settings, reference lists, upload to the sheet and the audit log. Roles are introduced at S6.

**Definitions**

- Episode: one form submission with targets, UAVs and munitions.
- Report: .docx document with the summary for a period, signed by a commander.
- Signer: a person from the reference list with full name, rank and position.
- Operator (UAV): person who fills the form, not a user of the app.
- Tag: placeholder `{{...}}` in the .docx template.
- Demo mode: work on built-in fictional data without internet.
- DPAPI: Windows Data Protection API.

## 2 References

[StRS](stakeholder_requirements.md), [roadmap](roadmap.md), [project policy](../policies/project_policy.md), ISO/IEC/IEEE 29148:2018, ISO/IEC/IEEE 42010:2022, NIST SP 800-218 (SSDF).

## 3 Requirements

### 3.1 Functional requirements

| ID | Requirement | Module | Priority | Stage |
|---|---|---|---|---|
| FR-01 | The system shall store reference lists of UAV types, targets, munitions and settlements, each item with a short name (used in the form) and a full name (used in the document) | ReferenceData | Must | S3 |
| FR-02 | The system shall let the user add, edit and delete reference items, keep the short name unique inside a list and ask for confirmation before deleting | ReferenceData | Must | S3 |
| FR-03 | The system shall keep a list of people (email, full name, rank, position) and allow as signer only a person with all three name fields filled | ReferenceData | Must | S3 |
| FR-04 | The system shall import new submissions from the active source as episodes and show a summary (total, imported, skipped, need attention, unknown values), skipping submissions whose key is already in the database | Episodes | Must | S3 |
| FR-05 | The system shall match names to reference lists by short name (ignoring case and spaces), collect unknown values, replace an empty date with the submission day and a negative UAV count with zero | Episodes | Must | S3 |
| FR-06 | The system shall set the episode status: Active, Cancelled (by marker), or Needs attention (unknown UAV type or target) | Episodes | Must | S3 |
| FR-07 | The system shall let the user cancel and restore an episode without deleting it | Episodes | Must | S3 |
| FR-08 | The system shall accept a period as one of six presets or custom dates, swap the dates if the start is later than the end and reject periods longer than 366 days | Reporting | Must | S3 |
| FR-09 | The system shall include in a report only Active episodes inside the period (both ends included), filtered by operator and settlement if set | Reporting | Must | S3 |
| FR-10 | The system shall calculate the values of 12 report tags (dates, UAVs, munitions, targets, settlement, region, unit, order, signer position, rank and name), grouping UAV types by name and sorting them by count | Reporting | Must | S3 |
| FR-11 | The system shall refuse to create a report without a complete signer or when the period has no episodes, and give a clear message | Reporting | Must | S3 |
| FR-12 | The system shall let the user preview the report text before creating the file | Reporting | Should | S3 |
| FR-13 | The system shall add an entry to an append-only journal for every generated report (time, period, signer, filters, file path) and let the user view it and open the file | Reporting | Should | S4 |
| FR-14 | The system shall be able to create the report as PDF through a separate renderer | Reporting | Could | S7 |
| FR-15 | The system shall create a .docx template (A4, margins 30/10/20/20 mm, line spacing 1.5, font from settings) with tags if it does not exist, and let the administrator rebuild it after a warning that manual edits are lost | Documents | Must | S4 |
| FR-16 | The system shall create a report as a copy of the template and replace tags in the body and headers, including tags split into several text runs, in a single pass | Documents | Must | S4 |
| FR-17 | The system shall name the file by the pattern with signer name and dates, replace invalid characters, add a suffix (N) if the name exists and save it to the output folder | Documents | Must | S4 |
| FR-18 | The system shall read reference lists, people and answers from the sheet, find answer columns by header keywords, and parse dates (text formats and Excel serial numbers) and counts | GoogleSheetsIntegration | Must | S4 |
| FR-19 | The system shall authorise in Google with OAuth 2.0 (desktop flow), cache the token and let the administrator delete the cached token | GoogleSheetsIntegration | Must | S4 |
| FR-20 | The system shall write new reference items, people and changed statuses to the sheet in one batch request, without overwriting values set outside the app, and disable this in demo mode | GoogleSheetsIntegration | Should | S4 |
| FR-21 | The system shall build the duplicate-check key of a row as a content hash that does not depend on row number or order | GoogleSheetsIntegration | Should | S4 |
| FR-22 | The system shall provide a built-in demo source with fictional submissions that works offline and is on by default | GoogleSheetsIntegration | Must | S3 |
| FR-23 | The system shall check the connection to the sheet and turn Google API errors (400, 401, 403, 404) into clear messages with advice | GoogleSheetsIntegration | Should | S4 |
| FR-24 | The system shall keep settings (unit details, order text, default signer, data source, sheet ID, sheet names, output folder, font) in a file and use safe defaults if the file is missing or damaged | Settings | Must | S3 |
| FR-25 | The system shall let a user with the right change settings in the UI and switch between light and dark theme | Settings | Should | S5 |
| FR-26 | The system shall create the built-in account ADMIN on the first start without a default password and force setting a password on the first login | Identity | Must | S6 |
| FR-27 | The system shall require login and password before any function, and let the user log out and change own password | Identity | Must | S6 |
| FR-28 | The system shall let the administrator create, block, unblock and delete accounts, assign roles (Administrator, Operator) and reset passwords with a forced change; the last active administrator cannot be deleted, blocked or demoted | Identity | Must | S6 |
| FR-29 | The system shall record security events (login success and failure, lockout, logout, password change, role change, account create and block) and data-changing actions (sync, upload, cancel and restore, reference and settings changes, report generation) | Audit | Must | S6 |
| FR-30 | Every audit record shall contain UTC time, actor, action, object and result; the log shall be append-only and shall not contain passwords, hashes or tokens | Audit | Must | S6 |
| FR-31 | The system shall let the administrator view the audit log filtered by user, action and period | Audit | Should | S6 |

### 3.2 External interfaces

- User interface: WPF window with side navigation (answers, report, reference lists, journal, settings, and after S6 users, audit and login), error messages without stack traces.
- Google Sheets API v4: OAuth 2.0 desktop flow, read of three sheets, writes in one batch request.
- File system: data folder `%AppData%` with the database, settings file, template, credentials and token, plus the folder for reports.
- Word .docx (OpenXML): input is the template with tags, output is a copy with values.

### 3.3 Non-functional requirements

| ID | Requirement | Priority | Stage |
|---|---|---|---|
| NFR-01 | Creating a report for a database of up to 5000 episodes shall take no longer than 5 seconds; the period filter shall run in the database query | Should | S7 |
| NFR-02 | Network and file operations shall run asynchronously and not block the UI thread for more than 200 ms | Should | S5 |
| NFR-03 | The full cycle (import, summary, .docx, journal) shall work in demo mode without internet and a Google account | Must | S5 |
| NFR-04 | An unhandled exception shall be written to the error log and shown to the user, the app shall not close silently | Must | S5 |
| NFR-05 | The database schema shall be managed by EF Core migrations and upgrade without data loss | Should | S3 |
| NFR-06 | The UI and error messages shall be in Ukrainian, with advice on how to fix the problem | Must | S5 |
| NFR-07 | The app shall run on Windows 10/11 x64 without Microsoft Word | Must | S5 |
| NFR-08 | Reports.Core shall not depend on other projects or NuGet packages, and layer dependencies shall be checked by architecture tests | Must | S2 |

### 3.4 Security requirements

| ID | Requirement | Priority | Stage |
|---|---|---|---|
| SEC-01 | The system shall store passwords only as PBKDF2-HMAC-SHA256 hashes with a unique random salt (at least 16 bytes) and at least 600 000 iterations, compare hashes in constant time and never log passwords | Must | S6 |
| SEC-02 | The system shall reject passwords that are empty, longer than 128 characters or equal to the login; for accounts with restrictions enabled it shall also require 8 or more characters, a digit and a punctuation mark | Must | S6 |
| SEC-03 | The system shall lock an account for 15 minutes after 5 failed logins in a row and shall not reveal whether the login exists | Must | S6 |
| SEC-04 | The system shall check the role for every protected action in the application layer, not only by hiding UI elements | Must | S6 |
| SEC-05 | The system shall protect the OAuth token and `credentials.json` with DPAPI (current Windows user) instead of plain text | Must | S6 |
| SEC-06 | The system shall request the minimal OAuth scope: `spreadsheets.readonly` for import and `spreadsheets` only after upload is enabled | Should | S6 |
| SEC-07 | The system shall write to the sheet in RAW mode and neutralise values that start with `=`, `+`, `-` or `@` (formula injection) | Must | S6 |
| SEC-08 | The system shall treat sheet data as untrusted: validate email, field lengths, UAV count range, dates, sheet ID and period length, and never interpret values as tags | Must | S3 |
| SEC-09 | The system shall record security events and user actions in the audit log (FR-29 to FR-31) | Must | S6 |
| SEC-10 | The repository shall contain no secrets or personal data: `.gitignore` shall exclude `credentials.json`, `token*`, `*.db`, `settings.json` and `*.log`, and CI shall scan for secrets | Must | S2 |
| SEC-11 | Package versions shall be managed in `Directory.Packages.props`, CI shall run a vulnerability check (`dotnet list package --vulnerable`) and a final check shall be done before release 1.0 | Must | S7 |
| SEC-12 | The data folder and the database shall be readable only by the owner of the Windows profile, and the error log shall not contain secrets | Could | S6 |
| SEC-13 | Every audit record shall contain a hash of its content and of the previous record, and the audit view shall warn the administrator if the chain is broken | Should | S6 |
| SEC-14 | The system shall show the title of the target spreadsheet and ask the administrator to confirm it before the first write and after the spreadsheet ID changes, and shall record such a change in the audit log | Should | S6 |
| SEC-15 | The system shall limit the rows and the cell length read from the sheet in one sync, let the user cancel the sync, and say in the sync summary when data was truncated | Should | S4 |

### 3.5 Design constraints

| ID | Constraint | Stage |
|---|---|---|
| CON-01 | Platform is .NET 8 and C#, with nullable reference types enabled | S2 |
| CON-02 | UI is WPF with the MVVM pattern, code-behind contains only view logic | S2 |
| CON-03 | Windows 10/11 is the target system, data is stored in the user profile | S2 |
| CON-04 | Storage is local SQLite through Entity Framework Core, no server components | S2 |
| CON-05 | Layered architecture: Reports.Core has no dependencies, Data, Documents and Integration depend only on Core, Reports.Desktop is the composition root | S2 |
| CON-06 | Documents are created only with the OpenXML library, Google is accessed only with the Google Sheets API client | S2 |

## 4 Verification

| Requirement group | Method |
|---|---|
| FR-01 to FR-14 | Test (unit tests, integration tests with in-memory SQLite), demo in demo mode |
| FR-15 to FR-17 | Test (open the generated .docx and check the text), inspection of the template |
| FR-18 to FR-23 | Test with fakes for the sheet source and sink, manual demo with a real sheet outside CI |
| FR-24, FR-25 | Test (defaults, serialisation), demonstration |
| FR-26 to FR-31 | Test (password policy, hashing, lockout, roles, audit records), code analysis |
| NFR-01, NFR-02 | Measurement on reference data |
| NFR-03 to NFR-07 | Demonstration on a clean Windows without Word and without network |
| NFR-08, CON-01 to CON-06 | Inspection of project files, architecture tests |
| SEC-01 to SEC-04 | Test, security tests of Identity |
| SEC-05, SEC-06, SEC-12 | Inspection of code and files, check of the Google consent screen |
| SEC-07, SEC-08 | Test with malicious values (formulas, tags in text) |
| SEC-09 | Test of Audit module |
| SEC-10, SEC-11 | Inspection, secret scan and vulnerability check in CI |
| SEC-13 | Test of Audit module (broken hash chain is detected) |
| SEC-14 | Test with a fake sheet (title shown and confirmed), check of the audit record |
| SEC-15 | Test with a large fake sheet (limit, cancel, truncation message) |

## 5 Traceability

| STK | Requirements |
|---|---|
| STK-01 | FR-09, FR-10, FR-15, FR-16, FR-17, NFR-01 |
| STK-02 | FR-04, FR-05, FR-18, FR-19, FR-21 |
| STK-03 | FR-22, FR-24, NFR-03 |
| STK-04 | FR-01, FR-02, FR-03, NFR-05 |
| STK-05 | FR-06, FR-07 |
| STK-06 | FR-08, FR-11, FR-12 |
| STK-07 | FR-13 |
| STK-08 | FR-20 |
| STK-09 | FR-15, FR-24, FR-25 |
| STK-10 | FR-14 |
| STK-11 | SEC-01, SEC-03, SEC-12 |
| STK-12 | FR-26, FR-27, FR-28, SEC-01 to SEC-04 |
| STK-13 | FR-29, FR-30, FR-31, SEC-09, SEC-13 |
| STK-14 | FR-19, SEC-05, SEC-06, SEC-14 |
| STK-15 | SEC-07, SEC-08, SEC-10, SEC-11, SEC-15 |
