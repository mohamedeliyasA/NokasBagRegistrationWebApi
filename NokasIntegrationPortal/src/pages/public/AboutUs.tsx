import PublicLayout from "../../layouts/PublicLayout";

const stats = [
  { value: "50bn+", label: "NOK in circulation daily", hint: "Banknotes and coins we help keep moving" },
  { value: "Lindesnes → Hammerfest", label: "Transport coverage", hint: "The length of Norway" },
  { value: "#1", label: "In Norwegian cash handling", hint: "Market-leading supplier" },
];

const pillars = [
  {
    title: "Security first",
    body: "The highest level of protection at every stage — from the checkout point right through to your account.",
    icon: (
      <path d="M12 3 4 6v6c0 4.5 3.4 8.3 8 9 4.6-.7 8-4.5 8-9V6l-8-3Z" strokeLinejoin="round" />
    ),
  },
  {
    title: "Cost efficiency",
    body: "Streamlining every process, eliminating waste and strengthening liquidity for our customers.",
    icon: (
      <>
        <circle cx="12" cy="12" r="8" />
        <path d="M12 8v8M9.5 10.5h5M9.5 13.5h5" strokeLinecap="round" />
      </>
    ),
  },
  {
    title: "Innovation",
    body: "Continuous product development to meet a market that is constantly changing.",
    icon: (
      <>
        <path d="M9 18h6M10 21h4" strokeLinecap="round" />
        <path d="M12 3a6 6 0 0 0-3.5 10.9c.4.3.5.7.5 1.1h6c0-.4.1-.8.5-1.1A6 6 0 0 0 12 3Z" strokeLinejoin="round" />
      </>
    ),
  },
];

export default function AboutUs() {
  return (
    <PublicLayout>
      {/* ---------------- Hero ---------------- */}
      <section className="border-b border-gray-200 bg-gradient-to-b from-brand-50 to-white px-6 py-16">
        <div className="mx-auto max-w-3xl text-center">
          <p className="text-xs font-semibold uppercase tracking-widest text-brand-600">About us</p>
          <h1 className="mt-2 text-3xl font-bold tracking-tight text-brand-800 sm:text-4xl">
            Turning physical money into electronic value
          </h1>
          <p className="mx-auto mt-4 text-base leading-relaxed text-gray-600">
            Nokas Verdihåndtering AS is Norway's leading supplier of cash handling products and
            services — and part of AVARN Security AS.
          </p>
        </div>
      </section>

      {/* ---------------- Stats ---------------- */}
      <section className="bg-white px-6 py-12">
        <div className="mx-auto grid max-w-5xl gap-6 sm:grid-cols-3">
          {stats.map((s) => (
            <div
              key={s.label}
              className="rounded-xl border border-gray-200 bg-white p-6 text-center shadow-card"
            >
              <p className="text-2xl font-bold leading-tight text-brand-800">{s.value}</p>
              <p className="mt-1.5 text-sm font-semibold text-gray-900">{s.label}</p>
              <p className="mt-1 text-xs text-gray-500">{s.hint}</p>
            </div>
          ))}
        </div>
      </section>

      {/* ---------------- Story ---------------- */}
      <section className="border-y border-gray-200 bg-gray-50 px-6 py-14">
        <div className="mx-auto max-w-3xl space-y-6 text-sm leading-relaxed text-gray-700">
          <h2 className="text-xl font-bold text-gray-900">Who we are</h2>

          <p>
            Nokas Verdihåndtering AS comes from a previous merger between Nokas Kontantservice AS —
            where Norges Bank was the majority owner — and Nokas Verditransport AS, and is owned by
            AVARN Security AS.
          </p>

          <p>
            Every day, banknotes and coins worth more than 50 billion kroner are in circulation in
            Norway. Nokas ensures that banks, business customers and consumers have the cash they
            need at all times, and that those banknotes and coins regularly undergo thorough
            authenticity and quality checks.
          </p>

          <p>
            As Norway's leading supplier of products and services within cash handling, we guarantee
            efficient and profitable solutions for the processing, control and distribution of both
            Norwegian and foreign cash.
          </p>

          <p>
            We are also the market leader in cash transport, with coverage from Lindesnes in the
            south to Hammerfest in the north. By choosing Nokas for both transport and counting, we
            can coordinate the two services and give you a single point of contact.
          </p>

          <blockquote className="rounded-r-lg border-l-4 border-brand-600 bg-brand-50 px-5 py-4">
            <p className="text-base font-semibold text-brand-900">
              In short: we want to give you better days.
            </p>
          </blockquote>

          <p>
            Our cash management system lets you spend more time on your core business, while your
            cash stays optimally secured and your money reaches your account as quickly as possible.
          </p>

          <p>
            Our business idea is the combination of banking and finance expertise: converting
            physical money into electronic value and back again — securely and efficiently.
          </p>
        </div>
      </section>

      {/* ---------------- Pillars ---------------- */}
      <section className="bg-white px-6 py-14">
        <div className="mx-auto max-w-5xl">
          <h2 className="text-center text-xl font-bold text-gray-900">What drives us</h2>

          <div className="mt-10 grid gap-6 md:grid-cols-3">
            {pillars.map((p) => (
              <article
                key={p.title}
                className="group rounded-xl border border-gray-200 bg-white p-6 shadow-card transition hover:-translate-y-1 hover:border-brand-300 hover:shadow-card-hover"
              >
                <span className="inline-flex h-11 w-11 items-center justify-center rounded-lg bg-brand-50 text-brand-700 transition group-hover:bg-brand-800 group-hover:text-white">
                  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" className="h-5 w-5" aria-hidden="true">
                    {p.icon}
                  </svg>
                </span>
                <h3 className="mt-4 text-base font-semibold text-gray-900">{p.title}</h3>
                <p className="mt-1.5 text-sm leading-relaxed text-gray-600">{p.body}</p>
              </article>
            ))}
          </div>
        </div>
      </section>
    </PublicLayout>
  );
}
