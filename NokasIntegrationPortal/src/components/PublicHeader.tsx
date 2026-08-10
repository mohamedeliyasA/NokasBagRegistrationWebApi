import { useEffect, useState } from "react";
import { NavLink, useLocation, useNavigate } from "react-router-dom";
import { useIsAuthenticated, useMsal } from "@azure/msal-react";

const navLinks = [
  { label: "Customer onboarding", to: "/customer-onboarding" },
  { label: "Products", to: "/product" },
  { label: "API", to: "/swagger" },
  { label: "Contact", to: "/contact" },
  { label: "About us", to: "/about-us" },
];

export default function PublicHeader() {
  const navigate = useNavigate();
  const location = useLocation();
  const isAuthenticated = useIsAuthenticated();
  const { instance } = useMsal();
  const [menuOpen, setMenuOpen] = useState(false);

  // Close the mobile menu whenever the route changes.
  useEffect(() => {
    setMenuOpen(false);
  }, [location.pathname]);

  const handleSignIn = () => {
    if (isAuthenticated) {
      navigate("/portal");
      return;
    }
    instance.loginRedirect().catch((err) => console.error("Login redirect failed:", err));
  };

  const linkClass = ({ isActive }: { isActive: boolean }) =>
    [
      "relative py-1 transition-colors",
      isActive
        ? "text-white after:absolute after:inset-x-0 after:-bottom-0.5 after:h-0.5 after:rounded-full after:bg-brand-400"
        : "text-white/75 hover:text-white",
    ].join(" ");

  return (
    <header className="sticky top-0 z-40 w-full border-b border-white/10 bg-brand-900/95 text-sm backdrop-blur supports-[backdrop-filter]:bg-brand-900/80">
      <div className="mx-auto flex max-w-7xl items-center justify-between gap-4 px-4 py-3">
        {/* Logo — the official wordmark already reads "NOKAS", so no duplicate text. */}
        <button
          onClick={() => navigate(isAuthenticated ? "/portal" : "/")}
          className="flex shrink-0 items-center rounded focus:outline-none focus:ring-2 focus:ring-brand-300"
          aria-label="NOKAS — go to home"
        >
          <img
            src="/Nokas_Digital_Logo_white.png"
            alt="NOKAS"
            width={906}
            height={136}
            className="h-7 w-auto sm:h-8"
          />
        </button>

        {/* Desktop nav */}
        <nav className="hidden items-center gap-6 font-medium md:flex">
          {navLinks.map((l) => (
            <NavLink key={l.to} to={l.to} className={linkClass}>
              {l.label}
            </NavLink>
          ))}
        </nav>

        {/* Desktop CTA */}
        <div className="hidden md:block">
          <button
            onClick={handleSignIn}
            className="rounded-full bg-brand-400 px-5 py-1.5 font-semibold text-brand-900 transition hover:bg-brand-300 focus:outline-none focus:ring-2 focus:ring-brand-300 focus:ring-offset-2 focus:ring-offset-brand-900"
          >
            {isAuthenticated ? "Go to portal" : "Login"}
          </button>
        </div>

        {/* Mobile toggle */}
        <button
          onClick={() => setMenuOpen((v) => !v)}
          className="rounded p-1 text-white focus:outline-none focus:ring-2 focus:ring-brand-400 md:hidden"
          aria-expanded={menuOpen}
          aria-controls="mobile-nav"
          aria-label={menuOpen ? "Close menu" : "Open menu"}
        >
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" className="h-6 w-6">
            {menuOpen ? (
              <path d="M6 6l12 12M18 6L6 18" strokeLinecap="round" />
            ) : (
              <path d="M4 6h16M4 12h16M4 18h16" strokeLinecap="round" />
            )}
          </svg>
        </button>
      </div>

      {/* Mobile nav */}
      {menuOpen && (
        <nav id="mobile-nav" className="border-t border-white/10 bg-brand-900 px-4 pb-4 pt-2 md:hidden">
          <ul className="flex flex-col">
            {navLinks.map((l) => (
              <li key={l.to}>
                <NavLink
                  to={l.to}
                  className={({ isActive }) =>
                    `block rounded px-2 py-2.5 font-medium transition ${
                      isActive ? "bg-white/10 text-white" : "text-white/80 hover:bg-white/5 hover:text-white"
                    }`
                  }
                >
                  {l.label}
                </NavLink>
              </li>
            ))}
          </ul>
          <button
            onClick={handleSignIn}
            className="mt-3 w-full rounded-full bg-brand-400 px-4 py-2 font-semibold text-brand-900 transition hover:bg-brand-300"
          >
            {isAuthenticated ? "Go to portal" : "Login"}
          </button>
        </nav>
      )}
    </header>
  );
}
