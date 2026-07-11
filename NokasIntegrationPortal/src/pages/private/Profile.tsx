import { useMsal } from "@azure/msal-react";
import { useEffect, useState } from "react";

export default function Profile() {
  const { instance } = useMsal();
  const [claims, setClaims] = useState<Record<string, any> | null>(null);

  useEffect(() => {
    const accounts = instance.getAllAccounts();
    if (accounts.length > 0) {
      const activeAccount = instance.getActiveAccount() ?? accounts[0];
      instance.setActiveAccount(activeAccount);

      const userClaims = activeAccount.idTokenClaims as Record<string, any>;
      console.log("✅ Loaded idTokenClaims:", userClaims);
      setClaims(userClaims);
    } else {
      console.warn("❌ No accounts found.");
      setClaims(null);
    }
  }, [instance]);

  return (
    <div className="animate-fade-in p-6">
      <h1 className="text-3xl font-bold mb-6 text-[#014737]">User Profile</h1>

      {claims ? (
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-6">
          <InfoBox label="Full Name" value={`${claims.given_name ?? ""} ${claims.family_name ?? ""}`} />
          <InfoBox label="Email" value={claims.email ?? "N/A"} />
          <InfoBox label="Company Name" value={claims.api_companyName ?? "N/A"} />
          <InfoBox label="API Client ID" value={claims.api_clientId ?? "N/A"} />
          <InfoBox label="Contact Person" value={claims.api_contactPerson ?? "N/A"} />
          <InfoBox label="Contact Person Email" value={claims.api_contactPersonEmail ?? "N/A"} />
          <InfoBox
            label="Login Time"
            value={
              claims.auth_time
                ? new Date(claims.auth_time * 1000).toLocaleString()
                : "N/A"
            }
          />
        </div>
      ) : (
        <p className="text-gray-600">No profile data available.</p>
      )}
    </div>
  );
}

function InfoBox({ label, value }: { label: string; value: string }) {
  return (
    <div className="bg-white border border-gray-200 shadow-sm rounded-lg p-4">
      <div className="text-sm text-gray-500">{label}</div>
      <div className="text-lg font-semibold text-gray-800 mt-1 break-words">
        {value}
      </div>
    </div>
  );
}
  