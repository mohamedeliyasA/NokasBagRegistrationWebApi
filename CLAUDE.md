# CLAUDE.md — NOKAS Cash Management Integration Platform

Guidance for Claude Code when working in this solution. Read this fully before making changes. This file merges the platform-wide context with the Bag Counting Result API extension — they're one system, one repo family, one set of conventions.

> ## ⚡ FULL REWRITE PROGRAM — decided 2026-08-08, read this first
>
> The owner has decided to **rewrite the registration API + Function from scratch** (new projects, same repo, same Azure resources) and build the counting-result surface on the new foundations. Two design-lock documents govern this work and **override anything below that conflicts with them**:
>
> - `REGISTRATION_REWRITE_DESIGN.md` — scope, frozen contracts, deliberate behavior changes, solution structure, patterns, testing, cutover.
> - `COUNTING_RESULT_DESIGN.md` — phase-1 (NOK-only) counting-result design; explicitly amends `bag-counting-result-api-architecture.md` (see its §2 deltas: `bagLifecycleId` dropped, `currencyType` constant `"NOK"`, mapping stays on-prem, StatusTracking index approved, trigger caveat accepted).
>
> Reference material provided by the owner (source of truth for downstream behavior): `ExistingFullFlow.md` (end-to-end business flow), `SQL/` (Nattsafe/CacheDB DDL + procs incl. `api_CancelBagIfRegistered`, `mwPutRegistrationV1`, `SaveRegistrationV2`, `trigStatusId`), `BizTalk/BagRegistration.xsd` (frozen Service Bus message contract — API delivers the `bagRegistration` JSON body only).
>
> Key facts established 2026-08-08 (correcting older statements in this file):
> - **`api_CancelBagIfRegistered` is MUTATING** — it performs the Nattsafe cancellation itself (status→10 when status∈{0,1,2} and not transport-fetched) and returns the *pre-cancel* status. The API's DELETE "status check" is actually "attempt cancel + interpret result". What BizTalk does with the `deleteBagRegistration` message afterwards (likely CacheDB bookkeeping) is out of scope by owner decision.
> - **The Service Bus message never carried `bagLifecycleId`** (older text below claims otherwise) — and per owner decision it won't; it's dropped from the phase-1 counting-result contract.
> - **NOK-only scope everywhere in phase 1**: the API will return 400 for `registrationType=Valuta` (deliberate change, model fields retained for a future phase); counting results phase 1 is NOK-only.
> - **`NokasExternalCustomerMapping` stays on-prem** — BizTalk resolves the mapping at message-processing time; the old "migrate to Cosmos?" question is closed.
> - **StatusTracking**: 15,047,336 rows, heap, no indexes, no purge; owner approved creating `IX_StatusTracking_dtmDate (dtmDate) INCLUDE (intId, intStatus)`. No other Nattsafe *table* changes allowed (new procs/indexes OK, columns/schema NO).
> - Bag numbers **cannot be reused via the API ever** — the validate check against `NokasMwCacheDbV3.dbo.Registration` is global and permanent; recycling risk applies only to pre-API/Portal-era rows.
> - Amounts on the wire are in **øre** (downstream procs divide by 100); preserved as-is, do not "fix".
>
> **Working style (owner preference, applies to all implementation):** changes are delivered as **cards** — what/why/file/current code/new code/concept-to-learn — and the owner applies every edit by hand to learn ASP.NET Core; Claude verifies afterwards by build + test, and reviews the applied diff (cards sometimes get missed — check, don't assume). Design docs contain design only, no code/cards.
>
> **Current-code state (interim fixes applied to the OLD codebase this session, all verified):** Tier-1/Tier-2 logging cards 1–10 (single-log-per-outcome; Warning for negative business outcomes incl. DELETE branches + model-validation 400s; `LogCritical` for Function-unreachable, generic Cosmos failures, unhandled 500s, outbox dispatch failures; duplicate-bag double-logging removed), `CorrelationIdMiddleware` pushes `CorrelationId` into Serilog `LogContext`, Seq dev sink added (`appsettings.development.json` + `Serilog.Sinks.Seq`; Seq runs in Docker at `http://localhost:5341`, WSL2 capped via `.wslconfig`). **Pending, superseded by the rewrite unless needed sooner:** ClientId `LogContext` middleware (cards 11–12, placement: between AuthN and AuthZ), AI-sink `restrictedToMinimumLevel: Warning`, Azure Monitor alert rule + action group for severity-4 email, two remaining double-log spots in `CosmosBagRegistrationRepo` (`GetBagByNumberForClientAsync`, `UpdateBagRegistrationWithOutboxAsync`), `deploy_guide.md` for prod (Front Door + APIM Basic + shared App Service Plan; owner to supply traffic estimate for plan sizing).
>
> **v2 rewrite progress (2026-08-09):** Batch 01 (POST vertical slice — `IMPLEMENTATION_CARDS_01_POST.md`, cards 1–37) implemented in new projects under `src/`/`tests/` (`Nokas.CashMgmt.Shared`, `Nokas.CashMgmt.BagRegistration.Api`, `Nokas.CashMgmt.BagRegistration.Func`, naming per §5.1 of the design doc), deployed to new TEST infrastructure (`api-nokas-bagreg-v2`, `func-test-nokas-bagreg-v2`, Cosmos container `bagreg-v2` — all tagged `version=v2`, reusing the existing `rg-nokasapi-cashmgmt` resource group, `as-test-integrationops` App Service Plan, Cosmos account, Key Vault, Blob storage, Service Bus, App Insights per the standing no-new-services rule), and **verified end-to-end with a real 201** (validate Function call → Cosmos transactional batch → outbox). Deploy scripts: `deploy/provision-v2-infrastructure.ps1` (infra, RBAC, Key Vault) — idempotent, safe to re-run, all fixes below already folded in.
>
> **Real bugs found and fixed while getting to that first 201 — all instructive, don't reintroduce:**
> - **Cosmos unique-key policy casing must match System.Text.Json's camelCase exactly.** The container was first created with `/BagRegistration/BagNumber` (PascalCase, copied from the old Newtonsoft-based codebase) but the new codebase serializes camelCase (`/bagRegistration/bagNumber`). Consequence: *neither* the bag document nor the outbox message actually had a property at the PascalCase path, so Cosmos treated both as "missing" it — and per Cosmos's own rule (a document missing a unique-key path shares one "undefined" value per partition), they collided **with each other**, on the very first transaction ever, regardless of the actual bag number. Symptom was a misleading `ERR_DUPLICATE_BAG` 409 for *every* write, even against an empty container — cost significant time to diagnose because the response shape is identical to a genuine duplicate. Fixed by deleting and recreating the (still-empty) container with the correct camelCase path. **Any time a Cosmos indexing/unique-key/composite-index policy is written by hand, verify its path casing against the actual `[JsonPropertyName(...)]` attributes — don't assume PascalCase.**
> - **This B2C tenant issues app-only (client-credentials) tokens with the v1-style issuer** (`https://sts.windows.net/{tenantId}/`) even though the v2.0 authority endpoint is used to request them. The old codebase's `AuthConfig.cs` hardcoded `ValidIssuer` to this exact value — the new codebase initially omitted it, relying on `Authority`-based OIDC discovery (which expects the v2-style issuer), causing every token to fail issuer validation (401). Fixed by adding `ValidIssuer = $"https://sts.windows.net/{tenantId}/"` explicitly back. Don't drop a hardcoded value from old code without understanding why it's there.
> - **`kv-dev-nokasapi-cashmgmt` uses the legacy Access Policy authorization model** (`enableRbacAuthorization: false`), not RBAC. `az role assignment create ... --role "Key Vault Secrets User"` succeeds but is **silently ignored** by a vault in this mode — Key Vault References in App Settings fail with "denied access" until an actual access policy (`az keyvault set-policy`) is added for the identity. Check `enableRbacAuthorization` before assuming an RBAC grant will work against any given vault.
> - **`az functionapp create --runtime-version` wants `10`, not `10.0`.** Cross-resource-group `--plan` references need the full resource ID for `az functionapp create` (short name resolution that works for `az webapp create` doesn't for the Functions equivalent). `az functionapp vnet-integration add` requires both `--vnet` and `--subnet` — a full subnet resource ID alone isn't accepted.
> - **Git Bash mangles `/subscriptions/...`-style CLI arguments** into Windows paths (MSYS path conversion) — prefix with `MSYS_NO_PATHCONV=1` when passing full Azure resource IDs as arguments from Git Bash.
> - **The `cashbag.registration.access` App Role now exists** in `Nokas.CashBagManagement.WebAPI`'s manifest (added via direct manifest edit — the App roles UI blade wasn't cooperating) and is granted + admin-consented on the session's test client app **only** — this was deliberately scoped as an additive, non-breaking change (old `cashbag.api.access` untouched, no other partners touched). The full rename/migration described in the paragraph below is still not done.
> - **`cashbag.countingresult.read` App Role added (2026-08-11), same additive pattern** — third entry in the same manifest (`id: fcc0e393-0ee6-4e61-8da2-6fc39bc4a6e6`), granted + admin-consented on the session's test client app only. This is ahead of the counting-result API actually being built (see phase 5 below) — the role exists and is grantable, but **no API code checks it yet**, since there's no counting-result endpoint to protect. The manifest now holds all three roles side by side: `cashbag.api.access` (original, live for all real partners), `cashbag.registration.access` (v2 registration surface, test client only), `cashbag.countingresult.read` (v2 counting-result surface, test client only, not yet enforced in code).

> **Planned role rename/split — not yet implemented, do not build against it prematurely:** today the single partner role is `cashbag.api.access`, enforced by the `PartnerAccess` policy on all existing registration endpoints. As part of the counting-result work, this is being **split into two roles**: `cashbag.registration.access` (the renamed successor to `cashbag.api.access`, covering the existing registration endpoints) and `cashbag.countingresult.read` (new, covering both counting-result endpoints), enforced by policies `PartnerRegistrationAccess` and `PartnerCountingResultRead` respectively. This is a breaking change for existing partners — the App Role manifest rename/add, admin-consent, and each partner's role assignment must be coordinated with the counting-result rollout (implementation phase 5 below), not deployed silently. Until that phase ships, `cashbag.api.access` / `PartnerAccess` remains the real, live role — don't reference the new names as if they already exist in production. **State as of 2026-08-10: `cashbag.registration.access`/`PartnerRegistrationAccess` is what the *v2* API (below) actually enforces in code, but it is only granted to the one test client app (`1a732120-1149-4d97-98b5-d454d144b383`) — no broader rollout has happened.**

> ## v2 rewrite — Batches 02 & 03 (2026-08-10), read this before touching the registration API or Function
>
> Continuing from Batch 01 (POST vertical slice, above). Two more batches shipped, deployed to TEST, and were verified against the **live deployed API** with a real partner token (not just locally) — this is now a functionally complete POST/GET/DELETE registration API on the new stack.
>
> **Batch 02 — bag-number existence cache (design decision C6):** replaced the synchronous on-prem `validate/{bagNumber}` Function call in the POST path with a Cosmos-backed cache. New container `bagnumber-cache` (PK `/id` = bagNumber, account is Serverless so idle cost is zero) is populated by a new 15-minute timer Function (`SyncBagNumberCacheFunction`) reading `dbo.Registration` (NokasMwCacheDB, **not** Nattsafearkiv) via a watermark on `CreatedAt`. POST now does a Cosmos point-read against this cache instead of calling the Function synchronously; fails closed (503 `ERR_VALIDATE_UNAVAILABLE`) if the cache read itself throws. Every successful 201 fires an App Insights custom event `BagNumberCacheCheck` (`bagNumber`, `bagLifecycleId`, `clientId`, `correlationId`, `cacheLastSyncedAt`) — unconditionally, not just on cache-miss — for forensic reconciliation. The old `IValidateBagNumberClient` HTTP-based check and its Function-call plumbing were deleted entirely; `BagStatusFunctionOptions` config was deliberately **kept** since DELETE (Batch 03) reuses it for the `status/{bagNumber}` call.
>
> **Batch 03 — GET/DELETE + shared status-code map + cutover-gate hardening:**
> - `GET /api/cash-mgmt/bag-registration/{bagNumber}` — plain Cosmos point read scoped to `clientId`, no mutation, no Function call.
> - `DELETE /api/cash-mgmt/bag-registration/{bagNumber}` — new Function endpoint `GetBagStatus` (route `status/{bagNumber}`) wraps `dbo.api_CancelBagIfRegistered` (mutating — see above) against **Nattsafe**, a different connection string (`NattsafeSQLDBConnString`) than the cache sync uses. The cancel/already-cancelled/not-cancellable decision is a **pure function** (`Api/Domain/BagCancellationDecision.Evaluate(status, transportFetchedAt)`) reconstructed purely from the row's own fields — the proc's OutStatusCode is never exposed over the wire, matching old behavior exactly. On success, writes the state update + a `deleteBagRegistration` outbox event in the same Cosmos transactional batch as POST's pattern.
> - **Shared status-code map**: `Nokas.CashMgmt.Shared/StatusMapping/NattsafeStatusMapper.cs` — the raw-code→English-label map (with the transport-fetched override) now lives in `Shared` from day one, ready for the counting-result sync to consume as its third/first-real consumer (no drift to consolidate later, unlike the old codebase's two-copy problem).
> - **Shared wire contract**: `Nokas.CashMgmt.Shared/Contracts/BagStatusRecord.cs` — used by both the Function (Dapper binds to it directly) and the API (deserializes the Function's JSON into it); property names deliberately match the SQL columns (`vchCustomer_id` etc.), not idiomatic C# casing, so Dapper's name-based binding just works.
> - **Real bug found and fixed**: `BizTalk/BagRegistration.xsd` marks `actionFlag` and `registeredBy` as **mandatory** on the `bagRegistration` message. POST's outbox payload was just re-serializing the raw partner request DTO, which never had those fields (they're server-computed) — so every `bagregistration` Service Bus message v2 had ever sent was missing two fields BizTalk's schema requires. Fixed by building the payload explicitly (`Api/Domain/BagRegistrationOutboxPayload`) instead of re-serializing the request. Caught by writing the golden-file test below, not by manual testing — the earlier "verified end-to-end with a real 201" checks never inspected the actual outbox JSON content.
> - **Build-stamp infrastructure** ported to v2 (`BuildInfo` class, `X-Build-Stamp` header, Swagger description, `buildStamp` in both Function HTTP responses) — same mechanism as the old codebase (reflection over the SDK's auto-generated `AssemblyInformationalVersionAttribute`, no custom MSBuild wiring needed).
> - **Golden-file + negative-path tests added**: `tests/Nokas.CashMgmt.BagRegistration.Api.Tests` grew from 2 files to 55 tests — `BagCancellationDecisionTests`, `NattsafeStatusMapperTests`, `BagRegistrationOutboxPayloadTests`/`BagCancellationOutboxPayloadTests` (byte-exact JSON assertions against the frozen wire shapes), and `BagRegistrationControllerTests` (hand-rolled fakes, no mocking library added — covers 401/404/408/503/409/400/200 paths for GET/DELETE). Two real gotchas surfaced writing these, worth knowing before writing more: STJ's default encoder escapes `&` as `&` (matches old code, not a bug); SQL `DATETIME` read via Dapper comes back `DateTimeKind.Unspecified`, so it serializes **without** a trailing `Z` — constructing test fixtures with `DateTimeKind.Utc` produces a mismatched golden string.
> - **Full E2E verified against the live deployed API** (2026-08-10) with a real partner client-credentials token: POST happy path, POST duplicate (409), GET found/not-found, unauthenticated (401), DELETE happy path, DELETE already-cancelled (409, confirmed the Cosmos self-heal fires), DELETE not-found (404) — all 8 scenarios matched expected status codes exactly, App Insights showed matching `requests`/`customEvents`/Warning-level `traces` for every call with correct correlation-ID propagation, zero unexpected Error/Critical entries, and both outbox messages (`bagregistration`, `deleteBagRegistration`) confirmed `status: "Published"` in Cosmos.
>
> **Network hardening (2026-08-10, both v2 apps):** `func-test-nokas-bagreg-v2` and `api-nokas-bagreg-v2` were fully open to the public internet (function-key/JWT auth only, no network restriction) — this was closed. `api-nokas-bagreg-v2` got Regional VNet Integration (outbound) into `snet-test-openAPI` (the same subnet the Function already used for on-prem reachability, already had the `Microsoft.Web` service endpoint from earlier setup — no new subnet/resource needed); `func-test-nokas-bagreg-v2` main-site inbound access is now restricted to **only** that subnet (`defaultAction: Deny`). SCM/Kudu deliberately left unrestricted on both (matches the old API's precedent) so zip-deploy keeps working from anywhere. The API additionally has `allow-apim`/`allow-afd` rules mirroring the old API's rules (harmless no-ops today since v2 isn't behind APIM/Front Door yet, but zero extra work needed when it is) plus a **temporary** `allow-dev-temp` rule for direct testing — remove that one specifically once v2 is wired into APIM/Front Door, it's not meant to be permanent.
>
> **Swagger was added to v2** (`Swashbuckle.AspNetCore` 10.2.3 — a real breaking-change version bump from what the old codebase uses: `Microsoft.OpenApi` types moved from the `.Models` sub-namespace to the top-level `Microsoft.OpenApi` namespace, and `AddSecurityRequirement` now takes a `Func<OpenApiDocument, OpenApiSecurityRequirement>` factory instead of a plain object). Deliberately **without** the old codebase's Front-Door-FDID-gate and prod-only restrictions — those would 403 everything today since nothing routes to v2 through Front Door yet; bring them back at actual cutover, not before (design doc §current-state already calls for this, see the FDID note in the old `SwaggerConfig`/`UseSwaggerRestrictions`).
>
> **Deliberately not done yet, by explicit owner decision (2026-08-10):** APIM + Front Door wiring for v2. Confirmed still blocked on: the role-split rollout (today only one test client has `cashbag.registration.access`; every real partner still only has `cashbag.api.access`, so pointing Front Door at v2 today would 403 all of them), and — now closed — golden-file contracts / build-stamp / negative-path test coverage. Owner chose to build counting-result next instead of closing the APIM/Front Door gap first, since counting-result is architecturally independent (own container, own Function, own API surface, own auth roles) and doesn't block on or get blocked by the registration cutover.
>
> ## Counting-result phase 1 — API/Cosmos/Function layer built, deployed, live-tested (2026-08-10 night session, unattended)
>
> Everything in `COUNTING_RESULT_DESIGN.md`'s implementation phases 1–4 and 6 is done except the on-prem SQL half of phase 1. Phase 5 (AuthZ) is done — the owner added `cashbag.countingresult.read` to the Entra manifest and granted it to the test client mid-session. Not done: phases 7–9 (Kusto workbook/alerts, initial backfill, partner-facing docs) — no reason to build those before real data flows.
>
> **What's live on TEST right now:**
> - Cosmos container `countingresults` (PK `/clientId`, `defaultTtl` 548 days, composite indexes + excluded paths exactly per §4) — provisioned via `deploy/provision-countingresult-infrastructure.ps1` (idempotent, same `Assert-Success` pattern as the v2 scripts; also grants the API's and Func's managed identities Cosmos data-plane RBAC and writes the `CosmosDb__CountingResultContainerName` / `CountingResults__*` app settings on both apps).
> - `Nokas.CashMgmt.Shared`: `NattsafeStatusMapper.ToBucket` (raw code → 6-value bucket) and `NattsafeStatusMapper.GranularLabelsForBucket` (bucket → granular label set, for query-time WHERE-IN construction — the document stores the granular label, not the bucket); `Documents/CountingResultDocument.cs` and `Documents/CountingResultSyncStateDocument.cs` (two independent watermarks + a `SentinelClientId = "_syncstate"` constant, since the sync-state doc is global but the container partitions on `/clientId`).
> - API: `BagCountingResultsController` (both endpoints, `[Authorize(Policy = AuthConfig.CountingResultRolePolicy)]`), `CosmosCountingResultRepository` (Cosmos SQL query built per `dateType`/`status`/`customerRefNumber`, `GetItemQueryIterator` + native continuation token — first repo in this codebase to actually page), `ContinuationTokenService` (ASP.NET Data Protection wrap/unwrap + SHA-256 query-param hash, `Microsoft.AspNetCore.DataProtection` needs no separate package reference, it's already in the `Microsoft.NET.Sdk.Web` shared framework), `CountingResultsOptions` (bound from `CountingResults:Query:*`). `AuthConfig` now has a second policy/role pair (`CountingResultRolePolicy` / `CountingResultRoleName`) alongside the registration one — additive, doesn't touch `PartnerRegistrationAccess`.
> - Func: `CountingResultSyncFunction` (timer, `0 */30 * * * *`), `CountingResultResyncFunction` (HTTP, function-key, route `resync/{customerNumber}`, `purgeOtherClients` query flag for repairing stale-partition docs via a cross-partition delete query — fine for this internal, low-volume, non-APIM-routed tool). Both registered and confirmed present via `az functionapp function list` after deploy (isolated-worker functions need a restart to re-index after a zip deploy in this environment — `az functionapp function list` showed only the 3 old functions until an explicit `az functionapp restart`, then all 5 showed up within ~15s). `CountingResultSyncService.BackfillCustomerAsync` is the one shared (customerNumber, fromDate) code path both the normal run's new-customer backfill loop and the resync function call — not two implementations.
> - Tests: suite grew from 55 to 109 (`NattsafeStatusMapperTests` bucket cases, `ContinuationTokenServiceTests` using `EphemeralDataProtectionProvider` for in-memory round-trip/tamper tests, `CountingResultMappingsTests`, `BagCountingResultsControllerTests` with `FakeCountingResultRepository`/`FakeContinuationTokenService` following the existing hand-rolled-fake style). All 109 pass.
>
> **Real live-test coverage, not just unit tests.** With no real synced data yet (see SQL blocker below), a throwaway seeder console app (`Microsoft.Azure.Cosmos` + `ProjectReference` to `Nokas.CashMgmt.Shared`, built in the scratch dir, never committed) upserted 9 realistic `CountingResultDocument`s directly into Cosmos under the real test client's real `clientId`/`customerRefNumber` — covering all 6 status buckets, a `countedAmount`/`deviationAmount` pair, and **one `bagNumber` reused across two different `nattsafeRecordId`s** to specifically exercise the "bag numbers recirculate, never trust them as identity" rule. Ran a full scenario pass against the **live deployed API** with a real partner token (after the owner added the `cashbag.countingresult.read` role — a fresh token had to be pulled, since roles are stamped in at issuance): both endpoints unauthenticated (401); missing dates, span >92 days, bad `dateType`, bad `status`, bad `pageSize` (400 `VALIDATION_ERROR` each, correct `field`); all 6 status-bucket filters returned exactly the expected subset, including the `received by bank`→`in-progress` and `missing`→`error` folds; `customerRefNumber` match and foreign-value-returns-empty-not-403; 3-page pagination over the 9 seeded docs with zero gaps or duplicates; continuation token correctly rejected when replayed against a different `pageSize` (proves the query-hash binding) and when garbage; single-bag lookup on the reused `bagNumber` correctly returned the newer (`counted`) record, not the older (`in-progress`) one; XML content negotiation. All of it showed up in App Insights (`CountingResultQuery` custom event × 13, matching request count, zero new exceptions beyond one pre-existing Cosmos cold-start timeout blip right after the deploy that never recurred). Seeded docs and the temporary Cosmos RBAC grant used to write them were both cleaned up afterward — the container is empty again, exactly as a pre-first-sync container should be.
>
> **Counting-result sync is live and confirmed working end-to-end with real data (2026-08-11).** First
> real `CountingResultSync` run: 220 rows upserted across 4 backfilled customers. Verified against the
> live API with a real partner token — real bag numbers show correct declared/counted/deviation amounts
> and accurate multi-step status history (e.g. registered → canceled → counted, matching what actually
> happened on-prem). Getting there surfaced several real bugs, all fixed:
> - The proc is named `api_CountingResultFetchChanges` (the `api_` prefix convention, matching
>   `api_CancelBagIfRegistered`) — `SQL/api_CountingResultFetchChanges.sql` drops any old differently-named
>   version and creates this one.
> - `NokasExternalCustomerMapping` lives in `[BizTalk_Helper]`, not `[Integrations.SN.CMS]` — don't
>   reintroduce the old DB name anywhere.
> - The Function's SQL login needed an explicit `GRANT EXECUTE` on the new proc — `db_datareader`-style
>   role membership does not cover stored procedures. `SQL/api_CountingResultFetchChanges_GrantExecute.sql`
>   grants it to whichever principal already has execute rights on `api_CancelBagIfRegistered`.
> - The proc originally used `STRING_SPLIT`, which needs database compatibility level ≥ 130 (SQL Server
>   2016) — this DB is below that. Rewritten to use an XML-based split instead, which works on older
>   compatibility levels.
> - `Nattsafearkiv.dblAmount`/`dblControlledAmount` are SQL `float`, not `decimal` — the Dapper row record
>   must type them as `double?`, not `decimal?`, or Dapper's constructor-based materialization fails
>   outright (throws instead of just losing precision). The conversion to `decimal` happens explicitly when
>   building the Cosmos document.
> - `CmsMappingConnString` needed both a Key Vault secret (owner added it) and the app-setting Key Vault
>   reference on `func-test-nokas-bagreg-v2` (added same way as `NattsafeSQLDBConnString`).
> - The Portal's "Test/Run" button needs the same direct network access and CORS origin
>   (`https://portal.azure.com`) as any other caller — used to manually fire the timer function once
>   during setup, both **removed again afterward** (temporary, not meant to be permanent, same as the
>   API's own `allow-dev-temp` rule).
>
> `IX_StatusTracking_dtmDate` and the `BackfillFromDate` column — owner says both were run; not
> independently re-verified by query (no on-prem SQL read access from the agent side). Neither is blocking
> (the sync completed successfully without needing to check).
>
> Deploy mechanics used this session (no dedicated `deploy-v2-countingresult.ps1` written — same manual
> `dotnet publish -c Release -o publish_X` → zip → `az webapp deploy`/`az functionapp deployment
> source config-zip` flow as the rest of v2; **consider writing a real script if this keeps being manual**,
> per the existing note about `deploy-v2.ps1`).

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

NokasIntegrationPortal/                Vite 8 + React 19 + TypeScript SPA (see Integration Portal section)
  src/components/ui/index.tsx          Shared design-system primitives - import via "@/components/ui"
  src/authConfig.ts                    MSAL config (B2C authorities)
  src/App.tsx                          Routes: public pages + /portal/* behind RequireAuth
  src/pages/private/                   Dashboard (dummy), BusinessActivityMonitoring (stub), Profile, api-guide/*
  src/pages/public/                    Landing, Product, Contact, AboutUs, CustomerOnboarding, PublicSwagger

B2C_CustomPolicies/                    Azure AD B2C Identity Experience Framework XML
  ...TRUSTFRAMEWORKBASE.xml            Base claims/technical profiles
  ...TRUSTFRAMEWORKEXTENSIONS.xml      Claims schema (api_* extension attrs), IdPs (AvarnAD/Google/Facebook), journeys
  ...SIGNUP_SIGNIN.xml                 Relying party (token claims contract)
  ...PASSWORDRESET.xml                 Relying party

src/                                    v2 rewrite (side-by-side with old projects above until cutover) — build via Nokas.CashMgmt.slnx
  Nokas.CashMgmt.Shared/                Cross-project code: 3 real consumers (API, Function, and eventually counting-result sync)
    StatusMapping/NattsafeStatusMapper.cs   Raw-code -> English-label map, single source of truth from day one
    Contracts/BagStatusRecord.cs            Function<->API wire contract for the status/{bagNumber} call
    Documents/                              BagNumberCacheDocument, SyncStateDocument (Cosmos, camelCase-attributed)
    Infrastructure/SystemTextJsonCosmosSerializer.cs   Required for ANY new Cosmos-writing project - see Known pitfalls
    Correlation/, Errors/, Results/          CorrelationConstants, ErrorResponse, Result<T>/Error (Kind-based)
  Nokas.CashMgmt.BagRegistration.Api/    POST/GET/DELETE bag-registration, fully built (Batches 01-03)
    Controllers/BagRegistrationController.cs
    Domain/                              BagCancellationDecision (pure), BagRegistrationOutboxPayload, BagCancellationOutboxPayload
    Repositories/                        CosmosBagRegistrationRepository, CosmosBagNumberCacheRepository
    Services/BagStatusFunctionClient.cs  Typed HttpClient + resilience handler calling the Function's status/{bagNumber}
    Configuration/BuildInfo.cs, SwaggerConfig.cs
  Nokas.CashMgmt.BagRegistration.Func/   Isolated worker, VNet-integrated into snet-test-openAPI
    Functions/ValidateBagNumberFunction.cs   GET validate/{bagNumber} - dead code path now (Batch 02 removed its only caller), kept as internal fallback tool per design
    Functions/GetBagStatusFunction.cs        GET status/{bagNumber} -> dbo.api_CancelBagIfRegistered (Nattsafe)
    Functions/SyncBagNumberCacheFunction.cs  Timer, 15 min -> bagnumber-cache container
    Services/BagStatusService.cs             Two SQL connection strings: NokasMwCacheDBConnString + NattsafeSQLDBConnString
tests/Nokas.CashMgmt.BagRegistration.Api.Tests/   xUnit, no mocking library (hand-rolled fakes) - 55 tests as of Batch 03

apimpolicy.xml                         APIM inbound policy: validate-jwt + rate-limit-by-key + backend rewrite
bag-counting-result-api-architecture.md   Base design doc for the counting-result extension (D1–D17) — now AMENDED by
                                          COUNTING_RESULT_DESIGN.md (2026-08-08); where they conflict, the newer doc wins.
REGISTRATION_REWRITE_DESIGN.md            Design lock for the full registration rewrite (see callout at top of this file)
COUNTING_RESULT_DESIGN.md                 Phase-1 (NOK-only) counting-result design lock
ExistingFullFlow.md                       Owner-authored end-to-end business flow (Cash Portal → BizTalk → Nattsafe)
SQL/                                      On-prem DDL + procs (source of truth for downstream behavior)
BizTalk/BagRegistration.xsd               Frozen Service Bus message contract (API delivers bagRegistration JSON body only)
```

## Architecture (request flows)

**Partner (M2M):** partner → token from `login.microsoftonline.com/{B2C tenant}` (client credentials, audience `https://nokasb2ctest.onmicrosoft.com/api/nokas`) → `api-test.nokas.com` → Front Door → APIM (JWT validation, 10 req/60s per app, backend rewrite) → Web API.

**POST bag-registration:** `[ApiController]` auto-validates the model (conditional: NOK requires `totalOrderAmount`, no `bagItemsFC`; Valuta requires `bagItemsFC`, no `bagItemsLC`) → call Function `validate/{bagNumber}` (duplicate check against on-prem cache DB) → map to document (`id = bagNumber`, `clientId` from token `azp`/`appid`, status `In-Progress`… response says `Registered`) → **one Cosmos transactional batch** creates the bag document *and* an `OutboxMessage` (subject `bagregistration`) atomically, same partition (unique index on `/BagRegistration/BagNumber` backs up the duplicate check). There is no direct Service Bus call in the request path — `OutboxDispatcherHostedService` polls for `Pending` outbox records every 5s and publishes them, retrying indefinitely on failure, so the partner's success/failure response depends only on Cosmos. Raw body archived to Blob via middleware → in-memory channel → hosted service. Returns `201 Created` with a `Location` header (not `200`).

**GET / DELETE / countingresult (single-bag, legacy path):** always scoped to the caller's `clientId` (Cosmos partition key). GET is a point read (`ReadItemAsync(bagNumber, clientId)`, since `id = bagNumber`) and no longer mutates the document. DELETE calls the Function `status/{bagNumber}`, whose proc `api_CancelBagIfRegistered` **performs the Nattsafe cancellation itself** (status→10 `'Kansellert'` when `intStatus ∈ {0,1,2}` and `dtmTransportFetch` is null; see `SQL/api_CancelBagIfRegistered.sql`) and returns the *pre-cancel* status — the "status check first" wording used here previously was wrong; it's attempt-cancel-and-interpret. The API then writes its own status update and a `deleteBagRegistration` outbox event in the same transactional batch (same pattern as POST); BizTalk's handling of that message is downstream bookkeeping, out of API scope per owner. Status-code map (keep API and Function copies in sync — they have already drifted; **there is now a third consumer of this map, the counting-result sync — see Conventions**): 0/1=registered, 2=in-progress, 3=picked-up, 4=received by bank, 5=counted, 10=canceled, 11=not approved, 12=approved by bank, 13=missing, -1/8/9=error; transport-fetched 0/1/2 ⇒ picked-up.

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
- Tenancy: `[BizTalk_Helper].dbo.NokasExternalCustomerMapping` maps B2C `ClientID` → many `NokasCustomerNumber` (= `Nattsafearkiv.vchCustomer_id`), each with `CustomerRefNumber` (partner's store id, e.g. GLN) + `CustomerRefName`. **Only mapped (onboarded) customers are ever synced.** Mapping is resolved at **sync time**, not request time — documents are fully stamped with tenancy so runtime API reads are single-partition Cosmos queries with zero on-prem or cross-reference lookups. Mapping is append-only in practice; corrections are rare and handled via the resync function.
- Counting fields: declared = `dblAmount`, counted = `dblControlledAmount`, countedAt = `dtmControlledAt`, pickedUp = `dtmTransportFetch`, registered = `dtmRegDate`. `deviation = countedAmount − declaredAmount`, computed at sync time; all three null until counted.
- All statuses are stored and returned (registered → counted → cancelled), plus a compact `statusHistory` array per document sourced from `StatusTracking`.
- `registrationChannel` derived from `vchMyReference LIKE 'API_%'` → `"API"` else `"Portal"`; informational only, **never used for authorization.**
- **`bagLifecycleId` — RESOLVED 2026-08-08: dropped from the phase-1 counting-result contract** (`COUNTING_RESULT_DESIGN.md` §2-A1). The old plan's premise was wrong: the Service Bus message **never carried** `bagLifecycleId` (verified against the outbox serialization code and the XSD), and Nattsafe schema changes are now barred anyway. Partners correlate by `bagNumber`, which is globally non-reusable for API-registered bags (the CacheDB duplicate check is permanent and not client-scoped). Documented fallback if ever demanded: sync-time Cosmos point-read using `vchMyReference`'s embedded clientId + bagNumber. `bagLifecycleId` remains in the registration 201 response exactly as today.
- **`currencyType` — phase-1 decision 2026-08-08:** response field kept but stamped constant `"NOK"` (phase 1 is NOK-only; everything in `Nattsafearkiv` is NOK-side by construction — Valuta went to AX). The filter parameter and its composite index are **deferred** to the DKK/Valuta phase (`COUNTING_RESULT_DESIGN.md` §2-A2). Eventual value set stays `NOK | DKK | Valuta` (SEK folded under `NOK` per business confirmation — known approximation).
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

- Container `countingresults`, PK `/clientId`, `id` = `nattsafeRecordId.ToString()`, TTL configurable (default 548 days). **If any document model here carries a nullable `ttl`, it must be serialised with `NullValueHandling.Ignore`** — `ttl` is a reserved Cosmos property that must be a valid integer whenever present, and emitting `"ttl": null` gets the document rejected with `400`. This exact bug broke every bag-registration POST (see Known pitfalls).
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

### Open architectural question — RESOLVED 2026-08-08

`NokasExternalCustomerMapping` **stays on-prem SQL**. Both blocking facts were answered by `ExistingFullFlow.md`: **BizTalk itself consumes the mapping table at message-processing time** — it resolves `CustomerRefNumber` → `NokasCustomerNumber` before calling the downstream service, for both the NOK and AX/Valuta branches (`AXProjectId` serves the AX branch) — so migrating the table would break BizTalk, which is out of bounds. The resolution never happens in the API at all. Do not revisit unless the BizTalk constraint itself changes.

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
- **Portal:** Node 24, `npm install && npm run dev`. `npm run typecheck` (`tsc -b --noEmit`) and `npm run lint` both pass clean — keep it that way; `build` is deliberately *not* gated on typecheck so a deploy is never blocked by it. Dev-server HTTPS is optional: certs at `cert/key.pem`/`cert/cert.pem` (mkcert) are used when present, otherwise it serves HTTP. Redirect URI is `window.location.origin`, so the B2C app registration must list whichever localhost origin you use. Note `/portal/*` sits behind `RequireAuth`, so previewing guide pages locally needs a B2C login.
- **B2C policies:** upload order matters — Base → Localization → Extensions → RP files. Tenant `nokasb2ctest.onmicrosoft.com` (TEST). Policy IDs are referenced by exact name in `authConfig.ts`.
- **Testing the API as a partner:** client-credentials token per the OAuth guide (`scope=https://nokasb2ctest.onmicrosoft.com/api/nokas/.default`), Postman collection in `wwwroot/docs/`.
- No automated test project for the OLD *registration* API — by explicit prior preference for that codebase; verification there is `dotnet build` + manual testing via Postman/Swagger. **The v2 rewrite and counting-result work do not carry this preference forward** — both get real unit test coverage (55 tests as of Batch 03 for v2 registration; see `tests/`).
- **v2 rewrite build/test**: solution file is `Nokas.CashMgmt.slnx` (repo root, the newer XML format — `dotnet build Nokas.CashMgmt.slnx` / `dotnet test tests/Nokas.CashMgmt.BagRegistration.Api.Tests`). This is **separate** from the old projects' own `.sln` files (`Nokas.CashMgmt.BagRegistration.API.sln`, `Nokas.CashMgmt.BagReg.Func.sln`) — the new solution doesn't reference the old projects at all, by design (they stay independently buildable/deployable until cutover, see the callout at the top of this file).
- **v2 deploy** currently has no dedicated script (the old `deploy-test.ps1` only knows the old projects) — deploys this session were done manually: `dotnet publish -c Release -o publish`, zip it, `az webapp deploy --type zip` (API) / `az functionapp deployment source config-zip` (Function). Consider writing a `deploy-v2.ps1` if this becomes frequent enough to be worth it.

## Deployment

**Deploy with the committed scripts — not Visual Studio.** The old `.pubxml` publish profiles are gitignored and no longer exist in the repo; only orphaned `serviceDependencies.*.json` remain. Everything is Azure CLI, so it behaves identically from VS Code, Visual Studio, or a plain terminal.

```text
deploy/deploy-test.ps1                            all logic + environment constants (single source of truth)
Nokas.CashMgmt.BagRegistration.API/deploy.ps1     thin wrapper -> -Target api
Nokas.CashMgmt.BagReg.Func/deploy.ps1             thin wrapper -> -Target func
NokasIntegrationPortal/deploy.ps1                 thin wrapper -> -Target portal
```

Each target builds → zips → deploys → **reads the live build stamp back** to prove the deployment actually landed. All three paths were re-verified end-to-end on 2026-08-04.

**Build stamps** (added so a silent deploy failure can't masquerade as success — they also encode the git SHA, e.g. `1.0.0+1396db50… @ 2026-08-04T22:07:22Z`):

| Component | Where the stamp appears |
|---|---|
| API | `X-Build-Stamp` response header on **every** response (incl. 401s) + the Swagger description, which also shows the environment |
| Function | `buildStamp` in `ValidateBagNumber` responses — deliberately on the **error paths too**, so it is visible even when on-prem SQL is down |
| Portal | `<meta name="build-stamp">` in `index.html` |

Deployment notes learned the hard way:

- `az webapp deploy` **merges** into `wwwroot` — deleted files stay served forever. The portal target passes `--clean true` (safe: `dist/` is a complete self-contained site including `web.config`). API and Function deliberately do **not** — their `wwwroot` was not inspectable (Kudu basic auth returns 401 on the API), and cleaning a directory you cannot inspect is not worth the risk. Consequence: stale assemblies linger there.
- The scripts set `$ErrorActionPreference = 'Continue'`, not `'Stop'`. In Windows PowerShell 5.1 native tools writing warnings to stderr surface as a terminating `NativeCommandError` even on success; every native call is checked via `$LASTEXITCODE` instead.
- Environments: TEST (`nokasb2ctest`, `api-test.nokas.com`) and PROD. Swagger restrictions and FDID checks activate only in `Production`.
- **PROD resources exist but are not live** (per project owner, 2026-08-04): `api-nokas-bagreg-prod` and `func-nokas-api-bagreg` in subscription **`corp-lz-prod-001`**, plus an APIM API `prod-nokas-bag-registration-api` (path `prod`) and Front Door route `apim-bagreg-prod`. These were created for testing and are **not** meant to be treated as production yet. When testing, make sure you are on `func-**test**-nokas-api-bagreg-cwe9eddzbmhjhtfe…` — the prod host `func-nokas-api-bagreg-bzdpf8b8dph8exbd…` looks similar and will 404 with a TEST key.

### TEST resource names (verified 2026-08-04)

Subscription `corp-lz-test-dev-001`, resource group `rg-nokasapi-cashmgmt` unless noted.

| Purpose | Resource |
|---|---|
| API | `api-nokas-bagreg` (.NET 8, AlwaysOn, **32-bit worker**, `ASPNETCORE_ENVIRONMENT=Production`) |
| Function | `func-test-nokas-api-bagreg` (dotnet-isolated, VNet → `snet-test-openAPI`) |
| Portal | `nokas-integration-portal`, custom domain `integration.nokas.com` |
| Cosmos | `nokasbagregistrationapitest` → db `BagRegistration` → containers `BagRegistrations` (PK `/clientId`, `defaultTtl = -1`) and `ClientDirectory` |
| Service Bus | namespace **`sb-test-crmintegration`** (`rg-appintegration-test-crm`), topic `sbt-bagregistration`, subs `sub-bagregistration` / `sub-bagdeletion` — **consumed by BizTalk**, so the subscription normally shows 0 active messages even when publishing works |
| Blob archive | `nokasintegrationarchive` / container `integrationarchive` |
| Key Vault | `kv-dev-nokasapi-cashmgmt` — exactly 4 secrets: `CosmosDb--KeyValue`, `BlobStorage--ConnectionString`, `ServiceBus--ConnectionString`, `NattsafeSQLDBConnString` |
| APIM | `apim-avarn-dev-test` (`rg-common-test-dev`), Developer SKU, API `nokas-bag-registration-api` (root path) |
| Front Door | `afd-test-dev` (`rg-common-test-dev`), domain `api-test.nokas.com`, route `apim-bagreg` |
| App Insights | `api-nokas-app-insights` (+ workspace `api-nokas-la-workspace`) |

**Front Door routing for `api-test.nokas.com`:** `/api/cash-mgmt/*` → APIM → App Service; `/swagger/*` → App Service **directly, bypassing APIM**; `/oauth2/v2.0/*` → `login.microsoftonline.com`.

### v2 TEST resource names (verified 2026-08-10)

Same subscription/resource group as above unless noted. All reused from the existing platform stack — no new Azure resource types were introduced for the rewrite.

| Purpose | Resource |
|---|---|
| API | `api-nokas-bagreg-v2` (net10.0, same `as-test-integrationops` App Service Plan as the old API/Portal/old Func — shared plan, so outbound IPs are identical across all apps on it). Regional VNet-Integrated (outbound) into `snet-test-openAPI`. Main-site inbound: `allow-apim` (`20.251.177.47/32`, unused today), `allow-afd` (`AzureFrontDoor.Backend`, unused today), `allow-dev-temp` (**temporary**, a specific dev-machine IP — remove at cutover), default deny. SCM unrestricted. |
| Function | `func-test-nokas-bagreg-v2` (dotnet-isolated net10.0, VNet → `snet-test-openAPI`, **has** a system-assigned Managed Identity — unlike the old Function, this was done right from the start). Main-site inbound: allow only `snet-test-openAPI`, default deny (no temp dev rule — only the API is meant to reach it). SCM unrestricted. |
| Cosmos | Same account `nokasbagregistrationapitest` → same db `BagRegistration` → new containers `bagreg-v2` (PK `/clientId`, id=bagNumber, unique key `/bagRegistration/bagNumber` — **camelCase path, this bit the team once, see Known pitfalls**) and `bagnumber-cache` (PK `/id`=bagNumber, holds a `sync-state` doc too). Old `BagRegistrations`/`ClientDirectory` containers untouched. |
| Key Vault | Same `kv-dev-nokasapi-cashmgmt`. New/changed secrets: `BagStatusFunction--FunctionKey` (rotates whenever the Function's `GetBagStatus`/`ValidateBagNumber` functions redeploy with a new default key — check this if DELETE starts 503ing with "unable to verify bag status"), `NattsafeSQLDBConnString` (was a **wrong placeholder** — wrong server, wrong DB (`tempdb`), Windows Integrated Auth that can never work from Azure — fixed 2026-08-10 to match the old Function's proven-working value: `10.141.35.100`/`Nattsafe`/SQL auth/`Encrypt=False`). |
| App Insights | Same `api-nokas-app-insights` — `cloud_RoleName` distinguishes `api-nokas-bagreg-v2` / `func-test-nokas-bagreg-v2` from the old apps in every KQL query. |

**v2 is NOT wired into APIM or Front Door** — reachable only directly via `*.azurewebsites.net`, gated by the access-restriction rules above. This is deliberate (see the callout at the top of this file for why) — don't assume `api-test.nokas.com` reaches v2 for anything.

**Live APIM policy** (API scope, `nokas-bag-registration-api`): `validate-jwt` against tenant `24aa7821…`, audience `https://nokasb2ctest.onmicrosoft.com/api/nokas`, then `rate-limit-by-key` **10 calls / 60 s** keyed on `iss|appid`, then backend rewrite. **The real TEST rate limit is 10/60s** — portal docs previously claimed 200/60s, which was wrong and has been corrected. The prod API definition uses 100/60s.

### Diagnostics (things that waste time if you don't know them)

- **App Insights via CLI:** `az extension add --name application-insights`, then `az monitor app-insights query --app api-nokas-app-insights -g rg-nokasapi-cashmgmt --analytics-query "<KQL>" --offset 6h -o json`. Use **`-o json`** — `-o table` silently renders nothing for many result shapes and looks like "no data".
- **`az` is a `.cmd` wrapper, so cmd.exe re-parses arguments.** Parentheses in JMESPath (`[?contains(name,'x')]`) and `&` in URLs both break it. Filter in PowerShell instead, or avoid those characters.
- **Reading a function key:** do **not** use `--query "'api-call-key'"` — in JMESPath a single-quoted token is a *string literal*, so that returns the text `api-call-key` (12 chars), not the key. Use `-o json | ConvertFrom-Json` then `.'api-call-key'`. Each function has its own `api-call-key`; the API uses that, not the host key.
- **The portal returns 200, not 404, for missing assets** — the IIS rewrite sends any unmatched path to `index.html`. Check `Content-Type` (`text/html` = the file is gone), never the status code.
- `curl.exe` output in PowerShell is a **string array**; `-match` on an array filters instead of setting `$Matches`. Join with `-join "\`n"` before regex matching.

## Integration Portal (rebuilt 2026-08-04)

**Brand:** the NOKAS green is **`#0e4938`** — sampled from the official logo two ways (the `fill` in `Nokas_Digital_Logo_stacked.svg`, and a pixel decode of `Nokas_Digital_Logo.png` returning `rgb(14,73,56)`). The codebase previously used `#014737` in ~48 places, which is a *different* colour and never matched the logo. The whole palette now derives from the real value via `tailwind.config.js` → `brand.800`. **Use the `brand-*` / `accent-*` Tailwind tokens, not raw hex.**

**Logo assets** (`public/`): `Nokas_Digital_Logo.png` (horizontal, black wordmark + green mark → light backgrounds), `Nokas_Digital_Logo_white.png` (pure white → dark backgrounds), `Nokas_Digital_Logo_stacked.*` (unused, kept), and `nokas-mark.svg` — the official wave mark only, white on a brand-green tile, used as the browser tab icon because the full wordmark is illegible at 16px. The old `logo.png`/`vite.svg` are deleted. The logo already contains the word "NOKAS", so **do not add a "NOKAS" text label beside it.**

**Design system:** shared primitives live in `src/components/ui/index.tsx` (`Card`, `CodeBlock` with copy buttons, `Badge`, `MethodBadge`, `Callout`, `Table`, `PageHeader`, `SectionHeading`, `JumpNav`, `CopyButton`, `InlineCode`). Import from `@/components/ui`. The `@/` alias is declared in **both** `vite.config.ts` and `tsconfig.app.json` — keep them in sync. Pages own their own width: `PublicLayout` no longer wraps children in a container, so pages can render full-bleed banded sections. `LandingPage` is the deliberate exception — it re-applies the original `max-w-5xl` container internally to preserve its previous look.

**Dependencies:** React 19.2, MSAL browser/react v5, react-router-dom 7, recharts 3, Vite 8, Tailwind 3.4. Notes:

- `package.json` previously could not install at all — `lucide-react: ^0.5.0` does not exist on npm, and `swagger-ui-react` was imported by `PublicSwagger.tsx` but absent from the manifest. A `package-lock.json` now exists (there was none), so builds are reproducible.
- **`swagger-ui-react` has been removed entirely.** `PublicSwagger` renders the live Swagger UI in an iframe; the library was only referenced by a fallback branch that could never execute.
- **MSAL v5 removed `cache.storeAuthStateInCookie`** — `CacheOptions` now accepts only `cacheLocation` and `cacheRetentionDays`. The old setting was being silently ignored.
- `vite.config.ts` guards the dev-server HTTPS certs behind `fs.existsSync` — Vite loads the config *before* setting `NODE_ENV`, so `isDev` is true even during `vite build`, and an unguarded `readFileSync` breaks the build on any machine without mkcert certs.

**Routing:** `/swagger` (lazy-loaded `PublicSwagger`) and `api-guide/use-cases` are now wired up — all three were previously orphaned components that no route referenced. There is a proper 404 page. `/portal` redirects to `/portal/api-guide`; **Dashboard and BusinessActivityMonitoring remain intentionally unlinked** because they are still placeholders.

**Caching — `public/web.config`:** `index.html` is served `no-cache, no-store, must-revalidate` and `/assets/*` is `immutable`. This is load-bearing, not an optimisation: `index.html` is the only file mapping to the current content-hashed bundle names, so a cached copy loads a stale bundle and any route added since that build 404s until the user hard-refreshes. That exact symptom was observed before the fix.

**Custom domain:** `integration.nokas.com` was down for ~6 weeks with an expired TLS cert — it was bound to the expired wildcard `*.nokas.com` (DigiCert, expired 2026-06-16) while a valid dedicated GeoTrust cert sat unused in the same resource group. Check cert bindings, not just DNS, when the portal appears unreachable.

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
- Security hardening: ✅ API-side role check done (`[Authorize(Policy = "PartnerAccess")]`, app role `cashbag.api.access` — see Conventions above for the App-Role-vs-scope gotcha). ✅ **Both `cashbag.registration.access` and `cashbag.countingresult.read` App Roles now exist** in the manifest, granted + admin-consented on the test client app only (additive, non-breaking — see the callout at the top of this file). **Still not done:** the actual rename of `cashbag.api.access` → `cashbag.registration.access` for real partners, and wiring `PartnerRegistrationAccess`/`PartnerCountingResultRead` policies into API code (no counting-result endpoint exists yet to protect) — both remain scheduled as implementation phase 5, a coordinated breaking change, not a silent deploy. ✅ **App Service access restriction to APIM is DONE** (verified 2026-08-04): `api-nokas-bagreg` has `ipSecurityRestrictionsDefaultAction: Deny`, allowing only the APIM IP `20.251.177.47/32` and the `AzureFrontDoor.Backend` service tag. Practical consequence — **you cannot test against `*.azurewebsites.net` directly, it returns 403; always go via `api-test.nokas.com`.** SCM/Kudu is unrestricted, so zip deploy works from anywhere. Still pending separately: APIM `required-claims` for the same role (confirmed absent); APIM `check-header` for `X-Azure-FDID` (confirmed absent — the API enforces FDID itself for Swagger instead); `ValidTokenIssuerPrefixes` in the AvarnAD technical profile must be pinned to Avarn's tenant (currently accepts any Entra tenant); social IdPs slated for removal.
- 🔴 **On-prem SQL credentials are in cleartext App Service settings** on `func-test-nokas-api-bagreg`: `NattsafeSQLDBConnString` and `NokasMwCacheDBConnString` contain server IP, user, password, and `Encrypt=False`. Key Vault *has* a `NattsafeSQLDBConnString` secret, but the Function cannot use it — **the Function App has no managed identity at all**, despite `KEYVAULT_NAME` being set. `AzureWebJobsStorage` is likewise a raw account key. Give the Function App a managed identity and move these to Key Vault references.
- ✅ Fixed: dual-write — POST/DELETE now write the bag document and an `OutboxMessage` atomically in one Cosmos transactional batch; `OutboxDispatcherHostedService` publishes to Service Bus from that committed state, retrying indefinitely rather than dropping on failure. See the outbox convention above before touching this path. **Verified working end-to-end on TEST 2026-08-04** (POST → Cosmos batch → outbox dispatch → `sbt-bagregistration` → BizTalk).
- ⚠️ **Lesson from 2026-08-04: "written" is not "executed."** The outbox code above was committed in August but the previous TEST deployment dated from **November 2025**, so it had never actually run. The first deploy exposed a bug that broke *every* POST:
  - `OutboxMessage.Ttl` is `int?` and stays null until published. Newtonsoft serialised it as `"ttl": null`, but `ttl` is a **reserved Cosmos property that must be a valid integer whenever present** (the container has `defaultTtl = -1`, i.e. TTL enabled). Cosmos rejected the outbox document with `400`, which failed the whole transactional batch. Fixed with `[JsonProperty("ttl", NullValueHandling = NullValueHandling.Ignore)]` — the property is now simply absent until `MarkOutboxMessagePublishedAsync` patches it to a real integer. **Never emit a null `ttl`.**
  - `ExceptionLoggingMiddleware` did `cosmosEx.Message.Contains(...)` — but `CosmosException.Message` is **null** when the repo builds it from `TransactionalBatchResponse.ErrorMessage`, which Cosmos leaves null for batch failures. The resulting `NullReferenceException` escaped the handler and turned a handled 400 into a bare 500, hiding the real cause. Now null-guarded.
  - Diagnosing a failed batch: the response reports **one** status for the whole transaction, and Cosmos returns the real status only on the *first failing* operation while marking the rest `424 FailedDependency`. `CosmosBagRegistrationRepo.CreateBagRegistration` now logs each operation's own result — keep that logging, it is what identified the culprit.
- `CreateBagRegistration` does **not** null-check `clientId`, unlike GET which returns `Unauthorized`. A token missing `azp`/`appid` would write a document with a null partition key rather than being rejected. Not yet fixed.
- ✅ Fixed: `PayloadArchiverHostedService`'s loop is now wrapped in try/catch (one failed upload no longer kills archiving permanently); `BlobArchiveService` no longer blocks DI by creating the container synchronously in its constructor. Still open: the archive queue itself remains in-memory (`Channel`) — a crash/restart/scale-in can still silently lose queued payloads; not yet durable.
- ✅ Fixed: GET is now a point read (`ReadItemAsync`) and no longer mutates the document (no more history-on-read).
- `EnsureClientInDirectoryAsync` and the `clientdirectory` single-document design are dead/legacy — don't build on them.
- B2C Extensions/Base: `api_companyName`'s `DefaultPartnerClaimTypes` points at the `api_clientId` extension attribute (copy-paste bug).
- No PUT endpoint exists although middleware archives PUTs and docs mention update.
- 🔴 **`BagStatusFunction:FunctionKey` is a REAL, LIVE key committed to `appsettings.json`** (added in `6945f3e`, pushed). Verified 2026-08-04: it matches the `api-call-key` **function-scope** key on all three functions of `func-test-nokas-api-bagreg`. The repo is private, so exposure is contained — but the key should be **rotated and removed from the file** (App Service already overrides it via the `BagStatusFunction:FunctionKey` app setting, so removing the committed value breaks nothing in Test/Prod). Note this entry previously claimed the config was *missing* from `appsettings.json` — that was wrong.
- This options class is `[Required]` + `.ValidateOnStart()`, so an environment missing these values fails at **startup**, not on first request — a misconfigured deploy shows up as blanket 500s, not a targeted error. Check App Service **Log stream** if that happens.
- `AutoMapper` 15.0.1 has a known high-severity vulnerability advisory (NU1903) — pending upgrade.
- Portal: ✅ **Rebuilt 2026-08-04** — see the Integration Portal section below. Still open: hard-coded client IDs (move to `VITE_*` envs); `i18next`/`react-i18next`/`i18next-browser-languagedetector` are installed but **never initialised**, so there is no working language switching (the decorative "EN" selector was removed); the main bundle is ~585 kB and could be code-split further.
- Status-code map drift between the registration API and the registration Function (§Architecture above) — this now matters even more with a third consumer (counting-result sync) about to read the same statuses; consolidate into one shared library as part of the counting-result work rather than adding a third copy.
- Target upgrades: .NET 10 LTS (both projects are still net8.0). ✅ Portal done — Node 24, React 19.2, MSAL v5, Vite 8.
- APIM `apim-avarn-dev-test` is **Developer SKU**, which carries no SLA and is not licensed for production use. Move to **Basic** (cheapest tier with an SLA) before go-live unless requirements demand higher.
- **v2-specific gotchas found during Batches 02-03 (apply to any new v2/counting-result work too):**
  - **Azure Functions isolated-worker `ConfigureFunctionsWebApplication()` requires the `Microsoft.Azure.Functions.Worker.Extensions.Http.AspNetCore` package + `<FrameworkReference Include="Microsoft.AspNetCore.App" />`** — removing either to "simplify" the csproj breaks the host with confusing, unrelated-looking DI/config errors (`IConfigurationSection.Get<T>()` not found, `CosmosClient` overload resolution failures) that look like something else is wrong. A function using native `HttpRequestData` (not ASP.NET Core's `HttpRequest`) can still coexist fine — the AspNetCore package brings the native `Http` types transitively, they're not mutually exclusive as an earlier pass through this code assumed.
  - **Dapper's `QueryAsync<(string, DateTime)>` (bare `ValueTuple`) is unreliable** — bind to a small `record`/class with matching property names instead (name-based, well-documented) rather than trusting positional tuple mapping.
  - **Key Vault references can serve a stale resolved value even after an explicit app restart** — seen twice on `NattsafeSQLDBConnString`. Fix: pin the App Setting to the exact new secret version (`.../secrets/{name}/{versionGuid}`) to force re-resolution, confirm it works, then revert to the floating/unversioned reference. If a Key Vault-backed connection string starts failing right after a secret rotation, try this before assuming the secret value itself is wrong.
  - Any **new Cosmos-writing project** needs the `SystemTextJsonCosmosSerializer` (`Nokas.CashMgmt.Shared/Infrastructure/`) wired into its `CosmosClientOptions.Serializer` explicitly — the SDK's default serializer ignores `[JsonPropertyName]` (it's an STJ-only attribute), so an unconfigured client silently writes `PascalCase` field names including `Id` instead of `id`, which Cosmos then can't find for id/partition-key extraction. Also still needs an explicit `Newtonsoft.Json` package reference regardless (a known, separate Cosmos SDK requirement) or the build fails outright.
  - When adding a `Microsoft.Azure.Cosmos` (or other Azure SDK) reference to `Nokas.CashMgmt.Shared` specifically, remember it's a plain `Microsoft.NET.Sdk` project (not `.Web`) — `ImplicitUsings` doesn't pull in `Microsoft.Extensions.Configuration`'s `Get<T>()` extension the way an ASP.NET Core project's implicit usings would; add the `using` explicitly.

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
