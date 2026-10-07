import { Navigate } from "react-router-dom";
import { guidePath } from "@/config/markets";
import { useMarket } from "./market";

import NoArchitectureSection from "./norway/ArchitectureSection";
import NoBagRegistrationEndpoints from "./norway/BagRegistrationEndpointsSection";
import NoBagRegistrationUseCases from "./norway/BagRegistrationUseCasesSection";
import NoCountingResultEndpoints from "./norway/CountingResultEndpointsSection";
import NoCountingResultUseCases from "./norway/CountingResultUseCasesSection";

import DkBagRegistrationEndpoints from "./denmark/BagRegistrationEndpointsSection";
import DkBagRegistrationUseCases from "./denmark/BagRegistrationUseCasesSection";
import DkCountingResult from "./denmark/CountingResultSection";

/**
 * Market dispatch for the pages whose content is country-specific.
 *
 * The route table stays a single :market branch so GuideLayout is mounted once and the
 * sidebar survives navigation; which country's content renders is decided here, in one
 * obvious place, rather than by duplicating the route tree per market.
 */

/** Sends a market to its own overview when it has no page for this route. */
function NotInThisMarket() {
  const market = useMarket();
  return <Navigate to={guidePath(market.key)} replace />;
}

export function ArchitecturePage() {
  const market = useMarket();
  return market.hasArchitecture ? <NoArchitectureSection /> : <NotInThisMarket />;
}

export function BagRegistrationEndpointsPage() {
  const market = useMarket();
  return market.key === "denmark" ? <DkBagRegistrationEndpoints /> : <NoBagRegistrationEndpoints />;
}

export function BagRegistrationUseCasesPage() {
  const market = useMarket();
  return market.key === "denmark" ? <DkBagRegistrationUseCases /> : <NoBagRegistrationUseCases />;
}

export function CountingResultEndpointsPage() {
  const market = useMarket();
  return market.key === "denmark" ? <DkCountingResult /> : <NoCountingResultEndpoints />;
}

export function CountingResultUseCasesPage() {
  const market = useMarket();
  // Denmark has no counting-result surface yet, so there are no use cases to show.
  return market.key === "denmark" ? <NotInThisMarket /> : <NoCountingResultUseCases />;
}
