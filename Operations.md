# Operations Runbook — Application Insights Queries

Reference KQL queries for the `api-nokas-app-insights` resource (resource group `rg-nokasapi-cashmgmt`).

Run these in the Azure Portal under **Application Insights → Logs**, or from the CLI:

```powershell
az monitor app-insights query --app api-nokas-app-insights -g rg-nokasapi-cashmgmt --analytics-query "<query>" -o table
```

## Contents

1. [Warnings and failures, last 30 minutes](#1-warnings-and-failures-last-30-minutes)
2. [Trace a request by correlation ID](#2-trace-a-request-by-correlation-id)
3. [Trace a partner by client ID](#3-trace-a-partner-by-client-id)
4. [Bag-number cache check (v2 POST telemetry)](#4-bag-number-cache-check-v2-post-telemetry)
5. [Bag-number cache sync runs](#5-bag-number-cache-sync-runs)
6. [Bag-number cache sync failures](#6-bag-number-cache-sync-failures)

---

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
