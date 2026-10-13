import { useEffect } from "react";
import { Link } from "react-router-dom";
import { PageHeader } from "@/components/ui";

const planned = [
  { title: "Live order tracking", body: "Follow every bag from registration through pickup to counted result." },
  { title: "Deviation alerts", body: "See at a glance where the counted amount differs from what was declared." },
  { title: "Historical reporting", body: "Filter and export your registration history for reconciliation." },
];

export default function BusinessActivityMonitoring() {
  useEffect(() => {
    document.title = "Portal | Business Activity Monitoring";
  }, []);

  return (
    <section className="space-y-8">
      <PageHeader
        eyebrow="Portal"
        title="Business Activity Monitoring"
        lead="A single view of your cash operations — currently in development."
      />

      <div className="rounded-2xl border border-dashed border-brand-300 bg-brand-50/60 p-10 text-center">
        <span className="mx-auto flex h-14 w-14 items-center justify-center rounded-2xl bg-brand-800 text-white">
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" className="h-7 w-7" aria-hidden="true">
            <path d="M3 12h4l3 8 4-16 3 8h4" strokeLinecap="round" strokeLinejoin="round" />
          </svg>
        </span>
        <h3 className="mt-5 text-lg font-bold text-brand-900">Coming soon</h3>
        <p className="mx-auto mt-2 max-w-md text-sm leading-relaxed text-gray-600">
          We are building a monitoring dashboard so you can track order status without leaving the
          portal. In the meantime, everything is available through the API.
        </p>
        <Link
          to="/portal/api-guide"
          className="mt-6 inline-flex items-center gap-1.5 rounded-full bg-brand-800 px-6 py-2.5 text-sm font-semibold text-white transition hover:bg-brand-700"
        >
          Browse the API endpoints
          <span aria-hidden="true">→</span>
        </Link>
      </div>

      <div className="grid gap-4 sm:grid-cols-3">
        {planned.map((p) => (
          <div key={p.title} className="rounded-xl border border-gray-200 bg-white p-5 shadow-card">
            <h4 className="text-sm font-semibold text-gray-900">{p.title}</h4>
            <p className="mt-1 text-xs leading-relaxed text-gray-600">{p.body}</p>
          </div>
        ))}
      </div>
    </section>
  );
}
