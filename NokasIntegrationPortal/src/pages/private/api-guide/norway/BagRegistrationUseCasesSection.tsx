import { useEffect } from "react";
import { Link } from "react-router-dom";
import { CodeBlock, EnvUrlList, JumpNav, MethodBadge, PageHeader, SectionHeading, type HttpMethod } from "@/components/ui";
import { API_ENVIRONMENTS, TEST_ENV } from "@/config/environments";

/** Worked examples use the Test host; the base URLs for both environments are listed in the header. */
const BASE = TEST_ENV.baseUrl;

const jumpItems = [
  { id: "register", label: "Register" },
  { id: "retrieve", label: "Retrieve" },
  { id: "cancel", label: "Cancel" },
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

export default function BagRegistrationUseCasesSection() {
  useEffect(() => {
    document.title = "API Guide | Bag Registration Use Cases";
  }, []);

  return (
    <section className="space-y-10">
      <PageHeader
        eyebrow="Bag Registration API"
        title="Use cases & worked examples"
        lead="End-to-end examples showing a bag through registration, retrieval, and cancellation."
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

      {/* ---------------- 1. Register ---------------- */}
      <UseCase
        id="register"
        method="POST"
        path="/api/cash-mgmt/bag-registration"
        title="1. Register a bag"
        summary="Registers a new bag. Requires a valid customer reference number and registration type."
      >
        <CodeBlock
          label="Request body"
          code={`{
  "bagRegistration": {
    "customerRefNumber": "5790002334764",
    "registrationType": "NOK",
    "messageId": "938D35D2-F8C1-4EAE-BB4D-0797E29DBB6A",
    "bagNumber": "34567895",
    "registrationDate": "2026-08-14",
    "referenceStatement": "kamal",
    "turnOverDate": "2026-08-14",
    "registeredNotes": "2000",
    "registeredCoins": "500",
    "registeredChecks": "0",
    "totalOrderAmount": "2500",
    "bagItemsLC": {
      "seddel1000": "1000",
      "seddel500": "500",
      "seddel200": "400",
      "seddel100": "100",
      "seddel50": "0"
    }
  }
}`}
        />
        <CodeBlock
          label="201 Created"
          code={`{
  "customerRefNumber": "5790002334764",
  "bagNumber": "34567895",
  "description": "Bag Registered Successfully",
  "registrationStatus": "In-Progress",
  "bagLifecycleId": "02877729-a00f-4882-aa46-67bef0ee0182",
  "requestCorrelationId": "123f647-b06f-4725-9ec7-8e6dfce18fbG"
}`}
        />
      </UseCase>

      {/* ---------------- 2. Retrieve ---------------- */}
      <UseCase
        id="retrieve"
        method="GET"
        path="/api/cash-mgmt/bag-registration/{bagNumber}"
        title="2. Retrieve a registration"
        summary="Fetches a previously registered bag and its current registration status."
      >
        <CodeBlock label="Request" code={`GET ${BASE}/bag-registration/34567895`} />
        <CodeBlock
          label="200 OK"
          code={`{
  "customerRefNumber": "5790002334764",
  "bagNumber": "34567895",
  "description": "Bag Registration Found",
  "registrationStatus": "In-Progress",
  "bagLifecycleId": "02877729-a00f-4882-aa46-67bef0ee0182",
  "requestCorrelationId": "789f647-b06f-4725-9ec7-8e6dfce18fbf"
}`}
        />
      </UseCase>

      {/* ---------------- 3. Cancel ---------------- */}
      <UseCase
        id="cancel"
        method="DELETE"
        path="/api/cash-mgmt/bag-registration/{bagNumber}"
        title="3. Cancel a registration"
        summary="Cancels a registered bag before it has been collected for pickup or counting."
      >
        <CodeBlock label="Request" code={`DELETE ${BASE}/bag-registration/34567895`} />
        <CodeBlock
          label="200 OK"
          code={`{
  "customerRefNumber": "5790002334764",
  "bagNumber": "34567895",
  "description": "Bag Cancelled Successfully.",
  "registrationStatus": "Deleted",
  "bagLifecycleId": "02877729-a00f-4882-aa46-67bef0ee0182",
  "requestCorrelationId": "456ff647-b06f-4725-9ec7-8e6dfce18fbf"
}`}
        />
      </UseCase>

      {/* ---------------- Cross-link ---------------- */}
      <aside className="rounded-lg border-l-4 border-accent-500 bg-accent-50/60 p-4">
        <h3 className="text-sm font-semibold text-accent-900">Next: track what happens after pickup</h3>
        <p className="mt-1 text-xs leading-relaxed text-accent-900/80">
          Once a bag has been counted, its outcome is available through the Counting Result API.
        </p>
        <Link
          to="/portal/api-guide/norway/counting-result/use-cases"
          className="mt-2 inline-flex items-center gap-1 text-xs font-semibold text-accent-700 underline-offset-2 hover:underline"
        >
          See Counting Result use cases
          <span aria-hidden="true">→</span>
        </Link>
      </aside>
    </section>
  );
}
