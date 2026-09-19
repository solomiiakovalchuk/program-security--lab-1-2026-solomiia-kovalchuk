# Stakeholder Requirements Specification (StRS)

Outline follows ISO/IEC/IEEE 29148:2018, adapted to the size of the project.

| Field | Value |
|---|---|
| Title | ReportsWpf - UAV and Ammunition Usage Reporting System. Stakeholder requirements |
| Version / status | 0.1.0 / Draft |
| Date | 2026-09-19 |
| Author | Solomiia Kovalchuk, group IPZs-24-1, KhNU |
| Course | Program and Data Security, Lab 1 |
| Related | [SRS](software_requirements.md), [modules](modules.md), [roadmap](roadmap.md) |

## 1 Introduction

**Purpose.** This document describes what the stakeholders need from ReportsWpf, without technical decisions. The system is not built yet. The project follows specification first, code later (stages S0-S7 in the [roadmap](roadmap.md)). Each STK requirement is refined into FR, NFR and SEC requirements in the [SRS](software_requirements.md).

**Scope.** In scope:

- receiving UAV and ammunition usage episodes from a Google Sheet filled by a Google Form;
- local reference lists and a log of generated reports;
- summary of episodes for a period and a Word (.docx) report;
- protection: login, roles, audit, protection of secrets and input data.

Out of scope for 0.1.0: web or mobile version, shared database over the network, database backup.

## 2 Business context

- Now: operators send data by message or form. The report writer counts episodes by hand and types the numbers into a Word file. Mistakes and duplicates are possible, and nobody knows who made which report.
- Planned: operators fill a Google Form, the answers go to a Google Sheet. The app imports them without duplicates, summarises a period and creates the .docx from a template.
- The data is sensitive (operational information, personal data of operators and commanders), so security needs are mandatory.

## 3 Stakeholders

| Stakeholder | Interest |
|---|---|
| UAV operator | Simple form, his episodes are shown correctly. Does not use the app |
| Report writer (user) | Fast and correct report, clear messages, can fix wrong submissions |
| Commander (signer) | Reliable report with his name, rank and position |
| Administrator | Accounts, settings, reference lists, audit log, protection of secrets |
| Google data owner | Minimal app access to the sheet, safe token storage |
| Report recipient | Document in the usual format with correct numbers |
| Instructor, developer | Clear specification, traceability, testable requirements, security work |

## 4 Main use cases

- Sync reference lists and answers from the sheet (or from demo data), work offline in demo mode.
- View episodes, cancel or restore a wrong one, edit reference lists.
- Build a report: choose period, signer, filters, preview, create .docx, view the journal.
- Change settings, check the connection, forget the Google token.
- Log in, change password, manage accounts and roles, view the audit log.

## 5 Stakeholder requirements

| ID | Requirement | Priority |
|---|---|---|
| STK-01 | The system must create a .docx report from episodes of a chosen period | Must |
| STK-02 | The system must get episodes from the Google Sheet and not create duplicates on repeated sync | Must |
| STK-03 | The system must work on demo data without internet and a Google account | Must |
| STK-04 | The user must be able to maintain reference lists (UAV types, targets, munitions, settlements, people) | Must |
| STK-05 | The user must be able to cancel a wrong submission without deleting it | Must |
| STK-06 | The user must be able to set period, signer, filters and order text, and preview the report text | Must |
| STK-07 | The system should keep a journal of generated reports | Should |
| STK-08 | The system should write new reference items and submission statuses back to the sheet | Should |
| STK-09 | The administrator should be able to configure unit details, data source, output folder and template | Should |
| STK-10 | The system could create the report as PDF | Could |
| STK-11 | Operational data and personal data must be protected from unauthorised access on the user's PC | Must |
| STK-12 | Access must require login and password, and actions must depend on the role (administrator, user) | Must |
| STK-13 | Actions of users and security events must be recorded in an audit log | Must |
| STK-14 | The Google token and credentials must be stored protected, and the app should ask only for the minimal access to Google | Must |
| STK-15 | Data from the sheet must be treated as untrusted, the repository must contain no secrets, and dependencies must be checked for known vulnerabilities | Must |

## 6 Constraints and assumptions

- Platform: .NET 8, WPF, Windows 10/11, local SQLite, no server part.
- Google is accessed only through the Google Sheets API (OAuth 2.0, desktop app flow). Reports are created without Microsoft Word.
- One person develops the project during one semester, code is written step by step according to the roadmap.
- UI and messages are in Ukrainian, code identifiers and documents are in English.
- Test data must be fictional, real operational data is not allowed in the repository. Identity and Audit (S6) are needed for STK-12 and STK-13.
- Assumptions: one user works on one PC protected by Windows (profile, disk encryption), and the sheet has the expected structure.
