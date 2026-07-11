// src/pages/PublicSwagger.tsx
import React, { useEffect, useState } from "react";
import PublicLayout from "../../layouts/PublicLayout";
import SwaggerUI from "swagger-ui-react";
import "swagger-ui-react/swagger-ui.css";

type ApiSpec = {
  id: string;
  title: string;
  url: string; // full URL to the swagger.json (or relative path)
  uiUrl?: string; // optional full URL to the hosted swagger UI index.html (iframe target)
  description?: string;
  tag?: string;
};

// Constants for the Bag Registration swagger (as requested)
const BAGREG_SWAGGER_UI = "https://api-test.nokas.com/swagger/index.html";
const BAGREG_SWAGGER_JSON = "https://api-test.nokas.com/swagger/v1/swagger.json";

const specs: ApiSpec[] = [
  {
    id: "bag-registration",
    title: "Nokas Bag Registration Open API",
    url: BAGREG_SWAGGER_JSON,
    uiUrl: BAGREG_SWAGGER_UI,
    description: "Submit bag registrations and retrieve counting results.",
  },
  // add more specs later:
  // {
  //   id: "payments",
  //   title: "Payments API",
  //   url: "https://api-test.nokas.com/docs/payments/swagger/v1/swagger.json",
  //   uiUrl: "https://api-test.nokas.com/docs/payments/swagger/index.html",
  //   description: "Payments endpoints..."
  // }
];

