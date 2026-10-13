/**
 * Single source of truth for what differs between markets.
 *
 * The API Guide renders one set of components for every market; everything that is
 * country-specific — base path, currency, app role, which surfaces exist and how far
 * along they are — is read from here. Adding a third market should mean adding an entry
 * to this file plus its market-specific content pages, and nothing else.
 *
 * Same reasoning as environments.ts one level up: a URL or a role name that is written
 * out in a page component is a URL that will eventually be wrong in exactly one place.
 */

import { PRODUCTION_OPEN, PROD_ENV, TEST_ENV } from "./environments";

export type MarketKey = "norway" | "denmark";

/**
 * live    — in production use, contract is stable.
 * stub    — available on the test environment only; not yet in production use.
 * planned — designed, not built. No endpoint exists.
 */
export type SurfaceStatus = "live" | "stub" | "planned";

export type MarketNotice = {
  tone: "info" | "warning" | "danger";
  title: string;
  body: string;
};

export type MarketConfig = {
  key: MarketKey;
  label: string;
  flag: string;
  /** One line, used on the market picker and the landing page cards. */
  tagline: string;
  currency: "NOK" | "DKK";
  /** Test-environment root for this market's endpoints. */
  testBaseUrl: string;
  /** Null until the market actually has a production deployment. */
  prodBaseUrl: string | null;
  roles: {
    registration: string;
    countingResult: string | null;
  };
  surfaces: {
    bagRegistration: SurfaceStatus;
    countingResult: SurfaceStatus;
  };
  /** Null hides the Architecture page for this market. */
  hasArchitecture: boolean;
  /** Null hides the Swagger page — see the Denmark note below. */
  swaggerUi: string | null;
  swaggerJson: string | null;
  postmanCollection: string | null;
  rateLimit: string;
  /** Rendered as a banner on every page of this market's guide. */
  notice?: MarketNotice;
};

// FIXME: this figure is inherited from the previous portal copy and does not match the
// live APIM policy, which has been observed with a much lower limit. It is deliberately
// left as-is rather than replaced with a guess — see the rate-limit note in CLAUDE.md.
// Confirm the real value against the APIM policy and correct it here, in one place.
const NORWAY_RATE_LIMIT = "500 requests per second";

// Authored directly in deploy/provision-dk-mock-apim.ps1, so this one is known-correct.
const DENMARK_RATE_LIMIT = "100 requests per 60 seconds";

export const NORWAY: MarketConfig = {
  key: "norway",
  label: "Norway",
  flag: "🇳🇴",
  tagline: "Register cash bags and retrieve counting results.",
  currency: "NOK",
  testBaseUrl: TEST_ENV.baseUrl,
  prodBaseUrl: PROD_ENV.baseUrl,
  roles: {
    registration: "cashbag.registration.access",
    countingResult: "cashbag.countingresult.read",
  },
  surfaces: {
    bagRegistration: "live",
    countingResult: "live",
  },
  hasArchitecture: true,
  swaggerUi: TEST_ENV.swaggerUi,
  swaggerJson: TEST_ENV.swaggerJson,
  postmanCollection: "/BagReg_PostmanCollection.json",
  rateLimit: NORWAY_RATE_LIMIT,
};

export const DENMARK: MarketConfig = {
  key: "denmark",
  label: "Denmark",
  flag: "🇩🇰",
  tagline: "Register cash bags and retrieve counting results.",
  currency: "DKK",
  testBaseUrl: `${TEST_ENV.baseUrl}/dk`,
  prodBaseUrl: null,
  roles: {
    registration: "cashbag.dk.registration.access",
    countingResult: "cashbag.dk.countingresult.read",
  },
  surfaces: {
    bagRegistration: "live",
    countingResult: "live",
  },
  hasArchitecture: false,
  // Served from /dk/swagger rather than /swagger: the latter is a Front Door route already
  // pointing at the Norwegian API, so Denmark needs its own path and its own route.
  swaggerUi: "https://api-test.nokas.com/dk/swagger/index.html",
  swaggerJson: "https://api-test.nokas.com/dk/swagger/v1/swagger.json",
  postmanCollection: "/DkBagReg_PostmanCollection.json",
  rateLimit: DENMARK_RATE_LIMIT,
};

/**
 * Shown on every market's guide while production is closed. This is deliberately
 * separate from MarketConfig.notice: that one carries a caveat about a particular
 * market, this one is a fact about the whole platform, and both can be true at once.
 */
export const TEST_ONLY_NOTICE: MarketNotice = {
  tone: "warning",
  title: "Test environment — not yet production",
  body:
    "Every endpoint in this guide runs in NOKAS's test environment. Registrations and " +
    "cancellations you send are real operations against our test systems, so treat the " +
    "responses as accurate — but no production environment is open yet, and production " +
    "uses different URLs and credentials. NOKAS will confirm the cutover details with you " +
    "before you go live.",
};

export const MARKETS: Record<MarketKey, MarketConfig> = {
  norway: NORWAY,
  denmark: DENMARK,
};

export const MARKET_LIST: MarketConfig[] = [NORWAY, DENMARK];

export const DEFAULT_MARKET: MarketKey = "norway";

export function isMarketKey(value: string | undefined): value is MarketKey {
  return value === "norway" || value === "denmark";
}

/** Absolute route into a market's guide, e.g. guidePath("norway", "errors"). */
export function guidePath(market: MarketKey, sub = ""): string {
  const base = `/portal/api-guide/${market}`;
  return sub ? `${base}/${sub}` : base;
}

// Surface maturity and environment are two different things: a surface can be finished
// and stable while the only place you can call it is Test. Rather than add a second badge
// everywhere, the live label carries the environment and collapses back to a plain "Live"
// on its own the moment PROD_ENV.open becomes true.
export const SURFACE_LABEL: Record<SurfaceStatus, string> = {
  live: PRODUCTION_OPEN ? "Live" : "Live · Test",
  stub: "Test",
  planned: "Planned",
};

export const SURFACE_TONE: Record<SurfaceStatus, "green" | "amber" | "gray"> = {
  live: "green",
  stub: "amber",
  planned: "gray",
};
