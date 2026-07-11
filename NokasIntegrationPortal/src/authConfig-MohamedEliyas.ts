// src/authConfig.ts
import type { Configuration } from "@azure/msal-browser";

const b2cDomain = "nokasb2ctest.b2clogin.com";
const tenant = "nokasb2ctest.onmicrosoft.com";

export const b2cPoliciesConfig = {
  names: {
    signUpSignIn: "B2C_1A_NOKASINTEGRATIONPORTAL_SIGNUP_SIGNIN",
    passwordReset: "B2C_1A_NOKASINTEGRATIONPORTAL_PasswordReset",
    editProfile: "B2C_1A_NOKASINTEGRATIONPORTAL_ProfileEdit",
  },
  authorities: {
    signUpSignIn: {
      authority: `https://${b2cDomain}/${tenant}/B2C_1A_NOKASINTEGRATIONPORTAL_SIGNUP_SIGNIN/v2.0`,
    },
    passwordReset: {
      authority: `https://${b2cDomain}/${tenant}/B2C_1A_NOKASINTEGRATIONPORTAL_PasswordReset/v2.0`,
    },
    editProfile: {
      authority: `https://${b2cDomain}/${tenant}/B2C_1A_NOKASINTEGRATIONPORTAL_ProfileEdit/v2.0`,
    },
  },
  authorityDomain: b2cDomain,
};

export const msalConfig: Configuration = {
  auth: {
    clientId: "df012e35-8a08-4ebd-835c-e0c0ae98af45",
    authority: b2cPoliciesConfig.authorities.signUpSignIn.authority,
    knownAuthorities: [b2cPoliciesConfig.authorityDomain], // keep b2clogin.com here
    redirectUri: window.location.origin,           // ✅ auto: http(s) + host + port
    postLogoutRedirectUri: window.location.origin, // ✅
  },
  cache: {
    cacheLocation: "localStorage",
    storeAuthStateInCookie: true, // safer with proxies/CDNs; set false if you know you don't need it
  },
};
