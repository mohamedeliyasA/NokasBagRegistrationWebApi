# NOKAS Cash Management Integration Platform — Architecture & Code Review

**Reviewed:** `Nokas.CashMgmt.BagRegistration.API` · `Nokas.CashMgmt.BagReg.Func` · `NokasIntegrationPortal` · `B2C_CustomPolicies` · APIM policy · OAuth guide
**Date:** 2026-07-11

Severity legend: 🔴 Critical (fix before/at go-live) · 🟠 High · 🟡 Medium · 🔵 Low / hygiene

---

## 1. Understanding of the Complete Solution

NOKAS/Avarn customers deposit physical cash bags. Before pickup and counting, the bag must be registered electronically. The platform provides:

- **Bag Registration API** (`api-test.nokas.com`) — partners' systems register/query/cancel bags via REST, authenticated with OAuth 2.0 client credentials against the `nokasb2ctest` tenant. Each partner has its own app registration (e.g. `NokasCashBagAPIPartner.JemnFix`) with the `cashbag.api.access` application permission on `Nokas.CashBagManagement.WebAPI`.
- **Request path:** Customer → Front Door → APIM (JWT validation + per-app rate limiting, backend rewrite to `api-nokas-bagreg-…azurewebsites.net`) → ASP.NET Core API (net8.0, despite the stated .NET 9).
- **API behavior:**
  - `POST /api/cash-mgmt/bag-registration` — synchronously calls the Azure Function (`validate/{bagNumber}`) which checks `dbo.Registration` in the on-prem `NokasMwCacheDB`; on pass, maps DTO → document (`id = bagNumber`, `clientId` from the token's `azp`/`appid`), publishes a `bagregistration` message to a Service Bus topic, then writes to Cosmos DB (partition key `clientId`). Raw payloads are archived to Blob Storage via an in-memory channel + hosted service, triggered by `RequestArchivingMiddleware`.
  - `GET .../{bagNumber}` — Cosmos query scoped to the caller's `clientId` partition; appends an `OperationHistory` "Read" entry and upserts the document on every read.
  - `DELETE .../{bagNumber}` — calls the Function's `status/{bagNumber}` (on-prem stored proc `dbo.api_CancelBagIfRegistered` against the Nattsafe DB), decides cancellability from `intStatus`/`dtmTransportFetch`, updates Cosmos, publishes a `deleteBagRegistration` message.
  - `GET /api/cash-mgmt/bag-countingresult/{bagNumber}` — calls the Function's `countingresult/{bagNumber}` (reads `Nattsafearkiv`), maps to a counting-result response.
- **Azure Function** (isolated worker, net8.0) — a synchronous HTTP bridge into on-prem SQL Server via VNet integration (Dapper + `Microsoft.Data.SqlClient`), function-key protected. It is *not* a Service Bus consumer; the write path to on-prem is the Service Bus topic consumed by a downstream system outside this review (BizTalk/integration layer).
- **Integration Portal** (Vite + React 18 + TS, MSAL Browser 2.x) — public marketing/onboarding pages + authenticated area (`/portal`) with API guide, embedded Swagger (iframe to `api-test.nokas.com/swagger`), Profile (raw ID-token claims), dummy Dashboard, and a stubbed Business Activity Monitoring page. Sign-in via B2C custom policies.
- **B2C custom policies (IEF)** — combined sign-up/sign-in with local accounts, "AvarnAD" (multi-tenant Entra ID via `login.microsoftonline.com/common`), Google, and Facebook. Sign-up collects `api_companyName`, `api_clientId`, `api_contactPerson`, `api_contactPersonEmail` as extension attributes, emitted in the ID token. Scaffolding exists (unused) for `role`, `uniquecustomerid`, `uniquechannelpartnerid` claims "picked from REST API response", a UserInfo endpoint journey, and a refresh-token journey.
- **The bridge between the two worlds** (your design question): the self-asserted `api_clientId` claim is intended to link a portal user to their API client ID, so BAM queries can filter Cosmos by `clientId`. Employees (AvarnAD sign-in) bypass the filter. Confirmed decisions: BAM endpoints will extend the existing API; AvarnAD sign-in ⇒ admin; a separate scheduled sync (~every 2 h) will copy status from on-prem into Cosmos.

## 2. Questions / Clarifications

Resolved in conversation. Remaining minor unknowns that don't block the review: the Service Bus topic's downstream consumer (assumed BizTalk-era integration layer), the actual App Service network configuration (assumed *not* locked to APIM — see 6.3), and whether Facebook/Google sign-in is intentional for production.

## 3. End-to-End Architecture

```
                        (M2M)                                        (Humans)
Partner system                                     Customer user            NOKAS employee
     │ client-credentials token                          │  B2C custom policy    │ AvarnAD (Entra)
     ▼                                                   ▼                       ▼
Entra ID (B2C tenant, AAD side)                   Azure AD B2C (IEF policies, MSAL SPA)
     │ Bearer JWT                                        │
     ▼                                                   ▼
api-test.nokas.com → Front Door → APIM ──────►  Integration Portal (React SPA)
                              │ validate-jwt,            │ (BAM: future /portal/* endpoints)
                              │ rate-limit-by-key        │
                              ▼                          │
                    ASP.NET Core Bag API  ◄──────────────┘
                    │        │        │
        ┌───────────┤        │        └── Blob Storage (payload archive)
        ▼           ▼        ▼
   Cosmos DB   Service Bus  Azure Function (HTTP, VNet)
   (bagreg,     topic  │         │ Dapper
    clientdir)         ▼         ▼
              downstream    on-prem SQL Server
              consumer      (Nattsafe, NokasMwCacheDB)
                 │                ▲
                 └── writes ──────┘
```

**Strengths of the current shape:** clear layered ingress (FD → APIM → API); tenant-per-partner identity with `clientId` as the natural partition key; correlation IDs propagated end-to-end (API → Function, response headers, logs); payload archiving for auditability; Cosmos unique-key safety net for duplicate bags; the Function isolates on-prem connectivity behind a single VNet-integrated component.

## 4. Business Flow

1. **Onboard:** customer emails BizTalkOps → NOKAS creates a partner app registration, grants `cashbag.api.access`, issues client ID + secret (12-month expiry) → customer signs up on the portal and types the client ID into the B2C sign-up form.
2. **Register bag:** partner system POSTs the registration (NOK with denominations/total, or Valuta with FC items — conditional validation via `IValidatableObject`). API checks bag doesn't already exist on-prem, stores it, and emits an event; the downstream consumer writes it to on-prem systems ahead of physical pickup.
3. **Track:** partner can GET the bag, GET the counting result (declared vs counted vs deviation once `intStatus = 5`), and DELETE (cancel) while the bag is still cancellable (`intStatus ∈ {0,1,2}` and not yet fetched by transport).
4. **Lifecycle statuses** (from on-prem): registered → in-progress → picked-up → counted → received/approved by bank, with error/cancelled/missing branches. `MapStatus` translates codes to API vocabulary.
5. **Future BAM:** customers see their own orders/status/progress; employees see and search all customers.

## 5. Authentication & Authorization Flow

**API (M2M):** partner → `login.microsoftonline.com/{tenant}/oauth2/v2.0/token` (client credentials, scope `…/api/nokas/.default`) → v1-issuer JWT (`sts.windows.net/{tenant}/`) → APIM `validate-jwt` (issuer + audience only) → API `JwtBearer` (authority/audience/issuer/lifetime/signing key) → `[Authorize]` (authentication only — **no role/claim requirement anywhere**) → `ClaimsHelper.GetClientId` reads `azp`/`appid` for data scoping.

**Portal (users):** MSAL redirect → B2C `SIGNUP_SIGNIN` journey → local account, AvarnAD (any Entra tenant — `ValidTokenIssuerPrefixes = https://sts.windows.net/`), Google, or Facebook → ID token with the four self-asserted `api_*` claims → SPA route guard checks `isAuthenticated` only. The SPA never acquires an access token for any API (scopes are just `openid profile`), and no server ever validates a portal user's token today.

**The two worlds do not meet yet.** The only link is the *unverified, user-typed* `api_clientId` claim.

---

## 6. Detailed Architecture Review

### 6.1 Strengths
Sound ingress layering; correct multi-tenant data model instinct (partition by `clientId`); event publication toward downstream instead of point-to-point coupling; Key Vault-backed configuration; correlation-ID discipline; conditional model validation done properly; the Function keeps SQL connectivity out of the API.

### 6.2 Weaknesses & technical debt

| # | Severity | Finding |
|---|---|---|
| A1 | 🔴 | **Authorization is authentication-only.** Neither APIM (`validate-jwt` has no `required-claims`) nor the API (`[Authorize]` with no policy) checks the `roles` claim for `cashbag.api.access`. Any token minted in that tenant for that audience passes. |
| A2 | 🔴 | **Synchronous availability coupling to on-prem for writes.** `POST` cannot succeed unless the Function *and* on-prem SQL are up (duplicate pre-check). A cloud API's write path now has on-prem availability. Also a TOCTOU race: two concurrent POSTs for the same bag both pass "validate" then both proceed (only the Cosmos unique index saves you, and only within one client's partition). |
| A3 | 🔴 | **Dual-write inconsistency.** `POST` sends the Service Bus message *before* the Cosmos write; if Cosmos fails (duplicate, throttle), downstream has already received a Create event with no compensating action. `DELETE` has the mirror problem (Cosmos updated, then message send can fail). No outbox, no idempotency on the consumer side is evident. |
| A4 | 🟠 | **Archive queue is in-memory** (`Channel` + hosted service). Any crash/restart/scale-in silently loses queued audit payloads — the one thing an audit archive must not do. There is also no retry: if a blob upload throws, `ExecuteAsync` crashes the background loop for good. |
| A5 | 🟠 | **GET mutates state.** Every read appends to `OperationHistory` and upserts the whole document: doubles RU cost, adds write latency to reads, creates concurrency conflicts (no ETag/`IfMatchEtag` used anywhere), and grows the document without bound toward Cosmos's 2 MB limit. |
| A6 | 🟠 | **`ClientDirectory` is a single "static-client-list" document** — a global hot partition with a 2 MB ceiling and read-modify-write races (no ETag). It's also dead code: `EnsureClientInDirectoryAsync` is never called. |
| A7 | 🟡 | **Update (PUT) is missing** — the middleware archives PUTs and the brief promises update, but no PUT endpoint exists. |
| A8 | 🟡 | Three JSON serializers in play: Newtonsoft (MVC + Cosmos SDK default), System.Text.Json (`JsonSerializer.Serialize` for Service Bus, `FunctionInvoker`), plus XML formatters. Models mix `[JsonProperty]` and `[JsonPropertyName]`; Cosmos ignores `[JsonPropertyName]`, MVC-Newtonsoft ignores it too — so wire/storage casing is accidental, not designed. One serializer (STJ) with an explicit Cosmos serializer options is the fix. |
| A9 | 🟡 | Scalability: fine for current volumes, but admin cross-customer search will be cross-partition scans; no continuation-token paging exists anywhere yet; Service Bus sender per call (never disposed) leaks AMQP links under load. |
| A10 | 🔵 | Maintainability: no tests at all, no solution-level layering (controllers do orchestration, mapping, messaging, and status policy), file `BagRegistrationRequest .cs` has a space in the name, `*-MohamedEliyas.*` developer copies are committed in the portal, README is the VS template. |

