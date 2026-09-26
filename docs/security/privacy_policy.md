# Privacy Policy - ReportsWpf

| | |
| --- | --- |
| Name | Privacy Policy - ReportsWpf (UAV and Ammunition Usage Reporting System) |
| Version | 0.1.0 |
| Status | Draft |
| Classification | Public |
| Last update | 2026-09-26 |
| Owner | Solomiia Kovalchuk |
| Related documents | [security_policy.md](security_policy.md), [threat_model.md](threat_model.md) |

ReportsWpf is a Windows desktop app. It reads UAV usage records from a Google Sheet that your unit owns and builds Word reports. This page explains what personal data the app handles and how it is protected. The app is still being designed, so this text describes what is planned. Accounts and the audit log come at stage S6 of the [roadmap](../specification/roadmap.md).

## What data the app processes

| Data | Examples | Where it comes from |
| --- | --- | --- |
| Operator data | Email of the UAV operator, date and place of a mission | Answers in the Google Form and the linked Google Sheet |
| People list and signer | Email, full name, rank and position (the signer's name, rank and position are printed in the report) | The People sheet, settings or the administrator |
| Account data (S6) | Login, role, password hash, number of failed logins, lock time | Entered by the administrator and by the user |
| Audit records (S6) | Time, login of the actor, action, object, result, Windows user name and computer name | Created by the app |
| Technical data | Error log without secrets, Google sign-in token | Created by the app and by Google sign-in |

The app does not collect phone numbers, addresses, photos or payment data. Demo mode data is made up.

## Why the data is used

To build a report about UAV and ammunition use for a period (with the signer's name and position), to avoid importing the same record twice, to let only authorised people use the app and see who changed what (S6), and to fix errors. The data is not used for advertising or profiling. The app collects only what these goals need.

## Where the data is stored

- On your PC, in `%AppData%\Рапорти`: the database `reports.db`, settings, the template, generated reports, the error log and the Google token.
- In the Google Sheet owned by your unit. The sheet is managed by your unit and Google, not by this app. The app can write reference lists, people and status marks back to it.
- The project repository does not contain real data, passwords or tokens.

## Who can access the data

| Who | Access |
| --- | --- |
| Administrator (S6) | Everything in the app: accounts, settings, People list, audit log, Google token |
| Operator (S6) | Records, reports and the report journal, own password. No accounts, settings or audit log |
| Other Windows users of the PC | Should have no access. The folder is in your Windows profile and is protected by its rights |
| The project author | No access. The app does not send data to the author |

Before S6 anyone who can open your Windows profile can open the app, so protect your Windows account with a password.

## How long the data is kept

| Data | Retention |
| --- | --- |
| Audit records | 365 days by default. The administrator can change it, but not below 90 days. Older records are removed only by the administrator after an export |
| Records and reports | Until the administrator deletes them. Records are cancelled and not deleted by default |
| Accounts | Until the administrator deletes the account (the audit keeps the login as text until its retention ends) |
| Error log | Limited by size, the oldest part is removed first |
| Google token | Until you use "Forget token" or remove access in your Google account |

## Sharing with third parties

- The app talks only to Google APIs (Google Sheets and Google sign-in) with your own Google account. Google handles this data under its own privacy policy.
- There is no telemetry, analytics, advertising or crash reports sent anywhere. The author receives no data.
- Data leaves your PC only when you sync with the sheet, upload changes to it, or pass a report or a backup to someone.

## Your rights

You can ask the administrator of your installation to show you your data (account, records, People entry, audit entries), correct wrong data (name, rank, position, email), or delete it (account, People entry). Records needed for issued reports may be kept without the personal link, and audit records stay until their retention ends. Data in the Google Sheet or Form must be changed there, by the person who manages the sheet.

## How the data is protected

- Passwords are never stored or logged in plain text. They are hashed with PBKDF2 and a random salt.
- The Google token and client file are encrypted with Windows data protection (DPAPI) for your account (S6).
- The app asks Google only for the access it needs. Reading is the default, writing only after you turn on upload.
- Logs and the audit log do not contain passwords, tokens or report content.
- Data from the sheet is checked before use, and writes to the sheet cannot run as formulas.
- Roles limit who sees and changes what, and important actions go to the audit log.

The full rules are in the [security policy](security_policy.md).

## Contacts

Project author: Solomiia Kovalchuk, group IPZs-24-1, Kharkiv National University (KhNU). Questions can be sent through the GitHub repository `program-security--lab-1-2026-solomiia-kovalchuk` (an issue, without private data in it).

## Changes to this policy

The policy may change when the app gets new features or uses new data. The version and date are at the top of this page, and a significant change is mentioned in the release notes.
