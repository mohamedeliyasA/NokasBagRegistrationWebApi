import { useMemo, useState } from "react";
import { Link } from "react-router-dom";
import PublicLayout from "../../layouts/PublicLayout";

type Group = { letter: string; items: string[] };

const catalogue: Group[] = [
  { letter: "A", items: ["autoCash Bank", "autoCash Drop", "autoCash Multi", "autoCash Retail", "Vending machine service"] },
  { letter: "B", items: ["Backup tape", "Bank-neutral ATMs"] },
  { letter: "C", items: ["coreCash for banking"] },
  { letter: "M", items: ["ATM in store", "ATM outsourcing"] },
  { letter: "N", items: ["Night safe settlement"] },
  { letter: "O", items: ["Storage of valuables"] },
  { letter: "S", items: ["ATM service"] },
  { letter: "V", items: ["Currency", "Currency services to banks", "Bill of exchange", "Currency exchange safe", "Exchanging expired banknotes", "Cash on delivery"] },
];

export default function Product() {
  const [query, setQuery] = useState("");

  const filtered = useMemo(() => {
    const q = query.trim().toLowerCase();
    if (!q) return catalogue;
    return catalogue
      .map((g) => ({ ...g, items: g.items.filter((i) => i.toLowerCase().includes(q)) }))
      .filter((g) => g.items.length > 0);
  }, [query]);

  const total = useMemo(() => catalogue.reduce((n, g) => n + g.items.length, 0), []);
  const shown = filtered.reduce((n, g) => n + g.items.length, 0);

  return (
    <PublicLayout>
      {/* ---------------- Header ---------------- */}
      <section className="border-b border-gray-200 bg-gradient-to-b from-brand-50 to-white px-6 py-14">
        <div className="mx-auto max-w-5xl text-center">
          <p className="text-xs font-semibold uppercase tracking-widest text-brand-600">Catalogue</p>
          <h1 className="mt-2 text-3xl font-bold tracking-tight text-brand-800 sm:text-4xl">
            Products &amp; Services
          </h1>
          <p className="mx-auto mt-3 max-w-2xl text-sm leading-relaxed text-gray-600">
            The full range of NOKAS cash handling and payment services. Search below, or browse
            alphabetically.
          </p>

          {/* Search */}
          <div className="mx-auto mt-7 max-w-md">
            <label htmlFor="product-search" className="sr-only">
              Search products and services
            </label>
            <div className="relative">
              <svg
                viewBox="0 0 24 24"
                fill="none"
                stroke="currentColor"
                strokeWidth="2"
                className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-gray-400"
                aria-hidden="true"
              >
                <circle cx="11" cy="11" r="7" />
                <path d="m20 20-3.5-3.5" strokeLinecap="round" />
              </svg>
              <input
                id="product-search"
                type="search"
                value={query}
                onChange={(e) => setQuery(e.target.value)}
                placeholder="Search services…"
                className="w-full rounded-full border border-gray-300 bg-white py-2.5 pl-10 pr-4 text-sm text-gray-900 shadow-sm transition placeholder:text-gray-400 focus:border-brand-500 focus:outline-none focus:ring-2 focus:ring-brand-500/30"
              />
            </div>
            <p className="mt-2 text-xs text-gray-500" aria-live="polite">
              Showing {shown} of {total} services
            </p>
          </div>
        </div>
      </section>

      {/* ---------------- Catalogue ---------------- */}
      <section className="bg-white px-6 py-12">
        <div className="mx-auto max-w-6xl space-y-12">
          {filtered.length === 0 && (
            <p className="rounded-xl border border-dashed border-gray-300 bg-gray-50 py-14 text-center text-sm text-gray-500">
              No services match “{query}”.
            </p>
          )}

          {filtered.map((group) => (
            <section key={group.letter}>
              <div className="mb-5 flex items-center gap-3">
                <span className="flex h-9 w-9 items-center justify-center rounded-lg bg-brand-800 text-sm font-bold text-white">
                  {group.letter}
                </span>
                <span className="h-px flex-1 bg-gray-200" />
              </div>

              <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">
                {group.items.map((item) => (
                  <article
                    key={item}
                    className="group flex items-start gap-3 rounded-xl border border-gray-200 bg-white p-4 shadow-card transition hover:-translate-y-0.5 hover:border-brand-300 hover:shadow-card-hover"
                  >
                    <span className="mt-0.5 flex h-8 w-8 shrink-0 items-center justify-center rounded-lg bg-brand-50 text-brand-700 transition group-hover:bg-brand-800 group-hover:text-white">
                      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" className="h-4 w-4" aria-hidden="true">
                        <path d="M20 7 12 3 4 7l8 4 8-4Z" strokeLinejoin="round" />
                        <path d="M4 7v10l8 4 8-4V7" strokeLinejoin="round" />
                        <path d="M12 11v10" />
                      </svg>
                    </span>
                    <p className="text-sm font-medium leading-snug text-gray-800">{item}</p>
                  </article>
                ))}
              </div>
            </section>
          ))}
        </div>
      </section>

      {/* ---------------- CTA ---------------- */}
      <section className="bg-white px-6 pb-16">
        <div className="mx-auto max-w-4xl overflow-hidden rounded-2xl border border-brand-200 bg-brand-50 shadow-card">
          <div className="flex flex-col items-start gap-4 p-8 sm:flex-row sm:items-center sm:justify-between">
            <div>
              <h2 className="text-xl font-bold text-brand-800">Questions about our products?</h2>
              <p className="mt-1.5 max-w-xl text-sm leading-relaxed text-gray-700">
                If you would like to order or simply want to know more, leave your details and we
                will get back to you shortly.
              </p>
            </div>
            <Link
              to="/contact"
              className="shrink-0 rounded-full bg-brand-800 px-6 py-3 text-sm font-semibold text-white transition hover:bg-brand-700"
            >
              Contact us
            </Link>
          </div>
        </div>
      </section>
    </PublicLayout>
  );
}
