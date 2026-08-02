import { useEffect, useState, type ReactNode } from "react";
import { Link } from "react-router-dom";

const BASE = "https://api-test.nokas.com/api/cash-mgmt";

type HttpMethod = "POST" | "GET" | "DELETE";

/* ------------------------------------------------------------------ */
/*  Small building blocks                                              */
/* ------------------------------------------------------------------ */

const methodStyles: Record<HttpMethod, string> = {
  POST: "bg-emerald-50 text-emerald-700 ring-emerald-600/20",
  GET: "bg-blue-50 text-blue-700 ring-blue-600/20",
  DELETE: "bg-rose-50 text-rose-700 ring-rose-600/20",
};

function MethodBadge({ method }: { method: HttpMethod }) {
  return (
    <span
      className={`inline-flex shrink-0 items-center rounded-md px-2 py-1 text-xs font-bold tracking-wide ring-1 ring-inset ${methodStyles[method]}`}
    >
      {method}
    </span>
  );
}

function CopyButton({ value, label }: { value: string; label: string }) {
  const [copied, setCopied] = useState(false);

  async function handleCopy() {
    try {
      await navigator.clipboard.writeText(value);
      setCopied(true);
      window.setTimeout(() => setCopied(false), 1800);
    } catch {
      // Clipboard needs a secure context; failing silently is better than a broken UI.
    }
  }

  return (
    <button
      type="button"
      onClick={handleCopy}
      aria-label={copied ? `${label} copied` : `Copy ${label}`}
      title={copied ? "Copied" : "Copy"}
      className="inline-flex items-center gap-1.5 rounded-md px-2 py-1 text-xs font-medium text-gray-300 transition hover:bg-white/10 hover:text-white focus:outline-none focus:ring-2 focus:ring-blue-400"
    >
      {copied ? (
        <>
          <svg viewBox="0 0 20 20" fill="currentColor" className="h-3.5 w-3.5 text-emerald-400">
            <path
              fillRule="evenodd"
              d="M16.7 5.3a1 1 0 0 1 0 1.4l-7.5 7.5a1 1 0 0 1-1.4 0l-3.5-3.5a1 1 0 1 1 1.4-1.4l2.8 2.8 6.8-6.8a1 1 0 0 1 1.4 0Z"
              clipRule="evenodd"
            />
          </svg>
          <span className="text-emerald-400">Copied</span>
        </>
      ) : (
        <>
          <svg viewBox="0 0 20 20" fill="currentColor" className="h-3.5 w-3.5">
            <path d="M7 3a2 2 0 0 0-2 2v8a2 2 0 0 0 2 2h6a2 2 0 0 0 2-2V5a2 2 0 0 0-2-2H7Z" />
            <path d="M3 7a2 2 0 0 1 1-1.73V15a2 2 0 0 0 2 2h7.73A2 2 0 0 1 12 18H6a3 3 0 0 1-3-3V7Z" />
          </svg>
          <span>Copy</span>
        </>
      )}
    </button>
  );
}

function CodeBlock({ label, code }: { label: string; code: string }) {
  return (
    <figure className="mt-2 overflow-hidden rounded-lg border border-slate-700 bg-slate-900 shadow-sm">
      <figcaption className="flex items-center justify-between border-b border-slate-700 bg-slate-800/80 px-3 py-1.5">
        <span className="text-[11px] font-semibold uppercase tracking-wider text-slate-400">
          {label}
        </span>
        <CopyButton value={code} label={label} />
      </figcaption>
      <div className="overflow-x-auto">
        <pre className="p-3 font-mono text-xs leading-relaxed text-slate-100">{code}</pre>
      </div>
    </figure>
  );
}

const requiredHeaders: { name: string; value: string; note: string }[] = [
  { name: "Authorization", value: "Bearer <token>", note: "OAuth2 client-credentials token" },
  { name: "Content-Type", value: "application/json", note: "JSON and XML are both supported" },
  { name: "X-Correlation-ID", value: "<uuid>", note: "Echoed back for end-to-end tracing" },
];

