import { useEffect, type ReactNode } from "react";
import { Link } from "react-router-dom";
import {
  Badge,
  Callout,
  CodeBlock,
  EnvUrlList,
  InlineCode,
  JumpNav,
  MethodBadge,
  SectionHeading,
  Table,
  type HttpMethod,
} from "@/components/ui";
import { API_ENVIRONMENTS, RATE_LIMIT } from "@/config/environments";

/** Route suffix shared by every Counting Result operation. */
const ROUTE_PREFIX = "/api/cash-mgmt";

const requiredHeaders: { name: string; value: string; note: string }[] = [
  { name: "Authorization", value: "Bearer <token>", note: "OAuth2 client-credentials token, cashbag.countingresult.read role" },
  { name: "X-Correlation-ID", value: "<uuid>", note: "Echoed back for end-to-end tracing" },
];

function HeadersTable() {
  return (
    <div className="mt-2">
      <Table headers={["Header", "Value", "Notes"]}>
        {requiredHeaders.map((h) => (
          <tr key={h.name}>
            <td className="whitespace-nowrap px-3 py-2 font-mono font-medium text-gray-900">{h.name}</td>
            <td className="whitespace-nowrap px-3 py-2 font-mono text-brand-700">{h.value}</td>
            <td className="px-3 py-2 text-gray-600">{h.note}</td>
          </tr>
        ))}
      </Table>
    </div>
  );
}

function EndpointCard({
  method,
  path,
  title,
  summary,
  children,
}: {
  method: HttpMethod;
  path: string;
  title: string;
  summary: string;
  children: ReactNode;
}) {
  return (
    <article className="scroll-mt-6 overflow-hidden rounded-xl border border-gray-200 bg-white shadow-sm transition hover:shadow-md">
      <header className="border-b border-gray-200 bg-gray-50/70 px-4 py-3">
        <div className="flex flex-wrap items-center gap-2">
          <MethodBadge method={method} />
          <code className="break-all font-mono text-sm font-semibold text-gray-900">
            {ROUTE_PREFIX}
            {path}
          </code>
        </div>
        <h4 className="mt-1.5 text-sm font-semibold text-gray-800">{title}</h4>
        <p className="mt-0.5 text-xs text-gray-600">{summary}</p>
      </header>

      <div className="space-y-4 p-4">
        <EnvUrlList
          title="Full URL"
          entries={API_ENVIRONMENTS.map((env) => ({
            label: env.label,
            url: `${env.baseUrl}${path}`,
            open: env.open,
          }))}
        />
        {children}
      </div>
    </article>
  );
}

/* ------------------------------------------------------------------ */
/*  Reference data                                                     */
/* ------------------------------------------------------------------ */

const queryParams: { name: string; required: boolean; values: string; note: string }[] = [
  { name: "fromDate", required: true, values: "ISO-8601 UTC", note: "Inclusive start of the date window." },
  { name: "toDate", required: true, values: "ISO-8601 UTC", note: "Inclusive end. Span from fromDate must not exceed 92 days." },
  { name: "dateType", required: false, values: "counted (default) | registered | lastUpdated", note: "Which timestamp field the date range filters on — see below." },
  { name: "status", required: false, values: "registered | in-progress | picked-up | counted | cancelled | error", note: "Optional filter on top of the date range — see bucket table below." },
  { name: "customerRefNumber", required: false, values: "string", note: "Your own store/customer reference. A value that isn't yours returns an empty result, never a 403." },
  { name: "pageSize", required: false, values: "1–500 (default 100)", note: "Larger pages cost more per call but need fewer round-trips." },
  { name: "continuationToken", required: false, values: "opaque string", note: "From the previous page's pagination.continuationToken — see Pagination below." },
];

