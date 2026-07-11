import CodeBlock from "../../../components/ui/CodeBlock";
import SectionLayout from "../../../components/ui/SectionLayout";

export default function ErrorsSection() {
  return (
    <SectionLayout
      id="error-handling"
      title="Error Handling & Logging"
      subtitle="How our API communicates and traces errors clearly"
    >
      <div className="space-y-10 text-sm text-gray-800">


        {/* 1. Business Logic Errors */}
        <div>
          <h3 className="text-lg font-semibold text-[#014737]">
            1. Business Logic Errors
          </h3>
          <p>
            Domain-level logic errors such as duplicate bags, missing data, or invalid states return meaningful HTTP
            codes like <code>400 Bad Request</code>, <code>404 Not Found</code>, or <code>409 Conflict</code>.
          </p>

          <h4 className="mt-4 font-semibold text-[#014737]">Example 1: Bag Already Exists (409 Conflict)</h4>
          <CodeBlock
            language="json"
            code={`{
  "customerRefNumber": "5790002334764",
  "bagNumber": "34567890",
  "description": "Bag 34567890 already exists and cannot be re-registered.",
  "registrationStatus": "AlreadyExists",
  "bagLifecycleId": "171349e2-6aa3-424e-a708-20cc94f03ae2",
  "requestCorrelationId": "5e3ff647-b06f-4725-9ec7-8e6dfce18fbG"
}`}
          />

          <h4 className="mt-4 font-semibold text-[#014737]">
            Example 2: Missing Required Field – TotalOrderAmount (400 Bad Request)
          </h4>
          <CodeBlock
            language="json"
            code={`{
  "errors": {
    "BagRegistration.TotalOrderAmount": [
      "totalOrderAmount is required when registrationType is NOK."
    ]
  },
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "traceId": "00-2791ac1a7f81eaeb75c20b102e4e694d-2bdcf9c47fceb5b5-00"
}`}
          />

          <h4 className="mt-4 font-semibold text-[#014737]">
            Example 3: Missing RegistrationType Field (400 Bad Request)
          </h4>
          <CodeBlock
            language="json"
            code={`{
  "errors": {
    "BagRegistration.RegistrationType": [
      "The RegistrationType field is required."
    ]
  },
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "traceId": "00-cff96ea89b5e9c73150f403434027a85-f021792aec19711c-00"
}`}
          />

          <h4 className="mt-4 font-semibold text-[#014737]">
            Example 4: Bag Not Found during Registration GET (404 Not Found)
          </h4>
          <CodeBlock
            language="json"
            code={`{
  "customerRefNumber": "NA",
  "bagNumber": "NA",
  "description": "No bag found with the BagNumber: 3456789234 for this client with Id: 1a732120-1149-4d97-98b5-d454d144b383.",
  "registrationStatus": "NA",
  "bagLifecycleId": "NA",
  "requestCorrelationId": "5e3ff647-b06f-4725-9ec7-8e6dfce18fbf"
}`}
          />
        </div>

        {/* 2. Cancellation Errors */}
        <div>
          <h3 className="text-lg font-semibold text-[#014737]">2. Cancellation Errors</h3>
          <p>
            Cancellation operations validate the lifecycle of the bag. Errors occur when cancellation is requested for
            invalid or already processed bags.
          </p>

          <h4 className="mt-4 font-semibold text-[#014737]">
            Example 1: Bag Already Processed (400 Bad Request)
          </h4>
          <CodeBlock
            language="json"
            code={`{
  "customerRefNumber": "5790002334764",
  "bagNumber": "34567894",
  "description": "Bag already processed and cannot be cancelled at this stage.",
  "registrationStatus": "picked-up",
  "bagLifecycleId": "657bdf7b-e8cc-45be-92a1-d08032f5316f",
  "requestCorrelationId": "5e3ff647-b06f-4725-9ec7-8e6dfce18fbf"
}`}
          />

          <h4 className="mt-4 font-semibold text-[#014737]">
            Example 2: Bag Not Registered (404 Not Found)
          </h4>
          <CodeBlock
            language="json"
            code={`{
  "customerRefNumber": "NA",
  "bagNumber": "NA",
  "description": "No bag found with the BagNumber: 34567894456 for this client with Id: 1a732120-1149-4d97-98b5-d454d144b383.",
  "registrationStatus": "NA",
  "bagLifecycleId": "NA",
  "requestCorrelationId": "5e3ff647-b06f-4725-9ec7-8e6dfce18fbf"
}`}
          />
        </div>

        {/* 3. Counting Result Errors */}
        <div>
          <h3 className="text-lg font-semibold text-[#014737]">
            3. Counting Result Errors
          </h3>
          <p>
            When attempting to fetch counting results for bags that do not exist or are not registered, a{" "}
            <code>404 Not Found</code> is returned.
          </p>
          <CodeBlock
            language="json"
            code={`{
  "customerRefNumber": "NA",
  "bagNumber": "NA",
  "description": "No bag found with the BagNumber: 345678912334 for this client with Id: 1a732120-1149-4d97-98b5-d454d144b383.",
  "registrationStatus": "NA",
  "bagLifecycleId": "NA",
  "requestCorrelationId": "5e3ff647-b06f-4725-9ec7-8e6dfce18fbf"
}`}
          />
        </div>

        {/* 4. Authentication & Authorization */}
        <div>
          <h3 className="text-lg font-semibold text-[#014737]">
            4. Authentication & Authorization
          </h3>
          <p>
            If your token is missing, invalid, or expired, the API will return a <code>401 Unauthorized</code> response.
            Ensure that your token is refreshed and sent in the request header.
          </p>
          <CodeBlock
            language="json"
            code={`{
  "statusCode": 401,
  "message": "Unauthorized"
}`}
          />
        </div>

        {/* 5. Rate Limiting */}
        <div>
          <h3 className="text-lg font-semibold text-[#014737]">5. Rate Limiting</h3>
          <p>
            To maintain system stability and prevent abuse, the API enforces a rate limit of{" "}
            <strong>200 requests per 60 seconds</strong>. Exceeding this limit triggers a <code>429 Too Many Requests</code> response.
          </p>
          <CodeBlock
            language="json"
            code={`{
  "statusCode": 429,
  "message": "Rate limit is exceeded. Try again in 58 seconds."
}`}
          />
        </div>

        {/* 6. Traceability */}
        <div>
          <h3 className="text-lg font-semibold text-[#014737]">
            6. Traceability with Correlation ID
          </h3>
          <p>
            Every request can include an <code>X-Correlation-ID</code> header. This ID is returned in responses and logs,
            helping trace requests end-to-end across distributed systems.
          </p>
        </div>

        {/* 7. HTTP Status Overview */}
        <div>
          <h3 className="text-lg font-semibold text-[#014737]">
            7. HTTP Status Code Overview
          </h3>
          <table className="w-full text-left text-sm border">
            <thead>
              <tr className="bg-gray-100">
                <th className="p-2">HTTP Code</th>
                <th className="p-2">Description</th>
              </tr>
            </thead>
            <tbody>
              <tr><td className="p-2">200 OK</td><td className="p-2">Request was successful</td></tr>
              <tr><td className="p-2">400 Bad Request</td><td className="p-2">Invalid or missing data fields</td></tr>
              <tr><td className="p-2">401 Unauthorized</td><td className="p-2">Invalid or missing access token</td></tr>
              <tr><td className="p-2">404 Not Found</td><td className="p-2">Requested bag or resource not found</td></tr>
              <tr><td className="p-2">409 Conflict</td><td className="p-2">Duplicate bag or logical constraint violation</td></tr>
              <tr><td className="p-2">429 Too Many Requests</td><td className="p-2">Rate limit exceeded</td></tr>
              <tr><td className="p-2">500 Internal Server Error</td><td className="p-2">Unexpected server-side failure</td></tr>
            </tbody>
          </table>
        </div>
      </div>
    </SectionLayout>
  );
}
