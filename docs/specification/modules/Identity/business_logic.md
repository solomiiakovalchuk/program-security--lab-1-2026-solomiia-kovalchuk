# ====== [Identity] BUSINESS LOGIC ======

Status: Domain logic implemented ahead of schedule in `src/Reports.Core` and `src/Reports.Identity` (Lab 3); persistence (`Reports.Data`, SQLite), wiring into `Reports.Desktop` and `Audit` events remain stage S6, see the [roadmap](../../roadmap.md)

## Purpose
`Identity` owns:
- User accounts (`Account`): login, password hash, role, block flag, password restriction flag, failed attempts counter.
- Login by login and password (before the main window), password change, first start with the built-in `ADMIN`.
- The password policy, the roles `Administrator` and `Operator`, and the access matrix.
- Blocking (by an administrator and temporary after failed attempts) and the current session, which is the actor for other modules and for `Audit`.

**Not here:**
- Logging security events and user actions: `Audit`. Identity only produces the events.
- The People directory: `ReferenceData`. A `Person` is not an account.
- Google credentials and token: `GoogleSheetsIntegration`. Encryption of `reports.db`: a separate security requirement.

## Entities
| Entity | Basic Fields | Description | Invariants |
|---|---|---|---|
| `Account` *(extends `Entity`, Aggregate Root)* | `Login`, `PasswordHash`, `Role`, `IsBlocked`, `PasswordRestrictionsEnabled`, `FailedAttempts`, `LockedUntilUtc?`, `MustChangePassword`, `IsBuiltIn` | User account (table `Accounts`). | - `Login` is trimmed, 3-64 chars (letters, digits, `._-`), unique case-insensitive (normalized column).<br>- The plain password is never stored or logged, only `PasswordHash`.<br>- `Role` is `Administrator` or `Operator`.<br>- `FailedAttempts` is reset to 0 after a successful login.<br>- `PasswordHash == null` only for the built-in `ADMIN` before its first password.<br>- At least one active `Administrator` always exists.<br>- The built-in `ADMIN` cannot be deleted, demoted or blocked. |
| `Session` *(in memory)* | `AccountId`, `Login`, `Role` | Current user, available through `ISessionContext`. | - Lives from login to logout or exit, one session per process. |

`Operator` is an app user, not the UAV operator who fills the Google form.

Password storage: PBKDF2-HMAC-SHA256 (`Rfc2898DeriveBytes.Pbkdf2`, built into .NET), a random 16-byte salt per user, at least 600 000 iterations, 32-byte output (Argon2id would be better but needs a third-party package). Stored as `pbkdf2-sha256$<iterations>$<salt>$<hash>` (base64), versioned, so rehashing at login is possible. Comparison is constant-time (`CryptographicOperations.FixedTimeEquals`).

First start: if `Accounts` is empty, `ADMIN` is created (role `Administrator`, built-in, no password, `MustChangePassword`). The first login forces setting a password. There is no default password in code or docs.

## Status Lifecycle
- Administrative: an administrator can block an `Active` account and unblock a `Blocked` one. There is no self-blocking.
- Lockout: after 5 failed logins in a row `LockedUntilUtc = now + 15 minutes` and login is refused until then. A successful login, an admin password reset or an unblock clears the counter and the lock.
- Password state: "not set" (`PasswordHash == null`, only the bootstrap `ADMIN`) allows only setting the initial password. "Must change" (`MustChangePassword`, after bootstrap or admin reset) allows only changing the password, then the main window opens.

## Login flow
1. After migrations, bootstrap runs and the login window is shown. `MainWindow` is not created before a successful login.
2. `LoginAC(login, password)`: find the account by normalized login (if not found, do a dummy hash to equalize timing and refuse), check `IsBlocked` and `LockedUntilUtc`, compare the hash. A failure does `FailedAttempts++` (lockout at the limit). A success resets the counter, rehashes if needed and creates the `Session`.
3. If `MustChangePassword`, the change form is required. The UI is then built by role: unavailable tabs are hidden, commands have `CanExecute = false`. Services also call `IAccessPolicy.Demand(action)`, so the UI cannot be bypassed.
4. Closing the login window without success shuts the app down. Logout closes the session and shows the login window.

