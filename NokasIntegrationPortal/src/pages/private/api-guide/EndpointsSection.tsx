import { useEffect, type ReactNode } from "react";
import { Link } from "react-router-dom";
import {
  Badge,
  CodeBlock,
  EnvUrlList,
  JumpNav,
  MethodBadge,
  SectionHeading,
  Table,
  type HttpMethod,
} from "@/components/ui";
import { API_ENVIRONMENTS, RATE_LIMIT } from "@/config/environments";

/** Route suffix shared by both environments (see API_ENVIRONMENTS for the full URLs). */
const ROUTE_PREFIX = "/api/cash-mgmt";

const requiredHeaders: { name: string; value: string; note: string }[] = [
  { name: "Authorization", value: "Bearer <token>", note: "OAuth2 client-credentials token" },
  { name: "Content-Type", value: "application/json", note: "JSON and XML are both supported" },
  { name: "X-Correlation-ID", value: "<uuid>", note: "Echoed back for end-to-end tracing" },
];

function HeadersTable({ omit = [] }: { omit?: string[] }) {
  const rows = requiredHeaders.filter((h) => !omit.includes(h.name));

  return (
    <div className="mt-2">
      <Table headers={["Header", "Value", "Notes"]}>
        {rows.map((h) => (
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
        {/* Both environments are listed explicitly so there is no ambiguity. */}
        <EnvUrlList
          title="Full URL"
          entries={API_ENVIRONMENTS.map((env) => ({
            label: env.label,
            url: `${env.baseUrl}${path}`,
          }))}
        />
        {children}
      </div>
    </article>
  );
}

/* ------------------------------------------------------------------ */
/*  Page                                                               */
/* ------------------------------------------------------------------ */

const statusCodes: { code: string; meaning: string; tone: "green" | "amber" | "red" }[] = [
  { code: "200 OK", meaning: "Request succeeded and the payload is returned.", tone: "green" },
  { code: "201 Created", meaning: "Bag registered. A Location header points to the new record.", tone: "green" },
  { code: "400 Bad Request", meaning: "Validation failed. The response lists each field error.", tone: "amber" },
  { code: "401 Unauthorized", meaning: "Token missing, expired, or not valid for this API.", tone: "amber" },
  { code: "404 Not Found", meaning: "No bag with that number exists for your account.", tone: "amber" },
  { code: "409 Conflict", meaning: "The bag number is already registered and cannot be reused.", tone: "amber" },
  { code: "429 Too Many Requests", meaning: `Rate limit exceeded (${RATE_LIMIT} per application).`, tone: "red" },
  { code: "503 Service Unavailable", meaning: "A downstream system is temporarily unreachable. Retry.", tone: "red" },
];

const onThisPage = [
  { id: "bag-registration", label: "Bag Registration" },
  { id: "counting-result", label: "Counting Result" },
  { id: "status-codes", label: "Status Codes" },
];

export default function EndpointsSection() {
  useEffect(() => {
    document.title = "API Guide | Endpoints";
  }, []);

  return (
    <section className="space-y-8">
      {/* ---------------- Page header ---------------- */}
      <header className="space-y-3">
        <h2 className="text-2xl font-bold tracking-tight text-gray-900">API Endpoints</h2>
        <p className="max-w-3xl text-sm leading-relaxed text-gray-600">
          Every endpoint below is scoped to your own account — you only ever see bags registered
          under your client credentials. All requests are authenticated with an OAuth2 bearer token
          and traced with a correlation ID.
        </p>

        <div className="rounded-lg border border-accent-200 bg-accent-50 p-3">
          <EnvUrlList
            title="Base URL"
            entries={API_ENVIRONMENTS.map((env) => ({ label: env.label, url: env.baseUrl }))}
          />
          <p className="mt-2 text-xs text-accent-900/80">
            Every path below is relative to these. Use <strong>Test</strong> while integrating and
            switch to <strong>Production</strong> only once NOKAS has issued your production
            credentials — they are separate B2C tenants, so test credentials will not work against
            production.
          </p>
        </div>

        <JumpNav items={onThisPage} />
      </header>

      {/* ---------------- Correlation callout ---------------- */}
      <aside className="rounded-lg border-l-4 border-blue-500 bg-blue-50/60 p-4">
        <h3 className="text-sm font-semibold text-blue-900">Request tracking</h3>
        <p className="mt-1 text-xs leading-relaxed text-blue-900/80">
          Every request <strong>must</strong> include a unique <code className="rounded bg-blue-100 px-1 py-0.5 font-mono">X-Correlation-ID</code> header.
          It is returned as <code className="rounded bg-blue-100 px-1 py-0.5 font-mono">requestCorrelationId</code>, alongside a{" "}
          <code className="rounded bg-blue-100 px-1 py-0.5 font-mono">bagLifecycleId</code> that identifies the cash bag for its whole lifetime.
        </p>
        <Link
          to="/portal/api-guide/correlation"
          className="mt-2 inline-flex items-center gap-1 text-xs font-semibold text-blue-700 underline-offset-2 hover:underline"
        >
          Read the correlation guide
          <span aria-hidden="true">→</span>
        </Link>
      </aside>

      {/* ---------------- Bag Registration ---------------- */}
      <div className="space-y-4">
        <SectionHeading id="bag-registration" icon="📦">
          Bag Registration API
        </SectionHeading>

        <EndpointCard
          method="POST"
          path="/bag-registration"
          title="Register a cash bag"
          summary="Declare a bag before pickup. The bag number must not already be registered."
        >
          <div>
            <p className="text-xs font-semibold uppercase tracking-wider text-gray-500">
              Required headers
            </p>
            <HeadersTable />
          </div>

          <CodeBlock
            label="Request body"
            code={`{
  "bagRegistration": {
    "customerRefNumber": "5790002334764",
    "registrationType": "NOK",
    "messageId": "938D35D2-F8C1-4EAE-BB4D-0797E29DBB6A",
    "bagNumber": "2033331011",
    "registrationDate": "2025-08-14",
    "referenceStatement": "kamal",
    "turnOverDate": "2025-08-14",
    "registeredNotes": "1000",
    "registeredCoins": "0",
    "registeredChecks": "0",
    "totalOrderAmount": "10000",
    "bagItemsLC": {
      "seddel1000": "1000",
      "seddel500": "0",
      "seddel200": "0",
      "seddel100": "0",
      "seddel50": "0"
    }
  }
}`}
          />

          <CodeBlock
            label="201 Created"
            code={`{
  "customerRefNumber": "5790002334764",
  "bagNumber": "2033331011",
  "description": "Bag Registered Successfully",
  "registrationStatus": "In-Progress",
  "bagLifecycleId": "287d119a-af9b-4d6a-81f4-23d1c0c4a4b6",
  "requestCorrelationId": "5e3ff647-b06f-4725-9ec7-8e6dfce18fbG"
}`}
          />
        </EndpointCard>

        <EndpointCard
          method="GET"
          path="/bag-registration/{bagNumber}"
          title="Retrieve a cash bag"
          summary="Fetch a previously registered bag and its current registration status."
        >
          <div>
            <p className="text-xs font-semibold uppercase tracking-wider text-gray-500">
              Required headers
            </p>
            <HeadersTable omit={["Content-Type"]} />
          </div>

          <CodeBlock
            label="200 OK"
            code={`{
  "bagRegistration": {
    "customerRefNumber": "123451",
    "registrationType": "NOK",
    "messageId": "938D35D2-F8C1-4EAE-BB4D-0797E29DBB6A",
    "bagNumber": "394857349859",
    "registrationDate": "2025-08-14",
    "totalOrderAmount": "10000"
  }
}`}
          />
        </EndpointCard>

        <EndpointCard
          method="DELETE"
          path="/bag-registration/{bagNumber}"
          title="Cancel a cash bag"
          summary="Cancel a registration. Only possible before the bag has been collected for transport."
        >
          <div>
            <p className="text-xs font-semibold uppercase tracking-wider text-gray-500">
              Required headers
            </p>
            <HeadersTable omit={["Content-Type"]} />
          </div>

          <CodeBlock
            label="200 OK"
            code={`{
  "customerRefNumber": "5790002334764",
  "bagNumber": "2033331011",
  "description": "Bag Cancelled Successfully.",
  "registrationStatus": "Deleted",
  "bagLifecycleId": "287d119a-af9b-4d6a-81f4-23d1c0c4a4b6",
  "requestCorrelationId": "5e3ff647-b06f-4725-9ec7-8e6dfce18fbf"
}`}
          />
        </EndpointCard>
      </div>

      {/* ---------------- Counting Result ---------------- */}
      <div className="space-y-4">
        <SectionHeading id="counting-result" icon="📊">
          Counting Result API
        </SectionHeading>

        <p className="max-w-3xl text-sm leading-relaxed text-gray-600">
          Retrieve the counted outcome for a previously registered bag. Results become available
          once NOKAS operations have verified and counted the contents.
        </p>

        <EndpointCard
          method="GET"
          path="/bag-countingresult/{bagNumber}"
          title="Retrieve a counting result"
          summary="Returns declared vs counted amounts and any deviation between them."
        >
          <div>
            <p className="text-xs font-semibold uppercase tracking-wider text-gray-500">
              Required headers
            </p>
            <HeadersTable omit={["Content-Type"]} />
          </div>

          <CodeBlock
            label="200 OK"
            code={`{
  "customerRefNumber": "5790002334764",
  "bagNumber": "34567891",
  "description": "Bag counting result retrieved successfully.",
  "bagStatus": "counted",
  "bagLifecycleId": "1f4fccf7-8b4d-4c70-a83e-d12626e8a5e5",
  "requestCorrelationId": "5e3ff647-b06f-4725-9ec7-8e6dfce18fbf",
  "declaredAmount": 2500.0,
  "actualCountedAmount": 2500.0,
  "deviationAmount": 0.0,
  "countedDate": "2025-11-11T11:59:00"
}`}
          />

          <p className="rounded-md bg-amber-50 px-3 py-2 text-xs text-amber-900 ring-1 ring-inset ring-amber-600/20">
            <strong>Note:</strong> if the bag has not been counted yet, the response still returns
            <code className="mx-1 rounded bg-amber-100 px-1 py-0.5 font-mono">200 OK</code>
            with <code className="mx-1 rounded bg-amber-100 px-1 py-0.5 font-mono">bagStatus</code>
            reflecting its current stage, and the counted fields omitted.
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
            to="/portal/api-guide/errors"
            className="font-semibold text-brand-700 underline-offset-2 hover:underline"
          >
            Errors
          </Link>{" "}
          section for the full catalogue.
        </p>
      </div>
    </section>
  );
}
