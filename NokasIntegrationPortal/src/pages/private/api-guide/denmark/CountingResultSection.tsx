import { useEffect } from "react";
import { Callout, CodeBlock, InlineCode, MethodBadge, PageHeader, SectionHeading, Table } from "@/components/ui";
import { useMarket } from "../market";

const ROUTE = "/api/cash-mgmt/dk/bag-counting-results";

const params: { name: string; required: string; description: string }[] = [
  { name: "fromDate", required: "Yes", description: "ISO-8601 UTC start of the window (see dateType)." },
  { name: "toDate", required: "Yes", description: "ISO-8601 UTC end of the window. The window may not exceed 92 days." },
  { name: "dateType", required: "No", description: "Which date the window applies to: lastUpdated (default), registered or counted. Results are sorted newest first on that date. A bag that is not counted yet is never returned for dateType=counted." },
  { name: "status", required: "No", description: "One of the status values listed below." },
  { name: "customerRefNumber", required: "No", description: "Only results for this branch. A value that is not yours returns an empty list, never an error." },
  { name: "pageSize", required: "No", description: "1–500, default 100." },
  { name: "continuationToken", required: "No", description: "The token from the previous page. Pass the same other parameters." },
];

const fields: { field: string; description: string }[] = [
  { field: "recordId", description: "NOKAS's identifier for the record. Quote it when contacting support. Bag numbers can be reused, so do not treat bagNumber as unique." },
  { field: "bagNumber", description: "The bag number." },
  { field: "customerRefNumber", description: "The branch the bag belongs to." },
  { field: "status", description: "Where the bag has reached. See the status table below." },
  { field: "currencyType", description: "Always DKK." },
  { field: "declaredAmount", description: "Declared at registration. Null if not available." },
  { field: "countedAmount", description: "Counted by NOKAS. Null until counted." },
  { field: "deviationAmount", description: "Counted minus declared. Null until both exist." },
  { field: "registeredAt", description: "When the bag was registered." },
  { field: "countedAt", description: "The day the bag was counted. Null until counted." },
  { field: "lastUpdatedAt", description: "When the record last changed." },
];

const listResponse = `{
  "items": [
    {
      "bagNumber": "420019800012",
      "customerRefNumber": "4200000198",
      "status": "counted",
      "currencyType": "DKK",
      "declaredAmount": 700.00,
      "countedAmount": 690.00,
      "deviationAmount": -10.00,
      "registeredAt": "2026-09-21T14:24:01Z",
      "countedAt": "2026-09-21T00:00:00Z",
      "lastUpdatedAt": "2026-09-21T14:32:07Z",
      "recordId": "NOC0430525"
    }
  ],
  "pagination": { "pageSize": 100, "count": 1, "hasMore": false, "continuationToken": null },
  "dataAsOf": "2026-09-18T09:30:00Z",
  "correlationId": "bc414e2a-ec7d-4c8b-8dac-a941098e6ff9"
}`;

