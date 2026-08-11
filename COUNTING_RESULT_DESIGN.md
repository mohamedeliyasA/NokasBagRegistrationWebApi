# Bag Counting Result API — Phase 1 Design Lock

**Status: DRAFT — pending owner approval. No code until this is locked.**
Companion doc: `REGISTRATION_REWRITE_DESIGN.md` (shared foundations: solution structure, serializer, error model, observability, testing — all of §5–§8 there applies here too).
Base document: `bag-counting-result-api-architecture.md` (decisions D1–D17). This doc **amends** the base design where session decisions changed it (§2) and restates the operative phase-1 shape; where the two conflict, **this doc wins**.

---

## 1. Phase-1 scope (owner decisions, 2026-08-08)

- **NOK only.** Everything in `Nattsafearkiv` is NOK-side by construction (Valuta went to AX, never to Nattsafe rows we serve). DKK and Valuta counting results are explicitly future phases; nothing in phase 1 designs for them beyond not breaking the contract when they arrive.
- Serves **all bags of mapped (onboarded) customers** — API-registered and Portal-registered alike; `registrationChannel` (`vchMyReference LIKE 'API_%'` → `"API"` else `"Portal"`) remains informational only.
- Runs on the rewritten foundations: same new API project (controller alongside registration), same new Function project (sync + resync), same App Service / Function App / Cosmos account. No new Azure services.

## 2. Amendments to the base design (deltas — read carefully)

