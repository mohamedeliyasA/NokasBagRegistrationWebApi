import CodeBlock from "../../../components/ui/CodeBlock";
import SectionLayout from "../../../components/ui/SectionLayout";

export default function UseCasesSection() {
  return (
    <SectionLayout
      id="use-cases"
      title="Use Cases with Sample Requests & Responses"
      subtitle="Practical examples demonstrating how to use the API endpoints effectively"
    >
      <div className="space-y-10 text-sm text-gray-800">
        {/* REGISTRATION POST */}
        <div>
          <h3 className="text-lg font-semibold text-[#014737]">
            1. Registration (POST)
          </h3>
          <p>
            Used to register a new bag in the system. Requires a valid customer reference number and registration type.
          </p>

          <h4 className="mt-3 font-semibold text-[#014737]">Sample Request:</h4>
          <CodeBlock
            language="json"
            code={`{
  "bagRegistration": {
    "customerRefNumber": "5790002334764",
    "registrationType": "NOK",
    "messageId": "938D35D2-F8C1-4EAE-BB4D-0797E29DBB6A",
    "bagNumber": "34567895",
    "registrationDate": "2025-08-14",
    "referenceStatement": "kamal",
    "turnOverDate": "2025-08-14",
    "registeredNotes": "2000",
    "registeredCoins": "500",
    "registeredChecks": "0",
    "totalOrderAmount": "2500",
    "bagItemsLC": {
      "seddel1000": "1000",
      "seddel500": "500",
      "seddel200": "400",
      "seddel100": "100",
      "seddel50": "0"
    }
  }
}`}
          />

          <h4 className="mt-3 font-semibold text-[#014737]">Sample Response:</h4>
          <CodeBlock
            language="json"
            code={`{
  "customerRefNumber": "5790002334764",
  "bagNumber": "34567895",
  "description": "Bag Registered Successfully",
  "registrationStatus": "In-Progress",
  "bagLifecycleId": "02877729-a00f-4882-aa46-67bef0ee0182",
  "requestCorrelationId": "123f647-b06f-4725-9ec7-8e6dfce18fbG"
}`}
          />
        </div>

        {/* REGISTRATION GET */}
        <div>
          <h3 className="text-lg font-semibold text-[#014737]">
            2. Registration (GET)
          </h3>
          <p>
            Retrieves the status of a previously registered bag.
          </p>
          <CodeBlock
            language="bash"
            code={`GET https://api-test.nokas.com/api/cash-mgmt/bag-countingresult/34567895`}
          />
          <CodeBlock
            language="json"
            code={`{
  "customerRefNumber": "5790002334764",
  "bagNumber": "34567895",
  "description": "Bag not yet counted.",
  "bagStatus": "registered",
  "bagLifecycleId": "02877729-a00f-4882-aa46-67bef0ee0182",
  "requestCorrelationId": "345ff647-b06f-4725-9ec7-8e6dfce18fbf",
  "declaredAmount": 2500.0,
  "actualCountedAmount": null,
  "deviationAmount": null,
  "countedDate": null
}`}
          />
        </div>

        {/* DELETE */}
        <div>
          <h3 className="text-lg font-semibold text-[#014737]">
            3. Bag Registration (DELETE)
          </h3>
          <p>
            Cancels a registered bag before it’s processed for pickup or counting.
          </p>
          <CodeBlock
            language="bash"
            code={`DELETE https://api-test.nokas.com/api/cash-mgmt/bag-registration/34567895`}
          />
          <CodeBlock
            language="json"
            code={`{
  "customerRefNumber": "5790002334764",
  "bagNumber": "34567895",
  "description": "Bag Cancelled Successfully.",
  "registrationStatus": "Deleted",
  "bagLifecycleId": "02877729-a00f-4882-aa46-67bef0ee0182",
  "requestCorrelationId": "456ff647-b06f-4725-9ec7-8e6dfce18fbf"
}`}
          />
        </div>

        {/* COUNTING RESULTS */}
        <div>
          <h3 className="text-lg font-semibold text-[#014737]">
            4. Counting Result (GET)
          </h3>
          <p>
            Retrieves the counting result for a given bag. The response varies depending on the bag’s current status.
          </p>

          <h4 className="mt-4 font-semibold text-[#014737]">Case 1: Registered (Not Yet Counted)</h4>
          <CodeBlock
            language="bash"
            code={`GET https://api-test.nokas.com/api/cash-mgmt/bag-countingresult/34567897`}
          />
          <CodeBlock
            language="json"
            code={`{
  "customerRefNumber": "5790002334764",
  "bagNumber": "34567897",
  "description": "Bag not yet counted.",
  "bagStatus": "registered",
  "bagLifecycleId": "f821de8a-583d-47bb-8e41-886c9d08a11c",
  "requestCorrelationId": "345ff647-b06f-4725-9ec7-8e6dfce18fbf",
  "declaredAmount": 2500.0,
  "actualCountedAmount": null,
  "deviationAmount": null,
  "countedDate": null
}`}
          />

          <h4 className="mt-4 font-semibold text-[#014737]">Case 2: Picked-Up</h4>
          <CodeBlock
            language="bash"
            code={`GET https://api-test.nokas.com/api/cash-mgmt/bag-countingresult/34567896`}
          />
          <CodeBlock
            language="json"
            code={`{
  "customerRefNumber": "5790002334764",
  "bagNumber": "34567896",
  "description": "Bag not yet counted.",
  "bagStatus": "picked-up",
  "bagLifecycleId": "5a75c602-f312-4510-af6f-1e5cb3b1f5eb",
  "requestCorrelationId": "345ff647-b06f-4725-9ec7-8e6dfce18fbf",
  "declaredAmount": 2500.0,
  "actualCountedAmount": null,
  "deviationAmount": null,
  "countedDate": null
}`}
          />

          <h4 className="mt-4 font-semibold text-[#014737]">Case 3: Counted (Matched)</h4>
          <CodeBlock
            language="json"
            code={`{
  "customerRefNumber": "5790002334764",
  "bagNumber": "34567896",
  "description": "Bag counting result retrieved successfully.",
  "bagStatus": "counted",
  "bagLifecycleId": "5a75c602-f312-4510-af6f-1e5cb3b1f5eb",
  "requestCorrelationId": "345ff647-b06f-4725-9ec7-8e6dfce18fbf",
  "declaredAmount": 2500.0,
  "actualCountedAmount": 2500.0,
  "deviationAmount": 0.0,
  "countedDate": "2025-11-11T12:31:00"
}`}
          />

          <h4 className="mt-4 font-semibold text-[#014737]">Case 4: Counted (With Deviation)</h4>
          <CodeBlock
            language="json"
            code={`{
  "customerRefNumber": "5790002334764",
  "bagNumber": "34567896",
  "description": "Bag counting result retrieved successfully.",
  "bagStatus": "counted",
  "bagLifecycleId": "5a75c602-f312-4510-af6f-1e5cb3b1f5eb",
  "requestCorrelationId": "345ff647-b06f-4725-9ec7-8e6dfce18fbf",
  "declaredAmount": 2500.0,
  "actualCountedAmount": 2000.0,
  "deviationAmount": -500.0,
  "countedDate": "2025-11-11T12:32:00"
}`}
          />
        </div>
      </div>
    </SectionLayout>
  );
}
