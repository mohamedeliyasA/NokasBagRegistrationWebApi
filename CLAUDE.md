# CLAUDE.md — NOKAS Cash Management Integration Platform

Guidance for Claude Code when working in this solution. Read this fully before making changes.

## What this system is

A secure integration platform for registering physical cash bags. External partners register bags via a REST API before pickup; NOKAS processes them through on-premises systems; customers and NOKAS employees track order status through the Integration Portal (Business Activity Monitoring, "BAM").

**Domain vocabulary:** *bag* = physical cash bag identified by `bagNumber`; *registration/order* = the API record of a bag; *counting result* = declared vs counted amount + deviation after processing; *client ID* = the partner's OAuth app registration ID — it is the tenant key for all data; *Nattsafe* = on-prem legacy system of record for bag processing; *BagLifecycleId* = GUID assigned at creation, tracks the bag through its life; *CorrelationId* = per-request ID (`X-Correlation-ID`), distinct from BagLifecycleId.

## Repository layout

```
Nokas.CashMgmt.BagRegistration.API/    ASP.NET Core Web API (net8.0)
  Configuration/    AuthConfig (JWT), SwaggerConfig (+ Front Door restrictions), options classes
  Controllers/      BagRegistrationController (POST/GET/DELETE), BagCountingResultController (GET)
  Middleware/       ExceptionLogging → CorrelationId → RequestArchiving (registered in this order)
  Models/           Request/response DTOs + Cosmos document model (BagRegistrationRequest)
  Repository/       CosmosBagRegistrationRepo (bagreg + clientdirectory containers)
  Services/         BlobArchiveService, ServiceBusSender, FunctionInvoker, payload queue + hosted service
  Profile/          AutoMapper profile (BagProfile)

Nokas.CashMgmt.BagReg.Func/            Azure Functions isolated worker (net8.0), VNet-integrated
  GetBagStatusFunction.cs              GET api/status/{bagNumber}        → dbo.api_CancelBagIfRegistered (Nattsafe DB)
  GetBagCountingResultFunction.cs      GET api/countingresult/{bagNumber} → SELECT from Nattsafearkiv
  ValidateBagNumberFunction.cs         GET api/validate/{bagNumber}      → dbo.Registration (NokasMwCacheDB)
  BagStatusService.cs                  Dapper + Microsoft.Data.SqlClient; conn strings from env vars

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
```

## Architecture (request flows)

**Partner (M2M):** partner → token from `login.microsoftonline.com/{B2C tenant}` (client credentials, audience `https://nokasb2ctest.onmicrosoft.com/api/nokas`) → `api-test.nokas.com` → Front Door → APIM (JWT validation, 10 req/60s per app, backend rewrite) → Web API.

**POST bag-registration:** validate model (conditional: NOK requires `totalOrderAmount`, no `bagItemsFC`; Valuta requires `bagItemsFC`, no `bagItemsLC`) → call Function `validate/{bagNumber}` (duplicate check against on-prem cache DB) → map to document (`id = bagNumber`, `clientId` from token `azp`/`appid`, status `In-Progress`… response says `Registered`) → publish `bagregistration` message to Service Bus topic → create in Cosmos (unique index on bag number backs up the duplicate check). Raw body archived to Blob via middleware → in-memory channel → hosted service.

**GET / DELETE / countingresult:** always scoped to the caller's `clientId` (Cosmos partition key). DELETE consults the Function `status/{bagNumber}` first; a bag is cancellable only when `intStatus ∈ {0,1,2}` and `dtmTransportFetch` is null; cancellation publishes `deleteBagRegistration`. Status-code map (keep API and Function copies in sync — they have already drifted): 0/1=registered, 2=in-progress, 3=picked-up, 4=received by bank, 5=counted, 10=canceled, 11=not approved, 12=approved by bank, 13=missing, -1/8/9=error; transport-fetched 0/1/2 ⇒ picked-up.

**Portal (users):** MSAL redirect → B2C custom policy `B2C_1A_NOKASINTEGRATIONPORTAL_SIGNUP_SIGNIN` → local account, AvarnAD (Entra ID), Google, or Facebook → ID token with self-asserted claims `api_companyName`, `api_clientId`, `api_contactPerson`, `api_contactPersonEmail` (B2C extension attributes, app id `36ba39fd…`). SPA currently requests only `openid profile` — no API access token yet.