### 6.3 Azure architecture gaps

- 🔴 **APIM does not verify Front Door**, and (as far as code/config shows) the App Service does not verify APIM. The `X-Azure-FDID` check exists only in the API's Swagger middleware, only in Production, only for swagger paths. Anyone who discovers `api-nokas-bagreg-….azurewebsites.net` bypasses Front Door WAF *and* APIM rate limiting entirely. Fix at the network layer: App Service access restrictions (allow only APIM subnet/IP, `X-Azure-FDID` service tag rules) or private endpoint + APIM VNet integration; add `check-header` for `X-Azure-FDID` in APIM inbound policy.
- 🟠 **Secrets instead of identities**: Cosmos account key, Blob connection string, Service Bus connection string, Function key. All four support Microsoft Entra auth with Managed Identity + RBAC (`DefaultAzureCredential`), which eliminates rotation risk and Key Vault sprawl. Keep Key Vault only for the on-prem SQL connection strings.
- 🟡 Function key travels in the query string (`?code=…`) — ends up in App Service/Front Door/proxy logs. Use the `x-functions-key` header at minimum; better, Entra-authenticated calls between API and Function, or put the Function behind the same VNet privately.

---

## 7. ASP.NET Core Code Review

**Program.cs / composition**
- `builder.Host.ConfigureAppConfiguration` + `config.Build()` is the legacy pattern and builds configuration twice. On .NET 8+: `builder.Configuration.AddAzureKeyVault(...)` directly.
- `AddHttpClient()` is registered twice; `IHttpClientFactory` is injected as *nullable* into the controller and never used there — dead dependencies (`IConfiguration`, `IHttpClientFactory`, `IBlobArchiveService` in both controllers) should be removed.
- Options classes are bound but the repo reads `IConfiguration["CosmosDb:…"]` directly — use `IOptions<CosmosDbOptions>` consistently, add `ValidateDataAnnotations().ValidateOnStart()` so a missing setting fails at startup, not first request.
- `CosmosClient` should be created via `AddSingleton` with `CosmosClientOptions` (Direct mode, `ApplicationName`, retry options, camel-case serializer) and Entra auth.
- Middleware order: `ExceptionLoggingMiddleware` runs *before* `CorrelationIdMiddleware`, so exceptions thrown before/inside correlation setup fall back to raw header reads; swap them. `UseHttpsRedirection` after `UseStaticFiles` also means archive/static requests skip redirect; put redirection first.
- No `AddHealthChecks()`/`MapHealthChecks("/healthz")` — Front Door and APIM need a probe target; no `AddApiVersioning` (`Asp.Versioning.Mvc`); no `AddProblemDetails()`.

