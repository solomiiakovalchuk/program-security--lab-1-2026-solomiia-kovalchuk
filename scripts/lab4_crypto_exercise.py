#!/usr/bin/env python3
"""
LAB 4 standalone exercises (items 2-8 of the assignment).
Classical ciphers (Caesar, columnar transposition) and a known-plaintext
CTR-mode keystream-reuse attack demo, all against self-generated data for
the student's own security course.
"""
import json
import os
from Crypto.Cipher import AES
from Crypto.Util import Counter

out = {}

rik = "2026"
grupa = "ІПЗс-24-1"
prizv = "Ковальчук"
imya = "Соломія"
variant = "05"  # item 2 of lab 4 explicitly requires zero-padding to 2 digits

base = f"{rik}_{grupa}_{prizv}_{imya}_варіант:{variant}_"
out["item2"] = {"base_string": base, "utf8_byte_length": len(base.encode("utf-8"))}

# ============================================================
# item 3: Caesar cipher
# ============================================================
UA_ALPHABET = "абвгґдеєжзиіїйклмнопрстуфхцчшщьюя"
CAESAR_KEY = 7


def caesar_encrypt(text: str, key: int) -> str:
    out_chars = []
    for ch in text:
        lower = ch.lower()
        if lower in UA_ALPHABET:
            idx = UA_ALPHABET.index(lower)
            new_ch = UA_ALPHABET[(idx + key) % len(UA_ALPHABET)]
            out_chars.append(new_ch.upper() if ch.isupper() else new_ch)
        else:
            out_chars.append(ch)  # digits, punctuation, Latin letters: unchanged
    return "".join(out_chars)


caesar_ct = caesar_encrypt(base, CAESAR_KEY)
out["item3_caesar"] = {
    "alphabet": UA_ALPHABET,
    "key_shift": CAESAR_KEY,
    "plaintext": base,
    "ciphertext": caesar_ct,
    "note": "Only Ukrainian Cyrillic letters are shifted; digits, '_', '-', ':' stay as-is (classic-Caesar scope).",
}

# ============================================================
# item 4: columnar transposition cipher
# ============================================================
TRANSPOSITION_KEY = "БЕЗПЕКА"  # column order key


