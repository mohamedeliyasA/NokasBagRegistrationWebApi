# Counting Result — Full Lifecycle Test Plan

Step-by-step manual test plan covering registration through every counting-result scenario, for you to
run yourself against TEST. Each step says who does it: **[API]** = an HTTP call you make, **[ON-PREM]** =
a status change you make directly against Nattsafe (however you've been doing it for the earlier test
bags), **[WAIT]** = let the 30-min timer run, or trigger `CountingResultResyncFunction` for a faster
turnaround (see `Operations.md` → Counting Result).

## 0. Setup

```bash
TOKEN=$(curl -s -X POST "https://api-test.nokas.com/oauth2/v2.0/token" \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "grant_type=client_credentials" \
  -d "client_id=1a732120-1149-4d97-98b5-d454d144b383" \
  -d "client_secret=$CLIENT_SECRET" \
  -d "scope=https://nokasb2ctest.onmicrosoft.com/api/nokas/.default" \
  | node -e "let d='';process.stdin.on('data',c=>d+=c);process.stdin.on('end',()=>console.log(JSON.parse(d).access_token))")

API=https://api-nokas-bagreg-v2.azurewebsites.net/api/cash-mgmt
CR=$API/bag-counting-results
```

Re-run the token block whenever it expires (tokens last ~1 hour) or whenever a role changes — roles are
stamped in at issuance.

## 1. Onboard a fresh customer

Rather than standing up a whole new partner (separate Entra app registration + role grant), the cheapest
way to test the *onboarding* path itself is a new `NokasExternalCustomerMapping` row using a **new**
`NokasCustomerNumber`/`CustomerRefNumber` pair, still under your existing test client. This exercises the
real "new customer" code path (`mappingWatermark` detection → backfill) without extra Entra setup.

**[ON-PREM]** Add a row to `BizTalk_Helper.dbo.NokasExternalCustomerMapping`:

```
ClientID = 1a732120-1149-4d97-98b5-d454d144b383
NokasCustomerNumber = <a real Nattsafe customer number you can register bags against>
CustomerRefNumber = <pick something new, e.g. 5790002334999>
BackfillFromDate = NULL   -- or a specific date if you want to test that column too
```

**[WAIT or run resync]** Either wait for the next 30-min tick, or call
`POST .../resync/<NokasCustomerNumber>` per `Operations.md`. Either way this is the run that should log
`backfilledCustomers: 1` for this customer.

**Verify onboarding worked:**

```bash
curl -s "$CR?fromDate=2020-01-01&toDate=$(date -u +%Y-%m-%d)&dateType=registered&customerRefNumber=<your new CustomerRefNumber>" \
  -H "Authorization: Bearer $TOKEN"
```

Expect `200` with an empty or near-empty `items` array (nothing registered yet) and no error — proves the
mapping is live and query-able before any bags exist.

## 2. Register bags [API]

Register at least 3 bags so each can be walked through a different lifecycle. Use **integer-only bag
numbers** (letters work but complicate matching your own on-prem tooling — keep it simple). Use the new
`CustomerRefNumber` from step 1 so everything in this test plan is isolated from your other test data.

```bash
for BAG in 900001 900002 900003; do
curl -s -X POST "$API/bag-registration" \
  -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{
    "bagRegistration": {
      "customerRefNumber": "<your new CustomerRefNumber>",
      "registrationType": "NOK",
      "messageId": "'"$(node -e "console.log(require('crypto').randomUUID())")"'",
      "bagNumber": "'"$BAG"'",
      "registrationDate": "'"$(date -u +%Y-%m-%d)"'",
      "referenceStatement": "e2e-lifecycle-test",
      "turnOverDate": "'"$(date -u +%Y-%m-%d)"'",
      "registeredNotes": "1000",
      "registeredCoins": "0",
      "registeredChecks": "0",
      "totalOrderAmount": "10000",
      "bagItemsLC": {"seddel1000":"1000","seddel500":"0","seddel200":"0","seddel100":"0","seddel50":"0"}
    }
  }'
echo ""
done
```

Confirm each with `201 Created` and a `bagLifecycleId`. These bags won't show up in counting-result until
they land in Nattsafe (via the outbox → Service Bus → BizTalk path) and a sync run picks them up — give it
a few minutes, or check `Nattsafearkiv` directly if you have a way to.

**[WAIT or resync]** before moving on — counting-result only ever reflects what's landed in Cosmos, not
what you just POSTed.

**Verify all 3 appear as `registered`:**

```bash
curl -s "$CR?fromDate=$(date -u -d '-1 day' +%Y-%m-%d 2>/dev/null || date -u +%Y-%m-%d)&toDate=$(date -u -d '+1 day' +%Y-%m-%d 2>/dev/null || date -u +%Y-%m-%d)&dateType=registered&customerRefNumber=<your CustomerRefNumber>" \
  -H "Authorization: Bearer $TOKEN"
```

