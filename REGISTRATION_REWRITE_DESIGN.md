# Bag Registration — Full Rewrite Design Lock (Phase 1)

**Status: DRAFT — pending owner approval. No code until this is locked.**
Companion doc: `COUNTING_RESULT_DESIGN.md` (separate surface, shared foundations).
Source material: `ExistingFullFlow.md`, `SQL/` (all DDL + procs), `BizTalk/BagRegistration.xsd`, current codebase, `CLAUDE.md`.

Governing principle (owner's rule): **preserve the business requirements and behavior — not the existing code or design.** Any deviation from current behavior must be listed in §4 and approved; nothing else may change observable behavior.

---

## 1. Decision & context

The registration API + Function are rewritten from scratch (new projects, same repo, same Azure resources). Drivers: clean, defensible enterprise patterns; consistent logging/error design; single serializer; testability; .NET LTS. The system is TEST-only today (PROD resources exist but are not live), which makes a cutover low-risk.

Explicitly **not** part of this rewrite: Integration Portal / BAM (separate track), BizTalk (untouched), on-prem SQL objects (only additions owned by us, see counting-result doc), Azure resource topology (reuse everything existing — standing constraint).

## 2. Scope

| In scope | Out of scope |
|---|---|
| New API project: POST / GET / DELETE bag-registration + counting-result controllers (see companion doc) | Portal SPA, BAM |
| New Function project: `validate`, `status`, `countingresult` bridge endpoints + counting-result sync/resync (companion doc) | BizTalk maps/pipelines/orchestrations |
| Shared library: status map, error model, correlation primitives | AX / Valuta processing |
| Test projects (unit + integration + contract) | Changing Nattsafe tables (index exception, see companion doc) |
| App Role split (`cashbag.registration.access` / `cashbag.countingresult.read`) | New Azure services |
| .NET 10 LTS (upgrade from net8.0) | Prod go-live activities (separate deploy guide) |

## 3. Frozen contracts (may not change)

### 3.1 Partner REST contract
- Routes: `POST/GET/DELETE /api/cash-mgmt/bag-registration[/{bagNumber}]`. Counting-result routes per companion doc.
- Response shapes byte-compatible for JSON **and** XML (content negotiation preserved): `BagRegResponse` (customerRefNumber, bagNumber, description, registrationStatus, bagLifecycleId, requestCorrelationId), `ErrorResponse` (title/status/correlationId/code/field + dev-only diagnostics).
- Status-code behavior preserved exactly, including both 409 variants on POST (pre-check `AlreadyExists` summary from the validate Function; `ERR_DUPLICATE_BAG` ErrorResponse from the Cosmos unique-index race), 201 + `Location` header, DELETE's 200/400/404/408/409/503 branches driven by `api_CancelBagIfRegistered` semantics.
- Headers: `X-Correlation-ID` echo, `X-Build-Stamp` on every response.
- `bagLifecycleId` continues to be issued at POST and returned in the 201 response (generation unchanged; it is simply not propagated downstream — that was never the case anyway).
- Swagger surface + Postman collection regenerated but content-equivalent; Front Door FDID restriction and prod Swagger rules preserved.

### 3.2 Service Bus contract (BizTalk)
- Payloads for subjects `bagregistration` and `deleteBagRegistration` are **byte-identical** to today's output. `BizTalk/BagRegistration.xsd` is the source of truth for the registration message; the API delivers the `bagRegistration` JSON body only (BizTalk's receive pipeline adds root node + namespace).
- Units preserved as-is (amounts in øre where the current code sends øre; downstream procs divide by 100 — we do not "fix" this).
- Enforced by golden-file contract tests (§9): serialize the new models, compare byte-for-byte against captured current-production payloads.
- `MessageId` = outbox record id (broker dedup) — unchanged.

