import { useEffect } from "react";
import { Link } from "react-router-dom";
import { Callout, CodeBlock, InlineCode, JumpNav, SectionHeading, Table } from "@/components/ui";
import { TEST_ENV } from "@/config/environments";
import { useGuideLink, useMarket } from "../market";

const onThisPage = [
  { id: "first-call", label: "First call" },
  { id: "end-of-shift", label: "End of shift" },
  { id: "verify", label: "Retrieve" },
  { id: "mistakes", label: "Common mistakes" },
];

const tokenCall = `curl -X POST "${TEST_ENV.tokenUrl}" \\
  -H "Content-Type: application/x-www-form-urlencoded" \\
  -d "grant_type=client_credentials" \\
  -d "client_id=YOUR_CLIENT_ID" \\
  -d "client_secret=YOUR_CLIENT_SECRET" \\
  -d "scope=${TEST_ENV.scope}"`;

const registerCall = (baseUrl: string) => `curl -X POST "${baseUrl}/bag-registration" \\
  -H "Authorization: Bearer $TOKEN" \\
  -H "Content-Type: application/json" \\
  -H "X-Correlation-ID: $(uuidgen)" \\
  -d '{
    "bagRegistration": {
      "customerRefNumber": "5790000123456",
      "cashRegisterId": "POS-01",
      "messageId": "0f8fad5b-d9cb-469f-a165-70867728950e",
      "bagNumber": "3164696800",
      "registrationDate": "2026-08-17",
      "totalOrderAmount": "1750.00",
      "bagItemsLC": {
        "DKK_500": "3",
        "DKK_200": "1",
        "DKK_50": "1"
      }
    }
  }'`;

const retrieveCall = (baseUrl: string) => `curl "${baseUrl}/bag-registration/3164696800" \\
  -H "Authorization: Bearer $TOKEN"`;

const mistakes: { wrong: string; right: string; why: string }[] = [
  {
    wrong: `"totalOrderAmount": 888.50`,
    right: `"totalOrderAmount": "888.50"`,
    why: "Every value is a JSON string, including the numeric ones.",
  },
  {
    wrong: `"totalOrderAmount": "888,50"`,
    right: `"totalOrderAmount": "888.50"`,
    why: "Use '.' as the decimal separator, not the Danish ','.",
  },
  {
    wrong: `"DKK_500": "1500"`,
    right: `"DKK_500": "3"`,
    why: "The value is how many notes, not how much they are worth.",
  },
  {
    wrong: `"registrationDate": "17-08-2026"`,
    right: `"registrationDate": "2026-08-17"`,
    why: "The format is exactly yyyy-MM-dd.",
  },
  {
    wrong: `"messageId": "POS-01-000123"`,
    right: `"messageId": "0f8fad5b-d9cb-469f-a165-70867728950e"`,
    why: "messageId must be a GUID.",
  },
  {
    wrong: `"currencyType": "DKK"`,
    right: `(omit it)`,
    why: "Unknown fields are rejected. The market is already DKK-only.",
  },
];

