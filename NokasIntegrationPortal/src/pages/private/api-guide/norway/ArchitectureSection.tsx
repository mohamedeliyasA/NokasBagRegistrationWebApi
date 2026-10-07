import { useEffect } from "react";
import { PageHeader, SectionHeading } from "@/components/ui";

// Served from public/ rather than imported, so the large draw.io export is fetched
// as its own file instead of being pulled through the bundler.
const architectureDiagram = "/NokasBagRegArchitecture.svg";

const flow = [
  {
    title: "Register",
    body: "Partners register with NOKAS and receive a Client ID and Client Secret.",
  },
  {
    title: "Request a token",
    body: "Tokens are issued by Azure AD B2C using the client-credentials grant.",
  },
  {
    title: "Validate",
    body: "Every request is validated against NOKAS Azure AD policies at the gateway.",
  },
  {
    title: "Process",
    body: "Authorised requests are routed into the Web API for processing.",
  },
  {
    title: "Respond",
    body: "Responses and counting results are returned to the partner application.",
  },
];

export default function ArchitectureSection() {
  useEffect(() => {
    document.title = "API Guide | Architecture";
  }, []);

  return (
    <section className="space-y-8">
      <PageHeader
        eyebrow="API Guide"
        title="Architecture overview"
        lead="The Bag Registration API is built for security and performance — OAuth 2.0 token validation, Azure AD integration, and a modular backend, all behind a managed gateway."
      />

      {/* ---------------- Flow ---------------- */}
      <div className="space-y-4">
        <SectionHeading id="flow" icon="🔄">
          Request flow
        </SectionHeading>

        <ol className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
          {flow.map((step, i) => (
            <li
              key={step.title}
              className="relative rounded-xl border border-gray-200 bg-white p-5 shadow-card transition hover:border-brand-300 hover:shadow-card-hover"
            >
              <span className="text-2xl font-bold text-brand-200">
                {String(i + 1).padStart(2, "0")}
              </span>
              <h4 className="mt-1 text-sm font-semibold text-gray-900">{step.title}</h4>
              <p className="mt-1 text-xs leading-relaxed text-gray-600">{step.body}</p>
            </li>
          ))}
        </ol>

        <p className="text-sm leading-relaxed text-gray-600">
          This architecture supports secure, scalable communication between external partners and
          the NOKAS platform.
        </p>
      </div>

      {/* ---------------- Diagram ---------------- */}
      <div className="space-y-4">
        <SectionHeading id="diagram" icon="🧩">
          Architecture diagram
        </SectionHeading>

        <figure className="overflow-hidden rounded-xl border border-gray-200 bg-white shadow-card">
          <div className="flex items-center justify-between border-b border-gray-200 bg-gray-50 px-4 py-2.5">
            <span className="text-xs font-semibold uppercase tracking-wider text-gray-500">
              Authentication &amp; data flow
            </span>
            <a
              href={architectureDiagram}
              target="_blank"
              rel="noopener noreferrer"
              className="inline-flex items-center gap-1.5 text-xs font-semibold text-brand-700 transition hover:text-brand-900"
            >
              Open full size
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" className="h-3.5 w-3.5" aria-hidden="true">
                <path d="M14 5h5v5M19 5l-8 8M11 5H6a1 1 0 0 0-1 1v12a1 1 0 0 0 1 1h12a1 1 0 0 0 1-1v-5" strokeLinecap="round" strokeLinejoin="round" />
              </svg>
            </a>
          </div>

          <div className="overflow-x-auto bg-white p-4">
            <img
              src={architectureDiagram}
              alt="Diagram showing how partner applications authenticate via Azure AD and submit data through the NOKAS Web API"
              width={1041}
              height={841}
              className="mx-auto h-auto w-full"
            />
          </div>

          <figcaption className="border-t border-gray-100 bg-gray-50 px-4 py-2.5 text-xs text-gray-500">
            Partner applications authenticate via Azure AD and securely submit data through the
            NOKAS Web API.
          </figcaption>
        </figure>
      </div>
    </section>
  );
}
