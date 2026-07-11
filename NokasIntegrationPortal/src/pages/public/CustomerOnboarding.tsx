import PublicLayout from "../../layouts/PublicLayout";

export default function CustomerOnboarding() {
  return (
    <PublicLayout>
      <div className="animate-fade-in space-y-8">
        <h1 className="text-3xl font-bold text-[#014737]">
          Customer Onboarding
        </h1>
        <p className="text-gray-700">
          Welcome to the Nokas Integration Portal. Complete the steps below to:
          <ol className="list-decimal list-inside mt-2 space-y-1 text-gray-700">
            <li>
              Use the Nokas Bag Registration API to submit bag registrations and
              retrieve counting results.
            </li>
            <li>
              Access the Nokas Integration Portal to view and track your order
              statuses.
            </li>
            <li>
              To view the complete integration specification guide with Swagger
              documentation to learn how to use the API effectively.
            </li>
          </ol>
        </p>

        <div className="space-y-6">
          <OnboardingStep
            step="Step 1"
            title="Obtain API Credentials"
            description={
              <>
                To access and use the Nokas API to submit bag registrations and retrieve counting results, you must first obtain a{" "}
                <strong>Client ID</strong> and <strong>Client Secret</strong>{" "}
                from Nokas.
                <br />
                If you don’t have these credentials yet, please send an email to{" "}
                <a
                  href="mailto:BizTalkOps@nokas.no"
                  className="text-blue-600 underline"
                >
                  BizTalkOps@nokas.no
                </a>{" "}
                to request them.
              </>
            }
          />

          <OnboardingStep
            step="Step 2"
            title="Register on the Portal"
            description={
              <>
                Once you have received your API credentials, return to the{" "}
      <strong>Nokas Integration Portal</strong>{" "}
      <span className="text-sm italic text-gray-600">(i.e., this portal you are currently viewing)</span>{" "}
      and click <em>Launch Portal</em> or <em>Login</em> to begin the registration process.
      <br />
      During sign-up, you will be asked to provide your organization details, including the assigned Client ID and other required information.
              </>
            }
          />

          <OnboardingStep
            step="Step 3"
            title="Access Orders & Documentation"
            description={
              <>
                After successful registration and login, you will be able to:
                <ul className="list-disc list-inside mt-2 space-y-1 text-gray-700">
                  <li>Track and manage your registration orders</li>
                  <li>Access full API documentation and usage guidelines</li>
                  <li>View and download integration guides</li>
                </ul>
              </>
            }
          />
        </div>
      </div>
    </PublicLayout>
  );
}

function OnboardingStep({
  step,
  title,
  description,
}: {
  step: string;
  title: string;
  description: React.ReactNode;
}) {
  return (
    <div className="bg-white border border-gray-200 shadow-sm rounded-lg p-6">
      <h2 className="text-xl font-bold text-[#014737] mb-2">
        {step}: {title}
      </h2>
      <div className="text-gray-800 text-sm">{description}</div>
    </div>
  );
}