Access matrix. Operator can: generate reports, view the journal and responses, sync, cancel and restore records, change own password. Only Administrator can: edit directories and People, publish to Google, change settings, rebuild the template, forget the token, manage accounts, view and export the audit log.

## Value Objects
- `LoginVO`: normalized login, immutable, trimmed, 3-64 chars, case-insensitive.
- `PasswordHashVO`: encoded hash with parameters, immutable, made only from a password by `IPasswordHasher`, never holds the plain password.
- The typed password exists only in memory during the operation, is not logged, and the array is cleared after use.

## Domain Policies
| Domain Policy | Description |
|---|---|
| `PasswordPolicyDPolicy` | Any password: non-empty, at most 128 chars, no control chars, not equal to the login. If `PasswordRestrictionsEnabled`: length >= 8, at least one digit and at least one punctuation or symbol. New rules apply at the next password change. |
| `LockoutDPolicy` | 5 failed attempts block login for 15 minutes. |
| `LastAdministratorDPolicy`, `BuiltInAccountDPolicy` | The last active `Administrator` and the built-in `ADMIN` cannot be deleted, blocked or demoted (the `ADMIN` password can change). |
| `SelfProtectionDPolicy` | A user cannot delete or block self, or change own role. |
| `ChangePasswordDPolicy` | Needs the current password, the new one must differ and pass the password policy. |
| `ResetPasswordDPolicy` | An administrator sets a temporary password for another user, `MustChangePassword = true`, lockout is cleared. Not allowed for self. |
| `AuthorizationDPolicy` | Role to action matrix, deny by default. |
| `UniformFailureDPolicy` | The same message for unknown login, wrong password and blocked account. Details only in audit. |

## Domain Services
- `AuthenticationService`: login, block checks, hash comparison, counters, `Session`, events.
- `PasswordService` *(`IPasswordHasher`)*: hashing with salt, verification, `NeedsRehash`. `AccountManagementService`: account changes with policy checks.
- `ChangePasswordService`, `AccessPolicyService` (`IAccessPolicy`), `BootstrapService` *(internal - no UC)*.

## Domain Events
Delivered to `Audit` in-process through `IAuditSink`. No password, hash or salt in payloads. For a failed login with an unknown login the login value is not sent (it may be a mistyped password), only `loginKnown = false`.

| Event | Carries |
|---|---|
| `LoginSucceededDE`, `LogoutDE` | account id, login, role |
| `LoginFailedDE` | account id (or null), `loginKnown`, reason (`BadPassword`, `Blocked`, `LockedOut`) |
| `AccountLockedOutDE`, `PasswordChangedDE`, `Account{Created,Deleted,Blocked,Unblocked,RoleChanged}DE`, `AccessDeniedDE` | account id, `lockedUntilUtc`, initiator (`Self`, `Admin`, `Initial`), changed values, performer or denied action |

## Application Commands & Queries
- Commands (`AC`): `LoginAC`, `LogoutAC`, `SetInitialPasswordAC`, `ChangePasswordAC`, `ResetPasswordAC`, `CreateAccountAC`, `DeleteAccountAC`, `ChangeAccountRoleAC`, `BlockAccountAC`, `UnblockAccountAC`, `SetPasswordRestrictionsAC`.
- Queries (`AQ`): `GetCurrentSessionAQ`, `ListAccountsAQ` and `GetAccountAQ` (never with hash or salt), `CheckAccessAQ` (for `CanExecute`).

## Infrastructure
- Table `Accounts` in the same SQLite `reports.db` (one database, atomic updates, unique index on `LoginNormalized`), added by an EF Core migration. Times are UTC, hash column up to 256 chars.
- `IAccountRepository` (contract in Core, implementation in `Reports.Data`). The code lives in the separate project `Reports.Identity`, which depends only on `Reports.Core`. Services are registered in `AppServices`.
- The hash does not protect against replacing a record in the database file. File protection is a separate security requirement.
- Lab 3 interim: `Reports.Identity.InMemoryAccountRepository` implements `IAccountRepository` in memory, so the domain logic and its tests do not depend on `Reports.Data`/SQLite, which is still stage S3. It is replaced by the EF Core-backed repository when `Reports.Data` exists; the contract does not change.
