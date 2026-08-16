import { useEffect, useRef, useState } from "react";
import PublicLayout from "../../layouts/PublicLayout";
import { PROD_ENV, RATE_LIMIT, TEST_ENV } from "@/config/environments";

const SWAGGER_UI = TEST_ENV.swaggerUi;
const SWAGGER_JSON = TEST_ENV.swaggerJson;
// Static asset served from public/ - see BagReg_PostmanCollection.json's own description
// for how to fill in your client id/secret. Base URL inside it matches TEST_ENV.baseUrl.
const POSTMAN_COLLECTION = "/BagReg_PostmanCollection.json";

/** How long to wait for the iframe to load before offering a fallback. */
const LOAD_TIMEOUT_MS = 8000;

const quickFacts = [
  { label: "Base URL — Test", value: TEST_ENV.baseUrl },
  { label: "Base URL — Production", value: PROD_ENV.baseUrl },
  { label: "Authentication", value: "OAuth2 client credentials (Bearer token)" },
  { label: "Rate limit", value: RATE_LIMIT },
];

export default function PublicSwagger() {
  // null = still loading, true = loaded, false = never loaded (likely framing blocked)
  const [loaded, setLoaded] = useState<boolean | null>(null);
  const timerRef = useRef<number | undefined>(undefined);

  useEffect(() => {
    document.title = "API Documentation | NOKAS Integration";
  }, []);

  useEffect(() => {
    // Cross-origin framing failures are invisible to us - the load event simply
    // never fires. Treat "no load within the timeout" as blocked.
    timerRef.current = window.setTimeout(() => {
      setLoaded((current) => (current === null ? false : current));
    }, LOAD_TIMEOUT_MS);

    return () => window.clearTimeout(timerRef.current);
  }, []);

  function handleLoad() {
    window.clearTimeout(timerRef.current);
    setLoaded(true);
  }

  return (
    <PublicLayout>
      {/* ---------------- Header ---------------- */}
      <section className="border-b border-gray-200 bg-gradient-to-b from-brand-50 to-white px-6 py-12">
        <div className="mx-auto max-w-6xl">
          <p className="text-xs font-semibold uppercase tracking-widest text-brand-600">
            Developer reference
          </p>
          <div className="mt-2 flex flex-col gap-5 md:flex-row md:items-end md:justify-between">
            <div>
              <h1 className="text-3xl font-bold tracking-tight text-brand-800">
                Bag Registration API
              </h1>
              <p className="mt-2 max-w-2xl text-sm leading-relaxed text-gray-600">
                Submit bag registrations and retrieve counting results. The interactive reference
                below is generated directly from the live API.
              </p>
            </div>

            <div className="flex shrink-0 flex-wrap gap-2">
              <a
                href={SWAGGER_UI}
                target="_blank"
                rel="noopener noreferrer"
                className="inline-flex items-center gap-1.5 rounded-full bg-brand-800 px-5 py-2 text-sm font-semibold text-white transition hover:bg-brand-700"
              >
                Open full view
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" className="h-3.5 w-3.5" aria-hidden="true">
                  <path d="M14 5h5v5M19 5l-8 8M11 5H6a1 1 0 0 0-1 1v12a1 1 0 0 0 1 1h12a1 1 0 0 0 1-1v-5" strokeLinecap="round" strokeLinejoin="round" />
                </svg>
              </a>
              <a
                href={SWAGGER_JSON}
                target="_blank"
                rel="noopener noreferrer"
                className="rounded-full border border-brand-300 bg-white px-5 py-2 text-sm font-semibold text-brand-800 transition hover:bg-brand-50"
              >
                Download OpenAPI JSON
              </a>
              <a
                href={POSTMAN_COLLECTION}
                download
                className="rounded-full border border-brand-300 bg-white px-5 py-2 text-sm font-semibold text-brand-800 transition hover:bg-brand-50"
              >
                Download Postman Collection
              </a>
            </div>
          </div>

          {/* Quick facts */}
          <dl className="mt-8 grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
            {quickFacts.map((f) => (
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
                Interactive reference
              </span>
              {loaded === true && (
                <span className="inline-flex items-center gap-1.5 text-xs font-medium text-emerald-700">
                  <span className="h-1.5 w-1.5 rounded-full bg-emerald-500" />
                  Live
                </span>
              )}
            </div>

            <div className="relative h-[70vh] min-h-[520px] w-full">
              {loaded !== false && (
                <iframe
                  title="NOKAS Bag Registration API — Swagger UI"
                  src={SWAGGER_UI}
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
                        href={SWAGGER_UI}
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
