import { Link, Navigate, NavLink, Outlet, useParams } from "react-router-dom";
import { Badge, Callout } from "@/components/ui";
import { PRODUCTION_OPEN } from "@/config/environments";
import {
  MARKETS,
  SURFACE_LABEL,
  SURFACE_TONE,
  TEST_ONLY_NOTICE,
  isMarketKey,
  type MarketConfig,
  type SurfaceStatus,
} from "@/config/markets";
import { MarketContext } from "./market";

type NavItem = { id: string; label: string; icon: string; status?: SurfaceStatus };
type NavGroup = { heading?: string; items: NavItem[] };

function buildNav(market: MarketConfig): NavGroup[] {
  const intro: NavItem[] = [
    { id: "", label: "Overview", icon: "📘" },
    ...(market.hasArchitecture ? [{ id: "architecture", label: "Architecture", icon: "🧩" }] : []),
    { id: "auth", label: "Authentication", icon: "🔐" },
  ];

  const registration: NavItem[] = [
    { id: "bag-registration", label: "Endpoints", icon: "📦", status: market.surfaces.bagRegistration },
    { id: "bag-registration/use-cases", label: "Use cases", icon: "💡" },
  ];

  // A planned surface still gets a nav entry: partners need to know it is coming and
  // roughly what it will look like, which is more useful than an absent menu item.
  const counting: NavItem[] =
    market.surfaces.countingResult === "planned"
      ? [{ id: "counting-result", label: "Overview", icon: "📊", status: "planned" }]
      : [
          { id: "counting-result", label: "Endpoints", icon: "📊", status: market.surfaces.countingResult },
          { id: "counting-result/use-cases", label: "Use cases", icon: "💡" },
        ];

  const reference: NavItem[] = [
    { id: "correlation", label: "Correlation", icon: "🔗" },
    { id: "errors", label: "Errors", icon: "❗" },
    ...(market.swaggerUi ? [{ id: "swagger", label: "Swagger", icon: "🧪" }] : []),
  ];

  return [
    { items: intro },
    { heading: "Bag Registration", items: registration },
    { heading: "Counting Result", items: counting },
    { items: reference },
  ];
}

export default function GuideLayout() {
  const { market: marketParam } = useParams();

  // Guards both a typo in a hand-edited URL and a legacy path that slipped past the
  // redirects, rather than rendering a guide with no market behind it.
  if (!isMarketKey(marketParam)) {
    return <Navigate to="/portal/api-guide" replace />;
  }

  const market = MARKETS[marketParam];
  const navGroups = buildNav(market);

  return (
    <MarketContext.Provider value={market}>
      <div className="flex h-full w-full gap-4">
        <aside className="w-16 shrink-0 sm:w-52">
          <nav className="sticky top-4 flex flex-col gap-1 rounded-xl border border-gray-200 bg-white p-2 shadow-card">
            <p className="hidden px-2 pb-1 pt-1.5 text-[11px] font-semibold uppercase tracking-widest text-gray-400 sm:block">
              API Guide
            </p>

            {/* The selected market is shown, not offered. Only one market's rules are ever
                reachable from inside the guide, so a stray click cannot land a partner in
                another country's contract; changing market is a deliberate trip back to the
                picker via the link at the bottom. */}
            <div
              className="mb-1 flex items-center gap-2 rounded-lg bg-brand-50 px-2 py-2 ring-1 ring-inset ring-brand-200"
              title={`${market.label} — selected market`}
            >
              <span className="text-base leading-none" aria-hidden="true">
                {market.flag}
              </span>
              <span className="hidden min-w-0 sm:block">
                <span className="block truncate text-sm font-semibold text-brand-800">{market.label}</span>
                <span className="block text-[10px] uppercase tracking-widest text-brand-600">
                  {market.currency}
                </span>
              </span>
            </div>

            {navGroups.map((group, gi) => (
              <div
                key={group.heading ?? `group-${gi}`}
                className={gi > 0 ? "mt-2 border-t border-gray-100 pt-2" : undefined}
              >
                {group.heading && (
                  <p className="hidden px-2 pb-1 text-[10px] font-bold uppercase tracking-widest text-brand-500 sm:block">
                    {group.heading}
                  </p>
                )}
                {group.items.map((s) => (
                  <NavLink
                    key={s.id || "overview"}
                    to={s.id ? s.id : "."}
                    end
                    title={s.label}
                    className={({ isActive }) =>
                      [
                        "flex w-full items-center gap-2.5 rounded-lg p-2 text-sm font-medium transition sm:justify-start",
                        isActive
                          ? "bg-brand-50 text-brand-800 ring-1 ring-inset ring-brand-200"
                          : "text-gray-600 hover:bg-gray-100 hover:text-brand-800",
                      ].join(" ")
                    }
                  >
                    <span className="text-base leading-none">{s.icon}</span>
                    <span className="hidden sm:inline">{s.label}</span>
                    {s.status && s.status !== "live" && (
                      <span className="ml-auto hidden sm:inline">
                        <Badge tone={SURFACE_TONE[s.status]}>{SURFACE_LABEL[s.status]}</Badge>
                      </span>
                    )}
                  </NavLink>
                ))}
              </div>
            ))}

            {market.postmanCollection && (
              <a
                href={market.postmanCollection}
                download
                title={`Download the ${market.label} Postman collection`}
                className="mt-1 flex w-full items-center gap-2.5 rounded-lg border-t border-gray-100 p-2 pt-3 text-sm font-medium text-brand-700 transition hover:bg-brand-50 sm:justify-start"
              >
                <span className="text-base leading-none">📥</span>
                <span className="hidden sm:inline">Postman Collection</span>
              </a>
            )}

            <Link
              to="/portal/api-guide"
              title="Change market"
              className="mt-1 flex w-full items-center gap-2.5 rounded-lg border-t border-gray-100 p-2 pt-3 text-xs font-medium text-gray-500 transition hover:bg-gray-100 hover:text-gray-700 sm:justify-start"
            >
              <span className="text-base leading-none">🌍</span>
              <span className="hidden sm:inline">Change market</span>
            </Link>
          </nav>
        </aside>

        <main className="min-w-0 flex-1">
          <div className="mx-auto max-w-5xl space-y-4">
            {/* Rendered by the layout rather than each page, so neither caveat can be
                missed by arriving on a deep link. The platform notice comes first: which
                environment you are talking to outranks any per-market detail. */}
            {!PRODUCTION_OPEN && (
              <Callout tone={TEST_ONLY_NOTICE.tone} title={TEST_ONLY_NOTICE.title}>
                {TEST_ONLY_NOTICE.body}
              </Callout>
            )}

            {market.notice && (
              <Callout tone={market.notice.tone} title={market.notice.title}>
                {market.notice.body}
              </Callout>
            )}

            <div className="rounded-xl border border-gray-200 bg-white p-5 shadow-card sm:p-8">
              <Outlet />
            </div>
          </div>
        </main>
      </div>
    </MarketContext.Provider>
  );
}
