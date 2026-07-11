import { useMsal } from "@azure/msal-react";
import { b2cPoliciesConfig } from "../authConfig";

export default function LogoutButton() {
  const { instance } = useMsal();

  const handleLogout = () => {
    instance.logoutRedirect({
      postLogoutRedirectUri: "/", 
      authority: b2cPoliciesConfig.authorities.signUpSignIn.authority, 
    });
  };

  return (
    <button
      onClick={handleLogout}
      className="bg-white text-red-600 font-semibold px-3 py-1 rounded"
    >
      Sign Out
    </button>
  );
}
