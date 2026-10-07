import { useEffect } from "react";
import { Link } from "react-router-dom";
import { Badge, InlineCode, PageHeader, SectionHeading } from "@/components/ui";
import { MARKET_LIST, SURFACE_LABEL, SURFACE_TONE, guidePath } from "@/config/markets";

export default function MarketPicker() {
  useEffect(() => {
    document.title = "API Guide | Choose a market";
  }, []);

  return (
    <section className="mx-auto max-w-5xl space-y-8 rounded-xl border border-gray-200 bg-white p-5 shadow-card sm:p-8">
      <PageHeader
        eyebrow="API Guide"
        title="Choose your market"
        lead="NOKAS operates a separate cash-handling integration per country. The authentication model, correlation and error contracts are shared; endpoints, payloads and currencies are not. Pick the market you are integrating with."
      />

      <div className="grid gap-4 sm:grid-cols-2">
        {MARKET_LIST.map((market) => (
          <Link
            key={market.key}
            to={guidePath(market.key)}
            className="group flex flex-col rounded-xl border border-gray-200 bg-white p-5 shadow-card transition hover:-translate-y-0.5 hover:border-brand-300 hover:shadow-card-hover"
          >
            <div className="flex items-center gap-3">
              <span className="text-3xl leading-none" aria-hidden="true">
                {market.flag}
              </span>
              <div>
                <h3 className="text-base font-semibold text-gray-900">{market.label}</h3>
                <p className="text-xs text-gray-500">{market.currency}</p>
              </div>
            </div>

            <p className="mt-3 text-sm leading-relaxed text-gray-600">{market.tagline}</p>

            <dl className="mt-4 space-y-1.5 border-t border-gray-100 pt-3">
              <div className="flex items-center justify-between gap-2">
                <dt className="text-xs text-gray-600">Bag Registration</dt>
                <dd>
                  <Badge tone={SURFACE_TONE[market.surfaces.bagRegistration]}>
                    {SURFACE_LABEL[market.surfaces.bagRegistration]}
                  </Badge>
                </dd>
              </div>
              <div className="flex items-center justify-between gap-2">
                <dt className="text-xs text-gray-600">Counting Result</dt>
                <dd>
                  <Badge tone={SURFACE_TONE[market.surfaces.countingResult]}>
                    {SURFACE_LABEL[market.surfaces.countingResult]}
                  </Badge>
                </dd>
              </div>
            </dl>

            <span className="mt-4 inline-flex items-center gap-1 text-xs font-semibold text-brand-700 underline-offset-2 group-hover:underline">
              Open the {market.label} guide <span aria-hidden="true">→</span>
            </span>
          </Link>
        ))}
      </div>

      <div className="space-y-4">
        <SectionHeading id="shared" icon="🔗">
          What is the same everywhere
        </SectionHeading>

        <div className="rounded-xl border border-gray-200 bg-white p-5 shadow-card">
          <ul className="space-y-2.5 text-sm text-gray-700">
            <li>
              <strong>Authentication.</strong> OAuth 2.0 client credentials against the same Azure AD
              B2C tenant. One set of credentials, with a per-market app role deciding what it can reach.
            </li>
            <li>
              <strong>Correlation.</strong> The same <InlineCode>X-Correlation-ID</InlineCode> header
              on every request and response, in every market.
            </li>
            <li>
              <strong>Error shape.</strong> The same JSON error body and the same error codes.
            </li>
            <li>
              <strong>Tenancy.</strong> Your client identity comes only from your token. You can never
              see another partner's data, in any market.
            </li>
          </ul>
        </div>
      </div>
    </section>
  );
}
