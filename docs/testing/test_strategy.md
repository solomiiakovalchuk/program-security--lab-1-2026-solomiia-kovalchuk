# Test Strategy - ReportsWpf

| | |
| --- | --- |
| Name | Test plan and strategy - ReportsWpf |
| Version | 0.1.0 |
| Status | Draft |
| Last update | 2026-09-19 |
| Author | Solomiia Kovalchuk |

## Purpose

This document plans how the system will be checked against the [software requirements](../specification/software_requirements.md). The system is not built yet, so this is a plan. Tests are written together with the code of each stage. The rules are in the [project policy](../policies/project_policy.md#testing-policy).

- No real network calls, Google is replaced with fakes.
- Unit tests come first. Every area has valid and invalid cases. Tests clean up after themselves.

## Test levels

| Level | What it checks | Stage |
| --- | --- | --- |
| Unit | Domain, aggregation rules, services, pure functions | S3 and later |
| Integration with database | Repositories, unique indexes, migrations (SQLite in-memory) | S3 |
| Document generation | `.docx` is created and tags are replaced | S4 |
| Google, ViewModels | Google through a mocked HTTP handler (S4); commands and states without windows (S5) | S4, S5 |
| End-to-end smoke | Demo mode: import, summary, `.docx`, journal | S4 |
| Security | Passwords, roles, audit, untrusted data, formula injection | S3 (input validation), S6 |
| Architecture | Layer boundaries | S2 |

Manual checks (clean Windows, real Google Sheet, look at the `.docx`) are done in S7.

## Tools

- xUnit with `Microsoft.NET.Test.Sdk`, plain `Assert`.
- SQLite in-memory for repository tests, own fakes for `IResponseSource`, `IResponseSink` and repositories, coverlet for coverage, NetArchTest for layer rules.

## Coverage goals

| Layer | Goal (lines) |
| --- | --- |
| Domain, Aggregation, Periods, Identity, Audit | at least 90% |
| Application (Services) | at least 80% |
| Infrastructure | at least 70% |
| ViewModels | at least 60% |

In S2 coverage is only collected. From S3 the report is checked in every PR. In S7 the goals become required.

## CI

GitHub Actions on `windows-latest` (WPF needs Windows), added in S2. Every PR to `develop` and `master` runs: build with warnings as errors, `dotnet test` with coverage and architecture tests, `dotnet format --verify-no-changes`, `dotnet list package --vulnerable --include-transitive` and a secret scan. Any error rejects the PR. Before S2 the PR template checklist is used by hand.
