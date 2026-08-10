import { useState, useEffect, useRef } from "react";
import { Outlet, NavLink, Link, useLocation } from "react-router-dom";
import { useMsal, useIsAuthenticated } from "@azure/msal-react";
import LogoutButton from "../components/LogoutButton";

type NokasClaims = {
  given_name?: string;
  family_name?: string;
  emails?: string[];
  [key: string]: unknown;
};

/**
 * Dashboard and Business Activity Monitoring are intentionally absent:
 * both are still placeholders, so they stay unlinked until they carry real data.
 */
const navigationLinks = [
  { to: "/portal/api-guide", label: "API Guide", icon: "📚" },
];

export default function SidebarLayout() {
  const [collapsed, setCollapsed] = useState(false);
  const [mobileSidebarOpen, setMobileSidebarOpen] = useState(false);
  const [showMenu, setShowMenu] = useState(false);
  const location = useLocation();
  const { instance } = useMsal();
  const isAuthenticated = useIsAuthenticated();
  const account = instance.getActiveAccount();
  const menuRef = useRef<HTMLDivElement | null>(null);

  const claims = account?.idTokenClaims as NokasClaims | undefined;
  const fullName =
    claims?.given_name && claims?.family_name
      ? `${claims.given_name} ${claims.family_name}`
      : "Welcome";
  const initials =
    claims?.given_name && claims?.family_name
      ? `${claims.given_name[0]}${claims.family_name[0]}`.toUpperCase()
      : "N";

  // /portal now redirects to the guide at the route level (see App.tsx), so no
  // effect-based redirect is needed here.

  // Close the mobile drawer on navigation.
  useEffect(() => {
    setMobileSidebarOpen(false);
  }, [location.pathname]);

  // Close the profile menu on outside click or Escape - a mouseleave alone is
  // easy to miss and leaves the menu stuck open.
  useEffect(() => {
    if (!showMenu) return;

    function onPointerDown(e: MouseEvent) {
      if (menuRef.current && !menuRef.current.contains(e.target as Node)) {
        setShowMenu(false);
      }
    }
    function onKeyDown(e: KeyboardEvent) {
      if (e.key === "Escape") setShowMenu(false);
    }

    document.addEventListener("mousedown", onPointerDown);
    document.addEventListener("keydown", onKeyDown);
    return () => {
      document.removeEventListener("mousedown", onPointerDown);
      document.removeEventListener("keydown", onKeyDown);
    };
  }, [showMenu]);

  const navLinkClass = ({ isActive }: { isActive: boolean }) =>
    [
      "flex items-center gap-3 rounded-lg p-2.5 transition",
      isActive
        ? "bg-brand-50 font-semibold text-brand-800 ring-1 ring-inset ring-brand-200"
        : "text-gray-600 hover:bg-gray-100 hover:text-brand-800",
    ].join(" ");

  return (
    <div className="flex min-h-screen flex-col bg-gray-50">
      {/* ---------------- Mobile top bar ---------------- */}
      {isAuthenticated && (
        <div className="flex w-full items-center justify-between border-b border-gray-200 bg-white px-4 py-3 md:hidden">
          <button
            aria-label="Open navigation"
            onClick={() => setMobileSidebarOpen(true)}
            className="rounded p-1 text-gray-700 focus:outline-none focus:ring-2 focus:ring-brand-500"
          >
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" className="h-6 w-6">
              <path d="M4 6h16M4 12h16M4 18h16" strokeLinecap="round" />
            </svg>
          </button>
          <span className="text-sm font-semibold text-brand-800">NOKAS Portal</span>
          <span className="flex h-8 w-8 items-center justify-center rounded-full bg-brand-800 text-xs font-bold text-white">
            {initials}
          </span>
        </div>
      )}

      <div className="flex flex-1">
        {/* ---------------- Desktop sidebar ---------------- */}
        <aside
          className={`hidden flex-col border-r border-gray-200 bg-white transition-all duration-300 md:flex ${
            collapsed ? "w-20" : "w-64"
          }`}
        >
          <div className="flex items-center gap-2 border-b border-gray-100 p-4">
            <button
              aria-label={collapsed ? "Expand sidebar" : "Collapse sidebar"}
              onClick={() => setCollapsed(!collapsed)}
              className="rounded p-1 text-gray-500 transition hover:bg-gray-100 hover:text-brand-800 focus:outline-none focus:ring-2 focus:ring-brand-500"
            >
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" className="h-5 w-5">
                <path d="M4 6h16M4 12h16M4 18h16" strokeLinecap="round" />
              </svg>
            </button>

            {/*
              Light background here, so the full-colour logo is used.
              When collapsed there is no room for the wordmark, so the square
              mark stands in for it.
            */}
            <Link
              to="/portal/api-guide"
              className="flex min-w-0 items-center focus:outline-none"
              aria-label="NOKAS — API guide"
            >
              {collapsed ? (
                <img src="/nokas-mark.svg" alt="NOKAS" className="h-7 w-7 rounded-md" />
              ) : (
                <img
                  src="/Nokas_Digital_Logo.png"
                  alt="NOKAS"
                  width={906}
                  height={136}
                  className="h-6 w-auto"
                />
              )}
            </Link>
          </div>

          <nav className="flex flex-col gap-1 p-3">
            {navigationLinks.map((link) => (
              <NavLink
                key={link.to}
                to={link.to}
                className={navLinkClass}
                title={collapsed ? link.label : undefined}
              >
                <span className="text-lg leading-none">{link.icon}</span>
                {!collapsed && <span className="text-sm">{link.label}</span>}
              </NavLink>
            ))}
          </nav>

          {isAuthenticated && (
            <div className="mt-auto flex items-center gap-3 border-t border-gray-200 p-4">
              <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-brand-800 text-xs font-bold text-white">
                {initials}
              </span>
              {!collapsed && (
                <span className="min-w-0 text-sm leading-tight text-gray-800">
                  <span className="block truncate font-medium">{fullName}</span>
                  {claims?.emails?.[0] && (
                    <span className="block truncate text-xs text-gray-500">{claims.emails[0]}</span>
                  )}
                </span>
              )}
            </div>
          )}
        </aside>

        {/* ---------------- Mobile drawer ---------------- */}
        {mobileSidebarOpen && (
          <div
            className="fixed inset-0 z-40 bg-black/50 md:hidden"
            onClick={() => setMobileSidebarOpen(false)}
            aria-hidden="true"
          />
        )}

        <aside
          className={`fixed left-0 top-0 z-50 h-full w-64 transform bg-white shadow-xl transition-transform duration-300 md:hidden ${
            mobileSidebarOpen ? "translate-x-0" : "-translate-x-full"
          }`}
        >
          <div className="flex items-center justify-between border-b border-gray-200 p-4">
            <span className="text-sm font-bold text-brand-800">Navigation</span>
            <button
              aria-label="Close navigation"
              onClick={() => setMobileSidebarOpen(false)}
              className="rounded p-1 text-gray-500 hover:bg-gray-100"
            >
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" className="h-5 w-5">
                <path d="M6 6l12 12M18 6L6 18" strokeLinecap="round" />
              </svg>
            </button>
          </div>

          <nav className="flex flex-col gap-1 p-3">
            {navigationLinks.map((link) => (
              <NavLink key={link.to} to={link.to} className={navLinkClass}>
                <span className="text-lg leading-none">{link.icon}</span>
                <span className="text-sm">{link.label}</span>
              </NavLink>
            ))}
            <NavLink to="/portal/profile" className={navLinkClass}>
              <span className="text-lg leading-none">👤</span>
              <span className="text-sm">Profile</span>
            </NavLink>
          </nav>
        </aside>

        {/* ---------------- Main ---------------- */}
        <div className="flex min-w-0 flex-1 flex-col">
          {isAuthenticated && (
            <header className="hidden items-center justify-end gap-3 border-b border-gray-200 bg-white px-6 py-3 md:flex">
              <span className="text-sm font-medium text-gray-700">{fullName}</span>

              <div className="relative" ref={menuRef}>
                <button
                  aria-label="Account menu"
                  aria-expanded={showMenu}
                  aria-haspopup="menu"
                  onClick={() => setShowMenu((prev) => !prev)}
                  className="flex h-9 w-9 items-center justify-center rounded-full bg-brand-800 text-xs font-bold text-white transition hover:bg-brand-700 focus:outline-none focus:ring-2 focus:ring-brand-500 focus:ring-offset-2"
                >
                  {initials}
                </button>

                {showMenu && (
                  <div
                    role="menu"
                    className="absolute right-0 z-50 mt-2 w-48 overflow-hidden rounded-lg border border-gray-200 bg-white shadow-lg"
                  >
                    <div className="border-b border-gray-100 px-4 py-2.5">
                      <p className="truncate text-sm font-medium text-gray-900">{fullName}</p>
                      {claims?.emails?.[0] && (
                        <p className="truncate text-xs text-gray-500">{claims.emails[0]}</p>
                      )}
                    </div>
                    <Link
                      to="/portal/profile"
                      onClick={() => setShowMenu(false)}
                      className="block px-4 py-2.5 text-sm text-gray-700 transition hover:bg-gray-50"
                      role="menuitem"
                    >
                      Profile
                    </Link>
                    <div className="border-t border-gray-100 px-4 py-2.5">
                      <LogoutButton />
                    </div>
                  </div>
                )}
              </div>
            </header>
          )}

          <main className="min-w-0 flex-1 overflow-auto p-4 sm:p-6">
            <div className="animate-fade-in">
              <Outlet />
            </div>
          </main>
        </div>
      </div>
    </div>
  );
}