### 3.3 Auth
- Mechanism unchanged: B2C client-credentials JWT, audience `https://nokasb2ctest.onmicrosoft.com/api/nokas`, clientId from `azp`/`appid` only, App-Role-based authorization (all documented Entra gotchas in CLAUDE.md still apply).
- Role split implemented as already planned: `cashbag.registration.access` → policy `PartnerRegistrationAccess` (registration endpoints); `cashbag.countingresult.read` → policy `PartnerCountingResultRead` (counting endpoints). Breaking change for existing partners — manifest update, admin re-consent, per-partner reassignment coordinated at cutover, not silently.

### 3.4 On-prem SQL touchpoints (consumed, not owned)
- `NokasMwCacheDbV3.dbo.Registration` existence check (validate) — semantics preserved (global, not client-scoped — this is what makes API bag numbers non-reusable forever), but the **mechanism changes**: no longer a live synchronous call in the request path — see §6.1 (superseded by C6 in §4).
- `Nattsafe.dbo.api_CancelBagIfRegistered` — **mutating**: it performs the cancellation itself (`intStatus → 10, 'Kansellert'` when status ∈ {0,1,2} and `dtmTransportFetch` null) and returns the pre-cancel status. OutStatusCode semantics (0 cancelled-now / 1 not-found / 2 already-cancelled / 3 not-cancellable / 4 error) drive the DELETE response mapping exactly as today. (CLAUDE.md previously described this as a read-only "status check" — corrected.)
- `Nattsafearkiv` single-bag SELECT (legacy counting-result fallback) — preserved as internal fallback only.
- What BizTalk does after consuming our messages is **out of scope by owner decision** — its own track.

## 4. Deliberate behavior changes (each requires this doc's approval — nothing else changes)

| # | Change | Rationale |
|---|---|---|
| C1 | `registrationType = "Valuta"` now returns 400 `VALIDATION_ERROR` ("not supported"). Model keeps the Valuta/`bagItemsFC` fields for the future phase; validation rejects them. | Owner decision: NOK-only scope; Valuta was never processable end-to-end via the API path anyway (XSD/model shape mismatch). |
| C2 | POST rejects a token without `azp`/`appid` with 401 (parity with GET). | Fixes documented pitfall: today a null clientId writes a document with a null partition key. |
| C3 | Logging policy: exactly one log per business outcome; Warning = expected negative outcomes (409 duplicate, 400 validation, 404, DELETE rejections), Critical = platform failures (Cosmos non-duplicate, Function unreachable, Service Bus dispatch failure, unhandled 500s). | Already applied to current code (cards 1–10) and verified; carried into the rewrite as design, not retrofit. |
| C4 | Application Insights Serilog sink restricted to Warning+; Information stays in Seq/console (dev) and auto-collected requests/dependencies telemetry (all envs). | Locked earlier this session; keeps AI signal clean. |
| C5 | Fail-closed 503 when the duplicate-bag check is unreachable on POST — **explicitly re-affirmed, not changed** (listed to close the question): fail-open would let a duplicate through to an async downstream rejection after the partner already received 201. Superseded in mechanism by C6 (the check itself no longer calls the Function live), but the fail-closed principle carries over unchanged: same response on Cosmos-cache-unreachable as previously applied on Function-unreachable. | Correctness over availability for this specific guard. |
| C6 | Duplicate bag-number check moves from a synchronous on-prem call to a periodically-synced Cosmos cache (§6.1). Accepts a small, bounded staleness window instead of a live, always-current check. | Removes the one on-prem dependency left in the request's hot path — same "sync-and-serve over synchronous resilience" principle already locked for counting-result. Owner-approved risk: a same-instant collision on a bag number nobody has ever used before is operationally rare; a same-bag-number *second* API attempt is still caught regardless of cache staleness, by Cosmos's own unique-key constraint on the registration container. |

Open review item R1: POST currently responds `registrationStatus: "In-Progress"` with description "Bag Registered Successfully". Preserve as-is (default) or change to `"Registered"`? Changing it is partner-visible — owner call.

