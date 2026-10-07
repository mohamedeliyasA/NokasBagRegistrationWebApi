import { useEffect } from "react";
import { Badge, Callout, CodeBlock, InlineCode, JumpNav, PageHeader, SectionHeading, Table } from "@/components/ui";
import { useMarket } from "./market";

const jumpItems = [
  { id: "business", label: "Business logic" },
  { id: "cancellation", label: "Cancellation" },
  { id: "counting", label: "Counting result" },
  { id: "auth", label: "Authentication" },
  { id: "rate", label: "Rate limiting" },
  { id: "codes", label: "Status codes" },
];

const statusCodes: { code: string; tone: "green" | "amber" | "red"; body: string }[] = [
  { code: "200 OK", tone: "green", body: "Request was successful." },
  { code: "201 Created", tone: "green", body: "Bag registered; a Location header points to the new record." },
  { code: "400 Bad Request", tone: "amber", body: "Invalid or missing data fields." },
  { code: "401 Unauthorized", tone: "amber", body: "Invalid or missing access token." },
  { code: "404 Not Found", tone: "amber", body: "Requested bag or resource not found." },
  { code: "409 Conflict", tone: "amber", body: "Duplicate bag, or a logical constraint was violated." },
  { code: "429 Too Many Requests", tone: "red", body: "Rate limit exceeded." },
  { code: "500 Internal Server Error", tone: "red", body: "Unexpected server-side failure." },
  { code: "503 Service Unavailable", tone: "red", body: "A downstream system is temporarily unreachable." },
];

