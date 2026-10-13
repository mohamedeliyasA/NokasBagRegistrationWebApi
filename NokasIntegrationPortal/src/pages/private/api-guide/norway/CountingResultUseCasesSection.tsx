import { useEffect } from "react";
import { Link } from "react-router-dom";
import { Badge, Callout, CodeBlock, EnvUrlList, InlineCode, JumpNav, MethodBadge, PageHeader, SectionHeading, type HttpMethod } from "@/components/ui";
import { API_ENVIRONMENTS, TEST_ENV } from "@/config/environments";

const BASE = TEST_ENV.baseUrl;

const jumpItems = [
  { id: "date-range", label: "Date range" },
  { id: "status-filter", label: "By status" },
  { id: "combined-filter", label: "Combined filters" },
  { id: "pagination", label: "Pagination" },
  { id: "delta-poll", label: "Delta polling" },
  { id: "single-bag", label: "Single bag" },
];

function UseCase({
  id,
  method,
  path,
  title,
  summary,
  children,
}: {
  id: string;
  method: HttpMethod;
  path: string;
  title: string;
  summary: string;
  children: React.ReactNode;
}) {
  return (
    <div className="space-y-4">
      <SectionHeading id={id}>{title}</SectionHeading>

      <div className="flex flex-wrap items-center gap-2">
        <MethodBadge method={method} />
        <code className="break-all font-mono text-sm font-semibold text-gray-900">{path}</code>
      </div>

      <p className="text-sm leading-relaxed text-gray-600">{summary}</p>

      {children}
    </div>
  );
}