## 5. Solution architecture

### 5.1 Projects (new, side-by-side with old until cutover)

```
src/
  Nokas.CashMgmt.Shared/                  status map (single source, 3 consumers), error model,
                                          correlation/logging primitives, shared constants
  Nokas.CashMgmt.BagRegistration.Api/     ASP.NET Core (net10.0) — registration + counting-result controllers
  Nokas.CashMgmt.BagRegistration.Func/    Functions isolated worker (net10.0) — validate/status/countingresult
                                          bridge + counting-result sync & resync timers/HTTP
tests/
  Nokas.CashMgmt.BagRegistration.Api.Tests/    unit + contract (golden-file SB payloads, JSON+XML response shapes)
  Nokas.CashMgmt.BagRegistration.Func.Tests/   unit (SQL layer mocked), sync delta-mapping tests
  Nokas.CashMgmt.Integration.Tests/            Cosmos-emulator-backed integration tests
```

Layering: deliberately **pragmatic** — folders + interfaces inside the API project (Endpoints/Controllers, Application, Domain, Infrastructure), not separate assemblies per layer. Rationale: ~1,000 bags/day, two developers; assembly-per-layer ceremony adds friction without enforcement value at this size. The Shared library exists because it has three real consumers (API, Function, sync), not for pattern's sake. Anyone challenging this gets the volume argument.

### 5.2 Technology decisions

| Area | Decision | Rationale |
|---|---|---|
| Runtime | .NET 10 LTS both projects | Owner decision; supported until ~2028 |
| Serialization | System.Text.Json **everywhere**, including Cosmos (custom STJ `CosmosSerializer`) and XML via existing ASP.NET formatters for responses | Ends the three-convention mix. Today's SB payload is already STJ → byte-compat safe. The Cosmos `ttl` lesson carries over as STJ `JsonIgnoreCondition.WhenWritingNull` on nullable `ttl` |
| Error handling | Result-pattern internally (no exceptions for expected outcomes: duplicate, not-found, not-cancellable); exceptions only for genuine faults. Wire shape stays `ErrorResponse` (not ProblemDetails — contract preservation) | Retires `DuplicateBagNumberException`-as-control-flow; single mapping point outcome→HTTP in one response-mapper |
| Cosmos access | **Managed Identity (RBAC)** for the API's Cosmos client — no account keys | Rule 4 (prefer MI); counting-result already mandated MI; one auth pattern for both surfaces. Requires data-plane role assignment (deploy guide item) |
| Validation | DataAnnotations + `IValidatableObject` conditional rules preserved semantically (NOK requires totalOrderAmount, forbids bagItemsFC; C1 adds Valuta rejection); `[ApiController]` auto-400 with logged factory | Same rules, same responses |
| Options | `IOptions<T>` + `[Required]` + `ValidateOnStart` for every config section (unchanged pattern — it works) | Fail at startup, not first request |
| Mapping | Hand-written mapping extensions; **AutoMapper dropped** | Removes NU1903-vulnerable dependency and its license nag; mapping surface is small and contract-critical — explicitness is a feature |

### 5.3 Request pipeline (ordered)

1. HTTPS redirection
2. Build-stamp header (every response, incl. 401s)
3. Correlation: read/create `X-Correlation-ID`, echo on response, store in `HttpContext.Items`, push `CorrelationId` into Serilog `LogContext` for the whole request
4. Exception boundary: maps faults to `ErrorResponse`, single Critical/Warning log per §4-C3 (null-safe against Cosmos batch messages — documented lesson preserved)
5. Request archiving: raw POST body → in-memory channel → hosted archiver → Blob (behavior preserved; known crash-loss limitation of the in-memory queue **accepted and documented** — durable queue is a future item, not this rewrite; R2 below)
6. Static files, AuthN
7. **ClientId enrichment** (new, between AuthN and AuthZ so role-rejected requests still get tagged): push `ClientId` from validated claims into `LogContext`
8. AuthZ (role policies per §3.3)

