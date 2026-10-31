#!/usr/bin/env python3
"""
LAB 5 dev signing tool. Builds config/release.manifest.json over config/sample-dist
and signs it with an offline ECDSA P-256 key. The private key stays out of git.
"""
import base64
import hashlib
import json
from pathlib import Path

from cryptography.hazmat.primitives import hashes, serialization
from cryptography.hazmat.primitives.asymmetric import ec

ROOT = Path(__file__).resolve().parent.parent
CONFIG = ROOT / "config"
DIST = CONFIG / "sample-dist"
KEY_PATH = CONFIG / "release-signing.key"
PUB_PATH = CONFIG / "release-public.spki"
MANIFEST_PATH = CONFIG / "release.manifest.json"
SIG_PATH = CONFIG / "release.manifest.sig"


def load_or_create_key() -> ec.EllipticCurvePrivateKey:
    if KEY_PATH.exists():
        return serialization.load_pem_private_key(KEY_PATH.read_bytes(), password=None)
    key = ec.generate_private_key(ec.SECP256R1())
    KEY_PATH.write_bytes(
        key.private_bytes(
            serialization.Encoding.PEM,
            serialization.PrivateFormat.PKCS8,
            serialization.NoEncryption(),
        )
    )
    return key


def build_manifest() -> bytes:
    files = []
    for p in sorted(DIST.rglob("*")):
        if p.is_file():
            files.append({"path": p.relative_to(DIST).as_posix(), "sha256": hashlib.sha256(p.read_bytes()).hexdigest()})
    files.sort(key=lambda f: f["path"])
    manifest = {"product": "ReportsWpf", "version": "0.1.0", "files": files}
    return json.dumps(manifest, separators=(",", ":")).encode("utf-8")


def main() -> None:
    key = load_or_create_key()
    spki = key.public_key().public_bytes(
        serialization.Encoding.DER, serialization.PublicFormat.SubjectPublicKeyInfo
    )
    PUB_PATH.write_text(base64.b64encode(spki).decode("ascii"), encoding="ascii")

    manifest = build_manifest()
    MANIFEST_PATH.write_bytes(manifest)
    # DER (Rfc3279 sequence) signature, SHA-256
    SIG_PATH.write_bytes(key.sign(manifest, ec.ECDSA(hashes.SHA256())))

    print("public key :", PUB_PATH.relative_to(ROOT))
    print("manifest   :", MANIFEST_PATH.relative_to(ROOT), f"({len(manifest)} bytes)")
    print("signature  :", SIG_PATH.relative_to(ROOT), f"({SIG_PATH.stat().st_size} bytes)")
    print(manifest.decode("utf-8"))


if __name__ == "__main__":
    main()
