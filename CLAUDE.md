# CLAUDE.md — NOKAS Cash Management Integration Platform

Guidance for Claude Code when working in this solution. Read this fully before making changes. This file merges the platform-wide context with the Bag Counting Result API extension — they're one system, one repo family, one set of conventions.

> **Planned role rename/split — not yet implemented, do not build against it prematurely:** today the single partner role is `cashbag.api.access`, enforced by the `PartnerAccess` policy on all existing registration endpoints. As part of the counting-result work, this is being **split into two roles**: `cashbag.registration.access` (the renamed successor to `cashbag.api.access`, covering the existing registration endpoints) and `cashbag.countingresult.read` (new, covering both counting-result endpoints), enforced by policies `PartnerRegistrationAccess` and `PartnerCountingResultRead` respectively. This is a breaking change for existing partners — the App Role manifest rename/add, admin-consent, and each partner's role assignment must be coordinated with the counting-result rollout (implementation phase 5 below), not deployed silently. Until that phase ships, `cashbag.api.access` / `PartnerAccess` remains the real, live role — don't reference the new names as if they already exist in production.

## What this system is

A secure integration platform for registering physical cash bags and retrieving their counting results. External partners register bags via a REST API before pickup; NOKAS processes them through on-premises systems; customers and NOKAS employees track order status through the Integration Portal (Business Activity Monitoring, "BAM").

**Domain vocabulary:**
- *bag* = physical cash bag identified by `bagNumber` (`intInnkastnr`). **Bag numbers are reused over time as physical bags are recirculated — never treat `bagNumber` as a stable unique identity.**
- *registration/order* = the API record of a bag.
- *counting result* = declared vs counted amount + deviation after processing.
- *client ID* = the partner's OAuth app registration ID (B2C `ClientID`, read from the token's `azp`/`appid` claim) — it is the tenant key for all data, in both the registration and counting-result containers.
- *Nattsafe* = on-prem legacy system of record for bag processing (`Nattsafearkiv`, `StatusTracking` tables).
- *nattsafeRecordId* (`intId`) = the **true, stable record identity** in Nattsafe — an identity column, never reused, unlike `bagNumber`. Used as the counting-result document `id`.
- *BagLifecycleId* = GUID assigned at creation, tracks the bag through its life (registration side).
- *CorrelationId* = per-request ID (`X-Correlation-ID`), distinct from BagLifecycleId, propagated across every component in the platform.
- *NokasCustomerNumber* = the on-prem customer identifier (`vchCustomer_id`) that a partner's `clientId` maps to via `NokasExternalCustomerMapping`; one `clientId` can map to multiple customer numbers.

## Repository layout

```
Nokas.CashMgmt.BagRegistration.API/    ASP.NET Core Web API (net8.0)
  Configuration/    AuthConfig (JWT), SwaggerConfig (+ Front Door restrictions), options classes
  Controllers/      BagRegistrationController (POST/GET/DELETE), BagCountingResultController (GET single + interval/paged — being repointed to Cosmos)
  Middleware/       HttpsRedirection → CorrelationId → ExceptionLogging → RequestArchiving (registered in this order)
  Models/           Request/response DTOs + Cosmos document models (BagRegistrationRequest, OutboxMessage, CountingResultDocument)
  Repository/       CosmosBagRegistrationRepo (bagreg + clientdirectory containers; transactional-batch outbox writes),
                     CosmosCountingResultRepo (new — countingresults container, opaque-token paging)
  Services/         BlobArchiveService, ServiceBusSender, FunctionInvoker (typed HttpClient + resilience handler),
                    payload queue + hosted service, OutboxDispatcherHostedService (publishes outbox → Service Bus),
                    ContinuationTokenService (new — Data Protection wrap/unwrap for counting-result pagination)
  Profile/          AutoMapper profile (BagProfile)

Nokas.CashMgmt.BagReg.Func/            Azure Functions isolated worker (net8.0), VNet-integrated
  GetBagStatusFunction.cs              GET api/status/{bagNumber}        → dbo.api_CancelBagIfRegistered (Nattsafe DB)
  GetBagCountingResultFunction.cs      GET api/countingresult/{bagNumber} → SELECT from Nattsafearkiv (on-prem fallback only, post-migration — see Counting Result API section)
  ValidateBagNumberFunction.cs         GET api/validate/{bagNumber}      → dbo.Registration (NokasMwCacheDB)
  BagStatusService.cs                  Dapper + Microsoft.Data.SqlClient; conn strings from env vars
  CountingResultSyncFunction.cs        (new) Timer (every 30 min), VNet — syncs Nattsafe → Cosmos countingresults
  CountingResultResyncFunction.cs      (new) HTTP, function-key auth, NOT exposed via APIM — repair/backfill tool

NokasIntegrationPortal/                Vite + React 18 + TypeScript SPA
  src/authConfig.ts                    MSAL config (B2C authorities)
  src/App.tsx                          Routes: public pages + /portal/* behind RequireAuth
  src/pages/private/                   Dashboard (dummy), BusinessActivityMonitoring (stub), Profile, api-guide/*
  src/pages/public/                    Landing, Product, Contact, AboutUs, CustomerOnboarding, PublicSwagger

B2C_CustomPolicies/                    Azure AD B2C Identity Experience Framework XML
  ...TRUSTFRAMEWORKBASE.xml            Base claims/technical profiles
  ...TRUSTFRAMEWORKEXTENSIONS.xml      Claims schema (api_* extension attrs), IdPs (AvarnAD/Google/Facebook), journeys
  ...SIGNUP_SIGNIN.xml                 Relying party (token claims contract)
  ...PASSWORDRESET.xml                 Relying party

apimpolicy.xml                         APIM inbound policy: validate-jwt + rate-limit-by-key + backend rewrite
bag-counting-result-api-architecture.md   Full design doc for the counting-result extension — decisions D1–D17 are LOCKED; implement them, do not re-litigate.
```

