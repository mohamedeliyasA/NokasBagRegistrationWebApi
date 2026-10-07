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
import { useGuideLink, useMarket } from "../market";

const ROUTE_PREFIX = "/api/cash-mgmt/dk";

const requiredHeaders = [
  { name: "Authorization", value: "Bearer <token>", note: "OAuth2 client-credentials token carrying the Danish app role" },
  { name: "Content-Type", value: "application/json", note: "JSON only — any other content type is rejected with 415" },
  { name: "X-Correlation-ID", value: "<uuid>", note: "Optional. Echoed back; generated for you if omitted" },
];

const registerRequest = `{
  "bagRegistration": {
    "customerRefNumber": "5790000123456",
    "cashRegisterId": "POS-01",
    "messageId": "0f8fad5b-d9cb-469f-a165-70867728950e",
    "bagNumber": "3164696800",
    "registrationDate": "2026-08-17",
    "totalOrderAmount": "888.50",
    "bagItemsLC": {
      "DKK_500": "1",
      "DKK_200": "1",
      "DKK_100": "1",
      "DKK_50": "1",
      "DKK_20": "1",
      "DKK_10": "1",
      "DKK_5": "1",
      "DKK_2": "1",
      "DKK_1": "1",
      "DKK_05": "1"
    }
  },
  "metaData": {
    "companyId": "3214",
    "branchId": "9543"
  }
}`;

const registerResponse = `{
  "customerRefNumber": "5790000123456",
  "bagNumber": "3164696800",
  "messageId": "0f8fad5b-d9cb-469f-a165-70867728950e",
  "description": "Bag registration successful.",
  "registrationStatus": "Registered",
  "bagLifecycleId": "45ffddba-0a87-4f82-9ae1-a01a7fac3432",
  "requestCorrelationId": "bc414e2a-ec7d-4c8b-8dac-a941098e6ff9"
}`;

const responseFields: { field: string; meaning: string }[] = [
  { field: "customerRefNumber", meaning: "Echoed from your request." },
  { field: "bagNumber", meaning: "Echoed from your request." },
  { field: "messageId", meaning: "Echoed from your request, so a response can be matched to the message you sent." },
  { field: "description", meaning: "Human-readable confirmation from NOKAS's system." },
  { field: "registrationStatus", meaning: "Registered." },
  { field: "bagLifecycleId", meaning: "NOKAS identifier for this registration. Quote it to support." },
  { field: "requestCorrelationId", meaning: "Your X-Correlation-ID, or one generated for you. Log it." },
];

const warningResponse = `{
  "customerRefNumber": "5790000123456",
  "bagNumber": "3164696800",
  "registrationStatus": "Registered",
  "warnings": [
    "totalOrderAmount (900.00) does not equal the sum of bagItemsLC (500). Each bagItemsLC value is a quantity of that denomination, not an amount."
  ]
}`;

const validationError = `{
  "title": "The payload does not match the Danish bag registration contract.",
  "status": 400,
  "correlationId": "8f14e45f-ceea-467a-9575-9f2b8c1a7b3d",
  "code": "VALIDATION_ERROR",
  "field": "bagRegistration.messageId",
  "exception": "bagRegistration.messageId: Must be a GUID. | bagRegistration.bagNumber: Must contain digits only."
}`;

const fieldRules: { field: string; rule: string }[] = [
  { field: "customerRefNumber", rule: "Branch id. Non-empty, at most 50 characters." },
  { field: "cashRegisterId", rule: "Cash register id. Non-empty, at most 50 characters." },
  { field: "messageId", rule: "Must be a GUID. Your own reference for this message." },
  { field: "bagNumber", rule: "The bag label. Digits only, and it must belong to the branch — see below." },
  { field: "registrationDate", rule: "Exactly yyyy-MM-dd, and a real calendar date." },
  { field: "totalOrderAmount", rule: "Positive decimal in DKK. '.' as the decimal separator, at most 2 decimal places." },
  { field: "bagItemsLC", rule: "OPTIONAL. Object of denomination → quantity. If you send it, it must be well formed." },
  { field: "metaData", rule: "OPTIONAL, and sits beside bagRegistration rather than inside it. Your own name/value pairs — see below." },
];

const denominations: { key: string; value: string; kind: string }[] = [
  { key: "DKK_500", value: "500.00", kind: "Note" },
  { key: "DKK_200", value: "200.00", kind: "Note" },
  { key: "DKK_100", value: "100.00", kind: "Note" },
  { key: "DKK_50", value: "50.00", kind: "Note" },
  { key: "DKK_20", value: "20.00", kind: "Coin" },
  { key: "DKK_10", value: "10.00", kind: "Coin" },
  { key: "DKK_5", value: "5.00", kind: "Coin" },
  { key: "DKK_2", value: "2.00", kind: "Coin" },
  { key: "DKK_1", value: "1.00", kind: "Coin" },
  { key: "DKK_05", value: "0.50", kind: "Coin (50 øre)" },
];

