#!/usr/bin/env python3
"""
LAB 5 obfuscation exercise (Appendix A structure).
Three functionally identical checks of Key #1:
  v0 - plain comparison
  v1 - SHA-256 of the key is compared instead of the literal
  v2 - hidden constants, flattened control flow, AES-GCM gate
Run `python lab5_obfuscation_exercise.py regen` to print the v2 gate blob.
"""
import hashlib
import json
import os
import sys
from pathlib import Path

from cryptography.exceptions import InvalidTag
from cryptography.hazmat.primitives.ciphers.aead import AESGCM

KEY1 = "2026_IPZs-24-1_Kovalchuk_Solomiia_variant:05_"

# ---------------------------------------------------------------- v0 ----


def check_v0(pw: str) -> bool:
    return pw == KEY1


# ---------------------------------------------------------------- v1 ----

# Computed once with: hashlib.sha256(KEY1.encode("utf-8")).hexdigest()
_V1_HASH = "59e7fa544c8c1c158d6648c0c373cf100254d7e8972a9038ba11e5a3db199df0"


def check_v1(pw: str) -> bool:
    return hashlib.sha256(pw.encode("utf-8")).hexdigest() == _V1_HASH


# ---------------------------------------------------------------- v2 ----


def _s(codes):
    # string built from char codes, so no readable literal is in the file
    return "".join(map(chr, codes))


def _salt():
    # split byte constant, reassembled in a different order
    z = (b"\x13\xa9", b"p7", b"lic", b"2026")
    return z[2] + z[1] + z[3] + z[0]


def _mix(a, b):
    return bytes(x ^ y for x, y in zip(a, b))


def _derive(pw):
    # multi-step transform: reorder + xor with a position mask, then chained sha256
    b = pw.encode("utf-8")
    fwd = bytes(v ^ ((i * 29 + 0x53) & 0xFF) for i, v in enumerate(b))
    h1 = hashlib.sha256(fwd + _salt() + bytes(reversed(b))).digest()
    h2 = hashlib.sha256(_mix(h1, hashlib.sha256(_salt()).digest()) + len(b).to_bytes(2, "big")).digest()
    return hashlib.sha256(h2[16:] + h2[:16] + _s((111, 107)).encode()).digest()


_AAD = _s((108, 97, 98, 53))  # "lab5"
_PAYLOAD = _s((111, 107)).encode()  # b"ok"

# nonce (12 bytes) + ciphertext + tag, filled in by `regen`
_BLOB = bytes.fromhex("7144f86651b9739502e7d5fafec50bd975cc47622134db8e1738a152c4e3")


def _regen(pw):
    nonce = os.urandom(12)
    ct = AESGCM(_derive(pw)).encrypt(nonce, _PAYLOAD, _AAD.encode())
    return (nonce + ct).hex()


def _opaque(x):
    # x*x + x is always even, so this is always True
    return ((x * x + x) & 1) == 0


def _gate(pw):
    nonce, ct = _BLOB[:12], _BLOB[12:]
    try:
        return AESGCM(_derive(pw)).decrypt(nonce, ct, _AAD.encode()) == _PAYLOAD
    except InvalidTag:
        return False


def check_v2(pw: str) -> bool:
    result = False
    junk = 0x6D ^ 0x54
    state = 0x21 ^ 0x21
    while True:
        match state:
            case 0:
                state = 3 if _opaque(junk) else 9  # opaque predicate, always 3
            case 3:
                junk = ((junk << 3) ^ (junk >> 2) ^ 0x31B4) & 0xFFFF  # junk
                state = 5
            case 5:
                result = _gate(pw)
                state = 7 if result else 8
            case 7:
                result = result and _opaque(junk + 1)
                state = 11
            case 8:
                junk ^= 0x55  # junk, result stays False
                state = 11
            case 9:
                result = True  # dead branch, never reached
                state = 11
            case 11:
                return result
            case _:
                state = 11


# ------------------------------------------------------------ verify ----


def verify_equivalence():
    inputs = [KEY1, "", "wrong", "Secret123", KEY1 + " ", KEY1.swapcase()]
    rows = []
    for s in inputs:
        a, b, c = check_v0(s), check_v1(s), check_v2(s)
        rows.append({"input": s, "v0": a, "v1": b, "v2": c, "agree": a == b == c})
    assert all(r["agree"] for r in rows), "variants disagree"
    out = Path(__file__).with_name("lab5_obfuscation_results.json")
    out.write_text(json.dumps(rows, ensure_ascii=False, indent=2), encoding="utf-8")
    return rows


if __name__ == "__main__":
    if len(sys.argv) > 1 and sys.argv[1] == "regen":
        print(_regen(KEY1))
    else:
        rows = verify_equivalence()
        print(f"KEY1 = {KEY1}")
        print(f"sha256(KEY1) = {hashlib.sha256(KEY1.encode()).hexdigest()}")
        for r in rows:
            print(f"{r['input']!r:55} v0={r['v0']!s:5} v1={r['v1']!s:5} v2={r['v2']!s:5} agree={r['agree']}")
        print("All variants are equivalent on", len(rows), "inputs")
        print("Wrote scripts/lab5_obfuscation_results.json")