## Architecture (request flows)

**Partner (M2M):** partner → token from `login.microsoftonline.com/{B2C tenant}` (client credentials, audience `https://nokasb2ctest.onmicrosoft.com/api/nokas`) → `api-test.nokas.com` → Front Door → APIM (JWT validation, 10 req/60s per app, backend rewrite) → Web API.

**POST bag-registration:** `[ApiController]` auto-validates the model (conditional: NOK requires `totalOrderAmount`, no `bagItemsFC`; Valuta requires `bagItemsFC`, no `bagItemsLC`) → call Function `validate/{bagNumber}` (duplicate check against on-prem cache DB) → map to document (`id = bagNumber`, `clientId` from token `azp`/`appid`, status `In-Progress`… response says `Registered`) → **one Cosmos transactional batch** creates the bag document *and* an `OutboxMessage` (subject `bagregistration`) atomically, same partition (unique index on `/BagRegistration/BagNumber` backs up the duplicate check). There is no direct Service Bus call in the request path — `OutboxDispatcherHostedService` polls for `Pending` outbox records every 5s and publishes them, retrying indefinitely on failure, so the partner's success/failure response depends only on Cosmos. Raw body archived to Blob via middleware → in-memory channel → hosted service. Returns `201 Created` with a `Location` header (not `200`).

**GET / DELETE / countingresult (single-bag, legacy path):** always scoped to the caller's `clientId` (Cosmos partition key). GET is a point read (`ReadItemAsync(bagNumber, clientId)`, since `id = bagNumber`) and no longer mutates the document. DELETE consults the Function `status/{bagNumber}` first; a bag is cancellable only when `intStatus ∈ {0,1,2}` and `dtmTransportFetch` is null; cancellation writes the status update and a `deleteBagRegistration` outbox event in the same transactional batch (same pattern as POST). Status-code map (keep API and Function copies in sync — they have already drifted; **there is now a third consumer of this map, the counting-result sync — see Conventions**): 0/1=registered, 2=in-progress, 3=picked-up, 4=received by bank, 5=counted, 10=canceled, 11=not approved, 12=approved by bank, 13=missing, -1/8/9=error; transport-fetched 0/1/2 ⇒ picked-up.

**Counting result (new sync-and-serve path — see full section below):** a VNet-integrated timer Function syncs on-prem Nattsafe data every 30 minutes into a dedicated Cosmos container, eliminating on-prem dependency from the partner request path at runtime. The single-bag `GET .../countingresult/{bagNumber}` endpoint is being repointed from its old live on-prem Function call to this Cosmos-backed path (old live lookup remains only as an internal fallback), and a new interval/paginated query endpoint is added.

**Portal (users):** MSAL redirect → B2C custom policy `B2C_1A_NOKASINTEGRATIONPORTAL_SIGNUP_SIGNIN` → local account, AvarnAD (Entra ID), Google, or Facebook → ID token with self-asserted claims `api_companyName`, `api_clientId`, `api_contactPerson`, `api_contactPersonEmail` (B2C extension attributes, app id `36ba39fd…`). SPA currently requests only `openid profile` — no API access token yet.

## Azure services

| Service | Use | Auth today | Config keys |
|---|---|---|---|
| Cosmos DB | `bagreg` container (PK `/clientId`, `id` = bagNumber, unique index on `/BagRegistration/BagNumber`) also holds outbox documents (`DocType: "outbox"`, same partition) + `clientdirectory` (dead/legacy). TTL enabled ("On, no default") — only `Published` outbox docs set `ttl` (2 days); bag documents never expire. **New:** `countingresults` container (PK `/clientId`, `id` = `nattsafeRecordId`, TTL default 548 days) — see Counting Result API section for indexing/schema. | Account key (registration containers); Managed Identity (new `countingresults` container) | `CosmosDb:*` |
| Service Bus | Topic (name in config), subjects `bagregistration` / `deleteBagRegistration`; published **only** by `OutboxDispatcherHostedService` from Cosmos-committed outbox records (never directly from a controller); `MessageId` = the outbox record's own id for broker-side dedup; consumed downstream (integration layer → on-prem) | Connection string | `ServiceBus:*` |
| Blob Storage | Raw payload archive (`bag_create_*.json`) | Connection string | `BlobStorage:*` |
| Key Vault | Fills the empty appsettings values at startup; keeps on-prem SQL connection strings for the sync Function | DefaultAzureCredential | `KeyVault:Uri` |
| App Insights | Serilog sink + telemetry (registration); structured custom events `CountingResultQuery` / `CountingResultSyncRun` (counting-result, no payload storage); B2C journeys also log here | Connection string | `Serilog`, `ApplicationInsights` |
| Azure Function | On-prem SQL bridge (read-only), called by API via `FunctionInvoker` (registration/status); new timer + internal HTTP functions for counting-result sync/resync | Function key (`?code=`) | `BagStatusFunction:BaseUrl`, `BagStatusFunction:FunctionKey` |
| Function env vars | `NattsafeSQLDBConnString` (Nattsafe), `NokasMwCacheDBConnString` (portal cache) | SQL auth | Function App settings |
| APIM + Front Door | Ingress, JWT pre-check, rate limiting; Swagger only via Front Door (`X-Azure-FDID` check, Production only); counting-result interval endpoint needs rate-limit tuning for paging loops | — | `FrontDoor:AllowedIds` |