Each item should show `"status":"registered"`, no `countedAmount`/`countedAt`.

## 3. Registered → Picked-up [ON-PREM]

Pick bag `900001`. On-prem, set it to picked-up — either directly (`intStatus = 3`), or via the
transport-fetch override (`intStatus` stays 0/1/2, but `dtmTransportFetch` gets a timestamp — both should
resolve to the same bucket, worth trying both if you have two spare bags):

- Bag `900001`: set `intStatus = 3`
- (Optional bonus) Bag `900002`: leave `intStatus` at 0/1, set `dtmTransportFetch = GETDATE()` — this
  should *also* show as `picked-up` even though the raw status never became 3. This is the specific
  override rule the status-bucket logic implements — worth confirming it actually works, not just the
  simple path.

**[WAIT or resync]**

**Verify via date range:**

```bash
curl -s "$CR?fromDate=$(date -u +%Y-%m-%d)&toDate=$(date -u -d '+1 day' +%Y-%m-%d 2>/dev/null || date -u +%Y-%m-%d)&dateType=lastUpdated&status=picked-up&customerRefNumber=<your CustomerRefNumber>" \
  -H "Authorization: Bearer $TOKEN"
```

**Verify via direct bag number:**

```bash
curl -s "$CR/900001" -H "Authorization: Bearer $TOKEN"
curl -s "$CR/900002" -H "Authorization: Bearer $TOKEN"   # if you did the bonus case
```

Both should show `"status":"picked-up"` and a `statusHistory` with 2+ entries (registered → picked-up /
the granular label your system actually used).

## 4. Picked-up → Counted, with and without deviation [ON-PREM]

**Important:** the `StatusTracking` trigger that drives sync only fires when `intStatus` itself changes —
if you only update `dblControlledAmount`/`dtmControlledAt` without changing `intStatus`, the change may
not get picked up by the normal delta sync (this is a known, accepted gap — the resync function is the
repair path for it). Always set `intStatus = 5` together with the amount fields in the same update.

Before setting the counted amount, note the bag's current `dblAmount` (its declared amount) so you can
choose to match it or not:

- Bag `900001` (**no deviation**): `intStatus = 5`, `dtmControlledAt = GETDATE()`,
  `dblControlledAmount = <same value as dblAmount>`
- Bag `900002` (**with deviation**): `intStatus = 5`, `dtmControlledAt = GETDATE()`,
  `dblControlledAmount = <a different value than dblAmount>`

**[WAIT or resync]**

**Verify via date range** (note `dateType=counted` now, the default, and it implies `countedAt != null`
so uncounted bags won't show up even if they're in range):

```bash
curl -s "$CR?fromDate=$(date -u +%Y-%m-%d)&toDate=$(date -u -d '+1 day' +%Y-%m-%d 2>/dev/null || date -u +%Y-%m-%d)&status=counted&customerRefNumber=<your CustomerRefNumber>" \
  -H "Authorization: Bearer $TOKEN"
```

**Verify via direct bag number:**

```bash
curl -s "$CR/900001" -H "Authorization: Bearer $TOKEN"   # expect deviationAmount: 0
curl -s "$CR/900002" -H "Authorization: Bearer $TOKEN"   # expect deviationAmount: countedAmount - declaredAmount, non-zero
```

Confirm `deviationAmount` is present and correct in both cases — `0` (not null) for the matched bag, the
actual signed difference for the mismatched one.

## 5. Other status buckets, for completeness

