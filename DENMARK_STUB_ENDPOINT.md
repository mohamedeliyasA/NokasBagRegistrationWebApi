# Denmark Bag Registration — TEST STUB

A stand-in endpoint so the Danish partner can build and test their integration before the
ServiceNow backend exists. It validates the payload against the agreed Danish contract,
stores it for inspection, and returns a fixed acknowledgement. **It registers nothing in any
backend system.**

Built 2026-08-18. Strict contract validation added 2026-08-19. Delete or replace it when the
real ServiceNow integration ships.

---

## Part 1 — What to send the customer

### Endpoint

```
POST https://api-test.nokas.com/api/cash-mgmt/dk/bag-registration
```

Also reachable directly on the APIM gateway if preferred:
`https://apim-avarn-dev-test.azure-api.net/api/cash-mgmt/dk/bag-registration`

### Getting a token

Standard OAuth 2.0 client credentials, same token service the Norwegian API uses.

```bash
curl -X POST "https://api-test.nokas.com/oauth2/v2.0/token" \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "grant_type=client_credentials" \
  -d "client_id=<their client id>" \
  -d "client_secret=<their client secret>" \
  -d "scope=https://nokasb2ctest.onmicrosoft.com/api/nokas/.default"
```

### Request

`Content-Type: application/json` is required. Any other content type is rejected with `415`.

```json
{
  "bagRegistration": {
    "customerRefNumber": "5790000123456",
    "cashRegisterId": "POS-01",
    "messageId": "0f8fad5b-d9cb-469f-a165-70867728950e",
    "bagNumber": "3164696800",
    "registrationDate": "2026-08-17",
    "totalOrderAmount": "888.50",
    "bagItemsLC": {
      "DKK_500": "1",
      "DKK_200": "1",
      "DKK_100": "1",
      "DKK_50": "1",
      "DKK_20": "1",
      "DKK_10": "1",
      "DKK_5": "1",
      "DKK_2": "1",
      "DKK_1": "1",
      "DKK_05": "1"
    }
  }
}
```

### Field rules

Every field is **mandatory except `bagItemsLC`**, and every value is a **JSON string** —
including the numeric ones. Sending `888.50` unquoted is rejected.

| Field | Rule |
|---|---|
| `customerRefNumber` | Branch id. Non-empty, max 50 characters. |
| `cashRegisterId` | Cash register id. Non-empty, max 50 characters. |
| `messageId` | Must be a GUID. |
| `bagNumber` | Digits only, 1–20 characters. |
| `registrationDate` | Exactly `yyyy-MM-dd`, and a real calendar date. |
| `totalOrderAmount` | Positive decimal in DKK, `.` as the decimal separator, max 2 decimal places. |
| `bagItemsLC` | **Optional.** Object of denomination → quantity. If sent it must be well formed; an empty object or all-zero quantities are accepted. |

**Unknown fields are rejected** — anywhere in the payload, at either level.

> `bagItemsLC` is **optional**. A partner may declare only `totalOrderAmount` and omit the
> denomination breakdown entirely, or send it as `null`. Optional does not mean unchecked: a
> breakdown that *is* sent must use valid denomination keys and whole-number quantities. When it is
> omitted there is nothing to cross-check the declared total against, so no mismatch warning is
> produced.

#### Denominations

Exactly these keys, spelled exactly this way (case-sensitive):

`DKK_500`, `DKK_200`, `DKK_100`, `DKK_50`, `DKK_20`, `DKK_10`, `DKK_5`, `DKK_2`, `DKK_1`,
`DKK_05`

`DKK_05` is the 50-øre coin. There is no `DKK_1000` — the 1000-krone note is being withdrawn
from circulation and is not part of the contract.

> **Each value is a *quantity* — how many notes or coins of that denomination — not an
> amount.** `"DKK_500": "3"` means three 500-krone notes, i.e. 1500.00 DKK.
>
> Quantities of `"0"` are allowed, so a POS system that emits the full denomination grid does
> not have to strip empty rows.

### Response — `201 Created`

