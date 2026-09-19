# Security - ReportsWpf

This folder is for the security documents of the project. They will be written in Lab 2 (stage S1), before any code.

## Planned documents

| Document | Content |
| --- | --- |
| `security_policy.md` | Security policy: scope, roles, principles, secure development, verification, vulnerability handling |
| `privacy_policy.md` | Privacy policy: data categories, purposes, storage, retention, user rights |
| `threat_model.md` | STRIDE threat model: system description, data flow diagram, threats and countermeasures |

## Basic security principles

- Least privilege: roles get only the rights they need, Google OAuth scope is as small as possible.
- Secrets stay out of the repository (`credentials.json`, tokens, `*.db`, `settings.json`).
- Data from the Google Sheet is untrusted: validate it and never run it as formulas or template tags.
- Passwords are stored only as salted hashes. The OAuth token is encrypted with DPAPI.
- Important actions and security events are written to an audit log without secrets.
