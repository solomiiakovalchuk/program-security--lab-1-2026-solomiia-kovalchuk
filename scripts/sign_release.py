#!/usr/bin/env python3
"""
Signs a published build (SEC-16). Writes release.manifest.json and release.manifest.sig
into the published folder: SHA-256 of every file (except the manifest files) signed with
an offline ECDSA P-256 key (DER signature, SHA-256). The private key stays out of git.

Usage: python scripts/sign_release.py --dir <published folder> --key config/release-signing.key
"""
import argparse
import base64
import hashlib
import json
import sys
from pathlib import Path

from cryptography.hazmat.primitives import hashes, serialization
from cryptography.hazmat.primitives.asymmetric import ec

MANIFEST = "release.manifest.json"
SIGNATURE = "release.manifest.sig"


def build_manifest(folder: Path, version: str) -> bytes:
    files = []
    for p in sorted(folder.rglob("*")):
        if p.is_file() and p.name not in (MANIFEST, SIGNATURE):
            files.append({"path": p.relative_to(folder).as_posix(), "sha256": hashlib.sha256(p.read_bytes()).hexdigest()})
    files.sort(key=lambda f: f["path"])
    manifest = {"product": "ReportsWpf", "version": version, "files": files}
    return json.dumps(manifest, separators=(",", ":"), ensure_ascii=False).encode("utf-8")


def main() -> int:
    parser = argparse.ArgumentParser(description="Sign a published build folder.")
    parser.add_argument("--dir", required=True, help="published folder (dotnet publish output)")
    parser.add_argument("--key", required=True, help="PEM ECDSA P-256 private key, e.g. config/release-signing.key")
    parser.add_argument("--version", default="1.0.0")
    args = parser.parse_args()

    folder = Path(args.dir)
    if not folder.is_dir():
        print("not a folder:", folder, file=sys.stderr)
        return 1

    key = serialization.load_pem_private_key(Path(args.key).read_bytes(), password=None)
    if not isinstance(key, ec.EllipticCurvePrivateKey) or key.curve.name != "secp256r1":
        print("the key must be ECDSA P-256", file=sys.stderr)
        return 1

    manifest = build_manifest(folder, args.version)
    (folder / MANIFEST).write_bytes(manifest)
    (folder / SIGNATURE).write_bytes(key.sign(manifest, ec.ECDSA(hashes.SHA256())))

    spki = key.public_key().public_bytes(serialization.Encoding.DER, serialization.PublicFormat.SubjectPublicKeyInfo)
    print("files signed:", len(json.loads(manifest)["files"]))
    print("public key (must equal config/release-public.spki):", base64.b64encode(spki).decode("ascii"))
    return 0


if __name__ == "__main__":
    sys.exit(main())
