import { useEffect, useState } from "react";

const SWAGGER_URL = "https://api-test.nokas.com/swagger/index.html";
const DEVELOPER_PORTAL_URL = "https://apim-avarn-dev-test.developer.azure-api.net/";

export default function SwaggerSection() {
  // Both panels visible by default
  const [swaggerVisible, setSwaggerVisible] = useState<boolean>(true);
  const [instructionsExpanded, setInstructionsExpanded] = useState<boolean>(true);
  const [iframeLoaded, setIframeLoaded] = useState<boolean | null>(null);

  useEffect(() => {
    document.title = "API Guide | Swagger & Developer Portal";
  }, []);

  return (
    <section className="space-y-8">
      {/* Swagger Panel */}
      <div className="space-y-4">
        <div className="flex items-center justify-between">
          <h2 className="text-xl font-semibold">Swagger UI (Embedded)</h2>
          <button
            onClick={() => setSwaggerVisible((p) => !p)}
            className="text-sm px-3 py-1 bg-blue-600 text-white rounded-md hover:bg-blue-700"
          >
            {swaggerVisible ? "Hide Swagger" : "Show Swagger"}
          </button>
        </div>

        {swaggerVisible && (
          <div className="h-[70vh] bg-white border rounded-lg overflow-hidden flex flex-col">
            <iframe
              title="Nokas Swagger UI"
              src={SWAGGER_URL}
              className="w-full h-full border-0"
              sandbox="allow-same-origin allow-scripts allow-forms allow-popups"
              onLoad={() => setIframeLoaded(true)}
            />

            {iframeLoaded === null && (
              <div className="absolute inset-0 flex items-center justify-center bg-white/60">
                <div className="text-center space-y-2">
                  <div className="text-sm text-gray-700">Loading embedded Swagger...</div>
                  <div className="text-xs text-gray-500">
                    If it doesn’t appear,{" "}
                    <a
                      href={SWAGGER_URL}
                      target="_blank"
                      rel="noopener noreferrer"
                      className="text-blue-600 underline"
                    >
                      open Swagger in a new tab
                    </a>
                    .
                  </div>
                </div>
              </div>
            )}

            <div className="px-4 py-3 border-t text-sm flex items-center justify-between">
              <div className="text-gray-600">
                If the embedded view is blocked, open Swagger in a new tab.
              </div>
              <a
                href={SWAGGER_URL}
                target="_blank"
                rel="noopener noreferrer"
                className="inline-block text-sm px-3 py-1 border rounded hover:bg-gray-50"
              >
                Open Swagger
              </a>
            </div>
          </div>
        )}
      </div>

      {/* Developer Portal Instructions */}
      <div className="space-y-4">
        <div className="flex items-center justify-between">
          <h2 className="text-xl font-semibold">Developer Portal Instructions</h2>
          <button
            onClick={() => setInstructionsExpanded((p) => !p)}
            className="text-sm px-3 py-1 border rounded-md hover:bg-gray-50"
          >
            {instructionsExpanded ? "Hide Instructions" : "Show Instructions"}
          </button>
        </div>

        {instructionsExpanded && (
          <div className="p-6 bg-white border rounded-lg space-y-4">
            <p className="text-gray-700">
              In addition to the embedded Swagger view, you can also use the{" "}
              <strong>Nokas Developer Portal</strong> for self-service access to API
              documentation, testing, and onboarding.
            </p>

            <h3 className="font-semibold">Getting Started</h3>
            <ol className="list-decimal list-inside space-y-2 text-gray-700">
              <li>
                Visit{" "}
                <a
                  href={DEVELOPER_PORTAL_URL}
                  target="_blank"
                  rel="noopener noreferrer"
                  className="text-blue-600 underline"
                >
                  Nokas Developer Portal
                </a>
                .
              </li>
              <li>Sign up for a developer account (registration may require admin approval).</li>
              <li>
                Once logged in, navigate to <strong>Nokas Bag Registration API</strong> in the API
                section.
              </li>
              <li>Explore operations, request/response schemas, and example payloads.</li>
              <li>
                To test an operation:
                <ul className="list-disc list-inside ml-6 space-y-1">
                  <li>Select the desired operation.</li>
                  <li>
                    In the <strong>Authorization</strong> section, choose <code>OAuth 2.0</code> with{" "}
                    <code>client_credentials</code> flow (already configured in the portal).
                  </li>
                  <li>Click <strong>Send</strong> to execute the request.</li>
                </ul>
              </li>
            </ol>

            <p className="text-gray-600 text-sm">
              Note: The Developer Portal is pre-configured with OAuth 2.0 credentials, so you can
              authenticate and try APIs without a subscription key.
            </p>
          </div>
        )}
      </div>
    </section>
  );
}
