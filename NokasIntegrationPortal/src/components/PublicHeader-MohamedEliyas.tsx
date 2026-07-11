import { useNavigate } from "react-router-dom";
import { useIsAuthenticated, useMsal } from "@azure/msal-react";
import { useState } from "react";

export default function PublicHeader() {
  const navigate = useNavigate();
  const isAuthenticated = useIsAuthenticated();
  const { instance } = useMsal();
  const [menuOpen, setMenuOpen] = useState(false);

  const handleLogoClick = () => {
    navigate(isAuthenticated ? "/portal" : "/");
  };

  const handleSignIn = () => {
    instance.loginRedirect();
  };

  const handleMobileNavigate = (path: string) => {
    navigate(path);
    setMenuOpen(false);
  };

  return (
    <header className="w-full bg-[#013c2f] py-4 text-white text-sm">
      <div className="max-w-7xl mx-auto flex items-center justify-between px-4">
        {/* Logo and Title */}
        <button
          onClick={handleLogoClick}
          className="flex items-center gap-2 focus:outline-none"
          aria-label="Go to home"
        >
          <img src="/logo.png" alt="NOKAS Logo" className="h-8" />
          <div className="flex flex-col leading-tight text-left">
            <span className="text-base font-semibold text-white">NOKAS</span>
            <span className="text-sm text-gray-300">Payment solutions</span>
          </div>
        </button>

        {/* Desktop Navigation */}
        <nav className="hidden md:flex gap-5 font-medium">
          <a href="/customer-onboarding" className="hover:underline">
            Customer Onboarding
          </a>
          <a href="/public-swagger" className="hover:underline">
            API Documentation
          </a>

          <a href="/contact" className="hover:underline">
            Contact
          </a>
          <a href="/about-us" className="hover:underline">
            About us
          </a>
        </nav>

        {/* Right-side controls */}
        <div className="hidden md:flex items-center gap-4">
          <button
            className="text-white hover:text-gray-300"
            aria-label="Search"
          >
            <svg
              xmlns="http://www.w3.org/2000/svg"
              className="h-5 w-5"
              fill="none"
              viewBox="0 0 24 24"
              stroke="currentColor"
              strokeWidth="2"
            >
              <path
                strokeLinecap="round"
                strokeLinejoin="round"
                d="M21 21l-4.35-4.35M17 11a6 6 0 11-12 0 6 6 0 0112 0z"
              />
            </svg>
          </button>
          <button
            onClick={handleSignIn}
            className="bg-green-400 hover:bg-green-300 text-black font-semibold px-4 py-1.5 rounded-full transition"
          >
            Login
          </button>
          <div className="flex items-center gap-1 font-medium">
            <span>EN</span>
            <svg
              xmlns="http://www.w3.org/2000/svg"
              className="h-4 w-4"
              fill="none"
              viewBox="0 0 24 24"
              stroke="currentColor"
              strokeWidth="2"
            >
              <path
                strokeLinecap="round"
                strokeLinejoin="round"
                d="M12 3c4.97 0 9 4.03 9 9s-4.03 9-9 9-9-4.03-9-9 4.03-9 9-9z"
              />
              <path
                strokeLinecap="round"
                strokeLinejoin="round"
                d="M2.05 12h19.9M12 2.05v19.9M4.22 4.22l15.56 15.56"
              />
            </svg>
          </div>
        </div>

        {/* Mobile Hamburger Icon */}
        <button
          onClick={() => setMenuOpen(!menuOpen)}
          className="md:hidden text-white focus:outline-none"
        >
          <svg
            xmlns="http://www.w3.org/2000/svg"
            className="h-6 w-6"
            fill="none"
            viewBox="0 0 24 24"
            stroke="currentColor"
            strokeWidth="2"
          >
            <path
              strokeLinecap="round"
              strokeLinejoin="round"
              d="M4 6h16M4 12h16M4 18h16"
            />
          </svg>
        </button>
      </div>

      {/* Mobile Menu */}
      {menuOpen && (
        <div className="md:hidden px-4 mt-2">
          <nav className="flex flex-col gap-2 bg-[#014737] p-4 rounded shadow-lg">
            {[
              { label: "CustomerOnboarding", to: "/customer-onboarding" },
              { label: "API Documentation", to: "/public-swagger" },

              { label: "Contact", to: "/contact" },
              { label: "About us", to: "/about-us" },
            ].map((link) => (
              <button
                key={link.to}
                onClick={() => handleMobileNavigate(link.to)}
                className="text-left hover:underline"
              >
                {link.label}
              </button>
            ))}
            <button
              onClick={handleSignIn}
              className="mt-3 bg-green-400 hover:bg-green-300 text-black font-semibold px-4 py-1.5 rounded-full transition"
            >
              Login
            </button>
          </nav>
        </div>
      )}
    </header>
  );
}
