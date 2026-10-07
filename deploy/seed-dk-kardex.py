#!/usr/bin/env python3
"""
Claims DK kardexes (customerRefNumber) for a client id in the dk-kardex-map container,
without needing a registration. Same result as the first registration claiming it.

Usage: seed-dk-kardex.py <account> <database> <container> <clientId> <kardex> [<kardex> ...]

Refuses to overwrite: a kardex already owned by another client id is reported and skipped
(HTTP 409 from Cosmos), matching the API's first-write-wins rule.
Requires the signed-in az CLI user to hold a Cosmos data-plane role (granted by the caller).
"""
import json
import shutil
import subprocess
import sys
import urllib.error
import urllib.parse
import urllib.request
from datetime import datetime, timezone


def fail(message: str) -> None:
    print(f"ERROR: {message}", file=sys.stderr)
    sys.exit(1)


if len(sys.argv) < 6:
    fail("usage: seed-dk-kardex.py <account> <database> <container> <clientId> <kardex> [<kardex> ...]")

account, database, container, client_id = sys.argv[1:5]
kardexes = sys.argv[5:]

az_path = shutil.which("az")
if az_path is None:
    fail("az CLI not found on PATH")

token_result = subprocess.run(
    [az_path, "account", "get-access-token", "--resource", f"https://{account}.documents.azure.com",
     "--query", "accessToken", "-o", "tsv"],
    capture_output=True, text=True,
)
token = token_result.stdout.strip()
if token_result.returncode != 0 or not token:
    fail(f"could not acquire an AAD token for the Cosmos account: {token_result.stderr.strip()}")

url = f"https://{account}.documents.azure.com/dbs/{database}/colls/{container}/docs"
auth_header = urllib.parse.quote(f"type=aad&ver=1.0&sig={token}", safe="")
exit_code = 0

for kardex in kardexes:
    document = {
        "id": kardex,
        "customerRefNumber": kardex,
        "clientId": client_id,
        "claimedAtUtc": datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%S.%fZ"),
        "claimedBy": "manual",
        "docType": "dkKardexMap",
    }
    request = urllib.request.Request(url, data=json.dumps(document).encode("utf-8"), method="POST")
    request.add_header("Authorization", auth_header)
    request.add_header("x-ms-date", datetime.now(timezone.utc).strftime("%a, %d %b %Y %H:%M:%S GMT"))
    request.add_header("x-ms-version", "2020-07-15")
    request.add_header("Content-Type", "application/json")
    request.add_header("x-ms-documentdb-partitionkey", json.dumps([kardex]))

    try:
        with urllib.request.urlopen(request, timeout=30) as response:
            response.read()
            print(f"OK: claimed {kardex} for {client_id}")
    except urllib.error.HTTPError as ex:
        if ex.code == 409:
            print(f"SKIPPED: {kardex} is already claimed (not overwritten)")
        else:
            print(f"ERROR: {kardex}: HTTP {ex.code}: {ex.read().decode('utf-8', errors='replace')[:300]}", file=sys.stderr)
            exit_code = 1

sys.exit(exit_code)
