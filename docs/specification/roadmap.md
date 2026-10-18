# Roadmap - ReportsWpf

| | |
| --- | --- |
| Name | Implementation plan - ReportsWpf |
| Version | 0.2.0 |
| Status | Draft |
| Classification | Internal |
| Last update | 2026-10-17 |
| Owner | Solomiia Kovalchuk |

## Purpose

This is the order in which `ReportsWpf` will be built. The specification and policies come first (S0, S1), then the code (S2 to S7). Other documents refer to stages by code S0-S7. A stage starts when the previous one is done. The plan has no calendar dates.

Each lab of the course fixes a slice of behaviour (authentication in Lab 3, cryptographic protection in Lab 4), so the matching piece of S2/S6 code is built then, ahead of the rest of that stage. This does not reorder the stages: S2 is "done" only once the full solution skeleton exists (all five `Reports.*` projects, CI), and S6 only once Identity also has SQLite persistence, UI wiring and Audit.

## Stages

| Stage | Goal | Main results |
| --- | --- | --- |
| S0 | Repository and specification (Lab 1) | Repo with SSH access, folder structure, project policy, requirements, architecture description, module descriptions, test strategy |
| S1 | Security documents (Lab 2) | Security policy, privacy policy, STRIDE threat model in `docs/security` |
| S2 | Solution skeleton | `.sln` (`ReportsWpf.sln`, started in Lab 3), projects `Reports.Core/Data/Documents/Integration/Desktop`, test projects, `Directory.Build.props`, CI |
| S3 | Core domain and data | ReferenceData, Episodes, Reporting, Settings, SQLite with EF Core migrations, unit and integration tests |
| S4 | Documents and Google | Documents (`.docx` generation), GoogleSheetsIntegration (OAuth, import, status write-back), demo source |
| S5 | WPF UI | MVVM base, main screens, end-to-end flow in demo mode and with a real sheet |
| S6 | Security features | Identity (domain logic done in Lab 3, SQLite-backed persistence and UI wiring still open), Audit, secrets protection (DPAPI + AES-GCM via `Reports.Security`, built in Lab 4), minimal OAuth scope, formula injection protection |
| S7 | Release | Coverage goals reached, dependency vulnerability check, release 1.0 |

A stage is done when its results exist, its tests pass, and the docs match the code.

## Process rules

- The code, docs and policies live in one GitHub repository (`program-security--lab-1-2026-solomiia-kovalchuk`). The key is created with `ssh-keygen`, the remote is named `origin`.
- Before any work, sync local `main` with the remote (`git fetch`, `git pull`). After work, `git push`.
- Commits use the format `[TYPE] summary` with `INIT`, `DEV-XXX`, `FIX-XXX`, `DOCS`, `SEC`, `RELEASE`.
- Documentation is updated together with the code. The spec comes first, then the code.
- No secrets or real data in the repository. Each lab gets a report with the repository link.
