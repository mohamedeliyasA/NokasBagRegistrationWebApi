# Denmark counting results — data feed requirements

**From:** NOKAS / Avarn Security — Integration Platform
**To:** ServiceNow team
**Subject:** How we would like to read counting results out of ServiceNow

---

## 1. What we are building

NOKAS is exposing a **Counting Result API** to Danish retail customers (starting with ShopBox).
A customer calls our API and gets back, for their own bags only: what they declared, what NOKAS
actually counted, and the deviation between the two.

The data originates in ServiceNow. We need a reliable way to read it out.

## 2. How we intend to consume it

We do **not** query ServiceNow when a customer calls us. Instead we poll ServiceNow on a schedule
(target: **every 30 minutes**) and keep our own copy.

Three reasons, all of which protect your instance:

1. **Tenancy.** ServiceNow has no concept of our customers' API credentials. We must attach the
   right customer identity to every record before we can safely serve it. That join happens once,
   at sync time, rather than on every request.
2. **Load.** A customer running a nightly reconciliation may page through 90 days of history. That
   traffic hits our store, never your instance.
3. **Availability.** Your maintenance windows do not become our outage.

Our polling is **incremental** — we ask only for records that changed since we last looked — and
**idempotent**: we key on the record's immutable id and overwrite, so re-reading the same record is
harmless. We can safely re-request an overlapping window, which is how we tolerate clock skew and
retries.

---

## 3. Option A — a Scripted REST endpoint *(our preference)*

You expose one purpose-built read-only endpoint, for example:

```
GET /api/<scope>/nokas_counting_result/changes?updatedSince=2026-08-19T08:00:00Z&limit=500
```

Returning a page of records plus a marker for where the next page starts.

**Why we prefer this**

| | |
|---|---|
| **Stable contract** | You can refactor, rename or restructure the underlying tables without breaking us. With direct table access, any schema change is our outage. |
| **Least privilege** | The integration user needs access to exactly one endpoint, not read rights over a table. |
| **You control the shape** | You decide what is exposed. Nothing internal leaks by accident. |
| **Correct pagination is yours to guarantee** | You can implement it in a way that is safe against concurrent edits (see §5.2). |
| **Cheaper for you** | One tuned query per call instead of us issuing broad table queries. |

**What it must do**

