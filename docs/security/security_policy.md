# Security Policy - ReportsWpf

| | |
| --- | --- |
| Name | Security Policy - ReportsWpf (UAV and Ammunition Usage Reporting System) |
| Version | 0.1.0 |
| Status | Draft |
| Classification | Internal |
| Last update | 2026-09-26 |
| Owner | Solomiia Kovalchuk |
| Related documents | [project_policy.md](../policies/project_policy.md), [privacy_policy.md](privacy_policy.md), [threat_model.md](threat_model.md), [software_requirements.md](../specification/software_requirements.md) |

## Table of Contents

1. [Purpose](#purpose)
2. [Scope](#scope)
3. [Roles and Responsibilities](#roles-and-responsibilities)
4. [Security Principles](#security-principles)
5. [Secure Development Environment](#secure-development-environment)
6. [Software Protection](#software-protection)
7. [Secure Implementation](#secure-implementation)
8. [Security Verification](#security-verification)
9. [Vulnerability Management](#vulnerability-management)
10. [Privacy Policy](#privacy-policy)
11. [Exceptions](#exceptions)
12. [Related Documents](#related-documents)
13. [Final Provisions](#final-provisions)

---

## Purpose

This policy sets the mandatory security rules for developing and maintaining `ReportsWpf`, a planned Windows desktop app that imports UAV usage episodes from a Google Sheet and builds `.docx` reports. The app processes operator emails, commander names and mission data, and it holds a Google OAuth token. The rules are meant to:

- keep secrets, personal data and the source code out of the wrong hands;
- keep report data correct, so a bad sheet row or a local user cannot silently change it;
- keep a trace of important actions (from S6);
- give a clear way to handle a vulnerability once it is found.

The policy follows the secure development practices of NIST SP 800-218 (SSDF) and builds on the [project policy](../policies/project_policy.md). Concrete requirements are the `SEC-xx` items in the [software requirements](../specification/software_requirements.md). Concrete threats are in the [threat model](threat_model.md). If they disagree with this text, the stricter rule applies until the documents are fixed.

## Scope

The policy covers:

- all code in `src/` and `tests/`, configuration in `config/`, scripts in `scripts/`, the CI workflows in `.github/` and `Directory.Packages.props`;
- all documents in `docs/`;
- the runtime data of one installation: `%AppData%\Рапорти` (`reports.db`, `settings.json`, `credentials.json`, `token-rw\`, `template.docx`, generated reports, `error.log`);
- third-party NuGet packages and the Google Sheets API used through the user's own Google account.

Not covered:

- the Google Form, the Google Sheet and Apps Script as products of Google. Only the way the app reads and writes the sheet is covered;
- the security of the user's Windows PC and Google account (disk encryption, antivirus, updates). The app assumes them and the [threat model](threat_model.md) lists what is assumed;
- web, server and container security, because the app has none of them.

The policy is mandatory for the developer, the reviewer and any AI agent that changes project files.

## Roles and Responsibilities

| Role | Who | Responsibility |
| --- | --- | --- |
| Project owner and developer | Solomiia Kovalchuk | Writes code and docs, follows this policy, keeps the threat model current, fixes vulnerabilities, approves exceptions |
| Reviewer | Teacher | Reviews security documents and critical changes, checks that findings are fixed |
| AI agent | Tool used by the developer | Works only inside the task given. Does not weaken a security rule, does not see real secrets or real operator data |
| Administrator (app role, S6) | Person who runs the app | Manages accounts, settings, the Google token and the audit log |
| Operator (app role, S6) | Person who makes reports | Works with data and reports, has no access to accounts, settings and audit |

The project has one developer, so there is no second developer to review changes. The teacher acts as the reviewer for the lab deliverables, and the developer follows the PR checklist on every change. Only the developer can change security settings of the repository and approve an exception.

## Security Principles

- Least privilege. App roles get only the rights they need (SEC-04). The Google OAuth scope is the smallest that works (SEC-06).
- Deny by default. An action that is not allowed for a role by the access matrix is refused.
- Untrusted input. Everything from the Google Sheet, `settings.json` and the template is checked before use (SEC-08).
- No secrets in code and in git (SEC-10). A secret that reached git is treated as leaked and replaced.
- Secure by default. The app starts in demo mode with fictional data, without network and without a Google connection. Upload to the sheet is off until it is enabled.
- Fail safely. A broken settings file or a bad row gives defaults or a skipped row with a message, never a crash or a silent change of data.
- Minimal data. The app keeps only the data that the report needs. Logs and audit contain ids instead of content.
- No own cryptography. Only .NET and OS mechanisms are used (PBKDF2 from `Rfc2898DeriveBytes`, DPAPI).
- Defence in depth. The role is checked in the application layer and not only in the UI (SEC-04), the sheet is written in RAW mode and also escaped (SEC-07).

## Secure Development Environment

- Source: one GitHub repository `program-security--lab-1-2026-solomiia-kovalchuk`. Access is only over SSH keys created with `ssh-keygen`. The private key stays on the developer's PC and never goes into the repository. HTTPS with a password is not used.
- Branches: in S0 and S1 (documents, one author) direct commits to `main` are allowed. From S2 `main` changes only by pull request, and work goes through `develop` and task branches (`dev-`, `fix-`, `docs-`, `sec-`). The PR template checklist is filled for every PR.
- Commits: format `[TYPE] summary`, security changes use `[SEC]`. A commit has one purpose.
- Secrets: `credentials.json`, `token*`, `*.db`, `settings.json` and `*.log` are excluded by `.gitignore` (SEC-10). Real data never goes into tests. Tests use fake data and temp folders and never touch the real `%AppData%`.
- Dependencies: versions are set only in `Directory.Packages.props` (Central Package Management). Each new package goes in a separate commit with the reason, and before adding it the developer checks the publisher, the licence and known vulnerabilities. Packages are restored only from nuget.org.
- Checks in CI (GitHub Actions on `windows-latest`, from S2): build with warnings as errors, Roslyn analyzers, `dotnet format --verify-no-changes`, tests, architecture tests, a secret scan and `dotnet list package --vulnerable --include-transitive` (SEC-10, SEC-11). Until CI exists, the same steps are done by hand and noted in the PR.
- Security tests run in CI from S2 (input validation tests from S3, Identity and Audit tests in S6). A failing security test blocks the merge.
- Workstation: the developer keeps Windows and .NET SDK updated and uses a Windows account protected by a password.
- AI agents: they get the task and the repository, but not real credentials, tokens, keys or real operator data. Their changes are read by the developer before commit.

## Software Protection

- The repository holds code and documents only. Build output (`bin/`, `obj/`, `publish/`) is excluded and the release is built from a clean checkout by CI.
- A release is a tag `vX.Y.Z` on `main` after the release checks in [Security Verification](#security-verification). The release package does not contain `credentials.json`, tokens, databases or logs.
- The OAuth token (`token-rw\`) and `credentials.json` are stored with DPAPI for the current Windows user, not as plain text (SEC-05, from S6). Before S6 they are plain files and the PC profile is the only protection (see the risks in the [architecture description](../architecture/architecture_description.md)).
- The data folder and `reports.db` must be readable only by the owner of the Windows profile (SEC-12). Whether to encrypt `reports.db` is decided by the threat model.
- Passwords are never stored in a recoverable form (see Secure Implementation).
- `error.log`, the audit log and exception messages never contain passwords, hashes, tokens, the client secret or the content of reports and submissions.
- Backups of the data folder are made by the user. They contain personal data and must be kept as carefully as the folder itself.
- If a secret reaches git by mistake, the developer revokes it first (deletes the Google client or token, changes the password), then removes it from the history. Removing the commit alone is not enough.

## Secure Implementation

Rules for the code. Each one is a reviewed and tested requirement.

- Untrusted data from Google Sheets (SEC-08, S3):
  - trim values, check email format, field lengths, the UAV count range, dates, the sheet ID and the period length (366 days at most);
  - values are used only as data. They are never executed, never used as a file name without cleaning, and never read as `{{tag}}` marks;
  - an invalid row is skipped or marked `Needs attention`, the import goes on.
- Writing to the sheet (SEC-07, S6): `ValueInputOption = RAW` and values that start with `=`, `+`, `-` or `@` are neutralised, so no formula injection is possible. The same rule applies to CSV or JSON export of the audit log.
- `.docx` generation: tags are replaced in one pass with the original text, so a submitted value that contains `{{...}}` does not become a tag. File names are cleaned of invalid characters. Document text is written through OpenXML objects, not as raw XML strings.
- Authentication (S6): PBKDF2-HMAC-SHA256, a random salt of at least 16 bytes per user, at least 600 000 iterations, constant-time comparison (SEC-01). Password rules are in SEC-02. After 5 failed logins an account is locked for 15 minutes, and the error message is the same for an unknown login, a wrong password and a blocked account (SEC-03). There is no default password. The first password of `ADMIN` is set at the first login.
- Authorisation (S6): the roles are Administrator and Operator, and every protected command checks the role through `IAccessPolicy` in the application layer (SEC-04). Hiding a button is not a protection.
- Google access: OAuth 2.0 desktop flow in the system browser, minimal scope (SEC-06). The app never asks for the Google password. `credentials.json` is placed by the user and is not created or shown by the app.
- Database: only EF Core with parameters. No SQL built from strings. The schema changes only by migrations.
- Errors: own exceptions with codes, the user sees a short message without a stack trace. Foreign exceptions are wrapped at the project boundary. Secrets and personal data are not put into messages (see the [project policy](../policies/project_policy.md#exception-policy)).
- Logging and audit (SEC-09, S6): security events (login, failed login, lockout, password and role changes, account changes, access denied) and data-changing actions (sync, upload, report generation, settings and token changes) go to the append-only audit log with UTC time, actor, action, object and result. The log is built from a whitelist of fields, so passwords, hashes, tokens, settings values and submission content do not get there.
- Network: the app opens only outgoing HTTPS connections to Google. Certificate checks are never turned off. The only local listener is the loopback redirect during OAuth consent.
- Forbidden: own crypto, `MD5` or `SHA1` for passwords, hard-coded secrets, disabling TLS validation, `USER_ENTERED` writes, `dynamic` SQL, empty `catch`.

## Security Verification

| Check | When | What it covers |
| --- | --- | --- |
| Unit and security tests | every PR from S2 | Validation of sheet data (S3), tag injection, formula injection, password hashing, lockout, role checks, audit content (S6) |
| Negative tests | every PR | Malicious values (formulas, tags, long text, bad dates), wrong roles, bad settings file |
| Architecture tests | every PR from S2 | Layer rules, no EF Core, OpenXML or Google types in `Reports.Core` |
| Roslyn analyzers, `dotnet format` | every PR from S2 | Code quality and some security rules |
| Vulnerability check of packages | every PR from S2 | `dotnet list package --vulnerable --include-transitive` (SEC-11) |
| Secret scan | every PR from S2 | Tokens and keys in files and history (SEC-10) |
| Manual review | changes in Identity, Audit, secrets, OAuth and sheet writing | The developer rereads the diff with the threat model, the teacher reviews on request |
| Release check | before S7 and release 1.0 | Full dependency check, check of the Google consent screen and scopes, check of file rights, test run on a clean Windows |

A critical or high vulnerability in a dependency, a failed security test or a secret in the repository blocks the merge and the release until it is fixed or accepted as an [exception](#exceptions). Coverage goals are in the [test strategy](../testing/test_strategy.md).

## Vulnerability Management

The project has one developer and the teacher as reviewer, so the process is short.

1. Report. A weakness can be found by the developer, a test, CI, a dependency alert or the teacher. It is written as a GitHub issue with the label `security`. The issue says what and where, but does not contain secrets, real data or a ready exploit. A sensitive finding is sent to the author directly (contact in the [README](../../README.md)) and the issue is created after the fix.
2. Classify. The developer sets the priority and the affected component:

   | Priority | Example | Fix within |
   | --- | --- | --- |
   | Critical | Leaked secret, password stored in plain text, remote code execution | 3 days, and rotate the secret at once |
   | High | Role check missing, formula or tag injection works | 7 days |
   | Medium | Missing validation with a limited effect | 30 days |
   | Low | Hardening, wording, minor leak in a message | next planned stage |

3. Fix. The work goes in a `sec-` branch and a `[SEC]` commit. The fix comes with a test that fails without it. Changes to authentication, secrets or OAuth are reviewed by the teacher.
4. Verify. The test and the CI checks pass, and the issue is closed with a link to the commit.
5. Learn. The developer checks if the cause is a gap in the design. If so, the [threat model](threat_model.md), this policy or the requirements are updated in the same change, and a new `SEC-xx` item is added when needed.
6. Dependencies. A vulnerable package is updated in `Directory.Packages.props` in a separate commit. If no fixed version exists, the risk is written as an exception with a workaround.
7. Disclosure. The app is used locally by one team, so there is no public disclosure process. If a release was made with the problem, the next release notes mention the fix.

## Privacy Policy

The rules for personal data are in a separate document, [privacy_policy.md](privacy_policy.md), because roles for several users are planned and that document may be shown to the users.

## Exceptions

A deviation from this policy is possible only when the rule cannot be met for a real technical reason.

- The developer writes the reason, the risk, the compensating measures and an end date in a `docs/security` note or in the PR.
- The teacher is informed for exceptions that touch secrets, authentication, authorisation or personal data.
- An exception has an end date, at most until the next stage of the [roadmap](../specification/roadmap.md). It is reviewed when the date comes. Open-ended exceptions are not allowed.
- AI agents cannot grant exceptions.

## Related Documents

- [Project policy](../policies/project_policy.md): code rules, Git workflow, CI.
- [Privacy policy](privacy_policy.md): personal data handling.
- [Threat model](threat_model.md): STRIDE analysis of the system.
- [Software requirements](../specification/software_requirements.md): `SEC-01` to `SEC-12`.
- [Architecture description](../architecture/architecture_description.md): data stores, trust boundaries, planned controls.
- Module descriptions: [Identity](../specification/modules/Identity/business_logic.md), [Audit](../specification/modules/Audit/business_logic.md), [GoogleSheetsIntegration](../specification/modules/GoogleSheetsIntegration/business_logic.md).
- [Test strategy](../testing/test_strategy.md).
- External: NIST SP 800-218 (SSDF), OWASP ASVS and Cheat Sheet Series (password storage, CSV injection), Microsoft DPAPI documentation.

## Final Provisions

- The policy takes effect when the project owner approves it. For the lab, it is submitted to the teacher in Lab 2.
- It is reviewed after every stage of the roadmap, and also after a change of architecture, technology stack or threat model, after a security incident and after a critical vulnerability.
- Changes go in a separate `[DOCS]` or `[SEC]` commit and the version of the document is increased.
- If this policy conflicts with another document, the stricter rule applies until the conflict is fixed.
- The developer is responsible for keeping the policy current.