**This is the real response contract, not a stub-shaped one.** It matches the Norwegian bag
registration response field for field (plus `messageId`, which Denmark makes mandatory in the
request). The only thing that marks this deployment as a stand-in is the wording of
`description`, which shouts in capitals.

```json
{
  "customerRefNumber": "5790000123456",
  "bagNumber": "3164696800",
  "messageId": "0f8fad5b-d9cb-469f-a165-70867728950e",
  "description": "MOCK RESPONSE - NO BAG HAS ACTUALLY BEEN REGISTERED. THE DENMARK BACKEND INTEGRATION IS NOT LIVE YET; THIS PAYLOAD WAS VALIDATED AND STORED FOR TESTING ONLY.",
  "registrationStatus": "Registered",
  "bagLifecycleId": "45ffddba-0a87-4f82-9ae1-a01a7fac3432",
  "requestCorrelationId": "bc414e2a-ec7d-4c8b-8dac-a941098e6ff9"
}
```

| Field | Type | Meaning |
|---|---|---|
| `customerRefNumber` | string | Echoed from the request. |
| `bagNumber` | string | Echoed from the request. |
| `messageId` | GUID | Echoed from the request, so a response can be matched to the message sent. |
| `description` | string | Human-readable outcome. **Carries the MOCK warning today; becomes `Bag Registered Successfully` when the backend goes live.** |
| `registrationStatus` | string | `Registered`. |
| `bagLifecycleId` | GUID | NOKAS identifier for this registration. Quote it to support. |
| `requestCorrelationId` | string | The `X-Correlation-ID` sent, or one generated. Log it. |
| `warnings` | string[] | Present only when something looks questionable. See below. |

A `Location` header points at the bag's own URL, e.g.
`/api/cash-mgmt/dk/bag-registration/3164696800`.

Responses also carry `X-Mock-Endpoint: true`. That header is operational metadata rather than
part of the contract, and it disappears with the stub — do not branch on it.

> **The only change at go-live is the `description` text.** No field is added, removed or
> renamed. Code written against this response today keeps working unchanged.

#### `warnings`

If `totalOrderAmount` does not equal the sum of the denominations, the request is still
accepted but the response gains a `warnings` array saying so. This is the quantity-versus-
amount mistake, caught early rather than silently:

```json
"warnings": [
  "totalOrderAmount (900.00) does not equal the sum of bagItemsLC (500). Each bagItemsLC value is a quantity of that denomination, not an amount."
]
```

### Response — `400 Bad Request`

All rule violations are reported at once, not one at a time.

```json
{
  "title": "The payload does not match the Danish bag registration contract.",
  "status": 400,
  "correlationId": "…",
  "code": "VALIDATION_ERROR",
  "field": "bagRegistration.messageId",
  "exception": "bagRegistration.messageId: Must be a GUID. | bagRegistration.bagNumber: Must contain digits only."
}
```

`field` names the first failing path; `exception` lists every failure as
`<json path>: <message>`.

### Retrieving a registration

```
GET https://api-test.nokas.com/api/cash-mgmt/dk/bag-registration/{bagNumber}
```

Keyed on **bag number**, exactly like the Norwegian API. Returns the same response body as the
POST, with `description` reading `MOCK RESPONSE - RETURNED FROM TEST STORAGE ONLY …`.

Scoped to the caller's own client — a bag registered by another partner returns `404`, never
their data. Unknown bag numbers return `404` with code `ERR_BAG_NOT_FOUND`, matching Norway.

> **Bag numbers are not unique.** Duplicate checking belongs to the backend, not to this API, so
> the same bag number can be posted repeatedly. `GET` returns the **most recent** submission for
> that bag number. Rejected payloads are never returned, since they have no valid bag number.

### Behaviour worth telling them up front

| Aspect | Behaviour |
|---|---|
| Duplicate bag numbers | Not checked. The same bag number can be posted repeatedly. |
| Content type | `application/json` only. Anything else is `415`. |
| Response format | JSON by default; `Accept: application/xml` returns XML. |
| Rate limit | 100 requests / 60 seconds per client. |
| Retention | Stored payloads expire after 90 days. |
| Errors | `400` contract violation · `401` no/invalid token · `403` token lacks the Danish role · `415` wrong content type · `404` unknown bag number |

