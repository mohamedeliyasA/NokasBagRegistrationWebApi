import { useEffect } from "react";
import { Link } from "react-router-dom";
import { Badge, InlineCode, PageHeader, SectionHeading, Table } from "@/components/ui";

const versions = [
  { version: "2.0", change: "First roll out", author: "Mohamed Eliyas Anver Deen", date: "05 Sep 2026", current: true },
  { version: "1.0", change: "Initial draft", author: "Mohamed Eliyas Anver Deen", date: "30 Jun 2025", current: false },
];

const capabilities = [
  {
    title: "Register bag",
    body: "Register cash bags by sending a structured payload in JSON or XML.",
    icon: (
      <>
        <path d="M20 7 12 3 4 7l8 4 8-4Z" strokeLinejoin="round" />
        <path d="M4 7v10l8 4 8-4V7" strokeLinejoin="round" />
      </>
    ),
  },
  {
    title: "Retrieve count result",
    body: "Fetch the verified count for a previously registered bag, including any deviation.",
    icon: (
      <>
        <path d="M3 12h4l3 8 4-16 3 8h4" strokeLinecap="round" strokeLinejoin="round" />
      </>
    ),
  },
  {
    title: "Track operations",
    body: "Every operation is logged with a correlation ID for full lifecycle traceability.",
    icon: (
      <>
        <circle cx="12" cy="12" r="8" />
        <path d="M12 8v4l2.5 2.5" strokeLinecap="round" strokeLinejoin="round" />
      </>
    ),
  },
  {
    title: "Secure access",
    body: "Protected by Azure AD B2C OAuth2 with client credentials.",
    icon: (
      <>
        <rect x="4.5" y="10" width="15" height="10" rx="2" />
        <path d="M8 10V7a4 4 0 1 1 8 0v3" strokeLinecap="round" />
      </>
    ),
  },
];

