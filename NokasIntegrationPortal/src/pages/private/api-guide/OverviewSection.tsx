export default function OverviewSection() {
  return (
    <section className="space-y-8 text-sm text-muted-foreground">
      <h2 className="text-xl font-semibold text-gray-800">
        Nokas Cash Handling – Cloud API Specification
      </h2>

      {/* Document Info */}
      <div className="overflow-x-auto border rounded bg-gray-50">
        <table className="w-full border-collapse text-sm">
          <thead className="bg-gray-100">
            <tr>
              <th className="border px-4 py-2 text-left">Version</th>
              <th className="border px-4 py-2 text-left">Purpose/Change</th>
              <th className="border px-4 py-2 text-left">Author</th>
              <th className="border px-4 py-2 text-left">Date</th>
            </tr>
          </thead>
          <tbody>
            <tr>
              <td className="border px-4 py-2">1.0</td>
              <td className="border px-4 py-2">Initial Draft</td>
              <td className="border px-4 py-2">Mohamed Eliyas Anver Deen</td>
              <td className="border px-4 py-2">30 Jun 2025</td>
            </tr>
              <tr>
              <td className="border px-4 py-2">2.0</td>
              <td className="border px-4 py-2">First Roll Out</td>
              <td className="border px-4 py-2">Mohamed Eliyas Anver Deen</td>
              <td className="border px-4 py-2">05 Sep 2026</td>
            </tr>
          </tbody>
        </table>
      </div>

      {/* Overview */}
      <div>
        <h3 className="text-md font-semibold text-gray-700 mb-2">
          📘 Document Overview
        </h3>
        <p>
          This document outlines the functional overview of Nokas Cash
          Handling’s public-facing cloud API. The API enables authorized vendors
          to digitally register cash bags and retrieve their counting results
          securely and efficiently.
        </p>
      </div>

      {/* About Nokas */}
      <div>
        <h3 className="text-md font-semibold text-gray-700 mb-2">
          🏢 About Nokas Cash Handling
        </h3>
        <ul className="list-disc list-inside space-y-1">
          <li>
            Nokas Cash Handling specializes in the secure management of physical
            cash for businesses, banks, and public institutions
          </li>
          <li>
            Clients benefit from safe cash transport, accurate counting, and
            direct bank deposits
          </li>
          <li>Accurate cash counting and reconciliation</li>
        </ul>
        <p className="mt-2">
          To enhance operational efficiency and vendor transparency, Nokas
          introduces a RESTful API enabling system-to-system integration to
          seamlessly interact with Nokas' services for Cash Bag Registration and
          Cash Bag Counting Result, enhancing transparency and speeding up cash
          handling operations.
        </p>
      </div>

      {/* Purpose */}
      <div>
        <h3 className="text-md font-semibold text-gray-700 mb-2">
          🎯 Purpose of the API
        </h3>
        <ul className="list-disc list-inside space-y-1">
          <li>
            <strong>Bag Registration:</strong> Vendors and Partners will be able
            to do bag registration via the API, providing necessary details in
            the bag registration request payload.
          </li>
          <li>
            <strong>Bag Counting Results:</strong> Vendors and Partners can
            retrieve the counting results for their registered bags through the
            API post-verification from Nokas.
          </li>
        </ul>
      </div>

      {/* Core Functionality */}
      <div>
        <h3 className="text-md font-semibold text-gray-700 mb-2">
          🔑 Core Functionalities
        </h3>
        <div className="overflow-x-auto border rounded">
          <table className="w-full border-collapse text-left text-sm">
            <thead className="bg-gray-100">
              <tr>
                <th className="border px-4 py-2">Function</th>
                <th className="border px-4 py-2">Description</th>
              </tr>
            </thead>
            <tbody>
              <tr>
                <td className="border px-4 py-2">Register Bag</td>
                <td className="border px-4 py-2">
                  Allows vendors to register cash bags by sending structured
                  payloads in JSON or XML.
                </td>
              </tr>
              <tr>
                <td className="border px-4 py-2">Retrieve Count Result</td>
                <td className="border px-4 py-2">
                  Enables vendors to fetch the verified count results of
                  previously registered cash bags.
                </td>
              </tr>
              <tr>
                <td className="border px-4 py-2">Track Operations</td>
                <td className="border px-4 py-2">
                  Every operation is logged with correlation IDs for full
                  lifecycle traceability.
                </td>
              </tr>
              <tr>
                <td className="border px-4 py-2">Secure Access</td>
                <td className="border px-4 py-2">
                  Secured via Azure AD B2C OAuth2 token-based authentication
                  using <code>client_id</code> and <code>client_secret</code>.
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </section>
  );
}
