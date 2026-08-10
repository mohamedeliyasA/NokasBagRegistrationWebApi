import type { ReactNode } from "react";
import { Link } from "react-router-dom";
import PublicHeader from "../components/PublicHeader";
// CookieConsentBanner is intentionally not rendered - see the note at the foot of this file.

const footerLinks = [
  { label: "Customer onboarding", to: "/customer-onboarding" },
  { label: "Products & services", to: "/product" },
  { label: "API documentation", to: "/swagger" },
  { label: "Contact", to: "/contact" },
  { label: "About us", to: "/about-us" },
];

export default function PublicLayout({ children }: { children: ReactNode }) {
  const year = new Date().getFullYear();

  return (
    <div className="flex min-h-screen flex-col bg-white text-gray-800">
      <PublicHeader />

      {/*
        No max-width or padding here on purpose: pages own their own layout so
        they can render full-bleed hero and banded sections.
      */}
      <main className="flex-1 animate-fade-in">{children}</main>

      <footer className="bg-brand-900 text-white">
        <div className="mx-auto max-w-6xl px-6 py-12">
          <div className="flex flex-col gap-10 md:flex-row md:justify-between">
            {/* Brand */}
            <div className="max-w-sm">
              <img
                src="/Nokas_Digital_Logo_white.png"
                alt="NOKAS"
                width={906}
                height={136}
                className="h-8 w-auto"
              />
              <p className="mt-2 text-xs text-white/60">Payment solutions</p>
              <p className="mt-4 text-sm leading-relaxed text-white/70">
                Nokas Verdihåndtering AS is a nationwide supplier of asset management services to
                banks, private businesses and public entities.
              </p>
            </div>

            {/* Nav */}
            <div>
              <h2 className="text-xs font-semibold uppercase tracking-widest text-white/50">
                Explore
              </h2>
              <ul className="mt-4 space-y-2">
                {footerLinks.map((l) => (
                  <li key={l.to}>
                    <Link
                      to={l.to}
                      className="text-sm text-white/75 transition hover:text-white hover:underline underline-offset-2"
                    >
                      {l.label}
                    </Link>
                  </li>
                ))}
              </ul>
            </div>

            {/* Contact */}
            <div>
              <h2 className="text-xs font-semibold uppercase tracking-widest text-white/50">
                Get in touch
              </h2>
              <ul className="mt-4 space-y-2 text-sm text-white/75">
                <li>
                  <a
                    href="mailto:BizTalkOps@nokas.no"
                    className="transition hover:text-white hover:underline underline-offset-2"
                  >
                    BizTalkOps@nokas.no
                  </a>
                </li>
                <li>
                  <a
                    href="tel:+4791502580"
                    className="transition hover:text-white hover:underline underline-offset-2"
                  >
                    +47 915 02580
                  </a>
                </li>
                <li className="pt-1 text-white/60">
                  Alf Bjerckes vei 1
                  <br />
                  0582 Oslo, Norway
                </li>
              </ul>
            </div>
          </div>

          <div className="mt-10 border-t border-white/10 pt-6 text-xs text-white/50">
            © {year} Nokas Verdihåndtering AS. All rights reserved.
          </div>
        </div>
      </footer>
    </div>
  );
}
