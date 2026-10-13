# Operations Runbook

## Contents

- [Counting Result](#counting-result)
- [Application Insights Queries](#application-insights-queries)
  1. [Warnings and failures, last 30 minutes](#1-warnings-and-failures-last-30-minutes)
  2. [Trace a request by correlation ID](#2-trace-a-request-by-correlation-id)
  3. [Trace a partner by client ID](#3-trace-a-partner-by-client-id)
  4. [Bag-number cache check (v2 POST telemetry)](#4-bag-number-cache-check-v2-post-telemetry)
  5. [Bag-number cache sync runs](#5-bag-number-cache-sync-runs)
  6. [Bag-number cache sync failures](#6-bag-number-cache-sync-failures)

## Counting Result

### Onboarding a new customer

Adding a row to `BizTalk_Helper.dbo.NokasExternalCustomerMapping` makes a customer's bags eligible for
sync, but the automatic timer only guarantees a full backfill up to 10,000 rows in a single run (20 pages
× 500 rows). If the customer has more history than that, or you just want certainty that their full
history landed rather than waiting for the next 30-minute tick, run the resync function manually right
after onboarding.

**Steps:**

1. Confirm the mapping row exists: `ClientID`, `NokasCustomerNumber` (the Nattsafe customer number,
   `vchCustomer_id` — not the clientId or the CustomerRefNumber), `CustomerRefNumber`, and optionally
   `BackfillFromDate` if the customer needs a specific history window.
2. Run `CountingResultResyncFunction` for that customer (see below).
3. Verify: query the counting-result API for that customer's `clientId` and confirm results appear, or
   check the App Insights log line (see the verification query below).

### How to run CountingResultResyncFunction

It's an HTTP function (`POST`), `AuthorizationLevel.Function` (needs a function key), route
`resync/{customerNumber}`. It is **not** exposed through APIM and has no Swagger entry — it's an internal
tool only.

**Network access:** the Function App's main site only accepts inbound traffic from the on-prem-reachable
VNet subnet. You can't call it directly from an arbitrary machine's public IP. To run it:

- From inside that subnet (a jump box or VPN-connected machine), or
- From the Azure Portal's **Test/Run** panel for the function — this itself needs direct network access
  from your browser and a CORS origin for `https://portal.azure.com`, so if neither is currently allowed
  you'll need to temporarily add an access-restriction rule for your IP and a CORS entry, then **remove
  both again afterward** — they're not meant to be left in place.

**Request:**

```http
POST https://func-test-nokas-bagreg-v2.azurewebsites.net/api/resync/{customerNumber}?code={functionKey}&fromDate=2024-01-01&purgeOtherClients=false
```

| Parameter | Where | Required | Meaning |
|---|---|---|---|
| `customerNumber` | path | yes | The Nattsafe customer number (`vchCustomer_id`) — not the clientId, not the CustomerRefNumber |
| `code` | query | yes | The function's own key (Function App → **App keys**, or the function's individual key) |
| `fromDate` | query | no | ISO date. Overrides `BackfillFromDate`/the default ~18-month window — use this to pull a specific history depth (e.g. 500 days back) |
| `purgeOtherClients` | query | no, default false | Deletes any stray documents for this customer number sitting under a *different* clientId first. Only needed when repairing a bad mapping correction — not for routine onboarding |

**Response:** `200` with `{ customerNumber, rowsUpserted, staleDocsPurged, correlationId, buildStamp }`,
`404` if the customer number isn't in the mapping table, or `500` with a `correlationId` to look up in
Function logs on failure.

**Easy mistake to make**: passing the `CustomerRefNumber` (the partner's own reference, e.g. a GLN) instead
of the actual `NokasCustomerNumber` gets you `404 "Customer number is not mapped."` — they're different
values. If you don't know the mapping off-hand, query it from already-synced data instead of guessing:

```sql
SELECT DISTINCT c.customerNumber, c.customerRefNumber FROM c WHERE c.clientId = '<clientId>'
```

For the main test client (`1a732120-1149-4d97-98b5-d454d144b383`), it's mapped to **two** customer
numbers: `customerNumber=314211` for `customerRefNumber=5790002334764`, and `customerNumber=314210` for
`customerRefNumber=5790002334757`. Use `314211` for resync calls against the usual test data.

**If the customer has more than ~10,000 eligible bags**, one call may not capture everything (same page
cap as the automatic backfill). Re-run with a narrower `fromDate` per call to sweep the remainder if
needed, or run it again — upserts are idempotent, so re-running is always safe.

### Verifying a sync or resync run

```kusto
union traces, exceptions
| where cloud_RoleName == "func-test-nokas-bagreg-v2"
| where message has "Counting-result sync" or outerMessage has "Counting-result sync"
| order by timestamp desc
| project timestamp, message = coalesce(outerMessage, message), severityLevel
```

`severityLevel` 1 = success (with a summary: rows upserted, watermarks, backfilled-customer count),
4 = failure with the underlying exception. A failed run never advances the watermark, so it's always safe
to just fix the underlying issue and let the next tick (or a manual resync) retry.

## Application Insights Queries

Run these in the Azure Portal under **Application Insights → Logs**, or from the CLI:

```powershell
az monitor app-insights query --app api-nokas-app-insights -g rg-nokasapi-cashmgmt --analytics-query "<query>" -o table
```

## 1. Warnings and failures, last 30 minutes

```kusto
union traces, exceptions, requests
| where timestamp > ago(30m)
| where (itemType == "trace" and severityLevel >= 2)
   or itemType == "exception"
   or (itemType == "request" and success == "False")
| project timestamp, itemType, severityLevel, message, resultCode, customDimensions
| order by timestamp desc
```

`severityLevel`: 2 = Warning, 3 = Error, 4 = Critical. The `requests` branch catches failed HTTP calls (4xx/5xx) even on the rare path where nothing got logged explicitly.

## 2. Trace a request by correlation ID

```kusto
union traces, exceptions, requests
| where timestamp > ago(1d)
| where customDimensions.CorrelationId == "PASTE-CORRELATION-ID-HERE"
| project timestamp, itemType, severityLevel, message, resultCode
| order by timestamp asc
```

Ordered ascending (not descending) — for one request you want to read the story top to bottom.

## 3. Trace a partner by client ID

```kusto
union traces, exceptions, requests
| where timestamp > ago(1d)
| where customDimensions.ClientId == "PASTE-CLIENT-ID-HERE"
| project timestamp, itemType, severityLevel, message, resultCode, customDimensions.CorrelationId
| order by timestamp desc
```

## 4. Bag-number cache check (v2 POST telemetry)

Fires once per successful `201 Created` on the v2 registration API (`api-nokas-bagreg-v2`) — see the `BagNumberCacheCheck` custom event in `BagRegistrationController`. Useful for confirming a test POST actually went through and for spotting a stale cache (`cacheLastSyncedAt` far in the past).

```kusto
customEvents
| where name == "BagNumberCacheCheck"
| order by timestamp desc
| project timestamp,
    bagNumber = tostring(customDimensions.bagNumber),
    bagLifecycleId = tostring(customDimensions.bagLifecycleId),
    clientId = tostring(customDimensions.clientId),
    correlationId = tostring(customDimensions.correlationId),
    cacheLastSyncedAt = tostring(customDimensions.cacheLastSyncedAt)
```

## 5. Bag-number cache sync runs

The `SyncBagNumberCache` timer function (`func-test-nokas-bagreg-v2`) runs every 15 minutes and logs a summary line on completion.

```kusto
traces
| where message contains "Bag-number cache sync run"
| order by timestamp desc
| project timestamp, message, severityLevel
```

## 6. Bag-number cache sync failures

A failed run does **not** advance the watermark, so failures here are safe to retry on the next scheduled tick — but worth checking if `bagnumber-cache` looks stale.

```kusto
exceptions
| where timestamp > ago(1d)
| where cloud_RoleName == "func-test-nokas-bagreg-v2"
| project timestamp, type, outerMessage
| order by timestamp desc
```