**Controllers**
- 🔴 With `[ApiController]`, invalid `ModelState` returns 400 *before* the action runs — your `BuildValidationError` branch is unreachable, and the response contract for validation errors is the framework's `ValidationProblemDetails`, not your `ErrorResponse`. Either suppress the filter or (better) delete the manual check and customize `InvalidModelStateResponseFactory` so all errors share one shape.
- `POST` returns `200 Ok` — should be `201 CreatedAtRoute("GetBagRegistration", …)`.
- Controllers inherit `Controller` (MVC views) instead of `ControllerBase`.
- No `[ProducesResponseType]` attributes → Swagger consumers can't see the 409/503/408 contracts you actually return.
- `DELETE` is ~180 lines mixing HTTP parsing of on-prem JSON, status policy, persistence, and messaging — extract a `BagCancellationService`; the duplicated `MapStatus` (API + Function, already drifted: API maps `3 => picked-up`, adds transport-fetch override) belongs in one shared library.
- No `CancellationToken` (`HttpContext.RequestAborted`) is passed to any Cosmos or Service Bus call — only to the Function invoker.
- `GetBagByNumberForClientAsync` is a SQL query, but `id = bagNumber` and PK = `clientId` — this is a **point read**: `ReadItemAsync<T>(bagNumber, new PartitionKey(clientId))` (~1 RU vs ~3+ RU and lower latency).