export default function DenmarkBagRegistrationUseCasesSection() {
  const market = useMarket();
  const link = useGuideLink();

  useEffect(() => {
    document.title = "API Guide | Denmark | Bag Registration Use Cases";
  }, []);

  return (
    <section className="space-y-8">
      <header className="space-y-3">
        <p className="text-xs font-semibold uppercase tracking-widest text-brand-600">
          🇩🇰 Denmark · Bag Registration API
        </p>
        <h2 className="text-2xl font-bold tracking-tight text-gray-900">Use cases</h2>
        <p className="max-w-3xl text-sm leading-relaxed text-gray-600">
          Worked examples for the Danish registration flow, from your first token to a verified
          submission.
        </p>
        <JumpNav items={onThisPage} />
      </header>

      {/* ---------------- First call ---------------- */}
      <div className="space-y-4">
        <SectionHeading id="first-call" icon="🚀">
          Your first call
        </SectionHeading>

        <p className="text-sm leading-relaxed text-gray-600">
          Get a token, then use it for the next hour. Tokens are valid for 60 minutes — request one
          per hour, not one per bag.
        </p>

        <CodeBlock label="1. Get a token" code={tokenCall} />

        <Callout tone="warning" title="Getting a 403 with a valid token?">
          Your application needs the <InlineCode>{market.roles.registration}</InlineCode> app role.
          Roles are stamped into a token when it is issued, so if the role was granted after you got
          your token, request a fresh one.
        </Callout>
      </div>

      {/* ---------------- End of shift ---------------- */}
      <div className="space-y-4">
        <SectionHeading id="end-of-shift" icon="📦">
          Registering a bag at end of shift
        </SectionHeading>

        <p className="text-sm leading-relaxed text-gray-600">
          The till closes, the cash is counted into a bag, and the bag is registered before pickup.
          The example below declares 1750.00 DKK: three 500s, one 200, one 50.
        </p>

        <CodeBlock label="2. Register the bag" code={registerCall(market.testBaseUrl)} />

        <Callout tone="info" title="Check the sum before you send">
          3 × 500 + 1 × 200 + 1 × 50 = 1750.00, which matches{" "}
          <InlineCode>totalOrderAmount</InlineCode>. If the two disagree the request still succeeds,
          but the response carries a <InlineCode>warnings</InlineCode> array — treat that as a bug in
          your integration rather than a normal outcome.
        </Callout>

        <div>
          <p className="mb-2 text-xs font-semibold uppercase tracking-wider text-gray-500">
            What to store from the response
          </p>
          <Table headers={["Field", "Why you want it"]}>
            <tr>
              <td className="whitespace-nowrap px-3 py-2 font-mono font-medium text-gray-900">bagLifecycleId</td>
              <td className="px-3 py-2 text-gray-600">
                NOKAS identifier for this registration. Quote it when contacting support.
              </td>
            </tr>
            <tr>
              <td className="whitespace-nowrap px-3 py-2 font-mono font-medium text-gray-900">messageId</td>
              <td className="px-3 py-2 text-gray-600">
                Echoed from your request, so you can match the response to the message you sent.
              </td>
            </tr>
            <tr>
              <td className="whitespace-nowrap px-3 py-2 font-mono font-medium text-gray-900">
                requestCorrelationId
              </td>
              <td className="px-3 py-2 text-gray-600">
                Log it. It is the single fastest way for NOKAS support to find your request.
              </td>
            </tr>
          </Table>
        </div>
      </div>

      {/* ---------------- Verify ---------------- */}
      <div className="space-y-4">
        <SectionHeading id="verify" icon="🔍">
          Retrieving a registration
        </SectionHeading>

        <p className="text-sm leading-relaxed text-gray-600">
          Look a bag up by its number to confirm it reached us. The response body is identical to the
          one returned by the registration call.
        </p>

        <CodeBlock label="3. Retrieve it" code={retrieveCall(market.testBaseUrl)} />

        <Callout tone="info" title="The most recent submission wins">
          Bag numbers are not checked for uniqueness. If the same bag number is registered twice,
          this returns the later one.
        </Callout>
      </div>

      {/* ---------------- Common mistakes ---------------- */}
      <div className="space-y-4">
        <SectionHeading id="mistakes" icon="🩹">
          Common mistakes
        </SectionHeading>

        <p className="text-sm leading-relaxed text-gray-600">
          Each of these returns <InlineCode>400</InlineCode> with a message naming the exact JSON
          path. All failures are reported together, so one call tells you everything to fix.
        </p>

        <Table headers={["Instead of", "Send", "Why"]}>
          {mistakes.map((m) => (
            <tr key={m.wrong}>
              <td className="whitespace-nowrap px-3 py-2 font-mono text-xs text-rose-700">{m.wrong}</td>
              <td className="whitespace-nowrap px-3 py-2 font-mono text-xs text-emerald-700">{m.right}</td>
              <td className="px-3 py-2 text-gray-600">{m.why}</td>
            </tr>
          ))}
        </Table>
      </div>

      <div className="rounded-xl border border-gray-200 bg-gray-50 p-4">
        <p className="text-sm text-gray-700">
          Full field rules and status codes are on the{" "}
          <Link
            to={link("bag-registration")}
            className="font-semibold text-brand-700 underline-offset-2 hover:underline"
          >
            endpoints page
          </Link>
          .
        </p>
      </div>
    </section>
  );
}
