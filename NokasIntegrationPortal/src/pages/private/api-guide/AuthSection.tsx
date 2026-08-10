import { useEffect } from "react";
import { Badge, Callout, CodeBlock, EnvUrlList, InlineCode, JumpNav, PageHeader, SectionHeading, Table } from "@/components/ui";
import { PROD_ENV, TEST_ENV } from "@/config/environments";

const tokenRequest = (env: typeof TEST_ENV) => `POST ${env.tokenUrl}
Content-Type: application/x-www-form-urlencoded

grant_type=client_credentials
client_id=YOUR_CLIENT_ID
client_secret=YOUR_CLIENT_SECRET
scope=${env.scope}`;

const tokenResponse = `{
  "token_type": "Bearer",
  "expires_in": 3599,
  "ext_expires_in": 3599,
  "access_token": "eyJ0eXAiOiJKV1QiLCJhbGciOi..."
}`;

const authHeader = `Authorization: Bearer YOUR_ACCESS_TOKEN`;

const jumpItems = [
  { id: "credentials", label: "Credentials" },
  { id: "token", label: "Get a token" },
  { id: "using", label: "Use the token" },
  { id: "security", label: "Security" },
];

export default function AuthSection() {
  useEffect(() => {
    document.title = "API Guide | Authentication";
  }, []);

  return (
    <section className="space-y-8">
      <PageHeader
        eyebrow="API Guide"
        title="Authentication & security"
        lead="The NOKAS Cash Handling API uses OAuth 2.0 client credentials — an industry-standard protocol for secure, token-based machine-to-machine authentication."
      >
        <JumpNav items={jumpItems} />
      </PageHeader>

      {/* ---------------- Credentials ---------------- */}
      <div className="space-y-4">
        <SectionHeading id="credentials" icon="🪪">
          Obtaining your credentials
        </SectionHeading>

        <ol className="space-y-3">
          {[
            {
              title: "Contact NOKAS support",
              body: "Get in touch to start the registration process.",
            },
            {
              title: "Submit your application details",
              body: "Application name, a description of what it does, and how you intend to use the API.",
            },
            {
              title: "Review",
              body: "Your application is reviewed for security and compliance.",
            },
            {
              title: "Receive your credentials",
              body: "On approval you receive a Client ID (public identifier) and a Client Secret (private — keep it safe).",
            },
          ].map((step, i) => (
            <li
              key={step.title}
              className="flex gap-4 rounded-xl border border-gray-200 bg-white p-4 shadow-card"
            >
              <span className="flex h-7 w-7 shrink-0 items-center justify-center rounded-full bg-brand-800 text-xs font-bold text-white">
                {i + 1}
              </span>
              <div>
                <h4 className="text-sm font-semibold text-gray-900">{step.title}</h4>
                <p className="mt-0.5 text-xs leading-relaxed text-gray-600">{step.body}</p>
              </div>
            </li>
          ))}
        </ol>

        <Callout tone="danger" title="Keep your secret secret">
          Your <InlineCode>client_secret</InlineCode> is equivalent to a password. Never commit it to
          source control, embed it in a browser application, or share it over email. If you believe
          it has been exposed, contact NOKAS immediately to have it rotated.
        </Callout>
      </div>

      {/* ---------------- Token ---------------- */}
      <div className="space-y-4">
        <SectionHeading id="token" icon="🎟️">
          Requesting an access token
        </SectionHeading>

        <EnvUrlList
          title="Token endpoint"
          entries={[
            { label: TEST_ENV.label, url: TEST_ENV.tokenUrl },
            { label: PROD_ENV.label, url: PROD_ENV.tokenUrl },
          ]}
        />

        <Callout tone="warning" title="Test and production are separate B2C tenants">
          Each environment issues its own credentials and uses its own <InlineCode>scope</InlineCode>.
          Test credentials will not authenticate against production, and vice versa.
        </Callout>

        <CodeBlock label={`Request — ${TEST_ENV.label}`} code={tokenRequest(TEST_ENV)} />
        <CodeBlock label={`Request — ${PROD_ENV.label}`} code={tokenRequest(PROD_ENV)} />
        <CodeBlock label="200 OK (both environments)" code={tokenResponse} />

        <Callout tone="info" title="Reuse your token">
          The token is valid for <strong>60 minutes</strong> (3599 seconds). Reuse it across all API
          calls within that window rather than requesting a new one per call — it reduces
          authentication overhead and load on both sides.
        </Callout>

        <div>
          <p className="mb-2 text-xs font-semibold uppercase tracking-wider text-gray-500">
            Expected status codes
          </p>
          <Table headers={["Code", "Meaning"]}>
            <tr>
              <td className="whitespace-nowrap px-3 py-2">
                <Badge tone="green">200 OK</Badge>
              </td>
              <td className="px-3 py-2 text-gray-700">Token successfully issued.</td>
            </tr>
            <tr>
              <td className="whitespace-nowrap px-3 py-2">
                <Badge tone="amber">400 Bad Request</Badge>
              </td>
              <td className="px-3 py-2 text-gray-700">Missing or invalid parameters.</td>
            </tr>
            <tr>
              <td className="whitespace-nowrap px-3 py-2">
                <Badge tone="amber">401 Unauthorized</Badge>
              </td>
              <td className="px-3 py-2 text-gray-700">Incorrect client credentials.</td>
            </tr>
          </Table>
        </div>
      </div>

      {/* ---------------- Using ---------------- */}
      <div className="space-y-4">
        <SectionHeading id="using" icon="🔗">
          Using the access token
        </SectionHeading>

        <p className="text-sm leading-relaxed text-gray-600">
          Include the token in the <InlineCode>Authorization</InlineCode> header of every request:
        </p>

        <CodeBlock label="Header" code={authHeader} />
      </div>

      {/* ---------------- Security ---------------- */}
      <div className="space-y-4">
        <SectionHeading id="security" icon="🛡️">
          Security requirements
        </SectionHeading>

        <div className="grid gap-4 sm:grid-cols-2">
          {[
            {
              title: "Always use HTTPS",
              body: "Tokens must only ever be transmitted over TLS. Plain HTTP requests are rejected.",
            },
            {
              title: "Store tokens securely",
              body: "Hold tokens in memory or a secure secret store — never in logs, URLs or client-side storage.",
            },
            {
              title: "One identity per integration",
              body: "Every request is scoped to the client ID in your token. You can only ever see your own data.",
            },
            {
              title: "Rotate on suspicion",
              body: "If a secret may have leaked, request a rotation rather than waiting for expiry.",
            },
          ].map((c) => (
            <div key={c.title} className="rounded-xl border border-gray-200 bg-white p-4 shadow-card">
              <h4 className="text-sm font-semibold text-gray-900">{c.title}</h4>
              <p className="mt-1 text-xs leading-relaxed text-gray-600">{c.body}</p>
            </div>
          ))}
        </div>
      </div>
    </section>
  );
}