### 5.4 Write path (unchanged pattern, new implementation)

Outbox pattern preserved exactly: bag document + outbox message in one Cosmos transactional batch, same partition (`/clientId`), unique key `/BagRegistration/BagNumber` (with the placeholder rule for outbox docs), dispatcher hosted service polls Pending → publishes → patches Published + ttl. Per-operation batch-failure logging preserved (it found the last production bug). No direct Service Bus calls from controllers — ever.

### 5.5 Cosmos data design

- **Fresh containers** in the same account/database: registration container (id = bagNumber, PK `/clientId`, unique key `/BagRegistration/BagNumber`, TTL enabled no-default) with a clean document model owned by the new code. Old `BagRegistrations` container stays untouched until cutover sign-off, then is retired. Rationale: TEST-only data, no migration needed; clean model beats dragging serializer-era quirks forward. (R3: container name to approve — proposal: `bagreg-v2`.)
- `clientdirectory` container: **not recreated** — dead/legacy, dropped from the new code entirely.
- `bagnumber-cache` container (new): see §6.1 — the synced duplicate-check cache, PK `/id` = `bagNumber`.
- Document model keeps: OperationHistory entries on mutations, BagLifecycleId, CorrelationId, RegisteredBy `API_{clientId}` — all feeding preserved behaviors.

## 6. Function rewrite

- Isolated worker, net10.0, same three HTTP endpoints with identical routes, function-key auth, response shapes and `buildStamp` on success **and** error paths.
- SQL access: thin Dapper layer behind an interface (testable), `api_CancelBagIfRegistered` OutStatusCode semantics encoded as a typed result.
- **Secrets fix designed-in** (current 🔴 pitfall): Function App gets a system-assigned Managed Identity; `NattsafeSQLDBConnString` / `NokasMwCacheDBConnString` become Key Vault references; `AzureWebJobsStorage` moves to identity-based connection. No cleartext credentials in App Settings.
- Committed function key pitfall: config value stays empty in the repo; TEST/PROD supply it via App Service settings (already the case) — and the exposed key gets rotated at cutover.

### 6.1 Bag-number existence cache (replaces the live validate call — C6)

Removes the one synchronous on-prem call left in POST's request path, by syncing bag numbers into Cosmos ahead of time instead of checking on-prem at request time.

