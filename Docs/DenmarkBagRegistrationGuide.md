# Denmark Bag Registration and Counting Result Guide

Reference for how the Danish (ServiceNow-backed) surface works end to end. State as of 2026-09-20, TEST environment.

## Cosmos containers

All are in database `BagRegistration` on account `nokasbagregistrationapitest`.

| Container | Partition key | Document id | Created | Purpose |
|---|---|---|---|---|
| `dk-registrations` | `/clientId` | `bagLifecycleId` (GUID) | Registration stub | Journal of every registration payload received, accepted or rejected. Used for support and for `GET .../bag-registration/{bagNumber}`. Not a source of truth. 90-day TTL. |
| `dk-customers` | `/id` | Client id | Registration stub | Maps a client id to a customer name (for example ShopBox). Only used to add `nokasSource.customerName` to what is forwarded to ServiceNow. |
| `dk-kardex-map` | `/id` | Kardex (`customerRefNumber`) | Counting result | Tenancy. Records which client id owns each kardex. The first registration for a kardex claims it permanently. A different client using it gets `409 ERR_CUSTOMER_REF_CONFLICT`. |
| `dk-countingresults` | `/clientId` | ServiceNow `number` (e.g. `NOC0430326`) | Counting result | Synced counting results served by the partner API, plus one `sync-state` document that supplies `dataAsOf`. 548-day TTL. |

## Counting result flow, end to end

1. **Registration.** The partner calls `POST /api/cash-mgmt/dk/bag-registration` with a kardex. The API validates the payload, stores it in `dk-registrations`, claims the kardex for the caller's client id in `dk-kardex-map`, and forwards the payload to ServiceNow in the background with a `nokasSource` block (client id and customer name).
2. **Sync.** Every 15 minutes, and once at each app start, a background service in the DK API polls ServiceNow `counting_results?state=1` and `?state=10`. It fetches everything each time and never uses ServiceNow's date filter, which is broken.
3. **Attribution.** Each row's kardex is looked up in `dk-kardex-map`. If a client owns it, the row is upserted into `dk-countingresults` under that client id. If nobody has claimed it, the row is skipped and counted, never guessed.
4. **Status and amounts.** State `1` becomes `registered`, `10` becomes `counted`, anything else `unknown`. Declared amount comes from `registeredAmount`. Counted amount is kept only for state `10`. Deviation is counted minus declared.
5. **Freshness.** After each run the sync writes its completion time to the `sync-state` document.
6. **Partner read.** The partner calls `GET /api/cash-mgmt/dk/bag-counting-results?fromDate&toDate` or `.../{bagNumber}` with a token carrying the counting-result role. The API reads only from Cosmos, partitioned by the client id in the token, and never calls ServiceNow. `dataAsOf` comes from `sync-state`. Paging uses opaque tokens bound to the query parameters.

## Access

- Registration role: `cashbag.dk.registration.access`.
- Counting-result role: `cashbag.dk.countingresult.read`. It must be created in the manifest and granted to the partner app by the owner. Until then every token gets 403.

## Known limits (as of 2026-09-21)

- Only the states "New" and "Paid out" are mapped (`registered`, `counted`); any other state is stored as `unknown`.
- `lastModified` and the registered date are assumed to be UTC (ServiceNow sends no zone).
- Bags registered more than 120 days ago are not refreshed by the sync.
- ServiceNow's field is spelled `regiteredDate`; the corrected spelling is also accepted.
- `abe243be-…` is not yet a registered source in ServiceNow; its registrations return 502.

## Update 2026-09-21