const statusBuckets: { bucket: string; tone: "gray" | "accent" | "brand" | "green" | "amber" | "red"; meaning: string }[] = [
  { bucket: "registered", tone: "gray", meaning: "Declared but not yet picked up." },
  { bucket: "in-progress", tone: "accent", meaning: "Received by bank or approved by bank — nothing wrong yet, just not counted." },
  { bucket: "picked-up", tone: "brand", meaning: "Collected for transport, awaiting counting." },
  { bucket: "counted", tone: "green", meaning: "Counting is complete. declaredAmount, countedAmount and deviationAmount are populated." },
  { bucket: "cancelled", tone: "red", meaning: "The registration was cancelled before processing." },
  { bucket: "error", tone: "amber", meaning: "Not approved by bank, missing at pickup, or a processing error." },
];

const responseFields: { field: string; type: string; note: string }[] = [
  { field: "bagNumber", type: "string", note: "As registered. Bag numbers recirculate over time — never treat this as a unique identity across your own history." },
  { field: "customerRefNumber", type: "string", note: "Your store/customer reference for this bag." },
  { field: "status", type: "string", note: "One of the 6 bucketed values above — not the full internal status vocabulary." },
  { field: "currencyType", type: "string", note: "Always \"NOK\" today. DKK/Valuta are a future phase." },
  { field: "declaredAmount", type: "number | null", note: "What you declared at registration. Null until the bag reaches the API." },
  { field: "countedAmount", type: "number | null", note: "What NOKAS counted. Null until status is counted." },
  { field: "deviationAmount", type: "number | null", note: "countedAmount − declaredAmount. Null until counted." },
  { field: "registeredAt", type: "date | null", note: "When the bag was registered." },
  { field: "countedAt", type: "date | null", note: "When counting completed. Null until counted." },
  { field: "lastUpdatedAt", type: "date", note: "Timestamp of the most recent status change — the field to poll on for delta sync." },
  { field: "statusHistory", type: "array", note: "Every status this bag has passed through, each with its own changedAt timestamp." },
];

const statusCodes: { code: string; meaning: string; tone: "green" | "amber" | "red" }[] = [
  { code: "200 OK", meaning: "Query succeeded — check items and pagination.hasMore.", tone: "green" },
  { code: "400 Bad Request (VALIDATION_ERROR)", meaning: "A query parameter failed validation. field names which one.", tone: "amber" },
  { code: "400 Bad Request (INVALID_CONTINUATION_TOKEN)", meaning: "The token is malformed, or doesn't match the query parameters it was issued for.", tone: "amber" },
  { code: "401 Unauthorized", meaning: "Token missing, expired, or missing the cashbag.countingresult.read role.", tone: "amber" },
  { code: "404 Not Found", meaning: "Single-bag lookup only — no counting result exists for that bag under your account.", tone: "amber" },
  { code: "429 Too Many Requests", meaning: `Rate limit exceeded (${RATE_LIMIT} per application).`, tone: "red" },
];

const onThisPage = [
  { id: "list", label: "List results" },
  { id: "query-params", label: "Query parameters" },
  { id: "date-type", label: "dateType vs status" },
  { id: "buckets", label: "Status buckets" },
  { id: "pagination", label: "Pagination" },
  { id: "single-bag", label: "Single bag" },
  { id: "status-codes", label: "Status codes" },
];

