import { createContext, useContext } from "react";
import type { MarketConfig } from "@/config/markets";
import { guidePath } from "@/config/markets";

/**
 * The market is taken from the :market route segment by GuideLayout and handed down here,
 * so every guide page renders the right base URL, role and currency without prop drilling
 * and without any client-side state to get out of sync with the URL.
 */
export const MarketContext = createContext<MarketConfig | null>(null);

export function useMarket(): MarketConfig {
  const market = useContext(MarketContext);
  if (!market) {
    throw new Error("useMarket must be used inside a market route (GuideLayout).");
  }
  return market;
}

/** Link helper scoped to the current market: link("errors") -> /portal/api-guide/norway/errors */
export function useGuideLink(): (sub?: string) => string {
  const market = useMarket();
  return (sub = "") => guidePath(market.key, sub);
}
