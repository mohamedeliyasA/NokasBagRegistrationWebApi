import type { ReactNode } from "react";
import PublicLayout from "../../layouts/PublicLayout";

type Location = {
  region: string;
  company: string;
  street: string;
  city: string;
  phone: string;
};

const norway: Location[] = [
  { region: "Oslo", company: "Nokas Verdihåndtering AS — Head office", street: "Alf Bjerckes vei 1", city: "0582 Oslo", phone: "915 02580" },
  { region: "Rogaland", company: "Nokas Value Handling AS", street: "Strandsvingen 10", city: "4032 Stavanger", phone: "915 02580" },
  { region: "Troms and Finnmark", company: "Nokas Value Management AS", street: "Bankgata 9/11", city: "9008 Tromsø", phone: "915 02580" },
  { region: "Trøndelag", company: "Nokas Value Handling AS", street: "Peter Egges Plass 2", city: "7011 Trondheim", phone: "915 02580" },
  { region: "Westland", company: "Nokas Value Management AS", street: "Bradbenken 1", city: "5003 Bergen", phone: "915 02580" },
];

const denmark: Location[] = [
  { region: "Brøndby", company: "Nokas Value Management A/S", street: "Solmarksvej 5", city: "2605 Brøndby", phone: "+45 70 15 76 00" },
  { region: "Brabrand", company: "Nokas Value Management A/S", street: "Rosbjergvej 15", city: "8220 Brabrand", phone: "+45 70 15 76 00" },
];

function telHref(phone: string) {
  return `tel:${phone.replace(/\s+/g, "")}`;
}

function mapHref(loc: Location) {
  return `https://www.google.com/maps/search/?api=1&query=${encodeURIComponent(
    `${loc.street}, ${loc.city}`,
  )}`;
}

function PrimaryCard({
  title,
  icon,
  children,
}: {
  title: string;
  icon: ReactNode;
  children: ReactNode;
}) {
  return (
    <article className="group rounded-xl border border-gray-200 bg-white p-6 shadow-card transition hover:-translate-y-0.5 hover:border-brand-300 hover:shadow-card-hover">
      <span className="inline-flex h-11 w-11 items-center justify-center rounded-lg bg-brand-50 text-brand-700 transition group-hover:bg-brand-800 group-hover:text-white">
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" className="h-5 w-5" aria-hidden="true">
          {icon}
        </svg>
      </span>
      <h2 className="mt-4 text-base font-semibold text-gray-900">{title}</h2>
      <div className="mt-2 space-y-1 text-sm leading-relaxed text-gray-600">{children}</div>
    </article>
  );
}

function LocationCard({ loc }: { loc: Location }) {
  return (
    <article className="flex flex-col rounded-xl border border-gray-200 bg-white p-5 shadow-card transition hover:border-brand-300 hover:shadow-card-hover">
      <h3 className="text-sm font-bold text-brand-800">{loc.region}</h3>
      <p className="mt-1.5 text-xs font-medium text-gray-800">{loc.company}</p>
      <address className="mt-1 text-xs not-italic leading-relaxed text-gray-600">
        {loc.street}
        <br />
        {loc.city}
      </address>

      <div className="mt-3 flex flex-wrap items-center gap-x-4 gap-y-1 border-t border-gray-100 pt-3 text-xs">
        <a
          href={telHref(loc.phone)}
          className="inline-flex items-center gap-1.5 font-semibold text-brand-700 transition hover:text-brand-900"
        >
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" className="h-3.5 w-3.5" aria-hidden="true">
            <path d="M5 4h4l2 5-2.5 1.5a11 11 0 0 0 5 5L15 13l5 2v4a1 1 0 0 1-1 1A16 16 0 0 1 4 5a1 1 0 0 1 1-1Z" strokeLinejoin="round" />
          </svg>
          {loc.phone}
        </a>
        <a
          href={mapHref(loc)}
          target="_blank"
          rel="noopener noreferrer"
          className="inline-flex items-center gap-1.5 text-gray-500 transition hover:text-brand-700"
        >
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" className="h-3.5 w-3.5" aria-hidden="true">
            <path d="M12 21s7-5.5 7-11a7 7 0 1 0-14 0c0 5.5 7 11 7 11Z" strokeLinejoin="round" />
            <circle cx="12" cy="10" r="2.5" />
          </svg>
          Map
        </a>
      </div>
    </article>
  );
}