export default function ErrorsSection() {
  const market = useMarket();

  useEffect(() => {
    document.title = `API Guide | ${market.label} | Errors`;
  }, [market.label]);

  return (
    <section className="space-y-8">
      <PageHeader
        eyebrow="API Guide"
        title="Error handling & logging"
        lead="Every error returns a meaningful HTTP status together with a correlation ID, so a failure can always be traced back to a specific request."
      >
        <JumpNav items={jumpItems} />
      </PageHeader>

      {/* ---------------- Business logic ---------------- */}
      <div className="space-y-4">
        <SectionHeading id="business" icon="📦">
          Business logic errors
        </SectionHeading>

        <p className="text-sm leading-relaxed text-gray-600">
          Domain-level problems — duplicate bags, missing data, invalid states — return{" "}
          <InlineCode>400</InlineCode>, <InlineCode>404</InlineCode> or <InlineCode>409</InlineCode>.
        </p>

        <CodeBlock
          label="409 Conflict — bag already exists"
          code={`{
  "customerRefNumber": "5790002334764",
  "bagNumber": "34567890",
  "description": "Bag 34567890 already exists and cannot be re-registered.",
  "registrationStatus": "AlreadyExists",
  "bagLifecycleId": "171349e2-6aa3-424e-a708-20cc94f03ae2",
  "requestCorrelationId": "5e3ff647-b06f-4725-9ec7-8e6dfce18fbG"
}`}
        />

        <CodeBlock
          label="400 Bad Request — missing totalOrderAmount"
          code={`{
  "errors": {
    "BagRegistration.TotalOrderAmount": [
      "totalOrderAmount is required when registrationType is NOK."
    ]
  },
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "traceId": "00-2791ac1a7f81eaeb75c20b102e4e694d-2bdcf9c47fceb5b5-00"
}`}
        />

        <CodeBlock
          label="400 Bad Request — missing registrationType"
          code={`{
  "errors": {
    "BagRegistration.RegistrationType": [
      "The RegistrationType field is required."
    ]
  },
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "traceId": "00-cff96ea89b5e9c73150f403434027a85-f021792aec19711c-00"
}`}
        />

        <CodeBlock
          label="404 Not Found — bag does not exist"
          code={`{
  "customerRefNumber": "NA",
  "bagNumber": "NA",
  "description": "No bag found with the BagNumber: 3456789234 for this client.",
  "registrationStatus": "NA",
  "bagLifecycleId": "NA",
  "requestCorrelationId": "5e3ff647-b06f-4725-9ec7-8e6dfce18fbf"
}`}
        />
      </div>

      {/* ---------------- Cancellation ---------------- */}
      <div className="space-y-4">
        <SectionHeading id="cancellation" icon="🚫">
          Cancellation errors
        </SectionHeading>

        <p className="text-sm leading-relaxed text-gray-600">
          Cancellation validates the bag's lifecycle stage. A bag can only be cancelled before it
          has been collected for transport.
        </p>

        <CodeBlock
          label="400 Bad Request — already processed"
          code={`{
  "customerRefNumber": "5790002334764",
  "bagNumber": "34567894",
  "description": "Bag already processed and cannot be cancelled at this stage.",
  "registrationStatus": "picked-up",
  "bagLifecycleId": "657bdf7b-e8cc-45be-92a1-d08032f5316f",
  "requestCorrelationId": "5e3ff647-b06f-4725-9ec7-8e6dfce18fbf"
}`}
        />

        <CodeBlock
          label="404 Not Found — bag not registered"
          code={`{
  "customerRefNumber": "NA",
  "bagNumber": "NA",
  "description": "No bag found with the BagNumber: 34567894456 for this client.",
  "registrationStatus": "NA",
  "bagLifecycleId": "NA",
  "requestCorrelationId": "5e3ff647-b06f-4725-9ec7-8e6dfce18fbf"
}`}
        />
      </div>

      {/* ---------------- Counting result ---------------- */}
      <div className="space-y-4">
        <SectionHeading id="counting" icon="📊">
          Counting result errors
        </SectionHeading>

        <p className="text-sm leading-relaxed text-gray-600">
          Requesting a counting result for a bag that does not exist, or is not registered to your
          account, returns <InlineCode>404 Not Found</InlineCode>.
        </p>

        <CodeBlock
          label="404 Not Found"
          code={`{
  "customerRefNumber": "NA",
  "bagNumber": "NA",
  "description": "No bag found with the BagNumber: 345678912334 for this client.",
  "registrationStatus": "NA",
  "bagLifecycleId": "NA",
  "requestCorrelationId": "5e3ff647-b06f-4725-9ec7-8e6dfce18fbf"
}`}
        />
      </div>

      {/* ---------------- Auth ---------------- */}
      <div className="space-y-4">
        <SectionHeading id="auth" icon="🔐">
          Authentication &amp; authorisation
        </SectionHeading>

        <p className="text-sm leading-relaxed text-gray-600">
          A missing, invalid or expired token returns <InlineCode>401 Unauthorized</InlineCode>.
          Refresh the token and send it in the <InlineCode>Authorization</InlineCode> header.
        </p>

        <CodeBlock
          label="401 Unauthorized"
          code={`{
  "statusCode": 401,
  "message": "Unauthorized"
}`}
        />
      </div>

      {/* ---------------- Rate limiting ---------------- */}
      <div className="space-y-4">
        <SectionHeading id="rate" icon="⏱️">
          Rate limiting
        </SectionHeading>

        <p className="text-sm leading-relaxed text-gray-600">
          To protect system stability the gateway enforces a per-application rate limit. Exceeding it
          returns <InlineCode>429 Too Many Requests</InlineCode>.
        </p>

        <Callout tone="info" title="Limit">
          The gateway allows <strong>{market.rateLimit}</strong> per application. Build your client to
          honour <InlineCode>429</InlineCode> responses with a backoff regardless — the limit may be
          lowered temporarily in the test environment during trials.
        </Callout>

        <CodeBlock
          label="429 Too Many Requests"
          code={`{
  "statusCode": 429,
  "message": "Rate limit is exceeded. Try again in 58 seconds."
}`}
        />
      </div>

      {/* ---------------- Status codes ---------------- */}
      <div className="space-y-4">
        <SectionHeading id="codes" icon="📶">
          HTTP status code overview
        </SectionHeading>

        <Table headers={["Code", "Description"]}>
          {statusCodes.map((s) => (
            <tr key={s.code}>
              <td className="whitespace-nowrap px-3 py-2">
                <Badge tone={s.tone}>{s.code}</Badge>
              </td>
              <td className="px-3 py-2 text-gray-700">{s.body}</td>
            </tr>
          ))}
        </Table>

        <Callout tone="info" title="Always capture the correlation ID">
          Every response includes <InlineCode>requestCorrelationId</InlineCode>. Log it on your side
          — when raising a support request it is the fastest way for NOKAS to find the exact call.
        </Callout>
      </div>
    </section>
  );
}