---

## Part 2 — Internal notes

### Still needed before the customer can call it

**The Entra app registration and app role are not created yet** — this is the only thing
blocking the customer. In the B2C tenant `nokasb2ctest.onmicrosoft.com`:

1. Add app role `cashbag.dk.registration.access` to the `Nokas.CashBagManagement.WebAPI`
   manifest, `allowedMemberTypes: ["Application"]`. Additive — leave the three existing roles
   (`cashbag.api.access`, `cashbag.registration.access`, `cashbag.countingresult.read`) alone.
2. Create the customer's app registration and a client secret.
3. Grant the new role to it as an **Application** permission and admin-consent it.

A delegated scope under "Expose an API" does nothing for client-credentials tokens — it must
be an App Role, or no `roles` claim is issued.

The API reads the required role from the `AzureAd__RequiredRole` app setting, already set to
`cashbag.dk.registration.access`. Until the role exists and is granted, a valid token gets
`403` — correct, verified behaviour, not a fault.

### Assumptions baked into validation — confirm these

These were not in the payload spec and were chosen to make "reject if it does not adhere"
enforceable. Each is a one-line change if wrong.

| Assumption | Where |
|---|---|
| `bagNumber` is digits only, 1–20 characters | `DkBagRegistrationValidator.ValidateBagNumber` |
| `customerRefNumber` / `cashRegisterId` max 50 characters | validator constants |
| `totalOrderAmount` is in **DKK with decimals**, not øre | inferred from `888.5` = one of each denomination |
| No `DKK_1000` denomination | `DkDenominations` |
| `bagItemsLC` optional; empty object and all-zero grids accepted | `TryValidateBagItems` |
| Denomination total mismatch is a **warning**, not an error | `Stub:EnforceDenominationTotal`, default `false` |

Flipping `Stub__EnforceDenominationTotal=true` as an app setting turns the mismatch into a
`400` with no redeploy.

### Swagger, and the Front Door route it needs

Denmark's Swagger UI is served at **`/dk/swagger`**, not `/swagger`. `/swagger/*` on
`api-test.nokas.com` is an existing Front Door route pointing at the Norwegian v2 app, and picking a
path the DK app serves natively avoids needing a Front Door URL-rewrite rule.

**Norway needs no change at all** — its `/swagger/*` route, its `og-bagreg-swagger` origin group,
and the shared `/api/cash-mgmt/*` route are all untouched. The two path patterns do not overlap.

> ⚠️ While doing this, do **not** edit the existing `api-bagreg-swagger` route or the
> `og-bagreg-swagger` origin group, and do not add `/dk/swagger/*` to an existing route. Denmark
> gets its own origin group, its own origin, and its own route.

#### Option A — Azure Portal (manual)

Everything is on the existing Front Door profile **`afd-test-dev`** in resource group
**`rg-common-test-dev`**.

**Step 1 — Create the origin group**

1. Azure Portal → search **Front Door and CDN profiles** → open **`afd-test-dev`**.
2. Left menu → **Settings → Origin groups** → **+ Add**.
3. Fill in:

   | Field | Value |
   |---|---|
   | Name | `og-bagreg-dk-swagger` |
   | Health probes | **Enable health probes** ✔ |
   | Probe path | `/healthz` |
   | Probe protocol | `HTTPS` |
   | Probe method | `GET` |
   | Probe interval (seconds) | `120` |
   | Sample size | `4` |
   | Successful samples required | `3` |
   | Latency sensitivity (ms) | `50` |

4. **Add**.

`/healthz` is deliberately the probe path — it is the one anonymous endpoint on the DK app, so the
probe is not affected by the token or app-role checks.

**Step 2 — Add the origin**

1. Open the new **`og-bagreg-dk-swagger`** origin group.
2. **+ Add an origin**.
3. Fill in:

   | Field | Value |
   |---|---|
   | Name | `SwaggerDk` |
   | Origin type | **App services** |
   | Host name | `api-nokas-bagreg-dk.azurewebsites.net` |
   | Origin host header | `api-nokas-bagreg-dk.azurewebsites.net` |
   | HTTP port | `80` |
   | HTTPS port | `443` |
   | Priority | `1` |
   | Weight | `1000` |
   | Certificate subject name validation | leave ticked (default) |
   | Status | **Enable this origin** ✔ |