**No new Azure services are introduced by the counting-result work** — same App Service, same Function App, same Cosmos account, same B2C client-credentials auth, same APIM/Front Door ingress. This is a hard constraint from the locked design, not just current practice.

## Domain model — counting results (critical facts)

- `Nattsafe.dbo.Nattsafearkiv` = bag lifecycle records. PK `intId` (identity, never reused) → `nattsafeRecordId`. **Never use `bagNumber` (`intInnkastnr`) as a unique identity or document id — it recirculates as physical bags are reused.** Counting-result document `id` = `nattsafeRecordId.ToString()`.
- `StatusTracking` (populated by trigger `trigStatusId` on `intStatus` change) is a **complete change signal**: counted-amount corrections always change `intStatus`. No secondary change scan is needed — the sync can rely on a watermark over `StatusTracking.dtmDate` alone.
- Status map (from `Nattsafe.dbo.Status`): 0/1/2=Registrert, 3=Klar til telling, 4=Mottatt bank, 5=Opptalt (Counted), 8=Overstiger beløpsgrense, 9=Feil, 10=Kansellert (Cancelled), 11=Ikke godkjent av bank, 12=Godkjent av bank, 13=Manglet ved henting, -1=Feil ved overføring. **English status names come from a single shared status map used by the registration API, the registration Function, and the counting-result sync — one source of truth. Do not add a third copy; consolidate the existing two-copy drift (see Known pitfalls) into a shared library as part of this work, not after.**
- Tenancy: `[Integrations.SN.CMS].dbo.NokasExternalCustomerMapping` maps B2C `ClientID` → many `NokasCustomerNumber` (= `Nattsafearkiv.vchCustomer_id`), each with `CustomerRefNumber` (partner's store id, e.g. GLN) + `CustomerRefName`. **Only mapped (onboarded) customers are ever synced.** Mapping is resolved at **sync time**, not request time — documents are fully stamped with tenancy so runtime API reads are single-partition Cosmos queries with zero on-prem or cross-reference lookups. Mapping is append-only in practice; corrections are rare and handled via the resync function.
- Counting fields: declared = `dblAmount`, counted = `dblControlledAmount`, countedAt = `dtmControlledAt`, pickedUp = `dtmTransportFetch`, registered = `dtmRegDate`. `deviation = countedAmount − declaredAmount`, computed at sync time; all three null until counted.
- All statuses are stored and returned (registered → counted → cancelled), plus a compact `statusHistory` array per document sourced from `StatusTracking`.
- `registrationChannel` derived from `vchMyReference LIKE 'API_%'` → `"API"` else `"Portal"`; informational only, **never used for authorization.**
- **`bagLifecycleId` (new, confirmed scope — implementation task open):** partners need the registration-side lifecycle GUID on the counting-result record to correlate it back to their original POST. The sync reads *only* from Nattsafe SQL and never touches the registration Cosmos container, so this has to arrive via Nattsafe itself, not a cross-container lookup — and specifically not a join on `bagNumber` (recycled, would risk stamping the wrong lifecycle ID onto a reused bag number). Decided approach: **repurpose an existing unused column in `Nattsafearkiv`** to carry the value, populated by the downstream on-prem integration writer at row-creation time (it already receives the full registration event, including `bagLifecycleId`, off the Service Bus topic), and `usp_CountingResult_FetchChanges` selects it through like any other field. **Open before implementation:** which specific column is safe to repurpose — confirm it's genuinely unused, and specifically don't reuse anything already implicated in the `vchMyReference`/`registrationChannel` derivation above. This is on-prem/Nattsafe-schema work, coordinate before phase 1 below.
- **`currencyType` (new, confirmed scope):** three values — `NOK`, `DKK`, `Valuta` (generic foreign-currency bucket; per business confirmation, SEK is treated as falling under `NOK` for now — a known approximation, not a rigorous mapping, revisit if it ever becomes material). Not currently a field on the counting-result document — needs to be added at sync time from Nattsafe (source column TBD, same on-prem coordination as `bagLifecycleId` above) and is filterable, so needs a matching composite index (see Cosmos schema below).
- **Status filter buckets (new — a second, coarser layer on top of the shared status map, specific to this API's `status` query parameter):** the shared raw-code → granular-label map above stays the single source of truth and is unchanged; the counting-result API additionally exposes only **6 bucketed values** for its `status` filter, folding the granular labels down as follows:

  | Exposed bucket | Raw Nattsafe codes folded in |
  |---|---|
  | `registered` | 0, 1 (unless transport-fetched — see `picked-up` override below) |
  | `in-progress` | 2 (existing "in-progress"), 4 (received-by-bank), 12 (approved-by-bank) — unless transport-fetched |
  | `picked-up` | 3, and any of 0/1/2 once `dtmTransportFetch` is set (existing override rule, unchanged) |
  | `counted` | 5 |
  | `cancelled` | 10 |
  | `error` | -1, 8, 9 (existing "error"), 11 (not-approved), 13 (missing) |

  This is a query-time mapping only (`status=in-progress` → `WHERE c.status IN (...)` over the granular values already on the document) — no new stored field or composite index required, since Cosmos indexes `status` by default. Document storage keeps the full granular status (for `statusHistory`/display); only the filter's public vocabulary is the 6-value bucket set. **Note this bucket set is deliberately coarser than, and different from, the existing registration API's status vocabulary** — `received-by-bank`/`approved-by-bank` are their own distinct labels there; here they're folded into `in-progress` because from a reconciliation standpoint nothing's wrong yet, the bag just hasn't reached `counted`. Don't assume the two APIs expose the same status words.

## Counting Result API — sync architecture

Timer Function (every 30 min, VNet) calls read-only SP `usp_CountingResult_FetchChanges` using a watermark on `StatusTracking.dtmDate` with a 10-minute overlap re-read → idempotently upserts documents into Cosmos container `countingresults` (PK `/clientId`, `id` = `nattsafeRecordId`). A second watermark on `NokasExternalCustomerMapping.CreatedAt` auto-backfills newly onboarded customers (window = `BackfillFromDate` column on the mapping table, else global default ~18 months). The Web API serves interval queries from Cosmos with opaque continuation-token pagination — **the API never calls on-prem SQL in the request path.** An internal HTTP function `resync/{customerNumber}` (function key, NOT exposed via APIM) repairs data, including delete-and-recreate for `clientId`/partition corrections.

### Cosmos schema & indexing

- Container `countingresults`, PK `/clientId`, `id` = `nattsafeRecordId.ToString()`, TTL configurable (default 548 days).
- Composite indexes: `(clientId, countedAt DESC)`, `(clientId, registeredAt DESC)`, `(clientId, lastUpdatedAt DESC)`, `(clientId, customerRefNumber, countedAt DESC)`, `(clientId, currencyType, countedAt DESC)` (new, for the `currencyType` filter). Exclude `/statusHistory/*`, `/denominations/*`, `/amounts/*` from indexing.
- Sync watermark state = single Cosmos doc `sync-state`: `{ statusTrackingWatermark, mappingWatermark, lastSuccessfulRunUtc }`. Advance watermarks **only after the full run commits**. `lastSuccessfulRunUtc` feeds `dataAsOf` in every API response.
- `lastUpdatedAt` on a document = max `StatusTracking.dtmDate` seen for that record in the run (NOT sync wall-clock, unless no tracking row exists).
- Volume is small (~1,000 bags/day, ~550k docs) — do not over-engineer RU/partitioning.

### API contract (implement exactly)

- `GET /api/cash-mgmt/bag-counting-results?fromDate&toDate&dateType&status&customerRefNumber&currencyType&pageSize&continuationToken`
  - **Path note:** matches the existing `/api/cash-mgmt/*` convention used by every other endpoint in the platform (registration included) — the earlier `/v1/bag-counting-results` naming is superseded, don't use it. Hyphenated `bag-counting-results`, not `bag-countingresults`.
  - `fromDate`/`toDate` required, ISO-8601 UTC, max span 92 days.
  - `dateType = counted (default) | registered | lastUpdated`; `counted` implies `countedAt != null`; `lastUpdated` is the delta-polling mode.
  - `status` — one of the 6 bucketed values in the Domain Model section above (`registered | in-progress | picked-up | counted | cancelled | error`), not the full granular status vocabulary.
  - `customerRefNumber` — optional; a foreign value yields an empty result, never a 403 (see Security rules).
  - `currencyType` — optional, one of `NOK | DKK | Valuta`.
  - `pageSize` 1–500, default 100, both configurable (`CountingResults:Query:MaxPageSize` et al.) — start with these values, but load-test the 500 ceiling against `statusHistory` payload size before it's advertised as the max in partner-facing docs. Order by selected date field DESC.
- `GET /api/cash-mgmt/bag-counting-results/{bagNumber}` — most recent record for that bag number within the caller's partition. **Replaces the old singular-path endpoint (`/api/cash-mgmt/bag-countingresult/{bagNumber}`) outright** — confirmed no live partners on the old path, so this is a straight rename, not a dual-routing/deprecation exercise. The old live on-prem lookup remains an internal fallback only, not partner-facing.
- Response envelope: `{ items, pagination: { pageSize, count, hasMore, continuationToken }, dataAsOf, correlationId }`. `hasMore` = Cosmos returned a continuation token — never a count heuristic. `dataAsOf` is load-bearing, not cosmetic: this is a 30-minute-lag sync feeding an unattended reconciliation job, and the field exists specifically so callers can detect/alert on staleness — don't let it get dropped or treated as optional in implementation.
- Per-item fields: `bagNumber`, `bagLifecycleId` (see Domain Model — open on-prem task), `customerRefNumber`, `status` (bucketed value), `currencyType`, `declaredAmount`, `countedAmount`, `deviationAmount`, `registeredAt`, `countedAt`, `lastUpdatedAt`, `statusHistory`. Field names are chosen to match the Cosmos composite-index document paths exactly (`countedAt`, `registeredAt`, `lastUpdatedAt`) — don't rename these in the DTO layer, since the index and the wire contract are meant to be the same path. **No per-item `correlationId` or status-message field** — a correlation ID is a per-request concept (see top-level `correlationId` above) and repeating it per item breaks that convention elsewhere in the platform; a static per-item success string is redundant with the HTTP 200 and populated array.
- **Pagination: opaque tokens only, never offset/skip.** Wrap the native Cosmos continuation token with ASP.NET Data Protection (encrypt + sign, Base64Url). Embed a hash of the original query params (clientId, dates, dateType, status, store filter, currencyType, pageSize); mismatch on next call → 400 `INVALID_CONTINUATION_TOKEN`.
- Errors use the existing `ErrorResponse` shape and codes: `VALIDATION_ERROR`, `INVALID_CONTINUATION_TOKEN`, `UNAUTHORIZED`, `FORBIDDEN`, `NOT_FOUND`, `RATE_LIMITED`, `INTERNAL_ERROR`. Always return `correlationId`.
- JSON default, XML supported via existing content negotiation. Swagger annotations (`[ProducesResponseType]`) on everything.

### Security rules for counting-result endpoints (non-negotiable)

- `clientId` comes ONLY from the validated B2C token (`azp`/`appid` claim). Never from a query/body parameter.
- Two app roles: `cashbag.registration.access` (the renamed successor to today's `cashbag.api.access`, for the existing registration endpoints) and `cashbag.countingresult.read` (new, for both counting endpoints), so partners who use only one surface aren't granted the other. Authorization policies: `PartnerRegistrationAccess`, `PartnerCountingResultRead`. This rename is part of this workstream, not a prerequisite done elsewhere — plan the manifest change, re-consent, and per-partner role reassignment as implementation phase 5 (below), and treat it as a breaking/coordinated change for existing partners, not a drop-in.
- Every Cosmos query is partition-scoped to the caller's `clientId`; a foreign `customerRefNumber` yields an empty result (no enumeration possible, no special 403 needed).
- Resync function: function-key auth, internal only, never routed through APIM. Log operator, params, counts, correlationId on every invocation.
- Secrets from Key Vault; Cosmos access via Managed Identity. Never hardcode connection strings.

### Sync rules

- SP is **read-only** against Nattsafe — no QUEUED bookkeeping tables (idempotent upserts + overlap re-read replace that pattern's failure-recovery role).
- Batch large runs (e.g. 500 rows/page); a failed run must not advance the watermark.
- Backfill path (onboarding, initial load, resync) is **ONE shared code path** driven by `(customerNumber, fromDate)` — do not write three copies.

### Telemetry (counting-result)

- Per API request: custom event `CountingResultQuery` (clientId, dateType, fromDate, toDate, statusFilter, storeFilter, currencyTypeFilter, itemCount, pageIndex, hasMore, durationMs, correlationId). No request/response payload storage anywhere.
- Per sync run: `CountingResultSyncRun` (rows read/upserted, backfilled customers, watermarks before/after, duration, outcome).

### Do NOT (counting-result specific)

- Do not call on-prem SQL from any API controller/service.
- Do not use `bagNumber` as document id or unique key.
- Do not implement offset/skip pagination or expose raw Cosmos continuation tokens.
- Do not duplicate the status map — the raw-code→granular-label map stays the single shared source; the 6-value bucket mapping for the `status` filter is a query-time layer on top of it, not a second copy of the map itself. Do not duplicate the backfill logic or the mapping lookup either.
- Do not expose the resync function through APIM or add it to Swagger.
- Do not advance a sync watermark on partial failure.
- Do not store outbound payloads (no Blob archiving for this surface — telemetry only).
- Do not accept `clientId`, customer numbers, or tenancy hints from request input.

### Implementation phases (work in this order)

0. **On-prem coordination (new, blocks phase 1):** identify a safe-to-repurpose unused column in `Nattsafearkiv` for `bagLifecycleId`, identify the source for `currencyType`, and get the downstream on-prem integration writer to populate `bagLifecycleId` from the registration event at row-creation time. Confirm neither overlaps anything already used by the `registrationChannel`/`vchMyReference` derivation.
1. SQL: `usp_CountingResult_FetchChanges` (+ backfill mode) — now also selecting `bagLifecycleId` and `currencyType` per phase 0 — add `BackfillFromDate` to mapping table.
2. Cosmos: container + indexing policy (including the new `currencyType` composite index) + TTL + `sync-state` doc.
3. Functions: `CountingResultSyncFunction` (timer, dual watermark, batching, overlap), `CountingResultResyncFunction` (HTTP internal, `purgeOtherClients` support).
4. API: controller, validation, repository (including the `status` bucket→raw-value query mapping), opaque token service, DTOs, shared status map consolidation.
5. AuthZ: rename `cashbag.api.access` → `cashbag.registration.access` and add new `cashbag.countingresult.read`; add `PartnerRegistrationAccess`/`PartnerCountingResultRead` policies; coordinate the rename with existing partners (App Role manifest, admin re-consent, per-partner role reassignment) as a breaking change, not a silent deploy; APIM policy update (JWT + rate limit tuned for paging loops).
6. Ship the new `/api/cash-mgmt/bag-counting-results/{bagNumber}` single-bag endpoint on Cosmos and retire the old `/api/cash-mgmt/bag-countingresult/{bagNumber}` path outright (confirmed no live partner consumers — straight rename, no dual-routing needed).
7. Telemetry events, Kusto workbook, alerts (sync failure; watermark lag > 90 min).
8. Initial backfill for all existing mapping rows; partner test on `api-test`.
9. Partner-facing docs/Swagger: `dateType` semantics, `status` bucket values, `currencyType` values, paging loop, `dataAsOf`, error codes.

### Open architectural question — carried forward, not yet decided

Should `NokasExternalCustomerMapping` migrate from on-prem SQL to Cosmos DB?

- The partner request path already has **zero** on-prem dependency once sync-and-serve is live (mapping is resolved at sync time), so migration would not improve runtime availability — that's not the argument for it.
- The genuine benefits would be operational: onboarding becomes a pure Azure-side action, and Cosmos Change Feed would enable event-driven backfill instead of maintaining a second watermark.
- **Two facts are needed before deciding:**
  1. Whether other on-prem or ERP processes consume the mapping table directly (the `AXProjectId` column suggests a possible ERP integration that a migration could break).
  2. Where the *existing* bag-registration flow currently resolves `customerRefNumber` → `NokasCustomerNumber` — if that resolution already happens somewhere reusable, migrating the table has a cleaner path; if it's baked into on-prem logic, migration is riskier than it looks.

Don't act on this migration without answering both.

## Conventions

- **Every log line and response carries a correlation ID.** Middleware reads/creates `X-Correlation-ID`, stores it in `HttpContext.Items["CorrelationId"]`, echoes it in responses; `FunctionInvoker` forwards it as `x-correlation-id`. Preserve this in any new code path, including the counting-result sync and query paths.
- **All data access is scoped by `clientId` from the validated token** (`ClaimsHelper.GetClientId` → `azp`/`appid`). Never accept a client ID from a request body or query string. This applies identically to counting-result endpoints.
- **Every state change appends an `OperationHistoryEntry`** (`Action`, `Timestamp`, `PerformedBy`, correlation IDs) — registration container only; counting-result documents use `statusHistory` sourced from `StatusTracking` instead (sync-populated, not request-populated).
- Serialization is currently mixed (Newtonsoft for MVC + Cosmos, System.Text.Json for Service Bus/Function calls, XML formatters enabled). When touching models, check which serializer actually processes the property — `[JsonPropertyName]` is ignored by both Newtonsoft and the Cosmos SDK's default serializer. New counting-result models should not add a third convention — use whichever the eventual STJ consolidation targets.
- Errors return the custom `ErrorResponse` shape (title/status/correlationId/code) everywhere, including counting-result endpoints and their extra codes (`INVALID_CONTINUATION_TOKEN`, `RATE_LIMITED`): `ExceptionLoggingMiddleware` for thrown exceptions, and `ApiBehaviorOptions.InvalidModelStateResponseFactory` (`Program.cs`) for `[ApiController]`'s automatic 400s. `DuplicateBagNumberException` → 409 `ERR_DUPLICATE_BAG`. Don't add manual `ModelState.IsValid` checks in actions — `[ApiController]` already 400s before the action runs, so they're unreachable.
- Config pattern: appsettings.json keys exist but are empty; real values come from Key Vault (names match config paths) or App Service settings (Test/Prod use environment variables directly — appsettings.json has no weight there). Never commit real values. Options classes (`AppSettingsOptions.cs`) are `[Required]` + `.ValidateOnStart()` — missing config now fails at startup, not first request. New counting-result config is namespaced `CountingResults:Sync:IntervalMinutes`, `CountingResults:Sync:OverlapMinutes`, `CountingResults:Backfill:DefaultMonths`, `CountingResults:Cosmos:TtlDays`, `CountingResults:Query:MaxSpanDays`, `CountingResults:Query:MaxPageSize` — same validate-on-start pattern.
- API responses support JSON and XML (`Produces` + `AddXmlSerializerFormatters`); DTOs carry both JSON and XML attributes. Applies to counting-result DTOs too.
- **Partner authorization**: `[Authorize(Policy = "PartnerAccess")]` (not bare `[Authorize]`) requires the `cashbag.api.access` app role (`AuthConfig.AddApiAuthorizationPolicies`, `RequireRole`) — **this is today's live state.** It is planned to be renamed to policy `PartnerRegistrationAccess` / role `cashbag.registration.access` as part of the counting-result rollout (see the callout at the top of this file and implementation phase 5) — update this convention entry once that rename actually ships, don't jump the gun on referencing the new name in code before the App Role manifest and partner consents are updated. Critical Entra/B2C gotcha learned the hard way: this must be an **App Role** (`appRoles` manifest entry, `allowedMemberTypes: ["Application"]`), granted as an Application permission on the partner app + admin-consented. A delegated scope under "Expose an API" does *nothing* for client-credentials tokens — no `roles` claim results, no matter how it's wired into "API permissions". Also: don't set `TokenValidationParameters.RoleClaimType` manually — the JWT handler's default inbound claim mapping already renames the token's `"roles"` claim to `ClaimTypes.Role`, which is the default `RoleClaimType`; overriding it to `"roles"` breaks the match (confirmed by decoding the actual token claims during debugging).
- **Outbox pattern (POST/DELETE)**: any mutation that needs to notify downstream must write an `OutboxMessage` in the *same* Cosmos transactional batch as the state change (`CosmosBagRegistrationRepo.CreateBagRegistration` / `UpdateBagRegistrationWithOutboxAsync`) — never call `IServiceBusSender` directly from a controller; `OutboxDispatcherHostedService` is the only publisher. New `OutboxMessage` documents must keep the `BagRegistration.BagNumber` placeholder — the container's unique-key path is `/BagRegistration/BagNumber`, and Cosmos treats a document missing that path as sharing one `undefined` value per partition, so a second outbox write in the same partition would collide with the first and fail the whole batch without the placeholder. **The counting-result path has no outbox** — it's read-only sync, not event-publishing; don't cargo-cult this pattern onto it.
- Nullable reference types on; async end-to-end; `CancellationToken` plumbed through (counting-result work in particular — don't repeat the registration API's gap here, see Known pitfalls).
- Unit tests for counting-result: query validation, token wrap/unwrap + param-hash mismatch, date-type query building, sync delta mapping, backfill window resolution. Integration tests against Cosmos emulator where available. (Registration API has no automated tests today by explicit prior preference — that preference predates the counting-result work and does not extend to it; see Development workflow.)

## Development workflow

- **API:** `dotnet run` in the API project; Swagger at `/swagger`. Needs Key Vault access (DefaultAzureCredential — `az login`) or local user-secrets overriding `CosmosDb/ServiceBus/BlobStorage/AzureAd/BagStatusFunction` sections. Use the Cosmos emulator locally if possible.
- **Function:** `func start` / F5 (port 7019). Needs `local.settings.json` with the two SQL connection strings; requires VPN/network path to on-prem SQL to be useful.
- **Portal:** Node ≥ 22 (moving to 24 LTS), `npm install && npm run dev` — dev server expects HTTPS certs at `cert/key.pem`/`cert/cert.pem` (mkcert). Redirect URI is `window.location.origin`, so B2C app registration must list the localhost origin. Known issue: `swagger-ui-react` is imported but missing from package.json.
- **B2C policies:** upload order matters — Base → Localization → Extensions → RP files. Tenant `nokasb2ctest.onmicrosoft.com` (TEST). Policy IDs are referenced by exact name in `authConfig.ts`.
- **Testing the API as a partner:** client-credentials token per the OAuth guide (`scope=https://nokasb2ctest.onmicrosoft.com/api/nokas/.default`), Postman collection in `wwwroot/docs/`.
- No automated test project for the *registration* API — by explicit prior preference for that codebase; verification there is `dotnet build` + manual testing via Postman/Swagger. **This preference does not extend to the counting-result work** — that surface gets unit + integration tests per the list above, `dotnet build`/`dotnet test` from solution root (adjust if the repo uses a different layout — check the `.sln` first).

## Deployment

- API → Azure App Service `api-nokas-bagreg-…` (Norway East), Web Deploy profile in Properties. Exposed only via `api-test.nokas.com` → Front Door → APIM (policy in `apimpolicy.xml`).
- Function → `func-test-nokas-api-bagreg` / `func-prod-nokas-api-bagreg`, zip deploy; VNet-integrated for on-prem SQL. Same Function App hosts the new counting-result sync/resync functions — no separate Function App.
- Portal → static build (`npm run build` → `dist/`), hosted on IIS/App Service (`public/web.config` present).
- Environments: TEST (`nokasb2ctest`, api-test.nokas.com) and PROD (separate tenant/apps). Swagger restrictions and FDID checks activate only in `Production`.

## Planned work: BAM (Business Activity Monitoring)

The agreed direction (see `NOKAS_Platform_Review.md` §10–11 for full rationale):

1. **Never trust the self-asserted `api_clientId` claim.** Access is granted via a server-side `userAccess` mapping in Cosmos (`userObjectId` → approved `clientIds` + `role`), created/approved by NOKAS admins.
2. B2C sign-in journey gets a REST claims-enrichment step returning `role` + verified `clientIds` (claim types `role`/`uniquecustomerid` are already scaffolded in the Extensions policy).
3. The existing API is extended with `/api/portal/*` endpoints using a **second JWT bearer scheme** (B2C policy issuer) alongside the existing partner scheme; policies: `PortalCustomer`, `PortalAdmin` (admins = AvarnAD users, with the issuer pinned to Avarn's tenant). Filtering happens server-side from token claims; admins get cross-partition queries with continuation-token paging.
4. A timer-triggered Function (~2 h) syncs status from on-prem SQL into Cosmos (incremental watermark). The portal reads Cosmos only — never on-prem synchronously. **Reuse the counting-result sync Function's watermark/backfill pattern here rather than designing a second version — it's effectively a proven reference implementation of the same idea at a tighter interval.**
5. The SPA acquires an access token for the API scope (replace the `openid profile`-only login), and the BAM page replaces the dummy dashboard.

Note: `userAccess` (portal-user → `clientId` access grants, Cosmos, planned) and `NokasExternalCustomerMapping` (partner `clientId` → `NokasCustomerNumber`, on-prem SQL today, existing) are related but distinct mappings — don't conflate them when building BAM.

## Known pitfalls & pending fixes (do not regress; fix opportunistically)

- ✅ Fixed: Serilog `MinimumLevel.Default` is now `Information` (`Warning` overrides for `Microsoft.*`/`System.*`); Development also gets a console sink (`Program.cs`) since App Insights has nowhere to write to locally.
- Security hardening: ✅ API-side role check done (`[Authorize(Policy = "PartnerAccess")]`, app role `cashbag.api.access` — see Conventions above for the App-Role-vs-scope gotcha). **Planned, not yet done:** rename/split into `cashbag.registration.access` + `PartnerRegistrationAccess` (registration) and `cashbag.countingresult.read` + `PartnerCountingResultRead` (counting-result), per the callout at the top of this file — coordinate as a breaking change for existing partners (manifest update, re-consent, per-partner role reassignment), scheduled as implementation phase 5 of the counting-result work. Still pending separately: APIM `required-claims` for the same role; APIM `check-header` for `X-Azure-FDID`; App Service access restriction to APIM; `ValidTokenIssuerPrefixes` in the AvarnAD technical profile must be pinned to Avarn's tenant (currently accepts any Entra tenant); social IdPs slated for removal.
- ✅ Fixed: dual-write — POST/DELETE now write the bag document and an `OutboxMessage` atomically in one Cosmos transactional batch; `OutboxDispatcherHostedService` publishes to Service Bus from that committed state, retrying indefinitely rather than dropping on failure. See the outbox convention above before touching this path.
- ✅ Fixed: `PayloadArchiverHostedService`'s loop is now wrapped in try/catch (one failed upload no longer kills archiving permanently); `BlobArchiveService` no longer blocks DI by creating the container synchronously in its constructor. Still open: the archive queue itself remains in-memory (`Channel`) — a crash/restart/scale-in can still silently lose queued payloads; not yet durable.
- ✅ Fixed: GET is now a point read (`ReadItemAsync`) and no longer mutates the document (no more history-on-read).
- `EnsureClientInDirectoryAsync` and the `clientdirectory` single-document design are dead/legacy — don't build on them.
- B2C Extensions/Base: `api_companyName`'s `DefaultPartnerClaimTypes` points at the `api_clientId` extension attribute (copy-paste bug).
- No PUT endpoint exists although middleware archives PUTs and docs mention update.
- **`BagStatusFunction:BaseUrl`/`FunctionKey` config is missing from base `appsettings.json` entirely** — only in `appsettings.Development.json` (git-ignored) locally, and environment variables in Test/Prod. This options class is now `[Required]` + `.ValidateOnStart()`, so an environment missing these values will fail at **startup**, not on first request — confirm Test/Prod actually have them set before deploying this branch.
- `AutoMapper` 15.0.1 has a known high-severity vulnerability advisory (NU1903) — pending upgrade.
- Portal: MSAL v2 (EOL), hard-coded client IDs (move to `VITE_*` envs), `*-MohamedEliyas.*` duplicate files to be deleted, `@types/axios`/`@types/react-router-dom` mismatched types.
- Status-code map drift between the registration API and the registration Function (§Architecture above) — this now matters even more with a third consumer (counting-result sync) about to read the same statuses; consolidate into one shared library as part of the counting-result work rather than adding a third copy.
- Target upgrades: .NET 10 LTS (both projects are net8.0), Node 24 LTS, React 19, MSAL v4.

## Rules for future changes

1. Any new endpoint must: require an authorization policy (not bare `[Authorize]`), scope data by validated identity, propagate the correlation ID, append operation history for mutations (or, for read-only sync-fed data like counting results, ensure the sync path's own audit trail — `statusHistory`/telemetry — covers it), and declare `[ProducesResponseType]` contracts.
2. Never put secrets, connection strings, instrumentation keys, or tenant-specific IDs in committed files (including B2C XML) — Key Vault / pipeline substitution only.
3. Anything reaching on-prem must be async or resilient (timeout ≤ 10 s, retry, fail-open where business rules allow) — on-prem availability must not break cloud writes. For new read surfaces, prefer removing the on-prem dependency from the request path entirely (sync-and-serve) over adding resilience to a synchronous call — that's the counting-result API's whole premise.
4. Prefer Managed Identity over keys/connection strings for any new Azure resource client — the counting-result Cosmos access is the reference example; don't introduce a fifth auth pattern.
5. Keep the status-code map in exactly one shared place if you touch it — now binding on three consumers (registration API, registration Function, counting-result sync), not two.
6. When changing DTOs, verify both JSON and XML serialization and regenerate the Postman collection + Swagger docs partners rely on.
7. Any endpoint that needs to notify downstream (Service Bus) must use the outbox pattern — write the event in the same Cosmos transactional batch as the state change, never call `IServiceBusSender` directly from a controller. (Not applicable to counting-result — it has no downstream notification, it's a read path.)
8. When onboarding a new partner or adding a new M2M (client-credentials) permission, it must be an **App Role** (`allowedMemberTypes: ["Application"]`), granted as an Application permission + admin-consented. "Expose an API" delegated scopes do not work for client-credentials tokens — no `roles` claim results. Applies to `cashbag.countingresult.read` exactly as it does to `cashbag.registration.access` (the planned rename of today's `cashbag.api.access`).
9. New Azure resources are not to be introduced for either the registration or counting-result surfaces without a specific, discussed reason — reuse of the existing platform stack (App Service, Functions, Cosmos, B2C, APIM, Front Door, Key Vault, App Insights) is a standing constraint, not a default that can be quietly overridden.
10. Never use `bagNumber` as a document id, unique key, or identity anywhere new gets built — `nattsafeRecordId`/`intId` (counting results) or the existing `id = bagNumber` registration convention (already locked in, not to be "fixed" by analogy) are the only sanctioned identity keys per container; don't mix the two conventions across containers.
