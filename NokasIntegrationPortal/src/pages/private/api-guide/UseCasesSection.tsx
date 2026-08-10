import { useEffect } from "react";
import { Badge, CodeBlock, EnvUrlList, JumpNav, MethodBadge, PageHeader, SectionHeading, type HttpMethod } from "@/components/ui";
import { API_ENVIRONMENTS, TEST_ENV } from "@/config/environments";

/** Worked examples use the Test host; the base URLs for both environments are listed in the header. */
const BASE = TEST_ENV.baseUrl;

const jumpItems = [
  { id: "register", label: "Register" },
  { id: "retrieve", label: "Retrieve" },
  { id: "cancel", label: "Cancel" },
  { id: "counting", label: "Counting result" },
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

export default function UseCasesSection() {
  useEffect(() => {
    document.title = "API Guide | Use Cases";
  }, []);

  return (
    <section className="space-y-10">
      <PageHeader
        eyebrow="API Guide"
        title="Use cases & worked examples"
        lead="End-to-end examples showing a bag through its whole life — registered, retrieved, cancelled, and finally counted."
      >
        <div className="rounded-lg border border-accent-200 bg-accent-50 p-3">
          <EnvUrlList
            title="Base URL"
            entries={API_ENVIRONMENTS.map((env) => ({ label: env.label, url: env.baseUrl }))}
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
    "registrationDate": "2025-08-14",
    "referenceStatement": "kamal",
    "turnOverDate": "2025-08-14",
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
  "bagRegistration": {
    "customerRefNumber": "5790002334764",
    "registrationType": "NOK",
    "bagNumber": "34567895",
    "registrationDate": "2025-08-14",
    "totalOrderAmount": "2500"
  }
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

      {/* ---------------- 4. Counting result ---------------- */}
      <UseCase
        id="counting"
        method="GET"
        path="/api/cash-mgmt/bag-countingresult/{bagNumber}"
        title="4. Retrieve the counting result"
        summary="Returns the counting outcome. The shape of the response depends on how far the bag has progressed."
      >
        <div className="space-y-5">
          <div>
            <Badge tone="gray">Case 1 — registered, not yet counted</Badge>
            <CodeBlock
              label="200 OK"
              code={`{
  "customerRefNumber": "5790002334764",
  "bagNumber": "34567897",
  "description": "Bag not yet counted.",
  "bagStatus": "registered",
  "bagLifecycleId": "f821de8a-583d-47bb-8e41-886c9d08a11c",
  "requestCorrelationId": "345ff647-b06f-4725-9ec7-8e6dfce18fbf",
  "declaredAmount": 2500.0,
  "actualCountedAmount": null,
  "deviationAmount": null,
  "countedDate": null
}`}
            />
          </div>

          <div>
            <Badge tone="accent">Case 2 — picked up</Badge>
            <CodeBlock
              label="200 OK"
              code={`{
  "customerRefNumber": "5790002334764",
  "bagNumber": "34567896",
  "description": "Bag not yet counted.",
  "bagStatus": "picked-up",
  "bagLifecycleId": "5a75c602-f312-4510-af6f-1e5cb3b1f5eb",
  "requestCorrelationId": "345ff647-b06f-4725-9ec7-8e6dfce18fbf",
  "declaredAmount": 2500.0,
  "actualCountedAmount": null,
  "deviationAmount": null,
  "countedDate": null
}`}
            />
          </div>

          <div>
            <Badge tone="green">Case 3 — counted, amounts match</Badge>
            <CodeBlock
              label="200 OK"
              code={`{
  "customerRefNumber": "5790002334764",
  "bagNumber": "34567896",
  "description": "Bag counting result retrieved successfully.",
  "bagStatus": "counted",
  "bagLifecycleId": "5a75c602-f312-4510-af6f-1e5cb3b1f5eb",
  "requestCorrelationId": "345ff647-b06f-4725-9ec7-8e6dfce18fbf",
  "declaredAmount": 2500.0,
  "actualCountedAmount": 2500.0,
  "deviationAmount": 0.0,
  "countedDate": "2025-11-11T12:31:00"
}`}
            />
          </div>

          <div>
            <Badge tone="amber">Case 4 — counted, with a deviation</Badge>
            <CodeBlock
              label="200 OK"
              code={`{
  "customerRefNumber": "5790002334764",
  "bagNumber": "34567896",
  "description": "Bag counting result retrieved successfully.",
  "bagStatus": "counted",
  "bagLifecycleId": "5a75c602-f312-4510-af6f-1e5cb3b1f5eb",
  "requestCorrelationId": "345ff647-b06f-4725-9ec7-8e6dfce18fbf",
  "declaredAmount": 2500.0,
  "actualCountedAmount": 2000.0,
  "deviationAmount": -500.0,
  "countedDate": "2025-11-11T12:32:00"
}`}
            />
          </div>
        </div>
      </UseCase>
    </section>
  );
}