4. **Add**.

The origin host header must stay the `azurewebsites.net` name — App Service routes on the Host
header, and overriding it with `api-test.nokas.com` would break the binding.

**Step 3 — Create the route**

1. Left menu → **Settings → Front Door manager** (called **Endpoint manager** in some views).
2. Expand the endpoint **`avarnsecurity-global`** → under **Routes**, click **+ Add a route**.
3. Fill in:

   | Field | Value |
   |---|---|
   | Name | `dk-swagger` |
   | Domains | select **`api-test.nokas.com`** only |
   | Patterns to match | **`/dk/swagger/*`** — see the warning below |
   | Accepted protocols | `HTTP and HTTPS` |
   | Redirect | **Redirect all traffic to use HTTPS** ✔ |
   | Origin group | `og-bagreg-dk-swagger` |
   | Origin path | *leave blank* |
   | Forwarding protocol | **HTTPS only** |
   | Caching | **leave disabled** |

4. **Add**.

> 🔴 **The "Patterns to match" box is pre-filled with `/*`. You must replace it.** Leaving the
> default creates a catch-all route that swallows every path on `api-test.nokas.com` that no more
> specific route claims, and points it at the Danish stub. The existing routes
> (`/api/cash-mgmt/*`, `/swagger/*`, `/oauth2/v2.0/*` …) still win by longest-prefix match, so
> Norway keeps working and the damage is not obvious — which is exactly what makes it worth
> checking. This was the mistake made the first time round.

Two more things to get right: leave the default `*.azurefd.net` domain **unticked** so this route
only serves the real domain, and leave **Origin path blank** — the app already serves `/dk/swagger`,
so anything here would double the prefix.

Caching is left off on purpose: a cached OpenAPI document goes stale silently after a deploy, and
the payoff on a low-traffic docs page is nil.

**Step 4 — Wait, then verify**

> ⏱️ **Expect a 404 for the first several minutes — this is normal.** Front Door Standard takes
> roughly 5–15 minutes to push a new route to the edge, and each subsequent edit restarts that
> clock. When this route was first created it 404'd for about 10 minutes before serving. Do not
> start changing settings during that window; you will only reset the timer.

Once it settles:

| Check | Expected |
|---|---|
| `https://api-test.nokas.com/dk/swagger/index.html` | Swagger UI renders |
| `https://api-test.nokas.com/dk/swagger/v1/swagger.json` | OpenAPI JSON, titled "… Denmark (TEST STUB)" |
| `https://api-test.nokas.com/swagger/index.html` | **still Norway** — unchanged |
| `https://api-nokas-bagreg-dk.azurewebsites.net/dk/swagger/index.html` | `403` |

The last one is expected and is the point: the App Service only accepts APIM and Front Door traffic,
and the app additionally gates `/dk/swagger` on the `X-Azure-FDID` header — because the
`AzureFrontDoor.Backend` service tag allows *every* Front Door instance, not just ours.

**If it is still 404 after ~15 minutes**, work out *which* component is answering before changing
anything. Two checks settle it in seconds:

1. **Look for the `X-Build-Stamp` response header.**

   ```bash
   curl -sD- -o /dev/null https://api-test.nokas.com/dk/swagger/index.html | grep -i x-build-stamp
   ```

   Our app stamps **every** response, including its own 404s. Header present → the request reached
   the app and the problem is in the app. Header absent → Front Door never forwarded, and the
   problem is the route.

2. **Compare the 404 body against a path that is definitely unrouted.**

   ```bash
   curl -s https://api-test.nokas.com/dk/swagger/index.html | md5sum
   curl -s https://api-test.nokas.com/zzz-does-not-exist/x   | md5sum
   ```

   Identical hashes mean Front Door is serving its own "no route matched" page — the route is not
   live at the edge, and no amount of app-side debugging will help.

