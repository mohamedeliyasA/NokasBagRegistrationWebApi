import { useEffect, useRef, useState } from "react";
import { Callout, InlineCode, PageHeader, SectionHeading } from "@/components/ui";
import { TEST_ENV } from "@/config/environments";

const SWAGGER_URL = TEST_ENV.swaggerUi;
const DEVELOPER_PORTAL_URL = "https://apim-avarn-dev-test.developer.azure-api.net/";

const LOAD_TIMEOUT_MS = 8000;

export default function SwaggerSection() {
  const [loaded, setLoaded] = useState<boolean | null>(null);
  const timerRef = useRef<number | undefined>(undefined);

  useEffect(() => {
    document.title = "API Guide | Swagger & Developer Portal";
  }, []);

  useEffect(() => {
    // A cross-origin framing block never fires `load`, so fall back on a timeout.
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
    <section className="space-y-8">
      <PageHeader
        eyebrow="API Guide"
        title="Swagger & Developer Portal"
        lead="Try the API interactively — either through the embedded Swagger reference below, or via the self-service Developer Portal."
      />

      {/* ---------------- Embedded Swagger ---------------- */}
      <div className="space-y-4">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <SectionHeading id="swagger" icon="🧪">
            Interactive reference
          </SectionHeading>
        </div>

        <div className="overflow-hidden rounded-xl border border-gray-200 bg-white shadow-card">
          <div className="flex items-center justify-between border-b border-gray-200 bg-gray-50 px-4 py-2.5">
            <span className="text-xs font-semibold uppercase tracking-wider text-gray-500">
              Swagger UI
            </span>
            <div className="flex items-center gap-3">
              {loaded === true && (
                <span className="inline-flex items-center gap-1.5 text-xs font-medium text-emerald-700">
                  <span className="h-1.5 w-1.5 rounded-full bg-emerald-500" />
                  Live
                </span>
              )}
              <a
                href={SWAGGER_URL}
                target="_blank"
                rel="noopener noreferrer"
                className="inline-flex items-center gap-1.5 text-xs font-semibold text-brand-700 transition hover:text-brand-900"
              >
                Open in new tab
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" className="h-3.5 w-3.5" aria-hidden="true">
                  <path d="M14 5h5v5M19 5l-8 8M11 5H6a1 1 0 0 0-1 1v12a1 1 0 0 0 1 1h12a1 1 0 0 0 1-1v-5" strokeLinecap="round" strokeLinejoin="round" />
                </svg>
              </a>
            </div>
          </div>

          {/* `relative` is required for the overlays below to position correctly. */}
          <div className="relative h-[70vh] min-h-[480px] w-full">
            {loaded !== false && (
              <iframe
                title="NOKAS Swagger UI"
                src={SWAGGER_URL}
                onLoad={handleLoad}
                className="h-full w-full border-0"
                sandbox="allow-same-origin allow-scripts allow-forms allow-popups"
              />
            )}

            {loaded === null && (
              <div className="pointer-events-none absolute inset-0 flex items-center justify-center bg-white/80">
                <div className="text-center">
                  <span className="mx-auto block h-7 w-7 animate-spin rounded-full border-2 border-brand-200 border-t-brand-700" />
                  <p className="mt-3 text-sm font-medium text-gray-700">Loading Swagger…</p>
                </div>
              </div>
            )}

            {loaded === false && (
              <div className="absolute inset-0 flex items-center justify-center p-8">
                <div className="max-w-md text-center">
                  <h3 className="text-base font-semibold text-gray-900">Embedded view unavailable</h3>
                  <p className="mt-2 text-sm leading-relaxed text-gray-600">
                    Swagger did not load in time — the browser may have blocked framing, or the API
                    is unreachable from your network.
                  </p>
                  <div className="mt-5 flex flex-wrap justify-center gap-2">
                    <a
                      href={SWAGGER_URL}
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
      </div>

      {/* ---------------- Developer portal ---------------- */}
      <div className="space-y-4">
        <SectionHeading id="developer-portal" icon="🚪">
          Developer Portal
        </SectionHeading>

        <p className="text-sm leading-relaxed text-gray-600">
          Alongside the embedded reference, the NOKAS Developer Portal offers self-service access to
          documentation, testing and onboarding.
        </p>

        <ol className="space-y-3">
          {[
            {
              title: "Visit the Developer Portal",
              body: (
                <>
                  Open{" "}
                  <a
                    href={DEVELOPER_PORTAL_URL}
                    target="_blank"
                    rel="noopener noreferrer"
                    className="font-semibold text-brand-700 underline-offset-2 hover:underline"
                  >
                    the portal
                  </a>{" "}
                  in a new tab.
                </>
              ),
            },
            {
              title: "Sign up for a developer account",
              body: <>Registration may require administrator approval.</>,
            },
            {
              title: "Find the API",
              body: (
                <>
                  Once signed in, go to the API section and select{" "}
                  <strong className="text-gray-800">NOKAS Bag Registration API</strong>.
                </>
              ),
            },
            {
              title: "Explore the operations",
              body: <>Review operations, request and response schemas, and example payloads.</>,
            },
            {
              title: "Try a request",
              body: (
                <>
                  Pick an operation, choose <InlineCode>OAuth 2.0</InlineCode> with the{" "}
                  <InlineCode>client_credentials</InlineCode> flow in the Authorization section, then
                  send it.
                </>
              ),
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

        <Callout tone="info" title="No subscription key needed">
          The Developer Portal is pre-configured with OAuth 2.0 credentials, so you can authenticate
          and try the API without managing a subscription key.
        </Callout>
      </div>
    </section>
  );
}
