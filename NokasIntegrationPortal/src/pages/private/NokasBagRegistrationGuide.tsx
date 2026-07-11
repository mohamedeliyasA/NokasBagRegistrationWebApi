// src/pages/private/NokasBagRegistrationGuide.tsx
import { useEffect, useState } from "react";
import { Card } from "../../components/ui/card";
import { ScrollArea } from "../../components/ui/scroll-area";
import { Separator } from "../../components/ui/separator";

const sections = [
  { id: "overview", label: "Overview", icon: "📘" },
  { id: "architecture", label: "Architecture", icon: "🧩" },
  { id: "auth", label: "Authentication", icon: "🔐" },
  { id: "usecases", label: "Use Cases", icon: "📂" },
  { id: "endpoints", label: "Endpoints", icon: "🧭" },
  { id: "format", label: "Formats", icon: "📄" },
  { id: "errors", label: "Errors", icon: "❗" },
  { id: "swagger", label: "Swagger", icon: "🧪" },
];

export default function NokasBagRegistrationGuide() {
  const [active, setActive] = useState("overview");

  useEffect(() => {
    document.title = "Nokas API Guide";
  }, []);

  return (
    <div className="flex h-full w-full">
      {/* Sidebar */}
      <aside className="w-16 sm:w-48 border-r bg-white p-2 flex flex-col items-center sm:items-stretch">
        {sections.map((s) => (
          <button
            key={s.id}
            onClick={() => setActive(s.id)}
            className={`flex items-center sm:justify-start gap-2 w-full p-2 rounded hover:bg-gray-100 transition text-sm font-medium ${
              active === s.id ? "bg-gray-200" : ""
            }`}
          >
            <span className="text-lg">{s.icon}</span>
            <span className="hidden sm:inline">{s.label}</span>
          </button>
        ))}
      </aside>

      {/* Main Content */}
      <main className="flex-1 overflow-auto p-4">
        <Card className="max-w-5xl mx-auto p-6">
          <h1 className="text-2xl font-bold mb-4 text-primary">Nokas API Guide</h1>

          {active === "overview" && (
            <section>
              <h2 className="text-xl font-semibold mb-2">Overview</h2>
              <p className="text-muted-foreground text-sm">
                This API allows secure registration, management, and tracking of Nokas cash bag deliveries.
              </p>
            </section>
          )}

          {active === "architecture" && (
            <section>
              <h2 className="text-xl font-semibold mb-2">Architecture Diagram</h2>
              <img
                src="/images/architecture-placeholder.png"
                alt="Architecture Diagram"
                className="rounded-xl border w-full max-w-3xl"
              />
            </section>
          )}

          {active === "auth" && (
            <section>
              <h2 className="text-xl font-semibold mb-2">Authentication</h2>
              <p className="text-muted-foreground text-sm mb-2">
                Azure AD B2C using OAuth2 client credentials grant.
              </p>
              <pre className="bg-muted p-3 rounded text-xs overflow-x-auto">{`
POST /oauth2/v2.0/token
Content-Type: application/x-www-form-urlencoded

grant_type=client_credentials&client_id=YOUR_CLIENT_ID&client_secret=YOUR_SECRET&scope=YOUR_SCOPE
              `}</pre>
              <p className="text-sm mt-2">Header: <code>Authorization: Bearer &lt;token&gt;</code></p>
            </section>
          )}

          {active === "usecases" && (
            <section>
              <h2 className="text-xl font-semibold mb-2">Core Use Cases</h2>
              <ul className="list-disc list-inside text-sm text-muted-foreground">
                <li>Register a new cash bag</li>
                <li>Update existing registration</li>
                <li>Delete a registration</li>
                <li>Track bag status</li>
              </ul>
            </section>
          )}

          {active === "endpoints" && (
            <section>
              <h2 className="text-xl font-semibold mb-2">Endpoint Summary</h2>
              <ul className="text-sm space-y-1 text-muted-foreground">
                <li><strong>POST</strong> /api/cashbag/register</li>
                <li><strong>GET</strong> /api/cashbag/{`{bagNumber}`}</li>
                <li><strong>PUT</strong> /api/cashbag/{`{bagNumber}`}</li>
                <li><strong>DELETE</strong> /api/cashbag/{`{bagNumber}`}</li>
              </ul>
            </section>
          )}

          {active === "format" && (
            <section>
              <h2 className="text-xl font-semibold mb-2">Sample Payload</h2>
              <pre className="bg-muted p-3 rounded text-xs overflow-x-auto">{`
{
  "BagRegistration": {
    "CustomerNumber": "101234",
    "BagNumber": "BAG12345",
    "TurnoverDate": "2025-06-28",
    "RegisteredBy": "system"
  },
  "CustomerCountry": "NO"
}`}</pre>
            </section>
          )}

          {active === "errors" && (
            <section>
              <h2 className="text-xl font-semibold mb-2">Error Handling</h2>
              <pre className="bg-muted p-3 rounded text-xs overflow-x-auto">{`
{
  "status": "error",
  "error_code": "DUPLICATE_BAG",
  "error_message": "Bag number already exists.",
  "correlationId": "uuid-1234"
}`}</pre>
            </section>
          )}

          {active === "swagger" && (
            <section>
              <h2 className="text-xl font-semibold mb-2">Swagger & Testing</h2>
              <p className="text-sm text-muted-foreground mb-2">
                Access Swagger at <code>/swagger</code> (post-login).
              </p>
              <p className="text-sm">
                Or use <a href="https://editor.swagger.io" target="_blank" className="text-blue-600 underline">Swagger Editor</a>
              </p>
            </section>
          )}
        </Card>
      </main>
    </div>
  );
}
