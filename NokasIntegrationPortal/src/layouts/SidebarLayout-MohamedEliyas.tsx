import { useState, useEffect } from "react";
import { Outlet, Link, useLocation, useNavigate } from "react-router-dom";
import { useMsal, useIsAuthenticated } from "@azure/msal-react";
import LogoutButton from "../components/LogoutButton";

type NokasClaims = {
  given_name?: string;
  family_name?: string;
  [key: string]: any;
};

export default function SidebarLayout() {
  const [collapsed, setCollapsed] = useState(false);
  const [mobileSidebarOpen, setMobileSidebarOpen] = useState(false);
  const [showMenu, setShowMenu] = useState(false);
  const location = useLocation();
  const navigate = useNavigate();
  const { instance } = useMsal();
  const isAuthenticated = useIsAuthenticated();
  const account = instance.getActiveAccount();

  // Only API Guide is visible now
  const navigationLinks = [
    { to: "/portal/api-guide", label: "Nokas Bag Registration API Guide", icon: "📚" },
  ];

  const handleLogoClick = () => {
    // Send authenticated users to the API Guide as primary entry
    navigate(isAuthenticated ? "/portal/api-guide" : "/");
  };

  // Redirect old /portal bookmarks to /portal/api-guide
  useEffect(() => {
    if (isAuthenticated && location.pathname === "/portal") {
      navigate("/portal/api-guide", { replace: true });
    }
  }, [isAuthenticated, location.pathname, navigate]);

  return (
    <div className="flex flex-col min-h-screen bg-gray-100">
      {/* Mobile Top Bar */}
      {isAuthenticated && (
        <div className="md:hidden flex items-center justify-between w-full bg-white border-b p-4">
          <button
            aria-label="Toggle sidebar"
            onClick={() => setMobileSidebarOpen(true)}
            className="text-xl"
          >
            ☰
          </button>
          <span className="font-semibold text-sm">NOKAS Portal</span>
        </div>
      )}

      <div className="flex flex-1">
        {/* Sidebar for Desktop */}
        <aside
          className={`hidden md:flex flex-col transition-all duration-500 ${
            collapsed ? "w-20" : "w-64"
          } bg-white text-[#014737] border-r border-gray-200`}
        >
          <div className="flex items-center gap-2 p-4">
            <button
              aria-label="Toggle sidebar"
              onClick={() => setCollapsed(!collapsed)}
              className="text-xl text-[#014737] focus:outline-none"
            >
              ☰
            </button>
            <button
              onClick={handleLogoClick}
              className="flex items-center focus:outline-none"
            >
              <img src="/logo.png" alt="Sidebar Logo" className="h-6" />
              {!collapsed && (
                <div className="ml-2 leading-tight text-left">
                  <div className="text-sm font-bold text-[#014737]">NOKAS</div>
                  <div className="text-xs text-gray-500 -mt-0.5">
                    Payment solutions
                  </div>
                </div>
              )}
            </button>
          </div>

          <nav className="flex flex-col p-4 gap-4">
            {navigationLinks.map((link) => (
              <SidebarLink
                key={link.to}
                to={link.to}
                label={link.label}
                icon={link.icon}
                collapsed={collapsed}
              />
            ))}
          </nav>

          {isAuthenticated && account?.idTokenClaims && (
            <div className="p-4 mt-auto flex items-center gap-2 border-t border-gray-200">
              <img
                src="/user-avatar.png"
                alt="User Avatar"
                className="h-8 w-8 rounded-full border border-gray-300"
              />
              {!collapsed &&
                (() => {
                  const claims = account.idTokenClaims as NokasClaims;
                  const fullName =
                    claims?.given_name && claims?.family_name
                      ? `${claims.given_name} ${claims.family_name}`
                      : "Welcome";
                  return (
                    <div className="text-sm text-gray-800 leading-tight">
                      {fullName}
                    </div>
                  );
                })()}
            </div>
          )}
        </aside>

        {/* Mobile Sidebar Overlay */}
        {mobileSidebarOpen && (
          <div
            className="fixed inset-0 bg-black bg-opacity-50 z-40 md:hidden"
            onClick={() => setMobileSidebarOpen(false)}
          ></div>
        )}

        {/* Mobile Sidebar Panel */}
        <aside
          className={`fixed top-0 left-0 h-full bg-white w-64 z-50 transform transition-transform duration-300 md:hidden ${
            mobileSidebarOpen ? "translate-x-0" : "-translate-x-full"
          }`}
        >
          <div className="flex items-center justify-between p-4 border-b">
            <span className="font-bold text-sm">Navigation</span>
            <button
              aria-label="Close"
              onClick={() => setMobileSidebarOpen(false)}
              className="text-xl"
            >
              ✕
            </button>
          </div>
          <nav className="flex flex-col p-4 gap-4">
            {navigationLinks.map((link) => (
              <Link
                key={link.to}
                to={link.to}
                onClick={() => setMobileSidebarOpen(false)}
                className={`flex items-center gap-3 p-2 rounded transition ${
                  location.pathname === link.to
                    ? "bg-gray-200 font-semibold"
                    : "hover:bg-gray-100"
                }`}
              >
                <span className="text-xl">{link.icon}</span>
                <span className="text-sm font-medium">{link.label}</span>
              </Link>
            ))}
          </nav>
        </aside>

        {/* Main Content Area */}
        <main className="flex-1 p-6 overflow-auto relative">
          {isAuthenticated && (
            <div className="flex justify-end items-center gap-3 mb-4">
              {(() => {
                const claims = account?.idTokenClaims as NokasClaims | undefined;
                const fullName =
                  claims?.given_name && claims?.family_name
                    ? `${claims.given_name} ${claims.family_name}`
                    : "Welcome";

                return (
                  <span className="hidden md:inline text-sm font-medium text-gray-700">
                    {fullName}
                  </span>
                );
              })()}
              <div className="relative">
                <button
                  aria-label="Toggle profile menu"
                  onClick={() => setShowMenu((prev) => !prev)}
                  className="flex items-center gap-2 focus:outline-none"
                >
                  <img
                    src="/user-avatar.png"
                    alt="User Avatar"
                    className="h-8 w-8 rounded-full border border-gray-300 cursor-pointer"
                  />
                </button>
                {showMenu && (
                  <div
                    className="absolute right-0 mt-2 w-40 bg-white text-black rounded shadow-lg z-50"
                    onMouseLeave={() => setShowMenu(false)}
                  >
                    <Link
                      to="/portal/profile"
                      onClick={() => setShowMenu(false)}
                      className="block px-4 py-2 text-sm hover:bg-gray-100 transition"
                    >
                      Profile
                    </Link>
                    <div className="border-t"></div>
                    <div className="px-4 py-2">
                      <LogoutButton />
                    </div>
                  </div>
                )}
              </div>
            </div>
          )}
          <Outlet />
        </main>
      </div>
    </div>
  );
}

function SidebarLink({
  to,
  label,
  icon,
  collapsed,
}: {
  to: string;
  label: string;
  icon: string;
  collapsed: boolean;
}) {
  const location = useLocation();
  const isActive = location.pathname === to;

  return (
    <Link
      to={to}
      className={`flex items-center gap-3 p-2 rounded transition ${
        isActive ? "bg-gray-200 font-semibold" : "hover:bg-gray-100"
      }`}
    >
      <span className="text-xl">{icon}</span>
      {!collapsed && <span className="text-sm font-medium">{label}</span>}
    </Link>
  );
}