export default function Contact() {
  return (
    <PublicLayout>
      {/* ---------------- Header ---------------- */}
      <section className="border-b border-gray-200 bg-gradient-to-b from-brand-50 to-white px-6 py-14">
        <div className="mx-auto max-w-4xl text-center">
          <p className="text-xs font-semibold uppercase tracking-widest text-brand-600">Get in touch</p>
          <h1 className="mt-2 text-3xl font-bold tracking-tight text-brand-800 sm:text-4xl">Contact us</h1>
          <p className="mx-auto mt-3 max-w-2xl text-sm leading-relaxed text-gray-600">
            Whether you are integrating with our API or need help with an existing service, here is
            how to reach the right team.
          </p>
        </div>
      </section>

      {/* ---------------- Primary contacts ---------------- */}
      <section className="bg-white px-6 py-12">
        <div className="mx-auto grid max-w-6xl gap-6 sm:grid-cols-2 lg:grid-cols-4">
          <PrimaryCard
            title="Integration team"
            icon={
              <>
                <path d="m8 16-4-4 4-4" strokeLinecap="round" strokeLinejoin="round" />
                <path d="m16 8 4 4-4 4" strokeLinecap="round" strokeLinejoin="round" />
              </>
            }
          >
            <p>
              Questions about the <strong className="text-gray-800">Cash Bag Registration API</strong>?
            </p>
            <a
              href="mailto:BizTalkOps@nokas.no"
              className="inline-block font-semibold text-brand-700 underline-offset-2 hover:underline"
            >
              BizTalkOps@nokas.no
            </a>
          </PrimaryCard>

          <PrimaryCard
            title="Customer service"
            icon={
              <path d="M5 4h4l2 5-2.5 1.5a11 11 0 0 0 5 5L15 13l5 2v4a1 1 0 0 1-1 1A16 16 0 0 1 4 5a1 1 0 0 1 1-1Z" strokeLinejoin="round" />
            }
          >
            <p>Our team is here to help.</p>
            <a
              href="tel:+4791502580"
              className="inline-block font-semibold text-brand-700 underline-offset-2 hover:underline"
            >
              +47 915 02580
            </a>
          </PrimaryCard>

          <PrimaryCard
            title="Visiting address"
            icon={
              <>
                <path d="M12 21s7-5.5 7-11a7 7 0 1 0-14 0c0 5.5 7 11 7 11Z" strokeLinejoin="round" />
                <circle cx="12" cy="10" r="2.5" />
              </>
            }
          >
            <p className="font-medium text-gray-800">Nokas Value Management AS</p>
            <address className="not-italic">
              Alf Bjerckes vei 1
              <br />
              0582 Oslo
            </address>
          </PrimaryCard>

          <PrimaryCard
            title="Postal address"
            icon={
              <>
                <rect x="3" y="5" width="18" height="14" rx="2" />
                <path d="m3 7 9 6 9-6" strokeLinejoin="round" />
              </>
            }
          >
            <p className="font-medium text-gray-800">Nokas Value Management AS</p>
            <address className="not-italic">
              PO Box 384 Nydalen
              <br />
              0513 Oslo
            </address>
          </PrimaryCard>
        </div>
      </section>

      {/* ---------------- Locations ---------------- */}
      <section className="border-t border-gray-200 bg-gray-50 px-6 py-14">
        <div className="mx-auto max-w-6xl space-y-12">
          <div>
            <div className="mb-6 flex items-center gap-3">
              <h2 className="text-xl font-bold text-gray-900">
                Valuables transport
                <span className="ml-2 text-sm font-medium text-gray-500">Norway</span>
              </h2>
              <span className="h-px flex-1 bg-gray-200" />
            </div>
            <div className="grid grid-cols-1 gap-5 sm:grid-cols-2 lg:grid-cols-3">
              {norway.map((loc) => (
                <LocationCard key={loc.region} loc={loc} />
              ))}
            </div>
          </div>

          <div>
            <div className="mb-6 flex items-center gap-3">
              <h2 className="text-xl font-bold text-gray-900">
                Denmark
              </h2>
              <span className="h-px flex-1 bg-gray-200" />
            </div>
            <div className="grid grid-cols-1 gap-5 sm:grid-cols-2 lg:grid-cols-3">
              {denmark.map((loc) => (
                <LocationCard key={loc.region} loc={loc} />
              ))}
            </div>
          </div>
        </div>
      </section>
    </PublicLayout>
  );
}