- Accept a timestamp (`updatedSince`) and return every record modified at or after it.
- Return results **ordered oldest-changed first**, so a page boundary is never ambiguous.
- Return a **continuation marker** (a cursor, or the last record's id + timestamp) so we can resume
  exactly where we stopped.
- Return an explicit indicator of whether more data remains.
- Be **read-only**. We never write to ServiceNow on this path.

---

## 4. Option B — the Table API *(workable, with conditions)*

If a Scripted REST endpoint is not feasible, we can use the standard Table API against a table or
database view you designate. This works, but it puts the correctness burden on us, and it couples
us to your schema. If we go this way we need the following agreed in writing:

1. **A stable table or database view.** Not a table you expect to restructure. If the underlying
   model changes, we need notice.
2. **A read-only integration user** with ACLs scoped to just that table and just the fields in §6.
3. **Confirmation that the fields in §6 exist and are populated.**
4. **The query parameters we intend to use are permitted** and not blocked by ACL or business rule
   (see §5.2).

---

## 5. Requirements that apply either way

### 5.1 Authentication

**OAuth 2.0**, not Basic auth. Client credentials if your instance supports it; otherwise JWT
bearer. Please tell us which grant types are available. We hold the credential in Azure Key Vault
and rotate it on request.

### 5.2 Ordering and pagination

This is the single most important technical point, so it is worth being explicit.

We need to page through a result set that **is changing while we read it**. Offset-based paging
(`sysparm_offset=0`, `500`, `1000` …) is not safe under those conditions: if a record is updated
between page 1 and page 2 its position in the sort order moves, and the row either **appears twice
or is skipped entirely**. A skipped counting result is a silent data-loss bug we would not detect.

We therefore need paging that is **keyed on values in the data**, not on a row offset:

- Sort **ascending** by last-modified timestamp, with the record id as a tie-breaker
  (`sys_updated_on ASC, sys_id ASC`) so the ordering is total and stable.
- Each request asks for "everything after this timestamp/id", rather than "skip the first N rows".

If we use the Table API, please confirm we may filter and order on the last-modified field, and
that deep paging is not restricted.

### 5.3 Timestamps and time zones

- All timestamps must be **UTC**, in a documented format.
- Please make sure the feed returns **raw stored values, not display-formatted values**. Display
  formatting renders dates in the calling user's configured time zone, which produces a silent
  offset that is very hard to spot — the data looks plausible, just wrong by a few hours.
- Note that ServiceNow's last-modified timestamp has **one-second** granularity. Two records can
  share the same value, which is exactly why we need the id tie-breaker in §5.2. We also re-read a
  small overlap window on each poll; duplicate records are harmless to us.

### 5.4 Deletions — important

An incremental "give me what changed since X" feed **cannot see a hard delete**. The row simply
stops existing and we would keep serving a stale counting result to the customer indefinitely.

We therefore ask that records in this feed are **never hard-deleted**. If a record is cancelled,
voided or superseded, please represent that as a **state change** on the record so it flows to us
through the normal update path. If hard deletes are unavoidable, we need an agreed way to learn
about them.

### 5.5 Rate limits and volume

- Please tell us the **inbound REST rate limits** on the instance for our integration user, so we
  can size our poll interval and page size to stay well within them.
- We expect **low volume** — on the order of a few thousand records per day at most.
- Our default plan is one poll every 30 minutes, ~500 records per page. We will tune to whatever
  suits you.

### 5.6 Environments

We need a **non-production instance** to build and test against first, with representative data,
before we point at production.

---

## 6. Fields we need

### Mandatory — the feed cannot work without these

| Field | Why |
|---|---|
| **Immutable record id** (`sys_id`) | Our primary key. Must never change or be reused for a different record. |
| **Last modified timestamp** (`sys_updated_on`) | Drives the incremental sync. Without it we can only do full reloads. |
| **Bag number** | How the customer identifies the bag. This is what they search on. |
| **Customer identifier** | Which customer the bag belongs to. Without this we cannot safely show the record to anyone. Please tell us what this field is and what it contains (account, company, or a customer/branch reference). |
| **Declared amount** | What the customer said was in the bag. |
| **Counted amount** | What NOKAS actually counted. |
| **Registered date** | When the bag was registered. |
| **Counted date** | When counting completed. |

> The declared amount matters as much as the counted amount. The entire purpose of the API is the
> comparison between the two — a counted figure on its own is not a counting result.

### Strongly requested

| Field | Why |
|---|---|
| **State / status**, with the full list of possible values | So the customer can see where a bag has reached, and distinguish "not counted yet" from "counted". |
| **A cancelled / voided state** | We expose a cancel operation. Without a cancelled state, a cancelled bag would appear stuck as "registered" forever. |
| **Currency code** | Constant DKK today, but explicit beats implied. |
| **Deviation** | We can calculate it as counted − declared. If ServiceNow already holds an authoritative figure we would rather use yours than compute a second one that could disagree. |

### Two questions on the amounts

1. **Are amounts stored in minor units (øre) or major units (DKK)?** For example, is 888.50 DKK
   stored as `88850` or as `888.50`? Getting this wrong is a factor-of-100 error.
2. **What type are they?** We need to avoid floating-point representation for money.

---

## 7. What we commit to on our side

- We only ever **read**. We never write, update or delete anything in ServiceNow on this path.
- We poll on a fixed schedule and will not burst.
- We handle retries with backoff and will not hammer the instance on failure.
- We store only the fields listed above, and only for onboarded customers.
- We will give you our source IP ranges if you want to allow-list them.

---

## 8. Summary of what we need from you

**Decisions**

1. Option A (Scripted REST endpoint) or Option B (Table API)? We recommend A.
2. Confirmation that records will be **soft-deleted / state-changed, never hard-deleted** (§5.4).

**Information**

3. The field list in §6 — which exist, what they are called, and are they reliably populated.
4. Are amounts in øre or DKK, and what data type (§6).
5. The customer identifier field, and what value it holds (§6).
6. The full list of state values, including any cancelled state.
7. OAuth grant types supported by the instance (§5.1).
8. Inbound REST rate limits for our integration user (§5.5).
9. Availability of a test instance with representative data (§5.6).

**Nothing here is urgent to the day.** But items 1–5 block us from finalising the API contract we
publish to customers, so an early answer on those is the most useful thing.

---

*Contact: NOKAS Integration Platform team.*