- **Source**: `NokasMwCacheDbV3.dbo.Registration`, the same table the live check queries today — not `Nattsafearkiv` (which is populated later in the downstream pipeline and would add processing lag on top of the sync interval). This table already covers both channels (Cash Portal and API), so the cache stays correct for cross-channel duplicates, not just API-originated ones.
- **Watermark**: `dbo.Registration.CreatedAt`, with a small overlap window on each run (mirrors the counting-result sync's overlap pattern) for safety against late-committed transactions.
- **Frequency**: every 15 minutes.
- **Target container**: new, `bagnumber-cache`, PK `/id`, `id` = `bagNumber` (self-partitioning — every lookup is an exact-key point read, no need for a separate partition scheme; high cardinality spreads load evenly, the opposite of a hot-partition risk). Document body is minimal: `{ id, syncedAt }`. The account (`nokasbagregistrationapitest`) is **Serverless** — no provisioned RU/s, billed per request, so a new container has zero idle cost.
- **Sync state**: single `sync-state` document tracking `lastSuccessfulRunUtc`, same pattern as counting-result's watermark doc. A failed run never advances it.
- **Hosting**: new timer-triggered function inside the existing `Nokas.CashMgmt.BagRegistration.Func` app — no new Function App.
- **API-side change**: the duplicate check becomes a Cosmos point read (`ReadItemAsync(bagNumber, PartitionKey(bagNumber))` against `bagnumber-cache`) instead of an HTTP call to the Function. Fail-closed on read failure per C5/C6: 503, never guess.
- **`ValidateBagNumberFunction`'s HTTP endpoint stays in place but drops off the live request path** — kept as an internal fallback/diagnostic tool only (function-key auth, not called by the API under normal operation), the same treatment already given to the legacy on-prem counting-result single-bag lookup.
- **Forensic telemetry**: custom event `BagNumberCacheCheck` (via `TelemetryClient.TrackEvent`, not a Serilog log line — bypasses the Warning+ severity filter so it doesn't compete with real alerting, but stays queryable) fired on every successful creation, every time, not conditionally: `bagNumber`, `clientId`, `correlationId`, `cacheLastSyncedAt` (the sync watermark at the moment of the check). Purpose: if a partner ever reports a real-world duplicate, this lets it be confirmed or ruled out as a cache-staleness collision in minutes instead of guesswork. Fires on every 201 because staleness exposure is identical for all of them — there's no way to know in advance which ones were at risk.

## 7. Observability design

- Severity policy per §4-C3/C4; every log line carries `CorrelationId` + `ClientId` (enrichment, not per-call-site interpolation).
- Custom event `BagNumberCacheCheck` per §6.1 — deliberately outside the Serilog severity pipeline, so routine successful creations never compete with the Warning/Critical signal used for alerting.
- Seq (`http://localhost:5341`, Docker) is the dev log console; Application Insights carries Warning+ app logs plus auto-collected telemetry in all environments.
- Daily-negative-outcomes query and severity-4 email alert (Azure Monitor alert rule + action group, TEST first) — provisioned during implementation, documented in the deploy guide.
- Build stamps preserved on all three surfaces (API header, Function responses, deploy-script verification).

## 8. Testing strategy (new — replaces the old no-tests preference by owner decision)

| Layer | What | Notes |
|---|---|---|
| Unit | Validation rules (incl. C1/C2), status-map, DELETE decision table over OutStatusCode × transportFetch, outcome→HTTP mapping, outbox document construction | xUnit; no I/O |
| Contract | **Golden-file**: SB payloads (`bagregistration`, `deleteBagRegistration`) byte-compared against captured current outputs; JSON + XML response shells for every endpoint/status combination | The rewrite's safety net for §3.1/§3.2 |
| Integration | Transactional batch + unique-key conflict + outbox dispatch + ttl patch against Cosmos emulator | Skipped gracefully where emulator unavailable |
| Excluded | Anything requiring live on-prem SQL or BizTalk | Function SQL layer verified by unit tests against the interface + manual TEST verification |

## 9. Cutover & rollback

1. All work on branch `rewrite/v2` (main stays deployable to TEST for hotfixes).
2. Build order: Shared → API skeleton (pipeline/auth/error model) → registration write path → read/delete paths → Function → tests green throughout (the solution always compiles; old projects remain in the .sln until cutover).
3. Verification gates before cutover: full test suite green; golden-file contracts pass; manual E2E on TEST (POST → Cosmos → outbox → `sbt-bagregistration`; DELETE path; duplicate/validation negatives); build stamp confirms deployment.
4. Cutover: deploy new API/Function to the **same** App Service/Function App via updated `deploy/deploy-test.ps1`; role split executed with partner coordination; function key rotated; old containers/projects retired after sign-off.
5. Rollback: redeploy `main` (old code + old containers still intact until retirement).

## 10. Open items for owner review

| # | Item | Proposal |
|---|---|---|
| R1 | POST response `registrationStatus` wording | Preserve `"In-Progress"` (no partner-visible change) |
| R2 | In-memory archive queue crash-loss | Accept for phase 1, document; durable queue later |
| R3 | New Cosmos container name | `bagreg-v2` |
| R4 | Drop AutoMapper for hand-written mapping | Yes (vulnerability + explicit contracts) |
| R5 | Managed Identity for API Cosmos access | Yes (one auth pattern platform-wide) |
