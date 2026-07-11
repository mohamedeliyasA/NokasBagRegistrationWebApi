import { useEffect } from "react";

export default function AuthSection() {
  useEffect(() => {
    document.title = "API Guide | Authentication";
  }, []);

  const jsonTokenResponse = {
    token_type: "Bearer",
    expires_in: 3599,
    ext_expires_in: 3599,
    access_token: "eyJ0eXAiOiJKV1QiLCJhbGciOi...",
  };

  return (
    <section className="space-y-6 text-sm text-muted-foreground">
      <h2 className="text-xl font-semibold text-gray-800">
        Authentication & Security
      </h2>

      <div>
        <h3 className="text-lg font-semibold text-gray-700">
          1. Importance of Authentication
        </h3>
        <p>
          In today's digital landscape, securing API access is paramount. The
          Nokas Cash Handling API employs
          <strong> OAuth 2.0</strong>, an industry-standard protocol designed to
          provide secure token-based authentication. This ensures only
          authorized vendors can access the API, protecting sensitive
          operational data.
        </p>
      </div>

      <div>
        <h3 className="text-lg font-semibold text-gray-700">
          2. Token Security
        </h3>
        <p>
          Vendors must protect their access tokens and always transmit them over{" "}
          <strong>HTTPS</strong>. This prevents interception and maintains
          confidentiality during API communications.
        </p>
      </div>

      <div>
        <h3 className="text-lg font-semibold text-gray-700">
          3. Obtaining Client ID & Secret
        </h3>
        <p>
          To get started, vendors must register with Nokas to obtain
          credentials:
        </p>
        <ul className="list-disc list-inside ml-4">
          <li>Contact Nokas Customer Support to initiate registration.</li>
          <li>
            Submit key application details:
            <ul className="list-disc list-inside ml-5">
              <li>
                <strong>Application Name:</strong> Your app's name
              </li>
              <li>
                <strong>Description:</strong> What your application does
              </li>
              <li>
                <strong>Intended Use:</strong> How you'll use the API
              </li>
            </ul>
          </li>
          <li>The application will be reviewed for security and compliance.</li>
          <li>
            If approved, you'll receive:
            <ul className="list-disc list-inside ml-5">
              <li>
                <strong>Client ID</strong> – public identifier
              </li>
              <li>
                <strong>Client Secret</strong> – private, keep this safe
              </li>
            </ul>
          </li>
        </ul>
      </div>

      <div>
        <h3 className="text-lg font-semibold text-gray-700">
          4. Requesting an Access Token
        </h3>
        <p>
          Once you have credentials, request an access token using the following
          values:
        </p>

       <div className="space-y-4 mt-4">
  {/* Method & URL */}
  <div>
    <label className="block text-xs font-semibold text-gray-600 uppercase mb-1">Method & URL</label>
    <div className="bg-black text-xs font-mono p-3 rounded text-white leading-relaxed overflow-x-auto">
      <span className="text-pink-400">POST</span>{" "}
      <span className="text-blue-300">https://api-test.nokas.com/oauth2/v2.0/token</span>
    </div>
  </div>

  {/* Headers */}
  <div>
    <label className="block text-xs font-semibold text-gray-600 uppercase mb-1">Headers</label>
    <div className="bg-black text-xs font-mono p-3 rounded text-white leading-relaxed overflow-x-auto">
      <span className="text-yellow-300">Content-Type</span>: <span className="text-green-400">application/x-www-form-urlencoded</span>
    </div>
  </div>

  {/* Body */}
  <div>
    <label className="block text-xs font-semibold text-gray-600 uppercase mb-1">Body</label>
    <div className="bg-black text-xs font-mono p-3 rounded text-white leading-relaxed overflow-x-auto">
      <div><span className="text-yellow-300">grant_type</span>=<span className="text-green-400">client_credentials</span></div>
      <div><span className="text-yellow-300">client_id</span>=<span className="text-green-400">YOUR_CLIENT_ID</span></div>
      <div><span className="text-yellow-300">client_secret</span>=<span className="text-green-400">YOUR_CLIENT_SECRET</span></div>
      <div><span className="text-yellow-300">scope</span>=<span className="text-green-400">https://nokasb2ctest.onmicrosoft.com/api/nokas/.default</span></div>
    </div>
  </div>
</div>

        <p className="text-gray-700 font-medium mt-4">
          Success Response (200 OK)
        </p>
        <div className="bg-black text-xs font-mono p-3 rounded leading-relaxed overflow-x-auto">
          <div>
            <span className="text-teal-400">{"{"}</span>
          </div>
          <div className="pl-4">
            <div>
              <span className="text-yellow-300">"token_type"</span>:{" "}
              <span className="text-green-400">"Bearer"</span>,
            </div>
            <div>
              <span className="text-yellow-300">"expires_in"</span>:{" "}
              <span className="text-blue-300">3599</span>,
            </div>
            <div>
              <span className="text-yellow-300">"ext_expires_in"</span>:{" "}
              <span className="text-blue-300">3599</span>,
            </div>
            <div>
              <span className="text-yellow-300">"access_token"</span>:{" "}
              <span className="text-green-400">
                "eyJ0eXAiOiJKV1QiLCJhbGciOi..."
              </span>
            </div>
          </div>
          <div>
            <span className="text-teal-400">{"}"}</span>
          </div>
        </div>

        <p className="mt-2">
          <strong>access_token</strong> is required in every secured API
          request. It expires in the given duration (in seconds).
        </p>

        <p className="mt-2">
  The <strong>access_token</strong> is valid for <strong>60 minutes</strong> (3599 seconds). Consumers are advised to reuse
  this token for all authorized API calls within that window, rather than requesting a new token for every call.
  This practice helps reduce authentication overhead and improves performance and resource usage on both client and server sides.
</p>

        <p className="mt-2">Expected Status Codes:</p>
        <ul className="list-disc list-inside ml-4">
          <li>
            <strong>200 OK</strong> – Token successfully issued
          </li>
          <li>
            <strong>400 Bad Request</strong> – Missing or invalid parameters
          </li>
          <li>
            <strong>401 Unauthorized</strong> – Incorrect client credentials
          </li>
        </ul>
      </div>

      <div>
        <h3 className="text-lg font-semibold text-gray-700">
          5. Using the Access Token
        </h3>
        <p>
          Include the token in the <strong>Authorization</strong> header for
          every request:
        </p>
        <pre className="bg-gray-100 p-3 rounded text-xs font-mono text-gray-800 overflow-x-auto">{`
Authorization: Bearer YOUR_ACCESS_TOKEN
        `}</pre>
        <p>
          This ensures every call is securely authenticated against the Nokas
          API.
        </p>
      </div>
    </section>
  );
}