export default function CountingResultEndpointsSection() {
  useEffect(() => {
    document.title = "API Guide | Counting Result Endpoints";
  }, []);

  return (
    <section className="space-y-8">
      {/* ---------------- Page header ---------------- */}
      <header className="space-y-3">
        <p className="text-xs font-semibold uppercase tracking-widest text-brand-600">Counting Result API</p>
        <h2 className="text-2xl font-bold tracking-tight text-gray-900">Counting Result Endpoints</h2>
        <p className="max-w-3xl text-sm leading-relaxed text-gray-600">
          Retrieve the declared-vs-counted outcome for your bags — either as a filtered, paginated
          list for reconciliation jobs, or a single lookup by bag number. Results are synced from
          NOKAS's processing systems on a rolling basis, not fetched live, so every response carries a{" "}
          <InlineCode>dataAsOf</InlineCode> watermark telling you how fresh the data is.
        </p>

        <div className="rounded-lg border border-accent-200 bg-accent-50 p-3">
          <EnvUrlList
            title="Base URL"
            entries={API_ENVIRONMENTS.map((env) => ({ label: env.label, url: env.baseUrl, open: env.open }))}
          />
          <p className="mt-2 text-xs text-accent-900/80">
            Every path below is relative to these.
          </p>
        </div>

        <Callout tone="warning" title="Separate authorization role">
          This API is protected by its own app role, <InlineCode>cashbag.countingresult.read</InlineCode> —
          distinct from the <InlineCode>cashbag.registration.access</InlineCode> role used for Bag
          Registration. If your token only has one, calls to the other API return{" "}
          <InlineCode>401</InlineCode>. Contact NOKAS if you need both granted.
        </Callout>

        <JumpNav items={onThisPage} />
      </header>

      {/* ---------------- List endpoint ---------------- */}
      <div id="list" className="space-y-4">
        <SectionHeading icon="📊">List counting results</SectionHeading>

        <EndpointCard
          method="GET"
          path="/bag-counting-results"
          title="Query counting results"
          summary="Filter by date range, status, and your own customer reference. Paginated with an opaque continuation token."
        >
          <HeadersTable />

          <CodeBlock
            label="Request"
            code={`GET ${ROUTE_PREFIX}/bag-counting-results?fromDate=2026-08-01T00:00:00Z&toDate=2026-08-14T00:00:00Z&status=counted&pageSize=50`}
          />

          <CodeBlock
            label="200 OK"
            code={`{
  "items": [
    {
      "bagNumber": "2033331011",
      "customerRefNumber": "5790002334764",
      "status": "counted",
      "currencyType": "NOK",
      "declaredAmount": 10000.0,
      "countedAmount": 10000.0,
      "deviationAmount": 0.0,
      "registeredAt": "2026-08-01T09:12:00",
      "countedAt": "2026-08-01T14:05:00",
      "lastUpdatedAt": "2026-08-01T14:05:00",
      "statusHistory": [
        { "status": "registered", "changedAt": "2026-08-01T09:12:00" },
        { "status": "picked-up", "changedAt": "2026-08-01T11:30:00" },
        { "status": "counted", "changedAt": "2026-08-01T14:05:00" }
      ]
    }
  ],
  "pagination": {
    "pageSize": 50,
    "count": 1,
    "hasMore": false,
    "continuationToken": null
  },
  "dataAsOf": "2026-08-14T06:00:00Z",
  "correlationId": "5e3ff647-b06f-4725-9ec7-8e6dfce18fbf"
}`}
          />
        </EndpointCard>
      </div>

      {/* ---------------- Query params ---------------- */}
      <div id="query-params" className="space-y-3">
        <SectionHeading icon="🧮">Query parameters</SectionHeading>

        <Table headers={["Parameter", "Required", "Values", "Notes"]}>
          {queryParams.map((p) => (
            <tr key={p.name}>
              <td className="whitespace-nowrap px-3 py-2 font-mono font-medium text-gray-900">{p.name}</td>
              <td className="whitespace-nowrap px-3 py-2">
                {p.required ? <Badge tone="brand">Required</Badge> : <Badge tone="gray">Optional</Badge>}
              </td>
              <td className="px-3 py-2 font-mono text-xs text-gray-700">{p.values}</td>
              <td className="px-3 py-2 text-gray-600">{p.note}</td>
            </tr>
          ))}
        </Table>
      </div>

      {/* ---------------- dateType vs status ---------------- */}
      <div id="date-type" className="space-y-3">
        <SectionHeading icon="🗓️">dateType and status are independent filters</SectionHeading>

        <p className="text-sm leading-relaxed text-gray-600">
          <InlineCode>dateType</InlineCode> picks <em>which timestamp field</em> your{" "}
          <InlineCode>fromDate</InlineCode>/<InlineCode>toDate</InlineCode> window applies to.{" "}
          <InlineCode>status</InlineCode> then narrows those results down by current state. You can
          pass either, both, or neither (dateType always has a default) — they compose, they don't
          replace each other.
        </p>

        <Table headers={["dateType", "Filters the window on…", "Typical use"]}>
          <tr>
            <td className="whitespace-nowrap px-3 py-2 font-mono font-medium text-gray-900">counted (default)</td>
            <td className="px-3 py-2 text-gray-700"><InlineCode>countedAt</InlineCode></td>
            <td className="px-3 py-2 text-gray-600">Reconciliation — "what got counted this week?"</td>
          </tr>
          <tr>
            <td className="whitespace-nowrap px-3 py-2 font-mono font-medium text-gray-900">registered</td>
            <td className="px-3 py-2 text-gray-700"><InlineCode>registeredAt</InlineCode></td>
            <td className="px-3 py-2 text-gray-600">Following up on bags you registered in a window, regardless of where they are now.</td>
          </tr>
          <tr>
            <td className="whitespace-nowrap px-3 py-2 font-mono font-medium text-gray-900">lastUpdated</td>
            <td className="px-3 py-2 text-gray-700"><InlineCode>lastUpdatedAt</InlineCode></td>
            <td className="px-3 py-2 text-gray-600">Delta polling — "give me everything that changed since my last sync."</td>
          </tr>
        </Table>

        <Callout tone="info" title="Example — combining both">
          <InlineCode>dateType=registered&status=cancelled</InlineCode> with a date range returns bags
          you registered in that window that ended up cancelled — regardless of when the cancellation
          itself happened.
        </Callout>
      </div>

      {/* ---------------- Status buckets ---------------- */}
      <div id="buckets" className="space-y-3">
        <SectionHeading icon="🪣">Status buckets</SectionHeading>

        <p className="text-sm leading-relaxed text-gray-600">
          The <InlineCode>status</InlineCode> filter and every item's <InlineCode>status</InlineCode>{" "}
          field use a deliberately coarse, 6-value vocabulary — folded down from NOKAS's internal
          processing states. This is different from the Bag Registration API's own status values;
          don't assume the two APIs share a vocabulary.
        </p>

        <Table headers={["Bucket", "Meaning"]}>
          {statusBuckets.map((b) => (
            <tr key={b.bucket}>
              <td className="whitespace-nowrap px-3 py-2">
                <Badge tone={b.tone}>{b.bucket}</Badge>
              </td>
              <td className="px-3 py-2 text-gray-700">{b.meaning}</td>
            </tr>
          ))}
        </Table>
      </div>

      {/* ---------------- Response fields ---------------- */}
      <div className="space-y-3">
        <SectionHeading icon="🧾">Per-item response fields</SectionHeading>

        <Table headers={["Field", "Type", "Notes"]}>
          {responseFields.map((f) => (
            <tr key={f.field}>
              <td className="whitespace-nowrap px-3 py-2 font-mono font-medium text-gray-900">{f.field}</td>
              <td className="whitespace-nowrap px-3 py-2 font-mono text-xs text-gray-600">{f.type}</td>
              <td className="px-3 py-2 text-gray-600">{f.note}</td>
            </tr>
          ))}
        </Table>
      </div>

      {/* ---------------- Pagination ---------------- */}
      <div id="pagination" className="space-y-3">
        <SectionHeading icon="📑">Pagination</SectionHeading>

        <p className="text-sm leading-relaxed text-gray-600">
          Pagination is opaque-token only — there is no page number or offset parameter. Check{" "}
          <InlineCode>pagination.hasMore</InlineCode>; if true, pass{" "}
          <InlineCode>pagination.continuationToken</InlineCode> back as the{" "}
          <InlineCode>continuationToken</InlineCode> query parameter on your next call to fetch the
          next page.
        </p>

        <Callout tone="danger" title="Keep every other parameter identical between pages">
          The continuation token is bound to the exact query it was issued for (date range,{" "}
          <InlineCode>dateType</InlineCode>, <InlineCode>status</InlineCode>,{" "}
          <InlineCode>customerRefNumber</InlineCode>, <InlineCode>pageSize</InlineCode>). If any of
          them differ on the next call — including <InlineCode>toDate</InlineCode> silently drifting
          because you regenerated "now" on every request — the API rejects the token with{" "}
          <InlineCode>400 INVALID_CONTINUATION_TOKEN</InlineCode>. Compute your date range once per
          paging run and reuse the exact same values on every page.
        </Callout>

        <CodeBlock
          label="Page 2 request"
          code={`GET ${ROUTE_PREFIX}/bag-counting-results?fromDate=2026-08-01T00:00:00Z&toDate=2026-08-14T00:00:00Z&status=counted&pageSize=50&continuationToken=eyJuYXRpdmVUb2tlbiI6Ii4uLiJ9`}
        />
      </div>

      {/* ---------------- Single bag ---------------- */}
      <div id="single-bag" className="space-y-4">
        <SectionHeading icon="🔎">Single-bag lookup</SectionHeading>

        <EndpointCard
          method="GET"
          path="/bag-counting-results/{bagNumber}"
          title="Retrieve a counting result by bag number"
          summary="Returns the most recent counting-result record for that bag number, scoped to your account."
        >
          <div>
            <p className="text-xs font-semibold uppercase tracking-wider text-gray-500">
              Required headers
            </p>
            <HeadersTable />
          </div>

          <CodeBlock
            label="200 OK"
            code={`{
  "bagNumber": "2033331011",
  "customerRefNumber": "5790002334764",
  "status": "counted",
  "currencyType": "NOK",
  "declaredAmount": 10000.0,
  "countedAmount": 10000.0,
  "deviationAmount": 0.0,
  "registeredAt": "2026-08-01T09:12:00",
  "countedAt": "2026-08-01T14:05:00",
  "lastUpdatedAt": "2026-08-01T14:05:00",
  "statusHistory": [
    { "status": "registered", "changedAt": "2026-08-01T09:12:00" },
    { "status": "picked-up", "changedAt": "2026-08-01T11:30:00" },
    { "status": "counted", "changedAt": "2026-08-01T14:05:00" }
  ]
}`}
          />

          <p className="rounded-md bg-amber-50 px-3 py-2 text-xs text-amber-900 ring-1 ring-inset ring-amber-600/20">
            <strong>Note:</strong> bag numbers recirculate as physical bags are reused. This endpoint
            always returns the <em>latest</em> record for that number under your account — not
            necessarily the one you're expecting if the number has been reused since you last checked.
          </p>
        </EndpointCard>
      </div>

      {/* ---------------- Status codes ---------------- */}
      <div className="space-y-3">
        <SectionHeading id="status-codes" icon="📶">
          Status Codes
        </SectionHeading>

        <Table headers={["Code", "Meaning"]}>
          {statusCodes.map((s) => (
            <tr key={s.code}>
              <td className="whitespace-nowrap px-3 py-2">
                <Badge tone={s.tone}>{s.code}</Badge>
              </td>
              <td className="px-3 py-2 text-gray-700">{s.meaning}</td>
            </tr>
          ))}
        </Table>

        <p className="text-xs text-gray-600">
          Every error response carries the same shape, including the{" "}
          <code className="rounded bg-gray-100 px-1 py-0.5 font-mono">correlationId</code> for the
          failed request. See the{" "}
          <Link
            to="/portal/api-guide/norway/errors"
            className="font-semibold text-brand-700 underline-offset-2 hover:underline"
          >
            Errors
          </Link>{" "}
          section for the full catalogue.
        </p>
      </div>

      {/* ---------------- Cross-link ---------------- */}
      <aside className="rounded-lg border-l-4 border-brand-500 bg-brand-50/60 p-4">
        <h3 className="text-sm font-semibold text-brand-900">Registering bags?</h3>
        <p className="mt-1 text-xs leading-relaxed text-brand-900/80">
          Bags must be registered through a separate API surface before they can ever have a
          counting result.
        </p>
        <Link
          to="/portal/api-guide/norway/bag-registration"
          className="mt-2 inline-flex items-center gap-1 text-xs font-semibold text-brand-700 underline-offset-2 hover:underline"
        >
          Go to the Bag Registration API
          <span aria-hidden="true">→</span>
        </Link>
      </aside>
    </section>
  );
}
