# Prod Deployment Guide — living checklist

Working checklist for standing up PROD, kept up to date as we learn things doing TEST. Update this file
whenever a new manual step (SQL, Key Vault, network, AuthZ) turns out to be needed — the goal is that
nothing gets rediscovered the hard way in prod that we already hit once in TEST. See `CLAUDE.md` for the
full narrative/history; this file is the distilled, ordered checklist.

## 0. Ground rules

- **No new Azure resource *types*** beyond what TEST already uses (App Service, Function App, Cosmos,
  Key Vault, Service Bus, Blob, B2C, APIM, Front Door, App Insights) — reuse the pattern, don't invent a
  new one. See CLAUDE.md rule #9.
- PROD Azure resources (`api-nokas-bagreg-prod`, `func-nokas-api-bagreg`, subscription
  `corp-lz-prod-001`) **already exist but are not live** — created for testing, not yet the real prod
  path. Treat as scaffolding to finish, not a green field.
- Every connection string / secret comes from Key Vault. Never a literal value in app settings, appsettings.json, or this file.

## 1. On-prem SQL — run in this order

Scripts live in `SQL/`. All are idempotent / safe to re-run unless noted.

| # | Script | DB | What it does | Status in TEST (2026-08-10/11) |
|---|---|---|---|---|
| 1 | `Nattsafearkiv.sql`, `StatusTracking.sql`, `NokasExternalCustomerMapping.sql` | Nattsafe / `BizTalk_Helper` | Reference DDL only — tables already exist, don't re-run as CREATE | N/A (existing tables) |
| 2 | `api_CancelBagIfRegistered.sql` | Nattsafe | Registration DELETE's status-check/cancel proc | Already existed pre-rewrite |
| 3 | `api_CountingResultFetchChanges.sql` | Nattsafe | Read-only delta/backfill proc for counting-result sync. Drops the old `usp_CountingResult_FetchChanges` name if present. Uses an XML-based customer-number split, not `STRING_SPLIT` (this DB's compatibility level is below 130). Safe to re-run (`CREATE OR ALTER`). | **Applied and confirmed working in TEST** — first real sync run upserted 220 rows across 4 customers |
| 4 | `api_CountingResultFetchChanges_GrantExecute.sql` | Nattsafe | Grants EXECUTE on the new proc to whichever principal already has EXECUTE on `api_CancelBagIfRegistered` (i.e. the Function's real SQL login) | **Required, easy to miss** — script #3 alone is not enough; a fresh proc has no EXECUTE grant by default. Applied and confirmed working in TEST. |
| 5 | `IX_StatusTracking_dtmDate.sql` | Nattsafe | Nonclustered index on the 15M-row `StatusTracking` heap | Applied per owner; not independently re-verified by query — see verification query below |
| 6 | `NokasExternalCustomerMapping_AddBackfillFromDate.sql` | `BizTalk_Helper` | Additive nullable `BackfillFromDate` column | Not confirmed either way — not blocking (nullable, sync falls back to an 18-month default) but confirm before relying on a custom backfill window for a real prod customer |

**Verification query for #5** (no on-prem SQL access from the agent side to check this automatically):

```sql
USE [Nattsafe];
SELECT name FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.StatusTracking') AND name = 'IX_StatusTracking_dtmDate';
```

**Login/permission model — do NOT use a blanket `db_datareader` role membership and assume it's enough.**
`db_datareader` grants `SELECT` on tables/views only — it does **not** grant `EXECUTE` on stored
procedures, and this whole platform's on-prem access pattern is proc-based (`api_CancelBagIfRegistered`,
`api_CountingResultFetchChanges`), not raw table reads. The login used by `NattsafeSQLDBConnString` /
`CmsMappingConnString` needs, per DB:

- **Nattsafe**: `EXECUTE` on `dbo.api_CancelBagIfRegistered` and `dbo.api_CountingResultFetchChanges`
  specifically (not a blanket schema-level grant unless that's already the existing convention — check
  what's there today before widening it).
- **`BizTalk_Helper`**: `SELECT` on `dbo.NokasExternalCustomerMapping` (this one genuinely is a plain
  table read, so `db_datareader` — or a narrower `GRANT SELECT` on just that table — is correct here).
- **`NokasMwCacheDbV3`**: `SELECT` on `dbo.Registration` (used by `ValidateBagNumberFunction`/bag-number
  cache sync) and `EXECUTE` on `dbo.ValidateBagNumberV2` if that path is still live.

When provisioning the prod login, grant these explicitly per-object rather than reusing the skeleton
`CREATE LOGIN` + `db_datareader` script that used to sit at the top of this file — that combination would
have looked successful (login created, no error) while silently lacking the EXECUTE rights the app
actually needs, which is exactly the failure mode hit in TEST on 2026-08-11.

## 2. Azure resources — Cosmos

- `countingresults` container (PK `/clientId`, `defaultTtl` = 548 days in **seconds** = 47,347,200, composite
  indexes on `(clientId, countedAt DESC)` / `(clientId, registeredAt DESC)` / `(clientId, lastUpdatedAt DESC)` /
  `(clientId, customerRefNumber, countedAt DESC)`, excluded paths `/statusHistory/*`, `/denominations/*`,
  `/amounts/*`) — see `deploy/provision-countingresult-infrastructure.ps1` for the exact indexing-policy
  JSON and the `az cosmosdb sql container create` invocation (parenthesized JSON has to go through a temp
  file, not inline, or `az.cmd`'s cmd.exe re-parsing mangles it).
- Grant the prod API's and Function's managed identities **Cosmos DB Built-in Data Contributor**
  (role id `00000000-0000-0000-0000-000000000002`) at account scope — same as TEST.
- `bagreg-v2` container's unique-key policy must be **camelCase** (`/bagRegistration/bagNumber`), matching
  the STJ camelCase serializer — this bit TEST once already (PascalCase path silently made every write
  collide on an "undefined" unique-key value). Verify the policy on the prod container before first write,
  don't assume the provisioning script's literal is still correct if it's ever hand-edited.

## 3. Key Vault

Vault uses the **legacy Access Policy model** (`enableRbacAuthorization: false`), not RBAC — an
`az role assignment create` for "Key Vault Secrets User" silently does nothing here. Use
`az keyvault set-policy --secret-permissions get list` for the app's managed identity instead. Access
policies are vault-wide (get/list on all secrets), not per-secret — granting the policy once covers every
secret added later, including new ones like `CmsMappingConnString`.

Secrets needed (prod vault, mirror of `kv-dev-nokasapi-cashmgmt`):

- `CosmosDb--KeyValue` (only if any component still uses account-key auth — prefer Managed Identity everywhere new)
- `BlobStorage--ConnectionString`
- `ServiceBus--ConnectionString`
- `NattsafeSQLDBConnString`
- `NokasMwCacheDBConnString`
- `CmsMappingConnString` — **new for counting-result**, points at `BizTalk_Helper`, not `Integrations.SN.CMS`
- `BagStatusFunction--FunctionKey`

## 4. App settings — Function App

Beyond the connection-string secrets above (all as `@Microsoft.KeyVault(SecretUri=...)` references, each
wrapped in escaped double quotes if set via `az` from PowerShell/cmd.exe — see the two `provision-v2-*.ps1`
scripts for the exact quoting):

```
CosmosDb__CountingResultContainerName=countingresults
CountingResults__Sync__IntervalMinutes=30
CountingResults__Sync__OverlapMinutes=10
CountingResults__Sync__PageSize=500
CountingResults__Backfill__DefaultMonths=18
```

## 5. App settings — API App

```
CosmosDb__CountingResultContainerName=countingresults
CountingResults__Query__MaxSpanDays=92
CountingResults__Query__MaxPageSize=500
CountingResults__Query__DefaultPageSize=100
CountingResults__Cosmos__TtlDays=548
```

## 6. AuthZ (Entra / B2C)

- `cashbag.registration.access` (renamed from `cashbag.api.access`) and `cashbag.countingresult.read` App
  Roles must exist on the resource app (`Nokas.CashBagManagement.WebAPI`) in the **B2C tenant**
  (`nokasb2ctest.onmicrosoft.com` for TEST — confirm the prod tenant/app equivalent before repeating this
  step, don't assume it's the same app registration).
- Must be `allowedMemberTypes: ["Application"]` App Roles, not delegated "Expose an API" scopes — the
  latter produce no `roles` claim for client-credentials tokens at all. See CLAUDE.md's "Critical
  Entra/B2C gotcha" note under Conventions.
- Each real partner needs the role granted as an **Application permission** + **admin-consented** —
  this is a coordinated, breaking change for `cashbag.registration.access` (rename of the live role), not
  a silent deploy. `cashbag.countingresult.read` is purely additive (new role, no existing partner had
  anything to lose), safe to roll out partner-by-partner as they onboard to the new endpoints.
- The agent/automation running this deployment will very likely **not** have Azure CLI access to the B2C
  tenant by default — it's a separate tenant from the subscription's home tenant. Confirmed blocked this
  way in TEST on 2026-08-10/11 (`az ad app list` against the corp tenant returns nothing; the B2C tenant
  needs its own `az login --tenant <b2c-tenant-id>`, which needs a human to complete interactively). Budget
  for a human to do the manifest edit + consent, same as happened in TEST.

## 7. Network / access restrictions

- Function App main site: allow only the on-prem-reachable VNet subnet (`snet-test-openAPI` in TEST — confirm the prod equivalent subnet name), default deny. SCM/Kudu unrestricted (zip-deploy needs it).
- API App: allow APIM's outbound IP + `AzureFrontDoor.Backend` service tag, default deny. Regional VNet
  Integration (outbound) into the same on-prem-reachable subnet if the API calls the Function directly for
  anything.
- **Temporary dev-access rules are genuinely temporary** — TEST accumulated an `allow-dev-temp` rule on
  the API (from initial dev testing) and, on 2026-08-11, a second one added to the *Function* app plus a
  `https://portal.azure.com` CORS origin, both added specifically to let the owner use the Azure Portal's
  "Test/Run" button to manually fire the `CountingResultSync` timer function (Portal Test/Run needs direct
  network access + CORS, same as any other caller — it does not get a bypass). **Remove both once no
  longer needed** — they are not meant to be permanent, exactly like the existing `allow-dev-temp` note on
  the API already says.

## 8. Verification after deploy

1. `curl -D - https://<api-host>/healthz` — confirm `X-Build-Stamp` matches the commit you just deployed.
2. Full registration E2E: POST → GET → DELETE → duplicate POST (409) → cancel-again DELETE (409) → GET
   not-found (404) — with a real partner token. Confirm each shows up correctly in App Insights
   (`requests` table, matching `resultCode`s) with **zero** unexpected `exceptions`/severity≥3 `traces`
   beyond the outcomes you intentionally triggered.
3. Counting-result: confirm the App Role exists and is granted before testing (a fresh token is required
   after any role grant — roles are stamped in at token issuance, not re-checked live). Then run the query
   endpoint with a real date range and confirm `dataAsOf` is populated (proves the sync has actually run
   at least once) rather than just checking for a 200 with an empty list.
4. Check `CountingResultSyncRun` / `CountingResultQuery` custom events are landing in App Insights with
   the expected `cloud_RoleName`.
5. Confirm the sync's `sync-state` document in Cosmos has a recent `lastSuccessfulRunUtc` after the first
   scheduled tick — if it's missing or stale, check Function logs for the exact SQL/permission error before
   assuming it's a code bug (permission-denied and missing-connection-string are the two failure modes
   actually hit in TEST, both silent from the outside except via `LogCritical` traces).

## 9. Things that bit us in TEST — don't repeat in prod

- Cosmos unique-key policy path casing must match the STJ camelCase serializer exactly (PascalCase silently breaks duplicate detection for *every* write, not just conflicting ones).
- B2C app-only tokens use the v1-style issuer (`https://sts.windows.net/{tenantId}/`) even via the v2.0 authority endpoint — `ValidIssuer` must be set explicitly, OIDC discovery alone gets it wrong.
- Key Vault access-policy vaults ignore RBAC role assignments silently — no error, just "denied access" at runtime.
- `az functionapp create --runtime-version` wants `10`, not `10.0`; cross-RG `--plan` needs the full resource ID.
- Git Bash mangles `/subscriptions/...`-style arguments into Windows paths — prefix `MSYS_NO_PATHCONV=1`.
- Isolated-worker Functions need an explicit restart to re-index newly deployed functions in some cases — `az functionapp function list` can lag behind an actual successful deploy until then.
- `db_datareader` role membership does not grant `EXECUTE` on stored procedures — grant explicitly per proc (see §1).
- `STRING_SPLIT` needs database compatibility level ≥ 130 (SQL Server 2016) — this environment's `Nattsafe` DB is below that, so any new proc needing to split a delimited string must use the older XML-based split trick instead (see `api_CountingResultFetchChanges.sql`), not `STRING_SPLIT`. Check compatibility level before writing new procs that use post-2016 T-SQL features.
- A DB a design doc names today (`Integrations.SN.CMS`) can move before you ever query it (`BizTalk_Helper`, discovered 2026-08-10/11) — don't hardcode DB names in more than one place if you can help it, and re-verify against whoever owns the on-prem side before a prod run, not just against an older doc.