Useful supporting facts while diagnosing:

- Health probes reach the origin independently of routes, so `GET /healthz` traffic appearing in App
  Insights for `cloud_RoleName == "api-nokas-bagreg-dk"` proves the origin is reachable and healthy
  even while the route is dead.
- An unhealthy origin group produces `503`, not `404`. A `404` is always a routing answer.
- `X-Cache: CONFIG_NOCACHE` on the response confirms caching is off, so a stale cached 404 is not
  the explanation.

#### Option B — Azure CLI

The earlier CLI snippet was written in bash style. In PowerShell, `RG=rg-common-test-dev` does not
assign anything, so `-g $RG` expanded to `-g` with no value — hence
`argument --resource-group/-g: expected one argument`. The flags themselves are fine on az 2.83.
Values are inlined below so there is nothing to set first:

```powershell
az afd origin-group create -g rg-common-test-dev --profile-name afd-test-dev `
  --origin-group-name og-bagreg-dk-swagger `
  --probe-request-type GET --probe-protocol Https --probe-interval-in-seconds 120 `
  --probe-path /healthz --sample-size 4 --successful-samples-required 3 `
  --additional-latency-in-milliseconds 50

az afd origin create -g rg-common-test-dev --profile-name afd-test-dev `
  --origin-group-name og-bagreg-dk-swagger --origin-name SwaggerDk `
  --host-name api-nokas-bagreg-dk.azurewebsites.net `
  --origin-host-header api-nokas-bagreg-dk.azurewebsites.net `
  --http-port 80 --https-port 443 --priority 1 --weight 1000 --enabled-state Enabled

az afd route create -g rg-common-test-dev --profile-name afd-test-dev `
  --endpoint-name avarnsecurity-global --route-name dk-swagger `
  --origin-group og-bagreg-dk-swagger --patterns-to-match "/dk/swagger/*" `
  --supported-protocols Https --https-redirect Enabled --forwarding-protocol HttpsOnly `
  --custom-domains api-test-nokas-com-aa48 --link-to-default-domain Disabled
```

`api-test-nokas-com-aa48` is the **existing** custom-domain resource on this profile — reuse it, do
not create another.

#### Step 5 — Switch it on in the portal UI

Once the route resolves, set Denmark's two Swagger URLs in
`NokasIntegrationPortal/src/config/markets.ts`:

```ts
swaggerUi:   "https://api-test.nokas.com/dk/swagger/index.html",
swaggerJson: "https://api-test.nokas.com/dk/swagger/v1/swagger.json",
```

✅ **Already done as of 2026-08-19** — the route is live and both URLs are set in `markets.ts`.
The values are recorded here because they are the switch to flip if the market is ever rebuilt, and
because setting them back to `null` is the correct way to hide a Swagger reference that is
temporarily unavailable: the guide drops its Swagger nav item and the public `/swagger` page shows
an "interactive reference is being published" panel with the Postman collection, rather than an
iframe pointing at nothing. Redeploy the portal after any change here
(`deploy/deploy-test.ps1 -Target portal`).

### ServiceNow shadow mode (live since 2026-08-26)

Every **accepted** payload is additionally POSTed to the ServiceNow dev endpoint, and whatever
comes back is recorded. **None of it reaches the partner** — the response, the status code and the
timing of the partner's request are all completely unaffected.

The point is to learn the real backend contract from live traffic before depending on it.

> **Any custom header the partner sends is forwarded to ServiceNow automatically.** ShopBox has
> said they will send a branch id "in metadata" but the exact header name was not agreed when this
> was built. Rather than guess and miss it, the API captures every header that is not
> infrastructure noise (see the blocklist in `DkBagRegistrationController.IsInfrastructureHeader`)
> and forwards the same set to ServiceNow. Confirmed working 2026-08-26: a request sent with
> `X-ShopBox-Branch-Id: BR-4477` had that header captured on the stored document and forwarded to
> ServiceNow verbatim, alongside `X-Correlation-ID` (kept deliberately, for cross-system tracing),
> `Accept` and `User-Agent`. **Once the real header name is confirmed with ShopBox, promote it to
> an explicit, documented, validated field** rather than relying on this catch-all indefinitely —
> the catch-all is a bridge, not the final design.
>
> The blocklist needed one correction after the first live test: ASP.NET Core and the Azure App
> Service host inject their own headers (`Request-Id`, `Request-Context`, `CLIENT-IP`,
> `X-SITE-DEPLOYMENT-ID`, `WAS-DEFAULT-HOSTNAME`, `X-AppService-Proto`) that are not anything a
> partner sent. These were being captured and forwarded as if they were partner metadata until the
> blocklist was extended to exclude them.

