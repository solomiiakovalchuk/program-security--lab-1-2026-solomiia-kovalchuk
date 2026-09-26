# Security - ReportsWpf

This folder holds the security documents of the project. They were written in Lab 2 (stage S1), before any code.

## Documents

| Document | Content |
| --- | --- |
| [security_policy.md](security_policy.md) | Security policy: scope, roles, principles, secure development, verification, vulnerability handling |
| [privacy_policy.md](privacy_policy.md) | Privacy policy: data categories, purposes, storage, retention, deletion |
| [threat_model.md](threat_model.md) | STRIDE threat model: scope, data flow diagram, intruder model, threats and mitigations |

## Basic security principles

- Least privilege: roles get only the rights they need, Google OAuth scope is as small as possible.
- Secrets stay out of the repository (`credentials.json`, tokens, `*.db`, `settings.json`).
- Data from the Google Sheet is untrusted: validate it and never run it as formulas or template tags.
- Passwords are stored only as salted hashes. The OAuth token is encrypted with DPAPI.
- Important actions and security events are written to an audit log without secrets.
