import { useEffect } from "react";
import { Link } from "react-router-dom";

export default function EndpointsSection() {
  useEffect(() => {
    document.title = "API Guide | Endpoints";
  }, []);

  const BASE = "https://api-test.nokas.com/api/cash-mgmt";

  return (
    <section className="space-y-6 text-sm text-muted-foreground">
<h2 className="text-xl font-semibold text-gray-800 mb-4">API Endpoints</h2>
<ul className="list-disc ml-5 text-sm text-gray-800 space-y-3">
  <li>
    📦{" "}
    <a
      href="#bag-declaration"
      className="font-medium underline text-blue-700 hover:text-blue-500"
    >
      Bag Declaration API
    </a>
    <ul className="ml-6 mt-1 text-gray-600 text-sm space-y-1">
      <li className="before:content-['→'] before:mr-2 before:text-gray-500">
        Declare a bag for processing
      </li>
      <li className="before:content-['→'] before:mr-2 before:text-gray-500">
        Retrieve the current processing status of a declared bag
      </li>
      <li className="before:content-['→'] before:mr-2 before:text-gray-500">
        Delete a declared bag if necessary
      </li>
    </ul>
  </li>
  <li>
    📊{" "}
    <a
      href="#bag-counting-result"
      className="font-medium underline text-blue-700 hover:text-blue-500"
    >
      Bag Counting Result API
    </a>
    <ul className="ml-6 mt-1 text-gray-600 text-sm space-y-1">
      <li className="before:content-['→'] before:mr-2 before:text-gray-500">
        Retrieve detailed results of processed and counted bags
      </li>
    </ul>
  </li>
</ul>



      {/* Bag Registration Section */}
      <h2 id="bag-registration" className="text-xl font-semibold text-gray-800">
        📦 Bag Registration API
      </h2>

      <div>
        <h3 className="text-md font-semibold text-gray-700">
          X-Correlation-ID & Request Tracking
        </h3>
        <p>
          All requests <strong>must</strong> include a unique{" "}
          <code>X-Correlation-ID</code> header for traceability. This ID will
          appear as <code>requestCorrelationId</code> in the response. The
          system also returns a <code>bagLifecycleId</code> identifying the cash
          bag.
        </p>
        <p className="mt-2 text-blue-800">
          🔗 For full tracking logic and behavior, visit the{" "}
          <Link
            to="/portal/api-guide/correlation"
            className="underline hover:text-blue-600 font-medium"
          >
            Correlation
          </Link>{" "}
          section.
        </p>
      </div>

      {/* POST */}
      <div>
        <h3 className="text-md font-semibold text-gray-700">
          1. Register a Cash Bag
        </h3>
        <p>
          Endpoint: <code>POST {BASE}/bag-registration</code>
        </p>

        <p className="mt-2 font-medium text-gray-700">Headers</p>
        <div className="bg-black text-xs font-mono p-3 rounded text-white overflow-x-auto">
          <div>
            <span className="text-yellow-300">Authorization</span>:{" "}
            <span className="text-green-400">Bearer &lt;token&gt;</span>
          </div>
          <div>
            <span className="text-yellow-300">Content-Type</span>:{" "}
            <span className="text-green-400">application/json</span>
          </div>
          <div>
            <span className="text-yellow-300">X-Correlation-ID</span>:{" "}
            <span className="text-green-400">uuid</span>
          </div>
        </div>

        <p className="mt-2 font-medium text-gray-700">Sample Request</p>
        <div className="bg-black text-white text-xs p-3 rounded overflow-x-auto font-mono">
          <pre>{`{
    "bagRegistration": {
        "customerRefNumber": "5790002334764",
        "registrationType": "NOK",
        "messageId": "938D35D2-F8C1-4EAE-BB4D-0797E29DBB6A",
        "bagNumber": "2033331011",
        "registrationDate": "2025-08-14",
        "referenceStatement": "kamal",
        "turnOverDate": "2025-08-14",
        "registeredNotes": "1000",
        "registeredCoins": "0",
        "registeredChecks": "0",
        "totalOrderAmount": "10000",
        "bagItemsLC": {
            "seddel1000": "1000",
            "seddel500": "0",
            "seddel200": "0",
            "seddel100": "0",
            "seddel50": "0"
        }
    }
 
}`}</pre>
        </div>

        <p className="mt-2 font-medium text-gray-700">Sample Response</p>
        <div className="bg-black text-white text-xs p-3 rounded overflow-x-auto font-mono">
          <pre>{`{
    "customerRefNumber": "5790002334764",
    "bagNumber": "2033331011",
    "description": "Bag Registered Successfully",
    "registrationStatus": "In-Progress",
    "bagLifecycleId": "287d119a-af9b-4d6a-81f4-23d1c0c4a4b6",
    "requestCorrelationId": "5e3ff647-b06f-4725-9ec7-8e6dfce18fbG"
}`}</pre>
        </div>
      </div>

      {/* GET */}
      <div>
        <h3 className="text-md font-semibold text-gray-700">
          2. Retrieve a Cash Bag
        </h3>
        <p>
          Endpoint: <code>GET {BASE}/{`{bagNumber}`}</code>
        </p>

        <p className="mt-2 font-medium text-gray-700">Sample Response</p>
        <div className="bg-black text-white text-xs p-3 rounded overflow-x-auto font-mono">
          <pre>{`{
  "bagRegistration": {
    "customerRefNumber": "123451",
    "registrationType": "NOK",
    "messageId": "938D35D2-F8C1-4EAE-BB4D-0797E29DBB6A",
    "bagNumber": "394857349859",
    ...
  },
}`}</pre>
        </div>
      </div>

      {/* DELETE */}
      <div>
        <h3 className="text-md font-semibold text-gray-700">
          3. Delete a Cash Bag
        </h3>
        <p>
          Endpoint: <code>DELETE {BASE}/bag-registration/{`{bagNumber}`}</code>
        </p>

        <p className="mt-2 font-medium text-gray-700">Sample Response</p>
        <div className="bg-black text-white text-xs p-3 rounded overflow-x-auto font-mono">
          <pre>{`{
    "customerRefNumber": "5790002334764",
    "bagNumber": "2033331011",
    "description": "Bag Cancelled Successfully.",
    "registrationStatus": "Deleted",
    "bagLifecycleId": "287d119a-af9b-4d6a-81f4-23d1c0c4a4b6",
    "requestCorrelationId": "5e3ff647-b06f-4725-9ec7-8e6dfce18fbf"
}`}</pre>
        </div>
      </div>

      {/* COUNTING RESULT */}
      <h2
        id="counting-result"
        className="text-xl font-semibold text-gray-800 mt-10"
      >
        📊 Counting Result Retrieval API
      </h2>
      <p className="mt-2 text-gray-700">
        Vendors and Partners can retrieve counting results for previously
        registered bags. This typically occurs after verification by Nokas
        operations.
      </p>

      <p className="mt-4 text-md font-semibold text-gray-700">Endpoint</p>
      <p>
        <code>GET {BASE}/bag-countingresult/{`{bagNumber}`}</code>
      </p>

      <p className="mt-4 font-medium text-gray-700">Required Headers</p>
      <div className="bg-black text-xs font-mono p-3 rounded text-white overflow-x-auto">
        <div>
          <span className="text-yellow-300">Authorization</span>:{" "}
          <span className="text-green-400">Bearer &lt;token&gt;</span>
        </div>
        <div>
          <span className="text-yellow-300">Accept</span>:{" "}
          <span className="text-green-400">application/json</span>
        </div>
        <div>
          <span className="text-yellow-300">X-Correlation-ID</span>:{" "}
          <span className="text-green-400">uuid</span>
        </div>
      </div>

     

      <p className="mt-4 font-medium text-gray-700">Sample Response</p>
      <div className="bg-black text-white text-xs p-3 rounded overflow-x-auto font-mono">
        <pre>{`{
    "customerRefNumber": "5790002334764",
    "bagNumber": "34567891",
    "description": "Bag counting result retrieved successfully.",
    "bagStatus": "counted",
    "bagLifecycleId": "1f4fccf7-8b4d-4c70-a83e-d12626e8a5e5",
    "requestCorrelationId": "5e3ff647-b06f-4725-9ec7-8e6dfce18fbf",
    "declaredAmount": 2500.0,
    "actualCountedAmount": 2500.0,
    "deviationAmount": 0.0,
    "countedDate": "2025-11-11T11:59:00"
}`}</pre>
      </div>

      <p className="mt-4 font-medium text-gray-700">Expected Status Codes</p>
      <ul className="list-disc list-inside ml-4">
        <li>
          <strong>200 OK</strong> – Result fetched successfully
        </li>
        <li>
          <strong>404 Not Found</strong> – Bag ID not found
        </li>
        <li>
          <strong>401 Unauthorized</strong> – Invalid or missing token
        </li>
        <li>
          <strong>500 Internal Server Error</strong> – Internal issue
        </li>
      </ul>
    </section>
  );
}