| | |
|---|---|
| Endpoint | `{baseUrl}/bag_registration` (baseUrl from the Key Vault secret) |
| Auth | HTTP Basic, user `BizTalkIntegration` |
| Credential | Key Vault `kv-dev-nokasapi-cashmgmt` → `ServiceNow--Dk--Credential`, a JSON blob of `baseUrl` / `username` / `password` |
| Kill switch | app setting `ServiceNow__Shadow__Enabled` → `false` stops all outbound calls, no redeploy |
| Telemetry | custom event `DkServiceNowShadowForward` — outcome, httpStatus, sysId, message, durationMs, forwardedHeaders, customerName |
| On the document | `serviceNow` property carries the same outcome plus `forwardedHeaders`/`customerName`, so support can answer "did this bag reach ServiceNow, and what did we send with it?" |

> **Every request identifies which onboarded customer it came from — "nokasSource" (added 2026-08-27).**
> The endpoint is shared across every Danish customer, present and future, with the same generic
> payload shape. So that ServiceNow never needs a new field, endpoint or partner directory when a
> new customer is onboarded, we add one small object to the payload before forwarding it:
>
> ```json
> { "bagRegistration": { ...unchanged, exactly what the partner sent... },
>   "nokasSource": { "customerId": "abe243be-...", "customerName": "ShopBox" } }
> ```
>
> `customerId` is the caller's own client id — it needs no lookup and cannot be spoofed, because
> it comes from the OAuth token that authenticated the call, never from the JSON body. `customerName`
> is resolved from a small NOKAS-owned directory (Cosmos container `dk-customers`, PK `/id` = client
> id, cached 5 minutes) so a ServiceNow record is human-readable without a lookup on their side.
> **Never partner-visible** — the partner's own response is completely unchanged; this only affects
> the copy of the payload sent onward.
>
> **Onboarding a new customer (e.g. Coop) needs no ServiceNow-side change at all** — just an insert
> into `dk-customers` (`deploy/provision-dk-customer-directory.ps1` shows the pattern; extend it
> with the new client id/name, or add a document directly). Until that insert happens, a new
> customer's requests still forward successfully — `customerId` is always present, `customerName`
> is simply absent — logged as `ClientId {id} has no entry in the DK customer directory` so the gap
> is visible in App Insights until someone runs the onboarding step. Verified live 2026-08-27: a
> request from a client not yet in the directory forwarded to ServiceNow with `200 success` and
> `customerName: "(not in directory)"` in telemetry, with the partner's own response untouched.

Confirmed working: a real registration returned `200` with
`{"result":{"status":"success","message_id":"…","message":"Bag registration successfull"}}` in
~300 ms, while the partner still received the ordinary MOCK response.

> **The record-id field name changed when the path went generic.** The original
> `/shopbox/bag_registration` path returned `sys_id`; the current generic `/bag_registration` path
> returns `message_id` instead. The response parser accepts either — check
> `TryParseResult` in `ServiceNowShadowForwarder.cs` before assuming one name if this changes again.

**Three deliberate design rules:**

1. **It runs after the response has been sent**, on a background queue. It can never add latency to
   a partner request.
2. **Every failure is swallowed and logged.** A ServiceNow outage, a bad credential or a malformed
   response cannot take the registration endpoint down.
3. **There is no retry.** A retried POST would create a duplicate registration in ServiceNow, and a
   single honest attempt is exactly what shadow mode is meant to observe.