| # | Base design said | Phase-1 decision | Why |
|---|---|---|---|
| A1 | `bagLifecycleId` carried into Nattsafe via a repurposed column, populated by the downstream writer from the SB message | **Dropped from the phase-1 contract entirely** (owner option (a)). Not in the per-item response. No XSD/BizTalk/proc changes. | The SB message never actually carried `bagLifecycleId` (base doc's premise was wrong — verified against code + XSD). For API-registered bags, `bagNumber` is globally non-reusable (CacheDB duplicate check), so partners correlate by bagNumber safely. Fallback if ever demanded: sync-time Cosmos point-read via `vchMyReference`'s clientId + bagNumber — no downstream changes even then. |
| A2 | `currencyType` field sourced from Nattsafe (column TBD) + filter param + composite index | Field **kept in the response, stamped constant `"NOK"`**; the `currencyType` *filter parameter* and its composite index are **deferred** to the DKK/Valuta phase. | Contract stays forward-compatible without dead filter machinery. |
| A3 | Open question: migrate `NokasExternalCustomerMapping` to Cosmos? | **Resolved: stays on-prem SQL.** | BizTalk itself resolves the mapping at message-processing time (both NOK and AX branches) — migration would break a system we're mandated not to touch. Question closed. |
| A4 | "Repurpose an unused `Nattsafearkiv` column"; on-prem coordination phase 0 | **No Nattsafe table changes at all** (owner constraint) — moot given A1. | Constraint recorded 2026-08-08. |
| A5 | Watermark over `StatusTracking.dtmDate` (implied index) | Same watermark design, plus: **create nonclustered index on `StatusTracking (dtmDate) INCLUDE (intId, intStatus)`** — approved by owner as it alters no consumer. Without it every sync run scans the full heap (measured: **15,047,336 rows**, no purge, no PK, no indexes). | Turns each 30-min sync read from a ~15M-row scan into a seek returning ~100–200 rows. |
| A6 | StatusTracking is a complete change signal | **Accepted with a known caveat**: `trigStatusId` only fires on `intStatus` change (a commented-out block once tracked `dtmControlledAt` separately). If a counted-amount correction ever occurs without a status change, the sync misses it — accepted as rare/one-off by owner; the **resync function is the designed repair path**. Revisit only if real cases appear. | Owner decision 2026-08-08. |

Everything else in the base design remains **locked as-is** — restated operatively below so this doc reads standalone.

## 3. Sync architecture (operative)

- **Timer Function** (every 30 min, VNet-integrated, config `CountingResults:Sync:*`) calls read-only SP `usp_CountingResult_FetchChanges`:
  - Delta mode: rows whose `StatusTracking.dtmDate` > watermark − 10-min overlap (config), joined to `Nattsafearkiv` for full record state, restricted to `vchCustomer_id` values present in `NokasExternalCustomerMapping` (only onboarded customers, resolved **at sync time**).
  - Backfill mode: same SP, driven by `(customerNumber, fromDate)` — **one shared code path** for onboarding backfill, initial load, and resync. `BackfillFromDate` column added to the mapping table (allowed — it lives in `BizTalk_Helper` (moved here 2026-08-10, was `Integrations.SN.CMS`), not Nattsafe), else global default window (config `CountingResults:Backfill:DefaultMonths`).
- Second watermark on `NokasExternalCustomerMapping.CreatedAt` auto-backfills newly onboarded customers.
- Idempotent upserts into Cosmos; batched (~500 rows/page); **a failed run never advances a watermark**; watermark state = single `sync-state` doc `{ statusTrackingWatermark, mappingWatermark, lastSuccessfulRunUtc }` in the counting-result container. `lastSuccessfulRunUtc` feeds `dataAsOf` on every API response.
- `lastUpdatedAt` per document = max `StatusTracking.dtmDate` seen for that record (not sync wall-clock unless no tracking row).
- **Resync Function**: HTTP, function-key auth, never through APIM, never in Swagger; supports per-customer repair incl. delete-and-recreate for partition corrections; logs operator, params, counts, correlationId.
- No outbox, no Blob archiving on this surface — read-only sync + telemetry only.

## 4. Cosmos schema (operative)

- Container `countingresults` (name final — new surface, no legacy), PK `/clientId`, `id` = `nattsafeRecordId.ToString()` (`intId` — identity, never reused; **never** `bagNumber`), TTL default 548 days (config `CountingResults:Cosmos:TtlDays`).
- Nullable `ttl` serialized with ignore-when-null (STJ) — the documented `"ttl": null` 400 lesson applies here identically.
- Composite indexes: `(clientId, countedAt DESC)`, `(clientId, registeredAt DESC)`, `(clientId, lastUpdatedAt DESC)`, `(clientId, customerRefNumber, countedAt DESC)`. The `currencyType` composite is deferred per A2. Exclude `/statusHistory/*`, `/denominations/*`, `/amounts/*` from indexing.
- Access via Managed Identity (shared decision R5 in companion doc).

## 5. API contract (operative — implement exactly)

- `GET /api/cash-mgmt/bag-counting-results?fromDate&toDate&dateType&status&customerRefNumber&pageSize&continuationToken`
  - `fromDate`/`toDate` required, ISO-8601 UTC, max span 92 days (config `CountingResults:Query:MaxSpanDays`).
  - `dateType = counted (default) | registered | lastUpdated`; `counted` implies `countedAt != null`; `lastUpdated` is the delta-polling mode.
  - `status`: the 6 bucketed values only (`registered | in-progress | picked-up | counted | cancelled | error`) — a query-time fold over the granular shared status map (buckets: 0/1→registered; 2,4,12→in-progress; 3 or transport-fetched 0/1/2→picked-up; 5→counted; 10→cancelled; −1/8/9/11/13→error). The granular map lives once in `Nokas.CashMgmt.Shared`, consumed by API, Function, and sync — no copies.
  - `customerRefNumber` optional; foreign values return empty results, never 403.
  - `pageSize` 1–500 default 100 (config), ordered by selected date field DESC. Load-test the 500 ceiling against `statusHistory` payload size before advertising it.
  - No `currencyType` parameter in phase 1 (A2).
- `GET /api/cash-mgmt/bag-counting-results/{bagNumber}` — most recent record for that bag number in the caller's partition. Replaces the old singular path outright (no live consumers — straight rename); the old on-prem Function lookup remains internal fallback only.
- Envelope: `{ items, pagination: { pageSize, count, hasMore, continuationToken }, dataAsOf, correlationId }`. `hasMore` = Cosmos returned a continuation token, never a count heuristic. `dataAsOf` is load-bearing (staleness detection for unattended reconciliation) — required, never optional.
- Per-item: `bagNumber`, `customerRefNumber`, `status` (bucket), `currencyType` (constant `"NOK"`), `declaredAmount`, `countedAmount`, `deviationAmount` (= counted − declared, computed at sync, all three null until counted), `registeredAt`, `countedAt`, `lastUpdatedAt`, `statusHistory` (compact array from StatusTracking). **No `bagLifecycleId`** (A1), no per-item correlationId, no per-item status message. Field names match Cosmos index paths exactly — no DTO renaming.
- Pagination: opaque tokens only. Native Cosmos continuation token wrapped with ASP.NET Data Protection (encrypt + sign, Base64Url), embedding a hash of the query params (clientId, dates, dateType, status, customerRefNumber, pageSize); mismatch → 400 `INVALID_CONTINUATION_TOKEN`. Never offset/skip, never raw tokens.
- Errors: shared `ErrorResponse` shape and codes (`VALIDATION_ERROR`, `INVALID_CONTINUATION_TOKEN`, `UNAUTHORIZED`, `FORBIDDEN`, `NOT_FOUND`, `RATE_LIMITED`, `INTERNAL_ERROR`), always with correlationId. JSON default, XML supported. `[ProducesResponseType]` on everything.

## 6. Security (operative)

- `clientId` exclusively from the validated token (`azp`/`appid`); never from request input — no customer numbers or tenancy hints accepted from callers.
- Role `cashbag.countingresult.read` / policy `PartnerCountingResultRead` (App Role, Application permission, admin-consented — delegated scopes do nothing for client-credentials, per platform gotcha).
- Every query partition-scoped; enumeration impossible by design (empty results, not 403s).
- Secrets via Key Vault; Cosmos via Managed Identity; resync internal-only.

## 7. SQL work (owned by us, phase 1)

| Item | Where | Notes |
|---|---|---|
| `usp_CountingResult_FetchChanges` (delta + backfill modes) | Nattsafe DB (new proc — allowed; only *table* changes are barred) | Read-only, NOLOCK consistent with existing procs; selects Nattsafearkiv fields + StatusTracking history slice |
| Nonclustered index `IX_StatusTracking_dtmDate` on `(dtmDate) INCLUDE (intId, intStatus)` | `Nattsafe.dbo.StatusTracking` | A5; verify with DBA before creation window; no consumer impact |
| `BackfillFromDate` column | `BizTalk_Helper.dbo.NokasExternalCustomerMapping` | Nullable; absent → global default window |

## 8. Telemetry & alerts

- Per request: custom event `CountingResultQuery` (clientId, dateType, fromDate, toDate, statusFilter, storeFilter, itemCount, pageIndex, hasMore, durationMs, correlationId). No payload storage anywhere on this surface.
- Per sync run: `CountingResultSyncRun` (rows read/upserted, backfilled customers, watermarks before/after, **SP query duration** — the scan-vs-seek health signal per A5 — total duration, outcome).
- Alerts: sync failure; watermark lag > 90 min; plus the platform severity-4 email alert from the companion doc.
- Kusto workbook for daily operational review.

## 9. Testing (this surface's specifics, on top of companion §8)

- Query validation matrix (dates, span, dateType, status bucket values, pageSize bounds).
- Continuation token round-trip + param-hash mismatch → `INVALID_CONTINUATION_TOKEN`.
- Status bucket fold: every granular code lands in exactly one bucket; transport-fetch override cases.
- Sync delta mapping (SQL row → document), watermark advance/no-advance on failure, backfill window resolution (`BackfillFromDate` vs default).
- Integration against Cosmos emulator: upsert idempotency, composite-index-shaped queries, sync-state read/write.

## 10. Build order (phases, design-level — cards come at implementation)

1. SQL: index + `BackfillFromDate` + `usp_CountingResult_FetchChanges` (delta + backfill modes).
2. Cosmos: container + indexing policy + TTL + `sync-state` doc.
3. Sync Function (timer, dual watermark, batching, overlap) + resync Function (HTTP internal).
4. API: controller, validation, repository (bucket→raw query mapping), continuation-token service, DTOs — on the shared status map from day one.
5. AuthZ: role/policy addition (coordinated with the registration role split — one partner-facing change window, not two).
6. Telemetry events, workbook, alerts.
7. Initial backfill for all existing mapping rows; partner test on `api-test`.
8. Partner-facing docs/Swagger: dateType semantics, status buckets, paging loop, `dataAsOf`, error codes.

## 11. Do NOT (unchanged from base, binding)

No on-prem SQL calls from any API request path. No `bagNumber` as document id/unique key. No status-map copies. No offset pagination or raw continuation tokens. No resync via APIM/Swagger. No watermark advance on partial failure. No payload storage. No tenancy input from callers. No new Azure services.
