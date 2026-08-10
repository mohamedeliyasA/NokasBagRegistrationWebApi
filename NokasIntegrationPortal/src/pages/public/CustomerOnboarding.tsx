import type { ReactNode } from "react";
import { Link } from "react-router-dom";
import PublicLayout from "../../layouts/PublicLayout";

function OnboardingStep({
  index,
  title,
  isLast = false,
  children,
}: {
  index: number;
  title: string;
  isLast?: boolean;
  children: ReactNode;
}) {
  return (
    <li className="relative flex gap-5">
      {/* Rail + numbered marker */}
      <div className="flex flex-col items-center">
        <span className="z-10 flex h-10 w-10 shrink-0 items-center justify-center rounded-full bg-brand-800 text-sm font-bold text-white shadow-card">
          {index}
        </span>
        {!isLast && <span className="mt-1 w-px flex-1 bg-gradient-to-b from-brand-300 to-transparent" aria-hidden="true" />}
      </div>

      <div className="flex-1 pb-10">
        <div className="rounded-xl border border-gray-200 bg-white p-6 shadow-card transition hover:border-brand-300 hover:shadow-card-hover">
          <h2 className="text-base font-bold text-gray-900">{title}</h2>
          <div className="mt-2 space-y-2 text-sm leading-relaxed text-gray-600">{children}</div>
        </div>
      </div>
    </li>
  );
}

export default function CustomerOnboarding() {
  return (
    <PublicLayout>
      {/* ---------------- Header ---------------- */}
      <section className="border-b border-gray-200 bg-gradient-to-b from-brand-50 to-white px-6 py-14">
        <div className="mx-auto max-w-3xl text-center">
          <p className="text-xs font-semibold uppercase tracking-widest text-brand-600">
            Getting started
          </p>
          <h1 className="mt-2 text-3xl font-bold tracking-tight text-brand-800 sm:text-4xl">
            Customer onboarding
          </h1>
          <p className="mx-auto mt-3 max-w-2xl text-sm leading-relaxed text-gray-600">
            Three steps to go from first contact to a live integration.
          </p>
        </div>
      </section>

      {/* ---------------- What you get ---------------- */}
      <section className="bg-white px-6 py-12">
        <div className="mx-auto max-w-4xl">
          <h2 className="text-lg font-bold text-gray-900">What onboarding gives you</h2>
          <div className="mt-5 grid gap-4 sm:grid-cols-3">
            {[
              { title: "Submit registrations", body: "Use the Bag Registration API to declare bags before pickup." },
              { title: "Retrieve counting results", body: "Pull the counted amount and any deviation straight back into your systems." },
              { title: "Full documentation", body: "Complete integration specification with Swagger reference." },
            ].map((c) => (
              <div key={c.title} className="rounded-xl border border-gray-200 bg-gray-50 p-5">
                <h3 className="text-sm font-semibold text-brand-800">{c.title}</h3>
                <p className="mt-1 text-xs leading-relaxed text-gray-600">{c.body}</p>
              </div>
            ))}
          </div>
        </div>
      </section>

      {/* ---------------- Steps ---------------- */}
      <section className="border-t border-gray-200 bg-gray-50 px-6 py-14">
        <div className="mx-auto max-w-3xl">
          <h2 className="mb-8 text-lg font-bold text-gray-900">The onboarding steps</h2>

          <ol className="relative">
            <OnboardingStep index={1} title="Obtain API credentials">
              <p>
                To submit bag registrations and retrieve counting results you first need a{" "}
                <strong className="text-gray-800">Client ID</strong> and{" "}
                <strong className="text-gray-800">Client Secret</strong> issued by NOKAS.
              </p>
              <p>
                Don't have them yet? Email{" "}
                <a
                  href="mailto:BizTalkOps@nokas.no"
                  className="font-semibold text-brand-700 underline-offset-2 hover:underline"
                >
                  BizTalkOps@nokas.no
                </a>{" "}
                to request access.
              </p>
            </OnboardingStep>

            <OnboardingStep index={2} title="Register on the portal">
              <p>
                Once your credentials arrive, come back to this portal and choose{" "}
                <em className="text-gray-800">Launch Portal</em> or{" "}
                <em className="text-gray-800">Login</em> to begin registration.
              </p>
              <p>
                During sign-up you will be asked for your organisation details, including the Client
                ID you were assigned.
              </p>
            </OnboardingStep>

            <OnboardingStep index={3} title="Access orders & documentation" isLast>
              <p>After registering and signing in you will be able to:</p>
              <ul className="ml-1 space-y-1">
                {[
                  "Access the full API documentation and usage guidelines",
                  "View worked examples for every endpoint",
                  "Explore the interactive Swagger reference",
                ].map((item) => (
                  <li key={item} className="flex items-start gap-2">
                    <svg viewBox="0 0 20 20" fill="currentColor" className="mt-0.5 h-4 w-4 shrink-0 text-brand-600" aria-hidden="true">
                      <path
                        fillRule="evenodd"
                        d="M16.7 5.3a1 1 0 0 1 0 1.4l-7.5 7.5a1 1 0 0 1-1.4 0l-3.5-3.5a1 1 0 1 1 1.4-1.4l2.8 2.8 6.8-6.8a1 1 0 0 1 1.4 0Z"
                        clipRule="evenodd"
                      />
                    </svg>
                    <span>{item}</span>
                  </li>
                ))}
              </ul>
            </OnboardingStep>
          </ol>
        </div>
      </section>

      {/* ---------------- CTA ---------------- */}
      <section className="bg-white px-6 py-14">
        <div className="mx-auto max-w-3xl rounded-2xl border border-brand-200 bg-brand-50 p-8 text-center shadow-card">
          <h2 className="text-xl font-bold text-brand-800">Ready to begin?</h2>
          <p className="mx-auto mt-2 max-w-lg text-sm leading-relaxed text-gray-700">
            Request your credentials and we will get you set up.
          </p>
          <div className="mt-5 flex flex-wrap justify-center gap-3">
            <a
              href="mailto:BizTalkOps@nokas.no"
              className="rounded-full bg-brand-800 px-6 py-2.5 text-sm font-semibold text-white transition hover:bg-brand-700"
            >
              Request credentials
            </a>
            <Link
              to="/contact"
              className="rounded-full border border-brand-300 bg-white px-6 py-2.5 text-sm font-semibold text-brand-800 transition hover:bg-brand-50"
            >
              Contact us
            </Link>
          </div>
        </div>
      </section>
    </PublicLayout>
  );
}