export default function CountingResultUseCasesSection() {
  useEffect(() => {
    document.title = "API Guide | Counting Result Use Cases";
  }, []);

  return (
    <section className="space-y-10">
      <PageHeader
        eyebrow="Counting Result API"
        title="Use cases & worked examples"
        lead="Six worked scenarios covering the filters, pagination, and delta-polling patterns partners actually use for reconciliation."
      >
        <div className="rounded-lg border border-accent-200 bg-accent-50 p-3">
          <EnvUrlList
            title="Base URL"
            entries={API_ENVIRONMENTS.map((env) => ({ label: env.label, url: env.baseUrl, open: env.open }))}
          />
          <p className="mt-2 text-xs text-accent-900/80">
            The worked examples below use the <strong>Test</strong> host. Swap the base URL for the
            production one when you go live.
          </p>
        </div>
        <JumpNav items={jumpItems} />
      </PageHeader>

      {/* ---------------- 1. Date range only ---------------- */}
      <UseCase
        id="date-range"
        method="GET"
        path="/api/cash-mgmt/bag-counting-results"
        title="1. List everything counted in a date range"
        summary="No dateType or status supplied — dateType defaults to counted, so this returns every bag counted within the window, regardless of current status."
      >
        <CodeBlock
          label="Request"
          code={`GET ${BASE}/bag-counting-results?fromDate=2026-08-01T00:00:00Z&toDate=2026-08-14T00:00:00Z&pageSize=20`}
        />
        <CodeBlock
          label="200 OK"
          code={`{
  "items": [
    {
      "bagNumber": "34567895",
      "customerRefNumber": "5790002334764",
      "status": "counted",
      "currencyType": "NOK",
      "declaredAmount": 2500.0,
      "countedAmount": 2500.0,
      "deviationAmount": 0.0,
      "registeredAt": "2026-08-02T09:00:00",
      "countedAt": "2026-08-03T13:20:00",
      "lastUpdatedAt": "2026-08-03T13:20:00",
      "statusHistory": [
        { "status": "registered", "changedAt": "2026-08-02T09:00:00" },
        { "status": "picked-up", "changedAt": "2026-08-02T15:00:00" },
        { "status": "counted", "changedAt": "2026-08-03T13:20:00" }
      ]
    }
  ],
  "pagination": { "pageSize": 20, "count": 1, "hasMore": false, "continuationToken": null },
  "dataAsOf": "2026-08-14T06:00:00Z",
  "correlationId": "9c1ff647-b06f-4725-9ec7-8e6dfce18fb1"
}`}
        />
      </UseCase>

      {/* ---------------- 2. Status filter ---------------- */}
      <UseCase
        id="status-filter"
        method="GET"
        path="/api/cash-mgmt/bag-counting-results"
        title="2. Find deviations — bags counted with a discrepancy"
        summary="Filter to status=counted, then inspect deviationAmount client-side. Every counted item always carries the three amount fields together."
      >
        <CodeBlock
          label="Request"
          code={`GET ${BASE}/bag-counting-results?fromDate=2026-08-01T00:00:00Z&toDate=2026-08-14T00:00:00Z&status=counted`}
        />
        <CodeBlock
          label="200 OK — a bag with a deviation"
          code={`{
  "items": [
    {
      "bagNumber": "34567896",
      "customerRefNumber": "5790002334764",
      "status": "counted",
      "currencyType": "NOK",
      "declaredAmount": 2500.0,
      "countedAmount": 2000.0,
      "deviationAmount": -500.0,
      "registeredAt": "2026-08-05T10:00:00",
      "countedAt": "2026-08-06T09:45:00",
      "lastUpdatedAt": "2026-08-06T09:45:00",
      "statusHistory": [
        { "status": "registered", "changedAt": "2026-08-05T10:00:00" },
        { "status": "picked-up", "changedAt": "2026-08-05T16:00:00" },
        { "status": "counted", "changedAt": "2026-08-06T09:45:00" }
      ]
    }
  ],
  "pagination": { "pageSize": 100, "count": 1, "hasMore": false, "continuationToken": null },
  "dataAsOf": "2026-08-14T06:00:00Z",
  "correlationId": "9c1ff647-b06f-4725-9ec7-8e6dfce18fb2"
}`}
        />
      </UseCase>

      {/* ---------------- 3. Combined filters ---------------- */}
      <UseCase
        id="combined-filter"
        method="GET"
        path="/api/cash-mgmt/bag-counting-results"
        title="3. Combine dateType and status"
        summary="dateType picks which timestamp the date range applies to; status then narrows the result on top. Here: bags registered in the window that ended up cancelled — regardless of when the cancellation happened."
      >
        <CodeBlock
          label="Request"
          code={`GET ${BASE}/bag-counting-results?fromDate=2026-08-01T00:00:00Z&toDate=2026-08-14T00:00:00Z&dateType=registered&status=cancelled`}
        />
        <CodeBlock
          label="200 OK"
          code={`{
  "items": [
    {
      "bagNumber": "34567899",
      "customerRefNumber": "5790002334764",
      "status": "cancelled",
      "currencyType": "NOK",
      "declaredAmount": 1500.0,
      "countedAmount": null,
      "deviationAmount": null,
      "registeredAt": "2026-08-03T08:30:00",
      "countedAt": null,
      "lastUpdatedAt": "2026-08-03T09:10:00",
      "statusHistory": [
        { "status": "registered", "changedAt": "2026-08-03T08:30:00" },
        { "status": "cancelled", "changedAt": "2026-08-03T09:10:00" }
      ]
    }
  ],
  "pagination": { "pageSize": 100, "count": 1, "hasMore": false, "continuationToken": null },
  "dataAsOf": "2026-08-14T06:00:00Z",
  "correlationId": "9c1ff647-b06f-4725-9ec7-8e6dfce18fb3"
}`}
        />
      </UseCase>

      {/* ---------------- 4. Pagination ---------------- */}
      <UseCase
        id="pagination"
        method="GET"
        path="/api/cash-mgmt/bag-counting-results"
        title="4. Page through a large result set"
        summary="Set pageSize, check pagination.hasMore, and if true, pass pagination.continuationToken back as continuationToken on the next call."
      >
        <CodeBlock
          label="Page 1 — request"
          code={`GET ${BASE}/bag-counting-results?fromDate=2026-07-01T00:00:00Z&toDate=2026-08-14T00:00:00Z&pageSize=10`}
        />
        <CodeBlock
          label="Page 1 — response (truncated)"
          code={`{
  "items": [ /* 10 items */ ],
  "pagination": {
    "pageSize": 10,
    "count": 10,
    "hasMore": true,
    "continuationToken": "eyJuYXRpdmVUb2tlbiI6Ii4uLiJ9"
  },
  "dataAsOf": "2026-08-14T06:00:00Z",
  "correlationId": "9c1ff647-b06f-4725-9ec7-8e6dfce18fb4"
}`}
        />
        <CodeBlock
          label="Page 2 — request"
          code={`GET ${BASE}/bag-counting-results?fromDate=2026-07-01T00:00:00Z&toDate=2026-08-14T00:00:00Z&pageSize=10&continuationToken=eyJuYXRpdmVUb2tlbiI6Ii4uLiJ9`}
        />

        <Callout tone="danger" title="fromDate, toDate, pageSize, status, dateType and customerRefNumber must match exactly on every page">
          The continuation token embeds a hash of the query it was issued for. A common bug: computing{" "}
          <InlineCode>toDate</InlineCode> as "now" freshly on each request — since it re-evaluates on
          every call, page 2's hash no longer matches page 1's, and the API rejects the token with{" "}
          <InlineCode>400 INVALID_CONTINUATION_TOKEN</InlineCode> even though nothing looks wrong.
          Compute the date range <strong>once</strong> at the start of a paging run, store it, and
          reuse the identical values for every subsequent page.
        </Callout>
      </UseCase>

      {/* ---------------- 5. Delta polling ---------------- */}
      <UseCase
        id="delta-poll"
        method="GET"
        path="/api/cash-mgmt/bag-counting-results"
        title="5. Poll for changes since your last sync (watermark pattern)"
        summary="dateType=lastUpdated is the delta-polling mode — it catches every status transition, including counted-amount corrections after the fact, not just newly-counted bags."
      >
        <CodeBlock
          label="Request — first poll of the day"
          code={`GET ${BASE}/bag-counting-results?fromDate=2026-08-13T06:00:00Z&toDate=2026-08-14T06:00:00Z&dateType=lastUpdated&pageSize=100`}
        />
        <Callout tone="info" title="How to use it in a loop">
          Store <InlineCode>dataAsOf</InlineCode> from each response as your next{" "}
          <InlineCode>fromDate</InlineCode>. Page through with <InlineCode>continuationToken</InlineCode>{" "}
          until <InlineCode>hasMore</InlineCode> is false, then sleep until your next scheduled poll.
          Because sync runs every 30 minutes on the NOKAS side, polling more often than that will
          mostly return empty pages.
        </Callout>
      </UseCase>

      {/* ---------------- 6. Single bag ---------------- */}
      <UseCase
        id="single-bag"
        method="GET"
        path="/api/cash-mgmt/bag-counting-results/{bagNumber}"
        title="6. Look up one bag by number"
        summary="Returns the most recent counting-result record for that bag number. The response shape depends on how far the bag has progressed."
      >
        <div className="space-y-5">
          <div>
            <Badge tone="gray">Case 1 — registered, not yet picked up</Badge>
            <CodeBlock
              label="200 OK"
              code={`{
  "bagNumber": "34567897",
  "customerRefNumber": "5790002334764",
  "status": "registered",
  "currencyType": "NOK",
  "declaredAmount": 2500.0,
  "countedAmount": null,
  "deviationAmount": null,
  "registeredAt": "2026-08-13T09:00:00",
  "countedAt": null,
  "lastUpdatedAt": "2026-08-13T09:00:00",
  "statusHistory": [
    { "status": "registered", "changedAt": "2026-08-13T09:00:00" }
  ]
}`}
            />
          </div>

          <div>
            <Badge tone="accent">Case 2 — picked up, awaiting counting</Badge>
            <CodeBlock
              label="200 OK"
              code={`{
  "bagNumber": "34567896",
  "customerRefNumber": "5790002334764",
  "status": "picked-up",
  "currencyType": "NOK",
  "declaredAmount": 2500.0,
  "countedAmount": null,
  "deviationAmount": null,
  "registeredAt": "2026-08-12T09:00:00",
  "countedAt": null,
  "lastUpdatedAt": "2026-08-12T15:30:00",
  "statusHistory": [
    { "status": "registered", "changedAt": "2026-08-12T09:00:00" },
    { "status": "picked-up", "changedAt": "2026-08-12T15:30:00" }
  ]
}`}
            />
          </div>

          <div>
            <Badge tone="green">Case 3 — counted, amounts match</Badge>
            <CodeBlock
              label="200 OK"
              code={`{
  "bagNumber": "34567895",
  "customerRefNumber": "5790002334764",
  "status": "counted",
  "currencyType": "NOK",
  "declaredAmount": 2500.0,
  "countedAmount": 2500.0,
  "deviationAmount": 0.0,
  "registeredAt": "2026-08-02T09:00:00",
  "countedAt": "2026-08-03T13:20:00",
  "lastUpdatedAt": "2026-08-03T13:20:00",
  "statusHistory": [
    { "status": "registered", "changedAt": "2026-08-02T09:00:00" },
    { "status": "picked-up", "changedAt": "2026-08-02T15:00:00" },
    { "status": "counted", "changedAt": "2026-08-03T13:20:00" }
  ]
}`}
            />
          </div>

          <div>
            <Badge tone="amber">Case 4 — not found for this account</Badge>
            <CodeBlock
              label="404 Not Found"
              code={`{
  "title": "No counting result found for bag number '99999999' for this client.",
  "status": 404,
  "correlationId": "9c1ff647-b06f-4725-9ec7-8e6dfce18fb5",
  "code": "NOT_FOUND"
}`}
            />
          </div>
        </div>
      </UseCase>

      {/* ---------------- Cross-link ---------------- */}
      <aside className="rounded-lg border-l-4 border-brand-500 bg-brand-50/60 p-4">
        <h3 className="text-sm font-semibold text-brand-900">Registering a new bag?</h3>
        <p className="mt-1 text-xs leading-relaxed text-brand-900/80">
          A bag must exist through the Bag Registration API before it can ever produce a counting result.
        </p>
        <Link
          to="/portal/api-guide/norway/bag-registration/use-cases"
          className="mt-2 inline-flex items-center gap-1 text-xs font-semibold text-brand-700 underline-offset-2 hover:underline"
        >
          See Bag Registration use cases
          <span aria-hidden="true">→</span>
        </Link>
      </aside>
    </section>
  );
}