export default function OverviewSection() {
  useEffect(() => {
    document.title = "API Guide | Overview";
  }, []);

  return (
    <section className="space-y-8">
      <PageHeader
        eyebrow="API Guide"
        title="Cash Handling Cloud API"
        lead="A functional overview of the NOKAS public-facing cloud API, which lets authorised vendors register cash bags digitally and retrieve their counting results securely."
      />

      {/* ---------------- What it does ---------------- */}
      <div className="space-y-4">
        <SectionHeading id="capabilities" icon="🔑">
          Core capabilities
        </SectionHeading>

        <div className="grid gap-4 sm:grid-cols-2">
          {capabilities.map((c) => (
            <article
              key={c.title}
              className="group rounded-xl border border-gray-200 bg-white p-5 shadow-card transition hover:-translate-y-0.5 hover:border-brand-300 hover:shadow-card-hover"
            >
              <span className="inline-flex h-10 w-10 items-center justify-center rounded-lg bg-brand-50 text-brand-700 transition group-hover:bg-brand-800 group-hover:text-white">
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" className="h-5 w-5" aria-hidden="true">
                  {c.icon}
                </svg>
              </span>
              <h4 className="mt-3 text-sm font-semibold text-gray-900">{c.title}</h4>
              <p className="mt-1 text-xs leading-relaxed text-gray-600">{c.body}</p>
            </article>
          ))}
        </div>
      </div>

      {/* ---------------- About ---------------- */}
      <div className="space-y-4">
        <SectionHeading id="about" icon="🏢">
          About NOKAS Cash Handling
        </SectionHeading>

        <div className="rounded-xl border border-gray-200 bg-white p-5 shadow-card">
          <ul className="space-y-2.5">
            {[
              "Specialists in the secure management of physical cash for businesses, banks and public institutions.",
              "Safe cash transport, accurate counting and direct bank deposits.",
              "Full reconciliation between what you declared and what we counted.",
            ].map((item) => (
              <li key={item} className="flex items-start gap-2.5 text-sm text-gray-700">
                <svg viewBox="0 0 20 20" fill="currentColor" className="mt-0.5 h-4 w-4 shrink-0 text-brand-600" aria-hidden="true">
                  <path
                    fillRule="evenodd"
                    d="M16.7 5.3a1 1 0 0 1 0 1.4l-7.5 7.5a1 1 0 0 1-1.4 0l-3.5-3.5a1 1 0 1 1 1.4-1.4l2.8 2.8 6.8-6.8a1 1 0 0 1 1.4 0Z"
                    clipRule="evenodd"
                  />
                </svg>
                <span>{item}</span>
              </li>
            ))}
          </ul>

          <p className="mt-4 border-t border-gray-100 pt-4 text-sm leading-relaxed text-gray-600">
            To improve operational efficiency and vendor transparency, NOKAS provides a RESTful API
            for system-to-system integration — covering both Cash Bag Registration and Cash Bag
            Counting Results.
          </p>
        </div>
      </div>

      {/* ---------------- Purpose ---------------- */}
      <div className="space-y-4">
        <SectionHeading id="purpose" icon="🎯">
          Purpose of the API
        </SectionHeading>

        <div className="grid gap-4 sm:grid-cols-2">
          <div className="rounded-xl border border-gray-200 bg-white p-5 shadow-card">
            <Badge tone="green">Bag registration</Badge>
            <p className="mt-2.5 text-sm leading-relaxed text-gray-600">
              Register bags via the API by supplying the required details in the registration
              request payload.
            </p>
            <Link
              to="/portal/api-guide/endpoints"
              className="mt-3 inline-flex items-center gap-1 text-xs font-semibold text-brand-700 underline-offset-2 hover:underline"
            >
              See the endpoints <span aria-hidden="true">→</span>
            </Link>
          </div>

          <div className="rounded-xl border border-gray-200 bg-white p-5 shadow-card">
            <Badge tone="accent">Counting results</Badge>
            <p className="mt-2.5 text-sm leading-relaxed text-gray-600">
              Retrieve counting results for your registered bags once verification by NOKAS is
              complete.
            </p>
            <Link
              to="/portal/api-guide/endpoints"
              className="mt-3 inline-flex items-center gap-1 text-xs font-semibold text-brand-700 underline-offset-2 hover:underline"
            >
              See the endpoints <span aria-hidden="true">→</span>
            </Link>
          </div>
        </div>
      </div>

      {/* ---------------- Reference table ---------------- */}
      <div className="space-y-4">
        <SectionHeading id="functions" icon="📋">
          Function reference
        </SectionHeading>

        <Table headers={["Function", "Description"]}>
          <tr>
            <td className="whitespace-nowrap px-3 py-2 font-semibold text-gray-900">Register bag</td>
            <td className="px-3 py-2 text-gray-600">
              Register cash bags by sending structured payloads in JSON or XML.
            </td>
          </tr>
          <tr>
            <td className="whitespace-nowrap px-3 py-2 font-semibold text-gray-900">
              Retrieve count result
            </td>
            <td className="px-3 py-2 text-gray-600">
              Fetch the verified count results of previously registered cash bags.
            </td>
          </tr>
          <tr>
            <td className="whitespace-nowrap px-3 py-2 font-semibold text-gray-900">
              Track operations
            </td>
            <td className="px-3 py-2 text-gray-600">
              Every operation is logged with correlation IDs for full lifecycle traceability.
            </td>
          </tr>
          <tr>
            <td className="whitespace-nowrap px-3 py-2 font-semibold text-gray-900">Secure access</td>
            <td className="px-3 py-2 text-gray-600">
              Secured via Azure AD B2C OAuth2 token-based authentication using{" "}
              <InlineCode>client_id</InlineCode> and <InlineCode>client_secret</InlineCode>.
            </td>
          </tr>
        </Table>
      </div>

      {/* ---------------- Version history ---------------- */}
      <div className="space-y-4">
        <SectionHeading id="versions" icon="🗂️">
          Document history
        </SectionHeading>

        <Table headers={["Version", "Purpose / change", "Author", "Date"]}>
          {versions.map((v) => (
            <tr key={v.version}>
              <td className="whitespace-nowrap px-3 py-2">
                <span className="inline-flex items-center gap-2">
                  <span className="font-mono font-semibold text-gray-900">{v.version}</span>
                  {v.current && <Badge tone="green">Current</Badge>}
                </span>
              </td>
              <td className="px-3 py-2 text-gray-700">{v.change}</td>
              <td className="whitespace-nowrap px-3 py-2 text-gray-600">{v.author}</td>
              <td className="whitespace-nowrap px-3 py-2 text-gray-600">{v.date}</td>
            </tr>
          ))}
        </Table>
      </div>
    </section>
  );
}