**Services / resilience**
- `FunctionInvoker` mutates `DefaultRequestHeaders` on a factory client (a shared-handler client — header check papers over the smell), has **no timeout** (a hung on-prem SQL call holds the request for 100 s default) and no retry/circuit breaker. Use a **typed client** with `AddStandardResilienceHandler()` (Microsoft.Extensions.Http.Resilience), 5–10 s timeout, and per-request headers.
- `ServiceBusSender` creates a new sender per message and never disposes it; create one `ServiceBusSender` per topic as a singleton (`IAsyncDisposable`).
- `BlobArchiveService` calls `CreateIfNotExists()` synchronously in the constructor (blocks DI, throws at first resolution if storage is unreachable) — move to an async init or `EnsureCreated` in the hosted service.
- `PayloadArchiverHostedService.ExecuteAsync` has no try/catch around the loop body — one failed upload kills archiving until the process restarts.

**Logging / observability**
- 🔴 **Serilog `MinimumLevel.Default` is `Error`** — every `LogInformation`/`LogWarning` in the codebase (correlation traces, bag-not-found, swagger blocks…) is dropped in the deployed configuration. Set `Information` for your own namespaces, keep `Error/Warning` overrides for `Microsoft.*`.
- Static `Log.Error` in `AuthConfig` bypasses the DI pipeline; inject `ILogger` via `context.HttpContext.RequestServices`.
- Consider OpenTelemetry (traces + metrics) via `Azure.Monitor.OpenTelemetry.AspNetCore` — you get Cosmos/HTTP/Service Bus dependency correlation for free and it's the strategic path App Insights SDKs are converging on.

**Validation & DTOs**
- Amounts are strings end-to-end (`TotalOrderAmount`, `dblAmount`…) — no range/format validation; a partner can send `"abc"`. Use `decimal` with converters, or at least `[RegularExpression]`.
- `BagNumber` has no length/format constraint despite the comment saying business rules exist.
- The GET returns `BagRegistrationRequestForCreation` (a *ForCreation* DTO as a read model) — introduce `BagRegistrationResponse` including status/lifecycle ID.

**Security in code**
- `HideInternalFieldsSchemaFilter` + `MapType<BagRegistrationRequest>` hide internals from Swagger, but the GET response mapping is what actually protects data — good; keep DTO separation strict.
- Swagger "modify block" via `Referer` is trivially spoofable and blocks nothing a curl user can't do; rely on authorization, not referers.

---

## 8. Azure Services Review

