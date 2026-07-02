#!/usr/bin/env python3
"""Minimal App Store Connect API client for Gravitile publishing automation.

Reads the team API key from ~/.appstoreconnect/private_keys/AuthKey_<KEY_ID>.p8.
No secrets live in this file. Usage: import from sibling scripts or run ad-hoc:
    python3 Tools/ascapi.py GET /v1/apps
"""
import base64
import hashlib
import json
import sys
import time
import urllib.request
import urllib.error
from pathlib import Path

from cryptography.hazmat.primitives import hashes, serialization
from cryptography.hazmat.primitives.asymmetric import ec
from cryptography.hazmat.primitives.asymmetric.utils import decode_dss_signature

KEY_ID = "V9VT258MM6"
ISSUER_ID = "2a885728-387b-4f10-9042-f8f089819dc8"
KEY_PATH = Path.home() / ".appstoreconnect/private_keys" / f"AuthKey_{KEY_ID}.p8"
BASE = "https://api.appstoreconnect.apple.com"

APP_ID = "6786840477"          # Gravitile — Tumbling Merge
BUNDLE_ID = "com.flutterly.gravitile"


def _b64url(data: bytes) -> str:
    return base64.urlsafe_b64encode(data).rstrip(b"=").decode()


def make_token() -> str:
    now = int(time.time())
    header = {"alg": "ES256", "kid": KEY_ID, "typ": "JWT"}
    payload = {"iss": ISSUER_ID, "iat": now, "exp": now + 19 * 60, "aud": "appstoreconnect-v1"}
    signing_input = _b64url(json.dumps(header).encode()) + "." + _b64url(json.dumps(payload).encode())
    key = serialization.load_pem_private_key(KEY_PATH.read_bytes(), password=None)
    der_sig = key.sign(signing_input.encode(), ec.ECDSA(hashes.SHA256()))
    r, s = decode_dss_signature(der_sig)
    raw = r.to_bytes(32, "big") + s.to_bytes(32, "big")
    return signing_input + "." + _b64url(raw)


def request(method: str, path: str, body=None, content_type="application/json", raw_url=None, extra_headers=None):
    url = raw_url or (BASE + path)
    headers = {"Authorization": f"Bearer {make_token()}"}
    data = None
    if body is not None:
        if isinstance(body, (dict, list)):
            data = json.dumps(body).encode()
            headers["Content-Type"] = content_type
        else:
            data = body
            headers["Content-Type"] = content_type
    if extra_headers:
        headers.update(extra_headers)
    req = urllib.request.Request(url, data=data, headers=headers, method=method)
    try:
        with urllib.request.urlopen(req) as resp:
            text = resp.read().decode() if resp.length != 0 else ""
            return resp.status, json.loads(text) if text else {}
    except urllib.error.HTTPError as e:
        detail = e.read().decode()
        try:
            detail = json.loads(detail)
        except Exception:
            pass
        return e.code, detail


def upload_asset(upload_operations, file_bytes: bytes):
    """Execute the uploadOperations returned by an asset reservation."""
    for op in upload_operations:
        chunk = file_bytes[op["offset"]: op["offset"] + op["length"]]
        headers = {h["name"]: h["value"] for h in op.get("requestHeaders", [])}
        req = urllib.request.Request(op["url"], data=chunk, headers=headers, method=op["method"])
        with urllib.request.urlopen(req) as resp:
            assert 200 <= resp.status < 300, f"chunk upload failed: {resp.status}"


def md5(file_bytes: bytes) -> str:
    return hashlib.md5(file_bytes).hexdigest()


if __name__ == "__main__":
    method, path = sys.argv[1], sys.argv[2]
    body = json.loads(sys.argv[3]) if len(sys.argv) > 3 else None
    status, out = request(method, path, body)
    print(status)
    print(json.dumps(out, indent=2)[:4000])
