// src/main.tsx
import React from "react";
import ReactDOM from "react-dom/client";
import App from "./App";
import "./index.css";

import { PublicClientApplication } from "@azure/msal-browser";
import { MsalProvider } from "@azure/msal-react";
import { msalConfig, b2cPoliciesConfig } from "./authConfig";

const msalInstance = new PublicClientApplication(msalConfig);

msalInstance
  .initialize()
  .then(() => msalInstance.handleRedirectPromise())
  .then((response) => {
    if (response?.account) {
      msalInstance.setActiveAccount(response.account);
    } else {
      const accounts = msalInstance.getAllAccounts();
      if (accounts.length > 0) {
        msalInstance.setActiveAccount(accounts[0]);
      }
    }
  })
  .catch((error) => {
    if (error?.errorMessage?.includes("AADB2C90118")) {
      // Password reset flow triggered from B2C
      msalInstance.loginRedirect({
        authority: b2cPoliciesConfig.authorities.passwordReset.authority,
        scopes: ["openid", "profile"],
      });
    } else {
      console.error("Unhandled MSAL redirect error:", error);
    }
  })
  .finally(() => {
    ReactDOM.createRoot(document.getElementById("root")!).render(
      <React.StrictMode>
        <MsalProvider instance={msalInstance}>
          <App />
        </MsalProvider>
      </React.StrictMode>
    );
  });