**Azure AD B2C** — Two roles conflated in one tenant: workforce-style app registrations for M2M (fine, standard workaround since B2C had no first-class client credentials) and CIAM for portal users. Works, but: (a) 🔴 `ValidTokenIssuerPrefixes = https://sts.windows.net/` accepts **every Entra tenant on the planet** as "AvarnAD" — combined with "AvarnAD ⇒ admin" this makes *anyone with any Entra account a NOKAS administrator*. Pin it to Avarn's tenant GUID(s). (b) Facebook/Google sign-in on a cash-logistics portal is an odd trust surface — remove unless there's a real requirement. (c) `api_companyName`'s `DefaultPartnerClaimTypes` maps to `extension_…_api_clientId` (copy-paste bug in both Extensions and Base). (d) The `role`/`uniquecustomerid` claim scaffolding + UserInfo journey you already built is exactly the hook the BAM design needs (see §10). (e) Strategic: Microsoft has stopped selling B2C to new customers and is investing in **Microsoft Entra External ID**; keep policies thin and portable, plan an eventual migration. (f) App Insights connection string and client IDs are hard-coded in policy XML — parameterize per environment in your policy build pipeline.

**Cosmos DB** — Partitioning on `clientId` is right for the workload and for BAM. Fix: point reads instead of queries (§7); pass cancellation tokens; use ETag optimistic concurrency for updates; cap or externalize `OperationHistory` (move history to a separate container partitioned by `bagNumber`/`clientId`, or a time-series container — reads stop rewriting documents); replace the single client-directory document with one document per client (`id = clientId`, PK = `clientId`); define a composite index (`clientId ASC, registrationDate DESC`) for BAM list queries; use Entra RBAC data-plane auth instead of the account key; consider autoscale RU and, later, analytical store/Fabric mirroring for reporting instead of hammering the transactional store.

**Service Bus** — Sender lifecycle bug (§7). Add: `MessageId = bagLifecycleId` for broker-level dedup; sessions or partition keys if ordering per bag matters; dead-letter monitoring/alerts; and solve the dual-write with either the **transactional outbox** (write an outbox item in the same Cosmos transactional batch, a background dispatcher publishes) or **Cosmos change feed → publisher** — then the Cosmos write is the single source of truth and Service Bus emission is at-least-once from committed state.

