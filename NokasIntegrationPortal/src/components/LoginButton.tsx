import { useMsal } from "@azure/msal-react";
import { b2cPoliciesConfig } from "../authConfig";

export default function LoginButton() {
  const { instance } = useMsal();

  const handleLogin = () => {
    instance
      .loginRedirect({
        authority: b2cPoliciesConfig.authorities.signUpSignIn.authority,
        scopes: ["openid", "profile"],
      })
      .catch((error) => {
        console.error("Login failed:", error);
      });
  };

  return (
    <button
      onClick={handleLogin}
      className="bg-white text-[#0078D4] font-semibold px-3 py-1 rounded"
    >
      Sign In
    </button>
  );
}