export default function PublicSwagger() {
  const [selected, setSelected] = useState<ApiSpec>(specs[0]);
  const [iframeLoaded, setIframeLoaded] = useState<boolean | null>(null);
  // used to force re-render / re-check when switching specs
  useEffect(() => {
    setIframeLoaded(null);
  }, [selected]);

  useEffect(() => {
    document.title = "API Documentation | Nokas Integration";
  }, []);

  return (
    <PublicLayout>
      <div className="animate-fade-in space-y-6">
        <header className="flex items-center justify-between">
          <div>
            <h1 className="text-2xl font-bold text-[#014737]">API Documentation</h1>
            <p className="text-gray-600">
              Select an API from the left to view its full Swagger UI inline.
            </p>
          </div>
        </header>

        <div className="grid grid-cols-1 lg:grid-cols-6 gap-6">
          {/* Left nav */}
          <nav className="lg:col-span-1 bg-white border border-gray-200 rounded-lg p-4 shadow-sm">
            <h3 className="text-sm font-semibold text-gray-700 mb-3">Available APIs</h3>
            <ul className="space-y-2">
              {specs.map((s) => (
                <li key={s.id}>
                  <button
                    onClick={() => setSelected(s)}
                    className={`w-full text-left p-3 rounded-md transition-colors ${
                      selected.id === s.id
                        ? "bg-[#e6f6ef] border border-[#bfe6d1] text-[#014737] font-semibold"
                        : "hover:bg-gray-50"
                    }`}
                  >
                    <div className="flex items-center justify-between">
                      <div>
                        <div className="text-sm">{s.title}</div>
                        {s.description && (
                          <div className="text-xs text-gray-500">{s.description}</div>
                        )}
                      </div>
                    </div>
                  </button>
                </li>
              ))}
            </ul>
          </nav>

          {/* Main Swagger area */}
          <main className="lg:col-span-5 min-h-[70vh] bg-white border border-gray-200 rounded-lg shadow-sm p-4">
            <div className="flex items-center justify-between mb-3">
              <div>
                <h2 className="text-lg font-semibold text-[#014737]">{selected.title}</h2>
                {selected.description && (
                  <p className="text-sm text-gray-600">{selected.description}</p>
                )}
              </div>

              <div className="flex items-center gap-2">
                {/* Open full hosted swagger UI (if available) */}
                <a
                  href={selected.uiUrl ?? `/docs/${selected.id}/swagger/index.html`}
                  target="_blank"
                  rel="noopener noreferrer"
                  className="px-3 py-1.5 border rounded text-sm"
                >
                  Open full view
                </a>
                {/* Download json */}
                <a
                  href={selected.url}
                  target="_blank"
                  rel="noopener noreferrer"
                  className="px-3 py-1.5 bg-[#014737] text-white rounded text-sm"
                >
                  Download JSON
                </a>
              </div>
            </div>

            {/* Container for embedded Swagger UI (iframe preferred) */}
            <div className="w-full h-[65vh] overflow-hidden border-t relative">
              {selected.uiUrl ? (
                <div className="w-full h-full flex flex-col">
                  {/* iframe area */}
                  <iframe
                    key={selected.uiUrl}
                    title={`${selected.title} Swagger UI`}
                    src={selected.uiUrl}
                    className="w-full h-full border-0"
                    // sandbox helps security; allow-same-origin is required if the host sets same-origin policies,
                    // but note that cross-origin access to iframe content is intentionally blocked by browsers.
                    sandbox="allow-same-origin allow-scripts allow-forms allow-popups"
                    onLoad={() => {
                      // We cannot reliably inspect cross-origin iframe contents, but load fired => likely loaded.
                      setIframeLoaded(true);
                    }}
                  />

                  {/* Overlay messages */}
                  {iframeLoaded === null && (
                    <div className="absolute inset-0 flex items-center justify-center bg-white/70 pointer-events-none">
                      <div className="text-center space-y-2">
                        <div className="text-sm text-gray-700">Loading embedded Swagger...</div>
                        <div className="text-xs text-gray-500">
                          If the iframe is blocked (framing denied by the server), open the hosted
                          Swagger in a new tab.
                        </div>
                      </div>
                    </div>
                  )}

                  {iframeLoaded === false && (
                    <div className="absolute inset-0 flex flex-col items-center justify-center bg-red-50 p-6">
                      <div className="text-center">
                        <div className="font-semibold text-red-700 mb-2">Embedded view blocked</div>
                        <div className="text-sm text-gray-700 mb-4">
                          The remote Swagger UI prevented embedding (common security header:
                          X-Frame-Options or CSP). You can still open the full UI in a new tab.
                        </div>
                        <div className="flex gap-2">
                          <a
                            href={selected.uiUrl}
                            target="_blank"
                            rel="noopener noreferrer"
                            className="px-4 py-2 bg-[#014737] text-white rounded"
                          >
                            Open hosted Swagger
                          </a>
                          <a
                            href={selected.url}
                            target="_blank"
                            rel="noopener noreferrer"
                            className="px-4 py-2 border rounded"
                          >
                            Download JSON
                          </a>
                        </div>
                      </div>
                    </div>
                  )}
                </div>
              ) : (
                // Fallback: if there's no hosted UI url, show embedded Swagger via swagger-ui-react
                <div className="w-full h-full overflow-auto">
                  <SwaggerViewer url={selected.url} />
                </div>
              )}
            </div>
          </main>
        </div>
      </div>
    </PublicLayout>
  );
}

/** Small wrapper for swagger-ui-react to handle errors & show spinner */
function SwaggerViewer({ url }: { url: string }) {
  const [error, setError] = useState<string | null>(null);

  return (
    <div className="w-full h-full">
      {error ? (
        <div className="p-6 text-red-600">
          Failed to load Swagger spec from <code>{url}</code>.
          <div className="mt-2 text-sm text-gray-600">
            Common causes: CORS restrictions or the JSON path is not publicly reachable.
            Ensure the swagger.json is served under the same origin or enable CORS.
          </div>
        </div>
      ) : (
        <SwaggerUIWrapper url={url} onError={(m) => setError(m)} />
      )}
    </div>
  );
}

/** Internal wrapper to mount SwaggerUI and capture load errors */
function SwaggerUIWrapper({
  url,
  onError,
}: {
  url: string;
  onError: (m: string) => void;
}) {
  useEffect(() => {
    onError("");
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [url]);

  try {
    return <SwaggerUI url={url} />;
  } catch (e: any) {
    onError(e?.message ?? "Unknown error rendering Swagger UI");
    return null;
  }
}
