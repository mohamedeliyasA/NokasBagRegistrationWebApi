import { useEffect, useRef, useState } from "react";
import { useSearchParams } from "react-router-dom";
import PublicLayout from "../../layouts/PublicLayout";
import { PRODUCTION_OPEN } from "@/config/environments";
import {
  DEFAULT_MARKET,
  MARKET_LIST,
  SURFACE_LABEL,
  isMarketKey,
  type MarketConfig,
} from "@/config/markets";

/** How long to wait for the iframe to load before offering a fallback. */
const LOAD_TIMEOUT_MS = 8000;

function quickFacts(market: MarketConfig) {
  return [
    { label: "Base URL — Test", value: market.testBaseUrl },
    {
      label: "Base URL — Production",
      // A provisioned host is not an available one: say so next to the URL rather than
      // letting a partner point an integration at it.
      value: !market.prodBaseUrl
        ? "Not available yet"
        : PRODUCTION_OPEN
          ? market.prodBaseUrl
          : `${market.prodBaseUrl} (not open yet)`,
    },
    { label: "Authentication", value: "OAuth2 client credentials (Bearer token)" },
    { label: "Rate limit", value: market.rateLimit },
  ];
}

export default function PublicSwagger() {
  // The market lives in the query string so a reference can be linked or bookmarked
  // per country, e.g. /swagger?market=denmark.
  const [searchParams, setSearchParams] = useSearchParams();
  const param = searchParams.get("market") ?? undefined;
  const marketKey = isMarketKey(param) ? param : DEFAULT_MARKET;
  const market = MARKET_LIST.find((m) => m.key === marketKey)!;

  // null = still loading, true = loaded, false = never loaded (likely framing blocked)
  const [loaded, setLoaded] = useState<boolean | null>(null);
  const timerRef = useRef<number | undefined>(undefined);

  useEffect(() => {
    document.title = `${market.label} API Documentation | NOKAS Integration`;
  }, [market.label]);

  useEffect(() => {
    if (!market.swaggerUi) {
      return;
    }

    // Switching market swaps the iframe source, so the load race starts again.
    setLoaded(null);

    // Cross-origin framing failures are invisible to us - the load event simply
    // never fires. Treat "no load within the timeout" as blocked.
    timerRef.current = window.setTimeout(() => {
      setLoaded((current) => (current === null ? false : current));
    }, LOAD_TIMEOUT_MS);

    return () => window.clearTimeout(timerRef.current);
  }, [market.swaggerUi]);

  function handleLoad() {
    window.clearTimeout(timerRef.current);
    setLoaded(true);
  }

  function selectMarket(key: string) {
    setSearchParams(key === DEFAULT_MARKET ? {} : { market: key }, { replace: true });
  }

  return (
    <PublicLayout>
      {/* ---------------- Header ---------------- */}
      <section className="border-b border-gray-200 bg-gradient-to-b from-brand-50 to-white px-6 py-12">
        <div className="mx-auto max-w-6xl">
          <p className="text-xs font-semibold uppercase tracking-widest text-brand-600">
            Developer reference
          </p>

          {/* Market selector */}
          <div
            className="mt-4 inline-flex rounded-full border border-gray-200 bg-white p-1 shadow-sm"
            role="tablist"
            aria-label="Choose a market"
          >
            {MARKET_LIST.map((m) => {
              const active = m.key === market.key;
              return (
                <button
                  key={m.key}
                  type="button"
                  role="tab"
                  aria-selected={active}
                  onClick={() => selectMarket(m.key)}
                  className={[
                    "inline-flex items-center gap-2 rounded-full px-5 py-2 text-sm font-semibold transition",
                    active
                      ? "bg-brand-800 text-white shadow-sm"
                      : "text-gray-600 hover:bg-gray-100 hover:text-brand-800",
                  ].join(" ")}
                >
                  <span aria-hidden="true">{m.flag}</span>
                  {m.label}
                </button>
              );
            })}
          </div>

          <div className="mt-4 flex flex-col gap-5 md:flex-row md:items-end md:justify-between">
            <div>
              <h1 className="text-3xl font-bold tracking-tight text-brand-800">
                {market.label} — Bag Registration API
              </h1>
              <p className="mt-2 max-w-2xl text-sm leading-relaxed text-gray-600">
                {market.key === "denmark"
                  ? "Register cash bags in Denmark. All amounts are in DKK."
                  : "Submit bag registrations and retrieve counting results."}{" "}
                The interactive reference below is generated directly from the live API.
              </p>
              {market.surfaces.bagRegistration !== "live" && (
                <p className="mt-3 inline-flex items-center gap-2 rounded-lg bg-amber-50 px-3 py-1.5 text-xs font-semibold text-amber-800 ring-1 ring-inset ring-amber-600/20">
                  {SURFACE_LABEL[market.surfaces.bagRegistration]} — a successful response does not
                  mean a bag has been registered.
                </p>
              )}
            </div>

            <div className="flex shrink-0 flex-wrap gap-2">
              {market.swaggerUi && (
                <a
                  href={market.swaggerUi}
                  target="_blank"
                  rel="noopener noreferrer"
                  className="inline-flex items-center gap-1.5 rounded-full bg-brand-800 px-5 py-2 text-sm font-semibold text-white transition hover:bg-brand-700"
                >
                  Open full view
                  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" className="h-3.5 w-3.5" aria-hidden="true">
                    <path d="M14 5h5v5M19 5l-8 8M11 5H6a1 1 0 0 0-1 1v12a1 1 0 0 0 1 1h12a1 1 0 0 0 1-1v-5" strokeLinecap="round" strokeLinejoin="round" />
                  </svg>
                </a>
              )}
              {market.swaggerJson && (
                <a
                  href={market.swaggerJson}
                  target="_blank"
                  rel="noopener noreferrer"
                  className="rounded-full border border-brand-300 bg-white px-5 py-2 text-sm font-semibold text-brand-800 transition hover:bg-brand-50"
                >
                  Download OpenAPI JSON
                </a>
              )}
              {market.postmanCollection && (
                <a
                  href={market.postmanCollection}
                  download
                  className="rounded-full border border-brand-300 bg-white px-5 py-2 text-sm font-semibold text-brand-800 transition hover:bg-brand-50"
                >
                  Download Postman Collection
                </a>
              )}
            </div>
          </div>

          {/* Quick facts */}
          <dl className="mt-8 grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
            {quickFacts(market).map((f) => (
              <div key={f.label} className="rounded-xl border border-gray-200 bg-white p-4 shadow-card">
                <dt className="text-xs font-semibold uppercase tracking-wider text-gray-500">
                  {f.label}
                </dt>
                <dd className="mt-1 break-all font-mono text-xs text-gray-900">{f.value}</dd>
              </div>
            ))}
          </dl>
        </div>
      </section>

      {/* ---------------- Embedded Swagger ---------------- */}
      <section className="bg-gray-50 px-6 py-10">
        <div className="mx-auto max-w-6xl">
          <div className="overflow-hidden rounded-xl border border-gray-200 bg-white shadow-card">
            <div className="flex items-center justify-between border-b border-gray-200 bg-gray-50 px-4 py-2.5">
              <span className="text-xs font-semibold uppercase tracking-wider text-gray-500">
                Interactive reference — {market.label}
              </span>
              {market.swaggerUi && loaded === true && (
                <span className="inline-flex items-center gap-1.5 text-xs font-medium text-emerald-700">
                  <span className="h-1.5 w-1.5 rounded-full bg-emerald-500" />
                  Live
                </span>
              )}
            </div>

            <div className="relative h-[70vh] min-h-[520px] w-full">
              {/* A market with no published Swagger host gets an explanation rather than a
                  broken frame - the Postman collection is the usable alternative meanwhile. */}
              {!market.swaggerUi ? (
                <div className="absolute inset-0 flex items-center justify-center p-8">
                  <div className="max-w-md text-center">
                    <span className="mx-auto flex h-12 w-12 items-center justify-center rounded-full bg-brand-50 text-brand-700">
                      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" className="h-6 w-6" aria-hidden="true">
                        <circle cx="12" cy="12" r="9" />
                        <path d="M12 7v5l3 2" strokeLinecap="round" strokeLinejoin="round" />
                      </svg>
                    </span>
                    <h2 className="mt-4 text-base font-semibold text-gray-900">
                      Interactive reference is being published
                    </h2>
                    <p className="mt-2 text-sm leading-relaxed text-gray-600">
                      The {market.label} API is live and callable — its browsable Swagger reference is
                      not published on this domain yet. Use the Postman collection meanwhile: it covers
                      the full contract, including a request that demonstrates the validation errors.
                    </p>
                    {market.postmanCollection && (
                      <a
                        href={market.postmanCollection}
                        download
                        className="mt-5 inline-block rounded-full bg-brand-800 px-5 py-2 text-sm font-semibold text-white transition hover:bg-brand-700"
                      >
                        Download the {market.label} Postman collection
                      </a>
                    )}
                  </div>
                </div>
              ) : (
                <>
                  {loaded !== false && (
                    <iframe
                      key={market.key}
                      title={`NOKAS ${market.label} Bag Registration API — Swagger UI`}
                      src={market.swaggerUi}
                      onLoad={handleLoad}
                      className="h-full w-full border-0"
                      sandbox="allow-same-origin allow-scripts allow-forms allow-popups"
                    />
                  )}

                  {loaded === null && (
                    <div className="pointer-events-none absolute inset-0 flex items-center justify-center bg-white/80">
                      <div className="text-center">
                        <span className="mx-auto block h-7 w-7 animate-spin rounded-full border-2 border-brand-200 border-t-brand-700" />
                        <p className="mt-3 text-sm font-medium text-gray-700">Loading reference…</p>
                      </div>
                    </div>
                  )}

                  {loaded === false && (
                    <div className="absolute inset-0 flex items-center justify-center p-8">
                      <div className="max-w-md text-center">
                        <span className="mx-auto flex h-12 w-12 items-center justify-center rounded-full bg-amber-100 text-amber-700">
                          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" className="h-6 w-6" aria-hidden="true">
                            <path d="M12 9v4M12 17h.01" strokeLinecap="round" />
                            <path d="M10.3 3.9 2.4 17.5A2 2 0 0 0 4.1 20.5h15.8a2 2 0 0 0 1.7-3L13.7 3.9a2 2 0 0 0-3.4 0Z" strokeLinejoin="round" />
                          </svg>
                        </span>
                        <h2 className="mt-4 text-base font-semibold text-gray-900">
                          Embedded view unavailable
                        </h2>
                        <p className="mt-2 text-sm leading-relaxed text-gray-600">
                          The reference did not load in time. This usually means the browser blocked
                          framing, or the API is unreachable from your network.
                        </p>
                        <div className="mt-5 flex flex-wrap justify-center gap-2">
                          <a
                            href={market.swaggerUi}
                            target="_blank"
                            rel="noopener noreferrer"
                            className="rounded-full bg-brand-800 px-5 py-2 text-sm font-semibold text-white transition hover:bg-brand-700"
                          >
                            Open in a new tab
                          </a>
                          <button
                            type="button"
                            onClick={() => setLoaded(null)}
                            className="rounded-full border border-gray-300 bg-white px-5 py-2 text-sm font-semibold text-gray-700 transition hover:bg-gray-50"
                          >
                            Try again
                          </button>
                        </div>
                      </div>
                    </div>
                  )}
                </>
              )}
            </div>
          </div>

          <p className="mt-4 text-center text-xs text-gray-500">
            Looking for worked examples and error handling? Sign in and open the{" "}
            <span className="font-medium text-gray-700">API Guide</span> inside the portal.
          </p>
        </div>
      </section>
    </PublicLayout>
  );
}
