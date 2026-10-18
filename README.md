# ReportsWpf - UAV and Ammunition Usage Reporting System

Current stage: between S1 and S6 (Lab 1/2 specification and security docs are done; Lab 3 added the `Reports.Core`/`Reports.Identity` authentication domain, Lab 4 added `Reports.Security` secrets protection, both ahead of the rest of their stage). There is no host application (`Reports.Desktop`) and no database yet. See the [roadmap](docs/specification/roadmap.md).

## What it is

A planned Windows desktop app (C# / .NET 8, WPF, MVVM). It will take UAV usage episodes submitted by operators through a Google Form and a linked Google Sheet (or from a built-in demo source) and build a Word report (`.docx`). It is not a server or a web app. All sample data is made up.

## Planned stack

- .NET 8, C#, WPF, MVVM, WPF-UI
- SQLite with EF Core migrations
- DocumentFormat.OpenXml for `.docx`, Google Sheets API v4 with OAuth 2.0
- xUnit, GitHub Actions (CI from stage S2)

## Repository structure

```
program-security/
├── src/            # Reports.Core, Reports.Identity, Reports.Security (Lab 3/4); Data/Documents/Integration/Desktop from S2
├── tests/          # xUnit projects, one per src project
├── config/ scripts/   # scripts/ holds the Lab 3/4 exercise scripts; config/ empty for now, filled from S2
├── docs/
│   ├── specification/   # requirements, modules, roadmap
│   ├── architecture/    # architecture description
│   ├── policies/        # project policy
│   ├── security/        # security docs (Lab 2, Lab 4)
│   └── testing/         # test strategy
├── .github/pull_request_template.md
├── ReportsWpf.sln
└── .gitignore  Directory.Packages.props  LICENSE  README.md
```

## Documents

- [Roadmap](docs/specification/roadmap.md), [project policy](docs/policies/project_policy.md)
- [Stakeholder requirements](docs/specification/stakeholder_requirements.md)
- [Software requirements](docs/specification/software_requirements.md), [modules registry](docs/specification/modules.md)
- [Architecture description](docs/architecture/architecture_description.md)
- [Test strategy](docs/testing/test_strategy.md), [security docs](docs/security/README.md) (including [crypto strategy](docs/security/crypto_strategy.md), Lab 4)

## How to build

The full application is available after stage S2. The `Reports.Core`, `Reports.Identity` and `Reports.Security` libraries already build and test today (`ReportsWpf.sln`). Needs Windows and .NET SDK 8.

```powershell
dotnet restore
dotnet build -c Release
dotnet test
```

## Commit format

`[TYPE] summary`, where TYPE is `INIT`, `DEV-XXX`, `FIX-XXX`, `DOCS`, `SEC` or `RELEASE`. Example: `[DOCS] Add roadmap`. More in the [project policy](docs/policies/project_policy.md#git-workflow).

## Author

Solomiia Kovalchuk, group IPZs-24-1, Khmelnytskyi National University (KhNU).

## License

MIT, see [LICENSE](LICENSE).