- **Cancelled** — this one's pure API, no on-prem step: `DELETE $API/bag-registration/900003`. After the
  next sync, `900003` should show `status:"cancelled"` in counting-result (note the raw label stored is
  `"canceled"` single-L, but the bucket exposed via the API is `"cancelled"` double-L — both correct,
  don't be surprised by the spelling difference if you inspect the raw Cosmos document).
- **Error** — on-prem, set a spare bag's `intStatus` to `13` (missing), `11` (not approved), or one of
  `-1/8/9`. All of these should fold into the `error` bucket.
- **In-progress (the non-obvious members)** — on-prem, set a spare bag's `intStatus` to `4`
  (received-by-bank) or `12` (approved-by-bank). Both should show as `in-progress`, not as their own
  distinct bucket — confirms the fold, not just the straightforward `intStatus = 2` case.

Verify each with a `status=<bucket>` filter query, same shape as steps 3–4.

## 6. Pagination — force more than one page

Register (or reuse already-registered) bags so you have **more than 20** for this `CustomerRefNumber`
within a date range, then query with `pageSize=10`:

```bash
PAGE1=$(curl -s "$CR?fromDate=2020-01-01&toDate=$(date -u -d '+1 day' +%Y-%m-%d 2>/dev/null || date -u +%Y-%m-%d)&dateType=registered&customerRefNumber=<your CustomerRefNumber>&pageSize=10" \
  -H "Authorization: Bearer $TOKEN")
echo "$PAGE1" | node -e "let d='';process.stdin.on('data',c=>d+=c);process.stdin.on('end',()=>{const j=JSON.parse(d);console.log('count='+j.pagination.count,'hasMore='+j.pagination.hasMore);console.log(j.items.map(i=>i.bagNumber))})"
```

Take the `continuationToken` from `PAGE1` and fetch the next page (**must repeat every other query
parameter identically** — the token is bound to a hash of the full query, not just the token):

```bash
TOKEN2=$(echo "$PAGE1" | node -e "let d='';process.stdin.on('data',c=>d+=c);process.stdin.on('end',()=>console.log(JSON.parse(d).pagination.continuationToken))")

curl -s --data-urlencode "continuationToken=$TOKEN2" -G \
  "$CR?fromDate=2020-01-01&toDate=$(date -u -d '+1 day' +%Y-%m-%d 2>/dev/null || date -u +%Y-%m-%d)&dateType=registered&customerRefNumber=<your CustomerRefNumber>&pageSize=10" \
  -H "Authorization: Bearer $TOKEN"
```

Repeat until `hasMore` is `false`. Total items across all pages should equal what you registered, with
**no duplicates and no gaps** (track bag numbers seen as you go).

**Negative cases worth checking here too:**

- Reuse `TOKEN2` but change `pageSize` to something else → expect `400 INVALID_CONTINUATION_TOKEN` (the
  hash-binding check should reject it).
- Pass a garbage string as `continuationToken` → expect the same `400`.

## 7. Filter combinations

Run these against the data you now have and sanity-check the results by eye:

- `dateType=registered` vs `dateType=counted` vs `dateType=lastUpdated` — confirm the ordering and the
  included/excluded bags differ sensibly (e.g. an uncounted bag disappears entirely under
  `dateType=counted`, but still appears under `dateType=registered`).
- `customerRefNumber=<your CustomerRefNumber>` vs a **different, real** CustomerRefNumber you're *not*
  mapped to → the foreign one should return `200` with an **empty list**, never a `403` or an error.
- Each of the 6 `status` values individually: `registered`, `in-progress`, `picked-up`, `counted`,
  `cancelled`, `error`.

## 8. Negative / validation / security checks

```bash
# No token — expect 401
curl -s -o /dev/null -w "%{http_code}\n" "$CR?fromDate=2020-01-01&toDate=2020-01-02"

# Missing dates — expect 400 VALIDATION_ERROR, field=fromDate
curl -s "$CR" -H "Authorization: Bearer $TOKEN"

# Span > 92 days — expect 400
curl -s "$CR?fromDate=2020-01-01&toDate=2020-12-31" -H "Authorization: Bearer $TOKEN"

# Bad dateType — expect 400
curl -s "$CR?fromDate=2026-01-01&toDate=2026-01-02&dateType=bogus" -H "Authorization: Bearer $TOKEN"

# Bad status value — expect 400 (note: granular labels like "received by bank" are NOT valid here, only the 6 buckets)
curl -s "$CR?fromDate=2026-01-01&toDate=2026-01-02&status=received-by-bank" -H "Authorization: Bearer $TOKEN"

# pageSize out of range — expect 400
curl -s "$CR?fromDate=2026-01-01&toDate=2026-01-02&pageSize=9999" -H "Authorization: Bearer $TOKEN"

# Not-found bag number — expect 404
curl -s "$CR/NOSUCHBAG999999" -H "Authorization: Bearer $TOKEN"

# XML content negotiation — expect an XML body, 200
curl -s -H "Accept: application/xml" "$CR?fromDate=2026-01-01&toDate=2026-01-02&pageSize=1" -H "Authorization: Bearer $TOKEN"
```

## 9. Bag-number reuse (optional, but worth doing once)

Register a **new** bag number, walk it to `cancelled`, then register the **same bag number again** as a
fresh registration (a genuinely new `bagLifecycleId`/`nattsafeRecordId` on the on-prem side — bag numbers
recirculate physically, this simulates that). After both are synced, `GET $CR/<bagNumber>` should return
only the **most recent** of the two records, not the older cancelled one, and a wide-date-range list query
should show **both** as separate items.

## Reference — bucket cheat sheet

| API bucket | Raw `intStatus` | Extra condition |
|---|---|---|
| `registered` | 0, 1 | `dtmTransportFetch` is null |
| `in-progress` | 2, 4, 12 | `dtmTransportFetch` is null |
| `picked-up` | 3 | — |
| `picked-up` (override) | 0, 1, 2 | `dtmTransportFetch` is set |
| `counted` | 5 | `dtmControlledAt`/`dblControlledAmount` set |
| `cancelled` | 10 | via API `DELETE`, or on-prem |
| `error` | -1, 8, 9, 11, 13 | — |

Set `intStatus` and any amount/date fields **together in the same on-prem update** — the sync only
notices a bag when `intStatus` itself changes.
