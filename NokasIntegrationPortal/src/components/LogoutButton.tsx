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
      className="inline-flex w-full items-center gap-2 rounded-md text-sm font-semibold text-rose-600 transition hover:text-rose-700 focus:outline-none focus:ring-2 focus:ring-rose-400"
    >
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" className="h-4 w-4" aria-hidden="true">
        <path d="M15 17l5-5-5-5" strokeLinecap="round" strokeLinejoin="round" />
        <path d="M20 12H9" strokeLinecap="round" />
        <path d="M12 20H6a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2h6" strokeLinecap="round" strokeLinejoin="round" />
      </svg>
      Sign out
    </button>
  );
}
