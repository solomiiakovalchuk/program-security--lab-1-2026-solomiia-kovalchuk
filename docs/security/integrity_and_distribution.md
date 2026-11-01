# Integrity and Distribution - ReportsWpf

| | |
| --- | --- |
| Name | Integrity and distribution protection - ReportsWpf |
| Version | 0.1.0 |
| Status | Draft |
| Classification | Internal |
| Last update | 2026-10-22 |
| Owner | Solomiia Kovalchuk |
| Related documents | [security_policy.md](security_policy.md), [threat_model.md](threat_model.md), [crypto_strategy.md](crypto_strategy.md), [roadmap.md](../specification/roadmap.md) |

## Purpose

This document fixes how the project protects its distributed build against tampering, which license and distribution model it uses, and what is left out on purpose. It extends the [security policy](security_policy.md) (SEC-16) and answers threat T-21 in the [threat model](threat_model.md). It is written for Lab 5 and implemented as `Reports.Integrity`.

## What needs integrity protection

| Asset | Risk | Protection |
| --- | --- | --- |
| Application binaries and assemblies (`Reports.*.dll`, the executable) | Patched or replaced by a trojanized copy | Signed release manifest with SHA-256 per file |
| `config/settings.json` and `credentials.json` | Edited by the user or by malware | Content is already protected at rest (DPAPI + AES-GCM, see [crypto_strategy.md](crypto_strategy.md)). The shipped defaults are covered by the signed manifest |
| `reports.db` | Personal data, changed with a SQLite tool | Out of scope here, see the T-14 decision in the threat model. Only an MDC (SHA-256) can be computed on demand |

## Why not MDC or MAC alone

- MDC (a plain hash such as SHA-256) detects a change only if the hash itself is trusted. An attacker who can change a file can also change the hash list next to it and recompute it, so a hash alone proves nothing about who made the build.
- MAC (for example HMAC) needs a shared secret. The client has to verify, so the key must sit in the client, and anyone who extracts it can forge a valid MAC for a modified build. The Lab 5 theory points to the same limit: symmetric protection of a distributed program only moves the secret into the program.
- A digital signature splits the roles. The private key signs and never leaves the developer. The client holds only the public key, which is enough to verify and useless for forging.

## Chosen mechanism

A signed release manifest:

1. The release manifest lists the critical files: relative path and SHA-256 per file (the MDC), plus product and version.
2. The developer signs the manifest bytes with an offline ECDSA P-256 private key (SHA-256, DER signature).
3. The client ships the public key (SubjectPublicKeyInfo), the manifest and the signature.
4. The client first verifies the signature (authenticity of the manifest), then recomputes the SHA-256 of each listed file (integrity). A bad signature, a modified file or a missing file makes the report invalid.

The private key lives in `config/release-signing.key` on the developer PC and is excluded by `.gitignore`. The public key, the manifest, the signature and the sample files are committed. Only the .NET base library is used (`System.Security.Cryptography`, `System.Text.Json`), in line with the "no own cryptography" rule.

## Distribution model

ReportsWpf is distributed as Freeware under a Perpetual license: an internal departmental reporting tool, free of charge, with no expiry and no per-user fee. There is no commercial DRM, no activation and no trial period.

Models considered and rejected: Trial and Shareware do not apply, because nothing is sold and there is no reason to limit time or features. For this reason the protection goal is the authenticity and integrity of the distributed build and of the config defaults, not copy prevention.

## What is implemented

- `src/Reports.Integrity`: `Sha256Mdc`, `ISignatureVerifier` / `EcdsaSignatureVerifier`, `FileManifest`, `ManifestVerifier`, `IntegrityReport`, `IntegrityException`.
- Test fixture: `scripts/lab5_build_manifest.py` signs the small folder `config/sample-dist/` and writes `config/release-public.spki`, `config/release.manifest.json` and `config/release.manifest.sig`. It exists only so the tests can check that the C# verifier accepts what the Python tool signed; it is not the release.
- Tests in `tests/Reports.Integrity.Tests`: valid and tampered signature, wrong public key, intact release, modified file, deleted file, tampered manifest, and a check that the C# verifier accepts the manifest signed by the Python tool.
- Startup check (`StartupIntegrityCheck`, run first in `App.OnStartup`): reads `release.manifest.json` and `release.manifest.sig` from the application folder and the public key embedded in `Reports.Desktop` (linked from `config/release-public.spki`). Valid: the app starts. Bad signature, modified or missing file, missing signature or unreadable manifest: the app records `App.IntegrityCheckFailed` (reason, count), shows a message and exits. No manifest at all (unsigned development build): the app starts, records `App.IntegrityUnsigned` and shows a warning.
- Signing the real release: `powershell -File scripts/publish_release.ps1 -Out publish` runs `dotnet publish src/Reports.Desktop -c Release` and then `scripts/sign_release.py`. The manifest lists every published file (about 70: `Рапорти.exe`, all `Reports.*.dll`, dependencies, `.deps.json`, `.pdb`) with its SHA-256 and is signed with `config/release-signing.key`; the two manifest files are written into the same folder and are not listed. Sign last: any later change to a file makes the build fail the check.
- Tests: `StartupIntegrityTests` (intact, tampered file, tampered manifest, wrong key, no manifest, missing file).
- Checked on the real published and signed build (app started from the publish folder with an isolated data folder): unchanged release starts with no warning; one byte of `Reports.Core.dll` changed, the manifest edited, a file deleted, or a changed dll with a manifest re-signed by another key: the app shows the integrity message, records `App.IntegrityCheckFailed` and exits with code 1; no manifest: the developer warning and `App.IntegrityUnsigned`. If an assembly the app needs to start is missing, the check still reports the failure first; the audit event is then written only if the database layer can load.
- Not covered: a file added to the folder that the manifest does not list, and replacing the whole folder with an older release that was also signed (the manifest has a version but the app does not compare it).

## Out of scope / accepted

- Anti-debugging, packing, virtualization and hardware binding: disproportionate for an internal tool with no secret algorithm to hide.
- Code obfuscation of the application: the Lab 5 exercise (`scripts/lab5_obfuscation_exercise.py`) shows it only raises the effort of reverse engineering, and the project has no logic that needs it.
- The verification runs on the user's PC, so malware running as the same user can patch out the check or replace the public key together with the program. The signed manifest proves a build came from the developer and was not changed afterwards, but it cannot protect a PC that is already compromised. This matches the residual risk in T-21.