const onThisPage = [
  { id: "register", label: "Register" },
  { id: "contract", label: "Field rules" },
  { id: "denominations", label: "Denominations" },
  { id: "readback", label: "Retrieve" },
  { id: "errors", label: "Errors" },
  { id: "status-codes", label: "Status codes" },
];

function EndpointCard({
  method,
  path,
  title,
  summary,
  baseUrl,
  children,
}: {
  method: HttpMethod;
  path: string;
  title: string;
  summary: string;
  baseUrl: string;
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
        <EnvUrlList title="Full URL" entries={[{ label: "Test", url: `${baseUrl}${path}` }]} />
        {children}
      </div>
    </article>
  );
}

export default function DenmarkBagRegistrationEndpointsSection() {
  const market = useMarket();
  const link = useGuideLink();

  useEffect(() => {
    document.title = "API Guide | Denmark | Bag Registration Endpoints";
  }, []);

  return (
    <section className="space-y-8">
      <header className="space-y-3">
        <p className="text-xs font-semibold uppercase tracking-widest text-brand-600">
          🇩🇰 Denmark · Bag Registration API
        </p>
        <h2 className="text-2xl font-bold tracking-tight text-gray-900">Bag Registration Endpoints</h2>
        <p className="max-w-3xl text-sm leading-relaxed text-gray-600">
          Register a cash bag before pickup, and read back what we received. Every endpoint is scoped
          to your own account — you only ever see bags registered under your client credentials. All
          amounts are in <strong>DKK</strong>.
        </p>
        <JumpNav items={onThisPage} />
      </header>

      {/* ---------------- Register ---------------- */}
      <div className="space-y-4">
        <SectionHeading id="register" icon="📦">
          Register a bag
        </SectionHeading>

        <EndpointCard
          method="POST"
          path="/bag-registration"
          title="Register a cash bag"
          summary="Submits a bag with its declared amount and denomination breakdown."
          baseUrl={market.testBaseUrl}
        >
          <div>
            <p className="mb-2 text-xs font-semibold uppercase tracking-wider text-gray-500">Headers</p>
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

          <CodeBlock label="Request body" code={registerRequest} />
          <CodeBlock label="201 Created" code={registerResponse} />

          <Table headers={["Field", "Meaning"]}>
            {responseFields.map((f) => (
              <tr key={f.field}>
                <td className="whitespace-nowrap px-3 py-2 font-mono font-medium text-gray-900">{f.field}</td>
                <td className="px-3 py-2 text-gray-600">{f.meaning}</td>
              </tr>
            ))}
          </Table>

          <Callout tone="info" title="Only a 201 means the bag is registered">
            The registration is sent to NOKAS's system before you get your answer. A <InlineCode>201</InlineCode>{" "}
            means it was confirmed. If you receive <InlineCode>502</InlineCode> or <InlineCode>503</InlineCode>,
            do not assume the bag was registered. After a <InlineCode>503</InlineCode> it may or may not exist:
            retry with the same <InlineCode>messageId</InlineCode>, or contact NOKAS with the{" "}
            <InlineCode>correlationId</InlineCode>.
          </Callout>
        </EndpointCard>
      </div>

      {/* ---------------- Field rules ---------------- */}
      <div className="space-y-4">
        <SectionHeading id="contract" icon="📋">
          Field rules
        </SectionHeading>

        <Callout tone="warning" title="Every value is a JSON string — including the numbers">
          <InlineCode>"totalOrderAmount": "888.50"</InlineCode> is correct.{" "}
          <InlineCode>"totalOrderAmount": 888.50</InlineCode> — unquoted — is rejected. The same applies
          to every denomination quantity. This keeps amounts exact rather than leaving them to a
          floating-point representation.
        </Callout>

        <p className="text-sm leading-relaxed text-gray-600">
          All fields are mandatory except <InlineCode>bagItemsLC</InlineCode>.{" "}
          <strong>Unknown fields are rejected</strong> at either level, so a typo in a field name
          fails loudly instead of being silently dropped.
        </p>

        <Callout tone="warning" title="The bag label must belong to the branch">
          <p>
            A bag label is issued to a specific branch, and NOKAS checks that the label you send
            belongs to the <InlineCode>customerRefNumber</InlineCode> you send it with. The first
            seven digits of <InlineCode>bagNumber</InlineCode> must equal{" "}
            <strong>the first two digits of the customerRefNumber followed by its sixth to tenth
            digits</strong>.
          </p>
          <p className="mt-2">
            For <InlineCode>customerRefNumber</InlineCode> <InlineCode>4400003533</InlineCode> that
            is <InlineCode>44</InlineCode> + <InlineCode>03533</InlineCode> ={" "}
            <InlineCode>4403533</InlineCode>, so a label such as{" "}
            <InlineCode>440353398231</InlineCode> is accepted.
          </p>
          <p className="mt-2">
            A label that does not match is rejected with <InlineCode>400</InlineCode> and the
            message “The bag label number must belong to the account of the registration.”
          </p>
        </Callout>

        <Callout tone="info" title="metaData: your own reference values">
          <p>
            <InlineCode>metaData</InlineCode> is optional and free-form. Use it to carry identifiers
            from your own system — a store id, a company id, a till reference — so you can match a
            registration back to your records. Send it as a sibling of{" "}
            <InlineCode>bagRegistration</InlineCode>, not inside it.
          </p>
          <p className="mt-2">
            Any names you like, up to 20 entries; names at most 50 characters, values at most 200,
            and <strong>every value must be a quoted string</strong>. NOKAS stores it and passes it
            on with your registration. It never affects what you are allowed to see — that is
            decided only by the credentials you authenticate with.
          </p>
        </Callout>

        <Callout tone="info" title="bagItemsLC is optional">
          You may declare only <InlineCode>totalOrderAmount</InlineCode> and omit the denomination
          breakdown entirely, or send it as <InlineCode>null</InlineCode>. Optional does not mean
          unchecked though — a breakdown you <em>do</em> send must use valid denomination keys and
          whole-number quantities. With no breakdown there is nothing to cross-check the total
          against, so no mismatch warning is produced.
        </Callout>

        <Table headers={["Field", "Rule"]}>
          {fieldRules.map((f) => (
            <tr key={f.field}>
              <td className="whitespace-nowrap px-3 py-2 font-mono font-medium text-gray-900">{f.field}</td>
              <td className="px-3 py-2 text-gray-600">{f.rule}</td>
            </tr>
          ))}
        </Table>

        <Callout tone="danger" title="Use '.' as the decimal separator, not ','">
          <InlineCode>"888,50"</InlineCode> is rejected. Danish locale formatting is the most common
          mistake here, and <InlineCode>"1.234"</InlineCode> versus <InlineCode>"1,234"</InlineCode>{" "}
          would otherwise be genuinely ambiguous.
        </Callout>
      </div>

      {/* ---------------- Denominations ---------------- */}
      <div className="space-y-4">
        <SectionHeading id="denominations" icon="🪙">
          Denominations
        </SectionHeading>

        <Callout tone="warning" title="Each value is a quantity, not an amount">
          <InlineCode>"DKK_500": "3"</InlineCode> means <strong>three 500-krone notes</strong> — that
          is 1500.00 DKK, not 3.00 DKK and not 500 DKK.
        </Callout>

        <p className="text-sm leading-relaxed text-gray-600">
          The whole block is optional. Keys are matched exactly and are case-sensitive. Quantities of{" "}
          <InlineCode>"0"</InlineCode> are accepted, so a till system that emits the full denomination
          grid does not have to strip the empty rows.
        </p>

        <Table headers={["Key", "Face value (DKK)", "Type"]}>
          {denominations.map((d) => (
            <tr key={d.key}>
              <td className="whitespace-nowrap px-3 py-2 font-mono font-medium text-gray-900">{d.key}</td>
              <td className="whitespace-nowrap px-3 py-2 font-mono text-brand-700">{d.value}</td>
              <td className="px-3 py-2 text-gray-600">{d.kind}</td>
            </tr>
          ))}
        </Table>

        <Callout tone="info" title="There is no DKK_1000">
          The 1000-krone note is being withdrawn from circulation and is not part of this contract.
        </Callout>

        <div className="space-y-2">
          <p className="text-sm font-semibold text-gray-900">
            If the total does not match the breakdown
          </p>
          <p className="text-sm leading-relaxed text-gray-600">
            The request is still accepted, but the response carries a <InlineCode>warnings</InlineCode>{" "}
            array. Treat it as a bug in your integration — it almost always means a quantity was sent
            where an amount was meant, or the other way round.
          </p>
          <CodeBlock label="201 Created — with a warning" code={warningResponse} />
        </div>
      </div>

      {/* ---------------- Read back ---------------- */}
      <div className="space-y-4">
        <SectionHeading id="readback" icon="🔍">
          Retrieve a registration
        </SectionHeading>

        <EndpointCard
          method="GET"
          path="/bag-registration/{bagNumber}"
          title="Retrieve a bag registration"
          summary="Returns the same response body as the registration call."
          baseUrl={market.testBaseUrl}
        >
          <p className="text-sm leading-relaxed text-gray-600">
            Keyed on bag number. Scoped to your own client — a bag
            registered by another partner returns <InlineCode>404</InlineCode>, never their data.
          </p>

          <Callout tone="warning" title="Bag numbers are not unique">
            The same bag number can be registered more than once. This endpoint returns the <strong>most recent</strong>{" "}
            submission for that bag number.
          </Callout>
        </EndpointCard>
      </div>

      {/* ---------------- Errors ---------------- */}
      <div className="space-y-4">
        <SectionHeading id="errors" icon="❗">
          Validation errors
        </SectionHeading>

        <p className="text-sm leading-relaxed text-gray-600">
          <strong>Every rule violation is reported at once</strong>, so you can fix a payload in one
          pass instead of discovering problems one round-trip at a time.{" "}
          <InlineCode>field</InlineCode> names the first failing path;{" "}
          <InlineCode>exception</InlineCode> lists every failure as{" "}
          <InlineCode>&lt;json path&gt;: &lt;message&gt;</InlineCode>.
        </p>

        <CodeBlock label="400 Bad Request" code={validationError} />

        <Link
          to={link("errors")}
          className="inline-flex items-center gap-1 text-xs font-semibold text-brand-700 underline-offset-2 hover:underline"
        >
          See the shared error reference <span aria-hidden="true">→</span>
        </Link>
      </div>

      {/* ---------------- Status codes ---------------- */}
      <div className="space-y-4">
        <SectionHeading id="status-codes" icon="🚦">
          Status codes
        </SectionHeading>

        <Table headers={["Code", "Meaning"]}>
          {[
            { code: "201 Created", meaning: "The registration was confirmed by NOKAS's system.", tone: "green" as const },
            { code: "200 OK", meaning: "Retrieve succeeded.", tone: "green" as const },
            { code: "400 Bad Request", meaning: "The payload does not match the contract (VALIDATION_ERROR), or NOKAS's system refused the registration (ERR_REGISTRATION_REJECTED); the message says why.", tone: "amber" as const },
            { code: "401 Unauthorized", meaning: "Token missing, expired, or not valid for this API.", tone: "amber" as const },
            { code: "403 Forbidden", meaning: `Token is valid but lacks the ${market.roles.registration} app role.`, tone: "amber" as const },
            { code: "404 Not Found", meaning: "No bag with that number exists for your account (ERR_BAG_NOT_FOUND).", tone: "amber" as const },
            { code: "409 Conflict", meaning: "The bag is already registered (ERR_DUPLICATE_BAG), or the customerRefNumber already belongs to a different client (ERR_CUSTOMER_REF_CONFLICT). The first client to register with a customerRefNumber owns it; contact NOKAS if it is yours.", tone: "amber" as const },
            { code: "415 Unsupported Media Type", meaning: "Content-Type was not application/json.", tone: "amber" as const },
            { code: "502 Bad Gateway", meaning: "NOKAS's system could not complete the registration (ERR_BACKEND_ERROR). The bag was not registered. Contact NOKAS support with the correlationId if it continues.", tone: "red" as const },
            { code: "503 Service Unavailable", meaning: "NOKAS's system did not answer in time (ERR_BACKEND_UNAVAILABLE), or the request could not be processed right now (ERR_SERVICE_UNAVAILABLE). The bag may or may not have been registered. Retry with the same messageId.", tone: "red" as const },
            { code: "429 Too Many Requests", meaning: `Rate limit exceeded (${market.rateLimit} per application).`, tone: "red" as const },
          ].map((s) => (
            <tr key={s.code}>
              <td className="whitespace-nowrap px-3 py-2">
                <Badge tone={s.tone}>{s.code}</Badge>
              </td>
              <td className="px-3 py-2 text-gray-700">{s.meaning}</td>
            </tr>
          ))}
        </Table>
      </div>

      <div className="rounded-xl border border-gray-200 bg-gray-50 p-4">
        <p className="text-sm text-gray-700">
          Ready to try it end to end?{" "}
          <Link
            to={link("bag-registration/use-cases")}
            className="font-semibold text-brand-700 underline-offset-2 hover:underline"
          >
            See the use cases
          </Link>
          .
        </p>
      </div>
    </section>
  );
}