- Registration is now a synchronous call to ServiceNow. The customer is told what ServiceNow answered: 201 confirmed, 409 duplicate, 400 refused (with ServiceNow's reason), 502 backend fault, 503 no answer. `dk-registrations` is a journal written after the call.
- ServiceNow rows now carry `clientID`, so counting results are stored under the client that registered the bag. `dk-kardex-map` is only a fallback for rows without a `clientID`, and still backs the 409 kardex-conflict check on registration.
- The sync re-reads every bag registered in the last 120 days each run and removes stored rows that ServiceNow no longer returns.
- The API supports `dateType` = `lastUpdated` (default), `registered`, `counted`, and returns `registeredAt` and `countedAt`.
- ServiceNow requires the bag label to belong to the customer's account (for kardex `4200000198`: label starts `4200198`). Other customers' rules are not yet confirmed.

## Update 2026-09-22

- Every field in a counting-result item is always present in the response, `null` when there is no value (`countedAmount`, `countedAt`, `deviationAmount`, `registeredAt`, `bagNumber`), as are `pagination.continuationToken`, `dataAsOf` and the registration `warnings`.
- Registration accepts an optional `metaData` object beside `bagRegistration`: any name/value pairs (max 20 entries, names 50 chars, values 200 chars, values must be strings). It is stored on the journal, forwarded to ServiceNow with the rest of the body, and never used for access control. It is not echoed back, because the XML response format cannot represent an open map.
- ShopBox asked to send this in a header; that was declined because a header cannot carry a nested object. Their `client_id` is called `companyId` here to avoid confusion with the OAuth client id.

## Update 2026-09-25 — sync moved to its own Function

- The counting-result sync now runs in **`func-test-nokas-bagreg-dk`** (timer, every 10 minutes) instead of inside the API. The API only serves partners and can now be scaled out; before, a second API instance would have run a second sync and raced on the watermark.
- **Incremental.** ServiceNow's URL is `/counting_results/dk` and its `startDate`/`endDate` now filter on last-modified time. Each run asks for what changed since the stored watermark minus a 10-minute overlap. The watermark only advances after a completed run, so a failure re-reads rather than skipping.
- **ServiceNow times are Central European, not UTC.** Windows are converted into their wall clock and their timestamps back to UTC. This also corrected stored `registeredAt`/`lastUpdatedAt`, which had been 1-2 hours ahead.
- **Deletions are no longer detected**, because an incremental feed cannot distinguish unchanged from deleted. Cancellations still arrive as a state change.
- Statuses now cover all 16 ServiceNow states: `registered`, `picked-up`, `in-progress`, `counted`, `cancelled`, `error`, with `unknown` only for a state they add without telling us.
- A `toDate` with no time covers that whole day.

### Code layout

`Nokas.CashMgmt.Dk.Shared` holds what the API and the Function both use: the counting-result and kardex documents and repositories, the ServiceNow options and credential, the clock, the status mapper and the counting client. The API keeps registration, validation and the partner endpoints.

## Update 2026-09-28 — ServiceNow moved to their TEST instance

- The backend is now `nokastest.service-now.com` (was `nokasdev`). The URL lives only in the Key Vault secret `ServiceNow--Dk--Credential`; both apps must be restarted after changing it, because a Key Vault reference caches its value.
- **The bag-label rule is confirmed**, from ServiceNow's own validation code: the first 7 digits of `bagNumber` must equal the first 2 digits of `customerRefNumber` plus its 6th-10th digits. `4200000198` → `4200198`, `4400003533` → `4403533`. Documented in the portal.
- ServiceNow's own internal errors now return `502` to partners instead of `400`, because they arrive in the same shape as a genuine refusal and nothing the partner sends could fix them.
- End-to-end re-verified on the test instance: registration, sync, partner read, duplicate, wrong label, validation, readback and unauthenticated access.
- **Resolved 2026-10-06:** the dev-instance rows were cleared. The whole container was emptied, including the `sync-state`
  document, and the sync Function restarted. Removing `sync-state` is the point: it resets the watermark so the next run
  reaches back over its full initial lookback and rebuilds from ServiceNow, rather than asking only for the last few
  minutes. The three genuine test records returned; the sixteen ghosts did not. Use this same wipe-and-rebuild if the
  container ever drifts again - it holds a copy of ServiceNow's data, never the only copy.