## Azure services

| Service | Use | Auth today | Config keys |
|---|---|---|---|
| Cosmos DB | `bagreg` container (PK `/clientId`, `id` = bagNumber, unique index on bag number) + `clientdirectory` | Account key | `CosmosDb:*` |
| Service Bus | Topic (name in config), subjects `bagregistration` / `deleteBagRegistration`; consumed downstream (integration layer → on-prem) | Connection string | `ServiceBus:*` |
| Blob Storage | Raw payload archive (`bag_create_*.json`) | Connection string | `BlobStorage:*` |
| Key Vault | Fills the empty appsettings values at startup | DefaultAzureCredential | `KeyVault:Uri` |
| App Insights | Serilog sink + telemetry; B2C journeys also log here | Connection string | `Serilog`, `ApplicationInsights` |
| Azure Function | On-prem SQL bridge (read-only), called by API via `FunctionInvoker` | Function key (`?code=`) | `BagStatusFunction:BaseUrl`, `BagStatusFunction:FunctionKey` |
| Function env vars | `NattsafeSQLDBConnString` (Nattsafe), `NokasMwCacheDBConnString` (portal cache) | SQL auth | Function App settings |
| APIM + Front Door | Ingress, JWT pre-check, rate limiting; Swagger only via Front Door (`X-Azure-FDID` check, Production only) | — | `FrontDoor:AllowedIds` |

## Conventions

- **Every log line and response carries a correlation ID.** Middleware reads/creates `X-Correlation-ID`, stores it in `HttpContext.Items["CorrelationId"]`, echoes it in responses; `FunctionInvoker` forwards it as `x-correlation-id`. Preserve this in any new code path.
- **All data access is scoped by `clientId` from the validated token** (`ClaimsHelper.GetClientId` → `azp`/`appid`). Never accept a client ID from a request body or query string.
- **Every state change appends an `OperationHistoryEntry`** (`Action`, `Timestamp`, `PerformedBy`, correlation IDs).
- Serialization is currently mixed (Newtonsoft for MVC + Cosmos, System.Text.Json for Service Bus/Function calls, XML formatters enabled). When touching models, check which serializer actually processes the property — `[JsonPropertyName]` is ignored by both Newtonsoft and the Cosmos SDK's default serializer.
- Errors return the custom `ErrorResponse` shape (title/status/correlationId/code) from `ExceptionLoggingMiddleware`; `DuplicateBagNumberException` → 409 `ERR_DUPLICATE_BAG`. Note: `[ApiController]` auto-400s validation failures before controller code runs.
- Config pattern: appsettings.json keys exist but are empty; real values come from Key Vault (names match config paths) or App Service settings. Never commit real values.
- API responses support JSON and XML (`Produces` + `AddXmlSerializerFormatters`); DTOs carry both JSON and XML attributes.

## Development workflow

- **API:** `dotnet run` in the API project; Swagger at `/swagger`. Needs Key Vault access (DefaultAzureCredential — `az login`) or local user-secrets overriding `CosmosDb/ServiceBus/BlobStorage/AzureAd/BagStatusFunction` sections. Use the Cosmos emulator locally if possible.
- **Function:** `func start` / F5 (port 7019). Needs `local.settings.json` with the two SQL connection strings; requires VPN/network path to on-prem SQL to be useful.
- **Portal:** Node ≥ 22 (moving to 24 LTS), `npm install && npm run dev` — dev server expects HTTPS certs at `cert/key.pem`/`cert/cert.pem` (mkcert). Redirect URI is `window.location.origin`, so B2C app registration must list the localhost origin. Known issue: `swagger-ui-react` is imported but missing from package.json.
- **B2C policies:** upload order matters — Base → Localization → Extensions → RP files. Tenant `nokasb2ctest.onmicrosoft.com` (TEST). Policy IDs are referenced by exact name in `authConfig.ts`.
- **Testing the API as a partner:** client-credentials token per the OAuth guide (`scope=https://nokasb2ctest.onmicrosoft.com/api/nokas/.default`), Postman collection in `wwwroot/docs/`.
- No automated tests exist yet. New non-trivial logic should come with xUnit tests (target: `WebApplicationFactory` integration tests + Cosmos emulator).