def columnar_transposition_encrypt(text: str, key: str) -> str:
    key_order = sorted(range(len(key)), key=lambda i: (key[i], i))
    ncols = len(key)
    nrows = -(-len(text) // ncols)  # ceil
    pad_char = "_"
    padded_text = text + pad_char * (nrows * ncols - len(text))
    grid = [padded_text[r * ncols:(r + 1) * ncols] for r in range(nrows)]
    ciphertext_parts = []
    for col in key_order:
        for row in grid:
            ciphertext_parts.append(row[col])
    return "".join(ciphertext_parts)


transp_ct = columnar_transposition_encrypt(base, TRANSPOSITION_KEY)
out["item4_transposition"] = {
    "key": TRANSPOSITION_KEY,
    "plaintext": base,
    "ciphertext": transp_ct,
    "note": "Columnar transposition, padded with '_' to a full rectangle; columns read out in alphabetical key order.",
}

# ============================================================
# item 5: comparison / analysis
# ============================================================
out["item5_analysis"] = {
    "caesar": (
        "Substitution: every letter is replaced by another fixed letter of the "
        "same alphabet (shift 7). Length and symbol positions are unchanged; "
        "digits/punctuation leak unchanged. Letter FREQUENCIES are preserved "
        "(just relabeled), so classic frequency analysis still recovers the key."
    ),
    "transposition": (
        "Permutation: the same multiset of characters as the plaintext (letters, "
        "digits, punctuation all appear, just padded with '_'), only their ORDER "
        "changes. Letter frequencies of the plaintext are fully preserved "
        "(anagram of the input), but word/field boundaries are destroyed."
    ),
    "overall": (
        "Neither on its own hides the fact that this is Ukrainian/Latin text "
        "built from a fixed template: substitution keeps structure+length, "
        "transposition keeps the alphabet's frequency profile. Both are "
        "pedagogical/historical and are explicitly NOT acceptable for protecting "
        "real data per the project's security policy (modern authenticated "
        "ciphers only, see Reports.Security / crypto_strategy.md)."
    ),
}

# ============================================================
# item 6: AES-256-CTR encryption
# ============================================================
key1 = os.urandom(32)   # AES-256 key
iv1 = os.urandom(16)    # 128-bit initial counter value

ctr = Counter.new(128, initial_value=int.from_bytes(iv1, "big"))
cipher = AES.new(key1, AES.MODE_CTR, counter=ctr)
plaintext_bytes = base.encode("utf-8")
ciphertext1 = cipher.encrypt(plaintext_bytes)

out["item6_aes_ctr"] = {
    "plaintext": base,
    "key1_hex": key1.hex(),
    "vector1_iv_hex": iv1.hex(),
    "ciphertext1_hex": ciphertext1.hex(),
}

# ============================================================
# item 7: known-plaintext CTR keystream-reuse attack
# ============================================================
# 7.1: attacker knows (plaintext, ciphertext1, algorithm=AES-CTR); key is unknown.
# 7.2: keystream = ciphertext XOR plaintext
def xor_bytes(a: bytes, b: bytes) -> bytes:
    return bytes(x ^ y for x, y in zip(a, b))


gamma1 = xor_bytes(ciphertext1, plaintext_bytes)
out["item7_2_gamma1"] = {"gamma1_hex": gamma1.hex()}

# 7.3: "alternative" plaintext, same template but variant -> "99"
alt_plaintext = f"{rik}_{grupa}_{prizv}_{imya}_варіант:99_"
alt_bytes = alt_plaintext.encode("utf-8")
out["item7_3_alt_plaintext"] = {
    "alt_plaintext": alt_plaintext,
    "utf8_byte_length": len(alt_bytes),
    "same_length_as_original": len(alt_bytes) == len(plaintext_bytes),
}

# 7.4: forged ciphertext2 = gamma1 XOR alt_plaintext (attacker needs NO key)
assert len(gamma1) == len(alt_bytes), "keystream must cover the alt plaintext length"
ciphertext2 = xor_bytes(gamma1, alt_bytes)
out["item7_4_ciphertext2"] = {"ciphertext2_hex": ciphertext2.hex()}

# ============================================================
# item 8: verification -- decrypt ciphertext2 with the (attacker-unknown) key/IV
# ============================================================
ctr_check = Counter.new(128, initial_value=int.from_bytes(iv1, "big"))
cipher_check = AES.new(key1, AES.MODE_CTR, counter=ctr_check)
decrypted2 = cipher_check.decrypt(ciphertext2)
decrypted2_text = decrypted2.decode("utf-8")

out["item8_verification"] = {
    "decrypted_ciphertext2_with_key1_vector1": decrypted2_text,
    "matches_alt_plaintext": decrypted2_text == alt_plaintext,
    "conclusion": (
        "The attacker, without ever learning Key #1 or Vector #1, forged a "
        "ciphertext that decrypts under the real key/IV to an arbitrary chosen "
        "message. This is the classic CTR/stream-cipher keystream-reuse "
        "vulnerability: reusing (key, nonce) -- or, equivalently, recovering the "
        "keystream once via a known-plaintext pair -- breaks both confidentiality "
        "and integrity for every other message protected with that same "
        "keystream. It demonstrates why plain CTR provides NO authenticity, and "
        "why the project standard is authenticated encryption (AES-GCM) with a "
        "unique nonce per message, never a reused (key, nonce) pair."
    ),
}

with open("lab4_results.json", "w", encoding="utf-8") as f:
    json.dump(out, f, ensure_ascii=False, indent=2)

print("OK, wrote lab4_results.json")
print(json.dumps(out, ensure_ascii=False, indent=2))