A missing or unparseable credential logs `Critical` and disables forwarding — it does not stop the
app from serving partners. That is why the credential is held in a wrapper type rather than
registered directly in DI, where a null would throw on resolution.

> **The endpoint path is generic as of 2026-08-27** — `/bag_registration`, no customer segment. It
> originally shipped as `/shopbox/bag_registration`; we asked ServiceNow to remove the per-customer
> segment and they did, so a second Danish customer needs no path change and no mapping. The path
> stays configurable via `ServiceNow__RegistrationPath` regardless, in case that ever changes again.

### What was built

| Resource | Name | Notes |
|---|---|---|
| App Service | `api-nokas-bagreg-dk` | Windows, .NET 10, on the existing shared plan `as-test-integrationops`. Inbound restricted to APIM + Front Door, default deny. |
| Cosmos container | `dk-registrations` | Existing account/database. PK `/clientId`, 90-day TTL, `id` = receipt GUID. |
| Blob | `nokasintegrationarchive` / `integrationarchive` | Existing. Blobs prefixed `dk_bag_create_*`. |
| APIM API | `dk-bag-registration-api` | Path `api/cash-mgmt/dk`. `validate-jwt` + 100/60s rate limit + backend rewrite. |
| App Insights | `api-nokas-app-insights` | Existing. `cloud_RoleName = api-nokas-bagreg-dk`. Custom event `DkBagRegistrationStubReceived` per post, accepted or rejected. |

No new Azure service types, no Key Vault dependency (Cosmos and Blob are both reached with
the app's managed identity — the app holds no secrets).

**Front Door needed no change.** Its existing `apim-bagreg` route already matches
`/api/cash-mgmt/*` on `api-test.nokas.com` and forwards to APIM without rewriting the path,
so putting the APIM API at path `api/cash-mgmt/dk` made the TEST domain work for free.

### Code

- `src/Nokas.CashMgmt.Dk.BagRegistration.Api/` — own project, own solution
  (`Nokas.CashMgmt.Dk.slnx`), sharing `Nokas.CashMgmt.Shared`.
  - `Validation/DkBagRegistrationValidator.cs` — every rule, as a pure function over the raw
    JSON. Validating the raw JSON rather than a bound model is what lets errors name the
    offending JSON path instead of leaking .NET type names.
  - `Validation/DkDenominations.cs` — the denomination table.
- `tests/Nokas.CashMgmt.Dk.BagRegistration.Api.Tests/` — 56 tests over the validator.
- `deploy/provision-dk-mock-infrastructure.ps1`, `provision-dk-mock-apim.ps1`,
  `deploy-dk-mock.ps1` — all idempotent.

Authorization is enforced in the API, not in APIM: APIM validates the token
(authentication), the API checks the app role (authorization). That keeps the role name in
one place and lets the API answer with the platform's own `ErrorResponse` shape.

### Design choices that were deliberate

- **The response contract is the real one.** It mirrors the Norwegian response field for
  field, and the stub nature lives only in the `description` wording (capitalised) plus an
  out-of-band `X-Mock-Endpoint` header. Nothing about the shape is conditional on this being a
  stand-in, so partner code survives go-live untouched. The two description strings are
  configuration (`Stub:RegisteredDescription`, `Stub:FoundDescription`), so switching to real
  wording is a setting change, not a code change.
- **Rejected payloads are still stored** in Cosmos with `accepted: false` and the full error
  list, and still archived to Blob. Seeing how a partner actually posts — including their
  failed attempts — is the reason this stub exists. They are not reachable over the API; inspect
  them in Cosmos or Blob.
- **Storage failures do not fail the request.** A Cosmos outage is logged `Critical` and the
  partner still gets their response. The endpoint is not a system of record.
- **Payload archiving runs after authorization,** so a rejected caller cannot write to Blob.
- Payloads over 1 MB are archived to Blob only, with a marker in Cosmos, so an oversized post
  cannot fail on the 2 MB Cosmos document limit.
- The `Authorization` header is never stored. A small allowlist of headers is
  (`Content-Type`, `Accept`, `User-Agent`, `X-Correlation-ID`).
- Telemetry records **which fields failed**, never their values.
