/**
 * Single source of truth for per-environment API endpoints.
 *
 * These values were previously hardcoded in five separate page components, which
 * is how the portal ended up advertising a base URL and a rate limit that did not
 * match reality. Import from here — do not re-declare a URL in a page.
 */

export type ApiEnvironment = {
  key: "test" | "production";
  label: string;
  /** Root for every cash-management endpoint. */
  baseUrl: string;
  /** OAuth2 client-credentials token endpoint. */
  tokenUrl: string;
  /** Scope requested with the client-credentials grant. */
  scope: string;
  swaggerUi: string;
  swaggerJson: string;
};

export const TEST_ENV: ApiEnvironment = {
  key: "test",
  label: "Test",
  baseUrl: "https://api-test.nokas.com/api/cash-mgmt",
  tokenUrl: "https://api-test.nokas.com/oauth2/v2.0/token",
  scope: "https://nokasb2ctest.onmicrosoft.com/api/nokas/.default",
  swaggerUi: "https://api-test.nokas.com/swagger/index.html",
  swaggerJson: "https://api-test.nokas.com/swagger/v1/swagger.json",
};

export const PROD_ENV: ApiEnvironment = {
  key: "production",
  label: "Production",
  baseUrl: "https://api.nokas.com/api/cash-mgmt",
  tokenUrl: "https://api.nokas.com/oauth2/v2.0/token",
  // Production authenticates against the separate production B2C tenant.
  scope: "https://nokasb2c.onmicrosoft.com/api/nokas/.default",
  swaggerUi: "https://api.nokas.com/swagger/index.html",
  swaggerJson: "https://api.nokas.com/swagger/v1/swagger.json",
};

export const API_ENVIRONMENTS: ApiEnvironment[] = [TEST_ENV, PROD_ENV];

/** Per-application gateway throttle. */
export const RATE_LIMIT = "500 requests per second";