function HeadersTable({ omit = [] }: { omit?: string[] }) {
  const rows = requiredHeaders.filter((h) => !omit.includes(h.name));

  return (
    <div className="mt-2 overflow-x-auto rounded-lg border border-gray-200">
      <table className="min-w-full divide-y divide-gray-200 text-left text-xs">
        <thead className="bg-gray-50">
          <tr>
            <th scope="col" className="px-3 py-2 font-semibold text-gray-700">Header</th>
            <th scope="col" className="px-3 py-2 font-semibold text-gray-700">Value</th>
            <th scope="col" className="px-3 py-2 font-semibold text-gray-700">Notes</th>
          </tr>
        </thead>
        <tbody className="divide-y divide-gray-100 bg-white">
          {rows.map((h) => (
            <tr key={h.name}>
              <td className="whitespace-nowrap px-3 py-2 font-mono font-medium text-gray-900">{h.name}</td>
              <td className="whitespace-nowrap px-3 py-2 font-mono text-emerald-700">{h.value}</td>
              <td className="px-3 py-2 text-gray-600">{h.note}</td>
            </tr>
          ))}
        </tbody>
      </table>
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
  const fullUrl = `${BASE}${path}`;

  return (
    <article className="scroll-mt-6 overflow-hidden rounded-xl border border-gray-200 bg-white shadow-sm transition hover:shadow-md">
      <header className="border-b border-gray-200 bg-gray-50/70 px-4 py-3">
        <div className="flex flex-wrap items-center gap-2">
          <MethodBadge method={method} />
          <code className="break-all font-mono text-sm font-semibold text-gray-900">
            /api/cash-mgmt{path}
          </code>
        </div>
        <h4 className="mt-1.5 text-sm font-semibold text-gray-800">{title}</h4>
        <p className="mt-0.5 text-xs text-gray-600">{summary}</p>
      </header>

      <div className="space-y-4 p-4">
        <div>
          <p className="text-xs font-semibold uppercase tracking-wider text-gray-500">Full URL</p>
          <div className="mt-1 flex items-center gap-2 overflow-hidden rounded-md border border-gray-200 bg-gray-50 px-2 py-1.5">
            <code className="flex-1 break-all font-mono text-xs text-gray-800">{fullUrl}</code>
            <span className="shrink-0 rounded bg-slate-800">
              <CopyButton value={fullUrl} label="URL" />
            </span>
          </div>
        </div>
        {children}
      </div>
    </article>
  );
}

function SectionHeading({ id, icon, children }: { id: string; icon: string; children: ReactNode }) {
  return (
    <h3
      id={id}
      className="scroll-mt-6 border-b border-gray-200 pb-2 text-lg font-bold text-gray-900"
    >
      <span className="mr-2">{icon}</span>
      {children}
    </h3>
  );
}

/* ------------------------------------------------------------------ */
/*  Page                                                               */
/* ------------------------------------------------------------------ */

const statusCodes: { code: string; meaning: string; tone: string }[] = [
  { code: "200 OK", meaning: "Request succeeded and the payload is returned.", tone: "bg-emerald-50 text-emerald-700 ring-emerald-600/20" },
  { code: "201 Created", meaning: "Bag registered. A Location header points to the new record.", tone: "bg-emerald-50 text-emerald-700 ring-emerald-600/20" },
  { code: "400 Bad Request", meaning: "Validation failed. The response lists each field error.", tone: "bg-amber-50 text-amber-700 ring-amber-600/20" },
  { code: "401 Unauthorized", meaning: "Token missing, expired, or not valid for this API.", tone: "bg-amber-50 text-amber-700 ring-amber-600/20" },
  { code: "404 Not Found", meaning: "No bag with that number exists for your account.", tone: "bg-amber-50 text-amber-700 ring-amber-600/20" },
  { code: "409 Conflict", meaning: "The bag number is already registered and cannot be reused.", tone: "bg-amber-50 text-amber-700 ring-amber-600/20" },
  { code: "503 Service Unavailable", meaning: "A downstream system is temporarily unreachable. Retry.", tone: "bg-rose-50 text-rose-700 ring-rose-600/20" },
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

        <div className="flex flex-wrap items-center gap-2 rounded-lg border border-blue-200 bg-blue-50 px-3 py-2">
          <span className="text-xs font-semibold uppercase tracking-wider text-blue-800">
            Base URL
          </span>
          <code className="break-all font-mono text-xs text-blue-900">{BASE}</code>
          <span className="ml-auto rounded bg-slate-800">
            <CopyButton value={BASE} label="base URL" />
          </span>
        </div>

        {/* On this page */}
        <nav aria-label="On this page" className="flex flex-wrap gap-2 pt-1">
          {onThisPage.map((item) => (
            <a
              key={item.id}
              href={`#${item.id}`}
              className="rounded-full border border-gray-300 bg-white px-3 py-1 text-xs font-medium text-gray-700 transition hover:border-blue-400 hover:bg-blue-50 hover:text-blue-700"
            >
              {item.label}
            </a>
          ))}
        </nav>
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

        <div className="overflow-x-auto rounded-lg border border-gray-200">
          <table className="min-w-full divide-y divide-gray-200 text-left text-xs">
            <thead className="bg-gray-50">
              <tr>
                <th scope="col" className="px-3 py-2 font-semibold text-gray-700">Code</th>
                <th scope="col" className="px-3 py-2 font-semibold text-gray-700">Meaning</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100 bg-white">
              {statusCodes.map((s) => (
                <tr key={s.code}>
                  <td className="whitespace-nowrap px-3 py-2">
                    <span
                      className={`inline-flex items-center rounded-md px-2 py-0.5 font-mono text-[11px] font-semibold ring-1 ring-inset ${s.tone}`}
                    >
                      {s.code}
                    </span>
                  </td>
                  <td className="px-3 py-2 text-gray-700">{s.meaning}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>

        <p className="text-xs text-gray-600">
          Every error response carries the same shape, including the{" "}
          <code className="rounded bg-gray-100 px-1 py-0.5 font-mono">correlationId</code> for the
          failed request. See the{" "}
          <Link
            to="/portal/api-guide/errors"
            className="font-semibold text-blue-700 underline-offset-2 hover:underline"
          >
            Errors
          </Link>{" "}
          section for the full catalogue.
        </p>
      </div>
    </section>
  );
}