## Deployment

- API → Azure App Service `api-nokas-bagreg-…` (Norway East), Web Deploy profile in Properties. Exposed only via `api-test.nokas.com` → Front Door → APIM (policy in `apimpolicy.xml`).
- Function → `func-test-nokas-api-bagreg` / `func-prod-nokas-api-bagreg`, zip deploy; VNet-integrated for on-prem SQL.
- Portal → static build (`npm run build` → `dist/`), hosted on IIS/App Service (`public/web.config` present).
- Environments: TEST (`nokasb2ctest`, api-test.nokas.com) and PROD (separate tenant/apps). Swagger restrictions and FDID checks activate only in `Production`.

## Planned work: BAM (Business Activity Monitoring)

The agreed direction (see `NOKAS_Platform_Review.md` §10–11 for full rationale):

1. **Never trust the self-asserted `api_clientId` claim.** Access is granted via a server-side `userAccess` mapping in Cosmos (`userObjectId` → approved `clientIds` + `role`), created/approved by NOKAS admins.
2. B2C sign-in journey gets a REST claims-enrichment step returning `role` + verified `clientIds` (claim types `role`/`uniquecustomerid` are already scaffolded in the Extensions policy).
3. The existing API is extended with `/api/portal/*` endpoints using a **second JWT bearer scheme** (B2C policy issuer) alongside the existing partner scheme; policies: `PortalCustomer`, `PortalAdmin` (admins = AvarnAD users, with the issuer pinned to Avarn's tenant). Filtering happens server-side from token claims; admins get cross-partition queries with continuation-token paging.
4. A timer-triggered Function (~2 h) syncs status from on-prem SQL into Cosmos (incremental watermark). The portal reads Cosmos only — never on-prem synchronously.
5. The SPA acquires an access token for the API scope (replace the `openid profile`-only login), and the BAM page replaces the dummy dashboard.

## Known pitfalls & pending fixes (do not regress; fix opportunistically)

- Serilog `MinimumLevel.Default` is `Error` in appsettings — Information/Warning logs are dropped. Intended fix: `Information` for app namespaces.
- Security hardening pending: role check (`cashbag.api.access`) in APIM + API; APIM `check-header` for `X-Azure-FDID`; App Service access restriction to APIM; `ValidTokenIssuerPrefixes` in the AvarnAD technical profile must be pinned to Avarn's tenant (currently accepts any Entra tenant); social IdPs slated for removal.
- Dual-write: Service Bus publish happens before the Cosmos write on POST — moving to outbox/change-feed publishing.
- Payload archive queue is in-memory (lost on restart) and the hosted service dies on first upload exception.
- GET endpoints upsert the document (history on read) — being removed; use point reads (`ReadItemAsync`) instead of queries.
- `EnsureClientInDirectoryAsync` and the `clientdirectory` single-document design are dead/legacy — don't build on them.
- B2C Extensions/Base: `api_companyName`'s `DefaultPartnerClaimTypes` points at the `api_clientId` extension attribute (copy-paste bug).
- No PUT endpoint exists although middleware archives PUTs and docs mention update.
- Portal: MSAL v2 (EOL), hard-coded client IDs (move to `VITE_*` envs), `*-MohamedEliyas.*` duplicate files to be deleted, `@types/axios`/`@types/react-router-dom` mismatched types.
- Target upgrades: .NET 10 LTS (both projects are net8.0), Node 24 LTS, React 19, MSAL v4.

## Rules for future changes

1. Any new endpoint must: require an authorization policy (not bare `[Authorize]`), scope data by validated identity, propagate the correlation ID, append operation history for mutations, and declare `[ProducesResponseType]` contracts.
2. Never put secrets, connection strings, instrumentation keys, or tenant-specific IDs in committed files (including B2C XML) — Key Vault / pipeline substitution only.
3. Anything reaching on-prem must be async or resilient (timeout ≤ 10 s, retry, fail-open where business rules allow) — on-prem availability must not break cloud writes.
4. Prefer Managed Identity over keys/connection strings for any new Azure resource client.
5. Keep the status-code map in exactly one shared place if you touch it.
6. When changing DTOs, verify both JSON and XML serialization and regenerate the Postman collection + Swagger docs partners rely on.