export default function DenmarkCountingResultSection() {
  const market = useMarket();

  useEffect(() => {
    document.title = "API Guide | Denmark | Counting Result";
  }, []);

  return (
    <section className="space-y-8">
      <PageHeader
        eyebrow="API Guide · 🇩🇰 Denmark"
        title="Counting Result"
        lead="Retrieve the outcome of your Danish bags: what you declared, what NOKAS counted, and the difference. Results are refreshed about every 10 minutes."
      />

      <Callout tone="info" title="Dates cover whole days">
        A <InlineCode>toDate</InlineCode> with no time covers that entire day, so{" "}
        <InlineCode>toDate=2026-09-23</InlineCode> includes everything up to midnight. Pass an
        explicit time (<InlineCode>2026-09-23T12:00:00Z</InlineCode>) if you need an exact cut-off.
      </Callout>

      <Callout tone="info" title="When do my bags appear?">
        A bag you register appears here within about 10 minutes, and any later change to it
        appears just as quickly. You only ever see bags registered by your own client.
      </Callout>

      <div className="space-y-4">
        <SectionHeading id="access" icon="🔐">
          Access
        </SectionHeading>
        <p className="text-sm leading-relaxed text-gray-600">
          Requires the app role <InlineCode>{market.roles.countingResult}</InlineCode> on your client
          application, separate from the registration role. Without it you receive{" "}
          <InlineCode>403</InlineCode>. You only ever see your own bags.
        </p>
      </div>

      <div className="space-y-4">
        <SectionHeading id="list" icon="📅">
          List counting results
        </SectionHeading>
        <div className="flex items-center gap-2">
          <MethodBadge method="GET" />
          <code className="text-sm">{ROUTE}</code>
        </div>
        <Table headers={["Parameter", "Required", "Description"]}>
          {params.map((p) => (
            <tr key={p.name}>
              <td className="whitespace-nowrap px-3 py-2 font-mono font-medium text-gray-900">{p.name}</td>
              <td className="px-3 py-2 text-gray-600">{p.required}</td>
              <td className="px-3 py-2 text-gray-600">{p.description}</td>
            </tr>
          ))}
        </Table>
        <CodeBlock label="Response 200" code={listResponse} />
        <p className="text-sm leading-relaxed text-gray-600">
          <strong>Paging:</strong> while <InlineCode>hasMore</InlineCode> is true, call again with the
          returned <InlineCode>continuationToken</InlineCode> and the same other parameters. A token used
          with different parameters is rejected with <InlineCode>400 INVALID_CONTINUATION_TOKEN</InlineCode>.
        </p>
        <p className="text-sm leading-relaxed text-gray-600">
          <strong>Freshness:</strong> <InlineCode>dataAsOf</InlineCode> is when the results were last refreshed.
          If it is much older than 10 minutes, treat the data as out of date.
        </p>
      </div>

      <div className="space-y-4">
        <SectionHeading id="single" icon="🔎">
          Single bag
        </SectionHeading>
        <div className="flex items-center gap-2">
          <MethodBadge method="GET" />
          <code className="text-sm">{ROUTE}/{"{bagNumber}"}</code>
        </div>
        <p className="text-sm leading-relaxed text-gray-600">
          Returns the most recently updated record for that bag number, or{" "}
          <InlineCode>404 NOT_FOUND</InlineCode>.
        </p>
      </div>

      <div className="space-y-4">
        <SectionHeading id="statuses" icon="🏷️">
          Status values
        </SectionHeading>
        <Table headers={["Status", "Meaning"]}>
          {[
            ["registered", "Registered with NOKAS. Nothing has happened to the bag yet."],
            ["picked-up", "Collected from you and on its way to NOKAS."],
            ["in-progress", "With NOKAS, not yet counted."],
            ["counted", "Counted. countedAmount and deviationAmount are meaningful from here on."],
            ["cancelled", "The bag was cancelled."],
            ["error", "Something went wrong with this bag. Contact NOKAS with the recordId."],
            ["unknown", "A status this API does not recognise yet. Rare; treat it as in progress."],
          ].map(([value, meaning]) => (
            <tr key={value}>
              <td className="whitespace-nowrap px-3 py-2 font-mono font-medium text-gray-900">{value}</td>
              <td className="px-3 py-2 text-gray-600">{meaning}</td>
            </tr>
          ))}
        </Table>
        <p className="text-sm leading-relaxed text-gray-600">
          A bag normally moves <InlineCode>registered</InlineCode> →{" "}
          <InlineCode>picked-up</InlineCode> → <InlineCode>in-progress</InlineCode> →{" "}
          <InlineCode>counted</InlineCode>, but it can skip steps between two of your calls.
        </p>
      </div>

      <div className="space-y-4">
        <SectionHeading id="fields" icon="📋">
          Result fields
        </SectionHeading>
        <Table headers={["Field", "Description"]}>
          {fields.map((f) => (
            <tr key={f.field}>
              <td className="whitespace-nowrap px-3 py-2 font-mono font-medium text-gray-900">{f.field}</td>
              <td className="px-3 py-2 text-gray-600">{f.description}</td>
            </tr>
          ))}
        </Table>
        <p className="text-sm leading-relaxed text-gray-600">
          Amounts are in <strong>DKK</strong> with decimals. Errors use the same shape as everywhere else,
          and every response carries a <InlineCode>correlationId</InlineCode>.
        </p>
      </div>
    </section>
  );
}
