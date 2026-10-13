#!/usr/bin/env python3
"""
Upserts one DK customer-directory document via the Cosmos SQL API's REST endpoint, AAD-
authenticated. Not az CLI: `az cosmosdb sql` only covers control-plane operations (accounts,
databases, containers) - there is no item-level create/upsert command. This is a thin,
dependency-free (stdlib only) client for that one call.

Usage: seed-dk-customer.py <account> <database> <container> <clientId> <customerName>

Requires the signed-in az CLI user to already hold a Cosmos DB data-plane RBAC role on the
account (granted by the calling PowerShell script immediately before this runs).
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

if len(sys.argv) != 6:
    fail("usage: seed-dk-customer.py <account> <database> <container> <clientId> <customerName>")

account, database, container, client_id, customer_name = sys.argv[1:6]

# On Windows "az" resolves to az.cmd via PATHEXT, which subprocess.run does not search
# unless shell=True. Resolving the real path avoids needing a shell at all.
az_path = shutil.which("az")
if az_path is None:
    fail("az CLI not found on PATH")

token_result = subprocess.run(
    [az_path, "account", "get-access-token", "--resource", f"https://{account}.documents.azure.com",
     "--query", "accessToken", "-o", "tsv"],
    capture_output=True, text=True,
)
if token_result.returncode != 0:
    fail(f"could not acquire an AAD token for the Cosmos account: {token_result.stderr.strip()}")

token = token_result.stdout.strip()
if not token:
    fail("acquired an empty AAD token")

document = {
    "id": client_id,
    "clientId": client_id,
    "customerName": customer_name,
    "active": True,
    "createdAtUtc": datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%S.%fZ"),
    "docType": "dkCustomerDirectory",
}
body = json.dumps(document).encode("utf-8")

url = f"https://{account}.documents.azure.com/dbs/{database}/colls/{container}/docs"
auth_header = urllib.parse.quote(f"type=aad&ver=1.0&sig={token}", safe="")
ms_date = datetime.now(timezone.utc).strftime("%a, %d %b %Y %H:%M:%S GMT")

request = urllib.request.Request(url, data=body, method="POST")
request.add_header("Authorization", auth_header)
request.add_header("x-ms-date", ms_date)
request.add_header("x-ms-version", "2020-07-15")
request.add_header("Content-Type", "application/json")
request.add_header("x-ms-documentdb-is-upsert", "True")
request.add_header("x-ms-documentdb-partitionkey", json.dumps([client_id]))

try:
    with urllib.request.urlopen(request, timeout=30) as response:
        response.read()
        print(f"OK: upserted {client_id} -> {customer_name} (HTTP {response.status})")
except urllib.error.HTTPError as ex:
    detail = ex.read().decode("utf-8", errors="replace")
    fail(f"Cosmos returned HTTP {ex.code}: {detail[:500]}")
except urllib.error.URLError as ex:
    fail(f"request failed: {ex.reason}")
