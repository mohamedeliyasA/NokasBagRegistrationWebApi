import { useEffect } from "react";
import architectureDiagram from "./uploads/architecture-diagram.svg";

export default function ArchitectureSection() {
  useEffect(() => {
    document.title = "API Guide | Architecture";
  }, []);

  return (
    <section className="space-y-6">
      <h2 className="text-xl font-semibold text-gray-800">
        Architecture Overview
      </h2>

      <p className="text-sm text-muted-foreground">
        The Nokas Bag Registration API architecture ensures security and
        performance through OAuth 2.0 token validation, Azure AD integration,
        and a modular backend structure. Below is a visual flow of the
        authentication and API interaction steps used by registered partners.
      </p>

      <ul className="list-decimal list-inside text-sm text-muted-foreground space-y-1 ml-4">
        <li>
          Partners register and receive a Client ID and Secret from Nokas.
        </li>
        <li>Tokens are requested via Azure AD and passed in each API call.</li>
        <li>All requests are validated through Nokas Azure AD policies.</li>
        <li>Authorized requests are routed into the Web API for processing.</li>
        <li>Responses and counting results are returned to the client app.</li>
      </ul>

      <div className="text-sm text-gray-600">
        This architecture supports secure, scalable communication between
        external partners and the Nokas platform.
      </div>

      {/* Embedded architecture image */}
      <div className="mt-6 border rounded bg-gray-50 p-4">
        <h3 className="text-base font-semibold text-gray-700 mb-2">
          Architecture Flow Diagram
        </h3>
        <img
          src={architectureDiagram}
          alt="Nokas Architecture Diagram"
          className="w-full h-auto rounded shadow"
        />
        <p className="text-xs text-gray-500 mt-2">
          This diagram illustrates how partner applications authenticate via
          Azure AD and securely submit data through the Nokas Web API.
        </p>
      </div>
    </section>
  );
}
