# Cryptographic Strategy - ReportsWpf

| | |
| --- | --- |
| Name | Cryptographic strategy - ReportsWpf |
| Version | 0.1.0 |
| Status | Draft |
| Classification | Internal |
| Last update | 2026-10-17 |
| Owner | Solomiia Kovalchuk |
| Related documents | [security_policy.md](security_policy.md), [threat_model.md](threat_model.md), [architecture_description.md](../architecture/architecture_description.md), [Identity business_logic.md](../specification/modules/Identity/business_logic.md) |

## Purpose

This document fixes, in one place, which data the project treats as cryptographically sensitive, when it is protected, where the keys live and how they are rotated. It extends the [security policy](security_policy.md) ("No own cryptography") with the concrete mechanism, written for Lab 4 and implemented as `Reports.Security`.

## What is sensitive and how it is protected today

| Data | Sensitivity | Protection | Owner document |
| --- | --- | --- | --- |
| Account password | Secret, never stored | PBKDF2-HMAC-SHA256 hash only, salted, never recoverable | [Identity business_logic.md](../specification/modules/Identity/business_logic.md), SEC-01 |
| Google OAuth token (`token-rw\`) | Secret, grants sheet access | DPAPI, current Windows user (SEC-05) | [security_policy.md](security_policy.md) |
| `credentials.json` (Google OAuth client) | Secret | DPAPI, current Windows user (SEC-05) | [security_policy.md](security_policy.md) |
| `reports.db` content (missions, people) | Personal data, not a secret key | File rights + disk encryption (BitLocker), not application-level encryption | [threat_model.md](threat_model.md), decision on T-14 |

This table does not change the T-14 decision: `reports.db` stays unencrypted in 0.1.0, protected only by owner-only file rights and the disk encryption assumption.

## Why a dedicated `Reports.Security` module

SEC-05 already requires DPAPI for the token and `credentials.json`. `ProtectedData.Protect` alone is an authenticated, OS-managed mechanism, but it ties every protected value to one Windows profile and gives no place to apply the project's own key-separation rule ("do not use the same key for different purposes" - see the Lab 4 theory handout). `Reports.Security` adds two things on top of DPAPI, not instead of it:

1. **Key separation with HKDF.** One random 32-byte master key is generated once and is itself the only value protected directly by DPAPI (`IMasterKeyProvider` / `DpapiMasterKeyProvider`). Every concrete secret (the OAuth token, the client secret, and any future purpose) gets its own derived key via `HKDF-SHA256(masterKey, info = purpose string)` (`HkdfPurposeKeyDeriver`), so a problem with one derived key (e.g. a bug that logs it) does not expose the others and does not expose the master key.
2. **Authenticated encryption for the data itself.** The derived key encrypts the actual bytes with AES-256-GCM (`AesGcmSecretProtector`): a random 96-bit nonce per call, a 128-bit authentication tag, output as `nonce || ciphertext || tag`. A modified token or a corrupted file is rejected by the tag check (`CryptographicException`) instead of being silently decrypted into garbage that the app would try to use. This follows the lab theory handout's guidance to prefer ready authenticated-encryption constructions (AES-GCM) over plain confidentiality-only modes (CTR, ECB, CBC) for anything beyond a learning exercise.

Composition at startup (planned wiring, stage S6, not yet called from `Reports.Desktop` because there is no host application yet):

```
masterKey = DpapiMasterKeyProvider(path to %AppData%\Рапорти\master.key).GetOrCreateMasterKey()
tokenKey  = HkdfPurposeKeyDeriver.DeriveKey(masterKey, "google-oauth-token")
clientKey = HkdfPurposeKeyDeriver.DeriveKey(masterKey, "google-client-secret")
tokenProtector  = new AesGcmSecretProtector(tokenKey)
clientProtector = new AesGcmSecretProtector(clientKey)
```

`master.key` is written next to `token-rw\` and `credentials.json` under `%AppData%\Рапорти`; it is covered by the same `.gitignore` exclusion as the rest of that folder (SEC-10) and is never committed.

## Key management rules

- Keys are generated only with `RandomNumberGenerator` (`System.Security.Cryptography`), never `System.Random` or a hand-written PRNG (no own cryptography).
- The master key is the only key protected directly by the OS (DPAPI); every other key is derived from it with HKDF and is never stored on disk by itself.
- A nonce is never reused with the same key: `AesGcmSecretProtector` generates a fresh random 96-bit nonce on every `Protect` call. Tests (`AesGcm_NoncesAreUnique_AcrossManyCalls`) check this does not collide across many calls.
- Rotation: if the master key must be rotated (suspected compromise, Windows profile reset), the stored `master.key` is deleted; `DpapiMasterKeyProvider` creates a fresh one on next start, and every value protected under the old key must be re-obtained (the token through a new Google consent, since there is nothing else to "migrate" - the app holds no other AES-GCM-protected value yet).
- Forbidden, same as [security_policy.md](security_policy.md): own cipher implementations, ECB mode, unauthenticated stream/CTR output used directly for anything beyond the Lab 4 teaching exercise below, hard-coded keys, disabling the GCM tag check.

## What is explicitly NOT used for real protection

Lab 4 also required working with classical ciphers (Caesar, columnar transposition) and with AES in CTR mode, to study their properties first-hand (see the Lab 4 report, `ЛР4_Ковальчук_ІПЗс-24-1_БПД.docx`, and `scripts/lab4_crypto_exercise.py` / `scripts/lab4_crypto_results.json` in this repository). Conclusions carried into this strategy:

- **Caesar / transposition**: reversible with pen and paper or a trivial script (frequency analysis for substitution, anagram search for transposition); never used for anything beyond this teaching exercise.
- **AES-CTR without authentication**: the exercise reproduces a classical known-plaintext attack - from one `(plaintext, ciphertext)` pair under a reused keystream, an attacker recovers the keystream and forges a second ciphertext for an arbitrary message, without ever learning the key. This is exactly why `AesGcmSecretProtector` above uses GCM (authenticated, unique nonce per message) and not raw CTR: GCM's tag makes a forged ciphertext fail decryption instead of silently succeeding.

## Verification

Covered by `tests/Reports.Security.Tests`: round-trip of `AesGcmSecretProtector`, tamper detection (flipped byte -> `CryptographicException`), nonce uniqueness across many calls, HKDF determinism and purpose separation, and master-key persistence through `DpapiMasterKeyProvider`. Wiring these protectors into the real `token-rw\` / `credentials.json` storage is stage S6 work (`Reports.Integration`), tracked in the [roadmap](../specification/roadmap.md).

## Related Documents

- [Security policy](security_policy.md): SEC-05 and the "no own cryptography" principle this document implements.
- [Threat model](threat_model.md): T-14, T-15 and the accepted risk on `reports.db`.
- [Identity business_logic.md](../specification/modules/Identity/business_logic.md): the PBKDF2 password-hashing design, a separate concern from the secrets-at-rest protection described here.