**Blob Storage** — Replace the in-memory channel with direct async upload (payloads are small; the latency you're hiding is ~tens of ms) or a durable queue if you must decouple. Name blobs `yyyy/MM/dd/{clientId}/{bagNumber}_{action}_{guid}.json` for findability; add lifecycle management (cool/archive tiers, retention policy) and immutability (WORM) if the archive is a compliance artifact; use Managed Identity.

**Key Vault** — Good pattern (empty appsettings + KV-provided values). Reduce what lives there by moving to managed identities; add secret-expiry alerts (partner client secrets expire yearly — today that's an outage with no warning).

**Application Insights** — Serilog min-level bug (§7) means you're nearly blind. Also: adaptive sampling disabled + Information logging re-enabled will get expensive — re-enable sampling or set per-table caps; add availability tests against `/healthz` through Front Door; alert on Function 503s and Service Bus DLQ depth.

**API Management** — Policy is minimal. Add: `check-header` for `X-Azure-FDID`; `required-claims` in `validate-jwt` (`roles` contains `cashbag.api.access`); per-partner rate limits/quotas (differentiated tiers rather than a flat 10/min); `set-header` to strip/replace inbound `X-Forwarded-*`; consider subscription keys per partner as a second factor and for revocation independent of Entra; response caching for `countingresult` (e.g. 30–60 s) to shield on-prem; emit APIM diagnostics to the same App Insights.

**Front Door** — Ensure WAF policy (managed ruleset + rate limiting) is attached; origin should require the private/locked APIM, not a public gateway; health probes to `/healthz`.

---

## 9. Integration Portal Review

**Structure & practices** — Clean routing split (public/private), layouts, and small components; TypeScript throughout. Gaps:

- 🔴 `swagger-ui-react` (and implicitly its CSS) is imported in `PublicSwagger.tsx` but **absent from `package.json`** — a fresh `npm ci && npm run build` fails. Add it or drop the page (the iframe approach in `SwaggerSection` already covers the need).
- 🟠 MSAL: `@azure/msal-browser` 2.38.3 / `msal-react` 1.5.8 are end-of-life; migrate to msal-browser v4 + msal-react v3. While migrating: `cacheLocation: "sessionStorage"` is the safer default for a finance portal; `storeAuthStateInCookie` only if you must support legacy browsers.
- 🟠 Hard-coded `clientId`/authority in `authConfig.ts` — move to `import.meta.env.VITE_*` per environment.
- 🟡 Dependency hygiene: `@types/axios` (deprecated — axios ships its own types), `@types/react-router-dom@5` against router v6, `@types/react`/`@types/react-dom` v19 against React 18, `@types/recharts` (recharts ships types), `lucide-react@^0.5.0` (ancient), `i18next`/`recharts` installed but unused. Delete the `*-MohamedEliyas.*` shadow copies and `package-MohamedEliyas.json` from source control.
- 🟡 `sandbox="allow-same-origin allow-scripts"` on the Swagger iframe neutralizes the sandbox (same-origin + scripts = escape hatch); since you trust your own Swagger host, drop `sandbox` or drop `allow-same-origin`.
- 🔵 `RequireAuth` has no role concept; there is no error boundary, no route-level code splitting (`React.lazy`), no data-fetching layer yet (fine — none is needed until BAM).

**Node.js upgrade** — Target **Node 24 (Active LTS, supported to 30 Apr 2028)**; Node 22 moves to maintenance and dies 30 Apr 2027. Practically: your toolchain (Vite 7, ESLint 9, TS 5.8) already supports Node 24, so the upgrade is: add `"engines": { "node": ">=24" }` + `.nvmrc`/`.node-version` with `24`, update CI/CD images, `npm ci`, run build + smoke test. No code changes expected; the only usual watch-items are native deps (you have none) and OpenSSL-related cert handling in the dev-server HTTPS config. Skip Node 26 (Current) until it reaches LTS in Oct 2026.

---

## 10. Review of the Customer/Employee Order-Visibility Design

**Your idea** (self-asserted `api_clientId` at B2C sign-up → portal filters Cosmos by it; AvarnAD ⇒ see everything) is directionally right — `clientId` *is* the correct tenant key, and Cosmos is already partitioned by it. But as implemented it has three fatal flaws:

1. **The claim is attacker-chosen.** Anyone can sign up (with a Gmail or Facebook account!) and type any client ID they've guessed or seen in an email. The token then "proves" they own that customer's data. A B2C claim is only as trustworthy as its provenance, and self-asserted sign-up input has none.
2. **The admin gate is broken upstream.** "Signed in via AvarnAD" currently means "signed in with *any* Entra ID tenant" (`ValidTokenIssuerPrefixes` accepts all issuers). Until that's pinned to Avarn's tenant, AvarnAD ⇒ admin is a critical hole, not a role model.
3. **Nothing enforces any of it server-side.** Filtering "in the portal" is client-side security. The SPA must never hold the query predicate; the API must derive it from a validated token.

**Recommended design — verified mapping + claims enrichment + server-side enforcement:**

**(a) Treat sign-up input as an onboarding request, not a fact.** Keep collecting `api_clientId` at sign-up, but store it as *pending*. Nothing grants access.

**(b) Hold the truth server-side in Cosmos.** New `userAccess` container (PK `/userObjectId`):
```json
{
  "id": "<b2c objectId>",
  "userObjectId": "<b2c objectId>",
  "role": "customer" | "admin",
  "clientIds": ["<api client id>", "..."],
  "status": "pending" | "approved" | "revoked",
  "approvedBy": "<admin objectId>", "approvedAt": "..."
}
```
A NOKAS admin approves the mapping in the portal (or it's auto-created by the same internal process that creates the partner app registration — best of all, since that's the moment NOKAS *knows* which company owns which client ID). One user can map to multiple client IDs; one client ID can have many users.

**(c) Enrich the token — you already built the hook.** Add a REST claims-provider step to the sign-in journey (the `role`, `uniquecustomerid` claim types and the UserInfo journey are already scaffolded for exactly this): after authentication, B2C calls a small endpoint on your API (`POST /internal/claims-enrichment`, certificate or client-credentials protected) with the user's `objectId`; it returns `role` and the approved `clientIds`. These land in the token as claims. Employees: pin AvarnAD to the Avarn tenant GUID and have the enrichment endpoint return `role=admin` for `identityProvider=AADCommon` users from that tenant (or, stricter, check Entra group membership via Graph).

**(d) Enforce in the API, not the SPA.** Extend the existing API (your chosen option) with a second JWT bearer scheme:

```csharp
services.AddAuthentication()
  .AddJwtBearer("PartnerM2M", o => { /* existing sts.windows.net config */ })
  .AddJwtBearer("PortalUser", o => {
      o.Authority = "https://nokasb2ctest.b2clogin.com/.../B2C_1A_NOKASINTEGRATIONPORTAL_SIGNUP_SIGNIN/v2.0";
      o.Audience  = "<portal API app id>";
  });

services.AddAuthorizationBuilder()
  .AddPolicy("PartnerAccess", p => p.AddAuthenticationSchemes("PartnerM2M")
      .RequireRole("cashbag.api.access"))                      // fixes A1 too
  .AddPolicy("PortalCustomer", p => p.AddAuthenticationSchemes("PortalUser")
      .RequireClaim("role", "customer", "admin"))
  .AddPolicy("PortalAdmin", p => p.AddAuthenticationSchemes("PortalUser")
      .RequireClaim("role", "admin"));
```

New endpoints (`/api/portal/orders…`) authorize with the portal policies and **derive the Cosmos filter from token claims** — never from a query parameter:
- customer → `WHERE c.clientId IN (token.clientIds)` executed per-partition (or fan-out over the user's few client IDs);
- admin → cross-partition query with paging (continuation tokens), optionally backed later by a search/materialized view for free-text search across customers.

Even if enrichment fails or feels heavy, the fallback is the API reading the `userAccess` document per request (cache 5 min) — claims-in-token is an optimization, the server-side mapping is the security boundary. **The SPA finally requests an access token** (`loginRedirect`/`acquireTokenSilent` with your API scope) instead of `openid profile` only.

**(e) Portal reads Cosmos only — never on-prem.** Your planned 2-hourly status sync Function (timer trigger) updates `RegistrationStatus` (+ one history entry) for open bags. Suggestions: sync incrementally (track `dtmRowVersion`/last-sync watermark instead of scanning all open bags), use bulk upserts with ETag, and consider event-driven push later; 2 h polling means BAM status can be 2 h stale — display "last updated" in the UI so customers aren't confused. Failed requests (validation failures never stored in Cosmos) are visible today only in App Insights; if BAM must show them, log rejected requests as lightweight Cosmos documents too (`status: "Failed"`, reason, correlation ID) — cheap and makes the "failed requests" requirement real.

**Why not other patterns:** separate B2C tenants per customer (overkill, operational pain); Cosmos resource tokens to the SPA (deprecated direction, no admin story); a separate BAM microservice (fine later, but you chose extension and the domain is one bounded context — a `/portal` area inside the API with separate auth policies is proportionate).

## 11. Suggested Improved Architecture

No big-bang redesign is warranted — the shape (FD → APIM → API → Cosmos/SB, Function to on-prem) is right. The redesign is surgical:

1. **Lock the doors:** App Service access restrictions to APIM only; APIM checks `X-Azure-FDID`; `required-claims` role check in APIM; role policy in API; pin AvarnAD issuer; drop social IdPs.
2. **Managed identity everywhere:** Cosmos RBAC, Blob, Service Bus, Function-to-API auth. Key Vault keeps only on-prem SQL strings.
3. **Make Cosmos the source of truth:** outbox/change-feed publishing (kills the dual-write), point reads, ETags, history externalized, status maintained by the sync Function → the API stops calling on-prem synchronously for GET/countingresult once the sync is trusted (keep the synchronous path only where real-time truth is mandatory: cancellation eligibility).
4. **Make duplicate-check local:** the on-prem `validate` pre-check can be replaced by the Cosmos unique key + the sync (or an async rejection event), removing on-prem from the write path entirely. If business rules require checking legacy bags registered outside the API, keep the check but make it fail-open with a compensating async validation, so cloud writes don't die when on-prem sleeps.
5. **BAM as described in §10**: dual-scheme auth on the same API, claims enrichment, `userAccess` container, `/api/portal/*` endpoints, timer-sync Function.
6. **Pipeline hygiene:** IaC (Bicep) for APIM policies/App Service/Front Door so the security config is reviewable; B2C policy build per environment.

## 12. Performance Recommendations

Point reads instead of queries (~1 RU); stop upserting on GET; pass `CancellationToken` everywhere; singleton Service Bus sender; typed HttpClient with 5–10 s timeout + standard resilience handler; Cosmos Direct connection mode; composite indexes + `ORDER BY` for BAM lists with continuation-token paging (never `OFFSET`); APIM response caching for counting results; response compression; async blob upload without the channel hop; re-enable App Insights sampling. The single biggest latency item is the synchronous on-prem round-trip on every POST/DELETE/countingresult — §11.3–4 removes most of it.

## 13. Security Recommendations (ordered)

1. 🔴 Pin `ValidTokenIssuerPrefixes` to Avarn's tenant; until then, do not ship "AvarnAD ⇒ admin".
2. 🔴 Enforce `cashbag.api.access` role in APIM and in the API.
3. 🔴 Network-lock the App Service to APIM; APIM `check-header` on `X-Azure-FDID`.
4. 🔴 Never trust self-asserted `api_clientId` — implement the verified `userAccess` mapping (§10) before any BAM data is exposed.
5. 🟠 Managed identities for Cosmos/Blob/Service Bus; retire the Cosmos account key.
6. 🟠 Function key out of query strings; prefer Entra auth API→Function.
7. 🟠 Remove Facebook/Google IdPs (or isolate them from any data access).
8. 🟡 Stop returning SQL server/database/procedure names from the Function's error responses (info disclosure to any caller holding the function key, and it ends up in API logs).
9. 🟡 Secret-expiry alerting for partner client secrets; document rotation.
10. 🟡 Fix the iframe `sandbox`; add CSP headers to the portal (web.config) and `frame-ancestors` on the Swagger host.
11. 🔵 Remove App Insights connection string / client IDs from committed policy XML via pipeline substitution; fix the `api_companyName`→`api_clientId` extension-attribute mapping bug.

## 14. Scalability Recommendations

Cosmos autoscale RU with `clientId` partitions (already right); externalized history container; per-partner APIM quotas so one partner can't exhaust the flat rate; Service Bus Standard→Premium only when volume justifies; the Function is the choke point to on-prem — keep it out of hot paths (§11) and set `maxConcurrentRequests`/SQL pool sizing deliberately; admin search at scale → change-feed-fed materialized view (by date, by status) or Azure AI Search index rather than cross-partition scans; App Service autoscale rules on RPS + health.

## 15. Modernization Recommendations

.NET: both projects are net8.0 (EOL Nov 2026) — go to **.NET 10 LTS** (supported to Nov 2028); it's a small hop (TFM bump, package updates, test). Adopt `Microsoft.Identity.Web` for auth config, `Asp.Versioning`, OpenTelemetry, `Microsoft.Extensions.Http.Resilience`. Consider minimal APIs for the new `/portal` endpoints or stay with controllers for consistency — either is fine, consistency matters more. Portal: Node 24 LTS, React 19, MSAL v4, envs via Vite, delete dead deps, add ESLint type-aware rules + Prettier + a CI build. B2C: plan the Entra External ID path; meanwhile parameterize policies per environment. Add a test baseline: xUnit + `WebApplicationFactory` integration tests with the Cosmos emulator + Testcontainers SQL for the Function; Playwright smoke for the portal.

## 16. Prioritized Roadmap

**P0 — before/at go-live (security-critical, days)**
1. Pin AvarnAD issuer tenant; remove social IdPs.
2. Role enforcement (`cashbag.api.access`) in APIM + API.
3. App Service access restrictions to APIM; APIM FDID check.
4. Fix Serilog minimum level (you are blind in prod right now).
5. Add `swagger-ui-react` to package.json (portal build is broken).

**P1 — correctness & reliability (1–3 weeks)**
6. Outbox/change-feed publishing — kill the dual-write.
7. Durable (or synchronous) payload archiving; fix hosted-service crash loop.
8. Stop mutating on GET; point reads; ETags; `CancellationToken`s.
9. Managed identities for all data services.
10. Health checks + availability tests + DLQ/secret-expiry alerts.
11. Singleton Service Bus sender; resilient typed HttpClient with timeout.

**P2 — BAM enablement (3–6 weeks)**
12. `userAccess` container + admin approval flow (or auto-provision at partner onboarding).
13. Claims-enrichment REST step in the B2C journey; portal acquires API access tokens.
14. Dual-scheme auth + `/api/portal/*` endpoints with server-side filtering & paging.
15. Timer-triggered status-sync Function (incremental, watermark-based); "failed request" documents.
16. BAM UI (customer + admin views) replacing the dummy dashboard.

**P3 — modernization & scale (opportunistic)**
17. .NET 10, Node 24, React 19, MSAL v4.
18. One JSON serializer; DTO cleanup; missing PUT endpoint (or remove it from the contract); tests.
19. IaC for APIM/FD/App Service; B2C policy pipeline; Entra External ID evaluation.
20. Materialized views / search index for admin cross-customer search; APIM per-partner tiers.

---

*Version facts checked July 2026: Node 24 is Active LTS (EOL 2028-04-30), Node 22 maintenance (EOL 2027-04-30) — [nodejs.org](https://nodejs.org/en/about/previous-releases), [endoflife.date](https://endoflife.date/nodejs). .NET 10 LTS supported 2025-11-11 → 2028-11-14 — [Microsoft support policy](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core).*
