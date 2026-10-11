#!/usr/bin/env python3
"""
LAB 3, exercise 2 (task items 8-12 of the assignment): local brute-force
demo against self-generated SHA-256 and bcrypt hashes, for the student's
own security course. Items 2-7 (base-string bcrypt hashing and pairwise
comparison) were done manually in CyberChef and are not part of this
script.
"""
import hashlib
import json
import time

import bcrypt

out = {}

# ---------- item 8: target string ----------
prizv = "Ковальчук"
pass_string = f"pass: {prizv}"
out["item8"] = {"string": pass_string}

# ---------- item 10: Hash #5 = SHA-256(pass_string) ----------
hash5_sha256 = hashlib.sha256(pass_string.encode("utf-8")).hexdigest()
out["item10"] = {"string": pass_string, "hash5_sha256": hash5_sha256}

# ---------- item 11: Hash #6 = bcrypt(pass_string, 12 rounds) ----------
hash6_bcrypt = bcrypt.hashpw(pass_string.encode("utf-8"), bcrypt.gensalt(rounds=12))
out["item11"] = {"string": pass_string, "hash6_bcrypt": hash6_bcrypt.decode()}

# ---------- item 9: 20-entry "dictionary", real candidate last ----------
decoys = [
    "pass: Admin", "pass: 123456", "pass: Password", "pass: Operator",
    "pass: Shevchenko", "pass: Kovalenko", "pass: Bondarenko", "pass: Tkachenko",
    "pass: qwerty", "pass: Petrenko", "pass: Melnyk", "pass: Kravchuk",
    "pass: Boyko", "pass: Koval", "pass: Savchenko", "pass: Moroz",
    "pass: Lysenko", "pass: Ivanenko", "pass: Marchenko",
]
assert len(decoys) == 19
wordlist = decoys + [pass_string]
assert len(wordlist) == 20
assert wordlist[-1] == pass_string
out["item9_wordlist"] = wordlist


def bruteforce_sha256(target_hex: str, candidates: list[str]) -> dict:
    """9.1-9.6: try each candidate's SHA-256 against the target hash in order."""
    t0 = time.perf_counter_ns()
    found = None
    tries = 0
    for cand in candidates:
        tries += 1
        h = hashlib.sha256(cand.encode("utf-8")).hexdigest()
        if h == target_hex:
            found = cand
            break
    t1 = time.perf_counter_ns()
    return {
        "algorithm": "SHA-256",
        "found": found,
        "attempts": tries,
        "elapsed_microseconds": (t1 - t0) / 1000.0,
    }


def bruteforce_bcrypt(target_hash: bytes, candidates: list[str]) -> dict:
    """9.1-9.6: try each candidate against the target bcrypt hash via checkpw."""
    t0 = time.perf_counter_ns()
    found = None
    tries = 0
    for cand in candidates:
        tries += 1
        if bcrypt.checkpw(cand.encode("utf-8"), target_hash):
            found = cand
            break
    t1 = time.perf_counter_ns()
    return {
        "algorithm": "bcrypt (12 rounds)",
        "found": found,
        "attempts": tries,
        "elapsed_microseconds": (t1 - t0) / 1000.0,
    }


# ---------- item 12: run both attacks ----------
out["item12_bruteforce_hash5_sha256"] = bruteforce_sha256(hash5_sha256, wordlist)
out["item12_bruteforce_hash6_bcrypt"] = bruteforce_bcrypt(hash6_bcrypt, wordlist)

with open("lab3_bruteforce_results.json", "w", encoding="utf-8") as f:
    json.dump(out, f, ensure_ascii=False, indent=2)

print("OK, wrote lab3_bruteforce_results.json")
print(json.dumps(out, ensure_ascii=False, indent=2))
